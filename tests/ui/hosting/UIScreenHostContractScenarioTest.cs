using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostContractScenarioTest : Node
{
    private static readonly StringName UiCancelAction = "ui_cancel";

    [TestCase]
    public async Task InventoryChildOfPause_PausesWorldHidesHudAndReturnsToPause()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { UiCancelAction });
        var pauseView = fixture.Track(new Control { Visible = true });
        var inventoryView = fixture.Track(new Control { Visible = true });
        try
        {
            tree.Paused = false;
            fixture.HudRoot.Visible = true;
            var pause = Open(fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true,
                    BlockGameplayInput = true,
                    Hud = UIHudPolicy.Hidden,
                    Cancel = UICancelPolicy.Close
                }));
            var inventory = Open(fixture.Host.TryPresent(
                inventoryView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = pause,
                    BlockGameplayInput = true,
                    Cancel = UICancelPolicy.Close
                }));

            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.HudRoot.Visible).IsFalse();
            AssertThat(fixture.Host.IsActive(pause)).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(inventory);

            var inventoryClose = fixture.Host.TryClose(
                inventory,
                UIScreenCloseReason.ExplicitAction);

            AssertThat(inventoryClose.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(fixture.Host.IsActive(inventory)).IsFalse();
            AssertThat(fixture.Host.IsActive(pause)).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(pause);
            AssertThat(tree.Paused).IsTrue();
            AssertThat(fixture.HudRoot.Visible).IsFalse();

            var pauseClose = fixture.Host.TryClose(
                pause,
                UIScreenCloseReason.ExplicitAction);

            AssertThat(pauseClose.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(tree.Paused).IsFalse();
            AssertThat(fixture.HudRoot.Visible).IsTrue();
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task SettingsChildOfPause_PreservesPauseGameplayInertContribution()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplayView = fixture.Track(new Control { Visible = true });
        var pauseView = fixture.Track(new Control { Visible = true });
        var settingsView = fixture.Track(new Control { Visible = true });
        try
        {
            gameplayView.SetProcessInput(true);
            Open(fixture.Host.TryPresent(
                gameplayView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = gameplayView.SetProcessInput
                }));
            var pause = Open(fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    BlockGameplayInput = true,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    Cancel = UICancelPolicy.Close
                }));
            var settings = Open(fixture.Host.TryPresent(
                settingsView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = pause,
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden,
                    Cancel = UICancelPolicy.Close
                }));

            AssertThat(pauseView.Visible).IsFalse();
            AssertThat(gameplayView.Visible).IsFalse();
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(settings);

            fixture.Host.TryClose(settings, UIScreenCloseReason.ExplicitAction);

            AssertThat(fixture.Host.IsActive(settings)).IsFalse();
            AssertThat(fixture.Host.IsActive(pause)).IsTrue();
            AssertThat(pauseView.Visible).IsTrue();
            AssertThat(gameplayView.Visible).IsTrue();
            AssertThat(gameplayView.IsProcessingInput()).IsFalse();
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(pause);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task DestructiveConfirmation_CancelReturnsWithoutDestructiveCallback()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { UiCancelAction });
        fixture.Viewport.GuiEmbedSubwindows = true;
        var parentView = fixture.Track(new Control { Visible = true });
        var confirmation = fixture.Track(new AcceptDialog { Visible = true });
        var safeButton = new Button
        {
            Name = "Cancel",
            FocusMode = Control.FocusModeEnum.All
        };
        confirmation.AddChild(safeButton);
        var destructiveCount = 0;
        confirmation.Confirmed += () => destructiveCount++;
        try
        {
            var parent = Open(fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Cancel = UICancelPolicy.Close
                }));
            var child = Open(fixture.Host.TryPresent(
                confirmation,
                UIScreenHostTestSupport.Spec(UIScreenKinds.ConfirmOverwrite) with
                {
                    Parent = parent,
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    Cancel = UICancelPolicy.Close,
                    InitialFocus = () => safeButton
                }));

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(confirmation.GuiGetFocusOwner()).IsEqual(safeButton);

            var cancel = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(cancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(child)).IsFalse();
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            AssertThat(destructiveCount).IsEqual(0);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(parent);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task RewardToast_IsPassiveAndNeverBecomesInputOwner()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var modalView = fixture.Track(new Control { Visible = true });
        var toastView = fixture.Track(new Control { Visible = true });
        try
        {
            var modal = Open(fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    BlockGameplayInput = true,
                    Cancel = UICancelPolicy.Consume
                }));
            var stateBeforeToast = fixture.Host.CurrentState;

            var toast = Open(fixture.Host.TryPresent(
                toastView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
                {
                    Layer = UIScreenLayer.Toast,
                    InputPriority = UIInputPriority.Passive
                }));

            AssertThat(fixture.Host.IsActive(toast)).IsTrue();
            AssertThat(fixture.Host.CurrentState).IsEqual(stateBeforeToast);
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(modal);
            AssertThat(fixture.Host.Diagnostics.ActionOwnership.TopInputOwner).IsEqual(modal);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task RequiredAcknowledgement_ConsumesCancelUntilContinue()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { UiCancelAction });
        var acknowledgementView = fixture.Track(new Control { Visible = true });
        var continueButton = new Button { Name = "Continue" };
        acknowledgementView.AddChild(continueButton);
        try
        {
            UIScreenHandle? acknowledgement = null;
            UIScreenCloseResult? continueCloseResult = null;
            continueButton.Pressed += () =>
            {
                if (acknowledgement.HasValue)
                {
                    continueCloseResult = fixture.Host.TryClose(
                        acknowledgement.Value,
                        UIScreenCloseReason.ExplicitAction);
                }
            };
            acknowledgement = Open(fixture.Host.TryPresent(
                acknowledgementView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardAcknowledgement) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    BlockGameplayInput = true,
                    Cancel = UICancelPolicy.Consume,
                    InitialFocus = () => continueButton
                }));

            var cancel = fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction));

            AssertThat(cancel).IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(acknowledgement.Value)).IsTrue();

            continueButton.EmitSignal(BaseButton.SignalName.Pressed);

            AssertThat(continueCloseResult?.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(fixture.Host.IsActive(acknowledgement.Value)).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task BattlePresentation_RemainsTopmostAfterDomainFlagClears()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { UiCancelAction });
        var battleView = fixture.Track(new Control { Visible = true });
        var domainBattleActive = true;
        UIScreenCloseReason? closeReason = null;
        try
        {
            var battle = Open(fixture.Host.TryPresent(
                battleView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    BlockGameplayInput = true,
                    Cancel = UICancelPolicy.Consume,
                    Cleanup = reason => closeReason = reason
                }));

            domainBattleActive = false;

            AssertThat(domainBattleActive).IsFalse();
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(battle);
            AssertThat(fixture.Host.TryHandleInput(
                UIScreenHostTestSupport.ActionPress(UiCancelAction)))
                .IsEqual(UIInputDispatchResult.Consumed);
            AssertThat(fixture.Host.IsActive(battle)).IsTrue();

            battleView.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.IsActive(battle)).IsFalse();
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsNull();
            AssertThat(closeReason).IsEqual(UIScreenCloseReason.NodeFreed);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task EitherPresentationOrDomainBlock_SuppressesComposedPredicate()
    {
        var presentationBlocked = false;
        var domainBlocked = false;
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            options: new UIScreenHostOptions
            {
                GameplayInputBlockChanged = blocked => presentationBlocked = blocked
            });
        var blockingView = fixture.Track(new Control { Visible = true });
        bool IsGameplaySuppressed() => presentationBlocked || domainBlocked;
        try
        {
            AssertThat(IsGameplaySuppressed()).IsFalse();

            var blocking = Open(fixture.Host.TryPresent(
                blockingView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Dialogue) with
                {
                    BlockGameplayInput = true
                }));

            AssertThat(presentationBlocked).IsTrue();
            AssertThat(domainBlocked).IsFalse();
            AssertThat(IsGameplaySuppressed()).IsTrue();

            domainBlocked = true;
            fixture.Host.TryClose(blocking, UIScreenCloseReason.Programmatic);

            AssertThat(presentationBlocked).IsFalse();
            AssertThat(domainBlocked).IsTrue();
            AssertThat(IsGameplaySuppressed()).IsTrue();

            domainBlocked = false;

            AssertThat(presentationBlocked).IsFalse();
            AssertThat(domainBlocked).IsFalse();
            AssertThat(IsGameplaySuppressed()).IsFalse();
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

    private static UIScreenHandle Open(UIScreenOpenResult result)
    {
        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(result.Handle).IsNotNull();
        return result.Handle!.Value;
    }
}
