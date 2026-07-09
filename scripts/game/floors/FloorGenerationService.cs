using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using TileData = Sirius.TilemapJson.TileData;

namespace Sirius.FloorTools;

public static class FloorGenerationService
{
    public static FloorJsonModel Generate(int floorNumber) => floorNumber switch
    {
        0 => GenerateGroundFloor(),
        1 => GenerateFloor1(),
        2 => GenerateFloor2(),
        3 => GenerateFloor3(),
        _ => throw new System.ArgumentException($"Unknown floor: {floorNumber}"),
    };

    public static FloorJsonModel GenerateGroundFloor()
    {
        var builder = new MazeBuilder(Floor0Layout.FloorWidth, Floor0Layout.FloorHeight);
        BuildGroundFloorWalls(builder);
        var walls = new HashSet<Vector2I>(builder.Walls);
        walls.UnionWith(PerimeterWalls(Floor0Layout.FloorWidth, Floor0Layout.FloorHeight, Floor0Layout.GridWidth, Floor0Layout.GridHeight));

        var model = new FloorJsonModel
        {
            SchemaVersion = "1.0",
            Metadata = new FloorMetadata
            {
                FloorName = "Ground Floor",
                FloorNumber = 0,
                Description = "A readable starter district loop with optional branches.",
                PlayerStart = new Vector2IData(Floor0Layout.PlayerStart),
            },
        };

        // Ground = full 160x160 grid, tile "starting_area"
        var ground = new List<TileData>();
        for (int y = 0; y < Floor0Layout.GridHeight; y++)
            for (int x = 0; x < Floor0Layout.GridWidth; x++)
                ground.Add(new TileData(x, y, "starting_area"));
        model.TileLayers["ground"] = ground;

        // Walls sorted by (y, x), tile "generic"
        model.TileLayers["wall"] = walls
            .OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => new TileData(p.X, p.Y, "generic")).ToList();

        // Stair layer
        model.TileLayers["stair"] = new List<TileData>
        {
            new(Floor0Layout.StairPos.X, Floor0Layout.StairPos.Y, "up"),
        };

        // Entities — port the exact enemy/npc/stair/treasure lists from
        // floor0_maze_generator.py:211-255 verbatim.
        model.Entities = new SceneEntities
        {
            EnemySpawns = new()
            {
                new() { Id = "EnemySpawn_Goblin", Position = new Vector2IData(Floor0Layout.FirstGoblinPos), EnemyType = "Goblin" },
                new() { Id = "EnemySpawn_Goblin_North", Position = new Vector2IData(Floor0Layout.GoblinNorthPos), EnemyType = "Goblin" },
                new() { Id = "EnemySpawn_Orc_East", Position = new Vector2IData(Floor0Layout.OrcEastPos), EnemyType = "Orc" },
                new() { Id = "EnemySpawn_Goblin_South", Position = new Vector2IData(Floor0Layout.GoblinSouthPos), EnemyType = "Goblin" },
            },
            NpcSpawns = new()
            {
                new() { Id = "NpcSpawn_Shopkeeper", Position = new Vector2IData(Floor0Layout.ShopkeeperPos), NpcId = "village_shopkeeper" },
                new() { Id = "NpcSpawn_Healer", Position = new Vector2IData(Floor0Layout.HealerPos), NpcId = "village_healer" },
            },
            StairConnections = new()
            {
                new() { Id = "GF_000", Position = new Vector2IData(Floor0Layout.StairPos), Direction = "up", TargetFloor = 1, DestinationStairId = "1F_001" },
            },
            TreasureBoxes = FloorEntityBuilders.TreasureBoxes(
                Floor0Layout.TreasureBoxes.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Gold, kv.Value.Items))),
            HiddenPlaceholders = new(),
            TrapTiles = new(),
            PuzzleSwitches = new(),
            PuzzleGates = new(),
            PuzzleRiddles = new(),
        };

        return model;
    }

    // Port of perimeter_walls (floor0_maze_generator.py:169-180).
    public static HashSet<Vector2I> PerimeterWalls(int floorWidth, int floorHeight, int gridWidth, int gridHeight)
    {
        var walls = new HashSet<Vector2I>();
        for (int y = floorHeight; y < gridHeight; y++)
            for (int x = 0; x < gridWidth; x++)
                walls.Add(new Vector2I(x, y));
        for (int y = 0; y < floorHeight; y++)
            for (int x = floorWidth; x < gridWidth; x++)
                walls.Add(new Vector2I(x, y));
        return walls;
    }

    // Port of MazeBuilder.build() for GF — floor0_maze_generator.py:93-126.
    // Carves the loop, plazas, rooms, and dead-end branches exactly as Python.
    private static void BuildGroundFloorWalls(MazeBuilder builder)
    {
        builder.CarveLoop(Floor0Layout.MainLoopPoints, halfWidth: 2);

        builder.CarveRect(5, 42, 17, 58);
        builder.CarveRect(9, 43, 15, 48);
        builder.CarveRect(9, 52, 15, 57);
        builder.CarveRect(20, 41, 29, 48);
        builder.CarveRect(11, 11, 25, 24);
        builder.CarveRect(38, 10, 52, 24);
        builder.CarvePath(new(25, 18), new(38, 18), 1);
        builder.CarvePath(new(44, 24), new(44, 36), 1);
        builder.CarveRect(39, 34, 50, 41);
        builder.CarvePath(new(39, 38), new(20, 50), 1);
        builder.CarveRect(62, 24, 81, 36);
        builder.CarveRect(70, 42, 88, 55);
        builder.CarvePath(new(76, 36), new(79, 42), 1);
        builder.CarvePath(new(70, 49), new(56, 49), 1);
        builder.CarvePath(new(56, 49), new(56, 18), 1);
        builder.CarveRect(66, 63, 88, 74);
        builder.CarveRect(72, 76, 90, 88);
        builder.CarvePath(new(80, 74), new(80, 76), 1);
        builder.CarveRect(34, 74, 58, 90);
        builder.CarveRect(14, 65, 25, 79);
        builder.CarvePath(new(34, 82), new(25, 72), 1);
        builder.CarvePath(new(52, 74), new(52, 52), 1);
        builder.CarvePath(new(52, 52), new(70, 49), 1);

        // Dead-end branches — port verbatim from floor0_maze_generator.py:129-137
        foreach (var (start, end) in Floor0Layout.DeadEndBranches)
            builder.CarvePath(start, end, 1);

        builder.ReinforcePerimeter();
    }

    public static FloorJsonModel GenerateFloor3()
    {
        var builder = new MazeBuilder(Floor3Layout.Width, Floor3Layout.Height);
        BuildFloor3Walls(builder);

        var model = new FloorJsonModel { SchemaVersion = "1.0" };
        model.Metadata = new FloorMetadata
        {
            FloorName = "Third Floor",
            FloorNumber = 3,
            Description = "A safe future landing for the second-floor up stair.",
            PlayerStart = new Vector2IData(Floor3Layout.PlayerStart),
        };
        model.TileLayers["ground"] = GroundTiles(Floor3Layout.Width, Floor3Layout.Height);
        model.TileLayers["wall"] = WallTiles(builder.Walls);
        model.TileLayers["stair"] = new List<TileData>
        {
            new(Floor3Layout.DownStair.X, Floor3Layout.DownStair.Y, "down"),
        };
        model.Entities = new SceneEntities
        {
            EnemySpawns = new(),
            NpcSpawns = new(),
            StairConnections = new()
            {
                new() { Id = "3F_2F_A", Position = new Vector2IData(Floor3Layout.DownStair), Direction = "down", TargetFloor = 2, DestinationStairId = "2F_3F_A" },
            },
            HiddenPlaceholders = new(),
            TreasureBoxes = new(),
            TrapTiles = new(),
            PuzzleSwitches = new(),
            PuzzleGates = new(),
            PuzzleRiddles = new(),
        };
        return model;
    }

    private static void BuildFloor3Walls(MazeBuilder builder)
    {
        builder.CarveRect(6, 6, 16, 13);
        builder.CarveHCorridor(8, 14, Floor3Layout.DownStair.Y, 1);
        builder.CarveVCorridor(8, 12, Floor3Layout.DownStair.X, 1);
        builder.ReinforcePerimeter();
    }

    public static FloorJsonModel GenerateFloor1()
    {
        var builder = new MazeBuilder(Floor1Layout.Width, Floor1Layout.Height);
        BuildFloor1Walls(builder);
        var walls = builder.Walls;
        var walkable = FloorGraph.WalkableCellsFromWalls(walls, Floor1Layout.Width, Floor1Layout.Height);

        var baseEnemies = MergeDicts(Floor1Layout.EnemyGates, Floor1Layout.ExtraEnemyPatrols);
        var occupied = new HashSet<Vector2I>
        {
            Floor1Layout.PlayerStart, Floor1Layout.DownStair,
            Floor1Layout.UpStairA, Floor1Layout.UpStairB,
        };
        occupied.UnionWith(Floor1Layout.HiddenPlaceholders.Values);
        occupied.UnionWith(PositionSet(baseEnemies));
        occupied.UnionWith(Floor1Layout.TreasureBoxes.Values.Select(t => t.Position));
        occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleTraps));
        occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleSwitches));
        occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleGates));
        occupied.UnionWith(AuthoredPositions(Floor1Layout.PuzzleRiddles));

        var enemySpawns = MergeDicts(baseEnemies, SupplementalEnemyPlanner.Plan(
            Floor1Layout.SupplementalPrefix, baseEnemies, walkable, occupied,
            Floor1Layout.SupplementalTypes));

        var model = new FloorJsonModel { SchemaVersion = "1.0" };
        model.Metadata = new FloorMetadata
        {
            FloorName = "First Floor",
            FloorNumber = 1,
            Description = "A compact combat-gated loop maze with two 2/F routes.",
            PlayerStart = new Vector2IData(Floor1Layout.PlayerStart),
        };

        model.TileLayers["ground"] = GroundTiles(Floor1Layout.Width, Floor1Layout.Height);
        model.TileLayers["wall"] = WallTiles(walls);
        model.TileLayers["stair"] = new List<TileData>
        {
            new(Floor1Layout.DownStair.X, Floor1Layout.DownStair.Y, "down"),
            new(Floor1Layout.UpStairA.X, Floor1Layout.UpStairA.Y, "up"),
            new(Floor1Layout.UpStairB.X, Floor1Layout.UpStairB.Y, "up"),
        };

        model.Entities = new SceneEntities
        {
            EnemySpawns = enemySpawns.Select(kv => new EnemySpawnData
            {
                Id = kv.Key, Position = new Vector2IData(kv.Value.Position), EnemyType = kv.Value.EnemyType,
            }).ToList(),
            NpcSpawns = new(),
            StairConnections = new()
            {
                new() { Id = "1F_001", Position = new Vector2IData(Floor1Layout.DownStair), Direction = "down", TargetFloor = 0, DestinationStairId = "GF_000" },
                new() { Id = "1F_2F_A", Position = new Vector2IData(Floor1Layout.UpStairA), Direction = "up", TargetFloor = 2, DestinationStairId = "2F_1F_A" },
                new() { Id = "1F_2F_B", Position = new Vector2IData(Floor1Layout.UpStairB), Direction = "up", TargetFloor = 2, DestinationStairId = "2F_1F_B" },
            },
            HiddenPlaceholders = Floor1Layout.HiddenPlaceholders
                .Select(kv => new HiddenPlaceholderData { Id = kv.Key, Position = new Vector2IData(kv.Value) }).ToList(),
            TreasureBoxes = FloorEntityBuilders.TreasureBoxes(Floor1Layout.TreasureBoxes.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Gold, kv.Value.Items))),
            TrapTiles = FloorEntityBuilders.TrapTiles(Floor1Layout.PuzzleTraps.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Damage, kv.Value.StatusEffect, kv.Value.Magnitude, kv.Value.Turns)), Floor1Layout.PuzzleId),
            PuzzleSwitches = FloorEntityBuilders.Switches(Floor1Layout.PuzzleSwitches.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Activated)), Floor1Layout.PuzzleId),
            PuzzleGates = FloorEntityBuilders.Gates(Floor1Layout.PuzzleGates.Select(kv => (kv.Key, kv.Value.Position, kv.Value.StartsClosed)), Floor1Layout.PuzzleId),
            PuzzleRiddles = FloorEntityBuilders.Riddles(Floor1Layout.PuzzleRiddles.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Choices, kv.Value.CorrectChoiceId, kv.Value.WrongDamage)), Floor1Layout.PuzzleId),
        };

        return model;
    }

    // Shared helpers (also used by Floor 2/3):
    private static List<TileData> GroundTiles(int width, int height)
    {
        var tiles = new List<TileData>(width * height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                tiles.Add(new TileData(x, y, "starting_area"));
        return tiles;
    }

    private static List<TileData> WallTiles(HashSet<Vector2I> walls)
    {
        return walls.OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => new TileData(p.X, p.Y, "generic")).ToList();
    }

    private static HashSet<Vector2I> PositionSet(Dictionary<string, EnemySpec> enemies)
        => enemies.Values.Select(e => e.Position).ToHashSet();
    private static HashSet<Vector2I> AuthoredPositions<T>(Dictionary<string, T> entities) where T : IHasPosition
        => entities.Values.Select(e => e.Position).ToHashSet();
    private static Dictionary<string, EnemySpec> MergeDicts(params Dictionary<string, EnemySpec>[] dicts)
    {
        var merged = new Dictionary<string, EnemySpec>();
        foreach (var d in dicts) foreach (var kv in d) merged[kv.Key] = kv.Value;
        return merged;
    }

    private static void AddGateBarrier(HashSet<Vector2I> walls, Vector2I gate, IEnumerable<Vector2I> blocked)
    {
        foreach (var cell in blocked)
            if (cell != gate)
                walls.Add(cell);
    }

    private static void BuildFloor1Walls(MazeBuilder builder)
    {
        builder.CarveLoop(Floor1Layout.MainLoop, halfWidth: 1);

        builder.CarveRect(5, 27, 11, 33);
        builder.CarveRect(24, 26, 34, 34);
        builder.CarvePath(new(16, 30), new(28, 30), 1);

        builder.CarveRect(46, 9, 53, 15);
        builder.CarveRect(44, 45, 52, 52);

        builder.CarveRect(11, 22, 18, 27);
        builder.CarvePath(new(16, 22), new(14, 25), 1);

        builder.CarvePath(new(16, 16), Floor1Layout.HiddenPlaceholders["hidden_room_north"], 1);
        builder.CarvePath(new(53, 30), Floor1Layout.HiddenPlaceholders["hidden_shortcut_east"], 1);
        builder.CarvePath(new(28, 50), Floor1Layout.SouthShortcutEntry, 1);

        builder.CarveRect(13, 6, 19, 10);
        builder.CarveRect(53, 28, 58, 32);
        builder.CarveRect(16, 52, 22, 56);

        foreach (var (start, end) in Floor1Layout.DeadEndBranches)
            builder.CarvePath(start, end, 0);

        foreach (var (direction, start, end, fixedCoord) in Floor1Layout.DecisionConnectors)
        {
            if (direction == 'h')
                builder.CarveHCorridor(start, end, fixedCoord, 0);
            else
                builder.CarveVCorridor(start, end, fixedCoord, 0);
        }

        foreach (var branch in Floor1Layout.ShortcutBranches)
            for (int i = 0; i < branch.Length - 1; i++)
                builder.CarvePath(branch[i], branch[i + 1], 0);

        foreach (var (start, end) in Floor1Layout.WallReliefPaths)
            builder.CarvePath(start, end, 0);

        foreach (var w in Floor1Layout.ExtraWalls)
            builder.Walls.Add(w);

        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates[Floor1Layout.GateKeys.GoblinBranch].Position,
            Enumerable.Range(11, 8).Select(x => new Vector2I(x, 23)));
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates[Floor1Layout.GateKeys.OrcCentral].Position,
            new[] { new Vector2I(22, 29), new Vector2I(22, 30), new Vector2I(22, 31) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates[Floor1Layout.GateKeys.SkeletonStairA].Position,
            new[] { new Vector2I(43, 11), new Vector2I(43, 12), new Vector2I(43, 13) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates[Floor1Layout.GateKeys.ForestSpiritStairB].Position,
            new[] { new Vector2I(42, 47), new Vector2I(42, 48), new Vector2I(42, 49) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates[Floor1Layout.GateKeys.OrcHiddenBranch].Position,
            Enumerable.Range(16, 7).Select(x => new Vector2I(x, 51)));

        builder.ReinforcePerimeter();
    }

    public static FloorJsonModel GenerateFloor2()
    {
        var builder = new MazeBuilder(Floor2Layout.Width, Floor2Layout.Height);
        BuildFloor2Walls(builder);
        var walls = builder.Walls;
        var walkable = FloorGraph.WalkableCellsFromWalls(walls, Floor2Layout.Width, Floor2Layout.Height);

        var baseEnemies = MergeDicts(Floor2Layout.EnemyGates, Floor2Layout.ExtraEnemyPatrols);
        var occupied = new HashSet<Vector2I>
        {
            Floor2Layout.PlayerStart,
            Floor2Layout.DownStairA, Floor2Layout.DownStairB, Floor2Layout.UpStair,
        };
        occupied.UnionWith(PositionSet(baseEnemies));
        occupied.UnionWith(Floor2Layout.TreasureBoxes.Values.Select(t => t.Position));
        occupied.UnionWith(AuthoredPositions(Floor2Layout.PuzzleTraps));
        occupied.UnionWith(AuthoredPositions(Floor2Layout.PuzzleSwitches));
        occupied.UnionWith(AuthoredPositions(Floor2Layout.PuzzleGates));
        occupied.UnionWith(AuthoredPositions(Floor2Layout.PuzzleRiddles));

        var enemySpawns = MergeDicts(baseEnemies, SupplementalEnemyPlanner.Plan(
            Floor2Layout.SupplementalPrefix, baseEnemies, walkable, occupied,
            Floor2Layout.SupplementalTypes));

        var model = new FloorJsonModel { SchemaVersion = "1.0" };
        model.Metadata = new FloorMetadata
        {
            FloorName = "Second Floor",
            FloorNumber = 2,
            Description = "A moderate archive maze with two 1/F return stairs, one 3/F stair, treasure, and a puzzle-gated side chamber.",
            PlayerStart = new Vector2IData(Floor2Layout.PlayerStart),
        };

        model.TileLayers["ground"] = GroundTiles(Floor2Layout.Width, Floor2Layout.Height);
        model.TileLayers["wall"] = WallTiles(walls);
        model.TileLayers["stair"] = new List<TileData>
        {
            new(Floor2Layout.DownStairA.X, Floor2Layout.DownStairA.Y, "down"),
            new(Floor2Layout.DownStairB.X, Floor2Layout.DownStairB.Y, "down"),
            new(Floor2Layout.UpStair.X, Floor2Layout.UpStair.Y, "up"),
        };

        model.Entities = new SceneEntities
        {
            EnemySpawns = enemySpawns.Select(kv => new EnemySpawnData
            {
                Id = kv.Key, Position = new Vector2IData(kv.Value.Position), EnemyType = kv.Value.EnemyType,
            }).ToList(),
            NpcSpawns = new(),
            StairConnections = new()
            {
                new() { Id = "2F_1F_A", Position = new Vector2IData(Floor2Layout.DownStairA), Direction = "down", TargetFloor = 1, DestinationStairId = "1F_2F_A" },
                new() { Id = "2F_1F_B", Position = new Vector2IData(Floor2Layout.DownStairB), Direction = "down", TargetFloor = 1, DestinationStairId = "1F_2F_B" },
                new() { Id = "2F_3F_A", Position = new Vector2IData(Floor2Layout.UpStair), Direction = "up", TargetFloor = 3, DestinationStairId = "3F_2F_A" },
            },
            HiddenPlaceholders = new(),
            TreasureBoxes = FloorEntityBuilders.TreasureBoxes(Floor2Layout.TreasureBoxes.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Gold, kv.Value.Items))),
            TrapTiles = FloorEntityBuilders.TrapTiles(Floor2Layout.PuzzleTraps.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Damage, kv.Value.StatusEffect, kv.Value.Magnitude, kv.Value.Turns)), Floor2Layout.PuzzleId),
            PuzzleSwitches = FloorEntityBuilders.Switches(Floor2Layout.PuzzleSwitches.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Activated)), Floor2Layout.PuzzleId),
            PuzzleGates = FloorEntityBuilders.Gates(Floor2Layout.PuzzleGates.Select(kv => (kv.Key, kv.Value.Position, kv.Value.StartsClosed)), Floor2Layout.PuzzleId),
            PuzzleRiddles = FloorEntityBuilders.Riddles(Floor2Layout.PuzzleRiddles.Select(kv => (kv.Key, kv.Value.Position, kv.Value.Prompt, kv.Value.Choices, kv.Value.CorrectChoiceId, kv.Value.WrongDamage)), Floor2Layout.PuzzleId),
        };

        return model;
    }

    private static void BuildFloor2Walls(MazeBuilder builder)
    {
        builder.CarveLoop(Floor2Layout.MainLoop, halfWidth: 1);

        builder.CarveHCorridor(Floor2Layout.DownStairA.X, Floor2Layout.DownStairB.X, Floor2Layout.DownStairA.Y, 1);
        builder.CarvePath(Floor2Layout.DownStairB, new(34, 14), 1);

        builder.CarveRect(7, 7, 13, 13);
        builder.CarveRect(23, 7, 29, 13);
        builder.CarveRect(3, 14, 9, 18);
        builder.CarveRect(26, 27, 37, 36);
        builder.CarveRect(41, 7, 49, 15);
        builder.CarveRect(50, 29, 57, 37);
        builder.CarveRect(38, 49, 55, 56);

        builder.CarvePath(new(10, 13), new(6, 16), 0);
        builder.CarvePath(new(34, 14), new(44, 8), 0);
        builder.CarvePath(new(34, 14), new(36, 31), 1);
        builder.CarvePath(new(36, 31), new(29, 34), 1);
        builder.CarvePath(new(52, 34), new(56, 36), 0);
        builder.CarvePath(new(38, 52), new(42, 55), 0);
        builder.CarvePath(Floor2Layout.UpStair, new(53, 48), 0);

        builder.CarveRect(27, 34, 32, 40);
        builder.CarveRect(34, 37, 36, 39);
        builder.CarveCell(Floor2Layout.PuzzleGates[Floor2Layout.GateKeys.ArchiveTrialVault].Position.X, Floor2Layout.PuzzleGates[Floor2Layout.GateKeys.ArchiveTrialVault].Position.Y);
        builder.CarvePath(new(36, 38), new(38, 44), 0);
        builder.CarvePath(new(38, 44), new(42, 52), 0);

        foreach (var (start, end) in Floor2Layout.SideBranches)
            builder.CarvePath(start, end, 0);

        foreach (var (direction, start, end, fixedCoord) in Floor2Layout.DecisionConnectors)
        {
            if (direction == 'h')
                builder.CarveHCorridor(start, end, fixedCoord, 0);
            else
                builder.CarveVCorridor(start, end, fixedCoord, 0);
        }

        foreach (var (start, end) in Floor2Layout.WallReliefPaths)
            builder.CarvePath(start, end, 0);

        foreach (var (start, end) in Floor2Layout.ShortcutLoopCuts)
            builder.CarvePath(start, end, 0);

        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates[Floor2Layout.GateKeys.ArchiveGate].Position,
            from x in System.Linq.Enumerable.Range(30, 8) from y in new[] { 13, 15 } select new Vector2I(x, y));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols[Floor2Layout.GateKeys.WestLoop].Position,
            from x in System.Linq.Enumerable.Range(23, 9) from y in new[] { 17, 19 } select new Vector2I(x, y));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates[Floor2Layout.GateKeys.GalleryGate].Position,
            System.Linq.Enumerable.Range(30, 8).Select(y => new Vector2I(52, y)));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates[Floor2Layout.GateKeys.UpStairGuard].Position,
            System.Linq.Enumerable.Range(47, 5).Select(x => new Vector2I(x, 50)));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols[Floor2Layout.GateKeys.SouthApproach].Position,
            System.Linq.Enumerable.Range(24, 5).Select(x => new Vector2I(x, 45))
                .Concat(System.Linq.Enumerable.Range(23, 6).Select(x => new Vector2I(x, 47))));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols[Floor2Layout.GateKeys.SouthArmory].Position,
            (from x in System.Linq.Enumerable.Range(37, 5) from y in new[] { 51, 52 } select new Vector2I(x, y))
                .Concat(new[] { new Vector2I(37, 50), new Vector2I(41, 54) }));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates[Floor2Layout.GateKeys.PuzzleApproach].Position,
            System.Linq.Enumerable.Range(28, 5).Select(x => new Vector2I(x, 34)));
        AddGateBarrier(builder.Walls, Floor2Layout.PuzzleGates[Floor2Layout.GateKeys.ArchiveTrialVault].Position,
            System.Linq.Enumerable.Range(35, 6).Select(y => new Vector2I(33, y)));

        foreach (var w in Floor2Layout.VaultWalls)
            builder.Walls.Add(w);

        builder.ReinforcePerimeter();
    }
}
