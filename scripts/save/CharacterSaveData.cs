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
            CurrentHealth = Mathf.Clamp(c.CurrentHealth, 0, c.MaxHealth),
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
        // Validate and sanitize save data to prevent corrupted values
        // Use sensible defaults for invalid values to avoid breaking gameplay
        int maxHealth = this.MaxHealth > 0 ? this.MaxHealth : 100;
        int level = this.Level > 0 ? this.Level : 1;
        int experienceToNext = this.ExperienceToNext > 0 ? this.ExperienceToNext : 100 * level + 10 * (level * level);
        int attack = this.Attack >= 0 ? this.Attack : 20;
        int defense = this.Defense >= 0 ? this.Defense : 10;
        int speed = this.Speed >= 0 ? this.Speed : 15;
        int experience = this.Experience >= 0 ? this.Experience : 0;
        int gold = this.Gold >= 0 ? this.Gold : 0;

        // Clamp CurrentHealth to valid range [0, MaxHealth] before assigning
        int clampedHealth = Mathf.Clamp(this.CurrentHealth, 0, maxHealth);

        var character = new Character
        {
            Name = string.IsNullOrWhiteSpace(this.Name) ? "Hero" : this.Name,
            Level = level,
            MaxHealth = maxHealth,
            CurrentHealth = clampedHealth,
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

        return character;
    }
}
