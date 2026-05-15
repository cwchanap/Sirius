using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.TilemapJson;

/// <summary>
/// Exports a floor scene to LLM-friendly JSON format.
/// Reads TileMapLayers and scene entities (EnemySpawn, NpcSpawn, StairConnection).
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
        else
        {
            GD.PrintErr("[TilemapJsonExporter] GroundLayer node not found — ground tiles omitted from export");
        }

        // Export WallLayer
        var wallLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("WallLayer");
        if (wallLayer != null)
        {
            layers["wall"] = ExportTileLayer(wallLayer, "wall");
        }
        else
        {
            GD.PrintErr("[TilemapJsonExporter] WallLayer node not found — wall tiles omitted from export");
        }

        // Export StairLayer
        var stairLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("StairLayer");
        if (stairLayer != null)
        {
            layers["stair"] = ExportTileLayer(stairLayer, "stair");
        }
        else
        {
            GD.PrintErr("[TilemapJsonExporter] StairLayer node not found — stair tiles omitted from export");
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

        entities.EnemySpawns = ExportEnemySpawns(gridMapNode);
        entities.NpcSpawns = ExportNpcSpawns(gridMapNode);
        entities.TreasureBoxes = ExportTreasureBoxes(gridMapNode);
        entities.TrapTiles = ExportTrapTiles(gridMapNode);
        entities.PuzzleSwitches = ExportPuzzleSwitches(gridMapNode);
        entities.PuzzleGates = ExportPuzzleGates(gridMapNode);
        entities.PuzzleRiddles = ExportPuzzleRiddles(gridMapNode);
        entities.StairConnections = ExportStairConnections(gridMapNode);

        return entities;
    }

    private List<EnemySpawnData> ExportEnemySpawns(Node2D gridMapNode)
    {
        var spawns = new List<EnemySpawnData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (!child.Name.ToString().Contains("EnemySpawn"))
                continue;

            var spawnData = new EnemySpawnData
            {
                Id = child.Name.ToString()
            };

            // Get GridPosition via property
            var gridPos = child.Get("GridPosition");
            if (gridPos.VariantType != Variant.Type.Nil)
            {
                spawnData.Position = new Vector2IData(gridPos.AsVector2I());
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

        return spawns;
    }

    private List<NpcSpawnData> ExportNpcSpawns(Node2D gridMapNode)
    {
        var spawns = new List<NpcSpawnData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is NpcSpawn spawn)
            {
                spawns.Add(new NpcSpawnData
                {
                    Id = child.Name.ToString(),
                    Position = new Vector2IData(spawn.GridPosition),
                    NpcId = spawn.NpcId
                });
            }
        }

        return spawns;
    }

    private List<TreasureBoxData> ExportTreasureBoxes(Node2D gridMapNode)
    {
        var boxes = new List<TreasureBoxData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is not TreasureBoxSpawn box)
            {
                continue;
            }

            var data = new TreasureBoxData
            {
                Id = string.IsNullOrWhiteSpace(box.TreasureBoxId) ? child.Name.ToString() : box.TreasureBoxId,
                Position = new Vector2IData(box.GridPosition),
                Gold = box.RewardGold
            };

            if (box.RewardItemIds == null)
            {
                boxes.Add(data);
                continue;
            }

            for (int i = 0; i < box.RewardItemIds.Count; i++)
            {
                data.Items.Add(new TreasureBoxItemData
                {
                    ItemId = box.RewardItemIds[i],
                    Quantity = box.RewardItemQuantities != null && i < box.RewardItemQuantities.Count
                        ? box.RewardItemQuantities[i]
                        : 1
                });
            }

            boxes.Add(data);
        }

        return boxes;
    }

    private List<TrapTileData> ExportTrapTiles(Node2D gridMapNode)
    {
        var trapTiles = new List<TrapTileData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is not TrapTileSpawn trapTile)
            {
                continue;
            }

            trapTiles.Add(new TrapTileData
            {
                Id = child.Name.ToString(),
                PuzzleId = trapTile.PuzzleId,
                Position = new Vector2IData(trapTile.GridPosition),
                Damage = trapTile.Damage,
                StatusEffect = trapTile.StatusEffectId,
                StatusMagnitude = trapTile.StatusMagnitude,
                StatusTurns = trapTile.StatusTurns
            });
        }

        return trapTiles;
    }

    private List<PuzzleSwitchData> ExportPuzzleSwitches(Node2D gridMapNode)
    {
        var switches = new List<PuzzleSwitchData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is not PuzzleSwitchSpawn puzzleSwitch)
            {
                continue;
            }

            switches.Add(new PuzzleSwitchData
            {
                Id = string.IsNullOrWhiteSpace(puzzleSwitch.SwitchId) ? child.Name.ToString() : puzzleSwitch.SwitchId,
                PuzzleId = puzzleSwitch.PuzzleId,
                Position = new Vector2IData(puzzleSwitch.GridPosition),
                PromptText = puzzleSwitch.PromptText,
                ActivatedText = puzzleSwitch.ActivatedText
            });
        }

        return switches;
    }

    private List<PuzzleGateData> ExportPuzzleGates(Node2D gridMapNode)
    {
        var gates = new List<PuzzleGateData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is not PuzzleGateSpawn gate)
            {
                continue;
            }

            gates.Add(new PuzzleGateData
            {
                Id = string.IsNullOrWhiteSpace(gate.GateId) ? child.Name.ToString() : gate.GateId,
                PuzzleId = gate.PuzzleId,
                Position = new Vector2IData(gate.GridPosition),
                StartsClosed = gate.StartsClosed
            });
        }

        return gates;
    }

    private List<PuzzleRiddleData> ExportPuzzleRiddles(Node2D gridMapNode)
    {
        var riddles = new List<PuzzleRiddleData>();

        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is not PuzzleRiddleSpawn riddle)
            {
                continue;
            }

            var data = new PuzzleRiddleData
            {
                Id = string.IsNullOrWhiteSpace(riddle.RiddleId) ? child.Name.ToString() : riddle.RiddleId,
                PuzzleId = riddle.PuzzleId,
                Position = new Vector2IData(riddle.GridPosition),
                PromptText = riddle.PromptText,
                CorrectChoiceId = riddle.CorrectChoiceId,
                WrongAnswerDamage = riddle.WrongAnswerDamage
            };

            if (riddle.ChoiceIds != null)
            {
                for (int i = 0; i < riddle.ChoiceIds.Count; i++)
                {
                    string choiceId = riddle.ChoiceIds[i];
                    data.Choices.Add(new PuzzleRiddleChoiceData
                    {
                        Id = choiceId,
                        Label = riddle.ChoiceLabels != null && i < riddle.ChoiceLabels.Count
                            ? riddle.ChoiceLabels[i]
                            : choiceId
                    });
                }
            }

            riddles.Add(data);
        }

        return riddles;
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
