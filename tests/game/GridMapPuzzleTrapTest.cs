using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GridMapPuzzleTrapTest : Node
{
    [TestCase]
    public async Task RegisterStaticPuzzleTraps_RegistersActiveTrapAsWalkableTrapCell()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(12, 23)
        };

        SetPrivateField(gridMap, "_tilemapOrigin", new Vector2I(10, 20));
        floor.AddChild(gridMap);
        gridMap.AddChild(trap);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            var grid = GetPrivateField<int[,]>(gridMap, "_grid");
            AssertThat(grid[2, 3]).IsEqual((int)GridMap.CellType.TrapTile);
        }
        finally
        {
            floor.Free();
        }
    }

    [TestCase]
    public async Task RegisterStaticPuzzleTraps_RegistersClosedGateAsBlockingGateCell()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var gate = new PuzzleGateSpawn
        {
            Name = "PuzzleGate_Test",
            GateId = "PuzzleGate_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(6, 5),
            StartsClosed = true
        };

        floor.AddChild(gridMap);
        gridMap.AddChild(gate);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            var grid = GetPrivateField<int[,]>(gridMap, "_grid");
            AssertThat(grid[6, 5]).IsEqual((int)GridMap.CellType.PuzzleGate);
            AssertThat(gate.BlocksMovement).IsTrue();
        }
        finally
        {
            floor.Free();
        }
    }

    [TestCase]
    public async Task RegisterStaticPuzzleTraps_RegistersSolvedGateAsEmptyCell()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var previousGameManager = GameManager.Instance;
        var gameManager = new GameManager();
        gameManager.MarkPuzzleSolved("Puzzle_Test");
        SetGameManagerSingleton(gameManager);

        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var gate = new PuzzleGateSpawn
        {
            Name = "PuzzleGate_Test",
            GateId = "PuzzleGate_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(6, 5),
            StartsClosed = true
        };

        floor.AddChild(gridMap);
        gridMap.AddChild(gate);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            var grid = GetPrivateField<int[,]>(gridMap, "_grid");
            AssertThat(grid[6, 5]).IsEqual((int)GridMap.CellType.Empty);
            AssertThat(gate.BlocksMovement).IsFalse();
        }
        finally
        {
            floor.Free();
            gameManager.Free();
            SetGameManagerSingleton(previousGameManager);
        }
    }

    [TestCase]
    public async Task RegisterStaticPuzzleEntities_DoesNotOverwriteHigherPriorityOccupancyCells()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var grid = GetPrivateField<int[,]>(gridMap, "_grid");
        grid[6, 5] = (int)GridMap.CellType.TreasureBox;
        grid[7, 5] = (int)GridMap.CellType.Npc;
        grid[8, 5] = (int)GridMap.CellType.Enemy;
        grid[9, 5] = (int)GridMap.CellType.Wall;

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_OnTreasure",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(6, 5)
        };
        var gate = new PuzzleGateSpawn
        {
            Name = "PuzzleGate_OnNpc",
            GateId = "PuzzleGate_OnNpc",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(7, 5),
            StartsClosed = true
        };
        var puzzleSwitch = new PuzzleSwitchSpawn
        {
            Name = "PuzzleSwitch_OnEnemy",
            SwitchId = "PuzzleSwitch_OnEnemy",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(8, 5)
        };
        var riddle = new PuzzleRiddleSpawn
        {
            Name = "PuzzleRiddle_OnWall",
            RiddleId = "PuzzleRiddle_OnWall",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(9, 5)
        };

        floor.AddChild(gridMap);
        gridMap.AddChild(trap);
        gridMap.AddChild(gate);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(riddle);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            AssertThat(grid[6, 5]).IsEqual((int)GridMap.CellType.TreasureBox);
            AssertThat(grid[7, 5]).IsEqual((int)GridMap.CellType.Npc);
            AssertThat(grid[8, 5]).IsEqual((int)GridMap.CellType.Enemy);
            AssertThat(grid[9, 5]).IsEqual((int)GridMap.CellType.Wall);
        }
        finally
        {
            floor.Free();
        }
    }

    [TestCase]
    public void TryMovePlayer_ActiveTrapMovesPlayerAndEmitsTrapTriggered()
    {
        var gridMap = CreateGridMapWithGrid();
        var grid = GetPrivateField<int[,]>(gridMap, "_grid");
        grid[5, 5] = (int)GridMap.CellType.Player;
        grid[6, 5] = (int)GridMap.CellType.TrapTile;
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(5, 5));

        var events = new System.Collections.Generic.List<string>();
        gridMap.PlayerMoved += position => events.Add($"moved:{position}");
        gridMap.TrapTileTriggered += position => events.Add($"trap:{position}");

        try
        {
            AssertThat(gridMap.TryMovePlayer(Vector2I.Right)).IsTrue();

            AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(6, 5));
            AssertThat(events.Count).IsEqual(2);
            AssertThat(events[0]).IsEqual("moved:(6, 5)");
            AssertThat(events[1]).IsEqual("trap:(6, 5)");
        }
        finally
        {
            gridMap.Free();
        }
    }

    [TestCase]
    public async Task TryMovePlayer_RestoresRegisteredTrapCellAfterPlayerStepsOff()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var grid = GetPrivateField<int[,]>(gridMap, "_grid");
        grid[5, 5] = (int)GridMap.CellType.Player;
        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(6, 5)
        };

        floor.AddChild(gridMap);
        gridMap.AddChild(trap);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            AssertThat(gridMap.TryMovePlayer(Vector2I.Right)).IsTrue();
            AssertThat(grid[6, 5]).IsEqual((int)GridMap.CellType.Player);

            AssertThat(gridMap.TryMovePlayer(Vector2I.Right)).IsTrue();

            AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(7, 5));
            AssertThat(grid[6, 5]).IsEqual((int)GridMap.CellType.TrapTile);
        }
        finally
        {
            floor.Free();
        }
    }

    [TestCase]
    public void TryMovePlayer_ClosedPuzzleGateBlocksMovement()
    {
        var gridMap = CreateGridMapWithGrid();
        var grid = GetPrivateField<int[,]>(gridMap, "_grid");
        grid[5, 5] = (int)GridMap.CellType.Player;
        grid[6, 5] = (int)GridMap.CellType.PuzzleGate;
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(5, 5));

        try
        {
            AssertThat(gridMap.TryMovePlayer(Vector2I.Right)).IsFalse();
            AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(5, 5));
        }
        finally
        {
            gridMap.Free();
        }
    }

    [TestCase]
    public async Task TryRequestPuzzleInteraction_EmitsForSwitchOrRiddleFacingDirection()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floor = new Node2D { Name = "PuzzleFloor" };
        var gridMap = CreateGridMapWithGrid();
        var puzzleSwitch = new PuzzleSwitchSpawn
        {
            Name = "PuzzleSwitch_Test",
            SwitchId = "PuzzleSwitch_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(6, 5)
        };
        var riddle = new PuzzleRiddleSpawn
        {
            Name = "PuzzleRiddle_Test",
            RiddleId = "PuzzleRiddle_Test",
            PuzzleId = "Puzzle_Test",
            GridPosition = new Vector2I(5, 6)
        };

        floor.AddChild(gridMap);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(riddle);
        sceneTree.Root.AddChild(floor);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        var requested = new System.Collections.Generic.List<Vector2I>();
        gridMap.PuzzleInteractionRequested += position => requested.Add(position);

        try
        {
            gridMap.RegisterStaticPuzzleEntities();

            AssertThat(gridMap.TryRequestPuzzleInteraction(Vector2I.Right)).IsTrue();
            AssertThat(gridMap.TryRequestPuzzleInteraction(Vector2I.Down)).IsTrue();
            AssertThat(requested.Count).IsEqual(2);
            AssertThat(requested[0]).IsEqual(new Vector2I(6, 5));
            AssertThat(requested[1]).IsEqual(new Vector2I(5, 6));
        }
        finally
        {
            floor.Free();
        }
    }

    private static GridMap CreateGridMapWithGrid()
    {
        var gridMap = new GridMap { Name = "GridMap" };
        var grid = new int[gridMap.GridWidth, gridMap.GridHeight];
        SetPrivateField(gridMap, "_grid", grid);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(5, 5));
        return gridMap;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        return (T)field.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    private static void SetGameManagerSingleton(GameManager? instance)
    {
        var property = typeof(GameManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, instance);
    }
}
