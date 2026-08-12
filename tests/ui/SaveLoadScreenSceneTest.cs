using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveLoadScreenSceneTest : Node
{
    private const string ScenePath = "res://scenes/ui/SaveLoadScreen.tscn";
    private static readonly string[] CardNames = { "%Slot0Card", "%Slot1Card", "%Slot2Card", "%Slot3Card" };

    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private SubViewport? _viewport;
    private SaveLoadScreenController? _screen;
    private TestHelpers.SaveFileSnapshot[] _saveFiles = null!;

    [BeforeTest]
    public void SetUp()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _saveFiles = TestHelpers.CaptureSaveFiles();
    }

    [AfterTest]
    public async Task TearDown()
    {
        try
        {
            if (_screen != null && GodotObject.IsInstanceValid(_screen))
                _screen.QueueFree();
            if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
                _viewport.QueueFree();
            if (_container != null && GodotObject.IsInstanceValid(_container))
                _container.QueueFree();

            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            TestHelpers.RestoreSaveFiles(_saveFiles);
            TestHelpers.ReportSaveFileMismatches(
                _saveFiles,
                nameof(SaveLoadScreenSceneTest));
            _screen = null;
            _viewport = null;
            _container = null;
        }
    }

    [TestCase]
    public void Scene_UsesOneScriptlessAuthoredCardStructure()
    {
        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        var screen = scene.Instantiate<SaveLoadScreenController>();
        try
        {
            foreach (var name in CardNames)
            {
                var card = screen.GetNode<Button>(name);
                AssertThat(card.GetNodeOrNull<Label>("Margin/Content/SlotNameLabel")).IsNotNull();
                AssertThat(card.GetNodeOrNull<Label>("Margin/Content/DetailLabel")).IsNotNull();
                AssertThat(card.GetNodeOrNull<Label>("Margin/Content/TimestampLabel")).IsNotNull();
                AssertThat(card.GetNodeOrNull<Label>("Margin/Content/StateLabel")).IsNotNull();
                AssertThat(card.GetNodeOrNull<Label>("Margin/Content/ActionLabel")).IsNotNull();
                AssertThat(card.GetScript().VariantType).IsEqual(Variant.Type.Nil);
            }
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task StandardViewport_UsesTwoColumnCards()
    {
        await ResizeAndCreate(new Vector2I(1280, 720));

        var grid = _screen!.GetNode<GridContainer>("%CardsGrid");
        GD.Print($"[SaveLoadScreenSceneTest] 1280x720 columns={grid.Columns}");
        AssertThat(grid.Columns).IsEqual(2);
    }

    [TestCase]
    public async Task CompactViewport_UsesSingleColumnCards()
    {
        await ResizeAndCreate(new Vector2I(640, 360));

        var grid = _screen!.GetNode<GridContainer>("%CardsGrid");
        GD.Print($"[SaveLoadScreenSceneTest] 640x360 columns={grid.Columns}");
        AssertThat(grid.Columns).IsEqual(1);
    }

    [TestCase]
    public async Task AllCardsHaveTargetSizeAndPanelStaysInsideCompactViewport()
    {
        await ResizeAndCreate(new Vector2I(640, 360));

        var panel = _screen!.GetNode<PanelContainer>("ModalShell/Panel");
        var panelRect = panel.GetGlobalRect();
        AssertThat(panelRect.Position.X).IsGreaterEqual(0f);
        AssertThat(panelRect.Position.Y).IsGreaterEqual(0f);
        AssertThat(panelRect.End.X).IsLessEqual(640.5f);
        AssertThat(panelRect.End.Y).IsLessEqual(360.5f);

        foreach (var name in CardNames)
        {
            var card = _screen.GetNode<Button>(name);
            AssertThat(card.Size.X).IsGreater(0f);
            AssertThat(card.Size.Y).IsGreaterEqual(SiriusUiMetrics.MinimumTarget(true).Y);
        }

        var card0 = _screen.GetNode<Button>("%Slot0Card");
        GD.Print(
            $"[SaveLoadScreenSceneTest] 640x360 panel={panelRect} " +
            $"card0={card0.Size} minimumTarget={SiriusUiMetrics.MinimumTarget(true)}");
    }

    [TestCase]
    public async Task CompactViewport_ShellBodyScrollsStackedCardsAndKeepsChromeVisible()
    {
        await ResizeAndCreate(new Vector2I(640, 360));

        var shell = _screen!.GetNode<SiriusModalShell>("%ModalShell");
        var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
        var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");
        var title = shell.GetNode<Label>("%TitleLabel");
        var cancel = _screen.GetNode<Button>("%CancelButton");
        var scrollBar = bodyScroll.GetVScrollBar();

        GD.Print(
            $"[SaveLoadScreenSceneTest] 640x360 scroll page={scrollBar.Page:F1} " +
            $"max={scrollBar.MaxValue:F1} title={title.GetGlobalRect()} " +
            $"footer={cancel.GetGlobalRect()}");
        AssertThat(scrollBar.MaxValue).IsGreater(scrollBar.Page);
        AssertThat(title.GetGlobalRect().Position.Y).IsGreaterEqual(0f);
        AssertThat(cancel.GetGlobalRect().End.Y).IsLessEqual(panel.GetGlobalRect().End.Y + 0.5f);
        AssertThat(cancel.GetGlobalRect().End.Y).IsLessEqual(360.5f);
    }

    [TestCase]
    public async Task LongIncompatibleReason_WrapsInsideCard()
    {
        await ResizeAndCreate(new Vector2I(640, 360));

        var label = _screen!.GetNode<Button>("%Slot0Card")
            .GetNode<Label>("Margin/Content/StateLabel");
        label.Text = string.Join(" ",
            "Incompatible save: this file was created by a newer Sirius version and cannot be loaded until the game is updated.",
            "Please choose another save slot.");

        await AwaitFrames(2);

        AssertThat(label.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(label.Size.Y).IsGreater(0f);
        AssertThat(label.GetGlobalRect().End.X)
            .IsLessEqual(_screen.GetNode<Button>("%Slot0Card").GetGlobalRect().End.X + 0.5f);
    }

    [TestCase(640, 360)]
    [TestCase(1024, 768)]
    [TestCase(1280, 720)]
    [TestCase(1440, 900)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    [TestCase(2560, 1440)]
    public async Task ApprovedViewport_KeepsPanelInsideViewport(int width, int height)
    {
        await ResizeAndCreate(new Vector2I(width, height));

        var panelRect = _screen!.GetNode<PanelContainer>("ModalShell/Panel").GetGlobalRect();
        AssertThat(panelRect.Position.X).IsGreaterEqual(0f);
        AssertThat(panelRect.Position.Y).IsGreaterEqual(0f);
        AssertThat(panelRect.End.X).IsLessEqual(width + 0.5f);
        AssertThat(panelRect.End.Y).IsLessEqual(height + 0.5f);
    }

    private async Task ResizeAndCreate(Vector2I size)
    {
        _container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(_container);

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        _container.AddChild(_viewport);

        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _screen = scene.Instantiate<SaveLoadScreenController>();
        _viewport.AddChild(_screen);
        await AwaitFrames(3);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
