# Sirius Maze TileMap + TileSet SOP (Godot 4.4, C#)

This SOP explains how to set up and debug the static maze using Godot 4 TileMapLayer nodes and a TileSet backed by TileSetAtlasSource. Follow this when editing tiles or onboarding new automation.

## Target Files and Nodes
- `scenes/game/Game.tscn`
  - Nodes: `GridMap` (Node2D), `GroundLayer` (TileMapLayer), `WallLayer` (TileMapLayer)
  - External TileSet: `res://assets/tiles/baked_tileset.tres`
- `assets/tiles/baked_tileset.tres`
  - Defines 8 TileSetAtlasSource entries (starting area, forest, cave, desert, swamp, mountain, dungeon, wall)
- `scripts/game/GridMap.cs`
  - Key flags: `UseBakedTileMapsAtRuntime`, `EnableDebugLogging`

## Canonical TileSet (.tres) Structure
Each TileSetAtlasSource must define at least one tile so the palette isn’t black.

Minimal valid per-source serialization (1×1 tile at top-left):
```
[sub_resource type="TileSetAtlasSource" id="<N>"]
texture = ExtResource("<texture_id>")
texture_region_size = Vector2i(32, 32)
0:0/size_in_atlas = Vector2i(1, 1)
0:0/0 = 0
```
Root `TileSet` must include:
```
[resource]
sources/0 = SubResource("1")
sources/1 = SubResource("2")
...
sources/7 = SubResource("8")
tile_size = Vector2i(32, 32)
```
Notes:
- `0:0/size_in_atlas = Vector2i(1, 1)` creates the tile at atlas coord (0,0).
- `0:0/0 = 0` ensures alternative tile ID 0 exists (matches editor serialization).
- `texture_region_size` and `tile_size` should match (32×32 in this project).

## Editor SOP (Painting and Verification)
1. Select `GridMap/GroundLayer` or `GridMap/WallLayer`.
2. Open the TileMap dock; verify the palette shows a valid tile thumbnail for each source (not a black square).
3. If palette is black:
   - Click `baked_tileset.tres` in the FileSystem and press the reload/refresh icon.
   - If still black, restart the Godot editor (clears cache quirks).
   - Ensure each atlas source in the .tres has `0:0/size_in_atlas` and `0:0/0` as above.
4. Paint a few tiles near the origin to confirm rendering.

## Runtime SOP (Static TileMapLayer default)
`GridMap.cs` controls runtime behavior, and the project defaults to static TileMapLayer tiles at runtime:
- `UseBakedTileMapsAtRuntime = true` (default) to render TileMapLayer tiles.
- `EnableDebugLogging = true` to print flag state and TileMapLayer used cell counts.
- No runtime auto-paint is performed; only static content saved in the scene is used.

Launch Game scene:
- Run `Game.tscn` (F5) or use the Godot MCP: run_project(scene="scenes/game/Game.tscn").
- Expected camera near grid (≈5, 80). Tiles visible only if painted statically in the editor or via MCP before running.

## Common Pitfalls and Fixes
- Black brush in editor:
  - TileSet tiles not defined. Ensure each atlas source has `0:0/size_in_atlas` and `0:0/0`.
  - Mismatched sizes: Keep `tile_size` and `texture_region_size` at 32×32.
  - Caching: Reload `baked_tileset.tres` or restart the editor.
- Tiles not visible at runtime with baked layers:
  - Confirm `UseBakedTileMapsAtRuntime = true`.
  - Confirm `GroundLayer`/`WallLayer` visible and not modulated.
  - Check debug output for used cell counts.

## MCP Playbook (Automation)
Use the Godot MCP operations to script checks/fixes without opening the editor:
- Read TileSet: `read_tileset(tilesetPath="assets/tiles/baked_tileset.tres")` to verify sources and tile ids.
- Set TileSet on layer: `set_tilemap_source(scenePath, tilemapPath, tilesetPath)`
- Paint tiles: `paint_tiles(scenePath, tilemapPath, tiles=[{sourceId, x, y, ...}])`
- Save scene: `save_scene(scenePath)`
- Run scene: `run_project(scene="scenes/game/Game.tscn")`
- Read logs: `get_debug_output()`

## Expansion: Multi-tile Atlases
If a texture contains multiple 32×32 tiles, add entries per atlas coord:
```
0:0/size_in_atlas = Vector2i(1, 1)
1:0/size_in_atlas = Vector2i(1, 1)
0:1/size_in_atlas = Vector2i(1, 1)
...
```
Optionally add alternatives for variation (e.g., `0:0/1 = 0`, `0:0/2 = 0`).

## Maintenance
- Keep `baked_tileset.tres` minimal and consistent.
- Do not hand-edit `tile_data` inside `Game.tscn`; paint in editor or use MCP.
- If converting from legacy `TileMap`, ensure `TileMapLayer` nodes remain direct children of `GridMap` as expected by `GridMap.cs`.

## Quick Checklist
- TileSet palette visible (no black squares)
- `tile_size` = `texture_region_size` = 32×32
- `GroundLayer`/`WallLayer` use `baked_tileset.tres`
- `UseBakedTileMapsAtRuntime = true`
- Debug logs show non-zero used cell counts or debug paint applied
