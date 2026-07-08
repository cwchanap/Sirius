using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

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
            // Floors 1/2/3: destinations mirror the stair positions themselves.
            def.StairsUpDestinations = ToArray(up);
            def.StairsDownDestinations = ToArray(down);
        }
    }

    private static Godot.Collections.Array<Vector2I> ToArray(List<Vector2I> values)
    {
        var arr = new Godot.Collections.Array<Vector2I>();
        foreach (var v in values) arr.Add(v);
        return arr;
    }

    private static Godot.Collections.Array<Vector2I> PreserveOrFallback(
        Godot.Collections.Array<Vector2I> existing, List<Vector2I> fallback)
    {
        if (existing != null && existing.Count > 0)
            return new Godot.Collections.Array<Vector2I>(existing);
        return ToArray(fallback);
    }
}
