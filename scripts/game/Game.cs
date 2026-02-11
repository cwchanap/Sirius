using Godot;
using System;

public partial class Game : Node2D
{
    [Export] public bool EnableCameraSmoothing { get; set; } = true;
    [Export] public float CameraSmoothingSpeed { get; set; } = 8.0f;
    // Set to > 0 to override zoom uniformly (X and Y). If 0 or less, keep scene's zoom.
    [Export] public float CameraZoomOverride { get; set; } = 0.0f;
    private GameManager _gameManager;
    private FloorManager _floorManager;
    private PlayerController _playerController;
    private GridMap _gridMap; // Dynamically set by FloorManager
    private Control _gameUI;
    private Camera2D _camera;
    private Label _playerNameLabel;
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private Label _playerExperienceLabel;
    private Label _playerGoldLabel;
    private BattleManager _battleManager;
    private Vector2I _lastEnemyPosition; // Store enemy position for after battle
    private PlayerDisplay _playerDisplay; // Visual sprite for player when using baked TileMaps
    private InventoryMenuController _inventoryMenu;

    private SaveLoadDialog _saveLoadDialog;

    public override void _EnterTree()
    {
        // Set SkipInitialFloorLoad early (parent _EnterTree runs before children's _Ready)
        // so FloorManager knows not to auto-load floor 0 when a save is pending.
        var fm = GetNodeOrNull<FloorManager>("FloorManager");
        if (fm != null && SaveManager.Instance?.PendingLoadData != null)
        {
            fm.SkipInitialFloorLoad = true;
        }
    }

    public override void _Ready()
    {
        GD.Print("Game scene loaded");

        // Get references
        _gameManager = GetNode<GameManager>("GameManager");
        _floorManager = GetNode<FloorManager>("FloorManager");
        _playerController = GetNode<PlayerController>("PlayerController");
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

        // Set FloorManager reference in GameManager for save system
        _gameManager.SetFloorManager(_floorManager);

        // Initialize HUD labels BEFORE connecting signals (LoadFromSaveData may emit PlayerStatsChanged)
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
        _playerGoldLabel =
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerGold") ??
            GetNodeOrNull<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerGold");

        // Connect signals AFTER HUD labels are initialized, so signals are always connected even if save is corrupted
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        _gameManager.PlayerStatsChanged += OnPlayerStatsChanged;

        // Connect to FloorManager for floor loading
        _floorManager.FloorLoaded += OnFloorLoaded;
        _floorManager.FloorChanged += OnFloorChanged;

        // Check for pending load data from main menu
        bool hadPendingData = SaveManager.Instance?.PendingLoadData != null;
        bool skipLoad = false;
        if (hadPendingData)
        {
            var loadData = SaveManager.Instance.PendingLoadData;
            SaveManager.Instance.PendingLoadData = null;

            // Validate save data before loading
            if (loadData.PlayerPosition == null)
            {
                GD.PushError("Save data corrupted: Missing player position");
                ShowCorruptedSaveError();
                skipLoad = true;
            }

            if (!skipLoad && loadData.Character == null)
            {
                GD.PushError("Save data corrupted: Missing character data");
                ShowCorruptedSaveError();
                skipLoad = true;
            }

            // Only validate floor index if floors are loaded; otherwise defer validation
            if (!skipLoad && _floorManager.GetFloorCount() > 0 &&
                (loadData.CurrentFloorIndex < 0 || loadData.CurrentFloorIndex >= _floorManager.GetFloorCount()))
            {
                GD.PushError($"Save data corrupted: Invalid floor index {loadData.CurrentFloorIndex}");
                ShowCorruptedSaveError();
                skipLoad = true;
            }

            // Only validate position if floors are loaded and we can determine dimensions
            if (!skipLoad && _floorManager.GetFloorCount() > 0)
            {
                var targetFloor = _floorManager.GetFloorByIndex(loadData.CurrentFloorIndex);
                if (targetFloor != null)
                {
                    // Get actual grid dimensions from the floor definition's scene
                    // We use a reasonable default if the scene isn't loaded yet
                    int gridWidth = 160;
                    int gridHeight = 160;

                    if (loadData.PlayerPosition.X < 0 || loadData.PlayerPosition.X >= gridWidth ||
                        loadData.PlayerPosition.Y < 0 || loadData.PlayerPosition.Y >= gridHeight)
                    {
                        GD.PushError($"Save data corrupted: Player position ({loadData.PlayerPosition.X}, {loadData.PlayerPosition.Y}) out of bounds");
                        ShowCorruptedSaveError();
                        skipLoad = true;
                    }
                }
            }

            if (!skipLoad)
            {
                GD.Print($"Loading save data: Floor {loadData.CurrentFloorIndex}, Position ({loadData.PlayerPosition.X}, {loadData.PlayerPosition.Y})");

                // Load player state
                _gameManager.LoadFromSaveData(loadData);

                // Load floor with saved position (deferred to after FloorManager is ready)
                CallDeferred(nameof(LoadFloorFromSave), loadData.CurrentFloorIndex, loadData.PlayerPosition.ToVector2I());
            }
        }

        if (skipLoad)
        {
            // Save data was corrupted - don't initialize game, just show error and return to menu
            GD.Print("Save data corrupted, aborting game initialization");
            // Still need fresh player state for clean state, but skip floor loading
            _gameManager.ResetBattleState();
            _gameManager.EnsureFreshPlayer();
        }
        else if (!hadPendingData)
        {
            // No save data - start new game with default floor
            _gameManager.ResetBattleState();
            _gameManager.EnsureFreshPlayer();

            // FloorManager._Ready() already loaded floor 0 with default position
            // Player setup and camera positioning will happen in OnFloorLoaded callback
        }

        // Update UI after all initialization is complete
        UpdatePlayerUI();

        // Player display and camera will be set up in OnFloorLoaded after floor loads
        
        // Load and setup inventory menu
        SetupInventoryMenu();
    }

    private void SetupPlayerDisplay()
    {
        // Find the PlayerDisplay that's already in the floor scene
        _playerDisplay = _gridMap.GetNodeOrNull<PlayerDisplay>("PlayerDisplay");
        
        if (_playerDisplay == null)
        {
            GD.PrintErr("PlayerDisplay not found in floor scene! Please add it to the floor scene.");
            return;
        }
        
        // Initialize with the current GridMap
        _playerDisplay.Initialize(_gridMap);
        // Ensure initial sync with current player position
        _playerDisplay.UpdatePosition(_gridMap.GetPlayerPosition());
    }
    
    private void SetInitialCameraPosition()
    {
        Vector2 playerWorldPos = _gridMap.GetWorldPosition(_gridMap.GetPlayerPosition());
        // GetWorldPosition now returns absolute world coordinates (includes TileMapLayer offset)
        _camera.Position = playerWorldPos;
        
        // Calculate visible world area with current zoom
        Vector2 viewportSize = GetViewportRect().Size;
        Vector2 worldViewSize = viewportSize / _camera.Zoom;
        Vector2 worldMin = _camera.Position - worldViewSize / 2;
        Vector2 worldMax = _camera.Position + worldViewSize / 2;
        
        GD.Print($"📷 Camera positioned at: {_camera.Position}, zoom: {_camera.Zoom}");
        GD.Print($"   Viewport size: {viewportSize}, World view size: {worldViewSize}");
        GD.Print($"   Visible world area: ({worldMin.X:F1}, {worldMin.Y:F1}) to ({worldMax.X:F1}, {worldMax.Y:F1})");
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
                    // Not in battle: show save menu (or close it if open)
                    if (_saveLoadDialog != null && _saveLoadDialog.Visible)
                    {
                        CleanupSaveDialog();
                    }
                    else
                    {
                        ShowSaveMenu();
                    }
                    GetViewport().SetInputAsHandled();
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
        // GetWorldPosition now returns absolute world coordinates (includes TileMapLayer offset)
        _camera.Position = worldPos;
        
        // Force redraw since we're using viewport culling
        _gridMap.QueueRedraw();

        // Update visual player sprite position
        _playerDisplay?.UpdatePosition(newPosition);
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
        // position is internal grid coordinates from GridMap
        // Need to convert to tilemap coordinates to match EnemySpawn.GridPosition
        Vector2I tilemapPos = _gridMap.InternalGridToTilemapCoords(position);
        
        GD.Print($"Looking for spawn: internal grid ({position.X}, {position.Y}) → tilemap ({tilemapPos.X}, {tilemapPos.Y})");
        
        var nodes = GetTree().GetNodesInGroup("EnemySpawn");
        foreach (Node n in nodes)
        {
            if (n is EnemySpawn spawn)
            {
                GD.Print($"  Checking spawn at GridPosition ({spawn.GridPosition.X}, {spawn.GridPosition.Y})");
                if (spawn.GridPosition == tilemapPos)
                {
                    GD.Print($"  ✓ Match found! Using blueprint spawn");
                    // Use new blueprint-based system (supports custom stats per spawn)
                    return spawn.CreateEnemyInstance();
                }
            }
        }
        GD.Print($"  No spawn found, using area-based generation");
        // No spawn found at position, fall back to area-based generation
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
            // Pass false to avoid triggering auto-save on a potentially lost/escaped battle.
            // This is a safety fallback; normal battle completion handles victory auto-save.
            _gameManager.EndBattle(false);
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
        // Only pass actual victory state - escape should not trigger auto-save
        _gameManager.EndBattle(playerWon);
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

            if (_playerNameLabel != null)
                _playerNameLabel.Text = player.Name;
            if (_playerLevelLabel != null)
                _playerLevelLabel.Text = $"Level: {player.Level}";
            if (_playerHealthLabel != null)
                _playerHealthLabel.Text = $"HP: {player.CurrentHealth}/{effectiveMaxHealth}";
            if (_playerExperienceLabel != null)
                _playerExperienceLabel.Text = $"EXP: {player.Experience}/{player.ExperienceToNext}";
            if (_playerGoldLabel != null)
            {
                _playerGoldLabel.Text = $"Gold: {player.Gold}";
            }

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
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerAttackHUD") ??
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/PlayerStats/PlayerAttackHUD");
            if (atkLabel != null)
            {
                int effectiveAttack = player.GetEffectiveAttack();
                int atkBonus = player.Equipment.GetAttackBonus();
                if (atkBonus > 0)
                {
                    atkLabel.Text = $"ATK: {player.Attack} [color=#4CAF50]+ {atkBonus}[/color]";
                }
                else
                {
                    atkLabel.Text = $"ATK: {player.Attack}";
                }
            }

            var defLabel =
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerDefenseHUD") ??
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/PlayerStats/PlayerDefenseHUD");
            if (defLabel != null)
            {
                int effectiveDefense = player.GetEffectiveDefense();
                int defBonus = player.Equipment.GetDefenseBonus();
                if (defBonus > 0)
                {
                    defLabel.Text = $"DEF: {player.Defense} [color=#4CAF50]+ {defBonus}[/color]";
                }
                else
                {
                    defLabel.Text = $"DEF: {player.Defense}";
                }
            }

            var spdLabel =
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/Content/PlayerStats/PlayerSpeedHUD") ??
                GetNodeOrNull<RichTextLabel>("UI/GameUI/TopPanel/PlayerStats/PlayerSpeedHUD");
            if (spdLabel != null)
            {
                int effectiveSpeed = player.GetEffectiveSpeed();
                int spdBonus = player.Equipment.GetSpeedBonus();
                if (spdBonus > 0)
                {
                    spdLabel.Text = $"SPD: {player.Speed} [color=#4CAF50]+ {spdBonus}[/color]";
                }
                else
                {
                    spdLabel.Text = $"SPD: {player.Speed}";
                }
            }
        }
    }

    private void ReturnToMainMenu()
    {
        GD.Print("Returning to main menu");
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }

    /// <summary>
    /// Loads a floor from save data with a specific player position.
    /// </summary>
    private void LoadFloorFromSave(int floorIndex, Vector2I playerPosition)
    {
        GD.Print($"Loading floor {floorIndex} with player position ({playerPosition.X}, {playerPosition.Y})");
        _floorManager.LoadFloor(floorIndex, playerPosition);
    }

    /// <summary>
    /// Shows the save menu dialog.
    /// </summary>
    private void ShowSaveMenu()
    {
        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.SaveSlotSelected -= OnSaveSlotSelected;
            _saveLoadDialog.DialogClosed -= OnSaveDialogClosed;
            _saveLoadDialog.QueueFree();
        }

        _saveLoadDialog = new SaveLoadDialog();
        GetNode("UI").AddChild(_saveLoadDialog);
        _saveLoadDialog.SaveSlotSelected += OnSaveSlotSelected;
        _saveLoadDialog.DialogClosed += OnSaveDialogClosed;
        _saveLoadDialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
    }

    private void OnSaveSlotSelected(int slot)
    {
        // Prevent saving if player is defeated (CurrentHealth <= 0)
        // This can happen during the 2-second delay after battle loss before returning to menu
        if (_gameManager.Player != null && !_gameManager.Player.IsAlive)
        {
            GD.PrintErr("Save blocked: Player is defeated.");
            ShowSaveError("Cannot save while defeated.");
            return;
        }

        var saveData = _gameManager.CollectSaveData();
        if (saveData == null)
        {
            GD.PrintErr("Save failed: unable to collect save data.");
            ShowSaveError("Unable to collect save data.");
            return;
        }

        if (SaveManager.Instance == null)
        {
            GD.PushError("Save failed: SaveManager not initialized.");
            ShowSaveError("Save system unavailable.");
            return;
        }

        bool success = SaveManager.Instance.SaveGame(slot, saveData);
        if (success)
        {
            GD.Print($"Game saved to slot {slot}");
            CleanupSaveDialog();
        }
        else
        {
            GD.PrintErr($"Save failed for slot {slot}.");
            ShowSaveError("Failed to save game.");
        }
    }

    private void OnSaveDialogClosed()
    {
        CleanupSaveDialog();
    }

    private void CleanupSaveDialog()
    {
        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.SaveSlotSelected -= OnSaveSlotSelected;
            _saveLoadDialog.DialogClosed -= OnSaveDialogClosed;
            _saveLoadDialog.QueueFree();
            _saveLoadDialog = null;
        }
    }

    private void ShowSaveError(string message)
    {
        var popup = new AcceptDialog();
        popup.Title = "Save Failed";
        popup.DialogText = message;
        GetNode("UI").AddChild(popup);
        popup.PopupCentered();

        // Clean up when confirmed or canceled
        popup.Confirmed += () =>
        {
            if (IsInstanceValid(popup))
                popup.QueueFree();
        };
        popup.Canceled += () =>
        {
            if (IsInstanceValid(popup))
                popup.QueueFree();
        };
    }

    private void ShowCorruptedSaveError()
    {
        var popup = new AcceptDialog();
        popup.Title = "Load Failed";
        popup.DialogText = "Save file is corrupted or invalid.\nReturning to main menu.";
        GetNode("UI").AddChild(popup);
        popup.PopupCentered();

        // Guard against double invocation (both Confirmed and Canceled can fire)
        bool handled = false;
        Action cleanupAndReturn = () =>
        {
            if (handled) return;
            handled = true;
            if (IsInstanceValid(popup))
                popup.QueueFree();
            GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
        };

        popup.Confirmed += cleanupAndReturn;
        popup.Canceled += cleanupAndReturn;
    }

    private void OnPlayerStatsChanged()
    {
        UpdatePlayerUI();
    }

    private void OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)
    {
        GD.Print($"🎮 Game.OnFloorLoaded: Floor '{floorDef.FloorName}' ready");
        
        // Disconnect signals from old GridMap to prevent handler accumulation
        if (_gridMap != null)
        {
            _gridMap.EnemyEncountered -= OnEnemyEncountered;
            _gridMap.PlayerMoved -= OnPlayerMoved;
        }
        
        // Update dynamic GridMap reference
        _gridMap = gridMap;
        
        // Update PlayerController's GridMap reference
        if (_playerController != null)
        {
            _playerController.SetGridMap(_gridMap);
        }
        
        // Connect GridMap signals
        if (_gridMap != null)
        {
            _gridMap.EnemyEncountered += OnEnemyEncountered;
            _gridMap.PlayerMoved += OnPlayerMoved;
        }
        
        // Setup player display for this floor
        CallDeferred(nameof(SetupPlayerDisplay));
        
        // Update camera position
        CallDeferred(nameof(SetInitialCameraPosition));
        
        GD.Print($"✅ Floor '{floorDef.FloorName}' ready for gameplay");
    }

    private void OnFloorChanged(int oldFloorIndex, int newFloorIndex)
    {
        GD.Print($"🔄 Floor transition: {oldFloorIndex} → {newFloorIndex}");
        UpdatePlayerUI();
        
        // Clean up old player display if transitioning
        if (_playerDisplay != null)
        {
            _playerDisplay.QueueFree();
            _playerDisplay = null;
        }
    }
}
