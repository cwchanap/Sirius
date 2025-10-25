using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
public partial class EnemyTest : Node
{
    [TestCase]
    public void TestEnemyInitialization()
    {
        // Arrange & Act
        var enemy = new Enemy
        {
            Name = "TestMonster",
            Level = 2,
            MaxHealth = 80,
            CurrentHealth = 80,
            Attack = 25,
            Defense = 8,
            Speed = 12,
            ExperienceReward = 50,
            GoldReward = 20
        };

        // Assert
        AssertThat(enemy.Name).IsEqual("TestMonster");
        AssertThat(enemy.Level).IsEqual(2);
        AssertThat(enemy.MaxHealth).IsEqual(80);
        AssertThat(enemy.CurrentHealth).IsEqual(80);
        AssertThat(enemy.Attack).IsEqual(25);
        AssertThat(enemy.Defense).IsEqual(8);
        AssertThat(enemy.Speed).IsEqual(12);
        AssertThat(enemy.ExperienceReward).IsEqual(50);
        AssertThat(enemy.GoldReward).IsEqual(20);
        AssertThat(enemy.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestTakeDamage_ReducesHealth()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        int initialHealth = enemy.CurrentHealth;
        int damageAmount = 20;
        int expectedDamage = Mathf.Max(1, damageAmount - enemy.Defense);

        // Act
        enemy.TakeDamage(damageAmount);

        // Assert
        AssertThat(enemy.CurrentHealth).IsEqual(initialHealth - expectedDamage);
        AssertThat(enemy.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestTakeDamage_MinimumDamageIsOne()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        enemy.Defense = 100; // Very high defense
        int initialHealth = enemy.CurrentHealth;
        int damageAmount = 5;

        // Act
        enemy.TakeDamage(damageAmount);

        // Assert - Should still take 1 damage minimum
        AssertThat(enemy.CurrentHealth).IsEqual(initialHealth - 1);
    }

    [TestCase]
    public void TestTakeDamage_CanKillEnemy()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        int damageAmount = 200; // Massive damage

        // Act
        enemy.TakeDamage(damageAmount);

        // Assert
        AssertThat(enemy.CurrentHealth).IsEqual(0);
        AssertThat(enemy.IsAlive).IsFalse();
    }

    [TestCase]
    public void TestCreateGoblin_HasCorrectStats()
    {
        // Act
        var goblin = Enemy.CreateGoblin();

        // Assert
        AssertThat(goblin.Name).IsEqual("Goblin");
        AssertThat(goblin.Level).IsEqual(1);
        AssertThat(goblin.MaxHealth).IsEqual(50);
        AssertThat(goblin.CurrentHealth).IsEqual(50);
        AssertThat(goblin.Attack).IsEqual(15);
        AssertThat(goblin.Defense).IsEqual(5);
        AssertThat(goblin.Speed).IsEqual(10);
        AssertThat(goblin.ExperienceReward).IsEqual(25);
        AssertThat(goblin.GoldReward).IsEqual(10);
    }

    [TestCase]
    public void TestCreateOrc_HasCorrectStats()
    {
        // Act
        var orc = Enemy.CreateOrc();

        // Assert
        AssertThat(orc.Name).IsEqual("Orc");
        AssertThat(orc.Level).IsEqual(2);
        AssertThat(orc.MaxHealth).IsEqual(80);
        AssertThat(orc.Attack).IsEqual(22);
        AssertThat(orc.ExperienceReward).IsEqual(45);
        AssertThat(orc.GoldReward).IsEqual(20);
    }

    [TestCase]
    public void TestCreateDragon_HasCorrectStats()
    {
        // Act
        var dragon = Enemy.CreateDragon();

        // Assert
        AssertThat(dragon.Name).IsEqual("Dragon");
        AssertThat(dragon.Level).IsEqual(5);
        AssertThat(dragon.MaxHealth).IsEqual(200);
        AssertThat(dragon.Attack).IsEqual(45);
        AssertThat(dragon.Defense).IsEqual(20);
        AssertThat(dragon.ExperienceReward).IsEqual(180);
        AssertThat(dragon.GoldReward).IsEqual(100);
    }

    [TestCase]
    public void TestCreateBoss_HasCorrectStats()
    {
        // Act
        var boss = Enemy.CreateBoss();

        // Assert
        AssertThat(boss.Name).IsEqual("Ancient Dragon King");
        AssertThat(boss.Level).IsEqual(10);
        AssertThat(boss.MaxHealth).IsEqual(500);
        AssertThat(boss.CurrentHealth).IsEqual(500);
        AssertThat(boss.Attack).IsEqual(80);
        AssertThat(boss.Defense).IsEqual(35);
        AssertThat(boss.Speed).IsEqual(18);
        AssertThat(boss.ExperienceReward).IsEqual(800);
        AssertThat(boss.GoldReward).IsEqual(500);
    }

    [TestCase]
    public void TestCreateForestSpirit_HasCorrectStats()
    {
        // Act
        var spirit = Enemy.CreateForestSpirit();

        // Assert
        AssertThat(spirit.Name).IsEqual("Forest Spirit");
        AssertThat(spirit.Level).IsEqual(2);
        AssertThat(spirit.MaxHealth).IsEqual(90);
        AssertThat(spirit.Speed).IsEqual(15);
    }

    [TestCase]
    public void TestCreateDemonLord_HasCorrectStats()
    {
        // Act
        var demon = Enemy.CreateDemonLord();

        // Assert
        AssertThat(demon.Name).IsEqual("Demon Lord");
        AssertThat(demon.Level).IsEqual(8);
        AssertThat(demon.MaxHealth).IsEqual(300);
        AssertThat(demon.Attack).IsEqual(65);
        AssertThat(demon.ExperienceReward).IsEqual(400);
    }

    [TestCase]
    public void TestEnemyProgression_StrongerAtHigherLevels()
    {
        // Arrange
        var goblin = Enemy.CreateGoblin();
        var orc = Enemy.CreateOrc();
        var dragon = Enemy.CreateDragon();
        var boss = Enemy.CreateBoss();

        // Assert - Each enemy should be progressively stronger
        AssertThat(goblin.Level).IsLess(orc.Level);
        AssertThat(orc.Level).IsLess(dragon.Level);
        AssertThat(dragon.Level).IsLess(boss.Level);

        AssertThat(goblin.MaxHealth).IsLess(orc.MaxHealth);
        AssertThat(orc.MaxHealth).IsLess(dragon.MaxHealth);
        AssertThat(dragon.MaxHealth).IsLess(boss.MaxHealth);

        AssertThat(goblin.Attack).IsLess(orc.Attack);
        AssertThat(orc.Attack).IsLess(dragon.Attack);
        AssertThat(dragon.Attack).IsLess(boss.Attack);

        AssertThat(goblin.ExperienceReward).IsLess(orc.ExperienceReward);
        AssertThat(orc.ExperienceReward).IsLess(dragon.ExperienceReward);
        AssertThat(dragon.ExperienceReward).IsLess(boss.ExperienceReward);
    }

    [TestCase]
    public void TestAllEnemyFactories_CreateValidEnemies()
    {
        // Act - Create all enemy types
        var enemies = new[]
        {
            Enemy.CreateGoblin(),
            Enemy.CreateOrc(),
            Enemy.CreateDragon(),
            Enemy.CreateSkeletonWarrior(),
            Enemy.CreateTroll(),
            Enemy.CreateDarkMage(),
            Enemy.CreateDemonLord(),
            Enemy.CreateBoss(),
            Enemy.CreateForestSpirit(),
            Enemy.CreateCaveSpider(),
            Enemy.CreateDesertScorpion(),
            Enemy.CreateSwampWretch(),
            Enemy.CreateMountainWyvern(),
            Enemy.CreateDungeonGuardian()
        };

        // Assert - All enemies should be valid and alive
        foreach (var enemy in enemies)
        {
            AssertThat(enemy).IsNotNull();
            AssertThat(enemy.Name).IsNotEmpty();
            AssertThat(enemy.Level).IsGreater(0);
            AssertThat(enemy.MaxHealth).IsGreater(0);
            AssertThat(enemy.CurrentHealth).IsEqual(enemy.MaxHealth);
            AssertThat(enemy.Attack).IsGreater(0);
            AssertThat(enemy.Defense).IsGreaterEqual(0);
            AssertThat(enemy.Speed).IsGreater(0);
            AssertThat(enemy.ExperienceReward).IsGreater(0);
            AssertThat(enemy.GoldReward).IsGreater(0);
            AssertThat(enemy.IsAlive).IsTrue();
        }
    }
}
