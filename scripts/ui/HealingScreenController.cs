using Godot;

/// <summary>
/// Hosted Sirius healing screen (replaces the native HealDialog window).
/// Charges the configured <see cref="NpcData.HealCost"/> and restores the
/// player to full HP. Configuration is one-shot via <see cref="TryOpenHeal"/>
/// and may happen before _Ready(); the host frees the screen after
/// <see cref="HealComplete"/> or <see cref="HealCancelled"/>. Availability
/// feedback is a standing label — Healing has no transient feedback timer.
/// </summary>
public partial class HealingScreenController : Control
{
    [Signal] public delegate void HealCompleteEventHandler();
    [Signal] public delegate void HealCancelledEventHandler();

    private const string FullHealthFeedback = "You are already at full health.";
    private const string NotEnoughGoldFeedback = "Not enough gold!";

    public Control? InitialFocusTarget { get; private set; }

    private SiriusModalShell _shell = null!;
    private Label _healthLabel = null!;
    private Label _costLabel = null!;
    private Label _goldLabel = null!;
    private Label _feedbackLabel = null!;
    private Button _healButton = null!;
    private Button _cancelButton = null!;

    private NpcData? _npc;
    private Character? _player;
    private bool _started;
    private bool _terminalEmitted;

    /// <summary>Opens healing for the given NPC and player. One-shot.</summary>
    public bool TryOpenHeal(NpcData npc, Character player)
    {
        if (_started)
            return false;

        _started = true;
        if (npc.HealCost <= 0)
            GD.PushWarning($"[HealingScreen] NPC '{npc.NpcId}' has HealCost={npc.HealCost}. This allows free healing — check NpcCatalog.");
        _npc = npc;
        _player = player;

        if (IsNodeReady())
            Render();
        return true;
    }

    public void RequestCancel()
    {
        if (_terminalEmitted)
            return;

        _terminalEmitted = true;
        EmitSignal(SignalName.HealCancelled);
    }

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _healthLabel = GetNode<Label>("%HealthLabel");
        _costLabel = GetNode<Label>("%CostLabel");
        _goldLabel = GetNode<Label>("%GoldLabel");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _healButton = GetNode<Button>("%HealButton");
        _cancelButton = GetNode<Button>("%CancelButton");

        _healButton.Pressed += OnHealPressed;
        _cancelButton.Pressed += OnCancelPressed;
        Resized += OnResized;

        if (_npc != null)
            Render();
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_healButton != null)
            _healButton.Pressed -= OnHealPressed;
        if (_cancelButton != null)
            _cancelButton.Pressed -= OnCancelPressed;
        Resized -= OnResized;
    }

    // Mirrors HealDialog's mutation order 1:1 (full-HP recheck, affordability
    // recheck, restore, stat notification, terminal emit). The terminal latch
    // is the one-shot duplicate guard — there is no operation-in-flight flag.
    private void OnHealPressed()
    {
        // Unconfigured activation (TryOpenHeal never ran) must not consume
        // the terminal latch — bail before any mutation.
        if (_player == null || _npc == null)
            return;

        if (_terminalEmitted)
            return;

        int maxHp = _player!.GetEffectiveMaxHealth();
        if (_player.CurrentHealth >= maxHp)
        {
            ShowStandingFeedback(FullHealthFeedback);
            return;
        }

        if (!_player.TrySpendGold(_npc!.HealCost))
        {
            ShowStandingFeedback(NotEnoughGoldFeedback);
            return;
        }

        _player.CurrentHealth = maxHp;
        GameManager.Instance?.NotifyPlayerStatsChanged();
        _terminalEmitted = true;
        EmitSignal(SignalName.HealComplete);
    }

    private void OnCancelPressed() => RequestCancel();

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        if (!IsNodeReady() || _shell == null || !IsInsideTree())
            return;

        var size = GetViewportRect().Size;
        _shell.Compact = SiriusUiMetrics.IsCompact(size);
        _shell.RefreshPresentation(size);
    }

    private void Render()
    {
        _shell.Title = _npc!.DisplayName;

        int maxHp = _player!.GetEffectiveMaxHealth();
        bool atFullHp = _player.CurrentHealth >= maxHp;
        bool canAfford = _player.Gold >= _npc.HealCost;

        _healthLabel.Text = $"Current HP: {_player.CurrentHealth}/{maxHp}";
        _costLabel.Text = $"Restore all HP for {_npc.HealCost} gold?";
        _goldLabel.Text = $"Your Gold: {_player.Gold}";

        if (atFullHp)
            ShowStandingFeedback(FullHealthFeedback);
        else if (!canAfford)
            ShowStandingFeedback(NotEnoughGoldFeedback);
        else
            ClearFeedback();

        _healButton.Disabled = atFullHp || !canAfford;

        RefreshLayout();
        ResolveFocus();
    }

    private void ShowStandingFeedback(string message)
    {
        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;
    }

    private void ClearFeedback()
    {
        _feedbackLabel.Text = string.Empty;
        _feedbackLabel.Visible = false;
    }

    private void ResolveFocus()
    {
        // Heal when available; otherwise No Thanks. Both buttons are static
        // scene nodes, so the Inventory-style focusability check suffices.
        Control target = CanGrabFocus(_healButton) ? _healButton : _cancelButton;
        InitialFocusTarget = target;
        target.GrabFocus();
    }

    private static bool CanGrabFocus(Control? target) =>
        target != null && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree() &&
        target.FocusMode != Control.FocusModeEnum.None &&
        (target is not BaseButton button || !button.Disabled);
}
