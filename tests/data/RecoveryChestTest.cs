using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class RecoveryChestTest : Node
{
    [TestCase]
    public void TestRecoverAll_PartialRecoveryUpdatesRemainingAmount()
    {
        // Arrange
        var chest = new RecoveryChest();
        var inventory = new Inventory();

        chest.AddOverflow("wooden_sword", 5);
        AssertThat(chest.GetOverflowAmount("wooden_sword")).IsEqual(5);

        // Act - Non-stackable item can only recover one at a time
        int firstRecovered = chest.RecoverAll(inventory);

        // Assert
        AssertThat(firstRecovered).IsEqual(1);
        AssertThat(inventory.GetQuantity("wooden_sword")).IsEqual(1);
        AssertThat(chest.GetOverflowAmount("wooden_sword")).IsEqual(4);

        // Remove recovered item, then recover again to verify remaining amount persists
        bool removed = inventory.TryRemoveItem("wooden_sword", 1);
        AssertThat(removed).IsTrue();

        int secondRecovered = chest.RecoverAll(inventory);
        AssertThat(secondRecovered).IsEqual(1);
        AssertThat(chest.GetOverflowAmount("wooden_sword")).IsEqual(3);
        chest.Free();
    }

    [TestCase]
    public void TestRecoverAll_UnknownItemRemainsInChest()
    {
        // Arrange
        var chest = new RecoveryChest();
        var inventory = new Inventory();
        chest.AddOverflow("unknown_item", 2);

        // Act
        int recovered = chest.RecoverAll(inventory);

        // Assert
        AssertThat(recovered).IsEqual(0);
        AssertThat(chest.OverflowCount).IsEqual(1);
        AssertThat(chest.GetOverflowAmount("unknown_item")).IsEqual(2);
        chest.Free();
    }
}
