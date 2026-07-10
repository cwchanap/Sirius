using GdUnit4;
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

    [TestCase]
    public void TestFindByScenePathMatchesRegisteredFloor()
    {
        AssertThat(FloorRegistry.FindByScenePath("res://scenes/game/floors/FloorGF.tscn")).IsEqual(0);
        AssertThat(FloorRegistry.FindByScenePath("res://scenes/game/floors/Floor2F.tscn")).IsEqual(2);
    }

    [TestCase]
    public void TestFindByScenePathRejectsSameBasenameInDifferentDirectory()
    {
        // An unrelated copy of Floor2F.tscn opened from a scratch/backup dir must
        // NOT resolve to floor 2 — otherwise the dock would export the copy's grid
        // into the canonical scenes/game/floors/Floor2F.json.
        AssertThat(FloorRegistry.FindByScenePath("res://scratch/Floor2F.tscn")).IsEqual(-1);
        AssertThat(FloorRegistry.FindByScenePath("res://backup/Floor1F.tscn")).IsEqual(-1);
    }

    [TestCase]
    public void TestFindByScenePathNormalizesSeparators()
    {
        // Windows-style backslashes from Godot on Windows still match the
        // forward-slash registry paths.
        AssertThat(FloorRegistry.FindByScenePath("res://scenes\\game\\floors\\Floor1F.tscn")).IsEqual(1);
    }

    [TestCase]
    public void TestFindByScenePathReturnsMinusOneForNullOrEmpty()
    {
        AssertThat(FloorRegistry.FindByScenePath(null)).IsEqual(-1);
        AssertThat(FloorRegistry.FindByScenePath("")).IsEqual(-1);
        AssertThat(FloorRegistry.FindByScenePath("res://scenes/game/floors/Unknown.tscn")).IsEqual(-1);
    }
}
