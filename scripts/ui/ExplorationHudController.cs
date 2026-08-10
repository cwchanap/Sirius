using Godot;
using System;

public readonly record struct ExplorationHudPlayerState(
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth,
    int CurrentMana,
    int MaxMana,
    int Experience,
    int ExperienceToNext);

public partial class ExplorationHudController : Control
{
    private static readonly StringName InteractAction = new("interact");
    private const double AreaTitleSeconds = 2.0;
    private const double SessionHintSeconds = 4.0;

    private Control _safeFrame = null!;
    private TextureRect _portrait = null!;
    private Label _playerName = null!;
    private Label _playerLevel = null!;
    private SiriusStatBar _healthBar = null!;
    private SiriusStatBar _manaBar = null!;
    private ProgressBar _experienceBar = null!;
    private PanelContainer _promptPlate = null!;
    private SiriusContextPrompt _contextPrompt = null!;
    private PanelContainer _transientPlate = null!;
    private Label _transientLabel = null!;
    private Timer _transientTimer = null!;

    private bool _compact;
    private bool _showingAreaTitle;
    private string? _pendingSessionHint;

    public override void _Ready()
    {
        BindNodes();
        MakePassive(this);
        _contextPrompt.Actions = new[] { InteractAction };
        _transientTimer.Timeout += OnTransientTimeout;
        GetViewport().SizeChanged += RefreshLayout;
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_transientTimer != null)
            _transientTimer.Timeout -= OnTransientTimeout;

        var viewport = GetViewport();
        if (viewport != null)
            viewport.SizeChanged -= RefreshLayout;
    }

    public void ApplyPlayerState(ExplorationHudPlayerState state)
    {
        _playerName.Text = string.IsNullOrWhiteSpace(state.Name)
            ? "Adventurer"
            : state.Name;
        _playerLevel.Text = $"Lv {state.Level}";

        _healthBar.Current = state.CurrentHealth;
        _healthBar.Maximum = state.MaxHealth;

        _manaBar.Visible = state.MaxMana > 0;
        if (_manaBar.Visible)
        {
            _manaBar.Current = state.CurrentMana;
            _manaBar.Maximum = state.MaxMana;
        }

        _experienceBar.Visible = state.ExperienceToNext > 0;
        if (_experienceBar.Visible)
        {
            _experienceBar.MaxValue = state.ExperienceToNext;
            _experienceBar.Value = Math.Clamp(
                state.Experience,
                0,
                state.ExperienceToNext);
        }

        _portrait.Visible = _portrait.Texture != null;
    }

    public void ShowInteractionPrompt(string text, UiIconId icon)
    {
        if (_contextPrompt.Prompt != text)
            _contextPrompt.Prompt = text;
        if (!_contextPrompt.ShowIcon)
            _contextPrompt.ShowIcon = true;
        if (_contextPrompt.IconId != icon)
            _contextPrompt.IconId = icon;

        // Required even when text/icon are unchanged: Settings may have remapped
        // the same interact action while this target remained valid.
        _contextPrompt.Refresh();
        _promptPlate.Visible = true;
    }

    public void HideInteractionPrompt() => _promptPlate.Visible = false;

    public void ShowAreaTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        if (_transientPlate.Visible && !_showingAreaTitle)
            _pendingSessionHint = _transientLabel.Text;

        _showingAreaTitle = true;
        ShowTransient(
            title,
            _compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title,
            AreaTitleSeconds);
    }

    public void ShowSessionHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_transientPlate.Visible && _showingAreaTitle)
        {
            _pendingSessionHint = text;
            return;
        }

        _showingAreaTitle = false;
        ShowTransient(
            text,
            _compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata,
            SessionHintSeconds);
    }

    private void BindNodes()
    {
        _safeFrame = GetNode<Control>("%SafeFrame");
        _portrait = GetNode<TextureRect>("%Portrait");
        _playerName = GetNode<Label>("%PlayerName");
        _playerLevel = GetNode<Label>("%PlayerLevel");
        _healthBar = GetNode<SiriusStatBar>("%HealthBar");
        _manaBar = GetNode<SiriusStatBar>("%ManaBar");
        _experienceBar = GetNode<ProgressBar>("%ExperienceBar");
        _promptPlate = GetNode<PanelContainer>("%PromptPlate");
        _contextPrompt = GetNode<SiriusContextPrompt>("%ContextPrompt");
        _transientPlate = GetNode<PanelContainer>("%TransientPlate");
        _transientLabel = GetNode<Label>("%TransientLabel");
        _transientTimer = GetNode<Timer>("%TransientTimer");
    }

    private void ShowTransient(string text, StringName variation, double seconds)
    {
        _transientLabel.Text = text;
        _transientLabel.ThemeTypeVariation = variation;
        _transientPlate.Visible = true;
        _transientTimer.WaitTime = seconds;
        _transientTimer.Start();
    }

    private void OnTransientTimeout()
    {
        if (_showingAreaTitle && !string.IsNullOrWhiteSpace(_pendingSessionHint))
        {
            var hint = _pendingSessionHint;
            _pendingSessionHint = null;
            _showingAreaTitle = false;
            ShowTransient(
                hint!,
                _compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata,
                SessionHintSeconds);
            return;
        }

        _showingAreaTitle = false;
        _pendingSessionHint = null;
        _transientPlate.Visible = false;
    }

    private static void MakePassive(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = Control.MouseFilterEnum.Ignore;
            control.FocusMode = Control.FocusModeEnum.None;
        }

        foreach (var child in node.GetChildren())
            MakePassive(child);
    }

    private void RefreshLayout()
    {
        var viewportSize = GetViewportRect().Size;
        _compact = SiriusUiMetrics.IsCompact(viewportSize);
        var margin = SiriusUiMetrics.SafeMargin(_compact);
        var availableWidth = MathF.Max(0, viewportSize.X - margin * 2f);
        var contentWidth = MathF.Min(
            availableWidth,
            SiriusUiMetrics.MaximumContentWidth);
        var sideInset = MathF.Max(
            margin,
            (viewportSize.X - contentWidth) / 2f);

        _safeFrame.OffsetLeft = sideInset;
        _safeFrame.OffsetRight = -sideInset;
        _safeFrame.OffsetTop = margin;
        _safeFrame.OffsetBottom = -margin;

        _portrait.CustomMinimumSize = _compact
            ? new Vector2(40, 40)
            : new Vector2(56, 56);
        _healthBar.Compact = _compact;
        _manaBar.Compact = _compact;
        _contextPrompt.Compact = _compact;
        _playerName.ThemeTypeVariation = _compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;
        _playerLevel.ThemeTypeVariation = _compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;

        if (_transientPlate.Visible)
        {
            _transientLabel.ThemeTypeVariation = _showingAreaTitle
                ? (_compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title)
                : (_compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata);
        }
    }
}
