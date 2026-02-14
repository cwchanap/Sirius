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
                if (discarded > 0 && RecoveryChest.Instance != null && Godot.GodotObject.IsInstanceValid(RecoveryChest.Instance))
                {
                    RecoveryChest.Instance.AddOverflow(entry.ItemId, discarded);
                }
            }
            else if (!fullyAdded || lost > 0)
            {
                // Partial add for reasons other than stack overflow (e.g., inventory full)
                if (addedQuantity > 0)
                {
                    GD.PushWarning($"Save load: Could not fully restore {entry.ItemId} - {lost} items lost due to stack limits");
                }
                else
                {
                    GD.PushWarning($"Save load: Could not add {entry.ItemId} x{entry.Quantity} - inventory full or invalid");
                }
            }
        }

        return inventory;
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
