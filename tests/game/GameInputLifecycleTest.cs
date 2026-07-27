using GdUnit4;
using Godot;
using System;
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

    [Before]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
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

        _gameManager = new GameManager();
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
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

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public async Task BattleResultCancelIsHandledAndClosesResultWithoutOpeningPause()
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn")
            ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
        var battle = scene.Instantiate<BattleManager>();
        _game!.GetNode<CanvasLayer>("UI").AddChild(battle);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        SetPrivateField(battle, "_resultEmitted", true);
        battle.PopupCentered();
        SetPrivateField(_game, "_battleManager", battle);
        AssertThat(_gameManager!.IsInBattle).IsFalse();

        PushPauseEvent();

        AssertThat(_viewport!.IsInputHandled()).IsTrue();
        AssertThat(battle.Visible).IsFalse();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
    }

    [TestCase]
    public void PauseRestorePending_ConsumesWithoutChangingVisibleStack()
    {
        SetPrivateField(_game!, "_pauseMenuRestorePending", true);

        PushPauseEvent();

        AssertThat(_viewport!.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
    }

    [TestCase]
    public void ErrorCancel_DismissesOnlyErrorAndLeavesPauseVisible()
    {
        var pause = new PauseMenuDialog();
        var error = new AcceptDialog();
        _game!.GetNode<CanvasLayer>("UI").AddChild(pause);
        _game.GetNode<CanvasLayer>("UI").AddChild(error);
        pause.PopupCentered();
        error.PopupCentered();
        SetPrivateField(_game, "_pauseMenuDialog", pause);
        SetPrivateField(_game, "_activeErrorPopup", error);

        PushPauseEvent();

        AssertThat(_viewport!.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup")).IsNull();
        AssertThat(pause.Visible).IsTrue();
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
        var game = await InstantiateGameScene();
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
        var game = await InstantiateGameScene();
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

    private void PushPauseEvent()
    {
        _viewport!.PushInput(new InputEventAction
        {
            Action = "pause_menu",
            Pressed = true
        });
    }

    private static async Task<Game> InstantiateGameScene()
    {
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        var game = scene.Instantiate<Game>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(game);
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

    private static void InvokePrivate(object instance, string methodName)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        method.Invoke(instance, null);
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

    public partial class LifecycleGame : Game
    {
        public Action? MainMenuNavigationRequested { get; set; }
        protected override double DefeatReturnDelaySeconds => 0.01;
        protected override void ReturnToMainMenu() => MainMenuNavigationRequested?.Invoke();

        public override void _Ready()
        {
        }
    }
}
