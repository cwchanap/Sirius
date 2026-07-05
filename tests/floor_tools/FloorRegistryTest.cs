using GdUnit4;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorRegistryTest
{
    [TestCase]
    public void TestGroundFloorPaths()
    {
        var p = FloorRegistry.Get(0);
        AssertThat(p.ScenePath).IsEqual("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(p.DefPath).IsEqual("res://resources/floors/FloorGF.tres");
        AssertThat(p.JsonPath).IsEqual("res://scenes/game/floors/FloorGF.json");
    }

    [TestCase]
    public void TestFloor1Paths()
    {
        var p = FloorRegistry.Get(1);
        AssertThat(p.ScenePath).IsEqual("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(p.DefPath).IsEqual("res://resources/floors/Floor1F.tres");
    }

    [TestCase]
    public void TestAllFloors()
    {
        AssertThat(FloorRegistry.AllFloors).IsEqual(new int[] { 0, 1, 2, 3 });
    }
}
