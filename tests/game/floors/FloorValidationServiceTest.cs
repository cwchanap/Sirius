using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using TileData = Sirius.TilemapJson.TileData;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorValidationServiceTest
{
    private static FloorJsonModel ValidMinimalModel()
    {
        var walls = new HashSet<Vector2I> { new(2, 0), new(0, 2), new(2, 2) };
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(0, 0) },
        };
        model.TileLayers["ground"] = new() { new(0, 0, "starting_area"), new(1, 0, "starting_area"), new(1, 1, "starting_area") };
        model.TileLayers["wall"] = WallsList(walls);
        model.Entities = new SceneEntities
        {
            EnemySpawns = new(), NpcSpawns = new(), TreasureBoxes = new(),
            TrapTiles = new(), PuzzleSwitches = new(), PuzzleGates = new(),
            PuzzleRiddles = new(), StairConnections = new(), HiddenPlaceholders = new(),
        };
        return model;
    }

    private static List<TileData> WallsList(HashSet<Vector2I> walls) =>
        walls.Select(w => new TileData(w.X, w.Y, "generic")).ToList();

    [TestCase]
    public void TestValidModelHasNoErrors()
    {
        var result = FloorValidationService.Validate(ValidMinimalModel(), 3, 3);
        AssertThat(result.HasErrors).IsFalse();
    }

    [TestCase]
    public void TestDisconnectedCellsReported()
    {
        var model = ValidMinimalModel();
        model.TileLayers["ground"].Add(new TileData(5, 5, "starting_area")); // disconnected walkable
        var result = FloorValidationService.Validate(model, 6, 6);
        AssertThat(result.HasErrors).IsTrue();
        AssertThat(result.Issues.Any(i => i.Code == "DisconnectedCells")).IsTrue();
    }

    [TestCase]
    public void TestPlayerStartOnStairReported()
    {
        var model = ValidMinimalModel();
        model.TileLayers["stair"] = new() { new(0, 0, "down") };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.HasErrors).IsTrue();
        AssertThat(result.Issues.Any(i => i.Code == "PlayerStartOnStair")).IsTrue();
    }

    [TestCase]
    public void TestEntityOverlapReported()
    {
        var model = ValidMinimalModel();
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "e1", Position = new Vector2IData(1, 0), EnemyType = "goblin" },
            new() { Id = "e2", Position = new Vector2IData(1, 0), EnemyType = "orc" },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "EntityOverlap")).IsTrue();
    }

    [TestCase]
    public void TestInvalidTreasureRewardReported()
    {
        var model = ValidMinimalModel();
        model.Entities.TreasureBoxes = new()
        {
            new() { Id = "t1", Position = new Vector2IData(1, 0), Gold = 0,
                    Items = new() { new() { ItemId = "nonexistent_item", Quantity = 1 } } },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "InvalidTreasureReward")).IsTrue();
    }

    [TestCase]
    public void TestEmptyPuzzleIdReported()
    {
        var model = ValidMinimalModel();
        model.Entities.PuzzleSwitches = new()
        {
            new() { Id = "s1", PuzzleId = "", Position = new Vector2IData(1, 0) },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "InvalidPuzzleIdentity")).IsTrue();
    }

    [TestCase]
    public void TestGeneratedFloorsAreValid()
    {
        AssertThat(FloorValidationService.Validate(FloorGenerationService.GenerateGroundFloor(), 160, 160).HasErrors).IsFalse();
        AssertThat(FloorValidationService.Validate(FloorGenerationService.GenerateFloor1(), 60, 60).HasErrors).IsFalse();
        AssertThat(FloorValidationService.Validate(FloorGenerationService.GenerateFloor2(), 60, 60).HasErrors).IsFalse();
        AssertThat(FloorValidationService.Validate(FloorGenerationService.GenerateFloor3(), 24, 18).HasErrors).IsFalse();
    }

    // --- Defect-injection tests for the hardest validators (FloorValidationService.cs:109-159) ---
    // These exercise the most complex code paths most likely to silently regress.

    private static FloorJsonModel ModelWithGround(IEnumerable<(int X, int Y)> ground, int playerStartX, int playerStartY, int floorNumber = 0)
    {
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(playerStartX, playerStartY), FloorNumber = floorNumber },
        };
        model.TileLayers["ground"] = ground.Select(g => new TileData(g.X, g.Y, "starting_area")).ToList();
        model.TileLayers["wall"] = new();
        model.Entities = new SceneEntities
        {
            EnemySpawns = new(), NpcSpawns = new(), TreasureBoxes = new(),
            TrapTiles = new(), PuzzleSwitches = new(), PuzzleGates = new(),
            PuzzleRiddles = new(), StairConnections = new(), HiddenPlaceholders = new(),
        };
        return model;
    }

    [TestCase]
    public void TestEntityUnreachableReported()
    {
        // (5,5) is walkable ground but isolated from the start cluster — an entity
        // placed there is on walkable ground yet has no path from start.
        var model = ModelWithGround(new[] { (0, 0), (1, 0), (1, 1), (5, 5) }, 0, 0);
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "iso", Position = new Vector2IData(5, 5), EnemyType = "goblin" },
        };
        var result = FloorValidationService.Validate(model, 6, 6);
        AssertThat(result.Issues.Any(i => i.Code == "EntityUnreachable")).IsTrue();
    }

    [TestCase]
    public void TestClosedGateBlocksStartReported()
    {
        // A closed puzzle gate sitting on the player start cell makes the start
        // unreachable when the gate is treated as closed.
        var model = ModelWithGround(new[] { (0, 0), (1, 0) }, 0, 0);
        model.Entities.PuzzleGates = new()
        {
            new() { Id = "g1", PuzzleId = "p1", Position = new Vector2IData(0, 0), StartsClosed = true },
        };
        var result = FloorValidationService.Validate(model, 2, 2);
        AssertThat(result.Issues.Any(i => i.Code == "ClosedGateBlocksStart")).IsTrue();
    }

    [TestCase]
    public void TestClosedGateBlocksRouteReported()
    {
        // Corridor: start (0,0) -> (1,0) -> (2,0)[gate] -> (3,0)[stair]. With the
        // gate closed, the stair at (3,0) is unreachable from start.
        var model = ModelWithGround(new[] { (0, 0), (1, 0), (2, 0), (3, 0) }, 0, 0);
        model.Entities.PuzzleGates = new()
        {
            new() { Id = "g1", PuzzleId = "p1", Position = new Vector2IData(2, 0), StartsClosed = true },
        };
        model.Entities.StairConnections = new()
        {
            new() { Id = "s1", Position = new Vector2IData(3, 0), Direction = "up" },
        };
        var result = FloorValidationService.Validate(model, 4, 2);
        AssertThat(result.Issues.Any(i => i.Code == "ClosedGateBlocksRoute")).IsTrue();
    }

    [TestCase]
    public void TestUnrewardedDeadEndReportedAsWarning()
    {
        // A two-cell corridor with no entities: both ends are dead-end branches
        // with no payoff adjacent. FloorNumber=1 enables the dead-end validator.
        var model = ModelWithGround(new[] { (0, 0), (1, 0) }, 0, 0, floorNumber: 1);
        var result = FloorValidationService.Validate(model, 2, 1);
        var issue = result.Issues.FirstOrDefault(i => i.Code == "UnrewardedDeadEnd");
        AssertThat(issue).IsNotNull();
        AssertThat(issue!.Severity).IsEqual(Severity.Warning);
        AssertThat(result.HasErrors).IsFalse();
    }

    [TestCase]
    public void TestRewardedDeadEndNotReported()
    {
        // Same corridor but an enemy sits at the dead-end (1,0): the branch now
        // has a payoff adjacent, so UnrewardedDeadEnd must NOT fire.
        var model = ModelWithGround(new[] { (0, 0), (1, 0) }, 0, 0, floorNumber: 1);
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "e1", Position = new Vector2IData(1, 0), EnemyType = "goblin" },
        };
        var result = FloorValidationService.Validate(model, 2, 1);
        AssertThat(result.Issues.Any(i => i.Code == "UnrewardedDeadEnd")).IsFalse();
    }

    [TestCase]
    public void TestDuplicateEntityIdReported()
    {
        // Two enemy spawns with the same Id in different entity groups trigger
        // DuplicateEntityId (the validator tracks ids across all entity types).
        var model = ModelWithGround(new[] { (0, 0), (1, 0), (1, 1) }, 0, 0);
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "dup", Position = new Vector2IData(1, 0), EnemyType = "goblin" },
        };
        model.Entities.NpcSpawns = new()
        {
            new() { Id = "dup", Position = new Vector2IData(1, 1), NpcId = "villager" },
        };
        var result = FloorValidationService.Validate(model, 3, 3);
        AssertThat(result.Issues.Any(i => i.Code == "DuplicateEntityId")).IsTrue();
    }

    [TestCase]
    public void TestEmptyEntityIdReported()
    {
        // An enemy spawn with a blank Id triggers EmptyEntityId.
        var model = ModelWithGround(new[] { (0, 0), (1, 0) }, 0, 0);
        model.Entities.EnemySpawns = new()
        {
            new() { Id = "", Position = new Vector2IData(1, 0), EnemyType = "goblin" },
        };
        var result = FloorValidationService.Validate(model, 2, 2);
        AssertThat(result.Issues.Any(i => i.Code == "EmptyEntityId")).IsTrue();
    }

    [TestCase]
    public void TestNullEntitiesDoesNotThrow()
    {
        // A JSON-deserialized model may have Entities == null (no "entities" key).
        // Validate must coerce it to an empty SceneEntities rather than NRE.
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(0, 0) },
        };
        model.TileLayers["ground"] = new() { new(0, 0, "starting_area") };
        model.TileLayers["wall"] = new();
        model.Entities = null;
        var result = FloorValidationService.Validate(model, 1, 1);
        AssertThat(result.HasErrors).IsFalse();
    }
}
