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
    private NpcInteractionController _npcInteractionController;
    private readonly System.Collections.Generic.HashSet<string> _questFlags = new();
    private PlayerDisplay _playerDisplay; // Visual sprite for player when using baked TileMaps
    private InventoryMenuController _inventoryMenu;
    private bool _isAbortInitialization; // Set when save corruption causes initialization abort
    private bool _hasPendingSaveSpawnValidation;
    private Vector2I _pendingSaveSpawnPosition;
    private int _pendingSaveSpawnFloorIndex = -1;
    private bool _hasShownCorruptedSaveError;

    private SaveLoadDialog? _saveLoadDialog;
    private PauseMenuDialog? _pauseMenuDialog;
    private SettingsMenuController? _settingsMenu;
    private AcceptDialog? _activeErrorPopup;
    private Label? _interactionPromptLabel;
    private bool _pauseMenuRestorePending;
    private bool _saveLoadFromPause;

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
        _gameManager.QuestFlagProvider = () => _questFlags;

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
        EnsureInteractionPromptLabel();

        // Connect signals AFTER HUD labels are initialized, so signals are always connected even if save is corrupted
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        _gameManager.PlayerStatsChanged += OnPlayerStatsChanged;
        _gameManager.NpcInteractionResetRequested += OnNpcInteractionResetRequested;
        _playerController.FacingChanged += OnPlayerFacingChanged;

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

            // Position bounds validation is deferred until floor is loaded
            // (see OnFloorLoaded where we validate against actual GridMap dimensions)

            if (!skipLoad)
            {
                GD.Print($"Loading save data: Floor {loadData.CurrentFloorIndex}, Position ({loadData.PlayerPosition.X}, {loadData.PlayerPosition.Y})");

                // Load player state
                _gameManager.LoadFromSaveData(loadData, _questFlags);

                // Load floor with saved position (deferred to after FloorManager is ready)
                _pendingSaveSpawnPosition = loadData.PlayerPosition.ToVector2I();
                _pendingSaveSpawnFloorIndex = loadData.CurrentFloorIndex;
                _hasPendingSaveSpawnValidation = true;
                CallDeferred(nameof(LoadFloorFromSave), loadData.CurrentFloorIndex, _pendingSaveSpawnPosition);
            }
        }

        if (skipLoad)
        {
            // Save data was corrupted - don't initialize game, just show error and return to menu
            GD.Print("Save data corrupted, aborting game initialization");
            _isAbortInitialization = true;
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
            if (_inventoryMenu != null && !_gameManager.IsInBattle && !_gameManager.IsInNpcInteraction && !_gameManager.IsInWorldInteraction
                && (_settingsMenu == null || !GodotObject.IsInstanceValid(_settingsMenu))
                && (_saveLoadDialog == null || !GodotObject.IsInstanceValid(_saveLoadDialog))
                && (_pauseMenuDialog == null || !GodotObject.IsInstanceValid(_pauseMenuDialog) || !_pauseMenuDialog.Visible))
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

        // Handle pause menu (ESC / remapped key)
        if (@event.IsActionPressed("pause_menu"))
        {
            HandlePauseMenuInput();
        }
    }

    protected void HandlePauseMenuInput()
    {
        // Dismiss an active error popup first — it must not be left orphaned
        // while HandlePauseMenuInput closes/reopens the save flow.
        if (_activeErrorPopup != null && IsInstanceValid(_activeErrorPopup))
        {
            _activeErrorPopup.QueueFree();
            _activeErrorPopup = null;
            GetViewport().SetInputAsHandled();
            return;
        }

        // Close inventory if open
        if (_inventoryMenu != null && _inventoryMenu.Visible)
        {
            _inventoryMenu.CloseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        // Safety fallback: SettingsMenuController._Input normally consumes ESC and
        // emits Closed synchronously, but if it fails to handle it (e.g. process input
        // disabled), close settings here so ESC never opens the pause menu on top.
        // Skip this fallback while the player is actively rebinding a key or when an
        // OptionButton popup is open — in both cases the event must reach the popup
        // / capture logic rather than force-closing the entire settings panel.
        if (_settingsMenu != null && GodotObject.IsInstanceValid(_settingsMenu)
            && !_settingsMenu.IsRebinding && !_settingsMenu.IsPopupOpen)
        {
            OnPauseSettingsClosed();
            GetViewport().SetInputAsHandled();
            return;
        }

        // When settings is open but a popup or rebinding is active, don't
        // force-close settings — but also don't fall through to the pause-menu
        // toggle.  The ESC event needs to reach the OptionButton popup or
        // key-capture logic, and opening the pause menu behind the settings
        // panel would be confusing.
        if (_settingsMenu != null && GodotObject.IsInstanceValid(_settingsMenu))
        {
            return;
        }

        // Dismiss save/load dialog before toggling the pause menu so ESC
        // cancels out of the modal first rather than stacking pause on top.
        if (_saveLoadDialog != null && GodotObject.IsInstanceValid(_saveLoadDialog))
        {
            // If the save dialog has an active child confirmation (e.g. overwrite
            // prompt), dismiss only the child so the player stays in the save flow.
            if (_saveLoadDialog.HasActiveChildDialog)
            {
                _saveLoadDialog.DismissActiveChildDialog();
            }
            else
            {
                CleanupSaveDialogAndRestorePause();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_pauseMenuRestorePending)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_gameManager.IsInWorldInteraction)
        {
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_gameManager.IsInNpcInteraction)
        {
            // Don't consume the event — AcceptDialog-based NPC modals
            // (DialogueDialog, ShopDialog, HealDialog) rely on ESC reaching
            // them to emit Canceled/CloseRequested.  Just skip pause-menu
            // logic so the dialog can dismiss itself.
            return;
        }

        if (!_gameManager.IsInBattle)
        {
            if (_pauseMenuDialog != null && GodotObject.IsInstanceValid(_pauseMenuDialog) && _pauseMenuDialog.Visible)
            {
                CleanupPauseMenu();
            }
            else
            {
                ShowPauseMenu();
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        // In battle: close the battle dialog as an escape to unlock input
        if (_battleManager != null)
        {
            GD.Print("ESC pressed during battle - requesting battle dialog to close as escape");
            _battleManager.ForceCloseAsEscape();
            GetViewport().SetInputAsHandled();
        }
        else
        {
            // Fallback safety: if dialog reference missing, force-clear battle state
            GD.PrintErr("ESC pressed during battle but BattleManager is null - forcing EndBattle(escaped)");
            _gameManager.EndBattle(false); // treat as not won (escape); no enemy removal here
            UpdatePlayerUI();
            GetViewport().SetInputAsHandled();
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
        UpdateInteractionPrompt();
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
    
    private void OnNpcInteracted(Vector2I npcPosition)
    {
        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction) return;

        Vector2I tilemapPos = _gridMap.InternalGridToTilemapCoords(npcPosition);
        NpcSpawn foundSpawn = null;
        Node? currentFloorRoot = _gridMap.GetParent();

        foreach (Node n in GetTree().GetNodesInGroup("NpcSpawn"))
        {
            if (n is NpcSpawn spawn &&
                spawn.BelongsToFloor(currentFloorRoot) &&
                spawn.GridPosition == tilemapPos)
            {
                foundSpawn = spawn;
                break;
            }
        }

        if (foundSpawn == null)
        {
            GD.PushWarning($"[Game] NPC encountered at {npcPosition} but no NpcSpawn found at tilemap {tilemapPos}.");
            return;
        }

        var npcData = foundSpawn.GetNpcData();
        if (npcData == null) return;

        _gameManager.StartNpcInteraction();

        _npcInteractionController = new NpcInteractionController(
            _gameManager, GetNode("UI"), npcData, _gameManager.Player, _questFlags);
        _npcInteractionController.InteractionComplete += OnNpcInteractionComplete;
        try
        {
            _npcInteractionController.Begin();
        }
        catch (Exception ex)
        {
            GD.PushError($"[Game] NpcInteractionController.Begin() threw: {ex.Message}. Ending NPC interaction.");
            _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
            _npcInteractionController = null;
            _gameManager.EndNpcInteraction();
        }
    }

    private void OnPlayerFacingChanged(Vector2I facingDirection)
    {
        UpdateInteractionPrompt();
    }

    private async void OnTreasureBoxOpenRequested(Vector2I treasurePosition)
    {
        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
        {
            return;
        }

        var box = FindTreasureBoxAt(treasurePosition);
        if (box == null)
        {
            GD.PushWarning($"[Game] Treasure box requested at {treasurePosition} but no TreasureBoxSpawn was found.");
            return;
        }

        if (_gameManager.IsTreasureBoxOpened(box.TreasureBoxId) || box.IsOpened)
        {
            box.ApplyOpenedState(true);
            UpdateInteractionPrompt();
            return;
        }

        if (string.IsNullOrWhiteSpace(box.TreasureBoxId))
        {
            GD.PushWarning($"[Game] Treasure box at {treasurePosition} has no TreasureBoxId; skipping to prevent infinite farming.");
            return;
        }

        try
        {
            _gameManager.StartWorldInteraction();
            UpdateInteractionPrompt();
            await box.OpenAsync();
            if (!IsInsideTree() || !IsInstanceValid(_gameManager) || !IsInstanceValid(box) || !box.IsOpened)
            {
                return;
            }

            box.GrantRewardTo(_gameManager.Player);
            _gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
            _gridMap.ClearTreasureBoxCell(treasurePosition);
            _gameManager.NotifyPlayerStatsChanged();
        }
        finally
        {
            if (IsInstanceValid(_gameManager) && _gameManager.IsInWorldInteraction)
            {
                _gameManager.EndWorldInteraction();
            }

            if (IsInsideTree())
            {
                UpdateInteractionPrompt();
            }
        }
    }

    private TreasureBoxSpawn? FindTreasureBoxAt(Vector2I internalGridPosition)
    {
        if (_gridMap == null)
        {
            return null;
        }

        Vector2I tilemapPos = _gridMap.InternalGridToTilemapCoords(internalGridPosition);
        Node? currentFloorRoot = _gridMap.GetParent();

        foreach (Node n in GetTree().GetNodesInGroup("TreasureBoxSpawn"))
        {
            if (n is TreasureBoxSpawn box &&
                box.BelongsToFloor(currentFloorRoot) &&
                box.GridPosition == tilemapPos)
            {
                return box;
            }
        }

        return null;
    }

    private void EnsureInteractionPromptLabel()
    {
        if (_interactionPromptLabel != null && IsInstanceValid(_interactionPromptLabel))
        {
            return;
        }

        var gameUi = GetNodeOrNull<Control>("UI/GameUI");
        if (gameUi == null)
        {
            return;
        }

        _interactionPromptLabel = gameUi.GetNodeOrNull<Label>("InteractionPrompt");
        if (_interactionPromptLabel == null)
        {
            _interactionPromptLabel = new Label
            {
                Name = "InteractionPrompt",
                Text = "Open",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ZIndex = 20
            };
            _interactionPromptLabel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _interactionPromptLabel.OffsetLeft = 0;
            _interactionPromptLabel.OffsetRight = 0;
            _interactionPromptLabel.OffsetTop = -96;
            _interactionPromptLabel.OffsetBottom = -56;
            gameUi.AddChild(_interactionPromptLabel);
        }
    }

    private void UpdateInteractionPrompt()
    {
        EnsureInteractionPromptLabel();
        if (_interactionPromptLabel == null)
        {
            return;
        }

        _interactionPromptLabel.Text = "Open";

        if (_gridMap == null || _playerController == null || _gameManager == null)
        {
            _interactionPromptLabel.Visible = false;
            return;
        }

        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
        {
            _interactionPromptLabel.Visible = false;
            return;
        }

        Vector2I target = _gridMap.GetPlayerPosition() + _playerController.FacingDirection;
        var box = FindTreasureBoxAt(target);
        bool canOpen = box != null &&
                       !box.IsOpened &&
                       !box.IsOpening &&
                       !_gameManager.IsTreasureBoxOpened(box.TreasureBoxId);

        _interactionPromptLabel.Visible = canOpen;
    }

    private void OnNpcInteractionComplete()
    {
        if (_npcInteractionController != null)
        {
            _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
        }

        _gameManager.EndNpcInteraction();
        _npcInteractionController = null;
        UpdatePlayerUI();
    }

    private void OnNpcInteractionResetRequested()
    {
        if (_npcInteractionController != null)
        {
            _npcInteractionController.Finish();
            return;
        }

        if (_gameManager.IsInNpcInteraction)
        {
            _gameManager.EndNpcInteraction();
        }
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
            string enemyType = EncounterTables.SelectDungeonEnemyType(GD.Randf());
            return EncounterTables.CreateEnemyByType(enemyType) ?? Enemy.CreateDungeonGuardian();
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
            _battleManager.BattleFinished -= OnBattleFinished;
            _battleManager.Confirmed -= OnBattleDialogConfirmed;
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
        
        // Disconnect BattleFinished to prevent multiple calls.
        // Keep Confirmed connected so the dialog can still be cleaned up when user presses Continue.
        _battleManager.BattleFinished -= OnBattleFinished;
        
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
        // Guard against accessing null _gridMap when initialization was aborted
        if (_isAbortInitialization)
        {
            return;
        }

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
                    atkLabel.Text = $"ATK: {effectiveAttack} [color=#4CAF50](+{atkBonus})[/color]";
                }
                else
                {
                    atkLabel.Text = $"ATK: {effectiveAttack}";
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
                    defLabel.Text = $"DEF: {effectiveDefense} [color=#4CAF50](+{defBonus})[/color]";
                }
                else
                {
                    defLabel.Text = $"DEF: {effectiveDefense}";
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
                    spdLabel.Text = $"SPD: {effectiveSpeed} [color=#4CAF50](+{spdBonus})[/color]";
                }
                else
                {
                    spdLabel.Text = $"SPD: {effectiveSpeed}";
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
        if (!_floorManager.LoadFloor(floorIndex, playerPosition))
        {
            _hasPendingSaveSpawnValidation = false;
            _pendingSaveSpawnFloorIndex = -1;
            GD.PushError($"Save data corrupted: Failed to load floor index {floorIndex}.");
            ShowCorruptedSaveError();
        }
    }

    /// <summary>
    /// Shows the save menu dialog.
    /// </summary>
    private void ShowSaveMenu()
    {
        if (_gameManager.IsInNpcInteraction)
        {
            GD.PrintErr("Save blocked: NPC interaction in progress.");
            ShowSaveError("Cannot save during NPC interaction.");
            return;
        }

        if (_gameManager.IsInWorldInteraction)
        {
            GD.PrintErr("Save/load blocked: world interaction in progress.");
            ShowSaveError("Cannot save or load while opening treasure.");
            return;
        }

        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.SaveSlotSelected -= OnSaveSlotSelected;
            _saveLoadDialog.DialogClosed -= OnSaveDialogClosed;
            _saveLoadDialog.MainMenuRequested -= OnMainMenuRequested;
            _saveLoadDialog.QueueFree();
        }

        _saveLoadDialog = new SaveLoadDialog();
        GetNode("UI").AddChild(_saveLoadDialog);
        _saveLoadDialog.SaveSlotSelected += OnSaveSlotSelected;
        _saveLoadDialog.DialogClosed += OnSaveDialogClosed;
        _saveLoadDialog.MainMenuRequested += OnMainMenuRequested;
        _saveLoadDialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
    }

    private void OnSaveSlotSelected(int slot)
    {
        if (_gameManager.IsInNpcInteraction)
        {
            GD.PrintErr("Save blocked: NPC interaction in progress.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Cannot save during NPC interaction.");
            return;
        }

        if (_gameManager.IsInWorldInteraction)
        {
            GD.PrintErr("Save/load blocked: world interaction in progress.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Cannot save or load while opening treasure.");
            return;
        }

        // Defensive: re-check battle state in case a battle started while dialog was open
        if (_gameManager.IsInBattle)
        {
            GD.PrintErr("Save blocked: Battle in progress.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Cannot save during battle.");
            return;
        }

        // Prevent saving if player is defeated (CurrentHealth <= 0)
        // This can happen during the 2-second delay after battle loss before returning to menu
        if (_gameManager.Player != null && !_gameManager.Player.IsAlive)
        {
            GD.PrintErr("Save blocked: Player is defeated.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Cannot save while defeated.");
            return;
        }

        var saveData = _gameManager.CollectSaveData(_questFlags);
        if (saveData == null)
        {
            GD.PrintErr("Save failed: unable to collect save data.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Unable to collect save data.");
            return;
        }

        if (SaveManager.Instance == null)
        {
            GD.PushError("Save failed: SaveManager not initialized.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Save system unavailable.");
            return;
        }

        bool success = SaveManager.Instance.SaveGame(slot, saveData);
        if (success)
        {
            GD.Print($"Game saved to slot {slot}");
            CleanupSaveDialogAndRestorePause();
        }
        else
        {
            GD.PrintErr($"Save failed for slot {slot}.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Failed to save game.");
        }
    }

    private void OnSaveDialogClosed()
    {
        CleanupSaveDialogAndRestorePause();
    }

    private void OnMainMenuRequested()
    {
        GD.Print("Main menu requested from save dialog");
        _saveLoadFromPause = false;
        CleanupSaveDialog();
        CleanupPauseMenu();
        ReturnToMainMenu();
    }

    private void CleanupSaveDialog()
    {
        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.SaveSlotSelected -= OnSaveSlotSelected;
            _saveLoadDialog.LoadSlotSelected -= OnInGameLoadSlotSelected;
            _saveLoadDialog.DialogClosed -= OnSaveDialogClosed;
            _saveLoadDialog.MainMenuRequested -= OnMainMenuRequested;
            _saveLoadDialog.QueueFree();
            _saveLoadDialog = null;
        }
    }

    /// <summary>
    /// Cleans up the save/load dialog and restores the hidden pause menu
    /// if the save/load was opened from the pause menu.
    /// </summary>
    private void CleanupSaveDialogAndRestorePause()
    {
        bool fromPause = _saveLoadFromPause;
        _saveLoadFromPause = false;
        CleanupSaveDialog();
        if (fromPause && _pauseMenuDialog != null && GodotObject.IsInstanceValid(_pauseMenuDialog))
        {
            _pauseMenuDialog.PopupCentered();
        }
    }

    private void ShowPauseMenu()
    {
        _pauseMenuRestorePending = false;
        if (_pauseMenuDialog != null) CleanupPauseMenu();
        _pauseMenuDialog = new PauseMenuDialog();
        GetNode("UI").AddChild(_pauseMenuDialog);
        _pauseMenuDialog.ResumeRequested += OnPauseResumeRequested;
        _pauseMenuDialog.SaveRequested += OnPauseSaveRequested;
        _pauseMenuDialog.LoadRequested += OnPauseLoadRequested;
        _pauseMenuDialog.SettingsRequested += OnPauseSettingsRequested;
        _pauseMenuDialog.QuitToMenuRequested += OnPauseQuitRequested;
        _pauseMenuDialog.PopupCentered();
    }

    private void CleanupPauseMenu()
    {
        _pauseMenuRestorePending = false;
        if (_pauseMenuDialog == null) return;
        _pauseMenuDialog.ResumeRequested -= OnPauseResumeRequested;
        _pauseMenuDialog.SaveRequested -= OnPauseSaveRequested;
        _pauseMenuDialog.LoadRequested -= OnPauseLoadRequested;
        _pauseMenuDialog.SettingsRequested -= OnPauseSettingsRequested;
        _pauseMenuDialog.QuitToMenuRequested -= OnPauseQuitRequested;
        if (GodotObject.IsInstanceValid(_pauseMenuDialog))
            _pauseMenuDialog.QueueFree();
        _pauseMenuDialog = null;
    }

    private void OnPauseResumeRequested() => CleanupPauseMenu();

    private void OnPauseSaveRequested()
    {
        _saveLoadFromPause = true;
        _pauseMenuDialog?.Hide();
        ShowSaveMenu();
    }

    private void OnPauseLoadRequested()
    {
        _saveLoadFromPause = true;
        _pauseMenuDialog?.Hide();
        ShowLoadMenu();
    }

    private void OnPauseSettingsRequested()
    {
        if (_settingsMenu != null) return;
        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        if (scene == null) { GD.PushError("[Game] SettingsMenu.tscn not found."); return; }
        _pauseMenuDialog?.Hide();
        _settingsMenu = scene.Instantiate<SettingsMenuController>();
        _settingsMenu.Closed += OnPauseSettingsClosed;
        GetNode("UI").AddChild(_settingsMenu);
        _settingsMenu.OpenSettings(showOverlay: false);
    }

    private void OnPauseSettingsClosed()
    {
        if (_settingsMenu != null)
        {
            _settingsMenu.Closed -= OnPauseSettingsClosed;
            _settingsMenu.QueueFree();
            _settingsMenu = null;
        }

        if (_pauseMenuDialog != null && GodotObject.IsInstanceValid(_pauseMenuDialog) && !_pauseMenuRestorePending)
        {
            _pauseMenuRestorePending = true;
            CallDeferred(nameof(RestorePauseMenuAfterSettings));
        }
    }

    private void RestorePauseMenuAfterSettings()
    {
        _pauseMenuRestorePending = false;
        if (_pauseMenuDialog != null && GodotObject.IsInstanceValid(_pauseMenuDialog))
        {
            _pauseMenuDialog.PopupCentered();
        }
    }

    private void OnPauseQuitRequested()
    {
        CleanupPauseMenu();
        ReturnToMainMenu();
    }

    private void ShowLoadMenu()
    {
        if (_gameManager.IsInNpcInteraction)
        {
            GD.PrintErr("Load blocked: NPC interaction in progress.");
            ShowSaveError("Cannot load during NPC interaction.", "Load Failed");
            return;
        }

        if (_gameManager.IsInWorldInteraction)
        {
            GD.PrintErr("Save/load blocked: world interaction in progress.");
            ShowSaveError("Cannot save or load while opening treasure.", "Load Failed");
            return;
        }

        CleanupSaveDialog();

        _saveLoadDialog = new SaveLoadDialog();
        GetNode("UI").AddChild(_saveLoadDialog);
        _saveLoadDialog.LoadSlotSelected += OnInGameLoadSlotSelected;
        _saveLoadDialog.DialogClosed += OnSaveDialogClosed;
        _saveLoadDialog.MainMenuRequested += OnMainMenuRequested;
        _saveLoadDialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
    }

    private void OnInGameLoadSlotSelected(int slot)
    {
        if (_gameManager.IsInWorldInteraction)
        {
            GD.PrintErr("Save/load blocked: world interaction in progress.");
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Cannot save or load while opening treasure.", "Load Failed");
            return;
        }

        var saveData = slot == 3
            ? SaveManager.Instance?.LoadAutosave()
            : SaveManager.Instance?.LoadGame(slot);

        if (saveData == null || SaveManager.Instance == null)
        {
            CleanupSaveDialogAndRestorePause();
            ShowSaveError("Failed to load save file.", "Load Failed");
            return;
        }

        SaveManager.Instance.PendingLoadData = saveData;
        _saveLoadFromPause = false;
        CleanupSaveDialog();
        GetTree().ChangeSceneToFile("res://scenes/game/Game.tscn");
    }

    private void ShowSaveError(string message, string title = "Save Failed")
    {
        // Dismiss any previous error popup before creating a new one.
        if (_activeErrorPopup != null && IsInstanceValid(_activeErrorPopup))
        {
            _activeErrorPopup.QueueFree();
            _activeErrorPopup = null;
        }

        var popup = new AcceptDialog();
        popup.Title = title;
        popup.DialogText = message;
        GetNode("UI").AddChild(popup);
        popup.PopupCentered();
        _activeErrorPopup = popup;

        // Clean up when confirmed or canceled
        popup.Confirmed += () =>
        {
            if (IsInstanceValid(popup))
                popup.QueueFree();
            if (_activeErrorPopup == popup)
                _activeErrorPopup = null;
        };
        popup.Canceled += () =>
        {
            if (IsInstanceValid(popup))
                popup.QueueFree();
            if (_activeErrorPopup == popup)
                _activeErrorPopup = null;
        };
    }

    private void ShowCorruptedSaveError()
    {
        if (_hasShownCorruptedSaveError)
        {
            return;
        }
        _hasShownCorruptedSaveError = true;

        var popup = new AcceptDialog();
        popup.Title = "Load Failed";
        popup.DialogText = "Save file is corrupted or invalid.\nReturning to main menu.";
        GetNode("UI").AddChild(popup);
        popup.PopupCentered();

        // Disable input processing to prevent additional interactions during error state
        SetProcessInput(false);

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
            _gridMap.NpcInteracted -= OnNpcInteracted;
            _gridMap.TreasureBoxOpenRequested -= OnTreasureBoxOpenRequested;
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
            _gridMap.NpcInteracted += OnNpcInteracted;
            _gridMap.TreasureBoxOpenRequested += OnTreasureBoxOpenRequested;

            if (_hasPendingSaveSpawnValidation)
            {
                if (_pendingSaveSpawnFloorIndex == _floorManager.CurrentFloorIndex)
                {
                    CallDeferred(nameof(ValidatePendingSaveSpawnPosition), _gridMap);
                }
                else
                {
                    GD.PushWarning($"Save validation skipped: Loaded floor {_floorManager.CurrentFloorIndex} while waiting for floor {_pendingSaveSpawnFloorIndex}.");
                    _hasPendingSaveSpawnValidation = false;
                    _pendingSaveSpawnFloorIndex = -1;
                }
            }
        }

        // Setup player display for this floor
        CallDeferred(nameof(SetupPlayerDisplay));

        // Update camera position
        CallDeferred(nameof(SetInitialCameraPosition));
        CallDeferred(nameof(UpdateInteractionPrompt));

        GD.Print($"✅ Floor '{floorDef.FloorName}' ready for gameplay");
    }

    private void ValidatePendingSaveSpawnPosition(GridMap gridMap)
    {
        if (!_hasPendingSaveSpawnValidation)
        {
            return;
        }

        _hasPendingSaveSpawnValidation = false;
        _pendingSaveSpawnFloorIndex = -1;

        if (gridMap == null)
        {
            GD.PushError("Save data corrupted: Floor loaded without a valid GridMap.");
            ShowCorruptedSaveError();
            return;
        }

        Vector2I actualPosition = gridMap.GetPlayerPosition();
        if (actualPosition != _pendingSaveSpawnPosition)
        {
            GD.PushError($"Save data corrupted: Player position ({_pendingSaveSpawnPosition.X}, {_pendingSaveSpawnPosition.Y}) is invalid for floor '{_floorManager.CurrentFloorDefinition?.FloorName ?? "Unknown"}'.");
            ShowCorruptedSaveError();
        }
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

    public override void _ExitTree()
    {
        // Disconnect all signal subscriptions to prevent memory leaks
        if (_gameManager != null)
        {
            _gameManager.BattleStarted -= OnBattleStarted;
            _gameManager.BattleEnded -= OnBattleEnded;
            _gameManager.PlayerStatsChanged -= OnPlayerStatsChanged;
            _gameManager.NpcInteractionResetRequested -= OnNpcInteractionResetRequested;
            _gameManager.QuestFlagProvider = null;
        }

        if (_floorManager != null)
        {
            _floorManager.FloorLoaded -= OnFloorLoaded;
            _floorManager.FloorChanged -= OnFloorChanged;
        }

        if (_gridMap != null)
        {
            _gridMap.EnemyEncountered -= OnEnemyEncountered;
            _gridMap.PlayerMoved -= OnPlayerMoved;
            _gridMap.NpcInteracted -= OnNpcInteracted;
            _gridMap.TreasureBoxOpenRequested -= OnTreasureBoxOpenRequested;
        }

        if (_playerController != null)
        {
            _playerController.FacingChanged -= OnPlayerFacingChanged;
        }

        if (_npcInteractionController != null)
        {
            _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
            _npcInteractionController.Finish();
            _npcInteractionController = null;
        }

        // Clean up save dialog if it exists
        if (_saveLoadDialog != null)
        {
            _saveLoadDialog.SaveSlotSelected -= OnSaveSlotSelected;
            _saveLoadDialog.LoadSlotSelected -= OnInGameLoadSlotSelected;
            _saveLoadDialog.DialogClosed -= OnSaveDialogClosed;
            _saveLoadDialog.MainMenuRequested -= OnMainMenuRequested;
            _saveLoadDialog.QueueFree();
            _saveLoadDialog = null;
        }

        // Clean up error popup if it exists
        if (_activeErrorPopup != null && IsInstanceValid(_activeErrorPopup))
        {
            _activeErrorPopup.QueueFree();
            _activeErrorPopup = null;
        }

        if (_battleManager != null)
        {
            _battleManager.BattleFinished -= OnBattleFinished;
            _battleManager.Confirmed -= OnBattleDialogConfirmed;
            _battleManager.QueueFree();
            _battleManager = null;
        }

        if (_pauseMenuDialog != null)
        {
            _pauseMenuDialog.ResumeRequested -= OnPauseResumeRequested;
            _pauseMenuDialog.SaveRequested -= OnPauseSaveRequested;
            _pauseMenuDialog.LoadRequested -= OnPauseLoadRequested;
            _pauseMenuDialog.SettingsRequested -= OnPauseSettingsRequested;
            _pauseMenuDialog.QuitToMenuRequested -= OnPauseQuitRequested;
            if (GodotObject.IsInstanceValid(_pauseMenuDialog))
                _pauseMenuDialog.QueueFree();
            _pauseMenuDialog = null;
        }

        if (_settingsMenu != null)
        {
            _settingsMenu.Closed -= OnPauseSettingsClosed;
            if (GodotObject.IsInstanceValid(_settingsMenu))
                _settingsMenu.QueueFree();
            _settingsMenu = null;
        }
    }
}
