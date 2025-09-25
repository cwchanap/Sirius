using Godot;

[Tool]
public partial class EnemySpawn : Sprite2D
{
    [Export] public Vector2I GridPosition { get; set; } = new Vector2I(0, 0);
    // Editor-only: allow designers to toggle snap behavior. Default OFF so dragging works naturally.
    [Export] public bool EditorSnapEnabled { get; set; } = false;

    // Leave empty to auto-pick by area rules. Otherwise set to a known type name:
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

    public override void _Ready()
    {
        // Ensure this node is discoverable by GridMap
        if (!IsInGroup("EnemySpawn"))
        {
            AddToGroup("EnemySpawn");
        }

        // Find GridMap up the tree
        _gridMap = GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap");
        if (_gridMap == null)
        {
            // Try searching the scene tree as a fallback
            _gridMap = GetTree().Root.GetNodeOrNull<GridMap>("**/GridMap");
        }

        // Read TileMapLayer offset so sprite aligns with baked layers that are shifted at runtime
        var ground = _gridMap?.GetNodeOrNull<TileMapLayer>("GroundLayer");
        _mapOffset = ground != null ? ground.Position : Vector2.Zero;

        // Attempt to load an appropriate sprite sheet when EnemyType is provided
        TryLoadSpriteTexture();

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
        if (string.IsNullOrEmpty(EnemyType))
        {
            // No explicit type, draw fallback rectangle (no texture)
            Texture = null;
            return;
        }

        // Prefer new enemies/ path; fallback to legacy characters/ path for compatibility
        string typeLower = EnemyType.ToLower();
        string newPath = $"res://assets/sprites/enemies/{typeLower}/sprite_sheet.png";
        string legacyFolder = $"enemy_{typeLower}";
        string legacyPath = $"res://assets/sprites/characters/{legacyFolder}/sprite_sheet.png";

        string pathToUse = null;
        if (FileAccess.FileExists(newPath)) pathToUse = newPath;
        else if (FileAccess.FileExists(legacyPath)) pathToUse = legacyPath;

        if (pathToUse == null)
        {
            Texture = null; // fallback to rectangle in _Draw
            return;
        }
        var tex = GD.Load<Texture2D>(pathToUse);
        Texture = tex; // may still be null if load failed
        if (Texture != null)
        {
            // Derive frame size dynamically (assume 4 frames horizontally)
            var size = Texture.GetSize();
            int w = Mathf.RoundToInt(size.X);
            int h = Mathf.RoundToInt(size.Y);
            if (w >= 4 && h > 0)
            {
                FrameWidth = Mathf.Max(1, w / 4);
                FrameHeight = h;
            }
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
