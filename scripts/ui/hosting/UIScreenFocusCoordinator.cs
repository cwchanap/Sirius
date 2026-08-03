using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Godot;

internal sealed record UIFocusRecord(
    Viewport Viewport,
    Control? FocusOwner,
    UIScreenHandle ParentHandle);

internal sealed record UIFocusRestorationLease(
    long Generation,
    UIScreenHandle ClosedHandle);

internal sealed record UIFocusPreparation(
    Control? DynamicSink,
    UIFocusRecord? ParentRecord);

internal sealed record UIFocusCloseState(
    UIScreenHandle Handle,
    UIScreenViewAdapter? Adapter,
    UIFocusRecord? ParentRecord,
    Viewport? ClosedViewport,
    bool RequiresRestoration);

internal sealed class UIScreenFocusCoordinator
{
    private const string DynamicSinkName = "__UIScreenFocusSink";

    private static readonly IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy>
        EmptyEffects = new ReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy>(
            new Dictionary<UIScreenHandle, UILowerLayerPolicy>(0));

    private readonly Dictionary<UIScreenHandle, FocusEntry> _entries = new();
    private UIScreenHost? _host;
    private Control? _rootSink;
    private UIFocusRestorationLease? _activeLease;
    private UIFocusCloseState? _activeCloseState;
    private long _nextGeneration;

    public bool IsRestorationPending => _activeLease != null;
    public UIScreenFocusRestorationDiagnostics? RestorationLease =>
        _activeLease == null
            ? null
            : new UIScreenFocusRestorationDiagnostics(
                _activeLease.Generation,
                _activeLease.ClosedHandle);

    public IReadOnlyList<UIScreenFocusStateDiagnostics> SnapshotDiagnostics(
        IReadOnlyList<UIScreenEntrySnapshot> inputOrder)
    {
        var diagnostics = new List<UIScreenFocusStateDiagnostics>(inputOrder.Count);
        foreach (var snapshot in inputOrder)
        {
            if (!_entries.TryGetValue(snapshot.Handle, out var entry))
                continue;

            // Use the viewport committed at registration time. Diagnostics are
            // documented as read-only, so a diagnostic getter must not invoke
            // the caller-provided FocusViewport delegate, which can
            // synchronously open/close entries, run cleanup, change pause and
            // lower-layer effects, and return a snapshot based on the
            // now-stale inputOrder. The rest of this coordinator correctly
            // treats FocusViewport as callback-capable and re-entrant; a
            // diagnostic read cannot safely cross that boundary without a
            // transaction. GuiGetFocusOwner is a Godot API query, not user
            // code, so it is safe to call on the committed viewport.
            var viewport = entry.CommittedViewport;
            if (viewport == null || !GodotObject.IsInstanceValid(viewport))
                continue;

            var focusOwner = viewport.GuiGetFocusOwner();
            var sink = entry.Policy.InputPriority == UIInputPriority.Blocking
                ? entry.DynamicSink ?? _rootSink
                : null;
            diagnostics.Add(new UIScreenFocusStateDiagnostics(
                snapshot.Handle,
                viewport.GetInstanceId(),
                ValidInstanceId(focusOwner),
                ValidInstanceId(sink),
                sink != null && focusOwner == sink));
        }

        return diagnostics.AsReadOnly();
    }

    public void Bind(UIScreenHost host, Control rootSink)
    {
        _host = host;
        _rootSink = rootSink;
    }

    public UIScreenOpenStatus TryPrepare(
        UIScreenViewAdapter adapter,
        UIScreenEntryPolicy policy,
        out UIFocusPreparation preparation)
    {
        Control? dynamicSink = null;
        var parentRecord = CaptureParentFocus(policy.Parent);
        if (policy.InputPriority != UIInputPriority.Blocking ||
            adapter.View is not Window window)
        {
            preparation = new UIFocusPreparation(null, parentRecord);
            return UIScreenOpenStatus.Opened;
        }

        // Capture a stable diagnostic name before sink attachment. The
        // parent's FocusViewport delegate (invoked above by CaptureParentFocus)
        // is caller-controlled and may have freed the candidate Window — or
        // otherwise mutated the host — before sink attachment runs. Without a
        // pre-captured name, the catch below dereferences adapter.View.Name on
        // the freed Godot object and throws a second exception while handling
        // the AddChild failure, bypassing the MissingRequiredAdapter fallback
        // and stranding the surrounding rollback or restoration logic.
        var view = adapter.View;
        var diagnosticName = GodotObject.IsInstanceValid(view)
            ? (string)view.Name
            : "<freed>";
        try
        {
            dynamicSink = CreateSink();
            window.AddChild(dynamicSink);
            preparation = new UIFocusPreparation(dynamicSink, parentRecord);
            return UIScreenOpenStatus.Opened;
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"UIScreenHost could not create a focus sink for '{diagnosticName}': {exception.Message}");
            RemoveSink(dynamicSink);
            preparation = new UIFocusPreparation(null, parentRecord);
            return UIScreenOpenStatus.MissingRequiredAdapter;
        }
    }

    public bool Register(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter,
        UIScreenEntryPolicy policy,
        UIFocusPreparation preparation)
    {
        // Resolve the focus viewport once, at registration (a mutation point),
        // and commit it on the focus entry. SnapshotDiagnostics uses this
        // committed value instead of re-invoking the caller-provided
        // FocusViewport delegate, so a diagnostic read cannot synchronously
        // mutate the host. SafeFocusViewport captures a stable diagnostic
        // name and tolerates a delegate that frees its view or throws.
        var committedViewport = SafeFocusViewport(adapter);
        // SafeFocusViewport invoked the caller-provided FocusViewport delegate,
        // which may have synchronously re-entered the host and closed the
        // candidate (e.g. by finding it through ActiveEntries and calling
        // TryClose), closed an ancestor (cascade-removing the candidate), or
        // started teardown. By the time Register() runs, the host has already
        // committed the model entry, adapter, ownership metadata, and
        // tree-exit handler — so a re-entrant close runs the full
        // CloseAdapter/CloseEntry path. But CloseEntry finds no focus entry
        // (Register() has not added it yet) and returns a no-op close state,
        // leaving the DynamicSink attached and never cleaning up a focus entry
        // that is about to be created. Without this liveness check, Register()
        // would add an orphan focus entry for a handle that is no longer in
        // the model, and TryPresent() would return the original Opened result
        // without detecting that the candidate was closed during registration.
        // Remove the sink that TryPrepare created (CloseEntry won't find a
        // focus entry to remove it from) and return false so the caller can
        // return InvalidNode without scheduling initial focus or recomputing.
        //
        // The guard must also treat a null or non-operational host as failure.
        // If the delegate called PrepareForTeardown() and finalization
        // completed, _focusCoordinator.Teardown() sets _host to null and
        // clears _entries. The previous guard (`_host != null && !IsActive`)
        // short-circuited to false on the null host, so Register() added an
        // orphan focus entry after teardown, scheduled initial focus, and
        // let TryPresent() return a stale Opened result. A teardown that
        // started but has not finalized (_tearingDown true, _host non-null)
        // is equally invalid: the host is going away and must not accept new
        // focus state. IsHostOperational() covers both the finalized
        // (null _host) and started-but-deferred teardown cases.
        if (_host == null || !_host.IsHostOperational() || !_host.IsActive(handle))
        {
            RemoveSink(preparation.DynamicSink);
            return false;
        }
        _entries.Add(handle, new FocusEntry(
            adapter,
            policy,
            preparation.DynamicSink,
            preparation.ParentRecord,
            committedViewport));
        if (policy.InputPriority != UIInputPriority.Passive)
            Callable.From(() => ApplyInitialFocus(handle)).CallDeferred();
        return true;
    }

    public void DiscardPreparation(UIFocusPreparation preparation) =>
        RemoveSink(preparation.DynamicSink);

    /// <summary>
    /// Releases focus on the closed viewport (and its parent record's viewport)
    /// without starting a restoration lease. Used during rollback of a pending
    /// open that never committed — there is nothing to restore to, but focus
    /// must not remain on a hidden/freed control inside a cascade-removed
    /// descendant.
    /// </summary>
    public void ReleaseFocusWithoutRestoration(UIFocusCloseState? closeState)
    {
        if (closeState == null || !closeState.RequiresRestoration)
            return;
        ReleaseFocus(closeState.ClosedViewport);
        if (closeState.ParentRecord?.Viewport != closeState.ClosedViewport)
            ReleaseFocus(closeState.ParentRecord?.Viewport);
    }

    public UIFocusCloseState CloseEntry(UIScreenHandle handle)
    {
        if (!_entries.Remove(handle, out var entry))
            return new UIFocusCloseState(handle, null, null, null, false);

        var viewport = SafeFocusViewport(entry.Adapter);
        RemoveSink(entry.DynamicSink);
        return new UIFocusCloseState(
            handle,
            entry.Adapter,
            entry.ParentRecord,
            viewport,
            entry.Policy.InputPriority != UIInputPriority.Passive);
    }

    /// <summary>
    /// Ensures no focused Control remains within <paramref name="inertControl"/>
    /// after it becomes <see cref="UILowerLayerPolicy.VisibleInert"/>. The
    /// InputShield only blocks pointer interaction; a focused descendant Button,
    /// LineEdit, or other Control can still receive keyboard/joypad GUI events
    /// (Godot routes those to the focus owner after the _Input phase). When the
    /// inert subtree has no valid redirect target, focus is released outright.
    /// <para>
    /// The redirect target is selected only from entries whose reduced
    /// lower-layer effect is <see cref="UILowerLayerPolicy.VisibleInteractive"/>
    /// and whose view is not inside <paramref name="inertControl"/>. This
    /// prevents focus from being redirected back into the inert subtree when a
    /// lower <see cref="UIInputPriority.Blocking"/> entry is visually inerted by
    /// an upper entry but remains first in logical input order.
    /// </para>
    /// </summary>
    public void RevokeFocusWithin(
        Control inertControl,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        if (_host == null || !GodotObject.IsInstanceValid(inertControl))
            return;

        var viewport = inertControl.GetViewport();
        if (viewport == null)
            return;

        var focusOwner = viewport.GuiGetFocusOwner();
        if (focusOwner == null || !GodotObject.IsInstanceValid(focusOwner))
            return;

        if (!IsSameOrAncestor(inertControl, focusOwner))
            return;

        // Redirect to the top interactive entry's first focusable descendant or
        // sink so the inert subtree cannot keep receiving keyboard/joypad GUI
        // events that the InputShield (pointer-only) does not block.
        var top = FindTopInteractiveEntry(effects, inertControl);
        if (top != null)
        {
            var topViewport = SafeFocusViewport(top.Adapter);
            if (topViewport != null)
            {
                var descendant = FindFirstFocusableDescendant(
                    top.Adapter.View, topViewport, top.DynamicSink);
                if (descendant != null && !IsSameOrAncestor(inertControl, descendant))
                {
                    if (TryFocus(descendant, topViewport))
                    {
                        if (topViewport != viewport)
                            ReleaseFocus(viewport);
                        return;
                    }
                }

                if (top.Policy.InputPriority == UIInputPriority.Blocking)
                {
                    var sink = top.DynamicSink ?? _rootSink;
                    if (sink != null && !IsSameOrAncestor(inertControl, sink))
                    {
                        if (TryFocus(sink, topViewport))
                        {
                            if (topViewport != viewport)
                                ReleaseFocus(viewport);
                            return;
                        }
                    }
                }
            }
        }

        // No valid redirect target: release focus so the inert descendant can
        // no longer be activated by ui_accept / joypad GUI events. Re-query the
        // current owner rather than reusing the stale focusOwner captured above
        // (SafeFocusViewport or a redirect attempt may have moved or freed it),
        // but release it only when it is still inside the inert subtree. A
        // FocusViewport callback that moved focus to a valid control outside
        // the inert subtree must keep that focus — releasing it would strand
        // keyboard/controller navigation without an owner.
        var currentOwner = viewport.GuiGetFocusOwner();
        if (currentOwner != null &&
            GodotObject.IsInstanceValid(currentOwner) &&
            IsSameOrAncestor(inertControl, currentOwner))
        {
            currentOwner.ReleaseFocus();
        }
    }

    /// <summary>
    /// Finds the top non-passive entry whose reduced lower-layer effect is
    /// <see cref="UILowerLayerPolicy.VisibleInteractive"/> and whose view is
    /// not inside <paramref name="inertControl"/>. Unlike
    /// <see cref="FindTopEntry"/>, this respects visual/effect state rather
    /// than logical input order alone, preventing a Blocking lower entry that
    /// is visually inerted from being selected as a redirect target.
    /// </summary>
    private FocusEntry? FindTopInteractiveEntry(
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects,
        Control inertControl)
    {
        if (_host == null)
            return null;

        foreach (var snapshot in _host.FocusInputOrder())
        {
            if (snapshot.Policy.InputPriority == UIInputPriority.Passive)
                continue;
            if (!effects.TryGetValue(snapshot.Handle, out var effect) ||
                effect != UILowerLayerPolicy.VisibleInteractive)
                continue;
            if (!_entries.TryGetValue(snapshot.Handle, out var entry))
                continue;
            // Skip entries whose view is inside the inert subtree. The effects
            // check above already filters the inerted entry, but this also
            // covers any descendant whose view is nested inside inertControl,
            // including Window and AcceptDialog adapter roots (not just Control).
            if (IsSameOrAncestor(inertControl, entry.Adapter.View))
                continue;
            return entry;
        }

        return null;
    }

    private static bool IsSameOrAncestor(Node ancestor, Node descendant)
    {
        var current = descendant;
        while (current != null)
        {
            if (current == ancestor)
                return true;
            current = current.GetParent();
        }
        return false;
    }

    public void BeginRestoration(
        UIFocusCloseState closeState,
        bool scheduleDeferred = true)
    {
        if (!closeState.RequiresRestoration)
            return;

        if (_activeLease != null)
        {
            CompleteRestoration(
                _activeLease.Generation,
                _activeLease.ClosedHandle,
                notifyHost: false);
        }

        var generation = ++_nextGeneration;
        _activeLease = new UIFocusRestorationLease(generation, closeState.Handle);
        _activeCloseState = closeState;
        if (scheduleDeferred)
        {
            Callable.From(() => CompleteRestoration(generation, closeState.Handle))
                .CallDeferred();
        }
    }

    public void CompleteActiveRestoration(bool propagateProviderExceptions = false)
    {
        if (_activeLease != null)
        {
            CompleteRestoration(
                _activeLease.Generation,
                _activeLease.ClosedHandle,
                propagateProviderExceptions: propagateProviderExceptions);
        }
    }

    public void Teardown()
    {
        foreach (var entry in _entries.Values)
            RemoveSink(entry.DynamicSink);
        _entries.Clear();
        _activeLease = null;
        _activeCloseState = null;
        _rootSink = null;
        _host = null;
    }

    private void CompleteRestoration(
        long generation,
        UIScreenHandle closedHandle,
        bool notifyHost = true,
        bool propagateProviderExceptions = false)
    {
        var completed = false;
        try
        {
            if (_activeLease?.Generation != generation)
                return;

            RestoreBestAvailableTarget(closedHandle, propagateProviderExceptions);
            completed = true;
        }
        finally
        {
            if (completed && _activeLease?.Generation == generation)
            {
                _activeLease = null;
                _activeCloseState = null;
                if (notifyHost)
                    _host?.OnFocusRestorationCompleted();
            }
        }
    }

    private void RestoreBestAvailableTarget(
        UIScreenHandle closedHandle,
        bool propagateProviderExceptions)
    {
        if (_activeCloseState?.Handle != closedHandle)
            return;

        var closeState = _activeCloseState;
        // Snapshot the restoration lease generation so that every
        // callback-capable stage can detect supersession. A caller-provided
        // RestoreFocus / FocusViewport / InitialFocus delegate may
        // synchronously close another entry, which installs a newer
        // restoration lease (BeginRestoration completes this one first, then
        // replaces _activeLease). Without a generation check after each
        // delegate, the outer — now-stale — restoration continues focusing
        // a target that belongs to a superseded transaction.
        var leaseGeneration = _activeLease?.Generation ?? -1;

        // Restoration selection runs as a restart loop. Lower-layer effects
        // are resolved from the current model at the top of each iteration
        // and re-resolved after any provider callback that bumps the model
        // generation (RestoreFocus, FocusViewport, InitialFocus). A provider
        // that opens a visually higher owner — e.g. a Modal that inerts a
        // lower Blocking entry — changes which entry FindTopEntry /
        // CurrentTopInputOwner should select: the frozen pre-callback
        // snapshot would still mark the now-inert Blocking entry
        // VisibleInteractive while the live input order still ranks it first
        // (Blocking outranks Modal), so selection would focus into the inert
        // subtree beneath the new owner. Re-resolving effects keeps the
        // snapshot coherent with the live input order, and the top-entry
        // stage restarts (re-runs FindTopEntry) when its FocusViewport
        // callback mutates the model so the new higher owner is selected
        // instead of the now-inert entry. Provider delegates that have
        // already run are NOT re-invoked on restart (RestoreFocus and the
        // parent InitialFocus path run once); only the model-based selection
        // (FindTopEntry / validation / focus) re-runs against the fresh
        // snapshot. A restart cap bounds pathological providers that mutate
        // on every call.
        var restoreFocusAttempted = false;
        var parentInitialPathAttempted = false;
        const int MaxRestarts = 8;
        var restarts = 0;

        while (true)
        {
            if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                return;

            // Resolve lower-layer effects freshly from the current model for
            // this iteration. ProcessClose calls BeginRestoration BEFORE
            // Recompute, so the host's committed _resolvedLowerLayerEffects
            // still reflects the pre-close state while the live model has
            // already removed the closing owner. Reading the stale committed
            // snapshot (LowerLayerEffectFor) would disagree with the model.
            // The snapshot is refreshed again after any provider callback
            // that mutates the model; live revalidation
            // (IsRestorationStillActive, _entries.ContainsKey) still guards
            // against supersession and registration changes.
            var effects = _host != null
                ? _host.ResolveCurrentLowerLayerEffects()
                : EmptyEffects;
            var generation = _host?.MutationGeneration ?? -1;

            // Re-resolve the effect snapshot when a provider callback bumped
            // the model generation, so owner/entry selection and target
            // validation use a view coherent with the live input order.
            void RefreshEffectsIfStale()
            {
                if (_host == null)
                    return;
                var now = _host.MutationGeneration;
                if (now == generation)
                    return;
                generation = now;
                effects = _host.ResolveCurrentLowerLayerEffects();
            }

            // --- Explicit RestoreFocus path (callback-capable, runs once) ---
            if (!restoreFocusAttempted)
            {
                restoreFocusAttempted = true;
                var explicitTarget = SafeTarget(
                    closeState.Adapter?.RestoreFocus,
                    propagateProviderExceptions);
                // RestoreFocus is caller-controlled and can synchronously
                // mutate the host: close another entry (installing a newer
                // restoration lease) or open a new higher owner that inerts a
                // lower entry. Revalidate that this restoration is still
                // active, refresh the effect snapshot, then validate the
                // target is still interactive and belongs to the current top
                // owner's subtree before focusing. Without these checks, the
                // old restoration focuses a target that belongs to a
                // superseded transaction or lands outside/beneath the top
                // owner until a later deferred initial-focus callback
                // corrects it — a transient state observable via FocusEntered
                // side effects.
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
                RefreshEffectsIfStale();
                if (explicitTarget != null &&
                    IsControlEffectivelyInteractive(explicitTarget, effects) &&
                    !TargetOutsideNewTopOwner(explicitTarget, effects) &&
                    TryFocus(explicitTarget, explicitTarget.GetViewport()))
                {
                    return;
                }
                // If the explicit delegate superseded this restoration, abort
                // — the newer restoration handles focus. Do not release focus
                // or run further paths; the newer lease's deferred completion
                // owns the focus state.
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
            }

            // --- Parent focus-owner path (no delegate, no supersession risk) ---
            // A top owner may be present either because one opened during the
            // explicit RestoreFocus delegate above, or because one opened
            // after TryClose returned but before this deferred callback began.
            // Without a TargetOutsideNewTopOwner check, this path focuses the
            // parent's previously-captured FocusOwner beneath the current top
            // owner. Re-runs on restart with the fresh snapshot so a newly
            // opened higher owner's inerting is respected.
            var parentRecord = closeState.ParentRecord;
            if (parentRecord != null &&
                _entries.ContainsKey(parentRecord.ParentHandle) &&
                IsHandleEffectivelyInteractive(parentRecord.ParentHandle, effects) &&
                !TargetOutsideNewTopOwner(parentRecord.FocusOwner, effects) &&
                TryFocus(parentRecord.FocusOwner, parentRecord.Viewport))
            {
                return;
            }

            // --- Parent initial-focus path (callback-capable, runs once) ---
            if (!parentInitialPathAttempted &&
                parentRecord != null &&
                _entries.TryGetValue(parentRecord.ParentHandle, out var parentEntry) &&
                IsHandleEffectivelyInteractive(parentRecord.ParentHandle, effects))
            {
                parentInitialPathAttempted = true;
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
                var parentViewport = SafeFocusViewport(parentEntry.Adapter);
                // SafeFocusViewport invoked the caller-provided FocusViewport
                // delegate, which may re-enter the host and close the parent,
                // open a new top owner that inerts it, or close another entry
                // that supersedes this restoration. Revalidate the
                // restoration lease and refresh the effect snapshot before
                // invoking InitialFocus; otherwise the stale callback may
                // mutate domain/UI state, open another screen, dereference
                // controls belonging to a closed view, or throw during
                // teardown finalization. Mirrors the post-delegate
                // revalidation ApplyInitialFocus performs between
                // FocusViewport and InitialFocus.
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
                RefreshEffectsIfStale();
                if (parentViewport != null &&
                    _entries.TryGetValue(parentRecord.ParentHandle, out parentEntry) &&
                    IsHandleEffectivelyInteractive(parentRecord.ParentHandle, effects))
                {
                    var parentInitial = SafeTarget(
                        parentEntry.Adapter.InitialFocus,
                        propagateProviderExceptions);
                    // SafeTarget invoked the caller-provided InitialFocus
                    // delegate, which can mutate the host the same way
                    // FocusViewport can. Revalidate again and refresh the
                    // effect snapshot before focusing the returned target.
                    if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                        return;
                    RefreshEffectsIfStale();
                    if (IsRestorationStillActive(leaseGeneration, closedHandle) &&
                        _entries.ContainsKey(parentRecord.ParentHandle) &&
                        IsHandleEffectivelyInteractive(parentRecord.ParentHandle, effects) &&
                        !TargetOutsideNewTopOwner(parentInitial, effects) &&
                        TryFocus(parentInitial, parentViewport))
                    {
                        return;
                    }
                }
                // If the parent delegate superseded this restoration, abort.
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
            }

            // --- Generic top entry path (callback-capable, restartable) ---
            var top = FindTopEntry(effects);
            if (top != null)
            {
                var topHandle = top.Value.Handle;
                var topEntry = top.Value.Entry;
                var viewport = SafeFocusViewport(topEntry.Adapter);
                // SafeFocusViewport invoked the caller-provided FocusViewport
                // delegate, which may re-enter the host and inert or close
                // the entry that FindTopEntry selected, open a new higher
                // owner that inerts it, or close another entry that
                // supersedes this restoration. Revalidate the restoration
                // lease, then refresh the effect snapshot. If the model
                // mutated, restart the selection loop so FindTopEntry re-runs
                // against the fresh snapshot and the new higher owner is
                // selected instead of the now-inert entry; otherwise
                // restoration can land inside a subtree that was inerted by
                // the delegate, or focus after supersession.
                if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                    return;
                if (_host != null && _host.MutationGeneration != generation)
                {
                    RefreshEffectsIfStale();
                    if (++restarts < MaxRestarts)
                        continue;
                    // Cap reached: a provider keeps mutating on every
                    // callback. Release focus rather than risk focusing into
                    // an inconsistent subtree after unbounded re-entrant
                    // opens.
                    if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                        return;
                    break;
                }
                if (viewport != null &&
                    IsRestorationStillActive(leaseGeneration, closedHandle) &&
                    _entries.ContainsKey(topHandle) &&
                    IsHandleEffectivelyInteractive(topHandle, effects))
                {
                    var descendant = FindFirstFocusableDescendant(
                        topEntry.Adapter.View,
                        viewport,
                        topEntry.DynamicSink);
                    // SafeFocusViewport may have opened a new top owner that
                    // outranks the selected top entry. Without this check,
                    // restoration focuses a descendant of the now-outranked
                    // top entry (or its DynamicSink) beneath the new top
                    // owner — a transient state observable via FocusEntered
                    // side effects and keyboard/controller input until the
                    // new owner's deferred initial-focus callback corrects
                    // it.
                    if (!TargetOutsideNewTopOwner(descendant, effects) &&
                        TryFocus(descendant, viewport))
                        return;

                    // The sink (DynamicSink for Window entries, _rootSink for
                    // Blocking Control entries) is the top entry's own focus
                    // mechanism, not a view descendant. _rootSink is outside
                    // every entry's view subtree by definition, so
                    // TargetOutsideNewTopOwner would always reject it when
                    // any top owner exists — breaking the sink fallback for
                    // Blocking Control top entries. Only reject the sink when
                    // a NEW top owner appeared that outranks the selected top
                    // entry (i.e. the top entry is no longer the current top
                    // owner). When the top entry IS the top owner, its sink
                    // is always a legitimate focus target. Use the
                    // fresh-snapshot CurrentTopInputOwner rather than
                    // CurrentState.TopInputOwner, which may be stale inside a
                    // pre-Recompute close transaction where a superseded
                    // restoration lease is completed synchronously.
                    var sink = topEntry.DynamicSink ?? _rootSink;
                    var topEntryIsTopOwner = CurrentTopInputOwner(effects) == topHandle;
                    if (topEntry.Policy.InputPriority == UIInputPriority.Blocking &&
                        (topEntryIsTopOwner || !TargetOutsideNewTopOwner(sink, effects)) &&
                        TryFocus(sink, viewport))
                    {
                        return;
                    }
                }
            }

            // If the top delegate superseded this restoration, abort before
            // releasing focus — the newer restoration owns the focus state.
            if (!IsRestorationStillActive(leaseGeneration, closedHandle))
                return;

            break;
        }

        ReleaseFocus(closeState.ClosedViewport);
        if (closeState.ParentRecord?.Viewport != closeState.ClosedViewport)
            ReleaseFocus(closeState.ParentRecord?.Viewport);
    }

    /// <summary>
    /// True when the restoration transaction identified by
    /// <paramref name="expectedGeneration"/> and
    /// <paramref name="expectedClosedHandle"/> is still the active one — i.e.
    /// no newer <see cref="BeginRestoration"/> has superseded it. A
    /// caller-provided RestoreFocus / FocusViewport / InitialFocus delegate
    /// may close another entry, which calls BeginRestoration; that completes
    /// this lease first and then replaces <c>_activeLease</c> with a newer
    /// generation. The older restoration must not focus a target after being
    /// superseded.
    /// </summary>
    private bool IsRestorationStillActive(
        long expectedGeneration,
        UIScreenHandle expectedClosedHandle) =>
        _activeLease?.Generation == expectedGeneration &&
        _activeCloseState?.Handle == expectedClosedHandle;

    /// <summary>
    /// True when <paramref name="target"/> is a control that the current top
    /// input owner outranks — i.e. the target is outside the current top
    /// owner's subtree (and is not the top owner's designated focus sink).
    /// A caller-provided RestoreFocus, FocusViewport, or InitialFocus delegate
    /// can open a new Blocking owner and return (or select) a target beneath a
    /// lower entry that remains interactive (VisibleInteractive);
    /// <see cref="IsControlEffectivelyInteractive"/> permits owned controls
    /// whose owner is interactive, so without this check the old restoration
    /// focuses beneath the top owner until a later deferred initial-focus
    /// callback corrects it — a transient state observable via FocusEntered
    /// side effects and keyboard/controller input. The rule applies to owned
    /// and unowned targets alike: whenever a top input owner exists, the
    /// target must belong to that owner's subtree or be its designated sink.
    /// <para>
    /// The check is NOT gated on the top owner having changed during the
    /// restoration callback. A Blocking owner may appear AFTER TryClose
    /// returned but BEFORE the deferred restoration callback begins — in that
    /// case the owner is already current when the callback starts, so a
    /// change-detection comparison against the owner captured at callback
    /// start would miss it and let restoration focus a parent beneath the new
    /// owner. Requiring the target to belong to the current top owner
    /// whenever one exists covers both the during-callback and before-callback
    /// cases.
    /// </para>
    /// <para>
    /// The top input owner is computed from the current model input order
    /// (<see cref="UIScreenHost.FocusInputOrder"/>), NOT from the published
    /// <see cref="UIScreenHost.CurrentState"/>. During a close transaction,
    /// <see cref="UIScreenHost.ProcessClose"/> calls
    /// <see cref="BeginRestoration"/> BEFORE <see cref="UIScreenHost.Recompute"/>
    /// — so when a superseded restoration lease is completed synchronously
    /// inside BeginRestoration, <c>CurrentState.TopInputOwner</c> still
    /// reflects the state before the current close. Reading the stale
    /// published value would cause this helper to look up a top owner that
    /// was already removed from <c>_entries</c> (returning false — "not
    /// outside") and let restoration focus a parent beneath the real new top
    /// owner. The model's input order is always current because mutations are
    /// immediate.
    /// </para>
    /// <para>
    /// The top owner's designated focus sink (DynamicSink for Window entries,
    /// _rootSink for Blocking Control entries) is accepted as a legitimate
    /// focus target even though it is outside the owner's view subtree. A
    /// Blocking Control with no focusable descendants uses _rootSink as its
    /// focus owner; when a child captures that sink and later closes, the
    /// parent-focus restoration path must accept the captured sink rather
    /// than rejecting it and falling through to the parent's FocusViewport /
    /// InitialFocus callbacks — which may have side effects (e.g. closing the
    /// parent) that the documented restoration order (captured focus control
    /// precedes provider callbacks) is meant to avoid.
    /// </para>
    /// </summary>
    private bool TargetOutsideNewTopOwner(
        Control? target,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        if (target == null || _host == null)
            return false;
        // A freed/invalid target cannot be focused anyway (CanFocus rejects
        // it), so treat it as "not outside" and let the caller's TryFocus
        // fail and fall through. IsSameOrAncestor would dereference
        // target.GetParent() on the freed Godot object and throw, propagating
        // out of restoration. The old change-detection gate short-circuited
        // before reaching IsSameOrAncestor; the stricter always-check gate
        // must replicate that safety for invalid targets.
        if (!GodotObject.IsInstanceValid(target))
            return false;
        var topOwnerNow = CurrentTopInputOwner(effects);
        if (topOwnerNow == null)
            return false;
        // The live top input owner is model-visible but its focus entry is not
        // yet committed — this happens during TryPresent's window between
        // _model.Open (model-visible) and Register (focus entry committed). A
        // close triggered from the candidate's own FocusViewport delegate
        // (invoked during Register, before the entry is added) can
        // synchronously complete an older restoration via BeginRestoration.
        // Without committed focus state for the live top owner, the
        // restoration must NOT focus a target beneath the visible pending
        // candidate. Returning true (block) aborts the target; the pending
        // candidate's own deferred ApplyInitialFocus claims focus once
        // Register completes. The previous `return false` (allow) let the
        // older restoration focus a lower control beneath the pending
        // candidate — a transient state observable via FocusEntered side
        // effects and keyboard/controller input.
        if (!_entries.TryGetValue(topOwnerNow.Value, out var ownerEntry))
            return true;
        // The top owner's designated focus sink (DynamicSink for Window
        // entries, _rootSink for Blocking Control entries) is an owned focus
        // target of the top owner, not a view descendant. Accept it so the
        // parent-focus restoration path can restore a captured _rootSink
        // without falling through to provider callbacks. For Window entries
        // the DynamicSink is inside the window's subtree and is already
        // accepted by IsSameOrAncestor below; this check only changes
        // behavior for Blocking Control entries whose sink is _rootSink.
        if (ownerEntry.Policy.InputPriority == UIInputPriority.Blocking)
        {
            var ownerSink = ownerEntry.DynamicSink ?? _rootSink;
            if (target == ownerSink)
                return false;
        }
        return !IsSameOrAncestor(ownerEntry.Adapter.View, target);
    }

    /// <summary>
    /// Computes the current effectively interactive top input owner from the
    /// model's live input order: the first non-Passive entry whose reduced
    /// lower-layer effect in <paramref name="effects"/> is
    /// <see cref="UILowerLayerPolicy.VisibleInteractive"/>. This mirrors
    /// <see cref="FindTopEntry"/>'s effect filter so both select the same
    /// entry, preventing <see cref="TargetOutsideNewTopOwner"/> from using a
    /// lower <see cref="UIInputPriority.Blocking"/> entry that is visually
    /// inerted by an upper owner as the ownership gate. Without the effect
    /// filter, a lower Blocking entry remains the logical top owner while a
    /// visually higher Modal inerts it; <see cref="FindTopEntry"/> selects the
    /// Modal, but <see cref="TargetOutsideNewTopOwner"/> rejects every target
    /// inside the Modal as being "outside" the inert Blocking owner, and
    /// restoration releases focus.
    /// <para>
    /// <paramref name="effects"/> is the fresh snapshot resolved once at the
    /// start of the restoration transaction (see
    /// <see cref="RestoreBestAvailableTarget"/>), NOT the host's committed
    /// <c>_resolvedLowerLayerEffects</c>, which lags the live model during a
    /// pre-Recompute close transaction.
    /// </para>
    /// <para>
    /// The pending-owner fail-closed in <see cref="TargetOutsideNewTopOwner"/>
    /// is preserved: a candidate that is model-visible but not yet in
    /// <c>_entries</c> (during TryPresent's window between _model.Open and
    /// Register) is present in the fresh snapshot (resolved from the live
    /// model) with its correctly-reduced effect, so it is still selected here
    /// and still triggers the <c>_entries.TryGetValue</c> block. A handle not
    /// present in the snapshot defaults to
    /// <see cref="UILowerLayerPolicy.VisibleInteractive"/> via
    /// <see cref="EffectFor"/>.
    /// </para>
    /// </summary>
    private UIScreenHandle? CurrentTopInputOwner(
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        if (_host == null)
            return null;
        foreach (var snapshot in _host.FocusInputOrder())
        {
            if (snapshot.Policy.InputPriority != UIInputPriority.Passive &&
                EffectFor(snapshot.Handle, effects) ==
                    UILowerLayerPolicy.VisibleInteractive)
            {
                return snapshot.Handle;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the reduced lower-layer effect for <paramref name="handle"/>
    /// from <paramref name="effects"/>, defaulting to
    /// <see cref="UILowerLayerPolicy.VisibleInteractive"/> when the handle is
    /// absent — mirroring <see cref="UIScreenHost.LowerLayerEffectFor"/>'s
    /// fallback so the pending-owner fail-closed behaviour is preserved.
    /// </summary>
    private static UILowerLayerPolicy EffectFor(
        UIScreenHandle handle,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects) =>
        effects.TryGetValue(handle, out var effect)
            ? effect
            : UILowerLayerPolicy.VisibleInteractive;

    private UIFocusRecord? CaptureParentFocus(UIScreenHandle? parentHandle)
    {
        if (!parentHandle.HasValue ||
            !_entries.TryGetValue(parentHandle.Value, out var parentEntry))
        {
            return null;
        }

        var viewport = SafeFocusViewport(parentEntry.Adapter);
        return viewport == null
            ? null
            : new UIFocusRecord(
                viewport,
                viewport.GuiGetFocusOwner(),
                parentHandle.Value);
    }

    private static ulong? ValidInstanceId(GodotObject? value) =>
        value != null && GodotObject.IsInstanceValid(value)
            ? value.GetInstanceId()
            : null;

    private (UIScreenHandle Handle, FocusEntry Entry)? FindTopEntry(
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        if (_host == null)
            return null;

        foreach (var snapshot in _host.FocusInputOrder())
        {
            if (snapshot.Policy.InputPriority != UIInputPriority.Passive &&
                // A Blocking entry that is first in logical input order may be
                // visually inerted by an upper owner. Skip it so focus
                // restoration does not return to an inert subtree; the next
                // interactive entry (or release) is a safer target.
                EffectFor(snapshot.Handle, effects) ==
                    UILowerLayerPolicy.VisibleInteractive &&
                _entries.TryGetValue(snapshot.Handle, out var entry))
            {
                return (snapshot.Handle, entry);
            }
        }

        return null;
    }

    private bool IsHandleEffectivelyInteractive(
        UIScreenHandle handle,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects) =>
        EffectFor(handle, effects) == UILowerLayerPolicy.VisibleInteractive;

    /// <summary>
    /// Returns the handle of the active entry whose view is (or is an ancestor
    /// of) <paramref name="control"/>, or null when the control is not owned by
    /// an active entry. Used to gate focus restoration targets on the owning
    /// entry's reduced effect.
    /// </summary>
    private UIScreenHandle? HandleForControl(Control? control)
    {
        if (control == null)
            return null;

        foreach (var (handle, entry) in _entries)
        {
            // Match against every adapter root as a Node, not only Control
            // roots. Controls inside an active embedded Window or AcceptDialog
            // are parented under the Window node; restricting the check to
            // Control views would leave them unowned, causing
            // IsControlEffectivelyInteractive to allow an explicit
            // RestoreFocus target inside a Window that is VisibleInert or
            // Hidden under another owner.
            if (IsSameOrAncestor(entry.Adapter.View, control))
                return handle;
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="control"/> is focusable with respect to its
    /// owning entry's reduced effect: a control not owned by any active entry
    /// is allowed, while a control inside an entry that is currently
    /// VisibleInert/Hidden is not.
    /// </summary>
    private bool IsControlEffectivelyInteractive(
        Control control,
        IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> effects)
    {
        var ownerHandle = HandleForControl(control);
        return !ownerHandle.HasValue ||
            IsHandleEffectivelyInteractive(ownerHandle.Value, effects);
    }

    private void ApplyInitialFocus(UIScreenHandle handle)
    {
        if (!IsStillFocusEligible(handle, out var entry))
            return;

        var viewport = SafeFocusViewport(entry.Adapter);
        if (viewport == null)
            return;

        // SafeFocusViewport invoked the caller-provided FocusViewport delegate,
        // which may have synchronously opened or closed entries. Opening an
        // upper owner with VisibleInert/Hidden inertes this entry while it
        // remains the logical top input owner; closing this handle removes it
        // from _entries. Revalidate before using the captured viewport or
        // focusing — otherwise this method reintroduces keyboard/controller
        // focus inside an inert subtree through a path that bypasses
        // RevokeFocusWithin, or focuses into a closed/freed subtree.
        if (!IsStillFocusEligible(handle, out entry))
            return;

        var declared = SafeTarget(entry.Adapter.InitialFocus);

        // SafeTarget invoked the caller-provided InitialFocus delegate, which
        // can mutate the host the same way FocusViewport can. Revalidate again
        // before focusing the returned target; a provider that opens an
        // inerting owner and then returns a control inside this entry must not
        // cause focus to land in the now-inert subtree.
        if (!IsStillFocusEligible(handle, out entry))
            return;

        // The InitialFocus delegate may return a control that lives inside a
        // LOWER entry the top owner has already reduced to VisibleInert, or a
        // control that is otherwise outside the current top owner's subtree.
        // Validate the target against the resolved lower-layer effects and the
        // current top owner, mirroring the checks restoration applies to
        // explicit RestoreFocus / InitialFocus targets.
        if (declared != null)
        {
            var effects = _host.ResolveCurrentLowerLayerEffects();
            if (!IsControlEffectivelyInteractive(declared, effects) ||
                TargetOutsideNewTopOwner(declared, effects))
            {
                declared = null;
            }
        }

        if (TryFocus(declared, viewport))
            return;

        var descendant = FindFirstFocusableDescendant(
            entry.Adapter.View,
            viewport,
            entry.DynamicSink);
        if (TryFocus(descendant, viewport))
            return;

        if (entry.Policy.InputPriority != UIInputPriority.Blocking)
            return;

        var sink = entry.DynamicSink ?? _rootSink;
        TryFocus(sink, viewport);
    }

    /// <summary>
    /// Re-validates that <paramref name="handle"/> is still eligible to
    /// acquire initial focus after a caller-provided delegate
    /// (FocusViewport / InitialFocus) may have mutated the host. Mirrors the
    /// entry gate of <see cref="ApplyInitialFocus"/> plus the reduced-effect
    /// check, so a delegate that inertes or closes the handle aborts focusing
    /// the same way a pre-callback state change would. Re-fetches the
    /// <see cref="FocusEntry"/> in case the registry was mutated.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_host))]
    private bool IsStillFocusEligible(UIScreenHandle handle, out FocusEntry entry)
    {
        entry = null!;
        if (_host == null || !_host.IsInsideTree() || !_host.IsActive(handle))
            return false;
        if (_host.CurrentState.TopInputOwner != handle)
            return false;
        if (_host.LowerLayerEffectFor(handle) != UILowerLayerPolicy.VisibleInteractive)
            return false;
        return _entries.TryGetValue(handle, out entry!);
    }

    private static Viewport? SafeFocusViewport(UIScreenViewAdapter adapter)
    {
        // Capture a stable diagnostic name before invoking the caller-provided
        // FocusViewport delegate. A custom FocusViewport can free its own
        // registered view (adapter.View) and then throw; without a pre-captured
        // name, the catch below dereferences adapter.View.Name on the freed
        // Godot object and throws a second exception while handling the first,
        // bypassing the null fallback and stranding the surrounding rollback or
        // restoration completion logic.
        var view = adapter.View;
        var diagnosticName = GodotObject.IsInstanceValid(view)
            ? (string)view.Name
            : "<freed>";
        try
        {
            var viewport = adapter.FocusViewport();
            return viewport != null && GodotObject.IsInstanceValid(viewport)
                ? viewport
                : null;
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"UIScreenHost focus viewport lookup failed for '{diagnosticName}': {exception.Message}");
            return null;
        }
    }

    private static Control? SafeTarget(
        Func<Control?>? target,
        bool propagateProviderExceptions = false)
    {
        if (target == null)
            return null;

        if (propagateProviderExceptions)
        {
            var control = target();
            return control != null && GodotObject.IsInstanceValid(control)
                ? control
                : null;
        }

        try
        {
            var control = target();
            return control != null && GodotObject.IsInstanceValid(control)
                ? control
                : null;
        }
        catch (Exception exception)
        {
            GD.PushError($"UIScreenHost focus target lookup failed: {exception.Message}");
            return null;
        }
    }

    private static Control? FindFirstFocusableDescendant(
        Node root,
        Viewport viewport,
        Control? excluded)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Control control && control != excluded && CanFocus(control, viewport))
                return control;

            var descendant = FindFirstFocusableDescendant(child, viewport, excluded);
            if (descendant != null)
                return descendant;
        }

        return null;
    }

    private static bool TryFocus(Control? control, Viewport? viewport)
    {
        if (!CanFocus(control, viewport))
            return false;

        control!.GrabFocus();
        return true;
    }

    private static void ReleaseFocus(Viewport? viewport)
    {
        if (viewport == null || !GodotObject.IsInstanceValid(viewport))
            return;

        var owner = viewport.GuiGetFocusOwner();
        if (owner != null && GodotObject.IsInstanceValid(owner))
            owner.ReleaseFocus();
    }

    private static bool CanFocus(Control? control, Viewport? viewport) =>
        control != null &&
        GodotObject.IsInstanceValid(control) &&
        !control.IsQueuedForDeletion() &&
        control.IsInsideTree() &&
        control.IsVisibleInTree() &&
        control.FocusMode != Control.FocusModeEnum.None &&
        control.GetViewport() == viewport &&
        (control is not BaseButton button || !button.Disabled);

    private static Control CreateSink() => new()
    {
        Name = DynamicSinkName,
        Visible = true,
        MouseFilter = Control.MouseFilterEnum.Ignore,
        FocusMode = Control.FocusModeEnum.All,
        CustomMinimumSize = Vector2.One,
        Position = Vector2.Zero,
        Size = Vector2.One,
        Modulate = new Color(1, 1, 1, 0)
    };

    private static void RemoveSink(Control? sink)
    {
        if (sink == null || !GodotObject.IsInstanceValid(sink))
            return;

        if (sink.IsInsideTree())
            sink.QueueFree();
        else
            sink.Free();
    }

    private sealed record FocusEntry(
        UIScreenViewAdapter Adapter,
        UIScreenEntryPolicy Policy,
        Control? DynamicSink,
        UIFocusRecord? ParentRecord,
        Viewport? CommittedViewport);
}
