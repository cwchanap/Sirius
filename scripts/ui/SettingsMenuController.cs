using Godot;
using System;
using System.Collections.Generic;

public partial class SettingsMenuController : Control
{
    [Signal] public delegate void ClosedEventHandler();

    internal SettingsData EditedSettings => _editedSettings;

    /// True while the player is in the middle of pressing a key to assign
    /// to a key-binding action.  Used by Game._Input to avoid force-closing
    /// the settings panel when the pause_menu key itself is being rebound.
    public bool IsRebinding => _listeningAction != null;

    private SettingsData _editedSettings = SettingsData.CreateDefaults();
    private string? _listeningAction;

    // Audio
    private HSlider _masterSlider;
    private Label _masterValueLabel;
    private HSlider _musicSlider;
    private Label _musicValueLabel;
    private HSlider _sfxSlider;
    private Label _sfxValueLabel;

    // Display
    private CheckBox _fullscreenCheck;
    private OptionButton _resolutionOption;

    // Gameplay
    private OptionButton _difficultyOption;
    private CheckBox _autoSaveCheck;

    // Controls
    private Button _inventoryKeyBtn;
    private Button _interactKeyBtn;
    private Button _pauseKeyBtn;

    // Feedback
    private Label _errorLabel;

    private static readonly (int W, int H)[] ResolutionPresets =
    {
        (640, 360), (1280, 720), (1920, 1080), (2560, 1440)
    };

    private static readonly string[] Difficulties = { "Easy", "Normal", "Hard" };

    public override void _Ready()
    {
        var content = GetNode<VBoxContainer>("Panel/ScrollContainer/Content");
        BuildUI(content);
        Hide();
        SetProcessInput(false);
    }

    public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
    {
        if (_listeningAction != null) CancelKeyCapture();

        var bg = GetNodeOrNull<ColorRect>("Background");
        if (bg != null) bg.Visible = showOverlay;

        var panel = GetNodeOrNull<PanelContainer>("Panel");
        if (panel != null)
        {
            if (showOverlay)
            {
                panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            else
            {
                // Cap the panel to 90% of viewport so it always fits, even at
                // the minimum supported resolution (640×360).  The
                // ScrollContainer handles overflow when content is taller.
                var vpSize = GetViewport().GetVisibleRect().Size;
                var maxW = Mathf.Min(580f, vpSize.X * 0.9f);
                var maxH = Mathf.Min(500f, vpSize.Y * 0.9f);
                panel.CustomMinimumSize = new Vector2(maxW, maxH);
                panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.Minsize);
            }
        }

        var source = snapshot ?? SettingsManager.Instance?.GetSnapshot() ?? SettingsData.CreateDefaults();
        _editedSettings = source.Clone();
        PopulateControls();
        SetProcessInput(true);
        Show();
    }

    private void BuildUI(VBoxContainer content)
    {
        content.AddChild(new Label { Text = "Settings" });

        var tabs = new TabContainer();
        tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddChild(tabs);

        tabs.AddChild(BuildAudioTab());
        tabs.AddChild(BuildDisplayTab());
        tabs.AddChild(BuildGameplayTab());
        tabs.AddChild(BuildControlsTab());

        _errorLabel = new Label { Modulate = Colors.Red, Visible = false };
        content.AddChild(_errorLabel);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);
        content.AddChild(btnRow);

        var applyBtn = new Button { Text = "Apply" };
        applyBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        applyBtn.Pressed += OnApplyPressed;
        btnRow.AddChild(applyBtn);

        var cancelBtn = new Button { Text = "Cancel" };
        cancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cancelBtn.Pressed += OnCancelPressed;
        btnRow.AddChild(cancelBtn);
    }

    private VBoxContainer BuildAudioTab()
    {
        var tab = new VBoxContainer { Name = "Audio" };
        tab.AddThemeConstantOverride("separation", 8);

        (_masterSlider, _masterValueLabel) = AddSliderRow(tab, "Master Volume");
        (_musicSlider,  _musicValueLabel)  = AddSliderRow(tab, "Music Volume");
        (_sfxSlider,    _sfxValueLabel)    = AddSliderRow(tab, "SFX Volume");

        _masterSlider.ValueChanged += v => _masterValueLabel.Text = $"{(int)v}%";
        _musicSlider.ValueChanged  += v => _musicValueLabel.Text  = $"{(int)v}%";
        _sfxSlider.ValueChanged    += v => _sfxValueLabel.Text    = $"{(int)v}%";

        return tab;
    }

    private static (HSlider slider, Label valueLabel) AddSliderRow(VBoxContainer parent, string labelText)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);

        var lbl = new Label { Text = labelText };
        lbl.CustomMinimumSize = new Vector2(140, 0);
        row.AddChild(lbl);

        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1 };
        slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(slider);

        var val = new Label { Text = "100%" };
        val.CustomMinimumSize = new Vector2(45, 0);
        row.AddChild(val);

        return (slider, val);
    }

    private VBoxContainer BuildDisplayTab()
    {
        var tab = new VBoxContainer { Name = "Display" };
        tab.AddThemeConstantOverride("separation", 8);

        var fsRow = new HBoxContainer();
        tab.AddChild(fsRow);
        fsRow.AddChild(new Label { Text = "Fullscreen" });
        _fullscreenCheck = new CheckBox();
        fsRow.AddChild(_fullscreenCheck);

        var resRow = new HBoxContainer();
        tab.AddChild(resRow);
        resRow.AddChild(new Label { Text = "Resolution" });
        _resolutionOption = new OptionButton();
        foreach (var (w, h) in ResolutionPresets)
            _resolutionOption.AddItem($"{w}\u00d7{h}");
        resRow.AddChild(_resolutionOption);

        return tab;
    }

    private VBoxContainer BuildGameplayTab()
    {
        var tab = new VBoxContainer { Name = "Gameplay" };
        tab.AddThemeConstantOverride("separation", 8);

        var diffRow = new HBoxContainer();
        tab.AddChild(diffRow);
        diffRow.AddChild(new Label { Text = "Difficulty" });
        _difficultyOption = new OptionButton();
        foreach (var d in Difficulties)
            _difficultyOption.AddItem(d);
        diffRow.AddChild(_difficultyOption);

        var asRow = new HBoxContainer();
        tab.AddChild(asRow);
        asRow.AddChild(new Label { Text = "Auto Save" });
        _autoSaveCheck = new CheckBox();
        asRow.AddChild(_autoSaveCheck);

        return tab;
    }

    private VBoxContainer BuildControlsTab()
    {
        var tab = new VBoxContainer { Name = "Controls" };
        tab.AddThemeConstantOverride("separation", 8);

        _inventoryKeyBtn = AddKeyRow(tab, "Toggle Inventory", "toggle_inventory");
        _interactKeyBtn  = AddKeyRow(tab, "Interact",         "interact");
        _pauseKeyBtn     = AddKeyRow(tab, "Pause / Cancel",   "pause_menu");

        return tab;
    }

    private Button AddKeyRow(VBoxContainer parent, string displayName, string action)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);

        var lbl = new Label { Text = displayName };
        lbl.CustomMinimumSize = new Vector2(160, 0);
        row.AddChild(lbl);

        var btn = new Button();
        btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        btn.Pressed += () => StartKeyCapture(action);
        row.AddChild(btn);

        return btn;
    }

    private void PopulateControls()
    {
        _masterSlider.Value      = _editedSettings.MasterVolumePercent;
        _masterValueLabel.Text   = $"{_editedSettings.MasterVolumePercent}%";
        _musicSlider.Value       = _editedSettings.MusicVolumePercent;
        _musicValueLabel.Text    = $"{_editedSettings.MusicVolumePercent}%";
        _sfxSlider.Value         = _editedSettings.SfxVolumePercent;
        _sfxValueLabel.Text      = $"{_editedSettings.SfxVolumePercent}%";

        _fullscreenCheck.ButtonPressed = _editedSettings.FullscreenEnabled;

        int resIdx = ResolutionIndexFor(_editedSettings.ResolutionWidth, _editedSettings.ResolutionHeight);
        _resolutionOption.Selected = resIdx >= 0 ? resIdx : 1;

        int diffIdx = System.Array.IndexOf(Difficulties, _editedSettings.Difficulty);
        _difficultyOption.Selected = diffIdx >= 0 ? diffIdx : 1;

        _autoSaveCheck.ButtonPressed = _editedSettings.AutoSaveEnabled;

        UpdateKeyButtonText(_inventoryKeyBtn, "toggle_inventory");
        UpdateKeyButtonText(_interactKeyBtn,  "interact");
        UpdateKeyButtonText(_pauseKeyBtn,     "pause_menu");
    }

    private static int ResolutionIndexFor(int w, int h)
    {
        for (int i = 0; i < ResolutionPresets.Length; i++)
            if (ResolutionPresets[i].W == w && ResolutionPresets[i].H == h) return i;
        return -1;
    }

    private void StartKeyCapture(string action)
    {
        if (_listeningAction != null) CancelKeyCapture();
        _listeningAction = action;
        GetKeyButton(action).Text = "Press a key...";
        if (_errorLabel != null) _errorLabel.Visible = false;
        SetProcessInput(true);
    }

    public override void _Input(InputEvent @event)
    {
        // Handle cancel/close via remappable actions (supports keyboard remaps
        // and non-keyboard inputs like joypad).  Check BEFORE the InputEventKey
        // filter so that preserved controller bindings on ui_cancel can dismiss
        // the panel, matching the intent of SettingsManager.RebindAction which
        // mirrors pause_menu onto ui_cancel and preserves non-key events.
        if ((@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("pause_menu"))
            && !IsPauseMenuCapture(_listeningAction))
        {
            GetViewport()?.SetInputAsHandled();
            if (_listeningAction != null)
                CancelKeyCapture();
            else
                OnCancelPressed();
            return;
        }

        // Let mouse events pass through to child GUI controls (buttons,
        // sliders, checkboxes, OptionButtons) so the settings UI is usable.
        // _Input() runs before the viewport's GUI dispatch, so calling
        // SetInputAsHandled() here would prevent clicks/drags from reaching
        // those controls at all.
        if (@event is InputEventMouseMotion || @event is InputEventMouseButton)
        {
            return;
        }

        // Key capture only works with keyboard events.
        // Consume non-keyboard input (joypad, etc.) and key-up / echo events
        // so they do not leak through to gameplay while the settings panel is open.
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            GetViewport()?.SetInputAsHandled();
            return;
        }

        var key = keyEvent.PhysicalKeycode;

        if (_listeningAction == null)
        {
            // Consume the event so gameplay input (movement, inventory, etc.)
            // does not leak through while the settings panel is open.
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (IsReservedKey(_listeningAction, (long)key))
        {
            ShowError("Key reserved");
            GetViewport()?.SetInputAsHandled();
            return;
        }

        _editedSettings.PrimaryKeybindings[_listeningAction] = (long)key;
        UpdateKeyButtonText(GetKeyButton(_listeningAction), _listeningAction);
        _listeningAction = null;
        if (_errorLabel != null) _errorLabel.Visible = false;
        GetViewport()?.SetInputAsHandled();
    }

    private static bool IsPauseMenuCapture(string action) => action == "pause_menu";

    private static bool IsReservedKey(string action, long code)
    {
        if (action == "pause_menu" && code == (long)Key.Escape)
            return false;

        return code is
            (long)Key.W or (long)Key.A or (long)Key.S or (long)Key.D or
            (long)Key.Up or (long)Key.Down or (long)Key.Left or (long)Key.Right or
            (long)Key.Escape or (long)Key.Enter or (long)Key.KpEnter or
            (long)Key.Space or (long)Key.Tab;
    }

    private void ShowError(string msg)
    {
        if (_errorLabel == null) return;
        _errorLabel.Text    = msg;
        _errorLabel.Visible = true;
    }

    private (int W, int H) ResolveSelectedResolution(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < ResolutionPresets.Length
            ? ResolutionPresets[selectedIndex]
            : (W: _editedSettings.ResolutionWidth, H: _editedSettings.ResolutionHeight);

    private string ResolveSelectedDifficulty(int selectedIndex) =>
        selectedIndex >= 0 && selectedIndex < Difficulties.Length
            ? Difficulties[selectedIndex]
            : _editedSettings.Difficulty;

    private void OnApplyPressed()
    {
        CancelKeyCapture();

        var mgr = SettingsManager.Instance;
        if (mgr == null || !GodotObject.IsInstanceValid(mgr))
        {
            ShowError("Settings system unavailable.");
            return;
        }

        var resolution = ResolveSelectedResolution(_resolutionOption.Selected);
        var difficulty = ResolveSelectedDifficulty(_difficultyOption.Selected);

        var candidate = new SettingsData
        {
            MasterVolumePercent = (int)_masterSlider.Value,
            MusicVolumePercent  = (int)_musicSlider.Value,
            SfxVolumePercent    = (int)_sfxSlider.Value,
            FullscreenEnabled   = _fullscreenCheck.ButtonPressed,
            ResolutionWidth     = resolution.W,
            ResolutionHeight    = resolution.H,
            Difficulty          = difficulty,
            AutoSaveEnabled     = _autoSaveCheck.ButtonPressed,
            PrimaryKeybindings  = new System.Collections.Generic.Dictionary<string, long>(_editedSettings.PrimaryKeybindings)
        };

        if (!mgr.ApplyAndSave(candidate))
        {
            ShowError("Invalid settings — could not apply.");
            return;
        }

        SetProcessInput(false);
        EmitSignal(SignalName.Closed);
    }

    private void OnCancelPressed()
    {
        CancelKeyCapture();
        SetProcessInput(false);
        EmitSignal(SignalName.Closed);
    }

    private void CancelKeyCapture()
    {
        if (_listeningAction == null) return;
        var prev = _listeningAction;
        _listeningAction = null;
        if (_errorLabel != null) _errorLabel.Visible = false;
        UpdateKeyButtonText(GetKeyButton(prev), prev);
    }

    private void UpdateKeyButtonText(Button btn, string action)
    {
        if (_editedSettings.PrimaryKeybindings.TryGetValue(action, out var code))
            btn.Text = code <= 0 ? "(unbound)" : OS.GetKeycodeString((Key)code);
        else
            btn.Text = "(unbound)";
    }

    private Button GetKeyButton(string action) => action switch
    {
        "toggle_inventory" => _inventoryKeyBtn,
        "interact"         => _interactKeyBtn,
        "pause_menu"       => _pauseKeyBtn,
        _ => throw new System.ArgumentException($"Unknown action: {action}")
    };
}
