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

    [TestCase]
    public void FloorGF_ContainsReachableNpcSpawns_WithRegisteredNpcIds()
    {
        var floorScene = GD.Load<PackedScene>("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(floorScene).IsNotNull();

        var floorRoot = floorScene!.Instantiate<Node2D>();

        try
        {
            var npcCount = AssertFloorNpcIds(floorRoot, "village_shopkeeper", "village_healer");
            AssertThat(npcCount).IsEqual(2);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_ContainsNoNpcSpawns()
    {
        var floorScene = GD.Load<PackedScene>("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(floorScene).IsNotNull();

        var floorRoot = floorScene!.Instantiate<Node2D>();

        try
        {
            var npcCount = AssertFloorNpcIds(floorRoot);
            AssertThat(npcCount).IsEqual(0);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static int AssertFloorNpcIds(Node2D floorRoot, params string[] expectedNpcIds)
    {
        var gridMap = floorRoot.GetNode<GridMap>("GridMap");
        var foundNpcIds = new Godot.Collections.Array<string>();

        foreach (Node child in gridMap.GetChildren())
        {
            if (child is not NpcSpawn spawn)
                continue;

            foundNpcIds.Add(spawn.NpcId);
            AssertThat(spawn.NpcId).IsNotEmpty();
            AssertThat(NpcCatalog.GetById(spawn.NpcId)).IsNotNull();
        }

        foreach (var expectedNpcId in expectedNpcIds)
        {
            AssertThat(foundNpcIds.Contains(expectedNpcId)).IsTrue();
        }

        return foundNpcIds.Count;
    }
}
