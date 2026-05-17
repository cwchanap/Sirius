using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.TilemapJson;

/// <summary>
/// Imports LLM-friendly JSON back into a floor scene.
/// Updates TileMapLayers and manages scene entities (EnemySpawn, NpcSpawn, StairConnection).
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
        if (gridMapNode == null)
        {
            GD.PrintErr("[TilemapJsonImporter] Cannot import to null gridMapNode");
            return Error.InvalidParameter;
        }

        if (model == null)
        {
            GD.PrintErr("[TilemapJsonImporter] Cannot import null model");
            return Error.InvalidParameter;
        }

        if (!_config.IsLoaded)
        {
            var err = _config.LoadConfig();
            if (err != Error.Ok)
            {
                GD.PrintErr("[TilemapJsonImporter] Failed to load tile config");
                return err;
            }
        }

        if (model.TileLayers == null)
        {
            GD.PrintErr("[TilemapJsonImporter] Cannot import: model.TileLayers is null");
            return Error.InvalidParameter;
        }

        if (model.Entities == null)
        {
            GD.PrintErr("[TilemapJsonImporter] Cannot import: model.Entities is null");
            return Error.InvalidParameter;
        }

        ConfigureGridMapBounds(model.TileLayers, gridMapNode);

        // Import tile layers
        int totalTileFailures = ImportTileLayers(model.TileLayers, gridMapNode);

        // Import entities
        ImportEntities(model.Entities, gridMapNode);

        if (totalTileFailures > 0)
        {
            GD.PrintErr($"[TilemapJsonImporter] Import completed with {totalTileFailures} tile mapping failures");
            return Error.Failed;
        }

        GD.Print("[TilemapJsonImporter] Import complete");
        return Error.Ok;
    }

    private static void ConfigureGridMapBounds(Dictionary<string, List<TileData>> layers, Node2D gridMapNode)
    {
        if (gridMapNode is not GridMap gridMap)
            return;

        if (!layers.TryGetValue("ground", out var groundTiles) || groundTiles == null || groundTiles.Count == 0)
            return;

        int maxX = groundTiles.Max(tile => tile.X);
        int maxY = groundTiles.Max(tile => tile.Y);
        int width = maxX + 1;
        int height = maxY + 1;
        if (width <= 0 || height <= 0)
            return;

        gridMap.GridWidth = width;
        gridMap.GridHeight = height;
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

    private int ImportTileLayers(Dictionary<string, List<TileData>> layers, Node2D gridMapNode)
    {
        int totalFailures = 0;

        // Import each layer type
        if (layers.TryGetValue("ground", out var groundTiles))
        {
            var groundLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("GroundLayer");
            if (groundLayer != null)
            {
                totalFailures += ImportTileLayer(groundTiles, groundLayer, "ground");
            }
            else
            {
                GD.PrintErr("[TilemapJsonImporter] GroundLayer node not found — ground tiles discarded");
            }
        }

        if (layers.TryGetValue("wall", out var wallTiles))
        {
            var wallLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("WallLayer");
            if (wallLayer != null)
            {
                totalFailures += ImportTileLayer(wallTiles, wallLayer, "wall");
            }
            else
            {
                GD.PrintErr("[TilemapJsonImporter] WallLayer node not found — wall tiles discarded");
            }
        }

        if (layers.TryGetValue("stair", out var stairTiles))
        {
            var stairLayer = gridMapNode.GetNodeOrNull<TileMapLayer>("StairLayer");
            if (stairLayer != null)
            {
                totalFailures += ImportTileLayer(stairTiles, stairLayer, "stair");
            }
            else
            {
                GD.PrintErr("[TilemapJsonImporter] StairLayer node not found — stair tiles discarded");
            }
        }

        return totalFailures;
    }

    private int ImportTileLayer(List<TileData> tiles, TileMapLayer layer, string layerType)
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

        if (failCount > 0)
            GD.PrintErr($"[TilemapJsonImporter] Layer '{layerType}': {successCount} tiles imported, {failCount} failed");
        else
            GD.Print($"[TilemapJsonImporter] Layer '{layerType}': {successCount} tiles imported");

        return failCount;
    }

    private void ImportEntities(SceneEntities entities, Node2D gridMapNode)
    {
        if (entities.EnemySpawns != null)
            ImportEnemySpawns(entities.EnemySpawns, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] enemy_spawns key missing from JSON — enemy spawns not imported");
        if (entities.NpcSpawns != null)
            ImportNpcSpawns(entities.NpcSpawns, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] npc_spawns key missing from JSON — NPC spawns not imported");
        if (entities.TreasureBoxes != null)
            ImportTreasureBoxes(entities.TreasureBoxes, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] treasure_boxes key missing from JSON — treasure boxes not imported");
        if (entities.TrapTiles != null)
            ImportTrapTiles(entities.TrapTiles, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] trap_tiles key missing from JSON — trap tiles not imported");
        if (entities.PuzzleSwitches != null)
            ImportPuzzleSwitches(entities.PuzzleSwitches, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] puzzle_switches key missing from JSON — puzzle switches not imported");
        if (entities.PuzzleGates != null)
            ImportPuzzleGates(entities.PuzzleGates, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] puzzle_gates key missing from JSON — puzzle gates not imported");
        if (entities.PuzzleRiddles != null)
            ImportPuzzleRiddles(entities.PuzzleRiddles, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] puzzle_riddles key missing from JSON — puzzle riddles not imported");
        if (entities.StairConnections != null)
            ImportStairConnections(entities.StairConnections, gridMapNode);
        else
            GD.PrintErr("[TilemapJsonImporter] stair_connections key missing from JSON — stair connections not imported");
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
                node.Free();
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

        // Update position in world
        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
        }

        GD.Print($"[TilemapJsonImporter] Updated enemy spawn: {data.Id}");
    }

    private void CreateEnemySpawnNode(EnemySpawnData data, Node2D parent)
    {
        string scenePath = $"res://scenes/spawns/EnemySpawn_{ToPascalCase(data.EnemyType)}.tscn";
        Node instance;

        if (ResourceLoader.Exists(scenePath))
        {
            var scene = GD.Load<PackedScene>(scenePath);
            if (scene == null)
            {
                GD.PrintErr($"[TilemapJsonImporter] Failed to load scene: {scenePath}");
                return;
            }
            instance = scene.Instantiate();
            if (instance == null)
            {
                GD.PrintErr($"[TilemapJsonImporter] Failed to instantiate: {scenePath}");
                return;
            }
        }
        else
        {
            instance = new EnemySpawn();
            GD.PrintErr($"[TilemapJsonImporter] Spawn scene not found for '{data.EnemyType}', created generic EnemySpawn");
        }

        instance.Name = data.Id;
        instance.Set("GridPosition", data.Position.ToVector2I());

        if (!string.IsNullOrEmpty(data.EnemyType))
        {
            instance.Set("EnemyType", data.EnemyType);
        }

        if (instance is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
            node2d.Scale = new Vector2(0.333333f, 0.333333f);
            node2d.ZIndex = 2;
        }

        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created enemy spawn: {data.Id}");
    }

    private void ImportNpcSpawns(List<NpcSpawnData> spawns, Node2D gridMapNode)
    {
        var existingSpawns = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is NpcSpawn)
            {
                existingSpawns[child.Name.ToString()] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var spawnData in spawns)
        {
            processedIds.Add(spawnData.Id);

            if (existingSpawns.TryGetValue(spawnData.Id, out var existingNode))
            {
                UpdateNpcSpawnNode(existingNode, spawnData);
            }
            else
            {
                CreateNpcSpawnNode(spawnData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingSpawns)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing NPC spawn: {id}");
                node.Free();
            }
        }
    }

    private void UpdateNpcSpawnNode(Node node, NpcSpawnData data)
    {
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("NpcId", data.NpcId);

        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
        }

        GD.Print($"[TilemapJsonImporter] Updated NPC spawn: {data.Id}");
    }

    private void CreateNpcSpawnNode(NpcSpawnData data, Node2D parent)
    {
        var instance = new NpcSpawn();
        instance.Name = data.Id;
        instance.GridPosition = data.Position.ToVector2I();
        instance.NpcId = data.NpcId;
        instance.Position = ToCenteredCellPosition(data.Position);
        instance.ZIndex = 2;

        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created NPC spawn: {data.Id}");
    }

    private void ImportTreasureBoxes(List<TreasureBoxData> boxes, Node2D gridMapNode)
    {
        var existingBoxes = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is TreasureBoxSpawn box)
            {
                existingBoxes[GetTreasureBoxImportKey(box)] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var boxData in boxes)
        {
            if (string.IsNullOrWhiteSpace(boxData.Id))
            {
                GD.PrintErr($"[TilemapJsonImporter] Skipping treasure box with empty/whitespace id at position {boxData.Position}.");
                continue;
            }

            processedIds.Add(boxData.Id);

            if (existingBoxes.TryGetValue(boxData.Id, out var existingNode))
            {
                UpdateTreasureBoxNode(existingNode, boxData);
            }
            else
            {
                CreateTreasureBoxNode(boxData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingBoxes)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing treasure box: {id}");
                node.Free();
            }
        }
    }

    private void UpdateTreasureBoxNode(Node node, TreasureBoxData data)
    {
        ConfigureTreasureBoxNode(node, data);
        GD.Print($"[TilemapJsonImporter] Updated treasure box: {data.Id}");
    }

    private void CreateTreasureBoxNode(TreasureBoxData data, Node2D parent)
    {
        var instance = new TreasureBoxSpawn
        {
            Name = data.Id
        };

        ConfigureTreasureBoxNode(instance, data);
        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created treasure box: {data.Id}");
    }

    private void ConfigureTreasureBoxNode(Node node, TreasureBoxData data)
    {
        node.Set("TreasureBoxId", data.Id);
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("RewardGold", data.Gold);

        var itemIds = new Godot.Collections.Array<string>();
        var quantities = new Godot.Collections.Array<int>();
        foreach (var item in data.Items ?? new List<TreasureBoxItemData>())
        {
            itemIds.Add(item.ItemId);
            quantities.Add(item.Quantity);
        }

        node.Set("RewardItemIds", itemIds);
        node.Set("RewardItemQuantities", quantities);

        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
            node2d.ZIndex = 2;
        }
    }

    private static string GetTreasureBoxImportKey(TreasureBoxSpawn box)
    {
        return string.IsNullOrWhiteSpace(box.TreasureBoxId)
            ? box.Name.ToString()
            : box.TreasureBoxId;
    }

    private void ImportTrapTiles(List<TrapTileData> trapTiles, Node2D gridMapNode)
    {
        var existingTrapTiles = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is TrapTileSpawn trapTile)
            {
                existingTrapTiles[GetTrapTileImportKey(trapTile)] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var trapData in trapTiles)
        {
            if (!string.IsNullOrWhiteSpace(trapData.Id) && processedIds.Contains(trapData.Id))
            {
                continue;
            }

            MarkProcessedIfIdPresent(processedIds, trapData.Id);

            if (!HasValidPuzzleIdentity(trapData.Id, trapData.PuzzleId, "trap tile"))
            {
                continue;
            }

            if (existingTrapTiles.TryGetValue(trapData.Id, out var existingNode))
            {
                ConfigureTrapTileNode(existingNode, trapData);
                GD.Print($"[TilemapJsonImporter] Updated trap tile: {trapData.Id}");
            }
            else
            {
                CreateTrapTileNode(trapData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingTrapTiles)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing trap tile: {id}");
                node.Free();
            }
        }
    }

    private void CreateTrapTileNode(TrapTileData data, Node2D parent)
    {
        var instance = new TrapTileSpawn
        {
            Name = data.Id
        };

        ConfigureTrapTileNode(instance, data);
        parent.AddChild(instance);
        AssignOwner(instance, parent);

        GD.Print($"[TilemapJsonImporter] Created trap tile: {data.Id}");
    }

    private void ConfigureTrapTileNode(Node node, TrapTileData data)
    {
        node.Set("PuzzleId", data.PuzzleId);
        node.Set("TrapId", data.Id);
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("Damage", data.Damage);
        node.Set("StatusEffectId", data.StatusEffect ?? "");
        node.Set("StatusMagnitude", data.StatusMagnitude);
        node.Set("StatusTurns", data.StatusTurns);
        ConfigurePuzzleNodeTransform(node, data.Position);
    }

    private static string GetTrapTileImportKey(TrapTileSpawn trapTile)
    {
        return string.IsNullOrWhiteSpace(trapTile.TrapId)
            ? trapTile.Name.ToString()
            : trapTile.TrapId;
    }

    private void ImportPuzzleSwitches(List<PuzzleSwitchData> switches, Node2D gridMapNode)
    {
        var existingSwitches = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is PuzzleSwitchSpawn puzzleSwitch)
            {
                existingSwitches[GetPuzzleSwitchImportKey(puzzleSwitch)] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var switchData in switches)
        {
            if (!string.IsNullOrWhiteSpace(switchData.Id) && processedIds.Contains(switchData.Id))
            {
                continue;
            }

            MarkProcessedIfIdPresent(processedIds, switchData.Id);

            if (!HasValidPuzzleIdentity(switchData.Id, switchData.PuzzleId, "puzzle switch"))
            {
                continue;
            }

            if (existingSwitches.TryGetValue(switchData.Id, out var existingNode))
            {
                ConfigurePuzzleSwitchNode(existingNode, switchData);
                GD.Print($"[TilemapJsonImporter] Updated puzzle switch: {switchData.Id}");
            }
            else
            {
                CreatePuzzleSwitchNode(switchData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingSwitches)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing puzzle switch: {id}");
                node.Free();
            }
        }
    }

    private void CreatePuzzleSwitchNode(PuzzleSwitchData data, Node2D parent)
    {
        var instance = new PuzzleSwitchSpawn
        {
            Name = data.Id
        };

        ConfigurePuzzleSwitchNode(instance, data);
        parent.AddChild(instance);
        AssignOwner(instance, parent);

        GD.Print($"[TilemapJsonImporter] Created puzzle switch: {data.Id}");
    }

    private void ConfigurePuzzleSwitchNode(Node node, PuzzleSwitchData data)
    {
        node.Set("SwitchId", data.Id);
        node.Set("PuzzleId", data.PuzzleId);
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("PromptText", data.PromptText ?? "");
        node.Set("ActivatedText", data.ActivatedText ?? "");
        ConfigurePuzzleNodeTransform(node, data.Position);
    }

    private static string GetPuzzleSwitchImportKey(PuzzleSwitchSpawn puzzleSwitch)
    {
        return string.IsNullOrWhiteSpace(puzzleSwitch.SwitchId)
            ? puzzleSwitch.Name.ToString()
            : puzzleSwitch.SwitchId;
    }

    private void ImportPuzzleGates(List<PuzzleGateData> gates, Node2D gridMapNode)
    {
        var existingGates = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is PuzzleGateSpawn gate)
            {
                existingGates[GetPuzzleGateImportKey(gate)] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var gateData in gates)
        {
            if (!string.IsNullOrWhiteSpace(gateData.Id) && processedIds.Contains(gateData.Id))
            {
                continue;
            }

            MarkProcessedIfIdPresent(processedIds, gateData.Id);

            if (!HasValidPuzzleIdentity(gateData.Id, gateData.PuzzleId, "puzzle gate"))
            {
                continue;
            }

            if (existingGates.TryGetValue(gateData.Id, out var existingNode))
            {
                ConfigurePuzzleGateNode(existingNode, gateData);
                GD.Print($"[TilemapJsonImporter] Updated puzzle gate: {gateData.Id}");
            }
            else
            {
                CreatePuzzleGateNode(gateData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingGates)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing puzzle gate: {id}");
                node.Free();
            }
        }
    }

    private void CreatePuzzleGateNode(PuzzleGateData data, Node2D parent)
    {
        var instance = new PuzzleGateSpawn
        {
            Name = data.Id
        };

        ConfigurePuzzleGateNode(instance, data);
        parent.AddChild(instance);
        AssignOwner(instance, parent);

        GD.Print($"[TilemapJsonImporter] Created puzzle gate: {data.Id}");
    }

    private void ConfigurePuzzleGateNode(Node node, PuzzleGateData data)
    {
        node.Set("GateId", data.Id);
        node.Set("PuzzleId", data.PuzzleId);
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("StartsClosed", data.StartsClosed);

        if (node is PuzzleGateSpawn gate)
        {
            gate.ApplySolvedState(!data.StartsClosed);
        }

        ConfigurePuzzleNodeTransform(node, data.Position);
    }

    private static string GetPuzzleGateImportKey(PuzzleGateSpawn gate)
    {
        return string.IsNullOrWhiteSpace(gate.GateId)
            ? gate.Name.ToString()
            : gate.GateId;
    }

    private void ImportPuzzleRiddles(List<PuzzleRiddleData> riddles, Node2D gridMapNode)
    {
        var existingRiddles = new Dictionary<string, Node>();
        foreach (var child in gridMapNode.GetChildren())
        {
            if (child is PuzzleRiddleSpawn riddle)
            {
                existingRiddles[GetPuzzleRiddleImportKey(riddle)] = child;
            }
        }

        var processedIds = new HashSet<string>();

        foreach (var riddleData in riddles)
        {
            if (!string.IsNullOrWhiteSpace(riddleData.Id) && processedIds.Contains(riddleData.Id))
            {
                continue;
            }

            MarkProcessedIfIdPresent(processedIds, riddleData.Id);

            if (!HasValidPuzzleIdentity(riddleData.Id, riddleData.PuzzleId, "puzzle riddle"))
            {
                continue;
            }

            if (existingRiddles.TryGetValue(riddleData.Id, out var existingNode))
            {
                ConfigurePuzzleRiddleNode(existingNode, riddleData);
                GD.Print($"[TilemapJsonImporter] Updated puzzle riddle: {riddleData.Id}");
            }
            else
            {
                CreatePuzzleRiddleNode(riddleData, gridMapNode);
            }
        }

        foreach (var (id, node) in existingRiddles)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing puzzle riddle: {id}");
                node.Free();
            }
        }
    }

    private void CreatePuzzleRiddleNode(PuzzleRiddleData data, Node2D parent)
    {
        var instance = new PuzzleRiddleSpawn
        {
            Name = data.Id
        };

        ConfigurePuzzleRiddleNode(instance, data);
        parent.AddChild(instance);
        AssignOwner(instance, parent);

        GD.Print($"[TilemapJsonImporter] Created puzzle riddle: {data.Id}");
    }

    private void ConfigurePuzzleRiddleNode(Node node, PuzzleRiddleData data)
    {
        node.Set("RiddleId", data.Id);
        node.Set("PuzzleId", data.PuzzleId);
        node.Set("GridPosition", data.Position.ToVector2I());
        node.Set("PromptText", data.PromptText ?? "");
        node.Set("CorrectChoiceId", data.CorrectChoiceId ?? "");
        node.Set("WrongAnswerDamage", data.WrongAnswerDamage);

        var choiceIds = new Godot.Collections.Array<string>();
        var choiceLabels = new Godot.Collections.Array<string>();
        foreach (var choice in data.Choices ?? new List<PuzzleRiddleChoiceData>())
        {
            choiceIds.Add(choice.Id);
            choiceLabels.Add(choice.Label);
        }

        node.Set("ChoiceIds", choiceIds);
        node.Set("ChoiceLabels", choiceLabels);
        ConfigurePuzzleNodeTransform(node, data.Position);
    }

    private static string GetPuzzleRiddleImportKey(PuzzleRiddleSpawn riddle)
    {
        return string.IsNullOrWhiteSpace(riddle.RiddleId)
            ? riddle.Name.ToString()
            : riddle.RiddleId;
    }

    private static bool HasValidPuzzleIdentity(string id, string puzzleId, string entityType)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(puzzleId))
        {
            GD.PrintErr($"[TilemapJsonImporter] Skipping {entityType} with empty id or puzzle_id.");
            return false;
        }

        return true;
    }

    private static void MarkProcessedIfIdPresent(HashSet<string> processedIds, string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            processedIds.Add(id);
        }
    }

    private static void ConfigurePuzzleNodeTransform(Node node, Vector2IData position)
    {
        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(position);
            node2d.ZIndex = 2;
        }
    }

    private static void AssignOwner(Node node, Node parent)
    {
        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            node.Owner = sceneRoot;
        }
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
                CreateStairConnectionNode(stairData, gridMapNode);
            }
        }

        // Remove stairs that are no longer in JSON
        foreach (var (id, node) in existingStairs)
        {
            if (!processedIds.Contains(id))
            {
                GD.Print($"[TilemapJsonImporter] Removing stair connection: {id}");
                node.Free();
            }
        }
    }

    private void UpdateStairConnectionNode(Node node, StairConnectionData data)
    {
        ConfigureStairConnectionNode(node, data);
        GD.Print($"[TilemapJsonImporter] Updated stair connection: {data.Id}");
    }

    private void CreateStairConnectionNode(StairConnectionData data, Node2D parent)
    {
        var instance = new StairConnection
        {
            Name = data.Id
        };

        ConfigureStairConnectionNode(instance, data);
        parent.AddChild(instance);

        var sceneRoot = parent.Owner ?? parent.GetTree()?.EditedSceneRoot;
        if (sceneRoot != null)
        {
            instance.Owner = sceneRoot;
        }

        GD.Print($"[TilemapJsonImporter] Created stair connection: {data.Id}");
    }

    private void ConfigureStairConnectionNode(Node node, StairConnectionData data)
    {
        node.Set("StairId", data.Id);
        node.Set("GridPosition", data.Position.ToVector2I());

        int direction;
        var dir = data.Direction?.ToLower();
        if (dir == "down")
            direction = 1;
        else if (dir == "up")
            direction = 0;
        else
        {
            GD.PrintErr($"[TilemapJsonImporter] Invalid stair direction '{data.Direction}' for '{data.Id}', expected 'up' or 'down'");
            direction = 0;
        }
        node.Set("Direction", direction);
        node.Set("TargetFloor", data.TargetFloor);
        node.Set("DestinationStairId", data.DestinationStairId ?? "");

        if (node is Node2D node2d)
        {
            node2d.Position = ToCenteredCellPosition(data.Position);
        }
    }

    private static Vector2 ToCenteredCellPosition(Vector2IData position)
    {
        const int cellSize = 32;
        return new Vector2(
            position.X * cellSize + cellSize / 2f,
            position.Y * cellSize + cellSize / 2f
        );
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        return string.Concat(
            s.Split('_')
                .Where(part => !string.IsNullOrEmpty(part))
                .Select(part => char.ToUpper(part[0]) + part.Substring(1).ToLower())
        );
    }
}
