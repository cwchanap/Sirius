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
    Viewport? ClosedViewport);

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
        Callable.From(() => ApplyInitialFocus(handle)).CallDeferred();
    }

    public void DiscardPreparation(UIFocusPreparation preparation) =>
        RemoveSink(preparation.DynamicSink);

    public UIFocusCloseState CloseEntry(UIScreenHandle handle)
    {
        if (!_entries.Remove(handle, out var entry))
            return new UIFocusCloseState(handle, null, null, null);

        var viewport = SafeFocusViewport(entry.Adapter);
        RemoveSink(entry.DynamicSink);
        return new UIFocusCloseState(
            handle,
            entry.Adapter,
            entry.ParentRecord,
            viewport);
    }

    public void BeginRestoration(UIFocusCloseState closeState)
    {
        if (_activeLease != null)
            CompleteRestoration(_activeLease.Generation, _activeLease.ClosedHandle);

        var generation = ++_nextGeneration;
        _activeLease = new UIFocusRestorationLease(generation, closeState.Handle);
        _activeCloseState = closeState;
        Callable.From(() => CompleteRestoration(generation, closeState.Handle))
            .CallDeferred();
    }

    public void CompleteActiveRestoration()
    {
        if (_activeLease != null)
            CompleteRestoration(_activeLease.Generation, _activeLease.ClosedHandle);
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

    private void CompleteRestoration(long generation, UIScreenHandle closedHandle)
    {
        try
        {
            if (_activeLease?.Generation != generation)
                return;

            RestoreBestAvailableTarget(closedHandle);
        }
        finally
        {
            if (_activeLease?.Generation == generation)
            {
                _activeLease = null;
                _activeCloseState = null;
                _host?.OnFocusRestorationCompleted();
            }
        }
    }

    private void RestoreBestAvailableTarget(UIScreenHandle closedHandle)
    {
        if (_activeCloseState?.Handle != closedHandle)
            return;

        var closeState = _activeCloseState;
        var explicitTarget = SafeTarget(closeState.Adapter?.RestoreFocus);
        if (TryFocus(explicitTarget, explicitTarget?.GetViewport()))
            return;

        var parentRecord = closeState.ParentRecord;
        if (parentRecord != null &&
            _entries.ContainsKey(parentRecord.ParentHandle) &&
            TryFocus(parentRecord.FocusOwner, parentRecord.Viewport))
        {
            return;
        }

        if (parentRecord != null &&
            _entries.TryGetValue(parentRecord.ParentHandle, out var parentEntry))
        {
            var parentViewport = SafeFocusViewport(parentEntry.Adapter);
            var parentInitial = SafeTarget(parentEntry.Adapter.InitialFocus);
            if (parentViewport != null && TryFocus(parentInitial, parentViewport))
                return;
        }

        var top = FindTopEntry();
        if (top != null)
        {
            var viewport = SafeFocusViewport(top.Adapter);
            if (viewport != null)
            {
                var descendant = FindFirstFocusableDescendant(
                    top.Adapter.View,
                    viewport,
                    top.DynamicSink);
                if (TryFocus(descendant, viewport))
                    return;

                if (top.Policy.InputPriority == UIInputPriority.Blocking &&
                    TryFocus(top.DynamicSink ?? _rootSink, viewport))
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

    private FocusEntry? FindTopEntry()
    {
        if (_host == null)
            return null;

        foreach (var snapshot in _host.FocusInputOrder())
        {
            if (snapshot.Policy.InputPriority != UIInputPriority.Passive &&
                _entries.TryGetValue(snapshot.Handle, out var entry))
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyInitialFocus(UIScreenHandle handle)
    {
        if (_host == null || !_host.IsInsideTree() || !_host.IsActive(handle) ||
            !_entries.TryGetValue(handle, out var entry))
        {
            return;
        }

        var viewport = SafeFocusViewport(entry.Adapter);
        if (viewport == null)
            return;

        var declared = SafeTarget(entry.Adapter.InitialFocus);
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

    private static Control? SafeTarget(Func<Control?>? target)
    {
        if (target == null)
            return null;

        try
        {
            return target();
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

    private static bool CanFocus(Control? control, Viewport viewport) =>
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
