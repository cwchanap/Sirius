using System;
using System.Collections.Generic;
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

            var viewport = SafeFocusViewport(entry.Adapter);
            if (viewport == null)
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
                $"UIScreenHost could not create a focus sink for '{adapter.View.Name}': {exception.Message}");
            RemoveSink(dynamicSink);
            preparation = new UIFocusPreparation(null, parentRecord);
            return UIScreenOpenStatus.MissingRequiredAdapter;
        }
    }

    public void Register(
        UIScreenHandle handle,
        UIScreenViewAdapter adapter,
        UIScreenEntryPolicy policy,
        UIFocusPreparation preparation)
    {
        _entries.Add(handle, new FocusEntry(
            adapter,
            policy,
            preparation.DynamicSink,
            preparation.ParentRecord));
        if (policy.InputPriority != UIInputPriority.Passive)
            Callable.From(() => ApplyInitialFocus(handle)).CallDeferred();
    }

    public void DiscardPreparation(UIFocusPreparation preparation) =>
        RemoveSink(preparation.DynamicSink);

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
        // no longer be activated by ui_accept / joypad GUI events.
        focusOwner.ReleaseFocus();
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
            // covers any descendant whose view is nested inside inertControl.
            if (entry.Adapter.View is Control view &&
                IsSameOrAncestor(inertControl, view))
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
        var explicitTarget = SafeTarget(
            closeState.Adapter?.RestoreFocus,
            propagateProviderExceptions);
        // The explicit restore target may live in a still-active entry that
        // another owner has since inerted. Do not focus into an inert/hidden
        // subtree; fall through to the next restoration path. A target not
        // owned by any active entry (e.g. a free-standing control) is allowed.
        if (explicitTarget != null &&
            IsControlEffectivelyInteractive(explicitTarget) &&
            TryFocus(explicitTarget, explicitTarget.GetViewport()))
        {
            return;
        }

        var parentRecord = closeState.ParentRecord;
        if (parentRecord != null &&
            _entries.ContainsKey(parentRecord.ParentHandle) &&
            IsHandleEffectivelyInteractive(parentRecord.ParentHandle) &&
            TryFocus(parentRecord.FocusOwner, parentRecord.Viewport))
        {
            return;
        }

        if (parentRecord != null &&
            _entries.TryGetValue(parentRecord.ParentHandle, out var parentEntry) &&
            IsHandleEffectivelyInteractive(parentRecord.ParentHandle))
        {
            var parentViewport = SafeFocusViewport(parentEntry.Adapter);
            var parentInitial = SafeTarget(
                parentEntry.Adapter.InitialFocus,
                propagateProviderExceptions);
            // SafeFocusViewport and SafeTarget invoked the caller-provided
            // FocusViewport and InitialFocus delegates. Either may re-enter the
            // host and open an upper owner that inerts (or closes) the parent
            // while it remains the selected restoration candidate. Revalidate
            // the parent's registration and reduced effect before focusing the
            // provider-returned target; otherwise restoration focuses inside a
            // now-inert subtree through a path that bypasses
            // RevokeFocusWithin. Mirrors the post-delegate revalidation
            // ApplyInitialFocus already performs.
            if (parentViewport != null &&
                _entries.ContainsKey(parentRecord.ParentHandle) &&
                IsHandleEffectivelyInteractive(parentRecord.ParentHandle) &&
                TryFocus(parentInitial, parentViewport))
            {
                return;
            }
        }

        var top = FindTopEntry();
        if (top != null)
        {
            var topHandle = top.Value.Handle;
            var topEntry = top.Value.Entry;
            var viewport = SafeFocusViewport(topEntry.Adapter);
            // SafeFocusViewport invoked the caller-provided FocusViewport
            // delegate, which may re-enter the host and inert or close the
            // entry that FindTopEntry selected. Revalidate that the selected
            // top is still registered and interactive before focusing into its
            // subtree; otherwise restoration can land inside a subtree that
            // was inerted by the delegate.
            if (viewport != null &&
                _entries.ContainsKey(topHandle) &&
                IsHandleEffectivelyInteractive(topHandle))
            {
                var descendant = FindFirstFocusableDescendant(
                    topEntry.Adapter.View,
                    viewport,
                    topEntry.DynamicSink);
                if (TryFocus(descendant, viewport))
                    return;

                if (topEntry.Policy.InputPriority == UIInputPriority.Blocking &&
                    TryFocus(topEntry.DynamicSink ?? _rootSink, viewport))
                {
                    return;
                }
            }
        }

        ReleaseFocus(closeState.ClosedViewport);
        if (parentRecord?.Viewport != closeState.ClosedViewport)
            ReleaseFocus(parentRecord?.Viewport);
    }

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

    private (UIScreenHandle Handle, FocusEntry Entry)? FindTopEntry()
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
                _host.LowerLayerEffectFor(snapshot.Handle) ==
                    UILowerLayerPolicy.VisibleInteractive &&
                _entries.TryGetValue(snapshot.Handle, out var entry))
            {
                return (snapshot.Handle, entry);
            }
        }

        return null;
    }

    private bool IsHandleEffectivelyInteractive(UIScreenHandle handle) =>
        _host != null &&
        _host.LowerLayerEffectFor(handle) == UILowerLayerPolicy.VisibleInteractive;

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
    private bool IsControlEffectivelyInteractive(Control control)
    {
        var ownerHandle = HandleForControl(control);
        return !ownerHandle.HasValue || IsHandleEffectivelyInteractive(ownerHandle.Value);
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
                $"UIScreenHost focus viewport lookup failed for '{adapter.View.Name}': {exception.Message}");
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
        UIFocusRecord? ParentRecord);
}
