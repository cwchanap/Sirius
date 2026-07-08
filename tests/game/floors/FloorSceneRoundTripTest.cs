using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

// Locks the committed .tscn/.tres scenes to the C# generation logic. Each test
// loads a Floor*.tscn, exports it via TilemapJsonExporter, and compares the
// result against FloorGenerationService.Generate(floor). This catches hand-edits
// to scenes or drift between generation and committed artifacts that the
// .json-only FloorGenerationParityTest cannot detect.
//
// HiddenPlaceholders are JSON-only metadata with no scene representation (the
// importer/exporter do not round-trip them), so they are cleared on the
// generated model before comparison. Everything else — tiles, stairs, enemies,
// NPCs, treasures, traps, puzzle entities — is expected to round-trip exactly.
[TestSuite]
[RequireGodotRuntime]
public partial class FloorSceneRoundTripTest
{
    private static FloorJsonModel ExportCommittedScene(int floor)
    {
        var paths = FloorRegistry.Get(floor);
        var packed = GD.Load<PackedScene>(paths.ScenePath);
        AssertThat(packed).IsNotNull();
        var scene = packed.Instantiate();
        try
        {
            var gridMap = scene.GetNodeOrNull<Node2D>("GridMap");
            AssertThat(gridMap).IsNotNull();
            var floorDef = ResourceLoader.Load<FloorDefinition>(paths.DefPath,
                cacheMode: ResourceLoader.CacheMode.Ignore);
            var exporter = new TilemapJsonExporter();
            var model = exporter.ExportScene(gridMap, floorDef);
            AssertThat(model).IsNotNull();
            return model!;
        }
        finally
        {
            scene.Free();
        }
    }

    private static void AssertRoundTrip(int floor, FloorJsonModel generated)
    {
        // HiddenPlaceholders have no scene representation; clear before compare.
        generated.Entities.HiddenPlaceholders = new();
        var exported = ExportCommittedScene(floor);
        exported.Entities.HiddenPlaceholders ??= new();
        // Blueprint/Stats are scene-enriched fields (read from the spawn node's
        // Blueprint resource by the exporter) that the generator does not
        // produce — clear them so the comparison covers generation-layer data only.
        if (exported.Entities.EnemySpawns != null)
            foreach (var e in exported.Entities.EnemySpawns)
            {
                e.Blueprint = null;
                e.Stats = null;
            }
        // FloorName/Description are hand-authored presentation text on the .tres
        // that intentionally diverges from the generator's internal labels (see
        // FloorSyncOptions.SyncMetadata, default false). Clear them on both sides
        // so the round-trip asserts structural parity, not presentation text.
        exported.Metadata.FloorName = "";
        exported.Metadata.Description = "";
        generated.Metadata.FloorName = "";
        generated.Metadata.Description = "";
        FloorModelAsserter.AssertModelsEqual(exported, generated);
    }

    [TestCase]
    public void TestGroundFloorSceneRoundTrip()
        => AssertRoundTrip(0, FloorGenerationService.GenerateGroundFloor());

    [TestCase]
    public void TestFloor1SceneRoundTrip()
        => AssertRoundTrip(1, FloorGenerationService.GenerateFloor1());

    [TestCase]
    public void TestFloor2SceneRoundTrip()
        => AssertRoundTrip(2, FloorGenerationService.GenerateFloor2());

    [TestCase]
    public void TestFloor3SceneRoundTrip()
        => AssertRoundTrip(3, FloorGenerationService.GenerateFloor3());
}
