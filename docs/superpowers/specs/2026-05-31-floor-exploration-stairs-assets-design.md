# Floor Exploration, Stairs, and Asset Coverage Design

## Summary

Improve the authored 1F and 2F maze experience so side branches feel worth exploring, stairs behave immediately when stepped on, and GF/early-floor actors stop falling back to colored rectangles because of missing runtime sprite sheets.

This pass updates generated floor content rather than hand-editing `.tscn` output. `tools/floor1_maze_generator.py` remains the source of truth for 1F, 2F, and the safe 3F landing. Imported scenes and floor resources are regenerated from the model and then verified with generator tests and GdUnit scene/runtime tests.

## Current Findings

The current tests prove the floors are reachable and structurally valid, but they do not enforce exploration payoff. A graph pass over the generated walkable cells found:

- 1F has 21 dead-end leaves, 7 with no enemy, treasure, stair, puzzle beat, or adjacent payoff.
- 2F has 16 dead-end leaves, 15 with no enemy, treasure, stair, puzzle beat, or adjacent payoff.
- Existing stair wiring includes GF, 1F, 2F, and the 3F landing, but player input currently requires standing on a stair and pressing `interact`.
- GF NPC runtime paths are missing for `shopkeeper` and `healer`.
- Early-floor enemy sprite gaps include `orc`, `skeleton_warrior`, `cave_spider`, and `forest_spirit` at the canonical `assets/sprites/enemies/{type}/sprite_sheet.png` path. Goblin already has a canonical runtime sheet.

## Goals

- Every dead-end branch on 1F and 2F should have an authored payoff: enemy, treasure, visible stair, puzzle/trap beat, hidden-placeholder marker, or shortcut function.
- 1F should be denser with enemies, but enemies should gate branch access or patrol meaningful corridors rather than fill empty space randomly.
- 2F should convert many empty dead ends into loopbacks or shortcut routes, with remaining dead ends rewarded.
- Stairs should transition immediately when the player steps onto the stair tile. No extra `interact` press is required for floor changes.
- GF NPCs and early-floor enemies should have canonical runtime sprite sheets or an intentional migration from an existing legacy path.

## Non-Goals

- Do not introduce a key/lock system, new floor index beyond the existing 3F landing, or new hidden-room mechanics.
- Do not replace the generated floor pipeline with manual `.tscn` editing.
- Do not reveal hidden placeholder branches as visible stairs unless the generator explicitly marks them as active floor exits.
- Do not redesign GF layout beyond asset coverage and stair regression tests.

## Floor Content Design

### Branch Payoff Rule

The generator should identify branch tails from the walkable graph and reject floors where a dead-end chain lacks payoff. A branch has payoff when one of these is on the branch path or directly guarding its mouth:

- `EnemySpawn`
- `TreasureBoxSpawn`
- `StairConnection`
- puzzle trap, switch, gate, or riddle entity
- hidden placeholder metadata
- a shortcut edge that produces a measurable path saving after its blocker is cleared or gate is opened

This rule should be covered in Python tests so future generator edits cannot reintroduce empty branches.

### 1F Changes

Keep the `60x60` footprint, existing down stair to GF, two up stairs to 2F, and the current south puzzle shortcut. Add content to the currently empty branch tails with a mix of:

- additional goblin/orc/skeleton/forest-spirit enemies on branch mouths or branch ends;
- small treasure caches at branch tips;
- one or more loop cuts where a dead end would be more useful as a shortcut.

Enemy additions should raise 1F density without making the floor a single forced combat hallway. Required stair routes must remain reachable after clearable enemy cells are removed.

### 2F Changes

Keep the `60x60` archive footprint, two down stairs to 1F, one up stair to 3F, existing treasure/puzzle identity conventions, and no NPCs. Rework the empty branch set more heavily than 1F:

- turn selected long dead ends into shortcut loopbacks;
- place treasure behind optional branch routes;
- place extra enemies where they guard branch access, archives, or shortcut mouths;
- keep the puzzle vault and shortcut behavior measurable with path-length savings.

2F should feel less like a comb and more like an authored archive with return routes and optional risks.

## Stair Behavior

Stairs transition immediately on successful movement onto a stair tile:

- `PlayerController` should transition after movement when `GridMap.IsOnStairs(playerPosition)` and `FloorManager.IsOnStairs(...)` both resolve.
- The old pending stair interaction state should be removed or bypassed so `interact` is no longer part of stair travel.
- Movement into battles, NPCs, treasure boxes, puzzle gates, and puzzle interactables should keep their current behavior.
- Regression coverage should include GF to 1F, 1F to GF, 1F to each 2F stair, 2F back to each matching 1F stair, 2F to 3F, and 3F back to 2F.

## Asset Coverage

Use the existing Sirius asset workflow:

- Check canonical runtime paths from code before generating.
- Do not overwrite existing runtime sheets.
- Migrate the existing forest spirit sheet from `assets/sprites/characters/forest_spirit/sprite_sheet.png` to `assets/sprites/enemies/forest_spirit/sprite_sheet.png` unless there is a reason to regenerate it.
- Generate missing runtime sheets for GF NPCs and early-floor enemies that currently fall back to rectangles.
- Update `docs/enemies/ENEMY_SPRITES.md` and any NPC/UI asset status docs added or touched during the work.

## Verification

Required verification before implementation is called complete:

- `python3 -m unittest tests.tools.test_floor1_maze_generator -v`
- focused GdUnit scene/runtime tests for 1F/2F/3F stair transitions and floor layout invariants
- focused asset existence/dimension checks for canonical NPC/enemy sprite sheets
- `dotnet build Sirius.sln`

If Godot runtime test settings are available, run the focused `dotnet test` suites using `test.runsettings.local`.
