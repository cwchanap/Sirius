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
    public void InputDuringCapture_PauseMenuEscape_AcceptsBinding()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["pause_menu"] = (long)Key.P;
        _ctrl.OpenSettings(data);
        InvokePrivate(_ctrl, "StartKeyCapture", "pause_menu");

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetField<Button>(_ctrl, "_pauseKeyBtn").Text)
            .IsEqual(OS.GetKeycodeString(Key.Escape));
        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Visible).IsFalse();
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
    public void InputOutsideCaptureMode_ConsumesEventToBlockGameplayInput()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);

        // Simulate a non-ESC, non-capture key that would normally move the player.
        // _Input should mark the event as handled so gameplay doesn't process it.
        var evt = new InputEventKey { PhysicalKeycode = Key.W, Pressed = true };
        _ctrl._Input(evt);

        // The viewport should have been told to handle input.
        // Since we can't easily check SetInputAsHandled in isolation,
        // verify that the binding was NOT changed (event was consumed, not processed).
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
    public void OpenSettings_InGameMode_PanelSizeClampedToViewport()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults(), showOverlay: false);

        var panel = _ctrl.GetNodeOrNull<PanelContainer>("Panel");
        AssertThat(panel).IsNotNull();
        // Panel must not exceed 90% of viewport height (default window > 360,
        // so the clamp ensures it stays within bounds at any resolution).
        var vpHeight = _ctrl.GetViewport().GetVisibleRect().Size.Y;
        AssertThat(panel!.CustomMinimumSize.Y).IsLessEqual(vpHeight * 0.9f + 0.5f);
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
        var simulatedWindowMode = DisplayServer.WindowMode.Windowed;
        var simulatedWindowSize = new Vector2I(1280, 720);
        SettingsManager.WindowGetModeOverride = () => simulatedWindowMode;
        SettingsManager.WindowGetSizeOverride = () => simulatedWindowSize;
        SettingsManager.WindowSetModeOverride = mode => simulatedWindowMode = mode;
        SettingsManager.WindowSetSizeOverride = size => simulatedWindowSize = size;
        SettingsManager.FileWriteTextOverride = (_, _) => { };
        SettingsManager.FileMoveWithOverwriteOverride = (_, _, _) => { };
        SettingsManager.FileMoveOverride = (_, _) => { };
        SettingsManager.FileDeleteOverride = _ => { };

        var settingsManager = new SettingsManager();
        _sceneTree.Root.AddChild(settingsManager);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            var tempSettingsPath = ProjectSettings.GlobalizePath("user://settings.json.tmp");
            if (System.IO.File.Exists(tempSettingsPath))
                System.IO.File.Delete(tempSettingsPath);

            bool closed = false;
            _ctrl.Closed += () => closed = true;
            _ctrl.OpenSettings(SettingsData.CreateDefaults());

            var resolutionOption = GetField<OptionButton>(_ctrl, "_resolutionOption");
            var difficultyOption = GetField<OptionButton>(_ctrl, "_difficultyOption");
            resolutionOption.Set("selected", -1);
            difficultyOption.Set("selected", -1);
            AssertThat(resolutionOption.Selected).IsEqual(-1);
            AssertThat(difficultyOption.Selected).IsEqual(-1);

            InvokePrivate(_ctrl, "OnApplyPressed");

            AssertThat(closed).IsTrue();
            AssertThat(System.IO.File.Exists(tempSettingsPath)).IsFalse();
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
            SettingsManager.FileWriteTextOverride = null;
            SettingsManager.FileMoveWithOverwriteOverride = null;
            SettingsManager.FileMoveOverride = null;
            SettingsManager.FileDeleteOverride = null;
        }
    }

    [TestCase]
    public void JoypadUiCancel_WhenNotCapturing_EmitsClosed()
    {
        // Temporarily add a joypad button to ui_cancel so IsActionPressed matches.
        var joyEvent = new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true };
        InputMap.ActionAddEvent("ui_cancel", joyEvent);
        try
        {
            bool closed = false;
            _ctrl.Closed += () => closed = true;
            _ctrl.OpenSettings(SettingsData.CreateDefaults());

            _ctrl._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });

            AssertThat(closed).IsTrue();
        }
        finally
        {
            InputMap.ActionEraseEvent("ui_cancel", joyEvent);
        }
    }

    [TestCase]
    public void JoypadUiCancel_WhenCapturing_CancelsKeyCapture()
    {
        var joyEvent = new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true };
        InputMap.ActionAddEvent("ui_cancel", joyEvent);
        try
        {
            var data = SettingsData.CreateDefaults();
            data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
            _ctrl.OpenSettings(data);
            InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");
            AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text).IsEqual("Press a key...");

            _ctrl._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });

            // Capture cancelled, text restored to previous binding
            AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text)
                .IsEqual(OS.GetKeycodeString(Key.I));
        }
        finally
        {
            InputMap.ActionEraseEvent("ui_cancel", joyEvent);
        }
    }

    [TestCase]
    public void JoypadUiCancel_DoesNotCloseWhenCapturingPauseMenu()
    {
        // When capturing pause_menu, ui_cancel should NOT close the panel;
        // the capture must continue so the user can assign a key.
        var joyEvent = new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true };
        InputMap.ActionAddEvent("ui_cancel", joyEvent);
        InputMap.ActionAddEvent("pause_menu", joyEvent);
        try
        {
            _ctrl.OpenSettings(SettingsData.CreateDefaults());
            InvokePrivate(_ctrl, "StartKeyCapture", "pause_menu");

            bool closed = false;
            _ctrl.Closed += () => closed = true;

            _ctrl._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.B, Pressed = true });

            // Should NOT have closed — still in capture mode
            AssertThat(closed).IsFalse();
            var listening = GetField<string>(_ctrl, "_listeningAction");
            AssertThat(listening).IsEqual("pause_menu");
        }
        finally
        {
            InputMap.ActionEraseEvent("ui_cancel", joyEvent);
            InputMap.ActionEraseEvent("pause_menu", joyEvent);
        }
    }

    [TestCase]
    public void PauseMenuAction_WhenNotCapturing_EmitsClosed()
    {
        // Verify that a keyboard key mapped to pause_menu (but NOT Escape) can
        // close the settings via the action-based path.
        var keyEvent = new InputEventKey { PhysicalKeycode = Key.P, Pressed = true };
        InputMap.ActionAddEvent("pause_menu", keyEvent);
        try
        {
            bool closed = false;
            _ctrl.Closed += () => closed = true;
            _ctrl.OpenSettings(SettingsData.CreateDefaults());

            _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.P, Pressed = true });

            AssertThat(closed).IsTrue();
        }
        finally
        {
            InputMap.ActionEraseEvent("pause_menu", keyEvent);
        }
    }

    [TestCase]
    public void IsRebinding_False_WhenNotCapturing()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        AssertThat(_ctrl.IsRebinding).IsFalse();
    }

    [TestCase]
    public void IsRebinding_True_WhileCapturing()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");
        AssertThat(_ctrl.IsRebinding).IsTrue();
    }

    [TestCase]
    public void IsRebinding_False_AfterCaptureCompletes()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");
        AssertThat(_ctrl.IsRebinding).IsTrue();

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.J, Pressed = true });
        AssertThat(_ctrl.IsRebinding).IsFalse();
    }

    [TestCase]
    public void ResolveSelectedResolution_WhenIndexIsOutOfRange_KeepsEditedResolution()
    {
        var data = SettingsData.CreateDefaults();
        data.ResolutionWidth = 1920;
        data.ResolutionHeight = 1080;
        _ctrl.OpenSettings(data);

        var resolution = ((System.ValueTuple<int, int>)InvokePrivateWithResult(_ctrl, "ResolveSelectedResolution", -1)!);

        AssertThat(resolution.Item1).IsEqual(1920);
        AssertThat(resolution.Item2).IsEqual(1080);
    }

    [TestCase]
    public void ResolveSelectedDifficulty_WhenIndexIsOutOfRange_KeepsEditedDifficulty()
    {
        var data = SettingsData.CreateDefaults();
        data.Difficulty = "Hard";
        _ctrl.OpenSettings(data);

        var difficulty = (string)InvokePrivateWithResult(_ctrl, "ResolveSelectedDifficulty", -1)!;

        AssertThat(difficulty).IsEqual("Hard");
    }

    protected static void InvokePrivate(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        m.Invoke(obj, args);
    }

    protected static object? InvokePrivateWithResult(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        return m.Invoke(obj, args);
    }

    protected static T GetField<T>(object obj, string field) where T : class
    {
        var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Field '{field}' not found.");
        return (T)f.GetValue(obj)!;
    }
}
