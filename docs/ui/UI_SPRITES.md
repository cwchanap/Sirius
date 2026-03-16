# UI & Effects Sprite Guide

AI prompts and checklist for all UI backgrounds, battle buttons, status icons, and combat effects.
For overall asset status see `docs/items/ASSET_STATUS.md`.

---

## Production Checklist

### Backgrounds

| Status | File | Size | Loaded By |
|--------|------|------|-----------|
| ✅ exists | `assets/sprites/ui/ui_main_menu_background.png` | 320×240 | `BattleManager.cs:710` |
| ✅ exists | `assets/sprites/ui/ui_battle_background.png` | 320×240 | `BattleManager.cs:763` |

### Battle Buttons

| Status | File | Size |
|--------|------|------|
| ❌ missing | `assets/sprites/ui/ui_button_attack.png` | 64×32 |
| ❌ missing | `assets/sprites/ui/ui_button_defend.png` | 64×32 |
| ❌ missing | `assets/sprites/ui/ui_button_run.png` | 64×32 |

### Status Icons

| Status | File | Size |
|--------|------|------|
| ❌ missing | `assets/sprites/ui/icon_health.png` | 16×16 |
| ❌ missing | `assets/sprites/ui/icon_experience.png` | 16×16 |
| ❌ missing | `assets/sprites/ui/icon_level.png` | 16×16 |

### Combat Effects

| Status | File | Size |
|--------|------|------|
| ❌ missing | `assets/sprites/effects/effect_hit_impact.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_magic_sparkles.png` | 96×96 |
| ❌ missing | `assets/sprites/effects/effect_level_up.png` | 96×96 |

> Combat effects have no loading code yet — reserved for future battle animations.

---

## Background Prompts

**Main Menu Background** (`ui_main_menu_background.png`) — ✅ exists
> "Create a 320x240 anime-style background for an RPG main menu. Beautiful anime landscape with rolling hills, a distant castle, and magical sky. Bright anime colors with dramatic lighting. Include anime-style clouds, magical stars, and a large moon. Epic and adventurous, typical anime opening scene aesthetics — inspiring, grand. Cel-shading with vibrant blues, purples, and golden highlights."

**Battle Background** (`ui_battle_background.png`) — ✅ exists
> "Create a 320x240 anime-style background for battle scenes. Mystical battleground with energy effects and dramatic sky. Bright anime colors with dynamic lighting. Include anime-style energy auras, floating particles, and dramatic shadows. Intense and exciting, anime battle aesthetics — dynamic, powerful. Cel-shading with deep purples, electric blues, and energy highlights."

---

## Battle Button Prompts

**Attack Button** (`ui_button_attack.png`) — ❌ needs generation
> "Create a 64x32 anime-style button sprite labeled 'ATTACK'. Bright red background with bold yellow text, anime-style sword icon, energy effects around the border. Bold black outlines. Exciting and action-oriented, anime UI aesthetics — dynamic, attention-grabbing. Include animation-ready highlights. Cel-shading with vibrant reds, yellows, energy effects. Transparent background."

**Defend Button** (`ui_button_defend.png`) — ❌ needs generation
> "Create a 64x32 anime-style button sprite labeled 'DEFEND'. Bright blue background with white text, anime-style shield icon, protective aura effects around the border. Bold black outlines. Reliable and protective, anime UI aesthetics — solid, trustworthy. Include subtle glow effects. Cel-shading with vibrant blues, whites, protective light effects. Transparent background."

**Run Button** (`ui_button_run.png`) — ❌ needs generation
> "Create a 64x32 anime-style button sprite labeled 'RUN'. Bright green background with white text, anime-style wind/speed lines icon, motion effects around the border. Bold black outlines. Fast and urgent, anime UI aesthetics — energetic, swift. Include motion blur effects. Cel-shading with vibrant greens, whites, speed line effects. Transparent background."

---

## Status Icon Prompts

**Health Icon** (`icon_health.png`) — ❌ needs generation
> "Create a 16x16 anime-style heart icon. Bright red with anime-style highlights, small sparkles, bold black outline. Cel-shading. Vital and life-giving, anime magical aesthetics — glowing, precious. Include subtle pulse effects. Vibrant reds, pinks, magical highlights. Transparent background."

**Experience Icon** (`icon_experience.png`) — ❌ needs generation
> "Create a 16x16 anime-style star icon. Bright yellow-gold with anime-style sparkles, energy radiating from points, bold black outline. Cel-shading. Valuable and empowering, anime power-up aesthetics — shining, magical. Include energy effects. Vibrant golds, yellows, light effects. Transparent background."

**Level Icon** (`icon_level.png`) — ❌ needs generation
> "Create a 16x16 anime-style upward arrow icon. Bright blue with anime-style energy trails, upward motion lines, bold black outline. Cel-shading. Progressive and inspiring, anime growth aesthetics — ascending, powerful. Include upward energy effects. Vibrant blues, whites, ascending light trails. Transparent background."

---

## Combat Effect Prompts

**Hit Impact** (`effect_hit_impact.png`) — ❌ needs generation
> "Create a 96x96 anime-style impact effect sprite. Explosive impact with anime-style action lines, energy bursts, and dynamic shapes. Bright anime colors with bold black outlines. Powerful and dramatic, anime battle effect aesthetics — intense, impactful. Include starburst patterns and energy waves. Vibrant oranges, yellows, white highlights. Transparent background."

**Magic Sparkles** (`effect_magic_sparkles.png`) — ❌ needs generation
> "Create a 96x96 anime-style magical sparkle effect sprite. Floating sparkles, magical particles, and energy wisps. Bright anime colors with soft glowing effects. Mystical and beautiful, anime magic aesthetics — ethereal, enchanting. Include various sized sparkles and light particles. Vibrant purples, blues, golden magical effects. Transparent background."

**Level Up** (`effect_level_up.png`) — ❌ needs generation
> "Create a 96x96 anime-style level up effect sprite. Triumphant light rays, rising stars, and celebratory sparkles. Bright anime colors with bold highlights. Victorious and empowering, anime achievement aesthetics — glorious, uplifting. Include ascending light beams and celebration particles. Vibrant golds, whites, rainbow highlights. Transparent background."
