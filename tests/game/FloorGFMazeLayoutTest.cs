using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorGFMazeLayoutTest : Node
{
    private static readonly Vector2I PlayerStart = new(8, 50);
    private static readonly Vector2I StairPosition = new(82, 68);
    private static readonly Dictionary<string, (Vector2I Position, int Gold, Dictionary<string, int> Items)> ExpectedTreasureBoxes = new()
    {
        ["TreasureBox_GF_EntranceCache"] = (new Vector2I(15, 50), 35, new Dictionary<string, int> { ["health_potion"] = 1 }),
        ["TreasureBox_GF_NorthwestCache"] = (new Vector2I(30, 8), 60, new Dictionary<string, int> { ["mana_potion"] = 1 }),
        ["TreasureBox_GF_NorthLoopCache"] = (new Vector2I(49, 8), 80, new Dictionary<string, int> { ["strength_tonic"] = 1 }),
        ["TreasureBox_GF_EastBranchCache"] = (new Vector2I(91, 30), 110, new Dictionary<string, int> { ["greater_health_potion"] = 1 }),
        ["TreasureBox_GF_StairDistrictCache"] = (new Vector2I(94, 68), 75, new Dictionary<string, int> { ["iron_skin"] = 1 }),
        ["TreasureBox_GF_SouthDeepCache"] = (new Vector2I(52, 94), 0, new Dictionary<string, int> { ["iron_sword"] = 1 }),
        ["TreasureBox_GF_SouthwestCache"] = (new Vector2I(7, 72), 50, new Dictionary<string, int> { ["antidote"] = 2 }),
        ["TreasureBox_GF_SoutheastCache"] = (new Vector2I(80, 82), 0, new Dictionary<string, int> { ["iron_shield"] = 1 })
    };

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

            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(25600);
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
                else if (child is TreasureBoxSpawn treasureBox)
                {
                    AssertThat(IsWalkable(treasureBox.GridPosition, walls)).IsTrue();
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

    [TestCase]
    public void FloorGF_GeneratedMaze_HasExpectedTreasureBoxes()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var boxes = gridMap.GetChildren()
                .OfType<TreasureBoxSpawn>()
                .ToDictionary(box => box.TreasureBoxId);
            var occupied = gridMap.GetChildren()
                .Where(child => child is EnemySpawn || child is NpcSpawn || child is StairConnection)
                .Select(child => child switch
                {
                    EnemySpawn enemy => enemy.GridPosition,
                    NpcSpawn npc => npc.GridPosition,
                    StairConnection stair => stair.GridPosition,
                    _ => Vector2I.Zero
                })
                .ToHashSet();

            AssertThat(boxes.Count).IsEqual(8);
            AssertThat(boxes.Values.Select(box => box.GridPosition).Distinct().Count()).IsEqual(8);

            foreach (var expected in ExpectedTreasureBoxes)
            {
                AssertThat(boxes.ContainsKey(expected.Key)).IsTrue();
                var box = boxes[expected.Key];
                var actualItems = RewardItems(box);

                AssertThat(box.Name.ToString()).IsEqual(expected.Key);
                AssertThat(box.GridPosition).IsEqual(expected.Value.Position);
                AssertThat(IsWalkable(box.GridPosition, walls)).IsTrue();
                AssertThat(occupied.Contains(box.GridPosition)).IsFalse();
                AssertThat(HasPath(PlayerStart, box.GridPosition, walls)).IsTrue();
                AssertThat(box.RewardGold).IsEqual(expected.Value.Gold);
                AssertThat(actualItems.Count).IsEqual(expected.Value.Items.Count);

                foreach (var item in expected.Value.Items)
                {
                    AssertThat(ItemCatalog.ItemExists(item.Key)).IsTrue();
                    AssertThat(actualItems.ContainsKey(item.Key)).IsTrue();
                    AssertThat(actualItems[item.Key]).IsEqual(item.Value);
                }
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public async Task Game_InteractImmediatelyAfterMovingOntoFloorGFStairLoadsFloor1()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var packed = GD.Load<PackedScene>("res://scenes/game/Game.tscn");
        AssertThat(packed).IsNotNull();

        SaveData? previousPendingLoad = SaveManager.Instance?.PendingLoadData;
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.PendingLoadData = null;
        }

        var game = packed!.Instantiate<Game>();

        try
        {
            sceneTree.Root.AddChild(game);
            await AwaitFrames(sceneTree, 4);

            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var playerController = game.GetNode<PlayerController>("PlayerController");
            var gridMap = floorManager.CurrentGridMap;
            var start = FindWalkableNeighbor(gridMap, StairPosition, out Vector2I moveDirection);

            SetPrivateField(gridMap, "_playerPosition", start);
            PressMovement(playerController, moveDirection);
            PressInteract(playerController);

            await AwaitFrames(sceneTree, 12);

            AssertThat(floorManager.CurrentFloorIndex).IsEqual(1);
            AssertThat(floorManager.CurrentGridMap.GetPlayerPosition()).IsEqual(new Vector2I(8, 30));
        }
        finally
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.PendingLoadData = previousPendingLoad;
            }

            if (IsInstanceValid(game))
            {
                game.QueueFree();
                await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/FloorGF.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static async Task AwaitFrames(SceneTree sceneTree, int frames)
    {
        for (int i = 0; i < frames; i++)
        {
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void PressMovement(PlayerController playerController, Vector2I direction)
    {
        playerController._UnhandledInput(new InputEventKey
        {
            Keycode = DirectionToKey(direction),
            Pressed = true
        });
    }

    private static void PressInteract(PlayerController playerController)
    {
        playerController._UnhandledInput(new InputEventAction
        {
            Action = "interact",
            Pressed = true
        });
    }

    private static Key DirectionToKey(Vector2I direction)
    {
        if (direction == Vector2I.Right) return Key.Right;
        if (direction == Vector2I.Left) return Key.Left;
        if (direction == Vector2I.Up) return Key.Up;
        if (direction == Vector2I.Down) return Key.Down;
        throw new ArgumentException($"Unsupported movement direction {direction}");
    }

    private static Vector2I FindWalkableNeighbor(GridMap gridMap, Vector2I target, out Vector2I moveDirection)
    {
        var walls = GetWalls(gridMap);
        var candidates = new[]
        {
            (Position: target + Vector2I.Left, Direction: Vector2I.Right),
            (Position: target + Vector2I.Right, Direction: Vector2I.Left),
            (Position: target + Vector2I.Up, Direction: Vector2I.Down),
            (Position: target + Vector2I.Down, Direction: Vector2I.Up)
        };

        foreach (var candidate in candidates)
        {
            if (IsWalkable(candidate.Position, walls))
            {
                moveDirection = candidate.Direction;
                return candidate.Position;
            }
        }

        throw new InvalidOperationException($"No walkable neighbor found for {target}");
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
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

    private static Dictionary<string, int> RewardItems(TreasureBoxSpawn box)
    {
        var result = new Dictionary<string, int>();
        if (box.RewardItemIds == null)
        {
            return result;
        }

        for (var i = 0; i < box.RewardItemIds.Count; i++)
        {
            var itemId = box.RewardItemIds[i];
            var quantity = box.RewardItemQuantities != null && i < box.RewardItemQuantities.Count
                ? box.RewardItemQuantities[i]
                : 1;
            result[itemId] = quantity;
        }

        return result;
    }
}
