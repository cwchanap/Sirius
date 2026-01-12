using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.TilemapJson;

/// <summary>
/// Exports a floor scene to LLM-friendly JSON format.
/// Reads TileMapLayers and scene entities (EnemySpawn, StairConnection).
/// </summary>
[Tool]
public partial class TilemapJsonExporter : RefCounted
{
    private TileConfigManager _config;

    public TilemapJsonExporter()
    {
        _config = new TileConfigManager();
    }

    /// <summary>
    /// Export a floor scene to FloorJsonModel.
    /// </summary>
    public FloorJsonModel ExportScene(Node2D gridMapNode, FloorDefinition floorDef = null)
    {
        if (!_config.IsLoaded)
        {
            var err = _config.LoadConfig();
            if (err != Error.Ok)
            {
                GD.PrintErr("[TilemapJsonExporter] Failed to load tile config");
                return null;
            }
        }

        var model = new FloorJsonModel();

        // Export metadata
        model.Metadata = ExportMetadata(gridMapNode, floorDef);

        // Export tile layers
        model.TileLayers = ExportTileLayers(gridMapNode);

        // Export entities
        model.Entities = ExportEntities(gridMapNode);

        return model;
    }

    /// <summary>
    /// Export to JSON file.
    /// </summary>
    public Error ExportToFile(Node2D gridMapNode, string outputPath, FloorDefinition floorDef = null)
    {
        var model = ExportScene(gridMapNode, floorDef);
        if (model == null)
        {
            return Error.Failed;
        }

        string json = model.ToJson(indented: true);

        using var file = FileAccess.Open(outputPath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[TilemapJsonExporter] Failed to open output file: {outputPath}");
            return Error.CantOpen;
        }

        file.StoreString(json);
        GD.Print($"[TilemapJsonExporter] Exported to {outputPath}");
        return Error.Ok;
    }

    private FloorMetadata ExportMetadata(Node2D gridMapNode, FloorDefinition floorDef)
    {
        var metadata = new FloorMetadata();

        if (floorDef != null)
        {
            metadata.FloorName = floorDef.FloorName;
            metadata.FloorNumber = floorDef.FloorNumber;
            metadata.Description = floorDef.FloorDescription;
            metadata.PlayerStart = new Vector2IData(floorDef.PlayerStartPosition);
        }
        else
        {
            // Try to infer from scene name
            var sceneName = gridMapNode.GetParent()?.Name ?? gridMapNode.Name;
            metadata.FloorName = sceneName.ToString();
        }

        return metadata;
    }

    private Dictionary<string, List<TileData>> ExportTileLayers(Node2D gridMapNode)
    {
        var layers = new Dictionary<string, List<TileData>>();

        // Export GroundLayer
        var groundLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("GroundLayer");
        if (groundLayer != null)
        {
            layers["ground"] = ExportTileLayer(groundLayer, "ground");
        }

        // Export WallLayer
        var wallLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("WallLayer");
        if (wallLayer != null)
        {
            layers["wall"] = ExportTileLayer(wallLayer, "wall");
        }

        // Export StairLayer
        var stairLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("StairLayer");
        if (stairLayer != null)
        {
            layers["stair"] = ExportTileLayer(stairLayer, "stair");
        }

        return layers;
    }

    private List<TileData> ExportTileLayer(TileMapLayer layer, string layerType)
    {
        var tiles = new List<TileData>();
        var usedCells = layer.GetUsedCells();

        foreach (var cell in usedCells)
        {
            var sourceId = layer.GetCellSourceId(cell);
            var atlasCoords = layer.GetCellAtlasCoords(cell);
            var altId = layer.GetCellAlternativeTile(cell);

            // Convert source ID to human-readable tile name
            string tileName = _config.GetTileName(layerType, sourceId);

            tiles.Add(new TileData(cell.X, cell.Y, tileName, altId));
        }

        // Sort by Y then X for consistent output
        tiles = tiles.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();

        return tiles;
    }

    private SceneEntities ExportEntities(Node2D gridMapNode)
    {
        var entities = new SceneEntities();

        // Export EnemySpawn nodes
        entities.EnemySpawns = ExportEnemySpawns(gridMapNode);

        // Export StairConnection nodes
        entities.StairConnections = ExportStairConnections(gridMapNode);

        return entities;
    }

    private List<EnemySpawnData> ExportEnemySpawns(Node2D gridMapNode)
    {
        var spawns = new List<EnemySpawnData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            // Check if it's an EnemySpawn node (by checking for GridPosition property)
            if (child is Sprite2D sprite && child.Name.ToString().Contains("EnemySpawn"))
            {
                var spawnData = new EnemySpawnData
                {
                    Id = child.Name.ToString()
                };

                // Get GridPosition via property
                if (child.HasMethod("get") || child.Get("GridPosition").VariantType != Variant.Type.Nil)
                {
                    var gridPos = child.Get("GridPosition").AsVector2I();
                    spawnData.Position = new Vector2IData(gridPos);
                }

                // Get EnemyType
                var enemyType = child.Get("EnemyType");
                if (enemyType.VariantType != Variant.Type.Nil)
                {
                    spawnData.EnemyType = enemyType.AsString();
                }

                // Get Blueprint if available
                var blueprint = child.Get("Blueprint");
                if (blueprint.VariantType != Variant.Type.Nil && blueprint.Obj is Resource blueprintRes)
                {
                    spawnData.Blueprint = blueprintRes.ResourcePath;

                    // Extract stats from blueprint with safe property access
                    spawnData.Stats = new EnemyStatsData
                    {
                        Level = GetIntProperty(blueprintRes, "Level", 1),
                        MaxHealth = GetIntProperty(blueprintRes, "MaxHealth", 50),
                        Attack = GetIntProperty(blueprintRes, "Attack", 10),
                        Defense = GetIntProperty(blueprintRes, "Defense", 5),
                        Speed = GetIntProperty(blueprintRes, "Speed", 10),
                        ExpReward = GetIntProperty(blueprintRes, "ExperienceReward", 20),
                        GoldReward = GetIntProperty(blueprintRes, "GoldReward", 10)
                    };
                }

                spawns.Add(spawnData);
            }
        }

        return spawns;
    }

    private List<StairConnectionData> ExportStairConnections(Node2D gridMapNode)
    {
        var stairs = new List<StairConnectionData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            // Check if it's a StairConnection node
            if (child.Name.ToString().Contains("Stair") && child is Node2D stairNode)
            {
                var stairId = child.Get("StairId");
                if (stairId.VariantType == Variant.Type.Nil)
                {
                    continue; // Not a StairConnection node
                }

                var stairData = new StairConnectionData
                {
                    Id = stairId.AsString()
                };

                // Get GridPosition
                var gridPos = child.Get("GridPosition");
                if (gridPos.VariantType != Variant.Type.Nil)
                {
                    stairData.Position = new Vector2IData(gridPos.AsVector2I());
                }

                // Get Direction
                var direction = child.Get("Direction");
                if (direction.VariantType != Variant.Type.Nil)
                {
                    // Direction is likely an enum (0=up, 1=down)
                    stairData.Direction = direction.AsInt32() == 0 ? "up" : "down";
                }

                // Get TargetFloor
                var targetFloor = child.Get("TargetFloor");
                if (targetFloor.VariantType != Variant.Type.Nil)
                {
                    stairData.TargetFloor = targetFloor.AsInt32();
                }

                // Get DestinationStairId
                var destId = child.Get("DestinationStairId");
                if (destId.VariantType != Variant.Type.Nil)
                {
                    stairData.DestinationStairId = destId.AsString();
                }

                stairs.Add(stairData);
            }
        }

        return stairs;
    }

    /// <summary>
    /// Safely get an integer property from a Godot object with a default fallback.
    /// </summary>
    private static int GetIntProperty(GodotObject obj, string propertyName, int defaultValue)
    {
        var prop = obj.Get(propertyName);
        return prop.VariantType != Variant.Type.Nil ? prop.AsInt32() : defaultValue;
    }
}
