using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public partial class UIScreenHost : Control
{
    private static readonly StringName ViewOwnerMeta = "__ui_screen_host_owner";

    private readonly UIScreenStackModel _model = new();
    private readonly UIScreenInputDispatcher _inputDispatcher = new();
    private readonly UIScreenFocusCoordinator _focusCoordinator = new();
    private readonly Dictionary<UIScreenHandle, UIScreenViewAdapter> _adapters = new();
    private readonly Dictionary<UIScreenLayer, Control> _layers = new();
    private readonly Dictionary<UIScreenHandle, UIControlEffectBaseline> _controlEffectBaselines = new();
    private readonly Dictionary<UIScreenHandle, UIWindowEffectBaseline> _windowEffectBaselines = new();
    private readonly Dictionary<UIScreenHandle, UILowerLayerPolicy> _appliedLowerLayerEffects = new();
    private readonly Queue<CloseRequest> _closeQueue = new();
    private readonly HashSet<UIScreenHandle> _queuedCloseHandles = new();
    private readonly HashSet<UIScreenHandle> _closingHandles = new();
    private UIScreenHostOptions _options = new();
    private UIScreenEffectiveState _currentState = EmptyEffectiveState;
    private PauseLease? _pauseLease;
    private CursorLease? _cursorLease;
    private HudLease? _hudLease;
    private int _pauseOwnershipDriftCount;
    private string? _lastPauseOwnershipViolation;
    private Control? _inputShield;
    private Node? _inputShieldParent;
    private int _inputShieldIndex;
    private ProcessModeEnum _inputShieldProcessMode;
    private bool _ready;
    private bool _malformed = true;
    private bool _tearingDown;
    private bool _teardownFinalized;
    private bool _drainingCloseQueue;

    public IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries => _model.Entries;
    public UIScreenEffectiveState CurrentState => _currentState;
    public UIScreenHostDiagnostics Diagnostics => CreateDiagnostics();

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

    // Godot queues an entire child subtree before this node receives _ExitTree().
    // Start typed host deletion synchronously so external views can be detached first.
    public new void QueueFree()
    {
        PrepareForTeardown();
        base.QueueFree();
    }

    /// <summary>
    /// Closes every hosted entry and restores host-owned state before a containing
    /// scene is deleted. A scene owner must call this synchronously before it
    /// queues or frees any ancestor of this host.
    /// </summary>
    public void PrepareForTeardown() => BeginTeardown();

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

        var effectStatus = ValidateEffectAdaptersForOpen(normalized.Policy, adapter);
        if (effectStatus != UIScreenOpenStatus.Opened)
            return new(effectStatus, null);

        var focusStatus = _focusCoordinator.TryPrepare(
            adapter,
            normalized.Policy,
            out var focusPreparation);
        if (focusStatus != UIScreenOpenStatus.Opened)
            return new(focusStatus, null);

        var opened = _model.Open(normalized.Policy);
        if (opened.Status != UIScreenOpenStatus.Opened || !opened.Handle.HasValue)
        {
            _focusCoordinator.DiscardPreparation(focusPreparation);
            return opened;
        }

        var handle = opened.Handle.Value;
        _adapters.Add(handle, adapter);
        view.SetMeta(ViewOwnerMeta, GetInstanceId());
        var applyStatus = adapter.Apply();
        if (applyStatus != UIScreenOpenStatus.Opened)
        {
            _adapters.Remove(handle);
            _model.Close(handle);
            adapter.RollbackRegistration();
            ReleaseOwnership(view);
            _focusCoordinator.DiscardPreparation(focusPreparation);
            return new(applyStatus, null);
        }

        Action treeExiting = () => OnViewTreeExiting(handle);
        adapter.TreeExitingHandler = treeExiting;
        view.TreeExiting += treeExiting;
        _focusCoordinator.Register(handle, adapter, normalized.Policy, focusPreparation);
        Recompute();
        return opened;
    }

    public UIScreenCloseResult TryClose(
        UIScreenHandle handle,
        UIScreenCloseReason reason)
    {
        if (_tearingDown && reason != UIScreenCloseReason.HostTeardown)
            return new(UIScreenCloseStatus.HostTearingDown);

        if (_closingHandles.Contains(handle) || _queuedCloseHandles.Contains(handle))
            return new(UIScreenCloseStatus.AlreadyClosed);

        if (_drainingCloseQueue)
        {
            if (!IsActive(handle))
                return new(_model.Close(handle).Status);

            _closeQueue.Enqueue(new CloseRequest(handle, reason));
            _queuedCloseHandles.Add(handle);
            return new(UIScreenCloseStatus.Closed);
        }

        _closeQueue.Enqueue(new CloseRequest(handle, reason));
        _queuedCloseHandles.Add(handle);
        return DrainCloseQueue();
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
        PrepareForTeardown();
    }

    private void BeginTeardown()
    {
        if (_tearingDown)
            return;

        _tearingDown = true;
        if (!_drainingCloseQueue && _model.Entries.Count != 0)
        {
            var top = _model.InputOrder[0].Handle;
            TryClose(top, UIScreenCloseReason.HostTeardown);
        }

        FinalizeTeardown();
    }

    private void FinalizeTeardown()
    {
        if (_teardownFinalized || _drainingCloseQueue || _model.Entries.Count != 0)
            return;

        _teardownFinalized = true;
        _focusCoordinator.CompleteActiveRestoration();
        RestoreStateLeases();
        _focusCoordinator.Teardown();
        _inputShield = null;
        _inputShieldParent = null;
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
        if (shield == null || shield.GetParent() != this ||
            !IsInputShieldValid(shield) ||
            sink == null || sink.GetParent() != this ||
            !IsFocusSinkValid(sink))
        {
            return false;
        }

        _inputShield = shield;
        _inputShieldParent = shield.GetParent();
        _inputShieldIndex = shield.GetIndex();
        _inputShieldProcessMode = shield.ProcessMode;
        _focusCoordinator.Bind(this, sink);
        return true;
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

    private UIScreenCloseResult DrainCloseQueue()
    {
        var firstStatus = UIScreenCloseStatus.StaleHandle;
        var first = true;
        _drainingCloseQueue = true;
        try
        {
            while (_closeQueue.Count != 0 ||
                   (_tearingDown && _model.Entries.Count != 0))
            {
                if (_closeQueue.Count == 0)
                {
                    var top = _model.InputOrder[0].Handle;
                    _closeQueue.Enqueue(new CloseRequest(
                        top,
                        UIScreenCloseReason.HostTeardown));
                    _queuedCloseHandles.Add(top);
                }

                var request = _closeQueue.Dequeue();
                _queuedCloseHandles.Remove(request.Handle);
                var status = ProcessClose(request);
                if (first)
                {
                    firstStatus = status;
                    first = false;
                }
            }
        }
        finally
        {
            _drainingCloseQueue = false;
            _queuedCloseHandles.Clear();
            _closingHandles.Clear();
        }

        if (_tearingDown)
            FinalizeTeardown();

        return new UIScreenCloseResult(firstStatus);
    }

    private UIScreenCloseStatus ProcessClose(CloseRequest request)
    {
        var mutation = _model.Close(request.Handle);
        if (mutation.Status != UIScreenCloseStatus.Closed)
            return mutation.Status;

        foreach (var closed in mutation.ClosedEntries)
            _closingHandles.Add(closed.Handle);

        UIFocusCloseState? requestedFocusState = null;
        foreach (var closed in mutation.ClosedEntries)
        {
            var closeReason = closed.Handle == request.Handle
                ? request.Reason
                : UIScreenCloseReason.ParentClosed;
            var focusState = CloseAdapter(closed.Handle, closeReason);
            if (closed.Handle == request.Handle)
                requestedFocusState = focusState;
        }

        requestedFocusState ??= new UIFocusCloseState(
            request.Handle,
            null,
            null,
            null,
            false);
        _focusCoordinator.BeginRestoration(requestedFocusState);
        Recompute();

        foreach (var closed in mutation.ClosedEntries)
            _closingHandles.Remove(closed.Handle);
        return UIScreenCloseStatus.Closed;
    }

    private UIFocusCloseState CloseAdapter(UIScreenHandle handle, UIScreenCloseReason reason)
    {
        var focusState = _focusCoordinator.CloseEntry(handle);
        if (!_adapters.Remove(handle, out var adapter))
            return focusState;

        RestoreLowerLayerEffect(handle, adapter);

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
        return focusState;
    }

    private void OnViewTreeExiting(UIScreenHandle handle)
    {
        if (!_tearingDown && (IsQueuedForDeletion() || !IsInsideTree()))
        {
            BeginTeardown();
            return;
        }

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
        ApplyLowerLayerEffects(resolved.LowerLayerEffects);

        var previousState = _currentState;
        var nextState = new UIScreenEffectiveState(
            resolved.PauseTree,
            resolved.BlockGameplayInput,
            resolved.Cursor,
            resolved.Hud,
            resolved.TopInputOwner,
            _focusCoordinator.IsRestorationPending);
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

    private UIScreenOpenStatus ValidateEffectAdaptersForOpen(
        UIScreenEntryPolicy candidatePolicy,
        UIScreenViewAdapter candidateAdapter)
    {
        foreach (var target in _model.Entries)
        {
            if (IsCandidateVisuallyAbove(candidatePolicy, target.Policy) &&
                _adapters.TryGetValue(target.Handle, out var targetAdapter) &&
                !targetAdapter.CanApply(candidatePolicy.LowerLayers))
            {
                return UIScreenOpenStatus.MissingRequiredAdapter;
            }
        }

        foreach (var owner in _model.Entries)
        {
            if (owner.Policy.Layer > candidatePolicy.Layer &&
                !candidateAdapter.CanApply(
                    owner.Policy.LowerLayers,
                    requireControlInteractivityAdapter: true))
            {
                return UIScreenOpenStatus.MissingRequiredAdapter;
            }
        }

        return UIScreenOpenStatus.Opened;
    }

    private static bool IsCandidateVisuallyAbove(
        UIScreenEntryPolicy candidate,
        UIScreenEntryPolicy target) =>
        candidate.Layer >= target.Layer;

    private void ApplyLowerLayerEffects(
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        foreach (var (handle, effect) in effects)
        {
            if (!_adapters.TryGetValue(handle, out var adapter) ||
                !GodotObject.IsInstanceValid(adapter.View))
            {
                continue;
            }

            if (_appliedLowerLayerEffects.TryGetValue(handle, out var applied) &&
                applied == effect)
            {
                continue;
            }

            switch (adapter.View)
            {
                case Control control:
                    ApplyControlEffect(handle, adapter, control, effect);
                    break;
                case Window window:
                    ApplyWindowEffect(handle, adapter, window, effect);
                    break;
            }
        }

        PlaceInputShield(FindTopmostInertControl(effects));
    }

    private Control? FindTopmostInertControl(
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        UIScreenEntrySnapshot? topmost = null;
        Control? target = null;
        foreach (var entry in _model.Entries)
        {
            if (!effects.TryGetValue(entry.Handle, out var effect) ||
                effect != UILowerLayerPolicy.VisibleInert ||
                !_adapters.TryGetValue(entry.Handle, out var adapter) ||
                adapter.View is not Control control)
            {
                continue;
            }

            if (topmost == null || IsVisuallyAbove(entry, topmost))
            {
                topmost = entry;
                target = control;
            }
        }

        return target;
    }

    private static bool IsVisuallyAbove(
        UIScreenEntrySnapshot candidate,
        UIScreenEntrySnapshot target) =>
        candidate.Policy.Layer > target.Policy.Layer ||
        (candidate.Policy.Layer == target.Policy.Layer &&
         candidate.Sequence > target.Sequence);

    private void PlaceInputShield(Control? target)
    {
        if (_inputShield == null || _inputShieldParent == null)
            return;

        _inputShield.Visible = false;
        if (target?.GetParent() is Node targetParent)
        {
            if (_inputShield.GetParent() != targetParent)
                _inputShield.Reparent(targetParent, false);
            targetParent.MoveChild(_inputShield, target.GetIndex() + 1);
            _inputShield.ProcessMode = ProcessModeEnum.Always;
            _inputShield.Visible = true;
            return;
        }

        if (_inputShield.GetParent() != _inputShieldParent)
            _inputShield.Reparent(_inputShieldParent, false);
        _inputShieldParent.MoveChild(_inputShield, _inputShieldIndex);
        _inputShield.ProcessMode = _inputShieldProcessMode;
    }

    private void ApplyControlEffect(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter,
        Control control,
        UILowerLayerPolicy effect)
    {
        if (effect == UILowerLayerPolicy.VisibleInteractive)
        {
            RestoreLowerLayerEffect(handle, adapter);
            return;
        }

        if (!_controlEffectBaselines.TryGetValue(handle, out var baseline))
        {
            baseline = new UIControlEffectBaseline(
                control.Visible,
                control.IsProcessingInput());
            _controlEffectBaselines.Add(handle, baseline);
            adapter.SetInteractive(false);
        }

        if (effect == UILowerLayerPolicy.Hidden)
        {
            control.Visible = false;
        }
        else
        {
            if (_appliedLowerLayerEffects.TryGetValue(handle, out var applied) &&
                applied == UILowerLayerPolicy.Hidden)
            {
                control.Visible = baseline.Visible;
            }
        }

        _appliedLowerLayerEffects[handle] = effect;
    }

    private void ApplyWindowEffect(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter,
        Window window,
        UILowerLayerPolicy effect)
    {
        if (effect == UILowerLayerPolicy.VisibleInteractive)
        {
            RestoreLowerLayerEffect(handle, adapter);
            return;
        }

        if (!_windowEffectBaselines.TryGetValue(handle, out var baseline))
        {
            baseline = new UIWindowEffectBaseline(
                adapter.IsPresented(),
                window.GuiDisableInput,
                window.Unfocusable);
            _windowEffectBaselines.Add(handle, baseline);
        }

        if (effect == UILowerLayerPolicy.Hidden)
        {
            window.GuiDisableInput = baseline.GuiDisableInput;
            window.Unfocusable = baseline.Unfocusable;
            adapter.SetPresented(false);
        }
        else
        {
            if (_appliedLowerLayerEffects.TryGetValue(handle, out var applied) &&
                applied == UILowerLayerPolicy.Hidden)
            {
                adapter.SetPresented(baseline.Visible);
            }
            window.GuiDisableInput = true;
            window.Unfocusable = true;
        }

        _appliedLowerLayerEffects[handle] = effect;
    }

    private void RestoreLowerLayerEffect(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter)
    {
        _appliedLowerLayerEffects.TryGetValue(handle, out var applied);
        _appliedLowerLayerEffects.Remove(handle);
        if (!GodotObject.IsInstanceValid(adapter.View))
        {
            _controlEffectBaselines.Remove(handle);
            _windowEffectBaselines.Remove(handle);
            return;
        }

        if (adapter.View is Control control &&
            _controlEffectBaselines.Remove(handle, out var controlBaseline))
        {
            if (applied == UILowerLayerPolicy.Hidden)
                control.Visible = controlBaseline.Visible;
            adapter.SetInteractive(controlBaseline.ProcessInputEnabled);
        }
        else if (adapter.View is Window window &&
                 _windowEffectBaselines.Remove(handle, out var windowBaseline))
        {
            if (applied == UILowerLayerPolicy.Hidden)
                adapter.SetPresented(windowBaseline.Visible);
            window.GuiDisableInput = windowBaseline.GuiDisableInput;
            window.Unfocusable = windowBaseline.Unfocusable;
        }
    }

    private void EnsurePauseLeaseInvariant()
    {
        if (_pauseLease == null || GetTree().Paused)
            return;

        _pauseOwnershipDriftCount++;
        _lastPauseOwnershipViolation = "TreeUnpausedWhilePauseLeaseActive";
        GD.PushError(
            "UIScreenHost pause ownership drift detected; reasserting the active pause lease.");
        GetTree().Paused = true;
    }

    private UIScreenHostDiagnostics CreateDiagnostics()
    {
        var inputOrder = new List<UIScreenEntrySnapshot>(_model.InputOrder).AsReadOnly();
        var resolved = UIScreenPolicyResolver.Resolve(inputOrder);
        var lowerEffects = new List<UIScreenLowerLayerEffectDiagnostics>(inputOrder.Count);
        var entryActions = new Dictionary<UIScreenHandle, IReadOnlySet<StringName>>(
            inputOrder.Count);
        var processStates = new List<UIScreenProcessStateDiagnostics>(inputOrder.Count);

        foreach (var target in inputOrder)
        {
            var contributors = new List<UIScreenHandle>();
            foreach (var owner in inputOrder)
            {
                if (owner.Policy.LowerLayers != UILowerLayerPolicy.VisibleInteractive &&
                    IsVisuallyAbove(owner, target))
                {
                    contributors.Add(owner.Handle);
                }
            }

            lowerEffects.Add(new UIScreenLowerLayerEffectDiagnostics(
                target.Handle,
                resolved.LowerLayerEffects[target.Handle],
                contributors.AsReadOnly()));
            entryActions.Add(
                target.Handle,
                target.Policy.EntryCancelActions.ToFrozenSet());

            if (_adapters.TryGetValue(target.Handle, out var adapter))
            {
                processStates.Add(new UIScreenProcessStateDiagnostics(
                    target.Handle,
                    adapter.IncomingProcessMode,
                    adapter.RegisteredProcessMode,
                    GodotObject.IsInstanceValid(adapter.View)
                        ? adapter.View.ProcessMode
                        : null,
                    adapter.View is Window));
            }
        }

        var controlEffects = new Dictionary<UIScreenHandle, UIControlEffectLeaseDiagnostics>(
            _controlEffectBaselines.Count);
        foreach (var (handle, baseline) in _controlEffectBaselines)
        {
            controlEffects.Add(handle, new UIControlEffectLeaseDiagnostics(
                baseline.Visible,
                baseline.ProcessInputEnabled));
        }

        var windowEffects = new Dictionary<UIScreenHandle, UIWindowEffectLeaseDiagnostics>(
            _windowEffectBaselines.Count);
        foreach (var (handle, baseline) in _windowEffectBaselines)
        {
            windowEffects.Add(handle, new UIWindowEffectLeaseDiagnostics(
                baseline.Visible,
                baseline.GuiDisableInput,
                baseline.Unfocusable));
        }

        return new UIScreenHostDiagnostics(
            inputOrder,
            _currentState,
            lowerEffects.AsReadOnly(),
            new UIScreenActionOwnershipDiagnostics(
                _options.CoreCancelActions.ToFrozenSet(),
                new ReadOnlyDictionary<UIScreenHandle, IReadOnlySet<StringName>>(entryActions),
                _currentState.TopInputOwner),
            _focusCoordinator.SnapshotDiagnostics(inputOrder),
            _focusCoordinator.RestorationLease,
            processStates.AsReadOnly(),
            IsInsideTree() && GetViewport().GuiEmbedSubwindows,
            new UIScreenStateLeaseDiagnostics(
                _pauseLease?.IncomingPaused,
                _cursorLease?.IncomingMode,
                _hudLease?.IncomingVisible,
                new ReadOnlyDictionary<UIScreenHandle, UIControlEffectLeaseDiagnostics>(
                    controlEffects),
                new ReadOnlyDictionary<UIScreenHandle, UIWindowEffectLeaseDiagnostics>(
                    windowEffects)),
            _pauseOwnershipDriftCount,
            _lastPauseOwnershipViolation);
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
    private sealed record CloseRequest(
        UIScreenHandle Handle,
        UIScreenCloseReason Reason);

    internal IReadOnlyList<UIScreenEntrySnapshot> FocusInputOrder() =>
        _model.InputOrder;

    internal void OnFocusRestorationCompleted()
    {
        if (_ready)
            Recompute();
    }
}

internal sealed record UIControlEffectBaseline(bool Visible, bool ProcessInputEnabled);
internal sealed record UIWindowEffectBaseline(bool Visible, bool GuiDisableInput, bool Unfocusable);
