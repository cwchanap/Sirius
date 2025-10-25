using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class GridMap : Node2D
{
    [Export] public int GridWidth { get; set; } = 160;
    [Export] public int GridHeight { get; set; } = 160;
    [Export] public int CellSize { get; set; } = 32; // Reduced cell size to fit larger grid
    [Export] public bool UseSprites { get; set; } = true; // Toggle for sprite rendering
    [Export] public bool UseBakedTileMapsAtRuntime { get; set; } = true; // Default to static TileMapLayer tiles at runtime
    [Export] public bool EnableDebugLogging { get; set; } = false; // Reduce noisy logs unless enabled
    
    // Editor preview controls
    [Export] public bool EditorPreviewFullMap { get; set; } = false;
    [Export] public Vector2I EditorPreviewSize { get; set; } = new Vector2I(60, 40);
    [Export] public bool EditorPreviewUseSprites { get; set; } = true;
    [Export] public bool EditorPreviewAnimate { get; set; } = true;
    [Export] public bool EditorBakeTileMap { get; set; } = false;
    [Export] public bool EditorClearBakedTileMaps { get; set; } = false;
    [Export] public bool EditorGenerateSpriteSheets { get; set; } = false; // Compose frame1-4.png into sprite_sheet.png per character
    [Export] public bool EditorSaveBakedTileSet { get; set; } = false; // Save the generated TileSet as a .tres resource
    
    private const float TERRAIN_BASE_PIXEL_SIZE = 96f;
    
    private int[,] _grid;
    private Vector2I _playerPosition;
    
    // Sprite dictionaries for different asset types
    private Dictionary<CellType, Texture2D> _cellSprites = new();
    private Dictionary<string, Texture2D> _enemySprites = new();
    private Dictionary<string, Texture2D> _terrainSprites = new();
    
    [Signal]
    public delegate void GridMapReadyEventHandler();
    
    // Cached TileMap layers (present when UseBakedTileMapsAtRuntime is true)
    private TileMapLayer _groundLayer;
    private TileMapLayer _wallLayer;
    
    // Pending floor load data (set before GridMap enters scene tree)
    private Node2D _pendingFloorInstance;
    private FloorDefinition _pendingFloorDef;
    private Vector2I _pendingPlayerSpawn;
    
    // Animation properties
    private float _animationTime = 0.0f;
    private const float ANIMATION_SPEED = 0.2f; // Time per frame in seconds (faster animation)
    private const int TOTAL_FRAMES = 4;
    private int _currentFrame = 0;
    
    // Debugging flag to use colors instead of textures
    private bool _useColorTerrain = false;

    // When using baked TileMaps, support content painted at negative coordinates by
    // computing a tilemap origin and mapping used cells into the 0..Grid bounds.
    // Also store a world-space pixel offset so world positions align to visuals.
    private Vector2I _tilemapOrigin = new Vector2I(0, 0);
    private Vector2 _tilemapWorldOffset = Vector2.Zero;
    
    // Fast lookup of wall tiles in tilemap coordinates for collision beyond grid bounds
    private HashSet<Vector2I> _wallTileCoords = new();
    
    // Fast lookup of stair tiles in tilemap coordinates for floor transitions
    private HashSet<Vector2I> _stairTileCoords = new();
    
    // Grid cell types
    public enum CellType
    {
        Empty = 0,
        Wall = 1,
        Enemy = 2,
        Player = 3
    }

    // ===== Editor-only baking helpers =====
    private TileSet _bakedTileSet;
    private int _sourceIdFloor = -1;
    private int _sourceIdWall = -1;
    private int _sourceIdEnemyMarker = -1;
    private int _sourceIdPlayerMarker = -1;
    // Map terrain type -> TileSet source id for themed floors
    private Dictionary<string, int> _floorSourceIds = new();

    private void EditorHandleBakingActions()
    {
        // Called from _Process in editor
        if (EditorClearBakedTileMaps)
        {
            EditorClearBakedTileMaps = false;
            EditorClearBaked();
            QueueRedraw();
        }

        if (EditorBakeTileMap)
        {
            EditorBakeTileMap = false;
            EditorBake();
            QueueRedraw();
        }

        if (EditorGenerateSpriteSheets)
        {
            EditorGenerateSpriteSheets = false;
            EditorGenerateSheets();
        }

        if (EditorSaveBakedTileSet)
        {
            EditorSaveBakedTileSet = false;
            EditorSaveTileSetResource();
        }

        // One-time prefill removed; static workflow uses manual painting or existing baked data
    }

    private void EditorSaveTileSetResource()
    {
        if (_bakedTileSet == null)
        {
            EnsureBakedTileSet();
        }
        // Save to a stable location so the editor shows a persistent resource
        const string path = "res://assets/tiles/baked_tileset.tres";
        // Ensure directory exists
        var dirPath = "res://assets/tiles";
        if (!DirAccess.DirExistsAbsolute(dirPath))
        {
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
        }
        var err = ResourceSaver.Save(_bakedTileSet, path);
        if (err == Error.Ok)
        {
            if (EnableDebugLogging) GD.Print($"💾 Saved TileSet to {path}");
            // Re-load resource so it becomes an external resource instance
            var saved = GD.Load<TileSet>(path);
            if (saved != null)
            {
                // Keep existing per-layer TileSets unchanged; just update our cached reference.
                _bakedTileSet = saved;
            }
        }
        else
        {
            GD.PrintErr($"❌ Failed to save TileSet to {path}: {err}");
        }
    }

    private void EditorEnsureTilesetAndLayers()
    {
        // Ensure layers exist and have a saved TileSet resource in assets/tiles
        const string path = "res://assets/tiles/baked_tileset.tres";
        EnsureBakedTileSet();

        if (!FileAccess.FileExists(path))
        {
            // Save if not present
            var err = ResourceSaver.Save(_bakedTileSet, path);
            if (err == Error.Ok && EnableDebugLogging)
            {
                GD.Print($"💾 Auto-saved TileSet to {path}");
            }
        }

        var saved = GD.Load<TileSet>(path);
        if (saved != null)
        {
            _bakedTileSet = saved;
        }

        // Ensure layers exist and have this tileset
        var ground = GetNodeOrNull<TileMapLayer>("GroundLayer") ?? CreateTileMapLayerChild("GroundLayer", 0);
        var walls = GetNodeOrNull<TileMapLayer>("WallLayer") ?? CreateTileMapLayerChild("WallLayer", 1);
        var markers = GetNodeOrNull<TileMapLayer>("MarkerLayer") ?? CreateTileMapLayerChild("MarkerLayer", 2);

        var sceneOwner = Owner ?? GetTree().EditedSceneRoot;
        if (ground.Owner == null) ground.Owner = sceneOwner;
        if (walls.Owner == null) walls.Owner = sceneOwner;
        if (markers.Owner == null) markers.Owner = sceneOwner;

        if (ground.TileSet == null) ground.TileSet = _bakedTileSet;
        if (walls.TileSet == null) walls.TileSet = _bakedTileSet;
        if (markers.TileSet == null) markers.TileSet = _bakedTileSet;
    }

    private void EditorGenerateSheets()
    {
        // Scan res://assets/sprites/characters/* for frame1-4.png and build sprite_sheet.png if missing
        string basePath = "res://assets/sprites/characters";
        var dir = DirAccess.Open(basePath);
        if (dir == null)
        {
            if (EnableDebugLogging) GD.PrintErr($"❌ Could not open characters directory: {basePath}");
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            string entry = dir.GetNext();
            if (string.IsNullOrEmpty(entry)) break;
            if (entry == "." || entry == "..") continue;
            if (!dir.CurrentIsDir()) continue;

            string charDir = $"{basePath}/{entry}";
            string sheetPath = $"{charDir}/sprite_sheet.png";
            if (FileAccess.FileExists(sheetPath))
            {
                if (EnableDebugLogging) GD.Print($"ℹ️ Skipping {entry}: sprite_sheet.png already exists");
                continue;
            }

            string[] frames = { "frame1.png", "frame2.png", "frame3.png", "frame4.png" };
            var images = new List<Image>();
            bool missing = false;
            foreach (var f in frames)
            {
                string fp = $"{charDir}/{f}";
                if (!FileAccess.FileExists(fp)) { missing = true; break; }
                var img = Image.LoadFromFile(fp);
                if (img == null)
                {
                    missing = true; break;
                }
                images.Add(img);
            }

            if (missing)
            {
                if (EnableDebugLogging) GD.Print($"⚠️ Skipping {entry}: missing frame images");
                continue;
            }

            // Verify sizes and compose 128x32
            int fw = 32, fh = 32;
            foreach (var img in images)
            {
                if (img.GetWidth() != fw || img.GetHeight() != fh)
                {
                    if (EnableDebugLogging) GD.Print($"⚠️ Skipping {entry}: frame size not 32x32");
                    missing = true; break;
                }
            }
            if (missing) continue;

            var outImg = Image.CreateEmpty(fw * 4, fh, false, Image.Format.Rgba8);
            for (int i = 0; i < 4; i++)
            {
                outImg.BlitRect(images[i], new Rect2I(0, 0, fw, fh), new Vector2I(i * fw, 0));
            }
            Error saveErr = outImg.SavePng(sheetPath);
            if (saveErr == Error.Ok)
            {
                if (EnableDebugLogging) GD.Print($"✅ Generated sprite sheet: {sheetPath}");
            }
            else
            {
                GD.PrintErr($"❌ Failed to save sprite sheet for {entry}: {saveErr}");
            }
        }
        dir.ListDirEnd();
    }

    private bool HasBakedTileMaps()
    {
        // Only support TileMapLayer nodes
        var ground = GetNodeOrNull<TileMapLayer>("GroundLayer");
        var walls = GetNodeOrNull<TileMapLayer>("WallLayer");
        var markers = GetNodeOrNull<TileMapLayer>("MarkerLayer");

        bool anyLayer = ground != null || walls != null || markers != null;
        if (!anyLayer) return false;

        bool hasCells = false;
        if (ground != null) hasCells |= ground.GetUsedCells().Count > 0;
        if (walls != null) hasCells |= walls.GetUsedCells().Count > 0;
        if (markers != null) hasCells |= markers.GetUsedCells().Count > 0;
        return hasCells;
    }

    private void EditorBake()
    {
        if (!Engine.IsEditorHint()) return;
        if (_grid == null || _grid.Length == 0)
        {
            InitializeGrid();
        }

        // Create/get TileMapLayer nodes
        var ground = GetNodeOrNull<TileMapLayer>("GroundLayer") ?? CreateTileMapLayerChild("GroundLayer", zIndex: 0);
        var walls = GetNodeOrNull<TileMapLayer>("WallLayer") ?? CreateTileMapLayerChild("WallLayer", zIndex: 1);
        var markers = GetNodeOrNull<TileMapLayer>("MarkerLayer") ?? CreateTileMapLayerChild("MarkerLayer", zIndex: 2);

        // Ensure layers are owned by the edited scene so they are selectable/editable and persist on save
        var sceneOwner = Owner ?? GetTree().EditedSceneRoot;
        if (ground.Owner == null) ground.Owner = sceneOwner;
        if (walls.Owner == null) walls.Owner = sceneOwner;
        if (markers.Owner == null) markers.Owner = sceneOwner;

        // Create a shared TileSet with themed floors
        EnsureBakedTileSet();
        // Respect existing per-layer TileSets; only assign if none is set
        if (ground.TileSet == null) ground.TileSet = _bakedTileSet;
        if (walls.TileSet == null) walls.TileSet = _bakedTileSet;
        if (markers.TileSet == null) markers.TileSet = _bakedTileSet;

        // Clear previous cells
        ground.Clear();
        walls.Clear();
        markers.Clear();

        // Fill ground (floor everywhere), walls, and markers (enemies/player)
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                // Ground base with themed floor per area
                string area = GetTerrainType(x, y);
                int floorSource = _sourceIdFloor;
                if (_floorSourceIds != null && _floorSourceIds.Count > 0)
                {
                    if (_floorSourceIds.TryGetValue(area, out int sid))
                    {
                        floorSource = sid;
                    }
                    else if (_floorSourceIds.TryGetValue("starting_area", out int startSid))
                    {
                        floorSource = startSid;
                    }
                }
                ground.SetCell(new Vector2I(x, y), floorSource, Vector2I.Zero, 0);

                var cellType = (CellType)_grid[x, y];
                if (cellType == CellType.Wall)
                {
                    walls.SetCell(new Vector2I(x, y), _sourceIdWall, Vector2I.Zero, 0);
                }
                else if (cellType == CellType.Enemy)
                {
                    markers.SetCell(new Vector2I(x, y), _sourceIdEnemyMarker, Vector2I.Zero, 0);
                }
                else if (cellType == CellType.Player)
                {
                    markers.SetCell(new Vector2I(x, y), _sourceIdPlayerMarker, Vector2I.Zero, 0);
                }
            }
        }
    }

    private void EditorClearBaked()
    {
        if (!Engine.IsEditorHint()) return;
        var groundL = GetNodeOrNull<TileMapLayer>("GroundLayer");
        var wallsL = GetNodeOrNull<TileMapLayer>("WallLayer");
        var markersL = GetNodeOrNull<TileMapLayer>("MarkerLayer");

        if (groundL != null) groundL.QueueFree();
        if (wallsL != null) wallsL.QueueFree();
        if (markersL != null) markersL.QueueFree();
    }

    private TileMapLayer CreateTileMapLayerChild(string name, int zIndex)
    {
        var layer = new TileMapLayer
        {
            Name = name,
            ZIndex = zIndex,
        };
        AddChild(layer);
        // Set owner so the node is part of the scene and editable in the editor
        layer.Owner = Owner ?? GetTree().EditedSceneRoot;
        return layer;
    }

    private void EnsureBakedTileSet()
    {
        if (_bakedTileSet != null && _sourceIdFloor != -1) return;

        _bakedTileSet = new TileSet();
        _floorSourceIds.Clear();

        // Use actual terrain textures for floor and wall so they appear in the editor's palette
        var wallTex = GD.Load<Texture2D>("res://assets/sprites/terrain/wall_generic.png");
        if (wallTex != null)
        {
            _sourceIdWall = AddAtlasSourceWithSingleTile(_bakedTileSet, wallTex);
        }

        // Add themed floors including starting_area
        var floorMap = new Dictionary<string, string>
        {
            {"starting_area", "res://assets/sprites/terrain/floor_starting_area.png"},
            {"forest",        "res://assets/sprites/terrain/floor_forest.png"},
            {"cave",          "res://assets/sprites/terrain/floor_cave.png"},
            {"desert",        "res://assets/sprites/terrain/floor_desert.png"},
            {"swamp",         "res://assets/sprites/terrain/floor_swamp.png"},
            {"mountain",      "res://assets/sprites/terrain/floor_mountain.png"},
            {"dungeon",       "res://assets/sprites/terrain/floor_dungeon.png"}
        };
        foreach (var kv in floorMap)
        {
            var tex = GD.Load<Texture2D>(kv.Value);
            if (tex != null)
            {
                int sid = AddAtlasSourceWithSingleTile(_bakedTileSet, tex);
                _floorSourceIds[kv.Key] = sid;
            }
        }
        // Default floor id
        if (_floorSourceIds.TryGetValue("starting_area", out int defSid))
        {
            _sourceIdFloor = defSid;
        }
        else
        {
            // Fallback to colored floor if texture not found
            var texFloor = CreateSolidTexture(new Color(0.85f, 0.85f, 0.85f, 1f));
            _sourceIdFloor = AddAtlasSourceWithSingleTile(_bakedTileSet, texFloor);
            _floorSourceIds["starting_area"] = _sourceIdFloor;
        }

        // Colored marker tiles for enemy/player markers
        var texEnemy = CreateSolidTexture(new Color(1f, 0.2f, 0.2f, 1f));       // red
        var texPlayer = CreateSolidTexture(new Color(0.2f, 0.4f, 1f, 1f));      // blue
        _sourceIdEnemyMarker = AddAtlasSourceWithSingleTile(_bakedTileSet, texEnemy);
        _sourceIdPlayerMarker = AddAtlasSourceWithSingleTile(_bakedTileSet, texPlayer);
    }

    private int AddAtlasSourceWithSingleTile(TileSet tileSet, Texture2D texture)
    {
        var src = new TileSetAtlasSource();
        src.Texture = texture;
        src.TextureRegionSize = new Vector2I((int)TERRAIN_BASE_PIXEL_SIZE, (int)TERRAIN_BASE_PIXEL_SIZE);
        src.CreateTile(Vector2I.Zero); // single tile at (0,0)
        return tileSet.AddSource(src);
    }

    private ImageTexture CreateSolidTexture(Color color)
    {
        var img = Image.CreateEmpty((int)TERRAIN_BASE_PIXEL_SIZE, (int)TERRAIN_BASE_PIXEL_SIZE, false, Image.Format.Rgba8);
        img.Fill(color);
        return ImageTexture.CreateFromImage(img);
    }

    private void EditorSetBakedVisible(bool visible)
    {
        var groundL = GetNodeOrNull<Node2D>("GroundLayer");
        var wallsL = GetNodeOrNull<Node2D>("WallLayer");
        var markersL = GetNodeOrNull<Node2D>("MarkerLayer");
        if (groundL != null) groundL.Visible = visible;
        if (wallsL != null) wallsL.Visible = visible;
        if (markersL != null) markersL.Visible = visible;
    }

    // Build the logical grid from baked TileMap layers so gameplay works without procedural generation
    private void BuildGridFromBakedTileMaps()
    {
        // Use cached layer references if available (from LoadFloor), otherwise look them up
        var ground = _groundLayer ?? GetNodeOrNull<TileMapLayer>("GroundLayer");
        var walls = _wallLayer ?? GetNodeOrNull<TileMapLayer>("WallLayer");
        var stairs = GetNodeOrNull<TileMapLayer>("StairLayer");
        _wallTileCoords.Clear();
        _stairTileCoords.Clear();

        // Compute bounding boxes separately and prefer Ground for origin/centering
        int gMinX = int.MaxValue, gMinY = int.MaxValue, gMaxX = int.MinValue, gMaxY = int.MinValue;
        int wMinX = int.MaxValue, wMinY = int.MaxValue, wMaxX = int.MinValue, wMaxY = int.MinValue;
        bool haveGround = false, haveWalls = false;
        Godot.Collections.Array<Vector2I> groundUsedLocal = ground != null ? ground.GetUsedCells() : null;
        Godot.Collections.Array<Vector2I> wallUsedLocal = walls != null ? walls.GetUsedCells() : null;
        if (groundUsedLocal != null && groundUsedLocal.Count > 0)
        {
            foreach (var c in groundUsedLocal)
            {
                if (c.X < gMinX) gMinX = c.X; if (c.X > gMaxX) gMaxX = c.X;
                if (c.Y < gMinY) gMinY = c.Y; if (c.Y > gMaxY) gMaxY = c.Y;
            }
            haveGround = true;
        }
        if (wallUsedLocal != null && wallUsedLocal.Count > 0)
        {
            foreach (var c in wallUsedLocal)
            {
                if (c.X < wMinX) wMinX = c.X; if (c.X > wMaxX) wMaxX = c.X;
                if (c.Y < wMinY) wMinY = c.Y; if (c.Y > wMaxY) wMaxY = c.Y;
            }
            haveWalls = true;
        }
        bool haveAnyCells = haveGround || haveWalls;

        // Store origin and world offset (pixels) so visuals and gameplay align
        // Origin is based on the union of Ground and Walls minima
        if (haveGround && haveWalls)
            _tilemapOrigin = new Vector2I(Mathf.Min(gMinX, wMinX), Mathf.Min(gMinY, wMinY));
        else if (haveGround)
            _tilemapOrigin = new Vector2I(gMinX, gMinY);
        else if (haveWalls)
            _tilemapOrigin = new Vector2I(wMinX, wMinY);
        else
            _tilemapOrigin = new Vector2I(0, 0);
        
        // Account for TileMapLayer scale when calculating world offset
        float scaleX = _groundLayer != null ? _groundLayer.Scale.X : 1f;
        float scaleY = _groundLayer != null ? _groundLayer.Scale.Y : 1f;
        _tilemapWorldOffset = new Vector2(-_tilemapOrigin.X * CellSize * scaleX, -_tilemapOrigin.Y * CellSize * scaleY);
        
        if (EnableDebugLogging)
        {
            GD.Print($"🧭 TileMap origin: {_tilemapOrigin}, layer scale: ({scaleX}, {scaleY}), world offset: {_tilemapWorldOffset}");
        }

        // Do not re-center visual layers here. We respect positions saved in the scene
        // so editor and runtime positions match while the camera follows the player.

        // Ensure grid allocated
        if (_grid == null || _grid.GetLength(0) != GridWidth || _grid.GetLength(1) != GridHeight)
        {
            _grid = new int[GridWidth, GridHeight];
        }

        // Initialize all to empty
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _grid[x, y] = (int)CellType.Empty;
            }
        }

        // Mark walls from WallLayer only
        bool markedFromLayers = false;
        if (walls != null)
        {
            var used = walls.GetUsedCells();
            foreach (var cell in used)
            {
                // Track wall positions in tilemap coordinates
                _wallTileCoords.Add(cell);
                // Shift indices so negative painted coords map into the 0..Grid bounds
                int gx = cell.X - _tilemapOrigin.X;
                int gy = cell.Y - _tilemapOrigin.Y;
                if (gx >= 0 && gx < GridWidth && gy >= 0 && gy < GridHeight)
                {
                    _grid[gx, gy] = (int)CellType.Wall;
                }
            }
            markedFromLayers = used.Count > 0;
        }
        
        // Track stair tiles from StairLayer for floor transitions
        if (stairs != null)
        {
            var stairCells = stairs.GetUsedCells();
            foreach (var cell in stairCells)
            {
                _stairTileCoords.Add(cell);
            }
        }

        // Choose a reasonable player start on an empty cell; try to center on ground's used region
        Vector2I start = new Vector2I(5, GridHeight / 2);
        Godot.Collections.Array<Vector2I> groundUsed = null;
        if (ground != null)
        {
            groundUsed = ground.GetUsedCells();
        }
        // If there's no GroundLayer, groundUsed remains null
        // Prefer a start near the ground centroid so the player appears at screen center
        bool placed = false;
        if (haveGround && groundUsed != null && groundUsed.Count > 0)
        {
            int cgMinX = int.MaxValue, cgMinY = int.MaxValue, cgMaxX = int.MinValue, cgMaxY = int.MinValue;
            foreach (var c in groundUsed)
            {
                if (c.X < cgMinX) cgMinX = c.X; if (c.X > cgMaxX) cgMaxX = c.X;
                if (c.Y < cgMinY) cgMinY = c.Y; if (c.Y > cgMaxY) cgMaxY = c.Y;
            }
            int cx = (cgMinX + cgMaxX) / 2;
            int cy = (cgMinY + cgMaxY) / 2;
            // Convert to grid coordinates by subtracting origin
            int sx = cx - _tilemapOrigin.X;
            int sy = cy - _tilemapOrigin.Y;
            if (sx >= 0 && sx < GridWidth && sy >= 0 && sy < GridHeight && _grid[sx, sy] != (int)CellType.Wall)
            {
                start = new Vector2I(sx, sy);
                placed = true;
            }
            else
            {
                // Find nearest non-wall around centroid
                bool found = false;
                int maxRadius = 50;
                for (int r = 1; r <= maxRadius && !found; r++)
                {
                    for (int dy = -r; dy <= r && !found; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int gx = sx + dx;
                            int gy = sy + dy;
                            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) continue;
                            if (_grid[gx, gy] != (int)CellType.Wall)
                            {
                                start = new Vector2I(gx, gy);
                                found = true;
                                placed = true;
                                break;
                            }
                        }
                    }
                }
            }
        }
        if (!placed)
        {
            // Fallback: near origin so at least it's predictable with fixed camera
            Vector2I desired = new Vector2I(0, 0);
            if (IsWithinGrid(desired) && _grid[desired.X, desired.Y] != (int)CellType.Wall)
            {
                start = desired;
            }
            else
            {
                bool found = false;
                int maxRadius = 50;
                for (int r = 1; r <= maxRadius && !found; r++)
                {
                    for (int dy = -r; dy <= r && !found; dy++)
                    {
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int gx = desired.X + dx;
                            int gy = desired.Y + dy;
                            if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight) continue;
                            if (_grid[gx, gy] != (int)CellType.Wall)
                            {
                                start = new Vector2I(gx, gy);
                                found = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        _playerPosition = start;
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
    }
    
    [Signal] public delegate void PlayerMovedEventHandler(Vector2I newPosition);
    [Signal] public delegate void EnemyEncounteredEventHandler(Vector2I enemyPosition);
    
    public override void _EnterTree()
    {
        // Ensure processing and drawing happens inside the editor for preview
        if (Engine.IsEditorHint())
        {
            ProcessMode = ProcessModeEnum.Always;
        }
    }
    
    public override void _Ready()
    {
        if (EnableDebugLogging)
            GD.Print($"🔔 GridMap._Ready called! Pending floor: {_pendingFloorDef?.FloorName ?? "none"}");
        
        // Emit signal to notify that GridMap is ready
        EmitSignal(SignalName.GridMapReady);
        
        // If floor load was deferred waiting for scene tree, execute it now
        if (_pendingFloorDef != null)
        {
            if (EnableDebugLogging)
                GD.Print($"✅ GridMap _Ready: Loading deferred floor '{_pendingFloorDef.FloorName}'...");
            LoadFloor(_pendingFloorInstance, _pendingFloorDef, _pendingPlayerSpawn);
            _pendingFloorDef = null;
            _pendingFloorInstance = null;
            return; // LoadFloor handles everything
        }
        
        // Ensure grid data exists before any drawing happens (both editor and runtime)
        if (!UseBakedTileMapsAtRuntime && (_grid == null || _grid.Length == 0))
        {
            InitializeGrid();
        }

        if (Engine.IsEditorHint())
        {
            // Editor preview: optionally use sprites and animation
            UseSprites = EditorPreviewUseSprites;
            _useColorTerrain = !EditorPreviewUseSprites;
            if (EditorPreviewUseSprites)
            {
                LoadSprites();
            }

            // Static workflow: no automatic bake/prefill
        }
        else
        {
            // Load sprites for runtime
            LoadSprites();
            
            // Use sprite-based terrain at runtime by default
            _useColorTerrain = false;
            if (EnableDebugLogging) GD.Print("🎨 TERRAIN DISPLAY: Using sprite textures for terrain");
        }

        CacheTileMapLayers();
        bool forceScale = Engine.IsEditorHint() || UseBakedTileMapsAtRuntime;
        ApplyTileMapScaling(forceScale);

        if (Engine.IsEditorHint())
        {
            ToggleTileMapLayersVisible(true);
        }
        else if (UseBakedTileMapsAtRuntime)
        {
            ToggleTileMapLayersVisible(true);
        }
        else
        {
            ToggleTileMapLayersVisible(false);
            if (HasBakedTileMaps())
            {
                EditorSetBakedVisible(false);
                if (EnableDebugLogging)
                {
                    GD.Print("ℹ️ Baked TileMaps found but disabled for runtime rendering. Using procedural draw.");
                }
            }
        }

        // Report configuration and current baked layer state
        var dbgGround = GetNodeOrNull<TileMapLayer>("GroundLayer");
        var dbgWalls = GetNodeOrNull<TileMapLayer>("WallLayer");
        int dbgGroundCells = dbgGround != null ? dbgGround.GetUsedCells().Count : 0;
        int dbgWallCells = dbgWalls != null ? dbgWalls.GetUsedCells().Count : 0;
        GD.Print($"⚙️ UseBakedTileMapsAtRuntime={UseBakedTileMapsAtRuntime}");
        GD.Print($"🧱 TILEMAPS STATE (pre-build): Ground cells={dbgGroundCells}, Wall cells={dbgWallCells}");
        GD.Print($"🧩 TILESETS: Ground={(dbgGround?.TileSet != null)}, Wall={(dbgWalls?.TileSet != null)}");

        // When using baked TileMaps, build gameplay grid from layers so movement/camera work
        if (UseBakedTileMapsAtRuntime)
        {
            if (_grid == null || _grid.Length == 0)
            {
                BuildGridFromBakedTileMaps();
            }

            // Debug after building grid
            dbgGround = GetNodeOrNull<TileMapLayer>("GroundLayer");
            dbgWalls = GetNodeOrNull<TileMapLayer>("WallLayer");
            dbgGroundCells = dbgGround != null ? dbgGround.GetUsedCells().Count : 0;
            dbgWallCells = dbgWalls != null ? dbgWalls.GetUsedCells().Count : 0;
            GD.Print($"🧱 TILEMAPS STATE (post-build): Ground cells={dbgGroundCells}, Wall cells={dbgWallCells}");
            // Removed runtime debug painting: rely solely on static TileMapLayer content saved in scene
        }
        
        // Debug: Test terrain type detection
        if (!Engine.IsEditorHint() && EnableDebugLogging)
        {
            GD.Print("🔍 TERRAIN TYPE TEST:");
            GD.Print($"  (10,80): {GetTerrainType(10, 80)} (should be starting_area)");
            GD.Print($"  (50,25): {GetTerrainType(50, 25)} (should be forest)");  
            GD.Print($"  (40,110): {GetTerrainType(40, 110)} (should be cave)");
            GD.Print($"  (110,60): {GetTerrainType(110, 60)} (should be desert)");
            GD.Print($"  (40,140): {GetTerrainType(40, 140)} (should be swamp)");
            GD.Print($"  (125,25): {GetTerrainType(125, 25)} (should be mountain)");
            GD.Print($"  (125,100): {GetTerrainType(125, 100)} (should be dungeon)");
        }
        
        // Skip procedural grid initialization and draw when using baked TileMaps
        if (!UseBakedTileMapsAtRuntime)
        {
            InitializeGrid();
            DrawGrid();
        }
        
        // Connect to camera movement to trigger redraws when needed
        if (!Engine.IsEditorHint())
        {
            GetViewport().SizeChanged += () => QueueRedraw();
        }

        // After grid/layers are initialized, register any static enemy spawns placed in the scene
        if (!Engine.IsEditorHint())
        {
            CallDeferred(nameof(RegisterStaticEnemySpawns));
        }
    }
    
    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint())
        {
            if (!EditorPreviewAnimate)
                return;
            
            // Animate in editor when enabled
            _animationTime += (float)delta;
            if (_animationTime >= ANIMATION_SPEED)
            {
                _animationTime = 0.0f;
                _currentFrame = (_currentFrame + 1) % TOTAL_FRAMES;
                QueueRedraw();
            }
            return;
        }
        // Runtime animation
        _animationTime += (float)delta;
        if (_animationTime >= ANIMATION_SPEED)
        {
            _animationTime = 0.0f;
            _currentFrame = (_currentFrame + 1) % TOTAL_FRAMES;
            QueueRedraw(); // Redraw to show next frame
        }
    }
    
    private void LoadSprites()
    {
        try
        {
            if (EnableDebugLogging) GD.Print("🎮 Starting sprite loading...");

            // Load character sprites (use sprite sheets)
            var playerTexture = GD.Load<Texture2D>("res://assets/sprites/characters/player_hero/sprite_sheet.png");
            if (playerTexture != null)
            {
                _cellSprites[CellType.Player] = playerTexture;
                if (EnableDebugLogging)
                {
                    GD.Print("✅ Player sprite loaded");
                    // Debug texture properties
                    GD.Print($"   📏 Size: {playerTexture.GetSize()}");
                    GD.Print($"   🔗 Resource ID: {playerTexture.GetRid()}");
                }
            }
            else
            {
                GD.PrintErr("❌ Player sprite failed to load");
            }

            // Load wall texture
            var wallTexture = GD.Load<Texture2D>("res://assets/sprites/terrain/wall_generic.png");
            if (wallTexture != null)
            {
                _cellSprites[CellType.Wall] = wallTexture;
                if (EnableDebugLogging) GD.Print("✅ Wall texture loaded");
            }
            else
            {
                GD.PrintErr("❌ Wall texture failed to load - using fallback");
            }

            // Load enemy sprites unless we're using baked TileMaps at runtime (to avoid noisy missing asset logs)
            if (!UseBakedTileMapsAtRuntime)
            {
                LoadEnemySprites();
            }
            
            // Load themed terrain (this loads all terrain types)
            LoadThemedTerrain();
            
            if (EnableDebugLogging) GD.Print($"🎯 Sprites loaded! UseSprites: {UseSprites}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"❌ Error loading sprites: {ex.Message}");
            UseSprites = false; // Fallback to colored rectangles
        }
    }

    private void LoadEnemySprites()
    {
        var enemyNames = new string[]
        {
            "enemy_goblin",
            "enemy_orc", 
            "enemy_skeleton_warrior",
            "enemy_troll",
            "enemy_dragon",
            "enemy_forest_spirit",
            "enemy_cave_spider",
            "enemy_desert_scorpion",
            "enemy_swamp_wretch",
            "enemy_mountain_wyvern",
            "enemy_dark_mage",
            "enemy_dungeon_guardian",
            "enemy_demon_lord",
            "enemy_ancient_dragon_king"
        };
        
        var failed = new List<string>();
        foreach (var enemyName in enemyNames)
        {
            // Prefer new path: res://assets/sprites/enemies/{type}/sprite_sheet.png
            var enemyType = enemyName.Replace("enemy_", "");
            string newPath = $"res://assets/sprites/enemies/{enemyType}/sprite_sheet.png";
            string legacyPath = $"res://assets/sprites/characters/{enemyName}/sprite_sheet.png";

            string path = null;
            if (FileAccess.FileExists(newPath)) path = newPath;
            else if (FileAccess.FileExists(legacyPath)) path = legacyPath;

            // Avoid engine error logs by checking existence before loading
            if (path == null)
            {
                failed.Add(enemyName);
                continue;
            }

            var texture = GD.Load<Texture2D>(path);
            if (texture != null)
            {
                _enemySprites[enemyType] = texture;
            }
            else
            {
                failed.Add(enemyName);
                if (EnableDebugLogging)
                {
                    GD.PrintErr($"❌ Failed to load sprite for {enemyName}");
                }
            }
        }

        if (failed.Count > 0 && !EnableDebugLogging)
        {
            // Print a concise summary to avoid log spam
            int show = Math.Min(3, failed.Count);
            string sample = string.Join(", ", failed.GetRange(0, show));
            string more = failed.Count > show ? $", +{failed.Count - show} more" : string.Empty;
            GD.PrintErr($"⚠️ Missing enemy sprite sheets for: {sample}{more}. Enemies will render as colored markers until sprite_sheet.png is added.");
        }
    }

    private void LoadThemedTerrain()
    {
        var terrainTypes = new string[]
        {
            "floor_forest.png",
            "floor_cave.png", 
            "floor_desert.png",
            "floor_swamp.png",
            "floor_mountain.png",
            "floor_dungeon.png",
            "floor_starting_area.png"
        };
        
        if (EnableDebugLogging) GD.Print("🗺️ Loading terrain textures...");
        int loadedCount = 0;
        
        foreach (var filename in terrainTypes)
        {
            var texture = GD.Load<Texture2D>($"res://assets/sprites/terrain/{filename}");
            if (texture != null)
            {
                var terrainType = filename.Replace("floor_", "").Replace(".png", "");
                _terrainSprites[terrainType] = texture;
                
                if (EnableDebugLogging)
                {
                    // Debug texture information
                    var size = texture.GetSize();
                    GD.Print($"✅ Loaded terrain texture: {terrainType} (Size: {size.X}x{size.Y}, Resource ID: {texture.GetRid()})");
                }
                loadedCount++;
            }
            else
            {
                GD.PrintErr($"❌ Failed to load terrain texture: {filename}");
            }
        }
        
        if (EnableDebugLogging)
        {
            GD.Print($"🎯 Total terrain textures loaded: {loadedCount}/{terrainTypes.Length}");
            
            // Print all loaded terrain types for debugging
            GD.Print("📋 Available terrain types:");
            foreach (var kvp in _terrainSprites)
            {
                var texture = kvp.Value;
                var size = texture.GetSize();
                GD.Print($"   - {kvp.Key}: {size.X}x{size.Y}, Resource ID: {texture.GetRid()}");
            }
        }
        
        // Test if all textures have the same resource ID (which would indicate they're the same file)
        var resourceIds = new System.Collections.Generic.HashSet<Rid>();
        foreach (var kvp in _terrainSprites)
        {
            resourceIds.Add(kvp.Value.GetRid());
        }
        
        if (resourceIds.Count == 1 && _terrainSprites.Count > 1)
        {
            GD.PrintErr("⚠️ WARNING: All terrain textures have the same Resource ID - they might be the same file!");
        }
        else if (EnableDebugLogging)
        {
            GD.Print($"✅ Good: Found {resourceIds.Count} unique texture resources");
        }
    }
    
    private void InitializeGrid()
    {
        _grid = new int[GridWidth, GridHeight];
        
        // Create a more complex maze pattern for larger grid
        // Fill with walls first
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _grid[x, y] = (int)CellType.Wall;
            }
        }
        
        // Generate a complex maze structure
        GenerateComplexMaze();
        
        // Create themed areas with different enemy types and densities
        CreateStartingArea();
        CreateForestZone();
        CreateCaveSystem();
        CreateDesertArea();
        CreateSwampLands();
        CreateMountainPeak();
        CreateDungeonComplex();
        CreateBossArena();
        
        // Add additional scattered enemies in corridors
        AddCorridorEnemies();
        
        // Set player starting position
        _playerPosition = new Vector2I(5, GridHeight / 2);
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
    }
    
    private void CreateRoom(int startX, int startY, int width, int height, int enemyCount)
    {
        // Clear the room area
        for (int x = startX; x < startX + width && x < GridWidth - 1; x++)
        {
            for (int y = startY; y < startY + height && y < GridHeight - 1; y++)
            {
                _grid[x, y] = (int)CellType.Empty;
            }
        }
        
        // Add enemies randomly in the room
        var random = new Random();
        for (int i = 0; i < enemyCount; i++)
        {
            int attempts = 0;
            while (attempts < 50) // Prevent infinite loop
            {
                int x = random.Next(startX + 1, Mathf.Min(startX + width - 1, GridWidth - 1));
                int y = random.Next(startY + 1, Mathf.Min(startY + height - 1, GridHeight - 1));
                
                if (_grid[x, y] == (int)CellType.Empty)
                {
                    _grid[x, y] = (int)CellType.Enemy;
                    break;
                }
                attempts++;
            }
        }
    }
    
    private void GenerateComplexMaze()
    {
        // Create a main pathway system with multiple routes
        CreateMainPathways();
        
        // Add winding corridors
        CreateWindingPaths();
        
        // Create interconnected chambers
        CreateInterconnectedChambers();
        
        // Add secret passages
        CreateSecretPassages();
    }
    
    private void CreateMainPathways()
    {
        // Primary horizontal corridor
        for (int x = 1; x < GridWidth - 1; x++)
        {
            _grid[x, GridHeight / 2] = (int)CellType.Empty;
        }
        
        // Primary vertical corridor
        for (int y = 1; y < GridHeight - 1; y++)
        {
            _grid[GridWidth / 2, y] = (int)CellType.Empty;
        }
        
        // Secondary pathways at quarters
        for (int x = 1; x < GridWidth - 1; x++)
        {
            _grid[x, GridHeight / 4] = (int)CellType.Empty;
            _grid[x, 3 * GridHeight / 4] = (int)CellType.Empty;
        }
        
        for (int y = 1; y < GridHeight - 1; y++)
        {
            _grid[GridWidth / 4, y] = (int)CellType.Empty;
            _grid[3 * GridWidth / 4, y] = (int)CellType.Empty;
        }
    }
    
    private void CreateWindingPaths()
    {
        var random = new Random();
        
        // Create winding paths connecting different areas
        for (int i = 0; i < 10; i++)
        {
            int startX = random.Next(10, GridWidth - 10);
            int startY = random.Next(10, GridHeight - 10);
            int endX = random.Next(10, GridWidth - 10);
            int endY = random.Next(10, GridHeight - 10);
            
            CreateWindingPath(startX, startY, endX, endY);
        }
    }
    
    private void CreateWindingPath(int startX, int startY, int endX, int endY)
    {
        int currentX = startX;
        int currentY = startY;
        var random = new Random();
        
        while (currentX != endX || currentY != endY)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            
            // Randomly choose direction, biased toward target
            if (random.NextDouble() < 0.7) // 70% chance to move toward target
            {
                if (currentX < endX) currentX++;
                else if (currentX > endX) currentX--;
                else if (currentY < endY) currentY++;
                else if (currentY > endY) currentY--;
            }
            else // 30% chance for random movement
            {
                int direction = random.Next(4);
                switch (direction)
                {
                    case 0: if (currentX > 1) currentX--; break;
                    case 1: if (currentX < GridWidth - 2) currentX++; break;
                    case 2: if (currentY > 1) currentY--; break;
                    case 3: if (currentY < GridHeight - 2) currentY++; break;
                }
            }
        }
    }
    
    private void CreateInterconnectedChambers()
    {
        // Create larger chamber areas connected by corridors
        var chambers = new (int x, int y, int w, int h)[]
        {
            (20, 20, 15, 15),
            (50, 30, 20, 18),
            (90, 25, 25, 20),
            (25, 60, 18, 22),
            (70, 70, 30, 25),
            (110, 80, 20, 15),
            (30, 110, 25, 20),
            (80, 120, 35, 25),
            (130, 130, 20, 20)
        };
        
        foreach (var (x, y, w, h) in chambers)
        {
            CreateChamber(x, y, w, h);
        }
    }
    
    private void CreateChamber(int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width && x < GridWidth - 1; x++)
        {
            for (int y = startY; y < startY + height && y < GridHeight - 1; y++)
            {
                _grid[x, y] = (int)CellType.Empty;
            }
        }
    }
    
    private void CreateSecretPassages()
    {
        var random = new Random();
        
        // Add some hidden shortcuts
        for (int i = 0; i < 5; i++)
        {
            int x1 = random.Next(20, GridWidth - 20);
            int y1 = random.Next(20, GridHeight - 20);
            int x2 = random.Next(20, GridWidth - 20);
            int y2 = random.Next(20, GridHeight - 20);
            
            // Create a narrow secret passage
            CreateStraightPath(x1, y1, x2, y2);
        }
    }
    
    private void CreateStraightPath(int startX, int startY, int endX, int endY)
    {
        // Create straight line paths for secret passages
        int dx = Math.Sign(endX - startX);
        int dy = Math.Sign(endY - startY);
        
        int currentX = startX;
        int currentY = startY;
        
        while (currentX != endX)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            currentX += dx;
        }
        
        while (currentY != endY)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            currentY += dy;
        }
    }
    
    private void CreateStartingArea()
    {
        // Safe starting zone with weak enemies
        CreateThematicArea(5, GridHeight / 2 - 10, 30, 20, 3, "starting");
    }
    
    private void CreateForestZone()
    {
        // Forest area with goblins and orcs
        CreateThematicArea(40, 15, 35, 30, 12, "forest");
        CreateThematicArea(45, 50, 25, 25, 8, "forest");
    }
    
    private void CreateCaveSystem()
    {
        // Underground cave system with skeleton warriors
        CreateThematicArea(20, 90, 40, 35, 15, "cave");
        CreateThematicArea(70, 95, 30, 30, 10, "cave");
    }
    
    private void CreateDesertArea()
    {
        // Desert region with varied enemies
        CreateThematicArea(90, 40, 45, 40, 18, "desert");
    }
    
    private void CreateSwampLands()
    {
        // Swamp area with trolls and dark creatures
        CreateThematicArea(25, 130, 35, 25, 14, "swamp");
        CreateThematicArea(70, 135, 25, 20, 10, "swamp");
    }
    
    private void CreateMountainPeak()
    {
        // High-altitude area with dragons and powerful enemies
        CreateThematicArea(110, 15, 40, 35, 16, "mountain");
    }
    
    private void CreateDungeonComplex()
    {
        // Multi-level dungeon with various high-level enemies - adjusted bounds
        CreateThematicArea(115, 85, 30, 35, 18, "dungeon");
    }
    
    private void CreateBossArena()
    {
        // Final boss area - moved slightly inward from edge
        CreateThematicArea(135, 135, 20, 20, 3, "boss");
    }
    
    private void CreateThematicArea(int startX, int startY, int width, int height, int enemyCount, string theme)
    {
        // Ensure area bounds are within grid limits
        int endX = Math.Min(startX + width, GridWidth);
        int endY = Math.Min(startY + height, GridHeight);
        
        // Clear the area
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                if (x > 0 && y > 0 && x < GridWidth && y < GridHeight)
                    _grid[x, y] = (int)CellType.Empty;
            }
        }
        
        // Add enemies based on theme
        var random = new Random();
        for (int i = 0; i < enemyCount; i++)
        {
            int attempts = 0;
            while (attempts < 100)
            {
                // Ensure enemy placement is within both area bounds and grid bounds
                int maxX = Math.Min(endX - 1, GridWidth - 1);
                int maxY = Math.Min(endY - 1, GridHeight - 1);
                
                if (startX + 1 >= maxX || startY + 1 >= maxY)
                    break; // Area too small for enemy placement
                    
                int x = random.Next(startX + 1, maxX);
                int y = random.Next(startY + 1, maxY);
                
                if (x > 0 && y > 0 && x < GridWidth && y < GridHeight && _grid[x, y] == (int)CellType.Empty)
                {
                    _grid[x, y] = (int)CellType.Enemy;
                    break;
                }
                attempts++;
            }
        }
    }
    
    private void AddCorridorEnemies()
    {
        var random = new Random();
        int corridorEnemies = 30; // Add scattered enemies in corridors
        
        for (int i = 0; i < corridorEnemies; i++)
        {
            int attempts = 0;
            while (attempts < 200)
            {
                // Ensure we stay well within grid boundaries (leave 1-cell border)
                int x = random.Next(2, GridWidth - 2);
                int y = random.Next(2, GridHeight - 2);
                
                if (_grid[x, y] == (int)CellType.Empty)
                {
                    // Only place in corridors (areas with limited adjacent empty spaces)
                    int emptyNeighbors = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < GridWidth && ny >= 0 && ny < GridHeight &&
                                _grid[nx, ny] == (int)CellType.Empty)
                            {
                                emptyNeighbors++;
                            }
                        }
                    }
                    
                    // Place enemy in narrow corridors (2-4 empty neighbors)
                    if (emptyNeighbors >= 2 && emptyNeighbors <= 4)
                    {
                        _grid[x, y] = (int)CellType.Enemy;
                        break;
                    }
                }
                attempts++;
            }
        }
    }
    
    private void CacheTileMapLayers()
    {
        _groundLayer = GetNodeOrNull<TileMapLayer>("GroundLayer");
        _wallLayer = GetNodeOrNull<TileMapLayer>("WallLayer");
    }

    private void ApplyTileMapScaling(bool scaleLayers)
    {
        float scaleFactor = scaleLayers ? CellSize / TERRAIN_BASE_PIXEL_SIZE : 1.0f;
        if (_groundLayer != null)
        {
            _groundLayer.Scale = new Vector2(scaleFactor, scaleFactor);
        }

        if (_wallLayer != null)
        {
            _wallLayer.Scale = new Vector2(scaleFactor, scaleFactor);
        }
    }

    private void ToggleTileMapLayersVisible(bool visible)
    {
        if (_groundLayer != null)
        {
            _groundLayer.Visible = visible;
        }

        if (_wallLayer != null)
        {
            _wallLayer.Visible = visible;
        }
    }

    private void DrawGrid()
    {
        // Just queue a redraw - the actual drawing happens in _Draw()
        QueueRedraw();
    }
    
    public override void _Draw()
    {
        // When using baked TileMaps, never perform procedural draw
        if (UseBakedTileMapsAtRuntime)
        {
            return;
        }
        // Safety: if grid data isn't ready for any reason, skip draw this frame
        if (_grid == null || _grid.Length == 0)
        {
            return;
        }
        // If we have baked TileMaps and we're running the game, skip procedural draw to avoid duplicates
        // (Handled above by the unconditional early return when UseBakedTileMapsAtRuntime is true)
        // Get camera for viewport culling, with fallback if not available
        Camera2D camera = GetViewport().GetCamera2D();
        Vector2 cameraPos = camera?.GlobalPosition ?? Vector2.Zero;
        Vector2 viewportSize = GetViewportRect().Size;
        float zoom = camera?.Zoom.X ?? 0.5f;
        
        // Calculate visible area with padding
        int padding = 10;
        int startX, endX, startY, endY;
        if (Engine.IsEditorHint())
        {
            // In editor: draw a top-left preview region or the full map
            startX = 0;
            startY = 0;
            if (EditorPreviewFullMap)
            {
                endX = GridWidth;
                endY = GridHeight;
            }
            else
            {
                endX = Mathf.Min(GridWidth, EditorPreviewSize.X);
                endY = Mathf.Min(GridHeight, EditorPreviewSize.Y);
            }
        }
        else if (camera != null)
        {
            // Use viewport culling for performance
            startX = Mathf.Max(0, (int)((cameraPos.X - viewportSize.X / zoom / 2) / CellSize) - padding);
            endX = Mathf.Min(GridWidth, (int)((cameraPos.X + viewportSize.X / zoom / 2) / CellSize) + padding);
            startY = Mathf.Max(0, (int)((cameraPos.Y - viewportSize.Y / zoom / 2) / CellSize) - padding);
            endY = Mathf.Min(GridHeight, (int)((cameraPos.Y + viewportSize.Y / zoom / 2) / CellSize) + padding);
        }
        else
        {
            // Fallback: draw a smaller area around the player if no camera
            Vector2I playerPos = GetPlayerPosition();
            int range = 20;
            startX = Mathf.Max(0, playerPos.X - range);
            endX = Mathf.Min(GridWidth, playerPos.X + range);
            startY = Mathf.Max(0, playerPos.Y - range);
            endY = Mathf.Min(GridHeight, playerPos.Y + range);
        }
        
        // Draw only visible cells
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                Vector2 cellPos = new Vector2(x * CellSize, y * CellSize);
                CellType cellType = (CellType)_grid[x, y];
                
                if (UseSprites)
                {
                    DrawCellWithSprite(cellPos, x, y, cellType);
                }
                else
                {
                    DrawCellWithColor(cellPos, x, y, cellType);
                }
            }
        }
    }

    private void DrawCellWithSprite(Vector2 cellPos, int x, int y, CellType cellType)
    {
        // Handle walls separately - they don't need terrain background
        if (cellType == CellType.Wall)
        {
            DrawWallSprite(cellPos, x, y);
            return;
        }
        
        // Draw terrain background for non-wall cells
        DrawTerrainSprite(cellPos, x, y);
        
        // Draw entity sprite on top of terrain
        if (cellType == CellType.Player)
        {
            if (_cellSprites.ContainsKey(CellType.Player))
            {
                DrawAnimatedSprite(_cellSprites[CellType.Player], cellPos);
            }
            else
            {
                // Fallback to a solid blue rectangle if player sprite is missing
                Vector2 cellSize = new Vector2(CellSize, CellSize);
                DrawRect(new Rect2(cellPos, cellSize), Colors.Blue);
            }
        }
        else if (cellType == CellType.Enemy)
        {
            DrawEnemySprite(cellPos, x, y);
        }
    }

    private void DrawWallSprite(Vector2 cellPos, int x, int y)
    {
        // Try to draw wall texture if available
        if (_cellSprites.ContainsKey(CellType.Wall))
        {
            // Draw with transparency support
            DrawTexture(_cellSprites[CellType.Wall], cellPos, modulate: new Color(1, 1, 1, 1));
        }
        else
        {
            // Fallback to dark gray solid color for walls
            Vector2 cellSize = new Vector2(CellSize, CellSize);
            DrawRect(new Rect2(cellPos, cellSize), Colors.DarkGray);
            DrawRect(new Rect2(cellPos, cellSize), Colors.Black, false, 1.0f); // Border
        }
    }

    private void DrawTerrainSprite(Vector2 cellPos, int x, int y)
    {
        string terrainType = GetTerrainType(x, y);
        
        // Optional debug: log some terrain requests
        if (EnableDebugLogging)
        {
            if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
            {
                GD.Print($"🗺️ Cell ({x},{y}) requesting terrain: {terrainType}");
                GD.Print($"� Contains '{terrainType}'? {_terrainSprites.ContainsKey(terrainType)}");
                if (_terrainSprites.ContainsKey(terrainType))
                {
                    var texture = _terrainSprites[terrainType];
                    GD.Print($"🎨 Texture for {terrainType} is null? {texture == null}");
                }
            }
        }
        
        if (_terrainSprites.ContainsKey(terrainType))
        {
            var texture = _terrainSprites[terrainType];
            if (texture != null)
            {
                // Draw terrain scaled to the current cell size so higher-resolution textures (e.g. 96x96) fit the grid
                Rect2 destRect = new Rect2(cellPos, new Vector2(CellSize, CellSize));
                DrawTextureRect(texture, destRect, tile: false, modulate: new Color(1, 1, 1, 1));
                if (EnableDebugLogging)
                {
                    if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
                    {
                        GD.Print($"✅ Drew {terrainType} texture at ({x},{y})");
                    }
                }
                return;
            }
            else
            {
                if (EnableDebugLogging)
                {
                    if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
                    {
                        GD.Print($"❌ Texture for {terrainType} is null at ({x},{y})");
                    }
                }
            }
        }
        else
        {
            if (EnableDebugLogging)
            {
                if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
                {
                    GD.Print($"❌ No texture found for {terrainType} at ({x},{y})");
                }
            }
        }
        
        // Fallback chain
        if (_terrainSprites.ContainsKey("starting_area"))
        {
            if (EnableDebugLogging)
            {
                if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
                {
                    GD.Print($"⚠️ Using starting_area fallback for {terrainType} at ({x},{y})");
                }
            }
            var fallbackTexture = _terrainSprites["starting_area"];
            Rect2 fallbackRect = new Rect2(cellPos, new Vector2(CellSize, CellSize));
            DrawTextureRect(fallbackTexture, fallbackRect, tile: false, modulate: new Color(1, 1, 1, 1));
        }
        else
        {
            // Final fallback to colored rectangle
            if (EnableDebugLogging)
            {
                if ((x % 10 == 0 && y % 10 == 0) && (x < 50 && y < 50))
                {
                    GD.Print($"🎨 Using color fallback for {terrainType}");
                }
            }
            Vector2 cellSize = new Vector2(CellSize, CellSize);
            DrawRect(new Rect2(cellPos, cellSize), GetAreaColor(x, y));
        }
    }

    private void DrawEnemySprite(Vector2 cellPos, int x, int y)
    {
        string enemyType = GetEnemyTypeForPosition(x, y);
        
        if (_enemySprites.ContainsKey(enemyType))
        {
            var sprite = _enemySprites[enemyType];
            if (sprite != null)
            {
                DrawAnimatedSprite(sprite, cellPos);
            }
            else
            {
                // Sprite is null - fallback
                Vector2 cellSize = new Vector2(CellSize, CellSize);
                DrawRect(new Rect2(cellPos, cellSize), GetEnemyColor(x, y));
            }
        }
        else
        {
            // Enemy type not found in sprites dictionary - fallback
            Vector2 cellSize = new Vector2(CellSize, CellSize);
            DrawRect(new Rect2(cellPos, cellSize), GetEnemyColor(x, y));
        }
    }

    private void DrawAnimatedSprite(Texture2D spriteSheet, Vector2 position)
    {
        if (spriteSheet == null)
        {
            return;
        }

        // Calculate source rectangle for current frame dynamically (assume 4 frames horizontally)
        var size = spriteSheet.GetSize();
        int frameWidth = Mathf.Max(1, Mathf.RoundToInt(size.X) / 4);
        int frameHeight = Mathf.Max(1, Mathf.RoundToInt(size.Y));
        int frameX = _currentFrame * frameWidth;

        Rect2 sourceRect = new Rect2(frameX, 0, frameWidth, frameHeight);
        // Always render at grid cell size to keep on-screen size consistent regardless of sheet resolution
        Rect2 destRect = new Rect2(position, new Vector2(CellSize, CellSize));

        // Draw with transparency support - use CanvasItem.DrawTextureRectRegion
        DrawTextureRectRegion(spriteSheet, destRect, sourceRect, modulate: new Color(1, 1, 1, 1), transpose: false);
    }

    private void DrawCellWithColor(Vector2 cellPos, int x, int y, CellType cellType)
    {
        // Original colored rectangle drawing (fallback)
        Vector2 cellSize = new Vector2(CellSize, CellSize);
        Rect2 cellRect = new Rect2(cellPos, cellSize);
        
        Color cellColor = GetCellColor(x, y, cellType);
        DrawRect(cellRect, cellColor);
        DrawRect(cellRect, Colors.Black, false, 1.0f);
    }

    private string GetTerrainType(int x, int y)
    {
        // Determine terrain type based on area with better debugging
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) 
        {
            return "starting_area";
        }
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) 
        {
            return "forest";
        }
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) 
        {
            return "cave";
        }
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) 
        {
            return "desert";
        }
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) 
        {
            return "swamp";
        }
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) 
        {
            return "mountain";
        }
        if (IsInAreaBounds(x, y, 115, 85, 30, 35)) 
        {
            return "dungeon";
        }
        if (IsInAreaBounds(x, y, 135, 135, 20, 20)) 
        {
            return "dungeon"; // Boss arena uses dungeon texture
        }
        
        // For corridors and paths, use starting_area as default
        return "starting_area";
    }

    private string GetEnemyTypeForPosition(int x, int y)
    {
        // Use position-based deterministic "random" instead of GD.Randf() 
        // to ensure enemy types don't change every frame
        int seed = x * 1000 + y;
        float pseudoRand = (seed % 100) / 100.0f;
        
        // Return appropriate enemy sprite based on area
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) 
            return pseudoRand < 0.8f ? "goblin" : "orc";
        
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25))
        {
            if (pseudoRand < 0.4f) return "goblin";
            else if (pseudoRand < 0.7f) return "orc";
            else return "forest_spirit";
        }
        
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30))
        {
            if (pseudoRand < 0.4f) return "skeleton_warrior";
            else if (pseudoRand < 0.7f) return "cave_spider";
            else return "troll";
        }
        
        if (IsInAreaBounds(x, y, 90, 40, 45, 40))
        {
            return pseudoRand < 0.5f ? "desert_scorpion" : "orc";
        }
        
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20))
        {
            return pseudoRand < 0.6f ? "swamp_wretch" : "troll";
        }
        
        if (IsInAreaBounds(x, y, 110, 15, 40, 35))
        {
            return pseudoRand < 0.6f ? "mountain_wyvern" : "dragon";
        }
        
        if (IsInAreaBounds(x, y, 115, 85, 30, 35))
        {
            return pseudoRand < 0.5f ? "dungeon_guardian" : "dark_mage";
        }
        
        if (IsInAreaBounds(x, y, 135, 135, 20, 20))
        {
            return pseudoRand < 0.7f ? "demon_lord" : "ancient_dragon_king";
        }
        
        // Default corridor enemies
        return "goblin";
    }
    
    private Color GetCellColor(int x, int y, CellType cellType)
    {
        if (cellType == CellType.Player) return Colors.Blue;
        if (cellType == CellType.Enemy) return GetEnemyColor(x, y);
        if (cellType == CellType.Wall) return Colors.DarkGray;
        
        // Empty cells get themed colors based on area
        return GetAreaColor(x, y);
    }
    
    private Color GetEnemyColor(int x, int y)
    {
        // Different enemy colors based on area theme
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) return Colors.Pink; // Starting area - weak
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) return Colors.Green; // Forest
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) return Colors.Brown; // Caves
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) return Colors.Yellow; // Desert
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) return Colors.Purple; // Swamp
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) return Colors.White; // Mountain
        if (IsInAreaBounds(x, y, 115, 85, 30, 35)) return Colors.DarkRed; // Dungeon
        if (IsInAreaBounds(x, y, 135, 135, 20, 20)) return Colors.Gold; // Boss arena
        
        return Colors.Red; // Default corridor enemies
    }
    
    private Color GetAreaColor(int x, int y)
    {
        // Themed background colors for different areas
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) return new Color(0.9f, 0.9f, 0.9f); // Starting - light gray
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) return new Color(0.8f, 0.9f, 0.8f); // Forest - light green
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) return new Color(0.7f, 0.7f, 0.7f); // Caves - darker gray
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) return new Color(0.9f, 0.9f, 0.7f); // Desert - sandy
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) return new Color(0.7f, 0.8f, 0.7f); // Swamp - murky green
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) return new Color(0.9f, 0.9f, 1.0f); // Mountain - light blue
        if (IsInAreaBounds(x, y, 115, 85, 30, 35)) return new Color(0.6f, 0.6f, 0.6f); // Dungeon - dark gray
        if (IsInAreaBounds(x, y, 135, 135, 20, 20)) return new Color(1.0f, 0.9f, 0.7f); // Boss arena - golden
        
        return Colors.LightGray; // Default corridor color
    }
    
    private bool IsInAreaBounds(int x, int y, int areaX, int areaY, int width, int height)
    {
        return x >= areaX && x < areaX + width && y >= areaY && y < areaY + height;
    }
    
    // Helpers for unbounded movement: treat out-of-bounds as empty/walkable
    private bool IsWithinGrid(Vector2I pos)
    {
        return pos.X >= 0 && pos.X < GridWidth && pos.Y >= 0 && pos.Y < GridHeight;
    }

    private CellType GetCellTypeAt(Vector2I pos)
    {
        if (!IsWithinGrid(pos))
        {
            return CellType.Empty;
        }
        return (CellType)_grid[pos.X, pos.Y];
    }
    
    public bool TryMovePlayer(Vector2I direction)
    {
        // Ensure grid exists before using it
        if (_grid == null || _grid.Length == 0)
        {
            if (UseBakedTileMapsAtRuntime)
            {
                BuildGridFromBakedTileMaps();
            }
            else
            {
                InitializeGrid();
            }
        }

        Vector2I newPosition = _playerPosition + direction;
        
        if (EnableDebugLogging)
            GD.Print($"🚶 TryMovePlayer: current={_playerPosition}, direction={direction}, new={newPosition}");
        
        // Compute corresponding tilemap coordinates and block if a wall exists there
        Vector2I newTileCoord = new Vector2I(newPosition.X + _tilemapOrigin.X, newPosition.Y + _tilemapOrigin.Y);
        
        if (EnableDebugLogging)
            GD.Print($"   Tilemap coord: {newTileCoord}, is wall: {_wallTileCoords.Contains(newTileCoord)}");
        
        if (_wallTileCoords.Contains(newTileCoord))
        {
            if (EnableDebugLogging)
                GD.Print($"   ❌ Blocked by wall at tilemap {newTileCoord}");
            return false;
        }
        
        // No hard bounds: treat any out-of-bounds cell as empty (walkable)
        CellType targetCell = GetCellTypeAt(newPosition);
        
        if (EnableDebugLogging)
            GD.Print($"   Cell type: {targetCell}");
        
        // Check if movement is valid
        if (targetCell == CellType.Wall)
        {
            if (EnableDebugLogging)
                GD.Print($"   ❌ Blocked by wall in grid");
            return false;
        }
        
        // Handle enemy encounter
        if (targetCell == CellType.Enemy)
        {
            EmitSignal(SignalName.EnemyEncountered, newPosition);
            return false; // Don't move onto enemy, handle in battle
        }
        
        // Move player (guard grid writes by bounds)
        if (IsWithinGrid(_playerPosition))
        {
            _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Empty;
        }
        _playerPosition = newPosition;
        if (IsWithinGrid(_playerPosition))
        {
            _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
        }
        
        if (EnableDebugLogging)
        {
            Vector2 worldPos = GetWorldPosition(newPosition);
            GD.Print($"   ✅ Move successful! Grid: {newPosition}, World: {worldPos}");
        }
        
        QueueRedraw();
        EmitSignal(SignalName.PlayerMoved, newPosition);
        return true;
    }
    
    public void RemoveEnemy(Vector2I position)
    {
        if (position.X >= 0 && position.X < GridWidth && 
            position.Y >= 0 && position.Y < GridHeight)
        {
            if (_grid[position.X, position.Y] == (int)CellType.Enemy)
            {
                _grid[position.X, position.Y] = (int)CellType.Empty;
                // Remove any static enemy spawn node at this grid position
                RemoveEnemySpawnNode(position);
                QueueRedraw();
            }
        }
    }
    
    /// <summary>
    /// Initialize grid from a loaded floor instance (used by FloorManager)
    /// </summary>
    public void LoadFloor(Node2D floorInstance, FloorDefinition floorDef, Vector2I playerSpawn)
    {
        if (EnableDebugLogging)
        {
            GD.Print($"🗺️ GridMap.LoadFloor called for: {floorDef.FloorName}");
            GD.Print($"   GridMap status: InTree={IsInsideTree()}, NodeReady={IsNodeReady()}");
            GD.Print($"   GridMap localPos={Position}, globalPos={GlobalPosition}");
            GD.Print($"   FloorInstance: {floorInstance.Name} localPos={floorInstance.Position}, globalPos={floorInstance.GlobalPosition}");
        }
        
        // Cache TileMapLayers - they are direct children of this GridMap instance
        _groundLayer = GetNodeOrNull<TileMapLayer>("GroundLayer");
        _wallLayer = GetNodeOrNull<TileMapLayer>("WallLayer");
        
        if (_groundLayer == null || _wallLayer == null)
        {
            GD.PushError($"Floor '{floorDef.FloorName}' missing TileMapLayers! Ground: {_groundLayer != null}, Wall: {_wallLayer != null}");
            return;
        }
        
        if (EnableDebugLogging)
        {
            GD.Print($"✓ Found layers - Ground cells: {_groundLayer.GetUsedCells().Count}, Wall cells: {_wallLayer.GetUsedCells().Count}");
            GD.Print($"📍 GridMap in tree: {IsInsideTree()}, ready: {IsNodeReady()}");
            GD.Print($"📍 GroundLayer: pos={_groundLayer.Position}, scale={_groundLayer.Scale}, visible={_groundLayer.Visible}, z_index={_groundLayer.ZIndex}");
            GD.Print($"📍 GroundLayer in tree: {_groundLayer.IsInsideTree()}, ready: {_groundLayer.IsNodeReady()}");
            GD.Print($"📍 WallLayer: pos={_wallLayer.Position}, scale={_wallLayer.Scale}, visible={_wallLayer.Visible}, z_index={_wallLayer.ZIndex}");
            GD.Print($"📍 WallLayer in tree: {_wallLayer.IsInsideTree()}, ready: {_wallLayer.IsNodeReady()}");
        }
        
        // Ensure TileMapLayers are visible
        _groundLayer.Visible = true;
        _wallLayer.Visible = true;
        _groundLayer.Show();
        _wallLayer.Show();
        
        // Rebuild grid from baked TileMaps (this calculates _tilemapWorldOffset)
        BuildGridFromBakedTileMaps();
        
        // CRITICAL: TileMapLayers must be in scene tree to render!
        // Defer positioning to next frame to ensure scene tree is fully ready
        if (!_groundLayer.IsInsideTree())
        {
            if (EnableDebugLogging)
                GD.Print($"⚠️ TileMapLayers not in scene tree yet, deferring to next frame...");
            CallDeferred(nameof(PositionTileMapLayers));
        }
        else
        {
            // Already in tree, position immediately
            PositionTileMapLayers();
        }
        
        // Set player position from floor definition
        _playerPosition = playerSpawn;
        if (IsWithinGrid(playerSpawn))
        {
            _grid[playerSpawn.X, playerSpawn.Y] = (int)CellType.Player;
        }
        else
        {
            GD.PushWarning($"Player spawn {playerSpawn} is outside grid bounds! Using fallback.");
            // Find nearest valid position
            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    if (_grid[x, y] != (int)CellType.Wall)
                    {
                        _playerPosition = new Vector2I(x, y);
                        _grid[x, y] = (int)CellType.Player;
                        break;
                    }
                }
                if (_grid[_playerPosition.X, _playerPosition.Y] == (int)CellType.Player)
                    break;
            }
        }
        
        // Register all EnemySpawn nodes from this floor
        CallDeferred(nameof(RegisterStaticEnemySpawns));
        
        // Register all StairConnection nodes from this floor
        CallDeferred(nameof(RegisterStairConnections), floorDef);
        
        // Force a redraw to display the loaded floor
        QueueRedraw();
        
        if (EnableDebugLogging)
            GD.Print($"✅ GridMap loaded for floor: {floorDef.FloorName}, player at {playerSpawn}");
    }
    
    /// <summary>
    /// Scan for StairConnection nodes and register them in the FloorDefinition
    /// </summary>
    private void RegisterStairConnections(FloorDefinition floorDef)
    {
        if (floorDef == null) return;
        
        // Clear existing stair data
        floorDef.StairsUp.Clear();
        floorDef.StairsDown.Clear();
        floorDef.StairsUpDestinations.Clear();
        floorDef.StairsDownDestinations.Clear();
        
        // Find all StairConnection nodes (they should be children of GridMap)
        var stairConnections = new List<StairConnection>();
        foreach (Node child in GetChildren())
        {
            if (child is StairConnection stair)
            {
                stairConnections.Add(stair);
            }
        }
        
        if (EnableDebugLogging)
            GD.Print($"🪜 Found {stairConnections.Count} StairConnection nodes in {floorDef.FloorName}");
        
        // Get FloorManager to register stairs globally
        var floorManager = GetNode<FloorManager>("/root/Game/FloorManager");
        
        // Register each stair connection
        foreach (var stair in stairConnections)
        {
            // Register in global registry if it has an ID
            if (!string.IsNullOrEmpty(stair.StairId) && floorManager != null)
            {
                floorManager.RegisterStair(stair.StairId, stair);
            }
            
            // Resolve destination from various sources
            Vector2I? destination = null;
            
            if (stair.UseCustomDestination)
            {
                // Priority 1: Use custom destination coordinates
                destination = stair.CustomDestination;
                if (EnableDebugLogging)
                    GD.Print($"  📍 Using custom destination: {stair.CustomDestination}");
            }
            else if (!string.IsNullOrEmpty(stair.DestinationStairId))
            {
                // Priority 2: Resolve by DestinationStairId (cross-scene linking)
                // Need to defer this because the target floor might not be loaded yet
                if (EnableDebugLogging)
                    GD.Print($"  🎯 Will resolve destination stair ID: '{stair.DestinationStairId}'");
                // Destination will be resolved when player uses the stair
            }
            else if (!string.IsNullOrEmpty(stair.LinkedStairPath))
            {
                // Priority 3: Try to resolve linked stair (same scene only)
                var linkedStair = GetNodeOrNull<StairConnection>(stair.LinkedStairPath);
                if (linkedStair != null)
                {
                    destination = linkedStair.GridPosition;
                    if (EnableDebugLogging)
                        GD.Print($"  🔗 Linked to {stair.LinkedStairPath} at {linkedStair.GridPosition}");
                }
                else if (EnableDebugLogging)
                {
                    GD.Print($"  ⚠️ Could not resolve NodePath (same-scene only): {stair.LinkedStairPath}");
                }
            }
            
            // Add to floor definition
            if (stair.Direction == StairDirection.Up)
            {
                floorDef.StairsUp.Add(stair.GridPosition);
                if (destination.HasValue)
                {
                    floorDef.StairsUpDestinations.Add(destination.Value);
                }
                
                if (EnableDebugLogging)
                    GD.Print($"  ↑ Stair Up at {stair.GridPosition} → Floor {stair.TargetFloor}" + 
                            (destination.HasValue ? $" @ {destination.Value}" : ""));
            }
            else
            {
                floorDef.StairsDown.Add(stair.GridPosition);
                if (destination.HasValue)
                {
                    floorDef.StairsDownDestinations.Add(destination.Value);
                }
                
                if (EnableDebugLogging)
                    GD.Print($"  ↓ Stair Down at {stair.GridPosition} → Floor {stair.TargetFloor}" + 
                            (destination.HasValue ? $" @ {destination.Value}" : ""));
            }
        }
    }
    
    /// <summary>
    /// Resolve stair destination by finding matching stair on target floor
    /// </summary>
    private void ResolveStairDestination(StairConnection stair, FloorManager floorManager)
    {
        if (stair == null || floorManager == null) return;
        
        // Find the opposite direction stair on the target floor
        var oppositeDirection = stair.Direction == StairDirection.Up ? StairDirection.Down : StairDirection.Up;
        
        // Try to find matching stair by ID first
        string oppositeId = stair.Direction == StairDirection.Up ? 
            $"{stair.TargetFloor}f_to_{_currentFloorDef?.FloorNumber}f" : 
            $"{stair.TargetFloor}f_to_{_currentFloorDef?.FloorNumber}f";
        
        var matchedStair = floorManager.FindStairOnFloor(stair.TargetFloor, oppositeDirection);
        if (matchedStair != null && EnableDebugLogging)
        {
            GD.Print($"  🔄 Auto-matched stair on floor {stair.TargetFloor} at {matchedStair.GridPosition}");
        }
    }
    
    private FloorDefinition _currentFloorDef;
    
    private void PositionTileMapLayers()
    {
        // TileMapLayers should stay at (0,0) - tiles are already painted at correct tilemap coords in the scene
        // The offset is applied in GetWorldPosition() instead
        _groundLayer.Position = Vector2.Zero;
        _wallLayer.Position = Vector2.Zero;
        
        if (EnableDebugLogging)
        {
            GD.Print($"🎯 Positioned TileMapLayers at world offset: {_tilemapWorldOffset}");
            GD.Print($"   GroundLayer.Position: {_groundLayer.Position}, GlobalPosition: {_groundLayer.GlobalPosition}");
            GD.Print($"   GroundLayer in scene tree: {_groundLayer.IsInsideTree()}, visible: {_groundLayer.Visible}");
            GD.Print($"   GroundLayer parent: {_groundLayer.GetParent()?.Name}, modulate: {_groundLayer.Modulate}");
            GD.Print($"   GroundLayer z_index: {_groundLayer.ZIndex}, z_as_relative: {_groundLayer.ZAsRelative}");
            var firstTile = _groundLayer.GetUsedCells()[0];
            GD.Print($"   First ground tile at tilemap coords: {firstTile}");
            // Calculate where this tile actually renders (accounting for scale)
            Vector2 tilePixelOffset = (Vector2)firstTile * CellSize * _groundLayer.Scale.X;
            Vector2 tileWorldPos = _groundLayer.GlobalPosition + tilePixelOffset;
            GD.Print($"   First tile renders at world position: {tileWorldPos}");
            GD.Print($"   Scaled tile size: {CellSize} * {_groundLayer.Scale.X} = {CellSize * _groundLayer.Scale.X} pixels");
        }
    }
    
    public Vector2I GetPlayerPosition()
    {
        return _playerPosition;
    }
    
    /// <summary>
    /// Check if a grid position has a stair tile
    /// </summary>
    public bool IsOnStairs(Vector2I gridPosition)
    {
        // Convert grid position to tilemap coordinates
        Vector2I tileCoord = new Vector2I(gridPosition.X + _tilemapOrigin.X, gridPosition.Y + _tilemapOrigin.Y);
        bool hasStair = _stairTileCoords.Contains(tileCoord);
        
        if (EnableDebugLogging)
        {
            GD.Print($"🔍 GridMap.IsOnStairs: gridPos={gridPosition}, tileCoord={tileCoord}, hasStair={hasStair}");
            GD.Print($"   _tilemapOrigin={_tilemapOrigin}, _stairTileCoords.Count={_stairTileCoords.Count}");
            if (_stairTileCoords.Count > 0)
            {
                GD.Print($"   Stair tiles: {string.Join(", ", _stairTileCoords)}");
            }
        }
        
        return hasStair;
    }
    
    public Vector2 GetWorldPosition(Vector2I gridPosition)
    {
        // Map grid coordinates to world space using TileMapLayer's coordinate system
        // 1. Convert grid to tilemap coordinates using origin
        int tileX = gridPosition.X + _tilemapOrigin.X;
        int tileY = gridPosition.Y + _tilemapOrigin.Y;
        
        // Use TileMapLayer's map_to_local() to get the actual world position
        // This accounts for scale, tile size, and any internal TileMapLayer positioning
        if (_groundLayer != null)
        {
            Vector2 localPos = _groundLayer.MapToLocal(new Vector2I(tileX, tileY));
            // MapToLocal gives us the center of the tile in the layer's local space
            // Since GroundLayer is a child of GridMap at (0,0), this is also world space
            return _groundLayer.ToGlobal(localPos);
        }
        
        // Fallback if no ground layer
        return Vector2.Zero;
    }

    // ===== Static enemy spawns (scene-placed) =====
    private void RegisterStaticEnemySpawns()
    {
        var nodes = GetTree().GetNodesInGroup("EnemySpawn");
        foreach (Node n in nodes)
        {
            if (n is EnemySpawn spawn)
            {
                // Convert from tilemap-local coordinates to internal grid coordinates
                Vector2I gp = spawn.GridPosition;
                Vector2I gg = new Vector2I(gp.X - _tilemapOrigin.X, gp.Y - _tilemapOrigin.Y);
                if (gg.X >= 0 && gg.X < GridWidth && gg.Y >= 0 && gg.Y < GridHeight)
                {
                    _grid[gg.X, gg.Y] = (int)CellType.Enemy;
                }
                // Ensure visual alignment with current layer offset
                spawn.UpdateVisual(this);
            }
        }
    }

    private void RemoveEnemySpawnNode(Vector2I position)
    {
        var nodes = GetTree().GetNodesInGroup("EnemySpawn");
        foreach (Node n in nodes)
        {
            if (n is EnemySpawn spawn)
            {
                // Convert internal grid coords back to tilemap-local for comparison
                Vector2I tilePos = new Vector2I(position.X + _tilemapOrigin.X,
                                                position.Y + _tilemapOrigin.Y);
                if (spawn.GridPosition == tilePos)
                {
                    spawn.QueueFree();
                    break;
                }
            }
        }
    }
}
