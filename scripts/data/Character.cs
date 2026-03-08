using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public partial class Character : Resource
{
    // Battle-scoped status effects. Not exported or persisted; cleared at battle end.
    public StatusEffectSet ActiveBuffs { get; } = new StatusEffectSet();
    [Export] public string Name { get; set; } = "Hero";
    [Export] public int Level { get; set; } = 1;
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public int CurrentHealth { get; set; } = 100;
    [Export] public int Attack { get; set; } = 20;
    [Export] public int Defense { get; set; } = 10;
    [Export] public int Speed { get; set; } = 15;
    [Export] public int Experience { get; set; } = 0;
    [Export] public int ExperienceToNext { get; set; } = 100;
    [Export] public int Gold { get; set; } = 0;
    [Export] public Inventory Inventory { get; set; } = new Inventory();
    [Export] public EquipmentSet Equipment { get; set; } = new EquipmentSet();

    // ---- Mana (persisted; no auto-restore between battles) -----------------

    [Export] public int MaxMana { get; set; } = 50;
    [Export] public int CurrentMana { get; set; } = 50;

    // ---- Skill loadout (persisted as IDs; resolved via SkillCatalog) -------

    /// <summary>ID of the equipped active skill, or null if none.</summary>
    [Export] public string? ActiveSkillId { get; set; }

    /// <summary>IDs of equipped passive skills (up to 3 slots). Not exported; persisted via JSON save system.</summary>
    public List<string> PassiveSkillIds { get; set; } = new();

    /// <summary>IDs of all skills the player has learned. Not exported; persisted via JSON save system.</summary>
    public List<string> KnownSkillIds { get; set; } = new();

    public bool IsAlive => CurrentHealth > 0;

    public bool TryAddItem(Item item, int quantity, out int addedQuantity)
    {
        EnsureInventory();
        return Inventory.TryAddItem(item, quantity, out addedQuantity);
    }

    public bool TryRemoveItem(string itemId, int quantity)
    {
        EnsureInventory();
        return Inventory.TryRemoveItem(itemId, quantity);
    }

    public int GetItemQuantity(string itemId)
    {
        EnsureInventory();
        return Inventory.GetQuantity(itemId);
    }

    public bool HasItem(string itemId)
    {
        EnsureInventory();
        return Inventory.ContainsItem(itemId);
    }

    // ---- Mana operations ---------------------------------------------------

    /// <summary>
    /// Tries to spend the given amount of mana.
    /// Returns true and deducts mana if sufficient; returns false without side effects if not.
    /// </summary>
    public bool TryUseMana(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Mana amount cannot be negative.");
        if (CurrentMana < amount) return false;
        CurrentMana -= amount;
        return true;
    }

    public void RestoreMana(int amount)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Mana amount cannot be negative.");
        CurrentMana = Mathf.Min(MaxMana, CurrentMana + amount);
    }

    // ---- Skill management --------------------------------------------------

    /// <summary>Records a skill as learned by ID. No-op if already known.</summary>
    public void LearnSkill(string skillId)
    {
        if (!string.IsNullOrEmpty(skillId) && !KnownSkillIds.Contains(skillId))
            KnownSkillIds.Add(skillId);
    }

    /// <summary>
    /// Equips a skill as the active skill.
    /// Returns false if the skill is not yet learned or is not an Active-type skill.
    /// </summary>
    public bool EquipActiveSkill(string skillId)
    {
        if (!KnownSkillIds.Contains(skillId)) return false;
        var skill = SkillCatalog.GetById(skillId);
        if (skill == null || skill.Type != SkillType.Active) return false;
        ActiveSkillId = skillId;
        return true;
    }

    /// <summary>
    /// Equips a skill into a passive slot (0–2).
    /// Returns false if the skill is not yet learned, is not a Passive-type skill, or slot is out of range.
    /// </summary>
    public bool EquipPassiveSkill(string skillId, int slot)
    {
        if (!KnownSkillIds.Contains(skillId) || slot < 0 || slot >= 3) return false;
        var skill = SkillCatalog.GetById(skillId);
        if (skill == null || skill.Type != SkillType.Passive) return false;
        while (PassiveSkillIds.Count <= slot)
            PassiveSkillIds.Add("");
        PassiveSkillIds[slot] = skillId;
        return true;
    }

    /// <summary>Resolved active skill object, or null if none is equipped.</summary>
    public Skill? GetActiveSkill() => SkillCatalog.GetById(ActiveSkillId);

    /// <summary>Resolved passive skill objects for all filled passive slots.</summary>
    public IEnumerable<Skill> GetEquippedPassiveSkills()
    {
        foreach (var id in PassiveSkillIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var skill = SkillCatalog.GetById(id);
            if (skill == null)
            {
                GD.PushWarning($"[Character] Equipped passive skill '{id}' not found in SkillCatalog — skipping.");
                continue;
            }
            yield return skill;
        }
    }

    public int TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - GetEffectiveDefense());
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
        GD.Print($"{Name} takes {actualDamage} damage! Health: {CurrentHealth}/{GetEffectiveMaxHealth()}");
        return actualDamage;
    }

    public void Heal(int amount)
    {
        int effectiveMaxHealth = GetEffectiveMaxHealth();
        CurrentHealth = Mathf.Min(effectiveMaxHealth, CurrentHealth + amount);
        GD.Print($"{Name} heals for {amount}! Health: {CurrentHealth}/{effectiveMaxHealth}");
    }

    public void GainExperience(int exp)
    {
        Experience += exp;
        GD.Print($"{Name} gains {exp} experience! ({Experience}/{ExperienceToNext})");
        
        // Check for level up(s) - can potentially level up multiple times
        while (Experience >= ExperienceToNext)
        {
            LevelUp();
        }
    }

    public void GainGold(int amount)
    {
        Gold += amount;
        GD.Print($"{Name} gains {amount} gold! (Total: {Gold})");
    }

    private void LevelUp()
    {
        Experience -= ExperienceToNext;
        Level++;
        
        // Calculate new experience requirement: 100 * level + 10 * level^2
        ExperienceToNext = 100 * Level + 10 * (Level * Level);
        
        int healthGain = 15 + (Level - 1) * 2; // More health per level as you get higher
        int attackGain = 3 + (Level - 1) / 3; // Gradually increase attack gains
        int defenseGain = 2 + (Level - 1) / 4; // Gradually increase defense gains
        int speedGain = 1;
        
        MaxHealth += healthGain;
        CurrentHealth = GetEffectiveMaxHealth(); // Full heal on level up (including equipment bonus)
        Attack += attackGain;
        Defense += defenseGain;
        Speed += speedGain;

        GD.Print($"{Name} levels up to Level {Level}!");
        GD.Print($"Stats Increased: +{healthGain} HP, +{attackGain} ATK, +{defenseGain} DEF, +{speedGain} SPD");
        GD.Print($"Experience required for next level: {ExperienceToNext}");
    }

    public int GetEffectiveAttack()
    {
        EnsureEquipment();
        int flat = Attack + Equipment.GetAttackBonus() + ActiveBuffs.GetAttackFlatBonus();
        return Mathf.Max(1, (int)(flat * ActiveBuffs.GetAttackMultiplier()));
    }

    public int GetEffectiveDefense()
    {
        EnsureEquipment();
        int flatDefense = Defense + Equipment.GetDefenseBonus() + ActiveBuffs.GetDefenseFlatBonus();
        return Mathf.Max(0, flatDefense);
    }

    public int GetEffectiveSpeed()
    {
        EnsureEquipment();
        int flat = Speed + Equipment.GetSpeedBonus() + ActiveBuffs.GetSpeedFlatBonus();
        return Mathf.Max(1, (int)(flat * ActiveBuffs.GetSpeedMultiplier()));
    }

    /// <summary>
    /// Effective accuracy as an integer percentage (0–100).
    /// Returns 55 when Blind is active, 100 otherwise.
    /// </summary>
    public int GetEffectiveAccuracy()
        => Mathf.Clamp((int)(100 * ActiveBuffs.GetAccuracyMultiplier()), 0, 100);

    public int GetEffectiveMaxHealth()
    {
        EnsureEquipment();
        return MaxHealth + Equipment.GetHealthBonus();
    }

    public bool TryEquip(EquipmentItem item, out EquipmentItem replacedItem, int accessorySlot = 0)
    {
        EnsureEquipment();
        replacedItem = null;

        if (item == null)
        {
            return false;
        }

        bool equipped = Equipment.TryEquip(item, out replacedItem, accessorySlot);

        if (equipped)
        {
            CurrentHealth = Mathf.Min(CurrentHealth, GetEffectiveMaxHealth());
            GameManager.Instance?.NotifyPlayerStatsChanged();
        }

        return equipped;
    }

    public EquipmentItem Unequip(EquipmentSlotType slot, int accessorySlot = 0)
    {
        EnsureEquipment();
        var removed = Equipment.Unequip(slot, accessorySlot);
        CurrentHealth = Mathf.Min(CurrentHealth, GetEffectiveMaxHealth());
        if (removed != null)
        {
            GameManager.Instance?.NotifyPlayerStatsChanged();
        }
        return removed;
    }

    private void EnsureInventory()
    {
        if (Inventory == null)
        {
            Inventory = new Inventory();
        }
    }

    private void EnsureEquipment()
    {
        if (Equipment == null)
        {
            Equipment = new EquipmentSet();
        }
    }
}
