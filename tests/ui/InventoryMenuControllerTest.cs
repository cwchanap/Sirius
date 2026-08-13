using GdUnit4;
using Godot;
using System;
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
    public async Task AccessoryEquip_FillsFirstEmptySlotAndFocusesIt()
    {
        var first = CreateAccessory("accessory_first", "Accessory First");
        var second = CreateAccessory("accessory_second", "Accessory Second");
        AssertThat(_gameManager.Player.TryAddItem(first, 1, out _)).IsTrue();
        AssertThat(_gameManager.Player.TryAddItem(second, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        FindInventorySlotByTooltip("Accessory First")
            .EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(first);

        FindInventorySlotByTooltip("Accessory Second")
            .EmitSignal(Button.SignalName.Pressed);
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

        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
            .IsEqual(replacement);
        for (var i = 1; i < originals.Length; i++)
            AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, i))
                .IsEqual(originals[i]);
    }

    [TestCase]
    public async Task EquipActivation_RestoresFocusToResultingEquipmentSlot()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
        _inventoryMenu.OpenMenu();

        var itemSlot = FindInventorySlotByTooltip(sword.DisplayName);
        itemSlot.GrabFocus();
        itemSlot.EmitSignal(Button.SignalName.Pressed);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var weapon = _inventoryMenu.GetNode<SiriusItemSlotController>("%WeaponSlot");
        AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weapon);
        AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
            .Contains(sword.DisplayName);
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
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var secondSlot = FindInventorySlotByTooltip("B Second");
        AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(secondSlot);
        AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
            .Contains("B Second");
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
