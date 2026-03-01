using System;
using System.Collections.Generic;

/// <summary>
/// Data-driven debuff ability for one enemy action.
/// BattleManager rolls Chance each time the enemy attacks; on success, the effect
/// is applied to the player.
/// </summary>
public record EnemyDebuffAbility
{
    public StatusEffectType EffectType { get; }
    public int              Magnitude  { get; }
    public int              Duration   { get; }   // turns
    public float            Chance     { get; }   // 0.0–1.0

    public EnemyDebuffAbility(StatusEffectType effectType, int magnitude, int duration, float chance)
    {
        if (chance < 0f || chance > 1f)
            throw new ArgumentOutOfRangeException(nameof(chance), chance, "Must be in [0.0, 1.0].");
        if (duration < 1)
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Must be at least 1 turn.");
        if (magnitude < 0)
            throw new ArgumentOutOfRangeException(nameof(magnitude), magnitude, "Must be non-negative.");

        EffectType = effectType;
        Magnitude  = magnitude;
        Duration   = duration;
        Chance     = chance;
    }
}

/// <summary>
/// Static lookup table mapping EnemyTypeId constants to the debuff abilities that
/// enemy can inflict during auto-battle. Enemies not listed here do normal attacks only.
///
/// To add a new enemy ability: add or extend an entry below using EnemyTypeId constants.
/// BattleManager.TryApplyEnemyDebuff() consumes this data without any per-enemy branching.
/// </summary>
public static class EnemyDebuffProfile
{
    private static readonly Dictionary<string, IReadOnlyList<EnemyDebuffAbility>> _profiles = new()
    {
        [EnemyTypeId.Goblin] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Poison, 5, 3, 0.20f),
        },
        [EnemyTypeId.CaveSpider] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Poison, 8, 4, 0.35f),
            new EnemyDebuffAbility(StatusEffectType.Slow,   4, 2, 0.20f),
        },
        [EnemyTypeId.SkeletonWarrior] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Weaken, 8, 3, 0.25f),
        },
        [EnemyTypeId.SwampWretch] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Poison, 10, 4, 0.30f),
            new EnemyDebuffAbility(StatusEffectType.Blind,   0, 2, 0.20f),
        },
        [EnemyTypeId.DarkMage] = new[]
        {
            new EnemyDebuffAbility(StatusEffectType.Weaken, 12, 3, 0.30f),
            new EnemyDebuffAbility(StatusEffectType.Stun,    0, 1, 0.15f),
        },
    };

    /// <summary>
    /// Returns the debuff abilities for the given enemy type, or null if that
    /// enemy has no debuff abilities (normal attacks only).
    /// </summary>
    public static IReadOnlyList<EnemyDebuffAbility>? GetAbilities(string? enemyType)
        => _profiles.TryGetValue(enemyType?.ToLowerInvariant() ?? string.Empty, out var abilities) ? abilities : null;
}
