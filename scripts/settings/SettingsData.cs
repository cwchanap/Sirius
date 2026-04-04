using Godot;
using System.Collections.Generic;

public class SettingsData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public int MasterVolumePercent { get; set; } = 100;
    public int MusicVolumePercent { get; set; } = 100;
    public int SfxVolumePercent { get; set; } = 100;
    public string Difficulty { get; set; } = "Normal";
    public bool FullscreenEnabled { get; set; }
    public int ResolutionWidth { get; set; } = 1280;
    public int ResolutionHeight { get; set; } = 720;
    public bool AutoSaveEnabled { get; set; } = true;
    public Dictionary<string, long> PrimaryKeybindings { get; set; } = new();

    public static SettingsData CreateDefaults()
    {
        return new SettingsData
        {
            PrimaryKeybindings = new Dictionary<string, long>
            {
                ["toggle_inventory"] = (long)Key.I,
                ["interact"] = (long)Key.E,
                ["pause_menu"] = (long)Key.Escape
            }
        };
    }

    public SettingsData Clone()
    {
        return new SettingsData
        {
            Version = Version,
            MasterVolumePercent = MasterVolumePercent,
            MusicVolumePercent = MusicVolumePercent,
            SfxVolumePercent = SfxVolumePercent,
            Difficulty = Difficulty,
            FullscreenEnabled = FullscreenEnabled,
            ResolutionWidth = ResolutionWidth,
            ResolutionHeight = ResolutionHeight,
            AutoSaveEnabled = AutoSaveEnabled,
            PrimaryKeybindings = new Dictionary<string, long>(PrimaryKeybindings)
        };
    }
}
