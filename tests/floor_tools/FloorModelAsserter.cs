using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using static GdUnit4.Assertions;

public static class FloorModelAsserter
{
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void AssertModelsEqual(FloorJsonModel actual, FloorJsonModel expected)
    {
        // Metadata
        AssertThat(actual.Metadata.FloorName).IsEqual(expected.Metadata.FloorName);
        AssertThat(actual.Metadata.FloorNumber).IsEqual(expected.Metadata.FloorNumber);
        AssertThat(actual.Metadata.Description).IsEqual(expected.Metadata.Description);
        AssertThat(actual.Metadata.PlayerStart.X).IsEqual(expected.Metadata.PlayerStart.X);
        AssertThat(actual.Metadata.PlayerStart.Y).IsEqual(expected.Metadata.PlayerStart.Y);

        // Tile layers as multisets
        AssertTileLayer(actual, expected, "ground");
        AssertTileLayer(actual, expected, "wall");
        AssertTileLayer(actual, expected, "stair");

        // Entities as id-keyed maps (canonical JSON per record)
        AssertEntityList(actual.Entities.EnemySpawns, expected.Entities.EnemySpawns);
        AssertEntityList(actual.Entities.NpcSpawns, expected.Entities.NpcSpawns);
        AssertEntityList(actual.Entities.TreasureBoxes, expected.Entities.TreasureBoxes);
        AssertEntityList(actual.Entities.TrapTiles, expected.Entities.TrapTiles);
        AssertEntityList(actual.Entities.PuzzleSwitches, expected.Entities.PuzzleSwitches);
        AssertEntityList(actual.Entities.PuzzleGates, expected.Entities.PuzzleGates);
        AssertEntityList(actual.Entities.PuzzleRiddles, expected.Entities.PuzzleRiddles);
        AssertEntityList(actual.Entities.StairConnections, expected.Entities.StairConnections);
        AssertEntityList(actual.Entities.HiddenPlaceholders, expected.Entities.HiddenPlaceholders);
    }

    private static void AssertTileLayer(FloorJsonModel actual, FloorJsonModel expected, string layer)
    {
        var a = Multiset(actual.TileLayers.GetValueOrDefault(layer));
        var e = Multiset(expected.TileLayers.GetValueOrDefault(layer));
        AssertThat(a.Count).IsEqual(e.Count);
        foreach (var key in e.Keys)
            AssertThat(a.GetValueOrDefault(key)).IsEqual(e.GetValueOrDefault(key));
    }

    private static Dictionary<string, int> Multiset(List<Sirius.TilemapJson.TileData> tiles)
    {
        var dict = new Dictionary<string, int>();
        foreach (var t in tiles ?? new List<Sirius.TilemapJson.TileData>())
        {
            string key = $"{t.X},{t.Y},{t.Tile},{t.Alternative}";
            dict[key] = dict.GetValueOrDefault(key) + 1;
        }
        return dict;
    }

    private static void AssertEntityList<T>(List<T> actual, List<T> expected) where T : class
    {
        var aMap = (actual ?? new List<T>()).ToDictionary(GetId, Canonical);
        var eMap = (expected ?? new List<T>()).ToDictionary(GetId, Canonical);

        AssertThat(aMap.Count).IsEqual(eMap.Count);
        foreach (var id in eMap.Keys)
        {
            AssertThat(aMap.ContainsKey(id)).IsTrue();
            AssertThat(aMap[id]).IsEqual(eMap[id]);
        }
    }

    private static string GetId<T>(T entity) => entity switch
    {
        EnemySpawnData e => e.Id,
        NpcSpawnData n => n.Id,
        TreasureBoxData t => t.Id,
        TrapTileData t => t.Id,
        PuzzleSwitchData s => s.Id,
        PuzzleGateData g => g.Id,
        PuzzleRiddleData r => r.Id,
        StairConnectionData s => s.Id,
        HiddenPlaceholderData h => h.Id,
        _ => Canonical(entity),
    };

    private static string Canonical<T>(T entity) =>
        JsonSerializer.Serialize(entity, CanonicalJson);
}
