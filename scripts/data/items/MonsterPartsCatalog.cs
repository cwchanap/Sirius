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
            MaxStackOverride = 99
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
            MaxStackOverride = 50
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
            MaxStackOverride = 99
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
            MaxStackOverride = 50
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
            MaxStackOverride = 10
        };
    }
}
