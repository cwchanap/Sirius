using Godot;

public partial class Game : Node2D
{
    [Export] public bool EnableCameraSmoothing { get; set; } = true;
    [Export] public float CameraSmoothingSpeed { get; set; } = 8.0f;
    // Set to > 0 to override zoom uniformly (X and Y). If 0 or less, keep scene's zoom.
    [Export] public float CameraZoomOverride { get; set; } = 0.0f;
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
    private PlayerDisplay _playerDisplay; // Visual sprite for player when using baked TileMaps
    private InventoryMenuController _inventoryMenu;

    public override void _Ready()
    {
        GD.Print("Game scene loaded");

        // Get references
        _gameManager = GetNode<GameManager>("GameManager");
        _gridMap = GetNode<GridMap>("GridMap");
        _gameUI = GetNode<Control>("UI/GameUI");
        // Make sure the UI layer is visible at runtime
        var uiLayer = GetNodeOrNull<CanvasLayer>("UI");
        if (uiLayer != null)
        {
            uiLayer.Visible = true;
        }
        _camera = GetNode<Camera2D>("Camera2D");
        // Ensure this camera is active at runtime
        _camera.MakeCurrent();

        // Configure camera smoothing and zoom
        _camera.PositionSmoothingEnabled = EnableCameraSmoothing;
        _camera.PositionSmoothingSpeed = CameraSmoothingSpeed;
        if (CameraZoomOverride > 0.0f)
        {
            _camera.Zoom = new Vector2(CameraZoomOverride, CameraZoomOverride);
        }

        // Reset battle state to ensure clean start
        _gameManager.ResetBattleState();
        // Ensure the player is reinitialized if previous run ended in defeat
        _gameManager.EnsureFreshPlayer();

        // Get UI labels (prefer new Content hierarchy, fallback to old paths)
        _playerNameLabel =
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerName") ??
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerName");
        _playerLevelLabel =
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerLevel") ??
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerLevel");
        _playerHealthLabel =
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerHealth") ??
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerHealth");
        _playerExperienceLabel =
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerExperience") ??
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerExperience");

        // Connect signals
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        _gameManager.PlayerStatsChanged += OnPlayerStatsChanged;

        // Connect to grid map for enemy encounters
        _gridMap.EnemyEncountered += OnEnemyEncountered;
        _gridMap.PlayerMoved += OnPlayerMoved;

        // Update UI
        UpdatePlayerUI();

        // Create the visible player sprite after all nodes are ready
        // This ensures GridMap has built its grid from baked TileMaps
        CallDeferred(nameof(SetupPlayerDisplay));

        // Use a deferred call to set camera position after grid is ready
        CallDeferred(nameof(SetInitialCameraPosition));
        
        // Load and setup inventory menu
        SetupInventoryMenu();
    }

    private void SetupPlayerDisplay()
    {
        if (_playerDisplay != null) return;
        _playerDisplay = new PlayerDisplay();
        // Attach under GridMap so ZIndex layering works with TileMap layers
        _gridMap.AddChild(_playerDisplay);
        _playerDisplay.Initialize(_gridMap);
        // Ensure initial sync with current player position
        _playerDisplay.UpdatePosition(_gridMap.GetPlayerPosition());
    }
    
    private void SetInitialCameraPosition()
    {
        Vector2 playerWorldPos = _gridMap.GetWorldPosition(_gridMap.GetPlayerPosition());
        _camera.Position = playerWorldPos + GetTileLayerVisualOffset();
        GD.Print($"Camera positioned (follow): {_camera.Position}");
    }

    private void SetupInventoryMenu()
    {
        var inventoryScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        if (inventoryScene == null)
        {
            GD.PushError("Failed to load InventoryMenu scene!");
            return;
        }

        _inventoryMenu = inventoryScene.Instantiate<InventoryMenuController>();
        if (_inventoryMenu == null)
        {
            GD.PushError("Failed to instantiate InventoryMenuController!");
            return;
        }

        GetNode("UI").AddChild(_inventoryMenu);
        _inventoryMenu.Hide();
    }

    public override void _Input(InputEvent @event)
    {
        // Handle inventory toggle (I key)
        if (@event.IsActionPressed("toggle_inventory"))
        {
            if (_inventoryMenu != null && !_gameManager.IsInBattle)
            {
                if (_inventoryMenu.Visible)
                {
                    _inventoryMenu.CloseMenu();
                }
                else
                {
                    _inventoryMenu.OpenMenu();
                }
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        // Handle ESC
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Escape)
            {
                // Close inventory if open
                if (_inventoryMenu != null && _inventoryMenu.Visible)
                {
                    _inventoryMenu.CloseMenu();
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (!_gameManager.IsInBattle)
                {
                    // Not in battle: go back to main menu
                    ReturnToMainMenu();
                }
                else
                {
                    // In battle: close the battle dialog as an escape to unlock input
                    if (_battleManager != null)
                    {
                        GD.Print("ESC pressed during battle - requesting battle dialog to close as escape");
                        _battleManager.ForceCloseAsEscape();
                    }
                    else
                    {
                        // Fallback safety: if dialog reference missing, force-clear battle state
                        GD.PrintErr("ESC pressed during battle but BattleManager is null - forcing EndBattle(escaped)");
                        _gameManager.EndBattle(false); // treat as not won (escape); no enemy removal here
                        UpdatePlayerUI();
                    }
                }
            }
        }
    }

    private void OnPlayerMoved(Vector2I newPosition)
    {
        Vector2 worldPos = _gridMap.GetWorldPosition(newPosition);
        _camera.Position = worldPos + GetTileLayerVisualOffset();
        
        // Force redraw since we're using viewport culling
        _gridMap.QueueRedraw();

        // Update visual player sprite position
        _playerDisplay?.UpdatePosition(newPosition);
    }

    private Vector2 GetTileLayerVisualOffset()
    {
        // Include the TileMapLayer node's position (e.g., GroundLayer.position)
        // so the camera centers on the same point the PlayerDisplay uses.
        var ground = _gridMap.GetNodeOrNull<TileMapLayer>("GroundLayer");
        return ground != null ? ground.Position : Vector2.Zero;
    }

    private void OnEnemyEncountered(Vector2I enemyPosition)
    {
        GD.Print($"Enemy encountered at position: {enemyPosition}");
        // Make sure the player is fresh in case a previous session ended with 0 HP
        _gameManager.EnsureFreshPlayer();
        
        // Check if player is alive
        if (!_gameManager.Player.IsAlive)
        {
            GD.Print("Player is dead, cannot start battle");
            ReturnToMainMenu();
            return;
        }
        
        _lastEnemyPosition = enemyPosition;
        
        // Prefer a scene-placed EnemySpawn override when available
        Enemy enemy = CreateEnemyFromSpawnOrArea(enemyPosition);
        
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

    // Attempt to create an enemy from a scene-placed EnemySpawn at the given grid position.
    // If no spawn exists or its EnemyType is empty/unknown, fall back to area-based selection.
    private Enemy CreateEnemyFromSpawnOrArea(Vector2I position)
    {
        var nodes = GetTree().GetNodesInGroup("EnemySpawn");
        foreach (Node n in nodes)
        {
            if (n is EnemySpawn spawn && spawn.GridPosition == position)
            {
                string t = (spawn.EnemyType ?? string.Empty).ToLower();
                switch (t)
                {
                    case "goblin": return Enemy.CreateGoblin();
                    case "orc": return Enemy.CreateOrc();
                    case "skeleton_warrior": return Enemy.CreateSkeletonWarrior();
                    case "troll": return Enemy.CreateTroll();
                    case "dragon": return Enemy.CreateDragon();
                    case "forest_spirit": return Enemy.CreateForestSpirit();
                    case "cave_spider": return Enemy.CreateCaveSpider();
                    case "desert_scorpion": return Enemy.CreateDesertScorpion();
                    case "swamp_wretch": return Enemy.CreateSwampWretch();
                    case "mountain_wyvern": return Enemy.CreateMountainWyvern();
                    case "dark_mage": return Enemy.CreateDarkMage();
                    case "dungeon_guardian": return Enemy.CreateDungeonGuardian();
                    case "demon_lord": return Enemy.CreateDemonLord();
                    case "boss": return Enemy.CreateBoss();
                }
                break; // spawn found but no valid type; fall back
            }
        }
        return CreateEnemyByArea(position);
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
        if (battleScene == null)
        {
            GD.PrintErr("ERROR: Failed to load battle scene!");
            return;
        }

        _battleManager = battleScene.Instantiate<BattleManager>();
        if (_battleManager == null)
        {
            GD.PrintErr("ERROR: Failed to instantiate BattleManager!");
            return;
        }

        GetNode("UI").AddChild(_battleManager);

        // Connect battle signals
        _battleManager.BattleFinished += OnBattleFinished;
        _battleManager.Confirmed += OnBattleDialogConfirmed; // Handle OK button press

        // Ensure dialog is properly configured
        _battleManager.PopupWindow = true;
        _battleManager.Exclusive = true;

        // Show the battle dialog
        _battleManager.PopupCentered();
        GD.Print("Battle dialog should now be visible");

        // Start the battle
        _battleManager.StartBattle(_gameManager.Player, enemy);
        GD.Print("Battle started successfully");
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
            var player = _gameManager.Player;
            int effectiveMaxHealth = player.GetEffectiveMaxHealth();
            int effectiveAttack = player.GetEffectiveAttack();
            int effectiveDefense = player.GetEffectiveDefense();
            int effectiveSpeed = player.GetEffectiveSpeed();

            _playerNameLabel.Text = player.Name;
            _playerLevelLabel.Text = $"Level: {player.Level}";
            _playerHealthLabel.Text = $"HP: {player.CurrentHealth}/{effectiveMaxHealth}";
            _playerExperienceLabel.Text = $"EXP: {player.Experience}/{player.ExperienceToNext}";

            // Update progress bars if present
            var hpBar =
                GetNodeOrNull<ProgressBar>("UI/GameUI/TopPanel/Content/PlayerStats/HPBar") ??
                GetNodeOrNull<ProgressBar>("UI/GameUI/TopPanel/PlayerStats/HPBar");
            if (hpBar != null)
            {
                hpBar.MaxValue = effectiveMaxHealth;
                hpBar.Value = player.CurrentHealth;
            }

            var expBar =
                GetNodeOrNull<ProgressBar>("UI/GameUI/TopPanel/Content/PlayerStats/ExpBar") ??
                GetNodeOrNull<ProgressBar>("UI/GameUI/TopPanel/PlayerStats/ExpBar");
            if (expBar != null)
            {
                expBar.MaxValue = _gameManager.Player.ExperienceToNext;
                expBar.Value = _gameManager.Player.Experience;
            }

            // Update level badge text if present
            var badge =
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/IconWrapper/LevelBadge/BadgeLabel") ??
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/IconWrapper/LevelBadge/BadgeLabel");
            if (badge != null)
            {
                badge.Text = $"Lv {_gameManager.Player.Level}";
            }

            // Update additional HUD labels if present
            var atkLabel =
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerAttackHUD") ??
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerAttackHUD");
            if (atkLabel != null)
            {
                atkLabel.Text = $"ATK: {effectiveAttack}";
            }

            var defLabel =
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerDefenseHUD") ??
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerDefenseHUD");
            if (defLabel != null)
            {
                defLabel.Text = $"DEF: {effectiveDefense}";
            }

            var spdLabel =
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerSpeedHUD") ??
                GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerSpeedHUD");
            if (spdLabel != null)
            {
                spdLabel.Text = $"SPD: {effectiveSpeed}";
            }
        }
    }

    private void ReturnToMainMenu()
    {
        GD.Print("Returning to main menu");
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }

    private void OnPlayerStatsChanged()
    {
        UpdatePlayerUI();
    }
}
