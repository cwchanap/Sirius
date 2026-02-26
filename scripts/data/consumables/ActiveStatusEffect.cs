/// <summary>
/// Immutable record representing one active status effect on a combatant.
/// Duration is measured in turns taken by the owning combatant (one of that
/// combatant's actions = one turn for its own effects). StatusEffectSet.Tick()
/// is called after each combatant's action, so this applies equally to players
/// and enemies.
/// Replaces the old ActiveBuff record.
///
/// Magnitude semantics depend on StatusEffectType — see StatusEffectType docs.
/// For Stun and Blind, Magnitude is always 0 (presence alone determines behaviour).
/// </summary>
public record ActiveStatusEffect(
    StatusEffectType Type,
    int              Magnitude,
    int              TurnsRemaining
)
{
    /// <summary>Returns a copy with TurnsRemaining decremented by one.</summary>
    public ActiveStatusEffect Tick() => this with { TurnsRemaining = TurnsRemaining - 1 };

    public bool IsExpired => TurnsRemaining <= 0;

    public bool IsDoT => Type is StatusEffectType.Poison or StatusEffectType.Burn;
    public bool IsHoT => Type == StatusEffectType.Regen;
}
