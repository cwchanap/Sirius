using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameInputHandlingTest : Node
{
    private TestableGame? _game;
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

        _game = new TestableGame();
        _viewport.AddChild(_game);

        _gameManager = new GameManager();
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_game != null && IsInstanceValid(_game))
        {
            _game.QueueFree();
        }

        if (_viewport != null && IsInstanceValid(_viewport))
        {
            _viewport.QueueFree();
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.Free();
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void PauseMenu_WhenInNpcInteraction_MarksInputAsHandled()
    {
        _gameManager!.StartNpcInteraction();

        _game!._Input(CreatePauseEvent());

        AssertThat(_game.GetViewport().IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInBattleWithDialog_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", new BattleManager());

        _game!._Input(CreatePauseEvent());

        AssertThat(_game.GetViewport().IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInBattleFallback_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", null);

        _game!._Input(CreatePauseEvent());

        AssertThat(_game.GetViewport().IsInputHandled()).IsTrue();
    }

    private static InputEventAction CreatePauseEvent()
    {
        return new InputEventAction
        {
            Action = "pause_menu",
            Pressed = true
        };
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

    private partial class TestableGame : Game
    {
        public override void _Ready()
        {
        }
    }
}
