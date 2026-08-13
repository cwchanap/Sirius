using Godot;
using System;

public enum SaveLoadMode
{
    Save,
    Load
}

public partial class SaveLoadScreenController : Control
{
    [Signal] public delegate void SaveSlotSelectedEventHandler(int slot);
    [Signal] public delegate void LoadSlotSelectedEventHandler(int slot);
    [Signal] public delegate void OverwriteRequestedEventHandler(int slot);
    [Signal] public delegate void ClosedEventHandler();
    [Signal] public delegate void MainMenuRequestedEventHandler();

    private SaveLoadMode _mode = SaveLoadMode.Save;
    private readonly SaveSlotInfo[] _slotInfos = new SaveSlotInfo[4];

    private Button[] _cards = null!;
    private Label[] _slotNameLabels = null!;
    private Label[] _detailLabels = null!;
    private Label[] _timestampLabels = null!;
    private Label[] _stateLabels = null!;
    private Label[] _actionLabels = null!;
    private Action[] _cardHandlers = null!;
    private GridContainer _cardsGrid = null!;
    private SiriusModalShell _shell = null!;
    private Button _mainMenuButton = null!;
    private Button _cancelButton = null!;
    private bool _saveSystemUnavailable;
    private bool _terminalEmitted;

    public SaveLoadMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            if (IsNodeReady())
            {
                ApplyModePresentation();
                RefreshSlotInfo();
                RefreshLayout();
            }
        }
    }

    public Control InitialFocusTarget => FirstEnabledCard() ?? _cancelButton;

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _cardsGrid = GetNode<GridContainer>("%CardsGrid");
        _mainMenuButton = GetNode<Button>("%MainMenuButton");
        _cancelButton = GetNode<Button>("%CancelButton");

        _cards = new Button[4];
        _slotNameLabels = new Label[4];
        _detailLabels = new Label[4];
        _timestampLabels = new Label[4];
        _stateLabels = new Label[4];
        _actionLabels = new Label[4];
        _cardHandlers = new Action[4];

        for (var slot = 0; slot < _cards.Length; slot++)
        {
            _slotInfos[slot] = new SaveSlotInfo { SlotIndex = slot };
            _cards[slot] = GetNode<Button>($"%Slot{slot}Card");
            _slotNameLabels[slot] = _cards[slot].GetNode<Label>("Margin/Content/SlotNameLabel");
            _detailLabels[slot] = _cards[slot].GetNode<Label>("Margin/Content/DetailLabel");
            _timestampLabels[slot] = _cards[slot].GetNode<Label>("Margin/Content/TimestampLabel");
            _stateLabels[slot] = _cards[slot].GetNode<Label>("Margin/Content/StateLabel");
            _actionLabels[slot] = _cards[slot].GetNode<Label>("Margin/Content/ActionLabel");

            var capturedSlot = slot;
            _cardHandlers[slot] = () => OnSlotPressed(capturedSlot);
            _cards[slot].Pressed += _cardHandlers[slot];
        }

        _mainMenuButton.Pressed += OnMainMenuPressed;
        _cancelButton.Pressed += OnCancelPressed;
        Resized += OnResized;

        ApplyModePresentation();
        RefreshSlotInfo();
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_cards != null && _cardHandlers != null)
        {
            for (var slot = 0; slot < _cards.Length; slot++)
            {
                if (_cards[slot] != null && _cardHandlers[slot] != null)
                    _cards[slot].Pressed -= _cardHandlers[slot];
            }
        }

        if (_mainMenuButton != null)
            _mainMenuButton.Pressed -= OnMainMenuPressed;
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
        Resized -= OnResized;
    }

    private void OnResized() => RefreshLayout();

    private void ApplyModePresentation()
    {
        if (_shell == null)
            return;

        _shell.Title = Mode == SaveLoadMode.Save ? "Save Game" : "Load Game";
        _mainMenuButton.Visible = Mode == SaveLoadMode.Save;
    }

    private void RefreshSlotInfo()
    {
        if (_cards == null)
            return;

        var manager = SaveManager.Instance;
        if (manager == null || !GodotObject.IsInstanceValid(manager))
        {
            _saveSystemUnavailable = true;
            for (var slot = 0; slot < _cards.Length; slot++)
            {
                _slotInfos[slot] = new SaveSlotInfo
                {
                    Exists = true,
                    IsCorrupted = true,
                    State = SaveSlotState.Corrupted,
                    SlotIndex = slot
                };
                ApplyUnavailablePresentation(slot, "Save system unavailable");
                _cards[slot].Disabled = true;
            }

            return;
        }

        _saveSystemUnavailable = false;
        for (var slot = 0; slot < _cards.Length; slot++)
        {
            _slotInfos[slot] = manager.GetSaveSlotInfo(slot)
                ?? new SaveSlotInfo { SlotIndex = slot };
            ApplySlotPresentation(slot, _slotInfos[slot]);
        }
    }

    private void ApplySlotPresentation(int slot, SaveSlotInfo info)
    {
        var slotName = info.GetDisplayName();
        _slotNameLabels[slot].Text = slotName;

        switch (info.State)
        {
            case SaveSlotState.Valid:
                // PlayerName is intentionally only rendered for valid metadata.
                _detailLabels[slot].Text =
                    $"{info.PlayerName}\nLevel {info.PlayerLevel} • {info.GetFloorName()}";
                _timestampLabels[slot].Text = info.Timestamp == DateTime.MinValue
                    ? string.Empty
                    : info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                _stateLabels[slot].Text = "Valid save";
                _actionLabels[slot].Text = Mode == SaveLoadMode.Save
                    ? slot == 3 ? "Autosave is read-only" : "Overwrite save"
                    : "Load save";
                break;

            case SaveSlotState.Corrupted:
                _detailLabels[slot].Text = "The save file cannot be read.";
                _timestampLabels[slot].Text = string.Empty;
                _stateLabels[slot].Text = "Corrupted save";
                _actionLabels[slot].Text = Mode == SaveLoadMode.Save && slot != 3
                    ? "Replace corrupted save"
                    : "Unavailable: corrupted save";
                break;

            case SaveSlotState.Incompatible:
                _detailLabels[slot].Text = "This save was created by a newer game version.";
                _timestampLabels[slot].Text = string.Empty;
                _stateLabels[slot].Text = "Incompatible save — update required.";
                _actionLabels[slot].Text = Mode == SaveLoadMode.Save && slot != 3
                    ? "Replace incompatible save"
                    : "Unavailable: incompatible save";
                break;

            default:
                _detailLabels[slot].Text = "No save data";
                _timestampLabels[slot].Text = string.Empty;
                _stateLabels[slot].Text = "Empty";
                _actionLabels[slot].Text = Mode == SaveLoadMode.Save && slot != 3
                    ? "Save here"
                    : "Unavailable: empty slot";
                break;
        }

        if (Mode == SaveLoadMode.Save && slot == 3)
            _actionLabels[slot].Text = "Autosave is read-only";

        _cards[slot].Disabled = !CanActivateSlot(slot, info);
    }

    private void ApplyUnavailablePresentation(int slot, string reason)
    {
        _slotNameLabels[slot].Text = slot == 3 ? "Autosave" : $"Slot {slot + 1}";
        _detailLabels[slot].Text = "Save metadata is unavailable.";
        _timestampLabels[slot].Text = string.Empty;
        _stateLabels[slot].Text = "Unavailable";
        _actionLabels[slot].Text = reason;
    }

    private bool CanActivateSlot(int slot, SaveSlotInfo info)
    {
        if (_terminalEmitted || _saveSystemUnavailable || slot < 0 || slot >= _cards.Length)
            return false;

        if (Mode == SaveLoadMode.Save)
            return slot != 3;

        return info.State == SaveSlotState.Valid;
    }

    private void OnSlotPressed(int slot)
    {
        if (slot < 0 || slot >= _slotInfos.Length)
            return;

        var info = _slotInfos[slot];
        if (!CanActivateSlot(slot, info))
            return;

        if (Mode == SaveLoadMode.Save && info.State == SaveSlotState.Valid)
        {
            EmitSignal(SignalName.OverwriteRequested, slot);
            return;
        }

        if (!TryBeginTerminal())
            return;

        DisableAllActions();
        EmitSignal(
            Mode == SaveLoadMode.Save
                ? SignalName.SaveSlotSelected
                : SignalName.LoadSlotSelected,
            slot);
    }

    private void OnCancelPressed()
    {
        if (!TryBeginTerminal())
            return;

        DisableAllActions();
        EmitSignal(SignalName.Closed);
    }

    private void OnMainMenuPressed()
    {
        if (Mode != SaveLoadMode.Save || !TryBeginTerminal())
            return;

        DisableAllActions();
        EmitSignal(SignalName.MainMenuRequested);
    }

    private bool TryBeginTerminal()
    {
        if (_terminalEmitted)
            return false;

        _terminalEmitted = true;
        return true;
    }

    private void DisableAllActions()
    {
        if (_cards != null)
        {
            foreach (var card in _cards)
                card.Disabled = true;
        }

        if (_mainMenuButton != null)
            _mainMenuButton.Disabled = true;
        if (_cancelButton != null)
            _cancelButton.Disabled = true;
    }

    private Control? FirstEnabledCard()
    {
        if (_cards == null)
            return null;

        foreach (var card in _cards)
        {
            if (!card.Disabled)
                return card;
        }

        return null;
    }

    private void RefreshLayout()
    {
        if (_shell == null || _cardsGrid == null)
            return;

        var size = GetViewportRect().Size;
        var compact = SiriusUiMetrics.IsCompact(size);
        _shell.Compact = compact;
        _cardsGrid.Columns = compact ? 1 : 2;
        ApplyCardTypography(compact);
        ApplyMinimumTargets(compact);
        _shell.RefreshPresentation(size);
    }

    private void ApplyCardTypography(bool compact)
    {
        if (_cards == null)
            return;

        for (var slot = 0; slot < _cards.Length; slot++)
        {
            _slotNameLabels[slot].ThemeTypeVariation = compact
                ? SiriusThemeTypes.SectionCompact
                : SiriusThemeTypes.Section;
            _detailLabels[slot].ThemeTypeVariation = compact
                ? SiriusThemeTypes.BodyCompact
                : SiriusThemeTypes.Body;
            _timestampLabels[slot].ThemeTypeVariation = compact
                ? SiriusThemeTypes.MetadataCompact
                : SiriusThemeTypes.Metadata;
            _stateLabels[slot].ThemeTypeVariation = compact
                ? SiriusThemeTypes.BodyCompact
                : SiriusThemeTypes.Body;
            _actionLabels[slot].ThemeTypeVariation = compact
                ? SiriusThemeTypes.MetadataCompact
                : SiriusThemeTypes.Metadata;

            _slotNameLabels[slot].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _detailLabels[slot].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _timestampLabels[slot].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _stateLabels[slot].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _actionLabels[slot].AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }
    }

    private void ApplyMinimumTargets(bool compact)
    {
        var target = SiriusUiMetrics.MinimumTarget(compact);
        var cardMinimumHeight = Mathf.Max(target.Y, compact ? 112f : 120f);

        foreach (var card in _cards)
            card.CustomMinimumSize = new Vector2(0, cardMinimumHeight);

        _mainMenuButton.CustomMinimumSize = new Vector2(0, target.Y);
        _cancelButton.CustomMinimumSize = new Vector2(0, target.Y);
    }
}
