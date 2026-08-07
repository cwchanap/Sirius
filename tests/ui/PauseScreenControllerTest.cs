using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PauseScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/PauseScreen.tscn";

    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public void Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [TestCase]
    public async Task SceneExposesSixActionsAndResumeFocus()
    {
        // This catches an authored action button that is missing its matching
        // presentation signal binding, or a host that cannot restore Resume focus.
        var fixture = await InstantiatePause(new Vector2I(1280, 720));
        try
        {
            var pause = fixture.Pause;
            AssertThat(pause.InitialFocusTarget)
                .IsEqual(pause.GetNode<Button>("%ResumeButton"));

            int resume = 0, inventory = 0, save = 0, load = 0, settings = 0, title = 0;
            pause.ResumeRequested += () => resume++;
            pause.InventoryRequested += () => inventory++;
            pause.SaveRequested += () => save++;
            pause.LoadRequested += () => load++;
            pause.SettingsRequested += () => settings++;
            pause.ReturnToTitleRequested += () => title++;

            pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
            pause.GetNode<Button>("%InventoryButton").EmitSignal(Button.SignalName.Pressed);
            pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
            pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
            pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
            pause.GetNode<Button>("%ReturnToTitleButton").EmitSignal(Button.SignalName.Pressed);

            AssertThat(new[] { resume, inventory, save, load, settings, title })
                .ContainsExactly(1, 1, 1, 1, 1, 1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase(1280, 720)]
    [TestCase(640, 360)]
    public async Task SceneButtonsMeetSharedMinimumTargetsAtFocusViewports(int width, int height)
    {
        // This catches a scene that gives one action a smaller hit target than
        // the shared responsive accessibility metric.
        var fixture = await InstantiatePause(new Vector2I(width, height));
        try
        {
            var compact = SiriusUiMetrics.IsCompact(fixture.Viewport.Size);
            var minimum = SiriusUiMetrics.MinimumTarget(compact);

            foreach (var button in GetActionButtons(fixture.Pause))
                AssertThat(button.CustomMinimumSize.Y).IsGreaterEqual(minimum.Y);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ResizeAfterReady_UpdatesModalShellCompactPresentation()
    {
        // This catches a controller that refreshes its shared layout metrics
        // only in _Ready and leaves a resized pause screen in desktop mode.
        var fixture = await InstantiatePause(new Vector2I(1280, 720));
        try
        {
            var shell = fixture.Pause.GetNode<SiriusModalShell>("%ModalShell");
            AssertThat(shell.Compact).IsFalse();

            fixture.Container.Size = new Vector2(640, 360);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            AssertThat(fixture.Viewport.Size).IsEqual(new Vector2I(640, 360));
            AssertThat(shell.Compact).IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    private async Task<PauseFixture> InstantiatePause(Vector2I size)
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
            return null!;

        var pause = scene.Instantiate<PauseScreenController>();
        viewport.AddChild(pause);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        return new PauseFixture(container, viewport, pause);
    }

    private async Task FreeAsync(PauseFixture fixture)
    {
        if (GodotObject.IsInstanceValid(fixture.Container))
            fixture.Container.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static Button[] GetActionButtons(PauseScreenController pause) => new[]
    {
        pause.GetNode<Button>("%ResumeButton"),
        pause.GetNode<Button>("%InventoryButton"),
        pause.GetNode<Button>("%SaveButton"),
        pause.GetNode<Button>("%LoadButton"),
        pause.GetNode<Button>("%SettingsButton"),
        pause.GetNode<Button>("%ReturnToTitleButton")
    };

    private sealed record PauseFixture(
        SubViewportContainer Container,
        SubViewport Viewport,
        PauseScreenController Pause);
}
