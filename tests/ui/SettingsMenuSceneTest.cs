using System.Reflection;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuSceneTest : Node
{
    private static readonly string[] RequiredUniqueNodes =
    {
        "%ModalShell",
        "%SettingsFrame",
        "%PageSelector",
        "%PageDeck",
        "%AudioPageButton",
        "%DisplayPageButton",
        "%GameplayPageButton",
        "%ControlsPageButton",
        "%AudioScroll",
        "%DisplayScroll",
        "%GameplayScroll",
        "%ControlsScroll",
        "%AudioRows",
        "%DisplayRows",
        "%GameplayRows",
        "%ControlsRows",

        "%MasterVolumeLabel",
        "%MasterSlider",
        "%MasterValueLabel",
        "%MusicVolumeLabel",
        "%MusicSlider",
        "%MusicValueLabel",
        "%SfxVolumeLabel",
        "%SfxSlider",
        "%SfxValueLabel",

        "%FullscreenLabel",
        "%FullscreenCheck",
        "%ResolutionLabel",
        "%ResolutionOption",

        "%DifficultyLabel",
        "%DifficultyOption",
        "%AutoSaveLabel",
        "%AutoSaveCheck",

        "%InventoryKeyLabel",
        "%InventoryKeyButton",
        "%InteractKeyLabel",
        "%InteractKeyButton",
        "%PauseKeyLabel",
        "%PauseKeyButton",

        "%ErrorPanel",
        "%ErrorLabel",
        "%ApplyButton",
        "%CancelButton"
    };

    [TestCase]
    public void SceneOwnsSettingsControlsBeforeReady()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        AssertThat(packed).IsNotNull();

        var screen = packed!.Instantiate<SettingsMenuController>();
        try
        {
            foreach (var path in RequiredUniqueNodes)
                AssertThat(screen.GetNodeOrNull(path)).IsNotNull();
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public void ControllerHasNoRuntimeLayoutBuilders()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildUI", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildAudioTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildDisplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildGameplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildControlsTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddSliderRow", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddKeyRow", flags)).IsNull();
    }
}
