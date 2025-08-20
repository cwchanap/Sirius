using Godot;

public partial class Game : Node2D
{
    private GameManager _gameManager;
    private GridMap _gridMap;
    private Control _gameUI;
    private Label _playerNameLabel;
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private BattleManager _battleManager;
    private Vector2I _lastEnemyPosition; // Store enemy position for after battle

    public override void _Ready()
    {
        GD.Print("Game scene loaded");
        
        // Get references
        _gameManager = GetNode<GameManager>("GameManager");
        _gridMap = GetNode<GridMap>("GridMap");
        _gameUI = GetNode<Control>("UI/GameUI");
        
        // Get UI labels
        _playerNameLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerName");
        _playerLevelLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerLevel");
        _playerHealthLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerHealth");
        
        // Connect signals
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        
        // Connect to grid map for enemy encounters
        _gridMap.EnemyEncountered += OnEnemyEncountered;
        
        // Update UI
        UpdatePlayerUI();
    }

    public override void _Input(InputEvent @event)
    {
        // Return to main menu when ESC is pressed (only if not in battle)
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Escape && !_gameManager.IsInBattle)
            {
                ReturnToMainMenu();
            }
        }
    }

    private void OnEnemyEncountered(Vector2I enemyPosition)
    {
        GD.Print($"Enemy encountered at position: {enemyPosition}");
        
        // Check if player is alive
        if (!_gameManager.Player.IsAlive)
        {
            GD.Print("Player is dead, cannot start battle");
            ReturnToMainMenu();
            return;
        }
        
        _lastEnemyPosition = enemyPosition;
        
        // Create enemy based on position (simple logic for now)
        Enemy enemy;
        
        // Vary enemies based on position
        if (enemyPosition.X < 5)
        {
            enemy = Enemy.CreateGoblin();
        }
        else if (enemyPosition.Y < 5)
        {
            enemy = Enemy.CreateOrc();
        }
        else
        {
            enemy = Enemy.CreateDragon();
        }
        
        _gameManager.StartBattle(enemy);
    }

    private void OnBattleStarted(Enemy enemy)
    {
        GD.Print($"Starting battle with {enemy.Name}");
        
        // Hide game UI
        _gameUI.Visible = false;
        
        // Load battle scene
        var battleScene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn");
        _battleManager = battleScene.Instantiate<BattleManager>();
        GetNode("UI").AddChild(_battleManager);
        
        // Connect battle signals
        _battleManager.BattleFinished += OnBattleFinished;
        
        // Start the battle
        _battleManager.StartBattle(_gameManager.Player, enemy);
    }

    private void OnBattleEnded(bool playerWon)
    {
        GD.Print($"Battle ended in GameManager. Player won: {playerWon}");
        // Battle logic is now handled in OnBattleFinished
    }

    private void OnBattleFinished(bool playerWon, bool playerEscaped)
    {
        GD.Print($"OnBattleFinished called. Player won: {playerWon}, Player escaped: {playerEscaped}");
        
        // Prevent multiple calls
        if (_battleManager == null)
        {
            GD.Print("BattleManager is null, battle already finished");
            return;
        }
        
        // Disconnect signal to prevent multiple calls
        _battleManager.BattleFinished -= OnBattleFinished;
        
        // Clean up battle UI
        _battleManager.QueueFree();
        _battleManager = null;
        
        // Show game UI again
        _gameUI.Visible = true;
        
        if (playerWon)
        {
            // Remove enemy from grid at the exact position where it was encountered
            _gridMap.RemoveEnemy(_lastEnemyPosition);
            GD.Print($"Enemy removed from position: {_lastEnemyPosition}");
        }
        else if (playerEscaped)
        {
            // Player escaped, don't remove enemy but don't end the game either
            GD.Print("Player escaped successfully, continuing game");
        }
        
        // Update UI
        UpdatePlayerUI();
        
        // End the battle in game manager
        _gameManager.EndBattle(playerWon);
        
        // Only return to main menu if player was actually defeated (not escaped)
        if (!playerWon && !playerEscaped)
        {
            GetTree().CreateTimer(2.0).Timeout += ReturnToMainMenu;
        }
    }

    private void UpdatePlayerUI()
    {
        if (_gameManager?.Player != null)
        {
            _playerNameLabel.Text = _gameManager.Player.Name;
            _playerLevelLabel.Text = $"Level: {_gameManager.Player.Level}";
            _playerHealthLabel.Text = $"HP: {_gameManager.Player.CurrentHealth}/{_gameManager.Player.MaxHealth}";
        }
    }

    private void ReturnToMainMenu()
    {
        GD.Print("Returning to main menu");
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }
}
