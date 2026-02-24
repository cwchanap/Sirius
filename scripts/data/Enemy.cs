using Godot;

[System.Serializable]
public partial class Enemy : Resource
{
    [Export] public string Name { get; set; } = "Goblin";
    [Export] public string EnemyType { get; set; } = EnemyTypeId.Goblin;
    [Export] public int Level { get; set; } = 1;
    [Export] public int MaxHealth { get; set; } = 50;
    [Export] public int CurrentHealth { get; set; } = 50;
    [Export] public int Attack { get; set; } = 15;
    [Export] public int Defense { get; set; } = 5;
    [Export] public int Speed { get; set; } = 10;
    [Export] public int ExperienceReward { get; set; } = 25;
    [Export] public int GoldReward { get; set; } = 10;

    // Battle-scoped status effects. Not exported or persisted; cleared at battle end.
    public StatusEffectSet ActiveStatusEffects { get; } = new StatusEffectSet();

    public bool IsAlive => CurrentHealth > 0;

    public int GetEffectiveAttack()
    {
        int flat = Attack + ActiveStatusEffects.GetAttackFlatBonus();
        return Mathf.Max(1, (int)(flat * ActiveStatusEffects.GetAttackMultiplier()));
    }

    public int GetEffectiveDefense()
        => Mathf.Max(0, Defense + ActiveStatusEffects.GetDefenseFlatBonus());

    public int GetEffectiveSpeed()
    {
        int flat = Speed + ActiveStatusEffects.GetSpeedFlatBonus();
        return Mathf.Max(1, (int)(flat * ActiveStatusEffects.GetSpeedMultiplier()));
    }

    public void TakeDamage(int damage)
    {
        int actualDamage = Mathf.Max(1, damage - Defense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - actualDamage);
        GD.Print($"{Name} takes {actualDamage} damage! Health: {CurrentHealth}/{MaxHealth}");
    }

    public static Enemy CreateGoblin()          => EnemyBlueprint.CreateGoblinBlueprint().CreateEnemy();
    public static Enemy CreateOrc()             => EnemyBlueprint.CreateOrcBlueprint().CreateEnemy();
    public static Enemy CreateDragon()          => EnemyBlueprint.CreateDragonBlueprint().CreateEnemy();
    public static Enemy CreateSkeletonWarrior() => EnemyBlueprint.CreateSkeletonWarriorBlueprint().CreateEnemy();
    public static Enemy CreateTroll()           => EnemyBlueprint.CreateTrollBlueprint().CreateEnemy();
    public static Enemy CreateDarkMage()        => EnemyBlueprint.CreateDarkMageBlueprint().CreateEnemy();
    public static Enemy CreateDemonLord()       => EnemyBlueprint.CreateDemonLordBlueprint().CreateEnemy();
    public static Enemy CreateBoss()            => EnemyBlueprint.CreateBossBlueprint().CreateEnemy();
    public static Enemy CreateForestSpirit()    => EnemyBlueprint.CreateForestSpiritBlueprint().CreateEnemy();
    public static Enemy CreateCaveSpider()      => EnemyBlueprint.CreateCaveSpiderBlueprint().CreateEnemy();
    public static Enemy CreateDesertScorpion()  => EnemyBlueprint.CreateDesertScorpionBlueprint().CreateEnemy();
    public static Enemy CreateSwampWretch()     => EnemyBlueprint.CreateSwampWretchBlueprint().CreateEnemy();
    public static Enemy CreateMountainWyvern()  => EnemyBlueprint.CreateMountainWyvernBlueprint().CreateEnemy();
    public static Enemy CreateDungeonGuardian() => EnemyBlueprint.CreateDungeonGuardianBlueprint().CreateEnemy();
}
