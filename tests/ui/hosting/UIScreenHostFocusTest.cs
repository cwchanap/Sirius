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

    private async Task DisposeFixture(HostFixture fixture)
    {
        fixture.Dispose();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }
}
