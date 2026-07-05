using GdUnit4;
using Godot;
using Sirius.FloorTools;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
public partial class FloorGraphTest
{
    [TestCase]
    public void TestConnectedCellsBFS()
    {
        var walkable = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(2, 0), new(5, 5) // (5,5) disconnected
        };
        var connected = FloorGraph.ConnectedCells(walkable, new Vector2I(0, 0));
        AssertThat(connected.Contains(new Vector2I(2, 0))).IsTrue();
        AssertThat(connected.Contains(new Vector2I(5, 5))).IsFalse();
    }

    [TestCase]
    public void TestWalkableNeighborCount()
    {
        var walkable = new HashSet<Vector2I> { new(0, 0), new(1, 0), new(0, 1) };
        AssertThat(FloorGraph.WalkableNeighborCount(walkable, new Vector2I(0, 0))).IsEqual(2);
    }

    [TestCase]
    public void TestDeadEndBranchesFindsLeaf()
    {
        // corridor: (0,0)-(1,0)-(2,0)-(3,0); (0,0) is a leaf with 1 neighbor
        var walkable = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0)
        };
        var branches = FloorGraph.DeadEndBranches(walkable, 4, 1);
        AssertThat(branches.Count >= 1).IsTrue();
    }
}
