---
name: generate-sirius-floor
description: Use when designing, generating, importing, or validating new Sirius Godot floors, maze layouts, stair connections, enemy-gated branches, NPC placement, hidden shortcut placeholders, or placeholder future floors. Trigger for requests to create or revise floor `.json`, `.tscn`, `.tres`, floor generator tools, floor registration, and scene-level reachability tests.
---

# Generate Sirius Floor

## Overview

Use the static generated-floor pipeline already established in Sirius. First turn the user's floor idea into a confirmed design brief, then generate/import the floor, register any new floor resources, and prove reachability with tests.

Read [references/sirius-floor-workflow.md](references/sirius-floor-workflow.md) before editing generator, scene, resource, or test files.

## Workflow

1. Inspect the current floor pipeline before proposing edits:
   - `resources/floors/*.tres`
   - `scenes/game/floors/*.json`
   - `scenes/game/floors/*.tscn`
   - `tools/*floor*_maze_generator.py`
   - `tools/tilemap_json_sync.py`
   - `scripts/tilemap_json/*`
   - `tests/game/*Floor*LayoutTest.cs`
2. Confirm the design brief before touching floor content. If any of these are missing, ask concise questions and wait:
   - floor id/name and source/destination floor connections
   - playable footprint size inside the 160x160 grid
   - entrance count and coordinates or rough placement
   - exit/stair count, directions, and whether each stair is visible or hidden
   - NPC count/types, or explicit "no NPCs"
   - enemy count/types and which paths they should gate
   - maze complexity: simple, moderate, complex, or custom constraints
   - theme/terrain mix and special landmarks
   - placeholder future rooms, shortcuts, or floors
3. Restate the confirmed brief as an implementation checklist. For broad or risky changes, save a plan under `docs/superpowers/plans/YYYY-MM-DD-<feature>.md`.
4. Prefer deterministic generated static content over manual `.json` or `.tscn` edits. Add or adapt a generator in `tools/`, then cover it with Python tests.
5. Import generated JSON into Godot scenes with `tools/tilemap_json_sync.py`. Preserve scene and ext_resource UIDs after import.
6. Register new floor resources in the floor manager scene/config so stair transitions can resolve.
7. Add scene-level tests that prove counts, stair visibility, entity placement, and route gating. Treat enemy-gated roads as blocked until the enemy is cleared.
8. Run focused verification before claiming success:
   - generator unit tests
   - importer/sync tests if the import pipeline changed
   - focused GdUnit floor layout tests
   - `dotnet build Sirius.sln`

## Design Rules

- Use the 160x160 grid coordinate system; keep generated playable footprints bounded and explicit.
- Keep hidden shortcuts as metadata or blocked future areas until the user asks to reveal them. Do not place hidden placeholder stairs as visible `StairConnection` nodes unless requested.
- If the user says no NPCs, add no `NpcSpawn` nodes and test that none exist.
- Place enemies where they control branch access, not only as decoration. Validate that blocked enemy cells actually prevent access to gated branches.
- For multiple visible exits to the same future floor, test that clearing one gate does not open unrelated exits.
- Keep generated artifacts reproducible. Regenerate instead of hand-editing large generated JSON or tile arrays.
