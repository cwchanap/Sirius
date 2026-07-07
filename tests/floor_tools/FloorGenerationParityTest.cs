using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

// Post-cutover (890eb10) these tests compare C# output against the committed
// JSON baselines, which were themselves produced by the C# generators. They are
// therefore determinism/regression gates (catch unintended changes to generation
// logic), NOT Python-parity gates. The deprecated Python generators in tools/
// remain as frozen reference implementations for manual drift checks.
[TestSuite]
[RequireGodotRuntime]
public partial class FloorGenerationParityTest
{
    private static FloorJsonModel LoadCommitted(int floor)
    {
        string path = FloorRegistry.Get(floor).JsonPath;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        AssertThat(file).IsNotNull();
        return FloorJsonModel.FromJson(file.GetAsText());
    }

    [TestCase]
    public void TestGroundFloorParity()
    {
        var generated = FloorGenerationService.GenerateGroundFloor();
        var committed = LoadCommitted(0);
        FloorModelAsserter.AssertModelsEqual(generated, committed);
    }

    [TestCase]
    public void TestFloor1Parity()
    {
        var generated = FloorGenerationService.GenerateFloor1();
        var committed = LoadCommitted(1);
        FloorModelAsserter.AssertModelsEqual(generated, committed);
    }

    [TestCase]
    public void TestFloor2Parity()
    {
        var generated = FloorGenerationService.GenerateFloor2();
        var committed = LoadCommitted(2);
        FloorModelAsserter.AssertModelsEqual(generated, committed);
    }

    [TestCase]
    public void TestFloor3Parity()
    {
        var generated = FloorGenerationService.GenerateFloor3();
        var committed = LoadCommitted(3);
        FloorModelAsserter.AssertModelsEqual(generated, committed);
    }
}
