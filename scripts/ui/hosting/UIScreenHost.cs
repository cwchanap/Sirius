using System;
using System.Collections.Generic;
using Godot;

public partial class UIScreenHost : Control
{
    private static readonly StringName ViewOwnerMeta = "__ui_screen_host_owner";

    private readonly UIScreenStackModel _model = new();
    private readonly Dictionary<UIScreenHandle, UIScreenViewAdapter> _adapters = new();
    private readonly Dictionary<UIScreenLayer, Control> _layers = new();
    private bool _ready;
    private bool _malformed = true;
    private bool _tearingDown;

    public IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries => _model.Entries;

    public override void _Ready()
    {
        _ready = true;
        _malformed = !TryBindRequiredNodes();
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
        var applyStatus = adapter.Apply();
        if (applyStatus != UIScreenOpenStatus.Opened)
        {
            _model.Close(handle);
            adapter.Restore();
            return new(applyStatus, null);
        }

        _adapters.Add(handle, adapter);
        view.SetMeta(ViewOwnerMeta, GetInstanceId());
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
               sink != null && sink.GetParent() == this;
    }

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
            if (adapter.View.HasMeta(ViewOwnerMeta) &&
                adapter.View.GetMeta(ViewOwnerMeta).AsUInt64() == GetInstanceId())
            {
                adapter.View.RemoveMeta(ViewOwnerMeta);
            }
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
            adapter.Restore();
        }
    }

    private void OnViewTreeExiting(UIScreenHandle handle)
    {
        if (!_tearingDown && IsActive(handle))
            TryClose(handle, UIScreenCloseReason.NodeFreed);
    }

    private void Recompute()
    {
        _ = UIScreenPolicyResolver.Resolve(_model.InputOrder);
    }
}
