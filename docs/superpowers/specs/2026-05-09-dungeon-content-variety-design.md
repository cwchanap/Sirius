# Dungeon Content Variety Design

## Purpose

Increase content variety for the current dungeon biome in Sirius by adding a deeper dungeon enemy roster, dungeon-specific loot, stronger equipment rewards, supporting consumables, and matching generated art assets.

The implementation must focus on dungeon content only. Other biomes, crafting, quests, boss phase behavior, and new equipment mechanics are out of scope for this pass.

## Approved Direction

Use a catalog-driven dungeon content expansion that follows the repo's existing patterns:

- New enemy IDs and factories use the existing `EnemyTypeId`, `EnemyBlueprint`, `Enemy`, and `EnemySpawn` chain.
- New items use the static item catalogs: `EquipmentCatalog`, `ConsumableCatalog`, `MonsterPartsCatalog`, and `ItemCatalog`.
- Loot remains the main integration path from enemies to inventory.
- Generated art follows the repo-local `manage-asset-generation` workflow.
- The first playable wiring targets the current dungeon biome logic, while the content names and tiers must still scale into later dungeon floors.

## Scope

This pass targets the dungeon biome only:

- Add 6 new dungeon enemies.
- Add 2 dungeon equipment tiers above iron.
- Add 20 item catalog entries across equipment, monster parts, and consumables.
- Refresh existing dungeon enemy loot tables so dungeon rewards stop relying mostly on generic iron and dragon-scale rewards.
- Generate or import enemy sprite sheets and item icons for the new dungeon content only.
- Update tests and docs so the content chain is verifiable.

## Non-Goals

- No other biome expansion.
- No crafting system.
- No quest chains.
- No boss phase mechanics.
- No new stat model beyond existing attack, defense, speed, health, rarity, value, stack size, and consumable effects.
- No overwriting existing assets unless explicitly requested.

## Current Repo Context

The current content model is mostly static catalog driven:

- `scripts/data/EnemyTypeId.cs` is the single source for enemy string IDs.
- `scripts/data/EnemyBlueprint.cs` defines default enemy stat factories.
- `scripts/data/Enemy.cs` exposes factory helpers consumed by area encounter logic.
- `scripts/game/EnemySpawn.cs` supports scene-placed enemies through blueprints or legacy `EnemyType` strings.
- `scripts/data/LootTableCatalog.cs` maps enemy types to drop tables.
- `scripts/data/items/EquipmentCatalog.cs`, `ConsumableCatalog.cs`, and `MonsterPartsCatalog.cs` define item factories.
- `scripts/data/items/ItemCatalog.cs` registers item IDs for save/load, loot, shops, and inventory reconstruction.
- `scripts/game/Game.cs` contains area-based encounter selection.
- `scripts/game/GridMap.cs` contains deterministic map enemy marker selection.

Existing dungeon-adjacent enemy IDs include `dungeon_guardian`, `dark_mage`, `demon_lord`, and `boss`. Existing loot tables for some higher-tier enemies still lean on generic iron equipment and `dragon_scale`, so this pass must replace or supplement those with dungeon-specific drops.

## Enemy Roster

Add the following dungeon enemies:

| Enemy | ID | Role | Combat Identity | Primary Drop |
| --- | --- | --- | --- | --- |
| Crypt Sentinel | `crypt_sentinel` | Armored frontliner | High defense, moderate attack, slow speed | `sentinel_core` |
| Grave Hexer | `grave_hexer` | Control caster | Weaken or blind pressure | `hexed_cloth` |
| Bone Archer | `bone_archer` | Fast striker | Higher speed, lower health, slow or blind chance | `splintered_bone` |
| Iron Revenant | `iron_revenant` | Elite undead knight | High attack and defense | `revenant_plate` |
| Cursed Gargoyle | `cursed_gargoyle` | Durable construct | High health/defense, burn or heavy-hit pressure | `gargoyle_shard` |
| Abyss Acolyte | `abyss_acolyte` | Late-dungeon cult caster | Stun and weaken pressure | `abyssal_sigil` |

Enemy factory integration must include:

- `EnemyTypeId` constant.
- `EnemyBlueprint.Create*Blueprint()` factory.
- `Enemy.Create*()` factory.
- `EnemySpawn.CreateEnemyInstance()` legacy switch branch.
- `LootTableCatalog.GetByEnemyType()` branch and table factory.
- `EnemyDebuffProfile` entries for `grave_hexer`, `bone_archer`, `cursed_gargoyle`, and `abyss_acolyte`.

## Dungeon Items

### Monster Parts

Add one stackable monster part per new enemy:

| ID | Display Name | Source | Use |
| --- | --- | --- | --- |
| `sentinel_core` | Sentinel Core | Crypt Sentinel | Sellable dungeon part |
| `hexed_cloth` | Hexed Cloth | Grave Hexer | Sellable dungeon part |
| `splintered_bone` | Splintered Bone | Bone Archer | Sellable dungeon part |
| `revenant_plate` | Revenant Plate | Iron Revenant | Rare sellable dungeon part |
| `gargoyle_shard` | Gargoyle Shard | Cursed Gargoyle | Rare sellable dungeon part |
| `abyssal_sigil` | Abyssal Sigil | Abyss Acolyte | Rare sellable dungeon part |

Each monster part must set `AssetPath` in `MonsterPartsCatalog`, use a defined sell value, and be registered in `ItemCatalog`.

### Equipment

Add dungeon equipment above iron using the existing equipment slots.

Steel tier:

- `steel_longsword`
- `chain_mail`
- `steel_tower_shield`
- `knight_helm`
- `swift_boots`

Steel is a straightforward progression tier above iron. Existing orphaned art files already use these IDs or names, so implementation must check the filesystem before generating replacements and reuse existing assets when the file exists at the canonical path, is `96x96`, and has transparent background pixels.

Obsidian tier:

- `obsidian_blade`
- `obsidian_carapace`
- `obsidian_guard`
- `obsidian_crown`
- `obsidian_treads`

Obsidian is rarer dungeon loot with stronger focused stats. It must not be sold in the normal village shop.

Equipment remains limited to current stat bonuses: attack, defense, speed, and health. No special proc effects or set bonuses are part of this pass.

### Consumables

Add a small dungeon consumable set using existing consumable effect types:

| ID | Display Name | Effect Direction |
| --- | --- | --- |
| `major_health_potion` | Major Health Potion | Stronger instant heal than Greater Health Potion |
| `major_mana_potion` | Major Mana Potion | Stronger instant mana restore |
| `warding_charm` | Warding Charm | Fortify-style defensive buff |
| `smoke_bomb` | Smoke Bomb | Enemy blind debuff |

Each consumable must set `AssetPath`, value, rarity, stack behavior, and a real `ConsumableEffect`.

## Loot Design

Every new dungeon enemy must have a loot table that includes:

- Its monster part as the primary weighted drop.
- A small chance for steel equipment or relevant dungeon consumables.
- A rarer chance for obsidian equipment on elite enemies.

Existing dungeon tables must be refreshed:

- `DarkMage` must include `hexed_cloth` and at least one caster-themed dungeon reward.
- `DungeonGuardian` must include `sentinel_core`, `revenant_plate`, and at least one steel or obsidian gear drop.
- `DemonLord` must include `abyssal_sigil`, obsidian gear, and fewer generic iron drops than before.
- `Boss` may retain premium guaranteed drops, but must include at least one dungeon-specific reward.

`dragon_scale` remains useful for dragon-like enemies, but it must not be the default stand-in for all high-tier dungeon rewards.

## Reachability

Dungeon content must be reachable through current runtime paths:

1. Area-generated encounters in `Game.CreateEnemyByArea()` must include the new dungeon enemies in the dungeon area mix.
2. Visual marker logic in `GridMap.GetEnemyTypeForPosition()` must stay aligned with the dungeon encounter mix so map markers do not imply unrelated enemy types.
3. Existing GF and 1F scene-placed content must stay stable.
4. Static dungeon spawns must not be added in this pass. Floor layout work stays separate.

Loot flow stays unchanged:

`Enemy.EnemyType` -> `LootTableCatalog.GetByEnemyType()` -> `LootManager.RollLoot()` -> player inventory or `RecoveryChest`.

## Asset Workflow

Asset work must follow `.codex/skills/manage-asset-generation/SKILL.md`.

General rules:

- Identify the canonical runtime path from code or catalog first.
- Run direct filesystem existence checks before generating.
- Do not regenerate or overwrite existing files unless explicitly requested.
- Inspect same-category shipped assets before generating.
- Copy generated output into the canonical repo path under `assets/`.
- Run category-specific post-processing.
- Verify final file existence and dimensions.
- Update docs to match the filesystem.

Enemy sprite rules:

- Canonical runtime path: `assets/sprites/enemies/{enemy_type}/sprite_sheet.png`.
- Generate four frames as `assets/sprites/enemies/{enemy_type}/frames/frame1.png` through `frame4.png` only when the runtime sheet is missing.
- Run `python3 tools/sprite_sheet_merger.py`.
- Verify merged sheet size is `384x96`.
- Keep fallback rendering valid if a sheet is absent.

Item icon rules:

- Equipment paths come from `EquipmentCatalog.AssetPath`.
- Consumables and monster parts must also set `AssetPath`.
- Final repo icons must follow the current `96x96` convention.
- Use `tools/resize_item_icons.py` for alpha cleanup and resizing.
- Verify dimensions and real transparency after saving.

Docs to update:

- `docs/enemies/ENEMY_SPRITES.md`
- `docs/items/ASSET_STATUS.md`
- `docs/items/ITEM_PROMPT_GUIDE.md`
- `docs/items/items-guide.md`

## Error Handling And Fallbacks

- Unknown enemy types must continue to fall back to Goblin through existing `EnemySpawn` behavior.
- Missing enemy sprite sheets must continue to render fallback rectangles.
- Missing or unknown loot item IDs must continue to be skipped by `LootManager`, but tests must prevent new dungeon loot from relying on that fallback.
- Consumables must not be registered without a usable effect.
- Asset docs must be corrected when they disagree with the filesystem.

## Testing Plan

Add focused tests for the content chain:

- Every new enemy factory creates an enemy with the expected ID, stats, rewards, and `EnemyType`.
- Every new enemy type returns a non-empty loot table.
- Every new dungeon loot entry resolves through `ItemCatalog.CreateItemById()`.
- New monster parts are registered, stackable, priced, and have `AssetPath`.
- New equipment items have expected slot, stats, rarity, value, and `AssetPath`.
- New consumables have expected effects, values, rarity, stack behavior, and `AssetPath`.
- Dungeon encounter selection and visual marker selection include all six new dungeon enemies.
- Existing tests for loot, item catalog, enemy spawn, and dungeon-related behavior continue passing.

Asset verification during implementation must include:

- Filesystem existence checks for each canonical path.
- Enemy sheet dimension checks for generated sheets.
- Item icon dimension and alpha checks.
- Docs checked against actual file state.

## Risks

Scope creep from generated art volume:

- Limit generation to new dungeon assets and assets that are missing at canonical paths.

Catalog drift:

- Add tests that connect enemy IDs, factories, loot tables, and item registrations.

Balance spikes:

- Keep steel as a normal upgrade above iron.
- Keep obsidian rare and dungeon-gated.
- Do not add new combat mechanics in this pass.

Asset/doc drift:

- Use filesystem checks as authoritative.
- Update docs only after confirming actual file state.

## Completion Criteria

The design is implemented when:

- Dungeon enemy factories, loot tables, item catalogs, and encounter logic are wired.
- Dungeon loot resolves without unknown item IDs.
- New dungeon equipment and consumables are usable through existing inventory/combat systems.
- Required enemy and item art assets exist at canonical paths and pass the category-specific checks.
- Asset docs match the filesystem.
- Focused tests and a project build pass.
