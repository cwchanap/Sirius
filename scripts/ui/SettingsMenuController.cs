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

    /// True while an OptionButton dropdown popup is visible.  Used by
    /// Game._Input to avoid force-closing the settings panel when ESC should
    /// dismiss the popup instead.
    public bool IsPopupOpen => IsAnyOptionPopupOpen();

    public Control InitialFocusTarget => _pageDeck.CurrentTab switch
    {
        0 => _masterSlider,
        1 => _fullscreenCheck,
        2 => _difficultyOption,
        3 => _inventoryKeyBtn,
        _ => _masterSlider
    };

    private SettingsData _editedSettings = SettingsData.CreateDefaults();
    private string? _listeningAction;
    private bool _closedEmitted;

    // Scene structure
    private SiriusModalShell _shell = null!;
    private ScrollContainer _shellBodyScroll = null!;
    private GridContainer _settingsFrame = null!;
    private GridContainer _pageSelector = null!;
    private TabContainer _pageDeck = null!;
    private GridContainer _audioRows = null!;
    private GridContainer _displayRows = null!;
    private GridContainer _gameplayRows = null!;
    private GridContainer _controlsRows = null!;

    private Button _audioPageButton = null!;
    private Button _displayPageButton = null!;
    private Button _gameplayPageButton = null!;
    private Button _controlsPageButton = null!;
    private Button[] _pageButtons = null!;

    // Audio
    private HSlider _masterSlider = null!;
    private Label _masterValueLabel = null!;
    private HSlider _musicSlider = null!;
    private Label _musicValueLabel = null!;
    private HSlider _sfxSlider = null!;
    private Label _sfxValueLabel = null!;

    // Display
    private CheckBox _fullscreenCheck = null!;
    private OptionButton _resolutionOption = null!;
    private CheckBox _reducedMotionCheck = null!;

    // Gameplay
    private OptionButton _difficultyOption = null!;
    private CheckBox _autoSaveCheck = null!;

    // Controls
    private Button _inventoryKeyBtn = null!;
    private Button _interactKeyBtn = null!;
    private Button _pauseKeyBtn = null!;

    // Feedback
    private PanelContainer _errorPanel = null!;
    private Label _errorLabel = null!;
    private Button _applyButton = null!;
    private Button _cancelButton = null!;

    private static readonly (int W, int H)[] ResolutionPresets =
    {
        (640, 360), (1280, 720), (1920, 1080), (2560, 1440)
    };

    private static readonly string[] Difficulties = { "Easy", "Normal", "Hard" };

    public override void _Ready()
    {
        BindSceneNodes();
        ConfigureScrollOwnership();
        PopulateChoiceItems();
        BindSignals();
        RefreshLayout();

        Hide();
        SetProcessInput(false);
    }

    public override void _ExitTree()
    {
        UnbindSignals();
    }

    public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
    {
        if (_listeningAction != null)
            CancelKeyCapture();

        _closedEmitted = false;
        GetNode<Control>("Background").Visible = showOverlay;
        ClearError();

        var source =
            snapshot ??
            SettingsManager.Instance?.GetSnapshot() ??
            SettingsData.CreateDefaults();

        _editedSettings = source.Clone();
        PopulateControls();
        Show();
        SetProcessInput(true);
    }

    private void BindSceneNodes()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _shellBodyScroll = _shell.GetNode<ScrollContainer>("%BodyScroll");
        _settingsFrame = GetNode<GridContainer>("%SettingsFrame");
        _pageSelector = GetNode<GridContainer>("%PageSelector");
        _pageDeck = GetNode<TabContainer>("%PageDeck");
        _audioRows = GetNode<GridContainer>("%AudioRows");
        _displayRows = GetNode<GridContainer>("%DisplayRows");
        _gameplayRows = GetNode<GridContainer>("%GameplayRows");
        _controlsRows = GetNode<GridContainer>("%ControlsRows");

        _audioPageButton = GetNode<Button>("%AudioPageButton");
        _displayPageButton = GetNode<Button>("%DisplayPageButton");
        _gameplayPageButton = GetNode<Button>("%GameplayPageButton");
        _controlsPageButton = GetNode<Button>("%ControlsPageButton");
        _pageButtons =
        [
            _audioPageButton,
            _displayPageButton,
            _gameplayPageButton,
            _controlsPageButton
        ];

        _masterSlider = GetNode<HSlider>("%MasterSlider");
        _masterValueLabel = GetNode<Label>("%MasterValueLabel");
        _musicSlider = GetNode<HSlider>("%MusicSlider");
        _musicValueLabel = GetNode<Label>("%MusicValueLabel");
        _sfxSlider = GetNode<HSlider>("%SfxSlider");
        _sfxValueLabel = GetNode<Label>("%SfxValueLabel");

        _fullscreenCheck = GetNode<CheckBox>("%FullscreenCheck");
        _resolutionOption = GetNode<OptionButton>("%ResolutionOption");
        _reducedMotionCheck = GetNode<CheckBox>("%ReducedMotionCheck");
        _difficultyOption = GetNode<OptionButton>("%DifficultyOption");
        _autoSaveCheck = GetNode<CheckBox>("%AutoSaveCheck");

        _inventoryKeyBtn = GetNode<Button>("%InventoryKeyButton");
        _interactKeyBtn = GetNode<Button>("%InteractKeyButton");
        _pauseKeyBtn = GetNode<Button>("%PauseKeyButton");

        _errorPanel = GetNode<PanelContainer>("%ErrorPanel");
        _errorLabel = GetNode<Label>("%ErrorLabel");
        _applyButton = GetNode<Button>("%ApplyButton");
        _cancelButton = GetNode<Button>("%CancelButton");
    }

    private void ConfigureScrollOwnership()
    {
        _shellBodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _shellBodyScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        // SiriusModalShell readies before this controller and may have sized
        // the shared body for its enabled outer-scroll contract. Settings
        // owns page-local scrolling, so clear that inherited height when we
        // claim the body rather than leaving a stale minimum across reflows.
        _shellBodyScroll.CustomMinimumSize = new Vector2(
            _shellBodyScroll.CustomMinimumSize.X,
            0f);
    }

    private void PopulateChoiceItems()
    {
        _resolutionOption.Clear();
        foreach (var (w, h) in ResolutionPresets)
            _resolutionOption.AddItem($"{w}×{h}");

        _difficultyOption.Clear();
        foreach (var difficulty in Difficulties)
            _difficultyOption.AddItem(difficulty);
    }

    private void BindSignals()
    {
        Resized += OnResized;

        _masterSlider.ValueChanged += OnMasterVolumeChanged;
        _musicSlider.ValueChanged += OnMusicVolumeChanged;
        _sfxSlider.ValueChanged += OnSfxVolumeChanged;

        _inventoryKeyBtn.Pressed += OnInventoryKeyPressed;
        _interactKeyBtn.Pressed += OnInteractKeyPressed;
        _pauseKeyBtn.Pressed += OnPauseKeyPressed;

        _audioPageButton.Pressed += OnAudioPagePressed;
        _displayPageButton.Pressed += OnDisplayPagePressed;
        _gameplayPageButton.Pressed += OnGameplayPagePressed;
        _controlsPageButton.Pressed += OnControlsPagePressed;

        _applyButton.Pressed += OnApplyPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    private void UnbindSignals()
    {
        Resized -= OnResized;

        _masterSlider.ValueChanged -= OnMasterVolumeChanged;
        _musicSlider.ValueChanged -= OnMusicVolumeChanged;
        _sfxSlider.ValueChanged -= OnSfxVolumeChanged;

        _inventoryKeyBtn.Pressed -= OnInventoryKeyPressed;
        _interactKeyBtn.Pressed -= OnInteractKeyPressed;
        _pauseKeyBtn.Pressed -= OnPauseKeyPressed;

        _audioPageButton.Pressed -= OnAudioPagePressed;
        _displayPageButton.Pressed -= OnDisplayPagePressed;
        _gameplayPageButton.Pressed -= OnGameplayPagePressed;
        _controlsPageButton.Pressed -= OnControlsPagePressed;

        _applyButton.Pressed -= OnApplyPressed;
        _cancelButton.Pressed -= OnCancelPressed;
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        var size = GetViewportRect().Size;
        var compact = SiriusUiMetrics.IsCompact(size);

        _shell.Compact = compact;
        _shell.RefreshPresentation(size);

        _settingsFrame.Columns = compact ? 1 : 2;
        _pageSelector.Columns = compact ? 4 : 1;

        var rowColumns = compact ? 1 : 2;
        _audioRows.Columns = rowColumns;
        _displayRows.Columns = rowColumns;
        _gameplayRows.Columns = rowColumns;
        _controlsRows.Columns = rowColumns;
    }

    private void OnMasterVolumeChanged(double value) =>
        _masterValueLabel.Text = $"{(int)value}%";

    private void OnMusicVolumeChanged(double value) =>
        _musicValueLabel.Text = $"{(int)value}%";

    private void OnSfxVolumeChanged(double value) =>
        _sfxValueLabel.Text = $"{(int)value}%";

    private void OnInventoryKeyPressed() => StartKeyCapture("toggle_inventory");

    private void OnInteractKeyPressed() => StartKeyCapture("interact");

    private void OnPauseKeyPressed() => StartKeyCapture("pause_menu");

    private void OnAudioPagePressed() => SelectPage(0);

    private void OnDisplayPagePressed() => SelectPage(1);

    private void OnGameplayPagePressed() => SelectPage(2);

    private void OnControlsPagePressed() => SelectPage(3);

    private void SelectPage(int pageIndex)
    {
        _pageDeck.CurrentTab = pageIndex;
        _pageButtons[pageIndex].ButtonPressed = true;
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
        _reducedMotionCheck.ButtonPressed = _editedSettings.ReducedMotionEnabled;

        PopulateResolutionOption();

        int diffIdx = System.Array.IndexOf(Difficulties, _editedSettings.Difficulty);
        _difficultyOption.Selected = diffIdx >= 0 ? diffIdx : 1;

        _autoSaveCheck.ButtonPressed = _editedSettings.AutoSaveEnabled;

        UpdateKeyButtonText(_inventoryKeyBtn, "toggle_inventory");
        UpdateKeyButtonText(_interactKeyBtn,  "interact");
        UpdateKeyButtonText(_pauseKeyBtn,     "pause_menu");
    }

    /// Ensures the resolution OptionButton shows the current edited resolution.
    /// If it matches a preset, selects that preset.  Otherwise adds or updates a
    /// dynamic "Custom (W×H)" entry so non-preset resolutions are preserved when
    /// the user applies settings without touching the resolution field.
    private void PopulateResolutionOption()
    {
        int resIdx = ResolutionIndexFor(_editedSettings.ResolutionWidth, _editedSettings.ResolutionHeight);
        if (resIdx >= 0)
        {
            // Matches a preset — remove any stale custom entry and select it.
            RemoveCustomResolutionEntry();
            _resolutionOption.Selected = resIdx;
        }
        else
        {
            // Non-preset resolution — ensure a custom entry exists and select it.
            EnsureCustomResolutionEntry(_editedSettings.ResolutionWidth, _editedSettings.ResolutionHeight);
            _resolutionOption.Selected = ResolutionPresets.Length;
        }
    }

    private void RemoveCustomResolutionEntry()
    {
        while (_resolutionOption.ItemCount > ResolutionPresets.Length)
            _resolutionOption.RemoveItem(ResolutionPresets.Length);
    }

    private void EnsureCustomResolutionEntry(int w, int h)
    {
        var customIdx = ResolutionPresets.Length;
        var label = $"Custom ({w}\u00d7{h})";
        if (_resolutionOption.ItemCount > customIdx)
        {
            _resolutionOption.SetItemText(customIdx, label);
        }
        else
        {
            _resolutionOption.AddItem(label);
        }
    }

    private static int ResolutionIndexFor(int w, int h)
    {
        for (int i = 0; i < ResolutionPresets.Length; i++)
            if (ResolutionPresets[i].W == w && ResolutionPresets[i].H == h) return i;
        return -1;
    }

    private void StartKeyCapture(string action)
    {
        if (_listeningAction != null)
            CancelKeyCapture();

        _listeningAction = action;
        GetKeyButton(action).Text = "Press a key...";
        ClearError();
        SetProcessInput(true);
    }

    public override void _Input(InputEvent @event)
    {
        // Handle cancel/close via remappable actions (supports keyboard remaps
        // and non-keyboard inputs like joypad).  Check BEFORE the InputEventKey
        // filter so that preserved controller bindings on ui_cancel can dismiss
        // the panel, matching the intent of SettingsManager.RebindAction which
        // mirrors pause_menu onto ui_cancel and preserves non-key events.
        //
        // During key capture, consult the *edited* pause key rather than the
        // live InputMap so that swapping the pause binding with another action
        // works within a single settings session.
        //
        // When an OptionButton popup is open, skip the cancel/close handling so
        // the popup can dismiss itself instead of the entire settings menu.
        if (ShouldCancelOrClose(@event))
        {
            if (IsAnyOptionPopupOpen())
            {
                // Let the popup handle the event — do not consume it here.
                return;
            }
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

        // Let non-keyboard UI navigation actions (joypad dpad, face buttons
        // mapped to ui_up/down/etc.) pass through to Godot's GUI focus system
        // so controller users can navigate the settings panel.  This mirrors
        // the IsUiNavigationAction check below for keyboard events.
        if (@event is not InputEventKey && IsDeviceUiNavigationAction(@event))
        {
            return;
        }

        // Key capture only works with keyboard events.
        // Consume remaining non-keyboard input (joypad non-nav, etc.) and
        // key-up / echo events so they do not leak through to gameplay while
        // the settings panel is open.
        if (@event is not InputEventKey keyEvent || !keyEvent.Pressed || keyEvent.Echo)
        {
            GetViewport()?.SetInputAsHandled();
            return;
        }

        var key = keyEvent.PhysicalKeycode;

        if (_listeningAction == null)
        {
            // Let UI navigation actions (arrow keys, Tab, Enter via ui_accept,
            // etc.) pass through to Godot's built-in GUI focus/activation system
            // so keyboard users can navigate TabContainer, OptionButtons,
            // CheckBoxes, and Buttons.
            if (IsUiNavigationAction(keyEvent))
            {
                return;
            }

            // Consume all other key events so gameplay input (movement, inventory,
            // etc.) does not leak through while the settings panel is open.
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (IsReservedKey(_listeningAction, (long)key))
        {
            ShowError("Key reserved");
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (IsDuplicateKey(_listeningAction, (long)key))
        {
            ShowError("Key already in use");
            GetViewport()?.SetInputAsHandled();
            return;
        }

        _editedSettings.PrimaryKeybindings[_listeningAction] = (long)key;
        UpdateKeyButtonText(GetKeyButton(_listeningAction), _listeningAction);
        _listeningAction = null;
        ClearError();
        GetViewport()?.SetInputAsHandled();
    }

    private static bool IsUiNavigationAction(InputEventKey keyEvent)
    {
        return keyEvent.IsActionPressed("ui_up") ||
               keyEvent.IsActionPressed("ui_down") ||
               keyEvent.IsActionPressed("ui_left") ||
               keyEvent.IsActionPressed("ui_right") ||
               keyEvent.IsActionPressed("ui_accept") ||
               keyEvent.IsActionPressed("ui_focus_next") ||
               keyEvent.IsActionPressed("ui_focus_prev");
    }

    /// Returns true when an OptionButton dropdown popup is currently visible,
    /// meaning ui_cancel should dismiss the popup rather than the entire
    /// settings menu.
    private bool IsAnyOptionPopupOpen()
    {
        if (_resolutionOption != null)
        {
            var popup = _resolutionOption.GetPopup();
            if (popup != null && popup.Visible) return true;
        }
        if (_difficultyOption != null)
        {
            var popup = _difficultyOption.GetPopup();
            if (popup != null && popup.Visible) return true;
        }
        return false;
    }

    /// Checks whether a non-keyboard event (e.g. joypad button / motion)
    /// matches a UI navigation action.  Uses the same action list as
    /// <see cref="IsUiNavigationAction"/> but works on any <see cref="InputEvent"/>.
    private static bool IsDeviceUiNavigationAction(InputEvent evt)
    {
        return evt.IsActionPressed("ui_up") ||
               evt.IsActionPressed("ui_down") ||
               evt.IsActionPressed("ui_left") ||
               evt.IsActionPressed("ui_right") ||
               evt.IsActionPressed("ui_accept") ||
               evt.IsActionPressed("ui_focus_next") ||
               evt.IsActionPressed("ui_focus_prev");
    }

    private bool IsDuplicateKey(string action, long keycode)
    {
        foreach (var (act, code) in _editedSettings.PrimaryKeybindings)
        {
            if (act != action && code == keycode)
                return true;
        }
        return false;
    }

    private static bool IsPauseMenuCapture(string action) => action == "pause_menu";

    /// Determines whether an input event should trigger cancel (during capture)
    /// or close (outside capture) via the pause/cancel pathways.
    /// When in key capture mode, checks the *edited* pause key rather than the
    /// live InputMap so that swapping the pause binding with another action
    /// works in a single settings session.
    private bool ShouldCancelOrClose(InputEvent evt)
    {
        if (IsPauseMenuCapture(_listeningAction)) return false;
        if (evt.IsActionPressed("ui_cancel")) return true;

        if (_listeningAction != null && evt is InputEventKey keyEvt)
        {
            // In capture mode: only the edited pause key counts as "pause".
            return _editedSettings.PrimaryKeybindings.TryGetValue("pause_menu", out var editedPauseKey)
                   && (long)keyEvt.PhysicalKeycode == editedPauseKey;
        }

        return evt.IsActionPressed("pause_menu");
    }

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
        _errorLabel.Text = msg;
        _errorLabel.Visible = true;
        _errorPanel.Visible = true;
    }

    private void ClearError()
    {
        _errorLabel.Visible = false;
        _errorPanel.Visible = false;
    }

    private (int W, int H) ResolveSelectedResolution(int selectedIndex)
    {
        if (selectedIndex >= 0 && selectedIndex < ResolutionPresets.Length)
            return ResolutionPresets[selectedIndex];

        // Custom entry or invalid index — keep the edited resolution as-is.
        return (_editedSettings.ResolutionWidth, _editedSettings.ResolutionHeight);
    }

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

        // Start from a clone of the edited snapshot (future fields carry over
        // automatically) and overwrite exactly the control-backed fields.
        var candidate = _editedSettings.Clone();
        candidate.MasterVolumePercent  = (int)_masterSlider.Value;
        candidate.MusicVolumePercent   = (int)_musicSlider.Value;
        candidate.SfxVolumePercent     = (int)_sfxSlider.Value;
        candidate.FullscreenEnabled    = _fullscreenCheck.ButtonPressed;
        candidate.ReducedMotionEnabled = _reducedMotionCheck.ButtonPressed;
        candidate.ResolutionWidth      = resolution.W;
        candidate.ResolutionHeight     = resolution.H;
        candidate.Difficulty           = difficulty;
        candidate.AutoSaveEnabled      = _autoSaveCheck.ButtonPressed;

        if (!mgr.ApplyAndSave(candidate))
        {
            ShowError("Invalid settings — could not apply.");
            return;
        }

        EmitClosedOnce();
    }

    private void OnCancelPressed()
    {
        CancelKeyCapture();
        EmitClosedOnce();
    }

    private void EmitClosedOnce()
    {
        if (_closedEmitted)
            return;

        _closedEmitted = true;
        SetProcessInput(false);
        EmitSignal(SignalName.Closed);
    }

    private void CancelKeyCapture()
    {
        if (_listeningAction == null)
        {
            ClearError();
            return;
        }

        var prev = _listeningAction;
        _listeningAction = null;
        ClearError();
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
