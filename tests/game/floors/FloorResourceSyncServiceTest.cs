using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.Collections.Generic;
using static GdUnit4.Assertions;
using TileData = Sirius.TilemapJson.TileData;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorResourceSyncServiceTest
{
    [TestCase]
    public void TestAppliesPlayerStartAndStairs()
    {
        var def = new FloorDefinition();
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(8, 30) },
            Entities = new()
            {
                StairConnections = new()
                {
                    new() { Position = new Vector2IData(8, 30), Direction = "down" },
                    new() { Position = new Vector2IData(49, 12), Direction = "up" },
                },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        AssertThat(def.PlayerStartPosition).IsEqual(new Vector2I(8, 30));
        AssertThat(def.StairsDown.Count).IsEqual(1);
        AssertThat(def.StairsDown[0]).IsEqual(new Vector2I(8, 30));
        AssertThat(def.StairsUp.Count).IsEqual(1);
        AssertThat(def.StairsUp[0]).IsEqual(new Vector2I(49, 12));
    }

    [TestCase]
    public void TestGfPreservesExistingDestinationsWhenNoOverride()
    {
        // Seed a destination DISTINCT from the Floor0Layout.ReturnSpawnFromFloor1
        // fallback (17,13) so this test actually exercises the preserve branch —
        // if the branch were deleted (always-fallback), the assertion would fail.
        var def = new FloorDefinition();
        def.StairsUpDestinations.Add(new Vector2I(20, 20));
        var model = new FloorJsonModel
        {
            Metadata = new() { PlayerStart = new Vector2IData(8, 50) },
            Entities = new()
            {
                StairConnections = new() { new() { Position = new Vector2IData(82, 68), Direction = "up" } },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        // GF keeps the existing up-destination when no override is supplied (parity with Python default).
        AssertThat(def.StairsUpDestinations[0]).IsEqual(new Vector2I(20, 20));
    }

    [TestCase]
    public void TestGfStairDestOverrideReplacesDestinations()
    {
        // The --stair-dest CLI flag flows through FloorSyncOptions.StairDestOverride;
        // when set, GF StairsUpDestinations is replaced with the single override value.
        var def = new FloorDefinition();
        def.StairsUpDestinations.Add(new Vector2I(17, 13)); // existing — should be replaced
        var model = new FloorJsonModel
        {
            Metadata = new() { FloorNumber = 0, PlayerStart = new Vector2IData(8, 50) },
            Entities = new()
            {
                StairConnections = new() { new() { Position = new Vector2IData(82, 68), Direction = "up" } },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions(new Vector2I(5, 80)));

        AssertThat(def.StairsUpDestinations.Count).IsEqual(1);
        AssertThat(def.StairsUpDestinations[0]).IsEqual(new Vector2I(5, 80));
    }

    [TestCase]
    public void TestGfResetsStaleStairsDownDestinations()
    {
        // GF has no down stairs today. A .tres that previously held down-stair
        // destinations (e.g. from an experiment) must have them cleared on sync
        // so stale entries do not survive a regeneration.
        var def = new FloorDefinition();
        def.StairsDownDestinations.Add(new Vector2I(99, 99));
        var model = new FloorJsonModel
        {
            Metadata = new() { FloorNumber = 0, PlayerStart = new Vector2IData(8, 50) },
            Entities = new()
            {
                StairConnections = new() { new() { Position = new Vector2IData(82, 68), Direction = "up" } },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        AssertThat(def.StairsDownDestinations.Count).IsEqual(0);
    }

    [TestCase]
    public void TestSyncMetadataDefaultFalsePreservesHandAuthoredValues()
    {
        // Default (SyncMetadata=false): FloorName/FloorDescription on the .tres
        // are preserved — the generator must not clobber hand-authored labels.
        var def = new FloorDefinition
        {
            FloorName = "Hand-Authored Name",
            FloorDescription = "Hand-authored description",
        };
        var model = new FloorJsonModel
        {
            Metadata = new()
            {
                FloorName = "Generator Name",
                FloorNumber = 2,
                Description = "Generator description",
                PlayerStart = new Vector2IData(11, 10),
            },
            Entities = new() { StairConnections = new() },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        AssertThat(def.FloorName).IsEqual("Hand-Authored Name");
        AssertThat(def.FloorDescription).IsEqual("Hand-authored description");
        // FloorNumber is structural and always synced.
        AssertThat(def.FloorNumber).IsEqual(2);
    }

    [TestCase]
    public void TestSyncMetadataTrueOverwritesFromGenerator()
    {
        // Opt-in (SyncMetadata=true): generator labels overwrite the .tres.
        var def = new FloorDefinition
        {
            FloorName = "Hand-Authored Name",
            FloorDescription = "Hand-authored description",
        };
        var model = new FloorJsonModel
        {
            Metadata = new()
            {
                FloorName = "Generator Name",
                FloorNumber = 2,
                Description = "Generator description",
                PlayerStart = new Vector2IData(11, 10),
            },
            Entities = new() { StairConnections = new() },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions(SyncMetadata: true));

        AssertThat(def.FloorName).IsEqual("Generator Name");
        AssertThat(def.FloorDescription).IsEqual("Generator description");
        AssertThat(def.FloorNumber).IsEqual(2);
    }

    [TestCase]
    public void TestNonGfDestinationsAreOffStair()
    {
        // Floors 1/2/3: StairsDownDestinations/StairsUpDestinations must NOT
        // equal the stair cells themselves. Spawning on a stair would bounce
        // the player back on the next move onto that cell (see the +1x
        // PlayerStart shift and PlayerStartOnStair validator).
        //
        // Layout: down-stair at (8,30) with walkable (9,30) to its right
        // (+1x preference → matches PlayerStart convention); up-stair at
        // (49,12) with walkable (50,12) to its right.
        var def = new FloorDefinition();
        var model = new FloorJsonModel
        {
            Metadata = new() { FloorNumber = 1, PlayerStart = new Vector2IData(9, 30) },
            TileLayers = new()
            {
                ["ground"] = new()
                {
                    new TileData(8, 30, "g"), new TileData(9, 30, "g"),
                    new TileData(49, 12, "g"), new TileData(50, 12, "g"),
                },
                ["wall"] = new(),
                ["stair"] = new()
                {
                    new TileData(8, 30, "s"), new TileData(49, 12, "s"),
                },
            },
            Entities = new()
            {
                StairConnections = new()
                {
                    new() { Position = new Vector2IData(8, 30), Direction = "down" },
                    new() { Position = new Vector2IData(49, 12), Direction = "up" },
                },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        // Down-stair destination: +1x = (9,30) = PlayerStart, NOT (8,30).
        AssertThat(def.StairsDownDestinations.Count).IsEqual(1);
        AssertThat(def.StairsDownDestinations[0]).IsEqual(new Vector2I(9, 30));
        AssertThat(def.StairsDownDestinations[0]).IsNotEqual(def.StairsDown[0]);

        // Up-stair destination: +1x = (50,12), NOT (49,12).
        AssertThat(def.StairsUpDestinations.Count).IsEqual(1);
        AssertThat(def.StairsUpDestinations[0]).IsEqual(new Vector2I(50, 12));
        AssertThat(def.StairsUpDestinations[0]).IsNotEqual(def.StairsUp[0]);
    }

    [TestCase]
    public void TestNonGfDestinationFallsBackToOtherDirectionWhenPlusXBlocked()
    {
        // When +1x is a wall, the search must try the other cardinal
        // directions rather than spawning on the stair.
        var def = new FloorDefinition();
        var model = new FloorJsonModel
        {
            Metadata = new() { FloorNumber = 1, PlayerStart = new Vector2IData(7, 30) },
            TileLayers = new()
            {
                ["ground"] = new()
                {
                    new TileData(8, 30, "g"), new TileData(7, 30, "g"), new TileData(8, 31, "g"),
                },
                ["wall"] = new() { new TileData(9, 30, "w") }, // +1x blocked
                ["stair"] = new() { new TileData(8, 30, "s") },
            },
            Entities = new()
            {
                StairConnections = new()
                {
                    new() { Position = new Vector2IData(8, 30), Direction = "down" },
                },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        // +1x (9,30) is a wall, -1x (7,30) is walkable → destination = (7,30).
        AssertThat(def.StairsDownDestinations[0]).IsEqual(new Vector2I(7, 30));
        AssertThat(def.StairsDownDestinations[0]).IsNotEqual(def.StairsDown[0]);
    }

    [TestCase]
    public void TestNonGfDestinationAvoidsAdjacentStairCell()
    {
        // Two adjacent stairs: the off-stair search must not pick a cell that
        // is itself a stair (would just move the bounce to a different stair).
        var def = new FloorDefinition();
        var model = new FloorJsonModel
        {
            Metadata = new() { FloorNumber = 2, PlayerStart = new Vector2IData(11, 10) },
            TileLayers = new()
            {
                ["ground"] = new()
                {
                    new TileData(10, 10, "g"), new TileData(11, 10, "g"),
                    new TileData(26, 10, "g"), new TileData(27, 10, "g"),
                },
                ["wall"] = new(),
                ["stair"] = new()
                {
                    new TileData(10, 10, "s"), new TileData(26, 10, "s"),
                },
            },
            Entities = new()
            {
                StairConnections = new()
                {
                    new() { Position = new Vector2IData(10, 10), Direction = "down" },
                    new() { Position = new Vector2IData(26, 10), Direction = "down" },
                },
            },
        };

        FloorResourceSyncService.Apply(def, model, new FloorSyncOptions());

        // First down-stair (10,10): +1x = (11,10), not a stair → (11,10).
        AssertThat(def.StairsDownDestinations[0]).IsEqual(new Vector2I(11, 10));
        // Second down-stair (26,10): +1x = (27,10), not a stair → (27,10).
        AssertThat(def.StairsDownDestinations[1]).IsEqual(new Vector2I(27, 10));
        // Neither destination is a stair cell.
        AssertThat(def.StairsDownDestinations[0]).IsNotEqual(def.StairsDown[0]);
        AssertThat(def.StairsDownDestinations[1]).IsNotEqual(def.StairsDown[1]);
    }
}
