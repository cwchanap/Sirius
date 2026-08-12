using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class MainMenu : Control
{
    private const string GameScenePath = "res://scenes/game/Game.tscn";

    private Control? _mainMenuContent;
    private Control? _safeFrame;
    private VBoxContainer? _menuRail;
    private Label? _wordmarkLabel;
    private Button? _continueButton;
    private PanelContainer? _continueSummary;
    private Label? _continueSlotLabel;
    private Label? _continueDetailLabel;
    private Label? _continueTimestampLabel;
    private Button? _newGameButton;
    private Button? _loadButton;
    private Button? _settingsButton;
    private Button? _quitButton;
    private SiriusInputHint? _selectHint;
    private AudioStreamPlayer? _backgroundMusic;
    private UIScreenHost? _screenHost;
    private UIScreenHandle? _loadHandle;
    private SaveLoadScreenController? _loadScreen;
    private UIScreenHandle? _settingsHandle;
    private SettingsMenuController? _settingsMenu;
    private UIScreenHandle? _messageHandle;
    private AcceptDialog? _messageDialog;
    private Action? _messageCloseDelegate;

    private SaveSlotInfo? _continueSave;
    private Button[] _rootActions = Array.Empty<Button>();
    private bool _sceneChangeCommitted;
    private string? _pendingScenePath;
    private static readonly IReadOnlySet<StringName> MainMenuCoreCancelActions =
        new HashSet<StringName> { "ui_cancel" };

    internal static SaveSlotInfo? SelectContinueSave(
        IReadOnlyList<SaveSlotInfo> slots)
    {
        SaveSlotInfo? best = null;
        foreach (var candidate in slots)
        {
            if (!candidate.Exists || candidate.IsCorrupted)
                continue;

            if (best == null || IsBetterContinueCandidate(candidate, best))
                best = candidate;
        }

        return best;
    }

    private static bool IsBetterContinueCandidate(
        SaveSlotInfo candidate,
        SaveSlotInfo current)
    {
        var timestampComparison = candidate.Timestamp.CompareTo(current.Timestamp);
        if (timestampComparison != 0)
            return timestampComparison > 0;

        return ContinueTieRank(candidate.SlotIndex) < ContinueTieRank(current.SlotIndex);
    }

    private static int ContinueTieRank(int slot) => slot switch
    {
        3 => 0,
        0 => 1,
        1 => 2,
        2 => 3,
        _ => int.MaxValue
    };

    public override void _Ready()
    {
        GD.Print("Main Menu loaded");

        BindSceneNodes();
        SetupBackgroundMusic();
        Resized += OnResized;
        RefreshContinueState();
        RefreshLayout();
        Callable.From(ApplyInitialFocus).CallDeferred();
    }

    public override void _EnterTree()
    {
        _screenHost = GetNodeOrNull<UIScreenHost>("%UIScreenHost");
        _screenHost?.Configure(new UIScreenHostOptions
        {
            CoreCancelActions = MainMenuCoreCancelActions,
            RootCancelFallback = _ => UIRootCancelResult.Consumed
        });
    }

    public override void _ExitTree()
    {
        Resized -= OnResized;
    }

    private void BindSceneNodes()
    {
        _mainMenuContent = GetNodeOrNull<Control>("%MainMenuContent");
        _safeFrame = GetNodeOrNull<Control>("%SafeFrame");
        _menuRail = GetNodeOrNull<VBoxContainer>("%MenuRail");
        _wordmarkLabel = GetNodeOrNull<Label>("%WordmarkLabel");
        _continueButton = GetNodeOrNull<Button>("%ContinueButton");
        _continueSummary = GetNodeOrNull<PanelContainer>("%ContinueSummary");
        _continueSlotLabel = GetNodeOrNull<Label>("%ContinueSlotLabel");
        _continueDetailLabel = GetNodeOrNull<Label>("%ContinueDetailLabel");
        _continueTimestampLabel = GetNodeOrNull<Label>("%ContinueTimestampLabel");
        _newGameButton = GetNodeOrNull<Button>("%NewGameButton");
        _loadButton = GetNodeOrNull<Button>("%LoadButton");
        _settingsButton = GetNodeOrNull<Button>("%SettingsButton");
        _quitButton = GetNodeOrNull<Button>("%QuitButton");
        _selectHint = GetNodeOrNull<SiriusInputHint>("%SelectHint");
        _backgroundMusic = GetNodeOrNull<AudioStreamPlayer>("BackgroundMusic");

        _rootActions = new Button?[]
        {
            _continueButton,
            _newGameButton,
            _loadButton,
            _settingsButton,
            _quitButton
        }.OfType<Button>().ToArray();
    }

    private void SetupBackgroundMusic()
    {
        if (_backgroundMusic == null)
        {
            GD.PrintErr("MainMenu: BackgroundMusic node not found");
            return;
        }

        var stream = _backgroundMusic.Stream;
        switch (stream)
        {
            case AudioStreamMP3 mp3Stream:
                mp3Stream.Loop = true;
                break;
            case AudioStreamOggVorbis oggStream:
                oggStream.Loop = true;
                break;
            case AudioStreamWav wavStream:
                wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
                break;
        }

        if (!_backgroundMusic.Playing)
            _backgroundMusic.Play();
    }

    private void _on_continue_button_pressed()
    {
        if (IsRootActionBlocked() || _continueSave == null)
            return;

        HandleContinueLoadResult(LoadSlot(_continueSave.SlotIndex));
    }

    private void _on_new_game_button_pressed()
    {
        if (IsRootActionBlocked())
            return;

        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = null;

        RequestSceneChange(GameScenePath);
    }

    private void HandleContinueLoadResult(SaveData? saveData)
    {
        if (saveData != null && SaveManager.Instance != null)
        {
            SaveManager.Instance.PendingLoadData = saveData;
            RequestSceneChange(GameScenePath);
            return;
        }

        if (SaveManager.Instance != null &&
            TryOpenHostedLoad(_continueButton) &&
            _loadHandle.HasValue)
        {
            TryOpenMessage(
                "Load Failed",
                "Failed to load the selected save.",
                restoreFocus: null,
                parent: _loadHandle);
            return;
        }

        TryOpenMessage(
            "Load Failed",
            "Failed to load the selected save.",
            _continueButton);
    }

    private void RefreshContinueState()
    {
        var manager = SaveManager.Instance;
        if (manager == null || !IsInstanceValid(manager))
        {
            _continueSave = null;
            RefreshContinuePresentation();
            return;
        }

        var slots = new SaveSlotInfo[4];
        for (var slot = 0; slot < slots.Length; slot++)
            slots[slot] = manager.GetSaveSlotInfo(slot);

        _continueSave = SelectContinueSave(slots);
        RefreshContinuePresentation();
    }

    private void RefreshContinuePresentation()
    {
        if (_continueSummary == null)
            return;

        _continueSummary.Visible = _continueSave != null;
        RefreshLayout();
        RefreshActionAvailability();
    }

    private void ApplyContinueText(bool compact)
    {
        if (_continueSlotLabel == null ||
            _continueDetailLabel == null ||
            _continueTimestampLabel == null)
            return;

        var info = _continueSave;
        if (info == null)
        {
            _continueSlotLabel.Text = string.Empty;
            _continueDetailLabel.Text = string.Empty;
            _continueTimestampLabel.Text = string.Empty;
            return;
        }

        var slot = info.GetDisplayName();
        var floor = info.GetFloorName();
        _continueSlotLabel.Visible = !compact;
        _continueSlotLabel.Text = slot;
        _continueDetailLabel.Text = compact
            ? $"{slot} · {info.PlayerName} · Lv {info.PlayerLevel} · {floor}"
            : $"{info.PlayerName} · Lv {info.PlayerLevel} · {floor}";
        _continueTimestampLabel.Text = info.Timestamp == DateTime.MinValue
            ? string.Empty
            : info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        if (_safeFrame == null || _menuRail == null)
            return;

        var layout = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
        _safeFrame.OffsetLeft = layout.SideInset;
        _safeFrame.OffsetRight = -layout.SideInset;
        _safeFrame.OffsetTop = layout.Margin;
        _safeFrame.OffsetBottom = -layout.Margin;

        var railWidth = layout.Compact ? 280f : 360f;
        var minimum = SiriusUiMetrics.MinimumTarget(layout.Compact);
        foreach (var button in _rootActions)
            button.CustomMinimumSize = new Vector2(railWidth, minimum.Y);

        if (_continueSummary != null)
            _continueSummary.CustomMinimumSize = new Vector2(railWidth, 0);

        _menuRail.AddThemeConstantOverride("separation", layout.Compact ? 4 : 8);
        _menuRail.CustomMinimumSize = new Vector2(railWidth, 0);

        if (_wordmarkLabel != null)
            _wordmarkLabel.ThemeTypeVariation = layout.Compact
                ? SiriusThemeTypes.DisplayCompact
                : SiriusThemeTypes.Display;
        if (_continueSlotLabel != null)
            _continueSlotLabel.ThemeTypeVariation = layout.Compact
                ? SiriusThemeTypes.SectionCompact
                : SiriusThemeTypes.Section;
        if (_continueDetailLabel != null)
            _continueDetailLabel.ThemeTypeVariation = layout.Compact
                ? SiriusThemeTypes.BodyCompact
                : SiriusThemeTypes.Body;
        if (_continueTimestampLabel != null)
            _continueTimestampLabel.ThemeTypeVariation = layout.Compact
                ? SiriusThemeTypes.MetadataCompact
                : SiriusThemeTypes.Metadata;

        ApplyContinueText(layout.Compact);
        if (_continueTimestampLabel != null)
        {
            _continueTimestampLabel.Visible =
                !layout.Compact &&
                _continueSave != null &&
                _continueSave.Timestamp != DateTime.MinValue;
        }

        if (_selectHint != null)
            _selectHint.Compact = layout.Compact;
    }

    private void RefreshActionAvailability()
    {
        var blocked = IsRootActionBlocked();
        if (_continueButton != null)
            _continueButton.Disabled = blocked || _continueSave == null;
        if (_newGameButton != null)
            _newGameButton.Disabled = blocked;
        if (_loadButton != null)
            _loadButton.Disabled = blocked;
        if (_settingsButton != null)
            _settingsButton.Disabled = blocked;
        if (_quitButton != null)
            _quitButton.Disabled = blocked;
    }

    private bool IsRootActionBlocked() =>
        _sceneChangeCommitted ||
        (_screenHost != null &&
         IsInstanceValid(_screenHost) &&
         _screenHost.ActiveEntries.Count != 0);

    private void ApplyInitialFocus()
    {
        // Invoked via Callable.From(...).CallDeferred(); the menu (or its target
        // button) may have been detached/freed before the deferred callback runs.
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree() ||
            _newGameButton == null)
        {
            return;
        }

        var target = _continueSave != null &&
                     _continueButton != null &&
                     !_continueButton.Disabled
            ? _continueButton
            : _newGameButton;
        if (GodotObject.IsInstanceValid(target) && target.IsInsideTree())
            target.GrabFocus();
    }

    private void _on_load_button_pressed()
    {
        if (IsRootActionBlocked())
            return;

        GD.Print("Load Game button pressed");

        var saveManager = SaveManager.Instance;
        if (saveManager == null || !IsInstanceValid(saveManager))
        {
            GD.PushError("MainMenu: SaveManager is not initialized.");
            TryOpenMessage("Load Failed", "Save system unavailable.", _loadButton);
            return;
        }

        var anySaveExists = false;
        for (var i = 0; i <= 3; i++)
        {
            if (saveManager.SaveExists(i))
            {
                anySaveExists = true;
                break;
            }
        }

        if (!anySaveExists)
        {
            TryOpenMessage("Load Game", "No save files found!", _loadButton);
            return;
        }

        TryOpenHostedLoad();
    }

    private bool TryOpenHostedSettings()
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            _sceneChangeCommitted)
        {
            return false;
        }

        if (_settingsHandle.HasValue)
        {
            if (_screenHost.IsActive(_settingsHandle.Value))
                return false;

            if (_settingsMenu != null)
                ClearHostedSettings(_settingsMenu);
            else
                _settingsHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.Settings))
            return false;

        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        if (scene == null)
        {
            TryOpenMessage("Settings", "Settings unavailable.", _settingsButton);
            return false;
        }

        var settings = scene.Instantiate<SettingsMenuController>();
        if (settings == null)
        {
            TryOpenMessage("Settings", "Settings unavailable.", _settingsButton);
            return false;
        }

        settings.Closed += OnHostedSettingsClosed;
        var result = _screenHost.TryPresent(settings, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Settings,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = false,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InterceptCancel = _ =>
                settings.IsRebinding || settings.IsPopupOpen
                    ? UIInputInterception.ReserveForNativeHandler
                    : UIInputInterception.DeferToPolicy,
            InitialFocus = () => settings.InitialFocusTarget,
            RestoreFocus = () => _settingsButton,
            SetPresented = visible =>
            {
                if (visible) settings.OpenSettings(showOverlay: false);
                else settings.Hide();
            },
            Cleanup = _ => ClearHostedSettings(settings),
            NodeLifetime = UINodeLifetime.QueueFree
        });

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            settings.Closed -= OnHostedSettingsClosed;
            settings.QueueFree();
            return false;
        }

        _settingsHandle = result.Handle.Value;
        _settingsMenu = settings;
        settings.OpenSettings(showOverlay: false);
        RefreshActionAvailability();
        return true;
    }

    private void OnHostedSettingsClosed() =>
        TryCloseHostedSettings(UIScreenCloseReason.ExplicitAction);

    private bool TryCloseHostedSettings(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            !_settingsHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_settingsHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_settingsMenu != null)
                ClearHostedSettings(_settingsMenu);
            else
                _settingsHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedSettings(SettingsMenuController settings)
    {
        if (IsInstanceValid(settings))
            settings.Closed -= OnHostedSettingsClosed;

        if (ReferenceEquals(_settingsMenu, settings))
        {
            _settingsHandle = null;
            _settingsMenu = null;
        }

        RefreshActionAvailability();
    }

    private bool TryOpenHostedLoad(Control? restoreFocus = null)
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            _sceneChangeCommitted)
        {
            return false;
        }

        if (_loadHandle.HasValue)
        {
            if (_screenHost.IsActive(_loadHandle.Value))
                return false;

            if (_loadScreen != null)
                ClearHostedLoad(_loadScreen);
            else
                _loadHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.SaveLoad))
            return false;

        var restoreTarget = restoreFocus ?? _loadButton;
        var scene = GD.Load<PackedScene>("res://scenes/ui/SaveLoadScreen.tscn");
        if (scene == null)
        {
            TryOpenMessage("Load Game", "Load screen unavailable.", restoreTarget);
            return false;
        }

        var screen = scene.Instantiate<SaveLoadScreenController>();
        if (screen == null)
        {
            TryOpenMessage("Load Game", "Load screen unavailable.", restoreTarget);
            return false;
        }

        screen.Mode = SaveLoadMode.Load;
        screen.LoadSlotSelected += OnHostedLoadSlotSelected;
        screen.Closed += OnHostedLoadClosed;

        var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.SaveLoad,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = false,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => screen.InitialFocusTarget,
            RestoreFocus = () => restoreTarget,
            SetPresented = visible => screen.Visible = visible,
            Cleanup = _ => ClearHostedLoad(screen),
            NodeLifetime = UINodeLifetime.QueueFree
        });

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            screen.LoadSlotSelected -= OnHostedLoadSlotSelected;
            screen.Closed -= OnHostedLoadClosed;
            screen.QueueFree();
            return false;
        }

        _loadHandle = result.Handle.Value;
        _loadScreen = screen;
        RefreshActionAvailability();
        return true;
    }

    private void OnHostedLoadSlotSelected(int slot)
    {
        if (_sceneChangeCommitted)
            return;

        GD.Print($"Loading from slot {slot}");

        var saveData = LoadSlot(slot);
        var manager = SaveManager.Instance;
        if (saveData == null || manager == null)
        {
            TryCloseHostedLoad(UIScreenCloseReason.ExplicitAction);
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
                    return;
                TryOpenMessage(
                    "Load Failed",
                    "Failed to load save file.",
                    _loadButton);
            }).CallDeferred();
            return;
        }

        manager.PendingLoadData = saveData;
        RequestSceneChange(GameScenePath);
    }

    private void OnHostedLoadClosed() =>
        TryCloseHostedLoad(UIScreenCloseReason.ExplicitAction);

    protected virtual SaveData? LoadSlot(int slot) => slot == 3
        ? SaveManager.Instance?.LoadAutosave()
        : SaveManager.Instance?.LoadGame(slot);

    protected virtual Error ChangeSceneToFile(string path) =>
        GetTree().ChangeSceneToFile(path);

    private void RequestSceneChange(string path)
    {
        if (_sceneChangeCommitted)
            return;

        _sceneChangeCommitted = true;
        _pendingScenePath = path;
        RefreshActionAvailability();
        ContinueSceneChangeAfterUiTeardown();
    }

    private void ContinueSceneChangeAfterUiTeardown()
    {
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        if (_screenHost != null && IsInstanceValid(_screenHost) &&
            _screenHost.PrepareForTeardown() == UIScreenTeardownPreparationStatus.Deferred)
        {
            Callable.From(ContinueSceneChangeAfterUiTeardown).CallDeferred();
            return;
        }

        var path = _pendingScenePath;
        _pendingScenePath = null;
        if (!string.IsNullOrEmpty(path))
            ChangeSceneToFile(path);
    }

    private bool TryCloseHostedLoad(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            !_loadHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_loadHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_loadScreen != null)
                ClearHostedLoad(_loadScreen);
            else
                _loadHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedLoad(SaveLoadScreenController screen)
    {
        if (IsInstanceValid(screen))
        {
            screen.LoadSlotSelected -= OnHostedLoadSlotSelected;
            screen.Closed -= OnHostedLoadClosed;
        }

        if (ReferenceEquals(_loadScreen, screen))
        {
            _loadHandle = null;
            _loadScreen = null;
        }

        RefreshActionAvailability();
    }

    private bool TryOpenMessage(
        string title,
        string message,
        Control? restoreFocus,
        UIScreenHandle? parent = null)
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            _screenHost.IsKindActive(UIScreenKinds.SaveError))
        {
            return false;
        }

        var popup = new AcceptDialog
        {
            Title = title,
            DialogText = message,
            Exclusive = true,
            Theme = GD.Load<Theme>(SiriusThemeTypes.ResourcePath)
        };

        var handled = false;
        Action close = () =>
        {
            if (handled)
                return;
            handled = true;
            TryCloseHostedMessage(UIScreenCloseReason.ExplicitAction);
        };

        _messageCloseDelegate = close;
        popup.Confirmed += close;
        popup.Canceled += close;
        popup.CloseRequested += close;

        var result = _screenHost.TryPresent(popup, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.SaveError,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            Parent = parent,
            ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt,
            PauseTree = false,
            BlockGameplayInput = false,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => popup.GetOkButton(),
            RestoreFocus = parent.HasValue || restoreFocus == null
                ? null
                : () => restoreFocus,
            SetPresented = visible =>
            {
                if (visible) popup.PopupCentered();
                else popup.Hide();
            },
            Cleanup = _ => ClearHostedMessage(popup),
            NodeLifetime = UINodeLifetime.QueueFree
        });

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            popup.Confirmed -= close;
            popup.Canceled -= close;
            popup.CloseRequested -= close;
            _messageCloseDelegate = null;
            popup.QueueFree();
            return false;
        }

        _messageHandle = result.Handle.Value;
        _messageDialog = popup;
        popup.PopupCentered();
        RefreshActionAvailability();
        return true;
    }

    private bool TryCloseHostedMessage(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !IsInstanceValid(_screenHost) ||
            !_messageHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_messageHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_messageDialog != null)
                ClearHostedMessage(_messageDialog);
            else
                _messageHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedMessage(AcceptDialog popup)
    {
        if (IsInstanceValid(popup) && _messageCloseDelegate != null)
        {
            popup.Confirmed -= _messageCloseDelegate;
            popup.Canceled -= _messageCloseDelegate;
            popup.CloseRequested -= _messageCloseDelegate;
        }

        if (ReferenceEquals(_messageDialog, popup))
        {
            if (IsInstanceValid(popup))
                popup.QueueFree();
            _messageHandle = null;
            _messageDialog = null;
            _messageCloseDelegate = null;
        }

        RefreshActionAvailability();
    }

    private void _on_settings_button_pressed()
    {
        if (IsRootActionBlocked())
            return;

        TryOpenHostedSettings();
    }

    private void _on_quit_button_pressed()
    {
        if (IsRootActionBlocked())
            return;

        GD.Print("Quit button pressed");
        _sceneChangeCommitted = true;
        RefreshActionAvailability();
        RequestApplicationQuit();
    }

    protected virtual void RequestApplicationQuit() => GetTree().Quit();
}
