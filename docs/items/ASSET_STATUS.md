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
| **NPC sprites** | `NpcSpawn.cs:66–67` tries new path then legacy fallback | `assets/sprites/npcs/{type}/sprite_sheet.png` (new) or `assets/sprites/characters/npc_{type}/sprite_sheet.png` (legacy) |
| **Sprite sheets** | Auto-built by `tools/sprite_sheet_merger.py` from `frames/frame1-4.png` | Same dir as `frames/` |
| **Item icons** | `AssetPath` field in item catalog factories | `assets/sprites/items/{slot}/{id}.png` |
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
| ✅ exists | `crypt_sentinel` | `assets/sprites/enemies/crypt_sentinel/sprite_sheet.png` | — |
| ✅ exists | `grave_hexer` | `assets/sprites/enemies/grave_hexer/sprite_sheet.png` | — |
| ✅ exists | `bone_archer` | `assets/sprites/enemies/bone_archer/sprite_sheet.png` | — |
| ✅ exists | `iron_revenant` | `assets/sprites/enemies/iron_revenant/sprite_sheet.png` | — |
| ✅ exists | `cursed_gargoyle` | `assets/sprites/enemies/cursed_gargoyle/sprite_sheet.png` | — |
| ✅ exists | `abyss_acolyte` | `assets/sprites/enemies/abyss_acolyte/sprite_sheet.png` | — |
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

## 3. NPC Sprites

NPC sprite sheets follow the same 128×32 px / 4-frame format as enemies. `NpcSpawn.cs` tries
`assets/sprites/npcs/{type}/sprite_sheet.png` first, then `assets/sprites/characters/npc_{type}/sprite_sheet.png`
as a legacy fallback. All 4 registered NPCs are missing their sprites; the `assets/sprites/npcs/`
directory does not yet exist.

| Status | NpcId | SpriteType | Expected Path |
|--------|-------|-----------|---------------|
| ❌ missing | `village_shopkeeper` | `shopkeeper` | `assets/sprites/npcs/shopkeeper/sprite_sheet.png` |
| ❌ missing | `village_healer` | `healer` | `assets/sprites/npcs/healer/sprite_sheet.png` |
| ❌ missing | `old_farmer` | `villager` | `assets/sprites/npcs/villager/sprite_sheet.png` |
| ❌ missing | `village_blacksmith` | `blacksmith` | `assets/sprites/npcs/blacksmith/sprite_sheet.png` |

**How to add an NPC sprite:**
1. Place 4 frames as `assets/sprites/npcs/{type}/frames/frame1.png` … `frame4.png`
2. Run `python3 tools/sprite_sheet_merger.py` to generate `sprite_sheet.png`
3. The game auto-loads it via the path above

---

## 4. Item Icons (current repo convention: 96×96 px)

See `ITEM_PROMPT_GUIDE.md` for full AI prompts. Paths come from the item catalog `AssetPath` fields.
Generate the source icon art, then resize the saved repo asset to match the existing item icon convention with `tools/resize_item_icons.py`.

Dungeon consumables and monster parts now define `AssetPath` directly in their catalog factories, matching equipment.

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
| ✅ exists | `iron_armor` | `assets/sprites/items/armor/iron_armor.png` |
| ✅ exists | `iron_shield` | `assets/sprites/items/shields/iron_shield.png` |
| ✅ exists | `iron_helmet` | `assets/sprites/items/helmet/iron_helmet.png` |
| ✅ exists | `iron_boots` | `assets/sprites/items/shoes/iron_boots.png` |

> `assets/sprites/items/weapons/iron_sword.png` now exists. Godot import metadata (`.import` files) is generated locally by the editor and is not tracked in this repository.

### Equipment — Dungeon Steel Tier

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `steel_longsword` | `assets/sprites/items/weapons/steel_longsword.png` |
| ✅ exists | `chain_mail` | `assets/sprites/items/armor/chain_mail.png` |
| ✅ exists | `steel_tower_shield` | `assets/sprites/items/shields/steel_tower_shield.png` |
| ✅ exists | `knight_helm` | `assets/sprites/items/helmet/knight_helm.png` |
| ✅ exists | `swift_boots` | `assets/sprites/items/shoes/swift_boots.png` |

### Equipment — Dungeon Obsidian Tier

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `obsidian_blade` | `assets/sprites/items/weapons/obsidian_blade.png` |
| ✅ exists | `obsidian_carapace` | `assets/sprites/items/armor/obsidian_carapace.png` |
| ✅ exists | `obsidian_guard` | `assets/sprites/items/shields/obsidian_guard.png` |
| ✅ exists | `obsidian_crown` | `assets/sprites/items/helmet/obsidian_crown.png` |
| ✅ exists | `obsidian_treads` | `assets/sprites/items/shoes/obsidian_treads.png` |

### Consumables

`ConsumableCatalog.cs` defines `AssetPath` for these catalog-backed rows.

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `health_potion` | `assets/sprites/items/consumables/health_potion.png` |
| ✅ exists | `greater_health_potion` | `assets/sprites/items/consumables/greater_health_potion.png` |
| ✅ exists | `mana_potion` | `assets/sprites/items/consumables/mana_potion.png` |
| ✅ exists | `strength_tonic` | `assets/sprites/items/consumables/strength_tonic.png` |
| ✅ exists | `iron_skin` | `assets/sprites/items/consumables/iron_skin.png` |
| ✅ exists | `swiftness_draught` | `assets/sprites/items/consumables/swiftness_draught.png` |
| ✅ exists | `antidote` | `assets/sprites/items/consumables/antidote.png` |
| ✅ exists | `regen_potion` | `assets/sprites/items/consumables/regen_potion.png` |
| ✅ exists | `poison_vial` | `assets/sprites/items/consumables/poison_vial.png` |
| ✅ exists | `flash_powder` | `assets/sprites/items/consumables/flash_powder.png` |
| ✅ exists | `major_health_potion` | `assets/sprites/items/consumables/major_health_potion.png` |
| ✅ exists | `major_mana_potion` | `assets/sprites/items/consumables/major_mana_potion.png` |
| ✅ exists | `warding_charm` | `assets/sprites/items/consumables/warding_charm.png` |
| ✅ exists | `smoke_bomb` | `assets/sprites/items/consumables/smoke_bomb.png` |

### Monster Parts

`MonsterPartsCatalog.cs` defines `AssetPath` for these catalog-backed rows.

| Status | ID | Asset Path |
|--------|----|-----------|
| ✅ exists | `goblin_ear` | `assets/sprites/items/monster_parts/goblin_ear.png` |
| ✅ exists | `orc_tusk` | `assets/sprites/items/monster_parts/orc_tusk.png` |
| ✅ exists | `skeleton_bone` | `assets/sprites/items/monster_parts/skeleton_bone.png` |
| ✅ exists | `spider_silk` | `assets/sprites/items/monster_parts/spider_silk.png` |
| ✅ exists | `dragon_scale` | `assets/sprites/items/monster_parts/dragon_scale.png` |
| ✅ exists | `sentinel_core` | `assets/sprites/items/monster_parts/sentinel_core.png` |
| ✅ exists | `hexed_cloth` | `assets/sprites/items/monster_parts/hexed_cloth.png` |
| ✅ exists | `splintered_bone` | `assets/sprites/items/monster_parts/splintered_bone.png` |
| ✅ exists | `revenant_plate` | `assets/sprites/items/monster_parts/revenant_plate.png` |
| ✅ exists | `gargoyle_shard` | `assets/sprites/items/monster_parts/gargoyle_shard.png` |
| ✅ exists | `abyssal_sigil` | `assets/sprites/items/monster_parts/abyssal_sigil.png` |

### Orphaned Item Assets

On-disk files not referenced by any current catalog entry.

| File | Former Use | Notes |
|------|-----------|-------|
| `assets/sprites/items/consumables/minor_health_potion.png` | minor_health_potion | ID is now `health_potion` |
| `assets/sprites/items/consumables/mana_berry.png` | mana_berry | No matching catalog entry |
| `assets/sprites/items/consumables/elixir_of_fortitude.png` | elixir_of_fortitude | No matching catalog entry |

Originals of the above are preserved under `assets/sprites/items/original/` as reference art.

---

## 5. UI Assets

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

## 6. Effect Sprites

No code currently loads these; reserved for future battle animations.

| Status | Asset Path | Size |
|--------|-----------|------|
| ❌ missing | `assets/sprites/effects/effect_hit_impact.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_magic_sparkles.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_level_up.png` | 96×96 |

**AI prompts** for all effects are in `docs/ASSET_REQUIREMENTS.md §Priority 4`.

---

## Summary

| Category | ✅ Exists | ❌ Missing | ⚠️ Unreachable |
|----------|----------|-----------|----------------|
| Terrain tiles | 10 | 7 (1 floor + 6 stair/gate) | 0 |
| Characters/Enemies | 8 | 12 | 1 legacy |
| NPC sprites | 0 | 4 | 0 |
| Item icons — equipment | 20 | 0 | 0 |
| Item icons — consumables | 14 | 0 | 0 |
| Item icons — monster parts | 11 | 0 | 0 |
| UI backgrounds | 2 | 0 | 0 |
| UI buttons & icons | 0 | 6 | 0 |
| Effects | 0 | 3 | 0 |
| **Total** | **65** | **32** | **1** |

---

## Maintenance

- When a new item is added to any `*Catalog.cs`, add a row here and in `ITEM_PROMPT_GUIDE.md`.
- When a new `EnemyTypeId` constant is added, add a row to the Enemies table.
- When a new NPC is added to `NpcCatalog.cs`, add a row to the NPC Sprites table.
- When an asset file is generated and placed, change ❌ to ✅.
- Keep the Summary counts in sync.
