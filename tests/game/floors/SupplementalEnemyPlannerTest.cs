using GdUnit4;
using Godot;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
public partial class SupplementalEnemyPlannerTest
{
    [TestCase]
    public void TestProducesTargetCount()
    {
        // 2 base enemies, multiplier 3 => target = 2*(3-1) = 4 supplemental
        var baseEnemies = new Dictionary<string, EnemySpec>
        {
            ["a"] = new(new Vector2I(0, 0), "goblin"),
            ["b"] = new(new Vector2I(9, 9), "orc"),
        };
        var walkable = new HashSet<Vector2I>();
        for (int y = 1; y < 9; y++)
            for (int x = 1; x < 9; x++)
                walkable.Add(new Vector2I(x, y));
        var occupied = new HashSet<Vector2I> { new(0, 0), new(9, 9) };
        var types = new List<string> { "goblin", "orc", "skeleton_warrior", "forest_spirit" };

        var result = SupplementalEnemyPlanner.Plan("Patrol", baseEnemies, walkable, occupied, types);

        AssertThat(result.Count).IsEqual(4);
        // IDs are 1-based zero-padded to 3 digits
        AssertThat(result.ContainsKey("Patrol_001")).IsTrue();
        AssertThat(result.ContainsKey("Patrol_004")).IsTrue();
        // types cycle by index
        AssertThat(result["Patrol_001"].EnemyType).IsEqual("goblin");
        AssertThat(result["Patrol_002"].EnemyType).IsEqual("orc");
    }

    [TestCase]
    public void TestDeterministicAcrossCalls()
    {
        var baseEnemies = new Dictionary<string, EnemySpec> { ["a"] = new(new Vector2I(1, 1), "goblin") };
        var walkable = new HashSet<Vector2I>();
        for (int y = 1; y < 12; y++)
            for (int x = 1; x < 12; x++)
                walkable.Add(new Vector2I(x, y));
        var occupied = new HashSet<Vector2I> { new(1, 1) };
        var types = new List<string> { "goblin", "orc" };

        var r1 = SupplementalEnemyPlanner.Plan("P", baseEnemies, walkable, occupied, types);
        var r2 = SupplementalEnemyPlanner.Plan("P", baseEnemies, walkable, occupied, types);
        AssertThat(r1.Count).IsEqual(r2.Count);
        foreach (var kv in r1)
            AssertThat(r2[kv.Key].Position).IsEqual(kv.Value.Position);
    }
}
