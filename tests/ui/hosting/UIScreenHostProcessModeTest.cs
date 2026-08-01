using System.Threading.Tasks;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostProcessModeTest : Node
{
    [TestCase]
    public async Task Scene_HasRequiredProcessModesAndVisibleSink()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        try
        {
            var host = fixture.Host;
            AssertThat(host.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
            AssertThat(host.GetNode<Control>("HUDLayer").ProcessMode)
                .IsEqual(Node.ProcessModeEnum.Pausable);
            AssertThat(host.GetNode<Control>("ScreenLayer").ProcessMode)
                .IsEqual(Node.ProcessModeEnum.Always);
            AssertThat(host.GetNode<Control>("ModalLayer").ProcessMode)
                .IsEqual(Node.ProcessModeEnum.Always);
            AssertThat(host.GetNode<Control>("ToastLayer").ProcessMode)
                .IsEqual(Node.ProcessModeEnum.Always);
            AssertThat(host.GetNode<Control>("TransitionLayer").ProcessMode)
                .IsEqual(Node.ProcessModeEnum.Always);

            var shield = host.GetNode<Control>("InputShield");
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.MouseFilter).IsEqual(Control.MouseFilterEnum.Stop);

            var sink = host.GetNode<Control>("FocusSink");
            AssertThat(sink.Visible).IsTrue();
            AssertThat(sink.FocusMode).IsEqual(Control.FocusModeEnum.All);
            AssertThat(sink.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(sink.Size).IsEqual(new Vector2(1, 1));
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PresentAndClose_ControlAttachesAndRestoresExactProcessMode()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { ProcessMode = ProcessModeEnum.WhenPaused });
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    ProcessPolicy = UIProcessPolicy.Always
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(view.GetParent()).IsEqual(fixture.Host.GetNode<Control>("ModalLayer"));
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.Always);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.IsKindActive(UIScreenKinds.Pause)).IsTrue();

            var closed = fixture.Host.TryClose(
                opened.Handle.Value,
                UIScreenCloseReason.Programmatic);

            AssertThat(closed.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(view.GetParent()).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_MalformedHost_IsRejectedWithoutMutation()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = new UIScreenHost { ProcessMode = ProcessModeEnum.Always };
        var view = new Control { ProcessMode = ProcessModeEnum.WhenPaused };
        tree.Root.AddChild(host);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        try
        {
            var result = host.TryPresent(view, UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MalformedHost);
            AssertThat(result.Handle).IsNull();
            AssertThat(host.ActiveEntries.Count).IsEqual(0);
            AssertThat(view.GetParent()).IsNull();
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            view.Free();
            host.Free();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase("shield_visible")]
    [TestCase("shield_mouse")]
    [TestCase("shield_focus")]
    [TestCase("shield_layout")]
    [TestCase("sink_hidden")]
    [TestCase("sink_mouse")]
    [TestCase("sink_focus")]
    [TestCase("sink_size")]
    [TestCase("sink_layout")]
    public async Task Present_MalformedSafetyNodeProperty_IsRejectedWithoutMutation(
        string malformedProperty)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")!;
        var host = scene.Instantiate<UIScreenHost>();
        var shield = host.GetNode<Control>("InputShield");
        var sink = host.GetNode<Control>("FocusSink");
        switch (malformedProperty)
        {
            case "shield_visible":
                shield.Visible = true;
                break;
            case "shield_mouse":
                shield.MouseFilter = Control.MouseFilterEnum.Ignore;
                break;
            case "shield_focus":
                shield.FocusMode = Control.FocusModeEnum.All;
                break;
            case "shield_layout":
                shield.AnchorRight = 0.5f;
                break;
            case "sink_hidden":
                sink.Visible = false;
                break;
            case "sink_mouse":
                sink.MouseFilter = Control.MouseFilterEnum.Stop;
                break;
            case "sink_focus":
                sink.FocusMode = Control.FocusModeEnum.None;
                break;
            case "sink_size":
                sink.CustomMinimumSize = Vector2.Zero;
                sink.Size = new Vector2(2, 2);
                break;
            case "sink_layout":
                sink.Position = new Vector2(2, 0);
                break;
        }

        var view = new Control { ProcessMode = ProcessModeEnum.WhenPaused };
        tree.Root.AddChild(host);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        try
        {
            var result = host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MalformedHost);
            AssertThat(result.Handle).IsNull();
            AssertThat(host.ActiveEntries.Count).IsEqual(0);
            AssertThat(view.GetParent()).IsNull();
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            view.Free();
            host.Free();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_InvalidNodeAndParentage_AreAtomicNoOps()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var invalidNode = fixture.Track(new Node { ProcessMode = ProcessModeEnum.WhenPaused });
        var foreignParent = fixture.Track(new Control());
        fixture.Viewport.AddChild(foreignParent);
        var foreignChild = new Control { ProcessMode = ProcessModeEnum.Pausable };
        foreignParent.AddChild(foreignChild);
        try
        {
            var invalidNodeResult = fixture.Host.TryPresent(
                invalidNode,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));
            var parentageResult = fixture.Host.TryPresent(
                foreignChild,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                });

            AssertThat(invalidNodeResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(parentageResult.Status).IsEqual(UIScreenOpenStatus.InvalidControlParentage);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(invalidNode.GetParent()).IsNull();
            AssertThat(invalidNode.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(foreignChild.GetParent()).IsEqual(foreignParent);
            AssertThat(foreignChild.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_ControlAlreadyUnderExactLayer_IsAcceptedAndDetachedOnClose()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var layer = fixture.Host.GetNode<Control>("ScreenLayer");
        var view = fixture.Track(new Control { ProcessMode = ProcessModeEnum.Pausable });
        layer.AddChild(view);
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                });
            var closed = fixture.Host.TryClose(
                opened.Handle!.Value,
                UIScreenCloseReason.Programmatic);

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(closed.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(view.GetParent()).IsNull();
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_UnusableProcessPolicies_AreRejectedBeforeMutation()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var disabled = fixture.Track(new Control { ProcessMode = ProcessModeEnum.Disabled });
        var pausableOwner = fixture.Track(new Control { ProcessMode = ProcessModeEnum.Inherit });
        var whenPausedOnly = fixture.Track(new Control { ProcessMode = ProcessModeEnum.Always });
        try
        {
            var disabledResult = fixture.Host.TryPresent(
                disabled,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });
            var pausableResult = fixture.Host.TryPresent(
                pausableOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    ProcessPolicy = UIProcessPolicy.Pausable,
                    PauseTree = true
                });
            var whenPausedResult = fixture.Host.TryPresent(
                whenPausedOnly,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    ProcessPolicy = UIProcessPolicy.WhenPaused,
                    PauseTree = false
                });

            AssertThat(disabledResult.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(pausableResult.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(whenPausedResult.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(disabled.GetParent()).IsNull();
            AssertThat(pausableOwner.GetParent()).IsNull();
            AssertThat(whenPausedOnly.GetParent()).IsNull();
            AssertThat(disabled.ProcessMode).IsEqual(ProcessModeEnum.Disabled);
            AssertThat(pausableOwner.ProcessMode).IsEqual(ProcessModeEnum.Inherit);
            AssertThat(whenPausedOnly.ProcessMode).IsEqual(ProcessModeEnum.Always);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PreserveAndValidate_UsesEffectiveParentPauseContext()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var pausableChild = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.Pausable
        });
        var whenPausedChild = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                }).Handle!.Value;

            var rejected = fixture.Host.TryPresent(
                pausableChild,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });
            var accepted = fixture.Host.TryPresent(
                whenPausedChild,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });

            AssertThat(rejected.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(rejected.Handle).IsNull();
            AssertThat(pausableChild.GetParent()).IsNull();
            AssertThat(pausableChild.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
            AssertThat(accepted.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(whenPausedChild.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PreserveAndValidate_UnrelatedWhenPausedCannotBorrowTemporaryPause()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var pauseView = fixture.Track(new Control());
        var unrelated = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        try
        {
            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });
            AssertThat(pause.Status).IsEqual(UIScreenOpenStatus.Opened);

            var rejected = fixture.Host.TryPresent(
                unrelated,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = null,
                    PauseTree = false,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });

            AssertThat(rejected.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(rejected.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(unrelated.GetParent()).IsNull();
            AssertThat(unrelated.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PreserveAndValidate_UnrelatedPausableRejectsImmediateAggregatePause()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var pauseView = fixture.Track(new Control());
        var unrelated = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.Pausable
        });
        try
        {
            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });
            AssertThat(pause.Status).IsEqual(UIScreenOpenStatus.Opened);

            var rejected = fixture.Host.TryPresent(
                unrelated,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = null,
                    PauseTree = false,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });

            AssertThat(rejected.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(rejected.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(unrelated.GetParent()).IsNull();
            AssertThat(unrelated.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PreserveAndValidate_TransitivePausingAncestorBoundsWhenPausedLifetime()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var pauseView = fixture.Track(new Control());
        var intermediateView = fixture.Track(new Control());
        var descendant = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        try
        {
            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                }).Handle!.Value;
            var intermediate = fixture.Host.TryPresent(
                intermediateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = pause,
                    PauseTree = false
                }).Handle!.Value;

            var opened = fixture.Host.TryPresent(
                descendant,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = intermediate,
                    PauseTree = false,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(descendant.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PreserveAndValidate_PauseOnlyCandidateUsesPostOpenContext()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var pausable = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.Pausable
        });
        var whenPaused = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        try
        {
            var rejected = fixture.Host.TryPresent(
                pausable,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });
            var accepted = fixture.Host.TryPresent(
                whenPaused,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    PauseTree = true,
                    ProcessPolicy = UIProcessPolicy.PreserveAndValidate
                });

            AssertThat(rejected.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(rejected.Handle).IsNull();
            AssertThat(pausable.GetParent()).IsNull();
            AssertThat(pausable.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
            AssertThat(accepted.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(whenPaused.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_DuplicateKindAndRegisteredNode_DoNotMutateRejectedView()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var first = fixture.Track(new Control());
        var duplicateKind = fixture.Track(new Control { ProcessMode = ProcessModeEnum.WhenPaused });
        try
        {
            var firstOpen = fixture.Host.TryPresent(
                first,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));
            var duplicateOpen = fixture.Host.TryPresent(
                duplicateKind,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                });
            var sameNodeOpen = fixture.Host.TryPresent(
                first,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));

            AssertThat(firstOpen.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(duplicateOpen.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
            AssertThat(sameNodeOpen.Status).IsEqual(UIScreenOpenStatus.NodeAlreadyRegistered);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(duplicateKind.GetParent()).IsNull();
            AssertThat(duplicateKind.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_NodeOwnedByAnotherHost_IsRejectedWithoutMutation()
    {
        var firstFixture = await UIScreenHostTestSupport.CreateHost(this);
        var secondFixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = firstFixture.Track(new Control { ProcessMode = ProcessModeEnum.WhenPaused });
        try
        {
            var firstOpen = firstFixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                });
            var ownerParent = view.GetParent();
            var secondOpen = secondFixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));

            AssertThat(firstOpen.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(secondOpen.Status).IsEqual(UIScreenOpenStatus.NodeOwnedByAnotherHost);
            AssertThat(firstFixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(secondFixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(view.GetParent()).IsEqual(ownerParent);
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.Always);
        }
        finally
        {
            secondFixture.Dispose();
            firstFixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_ViewQueuedDuringAttach_RollsBackModelAndProcessSnapshot()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new QueueFreeOnEnterControl
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        try
        {
            var result = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(view.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(view.GetParent()).IsNull();
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_AttachRejected_DoesNotApplyHideOrQueueFreeLifetime()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var hideView = fixture.Track(new DetachOnReadyControl
        {
            ProcessMode = ProcessModeEnum.WhenPaused,
            Visible = true
        });
        var queueFreeView = fixture.Track(new DetachOnReadyControl
        {
            ProcessMode = ProcessModeEnum.Pausable,
            Visible = true
        });
        try
        {
            var hideResult = fixture.Host.TryPresent(
                hideView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always,
                    NodeLifetime = UINodeLifetime.Hide
                });
            var queueFreeResult = fixture.Host.TryPresent(
                queueFreeView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    ProcessPolicy = UIProcessPolicy.Always,
                    NodeLifetime = UINodeLifetime.QueueFree
                });

            AssertThat(hideResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(queueFreeResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(hideView.GetParent()).IsNull();
            AssertThat(queueFreeView.GetParent()).IsNull();
            AssertThat(hideView.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(queueFreeView.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
            AssertThat(hideView.Visible).IsTrue();
            AssertThat(queueFreeView.Visible).IsTrue();
            AssertThat(hideView.IsQueuedForDeletion()).IsFalse();
            AssertThat(queueFreeView.IsQueuedForDeletion()).IsFalse();
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_ViewReentersDuringReady_IsAlreadyReservedByHost()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new ReentrantOnReadyControl
        {
            Host = fixture.Host
        });
        try
        {
            var outerResult = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));

            AssertThat(outerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(view.ReentrantResult.Status)
                .IsEqual(UIScreenOpenStatus.NodeAlreadyRegistered);
            AssertThat(view.ReentrantResult.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(fixture.Host.IsKindActive(UIScreenKinds.Settings)).IsFalse();
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Close_Parent_CleansDescendantsInModelOrderAndRestoresViews()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var cleanupOrder = new List<string>();
        var parentView = fixture.Track(new Control { ProcessMode = ProcessModeEnum.Pausable });
        var childView = fixture.Track(new Control { ProcessMode = ProcessModeEnum.WhenPaused });
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always,
                    Cleanup = _ => cleanupOrder.Add("parent")
                }).Handle!.Value;
            fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    ProcessPolicy = UIProcessPolicy.Always,
                    Cleanup = _ => cleanupOrder.Add("child")
                });

            var result = fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);

            AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(cleanupOrder.ToArray()).ContainsExactly("child", "parent");
            AssertThat(parentView.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
            AssertThat(childView.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(parentView.GetParent()).IsNull();
            AssertThat(childView.GetParent()).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task HostFixture_DisposeRestoresEnvironmentalStateAndTemporaryActions()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var action = new StringName($"ui_host_fixture_{GetInstanceId()}");
        AssertThat(InputMap.HasAction(action)).IsFalse();
        var incomingPaused = tree.Paused;
        var incomingMouseMode = Input.MouseMode;
        var externalHud = new Control();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { action },
            new UIScreenHostOptions { HudRoot = externalHud });
        var incomingEmbed = fixture.Viewport.GuiEmbedSubwindows;
        var incomingHud = fixture.HudRoot.Visible;

        tree.Paused = !incomingPaused;
        Input.MouseMode = incomingMouseMode == Input.MouseModeEnum.Visible
            ? Input.MouseModeEnum.Captured
            : Input.MouseModeEnum.Visible;
        fixture.Viewport.GuiEmbedSubwindows = !incomingEmbed;
        fixture.HudRoot.Visible = !incomingHud;
        fixture.Dispose();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        AssertThat(tree.Paused).IsEqual(incomingPaused);
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
        AssertThat(fixture.Viewport.GuiEmbedSubwindows).IsEqual(incomingEmbed);
        AssertThat(fixture.HudRoot.Visible).IsEqual(incomingHud);
        AssertThat(InputMap.HasAction(action)).IsFalse();
        externalHud.Free();
    }

    private sealed partial class QueueFreeOnEnterControl : Control
    {
        public override void _EnterTree() => QueueFree();
    }

    private sealed partial class DetachOnReadyControl : Control
    {
        public override void _Ready() => GetParent()?.RemoveChild(this);
    }

    private sealed partial class ReentrantOnReadyControl : Control
    {
        public UIScreenHost Host { get; init; } = null!;
        public UIScreenOpenResult ReentrantResult { get; private set; }

        public override void _Ready()
        {
            ReentrantResult = Host.TryPresent(
                this,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));
        }
    }
}
