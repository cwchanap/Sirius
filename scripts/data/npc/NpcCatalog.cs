using System.Collections.Generic;

/// <summary>
/// Static registry of all NPC definitions.
/// Add a private factory method and call Register() in the static constructor to add a new NPC.
/// </summary>
public static class NpcCatalog
{
    private static readonly Dictionary<string, NpcData> _registry = new();

    static NpcCatalog()
    {
        Register(CreateVillageShopkeeper());
        Register(CreateVillageHealer());
        Register(CreateVillager());
        Register(CreateBlacksmith());
    }

    /// <summary>Returns the NpcData for a given ID, or null if not found.</summary>
    public static NpcData? GetById(string? npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        return _registry.TryGetValue(npcId, out var data) ? data : null;
    }

    public static IReadOnlyCollection<NpcData> AllNpcs => _registry.Values;

    private static void Register(NpcData npc) => _registry.Add(npc.NpcId, npc);

    // ---- NPC definitions -----------------------------------------------

    private static NpcData CreateVillageShopkeeper() => new NpcData
    {
        NpcId = "village_shopkeeper",
        DisplayName = "Mira the Merchant",
        NpcType = NpcType.Shopkeeper,
        ShopId = "village_general_store",
        DialogueTreeId = "shopkeeper_greeting",
        SpriteType = "shopkeeper"
    };

    private static NpcData CreateVillageHealer() => new NpcData
    {
        NpcId = "village_healer",
        DisplayName = "Brother Aldric",
        NpcType = NpcType.Healer,
        HealCost = 50,
        DialogueTreeId = "healer_greeting",
        SpriteType = "healer"
    };

    private static NpcData CreateVillager() => new NpcData
    {
        NpcId = "old_farmer",
        DisplayName = "Old Farmer",
        NpcType = NpcType.Villager,
        DialogueTreeId = "villager_01",
        SpriteType = "villager"
    };

    private static NpcData CreateBlacksmith() => new NpcData
    {
        NpcId = "village_blacksmith",
        DisplayName = "Gareth the Smith",
        NpcType = NpcType.Blacksmith,
        ShopId = "blacksmith_shop",
        DialogueTreeId = "blacksmith_greeting",
        SpriteType = "blacksmith"
    };
}
