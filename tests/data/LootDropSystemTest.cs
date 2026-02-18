using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using System;
using System.Linq;

[TestSuite]
public partial class LootDropSystemTest : Node
{
    [TestCase]
    public void ItemRarity_ExistsOnItem()
    {
        var item = new GeneralItem { Id = "test", Rarity = ItemRarity.Rare };
        AssertThat((int)item.Rarity).IsEqual((int)ItemRarity.Rare);
    }

    [TestCase]
    public void ItemRarity_DefaultIsCommon()
    {
        var item = new GeneralItem { Id = "test" };
        AssertThat((int)item.Rarity).IsEqual((int)ItemRarity.Common);
    }

    [TestCase]
    public void Enemy_HasEnemyType()
    {
        var goblin = Enemy.CreateGoblin();
        AssertThat(goblin.EnemyType).IsEqual("goblin");

        var dragon = Enemy.CreateDragon();
        AssertThat(dragon.EnemyType).IsEqual("dragon");

        var boss = Enemy.CreateBoss();
        AssertThat(boss.EnemyType).IsEqual("boss");
    }

    [TestCase]
    public void Enemy_AllFactoryMethodsSetEnemyType()
    {
        AssertThat(Enemy.CreateGoblin().EnemyType).IsEqual("goblin");
        AssertThat(Enemy.CreateOrc().EnemyType).IsEqual("orc");
        AssertThat(Enemy.CreateDragon().EnemyType).IsEqual("dragon");
        AssertThat(Enemy.CreateSkeletonWarrior().EnemyType).IsEqual("skeleton_warrior");
        AssertThat(Enemy.CreateTroll().EnemyType).IsEqual("troll");
        AssertThat(Enemy.CreateDarkMage().EnemyType).IsEqual("dark_mage");
        AssertThat(Enemy.CreateDemonLord().EnemyType).IsEqual("demon_lord");
        AssertThat(Enemy.CreateBoss().EnemyType).IsEqual("boss");
        AssertThat(Enemy.CreateForestSpirit().EnemyType).IsEqual("forest_spirit");
        AssertThat(Enemy.CreateCaveSpider().EnemyType).IsEqual("cave_spider");
        AssertThat(Enemy.CreateDesertScorpion().EnemyType).IsEqual("desert_scorpion");
        AssertThat(Enemy.CreateSwampWretch().EnemyType).IsEqual("swamp_wretch");
        AssertThat(Enemy.CreateMountainWyvern().EnemyType).IsEqual("mountain_wyvern");
        AssertThat(Enemy.CreateDungeonGuardian().EnemyType).IsEqual("dungeon_guardian");
    }

    [TestCase]
    public void ItemCatalog_RegistersMonsterParts()
    {
        AssertThat(ItemCatalog.ItemExists("goblin_ear")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("orc_tusk")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("skeleton_bone")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("spider_silk")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("dragon_scale")).IsTrue();
    }

    [TestCase]
    public void ItemCatalog_RegistersIronEquipment()
    {
        AssertThat(ItemCatalog.ItemExists("iron_sword")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("iron_armor")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("iron_shield")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("iron_helmet")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("iron_boots")).IsTrue();
    }

    [TestCase]
    public void ItemCatalog_CreatesCorrectItemTypes()
    {
        var sword = ItemCatalog.CreateItemById("iron_sword");
        AssertThat(sword).IsNotNull();
        AssertThat(sword is EquipmentItem).IsTrue();
        AssertThat(((EquipmentItem)sword!).SlotType).IsEqual(EquipmentSlotType.Weapon);

        var ear = ItemCatalog.CreateItemById("goblin_ear");
        AssertThat(ear).IsNotNull();
        AssertThat(ear is GeneralItem).IsTrue();
    }

    [TestCase]
    public void LootTableCatalog_ReturnsTablesForAllEnemyTypes()
    {
        string[] types = {
            "goblin", "orc", "skeleton_warrior", "troll", "dragon",
            "forest_spirit", "cave_spider", "desert_scorpion", "swamp_wretch",
            "mountain_wyvern", "dark_mage", "dungeon_guardian", "demon_lord", "boss"
        };

        foreach (var type in types)
        {
            var table = LootTableCatalog.GetByEnemyType(type);
            AssertThat(table).IsNotNull();
            AssertThat(table!.Entries.Count).IsGreater(0);
        }
    }

    [TestCase]
    public void LootTableCatalog_ReturnsNullForUnknownType()
    {
        AssertThat(LootTableCatalog.GetByEnemyType("unknown")).IsNull();
        AssertThat(LootTableCatalog.GetByEnemyType(null)).IsNull();
    }

    [TestCase]
    public void LootManager_RollLoot_WithNullTableReturnsEmpty()
    {
        var result = LootManager.RollLoot(null, new Random(42));
        AssertThat(result.HasDrops).IsFalse();
    }

    [TestCase]
    public void LootResult_Empty_IsImmutable()
    {
        var item = ItemCatalog.CreateItemById("goblin_ear");
        AssertThat(item).IsNotNull();

        bool threw = false;
        try
        {
            LootResult.Empty.Add(item!, 1);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        AssertThat(threw).IsTrue();
    }

    [TestCase]
    public void LootManager_RollLoot_RespectsDropChanceZero()
    {
        var table = new LootTable
        {
            DropChance = 0.0f,
            MaxDrops = 3,
            Entries = new() { new LootEntry { ItemId = "goblin_ear", Weight = 100 } }
        };
        var result = LootManager.RollLoot(table, new Random(42));
        AssertThat(result.HasDrops).IsFalse();
    }

    [TestCase]
    public void LootManager_RollLoot_GuaranteedDropsAlwaysIncluded()
    {
        var table = new LootTable
        {
            DropChance = 1.0f,
            MaxDrops = 5,
            Entries = new()
            {
                new LootEntry { ItemId = "dragon_scale", GuaranteedDrop = true, MinQuantity = 1, MaxQuantity = 1, Weight = 0 }
            }
        };

        var result = LootManager.RollLoot(table, new Random(42));
        AssertThat(result.HasDrops).IsTrue();
        AssertThat(result.DroppedItems[0].Item.Id).IsEqual("dragon_scale");
    }

    [TestCase]
    public void LootManager_RollLoot_RespectsMaxDrops()
    {
        var table = new LootTable
        {
            DropChance = 1.0f,
            MaxDrops = 1,
            Entries = new()
            {
                new LootEntry { ItemId = "goblin_ear", Weight = 100 },
                new LootEntry { ItemId = "orc_tusk", Weight = 100 }
            }
        };

        var result = LootManager.RollLoot(table, new Random(42));
        AssertThat(result.DroppedItems.Count).IsLessEqual(1);
    }

    [TestCase]
    public void LootManager_RollLoot_GoblinDropsResolve()
    {
        var table = LootTableCatalog.GoblinDrops();
        // Force 100% drop for testing
        table.DropChance = 1.0f;

        var result = LootManager.RollLoot(table, new Random(42));
        AssertThat(result.HasDrops).IsTrue();
        // All items should resolve to real items (not null/skipped)
        foreach (var drop in result.DroppedItems)
        {
            AssertThat(drop.Item).IsNotNull();
            AssertThat(drop.Quantity).IsGreater(0);
        }
    }

    [TestCase]
    public void LootManager_AwardLootToCharacter_AddsToInventory()
    {
        var player = new Character { Name = "Test" };
        var item = ItemCatalog.CreateItemById("goblin_ear");
        AssertThat(item).IsNotNull();
        var result = new LootResult();
        result.Add(item!, 3);

        LootManager.AwardLootToCharacter(result, player);

        AssertThat(player.HasItem("goblin_ear")).IsTrue();
        AssertThat(player.GetItemQuantity("goblin_ear")).IsEqual(3);
    }

    [TestCase]
    public void IronEquipment_HasCorrectStats()
    {
        var swordItem = ItemCatalog.CreateItemById("iron_sword");
        AssertThat(swordItem).IsNotNull();
        var sword = (EquipmentItem)swordItem!;
        AssertThat(sword.AttackBonus).IsEqual(20);
        AssertThat((int)sword.Rarity).IsEqual((int)ItemRarity.Uncommon);

        var armorItem = ItemCatalog.CreateItemById("iron_armor");
        AssertThat(armorItem).IsNotNull();
        var armor = (EquipmentItem)armorItem!;
        AssertThat(armor.DefenseBonus).IsEqual(16);
        AssertThat(armor.SlotType).IsEqual(EquipmentSlotType.Armor);
    }

    [TestCase]
    public void DragonScale_HasRareRarity()
    {
        var scale = ItemCatalog.CreateItemById("dragon_scale");
        AssertThat(scale).IsNotNull();
        AssertThat((int)scale!.Rarity).IsEqual((int)ItemRarity.Rare);
    }

    [TestCase]
    public void LootManager_RollLoot_NullRng_ThrowsArgumentNullException()
    {
        var table = LootTableCatalog.GoblinDrops();
        bool threw = false;
        try
        {
            LootManager.RollLoot(table, null!);
        }
        catch (ArgumentNullException)
        {
            threw = true;
        }
        AssertThat(threw).IsTrue();
    }

    [TestCase]
    public void LootManager_AwardLootToCharacter_DoesNotThrowWhenInventoryFull()
    {
        // EquipmentItems cannot stack (CanStack=false), so adding a second iron_sword
        // forces added=0 and exercises the overflow/full-inventory path.
        // RecoveryChest.Instance is null in unit test context.
        var player = new Character { Name = "TestHero" };
        var sword1 = ItemCatalog.CreateItemById("iron_sword")!;
        player.TryAddItem(sword1, 1, out _);

        var sword2 = ItemCatalog.CreateItemById("iron_sword")!;
        var result = new LootResult();
        result.Add(sword2, 1);

        bool threw = false;
        try
        {
            LootManager.AwardLootToCharacter(result, player);
        }
        catch
        {
            threw = true;
        }

        AssertThat(threw).IsFalse();
        // Second sword not awarded (cannot stack), count stays at 1
        AssertThat(player.GetItemQuantity("iron_sword")).IsEqual(1);
    }

    [TestCase]
    public void LootTable_DropChance_ClampsAboveOne()
    {
        var table = new LootTable { DropChance = 1.5f };
        AssertThat(table.DropChance).IsEqual(1.0f);
    }

    [TestCase]
    public void LootTable_DropChance_ClampsBelowZero()
    {
        var table = new LootTable { DropChance = -0.5f };
        AssertThat(table.DropChance).IsEqual(0.0f);
    }

    [TestCase]
    public void LootEntry_ValidateAndNormalize_SwapsInvertedRange()
    {
        var entry = new LootEntry { ItemId = "goblin_ear", MinQuantity = 5, MaxQuantity = 2 };
        entry.ValidateAndNormalizeQuantityRange();
        AssertThat(entry.MinQuantity).IsEqual(2);
        AssertThat(entry.MaxQuantity).IsEqual(5);
    }

    [TestCase]
    public void LootEntry_ValidateAndNormalize_LeavesValidRangeAlone()
    {
        var entry = new LootEntry { ItemId = "goblin_ear", MinQuantity = 2, MaxQuantity = 5 };
        entry.ValidateAndNormalizeQuantityRange();
        AssertThat(entry.MinQuantity).IsEqual(2);
        AssertThat(entry.MaxQuantity).IsEqual(5);
    }

    [TestCase]
    public void LootManager_RollLoot_DoesNotMutateLootEntries()
    {
        // If ResolveQuantity mutated entries, rolling would swap MinQuantity/MaxQuantity in place.
        var entry = new LootEntry
        {
            ItemId = "goblin_ear",
            GuaranteedDrop = true,
            MinQuantity = 3,
            MaxQuantity = 1, // inverted range — would trigger swap in old code
            Weight = 0
        };
        var table = new LootTable
        {
            DropChance = 1.0f,
            MaxDrops = 5,
            Entries = new() { entry }
        };

        int minBefore = entry.MinQuantity;
        int maxBefore = entry.MaxQuantity;

        LootManager.RollLoot(table, new Random(42));

        // Entry must not have been mutated by rolling
        AssertThat(entry.MinQuantity).IsEqual(minBefore);
        AssertThat(entry.MaxQuantity).IsEqual(maxBefore);
    }

    [TestCase]
    public void LootResult_Empty_DroppedItems_IsReadOnly()
    {
        // DroppedItems must be IReadOnlyList backed by ReadOnlyCollection, not castable to List
        var list = LootResult.Empty.DroppedItems as System.Collections.Generic.List<LootResultEntry>;
        AssertThat(list).IsNull();
    }

    [TestCase]
    public void LootResult_Add_IgnoresNullItem()
    {
        var result = new LootResult();
        result.Add(null!, 1);
        AssertThat(result.HasDrops).IsFalse();
    }

    [TestCase]
    public void LootResult_Add_IgnoresZeroQuantity()
    {
        var result = new LootResult();
        var item = ItemCatalog.CreateItemById("goblin_ear")!;
        result.Add(item, 0);
        AssertThat(result.HasDrops).IsFalse();
    }

    [TestCase]
    public void LootResult_Add_IgnoresNegativeQuantity()
    {
        var result = new LootResult();
        var item = ItemCatalog.CreateItemById("goblin_ear")!;
        result.Add(item, -5);
        AssertThat(result.HasDrops).IsFalse();
    }
}
