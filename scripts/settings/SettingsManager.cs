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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private SettingsData _settings = SettingsData.CreateDefaults();
    internal DisplayServer.WindowMode LastAppliedWindowMode { get; private set; } = DisplayServer.WindowMode.Windowed;
    internal Vector2I LastAppliedWindowSize { get; private set; } = new(1280, 720);

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

        if (!SaveToFile(validated))
        {
            return false;
        }

        _settings = validated;
        ApplyToRuntime(_settings);
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
                GD.PushWarning($"Failed to open settings file: {FileAccess.GetOpenError()}");
                _settings = SettingsData.CreateDefaults();
                return;
            }

            var json = file.GetAsText();
            var loaded = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions);
            _settings = loaded == null ? SettingsData.CreateDefaults() : Sanitize(loaded);
            GD.Print("Settings loaded from disk");
        }
        catch (JsonException ex)
        {
            if (pathToRead == SettingsFile && TryLoadBackupAfterPrimaryCorruption(ex))
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
            GD.PushWarning($"Failed to load settings (using defaults): {ex.Message}");
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

            if (System.IO.File.Exists(absoluteBackupPath))
            {
                System.IO.File.Delete(absoluteBackupPath);
            }

            if (System.IO.File.Exists(absoluteTargetPath))
            {
                System.IO.File.Move(absoluteTargetPath, absoluteBackupPath);
            }

            try
            {
                System.IO.File.Move(absoluteTempPath, absoluteTargetPath);
            }
            catch
            {
                if (!System.IO.File.Exists(absoluteTargetPath) && System.IO.File.Exists(absoluteBackupPath))
                {
                    System.IO.File.Move(absoluteBackupPath, absoluteTargetPath);
                }

                throw;
            }

            TryDeleteBackup(absoluteBackupPath);

            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to save settings: {ex.Message}");
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
        LastAppliedWindowMode = fullscreenEnabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed;
        LastAppliedWindowSize = new Vector2I(width, height);

        DisplayServer.WindowSetMode(fullscreenEnabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
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

            InputMap.ActionAddEvent(binding.Key, new InputEventKey
            {
                PhysicalKeycode = (Key)binding.Value
            });
        }
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
            if (keybindings.TryGetValue(actionName, out var keycode) && IsValidKeycode(keycode))
            {
                normalized[actionName] = keycode;
            }
            else
            {
                normalized[actionName] = defaultKeycode;
            }
        }

        // Reject duplicate keys: if two actions share the same keycode, reset
        // the later one to its default to prevent one key consuming events for another.
        var seenKeys = new System.Collections.Generic.HashSet<long>();
        var defaultBindings = SettingsData.CreateDefaultKeybindings();
        foreach (var (actionName, _) in defaultBindings)
        {
            var keycode = normalized[actionName];
            if (!seenKeys.Add(keycode))
            {
                GD.PushWarning($"Duplicate keybinding: '{actionName}' shares keycode {keycode} with another action. Resetting to default.");
                normalized[actionName] = defaultBindings[actionName];
            }
        }

        return normalized;
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

    private static void CleanupFileIfPresent(string userPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(userPath);
        if (System.IO.File.Exists(absolutePath))
        {
            System.IO.File.Delete(absolutePath);
        }
    }

    private static void TryDeleteBackup(string absoluteBackupPath)
    {
        if (!System.IO.File.Exists(absoluteBackupPath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(absoluteBackupPath);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to delete settings backup after successful save: {ex.Message}");
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

    private bool TryLoadBackupAfterPrimaryCorruption(JsonException primaryException)
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
                return false;
            }

            GD.PushWarning($"Primary settings file was corrupt. Restoring from backup. {primaryException.Message}");
            _settings = Sanitize(backupSettings);
            if (!SaveToFile(_settings))
            {
                GD.PushWarning("Failed to rewrite settings after recovering from backup.");
            }

            return true;
        }
        catch (JsonException)
        {
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
