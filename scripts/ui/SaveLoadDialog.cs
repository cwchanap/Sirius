using Godot;
using System;

/// <summary>
/// Dialog for save/load slot selection.
/// Works in both Save and Load modes.
/// </summary>
public partial class SaveLoadDialog : AcceptDialog
{
    public enum DialogMode { Save, Load }

    [Signal]
    public delegate void SaveSlotSelectedEventHandler(int slot);

    [Signal]
    public delegate void LoadSlotSelectedEventHandler(int slot);

    [Signal]
    public delegate void DialogClosedEventHandler();

    [Signal]
    public delegate void MainMenuRequestedEventHandler();

    private DialogMode _mode;
    private VBoxContainer _slotContainer;
    private Button[] _slotButtons = new Button[4];
    private Label[] _slotLabels = new Label[4];
    private Button _mainMenuButton;
    private Button _cancelButton;
    private Action[] _slotButtonHandlers = new Action[4];
    private SaveSlotInfo[] _slotInfos = new SaveSlotInfo[4];
    private int _pendingSaveSlot = -1;

    public override void _Ready()
    {
        // Set up dialog properties
        Title = "Save Game";
        Size = new Vector2I(400, 350);
        Exclusive = true;

        // Hide the default OK button
        GetOkButton().Visible = false;

        // Create content container
        var mainContainer = new VBoxContainer();
        mainContainer.AddThemeConstantOverride("separation", 10);
        AddChild(mainContainer);

        // Create slot container
        _slotContainer = new VBoxContainer();
        _slotContainer.AddThemeConstantOverride("separation", 8);
        mainContainer.AddChild(_slotContainer);

        // Create slot buttons
        for (int i = 0; i < 4; i++)
        {
            var slotButton = new Button();
            slotButton.CustomMinimumSize = new Vector2(350, 50);
            slotButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var slotLabel = new Label();
            slotLabel.Text = i == 3 ? "Autosave - Empty" : $"Slot {i + 1} - Empty";
            slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            slotLabel.VerticalAlignment = VerticalAlignment.Center;
            slotLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

            slotButton.AddChild(slotLabel);
            _slotContainer.AddChild(slotButton);

            _slotButtons[i] = slotButton;
            _slotLabels[i] = slotLabel;

            int slotIndex = i; // Capture for closure
            _slotButtonHandlers[i] = () => OnSlotPressed(slotIndex);
            slotButton.Pressed += _slotButtonHandlers[i];
        }

        // Add spacer
        var spacer = new Control();
        spacer.CustomMinimumSize = new Vector2(0, 10);
        mainContainer.AddChild(spacer);

        // Create button row
        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 10);
        buttonRow.Alignment = BoxContainer.AlignmentMode.Center;
        mainContainer.AddChild(buttonRow);

        // Main Menu button
        _mainMenuButton = new Button();
        _mainMenuButton.Text = "Main Menu";
        _mainMenuButton.CustomMinimumSize = new Vector2(120, 35);
        _mainMenuButton.Pressed += OnMainMenuPressed;
        buttonRow.AddChild(_mainMenuButton);

        // Cancel button
        _cancelButton = new Button();
        _cancelButton.Text = "Cancel";
        _cancelButton.CustomMinimumSize = new Vector2(120, 35);
        _cancelButton.Pressed += OnCancelPressed;
        buttonRow.AddChild(_cancelButton);

        // Connect dialog close signal
        CloseRequested += OnCloseRequested;
    }

    public override void _ExitTree()
    {
        CloseRequested -= OnCloseRequested;
        if (_mainMenuButton != null)
            _mainMenuButton.Pressed -= OnMainMenuPressed;
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;

        // Disconnect slot button handlers
        for (int i = 0; i < _slotButtons.Length; i++)
        {
            if (_slotButtons[i] != null && _slotButtonHandlers[i] != null)
            {
                _slotButtons[i].Pressed -= _slotButtonHandlers[i];
            }
        }
    }

    /// <summary>
    /// Shows the dialog in the specified mode.
    /// </summary>
    public void ShowDialog(DialogMode mode)
    {
        _mode = mode;
        Title = mode == DialogMode.Save ? "Save Game" : "Load Game";

        // Show/hide main menu button based on mode
        if (_mainMenuButton != null)
        {
            _mainMenuButton.Visible = mode == DialogMode.Save;
        }

        RefreshSlotInfo();
        PopupCentered();
    }

    private void RefreshSlotInfo()
    {
        if (SaveManager.Instance == null)
        {
            GD.PushError("SaveManager not initialized - cannot load save slot info");
            // Show error state in UI
            for (int i = 0; i < 4; i++)
            {
                _slotLabels[i].Text = "Error: Save system unavailable";
                _slotButtons[i].Disabled = true;
            }
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            var info = SaveManager.Instance.GetSaveSlotInfo(i);
            _slotInfos[i] = info;

            if (info == null || !info.Exists)
            {
                string slotName = i == 3 ? "Autosave" : $"Slot {i + 1}";
                _slotLabels[i].Text = $"{slotName} - Empty";
                _slotButtons[i].Disabled = _mode == DialogMode.Load || (i == 3 && _mode == DialogMode.Save);
            }
            else if (info.IsCorrupted)
            {
                string slotName = i == 3 ? "Autosave" : $"Slot {i + 1}";
                _slotLabels[i].Text = $"{slotName} - CORRUPTED\n(File exists but cannot be read)";
                // Can overwrite corrupted slots in save mode, except autosave (slot 3) which is read-only
                _slotButtons[i].Disabled = _mode == DialogMode.Load || (i == 3 && _mode == DialogMode.Save);
            }
            else
            {
                string slotName = info.GetDisplayName();
                // Convert UTC timestamp to local time for display
                string timestamp = info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                _slotLabels[i].Text = $"{slotName}\nLevel {info.PlayerLevel} - {info.GetFloorName()}\n{timestamp}";

                // Autosave slot is read-only in Save mode
                _slotButtons[i].Disabled = (i == 3 && _mode == DialogMode.Save);
            }
        }
    }

    private void OnSlotPressed(int slot)
    {
        GD.Print($"Slot {slot} pressed in {_mode} mode");

        if (_mode == DialogMode.Save)
        {
            // Check if slot has existing save data that would be overwritten
            if (_slotInfos[slot] != null && _slotInfos[slot].Exists && !_slotInfos[slot].IsCorrupted && slot != 3)
            {
                // Show overwrite confirmation dialog
                _pendingSaveSlot = slot;
                ShowOverwriteConfirmation(slot);
            }
            else
            {
                // Empty slot, corrupted, or autosave - proceed immediately
                EmitSignal(SignalName.SaveSlotSelected, slot);
            }
        }
        else
        {
            EmitSignal(SignalName.LoadSlotSelected, slot);
            Hide();
        }
    }

    private void ShowOverwriteConfirmation(int slot)
    {
        var confirmDialog = new AcceptDialog();
        confirmDialog.Title = "Overwrite Save?";
        var info = _slotInfos[slot];
        string slotName = info?.GetDisplayName() ?? $"Slot {slot + 1}";
        confirmDialog.DialogText = $"{slotName} already has a save.\nLevel {info?.PlayerLevel} - {info?.GetFloorName()}\n\nOverwrite this save?";
        
        AddChild(confirmDialog);
        
        // Connect to confirmed (Overwrite) signal
        confirmDialog.Confirmed += () =>
        {
            if (IsInstanceValid(confirmDialog))
                confirmDialog.QueueFree();
            
            if (_pendingSaveSlot >= 0)
            {
                EmitSignal(SignalName.SaveSlotSelected, _pendingSaveSlot);
                _pendingSaveSlot = -1;
            }
        };
        
        // Also handle cancel - user stays in save dialog
        confirmDialog.Canceled += () =>
        {
            if (IsInstanceValid(confirmDialog))
                confirmDialog.QueueFree();
            _pendingSaveSlot = -1;
        };
        
        confirmDialog.PopupCentered();
    }

    private void OnMainMenuPressed()
    {
        GD.Print("Main Menu button pressed");
        Hide();

        // The save dialog should only be reachable when not in battle.
        // This defensive check is a safeguard against future changes.
        // Note: Direct EndBattle bypasses Game.cs cleanup - prefer normal flow.
        if (GameManager.Instance?.IsInBattle == true)
        {
            GD.PushWarning("SaveLoadDialog.OnMainMenuPressed called while in battle - this should not happen");
            GameManager.Instance.EndBattle(false);
        }

        EmitSignal(SignalName.MainMenuRequested);
        EmitSignal(SignalName.DialogClosed);
    }

    private void OnCancelPressed()
    {
        GD.Print("Cancel button pressed");
        Hide();
        EmitSignal(SignalName.DialogClosed);
    }

    private void OnCloseRequested()
    {
        Hide();
        EmitSignal(SignalName.DialogClosed);
    }
}
