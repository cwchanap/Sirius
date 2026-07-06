using GdUnit4;
using Godot;
using Sirius.FloorTools;
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
        var def = new FloorDefinition();
        def.StairsUpDestinations.Add(new Vector2I(17, 13));
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
        AssertThat(def.StairsUpDestinations[0]).IsEqual(new Vector2I(17, 13));
    }
}
