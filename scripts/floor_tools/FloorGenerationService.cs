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

    // Placeholders — implemented in Tasks 9-11.
    public static FloorJsonModel GenerateFloor1() => throw new System.NotImplementedException();
    public static FloorJsonModel GenerateFloor2() => throw new System.NotImplementedException();
    public static FloorJsonModel GenerateFloor3() => throw new System.NotImplementedException();
}
