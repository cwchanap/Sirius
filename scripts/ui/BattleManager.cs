using Godot;

public partial class BattleManager : Control
{
    [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, bool playerEscaped);
    
    private Character _player;
    private Enemy _enemy;
    private bool _playerTurn = true;
    
    // UI References
    private Label _playerHealthLabel;
    private Label _enemyHealthLabel;
    private Label _battleLogLabel;
    private Button _attackButton;
    private Button _defendButton;
    private Button _runButton;
    
    private string _battleLog = "";
    
    public override void _Ready()
    {
        // This will be called when the battle scene is instantiated
        SetupUI();
    }
    
    private void SetupUI()
    {
        // Create UI elements
        var vbox = new VBoxContainer();
        AddChild(vbox);
        
        // Set full rect manually
        vbox.AnchorLeft = 0;
        vbox.AnchorTop = 0;
        vbox.AnchorRight = 1;
        vbox.AnchorBottom = 1;
        vbox.OffsetLeft = 0;
        vbox.OffsetTop = 0;
        vbox.OffsetRight = 0;
        vbox.OffsetBottom = 0;
        
        // Enemy info
        var enemyInfo = new HBoxContainer();
        vbox.AddChild(enemyInfo);
        
        var enemyLabel = new Label();
        enemyLabel.Text = "Enemy: ";
        enemyInfo.AddChild(enemyLabel);
        
        _enemyHealthLabel = new Label();
        enemyInfo.AddChild(_enemyHealthLabel);
        
        // Player info
        var playerInfo = new HBoxContainer();
        vbox.AddChild(playerInfo);
        
        var playerLabel = new Label();
        playerLabel.Text = "Player: ";
        playerInfo.AddChild(playerLabel);
        
        _playerHealthLabel = new Label();
        playerInfo.AddChild(_playerHealthLabel);
        
        // Battle log
        _battleLogLabel = new Label();
        _battleLogLabel.VerticalAlignment = VerticalAlignment.Top;
        _battleLogLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _battleLogLabel.CustomMinimumSize = new Vector2(0, 200);
        vbox.AddChild(_battleLogLabel);
        
        // Action buttons
        var buttonContainer = new HBoxContainer();
        vbox.AddChild(buttonContainer);
        
        _attackButton = new Button();
        _attackButton.Text = "Attack";
        _attackButton.Pressed += OnAttackPressed;
        buttonContainer.AddChild(_attackButton);
        
        _defendButton = new Button();
        _defendButton.Text = "Defend";
        _defendButton.Pressed += OnDefendPressed;
        buttonContainer.AddChild(_defendButton);
        
        _runButton = new Button();
        _runButton.Text = "Run";
        _runButton.Pressed += OnRunPressed;
        buttonContainer.AddChild(_runButton);
    }
    
    public void StartBattle(Character player, Enemy enemy)
    {
        GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");
        
        _player = player;
        _enemy = enemy;
        _playerTurn = _player.Speed >= _enemy.Speed; // Faster character goes first
        
        AddToBattleLog($"Battle begins! {_player.Name} vs {_enemy.Name}");
        AddToBattleLog($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first!");
        
        UpdateUI();
        
        if (!_playerTurn)
        {
            // Enemy goes first
            GetTree().CreateTimer(1.0).Timeout += () => EnemyTurn();
        }
    }
    
    private void UpdateUI()
    {
        if (_playerHealthLabel != null && _player != null)
        {
            _playerHealthLabel.Text = $"{_player.Name} HP: {_player.CurrentHealth}/{_player.MaxHealth}";
        }
        
        if (_enemyHealthLabel != null && _enemy != null)
        {
            _enemyHealthLabel.Text = $"{_enemy.Name} HP: {_enemy.CurrentHealth}/{_enemy.MaxHealth}";
        }
        
        if (_battleLogLabel != null)
        {
            _battleLogLabel.Text = _battleLog;
        }
        
        // Enable/disable buttons based on turn
        if (_attackButton != null) _attackButton.Disabled = !_playerTurn;
        if (_defendButton != null) _defendButton.Disabled = !_playerTurn;
        if (_runButton != null) _runButton.Disabled = !_playerTurn;
    }
    
    private void OnAttackPressed()
    {
        if (!_playerTurn || !_player.IsAlive || !_enemy.IsAlive) return;
        
        PlayerAttack();
        _playerTurn = false;
        UpdateUI();
        
        if (_enemy.IsAlive)
        {
            GetTree().CreateTimer(1.5).Timeout += () => EnemyTurn();
        }
        else
        {
            EndBattle(true);
        }
    }
    
    private void OnDefendPressed()
    {
        if (!_playerTurn) return;
        
        AddToBattleLog($"{_player.Name} defends, reducing incoming damage!");
        _playerTurn = false;
        UpdateUI();
        
        GetTree().CreateTimer(1.0).Timeout += () => EnemyTurn(true);
    }
    
    private void OnRunPressed()
    {
        if (!_playerTurn) return;
        
        // 50% chance to successfully run
        if (GD.Randf() < 0.5f)
        {
            AddToBattleLog($"{_player.Name} successfully ran away!");
            EndBattleWithEscape();
        }
        else
        {
            AddToBattleLog($"{_player.Name} couldn't escape!");
            _playerTurn = false;
            UpdateUI();
            GetTree().CreateTimer(1.0).Timeout += () => EnemyTurn();
        }
    }
    
    private void PlayerAttack()
    {
        int damage = _player.Attack + GD.RandRange(-5, 5);
        damage = Mathf.Max(1, damage);
        
        _enemy.TakeDamage(damage);
        AddToBattleLog($"{_player.Name} attacks for {damage} damage!");
    }
    
    private void EnemyTurn(bool playerDefended = false)
    {
        if (!_enemy.IsAlive || !_player.IsAlive) return;
        
        int damage = _enemy.Attack + GD.RandRange(-3, 3);
        damage = Mathf.Max(1, damage);
        
        if (playerDefended)
        {
            damage = damage / 2;
            AddToBattleLog($"{_enemy.Name} attacks but the damage is reduced by defense!");
        }
        
        _player.TakeDamage(damage);
        AddToBattleLog($"{_enemy.Name} attacks for {damage} damage!");
        
        _playerTurn = true;
        UpdateUI();
        
        if (!_player.IsAlive)
        {
            EndBattle(false);
        }
    }
    
    private void EndBattle(bool playerWon)
    {
        GD.Print($"BattleManager.EndBattle called: playerWon = {playerWon}");
        
        if (playerWon)
        {
            AddToBattleLog($"{_player.Name} wins the battle!");
            _player.GainExperience(_enemy.ExperienceReward);
        }
        else
        {
            AddToBattleLog($"{_player.Name} was defeated...");
        }
        
        // Disable all buttons
        if (_attackButton != null) _attackButton.Disabled = true;
        if (_defendButton != null) _defendButton.Disabled = true;
        if (_runButton != null) _runButton.Disabled = true;
        
        // Wait a moment then end battle
        GetTree().CreateTimer(3.0).Timeout += () => {
            GD.Print("BattleManager emitting BattleFinished signal");
            EmitSignal(SignalName.BattleFinished, playerWon, false); // false for not escaped
        };
    }
    
    private void EndBattleWithEscape()
    {
        GD.Print("BattleManager.EndBattleWithEscape called: Player escaped");
        
        AddToBattleLog($"{_player.Name} escaped from battle!");
        
        // Disable all buttons
        if (_attackButton != null) _attackButton.Disabled = true;
        if (_defendButton != null) _defendButton.Disabled = true;
        if (_runButton != null) _runButton.Disabled = true;
        
        // Wait a moment then end battle - indicate escape
        GetTree().CreateTimer(2.0).Timeout += () => {
            GD.Print("BattleManager emitting BattleFinished signal with escape");
            EmitSignal(SignalName.BattleFinished, false, true); // false for not won, true for escaped
        };
    }
    
    private void AddToBattleLog(string message)
    {
        _battleLog += message + "\n";
        GD.Print(message);
    }
}
