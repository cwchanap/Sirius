# Floor 1 Combat-Gated Maze Redesign

## Summary

Redesign Floor 1 as a compact `60x60` combat-gated loop maze. The floor should feel more complex than the current small hand-authored layout, but shorter and denser than the Ground Floor maze. It will remove the current NPC, use enemy placements as visible branch gates, add two functional stairs to a new placeholder 2/F scene, and reserve three hidden future-room or shortcut branches without exposing those hidden destinations to the player yet.

## Goals

- Replace the current small Floor 1 layout with a `60x60` authored maze inside the existing `160x160` `GridMap` bounds.
- Add multiple branches and dead ends while keeping the floor readable.
- Use enemies as branch gates: walking into an enemy blocks movement, starts battle, and victory removes the enemy so the branch becomes passable.
- Provide two visible, usable stairs from 1/F to 2/F.
- Add a minimal placeholder 2/F scene so both new 2/F stairs have real return destinations.
- Reserve three hidden future-room or shortcut branches on 1/F without visible stair tiles or active `StairConnection` nodes.
- Keep the workflow repeatable through JSON generation and import rather than hand-editing packed tile data.

## Non-Goals

- Do not create the full 2/F maze or real 2/F content in this pass.
- Do not add NPCs to Floor 1 or the placeholder 2/F.
- Do not add new enemy types, loot systems, key systems, doors, chests, or hidden-room mechanics.
- Do not change the global `GridMap` size beyond the existing `160x160` runtime grid.
- Do not redesign Ground Floor beyond preserving the existing `GF_000` stair pairing.

## Layout Design

Floor 1 should use a central loop with several enemy-gated branches. The loop gives the player a route they can mentally map, while branch mouths and dead ends add local complexity.

Planned regions:

- **Entry Chamber**: contains the down stair back to G/F and gives the player a safe starting point.
- **Central Loop**: the main connected route, wide enough to avoid feeling cramped but still denser than G/F.
- **North/East 2/F Route**: a longer branch from the loop to one visible 2/F stair.
- **South/West 2/F Route**: a different branch from the loop to the second visible 2/F stair.
- **Hidden Placeholder Branches**: three carved side branches or dead ends reserved for future hidden rooms or shortcuts. They should look like ordinary dead ends for now, with no active stair marker.

The two 2/F routes should each require clearing at least one enemy gate. Optional branches may also be enemy-gated, but the main loop should not become a single forced hallway.

## Stairs

Floor 1 will contain three active stair connections:

- `1F_001`: visible down stair to `GF_000`.
- `1F_2F_A`: visible up stair to a matching 2/F down stair.
- `1F_2F_B`: visible up stair to a second matching 2/F down stair.

The new 2/F placeholder will contain matching down stairs:

- `2F_1F_A`: down stair linked back to `1F_2F_A`.
- `2F_1F_B`: down stair linked back to `1F_2F_B`.

`resources/floors/Floor1F.tres` should list both visible 2/F stairs in `StairsUp` and the G/F return stair in `StairsDown`. `resources/floors/Floor2F.tres` should list the two return stairs in `StairsDown` and no `StairsUp`.

The placeholder 2/F scene should be intentionally small and safe: enough ground and walls to show two separated landing pads with return stairs, but no maze, enemies, NPCs, or progression content.

## Hidden Placeholders

The three future hidden-room or shortcut placeholders on Floor 1 should be represented as reserved branch coordinates in the generator and tests. They should not be visible to the player in this pass:

- no stair-layer tile;
- no `StairConnection` node;
- no transition behavior;
- no special marker tile.

Validation should still know these reserved locations exist so future work can attach hidden-room or shortcut behavior without redesigning the floor.

## Enemy Gating

Floor 1 should remove `NpcSpawn_OldFarmer` and include a small mixed set of existing enemies. Enemy cells should occupy narrow branch mouths or chokepoints so each victory opens access to a branch or progression route.

Approved enemy set:

- Goblin near the first optional branch as the lightest gate.
- Orc at a central loop choke point.
- Skeleton Warrior on one 2/F stair route.
- Forest Spirit on the other 2/F stair route, giving variety without jumping to late-game difficulty.
- One optional side-branch Orc guarding a future-content dead end.

Implementation should use existing enemy spawn scenes and blueprint resources where possible. If a chosen enemy has no spawn scene, the implementation plan should either use a supported existing spawn scene with the correct blueprint or add the smallest reusable support needed for that enemy type.

Enemy placement must avoid stair tiles and must be on walkable cells. The floor should remain solvable after treating enemy cells as clearable gates.

## Technical Approach

Use the same static authored-floor approach as the Ground Floor maze:

- Add a deterministic `tools/floor1_maze_generator.py`.
- Generate `scenes/game/floors/Floor1F.json`.
- Import the JSON into `scenes/game/floors/Floor1F.tscn` through `tools/refresh_tilemap.gd`.
- Update `resources/floors/Floor1F.tres` from the generated model.
- Add `scenes/game/floors/Floor2F.tscn` and `resources/floors/Floor2F.tres` for the placeholder 2/F.
- Update `scenes/game/Game.tscn` so `FloorManager.Floors` contains G/F, 1/F, and 2/F in order.

The generator should emit `3,600` ground tiles for the `60x60` floor footprint and wall tiles for both interior maze walls and the surrounding unused grid area.

The existing importer currently updates existing `StairConnection` nodes but logs that new stair connections require manual creation. The implementation plan should inspect the importer again before coding and choose the least invasive path: pre-create required stair nodes in the placeholder scenes or add focused importer support for creating missing stairs.

## Runtime Behavior

Runtime should continue using baked tilemaps. `GridMap` should register static enemy spawns, static stair connections, and no NPC spawns for Floor 1.

Enemy gates rely on existing behavior:

- moving into an enemy cell emits `EnemyEncountered`;
- the player does not move onto the enemy cell before battle;
- after victory, `GridMap.RemoveEnemy()` clears the enemy and frees the spawn node;
- the cleared branch becomes passable during the current session.

No new lock or door system is required for this pass.

## Validation

Implementation should add generator-level and scene-level checks similar to the existing Ground Floor layout guardrails.

Required validation:

- `dotnet build Sirius.sln` succeeds.
- Floor 1 scene loads successfully.
- Floor 2 placeholder scene loads successfully.
- Floor 1 has non-zero ground, wall, and stair layers.
- Floor 1 has exactly three visible active stairs: one down to G/F and two up to 2/F.
- Floor 2 placeholder has exactly two visible active down stairs to 1/F.
- Floor 1 has no NPC spawns.
- Floor 1 has the approved enemy gate set: Goblin, Orc, Skeleton Warrior, Forest Spirit, and one optional side-branch Orc on walkable, non-stair cells.
- Player start, active stairs, enemies, and hidden placeholder coordinates are inside the `60x60` footprint.
- Paths exist from the 1/F entry to both 2/F stairs when enemy cells are treated as clearable.
- Hidden placeholder branches are reachable as ordinary dead ends but do not expose visible stair tiles or active `StairConnection` nodes.
- The G/F `GF_000` to 1/F `1F_001` pairing remains valid.

## Resolved Decisions

- Scale: `60x60`, not `100x100` or full `160x160`.
- Layout direction: combat-gated loop maze.
- Visible future exits: two 1/F to 2/F stairs.
- Hidden future destinations: three reserved branches, hidden for now.
- Floor 2 scope: minimal placeholder scene with matching return stairs only.
- NPCs: none on Floor 1 for this pass.
- Enemy role: enemies should intentionally block branch access and open routes after defeat.
