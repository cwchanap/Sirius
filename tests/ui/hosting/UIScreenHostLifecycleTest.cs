using System;
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
    public async Task LowerLayerEffects_NestedOwnersWeakenWithoutReplacingControlBaseline()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control { Visible = true });
        var pauseView = fixture.Track(new Control { Visible = true });
        var settingsView = fixture.Track(new Control { Visible = true });
        var interactivityChanges = new List<bool>();
        var pausePublicationSawCompleteEffects = false;
        try
        {
            tree.Paused = false;
            var gameplayResult = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        interactivityChanges.Add(enabled);
                        gameplay.SetProcessInput(enabled);
                    }
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplay.SetProcessInput(true);
            var shield = fixture.Host.GetNode<Control>("InputShield");

            fixture.Host.EffectiveStateChanged += state =>
            {
                if (!state.IsTreePauseOwned)
                    return;

                pausePublicationSawCompleteEffects =
                    !gameplay.IsProcessingInput() &&
                    shield.Visible;
            };

            var pause = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    PauseTree = true,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;

            AssertThat(tree.Paused).IsTrue();
            AssertThat(gameplay.Visible).IsTrue();
            AssertThat(gameplay.IsProcessingInput()).IsFalse();
            AssertThat(shield.Visible).IsTrue();
            AssertThat(shield.GetParent()).IsEqual(gameplay.GetParent());
            AssertThat(shield.GetIndex()).IsEqual(gameplay.GetIndex() + 1);
            AssertThat(shield.ProcessMode).IsEqual(ProcessModeEnum.Always);
            AssertThat(pausePublicationSawCompleteEffects).IsTrue();

            var settings = fixture.Host.TryPresent(
                settingsView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = pause,
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                }).Handle!.Value;

            AssertThat(tree.Paused).IsTrue();
            AssertThat(gameplay.Visible).IsFalse();
            AssertThat(gameplay.IsProcessingInput()).IsFalse();
            AssertThat(pauseView.Visible).IsFalse();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(fixture.Host);
            AssertThat(shield.ProcessMode).IsEqual(ProcessModeEnum.Inherit);

            fixture.Host.TryClose(settings, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsTrue();
            AssertThat(gameplay.Visible).IsTrue();
            AssertThat(gameplay.IsProcessingInput()).IsFalse();
            AssertThat(pauseView.Visible).IsTrue();
            AssertThat(shield.Visible).IsTrue();
            AssertThat(shield.GetParent()).IsEqual(gameplay.GetParent());
            AssertThat(shield.GetIndex()).IsEqual(gameplay.GetIndex() + 1);
            AssertThat(shield.ProcessMode).IsEqual(ProcessModeEnum.Always);

            fixture.Host.TryClose(pause, UIScreenCloseReason.Programmatic);

            AssertThat(tree.Paused).IsFalse();
            AssertThat(gameplay.Visible).IsTrue();
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(fixture.Host);
            AssertThat(shield.ProcessMode).IsEqual(ProcessModeEnum.Inherit);
            AssertThat(interactivityChanges.ToArray())
                .ContainsExactly(false, true);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_InertOwnerWithoutRequiredControlAdapterIsRejectedAtomically()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control { Visible = true });
        var pauseView = fixture.Track(new Control());
        try
        {
            var gameplayResult = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplay.SetProcessInput(true);

            var result = fixture.Host.TryPresent(
                pauseView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(pauseView.GetParent()).IsNull();
            AssertThat(gameplay.Visible).IsTrue();
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(fixture.Host.GetNode<Control>("InputShield").Visible).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_UnattachedControlBelowExistingOwnerRequiresAdapterBeforeReady()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var hiddenOwner = fixture.Track(new Control());
        var candidate = fixture.Track(new EnablesInputOnReadyControl());
        try
        {
            var owner = fixture.Host.TryPresent(
                hiddenOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(owner.Status).IsEqual(UIScreenOpenStatus.Opened);

            var result = fixture.Host.TryPresent(
                candidate,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(candidate.GetParent()).IsNull();
            AssertThat(candidate.WasReady).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_OwnerOpenedFromTargetReadyPublishesCompleteEffects()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var ownerView = fixture.Track(new Control());
        var target = fixture.Track(new OpensOwnerOnReadyControl
        {
            Host = fixture.Host,
            OwnerView = ownerView
        });
        var ownerPublicationSawCompleteEffects = false;
        var shield = fixture.Host.GetNode<Control>("InputShield");
        fixture.Host.EffectiveStateChanged += state =>
        {
            if (state.TopInputOwner?.Kind != UIScreenKinds.Pause)
                return;

            ownerPublicationSawCompleteEffects =
                !target.IsProcessingInput() && shield.Visible;
        };
        try
        {
            var result = fixture.Host.TryPresent(
                target,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = target.SetProcessInput
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(target.OwnerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);
            AssertThat(target.IsProcessingInput()).IsFalse();
            AssertThat(shield.Visible).IsTrue();
            AssertThat(ownerPublicationSawCompleteEffects).IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_FreeingShieldOwnerDefersShieldPlacementUntilAfterTreeExit()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var ownerView = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var shield = fixture.Host.GetNode<Control>("InputShield");
        try
        {
            var owner = fixture.Host.TryPresent(
                ownerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = ownerView.SetProcessInput
                }).Handle!.Value;
            var modal = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    InputPriority = UIInputPriority.Blocking,
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;
            ownerView.SetProcessInput(true);

            AssertThat(shield.Visible).IsTrue();
            AssertThat(shield.GetParent()).IsEqual(ownerView.GetParent());

            ownerView.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.IsActive(owner)).IsFalse();
            AssertThat(fixture.Host.IsActive(modal)).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(fixture.Host);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_OwnerOpenedFromTargetReadyRejectsMissingAdapterAtomically()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var ownerView = fixture.Track(new Control());
        var target = fixture.Track(new OpensOwnerOnReadyControl
        {
            Host = fixture.Host,
            OwnerView = ownerView
        });
        try
        {
            var result = fixture.Host.TryPresent(
                target,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(target.OwnerResult.Status)
                .IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(ownerView.GetParent()).IsNull();
            AssertThat(target.IsProcessingInput()).IsTrue();
            AssertThat(fixture.Host.GetNode<Control>("InputShield").Visible).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

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
        var incomingPaused = tree.Paused;
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
            tree.Paused = incomingPaused;
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
        var incomingMouseMode = Input.MouseMode;
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        try
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
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
            Input.MouseMode = incomingMouseMode;
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
        var originalMouseMode = Input.MouseMode;
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
            Input.MouseMode = originalMouseMode;
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
    public async Task SceneOwnerPrepareForTeardown_PreservesExternalWindowAcrossSceneDeletion()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var sceneOwner = new Node();
        tree.Root.AddChild(sceneOwner);
        var fixture = await UIScreenHostTestSupport.CreateHost(sceneOwner);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window { Visible = true });
        var cleanupReasons = new List<UIScreenCloseReason>();
        try
        {
            var opened = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Cleanup = cleanupReasons.Add
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            fixture.Host.PrepareForTeardown();

            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(cleanupReasons.ToArray()).ContainsExactly(
                UIScreenCloseReason.HostTeardown);
            AssertThat(window.GetParent()).IsNull();

            sceneOwner.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(GodotObject.IsInstanceValid(window)).IsTrue();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(fixture.Host))
                fixture.Host.PrepareForTeardown();
            if (GodotObject.IsInstanceValid(sceneOwner) && !sceneOwner.IsQueuedForDeletion())
                sceneOwner.QueueFree();
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PrepareForTeardown_DetachesPreParentedExternalControl()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var control = fixture.Track(new Control { Visible = true });
        var screenLayer = fixture.Host.GetNode<Control>("ScreenLayer");
        screenLayer.AddChild(control);
        try
        {
            var opened = fixture.Host.TryPresent(
                control,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory));
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            fixture.Host.PrepareForTeardown();

            AssertThat(GodotObject.IsInstanceValid(control)).IsTrue();
            AssertThat(control.GetParent()).IsNull();
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
        var originalMouseMode = Input.MouseMode;
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
            Input.MouseMode = originalMouseMode;
        }
    }

    [TestCase]
    public async Task FixtureDispose_RestoresGlobalsAfterActiveLeaseTeardownCompletes()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var incomingPaused = tree.Paused;
        var externalHud = new Control { Visible = true };
        HostFixture? fixture = null;
        try
        {
            tree.Paused = true;
            fixture = await UIScreenHostTestSupport.CreateHost(
                this,
                options: new UIScreenHostOptions { HudRoot = externalHud });
            var view = fixture.Track(new Control());
            tree.Paused = false;
            externalHud.Visible = false;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true,
                    Hud = UIHudPolicy.Visible
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(tree.Paused).IsTrue();
            AssertThat(externalHud.Visible).IsTrue();

            fixture.Dispose();
            fixture = null;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(tree.Paused).IsTrue();
            AssertThat(externalHud.Visible).IsTrue();
        }
        finally
        {
            fixture?.Dispose();
            tree.Paused = incomingPaused;
            externalHud.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task ExternalParentFree_ClosesDescendantsOnceAndRestoresNextOwner()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control());
        var gameplayFocus = new Button { FocusMode = Control.FocusModeEnum.All };
        gameplay.AddChild(gameplayFocus);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        var cleanup = new List<string>();
        try
        {
            var gameplayHandle = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    InitialFocus = () => gameplayFocus
                }).Handle!.Value;
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    Cleanup = reason => cleanup.Add($"parent:{reason}")
                }).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Layer = UIScreenLayer.Modal,
                    Cleanup = reason => cleanup.Add($"child:{reason}")
                }).Handle!.Value;

            parentView.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
            AssertThat(fixture.Host.IsActive(child)).IsFalse();
            AssertThat(fixture.Host.IsActive(gameplayHandle)).IsTrue();
            AssertThat(cleanup.ToArray()).ContainsExactly(
                "child:ParentClosed",
                "parent:NodeFreed");
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsEqual(gameplayHandle);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(gameplayFocus);

            fixture.Host.TryHandleInput(new InputEventAction
            {
                Action = "unmatched_after_external_free",
                Pressed = true
            });
            AssertThat(cleanup.Count).IsEqual(2);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Diagnostics_ExposeCompleteCopiedReadOnlyHostState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var coreAction = new StringName("diagnostics_core_cancel");
        var entryAction = new StringName("diagnostics_entry_cancel");
        var fixture = await UIScreenHostTestSupport.CreateHost(
            this,
            new[] { coreAction });
        fixture.Viewport.GuiEmbedSubwindows = true;
        var control = fixture.Track(new Control
        {
            Visible = true,
            ProcessMode = ProcessModeEnum.Pausable
        });
        control.SetProcessInput(true);
        var parentWindow = fixture.Track(new Window
        {
            Visible = true,
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        var childWindow = fixture.Track(new Window { Visible = true });
        try
        {
            tree.Paused = false;
            fixture.HudRoot.Visible = true;
            var controlHandle = fixture.Host.TryPresent(
                control,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    ProcessPolicy = UIProcessPolicy.Always,
                    SetInteractive = control.SetProcessInput
                }).Handle!.Value;
            var parentHandle = fixture.Host.TryPresent(
                parentWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    ProcessPolicy = UIProcessPolicy.Always
                }).Handle!.Value;
            var childHandle = fixture.Host.TryPresent(
                childWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Parent = parentHandle,
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    PauseTree = true,
                    Cursor = UICursorPolicy.Hidden,
                    Hud = UIHudPolicy.Hidden,
                    LowerLayers = UILowerLayerPolicy.Hidden,
                    EntryCancelActions = new HashSet<StringName> { entryAction }
                }).Handle!.Value;
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            tree.Paused = false;
            fixture.Host._Process(0);
            var diagnostics = fixture.Host.Diagnostics;

            AssertThat(diagnostics.ActiveEntries.Count).IsEqual(3);
            AssertThat(diagnostics.ActiveEntries[0].Handle).IsEqual(childHandle);
            AssertThat(diagnostics.ActiveEntries[1].Handle).IsEqual(parentHandle);
            AssertThat(diagnostics.ActiveEntries[2].Handle).IsEqual(controlHandle);
            AssertThat(diagnostics.ActiveEntries[0].Policy.EntryCancelActions.Contains(entryAction))
                .IsTrue();
            AssertThat(diagnostics.EffectiveState.TopInputOwner).IsEqual(childHandle);

            AssertThat(diagnostics.LowerLayerEffects.Count).IsEqual(3);
            AssertThat(diagnostics.LowerLayerEffects[2].Target).IsEqual(controlHandle);
            AssertThat(diagnostics.LowerLayerEffects[2].ReducedEffect)
                .IsEqual(UILowerLayerPolicy.Hidden);
            AssertThat(diagnostics.LowerLayerEffects[2].Contributors)
                .ContainsExactly(childHandle);

            AssertThat(diagnostics.ActionOwnership.CoreActions.Contains(coreAction)).IsTrue();
            AssertThat(diagnostics.ActionOwnership.TopInputOwner).IsEqual(childHandle);
            AssertThat(diagnostics.ActionOwnership.EntryActions[childHandle].Contains(entryAction))
                .IsTrue();

            AssertThat(diagnostics.FocusStates.Count).IsEqual(3);
            AssertThat(diagnostics.FocusStates[0].Handle).IsEqual(childHandle);
            AssertThat(diagnostics.FocusStates[0].ViewportInstanceId ==
                       childWindow.GetInstanceId()).IsTrue();
            AssertThat(diagnostics.FocusStates[0].SinkInstanceId.HasValue).IsTrue();
            AssertThat(diagnostics.FocusStates[0].IsSinkFocused).IsTrue();
            AssertThat(diagnostics.RestorationLease).IsNull();

            AssertThat(diagnostics.ProcessStates.Count).IsEqual(3);
            AssertThat(diagnostics.ProcessStates[1].Handle).IsEqual(parentHandle);
            AssertThat(diagnostics.ProcessStates[1].IncomingMode)
                .IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(diagnostics.ProcessStates[1].RegisteredMode)
                .IsEqual(ProcessModeEnum.Always);
            AssertThat(diagnostics.ProcessStates[1].CurrentMode)
                .IsEqual(ProcessModeEnum.Always);
            AssertThat(diagnostics.ProcessStates[1].IsEmbeddedSubwindow).IsTrue();
            AssertThat(diagnostics.SubwindowEmbeddingEnabled).IsTrue();

            AssertThat(diagnostics.StateLeases.IncomingPaused == false).IsTrue();
            AssertThat(diagnostics.StateLeases.IncomingCursorMode.HasValue).IsTrue();
            AssertThat(diagnostics.StateLeases.IncomingHudVisible == true).IsTrue();
            AssertThat(diagnostics.StateLeases.ControlEffects[controlHandle].Visible).IsTrue();
            AssertThat(diagnostics.StateLeases.ControlEffects[controlHandle].ProcessInputEnabled)
                .IsTrue();
            AssertThat(diagnostics.StateLeases.WindowEffects[parentHandle].Visible).IsTrue();
            AssertThat(diagnostics.StateLeases.WindowEffects[parentHandle].GuiDisableInput)
                .IsFalse();
            AssertThat(diagnostics.StateLeases.WindowEffects[parentHandle].Unfocusable)
                .IsFalse();
            AssertThat(diagnostics.PauseOwnershipDriftCount).IsEqual(1);
            AssertThat(diagnostics.LastPauseOwnershipViolation)
                .IsEqual("TreeUnpausedWhilePauseLeaseActive");

            fixture.Host.TryClose(childHandle, UIScreenCloseReason.Programmatic);
            AssertThat(diagnostics.ActiveEntries.Count).IsEqual(3);
            AssertThat(diagnostics.LowerLayerEffects.Count).IsEqual(3);
            AssertThat(diagnostics.ProcessStates.Count).IsEqual(3);
            AssertThrown(() => ((IList<UIScreenEntrySnapshot>)diagnostics.ActiveEntries)
                .Add(diagnostics.ActiveEntries[0]));
            AssertThrown(() => ((IDictionary<UIScreenHandle, IReadOnlySet<StringName>>)
                diagnostics.ActionOwnership.EntryActions)
                .Add(childHandle, EmptyStringNameSet.Value));
            AssertThrown(() => ((IDictionary<UIScreenHandle, UIControlEffectLeaseDiagnostics>)
                diagnostics.StateLeases.ControlEffects)
                .Add(controlHandle, diagnostics.StateLeases.ControlEffects[controlHandle]));
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ActiveControlAndWindowEffects_RestoreWhenTargetsClose()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var control = fixture.Track(new Control { Visible = true });
        control.SetProcessInput(true);
        var window = fixture.Track(new AcceptDialog
        {
            GuiDisableInput = true,
            Unfocusable = false
        });
        var owner = fixture.Track(new Control());
        try
        {
            var controlHandle = fixture.Host.TryPresent(
                control,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = control.SetProcessInput
                }).Handle!.Value;
            var windowResult = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Layer = UIScreenLayer.Screen
                });
            AssertThat(windowResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            var windowHandle = windowResult.Handle!.Value;
            window.Show();
            var shield = fixture.Host.GetNode<Control>("InputShield");
            var ownerResult = fixture.Host.TryPresent(
                owner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(ownerResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(control.IsProcessingInput()).IsFalse();
            AssertThat(shield.Visible).IsTrue();
            AssertThat(shield.GetParent()).IsEqual(control.GetParent());
            AssertThat(shield.GetIndex()).IsEqual(control.GetIndex() + 1);
            AssertThat(window.GuiDisableInput).IsTrue();
            AssertThat(window.Unfocusable).IsTrue();

            fixture.Host.TryClose(controlHandle, UIScreenCloseReason.Programmatic);

            AssertThat(control.IsProcessingInput()).IsTrue();
            AssertThat(control.Visible).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(fixture.Host);

            fixture.Host.TryClose(windowHandle, UIScreenCloseReason.Programmatic);

            AssertThat(window.Visible).IsTrue();
            AssertThat(window.GuiDisableInput).IsTrue();
            AssertThat(window.Unfocusable).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HostTeardown_RestoresActiveControlWindowAndShieldBaselines()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var control = fixture.Track(new Control { Visible = true });
        control.SetProcessInput(true);
        var window = fixture.Track(new AcceptDialog
        {
            GuiDisableInput = false,
            Unfocusable = false,
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        var owner = fixture.Track(new Control());
        try
        {
            fixture.Host.TryPresent(
                control,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = control.SetProcessInput
                });
            var windowResult = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Layer = UIScreenLayer.Screen,
                    ProcessPolicy = UIProcessPolicy.Always
                });
            AssertThat(windowResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            window.Show();
            var shield = fixture.Host.GetNode<Control>("InputShield");
            var shieldParent = shield.GetParent();
            var shieldIndex = shield.GetIndex();
            var shieldProcessMode = shield.ProcessMode;
            var ownerResult = fixture.Host.TryPresent(
                owner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(ownerResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(control.IsProcessingInput()).IsFalse();
            AssertThat(shield.Visible).IsTrue();
            AssertThat(window.GuiDisableInput).IsTrue();
            AssertThat(window.Unfocusable).IsTrue();

            fixture.Host.PrepareForTeardown();

            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(shieldParent);
            AssertThat(shield.GetIndex()).IsEqual(shieldIndex);
            AssertThat(shield.ProcessMode).IsEqual(shieldProcessMode);

            fixture.Host.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(control.IsProcessingInput()).IsTrue();
            AssertThat(control.Visible).IsTrue();
            AssertThat(control.GetParent()).IsNull();
            AssertThat(window.Visible).IsTrue();
            AssertThat(window.GuiDisableInput).IsFalse();
            AssertThat(window.Unfocusable).IsFalse();
            AssertThat(window.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(window.GetParent()).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HostTeardown_ReentrantFromCleanupFinishesCurrentDrainBeforeParentClose()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control());
        var childView = fixture.Track(new Control());
        var cleanup = new List<string>();
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cleanup = reason => cleanup.Add($"parent:{reason}")
                }).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Cleanup = reason =>
                    {
                        cleanup.Add($"child:{reason}");
                        fixture.Host.QueueFree();
                    }
                }).Handle!.Value;

            var result = fixture.Host.TryClose(child, UIScreenCloseReason.Programmatic);

            AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(cleanup.ToArray()).ContainsExactly(
                "child:Programmatic",
                "parent:HostTeardown");
            AssertThat(parentView.GetParent()).IsNull();
            AssertThat(childView.GetParent()).IsNull();
            AssertThat(GodotObject.IsInstanceValid(fixture.Host)).IsTrue();
            AssertThat(fixture.Host.IsQueuedForDeletion()).IsFalse();
            AssertThat(fixture.Host.PrepareForTeardown()).IsEqual(
                UIScreenTeardownPreparationStatus.Complete);

            fixture.Host.QueueFree();
            AssertThat(fixture.Host.IsQueuedForDeletion()).IsTrue();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(GodotObject.IsInstanceValid(fixture.Host)).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Close_CleanupReentrantPresentRejectsHostMutatingUntilRetry()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var closingView = fixture.Track(new Control());
        var candidateView = fixture.Track(new Control());
        UIScreenOpenResult reentrant = default;
        var publicationDuringCleanup = false;
        var inCleanup = false;
        fixture.Host.EffectiveStateChanged += _ =>
        {
            if (inCleanup)
                publicationDuringCleanup = true;
        };
        try
        {
            var opened = fixture.Host.TryPresent(
                closingView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Cleanup = _ =>
                    {
                        inCleanup = true;
                        reentrant = fixture.Host.TryPresent(
                            candidateView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));
                        inCleanup = false;
                    }
                }).Handle!.Value;

            var closed = fixture.Host.TryClose(opened, UIScreenCloseReason.Programmatic);

            AssertThat(closed.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(reentrant.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(reentrant.Handle).IsNull();
            AssertThat(publicationDuringCleanup).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(candidateView.GetParent()).IsNull();

            var retry = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));
            AssertThat(retry.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PrepareForTeardown_ReentrantCleanupReportsDeferredThenCompleteBeforeOwnerDeletion()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var sceneOwner = new Node();
        tree.Root.AddChild(sceneOwner);
        var fixture = await UIScreenHostTestSupport.CreateHost(sceneOwner);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window { Visible = true });
        var childView = fixture.Track(new Control());
        UIScreenTeardownPreparationStatus? duringCleanup = null;
        try
        {
            var parent = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad)).Handle!.Value;
            var child = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent,
                    Cleanup = _ =>
                        duringCleanup = fixture.Host.PrepareForTeardown()
                }).Handle!.Value;

            var closeResult = fixture.Host.TryClose(
                child,
                UIScreenCloseReason.Programmatic);

            AssertThat(closeResult.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(duringCleanup).IsEqual(
                UIScreenTeardownPreparationStatus.Deferred);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.PrepareForTeardown()).IsEqual(
                UIScreenTeardownPreparationStatus.Complete);
            AssertThat(window.GetParent()).IsNull();

            sceneOwner.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(GodotObject.IsInstanceValid(window)).IsTrue();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(fixture.Host))
                fixture.Host.PrepareForTeardown();
            if (GodotObject.IsInstanceValid(sceneOwner) && !sceneOwner.IsQueuedForDeletion())
                sceneOwner.QueueFree();
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task PrepareForTeardown_FinalizationCallbackDefersAndFailedAttemptMustRetry()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var incomingPaused = tree.Paused;
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var view = fixture.Track(new Control());
        UIScreenTeardownPreparationStatus? duringFinalization = null;
        var sawPending = false;
        var throwOnce = true;
        string? thrownMessage = null;
        try
        {
            tree.Paused = false;
            var opened = fixture.Host.TryPresent(
                view,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    PauseTree = true
                });
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(tree.Paused).IsTrue();

            fixture.Host.EffectiveStateChanged += state =>
            {
                if (state.IsFocusRestorationPending)
                {
                    sawPending = true;
                    return;
                }

                if (!sawPending || !throwOnce)
                    return;

                throwOnce = false;
                duringFinalization = fixture.Host.PrepareForTeardown();
                throw new InvalidOperationException("expected finalization callback failure");
            };

            try
            {
                fixture.Host.PrepareForTeardown();
            }
            catch (InvalidOperationException exception)
            {
                thrownMessage = exception.Message;
            }

            AssertThat(sawPending).IsTrue();
            AssertThat(thrownMessage).IsEqual("expected finalization callback failure");
            AssertThat(duringFinalization).IsEqual(
                UIScreenTeardownPreparationStatus.Deferred);

            AssertThat(fixture.Host.PrepareForTeardown()).IsEqual(
                UIScreenTeardownPreparationStatus.Complete);
            AssertThat(tree.Paused).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
            tree.Paused = incomingPaused;
        }
    }

    [TestCase]
    public async Task HostTeardown_DetachesPreParentedExternalWindowBeforeDeletion()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window { Visible = true });
        fixture.Host.AddChild(window);
        try
        {
            var opened = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad));
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);

            fixture.Host.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(GodotObject.IsInstanceValid(window)).IsTrue();
            AssertThat(window.GetParent()).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task HostTeardown_ClosesTopmostFirstRejectsReopenAndRemovesWindowSink()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var parentView = fixture.Track(new Control
        {
            ProcessMode = ProcessModeEnum.Pausable
        });
        var childWindow = fixture.Track(new Window
        {
            Visible = true,
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        var rejectedView = fixture.Track(new Control());
        var cleanup = new List<string>();
        UIScreenOpenStatus? reopenStatus = null;
        try
        {
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    ProcessPolicy = UIProcessPolicy.Always,
                    Cleanup = reason => cleanup.Add($"parent:{reason}")
                }).Handle!.Value;
            fixture.Host.TryPresent(
                childWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Parent = parent,
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    ProcessPolicy = UIProcessPolicy.Always,
                    Cleanup = reason =>
                    {
                        cleanup.Add($"child:{reason}");
                        reopenStatus = fixture.Host.TryPresent(
                            rejectedView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.Settings)).Status;
                    }
                });
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(childWindow.GuiGetFocusOwner()).IsNotNull();
            fixture.Host.QueueFree();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(cleanup.ToArray()).ContainsExactly(
                "child:HostTeardown",
                "parent:HostTeardown");
            AssertThat(reopenStatus).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(parentView.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
            AssertThat(childWindow.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
            AssertThat(parentView.GetParent()).IsNull();
            AssertThat(childWindow.GetParent()).IsNull();
            AssertThat(childWindow.GetNodeOrNull<Control>("__UIScreenFocusSink")).IsNull();
            AssertThat(GodotObject.IsInstanceValid(rejectedView)).IsTrue();
            AssertThat(rejectedView.GetParent()).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Recompute_SetInteractiveClosesEffectOwner_RestartsAndLeavesConsistentState()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var shield = fixture.Host.GetNode<Control>("InputShield");
        var publishedStates = new List<UIScreenEffectiveState>();
        fixture.Host.EffectiveStateChanged += state => publishedStates.Add(state);
        try
        {
            var gameplayResult = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        gameplay.SetProcessInput(enabled);
                        if (enabled)
                            return;

                        // Re-entrant close of the modal that just inerted this
                        // owner, fired from the SetInteractive callback while
                        // Recompute is still applying lower-layer effects.
                        foreach (var entry in fixture.Host.ActiveEntries)
                        {
                            if (entry.Policy.Kind == UIScreenKinds.Pause)
                            {
                                fixture.Host.TryClose(
                                    entry.Handle,
                                    UIScreenCloseReason.Programmatic);
                                break;
                            }
                        }
                    }
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplay.SetProcessInput(true);

            var modalResult = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            var modal = modalResult.Handle!.Value;

            // The re-entrant close must have closed the modal during its own open.
            AssertThat(fixture.Host.IsActive(modal)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The outer pass must have restarted from the current model: gameplay
            // is the sole owner, restored to interactive, shield withdrawn.
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(shield.GetParent()).IsEqual(fixture.Host);
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(gameplayResult.Handle);
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked)
                .IsEqual(false);

            // No published state may name the closed modal as top input owner.
            foreach (var state in publishedStates)
                AssertThat(state.TopInputOwner?.Kind == UIScreenKinds.Pause)
                    .IsEqual(false);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Recompute_SubscriberClosingTopOwner_RestartsAndStaysConsistent()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var shield = fixture.Host.GetNode<Control>("InputShield");
        var observations = new List<UIScreenEffectiveState>();
        var firstSubscriberClosed = false;

        // Earlier subscriber: closes the modal the first time it is named the
        // top input owner, mutating the host during publication.
        fixture.Host.EffectiveStateChanged += _ =>
        {
            if (firstSubscriberClosed)
                return;
            foreach (var entry in fixture.Host.ActiveEntries)
            {
                if (entry.Policy.Kind == UIScreenKinds.Pause)
                {
                    firstSubscriberClosed = true;
                    fixture.Host.TryClose(
                        entry.Handle,
                        UIScreenCloseReason.Programmatic);
                    break;
                }
            }
        };

        // Later subscriber: records every state it observes.
        fixture.Host.EffectiveStateChanged += state => observations.Add(state);
        try
        {
            var gameplayResult = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = gameplay.SetProcessInput
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplay.SetProcessInput(true);

            var modalResult = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            var modal = modalResult.Handle!.Value;

            AssertThat(fixture.Host.IsActive(modal)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(gameplayResult.Handle);

            // The later subscriber's final observation must agree with the model
            // (gameplay as top owner), not the stale snapshot naming the modal.
            var final = observations[observations.Count - 1];
            AssertThat(final.TopInputOwner).IsEqual(gameplayResult.Handle);
            AssertThat(final.TopInputOwner?.Kind == UIScreenKinds.Pause)
                .IsEqual(false);

            // No observation the later subscriber received may name the closed
            // modal as top input owner. An earlier subscriber closing the modal
            // during the same multicast must abort the remaining invocation list
            // so later subscribers never see a stale state.
            foreach (var observation in observations)
            {
                AssertThat(observation.TopInputOwner?.Kind == UIScreenKinds.Pause)
                    .IsEqual(false);
            }
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Recompute_GameplayInputBlockChangedClosingTopOwner_AbortsBeforeEffectiveStateChanged()
    {
        Action<bool>? blockCallback = null;
        var fixture = await UIScreenHostTestSupport.CreateHost(this, options:
            new UIScreenHostOptions
            {
                GameplayInputBlockChanged = blocked => blockCallback?.Invoke(blocked)
            });
        var gameplay = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var publishedStates = new List<UIScreenEffectiveState>();
        fixture.Host.EffectiveStateChanged += state => publishedStates.Add(state);
        var blockCallbackClosed = false;

        blockCallback = _ =>
        {
            if (blockCallbackClosed)
                return;
            foreach (var entry in fixture.Host.ActiveEntries)
            {
                if (entry.Policy.Kind == UIScreenKinds.Pause)
                {
                    blockCallbackClosed = true;
                    fixture.Host.TryClose(
                        entry.Handle,
                        UIScreenCloseReason.Programmatic);
                    break;
                }
            }
        };
        try
        {
            var gameplayResult = fixture.Host.TryPresent(
                gameplay,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = gameplay.SetProcessInput
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplay.SetProcessInput(true);

            var modalResult = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true
                });
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            var modal = modalResult.Handle!.Value;

            // The re-entrant close from GameplayInputBlockChanged must have
            // closed the modal during its own open.
            AssertThat(fixture.Host.IsActive(modal)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // No published EffectiveStateChanged observation may name the
            // closed modal as top input owner. The generation check after
            // GameplayInputBlockChanged must abort before EffectiveStateChanged
            // subscribers receive a stale snapshot.
            foreach (var state in publishedStates)
            {
                AssertThat(state.TopInputOwner?.Kind == UIScreenKinds.Pause)
                    .IsEqual(false);
            }

            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(gameplayResult.Handle);
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked)
                .IsEqual(false);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyControlEffect_SetInteractiveClosesOwnTargetUnderHiddenOwner_BaselineRestored()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? lowerHandle = null;
        try
        {
            var lowerResult = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        lowerView.SetProcessInput(enabled);
                        if (enabled || !lowerHandle.HasValue)
                            return;
                        // Re-entrant close of self from the SetInteractive(false)
                        // callback while ApplyControlEffect is applying Hidden.
                        fixture.Host.TryClose(
                            lowerHandle.Value,
                            UIScreenCloseReason.Programmatic);
                    }
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerHandle = lowerResult.Handle;
            lowerView.SetProcessInput(true);

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The lower entry must have been closed by the re-entrant callback.
            AssertThat(fixture.Host.IsActive(lowerHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The lower view's baseline visibility and interactivity must be
            // restored. ApplyControlEffect must have aborted before setting
            // control.Visible = false or re-adding the stale effect.
            AssertThat(lowerView.Visible).IsTrue();
            AssertThat(lowerView.IsProcessingInput()).IsTrue();

            // No stale effect bookkeeping should remain for the closed handle.
            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.ControlEffects.ContainsKey(lowerHandle.Value))
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyControlEffect_SetInteractiveClosesUnrelatedEntry_TargetStillHidden()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var lowerView = fixture.Track(new Control { Visible = true });
        var unrelatedView = fixture.Track(new Control { Visible = true });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? unrelatedHandle = null;
        var closedUnrelated = false;
        try
        {
            // Unrelated entry opened first so the SetInteractive callback can
            // close it re-entrantly while ApplyControlEffect applies Hidden to
            // the lower target. It sits at Modal with the default
            // VisibleInteractive lower-layer policy so it does not contribute
            // to the lower target's reduced effect.
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
                        // ApplyControlEffect is applying Hidden to this target.
                        // The target itself stays active; only the generation
                        // changes, so the provisional effect must not be
                        // mistaken for a committed one on Recompute restart.
                        closedUnrelated = true;
                        fixture.Host.TryClose(
                            unrelatedHandle.Value,
                            UIScreenCloseReason.Programmatic);
                    }
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerView.SetProcessInput(true);

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The unrelated entry must have been closed by the re-entrant
            // callback, and the lower target must remain active.
            AssertThat(closedUnrelated).IsTrue();
            AssertThat(fixture.Host.IsActive(unrelatedHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.IsActive(lowerResult.Handle!.Value)).IsTrue();

            // The lower target's effective policy is Hidden (the upper modal
            // still inerts it). Recompute must have reapplied the effect rather
            // than skipping the provisional marker, so the view is hidden.
            AssertThat(lowerView.Visible).IsFalse();
            AssertThat(lowerView.IsProcessingInput()).IsFalse();

            // The committed effect bookkeeping must reflect the applied Hidden
            // effect for the still-active target.
            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.ControlEffects.ContainsKey(lowerResult.Handle.Value))
                .IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyControlEffect_FocusExitedClosesLowerTarget_AbortsBeforeCommit()
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
        UIScreenHandle? lowerHandle = null;
        var focusExitedClosed = false;
        try
        {
            var lowerResult = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = lowerView.SetProcessInput,
                    NodeLifetime = UINodeLifetime.External
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerHandle = lowerResult.Handle;
            lowerView.SetProcessInput(true);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(lowerButton.HasFocus()).IsTrue();

            // FocusExited on the lower button closes the lower target. This
            // fires synchronously inside RevokeFocusWithin → ReleaseFocus
            // while ApplyControlEffect is applying Hidden. Without the
            // MutationGeneration check after RevokeFocusWithin, the stale
            // outer pass hides the already-restored/detached external view and
            // re-adds effect bookkeeping for the closed handle.
            lowerButton.FocusExited += () =>
            {
                if (focusExitedClosed || !lowerHandle.HasValue)
                    return;
                focusExitedClosed = true;
                fixture.Host.TryClose(
                    lowerHandle.Value,
                    UIScreenCloseReason.Programmatic);
            };

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(focusExitedClosed).IsTrue();
            AssertThat(fixture.Host.IsActive(lowerHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The lower view was restored by CloseAdapter's
            // RestoreLowerLayerEffect (Visible = baseline true) and detached
            // (External lifetime). The stale outer pass must not re-hide it.
            AssertThat(lowerView.Visible).IsTrue();
            AssertThat(lowerView.GetParent()).IsNull();

            // No stale effect bookkeeping should remain for the closed handle.
            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.ControlEffects.ContainsKey(lowerHandle.Value))
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyControlEffect_FocusEnteredClosesLowerTarget_AbortsBeforeCommit()
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
        UIScreenHandle? lowerHandle = null;
        var focusEnteredClosed = false;
        try
        {
            var lowerResult = fixture.Host.TryPresent(
                lowerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = lowerView.SetProcessInput,
                    NodeLifetime = UINodeLifetime.External
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerHandle = lowerResult.Handle;
            lowerView.SetProcessInput(true);
            lowerButton.GrabFocus();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            AssertThat(lowerButton.HasFocus()).IsTrue();

            // FocusEntered on the upper button closes the lower target. The
            // upper owner is the top interactive entry (highest sequence,
            // VisibleInteractive). RevokeFocusWithin redirects focus from
            // lowerButton to upperButton via GrabFocus, which synchronously
            // fires FocusEntered. Without the MutationGeneration check after
            // RevokeFocusWithin, the stale outer pass commits the effect for
            // the closed handle.
            upperButton.FocusEntered += () =>
            {
                if (focusEnteredClosed || !lowerHandle.HasValue)
                    return;
                focusEnteredClosed = true;
                fixture.Host.TryClose(
                    lowerHandle.Value,
                    UIScreenCloseReason.Programmatic);
            };

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(focusEnteredClosed).IsTrue();
            AssertThat(fixture.Host.IsActive(lowerHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The lower view was restored by CloseAdapter's
            // RestoreLowerLayerEffect and detached (External lifetime). The
            // stale outer pass must not re-hide it.
            AssertThat(lowerView.Visible).IsTrue();
            AssertThat(lowerView.GetParent()).IsNull();

            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.ControlEffects.ContainsKey(lowerHandle.Value))
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyWindowEffect_SetPresentedClosesUnrelatedEntry_TargetStillInert()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window
        {
            Visible = true,
            GuiDisableInput = false,
            Unfocusable = false
        });
        var inertOwner = fixture.Track(new Control { Visible = true });
        var hiddenOwner = fixture.Track(new Control { Visible = true });
        var unrelatedView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? unrelatedHandle = null;
        var closedUnrelated = false;
        try
        {
            var windowResult = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetPresented = presented =>
                    {
                        window.Visible = presented;
                        // The SetPresented(true) callback fires during the
                        // Hidden → VisibleInert transition (the applied ==
                        // Hidden branch). Close an UNRELATED entry so the
                        // generation changes while the target stays active
                        // and the inert owner continues contributing
                        // VisibleInert. Without the provisional-marker
                        // rollback, Recompute skips the target (applied ==
                        // effect) and leaves the Window visible but still
                        // accepting input.
                        if (!presented || !unrelatedHandle.HasValue || closedUnrelated)
                            return;
                        closedUnrelated = true;
                        fixture.Host.TryClose(
                            unrelatedHandle.Value,
                            UIScreenCloseReason.Programmatic);
                    }
                });
            AssertThat(windowResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Owner B: Screen layer, contributes VisibleInert to the window.
            var inertResult = fixture.Host.TryPresent(
                inertOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(inertResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(window.GuiDisableInput).IsTrue();
            AssertThat(window.Unfocusable).IsTrue();

            // Owner A: Modal layer, contributes Hidden (strongest). The
            // window is hidden; GuiDisableInput/Unfocusable are restored to
            // baseline (false) before SetPresented(false) fires.
            var hiddenResult = fixture.Host.TryPresent(
                hiddenOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(hiddenResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(window.Visible).IsFalse();

            // Unrelated entry at Toast (above Modal) so it does not contribute
            // to the window's reduced effect.
            var unrelatedResult = fixture.Host.TryPresent(
                unrelatedView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Toast
                });
            AssertThat(unrelatedResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            unrelatedHandle = unrelatedResult.Handle;

            // Close Owner A → window transitions Hidden → VisibleInert.
            // SetPresented(true) fires and closes the unrelated entry.
            AssertThat(fixture.Host.TryClose(
                hiddenResult.Handle!.Value,
                UIScreenCloseReason.Programmatic).Status).IsEqual(UIScreenCloseStatus.Closed);

            AssertThat(closedUnrelated).IsTrue();
            AssertThat(fixture.Host.IsActive(unrelatedHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.IsActive(windowResult.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.IsActive(inertResult.Handle!.Value)).IsTrue();

            // The window's reduced effect is still VisibleInert (Owner B
            // remains active). Recompute must have reapplied the effect rather
            // than skipping the provisional marker, so the window is visible
            // but input-disabled.
            AssertThat(window.Visible).IsTrue();
            AssertThat(window.GuiDisableInput).IsTrue();
            AssertThat(window.Unfocusable).IsTrue();

            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.WindowEffects.ContainsKey(windowResult.Handle.Value))
                .IsTrue();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyWindowEffect_SetPresentedClosesOwnWindowUnderHiddenOwner_BaselineRestored()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var lowerWindow = fixture.Track(new Window
        {
            Visible = true,
            GuiDisableInput = false,
            Unfocusable = false
        });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? lowerHandle = null;
        try
        {
            var lowerResult = fixture.Host.TryPresent(
                lowerWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetPresented = presented =>
                    {
                        lowerWindow.Visible = presented;
                        if (presented || !lowerHandle.HasValue)
                            return;
                        // Re-entrant close of self from the SetPresented(false)
                        // callback while ApplyWindowEffect is applying Hidden.
                        fixture.Host.TryClose(
                            lowerHandle.Value,
                            UIScreenCloseReason.Programmatic);
                    }
                });
            AssertThat(lowerResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            lowerHandle = lowerResult.Handle;

            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The lower entry must have been closed by the re-entrant callback.
            AssertThat(fixture.Host.IsActive(lowerHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The lower window's baseline visibility and input properties must
            // be restored. ApplyWindowEffect must have aborted before re-adding
            // the stale effect.
            AssertThat(lowerWindow.Visible).IsTrue();
            AssertThat(lowerWindow.GuiDisableInput).IsFalse();
            AssertThat(lowerWindow.Unfocusable).IsFalse();

            // No stale effect bookkeeping should remain for the closed handle.
            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.WindowEffects.ContainsKey(lowerHandle.Value))
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyWindowEffect_IsPresentedClosesTarget_AbortsBeforeBaselineCapture()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var window = fixture.Track(new Window
        {
            Visible = true,
            GuiDisableInput = false,
            Unfocusable = false
        });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? windowHandle = null;
        var isPresentedEntered = false;
        var closedSelf = false;
        try
        {
            var windowResult = fixture.Host.TryPresent(
                window,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    IsPresented = () =>
                    {
                        if (isPresentedEntered || !windowHandle.HasValue || closedSelf)
                            return window.Visible;
                        isPresentedEntered = true;
                        // Re-entrant self-close: IsPresented closes the target
                        // entry itself while ApplyWindowEffect captures the
                        // baseline. The generation changes before the baseline
                        // is captured. Without the guard, the baseline is
                        // captured for a now-closed entry and leaks.
                        closedSelf = true;
                        fixture.Host.TryClose(
                            windowHandle.Value,
                            UIScreenCloseReason.Programmatic);
                        return window.Visible;
                    }
                });
            AssertThat(windowResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            windowHandle = windowResult.Handle;

            // Present an upper Screen owner that inerts the window
            // (VisibleInert). This triggers ApplyWindowEffect which calls
            // IsPresented; the re-entrant self-close must abort before the
            // baseline is captured.
            var upperResult = fixture.Host.TryPresent(
                upperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            AssertThat(upperResult.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(closedSelf).IsTrue();
            AssertThat(fixture.Host.IsActive(windowHandle!.Value)).IsFalse();

            // The IsPresented callback closed the target. ApplyWindowEffect must
            // abort before capturing the baseline, so no window effect baseline
            // is recorded for the closed handle. Without the guard, the baseline
            // would be captured after the close (CloseAdapter's
            // RestoreLowerLayerEffect already ran and found nothing to remove)
            // and leak in _windowEffectBaselines.
            var diagnostics = fixture.Host.Diagnostics;
            AssertThat(diagnostics.StateLeases.WindowEffects.ContainsKey(windowHandle.Value))
                .IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task CloseQueue_StaleQueuedAncestorCloseDoesNotStrandLaterUnrelatedClose()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var ancestorView = fixture.Track(new Control());
        var descendantView = fixture.Track(new Control());
        var unrelatedView = fixture.Track(new Control());
        var triggerView = fixture.Track(new Control());
        var cleanupLog = new List<string>();
        try
        {
            var ancestor = fixture.Host.TryPresent(
                ancestorView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen
                }).Handle!.Value;
            var descendant = fixture.Host.TryPresent(
                descendantView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = ancestor,
                    Layer = UIScreenLayer.Modal
                }).Handle!.Value;
            var unrelated = fixture.Host.TryPresent(
                unrelatedView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Toast
                }).Handle!.Value;
            var trigger = fixture.Host.TryPresent(
                triggerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    Cleanup = _ =>
                    {
                        // While the drain is in progress, enqueue closes for an
                        // ancestor, its descendant, and an unrelated entry.
                        cleanupLog.Add("trigger");
                        cleanupLog.Add(fixture.Host.TryClose(
                            ancestor, UIScreenCloseReason.Programmatic)
                            .Status.ToString());
                        cleanupLog.Add(fixture.Host.TryClose(
                            descendant, UIScreenCloseReason.Programmatic)
                            .Status.ToString());
                        cleanupLog.Add(fixture.Host.TryClose(
                            unrelated, UIScreenCloseReason.Programmatic)
                            .Status.ToString());
                    }
                }).Handle!.Value;

            var result = fixture.Host.TryClose(trigger, UIScreenCloseReason.Programmatic);

            AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
            // All three nested enqueues were accepted as Closed while draining.
            AssertThat(cleanupLog.ToArray()).ContainsExactly(
                "trigger", "Closed", "Closed", "Closed");
            AssertThat(fixture.Host.IsActive(ancestor)).IsFalse();
            AssertThat(fixture.Host.IsActive(descendant)).IsFalse();
            // The unrelated entry must not strand behind the stale descendant
            // request that made no progress after its ancestor was closed.
            AssertThat(fixture.Host.IsActive(unrelated)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_ChildParentFocusViewportClosesParent_ReturnsInvalidNodeAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? parentHandle = null;
        var closeParentOnNextFocusViewport = false;
        var parentClosedFromDelegate = false;
        try
        {
            // Parent entry with a custom FocusViewport. The focus coordinator
            // invokes this delegate during a child's TryPrepare()
            // (CaptureParentFocus) BEFORE the child adapter is registered. The
            // delegate synchronously closes the parent; closing the parent
            // cascades through the model and removes the not-yet-registered
            // child. Without a liveness check after TryPrepare, TryPresent
            // would register an orphan adapter/ownership/focus record for the
            // cascade-removed child handle.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (closeParentOnNextFocusViewport && parentHandle.HasValue)
                        {
                            closeParentOnNextFocusViewport = false;
                            parentClosedFromDelegate = true;
                            fixture.Host.TryClose(
                                parentHandle.Value,
                                UIScreenCloseReason.Programmatic);
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) closes the parent, then present the
            // child synchronously before the parent's deferred ApplyInitialFocus
            // runs (so the delegate closes from the child path only). The
            // delegate is also re-invoked from CloseEntry's SafeFocusViewport
            // during the parent's cascade-close; the close-once guard (via the
            // flag) makes that re-invocation a no-op.
            closeParentOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = parent
                });
            closeParentOnNextFocusViewport = false;

            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(childResult.Handle).IsNull();
            AssertThat(parentClosedFromDelegate).IsTrue();
            // Both parent and cascade-removed child are gone from the model.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
            // No orphan focus registration, ownership metadata, or adapter.
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
            // The child view was never attached (we bailed before Apply()).
            AssertThat(childView.GetParent()).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // The parent's deferred restoration completes; no lease or focus
            // state remains.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_ChildParentFocusViewportOpensAnotherEntry_ReturnsHostMutatingAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var upperView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? parentHandle = null;
        var openOnNextFocusViewport = false;
        UIScreenOpenResult? upperResult = null;
        try
        {
            // Parent entry with a custom FocusViewport. The focus coordinator
            // invokes this delegate during a child's TryPrepare()
            // (CaptureParentFocus) BEFORE the child adapter is registered. The
            // delegate synchronously opens ANOTHER entry (an upper owner that
            // inerts the parent) re-entrantly through TryPresent. At that
            // moment the child is in _model but not in _adapters, so the upper
            // owner's ValidateEffectAdaptersForOpen and process-policy
            // validation skip the child — the child's own earlier validation
            // ran against a snapshot (unpaused tree, no inerting owner) that is
            // now stale. Without a generation snapshot around TryPrepare,
            // TryPresent would commit the child against the stale validation.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openOnNextFocusViewport)
                        {
                            openOnNextFocusViewport = false;
                            upperResult = fixture.Host.TryPresent(
                                upperView,
                                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                {
                                    Layer = UIScreenLayer.Modal,
                                    LowerLayers = UILowerLayerPolicy.VisibleInert
                                });
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) opens the upper owner, then present
            // the child synchronously before the parent's deferred
            // ApplyInitialFocus runs (so the delegate fires from the child
            // path only).
            openOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent
                });
            openOnNextFocusViewport = false;

            // The re-entrant open mutated the model during TryPrepare. The
            // child's validation snapshot is stale, so TryPresent must reject
            // the open as HostMutating rather than committing a candidate
            // whose effect/process validation was bypassed.
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(childResult.Handle).IsNull();
            // The re-entrant upper owner opened successfully.
            AssertThat(upperResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(upperResult.Value.Handle!.Value)).IsTrue();
            // The parent is still active (now inerted by the upper owner).
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            // The child was never committed: no orphan model entry, adapter,
            // ownership metadata, or focus record.
            AssertThat(fixture.Host.IsActive(childResult.Handle ?? default)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(2);
            // The child view was never attached (we bailed before Apply()).
            AssertThat(childView.GetParent()).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_ChildParentFocusViewportFreesChildView_ReturnsInvalidNodeAndLeavesNoOrphan()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? parentHandle = null;
        var freeChildOnNextFocusViewport = false;
        try
        {
            // Parent entry with a custom FocusViewport. The focus coordinator
            // invokes this delegate during a child's TryPrepare()
            // (CaptureParentFocus) BEFORE the child adapter is registered or
            // the child view is attached (AddChild happens in adapter.Apply(),
            // which runs after TryPrepare). The delegate synchronously frees
            // the child view without closing the child's model handle. Freeing
            // a detached node does not fire TreeExiting, so the model entry is
            // not removed and the generation/IsActive guards do not catch it.
            // Without re-validating the view's Godot-object validity after
            // TryPrepare, TryPresent proceeds to view.SetMeta(), which
            // dereferences the freed object and throws — stranding the model
            // entry and the adapter already added to _adapters with no
            // ownership metadata, focus record, or tree-exit handler.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (freeChildOnNextFocusViewport &&
                            GodotObject.IsInstanceValid(childView))
                        {
                            freeChildOnNextFocusViewport = false;
                            childView.Free();
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) frees the child view, then present
            // the child synchronously before the parent's deferred
            // ApplyInitialFocus runs (so the delegate fires from the child
            // path only).
            freeChildOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent
                });
            freeChildOnNextFocusViewport = false;

            // The freed view must be detected after TryPrepare and the open
            // rejected as InvalidNode — mirroring the view-validity check at
            // the top of TryPresent — instead of dereferencing the freed
            // object in SetMeta and stranding the model/adapter pair.
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(childResult.Handle).IsNull();
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            // No orphan model entry, adapter, ownership metadata, or focus
            // record for the child.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
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

    private sealed partial class EnablesInputOnReadyControl : Control
    {
        public bool WasReady { get; private set; }

        public override void _Ready()
        {
            WasReady = true;
            SetProcessInput(true);
        }
    }

    private sealed partial class OpensOwnerOnReadyControl : Control
    {
        public UIScreenHost Host { get; init; } = null!;
        public Control OwnerView { get; init; } = null!;
        public UIScreenOpenResult OwnerResult { get; private set; }

        public override void _Ready()
        {
            SetProcessInput(true);
            OwnerResult = Host.TryPresent(
                OwnerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
        }
    }
}
