using System.Collections.Generic;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostSubwindowTest : Node
{
    [TestCase]
    public async Task LowerLayerEffects_WindowStrongestOwnerWeakensAndRestoresExactBaseline()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var dialog = fixture.Track(new AcceptDialog
        {
            GuiDisableInput = true,
            Unfocusable = false
        });
        var inertOwner = fixture.Track(new Control());
        var hiddenOwner = fixture.Track(new Control());
        var presentationChanges = new List<bool>();
        fixture.Viewport.GuiEmbedSubwindows = true;
        try
        {
            var target = fixture.Host.TryPresent(
                dialog,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Dialogue) with
                {
                    Layer = UIScreenLayer.Hud,
                    SetPresented = presented =>
                    {
                        presentationChanges.Add(presented);
                        if (presented) dialog.Show(); else dialog.Hide();
                    }
                });
            AssertThat(target.Status).IsEqual(UIScreenOpenStatus.Opened);
            dialog.Show();

            var inert = fixture.Host.TryPresent(
                inertOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.VisibleInert
                }).Handle!.Value;

            AssertThat(dialog.Visible).IsTrue();
            AssertThat(dialog.GuiDisableInput).IsTrue();
            AssertThat(dialog.Unfocusable).IsTrue();

            var hidden = fixture.Host.TryPresent(
                hiddenOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
                {
                    Parent = inert,
                    Layer = UIScreenLayer.Modal,
                    LowerLayers = UILowerLayerPolicy.Hidden
                }).Handle!.Value;

            AssertThat(dialog.Visible).IsFalse();
            AssertThat(dialog.GuiDisableInput).IsTrue();
            AssertThat(dialog.Unfocusable).IsFalse();

            fixture.Host.TryClose(hidden, UIScreenCloseReason.Programmatic);

            AssertThat(dialog.Visible).IsTrue();
            AssertThat(dialog.GuiDisableInput).IsTrue();
            AssertThat(dialog.Unfocusable).IsTrue();

            fixture.Host.TryClose(inert, UIScreenCloseReason.Programmatic);

            AssertThat(dialog.Visible).IsTrue();
            AssertThat(dialog.GuiDisableInput).IsTrue();
            AssertThat(dialog.Unfocusable).IsFalse();
            AssertThat(presentationChanges.ToArray()).ContainsExactly(false, true);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task LowerLayerEffects_HiddenOwnerWithoutRequiredWindowAdapterIsRejectedAtomically()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var dialog = fixture.Track(new AcceptDialog());
        var hiddenOwner = fixture.Track(new Control());
        fixture.Viewport.GuiEmbedSubwindows = true;
        try
        {
            var target = fixture.Host.TryPresent(
                dialog,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Dialogue) with
                {
                    Layer = UIScreenLayer.Hud,
                    IsPresented = () => dialog.Visible
                });
            AssertThat(target.Status).IsEqual(UIScreenOpenStatus.Opened);
            dialog.Show();

            var result = fixture.Host.TryPresent(
                hiddenOwner,
                UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
                {
                    Layer = UIScreenLayer.Screen,
                    LowerLayers = UILowerLayerPolicy.Hidden
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.MissingRequiredAdapter);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
            AssertThat(hiddenOwner.GetParent()).IsNull();
            AssertThat(dialog.Visible).IsTrue();
            AssertThat(dialog.GuiDisableInput).IsFalse();
            AssertThat(dialog.Unfocusable).IsFalse();
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_WindowWithoutEmbedding_IsRejectedWithoutMutation()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var dialog = fixture.Track(new AcceptDialog
        {
            ProcessMode = ProcessModeEnum.Pausable
        });
        fixture.Viewport.GuiEmbedSubwindows = false;
        try
        {
            var result = fixture.Host.TryPresent(
                dialog,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal,
                    ProcessPolicy = UIProcessPolicy.Always
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.UnsupportedSubwindowMode);
            AssertThat(result.Handle).IsNull();
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
            AssertThat(dialog.GetParent()).IsNull();
            AssertThat(dialog.ProcessMode).IsEqual(ProcessModeEnum.Pausable);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_EmbeddedWindow_UsesWindowAsFocusViewport()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var dialog = fixture.Track(new AcceptDialog());
        fixture.Viewport.GuiEmbedSubwindows = true;
        try
        {
            var result = fixture.Host.TryPresent(
                dialog,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal
                });

            AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(dialog.GetParent()).IsEqual(fixture.Host);
            AssertThat(fixture.Host.FocusViewportFor(result.Handle!.Value)).IsEqual(dialog);
            AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(1);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task Present_RejectedPreParentedBlockingWindow_IsSynchronousAtomicNoOp()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        fixture.Viewport.GuiEmbedSubwindows = true;
        var activeView = fixture.Track(new Control());
        var rejectedWindow = fixture.Track(new Window { Visible = true });
        fixture.Host.AddChild(rejectedWindow);
        var cleanupCount = 0;
        try
        {
            var active = fixture.Host.TryPresent(
                activeView,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError));
            AssertThat(active.Status).IsEqual(UIScreenOpenStatus.Opened);
            var childrenBefore = fixture.Host.GetChildren();

            var rejected = fixture.Host.TryPresent(
                rejectedWindow,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Modal,
                    InputPriority = UIInputPriority.Blocking,
                    Cleanup = _ => cleanupCount++
                });

            AssertThat(rejected.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
            AssertThat(rejected.Handle).IsNull();
            AssertThat(fixture.Host.GetChildren().Count).IsEqual(childrenBefore.Count);
            for (var index = 0; index < childrenBefore.Count; index++)
            {
                AssertThat(fixture.Host.GetChild(index)).IsEqual(childrenBefore[index]);
            }
            AssertThat(rejectedWindow.GetChildCount()).IsEqual(0);
            AssertThat(rejectedWindow.GetParent()).IsEqual(fixture.Host);
            AssertThat(cleanupCount).IsEqual(0);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task PresentAndClose_EmbeddedWindowUsesHostProcessContextAndRestoresExactMode()
    {
        var fixture = await UIScreenHostTestSupport.CreateHost(this);
        var dialog = fixture.Track(new AcceptDialog
        {
            ProcessMode = ProcessModeEnum.WhenPaused
        });
        fixture.Viewport.GuiEmbedSubwindows = true;
        try
        {
            var opened = fixture.Host.TryPresent(
                dialog,
                UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
                {
                    Layer = UIScreenLayer.Hud,
                    ProcessPolicy = UIProcessPolicy.InheritHost,
                    PauseTree = true
                });

            AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
            AssertThat(dialog.GetParent()).IsEqual(fixture.Host);
            AssertThat(dialog.ProcessMode).IsEqual(ProcessModeEnum.Inherit);

            var closed = fixture.Host.TryClose(
                opened.Handle!.Value,
                UIScreenCloseReason.Programmatic);

            AssertThat(closed.Status).IsEqual(UIScreenCloseStatus.Closed);
            AssertThat(dialog.GetParent()).IsNull();
            AssertThat(dialog.ProcessMode).IsEqual(ProcessModeEnum.WhenPaused);
        }
        finally
        {
            fixture.Dispose();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
