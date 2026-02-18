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
            return LootResult.Empty;

        if (rng.NextDouble() >= table.DropChance)
            return LootResult.Empty;

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
        int rawMin = entry.MinQuantity;
        int rawMax = entry.MaxQuantity;

        // Normalize locally without mutating the entry
        int min = Math.Max(1, Math.Min(rawMin, rawMax));
        int max = Math.Max(min, rawMax);

        if (rawMin > rawMax)
        {
            GD.PushWarning($"[LootManager] LootEntry quantity range invalid for itemId '{entry.ItemId}' " +
                           $"(MinQuantity={rawMin}, MaxQuantity={rawMax}); normalizing locally.");
        }

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
