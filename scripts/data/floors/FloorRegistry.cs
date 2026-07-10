using System.Collections.Generic;
using System.IO;

public record FloorPaths(string ScenePath, string DefPath, string JsonPath);

public static class FloorRegistry
{
    private static readonly Dictionary<int, FloorPaths> _floors = new()
    {
        [0] = new FloorPaths(
            "res://scenes/game/floors/FloorGF.tscn",
            "res://resources/floors/FloorGF.tres",
            "res://scenes/game/floors/FloorGF.json"),
        [1] = new FloorPaths(
            "res://scenes/game/floors/Floor1F.tscn",
            "res://resources/floors/Floor1F.tres",
            "res://scenes/game/floors/Floor1F.json"),
        [2] = new FloorPaths(
            "res://scenes/game/floors/Floor2F.tscn",
            "res://resources/floors/Floor2F.tres",
            "res://scenes/game/floors/Floor2F.json"),
        [3] = new FloorPaths(
            "res://scenes/game/floors/Floor3F.tscn",
            "res://resources/floors/Floor3F.tres",
            "res://scenes/game/floors/Floor3F.json"),
    };

    public static IReadOnlyList<int> AllFloors { get; } = new List<int> { 0, 1, 2, 3 };

    public static FloorPaths Get(int floorNumber)
    {
        if (_floors.TryGetValue(floorNumber, out var paths))
            return paths;
        throw new System.ArgumentException($"Unknown floor number: {floorNumber}");
    }

    /// <summary>
    /// Resolves the floor index whose registered scene path matches the given
    /// scene file path. Returns -1 if the path is null/empty or does not match
    /// any registered floor. Used by the editor dock to guard against cross-floor
    /// data corruption: the open scene must match the selected floor before
    /// Export/Import touch files.
    /// </summary>
    /// <remarks>
    /// Compares the full normalized <c>res://</c> path, NOT just the basename.
    /// A basename-only match would treat an unrelated copy of <c>Floor2F.tscn</c>
    /// opened from a scratch/backup directory as floor 2, causing the dock to
    /// Export/Import against the canonical <c>scenes/game/floors/Floor2F.json</c>
    /// with the copy's grid. Path separators are normalized (<c>\</c> → <c>/</c>)
    /// so Windows-style scene paths still match the forward-slash registry paths.
    /// </remarks>
    public static int FindByScenePath(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return -1;
        string normalized = NormalizeResPath(scenePath);
        foreach (var (floor, paths) in _floors)
        {
            if (NormalizeResPath(paths.ScenePath) == normalized)
                return floor;
        }
        return -1;
    }

    private static string NormalizeResPath(string path)
        => path.Replace('\\', '/').TrimEnd('/');
}
