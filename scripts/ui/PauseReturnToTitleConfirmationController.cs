using Godot;

public partial class PauseReturnToTitleConfirmationController : Control
{
    [Signal] public delegate void ReturnToTitleConfirmedEventHandler();
    [Signal] public delegate void CancelRequestedEventHandler();

    private Button _return = null!;
    private Button _cancel = null!;

    public Control InitialFocusTarget => _cancel;

    public override void _Ready()
    {
        _return = GetNode<Button>("%ReturnToTitleButton");
        _cancel = GetNode<Button>("%CancelButton");
        _return.Pressed += OnReturn;
        _cancel.Pressed += OnCancel;
    }

    private void OnReturn() => EmitSignal(SignalName.ReturnToTitleConfirmed);

    private void OnCancel() => EmitSignal(SignalName.CancelRequested);

    public override void _ExitTree()
    {
        _return.Pressed -= OnReturn;
        _cancel.Pressed -= OnCancel;
    }
}
