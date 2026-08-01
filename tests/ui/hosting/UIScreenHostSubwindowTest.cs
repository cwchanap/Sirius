using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostSubwindowTest : Node
{
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
