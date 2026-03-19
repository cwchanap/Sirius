using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

/// <summary>
/// Tests the sell price formula used by ShopDialog:
///   sell price = max(1, floor(item.Value * 0.5))
///
/// Also tests the buy/sell gold flow via Character.TrySpendGold and GainGold.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ShopPricingTest : Node
{
    // Helper: mirrors ShopDialog's sell price formula exactly
    private static int SellPrice(int itemValue)
        => Mathf.Max(1, Mathf.FloorToInt(itemValue * 0.5f));

    // ---- Sell price formula -----------------------------------------------

    [TestCase]
    public void SellPrice_EvenValue_IsHalf()
    {
        AssertThat(SellPrice(100)).IsEqual(50);
        AssertThat(SellPrice(200)).IsEqual(100);
        AssertThat(SellPrice(10)).IsEqual(5);
    }

    [TestCase]
    public void SellPrice_OddValue_IsFlooredHalf()
    {
        AssertThat(SellPrice(101)).IsEqual(50);  // floor(50.5) = 50
        AssertThat(SellPrice(7)).IsEqual(3);     // floor(3.5) = 3
        AssertThat(SellPrice(1)).IsEqual(1);     // floor(0.5) = 0, clamped to 1
    }

    [TestCase]
    public void SellPrice_ValueOne_MinimumIsOne()
    {
        AssertThat(SellPrice(1)).IsEqual(1);
    }

    [TestCase]
    public void SellPrice_ValueZero_MinimumIsOne()
    {
        // An item with 0 value should still yield minimum 1 gold
        AssertThat(SellPrice(0)).IsEqual(1);
    }

    // ---- Buy flow: spend gold, receive item --------------------------------

    [TestCase]
    public void BuyFlow_SpendGold_ReducesGoldByBuyPrice()
    {
        var player = CreatePlayer(gold: 200);
        int buyPrice = 80;

        bool spent = player.TrySpendGold(buyPrice);

        AssertThat(spent).IsTrue();
        AssertThat(player.Gold).IsEqual(120);
    }

    [TestCase]
    public void BuyFlow_InsufficientGold_FailsWithoutSideEffect()
    {
        var player = CreatePlayer(gold: 50);

        bool spent = player.TrySpendGold(100);

        AssertThat(spent).IsFalse();
        AssertThat(player.Gold).IsEqual(50);
    }

    // ---- Sell flow: remove item, gain sell price gold ----------------------

    [TestCase]
    public void SellFlow_GainSellPrice_IncreasesGoldCorrectly()
    {
        var player = CreatePlayer(gold: 100);
        int itemValue = 60;
        int expectedSellPrice = SellPrice(itemValue); // 30

        player.GainGold(expectedSellPrice);

        AssertThat(player.Gold).IsEqual(130);
    }

    // ---- Buy-then-sell round trip ------------------------------------------

    [TestCase]
    public void BuyThenSell_PlayerLosesHalfValue()
    {
        // Buying an item at full price then selling at 50% results in net loss of 50%
        var player = CreatePlayer(gold: 200);
        int itemValue = 100;
        int sellPrice = SellPrice(itemValue);

        player.TrySpendGold(itemValue);   // buy: pay 100
        player.GainGold(sellPrice);       // sell: receive 50

        AssertThat(player.Gold).IsEqual(150); // 200 - 100 + 50 = 150
    }

    // ---- ItemCatalog values produce valid sell prices ----------------------

    [TestCase]
    public void ShopItems_AllHavePositiveBuyPrice()
    {
        var shop = ShopCatalog.GetById("village_general_store");
        foreach (var entry in shop.Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            AssertThat(item.Value).IsGreater(0);
        }
    }

    [TestCase]
    public void ShopItems_SellPriceAlwaysAtLeastOne()
    {
        var generalStore = ShopCatalog.GetById("village_general_store");
        var blacksmith = ShopCatalog.GetById("blacksmith_shop");

        foreach (var shop in new[] { generalStore, blacksmith })
        {
            foreach (var entry in shop.Entries)
            {
                var item = ItemCatalog.CreateItemById(entry.ItemId);
                AssertThat(SellPrice(item.Value)).IsGreaterEqual(1);
            }
        }
    }

    [TestCase]
    public void ShopItems_SellPriceNeverExceedsBuyPrice()
    {
        var generalStore = ShopCatalog.GetById("village_general_store");
        var blacksmith = ShopCatalog.GetById("blacksmith_shop");

        foreach (var shop in new[] { generalStore, blacksmith })
        {
            foreach (var entry in shop.Entries)
            {
                var item = ItemCatalog.CreateItemById(entry.ItemId);
                AssertThat(SellPrice(item.Value)).IsLessEqual(item.Value);
            }
        }
    }

    private static Character CreatePlayer(int gold) => new Character
    {
        Name = "TestPlayer",
        Level = 1,
        MaxHealth = 100,
        CurrentHealth = 100,
        Attack = 10,
        Defense = 5,
        Speed = 10,
        Gold = gold
    };
}
