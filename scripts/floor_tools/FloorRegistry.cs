using System.Collections.Generic;

namespace Sirius.FloorTools;

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
}
