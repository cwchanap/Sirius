using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ItemCatalogTest : Node
{
    [TestCase]
    public void TestCreateItemById_WoodenSword()
    {
        // Act
        var item = ItemCatalog.CreateItemById("wooden_sword");

        // Assert
        AssertThat(item).IsNotNull();
        AssertThat(item.Id).IsEqual("wooden_sword");
        AssertThat(item.DisplayName).IsEqual("Wooden Sword");
        AssertThat(item).IsInstanceOf<EquipmentItem>();

        var equipment = item as EquipmentItem;
        AssertThat(equipment).IsNotNull();
        AssertThat(equipment!.SlotType).IsEqual(EquipmentSlotType.Weapon);
        AssertThat(equipment.AttackBonus).IsEqual(10);
    }

    [TestCase]
    public void TestCreateItemById_WoodenArmor()
    {
        // Act
        var item = ItemCatalog.CreateItemById("wooden_armor");

        // Assert
        AssertThat(item).IsNotNull();
        AssertThat(item.Id).IsEqual("wooden_armor");

        var equipment = item as EquipmentItem;
        AssertThat(equipment).IsNotNull();
        AssertThat(equipment!.SlotType).IsEqual(EquipmentSlotType.Armor);
        AssertThat(equipment.DefenseBonus).IsEqual(8);
    }

    [TestCase]
    public void TestCreateItemById_WoodenShield()
    {
        // Act
        var item = ItemCatalog.CreateItemById("wooden_shield");

        // Assert
        AssertThat(item).IsNotNull();
        AssertThat(item.Id).IsEqual("wooden_shield");

        var equipment = item as EquipmentItem;
        AssertThat(equipment).IsNotNull();
        AssertThat(equipment!.SlotType).IsEqual(EquipmentSlotType.Shield);
        AssertThat(equipment.DefenseBonus).IsEqual(5);
    }

    [TestCase]
    public void TestCreateItemById_WoodenHelmet()
    {
        // Act
        var item = ItemCatalog.CreateItemById("wooden_helmet");

        // Assert
        AssertThat(item).IsNotNull();
        AssertThat(item.Id).IsEqual("wooden_helmet");

        var equipment = item as EquipmentItem;
        AssertThat(equipment).IsNotNull();
        AssertThat(equipment!.SlotType).IsEqual(EquipmentSlotType.Helmet);
        AssertThat(equipment.HealthBonus).IsEqual(50);
    }

    [TestCase]
    public void TestCreateItemById_WoodenShoes()
    {
        // Act
        var item = ItemCatalog.CreateItemById("wooden_shoes");

        // Assert
        AssertThat(item).IsNotNull();
        AssertThat(item.Id).IsEqual("wooden_shoes");

        var equipment = item as EquipmentItem;
        AssertThat(equipment).IsNotNull();
        AssertThat(equipment!.SlotType).IsEqual(EquipmentSlotType.Shoe);
        AssertThat(equipment.SpeedBonus).IsEqual(2);
    }

    [TestCase]
    public void TestCreateItemById_ReturnsNullForUnknownId()
    {
        // Act
        var item = ItemCatalog.CreateItemById("nonexistent_item");

        // Assert
        AssertThat(item).IsNull();
    }

    [TestCase]
    public void TestCreateItemById_ReturnsNullForNullId()
    {
        // Act
        var item = ItemCatalog.CreateItemById(null);

        // Assert
        AssertThat(item).IsNull();
    }

    [TestCase]
    public void TestCreateItemById_ReturnsNullForEmptyId()
    {
        // Act
        var item = ItemCatalog.CreateItemById("");

        // Assert
        AssertThat(item).IsNull();
    }

    [TestCase]
    public void TestCreateItemById_ReturnsNullForWhitespaceId()
    {
        // Act
        var item = ItemCatalog.CreateItemById("   ");

        // Assert
        AssertThat(item).IsNull();
    }

    [TestCase]
    public void TestItemExists_ReturnsTrueForKnownItems()
    {
        // Assert
        AssertThat(ItemCatalog.ItemExists("wooden_sword")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("wooden_armor")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("wooden_shield")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("wooden_helmet")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("wooden_shoes")).IsTrue();
    }

    [TestCase]
    public void TestItemExists_ReturnsFalseForUnknownItems()
    {
        // Assert
        AssertThat(ItemCatalog.ItemExists("nonexistent_item")).IsFalse();
        AssertThat(ItemCatalog.ItemExists("")).IsFalse();
        AssertThat(ItemCatalog.ItemExists(null)).IsFalse();
        AssertThat(ItemCatalog.ItemExists("   ")).IsFalse();
    }

    [TestCase]
    public void TestGetAllItemIds_ReturnsAllRegisteredItems()
    {
        // Act
        var ids = ItemCatalog.GetAllItemIds();
        int count = 0;
        foreach (var id in ids)
        {
            count++;
            AssertThat(id).IsNotEmpty();
        }

        // Assert - At least the 5 starter items
        AssertThat(count).IsGreaterEqual(5);
    }

    [TestCase]
    public void TestCreateItemById_CreatesNewInstanceEachTime()
    {
        // Act
        var item1 = ItemCatalog.CreateItemById("wooden_sword");
        var item2 = ItemCatalog.CreateItemById("wooden_sword");

        // Assert - Should be different instances
        AssertThat(item1).IsNotNull();
        AssertThat(item2).IsNotNull();
        AssertThat(item1).IsNotSame(item2);
    }
}
