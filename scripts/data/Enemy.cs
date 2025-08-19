using Godot;

[System.Serializable]
public partial class Enemy : Resource
{
    [Export] public string Name { get; set; } = "Goblin";
    [Export] public int Level { get; set; } = 1;
    [Export] public int MaxHealth { get; set; } = 50;
    [Export] public int CurrentHealth { get; set; } = 50;
    [Export] public int Attack { get; set; } = 15;
    [Export] public int Defense { get; set; } = 5;
    [Export] public int Speed { get; set; } = 10;
    [Export] public int ExperienceReward { get; set; } = 25;

    public bool IsAlive => CurrentHealth > 0;

    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - Defense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
        GD.Print($"{Name} takes {actualDamage} damage! Health: {CurrentHealth}/{MaxHealth}");
    }

    public static Enemy CreateGoblin()
    {
        return new Enemy
        {
            Name = "Goblin",
            Level = 1,
            MaxHealth = 50,
            CurrentHealth = 50,
            Attack = 15,
            Defense = 5,
            Speed = 10,
            ExperienceReward = 25
        };
    }

    public static Enemy CreateOrc()
    {
        return new Enemy
        {
            Name = "Orc",
            Level = 2,
            MaxHealth = 80,
            CurrentHealth = 80,
            Attack = 22,
            Defense = 8,
            Speed = 8,
            ExperienceReward = 40
        };
    }

    public static Enemy CreateDragon()
    {
        return new Enemy
        {
            Name = "Dragon",
            Level = 5,
            MaxHealth = 200,
            CurrentHealth = 200,
            Attack = 45,
            Defense = 20,
            Speed = 12,
            ExperienceReward = 150
        };
    }
}
