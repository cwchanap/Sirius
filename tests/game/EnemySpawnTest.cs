using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

/// <summary>
/// Tests for EnemySpawn.CreateEnemyInstance() to ensure it correctly propagates
/// the blueprint's SpriteType to the Enemy's EnemyType for proper loot table lookup.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class EnemySpawnTest : Node
{
    [TestCase]
    public void CreateEnemyInstance_FromBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange - Create an EnemySpawn with a Dragon blueprint
        var spawn = new EnemySpawn();
        var dragonBlueprint = EnemyBlueprint.CreateDragonBlueprint();
        spawn.Blueprint = dragonBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - EnemyType should match the blueprint's SpriteType
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("dragon");
        AssertThat(enemy.Name).IsEqual("Dragon");
        AssertThat(enemy.Level).IsEqual(5);
    }

    [TestCase]
    public void CreateEnemyInstance_FromOrcBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange - Create an EnemySpawn with an Orc blueprint
        var spawn = new EnemySpawn();
        var orcBlueprint = EnemyBlueprint.CreateOrcBlueprint();
        spawn.Blueprint = orcBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("orc");
        AssertThat(enemy.Name).IsEqual("Orc");
    }

    [TestCase]
    public void CreateEnemyInstance_FromGoblinBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange
        var spawn = new EnemySpawn();
        var goblinBlueprint = EnemyBlueprint.CreateGoblinBlueprint();
        spawn.Blueprint = goblinBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("goblin");
    }

    [TestCase]
    public void CreateEnemyInstance_FromBossBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange
        var spawn = new EnemySpawn();
        var bossBlueprint = EnemyBlueprint.CreateBossBlueprint();
        spawn.Blueprint = bossBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("boss");
    }

    [TestCase]
    public void CreateEnemyInstance_FromBlueprint_EnablesCorrectLootTableLookup()
    {
        // Arrange - Create spawn with Orc blueprint
        var spawn = new EnemySpawn();
        var orcBlueprint = EnemyBlueprint.CreateOrcBlueprint();
        spawn.Blueprint = orcBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();
        var lootTable = LootTableCatalog.GetByEnemyType(enemy.EnemyType);

        // Assert - Should find the Orc loot table, not Goblin
        AssertThat(lootTable).IsNotNull();
        AssertThat(lootTable!.Entries.Count).IsGreater(0);
        // Orc drops include orc_tusk
        var hasOrcTusk = lootTable.Entries.Exists(e => e.ItemId == "orc_tusk");
        AssertThat(hasOrcTusk).IsTrue();
    }

    [TestCase]
    public void CreateEnemyInstance_FromLegacyEnemyType_UsesFactoryMethod()
    {
        // Arrange - Create spawn without blueprint, using legacy EnemyType
        var spawn = new EnemySpawn();
        spawn.EnemyType = "orc";
        spawn.Blueprint = null;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - Should use Enemy.CreateOrc() which correctly sets EnemyType
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("orc");
    }

    [TestCase]
    public void CreateEnemyInstance_NoBlueprintNoEnemyType_FallsBackToGoblin()
    {
        // Arrange
        var spawn = new EnemySpawn();
        spawn.Blueprint = null;
        spawn.EnemyType = "";

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - Ultimate fallback should be goblin
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("goblin");
    }

    [TestCase]
    public void CreateEnemyInstance_FromCustomBlueprint_PropagatesCustomSpriteType()
    {
        // Arrange - Create a custom blueprint with non-default SpriteType
        var spawn = new EnemySpawn();
        var customBlueprint = new EnemyBlueprint
        {
            EnemyName = "Custom Cave Spider",
            SpriteType = "cave_spider",
            Level = 10,
            MaxHealth = 500,
            Attack = 60,
            Defense = 30,
            Speed = 25,
            ExperienceReward = 300,
            GoldReward = 150
        };
        spawn.Blueprint = customBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("cave_spider");
        AssertThat(enemy.Name).IsEqual("Custom Cave Spider");
        AssertThat(enemy.Level).IsEqual(10);

        // And should find cave_spider loot table
        var lootTable = LootTableCatalog.GetByEnemyType(enemy.EnemyType);
        AssertThat(lootTable).IsNotNull();
        var hasSpiderSilk = lootTable!.Entries.Exists(e => e.ItemId == "spider_silk");
        AssertThat(hasSpiderSilk).IsTrue();
    }
}
