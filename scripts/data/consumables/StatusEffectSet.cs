using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Tracks all active status effects on one combatant.
/// One entry per StatusEffectType is enforced — Add() merges duplicates.
/// Replaces the old ActiveBuffSet class.
///
/// Character.GetEffective*() and Enemy.GetEffective*() delegate to this for stat modifiers.
/// BattleManager calls Tick() after each combatant's action to advance durations and
/// accumulate DoT/HoT values that it then applies to HP directly.
/// </summary>
public class StatusEffectSet
{
    /// <summary>Accuracy multiplier when Blind is active (55% = 0.55).</summary>
    public const float BlindAccuracyMultiplier = 0.55f;

    private readonly List<ActiveStatusEffect> _effects = new();

    public IReadOnlyList<ActiveStatusEffect> Effects => _effects;
    public bool HasAny => _effects.Count > 0;

    /// <summary>True when a Stun effect is present (BattleManager skips the combatant's action).</summary>
    public bool IsStunned => _effects.Exists(e => e.Type == StatusEffectType.Stun);

    /// <summary>True when a Blind effect is present (BattleManager applies miss chance).</summary>
    public bool IsBlind => _effects.Exists(e => e.Type == StatusEffectType.Blind);

    // -------------------------------------------------------------------------
    // Add / merge
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds an effect. If the same type already exists, keeps the higher magnitude
    /// and higher turns remaining (max-merge prevents waste from re-applying).
    /// </summary>
    public void Add(ActiveStatusEffect effect)
    {
        if (effect == null) return;

        int idx = _effects.FindIndex(e => e.Type == effect.Type);
        if (idx >= 0)
        {
            var old = _effects[idx];
            _effects[idx] = effect with
            {
                Magnitude      = Math.Max(old.Magnitude, effect.Magnitude),
                TurnsRemaining = Math.Max(old.TurnsRemaining, effect.TurnsRemaining),
            };
        }
        else
        {
            _effects.Add(effect);
        }
    }

    /// <summary>
    /// Removes all effects of the given type. Returns true if any were removed.
    /// Used by cure items (e.g. Antidote removes Poison and Burn).
    /// </summary>
    public bool RemoveType(StatusEffectType type)
    {
        int removed = _effects.RemoveAll(e => e.Type == type);
        return removed > 0;
    }

    // -------------------------------------------------------------------------
    // Tick
    // -------------------------------------------------------------------------

    /// <summary>
    /// Accumulates DoT/HoT values, decrements all durations, and removes expired entries.
    /// Returns the expired effects list plus total dot and hot HP for this tick.
    /// BattleManager applies dotDamage and hotHeal to the combatant's HP directly
    /// (DoT bypasses defense; HoT is a straight heal).
    /// Call once after each combatant's action.
    /// </summary>
    public (IReadOnlyList<ActiveStatusEffect> Expired, int DotDamage, int HotHeal) Tick()
    {
        int dot = 0;
        int hot = 0;

        // Accumulate DoT/HoT for this tick before decrementing durations,
        // so an effect on its last turn still deals/heals.
        foreach (var e in _effects)
        {
            if (e.IsDoT) dot += Mathf.Max(1, e.Magnitude);
            if (e.IsHoT) hot += Mathf.Max(1, e.Magnitude);
        }

        var expired = new List<ActiveStatusEffect>();
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            _effects[i] = _effects[i].Tick();
            if (_effects[i].IsExpired)
            {
                expired.Add(_effects[i]);
                _effects.RemoveAt(i);
            }
        }

        return (expired, dot, hot);
    }

    // -------------------------------------------------------------------------
    // Stat modifiers
    // -------------------------------------------------------------------------

    /// <summary>Flat Attack bonus from Strength buffs.</summary>
    public int GetAttackFlatBonus() => Find(StatusEffectType.Strength)?.Magnitude ?? 0;

    /// <summary>Flat Defense bonus from Fortify buffs.</summary>
    public int GetDefenseFlatBonus() => Find(StatusEffectType.Fortify)?.Magnitude ?? 0;

    /// <summary>Flat Speed bonus from Haste buffs.</summary>
    public int GetSpeedFlatBonus() => Find(StatusEffectType.Haste)?.Magnitude ?? 0;

    /// <summary>
    /// Attack multiplier from Weaken debuff. Returns 1.0 when not weakened.
    /// Weaken with Magnitude=25 → 0.75 (−25%).
    /// </summary>
    public float GetAttackMultiplier()
    {
        var weaken = Find(StatusEffectType.Weaken);
        return weaken == null ? 1.0f : 1.0f - Mathf.Clamp(weaken.Magnitude, 0, 100) / 100.0f;
    }

    /// <summary>
    /// Speed multiplier from Slow debuff. Returns 1.0 when not slowed.
    /// Slow with Magnitude=50 → 0.5 (−50%).
    /// </summary>
    public float GetSpeedMultiplier()
    {
        var slow = Find(StatusEffectType.Slow);
        return slow == null ? 1.0f : 1.0f - Mathf.Clamp(slow.Magnitude, 0, 100) / 100.0f;
    }

    /// <summary>
    /// Accuracy multiplier from Blind debuff. Returns BlindAccuracyMultiplier when blinded, 1.0 otherwise.
    /// BattleManager uses this to determine miss chance.
    /// </summary>
    public float GetAccuracyMultiplier() => IsBlind ? BlindAccuracyMultiplier : 1.0f;

    /// <summary>Removes all effects. Call at battle end to clean up transient state.</summary>
    public void Clear() => _effects.Clear();

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Add() enforces at most one entry per type, so Find is sufficient.</summary>
    private ActiveStatusEffect? Find(StatusEffectType type)
        => _effects.Find(e => e.Type == type);
}
