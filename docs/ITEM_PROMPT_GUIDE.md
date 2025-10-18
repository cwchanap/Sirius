# Sirius RPG - Item Asset Prompt Guide

## Overview
This document provides structured AI prompt templates for generating 64x64 inventory icons for Sirius RPG items. Prompts follow the same anime-inspired visual style as character sprites defined in `ASSET_REQUIREMENTS.md` while adapting to the smaller icon format used in inventory and UI elements.

## Global Art Direction
- **Style**: Clean Japanese anime illustration with bold outlines and cel shading
- **Resolution**: 64x64 pixels, transparent PNG background
- **Lighting**: Soft top-left key light, subtle rim glow for readability
- **Color Palette**: Saturated anime hues with high contrast between silhouette and interior details
- **Outline**: 2px bold outline around icon, inner line work as needed
- **Background**: Transparent; avoid additional drop shadows so icons fit on various UI backgrounds
- **Angle**: Three-quarter top-down for equipment, straight-on hero shot for consumables/gems
- **Consistency**: Reuse material colors and motifs across upgrade tiers (e.g., wood → iron → steel progression)

## Directory & Naming
```
assets/
└── sprites/
    └── items/
        ├── weapons/
        │   ├── wooden_sword.png
        │   └── iron_sword.png
        ├── armor/
        │   ├── wooden_armor.png
        │   └── chain_mail.png
        ├── accessories/
        │   └── charm_of_swiftness.png
        └── consumables/
            ├── minor_health_potion.png
            └── mana_berry.png
```
- File name matches item `Id` in code (`EquipmentCatalog.cs`, `ConsumableCatalog.cs`)
- All files exported as PNG with alpha channel

## Asset Generation Checklist

### Weapons
- [x] Wooden Sword (`assets/sprites/items/weapons/wooden_sword.png`)
- [ ] Steel Longsword (`assets/sprites/items/weapons/steel_longsword.png`)

### Armor
- [x] Wooden Armor (`assets/sprites/items/armor/wooden_armor.png`)
- [x] Chain Mail (`assets/sprites/items/armor/chain_mail.png`)

### Shields
- [x] Wooden Shield (`assets/sprites/items/shields/wooden_shield.png`)
- [x] Steel Tower Shield (`assets/sprites/items/shields/steel_tower_shield.png`)

### Helmets
- [x] Wooden Helmet (`assets/sprites/items/helmet/wooden_helmet.png`)
- [x] Knight Helm (`assets/sprites/items/helmet/knight_helm.png`)

### Footwear
- [x] Wooden Shoes (`assets/sprites/items/shoes/wooden_shoes.png`)
- [x] Swift Boots (`assets/sprites/items/shoes/swift_boots.png`)

### Accessories
- [ ] Charm of Vitality (`assets/sprites/items/accessories/charm_of_vitality.png`)
- [ ] Ring of Focus (`assets/sprites/items/accessories/ring_of_focus.png`)

### Consumables
- [ ] Minor Health Potion (`assets/sprites/items/consumables/minor_health_potion.png`)
- [ ] Mana Berry (`assets/sprites/items/consumables/mana_berry.png`)
- [ ] Elixir of Fortitude (`assets/sprites/items/consumables/elixir_of_fortitude.png`)

### Crafting Materials & Quest Items
- [ ] Ancient Rune Fragment (`assets/sprites/items/materials/ancient_rune_fragment.png`)
- [ ] Goblin Camp Map (`assets/sprites/items/quest/goblin_camp_map.png`)

## Prompt Template Structure
Each prompt follows a base pattern with item-specific details. Replace bracketed sections accordingly.

```
"Create a 64x64 anime-style inventory icon of a [item description], transparent background. Three-quarter top-down angle, bold 2px outline, cel shading with saturated colors. [Material/element details]. Lighting from top-left with soft rim glow. Present the item centered in frame with no extra background elements."
```

Add optional embellishments for rarity (glow, floating particles) but keep icon readable at small size.

## Equipment Prompts

### Weapons
- **Wooden Sword** (`wooden_sword.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a beginner wooden sword, transparent background. Three-quarter top-down angle, bold 2px outline, cel shading with warm brown wood grain, leather-wrapped grip. Lighting from top-left with soft rim glow. Present the sword centered diagonally without extra background elements."

- **Steel Longsword** (`steel_longsword.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a polished steel longsword with blue leather grip and gold crossguard jewel, transparent background. Three-quarter top-down angle, bold outline, cel shading with cool steel reflections. Soft top-left lighting and faint magical sparkle around the gem. No extra background."

### Armor
- **Wooden Armor** (`wooden_armor.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a wooden chestplate with rope bindings, transparent background. Three-quarter top-down angle, bold outline, cel shading with warm browns and subtle carved details. Soft top-left lighting, centered view, no background."

- **Chain Mail** (`chain_mail.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of shimmering chain mail folded neatly, transparent background. Three-quarter top-down angle, bold outline, cel shading with silver-gray rings and blue shadow accents. Soft top-left light, slight sparkle, no background."

### Shields
- **Wooden Shield** (`wooden_shield.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a round wooden shield with iron rim and central boss, transparent background. Three-quarter top-down angle, bold outline, cel shading with warm wood planks and cool metal highlights. Soft top-left lighting, no extra elements."

- **Steel Tower Shield** (`steel_tower_shield.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a tall steel tower shield with embossed wings and blue enamel crest, transparent background. Three-quarter top-down angle, bold outline, cel shading with reflective steel and saturated blue accent. Soft top-left lighting, centered."

### Helmets
- **Wooden Helmet** (`wooden_helmet.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a carved wooden helmet with leather chin strap, transparent background. Three-quarter top-down angle, bold outline, cel shading with rich browns and subtle carved runes. Soft top-left lighting, no background."

- **Knight Helm** (`knight_helm.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a polished knight helmet with blue plume, transparent background. Three-quarter top-down angle, bold outline, cel shading with silver metal, bright blue plume, top-left light, faint sparkles, no extra background."

### Footwear
- **Wooden Shoes** (`wooden_shoes.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of wooden geta sandals with red straps, transparent background. Three-quarter top-down angle, bold outline, cel shading with warm wood tones and bright strap color. Soft top-left lighting, centered, no background."

- **Swift Boots** (`swift_boots.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of sleek leather boots with silver wing motifs, transparent background. Three-quarter top-down angle, bold outline, cel shading with rich brown leather and metallic silver wings. Add faint motion streaks to suggest speed, no background elements."

## Accessory Prompts
- **Charm of Vitality** (`charm_of_vitality.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a heart-shaped ruby amulet with golden chain, transparent background. Straight-on hero shot, bold outline, cel shading with vibrant reds and gold. Soft top-left lighting, gentle magical sparkle, no background."

- **Ring of Focus** (`ring_of_focus.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a silver ring with blue arcane gem levitating slightly, transparent background. Straight-on hero shot, bold outline, cel shading with cool silver and glowing blue. Add small floating rune particles. No background."

## Consumable Prompts
- **Minor Health Potion** (`minor_health_potion.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a small round potion bottle filled with bright red liquid, cork stopper, transparent background. Straight-on hero shot, bold outline, cel shading with glass reflections and liquid swirl. Soft top-left lighting, tiny heart sparkle, no background."

- **Mana Berry** (`mana_berry.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a cluster of glowing indigo berries with leaves, transparent background. Straight-on hero shot, bold outline, cel shading with luminous purple-blue hues. Soft top-left lighting, subtle magical sparkles, no background."

- **Elixir of Fortitude** (`elixir_of_fortitude.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of an ornate elixir bottle with gold trim and teal liquid core, transparent background. Straight-on hero shot, bold outline, cel shading with reflective glass and metallic highlights. Soft top-left lighting, aura glow around liquid, no extra background."

## Crafting Materials & Quest Items
- **Ancient Rune Fragment** (`ancient_rune_fragment.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a cracked stone tablet fragment with glowing cyan runes, transparent background. Three-quarter top-down angle, bold outline, cel shading with rough stone texture and cyan glow. Soft top-left lighting, floating rune shards, no background."

- **Goblin Camp Map** (`goblin_camp_map.png`)
  - Prompt: "Create a 64x64 anime-style inventory icon of a rolled parchment map with red wax seal and goblin claw marks, transparent background. Three-quarter top-down angle, bold outline, cel shading with aged paper tones and bright red seal. Soft top-left lighting, no background elements."

## Prompt Checklist
- **[ ]** Include transparent background callout
- **[ ]** Specify anime cel-shaded style with bold outline
- **[ ]** Describe material colors and special effects
- **[ ]** Mention lighting direction and glow accents
- **[ ]** Keep composition centered and background-free

Use this guide as a living document—add new prompts as items enter the catalog. Keep naming synchronized with in-game item IDs to streamline import automation.
