using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using TileData = Sirius.TilemapJson.TileData;

namespace Sirius.FloorTools;

/// <param name="SyncMetadata">When true, overwrites FloorName/FloorNumber/
/// FloorDescription on the .tres with the generator's values. Default false —
/// preserves hand-authored .tres metadata so regeneration does not clobber
/// presentation text that may differ from the generator's internal labels.</param>
public record FloorSyncOptions(Vector2I? StairDestOverride = null, bool SyncMetadata = false)
{
    public FloorSyncOptions() : this((Vector2I?)null, false) { }
    public FloorSyncOptions(Vector2I? stairDestOverride) : this(stairDestOverride, false) { }
}

public static class FloorResourceSyncService
{
    public static void Apply(FloorDefinition def, FloorJsonModel model, FloorSyncOptions options)
    {
        // Metadata sync is opt-in: by default the .tres keeps its hand-authored
        // FloorName/FloorDescription. FloorNumber is structural and always synced
        // so a renumbered generator output cannot silently mismatch the .tres.
        def.FloorNumber = model.Metadata.FloorNumber;
        if (options.SyncMetadata)
        {
            def.FloorName = model.Metadata.FloorName;
            def.FloorDescription = model.Metadata.Description;
        }

        def.PlayerStartPosition = model.Metadata.PlayerStart.ToVector2I();

        // Guard model.Entities (not just StairConnections): hand-edited JSON or
        // a partial model may have null Entities, which would NRE on the deref.
        var entities = model.Entities ?? new SceneEntities();
        var stairs = entities.StairConnections ?? new();
        // OrdinalIgnoreCase: all current producers emit lowercase "up"/"down",
        // but hand-edited JSON could use "Up"/"UP". A case-sensitive match would
        // silently drop such stairs from StairsUp/StairsDown on the .tres.
        var up = stairs.Where(s => string.Equals(s.Direction, "up", System.StringComparison.OrdinalIgnoreCase))
                       .Select(s => s.Position.ToVector2I()).ToList();
        var down = stairs.Where(s => string.Equals(s.Direction, "down", System.StringComparison.OrdinalIgnoreCase))
                         .Select(s => s.Position.ToVector2I()).ToList();

        def.StairsUp = ToArray(up);
        def.StairsDown = ToArray(down);

        if (model.Metadata.FloorNumber == 0)
        {
            def.StairsUpDestinations = options.StairDestOverride is { } o
                ? ToArray(new List<Vector2I> { o })
                : PreserveOrFallback(def.StairsUpDestinations, new List<Vector2I> { Floor0Layout.ReturnSpawnFromFloor1 });
            // GF has no down stairs today; reset destinations so a .tres that
            // previously held down-stair destinations (e.g. from an experiment)
            // does not retain stale entries.
            def.StairsDownDestinations = ToArray(new List<Vector2I>());
        }
        else
        {
            // Floors 1/2/3: destinations must be OFF the stair tile. Spawning on
            // a stair would bounce the player back to the previous floor on the
            // next move onto that cell (see PlayerStartOnStair validator and the
            // +1x PlayerStart shift in Floor{1,2,3}Layout). The cross-floor
            // transition path falls back to GetStairDestination when the target
            // floor's stairs are not yet registered (the normal case), so these
            // arrays are the operative spawn coordinates — they cannot be the
            // stair cells themselves. Compute an adjacent walkable, non-stair
            // cell for each stair; prefer +1x to match the PlayerStart convention
            // (PlayerStart = DownStair +1x, so the primary down-stair naturally
            // resolves to PlayerStart).
            var walkable = BuildWalkableSet(model);
            var stairCells = BuildStairSet(model, up, down);
            def.StairsUpDestinations = ToArray(up.Select(s => OffStairSpawn(s, walkable, stairCells)).ToList());
            def.StairsDownDestinations = ToArray(down.Select(s => OffStairSpawn(s, walkable, stairCells)).ToList());
        }
    }

    private static Godot.Collections.Array<Vector2I> ToArray(List<Vector2I> values)
    {
        var arr = new Godot.Collections.Array<Vector2I>();
        foreach (var v in values) arr.Add(v);
        return arr;
    }

    private static HashSet<Vector2I> BuildWalkableSet(FloorJsonModel model)
    {
        var walls = (model.TileLayers.GetValueOrDefault("wall") ?? new List<TileData>())
            .Select(t => new Vector2I(t.X, t.Y)).ToHashSet();
        var ground = model.TileLayers.GetValueOrDefault("ground") ?? new List<TileData>();
        return ground.Select(t => new Vector2I(t.X, t.Y)).Where(c => !walls.Contains(c)).ToHashSet();
    }

    private static HashSet<Vector2I> BuildStairSet(
        FloorJsonModel model, List<Vector2I> up, List<Vector2I> down)
    {
        var stairs = (model.TileLayers.GetValueOrDefault("stair") ?? new List<TileData>())
            .Select(t => new Vector2I(t.X, t.Y)).ToHashSet();
        foreach (var s in up) stairs.Add(s);
        foreach (var s in down) stairs.Add(s);
        return stairs;
    }

    private static Vector2I OffStairSpawn(
        Vector2I stair, HashSet<Vector2I> walkable, HashSet<Vector2I> stairCells)
        => FloorGraph.FindOffStairSpawn(stair, stairCells, walkable.Contains, "FloorResourceSyncService");

    private static Godot.Collections.Array<Vector2I> PreserveOrFallback(
        Godot.Collections.Array<Vector2I> existing, List<Vector2I> fallback)
    {
        if (existing != null && existing.Count > 0)
            return new Godot.Collections.Array<Vector2I>(existing);
        return ToArray(fallback);
    }
}
