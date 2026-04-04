using Godot;
using System;
using System.Text.Json;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance { get; private set; }
    private const string SettingsFile = "user://settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private SettingsData _settings = SettingsData.CreateDefaults();

    public override void _Ready()
    {
        if (Instance == null || !IsInstanceValid(Instance))
        {
            Instance = this;
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
    }

    public SettingsData GetSnapshot() => _settings.Clone();

    public SettingsData CreateDefaults() => SettingsData.CreateDefaults();

    public bool ApplyAndSave(SettingsData candidate)
    {
        var sanitized = Sanitize(candidate);
        if (!SaveToFile(sanitized))
        {
            return false;
        }

        _settings = sanitized;
        ApplyToRuntime(_settings);
        return true;
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!FileAccess.FileExists(SettingsFile))
            {
                _settings = SettingsData.CreateDefaults();
                return;
            }

            using var file = FileAccess.Open(SettingsFile, FileAccess.ModeFlags.Read);
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
        catch (Exception ex)
        {
            GD.PushWarning($"Failed to load settings (using defaults): {ex.Message}");
            _settings = SettingsData.CreateDefaults();
        }
    }

    private static SettingsData Sanitize(SettingsData data)
    {
        var defaults = SettingsData.CreateDefaults();
        var sanitized = new SettingsData
        {
            Version = data.Version,
            MasterVolumePercent = Mathf.Clamp(data.MasterVolumePercent, 0, 100),
            MusicVolumePercent = Mathf.Clamp(data.MusicVolumePercent, 0, 100),
            SfxVolumePercent = Mathf.Clamp(data.SfxVolumePercent, 0, 100),
            Difficulty = string.IsNullOrEmpty(data.Difficulty) ? "Normal" : data.Difficulty,
            FullscreenEnabled = data.FullscreenEnabled,
            ResolutionWidth = data.ResolutionWidth > 0 ? data.ResolutionWidth : 1280,
            ResolutionHeight = data.ResolutionHeight > 0 ? data.ResolutionHeight : 720,
            AutoSaveEnabled = data.AutoSaveEnabled,
            PrimaryKeybindings = new System.Collections.Generic.Dictionary<string, long>()
        };

        // Validate each keybinding: positive value = valid key, otherwise fall back to default.
        foreach (var (action, keyValue) in data.PrimaryKeybindings)
        {
            if (keyValue > 0)
                sanitized.PrimaryKeybindings[action] = keyValue;
            else if (defaults.PrimaryKeybindings.TryGetValue(action, out var defaultKey))
                sanitized.PrimaryKeybindings[action] = defaultKey;
        }

        // Ensure all default actions have a binding.
        foreach (var (action, defaultKey) in defaults.PrimaryKeybindings)
        {
            if (!sanitized.PrimaryKeybindings.ContainsKey(action))
                sanitized.PrimaryKeybindings[action] = defaultKey;
        }

        return sanitized;
    }

    private bool SaveToFile(SettingsData data)
    {
        var tempFile = $"{SettingsFile}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            using var file = FileAccess.Open(tempFile, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"Failed to open settings file for writing: {tempFile}");
                return false;
            }

            file.StoreString(json);
            file.Flush();
            file.Close();

            var absoluteTargetPath = ProjectSettings.GlobalizePath(SettingsFile);
            var absoluteTempPath = ProjectSettings.GlobalizePath(tempFile);
            if (System.IO.File.Exists(absoluteTargetPath))
            {
                System.IO.File.Delete(absoluteTargetPath);
            }

            System.IO.File.Move(absoluteTempPath, absoluteTargetPath);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to save settings: {ex.Message}");
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

    private static void ApplyWindowMode(bool fullscreenEnabled, int width, int height)
    {
        DisplayServer.WindowSetMode(fullscreenEnabled
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetSize(new Vector2I(width, height));
    }

    private static void ApplyAudioSettings(SettingsData settings)
    {
        ApplyBusVolume("Master", settings.MasterVolumePercent);
        ApplyBusVolume("Music", settings.MusicVolumePercent);
        ApplyBusVolume("SFX", settings.SfxVolumePercent);
    }

    private static void ApplyBusVolume(string busName, int volumePercent)
    {
        var busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
        {
            return;
        }

        var linear = Mathf.Clamp(volumePercent / 100.0f, 0.0f, 1.0f);
        var decibels = linear <= 0.0f ? -80.0f : Mathf.LinearToDb(linear);
        AudioServer.SetBusVolumeDb(busIndex, decibels);
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
                PhysicalKeycode = (Key)binding.Value,
                Keycode = (Key)binding.Value
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
}
