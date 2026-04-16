using Godot;
using System;
using System.Text.Json;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }
    private const string SettingsFile = "user://settings.json";
    private const string TempSettingsFile = "user://settings.json.tmp";
    private const string BackupSettingsFile = "user://settings.json.bak";
    private const int MinimumResolutionWidth = 640;
    private const int MinimumResolutionHeight = 360;
    private const int MaximumResolutionWidth = 7680;
    private const int MaximumResolutionHeight = 4320;

    // Keys that must not be used as remappable game action bindings.
    // Movement keys (W/A/S/D, arrows) are hard-coded in PlayerController._UnhandledInput()
    // and would be swallowed by Game._Input() if also mapped to an action.
    // UI keys (Escape, Enter, Space, Tab) back Godot's built-in ui_cancel / ui_accept /
    // ui_focus_next actions; binding a game action to one of these causes Game._Input()
    // to consume the event before AcceptDialog / SaveLoadDialog controls can see it.
    private static readonly System.Collections.Generic.HashSet<long> ReservedKeys = new()
    {
        (long)Key.W, (long)Key.A, (long)Key.S, (long)Key.D,
        (long)Key.Up, (long)Key.Down, (long)Key.Left, (long)Key.Right,
        (long)Key.Escape, (long)Key.Enter, (long)Key.KpEnter, (long)Key.Space, (long)Key.Tab
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private SettingsData _settings = SettingsData.CreateDefaults();
    internal DisplayServer.WindowMode LastAppliedWindowMode { get; private set; } = DisplayServer.WindowMode.Windowed;
    internal Vector2I LastAppliedWindowSize { get; private set; } = new(1280, 720);
    internal static Action<DisplayServer.WindowMode>? WindowSetModeOverride { get; set; }
    internal static Action<Vector2I>? WindowSetSizeOverride { get; set; }
    internal static Action<string, string, bool>? FileMoveWithOverwriteOverride { get; set; }
    internal static Action<string, string>? FileMoveOverride { get; set; }

    public override void _Ready()
    {
        if (Instance == null || !IsInstanceValid(Instance))
        {
            Instance = this;
            GetTree().NodeAdded += OnNodeAdded;
            LoadFromDisk();
            ApplyToRuntime(_settings);
            GD.Print("SettingsManager initialized as autoload singleton");
        }
        else
        {
            GD.Print("SettingsManager instance already exists, queueing free");
            QueueFree();
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (GetTree() != null)
        {
            GetTree().NodeAdded -= OnNodeAdded;
        }
    }

    public SettingsData GetSnapshot() => _settings.Clone();

    public SettingsData CreateDefaults() => SettingsData.CreateDefaults();

    public bool ApplyAndSave(SettingsData candidate)
    {
        if (!TryValidateForSave(candidate, out var validated))
        {
            return false;
        }

        // Apply to runtime first so we can detect platform failures (e.g. an
        // unsupported window mode) before persisting to disk.  If the runtime
        // rejects the setting, rolling back avoids saving a config that
        // doesn't match the live state and would fail again on next launch.
        var previousSettings = _settings;
        _settings = validated;
        ApplyToRuntime(_settings);

        var expectedMode = validated.FullscreenEnabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        var expectedSize = new Vector2I(validated.ResolutionWidth, validated.ResolutionHeight);
        if (LastAppliedWindowMode != expectedMode || LastAppliedWindowSize != expectedSize)
        {
            GD.PushWarning($"Window settings {expectedMode} at {expectedSize} were rejected by the platform. Settings not saved.");
            _settings = previousSettings;
            ApplyToRuntime(_settings);
            return false;
        }

        if (!SaveToFile(validated))
        {
            _settings = previousSettings;
            ApplyToRuntime(_settings);
            return false;
        }

        return true;
    }

    private void LoadFromDisk()
    {
        string? pathToRead = null;
        try
        {
            pathToRead = ResolveSettingsPathForLoad();
            if (pathToRead == null)
            {
                _settings = SettingsData.CreateDefaults();
                return;
            }

            using var file = FileAccess.Open(pathToRead, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                var openError = FileAccess.GetOpenError();
                if (pathToRead == SettingsFile && TryLoadBackupAfterPrimaryCorruption($"Failed to open settings file: {openError}"))
                {
                    return;
                }

                GD.PushWarning($"Failed to open settings file: {openError}");
                _settings = SettingsData.CreateDefaults();
                return;
            }

            var json = file.GetAsText();
            var loaded = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions);
            if (loaded == null)
            {
                // `null` JSON token deserializes without throwing, but the file is
                // still corrupt.  Try the backup before giving up on the player's
                // settings entirely.
                if (pathToRead == SettingsFile && TryLoadBackupAfterPrimaryCorruption("File deserialized to null"))
                {
                    return;
                }

                GD.PushError("Settings file deserialized to null — file may be corrupt or empty. Falling back to defaults.");
                _settings = SettingsData.CreateDefaults();
                if (!SaveToFile(_settings))
                {
                    GD.PushWarning("Failed to rewrite defaults after settings corruption was detected.");
                }
                return;
            }
            _settings = Sanitize(loaded);
            GD.Print("Settings loaded from disk");
        }
        catch (JsonException ex)
        {
            if (pathToRead == SettingsFile && TryLoadBackupAfterPrimaryCorruption(ex.Message))
            {
                return;
            }

            GD.PushError($"Failed to parse settings file. Falling back to defaults. {ex.Message}");
            _settings = SettingsData.CreateDefaults();
            if (!SaveToFile(_settings))
            {
                GD.PushWarning("Failed to rewrite defaults after settings corruption was detected.");
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"Unexpected error loading settings ({ex.GetType().Name}): {ex.Message}. Falling back to defaults.");
            _settings = SettingsData.CreateDefaults();
        }
    }

    private static SettingsData Sanitize(SettingsData data)
    {
        var defaults = SettingsData.CreateDefaults();
        var isResolutionValid = IsValidResolution(data.ResolutionWidth, data.ResolutionHeight);
        var sanitized = new SettingsData
        {
            Version = SettingsData.CurrentVersion,
            MasterVolumePercent = Mathf.Clamp(data.MasterVolumePercent, 0, 100),
            MusicVolumePercent = Mathf.Clamp(data.MusicVolumePercent, 0, 100),
            SfxVolumePercent = Mathf.Clamp(data.SfxVolumePercent, 0, 100),
            Difficulty = string.IsNullOrWhiteSpace(data.Difficulty) ? defaults.Difficulty : data.Difficulty,
            FullscreenEnabled = data.FullscreenEnabled,
            ResolutionWidth = isResolutionValid ? data.ResolutionWidth : defaults.ResolutionWidth,
            ResolutionHeight = isResolutionValid ? data.ResolutionHeight : defaults.ResolutionHeight,
            AutoSaveEnabled = data.AutoSaveEnabled,
            PrimaryKeybindings = NormalizeKeybindings(data.PrimaryKeybindings)
        };

        return sanitized;
    }

    private bool SaveToFile(SettingsData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            using var file = FileAccess.Open(TempSettingsFile, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"Failed to open settings file for writing: {TempSettingsFile}");
                return false;
            }

            file.StoreString(json);
            file.Flush();
            file.Close();

            var absoluteTargetPath = ProjectSettings.GlobalizePath(SettingsFile);
            var absoluteTempPath = ProjectSettings.GlobalizePath(TempSettingsFile);
            var absoluteBackupPath = ProjectSettings.GlobalizePath(BackupSettingsFile);

            // Safe rotation order:
            // 1. Rename current settings.json → settings.json.bak  (overwrites old backup)
            // 2. Rename temp → settings.json
            // At no point is the previous backup deleted before the new primary is confirmed.
            try
            {
                if (System.IO.File.Exists(absoluteTargetPath))
                {
                    MoveFile(absoluteTargetPath, absoluteBackupPath, overwrite: true);
                }

                MoveFile(absoluteTempPath, absoluteTargetPath);
            }
            catch (Exception renameEx) when (renameEx is System.IO.IOException or UnauthorizedAccessException)
            {
                GD.PushError($"Atomic rename of temp settings file failed: {renameEx.Message}");
                // If the backup move succeeded but the final rename failed, try to restore
                // the primary from the backup so the player doesn't lose their settings.
                if (!System.IO.File.Exists(absoluteTargetPath) && System.IO.File.Exists(absoluteBackupPath))
                {
                    try
                    {
                        System.IO.File.Copy(absoluteBackupPath, absoluteTargetPath, overwrite: true);
                    }
                    catch (Exception rollbackEx)
                    {
                        GD.PushError($"Settings rollback also failed — settings.json may be missing: {rollbackEx.Message}");
                    }
                }

                CleanupFileIfPresent(TempSettingsFile);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to save settings ({ex.GetType().Name}): {ex.Message}");
            CleanupFileIfPresent(TempSettingsFile);
            return false;
        }
    }

    private void ApplyToRuntime(SettingsData settings)
    {
        ApplyWindowMode(settings.FullscreenEnabled, settings.ResolutionWidth, settings.ResolutionHeight);
        ApplyAudioSettings(settings);
        ApplyInputBindings(settings);
        ApplyAutoSaveSetting(settings.AutoSaveEnabled);
    }

    private void ApplyWindowMode(bool fullscreenEnabled, int width, int height)
    {
        var targetMode = fullscreenEnabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        var targetSize = new Vector2I(width, height);

        try
        {
            SetWindowMode(targetMode);
            SetWindowSize(targetSize);
            LastAppliedWindowMode = targetMode;
            LastAppliedWindowSize = targetSize;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to apply window mode {targetMode} at {targetSize}: {ex.Message}");
            // Do not update LastApplied* — they must reflect what is actually applied
        }
    }

    private static void ApplyAudioSettings(SettingsData settings)
    {
        ApplyBusVolume(EnsureAudioBusExists("Master"), settings.MasterVolumePercent);
        ApplyBusVolume(EnsureAudioBusExists("Music"), settings.MusicVolumePercent);
        ApplyBusVolume(EnsureAudioBusExists("SFX"), settings.SfxVolumePercent);
    }

    private static void ApplyBusVolume(int busIndex, int volumePercent)
    {
        if (busIndex < 0)
        {
            return;
        }

        var linear = Mathf.Clamp(volumePercent / 100.0f, 0.0f, 1.0f);
        var decibels = linear <= 0.0f ? -80.0f : Mathf.LinearToDb(linear);
        AudioServer.SetBusVolumeDb(busIndex, decibels);
    }

    private static int EnsureAudioBusExists(string busName)
    {
        var busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex >= 0)
        {
            return busIndex;
        }

        GD.PushWarning($"Audio bus '{busName}' not found in project audio layout — creating it dynamically. Check Godot project audio settings.");
        AudioServer.AddBus(AudioServer.BusCount);
        var newIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(newIndex, busName);
        if (busName != "Master")
        {
            AudioServer.SetBusSend(newIndex, "Master");
        }

        return newIndex;
    }

    private static void ApplyInputBindings(SettingsData settings)
    {
        foreach (var binding in settings.PrimaryKeybindings)
        {
            if (!InputMap.HasAction(binding.Key))
            {
                InputMap.AddAction(binding.Key);
            }

            foreach (var inputEvent in InputMap.ActionGetEvents(binding.Key))
            {
                InputMap.ActionEraseEvent(binding.Key, inputEvent);
            }

            if (binding.Value > 0)
            {
                InputMap.ActionAddEvent(binding.Key, new InputEventKey
                {
                    PhysicalKeycode = (Key)binding.Value
                });
            }
            // else: action intentionally unbound — leave it with no events
        }

        // Mirror the pause_menu key onto ui_cancel so that AcceptDialog-based
        // modals (DialogueDialog, ShopDialog, HealDialog, BattleManager) close
        // with the same key the player configured for pause/cancel.
        // When pause_menu is unbound (-1), reset ui_cancel to the default
        // pause_menu key (Escape) so it never retains a stale binding from a
        // previous apply.
        var defaultPauseKey = SettingsData.CreateDefaultKeybindings()["pause_menu"];
        if (settings.PrimaryKeybindings.TryGetValue("pause_menu", out var pauseKey)
            && pauseKey > 0)
        {
            RebindAction("ui_cancel", (Key)pauseKey);
        }
        else
        {
            RebindAction("ui_cancel", (Key)defaultPauseKey);
        }
    }

    private static void RebindAction(string actionName, Key physicalKey)
    {
        if (!InputMap.HasAction(actionName))
        {
            return;
        }

        foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
        {
            InputMap.ActionEraseEvent(actionName, inputEvent);
        }

        InputMap.ActionAddEvent(actionName, new InputEventKey
        {
            PhysicalKeycode = physicalKey
        });
    }

    private static void ApplyAutoSaveSetting(bool autoSaveEnabled)
    {
        if (GameManager.Instance != null && IsInstanceValid(GameManager.Instance))
        {
            GameManager.Instance.AutoSaveEnabled = autoSaveEnabled;
        }
    }

    private static bool TryValidateForSave(SettingsData candidate, out SettingsData validated)
    {
        if (!IsValidResolution(candidate.ResolutionWidth, candidate.ResolutionHeight))
        {
            GD.PushWarning($"Resolution {candidate.ResolutionWidth}x{candidate.ResolutionHeight} is outside the allowed range " +
                           $"({MinimumResolutionWidth}x{MinimumResolutionHeight} – {MaximumResolutionWidth}x{MaximumResolutionHeight}). Settings not saved.");
            validated = SettingsData.CreateDefaults();
            return false;
        }

        validated = Sanitize(candidate);
        validated.ResolutionWidth = candidate.ResolutionWidth;
        validated.ResolutionHeight = candidate.ResolutionHeight;
        return true;
    }

    private static System.Collections.Generic.Dictionary<string, long> NormalizeKeybindings(System.Collections.Generic.Dictionary<string, long>? keybindings)
    {
        var normalized = SettingsData.CreateDefaultKeybindings();
        if (keybindings == null)
        {
            return normalized;
        }

        foreach (var (actionName, defaultKeycode) in SettingsData.CreateDefaultKeybindings())
        {
            if (keybindings.TryGetValue(actionName, out var keycode) && (keycode == -1 || IsValidKeycode(keycode)))
            {
                normalized[actionName] = keycode;
            }
            else
            {
                normalized[actionName] = defaultKeycode;
            }
        }

        // Reject reserved keys (movement + built-in UI keys).  If a managed action
        // collides with one, reset it to default so movement and dialog controls
        // remain functional.
        //
        // Exception: pause_menu is allowed to use Escape because it only backs
        // ui_cancel — the same action ApplyInputBindings mirrors pause_menu onto
        // — so there is no dual-action conflict.  Enter/Space/Tab are rejected
        // because they also back ui_accept / ui_focus_next; mirroring pause_menu
        // onto ui_cancel for those keys means a single press simultaneously
        // confirms AND cancels modals (NPC AcceptDialog, SaveLoadDialog, etc.).
        // Movement keys are still rejected for pause_menu because Game._Input()
        // would swallow them before PlayerController can process movement.
        var defaultsForReserved = SettingsData.CreateDefaultKeybindings();
        foreach (var actionName in new System.Collections.Generic.List<string>(normalized.Keys))
        {
            var keycode = normalized[actionName];
            if (keycode > 0 && ReservedKeys.Contains(keycode))
            {
                if (actionName == "pause_menu" && keycode == (long)Key.Escape)
                {
                    continue;
                }

                var defaultKey = defaultsForReserved[actionName];
                if (ReservedKeys.Contains(defaultKey))
                {
                    GD.PushWarning($"Keybinding '{actionName}' uses reserved key {keycode} and default {defaultKey} is also reserved. Unbinding.");
                    normalized[actionName] = -1;
                }
                else
                {
                    GD.PushWarning($"Keybinding '{actionName}' uses reserved key {keycode}. Resetting to default {defaultKey}.");
                    normalized[actionName] = defaultKey;
                }
            }
        }

        // Reject duplicate keys: if two actions share the same keycode, reset
        // the later one to its default. If the default is also taken, unbind it.
        // Repeat until stable (bounded by action count) to handle cascading conflicts,
        // e.g. toggle_inventory→E forces interact→default(E) → still duplicate.
        var defaultBindings = SettingsData.CreateDefaultKeybindings();
        var actionOrder = new System.Collections.Generic.List<string>(defaultBindings.Keys);
        int maxPasses = actionOrder.Count + 1;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool changed = false;
            var seenKeys = new System.Collections.Generic.HashSet<long>();
            foreach (var actionName in actionOrder)
            {
                var keycode = normalized[actionName];
                if (keycode <= 0)
                {
                    continue; // unbound actions cannot conflict with each other
                }
                if (!seenKeys.Add(keycode))
                {
                    var resolvedKeycode = defaultBindings[actionName];
                    // Check if the default is already taken in seenKeys OR in normalized (not yet processed)
                    bool defaultTaken = seenKeys.Contains(resolvedKeycode);
                    if (!defaultTaken)
                    {
                        foreach (var otherAction in actionOrder)
                        {
                            if (otherAction != actionName && normalized[otherAction] == resolvedKeycode && resolvedKeycode > 0)
                            {
                                defaultTaken = true;
                                break;
                            }
                        }
                    }

                    if (defaultTaken)
                    {
                        // Default is also taken — unbind to avoid any conflict.
                        GD.PushWarning($"Duplicate keybinding: '{actionName}' keycode {keycode} conflicts and default {resolvedKeycode} is also taken. Unbinding.");
                        resolvedKeycode = -1;
                    }
                    else
                    {
                        GD.PushWarning($"Duplicate keybinding: '{actionName}' shares keycode {keycode} with another action. Resetting to default {resolvedKeycode}.");
                    }
                    normalized[actionName] = resolvedKeycode;
                    changed = true;
                }
            }
            if (!changed) break;
        }

        // Required actions must never be left unbound.  For each, if the
        // duplicate-resolution loop set it to -1, try to force it back to its
        // default key — unless another action already claimed that key, in which
        // case we log a warning and leave it unbound (the player must reassign
        // manually via the settings menu).

        // pause_menu mirrors onto ui_cancel (AcceptDialog dismiss).  Without it,
        // every modal in the game becomes unclosable.
        if (normalized["pause_menu"] == -1)
        {
            ForceDefaultIfAvailable(normalized, actionOrder, "pause_menu", defaultBindings);
        }

        // interact gates stair traversal (PlayerController._UnhandledInput).
        // Leaving it unbound prevents floor changes and blocks game progress.
        if (normalized["interact"] == -1)
        {
            ForceDefaultIfAvailable(normalized, actionOrder, "interact", defaultBindings);
        }

        return normalized;
    }

    private static void ForceDefaultIfAvailable(
        System.Collections.Generic.Dictionary<string, long> normalized,
        System.Collections.Generic.List<string> actionOrder,
        string actionName,
        System.Collections.Generic.Dictionary<string, long> defaultBindings)
    {
        var defaultKey = defaultBindings[actionName];
        bool defaultTaken = false;
        foreach (var other in actionOrder)
        {
            if (other != actionName && normalized[other] == defaultKey)
            {
                defaultTaken = true;
                break;
            }
        }

        if (!defaultTaken)
        {
            GD.PushWarning($"{actionName} resolved to unbound; forcing back to default {defaultKey}.");
            normalized[actionName] = defaultKey;
        }
        else
        {
            GD.PushWarning($"{actionName} resolved to unbound and its default {defaultKey} is already taken by another action. Leaving unbound — the player must reassign {actionName} manually.");
        }
    }

    private static bool IsValidResolution(int width, int height) =>
        width >= MinimumResolutionWidth &&
        height >= MinimumResolutionHeight &&
        width <= MaximumResolutionWidth &&
        height <= MaximumResolutionHeight;

    private static bool IsValidKeycode(long value)
    {
        if (value <= 0 || value > int.MaxValue)
        {
            return false;
        }

        return Enum.IsDefined(typeof(Key), (Key)value);
    }

    private static bool IsMovementKey(long keycode) =>
        keycode == (long)Key.W || keycode == (long)Key.A ||
        keycode == (long)Key.S || keycode == (long)Key.D ||
        keycode == (long)Key.Up || keycode == (long)Key.Down ||
        keycode == (long)Key.Left || keycode == (long)Key.Right;

    private static void SetWindowMode(DisplayServer.WindowMode mode)
    {
        if (WindowSetModeOverride != null)
        {
            WindowSetModeOverride(mode);
            return;
        }

        DisplayServer.WindowSetMode(mode);
    }

    private static void SetWindowSize(Vector2I size)
    {
        if (WindowSetSizeOverride != null)
        {
            WindowSetSizeOverride(size);
            return;
        }

        DisplayServer.WindowSetSize(size);
    }

    private static void MoveFile(string sourcePath, string destinationPath, bool overwrite)
    {
        if (FileMoveWithOverwriteOverride != null)
        {
            FileMoveWithOverwriteOverride(sourcePath, destinationPath, overwrite);
            return;
        }

        System.IO.File.Move(sourcePath, destinationPath, overwrite);
    }

    private static void MoveFile(string sourcePath, string destinationPath)
    {
        if (FileMoveOverride != null)
        {
            FileMoveOverride(sourcePath, destinationPath);
            return;
        }

        System.IO.File.Move(sourcePath, destinationPath);
    }

    private static void CleanupFileIfPresent(string userPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(userPath);
        if (!System.IO.File.Exists(absolutePath)) return;
        try
        {
            System.IO.File.Delete(absolutePath);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to clean up temp settings file '{userPath}': {ex.Message}");
        }
    }

    private string? ResolveSettingsPathForLoad()
    {
        if (FileAccess.FileExists(SettingsFile))
        {
            return SettingsFile;
        }

        if (!FileAccess.FileExists(BackupSettingsFile))
        {
            return null;
        }

        try
        {
            var backupPath = ProjectSettings.GlobalizePath(BackupSettingsFile);
            var targetPath = ProjectSettings.GlobalizePath(SettingsFile);
            System.IO.File.Move(backupPath, targetPath);
            return SettingsFile;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to restore settings backup, reading backup directly: {ex.Message}");
            return BackupSettingsFile;
        }
    }

    private bool TryLoadBackupAfterPrimaryCorruption(string primaryFailureReason)
    {
        if (!FileAccess.FileExists(BackupSettingsFile))
        {
            return false;
        }

        try
        {
            using var backupFile = FileAccess.Open(BackupSettingsFile, FileAccess.ModeFlags.Read);
            if (backupFile == null)
            {
                return false;
            }

            var backupJson = backupFile.GetAsText();
            var backupSettings = JsonSerializer.Deserialize<SettingsData>(backupJson, JsonOptions);
            if (backupSettings == null)
            {
                GD.PushWarning("Backup settings file deserialized to null — treating backup as corrupt. Falling back to defaults.");
                return false;
            }

            GD.PushWarning($"Primary settings file was corrupt. Restoring from backup. {primaryFailureReason}");
            _settings = Sanitize(backupSettings);

            // Delete the corrupt primary before saving so that SaveToFile's rotation
            // does not overwrite the good backup with the corrupt file.
            CleanupFileIfPresent(SettingsFile);

            if (!SaveToFile(_settings))
            {
                GD.PushWarning("Failed to rewrite settings after recovering from backup.");
            }

            return true;
        }
        catch (JsonException backupEx)
        {
            GD.PushError($"Backup settings file is also corrupt — both primary and backup are unreadable. " +
                         $"Primary error: {primaryFailureReason} | Backup error: {backupEx.Message}. Falling back to defaults.");
            return false;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to recover settings from backup: {ex.Message}");
            return false;
        }
    }

    private void OnNodeAdded(Node node)
    {
        if (node is GameManager gameManager)
        {
            gameManager.AutoSaveEnabled = _settings.AutoSaveEnabled;
        }
    }
}
