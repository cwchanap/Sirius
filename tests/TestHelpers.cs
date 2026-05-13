using GdUnit4;
using Godot;
using System.Collections.Generic;
using static GdUnit4.Assertions;

public static class TestHelpers
{
    public static Character CreateTestCharacter() => new Character
    {
        Name             = "TestHero",
        Level            = 1,
        MaxHealth        = 100,
        CurrentHealth    = 100,
        Attack           = 20,
        Defense          = 10,
        Speed            = 15,
        Experience       = 0,
        ExperienceToNext = 100,
        Gold             = 0,
    };

    public static Dictionary<string, int> TreasureBoxRewardItems(TreasureBoxSpawn box)
    {
        var result = new Dictionary<string, int>();
        if (box.RewardItemIds == null)
        {
            return result;
        }

        for (var i = 0; i < box.RewardItemIds.Count; i++)
        {
            var itemId = box.RewardItemIds[i];
            var quantity = box.RewardItemQuantities != null && i < box.RewardItemQuantities.Count
                ? box.RewardItemQuantities[i]
                : 1;
            result[itemId] = quantity;
        }

        return result;
    }
}
