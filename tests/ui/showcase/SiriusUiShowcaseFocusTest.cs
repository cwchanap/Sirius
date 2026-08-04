using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiShowcaseFocusTest : Node
{
    private const string ScenePath = "res://scenes/ui/showcase/SiriusUiShowcase.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusUiShowcase _showcase = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _showcase = scene.Instantiate<SiriusUiShowcase>();
        _sceneTree.Root.AddChild(_showcase);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_showcase))
            _showcase.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase(640, 360)]
    [TestCase(1280, 720)]
    public async Task UiFocusNext_StaysInsidePreviewIncludesSelectionAndLoopsToFirst(int width, int height)
    {
        _showcase.SetPreviewSize(new Vector2I(width, height));
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var first = _showcase.GetNode<Button>("%FocusFirstFixture");
        var selected = _showcase.GetNode<Button>("%SelectedFocusedFixture");
        var last = _showcase.GetNode<Button>("%FocusLastFixture");
        AssertThat(first.FocusNext.ToString()).IsNotEmpty();
        AssertThat(selected.FocusNext.ToString()).IsNotEmpty();
        AssertThat(last.FocusNext.ToString()).IsNotEmpty();
        first.GrabFocus();
        AssertThat(_showcase.PreviewViewport.GuiGetFocusOwner()).IsEqual(first);

        var visited = new List<Control>();
        for (var i = 0; i < 3; i++)
        {
            _showcase.PreviewViewport.PushInput(new InputEventAction
            {
                Action = "ui_focus_next",
                Pressed = true
            });
            _showcase.PreviewViewport.PushInput(new InputEventAction
            {
                Action = "ui_focus_next",
                Pressed = false
            });
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var owner = _showcase.PreviewViewport.GuiGetFocusOwner();
            AssertThat(owner).IsNotNull();
            if (owner is not null)
            {
                AssertThat(_showcase.PreviewRoot.IsAncestorOf(owner)).IsTrue();
                visited.Add(owner);
            }
        }

        AssertThat(visited[0]).IsEqual(selected);
        AssertThat(visited[1]).IsEqual(last);
        AssertThat(visited[2]).IsEqual(first);
        AssertThat(selected.ButtonPressed).IsTrue();
    }
}
