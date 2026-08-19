using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostInputTest : Node
{
    private static readonly StringName PauseMenuAction = "pause_menu";
    private static readonly StringName UiCancelAction = "ui_cancel";
    private static readonly StringName ToggleInventoryAction = "toggle_inventory";
    private static readonly StringName ErrorFixture = "error_fixture";

    [TestCase]
    public async Task PhysicalInputMatchingTwoCoreActions_TraversesInterceptorOnce()
    {
        var interceptorCalls = 0;
        var matchedCoreCount = 0;
        var fixture = await CreateConfiguredHost(
            new HashSet<StringName> { PauseMenuAction, UiCancelAction });
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cancel = UICancelPolicy.Close,
                    InterceptCancel = context =>
                    {
                        interceptorCalls++;
                        matchedCoreCount = context.MatchedCoreActions.Count;
                        return UIInputInterception.ConsumeHere;
                    }
                });
            var inputEvent = UIScreenHostTestSupport.EscapeBoundTo(
                fixture,
                PauseMenuAction,
                UiCancelAction);

            var result = fixture.Host.TryHandleInput(inputEvent);

            AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(interceptorCalls).IsEqual(1);
            AssertThat(matchedCoreCount).IsEqual(2);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EntryScopedAction_ClosesActiveInventory()
    {
        UIScreenCloseReason? closeReason = null;
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Cancel = UICancelPolicy.Close,
                    EntryCancelActions = new HashSet<StringName> { ToggleInventoryAction },
                    Cleanup = reason => closeReason = reason
                });

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(ToggleInventoryAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsFalse();
            AssertThat(closeReason).IsEqual(UIScreenCloseReason.Cancel);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EntryScopedAction_WithSettingsAboveInventory_HasNoOwner()
    {
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var inventoryView = fixture.Track(new Control());
        var settingsView = fixture.Track(new Control());
        try
        {
            var inventory = fixture.Host.TryPresent(
                inventoryView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Cancel = UICancelPolicy.Close,
                    EntryCancelActions = new HashSet<StringName> { ToggleInventoryAction }
                }).Handle!.Value;
            var settings = fixture.Host.TryPresent(
                settingsView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = inventory,
                    Cancel = UICancelPolicy.Close
                }).Handle!.Value;

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(ToggleInventoryAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.NoOwner);
            AssertThat(fixture.Host.IsActive(inventory)).IsTrue();
            AssertThat(fixture.Host.IsActive(settings)).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EntryScopedAction_StaticNone_DoesNotInvokeRootFallback()
    {
        var fallbackCalls = 0;
        var fixture = await CreateConfiguredHost(
            new HashSet<StringName> { UiCancelAction },
            _ =>
            {
                fallbackCalls++;
                return UIRootCancelResult.Consumed;
            });
        var view = fixture.Track(new Control());
        try
        {
            fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Cancel = UICancelPolicy.None,
                    EntryCancelActions = new HashSet<StringName> { ToggleInventoryAction }
                });

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(ToggleInventoryAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.NoOwner);
            AssertThat(fallbackCalls).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DynamicInterception_PrecedesStaticClosePolicy()
    {
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Cancel = UICancelPolicy.Close,
                    InterceptCancel = _ => UIInputInterception.ReserveForNativeHandler
                });

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.ReservedForTopEntry);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task StaticNone_ContinuesFromChildToParent()
    {
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var pauseView = fixture.Track(new Control());
        var settingsView = fixture.Track(new Control());
        try
        {
            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cancel = UICancelPolicy.Close
                }).Handle!.Value;
            var settings = fixture.Host.TryPresent(
                settingsView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = pause,
                    Cancel = UICancelPolicy.None
                }).Handle!.Value;

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(settings)).IsFalse();
            AssertThat(fixture.Host.IsActive(pause)).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PassThrough_ReservesForTopEntryWithoutClosing()
    {
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(ErrorFixture) with
                {
                    Cancel = UICancelPolicy.PassThrough
                });

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.ReservedForTopEntry);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task RootFallback_RunsOnlyWhenCoreActionHasNoEntryOwner()
    {
        var fallbackCalls = 0;
        var fixture = await CreateConfiguredHost(
            new HashSet<StringName> { UiCancelAction },
            _ =>
            {
                fallbackCalls++;
                return UIRootCancelResult.Consumed;
            });
        var view = fixture.Track(new Control());
        try
        {
            var unmatchedResult = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));
            fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Cancel = UICancelPolicy.Consume
                });
            var ownedResult = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(unmatchedResult).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(ownedResult).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fallbackCalls).IsEqual(1);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task UiCloseDialog_IsNotACoreAction()
    {
        var fallbackCalls = 0;
        var fixture = await CreateConfiguredHost(
            new HashSet<StringName> { PauseMenuAction, UiCancelAction },
            _ =>
            {
                fallbackCalls++;
                return UIRootCancelResult.Consumed;
            });
        try
        {
            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress("ui_close_dialog"));

            AssertThat(result).IsEqual(UIInputDispatchResult.NoOwner);
            AssertThat(fallbackCalls).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PassiveEntry_WithDynamicCancelInterceptor_IsRejected()
    {
        var fixture = await CreateConfiguredHost(new HashSet<StringName> { UiCancelAction });
        var view = fixture.Track(new Control());
        try
        {
            var result = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
                {
                    InputPriority = UIInputPriority.Passive,
                    InterceptCancel = _ => UIInputInterception.ConsumeHere
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public void LiveRestorationBarrier_ConsumesCoreBeforeEntryOrRootDispatch()
    {
        var interceptorCalls = 0;
        var fallbackCalls = 0;
        var handle = new UIScreenHandle(1, UIScreenKinds.Settings);
        var entry = UIScreenHostTestSupport.Snapshot(
            handle,
            UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
            {
                Cancel = UICancelPolicy.None
            },
            1);
        var dispatcher = new UIScreenInputDispatcher();
        var result = dispatcher.TryHandleInput(
            UIScreenHostTestSupport.ActionPress(UiCancelAction),
            new HashSet<StringName> { UiCancelAction },
            () => { },
            () => UIScreenHostTestSupport.Snapshots(entry),
            _ => _ =>
            {
                interceptorCalls++;
                return UIInputInterception.DeferToPolicy;
            },
            _ => new UIScreenCloseResult(UIScreenCloseStatus.Closed),
            () => new UIScreenEffectiveState(
                false,
                false,
                UICursorPolicy.Inherit,
                UIHudPolicy.Inherit,
                handle,
                true),
            _ =>
            {
                fallbackCalls++;
                return UIRootCancelResult.Declined;
            });

        AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
        AssertThat(interceptorCalls).IsEqual(0);
        AssertThat(fallbackCalls).IsEqual(0);
    }

    [TestCase]
    public async Task QueuedTopEntry_IsPrunedBeforeRootFallbackContextIsBuilt()
    {
        UIScreenHandle? fallbackTopOwner = default;
        var fixture = await CreateConfiguredHost(
            new HashSet<StringName> { UiCancelAction },
            context =>
            {
                fallbackTopOwner = context.EffectiveState.TopInputOwner;
                return UIRootCancelResult.Consumed;
            });
        var view = fixture.Track(new Control());
        try
        {
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Cancel = UICancelPolicy.None
                });
            view.QueueFree();

            var result = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsFalse();
            AssertThat(fallbackTopOwner).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EscapeBoundTo_RestoresOriginalInputEventInstanceAfterQueuedTeardown()
    {
        var action = new StringName($"ui_host_escape_restore_{GetInstanceId()}");
        var originalBinding = new InputEventKey { PhysicalKeycode = Key.P };
        InputMap.AddAction(action, 0.37f);
        InputMap.ActionAddEvent(action, originalBinding);
        var originalBindingInstanceId = InputMap.ActionGetEvents(action)[0].GetInstanceId();
        HostFixture? fixture = null;
        try
        {
            fixture = await UIScreenHostTestSupport.CreateHost(this, new[] { action });
            var escapePress = UIScreenHostTestSupport.EscapeBoundTo(fixture, action);

            AssertThat(escapePress.IsActionPressed(action)).IsTrue();
            AssertThat(InputMap.ActionGetEvents(action).Count).IsEqual(2);

            fixture.Dispose();
            fixture = null;
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            AssertThat(InputMap.HasAction(action)).IsTrue();
            AssertThat(InputMap.ActionGetDeadzone(action)).IsEqual(0.37f);
            AssertThat(InputMap.ActionGetEvents(action).Count).IsEqual(1);
            AssertThat(InputMap.ActionGetEvents(action)[0].GetInstanceId())
                .IsEqual(originalBindingInstanceId);
            AssertThat(new InputEventKey
            {
                PhysicalKeycode = Key.P,
                Pressed = true
            }.IsActionPressed(action)).IsTrue();
            AssertThat(new InputEventKey
            {
                PhysicalKeycode = Key.Escape,
                Pressed = true
            }.IsActionPressed(action)).IsFalse();
        }
        finally
        {
            fixture?.Dispose();
            if (InputMap.HasAction(action))
                InputMap.EraseAction(action);
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task<HostFixture> CreateConfiguredHost(
        IReadOnlySet<StringName> coreActions,
        System.Func<UIRootCancelContext, UIRootCancelResult>? rootFallback = null) =>
        await UIScreenHostTestSupport.CreateHost(
            this,
            options: new UIScreenHostOptions
            {
                CoreCancelActions = coreActions,
                RootCancelFallback = rootFallback
            });

    private async Task DisposeFixture(HostFixture fixture)
    {
        fixture.Dispose();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }
}
