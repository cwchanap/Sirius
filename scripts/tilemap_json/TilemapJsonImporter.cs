using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.TilemapJson;

/// <summary>
/// Imports LLM-friendly JSON back into a floor scene.
/// Updates TileMapLayers and manages scene entities (EnemySpawn, StairConnection).
/// </summary>
[Tool]
public partial class TilemapJsonImporter : RefCounted
{
    private TileConfigManager _config;

    // Packed scenes for entity instantiation (loaded lazily)
    private PackedScene _enemySpawnScene;
    private PackedScene _stairConnectionScene;

    public TilemapJsonImporter()
    {
        _config = new TileConfigManager();
    }

    /// <summary>
    /// Import JSON model into the GridMap node.
    /// </summary>
    public Error ImportToScene(FloorJsonModel model, Node2D gridMapNode)
    {
        if (!_config.IsLoaded)
        {
            var err = _config.LoadConfig();
            if (err != Error.Ok)
            {
                GD.PrintErr("[TilemapJsonImporter] Failed to load tile config");
                return err;
            }
        }

        // Import tile layers
        ImportTileLayers(model.TileLayers, gridMapNode);

        // Import entities
        ImportEntities(model.Entities, gridMapNode);

        GD.Print("[TilemapJsonImporter] Import complete");
        return Error.Ok;
    }

    /// <summary>
    /// Import from JSON file.
    /// </summary>
    public Error ImportFromFile(string jsonPath, Node2D gridMapNode)
    {
        if (!FileAccess.FileExists(jsonPath))
        {
            GD.PrintErr($"[TilemapJsonImporter] JSON file not found: {jsonPath}");
            return Error.FileNotFound;
        }

        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[TilemapJsonImporter] Failed to open JSON file: {jsonPath}");
            return Error.CantOpen;
        }

        string json = file.GetAsText();
        var model = FloorJsonModel.FromJson(json);

        if (model == null)
        {
            GD.PrintErr("[TilemapJsonImporter] Failed to parse JSON");
            return Error.ParseError;
        }

        return ImportToScene(model, gridMapNode);
    }

    private void ImportTileLayers(Dictionary<string, List<TileData>> layers, Node2D gridMapNode)
    {
        // Import each layer type
        if (layers.TryGetValue("ground", out var groundTiles))
        {
            var groundLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("GroundLayer");
            if (groundLayer != null)
            {
                ImportTileLayer(groundTiles, groundLayer, "ground");
            }
        }

        if (layers.TryGetValue("wall", out var wallTiles))
        {
            var wallLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("WallLayer");
            if (wallLayer != null)
            {
                ImportTileLayer(wallTiles, wallLayer, "wall");
            }
        }

        if (layers.TryGetValue("stair", out var stairTiles))
        {
            var stairLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("StairLayer");
            if (stairLayer != null)
            {
                ImportTileLayer(stairTiles, stairLayer, "stair");
            }
        }
    }

    private void ImportTileLayer(List<TileData> tiles, TileMapLayer layer, string layerType)
    {
        // Clear existing tiles
        layer.Clear();

        int successCount = 0;
        int failCount = 0;

        foreach (var tile in tiles)
        {
            var mapping = _config.GetMapping(layerType, tile.Tile);
            if (mapping == null)
            {
                GD.PrintErr($"[TilemapJsonImporter] Unknown tile '{tile.Tile}' in layer '{layerType}'");
                failCount++;
                continue;
            }

            var cellPos = new Vector2I(tile.X, tile.Y);
            var atlasCoords = mapping.GetAtlasCoord();

            layer.SetCell(cellPos, mapping.SourceId, atlasCoords, tile.Alternative);
            successCount++;
        }

        GD.Print($"[TilemapJsonImporter] Layer '{layerType}': {successCount} tiles imported, {failCount} failed");
    }

    private void ImportEntities(SceneEntities entities, Node2D gridMapNode)
    {
        // Import enemy spawns
        ImportEnemySpawns(entities.EnemySpawns, gridMapNode);

        // Import stair connections
        ImportStairConnections(entities.StairConnections, gridMapNode);
    }

    private void ImportEnemySpawns(List<EnemySpawnData> spawns, Node2D gridMapNode)
    {
        // Get existing enemy spawn nodes
        var existingSpawns = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child.Name.ToString().Contains("EnemySpawn"))
            {
                existingSpawns[child.Name.ToString()] = child;
            }
        }

        // Track which spawns we've processed
        var processedIds = new HashSet<string>();

        foreach (var spawnData in spawns)
        {
            processedIds.Add(spawnData.Id);

            if (existingSpawns.TryGetValue(spawnData.Id, out var existingNode))
            {
                // Update existing node
                UpdateEnemySpawnNode(existingNode, spawnData);
            }
            else
            {
                // Create new node
                CreateEnemySpawnNode(spawnData, gridMapNode);
            }
        }

        // Remove spawns that are no longer in JSON
        foreach (var (id, node) in existingSpawns)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing enemy spawn: {id}");
                node.QueueFree();
            }
        }
    }

    private void UpdateEnemySpawnNode(Node node, EnemySpawnData data)
    {
        // Update GridPosition
        node.Set("GridPosition", data.Position.ToVector2I());

        // Update EnemyType
        if (!string.IsNullOrEmpty(data.EnemyType))
        {
            node.Set("EnemyType", data.EnemyType);
        }

        // Update position in world (assuming 32px cell size with 0.333 scale factor)
        if (node is Node2D node2d)
        {
            // Position = GridPosition * CellSize (32) * scale adjustment
            node2d.Position = new Vector2(data.Position.X * 32, data.Position.Y * 32);
        }

        GD.Print($"[TilemapJsonImporter] Updated enemy spawn: {data.Id}");
    }

    private void CreateEnemySpawnNode(EnemySpawnData data, Node2D parent)
    {
        // Try to load the appropriate enemy spawn scene
        string scenePath = $"res://scenes/spawns/EnemySpawn_{CapitalizeFirst(data.EnemyType)}.tscn";

        if (!ResourceLoader.Exists(scenePath))
        {
            GD.PrintErr($"[TilemapJsonImporter] Enemy spawn scene not found: {scenePath}");
            return;
        }

        var scene = GD.Load<PackedScene>(scenePath);
        var instance = scene.Instantiate();

        if (instance == null)
        {
            GD.PrintErr($"[TilemapJsonImporter] Failed to instantiate: {scenePath}");
            return;
        }

        // Configure the spawn
        instance.Name = data.Id;
        instance.Set("GridPosition", data.Position.ToVector2I());

        if (!string.IsNullOrEmpty(data.EnemyType))
        {
            instance.Set("EnemyType", data.EnemyType);
        }

        if (instance is Node2D node2d)
        {
            node2d.Position = new Vector2(data.Position.X * 32, data.Position.Y * 32);
            node2d.Scale = new Vector2(0.333333f, 0.333333f);
            node2d.ZIndex = 2;
        }

        // Add to parent
        parent.AddChild(instance);

        // Set owner for editor persistence
        if (Engine.IsEditorHint())
        {
            var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
            if (sceneRoot != null)
            {
                instance.Owner = sceneRoot;
            }
        }

        GD.Print($"[TilemapJsonImporter] Created enemy spawn: {data.Id}");
    }

    private void ImportStairConnections(List<StairConnectionData> stairs, Node2D gridMapNode)
    {
        // Get existing stair nodes
        var existingStairs = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            var stairId = child.Get("StairId");
            if (stairId.VariantType != Variant.Type.Nil)
            {
                existingStairs[stairId.AsString()] = child;
            }
        }

        // Track which stairs we've processed
        var processedIds = new HashSet<string>();

        foreach (var stairData in stairs)
        {
            processedIds.Add(stairData.Id);

            if (existingStairs.TryGetValue(stairData.Id, out var existingNode))
            {
                // Update existing node
                UpdateStairConnectionNode(existingNode, stairData);
            }
            else
            {
                // Create new node - for now just log, as stair creation is more complex
                GD.Print($"[TilemapJsonImporter] New stair connection needed: {stairData.Id} (manual creation required)");
            }
        }

        // Remove stairs that are no longer in JSON
        foreach (var (id, node) in existingStairs)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing stair connection: {id}");
                node.QueueFree();
            }
        }
    }

    private void UpdateStairConnectionNode(Node node, StairConnectionData data)
    {
        // Update GridPosition
        node.Set("GridPosition", data.Position.ToVector2I());

        // Update Direction (0 = up, 1 = down)
        int direction = data.Direction?.ToLower() == "down" ? 1 : 0;
        node.Set("Direction", direction);

        // Update TargetFloor
        node.Set("TargetFloor", data.TargetFloor);

        // Update DestinationStairId
        if (!string.IsNullOrEmpty(data.DestinationStairId))
        {
            node.Set("DestinationStairId", data.DestinationStairId);
        }

        // Update position in world
        if (node is Node2D node2d)
        {
            node2d.Position = new Vector2(data.Position.X * 32, data.Position.Y * 32);
        }

        GD.Print($"[TilemapJsonImporter] Updated stair connection: {data.Id}");
    }

    private static string CapitalizeFirst(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1).ToLower();
    }
}
