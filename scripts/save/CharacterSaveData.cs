using Godot;
using System.Collections.Generic;

/// <summary>
/// DTO for Character stats, inventory, equipment, mana, and skill loadout.
/// </summary>
public class CharacterSaveData
{
    public string? Name { get; set; }
    public int Level { get; set; }
    public int MaxHealth { get; set; }
    public int CurrentHealth { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public int Experience { get; set; }
    public int ExperienceToNext { get; set; }
    public int Gold { get; set; }
    public InventorySaveData? Inventory { get; set; }
    public EquipmentSaveData? Equipment { get; set; }

    // Mana (persists across battles; no auto-restore)
    public int? MaxMana { get; set; }
    public int? CurrentMana { get; set; }

    // Skill IDs (skills are resolved via SkillCatalog at runtime)
    public string? ActiveSkillId { get; set; }
    public List<string>? PassiveSkillIds { get; set; }
    public List<string>? KnownSkillIds { get; set; }

    public static CharacterSaveData? FromCharacter(Character? c)
    {
        if (c == null) return null;

        return new CharacterSaveData
        {
            Name = c.Name,
            Level = c.Level,
            MaxHealth = c.MaxHealth,
            CurrentHealth = Mathf.Clamp(c.CurrentHealth, 0, c.GetEffectiveMaxHealth()),
            Attack = c.Attack,
            Defense = c.Defense,
            Speed = c.Speed,
            Experience = c.Experience,
            ExperienceToNext = c.ExperienceToNext,
            Gold = c.Gold,
            Inventory = InventorySaveData.FromInventory(c.Inventory),
            Equipment = EquipmentSaveData.FromEquipmentSet(c.Equipment),
            MaxMana = c.MaxMana,
            CurrentMana = c.CurrentMana,
            ActiveSkillId = c.ActiveSkillId,
            PassiveSkillIds = c.PassiveSkillIds != null ? new List<string>(c.PassiveSkillIds) : null,
            KnownSkillIds = c.KnownSkillIds != null ? new List<string>(c.KnownSkillIds) : null,
        };
    }

    public Character ToCharacter()
    {
        // Validate and sanitize save data to prevent corrupted values.
        // Each replaced field is logged so data corruption is diagnosable.
        bool hadInvalidData = false;

        int maxHealth = this.MaxHealth;
        if (maxHealth <= 0)
        {
            GD.PushWarning($"Save data: Invalid MaxHealth ({this.MaxHealth}), using default 100");
            maxHealth = 100;
            hadInvalidData = true;
        }

        int level = this.Level;
        if (level <= 0)
        {
            GD.PushWarning($"Save data: Invalid Level ({this.Level}), using default 1");
            level = 1;
            hadInvalidData = true;
        }

        // Match Character.LevelUp() formula: 100 * level + 10 * level^2
        int experienceToNext = this.ExperienceToNext;
        if (experienceToNext <= 0)
        {
            experienceToNext = 100 * level + 10 * (level * level);
            GD.PushWarning($"Save data: Invalid ExperienceToNext ({this.ExperienceToNext}), using calculated {experienceToNext}");
            hadInvalidData = true;
        }

        int attack = this.Attack;
        if (attack < 0) { GD.PushWarning($"Save data: Invalid Attack ({this.Attack}), using default 20"); attack = 20; hadInvalidData = true; }

        int defense = this.Defense;
        if (defense < 0) { GD.PushWarning($"Save data: Invalid Defense ({this.Defense}), using default 10"); defense = 10; hadInvalidData = true; }

        int speed = this.Speed;
        if (speed < 0) { GD.PushWarning($"Save data: Invalid Speed ({this.Speed}), using default 15"); speed = 15; hadInvalidData = true; }

        int experience = this.Experience;
        if (experience < 0) { GD.PushWarning($"Save data: Invalid Experience ({this.Experience}), using 0"); experience = 0; hadInvalidData = true; }

        int gold = this.Gold;
        if (gold < 0) { GD.PushWarning($"Save data: Invalid Gold ({this.Gold}), using 0"); gold = 0; hadInvalidData = true; }

        int currentHealth = this.CurrentHealth;
        if (currentHealth < 0) { GD.PushWarning($"Save data: Invalid CurrentHealth ({this.CurrentHealth}), using 0"); currentHealth = 0; hadInvalidData = true; }

        string name = this.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            GD.PushWarning($"Save data: Invalid Name (null/empty), using default 'Hero'");
            name = "Hero";
            hadInvalidData = true;
        }

        if (hadInvalidData)
        {
            GD.PushWarning("Save data contained invalid values that were replaced with defaults. " +
                           "Character stats may not match what was originally saved.");
        }

        // Defer final health clamp until after equipment restore so +MaxHealth bonuses are respected.
        int sanitizedHealth = Mathf.Max(0, currentHealth);

        var character = new Character
        {
            Name = name,
            Level = level,
            MaxHealth = maxHealth,
            CurrentHealth = sanitizedHealth,
            Attack = attack,
            Defense = defense,
            Speed = speed,
            Experience = experience,
            ExperienceToNext = experienceToNext,
            Gold = gold
        };

        // Restore inventory
        if (this.Inventory != null)
        {
            character.Inventory = this.Inventory.ToInventory();
        }
        else
        {
            character.Inventory = new Inventory();
        }

        // Restore equipment
        if (this.Equipment != null)
        {
            character.Equipment = this.Equipment.ToEquipmentSet();
        }
        else
        {
            character.Equipment = new EquipmentSet();
        }

        // Clamp CurrentHealth after equipment is restored so effective max HP is used.
        character.CurrentHealth = Mathf.Clamp(character.CurrentHealth, 0, character.GetEffectiveMaxHealth());

        // Restore mana. Fall back to default (50) for saves written before mana was added.
        int maxMana = this.MaxMana.HasValue && this.MaxMana.Value > 0 ? this.MaxMana.Value : 50;
        int currentMana = this.CurrentMana ?? maxMana;
        if (currentMana < 0 || currentMana > maxMana)
        {
            GD.PushWarning($"Save data: Invalid CurrentMana ({currentMana}) for MaxMana {maxMana} — clamping.");
            hadInvalidData = true;
        }
        character.MaxMana = maxMana;
        character.CurrentMana = Mathf.Clamp(currentMana, 0, maxMana);

        // Restore skill loadout. First filter known skills, then validate equipped skills against them.
        bool isLegacySkillSave = IsLegacySkillSave();
        character.KnownSkillIds = FilterValidSkillIds(this.KnownSkillIds, character.Level);

        // Validate equipped skills: must be known and have correct type for the slot
        character.ActiveSkillId = FilterValidActiveSkillId(this.ActiveSkillId, character.KnownSkillIds);
        character.PassiveSkillIds = FilterValidPassiveSkillIds(this.PassiveSkillIds, character.KnownSkillIds);

        if (isLegacySkillSave)
        {
            GD.Print($"[CharacterSaveData] Detected legacy save format (pre-skill-system) for '{character.Name}' — backfilling skills up to level {character.Level}.");
            SkillCatalog.GrantSkillsUpToLevel(character, character.Level);
        }

        return character;
    }

    private bool IsLegacySkillSave()
    {
        return ActiveSkillId == null && PassiveSkillIds == null && KnownSkillIds == null;
    }

    private static List<string> FilterValidSkillIds(List<string>? skillIds, int characterLevel)
    {
        var validSkillIds = new List<string>();
        var seenSkillIds = new HashSet<string>();
        if (skillIds == null)
            return validSkillIds;

        foreach (var skillId in skillIds)
        {
            var skill = SkillCatalog.GetById(skillId);
            if (skill == null)
            {
                GD.PushWarning($"Save data: Known skill '{skillId}' not found in SkillCatalog — dropping (possibly a removed or renamed skill).");
                continue;
            }

            if (skill.UnlockLevel > characterLevel)
            {
                GD.PushWarning($"Save data: Known skill '{skillId}' requires level {skill.UnlockLevel}, but character is level {characterLevel}; ignoring to prevent progression bypass");
                continue;
            }

            if (!seenSkillIds.Add(skillId))
            {
                GD.PushWarning($"Save data: Duplicate known skill '{skillId}' encountered; ignoring duplicate entry");
                continue;
            }

            validSkillIds.Add(skillId);
        }

        return validSkillIds;
    }

    /// <summary>
    /// Filters an active skill ID to ensure it exists, is learned, and is an active skill type.
    /// Returns null if the skill is invalid, not learned, or is a passive skill.
    /// </summary>
    private static string? FilterValidActiveSkillId(string? skillId, List<string> knownSkillIds)
    {
        if (string.IsNullOrEmpty(skillId))
            return null;

        var skill = SkillCatalog.GetById(skillId);
        if (skill == null)
        {
            GD.PushWarning($"Save data: Active skill '{skillId}' not found in skill catalog");
            return null;
        }

        if (!knownSkillIds.Contains(skillId))
        {
            GD.PushWarning($"Save data: Active skill '{skillId}' not in known skills; ignoring to prevent progression bypass");
            return null;
        }

        if (skill.Type != SkillType.Active)
        {
            GD.PushWarning($"Save data: Active skill slot contains passive skill '{skillId}'; ignoring mismatched skill type");
            return null;
        }

        return skillId;
    }

    /// <summary>
    /// Filters passive skill IDs to ensure each exists, is learned, and is a passive skill type.
    /// Returns a list containing only valid passive skills.
    /// </summary>
    private static List<string> FilterValidPassiveSkillIds(List<string>? skillIds, List<string> knownSkillIds)
    {
        var validSkillIds = new List<string>();
        var seenSkillIds = new HashSet<string>();
        if (skillIds == null)
            return validSkillIds;

        foreach (var skillId in skillIds)
        {
            if (validSkillIds.Count >= 3)
            {
                GD.PushWarning("Save data: Passive skill loadout exceeded 3 supported slots; ignoring extra passive skills");
                break;
            }

            if (string.IsNullOrEmpty(skillId))
                continue;

            var skill = SkillCatalog.GetById(skillId);
            if (skill == null)
            {
                GD.PushWarning($"Save data: Passive skill '{skillId}' not found in skill catalog");
                continue;
            }

            if (!knownSkillIds.Contains(skillId))
            {
                GD.PushWarning($"Save data: Passive skill '{skillId}' not in known skills; ignoring to prevent progression bypass");
                continue;
            }

            if (skill.Type != SkillType.Passive)
            {
                GD.PushWarning($"Save data: Passive skill slot contains active skill '{skillId}'; ignoring mismatched skill type");
                continue;
            }

            if (!seenSkillIds.Add(skillId))
            {
                GD.PushWarning($"Save data: Duplicate passive skill '{skillId}' in loadout; ignoring duplicate entry");
                continue;
            }

            validSkillIds.Add(skillId);
        }

        return validSkillIds;
    }
}
