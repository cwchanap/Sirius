using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public static class FloorGraph
{
    private static readonly Vector2I[] Directions =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    public static HashSet<Vector2I> WalkableCellsFromWalls(HashSet<Vector2I> walls, int width, int height)
    {
        var walkable = new HashSet<Vector2I>();
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2I(x, y);
                if (!walls.Contains(cell))
                    walkable.Add(cell);
            }
        return walkable;
    }

    public static HashSet<Vector2I> ConnectedCells(HashSet<Vector2I> walkable, Vector2I start)
    {
        var queue = new Queue<Vector2I>();
        queue.Enqueue(start);
        var seen = new HashSet<Vector2I> { start };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var dir in Directions)
            {
                var next = current + dir;
                if (walkable.Contains(next) && !seen.Contains(next))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return seen;
    }

    public static List<Vector2I> WalkableNeighbors(HashSet<Vector2I> walkable, Vector2I position)
    {
        var result = new List<Vector2I>();
        foreach (var dir in Directions)
        {
            var next = position + dir;
            if (walkable.Contains(next))
                result.Add(next);
        }
        return result;
    }

    public static int WalkableNeighborCount(HashSet<Vector2I> walkable, Vector2I position)
        => WalkableNeighbors(walkable, position).Count;

    public static List<List<Vector2I>> DeadEndBranches(HashSet<Vector2I> walkable, int width, int height)
    {
        var branches = new List<List<Vector2I>>();
        var orderedLeaves = walkable.OrderBy(c => c.X).ThenBy(c => c.Y);
        foreach (var leaf in orderedLeaves)
        {
            if (leaf.X >= width || leaf.Y >= height)
                continue;
            if (WalkableNeighborCount(walkable, leaf) != 1)
                continue;

            var branch = new List<Vector2I> { leaf };
            Vector2I? previous = null;
            var current = leaf;
            var visited = new HashSet<Vector2I> { leaf };
            while (true)
            {
                var nextCells = WalkableNeighbors(walkable, current)
                    .Where(n => n != previous && !visited.Contains(n)).ToList();
                if (nextCells.Count == 0)
                    break;
                var nextCell = nextCells[0];
                if (WalkableNeighborCount(walkable, nextCell) != 2)
                    break;
                branch.Add(nextCell);
                visited.Add(nextCell);
                previous = current;
                current = nextCell;
            }
            branches.Add(branch);
        }
        return branches;
    }

    // Search order: +1x first to match the PlayerStart = DownStair +1x
    // convention, then the other cardinal directions. Shared by runtime
    // (GridMap.FindOffStairSpawn) and offline .tres generation
    // (FloorResourceSyncService.OffStairSpawn) so the +1x-first order,
    // wall/stair exclusions, and stair-cell fallback stay identical across
    // both paths — diverging one would desync baked destinations from
    // runtime spawn resolution.
    public static readonly Vector2I[] OffStairOffsets =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    // Build a position → destination lookup from the index-aligned stair and
    // destination arrays on a FloorDefinition (.tres). Used by runtime
    // stair registration (GridMap.RegisterStairConnections) to preserve
    // authored/generated destinations — including the GF
    // StairsUpDestinations return spawn and the --stair-dest CLI override —
    // instead of unconditionally recomputing an off-stair cell and clobbering
    // the .tres value on the shared FloorDefinition resource. Mirrors the
    // preserve branch in FloorResourceSyncService for floor 0.
    public static Dictionary<Vector2I, Vector2I> DestinationIndex(
        Godot.Collections.Array<Vector2I> stairs,
        Godot.Collections.Array<Vector2I> destinations)
    {
        var dict = new Dictionary<Vector2I, Vector2I>();
        for (int i = 0; i < stairs.Count && i < destinations.Count; i++)
            dict[stairs[i]] = destinations[i];
        return dict;
    }

    // isWalkable encapsulates the caller's bounds/grid check (GridMap uses its
    // live _grid array; FloorResourceSyncService uses a derived HashSet) so the
    // shared helper stays agnostic to the walkability representation.
    public static Vector2I FindOffStairSpawn(
        Vector2I stair,
        HashSet<Vector2I> stairCells,
        Func<Vector2I, bool> isWalkable,
        string caller = "FloorGraph")
    {
        foreach (var off in OffStairOffsets)
        {
            var candidate = stair + off;
            if (isWalkable(candidate) && !stairCells.Contains(candidate))
                return candidate;
        }
        // Last resort: no adjacent walkable non-stair cell. Keep the stair
        // position so a destination entry exists, but this will bounce.
        GD.PushWarning(
            $"{caller}: no off-stair walkable cell adjacent to stair {stair}; " +
            "destination will spawn on the stair (bounce risk).");
        return stair;
    }
}
