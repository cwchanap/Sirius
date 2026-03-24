using System.Collections.Generic;

/// <summary>
/// A single item entry in a shop's stock.
/// </summary>
public class ShopEntry
{
    public string ItemId { get; init; }
    /// <summary>-1 means unlimited stock.</summary>
    public int Stock { get; init; } = -1;
}

/// <summary>
/// Defines the items available in a named shop, resolved from ShopCatalog.
/// </summary>
public class ShopInventory
{
    public string ShopId { get; init; }
    public string DisplayName { get; init; }
    public IReadOnlyList<ShopEntry> Entries { get; init; } = new List<ShopEntry>();
}
