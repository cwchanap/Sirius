using Godot;

public static class EncounterTables
{
    public static Enemy? CreateEnemyByType(string? enemyType)
    {
        if (string.IsNullOrWhiteSpace(enemyType))
            return null;

        return enemyType.ToLowerInvariant() switch
        {
            EnemyTypeId.Goblin          => Enemy.CreateGoblin(),
            EnemyTypeId.Orc             => Enemy.CreateOrc(),
            EnemyTypeId.SkeletonWarrior => Enemy.CreateSkeletonWarrior(),
            EnemyTypeId.Troll           => Enemy.CreateTroll(),
            EnemyTypeId.Dragon          => Enemy.CreateDragon(),
            EnemyTypeId.ForestSpirit    => Enemy.CreateForestSpirit(),
            EnemyTypeId.CaveSpider      => Enemy.CreateCaveSpider(),
            EnemyTypeId.DesertScorpion  => Enemy.CreateDesertScorpion(),
            EnemyTypeId.SwampWretch     => Enemy.CreateSwampWretch(),
            EnemyTypeId.MountainWyvern  => Enemy.CreateMountainWyvern(),
            EnemyTypeId.DarkMage        => Enemy.CreateDarkMage(),
            EnemyTypeId.DungeonGuardian => Enemy.CreateDungeonGuardian(),
            EnemyTypeId.DemonLord       => Enemy.CreateDemonLord(),
            EnemyTypeId.Boss            => Enemy.CreateBoss(),
            EnemyTypeId.CryptSentinel   => Enemy.CreateCryptSentinel(),
            EnemyTypeId.GraveHexer      => Enemy.CreateGraveHexer(),
            EnemyTypeId.BoneArcher      => Enemy.CreateBoneArcher(),
            EnemyTypeId.IronRevenant    => Enemy.CreateIronRevenant(),
            EnemyTypeId.CursedGargoyle  => Enemy.CreateCursedGargoyle(),
            EnemyTypeId.AbyssAcolyte    => Enemy.CreateAbyssAcolyte(),
            _ => null,
        };
    }

    public static string SelectDungeonEnemyType(float roll)
    {
        float value = Mathf.Clamp(roll, 0f, 0.999999f);

        if (value < 0.12f) return EnemyTypeId.DungeonGuardian;
        if (value < 0.24f) return EnemyTypeId.CryptSentinel;
        if (value < 0.36f) return EnemyTypeId.GraveHexer;
        if (value < 0.50f) return EnemyTypeId.BoneArcher;
        if (value < 0.66f) return EnemyTypeId.IronRevenant;
        if (value < 0.82f) return EnemyTypeId.CursedGargoyle;
        return EnemyTypeId.AbyssAcolyte;
    }
}
