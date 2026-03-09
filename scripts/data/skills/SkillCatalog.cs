using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Factory and registry for all player skills.
///
/// Skills are instantiated once at static-constructor time and cached.
/// Character stores skill IDs (strings); call GetById() to resolve to a Skill object.
///
/// To add a new skill: create a private Create*() method and call Register() in the
/// static constructor. No other files need to change.
/// </summary>
public static class SkillCatalog
{
    private static readonly Dictionary<string, Skill> _registry = new();

    static SkillCatalog()
    {
        Register(CreatePowerStrike());
        Register(CreateHeal());
        Register(CreateFireBolt());
        Register(CreateShieldBash());
        Register(CreateBattleCry());
        Register(CreateCleave());
    }

    // ---- Lookup ------------------------------------------------------------

    /// <summary>Returns the skill with the given ID, or null if not found.</summary>
    public static Skill? GetById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return _registry.TryGetValue(id, out var skill) ? skill : null;
    }

    /// <summary>All registered skills in the catalog.</summary>
    public static IReadOnlyCollection<Skill> AllSkills => _registry.Values;

    // ---- Progression -------------------------------------------------------

    /// <summary>
    /// Learns all catalog skills whose UnlockLevel is at or below the given level,
    /// skipping any already known. Call this from BattleManager after a level-up.
    /// </summary>
    public static void GrantSkillsUpToLevel(Character player, int level)
    {
        if (player == null)
        {
            GD.PushError("[SkillCatalog] GrantSkillsUpToLevel called with null player — this is a programming error.");
            return;
        }

        var sortedSkills = new List<Skill>(_registry.Values);
        sortedSkills.Sort((left, right) =>
        {
            int byUnlockLevel = left.UnlockLevel.CompareTo(right.UnlockLevel);
            return byUnlockLevel != 0
                ? byUnlockLevel
                : string.CompareOrdinal(left.SkillId, right.SkillId);
        });

        foreach (var skill in sortedSkills)
        {
            if (skill.UnlockLevel <= level && !player.KnownSkillIds.Contains(skill.SkillId))
            {
                player.LearnSkill(skill.SkillId);
                AutoEquipLearnedSkill(player, skill);
                GD.Print($"[SkillCatalog] {player.Name} learned '{skill.DisplayName}'!");
            }
        }
    }

    // ---- Private helpers ---------------------------------------------------

    private static void Register(Skill skill)
    {
        if (string.IsNullOrEmpty(skill.SkillId))
            throw new InvalidOperationException($"Skill '{skill.DisplayName}' has an empty SkillId — every skill must have a unique non-empty ID.");
        if (skill.Type == SkillType.Active && skill.ActivePeriod < 1)
            throw new InvalidOperationException($"Skill '{skill.SkillId}' has ActivePeriod={skill.ActivePeriod} — must be >= 1 to avoid division-by-zero.");
        if (_registry.ContainsKey(skill.SkillId))
            throw new InvalidOperationException($"Duplicate SkillId '{skill.SkillId}' — each skill must have a unique ID. Check SkillCatalog factory methods.");
        _registry[skill.SkillId] = skill;
    }

    private static void AutoEquipLearnedSkill(Character player, Skill skill)
    {
        if (skill.Type == SkillType.Active)
        {
            if (string.IsNullOrEmpty(player.ActiveSkillId))
                player.EquipActiveSkill(skill.SkillId);
            else
                GD.Print($"[SkillCatalog] '{skill.DisplayName}' learned but active slot already has '{player.ActiveSkillId}' — not replaced.");
            return;
        }

        bool slotFound = false;
        for (int slot = 0; slot < 3; slot++)
        {
            if (slot >= player.PassiveSkillIds.Count || string.IsNullOrEmpty(player.PassiveSkillIds[slot]))
            {
                player.EquipPassiveSkill(skill.SkillId, slot);
                slotFound = true;
                break;
            }
        }
        if (!slotFound)
            GD.Print($"[SkillCatalog] All passive slots full — '{skill.DisplayName}' learned but not auto-equipped.");
    }

    // ---- Starter skills ----------------------------------------------------

    // 1. Power Strike — Active, fires every 3 turns, 150% ATK physical damage.
    private static Skill CreatePowerStrike() => new Skill
    {
        SkillId = "power_strike",
        DisplayName = "Power Strike",
        Description = "A powerful physical attack. Deals 150% ATK damage every 3 turns.",
        ManaCost = 10,
        UnlockLevel = 1,
        Type = SkillType.Active,
        ActivePeriod = 3,
        Effect = new DamageSkillEffect(1.5f, "physical"),
    };

    // 2. Heal — Passive, triggers when player HP drops below 40%.
    private static Skill CreateHeal() => new Skill
    {
        SkillId = "heal",
        DisplayName = "Heal",
        Description = "Restores 50 HP when health drops below 40%. Cooldown: 5 turns.",
        ManaCost = 15,
        UnlockLevel = 2,
        Type = SkillType.Passive,
        TriggerType = SkillTriggerType.OnLowPlayerHp,
        TriggerHpThreshold = 0.4f,
        PassiveCooldown = 5,
        Effect = new HealSkillEffect(50),
    };

    // 3. Fire Bolt — Active, fires every 3 turns, 120% ATK magical fire damage.
    private static Skill CreateFireBolt() => new Skill
    {
        SkillId = "fire_bolt",
        DisplayName = "Fire Bolt",
        Description = "A magical fire attack. Deals 120% ATK damage every 3 turns.",
        ManaCost = 20,
        UnlockLevel = 3,
        Type = SkillType.Active,
        ActivePeriod = 3,
        Effect = new DamageSkillEffect(1.2f, "magical fire"),
    };

    // 4. Shield Bash — Active, fires every 4 turns, 100% ATK + 50% chance to stun.
    private static Skill CreateShieldBash() => new Skill
    {
        SkillId = "shield_bash",
        DisplayName = "Shield Bash",
        Description = "Bash the enemy for 100% ATK damage with a 50% chance to stun. Fires every 4 turns.",
        ManaCost = 15,
        UnlockLevel = 5,
        Type = SkillType.Active,
        ActivePeriod = 4,
        Effect = new ComboSkillEffect(
            new DamageSkillEffect(1.0f, "shield"),
            new ApplyDebuffSkillEffect(StatusEffectType.Stun, 0, 1, 0.5f)
        ),
    };

    // 5. Battle Cry — Passive, triggers when enemy HP drops below 30%.
    private static Skill CreateBattleCry() => new Skill
    {
        SkillId = "battle_cry",
        DisplayName = "Battle Cry",
        Description = "Boost Attack by 15 for 3 turns when the enemy is near death (HP < 30%). Cooldown: 3 turns.",
        ManaCost = 25,
        UnlockLevel = 7,
        Type = SkillType.Passive,
        TriggerType = SkillTriggerType.OnLowEnemyHp,
        TriggerHpThreshold = 0.3f,
        PassiveCooldown = 3,
        Effect = new ApplyBuffSkillEffect(StatusEffectType.Strength, 15, 3, "ATK"),
    };

    // 6. Cleave — Active, fires every 5 turns, 80% ATK damage.
    private static Skill CreateCleave() => new Skill
    {
        SkillId = "cleave",
        DisplayName = "Cleave",
        Description = "Sweep your weapon in a wide arc. Deals 80% ATK damage every 5 turns.",
        ManaCost = 30,
        UnlockLevel = 10,
        Type = SkillType.Active,
        ActivePeriod = 5,
        Effect = new DamageSkillEffect(0.8f, "cleave"),
    };
}
