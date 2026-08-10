using System;
using System.Collections.Generic;
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
    private SaveLoadDialog? _loadDialog;
    private SettingsMenuController? _settingsMenu;

    private SaveSlotInfo? _continueSave;
    private Button[] _rootActions = Array.Empty<Button>();
    private bool _sceneChangeCommitted;

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

    public override void _ExitTree()
    {
        Resized -= OnResized;
        CleanupLoadDialog();
        if (_settingsMenu != null && IsInstanceValid(_settingsMenu))
        {
            _settingsMenu.Closed -= OnSettingsClosed;
            _settingsMenu.QueueFree();
            _settingsMenu = null;
        }
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

        _rootActions = new[]
        {
            _continueButton!,
            _newGameButton!,
            _loadButton!,
            _settingsButton!,
            _quitButton!
        };
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
        if (_sceneChangeCommitted || _continueSave == null)
            return;
    }

    private void _on_new_game_button_pressed()
    {
        if (_sceneChangeCommitted)
            return;

        _sceneChangeCommitted = true;
        RefreshActionAvailability();

        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = null;

        GetTree().ChangeSceneToFile(GameScenePath);
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

        foreach (var button in _rootActions)
        {
            button.CustomMinimumSize = new Vector2(
                button.CustomMinimumSize.X,
                SiriusUiMetrics.MinimumTarget(layout.Compact).Y);
        }

        _menuRail.AddThemeConstantOverride("separation", layout.Compact ? 4 : 8);
        _menuRail.CustomMinimumSize = new Vector2(layout.Compact ? 280 : 360, 0);

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
        var blocked = _sceneChangeCommitted;
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

    private void ApplyInitialFocus()
    {
        if (_newGameButton == null)
            return;

        var target = _continueSave != null &&
                     _continueButton != null &&
                     !_continueButton.Disabled
            ? _continueButton
            : _newGameButton;
        target.GrabFocus();
    }

    private void _on_load_button_pressed()
    {
        if (_sceneChangeCommitted)
            return;

        GD.Print("Load Game button pressed");

        var saveManager = SaveManager.Instance;
        if (saveManager == null || !IsInstanceValid(saveManager))
        {
            GD.PushError("MainMenu: SaveManager is not initialized.");
            ShowMessage("Save system unavailable.");
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
            ShowMessage("No save files found!");
            return;
        }

        if (_loadDialog != null)
            CleanupLoadDialog();

        _loadDialog = new SaveLoadDialog();
        _loadDialog.LoadSlotSelected += OnLoadSlotSelected;
        _loadDialog.DialogClosed += OnLoadDialogClosed;
        AddChild(_loadDialog);
        _loadDialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
    }

    private void OnLoadSlotSelected(int slot)
    {
        if (_sceneChangeCommitted)
            return;

        GD.Print($"Loading from slot {slot}");

        var saveData = slot == 3
            ? SaveManager.Instance?.LoadAutosave()
            : SaveManager.Instance?.LoadGame(slot);

        var manager = SaveManager.Instance;
        if (saveData != null && manager != null)
        {
            _sceneChangeCommitted = true;
            RefreshActionAvailability();
            manager.PendingLoadData = saveData;
            CleanupLoadDialog();
            GetTree().ChangeSceneToFile(GameScenePath);
        }
        else
        {
            ShowMessage("Failed to load save file!");
            CleanupLoadDialog();
        }
    }

    private void OnLoadDialogClosed() => CleanupLoadDialog();

    private void CleanupLoadDialog()
    {
        if (_loadDialog == null)
            return;

        _loadDialog.LoadSlotSelected -= OnLoadSlotSelected;
        _loadDialog.DialogClosed -= OnLoadDialogClosed;
        _loadDialog.QueueFree();
        _loadDialog = null;
    }

    private void _on_settings_button_pressed()
    {
        if (_settingsMenu != null)
            return;

        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        if (scene == null)
        {
            ShowMessage("Settings unavailable.");
            return;
        }

        _settingsMenu = scene.Instantiate<SettingsMenuController>();
        _settingsMenu.Closed += OnSettingsClosed;
        AddChild(_settingsMenu);
        _settingsMenu.OpenSettings();
        _settingsMenu.InitialFocusTarget.GrabFocus();
    }

    private void OnSettingsClosed()
    {
        if (_settingsMenu == null)
            return;

        _settingsMenu.Closed -= OnSettingsClosed;
        _settingsMenu.QueueFree();
        _settingsMenu = null;
        if (_settingsButton != null && IsInstanceValid(_settingsButton))
            _settingsButton.GrabFocus();
    }

    private void _on_quit_button_pressed()
    {
        if (_sceneChangeCommitted)
            return;

        GD.Print("Quit button pressed");
        _sceneChangeCommitted = true;
        RefreshActionAvailability();
        RequestApplicationQuit();
    }

    protected virtual void RequestApplicationQuit() => GetTree().Quit();

    private void ShowMessage(string message)
    {
        var popup = new AcceptDialog
        {
            DialogText = message
        };
        AddChild(popup);
        popup.PopupCentered();

        if (message != "Quitting game...")
        {
            GetTree().CreateTimer(2.0).Timeout += () =>
            {
                if (IsInstanceValid(popup))
                    popup.QueueFree();
            };
        }
    }
}
