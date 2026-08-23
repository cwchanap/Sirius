using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public partial class InventoryMenuController : Control
{
	[Signal] public delegate void CloseRequestedEventHandler();

	private static readonly StringName ToggleInventoryAction = "toggle_inventory";
	private static readonly StringName UiCancelAction = "ui_cancel";
	private readonly InputHintPresenter _inputHintPresenter = new();

	private GameManager _gameManager = null!;
	private Button _closeButton = null!;
	private Label _goldLabel = null!;
	private Label _playerName = null!;
	private Label _playerLevel = null!;
	private SiriusStatBar _healthBar = null!;
	private SiriusStatBar _manaBar = null!;
	private ProgressBar _experienceBar = null!;
	private Label _attackValue = null!;
	private Label _defenseValue = null!;
	private Label _speedValue = null!;
	private Label _focusSummary = null!;
	private Label _activeSkillSummary = null!;
	private Button _equipmentTab = null!;
	private Button _itemsTab = null!;
	private Button _skillsTab = null!;
	private Button _detailsTab = null!;
	private Control _compactTabs = null!;
	private Control _equipmentPage = null!;
	private Control _itemsPage = null!;
	private Control _skillsPage = null!;
	private Control _detailsPage = null!;
	private Control _characterColumn = null!;
	private Control _safeFrame = null!;
	private GridContainer _inventoryGrid = null!;
	private OptionButton _activeSkillSelector = null!;
	private TextureRect _detailsIcon = null!;
	private Label _detailsName = null!;
	private Label _detailsMeta = null!;
	private Label _detailsBody = null!;
	private Label _detailsComparison = null!;
	private Label _detailsActionReason = null!;
	private Button _detailsActionButton = null!;

	private readonly Dictionary<EquipmentSlotType, SiriusItemSlotController> _equipmentSlots = new();
	private readonly List<SiriusItemSlotController> _accessorySlots = new();
	private readonly List<SiriusItemSlotController> _inventorySlots = new();

	// Refresh-scoped only. Inventory owns these mutable entries; never retain one
	// from this map across a mutation / RefreshInventoryCatalogue call.
	private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot = new();
	private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId =
		new(StringComparer.Ordinal);

	private PackedScene _itemSlotScene = null!;
	private InventoryPage _activeCompactPage = InventoryPage.Equipment;
	private bool _isCompact;
	private bool _isRefreshingActiveSkillSelector;
	private InventorySemanticKey? _selection;
	private PendingFocusRestore? _pendingFocusRestore;

	public Control InitialFocusTarget => ResolveInitialFocusTarget();

	private Control ResolveInitialFocusTarget()
	{
		var page = _isCompact ? _activeCompactPage : InventoryPage.Equipment;
		Control? target = null;
		if (page == InventoryPage.Items)
			target = _inventorySlots.FirstOrDefault();
		else if (page == InventoryPage.Skills)
			target = _activeSkillSelector;
		else if (page == InventoryPage.Details)
			target = _detailsActionButton.Visible ? _detailsActionButton : _detailsTab;
		else if (_equipmentSlots.TryGetValue(EquipmentSlotType.Weapon, out var weapon))
			target = weapon;
		else
			target = _equipmentSlots.Values.FirstOrDefault();

		if (!CanGrabFocus(target) && page == InventoryPage.Equipment)
			target = _equipmentSlots.Values.FirstOrDefault();

		if (CanGrabFocus(target))
			return target!;

		if (_isCompact)
		{
			var tab = page switch
			{
				InventoryPage.Items => _itemsTab,
				InventoryPage.Skills => _skillsTab,
				InventoryPage.Details => _detailsTab,
				_ => _equipmentTab
			};
			if (CanGrabFocus(tab))
				return tab;
		}

		return _closeButton;
	}

	private enum InventoryPage
	{
		Equipment,
		Items,
		Skills,
		Details
	}

	private readonly record struct InventorySemanticKey(
		EquipmentSlotType? EquipmentSlot,
		int? AccessoryIndex,
		string? ItemId)
	{
		public static InventorySemanticKey ForEquipment(EquipmentSlotType slot) =>
			new(slot, null, null);

		public static InventorySemanticKey ForAccessory(int index) =>
			new(EquipmentSlotType.Accessory, index, null);

		public static InventorySemanticKey ForItem(string itemId) =>
			new(null, null, itemId);
	}

	private readonly record struct PendingFocusRestore(
		InventorySemanticKey Preferred,
		int PreviousCatalogueIndex)
	{
		public PendingFocusRestore WithPreferred(InventorySemanticKey preferred) =>
			this with { Preferred = preferred };
	}

	public override void _Ready()
	{
		_gameManager = GameManager.Instance;
		if (_gameManager == null || !GodotObject.IsInstanceValid(_gameManager))
		{
			GD.PushError("[InventoryMenuController] GameManager not found.");
			QueueFree();
			return;
		}

		BindNodes();
		UiIconPresenter.Apply(GetNode<TextureRect>("%EquipmentTitleIcon"), UiIconId.Equipment, UiIconSize.Default);
		UiIconPresenter.Apply(GetNode<TextureRect>("%InventoryTitleIcon"), UiIconId.General, UiIconSize.Default);
		UiIconPresenter.Apply(_detailsIcon, UiIconId.Info, UiIconSize.Default);
		_itemSlotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn")
			?? throw new InvalidOperationException("Failed to load SiriusItemSlot.tscn.");
		BindSignals();
		InitializeSkillSelector();
		InitializeEquipmentSlots();
		InitializeAccessorySlots();
		RefreshLayout();
		Show();
		RefreshUI();
		Hide();
	}

	public override void _Input(InputEvent @event)
	{
		if (_inputHintPresenter.Observe(@event) && Visible)
			RefreshCloseHint();

		if (Visible && _isCompact && @event is InputEventJoypadButton joy && joy.Pressed)
		{
			if (joy.ButtonIndex == JoyButton.LeftShoulder)
			{
				CycleCompactPage(-1);
				GetViewport().SetInputAsHandled();
			}
			else if (joy.ButtonIndex == JoyButton.RightShoulder)
			{
				CycleCompactPage(1);
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void BindNodes()
	{
		_safeFrame = GetNode<Control>("%SafeFrame");
		_compactTabs = GetNode<Control>("%CompactTabs");
		_equipmentPage = GetNode<Control>("%EquipmentPage");
		_itemsPage = GetNode<Control>("%ItemsPage");
		_skillsPage = GetNode<Control>("%SkillsPage");
		_detailsPage = GetNode<Control>("%DetailsPage");
		_characterColumn = GetNode<Control>("%CharacterColumn");
		_inventoryGrid = GetNode<GridContainer>("%InventoryGrid");
		_activeSkillSelector = GetNode<OptionButton>("%ActiveSkillSelector");
		_activeSkillSummary = GetNode<Label>("%ActiveSkillSummary");
		_focusSummary = GetNode<Label>("%FocusSummary");
		_detailsIcon = GetNode<TextureRect>("%DetailsIcon");
		_detailsName = GetNode<Label>("%DetailsName");
		_detailsMeta = GetNode<Label>("%DetailsMeta");
		_detailsBody = GetNode<Label>("%DetailsBody");
		_detailsComparison = GetNode<Label>("%DetailsComparison");
		_detailsActionReason = GetNode<Label>("%DetailsActionReason");
		_detailsActionButton = GetNode<Button>("%DetailsActionButton");

		_playerName = GetNode<Label>("%PlayerName");
		_playerLevel = GetNode<Label>("%PlayerLevel");
		_healthBar = GetNode<SiriusStatBar>("%HealthBar");
		_manaBar = GetNode<SiriusStatBar>("%ManaBar");
		_experienceBar = GetNode<ProgressBar>("%ExperienceBar");
		_attackValue = GetNode<Label>("%AttackValue");
		_defenseValue = GetNode<Label>("%DefenseValue");
		_speedValue = GetNode<Label>("%SpeedValue");
		_goldLabel = GetNode<Label>("%GoldLabel");
		_closeButton = GetNode<Button>("%CloseButton");

		_equipmentTab = GetNode<Button>("%EquipmentTab");
		_itemsTab = GetNode<Button>("%ItemsTab");
		_skillsTab = GetNode<Button>("%SkillsTab");
		_detailsTab = GetNode<Button>("%DetailsTab");
	}

	private void BindSignals()
	{
		// This view is detached and reattached by UIScreenHost without another
		// _Ready() call. Keep intrinsic connections for the view's entire lifetime;
		// Godot disconnects them when the node is finally freed.
		_equipmentTab.Pressed += OnEquipmentTabPressed;
		_itemsTab.Pressed += OnItemsTabPressed;
		_skillsTab.Pressed += OnSkillsTabPressed;
		_detailsTab.Pressed += OnDetailsTabPressed;
		_detailsActionButton.Pressed += OnDetailsActionPressed;
		_closeButton.Pressed += OnCloseButtonPressed;

		var viewport = GetViewport();
		if (viewport != null)
			viewport.SizeChanged += RefreshLayout;
	}

	private void InitializeSkillSelector()
	{
		_activeSkillSelector.ItemSelected += OnActiveSkillSelectorItemSelected;
		_activeSkillSelector.FocusEntered += OnActiveSkillFocusEntered;
		_activeSkillSelector.MouseEntered += OnActiveSkillMouseEntered;
	}

	private void InitializeEquipmentSlots()
	{
		AddEquipmentSlot("%HelmetSlot", EquipmentSlotType.Helmet);
		AddEquipmentSlot("%WeaponSlot", EquipmentSlotType.Weapon);
		AddEquipmentSlot("%ArmorSlot", EquipmentSlotType.Armor);
		AddEquipmentSlot("%ShieldSlot", EquipmentSlotType.Shield);
		AddEquipmentSlot("%ShoeSlot", EquipmentSlotType.Shoe);

		// The tab and first equipment control are the only compact boundary where
		// the authored layout does not provide a sufficiently distinct spatial
		// direction. Keep this direct pair local; the remaining graph is Godot's
		// normal spatial navigation.
		var weapon = _equipmentSlots[EquipmentSlotType.Weapon];
		_equipmentTab.FocusNeighborBottom = _equipmentTab.GetPathTo(weapon);
		weapon.FocusNeighborTop = weapon.GetPathTo(_equipmentTab);
	}

	private void AddEquipmentSlot(string slotPath, EquipmentSlotType slotType)
	{
		var slot = GetNode<SiriusItemSlotController>(slotPath);
		slot.ToggleMode = true;
		slot.Pressed += () => SelectEquipmentSlot(slotType, null);
		slot.FocusEntered += () => PresentFocusSummary(slot.TooltipText);
		slot.MouseEntered += () => PresentFocusSummary(slot.TooltipText);
		_equipmentSlots[slotType] = slot;
	}

	private void InitializeAccessorySlots()
	{
		_accessorySlots.Clear();
		for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
		{
			var slot = GetNode<SiriusItemSlotController>($"%AccessorySlot{index}");
			var capturedIndex = index;
			slot.ToggleMode = true;
			slot.Pressed += () => SelectEquipmentSlot(EquipmentSlotType.Accessory, capturedIndex);
			slot.FocusEntered += () => PresentFocusSummary(slot.TooltipText);
			slot.MouseEntered += () => PresentFocusSummary(slot.TooltipText);
			_accessorySlots.Add(slot);
		}
	}

	public void OpenMenu()
	{
		Show();
		RefreshLayout();
		RefreshUI();
		RefreshCloseHint();
	}

	private void RefreshCloseHint()
	{
		_inputHintPresenter.ApplyCompactButton(_closeButton, "Close", ToggleInventoryAction, UiCancelAction);
	}

	public void CloseMenu() => Hide();

	private void RefreshLayout()
	{
		// UIScreenHost detaches this view (RemoveChild) while closed but keeps
		// the viewport SizeChanged connection alive for the view's lifetime, so
		// a window resize while closed can reach us outside the scene tree.
		// GetViewportRect() errors and returns an empty Rect2 in that state;
		// bail out before touching it. OpenMenu() re-runs RefreshLayout() after
		// reattachment, so no signal lifecycle work is needed here.
		if (!GodotObject.IsInstanceValid(this) || _safeFrame == null || !IsInsideTree())
			return;

		var viewportSize = GetViewportRect().Size;
		var insets = SiriusUiMetrics.SafeFrameInsets(viewportSize);
		var wasCompact = _isCompact;
		_isCompact = insets.Compact;
		// Crossing the compact breakpoint can hide the current focus owner:
		// standard→compact hides every page except _activeCompactPage, and
		// compact→standard hides CompactTabs. Capture the focus owner's
		// semantic page before any visibility change so we can keep that page
		// visible (standard→compact) or restore content focus (compact→standard)
		// and prevent Godot from silently dropping focus on a hidden control.
		Control? focusOwner = null;
		InventoryPage? focusPage = null;
		if (wasCompact != _isCompact)
		{
			focusOwner = GetViewport()?.GuiGetFocusOwner() as Control;
			if (focusOwner != null)
				focusPage = ResolveFocusPage(focusOwner);
		}

		_safeFrame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_safeFrame.OffsetLeft = insets.SideInset;
		_safeFrame.OffsetTop = insets.Margin;
		_safeFrame.OffsetRight = -insets.SideInset;
		_safeFrame.OffsetBottom = -insets.Margin;

		foreach (var slot in _equipmentSlots.Values)
			slot.SetCompact(_isCompact);
		foreach (var slot in _accessorySlots)
			slot.SetCompact(_isCompact);
		foreach (var slot in _inventorySlots)
			slot.SetCompact(_isCompact);

		_healthBar.Compact = _isCompact;
		_manaBar.Compact = _isCompact;
		_compactTabs.Visible = _isCompact;

		if (focusPage.HasValue && _isCompact)
		{
			// Standard → compact: select the compact page that contains the
			// focus owner so it stays visible and retains focus. SetCompactPage
			// also updates the tab button pressed state for that page.
			SetCompactPage(focusPage.Value);
		}
		else
		{
			ApplyPageVisibility();
		}

		// Compact → standard: CompactTabs is now hidden. If the focus owner was
		// a compact tab (or otherwise no longer visible), restore content focus
		// on the corresponding page. Content controls were hidden only by page
		// visibility in compact mode; in standard mode all pages are visible, so
		// a surviving content focus owner keeps focus naturally.
		if (focusOwner != null && !_isCompact && wasCompact && focusPage.HasValue)
		{
			if (!GodotObject.IsInstanceValid(focusOwner) || !focusOwner.IsVisibleInTree())
				RestoreFocusForPage(focusPage.Value);
		}
	}

	private InventoryPage? ResolveFocusPage(Control focused)
	{
		if (focused == _activeSkillSelector || focused == _skillsTab)
			return InventoryPage.Skills;
		if (focused == _itemsTab)
			return InventoryPage.Items;
		if (focused == _detailsTab || focused == _detailsActionButton)
			return InventoryPage.Details;
		if (focused == _equipmentTab)
			return InventoryPage.Equipment;

		foreach (var pair in _equipmentSlots)
			if (GodotObject.IsInstanceValid(pair.Value) && pair.Value.GetInstanceId() == focused.GetInstanceId())
				return InventoryPage.Equipment;
		foreach (var slot in _accessorySlots)
			if (GodotObject.IsInstanceValid(slot) && slot.GetInstanceId() == focused.GetInstanceId())
				return InventoryPage.Equipment;
		foreach (var slot in _inventorySlots)
			if (GodotObject.IsInstanceValid(slot) && slot.GetInstanceId() == focused.GetInstanceId())
				return InventoryPage.Items;

		return null;
	}

	private void RestoreFocusForPage(InventoryPage page)
	{
		Control? target = page switch
		{
			InventoryPage.Items => _inventorySlots.FirstOrDefault(CanGrabFocus) ?? (Control?)_itemsTab,
			InventoryPage.Skills => _activeSkillSelector,
			InventoryPage.Details => _detailsActionButton.Visible ? _detailsActionButton : _detailsTab,
			_ => _equipmentSlots.TryGetValue(EquipmentSlotType.Weapon, out var weapon)
				? weapon
				: _equipmentSlots.Values.FirstOrDefault()
		};
		// The page-specific target may be unfocusable after a compact→standard
		// resize: an empty Items page falls back to _itemsTab, which is a child
		// of the now-hidden CompactTabs; a Skills page with no active skill has
		// a disabled _activeSkillSelector. In both cases CanGrabFocus rejects the
		// target and focus would be silently dropped. Fall back to the always-
		// focusable CloseButton before grabbing focus, mirroring the final
		// fallback in ResolvePendingFocusRestore.
		if (!CanGrabFocus(target))
			target = _closeButton;
		if (CanGrabFocus(target))
			target!.GrabFocus();
	}

	private void SetCompactPage(InventoryPage page)
	{
		_activeCompactPage = page;
		_equipmentTab.ButtonPressed = page == InventoryPage.Equipment;
		_itemsTab.ButtonPressed = page == InventoryPage.Items;
		_skillsTab.ButtonPressed = page == InventoryPage.Skills;
		_detailsTab.ButtonPressed = page == InventoryPage.Details;
		ApplyPageVisibility();
	}

	private void ApplyPageVisibility()
	{
		if (_isCompact)
		{
			_characterColumn.Visible =
				_activeCompactPage is InventoryPage.Equipment or InventoryPage.Skills;
			_equipmentPage.Visible = _activeCompactPage == InventoryPage.Equipment;
			_itemsPage.Visible = _activeCompactPage == InventoryPage.Items;
			_skillsPage.Visible = _activeCompactPage == InventoryPage.Skills;
			_detailsPage.Visible = _activeCompactPage == InventoryPage.Details;
		}
		else
		{
			_characterColumn.Visible = true;
			_equipmentPage.Visible = true;
			_itemsPage.Visible = true;
			_skillsPage.Visible = true;
			_detailsPage.Visible = true;
		}
	}

	private void CycleCompactPage(int direction)
	{
		var page = (int)_activeCompactPage;
		page = (page + direction + 4) % 4;
		SetCompactPage((InventoryPage)page);
		RestoreCompactPageFocus();
	}

	private void RestoreCompactPageFocus()
	{
		Control? target = _activeCompactPage switch
		{
			InventoryPage.Items => _inventorySlots.FirstOrDefault(CanGrabFocus),
			InventoryPage.Skills => _activeSkillSelector,
			InventoryPage.Details => _detailsActionButton.Visible ? _detailsActionButton : _detailsTab,
			_ => _equipmentSlots.TryGetValue(EquipmentSlotType.Weapon, out var weapon)
				? weapon
				: _equipmentSlots.Values.FirstOrDefault()
		};

		if (!CanGrabFocus(target))
			target = _activeCompactPage switch
			{
				InventoryPage.Items => _itemsTab,
				InventoryPage.Skills => _skillsTab,
				InventoryPage.Details => _detailsTab,
				_ => _equipmentTab
			};

		if (CanGrabFocus(target))
			target!.GrabFocus();
	}

	private void OnEquipmentTabPressed() => SetCompactPage(InventoryPage.Equipment);
	private void OnItemsTabPressed() => SetCompactPage(InventoryPage.Items);
	private void OnSkillsTabPressed() => SetCompactPage(InventoryPage.Skills);
	private void OnDetailsTabPressed()
	{
		SetCompactPage(InventoryPage.Details);
		RestoreCompactPageFocus();
	}

	private void RefreshUI()
	{
		if (_gameManager?.Player == null)
		{
			GD.PushError("[InventoryMenuController] RefreshUI called with no player data — UI will be empty.");
			return;
		}

		SeedPendingFocusRestoreFromCurrentFocus();
		var player = _gameManager.Player;
		var state = new ExplorationHudPlayerState(
			Name: player.Name,
			Level: player.Level,
			CurrentHealth: player.CurrentHealth,
			MaxHealth: player.GetEffectiveMaxHealth(),
			CurrentMana: player.CurrentMana,
			MaxMana: player.MaxMana,
			Experience: player.Experience,
			ExperienceToNext: player.ExperienceToNext);

		SiriusPlayerSummaryPresenter.Apply(
			state,
			_playerName,
			_playerLevel,
			_healthBar,
			_manaBar,
			_experienceBar);

		_attackValue.Text = player.GetEffectiveAttack().ToString();
		_defenseValue.Text = player.GetEffectiveDefense().ToString();
		_speedValue.Text = player.GetEffectiveSpeed().ToString();
		_goldLabel.Text = $"Gold: {player.Gold}";

		RefreshEquipmentSlots();
		RefreshAccessorySlots();
		RefreshActiveSkillSelector();
		RefreshInventoryCatalogue();
		ReconcileSelection();
		RefreshSelectionDetails();
		RefreshSelectionVisuals();
		RefreshFocusSummaryFromCurrentFocus();
		RestorePendingFocus();
	}

	private void RefreshEquipmentSlots()
	{
		var equipment = _gameManager.Player.Equipment;
		foreach (var pair in _equipmentSlots)
		{
			var item = equipment.GetEquipped(pair.Key);
			var slot = pair.Value;
			slot.Disabled = false;
			if (item == null)
			{
				slot.PresentGlyph(
					UiArtCatalog.ForEquipmentSlot(pair.Key),
					"", "", $"{SlotDisplayName(pair.Key)}\nEmpty",
					SiriusItemSlotVisualState.Empty);
			}
			else
			{
				slot.PresentItem(
					item.LoadAssetOrDefault<Texture2D>(),
					"", "", BuildEquipmentTooltip(item),
					SiriusItemSlotVisualState.Equipped);
			}
		}
	}

	private void RefreshAccessorySlots()
	{
		var equipment = _gameManager.Player.Equipment;
		for (var index = 0; index < _accessorySlots.Count; index++)
		{
			var slot = _accessorySlots[index];
			var item = equipment.GetEquipped(EquipmentSlotType.Accessory, index);
			slot.Disabled = false;
			if (item == null)
			{
				slot.PresentGlyph(
					UiIconId.Accessory,
					"", "", $"Accessory Slot {index + 1}\nEmpty",
					SiriusItemSlotVisualState.Empty);
			}
			else
			{
				slot.PresentItem(
					item.LoadAssetOrDefault<Texture2D>(),
					"", "", BuildEquipmentTooltip(item),
					SiriusItemSlotVisualState.Equipped);
			}
		}
	}

	private void RefreshActiveSkillSelector()
	{
		_isRefreshingActiveSkillSelector = true;
		_activeSkillSelector.Clear();
		var player = _gameManager.Player;
		_activeSkillSelector.AddItem("— None —");
		var selectedIndex = 0;

		foreach (var skillId in player.KnownSkillIds)
		{
			var skill = SkillCatalog.GetById(skillId);
			if (skill == null)
			{
				GD.PushWarning($"[InventoryMenuController] Known skill '{skillId}' was not found in SkillCatalog while refreshing the active skill selector.");
				continue;
			}
			if (skill.Type != SkillType.Active)
				continue;

			var itemIndex = _activeSkillSelector.ItemCount;
			_activeSkillSelector.AddItem(skill.DisplayName);
			_activeSkillSelector.SetItemMetadata(itemIndex, skill.SkillId);
			if (skill.SkillId == player.ActiveSkillId)
				selectedIndex = itemIndex;
		}

		_activeSkillSelector.Disabled = _activeSkillSelector.ItemCount == 1;
		_activeSkillSelector.Select(selectedIndex);
		UpdateActiveSkillSelectorTooltip(selectedIndex);
		_activeSkillSummary.Text = selectedIndex == 0
			? "No active skill equipped. Select one to auto-fire it in battle."
			: _activeSkillSelector.TooltipText;
		_isRefreshingActiveSkillSelector = false;
	}

	private void RefreshInventoryCatalogue()
	{
		var entries = new List<InventoryEntry>(_gameManager.Player.Inventory.GetAllEntries());
		entries.Sort((a, b) => string.Compare(
			a.Item.DisplayName,
			b.Item.DisplayName,
			StringComparison.Ordinal));

		while (_inventorySlots.Count < entries.Count)
			_inventorySlots.Add(CreateInventorySlot());

		while (_inventorySlots.Count > entries.Count)
		{
			var last = _inventorySlots[^1];
			_inventorySlots.RemoveAt(_inventorySlots.Count - 1);
			_inventoryEntryBySlot.Remove(last);
			_inventoryGrid.RemoveChild(last);
			last.QueueFree();
		}

		_inventoryEntryBySlot.Clear();
		_inventorySlotByItemId.Clear();
		for (var index = 0; index < entries.Count; index++)
			BindInventorySlot(_inventorySlots[index], entries[index]);
	}

	private SiriusItemSlotController CreateInventorySlot()
	{
		var slot = _itemSlotScene.Instantiate<SiriusItemSlotController>();
		_inventoryGrid.AddChild(slot);
		slot.SetCompact(_isCompact);
		slot.ToggleMode = true;
		slot.Pressed += () => SelectInventorySlot(slot);
		slot.FocusEntered += () => PresentFocusSummary(slot.TooltipText);
		slot.MouseEntered += () => PresentFocusSummary(slot.TooltipText);
		return slot;
	}

	private void BindInventorySlot(SiriusItemSlotController slot, InventoryEntry entry)
	{
		_inventoryEntryBySlot[slot] = entry;
		_inventorySlotByItemId[entry.Item.Id] = slot;

		var quantity = entry.Quantity > 1 ? $"×{entry.Quantity}" : string.Empty;
		var state = entry.Item switch
		{
			EquipmentItem => SiriusItemSlotVisualState.Available,
			ConsumableItem consumable when !IsBattleOnly(consumable)
				=> SiriusItemSlotVisualState.Available,
			_ => SiriusItemSlotVisualState.Unsupported
		};
		var stateText = state == SiriusItemSlotVisualState.Unsupported
			? entry.Item is ConsumableItem ? "BATTLE ONLY" : "UNSUPPORTED"
			: string.Empty;

		slot.SetCompact(_isCompact);
		slot.Disabled = false;
		slot.PresentItem(
			entry.Item.LoadAssetOrDefault<Texture2D>(),
			quantity,
			stateText,
			BuildInventoryTooltip(entry),
			state);
	}

	private static bool IsBattleOnly(ConsumableItem item) => item.RequiresBattle;

	private bool TryResolveSelectedInventoryEntry(
		out SiriusItemSlotController slot,
		out InventoryEntry entry)
	{
		slot = null!;
		entry = null!;

		if (_selection?.ItemId is not { } itemId ||
			!_inventorySlotByItemId.TryGetValue(itemId, out slot))
			return false;

		return _inventoryEntryBySlot.TryGetValue(slot, out entry);
	}

	private void SelectInventorySlot(SiriusItemSlotController slot)
	{
		if (_inventoryEntryBySlot.TryGetValue(slot, out var entry))
			_selection = InventorySemanticKey.ForItem(entry.Item.Id);

		RefreshSelectionDetails();
		RefreshSelectionVisuals();
	}

	private void SelectEquipmentSlot(EquipmentSlotType slotType, int? accessoryIndex)
	{
		var resolvedAccessoryIndex = accessoryIndex ?? 0;
		var item = _gameManager.Player.Equipment.GetEquipped(slotType, resolvedAccessoryIndex);
		if (item != null)
		{
			_selection = slotType == EquipmentSlotType.Accessory
				? InventorySemanticKey.ForAccessory(resolvedAccessoryIndex)
				: InventorySemanticKey.ForEquipment(slotType);
		}

		RefreshSelectionDetails();
		RefreshSelectionVisuals();
	}

	private void OnDetailsActionPressed()
	{
		if (_selection is not { } selection)
			return;

		if (selection.ItemId != null && TryResolveSelectedInventoryEntry(out _, out var entry))
		{
			if (entry.Item is EquipmentItem equipment)
				EquipFromInventory(equipment);
			else if (entry.Item is ConsumableItem consumable && !IsBattleOnly(consumable))
				UseConsumableOutOfBattle(consumable);
			return;
		}

		if (selection.EquipmentSlot is { } slotType)
			HandleUnequip(slotType, selection.AccessoryIndex ?? 0);
	}

	private void ReconcileSelection()
	{
		if (_selection is not { } selection)
			return;

		if (selection.ItemId != null)
		{
			if (TryResolveSelectedInventoryEntry(out _, out _))
				return;

			if (_pendingFocusRestore is { } pending &&
				pending.Preferred.ItemId == selection.ItemId &&
				pending.PreviousCatalogueIndex >= 0 &&
				_inventorySlots.Count > 0)
			{
				var fallbackIndex = Math.Min(
					pending.PreviousCatalogueIndex,
					_inventorySlots.Count - 1);
				if (_inventoryEntryBySlot.TryGetValue(_inventorySlots[fallbackIndex], out var fallbackEntry))
				{
					var preferred = InventorySemanticKey.ForItem(fallbackEntry.Item.Id);
					_selection = preferred;
					_pendingFocusRestore = pending.WithPreferred(preferred);
					return;
				}
			}
		}
		else if (selection.EquipmentSlot is { } slotType &&
			_gameManager.Player.Equipment.GetEquipped(
				slotType,
				selection.AccessoryIndex ?? 0) != null)
		{
			return;
		}

		_selection = null;
	}

	private void RefreshSelectionDetails()
	{
		_detailsName.Text = "Details";
		_detailsMeta.Text = "No selection";
		_detailsBody.Text = "Select an item or equipped slot to view details.";
		_detailsComparison.Text = "Comparison will appear here.";
		_detailsComparison.Visible = false;
		_detailsActionReason.Text = "Select an item or equipped slot to view details.";
		_detailsActionButton.Text = "Action";
		_detailsActionButton.Visible = false;
		UiIconPresenter.Apply(_detailsIcon, UiIconId.Info, UiIconSize.Default);

		if (_selection is not { } selection)
			return;

		if (selection.ItemId != null)
		{
			if (TryResolveSelectedInventoryEntry(out _, out var entry))
				RenderInventoryDetails(entry);
			return;
		}

		if (selection.EquipmentSlot is not { } slotType)
			return;

		var accessoryIndex = selection.AccessoryIndex ?? 0;
		var item = _gameManager.Player.Equipment.GetEquipped(slotType, accessoryIndex);
		if (item != null)
			RenderEquippedDetails(item, slotType, accessoryIndex);
	}

	private void RefreshSelectionVisuals()
	{
		foreach (var slot in _equipmentSlots.Values)
			slot.ButtonPressed = false;
		foreach (var slot in _accessorySlots)
			slot.ButtonPressed = false;
		foreach (var slot in _inventorySlots)
			slot.ButtonPressed = false;

		if (_selection is not { } selection)
			return;

		if (selection.ItemId != null)
		{
			if (_inventorySlotByItemId.TryGetValue(selection.ItemId, out var itemSlot))
				itemSlot.ButtonPressed = true;
			return;
		}

		if (selection.EquipmentSlot == EquipmentSlotType.Accessory &&
			selection.AccessoryIndex is { } accessoryIndex &&
			accessoryIndex >= 0 && accessoryIndex < _accessorySlots.Count)
		{
			_accessorySlots[accessoryIndex].ButtonPressed = true;
		}
		else if (selection.EquipmentSlot is { } slotType &&
			_equipmentSlots.TryGetValue(slotType, out var equipmentSlot))
		{
			equipmentSlot.ButtonPressed = true;
		}
	}

	private void RenderInventoryDetails(InventoryEntry entry)
	{
		var item = entry.Item;
		ApplyDetailsIcon(item);
		_detailsName.Text = item.DisplayName;
		_detailsMeta.Text = $"Category: {item.Category}\nRarity: {item.Rarity}\nQuantity: {entry.Quantity}";
		_detailsBody.Text = BuildDetailsBody(item);

		switch (item)
		{
			case EquipmentItem equipment:
				_detailsActionButton.Text = "Equip";
				_detailsActionButton.Visible = true;
				_detailsActionReason.Text = "Equip this item from the Details page.";
				RenderEquipmentComparison(equipment);
				break;
			case ConsumableItem consumable when !IsBattleOnly(consumable):
				_detailsActionButton.Text = "Use";
				_detailsActionButton.Visible = true;
				_detailsActionReason.Text = "Use this item outside battle.";
				break;
			case ConsumableItem:
				_detailsActionReason.Text = "Can only be used in battle.";
				break;
			default:
				_detailsActionReason.Text = "No inventory action is available for this item.";
				break;
		}
	}

	private void RenderEquippedDetails(EquipmentItem item, EquipmentSlotType slotType, int accessoryIndex)
	{
		ApplyDetailsIcon(item);
		var equippedSlot = slotType == EquipmentSlotType.Accessory
			? $"Accessory {accessoryIndex + 1}"
			: SlotDisplayName(slotType);
		_detailsName.Text = item.DisplayName;
		_detailsMeta.Text = $"Category: {item.Category}\nRarity: {item.Rarity}\nEquipped: {equippedSlot}";
		_detailsBody.Text = BuildDetailsBody(item);
		_detailsActionButton.Text = "Unequip";
		_detailsActionButton.Visible = true;
		_detailsActionReason.Text = "Unequip this item to return it to Inventory.";
	}

	private void ApplyDetailsIcon(Item item)
	{
		var texture = item.LoadAssetOrDefault<Texture2D>();
		if (texture != null)
		{
			UiIconPresenter.ApplyItem(_detailsIcon, texture);
			return;
		}

		var icon = item is EquipmentItem equipment
			? UiArtCatalog.ForEquipmentSlot(equipment.SlotType)
			: UiArtCatalog.ForItemCategory(item.Category);
		UiIconPresenter.Apply(_detailsIcon, icon, UiIconSize.Feature);
	}

	private static string BuildDetailsBody(Item item)
	{
		var lines = new List<string>();
		if (!string.IsNullOrWhiteSpace(item.Description))
			lines.Add(item.Description.Trim());

		if (item is EquipmentItem equipment)
		{
			lines.Add($"Slot: {SlotDisplayName(equipment.SlotType)}");
			var bonuses = GetBonusText(equipment);
			if (!string.IsNullOrEmpty(bonuses))
				lines.Add(bonuses);
		}
		else if (item is ConsumableItem consumable)
		{
			lines.Add($"Effect: {consumable.EffectDescription}");
			if (IsBattleOnly(consumable))
				lines.Add("Battle use only");
		}

		return lines.Count > 0 ? string.Join("\n", lines) : "No additional details.";
	}

	private void RenderEquipmentComparison(EquipmentItem candidate)
	{
		var accessoryIndex = candidate.SlotType == EquipmentSlotType.Accessory
			? ResolveAccessoryEquipIndex()
			: 0;
		var occupant = _gameManager.Player.Equipment.GetEquipped(candidate.SlotType, accessoryIndex);
		var delta = CompareEquipmentBonuses(candidate, occupant);
		var target = candidate.SlotType == EquipmentSlotType.Accessory
			? $"Accessory {accessoryIndex + 1}"
			: SlotDisplayName(candidate.SlotType);

		var comparison = new StringBuilder();
		comparison.AppendLine($"Equip to {target}");
		if (occupant != null)
			comparison.AppendLine($"Replaces: {occupant.DisplayName}");
		comparison.AppendLine(FormatDelta("ATK", delta.Attack));
		comparison.AppendLine(FormatDelta("DEF", delta.Defense));
		comparison.AppendLine(FormatDelta("SPD", delta.Speed));
		comparison.Append(FormatDelta("HP", delta.Health));
		_detailsComparison.Text = comparison.ToString();
		_detailsComparison.Visible = true;
	}

	private static (int Attack, int Defense, int Speed, int Health)
		CompareEquipmentBonuses(EquipmentItem candidate, EquipmentItem? occupant) =>
	(
		candidate.AttackBonus - (occupant?.AttackBonus ?? 0),
		candidate.DefenseBonus - (occupant?.DefenseBonus ?? 0),
		candidate.SpeedBonus - (occupant?.SpeedBonus ?? 0),
		candidate.HealthBonus - (occupant?.HealthBonus ?? 0));

	private static string FormatDelta(string label, int delta) => delta switch
	{
		> 0 => $"{label} +{delta}",
		< 0 => $"{label} {delta}",
		_ => $"{label} unchanged"
	};

	private void HandleUnequip(EquipmentSlotType slotType, int accessoryIndex)
	{
		var removed = slotType == EquipmentSlotType.Accessory
			? _gameManager.Player.Unequip(slotType, accessoryIndex)
			: _gameManager.Player.Unequip(slotType);
		if (removed == null)
			return;

		if (!_gameManager.Player.TryAddItem(removed, 1, out _))
		{
			if (slotType == EquipmentSlotType.Accessory)
				_gameManager.Player.TryEquip(removed, out _, accessoryIndex);
			else
				_gameManager.Player.TryEquip(removed, out _);
			GD.PushWarning("Unable to unequip item: inventory is full or already contains this unique item.");
			RefreshUI();
			return;
		}

		var resultingKey = InventorySemanticKey.ForItem(removed.Id);
		_selection = resultingKey;
		_pendingFocusRestore = new PendingFocusRestore(resultingKey, -1);
		RefreshUI();
	}

	private void UseConsumableOutOfBattle(ConsumableItem item)
	{
		if (_gameManager.IsInBattle)
		{
			GD.PushWarning("[InventoryMenuController] Cannot use consumable during battle from inventory menu");
			return;
		}
		if (IsBattleOnly(item))
		{
			GD.PushWarning($"[InventoryMenuController] '{item.DisplayName}' can only be used in battle (RequiresBattle=true)");
			return;
		}
		var previousIndex = ResolveVisibleInventoryIndex(item.Id);
		if (!_gameManager.Player.TryRemoveItem(item.Id, 1))
		{
			GD.PushWarning($"[InventoryMenuController] Failed to remove '{item.DisplayName}' from inventory; effect not applied");
			return;
		}
		if (!item.Apply(_gameManager.Player))
		{
			GD.PushWarning($"[InventoryMenuController] Failed to apply '{item.DisplayName}' after removal, attempting rollback");
			if (!_gameManager.Player.TryAddItem(item, 1, out _))
				GD.PrintErr($"[InventoryMenuController] ROLLBACK FAILED for '{item.DisplayName}' — item lost permanently!");
			return;
		}

		var resultingKey = InventorySemanticKey.ForItem(item.Id);
		_selection = resultingKey;
		_pendingFocusRestore = new PendingFocusRestore(resultingKey, previousIndex);
		_gameManager.NotifyPlayerStatsChanged();
		RefreshUI();
	}

	private int ResolveVisibleInventoryIndex(string itemId) =>
		_inventorySlotByItemId.TryGetValue(itemId, out var slot)
			? _inventorySlots.IndexOf(slot)
			: -1;

	private int ResolveAccessoryEquipIndex()
	{
		for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
		{
			if (_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, index) == null)
				return index;
		}

		return 0;
	}

	private void EquipFromInventory(EquipmentItem item)
	{
		var accessoryIndex = item.SlotType == EquipmentSlotType.Accessory
			? ResolveAccessoryEquipIndex()
			: 0;
		var previousCatalogueIndex = ResolveVisibleInventoryIndex(item.Id);

		// Remove from inventory before equipping so a swap can always re-add the
		// replaced item, even when the inventory is at its distinct-type limit.
		if (!_gameManager.Player.TryRemoveItem(item.Id, 1))
		{
			GD.PrintErr($"[InventoryMenuController] Failed to remove '{item.DisplayName}' from inventory before equipping.");
			return;
		}

		if (!_gameManager.Player.TryEquip(item, out var replacedItem, accessoryIndex))
		{
			GD.Print($"Failed to equip {item.DisplayName}");
			if (!_gameManager.Player.TryAddItem(item, 1, out _))
				GD.PrintErr($"[InventoryMenuController] ROLLBACK FAILED for '{item.DisplayName}' — item lost permanently!");
			return;
		}

		if (replacedItem != null && !_gameManager.Player.TryAddItem(replacedItem, 1, out _))
			GD.PrintErr($"[InventoryMenuController] Failed to return '{replacedItem.DisplayName}' to inventory after swap — item lost!");

		var resultingKey = item.SlotType == EquipmentSlotType.Accessory
			? InventorySemanticKey.ForAccessory(accessoryIndex)
			: InventorySemanticKey.ForEquipment(item.SlotType);
		_selection = resultingKey;
		_pendingFocusRestore = new PendingFocusRestore(resultingKey, previousCatalogueIndex);
		RefreshUI();
	}

	private void SeedPendingFocusRestoreFromCurrentFocus()
	{
		if (_pendingFocusRestore != null)
			return;

		var focused = GetViewport()?.GuiGetFocusOwner() as Control;
		if (focused == null)
			return;

		if (focused is SiriusItemSlotController itemSlot)
		{
			var slotIndex = _inventorySlots.FindIndex(slot =>
				GodotObject.IsInstanceValid(slot) && slot.GetInstanceId() == itemSlot.GetInstanceId());
			if (slotIndex >= 0 && _inventoryEntryBySlot.TryGetValue(_inventorySlots[slotIndex], out var entry))
			{
				_pendingFocusRestore = new PendingFocusRestore(
					InventorySemanticKey.ForItem(entry.Item.Id),
					slotIndex);
				return;
			}
			// Not a catalogue slot — fall through to equipment/accessory scans.
		}

		foreach (var pair in _equipmentSlots)
		{
			if (GodotObject.IsInstanceValid(pair.Value) &&
				pair.Value.GetInstanceId() == focused.GetInstanceId())
			{
				_pendingFocusRestore = new PendingFocusRestore(
					InventorySemanticKey.ForEquipment(pair.Key), -1);
				return;
			}
		}

		for (var index = 0; index < _accessorySlots.Count; index++)
		{
			if (GodotObject.IsInstanceValid(_accessorySlots[index]) &&
				_accessorySlots[index].GetInstanceId() == focused.GetInstanceId())
			{
				_pendingFocusRestore = new PendingFocusRestore(
					InventorySemanticKey.ForAccessory(index), -1);
				return;
			}
		}
	}

	private void RestorePendingFocus()
	{
		if (_pendingFocusRestore == null)
			return;

		var pending = _pendingFocusRestore.Value;
		if (_isCompact)
		{
			// A semantic item restore must make the catalogue page visible before
			// resolving the target; an equipment activation similarly returns to
			// the character page. This keeps focus restoration meaningful across
			// compact page visibility changes.
			if (pending.Preferred.ItemId != null)
				SetCompactPage(InventoryPage.Items);
			else if (pending.Preferred.EquipmentSlot != null)
				SetCompactPage(InventoryPage.Equipment);
		}

		Control? target = null;
		if (pending.Preferred.EquipmentSlot is { } slotType)
		{
			if (slotType == EquipmentSlotType.Accessory && pending.Preferred.AccessoryIndex is { } accessoryIndex)
				target = accessoryIndex >= 0 && accessoryIndex < _accessorySlots.Count
					? _accessorySlots[accessoryIndex]
					: null;
			else if (_equipmentSlots.TryGetValue(slotType, out var equipmentSlot))
				target = equipmentSlot;
		}
		else if (pending.Preferred.ItemId != null)
		{
			if (_inventorySlotByItemId.TryGetValue(pending.Preferred.ItemId, out var itemTarget))
				target = itemTarget;
		}

		if (!CanGrabFocus(target))
		{
			if (pending.PreviousCatalogueIndex >= 0 && pending.PreviousCatalogueIndex < _inventorySlots.Count)
				target = _inventorySlots[pending.PreviousCatalogueIndex];
			else if (_inventorySlots.Count > 0)
				target = _inventorySlots[^1];
		}

		if (!CanGrabFocus(target))
		{
			target = _activeCompactPage switch
			{
				InventoryPage.Items when _inventorySlots.Count > 0 => _inventorySlots[0],
				InventoryPage.Skills => _activeSkillSelector,
				InventoryPage.Details => _detailsTab,
				_ => _equipmentSlots.Values.FirstOrDefault()
			};
		}

		if (!CanGrabFocus(target) && _isCompact)
			target = _activeCompactPage switch
			{
				InventoryPage.Items => _itemsTab,
				InventoryPage.Skills => _skillsTab,
				InventoryPage.Details => _detailsTab,
				_ => _equipmentTab
			};

		if (!CanGrabFocus(target))
			target = _closeButton;

		if (CanGrabFocus(target))
			target.GrabFocus();

		_pendingFocusRestore = null;
	}

	private static bool CanGrabFocus(Control? target) =>
		target != null && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree() &&
		target.FocusMode != Control.FocusModeEnum.None &&
		(target is not BaseButton button || !button.Disabled);

	private void RefreshFocusSummaryFromCurrentFocus()
	{
		var focused = GetViewport()?.GuiGetFocusOwner() as Control;
		if (focused is SiriusItemSlotController slot)
		{
			PresentFocusSummary(slot.TooltipText);
			return;
		}
		if (focused == _activeSkillSelector)
		{
			PresentFocusSummary(_activeSkillSelector.TooltipText);
			return;
		}
		if (focused == null)
			_focusSummary.Text = string.Empty;
	}

	private void PresentFocusSummary(string text) => _focusSummary.Text = text ?? string.Empty;
	private void OnActiveSkillFocusEntered() => PresentFocusSummary(_activeSkillSelector.TooltipText);
	private void OnActiveSkillMouseEntered() => PresentFocusSummary(_activeSkillSelector.TooltipText);

	private void OnActiveSkillSelectorItemSelected(long index)
	{
		if (_isRefreshingActiveSkillSelector || _gameManager?.Player == null)
			return;

		var skillId = GetActiveSkillIdForIndex((int)index);
		if (string.IsNullOrEmpty(skillId))
		{
			_gameManager.Player.ActiveSkillId = null;
			_gameManager.Player.ActiveSkillExplicitlyNone = true;
			UpdateActiveSkillSelectorTooltip((int)index);
			return;
		}

		if (!_gameManager.Player.EquipActiveSkill(skillId))
		{
			GD.PushWarning($"[InventoryMenuController] Failed to equip active skill '{skillId}'.");
			RefreshActiveSkillSelector();
			return;
		}

		UpdateActiveSkillSelectorTooltip((int)index);
	}

	private string? GetActiveSkillIdForIndex(int index)
	{
		if (index < 0 || index >= _activeSkillSelector.ItemCount)
			return null;
		var metadata = _activeSkillSelector.GetItemMetadata(index);
		return metadata.VariantType == Variant.Type.Nil ? null : metadata.AsString();
	}

	private void UpdateActiveSkillSelectorTooltip(int index)
	{
		var skillId = GetActiveSkillIdForIndex(index);
		if (string.IsNullOrEmpty(skillId))
		{
			_activeSkillSelector.TooltipText = "No active skill equipped. Select one to auto-fire it in battle.";
			_activeSkillSummary.Text = _activeSkillSelector.TooltipText;
			return;
		}

		var skill = SkillCatalog.GetById(skillId);
		if (skill == null)
		{
			_activeSkillSelector.TooltipText = $"Active skill '{skillId}' could not be resolved from SkillCatalog.";
			_activeSkillSummary.Text = _activeSkillSelector.TooltipText;
			return;
		}

		var tooltip = new StringBuilder();
		tooltip.AppendLine(skill.DisplayName);
		tooltip.AppendLine(skill.Description);
		tooltip.AppendLine($"Mana Cost: {skill.ManaCost}");
		tooltip.AppendLine($"Auto-fires every {skill.ActivePeriod} turns");
		if (skill.SkillId == _gameManager.Player.ActiveSkillId)
			tooltip.Append("Currently equipped");
		_activeSkillSelector.TooltipText = tooltip.ToString();
		_activeSkillSummary.Text = _activeSkillSelector.TooltipText;
		PresentFocusSummary(_activeSkillSelector.TooltipText);
	}

	private string BuildEquipmentTooltip(EquipmentItem item)
	{
		var sb = new StringBuilder();
		sb.AppendLine(item.DisplayName);
		if (!string.IsNullOrWhiteSpace(item.Description))
			sb.AppendLine(item.Description.Trim());
		var bonuses = GetBonusText(item);
		if (!string.IsNullOrEmpty(bonuses))
			sb.AppendLine(bonuses);
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
			sb.AppendLine(entry.Item.Description.Trim());

		if (entry.Item is EquipmentItem equipmentItem)
		{
			var bonuses = GetBonusText(equipmentItem);
			if (!string.IsNullOrEmpty(bonuses))
				sb.AppendLine(bonuses);
			sb.Append("Select to view details");
		}
		else if (entry.Item is ConsumableItem consumable)
		{
			sb.AppendLine(consumable.EffectDescription);
			if (IsBattleOnly(consumable))
				sb.AppendLine("Battle use only");
			sb.Append("Select to view details");
		}
		else
		{
			sb.Append("Select to view details");
		}

		return sb.ToString();
	}

	private static string GetBonusText(EquipmentItem item)
	{
		var bonuses = new List<string>();
		if (item.AttackBonus > 0) bonuses.Add($"+{item.AttackBonus} ATK");
		if (item.DefenseBonus > 0) bonuses.Add($"+{item.DefenseBonus} DEF");
		if (item.SpeedBonus > 0) bonuses.Add($"+{item.SpeedBonus} SPD");
		if (item.HealthBonus > 0) bonuses.Add($"+{item.HealthBonus} HP");
		return bonuses.Count > 0 ? string.Join(", ", bonuses) : string.Empty;
	}

	private static string SlotDisplayName(EquipmentSlotType slotType) => slotType switch
	{
		EquipmentSlotType.Helmet => "Helmet",
		EquipmentSlotType.Weapon => "Weapon",
		EquipmentSlotType.Armor => "Armor",
		EquipmentSlotType.Shield => "Shield",
		EquipmentSlotType.Shoe => "Shoes",
		EquipmentSlotType.Accessory => "Accessory",
		_ => slotType.ToString()
	};

	private void OnCloseButtonPressed() => EmitSignal(SignalName.CloseRequested);
}
