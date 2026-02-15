using Godot;

/// <summary>
/// DTO for Character stats, inventory, and equipment.
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
            Equipment = EquipmentSaveData.FromEquipmentSet(c.Equipment)
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

        return character;
    }
}
