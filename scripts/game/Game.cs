using Godot;
using System;
using System.Collections.Generic;

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
    private ExplorationHudController _explorationHud = null!;
    private Camera2D _camera;
    private BattleManager? _battleManager;
    private Vector2I _lastEnemyPosition; // Store enemy position for after battle
    private NpcInteractionController _npcInteractionController;
    private PuzzleTrapController? _puzzleTrapController;
    private PuzzleRiddleScreenController? _puzzleRiddleScreen;
    private UIScreenHandle? _puzzleRiddleHandle;
    private PuzzleRiddleSpawn? _activePuzzleRiddle;
    private readonly System.Collections.Generic.HashSet<string> _questFlags = new();
    private PlayerDisplay _playerDisplay; // Visual sprite for player when using baked TileMaps
    private InventoryMenuController _inventoryMenu;
    private bool _isAbortInitialization; // Set when save corruption causes initialization abort
    private bool _hasPendingSaveSpawnValidation;
    private Vector2I _pendingSaveSpawnPosition;
    private int _pendingSaveSpawnFloorIndex = -1;
    private bool _hasShownCorruptedSaveError;

    private SceneTreeTimer? _defeatReturnTimer;
    private Action? _defeatReturnHandler;
    private UIScreenHost? _screenHost;
    private UIScreenHandle? _inventoryHandle;
    private UIScreenHandle? _battleHandle;
    private UIScreenHandle? _pauseHandle;
    private PauseScreenController? _pauseScreen;
    private UIScreenHandle? _hostedSettingsHandle;
    private SettingsMenuController? _hostedSettingsMenu;
    private UIScreenHandle? _hostedSaveLoadHandle;
    private SaveLoadScreenController? _hostedSaveLoadScreen;
    private UIScreenHandle? _hostedPromptHandle;
    private SiriusPrompt? _hostedPrompt;
    private Action? _hostedPromptPrimaryAction;
    private bool _presentationGameplayBlocked;
    private static readonly IReadOnlySet<StringName> GameplayCoreCancelActions =
        new HashSet<StringName> { "pause_menu", "ui_cancel" };
    private const string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";
    private const string GameScenePath = "res://scenes/game/Game.tscn";
    private string? _pendingScenePath;
    private bool _sceneChangeCommitted;

    protected virtual double DefeatReturnDelaySeconds => 2.0;

    private bool IsGameplayInputSuppressed() =>
        _presentationGameplayBlocked ||
        _gameManager.IsInBattle ||
        _gameManager.IsInNpcInteraction ||
        _gameManager.IsInWorldInteraction;

    public override void _EnterTree()
    {
        _screenHost = GetNodeOrNull<UIScreenHost>("UI/UIScreenHost");
        var gameUi = GetNodeOrNull<Control>("UI/GameUI");

        if (_screenHost != null && gameUi != null)
        {
            _screenHost.Configure(new UIScreenHostOptions
            {
                HudRoot = gameUi,
                CoreCancelActions = GameplayCoreCancelActions,
                RootCancelFallback = HandleGameplayRootCancel,
                GameplayInputBlockChanged = blocked =>
                {
                    _presentationGameplayBlocked = blocked;
                    if (IsNodeReady())
                        UpdateInteractionPrompt();
                }
            });
        }

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
        _playerController.GameplayInputSuppressedProvider = IsGameplayInputSuppressed;
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
        _puzzleTrapController = new PuzzleTrapController(_gameManager);

        // Bind the production HUD before connecting signals: LoadFromSaveData
        // may emit PlayerStatsChanged during initialization.
        _explorationHud = GetNode<ExplorationHudController>(
            "UI/GameUI/ExplorationHud");

        // Connect signals AFTER the HUD is initialized, so signals are always connected even if save is corrupted
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

        if (_floorManager.CurrentFloorDefinition != null)
        {
            _explorationHud.ShowAreaTitle(
                _floorManager.CurrentFloorDefinition.FloorName);
        }

        _explorationHud.ShowSessionHint("Move with WASD or Arrow Keys");
        
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

        _inventoryMenu.CloseRequested += OnInventoryCloseRequested;
        _inventoryMenu.Hide();
    }

    private bool TryOpenInventory(UIScreenHandle? parent)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            _inventoryMenu == null || !GodotObject.IsInstanceValid(_inventoryMenu))
        {
            return false;
        }

        if (_inventoryHandle.HasValue)
        {
            if (_screenHost.IsActive(_inventoryHandle.Value))
                return false;

            ClearInventoryHandle();
        }

        var hasParent = parent.HasValue;
        var spec = new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Inventory,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = hasParent ? UIProcessPolicy.Always : UIProcessPolicy.WhenPaused,
            Parent = hasParent ? parent : null,
            PauseTree = !hasParent,
            BlockGameplayInput = !hasParent,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
            InitialFocus = () => _inventoryMenu.InitialFocusTarget,
            SetPresented = visible =>
            {
                if (visible) _inventoryMenu.OpenMenu();
                else _inventoryMenu.CloseMenu();
            },
            Cleanup = _ => ClearInventoryHandle(),
            NodeLifetime = UINodeLifetime.External
        };

        var result = _screenHost.TryPresent(_inventoryMenu, spec);
        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
            return false;

        _inventoryHandle = result.Handle.Value;
        _inventoryMenu.OpenMenu();
        return true;
    }

    private bool TryCloseInventory(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_inventoryHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_inventoryHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
            ClearInventoryHandle();

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void OnInventoryCloseRequested() =>
        TryCloseInventory(UIScreenCloseReason.ExplicitAction);

    private void ClearInventoryHandle() => _inventoryHandle = null;

    private void OnHostedPauseInventoryRequested()
    {
        if (_screenHost != null && GodotObject.IsInstanceValid(_screenHost) &&
            _pauseHandle.HasValue && _screenHost.IsActive(_pauseHandle.Value))
        {
            TryOpenInventory(_pauseHandle);
        }
    }

    private void OnHostedPauseSaveRequested() =>
        TryOpenHostedSaveLoad(SaveLoadMode.Save);

    private void OnHostedPauseLoadRequested() =>
        TryOpenHostedSaveLoad(SaveLoadMode.Load);

    private void OnHostedPauseSettingsRequested() => TryOpenHostedSettings();

    private void OnHostedPauseReturnToTitleRequested()
    {
        if (!_pauseHandle.HasValue)
            return;

        TryOpenHostedPrompt(
            SiriusPromptVariant.DestructiveConfirmation,
            "Return to Title?",
            "Unsaved progress will be lost.",
            "Return to Title",
            onPrimary: ReturnToMainMenu,
            parent: _pauseHandle);
    }

    private bool TryOpenHostedSettings()
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_pauseHandle.HasValue || !_screenHost.IsActive(_pauseHandle.Value))
        {
            return false;
        }

        if (_hostedSettingsHandle.HasValue)
        {
            if (_screenHost.IsActive(_hostedSettingsHandle.Value))
                return false;

            if (_hostedSettingsMenu != null)
                ClearHostedSettings(_hostedSettingsMenu);
            else
                _hostedSettingsHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.Settings))
            return false;

        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        if (scene == null)
        {
            GD.PushError("[Game] SettingsMenu.tscn not found.");
            return false;
        }

        var settings = scene.Instantiate<SettingsMenuController>();
        if (settings == null)
        {
            GD.PushError("[Game] Failed to instantiate SettingsMenuController.");
            return false;
        }

        settings.Closed += OnHostedSettingsClosed;
        var result = _screenHost.TryPresent(settings, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Settings,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            Parent = _pauseHandle,
            PauseTree = false,
            BlockGameplayInput = false,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InterceptCancel = _ =>
                settings.IsRebinding || settings.IsPopupOpen
                    ? UIInputInterception.ReserveForNativeHandler
                    : UIInputInterception.DeferToPolicy,
            InitialFocus = () => settings.InitialFocusTarget,
            SetPresented = visible =>
            {
                if (visible) settings.OpenSettings(showOverlay: false);
                else settings.Hide();
            },
            Cleanup = _ => ClearHostedSettings(settings),
            NodeLifetime = UINodeLifetime.QueueFree
        });
        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            settings.Closed -= OnHostedSettingsClosed;
            settings.QueueFree();
            return false;
        }

        _hostedSettingsHandle = result.Handle.Value;
        _hostedSettingsMenu = settings;
        settings.OpenSettings(showOverlay: false);
        return true;
    }

    private void OnHostedSettingsClosed() =>
        TryCloseHostedSettings(UIScreenCloseReason.ExplicitAction);

    private bool TryCloseHostedSettings(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_hostedSettingsHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_hostedSettingsHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_hostedSettingsMenu != null)
                ClearHostedSettings(_hostedSettingsMenu);
            else
                _hostedSettingsHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedSettings(SettingsMenuController settings)
    {
        if (GodotObject.IsInstanceValid(settings))
            settings.Closed -= OnHostedSettingsClosed;

        if (ReferenceEquals(_hostedSettingsMenu, settings))
        {
            _hostedSettingsHandle = null;
            _hostedSettingsMenu = null;
        }
    }

    private bool TryOpenHostedSaveLoad(SaveLoadMode mode)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_pauseHandle.HasValue || !_screenHost.IsActive(_pauseHandle.Value))
        {
            return false;
        }

        if (_hostedSaveLoadHandle.HasValue)
        {
            if (_screenHost.IsActive(_hostedSaveLoadHandle.Value))
                return false;

            if (_hostedSaveLoadScreen != null)
                ClearHostedSaveLoadScreen(_hostedSaveLoadScreen);
            else
                _hostedSaveLoadHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.SaveLoad))
            return false;

        var scene = GD.Load<PackedScene>("res://scenes/ui/SaveLoadScreen.tscn");
        if (scene == null)
        {
            GD.PushError("[Game] SaveLoadScreen.tscn not found.");
            return false;
        }

        var screen = scene.Instantiate<SaveLoadScreenController>();
        if (screen == null)
        {
            GD.PushError("[Game] Failed to instantiate SaveLoadScreenController.");
            return false;
        }

        // Mode is part of the screen's presentation contract and must be set
        // before the host attaches the scene-authored view.
        screen.Mode = mode;
        screen.SaveSlotSelected += OnHostedSaveSlotSelected;
        screen.LoadSlotSelected += OnHostedLoadSlotSelected;
        screen.OverwriteRequested += OnHostedOverwriteRequested;
        screen.Closed += OnHostedSaveLoadClosed;
        screen.MainMenuRequested += OnHostedSaveLoadMainMenuRequested;

        var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.SaveLoad,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            Parent = _pauseHandle,
            PauseTree = false,
            BlockGameplayInput = false,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => screen.InitialFocusTarget,
            SetPresented = visible => screen.Visible = visible,
            Cleanup = _ => ClearHostedSaveLoadScreen(screen),
            NodeLifetime = UINodeLifetime.QueueFree
        });
        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            ClearHostedSaveLoadScreen(screen);
            if (GodotObject.IsInstanceValid(screen))
                screen.QueueFree();
            return false;
        }

        _hostedSaveLoadHandle = result.Handle.Value;
        _hostedSaveLoadScreen = screen;
        return true;
    }

    private void OnHostedOverwriteRequested(int slot)
    {
        if (!_hostedSaveLoadHandle.HasValue)
            return;

        TryOpenHostedPrompt(
            SiriusPromptVariant.DestructiveConfirmation,
            "Overwrite Save?",
            $"Slot {slot + 1} already contains save data. Overwrite it?",
            "Overwrite",
            onPrimary: () => OnHostedSaveSlotSelected(slot),
            parent: _hostedSaveLoadHandle);
    }

    private bool TryOpenHostedPrompt(
        SiriusPromptVariant variant,
        string title,
        string message,
        string primaryActionText,
        Action? onPrimary = null,
        string cancelActionText = "Cancel",
        UIScreenHandle? parent = null,
        bool blockGameplayInput = false)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            _sceneChangeCommitted)
        {
            return false;
        }

        if (_hostedPromptHandle.HasValue)
        {
            if (_screenHost.IsActive(_hostedPromptHandle.Value))
                return false;

            if (_hostedPrompt != null)
                ClearHostedPrompt(_hostedPrompt);
            else
                _hostedPromptHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.Prompt))
            return false;

        var scene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusPrompt.tscn");
        if (scene == null)
        {
            GD.PushError("[Game] SiriusPrompt.tscn not found.");
            return false;
        }

        var prompt = scene.Instantiate<SiriusPrompt>();
        if (prompt == null)
        {
            GD.PushError("[Game] Failed to instantiate SiriusPrompt.");
            return false;
        }

        prompt.Configure(variant, title, message, primaryActionText, cancelActionText);
        _hostedPromptPrimaryAction = onPrimary;
        prompt.PrimaryRequested += OnHostedPromptPrimaryRequested;
        prompt.CancelRequested += OnHostedPromptCancelRequested;

        var result = _screenHost.TryPresent(prompt, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Prompt,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            Parent = parent,
            ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt,
            PauseTree = false,
            BlockGameplayInput = blockGameplayInput,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Inherit,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Consume,
            InterceptCancel = _ =>
            {
                prompt.RequestCancel();
                return UIInputInterception.ConsumeHere;
            },
            InitialFocus = () => prompt.InitialFocusTarget,
            Cleanup = _ => ClearHostedPrompt(prompt),
            NodeLifetime = UINodeLifetime.QueueFree
        });
        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            prompt.PrimaryRequested -= OnHostedPromptPrimaryRequested;
            prompt.CancelRequested -= OnHostedPromptCancelRequested;
            _hostedPromptPrimaryAction = null;
            prompt.QueueFree();
            return false;
        }

        _hostedPromptHandle = result.Handle.Value;
        _hostedPrompt = prompt;
        return true;
    }

    private void OnHostedPromptPrimaryRequested()
    {
        var action = _hostedPromptPrimaryAction;
        TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction);
        action?.Invoke();
    }

    private void OnHostedPromptCancelRequested()
    {
        TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction);
    }

    private bool TryCloseHostedPrompt(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_hostedPromptHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_hostedPromptHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_hostedPrompt != null)
                ClearHostedPrompt(_hostedPrompt);
            else
                _hostedPromptHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedPrompt(SiriusPrompt prompt)
    {
        if (GodotObject.IsInstanceValid(prompt))
        {
            prompt.PrimaryRequested -= OnHostedPromptPrimaryRequested;
            prompt.CancelRequested -= OnHostedPromptCancelRequested;
        }

        if (ReferenceEquals(_hostedPrompt, prompt))
        {
            _hostedPromptHandle = null;
            _hostedPrompt = null;
            _hostedPromptPrimaryAction = null;
        }
    }

    private void OnHostedSaveSlotSelected(int slot)
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

        if (_gameManager.IsInBattle)
        {
            GD.PrintErr("Save blocked: Battle in progress.");
            ShowSaveError("Cannot save during battle.");
            return;
        }

        if (_gameManager.Player != null && !_gameManager.Player.IsAlive)
        {
            GD.PrintErr("Save blocked: Player is defeated.");
            ShowSaveError("Cannot save while defeated.");
            return;
        }

        var saveData = _gameManager.CollectSaveData(_questFlags);
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

        var success = SaveManager.Instance.SaveGame(slot, saveData);
        if (success)
        {
            GD.Print($"Game saved to slot {slot}");
            TryCloseHostedSaveLoad(UIScreenCloseReason.ExplicitAction);
        }
        else
        {
            GD.PrintErr($"Save failed for slot {slot}.");
            ShowSaveError("Failed to save game.");
        }
    }

    private void OnHostedLoadSlotSelected(int slot)
    {
        if (_gameManager.IsInWorldInteraction)
        {
            GD.PrintErr("Save/load blocked: world interaction in progress.");
            ShowSaveError("Cannot save or load while opening treasure.", "Load Failed");
            return;
        }

        var saveData = slot == 3
            ? SaveManager.Instance?.LoadAutosave()
            : SaveManager.Instance?.LoadGame(slot);

        if (saveData == null || SaveManager.Instance == null)
        {
            ShowSaveError("Failed to load save file.", "Load Failed");
            return;
        }

        SaveManager.Instance.PendingLoadData = saveData;
        TryCloseHostedSaveLoad(UIScreenCloseReason.ExplicitAction);
        RequestSceneChange(GameScenePath);
    }

    private void OnHostedSaveLoadClosed() =>
        TryCloseHostedSaveLoad(UIScreenCloseReason.ExplicitAction);

    private void OnHostedSaveLoadMainMenuRequested() => ReturnToMainMenu();

    private bool TryCloseHostedSaveLoad(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_hostedSaveLoadHandle.HasValue)
        {
            return false;
        }

        var result = _screenHost.TryClose(_hostedSaveLoadHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_hostedSaveLoadScreen != null)
                ClearHostedSaveLoadScreen(_hostedSaveLoadScreen);
            else
                _hostedSaveLoadHandle = null;
        }

        return result.Status == UIScreenCloseStatus.Closed;
    }

    private void ClearHostedSaveLoadScreen(SaveLoadScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.SaveSlotSelected -= OnHostedSaveSlotSelected;
            screen.LoadSlotSelected -= OnHostedLoadSlotSelected;
            screen.OverwriteRequested -= OnHostedOverwriteRequested;
            screen.Closed -= OnHostedSaveLoadClosed;
            screen.MainMenuRequested -= OnHostedSaveLoadMainMenuRequested;
        }

        if (ReferenceEquals(_hostedSaveLoadScreen, screen))
        {
            _hostedSaveLoadHandle = null;
            _hostedSaveLoadScreen = null;
        }
    }

    private bool TryOpenPause()
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
            return false;

        if (_pauseHandle.HasValue)
        {
            if (_screenHost.IsActive(_pauseHandle.Value))
                return false;

            if (_pauseScreen != null)
                ClearPausePresentation(_pauseScreen);
            else
                _pauseHandle = null;
        }

        if (_screenHost.IsKindActive(UIScreenKinds.Pause))
            return false;

        var scene = GD.Load<PackedScene>("res://scenes/ui/PauseScreen.tscn");
        if (scene == null)
        {
            GD.PushError("Failed to load PauseScreen scene!");
            return false;
        }

        var screen = scene.Instantiate<PauseScreenController>();
        if (screen == null)
        {
            GD.PushError("Failed to instantiate PauseScreenController!");
            return false;
        }

        screen.ResumeRequested += OnHostedPauseResumeRequested;
        screen.InventoryRequested += OnHostedPauseInventoryRequested;
        screen.SaveRequested += OnHostedPauseSaveRequested;
        screen.LoadRequested += OnHostedPauseLoadRequested;
        screen.SettingsRequested += OnHostedPauseSettingsRequested;
        screen.ReturnToTitleRequested += OnHostedPauseReturnToTitleRequested;
        var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Pause,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.WhenPaused,
            PauseTree = true,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => screen.InitialFocusTarget,
            Cleanup = _ => ClearPausePresentation(screen),
            NodeLifetime = UINodeLifetime.QueueFree
        });
        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            ClearPausePresentation(screen);
            screen.QueueFree();
            return false;
        }

        _pauseHandle = result.Handle.Value;
        _pauseScreen = screen;
        return true;
    }

    private void OnHostedPauseResumeRequested()
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_pauseHandle.HasValue)
        {
            return;
        }

        var result = _screenHost.TryClose(
            _pauseHandle.Value,
            UIScreenCloseReason.ExplicitAction);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_pauseScreen != null)
                ClearPausePresentation(_pauseScreen);
            else
                _pauseHandle = null;
        }
    }

    private void ClearPausePresentation(PauseScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.ResumeRequested -= OnHostedPauseResumeRequested;
            screen.InventoryRequested -= OnHostedPauseInventoryRequested;
            screen.SaveRequested -= OnHostedPauseSaveRequested;
            screen.LoadRequested -= OnHostedPauseLoadRequested;
            screen.SettingsRequested -= OnHostedPauseSettingsRequested;
            screen.ReturnToTitleRequested -= OnHostedPauseReturnToTitleRequested;
        }

        if (ReferenceEquals(_pauseScreen, screen))
        {
            _pauseHandle = null;
            _pauseScreen = null;
        }
    }

    public override void _Input(InputEvent @event)
    {
        // Handle inventory toggle (I key)
        if (@event.IsActionPressed("toggle_inventory"))
        {
            if (_inventoryMenu != null && !_gameManager.IsInBattle && !_gameManager.IsInNpcInteraction &&
                !_gameManager.IsInWorldInteraction &&
                (!_presentationGameplayBlocked || _inventoryHandle.HasValue))
            {
                var changed = _inventoryHandle.HasValue
                    ? TryCloseInventory(UIScreenCloseReason.ExplicitAction)
                    : TryOpenInventory(parent: null);
                if (changed)
                {
                    GetViewport().SetInputAsHandled();
                    return;
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

        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
        {
            GD.PushError("[Game] Cannot start NPC interaction without UIScreenHost.");
            return;
        }

        _gameManager.StartNpcInteraction();
        UpdateInteractionPrompt();

        _npcInteractionController = new NpcInteractionController(
            _gameManager,
            _screenHost,
            npcData,
            _gameManager.Player,
            _questFlags);
        _npcInteractionController.InteractionComplete += OnNpcInteractionComplete;
        try
        {
            _npcInteractionController.Begin();
        }
        catch (Exception ex)
        {
            GD.PushError($"[Game] NpcInteractionController.Begin() threw: {ex.Message}. Ending NPC interaction.");
            if (_npcInteractionController != null)
                _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
            _npcInteractionController = null;
            EndNpcInteractionIfActive();
            UpdateInteractionPrompt();
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
        catch (Exception ex)
        {
            GD.PushError($"[Game] Exception during treasure box opening at {treasurePosition}: {ex}");
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

    private void OnTrapTileTriggered(Vector2I trapPosition)
    {
        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
        {
            return;
        }

        var trap = FindTrapTileAt(trapPosition);
        if (trap == null)
        {
            GD.PushWarning($"[Game] Trap triggered at {trapPosition} but no TrapTileSpawn was found.");
            return;
        }

        if (_gameManager.IsPuzzleSolved(trap.PuzzleId))
        {
            return;
        }

        ApplyPuzzleDamage(trap.Damage);
        ApplyTrapStatusEffect(trap);
        _gameManager.NotifyPlayerStatsChanged();
    }

    private void OnPuzzleInteractionRequested(Vector2I puzzlePosition)
    {
        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
        {
            return;
        }

        var puzzleSwitch = FindPuzzleSwitchAt(puzzlePosition);
        if (puzzleSwitch != null)
        {
            HandlePuzzleSwitch(puzzleSwitch);
            return;
        }

        var riddle = FindPuzzleRiddleAt(puzzlePosition);
        if (riddle != null)
        {
            OpenPuzzleRiddle(riddle);
        }
    }

    private void HandlePuzzleSwitch(PuzzleSwitchSpawn puzzleSwitch)
    {
        if (_puzzleTrapController == null || string.IsNullOrWhiteSpace(puzzleSwitch.PuzzleId))
        {
            return;
        }

        try
        {
            _gameManager.StartWorldInteraction();
            bool activated = _puzzleTrapController.ActivateSwitch(puzzleSwitch.PuzzleId);
            if (!activated)
            {
                GD.Print($"[Game] Switch '{puzzleSwitch.Name}' did not activate (already solved, armed, or blank ID).");
            }
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

    private void OpenPuzzleRiddle(PuzzleRiddleSpawn riddle)
    {
        if (_puzzleTrapController == null || string.IsNullOrWhiteSpace(riddle.PuzzleId))
        {
            return;
        }

        if (_gameManager.IsPuzzleSolved(riddle.PuzzleId))
        {
            UpdateInteractionPrompt();
            return;
        }

        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
        {
            GD.PushError("[Game] UIScreenHost is unavailable; refusing to open puzzle riddle.");
            return;
        }

        var scene = GD.Load<PackedScene>("res://scenes/ui/PuzzleRiddleScreen.tscn");
        if (scene == null)
        {
            GD.PushError("[Game] PuzzleRiddleScreen.tscn not found.");
            return;
        }

        var screen = scene.Instantiate<PuzzleRiddleScreenController>();
        if (screen == null || !screen.TryOpenRiddle(riddle))
        {
            GD.PushError($"[Game] Failed to open puzzle riddle '{riddle.RiddleId}'.");
            if (screen != null && IsInstanceValid(screen))
            {
                screen.Free();
            }

            return;
        }

        screen.ChoiceSelected += OnPuzzleRiddleChoiceSelected;
        screen.PuzzleRiddleClosed += OnPuzzleRiddleClosed;

        _activePuzzleRiddle = riddle;
        _gameManager.StartWorldInteraction();
        UpdateInteractionPrompt();

        var spec = new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.PuzzleRiddle,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Consume,
            InitialFocus = () => screen.InitialFocusTarget,
            InterceptCancel = _ =>
            {
                screen.RequestCancel();
                return UIInputInterception.ConsumeHere;
            },
            Cleanup = _ => ClearPuzzleRiddlePresentation(screen),
            NodeLifetime = UINodeLifetime.QueueFree
        };

        UIScreenOpenResult openResult;
        try
        {
            openResult = _screenHost.TryPresent(screen, spec);
        }
        catch (Exception ex)
        {
            GD.PushError($"[Game] Failed to host puzzle riddle '{riddle.RiddleId}': {ex}");
            ClearRejectedPuzzleRiddleCandidate(screen);
            EndWorldInteractionIfActive();
            UpdateInteractionPrompt();
            return;
        }

        if (openResult.Status != UIScreenOpenStatus.Opened || !openResult.Handle.HasValue)
        {
            ClearRejectedPuzzleRiddleCandidate(screen);
            EndWorldInteractionIfActive();
            UpdateInteractionPrompt();
            return;
        }

        if (!_screenHost.IsActive(openResult.Handle.Value))
        {
            // A publication subscriber may already have closed the committed entry;
            // its Cleanup callback ran ClearPuzzleRiddlePresentation, but that only
            // resets _activePuzzleRiddle when _puzzleRiddleScreen matches — and the
            // commit assignment below has not run yet, so the match fails. Clear the
            // active riddle directly when no committed screen owns it, and only
            // ensure the world latch is down.
            if (_puzzleRiddleScreen == null)
            {
                _activePuzzleRiddle = null;
                _puzzleRiddleHandle = null;
            }
            EndWorldInteractionIfActive();
            UpdateInteractionPrompt();
            return;
        }

        _puzzleRiddleScreen = screen;
        _puzzleRiddleHandle = openResult.Handle.Value;
    }

    private void OnPuzzleRiddleChoiceSelected(string choiceId)
    {
        try
        {
            var riddle = _activePuzzleRiddle;
            var screen = _puzzleRiddleScreen;
            if (riddle == null || screen == null || _puzzleTrapController == null)
            {
                // The controller already entered Resolving, where both choices
                // and Cancel are ignored — this handler is the only exit left,
                // so a bare return would strand the hosted screen and latch.
                AbandonHostedPuzzleRiddle($"active riddle state missing for choice '{choiceId}'");
                return;
            }

            var result = _puzzleTrapController.TrySolveRiddle(riddle, choiceId);

            if (result.ShouldApplyPenalty)
            {
                var healthBefore = _gameManager.Player.CurrentHealth;
                ApplyPuzzleDamage(riddle.WrongAnswerDamage);
                _gameManager.NotifyPlayerStatsChanged();
                var healthLost = healthBefore - _gameManager.Player.CurrentHealth;

                screen.ShowTerminalFeedback(
                    $"{result.Message} (-{healthLost} HP)",
                    "Close");
            }
            else if (result.Solved)
            {
                ApplyPuzzleSolvedState(riddle.PuzzleId);
                _gameManager.NotifyPlayerStatsChanged();
                screen.ShowTerminalFeedback(result.Message, "Continue");
            }
            else
            {
                // Neither solved nor penalized (e.g. switch not armed) — rearm
                // the same hosted screen so the player gets another chance.
                screen.RearmWithFeedback(result.Message);
            }
        }
        // Intentionally broad: the controller sits in Resolving where Cancel
        // is ignored, so a swallowed resolution failure must still close the
        // hosted riddle or the game soft-locks behind the world latch.
        catch (Exception ex)
        {
            GD.PushError($"[Game] Failed to resolve puzzle riddle choice '{choiceId}': {ex}");
            AbandonHostedPuzzleRiddle($"resolution failure for choice '{choiceId}'");
        }
    }

    // Riddle-local last-resort exit: closes the hosted entry when possible and
    // always converges on the idempotent local clear, so the world latch ends
    // even when the close itself cannot run (host missing, no handle, or a
    // throwing close publication).
    private void AbandonHostedPuzzleRiddle(string cause)
    {
        GD.PushError($"[Game] Closing hosted puzzle riddle after {cause}.");
        try
        {
            ClosePuzzleRiddlePresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception closeEx)
        {
            GD.PushError($"[Game] Hosted puzzle riddle close after {cause} failed: {closeEx.Message}");
        }

        var screen = _puzzleRiddleScreen;
        if (screen != null)
        {
            ClearPuzzleRiddlePresentation(screen);
        }
        else
        {
            _puzzleRiddleHandle = null;
            _activePuzzleRiddle = null;
            EndWorldInteractionIfActive();
            if (IsInsideTree())
            {
                UpdateInteractionPrompt();
            }
        }
    }

    private void OnPuzzleRiddleClosed()
    {
        // TryClose's Recompute publishes EffectiveStateChanged /
        // GameplayInputBlockChanged; a throwing subscriber escapes TryClose
        // after the host already ran the spec Cleanup
        // (ClearPuzzleRiddlePresentation), which unsubscribed these signals
        // and released the world latch. Swallowing the publication exception
        // (mirroring the NPC close handlers) keeps the latch from leaking.
        try
        {
            ClosePuzzleRiddlePresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[Game] Close publication failed during puzzle riddle closed: {ex.Message}");
        }

        EndWorldInteractionIfActive();
        if (IsInsideTree())
        {
            UpdateInteractionPrompt();
        }
    }

    private void EndWorldInteractionIfActive()
    {
        if (_gameManager != null &&
            GodotObject.IsInstanceValid(_gameManager) &&
            _gameManager.IsInWorldInteraction)
        {
            _gameManager.EndWorldInteraction();
        }
    }

    private void ClearPuzzleRiddlePresentation(PuzzleRiddleScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.ChoiceSelected -= OnPuzzleRiddleChoiceSelected;
            screen.PuzzleRiddleClosed -= OnPuzzleRiddleClosed;
        }

        if (ReferenceEquals(_puzzleRiddleScreen, screen))
        {
            _puzzleRiddleScreen = null;
            _puzzleRiddleHandle = null;
            _activePuzzleRiddle = null;
        }

        EndWorldInteractionIfActive();
        if (IsInsideTree())
        {
            UpdateInteractionPrompt();
        }
    }

    private void ClosePuzzleRiddlePresentation(UIScreenCloseReason reason)
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_puzzleRiddleHandle.HasValue)
        {
            return;
        }

        var result = _screenHost.TryClose(_puzzleRiddleHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
        {
            if (_puzzleRiddleScreen != null)
                ClearPuzzleRiddlePresentation(_puzzleRiddleScreen);
            else
                _puzzleRiddleHandle = null;
        }
    }

    private void ClearRejectedPuzzleRiddleCandidate(PuzzleRiddleScreenController screen)
    {
        // Riddle-local: releases candidate views that were never committed, so
        // no host Cleanup callback will ever run for them.
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.ChoiceSelected -= OnPuzzleRiddleChoiceSelected;
            screen.PuzzleRiddleClosed -= OnPuzzleRiddleClosed;
            screen.QueueFree();
        }

        // The candidate was never committed (_puzzleRiddleScreen is still null),
        // so ClearPuzzleRiddlePresentation's ReferenceEquals guard would skip the
        // active-riddle/handle reset. Clear them directly so the rejection paths
        // (TryPresent throw, non-Opened status) do not strand _activePuzzleRiddle.
        if (_puzzleRiddleScreen == null)
        {
            _activePuzzleRiddle = null;
            _puzzleRiddleHandle = null;
        }
    }

    private void ApplyPuzzleSolvedState(string puzzleId)
    {
        var gate = FindPuzzleGateByPuzzleId(puzzleId);
        gate?.ApplySolvedState(true);
        _gridMap?.RegisterStaticPuzzleEntities();
    }

    // Traps deal damage but never kill — the floor at 1 HP is intentional so the
    // player always survives to reach a healer.  Combat death (BattleManager) uses a
    // different path because combat is an expected death scenario.
    private void ApplyPuzzleDamage(int damage)
    {
        if (damage <= 0 || _gameManager?.Player == null)
        {
            return;
        }

        var player = _gameManager.Player;
        player.CurrentHealth = Mathf.Max(1, player.CurrentHealth - damage);
    }

    private void ApplyTrapStatusEffect(TrapTileSpawn trap)
    {
        if (string.IsNullOrWhiteSpace(trap.StatusEffectId) || trap.StatusTurns <= 0 || _gameManager?.Player == null)
        {
            return;
        }

        if (Enum.TryParse(trap.StatusEffectId, ignoreCase: true, out StatusEffectType effectType))
        {
            _gameManager.Player.ActiveBuffs.Add(new ActiveStatusEffect(effectType, trap.StatusMagnitude, trap.StatusTurns));
        }
        else
        {
            GD.PushWarning($"[Game] Unknown trap status effect '{trap.StatusEffectId}' on '{trap.Name}'.");
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

    private TrapTileSpawn? FindTrapTileAt(Vector2I internalGridPosition)
    {
        return FindPuzzleNodeAt<TrapTileSpawn>("TrapTileSpawn", internalGridPosition);
    }

    private PuzzleSwitchSpawn? FindPuzzleSwitchAt(Vector2I internalGridPosition)
    {
        return FindPuzzleNodeAt<PuzzleSwitchSpawn>("PuzzleSwitchSpawn", internalGridPosition);
    }

    private PuzzleRiddleSpawn? FindPuzzleRiddleAt(Vector2I internalGridPosition)
    {
        return FindPuzzleNodeAt<PuzzleRiddleSpawn>("PuzzleRiddleSpawn", internalGridPosition);
    }

    private T? FindPuzzleNodeAt<T>(string groupName, Vector2I internalGridPosition)
        where T : PuzzleSpawnBase
    {
        if (_gridMap == null)
        {
            return null;
        }

        Vector2I tilemapPos = _gridMap.InternalGridToTilemapCoords(internalGridPosition);
        Node? currentFloorRoot = _gridMap.GetParent();

        foreach (Node n in GetTree().GetNodesInGroup(groupName))
        {
            if (n is T spawn &&
                spawn.BelongsToFloor(currentFloorRoot) &&
                spawn.GridPosition == tilemapPos)
            {
                return spawn;
            }
        }

        return null;
    }

    private PuzzleGateSpawn? FindPuzzleGateByPuzzleId(string puzzleId)
    {
        if (_gridMap == null || string.IsNullOrWhiteSpace(puzzleId))
        {
            return null;
        }

        Node? currentFloorRoot = _gridMap.GetParent();
        foreach (Node n in GetTree().GetNodesInGroup("PuzzleGateSpawn"))
        {
            if (n is PuzzleGateSpawn gate &&
                gate.BelongsToFloor(currentFloorRoot) &&
                gate.PuzzleId == puzzleId)
            {
                return gate;
            }
        }

        return null;
    }

    private void UpdateInteractionPrompt()
    {
        if (_explorationHud == null)
            return;

        if (_gridMap == null ||
            _playerController == null ||
            _gameManager == null ||
            _sceneChangeCommitted ||
            IsGameplayInputSuppressed())
        {
            _explorationHud.HideInteractionPrompt();
            return;
        }

        Vector2I target = _gridMap.GetPlayerPosition() + _playerController.FacingDirection;
        var box = FindTreasureBoxAt(target);
        if (box != null &&
            !box.IsOpened &&
            !box.IsOpening &&
            !_gameManager.IsTreasureBoxOpened(box.TreasureBoxId))
        {
            _explorationHud.ShowInteractionPrompt("Open", UiIconId.Reward);
            return;
        }

        var puzzleSwitch = FindPuzzleSwitchAt(target);
        if (puzzleSwitch != null &&
            !string.IsNullOrWhiteSpace(puzzleSwitch.PuzzleId) &&
            !_gameManager.IsPuzzleSolved(puzzleSwitch.PuzzleId))
        {
            _explorationHud.ShowInteractionPrompt("Use", UiIconId.Puzzle);
            return;
        }

        var riddle = FindPuzzleRiddleAt(target);
        if (riddle != null &&
            !string.IsNullOrWhiteSpace(riddle.PuzzleId) &&
            !_gameManager.IsPuzzleSolved(riddle.PuzzleId))
        {
            _explorationHud.ShowInteractionPrompt("Solve", UiIconId.Puzzle);
            return;
        }

        _explorationHud.HideInteractionPrompt();
    }

    private void EndNpcInteractionIfActive()
    {
        if (_gameManager != null &&
            GodotObject.IsInstanceValid(_gameManager) &&
            _gameManager.IsInNpcInteraction)
        {
            _gameManager.EndNpcInteraction();
        }
    }

    private void OnNpcInteractionComplete()
    {
        if (_npcInteractionController != null)
        {
            _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
        }

        EndNpcInteractionIfActive();
        _npcInteractionController = null;
        UpdatePlayerUI();
        UpdateInteractionPrompt();
    }

    private void OnNpcInteractionResetRequested()
    {
        if (_npcInteractionController != null)
        {
            _npcInteractionController.Finish();
            return;
        }

        EndNpcInteractionIfActive();
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
        UpdateInteractionPrompt();

        // Load battle scene
        var battleScene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn");
        if (battleScene == null)
        {
            GD.PrintErr("ERROR: Failed to load battle scene!");
            _gameManager.ResetBattleState();
            UpdateInteractionPrompt();
            return;
        }

        var battle = battleScene.Instantiate<BattleManager>();
        if (battle == null)
        {
            GD.PrintErr("ERROR: Failed to instantiate BattleManager!");
            _gameManager.ResetBattleState();
            UpdateInteractionPrompt();
            return;
        }

        // Signal subscriptions live here (not in _Ready) because a fresh
        // BattleManager is instantiated per encounter; Game._Ready cannot bind
        // to an instance that does not exist yet. Every teardown path
        // (ClearBattlePresentation, CleanupBattleManager, and the host-unavailable
        // fallbacks below) unsubscribes symmetrically.
        battle.BattleFinished += OnBattleFinished;
        battle.DismissRequested += OnBattleDismissRequested;

        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
        {
            GD.PrintErr("ERROR: UIScreenHost is unavailable; refusing to present Battle directly.");
            battle.BattleFinished -= OnBattleFinished;
            battle.DismissRequested -= OnBattleDismissRequested;
            battle.QueueFree();
            _gameManager.ResetBattleState();
            UpdateInteractionPrompt();
            return;
        }

        var result = _screenHost.TryPresent(battle, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Battle,
            Layer = UIScreenLayer.Screen,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.Hidden,
            Cancel = UICancelPolicy.Consume,
            InitialFocus = () => battle.InitialFocusTarget,
            InterceptCancel = _ =>
            {
                battle.RequestCancel();
                return UIInputInterception.ConsumeHere;
            },
            Cleanup = _ => ClearBattlePresentation(battle),
            NodeLifetime = UINodeLifetime.QueueFree
        });

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            GD.PrintErr($"ERROR: Failed to host Battle screen ({result.Status}).");
            battle.BattleFinished -= OnBattleFinished;
            battle.DismissRequested -= OnBattleDismissRequested;
            battle.QueueFree();
            _gameManager.ResetBattleState();
            UpdateInteractionPrompt();
            return;
        }

        _battleManager = battle;
        _battleHandle = result.Handle.Value;
        battle.StartBattle(_gameManager.Player, enemy);
        GD.Print("Battle started successfully");
    }

    private void OnBattleEnded(bool playerWon)
    {
        GD.Print($"Battle ended in GameManager. Player won: {playerWon}");
        // Battle logic is now handled in OnBattleFinished
    }

    private void ClearBattlePresentation(BattleManager battle)
    {
        battle.BattleFinished -= OnBattleFinished;
        battle.DismissRequested -= OnBattleDismissRequested;
        if (ReferenceEquals(_battleManager, battle))
            _battleManager = null;
        _battleHandle = null;
    }

    private void OnBattleDismissRequested()
    {
        if (_screenHost == null || !_battleHandle.HasValue)
            return;

        _screenHost.TryClose(_battleHandle.Value, UIScreenCloseReason.ExplicitAction);
    }

    private void CleanupBattleManager()
    {
        var battle = _battleManager;
        if (battle == null || !GodotObject.IsInstanceValid(battle))
        {
            _battleManager = null;
            _battleHandle = null;
            return;
        }

        if (_screenHost != null && _battleHandle.HasValue &&
            _screenHost.IsActive(_battleHandle.Value))
        {
            _screenHost.TryClose(_battleHandle.Value, UIScreenCloseReason.HostTeardown);
            return;
        }

        battle.BattleFinished -= OnBattleFinished;
        battle.DismissRequested -= OnBattleDismissRequested;
        battle.QueueFree();
        _battleManager = null;
        _battleHandle = null;
    }

    private void OnBattleFinished(bool playerWon, bool playerEscaped)
    {
        GD.Print($"OnBattleFinished called. Player won: {playerWon}, Player escaped: {playerEscaped}");
        
        // Prevent multiple calls
        if (_battleManager == null || !GodotObject.IsInstanceValid(_battleManager))
        {
            GD.Print("BattleManager is unavailable, battle already finished");
            return;
        }
        
        // Disconnect BattleFinished to prevent multiple calls. DismissRequested
        // remains connected until the hosted Control is explicitly dismissed.
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
            ScheduleDefeatReturnToMainMenu();
        }

        UpdateInteractionPrompt();
    }

    private void UpdatePlayerUI()
    {
        if (_isAbortInitialization ||
            _gameManager?.Player == null ||
            _explorationHud == null)
        {
            return;
        }

        var player = _gameManager.Player;
        _explorationHud.ApplyPlayerState(new ExplorationHudPlayerState(
            player.Name,
            player.Level,
            player.CurrentHealth,
            player.GetEffectiveMaxHealth(),
            player.CurrentMana,
            player.MaxMana,
            player.Experience,
            player.ExperienceToNext));
    }

    private void ScheduleDefeatReturnToMainMenu()
    {
        CancelDefeatReturnToMainMenu();
        _defeatReturnTimer = GetTree().CreateTimer(DefeatReturnDelaySeconds);
        _defeatReturnHandler = OnDefeatReturnTimeout;
        _defeatReturnTimer.Timeout += _defeatReturnHandler;
    }

    private void OnDefeatReturnTimeout()
    {
        CancelDefeatReturnToMainMenu();
        if (IsInsideTree())
        {
            ReturnToMainMenu();
        }
    }

    private void CancelDefeatReturnToMainMenu()
    {
        if (_defeatReturnTimer != null
            && GodotObject.IsInstanceValid(_defeatReturnTimer)
            && _defeatReturnHandler != null)
        {
            _defeatReturnTimer.Timeout -= _defeatReturnHandler;
        }

        _defeatReturnTimer = null;
        _defeatReturnHandler = null;
    }

    private UIRootCancelResult HandleGameplayRootCancel(UIRootCancelContext _)
    {
        if (_gameManager == null)
            return UIRootCancelResult.Declined;

        if (_puzzleRiddleScreen != null && IsInstanceValid(_puzzleRiddleScreen))
            return UIRootCancelResult.Declined;

        if (_gameManager.IsInWorldInteraction)
            return UIRootCancelResult.Consumed;

        if (_gameManager.IsInNpcInteraction)
            return UIRootCancelResult.Declined;

        return TryOpenPause()
            ? UIRootCancelResult.Consumed
            : UIRootCancelResult.Declined;
    }

    private void RequestSceneChange(string path)
    {
        if (_sceneChangeCommitted)
            return;

        _sceneChangeCommitted = true;
        UpdateInteractionPrompt();
        _pendingScenePath = path;
        ContinueSceneChangeAfterUiTeardown();
    }

    private void ContinueSceneChangeAfterUiTeardown()
    {
        // This method may be re-entered via CallDeferred; by the time the
        // deferred callback fires the Game node may have been detached or
        // freed (e.g., during defeat return or rapid navigation). The
        // Callable.From delegate keeps the C# wrapper alive after native
        // teardown, so IsInsideTree() alone is an unsafe dereference on a
        // disposed instance — validate the native object first.
        if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
            return;

        if (_screenHost != null && IsInstanceValid(_screenHost) &&
            _screenHost.PrepareForTeardown() == UIScreenTeardownPreparationStatus.Deferred)
        {
            Callable.From(ContinueSceneChangeAfterUiTeardown).CallDeferred();
            return;
        }

        var path = _pendingScenePath;
        _pendingScenePath = null;
        if (!string.IsNullOrEmpty(path))
            GetTree().ChangeSceneToFile(path);
    }

    protected virtual void ReturnToMainMenu() => RequestSceneChange(MainMenuScenePath);

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
    private bool ShowSaveError(string message, string title = "Save Failed")
    {
        if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
            !_hostedSaveLoadHandle.HasValue ||
            !_screenHost.IsActive(_hostedSaveLoadHandle.Value))
        {
            GD.PushError(
                "[Game] Cannot show save/load error without an active Save/Load parent.");
            return false;
        }

        var opened = TryOpenHostedPrompt(
            SiriusPromptVariant.RecoverableError,
            title,
            message,
            "OK",
            onPrimary: () => _hostedSaveLoadScreen?.RearmAfterFailedTerminal(),
            parent: _hostedSaveLoadHandle);

        if (!opened)
            _hostedSaveLoadScreen?.RearmAfterFailedTerminal();

        return opened;
    }

    private void ShowCorruptedSaveError()
    {
        if (_hasShownCorruptedSaveError)
        {
            return;
        }
        _hasShownCorruptedSaveError = true;

        var opened = TryOpenHostedPrompt(
            SiriusPromptVariant.RecoverableError,
            "Load Failed",
            "Save file is corrupted or invalid.\nReturning to main menu.",
            "Return to Title",
            onPrimary: ReturnToMainMenu,
            blockGameplayInput: true);

        if (!opened)
        {
            GD.PushError("[Game] Failed to present corrupted-save error; returning to title.");
            ReturnToMainMenu();
        }
    }

    private void OnPlayerStatsChanged()
    {
        UpdatePlayerUI();
    }

    private void OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)
    {
        GD.Print($"🎮 Game.OnFloorLoaded: Floor '{floorDef.FloorName}' ready");
        _explorationHud?.ShowAreaTitle(floorDef.FloorName);

        // Disconnect signals from old GridMap to prevent handler accumulation
        if (_gridMap != null)
        {
            _gridMap.EnemyEncountered -= OnEnemyEncountered;
            _gridMap.PlayerMoved -= OnPlayerMoved;
            _gridMap.NpcInteracted -= OnNpcInteracted;
            _gridMap.TreasureBoxOpenRequested -= OnTreasureBoxOpenRequested;
            _gridMap.TrapTileTriggered -= OnTrapTileTriggered;
            _gridMap.PuzzleInteractionRequested -= OnPuzzleInteractionRequested;
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
            _gridMap.TrapTileTriggered += OnTrapTileTriggered;
            _gridMap.PuzzleInteractionRequested += OnPuzzleInteractionRequested;

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
        CancelDefeatReturnToMainMenu();

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
            _gridMap.TrapTileTriggered -= OnTrapTileTriggered;
            _gridMap.PuzzleInteractionRequested -= OnPuzzleInteractionRequested;
        }

        if (_playerController != null)
        {
            _playerController.GameplayInputSuppressedProvider = null;
            _playerController.FacingChanged -= OnPlayerFacingChanged;
        }

        if (_npcInteractionController != null)
        {
            _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
            _npcInteractionController.Finish();
            _npcInteractionController = null;
        }

        EndNpcInteractionIfActive();

        // Close the hosted riddle entry with the teardown reason while the
        // screen node is still valid; regardless of the close outcome (host
        // missing, no handle, host tearing down, or close throwing), fall
        // back to a local idempotent clear so the retained riddle
        // presentation never outlives this node.
        try
        {
            ClosePuzzleRiddlePresentation(UIScreenCloseReason.HostTeardown);
        }
        catch (Exception ex)
        {
            GD.PushError($"[Game] Hosted puzzle riddle teardown close failed: {ex.Message}");
        }

        var riddleScreen = _puzzleRiddleScreen;
        if (riddleScreen != null)
        {
            ClearPuzzleRiddlePresentation(riddleScreen);
        }
        else
        {
            _puzzleRiddleHandle = null;
            _activePuzzleRiddle = null;
            EndWorldInteractionIfActive();
        }

        CleanupBattleManager();

        if (_inventoryMenu != null)
        {
            _inventoryMenu.CloseRequested -= OnInventoryCloseRequested;
            // Close the host entry before freeing the externally owned view so
            // its UIScreenHost record is released cleanly while the node is
            // still valid.
            TryCloseInventory(UIScreenCloseReason.NodeFreed);
            if (GodotObject.IsInstanceValid(_inventoryMenu))
                _inventoryMenu.QueueFree();
            _inventoryMenu = null!;
        }

        ClearInventoryHandle();
    }
}
