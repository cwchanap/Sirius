using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class Floor2FMazeLayoutTest : Node
{
    private const int EnemyDensityMultiplier = 3;
    private const string SupplementalEnemyPrefix = "EnemySpawn_2F_DensityPatrol_";
    private static readonly HashSet<string> SupplementalEnemyTypes = new()
    {
        "cave_spider",
        "skeleton_warrior",
        "grave_hexer",
        "bone_archer",
        "iron_revenant",
        "cursed_gargoyle",
        "crypt_sentinel"
    };
    private static readonly Vector2I PlayerStart = new(11, 10);
    private static readonly Vector2I DownStairA = new(10, 10);
    private static readonly Vector2I DownStairB = new(26, 10);
    private static readonly Vector2I UpStair = new(52, 50);
    private const string ExpectedPuzzleId = "Puzzle_2F_EastArchiveTrial";

    private static readonly Dictionary<string, string> ExpectedEnemyTypes = new()
    {
        ["EnemySpawn_2F_ArchiveGate"] = "skeleton_warrior",
        ["EnemySpawn_2F_GalleryGate"] = "grave_hexer",
        ["EnemySpawn_2F_UpStairGuard"] = "crypt_sentinel",
        ["EnemySpawn_2F_PuzzleApproach"] = "cave_spider",
        ["EnemySpawn_2F_WestSupply"] = "cave_spider",
        ["EnemySpawn_2F_WestLoop"] = "skeleton_warrior",
        ["EnemySpawn_2F_NorthStudy"] = "grave_hexer",
        ["EnemySpawn_2F_NorthStacks"] = "bone_archer",
        ["EnemySpawn_2F_PuzzleSide"] = "cave_spider",
        ["EnemySpawn_2F_WestReadingRoom"] = "skeleton_warrior",
        ["EnemySpawn_2F_CentralArchive"] = "bone_archer",
        ["EnemySpawn_2F_EastStacks"] = "iron_revenant",
        ["EnemySpawn_2F_SouthShortcut"] = "grave_hexer",
        ["EnemySpawn_2F_EastDeadEnd"] = "cursed_gargoyle",
        ["EnemySpawn_2F_UpperAlcove"] = "bone_archer",
        ["EnemySpawn_2F_EastGallery"] = "iron_revenant",
        ["EnemySpawn_2F_LowerWatch"] = "iron_revenant",
        ["EnemySpawn_2F_SouthApproach"] = "cave_spider",
        ["EnemySpawn_2F_SouthArmory"] = "iron_revenant",
        ["EnemySpawn_2F_StairWatch"] = "cursed_gargoyle"
    };

    private static readonly Dictionary<string, (Vector2I Position, int Gold, Dictionary<string, int> Items)> ExpectedTreasureBoxes = new()
    {
        ["TreasureBox_2F_WestSupplyCache"] = (new Vector2I(6, 16), 100, new Dictionary<string, int> { ["greater_health_potion"] = 1 }),
        ["TreasureBox_2F_WestArchiveCache"] = (new Vector2I(4, 32), 120, new Dictionary<string, int> { ["major_health_potion"] = 1 }),
        ["TreasureBox_2F_NorthLandingCache"] = (new Vector2I(18, 4), 0, new Dictionary<string, int> { ["major_mana_potion"] = 1 }),
        ["TreasureBox_2F_NorthStudyCache"] = (new Vector2I(44, 8), 0, new Dictionary<string, int> { ["major_mana_potion"] = 1 }),
        ["TreasureBox_2F_SouthStacksCache"] = (new Vector2I(13, 55), 130, new Dictionary<string, int> { ["warding_charm"] = 1 }),
        ["TreasureBox_2F_EastGalleryCache"] = (new Vector2I(56, 36), 140, new Dictionary<string, int> { ["smoke_bomb"] = 1 }),
        ["TreasureBox_2F_EastStudyCache"] = (new Vector2I(56, 24), 150, new Dictionary<string, int> { ["smoke_bomb"] = 1 }),
        ["TreasureBox_2F_SouthArmoryCache"] = (new Vector2I(42, 55), 0, new Dictionary<string, int> { ["steel_tower_shield"] = 1 }),
        ["TreasureBox_2F_SouthShortcutCache"] = (new Vector2I(30, 56), 0, new Dictionary<string, int> { ["swift_boots"] = 1 }),
        ["TreasureBox_2F_StairWatchCache"] = (new Vector2I(53, 48), 160, new Dictionary<string, int> { ["swift_boots"] = 1 }),
        ["TreasureBox_2F_PuzzleVaultCache"] = (new Vector2I(35, 38), 0, new Dictionary<string, int> { ["warding_charm"] = 1 })
    };

    private static readonly Dictionary<string, (Vector2I Position, int Damage)> ExpectedTrapTiles = new()
    {
        ["TrapTile_2F_ArchiveTrial_01"] = (new Vector2I(29, 35), 14),
        ["TrapTile_2F_ArchiveTrial_02"] = (new Vector2I(30, 36), 14),
        ["TrapTile_2F_ArchiveTrial_03"] = (new Vector2I(31, 39), 14)
    };

    private static readonly Dictionary<string, (Vector2I Position, string Prompt, string Activated)> ExpectedPuzzleSwitches = new()
    {
        ["PuzzleSwitch_2F_ArchiveTrial_Lever"] = (
            new Vector2I(27, 34),
            "Use",
            "The archive lock starts listening.")
    };

    private static readonly Dictionary<string, (Vector2I Position, bool StartsClosed)> ExpectedPuzzleGates = new()
    {
        ["PuzzleGate_2F_ArchiveTrial_Vault"] = (new Vector2I(33, 38), true),
        ["PuzzleGate_2F_ArchiveTrial_Shortcut"] = (new Vector2I(38, 44), true)
    };

    private static readonly Dictionary<string, (Vector2I Position, string CorrectChoiceId, int WrongDamage)> ExpectedPuzzleRiddles = new()
    {
        ["PuzzleRiddle_2F_ArchiveTrial_Seal"] = (new Vector2I(32, 36), "lever_memory", 14)
    };

    [TestCase]
    public void Floor2F_GeneratedMaze_HasExpectedStaticLayers()
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
            AssertThat(wallLayer.GetUsedCells().Count).IsGreater(1900);
            AssertThat(stairCells.Count).IsEqual(3);
            AssertThat(stairCells.Contains(DownStairA)).IsTrue();
            AssertThat(stairCells.Contains(DownStairB)).IsTrue();
            AssertThat(stairCells.Contains(UpStair)).IsTrue();
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
    public void Floor2F_GeneratedMaze_HasNoNpcsAndExpectedEnemies()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);

            var enemies = gridMap.GetChildren()
                .OfType<EnemySpawn>()
                .ToDictionary(enemy => enemy.Name.ToString(), enemy => enemy);

            AssertThat(enemies.Count).IsEqual(ExpectedEnemyTypes.Count * EnemyDensityMultiplier);

            foreach (var expectedEnemy in ExpectedEnemyTypes)
            {
                AssertThat(enemies.ContainsKey(expectedEnemy.Key)).IsTrue();
                AssertThat(enemies[expectedEnemy.Key].EnemyType).IsEqual(expectedEnemy.Value);
                AssertThat(IsWalkable(enemies[expectedEnemy.Key].GridPosition, walls)).IsTrue();
            }

            var supplementalEnemies = enemies
                .Where(enemy => enemy.Key.StartsWith(SupplementalEnemyPrefix, System.StringComparison.Ordinal))
                .ToDictionary(enemy => enemy.Key, enemy => enemy.Value);
            AssertThat(supplementalEnemies.Count).IsEqual(ExpectedEnemyTypes.Count * (EnemyDensityMultiplier - 1));
            AssertThat(supplementalEnemies.Keys.ToHashSet()).IsEqual(
                Enumerable.Range(1, supplementalEnemies.Count)
                    .Select(index => $"{SupplementalEnemyPrefix}{index:000}")
                    .ToHashSet());

            foreach (var enemy in supplementalEnemies.Values)
            {
                AssertThat(SupplementalEnemyTypes.Contains(enemy.EnemyType)).IsTrue();
                AssertThat(IsWalkable(enemy.GridPosition, walls)).IsTrue();
            }

            AssertThat(enemies.Keys.All(enemyId =>
                ExpectedEnemyTypes.ContainsKey(enemyId)
                || enemyId.StartsWith(SupplementalEnemyPrefix, System.StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_GeneratedMaze_StairsHaveExpectedIdsAndDestinations()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(stairs.Count).IsEqual(3);
            AssertThat(stairs.ContainsKey("2F_1F_A")).IsTrue();
            AssertThat(stairs.ContainsKey("2F_1F_B")).IsTrue();
            AssertThat(stairs.ContainsKey("2F_3F_A")).IsTrue();

            AssertThat(stairs["2F_1F_A"].GridPosition).IsEqual(DownStairA);
            AssertThat(stairs["2F_1F_A"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_A"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_A"].DestinationStairId).IsEqual("1F_2F_A");

            AssertThat(stairs["2F_1F_B"].GridPosition).IsEqual(DownStairB);
            AssertThat(stairs["2F_1F_B"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["2F_1F_B"].TargetFloor).IsEqual(1);
            AssertThat(stairs["2F_1F_B"].DestinationStairId).IsEqual("1F_2F_B");

            AssertThat(stairs["2F_3F_A"].GridPosition).IsEqual(UpStair);
            AssertThat(stairs["2F_3F_A"].Direction).IsEqual(StairDirection.Up);
            AssertThat(stairs["2F_3F_A"].TargetFloor).IsEqual(3);
            AssertThat(stairs["2F_3F_A"].DestinationStairId).IsEqual("3F_2F_A");
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_GeneratedMaze_HasExpectedTreasureBoxes()
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

            AssertThat(boxes.Count).IsEqual(11);
            AssertThat(boxes.Values.Select(box => box.GridPosition).Distinct().Count()).IsEqual(11);

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
    public void Floor2F_GeneratedMaze_HasExpectedPuzzleTrapSet()
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
                AssertThat(riddle.PromptText).IsEqual("The archive seal asks: what opens the vault without moving the stones?");
                AssertThat(riddle.CorrectChoiceId).IsEqual(expected.Value.CorrectChoiceId);
                AssertThat(riddle.WrongAnswerDamage).IsEqual(expected.Value.WrongDamage);
                AssertThat(choices.Count).IsEqual(3);
                AssertThat(choices["lever_memory"]).IsEqual("The remembered lever");
                AssertThat(choices["broken_key"]).IsEqual("The broken key");
                AssertThat(choices["silent_step"]).IsEqual("The silent step");
            }
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_GeneratedMaze_RequiredRoutesReachableWithPuzzleGatesClosed()
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

            foreach (var goal in new[] { DownStairA, DownStairB, UpStair })
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
    public void Floor2F_GeneratedMaze_PuzzleGatesOpenRewardAndShortcutRoute()
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

            var puzzleRoomSide = new Vector2I(32, 38);
            var reward = ExpectedTreasureBoxes["TreasureBox_2F_PuzzleVaultCache"].Position;
            var shortcutPayoff = new Vector2I(42, 52);

            AssertThat(HasPath(puzzleRoomSide, reward, closedGateWalls)).IsFalse();
            AssertThat(HasPath(puzzleRoomSide, reward, walls)).IsTrue();

            var closedLength = ShortestPathLength(puzzleRoomSide, shortcutPayoff, closedGateWalls);
            var openLength = ShortestPathLength(puzzleRoomSide, shortcutPayoff, walls);

            AssertThat(closedLength.HasValue).IsTrue();
            AssertThat(openLength.HasValue).IsTrue();
            AssertThat(closedLength!.Value - openLength!.Value).IsGreaterEqual(12);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_GeneratedMaze_EnemyGatesBlockMainUpStairRouteUntilCleared()
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
            foreach (var box in gridMap.GetChildren().OfType<TreasureBoxSpawn>())
            {
                blockedCells.Add(box.GridPosition);
            }

            AssertThat(HasPath(PlayerStart, UpStair, blockedCells)).IsFalse();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void Floor2F_GeneratedMaze_DeadEndBranchesHavePayoff()
    {
        var floorRoot = LoadFloor();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var walls = GetWalls(gridMap);

            AssertThat(UnrewardedDeadEndBranches(gridMap, walls).Count).IsEqual(0);
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor1NorthStairImmediatelyLoadsFloor2A()
    {
        // Arrive at 2F DownStairA (10,10); spawn is off-stair at +1x = (11,10).
        await AssertStepOnStairTransition(1, new Vector2I(49, 12), 2, new Vector2I(11, 10));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor1SouthStairImmediatelyLoadsFloor2B()
    {
        // Arrive at 2F DownStairB (26,10); spawn is off-stair at +1x = (27,10).
        await AssertStepOnStairTransition(1, new Vector2I(48, 48), 2, new Vector2I(27, 10));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2DownStairAImmediatelyLoadsFloor1A()
    {
        // Arrive at 1F UpStairA (49,12); spawn is off-stair at +1x = (50,12).
        await AssertStepOnStairTransition(2, DownStairA, 1, new Vector2I(50, 12));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2DownStairBImmediatelyLoadsFloor1B()
    {
        // Arrive at 1F UpStairB (48,48); spawn is off-stair at +1x = (49,48).
        await AssertStepOnStairTransition(2, DownStairB, 1, new Vector2I(49, 48));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor2UpStairImmediatelyLoadsFloor3()
    {
        // Arrive at 3F DownStair (10,10); spawn is off-stair at +1x = (11,10).
        await AssertStepOnStairTransition(2, UpStair, 3, new Vector2I(11, 10));
    }

    [TestCase]
    public async System.Threading.Tasks.Task Game_MovingOntoFloor3DownStairImmediatelyLoadsFloor2()
    {
        // Arrive at 2F UpStair (52,50); spawn is off-stair at +1x = (53,50).
        await AssertStepOnStairTransition(3, new Vector2I(10, 10), 2, new Vector2I(53, 50));
    }

    private static async System.Threading.Tasks.Task AssertStepOnStairTransition(
        int sourceFloor,
        Vector2I sourceStair,
        int expectedFloor,
        Vector2I expectedSpawn)
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
            floorManager.LoadFloor(sourceFloor);
            await AwaitFrames(sceneTree, 8);

            var playerController = game.GetNode<PlayerController>("PlayerController");
            var gridMap = floorManager.CurrentGridMap;
            var start = FindWalkableNeighbor(gridMap, sourceStair, out Vector2I moveDirection);

            SetPrivateField(gridMap, "_playerPosition", start);
            PressMovement(playerController, moveDirection);

            await AwaitFrames(sceneTree, 12);

            AssertThat(floorManager.CurrentFloorIndex).IsEqual(expectedFloor);
            AssertThat(floorManager.CurrentGridMap.GetPlayerPosition()).IsEqual(expectedSpawn);
        }
        finally
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.PendingLoadData = previousPendingLoad;
            }

            if (GodotObject.IsInstanceValid(game))
            {
                game.QueueFree();
                await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private static Node2D LoadFloor()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor2F.tscn");
        AssertThat(packed).IsNotNull();
        return packed!.Instantiate<Node2D>();
    }

    private static async System.Threading.Tasks.Task AwaitFrames(SceneTree sceneTree, int frames)
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

    private static Key DirectionToKey(Vector2I direction)
    {
        if (direction == Vector2I.Right) return Key.Right;
        if (direction == Vector2I.Left) return Key.Left;
        if (direction == Vector2I.Up) return Key.Up;
        if (direction == Vector2I.Down) return Key.Down;
        throw new System.ArgumentException($"Unsupported movement direction {direction}");
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

        throw new System.InvalidOperationException($"No walkable neighbor found for {target}");
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
        {
            throw new System.MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
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

    private static bool HasPath(Vector2I start, Vector2I goal, HashSet<Vector2I> walls)
    {
        return ShortestPathLength(start, goal, walls).HasValue;
    }

    private static List<List<Vector2I>> UnrewardedDeadEndBranches(GridMap gridMap, HashSet<Vector2I> walls)
    {
        var payoffs = BranchPayoffPositions(gridMap);
        return DeadEndBranches(walls)
            .Where(branch =>
            {
                var branchAndAdjacent = branch.ToHashSet();
                foreach (var cell in branch)
                {
                    branchAndAdjacent.UnionWith(Neighbors(cell).Where(neighbor => IsWalkable(neighbor, walls)));
                }

                return !branchAndAdjacent.Overlaps(payoffs);
            })
            .ToList();
    }

    private static HashSet<Vector2I> BranchPayoffPositions(GridMap gridMap)
    {
        var positions = new HashSet<Vector2I>();
        positions.UnionWith(gridMap.GetChildren().OfType<EnemySpawn>().Select(enemy => enemy.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<TreasureBoxSpawn>().Select(box => box.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<StairConnection>().Select(stair => stair.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<TrapTileSpawn>().Select(trap => trap.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<PuzzleSwitchSpawn>().Select(puzzleSwitch => puzzleSwitch.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<PuzzleGateSpawn>().Select(gate => gate.GridPosition));
        positions.UnionWith(gridMap.GetChildren().OfType<PuzzleRiddleSpawn>().Select(riddle => riddle.GridPosition));
        return positions;
    }

    private static List<List<Vector2I>> DeadEndBranches(HashSet<Vector2I> walls)
    {
        var branches = new List<List<Vector2I>>();
        for (var y = 0; y < 60; y++)
        {
            for (var x = 0; x < 60; x++)
            {
                var leaf = new Vector2I(x, y);
                if (!IsWalkable(leaf, walls) || NeighborCount(leaf, walls) != 1)
                {
                    continue;
                }

                var branch = new List<Vector2I> { leaf };
                Vector2I? previous = null;
                var current = leaf;
                while (true)
                {
                    var nextCells = Neighbors(current)
                        .Where(neighbor => IsWalkable(neighbor, walls) && (!previous.HasValue || neighbor != previous.Value))
                        .ToList();
                    if (nextCells.Count == 0)
                    {
                        break;
                    }

                    var nextCell = nextCells[0];
                    if (NeighborCount(nextCell, walls) != 2)
                    {
                        break;
                    }

                    branch.Add(nextCell);
                    previous = current;
                    current = nextCell;
                }

                branches.Add(branch);
            }
        }

        return branches;
    }

    private static int NeighborCount(Vector2I position, HashSet<Vector2I> walls)
    {
        var count = 0;
        foreach (var neighbor in Neighbors(position))
        {
            if (IsWalkable(neighbor, walls))
            {
                count++;
            }
        }

        return count;
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

[TestSuite]
[RequireGodotRuntime]
public partial class Floor3FPlaceholderLayoutTest : Node
{
    private static readonly Vector2I DownStair = new(10, 10);

    [TestCase]
    public void Floor3F_Placeholder_IsRegisteredLandingForFloor2F()
    {
        var definition = GD.Load<FloorDefinition>("res://resources/floors/Floor3F.tres");
        AssertThat(definition).IsNotNull();
        AssertThat(definition!.FloorNumber).IsEqual(3);
        // PlayerStart must NOT equal DownStair — spawning on a stair would
        // immediately trigger a floor transition (see PlayerStartOnStair validator).
        AssertThat(definition.PlayerStartPosition).IsEqual(new Vector2I(11, 10));
        AssertThat(definition.PlayerStartPosition).IsNotEqual(DownStair);
        AssertThat(definition.StairsUp.Count).IsEqual(0);
        AssertThat(definition.StairsDown.Count).IsEqual(1);
        AssertThat(definition.StairsDown[0]).IsEqual(DownStair);
    }

    [TestCase]
    public void Floor3F_Placeholder_HasOneReturnStairAndNoContent()
    {
        var packed = GD.Load<PackedScene>("res://scenes/game/floors/Floor3F.tscn");
        AssertThat(packed).IsNotNull();
        var floorRoot = packed!.Instantiate<Node2D>();
        try
        {
            var gridMap = floorRoot.GetNode<GridMap>("GridMap");
            var stairCells = gridMap.GetNode<TileMapLayer>("StairLayer").GetUsedCells();
            var stairs = gridMap.GetChildren().OfType<StairConnection>().ToDictionary(stair => stair.StairId);

            AssertThat(gridMap.GridWidth).IsEqual(24);
            AssertThat(gridMap.GridHeight).IsEqual(18);
            AssertThat(gridMap.GetNode<TileMapLayer>("GroundLayer").GetUsedCells().Count).IsEqual(432);
            AssertThat(stairCells.Count).IsEqual(1);
            AssertThat(stairCells.Contains(DownStair)).IsTrue();
            AssertThat(gridMap.GetChildren().OfType<EnemySpawn>().Count()).IsEqual(0);
            AssertThat(gridMap.GetChildren().OfType<NpcSpawn>().Count()).IsEqual(0);
            AssertThat(gridMap.GetChildren().OfType<TreasureBoxSpawn>().Count()).IsEqual(0);
            AssertThat(gridMap.GetChildren().OfType<TrapTileSpawn>().Count()).IsEqual(0);
            AssertThat(stairs.Count).IsEqual(1);
            AssertThat(stairs["3F_2F_A"].GridPosition).IsEqual(DownStair);
            AssertThat(stairs["3F_2F_A"].Direction).IsEqual(StairDirection.Down);
            AssertThat(stairs["3F_2F_A"].TargetFloor).IsEqual(2);
            AssertThat(stairs["3F_2F_A"].DestinationStairId).IsEqual("2F_3F_A");
        }
        finally
        {
            floorRoot.Free();
        }
    }
}
