using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Static service for loot generation and award.
/// RollLoot is pure logic (fully testable). AwardLootToCharacter touches RecoveryChest.
/// </summary>
public static class LootManager
{
    public static LootResult RollLoot(LootTable? table, Random rng)
    {
        if (table == null)
            return new LootResult();

        if (rng.NextDouble() >= table.DropChance)
            return new LootResult();

        var result = new LootResult();

        // Guaranteed drops always included
        foreach (var entry in table.GetGuaranteedEntries())
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"[LootManager] Unknown itemId '{entry.ItemId}' in LootTable - skipping");
                continue;
            }
            int qty = ResolveQuantity(entry, rng);
            result.Add(item, qty);
        }

        // Weighted random draws up to MaxDrops
        int remainingSlots = Math.Max(0, table.MaxDrops - result.DroppedItems.Count);
        if (remainingSlots > 0)
        {
            var weighted = table.GetWeightedEntries();
            if (weighted.Count > 0)
            {
                for (int i = 0; i < remainingSlots; i++)
                {
                    var entry = PickWeightedEntry(weighted, rng);
                    if (entry == null) break;
                    var item = ItemCatalog.CreateItemById(entry.ItemId);
                    if (item == null)
                    {
                        GD.PushWarning($"[LootManager] Unknown itemId '{entry.ItemId}' in LootTable - skipping");
                        continue;
                    }
                    int qty = ResolveQuantity(entry, rng);
                    result.Add(item, qty);
                }
            }
        }

        return result;
    }

    public static void AwardLootToCharacter(LootResult result, Character player)
    {
        if (result == null || !result.HasDrops || player == null)
            return;

        foreach (var entry in result.DroppedItems)
        {
            player.TryAddItem(entry.Item, entry.Quantity, out int added);

            if (added > 0)
            {
                GD.Print($"[LootManager] Awarded {added}x '{entry.Item.DisplayName}' to {player.Name}");
            }

            int overflow = entry.Quantity - added;
            if (overflow > 0)
            {
                if (RecoveryChest.Instance != null)
                {
                    RecoveryChest.Instance.AddOverflow(entry.Item.Id, overflow);
                    GD.Print($"[LootManager] {overflow}x '{entry.Item.DisplayName}' sent to RecoveryChest");
                }
                else
                {
                    GD.PushWarning($"[LootManager] RecoveryChest.Instance is null; AddOverflow skipped for item '{entry.Item.Id}'/'{entry.Item.DisplayName}', overflow={overflow}");
                }
            }
        }
    }

    private static int ResolveQuantity(LootEntry entry, Random rng)
    {
        if (entry.MinQuantity > entry.MaxQuantity)
        {
            GD.PushWarning($"[LootManager] LootEntry quantity range invalid for itemId '{entry.ItemId}' (MinQuantity={entry.MinQuantity}, MaxQuantity={entry.MaxQuantity}); normalizing.");
        }
        entry.ValidateAndNormalizeQuantityRange();

        int min = Math.Max(1, entry.MinQuantity);
        int max = Math.Max(min, entry.MaxQuantity);
        return rng.Next(min, max + 1);
    }

    private static LootEntry? PickWeightedEntry(List<LootEntry> entries, Random rng)
    {
        if (entries.Count == 0) return null;

        int totalWeight = 0;
        foreach (var e in entries) totalWeight += e.Weight;
        if (totalWeight <= 0) return null;

        int roll = rng.Next(0, totalWeight);
        int cumulative = 0;
        foreach (var e in entries)
        {
            cumulative += e.Weight;
            if (roll < cumulative) return e;
        }
        return entries[entries.Count - 1];
    }
}
