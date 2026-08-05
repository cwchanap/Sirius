using Godot;

public partial class SiriusToastShell : Control
{
    private SiriusUiSeverity _severity = SiriusUiSeverity.Info;
    private string _title = string.Empty;
    private string _message = string.Empty;
    private bool _compact;

    private PanelContainer _panel = null!;
    private TextureRect _severityIcon = null!;
    private Label _titleLabel = null!;
    private Label _messageLabel = null!;

    [Export]
    public SiriusUiSeverity Severity
    {
        get => _severity;
        set
        {
            _severity = value;
            RefreshIfReady();
        }
    }

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            RefreshIfReady();
        }
    }

    [Export]
    public string Message
    {
        get => _message;
        set
        {
            _message = value ?? string.Empty;
            RefreshIfReady();
        }
    }

    [Export]
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            RefreshIfReady();
        }
    }

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%Panel");
        _severityIcon = GetNode<TextureRect>("%SeverityIcon");
        _titleLabel = GetNode<Label>("%TitleLabel");
        _messageLabel = GetNode<Label>("%MessageLabel");
        RefreshPresentation();
    }

    public override Vector2 _GetMinimumSize()
    {
        // The scene root is a bare Control whose default internal minimum is
        // zero. Propagate the nested PanelContainer's combined minimum so a
        // parent container (e.g. a toast queue VBoxContainer) allocates each
        // shell a positive, non-overlapping rect without a hard-coded override.
        return IsNodeReady() && GodotObject.IsInstanceValid(_panel)
            ? _panel.GetCombinedMinimumSize()
            : base._GetMinimumSize();
    }

    public void RefreshPresentation()
    {
        _panel.ThemeTypeVariation = Severity.ToToastPanelThemeType();
        _titleLabel.Text = Title;
        _messageLabel.Text = Message;
        _titleLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.SectionCompact
            : SiriusThemeTypes.Section;
        _messageLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;
        UiIconPresenter.Apply(_severityIcon, Severity.ToIconId(), UiIconSize.Default);
    }

    private void RefreshIfReady()
    {
        if (IsNodeReady())
            RefreshPresentation();
    }
}
