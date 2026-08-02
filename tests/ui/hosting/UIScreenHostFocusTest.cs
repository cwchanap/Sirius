using System.Threading.Tasks;
using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostFocusTest : Node
{
    private static readonly StringName UiCancelAction = "ui_cancel";

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
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
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
    public async Task TryPresentCallback_CloseSeesFocusStateAndLeavesNoWindowSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window { Visible = true });
        var callbackEntered = false;
        var focusStateCountDuringCallback = -1;
        UIScreenCloseStatus? closeStatus = null;
        fixture.Host.EffectiveStateChanged += state =>
        {
            if (callbackEntered || !state.TopInputOwner.HasValue)
                return;

            callbackEntered = true;
            focusStateCountDuringCallback = fixture.Host.Diagnostics.FocusStates.Count;
            closeStatus = fixture.Host.TryClose(
                state.TopInputOwner.Value,
                UIScreenCloseReason.Programmatic).Status;
        };
        try
        {
            var opened = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(closeStatus).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(focusStateCountDuringCallback).IsEqual(1);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsFalse();
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);

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
                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
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
                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
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
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
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

            fixture.Host.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

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
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInteractive,
                    InitialFocus = HigherInitialFocus
                });
        }
    }

    private async Task DisposeFixture(HostFixture fixture)
    {
        fixture.Dispose();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }
}
