using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class InventoryMenuSceneTest : Node
{
    private SceneTree _sceneTree = null!;
    private GameManager _gameManager = null!;
    private SubViewportContainer _viewportContainer = null!;
    private SubViewport _viewport = null!;
    private InventoryMenuController _menu = null!;

    [BeforeTest]
    public async Task SetUp()
    {
        TestHelpers.ResetGameManagerSingleton();
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _sceneTree.Paused = false;

        _gameManager = new GameManager { AutoSaveEnabled = false };
        _sceneTree.Root.AddChild(_gameManager);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        _viewportContainer = new SubViewportContainer
        {
            Size = new Vector2(1280, 720),
            Stretch = true
        };
        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(1280, 720),
            GuiEmbedSubwindows = true
        };
        _viewportContainer.AddChild(_viewport);
        _sceneTree.Root.AddChild(_viewportContainer);

        var packed = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn")
            ?? throw new InvalidOperationException("Failed to load InventoryMenu.tscn.");
        _menu = packed.Instantiate<InventoryMenuController>();
        _viewport.AddChild(_menu);
        await AwaitFrames(2);
    }

    [AfterTest]
    public async Task TearDown()
    {
        _sceneTree.Paused = false;
        if (GodotObject.IsInstanceValid(_menu)) _menu.Free();
        if (GodotObject.IsInstanceValid(_viewportContainer)) _viewportContainer.Free();
        if (GodotObject.IsInstanceValid(_gameManager)) _gameManager.Free();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        TestHelpers.ResetGameManagerSingleton();
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private async Task Resize(Vector2I size)
    {
        _viewport.Size = size;
        _viewportContainer.Size = new Vector2(size.X, size.Y);
        await AwaitFrames(2);
    }

    private int VisiblePageCount() =>
        new[] { "%EquipmentPage", "%ItemsPage", "%SkillsPage" }
            .Count(path => _menu.GetNode<Control>(path).Visible);

    private void PushAction(StringName action)
    {
        _viewport.PushInput(new InputEventAction
        {
            Action = action,
            Pressed = true
        });
    }

    [TestCase]
    public async Task FitsEveryVerificationViewport()
    {
        foreach (var size in SiriusUiMetrics.VerificationViewports)
        {
            await Resize(size);
            _menu.OpenMenu();
            await AwaitFrames(2);

            var safe = _menu.GetNode<Control>("%SafeFrame");
            AssertThat(new Rect2(Vector2.Zero, size).Encloses(safe.GetGlobalRect())).IsTrue();
            AssertThat(safe.Size.X).IsGreater(0f);
            AssertThat(safe.Size.Y).IsGreater(0f);
        }
    }

    [TestCase]
    public async Task Standard_ShowsAllThreeContentAreasTogether()
    {
        await Resize(new Vector2I(1280, 720));
        _menu.OpenMenu();
        await AwaitFrames(2);

        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsFalse();
        AssertThat(_menu.GetNode<Control>("%EquipmentPage").Visible).IsTrue();
        AssertThat(_menu.GetNode<Control>("%SkillsPage").Visible).IsTrue();
        AssertThat(_menu.GetNode<Control>("%ItemsPage").Visible).IsTrue();
        AssertThat(_menu.GetNode<Label>("%EquipmentTitleLabel").Text).IsEqual("Equipment");
        AssertThat(_menu.GetNode<Label>("%InventoryTitleLabel").Text).IsEqual("Items");
        AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
            .IsEqual(new Vector2(56, 56));
    }

    [TestCase]
    public async Task Compact_ShowsOnePageAndApprovedSlotSize()
    {
        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
        AssertThat(VisiblePageCount()).IsEqual(1);
        AssertThat(_menu.GetNode<Control>("%IdentityStrip").Visible).IsTrue();
        AssertThat(_menu.GetNode<Button>("%CloseButton").Visible).IsTrue();
        AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
            .IsEqual(new Vector2(48, 48));
    }

    [TestCase]
    public async Task Compact_ConstrainsActivePageSummaryAndFooterToViewport()
    {
        var size = new Vector2I(640, 360);
        await Resize(size);
        _menu.OpenMenu();
        await AwaitFrames(2);

        var viewportRect = new Rect2(Vector2.Zero, size);
        var activePage = _menu.GetNode<Control>("%EquipmentPage");
        var summary = _menu.GetNode<Label>("%FocusSummary");
        var close = _menu.GetNode<Button>("%CloseButton");

        AssertThat(activePage.GetGlobalRect().Size.X).IsGreater(0f);
        AssertThat(activePage.GetGlobalRect().Size.Y).IsGreater(0f);
        AssertThat(activePage.GetGlobalRect().Intersects(viewportRect)).IsTrue();
        AssertThat(viewportRect.Encloses(summary.GetGlobalRect())).IsTrue();
        AssertThat(viewportRect.Encloses(close.GetGlobalRect())).IsTrue();

        summary.Text = string.Join(
            "\n",
            Enumerable.Repeat("A long focus explanation that must remain bounded.", 8));
        await AwaitFrames(2);

        AssertThat(viewportRect.Encloses(summary.GetGlobalRect())).IsTrue();
        AssertThat(viewportRect.Encloses(close.GetGlobalRect())).IsTrue();
    }

    [TestCase]
    public void AuthorsExactlyDomainAccessorySlotCount()
    {
        var slots = _menu.GetNode<Container>("%AccessorySlots")
            .GetChildren().OfType<SiriusItemSlotController>().ToArray();

        AssertThat(slots.Length).IsEqual(EquipmentSet.AccessorySlotCount);
        AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
        AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot5")).IsNull();
    }

    [TestCase]
    public async Task Compact_EquipmentTabDownAndFirstControlUpUseSpatialNavigation()
    {
        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var tab = _menu.GetNode<Button>("%EquipmentTab");
        var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");

        tab.GrabFocus();
        PushAction("ui_down");
        await AwaitFrames(2);
        AssertThat(weapon.HasFocus()).IsTrue();

        PushAction("ui_up");
        await AwaitFrames(2);
        AssertThat(tab.HasFocus()).IsTrue();
    }

    [TestCase]
    public async Task Compact_LastEquipmentControlDownReachesClose()
    {
        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var lastAccessory = _menu.GetNode<SiriusItemSlotController>("%AccessorySlot3");
        var close = _menu.GetNode<Button>("%CloseButton");

        lastAccessory.GrabFocus();
        PushAction("ui_down");
        await AwaitFrames(2);

        AssertThat(close.HasFocus()).IsTrue();
    }

    [TestCase]
    public async Task CompactShoulders_CyclePagesWhenProcessModeIsWhenPaused()
    {
        await Resize(new Vector2I(640, 360));
        _menu.ProcessMode = Node.ProcessModeEnum.WhenPaused;
        _menu.OpenMenu();
        _sceneTree.Paused = true;

        try
        {
            _viewport.PushInput(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.RightShoulder,
                Pressed = true
            });
            await AwaitFrames(2);
            AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();

            _viewport.PushInput(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.RightShoulder,
                Pressed = true
            });
            await AwaitFrames(2);
            AssertThat(_menu.GetNode<Button>("%SkillsTab").ButtonPressed).IsTrue();
        }
        finally
        {
            _sceneTree.Paused = false;
        }
    }
}
