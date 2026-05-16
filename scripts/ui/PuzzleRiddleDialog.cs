using Godot;

/// <summary>
/// Modal choice dialog for world puzzle riddles.
/// Instantiated per puzzle interaction and cleaned up by Game when closed.
/// </summary>
public partial class PuzzleRiddleDialog : AcceptDialog
{
    [Signal] public delegate void ChoiceSelectedEventHandler(string choiceId);
    [Signal] public delegate void PuzzleRiddleClosedEventHandler();

    private Label _messageLabel = null!;
    private RichTextLabel _promptLabel = null!;
    private VBoxContainer _choicesContainer = null!;

    public override void _Ready()
    {
        Title = "Seal";
        Size = new Vector2I(520, 320);
        Exclusive = true;
        GetOkButton().Visible = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _messageLabel = new Label();
        _messageLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_messageLabel);

        _promptLabel = new RichTextLabel();
        _promptLabel.BbcodeEnabled = true;
        _promptLabel.FitContent = true;
        _promptLabel.CustomMinimumSize = new Vector2(460, 90);
        _promptLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(_promptLabel);

        _choicesContainer = new VBoxContainer();
        _choicesContainer.AddThemeConstantOverride("separation", 4);
        root.AddChild(_choicesContainer);

        CloseRequested += OnCloseRequested;
        Canceled += OnCloseRequested;
    }

    public void OpenRiddle(PuzzleRiddleSpawn riddle, string? message = null)
    {
        Title = string.IsNullOrWhiteSpace(riddle.RiddleId) ? "Seal" : riddle.RiddleId;
        _messageLabel.Text = message ?? "";
        _messageLabel.Visible = !string.IsNullOrWhiteSpace(_messageLabel.Text);
        _promptLabel.Text = riddle.PromptText ?? "";

        foreach (Node child in _choicesContainer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var choice in riddle.GetChoices())
        {
            var button = new Button
            {
                Text = choice.Label,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            string capturedId = choice.Id;
            button.Pressed += () => EmitSignal(SignalName.ChoiceSelected, capturedId);
            _choicesContainer.AddChild(button);
        }

        PopupCentered();
    }

    private void OnCloseRequested()
    {
        EmitSignal(SignalName.PuzzleRiddleClosed);
    }

    public override void _ExitTree()
    {
        CloseRequested -= OnCloseRequested;
        Canceled -= OnCloseRequested;
    }
}
