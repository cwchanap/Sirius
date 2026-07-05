using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public record EnemySpec(Vector2I Position, string EnemyType);

public static class SupplementalEnemyPlanner
{
    public const int DensityMultiplier = 3;

    public static Dictionary<string, EnemySpec> Plan(
        string prefix,
        Dictionary<string, EnemySpec> baseEnemies,
        HashSet<Vector2I> walkable,
        HashSet<Vector2I> occupied,
        IReadOnlyList<string> enemyTypes)
    {
        int targetCount = baseEnemies.Count * (DensityMultiplier - 1);
        var localOccupied = new HashSet<Vector2I>(occupied);
        var supplemental = new Dictionary<string, EnemySpec>();
        var selectedPositions = new List<Vector2I>();

        // Deterministic candidate ordering: ((x*73 + y*37) % 997, y, x)
        var candidates = walkable
            .OrderBy(p => ((p.X * 73 + p.Y * 37) % 997, p.Y, p.X))
            .Where(p => !localOccupied.Contains(p) && FloorGraph.WalkableNeighborCount(walkable, p) >= 2)
            .ToList();

        foreach (int minDistance in new[] { 4, 3, 2, 1 })
        {
            foreach (var position in candidates)
            {
                if (supplemental.Count == targetCount)
                    break;
                if (localOccupied.Contains(position))
                    continue;
                bool tooClose = selectedPositions.Any(selected =>
                    System.Math.Abs(position.X - selected.X) + System.Math.Abs(position.Y - selected.Y) < minDistance);
                if (tooClose)
                    continue;

                int index = supplemental.Count + 1;
                string id = $"{prefix}_{index:D3}";
                supplemental[id] = new EnemySpec(position, enemyTypes[(index - 1) % enemyTypes.Count]);
                localOccupied.Add(position);
                selectedPositions.Add(position);
            }
            if (supplemental.Count == targetCount)
                break;
        }

        if (supplemental.Count != targetCount)
            throw new System.Exception(
                $"Could only place {supplemental.Count} supplemental enemies for {prefix}; needed {targetCount}");

        return supplemental;
    }
}
