public sealed record BattleResultSummary(
    bool PlayerWon,
    int ExperienceGained,
    int GoldGained,
    int PreviousLevel,
    int NewLevel,
    LootResult Loot);
