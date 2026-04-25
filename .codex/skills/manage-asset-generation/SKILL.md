---
name: manage-asset-generation
description: Use when generating or validating Sirius game art assets and the work depends on checking the canonical repo path first, matching the existing asset convention for that category, saving into `assets/`, and updating the relevant asset docs to match the filesystem.
---

# Manage Asset Generation

## Overview

Follow the repo's asset SOP instead of generating art blindly. Check the runtime asset path on disk first, use existing shipped assets in the same category as the primary reference, generate only when the file is actually missing, save into the canonical repo location, apply any category-specific post-processing, and then update the matching docs so they reflect reality.

## Workflow

1. Identify the canonical runtime asset path from code first, then confirm the matching doc entry.
2. Check the filesystem first with a direct existence test such as `test -f`.
3. If the file already exists, do not generate a replacement unless the user explicitly asks to overwrite it.
4. Inspect existing same-category assets and record the current size, sheet layout, and visual convention before generating.
5. If the file is missing, generate the asset using the prompt guidance in the relevant doc, adjusted to match the repo's current reference assets.
6. Copy the generated image into the exact repo path under `assets/`.
7. Run any required category-specific processing such as item icon resizing or enemy sprite-sheet merging.
8. Re-check the filesystem to confirm the file now exists and has the expected dimensions.
9. Update the relevant asset docs so the status rows, notes, and size references stay correct.

## Source Of Truth

- Use the path referenced by the code or catalog as the canonical target.
- For item icons, prefer `scripts/data/items/EquipmentCatalog.cs`, [docs/items/ASSET_STATUS.md](../../../docs/items/ASSET_STATUS.md), and [docs/items/ITEM_PROMPT_GUIDE.md](../../../docs/items/ITEM_PROMPT_GUIDE.md).
- For enemies, prefer `scripts/game/EnemySpawn.cs`, `scripts/game/GridMap.cs`, `scripts/ui/BattleManager.cs`, and [docs/enemies/ENEMY_SPRITES.md](../../../docs/enemies/ENEMY_SPRITES.md).
- For terrain, prefer `scripts/game/GridMap.cs` and [docs/terrain/TERRAIN_SPRITES.md](../../../docs/terrain/TERRAIN_SPRITES.md).
- For UI and effects, prefer `scripts/ui/MainMenu.cs`, `scripts/ui/BattleManager.cs`, and [docs/ui/UI_SPRITES.md](../../../docs/ui/UI_SPRITES.md).

## Category Conventions

- Item icons:
  - Canonical paths come from `EquipmentCatalog.cs` and related item docs.
  - Use existing same-category icons as the size and composition reference.
  - Current known equipment icon convention is `96x96`.
- Enemy sprites:
  - Canonical runtime existence check is the merged `sprite_sheet.png`.
  - Prefer `assets/sprites/enemies/{type}/sprite_sheet.png`.
  - Legacy sheets still exist under `assets/sprites/characters/*/sprite_sheet.png` for some entities such as `player_hero` and `forest_spirit`, but note that `EnemySpawn.cs` legacy fallback checks `characters/enemy_{type}/` — so `forest_spirit` is NOT reachable at its current location.
  - Existing reference sheets are `assets/sprites/characters/player_hero/sprite_sheet.png`, `assets/sprites/enemies/goblin/sprite_sheet.png`, and `assets/sprites/characters/forest_spirit/sprite_sheet.png`, all currently `384x96`.
- Terrain:
  - Canonical paths are the top-level files in `assets/sprites/terrain/`.
  - Current shipped terrain, wall, and stair source art is `96x96`, even though `GridMap.CellSize` is `32`.
  - `GridMap.cs` uses `TERRAIN_BASE_PIXEL_SIZE = 96f` and scales those textures to the in-game cell size.
  - Existing references include `floor_starting_area.png`, `floor_forest.png`, `floor_cave.png`, `floor_desert.png`, `floor_swamp.png`, `floor_mountain.png`, `floor_dungeon.png`, `wall_generic.png`, `stair_up.png`, and `stair_down.png`.
  - `assets/sprites/terrain/original/` contains additional reference copies of the existing terrain set.
- UI and effects:
  - Canonical runtime paths come from the code when they are already wired, otherwise from the doc table.
  - Existing shipped UI references are `assets/sprites/ui/ui_main_menu_background.png` (`1920x1080`) and `assets/sprites/ui/ui_battle_background.png` (`1280x720`).
  - `assets/sprites/ui/original/` contains reference copies for those backgrounds.
  - For buttons, icons, and effects that do not exist yet, use the size in the doc unless the repo gains a stronger same-class reference.

## Asset Checks

- Use a direct existence check against the repo path before generating:
  ```bash
  test -f assets/sprites/items/weapons/iron_sword.png && echo exists || echo missing
  ```
- Enemy example:
  ```bash
  test -f assets/sprites/enemies/orc/sprite_sheet.png && echo exists || echo missing
  ```
- Terrain example:
  ```bash
  test -f assets/sprites/terrain/floor_boss_arena.png && echo exists || echo missing
  ```
- UI example:
  ```bash
  test -f assets/sprites/ui/ui_button_attack.png && echo exists || echo missing
  ```
- Do not rely only on markdown tables. Treat the filesystem as authoritative.
- If a doc says an asset is missing but the file exists, update the doc rather than regenerating the asset.

## Generation Rules

- Use the prompt text from the repo docs when available instead of inventing a new style.
- Keep the original generated file in the Codex generated-images folder. Copy it into the repo path instead of moving it.
- Match the existing repo convention for the specific category, not an older doc assumption.
- Preserve the asset class requirements:
  - Item icons: convert the saved repo asset to match the established item icon size already used in the repo.
  - Enemy sprites: generate four `96x96` frames when needed, then merge them into a `384x96` `sprite_sheet.png`.
  - Terrain tiles: match the current shipped `96x96` terrain source-art convention unless the repo standard changes.
  - UI backgrounds: match the existing shipped background sizes when replacing or extending that set.
  - Future UI buttons, icons, and effects: use the documented target size unless same-class repo assets establish a newer convention.

## Item Icon Resize Step

- Use [tools/resize_item_icons.py](../../../tools/resize_item_icons.py) for item icon downscaling.
- The current known convention for existing wooden item icons is `96x96`; verify same-category assets before resizing a newly generated icon.
- The helper works on directories, so for a single generated asset use a temporary source directory containing only that file, then copy the resized output back to the canonical asset path.
- Example:
  ```bash
  python3 tools/resize_item_icons.py --size 96 --source /tmp/item_icon_source --dest /tmp/item_icon_out
  ```
- Verify the final repo file dimensions with (requires Pillow: `pip install Pillow`):
  ```bash
  python3 -c "from PIL import Image; img=Image.open('assets/sprites/items/weapons/iron_sword.png'); print(f'{img.width}x{img.height}')"
  ```
  The output should match the target size (e.g. `96x96`) used by `tools/resize_item_icons.py`.

## Enemy Sprite Sheet Step

- Use the per-enemy prompts in [docs/enemies/ENEMY_SPRITES.md](../../../docs/enemies/ENEMY_SPRITES.md) to generate four frames when the runtime sheet is missing.
- The committed runtime asset to verify is `assets/sprites/enemies/{type}/sprite_sheet.png`, not the frame directory.
- After generating or placing `frame1.png` through `frame4.png`, run the repo merger:
  ```bash
  python3 tools/sprite_sheet_merger.py
  ```
- Verify the merged runtime sheet dimensions with (requires Pillow: `pip install Pillow`):
  ```bash
  python3 -c "from PIL import Image; img=Image.open('assets/sprites/enemies/goblin/sprite_sheet.png'); print(f'{img.width}x{img.height}')"
  ```
  The output should be `384x96` (4 frames × 96 px wide, 96 px tall) as produced by `tools/sprite_sheet_merger.py`.
- If the asset exists only on a legacy runtime path, do not regenerate it just because the preferred new path is absent; note the legacy state in the docs instead.

## Terrain And UI Reference Step

- Before generating terrain, inspect at least one adjacent existing terrain asset and one matching functional asset:
  - Example floor references: `assets/sprites/terrain/floor_forest.png`, `assets/sprites/terrain/floor_cave.png`
  - Example transition references: `assets/sprites/terrain/stair_up.png`, `assets/sprites/terrain/stair_down.png`
- Before generating UI, inspect the nearest existing shipped UI assets:
  - `assets/sprites/ui/ui_main_menu_background.png`
  - `assets/sprites/ui/ui_battle_background.png`
- If no same-class asset exists yet, use the doc prompt and size, but still document that the repo currently lacks a direct reference asset for that subclass.

## Documentation Updates

- After saving a newly generated asset, update the matching row in [docs/items/ASSET_STATUS.md](../../../docs/items/ASSET_STATUS.md) from `❌ missing` to `✅ exists`.
- Update summary totals if the document includes category counts.
- If [docs/items/ITEM_PROMPT_GUIDE.md](../../../docs/items/ITEM_PROMPT_GUIDE.md) includes status tables or stale-file notes for the same asset, update those too so the docs do not drift.
- Remove or rewrite notes that are no longer true, such as "PNG missing" warnings after the PNG has been restored.
- For enemy work, also update [docs/enemies/ENEMY_SPRITES.md](../../../docs/enemies/ENEMY_SPRITES.md) if the runtime sheet status or path note changed.
- For terrain work, also update [docs/terrain/TERRAIN_SPRITES.md](../../../docs/terrain/TERRAIN_SPRITES.md) if the file status, target size, or repo convention note changed.
- For UI or effects work, also update [docs/ui/UI_SPRITES.md](../../../docs/ui/UI_SPRITES.md) if the status, loading note, or size reference changed.
- When docs mention a target size that no longer matches shipped assets, correct the doc to the repo's current convention instead of following stale text blindly.

## Response Pattern

- State whether the file existed before generation.
- If generation was required, state the canonical save path that was populated.
- Mention which docs were updated to reflect the new status.
