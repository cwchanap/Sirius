# Sirius RPG — Item Asset Prompt Guide

## Overview

This document is the authoritative reference for item image assets. It is generated from
`EquipmentCatalog.cs`, `ConsumableCatalog.cs`, and `MonsterPartsCatalog.cs` and should be
kept in sync with those files when items are added or removed.

All asset paths listed here must match the `AssetPath` field in the catalog source exactly.

---

## Global Art Direction

- **Style**: Clean Japanese anime illustration with bold outlines and cel shading
- **Generation resolution**: 64×64 pixels or larger source image, transparent PNG background
- **Final repo asset size**: Resize saved item icons to match the existing repo item icon convention, which is currently 96×96 for the wooden equipment set
- **Lighting**: Soft top-left key light, subtle rim glow for readability
- **Color Palette**: Saturated anime hues with high contrast between silhouette and interior details
- **Outline**: 2px bold outline around icon, inner line work as needed
- **Background**: Transparent; avoid drop shadows so icons fit on various UI backgrounds
- **Angle**: Three-quarter top-down for equipment; straight-on hero shot for consumables
- **Consistency**: Reuse material colors across upgrade tiers (wood → iron → steel progression)

---

## Directory Layout

```
assets/sprites/items/
├── weapons/
├── armor/
├── shields/
├── helmet/
├── shoes/
├── consumables/
└── monster_parts/
```

File name = item `Id` in the catalog (e.g. `iron_sword` → `iron_sword.png`).

---

## Asset Status

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

### Consumables

Consumables do not currently define an `AssetPath` in `ConsumableCatalog.cs`.
The paths below are the **intended** locations following the naming convention.

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

Monster parts do not currently define an `AssetPath` in `MonsterPartsCatalog.cs`.
The paths below are the **intended** locations.

| Status | ID | Intended Asset Path |
|--------|----|-------------------|
| ❌ missing | `goblin_ear` | `assets/sprites/items/monster_parts/goblin_ear.png` |
| ❌ missing | `orc_tusk` | `assets/sprites/items/monster_parts/orc_tusk.png` |
| ❌ missing | `skeleton_bone` | `assets/sprites/items/monster_parts/skeleton_bone.png` |
| ❌ missing | `spider_silk` | `assets/sprites/items/monster_parts/spider_silk.png` |
| ❌ missing | `dragon_scale` | `assets/sprites/items/monster_parts/dragon_scale.png` |

---

## Orphaned Assets

These PNG files exist on disk but are **not referenced** by any item in the current catalogs.
They may be kept as source material but should not be imported into Godot as active assets.

| File | Former Item | Notes |
|------|-------------|-------|
| `assets/sprites/items/weapons/steel_longsword.png` | steel_longsword | Replaced by iron_sword |
| `assets/sprites/items/armor/chain_mail.png` | chain_mail | Replaced by iron_armor |
| `assets/sprites/items/shields/steel_tower_shield.png` | steel_tower_shield | Replaced by iron_shield |
| `assets/sprites/items/helmet/knight_helm.png` | knight_helm | Replaced by iron_helmet |
| `assets/sprites/items/shoes/swift_boots.png` | swift_boots | Replaced by iron_boots |
| `assets/sprites/items/consumables/minor_health_potion.png` | minor_health_potion | ID is now `health_potion` |
| `assets/sprites/items/consumables/mana_berry.png` | mana_berry | No matching catalog entry |
| `assets/sprites/items/consumables/elixir_of_fortitude.png` | elixir_of_fortitude | No matching catalog entry |

> `assets/sprites/items/weapons/iron_sword.png` now exists, so the matching `.import` file is no longer stale.

---

## AI Prompt Templates

### Prompt Structure

```
"Create a 64x64 anime-style inventory icon of [item description], transparent background.
[angle]. Bold 2px outline, cel shading with [material/color details].
Lighting from top-left with soft rim glow. Centered in frame, no background elements."
```

After generation, resize the repo copy to match the existing item icon size used by comparable assets, currently `python3 tools/resize_item_icons.py --size 96` for the wooden equipment set.

- Equipment: use **three-quarter top-down** angle
- Consumables / Monster Parts: use **straight-on hero shot**

---

## Equipment Prompts

### Wooden Tier

**Wooden Sword** (`wooden_sword.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a beginner wooden sword, transparent background.
> Three-quarter top-down angle, bold 2px outline, cel shading with warm brown wood grain,
> leather-wrapped grip. Lighting from top-left with soft rim glow. Sword centered diagonally,
> no background."

**Wooden Armor** (`wooden_armor.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a wooden chestplate with rope bindings, transparent
> background. Three-quarter top-down angle, bold outline, cel shading with warm browns and subtle
> carved details. Soft top-left lighting, centered, no background."

**Wooden Shield** (`wooden_shield.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a round wooden shield with iron rim and central boss,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with warm wood
> planks and cool metal highlights. Soft top-left lighting, no extra elements."

**Wooden Helmet** (`wooden_helmet.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a carved wooden helmet with leather chin strap,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with rich browns
> and subtle carved runes. Soft top-left lighting, no background."

**Wooden Shoes** (`wooden_shoes.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of wooden geta sandals with red straps, transparent
> background. Three-quarter top-down angle, bold outline, cel shading with warm wood tones and
> bright strap color. Soft top-left lighting, centered, no background."

---

### Iron Tier

**Iron Sword** (`iron_sword.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a sturdy iron sword with a simple cross-guard and
> dark leather grip, transparent background. Three-quarter top-down angle, bold 2px outline,
> cel shading with cool grey iron and subtle forge-marks along the blade. Lighting from top-left,
> faint metallic sheen, no background."

**Iron Armor** (`iron_armor.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a heavy iron chestplate with riveted pauldrons,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with dark grey
> iron plates and subtle rust highlights at edges. Soft top-left lighting, centered, no background."

**Iron Shield** (`iron_shield.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a solid iron kite shield with a raised central boss
> and reinforced rim, transparent background. Three-quarter top-down angle, bold outline, cel shading
> with dark grey metal and blue-grey shadow accents. Soft top-left lighting, no extra elements."

**Iron Helmet** (`iron_helmet.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of an iron open-faced helmet with cheek guards and a
> nasal bar, transparent background. Three-quarter top-down angle, bold outline, cel shading with
> dark iron and cool metallic reflections. Soft top-left lighting, no background."

**Iron Boots** (`iron_boots.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of heavy iron-plated boots with leather straps,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with dark grey
> iron plates over brown leather. Soft top-left lighting, no background elements."

---

## Consumable Prompts

**Health Potion** (`health_potion.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a small round potion bottle filled with bright red
> liquid and a cork stopper, transparent background. Straight-on hero shot, bold outline, cel shading
> with glass reflections and a liquid swirl. Soft top-left lighting, tiny heart sparkle, no background."

**Greater Health Potion** (`greater_health_potion.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a large ornate potion bottle filled with deep crimson
> liquid, gold trim, and a wax-sealed stopper, transparent background. Straight-on hero shot, bold
> outline, cel shading with rich glass reflections and a swirling liquid interior. Soft top-left
> lighting, glowing red aura, no background. Visibly larger and more impressive than a basic health
> potion."

**Mana Potion** (`mana_potion.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a small glass vial filled with glowing blue liquid
> and a cork stopper, transparent background. Straight-on hero shot, bold outline, cel shading with
> cool blue glass reflections. Soft top-left lighting, magical sparkle, no background."

**Strength Tonic** (`strength_tonic.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a square flask filled with orange-red bubbling liquid,
> transparent background. Straight-on hero shot, bold outline, cel shading with amber tones and rising
> bubble effects. Soft top-left lighting, faint flame glow at base, no background."

**Iron Skin** (`iron_skin.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a grey-silver potion bottle with metallic sheen
> and a steel-capped stopper, transparent background. Straight-on hero shot, bold outline, cel shading
> with cool metallic grey tones and a subtle iron texture swirling inside. Soft top-left lighting,
> faint armor-plate highlight, no background."

**Swiftness Draught** (`swiftness_draught.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a slim tapered flask filled with bright green-yellow
> sparkling liquid, transparent background. Straight-on hero shot, bold outline, cel shading with
> vivid lime tones. Add faint horizontal speed streaks around the flask. Soft top-left lighting,
> no background."

**Antidote** (`antidote.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a small vial filled with bright green liquid and
> a leaf motif on the label, transparent background. Straight-on hero shot, bold outline, cel shading
> with fresh green tones and a clean white cork. Soft top-left lighting, small leaf sparkles,
> no background."

**Regen Potion** (`regen_potion.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a round flask filled with soft pink-white glowing
> liquid with a heart motif etched on the glass, transparent background. Straight-on hero shot,
> bold outline, cel shading with warm pink tones and a gentle glow. Soft top-left lighting,
> tiny healing cross sparkle, no background."

**Poison Vial** (`poison_vial.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a dark vial filled with murky purple-green toxic
> liquid and a skull-embossed stopper, transparent background. Straight-on hero shot, bold outline,
> cel shading with deep purple and sickly green swirling inside. Soft top-left lighting, dripping
> droplet accent, no background."

**Flash Powder** (`flash_powder.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a small paper pouch tied with string, filled with
> glowing yellow-white powder leaking from the top, transparent background. Straight-on hero shot,
> bold outline, cel shading with bright white-gold hues and radiant burst lines. Soft top-left
> lighting, starburst glow, no background."

---

## Monster Part Prompts

**Goblin Ear** (`goblin_ear.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a pointed green goblin ear with a small gold earring,
> transparent background. Straight-on hero shot, bold outline, cel shading with muted green skin tones.
> Soft top-left lighting, no background."

**Orc Tusk** (`orc_tusk.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a heavy yellowed orc tusk with a rough broken base,
> transparent background. Straight-on hero shot, bold outline, cel shading with ivory yellow tones
> and brown root staining. Soft top-left lighting, no background."

**Skeleton Bone** (`skeleton_bone.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a single femur bone with a faint dark energy aura,
> transparent background. Straight-on hero shot, bold outline, cel shading with aged off-white tones
> and a subtle purple glow at the joints. Soft top-left lighting, no background."

**Spider Silk** (`spider_silk.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a shimmering bundle of iridescent white silk threads
> loosely wound into a spool, transparent background. Straight-on hero shot, bold outline, cel shading
> with cool silver-white and faint rainbow shimmer. Soft top-left lighting, no background."

**Dragon Scale** (`dragon_scale.png`) — **⚠ needs generation**
> "Create a 64x64 anime-style inventory icon of a single large iridescent dragon scale with intricate
> surface patterns, transparent background. Straight-on hero shot, bold outline, cel shading with deep
> teal-gold hues and a metallic rainbow sheen. Soft top-left lighting, magical radiance glow,
> no background. Convey extreme rarity."

---

## Generation Checklist

Before submitting a prompt, confirm:

- [ ] Transparent background explicitly stated
- [ ] Anime cel-shaded style with bold 2px outline
- [ ] Material colors and special effects described
- [ ] Lighting direction (top-left) and glow accents mentioned
- [ ] Composition centered, no extra background elements
- [ ] File saved with exact item ID as filename (e.g. `iron_sword.png`)
- [ ] Placed in the correct subdirectory matching `AssetPath` in catalog

---

## Maintenance

When a new item is added to a catalog:
1. Add a row to the **Asset Status** table with `❌ missing`
2. Add an AI prompt under the appropriate section
3. After generating the asset, update the status to `✅ exists`
4. If an item is removed, move its entry to the **Orphaned Assets** table
