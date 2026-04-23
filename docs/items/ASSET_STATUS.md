# Sirius RPG — Full Asset Status

Complete status of every image asset the game requires, derived directly from source code.
Update the ✅/❌ column when an asset is generated. See `ITEM_PROMPT_GUIDE.md` for item-specific
generation prompts.

---

## How Sprites Are Loaded

| Category | How code loads it | Path convention |
|----------|------------------|-----------------|
| **Terrain tiles** | `GD.Load<Texture2D>(path)` in `GridMap.cs:473–479` | `assets/sprites/terrain/*.png` |
| **Character/Enemy sprites** | `sprite_sheet.png` per entity in `EnemySpawn.cs:262` / `PlayerDisplay.cs:26` | `assets/sprites/enemies/{type}/sprite_sheet.png` (new) or `assets/sprites/characters/{name}/sprite_sheet.png` (legacy) |
| **Sprite sheets** | Auto-built by `tools/sprite_sheet_merger.py` from `frames/frame1-4.png` | Same dir as `frames/` |
| **Item icons** | `AssetPath` field in `EquipmentCatalog.cs` | `assets/sprites/items/{slot}/{id}.png` |
| **UI backgrounds** | Hard-coded paths in `MainMenu.cs:27` and `BattleManager.cs:180` | `assets/sprites/ui/*.png` |
| **Effects/Icons** | Currently no code loads these; reserved for future use | `assets/sprites/effects/*.png`, `assets/sprites/ui/icon_*.png` |

---

## 1. Terrain Tiles

All tiles are **32×32 px** (the grid cell size), loaded by `GridMap.cs`.

### Floor Tiles

| Status | Asset Path | Area Key in Code |
|--------|-----------|-----------------|
| ✅ exists | `assets/sprites/terrain/floor_starting_area.png` | `"starting_area"` |
| ✅ exists | `assets/sprites/terrain/floor_forest.png` | `"forest"` |
| ✅ exists | `assets/sprites/terrain/floor_cave.png` | `"cave"` |
| ✅ exists | `assets/sprites/terrain/floor_desert.png` | `"desert"` |
| ✅ exists | `assets/sprites/terrain/floor_swamp.png` | `"swamp"` |
| ✅ exists | `assets/sprites/terrain/floor_mountain.png` | `"mountain"` |
| ✅ exists | `assets/sprites/terrain/floor_dungeon.png` | `"dungeon"` |
| ❌ missing | `assets/sprites/terrain/floor_boss_arena.png` | `"boss_arena"` (not yet in GridMap area dict) |

### Wall Tile

| Status | Asset Path |
|--------|-----------|
| ✅ exists | `assets/sprites/terrain/wall_generic.png` |

### Stair / Transition Tiles

> **Naming note:** The old `ASSET_REQUIREMENTS.md` used `stairs_up.png` / `stairs_down.png`
> (with an *s*), but the actual files on disk and what the TileMapLayer tileset references use
> `stair_up.png` / `stair_down.png` (without *s*). The shorter form is canonical.

| Status | Asset Path | Purpose |
|--------|-----------|---------|
| ✅ exists | `assets/sprites/terrain/stair_up.png` | Floor-to-floor ascent tile |
| ✅ exists | `assets/sprites/terrain/stair_down.png` | Floor-to-floor descent tile |
| ❌ missing | `assets/sprites/terrain/stair_left.png` | Lateral transition (left) |
| ❌ missing | `assets/sprites/terrain/stair_right.png` | Lateral transition (right) |
| ❌ missing | `assets/sprites/terrain/gate_north.png` | Same-floor scene gate (north) |
| ❌ missing | `assets/sprites/terrain/gate_south.png` | Same-floor scene gate (south) |
| ❌ missing | `assets/sprites/terrain/gate_west.png` | Same-floor scene gate (west) |
| ❌ missing | `assets/sprites/terrain/gate_east.png` | Same-floor scene gate (east) |

**AI prompts** for all stair/gate tiles are in `docs/ASSET_REQUIREMENTS.md §Transition Tiles`.

---

## 2. Character & Enemy Sprites

Sprite sheets are built from 4 animation frames by `tools/sprite_sheet_merger.py`.
Each entry below is the **sprite_sheet.png** the game loads at runtime.

### Player

| Status | Sprite Sheet Path | Frames Path |
|--------|-----------------|------------|
| ✅ exists | `assets/sprites/characters/player_hero/sprite_sheet.png` | `…/frames/frame1-4.png` ✅ |

### Enemies

Enemy type IDs come from `EnemyTypeId.cs`. The game resolves sprite sheets via
`EnemySpawn.cs:262–264`: new path first, legacy path as fallback.

| Status | EnemyTypeId | New Path (`enemies/{type}/sprite_sheet.png`) | Legacy Path |
|--------|------------|---------------------------------------------|-------------|
| ✅ exists | `goblin` | `assets/sprites/enemies/goblin/sprite_sheet.png` | — |
| ⚠️ unreachable | `forest_spirit` | ❌ not at `enemies/forest_spirit/` | `assets/sprites/characters/forest_spirit/sprite_sheet.png` exists on disk but code checks `characters/enemy_forest_spirit/` — **not reachable** |
| ❌ missing | `orc` | `assets/sprites/enemies/orc/sprite_sheet.png` | — |
| ❌ missing | `skeleton_warrior` | `assets/sprites/enemies/skeleton_warrior/sprite_sheet.png` | — |
| ❌ missing | `troll` | `assets/sprites/enemies/troll/sprite_sheet.png` | — |
| ❌ missing | `cave_spider` | `assets/sprites/enemies/cave_spider/sprite_sheet.png` | — |
| ❌ missing | `desert_scorpion` | `assets/sprites/enemies/desert_scorpion/sprite_sheet.png` | — |
| ❌ missing | `swamp_wretch` | `assets/sprites/enemies/swamp_wretch/sprite_sheet.png` | — |
| ❌ missing | `mountain_wyvern` | `assets/sprites/enemies/mountain_wyvern/sprite_sheet.png` | — |
| ❌ missing | `dark_mage` | `assets/sprites/enemies/dark_mage/sprite_sheet.png` | — |
| ❌ missing | `dungeon_guardian` | `assets/sprites/enemies/dungeon_guardian/sprite_sheet.png` | — |
| ❌ missing | `demon_lord` | `assets/sprites/enemies/demon_lord/sprite_sheet.png` | — |
| ❌ missing | `dragon` | `assets/sprites/enemies/dragon/sprite_sheet.png` | — |
| ❌ missing | `boss` | `assets/sprites/enemies/boss/sprite_sheet.png` | — |

**How to add a new enemy sprite:**
1. Place 4 frames as `assets/sprites/enemies/{type}/frames/frame1.png` … `frame4.png`
2. Run `python3 tools/sprite_sheet_merger.py` to generate `sprite_sheet.png`
3. The game auto-loads it via the path above

**AI prompts** for all enemies are in `docs/ASSET_REQUIREMENTS.md §Priority 1`.

> **`forest_spirit` migration:** The sprite exists at `assets/sprites/characters/forest_spirit/sprite_sheet.png`
> but `EnemySpawn.cs` cannot find it at runtime (checks `enemies/forest_spirit/` then `characters/enemy_forest_spirit/`).
> Move it to `assets/sprites/enemies/forest_spirit/sprite_sheet.png` to make it loadable, then delete the legacy directory.

---

## 3. Item Icons (current repo convention: 96×96 px)

See `ITEM_PROMPT_GUIDE.md` for full AI prompts. Paths come from `EquipmentCatalog.cs`.
Generate the source icon art, then resize the saved repo asset to match the existing item icon convention with `tools/resize_item_icons.py`.

### Equipment — Wooden Tier

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `wooden_sword` | `assets/sprites/items/weapons/wooden_sword.png` |
| ✅ exists | `wooden_armor` | `assets/sprites/items/armor/wooden_armor.png` |
| ✅ exists | `wooden_shield` | `assets/sprites/items/shields/wooden_shield.png` |
| ✅ exists | `wooden_helmet` | `assets/sprites/items/helmet/wooden_helmet.png` |
| ✅ exists | `wooden_shoes` | `assets/sprites/items/shoes/wooden_shoes.png` |

### Equipment — Iron Tier

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `iron_sword` | `assets/sprites/items/weapons/iron_sword.png` |
| ❌ missing | `iron_armor` | `assets/sprites/items/armor/iron_armor.png` |
| ❌ missing | `iron_shield` | `assets/sprites/items/shields/iron_shield.png` |
| ❌ missing | `iron_helmet` | `assets/sprites/items/helmet/iron_helmet.png` |
| ❌ missing | `iron_boots` | `assets/sprites/items/shoes/iron_boots.png` |

> `assets/sprites/items/weapons/iron_sword.png` now exists. Godot import metadata (`.import` files) is generated locally by the editor and is not tracked in this repository.

### Consumables

`ConsumableCatalog.cs` does not yet set `AssetPath`. Paths below are the intended locations.

| Status | ID | Intended Asset Path |
|--------|----|-------------------|
| ❌ missing | `health_potion` | `assets/sprites/items/consumables/health_potion.png` |
| ❌ missing | `greater_health_potion` | `assets/sprites/items/consumables/greater_health_potion.png` |
| ❌ missing | `mana_potion` | `assets/sprites/items/consumables/mana_potion.png` |
| ❌ missing | `strength_tonic` | `assets/sprites/items/consumables/strength_tonic.png` |
| ❌ missing | `iron_skin` | `assets/sprites/items/consumables/iron_skin.png` |
| ❌ missing | `swiftness_draught` | `assets/sprites/items/consumables/swiftness_draught.png` |
| ❌ missing | `antidote` | `assets/sprites/items/consumables/antidote.png` |
| ❌ missing | `regen_potion` | `assets/sprites/items/consumables/regen_potion.png` |
| ❌ missing | `poison_vial` | `assets/sprites/items/consumables/poison_vial.png` |
| ❌ missing | `flash_powder` | `assets/sprites/items/consumables/flash_powder.png` |

### Monster Parts

`MonsterPartsCatalog.cs` does not set `AssetPath`. No icons needed until inventory shows them visually.

| Status | ID | Intended Asset Path |
|--------|----|-------------------|
| ❌ missing | `goblin_ear` | `assets/sprites/items/monster_parts/goblin_ear.png` |
| ❌ missing | `orc_tusk` | `assets/sprites/items/monster_parts/orc_tusk.png` |
| ❌ missing | `skeleton_bone` | `assets/sprites/items/monster_parts/skeleton_bone.png` |
| ❌ missing | `spider_silk` | `assets/sprites/items/monster_parts/spider_silk.png` |
| ❌ missing | `dragon_scale` | `assets/sprites/items/monster_parts/dragon_scale.png` |

### Orphaned Item Assets

On-disk files not referenced by any current catalog entry.

| File | Former Use | Notes |
|------|-----------|-------|
| `assets/sprites/items/weapons/steel_longsword.png` | steel_longsword | Replaced by iron_sword |
| `assets/sprites/items/armor/chain_mail.png` | chain_mail | Replaced by iron_armor |
| `assets/sprites/items/shields/steel_tower_shield.png` | steel_tower_shield | Replaced by iron_shield |
| `assets/sprites/items/helmet/knight_helm.png` | knight_helm | Replaced by iron_helmet |
| `assets/sprites/items/shoes/swift_boots.png` | swift_boots | Replaced by iron_boots |
| `assets/sprites/items/consumables/minor_health_potion.png` | minor_health_potion | ID is now `health_potion` |
| `assets/sprites/items/consumables/mana_berry.png` | mana_berry | No matching catalog entry |
| `assets/sprites/items/consumables/elixir_of_fortitude.png` | elixir_of_fortitude | No matching catalog entry |

Originals of the above are preserved under `assets/sprites/items/original/` as reference art.

---

## 4. UI Assets

### Backgrounds

| Status | Asset Path | Size | Loaded By |
|--------|-----------|------|-----------|
| ✅ exists | `assets/sprites/ui/ui_main_menu_background.png` | 1920×1080 | `MainMenu.cs:27` |
| ✅ exists | `assets/sprites/ui/ui_battle_background.png` | 1280×720 | `BattleManager.cs:180` |

### Battle Buttons

| Status | Asset Path | Size |
|--------|-----------|------|
| ❌ missing | `assets/sprites/ui/ui_button_attack.png` | 64×32 |
| ❌ missing | `assets/sprites/ui/ui_button_defend.png` | 64×32 |
| ❌ missing | `assets/sprites/ui/ui_button_run.png` | 64×32 |

### Status Icons

| Status | Asset Path | Size |
|--------|-----------|------|
| ❌ missing | `assets/sprites/ui/icon_health.png` | 16×16 |
| ❌ missing | `assets/sprites/ui/icon_experience.png` | 16×16 |
| ❌ missing | `assets/sprites/ui/icon_level.png` | 16×16 |

**AI prompts** for all UI assets are in `docs/ASSET_REQUIREMENTS.md §Priority 3`.

---

## 5. Effect Sprites

No code currently loads these; reserved for future battle animations.

| Status | Asset Path | Size |
|--------|-----------|------|
| ❌ missing | `assets/sprites/effects/effect_hit_impact.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_magic_sparkles.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_level_up.png` | 96×96 |

**AI prompts** for all effects are in `docs/ASSET_REQUIREMENTS.md §Priority 4`.

---

## Summary

| Category | ✅ Exists | ❌ Missing |
|----------|----------|-----------|
| Terrain tiles | 9 | 7 (1 floor + 6 stair/gate) |
| Characters/Enemies | 2 (+ 1 legacy) | 13 |
| Item icons — equipment | 6 | 4 |
| Item icons — consumables | 0 | 10 |
| Item icons — monster parts | 0 | 5 |
| UI backgrounds | 2 | 0 |
| UI buttons & icons | 0 | 6 |
| Effects | 0 | 3 |
| **Total** | **19** | **48** |

---

## Maintenance

- When a new item is added to any `*Catalog.cs`, add a row here and in `ITEM_PROMPT_GUIDE.md`.
- When a new `EnemyTypeId` constant is added, add a row to the Enemies table.
- When an asset file is generated and placed, change ❌ to ✅.
- Keep the Summary counts in sync.
