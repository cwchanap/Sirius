using Godot;

/// <summary>
/// Modal dialog for the Healer NPC service.
/// Charges gold and restores the player to full HP.
/// </summary>
public partial class HealDialog : AcceptDialog
{
    [Signal] public delegate void HealCompleteEventHandler();
    [Signal] public delegate void HealCancelledEventHandler();

    private NpcData _npc;
    private Character _player;
    private Label _bodyLabel;
    private Label _feedbackLabel;
    private Button _healBtn;
    private Button _cancelBtn;

    public override void _Ready()
    {
        Size = new Vector2I(360, 200);
        Exclusive = true;
        GetOkButton().Visible = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        _bodyLabel = new Label();
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_bodyLabel);

        _feedbackLabel = new Label();
        _feedbackLabel.Modulate = Colors.Yellow;
        _feedbackLabel.Visible = false;
        root.AddChild(_feedbackLabel);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        root.AddChild(buttons);

        _healBtn = new Button();
        _healBtn.Text = "Heal";
        _healBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _healBtn.Pressed += OnHealPressed;
        buttons.AddChild(_healBtn);

        _cancelBtn = new Button();
        _cancelBtn.Text = "No thanks";
        _cancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _cancelBtn.Pressed += OnCancelPressed;
        buttons.AddChild(_cancelBtn);

        CloseRequested += OnCancelPressed;
        Canceled += OnCancelPressed;
    }

    /// <summary>Opens the heal dialog for the given NPC and player.</summary>
    public void OpenHeal(NpcData npc, Character player)
    {
        Title = npc.DisplayName;
        _npc = npc;
        _player = player;
        RefreshBody();
    }

    private void RefreshBody()
    {
        int maxHp = _player.GetEffectiveMaxHealth();
        bool atFullHp = _player.CurrentHealth >= maxHp;

        _bodyLabel.Text = atFullHp
            ? $"You are already at full health ({_player.CurrentHealth}/{maxHp} HP).\nNo healing needed."
            : $"Restore all HP for {_npc.HealCost} gold?\nCurrent HP: {_player.CurrentHealth}/{maxHp}\nYour gold: {_player.Gold}";

        _healBtn.Disabled = atFullHp || _player.Gold < _npc.HealCost;
    }

    private void OnHealPressed()
    {
        int maxHp = _player.GetEffectiveMaxHealth();
        if (_player.CurrentHealth >= maxHp)
        {
            _feedbackLabel.Text = "You are already at full health.";
            _feedbackLabel.Visible = true;
            return;
        }

        if (!_player.TrySpendGold(_npc.HealCost))
        {
            _feedbackLabel.Text = "Not enough gold!";
            _feedbackLabel.Visible = true;
            return;
        }

        _player.CurrentHealth = maxHp;
        GameManager.Instance?.NotifyPlayerStatsChanged();
        EmitSignal(SignalName.HealComplete);
    }

    private void OnCancelPressed()
    {
        EmitSignal(SignalName.HealCancelled);
    }

    public override void _ExitTree()
    {
        if (_healBtn != null)
            _healBtn.Pressed -= OnHealPressed;
        if (_cancelBtn != null)
            _cancelBtn.Pressed -= OnCancelPressed;
        CloseRequested -= OnCancelPressed;
        Canceled -= OnCancelPressed;
    }
}
