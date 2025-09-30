using Godot;

[System.Serializable]
public partial class Character : Resource
{
    [Export] public string Name { get; set; } = "Hero";
    [Export] public int Level { get; set; } = 1;
    [Export] public int MaxHealth { get; set; } = 100;
    [Export] public int CurrentHealth { get; set; } = 100;
    [Export] public int Attack { get; set; } = 20;
    [Export] public int Defense { get; set; } = 10;
    [Export] public int Speed { get; set; } = 15;
    [Export] public int Experience { get; set; } = 0;
    [Export] public int ExperienceToNext { get; set; } = 100;
    [Export] public Inventory Inventory { get; set; } = new Inventory();
    [Export] public EquipmentSet Equipment { get; set; } = new EquipmentSet();

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

    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - GetEffectiveDefense());
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
        GD.Print($"{Name} takes {actualDamage} damage! Health: {CurrentHealth}/{GetEffectiveMaxHealth()}");
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
        CurrentHealth = MaxHealth; // Full heal on level up
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
        return Attack + Equipment.GetAttackBonus();
    }

    public int GetEffectiveDefense()
    {
        EnsureEquipment();
        return Defense + Equipment.GetDefenseBonus();
    }

    public int GetEffectiveSpeed()
    {
        EnsureEquipment();
        return Speed + Equipment.GetSpeedBonus();
    }

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
        }

        return equipped;
    }

    public EquipmentItem Unequip(EquipmentSlotType slot, int accessorySlot = 0)
    {
        EnsureEquipment();
        var removed = Equipment.Unequip(slot, accessorySlot);
        CurrentHealth = Mathf.Min(CurrentHealth, GetEffectiveMaxHealth());
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
