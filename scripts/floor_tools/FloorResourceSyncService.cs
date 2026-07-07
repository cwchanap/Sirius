using Godot;
using Sirius.FloorTools.Layouts;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public record FloorSyncOptions(Vector2I? StairDestOverride = null)
{
    public FloorSyncOptions() : this((Vector2I?)null) { }
}

public static class FloorResourceSyncService
{
    public static void Apply(FloorDefinition def, FloorJsonModel model, FloorSyncOptions options)
    {
        // Sync metadata so the .tres reflects the generator's authoritative
        // values (FloorResourceSyncService is the only path that updates .tres
        // during floor generation). Without this, the .tres description drifts
        // from the generator and the round-trip parity test fails.
        def.FloorName = model.Metadata.FloorName;
        def.FloorNumber = model.Metadata.FloorNumber;
        def.FloorDescription = model.Metadata.Description;

        def.PlayerStartPosition = model.Metadata.PlayerStart.ToVector2I();

        var stairs = model.Entities.StairConnections ?? new();
        var up = stairs.Where(s => s.Direction == "up").Select(s => s.Position.ToVector2I()).ToList();
        var down = stairs.Where(s => s.Direction == "down").Select(s => s.Position.ToVector2I()).ToList();

        def.StairsUp = ToArray(up);
        def.StairsDown = ToArray(down);

        if (model.Metadata.FloorNumber == 0)
        {
            def.StairsUpDestinations = options.StairDestOverride is { } o
                ? ToArray(new List<Vector2I> { o })
                : PreserveOrFallback(def.StairsUpDestinations, new List<Vector2I> { Floor0Layout.ReturnSpawnFromFloor1 });
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
