using GdUnit4;
using Godot;
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
        // corridor: (0,0)-(1,0)-(2,0)-(3,0); both terminals are leaves with 1 neighbor.
        // Each branch should trace from its leaf inward and stop before the opposite
        // terminal (which has 1 neighbor, not 2, so the walk halts).
        var walkable = new HashSet<Vector2I>
        {
            new(0, 0), new(1, 0), new(2, 0), new(3, 0)
        };
        var branches = FloorGraph.DeadEndBranches(walkable, 4, 1);
        AssertThat(branches.Count).IsEqual(2);

        // Branch from (0,0): leaf -> (1,0) -> (2,0); stops before (3,0).
        var fromLeft = branches[0];
        AssertThat(fromLeft[0]).IsEqual(new Vector2I(0, 0));
        AssertThat(fromLeft.Contains(new Vector2I(1, 0))).IsTrue();
        AssertThat(fromLeft.Contains(new Vector2I(2, 0))).IsTrue();
        AssertThat(fromLeft.Contains(new Vector2I(3, 0))).IsFalse();

        // Branch from (3,0): leaf -> (2,0) -> (1,0); stops before (0,0).
        var fromRight = branches[1];
        AssertThat(fromRight[0]).IsEqual(new Vector2I(3, 0));
        AssertThat(fromRight.Contains(new Vector2I(2, 0))).IsTrue();
        AssertThat(fromRight.Contains(new Vector2I(1, 0))).IsTrue();
        AssertThat(fromRight.Contains(new Vector2I(0, 0))).IsFalse();
    }
}
