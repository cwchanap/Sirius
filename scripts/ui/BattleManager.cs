using Godot;

public partial class BattleManager : AcceptDialog
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
    
    // Auto-battle properties
    private Timer _battleTimer;
    private bool _battleInProgress = false;
    private bool _playerDefendedLastTurn = false;
    
    private string _battleLog = "";
    
    public override void _Ready()
    {
        // Get references to UI elements defined in the scene
        _enemyHealthLabel = GetNode<Label>("BattleContent/EnemyInfo/EnemyHealth");
        _playerHealthLabel = GetNode<Label>("BattleContent/PlayerInfo/PlayerHealth");
        _battleLogLabel = GetNode<Label>("BattleContent/BattleLog");
        _attackButton = GetNode<Button>("BattleContent/ActionButtons/AttackButton");
        _defendButton = GetNode<Button>("BattleContent/ActionButtons/DefendButton");
        _runButton = GetNode<Button>("BattleContent/ActionButtons/RunButton");
        
        // Hide manual action buttons since combat is now automated
        _attackButton.Visible = false;
        _defendButton.Visible = false;
        _runButton.Visible = false;
        
        // Create and configure battle timer for auto-combat
        _battleTimer = new Timer();
        _battleTimer.WaitTime = 0.9; // 0.9 seconds between actions for faster combat (2x speed)
        _battleTimer.Timeout += OnBattleTurnTimer;
        AddChild(_battleTimer);
        
        // Set dialog title and properties
        Title = "Battle!";
        GetOkButton().Text = "Close";
        GetOkButton().Visible = false; // Hide the OK button initially
        
        // Connect the close request signal
        CloseRequested += OnCloseRequested;
    }
    
    private void OnCloseRequested()
    {
        // In auto-battle mode, closing the dialog just stops the battle 
        // (no escape mechanics since it's automated)
        if (_battleInProgress && _player != null && _enemy != null && _player.IsAlive && _enemy.IsAlive)
        {
            AddToBattleLog("Battle interrupted!");
            _battleInProgress = false;
            _battleTimer.Stop();
            EndBattleWithEscape();
        }
    }
    
    public void StartBattle(Character player, Enemy enemy)
    {
        GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");
        
        _player = player;
        _enemy = enemy;
        _playerTurn = _player.Speed >= _enemy.Speed; // Faster character goes first
        _battleInProgress = true;
        
        AddToBattleLog($"Battle begins! {_player.Name} vs {_enemy.Name}");
        AddToBattleLog($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first!");
        AddToBattleLog("Auto-battle mode: Combat will proceed automatically!");
        
        UpdateUI();
        
        // Start the auto-battle timer
        _battleTimer.Start();
    }
    
    private void UpdateUI()
    {
        if (_playerHealthLabel != null && _player != null)
        {
            _playerHealthLabel.Text = $"{_player.Name} (Lv.{_player.Level}) HP: {_player.CurrentHealth}/{_player.MaxHealth}";
        }
        
        if (_enemyHealthLabel != null && _enemy != null)
        {
            _enemyHealthLabel.Text = $"{_enemy.Name} (Lv.{_enemy.Level}) HP: {_enemy.CurrentHealth}/{_enemy.MaxHealth}";
        }
        
        if (_battleLogLabel != null)
        {
            _battleLogLabel.Text = _battleLog;
        }
        
        // Enable/disable buttons based on turn (all disabled in auto-battle)
        if (_attackButton != null) _attackButton.Disabled = true;
        if (_defendButton != null) _defendButton.Disabled = true;
        if (_runButton != null) _runButton.Disabled = true;
    }
    
    private void OnBattleTurnTimer()
    {
        if (!_battleInProgress || !_player.IsAlive || !_enemy.IsAlive)
        {
            _battleTimer.Stop();
            return;
        }
        
        if (_playerTurn)
        {
            PlayerAutoAction();
        }
        else
        {
            EnemyTurn(_playerDefendedLastTurn);
            _playerDefendedLastTurn = false; // Reset defense flag after enemy turn
        }
        
        // Switch turns
        _playerTurn = !_playerTurn;
        UpdateUI();
        
        // Check for battle end conditions
        if (!_player.IsAlive)
        {
            _battleTimer.Stop();
            EndBattle(false);
        }
        else if (!_enemy.IsAlive)
        {
            _battleTimer.Stop();
            EndBattle(true);
        }
    }
    
    private void PlayerAutoAction()
    {
        // Player automatically chooses the best action based on situation
        float healthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;
        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        
        // More likely to defend when health is low
        if (healthPercentage < 0.4f && GD.Randf() < 0.3f)
        {
            AddToBattleLog($"{_player.Name} takes a defensive stance!");
            _playerDefendedLastTurn = true;
            return;
        }
        
        // Aggressive attack when enemy is low on health
        if (enemyHealthPercentage < 0.3f)
        {
            AddToBattleLog($"{_player.Name} goes for a finishing blow!");
        }
        
        // Otherwise, normal attack
        PlayerAttack();
    }
    
    private void PlayerAttack()
    {
        // Add some variation to attacks
        bool criticalHit = GD.Randf() < 0.15f; // 15% chance for critical hit
        int baseDamage = _player.Attack + GD.RandRange(-5, 5);
        
        if (criticalHit)
        {
            baseDamage = (int)(baseDamage * 1.5f);
            AddToBattleLog($"Critical hit! {_player.Name} deals {baseDamage} damage!");
        }
        else
        {
            AddToBattleLog($"{_player.Name} attacks for {baseDamage} damage!");
        }
        
        baseDamage = Mathf.Max(1, baseDamage);
        _enemy.TakeDamage(baseDamage);
    }
    
    private void EnemyTurn(bool playerDefended = false)
    {
        if (!_enemy.IsAlive || !_player.IsAlive) return;
        
        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;
        
        // Enemy AI: More aggressive when player is low on health
        bool aggressiveAttack = playerHealthPercentage < 0.3f && GD.Randf() < 0.4f;
        bool criticalHit = GD.Randf() < 0.1f; // 10% chance for enemy critical hit
        
        int damage = _enemy.Attack + GD.RandRange(-3, 3);
        
        if (aggressiveAttack)
        {
            damage = (int)(damage * 1.3f);
            AddToBattleLog($"{_enemy.Name} attacks ferociously!");
        }
        else if (criticalHit)
        {
            damage = (int)(damage * 1.4f);
            AddToBattleLog($"Critical hit! {_enemy.Name} strikes hard!");
        }
        
        if (playerDefended)
        {
            damage = damage / 2;
            AddToBattleLog($"The attack is weakened by {_player.Name}'s defense!");
        }
        
        damage = Mathf.Max(1, damage);
        _player.TakeDamage(damage);
        
        if (!aggressiveAttack && !criticalHit)
        {
            AddToBattleLog($"{_enemy.Name} attacks for {damage} damage!");
        }
    }
    
    private void EndBattle(bool playerWon)
    {
        GD.Print($"BattleManager.EndBattle called: playerWon = {playerWon}");
        
        _battleInProgress = false;
        _battleTimer.Stop();
        
        // Add spacing and clear result display
        AddToBattleLog(""); // Empty line for spacing
        AddToBattleLog("=== BATTLE RESULT ===");
        
        if (playerWon)
        {
            AddToBattleLog($"🎉 VICTORY! {_player.Name} wins the battle!");
            AddToBattleLog($"Experience gained: {_enemy.ExperienceReward} XP");
            
            int oldLevel = _player.Level;
            _player.GainExperience(_enemy.ExperienceReward);
            
            // Check if player leveled up
            if (_player.Level > oldLevel)
            {
                AddToBattleLog($"⭐ LEVEL UP! {_player.Name} reached level {_player.Level}!");
                AddToBattleLog($"New stats: HP {_player.MaxHealth}, ATK {_player.Attack}, DEF {_player.Defense}");
            }
        }
        else
        {
            AddToBattleLog($"💀 DEFEAT! {_player.Name} was defeated by {_enemy.Name}...");
            AddToBattleLog("Game Over - You will return to the main menu.");
        }
        
        AddToBattleLog("=====================");
        
        // Show the close button
        GetOkButton().Visible = true;
        GetOkButton().Text = "Continue";
        
        // Emit the signal immediately instead of waiting
        GD.Print("BattleManager emitting BattleFinished signal immediately");
        EmitSignal(SignalName.BattleFinished, playerWon, false); // false for not escaped
    }
    
    private void EndBattleWithEscape()
    {
        GD.Print("BattleManager.EndBattleWithEscape called: Player escaped");
        
        _battleInProgress = false;
        _battleTimer.Stop();
        
        // Add spacing and clear result display
        AddToBattleLog(""); // Empty line for spacing
        AddToBattleLog("=== BATTLE RESULT ===");
        AddToBattleLog($"🏃 ESCAPED! {_player.Name} fled from battle!");
        AddToBattleLog("No experience gained from escaping.");
        AddToBattleLog("=====================");
        
        // Show the close button
        GetOkButton().Visible = true;
        GetOkButton().Text = "Continue";
        
        // Emit the signal immediately instead of waiting
        GD.Print("BattleManager emitting BattleFinished signal with escape immediately");
        EmitSignal(SignalName.BattleFinished, false, true); // false for not won, true for escaped
    }
    
    private void AddToBattleLog(string message)
    {
        _battleLog += message + "\n";
        GD.Print(message);
    }
}
