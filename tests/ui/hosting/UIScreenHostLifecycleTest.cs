using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostLifecycleTest : Node
{
    [TestCase]
    public async Task PauseLease_RestoresIncomingFalse()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        try
        {
            tree.Paused = false;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });

            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.Host.CurrentState.IsTreePauseOwned).IsTrue();

            fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsFalse();
            AssertThat(fixture.Host.CurrentState.IsTreePauseOwned).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PauseLease_RestoresIncomingTrue()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        try
        {
            tree.Paused = true;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });

            AssertThat(tree.Paused).IsTrue();

            fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.Host.CurrentState.IsTreePauseOwned).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task NestedNonPausingChild_RetainsParentPauseLease()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var pauseView = fixture.Track(new Control());
        var settingsView = fixture.Track(new Control());
        try
        {
            tree.Paused = false;
            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                }).Handle!.Value;
            var settings = fixture.Host.TryPresent(
                settingsView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = pause,
                    PauseTree = false
                }).Handle!.Value;

            fixture.Host.TryClose(settings, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.Host.CurrentState.IsTreePauseOwned).IsTrue();
            AssertThat(fixture.Host.IsActive(pause)).IsTrue();

            fixture.Host.TryClose(pause, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task CursorLease_RetainsBaselineAcrossNestedOverrideChanges()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        try
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            var incomingMouseMode = Input.MouseMode;
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cursor = UICursorPolicy.Visible
                }).Handle!.Value;
            AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Cursor = UICursorPolicy.Hidden
                }).Handle!.Value;
            AssertThat(fixture.Host.CurrentState.Cursor).IsEqual(UICursorPolicy.Hidden);

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);

            fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);
            AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HudLease_RetainsBaselineAcrossNestedOverrideChanges()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        try
        {
            fixture.HudRoot.Visible = false;
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Hud = UIHudPolicy.Visible
                }).Handle!.Value;
            AssertThat(fixture.HudRoot.Visible).IsTrue();

            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Hud = UIHudPolicy.Hidden
                }).Handle!.Value;
            AssertThat(fixture.HudRoot.Visible).IsFalse();

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            AssertThat(fixture.HudRoot.Visible).IsTrue();

            fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);
            AssertThat(fixture.HudRoot.Visible).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ExplicitHudPolicy_WithoutConfiguredHud_IsRejectedAtomically()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            options: new UIScreenHostOptions());
        var view = fixture.Track(new Control());
        try
        {
            var incomingState = fixture.Host.CurrentState;

            var result = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Hud = UIHudPolicy.Hidden
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
            AssertThat(result.Handle).IsNull();
            AssertThat(view.GetParent()).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.CurrentState).IsEqual(incomingState);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task GameplayBlockCallback_EmitsOnlyFirstOwnerAndLastOwnerTransitions()
    {
        var transitions = new List<bool>();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            options: new UIScreenHostOptions
            {
                GameplayInputBlockChanged = blocked => transitions.Add(blocked)
            });
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    BlockGameplayInput = true
                }).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    BlockGameplayInput = true
                }).Handle!.Value;

            fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);
            fixture.Host.TryClose(parent, UIScreenCloseReason.Programmatic);

            AssertThat(transitions.ToArray()).ContainsExactly(true, false);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EffectiveStateChanged_PublishesAfterCompleteMutationsOnly()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        var publishedCount = 0;
        var publicationsWereConsistent = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        var incomingMouseMode = Input.MouseMode;
        fixture.Host.EffectiveStateChanged += state =>
        {
            publishedCount++;
            publicationsWereConsistent &= fixture.Host.CurrentState == state;
            publicationsWereConsistent &= state.IsTreePauseOwned
                ? tree.Paused &&
                  Input.MouseMode == Input.MouseModeEnum.Visible &&
                  !fixture.HudRoot.Visible
                : !tree.Paused &&
                  Input.MouseMode == incomingMouseMode &&
                  fixture.HudRoot.Visible;
        };
        try
        {
            tree.Paused = false;
            fixture.HudRoot.Visible = true;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true,
                    BlockGameplayInput = true,
                    Cursor = UICursorPolicy.Visible,
                    Hud = UIHudPolicy.Hidden
                });

            fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);
            fixture.Host.TryClose(opened.Handle.Value, UIScreenCloseReason.Programmatic);

            AssertThat(publishedCount).IsEqual(2);
            AssertThat(publicationsWereConsistent).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PauseDrift_IsCountedReassertedAndDoesNotReplaceBaseline()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        try
        {
            tree.Paused = false;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });
            AssertThat(fixture.Host.Diagnostics.PauseOwnershipDriftCount).IsEqual(0);

            tree.Paused = false;
            fixture.Host._Process(0);
            fixture.Host._Process(0);

            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.Host.Diagnostics.PauseOwnershipDriftCount).IsEqual(1);

            fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HostTeardown_RestoresAllActiveLeaseBaselines()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var blockTransitions = new List<bool>();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            options: new UIScreenHostOptions
            {
                HudRoot = null,
                GameplayInputBlockChanged = blocked => blockTransitions.Add(blocked)
            });
        var view = fixture.Track(new Control());
        try
        {
            tree.Paused = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            var incomingMouseMode = Input.MouseMode;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true,
                    BlockGameplayInput = true,
                    Cursor = UICursorPolicy.Visible
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            fixture.Host.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(tree.Paused).IsFalse();
            AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
            AssertThat(blockTransitions.ToArray()).ContainsExactly(true, false);
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
