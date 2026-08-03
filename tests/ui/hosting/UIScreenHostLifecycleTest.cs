using System;
using System.Collections.Generic;
using System.Linq;
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
    public async Task TryPresent_ReadyOpensSameLayerInertingOwner_RevalidationRejectsCandidateWithoutAdapter()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var candidateView = fixture.Track(new OpensSameLayerInertingOwnerOnReadyControl());
        var ownerView = fixture.Track(new Control());
        candidateView.Host = fixture.Host;
        candidateView.OwnerView = ownerView;
        try
        {
            // Candidate C on Screen with no SetInteractive adapter. C's
            // _Ready() disables input, opens owner U on the SAME layer
            // (Screen) with VisibleInert, then re-enables input. U's initial
            // validation passes because C is not processing input at that
            // moment. After C's Apply() returns, the generation changed (U
            // was opened), so RevalidateAfterApply runs. Without the fix,
            // the second loop only checks owners with Layer > candidate.Layer,
            // skipping same-layer U — C is falsely accepted even though U
            // cannot inert C (no SetInteractive adapter). With the fix,
            // sequence-aware IsVisuallyAbove detects U (same layer, higher
            // sequence) and rejects C with MissingRequiredAdapter.
            var result = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Screen
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(result.Handle).IsNull();
            // U opened successfully from C's _Ready().
            AssertThat(candidateView.OwnerResult.Status)
                .IsEqual(UIScreenOpenStatus.Opened);
            // C was rejected — not active, not attached.
            AssertThat(fixture.Host.IsActive(result.Handle ?? default)).IsFalse();
            AssertThat(candidateView.GetParent()).IsNull();
            // U remains active.
            AssertThat(fixture.Host.IsActive(candidateView.OwnerResult.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_CandidateDeclaresVisibleInert_ReadyMutates_RevalidationDoesNotRejectSelf()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var candidateView = fixture.Track(new OpensHigherLayerEntryOnReadyControl());
        var upperView = fixture.Track(new Control());
        candidateView.Host = fixture.Host;
        candidateView.UpperView = upperView;
        try
        {
            // Candidate C on Screen declares LowerLayers = VisibleInert,
            // has no SetInteractive adapter, and is processing input (default).
            // C's _Ready() opens an entry on Modal with VisibleInteractive
            // (no inerting), causing a generation change. RevalidateAfterApply
            // runs. Without the fix, the first loop checks C against itself
            // (IsCandidateVisuallyAbove uses >=, so C is "above" itself) and
            // CanApply(VisibleInert) fails for C's own adapter (processing
            // input, no adapter) — C is falsely rejected. With the fix, the
            // candidate is skipped in the first loop, and the second loop
            // passes (the Modal entry has VisibleInteractive). C opens
            // successfully.
            var result = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(result.Handle).IsNotNull();
            // The upper entry opened from _Ready().
            AssertThat(candidateView.UpperResult.Status)
                .IsEqual(UIScreenOpenStatus.Opened);
            // Both C and the upper entry are active.
            AssertThat(fixture.Host.IsActive(result.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.IsActive(candidateView.UpperResult.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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
        UIScreenHandle? closedModalHandle = null;
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
                                closedModalHandle = entry.Handle;
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
            // The SetInteractive callback closed the modal during its own
            // final Recompute. The final commit check detects the candidate
            // is no longer active and returns InvalidNode instead of a stale
            // Opened handle.
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(modalResult.Handle).IsNull();

            // The re-entrant close must have closed the modal during its own open.
            AssertThat(fixture.Host.IsActive(closedModalHandle!.Value)).IsFalse();
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
    public async Task Recompute_SubscriberClosingTopOwnerAfterCommit_ClosesNormallyAndStaysConsistent()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplay = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var shield = fixture.Host.GetNode<Control>("InputShield");
        var observations = new List<UIScreenEffectiveState>();
        var firstSubscriberClosed = false;
        UIScreenHandle? closedModalHandle = null;

        // Earlier subscriber: closes the modal the first time it is named the
        // top input owner, mutating the host during publication. Publication
        // is suppressed during the candidate's Recompute and deferred until
        // after the commit check passes, so this subscriber fires only after
        // the modal is committed — the close is a normal close, not a
        // rollback.
        fixture.Host.EffectiveStateChanged += _ =>
        {
            if (firstSubscriberClosed)
                return;
            foreach (var entry in fixture.Host.ActiveEntries)
            {
                if (entry.Policy.Kind == UIScreenKinds.Pause)
                {
                    firstSubscriberClosed = true;
                    closedModalHandle = entry.Handle;
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
            // The modal was committed before the subscriber fired. The
            // subscriber then closed it normally. The modal result is Opened
            // (the commit check passed before the subscriber ran).
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(modalResult.Handle).IsNotNull();

            // The subscriber closed the modal after commit.
            AssertThat(firstSubscriberClosed).IsTrue();
            AssertThat(fixture.Host.IsActive(closedModalHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(shield.Visible).IsFalse();
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(gameplayResult.Handle);

            // The later subscriber's final observation must agree with the model
            // (gameplay as top owner), not a stale snapshot naming the closed
            // modal.
            var final = observations[observations.Count - 1];
            AssertThat(final.TopInputOwner).IsEqual(gameplayResult.Handle);
            AssertThat(final.TopInputOwner?.Kind == UIScreenKinds.Pause)
                .IsEqual(false);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task Recompute_GameplayInputBlockChangedFiresAfterCommit_SubscriberCanCloseTopOwner()
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
        UIScreenHandle? closedModalHandle = null;

        blockCallback = _ =>
        {
            if (blockCallbackClosed)
                return;
            foreach (var entry in fixture.Host.ActiveEntries)
            {
                if (entry.Policy.Kind == UIScreenKinds.Pause)
                {
                    blockCallbackClosed = true;
                    closedModalHandle = entry.Handle;
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
            // GameplayInputBlockChanged is suppressed during the candidate's
            // Recompute and deferred until after the commit check passes. The
            // modal was committed before the callback fired; the callback then
            // closed it normally.
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(modalResult.Handle).IsNotNull();

            // The callback closed the modal after commit.
            AssertThat(blockCallbackClosed).IsTrue();
            AssertThat(fixture.Host.IsActive(closedModalHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // GameplayInputBlockChanged fires before EffectiveStateChanged in
            // Recompute's publication phase. The callback closed the modal
            // before any EffectiveStateChanged publication, so no
            // EffectiveStateChanged observation names the closed modal. The
            // final published state names gameplay as top owner.
            AssertThat(publishedStates.Count).IsGreater(0);
            foreach (var state in publishedStates)
            {
                AssertThat(state.TopInputOwner?.Kind == UIScreenKinds.Pause)
                    .IsEqual(false);
            }

            // The final state has gameplay as top owner and no gameplay-input
            // block (the modal that blocked gameplay was closed).
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
    public async Task PublicationCallback_ClosingCandidateDuringPublication_ReturnsOpenedAndStaysConsistent()
    {
        var blockTransitions = new List<bool>();
        var fixture = await UIScreenHostTestSupport.CreateHost(this, options:
            new UIScreenHostOptions
            {
                GameplayInputBlockChanged = blocked => blockTransitions.Add(blocked)
            });
        var gameplay = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var shield = fixture.Host.GetNode<Control>("InputShield");
        var publishedStates = new List<UIScreenEffectiveState>();
        var subscriberClosed = false;
        UIScreenHandle? closedModalHandle = null;

        // The EffectiveStateChanged subscriber fires during the publication
        // Recompute (the second, unsupervised pass after the commit check).
        // It closes the committed candidate the first time it sees the modal
        // named as the top input owner. This is a post-commit mutation: the
        // open succeeded, and the returned handle must remain Opened even
        // though the entry is already closed by the time TryPresent returns.
        fixture.Host.EffectiveStateChanged += state =>
        {
            publishedStates.Add(state);
            if (subscriberClosed)
                return;
            foreach (var entry in fixture.Host.ActiveEntries)
            {
                if (entry.Policy.Kind == UIScreenKinds.Pause)
                {
                    subscriberClosed = true;
                    closedModalHandle = entry.Handle;
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

            // Clear publications captured during the gameplay open so the
            // remaining assertions isolate the modal's publication phase.
            var gameplayPublications = publishedStates.Count;
            var gameplayBlockTransitions = blockTransitions.Count;

            var modalResult = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true
                });

            // The modal was committed before the subscriber fired. The
            // subscriber then closed it normally. The modal result is Opened
            // (the commit check passed before the subscriber ran); mutations
            // during the initial publication do not invalidate the result.
            AssertThat(modalResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(modalResult.Handle).IsNotNull();

            // The subscriber closed the modal after commit.
            AssertThat(subscriberClosed).IsTrue();
            AssertThat(fixture.Host.IsActive(closedModalHandle!.Value)).IsFalse();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            // The gameplay-input block transitions for the modal's publication
            // phase must be a matched pair: true when the modal was published,
            // false when the subscriber's close was published. No unmatched
            // notification may fire — the staged state was never published
            // without a corresponding rollback publication.
            var modalBlockTransitions = blockTransitions.Skip(gameplayBlockTransitions).ToList();
            AssertThat(modalBlockTransitions.ToArray())
                .ContainsExactly(true, false);

            // No published state may name the closed modal as top input owner
            // after the close. The final published state names gameplay.
            var modalPublications = publishedStates.Skip(gameplayPublications).ToList();
            AssertThat(modalPublications.Count).IsGreater(0);
            var final = modalPublications[modalPublications.Count - 1];
            AssertThat(final.TopInputOwner).IsEqual(gameplayResult.Handle);
            AssertThat(final.IsPresentationGameplayBlocked).IsEqual(false);

            // Host operational state is consistent: gameplay restored to
            // interactive, shield withdrawn.
            AssertThat(gameplay.IsProcessingInput()).IsTrue();
            AssertThat(shield.Visible).IsFalse();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task RejectedGameplayBlockingCandidate_ProducesZeroExternalStateCallbacks()
    {
        var blockTransitions = new List<bool>();
        var publishedStates = new List<UIScreenEffectiveState>();
        var fixture = await UIScreenHostTestSupport.CreateHost(this, options:
            new UIScreenHostOptions
            {
                GameplayInputBlockChanged = blocked => blockTransitions.Add(blocked)
            });
        var gameplayView = fixture.Track(new Control { Visible = true });
        // Caller-preparented External view: the contract says a rejected open
        // must not terminally hide/free OR detach a caller-preparented view.
        var modalLayer = fixture.Host.GetNode<Control>("ModalLayer");
        var candidateView = fixture.Track(new Control { Visible = true });
        modalLayer.AddChild(candidateView);
        fixture.Host.EffectiveStateChanged += state => publishedStates.Add(state);
        var candidateQueued = false;
        try
        {
            var gameplayResult = fixture.Host.TryPresent(
                gameplayView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        gameplayView.SetProcessInput(enabled);
                        if (enabled || candidateQueued)
                            return;
                        // The candidate's suppressed Recompute inerts the
                        // gameplay entry (SetInteractive(false)) via
                        // ApplyLowerLayerEffects. Queue the candidate view for
                        // deletion from this callback. QueueFree does not fire
                        // TreeExiting synchronously and does not bump
                        // MutationGeneration, so the suppressed Recompute
                        // converges with the candidate still in the model and
                        // _currentState set to the staged (block=true) state.
                        // The post-Recompute commit check then detects
                        // IsQueuedForDeletion and rejects the open.
                        candidateQueued = true;
                        candidateView.QueueFree();
                    }
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayView.SetProcessInput(true);

            // Isolate callbacks emitted by the candidate's rejection. The
            // gameplay open published its own state; capture the baseline.
            var gameplayPublications = publishedStates.Count;
            var gameplayBlockTransitions = blockTransitions.Count;

            var opened = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true,
                    NodeLifetime = UINodeLifetime.External,
                    Cleanup = _ => { }
                });

            // The commit check detected the queued-for-deletion view and
            // rejected the open.
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(opened.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.IsActive(gameplayResult.Handle!.Value)).IsTrue();

            // The rejected candidate's suppressed Recompute set _currentState
            // to the staged state (block=true) but must not have advanced
            // _lastPublishedState. The rollback Recompute compares
            // _lastPublishedState (pre-open, block=false) to the candidate-
            // free resolved state (block=false) — no delta, no publication.
            // Zero unmatched GameplayInputBlockChanged or EffectiveStateChanged
            // callbacks may fire for the rejection.
            var rejectionBlockTransitions = blockTransitions.Skip(gameplayBlockTransitions).ToList();
            var rejectionPublications = publishedStates.Skip(gameplayPublications).ToList();
            AssertThat(rejectionBlockTransitions.Count).IsEqual(0);
            AssertThat(rejectionPublications.Count).IsEqual(0);

            // The host's current state agrees with the pre-open state: no
            // gameplay-input block, gameplay as top input owner.
            AssertThat(fixture.Host.CurrentState.IsPresentationGameplayBlocked)
                .IsEqual(false);
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(gameplayResult.Handle);
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
    public async Task TryPresent_RollbackPendingOpenCleanupReentersTryPresent_ReturnsHostMutating()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var grandchildView = fixture.Track(new Control { Visible = true });
        var cleanupReentrantView = fixture.Track(new Control());
        UIScreenHandle? parentHandle = null;
        var openOnNextFocusViewport = false;
        UIScreenOpenResult? cleanupReentrantResult = null;
        try
        {
            // Parent entry with a custom FocusViewport. The focus coordinator
            // invokes this delegate during a child's TryPrepare()
            // (CaptureParentFocus). The delegate synchronously opens a
            // GRANDCHILD beneath the child candidate (Parent = child's handle,
            // found via ActiveEntries). The grandchild has a Cleanup callback
            // that tries to re-enter TryPresent. The child's TryPrepare
            // generation guard detects the mutation and calls
            // RollbackPendingOpen, which cascade-removes the grandchild and
            // invokes its Cleanup. Without _drainingCloseQueue during
            // rollback, the Cleanup's TryPresent would proceed instead of
            // returning HostMutating — violating the guarantee that managed
            // cleanup cannot reopen during an active host transaction.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openOnNextFocusViewport)
                        {
                            openOnNextFocusViewport = false;
                            // Find the child candidate's handle from
                            // ActiveEntries (it's in _model but not yet
                            // committed).
                            UIScreenHandle? childHandle = null;
                            foreach (var entry in fixture.Host.ActiveEntries)
                            {
                                if (entry.Policy.Kind == UIScreenKinds.Inventory)
                                {
                                    childHandle = entry.Handle;
                                    break;
                                }
                            }
                            if (childHandle.HasValue)
                            {
                                fixture.Host.TryPresent(
                                    grandchildView,
                                    UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                    {
                                        Parent = childHandle,
                                        Cleanup = _ =>
                                        {
                                            cleanupReentrantResult =
                                                fixture.Host.TryPresent(
                                                    cleanupReentrantView,
                                                    UIScreenHostTestSupport.Spec(
                                                        UIScreenKinds.Battle));
                                        }
                                    });
                            }
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) opens the grandchild beneath the
            // child, then present the child.
            openOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent
                });
            openOnNextFocusViewport = false;

            // The re-entrant open mutated the model during TryPrepare.
            // The child's validation snapshot is stale, so TryPresent must
            // reject the open as HostMutating.
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(childResult.Handle).IsNull();
            // The parent is still active.
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            // The child and grandchild were both rolled back.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);
            // The grandchild's Cleanup callback tried to re-enter TryPresent
            // during rollback. Without _drainingCloseQueue, it would have
            // proceeded; with the fix, it must return HostMutating.
            AssertThat(cleanupReentrantResult).IsNotNull();
            AssertThat(cleanupReentrantResult!.Value.Status)
                .IsEqual(UIScreenOpenStatus.HostMutating);
            // The grandchild view was detached during rollback.
            AssertThat(grandchildView.GetParent()).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_RevalidationRejectsCandidate_DoesNotInvokeCleanupOrApplyNodeLifetime()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var candidateView = fixture.Track(new OpensSameLayerInertingOwnerOnReadyControl());
        var ownerView = fixture.Track(new Control());
        candidateView.Host = fixture.Host;
        candidateView.OwnerView = ownerView;
        var cleanupCalled = false;
        try
        {
            // Candidate C on Screen with no SetInteractive adapter,
            // NodeLifetime = QueueFree, and a Cleanup callback. C's _Ready()
            // opens a same-layer owner with VisibleInert (triggering
            // revalidation failure). Without the fix, RollbackPendingOpen
            // calls CloseAdapter for the candidate, which invokes Cleanup
            // and applies NodeLifetime (QueueFree). With the fix, the
            // candidate is handled by RollbackPendingCandidate, which calls
            // RollbackRegistration (detach only, no Cleanup, no NodeLifetime).
            var result = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Screen,
                    NodeLifetime = UINodeLifetime.QueueFree,
                    Cleanup = _ => cleanupCalled = true
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            // Cleanup must NOT be called — rejected opens are atomic no-ops.
            AssertThat(cleanupCalled).IsFalse();
            // NodeLifetime (QueueFree) must NOT be applied — the view is not
            // queued for deletion. RollbackRegistration detaches without
            // applying NodeLifetime.
            AssertThat(candidateView.IsQueuedForDeletion()).IsFalse();
            // The view was detached by RollbackRegistration.
            AssertThat(candidateView.GetParent()).IsNull();
            // The owner opened from _Ready() remains active.
            AssertThat(fixture.Host.IsActive(candidateView.OwnerResult.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_RollbackPendingOpenCleanupClosesUnrelated_EntryIsClosedWhenTryPresentReturns()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var grandchildView = fixture.Track(new Control { Visible = true });
        var unrelatedView = fixture.Track(new Control { Visible = true });
        var openOnNextFocusViewport = false;
        UIScreenHandle? unrelatedHandle = null;
        UIScreenCloseResult? cleanupCloseResult = null;
        try
        {
            // Parent with a FocusViewport that opens a grandchild beneath the
            // child candidate during TryPrepare. The grandchild's Cleanup
            // calls TryClose on an unrelated active entry. The child's
            // TryPrepare generation guard detects the mutation and calls
            // RollbackPendingOpen, which cascade-removes the grandchild and
            // invokes its Cleanup. Under _drainingCloseQueue, TryClose queues
            // the request and returns Closed. Without the drain fix, the
            // unrelated entry remains active indefinitely. With the fix, the
            // drain processes the queued close before RollbackPendingOpen
            // returns.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openOnNextFocusViewport)
                        {
                            openOnNextFocusViewport = false;
                            UIScreenHandle? childHandle = null;
                            foreach (var entry in fixture.Host.ActiveEntries)
                            {
                                if (entry.Policy.Kind == UIScreenKinds.Inventory)
                                {
                                    childHandle = entry.Handle;
                                    break;
                                }
                            }
                            if (childHandle.HasValue)
                            {
                                fixture.Host.TryPresent(
                                    grandchildView,
                                    UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                    {
                                        Parent = childHandle,
                                        Cleanup = _ =>
                                        {
                                            if (unrelatedHandle.HasValue)
                                                cleanupCloseResult = fixture.Host.TryClose(
                                                    unrelatedHandle.Value,
                                                    UIScreenCloseReason.Programmatic);
                                        }
                                    });
                            }
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;

            // Open an unrelated entry that will be closed by the grandchild's
            // Cleanup during rollback.
            unrelatedHandle = fixture.Host.TryPresent(
                unrelatedView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle)).Handle;

            // Arm the delegate and present the child.
            openOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent
                });
            openOnNextFocusViewport = false;

            // The child was rejected (generation changed during TryPrepare).
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            // The grandchild's Cleanup called TryClose on the unrelated entry.
            AssertThat(cleanupCloseResult).IsNotNull();
            AssertThat(cleanupCloseResult!.Value.Status)
                .IsEqual(UIScreenCloseStatus.Closed);
            // The unrelated entry was closed by the drain before
            // RollbackPendingOpen returned.
            AssertThat(fixture.Host.IsActive(unrelatedHandle!.Value)).IsFalse();
            // Only the parent remains active.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

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

    [TestCase]
    public async Task PrepareForTeardown_RollbackCleanupBeginsTeardown_RetryClosesRemainingEntriesAndCompletes()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var grandchildView = fixture.Track(new Control { Visible = true });
        var unrelatedView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? parentHandle = null;
        UIScreenHandle? unrelatedHandle = null;
        var openOnNextFocusViewport = false;
        UIScreenTeardownPreparationStatus? cleanupTeardownStatus = null;
        try
        {
            // Parent P with a custom FocusViewport. The focus coordinator
            // invokes this delegate during a child's TryPrepare()
            // (CaptureParentFocus). The delegate synchronously opens a
            // GRANDCHILD beneath the child candidate (Parent = child's
            // handle, found via ActiveEntries). The grandchild has a Cleanup
            // callback that calls PrepareForTeardown(). The child's TryPrepare
            // generation guard detects the mutation and calls
            // RollbackPendingOpen, which cascade-removes the grandchild and
            // invokes its Cleanup. Inside that Cleanup, PrepareForTeardown()
            // runs while _drainingCloseQueue is held: BeginTeardown() sets
            // _tearingDown = true but cannot close entries, and
            // FinalizeTeardown() returns early. RollbackPendingOpen's finally
            // clears _drainingCloseQueue but does NOT resume teardown. An
            // unrelated entry U and the parent P remain active. Without the
            // fix, a later PrepareForTeardown() re-enters the
            // already-tearing-down branch which only calls FinalizeTeardown()
            // — returning Deferred forever because entries remain. With the
            // fix, the already-tearing-down branch closes remaining entries
            // before finalizing, so the retry reaches Complete.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openOnNextFocusViewport)
                        {
                            openOnNextFocusViewport = false;
                            UIScreenHandle? childHandle = null;
                            foreach (var entry in fixture.Host.ActiveEntries)
                            {
                                if (entry.Policy.Kind == UIScreenKinds.Inventory)
                                {
                                    childHandle = entry.Handle;
                                    break;
                                }
                            }
                            if (childHandle.HasValue)
                            {
                                fixture.Host.TryPresent(
                                    grandchildView,
                                    UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                    {
                                        Parent = childHandle,
                                        Cleanup = _ =>
                                            cleanupTeardownStatus =
                                                fixture.Host.PrepareForTeardown()
                                    });
                            }
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Open an unrelated entry U that stays active across the rollback.
            unrelatedHandle = fixture.Host.TryPresent(
                unrelatedView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle)).Handle;

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) opens the grandchild beneath the
            // child, then present the child.
            openOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent
                });
            openOnNextFocusViewport = false;

            // The re-entrant open mutated the model during TryPrepare, so the
            // child is rejected as HostMutating (the candidate was not
            // cascade-removed by the callback).
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(childResult.Handle).IsNull();
            // The grandchild's Cleanup called PrepareForTeardown() during
            // rollback. Teardown began (_tearingDown became true) but could
            // not close entries while _drainingCloseQueue was held, so it
            // reported Deferred.
            AssertThat(cleanupTeardownStatus).IsNotNull();
            AssertThat(cleanupTeardownStatus!.Value).IsEqual(
                UIScreenTeardownPreparationStatus.Deferred);
            // The grandchild was rolled back; the parent and the unrelated
            // entry remain active.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            AssertThat(fixture.Host.IsActive(unrelatedHandle!.Value)).IsTrue();

            // A later PrepareForTeardown() must close the remaining entries
            // (parent + unrelated) and reach Complete. Without the fix, the
            // already-tearing-down branch only calls FinalizeTeardown(), which
            // returns early because entries remain — Deferred forever.
            var retry = fixture.Host.PrepareForTeardown();
            AssertThat(retry).IsEqual(UIScreenTeardownPreparationStatus.Complete);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(fixture.Host.IsActive(parent)).IsFalse();
            AssertThat(fixture.Host.IsActive(unrelatedHandle.Value)).IsFalse();
            // The parent and unrelated views were detached during teardown.
            AssertThat(parentView.GetParent()).IsNull();
            AssertThat(unrelatedView.GetParent()).IsNull();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(fixture.Host))
                fixture.Host.PrepareForTeardown();
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_ParentFocusViewportOpensChildFreesBlockingWindow_SinkFailureRollsBackAtomically()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var parentView = fixture.Track(new Control { Visible = true });
        var candidateWindow = fixture.Track(new Window { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        UIScreenHandle? parentHandle = null;
        var armFocusViewport = false;
        var childCleanupCalled = false;
        try
        {
            // Parent P (Screen priority) with a custom FocusViewport. The
            // focus coordinator invokes this delegate during a Blocking
            // Window candidate's TryPrepare() (CaptureParentFocus) BEFORE the
            // dynamic focus sink is attached. The delegate synchronously:
            //   1. opens a logical CHILD beneath the pending candidate
            //      (Parent = candidate handle, found via ActiveEntries) — the
            //      child fully commits (adapter, focus record, tree-exit
            //      handler, applied effects);
            //   2. frees the candidate Window.
            // Back in TryPrepare, window.AddChild(dynamicSink) on the freed
            // Window throws. The catch must NOT dereference adapter.View.Name
            // on the freed object (a pre-captured diagnostic name is required)
            // or a secondary exception propagates and bypasses recovery.
            // TryPresent must route the post-model failure through
            // RollbackPendingOpen so the committed child is fully cleaned
            // (adapter, focus, ownership, effects) instead of orphaned by a
            // bare _model.Close(handle) that ignores ClosedEntries.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (armFocusViewport)
                        {
                            armFocusViewport = false;
                            UIScreenHandle? candidateHandle = null;
                            foreach (var entry in fixture.Host.ActiveEntries)
                            {
                                if (entry.Policy.Kind == UIScreenKinds.SaveLoad)
                                {
                                    candidateHandle = entry.Handle;
                                    break;
                                }
                            }
                            if (candidateHandle.HasValue)
                            {
                                fixture.Host.TryPresent(
                                    childView,
                                    UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                                    {
                                        Parent = candidateHandle,
                                        Cleanup = _ => childCleanupCalled = true
                                    });
                            }
                            // Free the candidate Window after opening the
                            // child. Freeing a detached Window (AddChild has
                            // not run yet — it happens in adapter.Apply(),
                            // after TryPrepare) does not fire TreeExiting, so
                            // the model entry is not removed and the
                            // generation/IsActive guards do not catch it.
                            if (GodotObject.IsInstanceValid(candidateWindow))
                                candidateWindow.Free();
                        }
                        return parentView.GetViewport();
                    }
                }).Handle!.Value;
            parentHandle = parent;

            // Arm the delegate so the next FocusViewport invocation (the
            // candidate's CaptureParentFocus) opens the child and frees the
            // Window, then present the Blocking Window candidate.
            armFocusViewport = true;
            var result = fixture.Host.TryPresent(
                candidateWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad) with
                {
                    Parent = parent,
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking
                });
            armFocusViewport = false;

            // The sink attachment failed on the freed Window. The catch must
            // use a pre-captured diagnostic name — without it, dereferencing
            // adapter.View.Name on the freed Godot object throws a second
            // exception that propagates out of TryPresent. Reaching these
            // assertions at all proves no secondary exception escaped.
            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(result.Handle).IsNull();
            // The committed child beneath the candidate was fully cleaned by
            // RollbackPendingOpen's cascade path (CloseAdapter invoked its
            // Cleanup). A bare _model.Close(handle) would have removed the
            // child from the pure model but orphaned its adapter, focus
            // record, ownership metadata, and tree-exit handler without
            // invoking Cleanup.
            AssertThat(childCleanupCalled).IsTrue();
            // Only the parent remains active — no orphan model entry, adapter,
            // ownership metadata, or focus record for the candidate or child.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.IsActive(parent)).IsTrue();
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);
            // The child view was detached during cascade cleanup.
            AssertThat(childView.GetParent()).IsNull();
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            if (GodotObject.IsInstanceValid(fixture.Host))
                fixture.Host.PrepareForTeardown();
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_ParentFocusViewportFreesViewAndThrows_DoesNotDoubleThrowAndReturnsInvalidNode()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var armed = false;
        try
        {
            // Parent with a custom FocusViewport. When armed, the delegate
            // frees the parent's own view (which is in the tree, so Free()
            // fires TreeExiting synchronously and closes the parent) and then
            // throws. SafeFocusViewport's catch must not dereference the freed
            // adapter.View.Name, or a second exception propagates and bypasses
            // the null fallback — stranding the child's TryPrepare with no
            // graceful recovery.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (armed && GodotObject.IsInstanceValid(parentView))
                        {
                            parentView.Free();
                            throw new InvalidOperationException("focus viewport boom");
                        }
                        return parentView.GetViewport();
                    }
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) frees parentView and throws.
            armed = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent.Handle
                });
            armed = false;

            // With the fix, SafeFocusViewport returns null (graceful fallback),
            // CaptureParentFocus returns null, and TryPrepare succeeds. The
            // generation check then detects the re-entrant close (TreeExiting
            // closed the parent and cascaded the child) and returns
            // InvalidNode. Without the fix, a second exception propagates out
            // of TryPresent.
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(childResult.Handle).IsNull();
            // No orphan entries — both parent (closed by TreeExiting) and
            // child (rejected) are gone from the model.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Host is still functional — no pending restoration for the
            // never-committed child.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task ApplyInitialFocus_FocusViewportFreesViewAndThrows_DoesNotDoubleThrowAndHostRemainsFunctional()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var entryView = fixture.Track(new Control { Visible = true });
        var laterView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var armed = false;
        try
        {
            // Entry with a custom FocusViewport. The deferred
            // ApplyInitialFocus calls SafeFocusViewport(entry.Adapter) which
            // invokes this delegate. When armed, it frees the entry's own view
            // (firing TreeExiting → TryClose) and throws. The catch in
            // SafeFocusViewport must not dereference the freed adapter.View.Name.
            var entry = fixture.Host.TryPresent(
                entryView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (armed && GodotObject.IsInstanceValid(entryView))
                        {
                            entryView.Free();
                            throw new InvalidOperationException("focus viewport boom");
                        }
                        return entryView.GetViewport();
                    }
                });
            AssertThat(entry.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Let the unarmed deferred ApplyInitialFocus run first.
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Present a second entry that stays active across the incident.
            var later = fixture.Host.TryPresent(
                laterView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory));
            AssertThat(later.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Arm the delegate and read Diagnostics. Diagnostics must NOT
            // invoke the caller-provided FocusViewport delegate (it uses the
            // viewport committed at registration). So the armed delegate must
            // NOT fire, entryView must NOT be freed, and no exception may
            // propagate out of the Diagnostics getter. The IsActive /
            // IsInstanceValid assertions below confirm the delegate was not
            // invoked by the read (if it had been, entryView would be freed).
            armed = true;
            var probe = fixture.Host.Diagnostics.FocusStates;
            armed = false;
            AssertThat(probe.Count).IsEqual(2);
            AssertThat(fixture.Host.IsActive(entry.Handle!.Value)).IsTrue();
            AssertThat(GodotObject.IsInstanceValid(entryView)).IsTrue();

            // Re-trigger SafeFocusViewport through a LEGITIMATE mutation path
            // (a child's CaptureParentFocus) instead of Diagnostics. Present a
            // child with Parent = entry; the focus coordinator invokes entry's
            // FocusViewport during TryPrepare. The armed delegate frees
            // entryView and throws. SafeFocusViewport's catch must not
            // dereference the freed adapter.View.Name (no second exception),
            // CaptureParentFocus returns null, and the child's TryPrepare
            // succeeds. The generation check then detects the re-entrant close
            // (TreeExiting closed entry and cascaded the child) and returns
            // InvalidNode.
            armed = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = entry.Handle
                });
            armed = false;

            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.InvalidNode);
            AssertThat(childResult.Handle).IsNull();
            // The freed entry is closed via TreeExiting; the child is rejected.
            AssertThat(fixture.Host.IsActive(entry.Handle!.Value)).IsFalse();
            AssertThat(fixture.Host.IsActive(later.Handle!.Value)).IsTrue();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // Host remains functional — can present another entry.
            var thirdView = fixture.Track(new Control { Visible = true });
            var third = fixture.Host.TryPresent(
                thirdView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));
            AssertThat(third.Status).IsEqual(UIScreenOpenStatus.Opened);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_PendingPauseChildNestedToastOpen_RestoresPauseAndEffectiveStateAfterRollback()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var toastView = fixture.Track(new Control { Visible = true });
        var openToastOnNextFocusViewport = false;
        UIScreenOpenResult? toastResult = null;
        try
        {
            // Parent with a custom FocusViewport. When armed, the delegate
            // opens a passive toast re-entrantly through TryPresent. The
            // toast's TryPresent calls Recompute while the pending child
            // (PauseTree=true) is still in _model but not in _adapters. That
            // re-computation pauses the tree and publishes an effective state
            // that names the pending child as the top-input owner. Without
            // Recompute after the rollback, the host remains paused and its
            // effective state can still name the rejected child.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openToastOnNextFocusViewport)
                        {
                            openToastOnNextFocusViewport = false;
                            toastResult = fixture.Host.TryPresent(
                                toastView,
                                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
                                {
                                    Layer = UIScreenLayer.Toast,
                                    InputPriority = UIInputPriority.Passive
                                });
                        }
                        return parentView.GetViewport();
                    }
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Arm the delegate so the next FocusViewport invocation (the
            // child's CaptureParentFocus) opens the toast, then present the
            // child synchronously.
            openToastOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent.Handle,
                    PauseTree = true
                });
            openToastOnNextFocusViewport = false;

            // The re-entrant toast open mutated the model during TryPrepare.
            // The child's validation snapshot is stale, so TryPresent rejects
            // the open as HostMutating (the child was not cascade-removed).
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(childResult.Handle).IsNull();
            // The toast opened successfully.
            AssertThat(toastResult!.Value.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(fixture.Host.IsActive(toastResult.Value.Handle!.Value)).IsTrue();

            // The rejected child must NOT leave its effects applied. Without
            // Recompute in the rollback, the tree would remain paused (the
            // pending child's PauseTree was applied by the toast's Recompute).
            AssertThat(fixture.Host.CurrentState.IsTreePauseOwned).IsFalse();
            AssertThat(tree.Paused).IsFalse();
            // The effective state must not name the rejected child as the
            // top-input owner.
            AssertThat(fixture.Host.CurrentState.TopInputOwner).IsNotEqual(childResult.Handle ?? default);
            // No orphan model entry or focus record for the child.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(2);
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    [TestCase]
    public async Task TryPresent_PendingChildCallbackOpensLogicalChild_RollbackCleansOrphanedDescendant()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var parentView = fixture.Track(new Control { Visible = true });
        var childView = fixture.Track(new Control { Visible = true });
        var grandchildView = fixture.Track(new Control { Visible = true });
        var openGrandchildOnNextFocusViewport = false;
        UIScreenOpenResult? grandchildResult = null;
        try
        {
            // Parent with a custom FocusViewport. When armed, the delegate
            // inspects ActiveEntries, obtains the pending candidate's handle,
            // and opens a logical child beneath it re-entrantly through
            // TryPresent. The grandchild's TryPresent completes fully —
            // adapter registered, view attached, focus record, tree-exit
            // handler — because the pending candidate is in _model (its
            // handle is in ActiveEntries) even though its adapter is not yet
            // registered. Without iterating ClosedEntries in the rollback,
            // _model.Close(handle) cascade-removes both entries from the pure
            // model but the grandchild's adapter, attached view, ownership
            // metadata, focus record, and tree-exit subscription remain
            // orphaned.
            var parent = fixture.Host.TryPresent(
                parentView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    FocusViewport = () =>
                    {
                        if (openGrandchildOnNextFocusViewport)
                        {
                            openGrandchildOnNextFocusViewport = false;
                            // Inspect ActiveEntries to find the pending
                            // candidate's handle — mirroring the reviewer's
                            // reproduction.
                            UIScreenHandle? candidateHandle = null;
                            foreach (var entry in fixture.Host.ActiveEntries)
                            {
                                if (entry.Policy.Kind == UIScreenKinds.Inventory)
                                {
                                    candidateHandle = entry.Handle;
                                    break;
                                }
                            }
                            if (candidateHandle.HasValue)
                            {
                                grandchildResult = fixture.Host.TryPresent(
                                    grandchildView,
                                    UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                                    {
                                        Parent = candidateHandle.Value,
                                        Layer = UIScreenLayer.Modal,
                                        InputPriority = UIInputPriority.Blocking
                                    });
                            }
                        }
                        return parentView.GetViewport();
                    }
                });
            AssertThat(parent.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Present the child. Its handle enters _model at the top of
            // TryPresent (line 210). The armed FocusViewport delegate finds
            // the pending candidate's handle in ActiveEntries and opens a
            // grandchild beneath it.
            openGrandchildOnNextFocusViewport = true;
            var childResult = fixture.Host.TryPresent(
                childView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Parent = parent.Handle
                });
            openGrandchildOnNextFocusViewport = false;

            // The re-entrant grandchild open mutated the model during
            // TryPrepare. The child's validation snapshot is stale, so
            // TryPresent rejects the open as HostMutating.
            AssertThat(childResult.Status).IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(childResult.Handle).IsNull();

            // The grandchild was opened re-entrantly but must be fully cleaned
            // by the rollback — no orphan adapter, focus record, ownership
            // metadata, or tree-exit handler.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(fixture.Host.Diagnostics.FocusStates.Count).IsEqual(1);
            // The grandchild's view must be detached (CloseAdapter →
            // adapter.Close removes it from its parent).
            AssertThat(grandchildView.GetParent()).IsNull();
            // No pending restoration lease for the never-committed child or
            // its rolled-back grandchild.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    // Regression for the re-entrant open gap during a suppressed Recompute
    // pass: a SetInteractive callback fires during ApplyLowerLayerEffects
    // inside the candidate's suppressed final Recompute (_recomputeDepth > 0,
    // _suppressPublication > 0). The callback attempts to open a PauseTree
    // owner. Before the fix, the guard only rejected opens when
    // _suppressPublication == 0, so the re-entrant TryPresent proceeded — but
    // both of its internal Recompute calls hit the re-entrant guard and only
    // marked _recomputePending, returning without committing pause, blocking,
    // cursor/HUD, lower-layer effects, or CurrentState. TryPresent returned
    // Opened with a handle whose effects were uncommitted at the nested return
    // boundary. With the fix, the guard rejects ALL opens while
    // _recomputeDepth > 0, so the re-entrant open returns HostMutating, the
    // PauseTree owner is never added to the model, and the candidate commits
    // successfully (no PauseTree owner to invalidate its Pausable process mode).
    [TestCase]
    public async Task SetInteractiveOpenDuringSuppressedRecompute_ReturnsHostMutatingAndCandidateCommits()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var screenLayer = fixture.Host.GetNode<Control>("ScreenLayer");
        // Caller-preparented External view: the contract says a committed open
        // must not terminally hide/free OR detach a caller-preparented view.
        var candidateView = fixture.Track(new Control { Visible = true });
        screenLayer.AddChild(candidateView);
        var gameplayView = fixture.Track(new Control { Visible = true });
        var pauseOwnerView = fixture.Track(new Control { Visible = true });
        var cleanupReasons = new List<UIScreenCloseReason>();
        UIScreenOpenResult? pauseOwnerResult = null;
        var pauseOwnerOpened = false;
        try
        {
            // Gameplay entry on Hud layer whose SetInteractive callback opens
            // a PauseTree owner when inerted. SetInteractive is a per-entry
            // effect callback that fires during ApplyLowerLayerEffects inside
            // the suppressed Recompute — it is NOT suppressed (only
            // EffectiveStateChanged / GameplayInputBlockChanged are). The
            // re-entrant TryPresent must be rejected with HostMutating so the
            // caller defers and retries after the Recompute transaction
            // completes.
            var gameplayResult = fixture.Host.TryPresent(
                gameplayView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        gameplayView.SetProcessInput(enabled);
                        if (enabled || pauseOwnerOpened)
                            return;
                        pauseOwnerOpened = true;
                        pauseOwnerResult = fixture.Host.TryPresent(
                            pauseOwnerView,
                            UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                            {
                                Layer = UIScreenLayer.Modal,
                                InputPriority = UIInputPriority.Blocking,
                                ProcessPolicy = UIProcessPolicy.Always,
                                PauseTree = true
                            });
                    }
                });
            AssertThat(gameplayResult.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayView.SetProcessInput(true);

            var opened = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Screen,
                    ProcessPolicy = UIProcessPolicy.Pausable,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    NodeLifetime = UINodeLifetime.External,
                    Cleanup = cleanupReasons.Add
                });

            // The SetInteractive callback attempted a re-entrant open during
            // the candidate's suppressed Recompute. The guard must reject it
            // with HostMutating — the re-entrant TryPresent's Recompute calls
            // would only mark _recomputePending and return without committing
            // effects, so returning Opened would give the caller a stale handle
            // with uncommitted effects.
            AssertThat(pauseOwnerOpened).IsTrue();
            AssertThat(pauseOwnerResult).IsNotNull();
            AssertThat(pauseOwnerResult!.Value.Status)
                .IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(pauseOwnerResult.Value.Handle).IsNull();

            // The PauseTree owner was never added to the model (the re-entrant
            // TryPresent returned HostMutating before _model.Open, so there is
            // no handle to check). Only gameplay and the candidate are active.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(2);

            // The candidate committed successfully: no PauseTree owner was
            // added to invalidate its Pausable process mode.
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(opened.Handle).IsNotNull();
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();

            // The tree is NOT paused: the PauseTree owner was rejected, so no
            // pause lease was acquired.
            AssertThat(tree.Paused).IsFalse();

            // Cleanup must NOT be invoked — the candidate committed, not
            // rejected. Cleanup fires only on close.
            AssertThat(cleanupReasons.Count).IsEqual(0);

            // The caller-preparented External view must remain parented to the
            // ScreenLayer (the candidate committed; no rollback occurred),
            // must remain valid, and must not be queued for deletion.
            AssertThat(GodotObject.IsInstanceValid(candidateView)).IsTrue();
            AssertThat(candidateView.IsQueuedForDeletion()).IsFalse();
            AssertThat(candidateView.GetParent()).IsEqual(screenLayer);

            // No focus-restoration lease may be started for a committed open
            // that was not followed by a close.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(fixture.Host.CurrentState.IsFocusRestorationPending)
                .IsFalse();

            // The candidate's committed state must be published: the candidate
            // is the top input owner (it is on Screen layer, higher than
            // gameplay on Hud).
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(opened.Handle);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            // No deferred restoration lease may materialize after a frame.
            AssertThat(fixture.Host.Diagnostics.RestorationLease).IsNull();
            AssertThat(cleanupReasons.Count).IsEqual(0);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    // Regression for the sequential nested-open gap during a suppressed
    // Recompute pass: when a candidate inerts multiple lower entries, each
    // entry's SetInteractive callback fires during ApplyLowerLayerEffects
    // inside the suppressed Recompute. Before the fix, the guard only rejected
    // opens when _suppressPublication == 0, so each callback's re-entrant
    // TryPresent proceeded and returned Opened — but neither committed its
    // effects (both Recompute calls were no-ops). An earlier callback could
    // open a Pausable entry X, and a later callback could open a PauseTree
    // owner Y that invalidates X's Pausable process mode. Only the outermost
    // candidate was revalidated after the suppressed pass, so X was never
    // revalidated and remained active with a stale Pausable process mode.
    // With the fix, ALL opens while _recomputeDepth > 0 are rejected with
    // HostMutating, so neither X nor Y is added to the model, no stale Pausable
    // entry remains, and the candidate commits successfully.
    [TestCase]
    public async Task SequentialSetInteractiveOpensDuringSuppressedRecompute_AllReturnHostMutatingAndNoStalePausableEntry()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplayAView = fixture.Track(new Control { Visible = true });
        var gameplayBView = fixture.Track(new Control { Visible = true });
        var pausableView = fixture.Track(new Control { Visible = true });
        var pauseOwnerView = fixture.Track(new Control { Visible = true });
        var candidateView = fixture.Track(new Control { Visible = true });

        UIScreenOpenResult? pausableResult = null;
        UIScreenOpenResult? pauseOwnerResult = null;
        var pausableOpened = false;
        var pauseOwnerOpened = false;
        var pausableKind = new StringName("test_pausable");
        var pauseOwnerKind = new StringName("test_pause_owner");

        try
        {
            // Two gameplay entries on Hud layer. Entry A's SetInteractive
            // opens a Pausable entry when inerted; entry B's SetInteractive
            // opens a PauseTree owner when inerted. Both callbacks fire during
            // the candidate's suppressed Recompute when the candidate inerts
            // both lower entries.
            var gameplayA = fixture.Host.TryPresent(
                gameplayAView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        gameplayAView.SetProcessInput(enabled);
                        if (enabled || pausableOpened)
                            return;
                        pausableOpened = true;
                        pausableResult = fixture.Host.TryPresent(
                            pausableView,
                            UIScreenHostTestSupport.Spec(pausableKind) with
                            {
                                Layer = UIScreenLayer.Screen,
                                InputPriority = UIInputPriority.Screen,
                                ProcessPolicy = UIProcessPolicy.Pausable
                            });
                    }
                });
            AssertThat(gameplayA.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayAView.SetProcessInput(true);

            var gameplayB = fixture.Host.TryPresent(
                gameplayBView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = enabled =>
                    {
                        gameplayBView.SetProcessInput(enabled);
                        if (enabled || pauseOwnerOpened)
                            return;
                        pauseOwnerOpened = true;
                        pauseOwnerResult = fixture.Host.TryPresent(
                            pauseOwnerView,
                            UIScreenHostTestSupport.Spec(pauseOwnerKind) with
                            {
                                Layer = UIScreenLayer.Modal,
                                InputPriority = UIInputPriority.Blocking,
                                ProcessPolicy = UIProcessPolicy.Always,
                                PauseTree = true
                            });
                    }
                });
            AssertThat(gameplayB.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayBView.SetProcessInput(true);

            // Candidate on Modal with VisibleInert inerts both gameplay entries.
            // Both SetInteractive callbacks fire during the suppressed Recompute.
            var opened = fixture.Host.TryPresent(
                candidateView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Modal,
                    ProcessPolicy = UIProcessPolicy.InheritHost,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true
                });

            // Both re-entrant opens must be rejected with HostMutating. With
            // the fix, the guard rejects ALL opens while _recomputeDepth > 0,
            // so neither the Pausable entry nor the PauseTree owner is added.
            AssertThat(pausableOpened).IsTrue();
            AssertThat(pausableResult).IsNotNull();
            AssertThat(pausableResult!.Value.Status)
                .IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(pausableResult.Value.Handle).IsNull();

            AssertThat(pauseOwnerOpened).IsTrue();
            AssertThat(pauseOwnerResult).IsNotNull();
            AssertThat(pauseOwnerResult!.Value.Status)
                .IsEqual(UIScreenOpenStatus.HostMutating);
            AssertThat(pauseOwnerResult.Value.Handle).IsNull();

            // No stale Pausable entry remains: the Pausable entry was never
            // added to the model. Only the two gameplay entries and the
            // candidate are active.
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(3);
            AssertThat(fixture.Host.IsKindActive(pausableKind)).IsFalse();
            AssertThat(fixture.Host.IsKindActive(pauseOwnerKind)).IsFalse();

            // The candidate committed successfully: no PauseTree owner was
            // added to invalidate any entry's process mode.
            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(opened.Handle).IsNotNull();
            AssertThat(fixture.Host.IsActive(opened.Handle!.Value)).IsTrue();

            // The tree is NOT paused: the PauseTree owner was rejected.
            AssertThat(tree.Paused).IsFalse();

            // The candidate is the top input owner (Modal > Hud).
            AssertThat(fixture.Host.CurrentState.TopInputOwner)
                .IsEqual(opened.Handle);

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            pausableKind.Dispose();
            pauseOwnerKind.Dispose();
            await DisposeFixture(fixture);
        }
    }

    // Regression for the re-entrant TryPresent gap: a TryPresent called from
    // an EffectiveStateChanged (or other Recompute) callback runs while
    // _recomputeDepth > 0. Its own suppressed/commit Recompute passes hit the
    // re-entrant guard and only mark _recomputePending, so pause, blocking,
    // cursor/HUD, lower-layer effects and CurrentState are NOT committed at
    // the nested return boundary — yet TryPresent returns Opened. The caller
    // receives a handle whose effects are uncommitted. The contract should
    // reject the re-entrant open with HostMutating (the same status returned
    // for a TryPresent during close draining) so the caller retries outside
    // the recompute transaction.
    //
    // NOTE: this pins the desired HostMutating behaviour. The existing test
    // TryPresent_SubscriberOpensPauseTreeOwnerAfterCommit (which expects
    // Opened from the same re-entrant path) is incompatible with this
    // direction and must be updated/removed when the fix lands.
    [TestCase]
    public async Task ReentrantTryPresent_DuringEffectiveStateChangedPublication_ReturnsHostMutating()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplayView = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });
        var reentrantView = fixture.Track(new Control { Visible = true });

        UIScreenOpenResult? reentrantResult = null;
        int? activeCountAtNestedReturn = null;
        bool? treePausedAtNestedReturn = null;
        var subscriberFired = false;

        // The subscriber fires during the modal's commit publication Recompute
        // (TryPresent's tail Recompute, _recomputeDepth > 0). It attempts a
        // re-entrant open and captures the result plus the host state BEFORE
        // the outer Recompute restarts and commits the re-entrant open's
        // effects.
        fixture.Host.EffectiveStateChanged += state =>
        {
            if (subscriberFired || state.TopInputOwner?.Kind != UIScreenKinds.Pause)
                return;
            subscriberFired = true;
            reentrantResult = fixture.Host.TryPresent(
                reentrantView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    ProcessPolicy = UIProcessPolicy.Always,
                    PauseTree = true
                });
            activeCountAtNestedReturn = fixture.Host.ActiveEntries.Count;
            treePausedAtNestedReturn = tree.Paused;
        };
        try
        {
            var gameplay = fixture.Host.TryPresent(
                gameplayView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = gameplayView.SetProcessInput
                });
            AssertThat(gameplay.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayView.SetProcessInput(true);

            var modal = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true
                });
            AssertThat(modal.Status).IsEqual(UIScreenOpenStatus.Opened);

            // The subscriber fired during the modal's commit publication and
            // attempted a re-entrant open. The re-entrant TryPresent must
            // return HostMutating, not Opened: its recompute passes only mark
            // _recomputePending, so the PauseTree owner's effects are not
            // committed at the nested return boundary.
            AssertThat(subscriberFired).IsTrue();
            AssertThat(reentrantResult).IsNotNull();
            AssertThat(reentrantResult!.Value.Status)
                .IsEqual(UIScreenOpenStatus.HostMutating);

            // A HostMutating result must not have committed the re-entrant
            // entry: only gameplay + the modal remain, and the tree is not
            // paused at the nested return. (Currently the re-entrant open
            // returns Opened, commits the entry to the model, and leaves the
            // tree un-paused at the nested return — the symptom that motivates
            // rejecting the re-entrant open.)
            AssertThat(activeCountAtNestedReturn!.Value).IsEqual(2);
            AssertThat(treePausedAtNestedReturn!.Value).IsFalse();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            await DisposeFixture(fixture);
        }
    }

    // Regression for the publication-cursor gap: _lastPublishedState is
    // advanced (Recompute, UIScreenHost.cs) BEFORE the complete
    // EffectiveStateChanged subscriber list is delivered. If an early
    // subscriber performs an effective-state-neutral mutation — here, closing
    // a Passive toast that contributes nothing to the resolved state —
    // MutationGeneration bumps, InvokeEffectiveStateChanged aborts the
    // remaining subscribers, and the restarted Recompute resolves an
    // identical state. Because _lastPublishedState was already advanced to
    // that state, previousPublishedState == nextState and no publication
    // fires, so the aborted later subscribers are never retried. The fix must
    // preserve an in-progress publication cursor and resume the remaining
    // subscribers when the recomputed state is unchanged.
    [TestCase]
    public async Task EffectiveStateChanged_EarlySubscriberStateNeutralMutation_LaterSubscriberStillInvoked()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var gameplayView = fixture.Track(new Control { Visible = true });
        var toastView = fixture.Track(new Control { Visible = true });
        var modalView = fixture.Track(new Control { Visible = true });

        var earlyFired = false;
        var laterFired = false;
        UIScreenEffectiveState? stateSeenByLater = null;

        try
        {
            var gameplay = fixture.Host.TryPresent(
                gameplayView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Battle) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetInteractive = gameplayView.SetProcessInput
                });
            AssertThat(gameplay.Status).IsEqual(UIScreenOpenStatus.Opened);
            gameplayView.SetProcessInput(true);

            // A Passive toast on ToastLayer. It is state-neutral: Passive
            // priority is skipped by the resolver for TopInputOwner, it has no
            // PauseTree/BlockGameplayInput, and its LowerLayers default to
            // VisibleInteractive (no inerting). Opening it bumps
            // MutationGeneration but does not change the resolved effective
            // state, so its own open publishes nothing.
            var toast = fixture.Host.TryPresent(
                toastView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
                {
                    Layer = UIScreenLayer.Toast,
                    InputPriority = UIInputPriority.Passive
                });
            AssertThat(toast.Status).IsEqual(UIScreenOpenStatus.Opened);

            // Two subscribers. The early one performs a state-neutral
            // mutation (closing the Passive toast) on the first publication it
            // sees; the later one records that it was invoked.
            fixture.Host.EffectiveStateChanged += state =>
            {
                if (!earlyFired)
                {
                    earlyFired = true;
                    // State-neutral mutation: closing the Passive toast bumps
                    // MutationGeneration without changing the resolved
                    // effective state.
                    fixture.Host.TryClose(
                        toast.Handle!.Value,
                        UIScreenCloseReason.Programmatic);
                    return;
                }
            };
            fixture.Host.EffectiveStateChanged += state =>
            {
                if (laterFired)
                    return;
                laterFired = true;
                stateSeenByLater = state;
            };

            // Opening the modal changes TopInputOwner (Blocking outranks
            // Screen) and BlockGameplayInput, so a publication fires. The
            // early subscriber runs first and closes the Passive toast
            // (state-neutral). Currently the generation bump aborts the later
            // subscriber and the restarted Recompute resolves an identical
            // state, so the later subscriber is never invoked. The fix must
            // resume the remaining subscribers when the recomputed state is
            // unchanged.
            var modal = fixture.Host.TryPresent(
                modalView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    LowerLayers = UILowerLayerPolicy.VisibleInert,
                    BlockGameplayInput = true
                });
            AssertThat(modal.Status).IsEqual(UIScreenOpenStatus.Opened);

            AssertThat(earlyFired).IsTrue();
            AssertThat(laterFired).IsTrue();
            AssertThat(stateSeenByLater).IsNotNull();
            AssertThat(stateSeenByLater!.TopInputOwner).IsEqual(modal.Handle);
            AssertThat(stateSeenByLater.IsPresentationGameplayBlocked).IsTrue();

            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
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

    // Opens a same-layer owner with VisibleInert from _Ready(), disabling
    // input before the open (so the owner's initial validation passes) and
    // re-enabling it after (so the candidate cannot actually be inerted
    // without a SetInteractive adapter). Used to test post-Apply()
    // revalidation of same-layer owners.
    private sealed partial class OpensSameLayerInertingOwnerOnReadyControl : Control
    {
        public UIScreenHost Host { get; set; } = null!;
        public Control OwnerView { get; set; } = null!;
        public UIScreenOpenResult OwnerResult { get; private set; }

        public override void _Ready()
        {
            SetProcessInput(false);
            OwnerResult = Host.TryPresent(
                OwnerView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                });
            SetProcessInput(true);
        }
    }

    // Opens a higher-layer entry with VisibleInteractive from _Ready(),
    // causing a model mutation that triggers post-Apply() revalidation.
    // Used to test that a candidate declaring VisibleInert is not falsely
    // rejected by self-comparison during revalidation.
    private sealed partial class OpensHigherLayerEntryOnReadyControl : Control
    {
        public UIScreenHost Host { get; set; } = null!;
        public Control UpperView { get; set; } = null!;
        public UIScreenOpenResult UpperResult { get; private set; }

        public override void _Ready()
        {
            UpperResult = Host.TryPresent(
                UpperView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.VisibleInteractive
                });
        }
    }
}
