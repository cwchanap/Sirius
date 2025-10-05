using Godot;
using System.Collections.Generic;

public partial class InventoryMenuController : Control
{
	private GameManager _gameManager;
	
	// Equipment slot references
	private Dictionary<EquipmentSlotType, EquipmentSlotUI> _equipmentSlots = new();
	
	// Inventory list container
	private VBoxContainer _itemsList;
	private Label _emptyLabel;
	
	// Item slot prefab style boxes
	private StyleBoxFlat _slotEmptyStyle;
	private StyleBoxFlat _slotEquippedStyle;
	private StyleBoxFlat _itemSlotStyle;
	private StyleBoxFlat _itemHoverStyle;

	public override void _Ready()
	{
		_gameManager = GameManager.Instance;
		
		if (_gameManager == null)
		{
			GD.PushError("GameManager not found!");
			QueueFree();
			return;
		}

		// Cache style boxes from the scene
		CacheStyleBoxes();
		
		// Get equipment slot references
		InitializeEquipmentSlots();
		
		// Get inventory list container
		_itemsList = GetNode<VBoxContainer>("%ItemsList");
		_emptyLabel = GetNode<Label>("%EmptyLabel");
		
		// Initial refresh
		RefreshUI();
		
		// Hide by default
		Hide();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("toggle_inventory"))
		{
			if (Visible)
			{
				CloseMenu();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void CacheStyleBoxes()
	{
		// Get the first equipment slot's panel to extract styles
		var weaponSlot = GetNode<PanelContainer>("%WeaponSlot");
		_slotEmptyStyle = weaponSlot.GetThemeStylebox("panel") as StyleBoxFlat;
		
		// Create equipped style (will be applied when item is equipped)
		_slotEquippedStyle = new StyleBoxFlat();
		_slotEquippedStyle.BgColor = new Color(0.2f, 0.25f, 0.35f, 0.9f);
		_slotEquippedStyle.BorderColor = new Color(0.4f, 0.6f, 0.9f, 0.7f);
		_slotEquippedStyle.SetBorderWidthAll(2);
		_slotEquippedStyle.SetCornerRadiusAll(6);
		_slotEquippedStyle.SetContentMarginAll(8);
	}

	private void InitializeEquipmentSlots()
	{
		// Main equipment slots
		_equipmentSlots[EquipmentSlotType.Weapon] = new EquipmentSlotUI
		{
			Panel = GetNode<PanelContainer>("%WeaponSlot"),
			NameLabel = GetNode<Label>("%WeaponSlot/WeaponContent/ItemName"),
			UnequipButton = GetNode<Button>("%WeaponSlot/WeaponContent/UnequipButton"),
			SlotType = EquipmentSlotType.Weapon
		};
		
		_equipmentSlots[EquipmentSlotType.Shield] = new EquipmentSlotUI
		{
			Panel = GetNode<PanelContainer>("%ShieldSlot"),
			NameLabel = GetNode<Label>("%ShieldSlot/ShieldContent/ItemName"),
			UnequipButton = GetNode<Button>("%ShieldSlot/ShieldContent/UnequipButton"),
			SlotType = EquipmentSlotType.Shield
		};
		
		_equipmentSlots[EquipmentSlotType.Armor] = new EquipmentSlotUI
		{
			Panel = GetNode<PanelContainer>("%ArmorSlot"),
			NameLabel = GetNode<Label>("%ArmorSlot/ArmorContent/ItemName"),
			UnequipButton = GetNode<Button>("%ArmorSlot/ArmorContent/UnequipButton"),
			SlotType = EquipmentSlotType.Armor
		};
		
		_equipmentSlots[EquipmentSlotType.Helmet] = new EquipmentSlotUI
		{
			Panel = GetNode<PanelContainer>("%HelmetSlot"),
			NameLabel = GetNode<Label>("%HelmetSlot/HelmetContent/ItemName"),
			UnequipButton = GetNode<Button>("%HelmetSlot/HelmetContent/UnequipButton"),
			SlotType = EquipmentSlotType.Helmet
		};
		
		_equipmentSlots[EquipmentSlotType.Shoe] = new EquipmentSlotUI
		{
			Panel = GetNode<PanelContainer>("%ShoeSlot"),
			NameLabel = GetNode<Label>("%ShoeSlot/ShoeContent/ItemName"),
			UnequipButton = GetNode<Button>("%ShoeSlot/ShoeContent/UnequipButton"),
			SlotType = EquipmentSlotType.Shoe
		};
		
		// Connect unequip buttons
		foreach (var slot in _equipmentSlots.Values)
		{
			slot.UnequipButton.Pressed += () => OnUnequipPressed(slot.SlotType);
		}
	}

	public void OpenMenu()
	{
		RefreshUI();
		Show();
		GetTree().Paused = true;
	}

	public void CloseMenu()
	{
		Hide();
		GetTree().Paused = false;
	}

	private void RefreshUI()
	{
		if (_gameManager?.Player == null)
		{
			return;
		}

		RefreshEquipmentSlots();
		RefreshInventoryList();
	}

	private void RefreshEquipmentSlots()
	{
		var equipment = _gameManager.Player.Equipment;
		
		foreach (var kvp in _equipmentSlots)
		{
			var slotType = kvp.Key;
			var slotUI = kvp.Value;
			var equippedItem = equipment.GetEquipped(slotType);
			
			if (equippedItem != null)
			{
				slotUI.NameLabel.Text = $"{equippedItem.DisplayName} {GetBonusText(equippedItem)}";
				slotUI.UnequipButton.Visible = true;
				slotUI.Panel.AddThemeStyleboxOverride("panel", _slotEquippedStyle);
			}
			else
			{
				slotUI.NameLabel.Text = "Empty";
				slotUI.UnequipButton.Visible = false;
				slotUI.Panel.AddThemeStyleboxOverride("panel", _slotEmptyStyle);
			}
		}
	}

	private void RefreshInventoryList()
	{
		// Clear existing items (except the empty label)
		foreach (var child in _itemsList.GetChildren())
		{
			if (child != _emptyLabel)
			{
				child.QueueFree();
			}
		}

		var inventory = _gameManager.Player.Inventory;
		var entries = inventory.GetAllEntries();
		bool hasItems = false;

		foreach (var entry in entries)
		{
			hasItems = true;
			CreateItemSlot(entry);
		}

		_emptyLabel.Visible = !hasItems;
	}

	private void CreateItemSlot(InventoryEntry entry)
	{
		var itemPanel = new PanelContainer();
		var itemContent = new HBoxContainer();
		itemContent.AddThemeConstantOverride("separation", 12);
		
		// Item name and quantity
		var nameLabel = new Label();
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", 16);
		
		string quantityText = entry.Quantity > 1 ? $" x{entry.Quantity}" : "";
		nameLabel.Text = $"{entry.Item.DisplayName}{quantityText}";
		
		// Add bonus text for equipment
		if (entry.Item is EquipmentItem equipItem)
		{
			nameLabel.Text += $" {GetBonusText(equipItem)}";
			
			// Equip button for equipment items
			var equipButton = new Button();
			equipButton.Text = "Equip";
			equipButton.CustomMinimumSize = new Vector2(80, 0);
			equipButton.Pressed += () => OnEquipPressed(equipItem);
			itemContent.AddChild(equipButton);
		}
		
		// Use button for consumables (future feature)
		if (entry.Item.Category == ItemCategory.Consumable)
		{
			var useButton = new Button();
			useButton.Text = "Use";
			useButton.CustomMinimumSize = new Vector2(80, 0);
			useButton.Disabled = true; // Not implemented yet
			itemContent.AddChild(useButton);
		}
		
		itemContent.AddChild(nameLabel);
		itemPanel.AddChild(itemContent);
		
		// Style the panel
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.18f, 0.18f, 0.22f, 0.85f);
		style.BorderColor = new Color(0.6f, 0.6f, 0.65f, 0.35f);
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		style.SetContentMarginAll(6);
		itemPanel.AddThemeStyleboxOverride("panel", style);
		
		_itemsList.AddChild(itemPanel);
	}

	private string GetBonusText(EquipmentItem item)
	{
		var bonuses = new List<string>();
		
		if (item.AttackBonus > 0) bonuses.Add($"+{item.AttackBonus} ATK");
		if (item.DefenseBonus > 0) bonuses.Add($"+{item.DefenseBonus} DEF");
		if (item.SpeedBonus > 0) bonuses.Add($"+{item.SpeedBonus} SPD");
		if (item.HealthBonus > 0) bonuses.Add($"+{item.HealthBonus} HP");
		
		return bonuses.Count > 0 ? $"({string.Join(", ", bonuses)})" : "";
	}

	private void OnEquipPressed(EquipmentItem item)
	{
		if (_gameManager?.Player == null)
		{
			return;
		}

		// Try to equip the item
		if (_gameManager.Player.TryEquip(item, out var replacedItem))
		{
			// If an item was replaced, add it back to inventory
			if (replacedItem != null)
			{
				_gameManager.Player.TryAddItem(replacedItem, 1, out _);
			}
			
			// Remove the equipped item from inventory
			_gameManager.Player.TryRemoveItem(item.Id, 1);
			
			GD.Print($"Equipped {item.DisplayName}");
			RefreshUI();
		}
		else
		{
			GD.Print($"Failed to equip {item.DisplayName}");
		}
	}

	private void OnUnequipPressed(EquipmentSlotType slotType)
	{
		if (_gameManager?.Player == null)
		{
			return;
		}

		var unequippedItem = _gameManager.Player.Unequip(slotType);
		
		if (unequippedItem != null)
		{
			// Add back to inventory
			if (_gameManager.Player.TryAddItem(unequippedItem, 1, out _))
			{
				GD.Print($"Unequipped {unequippedItem.DisplayName}");
				RefreshUI();
			}
			else
			{
				// If inventory is full, re-equip the item
				_gameManager.Player.TryEquip(unequippedItem, out _);
				GD.Print("Inventory is full! Cannot unequip.");
			}
		}
	}

	private void OnCloseButtonPressed()
	{
		CloseMenu();
	}

	private class EquipmentSlotUI
	{
		public PanelContainer Panel { get; set; }
		public Label NameLabel { get; set; }
		public Button UnequipButton { get; set; }
		public EquipmentSlotType SlotType { get; set; }
	}
}
