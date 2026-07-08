using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.Collections.Generic;

public static class Floor2Layout
{
    public const int Width = 60;
    public const int Height = 60;

    // +1 x vs Python FLOOR2_PLAYER_START (10,10): the C# PlayerStartOnStair
    // validator (FloorValidationService) rejects spawning on a stair tile, so
    // the spawn is shifted one cell right of DownStairA. Save-load is safe: the
    // .tres PlayerStartPosition matches this and the cell is off-stair.
    public static readonly Vector2I PlayerStart = new(11, 10);
    public static readonly Vector2I DownStairA = new(10, 10);
    public static readonly Vector2I DownStairB = new(26, 10);
    public static readonly Vector2I UpStair = new(52, 50);

    // Gate keys referenced by BuildFloor2Walls — constants so renames surface
    // as compile errors instead of runtime KeyNotFoundException.
    public static class GateKeys
    {
        public const string ArchiveGate = "EnemySpawn_2F_ArchiveGate";
        public const string GalleryGate = "EnemySpawn_2F_GalleryGate";
        public const string UpStairGuard = "EnemySpawn_2F_UpStairGuard";
        public const string PuzzleApproach = "EnemySpawn_2F_PuzzleApproach";
        public const string WestLoop = "EnemySpawn_2F_WestLoop";
        public const string SouthApproach = "EnemySpawn_2F_SouthApproach";
        public const string SouthArmory = "EnemySpawn_2F_SouthArmory";
        public const string ArchiveTrialVault = "PuzzleGate_2F_ArchiveTrial_Vault";
    }

    public static readonly Dictionary<string, EnemySpec> EnemyGates = new()
    {
        [GateKeys.ArchiveGate] = new(new Vector2I(34, 14), "skeleton_warrior"),
        [GateKeys.GalleryGate] = new(new Vector2I(52, 34), "grave_hexer"),
        [GateKeys.UpStairGuard] = new(new Vector2I(49, 50), "crypt_sentinel"),
        [GateKeys.PuzzleApproach] = new(new Vector2I(29, 34), "cave_spider"),
    };

    public static readonly Dictionary<string, EnemySpec> ExtraEnemyPatrols = new()
    {
        ["EnemySpawn_2F_WestSupply"] = new(new Vector2I(8, 16), "cave_spider"),
        [GateKeys.WestLoop] = new(new Vector2I(27, 18), "skeleton_warrior"),
        ["EnemySpawn_2F_NorthStudy"] = new(new Vector2I(44, 12), "grave_hexer"),
        ["EnemySpawn_2F_NorthStacks"] = new(new Vector2I(18, 28), "bone_archer"),
        ["EnemySpawn_2F_PuzzleSide"] = new(new Vector2I(24, 38), "cave_spider"),
        ["EnemySpawn_2F_WestReadingRoom"] = new(new Vector2I(30, 24), "skeleton_warrior"),
        ["EnemySpawn_2F_CentralArchive"] = new(new Vector2I(36, 31), "bone_archer"),
        ["EnemySpawn_2F_EastStacks"] = new(new Vector2I(40, 40), "iron_revenant"),
        ["EnemySpawn_2F_SouthShortcut"] = new(new Vector2I(41, 44), "grave_hexer"),
        ["EnemySpawn_2F_EastDeadEnd"] = new(new Vector2I(44, 34), "cursed_gargoyle"),
        ["EnemySpawn_2F_UpperAlcove"] = new(new Vector2I(48, 24), "bone_archer"),
        ["EnemySpawn_2F_EastGallery"] = new(new Vector2I(55, 34), "iron_revenant"),
        ["EnemySpawn_2F_LowerWatch"] = new(new Vector2I(55, 46), "iron_revenant"),
        [GateKeys.SouthApproach] = new(new Vector2I(24, 46), "cave_spider"),
        [GateKeys.SouthArmory] = new(new Vector2I(42, 53), "iron_revenant"),
        ["EnemySpawn_2F_StairWatch"] = new(new Vector2I(52, 48), "cursed_gargoyle"),
    };

    public const string SupplementalPrefix = "EnemySpawn_2F_DensityPatrol";

    public static readonly string[] SupplementalTypes =
    {
        "cave_spider",
        "skeleton_warrior",
        "grave_hexer",
        "bone_archer",
        "iron_revenant",
        "cursed_gargoyle",
        "crypt_sentinel",
    };

    public static readonly Dictionary<string, TreasureSpec> TreasureBoxes = new()
    {
        ["TreasureBox_2F_WestSupplyCache"] = new TreasureSpec(new Vector2I(6, 16), 100, new() { ["greater_health_potion"] = 1 }),
        ["TreasureBox_2F_WestArchiveCache"] = new TreasureSpec(new Vector2I(4, 32), 120, new() { ["major_health_potion"] = 1 }),
        ["TreasureBox_2F_NorthLandingCache"] = new TreasureSpec(new Vector2I(18, 4), 0, new() { ["major_mana_potion"] = 1 }),
        ["TreasureBox_2F_NorthStudyCache"] = new TreasureSpec(new Vector2I(44, 8), 0, new() { ["major_mana_potion"] = 1 }),
        ["TreasureBox_2F_SouthStacksCache"] = new TreasureSpec(new Vector2I(13, 55), 130, new() { ["warding_charm"] = 1 }),
        ["TreasureBox_2F_EastGalleryCache"] = new TreasureSpec(new Vector2I(56, 36), 140, new() { ["smoke_bomb"] = 1 }),
        ["TreasureBox_2F_EastStudyCache"] = new TreasureSpec(new Vector2I(56, 24), 150, new() { ["smoke_bomb"] = 1 }),
        ["TreasureBox_2F_SouthArmoryCache"] = new TreasureSpec(new Vector2I(42, 55), 0, new() { ["steel_tower_shield"] = 1 }),
        ["TreasureBox_2F_SouthShortcutCache"] = new TreasureSpec(new Vector2I(30, 56), 0, new() { ["swift_boots"] = 1 }),
        ["TreasureBox_2F_StairWatchCache"] = new TreasureSpec(new Vector2I(53, 48), 160, new() { ["swift_boots"] = 1 }),
        ["TreasureBox_2F_PuzzleVaultCache"] = new TreasureSpec(new Vector2I(35, 38), 0, new() { ["warding_charm"] = 1 }),
    };

    public const string PuzzleId = "Puzzle_2F_EastArchiveTrial";

    public static readonly Dictionary<string, TrapSpec> PuzzleTraps = new()
    {
        ["TrapTile_2F_ArchiveTrial_01"] = new TrapSpec(new Vector2I(29, 35), 14, "", 0, 0),
        ["TrapTile_2F_ArchiveTrial_02"] = new TrapSpec(new Vector2I(30, 36), 14, "", 0, 0),
        ["TrapTile_2F_ArchiveTrial_03"] = new TrapSpec(new Vector2I(31, 39), 14, "", 0, 0),
    };

    public static readonly Dictionary<string, SwitchSpec> PuzzleSwitches = new()
    {
        ["PuzzleSwitch_2F_ArchiveTrial_Lever"] = new SwitchSpec(
            new Vector2I(27, 34),
            "Use",
            "The archive lock starts listening."),
    };

    public static readonly Dictionary<string, GateSpec> PuzzleGates = new()
    {
        [GateKeys.ArchiveTrialVault] = new GateSpec(new Vector2I(33, 38), true),
        ["PuzzleGate_2F_ArchiveTrial_Shortcut"] = new GateSpec(new Vector2I(38, 44), true),
    };

    public static readonly Dictionary<string, RiddleSpec> PuzzleRiddles = new()
    {
        ["PuzzleRiddle_2F_ArchiveTrial_Seal"] = new RiddleSpec(
            new Vector2I(32, 36),
            "The archive seal asks: what opens the vault without moving the stones?",
            new List<PuzzleRiddleChoiceData>
            {
                new() { Id = "lever_memory", Label = "The remembered lever" },
                new() { Id = "broken_key", Label = "The broken key" },
                new() { Id = "silent_step", Label = "The silent step" },
            },
            "lever_memory",
            14),
    };

    // ── Wall carving data ──────────────────────────────────────────────
    // Coordinate arrays consumed by BuildFloor2Walls in FloorGenerationService.
    // Moved here from the logic layer so all floor-specific data lives in one
    // place (data/log split, commit 105c53a). The logic layer iterates these
    // arrays and calls MazeBuilder methods.

    public static readonly Vector2I[] MainLoop =
    {
        DownStairA,
        new(18, 14),
        new(34, 14),
        new(48, 20),
        new(52, 34),
        UpStair,
        new(38, 52),
        new(24, 44),
        new(16, 32),
        DownStairA,
    };

    public static readonly (Vector2I Start, Vector2I End)[] SideBranches =
    {
        (new(18, 14), new(18, 6)),
        (new(24, 44), new(16, 52)),
        (new(52, 34), new(56, 28)),
        (new(42, 52), new(34, 56)),
        (new(16, 32), new(7, 32)),
        (new(26, 10), new(26, 5)),
        (new(44, 12), new(50, 18)),
    };

    public static readonly (char Direction, int Start, int End, int Fixed)[] DecisionConnectors =
    {
        ('h', 12, 22, 18),
        ('v', 14, 28, 18),
        ('h', 18, 34, 18),
        ('h', 30, 44, 24),
        ('v', 24, 34, 44),
        ('h', 40, 52, 40),
        ('v', 36, 46, 50),
        ('h', 30, 42, 52),
        ('v', 38, 52, 24),
        ('h', 24, 36, 44),
    };

    public static readonly (Vector2I Start, Vector2I End)[] WallReliefPaths =
    {
        (new(6, 16), new(3, 16)),
        (new(18, 6), new(18, 4)),
        (new(44, 8), new(47, 8)),
        (new(44, 24), new(48, 24)),
        (new(56, 28), new(56, 24)),
        (new(7, 32), new(4, 32)),
        (new(16, 52), new(13, 55)),
        (new(34, 56), new(30, 56)),
        (new(50, 46), new(55, 46)),
    };

    public static readonly (Vector2I Start, Vector2I End)[] ShortcutLoopCuts =
    {
        (new(13, 55), new(30, 56)),
        (new(18, 28), new(24, 38)),
        (new(35, 44), new(41, 44)),
        (new(44, 34), new(48, 24)),
        (new(26, 5), new(18, 4)),
    };

    // Vault wall cells around the puzzle gate (lines 636-645 in the original
    // BuildFloor2Walls). Sealed cells that form the archive trial vault.
    public static readonly Vector2I[] VaultWalls =
    {
        new(33, 33),
        new(34, 33),
        new(35, 33),
        new(36, 33),
        new(33, 34),
        new(34, 34),
        new(34, 35),
        new(34, 36),
        new(35, 34),
        new(35, 35),
        new(35, 36),
        new(35, 37),
        new(34, 37),
        new(36, 37),
    };
}
