using Godot;

[Tool]
public abstract partial class PuzzleSpawnBase : Sprite2D
{
    private GridMap? _gridMap;
    private Vector2I? _lastOutOfBoundsEditorGrid;

    [Export] public string PuzzleId { get; set; } = "";
    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public bool EditorSnapEnabled { get; set; }

    protected abstract string GroupName { get; }
    protected virtual Color FallbackColor => Colors.MediumPurple;

    public override void _Ready()
    {
        if (!IsInGroup(GroupName))
        {
            AddToGroup(GroupName);
        }

        _gridMap = FindGridMap();
        Centered = true;
        ZIndex = 2;

        if (_gridMap != null)
        {
            UpdateVisual(_gridMap);
        }

        SetProcess(Engine.IsEditorHint());
    }

    public bool BelongsToFloor(Node? floorRoot)
    {
        return floorRoot != null && (ReferenceEquals(GetParent(), floorRoot) || floorRoot.IsAncestorOf(this));
    }

    public void UpdateVisual(GridMap grid)
    {
        if (grid == null)
        {
            return;
        }

        var ground = grid.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cell = grid.CellSize;
        Position = new Vector2(GridPosition.X * cell + cell / 2f, GridPosition.Y * cell + cell / 2f) + offset;
    }

    public override void _Draw()
    {
        if (Texture != null)
        {
            return;
        }

        var size = new Vector2(24, 24);
        DrawRect(new Rect2(-size / 2f, size), FallbackColor);
        DrawRect(new Rect2(-size / 2f, size), Colors.White, false, 2.0f);
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() || _gridMap == null)
        {
            return;
        }

        var ground = _gridMap.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cellSize = _gridMap.CellSize;
        int maxX = Mathf.Max(0, _gridMap.GridWidth - 1);
        int maxY = Mathf.Max(0, _gridMap.GridHeight - 1);

        Vector2 local = Position - offset;
        int rawTx = Mathf.FloorToInt(local.X / cellSize);
        int rawTy = Mathf.FloorToInt(local.Y / cellSize);
        var rawGrid = new Vector2I(rawTx, rawTy);

        if (rawTx < 0 || rawTx > maxX || rawTy < 0 || rawTy > maxY)
        {
            if (_lastOutOfBoundsEditorGrid != rawGrid)
            {
                GD.PrintErr($"{GetType().Name} '{PuzzleId}' editor position {rawGrid} is outside grid bounds 0..{maxX},0..{maxY}; clamping to fit.");
                _lastOutOfBoundsEditorGrid = rawGrid;
            }
        }
        else
        {
            _lastOutOfBoundsEditorGrid = null;
        }

        int tx = Mathf.Clamp(rawTx, 0, maxX);
        int ty = Mathf.Clamp(rawTy, 0, maxY);
        var newGrid = new Vector2I(tx, ty);
        if (newGrid != GridPosition)
        {
            GridPosition = newGrid;
        }

        if (EditorSnapEnabled)
        {
            Vector2 snapped = new Vector2(tx * cellSize + cellSize / 2f, ty * cellSize + cellSize / 2f) + offset;
            if (!snapped.IsEqualApprox(Position))
            {
                Position = snapped;
            }
        }
    }

    private GridMap? FindGridMap()
    {
        return GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap")
            ?? GetTree()?.Root.FindChild("GridMap", recursive: true, owned: false) as GridMap;
    }
}
