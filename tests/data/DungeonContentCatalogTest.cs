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
