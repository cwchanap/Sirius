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
    // The most recently committed resolved lower-layer effects, keyed by entry
    // handle. Updated at the end of a stable Recompute pass so the focus
    // coordinator can query a handle's reduced effect (VisibleInteractive vs
    // VisibleInert/Hidden) when acquiring or restoring focus, rather than
    // relying on logical input order alone.
    private IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> _resolvedLowerLayerEffects =
        EmptyLowerLayerEffects;
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
    private bool _inTreeExiting;
    private bool _finalizingTeardown;
    private bool _teardownFinalized;
    private bool _drainingCloseQueue;
    private int _recomputeDepth;
    private bool _recomputePending;

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

    private static IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy>
        EmptyLowerLayerEffects { get; } =
        new ReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy>(
            new Dictionary<UIScreenHandle, UILowerLayerPolicy>(0));

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
    // Prepare before typed deletion; a re-entrant caller must retry after Deferred.
    public new void QueueFree()
    {
        if (PrepareForTeardown() != UIScreenTeardownPreparationStatus.Complete)
        {
            GD.PushWarning(
                "UIScreenHost teardown is deferred; QueueFree was skipped. " +
                "Retry after PrepareForTeardown() returns Complete.");
            return;
        }

        base.QueueFree();
    }

    /// <summary>
    /// Closes every hosted entry and restores host-owned state before a containing
    /// scene is deleted. A scene owner may queue or free an ancestor only after
    /// this returns <see cref="UIScreenTeardownPreparationStatus.Complete"/>.
    /// A re-entrant call from an active close or finalization callback returns
    /// <see cref="UIScreenTeardownPreparationStatus.Deferred"/>; retry after the
    /// current operation finishes. If a finalization callback throws, the
    /// exception propagates without publishing completion and a later call may
    /// retry finalization.
    /// </summary>
    public UIScreenTeardownPreparationStatus PrepareForTeardown()
    {
        BeginTeardown();
        return _teardownFinalized
            ? UIScreenTeardownPreparationStatus.Complete
            : UIScreenTeardownPreparationStatus.Deferred;
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
        if (_drainingCloseQueue)
            return new(UIScreenOpenStatus.HostMutating, null);
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
        var isPausedAfterOpen = normalized.Policy.PauseTree;
        if (!isPausedAfterOpen)
        {
            foreach (var active in _model.Entries)
            {
                if (active.Policy.PauseTree)
                {
                    isPausedAfterOpen = true;
                    break;
                }
            }
        }
        var hasPauseBoundedLifetime = HasPauseBoundedLifetime(normalized.Policy);
        var adapterStatus = UIScreenViewAdapter.TryCreate(
            this,
            layer,
            view,
            spec,
            normalized.Policy,
            isPausedAfterOpen,
            hasPauseBoundedLifetime,
            out var adapter);
        if (adapterStatus != UIScreenOpenStatus.Opened || adapter == null)
            return new(adapterStatus, null);

        var effectStatus = ValidateEffectAdaptersForOpen(normalized.Policy, adapter);
        if (effectStatus != UIScreenOpenStatus.Opened)
            return new(effectStatus, null);

        var opened = _model.Open(normalized.Policy);
        if (opened.Status != UIScreenOpenStatus.Opened || !opened.Handle.HasValue)
            return opened;

        var handle = opened.Handle.Value;
        // Snapshot the model generation before TryPrepare. TryPrepare invokes
        // caller-provided focus delegates for a child entry (CaptureParentFocus
        // → the parent's FocusViewport). That delegate may synchronously
        // re-enter the host: open another entry or close an ancestor. While the
        // candidate sits in _model but not in _adapters, a re-entrant open's
        // ValidateEffectAdaptersForOpen and process-policy validation skip the
        // candidate (their _adapters.TryGetValue guard fails), so the
        // candidate's own earlier validation — which ran against the
        // pre-TryPrepare snapshot (pause state, inerting owners) — is now
        // stale. Detect any model mutation during TryPrepare and reject the
        // open instead of committing a candidate whose validation was
        // bypassed. A cascade that removed the candidate is distinguished
        // below (InvalidNode) from a re-entrant mutation that left it active
        // (HostMutating, so the caller retries against the current state).
        var generationBeforePrepare = _model.MutationGeneration;
        var focusStatus = _focusCoordinator.TryPrepare(
            adapter,
            normalized.Policy,
            out var focusPreparation);
        if (focusStatus != UIScreenOpenStatus.Opened)
        {
            _model.Close(handle);
            return new(focusStatus, null);
        }

        if (_model.MutationGeneration != generationBeforePrepare)
        {
            _focusCoordinator.DiscardPreparation(focusPreparation);
            var cascadeRemoved = !IsActive(handle);
            _model.Close(handle);
            return new(
                cascadeRemoved
                    ? UIScreenOpenStatus.InvalidNode
                    : UIScreenOpenStatus.HostMutating,
                null);
        }

        // A re-entrant close that cascades through the candidate without a
        // generation change is not possible (UIScreenStackModel.Close always
        // bumps MutationGeneration), so the generation check above subsumes
        // the cascade case. The IsActive check is retained as a defensive
        // guard for a candidate removed by a path that somehow did not bump
        // the generation, and documents the same invariant: never register an
        // orphan adapter/ownership/focus record/tree-exit handler for a handle
        // that is no longer in the model.
        if (!IsActive(handle))
        {
            _model.Close(handle);
            _focusCoordinator.DiscardPreparation(focusPreparation);
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        // TryPrepare's caller-provided delegates (the parent's FocusViewport)
        // may have freed the candidate view or queued it for deletion without
        // closing the model handle. Freeing a detached view (AddChild has not
        // run yet — it happens in adapter.Apply() below) does not fire
        // TreeExiting, so the generation and IsActive guards above do not
        // catch it. Re-validate the view's Godot-object validity before
        // touching it, mirroring the check at the top of TryPresent. Without
        // this, view.SetMeta below dereferences a freed object and throws
        // after the adapter has been added to _adapters — stranding the model
        // entry and adapter with no ownership metadata, focus record, or
        // tree-exit handler, and bypassing normal rollback.
        if (!GodotObject.IsInstanceValid(view) || view.IsQueuedForDeletion())
        {
            _model.Close(handle);
            _focusCoordinator.DiscardPreparation(focusPreparation);
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        _adapters.Add(handle, adapter);
        // Transactional guard: SetMeta dereferences the view. If it throws
        // (e.g. the view became invalid between the validity check above and
        // this point), roll back the adapter insertion and model entry so an
        // exception cannot strand a model/adapter pair with no ownership
        // metadata, focus record, or tree-exit handler.
        try
        {
            view.SetMeta(ViewOwnerMeta, GetInstanceId());
        }
        catch (Exception)
        {
            _adapters.Remove(handle);
            _model.Close(handle);
            _focusCoordinator.DiscardPreparation(focusPreparation);
            throw;
        }
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

        // A re-entrant close from the view's _Ready() (synchronously invoked
        // during adapter.Apply() via AddChild) may have closed this handle and
        // removed its adapter registration even though Apply() returned Opened.
        // Validate that the handle is still active and still maps to the same
        // adapter before registering focus or returning Opened. Without this,
        // TryPresent installs a tree-exit handler and focus record for a
        // closed handle, leaving orphan state and returning a stale handle.
        // The IsActive check also covers a cascade that removed the handle
        // from the model while leaving the adapter in _adapters (e.g. a
        // callback during preparation that closed an ancestor after the
        // adapter was registered but before this validation runs).
        if (!IsActive(handle) ||
            !_adapters.TryGetValue(handle, out var activeAdapter) ||
            !ReferenceEquals(activeAdapter, adapter))
        {
            _adapters.Remove(handle);
            _model.Close(handle);
            adapter.RollbackRegistration();
            ReleaseOwnership(view);
            _focusCoordinator.DiscardPreparation(focusPreparation);
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        Action treeExiting = () => OnViewTreeExiting(handle);
        adapter.TreeExitingHandler = treeExiting;
        view.TreeExiting += treeExiting;
        _focusCoordinator.Register(handle, adapter, normalized.Policy, focusPreparation);
        Recompute();
        return opened;
    }

    private bool HasPauseBoundedLifetime(UIScreenEntryPolicy candidate)
    {
        if (candidate.PauseTree)
            return true;

        var parent = candidate.Parent;
        while (parent.HasValue)
        {
            UIScreenEntryPolicy? parentPolicy = null;
            foreach (var active in _model.Entries)
            {
                if (active.Handle == parent.Value)
                {
                    parentPolicy = active.Policy;
                    break;
                }
            }

            if (parentPolicy == null)
                return false;
            if (parentPolicy.PauseTree)
                return true;

            parent = parentPolicy.Parent;
        }

        return false;
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
        {
            FinalizeTeardown();
            return;
        }

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
        if (_teardownFinalized || _finalizingTeardown ||
            _drainingCloseQueue || _model.Entries.Count != 0)
        {
            return;
        }

        _finalizingTeardown = true;
        try
        {
            _focusCoordinator.CompleteActiveRestoration(
                propagateProviderExceptions: true);
            RestoreStateLeases();
            _focusCoordinator.Teardown();
            _inputShield = null;
            _inputShieldParent = null;
            _layers.Clear();
            _ready = false;
            _teardownFinalized = true;
        }
        finally
        {
            _finalizingTeardown = false;
        }
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
                // A synthetic teardown retry is generated only when the queue is
                // empty but entries remain during teardown. It is the only case
                // allowed to break on no progress: without that, it would requeue
                // the same top entry forever. An explicit request that makes no
                // progress (e.g. a stale handle whose ancestor was already closed
                // earlier in this same drain) must NOT break the loop, or later
                // valid requests would strand behind it.
                var syntheticTeardownRetry = _closeQueue.Count == 0;
                var countBeforeRequest = _model.Entries.Count;
                if (syntheticTeardownRetry)
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

                if (_model.Entries.Count == countBeforeRequest &&
                    syntheticTeardownRetry)
                    break;
            }
        }
        finally
        {
            _drainingCloseQueue = false;
            // Keep _queuedCloseHandles consistent with whatever remains in the
            // queue. Clearing it unconditionally would let a still-queued handle
            // be enqueued again as a duplicate on the next close attempt, and
            // the original request's caller already received Closed.
            if (_closeQueue.Count == 0)
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
        _focusCoordinator.BeginRestoration(
            requestedFocusState,
            scheduleDeferred: !_tearingDown);
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
        {
            _inTreeExiting = true;
            try
            {
                TryClose(handle, UIScreenCloseReason.NodeFreed);
            }
            finally
            {
                _inTreeExiting = false;
            }
        }
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
        // Re-entrant guard: caller-controlled callbacks (SetInteractive,
        // GameplayInputBlockChanged, EffectiveStateChanged) may synchronously
        // call TryClose/TryPresent, which re-enters Recompute. A nested call
        // must not run its own pass against a snapshot the outer pass is still
        // consuming; it instead marks the pass dirty so the outer pass restarts
        // from the current model state once its callbacks return.
        if (_recomputeDepth > 0)
        {
            _recomputePending = true;
            return;
        }

        _recomputeDepth++;
        try
        {
            while (true)
            {
                _recomputePending = false;
                var generationBefore = _model.MutationGeneration;

                var resolved = UIScreenPolicyResolver.Resolve(_model.InputOrder);
                ApplyPausePolicy(resolved.PauseTree);
                ApplyCursorPolicy(resolved.Cursor);
                ApplyHudPolicy(resolved.Hud);
                ApplyLowerLayerEffects(resolved.LowerLayerEffects, generationBefore);

                // A callback during policy application (e.g. SetInteractive
                // closing the effect owner) may have mutated the model. The
                // resolved snapshot is now stale; restart from the current
                // model instead of publishing a state derived from it.
                if (_model.MutationGeneration != generationBefore || _recomputePending)
                    continue;

                var previousState = _currentState;
                var nextState = new UIScreenEffectiveState(
                    resolved.PauseTree,
                    resolved.BlockGameplayInput,
                    resolved.Cursor,
                    resolved.Hud,
                    resolved.TopInputOwner,
                    _focusCoordinator.IsRestorationPending);
                _currentState = nextState;
                // Commit the resolved lower-layer effects so the focus
                // coordinator can query a handle's reduced effect when
                // acquiring/restoring focus. This snapshot is consistent with
                // the published state and the applied effects.
                _resolvedLowerLayerEffects = resolved.LowerLayerEffects;

                if (previousState.IsPresentationGameplayBlocked !=
                    nextState.IsPresentationGameplayBlocked)
                {
                    _options.GameplayInputBlockChanged?.Invoke(
                        nextState.IsPresentationGameplayBlocked);
                    // The callback may have mutated the host (e.g. a close on
                    // block-state change). Don't publish the stale snapshot to
                    // EffectiveStateChanged subscribers; restart instead.
                    if (_model.MutationGeneration != generationBefore ||
                        _recomputePending)
                        continue;
                }

                if (previousState != nextState)
                {
                    // Invoke subscribers individually so a mutation by an earlier
                    // subscriber (e.g. TryClose during publication) aborts the
                    // remaining invocation list. Without this, later subscribers
                    // receive a stale state that names a now-closed entry.
                    InvokeEffectiveStateChanged(nextState, generationBefore);
                }

                // A subscriber may have mutated the host during publication.
                // Restart so the final published state agrees with the model
                // rather than the snapshot taken before the subscribers ran.
                if (_model.MutationGeneration != generationBefore || _recomputePending)
                    continue;

                break;
            }
        }
        finally
        {
            _recomputeDepth--;
        }
    }

    /// <summary>
    /// Invokes <see cref="EffectiveStateChanged"/> subscribers one at a time,
    /// aborting the remaining invocation list as soon as a subscriber mutates
    /// the model (detected via <see cref="UIScreenStackModel.MutationGeneration"/>
    /// or <c>_recomputePending</c>). The outer <see cref="Recompute"/> loop then
    /// restarts from the current model so every subscriber eventually observes a
    /// state consistent with the active entry set.
    /// </summary>
    private void InvokeEffectiveStateChanged(
        UIScreenEffectiveState state,
        long generationBefore)
    {
        if (EffectiveStateChanged == null)
            return;

        var handlers = EffectiveStateChanged.GetInvocationList();
        foreach (var handler in handlers)
        {
            ((Action<UIScreenEffectiveState>)handler)(state);
            if (_model.MutationGeneration != generationBefore || _recomputePending)
                return;
        }
    }

    private void ApplyPausePolicy(bool pauseTree)
    {
        var tree = GetTree();
        if (pauseTree)
        {
            _pauseLease ??= new PauseLease(tree?.Paused ?? false);
            if (tree != null && !tree.Paused)
                tree.Paused = true;
            return;
        }

        if (_pauseLease == null)
            return;

        if (tree != null)
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
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects,
        long generationBefore)
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
                    ApplyControlEffect(handle, adapter, control, effect, generationBefore, effects);
                    break;
                case Window window:
                    ApplyWindowEffect(handle, adapter, window, effect, generationBefore);
                    break;
            }

            // A caller-provided callback during effect application (e.g.
            // SetInteractive closing the effect owner) may have mutated the
            // model. The resolved effects are now stale; abort so Recompute
            // can restart from the current model state.
            if (_model.MutationGeneration != generationBefore)
                return;
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

        if (target != null &&
            (!GodotObject.IsInstanceValid(target) || target.IsQueuedForDeletion()))
        {
            target = null;
        }

        if (_inTreeExiting || !IsInsideTree())
        {
            Callable.From(() => PlaceInputShield(target)).CallDeferred();
            return;
        }

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
        UILowerLayerPolicy effect,
        long generationBefore,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
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
            // Record the effect before the caller-provided callback so a
            // re-entrant close (SetInteractive closing this entry) lets
            // RestoreLowerLayerEffect see the correct applied value and
            // restore visibility/interactivity properly. This is provisional:
            // it is committed (re-recorded below) only after every operation
            // succeeds.
            _appliedLowerLayerEffects[handle] = effect;
            adapter.SetInteractive(false);
            // A caller-provided SetInteractive callback may close this (or
            // another) entry, mutating the model. If the target itself was
            // closed, the provisional marker stays for CloseAdapter's
            // RestoreLowerLayerEffect (which removes it). If the target is
            // still active (an unrelated entry mutated), drop the provisional
            // marker so Recompute reapplies the effect from a clean state
            // instead of skipping a committed-looking entry and leaving the
            // target visible/interactive despite a Hidden/VisibleInert policy.
            if (_model.MutationGeneration != generationBefore)
            {
                if (_adapters.ContainsKey(handle))
                    _appliedLowerLayerEffects.Remove(handle);
                return;
            }
        }

        // VisibleInert only disables pointer interaction via the InputShield;
        // revoke keyboard/joypad focus from any focused descendant so it
        // cannot be activated by ui_accept while inert. Pass the resolved
        // effects so the redirect target is selected from interactive
        // entries only, never from the inert subtree itself. Run on every
        // application (including reapply after a re-entrant abort) so a
        // previously-skipped revocation is not lost.
        _focusCoordinator.RevokeFocusWithin(control, effects);

        // RevokeFocusWithin calls GrabFocus/ReleaseFocus, which can
        // synchronously invoke application-owned FocusEntered or FocusExited
        // handlers. If such a handler closes this target (or another entry),
        // the model mutates. Abort before modifying visibility or committing
        // the effect. If the target was closed, CloseAdapter already restored
        // it and removed the marker; just don't re-add it. If the target
        // remains active, drop the provisional marker so Recompute reapplies
        // from a clean state instead of skipping a committed-looking entry.
        if (_model.MutationGeneration != generationBefore)
        {
            if (_adapters.ContainsKey(handle))
                _appliedLowerLayerEffects.Remove(handle);
            return;
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

        // Commit: the effect is fully applied.
        _appliedLowerLayerEffects[handle] = effect;
    }

    private void ApplyWindowEffect(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter,
        Window window,
        UILowerLayerPolicy effect,
        long generationBefore)
    {
        if (effect == UILowerLayerPolicy.VisibleInteractive)
        {
            RestoreLowerLayerEffect(handle, adapter);
            return;
        }

        if (!_windowEffectBaselines.TryGetValue(handle, out var baseline))
        {
            // IsPresented is a caller-provided callback that may re-enter the
            // host (e.g. close an entry), mutating the model. Capture the
            // baseline only if the target is still active afterwards; otherwise
            // abort so CloseAdapter's restoration is not overwritten.
            var presented = adapter.IsPresented();
            if (_model.MutationGeneration != generationBefore)
                return;
            baseline = new UIWindowEffectBaseline(
                presented,
                window.GuiDisableInput,
                window.Unfocusable);
            _windowEffectBaselines.Add(handle, baseline);
        }

        if (effect == UILowerLayerPolicy.Hidden)
        {
            window.GuiDisableInput = baseline.GuiDisableInput;
            window.Unfocusable = baseline.Unfocusable;
            // Record the effect before the caller-provided callback so a
            // re-entrant close (SetPresented closing this entry) lets
            // RestoreLowerLayerEffect see the correct applied value and
            // restore presentation properly.
            _appliedLowerLayerEffects[handle] = effect;
            adapter.SetPresented(false);
            // A caller-provided SetPresented callback may close this (or
            // another) entry, mutating the model. Abort before applying any
            // further bookkeeping so CloseAdapter's restoration is not
            // overwritten with a stale effect.
            if (_model.MutationGeneration != generationBefore)
                return;
        }
        else
        {
            if (_appliedLowerLayerEffects.TryGetValue(handle, out var applied) &&
                applied == UILowerLayerPolicy.Hidden)
            {
                // Record the effect before the caller-provided callback so a
                // re-entrant close (SetPresented closing this entry) lets
                // RestoreLowerLayerEffect see the correct applied value and
                // restore presentation properly. This is provisional: it is
                // committed (re-recorded below) only after every operation
                // succeeds.
                _appliedLowerLayerEffects[handle] = effect;
                adapter.SetPresented(baseline.Visible);
                // A caller-provided SetPresented callback may close this (or
                // another) entry, mutating the model. If the target itself was
                // closed, the provisional marker stays for CloseAdapter's
                // RestoreLowerLayerEffect (which removes it). If the target is
                // still active (an unrelated entry mutated), drop the provisional
                // marker so Recompute reapplies the effect from a clean state
                // instead of skipping a committed-looking entry and leaving the
                // Window visible but still accepting input (GuiDisableInput and
                // Unfocusable not yet set).
                if (_model.MutationGeneration != generationBefore)
                {
                    if (_adapters.ContainsKey(handle))
                        _appliedLowerLayerEffects.Remove(handle);
                    return;
                }
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
            var tree = GetTree();
            if (tree != null)
                tree.Paused = _pauseLease.IncomingPaused;
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

    /// <summary>
    /// Returns the committed reduced lower-layer effect for an active entry,
    /// or <see cref="UILowerLayerPolicy.VisibleInteractive"/> when the handle is
    /// unknown (e.g. before the first Recompute or after teardown). The focus
    /// coordinator uses this to avoid acquiring or restoring focus into a
    /// subtree that is currently <see cref="UILowerLayerPolicy.VisibleInert"/>
    /// or <see cref="UILowerLayerPolicy.Hidden"/>.
    /// </summary>
    internal UILowerLayerPolicy LowerLayerEffectFor(UIScreenHandle handle) =>
        _resolvedLowerLayerEffects.TryGetValue(handle, out var effect)
            ? effect
            : UILowerLayerPolicy.VisibleInteractive;

    internal void OnFocusRestorationCompleted()
    {
        if (_ready)
            Recompute();
    }
}

internal sealed record UIControlEffectBaseline(bool Visible, bool ProcessInputEnabled);
internal sealed record UIWindowEffectBaseline(bool Visible, bool GuiDisableInput, bool Unfocusable);
