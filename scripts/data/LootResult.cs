using System;
using System.Collections.Generic;

/// <summary>
/// The resolved output of a LootManager.RollLoot call.
/// </summary>
[System.Serializable]
public class LootResult
{
    public static readonly LootResult Empty = new LootResult();

    public List<LootResultEntry> DroppedItems { get; } = new();

    public bool HasDrops => DroppedItems.Count > 0;

    public void Add(Item item, int quantity)
    {
        if (ReferenceEquals(this, Empty))
            throw new InvalidOperationException("LootResult.Empty is immutable and cannot be modified via Add().");

        if (item == null || quantity <= 0) return;
        DroppedItems.Add(new LootResultEntry(item, quantity));
    }
}

/// <summary>
/// One resolved entry in a LootResult.
/// </summary>
[System.Serializable]
public class LootResultEntry
{
    public Item Item { get; }
    public int Quantity { get; }
    public ItemRarity Rarity => Item.Rarity;

    public LootResultEntry(Item item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }
}
