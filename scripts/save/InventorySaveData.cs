using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// DTO for Inventory entries.
/// </summary>
public class InventorySaveData
{
    public List<InventoryEntryDto> Entries { get; set; } = new();
    public int MaxItemTypes { get; set; } = 100;
    
    /// <summary>
    /// Items that could not be recovered because RecoveryChest was unavailable during load.
    /// These will be flushed to RecoveryChest when it becomes available.
    /// </summary>
    public Dictionary<string, int> PendingRecovery { get; set; } = new();

    public static InventorySaveData FromInventory(Inventory? inv)
    {
        if (inv == null) return new InventorySaveData();

        return new InventorySaveData
        {
            MaxItemTypes = inv.MaxItemTypes,
            Entries = inv.GetAllEntries()
            .Where(e => e.Item != null && !string.IsNullOrWhiteSpace(e.Item.Id)) // Filter out entries with null items or invalid IDs
            .Select(e => new InventoryEntryDto
            {
                ItemId = e.Item.Id,
                Quantity = e.Quantity
            })
            .ToList()
        };
    }

    public Inventory ToInventory()
    {
        int maxItemTypes = this.MaxItemTypes;
        if (maxItemTypes <= 0)
        {
            GD.PushWarning($"Save data: Invalid MaxItemTypes ({this.MaxItemTypes}), using default 100");
            maxItemTypes = 100;
        }
        var inventory = new Inventory { MaxItemTypes = maxItemTypes };

        if (Entries == null)
        {
            return inventory;
        }

        foreach (var entry in Entries)
        {
            if (entry == null || entry.Quantity <= 0 || string.IsNullOrEmpty(entry.ItemId))
            {
                continue;
            }
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"Save load: Unknown item ID '{entry.ItemId}', skipping");
                continue;
            }

            // Inventory stores one entry per item ID, so overflow beyond that entry's stack limit
            // cannot be represented as additional stacks. Any remainder is dropped with a warning.
            bool fullyAdded = inventory.TryAddItem(item, entry.Quantity, out int addedQuantity);
            int lost = entry.Quantity - addedQuantity;

            // Check for overflow - quantity exceeded max stack size
            if (entry.Quantity > item.MaxStackSize)
            {
                int overflowAmount = entry.Quantity - item.MaxStackSize;
                int actuallyAdded = System.Math.Min(item.MaxStackSize, addedQuantity);
                int discarded = entry.Quantity - actuallyAdded;

                // Emit prominent, detailed warning about the overflow
                GD.PushWarning($"INVENTORY OVERFLOW: Item '{entry.ItemId}' - " +
                    $"Requested: {entry.Quantity}, MaxStack: {item.MaxStackSize}, " +
                    $"Added: {actuallyAdded}, Discarded: {discarded} (overflow: {overflowAmount})");

                // Queue discarded amount into RecoveryChest for later recovery
                if (discarded > 0)
                {
                    if (RecoveryChest.Instance != null && Godot.GodotObject.IsInstanceValid(RecoveryChest.Instance))
                    {
                        RecoveryChest.Instance.AddOverflow(entry.ItemId, discarded);
                    }
                    else
                    {
                        // RecoveryChest not available - store in pending recovery for later
                        if (PendingRecovery.ContainsKey(entry.ItemId))
                        {
                            PendingRecovery[entry.ItemId] += discarded;
                        }
                        else
                        {
                            PendingRecovery[entry.ItemId] = discarded;
                        }
                        GD.PushWarning($"Save load: RecoveryChest unavailable, stored {discarded}x '{entry.ItemId}' in pending recovery");
                    }
                }
            }
            else if (!fullyAdded || lost > 0)
            {
                // Partial add for reasons other than stack overflow (e.g., inventory full)
                if (addedQuantity > 0)
                {
                    GD.PushWarning($"Save load: Could not fully restore {entry.ItemId} - {lost} items lost due to inventory capacity/full slots");
                }
                else
                {
                    GD.PushWarning($"Save load: Could not add {entry.ItemId} x{entry.Quantity} - inventory full or invalid");
                }
            }
        }

        return inventory;
    }
    
    /// <summary>
    /// Attempts to flush all pending recovery items to the RecoveryChest.
    /// Call this when RecoveryChest becomes available (e.g., after scene load).
    /// </summary>
    /// <returns>A FlushResult indicating how many items were successfully flushed</returns>
    public FlushResult FlushPendingRecovery()
    {
        var result = new FlushResult();
        
        if (PendingRecovery == null || PendingRecovery.Count == 0)
        {
            return result;
        }
        
        if (RecoveryChest.Instance == null || !Godot.GodotObject.IsInstanceValid(RecoveryChest.Instance))
        {
            GD.PushWarning("FlushPendingRecovery: RecoveryChest still unavailable, items remain in pending");
            result.ItemsRemaining = PendingRecovery.Count;
            return result;
        }
        
        var toRemove = new List<string>();
        foreach (var kvp in PendingRecovery)
        {
            if (kvp.Value > 0)
            {
                RecoveryChest.Instance.AddOverflow(kvp.Key, kvp.Value);
                result.ItemsFlushed++;
                result.TotalQuantityFlushed += kvp.Value;
                toRemove.Add(kvp.Key);
                GD.Print($"FlushPendingRecovery: Flushed {kvp.Value}x '{kvp.Key}' to RecoveryChest");
            }
        }
        
        foreach (var key in toRemove)
        {
            PendingRecovery.Remove(key);
        }
        
        result.ItemsRemaining = PendingRecovery.Count;
        return result;
    }
    
    /// <summary>
    /// Result of flushing pending recovery items to RecoveryChest.
    /// </summary>
    public class FlushResult
    {
        public int ItemsFlushed { get; set; }
        public int TotalQuantityFlushed { get; set; }
        public int ItemsRemaining { get; set; }
        public bool Success => ItemsRemaining == 0;
    }
}

/// <summary>
/// DTO for a single inventory entry.
/// </summary>
public class InventoryEntryDto
{
    public string? ItemId { get; set; }
    public int Quantity { get; set; }
}
