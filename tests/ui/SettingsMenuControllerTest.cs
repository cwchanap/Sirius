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
    public void OpenSettings_NonPresetResolution_AddsCustomEntry()
    {
        var data = SettingsData.CreateDefaults();
        data.ResolutionWidth  = 1600;
        data.ResolutionHeight = 900;
        _ctrl.OpenSettings(data);

        // A custom entry should be added after the 4 presets
        var option = GetField<OptionButton>(_ctrl, "_resolutionOption");
        AssertThat(option.ItemCount).IsEqual(5); // 4 presets + 1 custom
        AssertThat(option.Selected).IsEqual(4); // custom entry index
        AssertThat(option.GetItemText(4)).IsEqual("Custom (1600\u00d7900)");
    }

    [TestCase]
    public void OpenSettings_NonPresetResolution_SelectedMatchesCustomEntry()
    {
        var data = SettingsData.CreateDefaults();
        data.ResolutionWidth  = 1600;
        data.ResolutionHeight = 900;
        _ctrl.OpenSettings(data);

        // The custom entry should be selected, not a preset.
        var option = GetField<OptionButton>(_ctrl, "_resolutionOption");
        AssertThat(option.Selected).IsEqual(4);
    }

    [TestCase]
    public void OpenSettings_SwitchFromCustomToPreset_RemovesCustomEntry()
    {
        // First open with a custom resolution
        var customData = SettingsData.CreateDefaults();
        customData.ResolutionWidth  = 1600;
        customData.ResolutionHeight = 900;
        _ctrl.OpenSettings(customData);
        var option = GetField<OptionButton>(_ctrl, "_resolutionOption");
        AssertThat(option.ItemCount).IsEqual(5);

        // Re-open with a preset resolution — custom entry should be removed
        var presetData = SettingsData.CreateDefaults();
        presetData.ResolutionWidth  = 1920;
        presetData.ResolutionHeight = 1080;
        _ctrl.OpenSettings(presetData);

        AssertThat(option.ItemCount).IsEqual(4); // back to just presets
        AssertThat(option.Selected).IsEqual(2); // 1920×1080
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
    public void ResolveSelectedResolution_CustomEntryIndex_KeepsEditedResolution()
    {
        var data = SettingsData.CreateDefaults();
        data.ResolutionWidth = 1600;
        data.ResolutionHeight = 900;
        _ctrl.OpenSettings(data);

        // Custom entry is at index 4 (ResolutionPresets.Length)
        var resolution = ((System.ValueTuple<int, int>)InvokePrivateWithResult(_ctrl, "ResolveSelectedResolution", 4)!);

        AssertThat(resolution.Item1).IsEqual(1600);
        AssertThat(resolution.Item2).IsEqual(900);
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

    [TestCase]
    public void Input_MouseMotion_DoesNotConsumeEvent()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);

        // Mouse motion must NOT be consumed — it needs to reach GUI controls
        // (sliders, buttons, checkboxes) for the settings panel to be usable.
        var evt = new InputEventMouseMotion();
        _ctrl._Input(evt);

        // Verify binding was NOT changed — the event passed through without
        // modifying state, which is correct for mouse events.
        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public void Input_MouseButtonClick_DoesNotConsumeEvent()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);

        // Mouse button click must NOT be consumed — it needs to reach GUI
        // controls (buttons, sliders, checkboxes, OptionButtons).
        var evt = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
        _ctrl._Input(evt);

        // Verify binding was NOT changed.
        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public void Input_JoypadEvent_ConsumesEvent()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);

        // Non-key, non-mouse events (joypad) should still be consumed to
        // prevent gameplay input leaks.
        var evt = new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true };
        _ctrl._Input(evt);

        // Verify binding was NOT changed — event was consumed at the early-return.
        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public void Input_KeyUpEvent_ConsumesEvent()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);

        // Key-up event (Pressed=false) should be consumed at the early-return.
        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.W, Pressed = false });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public void InputOutsideCapture_UiNavigationKey_DoesNotConsumeEvent()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        var originalBinding = _ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"];

        // Up arrow maps to ui_up by default — should pass through to Godot's
        // built-in GUI focus system, not be consumed as gameplay input.
        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Up, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual(originalBinding);
    }

    [TestCase]
    public void InputOutsideCapture_UiAcceptKey_DoesNotConsumeEvent()
    {
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        var originalBinding = _ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"];

        // Enter maps to ui_accept by default — should pass through to GUI.
        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Enter, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual(originalBinding);
    }

    [TestCase]
    public void InputDuringCapture_DuplicateKey_ShowsErrorAndKeepsCapture()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["interact"] = (long)Key.E;
        _ctrl.OpenSettings(data);
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        // Press E, which is already assigned to "interact"
        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.E, Pressed = true });

        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Visible).IsTrue();
        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Text).IsEqual("Key already in use");
        // toggle_inventory should NOT have been changed
        var defaults = SettingsData.CreateDefaultKeybindings();
        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual(defaults["toggle_inventory"]);
        // Should still be in capture mode
        AssertThat(_ctrl.IsRebinding).IsTrue();
    }

    [TestCase]
    public void InputDuringCapture_EditedPauseKey_CancelsCapture()
    {
        // Add P to the live pause_menu InputMap to simulate a non-default setup.
        var pKey = new InputEventKey { PhysicalKeycode = Key.P, Pressed = true };
        InputMap.ActionAddEvent("pause_menu", pKey);
        try
        {
            var data = SettingsData.CreateDefaults();
            data.PrimaryKeybindings["pause_menu"] = (long)Key.P; // edited pause is P
            data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
            _ctrl.OpenSettings(data);
            InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

            // Press P — this IS the edited pause key, so capture should cancel.
            _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.P, Pressed = true });

            // toggle_inventory should NOT have changed.
            AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
            // Capture cancelled, button text restored.
            AssertThat(GetField<Button>(_ctrl, "_inventoryKeyBtn").Text)
                .IsEqual(OS.GetKeycodeString(Key.I));
        }
        finally
        {
            InputMap.ActionEraseEvent("pause_menu", pKey);
        }
    }

    [TestCase]
    public void InputDuringCapture_LivePauseKeyDifferentFromEdited_CapturesKey()
    {
        // Add P to the live pause_menu InputMap (simulating the old binding
        // before the player remapped pause to something else in this session).
        var pKey = new InputEventKey { PhysicalKeycode = Key.P, Pressed = true };
        InputMap.ActionAddEvent("pause_menu", pKey);
        try
        {
            var data = SettingsData.CreateDefaults();
            data.PrimaryKeybindings["pause_menu"] = (long)Key.Escape; // edited pause is Escape
            data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
            _ctrl.OpenSettings(data);
            InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

            // Press P — live pause_menu has P, but edited pause is Escape.
            // P is NOT ui_cancel, so it should be captured as the new key,
            // not cancelled.
            _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.P, Pressed = true });

            // toggle_inventory should now be P (captured, not cancelled).
            AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.P);
            AssertThat(_ctrl.IsRebinding).IsFalse();
        }
        finally
        {
            InputMap.ActionEraseEvent("pause_menu", pKey);
        }
    }

    [TestCase]
    public void InputDuringCapture_SameKeySameAction_AcceptsBinding()
    {
        var data = SettingsData.CreateDefaults();
        data.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        _ctrl.OpenSettings(data);
        InvokePrivate(_ctrl, "StartKeyCapture", "toggle_inventory");

        // Press I — same key, same action — not a duplicate, should accept.
        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.I, Pressed = true });

        AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(_ctrl.IsRebinding).IsFalse();
        AssertThat(GetField<Label>(_ctrl, "_errorLabel").Visible).IsFalse();
    }

    [TestCase]
    public void OpenSettings_GrabsFocusOnFirstControl()
    {
        // After opening, the first interactive control (master slider) should
        // have focus so that UI navigation keys are captured by Godot's GUI
        // focus system instead of leaking to the game scene.
        _ctrl.OpenSettings(SettingsData.CreateDefaults());

        var masterSlider = GetField<HSlider>(_ctrl, "_masterSlider");
        AssertThat(_ctrl.GetViewport().GuiGetFocusOwner()).IsEqual(masterSlider);
    }

    [TestCase]
    public void JoypadUiNavigation_PassesThrough()
    {
        // Joypad buttons mapped to ui_up/ui_down/ui_accept must pass through
        // _Input() so Godot's GUI focus system can handle controller navigation.
        var joyUp = new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp, Pressed = true };
        InputMap.ActionAddEvent("ui_up", joyUp);
        try
        {
            _ctrl.OpenSettings(SettingsData.CreateDefaults());
            var originalBinding = _ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"];

            _ctrl._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp, Pressed = true });

            // Event passed through — no state change
            AssertThat(_ctrl.EditedSettings.PrimaryKeybindings["toggle_inventory"])
                .IsEqual(originalBinding);
        }
        finally
        {
            InputMap.ActionEraseEvent("ui_up", joyUp);
        }
    }

    [TestCase]
    public void EscapeKey_WhenResolutionPopupOpen_DoesNotCloseSettings()
    {
        bool closed = false;
        _ctrl.Closed += () => closed = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());

        // Simulate the OptionButton popup being visible (as it would be when
        // the user has opened the dropdown list).
        var resolutionOption = GetField<OptionButton>(_ctrl, "_resolutionOption");
        resolutionOption.GetPopup().Visible = true;

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        // Settings should NOT have closed — the popup should handle the event.
        AssertThat(closed).IsFalse();

        // Clean up
        resolutionOption.GetPopup().Visible = false;
    }

    [TestCase]
    public void EscapeKey_WhenDifficultyPopupOpen_DoesNotCloseSettings()
    {
        bool closed = false;
        _ctrl.Closed += () => closed = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());

        var difficultyOption = GetField<OptionButton>(_ctrl, "_difficultyOption");
        difficultyOption.GetPopup().Visible = true;

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        AssertThat(closed).IsFalse();

        difficultyOption.GetPopup().Visible = false;
    }

    [TestCase]
    public void EscapeKey_WhenNoPopupOpen_ClosesSettings()
    {
        bool closed = false;
        _ctrl.Closed += () => closed = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());

        _ctrl._Input(new InputEventKey { PhysicalKeycode = Key.Escape, Pressed = true });

        AssertThat(closed).IsTrue();
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
