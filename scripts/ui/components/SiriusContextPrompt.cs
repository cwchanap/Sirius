using Godot;
using System;

public partial class SiriusContextPrompt : HBoxContainer
{
    private bool _showIcon;
    private UiIconId _iconId = UiIconId.Info;
    private string _prompt = string.Empty;
    private StringName[] _actions = Array.Empty<StringName>();
    private bool _compact;

    private TextureRect _semanticIcon = null!;
    private Label _promptLabel = null!;
    private SiriusInputHint _inputHint = null!;

    [Export]
    public bool ShowIcon
    {
        get => _showIcon;
        set
        {
            _showIcon = value;
            RefreshIfReady();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            RefreshIfReady();
        }
    }

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

    public override void _Ready()
    {
        _semanticIcon = GetNode<TextureRect>("%SemanticIcon");
        _promptLabel = GetNode<Label>("%PromptLabel");
        _inputHint = GetNode<SiriusInputHint>("%InputHint");
        Refresh();
    }

    public void Refresh()
    {
        _semanticIcon.Visible = ShowIcon;
        if (ShowIcon)
            UiIconPresenter.Apply(_semanticIcon, IconId, UiIconSize.Default);
        else
            _semanticIcon.Texture = null;

        _promptLabel.Text = Prompt;
        _promptLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;

        _inputHint.Actions = Actions;
        _inputHint.Compact = Compact;
        _inputHint.Refresh();
    }

    private void RefreshIfReady()
    {
        if (IsNodeReady())
            Refresh();
    }
}
