using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sirius.TilemapJson;

/// <summary>
/// Root model for LLM-friendly floor JSON representation.
/// Designed for human readability and extensibility.
/// </summary>
public class FloorJsonModel
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("floor_metadata")]
    public FloorMetadata Metadata { get; set; } = new();

    [JsonPropertyName("tile_layers")]
    public Dictionary<string, List<TileData>> TileLayers { get; set; } = new();

    [JsonPropertyName("entities")]
    public SceneEntities Entities { get; set; } = new();

    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    public static FloorJsonModel FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            GD.PrintErr("[FloorJsonModel] Cannot parse null or empty JSON");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FloorJsonModel>(json) ?? new FloorJsonModel();
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[FloorJsonModel] JSON parse error: {ex.Message}");
            return null;
        }
    }
}

public class FloorMetadata
{
    [JsonPropertyName("floor_name")]
    public string FloorName { get; set; } = "";

    [JsonPropertyName("floor_number")]
    public int FloorNumber { get; set; } = 0;

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("player_start")]
    public Vector2IData PlayerStart { get; set; }
}

public class Vector2IData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    public Vector2IData() { }

    public Vector2IData(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Vector2IData(Vector2I v)
    {
        X = v.X;
        Y = v.Y;
    }

    public Vector2I ToVector2I() => new(X, Y);
}

public class TileData
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("tile")]
    public string Tile { get; set; } = "";

    [JsonPropertyName("alt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Alternative { get; set; } = 0;

    public TileData() { }

    public TileData(int x, int y, string tile, int alt = 0)
    {
        X = x;
        Y = y;
        Tile = tile;
        Alternative = alt;
    }
}

public class SceneEntities
{
    [JsonPropertyName("enemy_spawns")]
    public List<EnemySpawnData> EnemySpawns { get; set; } = new();

    [JsonPropertyName("stair_connections")]
    public List<StairConnectionData> StairConnections { get; set; } = new();
}

public class EnemySpawnData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector2IData Position { get; set; } = new();

    [JsonPropertyName("enemy_type")]
    public string EnemyType { get; set; } = "";

    [JsonPropertyName("blueprint")]
    public string Blueprint { get; set; }

    [JsonPropertyName("stats")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EnemyStatsData Stats { get; set; }
}

public class EnemyStatsData
{
    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("max_health")]
    public int MaxHealth { get; set; } = 50;

    [JsonPropertyName("attack")]
    public int Attack { get; set; } = 10;

    [JsonPropertyName("defense")]
    public int Defense { get; set; } = 5;

    [JsonPropertyName("speed")]
    public int Speed { get; set; } = 10;

    [JsonPropertyName("exp_reward")]
    public int ExpReward { get; set; } = 20;

    [JsonPropertyName("gold_reward")]
    public int GoldReward { get; set; } = 10;
}

public class StairConnectionData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("position")]
    public Vector2IData Position { get; set; } = new();

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "up";

    [JsonPropertyName("target_floor")]
    public int TargetFloor { get; set; } = 0;

    [JsonPropertyName("destination_stair_id")]
    public string DestinationStairId { get; set; } = "";
}
