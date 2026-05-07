# Sirius Floor Generation Workflow

## Current Pipeline

- Floors use grid coordinates multiplied by `GridMap.CellSize` (`32`). Match each floor scene's `GridMap.GridWidth` and `GridMap.GridHeight` to the confirmed playable footprint.
- Floor resources live in `resources/floors/Floor*.tres`.
- Authored/generated floor JSON lives in `scenes/game/floors/Floor*.json`.
- Imported Godot scenes live in `scenes/game/floors/Floor*.tscn`.
- Runtime registration is in `scenes/game/Game.tscn` through `FloorManager`.
- JSON import/export logic is in `scripts/tilemap_json/` and `tools/tilemap_json_sync.py`.
- Static enemy and NPC scene nodes must have `Owner` set to the scene root so `ResourceSaver` persists them in `.tscn`.

## Design Brief Template

Before implementation, confirm this with the user:

```text
Floor: <Floor2F/Floor3F/etc>
Footprint: <width>x<height>, with top-left or centered placement; confirm whether the scene bounds should match this size exactly
Entrances: <count and source floor/stair ids>
Visible exits: <count, target floor ids, rough locations>
Hidden placeholders: <count, purpose, visible now yes/no; hidden placeholders should not become visible stairs unless requested>
NPCs: <none or count/types/locations>
Enemies: <types, count, what each blocks>
Complexity: <simple/moderate/complex/custom; confirm optional knobs like deeper branches, more intersections, enemy gates, shortcut unlocks, and reduced long wall runs>
Theme: <terrain/area feel>
Verification expectations: <route gating, boss path, optional branches, etc>
```

If the user has already answered one of these, do not ask again; summarize it and ask only for missing high-impact choices.

## Implementation Checklist

1. Inspect existing floor files and generators:
   - `rg -n "Floor1F|Floor2F|FloorGF" resources scenes/game/Game.tscn tools tests scripts`
   - `rg -n "StairConnection|EnemySpawn|NpcSpawn" scenes/game/floors scripts tests`
2. Add or update a deterministic generator under `tools/`.
   - Keep dimensions, exits, hidden placeholders, enemies, and NPCs as named constants or structured data.
   - Emit `FloorJsonModel`-compatible JSON.
   - Update the matching `.tres` resource with player start and stair arrays when needed.
3. Add Python generator tests.
   - Dimensions and bounds.
   - Scene footprint size matches the brief; do not pad unused space with walls unless the user asked for that.
   - Entrance/exit count and stair visibility.
   - Hidden placeholders not visible unless requested.
   - NPC count matches the brief.
   - Enemy positions are walkable.
   - Reachability with enemies clear.
   - Gated branches unreachable while blocker enemy cells are treated as blocked.
   - Optional complexity knobs requested by the user: deep branches, intersections, shortcut unlocks, and maximum long wall runs.
4. Generate/import:
   - `python3 tools/<floor_generator>.py`
   - `python3 tools/tilemap_json_sync.py import scenes/game/floors/<Floor>.json scenes/game/floors/<Floor>.tscn`
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
- all visible stairs and entities are on walkable cells
- player start can reach required exits after clearable enemies are removed
- enemy blockers actually block the intended roads when treated as blocked
- separate exits remain separately gated if the brief requires it
- requested shortcut routes are measurably useful after the blocker is cleared
- requested branch/intersection depth and wall-run limits are enforced

## Verification Commands

Use the narrowest meaningful commands first:

```bash
python3 -m unittest tests.tools.test_<floor_generator_module> -v
```

```bash
python3 -m unittest tests.tools.test_tilemap_json_sync -v
```

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~<FloorLayoutTest>|FullyQualifiedName~TilemapJsonImporterTest|FullyQualifiedName~NpcSpawnTest"
```

```bash
dotnet build Sirius.sln
```

If Godot is not found, set `GODOT_PATH` to the local Godot Mono binary and rerun the same command.

## Failure Shields

- Do not hand-edit huge generated JSON or tile arrays. Change the generator and regenerate.
- Do not assume enemy placement gates a route. Test pathfinding with enemy cells blocked.
- Do not reveal hidden shortcut placeholders as visible stair nodes unless explicitly requested.
- Do not add advanced maze complexity by habit. Confirm whether the floor needs deeper branches, extra intersections, shortcut unlocks, and wall-run limits.
- Do not leave an intended small floor as a large scene padded by walls. Set `GridMap` bounds and tile layers to the confirmed footprint unless the user asked for padding.
- Do not add NPC spawns by habit; ask and test the requested count.
- Do not skip UID checks after scene import.
- Do not trust import logs alone. Inspect the saved `.tscn` and run scene-level tests.
