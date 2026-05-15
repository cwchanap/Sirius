# Floor 1 Puzzle Traps Design

## Summary

Add the first puzzle-trap set to an optional Floor 1 side path. The room combines visible floor traps, a switch, a riddle object, a gate, a treasure pocket, and a shortcut back into the existing Floor 1 route. The puzzle should feel fair and readable: the player receives a clue before entering, can see the hazards, and is never required to solve it for main floor progression.

The solved state persists in save data. Ordinary trap triggers do not persist, so active traps remain reusable until the puzzle is solved.

## Goals

- Add an optional Floor 1 puzzle-trap chamber with a moderate dungeon hazard level.
- Combine three trap styles in one coherent encounter: visible floor traps, switch-and-gate routing, and a riddle choice.
- Reward success with both a treasure pocket and a shortcut.
- Keep Floor 1's required stair routes reachable without solving the puzzle.
- Disable the chamber traps and open the gate when the puzzle is solved.
- Persist solved puzzle IDs in save files.
- Validate floor placement, route safety, and JSON scene round-trip behavior.

## Non-Goals

- Do not add random or hidden traps in the first slice.
- Do not make the puzzle required for main progression.
- Do not add multi-switch logic or several linked puzzle rooms.
- Do not introduce a full puzzle catalog before there are multiple authored puzzle sets.
- Do not redesign Floor 1 beyond the side path needed for the chamber, treasure pocket, and shortcut.
- Do not persist every trap trigger or temporary switch state.

## Player Experience

The player discovers a clued Floor 1 side path that hints at a dangerous shortcut. The room should be readable before commitment: trap tiles are visibly different from ordinary floor tiles, the gate is clearly blocking the reward/shortcut route, and the switch plus riddle object are visible as deliberate interactables.

Expected flow:

1. The player enters the optional side path after seeing a clue.
2. Active visible trap tiles apply a moderate penalty when stepped on.
3. The player can use the switch to arm the chamber mechanism.
4. The player answers the riddle at the riddle object near the gate.
5. A correct answer after the switch is used solves the puzzle, opens the gate, and disables active traps.
6. The player can collect a higher-value treasure reward and use the shortcut exit.

Wrong riddle answers should be recoverable. They may apply a moderate trap backlash, but they should not permanently fail the puzzle or force a reload.

## Architecture

Use the existing authored floor-content pattern already used by enemies, NPCs, stairs, and treasure boxes.

New scene-placed puzzle nodes:

- `TrapTileSpawn`: walkable floor hazard tied to a `PuzzleId`.
- `PuzzleSwitchSpawn`: adjacent or same-cell interactable tied to a `PuzzleId`.
- `PuzzleGateSpawn`: blocking route gate tied to a `PuzzleId`.
- `PuzzleRiddleSpawn`: adjacent or same-cell interactable with prompt text, choices, and a correct answer tied to a `PuzzleId`.

New runtime coordination:

- `PuzzleTrapController` tracks puzzle state for the loaded floor.
- `GridMap` registers puzzle nodes into cell state and signal routes.
- `Game` owns player-facing interaction flow for switch, riddle, trap feedback, and HUD prompt updates.
- `GameManager` owns the solved puzzle ID set for the current session.
- `SaveData` stores solved puzzle IDs.

The first implementation should keep puzzle data on authored nodes. If later floors add many puzzle rooms, the node data can be promoted into static catalogs.

## Floor JSON And Import

Extend the floor JSON entity model with puzzle entities so generated Floor 1 content remains reproducible and round-trippable.

Suggested entity groups:

- `trap_tiles`
- `puzzle_switches`
- `puzzle_gates`
- `puzzle_riddles`

Each puzzle entity should include a stable `id`, `puzzle_id`, and `position`. Trap tiles should include penalty data. Gates should include whether they block movement before solve. Riddles should include short prompt text, choices, correct choice ID, and wrong-answer penalty data.

Importer behavior:

- Create missing puzzle nodes under `GridMap`.
- Update existing nodes by stable ID.
- Remove stale puzzle nodes when the corresponding entity list is explicit.
- Assign ownership to the scene root so imported nodes persist in `.tscn` files.
- Preserve existing enemy, NPC, stair, and treasure import behavior.

Exporter behavior:

- Include puzzle nodes in exported JSON so the floor can round-trip cleanly.

## Runtime Behavior

`GridMap.LoadFloor()` registers puzzle nodes after the baked tile layers are available and alongside other static entities. Registration should initialize cells from `GameManager.IsPuzzleSolved(puzzleId)`.

Trap behavior:

- Active trap tiles are walkable.
- Stepping on an active trap applies the configured penalty and gives feedback.
- Solved puzzle traps become safe.
- Trap triggers are not saved.

Gate behavior:

- Closed gates block movement.
- Solved puzzle gates become passable or visually open.
- Gate open state is derived from the persisted solved puzzle ID.

Switch behavior:

- Interacting with the switch arms the chamber for the current session.
- If the puzzle is already solved, the switch remains visually resolved and does not reopen hazards.
- Temporary switch state does not need to persist separately from the solved state.

Riddle behavior:

- Interacting with the riddle object opens a short choice prompt using world interaction state.
- If the switch has not been used, the riddle should indicate that the mechanism is dormant and should not solve the puzzle.
- Correct answer after switch use marks the puzzle solved, opens the gate, disables traps, and updates prompts.
- Wrong answer applies the configured penalty and allows retry.

World interaction state should block movement only while switch or riddle interaction is resolving. It must not interfere with battle, NPC dialogue, stairs, treasure boxes, inventory, pause, or save/load controls.

## Save Behavior

`SaveData` gains a solved puzzle ID list, for example `SolvedPuzzleIds`.

Save rules:

- Solving a puzzle adds its stable puzzle ID to `GameManager`.
- Manual saves include the solved puzzle ID list.
- Loading a save restores solved puzzle IDs before or during floor load.
- Puzzle nodes initialize from the solved ID list every time a floor loads.
- Older saves without solved puzzle IDs load with no solved puzzles.
- Trap trigger history and temporary switch state are not saved.

## Floor 1 Layout

Place the first puzzle set on an optional Floor 1 side branch. It must not sit on the required route to either active stair route.

Layout requirements:

- A clue appears before the first trap tile.
- Visible trap tiles form a short hazard lane.
- The switch is reachable before the gate but still inside the risky area.
- The riddle object is near the gate so its relationship to the gate is clear.
- The gate blocks access to both a treasure pocket and a shortcut.
- The shortcut reconnects to the existing Floor 1 route without bypassing required progression too aggressively.
- Rewards should be stronger than an ordinary branch cache because the chamber requires multi-step risk and puzzle solving.

The implementation plan should choose exact coordinates by inspecting the current `tools/floor1_maze_generator.py` layout and preserving existing stair, enemy, treasure, and required route validation.

## Error Handling

- Missing puzzle art falls back to simple visible drawings.
- Duplicate puzzle entity IDs are invalid authored content and should be caught by tests.
- Duplicate `PuzzleId` use is allowed only for entities intentionally belonging to the same puzzle group.
- Puzzle entities with empty IDs or puzzle IDs should be skipped with warnings.
- Unknown riddle choice IDs should prevent solving and emit a warning.
- Invalid trap penalty values should fall back to safe defaults.
- If save data references a solved puzzle ID that is absent from the loaded floor, loading should continue without error.

## Testing

Add tests at the same layers as the existing static floor-content systems.

Coverage targets:

- `FloorJsonModel` parses puzzle trap entities.
- `TilemapJsonImporter` creates, updates, removes, and assigns owners to puzzle nodes.
- `TilemapJsonExporter` includes puzzle nodes in exported JSON.
- `SaveData` serializes and deserializes solved puzzle IDs.
- `GameManager` records and restores solved puzzle IDs.
- `GridMap` registers trap, switch, gate, and riddle nodes without overlapping existing entities incorrectly.
- Trap movement applies penalty while active and not after solve.
- Gate movement blocks while unsolved and passes after solve.
- Switch plus correct riddle answer marks the puzzle solved, opens the gate, and disables traps.
- Wrong riddle answer applies the configured penalty and allows retry.
- `Floor1FMazeLayoutTest` asserts puzzle nodes are on walkable tiles, unique where required, do not overlap enemies, NPCs, treasure, or stairs, and do not block required routes.

Recommended verification loop:

- `python3 -m unittest tests.tools.test_floor1_maze_generator -v`
- `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorJsonModelTest|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~TilemapJsonExporterTest|FullyQualifiedName~Floor1FMazeLayoutTest|FullyQualifiedName~SaveDataTest|FullyQualifiedName~GameManagerTest"`
- `dotnet build Sirius.sln`

## Resolved Decisions

- First location: Floor 1 side path.
- Included trap types: visible floor traps, switch-and-gate puzzle, and riddle choice.
- Hazard level: moderate dungeon hazard.
- Payoff: treasure plus shortcut.
- Persistence: solved state only.
- Discovery style: clued side path.
- Trap shutdown: successful solve disables active trap tiles.
