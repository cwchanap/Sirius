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
    public void OpenAndClose_FromRunningTree_RestoresRunningTree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;

        _inventoryMenu.OpenMenu();
        AssertThat(_inventoryMenu.Visible).IsTrue();
        AssertThat(tree.Paused).IsTrue();

        _inventoryMenu.CloseMenu();
        AssertThat(_inventoryMenu.Visible).IsFalse();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void OpenAndClose_FromPausedParent_RestoresPausedParent()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = true;
        try
        {
            _inventoryMenu.OpenMenu();
            _inventoryMenu.OpenMenu();
            _inventoryMenu.CloseMenu();

            AssertThat(tree.Paused).IsTrue();
        }
        finally
        {
            tree.Paused = false;
        }
    }

    [TestCase]
    public void CloseMenu_CalledTwice_DoesNotOverwriteRestoredPauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = true;
        try
        {
            _inventoryMenu.OpenMenu();
            _inventoryMenu.CloseMenu();
            _inventoryMenu.CloseMenu();

            AssertThat(tree.Paused).IsTrue();
        }
        finally
        {
            tree.Paused = false;
        }
    }

    [TestCase]
    public void ExitTree_WhileOpen_RestoresIncomingPauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        _inventoryMenu.OpenMenu();

        _inventoryMenu.Free();

        AssertThat(tree.Paused).IsFalse();
        _inventoryMenu = null!;
    }

    [TestCase]
    public void UiCancelWhileVisible_ClosesAndRestoresIncomingPauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        _inventoryMenu.OpenMenu();

        _inventoryMenu._Input(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(_inventoryMenu.Visible).IsFalse();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public void ToggleInventoryWhileVisible_ClosesAndRestoresIncomingPauseState()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;
        _inventoryMenu.OpenMenu();

        _inventoryMenu._Input(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });

        AssertThat(_inventoryMenu.Visible).IsFalse();
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

        var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
            .GetNode<TextureButton>("Button");
        var accessory = _inventoryMenu.GetNode<PanelContainer>("%AccessorySlot0")
            .GetNode<TextureButton>("Button");

        AssertThat(weapon.TextureNormal.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(accessory.TextureNormal.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
    }

    [TestCase]
    public void InactiveAccessoryPlaceholders_ShowLockWithoutUnlockRule()
    {
        for (var index = EquipmentSet.AccessorySlotCount; index < 6; index++)
        {
            var button = _inventoryMenu.GetNode<PanelContainer>($"%AccessorySlot{index}")
                .GetNode<TextureButton>("Button");
            AssertThat(button.Disabled).IsTrue();
            AssertThat(button.TextureDisabled.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));
            AssertThat(button.TooltipText).IsEqual("Accessory Slot Locked");
        }
    }

    [TestCase]
    public void PopulatedEquipmentSlot_ItemArtOverridesTypeGlyph()
    {
        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();

        _inventoryMenu.OpenMenu();

        var weapon = _inventoryMenu.GetNode<PanelContainer>("%WeaponSlot")
            .GetNode<TextureButton>("Button");
        AssertThat(weapon.TextureNormal.ResourcePath).IsEqual(sword.AssetPath);
        AssertThat(weapon.TextureNormal.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
    }

    [TestCase]
    public void EmptySlotGlyphsUseKeepCenteredWhileItemArtUsesKeepAspectCentered()
    {
        // Empty/locked slots display 32px generated glyphs at native size (KeepCentered),
        // not enlarged to the 96px button. Populated slots scale item art to fit (KeepAspectCentered).
        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();

        var emptyWeapon = GetSlotButton("%WeaponSlot");
        var lockedAccessory = GetSlotButton("%AccessorySlot5");
        AssertThat(emptyWeapon.StretchMode).IsEqual(TextureButton.StretchModeEnum.KeepCentered);
        AssertThat(lockedAccessory.StretchMode).IsEqual(TextureButton.StretchModeEnum.KeepCentered);

        var sword = EquipmentCatalog.CreateWoodenSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        var populatedWeapon = GetSlotButton("%WeaponSlot");
        AssertThat(populatedWeapon.StretchMode).IsEqual(TextureButton.StretchModeEnum.KeepAspectCentered);
    }

    [TestCase]
    public void ActiveEmptyEquipmentAndAccessorySlots_RenderTypeGlyphsWhenDisabled()
    {
        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();

        var weapon = GetSlotButton("%WeaponSlot");
        var accessory = GetSlotButton("%AccessorySlot0");

        AssertThat(weapon.Disabled).IsTrue();
        AssertThat(weapon.TextureDisabled.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(accessory.Disabled).IsTrue();
        AssertThat(accessory.TextureDisabled.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
        AssertThat(accessory.TextureDisabled.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));
    }

    [TestCase]
    public void EquipmentSlot_TransitionsAllTextureStatesBetweenItemAndEmptyGlyph()
    {
        var sword = EquipmentCatalog.CreateIronSword();
        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();

        _inventoryMenu.OpenMenu();
        var weapon = GetSlotButton("%WeaponSlot");
        AssertThat(weapon.Disabled).IsFalse();
        AssertAllButtonTexturePaths(weapon, sword.AssetPath);

        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Weapon)).IsNotNull();
        _inventoryMenu.OpenMenu();
        AssertThat(weapon.Disabled).IsTrue();
        AssertAllButtonTexturePaths(
            weapon,
            UiArtCatalog.GetIconPath(UiIconId.Weapon, UiIconSize.Feature));
        AssertThat(weapon.TextureDisabled.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));

        AssertThat(_gameManager.Player.TryEquip(sword, out _)).IsTrue();
        _inventoryMenu.OpenMenu();
        AssertThat(weapon.Disabled).IsFalse();
        AssertAllButtonTexturePaths(weapon, sword.AssetPath);
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
        var accessory = GetSlotButton("%AccessorySlot0");
        AssertThat(accessory.Disabled).IsFalse();
        AssertAllButtonTexturePaths(accessory, charm.AssetPath);

        AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Accessory, 0)).IsNotNull();
        _inventoryMenu.OpenMenu();
        AssertThat(accessory.Disabled).IsTrue();
        AssertAllButtonTexturePaths(
            accessory,
            UiArtCatalog.GetIconPath(UiIconId.Accessory, UiIconSize.Feature));
        AssertThat(accessory.TextureDisabled.ResourcePath)
            .IsNotEqual(UiArtCatalog.GetIconPath(UiIconId.Locked, UiIconSize.Feature));

        AssertThat(_gameManager.Player.TryEquip(charm, out _, 0)).IsTrue();
        _inventoryMenu.OpenMenu();
        AssertThat(accessory.Disabled).IsFalse();
        AssertAllButtonTexturePaths(accessory, charm.AssetPath);
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

    private TextureButton GetSlotButton(string slotPath) =>
        _inventoryMenu.GetNode<PanelContainer>(slotPath).GetNode<TextureButton>("Button");

    private static void AssertAllButtonTexturePaths(TextureButton button, string expectedPath)
    {
        AssertThat(button.TextureNormal.ResourcePath).IsEqual(expectedPath);
        AssertThat(button.TextureHover.ResourcePath).IsEqual(expectedPath);
        AssertThat(button.TexturePressed.ResourcePath).IsEqual(expectedPath);
        AssertThat(button.TextureDisabled.ResourcePath).IsEqual(expectedPath);
        AssertThat(button.TextureFocused.ResourcePath).IsEqual(expectedPath);
    }
}
