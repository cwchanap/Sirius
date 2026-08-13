using Godot;

public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Unsupported
}

public partial class SiriusItemSlotController : Button
{
    [Signal] public delegate void ActivatedEventHandler();

    private SiriusItemSlotVisualState _state;
    private TextureRect _icon = null!;
    private Label _quantityLabel = null!;
    private Label _stateLabel = null!;

    public bool Actionable =>
        _state is SiriusItemSlotVisualState.Available
            or SiriusItemSlotVisualState.Equipped;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("%Icon");
        _quantityLabel = GetNode<Label>("%QuantityLabel");
        _stateLabel = GetNode<Label>("%StateLabel");
        FocusMode = FocusModeEnum.All;
        Pressed += OnPressed;
    }

    public void SetCompact(bool compact) =>
        CustomMinimumSize = SiriusUiMetrics.ItemSlotSize(compact);

    public void PresentGlyph(
        UiIconId iconId,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state)
    {
        UiIconPresenter.ApplyGlyph(_icon, iconId, UiIconSize.Feature);
        PresentCore(quantityText, stateText, tooltipText, state);
    }

    public void PresentItem(
        Texture2D? texture,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state)
    {
        UiIconPresenter.ApplyItem(_icon, texture);
        PresentCore(quantityText, stateText, tooltipText, state);
    }

    private void PresentCore(
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state)
    {
        _state = state;
        TooltipText = tooltipText ?? string.Empty;
        _quantityLabel.Text = quantityText ?? string.Empty;
        _quantityLabel.Visible = !string.IsNullOrWhiteSpace(_quantityLabel.Text);
        _stateLabel.Text = stateText ?? string.Empty;
        _stateLabel.Visible = !string.IsNullOrWhiteSpace(_stateLabel.Text);
        ThemeTypeVariation = state switch
        {
            SiriusItemSlotVisualState.Equipped => SiriusThemeTypes.ItemSlotEquippedButton,
            SiriusItemSlotVisualState.Empty or SiriusItemSlotVisualState.Unsupported
                => SiriusThemeTypes.ItemSlotUnavailableButton,
            _ => SiriusThemeTypes.ItemSlotButton
        };
    }

    private void OnPressed()
    {
        if (Actionable)
            EmitSignal(SignalName.Activated);
    }
}
