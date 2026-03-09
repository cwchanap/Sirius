using System;
using Godot;

/// <summary>
/// Base for all skill effects. Implement Apply() with the effect logic.
/// Effects must not access the Godot scene tree — they operate on Character and Enemy data only.
/// To add a new effect type: create a new sealed subclass here and a factory method
/// in SkillCatalog. No existing files need to change.
/// </summary>
public abstract class SkillEffect
{
    public abstract string Description { get; }

    /// <summary>
    /// Applies this skill's effect. caster is the player; target is the enemy.
    /// Returns true if the effect was applied successfully.
    /// </summary>
    public abstract bool Apply(Character caster, Enemy target);
}

// ---- Damage effects --------------------------------------------------------

/// <summary>
/// Deals damage to the enemy based on the caster's effective attack multiplied by a scaling factor.
/// </summary>
public sealed class DamageSkillEffect : SkillEffect
{
    public float DamageMultiplier { get; }
    public string FlavorLabel { get; }

    public DamageSkillEffect(float damageMultiplier, string flavorLabel = "")
    {
        DamageMultiplier = damageMultiplier;
        FlavorLabel = flavorLabel;
    }

    public override string Description =>
        FlavorLabel.Length > 0
            ? $"{(int)(DamageMultiplier * 100)}% ATK {FlavorLabel} damage"
            : $"{(int)(DamageMultiplier * 100)}% ATK damage";

    public override bool Apply(Character caster, Enemy target)
    {
        if (caster == null || target == null) return false;
        int damage = Mathf.Max(1, (int)(caster.GetEffectiveAttack() * DamageMultiplier));
        int actualDamage = target.TakeDamage(damage);
        GD.Print($"[SkillEffect] {caster.Name} deals {actualDamage} {FlavorLabel} damage to {target.Name}!");
        return true;
    }
}

// ---- Healing effects -------------------------------------------------------

/// <summary>
/// Restores a fixed amount of HP to the caster.
/// </summary>
public sealed class HealSkillEffect : SkillEffect
{
    public int HealAmount { get; }

    public HealSkillEffect(int healAmount)
    {
        HealAmount = Mathf.Max(1, healAmount);
    }

    public override string Description => $"Restore {HealAmount} HP";

    public override bool Apply(Character caster, Enemy target)
    {
        if (caster == null) return false;
        caster.Heal(HealAmount);
        GD.Print($"[SkillEffect] {caster.Name} heals for {HealAmount} HP!");
        return true;
    }
}

// ---- Buff effects ----------------------------------------------------------

/// <summary>
/// Applies a temporary status buff to the caster for a number of turns.
/// Uses the same StatusEffectSet.Add() merge semantics as consumable buffs.
/// </summary>
public sealed class ApplyBuffSkillEffect : SkillEffect
{
    public StatusEffectType BuffType { get; }
    public int Magnitude { get; }
    public int Duration { get; }
    public string Label { get; }

    public ApplyBuffSkillEffect(StatusEffectType buffType, int magnitude, int duration, string label)
    {
        if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be at least 1 turn.");
        BuffType = buffType;
        Magnitude = magnitude;
        Duration = duration;
        Label = label;
    }

    public override string Description => $"+{Magnitude} {Label} for {Duration} turns";

    public override bool Apply(Character caster, Enemy target)
    {
        if (caster == null) return false;
        caster.ActiveBuffs.Add(new ActiveStatusEffect(BuffType, Magnitude, Duration));
        GD.Print($"[SkillEffect] {caster.Name} gains {BuffType} +{Magnitude} for {Duration} turns!");
        return true;
    }
}

// ---- Debuff effects --------------------------------------------------------

/// <summary>
/// Applies a temporary status debuff to the enemy with an optional chance to land.
/// Chance = 1.0 means guaranteed; 0.5 means 50% chance.
/// </summary>
public sealed class ApplyDebuffSkillEffect : SkillEffect
{
    public StatusEffectType DebuffType { get; }
    public int Magnitude { get; }
    public int Duration { get; }
    public float Chance { get; }

    public ApplyDebuffSkillEffect(StatusEffectType debuffType, int magnitude, int duration, float chance = 1.0f)
    {
        if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be at least 1 turn.");
        DebuffType = debuffType;
        Magnitude = magnitude;
        Duration = duration;
        Chance = Mathf.Clamp(chance, 0f, 1f);
    }

    public override string Description => Chance < 1.0f
        ? $"{(int)(Chance * 100)}% chance to inflict {DebuffType} for {Duration} turn(s)"
        : $"Inflict {DebuffType} for {Duration} turn(s)";

    public override bool Apply(Character caster, Enemy target)
    {
        if (target == null) return false;
        if (GD.Randf() <= Chance)
        {
            target.ActiveStatusEffects.Add(new ActiveStatusEffect(DebuffType, Magnitude, Duration));
            GD.Print($"[SkillEffect] {target.Name} inflicted with {DebuffType} for {Duration} turn(s)!");
        }
        // BattleManager refunds mana only when Apply() returns false, so a missed roll still counts
        // as an attempted activation that consumes mana.
        return true;
    }
}

// ---- Combo effects ---------------------------------------------------------

/// <summary>
/// Applies two effects in sequence (e.g. damage then debuff).
/// Both effects run even if the first fails — returns true if either succeeded.
/// </summary>
public sealed class ComboSkillEffect : SkillEffect
{
    public SkillEffect Primary { get; }
    public SkillEffect Secondary { get; }

    public ComboSkillEffect(SkillEffect primary, SkillEffect secondary)
    {
        Primary = primary ?? throw new ArgumentNullException(nameof(primary));
        Secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
    }

    public override string Description => $"{Primary.Description}; {Secondary.Description}";

    public override bool Apply(Character caster, Enemy target)
    {
        bool a = Primary.Apply(caster, target);
        bool b = Secondary.Apply(caster, target);
        return a || b;
    }
}
