using Godot;
using System;
using System.Collections.Generic;

[System.Serializable]
public partial class Inventory : Resource
{
    [Export]
    public int MaxItemTypes { get; set; } = 100;

    private readonly Dictionary<string, InventoryEntry> _entries = new();

    public int ItemTypeCount => _entries.Count;

    public IReadOnlyDictionary<string, InventoryEntry> Entries => _entries;

    public bool TryAddItem(Item item, int quantity, out int addedQuantity)
    {
        addedQuantity = 0;

        if (item == null)
        {
            GD.PushWarning("Attempted to add a null item to inventory.");
            return false;
        }

        if (quantity <= 0)
        {
            return false;
        }

        if (_entries.TryGetValue(item.Id, out var entry))
        {
            if (!item.CanStack)
            {
                GD.PushWarning($"Item '{item.DisplayName}' is not stackable and already exists in inventory.");
                return false;
            }

            int availableSpace = Math.Max(0, item.MaxStackSize - entry.Quantity);
            addedQuantity = Math.Min(quantity, availableSpace);

            if (addedQuantity <= 0)
            {
                return false;
            }

            entry.Add(addedQuantity);
            return addedQuantity == quantity;
        }
        else
        {
            if (ItemTypeCount >= MaxItemTypes)
            {
                GD.PushWarning("Inventory item type limit reached.");
                return false;
            }

            int clampedQuantity = item.CanStack
                ? Math.Min(quantity, item.MaxStackSize)
                : Math.Min(quantity, 1);

            addedQuantity = clampedQuantity;

            if (addedQuantity <= 0)
            {
                return false;
            }

            _entries[item.Id] = new InventoryEntry(item, addedQuantity);
            return addedQuantity == quantity;
        }
    }

    public bool TryRemoveItem(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        if (!_entries.TryGetValue(itemId, out var entry))
        {
            return false;
        }

        if (entry.Quantity < quantity)
        {
            return false;
        }

        entry.Remove(quantity);

        if (entry.Quantity <= 0)
        {
            _entries.Remove(itemId);
        }

        return true;
    }

    public bool ContainsItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        return _entries.ContainsKey(itemId);
    }

    public int GetQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return _entries.TryGetValue(itemId, out var entry) ? entry.Quantity : 0;
    }

    public void Clear()
    {
        _entries.Clear();
    }

    public IEnumerable<InventoryEntry> GetAllEntries()
    {
        return _entries.Values;
    }
}

public class InventoryEntry
{
    public Item Item { get; }
    public int Quantity { get; private set; }
    public ItemCategory Category => Item.Category;

    public bool IsFull => Item.CanStack && Quantity >= Item.MaxStackSize;

    internal InventoryEntry(Item item, int quantity)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Quantity = Math.Max(0, quantity);
    }

    internal void Add(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (Item.CanStack)
        {
            Quantity = Math.Min(Item.MaxStackSize, Quantity + amount);
        }
        else if (Quantity == 0)
        {
            Quantity = 1;
        }
    }

    internal void Remove(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Quantity = Math.Max(0, Quantity - amount);
    }
}
