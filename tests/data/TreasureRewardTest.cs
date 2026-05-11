using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TreasureRewardTest : Godot.Node
{
    [TestCase]
    public void GrantTo_AddsGoldAndKnownItems()
    {
        var player = new Character { Name = "Tester", Gold = 10 };
        var reward = new TreasureReward
        {
            Gold = 40,
            Items =
            [
                new TreasureRewardItem("health_potion", 2),
                new TreasureRewardItem("mana_potion", 1)
            ]
        };

        var result = reward.GrantTo(player);

        AssertThat(result.GoldGranted).IsEqual(40);
        AssertThat(player.Gold).IsEqual(50);
        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(2);
        AssertThat(player.GetItemQuantity("mana_potion")).IsEqual(1);
        AssertThat(result.ItemQuantitiesGranted["health_potion"]).IsEqual(2);
        AssertThat(result.ItemQuantitiesGranted["mana_potion"]).IsEqual(1);
    }

    [TestCase]
    public void GrantTo_SkipsUnknownItems()
    {
        var player = new Character { Name = "Tester" };
        var reward = new TreasureReward
        {
            Items = [new TreasureRewardItem("missing_item", 1)]
        };

        var result = reward.GrantTo(player);

        AssertThat(result.ItemQuantitiesGranted.Count).IsEqual(0);
        AssertThat(result.SkippedItemIds.Count).IsEqual(1);
        AssertThat(result.SkippedItemIds[0]).IsEqual("missing_item");
    }

    [TestCase]
    public void GrantTo_SkipsInvalidQuantities()
    {
        var player = new Character { Name = "Tester" };
        var reward = new TreasureReward
        {
            Items = [new TreasureRewardItem("health_potion", 0)]
        };

        var result = reward.GrantTo(player);

        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(0);
        AssertThat(result.SkippedItemIds.Count).IsEqual(1);
        AssertThat(result.SkippedItemIds[0]).IsEqual("health_potion");
    }

    [TestCase]
    public void Validate_ReturnsAuthoredErrors()
    {
        var reward = new TreasureReward
        {
            Gold = -1,
            Items =
            [
                new TreasureRewardItem("", 1),
                new TreasureRewardItem("missing_item", 1),
                new TreasureRewardItem("health_potion", -2)
            ]
        };

        var errors = reward.ValidateAuthoredContent();

        AssertThat(errors.Count).IsEqual(4);
        AssertThat(errors[0]).Contains("Gold");
        AssertThat(errors[1]).Contains("empty");
        AssertThat(errors[2]).Contains("missing_item");
        AssertThat(errors[3]).Contains("health_potion");
    }

    [TestCase]
    public void NullItems_TreatedAsEmptyRewardItems()
    {
        var player = new Character { Name = "Tester", Gold = 5 };
        var reward = new TreasureReward { Gold = 10, Items = null! };

        AssertThat(reward.HasAnyReward).IsTrue();

        var errors = reward.ValidateAuthoredContent();
        var result = reward.GrantTo(player);

        AssertThat(errors.Count).IsEqual(0);
        AssertThat(result.GoldGranted).IsEqual(10);
        AssertThat(player.Gold).IsEqual(15);
        AssertThat(result.ItemQuantitiesGranted.Count).IsEqual(0);
        AssertThat(result.SkippedItemIds.Count).IsEqual(0);
    }

}
