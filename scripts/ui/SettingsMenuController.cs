using Godot;
using System.Collections.Generic;

public partial class SettingsMenuController : Control
{
    [Signal] public delegate void ClosedEventHandler();

    internal SettingsData EditedSettings => _editedSettings;

    private SettingsData _editedSettings = SettingsData.CreateDefaults();
    private string _listeningAction;

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
        var content = GetNode<VBoxContainer>("Center/Panel/Content");
        BuildUI(content);
        Hide();
    }

    public void OpenSettings(SettingsData snapshot = null)
    {
        var source = snapshot ?? SettingsManager.Instance?.GetSnapshot() ?? SettingsData.CreateDefaults();
        _editedSettings = source.Clone();
        PopulateControls();
        Show();
    }

    // Stubs — implemented in Tasks 4 and 5
    private void BuildUI(VBoxContainer content) { }
    private void PopulateControls() { }
    private void StartKeyCapture(string action) { }
    private void OnApplyPressed() { }

    private void OnCancelPressed()
    {
        CancelKeyCapture();
        EmitSignal(SignalName.Closed);
    }

    private void CancelKeyCapture()
    {
        if (_listeningAction == null) return;
        var prev = _listeningAction;
        _listeningAction = null;
        if (_errorLabel != null) _errorLabel.Visible = false;
        SetProcessInput(false);
        if (GetKeyButtonSafe(prev) is Button btn)
            UpdateKeyButtonText(btn, prev);
    }

    private void UpdateKeyButtonText(Button btn, string action)
    {
        if (_editedSettings.PrimaryKeybindings.TryGetValue(action, out var code))
            btn.Text = code <= 0 ? "(unbound)" : OS.GetKeycodeString((Key)code);
        else
            btn.Text = "(unbound)";
    }

    private Button GetKeyButtonSafe(string action) => action switch
    {
        "toggle_inventory" => _inventoryKeyBtn,
        "interact"         => _interactKeyBtn,
        "pause_menu"       => _pauseKeyBtn,
        _                  => null
    };
}
