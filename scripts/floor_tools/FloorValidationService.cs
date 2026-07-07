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
        var walls = (model.TileLayers.GetValueOrDefault("wall") ?? new List<TileData>())
            .Select(t => new Vector2I(t.X, t.Y)).ToHashSet();
        var ground = model.TileLayers.GetValueOrDefault("ground") ?? new List<TileData>();
        var walkable = ground.Select(t => new Vector2I(t.X, t.Y)).Where(c => !walls.Contains(c)).ToHashSet();
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

        // Entity id/overlap/walkable/reachable checks
        var seenIds = new Dictionary<string, string>();
        var occupied = new Dictionary<Vector2I, string>();
        var goals = new List<Vector2I>();
        foreach (var (key, entities) in EntityGroups(model))
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
            ValidateDeadEnds(model, walkable, width, height, result);

        return result;
    }

    private static IEnumerable<(string Key, List<EntityView> Entities)> EntityGroups(FloorJsonModel model)
    {
        yield return ("enemy_spawns", View(model.Entities.EnemySpawns));
        yield return ("npc_spawns", View(model.Entities.NpcSpawns));
        yield return ("stair_connections", View(model.Entities.StairConnections));
        yield return ("hidden_placeholders", View(model.Entities.HiddenPlaceholders));
        yield return ("treasure_boxes", View(model.Entities.TreasureBoxes));
        yield return ("trap_tiles", View(model.Entities.TrapTiles));
        yield return ("puzzle_switches", View(model.Entities.PuzzleSwitches));
        yield return ("puzzle_gates", View(model.Entities.PuzzleGates));
        yield return ("puzzle_riddles", View(model.Entities.PuzzleRiddles));
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

    private static void ValidateDeadEnds(FloorJsonModel model, HashSet<Vector2I> walkable, int width, int height, ValidationResult result)
    {
        var payoff = new HashSet<Vector2I>();
        foreach (var (key, entities) in EntityGroups(model))
        {
            if (key == "npc_spawns") continue;
            foreach (var e in entities)
                payoff.Add(e.Position);
        }

        foreach (var branch in FloorGraph.DeadEndBranches(walkable, width, height))
        {
            var adjacent = new HashSet<Vector2I>();
            foreach (var cell in branch)
                adjacent.UnionWith(FloorGraph.WalkableNeighbors(walkable, cell));
            var branchSet = branch.ToHashSet();
            adjacent.UnionWith(branchSet);
            if (payoff.Intersect(adjacent).Any() == false)
                result.Error("UnrewardedDeadEnd", $"Unrewarded dead-end branch at {branch[0]}");
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

    private static List<EntityView> View(System.Collections.IList? list)
    {
        var result = new List<EntityView>();
        if (list == null) return result;
        foreach (var e in list)
        {
            switch (e)
            {
                case TreasureBoxData t:
                    result.Add(new TreasureDataBoxed { Id = t.Id, Position = t.Position.ToVector2I(), Items = t.Items });
                    break;
                case EnemySpawnData x:    result.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case NpcSpawnData x:      result.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case StairConnectionData x: result.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case HiddenPlaceholderData x: result.Add(new EntityView { Id = x.Id, Position = x.Position.ToVector2I() }); break;
                case TrapTileData x:      result.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleSwitchData x:  result.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleGateData x:    result.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
                case PuzzleRiddleData x:  result.Add(new EntityView { Id = x.Id, PuzzleId = x.PuzzleId, Position = x.Position.ToVector2I() }); break;
            }
        }
        return result;
    }
}
