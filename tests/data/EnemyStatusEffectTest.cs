using System;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>Tests for Enemy status effects and GetEffective*() methods.</summary>
[TestSuite]
[RequireGodotRuntime]
public partial class EnemyStatusEffectTest : Godot.Node
{
    // ---- Enemy.GetEffective*() with no effects --------------------------------

    [TestCase]
    public void GetEffectiveAttack_NoEffects_EqualsBaseAttack()
    {
        var enemy = Enemy.CreateGoblin();
        AssertThat(enemy.GetEffectiveAttack()).IsEqual(enemy.Attack);
    }

    [TestCase]
    public void GetEffectiveDefense_NoEffects_EqualsBaseDefense()
    {
        var enemy = Enemy.CreateGoblin();
        AssertThat(enemy.GetEffectiveDefense()).IsEqual(enemy.Defense);
    }

    [TestCase]
    public void GetEffectiveSpeed_NoEffects_EqualsBaseSpeed()
    {
        var enemy = Enemy.CreateGoblin();
        AssertThat(enemy.GetEffectiveSpeed()).IsEqual(enemy.Speed);
    }

    // ---- Weaken debuff -------------------------------------------------------

    [TestCase]
    public void GetEffectiveAttack_Weaken25Percent_ReducesAttack()
    {
        var enemy = Enemy.CreateOrc();
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Weaken, 25, 3));

        int expected = Godot.Mathf.Max(1, (int)(enemy.Attack * 0.75f));
        AssertThat(enemy.GetEffectiveAttack()).IsEqual(expected);
    }

    // ---- Slow debuff ---------------------------------------------------------

    [TestCase]
    public void GetEffectiveSpeed_Slow50Percent_HalvesSpeed()
    {
        var enemy = Enemy.CreateGoblin();
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Slow, 50, 3));

        int expected = Godot.Mathf.Max(1, (int)(enemy.Speed * 0.5f));
        AssertThat(enemy.GetEffectiveSpeed()).IsEqual(expected);
    }

    // ---- Stun detection ------------------------------------------------------

    [TestCase]
    public void IsStunned_FalseByDefault()
    {
        var enemy = Enemy.CreateGoblin();
        AssertThat(enemy.ActiveStatusEffects.IsStunned).IsFalse();
    }

    [TestCase]
    public void IsStunned_TrueAfterStunApplied()
    {
        var enemy = Enemy.CreateGoblin();
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Stun, 0, 1));
        AssertThat(enemy.ActiveStatusEffects.IsStunned).IsTrue();
    }

    [TestCase]
    public void IsStunned_FalseAfterStunExpires()
    {
        var enemy = Enemy.CreateGoblin();
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Stun, 0, 1));
        enemy.ActiveStatusEffects.Tick();
        AssertThat(enemy.ActiveStatusEffects.IsStunned).IsFalse();
    }

    // ---- Blind detection -----------------------------------------------------

    [TestCase]
    public void IsBlind_TrueAfterBlindApplied()
    {
        var enemy = Enemy.CreateSwampWretch();
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Blind, 0, 2));
        AssertThat(enemy.ActiveStatusEffects.IsBlind).IsTrue();
    }

    // ---- EnemyDebuffProfile --------------------------------------------------

    [TestCase]
    public void EnemyDebuffProfile_Goblin_HasPoisonAbility()
    {
        var abilities = EnemyDebuffProfile.GetAbilities(EnemyTypeId.Goblin);
        AssertThat(abilities).IsNotNull();
        AssertThat(abilities!.Count).IsGreaterEqual(1);
        AssertThat((int)abilities[0].EffectType).IsEqual((int)StatusEffectType.Poison);
    }

    [TestCase]
    public void EnemyDebuffProfile_Troll_ReturnsNull()
    {
        // Troll has no debuff profile
        var abilities = EnemyDebuffProfile.GetAbilities(EnemyTypeId.Troll);
        AssertThat(abilities).IsNull();
    }

    [TestCase]
    public void EnemyDebuffProfile_NullType_ReturnsNull()
    {
        var abilities = EnemyDebuffProfile.GetAbilities(null);
        AssertThat(abilities).IsNull();
    }

    // ---- Antidote / CureStatusEffect ----------------------------------------

    [TestCase]
    public void Antidote_CuresPoisonOnPlayer()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Poison, 8, 3));
        AssertThat(player.ActiveBuffs.HasAny).IsTrue();

        var antidote = ConsumableCatalog.CreateAntidote();
        antidote.Apply(player);

        AssertThat(player.ActiveBuffs.HasAny).IsFalse();
    }

    [TestCase]
    public void Antidote_LeavesOtherEffectsIntact()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Poison,   8, 3));
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 2));

        ConsumableCatalog.CreateAntidote().Apply(player);

        AssertThat(player.ActiveBuffs.Effects.Count).IsEqual(1);
        AssertThat((int)player.ActiveBuffs.Effects[0].Type).IsEqual((int)StatusEffectType.Strength);
    }

    // ---- New consumable items ------------------------------------------------

    [TestCase]
    public void ItemCatalog_RegistersNewConsumables()
    {
        AssertThat(ItemCatalog.ItemExists("antidote")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("regen_potion")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("poison_vial")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("flash_powder")).IsTrue();
    }

    [TestCase]
    public void RegenPotion_Apply_AddsRegenToPlayer()
    {
        var player = TestHelpers.CreateTestCharacter();
        ConsumableCatalog.CreateRegenPotion().Apply(player);
        AssertThat(player.ActiveBuffs.HasAny).IsTrue();
        AssertThat(player.ActiveBuffs.IsStunned).IsFalse();
    }

    [TestCase]
    public void EnemyDebuffEffect_ApplyToEnemy_AddsPoisonToEnemy()
    {
        var enemy  = Enemy.CreateGoblin();
        var vial   = ConsumableCatalog.CreatePoisonVial();

        // EnemyDebuffEffect.Apply(Character) is a no-op; ApplyToEnemy is used by BattleManager
        if (vial.Effect is EnemyDebuffEffect ede)
        {
            ede.ApplyToEnemy(enemy);
            AssertThat(enemy.ActiveStatusEffects.HasAny).IsTrue();
        }
        else
        {
            AssertThat(false).OverrideFailureMessage("PoisonVial.Effect should be EnemyDebuffEffect, but was: " + (vial.Effect?.GetType().Name ?? "null")).IsTrue();
        }
    }

    // ---- Character.GetEffectiveAccuracy -------------------------------------

    [TestCase]
    public void GetEffectiveAccuracy_NoBlind_Returns100()
    {
        var player = TestHelpers.CreateTestCharacter();
        AssertThat(player.GetEffectiveAccuracy()).IsEqual(100);
    }

    [TestCase]
    public void GetEffectiveAccuracy_Blinded_Returns55()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Blind, 0, 2));
        AssertThat(player.GetEffectiveAccuracy()).IsEqual(55);
    }

    // ---- EnemyDebuffAbility validation ------------------------------------------

    [TestCase]
    public void EnemyDebuffAbility_InvalidChance_Throws()
    {
        AssertThrown(() => new EnemyDebuffAbility(StatusEffectType.Poison, 5, 3, 1.5f))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void EnemyDebuffAbility_NegativeChance_Throws()
    {
        AssertThrown(() => new EnemyDebuffAbility(StatusEffectType.Poison, 5, 3, -0.1f))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void EnemyDebuffAbility_ZeroDuration_Throws()
    {
        AssertThrown(() => new EnemyDebuffAbility(StatusEffectType.Poison, 5, 0, 0.20f))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void EnemyDebuffProfile_GetAbilities_KnownEnemy_ReturnsAbilities()
    {
        var abilities = EnemyDebuffProfile.GetAbilities(EnemyTypeId.Goblin);
        AssertThat(abilities).IsNotNull();
        AssertThat(abilities!.Count).IsGreater(0);
        AssertThat((int)abilities[0].EffectType).IsEqual((int)StatusEffectType.Poison);
    }

    [TestCase]
    public void EnemyDebuffProfile_GetAbilities_UnknownEnemy_ReturnsNull()
    {
        var abilities = EnemyDebuffProfile.GetAbilities("unknown_xyz");
        AssertThat(abilities).IsNull();
    }
}
