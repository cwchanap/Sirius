using GdUnit4;
using Godot;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuControllerTest : Node
{
    private SettingsMenuController _ctrl = null!;
    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();

        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        _ctrl = scene.Instantiate<SettingsMenuController>();
        _sceneTree.Root.AddChild(_ctrl);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_ctrl != null && GodotObject.IsInstanceValid(_ctrl))
            _ctrl.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _ctrl = null!;
        _sceneTree = null!;
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void SceneLoads_ControllerIsNotNull()
    {
        AssertThat(_ctrl).IsNotNull();
    }

    [TestCase]
    public void OpenSettings_ShowsController()
    {
        _ctrl.Hide();
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        AssertThat(_ctrl.Visible).IsTrue();
    }

    [TestCase]
    public void OnCancelPressed_EmitsClosed()
    {
        bool fired = false;
        _ctrl.Closed += () => fired = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "OnCancelPressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OpenSettings_SetsAudioSliderValues()
    {
        var data = SettingsData.CreateDefaults();
        data.MasterVolumePercent = 75;
        data.MusicVolumePercent  = 50;
        data.SfxVolumePercent    = 25;
        _ctrl.OpenSettings(data);

        AssertThat((int)GetField<HSlider>(_ctrl, "_masterSlider").Value).IsEqual(75);
        AssertThat((int)GetField<HSlider>(_ctrl, "_musicSlider").Value).IsEqual(50);
        AssertThat((int)GetField<HSlider>(_ctrl, "_sfxSlider").Value).IsEqual(25);
    }

    [TestCase]
    public void OpenSettings_SetsFullscreenCheckbox()
    {
        var data = SettingsData.CreateDefaults();
        data.FullscreenEnabled = true;
        _ctrl.OpenSettings(data);

        AssertThat(GetField<CheckBox>(_ctrl, "_fullscreenCheck").ButtonPressed).IsTrue();
    }

    [TestCase]
    public void OpenSettings_SetsResolutionPreset()
    {
        var data = SettingsData.CreateDefaults();
        data.ResolutionWidth  = 1920;
        data.ResolutionHeight = 1080;
        _ctrl.OpenSettings(data);

        // ResolutionPresets: index 0=640×360, 1=1280×720, 2=1920×1080, 3=2560×1440
        AssertThat(GetField<OptionButton>(_ctrl, "_resolutionOption").Selected).IsEqual(2);
    }

    [TestCase]
    public void OpenSettings_SetsDifficultyOption()
    {
        var data = SettingsData.CreateDefaults();
        data.Difficulty = "Hard";
        _ctrl.OpenSettings(data);

        // Difficulties: index 0=Easy, 1=Normal, 2=Hard
        AssertThat(GetField<OptionButton>(_ctrl, "_difficultyOption").Selected).IsEqual(2);
    }

    [TestCase]
    public void OpenSettings_SetsKeyButtonText()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.J;
        _ctrl.OpenSettings(data);

        AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text)
            .IsEqual(OS.GetKeycodeString(Key.J));
    }

    [TestCase]
    public void StartKeyCapture_SetsButtonTextToPrompt()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text).IsEqual("Press a key...");
    }

    [TestCase]
    public void InputDuringCapture_ValidKey_UpdatesEditedSettings()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.J, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.J);
    }

    [TestCase]
    public void InputDuringCapture_EscapeKey_CancelsAndRestoresPreviousText()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text)
            .IsEqual(OS.GetKeycodeString(Key.I));
    }

    [TestCase]
    public void InputDuringCapture_ReservedKey_ShowsError()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.W, Pressed = true });

        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Visible).IsTrue();
        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Text).IsEqual("Key reserved");
    }

    [TestCase]
    public void InputOutsideCaptureMode_IsIgnored()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);
        // Not in capture mode — non-ESC key press should be ignored

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.J, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public void EscapeKey_WhenNotCapturing_EmitsClosed()
    {
        bool closed = false;
        void OnClosed() => closed = true;
        _ctrl.Closed += OnClosed;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        _ctrl.Closed -= OnClosed;
        AssertThat(closed).IsTrue();
    }

    [TestCase]
    public void OnCancelPressed_EmitsClosed_WhenSettingsManagerIsNull()
    {
        // SettingsManager.Instance is null in unit tests (not autoloaded).
        // Cancel must never need it.
        bool closed = false;
        _ctrl.Closed += () => closed = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "OnCancelPressed");
        AssertThat(closed).IsTrue();
    }

    [TestCase]
    public void OnApplyPressed_WhenSettingsManagerNull_ShowsErrorAndDoesNotEmitClosed()
    {
        // SettingsManager.Instance is null in unit tests.
        bool closed = false;
        _ctrl.Closed += () => closed = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "OnApplyPressed");

        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Visible).IsTrue();
        AssertThat(closed).IsFalse();
    }

    [TestCase]
    public async Task OnApplyPressed_WhenSelectionsAreUnset_DoesNotIndexPastOptions()
    {
        SettingsManager.WindowGetModeOverride = () => DisplayServer.WindowMode.Windowed;
        SettingsManager.WindowGetSizeOverride = () => new Vector2I(640, 360);
        SettingsManager.WindowSetModeOverride = _ => { };
        SettingsManager.WindowSetSizeOverride = _ => { };

        var settingsManager = new SettingsManager();
        _sceneTree.Root.AddChild(settingsManager);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            bool closed = false;
            _ctrl.Closed += () => closed = true;
            _ctrl.OpenSettings(SettingsData.CreateDefaults());

            GetField<OptionButton>(_ctrl, "_resolutionOption").Selected = -1;
            GetField<OptionButton>(_ctrl, "_difficultyOption").Selected = -1;

            InvokePrivate(_ctrl, "OnApplyPressed");

            AssertThat(closed).IsTrue();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(settingsManager))
                settingsManager.QueueFree();
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            SettingsManager.WindowGetModeOverride = null;
            SettingsManager.WindowGetSizeOverride = null;
            SettingsManager.WindowSetModeOverride = null;
            SettingsManager.WindowSetSizeOverride = null;
        }
    }

    protected static void InvokePrivate(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        m.Invoke(obj, args);
    }

    protected static T GetField<T>(object obj, string field) where T : class
    {
        var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Field '{field}' not found.");
        return (T)f.GetValue(obj)!;
    }
}
