using Godot;
using System.Threading.Tasks;

[Tool]
public partial class TreasureBoxSpawn : Sprite2D
{
    private const string SpritePath = "res://assets/sprites/objects/treasure_box/sprite_sheet.png";
    private const int FrameCount = 4;
    private int _frameWidth = 32;
    private int _frameHeight = 32;
    private GridMap? _gridMap;
    private Vector2I? _lastOutOfBoundsEditorGrid;

    [Export] public Vector2I GridPosition { get; set; } = Vector2I.Zero;
    [Export] public string TreasureBoxId { get; set; } = "";
    [Export] public int RewardGold { get; set; }
    [Export] public Godot.Collections.Array<string>? RewardItemIds { get; set; } = new();
    [Export] public Godot.Collections.Array<int>? RewardItemQuantities { get; set; } = new();
    [Export] public bool EditorSnapEnabled { get; set; } = false;

    public bool IsOpened { get; private set; }
    public bool IsOpening { get; private set; }
    public int CurrentFrameIndex { get; private set; }

    public override void _Ready()
    {
        if (!IsInGroup("TreasureBoxSpawn"))
        {
            AddToGroup("TreasureBoxSpawn");
        }

        _gridMap = GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap");
        if (_gridMap == null)
        {
            _gridMap = GetTree().Root.FindChild("GridMap", recursive: true, owned: false) as GridMap;
        }

        TryLoadSpriteTexture();
        Centered = true;
        RegionEnabled = Texture != null;
        SetFrameIndex(IsOpened ? FrameCount - 1 : 0);

        int cell = _gridMap != null ? _gridMap.CellSize : 32;
        if (_frameWidth > 0 && _frameHeight > 0)
        {
            Scale = new Vector2(cell / (float)_frameWidth, cell / (float)_frameHeight);
        }

        if (_gridMap != null)
        {
            UpdateVisual(_gridMap);
        }

        ZIndex = 2;
        SetProcess(Engine.IsEditorHint());
    }

    public TreasureReward BuildReward()
    {
        var reward = new TreasureReward { Gold = RewardGold };
        var itemIds = RewardItemIds;
        var itemQuantities = RewardItemQuantities;
        if (itemIds == null)
        {
            return reward;
        }

        for (int i = 0; i < itemIds.Count; i++)
        {
            string itemId = itemIds[i];
            int quantity = itemQuantities != null && i < itemQuantities.Count ? itemQuantities[i] : 1;
            reward.Items.Add(new TreasureRewardItem(itemId, quantity));
        }

        return reward;
    }

    public TreasureRewardGrantResult GrantRewardTo(Character player)
    {
        return BuildReward().GrantTo(player);
    }

    public void ApplyOpenedState(bool opened)
    {
        IsOpened = opened;
        IsOpening = false;
        SetFrameIndex(opened ? FrameCount - 1 : 0);
    }

    public async Task OpenAsync()
    {
        if (IsOpened || IsOpening)
        {
            return;
        }

        IsOpening = true;
        for (int frame = 1; frame < FrameCount; frame++)
        {
            SetFrameIndex(frame);
            if (IsInsideTree())
            {
                await ToSignal(GetTree().CreateTimer(0.12), Timer.SignalName.Timeout);
                if (AbortOpeningIfUnavailable())
                {
                    return;
                }
            }
        }

        if (!GodotObject.IsInstanceValid(this))
        {
            return;
        }

        IsOpened = true;
        IsOpening = false;
        SetFrameIndex(FrameCount - 1);
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

    public bool BelongsToFloor(Node? floorRoot)
    {
        return floorRoot != null && (ReferenceEquals(GetParent(), floorRoot) || floorRoot.IsAncestorOf(this));
    }

    public override void _Draw()
    {
        if (Texture != null)
        {
            return;
        }

        var size = new Vector2(_frameWidth, _frameHeight);
        var body = IsOpened ? Colors.Goldenrod.Darkened(0.25f) : Colors.SaddleBrown;
        DrawRect(new Rect2(-size / 2f, size), body);
        DrawRect(new Rect2(-size / 2f, size), Colors.Gold, false, 2.0f);
        if (!IsOpened)
        {
            DrawLine(new Vector2(-size.X / 2f, -2), new Vector2(size.X / 2f, -2), Colors.Gold, 2.0f);
        }
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
                GD.PrintErr($"TreasureBoxSpawn '{TreasureBoxId}' editor position {rawGrid} is outside grid bounds 0..{maxX},0..{maxY}; clamping to fit.");
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

    private void TryLoadSpriteTexture()
    {
        if (!FileAccess.FileExists(SpritePath))
        {
            Texture = null;
            return;
        }

        Texture = GD.Load<Texture2D>(SpritePath);
        if (Texture == null)
        {
            return;
        }

        var size = Texture.GetSize();
        _frameWidth = Mathf.Max(1, Mathf.RoundToInt(size.X) / FrameCount);
        _frameHeight = Mathf.Max(1, Mathf.RoundToInt(size.Y));
    }

    private void SetFrameIndex(int frameIndex)
    {
        CurrentFrameIndex = Mathf.Clamp(frameIndex, 0, FrameCount - 1);
        if (Texture != null)
        {
            RegionEnabled = true;
            RegionRect = new Rect2(CurrentFrameIndex * _frameWidth, 0, _frameWidth, _frameHeight);
        }

        QueueRedraw();
    }

    private bool AbortOpeningIfUnavailable()
    {
        if (!GodotObject.IsInstanceValid(this))
        {
            return true;
        }

        if (IsInsideTree())
        {
            return false;
        }

        IsOpening = false;
        return true;
    }
}
