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
        int actualDamage = character.TakeDamage(damageAmount);

        // Assert
        AssertThat(character.CurrentHealth).IsEqual(initialHealth - expectedDamage);
        AssertThat(actualDamage).IsEqual(expectedDamage);
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
        int actualDamage = character.TakeDamage(damageAmount);

        // Assert - Should still take 1 damage minimum
        AssertThat(character.CurrentHealth).IsEqual(initialHealth - 1);
        AssertThat(actualDamage).IsEqual(1);
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

    [TestCase]
    public void GetEffectiveAttack_Weakened25Percent_ReducesAttack()
    {
        var character = CreateTestCharacter(); // Attack = 20
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Weaken, 25, 3));
        // flat = 20, multiplier = 1 - 25/100 = 0.75, effective = max(1, (int)(20 * 0.75)) = 15
        AssertThat(character.GetEffectiveAttack()).IsEqual(15);
    }

    [TestCase]
    public void GetEffectiveSpeed_Slowed50Percent_HalvesSpeed()
    {
        var character = CreateTestCharacter(); // Speed = 15
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Slow, 50, 3));
        // flat = 15, multiplier = 1 - 50/100 = 0.5, effective = max(1, (int)(15 * 0.5)) = 7
        AssertThat(character.GetEffectiveSpeed()).IsEqual(7);
    }

    [TestCase]
    public void GetEffectiveSpeed_UsesEffectiveSpeedWithStatusEffects()
    {
        var player = CreateTestCharacter();
        player.Speed = 20;
        // Apply Slow 50% — effective speed becomes max(1, (int)(20 * 0.5)) = 10
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Slow, 50, 3));
        AssertThat(player.GetEffectiveSpeed()).IsEqual(10);
    }

    // ---- TrySpendGold -----------------------------------------------------

    [TestCase]
    public void TrySpendGold_SufficientGold_DeductsAndReturnsTrue()
    {
        var character = CreateTestCharacter();
        character.Gold = 100;

        bool result = character.TrySpendGold(60);

        AssertThat(result).IsTrue();
        AssertThat(character.Gold).IsEqual(40);
    }

    [TestCase]
    public void TrySpendGold_InsufficientGold_ReturnsFalseGoldUnchanged()
    {
        var character = CreateTestCharacter();
        character.Gold = 30;

        bool result = character.TrySpendGold(50);

        AssertThat(result).IsFalse();
        AssertThat(character.Gold).IsEqual(30);
    }

    [TestCase]
    public void TrySpendGold_ExactAmount_ReducesToZero()
    {
        var character = CreateTestCharacter();
        character.Gold = 50;

        bool result = character.TrySpendGold(50);

        AssertThat(result).IsTrue();
        AssertThat(character.Gold).IsEqual(0);
    }

    [TestCase]
    public void TrySpendGold_ZeroAmount_AlwaysSucceeds()
    {
        var character = CreateTestCharacter();
        character.Gold = 0;

        bool result = character.TrySpendGold(0);

        AssertThat(result).IsTrue();
        AssertThat(character.Gold).IsEqual(0);
    }

    [TestCase]
    public void TrySpendGold_NegativeAmount_Throws()
    {
        var character = CreateTestCharacter();
        character.Gold = 100;

        AssertThrown(() => character.TrySpendGold(-1))
            .IsInstanceOf<System.ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void GainGold_ThenSpend_RoundTrip()
    {
        var character = CreateTestCharacter();
        character.Gold = 0;
        character.GainGold(200);
        character.TrySpendGold(75);

        AssertThat(character.Gold).IsEqual(125);
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
