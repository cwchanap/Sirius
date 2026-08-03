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
        var isPausedAfterOpen = ComputeIsPausedAfterOpen(normalized.Policy);
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
            // TryPrepare invoked the parent's caller-controlled FocusViewport
            // delegate (CaptureParentFocus) before sink attachment. That
            // delegate may have mutated the model — opened a committed
            // descendant beneath the candidate, freed the candidate view, or
            // closed an ancestor — and then sink attachment failed. A direct
            // _model.Close(handle) would cascade-remove a committed descendant
            // from the pure model but ignore its ClosedEntries, orphaning the
            // descendant's adapter, focus record, ownership metadata,
            // tree-exit handler, and applied effects; it would also skip the
            // Recompute that restores pause/cursor/HUD/input-ownership and
            // lower-layer effects to the state without the rejected candidate.
            // Route through RollbackPendingOpen for the same cascade-cleanup
            // and effect-restore reasons as the generation-change path below.
            // Distinguish cascade-removed (InvalidNode) from a non-cascade
            // sink-attachment failure (the original focusStatus).
            var cascadeRemoved = RollbackPendingOpen(handle, focusPreparation);
            return new(
                cascadeRemoved
                    ? UIScreenOpenStatus.InvalidNode
                    : focusStatus,
                null);
        }

        if (_model.MutationGeneration != generationBeforePrepare)
        {
            // The nested operation may already have completed a Recompute()
            // while the pending candidate was still present in _model. That
            // re-computation can apply or publish the pending candidate's
            // pause ownership, gameplay-input block, cursor/HUD policy,
            // top-input ownership, and lower-layer effects. A simple
            // _model.Close(handle) without Recompute leaves these effects
            // applied after the rejected candidate is gone. Worse, the
            // callback can inspect ActiveEntries, obtain the pending
            // candidate's handle, and open a logical child beneath it;
            // _model.Close(handle) cascade-removes both from the pure model
            // but its ClosedEntries are ignored, orphaning the nested
            // child's adapter, attached view, ownership metadata, focus
            // record, and tree-exit subscription. RollbackPendingOpen
            // cleans every cascade-removed adapter and focus entry, restores
            // applied effects via Recompute, and does not create a normal
            // restoration lease for a presentation that never committed.
            var cascadeRemoved = RollbackPendingOpen(handle, focusPreparation);
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
        // Snapshot the model generation before Apply(). Apply() runs
        // AddChild() which synchronously invokes the view's _EnterTree() and
        // _Ready(). Those lifecycle callbacks can re-enter the host: open
        // another entry or close an ancestor. Unlike the TryPrepare guard
        // (where the candidate is NOT in _adapters and re-entrant validation
        // skips it), here the candidate IS in _adapters — but a re-entrant
        // open's ValidateEffectAdaptersForOpen does NOT revalidate the
        // candidate's own process mode, and a re-entrant close can cascade
        // through the candidate while a re-entrant Recompute applies its
        // effects. The candidate's process-mode and effect validation ran
        // against the pre-Apply snapshot (pause state, inerting owners) which
        // is now stale. Detect any model mutation during Apply() and reject
        // the open instead of committing a candidate whose validation was
        // bypassed, using the same RollbackPendingOpen routine that cleans
        // cascade descendants and restores applied effects.
        var generationBeforeApply = _model.MutationGeneration;
        var applyStatus = adapter.Apply();
        if (applyStatus != UIScreenOpenStatus.Opened)
        {
            // Attachment failed. The adapter has already been rolled back
            // inside Apply() (RollbackRegistration set _finished, restored
            // the view's process mode, and detached it if it was attached).
            // Use RollbackPendingOpen to close the model entry, clean any
            // cascade-removed descendants (a _Ready() that opened a logical
            // child beneath the candidate before failing), and Recompute to
            // restore applied effects — rather than a direct _model.Close
            // that ignores ClosedEntries and leaves cascade descendants'
            // adapters, focus records, ownership metadata, and effects
            // orphaned. Distinguish cascade-removed (InvalidNode) from a
            // non-cascade failure (the original applyStatus).
            var cascadeRemoved = RollbackPendingOpen(handle, focusPreparation);
            return new(
                cascadeRemoved
                    ? UIScreenOpenStatus.InvalidNode
                    : applyStatus,
                null);
        }

        if (_model.MutationGeneration != generationBeforeApply)
        {
            // Apply()'s synchronous lifecycle callbacks (_EnterTree, _Ready)
            // mutated the model. The candidate's process-mode and effect
            // validation ran against the pre-Apply snapshot (pause state,
            // inerting owners) which is now potentially stale. Revalidate
            // the candidate's process mode and effect adapters against the
            // current state instead of committing a candidate whose
            // validation was bypassed. If revalidation passes, proceed; if
            // it fails, roll back the committed adapter and any cascade
            // descendants, and restore applied effects via Recompute.
            var revalidateStatus = RevalidateAfterApply(
                normalized.Policy, adapter, handle);
            if (revalidateStatus != UIScreenOpenStatus.Opened)
            {
                var cascadeRemoved = RollbackPendingOpen(handle, focusPreparation);
                return new(
                    cascadeRemoved
                        ? UIScreenOpenStatus.InvalidNode
                        : revalidateStatus,
                    null);
            }
        }

        // A re-entrant close from the view's _Ready() (synchronously invoked
        // during adapter.Apply() via AddChild) may have closed this handle and
        // removed its adapter registration even though Apply() returned Opened
        // and the generation did not change (a defensive guard —
        // UIScreenStackModel.Close always bumps MutationGeneration, so the
        // generation check above subsumes this case in normal operation).
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
            // Use RollbackPendingOpen for the same cascade-cleanup reasons
            // as the generation-change path above: a direct _model.Close
            // would ignore ClosedEntries and leave cascade descendants
            // orphaned.
            RollbackPendingOpen(handle, focusPreparation);
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        Action treeExiting = () => OnViewTreeExiting(handle);
        adapter.TreeExitingHandler = treeExiting;
        view.TreeExiting += treeExiting;
        // Register() invokes the candidate's own FocusViewport delegate, which
        // may synchronously re-enter the host and close the candidate, close an
        // ancestor (cascade-removing the candidate), or start teardown. In any
        // of those cases, the re-entrant close already ran the full
        // CloseAdapter path (removing the adapter, ownership metadata,
        // tree-exit handler, and restoring lower-layer effects) and called
        // Recompute() inside ProcessClose. Register() detects the liveness
        // failure, removes the DynamicSink, and returns false without adding
        // an orphan focus entry or scheduling initial focus. Without this
        // check, TryPresent() would call Recompute() again (harmless but
        // redundant) and return the original Opened result for a handle that
        // is no longer active — a stale handle the caller could try to close
        // or present a child beneath.
        //
        // Snapshot the model generation before Register. The FocusViewport
        // delegate can also mutate the host WITHOUT closing the candidate:
        // open a PauseTree owner (invalidating the candidate's Pausable
        // process mode that was assigned during Apply()), or queue the
        // candidate view for deletion (QueueFree does not fire TreeExiting
        // synchronously and does not bump MutationGeneration). After Register
        // returns true, revalidate node validity and liveness unconditionally,
        // and revalidate process mode + effect adapters when the model was
        // mutated — the same transactional validation used after _Ready()
        // (RevalidateAfterApply). Without this, TryPresent returns Opened for
        // a candidate whose process-mode or node-validity validation was
        // bypassed by the callback.
        var generationBeforeRegister = _model.MutationGeneration;
        if (!_focusCoordinator.Register(
                handle, adapter, normalized.Policy, focusPreparation))
            return new(UIScreenOpenStatus.InvalidNode, null);

        // Register() succeeded: the candidate now has a committed focus entry
        // (in _entries) and a scheduled deferred ApplyInitialFocus, in addition
        // to the adapter, ownership metadata, and tree-exit handler committed
        // earlier. Revalidate the node validity and liveness unconditionally —
        // a delegate that queued the view for deletion or closed the candidate
        // without a generation change (QueueFree, or a close path that did not
        // bump the generation) must be caught here. RollbackPendingOpen removes
        // the focus entry (via CloseEntry), tree-exit handler, adapter, and
        // ownership metadata, and restores lower-layer effects via Recompute.
        if (!GodotObject.IsInstanceValid(view) || view.IsQueuedForDeletion() ||
            !IsActive(handle) ||
            !_adapters.TryGetValue(handle, out var registeredAdapter) ||
            !ReferenceEquals(registeredAdapter, adapter))
        {
            RollbackPendingOpen(handle, focusPreparation);
            return new(UIScreenOpenStatus.InvalidNode, null);
        }

        // When the delegate mutated the model (e.g. opened a PauseTree owner),
        // the candidate's process mode and effect-adapter validation — which
        // ran against the pre-Register snapshot during Apply() — are now stale.
        // Revalidate against the current state, mirroring the post-Apply()
        // generation-change path. If revalidation fails, roll back the
        // committed candidate (focus entry, adapter, ownership, effects) and
        // return the failing status.
        if (_model.MutationGeneration != generationBeforeRegister)
        {
            var revalidateStatus = RevalidateAfterApply(
                normalized.Policy, adapter, handle);
            if (revalidateStatus != UIScreenOpenStatus.Opened)
            {
                var cascadeRemoved = RollbackPendingOpen(handle, focusPreparation);
                return new(
                    cascadeRemoved
                        ? UIScreenOpenStatus.InvalidNode
                        : revalidateStatus,
                    null);
            }
        }

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

    /// <summary>
    /// True when the host is ready and not tearing down — i.e. it can still
    /// accept new focus registrations. Used by the focus coordinator to reject
    /// a <see cref="UIScreenFocusCoordinator.Register"/> call whose
    /// caller-provided FocusViewport delegate synchronously started or
    /// finalized teardown (via <see cref="PrepareForTeardown"/>). A finalized
    /// teardown clears <c>_focusCoordinator._host</c> (null host); a
    /// started-but-deferred teardown leaves <c>_host</c> non-null but sets
    /// <c>_tearingDown</c>. Both must be treated as failure so Register() does
    /// not add an orphan focus entry or let TryPresent() return a stale
    /// Opened result for a host that is going away.
    /// </summary>
    internal bool IsHostOperational() => _ready && !_tearingDown;

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
            // A previous teardown was begun but deferred — most commonly
            // because rollback cleanup (RollbackPendingOpen) re-entered
            // PrepareForTeardown() while _drainingCloseQueue was held, so the
            // initial BeginTeardown() could not close entries and
            // FinalizeTeardown() returned early. RollbackPendingOpen's finally
            // clears _drainingCloseQueue but does not itself resume teardown.
            // Without closing remaining entries here, a later retry re-enters
            // this branch and only calls FinalizeTeardown(), which returns
            // early whenever an unrelated entry or parent remains active —
            // leaving teardown Deferred forever. Now that the mutation guard
            // has been released, resume the standard teardown drain (close the
            // top remaining entry, which loops through DrainCloseQueue until
            // all entries are gone) before finalizing.
            if (!_drainingCloseQueue && _model.Entries.Count != 0)
            {
                var top = _model.InputOrder[0].Handle;
                TryClose(top, UIScreenCloseReason.HostTeardown);
            }
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

    /// <summary>
    /// Rolls back a pending open whose TryPrepare callback mutated the model.
    /// Unlike <see cref="ProcessClose"/>, this does NOT start a focus
    /// restoration lease — the presentation never committed (no adapter, focus
    /// record, or view attachment for the candidate), so there is nothing to
    /// restore to. Cascade-removed descendants (e.g. a re-entrant child opened
    /// beneath the candidate during TryPrepare) ARE fully cleaned: adapter,
    /// focus entry, ownership metadata, tree-exit handler, and lower-layer
    /// effects. <see cref="Recompute"/> is called afterward so applied effects
    /// (pause, cursor, HUD, input ownership, lower-layer inerting) are restored
    /// to the state without the rejected candidate and its descendants.
    /// Returns true when the candidate was already cascade-removed by the
    /// re-entrant mutation (distinguished from a non-cascade mutation so the
    /// caller returns InvalidNode vs HostMutating).
    /// </summary>
    private bool RollbackPendingOpen(
        UIScreenHandle handle,
        UIFocusPreparation focusPreparation)
    {
        _focusCoordinator.DiscardPreparation(focusPreparation);
        // Check whether the candidate was already cascade-removed by the
        // re-entrant mutation BEFORE closing — after Close the candidate is
        // always gone and the distinction is lost.
        var cascadeRemoved = !IsActive(handle);
        // Run rollback under the same mutation/finalization guard as normal
        // close draining. CloseAdapter() invokes caller-provided Cleanup,
        // lower-effect restoration callbacks (SetInteractive, SetPresented),
        // and node lifecycle operations. Without _drainingCloseQueue, a
        // cleanup callback can synchronously re-enter TryPresent() (which
        // only checks _drainingCloseQueue) and open another entry instead of
        // receiving HostMutating, or call PrepareForTeardown() whose
        // FinalizeTeardown() would finalize and return Complete from inside
        // rollback cleanup — violating both guarantees: managed cleanup
        // cannot reopen during an active host transaction, and Complete means
        // no host work or publication remains afterward.
        _drainingCloseQueue = true;
        try
        {
            var mutation = _model.Close(handle);
            if (mutation.Status == UIScreenCloseStatus.Closed)
            {
                foreach (var closed in mutation.ClosedEntries)
                {
                    if (closed.Handle == handle)
                    {
                        // The pending candidate was never fully committed —
                        // no tree-exit handler (registered after all rollback
                        // paths) and no focus registration. The public
                        // contract says rejected opens are atomic no-ops:
                        // do NOT invoke Cleanup or apply NodeLifetime
                        // (Hide/QueueFree/External detach). Remove the adapter
                        // and ownership metadata, restore lower-layer
                        // effects, and call RollbackRegistration (which
                        // restores the process mode and detaches only if the
                        // host attached the view, preserving a caller-
                        // preparented view).
                        RollbackPendingCandidate(closed.Handle);
                    }
                    else
                    {
                        // Cascade descendants were fully committed (complete
                        // TryPresent flow): full terminal close with Cleanup,
                        // NodeLifetime, and focus release.
                        var focusState = CloseAdapter(
                            closed.Handle,
                            UIScreenCloseReason.ParentClosed);
                        // Release focus on cascade-removed descendants
                        // without starting a restoration lease — the
                        // presentation never committed.
                        _focusCoordinator.ReleaseFocusWithoutRestoration(focusState);
                    }
                }
            }
            // Recompute so applied effects (pause ownership, gameplay-input
            // block, cursor/HUD policy, top-input ownership, lower-layer
            // effects) are restored to the state without the rejected
            // candidate and its descendants. Without this, a re-entrant
            // Recompute that ran while the pending candidate was still in
            // _model can leave its effects applied after the candidate is
            // gone.
            Recompute();
            // Drain any close requests queued by cleanup callbacks during
            // the rollback above. TryClose under _drainingCloseQueue queues
            // and returns Closed, but without this drain the queued entry
            // remains active indefinitely and a later TryClose for the same
            // handle returns AlreadyClosed (it remains in
            // _queuedCloseHandles). Process each queued request through the
            // normal close transaction so the unrelated entry is fully
            // closed before the outer rejected TryPresent returns.
            while (_closeQueue.Count != 0)
            {
                var request = _closeQueue.Dequeue();
                _queuedCloseHandles.Remove(request.Handle);
                ProcessClose(request);
            }
        }
        finally
        {
            _drainingCloseQueue = false;
            // Keep _queuedCloseHandles consistent with whatever remains in
            // the queue, mirroring DrainCloseQueue's finally. The drain loop
            // above should have emptied the queue, but this is defensive.
            if (_closeQueue.Count == 0)
                _queuedCloseHandles.Clear();
            _closingHandles.Clear();
        }
        return cascadeRemoved;
    }

    /// <summary>
    /// Rolls back a pending candidate that was never fully committed (no
    /// tree-exit handler, no focus registration). Removes the adapter and
    /// ownership metadata, restores lower-layer effects, and calls
    /// <see cref="UIScreenViewAdapter.RollbackRegistration"/> (which restores
    /// the process mode and detaches only if the host attached the view,
    /// preserving a caller-preparented view). Does NOT invoke Cleanup or
    /// apply NodeLifetime — rejected opens are atomic no-ops per the public
    /// contract.
    /// </summary>
    private void RollbackPendingCandidate(UIScreenHandle handle)
    {
        var focusState = _focusCoordinator.CloseEntry(handle);
        _focusCoordinator.ReleaseFocusWithoutRestoration(focusState);

        if (!_adapters.Remove(handle, out var adapter))
            return;

        RestoreLowerLayerEffect(handle, adapter);

        if (GodotObject.IsInstanceValid(adapter.View))
        {
            if (adapter.TreeExitingHandler != null)
                adapter.View.TreeExiting -= adapter.TreeExitingHandler;
            ReleaseOwnership(adapter.View);
        }

        adapter.RollbackRegistration();
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
        UIScreenViewAdapter candidateAdapter,
        UIScreenHandle? candidateHandle = null)
    {
        // During initial validation (candidateHandle is null), the candidate
        // is NOT yet in _model, so the policy-only IsCandidateVisuallyAbove
        // comparison is sufficient. During post-Apply() revalidation
        // (candidateHandle is set), the candidate IS in _model with a
        // sequence. Look up its snapshot so we can skip it (a screen does
        // not apply its own LowerLayers policy to itself) and use
        // sequence-aware IsVisuallyAbove comparisons that catch same-layer
        // owners opened during _Ready() with a higher sequence.
        UIScreenEntrySnapshot? candidateSnapshot = null;
        if (candidateHandle.HasValue)
        {
            foreach (var entry in _model.Entries)
            {
                if (entry.Handle == candidateHandle.Value)
                {
                    candidateSnapshot = entry;
                    break;
                }
            }
        }

        foreach (var target in _model.Entries)
        {
            // Skip the candidate itself during revalidation — a screen does
            // not apply its own LowerLayers policy to itself. Without this,
            // a candidate declaring VisibleInert is falsely rejected because
            // CanApply checks the candidate's own adapter against its own
            // policy.
            if (candidateSnapshot != null && target.Handle == candidateHandle!.Value)
                continue;

            var isAbove = candidateSnapshot != null
                ? IsVisuallyAbove(candidateSnapshot, target)
                : IsCandidateVisuallyAbove(candidatePolicy, target.Policy);
            if (isAbove &&
                _adapters.TryGetValue(target.Handle, out var targetAdapter) &&
                !targetAdapter.CanApply(candidatePolicy.LowerLayers))
            {
                return UIScreenOpenStatus.MissingRequiredAdapter;
            }
        }

        foreach (var owner in _model.Entries)
        {
            // Skip the candidate itself — it cannot be its own inerting owner.
            if (candidateSnapshot != null && owner.Handle == candidateHandle!.Value)
                continue;

            // During revalidation, use sequence-aware comparison so same-layer
            // owners opened during _Ready() with a higher sequence are
            // detected. Without this, a same-layer owner with VisibleInert
            // is silently skipped, and the candidate is falsely accepted even
            // though it cannot be inerted.
            var ownerIsAbove = candidateSnapshot != null
                ? IsVisuallyAbove(owner, candidateSnapshot)
                : owner.Policy.Layer > candidatePolicy.Layer;
            if (ownerIsAbove &&
                !candidateAdapter.CanApply(
                    owner.Policy.LowerLayers,
                    requireControlInteractivityAdapter: true))
            {
                return UIScreenOpenStatus.MissingRequiredAdapter;
            }
        }

        return UIScreenOpenStatus.Opened;
    }

    /// <summary>
    /// Revalidates the candidate's process mode and effect adapters against
    /// the current model state after adapter.Apply()'s synchronous lifecycle
    /// callbacks (_EnterTree, _Ready) mutated the host. The process mode was
    /// selected before attachment in TryCreate but assigned on the view only
    /// after _Ready() returns; if _Ready() opened a PauseTree owner, the
    /// candidate's Pausable mode may now be invalid. Similarly, a new owner
    /// may have appeared on a higher layer with inerting lower-layer effects
    /// that the candidate's adapters cannot satisfy. Returns Opened when both
    /// revalidations pass (updating the registered process mode if it
    /// changed), or the failing status otherwise.
    /// </summary>
    private UIScreenOpenStatus RevalidateAfterApply(
        UIScreenEntryPolicy candidatePolicy,
        UIScreenViewAdapter candidateAdapter,
        UIScreenHandle candidateHandle)
    {
        // Recompute isPausedAfterOpen against the current model state — a
        // _Ready() callback may have opened a PauseTree owner.
        var isPausedAfterOpen = ComputeIsPausedAfterOpen(candidatePolicy);
        var hasPauseBoundedLifetime = HasPauseBoundedLifetime(candidatePolicy);

        var processStatus = candidateAdapter.RevalidateProcessMode(
            isPausedAfterOpen,
            hasPauseBoundedLifetime);
        if (processStatus != UIScreenOpenStatus.Opened)
            return processStatus;

        return ValidateEffectAdaptersForOpen(candidatePolicy, candidateAdapter, candidateHandle);
    }

    private bool ComputeIsPausedAfterOpen(UIScreenEntryPolicy candidatePolicy)
    {
        if (candidatePolicy.PauseTree)
            return true;
        foreach (var active in _model.Entries)
        {
            if (active.Policy.PauseTree)
                return true;
        }
        return false;
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
