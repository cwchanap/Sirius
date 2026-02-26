using Godot;
using System;
using System.Collections.Generic;
using System.Text;

public partial class InventoryMenuController : Control
{
	private GameManager _gameManager;

	private readonly Dictionary<EquipmentSlotType, EquipmentSlotUI> _equipmentSlots = new();
	private readonly List<AccessorySlotUI> _accessorySlots = new();
	private readonly List<InventorySlotUI> _inventorySlots = new();

	private static readonly Vector2 EquipmentPanelSize = new(108, 108);
	private static readonly Vector2 EquipmentButtonSize = new(96, 96);
	private static readonly Vector2 AccessoryPanelSize = new(108, 108);
	private static readonly Vector2 AccessoryButtonSize = new(96, 96);
	private static readonly Vector2 InventoryPanelSize = new(108, 108);
	private static readonly Vector2 InventoryButtonSize = new(96, 96);

	private InventoryEntry[] _inventorySlotEntries = Array.Empty<InventoryEntry>();

	private StyleBoxFlat _basePanelStyle;
	private StyleBoxFlat _equippedPanelStyle;
	private StyleBoxFlat _lockedPanelStyle;

	public override void _Ready()
	{
		_gameManager = GameManager.Instance;

		if (_gameManager == null)
		{
			GD.PushError("GameManager not found!");
			QueueFree();
			return;
		}

		CacheStyles();
		InitializeEquipmentSlots();
		InitializeAccessorySlots();
		InitializeInventorySlots();
		RefreshUI();
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

	private void CacheStyles()
	{
		var basePanel = GetNode<PanelContainer>("%WeaponSlot");
		if (basePanel.GetThemeStylebox("panel") is StyleBoxFlat baseStyle)
		{
			_basePanelStyle = (StyleBoxFlat)baseStyle.Duplicate();
		}
		else
		{
			_basePanelStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.12f, 0.13f, 0.17f, 0.95f),
				BorderColor = new Color(0.32f, 0.42f, 0.6f, 0.6f)
			};
			_basePanelStyle.SetBorderWidthAll(1);
			_basePanelStyle.SetCornerRadiusAll(6);
		}

		_equippedPanelStyle = (StyleBoxFlat)_basePanelStyle.Duplicate();
		_equippedPanelStyle.BgColor = new Color(0.22f, 0.28f, 0.4f, 0.95f);
		_equippedPanelStyle.BorderColor = new Color(0.48f, 0.68f, 0.95f, 1f);

		_lockedPanelStyle = (StyleBoxFlat)_basePanelStyle.Duplicate();
		_lockedPanelStyle.BgColor = new Color(0.08f, 0.08f, 0.08f, 0.8f);
		_lockedPanelStyle.BorderColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
	}

	private void InitializeEquipmentSlots()
	{
		AddEquipmentSlot("%HelmetSlot", EquipmentSlotType.Helmet);
		AddEquipmentSlot("%WeaponSlot", EquipmentSlotType.Weapon);
		AddEquipmentSlot("%ArmorSlot", EquipmentSlotType.Armor);
		AddEquipmentSlot("%ShieldSlot", EquipmentSlotType.Shield);
		AddEquipmentSlot("%ShoeSlot", EquipmentSlotType.Shoe);
	}

	private void AddEquipmentSlot(string panelPath, EquipmentSlotType slotType)
	{
		var panel = GetNode<PanelContainer>(panelPath);
		var button = panel.GetNode<TextureButton>("Button");
		ConfigureSlotButton(button);
		panel.CustomMinimumSize = EquipmentPanelSize;
		button.CustomMinimumSize = EquipmentButtonSize;

		var slot = new EquipmentSlotUI
		{
			Panel = panel,
			Button = button,
			SlotType = slotType
		};

		button.Pressed += () => OnEquipmentSlotPressed(slotType);
		_equipmentSlots[slotType] = slot;
	}

	private void InitializeAccessorySlots()
	{
		_accessorySlots.Clear();
		var accessoryGrid = GetNode<GridContainer>("%AccessoryGrid");

		for (int i = 0; i < accessoryGrid.GetChildCount(); i++)
		{
			if (accessoryGrid.GetChild(i) is not PanelContainer panel)
			{
				continue;
			}

			var button = panel.GetNode<TextureButton>("Button");
			ConfigureSlotButton(button);
			panel.CustomMinimumSize = AccessoryPanelSize;
			button.CustomMinimumSize = AccessoryButtonSize;
			bool isActive = i < EquipmentSet.AccessorySlotCount;

			var slot = new AccessorySlotUI
			{
				Panel = panel,
				Button = button,
				Index = i,
				IsActive = isActive
			};

			int indexCopy = i;
			button.Pressed += () => OnAccessorySlotPressed(indexCopy);
			_accessorySlots.Add(slot);
		}
	}

	private void InitializeInventorySlots()
	{
		_inventorySlots.Clear();
		var grid = GetNode<GridContainer>("%InventoryGrid");
		int slotCount = grid.GetChildCount();
		_inventorySlotEntries = new InventoryEntry[slotCount];
		GD.Print($"InitializeInventorySlots: found {slotCount} slots");

		for (int i = 0; i < slotCount; i++)
		{
			if (grid.GetChild(i) is not PanelContainer panel)
			{
				continue;
			}

			var button = panel.GetNode<TextureButton>("Button");
			ConfigureSlotButton(button);
			panel.CustomMinimumSize = InventoryPanelSize;
			button.CustomMinimumSize = InventoryButtonSize;
			var slot = new InventorySlotUI
			{
				Panel = panel,
				Button = button,
				Index = i
			};

			int indexCopy = i;
			button.Pressed += () => OnInventorySlotPressed(indexCopy);
			_inventorySlots.Add(slot);
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
		RefreshAccessorySlots();
		RefreshInventoryGrid();
	}

	private void RefreshEquipmentSlots()
	{
		var equipment = _gameManager.Player.Equipment;

		foreach (var slotPair in _equipmentSlots)
		{
			var slotType = slotPair.Key;
			var slot = slotPair.Value;
			var equippedItem = equipment.GetEquipped(slotType);

			if (equippedItem != null)
			{
				ApplyPanelStyle(slot.Panel, _equippedPanelStyle);
				SetButtonIcon(slot.Button, equippedItem);
				slot.Button.TooltipText = BuildEquipmentTooltip(equippedItem);
				slot.Button.Disabled = false;
			}
			else
			{
				ApplyPanelStyle(slot.Panel, _basePanelStyle);
				ClearButtonIcon(slot.Button);
				slot.Button.TooltipText = $"{SlotDisplayName(slotType)}\nEmpty";
				slot.Button.Disabled = true;
			}
		}
	}

	private void RefreshAccessorySlots()
	{
		var equipment = _gameManager.Player.Equipment;

		foreach (var slot in _accessorySlots)
		{
			if (!slot.IsActive)
			{
				ApplyPanelStyle(slot.Panel, _lockedPanelStyle);
				ClearButtonIcon(slot.Button);
				slot.Button.Disabled = true;
				slot.Button.TooltipText = "Accessory Slot Locked";
				continue;
			}

			var accessory = equipment.GetEquipped(EquipmentSlotType.Accessory, slot.Index);
			if (accessory != null)
			{
				ApplyPanelStyle(slot.Panel, _equippedPanelStyle);
				SetButtonIcon(slot.Button, accessory);
				slot.Button.TooltipText = BuildEquipmentTooltip(accessory);
				slot.Button.Disabled = false;
			}
			else
			{
				ApplyPanelStyle(slot.Panel, _basePanelStyle);
				ClearButtonIcon(slot.Button);
				slot.Button.TooltipText = $"Accessory Slot {slot.Index + 1}\nEmpty";
				slot.Button.Disabled = true;
			}
		}
	}

	private void RefreshInventoryGrid()
	{
		if (_inventorySlots.Count == 0)
		{
			GD.PushWarning("Inventory slots list empty, reinitializing...");
			InitializeInventorySlots();
		}

		Array.Fill(_inventorySlotEntries, null);
		var entries = new List<InventoryEntry>(_gameManager.Player.Inventory.GetAllEntries());
		GD.Print($"RefreshInventoryGrid: {entries.Count} entries");
		GD.Print($"Inventory UI slots tracked: {_inventorySlots.Count}");
		entries.Sort((a, b) => string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal));

		for (int i = 0; i < _inventorySlots.Count; i++)
		{
			var slot = _inventorySlots[i];
			if (i < entries.Count)
			{
				var entry = entries[i];
				_inventorySlotEntries[i] = entry;
				GD.Print($"Inventory slot {i}: {entry.Item.DisplayName} x{entry.Quantity}");
				SetButtonIcon(slot.Button, entry.Item);
				slot.Button.TooltipText = BuildInventoryTooltip(entry);
				slot.Button.Disabled = entry.Item is not EquipmentItem and not ConsumableItem;
			}
			else
			{
				ClearButtonIcon(slot.Button);
				slot.Button.TooltipText = "Empty";
				slot.Button.Disabled = true;
			}
		}

		if (entries.Count > _inventorySlots.Count)
		{
			GD.PushWarning($"Inventory UI only displays {_inventorySlots.Count} item types. {entries.Count - _inventorySlots.Count} hidden.");
		}
	}

	private void OnEquipmentSlotPressed(EquipmentSlotType slotType)
	{
		if (_gameManager?.Player == null)
		{
			return;
		}

		var equipped = _gameManager.Player.Equipment.GetEquipped(slotType);
		if (equipped == null)
		{
			return;
		}

		HandleUnequip(slotType, 0);
	}

	private void OnAccessorySlotPressed(int accessoryIndex)
	{
		if (_gameManager?.Player == null)
		{
			return;
		}

		if (accessoryIndex < 0 || accessoryIndex >= EquipmentSet.AccessorySlotCount)
		{
			return;
		}

		var equipped = _gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, accessoryIndex);
		if (equipped == null)
		{
			return;
		}

		HandleUnequip(EquipmentSlotType.Accessory, accessoryIndex);
	}

	private void HandleUnequip(EquipmentSlotType slotType, int accessoryIndex)
	{
		var removed = slotType == EquipmentSlotType.Accessory
			? _gameManager.Player.Unequip(slotType, accessoryIndex)
			: _gameManager.Player.Unequip(slotType);

		if (removed == null)
		{
			return;
		}

		bool addedToInventory = _gameManager.Player.TryAddItem(removed, 1, out _);
		GD.Print($"HandleUnequip: added={addedToInventory}, inventoryTypes={_gameManager.Player.Inventory.ItemTypeCount}");
		if (addedToInventory)
		{
			GD.Print($"Unequipped {removed.DisplayName}");
		}
		else
		{
			if (slotType == EquipmentSlotType.Accessory)
			{
				_gameManager.Player.TryEquip(removed, out _, accessoryIndex);
			}
			else
			{
				_gameManager.Player.TryEquip(removed, out _);
			}
			GD.PushWarning("Unable to unequip item: inventory is full or already contains this unique item.");
		}

		RefreshUI();
	}
	private void OnInventorySlotPressed(int slotIndex)
	{
		if (_gameManager?.Player == null) return;
		if (slotIndex < 0 || slotIndex >= _inventorySlotEntries.Length) return;

		var entry = _inventorySlotEntries[slotIndex];
		if (entry == null) return;

		if (entry.Item is EquipmentItem equipmentItem)
		{
			EquipFromInventory(equipmentItem);
			return;
		}

		if (entry.Item is ConsumableItem consumable)
		{
			UseConsumableOutOfBattle(consumable);
		}
	}

	private void UseConsumableOutOfBattle(ConsumableItem item)
	{
		if (_gameManager.IsInBattle)
		{
			GD.PushWarning("[InventoryMenuController] Cannot use consumable during battle from inventory menu");
			return;
		}

		if (item.Effect?.RequiresBattle == true)
		{
			GD.Print($"[InventoryMenuController] '{item.DisplayName}' can only be used in battle");
			return;
		}

		// Remove item first to prevent duplication if effect application succeeds but removal fails
		if (!_gameManager.Player.TryRemoveItem(item.Id, 1))
		{
			GD.PushWarning($"[InventoryMenuController] Failed to remove '{item.DisplayName}' from inventory; effect not applied");
			return;
		}

		if (!item.Apply(_gameManager.Player))
		{
			GD.PushWarning($"[InventoryMenuController] Failed to apply '{item.DisplayName}' after removal, attempting rollback");
			_gameManager.Player.TryAddItem(item, 1, out _);
			return;
		}

		GD.Print($"[InventoryMenuController] Used {item.DisplayName} out of battle");

		_gameManager.NotifyPlayerStatsChanged();
		RefreshUI();
	}

	private void EquipFromInventory(EquipmentItem item)
	{
		if (!_gameManager.Player.TryEquip(item, out var replacedItem))
		{
			GD.Print($"Failed to equip {item.DisplayName}");
			return;
		}

		if (replacedItem != null)
		{
			_gameManager.Player.TryAddItem(replacedItem, 1, out _);
		}

		_gameManager.Player.TryRemoveItem(item.Id, 1);
		GD.Print($"Equipped {item.DisplayName}");
		RefreshUI();
	}

	private void SetButtonIcon(TextureButton button, Item item)
	{
		if (button == null)
		{
			return;
		}

		var texture = item?.LoadAssetOrDefault<Texture2D>();
		if (item != null && texture == null && !string.IsNullOrWhiteSpace(item.AssetPath))
		{
			GD.PushWarning($"Failed to load icon for '{item.DisplayName}' at '{item.AssetPath}'");
		}
		button.TextureNormal = texture;
		button.TextureHover = texture;
		button.TexturePressed = texture;
		button.TextureDisabled = texture;
		button.TextureFocused = texture;
	}

	private void ClearButtonIcon(TextureButton button)
	{
		if (button == null)
		{
			return;
		}

		button.TextureNormal = null;
		button.TextureHover = null;
		button.TexturePressed = null;
		button.TextureDisabled = null;
		button.TextureFocused = null;
	}

	private void ConfigureSlotButton(TextureButton button)
	{
		if (button == null)
		{
			return;
		}

		button.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
		button.IgnoreTextureSize = true;
	}

	private void ApplyPanelStyle(PanelContainer panel, StyleBoxFlat style)
	{
		if (panel == null || style == null)
		{
			return;
		}

		panel.AddThemeStyleboxOverride("panel", (StyleBox)style.Duplicate());
	}

	private string BuildEquipmentTooltip(EquipmentItem item)
	{
		var sb = new StringBuilder();
		sb.AppendLine(item.DisplayName);

		if (!string.IsNullOrWhiteSpace(item.Description))
		{
			sb.AppendLine(item.Description.Trim());
		}

		var bonuses = GetBonusText(item);
		if (!string.IsNullOrEmpty(bonuses))
		{
			sb.AppendLine(bonuses);
		}

		sb.Append($"Slot: {SlotDisplayName(item.SlotType)}");
		return sb.ToString();
	}

	private string BuildInventoryTooltip(InventoryEntry entry)
	{
		var sb = new StringBuilder();
		sb.AppendLine(entry.Item.DisplayName);
		sb.AppendLine($"Quantity: {entry.Quantity}");
		sb.AppendLine($"Category: {entry.Item.Category}");

		if (!string.IsNullOrWhiteSpace(entry.Item.Description))
		{
			sb.AppendLine(entry.Item.Description.Trim());
		}

		if (entry.Item is EquipmentItem equipmentItem)
		{
			var bonuses = GetBonusText(equipmentItem);
			if (!string.IsNullOrEmpty(bonuses))
			{
				sb.AppendLine(bonuses);
			}

			sb.Append("Click to equip");
		}
		else if (entry.Item is ConsumableItem consumable)
		{
			sb.AppendLine(consumable.EffectDescription);
			sb.Append(consumable.Effect?.RequiresBattle == true ? "Battle use only" : "Click to use");
		}

		return sb.ToString();
	}

	private string GetBonusText(EquipmentItem item)
	{
		var bonuses = new List<string>();

		if (item.AttackBonus > 0) bonuses.Add($"+{item.AttackBonus} ATK");
		if (item.DefenseBonus > 0) bonuses.Add($"+{item.DefenseBonus} DEF");
		if (item.SpeedBonus > 0) bonuses.Add($"+{item.SpeedBonus} SPD");
		if (item.HealthBonus > 0) bonuses.Add($"+{item.HealthBonus} HP");

		return bonuses.Count > 0 ? string.Join(", ", bonuses) : string.Empty;
	}

	private static string SlotDisplayName(EquipmentSlotType slotType)
	{
		return slotType switch
		{
			EquipmentSlotType.Helmet => "Helmet",
			EquipmentSlotType.Weapon => "Weapon",
			EquipmentSlotType.Armor => "Armor",
			EquipmentSlotType.Shield => "Shield",
			EquipmentSlotType.Shoe => "Shoes",
			EquipmentSlotType.Accessory => "Accessory",
			_ => slotType.ToString()
		};
	}

	private void OnCloseButtonPressed()
	{
		CloseMenu();
	}

	private class EquipmentSlotUI
	{
		public PanelContainer Panel { get; set; }
		public TextureButton Button { get; set; }
		public EquipmentSlotType SlotType { get; set; }
	}

	private class AccessorySlotUI
	{
		public PanelContainer Panel { get; set; }
		public TextureButton Button { get; set; }
		public int Index { get; set; }
		public bool IsActive { get; set; }
	}

	private class InventorySlotUI
	{
		public PanelContainer Panel { get; set; }
		public TextureButton Button { get; set; }
		public int Index { get; set; }
	}
}
