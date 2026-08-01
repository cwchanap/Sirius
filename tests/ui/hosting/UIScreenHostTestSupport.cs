using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public static class UIScreenHostTestSupport
{
    public static async Task<HostFixture> CreateHost(
        Node owner,
        IEnumerable<StringName>? coreActions = null)
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var host = scene.Instantiate<UIScreenHost>();
        var tree = (SceneTree)Engine.GetMainLoop();
        var hostParent = owner.IsInsideTree() ? owner : tree.Root;
        hostParent.AddChild(host);
        var fixture = new HostFixture(host, coreActions);
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

    internal HostFixture(UIScreenHost host, IEnumerable<StringName>? coreActions)
    {
        Host = host;
        _tree = host.GetTree();
        _viewport = host.GetViewport();
        HudRoot = host.GetNode<Control>("HUDLayer");
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _tree.Paused = false;

        foreach (var view in _trackedViews)
        {
            if (GodotObject.IsInstanceValid(view) && !view.IsQueuedForDeletion())
                view.QueueFree();
        }

        if (GodotObject.IsInstanceValid(Host) && !Host.IsQueuedForDeletion())
            Host.QueueFree();

        foreach (var (action, snapshot) in _inputActions)
            snapshot.Restore(action);

        if (GodotObject.IsInstanceValid(HudRoot))
            HudRoot.Visible = _incomingHudVisible;
        if (GodotObject.IsInstanceValid(_viewport))
            _viewport.GuiEmbedSubwindows = _incomingEmbedSubwindows;

        Input.MouseMode = _incomingMouseMode;
        _tree.Paused = _incomingPaused;
    }

    private sealed record InputActionSnapshot(
        bool Existed,
        float Deadzone,
        IReadOnlyList<InputEvent> Events)
    {
        public static InputActionSnapshot Capture(StringName action)
        {
            var existed = InputMap.HasAction(action);
            var events = new List<InputEvent>();
            if (existed)
            {
                foreach (var inputEvent in InputMap.ActionGetEvents(action))
                    events.Add((InputEvent)inputEvent.Duplicate());
            }

            return new InputActionSnapshot(
                existed,
                existed ? InputMap.ActionGetDeadzone(action) : 0.5f,
                events.AsReadOnly());
        }

        public void Restore(StringName action)
        {
            if (!Existed)
            {
                if (InputMap.HasAction(action))
                    InputMap.EraseAction(action);
                return;
            }

            if (!InputMap.HasAction(action))
                InputMap.AddAction(action);
            InputMap.ActionSetDeadzone(action, Deadzone);
            InputMap.ActionEraseEvents(action);
            foreach (var inputEvent in Events)
                InputMap.ActionAddEvent(action, (InputEvent)inputEvent.Duplicate());
        }
    }
}
