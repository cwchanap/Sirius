using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuSceneTest : Node
{
    private const string ScenePath = "res://scenes/ui/SettingsMenu.tscn";

    private static readonly string[] RequiredUniqueNodes =
    {
        "%ModalShell",
        "%SettingsFrame",
        "%PageSelector",
        "%PageDeck",
        "%AudioPageButton",
        "%DisplayPageButton",
        "%GameplayPageButton",
        "%ControlsPageButton",
        "%AudioScroll",
        "%DisplayScroll",
        "%GameplayScroll",
        "%ControlsScroll",
        "%AudioRows",
        "%DisplayRows",
        "%GameplayRows",
        "%ControlsRows",

        "%MasterVolumeLabel",
        "%MasterSlider",
        "%MasterValueLabel",
        "%MusicVolumeLabel",
        "%MusicSlider",
        "%MusicValueLabel",
        "%SfxVolumeLabel",
        "%SfxSlider",
        "%SfxValueLabel",

        "%FullscreenLabel",
        "%FullscreenCheck",
        "%ResolutionLabel",
        "%ResolutionOption",
        "%ReducedMotionLabel",
        "%ReducedMotionCheck",

        "%DifficultyLabel",
        "%DifficultyOption",
        "%AutoSaveLabel",
        "%AutoSaveCheck",

        "%InventoryKeyLabel",
        "%InventoryKeyButton",
        "%InteractKeyLabel",
        "%InteractKeyButton",
        "%PauseKeyLabel",
        "%PauseKeyButton",

        "%ErrorPanel",
        "%ErrorLabel",
        "%ApplyButton",
        "%CancelButton"
    };

    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private SubViewport? _viewport;
    private SettingsMenuController? _screen;

    [BeforeTest]
    public void Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_container != null && GodotObject.IsInstanceValid(_container))
            _container.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        _container = null;
        _viewport = null;
        _screen = null;
    }

    [TestCase]
    public void SceneOwnsSettingsControlsBeforeReady()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        AssertThat(packed).IsNotNull();

        var screen = packed!.Instantiate<SettingsMenuController>();
        try
        {
            foreach (var path in RequiredUniqueNodes)
                AssertThat(screen.GetNodeOrNull(path)).IsNotNull();
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public void ControllerHasNoRuntimeLayoutBuilders()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildUI", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildAudioTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildDisplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildGameplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildControlsTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddSliderRow", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddKeyRow", flags)).IsNull();
    }

    [TestCase]
    public async Task StandardViewportUsesLeftRailAndTwoColumnRows()
    {
        // Catches a responsive layout that stays in compact reflow at a
        // standard desktop viewport.
        await ResizeAndOpen(new Vector2I(1280, 720));

        AssertThat(_screen!.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(2);
        AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(1);
        AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(2);
        AssertThat(_screen.GetNode<GridContainer>("%DisplayRows").Columns).IsEqual(2);
        AssertThat(_screen.GetNode<GridContainer>("%GameplayRows").Columns).IsEqual(2);
        AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(2);
    }

    [TestCase]
    public async Task MinimumViewportUsesTopSelectorAndSingleColumnRows()
    {
        // Catches a settings panel that preserves the desktop rail and
        // two-column controls when the compact layout is required.
        await ResizeAndOpen(new Vector2I(640, 360));

        AssertThat(_screen!.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(1);
        AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(4);
        AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(1);
        AssertThat(_screen.GetNode<GridContainer>("%DisplayRows").Columns).IsEqual(1);
        AssertThat(_screen.GetNode<GridContainer>("%GameplayRows").Columns).IsEqual(1);
        AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(1);
    }

    [TestCase]
    public async Task SettingsDisablesShellScrollAndKeepsPageScrollAuto()
    {
        // Catches overflow that is owned by the shared modal shell instead
        // of the selected settings page.
        await ResizeAndOpen(new Vector2I(640, 360));

        var shell = _screen!.GetNode<SiriusModalShell>("%ModalShell");
        var shellScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
        var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");

        AssertThat(shellScroll.VerticalScrollMode)
            .IsEqual(ScrollContainer.ScrollMode.Disabled);
        AssertThat(shellScroll.HorizontalScrollMode)
            .IsEqual(ScrollContainer.ScrollMode.Disabled);
        AssertThat(controlsScroll.VerticalScrollMode)
            .IsEqual(ScrollContainer.ScrollMode.Auto);

        // follow_focus must be enabled on every page scroller so keyboard/
        // gamepad navigation scrolls focused controls into view. A disabled
        // value on any page scroller breaks focus-driven scrolling on that
        // page, so assert all four rather than only the Controls page.
        AssertThat(_screen.GetNode<ScrollContainer>("%AudioScroll").FollowFocus).IsTrue();
        AssertThat(_screen.GetNode<ScrollContainer>("%DisplayScroll").FollowFocus).IsTrue();
        AssertThat(_screen.GetNode<ScrollContainer>("%GameplayScroll").FollowFocus).IsTrue();
        AssertThat(_screen.GetNode<ScrollContainer>("%ControlsScroll").FollowFocus).IsTrue();
    }

    [TestCase]
    public async Task PageButtonSelectsOneTabAndButtonGroupKeepsExclusivity()
    {
        // Catches a selector button that changes neither the deck nor its
        // mutually-exclusive pressed state.
        await ResizeAndOpen(new Vector2I(1280, 720));

        var deck = _screen!.GetNode<TabContainer>("%PageDeck");
        var audio = _screen.GetNode<Button>("%AudioPageButton");
        var controls = _screen.GetNode<Button>("%ControlsPageButton");

        controls.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(deck.CurrentTab).IsEqual(3);
        AssertThat(controls.ButtonPressed).IsTrue();
        AssertThat(audio.ButtonPressed).IsFalse();
    }

    [TestCase(640, 360)]
    [TestCase(1024, 768)]
    [TestCase(1280, 720)]
    [TestCase(1440, 900)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    [TestCase(2560, 1440)]
    public async Task ApprovedViewportKeepsSettingsPanelInsideViewport(int width, int height)
    {
        // Catches a settings content minimum size that grows the shared shell
        // outside an approved viewport.
        await ResizeAndOpen(new Vector2I(width, height));

        var panel = _screen!.GetNode<PanelContainer>("ModalShell/Panel");
        var panelRect = panel.GetGlobalRect();

        AssertThat(panelRect.Position.X).IsGreaterEqual(0f);
        AssertThat(panelRect.Position.Y).IsGreaterEqual(0f);
        AssertThat(panelRect.End.X).IsLessEqual(width + 0.5f);
        AssertThat(panelRect.End.Y).IsLessEqual(height + 0.5f);
    }

    [TestCase]
    public async Task LongCompactControlsLabelUsesPageScrollWithoutGrowingShell()
    {
        // Catches a localized label that expands the modal shell instead of
        // extending the selected page's own scroll range.
        await ResizeAndOpen(new Vector2I(640, 360));

        _screen!.GetNode<Button>("%ControlsPageButton")
            .EmitSignal(Button.SignalName.Pressed);

        var label = _screen.GetNode<Label>("%InventoryKeyLabel");
        label.Text = string.Join(" ", Enumerable.Repeat(
            "RepresentativeLocalizedInventoryBindingLabel", 12));

        await AwaitFrames(3);

        var shell = _screen.GetNode<SiriusModalShell>("%ModalShell");
        var shellScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
        var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");
        var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");

        AssertThat(shellScroll.ScrollVertical).IsEqual(0);
        AssertThat(controlsScroll.GetVScrollBar().MaxValue)
            .IsGreater(controlsScroll.GetVScrollBar().Page);
        AssertThat(panel.GetGlobalRect().End.Y).IsLessEqual(360.5f);
    }

    [TestCase]
    public async Task CompactControlsFocusScrollsLastControlIntoView()
    {
        // Catches a page-local ScrollContainer that lets keyboard/gamepad
        // navigation move focus onto an off-screen setting without scrolling
        // it into view (follow_focus must be enabled on every page scroller).
        await ResizeAndOpen(new Vector2I(640, 360));

        _screen!.GetNode<Button>("%ControlsPageButton")
            .EmitSignal(Button.SignalName.Pressed);

        var label = _screen.GetNode<Label>("%InventoryKeyLabel");
        label.Text = string.Join(" ", Enumerable.Repeat(
            "RepresentativeLocalizedInventoryBindingLabel", 12));

        await AwaitFrames(3);

        var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");
        var pauseButton = _screen.GetNode<Button>("%PauseKeyButton");

        // Establish that the page content overflows the local scroller and
        // that the last control starts below the visible viewport.
        AssertThat(controlsScroll.GetVScrollBar().MaxValue)
            .IsGreater(controlsScroll.GetVScrollBar().Page);
        AssertThat(controlsScroll.ScrollVertical).IsEqual(0);

        var scrollBefore = controlsScroll.ScrollVertical;

        // Focus the last control — follow_focus should scroll it into view.
        pauseButton.GrabFocus();
        await AwaitFrames(3);

        // follow_focus only needs to scroll enough to make the focused
        // control visible; it does not guarantee the scroller reaches its
        // maximum range (content height varies with font/theme metrics
        // across CI environments, so asserting ScrollVertical >= maxScroll
        // is flaky). Assert the scroller moved off its initial position...
        AssertThat(controlsScroll.ScrollVertical).IsGreater(scrollBefore);

        // ...and the focused control is now fully visible within the scroll
        // viewport, per the follow_focus contract (Godot scrolls focused
        // descendants so they are fully visible). When the control is
        // shorter than the viewport, the full control height must be
        // visible; when the control is taller than the viewport (compact
        // page scroller), the full viewport height must be covered by the
        // control. Either way the visible intersection height equals
        // min(button height, viewport height), which avoids depending on
        // font/content height while still rejecting a 1px-only overlap.
        var scrollRect = controlsScroll.GetGlobalRect();
        var buttonRect = pauseButton.GetGlobalRect();
        var visibleTop = Mathf.Max(buttonRect.Position.Y, scrollRect.Position.Y);
        var visibleBottom = Mathf.Min(buttonRect.End.Y, scrollRect.End.Y);
        var visibleHeight = Mathf.Max(0f, visibleBottom - visibleTop);
        var expectedVisibleHeight = Mathf.Min(buttonRect.Size.Y, scrollRect.Size.Y);
        AssertThat(visibleHeight).IsGreaterEqual(expectedVisibleHeight - 1f);
    }

    [TestCase]
    public async Task StandardToCompactResizeKeepsShellBoundsAndPageLocalOverflow()
    {
        // This is intentionally one in-place resize: the shared shell child
        // is ready before SettingsMenuController claims page-local scroll
        // ownership, so a stale outer minimum can survive the transition.
        await ResizeAndOpen(new Vector2I(1280, 720), stretch: false);

        var shell = _screen!.GetNode<SiriusModalShell>("%ModalShell");
        var shellScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
        var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");
        var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");

        var standardRect = panel.GetGlobalRect();
        AssertThat(standardRect.Position.X).IsGreaterEqual(0f);
        AssertThat(standardRect.Position.Y).IsGreaterEqual(0f);
        AssertThat(standardRect.End.X).IsLessEqual(1280.5f);
        AssertThat(standardRect.End.Y).IsLessEqual(720.5f);

        _screen.GetNode<Button>("%ControlsPageButton")
            .EmitSignal(Button.SignalName.Pressed);
        _screen.GetNode<Label>("%InventoryKeyLabel").Text = string.Join(
            " ", Enumerable.Repeat("RepresentativeLocalizedInventoryBindingLabel", 12));

        _container!.Size = new Vector2(640, 360);
        _viewport!.Size = new Vector2I(640, 360);
        await AwaitFrames(3);

        var compactRect = panel.GetGlobalRect();
        GD.Print(
            $"[SettingsMenuSceneTest] 1280x720->640x360 panel={compactRect} " +
            $"outerMax={shellScroll.GetVScrollBar().MaxValue:F1} " +
            $"pageMax={controlsScroll.GetVScrollBar().MaxValue:F1} " +
            $"page={controlsScroll.GetVScrollBar().Page:F1}");

        AssertThat(compactRect.Position.X).IsGreaterEqual(0f);
        AssertThat(compactRect.Position.Y).IsGreaterEqual(0f);
        AssertThat(compactRect.End.X).IsLessEqual(640.5f);
        AssertThat(compactRect.End.Y).IsLessEqual(360.5f);
        AssertThat(shellScroll.VerticalScrollMode)
            .IsEqual(ScrollContainer.ScrollMode.Disabled);
        AssertThat(shellScroll.HorizontalScrollMode)
            .IsEqual(ScrollContainer.ScrollMode.Disabled);
        AssertThat(shellScroll.ScrollVertical).IsEqual(0);
        AssertThat(controlsScroll.GetVScrollBar().MaxValue)
            .IsGreater(controlsScroll.GetVScrollBar().Page);
    }

    private async Task ResizeAndOpen(Vector2I size, bool stretch = true)
    {
        _container = new SubViewportContainer
        {
            Size = size,
            Stretch = stretch
        };
        _sceneTree.Root.AddChild(_container);

        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        _container.AddChild(viewport);
        _viewport = viewport;

        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _screen = scene.Instantiate<SettingsMenuController>();
        viewport.AddChild(_screen);
        await AwaitFrames(1);

        _screen.OpenSettings(SettingsData.CreateDefaults());
        await AwaitFrames(1);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
