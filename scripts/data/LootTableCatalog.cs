/// <summary>
/// Centralizes default drop tables for all enemies.
/// Called by BattleManager.EndBattle() via LootTableCatalog.GetByEnemyType().
/// </summary>
public static class LootTableCatalog
{
    /// <summary>
    /// Looks up a drop table by enemy sprite type name (case-insensitive via .ToLower()).
    /// Used by EnemySpawn when creating enemies from blueprints.
    /// Returns null if no drop table is defined for the given type or if enemyType is null.
    /// </summary>
    public static LootTable? GetByEnemyType(string? enemyType)
    {
        return enemyType?.ToLowerInvariant() switch
        {
            EnemyTypeId.Goblin          => CreateGoblinTable(),
            EnemyTypeId.Orc             => CreateOrcTable(),
            EnemyTypeId.SkeletonWarrior => CreateSkeletonWarriorTable(),
            EnemyTypeId.Troll           => CreateTrollTable(),
            EnemyTypeId.Dragon          => CreateDragonTable(),
            EnemyTypeId.ForestSpirit    => CreateForestSpiritTable(),
            EnemyTypeId.CaveSpider      => CreateCaveSpiderTable(),
            EnemyTypeId.DesertScorpion  => CreateDesertScorpionTable(),
            EnemyTypeId.SwampWretch     => CreateSwampWretchTable(),
            EnemyTypeId.MountainWyvern  => CreateMountainWyvernTable(),
            EnemyTypeId.DarkMage        => CreateDarkMageTable(),
            EnemyTypeId.DungeonGuardian => CreateDungeonGuardianTable(),
            EnemyTypeId.DemonLord       => CreateDemonLordTable(),
            EnemyTypeId.Boss            => CreateBossTable(),
            EnemyTypeId.CryptSentinel   => CreateCryptSentinelTable(),
            EnemyTypeId.GraveHexer      => CreateGraveHexerTable(),
            EnemyTypeId.BoneArcher      => CreateBoneArcherTable(),
            EnemyTypeId.IronRevenant    => CreateIronRevenantTable(),
            EnemyTypeId.CursedGargoyle  => CreateCursedGargoyleTable(),
            EnemyTypeId.AbyssAcolyte    => CreateAbyssAcolyteTable(),
            _ => null
        };
    }

    public static LootTable CreateGoblinTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "goblin_ear", Weight = 200, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "mana_potion", Weight = 40, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "wooden_sword", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateOrcTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 180, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "iron_sword", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateSkeletonWarriorTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 180, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_armor", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateTrollTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.90f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 120, MinQuantity = 1, MaxQuantity = 2 }, // Placeholder until troll_hide is added to ItemCatalog
            new LootEntry { ItemId = "iron_shield", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDragonTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            // GuaranteedDrop = true; Weight = 0 by convention (excluded from weighted draws).
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 2, Weight = 0 },
            new LootEntry { ItemId = "iron_sword", Weight = 50, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "iron_armor", Weight = 50, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateForestSpiritTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.80f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "goblin_ear", Weight = 80, MinQuantity = 1, MaxQuantity = 2 } // Placeholder until forest_spirit_essence is added to ItemCatalog
        }
    };

    public static LootTable CreateCaveSpiderTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.85f,
        Entries = new()
        {
            new LootEntry { ItemId = "spider_silk", Weight = 200, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "iron_boots", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDesertScorpionTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "skeleton_bone", Weight = 100, MinQuantity = 1, MaxQuantity = 2 }, // Placeholder until scorpion_claw is added to ItemCatalog
            new LootEntry { ItemId = "iron_helmet", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateSwampWretchTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.88f,
        Entries = new()
        {
            new LootEntry { ItemId = "orc_tusk", Weight = 100, MinQuantity = 1, MaxQuantity = 2 }, // Placeholder until swamp_wretch_part is added to ItemCatalog
            new LootEntry { ItemId = "iron_armor", Weight = 20, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateMountainWyvernTable() => new LootTable
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

    public static LootTable CreateDarkMageTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.95f,
        Entries = new()
        {
            new LootEntry { ItemId = "hexed_cloth", Weight = 140, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "skeleton_bone", Weight = 80, MinQuantity = 2, MaxQuantity = 4 },
            new LootEntry { ItemId = "major_mana_potion", Weight = 36, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "warding_charm", Weight = 24, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDungeonGuardianTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "sentinel_core", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 1, Weight = 0 },
            new LootEntry { ItemId = "revenant_plate", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "steel_longsword", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "chain_mail", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_guard", Weight = 12, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateDemonLordTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "abyssal_sigil", GuaranteedDrop = true, MinQuantity = 2, MaxQuantity = 3, Weight = 0 },
            new LootEntry { ItemId = "obsidian_blade", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_carapace", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "major_health_potion", Weight = 50, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "dragon_scale", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateBossTable() => new LootTable
    {
        MaxDrops = 5,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "abyssal_sigil", GuaranteedDrop = true, MinQuantity = 3, MaxQuantity = 5, Weight = 0 },
            new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 2, MaxQuantity = 4, Weight = 0 },
            new LootEntry { ItemId = "obsidian_blade", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_carapace", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_guard", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_crown", Weight = 100, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_treads", Weight = 100, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateCryptSentinelTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.95f,
        Entries = new()
        {
            new LootEntry { ItemId = "sentinel_core", Weight = 190, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "steel_tower_shield", Weight = 24, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "warding_charm", Weight = 36, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateGraveHexerTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.95f,
        Entries = new()
        {
            new LootEntry { ItemId = "hexed_cloth", Weight = 190, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "major_mana_potion", Weight = 30, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "smoke_bomb", Weight = 34, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateBoneArcherTable() => new LootTable
    {
        MaxDrops = 2,
        DropChance = 0.92f,
        Entries = new()
        {
            new LootEntry { ItemId = "splintered_bone", Weight = 200, MinQuantity = 1, MaxQuantity = 3 },
            new LootEntry { ItemId = "swift_boots", Weight = 24, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "smoke_bomb", Weight = 30, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateIronRevenantTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "revenant_plate", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 1, Weight = 0 },
            new LootEntry { ItemId = "steel_longsword", Weight = 55, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "chain_mail", Weight = 55, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_blade", Weight = 10, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateCursedGargoyleTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "gargoyle_shard", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 2, Weight = 0 },
            new LootEntry { ItemId = "knight_helm", Weight = 50, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "steel_tower_shield", Weight = 45, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_carapace", Weight = 10, MinQuantity = 1, MaxQuantity = 1 }
        }
    };

    public static LootTable CreateAbyssAcolyteTable() => new LootTable
    {
        MaxDrops = 3,
        DropChance = 1.0f,
        Entries = new()
        {
            new LootEntry { ItemId = "abyssal_sigil", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 2, Weight = 0 },
            new LootEntry { ItemId = "major_mana_potion", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_crown", Weight = 16, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_treads", Weight = 16, MinQuantity = 1, MaxQuantity = 1 }
        }
    };
}
