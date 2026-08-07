using Godot;

public partial class PauseScreenController : Control
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void InventoryRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void ReturnToTitleRequestedEventHandler();

    private SiriusModalShell _shell = null!;
    private Button _resume = null!;
    private Button _inventory = null!;
    private Button _save = null!;
    private Button _load = null!;
    private Button _settings = null!;
    private Button _returnToTitle = null!;

    public Control InitialFocusTarget => _resume;

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _resume = GetNode<Button>("%ResumeButton");
        _inventory = GetNode<Button>("%InventoryButton");
        _save = GetNode<Button>("%SaveButton");
        _load = GetNode<Button>("%LoadButton");
        _settings = GetNode<Button>("%SettingsButton");
        _returnToTitle = GetNode<Button>("%ReturnToTitleButton");

        Resized += OnResized;
        BindButtons();
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        Resized -= OnResized;
        UnbindButtons();
    }

    private void BindButtons()
    {
        _resume.Pressed += OnResumePressed;
        _inventory.Pressed += OnInventoryPressed;
        _save.Pressed += OnSavePressed;
        _load.Pressed += OnLoadPressed;
        _settings.Pressed += OnSettingsPressed;
        _returnToTitle.Pressed += OnReturnToTitlePressed;
    }

    private void UnbindButtons()
    {
        _resume.Pressed -= OnResumePressed;
        _inventory.Pressed -= OnInventoryPressed;
        _save.Pressed -= OnSavePressed;
        _load.Pressed -= OnLoadPressed;
        _settings.Pressed -= OnSettingsPressed;
        _returnToTitle.Pressed -= OnReturnToTitlePressed;
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        var size = GetViewportRect().Size;
        _shell.Compact = SiriusUiMetrics.IsCompact(size);
        _shell.RefreshPresentation(size);
    }

    private void OnResumePressed() => EmitSignal(SignalName.ResumeRequested);

    private void OnInventoryPressed() => EmitSignal(SignalName.InventoryRequested);

    private void OnSavePressed() => EmitSignal(SignalName.SaveRequested);

    private void OnLoadPressed() => EmitSignal(SignalName.LoadRequested);

    private void OnSettingsPressed() => EmitSignal(SignalName.SettingsRequested);

    private void OnReturnToTitlePressed() => EmitSignal(SignalName.ReturnToTitleRequested);
}
