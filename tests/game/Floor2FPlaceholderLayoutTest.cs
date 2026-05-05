using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Floor2FPlaceholderLayoutTest : Node
{
    private static readonly Vector2I DownStairA = new(10, 10);
    private static readonly Vector2I DownStairB = new(26, 10);

    [TestCase]
    public void Floor2F_Placeholder_HasTwoReturnStairsAndNoContent()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");
            var stairCells = stairLayer.GetUsedCells();

            AssertThat(gridMap.GetNode<TileMapLayer>("GroundLayer").GetUsedCells().Count).IsEqual(792);
            AssertThat(gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().Count).IsGreater(24000);
            AssertThat(stairCells.Count).IsEqual(2);
            AssertThat(stairCells.Contains(DownStairA)).IsTrue();
            AssertThat(stairCells.Contains(DownStairB)).IsTrue();
            AssertThat(gridMap.GetChildren().OfType<EnemySpawn>().Count()).IsEqual(0);
            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_Placeholder_StairsLinkBackToFloor1()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(stairs.Count).IsEqual(2);
            AssertThat(stairs.ContainsKey("2F_1F_A")).IsTrue();
            AssertThat(stairs.ContainsKey("2F_1F_B")).IsTrue();

            AssertThat(stairs["2F_1F_A"].GridPosition).IsEqual(DownStairA);
            AssertThat(stairs["2F_1F_A"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_A"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_A"].DestinationStairId).IsEqual("1F_2F_A");

            AssertThat(stairs["2F_1F_B"].GridPosition).IsEqual(DownStairB);
            AssertThat(stairs["2F_1F_B"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_B"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_B"].DestinationStairId).IsEqual("1F_2F_B");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_Placeholder_ReturnStairsAreConnected()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().ToHashSet();

            AssertThat(IsWalkable(DownStairA, walls)).IsTrue();
            AssertThat(IsWalkable(DownStairB, walls)).IsTrue();
            AssertThat(HasPath(DownStairA, DownStairB, walls)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor2F.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return position.X >= 0
            && position.X < 36
            && position.Y >= 0
            && position.Y < 22
            && !walls.Contains(position);
    }

    private static bool HasPath(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        var queue = new Queue<Vector2I>();
        var seen = new HashSet<Vector2I> { start };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return true;
            }

            foreach (var next in Neighbors(current))
            {
                if (!IsWalkable(next, walls) || seen.Contains(next))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static IEnumerable<Vector2I> Neighbors(Vector2I position)
    {
        yield return new Vector2I(position.X + 1, position.Y);
        yield return new Vector2I(position.X - 1, position.Y);
        yield return new Vector2I(position.X, position.Y + 1);
        yield return new Vector2I(position.X, position.Y - 1);
    }
}
