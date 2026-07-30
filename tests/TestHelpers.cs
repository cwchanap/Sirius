using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using static GdUnit4.Assertions;

public static class TestHelpers
{
    /// <summary>
    /// Reset the <see cref="GameManager"/> singleton to null via reflection.
    /// Tries the public Instance setter first, then falls back to the
    /// compiler-generated backing field, and throws if neither is available.
    /// </summary>
    public static void ResetGameManagerSingleton()
    {
        var property = typeof(GameManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object[] { null! });
            return;
        }

        var field = typeof(GameManager).GetField(
            "<Instance>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
            return;
        }

        throw new InvalidOperationException(
            "Failed to reset GameManager singleton: no Instance setter or backing field found.");
    }

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
