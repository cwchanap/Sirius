using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public static class UIScreenHostTestSupport
{
    public static async Task<HostFixture> CreateHost(
        Node owner,
        IEnumerable<StringName>? coreActions = null,
        UIScreenHostOptions? options = null)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var host = scene.Instantiate<UIScreenHost>();
        var sceneHudRoot = host.GetNode<Control>("HUDLayer");
        var configuredCoreActions = coreActions == null
            ? new HashSet<StringName>()
            : new HashSet<StringName>(coreActions);
        var configuredOptions = options ?? new UIScreenHostOptions
        {
            HudRoot = sceneHudRoot
        };
        if (configuredOptions.CoreCancelActions is null ||
            configuredOptions.CoreCancelActions.Count == 0)
        {
            configuredOptions = configuredOptions with
            {
                CoreCancelActions = configuredCoreActions
            };
        }
        host.Configure(configuredOptions);
        var tree = (SceneTree)Engine.GetMainLoop();
        var hostParent = owner.IsInsideTree() ? owner : tree.Root;
        hostParent.AddChild(host);
        var fixture = new HostFixture(
            host,
            configuredOptions.HudRoot ?? sceneHudRoot,
            coreActions);
        await owner.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        return fixture;
    }

    public static UIScreenEntrySpec Spec(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        LowerLayers = UILowerLayerPolicy.VisibleInteractive
    };

    public static UIScreenEntryPolicy Policy(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        ExclusiveGroup = UIScreenExclusiveGroups.None,
        IncompatibleKinds = EmptyStringNameSet.Value,
        LowerLayers = UILowerLayerPolicy.VisibleInteractive,
        EntryCancelActions = EmptyStringNameSet.Value
    };

    public static UIScreenEntrySnapshot Snapshot(
        UIScreenHandle handle,
        UIScreenEntryPolicy policy,
        long sequence) => new(handle, policy, sequence);

    public static IReadOnlyList<UIScreenEntrySnapshot> Snapshots(
        params UIScreenEntrySnapshot[] entries) => entries;

    public static InputEventAction ActionPress(StringName action) => new()
    {
        Action = action,
        Pressed = true
    };

    public static InputEventKey EscapeBoundTo(
        HostFixture fixture,
        params StringName[] actions)
    {
        var binding = new InputEventKey { PhysicalKeycode = Key.Escape };
        foreach (var action in actions)
            fixture.BindAction(action, binding);

        return new InputEventKey
        {
            PhysicalKeycode = Key.Escape,
            Pressed = true
        };
    }
}

public sealed class HostFixture : IDisposable
{
    private readonly SceneTree _tree;
    private readonly Viewport _viewport;
    private readonly bool _incomingPaused;
    private readonly Input.MouseModeEnum _incomingMouseMode;
    private readonly bool _incomingEmbedSubwindows;
    private readonly bool _incomingHudVisible;
    private readonly Dictionary<StringName, InputActionSnapshot> _inputActions = new();
    private readonly List<Node> _trackedViews = new();
    private bool _disposed;
    private bool _environmentRestored;

    internal HostFixture(
        UIScreenHost host,
        Control hudRoot,
        IEnumerable<StringName>? coreActions)
    {
        Host = host;
        _tree = host.GetTree();
        _viewport = host.GetViewport();
        HudRoot = hudRoot;
        _incomingPaused = _tree.Paused;
        _incomingMouseMode = Input.MouseMode;
        _incomingEmbedSubwindows = _viewport.GuiEmbedSubwindows;
        _incomingHudVisible = HudRoot.Visible;

        if (coreActions == null)
            return;

        foreach (var action in coreActions)
        {
            if (_inputActions.ContainsKey(action))
                continue;

            _inputActions[action] = InputActionSnapshot.Capture(action);
            if (!InputMap.HasAction(action))
                InputMap.AddAction(action);
        }
    }

    public UIScreenHost Host { get; }
    public Control HudRoot { get; }
    public Viewport Viewport => _viewport;

    public T Track<T>(T view) where T : Node
    {
        _trackedViews.Add(view);
        return view;
    }

    internal void BindAction(StringName action, InputEvent inputEvent)
    {
        if (!_inputActions.ContainsKey(action))
            _inputActions[action] = InputActionSnapshot.Capture(action);
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        _inputActions[action].Inject(action, inputEvent);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var view in _trackedViews)
        {
            if (GodotObject.IsInstanceValid(view) && !view.IsQueuedForDeletion())
                view.QueueFree();
        }

        if (GodotObject.IsInstanceValid(Host) && Host.IsInsideTree())
        {
            Host.TreeExited += RestoreEnvironment;
            if (!Host.IsQueuedForDeletion())
                Host.QueueFree();
            if (GodotObject.IsInstanceValid(Host) && !Host.IsQueuedForDeletion())
            {
                Host.TreeExited -= RestoreEnvironment;
                RestoreEnvironment();
            }
            return;
        }

        if (GodotObject.IsInstanceValid(Host) && !Host.IsQueuedForDeletion())
            Host.QueueFree();
        RestoreEnvironment();
    }

    private void RestoreEnvironment()
    {
        if (_environmentRestored)
            return;

        _environmentRestored = true;
        foreach (var (action, snapshot) in _inputActions)
            snapshot.Restore(action);

        if (GodotObject.IsInstanceValid(HudRoot))
            HudRoot.Visible = _incomingHudVisible;
        if (GodotObject.IsInstanceValid(_viewport))
            _viewport.GuiEmbedSubwindows = _incomingEmbedSubwindows;

        Input.MouseMode = _incomingMouseMode;
        _tree.Paused = _incomingPaused;
    }

    private sealed class InputActionSnapshot
    {
        private readonly bool _existed;
        private readonly HashSet<ulong> _injectedEventInstanceIds = new();

        private InputActionSnapshot(bool existed) => _existed = existed;

        public static InputActionSnapshot Capture(StringName action)
            => new(InputMap.HasAction(action));

        public void Inject(StringName action, InputEvent inputEvent)
        {
            if (InputMap.ActionHasEvent(action, inputEvent))
                return;

            var injected = (InputEvent)inputEvent.Duplicate();
            var instanceId = injected.GetInstanceId();
            InputMap.ActionAddEvent(action, injected);
            foreach (var currentEvent in InputMap.ActionGetEvents(action))
            {
                if (currentEvent.GetInstanceId() == instanceId)
                {
                    _injectedEventInstanceIds.Add(instanceId);
                    break;
                }
            }
        }

        public void Restore(StringName action)
        {
            if (!_existed)
            {
                if (InputMap.HasAction(action))
                    InputMap.EraseAction(action);
                return;
            }

            if (!InputMap.HasAction(action) || _injectedEventInstanceIds.Count == 0)
                return;

            foreach (var currentEvent in InputMap.ActionGetEvents(action))
            {
                if (_injectedEventInstanceIds.Contains(currentEvent.GetInstanceId()))
                    InputMap.ActionEraseEvent(action, currentEvent);
            }
            _injectedEventInstanceIds.Clear();
        }
    }
}
