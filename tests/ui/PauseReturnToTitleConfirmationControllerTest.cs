using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PauseReturnToTitleConfirmationControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/PauseReturnToTitleConfirmation.tscn";

    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public void Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [TestCase]
    public async Task Scene_ExposesCancelFocusAndEmitsOnePresentationSignalPerActionPress()
    {
        // This catches missing or reversed action bindings, a missing safe
        // Cancel focus target, and an out-of-scope one-shot guard that drops
        // a later presentation signal before Game receives it.
        var fixture = await InstantiateConfirmation(new Vector2I(1280, 720));
        if (fixture is null)
            return;

        try
        {
            var confirmation = fixture.Confirmation;
            var cancel = confirmation.GetNode<Button>("%CancelButton");
            var returnToTitle = confirmation.GetNode<Button>("%ReturnToTitleButton");
            AssertThat(confirmation.InitialFocusTarget).IsEqual(cancel);

            var returns = 0;
            var cancels = 0;
            confirmation.ReturnToTitleConfirmed += () => returns++;
            confirmation.CancelRequested += () => cancels++;

            returnToTitle.EmitSignal(Button.SignalName.Pressed);
            AssertThat(returns).IsEqual(1);
            AssertThat(cancels).IsEqual(0);

            returnToTitle.EmitSignal(Button.SignalName.Pressed);
            AssertThat(returns).IsEqual(2);

            cancel.EmitSignal(Button.SignalName.Pressed);
            AssertThat(cancels).IsEqual(1);
            AssertThat(returns).IsEqual(2);

            cancel.EmitSignal(Button.SignalName.Pressed);
            AssertThat(cancels).IsEqual(2);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    private async Task<ConfirmationFixture?> InstantiateConfirmation(Vector2I size)
    {
        var container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(container);

        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        container.AddChild(viewport);

        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
        {
            container.QueueFree();
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            return null;
        }

        var confirmation = scene.Instantiate<PauseReturnToTitleConfirmationController>();
        viewport.AddChild(confirmation);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        return new ConfirmationFixture(container, confirmation);
    }

    private async Task FreeAsync(ConfirmationFixture fixture)
    {
        if (GodotObject.IsInstanceValid(fixture.Container))
            fixture.Container.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private sealed record ConfirmationFixture(
        SubViewportContainer Container,
        PauseReturnToTitleConfirmationController Confirmation);
}
