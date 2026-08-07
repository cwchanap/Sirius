using System;
using System.Collections.Generic;
using System.Reflection;
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

    [TestCase]
    public void RuntimeGridMapDoesNotRemainExplicitAlways()
    {
        var grid = _game!.GetNode<FloorManager>("FloorManager").CurrentGridMap;

        AssertThat(grid.ProcessMode).IsNotEqual(Node.ProcessModeEnum.Always);
    }

    [TestCase]
    public async Task DirectInventory_HostsDetachesAndReusesTheExternalView()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(tree.Paused).IsFalse();

        _viewport!.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
        AssertThat(inventory.GetParent()).IsEqual(modalLayer);
        AssertThat(inventory.Visible).IsTrue();
        AssertThat(tree.Paused).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);

        var entry = host.ActiveEntries[0];
        AssertThat(entry.Policy.Parent).IsNull();
        AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(entry.Policy.PauseTree).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsFalse();
        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(tree.Paused).IsFalse();

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
        AssertThat(inventory.GetParent()).IsEqual(modalLayer);
        AssertThat(inventory.Visible).IsTrue();

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(inventory.GetParent()).IsNull();
    }

    [TestCase]
    public async Task PauseParity_HostsOneAlwaysProcessingViewAndResumeRestoresIncomingState()
    {
        // This catches a hosted Pause path that either takes tree-pause ownership,
        // skips the host policy effects, or leaves its view/entry behind after Resume.
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var incomingHudVisible = gameUi.Visible;
        var incomingMouseMode = Input.MouseMode;

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(pause.GetParent()).IsEqual(modalLayer);
        AssertThat(tree.Paused).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);
        AssertThat(gameUi.Visible).IsTrue();

        var entry = host.ActiveEntries[0];
        AssertThat(entry.Policy.Kind).IsEqual(UIScreenKinds.Pause);
        AssertThat(entry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(entry.Policy.PauseTree).IsFalse();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Visible);
        AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
        AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(GetPrivateField<PauseScreenController>(_game, "_pauseScreen"))
            .IsEqual(pause);

        pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
        AssertThat(gameUi.Visible).IsEqual(incomingHudVisible);
        AssertThat(GetPrivateField<PauseScreenController?>(_game, "_pauseScreen")).IsNull();
        AssertThat(GodotObject.IsInstanceValid(pause)).IsFalse();
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (var index = 0; index < frameCount; index++)
            await tree.Root.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new MissingFieldException(instance.GetType().FullName, fieldName);

        return (T)field.GetValue(instance)!;
    }

    private static bool InvokePrivateBool(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);

        if (method.Invoke(instance, null) is bool result)
            return result;

        throw new InvalidOperationException($"Method '{methodName}' did not return bool.");
    }
}
