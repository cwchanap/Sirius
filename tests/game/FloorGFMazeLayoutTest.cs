using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorGFMazeLayoutTest : Node
{
    private static readonly Vector2I PlayerStart = new(8, 50);
    private static readonly Vector2I StairPosition = new(82, 68);

    [TestCase]
    public void FloorGF_GeneratedMaze_HasExpectedStaticLayers()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var groundLayer = gridMap.GetNode<TileMapLayer>("GroundLayer");
            var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");

            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(10000);
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(6000);
            AssertThat(stairLayer.GetUsedCells().Count).IsEqual(1);
            AssertThat(stairLayer.GetUsedCells().Contains(StairPosition)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void FloorGF_GeneratedMaze_EntitiesAreOnWalkableTiles()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(IsWalkable(PlayerStart, walls)).IsTrue();
            AssertThat(IsWalkable(StairPosition, walls)).IsTrue();

            foreach (var child in gridMap.GetChildren())
            {
                if (child is EnemySpawn enemySpawn)
                {
                    AssertThat(IsWalkable(enemySpawn.GridPosition, walls)).IsTrue();
                }
                else if (child is NpcSpawn npcSpawn)
                {
                    AssertThat(IsWalkable(npcSpawn.GridPosition, walls)).IsTrue();
                }
                else if (child is StairConnection stair)
                {
                    AssertThat(IsWalkable(stair.GridPosition, walls)).IsTrue();
                }
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void FloorGF_GeneratedMaze_CriticalBeatsAreReachable()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            var goals = new List<Vector2I> { StairPosition };
            goals.AddRange(gridMap.GetChildren().OfType<NpcSpawn>().Select(n => n.GridPosition));
            goals.Add(gridMap.GetChildren().OfType<EnemySpawn>().First(e => e.Name == "EnemySpawn_Goblin").GridPosition);

            foreach (var goal in goals)
            {
                AssertThat(HasPath(PlayerStart, goal, walls)).IsTrue();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static HashSet<Vector2I> GetWalls(GridMap gridMap)
    {
        var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
        return wallLayer.GetUsedCells().ToHashSet();
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return position.X >= 0
            && position.X < 100
            && position.Y >= 0
            && position.Y < 100
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
