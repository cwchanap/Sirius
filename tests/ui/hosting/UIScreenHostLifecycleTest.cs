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
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(cleanup.ToArray()).ContainsExactly(
                "child:Programmatic",
                "parent:HostTeardown");
            AssertThat(parentView.GetParent()).IsNull();
            AssertThat(childView.GetParent()).IsNull();
        }
        finally
        {
            await DisposeFixture(fixture);
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
            AssertThat(reopenStatus).IsEqual(UIScreenOpenStatus.MalformedHost);
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
