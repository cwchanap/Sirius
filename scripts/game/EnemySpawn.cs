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

    private const int FrameWidth = 32;
    private const int FrameHeight = 32;

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

        // Position initially
        UpdateVisual(_gridMap);

        // Keep visible and responsive in the editor for easy placement
        SetProcess(Engine.IsEditorHint());
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

        // Expect asset path like: res://assets/sprites/characters/enemy_goblin/sprite_sheet.png
        string folderName = $"enemy_{EnemyType.ToLower()}";
        string path = $"res://assets/sprites/characters/{folderName}/sprite_sheet.png";
        if (!FileAccess.FileExists(path))
        {
            Texture = null; // fallback to rectangle in _Draw
            return;
        }
        var tex = GD.Load<Texture2D>(path);
        Texture = tex; // may still be null if load failed
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
        if (!Engine.IsEditorHint()) return;
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
    }
}
