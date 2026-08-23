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
        new[] { "%EquipmentPage", "%ItemsPage", "%SkillsPage", "%DetailsPage" }
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
    public async Task Standard1024_ItemsGridFitsWithoutHorizontalScroll()
    {
        _gameManager.Player.Inventory.Clear();
        for (var i = 0; i < 6; i++)
        {
            AssertThat(_gameManager.Player.TryAddItem(new EquipmentItem
            {
                Id = $"width_item_{i}",
                DisplayName = $"Width Item {i}",
                SlotType = EquipmentSlotType.Weapon
            }, 1, out _)).IsTrue();
        }

        await Resize(new Vector2I(1024, 768));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var scroll = _menu.GetNode<ScrollContainer>("%InventoryScroll");
        var grid = _menu.GetNode<GridContainer>("%InventoryGrid");
        AssertThat(grid.GetCombinedMinimumSize().X).IsLessEqual(scroll.Size.X);
        AssertThat(scroll.GetHScrollBar().Visible).IsFalse();
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
    public async Task InitialFocusTarget_UsesFirstVisibleContentControl()
    {
        var firstContent = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");
        var close = _menu.GetNode<Button>("%CloseButton");

        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(640, 360) })
        {
            await Resize(size);
            _menu.OpenMenu();
            await AwaitFrames(2);

            AssertThat(_menu.InitialFocusTarget).IsEqual(firstContent);
            AssertThat(_menu.InitialFocusTarget).IsNotEqual(close);
            AssertThat(_menu.InitialFocusTarget.IsVisibleInTree()).IsTrue();

            _menu.CloseMenu();
        }
    }

    [TestCase]
    public async Task Compact_DetailsPageIsOneOfExactlyFourPages()
    {
        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        _menu.GetNode<Button>("%DetailsTab").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(VisiblePageCount()).IsEqual(1);
        AssertThat(_menu.GetNode<Control>("%DetailsPage").Visible).IsTrue();
        AssertThat(_menu.GetNode<Control>("%CharacterColumn").Visible).IsFalse();
    }

    [TestCase]
    public async Task CompactSkillsWithoutActiveSkill_FallsBackToSkillsTabForInitialFocus()
    {
        _gameManager.Player.KnownSkillIds.Clear();
        _gameManager.Player.ActiveSkillId = null;
        _gameManager.Player.ActiveSkillExplicitlyNone = true;

        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var skillsTab = _menu.GetNode<Button>("%SkillsTab");
        var selector = _menu.GetNode<OptionButton>("%ActiveSkillSelector");
        var close = _menu.GetNode<Button>("%CloseButton");

        skillsTab.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(skillsTab.ButtonPressed).IsTrue();
        AssertThat(selector.Disabled).IsTrue();
        AssertThat(_menu.InitialFocusTarget).IsEqual(skillsTab);
        AssertThat(_menu.InitialFocusTarget).IsNotEqual(close);
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

    [TestCase]
    public async Task CompactShoulders_FromFocusedContentRestoreFocusOnNewPage()
    {
        // Explicit precondition: one known actionable consumable in the inventory
        // so the firstUsableItem lookup below is deterministic regardless of
        // starter inventory changes.
        _gameManager.Player.Inventory.Clear();
        _gameManager.Player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _);

        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");
        var firstUsableItem = _menu.GetNode<Container>("%InventoryGrid")
            .GetChildren()
            .OfType<SiriusItemSlotController>()
            .First(slot => slot.Actionable);
        weapon.GrabFocus();

        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.RightShoulder,
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(firstUsableItem);
        AssertThat(firstUsableItem.IsVisibleInTree()).IsTrue();
    }

    [TestCase]
    public async Task CompactToStandard_EmptyItemsPage_FallsBackToCloseButtonForFocus()
    {
        // Empty inventory: compact Items page falls back to %ItemsTab for focus
        // (a child of the hidden-in-standard CompactTabs). Resizing to standard
        // must not drop focus silently — it should fall back to the always-
        // focusable CloseButton.
        _gameManager.Player.Inventory.Clear();

        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var itemsTab = _menu.GetNode<Button>("%ItemsTab");
        itemsTab.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);
        // Simulate the RestoreCompactPageFocus fallback state: the tab itself
        // holds focus because no inventory slot is focusable.
        itemsTab.GrabFocus();
        await AwaitFrames(1);
        AssertThat(itemsTab.HasFocus()).IsTrue();

        await Resize(new Vector2I(1280, 720));
        await AwaitFrames(2);

        var close = _menu.GetNode<Button>("%CloseButton");
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(close);
        AssertThat(close.IsVisibleInTree()).IsTrue();
    }

    [TestCase]
    public async Task CompactToStandard_DisabledSkillsSelector_FallsBackToCloseButtonForFocus()
    {
        // No active skill: compact Skills page falls back to %SkillsTab for focus
        // (a child of CompactTabs) and the selector is disabled. Resizing to
        // standard must fall back to CloseButton rather than dropping focus on
        // the disabled selector.
        _gameManager.Player.KnownSkillIds.Clear();
        _gameManager.Player.ActiveSkillId = null;
        _gameManager.Player.ActiveSkillExplicitlyNone = true;

        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var skillsTab = _menu.GetNode<Button>("%SkillsTab");
        skillsTab.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);
        // Simulate the RestoreCompactPageFocus fallback state: the tab itself
        // holds focus because the active-skill selector is disabled.
        skillsTab.GrabFocus();
        await AwaitFrames(1);
        AssertThat(skillsTab.HasFocus()).IsTrue();

        await Resize(new Vector2I(1280, 720));
        await AwaitFrames(2);

        var close = _menu.GetNode<Button>("%CloseButton");
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(close);
        AssertThat(close.IsVisibleInTree()).IsTrue();
    }

    [TestCase]
    public async Task CompactItems_HidesCharacterColumnAndUsesFullContentWidth()
    {
        var size = new Vector2I(640, 360);
        await Resize(size);
        _menu.OpenMenu();
        await AwaitFrames(2);

        _menu.GetNode<Button>("%ItemsTab").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var safeFrame = _menu.GetNode<Control>("%SafeFrame");
        var characterColumn = _menu.GetNode<Control>("%CharacterColumn");
        var itemsPage = _menu.GetNode<Control>("%ItemsPage");

        AssertThat(characterColumn.Visible).IsFalse();
        AssertThat(itemsPage.GetGlobalRect().Size.X).IsGreater(safeFrame.GetGlobalRect().Size.X * 0.8f);
        AssertThat(new Rect2(Vector2.Zero, size).Encloses(itemsPage.GetGlobalRect())).IsTrue();
    }

    [TestCase]
    public async Task ReattachedPresentation_RetainsCloseTabsSkillAndResizeBehavior()
    {
        await Resize(new Vector2I(1280, 720));
        SkillCatalog.GrantSkillsUpToLevel(_gameManager.Player, 3);
        _menu.OpenMenu();
        await AwaitFrames(2);

        var parent = _menu.GetParent();
        parent.RemoveChild(_menu);
        await AwaitFrames(1);
        parent.AddChild(_menu);
        await AwaitFrames(2);

        var closeRequests = 0;
        _menu.CloseRequested += () => closeRequests++;
        _menu.OpenMenu();
        await AwaitFrames(2);

        var close = _menu.GetNode<Button>("%CloseButton");
        close.EmitSignal(Button.SignalName.Pressed);
        AssertThat(closeRequests).IsEqual(1);

        await Resize(new Vector2I(640, 360));
        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();

        var itemsTab = _menu.GetNode<Button>("%ItemsTab");
        itemsTab.EmitSignal(Button.SignalName.Pressed);
        AssertThat(itemsTab.ButtonPressed).IsTrue();
        AssertThat(_menu.GetNode<Control>("%ItemsPage").Visible).IsTrue();
        AssertThat(_menu.GetNode<Control>("%EquipmentPage").Visible).IsFalse();

        _menu.GetNode<Button>("%SkillsTab").EmitSignal(Button.SignalName.Pressed);
        var selector = _menu.GetNode<OptionButton>("%ActiveSkillSelector");
        var activeSkillBefore = _gameManager.Player.ActiveSkillId;
        selector.Select(2);
        selector.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
        AssertThat(_gameManager.Player.ActiveSkillId).IsNotEqual(activeSkillBefore);
    }

    [TestCase]
    public async Task ResizeWhileDetached_DoesNotCorruptLayoutAfterReattach()
    {
        // UIScreenHost detaches the view while closed but the viewport
        // SizeChanged connection survives. Resizing across the compact
        // breakpoint while detached must not invoke GetViewportRect() outside
        // the tree; OpenMenu() re-runs RefreshLayout() after reattachment.
        await Resize(new Vector2I(1280, 720));
        _menu.OpenMenu();
        await AwaitFrames(2);
        _menu.CloseMenu();

        var parent = _menu.GetParent();
        parent.RemoveChild(_menu);
        await AwaitFrames(1);
        AssertThat(_menu.IsInsideTree()).IsFalse();

        await Resize(new Vector2I(640, 360));

        parent.AddChild(_menu);
        await AwaitFrames(2);

        _menu.OpenMenu();
        await AwaitFrames(2);

        AssertThat(_menu.IsInsideTree()).IsTrue();
        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
        AssertThat(VisiblePageCount()).IsEqual(1);
    }

    [TestCase]
    public async Task StandardToCompact_PreservesFocusOnFocusedInventoryItem()
    {
        _gameManager.Player.Inventory.Clear();
        AssertThat(_gameManager.Player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _)).IsTrue();

        await Resize(new Vector2I(1280, 720));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var itemSlot = _menu.GetNode<Container>("%InventoryGrid")
            .GetChildren()
            .OfType<SiriusItemSlotController>()
            .First(slot => slot.Actionable);
        itemSlot.GrabFocus();
        await AwaitFrames(1);
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(itemSlot);

        await Resize(new Vector2I(640, 360));

        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
        AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();
        AssertThat(itemSlot.IsVisibleInTree()).IsTrue();
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(itemSlot);
    }

    [TestCase]
    public async Task StandardToCompact_PreservesFocusOnActiveSkillSelector()
    {
        SkillCatalog.GrantSkillsUpToLevel(_gameManager.Player, 3);

        await Resize(new Vector2I(1280, 720));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var selector = _menu.GetNode<OptionButton>("%ActiveSkillSelector");
        selector.GrabFocus();
        await AwaitFrames(1);
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(selector);

        await Resize(new Vector2I(640, 360));

        AssertThat(_menu.GetNode<Button>("%SkillsTab").ButtonPressed).IsTrue();
        AssertThat(selector.IsVisibleInTree()).IsTrue();
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(selector);
    }

    [TestCase]
    public async Task CompactToStandard_RestoresContentFocusWhenCompactTabHidden()
    {
        _gameManager.Player.Inventory.Clear();
        AssertThat(_gameManager.Player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _)).IsTrue();

        await Resize(new Vector2I(640, 360));
        _menu.OpenMenu();
        await AwaitFrames(2);

        var itemsTab = _menu.GetNode<Button>("%ItemsTab");
        itemsTab.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);
        itemsTab.GrabFocus();
        await AwaitFrames(1);
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(itemsTab);

        await Resize(new Vector2I(1280, 720));

        AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsFalse();
        AssertThat(itemsTab.IsVisibleInTree()).IsFalse();

        var firstItem = _menu.GetNode<Container>("%InventoryGrid")
            .GetChildren()
            .OfType<SiriusItemSlotController>()
            .First(slot => slot.Actionable);
        AssertThat(firstItem.IsVisibleInTree()).IsTrue();
        AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(firstItem);
    }
}
