using Godot;

/// <summary>
/// Scene node representing an NPC placed in a floor scene.
/// Mirrors the EnemySpawn pattern: added to group "NpcSpawn", registered by GridMap
/// via RegisterStaticNpcSpawns(), referenced by NpcCatalog via NpcId.
///
/// Place this node in a floor scene (.tscn) in the Godot editor.
/// Set NpcId to a registered NpcCatalog ID (e.g. "village_shopkeeper").
/// Set GridPosition to the tilemap-local cell coordinates.
/// </summary>
[Tool]
public partial class NpcSpawn : Sprite2D
{
    [Export] public Vector2I GridPosition { get; set; } = new Vector2I(0, 0);
    [Export] public string NpcId { get; set; } = string.Empty;
    [Export] public bool EditorSnapEnabled { get; set; } = false;

    private GridMap? _gridMap;
    private int _frameWidth = 32;
    private int _frameHeight = 32;
    private Vector2I? _lastOutOfBoundsEditorGrid;

    public override void _Ready()
    {
        if (!IsInGroup("NpcSpawn"))
            AddToGroup("NpcSpawn");

        GD.Print($"NpcSpawn._Ready() at {GridPosition}, NpcId: '{NpcId}'");

        _gridMap = GetParent() as GridMap ?? GetNodeOrNull<GridMap>("../GridMap");
        if (_gridMap == null)
            _gridMap = GetTree().Root.FindChild("GridMap", recursive: true, owned: false) as GridMap;

        if (_gridMap == null)
            GD.PrintErr($"NpcSpawn '{NpcId}' at {GridPosition} could not find GridMap via parent or scene-tree search.");

        TryLoadSpriteTexture();

        Centered = true;
        RegionEnabled = Texture != null;
        if (RegionEnabled)
            RegionRect = new Rect2(0, 0, _frameWidth, _frameHeight);

        int cell = _gridMap != null ? _gridMap.CellSize : 32;
        if (_frameWidth > 0 && _frameHeight > 0)
            Scale = new Vector2(cell / (float)_frameWidth, cell / (float)_frameHeight);

        if (_gridMap != null)
            UpdateVisual(_gridMap);

        SetProcess(Engine.IsEditorHint());
        ZIndex = 2;
    }

    private void TryLoadSpriteTexture()
    {
        var npcData = NpcCatalog.GetById(NpcId);
        if (npcData == null || string.IsNullOrEmpty(npcData.SpriteType))
        {
            Texture = null;
            return;
        }

        string typeLower = npcData.SpriteType.ToLower();
        string path = $"res://assets/sprites/npcs/{typeLower}/sprite_sheet.png";
        string legacyPath = $"res://assets/sprites/characters/npc_{typeLower}/sprite_sheet.png";

        string pathToUse = FileAccess.FileExists(path) ? path
            : FileAccess.FileExists(legacyPath) ? legacyPath
            : null;

        if (pathToUse == null)
        {
            Texture = null;
            return;
        }

        var tex = GD.Load<Texture2D>(pathToUse);
        Texture = tex;

        if (Texture != null)
        {
            var size = Texture.GetSize();
            _frameWidth = Mathf.RoundToInt(size.X) / 4;
            _frameHeight = Mathf.RoundToInt(size.Y);
        }
    }

    /// <summary>Aligns this node's world position to its GridPosition on the given GridMap.</summary>
    public void UpdateVisual(GridMap grid)
    {
        if (grid == null) return;
        var ground = grid.GetNodeOrNull<TileMapLayer>("GroundLayer");
        var offset = ground != null ? ground.Position : Vector2.Zero;
        int cell = grid.CellSize;
        Position = new Vector2(GridPosition.X * cell + cell / 2f,
                               GridPosition.Y * cell + cell / 2f) + offset;
    }

    /// <summary>Returns the NpcData for this spawn, or null if the NpcId is not registered.</summary>
    public NpcData? GetNpcData()
    {
        var data = NpcCatalog.GetById(NpcId);
        if (data == null)
            GD.PushWarning($"NpcSpawn at {GridPosition}: NpcId '{NpcId}' not found in NpcCatalog.");
        return data;
    }

    public bool BelongsToFloor(Node? floorRoot)
    {
        return floorRoot != null &&
               (ReferenceEquals(GetParent(), floorRoot) || floorRoot.IsAncestorOf(this));
    }

    public override void _Draw()
    {
        if (Texture == null)
        {
            var size = new Vector2(_frameWidth, _frameHeight);
            DrawRect(new Rect2(-size / 2, size), Colors.Teal);
        }
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() || _gridMap == null) return;

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
                GD.PrintErr($"NpcSpawn '{NpcId}' editor position {rawGrid} is outside grid bounds 0..{maxX},0..{maxY}; clamping to fit.");
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
            GridPosition = newGrid;

        if (EditorSnapEnabled)
        {
            Vector2 snapped = new Vector2(tx * cellSize + cellSize / 2f,
                                          ty * cellSize + cellSize / 2f) + offset;
            if (!snapped.IsEqualApprox(Position))
                Position = snapped;
        }
    }
}
