using Godot;

/// <summary>
/// Blueprint Resource for enemy spawning. Create instances via Godot editor and assign to EnemySpawn nodes.
/// Each blueprint can be cloned and customized with unique stats for level design.
/// </summary>
[GlobalClass]
public partial class EnemyBlueprint : Resource
{
    // Enemy identity
    [ExportGroup("Identity")]
    [Export] public string EnemyName { get; set; } = "Goblin";
    
    /// <summary>
    /// Visual sprite type (must match folder name in assets/sprites/enemies/).
    /// Common types: goblin, orc, skeleton_warrior, troll, dragon, forest_spirit, 
    /// cave_spider, desert_scorpion, swamp_wretch, mountain_wyvern, dark_mage, 
    /// dungeon_guardian, demon_lord, boss
    /// </summary>
    [Export(PropertyHint.Enum, "goblin,orc,skeleton_warrior,troll,dragon,forest_spirit,cave_spider,desert_scorpion,swamp_wretch,mountain_wyvern,dark_mage,dungeon_guardian,demon_lord,boss")] 
    public string SpriteType { get; set; } = "goblin";

    // Combat stats
    [ExportGroup("Stats")]
    [Export(PropertyHint.Range, "1,100,1")] public int Level { get; set; } = 1;
    [Export(PropertyHint.Range, "1,9999,1")] public int MaxHealth { get; set; } = 50;
    [Export(PropertyHint.Range, "1,999,1")] public int Attack { get; set; } = 15;
    [Export(PropertyHint.Range, "0,999,1")] public int Defense { get; set; } = 5;
    [Export(PropertyHint.Range, "1,999,1")] public int Speed { get; set; } = 10;

    // Rewards
    [ExportGroup("Rewards")]
    [Export(PropertyHint.Range, "0,9999,1")] public int ExperienceReward { get; set; } = 25;
    [Export(PropertyHint.Range, "0,9999,1")] public int GoldReward { get; set; } = 10;

    /// <summary>
    /// Create an Enemy instance from this blueprint with fresh CurrentHealth equal to MaxHealth.
    /// </summary>
    public Enemy CreateEnemy()
    {
        return new Enemy
        {
            Name = EnemyName,
            Level = Level,
            MaxHealth = MaxHealth,
            CurrentHealth = MaxHealth, // Fresh spawn at full health
            Attack = Attack,
            Defense = Defense,
            Speed = Speed,
            ExperienceReward = ExperienceReward,
            GoldReward = GoldReward
        };
    }

    /// <summary>
    /// Factory method: Create a Goblin blueprint with default stats.
    /// </summary>
    public static EnemyBlueprint CreateGoblinBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Goblin",
            SpriteType = "goblin",
            Level = 1,
            MaxHealth = 50,
            Attack = 15,
            Defense = 5,
            Speed = 10,
            ExperienceReward = 25,
            GoldReward = 10
        };
    }

    /// <summary>
    /// Factory method: Create an Orc blueprint with default stats.
    /// </summary>
    public static EnemyBlueprint CreateOrcBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Orc",
            SpriteType = "orc",
            Level = 2,
            MaxHealth = 80,
            Attack = 22,
            Defense = 8,
            Speed = 8,
            ExperienceReward = 45,
            GoldReward = 20
        };
    }

    /// <summary>
    /// Factory method: Create a Dragon blueprint with default stats.
    /// </summary>
    public static EnemyBlueprint CreateDragonBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Dragon",
            SpriteType = "dragon",
            Level = 5,
            MaxHealth = 200,
            Attack = 45,
            Defense = 20,
            Speed = 12,
            ExperienceReward = 180,
            GoldReward = 100
        };
    }

    /// <summary>
    /// Factory method: Create a Boss blueprint with default stats.
    /// </summary>
    public static EnemyBlueprint CreateBossBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Ancient Dragon King",
            SpriteType = "boss",
            Level = 10,
            MaxHealth = 500,
            Attack = 80,
            Defense = 35,
            Speed = 18,
            ExperienceReward = 800,
            GoldReward = 500
        };
    }
}
