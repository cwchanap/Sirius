---
name: generate-sirius-floor
description: Use when designing, generating, importing, or validating Sirius Godot floors, maze layouts, stair transitions, rewarded branches, enemy-gated routes, shortcut loops, NPC placement, treasure boxes, puzzle trap chambers, hidden placeholders, or placeholder future floors. Trigger for floor `.json`, `.tscn`, `.tres`, generator, registration, and reachability-test work.
---

# Generate Sirius Floor

## Overview

Use the static generated-floor pipeline already established in Sirius. First turn the user's floor idea into a confirmed design brief, then generate/import the floor, register any new floor resources, and prove reachability, rewarded exploration, stair transitions, treasure, and puzzle-trap behavior with tests.

Read [references/sirius-floor-workflow.md](references/sirius-floor-workflow.md) before editing generator, scene, resource, or test files.

## Workflow

1. Inspect the current floor pipeline before proposing edits:
   - `resources/floors/*.tres`
   - `scenes/game/floors/*.json`
   - `scenes/game/floors/*.tscn`
   - `scripts/floor_tools/` (C# generation source: `FloorGenerationService`, `layouts/*Layout.cs`, scene writer, validation)
   - `tools/generate_floor.gd` (headless regenerate CLI)
   - `tools/tilemap_json_sync.py` (round-trip `.tscn` ↔ `.json` for manual edits)
   - `tools/floor0_maze_generator.py`, `tools/floor1_maze_generator.py` (DEPRECATED parity references)
   - `scripts/tilemap_json/*`
   - `scripts/game/TreasureBoxSpawn.cs`
   - `scripts/data/TreasureReward.cs`
   - `scripts/game/Puzzle*Spawn.cs`
   - `scripts/game/PuzzleTrapController.cs`
   - `tests/game/*Floor*LayoutTest.cs`
   - `tests/floor_tools/` (C# parity tests)
2. Confirm the design brief before touching floor content. If any of these are missing, ask concise questions and wait:
   - floor id/name and source/destination floor connections
   - playable footprint size inside the 160x160 grid
   - entrance count and coordinates or rough placement
   - exit/stair count, directions, and whether each stair is visible or hidden
   - NPC count/types, or explicit "no NPCs"
   - enemy count/types and which paths they should gate
   - treasure box count, IDs, rough placement, and gold/item rewards, or explicit "no treasure"
   - puzzle-trap count, `PuzzleId`s, switch/riddle/gate/trap composition, damage/status effects, solved-state expectations, and what each closed gate rewards or unlocks
   - maze complexity: simple, moderate, complex, or custom constraints; ask which optional complexity knobs to include
   - theme/terrain mix and special landmarks
   - placeholder future rooms, shortcuts, or floors
   - stair activation behavior: immediate on step-on or interact-required
3. Restate the confirmed brief as an implementation checklist. For broad or risky changes, save a plan under `docs/superpowers/plans/YYYY-MM-DD-<feature>.md`.
4. Edit `FloorGenerationService` / `scripts/floor_tools/layouts/*Layout.cs` (C#) and regenerate via the headless CLI; cover changes with C# parity tests under `tests/floor_tools/`. The Python generators under `tools/floor*_maze_generator.py` are deprecated references — do not edit them for new generation work.
5. Regenerate via `godot --headless --path . --script tools/generate_floor.gd -- --floor N`. No separate JSON-import step is needed for generation; the CLI writes the `.json`, `.tscn`, and `.tres` in one pass.
6. Register new floor resources in the floor manager scene/config so stair transitions can resolve.
7. Add scene-level tests that prove counts, stair visibility, entity placement, treasure rewards, puzzle identities, gate blocking/opening, branch payoff, stair transition behavior, and route gating. Treat enemy-gated roads as blocked until the enemy is cleared and starts-closed puzzle gates as blocked until solved.
8. Run focused verification before claiming success:
   - generator unit tests
   - importer/sync tests if the import pipeline changed
   - focused GdUnit floor layout tests
   - `dotnet build Sirius.sln`

## Optional Complexity Knobs

Ask the user before enabling these unless they already requested them:

- multiple dead ends and deeper branch chains, not only one-room side paths
- more decision intersections and loops so the route has real navigation choices
- enemy-blocked roads where branch access requires clearing specific enemies
- shortcut branches that become useful only after a blocker is cleared
- treasure rewards behind optional dead ends, enemy gates, or puzzle gates
- puzzle trap chambers with visible traps, switch arming, riddle solving, starts-closed gates, and wrong-answer penalties
- trap status effects using existing `StatusEffectType` IDs
- visible stairs separated from hidden future-room or shortcut placeholders
- reduced long consecutive wall blocks so the map reads as authored maze space, not padded filler
- true scene footprint bounds matching the intended size, not a larger map filled with walls
- branch-payoff enforcement: each dead-end branch has treasure, enemy, stair, puzzle beat, hidden-placeholder purpose, or measurable shortcut function
- immediate stair transitions when the player steps onto a stair tile

## Design Rules

- Use the 160x160 grid coordinate system; keep generated playable footprints bounded and explicit.
- Keep hidden shortcuts as metadata or blocked future areas until the user asks to reveal them. Do not place hidden placeholder stairs as visible `StairConnection` nodes unless requested.
- If the user says no NPCs, add no `NpcSpawn` nodes and test that none exist.
- If the user says no treasure, emit/test an empty `treasure_boxes` list. If treasure exists, use stable non-empty `TreasureBoxId`s, valid `ItemCatalog` IDs, and test that boxes are walkable, unique, non-overlapping, reachable, and blocked only by intended gates.
- If the user says no puzzle traps, emit/test empty `trap_tiles`, `puzzle_switches`, `puzzle_gates`, and `puzzle_riddles` lists.
- Author puzzle traps as coordinated entity sets sharing a stable `PuzzleId`. Switches arm, riddles solve, starts-closed gates block until solved, and traps are walkable damage cells that disappear when the puzzle is solved.
- Do not place puzzle entities on stairs, walls, enemies, NPCs, treasure boxes, or each other. Registration will skip or overwrite some conflicts; tests should prevent the conflict instead.
- Place enemies where they control branch access, not only as decoration. Validate that blocked enemy cells actually prevent access to gated branches.
- For enemy-density increases, preserve existing named gate/patrol enemies and add deterministic supplemental patrol IDs instead of renaming authored blockers. Test both the requested multiplier and the stable supplemental ID range in generator and scene tests.
- Do not leave empty dead-end branches. Every dead end should reward exploration with an entity, guard, hidden-placeholder purpose, or a tested shortcut payoff.
- If stairs are intended to be immediate, test the full runtime transition path. Do not rely only on stair node/resource metadata.
- For multiple visible exits to the same future floor, test that clearing one gate does not open unrelated exits.
- Keep generated artifacts reproducible. Regenerate instead of hand-editing large generated JSON or tile arrays.
