using GdUnit4;
using Godot;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorSceneWriterTest
{
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
