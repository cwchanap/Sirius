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
        // Reset interaction/battle state before freeing to prevent signal
        // callbacks from firing on half-freed nodes during teardown.
        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            if (_gameManager.IsInNpcInteraction) _gameManager.EndNpcInteraction();
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

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    // NOTE: This test must run BEFORE any test that does NOT consume input.
    // Test ordering matters because the SubViewport's IsInputHandled flag
    // appears to be affected by prior tests that push input events without
    // calling SetInputAsHandled.  GdUnit4 executes test methods in declaration
    // order, so battle tests are placed first.
    [TestCase]
    public void PauseMenu_WhenInBattleWithDialog_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", new BattleManager());

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInBattleFallback_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", null);

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInNpcInteraction_DoesNotConsumeInput()
    {
        _gameManager!.StartNpcInteraction();

        PushPauseEvent();

        // Input must NOT be marked as handled — AcceptDialog-based NPC modals
        // (DialogueDialog, ShopDialog, HealDialog) need ESC to reach them so
        // they can emit Canceled / CloseRequested and dismiss themselves.
        AssertThat(_viewport!.IsInputHandled()).IsFalse();
    }

    private void PushPauseEvent()
    {
        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);
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
