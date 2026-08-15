using Godot;
using System;

public enum SiriusPromptVariant
{
    DestructiveConfirmation,
    Warning,
    RecoverableError
}

public partial class SiriusPrompt : Control
{
    [Signal] public delegate void PrimaryRequestedEventHandler();
    [Signal] public delegate void CancelRequestedEventHandler();

    private SiriusPromptVariant _variant = SiriusPromptVariant.Warning;
    private string _title = "Notice";
    private string _message = string.Empty;
    private string _primaryActionText = "OK";
    private string _cancelActionText = "Cancel";
    private bool _terminalEmitted;

    private SiriusModalShell _shell = null!;
    private Label _messageLabel = null!;
    private Button _primary = null!;
    private Button _cancel = null!;

    public Control InitialFocusTarget =>
        _variant == SiriusPromptVariant.DestructiveConfirmation ? _cancel : _primary;

    public void Configure(
        SiriusPromptVariant variant,
        string title,
        string message,
        string primaryActionText,
        string cancelActionText = "Cancel")
    {
        _variant = variant;
        _title = title ?? string.Empty;
        _message = message ?? string.Empty;
        _primaryActionText = primaryActionText ?? string.Empty;
        _cancelActionText = cancelActionText ?? string.Empty;
        if (IsNodeReady())
            RefreshPresentation();
    }

    public void RequestCancel()
    {
        if (_variant == SiriusPromptVariant.DestructiveConfirmation)
            EmitCancelOnce();
        else
            EmitPrimaryOnce();
    }

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _messageLabel = GetNode<Label>("%Message");
        _primary = GetNode<Button>("%PrimaryButton");
        _cancel = GetNode<Button>("%CancelButton");

        _primary.Pressed += EmitPrimaryOnce;
        _cancel.Pressed += EmitCancelOnce;
        Resized += OnResized;

        RefreshPresentation();
    }

    public override void _ExitTree()
    {
        if (_primary != null)
            _primary.Pressed -= EmitPrimaryOnce;
        if (_cancel != null)
            _cancel.Pressed -= EmitCancelOnce;
        Resized -= OnResized;
    }

    private void OnResized() => RefreshPresentation();

    private void RefreshPresentation()
    {
        var size = GetViewportRect().Size;
        var compact = SiriusUiMetrics.IsCompact(size);

        _shell.Title = _title;
        _shell.Severity = SeverityFor(_variant);
        _shell.Compact = compact;

        _messageLabel.Text = _message;
        _primary.Text = _primaryActionText;
        _primary.ThemeTypeVariation = PrimaryThemeFor(_variant);
        _cancel.Text = _cancelActionText;
        _cancel.Visible = _variant == SiriusPromptVariant.DestructiveConfirmation;

        var target = SiriusUiMetrics.MinimumTarget(compact);
        _primary.CustomMinimumSize = new Vector2(0, target.Y);
        _cancel.CustomMinimumSize = new Vector2(0, target.Y);

        _shell.RefreshPresentation(size);
    }

    private static SiriusUiSeverity SeverityFor(SiriusPromptVariant variant) => variant switch
    {
        SiriusPromptVariant.DestructiveConfirmation => SiriusUiSeverity.Warning,
        SiriusPromptVariant.Warning => SiriusUiSeverity.Warning,
        SiriusPromptVariant.RecoverableError => SiriusUiSeverity.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
    };

    private static StringName PrimaryThemeFor(SiriusPromptVariant variant) =>
        variant == SiriusPromptVariant.DestructiveConfirmation
            ? SiriusThemeTypes.DestructiveButton
            : SiriusThemeTypes.PrimaryButton;

    private void EmitPrimaryOnce()
    {
        if (_terminalEmitted) return;
        _terminalEmitted = true;
        EmitSignal(SignalName.PrimaryRequested);
    }

    private void EmitCancelOnce()
    {
        if (_terminalEmitted) return;
        _terminalEmitted = true;
        EmitSignal(SignalName.CancelRequested);
    }
}
