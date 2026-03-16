# Terrain Sprite Guide

AI prompts and checklist for all terrain tiles — floors, walls, stairs, and gates.
For overall asset status see `docs/items/ASSET_STATUS.md`.

---

## Art Style Guidelines

- **Style**: Japanese anime with cel-shading
- **Floor/Wall tiles**: 32×32 pixels, transparent PNG
- **Stair/Gate tiles**: 32×32 pixels, transparent PNG
- **Outlines**: Bold black lines for tile definition
- **Colors**: Bright, saturated, area-appropriate palette

### Area → Floor Tile Mapping (from `GridMap.cs`)

| Area key | File |
|----------|------|
| `"starting_area"` | `floor_starting_area.png` |
| `"forest"` | `floor_forest.png` |
| `"cave"` | `floor_cave.png` |
| `"desert"` | `floor_desert.png` |
| `"swamp"` | `floor_swamp.png` |
| `"mountain"` | `floor_mountain.png` |
| `"dungeon"` | `floor_dungeon.png` |
| `"boss_arena"` | `floor_boss_arena.png` (not yet wired in GridMap) |

---

## Production Checklist

### Floor & Wall Tiles

| Status | File | Size |
|--------|------|------|
| ✅ exists | `assets/sprites/terrain/floor_starting_area.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_forest.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_cave.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_desert.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_swamp.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_mountain.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/floor_dungeon.png` | 32×32 |
| ✅ exists | `assets/sprites/terrain/wall_generic.png` | 32×32 |
| ❌ missing | `assets/sprites/terrain/floor_boss_arena.png` | 32×32 |

### Stair & Gate Tiles

| Status | File | Purpose |
|--------|------|---------|
| ✅ exists | `assets/sprites/terrain/stair_up.png` | Floor-to-floor ascent |
| ✅ exists | `assets/sprites/terrain/stair_down.png` | Floor-to-floor descent |
| ❌ missing | `assets/sprites/terrain/stair_left.png` | Lateral transition (left) |
| ❌ missing | `assets/sprites/terrain/stair_right.png` | Lateral transition (right) |
| ❌ missing | `assets/sprites/terrain/gate_north.png` | Same-floor scene gate |
| ❌ missing | `assets/sprites/terrain/gate_south.png` | Same-floor scene gate |
| ❌ missing | `assets/sprites/terrain/gate_west.png` | Same-floor scene gate |
| ❌ missing | `assets/sprites/terrain/gate_east.png` | Same-floor scene gate |

> **Stairs vs Gates**: Stairs = vertical floor transitions (up/down levels). Gates = horizontal
> same-floor scene transitions (e.g. castle exterior → interior). Both placed on `StairLayer`.
> See `STAIR_SETUP_GUIDE.md` and `CROSS_SCENE_STAIR_LINKING.md` for placement details.

---

## Floor Tile Prompts

**Starting Area** (`floor_starting_area.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a peaceful grass floor tile in top-down view. Short green grass with small colorful flowers scattered throughout. Bright anime colors with subtle black outlines. Welcoming and safe, anime pastoral aesthetics. Include tiny sparkles for a magical feel. Cel-shading with bright greens, colorful flower accents, subtle highlights. Transparent background."

**Forest** (`floor_forest.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a forest floor tile in top-down view. Dark earth with fallen leaves, small mushrooms, and tiny forest flowers. Bright anime colors with subtle black outlines. Mystical and natural, anime nature aesthetics — vibrant, alive. Include tiny glowing spores or fireflies. Cel-shading with rich browns, vibrant greens, magical light accents. Transparent background."

**Cave** (`floor_cave.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a cave floor tile in top-down view. Rough stone surface with small crystals and mineral deposits. Bright anime colors with bold black outlines. Mysterious and ancient, anime cave aesthetics — dramatic shadows. Include small glowing crystals. Cel-shading with grays, purples, crystal highlights. Transparent background."

**Desert** (`floor_desert.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a desert floor tile in top-down view. Golden sand with wind patterns and small scattered pebbles. Bright anime colors with subtle black outlines. Hot and vast, anime desert aesthetics — warm, sun-bleached. Include tiny heat shimmer effects. Cel-shading with warm yellows, oranges, sun highlights. Transparent background."

**Swamp** (`floor_swamp.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a swamp floor tile in top-down view. Muddy water with lily pads, cattails, and bubbles. Bright anime colors with bold black outlines. Murky but alive, anime wetland aesthetics — mysterious, atmospheric. Include water ripples and glowing marsh lights. Cel-shading with murky greens, browns, mysterious light effects. Transparent background."

**Mountain** (`floor_mountain.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a mountain floor tile in top-down view. Rocky ground with patches of snow and small alpine flowers. Bright anime colors with bold black outlines. Cold and high-altitude, anime mountain aesthetics — crisp, pristine. Include tiny snowflakes or ice crystals. Cel-shading with grays, whites, cool blue highlights. Transparent background."

**Dungeon** (`floor_dungeon.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a dungeon floor tile in top-down view. Ancient stone blocks with glowing runes and mystic symbols. Bright anime colors with bold black outlines. Ancient and magical, anime dungeon aesthetics — mysterious, powerful. Include glowing magical symbols. Cel-shading with dark grays, mystical blues, glowing accents. Transparent background."

**Boss Arena** (`floor_boss_arena.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of a boss arena floor tile in top-down view. Ornate stone with intricate patterns, magical circles, and energy lines. Bright anime colors with bold black outlines. Epic and powerful, anime final battle aesthetics — dramatic, imposing. Include glowing magical patterns and energy effects. Cel-shading with deep reds, golds, powerful light effects. Transparent background."

---

## Wall Tile Prompt

**Generic Wall** (`wall_generic.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of a stone wall tile in top-down view. Gray stone blocks with anime-style simplified details and subtle moss patches. Bright anime colors with bold black outlines. Sturdy but not intimidating, anime dungeon aesthetics — clean, stylized. Cel-shading with grays, subtle greens for moss, gentle highlights. Transparent background."

---

## Stair Tile Prompts

**Stair Up** (`stair_up.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of upward stairs in top-down view. Stone steps ascending upward with an upward-pointing arrow symbol, magical glow around edges, light emanating from the top. Bright anime colors with bold black outlines. Inviting and mystical, clear RPG aesthetic. Include sparkles rising upward and soft illumination. Cel-shading with grays, blues, ascending light effects. Transparent background."

**Stair Down** (`stair_down.png`) — ✅ exists
> "Create a 32x32 anime-style sprite of downward stairs in top-down view. Stone steps descending with a downward-pointing arrow symbol, shadowy depths visible below, subtle dark aura. Bright anime colors with bold black outlines. Mysterious but accessible. Include faint descending shadows and cool blue glow from depths. Cel-shading with dark grays, deep blues, descending shadow effects. Transparent background."

**Stair Left** (`stair_left.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of leftward stairs in top-down view. Stone steps leading left with a left-pointing arrow symbol and gentle glow indicating passage. Bright anime colors with bold black outlines. Directional and clear. Include subtle motion lines pointing left and soft directional lighting. Cel-shading with stone grays, warm lighting, leftward flow effects. Transparent background."

**Stair Right** (`stair_right.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of rightward stairs in top-down view. Stone steps leading right with a right-pointing arrow symbol and gentle glow. Bright anime colors with bold black outlines. Directional and clear. Include motion lines pointing right and soft directional lighting. Cel-shading with stone grays, warm lighting, rightward flow effects. Transparent background."

---

## Gate Tile Prompts

**Gate North** (`gate_north.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of a northern gate/doorway tile in top-down view. Ornate archway leading upward/north on screen, magical runes around frame, soft energy barrier, upward decorative elements. Bright anime colors with bold black outlines. Grand and portal-like, anime fantasy aesthetics. Include particle effects floating upward and magical shimmer. Cel-shading with stone grays, mystical blues/purples, magical energy effects. Transparent background."

**Gate South** (`gate_south.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of a southern gate/doorway tile in top-down view. Ornate archway leading downward/south, magical runes, soft energy barrier, downward decorative elements. Bright anime colors with bold black outlines. Welcoming yet mysterious. Include particle effects floating downward. Cel-shading with stone grays, warm golds, magical energy. Transparent background."

**Gate West** (`gate_west.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of a western gate/doorway tile in top-down view. Ornate archway leading left/west, magical runes, soft energy barrier, leftward decorative elements. Bright anime colors with bold black outlines. Mystical and directional. Include particles drifting left and magical curtain effect. Cel-shading with stone grays, cool blues, magical energy. Transparent background."

**Gate East** (`gate_east.png`) — ❌ needs generation
> "Create a 32x32 anime-style sprite of an eastern gate/doorway tile in top-down view. Ornate archway leading right/east, magical runes, soft energy barrier, rightward decorative elements. Bright anime colors with bold black outlines. Enchanted and directional. Include particles drifting right and magical curtain effect. Cel-shading with stone grays, warm oranges, magical energy. Transparent background."
