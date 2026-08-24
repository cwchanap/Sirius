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
    public bool ReducedMotionEnabled { get; set; }
    public Dictionary<string, long> PrimaryKeybindings { get; set; } = CreateDefaultKeybindings();

    public static Dictionary<string, long> CreateDefaultKeybindings()
    {
        return new Dictionary<string, long>
        {
            ["toggle_inventory"] = (long)Key.I,
            ["interact"] = (long)Key.E,
            ["pause_menu"] = (long)Key.Escape
        };
    }

    public static SettingsData CreateDefaults() => new();

    public SettingsData Clone()
    {
        // Shallow copy covers every value-type/immutable field automatically,
        // so future plain properties clone without touching this method.
        // PrimaryKeybindings is the only mutable reference field and gets an
        // explicit copy below.
        var clone = (SettingsData)MemberwiseClone();
        clone.PrimaryKeybindings = PrimaryKeybindings is null
            ? CreateDefaultKeybindings()
            : new Dictionary<string, long>(PrimaryKeybindings);
        return clone;
    }
}
