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

    public bool IsAlive => CurrentHealth > 0;

    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - Defense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
        GD.Print($"{Name} takes {actualDamage} damage! Health: {CurrentHealth}/{MaxHealth}");
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        GD.Print($"{Name} heals for {amount}! Health: {CurrentHealth}/{MaxHealth}");
    }

    public void GainExperience(int exp)
    {
        Experience += exp;
        GD.Print($"{Name} gains {exp} experience!");
        
        if (Experience >= ExperienceToNext)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        Level++;
        Experience -= ExperienceToNext;
        ExperienceToNext = (int)(ExperienceToNext * 1.2f);
        
        int healthGain = 15;
        int attackGain = 3;
        int defenseGain = 2;
        int speedGain = 1;
        
        MaxHealth += healthGain;
        CurrentHealth = MaxHealth; // Full heal on level up
        Attack += attackGain;
        Defense += defenseGain;
        Speed += speedGain;
        
        GD.Print($"{Name} levels up to Level {Level}!");
        GD.Print($"Stats increased: +{healthGain} HP, +{attackGain} ATK, +{defenseGain} DEF, +{speedGain} SPD");
    }
}
