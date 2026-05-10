# Sirius RPG — Item Asset Prompt Guide

## Overview

This document is the authoritative reference for item image assets. It is generated from
`EquipmentCatalog.cs`, `ConsumableCatalog.cs`, and `MonsterPartsCatalog.cs` and should be
kept in sync with those files when items are added or removed.

Asset paths listed here are repo-relative paths corresponding to the catalog `AssetPath` with the `res://` prefix omitted.

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
| ✅ exists | `iron_armor` | `assets/sprites/items/armor/iron_armor.png` |
| ✅ exists | `iron_shield` | `assets/sprites/items/shields/iron_shield.png` |
| ✅ exists | `iron_helmet` | `assets/sprites/items/helmet/iron_helmet.png` |
| ✅ exists | `iron_boots` | `assets/sprites/items/shoes/iron_boots.png` |

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

Consumables define `AssetPath` in `ConsumableCatalog.cs`.

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

Monster parts define `AssetPath` in `MonsterPartsCatalog.cs`.

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

---

## Orphaned Assets

These PNG files exist on disk but are **not referenced** by any item in the current catalogs.
They may be kept as source material but should not be imported into Godot as active assets.

| File | Former Item | Notes |
|------|-------------|-------|
| `assets/sprites/items/consumables/minor_health_potion.png` | minor_health_potion | ID is now `health_potion` |
| `assets/sprites/items/consumables/mana_berry.png` | mana_berry | No matching catalog entry |
| `assets/sprites/items/consumables/elixir_of_fortitude.png` | elixir_of_fortitude | No matching catalog entry |

> `assets/sprites/items/weapons/iron_sword.png` now exists. Godot import metadata (`.import` files) is generated locally by the editor and is not tracked in this repository.

---

## AI Prompt Templates

### Prompt Structure

```
"Create a 64x64 anime-style inventory icon of [item description], transparent background.
[angle]. Bold 2px outline, cel shading with [material/color details].
Lighting from top-left with soft rim glow. Centered in frame, no background elements."
```

After generation, save the full-size source PNG into a source directory (by default
`assets/sprites/items/original/`), then run the resize tool to write the final asset into the
appropriate destination tree. `tools/resize_item_icons.py` resizes images from a **source directory**
into a **destination directory** and can strip edge-connected opaque matte backgrounds when the
generator fails to emit real alpha. It does not resize a single repo copy in place. To avoid
unintentionally processing the entire default source directory, prefer passing explicit `--source` and
`--dest` paths when working on a single item or subset, for example:
`python3 tools/resize_item_icons.py --source assets/sprites/items/original --dest assets/sprites/items --size 96`

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

**Iron Armor** (`iron_armor.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a heavy iron chestplate with riveted pauldrons,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with dark grey
> iron plates and subtle rust highlights at edges. Soft top-left lighting, centered, no background."

**Iron Shield** (`iron_shield.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a solid iron kite shield with a raised central boss
> and reinforced rim, transparent background. Three-quarter top-down angle, bold outline, cel shading
> with dark grey metal and blue-grey shadow accents. Soft top-left lighting, no extra elements."

**Iron Helmet** (`iron_helmet.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of an iron open-faced helmet with cheek guards and a
> nasal bar, transparent background. Three-quarter top-down angle, bold outline, cel shading with
> dark iron and cool metallic reflections. Soft top-left lighting, no background."

**Iron Boots** (`iron_boots.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of heavy iron-plated boots with leather straps,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with dark grey
> iron plates over brown leather. Soft top-left lighting, no background elements."

### Dungeon Steel Tier

**Steel Longsword** (`steel_longsword.png`) — *asset exists; reused from existing repo asset after filesystem and transparency verification*
> "Create a 64x64 anime-style inventory icon of a polished steel longsword with leather grip,
> transparent background. Three-quarter top-down angle, bold 2px outline, cel shading with bright
> steel highlights and a dark leather wrap. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

**Chain Mail** (`chain_mail.png`) — *asset exists; reused from existing repo asset after filesystem and transparency verification*
> "Create a 64x64 anime-style inventory icon of a folded chain mail shirt with steel rings,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with interlinked
> steel ring texture and cool grey highlights. Lighting from top-left with soft rim glow. Centered
> in frame, no background elements."

**Steel Tower Shield** (`steel_tower_shield.png`) — *asset exists; reused from existing repo asset after filesystem and transparency verification*
> "Create a 64x64 anime-style inventory icon of a tall reinforced steel tower shield,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with polished
> steel plates, rivets, and reinforced edges. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

**Knight Helm** (`knight_helm.png`) — *asset exists; reused from existing repo asset after filesystem and transparency verification*
> "Create a 64x64 anime-style inventory icon of an open-faced steel knight helmet with cheek guards,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with clean steel
> highlights and dark interior shadow. Lighting from top-left with soft rim glow. Centered in frame,
> no background elements."

**Swift Boots** (`swift_boots.png`) — *asset exists; reused from existing repo asset after filesystem and transparency verification*
> "Create a 64x64 anime-style inventory icon of light steel-capped adventurer boots,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with brown leather,
> bright steel toe caps, and agile silhouette. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

### Dungeon Obsidian Tier

**Obsidian Blade** (`obsidian_blade.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a black glass sword with purple edge glow,
> transparent background. Three-quarter top-down angle, bold 2px outline, cel shading with glossy
> obsidian facets and a violet magical edge. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

**Obsidian Carapace** (`obsidian_carapace.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of black segmented armor plates with violet highlights,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with glossy black
> plate segments and purple reflections. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

**Obsidian Guard** (`obsidian_guard.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a polished obsidian shield with etched warding lines,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with black glass
> shine and violet etched runes. Lighting from top-left with soft rim glow. Centered in frame, no
> background elements."

**Obsidian Crown** (`obsidian_crown.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a crown-like obsidian helmet with abyssal gem,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with black crystal
> points, violet reflections, and a dark central gem. Lighting from top-left with soft rim glow.
> Centered in frame, no background elements."

**Obsidian Treads** (`obsidian_treads.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of black plated boots with violet sole glow,
> transparent background. Three-quarter top-down angle, bold outline, cel shading with glossy black
> plates and purple glow under the soles. Lighting from top-left with soft rim glow. Centered in
> frame, no background elements."

---

## Consumable Prompts

**Health Potion** (`health_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small round potion bottle filled with bright red
> liquid and a cork stopper, transparent background. Straight-on hero shot, bold outline, cel shading
> with glass reflections and a liquid swirl. Soft top-left lighting, tiny heart sparkle, no background."

**Greater Health Potion** (`greater_health_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a large ornate potion bottle filled with deep crimson
> liquid, gold trim, and a wax-sealed stopper, transparent background. Straight-on hero shot, bold
> outline, cel shading with rich glass reflections and a swirling liquid interior. Soft top-left
> lighting, glowing red aura, no background. Visibly larger and more impressive than a basic health
> potion."

**Mana Potion** (`mana_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small glass vial filled with glowing blue liquid
> and a cork stopper, transparent background. Straight-on hero shot, bold outline, cel shading with
> cool blue glass reflections. Soft top-left lighting, magical sparkle, no background."

**Strength Tonic** (`strength_tonic.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a square flask filled with orange-red bubbling liquid,
> transparent background. Straight-on hero shot, bold outline, cel shading with amber tones and rising
> bubble effects. Soft top-left lighting, faint flame glow at base, no background."

**Iron Skin** (`iron_skin.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a grey-silver potion bottle with metallic sheen
> and a steel-capped stopper, transparent background. Straight-on hero shot, bold outline, cel shading
> with cool metallic grey tones and a subtle iron texture swirling inside. Soft top-left lighting,
> faint armor-plate highlight, no background."

**Swiftness Draught** (`swiftness_draught.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a slim tapered flask filled with bright green-yellow
> sparkling liquid, transparent background. Straight-on hero shot, bold outline, cel shading with
> vivid lime tones. Add faint horizontal speed streaks around the flask. Soft top-left lighting,
> no background."

**Antidote** (`antidote.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small vial filled with bright green liquid and
> a leaf motif on the label, transparent background. Straight-on hero shot, bold outline, cel shading
> with fresh green tones and a clean white cork. Soft top-left lighting, small leaf sparkles,
> no background."

**Regen Potion** (`regen_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a round flask filled with soft pink-white glowing
> liquid with a heart motif etched on the glass, transparent background. Straight-on hero shot,
> bold outline, cel shading with warm pink tones and a gentle glow. Soft top-left lighting,
> tiny healing cross sparkle, no background."

**Poison Vial** (`poison_vial.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a dark vial filled with murky purple-green toxic
> liquid and a skull-embossed stopper, transparent background. Straight-on hero shot, bold outline,
> cel shading with deep purple and sickly green swirling inside. Soft top-left lighting, dripping
> droplet accent, no background."

**Flash Powder** (`flash_powder.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small paper pouch tied with string, filled with
> glowing yellow-white powder leaking from the top, transparent background. Straight-on hero shot,
> bold outline, cel shading with bright white-gold hues and radiant burst lines. Soft top-left
> lighting, starburst glow, no background."

**Major Health Potion** (`major_health_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of an ornate large crimson potion bottle with gold trim,
> transparent background. Straight-on hero shot, bold outline, cel shading with rich red glass,
> gold accents, and a swirling healing liquid. Soft top-left lighting, warm healing glow, no background."

**Major Mana Potion** (`major_mana_potion.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of an ornate large blue potion bottle with silver trim,
> transparent background. Straight-on hero shot, bold outline, cel shading with luminous blue glass,
> silver accents, and a magical liquid swirl. Soft top-left lighting, cool mana glow, no background."

**Warding Charm** (`warding_charm.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small protective charm with blue shield rune,
> transparent background. Straight-on hero shot, bold outline, cel shading with polished charm
> material and a bright blue defensive symbol. Soft top-left lighting, subtle barrier glow, no background."

**Smoke Bomb** (`smoke_bomb.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a round black smoke bomb with short fuse,
> transparent background. Straight-on hero shot, bold outline, cel shading with matte black casing,
> tied seam, and a small fuse. Soft top-left lighting, faint grey smoke wisps, no background."

---

## Monster Part Prompts

**Goblin Ear** (`goblin_ear.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a pointed green goblin ear with a small gold earring,
> transparent background. Straight-on hero shot, bold outline, cel shading with muted green skin tones.
> Soft top-left lighting, no background."

**Orc Tusk** (`orc_tusk.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a heavy yellowed orc tusk with a rough broken base,
> transparent background. Straight-on hero shot, bold outline, cel shading with ivory yellow tones
> and brown root staining. Soft top-left lighting, no background."

**Skeleton Bone** (`skeleton_bone.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a single femur bone with a faint dark energy aura,
> transparent background. Straight-on hero shot, bold outline, cel shading with aged off-white tones
> and a subtle purple glow at the joints. Soft top-left lighting, no background."

**Spider Silk** (`spider_silk.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a shimmering bundle of iridescent white silk threads
> loosely wound into a spool, transparent background. Straight-on hero shot, bold outline, cel shading
> with cool silver-white and faint rainbow shimmer. Soft top-left lighting, no background."

**Dragon Scale** (`dragon_scale.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a single large iridescent dragon scale with intricate
> surface patterns, transparent background. Straight-on hero shot, bold outline, cel shading with deep
> teal-gold hues and a metallic rainbow sheen. Soft top-left lighting, magical radiance glow,
> no background. Convey extreme rarity."

**Sentinel Core** (`sentinel_core.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a stone-and-metal glowing blue construct core,
> transparent background. Straight-on hero shot, bold outline, cel shading with cracked stone,
> steel bands, and a bright blue inner glow. Soft top-left lighting, no background."

**Hexed Cloth** (`hexed_cloth.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a torn violet cloth scrap with green runes,
> transparent background. Straight-on hero shot, bold outline, cel shading with frayed fabric,
> glowing green curse marks, and deep violet folds. Soft top-left lighting, no background."

**Splintered Bone** (`splintered_bone.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a sharp pale bone fragment with cracks,
> transparent background. Straight-on hero shot, bold outline, cel shading with pale ivory tones,
> jagged edges, and dark crack lines. Soft top-left lighting, no background."

**Revenant Plate** (`revenant_plate.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a battered dark armor plate with red glow,
> transparent background. Straight-on hero shot, bold outline, cel shading with scratched blackened
> iron, dented edges, and a faint red undead glow. Soft top-left lighting, no background."

**Gargoyle Shard** (`gargoyle_shard.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a jagged black stone shard with ember cracks,
> transparent background. Straight-on hero shot, bold outline, cel shading with rough black stone
> texture and orange ember light in the fractures. Soft top-left lighting, no background."

**Abyssal Sigil** (`abyssal_sigil.png`) — *asset exists*
> "Create a 64x64 anime-style inventory icon of a small black-and-purple sigil token,
> transparent background. Straight-on hero shot, bold outline, cel shading with glossy black token
> material, purple carved symbol, and ominous magical glow. Soft top-left lighting, no background."

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
