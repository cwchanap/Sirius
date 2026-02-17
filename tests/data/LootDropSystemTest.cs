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
        var item = ItemCatalog.CreateItemById("goblin_ear")!;
        var result = new LootResult();
        result.Add(item, 3);

        LootManager.AwardLootToCharacter(result, player);

        AssertThat(player.HasItem("goblin_ear")).IsTrue();
        AssertThat(player.GetItemQuantity("goblin_ear")).IsEqual(3);
    }

    [TestCase]
    public void IronEquipment_HasCorrectStats()
    {
        var sword = (EquipmentItem)ItemCatalog.CreateItemById("iron_sword")!;
        AssertThat(sword.AttackBonus).IsEqual(20);
        AssertThat((int)sword.Rarity).IsEqual((int)ItemRarity.Uncommon);

        var armor = (EquipmentItem)ItemCatalog.CreateItemById("iron_armor")!;
        AssertThat(armor.DefenseBonus).IsEqual(16);
        AssertThat(armor.SlotType).IsEqual(EquipmentSlotType.Armor);
    }

    [TestCase]
    public void DragonScale_HasRareRarity()
    {
        var scale = ItemCatalog.CreateItemById("dragon_scale")!;
        AssertThat((int)scale.Rarity).IsEqual((int)ItemRarity.Rare);
    }
}
