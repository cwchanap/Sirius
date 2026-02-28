using System;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>Tests for StatusEffectSet and ActiveStatusEffect.</summary>
[TestSuite]
[RequireGodotRuntime]
public partial class StatusEffectSetTest : Godot.Node
{
    // ---- ActiveStatusEffect record ------------------------------------------

    [TestCase]
    public void Tick_DecrementsTurnsRemaining()
    {
        var effect = new ActiveStatusEffect(StatusEffectType.Poison, 8, 3);
        var ticked  = effect.Tick();
        AssertThat(ticked.TurnsRemaining).IsEqual(2);
        AssertThat(ticked.Magnitude).IsEqual(8);  // unchanged
    }

    [TestCase]
    public void IsExpired_TrueAtZeroTurns()
    {
        var effect = new ActiveStatusEffect(StatusEffectType.Stun, 0, 1).Tick();
        AssertThat(effect.IsExpired).IsTrue();
    }

    [TestCase]
    public void IsDoT_TrueForPoisonAndBurn()
    {
        AssertThat(new ActiveStatusEffect(StatusEffectType.Poison, 5, 3).IsDoT).IsTrue();
        AssertThat(new ActiveStatusEffect(StatusEffectType.Burn,   5, 3).IsDoT).IsTrue();
        AssertThat(new ActiveStatusEffect(StatusEffectType.Regen,  5, 3).IsDoT).IsFalse();
    }

    [TestCase]
    public void IsHoT_TrueForRegen()
    {
        AssertThat(new ActiveStatusEffect(StatusEffectType.Regen,  5, 3).IsHoT).IsTrue();
        AssertThat(new ActiveStatusEffect(StatusEffectType.Poison, 5, 3).IsHoT).IsFalse();
    }

    // ---- StatusEffectSet.Add / merge ----------------------------------------

    [TestCase]
    public void Add_MergesTakesMaxMagnitudeAndMaxTurns()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 3));
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 10, 5));

        AssertThat(set.Effects.Count).IsEqual(1);
        AssertThat(set.Effects[0].Magnitude).IsEqual(15);      // max(15,10)
        AssertThat(set.Effects[0].TurnsRemaining).IsEqual(5);  // max(3,5)
    }

    [TestCase]
    public void Add_DifferentTypes_CreatesSeparateEntries()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Poison,   5, 3));
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 3));
        AssertThat(set.Effects.Count).IsEqual(2);
    }

    // ---- StatusEffectSet.IsStunned / IsBlind --------------------------------

    [TestCase]
    public void IsStunned_TrueWhenStunPresent()
    {
        var set = new StatusEffectSet();
        AssertThat(set.IsStunned).IsFalse();
        set.Add(new ActiveStatusEffect(StatusEffectType.Stun, 0, 1));
        AssertThat(set.IsStunned).IsTrue();
    }

    [TestCase]
    public void IsBlind_TrueWhenBlindPresent()
    {
        var set = new StatusEffectSet();
        AssertThat(set.IsBlind).IsFalse();
        set.Add(new ActiveStatusEffect(StatusEffectType.Blind, 0, 2));
        AssertThat(set.IsBlind).IsTrue();
    }

    // ---- StatusEffectSet stat getters ---------------------------------------

    [TestCase]
    public void GetAttackFlatBonus_ReturnsStrengthMagnitude()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 3));
        AssertThat(set.GetAttackFlatBonus()).IsEqual(15);
    }

    [TestCase]
    public void GetDefenseFlatBonus_ReturnsFortifyMagnitude()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Fortify, 10, 4));
        AssertThat(set.GetDefenseFlatBonus()).IsEqual(10);
    }

    [TestCase]
    public void GetSpeedFlatBonus_ReturnsHasteMagnitude()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Haste, 8, 3));
        AssertThat(set.GetSpeedFlatBonus()).IsEqual(8);
    }

    [TestCase]
    public void GetAttackMultiplier_WeakenReducesAttack()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Weaken, 25, 3));
        AssertThat(set.GetAttackMultiplier()).IsEqualApprox(0.75f, 0.001f);
    }

    [TestCase]
    public void GetSpeedMultiplier_SlowReducesSpeed()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Slow, 50, 3));
        AssertThat(set.GetSpeedMultiplier()).IsEqualApprox(0.5f, 0.001f);
    }

    [TestCase]
    public void GetAccuracyMultiplier_BlindReturns055()
    {
        var set = new StatusEffectSet();
        AssertThat(set.GetAccuracyMultiplier()).IsEqualApprox(1.0f, 0.001f);
        set.Add(new ActiveStatusEffect(StatusEffectType.Blind, 0, 2));
        AssertThat(set.GetAccuracyMultiplier()).IsEqualApprox(0.55f, 0.001f);
    }

    // ---- StatusEffectSet.Tick -----------------------------------------------

    [TestCase]
    public void Tick_RemovesExpiredEffects()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 10, 1));
        var (expired, _, _) = set.Tick();

        AssertThat(expired.Count).IsEqual(1);
        AssertThat((int)expired[0].Type).IsEqual((int)StatusEffectType.Strength);
        AssertThat(set.Effects.Count).IsEqual(0);
    }

    [TestCase]
    public void Tick_AccumulatesDotDamage()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Poison, 8, 3));
        var (_, dot, _) = set.Tick();

        AssertThat(dot).IsEqual(8);
        AssertThat(set.Effects.Count).IsEqual(1);  // 2 turns left
    }

    [TestCase]
    public void Tick_AccumulatesHotHeal()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Regen, 15, 3));
        var (_, _, hot) = set.Tick();

        AssertThat(hot).IsEqual(15);
    }

    [TestCase]
    public void Tick_DotStillFiresOnLastTurn()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Poison, 5, 1));
        var (expired, dot, _) = set.Tick();

        AssertThat(dot).IsEqual(5);       // fires on the last turn
        AssertThat(expired.Count).IsEqual(1);  // and then expires
    }

    // ---- StatusEffectSet.RemoveType -----------------------------------------

    [TestCase]
    public void RemoveType_CuresPoison()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Poison, 5, 3));
        set.Add(new ActiveStatusEffect(StatusEffectType.Blind,  0, 2));

        bool removed = set.RemoveType(StatusEffectType.Poison);

        AssertThat(removed).IsTrue();
        AssertThat(set.Effects.Count).IsEqual(1);
        AssertThat((int)set.Effects[0].Type).IsEqual((int)StatusEffectType.Blind);
    }

    [TestCase]
    public void RemoveType_ReturnsFalseWhenNotPresent()
    {
        var set = new StatusEffectSet();
        AssertThat(set.RemoveType(StatusEffectType.Poison)).IsFalse();
    }

    // ---- StatusEffectSet.Clear ----------------------------------------------

    [TestCase]
    public void Clear_RemovesAllEffects()
    {
        var set = new StatusEffectSet();
        set.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 3));
        set.Add(new ActiveStatusEffect(StatusEffectType.Poison,    5, 2));
        set.Clear();
        AssertThat(set.Effects.Count).IsEqual(0);
        AssertThat(set.HasAny).IsFalse();
    }

    // ---- ActiveStatusEffect validation ------------------------------------------

    [TestCase]
    public void ActiveStatusEffect_NegativeMagnitude_Throws()
    {
        AssertThrown(() => new ActiveStatusEffect(StatusEffectType.Poison, -1, 3))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void ActiveStatusEffect_ZeroMagnitude_IsValid()
    {
        // Stun and Blind use Magnitude=0 by design
        var effect = new ActiveStatusEffect(StatusEffectType.Stun, 0, 2);
        AssertThat(effect.Magnitude).IsEqual(0);
    }

    [TestCase]
    public void ActiveStatusEffect_Tick_CanProduceExpiredState()
    {
        // TurnsRemaining is NOT validated — Tick() must be able to produce TurnsRemaining=0
        var effect = new ActiveStatusEffect(StatusEffectType.Poison, 5, 1);
        var ticked = effect.Tick();
        AssertThat(ticked.IsExpired).IsTrue();
        AssertThat(ticked.TurnsRemaining).IsEqual(0);
    }
}
