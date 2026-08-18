using Godot;
using System.Linq;

/// <summary>
/// Hosted Sirius riddle screen (replaces the native PuzzleRiddleDialog
/// window). Presentation-only: renders one <see cref="PuzzleRiddleSpawn"/>'s
/// prompt and answers, tracks the local AwaitingChoice → Resolving →
/// Terminal phase, and emits presentation events. It never validates
/// answers or touches puzzle domain state — Game owns resolution.
/// Configuration is one-shot via <see cref="TryOpenRiddle"/> and may happen
/// before _Ready(); Game rearms after dormant results and delivers terminal
/// feedback via <see cref="RearmWithFeedback"/> /
/// <see cref="ShowTerminalFeedback"/>.
/// </summary>
public partial class PuzzleRiddleScreenController : Control
{
    [Signal] public delegate void ChoiceSelectedEventHandler(string choiceId);
    [Signal] public delegate void PuzzleRiddleClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    private enum PuzzleRiddlePresentationPhase
    {
        AwaitingChoice,
        Resolving,
        Terminal
    }

    private PuzzleRiddlePresentationPhase _phase = PuzzleRiddlePresentationPhase.AwaitingChoice;
    private bool _closedEmitted;
    private bool _started;

    private SiriusModalShell _shell = null!;
    private SiriusInputHint _cancelHint = null!;
    private Label _feedbackLabel = null!;
    private RichTextLabel _promptLabel = null!;
    private VBoxContainer _choicesContainer = null!;
    private Button _cancelButton = null!;

    private PuzzleRiddleSpawn? _riddle;

    /// <summary>
    /// Opens the given riddle. One-shot; rejects null or zero-choice input
    /// so the surface can never render stuck without an answer.
    /// </summary>
    public bool TryOpenRiddle(PuzzleRiddleSpawn riddle)
    {
        if (_started)
            return false;

        if (riddle == null || riddle.GetChoices().Count == 0)
        {
            GD.PushError("[PuzzleRiddleScreen] Refusing to open a riddle with no answer choices.");
            return false;
        }

        _started = true;
        _riddle = riddle;

        if (IsNodeReady())
            Render();
        return true;
    }

    /// <summary>
    /// Dormant-result rearm: standing feedback, the same choices re-enabled,
    /// and a restored choice focus target. The world latch stays with Game.
    /// </summary>
    public void RearmWithFeedback(string message)
    {
        _phase = PuzzleRiddlePresentationPhase.AwaitingChoice;
        SetFeedback(message);
        SetChoicesVisible(true);
        SetChoicesEnabled(true);
        _cancelButton.Text = "Cancel";
        RestoreChoiceFocus();
    }

    /// <summary>
    /// Terminal result presentation: choices retire, the cancel button
    /// becomes the labelled final action, and it takes focus.
    /// </summary>
    public void ShowTerminalFeedback(string message, string actionLabel)
    {
        _phase = PuzzleRiddlePresentationPhase.Terminal;
        SetFeedback(message);
        SetChoicesEnabled(false);
        SetChoicesVisible(false);
        _cancelButton.Text = string.IsNullOrWhiteSpace(actionLabel) ? "Close" : actionLabel;
        InitialFocusTarget = _cancelButton;
        _cancelButton.GrabFocus();
    }

    /// <summary>
    /// Cancel / final-action close. Ignored while Resolving so a
    /// synchronously resolving answer can never race a close emission.
    /// </summary>
    public void RequestCancel()
    {
        if (_phase == PuzzleRiddlePresentationPhase.Resolving)
            return;

        EmitClosedOnce();
    }

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _cancelHint = GetNode<SiriusInputHint>("%CancelHint");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _promptLabel = GetNode<RichTextLabel>("%PromptLabel");
        _choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");
        _cancelButton = GetNode<Button>("%CancelButton");

        _cancelButton.Pressed += OnCancelPressed;
        Resized += OnResized;

        if (_riddle != null)
            Render();
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
        Resized -= OnResized;
    }

    private void OnCancelPressed() => RequestCancel();

    private void OnResized() => RefreshLayout();

    private void Render()
    {
        _shell.Title = string.IsNullOrWhiteSpace(_riddle!.RiddleId) ? "Seal" : _riddle.RiddleId;
        _promptLabel.Text = _riddle.PromptText ?? string.Empty;

        foreach (Node child in _choicesContainer.GetChildren())
        {
            _choicesContainer.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var choice in _riddle.GetChoices())
        {
            var button = CreateActionButton(choice.Label);
            var captured = choice;
            button.Pressed += () => OnChoicePressed(captured.Id);
            _choicesContainer.AddChild(button);
        }

        RefreshLayout();
        RestoreChoiceFocus();
    }

    private void RefreshLayout()
    {
        if (!IsNodeReady() || _shell == null || !IsInsideTree())
            return;

        var size = GetViewportRect().Size;
        var compact = SiriusUiMetrics.IsCompact(size);
        _shell.Compact = compact;
        _cancelHint.Compact = compact;
        _promptLabel.ThemeTypeVariation = compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;
        _feedbackLabel.ThemeTypeVariation = compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;

        var target = SiriusUiMetrics.MinimumTarget(compact);
        foreach (Node child in _choicesContainer.GetChildren())
            if (child is Button button)
                button.CustomMinimumSize = new Vector2(0f, target.Y);

        _shell.RefreshPresentation(size);
    }

    private void OnChoicePressed(string choiceId)
    {
        if (_phase != PuzzleRiddlePresentationPhase.AwaitingChoice || _closedEmitted)
            return;

        _phase = PuzzleRiddlePresentationPhase.Resolving;
        SetChoicesEnabled(false);
        EmitSignal(SignalName.ChoiceSelected, choiceId);
    }

    private void RestoreChoiceFocus()
    {
        // First focusable answer; Cancel is the fallback when every choice
        // is somehow unfocusable.
        var target = _choicesContainer.GetChildren()
            .OfType<Button>()
            .FirstOrDefault(CanGrabFocus);
        InitialFocusTarget = target ?? _cancelButton;
        Callable.From(InitialFocusTarget.GrabFocus).CallDeferred();
    }

    private void SetFeedback(string message)
    {
        _feedbackLabel.Text = message ?? string.Empty;
        _feedbackLabel.Visible = !string.IsNullOrWhiteSpace(message);
    }

    private void SetChoicesEnabled(bool enabled)
    {
        foreach (Node child in _choicesContainer.GetChildren())
            if (child is Button button)
                button.Disabled = !enabled;
    }

    private void SetChoicesVisible(bool visible)
    {
        foreach (Node child in _choicesContainer.GetChildren())
            if (child is Button button)
                button.Visible = visible;
    }

    private void EmitClosedOnce()
    {
        if (_closedEmitted)
            return;

        _closedEmitted = true;
        EmitSignal(SignalName.PuzzleRiddleClosed);
    }

    private static bool CanGrabFocus(Control? target) =>
        target != null && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree() &&
        target.FocusMode != Control.FocusModeEnum.None &&
        (target is not BaseButton button || !button.Disabled);

    private static Button CreateActionButton(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
        SizeFlagsHorizontal = SizeFlags.ExpandFill
    };
}
