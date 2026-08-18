using Godot;
using System;
using System.Linq;

/// <summary>
/// Hosted Sirius shop screen (replaces the native ShopDialog window).
/// Buy price = Item.Value; sell price = <see cref="SellPrice"/>. Rows are
/// controller-local; the shell body scroll owns overflow. Configuration is
/// one-shot via <see cref="TryOpenShop"/> and may happen before _Ready();
/// the host frees the screen after <see cref="ShopClosed"/>.
/// </summary>
public partial class ShopScreenController : Control
{
    [Signal] public delegate void ShopClosedEventHandler();

    private const float FeedbackSeconds = 2.0f;
    private const string RowItemIdMeta = "ItemId";
    // Must match the Sell tab node name in ShopScreen.tscn (TabContainer
    // child "Sell"); ActiveList keys off this name.
    private const string SellTabName = "Sell";

    public Control? InitialFocusTarget { get; private set; }

    /// <summary>The single production sell-price definition.</summary>
    internal static int SellPrice(int itemValue) =>
        Mathf.Max(1, Mathf.FloorToInt(itemValue * 0.5f));

    private SiriusModalShell _shell = null!;
    private Label _goldLabel = null!;
    private Label _feedbackLabel = null!;
    private TabContainer _shopTabs = null!;
    private VBoxContainer _buyList = null!;
    private VBoxContainer _sellList = null!;
    private Button _closeButton = null!;

    private ShopInventory? _shop;
    private Character? _player;
    private bool _started;
    private bool _operationInFlight;
    private bool _terminalEmitted;
    private SceneTreeTimer? _feedbackTimer;
    private Action? _feedbackTimeoutHandler;

    /// <summary>Opens the shop for the given inventory and player. One-shot.</summary>
    public bool TryOpenShop(ShopInventory shop, Character player)
    {
        if (_started)
            return false;

        _started = true;
        _shop = shop;
        _player = player;

        if (IsNodeReady())
            RenderWithFocusRestore();
        return true;
    }

    public void RequestCancel()
    {
        if (_terminalEmitted)
            return;

        _terminalEmitted = true;
        CancelFeedbackTimer();
        EmitSignal(SignalName.ShopClosed);
    }

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _goldLabel = GetNode<Label>("%GoldLabel");
        _feedbackLabel = GetNode<Label>("%FeedbackLabel");
        _shopTabs = GetNode<TabContainer>("%ShopTabs");
        _buyList = GetNode<VBoxContainer>("%BuyList");
        _sellList = GetNode<VBoxContainer>("%SellList");
        _closeButton = GetNode<Button>("%CloseButton");

        _closeButton.Pressed += OnCloseButtonPressed;
        _shopTabs.TabChanged += OnTabChanged;
        Resized += OnResized;

        if (_shop != null)
            RenderWithFocusRestore();
        RefreshLayout();
    }

    public override void _ExitTree()
    {
        if (_closeButton != null)
            _closeButton.Pressed -= OnCloseButtonPressed;
        if (_shopTabs != null)
            _shopTabs.TabChanged -= OnTabChanged;
        Resized -= OnResized;
        CancelFeedbackTimer();
    }

    private void OnCloseButtonPressed() => RequestCancel();

    // Hiding the previously active tab releases its focus owner, and the
    // newly active page is not visible until after the TabChanged signal;
    // re-resolve deferred so the new tab always has a usable focus target.
    private void OnTabChanged(long activeTab)
    {
        var focusedItemId = FindFocusedRowItemId();
        Callable.From(() => ResolveFocus((int)activeTab, focusedItemId)).CallDeferred();
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        if (!IsNodeReady() || _shell == null || !IsInsideTree())
            return;

        var size = GetViewportRect().Size;
        _shell.Compact = SiriusUiMetrics.IsCompact(size);
        _shell.RefreshPresentation(size);
    }

    private void RefreshGoldLabel()
    {
        _goldLabel.Text = $"Your Gold: {_player!.Gold}";
    }

    private void RefreshBuyList()
    {
        ClearContainer(_buyList);

        foreach (var entry in _shop!.Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"[ShopScreen] ItemCatalog has no entry for '{entry.ItemId}'");
                continue;
            }

            int buyPrice = item.Value;
            bool affordable = _player!.Gold >= buyPrice;
            var row = new HBoxContainer();

            var nameLabel = new Label();
            nameLabel.Text = item.DisplayName;
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var priceLabel = new Label();
            priceLabel.Text = $"{buyPrice}g";
            row.AddChild(priceLabel);

            // Standing row-local affordability reason: never cleared by the
            // transient feedback timer, only by a re-render.
            if (!affordable)
            {
                var reasonLabel = new Label();
                reasonLabel.Text = "Not enough gold!";
                row.AddChild(reasonLabel);
            }

            var btn = new Button();
            btn.Text = "Buy";
            btn.Disabled = !affordable;
            btn.SetMeta(RowItemIdMeta, entry.ItemId);
            var capturedId = entry.ItemId;
            var capturedPrice = buyPrice;
            btn.Pressed += () => OnBuyPressed(capturedId, capturedPrice);
            row.AddChild(btn);

            _buyList.AddChild(row);
        }
    }

    private void RefreshSellList()
    {
        ClearContainer(_sellList);

        int addedCount = 0;
        if (_player!.Inventory != null)
        {
            foreach (var entry in _player.Inventory.GetAllEntries())
            {
                if (entry.Item == null || entry.Quantity <= 0) continue;

                int sellPrice = SellPrice(entry.Item.Value);
                var row = new HBoxContainer();

                var nameLabel = new Label();
                nameLabel.Text = $"{entry.Item.DisplayName} x{entry.Quantity}";
                nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(nameLabel);

                var priceLabel = new Label();
                priceLabel.Text = $"{sellPrice}g";
                row.AddChild(priceLabel);

                var btn = new Button();
                btn.Text = "Sell";
                btn.SetMeta(RowItemIdMeta, entry.Item.Id);
                var capturedId = entry.Item.Id;
                var capturedPrice = sellPrice;
                btn.Pressed += () => OnSellPressed(capturedId, capturedPrice);
                row.AddChild(btn);

                _sellList.AddChild(row);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "Nothing to sell.";
            _sellList.AddChild(emptyLabel);
        }
    }

    private void OnBuyPressed(string itemId, int buyPrice)
    {
        if (_operationInFlight || _terminalEmitted)
            return;

        _operationInFlight = true;
        try
        {
            var item = ItemCatalog.CreateItemById(itemId);
            if (item == null)
            {
                GD.PushError($"[ShopScreen] OnBuyPressed: item '{itemId}' was in shop list but CreateItemById returned null.");
                RenderWithFocusRestore();
                return;
            }

            if (!_player!.TrySpendGold(buyPrice))
            {
                ShowFeedback("Not enough gold!");
                return;
            }

            _player.TryAddItem(item, 1, out int added);
            if (added == 0)
            {
                // Roll back gold if item couldn't be added
                _player.GainGold(buyPrice);
                ShowFeedback("Inventory full!");
                return;
            }

            GameManager.Instance?.NotifyPlayerStatsChanged();
            RenderWithFocusRestore();
        }
        finally
        {
            _operationInFlight = false;
        }
    }

    private void OnSellPressed(string itemId, int sellPrice)
    {
        if (_operationInFlight || _terminalEmitted)
            return;

        _operationInFlight = true;
        try
        {
            if (!_player!.TryRemoveItem(itemId, 1))
            {
                GD.PushWarning($"[ShopScreen] TryRemoveItem('{itemId}') returned false — item not in inventory. Refreshing sell list.");
                ShowFeedback("Item no longer available.");
                RenderWithFocusRestore();
                return;
            }

            _player.GainGold(sellPrice);
            GameManager.Instance?.NotifyPlayerStatsChanged();
            RenderWithFocusRestore();
        }
        finally
        {
            _operationInFlight = false;
        }
    }

    private void ShowFeedback(string message)
    {
        CancelFeedbackTimer();

        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;

        _feedbackTimer = GetTree().CreateTimer(FeedbackSeconds);
        _feedbackTimeoutHandler = OnFeedbackTimeout;
        _feedbackTimer.Timeout += _feedbackTimeoutHandler;
    }

    private void OnFeedbackTimeout()
    {
        // Clears the transient feedback label only; standing row-local
        // affordability reasons live in their rows and are untouched.
        _feedbackLabel.Visible = false;
        CancelFeedbackTimer();
    }

    private void CancelFeedbackTimer()
    {
        if (_feedbackTimer != null && _feedbackTimeoutHandler != null)
            _feedbackTimer.Timeout -= _feedbackTimeoutHandler;

        _feedbackTimer = null;
        _feedbackTimeoutHandler = null;
    }

    private void RenderWithFocusRestore()
    {
        var activeTab = _shopTabs.CurrentTab;
        var focusedItemId = FindFocusedRowItemId();

        _shell.Title = _shop!.DisplayName;
        RefreshGoldLabel();
        RefreshBuyList();
        RefreshSellList();
        RefreshLayout();

        ResolveFocus(activeTab, focusedItemId);
    }

    private string? FindFocusedRowItemId()
    {
        if (GetViewport()?.GuiGetFocusOwner() is Button button &&
            button.HasMeta(RowItemIdMeta))
        {
            return button.GetMeta(RowItemIdMeta).AsString();
        }

        return null;
    }

    private void ResolveFocus(int activeTab, string? focusedItemId)
    {
        // Semantic identity: re-grab the same item's action when it survived
        // the rebuild and is focusable; otherwise fall through the chain.
        // FindRowButton already applies the focusability guard, so one call
        // resolves both the check and the assignment.
        Control? target = null;
        if (focusedItemId != null)
            target = FindRowButton(activeTab, focusedItemId);

        if (target == null)
            target = FirstFocusableRow(activeTab);

        if (target == null && CanGrabFocus(_shopTabs))
            target = _shopTabs;

        if (target == null && CanGrabFocus(_closeButton))
            target = _closeButton;

        InitialFocusTarget = target;
        // The resolved target is freshly rendered, in-tree and focusable, so
        // grab synchronously: a deferred grab can race the same-frame deletion
        // of stale queued rows and lose focus ownership.
        target?.GrabFocus();
    }

    // Returns the first FOCUSABLE row button for the item. ClearContainer
    // detaches stale rows up front, so the child list holds only the freshly
    // rendered rows; the focusable skip guards against disabled rows.
    private Button? FindRowButton(int activeTab, string itemId)
    {
        foreach (var child in ActiveList(activeTab).GetChildren())
        {
            if (child is not HBoxContainer row)
                continue;

            foreach (var rowChild in row.GetChildren().OfType<Button>())
                if (rowChild.HasMeta(RowItemIdMeta) &&
                    rowChild.GetMeta(RowItemIdMeta).AsString() == itemId &&
                    CanGrabFocus(rowChild))
                    return rowChild;
        }

        return null;
    }

    private Button? FirstFocusableRow(int activeTab)
    {
        foreach (var child in ActiveList(activeTab).GetChildren())
        {
            if (child is not HBoxContainer row)
                continue;

            foreach (var rowChild in row.GetChildren().OfType<Button>())
                if (CanGrabFocus(rowChild))
                    return rowChild;
        }

        return null;
    }

    private VBoxContainer ActiveList(int activeTab) =>
        _shopTabs.GetTabControl(activeTab)?.Name == SellTabName ? _sellList : _buyList;

    // Equivalent to InventoryMenuController's focusability guard, plus a
    // queued-subtree skip as a generic safety net: a control under any
    // queue-freed ancestor (e.g. a host closing the whole screen this frame)
    // would dangle after the frame ends.
    private static bool CanGrabFocus(Control? target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target) || !target.IsVisibleInTree() ||
            target.FocusMode == Control.FocusModeEnum.None ||
            (target is BaseButton button && button.Disabled))
            return false;

        for (Node? node = target; node != null; node = node.GetParent())
            if (node.IsQueuedForDeletion())
                return false;

        return true;
    }

    // Detach before freeing (DialogueScreenController.ShowNode pattern) so
    // refreshed lists never retain queued rows for the remainder of the frame.
    private static void ClearContainer(VBoxContainer container)
    {
        foreach (Node child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
