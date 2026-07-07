# Sirius Floor Generation Workflow

## Current Pipeline

- Floors use grid coordinates multiplied by `GridMap.CellSize` (`32`). Match each floor scene's `GridMap.GridWidth` and `GridMap.GridHeight` to the confirmed playable footprint.
- Floor resources live in `resources/floors/Floor*.tres`.
- Authored/generated floor JSON lives in `scenes/game/floors/Floor*.json`.
- Imported Godot scenes live in `scenes/game/floors/Floor*.tscn`.
- Runtime registration is in `scenes/game/Game.tscn` through `FloorManager`.
- Floor generation source is split into `scripts/data/floors/` (layout data: `Floor0-3Layout.cs`, `LayoutSpecs.cs`, `FloorRegistry.cs`) and `scripts/game/floors/` (generation logic: `FloorGenerationService`, `MazeBuilder`, `FloorGraph`, validation, scene writer, CLI). Regenerate a floor end-to-end with `godot --headless --path . --script tools/generate_floor.gd -- --floor N`, which writes the `.json`, `.tscn`, and `.tres` in one pass. The deprecated `tools/floor*_maze_generator.py` files are retained only as parity references.
- JSON import/export logic for manual `.json` ↔ `.tscn` round-trips is in `scripts/tilemap_json/` and `tools/tilemap_json_sync.py`.
- Static enemy and NPC scene nodes must have `Owner` set to the scene root so `ResourceSaver` persists them in `.tscn`.
- Static treasure and puzzle-trap nodes are imported from `entities.treasure_boxes`, `entities.trap_tiles`, `entities.puzzle_switches`, `entities.puzzle_gates`, and `entities.puzzle_riddles`. When a floor intentionally has none, prefer explicit empty arrays so the generator and scene tests can prove that intent.
- Side branches should have authored payoff. A generated floor should not contain dead-end branches that lack an enemy, treasure box, visible stair, puzzle/trap beat, hidden-placeholder purpose, or tested shortcut value.
- Stairs may be immediate or interact-required depending on the confirmed design. When immediate transitions are requested, runtime tests must prove stepping onto each visible stair changes floors without pressing interact.

## Current Treasure Box System

- Floor JSON uses `entities.treasure_boxes`: `{ id, position, gold, items: [{ item_id, quantity }] }`.
- Importer support lives in `scripts/tilemap_json/FloorJsonModel.cs` and `scripts/tilemap_json/TilemapJsonImporter.cs`. It creates/updates `TreasureBoxSpawn` children under `GridMap`, keys existing nodes by `TreasureBoxId` with node-name fallback, and removes stale boxes only when `treasure_boxes` is present.
- Runtime support lives in `scripts/game/TreasureBoxSpawn.cs`, `scripts/data/TreasureReward.cs`, `scripts/data/RecoveryChest.cs`, `scripts/game/GridMap.cs`, and `scripts/game/Game.cs`.
- `TreasureBoxSpawn` exports `GridPosition`, `TreasureBoxId`, `RewardGold`, `RewardItemIds`, and `RewardItemQuantities`. IDs must be stable and non-empty; empty IDs are rejected at open time to prevent repeat farming.
- Treasure cells block movement until the player faces the box and presses interact. Opening plays the box animation, grants gold/items once, marks `GameManager.OpenedTreasureBoxIds`, clears the `GridMap` cell to empty, and persists the opened ID in `SaveData`.
- Rewards should use valid `ItemCatalog` IDs. Inventory overflow is routed through `RecoveryChest` when available.

## Current Puzzle Trap System

- Floor JSON uses four coordinated entity lists:
  - `trap_tiles`: `{ id, puzzle_id, position, damage, status_effect, status_magnitude, status_turns }`
  - `puzzle_switches`: `{ id, puzzle_id, position, prompt_text, activated_text }`
  - `puzzle_gates`: `{ id, puzzle_id, position, starts_closed }`
  - `puzzle_riddles`: `{ id, puzzle_id, position, prompt_text, choices, correct_choice_id, wrong_answer_damage }`
- Importer support creates/updates `TrapTileSpawn`, `PuzzleSwitchSpawn`, `PuzzleGateSpawn`, and `PuzzleRiddleSpawn` nodes. Puzzle entities with empty `id` or empty `puzzle_id` are skipped. Existing nodes are keyed by their exported entity IDs (`TrapId`, `SwitchId`, `GateId`, `RiddleId`) with node-name fallback.
- Runtime support lives in `scripts/game/PuzzleSpawnBase.cs`, `scripts/game/TrapTileSpawn.cs`, `scripts/game/PuzzleSwitchSpawn.cs`, `scripts/game/PuzzleGateSpawn.cs`, `scripts/game/PuzzleRiddleSpawn.cs`, `scripts/game/PuzzleTrapController.cs`, `scripts/ui/PuzzleRiddleDialog.cs`, `scripts/game/GridMap.cs`, and `scripts/game/Game.cs`.
- `GridMap.RegisterStaticPuzzleEntities()` filters nodes to the current floor root. Active trap tiles are walkable damage cells; starts-closed unsolved gates block movement; unsolved switches and riddles are adjacent interactables. Registration will not overwrite walls, stairs, enemies, NPCs, or treasure boxes, so authored generators should avoid those conflicts and test for them.
- The player interacts with switches and riddles by facing them and pressing interact. Switches arm a `PuzzleId`; riddles before arming show the dormant message and stay open. Wrong riddle choices apply `WrongAnswerDamage` and allow retry. Correct choices after arming mark `GameManager.SolvedPuzzleIds`, open matching gates, clear trap/interactable cells, and persist the solved ID in `SaveData`.
- Trap damage never kills the player; it floors HP at 1. Trap status effects apply only when `status_effect` matches a `StatusEffectType` name and `status_turns > 0`.
- Only solved puzzle IDs are saved. Switch-armed state is session-local, so floor designs should not depend on half-solved switch state surviving save/load.
- Current Floor1F example: `Puzzle_1F_SouthShortcutTrial` contains four visible traps, one switch, one riddle, one starts-closed gate, and `TreasureBox_1F_SouthHiddenCache` behind the gate. Use it as a pattern, not a hardcoded template.

## Design Brief Template

Before implementation, confirm this with the user:

```text
Floor: <Floor2F/Floor3F/etc>
Footprint: <width>x<height>, with top-left or centered placement; confirm whether the scene bounds should match this size exactly
Entrances: <count and source floor/stair ids>
Visible exits: <count, target floor ids, rough locations>
Hidden placeholders: <count, purpose, visible now yes/no; hidden placeholders should not become visible stairs unless requested>
Stair behavior: <immediate on step-on or interact-required>
NPCs: <none or count/types/locations>
Enemies: <types, count, what each blocks>
Treasure: <none or box ids/rough locations/gold/item rewards; note which are optional, enemy-gated, or puzzle-gated>
Puzzle traps: <none or puzzle ids, traps, switches, riddles, gates, penalties, status effects, solved-state rewards/unlocks>
Complexity: <simple/moderate/complex/custom; confirm optional knobs like deeper branches, more intersections, enemy gates, shortcut unlocks, and reduced long wall runs>
Theme: <terrain/area feel>
Verification expectations: <route gating, boss path, optional branches, etc>
```

If the user has already answered one of these, do not ask again; summarize it and ask only for missing high-impact choices.

## Implementation Checklist

1. Inspect existing floor files and generators:
   - `rg -n "Floor1F|Floor2F|FloorGF" resources scenes/game/Game.tscn tools tests scripts`
   - `rg -n "StairConnection|EnemySpawn|NpcSpawn" scenes/game/floors scripts tests`
   - `rg -n "TreasureBox|RecoveryChest|Puzzle|TrapTile|PuzzleGate|PuzzleSwitch|PuzzleRiddle" scripts scenes/game/floors tools tests`
2. Add or update the C# generator. The root service `FloorGenerationService` lives in `scripts/game/floors/`; per-floor layout definitions live in `scripts/data/floors/` (`Floor0Layout` … `Floor3Layout`, `LayoutSpecs`). The service is separate from the layout data — edit the layout files for dimensional/entity constants and the service for generation/wall-carving logic.
   - Keep dimensions, exits, hidden placeholders, enemies, NPCs, treasure boxes, and puzzle traps as named constants or structured data.
   - For requested enemy-density changes, keep existing authored gate/patrol enemy IDs intact and add deterministic supplemental patrols with stable ID prefixes.
   - The model must round-trip through `FloorJsonModel` so JSON, scene, and `.tres` stay in sync.
   - The `.tres` resource (player start and stair arrays) is updated by the same CLI run; no manual regex edit.
3. Add C# parity tests under `tests/game/floors/` (and `tests/data/floors/` for registry tests).
   - Dimensions and bounds.
   - Scene footprint size matches the brief; do not pad unused space with walls unless the user asked for that.
   - Entrance/exit count and stair visibility.
   - Hidden placeholders not visible unless requested.
   - NPC count matches the brief.
   - Enemy positions are walkable.
   - Enemy-density multipliers preserve the authored baseline enemies and emit the expected stable supplemental patrol ID range.
   - Treasure boxes have unique IDs/positions, valid reward shape, walkable cells, no entity overlap, and intended reachability.
   - Puzzle entities have unique IDs/positions, non-empty shared `puzzle_id`s, valid riddle choices, walkable cells, no entity overlap, and no stair conflicts.
   - Reachability with enemies clear.
   - Gated branches unreachable while blocker enemy cells are treated as blocked.
   - Puzzle-gated rewards/shortcuts are blocked with starts-closed gates and reachable after those gates are open.
   - Dead-end branches have a payoff or explicit hidden-placeholder purpose.
   - Immediate stair designs have runtime coverage for stepping onto each visible stair without pressing interact.
   - Optional complexity knobs requested by the user: deep branches, intersections, shortcut unlocks, and maximum long wall runs.
4. Regenerate from C#:
   - `godot --headless --path . --script tools/generate_floor.gd -- --floor <N>`
   - Options: `--json-only` (write JSON only), `--skip-floor-def` (skip `.tres` sync), `--stair-dest x,y` (GF override).
   - No separate JSON-import step is needed for generation; the CLI writes `.json`, `.tscn`, and `.tres` together. For manual `.json` tweaks only, use `python3 tools/tilemap_json_sync.py import scenes/game/floors/<Floor>.json scenes/game/floors/<Floor>.tscn`.
5. Preserve UIDs.
   - Check the `[gd_scene ... uid="..."]` line.
   - Check each new `[ext_resource ... uid="..."]` line.
   - If import strips UIDs, restore them or extend the sync tooling and test it.
6. Register any new floor in `scenes/game/Game.tscn`.
7. Add focused GdUnit tests under `tests/game/`.

## Scene-Level Test Expectations

Scene tests should assert:

- ground count and wall count are in the intended range
- `GridMap.GridWidth` and `GridMap.GridHeight` match the intended scene footprint
- no layer has cells outside the intended footprint unless the brief explicitly asked for padded bounds
- visible stair node count and coordinates match the brief
- hidden placeholders are not visible stair nodes while hidden
- no NPC nodes exist when the brief says no NPCs
- enemy spawn count, types, and coordinates match the brief
- enemy-density multipliers preserve the required baseline enemies and add only expected supplemental patrol IDs
- treasure box count, IDs, coordinates, gold, reward item IDs, and quantities match the brief
- all treasure reward item IDs exist in `ItemCatalog`
- puzzle trap, switch, gate, and riddle counts match the brief
- puzzle entity IDs and `PuzzleId`s are non-empty, unique where required, and shared intentionally across each puzzle set
- riddles have choices, a `CorrectChoiceId` present in those choices, and intended wrong-answer damage
- starts-closed gates report `BlocksMovement` before solved
- all visible stairs and entities are on walkable cells
- puzzle entities do not overlap stairs, walls, enemies, NPCs, treasure boxes, or each other
- player start can reach required exits after clearable enemies are removed
- enemy blockers actually block the intended roads when treated as blocked
- puzzle-gated treasure or shortcut branches are unreachable with starts-closed gate cells blocked and reachable with only walls blocked
- separate exits remain separately gated if the brief requires it
- requested shortcut routes are measurably useful after the blocker is cleared
- no dead-end branch is empty unless the confirmed brief explicitly reserves it as a hidden placeholder
- immediate stair behavior transitions on step-on for every visible stair pair when requested
- requested branch/intersection depth and wall-run limits are enforced

## Verification Commands

Use the narrowest meaningful commands first:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~FloorGeneration"
```

```bash
godot --headless --path . --script tools/generate_floor.gd -- --floor <N>
```

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~<FloorLayoutTest>|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~NpcSpawnTest"
```

For treasure or puzzle trap changes, include the focused runtime suites when useful:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~TreasureBoxSpawnTest|FullyQualifiedName~TreasureRewardTest|FullyQualifiedName~PuzzleTrapSpawnTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GridMapPuzzleTrapTest"
```

```bash
dotnet build Sirius.sln
```

If Godot is not found, set `GODOT_PATH` to the local Godot Mono binary and rerun the same command.

## Failure Shields

- Do not hand-edit huge generated JSON or tile arrays. Change the generator and regenerate.
- Do not assume enemy placement gates a route. Test pathfinding with enemy cells blocked.
- Do not satisfy density requests by moving or renaming existing authored enemy blockers. Add supplemental deterministic patrols and keep baseline route-gating tests intact.
- Do not leave unrewarded dead ends. Add payoff content, convert the branch into a shortcut, or mark it as an intentional hidden placeholder and test that intent.
- Do not reveal hidden shortcut placeholders as visible stair nodes unless explicitly requested.
- Do not add advanced maze complexity by habit. Confirm whether the floor needs deeper branches, extra intersections, shortcut unlocks, and wall-run limits.
- Do not leave an intended small floor as a large scene padded by walls. Set `GridMap` bounds and tile layers to the confirmed footprint unless the user asked for padding.
- Do not add NPC spawns by habit; ask and test the requested count.
- Do not give treasure boxes blank or throwaway IDs. Opened state is save data, so renaming IDs changes player-visible persistence.
- Do not author treasure rewards with unverified item IDs or invalid quantities. Validate against `ItemCatalog` in scene tests.
- Do not place puzzle traps, gates, switches, or riddles on top of stairs, walls, enemies, NPCs, or treasure boxes. The runtime has skip/priority behavior, but generated floor content should be clean.
- Do not rely on switch-armed state as persistent progress. Only solved puzzle IDs persist.
- Do not let a starts-closed puzzle gate block mandatory exits or hidden placeholders unless the confirmed brief says that route should be puzzle-gated.
- Do not skip UID checks after scene import.
- Do not trust import logs alone. Inspect the saved `.tscn` and run scene-level tests.
