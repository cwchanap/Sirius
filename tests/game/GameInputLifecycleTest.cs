using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameInputLifecycleTest : Node
{
    private LifecycleGame? _game;
    private SubViewport? _viewport;
    private GameManager? _gameManager;
    private Game? _realGame;
    private readonly Dictionary<string, InputActionSnapshot> _inputActionSnapshots = new();
    private readonly Dictionary<int, float> _audioBusVolumes = new();
    private bool _treeWasPaused;
    private int _audioBusCount;
    private DisplayServer.WindowMode _simulatedWindowMode;
    private Vector2I _simulatedWindowSize;
    private Action<DisplayServer.WindowMode>? _previousWindowSetModeOverride;
    private Action<Vector2I>? _previousWindowSetSizeOverride;
    private Func<DisplayServer.WindowMode>? _previousWindowGetModeOverride;
    private Func<Vector2I>? _previousWindowGetSizeOverride;
    private Action<string, string>? _previousFileWriteTextOverride;
    private Action<string, string, bool>? _previousFileMoveWithOverwriteOverride;
    private Action<string, string>? _previousFileMoveOverride;
    private Action<string>? _previousFileDeleteOverride;

    [BeforeTest]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        _treeWasPaused = sceneTree.Paused;
        sceneTree.Paused = false;
        CaptureInputActions("toggle_inventory", "interact", "pause_menu", "ui_cancel", "ui_close_dialog");
        CaptureAudioState();
        CaptureAndInstallSettingsOverrides();

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360)
        };
        sceneTree.Root.AddChild(_viewport);

        _game = new LifecycleGame();
        _viewport.AddChild(_game);
        _game.AddChild(new CanvasLayer { Name = "UI" });

        _gameManager = new LifecycleGameManager();
        _game.AddChild(_gameManager);
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Paused = false;

        if (_realGame != null && IsInstanceValid(_realGame))
        {
            _realGame.Free();
            _realGame = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            if (_gameManager.IsInNpcInteraction) _gameManager.EndNpcInteraction();
            if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
            if (_gameManager.IsInBattle) _gameManager.EndBattle(false);
        }

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

        await AwaitFrames(2);
        RestoreInputActions();
        RestoreAudioState();
        RestoreSettingsOverrides();
        sceneTree.Paused = _treeWasPaused;
    }
    [TestCase]
    public async Task ConfiguredKeyboardCancel_BattleResultClosesNativeDialogWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        var scene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn")
            ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
        var battle = scene.Instantiate<BattleManager>();
        _game!.GetNode<CanvasLayer>("UI").AddChild(battle);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        SetPrivateField(battle, "_resultEmitted", true);
        battle.PopupCentered();
        SetPrivateField(_game, "_battleManager", battle);
        AssertThat(_gameManager!.IsInBattle).IsFalse();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(GodotObject.IsInstanceValid(battle)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_ErrorDismissesBeforeOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        var error = new AcceptDialog();
        SetPrivateField(_game!, "_activeErrorPopup", error);

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup")).IsNull();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_BattleWithoutDialogEndsBattleWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        _gameManager!.StartBattle(Enemy.CreateGoblin());

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(_gameManager.IsInBattle).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_WorldInteractionConsumesWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        _gameManager!.StartWorldInteraction();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(_gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        _gameManager!.StartNpcInteraction();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsFalse();
            AssertThat(_gameManager.IsInNpcInteraction).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_ClosesAcceptDialogExactlyOnce()
    {
        ConfigureCancelBindings(Key.P);
        _viewport!.GuiEmbedSubwindows = true;
        var dialog = new AcceptDialog();
        int canceledCount = 0;
        dialog.Canceled += () => canceledCount++;
        _viewport.AddChild(dialog);
        await AwaitFrames(1);
        try
        {
            dialog.PopupCentered();
            await AwaitFrames(1);
            AssertThat(_viewport.GetEmbeddedSubwindows().Count).IsEqual(1);

            PushPhysicalKey(Key.P);
            await AwaitFrames(1);
            PushPhysicalKey(Key.P);

            AssertThat(canceledCount).IsEqual(1);
            AssertThat(dialog.Visible).IsFalse();
        }
        finally
        {
            if (IsInstanceValid(dialog))
            {
                dialog.Free();
            }
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardPauseMenu_OpensHostedPauseThenResumesTreeOnSecondPhysicalAction()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        var pressedEvent = new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("pause_menu")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        await AwaitFrames(1);

        try
        {
            AssertThat(_viewport.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
            AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
            AssertThat(host.ActiveEntries.Count).IsEqual(1);
            AssertThat(host.ActiveEntries[0].Policy.ProcessPolicy)
                .IsEqual(UIProcessPolicy.WhenPaused);
            AssertThat(host.ActiveEntries[0].Policy.PauseTree).IsTrue();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public async Task ConfiguredControllerUiCancel_OpensHostedPauseThenResumesTreeOnSecondPhysicalAction()
    {
        var controllerButton = JoyButton.B;
        ConfigureCancelBindings(Key.P, new InputEventJoypadButton
        {
            ButtonIndex = controllerButton
        });
        await ReplaceWithHostedLifecycleFixture();

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        var pressedEvent = new InputEventJoypadButton
        {
            ButtonIndex = controllerButton,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        await AwaitFrames(1);

        try
        {
            AssertThat(_viewport.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
            AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
            AssertThat(host.ActiveEntries.Count).IsEqual(1);
            AssertThat(host.ActiveEntries[0].Policy.ProcessPolicy)
                .IsEqual(UIProcessPolicy.WhenPaused);
            AssertThat(host.ActiveEntries[0].Policy.PauseTree).IsTrue();
        }
        finally
        {
            ReleasePhysicalJoypadButton(controllerButton);
        }

        PushPhysicalJoypadButton(controllerButton);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_SettingsKeyCaptureAndPopupKeepHostedSettingsForNativeHandlers()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(host.GetNode<Control>("ModalLayer"));
        GetPrivateField<Button>(settings, "_inventoryKeyBtn")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(settings.IsRebinding).IsTrue();
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(settings.IsRebinding).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();

        var resolution = GetPrivateField<OptionButton>(settings, "_resolutionOption");
        resolution.ShowPopup();
        await AwaitFrames(1);

        AssertThat(settings.IsPopupOpen).IsTrue();
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        // The host reserves this configured event for the native popup rather
        // than closing the Settings entry or its logical Pause parent.
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        resolution.GetPopup().Hide();
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_SaveLoadOverwriteDismissesChildThenClosesHostedChild()
    {
        ConfigureCancelBindings(Key.P);
        _viewport!.GuiEmbedSubwindows = true;
        await ReplaceWithHostedLifecycleFixture();

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveLoad = FindDirectChild<SaveLoadDialog>(host);
        var slots = GetPrivateField<SaveSlotInfo[]>(saveLoad, "_slotInfos");
        slots[0] = new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 };
        InvokePrivate(saveLoad, "OnSlotPressed", 0);
        await AwaitFrames(1);

        AssertThat(saveLoad.HasActiveChildDialog).IsTrue();
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(saveLoad.HasActiveChildDialog).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();

        PushPhysicalKey(Key.P);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(saveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
    }

    [TestCase]
    public async Task ConfiguredControllerCancel_ClosesSaveLoadDialogExactlyOnce()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        _viewport!.GuiEmbedSubwindows = true;
        var dialog = new SaveLoadDialog();
        int closedCount = 0;
        dialog.DialogClosed += () => closedCount++;
        _viewport.AddChild(dialog);
        await AwaitFrames(1);
        try
        {
            dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
            await AwaitFrames(1);
            AssertThat(_viewport.GetEmbeddedSubwindows().Count).IsEqual(1);

            PushPhysicalJoypadButton((JoyButton)10);
            await AwaitFrames(1);
            PushPhysicalJoypadButton((JoyButton)10);

            AssertThat(closedCount).IsEqual(1);
            AssertThat(dialog.Visible).IsFalse();
        }
        finally
        {
            if (IsInstanceValid(dialog))
            {
                dialog.Free();
            }
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_DeclinesToTopmostRiddleWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        _viewport!.GuiEmbedSubwindows = true;
        await FreeLifecycleFixture();
        _realGame = await InstantiateGameScene(_viewport);

        var floorManager = _realGame.GetNode<FloorManager>("FloorManager");
        var gridMap = floorManager.CurrentGridMap;
        var playerController = _realGame.GetNode<PlayerController>("PlayerController");
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_ConfiguredCancelTest",
            "Puzzle_ConfiguredCancelTest",
            new Vector2I(8, 51));
        gridMap.AddChild(riddle);
        riddle.AddToGroup("PuzzleRiddleSpawn");
        SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
        SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Down);
        gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
        await AwaitFrames(3);
        InvokePrivate(_realGame, "UpdateInteractionPrompt");

        var prompt = _realGame.GetNode<Label>("UI/GameUI/InteractionPrompt");
        AssertThat(prompt.Visible).IsTrue();
        AssertThat(prompt.Text).IsEqual("Solve");

        InvokePrivate(_realGame, "OpenPuzzleRiddle", riddle);
        var dialog = GetPrivateField<PuzzleRiddleDialog>(_realGame, "_puzzleRiddleDialog");
        int closedCount = 0;
        dialog.PuzzleRiddleClosed += () => closedCount++;
        AssertThat(gameManager.IsInWorldInteraction).IsTrue();
        AssertThat(prompt.Visible).IsFalse();
        await AwaitFrames(1);
        AssertThat(_viewport!.GetEmbeddedSubwindows().Count).IsEqual(1);

        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = true
        });
        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = false
        });
        await AwaitFrames(2);

        AssertThat(closedCount).IsEqual(1);
        AssertThat(GetPrivateField<PuzzleRiddleDialog?>(_realGame, "_puzzleRiddleDialog")).IsNull();
        AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        AssertThat(prompt.Visible).IsTrue();
        AssertThat(_realGame.GetNode<UIScreenHost>("UI/UIScreenHost")
            .IsKindActive(UIScreenKinds.Pause)).IsFalse();
    }

    [TestCase]
    public async Task DefeatReturnTimerIsOwnedAndDoesNotNavigateAfterCleanup()
    {
        int navigations = 0;
        var game = new LifecycleGame
        {
            MainMenuNavigationRequested = () => navigations++
        };
        _viewport!.AddChild(game);
        InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");

        game.Free();
        await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
            SceneTreeTimer.SignalName.Timeout);

        AssertThat(navigations).IsEqual(0);
    }

    [TestCase]
    public async Task DefeatReturnTimer_NavigatesOnceWhileOwnerLives()
    {
        int navigations = 0;
        var game = new LifecycleGame
        {
            MainMenuNavigationRequested = () => navigations++
        };
        _viewport!.AddChild(game);
        try
        {
            InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");
            InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");

            await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
                SceneTreeTimer.SignalName.Timeout);

            AssertThat(navigations).IsEqual(1);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(game))
            {
                game.Free();
            }
        }
    }

    [TestCase]
    public async Task FloorReplacement_RebindsGridAndRefreshesPrompt()
    {
        var game = await InstantiateGameScene(_viewport!);
        try
        {
            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var originalGrid = floorManager.CurrentGridMap;
            ulong originalGridId = originalGrid.GetInstanceId();
            var prompt = game.GetNode<Label>("UI/GameUI/InteractionPrompt");
            prompt.Visible = true;
            AssertThat(prompt.Visible).IsTrue();

            AssertThat(floorManager.LoadFloor(1)).IsTrue();
            await AwaitFrames(8);

            AssertThat(floorManager.CurrentGridMap.GetInstanceId()).IsNotEqual(originalGridId);
            AssertThat(GetPrivateField<GridMap>(game, "_gridMap"))
                .IsEqual(floorManager.CurrentGridMap);
            AssertThat(prompt.Visible).IsFalse();
        }
        finally
        {
            await FreeGameScene(game);
        }
    }

    [TestCase]
    public async Task InteractionPrompt_HidesDuringBattleAndRestoresAfterEscape()
    {
        var game = await InstantiateGameScene(_viewport!);
        try
        {
            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = game.GetNode<PlayerController>("PlayerController");
            var gameManager = game.GetNode<GameManager>("GameManager");
            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_BattlePromptTest",
                TreasureBoxId = "TreasureBox_BattlePromptTest",
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

            var prompt = game.GetNode<Label>("UI/GameUI/InteractionPrompt");
            AssertThat(prompt.Visible).IsTrue();

            gameManager.StartBattle(Enemy.CreateGoblin());

            AssertThat(prompt.Visible).IsFalse();
            var battle = GetPrivateField<BattleManager>(game, "_battleManager");
            AssertThat(GodotObject.IsInstanceValid(battle)).IsTrue();

            battle.ForceCloseAsEscape();

            AssertThat(prompt.Visible).IsTrue();
        }
        finally
        {
            await FreeGameScene(game);
        }
    }

    private static T FindDirectChild<T>(Node parent) where T : Node
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T typed)
                return typed;
        }

        throw new InvalidOperationException($"Direct child '{typeof(T).Name}' was not found.");
    }

    private void ConfigureCancelBindings(Key pauseKey, InputEvent? controllerBinding = null)
    {
        if (controllerBinding != null)
        {
            EnsureInputAction("ui_cancel");
            InputMap.ActionAddEvent("ui_cancel", controllerBinding);
        }

        var settingsManager = new SettingsManager();
        try
        {
            var candidate = settingsManager.GetSnapshot();
            candidate.PrimaryKeybindings["pause_menu"] = (long)pauseKey;

            AssertThat(settingsManager.ApplyAndSave(candidate)).IsTrue();
        }
        finally
        {
            settingsManager.Free();
        }
    }

    private void PushPhysicalKey(Key physicalKey)
    {
        PushPhysicalKeyDown(physicalKey);
        ReleasePhysicalKey(physicalKey);
    }

    private void PushPhysicalKeyDown(Key physicalKey)
    {
        var pressedEvent = new InputEventKey
        {
            PhysicalKeycode = physicalKey,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("pause_menu")).IsTrue();
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        AssertThat(pressedEvent.IsActionPressed("ui_close_dialog")).IsTrue();
        _viewport!.PushInput(pressedEvent);
    }

    private void ReleasePhysicalKey(Key physicalKey)
    {
        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = physicalKey,
            Pressed = false
        });
    }

    private void PushPhysicalJoypadButton(JoyButton button)
    {
        var pressedEvent = new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        AssertThat(pressedEvent.IsActionPressed("ui_close_dialog")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        ReleasePhysicalJoypadButton(button);
    }

    private void ReleasePhysicalJoypadButton(JoyButton button)
    {
        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = false
        });
    }

    private async Task FreeLifecycleFixture()
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

        await AwaitFrames(2);
    }

    private async Task ReplaceWithHostedLifecycleFixture()
    {
        await FreeLifecycleFixture();

        var hostScene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var game = new LifecycleGame();
        var ui = new CanvasLayer { Name = "UI" };
        ui.AddChild(new Control { Name = "GameUI" });
        ui.AddChild(hostScene.Instantiate<UIScreenHost>());
        game.AddChild(ui);

        var gameManager = new LifecycleGameManager();
        game.AddChild(gameManager);
        SetPrivateField(game, "_gameManager", gameManager);

        _game = game;
        _gameManager = gameManager;
        _viewport!.AddChild(game);
        await AwaitFrames(2);
    }

    private static async Task<Game> InstantiateGameScene(Node parent)
    {
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        var game = scene.Instantiate<Game>();
        parent.AddChild(game);
        await AwaitFrames(8);
        return game;
    }

    private static async Task FreeGameScene(Game game)
    {
        if (GodotObject.IsInstanceValid(game))
        {
            game.Free();
        }

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

    private static void InvokePrivate(object instance, string methodName, params object?[] arguments)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        method.Invoke(instance, arguments);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = FindPrivateField(instance.GetType(), fieldName)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = FindPrivateField(instance.GetType(), fieldName)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
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

    private static MethodInfo? FindPrivateMethod(Type? type, string methodName)
    {
        while (type != null)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }

    private void CaptureInputActions(params string[] actionNames)
    {
        _inputActionSnapshots.Clear();
        foreach (var actionName in actionNames)
        {
            var snapshot = new InputActionSnapshot
            {
                Existed = InputMap.HasAction(actionName),
                Deadzone = InputMap.HasAction(actionName)
                    ? InputMap.ActionGetDeadzone(actionName)
                    : 0.5f
            };

            if (snapshot.Existed)
            {
                foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
                {
                    snapshot.Events.Add((InputEvent)inputEvent.Duplicate());
                }
            }

            _inputActionSnapshots[actionName] = snapshot;
        }
    }

    private void RestoreInputActions()
    {
        foreach (var (actionName, snapshot) in _inputActionSnapshots)
        {
            if (!snapshot.Existed)
            {
                if (InputMap.HasAction(actionName))
                {
                    InputMap.EraseAction(actionName);
                }
                continue;
            }

            EnsureInputAction(actionName);
            InputMap.ActionSetDeadzone(actionName, snapshot.Deadzone);
            foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
            {
                InputMap.ActionEraseEvent(actionName, inputEvent);
            }
            foreach (var inputEvent in snapshot.Events)
            {
                InputMap.ActionAddEvent(actionName, (InputEvent)inputEvent.Duplicate());
            }
        }

        _inputActionSnapshots.Clear();
    }

    private static void EnsureInputAction(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }
    }

    private void CaptureAudioState()
    {
        _audioBusCount = AudioServer.BusCount;
        _audioBusVolumes.Clear();
        for (int i = 0; i < _audioBusCount; i++)
        {
            _audioBusVolumes[i] = AudioServer.GetBusVolumeDb(i);
        }
    }

    private void RestoreAudioState()
    {
        while (AudioServer.BusCount > _audioBusCount)
        {
            AudioServer.RemoveBus(AudioServer.BusCount - 1);
        }

        foreach (var (busIndex, volumeDb) in _audioBusVolumes)
        {
            if (busIndex < AudioServer.BusCount)
            {
                AudioServer.SetBusVolumeDb(busIndex, volumeDb);
            }
        }
    }

    private void CaptureAndInstallSettingsOverrides()
    {
        _previousWindowSetModeOverride = SettingsManager.WindowSetModeOverride;
        _previousWindowSetSizeOverride = SettingsManager.WindowSetSizeOverride;
        _previousWindowGetModeOverride = SettingsManager.WindowGetModeOverride;
        _previousWindowGetSizeOverride = SettingsManager.WindowGetSizeOverride;
        _previousFileWriteTextOverride = SettingsManager.FileWriteTextOverride;
        _previousFileMoveWithOverwriteOverride = SettingsManager.FileMoveWithOverwriteOverride;
        _previousFileMoveOverride = SettingsManager.FileMoveOverride;
        _previousFileDeleteOverride = SettingsManager.FileDeleteOverride;

        _simulatedWindowMode = DisplayServer.WindowGetMode();
        _simulatedWindowSize = DisplayServer.WindowGetSize();
        SettingsManager.WindowSetModeOverride = mode => _simulatedWindowMode = mode;
        SettingsManager.WindowSetSizeOverride = size => _simulatedWindowSize = size;
        SettingsManager.WindowGetModeOverride = () => _simulatedWindowMode;
        SettingsManager.WindowGetSizeOverride = () => _simulatedWindowSize;
        SettingsManager.FileWriteTextOverride = (_, _) => { };
        SettingsManager.FileMoveWithOverwriteOverride = (_, _, _) => { };
        SettingsManager.FileMoveOverride = (_, _) => { };
        SettingsManager.FileDeleteOverride = _ => { };
    }

    private void RestoreSettingsOverrides()
    {
        SettingsManager.WindowSetModeOverride = _previousWindowSetModeOverride;
        SettingsManager.WindowSetSizeOverride = _previousWindowSetSizeOverride;
        SettingsManager.WindowGetModeOverride = _previousWindowGetModeOverride;
        SettingsManager.WindowGetSizeOverride = _previousWindowGetSizeOverride;
        SettingsManager.FileWriteTextOverride = _previousFileWriteTextOverride;
        SettingsManager.FileMoveWithOverwriteOverride = _previousFileMoveWithOverwriteOverride;
        SettingsManager.FileMoveOverride = _previousFileMoveOverride;
        SettingsManager.FileDeleteOverride = _previousFileDeleteOverride;
    }

    private static PuzzleRiddleSpawn CreateRuntimeRiddle(
        string name,
        string puzzleId,
        Vector2I gridPosition)
    {
        return new PuzzleRiddleSpawn
        {
            Name = name,
            RiddleId = name,
            PuzzleId = puzzleId,
            GridPosition = gridPosition,
            PromptText = "Which stone opens the old gate?",
            ChoiceIds = new Godot.Collections.Array<string> { "east_stone" },
            ChoiceLabels = new Godot.Collections.Array<string> { "East Stone" },
            CorrectChoiceId = "east_stone",
            WrongAnswerDamage = 12
        };
    }

    private sealed class InputActionSnapshot
    {
        public bool Existed { get; init; }
        public float Deadzone { get; init; }
        public List<InputEvent> Events { get; } = new();
    }

    public partial class LifecycleGame : Game
    {
        public Action? MainMenuNavigationRequested { get; set; }
        protected override double DefeatReturnDelaySeconds => 0.01;
        protected override void ReturnToMainMenu() => MainMenuNavigationRequested?.Invoke();

        public override void _Ready()
        {
        }
    }

    public partial class LifecycleGameManager : GameManager
    {
        public override void _Ready()
        {
        }
    }
}
