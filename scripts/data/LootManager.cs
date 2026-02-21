using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Static service for loot generation and award.
/// RollLoot is pure logic (fully testable).
/// AwardLootToCharacter has side effects: it writes to player.Inventory and may
/// route overflow to RecoveryChest.Instance. If RecoveryChest.Instance is null,
/// overflow items are permanently discarded and an error is logged.
/// </summary>
public static class LootManager
{
    public static LootResult RollLoot(LootTable? table, Random rng)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        if (table == null)
            return LootResult.Empty;

        if (rng.NextDouble() >= table.DropChance)
            return LootResult.Empty;

        var result = new LootResult();

        // Guaranteed drops are included when the DropChance roll succeeds.
        // They are not capped by MaxDrops — MaxDrops applies only to weighted draws.
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

        // Weighted random draws up to MaxDrops slots.
        // A null/unknown itemId skips that entry but does NOT consume a slot.
        // maxAttempts guards against an infinite loop if all weighted entries have unknown itemIds.
        int remainingSlots = Math.Max(0, table.MaxDrops - result.DroppedItems.Count);
        if (remainingSlots > 0)
        {
            var weighted = table.GetWeightedEntries();
            if (weighted.Count > 0)
            {
                int added = 0;
                int attempts = 0;
                int maxAttempts = remainingSlots * 2;
                while (added < remainingSlots && attempts++ < maxAttempts)
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
                    added++;
                }
            }
        }

        return result;
    }

    public static void AwardLootToCharacter(LootResult result, Character player)
    {
        if (result == null)
        {
            GD.PrintErr("[LootManager] AwardLootToCharacter called with null result.");
            return;
        }
        if (player == null)
        {
            GD.PrintErr("[LootManager] AwardLootToCharacter called with null player; loot will not be awarded.");
            return;
        }
        if (!result.HasDrops)
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
                    GD.PrintErr($"[LootManager] RecoveryChest.Instance is null; {overflow}x " +
                                $"'{entry.Item.DisplayName}' (id='{entry.Item.Id}') permanently lost " +
                                $"for player '{player.Name}'");
                }
            }
        }
    }

    private static int ResolveQuantity(in LootEntry entry, Random rng)
    {
        int rawMin = entry.MinQuantity;
        int rawMax = entry.MaxQuantity;

        // Normalize locally without mutating the entry
        int min = Math.Max(1, Math.Min(rawMin, rawMax));
        int max = Math.Max(min, Math.Max(rawMin, rawMax));

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
        // Fallback: floating-point/integer boundary guard; should not be reached with valid weights.
        return entries[entries.Count - 1];
    }
}
