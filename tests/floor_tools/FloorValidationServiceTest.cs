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
}
