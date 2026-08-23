using GdUnit4;
using Godot;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class InventoryMenuControllerTest : Node
{
    private GameManager _gameManager = null!;
    private InventoryMenuController _inventoryMenu = null!;
    private Variant _originalVerboseOrphans;

    [BeforeTest]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);

        TestHelpers.ResetGameManagerSingleton();

        var sceneTree = (SceneTree)Engine.GetMainLoop();

        _gameManager = new GameManager
        {
            AutoSaveEnabled = false
        };
        sceneTree.Root.AddChild(_gameManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        var inventoryScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        AssertThat(inventoryScene).IsNotNull();

        _inventoryMenu = inventoryScene!.Instantiate<InventoryMenuController>();
        sceneTree.Root.AddChild(_inventoryMenu);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_inventoryMenu != null && IsInstanceValid(_inventoryMenu))
        {
            if (_inventoryMenu.Visible)
            {
                _inventoryMenu.CloseMenu();
            }

            _inventoryMenu.QueueFree();
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.QueueFree();
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        ((SceneTree)Engine.GetMainLoop()).Paused = false;
        _inventoryMenu = null!;
        _gameManager = null!;

        TestHelpers.ResetGameManagerSingleton();
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void OpenAndClose_FromRunningTree_DoesNotChangeTreePauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;

        _inventoryMenu.OpenMenu();
        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(tree.Paused).IsFalse();

        _inventoryMenu.CloseMenu();
        AssertThat(_inventoryMenu.Visible).IsFalse();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void OpenMenuShowsCurrentPlayerGold()
    {
        _gameManager.Player.Gold = 321;

        _inventoryMenu.OpenMenu();

        AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text)
            .IsEqual("Gold: 321");
    }

    [TestCase]
    public void OpenAndClose_FromPausedTree_DoesNotChangeTreePauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = true;
        try
        {
            _inventoryMenu.OpenMenu();
            AssertThat(_inventoryMenu.Visible).IsTrue();
            AssertThat(tree.Paused).IsTrue();

            _inventoryMenu.CloseMenu();

            AssertThat(_inventoryMenu.Visible).IsFalse();
            AssertThat(tree.Paused).IsTrue();
        }
        finally
        {
            tree.Paused = false;
        }
    }

    [TestCase]
    public void CloseButton_EmitsOneCloseRequestedAndLeavesPresentationToHost()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        var closeRequests = 0;

        _inventoryMenu.CloseRequested += () => closeRequests++;

        _inventoryMenu.OpenMenu();
        _inventoryMenu.GetNode<Button>("%CloseButton").EmitSignal(Button.SignalName.Pressed);

        AssertThat(closeRequests).IsEqual(1);
        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void UiCancelWhileVisible_DoesNotCloseOrChangeTreePauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        _inventoryMenu.OpenMenu();

        _inventoryMenu._Input(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void ToggleInventoryWhileVisible_DoesNotCloseOrChangeTreePauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        _inventoryMenu.OpenMenu();

        _inventoryMenu._Input(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });

        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void InputWhileHidden_DoesNotChangePauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;

        _inventoryMenu._Input(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(_inventoryMenu.Visible).IsFalse();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public async Task InventoryMenu_ActiveSkillSelector_EquipsLaterLearnedActiveSkill()
    {
        var player = _gameManager.Player;
        SkillCatalog.GrantSkillsUpToLevel(player, 3);

        _inventoryMenu.OpenMenu();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var selector = _inventoryMenu.GetNode<OptionButton>("%ActiveSkillSelector");
        AssertThat(selector.Disabled).IsFalse();
        AssertThat(selector.ItemCount).IsEqual(3);
        AssertThat(selector.GetItemText(0)).IsEqual("— None —");
        AssertThat(selector.GetItemText(1)).IsEqual("Power Strike");
        AssertThat(selector.GetItemText(2)).IsEqual("Fire Bolt");
        AssertThat(player.ActiveSkillId).IsEqual("power_strike");

        selector.Select(2);
        selector.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);

        AssertThat(player.ActiveSkillId).IsEqual("fire_bolt");
        AssertThat(selector.TooltipText).Contains("Currently equipped");
    }

    [TestCase]
    public void InventoryHeadings_UseReadableLabelsAndGeneratedIcons()
    {
        var equipmentLabel = _inventoryMenu.GetNode<Label>("%EquipmentTitleLabel");
        var itemsLabel = _inventoryMenu.GetNode<Label>("%InventoryTitleLabel");
        var equipmentIcon = _inventoryMenu.GetNode<TextureRect>("%EquipmentTitleIcon");
        var itemsIcon = _inventoryMenu.GetNode<TextureRect>("%InventoryTitleIcon");

        AssertThat(equipmentLabel.Text).IsEqual("Equipment");
        AssertThat(itemsLabel.Text).IsEqual("Items");
        AssertThat(equipmentIcon.Texture.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Equipment, UiIconSize.Default));
        AssertThat(itemsIcon.Texture.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.General, UiIconSize.Default));
    }

    [TestCase]
    public void EmptyEquipmentAndAccessorySlots_ShowTypeGlyphs()
    {
        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();

        var weapon = GetSlotIcon("%WeaponSlot");
        var accessory = GetSlotIcon("%AccessorySlot0");

        AssertThat(weapon.Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(accessory.Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
    }

    [TestCase]
    public void AccessorySlots_AuthorExactlyTheFourDomainSlots()
    {
        AssertThat(_inventoryMenu.GetNode<Container>("%AccessorySlots")
            .GetChildren().OfType<SiriusItemSlotController>().Count())
            .IsEqual(EquipmentSet.AccessorySlotCount);
        AssertThat(_inventoryMenu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
        AssertThat(_inventoryMenu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot5")).IsNull();
    }

    [TestCase]
    public void PopulatedEquipmentSlot_ItemArtOverridesTypeGlyph()
    {
        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();

        _inventoryMenu.OpenMenu();

        var weapon = GetSlotIcon("%WeaponSlot");
        AssertThat(weapon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
        AssertThat(weapon.Texture.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
    }

    [TestCase]
    public void EmptySlotGlyphsUseKeepCenteredWhileItemArtUsesKeepAspectCentered()
    {
        // Empty/locked slots display 32px generated glyphs at native size (KeepCentered),
        // not enlarged to the 96px button. Populated slots scale item art to fit (KeepAspectCentered).
        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();

        var emptyWeapon = GetSlotIcon("%WeaponSlot");
        var emptyAccessory = GetSlotIcon("%AccessorySlot0");
        AssertThat(emptyWeapon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
        AssertThat(emptyAccessory.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);

        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        var populatedWeapon = GetSlotIcon("%WeaponSlot");
        AssertThat(populatedWeapon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
    }

    [TestCase]
    public void ActiveEmptyEquipmentAndAccessorySlots_RenderTypeGlyphsWhenDisabled()
    {
        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();

        var weapon = GetSlot("%WeaponSlot");
        var accessory = GetSlot("%AccessorySlot0");

        AssertThat(weapon.Actionable).IsFalse();
        AssertThat(GetSlotIcon("%WeaponSlot").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(accessory.Actionable).IsFalse();
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));
    }

    [TestCase]
    public void EquipmentSlot_TransitionsAllTextureStatesBetweenItemAndEmptyGlyph()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        var weapon = GetSlot("%WeaponSlot");
        AssertThat(weapon.Actionable).IsTrue();
        AssertThat(GetSlotIcon("%WeaponSlot").Texture!.ResourcePath).IsEqual(sword.AssetPath);

        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();
        AssertThat(weapon.Actionable).IsFalse();
        AssertThat(GetSlotIcon("%WeaponSlot").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(GetSlotIcon("%WeaponSlot").Texture!.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));

        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        AssertThat(weapon.Actionable).IsTrue();
        AssertThat(GetSlotIcon("%WeaponSlot").Texture!.ResourcePath).IsEqual(sword.AssetPath);
    }

    [TestCase]
    public void AccessorySlot_TransitionsAllTextureStatesBetweenItemAndEmptyGlyph()
    {
        var charm = new EquipmentItem
        {
            Id = "test_transition_charm",
            DisplayName = "Test Transition Charm",
            SlotType = EquipmentSlotType.Accessory,
            AssetPath = "res://assets/sprites/items/consumables/warding_charm.png"
        };
        AssertThat(_gameManager.Player.TryEquip(charm, out _, 0)).IsTrue();

        _inventoryMenu.OpenMenu();
        var accessory = GetSlot("%AccessorySlot0");
        AssertThat(accessory.Actionable).IsTrue();
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath).IsEqual(charm.AssetPath);

        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Accessory, 0)).IsNotNull();
        _inventoryMenu.OpenMenu();
        AssertThat(accessory.Actionable).IsFalse();
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));

        AssertThat(_gameManager.Player.TryEquip(charm, out _, 0)).IsTrue();
        _inventoryMenu.OpenMenu();
        AssertThat(accessory.Actionable).IsTrue();
        AssertThat(GetSlotIcon("%AccessorySlot0").Texture!.ResourcePath).IsEqual(charm.AssetPath);
    }

    [TestCase]
    public void OpenMenu_UsesCurrentToggleInventoryBindingInCloseLabel()
    {
        _inventoryMenu.OpenMenu();
        AssertThat(_inventoryMenu.GetNode<Button>("%CloseButton").Text).IsEqual("Close [I]");
    }

    [TestCase]
    public void ReopenMenu_ReReadsChangedToggleInventoryBinding()
    {
        var original = InputMap.ActionGetEvents("toggle_inventory")
            .Select(inputEvent => (InputEvent)inputEvent.Duplicate())
            .ToArray();
        try
        {
            InputMap.ActionEraseEvents("toggle_inventory");
            InputMap.ActionAddEvent("toggle_inventory", new InputEventKey { PhysicalKeycode = Key.K });
            _inventoryMenu.OpenMenu();
            AssertThat(_inventoryMenu.GetNode<Button>("%CloseButton").Text).IsEqual("Close [K]");
        }
        finally
        {
            if (_inventoryMenu.Visible)
                _inventoryMenu.CloseMenu();
            InputMap.ActionEraseEvents("toggle_inventory");
            foreach (var inputEvent in original)
                InputMap.ActionAddEvent("toggle_inventory", inputEvent);
        }
    }

    [TestCase]
    public void CloseHint_ShowsGamepadBindingWhenGamepadIsActive()
    {
        // Real-world config: toggle_inventory has only a keyboard binding.
        // ui_cancel provides the gamepad binding. A gamepad user closing the
        // menu via ui_cancel must see a gamepad keycap, not the keyboard I.
        var cancelExisted = InputMap.HasAction("ui_cancel");
        var cancelOriginal = new System.Collections.Generic.List<InputEvent>();
        if (cancelExisted)
            foreach (var e in InputMap.ActionGetEvents("ui_cancel"))
                cancelOriginal.Add((InputEvent)e.Duplicate());
        try
        {
            if (!cancelExisted)
                InputMap.AddAction("ui_cancel", 0.5f);
            InputMap.ActionEraseEvents("ui_cancel");
            InputMap.ActionAddEvent("ui_cancel", new InputEventKey { PhysicalKeycode = Key.Escape });
            InputMap.ActionAddEvent("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B });

            _inventoryMenu.OpenMenu();
            // Simulate gamepad input to switch the active device.
            _inventoryMenu._Input(new InputEventJoypadButton
            {
                ButtonIndex = JoyButton.A,
                Pressed = true
            });

            AssertThat(_inventoryMenu.GetNode<Button>("%CloseButton").Text).IsEqual("Close [B]");
        }
        finally
        {
            if (_inventoryMenu.Visible)
                _inventoryMenu.CloseMenu();
            InputMap.ActionEraseEvents("ui_cancel");
            if (cancelExisted)
                foreach (var e in cancelOriginal)
                    InputMap.ActionAddEvent("ui_cancel", e);
        }
    }

    private SiriusItemSlotController GetSlot(string slotPath) =>
        _inventoryMenu.GetNode<SiriusItemSlotController>(slotPath);

    private OptionButton InventoryFilterControl() =>
        _inventoryMenu.GetNode<OptionButton>("%InventoryFilter");

    private OptionButton InventorySortControl() =>
        _inventoryMenu.GetNode<OptionButton>("%InventorySort");

    private void SelectInventoryFilter(string label)
    {
        var filter = InventoryFilterControl();
        var index = Enumerable.Range(0, filter.ItemCount)
            .Single(i => filter.GetItemText(i) == label);
        filter.Select(index);
        filter.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
    }

    private void SelectInventorySort(string label)
    {
        var sort = InventorySortControl();
        var index = Enumerable.Range(0, sort.ItemCount)
            .Single(i => sort.GetItemText(i) == label);
        sort.Select(index);
        sort.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
    }

    private string[] VisibleInventoryNames() =>
        _inventoryMenu.GetNode<Container>("%InventoryGrid")
            .GetChildren()
            .OfType<SiriusItemSlotController>()
            .Select(slot => slot.TooltipText.Split('\n')[0])
            .ToArray();

    private Button DetailsActionButton() =>
        _inventoryMenu.GetNode<Button>("%DetailsActionButton");

    private TextureRect GetSlotIcon(string slotPath) =>
        GetSlot(slotPath).GetNode<TextureRect>("%Icon");

    private SiriusItemSlotController FindInventorySlotByTooltip(string text) =>
        _inventoryMenu.GetNode<Container>("%InventoryGrid")
            .GetChildren()
            .OfType<SiriusItemSlotController>()
            .Single(slot => slot.TooltipText.Contains(text, StringComparison.Ordinal));

    private static EquipmentItem CreateAccessory(string id, string name) => new()
    {
        Id = id,
        DisplayName = name,
        SlotType = EquipmentSlotType.Accessory,
        AssetPath = "res://assets/sprites/items/consumables/warding_charm.png"
    };

    private static void FillInventoryToCapacity(Character player)
    {
        for (var index = 0; index < player.Inventory.MaxItemTypes; index++)
        {
            AssertThat(player.TryAddItem(new EquipmentItem
            {
                Id = $"capacity_fill_{index:000}",
                DisplayName = $"Capacity Fill {index:000}",
                SlotType = EquipmentSlotType.Weapon
            }, 1, out _)).IsTrue();
        }
    }

    private sealed partial class QuestTestItem : Item
    {
        public QuestTestItem() => SetCategory(ItemCategory.Quest);
    }

    [TestCase]
    public void OpenMenu_UsesSharedFallbacksAndExactGoldCopy()
    {
        var player = _gameManager.Player;
        player.Name = "   ";
        player.MaxMana = 0;
        player.ExperienceToNext = 0;
        player.Gold = 321;

        _inventoryMenu.OpenMenu();

        AssertThat(_inventoryMenu.GetNode<Label>("%PlayerName").Text).IsEqual("Adventurer");
        AssertThat(_inventoryMenu.GetNode<SiriusStatBar>("%ManaBar").Visible).IsFalse();
        AssertThat(_inventoryMenu.GetNode<ProgressBar>("%ExperienceBar").Visible).IsFalse();
        AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text).IsEqual("Gold: 321");
    }

    [TestCase]
    public void Catalogue_RendersEveryCurrentItemTypeBeyondLegacyTwentyFourLimit()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();

        for (var i = 29; i >= 0; i--)
        {
            var item = new EquipmentItem
            {
                Id = $"inventory_test_{i:00}",
                DisplayName = $"Item {i:00}",
                SlotType = EquipmentSlotType.Weapon
            };
            AssertThat(player.TryAddItem(item, 1, out var added)).IsTrue();
            AssertThat(added).IsEqual(1);
        }

        _inventoryMenu.OpenMenu();

        var slots = _inventoryMenu.GetNode<Container>("%InventoryGrid")
            .GetChildren().OfType<SiriusItemSlotController>().ToArray();
        AssertThat(slots.Length).IsEqual(30);
        AssertThat(slots[0].TooltipText).Contains("Item 00");
        AssertThat(slots[^1].TooltipText).Contains("Item 29");
    }

    [TestCase]
    public void Catalogue_UsesOrdinalDisplayNameOrdering()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            _gameManager.Player.Inventory.Clear();
            var fixture = new[]
            {
                ("order_diaeresis", "ä"),
                ("order_lower_a", "a"),
                ("order_upper_b", "B"),
                ("order_upper_a", "A")
            };
            foreach (var (id, name) in fixture)
            {
                AssertThat(_gameManager.Player.TryAddItem(new EquipmentItem
                {
                    Id = id,
                    DisplayName = name,
                    SlotType = EquipmentSlotType.Weapon
                }, 1, out _)).IsTrue();
            }

            _inventoryMenu.OpenMenu();
            SelectInventorySort("Name");

            var slots = _inventoryMenu.GetNode<Container>("%InventoryGrid")
                .GetChildren().OfType<SiriusItemSlotController>().ToArray();
            var actual = slots.Select(slot => slot.TooltipText.Split('\n')[0]).ToArray();
            var expectedOrdinal = new[] { "A", "B", "a", "ä" };
            var cultureAware = fixture
                .Select(pair => pair.Item2)
                .OrderBy(name => name, StringComparer.CurrentCulture)
                .ToArray();

            AssertThat(cultureAware.SequenceEqual(expectedOrdinal)).IsFalse();
            AssertThat(actual).IsEqual(expectedOrdinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestCase]
    public void InventoryFilter_ShowsAllAndExactCategoryMatches()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();

        AssertThat(player.TryAddItem(new EquipmentItem
        {
            Id = "filter_equipment",
            DisplayName = "Equipment Item",
            SlotType = EquipmentSlotType.Weapon
        }, 1, out _)).IsTrue();

        var consumable = ConsumableCatalog.CreateHealthPotion();
        consumable.DisplayName = "Consumable Item";
        AssertThat(player.TryAddItem(consumable, 1, out _)).IsTrue();

        AssertThat(player.TryAddItem(new GeneralItem
        {
            Id = "filter_general",
            DisplayName = "General Item"
        }, 1, out _)).IsTrue();

        AssertThat(player.TryAddItem(new QuestTestItem
        {
            Id = "filter_quest",
            DisplayName = "Quest Item"
        }, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();

        var filter = InventoryFilterControl();
        AssertThat(filter.ItemCount).IsEqual(5);
        AssertThat(Enumerable.Range(0, filter.ItemCount)
            .Select(filter.GetItemText)
            .ToArray())
            .IsEqual(new[] { "All", "Equipment", "Consumable", "General", "Quest" });

        SelectInventoryFilter("All");
        AssertThat(VisibleInventoryNames())
            .IsEqual(new[] { "Consumable Item", "Equipment Item", "General Item", "Quest Item" });

        SelectInventoryFilter("Equipment");
        AssertThat(VisibleInventoryNames()).IsEqual(new[] { "Equipment Item" });

        SelectInventoryFilter("Consumable");
        AssertThat(VisibleInventoryNames()).IsEqual(new[] { "Consumable Item" });

        SelectInventoryFilter("General");
        AssertThat(VisibleInventoryNames()).IsEqual(new[] { "General Item" });

        SelectInventoryFilter("Quest");
        AssertThat(VisibleInventoryNames()).IsEqual(new[] { "Quest Item" });
    }

    [TestCase]
    public void Catalogue_CategorySort_UsesCategoryThenDisplayName()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();

        AssertThat(player.TryAddItem(new GeneralItem
        {
            Id = "category_general_z",
            DisplayName = "Z General"
        }, 1, out _)).IsTrue();
        AssertThat(player.TryAddItem(new GeneralItem
        {
            Id = "category_general_a",
            DisplayName = "A General"
        }, 1, out _)).IsTrue();
        AssertThat(player.TryAddItem(new EquipmentItem
        {
            Id = "category_equipment",
            DisplayName = "A Equipment",
            SlotType = EquipmentSlotType.Weapon
        }, 1, out _)).IsTrue();

        var consumable = ConsumableCatalog.CreateHealthPotion();
        consumable.DisplayName = "A Consumable";
        AssertThat(player.TryAddItem(consumable, 1, out _)).IsTrue();

        AssertThat(player.TryAddItem(new QuestTestItem
        {
            Id = "category_quest",
            DisplayName = "A Quest"
        }, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        SelectInventorySort("Category");

        AssertThat(VisibleInventoryNames())
            .IsEqual(new[] { "A General", "Z General", "A Equipment", "A Consumable", "A Quest" });
    }

    [TestCase]
    public void NameSort_UsesItemIdAsFinalOrdinalTieBreak()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var lower = new EquipmentItem
        {
            Id = "a_tie",
            DisplayName = "Same",
            Description = "Lower id item",
            SlotType = EquipmentSlotType.Weapon
        };
        var upper = new EquipmentItem
        {
            Id = "b_tie",
            DisplayName = "Same",
            Description = "Upper id item",
            SlotType = EquipmentSlotType.Weapon
        };
        AssertThat(player.TryAddItem(upper, 1, out _)).IsTrue();
        AssertThat(player.TryAddItem(lower, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        var first = _inventoryMenu.GetNode<Container>("%InventoryGrid")
            .GetChildren().OfType<SiriusItemSlotController>().First();
        first.EmitSignal(Button.SignalName.Pressed);

        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsBody").Text)
            .Contains("Lower id item");
    }

    [TestCase]
    public void EquipmentComparison_ShowsGainsLossesAndUnchanged()
    {
        var player = _gameManager.Player;
        var equipped = new EquipmentItem
        {
            Id = "equipped_compare",
            DisplayName = "Current Blade",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 3,
            DefenseBonus = 2,
            SpeedBonus = 1,
            HealthBonus = 0
        };
        var candidate = new EquipmentItem
        {
            Id = "candidate_compare",
            DisplayName = "Candidate Blade",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 5,
            DefenseBonus = 1,
            SpeedBonus = 1,
            HealthBonus = 0
        };
        AssertThat(player.TryEquip(equipped, out _)).IsTrue();
        AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip(candidate.DisplayName)
            .EmitSignal(Button.SignalName.Pressed);

        var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
        AssertThat(comparison).Contains("Will replace Current Blade in Weapon");
        AssertThat(comparison).Contains("ATK +2");
        AssertThat(comparison).Contains("DEF -1");
        AssertThat(comparison).Contains("SPD unchanged");
        AssertThat(comparison).Contains("HP unchanged");
    }

    [TestCase]
    public void EquipmentComparison_EmptyWeaponShowsFillAndAllDeltas()
    {
        var player = _gameManager.Player;
        AssertThat(player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        var candidate = new EquipmentItem
        {
            Id = "empty_weapon_compare",
            DisplayName = "Empty Weapon Candidate",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 4,
            DefenseBonus = 0,
            SpeedBonus = 2,
            HealthBonus = 1
        };
        AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip(candidate.DisplayName)
            .EmitSignal(Button.SignalName.Pressed);

        var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
        AssertThat(comparison).Contains("Will fill Weapon");
        AssertThat(comparison).Contains("ATK +4");
        AssertThat(comparison).Contains("DEF unchanged");
        AssertThat(comparison).Contains("SPD +2");
        AssertThat(comparison).Contains("HP +1");
    }

    [TestCase]
    public async Task EquipmentComparison_AccessoryFirstEmptyTargetMatchesEquipTarget()
    {
        var player = _gameManager.Player;
        var occupiedLater = CreateAccessory("comparison_accessory_later", "Occupied Accessory 2");
        var candidate = CreateAccessory("comparison_accessory_first_empty", "First Empty Accessory");
        occupiedLater.AttackBonus = 3;
        candidate.AttackBonus = 5;
        AssertThat(player.TryEquip(occupiedLater, out _, 1)).IsTrue();
        AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip(candidate.DisplayName)
            .EmitSignal(Button.SignalName.Pressed);

        var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
        AssertThat(comparison).Contains("Will fill Accessory 1");

        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(candidate);
        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 1))
            .IsEqual(occupiedLater);
    }

    [TestCase]
    public async Task EquipmentComparison_FullAccessoriesTargetMatchesEquipTarget()
    {
        var player = _gameManager.Player;
        var originals = new EquipmentItem[EquipmentSet.AccessorySlotCount];
        for (var index = 0; index < originals.Length; index++)
        {
            originals[index] = CreateAccessory(
                $"comparison_accessory_original_{index}",
                $"Comparison Original {index}");
            originals[index].DefenseBonus = index + 1;
            AssertThat(player.TryEquip(originals[index], out _, index)).IsTrue();
        }

        var candidate = CreateAccessory("comparison_accessory_replacement", "Comparison Replacement");
        candidate.DefenseBonus = 10;
        AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip(candidate.DisplayName)
            .EmitSignal(Button.SignalName.Pressed);

        var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
        AssertThat(comparison).Contains("Will replace Comparison Original 0 in Accessory 1");
        AssertThat(comparison).Contains("DEF +9");

        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(candidate);
        for (var index = 1; index < originals.Length; index++)
            AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Accessory, index))
                .IsEqual(originals[index]);
    }

    [TestCase]
    public void PressingInventoryEquipment_SelectsWithoutEquipping()
    {
        var player = _gameManager.Player;
        var candidate = EquipmentCatalog.CreateIronSword();
        AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
        var before = player.Equipment.GetEquipped(EquipmentSlotType.Weapon);
        _inventoryMenu.OpenMenu();

        var slot = FindInventorySlotByTooltip(candidate.DisplayName);
        slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(before);
        AssertThat(player.Inventory.ContainsItem(candidate.Id)).IsTrue();
        AssertThat(slot.ButtonPressed).IsTrue();
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
            .IsEqual(candidate.DisplayName);
        AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
            .IsEqual("Equip");
    }

    [TestCase]
    public void PressingSelectedInventorySlotAgain_RemainsVisuallySelected()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        var slot = FindInventorySlotByTooltip(sword.DisplayName);

        slot.EmitSignal(Button.SignalName.Pressed);
        slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(slot.ButtonPressed).IsTrue();
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
            .IsEqual(sword.DisplayName);
    }

    [TestCase]
    public void PressingEmptyEquipmentSlot_DoesNotStealSelectionVisual()
    {
        var player = _gameManager.Player;
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(player.TryAddItem(sword, 1, out _)).IsTrue();
        var removedShield = player.Unequip(EquipmentSlotType.Shield);
        if (removedShield != null)
            AssertThat(player.TryAddItem(removedShield, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        var selected = FindInventorySlotByTooltip(sword.DisplayName);
        selected.EmitSignal(Button.SignalName.Pressed);

        var emptyShield = GetSlot("%ShieldSlot");
        emptyShield.EmitSignal(Button.SignalName.Pressed);

        AssertThat(selected.ButtonPressed).IsTrue();
        AssertThat(emptyShield.ButtonPressed).IsFalse();
    }

    [TestCase]
    public void PressingUnsupportedInventoryEntry_SelectsAndExplainsUnavailableAction()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var unsupported = new GeneralItem
        {
            Id = "unsupported_selection_item",
            DisplayName = "Unsupported Selection Item",
            Description = "Cannot be used here."
        };
        AssertThat(player.TryAddItem(unsupported, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        var slot = FindInventorySlotByTooltip(unsupported.DisplayName);
        slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(player.Inventory.GetQuantity(unsupported.Id)).IsEqual(1);
        AssertThat(slot.ButtonPressed).IsTrue();
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
            .IsEqual(unsupported.DisplayName);
        AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Visible).IsFalse();
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsActionReason").Text)
            .IsEqual("No inventory action is available for this item.");
    }

    [TestCase]
    public void PressingEquippedEquipmentSlot_SelectsWithoutUnequipping()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(player.TryEquip(sword, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        var slot = GetSlot("%WeaponSlot");
        slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(sword);
        AssertThat(player.Inventory.ContainsItem(sword.Id)).IsFalse();
        AssertThat(slot.ButtonPressed).IsTrue();
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
            .IsEqual(sword.DisplayName);
        AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
            .IsEqual("Unequip");
    }

    [TestCase]
    public void InventoryTooltips_UseSelectionCopyInsteadOfMutationVerbs()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        var potion = ConsumableCatalog.CreateHealthPotion();
        AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
        AssertThat(_gameManager.Player.TryAddItem(potion, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();

        var swordTooltip = FindInventorySlotByTooltip(sword.DisplayName).TooltipText;
        var potionTooltip = FindInventorySlotByTooltip(potion.DisplayName).TooltipText;
        AssertThat(swordTooltip).Contains("Select to view details");
        AssertThat(potionTooltip).Contains("Select to view details");
        AssertThat(swordTooltip.Contains("Click to equip", StringComparison.Ordinal)).IsFalse();
        AssertThat(potionTooltip.Contains("Click to use", StringComparison.Ordinal)).IsFalse();
    }

    [TestCase]
    public void CatalogueUnsupportedEntry_RemainsFocusableButNeverActivates()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var unsupported = new GeneralItem
        {
            Id = "unsupported_menu_item",
            DisplayName = "Unsupported Menu Item"
        };
        AssertThat(player.TryAddItem(unsupported, 1, out _)).IsTrue();

        _inventoryMenu.OpenMenu();

        var slot = FindInventorySlotByTooltip(unsupported.DisplayName);
        var activations = 0;
        slot.Activated += () => activations++;

        AssertThat(slot.Disabled).IsFalse();
        slot.GrabFocus();
        AssertThat(slot.HasFocus()).IsTrue();
        slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(activations).IsEqual(0);
        AssertThat(player.Inventory.GetQuantity(unsupported.Id)).IsEqual(1);
    }

    [TestCase]
    public async Task PrimaryUnequip_WhenInventoryIsFull_RollsBackThroughDetailsAction()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(player.TryEquip(sword, out _)).IsTrue();
        FillInventoryToCapacity(player);
        AssertThat(player.Inventory.ItemTypeCount).IsEqual(player.Inventory.MaxItemTypes);

        _inventoryMenu.OpenMenu();
        var weapon = GetSlot("%WeaponSlot");
        weapon.EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(sword);
        AssertThat(player.Inventory.ContainsItem(sword.Id)).IsFalse();
        AssertThat(weapon.Actionable).IsTrue();
    }

    [TestCase]
    public async Task AccessoryUnequip_WhenInventoryIsFull_RollsBackThroughDetailsAction()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var charm = CreateAccessory("rollback_accessory", "Rollback Accessory");
        AssertThat(player.TryEquip(charm, out _, 0)).IsTrue();
        FillInventoryToCapacity(player);
        AssertThat(player.Inventory.ItemTypeCount).IsEqual(player.Inventory.MaxItemTypes);

        _inventoryMenu.OpenMenu();
        var accessory = GetSlot("%AccessorySlot0");
        accessory.EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0)).IsEqual(charm);
        AssertThat(player.Inventory.ContainsItem(charm.Id)).IsFalse();
        AssertThat(accessory.Actionable).IsTrue();
    }

    [TestCase]
    public async Task FailedConsumableApplication_RollsBackThroughDetailsAction()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var broken = new ConsumableItem
        {
            Id = "broken_menu_consumable",
            DisplayName = "Broken Menu Consumable"
        };
        AssertThat(player.TryAddItem(broken, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        var slot = FindInventorySlotByTooltip(broken.DisplayName);
        slot.EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(player.GetItemQuantity(broken.Id)).IsEqual(1);
        AssertThat(slot.TooltipText).Contains(broken.DisplayName);
    }

    [TestCase]
    public void SelectingNoActiveSkillThroughMenu_PersistsExplicitNone()
    {
        var player = _gameManager.Player;
        SkillCatalog.GrantSkillsUpToLevel(player, 1);
        AssertThat(player.ActiveSkillId).IsEqual("power_strike");
        _inventoryMenu.OpenMenu();

        var selector = _inventoryMenu.GetNode<OptionButton>("%ActiveSkillSelector");
        selector.Select(0);
        selector.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);

        AssertThat(player.ActiveSkillId).IsNull();
        AssertThat(player.ActiveSkillExplicitlyNone).IsTrue();
        SkillCatalog.GrantSkillsUpToLevel(player, 3);
        AssertThat(player.ActiveSkillId).IsNull();
        AssertThat(selector.TooltipText).Contains("No active skill equipped");
    }

    [TestCase]
    public async Task AccessoryEquip_FillsFirstEmptySlotAndFocusesIt()
    {
        var first = CreateAccessory("accessory_first", "Accessory First");
        var second = CreateAccessory("accessory_second", "Accessory Second");
        AssertThat(_gameManager.Player.TryAddItem(first, 1, out _)).IsTrue();
        AssertThat(_gameManager.Player.TryAddItem(second, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip("Accessory First")
            .EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(first);

        FindInventorySlotByTooltip("Accessory Second")
            .EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(first);
        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 1))
            .IsEqual(second);
        AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner())
            .IsEqual(_inventoryMenu.GetNode<SiriusItemSlotController>("%AccessorySlot1"));
    }

    [TestCase]
    public void AccessoryEquip_WhenAllSlotsAreFullFallsBackToExistingSlotZeroReplacement()
    {
        var originals = new EquipmentItem[EquipmentSet.AccessorySlotCount];
        for (var i = 0; i < originals.Length; i++)
        {
            originals[i] = CreateAccessory($"accessory_original_{i}", $"Original {i}");
            AssertThat(_gameManager.Player.TryEquip(originals[i], out _, i)).IsTrue();
        }

        var replacement = CreateAccessory("accessory_replacement", "Replacement");
        AssertThat(_gameManager.Player.TryAddItem(replacement, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        FindInventorySlotByTooltip("Replacement")
            .EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);

        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(replacement);
        for (var i = 1; i < originals.Length; i++)
            AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, i))
                .IsEqual(originals[i]);
    }

    [TestCase]
    public async Task EquipAction_MovesFocusToResultingEquipmentSlot()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip(sword.DisplayName)
            .EmitSignal(Button.SignalName.Pressed);
        var action = DetailsActionButton();
        action.GrabFocus();
        action.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var weapon = GetSlot("%WeaponSlot");
        AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weapon);
        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Weapon))
            .IsEqual(sword);
        AssertThat(action.Text).IsEqual("Unequip");
        AssertThat(action.HasFocus()).IsFalse();
    }

    [TestCase]
    public async Task ConsumingFinalItem_RestoresFocusToNextCatalogueEntry()
    {
        _gameManager.Player.Inventory.Clear();
        var first = ConsumableCatalog.CreateHealthPotion();
        var second = ConsumableCatalog.CreateManaPotion();
        first.DisplayName = "A First";
        second.DisplayName = "B Second";
        AssertThat(_gameManager.Player.TryAddItem(first, 1, out _)).IsTrue();
        AssertThat(_gameManager.Player.TryAddItem(second, 1, out _)).IsTrue();
        _gameManager.Player.CurrentHealth = 1;
        _inventoryMenu.OpenMenu();

        var firstSlot = FindInventorySlotByTooltip("A First");
        firstSlot.GrabFocus();
        firstSlot.EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var secondSlot = FindInventorySlotByTooltip("B Second");
        AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(secondSlot);
        AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
            .Contains("B Second");
    }

    [TestCase]
    public void UnequipResultHiddenByFilter_ClearsSelectionWithoutResettingFilter()
    {
        var player = _gameManager.Player;
        player.Inventory.Clear();
        var potion = ConsumableCatalog.CreateHealthPotion();
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(player.TryAddItem(potion, 1, out _)).IsTrue();
        AssertThat(player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        var filter = InventoryFilterControl();
        var consumableIndex = Enumerable.Range(0, filter.ItemCount)
            .Single(i => filter.GetItemText(i) == "Consumable");
        filter.Select(consumableIndex);
        filter.EmitSignal(OptionButton.SignalName.ItemSelected, (long)consumableIndex);

        GetSlot("%WeaponSlot").EmitSignal(Button.SignalName.Pressed);
        DetailsActionButton().EmitSignal(Button.SignalName.Pressed);

        AssertThat(player.Inventory.ContainsItem(sword.Id)).IsTrue();
        AssertThat(filter.GetItemText(filter.Selected)).IsEqual("Consumable");
        AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text).IsEmpty();
        AssertThat(DetailsActionButton().Visible).IsFalse();
    }

    [TestCase]
    public void Refresh_RePushesSummaryWhenFocusedSlotSurvives()
    {
        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

		var slot = FindInventorySlotByTooltip(sword.DisplayName);
		slot.GrabFocus();
		_inventoryMenu.OpenMenu();

		AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(slot);
        AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
            .Contains(sword.DisplayName);
    }

}
