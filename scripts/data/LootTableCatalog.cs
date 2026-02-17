/// <summary>
/// Centralizes default drop tables for all enemies.
/// Used by Enemy.CreateX() static factory methods and EnemySpawn blueprint path.
/// </summary>
public static class LootTableCatalog
{
    /// <summary>
    /// Looks up a drop table by enemy sprite type name.
    /// Used by EnemySpawn when creating enemies from blueprints.
    /// Returns null if no drop table is defined for the given type.
    /// </summary>
    public static LootTable? GetByEnemyType(string enemyType)
    {
        return enemyType?.ToLower() switch
        {
            "goblin" => GoblinDrops(),
            "orc" => OrcDrops(),
            "skeleton_warrior" => SkeletonWarriorDrops(),
            "troll" => TrollDrops(),
            "dragon" => DragonDrops(),
            "forest_spirit" => ForestSpiritDrops(),
            "cave_spider" => CaveSpiderDrops(),
            "desert_scorpion" => DesertScorpionDrops(),
            "swamp_wretch" => SwampWretchDrops(),
            "mountain_wyvern" => MountainWyvernDrops(),
            "dark_mage" => DarkMageDrops(),
            "dungeon_guardian" => DungeonGuardianDrops(),
            "demon_lord" => DemonLordDrops(),
            "boss" => BossDrops(),
            _ => null
        };
    }

    public static LootTable GoblinDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "goblin_ear", Weight = 200, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "wooden_sword", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable OrcDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 180, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_sword", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable SkeletonWarriorDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 180, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_armor", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable TrollDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 120, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_shield", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable DragonDrops() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 2, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 50, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 50, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable ForestSpiritDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.80f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "goblin_ear", Weight = 80, MinQuantity = 1, MaxQuantity = 2 }
        }
    };

    public static LootTable CaveSpiderDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 200, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_boots", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable DesertScorpionDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_helmet", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable SwampWretchDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_armor", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable MountainWyvernDrops() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_sword", Weight = 40, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_boots", Weight = 40, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable DarkMageDrops() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.95f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 100, MinQuantity = 2, MaxQuantity = 4 },
            new LootEntry { ItemId = "iron_helmet", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable DungeonGuardianDrops() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "iron_sword", Weight = 80, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 80, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_shield", Weight = 60, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable DemonLordDrops() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 2, MaxQuantity = 3, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 100, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable BossDrops() => new LootTable
    {
        MaxDrops = 5,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 3, MaxQuantity = 5, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_shield", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_helmet", Weight = 100, MinQuantity = 1, MaxQuantity = 1 }
        }
    };
}
