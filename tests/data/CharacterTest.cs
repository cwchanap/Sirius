using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class CharacterTest : Node
{
    [TestCase]
    public void TestCharacterInitialization()
    {
        // Arrange & Act
        var character = new Character
        {
            Name = "TestHero",
            Level = 1,
            MaxHealth = 100,
            CurrentHealth = 100,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            Experience = 0,
            ExperienceToNext = 100,
            Gold = 50
        };

        // Assert
        AssertThat(character.Name).IsEqual("TestHero");
        AssertThat(character.Level).IsEqual(1);
        AssertThat(character.MaxHealth).IsEqual(100);
        AssertThat(character.CurrentHealth).IsEqual(100);
        AssertThat(character.Attack).IsEqual(20);
        AssertThat(character.Defense).IsEqual(10);
        AssertThat(character.Speed).IsEqual(15);
        AssertThat(character.Experience).IsEqual(0);
        AssertThat(character.ExperienceToNext).IsEqual(100);
        AssertThat(character.Gold).IsEqual(50);
        AssertThat(character.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestTakeDamage_ReducesHealth()
    {
        // Arrange
        var character = CreateTestCharacter();
        int initialHealth = character.CurrentHealth;
        int damageAmount = 20;
        int expectedDamage = Mathf.Max(1, damageAmount - character.Defense);

        // Act
        character.TakeDamage(damageAmount);

        // Assert
        AssertThat(character.CurrentHealth).IsEqual(initialHealth - expectedDamage);
        AssertThat(character.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestTakeDamage_MinimumDamageIsOne()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Defense = 100; // Very high defense
        int initialHealth = character.CurrentHealth;
        int damageAmount = 5;

        // Act
        character.TakeDamage(damageAmount);

        // Assert - Should still take 1 damage minimum
        AssertThat(character.CurrentHealth).IsEqual(initialHealth - 1);
    }

    [TestCase]
    public void TestTakeDamage_CanKillCharacter()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.CurrentHealth = 10;
        int damageAmount = 50;

        // Act
        character.TakeDamage(damageAmount);

        // Assert
        AssertThat(character.CurrentHealth).IsEqual(0);
        AssertThat(character.IsAlive).IsFalse();
    }

    [TestCase]
    public void TestHeal_RestoresHealth()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.CurrentHealth = 50;
        int healAmount = 30;

        // Act
        character.Heal(healAmount);

        // Assert
        AssertThat(character.CurrentHealth).IsEqual(80);
    }

    [TestCase]
    public void TestHeal_CannotExceedMaxHealth()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.CurrentHealth = 90;
        int healAmount = 50;

        // Act
        character.Heal(healAmount);

        // Assert
        AssertThat(character.CurrentHealth).IsEqual(character.MaxHealth);
    }

    [TestCase]
    public void TestGainExperience_IncreasesExperience()
    {
        // Arrange
        var character = CreateTestCharacter();
        int initialExp = character.Experience;
        int expGain = 50;

        // Act
        character.GainExperience(expGain);

        // Assert
        AssertThat(character.Experience).IsEqual(initialExp + expGain);
        AssertThat(character.Level).IsEqual(1); // Should not level up yet
    }

    [TestCase]
    public void TestGainExperience_TriggersLevelUp()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Experience = 90;
        character.ExperienceToNext = 100;
        int initialLevel = character.Level;
        int initialMaxHealth = character.MaxHealth;
        int initialAttack = character.Attack;
        int initialDefense = character.Defense;
        int initialSpeed = character.Speed;

        // Act
        character.GainExperience(20); // Should trigger level up

        // Assert
        AssertThat(character.Level).IsEqual(initialLevel + 1);
        AssertThat(character.MaxHealth).IsGreater(initialMaxHealth);
        AssertThat(character.Attack).IsGreater(initialAttack);
        AssertThat(character.Defense).IsGreater(initialDefense);
        AssertThat(character.Speed).IsGreater(initialSpeed);
        AssertThat(character.CurrentHealth).IsEqual(character.MaxHealth); // Full heal on level up
        AssertThat(character.Experience).IsEqual(10); // 90 + 20 - 100 = 10 overflow
    }

    [TestCase]
    public void TestLevelUp_ExperienceRequirementIncreases()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Level = 1;
        character.Experience = 100;
        character.ExperienceToNext = 100;
        int initialExpToNext = character.ExperienceToNext;

        // Act
        character.GainExperience(1); // Trigger level up

        // Assert
        AssertThat(character.Level).IsEqual(2);
        // Level 2 requirement: 100 * 2 + 10 * (2 * 2) = 200 + 40 = 240
        AssertThat(character.ExperienceToNext).IsEqual(240);
        AssertThat(character.ExperienceToNext).IsGreater(initialExpToNext);
    }

    [TestCase]
    public void TestGainGold_IncreasesGold()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Gold = 100;
        int goldGain = 50;

        // Act
        character.GainGold(goldGain);

        // Assert
        AssertThat(character.Gold).IsEqual(150);
    }

    [TestCase]
    public void TestInventory_AddAndCheckItem()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = new GeneralItem
        {
            Id = "test_item",
            DisplayName = "Test Item",
            MaxStackOverride = 99
        };

        // Act
        bool added = character.TryAddItem(item, 5, out int addedQuantity);

        // Assert
        AssertThat(added).IsTrue();
        AssertThat(addedQuantity).IsEqual(5);
        AssertThat(character.HasItem("test_item")).IsTrue();
        AssertThat(character.GetItemQuantity("test_item")).IsEqual(5);
    }

    [TestCase]
    public void TestInventory_RemoveItem()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = new GeneralItem
        {
            Id = "test_item",
            DisplayName = "Test Item",
            MaxStackOverride = 99
        };
        character.TryAddItem(item, 10, out _);

        // Act
        bool removed = character.TryRemoveItem("test_item", 3);

        // Assert
        AssertThat(removed).IsTrue();
        AssertThat(character.GetItemQuantity("test_item")).IsEqual(7);
    }

    [TestCase]
    public void TestEquipment_BoostsEffectiveStats()
    {
        // Arrange
        var character = CreateTestCharacter();
        int baseAttack = character.Attack;
        int baseDefense = character.Defense;
        
        var weapon = new EquipmentItem
        {
            Id = "test_sword",
            DisplayName = "Test Sword",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 10,
            DefenseBonus = 2
        };

        // Act
        character.TryEquip(weapon, out _);

        // Assert
        AssertThat(character.GetEffectiveAttack()).IsEqual(baseAttack + 10);
        AssertThat(character.GetEffectiveDefense()).IsEqual(baseDefense + 2);
    }

    [TestCase]
    public void TestEquipment_UnequipReturnsItem()
    {
        // Arrange
        var character = CreateTestCharacter();
        var weapon = new EquipmentItem
        {
            Id = "test_sword",
            DisplayName = "Test Sword",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 10
        };
        character.TryEquip(weapon, out _);

        // Act
        var unequipped = character.Unequip(EquipmentSlotType.Weapon);

        // Assert
        AssertThat(unequipped).IsNotNull();
        AssertThat(unequipped.Id).IsEqual("test_sword");
        AssertThat(character.GetEffectiveAttack()).IsEqual(character.Attack); // Back to base stats
    }

    [TestCase]
    public void TestMultipleLevelUps_CanOccurAtOnce()
    {
        // Arrange
        var character = CreateTestCharacter();
        character.Level = 1;
        character.Experience = 0;
        character.ExperienceToNext = 100;
        int initialLevel = character.Level;

        // Act - Give enough exp to level up multiple times
        character.GainExperience(500);

        // Assert - Should level up at least twice
        AssertThat(character.Level).IsGreater(initialLevel + 1);
    }

    // Helper method to create a standard test character
    private Character CreateTestCharacter()
    {
        return new Character
        {
            Name = "TestHero",
            Level = 1,
            MaxHealth = 100,
            CurrentHealth = 100,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            Experience = 0,
            ExperienceToNext = 100,
            Gold = 0
        };
    }
}
