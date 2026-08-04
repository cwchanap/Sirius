using GdUnit4;
using Godot;
using System;
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

    [TestCase]
    public void AuthoredFocusScope_AllowsOnlyTheExplicitThreeControlLoop()
    {
        var sceneSource = FileAccess.GetFileAsString(ScenePath);
        var first = _showcase.GetNode<Button>("%FocusFirstFixture");
        var selected = _showcase.GetNode<Button>("%SelectedFocusedFixture");
        var last = _showcase.GetNode<Button>("%FocusLastFixture");
        var nativeTabs = _showcase.GetNode<TabContainer>("%NativeTabs");

        AssertThat(first.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(selected.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(last.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(first.FocusNext.ToString()).IsEqual("../SelectedFocusedFixture");
        AssertThat(first.FocusPrevious.ToString()).IsEqual("../FocusLastFixture");
        AssertThat(selected.FocusNext.ToString()).IsEqual("../FocusLastFixture");
        AssertThat(selected.FocusPrevious.ToString()).IsEqual("../FocusFirstFixture");
        AssertThat(last.FocusNext.ToString()).IsEqual("../FocusFirstFixture");
        AssertThat(last.FocusPrevious.ToString()).IsEqual("../SelectedFocusedFixture");

        AssertSceneNodeProperty(sceneSource, "FocusFirstFixture", "focus_mode = 2");
        AssertSceneNodeProperty(sceneSource, "SelectedFocusedFixture", "focus_mode = 2");
        AssertSceneNodeProperty(sceneSource, "FocusLastFixture", "focus_mode = 2");

        string[] staticPreviewButtonNames =
        {
            "PrimaryButtonFixture", "SecondaryButtonFixture", "TertiaryButtonFixture",
            "WarningButtonFixture", "DestructiveButtonFixture", "LoadingFixture",
            "IgnitionStandardFixture", "IgnitionCompactFixture", "MotionPlayButton",
            "StressAction"
        };
        foreach (var buttonName in staticPreviewButtonNames)
        {
            AssertSceneNodeProperty(sceneSource, buttonName, "focus_mode = 0");
            AssertThat(_showcase.GetNode<Button>($"%{buttonName}").FocusMode)
                .IsEqual(Control.FocusModeEnum.None);
        }

        var tooltipButton = _showcase.FindChild("TooltipButton", true, false) as Button;
        AssertThat(tooltipButton).IsNotNull();
        AssertSceneNodeProperty(sceneSource, "TooltipButton", "focus_mode = 0");
        AssertThat(tooltipButton!.FocusMode).IsEqual(Control.FocusModeEnum.None);
        AssertSceneNodeProperty(sceneSource, "NativeTabs", "focus_mode = 0");
        AssertThat(nativeTabs.GetTabBar().FocusMode).IsEqual(Control.FocusModeEnum.None);
    }

    private static void AssertSceneNodeProperty(
        string sceneSource,
        string nodeName,
        string expectedProperty)
    {
        var nodeStart = sceneSource.IndexOf($"[node name=\"{nodeName}\"", StringComparison.Ordinal);
        AssertThat(nodeStart >= 0).IsTrue();
        if (nodeStart < 0)
            return;

        var nextNode = sceneSource.IndexOf("\n[node ", nodeStart + 1, StringComparison.Ordinal);
        var nodeEnd = nextNode >= 0 ? nextNode : sceneSource.Length;
        var nodeSource = sceneSource.Substring(nodeStart, nodeEnd - nodeStart);
        AssertThat(nodeSource.Contains(expectedProperty, StringComparison.Ordinal)).IsTrue();
    }
}
