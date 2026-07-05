using Godot;
using System.Collections.Generic;

namespace Sirius.FloorTools.Layouts;

public static class Floor0Layout
{
    public const int FloorWidth = 100;   // from floor0_maze_generator.py:14
    public const int FloorHeight = 100;  // :15
    public const int GridWidth = 160;    // :19
    public const int GridHeight = 160;   // :20

    public static readonly Vector2I PlayerStart = new(8, 50);       // :22
    public static readonly Vector2I ShopkeeperPos = new(12, 46);    // :23
    public static readonly Vector2I HealerPos = new(12, 54);        // :24
    public static readonly Vector2I FirstGoblinPos = new(24, 45);   // :25
    public static readonly Vector2I StairPos = new(82, 68);         // :26
    public static readonly Vector2I ReturnSpawnFromFloor1 = new(17, 13); // :28

    // MAIN_LOOP_POINTS — port all 9 points verbatim from :30-40
    public static readonly Vector2I[] MainLoopPoints =
    {
        new(8, 50), new(18, 50), new(18, 18), new(56, 18),
        new(76, 30), new(82, 68), new(52, 82), new(18, 72), new(8, 50),
    };

    // TREASURE_BOXES — port all 8 entries verbatim from :42-51
    public static readonly Dictionary<string, (Vector2I Position, int Gold, Dictionary<string, int> Items)> TreasureBoxes = new()
    {
        ["TreasureBox_GF_EntranceCache"] = (new Vector2I(15, 50), 35, new() { ["health_potion"] = 1 }),
        ["TreasureBox_GF_NorthwestCache"] = (new Vector2I(30, 8), 60, new() { ["mana_potion"] = 1 }),
        ["TreasureBox_GF_NorthLoopCache"] = (new Vector2I(49, 8), 80, new() { ["strength_tonic"] = 1 }),
        ["TreasureBox_GF_EastBranchCache"] = (new Vector2I(91, 30), 110, new() { ["greater_health_potion"] = 1 }),
        ["TreasureBox_GF_StairDistrictCache"] = (new Vector2I(94, 68), 75, new() { ["iron_skin"] = 1 }),
        ["TreasureBox_GF_SouthDeepCache"] = (new Vector2I(52, 94), 0, new() { ["iron_sword"] = 1 }),
        ["TreasureBox_GF_SouthwestCache"] = (new Vector2I(7, 72), 50, new() { ["antidote"] = 2 }),
        ["TreasureBox_GF_SoutheastCache"] = (new Vector2I(80, 82), 0, new() { ["iron_shield"] = 1 }),
    };
}
