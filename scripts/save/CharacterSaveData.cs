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
        // Clamp CurrentHealth to valid range [0, MaxHealth] before assigning
        int clampedHealth = Mathf.Clamp(this.CurrentHealth, 0, this.MaxHealth);

        var character = new Character
        {
            Name = string.IsNullOrWhiteSpace(this.Name) ? "Hero" : this.Name,
            Level = this.Level,
            MaxHealth = this.MaxHealth,
            CurrentHealth = clampedHealth,
            Attack = this.Attack,
            Defense = this.Defense,
            Speed = this.Speed,
            Experience = this.Experience,
            ExperienceToNext = this.ExperienceToNext,
            Gold = this.Gold
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
