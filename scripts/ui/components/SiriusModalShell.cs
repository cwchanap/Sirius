using Godot;

public partial class SiriusModalShell : Control
{
    private string _title = string.Empty;
    private SiriusUiSeverity _severity = SiriusUiSeverity.Info;
    private SiriusModalSizeClass _sizeClass = SiriusModalSizeClass.Medium;
    private bool _compact;

    private PanelContainer _panel = null!;
    private TextureRect _severityIcon = null!;
    private Label _titleLabel = null!;

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
    public SiriusModalSizeClass SizeClass
    {
        get => _sizeClass;
        set
        {
            _sizeClass = value;
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

    public VBoxContainer BodyHost { get; private set; } = null!;
    public HBoxContainer ActionsHost { get; private set; } = null!;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("%Panel");
        _severityIcon = GetNode<TextureRect>("%SeverityIcon");
        _titleLabel = GetNode<Label>("%TitleLabel");
        BodyHost = GetNode<VBoxContainer>("%BodyHost");
        ActionsHost = GetNode<HBoxContainer>("%ActionsHost");
        RefreshPresentation(GetViewportRect().Size);
    }

    public void RefreshPresentation(Vector2 availableSize)
    {
        _panel.ThemeTypeVariation = Severity.ToModalPanelThemeType();
        _titleLabel.Text = Title;
        _titleLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.TitleCompact
            : SiriusThemeTypes.Title;
        UiIconPresenter.Apply(_severityIcon, Severity.ToIconId(), UiIconSize.Default);

        var width = Compact
            ? availableSize.X - SiriusUiMetrics.SafeMargin(true) * 2
            : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X * 0.90f);
        _panel.CustomMinimumSize = new Vector2(Mathf.Max(0, width), 0);
    }

    private void RefreshIfReady()
    {
        if (IsNodeReady())
            RefreshPresentation(GetViewportRect().Size);
    }
}
