using GdUnit4;
using Godot;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
public partial class SettingsDataTest
{
    [TestCase]
    public void SettingsData_DefaultConstructor_PopulatesKeybindings()
    {
        var data = new SettingsData();

        AssertThat(data.PrimaryKeybindings).IsNotNull();
        AssertThat(data.PrimaryKeybindings.Count).IsEqual(3);
        AssertThat(data.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(data.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        AssertThat(data.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public void SettingsData_DefaultConstructor_MatchesCreateDefaults()
    {
        var fromConstructor = new SettingsData();
        var fromFactory = SettingsData.CreateDefaults();

        AssertThat(fromConstructor.MasterVolumePercent).IsEqual(fromFactory.MasterVolumePercent);
        AssertThat(fromConstructor.MusicVolumePercent).IsEqual(fromFactory.MusicVolumePercent);
        AssertThat(fromConstructor.SfxVolumePercent).IsEqual(fromFactory.SfxVolumePercent);
        AssertThat(fromConstructor.Difficulty).IsEqual(fromFactory.Difficulty);
        AssertThat(fromConstructor.FullscreenEnabled).IsEqual(fromFactory.FullscreenEnabled);
        AssertThat(fromConstructor.ResolutionWidth).IsEqual(fromFactory.ResolutionWidth);
        AssertThat(fromConstructor.ResolutionHeight).IsEqual(fromFactory.ResolutionHeight);
        AssertThat(fromConstructor.AutoSaveEnabled).IsEqual(fromFactory.AutoSaveEnabled);

        foreach (var key in fromFactory.PrimaryKeybindings.Keys)
        {
            AssertThat(fromConstructor.PrimaryKeybindings.ContainsKey(key)).IsTrue();
            AssertThat(fromConstructor.PrimaryKeybindings[key]).IsEqual(fromFactory.PrimaryKeybindings[key]);
        }
    }

    [TestCase]
    public void SettingsData_Clone_ProducesIndependentKeybindings()
    {
        var original = new SettingsData();
        var cloned = original.Clone();

        cloned.PrimaryKeybindings["toggle_inventory"] = (long)Key.Q;

        AssertThat(original.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(cloned.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Q);
    }

    [TestCase]
    public void SettingsData_CreateDefaultKeybindings_ReturnsNewDictionaryEachCall()
    {
        var first = SettingsData.CreateDefaultKeybindings();
        var second = SettingsData.CreateDefaultKeybindings();

        first["toggle_inventory"] = 0;

        AssertThat(second["toggle_inventory"]).IsEqual((long)Key.I);
    }
}
