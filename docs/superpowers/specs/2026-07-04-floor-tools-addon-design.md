# Sirius Floor Tools Editor Addon Design

Linear: HPA-125 — Migrate floor/tilemap generation into a Godot editor addon.

## Summary

Replace the manual Python/JSON-first floor generation workflow with a first-class Godot editor addon and headless CLI, both backed by reusable C# services that generate, validate, write, and import/export Sirius floors. The existing Python generators are kept as legacy references until a semantic-parity gate proves the C# output reproduces them, after which they are deprecated.

The new `FloorGenerationService` ports the maze builders verbatim into C# and produces the same `FloorJsonModel` DTO that the existing importer/exporter already consume. One in-memory model fans out to scene writes (via the existing `TilemapJsonImporter`), typed `FloorDefinition.tres` updates (replacing regex text mutation), and JSON serialization for interchange and parity testing. The agent/CI surface is a single headless command that replaces the current `python3 tools/floor*_maze_generator.py` + `tilemap_json_sync.py import` chain.

## Current Findings

- `tools/floor0_maze_generator.py` (424 lines) and `tools/floor1_maze_generator.py` (1332 lines) hard-code maze layouts, build a `FloorJsonModel`-shaped dict, validate connectivity/placement, write JSON, and mutate `resources/floors/Floor*.tres` via fragile regex replacement.
- `scripts/tilemap_json/` already provides a solid C# bridge: `FloorJsonModel` (DTO), `TileConfigManager` (tile name <-> source-ID mapping from `config/tile_mapping.json`), `TilemapJsonImporter` (JSON -> `TileMapLayer`s + entity nodes), `TilemapJsonExporter` (scene -> JSON).
- `scripts/game/GridMap.cs` is `[Tool]` with editor toggles (`EditorExportToJson`, `EditorImportFromJson`, bake toggles) already wired through `_Process`.
- `tools/tilemap_json_sync.py` + `tools/refresh_tilemap.gd` provide a headless import path; headless export is unsupported.
- Agent floor work is driven by `.codex/skills/generate-sirius-floor/` (instructs agents to edit Python generators, run them, import JSON, preserve UIDs, add tests) and an external `godot-mcp` server referenced in `.mcp.json` (paint/save/run ops against the running editor).

## Goals

- An enableable `Sirius Floor Tools` plugin with an editor dock: select floor, generate, validate, bake/save scene, export JSON, import JSON.
- Generate GF and 1F/2F/3F floors from C# without running Python.
- Generate output writes directly into `GroundLayer`, `WallLayer`, `StairLayer`, and static entity children under `GridMap`.
- `FloorDefinition.tres` updates via typed Godot resource APIs (`ResourceLoader`/`ResourceSaver`), not regex.
- Validation catches disconnected walkable cells, unreachable entities, entity overlaps, entity-on-wall, entity-on-stair, closed puzzle gate blocking a required route, invalid treasure rewards, and invalid puzzle identity.
- Existing JSON import/export continues to work for LLM/agent co-editing.
- A headless CLI generates (and exports) floors for CI and agent use, replacing the Python entry points.
- Python generators are deprecated only after parity is proven.

## Non-Goals

- No `EditorImportPlugin` for custom file extensions (optional per the issue).
- No `FloorGenerationSpec` data-resource authoring format (follow-up candidate).
- No in-repo MCP server and no changes to the external `godot-mcp` server. The headless CLI is the agent surface.
- No byte-for-byte JSON reproduction; parity is semantic (deserialize-and-deep-equal). Committed JSON is regenerated from C# after the gate passes.
- No removal of the GridMap editor toggles; the dock is a superset.
- No deletion of the Python generators; they are retained and deprecated after parity.

## Key Decisions

- **Parity: semantic, exact.** The C# generator must reproduce the current Python output cell-for-cell and entity-for-entity. The gate is deserialize-and-deep-equal against the committed `Floor*.json`, not raw byte comparison (key ordering differs between Python `json.dumps` and `System.Text.Json`; matching it byte-for-byte would require per-floor custom serializers and would freeze the DTO). Once green, committed JSON is regenerated from C#.
- **Data flow: Generator -> `FloorJsonModel` -> three sinks.** `FloorGenerationService` produces one in-memory model. That model fans out to the existing `TilemapJsonImporter` (writes scene), `FloorResourceSyncService` (writes `.tres`), and `ToJson()` (writes JSON). This maximally reuses the proven importer and makes parity natural.
- **Headless generation is in scope**, not merely documented. The core services depend only on Godot core + `Sirius.TilemapJson` (no `EditorInterface`), so they run identically under `--headless`.
- **Agent surface: headless CLI only.** `tools/generate_floor.gd` is the single agent/CI entry point. The Codex skill and workflow docs are updated to use it. No MCP server work in this spec.

## Architecture

### Two-zone split

Core logic is editor-API-free (so it runs headless and is unit-testable); the addon is thin editor glue.

```text
addons/sirius_floor_tools/
  plugin.cfg                          name="Sirius Floor Tools", script=plugin .cs
  SiriusFloorToolsPlugin.cs           [Tool] EditorPlugin: add/remove dock
  SiriusFloorToolsDock.tscn           dock UI scene (Control)
  SiriusFloorToolsDock.cs             [Tool] dock controller - thin; delegates to services
  FloorDockGuard.cs                   pure cross-floor mismatch guard (extracted for unit tests)

scripts/data/floors/                  layout data + shared registry (namespace: global)
  Floor0Layout.cs / Floor1Layout.cs / Floor2Layout.cs / Floor3Layout.cs
  LayoutSpecs.cs                      per-floor layout spec (size, player start, areas)
  FloorRegistry.cs                    floor number -> .tscn/.tres/.json paths (shared by dock, CLI, tests)
  EnemySpec.cs                        supplemental-enemy placement spec record

scripts/game/floors/                  namespace Sirius.FloorTools - reusable, testable, headless-safe
  FloorGenerationService.cs           ports Python maze builders -> FloorJsonModel
  MazeBuilder.cs                      ports Python MazeBuilder (carve_cell/rect/corridor/loop)
  FloorValidationService.cs           ports validate_model -> ValidationResult (issues list)
  FloorResourceSyncService.cs         typed FloorDefinition .tres updates (replaces regex)
  FloorSceneWriter.cs                 orchestrator: model -> importer + resource sync + save scene
  FloorCli.cs                         static entry invoked by the headless GDScript
  FloorEntityBuilders.cs              entity-record construction helpers
  FloorGraph.cs                       walkable-cell graph / reachability helpers
  SupplementalEnemyPlanner.cs         deterministic supplemental-enemy placement
  UidPreserver.cs                     capture/restore file-level UIDs across scene saves
  AtomicFileWriter.cs                 temp -> rename atomic write
  ValidationResult.cs / ValidationIssue.cs

tools/generate_floor.gd               headless entry: --floor N -> generate+validate+scene+.tres+json
```

> **Note:** the original spec proposed a single `scripts/floor_tools/` directory.
> The implementation split it into `scripts/data/floors/` (layout data + registry)
> and `scripts/game/floors/` (generation logic + services) to match the existing
> `scripts/data` vs `scripts/game` convention.

### Data flow

```text
FloorGenerationService.Generate(n)  -->  FloorJsonModel  (in memory)
                                              |
              +-------------------------------+-------------------------------+
              v                               v                               v
   FloorValidationService         TilemapJsonImporter              FloorResourceSyncService
   (abort on Error)               (existing) -> writes              -> typed FloorDefinition.tres
                                  TileMapLayers + entities           via ResourceSaver
                                              |
                                  FloorSceneWriter orchestrates, then PackScene saves the .tscn
                                  (works under --headless and in editor)
                                              |
                                  optional: model.ToJson() -> Floor*.json (interchange/parity)
```

## Service Layer

### MazeBuilder.cs

Verbatim port of the Python `MazeBuilder`: `CarveCell`, `CarveRect`, `CarveHCorridor`, `CarveVCorridor`, `CarvePath`, `CarveLoop`, `ReinforcePerimeter`. Wall set stored as `HashSet<Vector2I>` (value-equality struct matching Python set semantics). Floor-specific carving stays in the generation methods, mirroring Python structure.

### FloorGenerationService.cs

One method per floor, each a line-for-line port of the corresponding Python build function:

- `GenerateGroundFloor()`, `GenerateFloor1()`, `GenerateFloor2()`, `GenerateFloor3()`, plus `Generate(int n)` dispatch.
- All hard-coded constants (`PLAYER_START`, `MAIN_LOOP_POINTS`, `TREASURE_BOXES`, every entity coordinate, `perimeter_walls`) ported unchanged. This is what guarantees parity.
- Output ordering mirrors Python: ground tiles row-major across the full grid, walls sorted by `(y, x)`, entity lists in Python's array order.

### FloorValidationService.cs

Ports both Python `validate_model` functions plus the extra acceptance-criteria checks. Returns a `ValidationResult` (`List<ValidationIssue>` with `Severity {Error, Warning}`, `Code`, `Message`) instead of throwing, so the dock can render issues. Generate/write treats any `Error` as an abort.

| Check | Source |
|---|---|
| Player start walkable | Python |
| Disconnected walkable cells (BFS) | Python |
| Entity id empty / duplicate | Python |
| Entity position overlap (= entity-on-stair, entity-on-entity) | Python |
| Entity on wall / unreachable | Python |
| Closed puzzle gate blocks player start | Python |
| Closed puzzle gate blocks required entities (stairs, placeholders) | Python |
| Unrewarded dead-end branches (floor 1, 2) | Python |
| Invalid puzzle identity (empty `puzzle_id`) | importer + issue |
| Invalid treasure rewards (unknown item id / qty <= 0) | issue (new) |

### FloorResourceSyncService.cs

Replaces regex. Loads `FloorDefinition` via `ResourceLoader`, sets typed properties, saves via `ResourceSaver`:

- `PlayerStartPosition`, `StairsUp`/`StairsDown` (split `stair_connections` by direction), `StairsUpDestinations`/`StairsDownDestinations`.
- `FloorSyncOptions` carries the GF return-spawn override + "preserve existing destinations" flag, mirroring the Python `--stair-dest` argument and the GF special-case, so `.tres` output is identical to today.

### FloorSceneWriter.cs

Context-independent orchestrator (identical in-editor and headless), so no save-strategy abstraction is needed:

1. `model = FloorGenerationService.Generate(n)`
2. `result = FloorValidationService.Validate(model, ...)` -> abort if any Error
3. `scene = Load<PackedScene>(scenePath).Instantiate(); gridMap = scene.FindChild("GridMap")`
4. `new TilemapJsonImporter().ImportToScene(model, gridMap)` (reuses existing bridge; writes layers + entities, sets grid bounds)
5. `def = Load<FloorDefinition>(defPath); FloorResourceSyncService.Apply(def, model, options); ResourceSaver.Save(def, defPath)`
6. `var pack = new PackedScene(); pack.Pack(scene); ResourceSaver.Save(pack, scenePath)` (works under `--headless` and in editor)
7. optional `model.ToJson() -> Floor*.json`; `scene.Free()`

## Editor Plugin and Dock

`plugin.cfg` (standard): name "Sirius Floor Tools", script "SiriusFloorToolsPlugin.cs".

`SiriusFloorToolsPlugin.cs` (`[Tool]`, extends `EditorPlugin`): `_EnterTree` loads the dock scene and `AddControlToDock(DockSlot.LeftUl, dock)`; `_ExitTree` removes and frees it. No `EditorImportPlugin`.

Dock UI (`SiriusFloorToolsDock.tscn`, a `VBoxContainer`):

- Floor selector (`OptionButton`: GF/1F/2F/3F) mapping to scene + def paths via `FloorRegistry`.
- Buttons: Generate, Validate, Export JSON, Import JSON, Bake/Save Scene.
- Results panel (`RichTextLabel`) streaming operation output and structured validation issues with severity icons.

Button behavior:

- **Generate**: runs `FloorSceneWriter` end-to-end (generate -> validate -> write scene -> sync `.tres` -> save), then `EditorInterface.GetResourceFilesystem().Scan()` so the editor reloads changed files. Same code path as headless.
- **Validate**: exports the current open floor scene to a model via the existing `TilemapJsonExporter`, runs `FloorValidationService`, shows issues.
- **Export/Import JSON**: thin wrappers over `TilemapJsonExporter`/`TilemapJsonImporter` (same code the GridMap toggles already call).
- **Bake/Save Scene**: packs + saves the currently-edited floor scene (for manual edits).

The dock holds no generation logic; it only wires buttons to services and renders output.

## Headless CLI and Agent Workflow

`tools/generate_floor.gd` is a thin GDScript entry (mirrors `refresh_tilemap.gd`) that calls a C# `FloorCli.Run(...)` static so all logic stays testable in C#:

```bash
godot --headless --path . --script tools/generate_floor.gd -- \
    --floor <0|1|2|3> \
    [--json-only]        write Floor*.json without touching scene/.tres
    [--skip-floor-def]   skip .tres sync
    [--stair-dest x,y]   GF return-spawn override (parity with Python --stair-dest)
```

It runs `FloorSceneWriter` end-to-end, prints a summary line (wall/enemy/treasure counts, like the Python generators, for log scraping), and exits non-zero if validation produces any `Error`. This single command replaces the entire current agent chain (`python3 tools/floor*_maze_generator.py` + `tilemap_json_sync.py import` + UID restoration): because generation writes the scene directly via `PackScene`, there is no separate import/UID-restore step for generation.

Two co-existing authoring flows for agents (both headless):

- **Structural changes** (new rooms/enemies/puzzles): edit `FloorGenerationService.cs`, run `--floor N`. One language now (C#), not Python.
- **Small tweaks**: edit `Floor*.json`, import via the existing headless `tilemap_json_sync.py import` or `refresh_tilemap.gd`.

Agent skill and docs update (`.codex/skills/generate-sirius-floor/SKILL.md` + `references/sirius-floor-workflow.md`):

- Step 4 ("Add or adapt a generator in `tools/`, cover with Python tests") becomes "Edit `FloorGenerationService` (C#); regenerate via the headless CLI; cover with C# parity tests under `tests/game/floors/`."
- Step 5 ("Import with `tilemap_json_sync.py`") becomes "Regenerate via `generate_floor.gd`; no separate import for generation."
- Verification commands swap `python3 -m unittest tests.tools.test_<gen>` for `dotnet test --filter "~FloorGeneration"`.

Python deprecation: `tools/floor0_maze_generator.py`, `tools/floor1_maze_generator.py`, `tilemap_json_sync.py`, and `tests/tools/test_floor*` stay as-is. After parity tests pass and a full GF/1F/2F/3F regeneration confirms scenes + `.tres` match, the Python generators get a deprecation header pointing at `generate_floor.gd` (not deleted).

## Testing and Parity

> **Status (implemented):** the cutover is complete. Committed `Floor*.json` is now
> generated from C#, and `FloorGenerationParityTest` is a **C#↔C# determinism /
> regression gate** (generated model vs committed JSON baseline), **not** a
> Python-parity gate. The deprecated Python generators in `tools/` are frozen as
> historical reference and are NOT kept in parity with subsequent C# changes (see
> `CLAUDE.md` "Floor generation: PlayerStart deviation from Python"). The
> PlayerStart +1 x shift is an intentional, ratified deviation from the Python
> constants.

Parity gate = deserialize-and-deep-equal, for each of GF/1F/2F/3F:

- **Metadata**: `floor_name`, `floor_number`, `description`, `player_start{x,y}` - exact.
- **Tile layers** (ground/wall/stair): compare as cell-multisets keyed by `(x, y, tile_name, alt)` - order-independent, catches any missing/extra/wrong tile.
- **Entities** (all 8 types): compare as `id -> full-record` maps - catches any missing/extra/mis-fielded entity; duplicate-id detection falls out for free.

The golden reference is the committed `Floor*.json` (now C#-generated). The gate
locks the generated model to the committed baseline so any non-deterministic or
accidental drift in `FloorGenerationService` is caught.

Test files under `tests/game/floors/` and `tests/data/floors/`:

- `tests/game/floors/FloorGenerationParityTest.cs` - the determinism/regression gate (single most important test).
- `tests/game/floors/FloorGraphTest.cs` - walkable-cell graph / reachability helpers.
- `tests/game/floors/FloorValidationServiceTest.cs` - valid model -> no errors; inject defects (disconnected cell, entity overlap, closed gate on start, unrewarded dead-end, unknown item id, empty puzzle_id) -> expected error codes.
- `tests/game/floors/FloorResourceSyncServiceTest.cs` - apply to a temp `FloorDefinition`, assert typed fields match the values the Python regex produced.
- `tests/game/floors/FloorSceneWriterTest.cs` / `FloorSceneRoundTripTest.cs` - end-to-end into a temp scene: model -> importer -> `.tres` -> pack; assert layer cell counts and entity node presence.
- `tests/game/floors/FloorCliTest.cs` - CLI arg parsing (pure, no Godot I/O).
- `tests/data/floors/FloorRegistryTest.cs` - floor number -> path resolution + `FindByScenePath`.
- `tests/addon/FloorDockGuardTest.cs` - cross-floor mismatch guard (pure helper extracted from the editor dock).

Unchanged, must stay green: `TilemapJsonImporterTest`, `TilemapJsonExporterTest`, `FloorJsonModelTest`, `TileConfigManagerTest`, and the scene-level `tests/game/Floor*LayoutTest.cs` (these run against regenerated scenes and are the runtime-correctness gate: counts, stair visibility, reachability, treasure/puzzle behavior).

Verification commands:

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local --filter "~FloorTools|~TilemapJson|~FloorLayout"
godot --headless --path . --script tools/generate_floor.gd -- --floor 0   # smoke each floor 0-3
```

## Acceptance-Criteria Mapping

| # | Criterion | Satisfied by |
|---|---|---|
| 1 | Plugin appears in Project Settings, enableable after build | plugin.cfg + SiriusFloorToolsPlugin.cs |
| 2 | Dock w/ select, generate, validate, bake/save, export, import | dock + buttons (wired across phases) |
| 3 | Generate GF/1F/2F/3F without Python | FloorGenerationService + headless CLI |
| 4 | Output -> GroundLayer/WallLayer/StairLayer + entity children | FloorSceneWriter via existing TilemapJsonImporter |
| 5 | `.tres` via typed Godot APIs, not regex | FloorResourceSyncService |
| 6 | Validation (disconnect, unreachable, overlap, on-wall, on-stair, closed-gate, bad rewards, bad puzzle id) | FloorValidationService |
| 7 | JSON import/export still works | unchanged importer/exporter, reused |
| 8 | Headless generation/export | generate_floor.gd - fully implemented |
| 9 | Python kept/deprecated after parity | deprecation headers post-parity |

## Implementation Phasing

Each phase independently builds and tests.

1. **Addon skeleton** - `plugin.cfg`, plugin, dock shell (buttons non-functional), `FloorRegistry`. Meets AC #1. Build green.
2. **Generation** - `MazeBuilder` + `FloorGenerationService` (GF -> 1F -> 2F -> 3F) + parity tests. Meets AC #3, parity gate. Largest phase.
3. **Scene + resource write** - `FloorSceneWriter` + `FloorResourceSyncService`; wire Generate button; `.tres` tests. Meets AC #4, #5.
4. **Validation** - `FloorValidationService`; wire Validate button; defect-injection tests. Meets AC #6.
5. **Import/Export/Bake buttons** - thin wrappers over existing importer/exporter. Meets AC #2 complete, #7.
6. **Headless CLI** - `generate_floor.gd` + `FloorCli`; smoke each floor 0-3. Meets AC #8.
7. **Docs + deprecation** - update Codex skill/workflow, add Python deprecation headers, regenerate committed JSON/scenes/.tres from C#. Meets AC #9.

## Risks and Mitigations

- **Parity drift**: the deserialize-and-deep-equal gate is the cutover lock; committed JSON regenerated from C# once green.
- **C# editor plugin must build before it can be enabled**: Phase 1 keeps the plugin minimal and proves the build/enable loop early.
- **PackScene scene-save correctness**: `FloorSceneWriterTest` plus the existing `Floor*LayoutTest` suite (runs against regenerated scenes) cover it.
- **Tile source drift** (`config/tile_mapping.json`): pre-existing risk, unchanged; services reuse `TileConfigManager` so no new exposure.
