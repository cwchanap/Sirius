using Godot;

public partial class PlayerDisplay : Sprite2D
{
    private GridMap _gridMap;
    private Vector2 _mapOffset = Vector2.Zero;

    // Animation
    private int _currentFrame = 0;
    private float _animTimer = 0f;
    private const float FrameTime = 0.2f; // 5 FPS
    private const int FrameWidth = 32;
    private const int FrameHeight = 32;

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
        // Apply the same layer offset the TileMapLayer uses so we overlay correctly
        Position = worldCenter + _mapOffset;
        GD.Print($"PlayerDisplay.UpdatePosition grid={gridPosition} worldCenter={worldCenter} final={Position}");
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
