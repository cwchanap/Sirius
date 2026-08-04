using Godot;
using System;
using System.Globalization;

public partial class SiriusStatBar : VBoxContainer
{
    private const double LowThreshold = 0.25;

    private SiriusStatBarKind _kind = SiriusStatBarKind.Health;
    private double _current;
    private double _maximum;
    private string _label = string.Empty;
    private bool _compact;

    private TextureRect _icon = null!;
    private Label _nameLabel = null!;
    private Label _valueLabel = null!;
    private ProgressBar _bar = null!;
    private Label _stateLabel = null!;

    [Export]
    public SiriusStatBarKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            RefreshIfReady();
        }
    }

    [Export]
    public double Current
    {
        get => _current;
        set
        {
            _current = value;
            RefreshIfReady();
        }
    }

    [Export]
    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            RefreshIfReady();
        }
    }

    [Export]
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? string.Empty;
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
        _icon = GetNode<TextureRect>("%Icon");
        _nameLabel = GetNode<Label>("%NameLabel");
        _valueLabel = GetNode<Label>("%ValueLabel");
        _bar = GetNode<ProgressBar>("%Bar");
        _stateLabel = GetNode<Label>("%StateLabel");
        RefreshPresentation();
    }

    public void RefreshPresentation()
    {
        _nameLabel.Text = Label;
        _nameLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;
        _valueLabel.Text = $"{Current.ToString(CultureInfo.InvariantCulture)} / {Maximum.ToString(CultureInfo.InvariantCulture)}";
        _valueLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.NumericCompact
            : SiriusThemeTypes.Numeric;
        _stateLabel.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.MetadataCompact
            : SiriusThemeTypes.Metadata;
        UiIconPresenter.Apply(_icon, Kind.ToIconId(), UiIconSize.Metadata);

        string state;
        if (Maximum <= 0)
        {
            _bar.MinValue = 0;
            _bar.MaxValue = 1;
            _bar.Value = 0;
            _bar.ThemeTypeVariation = SiriusThemeTypes.InvalidBar;
            state = "Invalid maximum";
        }
        else
        {
            _bar.MinValue = 0;
            _bar.MaxValue = Maximum;
            _bar.Value = Math.Clamp(Current, 0, Maximum);
            _bar.ThemeTypeVariation = Kind.ToThemeType();
            state = Current < 0
                ? "Invalid value"
                : Current > Maximum
                    ? "Overflow"
                    : Current / Maximum <= LowThreshold
                        ? "Low"
                        : "Normal";
        }

        _stateLabel.Text = state;
        _stateLabel.Visible = state != "Normal";
    }

    private void RefreshIfReady()
    {
        if (IsNodeReady())
            RefreshPresentation();
    }
}
