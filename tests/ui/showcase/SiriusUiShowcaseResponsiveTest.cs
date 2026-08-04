using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiShowcaseResponsiveTest : Node
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

    [TestCase]
    public async Task SetPreviewSize_AppliesTheSameResponsiveContractAtEveryApprovedViewport()
    {
        foreach (var size in SiriusUiMetrics.VerificationViewports)
        {
            _showcase.SetPreviewSize(size);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var compact = SiriusUiMetrics.IsCompact(size);
            var safeMargin = SiriusUiMetrics.SafeMargin(compact);
            var target = SiriusUiMetrics.MinimumTarget(compact);
            var safeFrame = _showcase.GetNode<MarginContainer>("%SafeFrame");
            var content = _showcase.GetNode<VBoxContainer>("%ShowcaseContent");
            var scroll = _showcase.GetNode<ScrollContainer>("%ResponsiveScroll");
            var primary = _showcase.GetNode<Button>("%PrimaryButtonFixture");
            var selected = _showcase.GetNode<Button>("%SelectedFocusedFixture");
            var ignition = _showcase.GetNode<Button>("%IgnitionStandardFixture");
            var body = _showcase.GetNode<Label>("%StressBody");
            var metadata = _showcase.GetNode<Label>("%StressMetadata");

            AssertThat(_showcase.PreviewViewport.Size).IsEqual(size);
            AssertThat(_showcase.Compact).IsEqual(compact);
            AssertThat(safeFrame.GetThemeConstant("margin_left")).IsEqual(safeMargin);
            AssertThat(safeFrame.GetThemeConstant("margin_top")).IsEqual(safeMargin);
            AssertThat(safeFrame.GetThemeConstant("margin_right")).IsEqual(safeMargin);
            AssertThat(safeFrame.GetThemeConstant("margin_bottom")).IsEqual(safeMargin);
            AssertThat(content.CustomMinimumSize.X)
                .IsEqual(Mathf.Min(1600f, size.X - safeMargin * 2f));
            AssertThat(primary.CustomMinimumSize).IsEqual(target);
            AssertThat(selected.CustomMinimumSize).IsEqual(target);
            AssertThat(ignition.CustomMinimumSize).IsEqual(SiriusUiMetrics.IgnitionSize(compact));
            AssertThat(body.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
            AssertThat(metadata.ClipText).IsTrue();
            AssertThat(metadata.TooltipText).IsEqual(metadata.Text);

            scroll.ScrollVertical = 0;
            scroll.EnsureControlVisible(primary);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            AssertThat(primary.GetGlobalRect().Intersects(scroll.GetGlobalRect())).IsTrue();
        }
    }

    [TestCase]
    public async Task CompactStressModal_WrapsLongBodyInsideItsScrollableBodyRegion()
    {
        _showcase.SetPreviewSize(new Vector2I(640, 360));
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var modal = _showcase.GetNode<SiriusModalShell>("%MediumModalFixture");
        var body = _showcase.GetNode<Label>("%StressBody");
        var bodyScroll = modal.GetNode<ScrollContainer>("%BodyScroll");
        AssertThat(body.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(bodyScroll.GetVScrollBar().MaxValue).IsGreater(0d);

        bodyScroll.ScrollVertical = Mathf.CeilToInt((float)bodyScroll.GetVScrollBar().MaxValue);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(bodyScroll.ScrollVertical).IsGreater(0);
    }
}
