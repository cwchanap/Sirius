using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Floor1FMazeLayoutTest : Node
{
    private static readonly Vector2I PlayerStart = new(8, 30);
    private static readonly Vector2I DownStair = new(8, 30);
    private static readonly Vector2I UpStairA = new(49, 12);
    private static readonly Vector2I UpStairB = new(48, 48);
    private static readonly Vector2I[] HiddenPlaceholders =
    [
        new Vector2I(16, 8),
        new Vector2I(56, 30),
        new Vector2I(19, 54)
    ];
    private static readonly Vector2I[] DecisionIntersections =
    [
        new Vector2I(12, 37),
        new Vector2I(13, 28),
        new Vector2I(28, 8),
        new Vector2I(28, 11),
        new Vector2I(52, 34),
        new Vector2I(50, 32)
    ];

    private static readonly Dictionary<string, string> ExpectedEnemyTypes = new()
    {
        ["EnemySpawn_Goblin_Branch"] = "goblin",
        ["EnemySpawn_Orc_Central"] = "orc",
        ["EnemySpawn_Skeleton_StairA"] = "skeleton_warrior",
        ["EnemySpawn_ForestSpirit_StairB"] = "forest_spirit",
        ["EnemySpawn_Orc_HiddenBranch"] = "orc"
    };

    [TestCase]
    public void Floor1F_GeneratedMaze_HasExpectedStaticLayers()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var groundLayer = gridMap.GetNode<TileMapLayer>("GroundLayer");
            var wallLayer = gridMap.GetNode<TileMapLayer>("WallLayer");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");
            var stairCells = stairLayer.GetUsedCells();

            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(3600);
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(22000);
            AssertThat(stairCells.Count).IsEqual(3);
            AssertThat(stairCells.Contains(DownStair)).IsTrue();
            AssertThat(stairCells.Contains(UpStairA)).IsTrue();
            AssertThat(stairCells.Contains(UpStairB)).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HasNoNpcsAndExpectedEnemyGates()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");

            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);

            var enemies = gridMap.GetChildren()
                .OfType<EnemySpawn>()
                .ToDictionary(enemy => enemy.Name.ToString(), enemy => enemy);

            AssertThat(enemies.Count).IsEqual(ExpectedEnemyTypes.Count);

            foreach (var expectedEnemy in ExpectedEnemyTypes)
            {
                AssertThat(enemies.ContainsKey(expectedEnemy.Key)).IsTrue();
                AssertThat(enemies[expectedEnemy.Key].EnemyType).IsEqual(expectedEnemy.Value);
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_StairsHaveExpectedIdsAndDestinations()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(stairs.Count).IsEqual(3);
            AssertThat(stairs.ContainsKey("1F_001")).IsTrue();
            AssertThat(stairs.ContainsKey("1F_2F_A")).IsTrue();
            AssertThat(stairs.ContainsKey("1F_2F_B")).IsTrue();

            AssertThat(stairs["1F_001"].GridPosition).IsEqual(DownStair);
            AssertThat(stairs["1F_001"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["1F_001"].TargetFloor).IsEqual(0);
            AssertThat(stairs["1F_001"].DestinationStairId).IsEqual("GF_000");

            AssertThat(stairs["1F_2F_A"].GridPosition).IsEqual(UpStairA);
            AssertThat(stairs["1F_2F_A"].Direction).IsEqual(StairDirection.Up);
            AssertThat(stairs["1F_2F_A"].TargetFloor).IsEqual(2);
            AssertThat(stairs["1F_2F_A"].DestinationStairId).IsEqual("2F_1F_A");

            AssertThat(stairs["1F_2F_B"].GridPosition).IsEqual(UpStairB);
            AssertThat(stairs["1F_2F_B"].Direction).IsEqual(StairDirection.Up);
            AssertThat(stairs["1F_2F_B"].TargetFloor).IsEqual(2);
            AssertThat(stairs["1F_2F_B"].DestinationStairId).IsEqual("2F_1F_B");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_CriticalBeatsAreReachableWhenEnemiesAreClearable()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var goals = new List<Vector2I> { UpStairA, UpStairB };
            goals.AddRange(HiddenPlaceholders);
            goals.AddRange(gridMap.GetChildren().OfType<EnemySpawn>().Select(enemy => enemy.GridPosition));

            AssertThat(IsWalkable(PlayerStart, walls)).IsTrue();

            foreach (var goal in goals)
            {
                AssertThat(IsInsideFloor(goal)).IsTrue();
                AssertThat(IsWalkable(goal, walls)).IsTrue();
                AssertThat(HasPath(PlayerStart, goal, walls)).IsTrue();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HasMultipleDeadEndBranches()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(CountDeadEndCells(walls)).IsGreaterEqual(8);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HasExpectedDecisionIntersections()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            foreach (var intersection in DecisionIntersections)
            {
                AssertThat(NeighborCount(intersection, walls)).IsGreaterEqual(3);
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_EnemyGatesBlockBranchesUntilCleared()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var blockedCells = GetWalls(gridMap);
            foreach (var enemy in gridMap.GetChildren().OfType<EnemySpawn>())
            {
                blockedCells.Add(enemy.GridPosition);
            }

            var goals = new List<Vector2I> { UpStairA, UpStairB };
            goals.AddRange(HiddenPlaceholders);

            foreach (var goal in goals)
            {
                AssertThat(HasPath(PlayerStart, goal, blockedCells)).IsFalse();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_SouthStairGateDoesNotOpenNorthStair()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var blockedCells = GetWalls(gridMap);
            foreach (var enemy in gridMap.GetChildren().OfType<EnemySpawn>())
            {
                if (enemy.Name == "EnemySpawn_ForestSpirit_StairB")
                {
                    continue;
                }

                blockedCells.Add(enemy.GridPosition);
            }

            AssertThat(HasPath(PlayerStart, UpStairA, blockedCells)).IsFalse();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HiddenPlaceholdersAreNotVisibleStairs()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairLayer = gridMap.GetNode<TileMapLayer>("StairLayer");
            var stairCells = stairLayer.GetUsedCells().ToHashSet();
            var stairNodes = gridMap.GetChildren().OfType<StairConnection>().Select(stair => stair.GridPosition).ToHashSet();

            foreach (var hidden in HiddenPlaceholders)
            {
                AssertThat(stairCells.Contains(hidden)).IsFalse();
                AssertThat(stairNodes.Contains(hidden)).IsFalse();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor1F.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static HashSet<Vector2I> GetWalls(GridMap gridMap)
    {
        return gridMap.GetNode<TileMapLayer>("WallLayer").GetUsedCells().ToHashSet();
    }

    private static bool IsInsideFloor(Vector2I position)
    {
        return position.X >= 0
            && position.X < 60
            && position.Y >= 0
            && position.Y < 60;
    }

    private static bool IsWalkable(Vector2I position, HashSet<Vector2I> walls)
    {
        return IsInsideFloor(position) && !walls.Contains(position);
    }

    private static int CountDeadEndCells(HashSet<Vector2I> walls)
    {
        var deadEnds = 0;
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 60; x++)
            {
                var position = new Vector2I(x, y);
                if (!IsWalkable(position, walls))
                {
                    continue;
                }

                var neighborCount = 0;
                foreach (var neighbor in new[]
                {
                    new Vector2I(x + 1, y),
                    new Vector2I(x - 1, y),
                    new Vector2I(x, y + 1),
                    new Vector2I(x, y - 1)
                })
                {
                    if (IsWalkable(neighbor, walls))
                    {
                        neighborCount++;
                    }
                }

                if (neighborCount == 1)
                {
                    deadEnds++;
                }
            }
        }

        return deadEnds;
    }

    private static int NeighborCount(Vector2I position, HashSet<Vector2I> walls)
    {
        var count = 0;
        foreach (var neighbor in new[]
        {
            new Vector2I(position.X + 1, position.Y),
            new Vector2I(position.X - 1, position.Y),
            new Vector2I(position.X, position.Y + 1),
            new Vector2I(position.X, position.Y - 1)
        })
        {
            if (IsWalkable(neighbor, walls))
            {
                count++;
            }
        }

        return count;
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
