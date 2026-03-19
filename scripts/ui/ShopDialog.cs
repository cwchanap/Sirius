using Godot;
using System.Collections.Generic;

/// <summary>
/// Modal shop dialog for buying and selling items.
/// Instantiated per NPC interaction; QueueFree'd by NpcInteractionController when done.
/// Buy price = Item.Value. Sell price = floor(Item.Value * 0.5).
/// </summary>
public partial class ShopDialog : AcceptDialog
{
    [Signal] public delegate void ShopClosedEventHandler();

    private Character _player;
    private ShopInventory _shop;
    private Label _goldLabel;
    private VBoxContainer _buyList;
    private VBoxContainer _sellList;
    private Label _feedbackLabel;
    private bool _feedbackActive;

    public override void _Ready()
    {
        Size = new Vector2I(520, 460);
        Exclusive = true;
        GetOkButton().Visible = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        AddChild(root);

        _goldLabel = new Label();
        root.AddChild(_goldLabel);

        _feedbackLabel = new Label();
        _feedbackLabel.Modulate = Colors.Yellow;
        _feedbackLabel.Visible = false;
        root.AddChild(_feedbackLabel);

        var tabs = new TabContainer();
        tabs.CustomMinimumSize = new Vector2(490, 320);
        tabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(tabs);

        // Buy tab
        var buyScroll = new ScrollContainer();
        buyScroll.Name = "Buy";
        buyScroll.CustomMinimumSize = new Vector2(0, 280);
        _buyList = new VBoxContainer();
        _buyList.AddThemeConstantOverride("separation", 4);
        buyScroll.AddChild(_buyList);
        tabs.AddChild(buyScroll);

        // Sell tab
        var sellScroll = new ScrollContainer();
        sellScroll.Name = "Sell";
        sellScroll.CustomMinimumSize = new Vector2(0, 280);
        _sellList = new VBoxContainer();
        _sellList.AddThemeConstantOverride("separation", 4);
        sellScroll.AddChild(_sellList);
        tabs.AddChild(sellScroll);

        var closeBtn = new Button();
        closeBtn.Text = "Close";
        closeBtn.Pressed += () => EmitSignal(SignalName.ShopClosed);
        root.AddChild(closeBtn);

        CloseRequested += OnCloseRequested;
        Canceled += OnCloseRequested;
    }

    /// <summary>Opens the shop for the given inventory and player.</summary>
    public void OpenShop(ShopInventory shop, Character player)
    {
        Title = shop.DisplayName;
        _shop = shop;
        _player = player;
        RefreshGoldLabel();
        RefreshBuyList();
        RefreshSellList();
    }

    private void RefreshGoldLabel()
    {
        _goldLabel.Text = $"Your Gold: {_player.Gold}";
    }

    private void RefreshBuyList()
    {
        ClearContainer(_buyList);

        foreach (var entry in _shop.Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"[ShopDialog] ItemCatalog has no entry for '{entry.ItemId}'");
                continue;
            }

            int buyPrice = item.Value;
            var row = new HBoxContainer();

            var nameLabel = new Label();
            nameLabel.Text = item.DisplayName;
            nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(nameLabel);

            var priceLabel = new Label();
            priceLabel.Text = $"{buyPrice}g";
            row.AddChild(priceLabel);

            var btn = new Button();
            btn.Text = "Buy";
            btn.Disabled = _player.Gold < buyPrice;
            var capturedId = entry.ItemId;
            var capturedPrice = buyPrice;
            var capturedBtn = btn;
            btn.Pressed += () => OnBuyPressed(capturedId, capturedPrice, capturedBtn);
            row.AddChild(btn);

            _buyList.AddChild(row);
        }
    }

    private void RefreshSellList()
    {
        ClearContainer(_sellList);

        if (_player.Inventory == null) return;

        foreach (var entry in _player.Inventory.GetAllEntries())
        {
            if (entry.Item == null || entry.Quantity <= 0) continue;

            int sellPrice = Mathf.Max(1, Mathf.FloorToInt(entry.Item.Value * 0.5f));
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
            var capturedId = entry.Item.Id;
            var capturedPrice = sellPrice;
            btn.Pressed += () => OnSellPressed(capturedId, capturedPrice);
            row.AddChild(btn);

            _sellList.AddChild(row);
        }

        if (_sellList.GetChildCount() == 0)
        {
            var emptyLabel = new Label();
            emptyLabel.Text = "Nothing to sell.";
            _sellList.AddChild(emptyLabel);
        }
    }

    private void OnBuyPressed(string itemId, int buyPrice, Button btn)
    {
        var item = ItemCatalog.CreateItemById(itemId);
        if (item == null) return;

        if (!_player.TrySpendGold(buyPrice))
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
        RefreshGoldLabel();
        RefreshBuyList();
        RefreshSellList();
    }

    private void OnSellPressed(string itemId, int sellPrice)
    {
        if (!_player.TryRemoveItem(itemId, 1)) return;

        _player.GainGold(sellPrice);
        GameManager.Instance?.NotifyPlayerStatsChanged();
        RefreshGoldLabel();
        RefreshSellList();
        RefreshBuyList();
    }

    private void ShowFeedback(string message)
    {
        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = true;
        _feedbackActive = true;

        GetTree().CreateTimer(2.0).Timeout += () =>
        {
            if (_feedbackActive && IsInstanceValid(this))
            {
                _feedbackLabel.Visible = false;
                _feedbackActive = false;
            }
        };
    }

    private static void ClearContainer(VBoxContainer container)
    {
        foreach (Node child in container.GetChildren())
            child.QueueFree();
    }

    private void OnCloseRequested()
    {
        EmitSignal(SignalName.ShopClosed);
    }

    public override void _ExitTree()
    {
        CloseRequested -= OnCloseRequested;
        Canceled -= OnCloseRequested;
    }
}
