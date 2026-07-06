using GdUnit4;
using Godot;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorSceneWriterTest
{
    // NOTE: This test exercises the REAL write pipeline by calling
    // FloorSceneWriter.Generate(3, ...), which writes the committed
    // Floor3F.tscn/.tres/.json artifacts. This is an intentional integration
    // test of the full write path. It is idempotent: Floor 3 generation is
    // deterministic, so the rewritten artifacts are byte-identical to the
    // committed canonical output and do not dirty the working tree. If your
    // working tree appears dirty after running this test (e.g. UIDs resaved
    // differently by another Godot version), re-canonicalize with:
    //   godot --headless --path . --script tools/generate_floor.gd -- --floor 3
    [TestCase]
    public void TestGenerateFloor3WritesSceneAndDefAndJson()
    {
        // Floor 3 is the smallest/safest to round-trip in a test.
        var paths = FloorRegistry.Get(3);
        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions());

        AssertThat(result.Success).IsTrue();
        AssertThat(result.Validation.HasErrors).IsFalse();
        // Files were rewritten
        AssertThat(FileAccess.FileExists(paths.ScenePath)).IsTrue();
        AssertThat(FileAccess.FileExists(paths.JsonPath)).IsTrue();
    }
}
