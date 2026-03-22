using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class NpcSpawnTest : Node
{
    [TestCase]
    public void BelongsToFloor_ReturnsTrue_ForSameFloorHierarchy()
    {
        var floorRoot = new Node2D();
        var gridMap = new GridMap();
        var spawn = new NpcSpawn();

        floorRoot.AddChild(gridMap);
        gridMap.AddChild(spawn);

        AssertThat(spawn.BelongsToFloor(floorRoot)).IsTrue();

        floorRoot.Free();
    }

    [TestCase]
    public void BelongsToFloor_ReturnsFalse_ForDifferentFloorHierarchy()
    {
        var activeFloor = new Node2D();
        var otherFloor = new Node2D();
        var spawn = new NpcSpawn();

        activeFloor.AddChild(new GridMap());
        otherFloor.AddChild(spawn);

        AssertThat(spawn.BelongsToFloor(activeFloor)).IsFalse();

        activeFloor.Free();
        otherFloor.Free();
    }

    [TestCase]
    public async Task Ready_DisablesProcessing_AtRuntime()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floorRoot = new Node2D();
        var gridMap = new GridMap();
        var spawn = new NpcSpawn();

        floorRoot.AddChild(gridMap);
        gridMap.AddChild(spawn);
        sceneTree.Root.AddChild(floorRoot);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        AssertThat(spawn.IsProcessing()).IsFalse();

        floorRoot.QueueFree();
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
