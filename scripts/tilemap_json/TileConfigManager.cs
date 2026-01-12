using Godot;
using System.Collections.Generic;
using System.Text.Json;

namespace Sirius.TilemapJson;

/// <summary>
/// Manages bidirectional mapping between human-readable tile names and Godot source IDs.
/// Loads configuration from config/tile_mapping.json.
/// </summary>
[Tool]
[GlobalClass]
public partial class TileConfigManager : RefCounted
{
    private const string DefaultConfigPath = "res://config/tile_mapping.json";

    // Layer type -> (tile name -> TileMapping)
    private Dictionary<string, Dictionary<string, TileMapping>> _nameMappings = new();

    // Layer type -> (source_id -> tile name) for reverse lookup
    private Dictionary<string, Dictionary<int, string>> _idMappings = new();

    private bool _isLoaded = false;

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// Load tile configuration from JSON file.
    /// </summary>
    public Error LoadConfig(string configPath = null)
    {
        configPath ??= DefaultConfigPath;

        if (!FileAccess.FileExists(configPath))
        {
            GD.PrintErr($"[TileConfigManager] Config file not found: {configPath}");
            return Error.FileNotFound;
        }

        using var file = FileAccess.Open(configPath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[TileConfigManager] Failed to open config: {configPath}");
            return Error.CantOpen;
        }

        string json = file.GetAsText();

        try
        {
            var config = JsonSerializer.Deserialize<TileConfigRoot>(json);
            if (config?.TileMappings == null)
            {
                GD.PrintErr("[TileConfigManager] Invalid config format");
                return Error.ParseError;
            }

            _nameMappings.Clear();
            _idMappings.Clear();

            foreach (var (layerType, tiles) in config.TileMappings)
            {
                _nameMappings[layerType] = new Dictionary<string, TileMapping>();
                _idMappings[layerType] = new Dictionary<int, string>();

                foreach (var (tileName, mapping) in tiles)
                {
                    _nameMappings[layerType][tileName] = mapping;
                    // For reverse lookup, use source_id as key
                    // Note: If multiple names map to same source_id, last one wins
                    _idMappings[layerType][mapping.SourceId] = tileName;
                }
            }

            _isLoaded = true;
            GD.Print($"[TileConfigManager] Loaded {_nameMappings.Count} layer types from {configPath}");
            return Error.Ok;
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[TileConfigManager] JSON parse error: {ex.Message}");
            return Error.ParseError;
        }
    }

    /// <summary>
    /// Get tile mapping by layer type and tile name.
    /// </summary>
    public TileMapping GetMapping(string layerType, string tileName)
    {
        if (!_isLoaded)
        {
            GD.PrintErr("[TileConfigManager] Config not loaded, call LoadConfig() first");
            return null;
        }

        if (_nameMappings.TryGetValue(layerType, out var tiles))
        {
            if (tiles.TryGetValue(tileName, out var mapping))
            {
                return mapping;
            }
        }

        GD.PrintErr($"[TileConfigManager] Unknown tile: {layerType}/{tileName}");
        return null;
    }

    /// <summary>
    /// Get tile name by layer type and source ID (reverse lookup).
    /// </summary>
    public string GetTileName(string layerType, int sourceId)
    {
        if (!_isLoaded)
        {
            GD.PrintErr("[TileConfigManager] Config not loaded, call LoadConfig() first");
            return null;
        }

        if (_idMappings.TryGetValue(layerType, out var tiles))
        {
            if (tiles.TryGetValue(sourceId, out var name))
            {
                return name;
            }
        }

        // Fallback: return source_id as string
        return $"source_{sourceId}";
    }

    /// <summary>
    /// Check if a tile name exists for a layer type.
    /// </summary>
    public bool HasTile(string layerType, string tileName)
    {
        if (!_isLoaded) return false;
        return _nameMappings.TryGetValue(layerType, out var tiles) && tiles.ContainsKey(tileName);
    }

    /// <summary>
    /// Get all layer types defined in config.
    /// </summary>
    public IEnumerable<string> GetLayerTypes()
    {
        return _nameMappings.Keys;
    }

    /// <summary>
    /// Get all tile names for a layer type.
    /// </summary>
    public IEnumerable<string> GetTileNames(string layerType)
    {
        if (_nameMappings.TryGetValue(layerType, out var tiles))
        {
            return tiles.Keys;
        }
        return System.Array.Empty<string>();
    }
}

/// <summary>
/// Represents the mapping of a tile name to its TileSet source configuration.
/// </summary>
public class TileMapping
{
    public int SourceId { get; set; }
    public int[] AtlasCoord { get; set; } = new[] { 0, 0 };

    public Vector2I GetAtlasCoord()
    {
        // Validate array bounds to handle malformed JSON deserialization
        if (AtlasCoord == null || AtlasCoord.Length < 2)
        {
            return new Vector2I(0, 0);
        }
        return new Vector2I(AtlasCoord[0], AtlasCoord[1]);
    }
}

// JSON deserialization model
internal class TileConfigRoot
{
    public string SchemaVersion { get; set; }
    public Dictionary<string, Dictionary<string, TileMapping>> TileMappings { get; set; }
    public Dictionary<string, string> TilesetPaths { get; set; }
}
