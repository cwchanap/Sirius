using System;
using Godot;

internal sealed class UIScreenViewAdapter
{
    private readonly Node _attachmentParent;
    private readonly bool _needsAttachment;
    private readonly Node.ProcessModeEnum _incomingProcessMode;
    private readonly UIProcessPolicy _processPolicy;
    private Node.ProcessModeEnum _registeredProcessMode;
    private bool _attachedByHost;
    private bool _finished;

    private UIScreenViewAdapter(
        Node view,
        Node attachmentParent,
        Node.ProcessModeEnum registeredProcessMode,
        UIProcessPolicy processPolicy,
        UIScreenEntrySpec spec)
    {
        View = view;
        _attachmentParent = attachmentParent;
        _needsAttachment = view.GetParent() == null;
        _incomingProcessMode = view.ProcessMode;
        _registeredProcessMode = registeredProcessMode;
        _processPolicy = processPolicy;
        IsPresented = spec.IsPresented ?? DefaultIsPresented(view);
        SetPresented = spec.SetPresented ?? DefaultSetPresented(view);
        SetInteractive = spec.SetInteractive ?? (_ => { });
        HasRequiredPresentationAdapter = spec.IsPresented == null || spec.SetPresented != null;
        HasInteractiveAdapter = spec.SetInteractive != null;
        FocusViewport = spec.FocusViewport ?? DefaultFocusViewport(view);
        InitialFocus = spec.InitialFocus;
        RestoreFocus = spec.RestoreFocus;
        InterceptCancel = spec.InterceptCancel;
        Cleanup = spec.Cleanup;
        NodeLifetime = spec.NodeLifetime;
    }

    public Node View { get; }
    public Func<bool> IsPresented { get; }
    public Action<bool> SetPresented { get; }
    public Action<bool> SetInteractive { get; }
    public bool HasRequiredPresentationAdapter { get; }
    public bool HasInteractiveAdapter { get; }
    public Func<Viewport> FocusViewport { get; }
    public Func<Control?>? InitialFocus { get; }
    public Func<Control?>? RestoreFocus { get; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; }
    public Action<UIScreenCloseReason>? Cleanup { get; }
    public UINodeLifetime NodeLifetime { get; }
    public Action? TreeExitingHandler { get; set; }
    public Node.ProcessModeEnum IncomingProcessMode => _incomingProcessMode;
    public Node.ProcessModeEnum RegisteredProcessMode => _registeredProcessMode;

    public bool CanApply(
        UILowerLayerPolicy effect,
        bool requireControlInteractivityAdapter = false) => effect switch
    {
        UILowerLayerPolicy.VisibleInteractive => true,
        UILowerLayerPolicy.Hidden => HasRequiredPresentationAdapter &&
                                     CanDisableControlInput(
                                         requireControlInteractivityAdapter),
        UILowerLayerPolicy.VisibleInert => View switch
        {
            Window => true,
            Control => CanDisableControlInput(requireControlInteractivityAdapter),
            _ => false
        },
        _ => false
    };

    private bool CanDisableControlInput(bool requireAdapter) =>
        View is not Control control ||
        HasInteractiveAdapter ||
        (!requireAdapter && !control.IsProcessingInput());

    public static UIScreenOpenStatus TryCreate(
        UIScreenHost host,
        Control layer,
        Node view,
        UIScreenEntrySpec spec,
        UIScreenEntryPolicy policy,
        bool isPausedAfterOpen,
        bool hasPauseBoundedLifetime,
        out UIScreenViewAdapter? adapter)
    {
        adapter = null;
        Node attachmentParent;

        if (view is Control control)
        {
            if (control.GetParent() != null && control.GetParent() != layer)
                return UIScreenOpenStatus.InvalidControlParentage;
            attachmentParent = layer;
        }
        else if (view is Window window)
        {
            if (!host.GetViewport().GuiEmbedSubwindows)
                return UIScreenOpenStatus.UnsupportedSubwindowMode;
            if (window.GetParent() != null && window.GetParent() != host)
                return UIScreenOpenStatus.InvalidControlParentage;
            attachmentParent = host;
        }
        else
        {
            return UIScreenOpenStatus.InvalidNode;
        }

        var processStatus = ResolveProcessMode(
            view.ProcessMode,
            attachmentParent.ProcessMode,
            policy.ProcessPolicy,
            isPausedAfterOpen,
            hasPauseBoundedLifetime,
            out var registeredProcessMode);
        if (processStatus != UIScreenOpenStatus.Opened)
            return processStatus;

        adapter = new UIScreenViewAdapter(
            view,
            attachmentParent,
            registeredProcessMode,
            policy.ProcessPolicy,
            spec);
        return UIScreenOpenStatus.Opened;
    }

    public UIScreenOpenStatus Apply()
    {
        // Capture a safe diagnostic name before attachment so the catch block
        // below never re-dereferences View to build its message. If AddChild
        // ever throws with View in an invalid state, accessing View.Name inside
        // the catch would throw a second time while handling the first
        // exception, skipping RollbackRegistration and stranding the model
        // entry and adapter registration. (Note: in Godot 4.6.2 a view that
        // Free()s itself from _Ready() segfaults the engine inside add_child
        // before returning to C#, and a thrown _Ready() exception is swallowed
        // by the engine, so neither reaches this catch; the capture is a
        // defensive hardening of the existing catch, not a fix for a reachable
        // path.)
        var viewName = GodotObject.IsInstanceValid(View) ? (string)View.Name : "<invalid>";

        try
        {
            if (_needsAttachment)
            {
                // Mark as host-attached BEFORE AddChild so a re-entrant Close()
                // from the view's _Ready() (synchronously invoked during
                // AddChild) can detach the view correctly. Without this,
                // _attachedByHost is still false when Close() checks it, so
                // the view is left parented and Apply() returns Opened for a
                // closed entry — orphaning focus registration and leaving the
                // view as an unmanaged child of the host.
                _attachedByHost = true;
                _attachmentParent.AddChild(View);
                _attachedByHost = View.GetParent() == _attachmentParent;
            }

            // _finished is set by a re-entrant Close() from _Ready(). Even if
            // the view was detached (so GetParent() != _attachmentParent),
            // _finished is the authoritative signal that the adapter is no
            // longer open.
            if (_finished || View.IsQueuedForDeletion() ||
                View.GetParent() != _attachmentParent)
            {
                RollbackRegistration();
                return UIScreenOpenStatus.InvalidNode;
            }

            View.ProcessMode = _registeredProcessMode;
            return UIScreenOpenStatus.Opened;
        }
        catch (Exception exception)
        {
            // Use the pre-attachment name; View may be invalid here and
            // dereferencing View.Name would re-throw inside the catch, leaving
            // the model entry and adapter registration intact.
            GD.PushError($"UIScreenHost could not attach '{viewName}': {exception.Message}");
            RollbackRegistration();
            return UIScreenOpenStatus.InvalidNode;
        }
    }

    /// <summary>
    /// Revalidates the registered process mode against the current pause
    /// state after Apply()'s synchronous lifecycle callbacks (_EnterTree,
    /// _Ready) may have mutated the host (e.g. opened a PauseTree owner).
    /// The process mode was selected before attachment in TryCreate but
    /// assigned on the view only after _Ready() returns (in Apply()). If
    /// the pause state changed during _Ready(), the originally-selected
    /// mode may now be invalid (e.g. Pausable while the tree is now paused).
    /// Re-runs ResolveProcessMode with the current isPausedAfterOpen and
    /// hasPauseBoundedLifetime; on success, updates the registered mode and
    /// the view's ProcessMode if they differ. Returns InvalidProcessPolicy
    /// when the current state rejects the candidate's process policy.
    /// </summary>
    public UIScreenOpenStatus RevalidateProcessMode(
        bool isPausedAfterOpen,
        bool hasPauseBoundedLifetime)
    {
        var status = ResolveProcessMode(
            _incomingProcessMode,
            _attachmentParent.ProcessMode,
            _processPolicy,
            isPausedAfterOpen,
            hasPauseBoundedLifetime,
            out var newRegisteredMode);
        if (status != UIScreenOpenStatus.Opened)
            return status;
        if (newRegisteredMode != _registeredProcessMode)
        {
            _registeredProcessMode = newRegisteredMode;
            if (GodotObject.IsInstanceValid(View) && !_finished)
                View.ProcessMode = newRegisteredMode;
        }
        return status;
    }

    public void RollbackRegistration()
    {
        if (_finished)
            return;

        _finished = true;
        if (!GodotObject.IsInstanceValid(View))
            return;

        View.ProcessMode = _incomingProcessMode;

        if (_attachedByHost && View.GetParent() == _attachmentParent)
            _attachmentParent.RemoveChild(View);
    }

    public void Close()
    {
        if (_finished)
            return;

        _finished = true;
        if (!GodotObject.IsInstanceValid(View))
            return;

        View.ProcessMode = _incomingProcessMode;

        if (NodeLifetime == UINodeLifetime.Hide && View is CanvasItem canvasItem)
            canvasItem.Hide();
        else if (NodeLifetime == UINodeLifetime.Hide && View is Window window)
            window.Hide();

        if (NodeLifetime == UINodeLifetime.QueueFree)
        {
            if (!View.IsQueuedForDeletion())
                View.QueueFree();
            return;
        }

        if (NodeLifetime == UINodeLifetime.External &&
            View.GetParent() == _attachmentParent)
        {
            _attachmentParent.RemoveChild(View);
        }
        else if (_attachedByHost && View.GetParent() == _attachmentParent)
        {
            _attachmentParent.RemoveChild(View);
        }
    }

    private static UIScreenOpenStatus ResolveProcessMode(
        Node.ProcessModeEnum incoming,
        Node.ProcessModeEnum parentMode,
        UIProcessPolicy processPolicy,
        bool isPausedAfterOpen,
        bool hasPauseBoundedLifetime,
        out Node.ProcessModeEnum registered)
    {
        registered = incoming;
        switch (processPolicy)
        {
            case UIProcessPolicy.PreserveAndValidate:
                if (incoming == Node.ProcessModeEnum.Disabled ||
                    (isPausedAfterOpen && incoming == Node.ProcessModeEnum.Pausable) ||
                    (!hasPauseBoundedLifetime &&
                     incoming == Node.ProcessModeEnum.WhenPaused) ||
                    (isPausedAfterOpen && incoming == Node.ProcessModeEnum.Inherit &&
                     parentMode == Node.ProcessModeEnum.Pausable))
                {
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                }
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.InheritHost:
                if (isPausedAfterOpen && parentMode == Node.ProcessModeEnum.Pausable)
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                registered = Node.ProcessModeEnum.Inherit;
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.Pausable:
                if (isPausedAfterOpen)
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                registered = Node.ProcessModeEnum.Pausable;
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.WhenPaused:
                if (!hasPauseBoundedLifetime)
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                registered = Node.ProcessModeEnum.WhenPaused;
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.Always:
                registered = Node.ProcessModeEnum.Always;
                return UIScreenOpenStatus.Opened;

            default:
                return UIScreenOpenStatus.InvalidProcessPolicy;
        }
    }

    private static Func<bool> DefaultIsPresented(Node view) => view switch
    {
        Control control => () => control.Visible,
        Window window => () => window.Visible,
        _ => () => false
    };

    private static Action<bool> DefaultSetPresented(Node view) => view switch
    {
        Control control => visible =>
        {
            if (visible) control.Show(); else control.Hide();
        },
        Window window => visible =>
        {
            if (visible) window.Show(); else window.Hide();
        },
        _ => _ => { }
    };

    private static Func<Viewport> DefaultFocusViewport(Node view) => view switch
    {
        Window window => () => window,
        Control control => () => control.GetViewport(),
        _ => throw new ArgumentOutOfRangeException(nameof(view))
    };
}
