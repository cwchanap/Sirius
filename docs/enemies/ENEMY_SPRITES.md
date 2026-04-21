# Enemy & Character Sprite Guide

Sprite prompts and checklist for the player character and all 14 enemy types.
For overall asset status across all categories see `docs/items/ASSET_STATUS.md`.

---

## Art Style Guidelines

- **Style**: Japanese anime with chibi/super deformed proportions
- **Resolution**: 96×96 pixels per frame
- **Animation**: 4-frame walking cycle per character
- **Format**: Generate 4 individual frame PNGs when needed, then merge them into `sprite_sheet.png` via `tools/sprite_sheet_merger.py`
- **Background**: Transparent (PNG with alpha channel)
- **Color Palette**: Bright, saturated anime colors with cel-shading contrast
- **Outlines**: Bold black lines for definition
- **Shading**: Cel-shading — hard shadows and bright highlights

### Standard 4-Frame Cycle

| Frame | Description |
|-------|-------------|
| 1 | Idle/Standing (neutral pose) |
| 2 | Left foot forward + character-specific motion |
| 3 | Return to idle (or slight variation) |
| 4 | Right foot forward + character-specific motion (mirrors frame 2) |

### Movement Types by Enemy

| Type | Motion |
|------|--------|
| Hero, Goblin, Orc | Confident/bouncy stride with weapon bob |
| Forest Spirit, Dark Mage | Floating/gliding with flowing robes |
| Cave Spider, Desert Scorpion | Multi-leg scuttling |
| Mountain Wyvern, Dragon | Wing-beat hovering |
| Skeleton Warrior, Troll, Dungeon Guardian | Heavy-impact steps with armor rattle |
| Swamp Wretch | Slow shambling drag |
| Demon Lord, Boss | Commanding stride with aura effects |

---

## Asset Path Convention

Canonical runtime check: `assets/sprites/enemies/{type}/sprite_sheet.png`
Legacy runtime path still used by some shipped assets: `assets/sprites/characters/{type}/sprite_sheet.png`
Optional generation frames: `assets/sprites/enemies/{type}/frames/frame1-4.png`

Run `python3 tools/sprite_sheet_merger.py` after placing frames to generate the sheet.

### Current Repo References

- `assets/sprites/characters/player_hero/sprite_sheet.png` — existing runtime sheet, `384×96`
- `assets/sprites/enemies/goblin/sprite_sheet.png` — existing runtime sheet, `384×96`
- `assets/sprites/characters/forest_spirit/sprite_sheet.png` — legacy runtime sheet, `384×96`

Per-entity `Files` entries below describe where to create frame sources when generating new art. Treat the runtime `sprite_sheet.png` path as the authoritative existence check.

---

## Production Checklist

| Status | Entity | Runtime Sheet |
|--------|--------|--------------|
| ✅ exists | Player Hero | `assets/sprites/characters/player_hero/sprite_sheet.png` |
| ✅ exists | Goblin | `assets/sprites/enemies/goblin/sprite_sheet.png` |
| ⚠️ legacy | Forest Spirit | `assets/sprites/characters/forest_spirit/sprite_sheet.png` — runtime asset exists on legacy path |
| ❌ missing | Orc | `assets/sprites/enemies/orc/sprite_sheet.png` |
| ❌ missing | Skeleton Warrior | `assets/sprites/enemies/skeleton_warrior/sprite_sheet.png` |
| ❌ missing | Cave Spider | `assets/sprites/enemies/cave_spider/sprite_sheet.png` |
| ❌ missing | Troll | `assets/sprites/enemies/troll/sprite_sheet.png` |
| ❌ missing | Desert Scorpion | `assets/sprites/enemies/desert_scorpion/sprite_sheet.png` |
| ❌ missing | Swamp Wretch | `assets/sprites/enemies/swamp_wretch/sprite_sheet.png` |
| ❌ missing | Mountain Wyvern | `assets/sprites/enemies/mountain_wyvern/sprite_sheet.png` |
| ❌ missing | Dragon | `assets/sprites/enemies/dragon/sprite_sheet.png` |
| ❌ missing | Dark Mage | `assets/sprites/enemies/dark_mage/sprite_sheet.png` |
| ❌ missing | Dungeon Guardian | `assets/sprites/enemies/dungeon_guardian/sprite_sheet.png` |
| ❌ missing | Demon Lord | `assets/sprites/enemies/demon_lord/sprite_sheet.png` |
| ❌ missing | Boss | `assets/sprites/enemies/boss/sprite_sheet.png` |

---

## Player Character

**Generation frames**: `assets/sprites/characters/player_hero/frames/frame1-4.png` (96×96)
Runtime sheet exists at `assets/sprites/characters/player_hero/sprite_sheet.png`

**Frame 1 (Idle/Standing)**
> "Create a 96x96 anime-style sprite of a young adventurer standing ready, top-down view, facing downward toward camera. Large expressive anime eyes, spiky brown hair, simple blue tunic, brown pants, small leather boots. Standing confidently with arms at sides, character oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Heroic and determined. Cel-shading with vibrant blues, browns, skin tones. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step)**
> "Same character as frame 1 but: Left foot stepped forward, right foot back, slight body lean forward, arms swinging naturally with walking motion."

**Frame 3 (Standing Variant)**
> "Same character as frame 1 but: Slightly different arm position, tunic settled differently from movement."

**Frame 4 (Right Step)**
> "Same character as frame 1 but: Right foot stepped forward, left foot back, body lean opposite to frame 2, arms in opposite swing position."

---

## Starting Area Enemies

### Goblin (`goblin`)

**Generation frames**: `assets/sprites/enemies/goblin/frames/frame1-4.png` (96×96)
Runtime sheet exists at `assets/sprites/enemies/goblin/sprite_sheet.png`

**Frame 1 (Idle)**
> "Create a 96x96 anime-style sprite of a cute goblin in top-down view, standing, facing downward toward camera. The goblin should have large round eyes, green skin, pointed ears, wearing simple brown rags. Include a small wooden club held at side. Character facing toward bottom of screen. Use bright anime colors with bold black outlines. Make it look mischievous but cute with anime kawaii style. Use cel-shading with vibrant greens and browns. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Hop)** — "Same but left foot forward with slight bounce, wooden club raised slightly."

**Frame 3 (Idle Variant)** — "Same but neutral standing pose with wooden club at side, slight posture variation."

**Frame 4 (Right Hop)** — "Same but right foot forward with slight bounce, wooden club raised slightly, mirroring frame 2."

### Orc (`orc`)

**Files**: `assets/sprites/enemies/orc/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Idle)**
> "Create a 96x96 anime-style sprite of an orc warrior in top-down view, standing, facing downward toward camera. The orc should have large expressive eyes, gray-green skin, small tusks, wearing crude armor. Include a simple axe held at side. Character facing toward bottom of screen. Use bright anime colors with bold black outlines. Make it look tough but with anime-style charm. Use cel-shading with vibrant grays and greens. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step)** — "Same but left foot stepping forward with slight lean, axe moved with stride motion."

**Frame 3 (Idle Variant)** — "Same but neutral standing pose with axe at side."

**Frame 4 (Right Step)** — "Same but right foot stepping forward with slight lean, axe moved with stride, mirroring frame 2."

---

## Forest Area Enemies

### Forest Spirit (`forest_spirit`)

**Generation frames**: `assets/sprites/characters/forest_spirit/frames/frame1-4.png` (96×96)
Legacy runtime sheet exists at `assets/sprites/characters/forest_spirit/sprite_sheet.png`
> Migrate to `assets/sprites/enemies/forest_spirit/frames/` only when the repo is ready to move the runtime asset to the new convention.

**Frame 1 (Idle Float)**
> "Create a 96x96 anime-style sprite of a forest spirit in idle pose, top-down view, facing downward toward camera. Large gentle green eyes, translucent green-blue body, flower crown, leaf clothing. Arms at sides, gentle glow, character facing toward bottom of screen. Bright anime colors with soft black outlines. Mystical and beautiful with sparkles around. Cel-shading with ethereal greens, blues, magical light. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Float Up)** — "Same but arms slightly raised, brighter glow, flower petals floating around."

**Frame 3 (Idle Return)** — "Same but arms at sides with gentle glow, slightly different sparkle positions."

**Frame 4 (Float Down)** — "Same but arms gently swaying down, soft glow, leaves drifting downward."

---

## Cave Area Enemies

### Skeleton Warrior (`skeleton_warrior`)

**Files**: `assets/sprites/enemies/skeleton_warrior/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Ready Stance)**
> "Create a 96x96 anime-style sprite of a skeleton warrior in ready stance, top-down view, facing downward toward camera. Large glowing red eye sockets, bone white skeleton, rusty armor pieces, bone sword raised. Standing proud and menacing, character facing toward bottom of screen. Bright anime colors with bold black outlines. Intimidating yet stylized. Cel-shading with bone whites, rusty browns, glowing red. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step Attack)** — "Same but left foot forward, bone sword in mid-swing, armor pieces rattling."

**Frame 3 (Ready Variant)** — "Same but slightly different bone positioning, bone sword raised in ready position."

**Frame 4 (Right Step Attack)** — "Same but right foot forward, bone sword completing swing, joints clicking."

### Cave Spider (`cave_spider`)

**Files**: `assets/sprites/enemies/cave_spider/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Crouched)**
> "Create a 96x96 anime-style sprite of a cave spider crouched, top-down view, oriented with head facing downward toward camera. Large cute but menacing purple eyes, dark purple-black body, 8 legs visible, small web pattern on body. Crouched low with legs drawn in, spider oriented toward bottom of screen. Bright anime colors with bold black outlines. Creepy but stylized. Cel-shading with dark purples, blacks, subtle web shimmer. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Legs Left)** — "Same but 8 legs extended leftward in scuttling motion, body shifted slightly left."

**Frame 3 (Centered)** — "Same but 8 legs in neutral position, balanced stance."

**Frame 4 (Legs Right)** — "Same but 8 legs extended rightward, mirroring frame 2."

### Troll (`troll`)

**Files**: `assets/sprites/enemies/troll/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Standing Proud)**
> "Create a 96x96 anime-style sprite of a cave troll standing proud, top-down view, facing downward toward camera. Large aggressive yellow eyes, gray-green skin, massive muscular build, stone club in hand. Standing tall and intimidating, character facing toward bottom of screen. Bright anime colors with bold black outlines. Powerful and brutish. Cel-shading with gray-greens, browns, stone textures. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Stomp)** — "Same but left foot forward with ground impact effect, stone club raised."

**Frame 3 (Standing Variant)** — "Same but stone club in slightly different ready position."

**Frame 4 (Right Stomp)** — "Same but right foot forward with ground impact, stone club swinging, mirroring frame 2."

---

## Desert Area Enemies

### Desert Scorpion (`desert_scorpion`)

**Files**: `assets/sprites/enemies/desert_scorpion/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Standing Menace)**
> "Create a 96x96 anime-style sprite of a desert scorpion standing menacingly, top-down view, facing downward toward camera. Glowing red eyes, sandy yellow-brown chitinous armor, large pincers ready to strike, segmented tail with stinger raised. Standing in threatening pose, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Chitinous texture with sandy browns, yellows. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Pincer Strike)** — "Same but left pincer extended in attack, tail coiled."

**Frame 3 (Standing Alert)** — "Same but pincers raised defensively, tail swaying."

**Frame 4 (Right Pincer Strike)** — "Same but right pincer extended, mirroring frame 2."

---

## Swamp Area Enemies

### Swamp Wretch (`swamp_wretch`)

**Files**: `assets/sprites/enemies/swamp_wretch/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Hunched)**
> "Create a 96x96 anime-style sprite of a swamp wretch hunched standing, top-down view, facing downward toward camera. Large sad anime eyes, muddy green-brown skin, tattered robes, swamp vegetation clinging to body. Hunched posture, arms hanging, looking pitiful, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Mysterious and sad. Cel-shading with murky greens, browns, dark purples. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Drag)** — "Same but left foot struggling forward in dragging motion, tattered robes flowing, vegetation swaying."

**Frame 3 (Hunched Variant)** — "Same but robes settled differently, slightly different hunched pose."

**Frame 4 (Right Drag)** — "Same but right foot struggling forward in shambling motion, mirroring frame 2."

---

## Mountain Area Enemies

### Mountain Wyvern (`mountain_wyvern`)

**Files**: `assets/sprites/enemies/mountain_wyvern/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Wings Up)**
> "Create a 96x96 anime-style sprite of a mountain wyvern with wings up, top-down view, facing downward toward camera. Large expressive dragon eyes, blue-gray scales, small wings spread upward, sleek body. Hovering majestically with wings at peak, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Majestic and noble. Cel-shading with vibrant blues, grays, white highlights. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Wings Mid-Down)** — "Same but wings in downward motion, body slightly bobbing."

**Frame 3 (Wings Down)** — "Same but wings at lowest point, tail swaying."

**Frame 4 (Wings Mid-Up)** — "Same but wings rising upward, body adjusting height."

### Dragon (`dragon`)

**Files**: `assets/sprites/enemies/dragon/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Standing Proud)**
> "Create a 96x96 anime-style sprite of a red dragon standing proud, top-down view, facing downward toward camera. Large intimidating yet expressive eyes, crimson scales, small wings folded, powerful stance. Standing regally with small flame breath visible, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Powerful and majestic. Cel-shading with vibrant reds, oranges, gold accents. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step + Flame)** — "Same but left foot forward, wings flutter, flame puff from mouth."

**Frame 3 (Standing Variant)** — "Same but wings slightly different, subtle flame from mouth."

**Frame 4 (Right Step + Flame)** — "Same but right foot forward, wings flutter, flame burst, mirroring frame 2."

---

## Dungeon Area Enemies

### Dark Mage (`dark_mage`)

**Files**: `assets/sprites/enemies/dark_mage/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Robes Flowing)**
> "Create a 96x96 anime-style sprite of a dark mage with robes flowing normally, top-down view, facing downward toward camera. Large glowing purple eyes, dark hooded robes, magical staff with crystal, mysterious aura. Gliding mystically with robes settled, character oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Mysterious and powerful. Cel-shading with deep purples, blacks, magical glowing effects. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Billow Left + Staff Glow)** — "Same but robes flowing leftward, staff crystal pulsing bright, magical sparkles."

**Frame 3 (Robes Settled)** — "Same but robes calm, sparkles in different positions, staff in mystic aura."

**Frame 4 (Billow Right + Staff Glow)** — "Same but robes flowing rightward, staff crystal pulsing, opposite direction to frame 2."

### Dungeon Guardian (`dungeon_guardian`)

**Files**: `assets/sprites/enemies/dungeon_guardian/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Standing Ready)**
> "Create a 96x96 anime-style sprite of a dungeon guardian standing ready, top-down view. Large glowing blue eyes, stone-gray body, ancient armor pieces, massive sword held ready. Standing mechanically with runes softly glowing. Bright anime colors with bold black outlines. Ancient and powerful. Cel-shading with grays, blues, mystical light effects. Use transparent background."

**Frame 2 (Left Step Impact)** — "Same but left foot forward with ground impact/dust, sword raised, armor clanking."

**Frame 3 (Ready Variant)** — "Same but sword in slightly different ready position, runes pulsing."

**Frame 4 (Right Step Impact)** — "Same but right foot forward with ground effects, armor shifting, mirroring frame 2."

---

## Boss Area Enemies

### Demon Lord (`demon_lord`)

**Files**: `assets/sprites/enemies/demon_lord/frames/frame1-4.png` (96×96) — ❌ needs generation

**Frame 1 (Menacing Stance)**
> "Create a 96x96 anime-style sprite of a demon lord in menacing stance, top-down view. Large intimidating red eyes, dark red skin, ornate black spiked armor, flaming sword ready. Standing commandingly with dark aura pulsing. Bright anime colors with bold black outlines. Powerful and menacing. Cel-shading with deep reds, blacks, fiery highlights. Use transparent background."

**Frame 2 (Left Step + Flame Burst)** — "Same but left foot forward, commanding stride with flame explosion from sword."

**Frame 3 (Cape Flutter)** — "Same but cape billowing, different flame intensity."

**Frame 4 (Right Step + Flame Burst)** — "Same but right foot forward, major flame burst, mirroring frame 2."

### Boss (`boss`)

**Files**: `assets/sprites/enemies/boss/frames/frame1-4.png` (96×96) — ❌ needs generation
Maps to `EnemyTypeId.Boss = "boss"` — final boss / ancient dragon king variant.

**Frame 1 (Hovering Regally)**
> "Create a 96x96 anime-style sprite of an ancient dragon king hovering regally, top-down view. Large wise golden eyes, golden scales with ancient markings, ornate crown/horns, magical energy radiating. Floating majestically with divine aura. Bright anime colors with bold black outlines. Absolutely majestic. Cel-shading with brilliant golds, deep purples, divine light. Use transparent background."

**Frame 2 (Rise + Energy Pulse)** — "Same but floating higher, ancient markings glowing, crown shimmering, magical energy surging."

**Frame 3 (Hover Variant)** — "Same but different magical aura pattern, scale shimmer."

**Frame 4 (Dip + Energy Pulse)** — "Same but floating lower, scales pulsing, crown radiating light, divine power wave."
