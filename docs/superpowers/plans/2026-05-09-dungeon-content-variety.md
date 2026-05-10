# Dungeon Content Variety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dungeon-only content expansion with six new enemies, two equipment tiers, dungeon monster parts, dungeon consumables, loot refreshes, generated assets, tests, and docs.

**Architecture:** Keep the existing static catalog pattern. Add a small `EncounterTables` helper to centralize enemy type selection and factory dispatch so `Game`, `GridMap`, and `EnemySpawn` do not drift. Art generation follows `.codex/skills/manage-asset-generation/SKILL.md`: check canonical paths first, generate only missing assets, post-process, verify dimensions/transparency, then update docs.

**Tech Stack:** Godot 4.5.1, C# .NET 8.0, GdUnit4, Python asset tools, PNG image assets.

---

## File Structure

- Create `scripts/game/EncounterTables.cs`: central factory and dungeon encounter selector for enemy type IDs.
- Modify `scripts/data/EnemyTypeId.cs`: add six dungeon enemy ID constants.
- Modify `scripts/data/EnemyBlueprint.cs`: add six `Create*Blueprint()` factories.
- Modify `scripts/data/Enemy.cs`: add six `Create*()` factories.
- Modify `scripts/game/EnemySpawn.cs`: route legacy string creation through `EncounterTables.CreateEnemyByType()`.
- Modify `scripts/data/EnemyDebuffProfile.cs`: add debuff profiles for four new dungeon enemies.
- Modify `scripts/data/items/EquipmentCatalog.cs`: add steel and obsidian equipment factories.
- Modify `scripts/data/items/ConsumableCatalog.cs`: add four dungeon consumables with asset paths.
- Modify `scripts/data/items/MonsterPartsCatalog.cs`: add six dungeon monster parts with asset paths.
- Modify `scripts/data/items/ItemCatalog.cs`: register all 20 new item IDs.
- Modify `scripts/data/LootTableCatalog.cs`: add six new loot tables and refresh existing dungeon-adjacent tables.
- Modify `scripts/game/Game.cs`: use `EncounterTables.SelectDungeonEnemyType()` for dungeon area battles.
- Modify `scripts/game/GridMap.cs`: use `EncounterTables.SelectDungeonEnemyType()` for dungeon area markers.
- Create `tests/data/DungeonContentCatalogTest.cs`: catalog, stat, loot, and debuff guardrails.
- Create `tests/game/EncounterTablesTest.cs`: encounter table and factory guardrails.
- Modify `docs/enemies/ENEMY_SPRITES.md`: add six new dungeon enemy art rows and prompts.
- Modify `docs/items/ASSET_STATUS.md`: add new item icon rows and update counts.
- Modify `docs/items/ITEM_PROMPT_GUIDE.md`: add prompt/status entries for the new item icons.
- Modify `docs/items/items-guide.md`: add dungeon items, equipment, and enemy debuff summary rows.
- Add generated enemy assets under `assets/sprites/enemies/{enemy_type}/`.
- Add generated item icons under `assets/sprites/items/{category}/`.

---

### Task 1: Add Failing Dungeon Content Tests

**Files:**
- Create: `tests/data/DungeonContentCatalogTest.cs`
- Create: `tests/game/EncounterTablesTest.cs`

- [ ] **Step 1: Create catalog tests for the approved dungeon content**

Create `tests/data/DungeonContentCatalogTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class DungeonContentCatalogTest : Node
{
    private static readonly string[] NewEnemyTypes =
    {
        EnemyTypeId.CryptSentinel,
        EnemyTypeId.GraveHexer,
        EnemyTypeId.BoneArcher,
        EnemyTypeId.IronRevenant,
        EnemyTypeId.CursedGargoyle,
        EnemyTypeId.AbyssAcolyte,
    };

    private static readonly string[] NewMonsterParts =
    {
        "sentinel_core",
        "hexed_cloth",
        "splintered_bone",
        "revenant_plate",
        "gargoyle_shard",
        "abyssal_sigil",
    };

    private static readonly string[] NewEquipment =
    {
        "steel_longsword",
        "chain_mail",
        "steel_tower_shield",
        "knight_helm",
        "swift_boots",
        "obsidian_blade",
        "obsidian_carapace",
        "obsidian_guard",
        "obsidian_crown",
        "obsidian_treads",
    };

    private static readonly string[] NewConsumables =
    {
        "major_health_potion",
        "major_mana_potion",
        "warding_charm",
        "smoke_bomb",
    };

    [TestCase]
    public void NewDungeonEnemyFactories_CreateExpectedEnemyTypes()
    {
        var enemies = new[]
        {
            Enemy.CreateCryptSentinel(),
            Enemy.CreateGraveHexer(),
            Enemy.CreateBoneArcher(),
            Enemy.CreateIronRevenant(),
            Enemy.CreateCursedGargoyle(),
            Enemy.CreateAbyssAcolyte(),
        };

        var createdTypes = enemies.Select(e => e.EnemyType).ToHashSet();
        foreach (var expectedType in NewEnemyTypes)
        {
            AssertThat(createdTypes.Contains(expectedType))
                .OverrideFailureMessage($"Expected factory coverage for '{expectedType}'.")
                .IsTrue();
        }

        foreach (var enemy in enemies)
        {
            AssertThat(enemy.Name).IsNotEmpty();
            AssertThat(enemy.Level).IsGreaterEqual(6);
            AssertThat(enemy.MaxHealth).IsGreater(100);
            AssertThat(enemy.CurrentHealth).IsEqual(enemy.MaxHealth);
            AssertThat(enemy.Attack).IsGreater(0);
            AssertThat(enemy.Defense).IsGreaterEqual(0);
            AssertThat(enemy.Speed).IsGreater(0);
            AssertThat(enemy.ExperienceReward).IsGreater(0);
            AssertThat(enemy.GoldReward).IsGreater(0);
        }
    }

    [TestCase]
    public void NewDungeonEnemyLootTables_ResolveAllItems()
    {
        foreach (var enemyType in NewEnemyTypes)
        {
            var table = LootTableCatalog.GetByEnemyType(enemyType);
            AssertThat(table).IsNotNull();
            AssertThat(table!.Entries.Count).IsGreater(0);

            foreach (var entry in table.Entries)
            {
                AssertThat(ItemCatalog.CreateItemById(entry.ItemId))
                    .OverrideFailureMessage($"Loot entry '{entry.ItemId}' for '{enemyType}' must resolve through ItemCatalog.")
                    .IsNotNull();
            }
        }
    }

    [TestCase]
    public void ExistingDungeonLootTables_IncludeDungeonSpecificRewards()
    {
        AssertTableContains(EnemyTypeId.DarkMage, "hexed_cloth");
        AssertTableContains(EnemyTypeId.DungeonGuardian, "sentinel_core");
        AssertTableContains(EnemyTypeId.DungeonGuardian, "revenant_plate");
        AssertTableContains(EnemyTypeId.DemonLord, "abyssal_sigil");
        AssertTableContains(EnemyTypeId.Boss, "abyssal_sigil");
    }

    [TestCase]
    public void ItemCatalog_RegistersNewDungeonItems()
    {
        foreach (var id in NewMonsterParts.Concat(NewEquipment).Concat(NewConsumables))
        {
            AssertThat(ItemCatalog.ItemExists(id))
                .OverrideFailureMessage($"Expected ItemCatalog to register '{id}'.")
                .IsTrue();
            AssertThat(ItemCatalog.CreateItemById(id)).IsNotNull();
        }
    }

    [TestCase]
    public void DungeonMonsterParts_AreStackableSellableAndHaveAssets()
    {
        foreach (var id in NewMonsterParts)
        {
            var item = ItemCatalog.CreateItemById(id);
            AssertThat(item).IsInstanceOf<GeneralItem>();
            AssertThat(item!.Value).IsGreater(0);
            AssertThat(item.CanStack).IsTrue();
            AssertThat(item.AssetPath).StartsWith("res://assets/sprites/items/monster_parts/");
            AssertThat(item.AssetPath).EndsWith($"{id}.png");
        }
    }

    [TestCase]
    public void DungeonEquipment_HasSlotsStatsRarityValueAndAssets()
    {
        var expectedSlots = new Dictionary<string, EquipmentSlotType>
        {
            ["steel_longsword"] = EquipmentSlotType.Weapon,
            ["chain_mail"] = EquipmentSlotType.Armor,
            ["steel_tower_shield"] = EquipmentSlotType.Shield,
            ["knight_helm"] = EquipmentSlotType.Helmet,
            ["swift_boots"] = EquipmentSlotType.Shoe,
            ["obsidian_blade"] = EquipmentSlotType.Weapon,
            ["obsidian_carapace"] = EquipmentSlotType.Armor,
            ["obsidian_guard"] = EquipmentSlotType.Shield,
            ["obsidian_crown"] = EquipmentSlotType.Helmet,
            ["obsidian_treads"] = EquipmentSlotType.Shoe,
        };

        foreach (var (id, slot) in expectedSlots)
        {
            var item = ItemCatalog.CreateItemById(id);
            AssertThat(item).IsInstanceOf<EquipmentItem>();
            var equipment = (EquipmentItem)item!;
            AssertThat(equipment.SlotType).IsEqual(slot);
            AssertThat(equipment.Value).IsGreater(0);
            AssertThat((int)equipment.Rarity).IsGreaterEqual((int)ItemRarity.Uncommon);
            AssertThat(equipment.AttackBonus + equipment.DefenseBonus + equipment.SpeedBonus + equipment.HealthBonus).IsGreater(0);
            AssertThat(equipment.AssetPath).StartsWith("res://assets/sprites/items/");
            AssertThat(equipment.AssetPath).EndsWith($"{id}.png");
        }
    }

    [TestCase]
    public void DungeonConsumables_HaveEffectsValuesRarityStackingAndAssets()
    {
        foreach (var id in NewConsumables)
        {
            var item = ItemCatalog.CreateItemById(id);
            AssertThat(item).IsInstanceOf<ConsumableItem>();
            var consumable = (ConsumableItem)item!;
            AssertThat(consumable.Effect).IsNotNull();
            AssertThat(consumable.Value).IsGreater(0);
            AssertThat((int)consumable.Rarity).IsGreaterEqual((int)ItemRarity.Uncommon);
            AssertThat(consumable.CanStack).IsTrue();
            AssertThat(consumable.AssetPath).StartsWith("res://assets/sprites/items/consumables/");
            AssertThat(consumable.AssetPath).EndsWith($"{id}.png");
        }
    }

    [TestCase]
    public void DungeonEnemiesWithStatusIdentity_HaveDebuffProfiles()
    {
        AssertThat(EnemyDebuffProfile.GetAbilities(EnemyTypeId.GraveHexer).Count).IsGreater(0);
        AssertThat(EnemyDebuffProfile.GetAbilities(EnemyTypeId.BoneArcher).Count).IsGreater(0);
        AssertThat(EnemyDebuffProfile.GetAbilities(EnemyTypeId.CursedGargoyle).Count).IsGreater(0);
        AssertThat(EnemyDebuffProfile.GetAbilities(EnemyTypeId.AbyssAcolyte).Count).IsGreater(0);
    }

    private static void AssertTableContains(string enemyType, string itemId)
    {
        var table = LootTableCatalog.GetByEnemyType(enemyType);
        AssertThat(table).IsNotNull();
        AssertThat(table!.Entries.Any(entry => entry.ItemId == itemId))
            .OverrideFailureMessage($"Expected '{enemyType}' loot table to include '{itemId}'.")
            .IsTrue();
    }
}
```

- [ ] **Step 2: Create encounter/factory tests**

Create `tests/game/EncounterTablesTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class EncounterTablesTest : Node
{
    [TestCase]
    public void CreateEnemyByType_CreatesNewDungeonEnemies()
    {
        string[] types =
        {
            EnemyTypeId.CryptSentinel,
            EnemyTypeId.GraveHexer,
            EnemyTypeId.BoneArcher,
            EnemyTypeId.IronRevenant,
            EnemyTypeId.CursedGargoyle,
            EnemyTypeId.AbyssAcolyte,
        };

        foreach (var type in types)
        {
            var enemy = EncounterTables.CreateEnemyByType(type);
            AssertThat(enemy).IsNotNull();
            AssertThat(enemy!.EnemyType).IsEqual(type);
        }
    }

    [TestCase]
    public void CreateEnemyByType_ReturnsNullForUnknownType()
    {
        AssertThat(EncounterTables.CreateEnemyByType("unknown_dungeon_enemy")).IsNull();
        AssertThat(EncounterTables.CreateEnemyByType(null)).IsNull();
        AssertThat(EncounterTables.CreateEnemyByType("")).IsNull();
    }

    [TestCase]
    public void SelectDungeonEnemyType_CoversAllNewDungeonEnemies()
    {
        var selected = new[]
        {
            EncounterTables.SelectDungeonEnemyType(0.05f),
            EncounterTables.SelectDungeonEnemyType(0.18f),
            EncounterTables.SelectDungeonEnemyType(0.31f),
            EncounterTables.SelectDungeonEnemyType(0.48f),
            EncounterTables.SelectDungeonEnemyType(0.64f),
            EncounterTables.SelectDungeonEnemyType(0.80f),
            EncounterTables.SelectDungeonEnemyType(0.94f),
        };

        AssertThat(selected.Contains(EnemyTypeId.DungeonGuardian)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.CryptSentinel)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.GraveHexer)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.BoneArcher)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.IronRevenant)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.CursedGargoyle)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.AbyssAcolyte)).IsTrue();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail for missing content**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DungeonContentCatalogTest|FullyQualifiedName~EncounterTablesTest"
```

Expected: FAIL with compile errors for missing `EnemyTypeId.CryptSentinel`, missing `EncounterTables`, missing enemy factories, and missing item registrations.

- [ ] **Step 4: Commit failing tests**

```bash
git add tests/data/DungeonContentCatalogTest.cs tests/game/EncounterTablesTest.cs
git commit -m "test: add dungeon content expansion guardrails"
```

---

### Task 2: Add Enemy IDs, Factories, And Encounter Factory

**Files:**
- Modify: `scripts/data/EnemyTypeId.cs`
- Modify: `scripts/data/EnemyBlueprint.cs`
- Modify: `scripts/data/Enemy.cs`
- Create: `scripts/game/EncounterTables.cs`
- Modify: `scripts/game/EnemySpawn.cs`

- [ ] **Step 1: Add enemy type constants**

Append these constants inside `EnemyTypeId`:

```csharp
    public const string CryptSentinel   = "crypt_sentinel";
    public const string GraveHexer      = "grave_hexer";
    public const string BoneArcher      = "bone_archer";
    public const string IronRevenant    = "iron_revenant";
    public const string CursedGargoyle  = "cursed_gargoyle";
    public const string AbyssAcolyte    = "abyss_acolyte";
```

Update the XML summary line from `String constants for all 14 enemy types.` to:

```csharp
/// String constants for all enemy types.
```

- [ ] **Step 2: Add enemy blueprint factories**

Append these methods to `EnemyBlueprint`:

```csharp
    public static EnemyBlueprint CreateCryptSentinelBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Crypt Sentinel",
            SpriteType = EnemyTypeId.CryptSentinel,
            Level = 7,
            MaxHealth = 240,
            Attack = 48,
            Defense = 32,
            Speed = 7,
            ExperienceReward = 260,
            GoldReward = 140
        };
    }

    public static EnemyBlueprint CreateGraveHexerBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Grave Hexer",
            SpriteType = EnemyTypeId.GraveHexer,
            Level = 7,
            MaxHealth = 180,
            Attack = 52,
            Defense = 18,
            Speed = 14,
            ExperienceReward = 270,
            GoldReward = 160
        };
    }

    public static EnemyBlueprint CreateBoneArcherBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Bone Archer",
            SpriteType = EnemyTypeId.BoneArcher,
            Level = 6,
            MaxHealth = 150,
            Attack = 46,
            Defense = 14,
            Speed = 20,
            ExperienceReward = 230,
            GoldReward = 120
        };
    }

    public static EnemyBlueprint CreateIronRevenantBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Iron Revenant",
            SpriteType = EnemyTypeId.IronRevenant,
            Level = 8,
            MaxHealth = 320,
            Attack = 64,
            Defense = 30,
            Speed = 11,
            ExperienceReward = 360,
            GoldReward = 190
        };
    }

    public static EnemyBlueprint CreateCursedGargoyleBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Cursed Gargoyle",
            SpriteType = EnemyTypeId.CursedGargoyle,
            Level = 8,
            MaxHealth = 360,
            Attack = 58,
            Defense = 34,
            Speed = 8,
            ExperienceReward = 380,
            GoldReward = 210
        };
    }

    public static EnemyBlueprint CreateAbyssAcolyteBlueprint()
    {
        return new EnemyBlueprint
        {
            EnemyName = "Abyss Acolyte",
            SpriteType = EnemyTypeId.AbyssAcolyte,
            Level = 9,
            MaxHealth = 260,
            Attack = 70,
            Defense = 22,
            Speed = 16,
            ExperienceReward = 450,
            GoldReward = 260
        };
    }
```

Update the `SpriteType` export hint string in `EnemyBlueprint` to include:

```text
crypt_sentinel,grave_hexer,bone_archer,iron_revenant,cursed_gargoyle,abyss_acolyte
```

- [ ] **Step 3: Add enemy factory methods**

Append to `Enemy.cs`:

```csharp
    public static Enemy CreateCryptSentinel()  => EnemyBlueprint.CreateCryptSentinelBlueprint().CreateEnemy();
    public static Enemy CreateGraveHexer()     => EnemyBlueprint.CreateGraveHexerBlueprint().CreateEnemy();
    public static Enemy CreateBoneArcher()     => EnemyBlueprint.CreateBoneArcherBlueprint().CreateEnemy();
    public static Enemy CreateIronRevenant()   => EnemyBlueprint.CreateIronRevenantBlueprint().CreateEnemy();
    public static Enemy CreateCursedGargoyle() => EnemyBlueprint.CreateCursedGargoyleBlueprint().CreateEnemy();
    public static Enemy CreateAbyssAcolyte()   => EnemyBlueprint.CreateAbyssAcolyteBlueprint().CreateEnemy();
```

- [ ] **Step 4: Add the central encounter helper**

Create `scripts/game/EncounterTables.cs`:

```csharp
using Godot;

public static class EncounterTables
{
    public static Enemy? CreateEnemyByType(string? enemyType)
    {
        if (string.IsNullOrWhiteSpace(enemyType))
            return null;

        return enemyType.ToLowerInvariant() switch
        {
            EnemyTypeId.Goblin          => Enemy.CreateGoblin(),
            EnemyTypeId.Orc             => Enemy.CreateOrc(),
            EnemyTypeId.SkeletonWarrior => Enemy.CreateSkeletonWarrior(),
            EnemyTypeId.Troll           => Enemy.CreateTroll(),
            EnemyTypeId.Dragon          => Enemy.CreateDragon(),
            EnemyTypeId.ForestSpirit    => Enemy.CreateForestSpirit(),
            EnemyTypeId.CaveSpider      => Enemy.CreateCaveSpider(),
            EnemyTypeId.DesertScorpion  => Enemy.CreateDesertScorpion(),
            EnemyTypeId.SwampWretch     => Enemy.CreateSwampWretch(),
            EnemyTypeId.MountainWyvern  => Enemy.CreateMountainWyvern(),
            EnemyTypeId.DarkMage        => Enemy.CreateDarkMage(),
            EnemyTypeId.DungeonGuardian => Enemy.CreateDungeonGuardian(),
            EnemyTypeId.DemonLord       => Enemy.CreateDemonLord(),
            EnemyTypeId.Boss            => Enemy.CreateBoss(),
            EnemyTypeId.CryptSentinel   => Enemy.CreateCryptSentinel(),
            EnemyTypeId.GraveHexer      => Enemy.CreateGraveHexer(),
            EnemyTypeId.BoneArcher      => Enemy.CreateBoneArcher(),
            EnemyTypeId.IronRevenant    => Enemy.CreateIronRevenant(),
            EnemyTypeId.CursedGargoyle  => Enemy.CreateCursedGargoyle(),
            EnemyTypeId.AbyssAcolyte    => Enemy.CreateAbyssAcolyte(),
            _ => null,
        };
    }

    public static string SelectDungeonEnemyType(float roll)
    {
        float value = Mathf.Clamp(roll, 0f, 0.999999f);

        if (value < 0.12f) return EnemyTypeId.DungeonGuardian;
        if (value < 0.24f) return EnemyTypeId.CryptSentinel;
        if (value < 0.36f) return EnemyTypeId.GraveHexer;
        if (value < 0.50f) return EnemyTypeId.BoneArcher;
        if (value < 0.66f) return EnemyTypeId.IronRevenant;
        if (value < 0.82f) return EnemyTypeId.CursedGargoyle;
        return EnemyTypeId.AbyssAcolyte;
    }
}
```

- [ ] **Step 5: Replace the legacy EnemySpawn switch with EncounterTables**

In `EnemySpawn.CreateEnemyInstance()`, replace the legacy switch block:

```csharp
            string type = EnemyType.ToLower();
            return type switch
            {
                "goblin" => Enemy.CreateGoblin(),
                "orc" => Enemy.CreateOrc(),
                "skeleton_warrior" => Enemy.CreateSkeletonWarrior(),
                "troll" => Enemy.CreateTroll(),
                "dragon" => Enemy.CreateDragon(),
                "forest_spirit" => Enemy.CreateForestSpirit(),
                "cave_spider" => Enemy.CreateCaveSpider(),
                "desert_scorpion" => Enemy.CreateDesertScorpion(),
                "swamp_wretch" => Enemy.CreateSwampWretch(),
                "mountain_wyvern" => Enemy.CreateMountainWyvern(),
                "dark_mage" => Enemy.CreateDarkMage(),
                "dungeon_guardian" => Enemy.CreateDungeonGuardian(),
                "demon_lord" => Enemy.CreateDemonLord(),
                "boss" => Enemy.CreateBoss(),
                _ => LogAndDefaultToGoblin(type, GridPosition)
            };
```

with:

```csharp
            string type = EnemyType.ToLowerInvariant();
            var enemy = EncounterTables.CreateEnemyByType(type);
            return enemy ?? LogAndDefaultToGoblin(type, GridPosition);
```

Update the legacy support comments and export enum string in `EnemySpawn` to include the six new dungeon IDs:

```text
crypt_sentinel,grave_hexer,bone_archer,iron_revenant,cursed_gargoyle,abyss_acolyte
```

- [ ] **Step 6: Run enemy and encounter tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~EncounterTablesTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~EnemyTest"
```

Expected: `EncounterTablesTest.CreateEnemyByType_CreatesNewDungeonEnemies` passes. Existing `EnemySpawnTest` and `EnemyTest` pass.

- [ ] **Step 7: Commit enemy factory work**

```bash
git add scripts/data/EnemyTypeId.cs scripts/data/EnemyBlueprint.cs scripts/data/Enemy.cs scripts/game/EncounterTables.cs scripts/game/EnemySpawn.cs tests/game/EncounterTablesTest.cs
git commit -m "feat: add dungeon enemy factories"
```

---

### Task 3: Add Dungeon Items And Register Them

**Files:**
- Modify: `scripts/data/items/EquipmentCatalog.cs`
- Modify: `scripts/data/items/ConsumableCatalog.cs`
- Modify: `scripts/data/items/MonsterPartsCatalog.cs`
- Modify: `scripts/data/items/ItemCatalog.cs`

- [ ] **Step 1: Add steel equipment factories**

Append to `EquipmentCatalog`:

```csharp
    public static EquipmentItem CreateSteelLongsword() => new EquipmentItem
    {
        Id = "steel_longsword",
        DisplayName = "Steel Longsword",
        Description = "A balanced dungeon-forged blade with a keen edge.",
        Value = 180,
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 32,
        AssetPath = "res://assets/sprites/items/weapons/steel_longsword.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateChainMail() => new EquipmentItem
    {
        Id = "chain_mail",
        DisplayName = "Chain Mail",
        Description = "Interlocked steel rings that turn aside dungeon blades.",
        Value = 190,
        SlotType = EquipmentSlotType.Armor,
        DefenseBonus = 26,
        HealthBonus = 50,
        AssetPath = "res://assets/sprites/items/armor/chain_mail.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateSteelTowerShield() => new EquipmentItem
    {
        Id = "steel_tower_shield",
        DisplayName = "Steel Tower Shield",
        Description = "A tall steel shield built for holding narrow corridors.",
        Value = 170,
        SlotType = EquipmentSlotType.Shield,
        DefenseBonus = 18,
        HealthBonus = 30,
        AssetPath = "res://assets/sprites/items/shields/steel_tower_shield.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateKnightHelm() => new EquipmentItem
    {
        Id = "knight_helm",
        DisplayName = "Knight Helm",
        Description = "A sturdy helm worn by veteran dungeon guards.",
        Value = 160,
        SlotType = EquipmentSlotType.Helmet,
        DefenseBonus = 8,
        HealthBonus = 160,
        AssetPath = "res://assets/sprites/items/helmet/knight_helm.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateSwiftBoots() => new EquipmentItem
    {
        Id = "swift_boots",
        DisplayName = "Swift Boots",
        Description = "Light boots with steel caps and quiet soles.",
        Value = 165,
        SlotType = EquipmentSlotType.Shoe,
        DefenseBonus = 4,
        SpeedBonus = 8,
        AssetPath = "res://assets/sprites/items/shoes/swift_boots.png",
        Rarity = ItemRarity.Uncommon
    };
```

- [ ] **Step 2: Add obsidian equipment factories**

Append to `EquipmentCatalog`:

```csharp
    public static EquipmentItem CreateObsidianBlade() => new EquipmentItem
    {
        Id = "obsidian_blade",
        DisplayName = "Obsidian Blade",
        Description = "A black glass blade that cuts with abyssal sharpness.",
        Value = 420,
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 44,
        SpeedBonus = 4,
        AssetPath = "res://assets/sprites/items/weapons/obsidian_blade.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianCarapace() => new EquipmentItem
    {
        Id = "obsidian_carapace",
        DisplayName = "Obsidian Carapace",
        Description = "Dark armor layered like a dungeon construct's shell.",
        Value = 480,
        SlotType = EquipmentSlotType.Armor,
        DefenseBonus = 36,
        HealthBonus = 120,
        AssetPath = "res://assets/sprites/items/armor/obsidian_carapace.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianGuard() => new EquipmentItem
    {
        Id = "obsidian_guard",
        DisplayName = "Obsidian Guard",
        Description = "A polished black shield etched with warding lines.",
        Value = 430,
        SlotType = EquipmentSlotType.Shield,
        AttackBonus = 8,
        DefenseBonus = 26,
        AssetPath = "res://assets/sprites/items/shields/obsidian_guard.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianCrown() => new EquipmentItem
    {
        Id = "obsidian_crown",
        DisplayName = "Obsidian Crown",
        Description = "A crown-like helm humming with dungeon pressure.",
        Value = 460,
        SlotType = EquipmentSlotType.Helmet,
        HealthBonus = 240,
        SpeedBonus = 8,
        AssetPath = "res://assets/sprites/items/helmet/obsidian_crown.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianTreads() => new EquipmentItem
    {
        Id = "obsidian_treads",
        DisplayName = "Obsidian Treads",
        Description = "Black plated boots that move silently over stone.",
        Value = 410,
        SlotType = EquipmentSlotType.Shoe,
        DefenseBonus = 8,
        SpeedBonus = 12,
        AssetPath = "res://assets/sprites/items/shoes/obsidian_treads.png",
        Rarity = ItemRarity.Rare
    };
```

- [ ] **Step 3: Add dungeon consumables**

Append to `ConsumableCatalog`:

```csharp
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
```

Also add `AssetPath` properties to the ten existing consumable factories using these paths:

```csharp
AssetPath = "res://assets/sprites/items/consumables/health_potion.png"
AssetPath = "res://assets/sprites/items/consumables/greater_health_potion.png"
AssetPath = "res://assets/sprites/items/consumables/mana_potion.png"
AssetPath = "res://assets/sprites/items/consumables/strength_tonic.png"
AssetPath = "res://assets/sprites/items/consumables/iron_skin.png"
AssetPath = "res://assets/sprites/items/consumables/swiftness_draught.png"
AssetPath = "res://assets/sprites/items/consumables/antidote.png"
AssetPath = "res://assets/sprites/items/consumables/regen_potion.png"
AssetPath = "res://assets/sprites/items/consumables/poison_vial.png"
AssetPath = "res://assets/sprites/items/consumables/flash_powder.png"
```

- [ ] **Step 4: Add dungeon monster parts**

Append to `MonsterPartsCatalog`:

```csharp
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
```

Also add `AssetPath` to existing monster part factories using these paths:

```csharp
AssetPath = "res://assets/sprites/items/monster_parts/goblin_ear.png"
AssetPath = "res://assets/sprites/items/monster_parts/orc_tusk.png"
AssetPath = "res://assets/sprites/items/monster_parts/skeleton_bone.png"
AssetPath = "res://assets/sprites/items/monster_parts/spider_silk.png"
AssetPath = "res://assets/sprites/items/monster_parts/dragon_scale.png"
```

- [ ] **Step 5: Register new items in ItemCatalog**

Add these entries to `_itemRegistry`:

```csharp
        ["steel_longsword"] = EquipmentCatalog.CreateSteelLongsword,
        ["chain_mail"] = EquipmentCatalog.CreateChainMail,
        ["steel_tower_shield"] = EquipmentCatalog.CreateSteelTowerShield,
        ["knight_helm"] = EquipmentCatalog.CreateKnightHelm,
        ["swift_boots"] = EquipmentCatalog.CreateSwiftBoots,
        ["obsidian_blade"] = EquipmentCatalog.CreateObsidianBlade,
        ["obsidian_carapace"] = EquipmentCatalog.CreateObsidianCarapace,
        ["obsidian_guard"] = EquipmentCatalog.CreateObsidianGuard,
        ["obsidian_crown"] = EquipmentCatalog.CreateObsidianCrown,
        ["obsidian_treads"] = EquipmentCatalog.CreateObsidianTreads,
        ["major_health_potion"] = ConsumableCatalog.CreateMajorHealthPotion,
        ["major_mana_potion"] = ConsumableCatalog.CreateMajorManaPotion,
        ["warding_charm"] = ConsumableCatalog.CreateWardingCharm,
        ["smoke_bomb"] = ConsumableCatalog.CreateSmokeBomb,
        ["sentinel_core"] = MonsterPartsCatalog.CreateSentinelCore,
        ["hexed_cloth"] = MonsterPartsCatalog.CreateHexedCloth,
        ["splintered_bone"] = MonsterPartsCatalog.CreateSplinteredBone,
        ["revenant_plate"] = MonsterPartsCatalog.CreateRevenantPlate,
        ["gargoyle_shard"] = MonsterPartsCatalog.CreateGargoyleShard,
        ["abyssal_sigil"] = MonsterPartsCatalog.CreateAbyssalSigil,
```

- [ ] **Step 6: Run item catalog tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DungeonContentCatalogTest|FullyQualifiedName~ItemCatalogTest|FullyQualifiedName~LootDropSystemTest"
```

Expected: item registration, equipment, consumable, and monster-part tests pass. Loot table tests still fail until Task 4 adds loot tables.

- [ ] **Step 7: Commit item catalog work**

```bash
git add scripts/data/items/EquipmentCatalog.cs scripts/data/items/ConsumableCatalog.cs scripts/data/items/MonsterPartsCatalog.cs scripts/data/items/ItemCatalog.cs tests/data/DungeonContentCatalogTest.cs
git commit -m "feat: add dungeon item catalog entries"
```

---

### Task 4: Add Dungeon Loot Tables And Debuff Profiles

**Files:**
- Modify: `scripts/data/LootTableCatalog.cs`
- Modify: `scripts/data/EnemyDebuffProfile.cs`

- [ ] **Step 1: Add dungeon loot table branches**

Add branches to `LootTableCatalog.GetByEnemyType()`:

```csharp
            EnemyTypeId.CryptSentinel   => CreateCryptSentinelTable(),
            EnemyTypeId.GraveHexer      => CreateGraveHexerTable(),
            EnemyTypeId.BoneArcher      => CreateBoneArcherTable(),
            EnemyTypeId.IronRevenant    => CreateIronRevenantTable(),
            EnemyTypeId.CursedGargoyle  => CreateCursedGargoyleTable(),
            EnemyTypeId.AbyssAcolyte    => CreateAbyssAcolyteTable(),
```

- [ ] **Step 2: Add new dungeon loot table factories**

Append to `LootTableCatalog`:

```csharp
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
```

- [ ] **Step 3: Refresh existing dungeon-adjacent loot tables**

Replace the `Entries` blocks in existing methods:

`CreateDarkMageTable()`:

```csharp
        Entries = new()
        {
            new LootEntry { ItemId = "hexed_cloth", Weight = 140, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "skeleton_bone", Weight = 80, MinQuantity = 2, MaxQuantity = 4 },
            new LootEntry { ItemId = "major_mana_potion", Weight = 36, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "warding_charm", Weight = 24, MinQuantity = 1, MaxQuantity = 1 }
        }
```

`CreateDungeonGuardianTable()`:

```csharp
        Entries = new()
        {
            new LootEntry { ItemId = "sentinel_core", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 1, Weight = 0 },
            new LootEntry { ItemId = "revenant_plate", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "steel_longsword", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "chain_mail", Weight = 70, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_guard", Weight = 12, MinQuantity = 1, MaxQuantity = 1 }
        }
```

`CreateDemonLordTable()`:

```csharp
        Entries = new()
        {
            new LootEntry { ItemId = "abyssal_sigil", GuaranteedDrop = true, MinQuantity = 2, MaxQuantity = 3, Weight = 0 },
            new LootEntry { ItemId = "obsidian_blade", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "obsidian_carapace", Weight = 60, MinQuantity = 1, MaxQuantity = 1 },
            new LootEntry { ItemId = "major_health_potion", Weight = 50, MinQuantity = 1, MaxQuantity = 2 },
            new LootEntry { ItemId = "dragon_scale", Weight = 25, MinQuantity = 1, MaxQuantity = 1 }
        }
```

`CreateBossTable()`:

```csharp
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
```

- [ ] **Step 4: Add debuff profiles**

Add entries to `_profiles` in `EnemyDebuffProfile`:

```csharp
        [EnemyTypeId.GraveHexer] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Weaken, 14, 3, 0.32f),
            new EnemyDebuffAbility(StatusEffectType.Blind,   0, 2, 0.20f),
        },
        [EnemyTypeId.BoneArcher] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Slow, 10, 2, 0.28f),
            new EnemyDebuffAbility(StatusEffectType.Blind, 0, 1, 0.16f),
        },
        [EnemyTypeId.CursedGargoyle] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Burn, 10, 3, 0.25f),
        },
        [EnemyTypeId.AbyssAcolyte] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Weaken, 16, 3, 0.34f),
            new EnemyDebuffAbility(StatusEffectType.Stun,    0, 1, 0.16f),
        },
```

- [ ] **Step 5: Run loot/debuff tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DungeonContentCatalogTest|FullyQualifiedName~LootDropSystemTest|FullyQualifiedName~LootTableCatalogTest|FullyQualifiedName~EnemyStatusEffectTest"
```

Expected: all filtered tests pass.

- [ ] **Step 6: Commit loot and debuff work**

```bash
git add scripts/data/LootTableCatalog.cs scripts/data/EnemyDebuffProfile.cs tests/data/DungeonContentCatalogTest.cs
git commit -m "feat: add dungeon loot tables"
```

---

### Task 5: Wire Dungeon Encounter Selection And Map Markers

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/game/GridMap.cs`
- Modify: `tests/game/EncounterTablesTest.cs`

- [ ] **Step 1: Update Game dungeon encounter selection**

In `Game.CreateEnemyByArea()`, replace the dungeon complex block:

```csharp
        if (IsInArea(x, y, 115, 85, 30, 35))
        {
            float rand = GD.Randf();
            if (rand < 0.3f) return Enemy.CreateDungeonGuardian();
            else if (rand < 0.5f) return Enemy.CreateDarkMage();
            else if (rand < 0.7f) return Enemy.CreateDragon();
            else return Enemy.CreateDemonLord();
        }
```

with:

```csharp
        if (IsInArea(x, y, 115, 85, 30, 35))
        {
            string enemyType = EncounterTables.SelectDungeonEnemyType(GD.Randf());
            return EncounterTables.CreateEnemyByType(enemyType) ?? Enemy.CreateDungeonGuardian();
        }
```

- [ ] **Step 2: Update GridMap dungeon marker selection**

In `GridMap.GetEnemyTypeForPosition()`, replace the dungeon block:

```csharp
        if (IsInAreaBounds(x, y, 115, 85, 30, 35))
        {
            return pseudoRand < 0.5f ? "dungeon_guardian" : "dark_mage";
        }
```

with:

```csharp
        if (IsInAreaBounds(x, y, 115, 85, 30, 35))
        {
            return EncounterTables.SelectDungeonEnemyType(pseudoRand);
        }
```

Leave GF and 1F scene-placed enemy spawns unchanged.

- [ ] **Step 3: Add test coverage for boundary behavior**

Append to `tests/game/EncounterTablesTest.cs`:

```csharp
    [TestCase]
    public void SelectDungeonEnemyType_ClampsOutOfRangeRolls()
    {
        AssertThat(EncounterTables.SelectDungeonEnemyType(-1.0f)).IsEqual(EnemyTypeId.DungeonGuardian);
        AssertThat(EncounterTables.SelectDungeonEnemyType(2.0f)).IsEqual(EnemyTypeId.AbyssAcolyte);
    }
```

- [ ] **Step 4: Run encounter tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~EncounterTablesTest|FullyQualifiedName~GameTest|FullyQualifiedName~EnemySpawnTest"
```

Expected: all filtered tests pass.

- [ ] **Step 5: Commit encounter wiring**

```bash
git add scripts/game/Game.cs scripts/game/GridMap.cs scripts/game/EncounterTables.cs tests/game/EncounterTablesTest.cs
git commit -m "feat: wire dungeon encounter table"
```

---

### Task 6: Generate And Verify Dungeon Art Assets

**Files:**
- Create or reuse: `assets/sprites/enemies/crypt_sentinel/`
- Create or reuse: `assets/sprites/enemies/grave_hexer/`
- Create or reuse: `assets/sprites/enemies/bone_archer/`
- Create or reuse: `assets/sprites/enemies/iron_revenant/`
- Create or reuse: `assets/sprites/enemies/cursed_gargoyle/`
- Create or reuse: `assets/sprites/enemies/abyss_acolyte/`
- Create or reuse: item icons listed below under `assets/sprites/items/`

- [ ] **Step 1: Use the asset-generation skill**

Before any generation, load `.codex/skills/manage-asset-generation/SKILL.md` and follow it. Treat filesystem checks as authoritative.

- [ ] **Step 2: Check enemy runtime sheet paths**

Run:

```bash
for type in crypt_sentinel grave_hexer bone_archer iron_revenant cursed_gargoyle abyss_acolyte; do
  test -f "assets/sprites/enemies/$type/sprite_sheet.png" && echo "$type exists" || echo "$type missing"
done
```

Expected before first implementation: each new enemy prints `missing`.

- [ ] **Step 3: Check item icon paths**

Run:

```bash
for path in \
  assets/sprites/items/weapons/steel_longsword.png \
  assets/sprites/items/armor/chain_mail.png \
  assets/sprites/items/shields/steel_tower_shield.png \
  assets/sprites/items/helmet/knight_helm.png \
  assets/sprites/items/shoes/swift_boots.png \
  assets/sprites/items/weapons/obsidian_blade.png \
  assets/sprites/items/armor/obsidian_carapace.png \
  assets/sprites/items/shields/obsidian_guard.png \
  assets/sprites/items/helmet/obsidian_crown.png \
  assets/sprites/items/shoes/obsidian_treads.png \
  assets/sprites/items/consumables/major_health_potion.png \
  assets/sprites/items/consumables/major_mana_potion.png \
  assets/sprites/items/consumables/warding_charm.png \
  assets/sprites/items/consumables/smoke_bomb.png \
  assets/sprites/items/monster_parts/sentinel_core.png \
  assets/sprites/items/monster_parts/hexed_cloth.png \
  assets/sprites/items/monster_parts/splintered_bone.png \
  assets/sprites/items/monster_parts/revenant_plate.png \
  assets/sprites/items/monster_parts/gargoyle_shard.png \
  assets/sprites/items/monster_parts/abyssal_sigil.png
do
  test -f "$path" && echo "$path exists" || echo "$path missing"
done
```

Expected before first implementation: steel orphan files may print `exists`; new obsidian, consumable, and monster-part icons print `missing`.

- [ ] **Step 4: Inspect reference asset dimensions**

Run:

```bash
python3 - <<'PY'
from PIL import Image
for path in [
    "assets/sprites/enemies/goblin/sprite_sheet.png",
    "assets/sprites/items/weapons/iron_sword.png",
    "assets/sprites/items/armor/iron_armor.png",
    "assets/sprites/items/consumables/health_potion.png",
    "assets/sprites/items/monster_parts/dragon_scale.png",
]:
    img = Image.open(path).convert("RGBA")
    print(path, f"{img.width}x{img.height}", img.getchannel("A").getextrema(), img.getpixel((0, 0))[3])
PY
```

Expected: enemy sheet reference is `384x96`; item references are `96x96`; item alpha extrema include a minimum below `255`, and corner alpha is normally `0`.

- [ ] **Step 5: Generate missing enemy frames and merge sheets**

For each missing enemy, generate four transparent frame PNGs using this style anchor:

```text
96x96 anime chibi top-down RPG enemy sprite, facing downward toward camera, bold black outlines, bright cel shading, transparent background, four-frame walking or floating cycle, readable silhouette at 32x32 gameplay scale.
```

Use these per-enemy prompts:

```text
Crypt Sentinel: armored stone-and-steel dungeon guard, heavy rectangular shield, glowing blue eye slit, slow marching stance.
Grave Hexer: hooded undead caster, torn violet cloth, green curse glow, floating hands, ritual charm fragments.
Bone Archer: skeletal archer with cracked bow, fast narrow silhouette, pale bone and rusted leather, arrow drawn.
Iron Revenant: undead knight in battered iron armor, long sword, dark red eye glow, heavy determined stride.
Cursed Gargoyle: squat winged stone gargoyle, black stone cracks, ember glow in cracks, clawed crouch.
Abyss Acolyte: cult caster in black and crimson robes, abyssal sigil hovering, ominous purple glow, floating stride.
```

Save generated frames to:

```text
assets/sprites/enemies/{enemy_type}/frames/frame1.png
assets/sprites/enemies/{enemy_type}/frames/frame2.png
assets/sprites/enemies/{enemy_type}/frames/frame3.png
assets/sprites/enemies/{enemy_type}/frames/frame4.png
```

Run:

```bash
python3 tools/sprite_sheet_merger.py
```

- [ ] **Step 6: Verify enemy sprite sheets**

Run:

```bash
python3 - <<'PY'
from PIL import Image
for enemy_type in ["crypt_sentinel", "grave_hexer", "bone_archer", "iron_revenant", "cursed_gargoyle", "abyss_acolyte"]:
    path = f"assets/sprites/enemies/{enemy_type}/sprite_sheet.png"
    img = Image.open(path)
    print(path, f"{img.width}x{img.height}")
    assert (img.width, img.height) == (384, 96)
PY
```

Expected: each sheet prints `384x96`.

- [ ] **Step 7: Generate or reuse item icons**

For missing item icons, generate source images with this prompt structure:

```text
Create a 96x96 anime-style inventory icon of [item description], transparent background, bold 2px outline, cel shading, soft top-left lighting, centered in frame, no background elements.
```

Use these item descriptions:

```text
steel_longsword: a polished steel longsword with leather grip
chain_mail: a folded chain mail shirt with steel rings
steel_tower_shield: a tall reinforced steel tower shield
knight_helm: an open-faced steel knight helmet with cheek guards
swift_boots: light steel-capped adventurer boots
obsidian_blade: a black glass sword with purple edge glow
obsidian_carapace: black segmented armor plates with violet highlights
obsidian_guard: a polished obsidian shield with etched warding lines
obsidian_crown: crown-like obsidian helmet with abyssal gem
obsidian_treads: black plated boots with violet sole glow
major_health_potion: ornate large crimson potion bottle with gold trim
major_mana_potion: ornate large blue potion bottle with silver trim
warding_charm: small protective charm with blue shield rune
smoke_bomb: round black smoke bomb with short fuse
sentinel_core: stone-and-metal glowing blue construct core
hexed_cloth: torn violet cloth scrap with green runes
splintered_bone: sharp pale bone fragment with cracks
revenant_plate: battered dark armor plate with red glow
gargoyle_shard: jagged black stone shard with ember cracks
abyssal_sigil: small black-and-purple sigil token
```

For each generated icon, place the source file alone in `/tmp/sirius_item_icon_source`, then run:

```bash
python3 tools/resize_item_icons.py --size 96 --source /tmp/sirius_item_icon_source --dest /tmp/sirius_item_icon_out
```

Copy the resized output to the canonical path from the catalog.

- [ ] **Step 8: Verify item icon dimensions and transparency**

Run:

```bash
python3 - <<'PY'
from PIL import Image
paths = [
    "assets/sprites/items/weapons/steel_longsword.png",
    "assets/sprites/items/armor/chain_mail.png",
    "assets/sprites/items/shields/steel_tower_shield.png",
    "assets/sprites/items/helmet/knight_helm.png",
    "assets/sprites/items/shoes/swift_boots.png",
    "assets/sprites/items/weapons/obsidian_blade.png",
    "assets/sprites/items/armor/obsidian_carapace.png",
    "assets/sprites/items/shields/obsidian_guard.png",
    "assets/sprites/items/helmet/obsidian_crown.png",
    "assets/sprites/items/shoes/obsidian_treads.png",
    "assets/sprites/items/consumables/major_health_potion.png",
    "assets/sprites/items/consumables/major_mana_potion.png",
    "assets/sprites/items/consumables/warding_charm.png",
    "assets/sprites/items/consumables/smoke_bomb.png",
    "assets/sprites/items/monster_parts/sentinel_core.png",
    "assets/sprites/items/monster_parts/hexed_cloth.png",
    "assets/sprites/items/monster_parts/splintered_bone.png",
    "assets/sprites/items/monster_parts/revenant_plate.png",
    "assets/sprites/items/monster_parts/gargoyle_shard.png",
    "assets/sprites/items/monster_parts/abyssal_sigil.png",
]
for path in paths:
    img = Image.open(path).convert("RGBA")
    alpha = img.getchannel("A")
    print(path, f"{img.width}x{img.height}", alpha.getextrema(), img.getpixel((0, 0))[3])
    assert (img.width, img.height) == (96, 96)
    assert alpha.getextrema()[0] < 255
PY
```

Expected: every path prints `96x96`, a minimum alpha below `255`, and no assertion fails.

- [ ] **Step 9: Commit asset files**

```bash
git add assets/sprites/enemies assets/sprites/items
git commit -m "feat: add dungeon content art assets"
```

---

### Task 7: Update Asset And Gameplay Docs

**Files:**
- Modify: `docs/enemies/ENEMY_SPRITES.md`
- Modify: `docs/items/ASSET_STATUS.md`
- Modify: `docs/items/ITEM_PROMPT_GUIDE.md`
- Modify: `docs/items/items-guide.md`

- [ ] **Step 1: Update enemy sprite docs**

In `docs/enemies/ENEMY_SPRITES.md`, add rows to the enemy production checklist:

```markdown
| ✅ exists | Crypt Sentinel | `assets/sprites/enemies/crypt_sentinel/sprite_sheet.png` |
| ✅ exists | Grave Hexer | `assets/sprites/enemies/grave_hexer/sprite_sheet.png` |
| ✅ exists | Bone Archer | `assets/sprites/enemies/bone_archer/sprite_sheet.png` |
| ✅ exists | Iron Revenant | `assets/sprites/enemies/iron_revenant/sprite_sheet.png` |
| ✅ exists | Cursed Gargoyle | `assets/sprites/enemies/cursed_gargoyle/sprite_sheet.png` |
| ✅ exists | Abyss Acolyte | `assets/sprites/enemies/abyss_acolyte/sprite_sheet.png` |
```

Add a `Dungeon Expansion Enemies` section with the six per-enemy prompt descriptions from Task 6.

- [ ] **Step 2: Update item asset status docs**

In `docs/items/ASSET_STATUS.md`, add rows for the ten equipment icons, four consumables, and six monster parts from Task 6. Mark each row `✅ exists` only after the filesystem verification passes.

Add this note under item icon loading:

```markdown
Dungeon consumables and monster parts now define `AssetPath` directly in their catalog factories, matching equipment.
```

Update summary totals so equipment, consumables, and monster parts counts match the rows in the document.

- [ ] **Step 3: Update item prompt guide**

In `docs/items/ITEM_PROMPT_GUIDE.md`, add prompt entries for the new equipment, consumables, and monster parts using the exact item descriptions from Task 6.

For steel orphan assets that were reused, write:

```markdown
*asset exists; reused from existing repo asset after filesystem and transparency verification*
```

- [ ] **Step 4: Update gameplay item guide**

In `docs/items/items-guide.md`, add:

```markdown
## Dungeon Consumables

| ID | Display Name | Effect | Value | Rarity |
|----|-------------|--------|-------|--------|
| major_health_potion | Major Health Potion | Restores 300 HP | 150g | Rare |
| major_mana_potion | Major Mana Potion | Restores 75 MP | 130g | Rare |
| warding_charm | Warding Charm | +18 DEF for 4 turns | 120g | Uncommon |
| smoke_bomb | Smoke Bomb | Blinds enemy for 3 turns | 95g | Uncommon |

## Dungeon Equipment

| ID | Slot | Key Bonuses | Rarity |
|----|------|-------------|--------|
| steel_longsword | Weapon | +32 ATK | Uncommon |
| chain_mail | Armor | +26 DEF, +50 HP | Uncommon |
| steel_tower_shield | Shield | +18 DEF, +30 HP | Uncommon |
| knight_helm | Helmet | +8 DEF, +160 HP | Uncommon |
| swift_boots | Shoe | +4 DEF, +8 SPD | Uncommon |
| obsidian_blade | Weapon | +44 ATK, +4 SPD | Rare |
| obsidian_carapace | Armor | +36 DEF, +120 HP | Rare |
| obsidian_guard | Shield | +8 ATK, +26 DEF | Rare |
| obsidian_crown | Helmet | +240 HP, +8 SPD | Rare |
| obsidian_treads | Shoe | +8 DEF, +12 SPD | Rare |
```

Add the six monster parts to the Monster Parts table.

- [ ] **Step 5: Run doc consistency checks**

Run:

```bash
rg -n "crypt_sentinel|grave_hexer|bone_archer|iron_revenant|cursed_gargoyle|abyss_acolyte|obsidian_blade|abyssal_sigil" docs/enemies/ENEMY_SPRITES.md docs/items/ASSET_STATUS.md docs/items/ITEM_PROMPT_GUIDE.md docs/items/items-guide.md
```

Expected: all new IDs appear in the relevant docs.

- [ ] **Step 6: Commit docs**

```bash
git add docs/enemies/ENEMY_SPRITES.md docs/items/ASSET_STATUS.md docs/items/ITEM_PROMPT_GUIDE.md docs/items/items-guide.md
git commit -m "docs: document dungeon content assets"
```

---

### Task 8: Full Verification And Cleanup

**Files:**
- Verify all touched code, tests, docs, and assets.

- [ ] **Step 1: Run focused content tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DungeonContentCatalogTest|FullyQualifiedName~EncounterTablesTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~LootDropSystemTest|FullyQualifiedName~LootTableCatalogTest|FullyQualifiedName~EnemyStatusEffectTest"
```

Expected: PASS.

- [ ] **Step 2: Run project build**

Run:

```bash
dotnet build Sirius.sln
```

Expected: build succeeds with `0 Error(s)`.

- [ ] **Step 3: Run full test suite if local Godot settings are available**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: PASS. If `test.runsettings.local` is not configured, report that full test execution was blocked by local Godot settings and include the focused test/build results.

- [ ] **Step 4: Check git status**

Run:

```bash
git status --short
```

Expected: clean worktree.

- [ ] **Step 5: Final commit only if verification changed files**

If Task 8 produced doc or metadata changes, list them and commit the exact paths. For example, if Godot import metadata appears for the new dungeon assets, run:

```bash
git add assets/sprites/enemies assets/sprites/items docs/enemies/ENEMY_SPRITES.md docs/items/ASSET_STATUS.md docs/items/ITEM_PROMPT_GUIDE.md docs/items/items-guide.md
git commit -m "chore: verify dungeon content expansion"
```

If `git status --short` is clean, do not create an empty commit.
