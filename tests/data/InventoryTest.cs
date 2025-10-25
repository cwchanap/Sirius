using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
public partial class InventoryTest : Node
{
    [TestCase]
    public void TestInventory_InitiallyEmpty()
    {
        // Arrange & Act
        var inventory = new Inventory();

        // Assert
        AssertThat(inventory.ItemTypeCount).IsEqual(0);
    }

    [TestCase]
    public void TestTryAddItem_AddsNewItem()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);

        // Act
        bool added = inventory.TryAddItem(item, 5, out int addedQuantity);

        // Assert
        AssertThat(added).IsTrue();
        AssertThat(addedQuantity).IsEqual(5);
        AssertThat(inventory.ItemTypeCount).IsEqual(1);
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(5);
        AssertThat(inventory.ContainsItem("test_item")).IsTrue();
    }

    [TestCase]
    public void TestTryAddItem_StacksExistingItem()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 5, out _);

        // Act
        bool added = inventory.TryAddItem(item, 3, out int addedQuantity);

        // Assert
        AssertThat(added).IsTrue();
        AssertThat(addedQuantity).IsEqual(3);
        AssertThat(inventory.ItemTypeCount).IsEqual(1); // Still only one item type
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(8);
    }

    [TestCase]
    public void TestTryAddItem_RespectsMaxStackSize()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 10);
        inventory.TryAddItem(item, 8, out _);

        // Act
        bool added = inventory.TryAddItem(item, 5, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse(); // Can't add all 5
        AssertThat(addedQuantity).IsEqual(2); // Only 2 added to reach max of 10
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(10);
    }

    [TestCase]
    public void TestTryAddItem_RejectsNullItem()
    {
        // Arrange
        var inventory = new Inventory();

        // Act
        bool added = inventory.TryAddItem(null, 5, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse();
        AssertThat(addedQuantity).IsEqual(0);
        AssertThat(inventory.ItemTypeCount).IsEqual(0);
    }

    [TestCase]
    public void TestTryAddItem_RejectsZeroQuantity()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);

        // Act
        bool added = inventory.TryAddItem(item, 0, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse();
        AssertThat(addedQuantity).IsEqual(0);
        AssertThat(inventory.ItemTypeCount).IsEqual(0);
    }

    [TestCase]
    public void TestTryAddItem_RejectsNegativeQuantity()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);

        // Act
        bool added = inventory.TryAddItem(item, -5, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse();
        AssertThat(addedQuantity).IsEqual(0);
    }

    [TestCase]
    public void TestTryAddItem_RespectsMaxItemTypes()
    {
        // Arrange
        var inventory = new Inventory { MaxItemTypes = 2 };
        var item1 = CreateTestItem("item1", "Item 1", 99);
        var item2 = CreateTestItem("item2", "Item 2", 99);
        var item3 = CreateTestItem("item3", "Item 3", 99);
        inventory.TryAddItem(item1, 1, out _);
        inventory.TryAddItem(item2, 1, out _);

        // Act
        bool added = inventory.TryAddItem(item3, 1, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse();
        AssertThat(addedQuantity).IsEqual(0);
        AssertThat(inventory.ItemTypeCount).IsEqual(2);
        AssertThat(inventory.ContainsItem("item3")).IsFalse();
    }

    [TestCase]
    public void TestTryRemoveItem_RemovesPartialStack()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 10, out _);

        // Act
        bool removed = inventory.TryRemoveItem("test_item", 3);

        // Assert
        AssertThat(removed).IsTrue();
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(7);
        AssertThat(inventory.ItemTypeCount).IsEqual(1); // Still present
    }

    [TestCase]
    public void TestTryRemoveItem_RemovesEntireStack()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 5, out _);

        // Act
        bool removed = inventory.TryRemoveItem("test_item", 5);

        // Assert
        AssertThat(removed).IsTrue();
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(0);
        AssertThat(inventory.ItemTypeCount).IsEqual(0); // Removed entirely
        AssertThat(inventory.ContainsItem("test_item")).IsFalse();
    }

    [TestCase]
    public void TestTryRemoveItem_FailsIfNotEnough()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 3, out _);

        // Act
        bool removed = inventory.TryRemoveItem("test_item", 5);

        // Assert
        AssertThat(removed).IsFalse();
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(3); // Unchanged
    }

    [TestCase]
    public void TestTryRemoveItem_FailsIfItemDoesNotExist()
    {
        // Arrange
        var inventory = new Inventory();

        // Act
        bool removed = inventory.TryRemoveItem("nonexistent_item", 1);

        // Assert
        AssertThat(removed).IsFalse();
    }

    [TestCase]
    public void TestTryRemoveItem_RejectsInvalidInputs()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 5, out _);

        // Act & Assert
        AssertThat(inventory.TryRemoveItem(null, 1)).IsFalse();
        AssertThat(inventory.TryRemoveItem("", 1)).IsFalse();
        AssertThat(inventory.TryRemoveItem("test_item", 0)).IsFalse();
        AssertThat(inventory.TryRemoveItem("test_item", -1)).IsFalse();
        AssertThat(inventory.GetQuantity("test_item")).IsEqual(5); // Unchanged
    }

    [TestCase]
    public void TestContainsItem_ReturnsTrueForExistingItem()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 1, out _);

        // Act & Assert
        AssertThat(inventory.ContainsItem("test_item")).IsTrue();
    }

    [TestCase]
    public void TestContainsItem_ReturnsFalseForNonexistentItem()
    {
        // Arrange
        var inventory = new Inventory();

        // Act & Assert
        AssertThat(inventory.ContainsItem("nonexistent_item")).IsFalse();
    }

    [TestCase]
    public void TestGetQuantity_ReturnsCorrectAmount()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 99);
        inventory.TryAddItem(item, 42, out _);

        // Act
        int quantity = inventory.GetQuantity("test_item");

        // Assert
        AssertThat(quantity).IsEqual(42);
    }

    [TestCase]
    public void TestGetQuantity_ReturnsZeroForNonexistentItem()
    {
        // Arrange
        var inventory = new Inventory();

        // Act
        int quantity = inventory.GetQuantity("nonexistent_item");

        // Assert
        AssertThat(quantity).IsEqual(0);
    }

    [TestCase]
    public void TestClear_RemovesAllItems()
    {
        // Arrange
        var inventory = new Inventory();
        var item1 = CreateTestItem("item1", "Item 1", 99);
        var item2 = CreateTestItem("item2", "Item 2", 99);
        inventory.TryAddItem(item1, 5, out _);
        inventory.TryAddItem(item2, 10, out _);

        // Act
        inventory.Clear();

        // Assert
        AssertThat(inventory.ItemTypeCount).IsEqual(0);
        AssertThat(inventory.ContainsItem("item1")).IsFalse();
        AssertThat(inventory.ContainsItem("item2")).IsFalse();
    }

    [TestCase]
    public void TestGetAllEntries_ReturnsAllItems()
    {
        // Arrange
        var inventory = new Inventory();
        var item1 = CreateTestItem("item1", "Item 1", 99);
        var item2 = CreateTestItem("item2", "Item 2", 99);
        inventory.TryAddItem(item1, 5, out _);
        inventory.TryAddItem(item2, 10, out _);

        // Act
        var entries = inventory.GetAllEntries();
        int count = 0;
        foreach (var entry in entries)
        {
            count++;
        }

        // Assert
        AssertThat(count).IsEqual(2);
    }

    [TestCase]
    public void TestNonStackableItem_CannotStack()
    {
        // Arrange
        var inventory = new Inventory();
        var equipment = new EquipmentItem
        {
            Id = "sword",
            DisplayName = "Iron Sword",
            SlotType = EquipmentSlotType.Weapon
        };
        inventory.TryAddItem(equipment, 1, out _);

        // Act
        bool added = inventory.TryAddItem(equipment, 1, out int addedQuantity);

        // Assert
        AssertThat(added).IsFalse();
        AssertThat(addedQuantity).IsEqual(0);
        AssertThat(inventory.GetQuantity("sword")).IsEqual(1);
    }

    [TestCase]
    public void TestInventoryEntry_IsFull()
    {
        // Arrange
        var inventory = new Inventory();
        var item = CreateTestItem("test_item", "Test Item", 5);
        inventory.TryAddItem(item, 5, out _);

        // Act
        var entry = inventory.Entries["test_item"];

        // Assert
        AssertThat(entry.IsFull).IsTrue();
    }

    // Helper method to create test items
    private GeneralItem CreateTestItem(string id, string displayName, int maxStack)
    {
        return new GeneralItem
        {
            Id = id,
            DisplayName = displayName,
            MaxStackOverride = maxStack
        };
    }
}
