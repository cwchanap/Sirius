using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;

public static class Floor1Layout
{
    public const int Width = 60;
    public const int Height = 60;

    // +1 x vs Python FLOOR1_PLAYER_START (8,30): the C# PlayerStartOnStair
    // validator (FloorValidationService) rejects spawning on a stair tile, so
    // the spawn is shifted one cell right of DownStair. Save-load is safe: the
    // .tres PlayerStartPosition matches this and the cell is off-stair.
    public static readonly Vector2I PlayerStart = new(9, 30);
    public static readonly Vector2I DownStair = new(8, 30);
    public static readonly Vector2I UpStairA = new(49, 12);
    public static readonly Vector2I UpStairB = new(48, 48);

    public static readonly Vector2I SouthShortcutEntry = new(19, 54);

    // Gate keys referenced by BuildFloor1Walls — constants so renames surface
    // as compile errors instead of runtime KeyNotFoundException.
    public static class GateKeys
    {
        public const string GoblinBranch = "EnemySpawn_Goblin_Branch";
        public const string OrcCentral = "EnemySpawn_Orc_Central";
        public const string SkeletonStairA = "EnemySpawn_Skeleton_StairA";
        public const string ForestSpiritStairB = "EnemySpawn_ForestSpirit_StairB";
        public const string OrcHiddenBranch = "EnemySpawn_Orc_HiddenBranch";
        public const string SkeletonNorthShortcut = "EnemySpawn_Skeleton_NorthShortcut";
        public const string ForestSpiritEastShortcut = "EnemySpawn_ForestSpirit_EastShortcut";
        public const string OrcSouthShortcut = "EnemySpawn_Orc_SouthShortcut";
    }

    public static readonly Dictionary<string, Vector2I> HiddenPlaceholders = new()
    {
        ["hidden_room_north"] = new Vector2I(16, 8),
        ["hidden_shortcut_east"] = new Vector2I(56, 30),
    };

    public static readonly Dictionary<string, EnemySpec> EnemyGates = new()
    {
        [GateKeys.GoblinBranch] = new(new Vector2I(16, 23), "goblin"),
        [GateKeys.OrcCentral] = new(new Vector2I(22, 30), "orc"),
        [GateKeys.SkeletonStairA] = new(new Vector2I(43, 12), "skeleton_warrior"),
        [GateKeys.ForestSpiritStairB] = new(new Vector2I(42, 48), "forest_spirit"),
        [GateKeys.OrcHiddenBranch] = new(new Vector2I(19, 51), "orc"),
        [GateKeys.SkeletonNorthShortcut] = new(new Vector2I(36, 6), "skeleton_warrior"),
        [GateKeys.ForestSpiritEastShortcut] = new(new Vector2I(54, 56), "forest_spirit"),
        [GateKeys.OrcSouthShortcut] = new(new Vector2I(32, 58), "orc"),
    };

    public static readonly Dictionary<string, EnemySpec> ExtraEnemyPatrols = new()
    {
        ["EnemySpawn_Goblin_WestDeadEnd"] = new(new Vector2I(5, 22), "goblin"),
        ["EnemySpawn_Goblin_SideRoom"] = new(new Vector2I(18, 22), "goblin"),
        ["EnemySpawn_Goblin_SouthwestSpur"] = new(new Vector2I(5, 54), "goblin"),
        ["EnemySpawn_Goblin_WestLoop"] = new(new Vector2I(7, 42), "goblin"),
        ["EnemySpawn_Goblin_NorthRoom"] = new(new Vector2I(8, 4), "goblin"),
        ["EnemySpawn_Goblin_NorthBranch"] = new(new Vector2I(27, 8), "goblin"),
        ["EnemySpawn_Goblin_CentralSouth"] = new(new Vector2I(28, 40), "goblin"),
        ["EnemySpawn_Goblin_SouthLoop"] = new(new Vector2I(23, 58), "goblin"),
        ["EnemySpawn_Goblin_EastSwitchback"] = new(new Vector2I(58, 50), "goblin"),
        ["EnemySpawn_Goblin_EastCorridor"] = new(new Vector2I(56, 34), "goblin"),
        ["EnemySpawn_Goblin_CentralHall"] = new(new Vector2I(12, 28), "goblin"),
        ["EnemySpawn_Orc_WestCrossing"] = new(new Vector2I(13, 37), "orc"),
        ["EnemySpawn_Orc_NorthConnector"] = new(new Vector2I(30, 17), "orc"),
        ["EnemySpawn_Orc_NortheastBend"] = new(new Vector2I(34, 22), "orc"),
        ["EnemySpawn_Orc_EastHall"] = new(new Vector2I(44, 24), "orc"),
        ["EnemySpawn_Orc_EastLoop"] = new(new Vector2I(52, 34), "orc"),
        ["EnemySpawn_Orc_SoutheastSwitchback"] = new(new Vector2I(56, 46), "orc"),
        ["EnemySpawn_Orc_SouthBend"] = new(new Vector2I(35, 54), "orc"),
        ["EnemySpawn_Orc_SouthLoopEast"] = new(new Vector2I(42, 58), "orc"),
        ["EnemySpawn_Orc_CentralLower"] = new(new Vector2I(32, 34), "orc"),
        ["EnemySpawn_Skeleton_NorthDeadEnd"] = new(new Vector2I(49, 5), "skeleton_warrior"),
        ["EnemySpawn_Skeleton_NorthShortcutBend"] = new(new Vector2I(38, 7), "skeleton_warrior"),
        ["EnemySpawn_Skeleton_UpperConnector"] = new(new Vector2I(27, 11), "skeleton_warrior"),
        ["EnemySpawn_Skeleton_EastSpur"] = new(new Vector2I(47, 35), "skeleton_warrior"),
        ["EnemySpawn_Skeleton_CentralSpur"] = new(new Vector2I(38, 39), "skeleton_warrior"),
        ["EnemySpawn_Skeleton_SouthSpur"] = new(new Vector2I(12, 49), "skeleton_warrior"),
        ["EnemySpawn_ForestSpirit_EastSwitchback"] = new(new Vector2I(54, 58), "forest_spirit"),
        ["EnemySpawn_ForestSpirit_SouthGallery"] = new(new Vector2I(39, 44), "forest_spirit"),
    };

    public const string SupplementalPrefix = "EnemySpawn_1F_DensityPatrol";

    public static readonly string[] SupplementalTypes =
    {
        "goblin",
        "orc",
        "skeleton_warrior",
        "forest_spirit",
    };

    public static readonly Dictionary<string, TreasureSpec> TreasureBoxes = new()
    {
        ["TreasureBox_1F_WestDeadEndCache"] = new TreasureSpec(new Vector2I(4, 22), 85, new() { ["health_potion"] = 2 }),
        ["TreasureBox_1F_WestCrossingCache"] = new TreasureSpec(new Vector2I(5, 37), 55, new() { ["health_potion"] = 1 }),
        ["TreasureBox_1F_WestLoopCache"] = new TreasureSpec(new Vector2I(2, 42), 70, new() { ["swiftness_draught"] = 1 }),
        ["TreasureBox_1F_NorthSpurCache"] = new TreasureSpec(new Vector2I(28, 20), 0, new() { ["mana_potion"] = 1 }),
        ["TreasureBox_1F_NorthConnectorCache"] = new TreasureSpec(new Vector2I(30, 19), 0, new() { ["mana_potion"] = 2 }),
        ["TreasureBox_1F_CentralSpurCache"] = new TreasureSpec(new Vector2I(43, 34), 95, new() { ["iron_skin"] = 1 }),
        ["TreasureBox_1F_EastHallCache"] = new TreasureSpec(new Vector2I(52, 24), 120, new() { ["greater_health_potion"] = 1 }),
        ["TreasureBox_1F_NorthStairCache"] = new TreasureSpec(new Vector2I(49, 14), 0, new() { ["iron_boots"] = 1 }),
        ["TreasureBox_1F_EastShortcutCache"] = new TreasureSpec(new Vector2I(58, 46), 0, new() { ["steel_longsword"] = 1 }),
        ["TreasureBox_1F_SouthGalleryCache"] = new TreasureSpec(new Vector2I(38, 55), 130, new() { ["flash_powder"] = 1 }),
        ["TreasureBox_1F_SouthHiddenCache"] = new TreasureSpec(new Vector2I(24, 56), 0, new() { ["chain_mail"] = 1 }),
        ["TreasureBox_1F_SouthShortcutPocket"] = new TreasureSpec(new Vector2I(26, 56), 0, new() { ["antidote"] = 1 }),
    };

    public const string PuzzleId = "Puzzle_1F_SouthShortcutTrial";

    public static readonly Dictionary<string, TrapSpec> PuzzleTraps = new()
    {
        ["TrapTile_1F_SouthTrial_01"] = new TrapSpec(new Vector2I(18, 53), 12, "", 0, 0),
        ["TrapTile_1F_SouthTrial_02"] = new TrapSpec(new Vector2I(17, 54), 12, "", 0, 0),
        ["TrapTile_1F_SouthTrial_03"] = new TrapSpec(new Vector2I(20, 54), 12, "", 0, 0),
        ["TrapTile_1F_SouthTrial_04"] = new TrapSpec(new Vector2I(21, 55), 12, "", 0, 0),
    };

    public static readonly Dictionary<string, SwitchSpec> PuzzleSwitches = new()
    {
        ["PuzzleSwitch_1F_SouthTrial_Lever"] = new SwitchSpec(
            new Vector2I(16, 52),
            "Use",
            "The lever wakes the old shortcut seal."),
    };

    public static readonly Dictionary<string, GateSpec> PuzzleGates = new()
    {
        ["PuzzleGate_1F_SouthTrial_Shortcut"] = new GateSpec(new Vector2I(23, 56), true),
    };

    public static readonly Dictionary<string, RiddleSpec> PuzzleRiddles = new()
    {
        ["PuzzleRiddle_1F_SouthTrial_Seal"] = new RiddleSpec(
            new Vector2I(22, 54),
            "Four stones face the old shortcut. Which stone sleeps until the lever wakes it?",
            new List<PuzzleRiddleChoiceData>
            {
                new() { Id = "north_stone", Label = "North stone" },
                new() { Id = "east_stone", Label = "East stone" },
                new() { Id = "south_stone", Label = "South stone" },
            },
            "east_stone",
            12),
    };

    // ── Wall carving data ──────────────────────────────────────────────
    // Coordinate arrays consumed by BuildFloor1Walls in FloorGenerationService.
    // Moved here from the logic layer so all floor-specific data lives in one
    // place (data/log split, commit 105c53a). The logic layer iterates these
    // arrays and calls MazeBuilder methods.

    public static readonly Vector2I[] MainLoop =
    {
        new(8, 30),
        new(16, 16),
        new(33, 12),
        new(49, 12),
        new(53, 30),
        new(48, 48),
        new(28, 50),
        new(12, 42),
        new(8, 30),
    };

    public static readonly (Vector2I Start, Vector2I End)[] DeadEndBranches =
    {
        (new(11, 22), new(5, 22)),
        (new(28, 26), new(28, 20)),
        (new(32, 34), new(38, 39)),
        (new(49, 9), new(49, 5)),
        (new(53, 35), new(47, 35)),
        (new(56, 30), new(56, 36)),
        (new(28, 50), new(35, 55)),
        (new(7, 42), new(2, 42)),
        (new(12, 49), new(5, 54)),
        (new(38, 12), new(38, 7)),
    };

    public static readonly (char Direction, int Start, int End, int Fixed)[] DecisionConnectors =
    {
        ('h', 5, 14, 37),
        ('v', 31, 41, 12),
        ('h', 11, 15, 28),
        ('h', 19, 38, 8),
        ('h', 17, 33, 11),
        ('v', 8, 15, 28),
        ('h', 49, 56, 34),
        ('v', 31, 45, 52),
        ('h', 49, 53, 32),
        ('v', 31, 35, 50),
    };

    public static readonly Vector2I[][] ShortcutBranches =
    {
        new Vector2I[]
        {
            HiddenPlaceholders["hidden_room_north"],
            new(8, 8),
            new(8, 4),
            new(36, 4),
            new(36, 8),
            new(38, 8),
        },
        new Vector2I[]
        {
            HiddenPlaceholders["hidden_shortcut_east"],
            new(58, 46),
            new(56, 46),
            new(56, 48),
            new(58, 48),
            new(58, 50),
            new(56, 50),
            new(56, 52),
            new(58, 52),
            new(58, 54),
            new(56, 54),
            new(56, 56),
            new(58, 56),
            new(58, 58),
            new(54, 58),
            new(54, 46),
            new(58, 46),
        },
        new Vector2I[]
        {
            SouthShortcutEntry,
            new(23, 58),
            new(23, 56),
            new(58, 56),
            new(58, 58),
            new(42, 58),
            new(23, 58),
        },
    };

    public static readonly (Vector2I Start, Vector2I End)[] WallReliefPaths =
    {
        (new(5, 22), new(4, 22)),
        (new(30, 17), new(30, 19)),
        (new(34, 26), new(34, 22)),
        (new(52, 24), new(44, 24)),
        (new(39, 34), new(43, 34)),
        (new(48, 35), new(44, 35)),
        (new(12, 40), new(28, 40)),
        (new(12, 41), new(28, 41)),
        (new(13, 42), new(28, 42)),
        (new(13, 43), new(28, 43)),
        (new(13, 44), new(28, 44)),
        (new(47, 42), new(39, 42)),
        (new(47, 43), new(39, 43)),
        (new(47, 44), new(39, 44)),
        (new(47, 45), new(39, 45)),
        (new(13, 46), new(28, 46)),
        (new(38, 56), new(38, 55)),
    };

    // Individual wall cells added outside the named arrays above.
    public static readonly Vector2I[] ExtraWalls =
    {
        new(48, 16),
        new(49, 16),
        new(50, 16),
        new(51, 16),
        new(52, 16),
        new(53, 16),
        new(54, 16),
        new(19, 8),
        new(35, 55),
        new(25, 56),
    };
}
