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
        new Vector2I(56, 30)
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
    private const int InteriorWallRunMargin = 4;
    private const int MaxInteriorWallRun = 28;

    private readonly record struct ShortcutRoute(
        string EnemyId,
        Vector2I Entry,
        Vector2I Source,
        Vector2I Target,
        int MinDepth,
        int MinSavings);

    private static readonly ShortcutRoute[] ShortcutRoutes =
    [
        new(
            "EnemySpawn_Skeleton_NorthShortcut",
            new Vector2I(16, 8),
            new Vector2I(36, 4),
            new Vector2I(38, 8),
            24,
            10),
        new(
            "EnemySpawn_ForestSpirit_EastShortcut",
            new Vector2I(56, 30),
            new Vector2I(54, 58),
            new Vector2I(58, 46),
            18,
            10),
        new(
            "EnemySpawn_Orc_SouthShortcut",
            new Vector2I(19, 54),
            new Vector2I(42, 58),
            new Vector2I(23, 58),
            18,
            10)
    ];

    private static readonly Dictionary<string, string> ExpectedEnemyTypes = new()
    {
        ["EnemySpawn_Goblin_Branch"] = "goblin",
        ["EnemySpawn_Orc_Central"] = "orc",
        ["EnemySpawn_Skeleton_StairA"] = "skeleton_warrior",
        ["EnemySpawn_ForestSpirit_StairB"] = "forest_spirit",
        ["EnemySpawn_Orc_HiddenBranch"] = "orc",
        ["EnemySpawn_Skeleton_NorthShortcut"] = "skeleton_warrior",
        ["EnemySpawn_ForestSpirit_EastShortcut"] = "forest_spirit",
        ["EnemySpawn_Orc_SouthShortcut"] = "orc",
        ["EnemySpawn_Goblin_WestDeadEnd"] = "goblin",
        ["EnemySpawn_Goblin_SouthwestSpur"] = "goblin",
        ["EnemySpawn_Goblin_WestLoop"] = "goblin",
        ["EnemySpawn_Goblin_NorthRoom"] = "goblin",
        ["EnemySpawn_Goblin_NorthBranch"] = "goblin",
        ["EnemySpawn_Goblin_CentralSouth"] = "goblin",
        ["EnemySpawn_Goblin_SouthLoop"] = "goblin",
        ["EnemySpawn_Goblin_EastSwitchback"] = "goblin",
        ["EnemySpawn_Goblin_EastCorridor"] = "goblin",
        ["EnemySpawn_Goblin_CentralHall"] = "goblin",
        ["EnemySpawn_Orc_WestCrossing"] = "orc",
        ["EnemySpawn_Orc_NorthConnector"] = "orc",
        ["EnemySpawn_Orc_NortheastBend"] = "orc",
        ["EnemySpawn_Orc_EastHall"] = "orc",
        ["EnemySpawn_Orc_EastLoop"] = "orc",
        ["EnemySpawn_Orc_SoutheastSwitchback"] = "orc",
        ["EnemySpawn_Orc_SouthLoopEast"] = "orc",
        ["EnemySpawn_Orc_CentralLower"] = "orc",
        ["EnemySpawn_Skeleton_NorthDeadEnd"] = "skeleton_warrior",
        ["EnemySpawn_Skeleton_NorthShortcutBend"] = "skeleton_warrior",
        ["EnemySpawn_Skeleton_UpperConnector"] = "skeleton_warrior",
        ["EnemySpawn_Skeleton_EastSpur"] = "skeleton_warrior",
        ["EnemySpawn_Skeleton_SouthSpur"] = "skeleton_warrior",
        ["EnemySpawn_ForestSpirit_EastSwitchback"] = "forest_spirit",
        ["EnemySpawn_ForestSpirit_SouthGallery"] = "forest_spirit"
    };
    private static readonly Dictionary<string, (Vector2I Position, int Gold, Dictionary<string, int> Items)> ExpectedTreasureBoxes = new()
    {
        ["TreasureBox_1F_WestDeadEndCache"] = (new Vector2I(4, 22), 85, new Dictionary<string, int> { ["health_potion"] = 2 }),
        ["TreasureBox_1F_WestLoopCache"] = (new Vector2I(2, 42), 70, new Dictionary<string, int> { ["swiftness_draught"] = 1 }),
        ["TreasureBox_1F_NorthConnectorCache"] = (new Vector2I(30, 19), 0, new Dictionary<string, int> { ["mana_potion"] = 2 }),
        ["TreasureBox_1F_EastHallCache"] = (new Vector2I(52, 24), 120, new Dictionary<string, int> { ["greater_health_potion"] = 1 }),
        ["TreasureBox_1F_NorthStairCache"] = (new Vector2I(49, 14), 0, new Dictionary<string, int> { ["iron_boots"] = 1 }),
        ["TreasureBox_1F_EastShortcutCache"] = (new Vector2I(58, 46), 0, new Dictionary<string, int> { ["steel_longsword"] = 1 }),
        ["TreasureBox_1F_SouthGalleryCache"] = (new Vector2I(38, 55), 130, new Dictionary<string, int> { ["flash_powder"] = 1 }),
        ["TreasureBox_1F_SouthHiddenCache"] = (new Vector2I(24, 56), 0, new Dictionary<string, int> { ["chain_mail"] = 1 })
    };
    private const string ExpectedPuzzleId = "Puzzle_1F_SouthShortcutTrial";
    private static readonly Dictionary<string, (Vector2I Position, int Damage)> ExpectedTrapTiles = new()
    {
        ["TrapTile_1F_SouthTrial_01"] = (new Vector2I(18, 53), 12),
        ["TrapTile_1F_SouthTrial_02"] = (new Vector2I(17, 54), 12),
        ["TrapTile_1F_SouthTrial_03"] = (new Vector2I(20, 54), 12),
        ["TrapTile_1F_SouthTrial_04"] = (new Vector2I(21, 55), 12)
    };
    private static readonly Dictionary<string, (Vector2I Position, string Prompt, string Activated)> ExpectedPuzzleSwitches = new()
    {
        ["PuzzleSwitch_1F_SouthTrial_Lever"] = (
            new Vector2I(16, 52),
            "Use",
            "The lever wakes the old shortcut seal.")
    };
    private static readonly Dictionary<string, (Vector2I Position, bool StartsClosed)> ExpectedPuzzleGates = new()
    {
        ["PuzzleGate_1F_SouthTrial_Shortcut"] = (new Vector2I(23, 56), true)
    };
    private static readonly Dictionary<string, (Vector2I Position, string CorrectChoiceId, int WrongDamage)> ExpectedPuzzleRiddles = new()
    {
        ["PuzzleRiddle_1F_SouthTrial_Seal"] = (new Vector2I(22, 54), "east_stone", 12)
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

            AssertThat(gridMap.GridWidth).IsEqual(60);
            AssertThat(gridMap.GridHeight).IsEqual(60);
            AssertThat(groundLayer.GetUsedCells().Count).IsEqual(3600);
            AssertThat(wallLayer.GetUsedCells().Count).IsLess(3600);
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(2000);
            AssertThat(stairCells.Count).IsEqual(3);
            AssertThat(stairCells.Contains(DownStair)).IsTrue();
            AssertThat(stairCells.Contains(UpStairA)).IsTrue();
            AssertThat(stairCells.Contains(UpStairB)).IsTrue();
            AssertLayerCellsInsideFootprint(groundLayer, 60, 60);
            AssertLayerCellsInsideFootprint(wallLayer, 60, 60);
            AssertLayerCellsInsideFootprint(stairLayer, 60, 60);
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
    public void Floor1F_GeneratedMaze_HasExpectedTreasureBoxes()
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
            occupied.UnionWith(HiddenPlaceholders);

            AssertThat(boxes.Count).IsEqual(8);
            AssertThat(boxes.Values.Select(box => box.GridPosition).Distinct().Count()).IsEqual(8);

            foreach (var expected in ExpectedTreasureBoxes)
            {
                AssertThat(boxes.ContainsKey(expected.Key)).IsTrue();
                var box = boxes[expected.Key];
                var actualItems = TestHelpers.TreasureBoxRewardItems(box);

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
    public void Floor1F_GeneratedMaze_HasExpectedPuzzleTrapSet()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var traps = gridMap.GetChildren().OfType<TrapTileSpawn>().ToDictionary(trap => trap.TrapId);
            var switches = gridMap.GetChildren().OfType<PuzzleSwitchSpawn>().ToDictionary(puzzleSwitch => puzzleSwitch.SwitchId);
            var gates = gridMap.GetChildren().OfType<PuzzleGateSpawn>().ToDictionary(gate => gate.GateId);
            var riddles = gridMap.GetChildren().OfType<PuzzleRiddleSpawn>().ToDictionary(riddle => riddle.RiddleId);

            AssertThat(traps.Count).IsEqual(ExpectedTrapTiles.Count);
            AssertThat(switches.Count).IsEqual(ExpectedPuzzleSwitches.Count);
            AssertThat(gates.Count).IsEqual(ExpectedPuzzleGates.Count);
            AssertThat(riddles.Count).IsEqual(ExpectedPuzzleRiddles.Count);

            var occupied = gridMap.GetChildren()
                .Where(child => child is EnemySpawn || child is NpcSpawn || child is StairConnection || child is TreasureBoxSpawn)
                .Select(child => child switch
                {
                    EnemySpawn enemy => enemy.GridPosition,
                    NpcSpawn npc => npc.GridPosition,
                    StairConnection stair => stair.GridPosition,
                    TreasureBoxSpawn box => box.GridPosition,
                    _ => Vector2I.Zero
                })
                .ToHashSet();
            occupied.UnionWith(HiddenPlaceholders);

            var puzzlePositions = traps.Values.Select(trap => trap.GridPosition)
                .Concat(switches.Values.Select(puzzleSwitch => puzzleSwitch.GridPosition))
                .Concat(gates.Values.Select(gate => gate.GridPosition))
                .Concat(riddles.Values.Select(riddle => riddle.GridPosition))
                .ToList();
            AssertThat(puzzlePositions.Distinct().Count()).IsEqual(puzzlePositions.Count);

            foreach (var position in puzzlePositions)
            {
                AssertThat(IsWalkable(position, walls)).IsTrue();
                AssertThat(occupied.Contains(position)).IsFalse();
            }

            foreach (var expected in ExpectedTrapTiles)
            {
                AssertThat(traps.ContainsKey(expected.Key)).IsTrue();
                var trap = traps[expected.Key];
                AssertThat(trap.PuzzleId).IsEqual(ExpectedPuzzleId);
                AssertThat(trap.GridPosition).IsEqual(expected.Value.Position);
                AssertThat(trap.Damage).IsEqual(expected.Value.Damage);
                AssertThat(trap.StatusEffectId).IsEqual("");
            }

            foreach (var expected in ExpectedPuzzleSwitches)
            {
                AssertThat(switches.ContainsKey(expected.Key)).IsTrue();
                var puzzleSwitch = switches[expected.Key];
                AssertThat(puzzleSwitch.PuzzleId).IsEqual(ExpectedPuzzleId);
                AssertThat(puzzleSwitch.GridPosition).IsEqual(expected.Value.Position);
                AssertThat(puzzleSwitch.PromptText).IsEqual(expected.Value.Prompt);
                AssertThat(puzzleSwitch.ActivatedText).IsEqual(expected.Value.Activated);
            }

            foreach (var expected in ExpectedPuzzleGates)
            {
                AssertThat(gates.ContainsKey(expected.Key)).IsTrue();
                var gate = gates[expected.Key];
                AssertThat(gate.PuzzleId).IsEqual(ExpectedPuzzleId);
                AssertThat(gate.GridPosition).IsEqual(expected.Value.Position);
                AssertThat(gate.StartsClosed).IsEqual(expected.Value.StartsClosed);
                AssertThat(gate.BlocksMovement).IsTrue();
            }

            foreach (var expected in ExpectedPuzzleRiddles)
            {
                AssertThat(riddles.ContainsKey(expected.Key)).IsTrue();
                var riddle = riddles[expected.Key];
                var choices = riddle.GetChoices().ToDictionary(choice => choice.Id, choice => choice.Label);

                AssertThat(riddle.PuzzleId).IsEqual(ExpectedPuzzleId);
                AssertThat(riddle.GridPosition).IsEqual(expected.Value.Position);
                AssertThat(riddle.PromptText).IsEqual("Four stones face the old shortcut. Which stone sleeps until the lever wakes it?");
                AssertThat(riddle.CorrectChoiceId).IsEqual(expected.Value.CorrectChoiceId);
                AssertThat(riddle.WrongAnswerDamage).IsEqual(expected.Value.WrongDamage);
                AssertThat(choices.Count).IsEqual(3);
                AssertThat(choices["north_stone"]).IsEqual("North stone");
                AssertThat(choices["east_stone"]).IsEqual("East stone");
                AssertThat(choices["south_stone"]).IsEqual("South stone");
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
                AssertThat(IsWalkable(intersection, walls)).IsTrue();
                AssertThat(NeighborCount(intersection, walls)).IsGreaterEqual(3);
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_BreaksUpLongInteriorWallRuns()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(MaxConsecutiveWallRun(walls, InteriorWallRunMargin)).IsLessEqual(MaxInteriorWallRun);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_HasDeepShortcutBranches()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var enemyPositions = gridMap.GetChildren()
                .OfType<EnemySpawn>()
                .ToDictionary(enemy => enemy.Name.ToString(), enemy => enemy.GridPosition);

            foreach (var route in ShortcutRoutes)
            {
                AssertThat(IsWalkable(route.Source, walls)).IsTrue();
                var lockedCells = new HashSet<Vector2I>(walls) { enemyPositions[route.EnemyId] };
                var depth = ShortestPathLength(route.Entry, route.Source, lockedCells);
                AssertThat(depth.HasValue).IsTrue();
                AssertThat(depth!.Value).IsGreaterEqual(route.MinDepth);
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_ShortcutEnemiesUnlockShorterRoutes()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var enemyPositions = gridMap.GetChildren()
                .OfType<EnemySpawn>()
                .ToDictionary(enemy => enemy.Name.ToString(), enemy => enemy.GridPosition);

            foreach (var route in ShortcutRoutes)
            {
                AssertThat(enemyPositions.ContainsKey(route.EnemyId)).IsTrue();
                var lockedCells = new HashSet<Vector2I>(walls) { enemyPositions[route.EnemyId] };
                var lockedLength = ShortestPathLength(route.Source, route.Target, lockedCells);
                var unlockedLength = ShortestPathLength(route.Source, route.Target, walls);

                AssertThat(unlockedLength.HasValue).IsTrue();
                if (lockedLength.HasValue)
                {
                    AssertThat(lockedLength.Value - unlockedLength!.Value).IsGreaterEqual(route.MinSavings);
                }
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
    public void Floor1F_GeneratedMaze_RequiredRoutesReachableWithPuzzleGatesClosed()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var blockedCells = GetWalls(gridMap);
            foreach (var gate in gridMap.GetChildren().OfType<PuzzleGateSpawn>().Where(gate => gate.StartsClosed))
            {
                blockedCells.Add(gate.GridPosition);
            }

            foreach (var goal in new[] { UpStairA, UpStairB }.Concat(HiddenPlaceholders))
            {
                AssertThat(HasPath(PlayerStart, goal, blockedCells)).IsTrue();
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor1F_GeneratedMaze_SouthPuzzleGateOpensRewardAndShortcutRoute()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);
            var closedGateWalls = new HashSet<Vector2I>(walls);
            foreach (var gate in gridMap.GetChildren().OfType<PuzzleGateSpawn>().Where(gate => gate.StartsClosed))
            {
                closedGateWalls.Add(gate.GridPosition);
            }

            var puzzleRoomSide = new Vector2I(22, 56);
            var reward = ExpectedTreasureBoxes["TreasureBox_1F_SouthHiddenCache"].Position;
            var shortcutPayoff = new Vector2I(42, 58);

            AssertThat(HasPath(puzzleRoomSide, reward, closedGateWalls)).IsFalse();
            AssertThat(HasPath(puzzleRoomSide, reward, walls)).IsTrue();

            var closedLength = ShortestPathLength(puzzleRoomSide, shortcutPayoff, closedGateWalls);
            var openLength = ShortestPathLength(puzzleRoomSide, shortcutPayoff, walls);

            AssertThat(closedLength.HasValue).IsTrue();
            AssertThat(openLength.HasValue).IsTrue();
            AssertThat(closedLength!.Value - openLength!.Value).IsGreaterEqual(50);
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

    private static void AssertLayerCellsInsideFootprint(TileMapLayer layer, int width, int height)
    {
        foreach (var cell in layer.GetUsedCells())
        {
            AssertThat(cell.X).IsGreaterEqual(0);
            AssertThat(cell.X).IsLess(width);
            AssertThat(cell.Y).IsGreaterEqual(0);
            AssertThat(cell.Y).IsLess(height);
        }
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

    private static int MaxConsecutiveWallRun(HashSet<Vector2I> walls, int margin)
    {
        var maxRun = 0;

        for (var y = margin; y < 60 - margin; y++)
        {
            var run = 0;
            for (var x = margin; x < 60 - margin; x++)
            {
                if (walls.Contains(new Vector2I(x, y)))
                {
                    run++;
                    maxRun = Mathf.Max(maxRun, run);
                }
                else
                {
                    run = 0;
                }
            }
        }

        for (var x = margin; x < 60 - margin; x++)
        {
            var run = 0;
            for (var y = margin; y < 60 - margin; y++)
            {
                if (walls.Contains(new Vector2I(x, y)))
                {
                    run++;
                    maxRun = Mathf.Max(maxRun, run);
                }
                else
                {
                    run = 0;
                }
            }
        }

        return maxRun;
    }

    private static bool HasPath(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        return ShortestPathLength(start, goal, walls).HasValue;
    }

    private static int? ShortestPathLength(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        var queue = new Queue<Vector2I>();
        var distances = new Dictionary<Vector2I, int> { [start] = 0 };
        var seen = new HashSet<Vector2I> { start };
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == goal)
            {
                return distances[current];
            }

            foreach (var next in Neighbors(current))
            {
                if (!IsWalkable(next, walls) || seen.Contains(next))
                {
                    continue;
                }

                seen.Add(next);
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static IEnumerable<Vector2I> Neighbors(Vector2I position)
    {
        yield return new Vector2I(position.X + 1, position.Y);
        yield return new Vector2I(position.X - 1, position.Y);
        yield return new Vector2I(position.X, position.Y + 1);
        yield return new Vector2I(position.X, position.Y - 1);
    }
}
