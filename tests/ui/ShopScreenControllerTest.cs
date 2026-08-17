using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ShopScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/ShopScreen.tscn";

    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [BeforeTest]
    public void Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    // ---- Scene & configuration --------------------------------------------

    [TestCase]
    public void Scene_InstantiatesShopScreenController()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();

        var screen = scene!.Instantiate<ShopScreenController>();
        try
        {
            AssertThat(screen).IsNotNull();
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task TryOpenShop_BeforeReady_RendersAfterAttach()
    {
        var screen = CreateUnparentedScreen();
        var shop = ShopCatalog.GetById("village_general_store")!;
        var player = CreatePlayer(gold: 500);
        var firstItemId = shop.Entries[0].ItemId;

        AssertThat(screen.TryOpenShop(shop, player)).IsTrue();

        var (container, viewport) = TestHelpers.MountInViewport(screen, new Vector2I(1280, 720));
        try
        {
            await AwaitFrames(2);

            AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
                .IsEqual(shop.DisplayName);
            AssertThat(screen.GetNode<Label>("%GoldLabel").Text).IsEqual("Your Gold: 500");

            var buyList = screen.GetNode<VBoxContainer>("%BuyList");
            AssertThat(buyList.GetChildren().OfType<HBoxContainer>().Count())
                .IsEqual(shop.Entries.Count);

            AssertThat(ContainsLabelText(
                screen.GetNode<VBoxContainer>("%SellList"), "Nothing to sell.")).IsTrue();

            AssertThat(screen.InitialFocusTarget).IsNotNull();
            AssertThat(IsFocusable(screen.InitialFocusTarget!)).IsTrue();
            AssertThat(screen.InitialFocusTarget).IsEqual(RowButton(buyList, firstItemId));
            AssertThat(viewport.GuiGetFocusOwner()).IsEqual(screen.InitialFocusTarget);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(container))
                container.QueueFree();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public void TryOpenShop_SecondStartRejected_EvenAfterCancel()
    {
        var screen = CreateUnparentedScreen();
        try
        {
            var shop = ShopCatalog.GetById("village_general_store")!;
            var player = CreatePlayer(gold: 100);
            int closed = 0;
            screen.ShopClosed += () => closed++;

            AssertThat(screen.TryOpenShop(shop, player)).IsTrue();
            AssertThat(screen.TryOpenShop(shop, player)).IsFalse();

            screen.RequestCancel();
            AssertThat(screen.TryOpenShop(shop, player)).IsFalse();
            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task Scene_UsesLargeCentredModalShell_WithoutSafeFrameOrAcceptDialog()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            var screen = fixture.Screen;
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");

            // Centred non-Full screen: no SafeFrame node, shell is a direct
            // child of the screen root (no Dialogue-style bottom-band wrapper).
            AssertThat(screen.GetNodeOrNull("%SafeFrame")).IsNull();
            AssertThat(shell.GetParent()).IsEqual(screen);
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Large);
            AssertThat(ContainsAcceptDialog(screen)).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Viewport geometry ----------------------------------------------------
    // Integration-level geometry only: SiriusModalShellTest owns the exhaustive
    // clamp math; these pin the authored screens' centring, size class, margin
    // and scroll outcomes at the representative verification viewports.

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    public async Task StandardShop_IsCentredLargeWithinSafeMargins(int width, int height)
    {
        var fixture = await OpenMountedShopAsync(
            CreatePlayer(gold: 500),
            ShopCatalog.GetById("village_general_store")!,
            new Vector2I(width, height));
        try
        {
            var shell = fixture.Screen.GetNode<SiriusModalShell>("%ModalShell");
            var panel = shell.GetNode<PanelContainer>("%Panel");

            AssertThat(shell.Compact).IsFalse();
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Large);

            var margin = SiriusUiMetrics.SafeMargin(false);
            var rect = panel.GetGlobalRect();

            // Centred on the viewport in both axes — a Dialogue-style bottom
            // band would pin the panel far below mid-screen.
            AssertThat(rect.GetCenter().X).IsEqualApprox(width / 2f, 1f);
            AssertThat(rect.GetCenter().Y).IsEqualApprox(height / 2f, 1f);

            AssertThat(rect.Position.X).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.End.X).IsLessEqual(width - margin + 0.5f);
            AssertThat(rect.End.Y).IsLessEqual(height - margin + 0.5f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task CompactShop_AppliesTwelvePxMarginExactlyOnce()
    {
        var fixture = await OpenMountedShopAsync(
            CreatePlayer(gold: 500),
            ShopCatalog.GetById("village_general_store")!,
            new Vector2I(640, 360));
        try
        {
            var shell = fixture.Screen.GetNode<SiriusModalShell>("%ModalShell");
            var panel = shell.GetNode<PanelContainer>("%Panel");

            AssertThat(shell.Compact).IsTrue();

            // The shell owns the compact margin; the screen must not re-apply
            // it. One 12 px inset per side → width is exactly viewport minus
            // 24, not 48.
            var margin = SiriusUiMetrics.SafeMargin(true);
            var rect = panel.GetGlobalRect();
            AssertThat(rect.Position.X).IsEqualApprox(margin, 1f);
            AssertThat(rect.End.X).IsEqualApprox(640f - margin, 1f);
            AssertThat(rect.Size.X).IsEqualApprox(640f - margin * 2f, 1f);

            AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.End.Y).IsLessEqual(360f - margin + 0.5f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task CompactShop_OverflowingBodyScrolls_KeepsRequiredControlsReachable()
    {
        var fixture = await OpenMountedShopAsync(
            CreatePlayer(gold: 500),
            ShopCatalog.GetById("village_general_store")!,
            new Vector2I(640, 360));
        try
        {
            var screen = fixture.Screen;
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
            var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
            var close = screen.GetNode<Button>("%CloseButton");

            // The eight store rows cannot fit the compact body budget: the
            // shell body must scroll instead of growing past the safe margins.
            AssertThat(bodyScroll.GetVScrollBar().MaxValue)
                .IsGreater(bodyScroll.GetVScrollBar().Page);

            // Required controls stay reachable: the shell-level Close action
            // never leaves the viewport-safe band.
            var margin = SiriusUiMetrics.SafeMargin(true);
            var closeRect = close.GetGlobalRect();
            AssertThat(closeRect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(closeRect.End.Y).IsLessEqual(360f - margin + 0.5f);
            // The scrolling body ends above the shell-level actions: no
            // overlap even when the rows overflow the compact body budget.
            AssertThat(closeRect.Position.Y)
                .IsGreaterEqual(bodyScroll.GetGlobalRect().End.Y - 0.5f);

            // Focus-follow keeps overflowing rows reachable: focusing the
            // last row scrolls it into view inside the shell body.
            var lastRow = screen.GetNode<VBoxContainer>("%BuyList")
                .GetChildren().OfType<HBoxContainer>().Last();
            lastRow.GetChildren().OfType<Button>().Single().GrabFocus();
            await AwaitFrames(2);

            AssertThat(bodyScroll.ScrollVertical).IsGreater(0);
            var rowRect = lastRow.GetGlobalRect();
            AssertThat(rowRect.Position.Y)
                .IsGreaterEqual(bodyScroll.GetGlobalRect().Position.Y - 0.5f);
            AssertThat(rowRect.End.Y).IsLessEqual(360f - margin + 0.5f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Buy ----------------------------------------------------------------

    [TestCase]
    public async Task Buy_Success_DeductsValueAddsItemAndRefreshesLists()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            var item = ItemCatalog.CreateItemById("health_potion")!;

            RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(500 - item.Value);
            AssertThat(player.Inventory.GetQuantity("health_potion")).IsEqual(1);
            AssertThat(screen.GetNode<Label>("%GoldLabel").Text)
                .IsEqual($"Your Gold: {500 - item.Value}");
            AssertThat(RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion"))
                .IsNotNull();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Buy_Unaffordable_DisablesButtonAndShowsStandingRowReason()
    {
        var fixture = await OpenMountedShopAsync(gold: 0);
        try
        {
            var screen = fixture.Screen;
            var buyList = screen.GetNode<VBoxContainer>("%BuyList");

            AssertThat(RowButton(buyList, "health_potion")!.Disabled).IsTrue();
            AssertThat(RowContainsLabel(buyList, "health_potion", "Not enough gold!"))
                .IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task FeedbackTimeout_ClearsTransientOnly_StandingReasonRemains()
    {
        // Player can afford the cheap item but not the expensive one, and the
        // inventory rejects new item types, so the cheap Buy produces transient
        // "Inventory full!" while the expensive row keeps its standing reason.
        var cheap = ItemCatalog.CreateItemById("health_potion")!;
        var expensive = ItemCatalog.CreateItemById("greater_health_potion")!;
        AssertThat(cheap.Value < expensive.Value).IsTrue();

        var player = CreatePlayer(gold: cheap.Value);
        player.Inventory.MaxItemTypes = 0;

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;
            var buyList = screen.GetNode<VBoxContainer>("%BuyList");

            RowButton(buyList, "health_potion")!.EmitSignal(Button.SignalName.Pressed);

            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Inventory full!");
            AssertThat(player.Gold).IsEqual(cheap.Value); // rolled back
            AssertThat(RowContainsLabel(buyList, "greater_health_potion", "Not enough gold!"))
                .IsTrue();

            await ToSignal(_sceneTree.CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);

            AssertThat(feedback.Visible).IsFalse();
            AssertThat(RowContainsLabel(buyList, "greater_health_potion", "Not enough gold!"))
                .IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Buy_InventoryFull_RollsBackGoldAndShowsTransientFeedback()
    {
        var player = CreatePlayer(gold: 500);
        player.Inventory.MaxItemTypes = 0;

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;

            RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(500);
            AssertThat(player.Inventory.GetQuantity("health_potion")).IsEqual(0);
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Inventory full!");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Buy_CallbackRevalidationAfterGoldDrop_ShowsTransientNotEnoughGold()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            var item = ItemCatalog.CreateItemById("health_potion")!;

            // Row was rendered affordable; gold then drops below the price
            // before the captured callback revalidates.
            AssertThat(player.TrySpendGold(500 - item.Value + 1)).IsTrue();

            RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(item.Value - 1);
            AssertThat(player.Inventory.GetQuantity("health_potion")).IsEqual(0);
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Not enough gold!");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Buy_MissingCatalogItem_SkippedAndCallbackSafe()
    {
        var shop = new ShopInventory
        {
            ShopId = "test_mixed_shop",
            DisplayName = "Mixed Test Shop",
            Entries = new List<ShopEntry>
            {
                new() { ItemId = "health_potion" },
                new() { ItemId = "not_a_real_item" }
            }
        };

        var fixture = await OpenMountedShopAsync(CreatePlayer(gold: 500), shop);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            var buyList = screen.GetNode<VBoxContainer>("%BuyList");
            AssertThat(buyList.GetChildren().OfType<HBoxContainer>().Count()).IsEqual(1);

            // The captured callback path revalidates and refreshes safely.
            InvokePrivate(screen, "OnBuyPressed", "not_a_real_item", 5);
            await AwaitFrames(1);

            AssertThat(player.Gold).IsEqual(500);
            AssertThat(buyList.GetChildren().OfType<HBoxContainer>().Count()).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Sell ---------------------------------------------------------------

    [TestCase]
    public async Task Sell_OneActivation_RemovesOneItemAndGrantsSellPrice()
    {
        var player = CreatePlayer(gold: 10);
        var item = ItemCatalog.CreateItemById("health_potion")!;
        player.TryAddItem(item, 2, out _);

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;

            RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            var expectedGold = 10 + ShopScreenController.SellPrice(item.Value);
            AssertThat(player.Inventory.GetQuantity("health_potion")).IsEqual(1);
            AssertThat(player.Gold).IsEqual(expectedGold);
            AssertThat(screen.GetNode<Label>("%GoldLabel").Text)
                .IsEqual($"Your Gold: {expectedGold}");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Sell_LastItem_ShowsNothingToSellImmediately()
    {
        var player = CreatePlayer(gold: 0);
        var item = ItemCatalog.CreateItemById("health_potion")!;
        player.TryAddItem(item, 1, out _);

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;

            RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            AssertThat(ContainsLabelText(
                screen.GetNode<VBoxContainer>("%SellList"), "Nothing to sell.")).IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Sell_FailedRemoval_NoGoldTransientFeedbackAndRefreshes()
    {
        var player = CreatePlayer(gold: 10);
        var item = ItemCatalog.CreateItemById("health_potion")!;
        player.TryAddItem(item, 1, out _);

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;

            // The captured callback revalidates: the item vanished externally.
            AssertThat(player.TryRemoveItem("health_potion", 1)).IsTrue();

            RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion")!
                .EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(10);
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Item no longer available.");
            AssertThat(ContainsLabelText(
                screen.GetNode<VBoxContainer>("%SellList"), "Nothing to sell.")).IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Transient feedback timer -------------------------------------------

    [TestCase]
    public async Task ShowFeedback_KeepsLatestMessageVisible_UntilLatestTimerExpires()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            var feedback = fixture.Screen.GetNode<Label>("%FeedbackLabel");

            InvokePrivate(fixture.Screen, "ShowFeedback", "First message");
            AssertThat(feedback.Text).IsEqual("First message");
            AssertThat(feedback.Visible).IsTrue();

            await ToSignal(_sceneTree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

            InvokePrivate(fixture.Screen, "ShowFeedback", "Second message");
            AssertThat(feedback.Text).IsEqual("Second message");
            AssertThat(feedback.Visible).IsTrue();

            // The first timer (2s from t=0) has expired by t=2.2; only the
            // second message's timer may hide the label (at t=3.0).
            await ToSignal(_sceneTree.CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            AssertThat(feedback.Text).IsEqual("Second message");
            AssertThat(feedback.Visible).IsTrue();

            await ToSignal(_sceneTree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            AssertThat(feedback.Visible).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task RequestCancel_CancelsPendingFeedbackTimer()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            InvokePrivate(fixture.Screen, "ShowFeedback", "Pending");
            fixture.Screen.RequestCancel();

            AssertThat(GetNullableField<SceneTreeTimer>(fixture.Screen, "_feedbackTimer"))
                .IsNull();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ExitTree_CancelsPendingFeedbackTimer()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            InvokePrivate(fixture.Screen, "ShowFeedback", "Pending");

            fixture.Screen.GetParent().RemoveChild(fixture.Screen);
            await AwaitFrames(1);

            AssertThat(GodotObject.IsInstanceValid(fixture.Screen)).IsTrue();
            AssertThat(GetNullableField<SceneTreeTimer>(fixture.Screen, "_feedbackTimer"))
                .IsNull();
        }
        finally
        {
            fixture.Screen.QueueFree();
            await FreeAsync(fixture);
        }
    }

    // ---- Re-entrancy & terminal guards ---------------------------------------

    [TestCase]
    public async Task Buy_WhileOperationInFlight_ReturnsWithoutMutation()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;

            SetPrivateField(screen, "_operationInFlight", true);
            try
            {
                RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion")!
                    .EmitSignal(Button.SignalName.Pressed);

                AssertThat(player.Gold).IsEqual(500);
                AssertThat(player.Inventory.GetQuantity("health_potion")).IsEqual(0);
            }
            finally
            {
                SetPrivateField(screen, "_operationInFlight", false);
            }
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task RequestCancelTwice_EmitsShopClosedOnce()
    {
        var fixture = await OpenMountedShopAsync(gold: 500);
        try
        {
            int closed = 0;
            fixture.Screen.ShopClosed += () => closed++;

            fixture.Screen.RequestCancel();
            fixture.Screen.RequestCancel();

            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Focus chain ----------------------------------------------------------

    [TestCase]
    public async Task ZeroGoldShop_NeverFocusesDisabledBuyButton()
    {
        var fixture = await OpenMountedShopAsync(gold: 0);
        try
        {
            var screen = fixture.Screen;
            await AwaitFrames(2);

            AssertThat(screen.InitialFocusTarget).IsNotNull();
            AssertThat(IsFocusable(screen.InitialFocusTarget!)).IsTrue();
            AssertThat(screen.InitialFocusTarget is Button { Disabled: true }).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(screen.InitialFocusTarget);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ShopTabSwitch_LeavesUsableFocusTarget()
    {
        var player = CreatePlayer(gold: 500);
        player.TryAddItem(ItemCatalog.CreateItemById("health_potion")!, 1, out _);

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;
            var tabs = screen.GetNode<TabContainer>("%ShopTabs");
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(
                RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion"));

            tabs.CurrentTab = 1;
            await AwaitFrames(1);

            // Hiding the Buy tab releases its focus owner; the screen must
            // resolve to a focusable control on the newly active tab.
            var sellFocus = fixture.Viewport.GuiGetFocusOwner();
            AssertThat(sellFocus).IsNotNull();
            AssertThat(IsFocusable(sellFocus!)).IsTrue();
            AssertThat(sellFocus).IsEqual(
                RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion"));

            tabs.CurrentTab = 0;
            await AwaitFrames(1);

            var buyFocus = fixture.Viewport.GuiGetFocusOwner();
            AssertThat(buyFocus).IsNotNull();
            AssertThat(IsFocusable(buyFocus!)).IsTrue();
            AssertThat(buyFocus).IsEqual(
                RowButton(screen.GetNode<VBoxContainer>("%BuyList"), "health_potion"));
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task SellFocusedLastItem_FocusLandsOnFocusableTarget()
    {
        var player = CreatePlayer(gold: 0);
        var item = ItemCatalog.CreateItemById("health_potion")!;
        player.TryAddItem(item, 1, out _);

        var fixture = await OpenMountedShopAsync(
            player, ShopCatalog.GetById("village_general_store")!);
        try
        {
            var screen = fixture.Screen;
            screen.GetNode<TabContainer>("%ShopTabs").CurrentTab = 1;
            await AwaitFrames(1);

            var sellButton = RowButton(screen.GetNode<VBoxContainer>("%SellList"), "health_potion")!;
            sellButton.GrabFocus();
            await AwaitFrames(1);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(sellButton);

            sellButton.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            var focused = fixture.Viewport.GuiGetFocusOwner();
            AssertThat(focused == sellButton).IsFalse();
            AssertThat(focused).IsNotNull();
            AssertThat(IsFocusable(focused!)).IsTrue();
            AssertThat(focused!.IsQueuedForDeletion()).IsFalse();
            AssertThat(screen.InitialFocusTarget).IsEqual(focused);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task RebuildWithChangedAffordability_LandsOnFocusableTarget()
    {
        var item = ItemCatalog.CreateItemById("health_potion")!;
        var shop = new ShopInventory
        {
            ShopId = "test_single_shop",
            DisplayName = "Single Item Shop",
            Entries = new List<ShopEntry> { new() { ItemId = "health_potion" } }
        };
        // Gold covers exactly two purchases: after the second buy the row's
        // Buy button becomes disabled and focus must fall through the chain.
        var player = CreatePlayer(gold: item.Value * 2);

        var fixture = await OpenMountedShopAsync(player, shop);
        try
        {
            var screen = fixture.Screen;
            var buyList = screen.GetNode<VBoxContainer>("%BuyList");

            var firstButton = RowButton(buyList, "health_potion")!;
            firstButton.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            // Same item action: the rebuilt Buy button is focused again.
            var refocused = RowButton(buyList, "health_potion")!;
            AssertThat(refocused == firstButton).IsFalse();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(refocused);

            refocused.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            // Affordability changed: the only Buy button is now disabled.
            AssertThat(RowButton(buyList, "health_potion")!.Disabled).IsTrue();
            var focused = fixture.Viewport.GuiGetFocusOwner();
            AssertThat(focused).IsNotNull();
            AssertThat(IsFocusable(focused!)).IsTrue();
            AssertThat(focused!.IsQueuedForDeletion()).IsFalse();
            AssertThat(focused is Button { Disabled: true }).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Fixture helpers -------------------------------------------------------

    private sealed record ShopFixture(
        SubViewportContainer Container,
        SubViewport Viewport,
        ShopScreenController Screen,
        Character Player);

    private static ShopScreenController CreateUnparentedScreen()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        return scene!.Instantiate<ShopScreenController>();
    }

    private async Task<ShopFixture> OpenMountedShopAsync(int gold)
        => await OpenMountedShopAsync(
            CreatePlayer(gold), ShopCatalog.GetById("village_general_store")!);

    private async Task<ShopFixture> OpenMountedShopAsync(Character player, ShopInventory shop)
        => await OpenMountedShopAsync(player, shop, new Vector2I(1280, 720));

    private async Task<ShopFixture> OpenMountedShopAsync(
        Character player, ShopInventory shop, Vector2I viewportSize)
    {
        var screen = CreateUnparentedScreen();
        AssertThat(screen.TryOpenShop(shop, player)).IsTrue();

        var (container, viewport) = TestHelpers.MountInViewport(screen, viewportSize);
        await AwaitFrames(1);
        return new ShopFixture(container, viewport, screen, player);
    }

    private async Task FreeAsync(ShopFixture fixture)
    {
        if (GodotObject.IsInstanceValid(fixture.Container))
            fixture.Container.QueueFree();
        await AwaitFrames(1);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static Character CreatePlayer(int gold) => new Character
    {
        Name = "ShopScreenTester",
        Level = 1,
        MaxHealth = 100,
        CurrentHealth = 100,
        Attack = 10,
        Defense = 5,
        Speed = 10,
        Gold = gold
    };

    private static bool IsFocusable(Control control) =>
        GodotObject.IsInstanceValid(control) && control.IsVisibleInTree() &&
        control.FocusMode != Control.FocusModeEnum.None &&
        (control is not BaseButton button || !button.Disabled);

    private static Button? RowButton(VBoxContainer list, string itemId)
    {
        foreach (var child in list.GetChildren())
        {
            if (child is not HBoxContainer row)
                continue;

            foreach (var rowChild in row.GetChildren().OfType<Button>())
                if (rowChild.HasMeta("ItemId") && rowChild.GetMeta("ItemId").AsString() == itemId)
                    return rowChild;
        }

        return null;
    }

    private static bool RowContainsLabel(VBoxContainer list, string itemId, string expectedText)
    {
        foreach (var child in list.GetChildren())
        {
            if (child is not HBoxContainer row)
                continue;

            bool isRow = row.GetChildren().OfType<Button>()
                .Any(b => b.HasMeta("ItemId") && b.GetMeta("ItemId").AsString() == itemId);
            if (!isRow)
                continue;

            return row.GetChildren().OfType<Label>().Any(l => l.Text == expectedText);
        }

        return false;
    }

    private static bool ContainsLabelText(VBoxContainer container, string expectedText)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is Label label && label.Text == expectedText)
                return true;
        }

        return false;
    }

    private static bool ContainsAcceptDialog(Node node)
    {
        if (node is AcceptDialog)
            return true;

        foreach (Node child in node.GetChildren())
        {
            if (ContainsAcceptDialog(child))
                return true;
        }

        return false;
    }

    private static T? GetNullableField<T>(object instance, string fieldName)
        where T : class
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Failed to locate private field '{fieldName}'.");

        return field.GetValue(instance) as T;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Failed to locate private field '{fieldName}'.");

        field.SetValue(instance, value);
    }

    private static void InvokePrivate(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Failed to locate private method '{methodName}'.");

        method.Invoke(instance, args);
    }
}
