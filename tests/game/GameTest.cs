using GdUnit4;
using Godot;
using System;
using System.Linq;
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
    public async Task ShowSaveError_WithoutHostedSaveLoadReturnsFalseAndDoesNotOpenPrompt()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        var opened = InvokePrivate<bool>(
            _game,
            "ShowSaveError",
            "Failed to save game.",
            "Save Failed");

        AssertThat(opened).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
    }

    [TestCase]
    public async Task CorruptedSave_OpensRootRecoverablePromptAndBlocksGameplayWithoutTreePause()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        InvokePrivate(_game, "ShowCorruptedSaveError");

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(_game.GetTree().Paused).IsFalse();
        AssertThat(_game.IsProcessingInput()).IsTrue();
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
    }

    [TestCase]
    public async Task CorruptedSave_PrimaryRequestsMainMenuExactlyOnce()
    {
        await ReplaceWithHostedFixture();

        InvokePrivate(_game!, "ShowCorruptedSaveError");

        var prompt = GetPrivateField<SiriusPrompt>(_game!, "_hostedPrompt");
        var primary = prompt.GetNode<Button>("%PrimaryButton");
        primary.EmitSignal(Button.SignalName.Pressed);
        primary.EmitSignal(Button.SignalName.Pressed);

        AssertThat(_game!.ReturnToMainMenuCalls).IsEqual(1);
        AssertThat(_game.GetNode<UIScreenHost>("UI/UIScreenHost")
            .IsKindActive(UIScreenKinds.Prompt)).IsFalse();
    }

    [TestCase]
    public async Task CorruptedSave_ConfiguredCancelMapsToPrimaryAndRequestsMainMenuExactlyOnce()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        InvokePrivate(_game!, "ShowCorruptedSaveError");

        var handled = host.TryHandleInput(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(handled).IsEqual(UIInputDispatchResult.Consumed);
        AssertThat(_game!.ReturnToMainMenuCalls).IsEqual(1);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
    }

    [TestCase]
    public async Task CorruptedSave_SecondDetectionDoesNotOpenSecondPrompt()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        InvokePrivate(_game!, "ShowCorruptedSaveError");
        InvokePrivate(_game!, "ShowCorruptedSaveError");

        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
            .IsEqual(1);
    }

    [TestCase]
    public async Task CorruptedSave_RootTeardownClearsPromptAndGameplayBlock()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        InvokePrivate(_game!, "ShowCorruptedSaveError");
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();

        // Exercise the real root teardown path (scene change / host finalize)
        // rather than a direct entry close.
        var preparation = host.PrepareForTeardown();

        AssertThat(preparation).IsEqual(UIScreenTeardownPreparationStatus.Complete);
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(GetPrivateField<UIScreenHandle?>(_game!, "_hostedPromptHandle")).IsNull();
        AssertThat(GetPrivateField<SiriusPrompt?>(_game!, "_hostedPrompt")).IsNull();
    }

    [TestCase]
    public async Task CorruptedSave_PresentationFailureStillReturnsToTitle()
    {
        // A fresh TestableGame without a production host: the host open attempt
        // fails and the mandatory navigation fallback must run. Built locally so
        // the test never depends on the shared fixture field.
        var game = new TestableGame();
        _viewport!.AddChild(game);
        await AwaitFrames(1);
        try
        {
            InvokePrivate(game, "ShowCorruptedSaveError");

            AssertThat(game.ReturnToMainMenuCalls).IsEqual(1);
            AssertThat(GetPrivateField<UIScreenHandle?>(game, "_hostedPromptHandle")).IsNull();
            AssertThat(GetPrivateField<SiriusPrompt?>(game, "_hostedPrompt")).IsNull();

            // The one-shot latch must not produce a second navigation on re-detection.
            InvokePrivate(game, "ShowCorruptedSaveError");
            AssertThat(game.ReturnToMainMenuCalls).IsEqual(1);
        }
        finally
        {
            if (IsInstanceValid(game))
                game.Free();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task BattleStart_HostsBlockingControlWithoutPausingTree()
    {
        var game = await InstantiateRealGameScene();
        try
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            var manager = game.GetNode<GameManager>("GameManager");
            var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

            manager.StartBattle(Enemy.CreateGoblin());
            await AwaitFrames(2);

            var battle = GetPrivateField<BattleManager>(game, "_battleManager");
            AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
            AssertThat(host.ActiveEntries.Count).IsEqual(1);
            AssertThat(battle.GetParent()).IsEqual(host.GetNode<Control>("ScreenLayer"));
            AssertThat(tree.Paused).IsFalse();
            AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();

            battle.RequestCancel();
            await AwaitFrames(2);
            AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsFalse();
            AssertThat(manager.IsInBattle).IsFalse();
        }
        finally
        {
            game.Free();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task GameSceneUsesExplorationHudWithoutPrototypeHud()
    {
        var game = await InstantiateRealGameScene();
        try
        {
            AssertThat(game.GetNodeOrNull<ExplorationHudController>(
                "UI/GameUI/ExplorationHud")).IsNotNull();

            string[] removedPaths =
            {
                "UI/GameUI/" + "TopPanel",
                "UI/GameUI/" + "Instructions",
                "UI/GameUI/" + "InteractionPrompt"
            };

            foreach (var path in removedPaths)
                AssertThat(game.GetNodeOrNull(path)).IsNull();
        }
        finally
        {
            game.Free();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task PlayerStatsChangedRefreshesExplorationHud()
    {
        var game = await InstantiateRealGameScene();
        try
        {
            var manager = game.GetNode<GameManager>("GameManager");
            var hud = game.GetNode<ExplorationHudController>(
                "UI/GameUI/ExplorationHud");

            manager.Player.CurrentHealth = 61;
            manager.Player.CurrentMana = 17;
            manager.Player.Experience = 42;
            manager.NotifyPlayerStatsChanged();
            await AwaitFrames(1);

            AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(61);
            AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(17);
            AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(42);
        }
        finally
        {
            game.Free();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task PauseHidesPromptAndResumeRestoresResolvedPrompt()
    {
        var game = await InstantiateRealGameScene();
        try
        {
            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = game.GetNode<PlayerController>("PlayerController");
            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_PausePromptTest",
                TreasureBoxId = "TreasureBox_PausePromptTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 1
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");
            SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Right);
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);
            InvokePrivate(game, "UpdateInteractionPrompt");

            var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
            var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
            var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");

            AssertThat(InvokePrivate<bool>(game, "TryOpenPause")).IsTrue();
            await AwaitFrames(2);
            AssertThat(promptPlate.Visible).IsFalse();

            var pause = GetPrivateField<PauseScreenController>(game, "_pauseScreen");
            pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");
        }
        finally
        {
            game.Free();
            await AwaitFrames(1);
        }
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

            var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
            var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
            var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");
            AssertThat(prompt.IconId).IsEqual(UiIconId.Reward);
            AssertThat(prompt.Actions).Contains(new StringName("interact"));

            int startingGold = gameManager.Player.Gold;
            int startingPotionCount = gameManager.Player.GetItemQuantity("health_potion");
            PressInteract(playerController);
            await AwaitFrames(1);

            AssertThat(promptPlate.Visible).IsFalse();

            await AwaitFrames(120);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(startingPotionCount + 1);
            AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeTest")).IsTrue();
            AssertThat(box.IsOpened).IsTrue();
            AssertThat(promptPlate.Visible).IsFalse();

            PressInteractRelease(playerController);
            PressInteract(playerController);
            await AwaitFrames(30);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(promptPlate.Visible).IsFalse();
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
    public async Task Game_OpenPuzzleRiddle_HostsScreenWithModalPolicy()
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

            var gridMap = gameScene.GetNode<FloorManager>("FloorManager").CurrentGridMap;
            var gameManager = gameScene.GetNode<GameManager>("GameManager");
            AssertThat(gridMap).IsNotNull();

            var riddle = CreateRuntimeRiddle(
                "PuzzleRiddle_HostedPolicyTest",
                "Puzzle_RuntimeHostedPolicyTest",
                new Vector2I(8, 51));
            AddPuzzleNode(gridMap, riddle, "PuzzleRiddleSpawn");
            await AwaitFrames(2);

            InvokePrivate(gameScene, "OpenPuzzleRiddle", riddle);
            await AwaitFrames(2);

            var host = gameScene.GetNode<UIScreenHost>("UI/UIScreenHost");
            var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.PuzzleRiddle);

            AssertThat(entry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
            AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
            AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
            AssertThat(entry.Policy.PauseTree).IsFalse();
            AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
            AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
            AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
            AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
            AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
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

            var host = gameScene.GetNode<UIScreenHost>("UI/UIScreenHost");
            var screen = GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen");
            AssertThat(screen).IsNotNull();

            int statsChangedCount = 0;
            gameManager.PlayerStatsChanged += () => statsChangedCount++;

            var eastStoneButton = FindButtonWithText(screen!, "East Stone");
            AssertThat(eastStoneButton).IsNotNull();
            eastStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            // The domain result lands immediately; the hosted riddle stays up
            // in Terminal presenting the success message.
            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsTrue();
            AssertThat(gate.BlocksMovement).IsFalse();
            AssertThat(freshGrid[10, 50]).IsEqual((int)GridMap.CellType.Empty);
            AssertThat(freshGrid[11, 50]).IsEqual((int)GridMap.CellType.Empty);
            AssertThat(statsChangedCount).IsEqual(1);
            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(FindLabelWithText(screen!, "gate opens")).IsNotNull();
            var continueButton = FindButtonWithText(screen!, "Continue");
            AssertThat(continueButton).IsNotNull();

            // Only dismissing the terminal result releases the world.
            continueButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
            AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen")).IsNull();
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

            var host = gameScene.GetNode<UIScreenHost>("UI/UIScreenHost");
            var screen = GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen");
            AssertThat(screen).IsNotNull();

            var northStoneButton = FindButtonWithText(screen!, "North Stone");
            AssertThat(northStoneButton).IsNotNull();
            northStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            // Wrong answer: exact domain HP loss, still unsolved, terminal
            // feedback reporting the actual loss, screen stays up until Close.
            AssertThat(gameManager.Player.CurrentHealth).IsEqual(23);
            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(FindLabelWithText(screen!, "(-7 HP)")).IsNotNull();
            var closeButton = FindButtonWithText(screen!, "Close");
            AssertThat(closeButton).IsNotNull();

            closeButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();

            // After dismissal a fresh interaction opens a new screen, and the
            // armed switch makes the retry solve.
            PressInteractRelease(playerController);
            await AwaitFrames(1);
            PressInteract(playerController);
            await AwaitFrames(3);

            var retryScreen = GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen");
            AssertThat(retryScreen).IsNotNull();
            AssertThat(ReferenceEquals(retryScreen, screen)).IsFalse();

            var eastStoneButton = FindButtonWithText(retryScreen!, "East Stone");
            AssertThat(eastStoneButton).IsNotNull();
            eastStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsTrue();
            AssertThat(FindLabelWithText(retryScreen!, "gate opens")).IsNotNull();
            retryScreen!.RequestCancel();
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
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
    public async Task Game_DormantRiddleShowsFeedbackAndRearmsHostedScreen()
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

            var host = gameScene.GetNode<UIScreenHost>("UI/UIScreenHost");
            var screen = GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen");
            AssertThat(screen).IsNotNull();
            var openedHandle = GetPrivateField<UIScreenHandle?>(gameScene, "_puzzleRiddleHandle");

            // Choose an answer before arming the switch — should show dormant message
            var eastStoneButton = FindButtonWithText(screen!, "East Stone");
            AssertThat(eastStoneButton).IsNotNull();
            eastStoneButton!.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            // Dormant: the same hosted screen and handle stay active, the
            // feedback explains why, and the world latch is untouched.
            var dormantScreen = GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen");
            AssertThat(ReferenceEquals(dormantScreen, screen)).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
            AssertThat(GetPrivateField<UIScreenHandle?>(gameScene, "_puzzleRiddleHandle")).IsEqual(openedHandle);
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsFalse();
            AssertThat(FindLabelWithText(dormantScreen!, "dormant")).IsNotNull();

            // The rearmed screen accepts another choice.
            var rearmedButton = FindButtonWithText(dormantScreen!, "East Stone");
            AssertThat(rearmedButton).IsNotNull();
            AssertThat(rearmedButton!.Disabled).IsFalse();
            rearmedButton.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(FindLabelWithText(dormantScreen!, "dormant")).IsNotNull();

            // Cancel closes the hosted riddle and releases the world.
            dormantScreen!.RequestCancel();
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
            AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(gameScene, "_puzzleRiddleScreen")).IsNull();
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

    // The shared [Before] fixture has no UI/UIScreenHost child. Arm the
    // puzzle controller so the only failing guard is the missing host —
    // the open must reject before StartWorldInteraction latches the world.
    [TestCase]
    public void Game_OpenPuzzleRiddle_WithoutHost_FailsBeforeWorldLatchStarts()
    {
        SetPrivateField(_game!, "_puzzleTrapController", new PuzzleTrapController(_gameManager!));
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_NoHostTest",
            "Puzzle_NoHostTest",
            new Vector2I(8, 51));

        InvokePrivate(_game, "OpenPuzzleRiddle", riddle);

        AssertThat(_gameManager!.IsInWorldInteraction).IsFalse();
        AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(_game, "_puzzleRiddleScreen"))
            .IsNull();
    }

    // Game-level regression of the host publication-callback contract: an
    // EffectiveStateChanged subscriber closing the committed entry during
    // publication leaves TryPresent returning Opened for a dead handle —
    // only Game's post-Opened IsActive(handle) recheck prevents retention.
    [TestCase]
    public async Task Game_OpenPuzzleRiddle_PublicationSubscriberClosesEntry_RetainsNothing()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        SetPrivateField(_game, "_puzzleTrapController", new PuzzleTrapController(_gameManager!));
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_PublicationCloseTest",
            "Puzzle_PublicationCloseTest",
            new Vector2I(8, 51));

        void CloseRiddleDuringPublication(UIScreenEffectiveState _)
        {
            var entry = host.ActiveEntries
                .FirstOrDefault(e => e.Policy.Kind == UIScreenKinds.PuzzleRiddle);
            if (entry != null)
                host.TryClose(entry.Handle, UIScreenCloseReason.Programmatic);
        }

        host.EffectiveStateChanged += CloseRiddleDuringPublication;
        try
        {
            InvokePrivate(_game, "OpenPuzzleRiddle", riddle);
        }
        finally
        {
            host.EffectiveStateChanged -= CloseRiddleDuringPublication;
        }

        AssertThat(_gameManager!.IsInWorldInteraction).IsFalse();
        AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(_game, "_puzzleRiddleScreen"))
            .IsNull();
        AssertThat(GetPrivateField<UIScreenHandle?>(_game, "_puzzleRiddleHandle"))
            .IsNull();
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
    }

    // A throwing EffectiveStateChanged handler escapes TryPresent after the
    // candidate was committed. Game's catch frees the still-valid candidate
    // (its deferred deletion drives the host's NodeFreed cleanup) and ends
    // the world latch, so the publication failure cannot strand the world.
    [TestCase]
    public async Task Game_OpenPuzzleRiddle_PublicationException_DoesNotStrandWorldInteraction()
    {
        await ReplaceWithHostedFixture();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        SetPrivateField(_game, "_puzzleTrapController", new PuzzleTrapController(_gameManager!));
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_PublicationFailureTest",
            "Puzzle_PublicationFailureTest",
            new Vector2I(8, 51));

        Action<UIScreenEffectiveState> throwing = _ =>
            throw new InvalidOperationException("fixture publication failure");

        host.EffectiveStateChanged += throwing;
        try
        {
            InvokePrivate(_game, "OpenPuzzleRiddle", riddle);
        }
        finally
        {
            host.EffectiveStateChanged -= throwing;
        }

        await AwaitFrames(2);

        AssertThat(_gameManager!.IsInWorldInteraction).IsFalse();
        AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(_game, "_puzzleRiddleScreen"))
            .IsNull();
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
    }

    // Root teardown must end an active riddle world-interaction before the
    // owner exits. The fixture's Free() invokes _ExitTree() again, so the
    // production cleanup has to stay idempotent.
    [TestCase]
    public async Task Game_RootTeardown_EndsHostedRiddleWorldLatch()
    {
        var game = await InstantiateRealGameScene();
        try
        {
            var gameManager = game.GetNode<GameManager>("GameManager");
            var riddle = CreateRuntimeRiddle(
                "PuzzleRiddle_RootTeardownTest",
                "Puzzle_RootTeardownTest",
                new Vector2I(8, 51));

            InvokePrivate(game, "OpenPuzzleRiddle", riddle);
            AssertThat(gameManager.IsInWorldInteraction).IsTrue();

            game._ExitTree();

            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
            AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(game, "_puzzleRiddleScreen"))
                .IsNull();
        }
        finally
        {
            game.Free();
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

            var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
            var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
            var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");

            PressMovement(playerController, Vector2I.Right);
            await AwaitInputDebounce();

            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Use");
            AssertThat(prompt.IconId).IsEqual(UiIconId.Puzzle);

            PressMovement(playerController, Vector2I.Down);
            await AwaitInputDebounce();

            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Solve");
            AssertThat(prompt.IconId).IsEqual(UiIconId.Puzzle);
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

    private static async Task<Game> InstantiateRealGameScene()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        var game = scene.Instantiate<Game>();
        sceneTree.Root.AddChild(game);
        await AwaitFrames(6);
        return game;
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

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] arguments)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName);
        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);

        return (T)method.Invoke(instance, arguments)!;
    }

    private static void InvokePrivate(object instance, string methodName, params object?[] arguments)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName);
        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);

        method.Invoke(instance, arguments);
    }

    private static MethodInfo? FindPrivateMethod(Type? type, string methodName)
    {
        while (type != null)
        {
            var method = type.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
                return method;

            type = type.BaseType;
        }

        return null;
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
        public int ReturnToMainMenuCalls { get; private set; }

        protected override void ReturnToMainMenu()
        {
            ReturnToMainMenuCalls++;
        }

        public override void _Ready()
        {
        }

    }
}
