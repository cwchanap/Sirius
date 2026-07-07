using Godot;
using System.Collections.Generic;
using System.Linq;

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
}
