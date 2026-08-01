using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

public partial class UIScreenHost : Control
{
    private static readonly StringName ViewOwnerMeta = "__ui_screen_host_owner";

    private readonly UIScreenStackModel _model = new();
    private readonly UIScreenInputDispatcher _inputDispatcher = new();
    private readonly Dictionary<UIScreenHandle, UIScreenViewAdapter> _adapters = new();
    private readonly Dictionary<UIScreenLayer, Control> _layers = new();
    private UIScreenHostOptions _options = new();
    private UIScreenEffectiveState _currentState = EmptyEffectiveState;
    private PauseLease? _pauseLease;
    private CursorLease? _cursorLease;
    private HudLease? _hudLease;
    private int _pauseOwnershipDriftCount;
    private bool _ready;
    private bool _malformed = true;
    private bool _tearingDown;

    public IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries => _model.Entries;
    public UIScreenEffectiveState CurrentState => _currentState;
    public UIScreenHostDiagnostics Diagnostics => new(
        _currentState,
        _pauseOwnershipDriftCount);

    public event Action<UIScreenEffectiveState>? EffectiveStateChanged;

    private static UIScreenEffectiveState EmptyEffectiveState => new(
        false,
        false,
        UICursorPolicy.Inherit,
        UIHudPolicy.Inherit,
        null,
        false);

    public void Configure(UIScreenHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_ready || _tearingDown || _model.Entries.Count != 0)
            throw new InvalidOperationException("UIScreenHost must be configured before entering the tree.");

        _options = options with
        {
            CoreCancelActions = options.CoreCancelActions == null ||
                                options.CoreCancelActions.Count == 0
                ? EmptyStringNameSet.Value
                : options.CoreCancelActions.ToFrozenSet()
        };
    }

    public override void _Ready()
    {
        _ready = true;
        _malformed = !TryBindRequiredNodes();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_tearingDown)
            return;

        if (TryHandleInput(inputEvent) == UIInputDispatchResult.Consumed)
            GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!_tearingDown)
            EnsurePauseLeaseInvariant();
    }

    public UIInputDispatchResult TryHandleInput(InputEvent inputEvent)
    {
        if (_tearingDown || !_ready || _malformed)
            return UIInputDispatchResult.NoOwner;

        return _inputDispatcher.TryHandleInput(
            inputEvent,
            _options.CoreCancelActions,
            PruneInvalidEntries,
            () => _model.InputOrder,
            InterceptorFor,
            handle => TryClose(handle, UIScreenCloseReason.Cancel),
            () => _currentState,
            _options.RootCancelFallback);
    }

    public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec)
    {
        if (!_ready || _tearingDown)
            return new(UIScreenOpenStatus.MalformedHost, null);
        if (_malformed)
            return new(UIScreenOpenStatus.MalformedHost, null);
        if (view == null || !GodotObject.IsInstanceValid(view) ||
            view == this || view.IsQueuedForDeletion())
        {
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        var ownershipStatus = GetOwnershipStatus(view);
        if (ownershipStatus != UIScreenOpenStatus.Opened)
            return new(ownershipStatus, null);

        if (spec == null)
            return new(UIScreenOpenStatus.InvalidSpecification, null);
        var normalized = spec.Normalize();
        if (normalized.Status != UIScreenOpenStatus.Opened || normalized.Policy == null)
            return new(normalized.Status, null);
        if (normalized.Policy.Hud != UIHudPolicy.Inherit && !HasConfiguredHudRoot())
            return new(UIScreenOpenStatus.InvalidSpecification, null);

        var layer = _layers[normalized.Policy.Layer];
        var adapterStatus = UIScreenViewAdapter.TryCreate(
            this,
            layer,
            view,
            spec,
            normalized.Policy,
            out var adapter);
        if (adapterStatus != UIScreenOpenStatus.Opened || adapter == null)
            return new(adapterStatus, null);

        var opened = _model.Open(normalized.Policy);
        if (opened.Status != UIScreenOpenStatus.Opened || !opened.Handle.HasValue)
            return opened;

        var handle = opened.Handle.Value;
        view.SetMeta(ViewOwnerMeta, GetInstanceId());
        var applyStatus = adapter.Apply();
        if (applyStatus != UIScreenOpenStatus.Opened)
        {
            _model.Close(handle);
            adapter.RollbackRegistration();
            ReleaseOwnership(view);
            return new(applyStatus, null);
        }

        _adapters.Add(handle, adapter);
        Action treeExiting = () => OnViewTreeExiting(handle);
        adapter.TreeExitingHandler = treeExiting;
        view.TreeExiting += treeExiting;
        Recompute();
        return opened;
    }

    public UIScreenCloseResult TryClose(
        UIScreenHandle handle,
        UIScreenCloseReason reason)
    {
        if (_tearingDown && reason != UIScreenCloseReason.HostTeardown)
            return new(UIScreenCloseStatus.HostTearingDown);

        var mutation = _model.Close(handle);
        if (mutation.Status != UIScreenCloseStatus.Closed)
            return new(mutation.Status);

        foreach (var closed in mutation.ClosedEntries)
            CloseAdapter(closed.Handle, reason);

        Recompute();
        return new(UIScreenCloseStatus.Closed);
    }

    public bool IsActive(UIScreenHandle handle)
    {
        foreach (var entry in _model.Entries)
        {
            if (entry.Handle == handle)
                return true;
        }
        return false;
    }

    private UIScreenOpenStatus GetOwnershipStatus(Node view)
    {
        if (!view.HasMeta(ViewOwnerMeta))
            return UIScreenOpenStatus.Opened;

        var ownerId = view.GetMeta(ViewOwnerMeta).AsUInt64();
        if (ownerId == GetInstanceId())
            return UIScreenOpenStatus.NodeAlreadyRegistered;

        var owner = GodotObject.InstanceFromId(ownerId);
        if (owner is UIScreenHost && GodotObject.IsInstanceValid(owner))
            return UIScreenOpenStatus.NodeOwnedByAnotherHost;

        view.RemoveMeta(ViewOwnerMeta);
        return UIScreenOpenStatus.Opened;
    }

    private void ReleaseOwnership(Node view)
    {
        if (GodotObject.IsInstanceValid(view) && view.HasMeta(ViewOwnerMeta) &&
            view.GetMeta(ViewOwnerMeta).AsUInt64() == GetInstanceId())
        {
            view.RemoveMeta(ViewOwnerMeta);
        }
    }

    public bool IsKindActive(StringName kind)
    {
        foreach (var entry in _model.Entries)
        {
            if (entry.Policy.Kind == kind)
                return true;
        }
        return false;
    }

    internal Viewport? FocusViewportFor(UIScreenHandle handle) =>
        _adapters.TryGetValue(handle, out var adapter)
            ? adapter.FocusViewport()
            : null;

    public override void _ExitTree()
    {
        _tearingDown = true;
        while (_model.Entries.Count != 0)
        {
            var top = _model.InputOrder[0].Handle;
            TryClose(top, UIScreenCloseReason.HostTeardown);
        }
        RestoreStateLeases();
        _layers.Clear();
        _ready = false;
    }

    private bool TryBindRequiredNodes()
    {
        _layers.Clear();
        if (ProcessMode != ProcessModeEnum.Always ||
            !TryAddLayer(UIScreenLayer.Hud, "HUDLayer", ProcessModeEnum.Pausable) ||
            !TryAddLayer(UIScreenLayer.Screen, "ScreenLayer", ProcessModeEnum.Always) ||
            !TryAddLayer(UIScreenLayer.Modal, "ModalLayer", ProcessModeEnum.Always) ||
            !TryAddLayer(UIScreenLayer.Toast, "ToastLayer", ProcessModeEnum.Always) ||
            !TryAddLayer(UIScreenLayer.Transition, "TransitionLayer", ProcessModeEnum.Always))
        {
            return false;
        }

        var shield = GetNodeOrNull<Control>("InputShield");
        var sink = GetNodeOrNull<Control>("FocusSink");
        return shield != null && shield.GetParent() == this &&
               IsInputShieldValid(shield) &&
               sink != null && sink.GetParent() == this &&
               IsFocusSinkValid(sink);
    }

    private static bool IsInputShieldValid(Control shield) =>
        !shield.Visible &&
        shield.MouseFilter == MouseFilterEnum.Stop &&
        shield.FocusMode == FocusModeEnum.None &&
        IsFullRect(shield);

    private static bool IsFocusSinkValid(Control sink) =>
        sink.Visible &&
        sink.MouseFilter == MouseFilterEnum.Ignore &&
        sink.FocusMode == FocusModeEnum.All &&
        sink.CustomMinimumSize == Vector2.One &&
        sink.Position == Vector2.Zero &&
        sink.Size == Vector2.One &&
        sink.AnchorLeft == 0 &&
        sink.AnchorTop == 0 &&
        sink.AnchorRight == 0 &&
        sink.AnchorBottom == 0;

    private static bool IsFullRect(Control control) =>
        control.AnchorLeft == 0 &&
        control.AnchorTop == 0 &&
        control.AnchorRight == 1 &&
        control.AnchorBottom == 1 &&
        control.OffsetLeft == 0 &&
        control.OffsetTop == 0 &&
        control.OffsetRight == 0 &&
        control.OffsetBottom == 0 &&
        control.GrowHorizontal == GrowDirection.Both &&
        control.GrowVertical == GrowDirection.Both;

    private bool TryAddLayer(
        UIScreenLayer layer,
        NodePath path,
        ProcessModeEnum expectedProcessMode)
    {
        var node = GetNodeOrNull<Control>(path);
        if (node == null || node.GetParent() != this || node.ProcessMode != expectedProcessMode)
            return false;
        _layers.Add(layer, node);
        return true;
    }

    private void CloseAdapter(UIScreenHandle handle, UIScreenCloseReason reason)
    {
        if (!_adapters.Remove(handle, out var adapter))
            return;

        if (GodotObject.IsInstanceValid(adapter.View))
        {
            if (adapter.TreeExitingHandler != null)
                adapter.View.TreeExiting -= adapter.TreeExitingHandler;
            ReleaseOwnership(adapter.View);
        }

        try
        {
            adapter.Cleanup?.Invoke(reason);
        }
        catch (Exception exception)
        {
            GD.PushError($"UIScreenHost cleanup failed for '{handle.Kind}': {exception.Message}");
        }
        finally
        {
            adapter.Close();
        }
    }

    private void OnViewTreeExiting(UIScreenHandle handle)
    {
        if (!_tearingDown && IsActive(handle))
            TryClose(handle, UIScreenCloseReason.NodeFreed);
    }

    private Func<UIInputContext, UIInputInterception>? InterceptorFor(
        UIScreenHandle handle) =>
        _adapters.TryGetValue(handle, out var adapter)
            ? adapter.InterceptCancel
            : null;

    private void PruneInvalidEntries()
    {
        var invalidHandles = new List<UIScreenHandle>();
        foreach (var entry in _model.Entries)
        {
            if (!_adapters.TryGetValue(entry.Handle, out var adapter) ||
                !GodotObject.IsInstanceValid(adapter.View) ||
                adapter.View.IsQueuedForDeletion())
            {
                invalidHandles.Add(entry.Handle);
            }
        }

        foreach (var handle in invalidHandles)
        {
            if (IsActive(handle))
                TryClose(handle, UIScreenCloseReason.NodeFreed);
        }
    }

    private void Recompute()
    {
        var resolved = UIScreenPolicyResolver.Resolve(_model.InputOrder);
        ApplyPausePolicy(resolved.PauseTree);
        ApplyCursorPolicy(resolved.Cursor);
        ApplyHudPolicy(resolved.Hud);

        var previousState = _currentState;
        var nextState = new UIScreenEffectiveState(
            resolved.PauseTree,
            resolved.BlockGameplayInput,
            resolved.Cursor,
            resolved.Hud,
            resolved.TopInputOwner,
            false);
        _currentState = nextState;

        if (previousState.IsPresentationGameplayBlocked !=
            nextState.IsPresentationGameplayBlocked)
        {
            _options.GameplayInputBlockChanged?.Invoke(
                nextState.IsPresentationGameplayBlocked);
        }

        if (previousState != nextState)
            EffectiveStateChanged?.Invoke(nextState);
    }

    private void ApplyPausePolicy(bool pauseTree)
    {
        var tree = GetTree();
        if (pauseTree)
        {
            _pauseLease ??= new PauseLease(tree.Paused);
            if (!tree.Paused)
                tree.Paused = true;
            return;
        }

        if (_pauseLease == null)
            return;

        tree.Paused = _pauseLease.IncomingPaused;
        _pauseLease = null;
    }

    private void ApplyCursorPolicy(UICursorPolicy cursor)
    {
        if (cursor == UICursorPolicy.Inherit)
        {
            if (_cursorLease == null)
                return;

            Input.MouseMode = _cursorLease.IncomingMode;
            _cursorLease = null;
            return;
        }

        _cursorLease ??= new CursorLease(Input.MouseMode);
        Input.MouseMode = cursor == UICursorPolicy.Visible
            ? Input.MouseModeEnum.Visible
            : Input.MouseModeEnum.Hidden;
    }

    private void ApplyHudPolicy(UIHudPolicy hud)
    {
        if (hud == UIHudPolicy.Inherit)
        {
            if (_hudLease == null)
                return;

            if (HasConfiguredHudRoot())
                _options.HudRoot!.Visible = _hudLease.IncomingVisible;
            _hudLease = null;
            return;
        }

        if (!HasConfiguredHudRoot())
            return;

        _hudLease ??= new HudLease(_options.HudRoot!.Visible);
        _options.HudRoot.Visible = hud == UIHudPolicy.Visible;
    }

    private bool HasConfiguredHudRoot() =>
        _options.HudRoot != null &&
        GodotObject.IsInstanceValid(_options.HudRoot) &&
        !_options.HudRoot.IsQueuedForDeletion();

    private void EnsurePauseLeaseInvariant()
    {
        if (_pauseLease == null || GetTree().Paused)
            return;

        _pauseOwnershipDriftCount++;
        GD.PushError(
            "UIScreenHost pause ownership drift detected; reasserting the active pause lease.");
        GetTree().Paused = true;
    }

    private void RestoreStateLeases()
    {
        if (_pauseLease != null)
        {
            GetTree().Paused = _pauseLease.IncomingPaused;
            _pauseLease = null;
        }

        if (_cursorLease != null)
        {
            Input.MouseMode = _cursorLease.IncomingMode;
            _cursorLease = null;
        }

        if (_hudLease != null)
        {
            if (HasConfiguredHudRoot())
                _options.HudRoot!.Visible = _hudLease.IncomingVisible;
            _hudLease = null;
        }
    }

    private sealed record PauseLease(bool IncomingPaused);
    private sealed record CursorLease(Input.MouseModeEnum IncomingMode);
    private sealed record HudLease(bool IncomingVisible);
}
