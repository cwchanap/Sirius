using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameplayPauseHostTest : Node
{
    private SubViewport? _viewport;
    private Game? _game;
    private bool _incomingTreePaused;
    private Input.MouseModeEnum _originalMouseMode;

    [BeforeTest]
    public async Task SetUp()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        _incomingTreePaused = tree.Paused;
        _originalMouseMode = Input.MouseMode;
        tree.Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360),
            GuiEmbedSubwindows = true
        };
        tree.Root.AddChild(_viewport);

        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        _game = scene.Instantiate<Game>();
        _viewport.AddChild(_game);
        await AwaitFrames(8);
    }

    [AfterTest]
    public async Task TearDown()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;

        if (_game != null && GodotObject.IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
        {
            _viewport.Free();
            _viewport = null;
        }

        await AwaitFrames(2);
        Input.MouseMode = _originalMouseMode;
        tree.Paused = _incomingTreePaused;
    }

    [TestCase]
    public void GameSceneOwnsOneReadyHost()
    {
        var hosts = new List<UIScreenHost>();
        foreach (var child in _game!.GetNode<CanvasLayer>("UI").GetChildren())
        {
            if (child is UIScreenHost host)
                hosts.Add(host);
        }

        AssertThat(hosts.Count).IsEqual(1);
        AssertThat(hosts[0].ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public void GameSceneHostPrepareForTeardown_ClosesEntryAndRestoresIncomingState()
    {
        var host = _game!.GetNodeOrNull<UIScreenHost>("UI/UIScreenHost");
        if (host == null)
        {
            AssertThat(host).IsNotNull();
            return;
        }

        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var incomingHudVisible = gameUi.Visible;
        var incomingMouseMode = Input.MouseMode;
        var disposableEntry = new Control();
        var opened = host.TryPresent(disposableEntry, new UIScreenEntrySpec
        {
            Kind = new StringName("gameplay_pause_host_teardown_test"),
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            NodeLifetime = UINodeLifetime.QueueFree
        });

        AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(gameUi.Visible).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);

        var preparation = host.PrepareForTeardown();

        AssertThat(preparation).IsEqual(UIScreenTeardownPreparationStatus.Complete);
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(gameUi.Visible).IsEqual(incomingHudVisible);
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (var index = 0; index < frameCount; index++)
            await tree.Root.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
