/// <summary>
/// Factory methods for monster part GeneralItems dropped from enemies.
/// </summary>
public static class MonsterPartsCatalog
{
    public static GeneralItem CreateGoblinEar()
    {
        return new GeneralItem
        {
            Id = "goblin_ear",
            DisplayName = "Goblin Ear",
            Description = "A pointed ear from a slain goblin. Collectors pay decent coin.",
            Value = 5,
            MaxStackOverride = 99,
            AssetPath = "res://assets/sprites/items/monster_parts/goblin_ear.png"
        };
    }

    public static GeneralItem CreateOrcTusk()
    {
        return new GeneralItem
        {
            Id = "orc_tusk",
            DisplayName = "Orc Tusk",
            Description = "A heavy yellowed tusk. Alchemists use it in strength tonics.",
            Value = 15,
            MaxStackOverride = 50,
            AssetPath = "res://assets/sprites/items/monster_parts/orc_tusk.png"
        };
    }

    public static GeneralItem CreateSkeletonBone()
    {
        return new GeneralItem
        {
            Id = "skeleton_bone",
            DisplayName = "Skeleton Bone",
            Description = "An ancient bone rattling with residual dark energy.",
            Value = 12,
            MaxStackOverride = 99,
            AssetPath = "res://assets/sprites/items/monster_parts/skeleton_bone.png"
        };
    }

    public static GeneralItem CreateSpiderSilk()
    {
        return new GeneralItem
        {
            Id = "spider_silk",
            DisplayName = "Spider Silk",
            Description = "Incredibly strong webbing from a cave spider.",
            Value = 20,
            MaxStackOverride = 50,
            AssetPath = "res://assets/sprites/items/monster_parts/spider_silk.png"
        };
    }

    public static GeneralItem CreateDragonScale()
    {
        return new GeneralItem
        {
            Id = "dragon_scale",
            DisplayName = "Dragon Scale",
            Description = "A gleaming iridescent scale. Rare and extremely valuable.",
            Value = 200,
            Rarity = ItemRarity.Rare,
            MaxStackOverride = 10,
            AssetPath = "res://assets/sprites/items/monster_parts/dragon_scale.png"
        };
    }

    public static GeneralItem CreateSentinelCore() => new GeneralItem
    {
        Id = "sentinel_core",
        DisplayName = "Sentinel Core",
        Description = "A cold stone-and-metal core from a crypt sentinel.",
        Value = 45,
        MaxStackOverride = 50,
        AssetPath = "res://assets/sprites/items/monster_parts/sentinel_core.png"
    };

    public static GeneralItem CreateHexedCloth() => new GeneralItem
    {
        Id = "hexed_cloth",
        DisplayName = "Hexed Cloth",
        Description = "A scrap of cursed fabric threaded with dim runes.",
        Value = 50,
        MaxStackOverride = 50,
        AssetPath = "res://assets/sprites/items/monster_parts/hexed_cloth.png"
    };

    public static GeneralItem CreateSplinteredBone() => new GeneralItem
    {
        Id = "splintered_bone",
        DisplayName = "Splintered Bone",
        Description = "A sharpened bone fragment from a dungeon archer.",
        Value = 38,
        MaxStackOverride = 99,
        AssetPath = "res://assets/sprites/items/monster_parts/splintered_bone.png"
    };

    public static GeneralItem CreateRevenantPlate() => new GeneralItem
    {
        Id = "revenant_plate",
        DisplayName = "Revenant Plate",
        Description = "A battered armor plate still clinging to undead will.",
        Value = 90,
        Rarity = ItemRarity.Uncommon,
        MaxStackOverride = 25,
        AssetPath = "res://assets/sprites/items/monster_parts/revenant_plate.png"
    };

    public static GeneralItem CreateGargoyleShard() => new GeneralItem
    {
        Id = "gargoyle_shard",
        DisplayName = "Gargoyle Shard",
        Description = "A jagged black stone shard from a cursed gargoyle.",
        Value = 100,
        Rarity = ItemRarity.Uncommon,
        MaxStackOverride = 25,
        AssetPath = "res://assets/sprites/items/monster_parts/gargoyle_shard.png"
    };

    public static GeneralItem CreateAbyssalSigil() => new GeneralItem
    {
        Id = "abyssal_sigil",
        DisplayName = "Abyssal Sigil",
        Description = "A small sigil pulsing with abyssal ritual energy.",
        Value = 140,
        Rarity = ItemRarity.Rare,
        MaxStackOverride = 10,
        AssetPath = "res://assets/sprites/items/monster_parts/abyssal_sigil.png"
    };
}
