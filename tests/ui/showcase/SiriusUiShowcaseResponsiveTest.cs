using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiShowcaseResponsiveTest : Node
{
    private SceneTree _sceneTree = null!;
    private SiriusUiShowcase _showcase = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _showcase = await SiriusUiShowcaseTestHarness.InstantiateAsync(_sceneTree);
    }

    [AfterTest]
    public async Task Cleanup()
        => await SiriusUiShowcaseTestHarness.FreeAsync(_sceneTree, _showcase);

    [TestCase(640, 360)]
    [TestCase(1024, 768)]
    [TestCase(1280, 720)]
    [TestCase(1440, 900)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    [TestCase(2560, 1440)]
    public async Task SetPreviewSize_AppliesTheSameResponsiveContractAtEveryApprovedViewport(int width, int height)
    {
        var size = new Vector2I(width, height);
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
        var metadataFixture = _showcase.GetNode<Control>("%MetadataFixture");
        var tooltipMaximum = SiriusUiMetrics.TooltipMaximum(compact);

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
        AssertThat(metadataFixture.Size.X).IsEqualApprox(tooltipMaximum, 0.001f);
        AssertThat(metadata.Size.X).IsEqualApprox(tooltipMaximum, 0.001f);
        var metadataFont = metadata.GetThemeFont("font");
        AssertThat(metadataFont).IsNotNull();
        if (metadataFont is not null)
        {
            var fullMetadataWidth = metadataFont.GetStringSize(
                metadata.Text,
                HorizontalAlignment.Left,
                -1,
                metadata.GetThemeFontSize("font_size")).X;
            AssertThat(fullMetadataWidth).IsGreater(metadata.Size.X);
        }

        scroll.ScrollVertical = 0;
        scroll.EnsureControlVisible(primary);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(primary.GetGlobalRect().Intersects(scroll.GetGlobalRect())).IsTrue();
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
