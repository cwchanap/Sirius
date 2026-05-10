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
        // Wooden equipment
        ["wooden_sword"] = EquipmentCatalog.CreateWoodenSword,
        ["wooden_armor"] = EquipmentCatalog.CreateWoodenArmor,
        ["wooden_shield"] = EquipmentCatalog.CreateWoodenShield,
        ["wooden_helmet"] = EquipmentCatalog.CreateWoodenHelmet,
        ["wooden_shoes"] = EquipmentCatalog.CreateWoodenShoes,

        // Iron equipment
        ["iron_sword"] = EquipmentCatalog.CreateIronSword,
        ["iron_armor"] = EquipmentCatalog.CreateIronArmor,
        ["iron_shield"] = EquipmentCatalog.CreateIronShield,
        ["iron_helmet"] = EquipmentCatalog.CreateIronHelmet,
        ["iron_boots"] = EquipmentCatalog.CreateIronBoots,

        // Steel equipment
        ["steel_longsword"] = EquipmentCatalog.CreateSteelLongsword,
        ["chain_mail"] = EquipmentCatalog.CreateChainMail,
        ["steel_tower_shield"] = EquipmentCatalog.CreateSteelTowerShield,
        ["knight_helm"] = EquipmentCatalog.CreateKnightHelm,
        ["swift_boots"] = EquipmentCatalog.CreateSwiftBoots,

        // Obsidian equipment
        ["obsidian_blade"] = EquipmentCatalog.CreateObsidianBlade,
        ["obsidian_carapace"] = EquipmentCatalog.CreateObsidianCarapace,
        ["obsidian_guard"] = EquipmentCatalog.CreateObsidianGuard,
        ["obsidian_crown"] = EquipmentCatalog.CreateObsidianCrown,
        ["obsidian_treads"] = EquipmentCatalog.CreateObsidianTreads,

        // Consumables
        ["health_potion"]         = ConsumableCatalog.CreateHealthPotion,
        ["greater_health_potion"] = ConsumableCatalog.CreateGreaterHealthPotion,
        ["mana_potion"]           = ConsumableCatalog.CreateManaPotion,
        ["strength_tonic"]        = ConsumableCatalog.CreateStrengthTonic,
        ["iron_skin"]             = ConsumableCatalog.CreateIronSkin,
        ["swiftness_draught"]     = ConsumableCatalog.CreateSwiftnessDraught,
        ["antidote"]              = ConsumableCatalog.CreateAntidote,
        ["regen_potion"]          = ConsumableCatalog.CreateRegenPotion,
        ["poison_vial"]           = ConsumableCatalog.CreatePoisonVial,
        ["flash_powder"]          = ConsumableCatalog.CreateFlashPowder,
        ["major_health_potion"]   = ConsumableCatalog.CreateMajorHealthPotion,
        ["major_mana_potion"]     = ConsumableCatalog.CreateMajorManaPotion,
        ["warding_charm"]         = ConsumableCatalog.CreateWardingCharm,
        ["smoke_bomb"]            = ConsumableCatalog.CreateSmokeBomb,

        // Monster parts
        ["goblin_ear"] = MonsterPartsCatalog.CreateGoblinEar,
        ["orc_tusk"] = MonsterPartsCatalog.CreateOrcTusk,
        ["skeleton_bone"] = MonsterPartsCatalog.CreateSkeletonBone,
        ["spider_silk"] = MonsterPartsCatalog.CreateSpiderSilk,
        ["dragon_scale"] = MonsterPartsCatalog.CreateDragonScale,
        ["sentinel_core"] = MonsterPartsCatalog.CreateSentinelCore,
        ["hexed_cloth"] = MonsterPartsCatalog.CreateHexedCloth,
        ["splintered_bone"] = MonsterPartsCatalog.CreateSplinteredBone,
        ["revenant_plate"] = MonsterPartsCatalog.CreateRevenantPlate,
        ["gargoyle_shard"] = MonsterPartsCatalog.CreateGargoyleShard,
        ["abyssal_sigil"] = MonsterPartsCatalog.CreateAbyssalSigil,
    };

    /// <summary>
    /// Creates an item by its string ID.
    /// </summary>
    /// <param name="id">The item ID (e.g., "wooden_sword")</param>
    /// <returns>A new Item instance, or null if ID not found</returns>
    public static Item? CreateItemById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return _itemRegistry.TryGetValue(id, out var factory) ? factory() : null;
    }

    /// <summary>
    /// Checks if an item ID exists in the catalog.
    /// </summary>
    public static bool ItemExists(string? id)
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
