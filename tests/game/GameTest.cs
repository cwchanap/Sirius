using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameTest : Node
{
    private TestableGame? _game;
    private SubViewport? _viewport;
    private GameManager? _gameManager;
    private bool _incomingTreePaused;
    private Input.MouseModeEnum _incomingMouseMode;

    [Before]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        _incomingTreePaused = sceneTree.Paused;
        _incomingMouseMode = Input.MouseMode;
        sceneTree.Paused = false;

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360)
        };
        sceneTree.Root.AddChild(_viewport);

        _game = new TestableGame();
        _viewport.AddChild(_game);

        _gameManager = new GameManager();
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Paused = false;

        // Reset interaction/battle state before freeing to prevent signal
        // callbacks from firing on half-freed nodes during teardown.
        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            if (_gameManager.IsInNpcInteraction) _gameManager.EndNpcInteraction();
            if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
            if (_gameManager.IsInBattle) _gameManager.EndBattle(false);
        }

        // Use Free() for immediate cleanup to avoid state leaking between tests.
        // Game must be freed before its viewport parent.
        if (_game != null && IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_viewport != null && IsInstanceValid(_viewport))
        {
            _viewport.Free();
            _viewport = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.Free();
            _gameManager = null;
        }

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        Input.MouseMode = _incomingMouseMode;
        sceneTree.Paused = _incomingTreePaused;
    }
    [TestCase]
    public async Task RootCancel_WhenUnblocked_OpensHostedPauseAndOwnsTreePause()
    {
        await ReplaceWithHostedFixture();

        _viewport!.PushInput(new InputEventAction
        {
            Action = "pause_menu",
            Pressed = true
        });
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(_viewport.IsInputHandled()).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(host.ActiveEntries[0].Policy.ProcessPolicy)
            .IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(host.ActiveEntries[0].Policy.PauseTree).IsTrue();

        // Clean up the hosted Pause entry through the host and restore the tree
        // so later tests do not inherit an active screen or a paused tree.
        var closeResult = host.TryClose(
            host.ActiveEntries[0].Handle,
            UIScreenCloseReason.ExplicitAction);
        AssertThat(closeResult.Status).IsEqual(UIScreenCloseStatus.Closed);
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
    }

    [TestCase]
    public async Task Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_RuntimeTest",
                TreasureBoxId = "TreasureBox_RuntimeTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 25,
                RewardItemIds = new Godot.Collections.Array<string> { "health_potion" },
                RewardItemQuantities = new Godot.Collections.Array<int> { 1 }
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitFrames(1);

            var prompt = gameScene.GetNodeOrNull<Label>("UI/GameUI/InteractionPrompt");
            AssertThat(prompt).IsNotNull();
            AssertThat(prompt!.Visible).IsTrue();
            AssertThat(prompt.Text).IsEqual("Open");

            int startingGold = gameManager.Player.Gold;
            int startingPotionCount = gameManager.Player.GetItemQuantity("health_potion");
            PressInteract(playerController);
            await AwaitFrames(1);

            AssertThat(prompt.Visible).IsFalse();

            await AwaitFrames(120);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(startingPotionCount + 1);
            AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeTest")).IsTrue();
            AssertThat(box.IsOpened).IsTrue();
            AssertThat(prompt.Visible).IsFalse();

            PressInteractRelease(playerController);
            PressInteract(playerController);
            await AwaitFrames(30);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(prompt.Visible).IsFalse();
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_AbortedTreasureOpeningDoesNotGrantRewardOrPersistOpenedId()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;
        TreasureBoxSpawn? box = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_RuntimeAbortTest",
                TreasureBoxId = "TreasureBox_RuntimeAbortTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 25
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitFrames(1);

            int startingGold = gameManager.Player.Gold;
            PressInteract(playerController);
            await AwaitFrames(1);

            gridMap.RemoveChild(box);
            await AwaitFrames(120);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold);
            AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeAbortTest")).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        }
        finally
        {
            if (box != null && IsInstanceValid(box))
            {
                box.Free();
            }

            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_TreasureBoxWithEmptyId_DoesNotGrantReward()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_EmptyIdTest",
                TreasureBoxId = "",  // Empty ID — should be rejected
                GridPosition = new Vector2I(9, 50),
                RewardGold = 100
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitFrames(1);

            int startingGold = gameManager.Player.Gold;
            PressInteract(playerController);
            await AwaitFrames(1);

            // Reward must NOT be granted for empty-ID boxes
            AssertThat(gameManager.Player.Gold).IsEqual(startingGold);
            AssertThat(box.IsOpened).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_TrapTileTriggerAppliesDamageAndKeepsPlayerOnTrap()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            var trap = new TrapTileSpawn
            {
                Name = "TrapTile_RuntimeDamageTest",
                PuzzleId = "Puzzle_RuntimeDamageTest",
                GridPosition = new Vector2I(9, 50),
                Damage = 12
            };
            gridMap.AddChild(trap);
            trap.AddToGroup("TrapTileSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gameManager.Player.CurrentHealth = 30;
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitInputDebounce();

            AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(9, 50));
            AssertThat(gameManager.Player.CurrentHealth).IsEqual(18);
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_SwitchThenCorrectRiddleSolvesPuzzleOpensGateAndDisablesTrap()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            const string puzzleId = "Puzzle_RuntimeSolveTest";
            var puzzleSwitch = new PuzzleSwitchSpawn
            {
                Name = "PuzzleSwitch_RuntimeSolveTest",
                SwitchId = "PuzzleSwitch_RuntimeSolveTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(9, 50)
            };
            var riddle = CreateRuntimeRiddle("PuzzleRiddle_RuntimeSolveTest", puzzleId, new Vector2I(8, 51));
            var gate = new PuzzleGateSpawn
            {
                Name = "PuzzleGate_RuntimeSolveTest",
                GateId = "PuzzleGate_RuntimeSolveTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(10, 50),
                StartsClosed = true
            };
            var trap = new TrapTileSpawn
            {
                Name = "TrapTile_RuntimeSolveTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(11, 50),
                Damage = 12
            };

            AddPuzzleNode(gridMap, puzzleSwitch, "PuzzleSwitchSpawn");
            AddPuzzleNode(gridMap, riddle, "PuzzleRiddleSpawn");
            AddPuzzleNode(gridMap, gate, "PuzzleGateSpawn");
            AddPuzzleNode(gridMap, trap, "TrapTileSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitInputDebounce();
            PressInteract(playerController);
            await AwaitFrames(2);
            PressInteractRelease(playerController);
            await AwaitFrames(1);

            PressMovement(playerController, Vector2I.Down);
            await AwaitInputDebounce();
            PressInteract(playerController);
            await AwaitFrames(3);

            var dialog = GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog");
            AssertThat(dialog).IsNotNull();

            int statsChangedCount = 0;
            gameManager.PlayerStatsChanged += () => statsChangedCount++;

            var eastStoneButton = FindButtonWithText(dialog!, "East Stone");
            AssertThat(eastStoneButton).IsNotNull();
            eastStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsTrue();
            AssertThat(gate.BlocksMovement).IsFalse();
            AssertThat(freshGrid[10, 50]).IsEqual((int)GridMap.CellType.Empty);
            AssertThat(freshGrid[11, 50]).IsEqual((int)GridMap.CellType.Empty);
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
            AssertThat(statsChangedCount).IsEqual(1);
            AssertThat(GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog")).IsNull();
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_WrongRiddleAnswerAppliesPenaltyAndAllowsRetry()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            const string puzzleId = "Puzzle_RuntimeWrongAnswerTest";
            var puzzleSwitch = new PuzzleSwitchSpawn
            {
                Name = "PuzzleSwitch_RuntimeWrongAnswerTest",
                SwitchId = "PuzzleSwitch_RuntimeWrongAnswerTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(9, 50)
            };
            var riddle = CreateRuntimeRiddle("PuzzleRiddle_RuntimeWrongAnswerTest", puzzleId, new Vector2I(8, 51));
            riddle.WrongAnswerDamage = 7;

            AddPuzzleNode(gridMap, puzzleSwitch, "PuzzleSwitchSpawn");
            AddPuzzleNode(gridMap, riddle, "PuzzleRiddleSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gameManager.Player.CurrentHealth = 30;
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitInputDebounce();
            PressInteract(playerController);
            await AwaitFrames(2);
            PressInteractRelease(playerController);
            await AwaitFrames(1);

            PressMovement(playerController, Vector2I.Down);
            await AwaitInputDebounce();
            PressInteract(playerController);
            await AwaitFrames(3);

            var dialog = GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog");
            AssertThat(dialog).IsNotNull();

            dialog!.EmitSignal(PuzzleRiddleDialog.SignalName.ChoiceSelected, "north_stone");
            await AwaitFrames(3);

            AssertThat(gameManager.Player.CurrentHealth).IsEqual(23);
            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
            AssertThat(GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog")).IsNull();

            PressInteractRelease(playerController);
            await AwaitFrames(1);
            PressInteract(playerController);
            await AwaitFrames(3);

            var retryDialog = GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog");
            AssertThat(retryDialog).IsNotNull();
            retryDialog!.EmitSignal(PuzzleRiddleDialog.SignalName.PuzzleRiddleClosed);
            await AwaitFrames(2);
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_DormantRiddleShowsMessageAndKeepsDialogOpen()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            const string puzzleId = "Puzzle_RuntimeDormantTest";
            // Place a switch but do NOT arm it yet
            var puzzleSwitch = new PuzzleSwitchSpawn
            {
                Name = "PuzzleSwitch_DormantTest",
                SwitchId = "PuzzleSwitch_DormantTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(9, 50)
            };
            var riddle = CreateRuntimeRiddle("PuzzleRiddle_DormantTest", puzzleId, new Vector2I(8, 51));

            AddPuzzleNode(gridMap, puzzleSwitch, "PuzzleSwitchSpawn");
            AddPuzzleNode(gridMap, riddle, "PuzzleRiddleSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
            await AwaitFrames(3);

            // Walk to the riddle (down from player)
            PressMovement(playerController, Vector2I.Down);
            await AwaitInputDebounce();
            PressInteract(playerController);
            await AwaitFrames(3);

            var dialog = GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog");
            AssertThat(dialog).IsNotNull();

            // Choose an answer before arming the switch — should show dormant message
            var eastStoneButton = FindButtonWithText(dialog!, "East Stone");
            AssertThat(eastStoneButton).IsNotNull();
            eastStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            // Dialog should remain open and still be valid
            var dormantDialog = GetPrivateField<PuzzleRiddleDialog?>(gameScene, "_puzzleRiddleDialog");
            AssertThat(dormantDialog).IsNotNull();
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsFalse();

            // The message label should now display the dormant message
            var messageLabel = FindLabelWithText(dormantDialog!, "dormant");
            AssertThat(messageLabel).IsNotNull();

            // Close the dialog to clean up
            dormantDialog!.EmitSignal(PuzzleRiddleDialog.SignalName.PuzzleRiddleClosed);
            await AwaitFrames(2);
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_PuzzlePromptUsesUseForSwitchAndSolveForRiddle()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");

            AssertThat(gridMap).IsNotNull();

            const string puzzleId = "Puzzle_RuntimePromptTest";
            var puzzleSwitch = new PuzzleSwitchSpawn
            {
                Name = "PuzzleSwitch_RuntimePromptTest",
                SwitchId = "PuzzleSwitch_RuntimePromptTest",
                PuzzleId = puzzleId,
                GridPosition = new Vector2I(9, 50)
            };
            var riddle = CreateRuntimeRiddle("PuzzleRiddle_RuntimePromptTest", puzzleId, new Vector2I(8, 51));

            AddPuzzleNode(gridMap, puzzleSwitch, "PuzzleSwitchSpawn");
            AddPuzzleNode(gridMap, riddle, "PuzzleRiddleSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
            await AwaitFrames(3);

            var prompt = gameScene.GetNodeOrNull<Label>("UI/GameUI/InteractionPrompt");
            AssertThat(prompt).IsNotNull();

            PressMovement(playerController, Vector2I.Right);
            await AwaitInputDebounce();

            AssertThat(prompt!.Visible).IsTrue();
            AssertThat(prompt.Text).IsEqual("Use");

            PressMovement(playerController, Vector2I.Down);
            await AwaitInputDebounce();

            AssertThat(prompt.Visible).IsTrue();
            AssertThat(prompt.Text).IsEqual("Solve");
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    private async Task ReplaceWithHostedFixture()
    {
        if (_game != null && IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.Free();
            _gameManager = null;
        }

        await AwaitFrames(1);

        var hostScene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var game = new TestableGame();
        var ui = new CanvasLayer { Name = "UI" };
        ui.AddChild(new Control { Name = "GameUI" });
        ui.AddChild(hostScene.Instantiate<UIScreenHost>());
        game.AddChild(ui);

        var gameManager = new GameManager();
        SetPrivateField(game, "_gameManager", gameManager);

        _game = game;
        _gameManager = gameManager;
        _viewport!.AddChild(game);
        await AwaitFrames(2);
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < frameCount; i++)
        {
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task AwaitInputDebounce()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        await sceneTree.ToSignal(sceneTree.CreateTimer(0.15), Timer.SignalName.Timeout);
        await AwaitFrames(1);
    }

    private static void PressMovement(PlayerController controller, Vector2I direction)
    {
        controller._UnhandledInput(new InputEventKey
        {
            Keycode = DirectionToKey(direction),
            Pressed = true
        });
    }

    private static void PressInteract(PlayerController controller)
    {
        controller._UnhandledInput(new InputEventAction
        {
            Action = "interact",
            Pressed = true
        });
    }

    private static void PressInteractRelease(PlayerController controller)
    {
        controller._UnhandledInput(new InputEventAction
        {
            Action = "interact",
            Pressed = false
        });
    }

    private static Key DirectionToKey(Vector2I direction)
    {
        if (direction == Vector2I.Up) return Key.Up;
        if (direction == Vector2I.Down) return Key.Down;
        if (direction == Vector2I.Left) return Key.Left;
        if (direction == Vector2I.Right) return Key.Right;

        throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported movement direction.");
    }

    private static PuzzleRiddleSpawn CreateRuntimeRiddle(string name, string puzzleId, Vector2I gridPosition)
    {
        return new PuzzleRiddleSpawn
        {
            Name = name,
            RiddleId = name,
            PuzzleId = puzzleId,
            GridPosition = gridPosition,
            PromptText = "Four stones face the old shortcut. Which stone sleeps until the lever wakes it?",
            ChoiceIds = new Godot.Collections.Array<string> { "north_stone", "east_stone", "west_stone" },
            ChoiceLabels = new Godot.Collections.Array<string> { "North Stone", "East Stone", "West Stone" },
            CorrectChoiceId = "east_stone",
            WrongAnswerDamage = 12
        };
    }

    // ─── Critical #2: Trap status-effect path tests ────────────────────────

    [TestCase]
    public void TrapStatusEffect_ValidId_AppliesEffectToPlayer()
    {
        // GameManager._Ready() creates the player; _gameManager is set up in [Before].
        _gameManager!.EnsureFreshPlayer();
        var player = _gameManager.Player;
        player.ActiveBuffs.Clear();
        player.CurrentHealth = 50;

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_StatusTest",
            PuzzleId = "Puzzle_Status",
            StatusEffectId = "Poison",
            StatusMagnitude = 3,
            StatusTurns = 4
        };

        var method = typeof(Game).GetMethod("ApplyTrapStatusEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertThat(method).IsNotNull();
        method!.Invoke(_game, new object[] { trap });

        var effects = player.ActiveBuffs.Effects;
        AssertThat(effects.Count).IsEqual(1);
        AssertThat(effects[0].Type).IsEqual(StatusEffectType.Poison);
        AssertThat(effects[0].Magnitude).IsEqual(3);
        AssertThat(effects[0].TurnsRemaining).IsEqual(4);
    }

    [TestCase]
    public void TrapStatusEffect_InvalidId_DoesNotApplyEffect()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.ActiveBuffs.Clear();

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_BadStatus",
            PuzzleId = "Puzzle_BadStatus",
            StatusEffectId = "NotARealEffect",
            StatusMagnitude = 5,
            StatusTurns = 3
        };

        var method = typeof(Game).GetMethod("ApplyTrapStatusEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(_game, new object[] { trap });

        AssertThat(_gameManager.Player.ActiveBuffs.Effects.Count).IsEqual(0);
    }

    [TestCase]
    public void TrapStatusEffect_ZeroTurns_DoesNotApplyEffect()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.ActiveBuffs.Clear();

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_ZeroTurns",
            PuzzleId = "Puzzle_ZeroTurns",
            StatusEffectId = "Poison",
            StatusMagnitude = 3,
            StatusTurns = 0
        };

        var method = typeof(Game).GetMethod("ApplyTrapStatusEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(_game, new object[] { trap });

        AssertThat(_gameManager.Player.ActiveBuffs.Effects.Count).IsEqual(0);
    }

    [TestCase]
    public void TrapStatusEffect_EmptyId_DoesNotApplyEffect()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.ActiveBuffs.Clear();

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_EmptyStatus",
            PuzzleId = "Puzzle_EmptyStatus",
            StatusEffectId = "",
            StatusMagnitude = 2,
            StatusTurns = 3
        };

        var method = typeof(Game).GetMethod("ApplyTrapStatusEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(_game, new object[] { trap });

        AssertThat(_gameManager.Player.ActiveBuffs.Effects.Count).IsEqual(0);
    }

    // ─── Critical #3: Trap damage blocked by interaction flags ─────────────

    [TestCase]
    public void TrapDamage_BlockedWhenInBattle()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.CurrentHealth = 50;
        _gameManager.StartBattle(Enemy.CreateGoblin());

        InvokeOnTrapTileTriggered(new Vector2I(5, 5));
        AssertThat(_gameManager.Player.CurrentHealth).IsEqual(50);
    }

    [TestCase]
    public void TrapDamage_BlockedWhenInNpcInteraction()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.CurrentHealth = 50;
        _gameManager.StartNpcInteraction();

        InvokeOnTrapTileTriggered(new Vector2I(5, 5));
        AssertThat(_gameManager.Player.CurrentHealth).IsEqual(50);
    }

    [TestCase]
    public void TrapDamage_BlockedWhenInWorldInteraction()
    {
        _gameManager!.EnsureFreshPlayer();
        _gameManager.Player.CurrentHealth = 50;
        _gameManager.StartWorldInteraction();

        InvokeOnTrapTileTriggered(new Vector2I(5, 5));
        AssertThat(_gameManager.Player.CurrentHealth).IsEqual(50);
    }

    private void InvokeOnTrapTileTriggered(Vector2I position)
    {
        var method = typeof(Game).GetMethod("OnTrapTileTriggered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertThat(method).IsNotNull();
        method!.Invoke(_game, new object[] { position });
    }

    private static void AddPuzzleNode(GridMap gridMap, Node node, string groupName)
    {
        gridMap.AddChild(node);
        node.AddToGroup(groupName);
    }

    private static Button? FindButtonWithText(Node root, string text)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            var nested = FindButtonWithText(child, text);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Label? FindLabelWithText(Node root, string text)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Label label && label.Text.Contains(text))
            {
                return label;
            }

            var nested = FindLabelWithText(child, text);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = FindPrivateField(instance.GetType(), fieldName);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = FindPrivateField(instance.GetType(), fieldName);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        return (T)field.GetValue(instance)!;
    }

    private static FieldInfo? FindPrivateField(Type? type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    public partial class TestableGame : Game
    {
        public override void _Ready()
        {
        }

    }
}
