using Godot;
using Sirius.FloorTools.Layouts;
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
                new() { Id = "EnemySpawn_Goblin_North", Position = new Vector2IData(44, 36), EnemyType = "Goblin" },
                new() { Id = "EnemySpawn_Orc_East", Position = new Vector2IData(74, 49), EnemyType = "Orc" },
                new() { Id = "EnemySpawn_Goblin_South", Position = new Vector2IData(45, 82), EnemyType = "Goblin" },
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
        var branches = new (Vector2I, Vector2I)[]
        {
            (new(30, 18), new(30, 8)),
            (new(49, 18), new(49, 8)),
            (new(76, 30), new(91, 30)),
            (new(82, 68), new(94, 68)),
            (new(52, 82), new(52, 94)),
            (new(18, 72), new(7, 72)),
            (new(18, 50), new(33, 50)),
        };
        foreach (var (start, end) in branches)
            builder.CarvePath(start, end, 1);

        builder.ReinforcePerimeter();
    }

    // Placeholders — implemented in Tasks 10-11.
    public static FloorJsonModel GenerateFloor3() => throw new System.NotImplementedException();

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
        model.TileLayers["wall"] = WallTiles(walls, Floor1Layout.Width, Floor1Layout.Height, includeOutsideFootprint: false);
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

    private static List<TileData> WallTiles(HashSet<Vector2I> walls, int width, int height, bool includeOutsideFootprint)
    {
        var all = new HashSet<Vector2I>(walls);
        if (includeOutsideFootprint)
            all.UnionWith(OutsideFootprintWalls(width, height));
        return all.OrderBy(p => p.Y).ThenBy(p => p.X)
            .Select(p => new TileData(p.X, p.Y, "generic")).ToList();
    }

    private static HashSet<Vector2I> OutsideFootprintWalls(int width, int height)
    {
        var walls = new HashSet<Vector2I>();
        for (int y = height; y < 160; y++)
            for (int x = 0; x < 160; x++)
                walls.Add(new Vector2I(x, y));
        for (int y = 0; y < height; y++)
            for (int x = width; x < 160; x++)
                walls.Add(new Vector2I(x, y));
        return walls;
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
        var mainLoop = new Vector2I[]
        {
            new(8, 30),
            new(16, 16),
            new(33, 12),
            new(49, 12),
            new(53, 30),
            new(48, 48),
            new(28, 50),
            new(12, 42),
            new(8, 30),
        };
        builder.CarveLoop(mainLoop, halfWidth: 1);

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

        var deadEndBranches = new (Vector2I, Vector2I)[]
        {
            (new(11, 22), new(5, 22)),
            (new(28, 26), new(28, 20)),
            (new(32, 34), new(38, 39)),
            (new(49, 9), new(49, 5)),
            (new(53, 35), new(47, 35)),
            (new(56, 30), new(56, 36)),
            (new(28, 50), new(35, 55)),
            (new(7, 42), new(2, 42)),
            (new(12, 49), new(5, 54)),
            (new(38, 12), new(38, 7)),
        };
        foreach (var (start, end) in deadEndBranches)
            builder.CarvePath(start, end, 0);

        var decisionConnectors = new (char Direction, int Start, int End, int Fixed)[]
        {
            ('h', 5, 14, 37),
            ('v', 31, 41, 12),
            ('h', 11, 15, 28),
            ('h', 19, 38, 8),
            ('h', 17, 33, 11),
            ('v', 8, 15, 28),
            ('h', 49, 56, 34),
            ('v', 31, 45, 52),
            ('h', 49, 53, 32),
            ('v', 31, 35, 50),
        };
        foreach (var (direction, start, end, fixedCoord) in decisionConnectors)
        {
            if (direction == 'h')
                builder.CarveHCorridor(start, end, fixedCoord, 0);
            else
                builder.CarveVCorridor(start, end, fixedCoord, 0);
        }

        var shortcutBranches = new Vector2I[][]
        {
            new Vector2I[]
            {
                Floor1Layout.HiddenPlaceholders["hidden_room_north"],
                new(8, 8),
                new(8, 4),
                new(36, 4),
                new(36, 8),
                new(38, 8),
            },
            new Vector2I[]
            {
                Floor1Layout.HiddenPlaceholders["hidden_shortcut_east"],
                new(58, 46),
                new(56, 46),
                new(56, 48),
                new(58, 48),
                new(58, 50),
                new(56, 50),
                new(56, 52),
                new(58, 52),
                new(58, 54),
                new(56, 54),
                new(56, 56),
                new(58, 56),
                new(58, 58),
                new(54, 58),
                new(54, 46),
                new(58, 46),
            },
            new Vector2I[]
            {
                Floor1Layout.SouthShortcutEntry,
                new(23, 58),
                new(23, 56),
                new(58, 56),
                new(58, 58),
                new(42, 58),
                new(23, 58),
            },
        };
        foreach (var branch in shortcutBranches)
            for (int i = 0; i < branch.Length - 1; i++)
                builder.CarvePath(branch[i], branch[i + 1], 0);

        var wallReliefPaths = new (Vector2I, Vector2I)[]
        {
            (new(5, 22), new(4, 22)),
            (new(30, 17), new(30, 19)),
            (new(34, 26), new(34, 22)),
            (new(52, 24), new(44, 24)),
            (new(39, 34), new(43, 34)),
            (new(48, 35), new(44, 35)),
            (new(12, 40), new(28, 40)),
            (new(12, 41), new(28, 41)),
            (new(13, 42), new(28, 42)),
            (new(13, 43), new(28, 43)),
            (new(13, 44), new(28, 44)),
            (new(47, 42), new(39, 42)),
            (new(47, 43), new(39, 43)),
            (new(47, 44), new(39, 44)),
            (new(47, 45), new(39, 45)),
            (new(13, 46), new(28, 46)),
            (new(38, 56), new(38, 55)),
        };
        foreach (var (start, end) in wallReliefPaths)
            builder.CarvePath(start, end, 0);

        for (int x = 48; x < 55; x++)
            builder.Walls.Add(new Vector2I(x, 16));
        builder.Walls.Add(new Vector2I(19, 8));
        builder.Walls.Add(new Vector2I(35, 55));
        builder.Walls.Add(new Vector2I(25, 56));

        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates["EnemySpawn_Goblin_Branch"].Position,
            Enumerable.Range(11, 8).Select(x => new Vector2I(x, 23)));
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates["EnemySpawn_Orc_Central"].Position,
            new[] { new Vector2I(22, 29), new Vector2I(22, 30), new Vector2I(22, 31) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates["EnemySpawn_Skeleton_StairA"].Position,
            new[] { new Vector2I(43, 11), new Vector2I(43, 12), new Vector2I(43, 13) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates["EnemySpawn_ForestSpirit_StairB"].Position,
            new[] { new Vector2I(42, 47), new Vector2I(42, 48), new Vector2I(42, 49) });
        AddGateBarrier(builder.Walls, Floor1Layout.EnemyGates["EnemySpawn_Orc_HiddenBranch"].Position,
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
        model.TileLayers["wall"] = WallTiles(walls, Floor2Layout.Width, Floor2Layout.Height, includeOutsideFootprint: false);
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
        var mainLoop = new Vector2I[]
        {
            Floor2Layout.DownStairA,
            new(18, 14),
            new(34, 14),
            new(48, 20),
            new(52, 34),
            Floor2Layout.UpStair,
            new(38, 52),
            new(24, 44),
            new(16, 32),
            Floor2Layout.DownStairA,
        };
        builder.CarveLoop(mainLoop, halfWidth: 1);

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
        builder.CarveCell(Floor2Layout.PuzzleGates["PuzzleGate_2F_ArchiveTrial_Vault"].Position.X, Floor2Layout.PuzzleGates["PuzzleGate_2F_ArchiveTrial_Vault"].Position.Y);
        builder.CarvePath(new(36, 38), new(38, 44), 0);
        builder.CarvePath(new(38, 44), new(42, 52), 0);

        var sideBranches = new (Vector2I, Vector2I)[]
        {
            (new(18, 14), new(18, 6)),
            (new(24, 44), new(16, 52)),
            (new(52, 34), new(56, 28)),
            (new(42, 52), new(34, 56)),
            (new(16, 32), new(7, 32)),
            (new(26, 10), new(26, 5)),
            (new(44, 12), new(50, 18)),
        };
        foreach (var (start, end) in sideBranches)
            builder.CarvePath(start, end, 0);

        var decisionConnectors = new (char Direction, int Start, int End, int Fixed)[]
        {
            ('h', 12, 22, 18),
            ('v', 14, 28, 18),
            ('h', 18, 34, 18),
            ('h', 30, 44, 24),
            ('v', 24, 34, 44),
            ('h', 40, 52, 40),
            ('v', 36, 46, 50),
            ('h', 30, 42, 52),
            ('v', 38, 52, 24),
            ('h', 24, 36, 44),
        };
        foreach (var (direction, start, end, fixedCoord) in decisionConnectors)
        {
            if (direction == 'h')
                builder.CarveHCorridor(start, end, fixedCoord, 0);
            else
                builder.CarveVCorridor(start, end, fixedCoord, 0);
        }

        var wallReliefPaths = new (Vector2I, Vector2I)[]
        {
            (new(6, 16), new(3, 16)),
            (new(18, 6), new(18, 4)),
            (new(44, 8), new(47, 8)),
            (new(44, 24), new(48, 24)),
            (new(56, 28), new(56, 24)),
            (new(7, 32), new(4, 32)),
            (new(16, 52), new(13, 55)),
            (new(34, 56), new(30, 56)),
            (new(50, 46), new(55, 46)),
        };
        foreach (var (start, end) in wallReliefPaths)
            builder.CarvePath(start, end, 0);

        var shortcutLoopCuts = new (Vector2I, Vector2I)[]
        {
            (new(13, 55), new(30, 56)),
            (new(18, 28), new(24, 38)),
            (new(35, 44), new(41, 44)),
            (new(44, 34), new(48, 24)),
            (new(26, 5), new(18, 4)),
        };
        foreach (var (start, end) in shortcutLoopCuts)
            builder.CarvePath(start, end, 0);

        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates["EnemySpawn_2F_ArchiveGate"].Position,
            from x in System.Linq.Enumerable.Range(30, 8) from y in new[] { 13, 15 } select new Vector2I(x, y));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols["EnemySpawn_2F_WestLoop"].Position,
            from x in System.Linq.Enumerable.Range(23, 9) from y in new[] { 17, 19 } select new Vector2I(x, y));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates["EnemySpawn_2F_GalleryGate"].Position,
            System.Linq.Enumerable.Range(30, 8).Select(y => new Vector2I(52, y)));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates["EnemySpawn_2F_UpStairGuard"].Position,
            System.Linq.Enumerable.Range(47, 5).Select(x => new Vector2I(x, 50)));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols["EnemySpawn_2F_SouthApproach"].Position,
            System.Linq.Enumerable.Range(24, 5).Select(x => new Vector2I(x, 45))
                .Concat(System.Linq.Enumerable.Range(23, 6).Select(x => new Vector2I(x, 47))));
        AddGateBarrier(builder.Walls, Floor2Layout.ExtraEnemyPatrols["EnemySpawn_2F_SouthArmory"].Position,
            (from x in System.Linq.Enumerable.Range(37, 5) from y in new[] { 51, 52 } select new Vector2I(x, y))
                .Concat(new[] { new Vector2I(37, 50), new Vector2I(41, 54) }));
        AddGateBarrier(builder.Walls, Floor2Layout.EnemyGates["EnemySpawn_2F_PuzzleApproach"].Position,
            System.Linq.Enumerable.Range(28, 5).Select(x => new Vector2I(x, 34)));
        AddGateBarrier(builder.Walls, Floor2Layout.PuzzleGates["PuzzleGate_2F_ArchiveTrial_Vault"].Position,
            System.Linq.Enumerable.Range(35, 6).Select(y => new Vector2I(33, y)));

        for (int x = 33; x < 37; x++)
            builder.Walls.Add(new Vector2I(x, 33));
        for (int y = 34; y < 38; y++)
            builder.Walls.Add(new Vector2I(35, y));
        for (int x = 34; x < 37; x++)
            builder.Walls.Add(new Vector2I(x, 37));
        builder.Walls.Add(new Vector2I(33, 34));
        builder.Walls.Add(new Vector2I(34, 34));
        builder.Walls.Add(new Vector2I(34, 35));
        builder.Walls.Add(new Vector2I(34, 36));

        AddGateBarrier(builder.Walls, Floor2Layout.PuzzleGates["PuzzleGate_2F_ArchiveTrial_Shortcut"].Position,
            new[] { Floor2Layout.PuzzleGates["PuzzleGate_2F_ArchiveTrial_Shortcut"].Position });

        builder.ReinforcePerimeter();
    }
}
