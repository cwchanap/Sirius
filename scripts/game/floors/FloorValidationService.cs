using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;
using TileData = Sirius.TilemapJson.TileData;

namespace Sirius.FloorTools;

public static class FloorValidationService
{
    // width/height are forwarded to FloorGraph.DeadEndBranches as a bounds filter
    // (cells with X>=width or Y>=height are skipped). They are not stale: the walkable
    // set is derived from model ground tiles, but the bounds filter guards against
    // any out-of-range cells slipping through and keeps DeadEndBranches deterministic.
    public static ValidationResult Validate(FloorJsonModel model, int width, int height)
    {
        var result = new ValidationResult();
        // Entities defaults to new() on the C# model but is null when deserialized
        // from JSON lacking an "entities" key (TilemapJsonImporter also guards this).
        // EntityGroups below dereferences model.Entities unconditionally.
        model.Entities ??= new SceneEntities();

        // Footprint check: TilemapJsonImporter.ConfigureGridMapBounds derives
        // GridWidth/GridHeight from the max ground-tile coordinate (maxX+1,
        // maxY+1). If any tile or entity falls outside [0,width) x [0,height)
        // or has a negative coordinate, the importer silently shrinks/expands
        // the grid or includes negative cells, producing a malformed GridMap.
        // Reject before the rest of validation can pass on a wrong-footprint model.
        ValidateFootprint(model, width, height, result);

        var walls = (model.TileLayers.GetValueOrDefault("wall") ?? new List<TileData>())
            .Select(t => new Vector2I(t.X, t.Y)).ToHashSet();
        var ground = model.TileLayers.GetValueOrDefault("ground") ?? new List<TileData>();
        var walkable = ground.Select(t => new Vector2I(t.X, t.Y)).Where(c => !walls.Contains(c)).ToHashSet();

        // PlayerStart is a nullable reference (Vector2IData has no default
        // initializer); a JSON model lacking "player_start" leaves it null.
        // Dereferencing it below would NRE. Report a clear validation error and
        // stop — the remaining checks all depend on a valid start coordinate.
        if (model.Metadata?.PlayerStart == null)
        {
            result.Error("MissingPlayerStart", "Floor metadata is missing player_start");
            return result;
        }
        var start = model.Metadata.PlayerStart.ToVector2I();

        if (!walkable.Contains(start))
            result.Error("PlayerStartNotWalkable", $"Player start {start} is not walkable");

        // PlayerStart must not coincide with a stair tile — spawning on a stair
        // would immediately trigger a floor transition.
        var stairs = model.TileLayers.GetValueOrDefault("stair") ?? new List<TileData>();
        if (stairs.Any(t => t.X == start.X && t.Y == start.Y))
            result.Error("PlayerStartOnStair", $"Player start {start} coincides with a stair tile");

        var connected = FloorGraph.ConnectedCells(walkable, start);
        var disconnected = walkable.Except(connected).ToList();
        if (disconnected.Count > 0)
            result.Error("DisconnectedCells", $"Disconnected walkable cells: {Format(disconnected.Take(5))}");

        // Entity id/overlap/walkable/reachable checks.
        // EntityGroups is materialized once here and reused for the dead-end
        // payoff check below — re-enumerating it would re-run View() and
        // duplicate UnrecognizedEntityType warnings for the same entities.
        var entityGroups = EntityGroups(model, result).ToList();
        var seenIds = new Dictionary<string, string>();
        var occupied = new Dictionary<Vector2I, string>();
        var goals = new List<Vector2I>();
        // Payoff positions for the dead-end check: every entity type except
        // npc_spawns (NPCs are not a "reward" that justifies a dead-end branch).
        var payoff = new HashSet<Vector2I>();
        foreach (var (key, entities) in entityGroups)
        {
            foreach (var e in entities)
            {
                string id = e.Id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    result.Error("EmptyEntityId", $"Entity in {key} has empty id");
                    continue;
                }
                if (seenIds.TryGetValue(id, out var prev))
                    result.Error("DuplicateEntityId", $"Duplicate entity id '{id}' in {key} and {prev}");
                seenIds[id] = key;

                var pos = e.Position;
                if (occupied.TryGetValue(pos, out var occupier))
                    result.Error("EntityOverlap", $"Entity position {pos} overlaps {key} and {occupier}");
                occupied[pos] = key;
                goals.Add(pos);
                if (key != "npc_spawns")
                    payoff.Add(pos);

                // Invalid puzzle identity (non-treasure, non-stair puzzle-bearing entities)
                if (IsPuzzleEntity(key) && string.IsNullOrWhiteSpace(e.PuzzleId))
                    result.Error("InvalidPuzzleIdentity", $"{key} '{id}' has empty puzzle_id");

                // Invalid treasure reward
                if (e is TreasureDataBoxed tb)
                    ValidateTreasure(tb, id, result);
            }
        }

        foreach (var goal in goals)
        {
            if (!walkable.Contains(goal))
                result.Error("EntityNotWalkable", $"Entity position {goal} is not walkable");
            else if (!connected.Contains(goal))
                result.Error("EntityUnreachable", $"No path from {start} to {goal}");
        }

        // Closed puzzle gate blocking required route (stairs + hidden placeholders)
        ValidateClosedGates(model, walkable, start, result);

        // Unrewarded dead-end branches (floor 1, 2)
        if (model.Metadata.FloorNumber is 1 or 2)
            ValidateDeadEnds(walkable, width, height, payoff, result);

        return result;
    }

    private static IEnumerable<(string Key, List<EntityView> Entities)> EntityGroups(FloorJsonModel model, ValidationResult result)
    {
        yield return ("enemy_spawns", View(model.Entities.EnemySpawns, result));
        yield return ("npc_spawns", View(model.Entities.NpcSpawns, result));
        yield return ("stair_connections", View(model.Entities.StairConnections, result));
        yield return ("hidden_placeholders", View(model.Entities.HiddenPlaceholders, result));
        yield return ("treasure_boxes", View(model.Entities.TreasureBoxes, result));
        yield return ("trap_tiles", View(model.Entities.TrapTiles, result));
        yield return ("puzzle_switches", View(model.Entities.PuzzleSwitches, result));
        yield return ("puzzle_gates", View(model.Entities.PuzzleGates, result));
        yield return ("puzzle_riddles", View(model.Entities.PuzzleRiddles, result));
    }

    private static bool IsPuzzleEntity(string key)
        => key is "trap_tiles" or "puzzle_switches" or "puzzle_gates" or "puzzle_riddles";

    private static void ValidateTreasure(TreasureDataBoxed box, string id, ValidationResult result)
    {
        foreach (var item in box.Items ?? new())
        {
            if (item.Quantity <= 0 || !ItemCatalog.ItemExists(item.ItemId))
                result.Error("InvalidTreasureReward", $"Treasure '{id}' has invalid reward item '{item.ItemId}' x{item.Quantity}");
        }
    }

    private static void ValidateClosedGates(FloorJsonModel model, HashSet<Vector2I> walkable, Vector2I start, ValidationResult result)
    {
        var closedGates = (model.Entities.PuzzleGates ?? new())
            .Where(g => g.StartsClosed)
            .Select(g => g.Position.ToVector2I()).ToHashSet();
        if (closedGates.Count == 0) return;

        var gateWalkable = new HashSet<Vector2I>(walkable);
        foreach (var g in closedGates) gateWalkable.Remove(g);

        if (!gateWalkable.Contains(start))
        {
            result.Error("ClosedGateBlocksStart", $"Player start {start} is blocked by a closed puzzle gate");
            return;
        }
        var gateConnected = FloorGraph.ConnectedCells(gateWalkable, start);
        foreach (var stair in model.Entities.StairConnections ?? new())
        {
            var pos = stair.Position.ToVector2I();
            if (!gateConnected.Contains(pos))
                result.Error("ClosedGateBlocksRoute", $"Required stair {stair.Id} is blocked by a closed puzzle gate");
        }
        foreach (var ph in model.Entities.HiddenPlaceholders ?? new())
        {
            var pos = ph.Position.ToVector2I();
            if (!gateConnected.Contains(pos))
                result.Error("ClosedGateBlocksRoute", $"Required hidden placeholder {ph.Id} is blocked by a closed puzzle gate");
        }
    }

    private static void ValidateDeadEnds(HashSet<Vector2I> walkable, int width, int height, HashSet<Vector2I> payoff, ValidationResult result)
    {
        foreach (var branch in FloorGraph.DeadEndBranches(walkable, width, height))
        {
            var adjacent = new HashSet<Vector2I>();
            foreach (var cell in branch)
                adjacent.UnionWith(FloorGraph.WalkableNeighbors(walkable, cell));
            var branchSet = branch.ToHashSet();
            adjacent.UnionWith(branchSet);
            if (payoff.Intersect(adjacent).Any() == false)
                result.Warning("UnrewardedDeadEnd", $"Unrewarded dead-end branch at {branch[0]}");
        }
    }

    // Reject tiles and entities whose coordinates fall outside [0,width) x
    // [0,height). The importer derives GridMap bounds from ground tiles, so an
    // out-of-range or negative cell silently distorts the grid. Each layer is
    // checked independently so the error message identifies the offending layer.
    //
    // Entity positions are read directly from model.Entities lists rather than
    // via EntityGroups() to avoid double-enumerating View() (which would
    // duplicate UnrecognizedEntityType warnings when EntityGroups is called
    // again in the main validation loop below).
    private static void ValidateFootprint(FloorJsonModel model, int width, int height, ValidationResult result)
    {
        foreach (var (layerName, tiles) in model.TileLayers)
        {
            if (tiles == null) continue;
            foreach (var t in tiles)
            {
                if (t.X < 0 || t.Y < 0 || t.X >= width || t.Y >= height)
                    result.Error("TileOutOfBounds",
                        $"Tile in layer '{layerName}' at ({t.X},{t.Y}) is outside expected footprint [0,{width}) x [0,{height})");
            }
        }

        CheckEntityBounds("enemy_spawns", model.Entities.EnemySpawns, width, height, result);
        CheckEntityBounds("npc_spawns", model.Entities.NpcSpawns, width, height, result);
        CheckEntityBounds("treasure_boxes", model.Entities.TreasureBoxes, width, height, result);
        CheckEntityBounds("trap_tiles", model.Entities.TrapTiles, width, height, result);
        CheckEntityBounds("puzzle_switches", model.Entities.PuzzleSwitches, width, height, result);
        CheckEntityBounds("puzzle_gates", model.Entities.PuzzleGates, width, height, result);
        CheckEntityBounds("puzzle_riddles", model.Entities.PuzzleRiddles, width, height, result);
        CheckEntityBounds("stair_connections", model.Entities.StairConnections, width, height, result);
        CheckEntityBounds("hidden_placeholders", model.Entities.HiddenPlaceholders, width, height, result);
    }

    private static void CheckEntityBounds<T>(string group, List<T>? entities, int width, int height, ValidationResult result) where T : class
    {
        if (entities == null) return;
        foreach (var e in entities)
        {
            var pos = e switch
            {
                EnemySpawnData x => x.Position,
                NpcSpawnData x => x.Position,
                TreasureBoxData x => x.Position,
                TrapTileData x => x.Position,
                PuzzleSwitchData x => x.Position,
                PuzzleGateData x => x.Position,
                PuzzleRiddleData x => x.Position,
                StairConnectionData x => x.Position,
                HiddenPlaceholderData x => x.Position,
                _ => null,
            };
            if (pos == null) continue;
            if (pos.X < 0 || pos.Y < 0 || pos.X >= width || pos.Y >= height)
            {
                string id = e switch
                {
                    EnemySpawnData x => x.Id, NpcSpawnData x => x.Id, TreasureBoxData x => x.Id,
                    TrapTileData x => x.Id, PuzzleSwitchData x => x.Id, PuzzleGateData x => x.Id,
                    PuzzleRiddleData x => x.Id, StairConnectionData x => x.Id, HiddenPlaceholderData x => x.Id,
                    _ => "?",
                };
                result.Error("EntityOutOfBounds",
                    $"Entity '{id}' in {group} at ({pos.X},{pos.Y}) is outside expected footprint [0,{width}) x [0,{height})");
            }
        }
    }

    private static string Format(IEnumerable<Vector2I> cells)
        => string.Join(", ", cells.Select(c => $"({c.X},{c.Y})"));

    private class EntityView
    {
        public string Id = "";
        public string PuzzleId = "";
        public Vector2I Position;
    }

    private class TreasureDataBoxed : EntityView
    {
        public List<TreasureBoxItemData>? Items;
    }

    private static List<EntityView> View(System.Collections.IList? list, ValidationResult result)
    {
        var views = new List<EntityView>();
        if (list == null) return views;
        foreach (var e in list)
        {
            switch (e)
            {
                case TreasureBoxData t:
                    views.Add(new TreasureDataBoxed { Id = t.Id, Position = t.Position.ToVector2I(), Items = t.Items });
                    break;
                case EnemySpawnData x:    views.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case NpcSpawnData x:      views.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case StairConnectionData x: views.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case HiddenPlaceholderData x: views.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case TrapTileData x:      views.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleSwitchData x:  views.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleGateData x:    views.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleRiddleData x:  views.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                default:
                    result.Warning("UnrecognizedEntityType",
                        $"Unrecognized entity type in entity list: {e?.GetType().Name ?? "null"} — skipped");
                    break;
            }
        }
        return views;
    }
}
