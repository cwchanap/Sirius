# Floor2F Real Floor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the safe Floor2F landing with a real playable generated floor while preserving the existing Floor1F links.

**Architecture:** Extend the existing deterministic static floor pipeline in `tools/floor1_maze_generator.py` because it already owns Floor1F-to-Floor2F stairs and Floor2F placeholder generation. Add a minimal Floor3F placeholder so the new visible Floor2F up-stair resolves through `FloorManager` instead of pointing at a missing floor.

**Tech Stack:** Python generator/tests, Godot 4.6.2 C# scene resources, GdUnit4 scene tests, `tools/tilemap_json_sync.py`.

---

## Task 1: Test the New Floor2F Brief

**Files:**
- Modify: `tests/tools/test_floor1_maze_generator.py`
- Modify: `tests/game/Floor2FPlaceholderLayoutTest.cs`

- [ ] Replace placeholder generator expectations with a `60x60` Floor2F containing two down stairs, one up stair to `3F_2F_A`, 10-14 enemy spawns, no NPCs, 5-6 treasure boxes, one puzzle trap set, and explicit empty/filled entity arrays.
- [ ] Add path tests for required stairs with enemy cells clear, enemy-gated branches blocked before clearing enemies, and the puzzle-gated treasure/shortcut blocked while starts-closed gates are treated as walls.
- [ ] Run `python3 -m unittest tests.tools.test_floor1_maze_generator -v` and confirm the Floor2F tests fail against the current placeholder.

## Task 2: Generate Real Floor2F and Floor3F Placeholder

**Files:**
- Modify: `tools/floor1_maze_generator.py`
- Modify: `resources/floors/Floor2F.tres`
- Create: `resources/floors/Floor3F.tres`

- [ ] Change Floor2F constants to `60x60`, keep down stairs `2F_1F_A` and `2F_1F_B`, and add up stair `2F_3F_A`.
- [ ] Add deterministic Floor2F walls, enemy dictionaries, treasure dictionaries, and one puzzle set with traps, switch, gate, and riddle.
- [ ] Emit `trap_tiles`, `puzzle_switches`, `puzzle_gates`, and `puzzle_riddles` for Floor2F using a puzzle-specific helper so Floor1F keeps `Puzzle_1F_SouthShortcutTrial`.
- [ ] Add a small safe Floor3F placeholder model with down stair `3F_2F_A` back to `2F_3F_A`.
- [ ] Update generator CLI args for Floor3F output/resource and update the Floor2F/Floor3F resources from generated stair arrays.

## Task 3: Import Scenes and Register Floor3F

**Files:**
- Modify: `scenes/game/floors/Floor2F.json`
- Modify: `scenes/game/floors/Floor2F.tscn`
- Create: `scenes/game/floors/Floor3F.json`
- Create: `scenes/game/floors/Floor3F.tscn`
- Modify: `scenes/game/Game.tscn`

- [ ] Run `python3 tools/floor1_maze_generator.py`.
- [ ] Import Floor2F with `python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor2F.json scenes/game/floors/Floor2F.tscn`.
- [ ] Import Floor3F with `python3 tools/tilemap_json_sync.py import scenes/game/floors/Floor3F.json scenes/game/floors/Floor3F.tscn`.
- [ ] Preserve existing Floor2F scene UID and resource UID, and ensure new Floor3F resources have stable UIDs.
- [ ] Add `Floor3F.tres` to `FloorManager.Floors` after Floor2F.

## Task 4: Verify

**Files:**
- Test only.

- [ ] Run `python3 -m unittest tests.tools.test_floor1_maze_generator -v`.
- [ ] Run `python3 -m unittest tests.tools.test_tilemap_json_sync -v`.
- [ ] Run `dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~Floor2FMazeLayoutTest|FullyQualifiedName~TilemapJsonImporterTest"`.
- [ ] Run `dotnet build Sirius.sln`.

## Self-Review

- Scope is focused on generated Floor2F content plus the minimal Floor3F landing required by the approved visible future stair.
- No generated tile arrays will be hand-edited.
- Existing `.codex/skills/generate-sirius-floor/*` changes are unrelated prior skill edits and should remain untouched unless the user asks to stage or revise them.
