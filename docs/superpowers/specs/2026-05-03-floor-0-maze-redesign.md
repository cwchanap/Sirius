# Floor 0 Maze Redesign

## Summary

Redesign floor 0 as a 100x100 playable beginner maze on top of the existing static `FloorGF.tscn` tilemap workflow. The floor should feel more complex and rewarding than the current small layout while remaining readable for new players.

The selected direction is a moderate-density District Loop: one clear main loop through several starter districts, with optional branches for combat, shortcuts, and future content pockets.

## Goals

- Replace the current compact floor 0 layout with a 100x100 playable maze.
- Preserve a beginner-friendly feel with readable navigation, clear landmarks, and short optional dead ends.
- Keep shopkeeper and healer access early and easy.
- Move the floor 1 stair deeper into the floor so reaching it feels like completing the first exploration arc.
- Use a data-driven generated static floor approach rather than hand-editing packed tile data.
- Keep runtime behavior aligned with the existing baked `TileMapLayer` flow.

## Non-Goals

- Do not resize the global `GridMap` beyond its current 160x160 default for this pass.
- Do not implement runtime procedural generation for floor 0.
- Do not add a minimap, quest system, chest system, or new NPC interaction features as part of this maze pass.
- Do not redesign floor 1.

## Layout Design

The 100x100 floor is organized around a primary district loop. The loop should remain easy to follow, with wider corridors and landmark rooms that help the player build a mental map.

Planned regions:

- **Entrance Quarter**: player start, early access to shopkeeper and healer, and one low-risk combat branch.
- **North Loop District**: light side maze with one or two enemy pockets and a shortcut back toward the entrance loop.
- **East Progression District**: the main route advances here, with moderate branching and enough turns to feel maze-like.
- **South Optional District**: content pockets reserved for future chests, quests, or special NPCs, plus short dead ends.
- **Stair District**: deeper destination for the `GF_000` stair to floor 1, reachable through the main loop with at least one alternate route.

## Entity Placement

Existing entities may move to serve the new layout.

- Shopkeeper: early, near the entrance quarter, reachable without combat.
- Healer: early, near the entrance quarter or first loop bend, reachable without combat.
- First goblin: near an optional early branch so combat is discoverable but not forced immediately.
- Floor 1 stair: deeper in the stair district.
- Additional enemies: modest count, placed in pockets or route-adjacent rooms rather than directly blocking every path.

All entity grid positions must correspond to walkable cells. Scene node `position` values and exported `GridPosition` values must stay in sync.

## Technical Approach

Use a deterministic generator in `tools/` to produce/update floor 0 static tilemap data. The generator creates the authored layout from simple rules, writes JSON to `scenes/game/floors/FloorGF.json`, and updates `resources/floors/FloorGF.tres` metadata. A separate import step (`tools/refresh_tilemap.gd`) syncs the JSON into `scenes/game/floors/FloorGF.tscn`.

The generator should:

- fill a 100x100 ground region;
- create perimeter walls;
- create interior maze walls;
- carve the main district loop;
- carve branch corridors and landmark rooms;
- reserve future content rooms;
- place the stair tile;
- update the `StairConnection` node position and `GridPosition`;
- update NPC and enemy spawn positions;
- update `resources/floors/FloorGF.tres` metadata when needed.

This keeps the shipped floor static while making future layout adjustments repeatable and reviewable.

## Runtime Behavior

Runtime should continue using `GridMap.UseBakedTileMapsAtRuntime = true`. `GridMap.BuildGridFromBakedTileMaps()` should read the baked `GroundLayer`, `WallLayer`, and `StairLayer` content as it does today.

Because the target size is 100x100, the existing 160x160 grid bounds should remain sufficient. No global grid-size migration is required for this pass.

## Validation

Implementation should verify:

- `dotnet build Sirius.sln` succeeds.
- `FloorGF.tscn` loads successfully.
- Ground, wall, and stair layers contain non-zero cells.
- Player start, shopkeeper, healer, enemy spawns, and stair positions are inside the 100x100 layout.
- Each placed entity is on a walkable cell.
- A path exists from the player start to shopkeeper, healer, first enemy branch, and the floor 1 stair.
- The main loop remains connected after optional branches are added.

## Open Decisions Resolved

- Initial request considered 250x250, but the approved first pass is 100x100.
- The floor is a fully playable maze, not a sparse canvas.
- Maze density is moderate.
- Optional branches use a balanced mix of combat, shortcuts, and future content pockets.
- The implementation direction is generated static tilemap content, not hand-painted or runtime procedural generation.
