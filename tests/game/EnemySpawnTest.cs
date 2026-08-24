using GdUnit4;
using Godot;
using System;
using System.Reflection;
using static GdUnit4.Assertions;

/// <summary>
/// Tests for EnemySpawn.CreateEnemyInstance() to ensure it correctly propagates
/// the blueprint's SpriteType to the Enemy's EnemyType for proper loot table lookup.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class EnemySpawnTest : Node
{
    private EnemySpawn _spawn;

    private EnemySpawn CreateSpawn()
    {
        _spawn = new EnemySpawn();
        AddChild(_spawn);
        return _spawn;
    }

    [After]
    public void Cleanup()
    {
        if (_spawn != null && IsInstanceValid(_spawn))
        {
            _spawn.Free();
        }

        _spawn = null;
    }

    [TestCase]
    public void Process_ReducedMotionKeepsFrameZero()
    {
        var grid = new GridMap { ReducedMotionEnabled = true };
        var spawn = CreateSpawn();
        try
        {
            SetPrivateField(spawn, "_gridMap", grid);
            SetPrivateField(spawn, "FrameWidth", 96);
            SetPrivateField(spawn, "FrameHeight", 96);
            spawn.Texture = CreateFourFrameTexture();
            spawn.RegionEnabled = true;
            spawn.RegionRect = new Rect2(0, 0, 96, 96);

            spawn._Process(0.2);

            AssertThat(spawn.RegionRect.Position.X).IsEqual(0f);
        }
        finally
        {
            spawn.Free();
            grid.Free();
        }
    }

    [TestCase]
    public void Process_DefaultMotionAdvancesOneFrame()
    {
        var grid = new GridMap { ReducedMotionEnabled = false };
        var spawn = CreateSpawn();
        try
        {
            SetPrivateField(spawn, "_gridMap", grid);
            SetPrivateField(spawn, "FrameWidth", 96);
            SetPrivateField(spawn, "FrameHeight", 96);
            spawn.Texture = CreateFourFrameTexture();
            spawn.RegionEnabled = true;
            spawn.RegionRect = new Rect2(0, 0, 96, 96);

            spawn._Process(0.2);

            AssertThat(spawn.RegionRect.Position.X).IsEqual(96f);
        }
        finally
        {
            spawn.Free();
            grid.Free();
        }
    }

    private static Texture2D CreateFourFrameTexture()
    {
        var image = Image.CreateEmpty(384, 96, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    [TestCase]
    public void CreateEnemyInstance_FromBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange - Create an EnemySpawn with a Dragon blueprint
        var spawn = CreateSpawn();
        var dragonBlueprint = EnemyBlueprint.CreateDragonBlueprint();
        spawn.Blueprint = dragonBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - EnemyType should match the blueprint's SpriteType
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("dragon");
        AssertThat(enemy.Name).IsEqual("Dragon");
        AssertThat(enemy.Level).IsEqual(5);
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromOrcBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange - Create an EnemySpawn with an Orc blueprint
        var spawn = CreateSpawn();
        var orcBlueprint = EnemyBlueprint.CreateOrcBlueprint();
        spawn.Blueprint = orcBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("orc");
        AssertThat(enemy.Name).IsEqual("Orc");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromGoblinBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange
        var spawn = CreateSpawn();
        var goblinBlueprint = EnemyBlueprint.CreateGoblinBlueprint();
        spawn.Blueprint = goblinBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("goblin");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromBossBlueprint_PropagatesSpriteTypeToEnemyType()
    {
        // Arrange
        var spawn = CreateSpawn();
        var bossBlueprint = EnemyBlueprint.CreateBossBlueprint();
        spawn.Blueprint = bossBlueprint;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("boss");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromBlueprint_EnablesCorrectLootTableLookup()
    {
        // Arrange - Create spawn with Orc blueprint
        var spawn = CreateSpawn();
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
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromLegacyEnemyType_UsesFactoryMethod()
    {
        // Arrange - Create spawn without blueprint, using legacy EnemyType
        var spawn = CreateSpawn();
        spawn.EnemyType = "orc";
        spawn.Blueprint = null;

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - Should use Enemy.CreateOrc() which correctly sets EnemyType
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("orc");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_NoBlueprintNoEnemyType_FallsBackToGoblin()
    {
        // Arrange
        var spawn = CreateSpawn();
        spawn.Blueprint = null;
        spawn.EnemyType = "";

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - Ultimate fallback should be goblin
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("goblin");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_UnknownEnemyType_FallsBackToGoblin()
    {
        // Arrange - no blueprint, unrecognized EnemyType string
        var spawn = CreateSpawn();
        spawn.Blueprint = null;
        spawn.EnemyType = "unknown_monster_xyz";

        // Act
        var enemy = spawn.CreateEnemyInstance();

        // Assert - switch default falls back to Goblin
        AssertThat(enemy).IsNotNull();
        AssertThat(enemy.EnemyType).IsEqual("goblin");
        spawn.Free();
    }

    [TestCase]
    public void CreateEnemyInstance_FromCustomBlueprint_PropagatesCustomSpriteType()
    {
        // Arrange - Create a custom blueprint with non-default SpriteType
        var spawn = CreateSpawn();
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
        spawn.Free();
    }
}
