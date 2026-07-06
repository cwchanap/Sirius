using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

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
}
