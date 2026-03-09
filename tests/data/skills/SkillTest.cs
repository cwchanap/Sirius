using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SkillTest : Node
{
    // ---- Helpers -----------------------------------------------------------

    private static Character CreateCharacter(int hp = 100, int attack = 20, int mana = 50) => new Character
    {
        Name = "TestHero",
        Level = 1,
        MaxHealth = hp,
        CurrentHealth = hp,
        Attack = attack,
        Defense = 10,
        Speed = 15,
        Experience = 0,
        ExperienceToNext = 110,
        Gold = 0,
        MaxMana = mana,
        CurrentMana = mana,
    };

    private static Enemy CreateEnemy(int hp = 80)
    {
        var e = Enemy.CreateGoblin();
        e.MaxHealth = hp;
        e.CurrentHealth = hp;
        return e;
    }

    // ---- Mana operations ---------------------------------------------------

    [TestCase]
    public void TryUseMana_DeductsManaWhenSufficient()
    {
        var c = CreateCharacter(mana: 50);
        bool result = c.TryUseMana(10);
        AssertThat(result).IsTrue();
        AssertThat(c.CurrentMana).IsEqual(40);
    }

    [TestCase]
    public void TryUseMana_ReturnsFalseWhenInsufficient()
    {
        var c = CreateCharacter(mana: 5);
        bool result = c.TryUseMana(10);
        AssertThat(result).IsFalse();
        AssertThat(c.CurrentMana).IsEqual(5); // unchanged
    }

    [TestCase]
    public void TryUseMana_ThrowsForNegativeAmount()
    {
        var c = CreateCharacter(mana: 50);
        AssertThrown(() => c.TryUseMana(-1)).IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThat(c.CurrentMana).IsEqual(50);
    }

    [TestCase]
    public void RestoreMana_ClampsToMaxMana()
    {
        var c = CreateCharacter(mana: 50);
        c.CurrentMana = 30;
        c.RestoreMana(100);
        AssertThat(c.CurrentMana).IsEqual(50); // capped at max
    }

    [TestCase]
    public void RestoreMana_ThrowsForNegativeAmount()
    {
        var c = CreateCharacter(mana: 50);
        AssertThrown(() => c.RestoreMana(-1)).IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThat(c.CurrentMana).IsEqual(50);
    }

    // ---- Skill learning and loadout ----------------------------------------

    [TestCase]
    public void LearnSkill_AddsToKnownSkills()
    {
        var c = CreateCharacter();
        c.LearnSkill("power_strike");
        AssertThat(c.KnownSkillIds.Contains("power_strike")).IsTrue();
    }

    [TestCase]
    public void LearnSkill_NoDuplicates()
    {
        var c = CreateCharacter();
        c.LearnSkill("power_strike");
        c.LearnSkill("power_strike");
        AssertThat(c.KnownSkillIds.Count).IsEqual(1);
    }

    [TestCase]
    public void EquipActiveSkill_SucceedsWhenKnown()
    {
        var c = CreateCharacter();
        c.LearnSkill("power_strike");
        bool result = c.EquipActiveSkill("power_strike");
        AssertThat(result).IsTrue();
        AssertThat(c.ActiveSkillId).IsEqual("power_strike");
    }

    [TestCase]
    public void EquipActiveSkill_FailsWhenNotKnown()
    {
        var c = CreateCharacter();
        bool result = c.EquipActiveSkill("power_strike");
        AssertThat(result).IsFalse();
        AssertThat(c.ActiveSkillId).IsNull();
    }

    [TestCase]
    public void EquipPassiveSkill_SucceedsWhenKnown()
    {
        var c = CreateCharacter();
        c.LearnSkill("heal");
        bool result = c.EquipPassiveSkill("heal", 0);
        AssertThat(result).IsTrue();
        AssertThat(c.PassiveSkillIds[0]).IsEqual("heal");
    }

    [TestCase]
    public void EquipPassiveSkill_FailsForInvalidSlot()
    {
        var c = CreateCharacter();
        c.LearnSkill("heal");
        bool result = c.EquipPassiveSkill("heal", 5); // slot out of range
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void EquipActiveSkill_FailsForPassiveTypedSkill()
    {
        var c = CreateCharacter();
        c.LearnSkill("heal"); // Heal is Passive type
        bool result = c.EquipActiveSkill("heal");
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void EquipPassiveSkill_FailsForActiveTypedSkill()
    {
        var c = CreateCharacter();
        c.LearnSkill("power_strike"); // Power Strike is Active type
        bool result = c.EquipPassiveSkill("power_strike", 0);
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void SkillCatalog_GrantSkillsUpToLevel_LearnsEligibleSkills()
    {
        var c = CreateCharacter();
        SkillCatalog.GrantSkillsUpToLevel(c, 1);
        // Power Strike unlocks at level 1
        AssertThat(c.KnownSkillIds.Contains("power_strike")).IsTrue();
        // Heal unlocks at level 2 — should not be granted
        AssertThat(c.KnownSkillIds.Contains("heal")).IsFalse();
    }

    [TestCase]
    public void SkillCatalog_GrantSkillsUpToLevel_NoDuplicatesOnRepeatCall()
    {
        var c = CreateCharacter();
        SkillCatalog.GrantSkillsUpToLevel(c, 1);
        SkillCatalog.GrantSkillsUpToLevel(c, 1);
        int count = 0;
        foreach (var id in c.KnownSkillIds)
            if (id == "power_strike") count++;
        AssertThat(count).IsEqual(1);
    }

    [TestCase]
    public void SkillCatalog_GrantSkillsUpToLevel_AutoEquipsPassiveSkill()
    {
        var c = CreateCharacter();

        SkillCatalog.GrantSkillsUpToLevel(c, 2);

        bool healEquipped = false;
        foreach (var skill in c.GetEquippedPassiveSkills())
        {
            if (skill.SkillId != "heal") continue;
            healEquipped = true;
            break;
        }

        AssertThat(c.KnownSkillIds.Contains("heal")).IsTrue();
        AssertThat(healEquipped).IsTrue();
        AssertThat(c.ActiveSkillId).IsEqual("power_strike");
    }

    [TestCase]
    public void SkillCatalog_GrantSkillsUpToLevel_PreservesActiveSkillWhenAutoEquipWouldReplaceIt()
    {
        var c = CreateCharacter();

        SkillCatalog.GrantSkillsUpToLevel(c, 1);
        AssertThat(c.ActiveSkillId).IsEqual("power_strike");

        SkillCatalog.GrantSkillsUpToLevel(c, 3);

        AssertThat(c.KnownSkillIds.Contains("fire_bolt")).IsTrue();
        AssertThat(c.ActiveSkillId).IsEqual("power_strike");
    }

    [TestCase]
    public void SkillCatalog_AutoEquip_DoesNotOverwriteExistingActiveSkill()
    {
        // Arrange: give player level 1 skills (power_strike = active, level 1)
        var player = new Character { Name = "Hero", Level = 1, MaxHealth = 100, CurrentHealth = 100 };
        SkillCatalog.GrantSkillsUpToLevel(player, 1);
        // power_strike should be auto-equipped as the active skill
        AssertThat(player.ActiveSkillId).IsEqual("power_strike");

        // Act: grant level 3 skills (fire_bolt = active, level 3)
        SkillCatalog.GrantSkillsUpToLevel(player, 3);

        // Assert: player's active skill was NOT replaced — fire_bolt is learned but not auto-equipped
        AssertThat(player.ActiveSkillId).IsEqual("power_strike");
        AssertThat(player.KnownSkillIds).Contains("fire_bolt");
    }

    [TestCase]
    public void SkillCatalog_GrantSkillsUpToLevel_OrdersLearnedSkillsByUnlockLevelThenSkillId()
    {
        var player = CreateCharacter();

        var registryField = typeof(SkillCatalog).GetField(
            "_registry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var registry = (Dictionary<string, Skill>)registryField!.GetValue(null)!;

        const string injectedSkillId = "a_test_low_unlock";
        var injectedSkill = new Skill
        {
            SkillId = injectedSkillId,
            DisplayName = "Injected Low Unlock",
            Description = "Test skill for deterministic ordering",
            ManaCost = 1,
            UnlockLevel = 1,
            Type = SkillType.Passive,
            TriggerType = SkillTriggerType.OnPlayerTurn,
            TriggerChance = 1.0f,
            PassiveCooldown = 0,
        };

        bool hadExisting = registry.TryGetValue(injectedSkillId, out Skill? previousSkill);
        registry[injectedSkillId] = injectedSkill;

        try
        {
            SkillCatalog.GrantSkillsUpToLevel(player, 2);

            AssertThat(player.KnownSkillIds.Count).IsEqual(3);
            AssertThat(player.KnownSkillIds[0]).IsEqual("a_test_low_unlock");
            AssertThat(player.KnownSkillIds[1]).IsEqual("power_strike");
            AssertThat(player.KnownSkillIds[2]).IsEqual("heal");
        }
        finally
        {
            if (hadExisting && previousSkill != null)
                registry[injectedSkillId] = previousSkill;
            else
                registry.Remove(injectedSkillId);
        }
    }

    // ---- SkillCatalog ------------------------------------------------------

    [TestCase]
    public void SkillCatalog_GetById_ReturnsSkillForKnownId()
    {
        var skill = SkillCatalog.GetById("power_strike");
        AssertThat(skill).IsNotNull();
        AssertThat(skill!.DisplayName).IsEqual("Power Strike");
        AssertThat(skill.Type).IsEqual(SkillType.Active);
        AssertThat(skill.ActivePeriod).IsEqual(3);
    }

    [TestCase]
    public void SkillCatalog_GetById_ReturnsNullForUnknownId()
    {
        var skill = SkillCatalog.GetById("nonexistent_skill");
        AssertThat(skill).IsNull();
    }

    [TestCase]
    public void SkillCatalog_GetById_BattleCry_HasPassiveCooldown()
    {
        var skill = SkillCatalog.GetById("battle_cry");

        AssertThat(skill).IsNotNull();
        AssertThat(skill!.Type).IsEqual(SkillType.Passive);
        AssertThat(skill.PassiveCooldown).IsEqual(3);
    }

    // ---- SkillEffect: Damage -----------------------------------------------

    [TestCase]
    public void DamageSkillEffect_DealsDamageToEnemy()
    {
        var caster = CreateCharacter(attack: 20);
        var enemy = CreateEnemy(hp: 100);
        var effect = new DamageSkillEffect(1.5f, "physical");

        int enemyHpBefore = enemy.CurrentHealth;
        effect.Apply(caster, enemy);

        AssertThat(enemy.CurrentHealth).IsLess(enemyHpBefore);
    }

    [TestCase]
    public void DamageSkillEffect_ScalesWithCasterAttack()
    {
        var weakCaster = CreateCharacter(attack: 10);
        var strongCaster = CreateCharacter(attack: 40);

        var enemy1 = CreateEnemy(hp: 1000);
        var enemy2 = CreateEnemy(hp: 1000);
        var effect = new DamageSkillEffect(1.0f);

        effect.Apply(weakCaster, enemy1);
        effect.Apply(strongCaster, enemy2);

        // Stronger caster deals more damage → enemy2 has less HP remaining than enemy1
        AssertThat(enemy1.CurrentHealth).IsGreater(enemy2.CurrentHealth);
    }

    // ---- SkillEffect: Heal -------------------------------------------------

    [TestCase]
    public void HealSkillEffect_RestoresHP()
    {
        var caster = CreateCharacter(hp: 100);
        caster.CurrentHealth = 40;
        var enemy = CreateEnemy();
        var effect = new HealSkillEffect(50);

        effect.Apply(caster, enemy);

        AssertThat(caster.CurrentHealth).IsEqual(90);
    }

    [TestCase]
    public void HealSkillEffect_ClampsToMaxHealth()
    {
        var caster = CreateCharacter(hp: 100);
        caster.CurrentHealth = 95;
        var effect = new HealSkillEffect(50);

        effect.Apply(caster, CreateEnemy());

        AssertThat(caster.CurrentHealth).IsEqual(100); // capped at max
    }

    // ---- SkillEffect: ApplyBuff --------------------------------------------

    [TestCase]
    public void ApplyBuffSkillEffect_AppliesStrengthToPlayer()
    {
        var caster = CreateCharacter(attack: 20);
        var effect = new ApplyBuffSkillEffect(StatusEffectType.Strength, 15, 3, "ATK");

        effect.Apply(caster, CreateEnemy());

        AssertThat(caster.ActiveBuffs.GetAttackFlatBonus()).IsEqual(15);
    }

    // ---- SkillEffect: ApplyDebuff ------------------------------------------

    [TestCase]
    public void ApplyDebuffSkillEffect_GuaranteedStunLandsOnEnemy()
    {
        var enemy = CreateEnemy();
        var effect = new ApplyDebuffSkillEffect(StatusEffectType.Stun, 0, 1, 1.0f); // 100% chance

        effect.Apply(CreateCharacter(), enemy);

        AssertThat(enemy.ActiveStatusEffects.IsStunned).IsTrue();
    }

    // ---- SkillEffect: Combo -----------------------------------------------

    [TestCase]
    public void ComboSkillEffect_AppliesBothEffects()
    {
        var caster = CreateCharacter(attack: 20);
        var enemy = CreateEnemy(hp: 200);
        int hpBefore = enemy.CurrentHealth;

        var combo = new ComboSkillEffect(
            new DamageSkillEffect(1.0f),
            new ApplyDebuffSkillEffect(StatusEffectType.Stun, 0, 1, 1.0f)
        );
        combo.Apply(caster, enemy);

        AssertThat(enemy.CurrentHealth).IsLess(hpBefore); // damage applied
        AssertThat(enemy.ActiveStatusEffects.IsStunned).IsTrue();  // stun applied
    }

    // ---- Skill.ShouldTriggerPassive ----------------------------------------

    [TestCase]
    public void ShouldTriggerPassive_ReturnsFalseForActiveSkill()
    {
        var skill = SkillCatalog.GetById("power_strike")!;
        var result = skill.ShouldTriggerPassive(CreateCharacter(), CreateEnemy(), new System.Random());
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void ShouldTriggerPassive_OnLowPlayerHp_TriggersBelowThreshold()
    {
        var skill = SkillCatalog.GetById("heal")!; // threshold = 40%
        var caster = CreateCharacter(hp: 100);
        caster.CurrentHealth = 30; // 30% < 40%

        bool triggered = skill.ShouldTriggerPassive(caster, CreateEnemy(), new System.Random());

        AssertThat(triggered).IsTrue();
    }

    [TestCase]
    public void ShouldTriggerPassive_OnLowPlayerHp_DoesNotTriggerAboveThreshold()
    {
        var skill = SkillCatalog.GetById("heal")!; // threshold = 40%
        var caster = CreateCharacter(hp: 100);
        caster.CurrentHealth = 60; // 60% > 40%

        bool triggered = skill.ShouldTriggerPassive(caster, CreateEnemy(), new System.Random());

        AssertThat(triggered).IsFalse();
    }

    [TestCase]
    public void ShouldTriggerPassive_OnLowEnemyHp_TriggersBelowThreshold()
    {
        var skill = SkillCatalog.GetById("battle_cry")!; // threshold = 30%
        var enemy = CreateEnemy(hp: 100);
        enemy.CurrentHealth = 20; // 20% < 30%

        bool triggered = skill.ShouldTriggerPassive(CreateCharacter(), enemy, new System.Random());

        AssertThat(triggered).IsTrue();
    }
}
