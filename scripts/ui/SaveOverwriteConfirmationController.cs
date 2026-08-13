using Godot;

public partial class SaveOverwriteConfirmationController : Control
{
    [Signal] public delegate void OverwriteConfirmedEventHandler(int slot);
    [Signal] public delegate void CancelRequestedEventHandler();

    private int _slot;
    private Label _message = null!;
    private Button _overwrite = null!;
    private Button _cancel = null!;
    private SiriusModalShell _shell = null!;
    private bool _terminalEmitted;

    public int Slot
    {
        get => _slot;
        set
        {
            _slot = value;
            if (IsNodeReady())
                RefreshMessage();
        }
    }

    public Control InitialFocusTarget => _cancel;

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _message = GetNode<Label>("%Message");
        _overwrite = GetNode<Button>("%OverwriteButton");
        _cancel = GetNode<Button>("%CancelButton");
        RefreshMessage();
        RefreshLayout();

        _overwrite.Pressed += OnOverwrite;
        _cancel.Pressed += OnCancel;
        Resized += OnResized;
    }

    public override void _ExitTree()
    {
        if (_overwrite != null)
            _overwrite.Pressed -= OnOverwrite;
        if (_cancel != null)
            _cancel.Pressed -= OnCancel;
        Resized -= OnResized;
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        if (_shell == null)
            return;

        var size = GetViewportRect().Size;
        var compact = SiriusUiMetrics.IsCompact(size);
        _shell.Compact = compact;

        var target = SiriusUiMetrics.MinimumTarget(compact);
        _overwrite.CustomMinimumSize = new Vector2(0, target.Y);
        _cancel.CustomMinimumSize = new Vector2(0, target.Y);

        _shell.RefreshPresentation(size);
    }

    private void RefreshMessage() =>
        _message.Text = $"Slot {_slot + 1} already contains save data. Overwrite it?";

    private void OnOverwrite()
    {
        if (_terminalEmitted)
            return;

        _terminalEmitted = true;
        EmitSignal(SignalName.OverwriteConfirmed, _slot);
    }

    private void OnCancel()
    {
        if (_terminalEmitted)
            return;

        _terminalEmitted = true;
        EmitSignal(SignalName.CancelRequested);
    }
}
