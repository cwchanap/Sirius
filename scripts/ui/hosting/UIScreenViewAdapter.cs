using System;
using Godot;

internal sealed class UIScreenViewAdapter
{
    private readonly Node _attachmentParent;
    private readonly bool _needsAttachment;
    private readonly Node.ProcessModeEnum _incomingProcessMode;
    private readonly Node.ProcessModeEnum _registeredProcessMode;
    private bool _attachedByHost;
    private bool _finished;

    private UIScreenViewAdapter(
        Node view,
        Node attachmentParent,
        Node.ProcessModeEnum registeredProcessMode,
        UIScreenEntrySpec spec)
    {
        View = view;
        _attachmentParent = attachmentParent;
        _needsAttachment = view.GetParent() == null;
        _incomingProcessMode = view.ProcessMode;
        _registeredProcessMode = registeredProcessMode;
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
            policy,
            out var registeredProcessMode);
        if (processStatus != UIScreenOpenStatus.Opened)
            return processStatus;

        adapter = new UIScreenViewAdapter(
            view,
            attachmentParent,
            registeredProcessMode,
            spec);
        return UIScreenOpenStatus.Opened;
    }

    public UIScreenOpenStatus Apply()
    {
        try
        {
            if (_needsAttachment)
            {
                _attachmentParent.AddChild(View);
                _attachedByHost = View.GetParent() == _attachmentParent;
            }

            if (View.IsQueuedForDeletion() || View.GetParent() != _attachmentParent)
            {
                RollbackRegistration();
                return UIScreenOpenStatus.InvalidNode;
            }

            View.ProcessMode = _registeredProcessMode;
            return UIScreenOpenStatus.Opened;
        }
        catch (Exception exception)
        {
            GD.PushError($"UIScreenHost could not attach '{View.Name}': {exception.Message}");
            RollbackRegistration();
            return UIScreenOpenStatus.InvalidNode;
        }
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
        UIScreenEntryPolicy policy,
        out Node.ProcessModeEnum registered)
    {
        registered = incoming;
        switch (policy.ProcessPolicy)
        {
            case UIProcessPolicy.PreserveAndValidate:
                if (incoming == Node.ProcessModeEnum.Disabled ||
                    (policy.PauseTree && incoming == Node.ProcessModeEnum.Pausable) ||
                    (!policy.PauseTree && incoming == Node.ProcessModeEnum.WhenPaused) ||
                    (policy.PauseTree && incoming == Node.ProcessModeEnum.Inherit &&
                     parentMode == Node.ProcessModeEnum.Pausable))
                {
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                }
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.InheritHost:
                if (policy.PauseTree && parentMode == Node.ProcessModeEnum.Pausable)
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                registered = Node.ProcessModeEnum.Inherit;
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.Pausable:
                if (policy.PauseTree)
                    return UIScreenOpenStatus.InvalidProcessPolicy;
                registered = Node.ProcessModeEnum.Pausable;
                return UIScreenOpenStatus.Opened;

            case UIProcessPolicy.WhenPaused:
                if (!policy.PauseTree)
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
