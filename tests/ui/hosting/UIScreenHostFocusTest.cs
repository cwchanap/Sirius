using System.Threading.Tasks;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using static UIScreenHostTestSupport;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostFocusTest : Node
{
    private static readonly StringName UiCancelAction = "ui_cancel";
    private static readonly StringName ErrorFixture = "error_fixture";
    private static readonly StringName BlockingFixtureA = "blocking_fixture_a";
    private static readonly StringName BlockingFixtureB = "blocking_fixture_b";

    [TestCase]
    public async Task BlockingControlWithoutFocusableDescendant_UsesRootSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var sink = fixture.Host.GetNode<Control>("FocusSink");
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(sink);
            AssertThat(sink.Visible).IsTrue();
            AssertThat(sink.FocusMode).IsEqual(Control.FocusModeEnum.All);
            AssertThat(sink.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task BlockingWindowWithoutFocusableDescendant_UsesVisibleViewportSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var window = fixture.Track(new Window { Visible = true });
        fixture.Viewport.GuiEmbedSubwindows = true;
        try
        {
            var opened = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var sink = window.GuiGetFocusOwner();
            AssertThat(sink).IsNotNull();
            AssertThat(sink!.GetParent()).IsEqual(window);
            AssertThat(sink.Visible).IsTrue();
            AssertThat(sink.FocusMode).IsEqual(Control.FocusModeEnum.All);
            AssertThat(sink.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(sink.CustomMinimumSize).IsEqual(Vector2.One);
            AssertThat(sink.Position).IsEqual(Vector2.Zero);
            AssertThat(sink.Size).IsEqual(Vector2.One);
            AssertThat(sink.AnchorLeft).IsEqual(0f);
            AssertThat(sink.AnchorTop).IsEqual(0f);
            AssertThat(sink.AnchorRight).IsEqual(0f);
            AssertThat(sink.AnchorBottom).IsEqual(0f);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_SubscriberDoesNotFireDuringCandidateRecompute_PublishesAfterCommit()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window { Visible = true });
        var publishedStates = new List<UIScreenEffectiveState>();
        fixture.Host.EffectiveStateChanged += state => publishedStates.Add(state);
        try
        {
            var opened = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });

            // Publication is suppressed during the candidate's final Recompute
            // and deferred until after the commit check passes. The candidate
            // opens successfully; the subscriber observes only the committed
            // state, never a transient state that could be rejected.
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(opened.Handle).IsNotNull();
            AssertThat(publishedStates.Count).IsEqual(1);
            AssertThat(publishedStates[0].TopInputOwner).IsEqual(opened.Handle);

            // The subscriber fires after the commit with the committed state.
            // The candidate is active and has a focus sink (it was committed,
            // not rolled back).
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Closing the committed candidate removes the sink normally.
            fixture.Host.TryClose(opened.Handle.Value, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(window.GetNodeOrNull<Control>("__UIScreenFocusSink")).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PassiveEntry_WithFocusableDescendantDoesNotStealFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var existingOwner = fixture.Track(new Button
        {
            FocusMode = Control.FocusModeEnum.All
        });
        tree.Root.AddChild(existingOwner);
        var toast = fixture.Track(new Control());
        var toastButton = new Button { FocusMode = Control.FocusModeEnum.All };
        toast.AddChild(toastButton);
        try
        {
            existingOwner.GrabFocus();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(existingOwner);

            var opened = fixture.Host.TryPresent(
                toast,
                UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
                {
                    Layer = UIScreenLayer.Toast,
                    InputPriority = UIInputPriority.Passive
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(existingOwner);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ClosingPassiveEntry_DoesNotCreateBarrierBeforeNextCancel()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { UiCancelAction });
        var parentView = fixture.Track(new Control());
        var toast = fixture.Track(new Control());
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cancel = UICancelPolicy.Close
                }).Handle!.Value;
            var passive = fixture.Host.TryPresent(
                toast,
                UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
                {
                    Layer = UIScreenLayer.Toast,
                    InputPriority = UIInputPriority.Passive
                }).Handle!.Value;

            fixture.Host.TryClose(passive, UIScreenCloseReason.Programmatic);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();

            var cancel = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(cancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task InitialFocus_IsDeferredWithoutBarrierAndClosedTokenCannotStealFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this, new[] { UiCancelAction });
        var view = fixture.Track(new Control());
        var target = new Button { FocusMode = Control.FocusModeEnum.All };
        view.AddChild(target);
        fixture.Viewport.GuiGetFocusOwner()?.ReleaseFocus();
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cancel = UICancelPolicy.Close,
                    InitialFocus = () => target
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNotEqual(target);

            var cancel = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));
            AssertThat(cancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsFalse();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNotEqual(target);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DeferredInitialFocus_LowerReadyReentrantOpenCannotStealFromTopOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var focusEvents = new List<string>();
        var higherView = fixture.Track(new Control());
        var higherTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        higherTarget.FocusEntered += () => focusEvents.Add("higher");
        higherView.AddChild(higherTarget);
        var lowerView = fixture.Track(new OpensHigherOwnerOnReadyControl
        {
            Host = fixture.Host,
            HigherView = higherView,
            HigherInitialFocus = () => higherTarget
        });
        var lowerTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        lowerTarget.FocusEntered += () => focusEvents.Add("lower");
        lowerView.AddChild(lowerTarget);
        try
        {
            var lowerResult = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Screen,
                    InitialFocus = () => lowerTarget
                });

            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(lowerView.HigherResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(
                lowerView.HigherResult.Handle);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(focusEvents.ToArray()).ContainsExactly("higher");
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(higherTarget);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task CloseChild_RestoresExplicitTargetBeforeCapturedParentFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var captured = new Button { FocusMode = Control.FocusModeEnum.All };
        var explicitTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(captured);
        parentView.AddChild(explicitTarget);
        var childView = fixture.Track(new Control());
        var childTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        childView.AddChild(childTarget);
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InitialFocus = () => captured
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    InitialFocus = () => childTarget,
                    RestoreFocus = () => explicitTarget
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(childTarget);

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsTrue();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(explicitTarget);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FreedExplicitTarget_FallsBackToCapturedParentFocusAndReleasesLease()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this, new[] { UiCancelAction });
        var parentView = fixture.Track(new Control());
        var captured = new Button { FocusMode = Control.FocusModeEnum.All };
        var freedTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(captured);
        parentView.AddChild(freedTarget);
        var childView = fixture.Track(new Control());
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cancel = UICancelPolicy.Close,
                    InitialFocus = () => captured
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            captured.GrabFocus();

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    RestoreFocus = () => freedTarget
                }).Handle!.Value;
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            var generation = fixture.Host.Diagnostics.RestorationLease!.Generation;
            freedTarget.QueueFree();

            var duringBarrier = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));
            AssertThat(duringBarrier).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();

            var duplicate = fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            AssertThat(duplicate.Status).IsEqual(UIScreenCloseStatus.AlreadyClosed);
            AssertThat(fixture.Host.Diagnostics.RestorationLease!.Generation)
                .IsEqual(generation);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(captured);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();

            var nextCancel = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));
            AssertThat(nextCancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DisposedExplicitTarget_IsValidatedBeforeFallbackDereference()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var captured = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(captured);
        var childView = fixture.Track(new Control());
        var childTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        childView.AddChild(childTarget);
        var disposedTarget = fixture.Track(new Button
        {
            FocusMode = Control.FocusModeEnum.All
        });
        tree.Root.AddChild(disposedTarget);
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InitialFocus = () => captured
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    InitialFocus = () => childTarget,
                    RestoreFocus = () => disposedTarget
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(childTarget);

            disposedTarget.Free();
            AssertThat(GodotObject.IsInstanceValid(disposedTarget)).IsFalse();
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(captured);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task InvalidCapturedFocus_FallsBackThroughParentInitialThenDescendantThenSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var parentInitial = new Button { FocusMode = Control.FocusModeEnum.All };
        var descendantFallback = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(parentInitial);
        parentView.AddChild(descendantFallback);
        var childView = fixture.Track(new Control());
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    InitialFocus = () => parentInitial
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                }).Handle!.Value;
            parentInitial.QueueFree();
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(descendantFallback);

            descendantFallback.QueueFree();
            var secondChild = fixture.Track(new Control());
            var second = fixture.Host.TryPresent(
                secondChild,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                }).Handle!.Value;
            fixture.Host.GetNode<Control>("FocusSink").ReleaseFocus();
            fixture.Host.TryClose(second, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner())
                .IsEqual(fixture.Host.GetNode<Control>("FocusSink"));
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task InvalidCapturedFocus_RestoresParentInitialTarget()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var parentInitial = new Button { FocusMode = Control.FocusModeEnum.All };
        var captured = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(parentInitial);
        parentView.AddChild(captured);
        var childView = fixture.Track(new Control());
        var childTarget = new Button { FocusMode = Control.FocusModeEnum.All };
        childView.AddChild(childTarget);
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InitialFocus = () => parentInitial
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            captured.GrabFocus();

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    InitialFocus = () => childTarget
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            captured.Free();

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(parentInitial);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ClosingFinalOwner_ReleasesViewportFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        var target = new Button { FocusMode = Control.FocusModeEnum.All };
        view.AddChild(target);
        try
        {
            var handle = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InitialFocus = () => target
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(target);

            fixture.Host.TryClose(handle, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNull();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ClosingChildWindow_RestoresParentWindowSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var parentWindow = fixture.Track(new Window { Visible = true });
        var childWindow = fixture.Track(new Window { Visible = true });
        try
        {
            var parent = fixture.Host.TryPresent(
                parentWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    InputPriority = UIInputPriority.Blocking
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var parentSink = parentWindow.GetNode<Control>("__UIScreenFocusSink");
            AssertThat(parentWindow.GuiGetFocusOwner()).IsEqual(parentSink);
            parentSink.ReleaseFocus();
            AssertThat(parentWindow.GuiGetFocusOwner()).IsNull();

            var child = fixture.Host.TryPresent(
                childWindow,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Parent = parent,
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(parentWindow.GuiGetFocusOwner()).IsEqual(parentSink);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task SupersedingClose_StaleCallbackCannotClearNewerLease()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        var externalTarget = fixture.Track(new Button { FocusMode = Control.FocusModeEnum.All });
        tree.Root.AddChild(externalTarget);
        var pendingAfterStaleCallback = false;
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    RestoreFocus = () => externalTarget
                }).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                }).Handle!.Value;

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            var staleGeneration = fixture.Host.Diagnostics.RestorationLease!.Generation;
            Callable.From(() =>
            {
                pendingAfterStaleCallback =
                    fixture.Host.CurrentState.IsFocusRestorationPending;
            }).CallDeferred();

            fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);
            var currentGeneration = fixture.Host.Diagnostics.RestorationLease!.Generation;

            AssertThat(currentGeneration).IsGreater(staleGeneration);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(pendingAfterStaleCallback).IsTrue();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(externalTarget);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task SupersedingClose_PublishesBarrierOnceAndCoreCancelCannotCrossIt()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this, new[] { UiCancelAction });
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        var pendingStates = new List<bool>();
        UIInputDispatchResult? callbackCancel = null;
        var observeSupersession = false;
        try
        {
            fixture.Host.EffectiveStateChanged += state =>
            {
                if (!observeSupersession)
                    return;

                pendingStates.Add(state.IsFocusRestorationPending);
                callbackCancel ??= fixture.Host.TryHandleInput(
                    UIScreenHostTestSupport.ActionPress(UiCancelAction));
            };
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause)).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                }).Handle!.Value;

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            var oldGeneration = fixture.Host.Diagnostics.RestorationLease!.Generation;
            observeSupersession = true;
            fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);

            AssertThat(pendingStates.ToArray()).ContainsExactly(true);
            AssertThat(callbackCancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsTrue();
            AssertThat(fixture.Host.Diagnostics.RestorationLease!.Generation)
                .IsGreater(oldGeneration);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PrepareForTeardown_ThrowOnceRestoreFocusPropagatesAndRetryCompletes()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        var restoreAttempts = 0;
        try
        {
            fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    RestoreFocus = () =>
                    {
                        restoreAttempts++;
                        if (restoreAttempts == 1)
                            throw new System.InvalidOperationException("retry focus");
                        return null;
                    }
                });

            AssertThrown(() => fixture.Host.PrepareForTeardown())
                .IsInstanceOf<System.InvalidOperationException>()
                .HasMessage("retry focus");
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNotNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsTrue();

            AssertThat(fixture.Host.PrepareForTeardown()).IsEqual(
                UIScreenTeardownPreparationStatus.Complete);
            AssertThat(restoreAttempts).IsEqual(2);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ReentrantParentClose_IsQueuedAndSupersedesChildLease()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        UIScreenHandle parent = default;
        UIScreenCloseStatus? reentrantStatus = null;
        try
        {
            parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause)).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Cleanup = _ =>
                    {
                        reentrantStatus = fixture.Host.TryClose(
                            parent,
                            UIScreenCloseReason.Programmatic).Status;
                    }
                }).Handle!.Value;

            var result = fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(reentrantStatus).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(fixture.Host.IsActive(child)).IsFalse();
            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNotNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsTrue();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HostTeardown_CompletesPendingLeaseAndInvalidatesDeferredFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var pendingStates = new List<bool>();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        try
        {
            fixture.Host.EffectiveStateChanged += state =>
                pendingStates.Add(state.IsFocusRestorationPending);
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause)).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                }).Handle!.Value;

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending).IsTrue();

            // QueueFree() internally calls PrepareForTeardown() and silently
            // skips the free on Deferred. Assert Complete up front so the test
            // actually exercises the finalized teardown path it claims to, then
            // confirm the host is deleted after the queued free is processed.
            AssertThat(fixture.Host.PrepareForTeardown()).IsEqual(
                UIScreenTeardownPreparationStatus.Complete);
            fixture.Host.QueueFree();
            AssertThat(fixture.Host.IsQueuedForDeletion()).IsTrue();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(GodotObject.IsInstanceValid(fixture.Host)).IsFalse();
            AssertThat(pendingStates).Contains(true);
            AssertThat(pendingStates[^1]).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task VisibleInert_RevokesFocusFromLowerFocusableDescendantSoUiAcceptCannotActivateIt()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var upperView = fixture.Track(new Control { Visible = true });
        try
        {
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            // Open a non-blocking owner with no focusable descendant that inerts
            // the lower owner. VisibleInert must revoke lower-layer focus rather
            // than relying on the pointer-only InputShield.
            var upper = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upper.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The lower button must no longer hold focus. Godot routes ui_accept
            // and joypad GUI events to the focus owner after the _Input phase;
            // mouse_filter / the InputShield only govern pointer interaction. A
            // focused descendant would otherwise stay activatable by ui_accept
            // for as long as the upper (non-Blocking, no focusable descendant)
            // entry is open. Revoking focus is what makes the lower button
            // keyboard-inert under VisibleInert.
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNull();
            AssertThat(lowerButton.HasFocus()).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task VisibleInert_BlockingLowerEntry_RedirectsAwayFromInertSubtree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var upperView = fixture.Track(new Control { Visible = true });
        var upperButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        upperView.AddChild(upperButton);
        try
        {
            // Lower entry: Blocking priority in the HUD layer. A Blocking entry
            // is first in logical input order regardless of visual layer.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            // Upper entry: Screen priority, VisibleInert lower layers. The upper
            // entry visually inerts the lower Blocking entry, but the lower
            // entry remains first in logical input order. RevokeFocusWithin must
            // not redirect focus back into the inert lower subtree.
            var upper = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upper.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The lower Button must no longer own focus. The redirect target
            // must be selected from interactive entries only (the upper entry),
            // not from the inert lower Blocking entry that is first in input
            // order.
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(upperButton);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task VisibleInert_FocusViewportMovesFocusOutsideInertSubtree_FallbackKeepsExternalFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var upperView = fixture.Track(new Control { Visible = true });
        // externalButton lives inside the upper view, so it is NOT a
        // descendant of the inert lowerView. A FocusViewport side effect
        // moves focus to it and returns no usable redirect viewport.
        var externalButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        upperView.AddChild(externalButton);
        var focusViewportInvoked = false;
        var focusViewportCallCount = 0;
        try
        {
            // Lower entry: Blocking priority in the HUD layer. Blocking keeps
            // it first in logical input order even when visually inerted.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            // Upper entry: Screen priority, VisibleInert lower layers. Its
            // FocusViewport delegate is invoked twice during TryPresent:
            //   1. Register() resolves the committed viewport BEFORE Recompute
            //      applies VisibleInert and calls RevokeFocusWithin.
            //   2. RevokeFocusWithin() resolves the redirect viewport AFTER
            //      capturing the focus owner (lowerButton, inside the inert
            //      subtree) and passing the IsSameOrAncestor guard.
            // The delegate must NOT move focus on the first call (Register) so
            // the guard in RevokeFocusWithin still sees lowerButton inside the
            // inert subtree. On the second call (RevokeFocusWithin) it moves
            // focus to externalButton (outside the inert lowerView subtree)
            // and returns null (no usable redirect viewport). The fallback
            // must re-query the current owner but release it only when it is
            // still a descendant of the inert control. Releasing the
            // externally-focused control would strand keyboard/controller
            // navigation without an owner.
            var upper = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    FocusViewport = () =>
                    {
                        focusViewportCallCount++;
                        focusViewportInvoked = true;
                        if (focusViewportCallCount == 2)
                            externalButton.GrabFocus();
                        return null;
                    }
                });
            AssertThat(upper.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(focusViewportInvoked).IsTrue();
            AssertThat(focusViewportCallCount).IsEqual(2);

            // Assert immediately after TryPresent, before the deferred
            // ApplyInitialFocus runs on the next frame and re-focuses
            // externalButton (which would mask the fallback's erroneous
            // release). The inert lowerButton must no longer own focus, and
            // externalButton — moved outside the inert subtree by the
            // FocusViewport side effect — must remain focused.
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(externalButton.HasFocus()).IsTrue();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(externalButton);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task VisibleInert_SetInteractiveClosesUnrelatedEntry_ReapplyRevokesFocus()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var unrelatedView = fixture.Track(new Control { Visible = true });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? unrelatedHandle = null;
        var closedUnrelated = false;
        try
        {
            // Unrelated entry opened first so the SetInteractive callback can
            // close it re-entrantly while ApplyControlEffect applies
            // VisibleInert to the lower target. It sits at Modal with the
            // default VisibleInteractive lower-layer policy so it does not
            // contribute to the lower target's reduced effect.
            var unrelatedResult = fixture.Host.TryPresent(
                unrelatedView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal
                });
            AssertThat(unrelatedResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            unrelatedHandle = unrelatedResult.Handle;

            var lowerResult = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        lowerView.SetProcessInput(enabled);
                        if (enabled || !unrelatedHandle.HasValue || closedUnrelated)
                            return;
                        // Re-entrant close of an UNRELATED entry while
                        // ApplyControlEffect applies VisibleInert to this
                        // target. The target stays active; Recompute must
                        // reapply the effect and revoke focus from the inert
                        // subtree rather than skipping a committed-looking
                        // provisional marker.
                        closedUnrelated = true;
                        fixture.Host.TryClose(
                            unrelatedHandle.Value,
                            UIScreenCloseReason.Programmatic);
                    }
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerView.SetProcessInput(true);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(closedUnrelated).IsTrue();
            AssertThat(fixture.Host.IsActive(unrelatedHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.IsActive(lowerResult.Handle!.Value)).IsTrue();

            // The lower target's effective policy is VisibleInert. The
            // re-entrant close must not strand a provisional marker: Recompute
            // reapplies the effect, and RevokeFocusWithin must release the
            // lower button's focus so ui_accept cannot activate it while the
            // upper owner inerts the subtree.
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DeferredInitialFocus_SkippedWhenEntryBecomesInertBeforeCallbackRuns()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var upperView = fixture.Track(new Control { Visible = true });
        try
        {
            // Lower HUD entry: Blocking priority with a deferred InitialFocus
            // targeting its button. Blocking keeps it first in logical input
            // order (TopInputOwner) even when visually inerted.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking,
                    InitialFocus = () => lowerButton
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Before the deferred ApplyInitialFocus runs, present an upper
            // Screen entry that inerts the lower entry. The lower entry remains
            // TopInputOwner (Blocking) but is now VisibleInert.
            var upper = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upper.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(lower.Handle);

            // Let the deferred ApplyInitialFocus run. It must not focus inside
            // the inert subtree: the lower button must not hold focus.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == lowerButton).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task CloseChild_ExplicitRestoreTargetInsideInertWindow_NotFocused()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var parentWindow = fixture.Track(new Window { Visible = true });
        var restoreTarget = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        parentWindow.AddChild(restoreTarget);
        var inertOwner = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var childButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        childView.AddChild(childButton);
        try
        {
            // Parent entry is an embedded Window at Hud with Blocking input
            // priority. Its focus viewport is the Window itself, and controls
            // inside it are parented under the Window node — not under a
            // Control adapter root.
            var parent = fixture.Host.TryPresent(
                parentWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Upper Screen owner inerts the parent Window (VisibleInert).
            // The parent remains TopInputOwner (Blocking) but is visually
            // inert.
            var inert = fixture.Host.TryPresent(
                inertOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(inert.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(parentWindow.GuiDisableInput).IsTrue();

            // Child entry at Modal (above Screen so the inert owner does not
            // inert it) with Parent = parent. RestoreFocus targets a control
            // inside the inert parent Window.
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Modal,
                    Parent = parent.Handle,
                    InitialFocus = () => childButton,
                    RestoreFocus = () => restoreTarget
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(childButton.HasFocus()).IsTrue();

            // Close the child. Restoration must not focus restoreTarget inside
            // the inert parent Window. HandleForControl must match the Window
            // adapter root (as a Node, not only Control roots) so
            // IsControlEffectivelyInteractive gates the target on the parent's
            // reduced effect (VisibleInert).
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(restoreTarget.HasFocus()).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_SkipsInertedParentWhenAnotherOwnerRemains()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        parentView.AddChild(parentButton);
        var inertOwner = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var childButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        childView.AddChild(childButton);
        try
        {
            // Parent entry (Screen, Blocking) with a focusable button. Blocking
            // keeps it first in logical input order even when visually inerted,
            // so FindTopEntry() would return it without the effect gate.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);
            parentButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(parentButton);

            // Upper Modal owner that inerts the parent (VisibleInert) and
            // remains active for the whole scenario. The parent stays
            // TopInputOwner (Blocking) but is now visually inert.
            var inert = fixture.Host.TryPresent(
                inertOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(inert.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(parent.Handle);

            // Child entry (Toast, above Modal so the inert owner does not inert
            // it) opened with Parent = parent. Its open captures the parent's
            // focused button as the restoration target.
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Toast,
                    Parent = parent.Handle,
                    InitialFocus = () => childButton
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(childButton.HasFocus()).IsTrue();

            // Close the child. Restoration must not return focus to the parent
            // button, which lives in the inert (VisibleInert) subtree. The
            // parent's reduced effect is still VisibleInert because the Modal
            // inert owner remains active, and the parent remains first in input
            // order (Blocking). FindTopEntry() must skip the inert parent.
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(parentButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == parentButton).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_LowerBlockingInertedByHigherModal_FocusesModalTarget()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var modalView = fixture.Track(new Control { Visible = true });
        var modalButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        modalView.AddChild(modalButton);
        var childView = fixture.Track(new Control { Visible = true });
        var childButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        childView.AddChild(childButton);
        try
        {
            // Lower entry: Blocking priority in the HUD layer with a focusable
            // button. A Blocking entry is first in logical input order
            // regardless of visual layer, so CurrentTopInputOwner() would
            // return it without the effect filter.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(lowerButton.HasFocus()).IsTrue();

            // Upper Modal owner with a focusable button. VisibleInert lower
            // layers inert the lower Blocking entry, but the lower Blocking
            // remains first in logical input order (Blocking). RevokeFocusWithin
            // redirects focus from the lower button to the Modal's button —
            // the top interactive entry's first focusable descendant. The
            // Modal is the effectively interactive top owner.
            var modal = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(modal.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(modalButton.HasFocus()).IsTrue();

            // Child entry (Toast, above Modal so the Modal does not inert it)
            // opened with Parent = lower. The child inherits Blocking priority
            // from its parent, so it is first in input order and is the
            // TopInputOwner — its deferred ApplyInitialFocus fires. The child's
            // open captures the modal's button as the parent-record focus
            // target (same viewport).
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Toast,
                    Parent = lower.Handle,
                    InitialFocus = () => childButton
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(childButton.HasFocus()).IsTrue();

            // Close the child. Restoration must focus the Modal's button —
            // the effectively interactive top owner's focusable descendant.
            // Without the fix, CurrentTopInputOwner() returns the inert
            // lower Blocking (first non-Passive in input order, no effect
            // filter). TargetOutsideNewTopOwner rejects the Modal's button as
            // being "outside" the inert Blocking's subtree, the sink fallback
            // fails (Modal is not Blocking), and restoration releases focus.
            // With the fix, CurrentTopInputOwner() filters on
            // VisibleInteractive and returns the Modal, so the Modal's button
            // is accepted and focused.
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(modalButton.HasFocus()).IsTrue();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(modalButton);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DeferredInitialFocus_InitialFocusProviderOpensInertingOwner_DoesNotFocusInertSubtree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenOpenResult? upperResult = null;
        try
        {
            // Lower HUD entry: Blocking priority with a deferred InitialFocus
            // whose provider opens an upper Screen owner that inerts the lower
            // entry (VisibleInert), then returns the lower entry's button.
            // Blocking keeps the lower entry first in logical input order
            // (TopInputOwner) even after it is visually inerted.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InputPriority = UIInputPriority.Blocking,
                    InitialFocus = () =>
                    {
                        upperResult = fixture.Host.TryPresent(
                            upperView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                            {
                                Layer = UIScreenLayer.Screen,
                                LowerLayers = UILowerLayerPolicy.VisibleInert
                            });
                        return lowerButton;
                    }
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Let the deferred ApplyInitialFocus run. The provider opens the
            // upper owner DURING the callback, inerting the lower entry while
            // it remains TopInputOwner. ApplyInitialFocus must revalidate after
            // the InitialFocus delegate and NOT focus lowerButton inside the
            // now-inert subtree — the same invariant RevokeFocusWithin
            // enforces for focus that existed before inerting. Without the
            // post-delegate revalidation, focus would land on lowerButton.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(upperResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(lower.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.IsActive(upperResult.Value.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(lower.Handle);
            AssertThat(fixture.Host.LowerLayerEffectFor(lower.Handle!.Value))
                .IsEqual(UILowerLayerPolicy.VisibleInert);
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == lowerButton).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_InitialFocusProviderOpensInertingOwner_DoesNotFocusInertSubtree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        // parentButton starts non-focusable so the parent's deferred
        // ApplyInitialFocus does not focus it (the descendant scan skips it).
        // The parent therefore has no focus owner when the child opens, so the
        // captured parent record's FocusOwner is null and restoration falls
        // through to the parent-initial path that invokes the InitialFocus
        // provider.
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.None,
            Disabled = false
        };
        parentView.AddChild(parentButton);
        var upperView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var armed = false;
        UIScreenOpenResult? upperResult = null;
        try
        {
            // Parent (Screen, Screen priority — not Blocking so no sink focus)
            // with an InitialFocus provider that, when armed, makes parentButton
            // focusable, opens an upper Modal owner that inerts the parent
            // (VisibleInert), and returns parentButton.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Screen,
                    InitialFocus = () =>
                    {
                        if (armed)
                        {
                            parentButton.FocusMode = Control.FocusModeEnum.All;
                            upperResult = fixture.Host.TryPresent(
                                upperView,
                                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                {
                                    Layer = UIScreenLayer.Modal,
                                    LowerLayers = UILowerLayerPolicy.VisibleInert
                                });
                        }
                        return armed ? parentButton : null;
                    }
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);
            // Let the parent's deferred ApplyInitialFocus run (unarmed): it
            // finds no focusable descendant and, not being Blocking, focuses no
            // sink, so the parent has no focus owner.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNull();

            // Child (Toast, above Modal so the upper owner does not inert it)
            // opened with Parent = parent. Its open captures a parent record
            // with a null FocusOwner.
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Toast,
                    Parent = parent.Handle
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Arm the provider so the next InitialFocus invocation (during the
            // child's deferred restoration) opens the inerting owner and
            // returns parentButton, then close the child. Restoration must
            // revalidate the parent's effect AFTER the provider callback and
            // must NOT focus parentButton inside the now-inert subtree — the
            // same invariant ApplyInitialFocus already enforces. Without the
            // post-provider revalidation, focus lands on parentButton.
            armed = true;
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(upperResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(parent.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.LowerLayerEffectFor(parent.Handle!.Value))
                .IsEqual(UILowerLayerPolicy.VisibleInert);
            AssertThat(parentButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == parentButton).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DeferredInitialFocus_FocusViewportClosesHandle_DoesNotFocusAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { Visible = true });
        var button = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        view.AddChild(button);
        UIScreenHandle? handle = null;
        var closedFromDelegate = false;
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    NodeLifetime = UINodeLifetime.Hide,
                    FocusViewport = () =>
                    {
                        // Close this handle from within the FocusViewport
                        // delegate invoked by ApplyInitialFocus. Without
                        // post-delegate revalidation, ApplyInitialFocus would
                        // continue using the captured adapter/viewport and
                        // focus into the closed subtree.
                        if (handle.HasValue && fixture.Host.IsActive(handle.Value))
                        {
                            closedFromDelegate = true;
                            fixture.Host.TryClose(
                                handle.Value,
                                UIScreenCloseReason.Programmatic);
                        }
                        return view.GetViewport();
                    },
                    InitialFocus = () => button
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            handle = opened.Handle;

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(closedFromDelegate).IsTrue();
            AssertThat(fixture.Host.IsActive(handle!.Value)).IsFalse();
            AssertThat(button.HasFocus()).IsFalse();
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_ParentFocusViewportClosesParent_InitialFocusNotInvokedAfterStaleParent()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var parentInitial = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(parentInitial);
        var captured = new Button { FocusMode = Control.FocusModeEnum.All };
        parentView.AddChild(captured);
        var childView = fixture.Track(new Control());
        var closeParentOnNextFocusViewport = false;
        var initialFocusCallCount = 0;
        UIScreenHandle? parentHandle = null;
        try
        {
            // Parent P with FocusViewport and InitialFocus. FocusViewport
            // closes P when a flag is set. When child C (Parent = P) is
            // closed, deferred restoration takes the parent-initial path:
            // SafeFocusViewport(P) invokes FocusViewport, which closes P,
            // superseding the restoration. Without the fix, the stale
            // parent's InitialFocus is still invoked after FocusViewport.
            // With the fix, revalidation after FocusViewport detects the
            // supersession and returns without invoking InitialFocus.
            parentHandle = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InitialFocus = () =>
                    {
                        initialFocusCallCount++;
                        return parentInitial;
                    },
                    FocusViewport = () =>
                    {
                        var viewport = parentView.GetViewport();
                        if (closeParentOnNextFocusViewport && parentHandle.HasValue)
                        {
                            closeParentOnNextFocusViewport = false;
                            fixture.Host.TryClose(
                                parentHandle.Value,
                                UIScreenCloseReason.Programmatic);
                        }
                        return viewport;
                    }
                }).Handle;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            // P's ApplyInitialFocus called InitialFocus once.
            AssertThat(initialFocusCallCount).IsEqual(1);
            captured.GrabFocus();

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parentHandle!.Value
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            // Free the captured focus owner so the parent focus-owner path
            // fails and restoration falls through to the parent-initial path.
            captured.Free();

            // Arm FocusViewport to close P on the next invocation (during
            // deferred restoration). Reset the call count so we can detect
            // any stale InitialFocus invocation during restoration.
            closeParentOnNextFocusViewport = true;
            initialFocusCallCount = 0;
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // With the fix: FocusViewport closed P, superseding the
            // restoration. The revalidation after FocusViewport detects
            // the supersession and returns without invoking InitialFocus.
            // Without the fix, InitialFocus is invoked on the stale parent.
            AssertThat(initialFocusCallCount).IsEqual(0);
            AssertThat(fixture.Host.IsActive(parentHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_RestoreFocusClosesEntryAndReturnsTarget_OldRestorationDoesNotFocusAfterSupersession()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var otherView = fixture.Track(new Control { Visible = true });
        var ownerView = fixture.Track(new Control { Visible = true });
        var externalButton = fixture.Track(new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        });
        fixture.Host.GetParent().AddChild(externalButton);
        var restoreCallCount = 0;
        UIScreenHandle? otherHandle = null;
        try
        {
            // Present "other" first so its handle is available for the
            // owner's RestoreFocus delegate.
            var other = fixture.Host.TryPresent(
                otherView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });
            AssertThat(other.Status).IsEqual(UIScreenOpenStatus.Opened);
            otherHandle = other.Handle;

            // Owner with a RestoreFocus that closes "other" on the first call
            // and returns an external Button. The close installs a newer
            // restoration lease, superseding the owner's. On the re-entrant
            // second call (BeginRestoration completes the old lease first),
            // the delegate returns null so the re-entrant restoration does not
            // focus the Button — isolating the outer call's behavior.
            var owner = fixture.Host.TryPresent(
                ownerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    RestoreFocus = () =>
                    {
                        restoreCallCount++;
                        if (restoreCallCount == 1 && otherHandle.HasValue)
                        {
                            fixture.Host.TryClose(
                                otherHandle.Value,
                                UIScreenCloseReason.Programmatic);
                            return externalButton;
                        }
                        return null;
                    }
                });
            AssertThat(owner.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Close the owner. Its deferred restoration runs RestoreFocus,
            // which closes "other" (installing a newer lease) and returns the
            // external Button. The re-entrant completion of the old lease calls
            // RestoreFocus again (returns null). Without the fix, the outer
            // restoration still focuses the external Button after being
            // superseded; with the fix, it detects the supersession and aborts.
            restoreCallCount = 0;
            AssertThat(fixture.Host.TryClose(
                owner.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // The external Button must not retain focus — the old restoration
            // was superseded by "other"'s restoration lease.
            AssertThat(externalButton.HasFocus()).IsFalse();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(externalButton) &&
                externalButton.IsInsideTree())
            {
                externalButton.GetParent()?.RemoveChild(externalButton);
            }
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_RestoreFocusOpensBlockingOwnerAndReturnsExternalTarget_DoesNotFocusOutsideNewOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var ownerView = fixture.Track(new Control { Visible = true });
        var blockingView = fixture.Track(new Control { Visible = true });
        var blockingButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        blockingView.AddChild(blockingButton);
        var externalButton = fixture.Track(new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        });
        fixture.Host.GetParent().AddChild(externalButton);
        var armed = false;
        UIScreenOpenResult? blockingResult = null;
        try
        {
            // Owner (Screen priority, non-Blocking) with a RestoreFocus that,
            // when armed, opens a new Blocking Modal owner and returns an
            // external Button outside the new owner's subtree.
            var owner = fixture.Host.TryPresent(
                ownerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Screen,
                    RestoreFocus = () =>
                    {
                        if (armed)
                        {
                            blockingResult = fixture.Host.TryPresent(
                                blockingView,
                                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                {
                                    Layer = UIScreenLayer.Modal,
                                    InputPriority = UIInputPriority.Blocking,
                                    LowerLayers = UILowerLayerPolicy.VisibleInert
                                });
                            return externalButton;
                        }
                        return null;
                    }
                });
            AssertThat(owner.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Arm the delegate so the next RestoreFocus invocation (during the
            // owner's deferred restoration) opens the Blocking owner and
            // returns the external Button. IsControlEffectivelyInteractive
            // permits unowned controls, so without the fix the old restoration
            // focuses outside the newly opened top owner until a later deferred
            // initial-focus callback corrects it.
            armed = true;
            AssertThat(fixture.Host.TryClose(
                owner.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);

            // The deferred CompleteRestoration runs on the next frame and
            // invokes the armed RestoreFocus delegate. Keep armed until after
            // that frame so the delegate opens the Blocking owner.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            armed = false;

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(blockingResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(blockingResult.Value.Handle!.Value)).IsTrue();
            // The external Button must not have focus — it is outside the new
            // Blocking top owner. Focus should have landed inside the new
            // owner's subtree instead.
            AssertThat(externalButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == externalButton).IsFalse();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(externalButton) &&
                externalButton.IsInsideTree())
            {
                externalButton.GetParent()?.RemoveChild(externalButton);
            }
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_RestoreFocusOpensBlockingOwnerAndReturnsOwnedTarget_DoesNotFocusBeneathNewOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        parentView.AddChild(parentButton);
        var childView = fixture.Track(new Control { Visible = true });
        var blockingView = fixture.Track(new Control { Visible = true });
        var armed = false;
        UIScreenOpenResult? blockingResult = null;
        try
        {
            // Parent P with a focusable Button inside its view.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Screen
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Child C (child of P) with a RestoreFocus that, when armed,
            // opens a new Blocking Modal owner U with LowerLayers.VisibleInteractive
            // and returns P's Button. P remains interactive (VisibleInteractive),
            // so IsControlEffectivelyInteractive permits P's Button. But P's
            // Button is owned by P, which is NOT in U's subtree. Without the
            // fix, TargetOutsideNewTopOwner exempts owned targets, so the old
            // restoration focuses P's Button beneath U — a transient state
            // observable via FocusEntered side effects and keyboard/controller
            // input until U's deferred initial-focus callback corrects it.
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent.Handle,
                    RestoreFocus = () =>
                    {
                        if (armed)
                        {
                            blockingResult = fixture.Host.TryPresent(
                                blockingView,
                                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                {
                                    Layer = UIScreenLayer.Modal,
                                    InputPriority = UIInputPriority.Blocking,
                                    LowerLayers = UILowerLayerPolicy.VisibleInteractive
                                });
                            return parentButton;
                        }
                        return null;
                    }
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Arm the delegate so the next RestoreFocus invocation (during the
            // child's deferred restoration) opens the Blocking owner and
            // returns P's Button.
            armed = true;
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            armed = false;

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(blockingResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(blockingResult.Value.Handle!.Value)).IsTrue();
            // P's Button must not have focus — it is owned by P, which is
            // outside the new Blocking top owner U's subtree. Focus should
            // have landed inside U's subtree instead.
            AssertThat(parentButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == parentButton).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    private sealed partial class OpensHigherOwnerOnReadyControl : Control
    {
        public UIScreenHost Host { get; init; } = null!;
        public Control HigherView { get; init; } = null!;
        public System.Func<Control?> HigherInitialFocus { get; init; } = null!;
        public UIScreenOpenResult HigherResult { get; private set; }

        public override void _Ready()
        {
            HigherResult = Host.TryPresent(
                HigherView,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInteractive,
                    InitialFocus = HigherInitialFocus
                });
        }
    }

    [TestCase]
    public async Task FocusRestoration_BlockingOwnerOpenedBeforeDeferredCallback_DoesNotFocusParentBeneathNewOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        parentView.AddChild(parentButton);
        var childView = fixture.Track(new Control { Visible = true });
        var childButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        childView.AddChild(childButton);
        var blockingView = fixture.Track(new Control { Visible = true });
        var parentFocusEnteredDuringRestoration = false;
        try
        {
            // Parent P (Screen) with a focusable Button. P's deferred
            // ApplyInitialFocus focuses parentButton on the next frame.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Screen
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(parentButton.HasFocus()).IsTrue();

            // Child C (child of P) with a focusable Button. C's
            // CaptureParentFocus records parentButton as P's FocusOwner in C's
            // parent record. C's deferred ApplyInitialFocus focuses childButton.
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent.Handle
                });
            AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(childButton.HasFocus()).IsTrue();

            // Arm a FocusEntered observer on parentButton so the test can
            // detect the transient focus that the buggy restoration would
            // place on it beneath the new top owner. Reset it after the
            // initial-focus phase so only restoration-period focus is recorded.
            parentButton.FocusEntered += () =>
                parentFocusEnteredDuringRestoration = true;
            parentFocusEnteredDuringRestoration = false;

            // Close C. ProcessClose detaches C's view (childButton loses focus)
            // and schedules a DEFERRED restoration that targets C's parent
            // record (parentButton). TryClose returns Closed before the
            // deferred callback runs.
            AssertThat(fixture.Host.TryClose(
                child.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(childButton.IsInsideTree()).IsFalse();

            // SYNCHRONOUSLY open a Blocking Modal owner U with
            // LowerLayers.VisibleInteractive AFTER TryClose returned but BEFORE
            // the deferred restoration frame. U becomes TopInputOwner; P remains
            // VisibleInteractive. When the deferred restoration callback begins,
            // topOwnerBefore is already U (the owner appeared before the
            // callback, not during it). Without the fix,
            // TargetOutsideNewTopOwner only rejects when the top owner CHANGED
            // during the callback, so topOwnerNow == topOwnerBefore == U means
            // no rejection — the parent-focus path focuses parentButton beneath
            // U until U's deferred ApplyInitialFocus corrects it. That transient
            // focus is observable via FocusEntered and keyboard/controller
            // ownership.
            var blocking = fixture.Host.TryPresent(
                blockingView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInteractive
                });
            AssertThat(blocking.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(blocking.Handle);

            // Run the deferred restoration and U's deferred ApplyInitialFocus.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // With the fix, restoration never focuses parentButton beneath U:
            // TargetOutsideNewTopOwner rejects any target outside the current
            // top owner, so the parent-focus path is skipped and focus lands
            // inside U's subtree (U's sink via the top-entry path, then U's
            // ApplyInitialFocus). Without the fix, parentButton.GrabFocus()
            // fired during restoration, setting the flag.
            AssertThat(parentFocusEnteredDuringRestoration).IsFalse();
            AssertThat(parentButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == parentButton).IsFalse();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Diagnostics_FocusStatesDoesNotInvokeFocusViewportDelegate()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { Visible = true });
        var focusViewportInvocations = 0;
        try
        {
            // Entry with a custom FocusViewport delegate that counts every
            // invocation. The delegate is legitimately invoked at registration
            // (to commit the viewport for diagnostics) and during deferred
            // ApplyInitialFocus. After those settle, reading
            // host.Diagnostics.FocusStates must NOT re-invoke the delegate —
            // diagnostics are documented as read-only. Without the fix,
            // SnapshotDiagnostics calls SafeFocusViewport per entry, so each
            // diagnostics read invokes the caller-controlled delegate, which
            // can synchronously open/close entries, run cleanup, change pause
            // and lower-layer effects, and return a snapshot based on the
            // now-stale inputOrder.
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        focusViewportInvocations++;
                        return view.GetViewport();
                    }
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Let the deferred ApplyInitialFocus (which invokes FocusViewport)
            // settle so the only later delegate invocations would come from
            // diagnostics reads.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var settledInvocations = focusViewportInvocations;
            AssertThat(settledInvocations).IsGreater(0);

            // Reading diagnostics multiple times must not invoke the
            // FocusViewport delegate. SnapshotDiagnostics must use the viewport
            // committed at registration, not re-query user code.
            for (var i = 0; i < 3; i++)
            {
                var states = fixture.Host.Diagnostics.FocusStates;
                AssertThat(states.Count).IsEqual(1);
                AssertThat(states[0].Handle).IsEqual(opened.Handle!.Value);
            }

            AssertThat(focusViewportInvocations).IsEqual(settledInvocations);

            // Closing the entry invokes FocusViewport via CloseEntry's
            // SafeFocusViewport (a mutation, not a read), which is allowed.
            // This confirms the delegate is still wired and the earlier
            // non-increment was specifically because diagnostics used the
            // committed viewport.
            var beforeClose = focusViewportInvocations;
            fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);
            // CloseEntry invokes SafeFocusViewport; the count must increase.
            AssertThat(focusViewportInvocations).IsGreater(beforeClose);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_CandidateFocusViewportClosesSelf_ReturnsInvalidNodeAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { Visible = true });
        var closedFromDelegate = false;
        try
        {
            // The candidate's own FocusViewport delegate is invoked during
            // Register() (after the model entry, adapter, ownership metadata,
            // and tree-exit handler are committed). The delegate finds the
            // candidate through ActiveEntries and closes it synchronously.
            // Without the liveness check in Register(), CloseEntry finds no
            // focus entry (Register() hasn't added it yet) and returns a
            // no-op, so Register() adds an orphan focus entry and TryPresent
            // returns the original Opened result for a handle that is no
            // longer active. With the fix, Register() detects the liveness
            // failure, removes the DynamicSink, and returns false; TryPresent
            // returns InvalidNode.
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    FocusViewport = () =>
                    {
                        foreach (var entry in fixture.Host.ActiveEntries)
                        {
                            if (entry.Policy.Kind == UIScreenKinds.Pause)
                            {
                                closedFromDelegate = true;
                                fixture.Host.TryClose(
                                    entry.Handle,
                                    UIScreenCloseReason.Programmatic);
                                break;
                            }
                        }
                        return view.GetViewport();
                    }
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(opened.Handle).IsNull();
            AssertThat(closedFromDelegate).IsTrue();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_SupersededLeaseDoesNotFocusUnderStaleTopOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        parentView.AddChild(parentButton);
        var childView = fixture.Track(new Control { Visible = true });
        var u1View = fixture.Track(new Control { Visible = true });
        var u2View = fixture.Track(new Control { Visible = true });
        try
        {
            // P (Screen, with a focusable Button) is opened and its Button
            // receives focus. Child C (Parent = P) captures P's Button as the
            // parent focus owner. C is closed, starting a deferred restoration
            // lease targeting P's Button. Before that deferred restoration
            // runs, two unrelated Blocking owners U1 and U2 are opened (U2 on
            // top). Closing U2 calls BeginRestoration, which synchronously
            // completes C's still-pending lease BEFORE Recompute updates
            // CurrentState. Without the fix, TargetOutsideNewTopOwner reads
            // the stale CurrentState.TopInputOwner (U2), doesn't find U2 in
            // _entries, and returns false — so P's Button is focused beneath
            // U1. With the fix, the top owner is computed from the live model
            // input order (U1), P's Button is outside U1's subtree, and the
            // parent-focus path is rejected.
            fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            parentButton.GrabFocus();

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = fixture.Host.ActiveEntries[0].Handle
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Close C — its deferred restoration is scheduled but has not run.
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            // Open U1 and U2 synchronously before C's deferred restoration
            // runs. Both are Blocking Modal so they outrank P in input order.
            // U2 (opened second) is the top input owner.
            fixture.Host.TryPresent(
                u1View,
                UIScreenHostTestSupport.Spec(BlockingFixtureA) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });
            fixture.Host.TryPresent(
                u2View,
                UIScreenHostTestSupport.Spec(BlockingFixtureB) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });

            // Close U2 synchronously. ProcessClose calls BeginRestoration
            // (which completes C's lease synchronously) BEFORE Recompute.
            // Check P's Button focus immediately — before the next frame
            // corrects it via U2's deferred restoration.
            fixture.Host.TryClose(
                fixture.Host.ActiveEntries[0].Handle,
                UIScreenCloseReason.Programmatic);

            // With the fix: P's Button must NOT have focus — it is outside
            // the real top owner U1. Without the fix, the stale
            // CurrentState.TopInputOwner (U2) caused TargetOutsideNewTopOwner
            // to return false and P's Button was focused beneath U1.
            AssertThat(parentButton.HasFocus()).IsFalse();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(parentButton.HasFocus()).IsFalse();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_BlockingControlRootSink_NotRejectedAsOutsideTopOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        var focusViewportCallCount = 0;
        var closeParentOnNextFocusViewport = false;
        UIScreenHandle? parentHandle = null;
        try
        {
            // Parent P is a Blocking Control with no focusable descendants.
            // Initial focus lands on _rootSink (the host's FocusSink). When
            // child C opens, CaptureParentFocus records _rootSink as P's
            // FocusOwner. When C closes, the parent-focus restoration path
            // must accept _rootSink as P's designated sink (not reject it as
            // outside P's view subtree). Without the fix, the valid captured
            // sink is rejected and restoration falls through to the parent
            // initial-focus path, which invokes P's FocusViewport — if that
            // delegate closes P, P is unexpectedly closed. With the fix,
            // _rootSink is accepted and focused directly; FocusViewport is
            // not invoked during restoration.
            parentHandle = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    FocusViewport = () =>
                    {
                        focusViewportCallCount++;
                        if (closeParentOnNextFocusViewport && parentHandle.HasValue)
                        {
                            closeParentOnNextFocusViewport = false;
                            fixture.Host.TryClose(
                                parentHandle.Value,
                                UIScreenCloseReason.Programmatic);
                        }
                        return parentView.GetViewport();
                    }
                }).Handle;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // P's initial focus landed on _rootSink.
            var sink = fixture.Host.GetNode<Control>("FocusSink");
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(sink);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parentHandle!.Value
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Arm FocusViewport to close P on the next invocation. If the
            // parent-focus path rejects _rootSink and falls through to the
            // parent initial-focus path, FocusViewport will be invoked and
            // P will be closed.
            closeParentOnNextFocusViewport = true;
            var focusViewportCountBeforeClose = focusViewportCallCount;
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // With the fix: the parent-focus path accepted _rootSink as P's
            // designated sink and focused it directly. FocusViewport was NOT
            // invoked during restoration, so P is still active.
            AssertThat(fixture.Host.IsActive(parentHandle!.Value)).IsTrue();
            AssertThat(focusViewportCallCount).IsEqual(focusViewportCountBeforeClose);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(sink);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_CandidateFocusViewportTriggersTeardown_ReturnsInvalidNodeAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { Visible = true });
        var teardownStarted = false;
        try
        {
            // The candidate's own FocusViewport delegate is invoked during
            // Register() (after the model entry, adapter, ownership metadata,
            // and tree-exit handler are committed). The delegate calls
            // PrepareForTeardown(), which begins teardown, closes every entry
            // (including the candidate via the full CloseAdapter path), and
            // finalizes: _focusCoordinator.Teardown() sets _host to null and
            // clears _entries. Without treating a null host as failure, the
            // guard `_host != null && !_host.IsActive(handle)` short-circuits
            // to false on the null host, Register() adds an orphan focus
            // entry, schedules initial focus, and returns true; TryPresent
            // then calls Recompute() and returns a stale Opened result for a
            // handle whose host is finalized. With the fix, a null (or
            // tearing-down) host is failure: Register() removes the sink and
            // returns false, and TryPresent returns InvalidNode.
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    FocusViewport = () =>
                    {
                        teardownStarted = true;
                        fixture.Host.PrepareForTeardown();
                        return view.GetViewport();
                    }
                });

            AssertThat(teardownStarted).IsTrue();
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(opened.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_CandidateFocusViewportOpensPauseTreeOwner_RollbackPausableCandidate()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var candidateView = fixture.Track(new Control
        {
            Visible = true,
            ProcessMode = ProcessModeEnum.Inherit
        });
        var pauseOwnerView = fixture.Track(new Control { Visible = true });
        try
        {
            // The candidate is Pausable. Its FocusViewport delegate (invoked
            // during Register()) opens a PauseTree owner. The candidate's
            // process mode was validated and assigned during Apply() against
            // the pre-Register pause state (no PauseTree owner). After the
            // callback, the candidate's Pausable mode is invalid under the
            // now-paused tree. Without post-Register revalidation, TryPresent
            // returns Opened for a candidate whose process-mode validation
            // was bypassed. With the fix, a generation change after Register
            // triggers the same transactional validation used after _Ready()
            // (RevalidateAfterApply), which rejects the Pausable candidate
            // (InvalidProcessPolicy) and rolls back the committed adapter,
            // focus entry, and ownership metadata.
            var opened = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Pausable,
                    FocusViewport = () =>
                    {
                        fixture.Host.TryPresent(
                            pauseOwnerView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                            {
                                PauseTree = true
                            });
                        return candidateView.GetViewport();
                    }
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.InvalidProcessPolicy);
            AssertThat(opened.Handle).IsNull();
            // The candidate was rolled back; only the pause owner remains.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.ActiveEntries[0].Policy.Kind)
                .IsEqual(UIScreenKinds.Settings);
            // The candidate's focus entry was removed; only the pause owner's
            // focus entry remains.
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);
            // The candidate's view was detached (External lifetime rollback).
            AssertThat(candidateView.GetParent()).IsNull();
            // The candidate's process mode was restored to its incoming value.
            AssertThat(candidateView.ProcessMode).IsEqual(ProcessModeEnum.Inherit);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_CandidateFocusViewportQueuesViewForDeletion_ReturnsInvalidNode()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control { Visible = true });
        try
        {
            // The candidate's FocusViewport delegate (invoked during
            // Register()) queues the candidate view for deletion without
            // closing the model handle. QueueFree does not fire TreeExiting
            // synchronously and does not bump MutationGeneration, so the
            // generation guard and the tree-exit path do not catch it.
            // Without a node-validity recheck after Register, TryPresent
            // returns Opened for a view that is queued for deletion —
            // inconsistent with the top-of-TryPresent check that rejects
            // queued views. With the fix, the post-Register node-validity
            // check detects IsQueuedForDeletion, rolls back the committed
            // candidate, and returns InvalidNode.
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    FocusViewport = () =>
                    {
                        view.QueueFree();
                        return view.GetViewport();
                    }
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(opened.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task FocusRestoration_PendingTopOwnerBlocksRestorationBeneathCandidate()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var parentButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        parentView.AddChild(parentButton);
        var siblingView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var candidateView = fixture.Track(new Control { Visible = true });
        try
        {
            // P (Screen, focusable Button) is opened and its Button receives
            // focus. Sibling S (Screen) is opened independently. Child C
            // (Parent = P) captures P's Button as the parent focus owner.
            // Closing C starts a deferred restoration lease R1 targeting P's
            // Button. Before R1's deferred completion runs, a Blocking
            // candidate B is opened. During B's Register() (which invokes B's
            // FocusViewport), B's delegate closes S. Closing S calls
            // BeginRestoration, which synchronously completes the
            // still-pending R1. At that moment B is the live top input owner
            // (model-visible) but its focus entry is not yet committed
            // (Register has not added it to _entries). Without the fix,
            // TargetOutsideNewTopOwner returns false when the top owner is
            // absent from _entries, so R1 focuses P's Button beneath the
            // visible pending candidate B. With the fix, the missing committed
            // focus state for the live top owner blocks the target; R1 aborts
            // (releases focus) and B's own deferred ApplyInitialFocus claims
            // focus next frame.
            fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause));
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            parentButton.GrabFocus();
            var parentHandle = fixture.Host.ActiveEntries[0].Handle;

            var siblingHandle = fixture.Host.TryPresent(
                siblingView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory)).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parentHandle
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Close C — its deferred restoration R1 (targeting P's Button) is
            // scheduled but has not run.
            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            // Open B (Blocking Modal). Its FocusViewport delegate closes S
            // during Register(), before B's focus entry is committed. Closing
            // S synchronously completes R1 while B is the live top owner but
            // absent from _entries.
            var candidateOpened = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    FocusViewport = () =>
                    {
                        fixture.Host.TryClose(
                            siblingHandle,
                            UIScreenCloseReason.Programmatic);
                        return candidateView.GetViewport();
                    }
                });

            AssertThat(candidateOpened.Status).IsEqual(UIScreenOpenStatus.Opened);
            // R1 completed synchronously while B was pending. With the fix,
            // R1 did NOT focus P's Button beneath B; the missing committed
            // focus state for the live top owner blocked the target. B's
            // deferred ApplyInitialFocus has not run yet (same frame), so the
            // focus owner is NOT P's Button.
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNotEqual(parentButton);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            // P and B remain (S and C closed). B's deferred ApplyInitialFocus
            // claimed focus; no restoration lease is pending.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task CloseInertingOwner_RestorationUsesFreshLowerLayerEffectsBeforeRecompute()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        lowerView.AddChild(lowerButton);
        var ownerAView = fixture.Track(new Control { Visible = true });
        var ownerBView = fixture.Track(new Control { Visible = true });
        try
        {
            // L (Screen, focusable Button) is opened and its Button receives
            // focus. A then B are Blocking Modal owners that inert every lower
            // entry (VisibleInert). B is opened above A, so B inerts both A
            // and L; A inerts L. Focus rests on the shared root sink owned by
            // the top Blocking entry.
            fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Screen
                });
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            lowerButton.GrabFocus();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            var ownerAHandle = fixture.Host.TryPresent(
                ownerAView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var ownerBHandle = fixture.Host.TryPresent(
                ownerBView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Close B (synchronous). ProcessClose starts a deferred
            // restoration lease R_B for B, THEN Recomputes — so the committed
            // _resolvedLowerLayerEffects now reflects {A: interactive,
            // L: VisibleInert (inerted by A)}. R_B is pending (deferred).
            fixture.Host.TryClose(ownerBHandle, UIScreenCloseReason.Programmatic);
            // B was topmost; after closing it A is topmost again.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);

            // Close A synchronously, in the SAME frame, before R_B's deferred
            // completion runs. ProcessClose calls BeginRestoration, which
            // finds R_B still active and SYNCHRONOUSLY completes it before
            // A's Recompute. At that moment the live model has both A and B
            // removed (L is the only entry, now interactive), but the host's
            // committed _resolvedLowerLayerEffects still reflects the
            // post-B-close state (L inerted by A). The restoration must
            // resolve lower-layer effects FRESHLY from the current model for
            // the whole transaction; otherwise FindTopEntry/CurrentTopInputOwner
            // read the stale snapshot, skip L (still VisibleInert), and
            // release focus instead of restoring it to L's Button.
            fixture.Host.TryClose(ownerAHandle, UIScreenCloseReason.Programmatic);

            // Synchronous window (before any deferred callback): with the
            // fresh-snapshot fix, R_B restored focus to L's Button because L
            // is interactive in the current model. Without the fix, R_B read
            // the stale committed snapshot, skipped L, and released focus.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Viewport.GuiGetFocusOwner())
                .IsEqual(lowerButton);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // After the deferred restoration for A completes, L remains the
            // sole interactive owner and focus stays on its Button. No
            // restoration lease lingers.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Viewport.GuiGetFocusOwner())
                .IsEqual(lowerButton);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending)
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task RestoreFocusProviderOpensInertingHigherModal_ReResolvesEffectsAndDoesNotFocusInertBlockingEntry()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        lowerView.AddChild(lowerButton);
        var blockingView = fixture.Track(new Control { Visible = true });
        var closedOwnerView = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var modalButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false,
            Visible = true
        };
        modalView.AddChild(modalButton);
        var focusSink = fixture.Host.GetNode<Control>("FocusSink");
        UIScreenHandle? closedOwnerHandle = null;
        try
        {
            // L (Screen, focusable Button) is opened and focused.
            fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Screen
                });
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            lowerButton.GrabFocus();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(lowerButton);

            // B (Blocking, ScreenLayer, VisibleInert) inerts L and becomes the
            // top input owner (Blocking outranks Screen). Focus rests on the
            // shared root FocusSink owned by the top Blocking entry.
            var blockingHandle = fixture.Host.TryPresent(
                blockingView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // C (Blocking, ModalLayer, VisibleInert) opens above B. C is
            // visually higher (ModalLayer > ScreenLayer) so it inerts B; C is
            // also newer and Blocking, so it is the top input owner. C's
            // RestoreFocus provider opens M (Modal, ModalLayer, VisibleInert)
            // during C's deferred restoration.
            closedOwnerHandle = fixture.Host.TryPresent(
                closedOwnerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    RestoreFocus = () =>
                    {
                        fixture.Host.TryPresent(
                            modalView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                            {
                                Layer = UIScreenLayer.Modal,
                                InputPriority = UIInputPriority.Modal,
                                LowerLayers = UILowerLayerPolicy.VisibleInert
                            });
                        return null;
                    }
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Close C. Its deferred restoration runs RestoreFocus, which opens
            // M above B. At restoration start B is the top owner
            // (VisibleInteractive in the pre-callback snapshot); after the
            // provider opens M, B is inerted (VisibleInert) while still
            // ranking first in input order (Blocking > Modal). With the
            // generation-aware restart, the coordinator re-resolves effects
            // and re-runs FindTopEntry, selecting M (the new interactive top)
            // and focusing M's button — NOT B's root FocusSink beneath the
            // Modal. Without the fix, FindTopEntry used the stale snapshot
            // (B still VisibleInteractive), selected B, and focused the root
            // FocusSink beneath the newly opened Modal.
            fixture.Host.TryClose(closedOwnerHandle.Value, UIScreenCloseReason.Programmatic);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // M is active; B remains active but inert (inerted by M's
            // VisibleInert lower-layer policy, since M is on ModalLayer above
            // B's ScreenLayer). L remains inert (inerted by B). The policy
            // resolver selects B as TopInputOwner (Blocking priority outranks
            // Modal and is first in input order), but the focus coordinator's
            // FindTopEntry skips inerted entries and selects M for focus.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(3);
            AssertThat(fixture.Host.IsActive(blockingHandle)).IsTrue();

            // Focus must rest on M's button (the new interactive top owner's
            // descendant), not on the root FocusSink beneath the Modal. Without
            // the generation-aware restart, FindTopEntry used the stale
            // pre-callback snapshot (B still VisibleInteractive), selected B,
            // and focused the root FocusSink beneath the newly opened Modal.
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(modalButton);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsNotEqual(focusSink);

            // No restoration lease lingers.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending)
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    // Regression for the third correctness gap in deferred initial focus:
    // ApplyInitialFocus gates only on the candidate's OWN eligibility
    // (IsStillFocusEligible: active, TopInputOwner, own reduced effect ==
    // VisibleInteractive). It does not validate the InitialFocus delegate's
    // returned target against the current lower-layer effects or the current
    // top owner — the same ownership checks restoration applies via
    // IsControlEffectivelyInteractive / TargetOutsideNewTopOwner. A modal's
    // InitialFocus can return a control that lives inside a LOWER entry the
    // modal has already reduced to VisibleInert. The deferred ApplyInitialFocus
    // runs AFTER Recompute's RevokeFocusWithin, so it bypasses the inert-subtree
    // revocation and reintroduces keyboard/controller focus behind the modal.
    [TestCase]
    public async Task DeferredInitialFocus_TargetInsideAlreadyInertedLowerEntry_DoesNotFocusInertSubtree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var lowerButton = new Button
        {
            FocusMode = Control.FocusModeEnum.All,
            Disabled = false
        };
        lowerView.AddChild(lowerButton);
        var modalView = fixture.Track(new Control { Visible = true });
        try
        {
            // Lower Screen entry with a focusable button. It is the initial
            // top input owner; its deferred ApplyInitialFocus focuses
            // lowerButton via the descendant scan.
            var lower = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Screen
                });
            AssertThat(lower.Status).IsEqual(UIScreenOpenStatus.Opened);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(lowerButton.HasFocus()).IsTrue();

            // Modal owner on ModalLayer, Blocking, with VisibleInert lower
            // layers. It inerts the lower entry (VisibleInert) and becomes the
            // top input owner (Blocking outranks Screen). RevokeFocusWithin
            // redirects focus from lowerButton to the Modal's sink — the
            // Modal has no focusable descendant and is a Blocking Control, so
            // the root FocusSink is used. The Modal's InitialFocus provider
            // returns lowerButton, a control inside the already-inerted lower
            // entry.
            var modal = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    InitialFocus = () => lowerButton
                });
            AssertThat(modal.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(modal.Handle);
            AssertThat(fixture.Host.LowerLayerEffectFor(lower.Handle!.Value))
                .IsEqual(UILowerLayerPolicy.VisibleInert);

            // Let the Modal's deferred ApplyInitialFocus run. It must NOT
            // focus lowerButton: the target lives inside a lower entry the
            // modal has reduced to VisibleInert, i.e. behind the modal.
            // ApplyInitialFocus must validate the InitialFocus target against
            // the current lower-layer effects and the current top owner
            // (rejecting a target outside the top owner's subtree / inside an
            // inerted entry), fall through the descendant scan (no focusable
            // descendant in the Modal), and focus the Modal's sink. Without
            // that validation, the deferred callback reintroduces focus into
            // the inert subtree through a path that bypasses
            // RevokeFocusWithin.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var focusSink = fixture.Host.GetNode<Control>("FocusSink");
            AssertThat(lowerButton.HasFocus()).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner() == lowerButton).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(focusSink);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }
}
