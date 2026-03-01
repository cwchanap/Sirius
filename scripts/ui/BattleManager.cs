using Godot;
using System;

public partial class BattleManager : AcceptDialog
{
    [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, bool playerEscaped);
    
    private Character _player;
    private Enemy _enemy;
    private bool _playerTurn = true;

    // Action point system for speed-based turn frequency
    private float _playerActionPoints = 0f;
    private float _enemyActionPoints = 0f;
    private const float ACTION_POINT_THRESHOLD = 100f;

    // UI References
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private Label _playerAttackLabel;
    private Label _playerDefenseLabel;
    private Label _enemyLevelLabel;
    private Label _enemyHealthLabel;
    private Label _enemyAttackLabel;
    private Label _enemyDefenseLabel;
    private Label _playerSpeedLabel;
    private Label _enemySpeedLabel;
    private Button _attackButton;
    private Button _defendButton;
    private Button _runButton;
    private Button _itemButton;
    private Button _startButton;
    
    // Animation and Visual References
    private AnimatedSprite2D _playerSprite;
    private AnimatedSprite2D _enemySprite;
    private Label _playerDamageLabel;
    private Label _enemyDamageLabel;
    private Label _lootLabel;

    // Auto-battle properties
    private Timer _battleTimer;
    private bool _battleInProgress = false;
    private bool _playerDefendedLastTurn = false;
    private bool _resultEmitted = false; // Guards against double-emission in the common case; timer stop and signal emit must always be called together
    private readonly Random _rng = new();
    private LootResult? _pendingLootDisplay;
    private bool _playerActedLast = false;

    // Pre-battle item selection
    private VBoxContainer? _itemPanel;
    private ConsumableItem? _selectedConsumable;
    
    public override void _Ready()
    {
        GD.Print("BattleManager _Ready called");

        // Add battle background first (before other UI elements)
        AddBattleBackground();

        // Get references to UI elements defined in the scene
        _enemyLevelLabel = GetNode<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemyLevel");
        _enemyHealthLabel = GetNode<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemyHealth");
        _enemyAttackLabel = GetNode<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemyAttack");
        _enemyDefenseLabel = GetNode<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemyDefense");
        _enemySpeedLabel = GetNodeOrNull<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemySpeed");
        _playerLevelLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerLevel");
        _playerHealthLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerHealth");
        _playerAttackLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerAttack");
        _playerDefenseLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerDefense");
        _playerSpeedLabel = GetNodeOrNull<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerSpeed");
        _attackButton = GetNode<Button>("BattleContent/ActionButtons/AttackButton");
        _defendButton = GetNode<Button>("BattleContent/ActionButtons/DefendButton");
        _runButton = GetNode<Button>("BattleContent/ActionButtons/RunButton");
        _itemButton = GetNodeOrNull<Button>("BattleContent/ActionButtons/ItemButton");
        _startButton = GetNodeOrNull<Button>("BattleContent/ActionButtons/StartButton");

        // Verify all UI elements are loaded
        if (_enemyLevelLabel == null) GD.PrintErr("ERROR: EnemyLevelLabel not found!");
        if (_playerLevelLabel == null) GD.PrintErr("ERROR: PlayerLevelLabel not found!");
        if (_attackButton == null) GD.PrintErr("ERROR: AttackButton not found!");
        if (_defendButton == null) GD.PrintErr("ERROR: DefendButton not found!");
        if (_runButton == null) GD.PrintErr("ERROR: RunButton not found!");
        if (_startButton == null) GD.Print("INFO: StartButton not found (auto-start fallback)");
        if (_playerSpeedLabel == null) GD.Print("INFO: PlayerSpeed label not found (optional)");
        if (_enemySpeedLabel == null) GD.Print("INFO: EnemySpeed label not found (optional)");

        GD.Print("BattleManager UI elements loaded");

        // Get animation and visual references
        _playerSprite = GetNode<AnimatedSprite2D>("BattleContent/BattleArena/LeftSide/PlayerSpriteContainer/PlayerSprite");
        _enemySprite = GetNode<AnimatedSprite2D>("BattleContent/BattleArena/RightSide/EnemySpriteContainer/EnemySprite");
        _playerDamageLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerDamageLabel");
        _enemyDamageLabel = GetNode<Label>("BattleContent/BattleArena/RightSide/EnemyStatsContainer/EnemyDamageLabel");
        
        // Get container references for responsive positioning
        var playerContainer = GetNode<Control>("BattleContent/BattleArena/LeftSide/PlayerSpriteContainer");
        var enemyContainer = GetNode<Control>("BattleContent/BattleArena/RightSide/EnemySpriteContainer");
        
        // Connect to container resizing events for responsive positioning
        playerContainer.Resized += () => PositionPlayerSprite(playerContainer);
        enemyContainer.Resized += () => PositionEnemySprite(enemyContainer);
        
        // Initial centering
        CenterSprites();
        
        // Hide manual action buttons since combat is automated
        _attackButton.Visible = false;
        _defendButton.Visible = false;
        _runButton.Visible = false;
        if (_itemButton != null)
        {
            _itemButton.Visible = false;
            _itemButton.Pressed += OnItemButtonPressed;
        }
        if (_startButton != null)
        {
            _startButton.Visible = true;
            _startButton.Disabled = false;
            _startButton.Pressed += OnStartButtonPressed;
        }
        
        // Initialize damage labels as invisible
        _playerDamageLabel.Modulate = new Color(1, 0, 0, 0);
        _enemyDamageLabel.Modulate = new Color(1, 0, 0, 0);
        
        // Create and configure battle timer for auto-combat
        _battleTimer = new Timer();
        _battleTimer.WaitTime = 1.5; // 1.5 seconds between actions for visual feedback
        _battleTimer.Timeout += OnBattleTurnTimer;
        AddChild(_battleTimer);
        
        // Set dialog title and properties
        Title = "Battle!";
        GetOkButton().Text = "Close";
        GetOkButton().Visible = false; // Hide the OK button initially
        
        // Connect close signals (window X and ESC)
        CloseRequested += OnCloseRequested; // Window close button
        Canceled += OnCloseRequested;       // ESC key path
    }
    
    private void AddBattleBackground()
    {
        // Create a TextureRect for the battle background
        var backgroundRect = new TextureRect();
        backgroundRect.Name = "BattleBackground";
        
        // Set it to fill the entire dialog using anchors manually
        backgroundRect.AnchorLeft = 0;
        backgroundRect.AnchorTop = 0;
        backgroundRect.AnchorRight = 1;
        backgroundRect.AnchorBottom = 1;
        backgroundRect.OffsetLeft = 0;
        backgroundRect.OffsetTop = 0;
        backgroundRect.OffsetRight = 0;
        backgroundRect.OffsetBottom = 0;
        backgroundRect.ZIndex = -1; // Put it behind other elements
        
        // Try to load the battle background image
        var backgroundTexture = GD.Load<Texture2D>("res://assets/sprites/ui/ui_battle_background.png");
        
        if (backgroundTexture != null)
        {
            backgroundRect.Texture = backgroundTexture;
            backgroundRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
            backgroundRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            // Battle background loaded successfully
        }
        else
        {
            // Fallback to a solid color background using a ColorRect instead
            var colorRect = new ColorRect();
            colorRect.Name = "BattleBackgroundColor";
            colorRect.AnchorLeft = 0;
            colorRect.AnchorTop = 0;
            colorRect.AnchorRight = 1;
            colorRect.AnchorBottom = 1;
            colorRect.OffsetLeft = 0;
            colorRect.OffsetTop = 0;
            colorRect.OffsetRight = 0;
            colorRect.OffsetBottom = 0;
            colorRect.ZIndex = -1;
            colorRect.Color = new Color(0.1f, 0.1f, 0.1f, 1.0f); // Dark fallback — replace with proper asset before shipping
            AddChild(colorRect);
            MoveChild(colorRect, 0);
            GD.PushWarning("[BattleManager] Battle background asset not found; using dark color fallback.");
            return;
        }
        
        // Add the background as the first child (so it appears behind everything)
        AddChild(backgroundRect);
        MoveChild(backgroundRect, 0);
    }
    
    private void OnCloseRequested()
    {
        // Always resolve battle state when the dialog is closed, including
        // the case where the user closes before pressing Start.
        if (!_resultEmitted)
        {
            if (_battleInProgress && _player != null && _enemy != null && _player.IsAlive && _enemy.IsAlive)
            {
                GD.Print("Battle interrupted via window close - treating as escape");
                _battleInProgress = false;
                _battleTimer.Stop();
                EndBattleWithEscape(); // Emits BattleFinished and sets _resultEmitted
            }
            else
            {
                GD.Print("Battle dialog closed before start or after result - emitting escape to unlock input");
                _resultEmitted = true;
                EmitSignal(SignalName.BattleFinished, false, true); // Treat as escaped
            }
        }

        // Close and free the dialog so it cannot keep any input focus
        Hide();
        QueueFree();
    }

    // Allow the game scene to programmatically request closing the battle dialog
    // (e.g., when ESC is pressed). This reuses the same logic as the window's
    // close button and guarantees the battle state is resolved.
    public void ForceCloseAsEscape()
    {
        GD.Print("ForceCloseAsEscape invoked by Game");
        OnCloseRequested();
    }
    
    /// <summary>
    /// Initializes the battle with the given combatants and sets up UI.
    /// Player goes first if Speed &gt;= enemy Speed (ties favor the player).
    /// </summary>
    public void StartBattle(Character player, Enemy enemy)
    {
        if (player == null || enemy == null)
        {
            GD.PrintErr($"[BattleManager] StartBattle called with null {(player == null ? "player" : "enemy")}; aborting battle.");
            _resultEmitted = true;
            EmitSignal(SignalName.BattleFinished, false, true);
            Hide();
            QueueFree();
            return;
        }

        GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");

        _player = player;
        _enemy = enemy;
        _playerTurn = true; // Placeholder; determined after pre-battle consumables in OnStartButtonPressed()
        _playerActedLast = false; // Reset turn tracking for dynamic speed-based turn order
        _battleInProgress = false;

        // Initialize action points for speed-based turn frequency
        _playerActionPoints = 0f;
        _enemyActionPoints = 0f;
        _pendingLootDisplay = null;
        // Clean up any loot label left from a previous battle
        if (_lootLabel != null && IsInstanceValid(_lootLabel))
        {
            _lootLabel.QueueFree();
            _lootLabel = null;
        }
        
        // Setup character animations
        SetupCharacterAnimations();
        
        GD.Print($"Battle begins! {_player.Name} vs {_enemy.Name}");
        GD.Print($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first!");
        GD.Print("Auto-battle mode: Click Start to begin.");
        
        UpdateUI();
        _selectedConsumable = null;

        // Start immediately if no StartButton exists (fallback), otherwise wait for user
        // Skip building the consumable panel when auto-starting since there's no way to select items
        if (_startButton == null)
        {
            _playerTurn = _player.GetEffectiveSpeed() >= _enemy.GetEffectiveSpeed();
            _battleInProgress = true;
            _battleTimer.Start();
            GD.Print($"StartButton not present; auto-battle started. Turn order: {(_playerTurn ? "Player" : "Enemy")} first.");
        }
        else
        {
            BuildConsumablePanel();
        }
    }

    private void BuildConsumablePanel()
    {
        // Remove any stale panel from a previous StartBattle call
        if (_itemPanel != null && IsInstanceValid(_itemPanel))
        {
            _itemPanel.QueueFree();
            _itemPanel = null;
        }

        var battleContent = GetNodeOrNull<VBoxContainer>("BattleContent");
        if (battleContent == null) return;

        _itemPanel = new VBoxContainer { Name = "ItemPanel" };

        var title = new Label { Text = "Use an item before battle? (optional)" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _itemPanel.AddChild(title);

        bool hasConsumables = false;
        foreach (var entry in _player.Inventory.GetAllEntries())
        {
            if (entry.Item is not ConsumableItem consumable) continue;

            hasConsumables = true;
            var btn = new Button
            {
                Text        = $"{consumable.DisplayName} x{entry.Quantity}  ({consumable.EffectDescription})",
                TooltipText = consumable.Description
            };
            ConsumableItem captured = consumable;
            btn.Pressed += () => OnConsumableSelected(captured, btn);
            _itemPanel.AddChild(btn);
        }

        if (!hasConsumables)
        {
            var none = new Label { Text = "(No consumables in inventory)" };
            none.HorizontalAlignment = HorizontalAlignment.Center;
            _itemPanel.AddChild(none);
        }

        // Insert above ActionButtons row
        var actionButtons = GetNodeOrNull<HBoxContainer>("BattleContent/ActionButtons");
        int insertIndex = battleContent.GetChildCount();
        if (actionButtons != null)
        {
            for (int i = 0; i < battleContent.GetChildCount(); i++)
            {
                if (battleContent.GetChild(i) == actionButtons) { insertIndex = i; break; }
            }
        }
        battleContent.AddChild(_itemPanel);
        battleContent.MoveChild(_itemPanel, insertIndex);
    }

    private void OnConsumableSelected(ConsumableItem item, Button sourceButton)
    {
        _selectedConsumable = item;
        GD.Print($"[BattleManager] Pre-battle item selected: {item.DisplayName}");

        // Visually indicate selection by disabling the chosen button
        if (_itemPanel != null && IsInstanceValid(_itemPanel))
        {
            foreach (var child in _itemPanel.GetChildren())
            {
                if (child is Button btn) btn.Disabled = false;
            }
        }
        sourceButton.Disabled = true;
    }

    private void OnStartButtonPressed()
    {
        if (_battleInProgress) return;
        if (_player == null || _enemy == null)
        {
            GD.PrintErr("Start pressed but battle participants not initialized");
            return;
        }

        // Apply the selected pre-battle consumable (if any)
        // Remove item first to prevent duplication if effect application succeeds but removal fails
        if (_selectedConsumable != null)
        {
            if (_selectedConsumable.Effect is EnemyDebuffEffect enemyEffect)
            {
                // Enemy-targeting item: remove first, then apply to enemy
                if (_player.TryRemoveItem(_selectedConsumable.Id, 1))
                {
                    enemyEffect.ApplyToEnemy(_enemy);
                    GD.Print($"[BattleManager] Applied '{_selectedConsumable.DisplayName}' to {_enemy.Name}");
                }
                else
                {
                    GD.PushWarning($"[BattleManager] Could not consume '{_selectedConsumable.DisplayName}'; effect not applied to {_enemy.Name}");
                }
            }
            else
            {
                // Player-targeting item: remove first, then apply to player
                if (_player.TryRemoveItem(_selectedConsumable.Id, 1))
                {
                    if (_selectedConsumable.Apply(_player))
                    {
                        UpdateUI(); // Refresh HP display if a potion was used
                        GD.Print($"[BattleManager] Applied '{_selectedConsumable.DisplayName}' to {_player.Name}");
                    }
                    else
                    {
                        GD.PushWarning($"[BattleManager] '{_selectedConsumable.DisplayName}' was consumed but could not be applied, attempting rollback");
                        bool rollbackSuccess = _player.TryAddItem(_selectedConsumable, 1, out _);
                        if (!rollbackSuccess)
                            GD.PrintErr($"[BattleManager] ROLLBACK FAILED for '{_selectedConsumable.DisplayName}' — item lost permanently!");
                        UpdateUI();
                    }
                }
                else
                {
                    GD.PushWarning($"[BattleManager] Could not consume '{_selectedConsumable.DisplayName}'; effect not applied");
                }
            }
            _selectedConsumable = null;
        }

        // Determine turn order using effective speed (accounts for pre-battle consumables)
        _playerTurn = _player.GetEffectiveSpeed() >= _enemy.GetEffectiveSpeed();
        GD.Print($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first! (Player SPD: {_player.GetEffectiveSpeed()}, Enemy SPD: {_enemy.GetEffectiveSpeed()})");

        // Hide item panel — no longer needed during combat
        if (_itemPanel != null && IsInstanceValid(_itemPanel))
        {
            _itemPanel.Visible = false;
        }

        _battleInProgress = true;
        if (_startButton != null)
        {
            _startButton.Visible = false;
        }
        if (_itemButton != null)
        {
            _itemButton.Visible = true;
            _itemButton.Disabled = false;
        }
        GD.Print("Battle started by user");
        _battleTimer.Start();
    }

    private void OnItemButtonPressed()
    {
        if (!_battleInProgress || _player == null) return;
        if (_itemPanel != null && IsInstanceValid(_itemPanel) && _itemPanel.Visible) return;

        CallDeferred(nameof(ShowCombatItemPanel));
    }

    private void ShowCombatItemPanel()
    {
        if (_itemPanel != null && IsInstanceValid(_itemPanel))
        {
            _itemPanel.QueueFree();
            _itemPanel = null;
        }

        var battleContent = GetNodeOrNull<VBoxContainer>("BattleContent");
        if (battleContent == null) return;

        _itemPanel = new VBoxContainer { Name = "ItemPanel" };

        var title = new Label { Text = "Use an item (cures work mid-battle):" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _itemPanel.AddChild(title);

        bool hasConsumables = false;
        foreach (var entry in _player.Inventory.GetAllEntries())
        {
            if (entry.Item is not ConsumableItem consumable) continue;

            hasConsumables = true;
            var btn = new Button
            {
                Text        = $"{consumable.DisplayName} x{entry.Quantity}  ({consumable.EffectDescription})",
                TooltipText = consumable.Description
            };
            bool isCureItem = consumable.Effect is CureStatusEffect;
            btn.Disabled = !isCureItem;
            if (!isCureItem)
            {
                btn.TooltipText = "Can only be used outside battle or at battle start";
            }
            ConsumableItem captured = consumable;
            btn.Pressed += () => OnCombatItemSelected(captured);
            _itemPanel.AddChild(btn);
        }

        if (!hasConsumables)
        {
            var none = new Label { Text = "(No consumables in inventory)" };
            none.HorizontalAlignment = HorizontalAlignment.Center;
            _itemPanel.AddChild(none);
        }

        var closeBtn = new Button { Text = "Cancel" };
        closeBtn.Pressed += () =>
        {
            if (_itemPanel != null && IsInstanceValid(_itemPanel))
            {
                _itemPanel.Visible = false;
            }
        };
        _itemPanel.AddChild(closeBtn);

        battleContent.AddChild(_itemPanel);
    }

    private void OnCombatItemSelected(ConsumableItem item)
    {
        if (_player == null || !_player.IsAlive) return;

        if (item.Effect is CureStatusEffect cureEffect)
        {
            if (_player.TryRemoveItem(item.Id, 1))
            {
                if (cureEffect.Apply(_player))
                {
                    UpdateUI();
                    GD.Print($"[BattleManager] Used '{item.DisplayName}' mid-battle to cure status effects.");
                }
                else
                {
                    // Defensive rollback — CureStatusEffect.Apply currently always returns true,
                    // but this branch handles any future effect that can fail after the item is removed.
                    GD.PushWarning($"[BattleManager] '{item.DisplayName}' was consumed but Apply returned false, attempting rollback");
                    bool rollbackSuccess = _player.TryAddItem(item, 1, out _);
                    if (!rollbackSuccess)
                        GD.PrintErr($"[BattleManager] ROLLBACK FAILED for '{item.DisplayName}' — item lost permanently!");
                    UpdateUI();
                }
            }
            else
            {
                GD.PushWarning($"[BattleManager] Could not consume '{item.DisplayName}'; item not removed.");
            }
        }

        if (_itemPanel != null && IsInstanceValid(_itemPanel))
        {
            _itemPanel.Visible = false;
        }
    }
    
    private void CenterSprites()
    {
        // Get the containers to center sprites within them
        var playerContainer = GetNode<Control>("BattleContent/BattleArena/LeftSide/PlayerSpriteContainer");
        var enemyContainer = GetNode<Control>("BattleContent/BattleArena/RightSide/EnemySpriteContainer");
        
        // Center sprites when their containers are ready
        CallDeferred(nameof(PositionSpritesInCenter), playerContainer, enemyContainer);
    }
    
    private void PositionSpritesInCenter(Control playerContainer, Control enemyContainer)
    {
        PositionPlayerSprite(playerContainer);
        PositionEnemySprite(enemyContainer);
    }
    
    private void PositionPlayerSprite(Control container)
    {
        if (_playerSprite != null && container.Size.X > 0 && container.Size.Y > 0)
        {
            var center = container.Size / 2;
            _playerSprite.Position = center;
        }
    }
    
    private void PositionEnemySprite(Control container)
    {
        if (_enemySprite != null && container.Size.X > 0 && container.Size.Y > 0)
        {
            var center = container.Size / 2;
            _enemySprite.Position = center;
        }
    }
    
    private void SetupCharacterAnimations()
    {
        // Create animation resources for player
        var playerSpriteFrames = new SpriteFrames();

        // Load player sprite sheet and create animation - with fallback
        var playerTexture = GD.Load<Texture2D>("res://assets/sprites/characters/player_hero/sprite_sheet.png");
        if (playerTexture != null)
        {
            playerSpriteFrames.AddAnimation("idle");

            // Derive frame size dynamically from texture (4 frames horizontally)
            var pSize = playerTexture.GetSize();
            int PLAYER_FRAME_W = Mathf.Max(1, Mathf.RoundToInt(pSize.X) / 4);
            int PLAYER_FRAME_H = Mathf.Max(1, Mathf.RoundToInt(pSize.Y));
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = playerTexture;
                atlasTexture.Region = new Rect2(i * PLAYER_FRAME_W, 0, PLAYER_FRAME_W, PLAYER_FRAME_H);
                atlasTexture.FilterClip = true;
                playerSpriteFrames.AddFrame("idle", atlasTexture);
            }

            playerSpriteFrames.SetAnimationSpeed("idle", 4.0);
            playerSpriteFrames.SetAnimationLoop("idle", true);
            _playerSprite.SpriteFrames = playerSpriteFrames;
            // Keep on-screen size ~96px width regardless of source resolution
            float targetPx = 96f;
            float pScale = targetPx / (float)PLAYER_FRAME_W;
            _playerSprite.Scale = new Vector2(pScale, pScale);
            _playerSprite.Modulate = new Color(1, 1, 1, 1);
            _playerSprite.Play("idle");

            var material = new CanvasItemMaterial();
            material.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            material.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _playerSprite.Material = material;

            _playerSprite.SelfModulate = new Color(1, 1, 1, 1);
            _playerSprite.Visible = true;
            _playerSprite.Centered = true;
        }
        else
        {
            GD.PushWarning("[BattleManager] Player sprite sheet not found; using fallback rendering.");
        }

        // Create animation resources for enemy
        var enemySpriteFrames = new SpriteFrames();

        // Load enemy sprite sheet and create animation - prefer new enemies/ path with fallback to legacy characters/
        Texture2D enemyTexture = null;
        string newGoblinPath = "res://assets/sprites/enemies/goblin/sprite_sheet.png";
        string legacyGoblinPath = "res://assets/sprites/characters/enemy_goblin/sprite_sheet.png";
        if (FileAccess.FileExists(newGoblinPath))
        {
            enemyTexture = GD.Load<Texture2D>(newGoblinPath);
        }
        else if (FileAccess.FileExists(legacyGoblinPath))
        {
            enemyTexture = GD.Load<Texture2D>(legacyGoblinPath);
        }
        if (enemyTexture != null)
        {
            enemySpriteFrames.AddAnimation("idle");

            // Derive frame size dynamically from texture (4 frames horizontally)
            var eSize = enemyTexture.GetSize();
            int ENEMY_FRAME_W = Mathf.Max(1, Mathf.RoundToInt(eSize.X) / 4);
            int ENEMY_FRAME_H = Mathf.Max(1, Mathf.RoundToInt(eSize.Y));
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = enemyTexture;
                atlasTexture.Region = new Rect2(i * ENEMY_FRAME_W, 0, ENEMY_FRAME_W, ENEMY_FRAME_H);
                atlasTexture.FilterClip = true;
                enemySpriteFrames.AddFrame("idle", atlasTexture);
            }

            enemySpriteFrames.SetAnimationSpeed("idle", 4.0);
            enemySpriteFrames.SetAnimationLoop("idle", true);
            _enemySprite.SpriteFrames = enemySpriteFrames;
            // Keep on-screen size ~96px width regardless of source resolution
            float eScale = 96f / (float)ENEMY_FRAME_W;
            _enemySprite.Scale = new Vector2(eScale, eScale);
            _enemySprite.Modulate = new Color(1, 1, 1, 1);
            _enemySprite.Play("idle");

            var enemyMaterial = new CanvasItemMaterial();
            enemyMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            enemyMaterial.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _enemySprite.Material = enemyMaterial;

            _enemySprite.SelfModulate = new Color(1, 1, 1, 1);
            _enemySprite.Visible = true;
            _enemySprite.Centered = true;
        }
        else
        {
            // TODO: use _enemy.EnemyType to select the correct sprite path (currently always loads goblin)
            GD.PushWarning("[BattleManager] Enemy sprite sheet not found; using fallback rendering.");
            // Check if there are sprite files that need to be merged
            CheckAndCreateSpriteSheet();
        }
    }
    
    private void CheckAndCreateSpriteSheet()
    {
        // Check if individual sprite frames exist for goblin
        string newGoblinDir = "res://assets/sprites/enemies/goblin/";
        string legacyGoblinDir = "res://assets/sprites/characters/enemy_goblin/";
        string goblinDir = DirAccess.DirExistsAbsolute(newGoblinDir) ? newGoblinDir : legacyGoblinDir;
        if (DirAccess.DirExistsAbsolute(goblinDir))
        {
            GD.Print($"Goblin sprite directory exists: {goblinDir}");
            GD.Print("You may need to run: python3 tools/sprite_sheet_merger.py");
        }
    }
    
    private void UpdateUI()
    {
        if (_playerLevelLabel != null && _player != null)
        {
            _playerLevelLabel.Text = $"Lv: {_player.Level}";
        }

        if (_playerHealthLabel != null && _player != null)
        {
            _playerHealthLabel.Text = $"HP: {_player.CurrentHealth}/{_player.GetEffectiveMaxHealth()}";
        }

        if (_playerAttackLabel != null && _player != null)
        {
            _playerAttackLabel.Text = $"ATK: {_player.GetEffectiveAttack()}";
        }

        if (_playerDefenseLabel != null && _player != null)
        {
            _playerDefenseLabel.Text = $"DEF: {_player.GetEffectiveDefense()}";
        }

        if (_playerSpeedLabel != null && _player != null)
        {
            _playerSpeedLabel.Text = $"SPD: {_player.GetEffectiveSpeed()}";
        }

        if (_enemyLevelLabel != null && _enemy != null)
        {
            _enemyLevelLabel.Text = $"Lv: {_enemy.Level}";
        }

        if (_enemyHealthLabel != null && _enemy != null)
        {
            _enemyHealthLabel.Text = $"HP: {_enemy.CurrentHealth}/{_enemy.MaxHealth}";
        }

        if (_enemyAttackLabel != null && _enemy != null)
        {
            _enemyAttackLabel.Text = $"ATK: {_enemy.GetEffectiveAttack()}";
        }

        if (_enemyDefenseLabel != null && _enemy != null)
        {
            _enemyDefenseLabel.Text = $"DEF: {_enemy.GetEffectiveDefense()}";
        }

        if (_enemySpeedLabel != null && _enemy != null)
        {
            _enemySpeedLabel.Text = $"SPD: {_enemy.GetEffectiveSpeed()}";
        }

        // Enable/disable buttons based on turn (all disabled in auto-battle)
        if (_attackButton != null) _attackButton.Disabled = true;
        if (_defendButton != null) _defendButton.Disabled = true;
        if (_runButton != null) _runButton.Disabled = true;

        // Status effect text labels (created dynamically; graceful no-op if absent)
        UpdateStatusLabel(ref _playerStatusLabel,
            "BattleContent/BattleArena/LeftSide/PlayerStatsContainer",
            _player?.ActiveBuffs);
        UpdateStatusLabel(ref _enemyStatusLabel,
            "BattleContent/BattleArena/RightSide/EnemyStatsContainer",
            _enemy?.ActiveStatusEffects);
    }

    // -------------------------------------------------------------------------
    // Status effect UI helpers
    // -------------------------------------------------------------------------

    private Label _playerStatusLabel;
    private Label _enemyStatusLabel;

    /// <summary>
    /// Lazily creates a status label as a child of the given container path if one
    /// doesn't exist yet, then updates its text. Uses GetNodeOrNull so it is safe
    /// when the scene node is absent.
    /// </summary>
    private void UpdateStatusLabel(ref Label labelRef, string containerPath, StatusEffectSet? effects)
    {
        if (effects == null) return;

        var container = GetNodeOrNull<Godot.Container>(containerPath);
        if (container == null) return;

        if (labelRef == null || !Godot.GodotObject.IsInstanceValid(labelRef))
        {
            labelRef = new Label { Name = "StatusEffectLabel" };
            labelRef.HorizontalAlignment = Godot.HorizontalAlignment.Left;
            container.AddChild(labelRef);
        }

        string text = BuildStatusText(effects);
        labelRef.Text    = text;
        labelRef.Visible = text.Length > 0;
    }

    private static string BuildStatusText(StatusEffectSet effects)
    {
        if (!effects.HasAny) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var eff in effects.Effects)
        {
            string tag = eff.Type switch
            {
                StatusEffectType.Poison   => "PSN",
                StatusEffectType.Burn     => "BRN",
                StatusEffectType.Stun     => "STN",
                StatusEffectType.Weaken   => "WKN",
                StatusEffectType.Slow     => "SLW",
                StatusEffectType.Blind    => "BLD",
                StatusEffectType.Regen    => "RGN",
                StatusEffectType.Haste    => "HST",
                StatusEffectType.Strength => "STR",
                StatusEffectType.Fortify  => "FRT",
                _                         => "???",
            };
            sb.Append($"[{tag} {eff.TurnsRemaining}t] ");
        }
        return sb.ToString().TrimEnd();
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
            // Stun check: stunned player loses their action but still ticks
            if (_player.ActiveBuffs.IsStunned)
            {
                GD.Print($"[BattleManager] {_player.Name} is Stunned and loses their turn!");
            }
            else
            {
                PlayerAutoAction();
            }

            // Tick player status effects (DoT, HoT, duration countdown)
            var (expiredPlayer, dotPlayer, hotPlayer) = _player.ActiveBuffs.Tick();
            if (dotPlayer > 0)
            {
                _player.CurrentHealth = Godot.Mathf.Max(0, _player.CurrentHealth - dotPlayer);
                GD.Print($"[BattleManager] {_player.Name} takes {dotPlayer} status damage!");
                ShowDamageNumber(_playerDamageLabel, dotPlayer);
            }
            if (hotPlayer > 0 && _player.IsAlive)
            {
                _player.Heal(hotPlayer);
                GD.Print($"[BattleManager] {_player.Name} regenerates {hotPlayer} HP!");
            }
            foreach (var eff in expiredPlayer)
                GD.Print($"[BattleManager] Status effect expired: {eff.Type} on {_player.Name}");
        }
        else
        {
            // Stun check: stunned enemy loses their action but still ticks
            if (_enemy.ActiveStatusEffects.IsStunned)
            {
                GD.Print($"[BattleManager] {_enemy.Name} is Stunned and loses their turn!");
            }
            else
            {
                EnemyTurn(_playerDefendedLastTurn);
            }
            _playerDefendedLastTurn = false;

            // Tick enemy status effects
            var (expiredEnemy, dotEnemy, hotEnemy) = _enemy.ActiveStatusEffects.Tick();
            if (dotEnemy > 0)
            {
                _enemy.CurrentHealth = Godot.Mathf.Max(0, _enemy.CurrentHealth - dotEnemy);
                GD.Print($"[BattleManager] {_enemy.Name} takes {dotEnemy} status damage!");
                ShowDamageNumber(_enemyDamageLabel, dotEnemy);
            }
            if (hotEnemy > 0 && _enemy.IsAlive)
            {
                _enemy.CurrentHealth = Godot.Mathf.Min(_enemy.MaxHealth, _enemy.CurrentHealth + hotEnemy);
                GD.Print($"[BattleManager] {_enemy.Name} regenerates {hotEnemy} HP!");
            }
            foreach (var eff in expiredEnemy)
                GD.Print($"[BattleManager] Status effect expired: {eff.Type} on {_enemy.Name}");
        }

        // Action point system: speed determines turn frequency, not just initial priority
        // Accumulate action points based on effective speed each turn
        _playerActionPoints += _player.GetEffectiveSpeed();
        _enemyActionPoints += _enemy.GetEffectiveSpeed();

        // Determine who acts next based on accumulated action points
        bool justActed = _playerTurn;
        if (_playerActionPoints >= _enemyActionPoints)
        {
            _playerTurn = true;
            _playerActionPoints -= ACTION_POINT_THRESHOLD;
        }
        else
        {
            _playerTurn = false;
            _enemyActionPoints -= ACTION_POINT_THRESHOLD;
        }
        _playerActedLast = justActed; // Track who actually acted, not who's next
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
        // Player auto-AI: defends with 30% probability when health drops below 40%, otherwise attacks.
        float healthPercentage = (float)_player.CurrentHealth / _player.GetEffectiveMaxHealth();
        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        
        // More likely to defend when health is low
        if (healthPercentage < 0.4f && GD.Randf() < 0.3f)
        {
            GD.Print($"{_player.Name} takes a defensive stance!");
            _playerDefendedLastTurn = true;
            return;
        }
        
        // Aggressive attack when enemy is low on health
        if (enemyHealthPercentage < 0.3f)
        {
            GD.Print($"{_player.Name} goes for a finishing blow!");
        }
        
        // Otherwise, normal attack
        PlayerAttack();
    }
    
    private void PlayerAttack()
    {
        // Blind miss check: Blind reduces accuracy to 55% (GetAccuracyMultiplier returns 1.0 when not blind)
        if (GD.Randf() > _player.ActiveBuffs.GetAccuracyMultiplier())
        {
            GD.Print($"{_player.Name} is Blinded and misses the attack!");
            return;
        }

        // Add some variation to attacks
        bool criticalHit = GD.Randf() < 0.15f; // 15% chance for critical hit
        int baseDamage = _player.GetEffectiveAttack() + GD.RandRange(-5, 5);
        
        if (criticalHit)
        {
            baseDamage = (int)(baseDamage * 1.5f);
            GD.Print($"Critical hit! {_player.Name} deals {baseDamage} damage!");
        }
        else
        {
            GD.Print($"{_player.Name} attacks for {baseDamage} damage!");
        }
        
        baseDamage = Mathf.Max(1, baseDamage);
        _enemy.TakeDamage(baseDamage);
        
        // Show damage number on enemy
        ShowDamageNumber(_enemyDamageLabel, baseDamage, criticalHit);
        
        // Play attack animation (flash the player sprite)
        PlayAttackAnimation(_playerSprite);
    }
    
    private void EnemyTurn(bool playerDefended = false)
    {
        if (!_enemy.IsAlive || !_player.IsAlive) return;

        // Blind miss check: Blind reduces accuracy to 55% (GetAccuracyMultiplier returns 1.0 when not blind)
        if (GD.Randf() > _enemy.ActiveStatusEffects.GetAccuracyMultiplier())
        {
            GD.Print($"{_enemy.Name} is Blinded and misses the attack!");
            return;
        }

        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        // Note: uses base MaxHealth (not GetEffectiveMaxHealth()); equipment bonuses are not reflected in this threshold.
        float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;

        // Enemy AI: More aggressive when player is low on health
        bool aggressiveAttack = playerHealthPercentage < 0.3f && GD.Randf() < 0.4f;
        bool criticalHit = GD.Randf() < 0.1f; // 10% chance for enemy critical hit

        int damage = _enemy.GetEffectiveAttack() + GD.RandRange(-3, 3);
        
        if (aggressiveAttack)
        {
            damage = (int)(damage * 1.3f);
            GD.Print($"{_enemy.Name} attacks ferociously!");
        }
        else if (criticalHit)
        {
            damage = (int)(damage * 1.4f);
            GD.Print($"Critical hit! {_enemy.Name} strikes hard!");
        }
        
        if (playerDefended)
        {
            damage = damage / 2;
            GD.Print($"The attack is weakened by {_player.Name}'s defense!");
        }
        
        damage = Mathf.Max(1, damage);
        _player.TakeDamage(damage);
        
        if (!aggressiveAttack && !criticalHit)
        {
            GD.Print($"{_enemy.Name} attacks for {damage} damage!");
        }
        
        // Show damage number on player
        ShowDamageNumber(_playerDamageLabel, damage, criticalHit);

        // Play attack animation (flash the enemy sprite)
        PlayAttackAnimation(_enemySprite);

        // Attempt to apply a debuff from this enemy's profile
        TryApplyEnemyDebuff();
    }

    private void TryApplyEnemyDebuff()
    {
        var abilities = EnemyDebuffProfile.GetAbilities(_enemy?.EnemyType);
        if (abilities == null) return;

        foreach (var ability in abilities)
        {
            if (GD.Randf() < ability.Chance)
            {
                _player.ActiveBuffs.Add(new ActiveStatusEffect(ability.EffectType, ability.Magnitude, ability.Duration));
                GD.Print($"[BattleManager] {_enemy.Name} inflicts {ability.EffectType} on {_player.Name} ({ability.Duration} turns)!");
            }
        }
    }

    private void EndBattle(bool playerWon)
    {
        GD.Print($"BattleManager.EndBattle called: playerWon = {playerWon}");

        _battleInProgress = false;
        _battleTimer.Stop();
        _player?.ActiveBuffs.Clear();
        _enemy?.ActiveStatusEffects.Clear();

        // Add spacing and clear result display
        GD.Print("=== BATTLE RESULT ===");

        if (playerWon)
        {
            GD.Print($"🎉 VICTORY! {_player.Name} wins the battle!");
            GD.Print($"Experience gained: {_enemy.ExperienceReward} XP");
            GD.Print($"Gold gained: {_enemy.GoldReward} Gold");
            
            int oldLevel = _player.Level;
            _player.GainExperience(_enemy.ExperienceReward);
            _player.GainGold(_enemy.GoldReward);
            
            // Check if player leveled up
            if (_player.Level > oldLevel)
            {
                GD.Print($"⭐ LEVEL UP! {_player.Name} reached level {_player.Level}!");
                GD.Print($"New stats: HP {_player.MaxHealth}, ATK {_player.Attack}, DEF {_player.Defense}");
            }

            // Roll and award loot
            var lootTable = LootTableCatalog.GetByEnemyType(_enemy.EnemyType);
            if (lootTable == null)
            {
                GD.PushWarning($"[BattleManager] No LootTable found for enemy type '{_enemy.EnemyType}'. Skipping loot roll.");
            }

            var lootResult = lootTable == null
                ? LootResult.Empty
                : LootManager.RollLoot(lootTable, _rng);
            if (lootResult.HasDrops)
            {
                LootManager.AwardLootToCharacter(lootResult, _player);
                GD.Print("--- Loot Drops ---");
                foreach (var drop in lootResult.DroppedItems)
                {
                    GD.Print($"  {drop.Quantity}x {drop.Item.DisplayName}");
                }
                GD.Print("------------------");
                _pendingLootDisplay = lootResult;
                CallDeferred(nameof(ShowPendingLootDisplay));
            }
            else
            {
                GD.Print("No loot dropped.");
            }
        }
        else
        {
            GD.Print($"💀 DEFEAT! {_player.Name} was defeated by {_enemy.Name}...");
            GD.Print("Game Over - You will return to the main menu.");
        }
        
        GD.Print("=====================");
        
        // Show the close button
        GetOkButton().Visible = true;
        GetOkButton().Text = "Continue";
        
        // Emit the signal immediately instead of waiting
        GD.Print("BattleManager emitting BattleFinished signal immediately");
        _resultEmitted = true;
        EmitSignal(SignalName.BattleFinished, playerWon, false); // false for not escaped
    }
    
    private void EndBattleWithEscape()
    {
        GD.Print("BattleManager.EndBattleWithEscape called: Player escaped");

        _battleInProgress = false;
        _battleTimer.Stop();
        _player?.ActiveBuffs.Clear();
        _enemy?.ActiveStatusEffects.Clear();

        // Add spacing and clear result display
        GD.Print("=== BATTLE RESULT ===");
        GD.Print($"🏃 ESCAPED! {_player.Name} fled from battle!");
        GD.Print("No experience gained from escaping.");
        GD.Print("=====================");
        
        // Show the close button
        GetOkButton().Visible = true;
        GetOkButton().Text = "Continue";
        
        // Emit the signal immediately instead of waiting
        GD.Print("BattleManager emitting BattleFinished signal with escape immediately");
        _resultEmitted = true;
        EmitSignal(SignalName.BattleFinished, false, true); // false for not won, true for escaped
    }
    
    private void ShowPendingLootDisplay()
    {
        if (_pendingLootDisplay == null || !_pendingLootDisplay.HasDrops)
            return;

        if (!IsInsideTree() || !IsInstanceValid(this))
        {
            GD.PushWarning("[BattleManager] ShowPendingLootDisplay: dialog no longer in scene tree; skipping loot UI.");
            _pendingLootDisplay = null;
            return;
        }

        var loot = _pendingLootDisplay;
        _pendingLootDisplay = null; // Clear before calling to prevent re-entry on exception
        ShowLootDisplay(loot);
    }

    private void ShowDamageNumber(Label damageLabel, int damage, bool isCritical = false)
    {
        // Set damage text
        damageLabel.Text = $"-{damage}";
        
        // Set color based on critical hit
        if (isCritical)
        {
            damageLabel.Modulate = new Color(1, 1, 0, 1); // Yellow for critical
        }
        else
        {
            damageLabel.Modulate = new Color(1, 0, 0, 1); // Red for normal damage
        }
        
        // Create tween for damage number animation
        var tween = CreateTween();
        tween.SetParallel(true);
        
        // Animate position (move up)
        var startPos = damageLabel.Position;
        var endPos = startPos + new Vector2(0, -30);
        tween.TweenProperty(damageLabel, "position", endPos, 1.0);
        
        // Animate opacity (fade out)
        tween.TweenProperty(damageLabel, "modulate:a", 0.0f, 1.0);
        
        // Reset position and hide when animation is done
        tween.TweenCallback(Callable.From(() => {
            damageLabel.Position = startPos;
            damageLabel.Modulate = new Color(1, 0, 0, 0);
        })).SetDelay(1.0);
    }
    
    private void ShowLootDisplay(LootResult lootResult)
    {
        var battleContent = GetNodeOrNull<VBoxContainer>("BattleContent");
        if (battleContent == null)
        {
            GD.PrintErr("[BattleManager] ShowLootDisplay: 'BattleContent' VBoxContainer not found; loot UI will not be shown.");
            return;
        }

        _lootLabel = new Label();
        _lootLabel.HorizontalAlignment = HorizontalAlignment.Center;

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("--- Loot ---");
        foreach (var drop in lootResult.DroppedItems)
        {
            string rarityTag = drop.Rarity > ItemRarity.Common ? $" [{drop.Rarity}]" : "";
            lines.AppendLine($"{drop.Quantity}x {drop.Item.DisplayName}{rarityTag}");
        }
        _lootLabel.Text = lines.ToString().TrimEnd();

        battleContent.AddChild(_lootLabel);
    }

    private void PlayAttackAnimation(AnimatedSprite2D sprite)
    {
        // Create a quick flash effect for attack
        var tween = CreateTween();
        tween.SetParallel(true);
        
        // Get current scale (should be 3.0f) and scale up slightly from that
        var currentScale = sprite.Scale;
        var attackScale = currentScale * 1.2f;
        
        // Scale up slightly and back
        tween.TweenProperty(sprite, "scale", attackScale, 0.1);
        tween.TweenProperty(sprite, "scale", currentScale, 0.1).SetDelay(0.1);
        
        // Flash white
        tween.TweenProperty(sprite, "modulate", new Color(2, 2, 2, 1), 0.1);
        tween.TweenProperty(sprite, "modulate", new Color(1, 1, 1, 1), 0.1).SetDelay(0.1);
    }
}
