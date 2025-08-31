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
        
        // Create enemy based on area/theme rather than just distance
        Enemy enemy = CreateEnemyByArea(enemyPosition);
        
        _gameManager.StartBattle(enemy);
    }
    
    private Enemy CreateEnemyByArea(Vector2I position)
    {
        int x = position.X;
        int y = position.Y;
        
        // Starting area (safe zone)
        if (IsInArea(x, y, 5, GridHeight / 2 - 10, 30, 20))
        {
            return GD.Randf() < 0.8f ? Enemy.CreateGoblin() : Enemy.CreateOrc();
        }
        
        // Forest zones
        if (IsInArea(x, y, 40, 15, 35, 30) || IsInArea(x, y, 45, 50, 25, 25))
        {
            float rand = GD.Randf();
            if (rand < 0.4f) return Enemy.CreateGoblin();
            else if (rand < 0.7f) return Enemy.CreateOrc();
            else if (rand < 0.9f) return Enemy.CreateForestSpirit();
            else return Enemy.CreateSkeletonWarrior();
        }
        
        // Cave systems
        if (IsInArea(x, y, 20, 90, 40, 35) || IsInArea(x, y, 70, 95, 30, 30))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateSkeletonWarrior();
            else if (rand < 0.6f) return Enemy.CreateCaveSpider();
            else if (rand < 0.8f) return Enemy.CreateOrc();
            else return Enemy.CreateTroll();
        }
        
        // Desert area
        if (IsInArea(x, y, 90, 40, 45, 40))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateDesertScorpion();
            else if (rand < 0.5f) return Enemy.CreateOrc();
            else if (rand < 0.7f) return Enemy.CreateSkeletonWarrior();
            else if (rand < 0.9f) return Enemy.CreateTroll();
            else return Enemy.CreateDragon();
        }
        
        // Swamp lands
        if (IsInArea(x, y, 25, 130, 35, 25) || IsInArea(x, y, 70, 135, 25, 20))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateSwampWretch();
            else if (rand < 0.5f) return Enemy.CreateTroll();
            else if (rand < 0.7f) return Enemy.CreateSkeletonWarrior();
            else return Enemy.CreateDarkMage();
        }
        
        // Mountain peak
        if (IsInArea(x, y, 110, 15, 40, 35))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateMountainWyvern();
            else if (rand < 0.6f) return Enemy.CreateDragon();
            else if (rand < 0.8f) return Enemy.CreateTroll();
            else return Enemy.CreateDarkMage();
        }
        
        // Dungeon complex
        if (IsInArea(x, y, 115, 85, 30, 35))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateDungeonGuardian();
            else if (rand < 0.5f) return Enemy.CreateDarkMage();
            else if (rand < 0.7f) return Enemy.CreateDragon();
            else return Enemy.CreateDemonLord();
        }
        
        // Boss arena
        if (IsInArea(x, y, 135, 135, 20, 20))
        {
            return GD.Randf() < 0.7f ? Enemy.CreateDemonLord() : Enemy.CreateBoss();
        }
        
        // Default corridor enemies based on distance from start
        int distanceFromStart = Mathf.Abs(x - 5) + Mathf.Abs(y - GridHeight / 2);
        
        if (distanceFromStart < 30)
        {
            return GD.Randf() < 0.7f ? Enemy.CreateGoblin() : Enemy.CreateOrc();
        }
        else if (distanceFromStart < 60)
        {
            float rand = GD.Randf();
            if (rand < 0.4f) return Enemy.CreateGoblin();
            else if (rand < 0.7f) return Enemy.CreateOrc();
            else return Enemy.CreateSkeletonWarrior();
        }
        else if (distanceFromStart < 90)
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateOrc();
            else if (rand < 0.6f) return Enemy.CreateSkeletonWarrior();
            else return Enemy.CreateTroll();
        }
        else if (distanceFromStart < 120)
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateSkeletonWarrior();
            else if (rand < 0.6f) return Enemy.CreateTroll();
            else return Enemy.CreateDragon();
        }
        else if (distanceFromStart < 180)
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateTroll();
            else if (rand < 0.6f) return Enemy.CreateDragon();
            else return Enemy.CreateDarkMage();
        }
        else
        {
            float rand = GD.Randf();
            if (rand < 0.4f) return Enemy.CreateDarkMage();
            else if (rand < 0.7f) return Enemy.CreateDemonLord();
            else return Enemy.CreateBoss();
        }
    }
    
    private bool IsInArea(int x, int y, int areaX, int areaY, int width, int height)
    {
        return x >= areaX && x < areaX + width && y >= areaY && y < areaY + height;
    }
    
    // Helper property to access grid size
    private int GridHeight => 160;

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
        // Clean up the battle dialog
        if (_battleManager != null)
        {
            _battleManager.QueueFree();
            _battleManager = null;
        }
        
        // Ensure battle state is properly reset (safety check)
        if (_gameManager.IsInBattle)
        {
            GD.Print("WARNING: Battle state still active after dialog close, forcing reset");
            _gameManager.EndBattle(true); // Force end battle state
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
        
        // End the battle in game manager FIRST to allow player movement
        // Pass true if either won or escaped - this just ends the battle state
        _gameManager.EndBattle(playerWon || playerEscaped);
        GD.Print($"Battle state ended in GameManager. IsInBattle: {_gameManager.IsInBattle}");
        
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
