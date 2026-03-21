/// <summary>
/// NPC types that determine available interaction options.
/// </summary>
public enum NpcType
{
    Villager,
    Shopkeeper,
    QuestGiver,
    Blacksmith,
    Healer
}

/// <summary>
/// Plain C# data class representing an NPC's definition.
/// Resolved from NpcCatalog by string ID — not a Godot Resource.
/// </summary>
[System.Serializable]
public class NpcData
{
    public string NpcId { get; init; }
    public string DisplayName { get; init; }
    public NpcType NpcType { get; init; }

    /// <summary>Shop ID resolved via ShopCatalog. Non-null for Shopkeeper and Blacksmith.</summary>
    public string ShopId { get; init; }

    /// <summary>Gold cost to restore full HP. Used when NpcType is Healer.</summary>
    public int HealCost { get; init; }

    /// <summary>Dialogue tree ID resolved via DialogueCatalog.</summary>
    public string DialogueTreeId { get; init; }

    /// <summary>Sprite folder name used for texture loading (e.g. "shopkeeper", "healer").</summary>
    public string SpriteType { get; init; }
}
