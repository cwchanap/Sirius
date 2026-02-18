using Godot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// The resolved output of a LootManager.RollLoot call.
/// </summary>
[System.Serializable]
public class LootResult
{
    public static readonly LootResult Empty = new LootResult();

    private readonly List<LootResultEntry> _droppedItems = new();
    private readonly ReadOnlyCollection<LootResultEntry> _droppedItemsView;

    public LootResult()
    {
        _droppedItemsView = _droppedItems.AsReadOnly();
    }

    public IReadOnlyList<LootResultEntry> DroppedItems => _droppedItemsView;

    public bool HasDrops => _droppedItems.Count > 0;

    public void Add(Item item, int quantity)
    {
        if (ReferenceEquals(this, Empty))
            throw new InvalidOperationException("LootResult.Empty is immutable and cannot be modified via Add().");

        if (item == null)
        {
            GD.PushWarning("[LootResult] Add called with null item; skipping.");
            return;
        }
        if (quantity <= 0)
        {
            GD.PushWarning($"[LootResult] Add called with non-positive quantity ({quantity}) for item '{item.Id}'; skipping.");
            return;
        }
        _droppedItems.Add(new LootResultEntry(item, quantity));
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
        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        Quantity = quantity;
    }
}
