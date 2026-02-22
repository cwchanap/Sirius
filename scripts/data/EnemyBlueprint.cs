using Godot;

/// <summary>
/// Blueprint Resource for enemy spawning. Create instances via Godot editor and assign to EnemySpawn nodes.
/// Each blueprint can be cloned and customized with unique stats for level design.
/// To give a spawn node unique stats, use 'Make Unique' in the Godot Inspector or
/// set EnemySpawn.AutoMakeBlueprintUnique = true.
/// </summary>
[GlobalClass]
[System.Serializable]
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
    /// EnemyType is set from SpriteType, used by LootTableCatalog.GetByEnemyType() to look up
    /// loot tables after combat.
    /// </summary>
    public Enemy CreateEnemy()
    {
        return new Enemy
        {
            Name = EnemyName,
            EnemyType = SpriteType,
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

    /// <summary>Factory method: Create a Goblin blueprint with default stats.</summary>
    public static EnemyBlueprint CreateGoblinBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Goblin",
            SpriteType = EnemyTypeId.Goblin,
            Level = 1,
            MaxHealth = 50,
            Attack = 15,
            Defense = 5,
            Speed = 10,
            ExperienceReward = 25,
            GoldReward = 10
        };
    }

    /// <summary>Factory method: Create an Orc blueprint with default stats.</summary>
    public static EnemyBlueprint CreateOrcBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Orc",
            SpriteType = EnemyTypeId.Orc,
            Level = 2,
            MaxHealth = 80,
            Attack = 22,
            Defense = 8,
            Speed = 8,
            ExperienceReward = 45,
            GoldReward = 20
        };
    }

    /// <summary>Factory method: Create a Dragon blueprint with default stats.</summary>
    public static EnemyBlueprint CreateDragonBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Dragon",
            SpriteType = EnemyTypeId.Dragon,
            Level = 5,
            MaxHealth = 200,
            Attack = 45,
            Defense = 20,
            Speed = 12,
            ExperienceReward = 180,
            GoldReward = 100
        };
    }

    /// <summary>Factory method: Create a Boss blueprint with default stats.</summary>
    public static EnemyBlueprint CreateBossBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Ancient Dragon King",
            SpriteType = EnemyTypeId.Boss,
            Level = 10,
            MaxHealth = 500,
            Attack = 80,
            Defense = 35,
            Speed = 18,
            ExperienceReward = 800,
            GoldReward = 500
        };
    }

    /// <summary>Factory method: Create a Skeleton Warrior blueprint with default stats.</summary>
    public static EnemyBlueprint CreateSkeletonWarriorBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Skeleton Warrior",
            SpriteType = EnemyTypeId.SkeletonWarrior,
            Level = 3,
            MaxHealth = 120,
            Attack = 28,
            Defense = 12,
            Speed = 9,
            ExperienceReward = 70,
            GoldReward = 30
        };
    }

    /// <summary>Factory method: Create a Troll blueprint with default stats.</summary>
    public static EnemyBlueprint CreateTrollBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Troll",
            SpriteType = EnemyTypeId.Troll,
            Level = 4,
            MaxHealth = 150,
            Attack = 35,
            Defense = 15,
            Speed = 6,
            ExperienceReward = 120,
            GoldReward = 50
        };
    }

    /// <summary>Factory method: Create a Dark Mage blueprint with default stats.</summary>
    public static EnemyBlueprint CreateDarkMageBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Dark Mage",
            SpriteType = EnemyTypeId.DarkMage,
            Level = 6,
            MaxHealth = 180,
            Attack = 50,
            Defense = 18,
            Speed = 14,
            ExperienceReward = 220,
            GoldReward = 120
        };
    }

    /// <summary>Factory method: Create a Demon Lord blueprint with default stats.</summary>
    public static EnemyBlueprint CreateDemonLordBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Demon Lord",
            SpriteType = EnemyTypeId.DemonLord,
            Level = 8,
            MaxHealth = 300,
            Attack = 65,
            Defense = 25,
            Speed = 15,
            ExperienceReward = 400,
            GoldReward = 200
        };
    }

    /// <summary>Factory method: Create a Forest Spirit blueprint with default stats.</summary>
    public static EnemyBlueprint CreateForestSpiritBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Forest Spirit",
            SpriteType = EnemyTypeId.ForestSpirit,
            Level = 2,
            MaxHealth = 90,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            ExperienceReward = 50,
            GoldReward = 22
        };
    }

    /// <summary>Factory method: Create a Giant Cave Spider blueprint with default stats.</summary>
    public static EnemyBlueprint CreateCaveSpiderBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Giant Cave Spider",
            SpriteType = EnemyTypeId.CaveSpider,
            Level = 3,
            MaxHealth = 110,
            Attack = 25,
            Defense = 8,
            Speed = 18,
            ExperienceReward = 65,
            GoldReward = 28
        };
    }

    /// <summary>Factory method: Create a Desert Scorpion blueprint with default stats.</summary>
    public static EnemyBlueprint CreateDesertScorpionBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Desert Scorpion",
            SpriteType = EnemyTypeId.DesertScorpion,
            Level = 4,
            MaxHealth = 130,
            Attack = 32,
            Defense = 14,
            Speed = 11,
            ExperienceReward = 95,
            GoldReward = 45
        };
    }

    /// <summary>Factory method: Create a Swamp Wretch blueprint with default stats.</summary>
    public static EnemyBlueprint CreateSwampWretchBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Swamp Wretch",
            SpriteType = EnemyTypeId.SwampWretch,
            Level = 5,
            MaxHealth = 160,
            Attack = 38,
            Defense = 16,
            Speed = 7,
            ExperienceReward = 140,
            GoldReward = 70
        };
    }

    /// <summary>Factory method: Create a Mountain Wyvern blueprint with default stats.</summary>
    public static EnemyBlueprint CreateMountainWyvernBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Mountain Wyvern",
            SpriteType = EnemyTypeId.MountainWyvern,
            Level = 6,
            MaxHealth = 220,
            Attack = 48,
            Defense = 22,
            Speed = 16,
            ExperienceReward = 200,
            GoldReward = 110
        };
    }

    /// <summary>Factory method: Create a Dungeon Guardian blueprint with default stats.</summary>
    public static EnemyBlueprint CreateDungeonGuardianBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Dungeon Guardian",
            SpriteType = EnemyTypeId.DungeonGuardian,
            Level = 7,
            MaxHealth = 280,
            Attack = 55,
            Defense = 28,
            Speed = 10,
            ExperienceReward = 300,
            GoldReward = 150
        };
    }
}
