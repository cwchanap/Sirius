using System.Collections.Generic;

/// <summary>
/// Static registry of all shop inventories, keyed by shop ID.
/// Add a private factory method and call Register() in the static constructor to add a new shop.
/// </summary>
public static class ShopCatalog
{
    private static readonly Dictionary<string, ShopInventory> _registry = new();

    static ShopCatalog()
    {
        Register(CreateVillageGeneralStore());
        Register(CreateBlacksmithShop());
    }

    /// <summary>Returns the ShopInventory for a given shop ID, or null if not found.</summary>
    public static ShopInventory? GetById(string? shopId)
    {
        if (string.IsNullOrEmpty(shopId)) return null;
        return _registry.TryGetValue(shopId, out var inv) ? inv : null;
    }

    private static void Register(ShopInventory shop) => _registry.Add(shop.ShopId, shop);

    // ---- Shop definitions -----------------------------------------------

    private static ShopInventory CreateVillageGeneralStore() => new ShopInventory
    {
        ShopId = "village_general_store",
        DisplayName = "Mira's General Store",
        Entries = new List<ShopEntry>
        {
            new() { ItemId = "health_potion" },
            new() { ItemId = "greater_health_potion" },
            new() { ItemId = "mana_potion" },
            new() { ItemId = "antidote" },
            new() { ItemId = "regen_potion" },
            new() { ItemId = "strength_tonic" },
            new() { ItemId = "iron_skin" },
            new() { ItemId = "swiftness_draught" },
        }
    };

    private static ShopInventory CreateBlacksmithShop() => new ShopInventory
    {
        ShopId = "blacksmith_shop",
        DisplayName = "Gareth's Smithy",
        Entries = new List<ShopEntry>
        {
            new() { ItemId = "iron_sword" },
            new() { ItemId = "iron_armor" },
            new() { ItemId = "iron_shield" },
            new() { ItemId = "iron_helmet" },
            new() { ItemId = "iron_boots" },
        }
    };
}
