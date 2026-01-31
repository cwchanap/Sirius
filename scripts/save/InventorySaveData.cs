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
        var inventory = new Inventory { MaxItemTypes = this.MaxItemTypes };

        foreach (var entry in Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item != null)
            {
                inventory.TryAddItem(item, entry.Quantity, out _);
            }
            else
            {
                GD.PushWarning($"Save load: Unknown item ID '{entry.ItemId}', skipping");
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
    public string ItemId { get; set; }
    public int Quantity { get; set; }
}
