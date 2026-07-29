# UI & Effects Sprite Guide

AI prompts and checklist for all UI backgrounds, battle buttons, status icons, and combat effects.
For overall asset status see `docs/items/ASSET_STATUS.md`.

---

## Current Repo References

- `assets/sprites/ui/ui_main_menu_background.png` — existing background, `1920×1080`
- `assets/sprites/ui/ui_battle_background.png` — existing background, `1280×720`
- `assets/sprites/ui/original/` contains reference copies of both existing shipped backgrounds
- HPA-374 catalog icons and effects are runtime assets; `UiArtCatalog` owns their paths and optional-load behavior.

## Generation Notes

- Check the exact runtime asset path on disk before generating.
- When replacing or extending a class that already has shipped art, match the existing repo dimensions first.
- For future UI buttons, icons, and effects with no same-class repo asset yet, use the target size in the checklist below.

---

## Production Checklist

### Backgrounds

| Status | File | Size | Loaded By |
|--------|------|------|-----------|
| ✅ exists | `assets/sprites/ui/ui_main_menu_background.png` | 1920×1080 | `MainMenu.cs:27` |
| ✅ exists | `assets/sprites/ui/ui_battle_background.png` | 1280×720 | `BattleManager.cs:180` |

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

### Catalog Effects (HPA-374)

| Status | File | Size | Mipmaps |
|--------|------|------|---------|
| ✅ exists | `assets/sprites/effects/ui/encounter_burst.png` | 256×256 | enabled |
| ✅ exists | `assets/sprites/effects/ui/hit_impact.png` | 256×256 | enabled |
| ✅ exists | `assets/sprites/effects/ui/status_pulse.png` | 256×256 | enabled |
| ✅ exists | `assets/sprites/effects/ui/reward_level_up.png` | 256×256 | enabled |

> These are static catalog resources. `UiArtCatalog.LoadEffect()` remains the single runtime loading path; this task does not add scene integration or animation playback.

---

## Background Prompts

**Main Menu Background** (`ui_main_menu_background.png`) — ✅ exists
> "Create a 1920x1080 anime-style background for an RPG main menu. Beautiful anime landscape with rolling hills, a distant castle, and magical sky. Bright anime colors with dramatic lighting. Include anime-style clouds, magical stars, and a large moon. Epic and adventurous, typical anime opening scene aesthetics — inspiring, grand. Cel-shading with vibrant blues, purples, and golden highlights."

**Battle Background** (`ui_battle_background.png`) — ✅ exists
> "Create a 1280x720 anime-style background for battle scenes. Mystical battleground with energy effects and dramatic sky. Bright anime colors with dynamic lighting. Include anime-style energy auras, floating particles, and dramatic shadows. Intense and exciting, anime battle aesthetics — dynamic, powerful. Cel-shading with deep purples, electric blues, and energy highlights."

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

Canonical HPA-374 prompts and their visual/reference contracts are recorded in `docs/ui/hpa-374/sources/prompts/effects.md`. The four catalog effects above supersede the older 96×96 planning placeholders.
