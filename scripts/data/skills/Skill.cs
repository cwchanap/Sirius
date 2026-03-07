using System;
using Godot;

/// <summary>
/// Defines a learnable skill the player can equip.
///
/// Skills come in two types:
///   Active  — fires automatically every ActivePeriod player turns.
///   Passive — fires automatically when a trigger condition is met.
///
/// Both types cost mana on each activation. If the player has insufficient mana,
/// the skill is skipped that turn.
///
/// Skills are never stored as Godot Resources — they live in SkillCatalog and are
/// referenced on Character by ID (string). This avoids the problem of .tres-loaded
/// skills losing their non-exported Effect field.
/// </summary>
public class Skill
{
    // ---- Identity ----------------------------------------------------------

    public string SkillId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public int ManaCost { get; init; } = 10;
    public int UnlockLevel { get; init; } = 1;

    // ---- Type --------------------------------------------------------------

    public SkillType Type { get; init; } = SkillType.Active;

    // ---- Active skill config -----------------------------------------------

    /// <summary>
    /// How many player turns between each activation (e.g. 3 = fires on turns 3, 6, 9…).
    /// Only applies when Type == Active.
    /// </summary>
    public int ActivePeriod { get; init; } = 3;

    // ---- Passive skill trigger config -------------------------------------

    /// <summary>Condition that must be met for a passive skill to fire.</summary>
    public SkillTriggerType TriggerType { get; init; } = SkillTriggerType.OnPlayerTurn;

    /// <summary>
    /// Probability (0–1) of firing on each eligible turn.
    /// Used by OnPlayerTurn triggers. Ignored by HP-threshold triggers.
    /// </summary>
    public float TriggerChance { get; init; } = 0.15f;

    /// <summary>
    /// HP fraction below which an HP-threshold trigger fires.
    /// 0.4 means "HP &lt; 40%". Only used by OnLowPlayerHp and OnLowEnemyHp triggers.
    /// </summary>
    public float TriggerHpThreshold { get; init; } = 0.4f;

    /// <summary>
    /// Minimum number of player turns that must pass before this passive can fire again.
    /// 0 = no cooldown. Prevents HP-based passives from triggering every single turn.
    /// </summary>
    public int PassiveCooldown { get; init; } = 0;

    // ---- Effect ------------------------------------------------------------

    private SkillEffect? _effect;

    /// <summary>
    /// The effect applied when this skill fires.
    /// Throws if accessed before an effect is configured — always use SkillCatalog to create skills.
    /// </summary>
    public SkillEffect Effect
    {
        get => _effect ?? throw new InvalidOperationException(
            $"Skill '{DisplayName}' (id: '{SkillId}') has no Effect configured. " +
            "Create skills via SkillCatalog factory methods.");
        internal set => _effect = value;
    }

    /// <summary>Human-readable effect summary for tooltips.</summary>
    public string EffectDescription => _effect?.Description ?? "No effect";

    // ---- Trigger logic -----------------------------------------------------

    /// <summary>
    /// Returns true if this passive skill's trigger condition is satisfied.
    /// Always returns false for Active skills.
    /// </summary>
    public bool ShouldTriggerPassive(Character caster, Enemy target, Random rng)
    {
        if (Type != SkillType.Passive) return false;

        return TriggerType switch
        {
            SkillTriggerType.OnPlayerTurn =>
                rng.NextDouble() <= TriggerChance,

            SkillTriggerType.OnLowPlayerHp =>
                (float)caster.CurrentHealth / caster.GetEffectiveMaxHealth() < TriggerHpThreshold,

            SkillTriggerType.OnLowEnemyHp =>
                (float)target.CurrentHealth / target.MaxHealth < TriggerHpThreshold,

            _ => false
        };
    }

    /// <summary>
    /// Applies this skill's effect to the combatants.
    /// Returns true if the effect was applied successfully.
    /// Does NOT deduct mana — callers must call Character.TryUseMana() first.
    /// </summary>
    public bool Apply(Character caster, Enemy target)
    {
        if (_effect == null)
        {
            GD.PushWarning($"[Skill] '{DisplayName}' has no effect configured — skipping.");
            return false;
        }
        return _effect.Apply(caster, target);
    }
}

// ---- Enums -----------------------------------------------------------------

public enum SkillType
{
    /// <summary>Fires automatically every ActivePeriod player turns.</summary>
    Active,

    /// <summary>Fires automatically when a trigger condition is met.</summary>
    Passive,
}

public enum SkillTriggerType
{
    /// <summary>Random chance (TriggerChance) each player turn.</summary>
    OnPlayerTurn,

    /// <summary>Fires when player HP fraction falls below TriggerHpThreshold.</summary>
    OnLowPlayerHp,

    /// <summary>Fires when enemy HP fraction falls below TriggerHpThreshold.</summary>
    OnLowEnemyHp,
}
