/// <summary>
/// Factory methods for all consumable items.
/// To add a new consumable: add a method here and register it in ItemCatalog.
/// </summary>
public static class ConsumableCatalog
{
    public static ConsumableItem CreateHealthPotion() => new ConsumableItem
    {
        Id          = "health_potion",
        DisplayName = "Health Potion",
        Description = "Restores 50 HP instantly.",
        Value       = 30,
        Effect      = new HealEffect(50),
    };

    public static ConsumableItem CreateGreaterHealthPotion() => new ConsumableItem
    {
        Id          = "greater_health_potion",
        DisplayName = "Greater Health Potion",
        Description = "Restores 150 HP instantly.",
        Value       = 80,
        Rarity      = ItemRarity.Uncommon,
        Effect      = new HealEffect(150),
    };

    public static ConsumableItem CreateStrengthTonic() => new ConsumableItem
    {
        Id               = "strength_tonic",
        DisplayName      = "Strength Tonic",
        Description      = "Raises Attack by 15 for 3 turns.",
        Value            = 50,
        MaxStackOverride = 20,
        Effect           = new StatusEffectEffect(StatusEffectType.Strength, "ATK", 15, 3),
    };

    public static ConsumableItem CreateIronSkin() => new ConsumableItem
    {
        Id               = "iron_skin",
        DisplayName      = "Iron Skin",
        Description      = "Raises Defense by 10 for 4 turns.",
        Value            = 50,
        MaxStackOverride = 20,
        Effect           = new StatusEffectEffect(StatusEffectType.Fortify, "DEF", 10, 4),
    };

    public static ConsumableItem CreateSwiftnessDraught() => new ConsumableItem
    {
        Id               = "swiftness_draught",
        DisplayName      = "Swiftness Draught",
        Description      = "Raises Speed by 8 for 3 turns.",
        Value            = 40,
        MaxStackOverride = 20,
        Effect           = new StatusEffectEffect(StatusEffectType.Haste, "SPD", 8, 3),
    };

    public static ConsumableItem CreateAntidote() => new ConsumableItem
    {
        Id          = "antidote",
        DisplayName = "Antidote",
        Description = "Cures Poison and Burn.",
        Value       = 35,
        Effect      = new CureStatusEffect("Poison & Burn", StatusEffectType.Poison, StatusEffectType.Burn),
    };

    public static ConsumableItem CreateRegenPotion() => new ConsumableItem
    {
        Id               = "regen_potion",
        DisplayName      = "Regen Potion",
        Description      = "Restores 15 HP per turn for 3 turns.",
        Value            = 65,
        MaxStackOverride = 20,
        Effect           = new StatusEffectEffect(StatusEffectType.Regen, "HP/turn", 15, 3),
    };

    public static ConsumableItem CreatePoisonVial() => new ConsumableItem
    {
        Id               = "poison_vial",
        DisplayName      = "Poison Vial",
        Description      = "Inflicts Poison on the enemy for 4 turns (8 dmg/turn).",
        Value            = 60,
        Rarity           = ItemRarity.Uncommon,
        MaxStackOverride = 10,
        Effect           = new EnemyDebuffEffect(StatusEffectType.Poison, 8, 4),
    };

    public static ConsumableItem CreateFlashPowder() => new ConsumableItem
    {
        Id               = "flash_powder",
        DisplayName      = "Flash Powder",
        Description      = "Blinds the enemy for 2 turns (reduces accuracy to 55%).",
        Value            = 55,
        Rarity           = ItemRarity.Uncommon,
        MaxStackOverride = 10,
        Effect           = new EnemyDebuffEffect(StatusEffectType.Blind, 0, 2),
    };
}
