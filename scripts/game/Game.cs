using Godot;

public partial class Game : Node2D
{
    private GameManager _gameManager;
    private GridMap _gridMap;
    private Control _gameUI;
    private Camera2D _camera;
    private Label _playerNameLabel;
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private Label _playerExperienceLabel;
    private BattleManager _battleManager;
    private Vector2I _lastEnemyPosition; // Store enemy position for after battle

    public override void _Ready()
    {
        GD.Print("Game scene loaded");
        
        // Get references
        _gameManager = GetNode<GameManager>("GameManager");
        _gridMap = GetNode<GridMap>("GridMap");
        _gameUI = GetNode<Control>("UI/GameUI");
        _camera = GetNode<Camera2D>("Camera2D");
        
        // Get UI labels
        _playerNameLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerName");
        _playerLevelLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerLevel");
        _playerHealthLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerHealth");
        _playerExperienceLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerExperience");
        
        // Connect signals
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        
        // Connect to grid map for enemy encounters
        _gridMap.EnemyEncountered += OnEnemyEncountered;
        _gridMap.PlayerMoved += OnPlayerMoved;
        
        // Update UI
        UpdatePlayerUI();
        
        // Use a deferred call to set camera position after grid is ready
        CallDeferred(nameof(SetInitialCameraPosition));
    }
    
    private void SetInitialCameraPosition()
    {
        // Set initial camera position to follow player
        Vector2 playerWorldPos = _gridMap.GetWorldPosition(_gridMap.GetPlayerPosition());
        _camera.Position = playerWorldPos;
        GD.Print($"Camera positioned at: {_camera.Position}, Player at: {playerWorldPos}");
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

    private void OnPlayerMoved(Vector2I newPosition)
    {
        // Update camera to follow player
        Vector2 worldPos = _gridMap.GetWorldPosition(newPosition);
        _camera.Position = worldPos;
        
        // Force redraw since we're using viewport culling
        _gridMap.QueueRedraw();
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
        
        // Create enemy based on position - different areas have different enemy levels
        Enemy enemy;
        
        // Calculate distance from starting area to determine enemy difficulty
        int distanceFromStart = Mathf.Abs(enemyPosition.X - 1) + Mathf.Abs(enemyPosition.Y - 80);
        
        if (distanceFromStart < 30)
        {
            // Starting area - weak enemies
            enemy = Enemy.CreateGoblin();
        }
        else if (distanceFromStart < 60)
        {
            // Early area - mix of weak and medium enemies
            enemy = GD.Randf() < 0.7f ? Enemy.CreateGoblin() : Enemy.CreateOrc();
        }
        else if (distanceFromStart < 90)
        {
            // Medium area - medium enemies
            enemy = GD.Randf() < 0.5f ? Enemy.CreateOrc() : Enemy.CreateSkeletonWarrior();
        }
        else if (distanceFromStart < 120)
        {
            // Advanced area - strong enemies
            int rand = GD.RandRange(0, 2);
            enemy = rand switch
            {
                0 => Enemy.CreateSkeletonWarrior(),
                1 => Enemy.CreateTroll(),
                _ => Enemy.CreateDragon()
            };
        }
        else if (distanceFromStart < 150)
        {
            // High level area - very strong enemies
            int rand = GD.RandRange(0, 2);
            enemy = rand switch
            {
                0 => Enemy.CreateTroll(),
                1 => Enemy.CreateDragon(),
                _ => Enemy.CreateDarkMage()
            };
        }
        else if (distanceFromStart < 200)
        {
            // Elite area - top tier enemies
            int rand = GD.RandRange(0, 2);
            enemy = rand switch
            {
                0 => Enemy.CreateDarkMage(),
                1 => Enemy.CreateDemonLord(),
                _ => Enemy.CreateDragon()
            };
        }
        else
        {
            // Boss area - ultimate enemies
            enemy = GD.Randf() < 0.8f ? Enemy.CreateDemonLord() : Enemy.CreateBoss();
        }
        
        _gameManager.StartBattle(enemy);
    }

    private void OnBattleStarted(Enemy enemy)
    {
        GD.Print($"Starting battle with {enemy.Name}");
        
        // Don't hide game UI - battle will be shown as a popup dialog
        
        // Load battle scene
        var battleScene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn");
        _battleManager = battleScene.Instantiate<BattleManager>();
        GetNode("UI").AddChild(_battleManager);
        
        // Connect battle signals
        _battleManager.BattleFinished += OnBattleFinished;
        _battleManager.Confirmed += OnBattleDialogConfirmed; // Handle OK button press
        
        // Show the battle dialog
        _battleManager.PopupCentered();
        
        // Start the battle
        _battleManager.StartBattle(_gameManager.Player, enemy);
    }

    private void OnBattleEnded(bool playerWon)
    {
        GD.Print($"Battle ended in GameManager. Player won: {playerWon}");
        // Battle logic is now handled in OnBattleFinished
    }

    private void OnBattleDialogConfirmed()
    {
        GD.Print("Battle dialog confirmed (OK button pressed)");
        // Just close the dialog - the actual battle result was already handled
        if (_battleManager != null)
        {
            _battleManager.QueueFree();
            _battleManager = null;
        }
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
        
        // Disconnect signals to prevent multiple calls
        _battleManager.BattleFinished -= OnBattleFinished;
        _battleManager.Confirmed -= OnBattleDialogConfirmed;
        
        // Don't immediately clean up battle UI - let the dialog handle its own cleanup
        // when the player clicks OK or closes the dialog
        
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
            _playerExperienceLabel.Text = $"EXP: {_gameManager.Player.Experience}/{_gameManager.Player.ExperienceToNext}";
        }
    }

    private void ReturnToMainMenu()
    {
        GD.Print("Returning to main menu");
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }
}
