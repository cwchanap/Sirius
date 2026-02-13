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

    public static InventorySaveData FromInventory(Inventory inv)
    {
        if (inv == null) return new InventorySaveData();

        return new InventorySaveData
        {
            MaxItemTypes = inv.MaxItemTypes,
            Entries = inv.GetAllEntries()
                .Where(e => e.Item != null)  // Filter out entries with null items
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
            if (!fullyAdded || lost > 0)
            {
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
