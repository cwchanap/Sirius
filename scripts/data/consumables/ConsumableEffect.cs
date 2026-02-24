using Godot;

/// <summary>
/// Base for all consumable effects. Implement Apply() with the effect logic.
/// Effects must not access the Godot scene tree — they operate on Character data only.
/// To add a new effect type: create a new sealed subclass here and a factory method
/// in ConsumableCatalog. No existing files need to change.
/// </summary>
public abstract class ConsumableEffect
{
    public abstract string Description { get; }

    /// <summary>
    /// True if this effect only makes sense during battle (e.g. stat buffs/debuffs).
    /// False for effects usable anywhere (e.g. healing, cures).
    /// </summary>
    public virtual bool RequiresBattle => false;

    public abstract void Apply(Character target);
}

// ---- Instant effects --------------------------------------------------------

public sealed class HealEffect : ConsumableEffect
{
    public int Amount { get; }

    public HealEffect(int amount)
    {
        Amount = Mathf.Max(1, amount);
    }

    public override string Description => $"Restores {Amount} HP";

    public override void Apply(Character target)
    {
        if (target == null) return;
        target.Heal(Amount);
        GD.Print($"[HealEffect] {target.Name} healed for {Amount} HP");
    }
}

// ---- Status effect buffs/debuffs (player-targeting) -------------------------

/// <summary>
/// Applies a temporary status effect to the player for a fixed number of turns.
/// Replaces the old BuffEffect class. Handles all StatusEffectType values.
///
/// For flat buffs (Strength, Fortify, Haste): Magnitude is the flat stat bonus.
/// For percent debuffs (Weaken, Slow): Magnitude is the percent reduction (e.g. 25 = 25%).
/// For Blind and Stun: Magnitude should be 0 (presence alone is sufficient).
/// For DoT/HoT (Poison, Burn, Regen): Magnitude is flat HP per turn.
/// </summary>
public sealed class StatusEffectEffect : ConsumableEffect
{
    private readonly StatusEffectType _type;
    private readonly string           _label;

    public int Magnitude { get; }
    public int Turns     { get; }

    public override bool RequiresBattle => true;

    public StatusEffectEffect(StatusEffectType type, string label, int magnitude, int turns)
    {
        _type     = type;
        _label    = label;
        Magnitude = Mathf.Max(0, magnitude);  // 0 is valid for Stun/Blind
        Turns     = Mathf.Max(1, turns);
    }

    public override string Description => _type switch
    {
        StatusEffectType.Stun                              => $"Stuns for {Turns} turn(s)",
        StatusEffectType.Blind                             => $"Blinds for {Turns} turns ({(int)(StatusEffectSet.BlindAccuracyMultiplier * 100)}% accuracy)",
        StatusEffectType.Weaken or StatusEffectType.Slow   => $"-{Magnitude}% {_label} for {Turns} turns",
        StatusEffectType.Poison or StatusEffectType.Burn   => $"{_label} {Magnitude} HP/turn for {Turns} turns",
        StatusEffectType.Regen                             => $"Regen {Magnitude} HP/turn for {Turns} turns",
        _                                                  => $"+{Magnitude} {_label} for {Turns} turns",
    };

    public override void Apply(Character target)
    {
        if (target == null) return;
        target.ActiveBuffs.Add(new ActiveStatusEffect(_type, Magnitude, Turns));
        GD.Print($"[StatusEffectEffect] {target.Name} gains {_type} ({Magnitude}) for {Turns} turns");
    }
}

// ---- Cure effects -----------------------------------------------------------

/// <summary>
/// Removes one or more specific debuffs from the player.
/// Used by Antidote to cure Poison and Burn.
/// RequiresBattle is false — cures can be used anytime (they are safe no-ops if unneeded).
/// </summary>
public sealed class CureStatusEffect : ConsumableEffect
{
    private readonly StatusEffectType[] _cures;
    private readonly string             _label;

    public CureStatusEffect(string label, params StatusEffectType[] cures)
    {
        _cures = cures;
        _label = label;
    }

    public override string Description => $"Cures {_label}";

    public override void Apply(Character target)
    {
        if (target == null) return;
        bool removed = false;
        foreach (var type in _cures)
            removed |= target.ActiveBuffs.RemoveType(type);
        if (removed)
            GD.Print($"[CureStatusEffect] {target.Name} cured of {_label}");
    }
}

// ---- Enemy-targeting debuff effects -----------------------------------------

/// <summary>
/// Applies a debuff to the enemy rather than the player.
/// Apply(Character) is an intentional no-op — BattleManager detects this type and
/// routes it to ApplyToEnemy(Enemy) instead.
/// </summary>
public sealed class EnemyDebuffEffect : ConsumableEffect
{
    public StatusEffectType EffectType { get; }
    public int              Magnitude  { get; }
    public int              Turns      { get; }

    public override bool RequiresBattle => true;

    public EnemyDebuffEffect(StatusEffectType type, int magnitude, int turns)
    {
        EffectType = type;
        Magnitude  = Mathf.Max(0, magnitude);
        Turns      = Mathf.Max(1, turns);
    }

    public override string Description => $"Inflicts {EffectType} on enemy for {Turns} turns";

    /// <remarks>
    /// No-op — this effect targets the enemy. BattleManager calls ApplyToEnemy() directly.
    /// </remarks>
    public override void Apply(Character target) { }

    public void ApplyToEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(EffectType, Magnitude, Turns));
        GD.Print($"[EnemyDebuffEffect] {enemy.Name} inflicted with {EffectType} ({Magnitude}) for {Turns} turns");
    }
}
