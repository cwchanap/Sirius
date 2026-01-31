using System;
using System.Collections.Generic;

/// <summary>
/// Unified item registry for creating any item by string ID.
/// Used by save/load system to recreate items from saved IDs.
/// </summary>
public static class ItemCatalog
{
    private static readonly Dictionary<string, Func<Item>> _itemRegistry = new()
    {
        // Equipment - delegates to EquipmentCatalog
        ["wooden_sword"] = EquipmentCatalog.CreateWoodenSword,
        ["wooden_armor"] = EquipmentCatalog.CreateWoodenArmor,
        ["wooden_shield"] = EquipmentCatalog.CreateWoodenShield,
        ["wooden_helmet"] = EquipmentCatalog.CreateWoodenHelmet,
        ["wooden_shoes"] = EquipmentCatalog.CreateWoodenShoes,
    };

    /// <summary>
    /// Creates an item by its string ID.
    /// </summary>
    /// <param name="id">The item ID (e.g., "wooden_sword")</param>
    /// <returns>A new Item instance, or null if ID not found</returns>
    public static Item CreateItemById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _itemRegistry.TryGetValue(id, out var factory) ? factory() : null;
    }

    /// <summary>
    /// Checks if an item ID exists in the catalog.
    /// </summary>
    public static bool ItemExists(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && _itemRegistry.ContainsKey(id);
    }

    /// <summary>
    /// Gets all registered item IDs.
    /// </summary>
    public static IEnumerable<string> GetAllItemIds()
    {
        return _itemRegistry.Keys;
    }
}
