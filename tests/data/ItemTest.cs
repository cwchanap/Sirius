using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ItemTest : Node
{
    [TestCase]
    public void TestGeneralItem_Initialization()
    {
        // Arrange & Act
        var item = new GeneralItem
        {
            Id = "health_potion",
            DisplayName = "Health Potion",
            Description = "Restores 50 HP",
            Value = 25,
            MaxStackOverride = 20
        };

        // Assert
        AssertThat(item.Id).IsEqual("health_potion");
        AssertThat(item.DisplayName).IsEqual("Health Potion");
        AssertThat(item.Description).IsEqual("Restores 50 HP");
        AssertThat(item.Value).IsEqual(25);
        AssertThat(item.Category).IsEqual(ItemCategory.General);
        AssertThat(item.MaxStackSize).IsEqual(20);
        AssertThat(item.CanStack).IsTrue();
    }

    [TestCase]
    public void TestGeneralItem_DefaultStackable()
    {
        // Arrange & Act
        var item = new GeneralItem();

        // Assert
        AssertThat(item.CanStack).IsTrue();
        AssertThat(item.MaxStackSize).IsGreater(1);
    }

    [TestCase]
    public void TestEquipmentItem_Initialization()
    {
        // Arrange & Act
        var weapon = new EquipmentItem
        {
            Id = "iron_sword",
            DisplayName = "Iron Sword",
            Description = "A sturdy iron blade",
            Value = 100,
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 15,
            DefenseBonus = 2,
            SpeedBonus = 0,
            HealthBonus = 0
        };

        // Assert
        AssertThat(weapon.Id).IsEqual("iron_sword");
        AssertThat(weapon.DisplayName).IsEqual("Iron Sword");
        AssertThat(weapon.Category).IsEqual(ItemCategory.Equipment);
        AssertThat(weapon.SlotType).IsEqual(EquipmentSlotType.Weapon);
        AssertThat(weapon.AttackBonus).IsEqual(15);
        AssertThat(weapon.DefenseBonus).IsEqual(2);
    }

    [TestCase]
    public void TestEquipmentItem_NotStackable()
    {
        // Arrange & Act
        var equipment = new EquipmentItem();

        // Assert
        AssertThat(equipment.CanStack).IsFalse();
        AssertThat(equipment.MaxStackSize).IsEqual(1);
    }

    [TestCase]
    public void TestEquipmentItem_AllSlotTypes()
    {
        // Arrange & Act
        var weapon = new EquipmentItem { SlotType = EquipmentSlotType.Weapon };
        var shield = new EquipmentItem { SlotType = EquipmentSlotType.Shield };
        var armor = new EquipmentItem { SlotType = EquipmentSlotType.Armor };
        var helmet = new EquipmentItem { SlotType = EquipmentSlotType.Helmet };
        var shoe = new EquipmentItem { SlotType = EquipmentSlotType.Shoe };
        var accessory = new EquipmentItem { SlotType = EquipmentSlotType.Accessory };

        // Assert - All slot types should be valid
        AssertThat(weapon.SlotType).IsEqual(EquipmentSlotType.Weapon);
        AssertThat(shield.SlotType).IsEqual(EquipmentSlotType.Shield);
        AssertThat(armor.SlotType).IsEqual(EquipmentSlotType.Armor);
        AssertThat(helmet.SlotType).IsEqual(EquipmentSlotType.Helmet);
        AssertThat(shoe.SlotType).IsEqual(EquipmentSlotType.Shoe);
        AssertThat(accessory.SlotType).IsEqual(EquipmentSlotType.Accessory);
    }

    [TestCase]
    public void TestEquipmentItem_StatBonuses()
    {
        // Arrange & Act
        var balancedItem = new EquipmentItem
        {
            AttackBonus = 10,
            DefenseBonus = 5,
            SpeedBonus = 3,
            HealthBonus = 20
        };

        // Assert
        AssertThat(balancedItem.AttackBonus).IsEqual(10);
        AssertThat(balancedItem.DefenseBonus).IsEqual(5);
        AssertThat(balancedItem.SpeedBonus).IsEqual(3);
        AssertThat(balancedItem.HealthBonus).IsEqual(20);
    }

    [TestCase]
    public void TestItem_IdAutoGeneration()
    {
        // Arrange & Act
        var item1 = new GeneralItem();
        var item2 = new GeneralItem();

        // Assert - IDs should be auto-generated and unique
        AssertThat(item1.Id).IsNotEmpty();
        AssertThat(item2.Id).IsNotEmpty();
        AssertThat(item1.Id).IsNotEqual(item2.Id);
    }

    [TestCase]
    public void TestItem_IdCanBeSet()
    {
        // Arrange
        var item = new GeneralItem();

        // Act
        item.Id = "custom_item_id";

        // Assert
        AssertThat(item.Id).IsEqual("custom_item_id");
    }

    [TestCase]
    public void TestItem_IdTrimsWhitespace()
    {
        // Arrange
        var item = new GeneralItem();

        // Act
        item.Id = "  test_item  ";

        // Assert
        AssertThat(item.Id).IsEqual("test_item");
    }

    [TestCase]
    public void TestItem_AssetPathCanBeSet()
    {
        // Arrange
        var item = new GeneralItem();

        // Act
        item.AssetPath = "res://assets/items/potion.png";

        // Assert
        AssertThat(item.AssetPath).IsEqual("res://assets/items/potion.png");
    }

    [TestCase]
    public void TestItem_AssetPathTrimsWhitespace()
    {
        // Arrange
        var item = new GeneralItem();

        // Act
        item.AssetPath = "  res://assets/test.png  ";

        // Assert
        AssertThat(item.AssetPath).IsEqual("res://assets/test.png");
    }

    [TestCase]
    public void TestItem_ToString()
    {
        // Arrange
        var item = new GeneralItem
        {
            DisplayName = "Test Item"
        };

        // Act
        string result = item.ToString();

        // Assert
        AssertThat(result).Contains("Test Item");
        AssertThat(result).Contains("General");
    }

    [TestCase]
    public void TestGeneralItem_MaxStackOverride()
    {
        // Arrange & Act
        var item = new GeneralItem { MaxStackOverride = 50 };

        // Assert
        AssertThat(item.MaxStackSize).IsEqual(50);
    }

    [TestCase]
    public void TestGeneralItem_MaxStackMinimumIsOne()
    {
        // Arrange & Act
        var item = new GeneralItem { MaxStackOverride = -10 };

        // Assert - Should be clamped to minimum of 1
        AssertThat(item.MaxStackSize).IsGreaterEqual(1);
    }

    [TestCase]
    public void TestItemCategory_AllCategories()
    {
        // Arrange & Act
        var general = ItemCategory.General;
        var equipment = ItemCategory.Equipment;
        var consumable = ItemCategory.Consumable;
        var quest = ItemCategory.Quest;

        // Assert - All categories should be distinct
        AssertThat((int)general).IsEqual(0);
        AssertThat((int)equipment).IsEqual(1);
        AssertThat((int)consumable).IsEqual(2);
        AssertThat((int)quest).IsEqual(3);
    }

    [TestCase]
    public void TestEquipmentCatalog_WoodenSword()
    {
        // Act
        var sword = EquipmentCatalog.CreateWoodenSword();

        // Assert
        AssertThat(sword.Id).IsEqual("wooden_sword");
        AssertThat(sword.DisplayName).IsEqual("Wooden Sword");
        AssertThat(sword.SlotType).IsEqual(EquipmentSlotType.Weapon);
        AssertThat(sword.AttackBonus).IsEqual(10);
        AssertThat(sword.Category).IsEqual(ItemCategory.Equipment);
        AssertThat(sword.CanStack).IsFalse();
    }

    [TestCase]
    public void TestEquipmentCatalog_WoodenArmor()
    {
        // Act
        var armor = EquipmentCatalog.CreateWoodenArmor();

        // Assert
        AssertThat(armor.Id).IsEqual("wooden_armor");
        AssertThat(armor.DisplayName).IsEqual("Wooden Armor");
        AssertThat(armor.SlotType).IsEqual(EquipmentSlotType.Armor);
        AssertThat(armor.DefenseBonus).IsEqual(8);
    }

    [TestCase]
    public void TestEquipmentCatalog_WoodenShield()
    {
        // Act
        var shield = EquipmentCatalog.CreateWoodenShield();

        // Assert
        AssertThat(shield.Id).IsEqual("wooden_shield");
        AssertThat(shield.SlotType).IsEqual(EquipmentSlotType.Shield);
        AssertThat(shield.DefenseBonus).IsEqual(5);
    }

    [TestCase]
    public void TestEquipmentCatalog_WoodenHelmet()
    {
        // Act
        var helmet = EquipmentCatalog.CreateWoodenHelmet();

        // Assert
        AssertThat(helmet.Id).IsEqual("wooden_helmet");
        AssertThat(helmet.SlotType).IsEqual(EquipmentSlotType.Helmet);
        AssertThat(helmet.HealthBonus).IsEqual(50);
    }

    [TestCase]
    public void TestEquipmentCatalog_WoodenShoes()
    {
        // Act
        var shoes = EquipmentCatalog.CreateWoodenShoes();

        // Assert
        AssertThat(shoes.Id).IsEqual("wooden_shoes");
        AssertThat(shoes.SlotType).IsEqual(EquipmentSlotType.Shoe);
        AssertThat(shoes.SpeedBonus).IsEqual(2);
    }

    [TestCase]
    public void TestEquipmentCatalog_AllStarterGearIsValid()
    {
        // Act
        var starterGear = new[]
        {
            EquipmentCatalog.CreateWoodenSword(),
            EquipmentCatalog.CreateWoodenArmor(),
            EquipmentCatalog.CreateWoodenShield(),
            EquipmentCatalog.CreateWoodenHelmet(),
            EquipmentCatalog.CreateWoodenShoes()
        };

        // Assert - All gear should be valid equipment
        foreach (var gear in starterGear)
        {
            AssertThat(gear).IsNotNull();
            AssertThat(gear.Id).IsNotEmpty();
            AssertThat(gear.DisplayName).IsNotEmpty();
            AssertThat(gear.Category).IsEqual(ItemCategory.Equipment);
            AssertThat(gear.CanStack).IsFalse();
            AssertThat(gear.MaxStackSize).IsEqual(1);
        }
    }
}
