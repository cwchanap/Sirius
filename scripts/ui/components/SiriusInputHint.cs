using Godot;
using System;

public partial class SiriusInputHint : HBoxContainer
{
    private readonly InputHintPresenter _presenter = new();

    private string _prompt = string.Empty;
    private StringName[] _actions = Array.Empty<StringName>();
    private bool _compact;

    private TextureRect _deviceIcon = null!;
    private Label _promptLabel = null!;
    private Label _bindingLabel = null!;

    [Export]
    public string Prompt
    {
        get => _prompt;
        set
        {
            _prompt = value ?? string.Empty;
            RefreshIfReady();
        }
    }

    [Export]
    public StringName[] Actions
    {
        get => _actions;
        set
        {
            _actions = value ?? Array.Empty<StringName>();
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

    public UiInputDevice ActiveDevice => _presenter.ActiveDevice;

    public override void _Ready()
    {
        _deviceIcon = GetNode<TextureRect>("%DeviceIcon");
        _promptLabel = GetNode<Label>("%PromptLabel");
        _bindingLabel = GetNode<Label>("%BindingLabel");

        VisibilityChanged += OnVisibilityChanged;
        UpdateInputProcessing();
        Refresh();
    }

    public override void _ExitTree()
    {
        VisibilityChanged -= OnVisibilityChanged;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!IsVisibleInTree())
            return;

        Observe(inputEvent);
    }

    public bool Observe(InputEvent inputEvent)
    {
        var changed = _presenter.Observe(inputEvent);
        if (changed)
            RefreshIfReady();
        return changed;
    }

    public void Refresh()
    {
        var hint = _presenter.ResolveActions(Actions);
        UiIconPresenter.Apply(_deviceIcon, hint.IconId, UiIconSize.Metadata);

        _promptLabel.Text = Prompt;
        _promptLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;
        _bindingLabel.Text = hint.BindingLabel;
        _bindingLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;
    }

    private void OnVisibilityChanged()
    {
        UpdateInputProcessing();
        if (IsVisibleInTree())
            Refresh();
    }

    private void UpdateInputProcessing() => SetProcessInput(IsVisibleInTree());

    private void RefreshIfReady()
    {
        if (IsNodeReady())
            Refresh();
    }
}
