/// <summary>
/// All status effect types. Replaces the old BuffType enum.
/// Integer values are stable — do not renumber existing entries.
///
/// Magnitude semantics by category:
///   Flat buffs  (Strength, Fortify, Haste) : flat stat addition
///   Percent debuffs (Weaken, Slow)         : percent reduction (e.g. 25 = 25%)
///   Blind                                  : Magnitude unused; AccuracyMultiplier hardcodes 0.55
///   Stun                                   : Magnitude unused; presence skips the turn
///   DoT (Poison, Burn)                     : flat HP damage per turn
///   HoT (Regen)                            : flat HP heal per turn
/// </summary>
public enum StatusEffectType
{
    // Debuffs (values 0–5)
    Poison   = 0,   // DoT: damage per turn, bypasses defense
    Burn     = 1,   // DoT: damage per turn (fire flavor), bypasses defense
    Stun     = 2,   // Skip the next action; Magnitude unused
    Weaken   = 3,   // Reduce Attack by Magnitude%
    Slow     = 4,   // Reduce Speed by Magnitude%
    Blind    = 5,   // Reduce accuracy to 55%; Magnitude unused

    // Buffs (values 10–14)
    Regen    = 10,  // HoT: flat HP heal per turn
    Haste    = 12,  // Flat Speed bonus (replaces BuffType.SpeedUp)
    Strength = 13,  // Flat Attack bonus (replaces BuffType.AttackUp)
    Fortify  = 14,  // Flat Defense bonus (replaces BuffType.DefenseUp)
}
