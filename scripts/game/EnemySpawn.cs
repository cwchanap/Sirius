using Godot;

[Tool]
public partial class EnemySpawn : Sprite2D
{
    [Export] public Vector2I GridPosition { get; set; } = new Vector2I(0, 0);
    // Editor-only: allow designers to toggle snap behavior. Default OFF so dragging works naturally.
    [Export] public bool EditorSnapEnabled { get; set; } = false;
    
    /// <summary>
    /// If true, automatically duplicates the Blueprint when this node enters the scene tree,
    /// giving each spawn unique stats without manual intervention.
    /// Useful when you want every enemy to have independent stats.
    /// </summary>
    [Export] public bool AutoMakeBlueprintUnique { get; set; } = false;

    /// <summary>
    /// Blueprint defining enemy stats (Level, HP, Atk, Def, Spd, Exp, Gold).
    /// Create .tres resources in Godot editor and assign here. If null, uses legacy EnemyType string.
    /// Note: Stored as Resource to avoid casting issues during Godot serialization.
    /// 
    /// IMPORTANT: To make this spawn have unique stats:
    /// 1. Select the spawn node in the scene tree
    /// 2. In Inspector, click the dropdown next to Blueprint
    /// 3. Choose "Make Unique" or "Duplicate"
    /// 4. Now you can edit stats without affecting other spawns
    /// </summary>
    private Resource _blueprint;
    
    [Export]
    public Resource Blueprint
    {
        get => _blueprint;
        set
        {
            _blueprint = value;
            // Reload sprite when blueprint is assigned (especially in editor)
            if (IsNodeReady())
            {
                CallDeferred(nameof(ReloadSprite));
            }
        }
    }
    
    /// <summary>
    /// Editor helper: Makes this spawn's Blueprint unique so stats can be customized per-instance.
    /// Call this in the editor or via code to create an independent copy of the blueprint.
    /// </summary>
    public void MakeBlueprintUnique()
    {
        if (Blueprint != null)
        {
            Blueprint = (Resource)Blueprint.Duplicate();
            GD.Print($"[EnemySpawn] Blueprint made unique for spawn at {GridPosition}");
        }
    }
    
    private void ReloadSprite()
    {
        GD.Print($"EnemySpawn.ReloadSprite called for Blueprint: {(Blueprint != null ? "assigned" : "null")}");
        TryLoadSpriteTexture();
        
        // Re-enable region if we now have a texture
        RegionEnabled = Texture != null;
        if (RegionEnabled)
        {
            RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
        }
        
        // Update scale to match cell size (use default 32 if GridMap not available in editor)
        if (FrameWidth > 0 && FrameHeight > 0)
        {
            int cell = _gridMap != null ? _gridMap.CellSize : 32;
            Scale = new Vector2(cell / (float)FrameWidth, cell / (float)FrameHeight);
        }
        
        QueueRedraw(); // Force visual update
        
        // Update position if in a scene with GridMap
        if (_gridMap != null)
        {
            UpdateVisual(_gridMap);
        }
    }
    
    // Override _Set to detect property changes in the editor
    public override bool _Set(StringName property, Variant value)
    {
        if (Engine.IsEditorHint() && property == PropertyName.Blueprint)
        {
            Blueprint = value.As<Resource>();
            
            // Reload sprite immediately in editor when Blueprint changes
            CallDeferred(MethodName.ReloadSprite);
            NotifyPropertyListChanged();
            return true;
        }
        return base._Set(property, value);
    }

    // LEGACY SUPPORT: Leave empty to auto-pick by area rules. Otherwise set to a known type name.
    // Prefer using Blueprint property instead for full stat control.
    // goblin, orc, skeleton_warrior, troll, dragon, forest_spirit, cave_spider,
    // desert_scorpion, swamp_wretch, mountain_wyvern, dark_mage, dungeon_guardian,
    // demon_lord, boss
    [Export(PropertyHint.Enum, "goblin,orc,skeleton_warrior,troll,dragon,forest_spirit,cave_spider,desert_scorpion,swamp_wretch,mountain_wyvern,dark_mage,dungeon_guardian,demon_lord,boss")] 
    public string EnemyType { get; set; } = string.Empty;

    private GridMap _gridMap;
    private Vector2 _mapOffset = Vector2.Zero;

    // Frame size will be derived from texture (defaults assume 96x96 high-res sheets)
    private int FrameWidth = 96;
    private int FrameHeight = 96;
    private int _currentFrame = 0;
    private float _animTimer = 0f;
    private const float FrameTime = 0.2f; // 5 FPS

    public override void _EnterTree()
    {
        GD.Print($"[EnemySpawn] _EnterTree called (IsEditorHint={Engine.IsEditorHint()})");
        
        // Auto-duplicate blueprint if requested (makes each spawn have unique stats)
        if (AutoMakeBlueprintUnique && Blueprint != null)
        {
            MakeBlueprintUnique();
        }
        
        // Load sprite early for editor display
        if (Engine.IsEditorHint())
        {
            // Give the editor time to deserialize the Blueprint property
            CallDeferred(MethodName.LoadSpriteForEditor);
        }
    }
    
    private void LoadSpriteForEditor()
    {
        GD.Print($"[EnemySpawn] LoadSpriteForEditor called, Blueprint: {Blueprint?.GetType().Name ?? "null"}");
        TryLoadSpriteTexture();
        
        Centered = true;
        RegionEnabled = Texture != null;
        if (RegionEnabled)
        {
            RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
        }
        
        // Use default cell size in editor
        if (FrameWidth > 0 && FrameHeight > 0)
        {
            Scale = new Vector2(32 / (float)FrameWidth, 32 / (float)FrameHeight);
        }
        
        ZIndex = 2;
        QueueRedraw();
    }

    public override void _Ready()
    {
        // Ensure this node is discoverable by GridMap
        if (!IsInGroup("EnemySpawn"))
        {
            AddToGroup("EnemySpawn");
        }
        
        GD.Print($"EnemySpawn._Ready() at position {GridPosition}, Blueprint: {(Blueprint != null ? "assigned" : "null")}");

        // Find GridMap up the tree
        _gridMap = GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap");
        if (_gridMap == null)
        {
            // Try searching the scene tree as a fallback
            _gridMap = GetTree().Root.GetNodeOrNull<GridMap>("**/GridMap");
        }
        
        if (_gridMap != null)
        {
            GD.Print($"EnemySpawn found GridMap: {_gridMap.Name}");
        }
        else
        {
            GD.PrintErr($"EnemySpawn at {GridPosition} could not find GridMap!");
        }

        // Read TileMapLayer offset so sprite aligns with baked layers that are shifted at runtime
        var ground = _gridMap?.GetNodeOrNull<TileMapLayer>("GroundLayer");
        _mapOffset = ground != null ? ground.Position : Vector2.Zero;

        // Load sprite texture if not already loaded (runtime or editor)
        if (!Engine.IsEditorHint())
        {
            TryLoadSpriteTexture();
        }

        Centered = true;
        RegionEnabled = Texture != null; // use region when sprite sheet available
        if (RegionEnabled)
        {
            RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
        }

        // Keep on-screen size equal to one grid cell regardless of frame resolution
        int cell = _gridMap != null ? _gridMap.CellSize : 32;
        if (FrameWidth > 0 && FrameHeight > 0)
        {
            Scale = new Vector2(cell / (float)FrameWidth, cell / (float)FrameHeight);
        }

        // Position initially
        UpdateVisual(_gridMap);

        // Process in both editor (for placement) and runtime (for animation)
        SetProcess(true);
        ZIndex = 2; // Above walls (z_index = 1)
    }

    private void TryLoadSpriteTexture()
    {
        GD.Print($"[EnemySpawn] TryLoadSpriteTexture called (IsEditorHint={Engine.IsEditorHint()})");
        
        // Determine sprite type from Blueprint first, fallback to legacy EnemyType string
        string spriteType = null;
        
        if (Blueprint != null)
        {
            GD.Print($"[EnemySpawn] Blueprint: {Blueprint.GetType().Name}");
            
            // Try to access SpriteType property using Variant Get method
            if (Blueprint.Get("SpriteType").AsString() is string bpSpriteType && !string.IsNullOrEmpty(bpSpriteType))
            {
                spriteType = bpSpriteType;
                GD.Print($"[EnemySpawn] ✓ Loading sprite from Blueprint.SpriteType: '{spriteType}'");
            }
            else
            {
                GD.Print($"[EnemySpawn] ✗ Blueprint.SpriteType is null or empty");
            }
        }
        else
        {
            GD.Print($"[EnemySpawn] Blueprint: null");
        }
        
        // Fallback to EnemyType if no valid Blueprint
        if (string.IsNullOrEmpty(spriteType) && !string.IsNullOrEmpty(EnemyType))
        {
            spriteType = EnemyType;
            GD.Print($"[EnemySpawn] Loading sprite from EnemyType: '{spriteType}'");
        }

        if (string.IsNullOrEmpty(spriteType))
        {
            // No explicit type, draw fallback rectangle (no texture)
            GD.Print($"[EnemySpawn] ✗ No sprite type specified, will draw red rectangle");
            Texture = null;
            return;
        }

        // Prefer new enemies/ path; fallback to legacy characters/ path for compatibility
        string typeLower = spriteType.ToLower();
        string newPath = $"res://assets/sprites/enemies/{typeLower}/sprite_sheet.png";
        string legacyFolder = $"enemy_{typeLower}";
        string legacyPath = $"res://assets/sprites/characters/{legacyFolder}/sprite_sheet.png";

        string pathToUse = null;
        if (FileAccess.FileExists(newPath))
        {
            pathToUse = newPath;
            GD.Print($"  Found sprite at: {newPath}");
        }
        else if (FileAccess.FileExists(legacyPath))
        {
            pathToUse = legacyPath;
            GD.Print($"  Found sprite at: {legacyPath}");
        }
        else
        {
            GD.PrintErr($"  ✗ Sprite not found! Tried: {newPath} and {legacyPath}");
        }

        if (pathToUse == null)
        {
            GD.PrintErr($"[EnemySpawn] ✗ No valid path found, texture will be null");
            Texture = null; // fallback to rectangle in _Draw
            return;
        }
        
        GD.Print($"[EnemySpawn] Loading texture from: {pathToUse}");
        var tex = GD.Load<Texture2D>(pathToUse);
        Texture = tex; // may still be null if load failed
        
        if (Texture != null)
        {
            GD.Print($"[EnemySpawn] ✓ Sprite loaded successfully!");
            // Derive frame size dynamically (assume 4 frames horizontally)
            var size = Texture.GetSize();
            int w = Mathf.RoundToInt(size.X);
            int h = Mathf.RoundToInt(size.Y);
            FrameWidth = w / 4;
            FrameHeight = h;
            GD.Print($"[EnemySpawn] Texture size: {w}x{h}, Frame size: {FrameWidth}x{FrameHeight}");
        }
        else
        {
            GD.PrintErr($"[EnemySpawn] ✗ GD.Load returned null for path: {pathToUse}");
        }
    }

    public void UpdateVisual(GridMap grid)
    {
        if (grid == null) return;
        // Recompute layer offset in case it changed after _Ready
        var ground = grid.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        // Treat GridPosition as tilemap-local coordinates
        int cell = grid.CellSize;
        Vector2 worldCenter = new Vector2(GridPosition.X * cell + cell / 2f,
                                          GridPosition.Y * cell + cell / 2f);
        Position = worldCenter + offset;
    }

    /// <summary>
    /// Create an Enemy instance from this spawn point. 
    /// Uses Blueprint if assigned, otherwise falls back to legacy EnemyType factory methods.
    /// </summary>
    public Enemy CreateEnemyInstance()
    {
        // Priority: Use Blueprint if available
        if (Blueprint != null)
        {
            // Access properties via Variant Get method since cast fails in editor
            var name = Blueprint.Get("EnemyName").AsString();
            var spriteType = Blueprint.Get("SpriteType").AsString();
            var level = Blueprint.Get("Level").AsInt32();
            var maxHealth = Blueprint.Get("MaxHealth").AsInt32();
            var attack = Blueprint.Get("Attack").AsInt32();
            var defense = Blueprint.Get("Defense").AsInt32();
            var speed = Blueprint.Get("Speed").AsInt32();
            var expReward = Blueprint.Get("ExperienceReward").AsInt32();
            var goldReward = Blueprint.Get("GoldReward").AsInt32();

            if (string.IsNullOrEmpty(spriteType))
                GD.PrintErr($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty SpriteType; loot table lookup will fail.");
            if (string.IsNullOrEmpty(name))
                GD.PushWarning($"[EnemySpawn] Blueprint at GridPosition {GridPosition} has empty EnemyName.");

            return new Enemy
            {
                Name = name,
                EnemyType = spriteType,
                Level = level,
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth,
                Attack = attack,
                Defense = defense,
                Speed = speed,
                ExperienceReward = expReward,
                GoldReward = goldReward
            };
        }

        // Fallback: Use legacy EnemyType string with factory methods
        if (!string.IsNullOrEmpty(EnemyType))
        {
            string type = EnemyType.ToLower();
            return type switch
            {
                "goblin" => Enemy.CreateGoblin(),
                "orc" => Enemy.CreateOrc(),
                "skeleton_warrior" => Enemy.CreateSkeletonWarrior(),
                "troll" => Enemy.CreateTroll(),
                "dragon" => Enemy.CreateDragon(),
                "forest_spirit" => Enemy.CreateForestSpirit(),
                "cave_spider" => Enemy.CreateCaveSpider(),
                "desert_scorpion" => Enemy.CreateDesertScorpion(),
                "swamp_wretch" => Enemy.CreateSwampWretch(),
                "mountain_wyvern" => Enemy.CreateMountainWyvern(),
                "dark_mage" => Enemy.CreateDarkMage(),
                "dungeon_guardian" => Enemy.CreateDungeonGuardian(),
                "demon_lord" => Enemy.CreateDemonLord(),
                "boss" => Enemy.CreateBoss(),
                _ => LogAndDefaultToGoblin(type, GridPosition)
            };
        }

        // Ultimate fallback: no Blueprint and no EnemyType set
        GD.PrintErr($"[EnemySpawn] No Blueprint and no EnemyType set at GridPosition {GridPosition}; defaulting to Goblin.");
        return Enemy.CreateGoblin();
    }

    private static Enemy LogAndDefaultToGoblin(string type, Vector2I gridPosition)
    {
        GD.PrintErr($"[EnemySpawn] Unknown EnemyType '{type}' at GridPosition {gridPosition}; defaulting to Goblin. Check the EnemyType property.");
        return Enemy.CreateGoblin();
    }

    public override void _Draw()
    {
        // If texture missing, draw a simple red square as fallback for enemy marker
        if (Texture == null)
        {
            var size = new Vector2(FrameWidth, FrameHeight);
            DrawRect(new Rect2(-size / 2, size), Colors.Red);
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            if (_gridMap == null) return;

            // Compute current grid position from node's position relative to the layer offset
            var ground = _gridMap.GetNodeOrNull<TileMapLayer>("GroundLayer");
            var offset = ground != null ? ground.Position : Vector2.Zero;
            int cellSize = _gridMap.CellSize;

            Vector2 local = Position - offset;
            int tx = Mathf.FloorToInt(local.X / cellSize);
            int ty = Mathf.FloorToInt(local.Y / cellSize);
            // Clamp to non-negative to avoid confusing negative tiles during level design
            tx = Mathf.Max(0, tx);
            ty = Mathf.Max(0, ty);

            var newGrid = new Vector2I(tx, ty);
            if (newGrid != GridPosition)
            {
                GridPosition = newGrid;
            }

            // Optionally snap to the nearest tile center when enabled
            if (EditorSnapEnabled)
            {
                Vector2 snapped = new Vector2(tx * cellSize + cellSize / 2f, ty * cellSize + cellSize / 2f) + offset;
                if (!snapped.IsEqualApprox(Position))
                {
                    Position = snapped;
                }
            }
            return;
        }

        // Runtime: advance animation on the sprite sheet if available
        if (Texture != null && RegionEnabled)
        {
            _animTimer += (float)delta;
            if (_animTimer >= FrameTime)
            {
                _animTimer = 0f;
                _currentFrame = (_currentFrame + 1) % 4;
                RegionRect = new Rect2(_currentFrame * FrameWidth, 0, FrameWidth, FrameHeight);
            }
        }
    }
}
