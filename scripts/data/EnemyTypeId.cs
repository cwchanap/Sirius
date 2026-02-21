/// <summary>
/// String constants for all 14 enemy types.
/// Single source of truth referenced by Enemy.Create*(), EnemyBlueprint.SpriteType,
/// and LootTableCatalog.GetByEnemyType().
/// Adding a new enemy type: add a constant here, then add a blueprint factory in
/// EnemyBlueprint, a factory method in Enemy, and a table method in LootTableCatalog.
/// </summary>
public static class EnemyTypeId
{
    public const string Goblin          = "goblin";
    public const string Orc             = "orc";
    public const string Dragon          = "dragon";
    public const string SkeletonWarrior = "skeleton_warrior";
    public const string Troll           = "troll";
    public const string DarkMage        = "dark_mage";
    public const string DemonLord       = "demon_lord";
    public const string Boss            = "boss";
    public const string ForestSpirit    = "forest_spirit";
    public const string CaveSpider      = "cave_spider";
    public const string DesertScorpion  = "desert_scorpion";
    public const string SwampWretch     = "swamp_wretch";
    public const string MountainWyvern  = "mountain_wyvern";
    public const string DungeonGuardian = "dungeon_guardian";
}
