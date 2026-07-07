using Godot;
using System.Collections.Generic;

public class MazeBuilder
{
    public int Width { get; }
    public int Height { get; }
    public HashSet<Vector2I> Walls { get; } = new();

    public MazeBuilder(int width, int height)
    {
        Width = width;
        Height = height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Walls.Add(new Vector2I(x, y));
    }

    public void CarveCell(int x, int y)
    {
        if (x >= 1 && x < Width - 1 && y >= 1 && y < Height - 1)
            Walls.Remove(new Vector2I(x, y));
    }

    public void CarveRect(int x1, int y1, int x2, int y2)
    {
        int left = System.Math.Min(x1, x2);
        int right = System.Math.Max(x1, x2);
        int top = System.Math.Min(y1, y2);
        int bottom = System.Math.Max(y1, y2);
        for (int y = top; y <= bottom; y++)
            for (int x = left; x <= right; x++)
                CarveCell(x, y);
    }

    public void CarveHCorridor(int x1, int x2, int y, int halfWidth = 1)
    {
        int left = System.Math.Min(x1, x2);
        int right = System.Math.Max(x1, x2);
        for (int x = left; x <= right; x++)
            for (int dy = -halfWidth; dy <= halfWidth; dy++)
                CarveCell(x, y + dy);
    }

    public void CarveVCorridor(int y1, int y2, int x, int halfWidth = 1)
    {
        int top = System.Math.Min(y1, y2);
        int bottom = System.Math.Max(y1, y2);
        for (int y = top; y <= bottom; y++)
            for (int dx = -halfWidth; dx <= halfWidth; dx++)
                CarveCell(x + dx, y);
    }

    public void CarvePath(Vector2I start, Vector2I end, int halfWidth = 1)
    {
        CarveHCorridor(start.X, end.X, start.Y, halfWidth);
        CarveVCorridor(start.Y, end.Y, end.X, halfWidth);
    }

    public void CarveLoop(IReadOnlyList<Vector2I> points, int halfWidth = 1)
    {
        for (int i = 0; i < points.Count - 1; i++)
            CarvePath(points[i], points[i + 1], halfWidth);
    }

    public void ReinforcePerimeter()
    {
        for (int x = 0; x < Width; x++)
        {
            Walls.Add(new Vector2I(x, 0));
            Walls.Add(new Vector2I(x, Height - 1));
        }
        for (int y = 0; y < Height; y++)
        {
            Walls.Add(new Vector2I(0, y));
            Walls.Add(new Vector2I(Width - 1, y));
        }
    }
}
