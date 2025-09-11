using Godot;

public partial class BattleManager : AcceptDialog
{
    [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, bool playerEscaped);
    
    private Character _player;
    private Enemy _enemy;
    private bool _playerTurn = true;
    
    // UI References
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private Label _playerAttackLabel;
    private Label _playerDefenseLabel;
    private Label _enemyLevelLabel;
    private Label _enemyHealthLabel;
    private Label _enemyAttackLabel;
    private Label _enemyDefenseLabel;
    private Button _attackButton;
    private Button _defendButton;
    private Button _runButton;
    
    // Animation and Visual References
    private AnimatedSprite2D _playerSprite;
    private AnimatedSprite2D _enemySprite;
    private Label _playerDamageLabel;
    private Label _enemyDamageLabel;
    
    // Auto-battle properties
    private Timer _battleTimer;
    private bool _battleInProgress = false;
    private bool _playerDefendedLastTurn = false;
    
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
        _playerLevelLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerLevel");
        _playerHealthLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerHealth");
        _playerAttackLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerAttack");
        _playerDefenseLabel = GetNode<Label>("BattleContent/BattleArena/LeftSide/PlayerStatsContainer/PlayerDefense");
        _attackButton = GetNode<Button>("BattleContent/ActionButtons/AttackButton");
        _defendButton = GetNode<Button>("BattleContent/ActionButtons/DefendButton");
        _runButton = GetNode<Button>("BattleContent/ActionButtons/RunButton");

        // Verify all UI elements are loaded
        if (_enemyLevelLabel == null) GD.PrintErr("ERROR: EnemyLevelLabel not found!");
        if (_playerLevelLabel == null) GD.PrintErr("ERROR: PlayerLevelLabel not found!");
        if (_attackButton == null) GD.PrintErr("ERROR: AttackButton not found!");
        if (_defendButton == null) GD.PrintErr("ERROR: DefendButton not found!");
        if (_runButton == null) GD.PrintErr("ERROR: RunButton not found!");

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
        
        // Hide manual action buttons since combat is now automated
        _attackButton.Visible = false;
        _defendButton.Visible = false;
        _runButton.Visible = false;
        
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
        
        // Connect the close request signal
        CloseRequested += OnCloseRequested;
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
            GD.Print("✅ Battle background loaded successfully");
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
            colorRect.Color = new Color(1.0f, 0.0f, 1.0f, 1.0f); // Bright magenta for testing transparency
            AddChild(colorRect);
            MoveChild(colorRect, 0);
            GD.Print("⚠️ Battle background not found, using fallback color (bright magenta for transparency testing)");
            return;
        }
        
        // Add the background as the first child (so it appears behind everything)
        AddChild(backgroundRect);
        MoveChild(backgroundRect, 0);
    }
    
    private void OnCloseRequested()
    {
        // In auto-battle mode, closing the dialog just stops the battle 
        // (no escape mechanics since it's automated)
        if (_battleInProgress && _player != null && _enemy != null && _player.IsAlive && _enemy.IsAlive)
        {
            GD.Print("Battle interrupted!");
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
        
        // Setup character animations
        SetupCharacterAnimations();
        
        GD.Print($"Battle begins! {_player.Name} vs {_enemy.Name}");
        GD.Print($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first!");
        GD.Print("Auto-battle mode: Combat will proceed automatically!");
        
        UpdateUI();
        
        // Start the auto-battle timer
        _battleTimer.Start();
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
            GD.Print("🎮 Player texture loaded:");
            GD.Print($"   📏 Size: {playerTexture.GetSize()}");
            GD.Print($"   🔗 Resource ID: {playerTexture.GetRid()}");
            GD.Print($"   🎨 Texture is null: {playerTexture == null}");
            GD.Print($"   📊 Texture resource path: {playerTexture.ResourcePath}");

            playerSpriteFrames.AddAnimation("idle");

            // Add frames from sprite sheet (4 frames, 32x32 each)
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = playerTexture;
                atlasTexture.Region = new Rect2(i * 32, 0, 32, 32);
                // Ensure transparency is preserved
                atlasTexture.FilterClip = true;
                playerSpriteFrames.AddFrame("idle", atlasTexture);
                GD.Print($"   ✅ Frame {i} added to animation");
            }

            playerSpriteFrames.SetAnimationSpeed("idle", 4.0);
            playerSpriteFrames.SetAnimationLoop("idle", true);
            _playerSprite.SpriteFrames = playerSpriteFrames;
            _playerSprite.Scale = new Vector2(3.0f, 3.0f); // Make sprite 3x larger
            // Ensure sprite uses transparency
            _playerSprite.Modulate = new Color(1, 1, 1, 1); // Reset modulate to ensure transparency works
            _playerSprite.Play("idle");

            // Force material for transparency
            var material = new CanvasItemMaterial();
            material.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            material.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _playerSprite.Material = material;

            // Try to force self-modulate for transparency
            _playerSprite.SelfModulate = new Color(1, 1, 1, 1);

            // Force sprite to be visible and centered
            _playerSprite.Visible = true;
            _playerSprite.Centered = true;

            GD.Print("✅ Player sprite loaded with transparency support");
            GD.Print($"   🎭 SpriteFrames assigned: {_playerSprite.SpriteFrames != null}");
            GD.Print($"   🎬 Animation playing: {_playerSprite.IsPlaying()}");
            GD.Print($"   👁️  Sprite visible: {_playerSprite.Visible}");
            GD.Print($"   📍 Sprite position: {_playerSprite.Position}");
            GD.Print($"   📏 Sprite scale: {_playerSprite.Scale}");
        }
        else
        {
            GD.Print("Warning: Player sprite sheet not found, using fallback");
        }

        // Create animation resources for enemy
        var enemySpriteFrames = new SpriteFrames();

        // Load enemy sprite sheet and create animation - with fallback
        var enemyTexture = GD.Load<Texture2D>("res://assets/sprites/characters/enemy_goblin/sprite_sheet.png");
        if (enemyTexture != null)
        {
            GD.Print("👹 Enemy texture loaded:");
            GD.Print($"   📏 Size: {enemyTexture.GetSize()}");
            GD.Print($"   🔗 Resource ID: {enemyTexture.GetRid()}");
            GD.Print($"   🎨 Texture is null: {enemyTexture == null}");
            GD.Print($"   📊 Texture resource path: {enemyTexture.ResourcePath}");

            enemySpriteFrames.AddAnimation("idle");

            // Add frames from sprite sheet (4 frames, 32x32 each)
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = enemyTexture;
                atlasTexture.Region = new Rect2(i * 32, 0, 32, 32);
                // Ensure transparency is preserved
                atlasTexture.FilterClip = true;
                enemySpriteFrames.AddFrame("idle", atlasTexture);
            }

            enemySpriteFrames.SetAnimationSpeed("idle", 4.0);
            enemySpriteFrames.SetAnimationLoop("idle", true);
            _enemySprite.SpriteFrames = enemySpriteFrames;
            _enemySprite.Scale = new Vector2(3.0f, 3.0f); // Make sprite 3x larger
            // Ensure sprite uses transparency
            _enemySprite.Modulate = new Color(1, 1, 1, 1); // Reset modulate to ensure transparency works
            _enemySprite.Play("idle");

            // Force material for transparency
            var enemyMaterial = new CanvasItemMaterial();
            enemyMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            enemyMaterial.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _enemySprite.Material = enemyMaterial;

            // Try to force self-modulate for transparency
            _enemySprite.SelfModulate = new Color(1, 1, 1, 1);

            // Force sprite to be visible and centered
            _enemySprite.Visible = true;
            _enemySprite.Centered = true;

            GD.Print("✅ Enemy sprite loaded with transparency support");
            GD.Print($"   🎭 SpriteFrames assigned: {_enemySprite.SpriteFrames != null}");
            GD.Print($"   🎬 Animation playing: {_enemySprite.IsPlaying()}");
            GD.Print($"   👁️  Sprite visible: {_enemySprite.Visible}");
            GD.Print($"   📍 Sprite position: {_enemySprite.Position}");
            GD.Print($"   📏 Sprite scale: {_enemySprite.Scale}");
        }
        else
        {
            GD.Print("Warning: Enemy goblin sprite sheet not found, using fallback");
            // Check if there are sprite files that need to be merged
            CheckAndCreateSpriteSheet();
        }
    }
    
    private void CheckAndCreateSpriteSheet()
    {
        // Check if individual sprite frames exist for goblin
        string goblinDir = "res://assets/sprites/characters/enemy_goblin/";
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
            _playerHealthLabel.Text = $"HP: {_player.CurrentHealth}/{_player.MaxHealth}";
        }

        if (_playerAttackLabel != null && _player != null)
        {
            _playerAttackLabel.Text = $"ATK: {_player.Attack}";
        }

        if (_playerDefenseLabel != null && _player != null)
        {
            _playerDefenseLabel.Text = $"DEF: {_player.Defense}";
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
            _enemyAttackLabel.Text = $"ATK: {_enemy.Attack}";
        }

        if (_enemyDefenseLabel != null && _enemy != null)
        {
            _enemyDefenseLabel.Text = $"DEF: {_enemy.Defense}";
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
        // Add some variation to attacks
        bool criticalHit = GD.Randf() < 0.15f; // 15% chance for critical hit
        int baseDamage = _player.Attack + GD.RandRange(-5, 5);
        
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
        
        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;
        
        // Enemy AI: More aggressive when player is low on health
        bool aggressiveAttack = playerHealthPercentage < 0.3f && GD.Randf() < 0.4f;
        bool criticalHit = GD.Randf() < 0.1f; // 10% chance for enemy critical hit
        
        int damage = _enemy.Attack + GD.RandRange(-3, 3);
        
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
    }
    
    private void EndBattle(bool playerWon)
    {
        GD.Print($"BattleManager.EndBattle called: playerWon = {playerWon}");
        
        _battleInProgress = false;
        _battleTimer.Stop();
        
        // Add spacing and clear result display
        GD.Print("=== BATTLE RESULT ===");
        
        if (playerWon)
        {
            GD.Print($"🎉 VICTORY! {_player.Name} wins the battle!");
            GD.Print($"Experience gained: {_enemy.ExperienceReward} XP");
            
            int oldLevel = _player.Level;
            _player.GainExperience(_enemy.ExperienceReward);
            
            // Check if player leveled up
            if (_player.Level > oldLevel)
            {
                GD.Print($"⭐ LEVEL UP! {_player.Name} reached level {_player.Level}!");
                GD.Print($"New stats: HP {_player.MaxHealth}, ATK {_player.Attack}, DEF {_player.Defense}");
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
        EmitSignal(SignalName.BattleFinished, playerWon, false); // false for not escaped
    }
    
    private void EndBattleWithEscape()
    {
        GD.Print("BattleManager.EndBattleWithEscape called: Player escaped");
        
        _battleInProgress = false;
        _battleTimer.Stop();
        
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
        EmitSignal(SignalName.BattleFinished, false, true); // false for not won, true for escaped
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
