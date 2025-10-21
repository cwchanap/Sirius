using Godot;

public partial class PlayerDisplay : Sprite2D
{
    private GridMap _gridMap;
    private Vector2 _mapOffset = Vector2.Zero;

    // Animation
    private int _currentFrame = 0;
    private float _animTimer = 0f;
    private const float FrameTime = 0.2f; // 5 FPS
    // Sprite sheet frames are now 96x96 for higher resolution; we'll scale to keep same on-screen size
    private const int FrameWidth = 96;
    private const int FrameHeight = 96;

    public void Initialize(GridMap gridMap)
    {
        _gridMap = gridMap;

        // Read TileMapLayer offset so sprite aligns with baked layers that are shifted at runtime
        var ground = _gridMap.GetNodeOrNull<TileMapLayer>("GroundLayer");
        _mapOffset = ground != null ? ground.Position : Vector2.Zero;
        GD.Print($"PlayerDisplay init. Layer offset: {_mapOffset}");

        // Load sprite sheet
        var tex = GD.Load<Texture2D>("res://assets/sprites/characters/player_hero/sprite_sheet.png");
        if (tex != null)
        {
            Texture = tex;
            Centered = true; // position will be at tile center
            RegionEnabled = true;
            RegionRect = new Rect2(0, 0, FrameWidth, FrameHeight);
            // Keep on-screen size identical to previous 32x32 frames by scaling 96x96 down by 1/3
            Scale = new Vector2(1f / 3f, 1f / 3f);
        }
        else
        {
            GD.PrintErr("PlayerDisplay: Failed to load player sprite sheet. A blue rectangle will be shown instead.");
        }

        // Place at initial player position
        UpdatePosition(_gridMap.GetPlayerPosition());

        SetProcess(true);
        ZIndex = 2; // Above walls (which are z_index = 1)
    }

    public void UpdatePosition(Vector2I gridPosition)
    {
        if (_gridMap == null) return;
        Vector2 worldCenter = _gridMap.GetWorldPosition(gridPosition);
        // TileMapLayers are now positioned at the world offset, so we use worldCenter directly
        Position = worldCenter;
        GD.Print($"PlayerDisplay.UpdatePosition grid={gridPosition} worldCenter={worldCenter} localPos={Position} globalPos={GlobalPosition}");
        GD.Print($"  Parent: {GetParent().Name} at globalPos={GetParent<Node2D>().GlobalPosition}");
    }

    public override void _Process(double delta)
    {
        if (Texture == null || !RegionEnabled)
            return;

        _animTimer += (float)delta;
        if (_animTimer >= FrameTime)
        {
            _animTimer = 0f;
            _currentFrame = (_currentFrame + 1) % 4;
            RegionRect = new Rect2(_currentFrame * FrameWidth, 0, FrameWidth, FrameHeight);
        }
    }

    public override void _Draw()
    {
        // If texture missing, draw a simple blue square as fallback
        if (Texture == null)
        {
            var size = new Vector2(FrameWidth, FrameHeight);
            DrawRect(new Rect2(-size / 2, size), Colors.Blue);
        }
    }
}
