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
        AssetPath   = "res://assets/sprites/items/consumables/health_potion.png",
        Effect      = new HealEffect(50),
    };

    public static ConsumableItem CreateGreaterHealthPotion() => new ConsumableItem
    {
        Id          = "greater_health_potion",
        DisplayName = "Greater Health Potion",
        Description = "Restores 150 HP instantly.",
        Value       = 80,
        Rarity      = ItemRarity.Uncommon,
        AssetPath   = "res://assets/sprites/items/consumables/greater_health_potion.png",
        Effect      = new HealEffect(150),
    };

    public static ConsumableItem CreateManaPotion() => new ConsumableItem
    {
        Id          = "mana_potion",
        DisplayName = "Mana Potion",
        Description = "Restores 25 MP instantly.",
        Value       = 35,
        AssetPath   = "res://assets/sprites/items/consumables/mana_potion.png",
        Effect      = new RestoreManaEffect(25),
    };

    public static ConsumableItem CreateStrengthTonic() => new ConsumableItem
    {
        Id               = "strength_tonic",
        DisplayName      = "Strength Tonic",
        Description      = "Raises Attack by 15 for 3 turns.",
        Value            = 50,
        MaxStackOverride = 20,
        AssetPath        = "res://assets/sprites/items/consumables/strength_tonic.png",
        Effect           = new StatusEffectEffect(StatusEffectType.Strength, "ATK", 15, 3),
    };

    public static ConsumableItem CreateIronSkin() => new ConsumableItem
    {
        Id               = "iron_skin",
        DisplayName      = "Iron Skin",
        Description      = "Raises Defense by 10 for 4 turns.",
        Value            = 50,
        MaxStackOverride = 20,
        AssetPath        = "res://assets/sprites/items/consumables/iron_skin.png",
        Effect           = new StatusEffectEffect(StatusEffectType.Fortify, "DEF", 10, 4),
    };

    public static ConsumableItem CreateSwiftnessDraught() => new ConsumableItem
    {
        Id               = "swiftness_draught",
        DisplayName      = "Swiftness Draught",
        Description      = "Raises Speed by 8 for 3 turns.",
        Value            = 40,
        MaxStackOverride = 20,
        AssetPath        = "res://assets/sprites/items/consumables/swiftness_draught.png",
        Effect           = new StatusEffectEffect(StatusEffectType.Haste, "SPD", 8, 3),
    };

    public static ConsumableItem CreateAntidote() => new ConsumableItem
    {
        Id          = "antidote",
        DisplayName = "Antidote",
        Description = "Cures Poison and Burn.",
        Value       = 35,
        AssetPath   = "res://assets/sprites/items/consumables/antidote.png",
        Effect      = new CureStatusEffect("Poison & Burn", StatusEffectType.Poison, StatusEffectType.Burn),
    };

    public static ConsumableItem CreateRegenPotion() => new ConsumableItem
    {
        Id               = "regen_potion",
        DisplayName      = "Regen Potion",
        Description      = "Restores 15 HP per turn for 3 turns.",
        Value            = 65,
        MaxStackOverride = 20,
        AssetPath        = "res://assets/sprites/items/consumables/regen_potion.png",
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
        AssetPath        = "res://assets/sprites/items/consumables/poison_vial.png",
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
        AssetPath        = "res://assets/sprites/items/consumables/flash_powder.png",
        Effect           = new EnemyDebuffEffect(StatusEffectType.Blind, 0, 2),
    };

    public static ConsumableItem CreateMajorHealthPotion() => new ConsumableItem
    {
        Id          = "major_health_potion",
        DisplayName = "Major Health Potion",
        Description = "Restores 300 HP instantly.",
        Value       = 150,
        Rarity      = ItemRarity.Rare,
        AssetPath   = "res://assets/sprites/items/consumables/major_health_potion.png",
        Effect      = new HealEffect(300),
    };

    public static ConsumableItem CreateMajorManaPotion() => new ConsumableItem
    {
        Id          = "major_mana_potion",
        DisplayName = "Major Mana Potion",
        Description = "Restores 75 MP instantly.",
        Value       = 130,
        Rarity      = ItemRarity.Rare,
        AssetPath   = "res://assets/sprites/items/consumables/major_mana_potion.png",
        Effect      = new RestoreManaEffect(75),
    };

    public static ConsumableItem CreateWardingCharm() => new ConsumableItem
    {
        Id               = "warding_charm",
        DisplayName      = "Warding Charm",
        Description      = "Raises Defense by 18 for 4 turns.",
        Value            = 120,
        Rarity           = ItemRarity.Uncommon,
        MaxStackOverride = 10,
        AssetPath        = "res://assets/sprites/items/consumables/warding_charm.png",
        Effect           = new StatusEffectEffect(StatusEffectType.Fortify, "DEF", 18, 4),
    };

    public static ConsumableItem CreateSmokeBomb() => new ConsumableItem
    {
        Id               = "smoke_bomb",
        DisplayName      = "Smoke Bomb",
        Description      = "Blinds the enemy for 3 turns (reduces accuracy to 55%).",
        Value            = 95,
        Rarity           = ItemRarity.Uncommon,
        MaxStackOverride = 10,
        AssetPath        = "res://assets/sprites/items/consumables/smoke_bomb.png",
        Effect           = new EnemyDebuffEffect(StatusEffectType.Blind, 0, 3),
    };
}
