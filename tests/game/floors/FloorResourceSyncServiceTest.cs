using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using static GdUnit4.Assertions;

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
}
