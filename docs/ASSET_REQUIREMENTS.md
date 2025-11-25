# Asset Requirements for Sirius RPG

## Overview
This document outlines all visual assets needed for the Sirius RPG game, organized by priority and category. All assets should be 96x96 pixels to match the game's grid system.

## Art Style Guidelines
- **Style**: Standard Japanese anime style with chibi/super deformed character proportions
- **Resolution**: 96x96 pixels per frame
- **Animation**: 4-frame walking cycles for all characters and enemies
- **Format**: Individual frame files (will be merged into sprite sheets via script)
- **Background**: Transparent background for all sprites (PNG format with alpha channel)
- **Color Palette**: Bright, saturated anime colors with cel-shading style contrast
- **Character Design**: Large expressive eyes, simplified facial features, exaggerated proportions
- **Aesthetic**: Clean, polished anime art with traditional anime visual tropes and styling
- **Consistency**: Maintain anime art style conventions across all character and creature designs
- **Readability**: Ensure sprites are clearly distinguishable with bold outlines and vibrant colors
- **Animation Style**: Smooth 4-frame walking cycles with subtle bouncing motion typical of anime

## Asset Production Checklist

### Priority 1: Core Characters & Enemies
- [x] **Player Hero Frames** — `assets/sprites/characters/player_hero/frames/` (frame1-4.png)
- [x] **Enemy Goblin Frames** — `assets/sprites/enemies/goblin/frames/` (frame1-4.png)
- [ ] **Enemy Orc Frames** — `assets/sprites/characters/enemy_orc/`
- [x] **Enemy Forest Spirit Frames** — `assets/sprites/characters/forest_spirit/frames/` (frame1-4.png)
- [ ] **Enemy Skeleton Warrior Frames** — `assets/sprites/characters/enemy_skeleton_warrior/`
- [ ] **Enemy Cave Spider Frames** — `assets/sprites/characters/enemy_cave_spider/`
- [ ] **Enemy Troll Frames** — `assets/sprites/characters/enemy_troll/`
- [ ] **Enemy Desert Scorpion Frames** — `assets/sprites/characters/enemy_desert_scorpion/`
- [ ] **Enemy Swamp Wretch Frames** — `assets/sprites/characters/enemy_swamp_wretch/`
- [ ] **Enemy Mountain Wyvern Frames** — `assets/sprites/characters/enemy_mountain_wyvern/`
- [ ] **Enemy Dragon Frames** — `assets/sprites/characters/enemy_dragon/`
- [ ] **Enemy Dark Mage Frames** — `assets/sprites/characters/enemy_dark_mage/`
- [ ] **Enemy Dungeon Guardian Frames** — `assets/sprites/characters/enemy_dungeon_guardian/`
- [ ] **Enemy Demon Lord Frames** — `assets/sprites/characters/enemy_demon_lord/`
- [ ] **Enemy Ancient Dragon King Frames** — `assets/sprites/characters/enemy_ancient_dragon_king/`

### Priority 2: Terrain
- [x] **Starting Area Floor Tile** — `assets/sprites/terrain/floor_starting_area.png`
- [x] **Generic Wall Tile** — `assets/sprites/terrain/wall_generic.png`
- [x] **Forest Floor Tile** — `assets/sprites/terrain/floor_forest.png`
- [x] **Cave Floor Tile** — `assets/sprites/terrain/floor_cave.png`
- [x] **Desert Floor Tile** — `assets/sprites/terrain/floor_desert.png`
- [x] **Swamp Floor Tile** — `assets/sprites/terrain/floor_swamp.png`
- [x] **Mountain Floor Tile** — `assets/sprites/terrain/floor_mountain.png`
- [x] **Dungeon Floor Tile** — `assets/sprites/terrain/floor_dungeon.png`
- [ ] **Boss Arena Floor Tile** — `assets/sprites/terrain/floor_boss_arena.png`
- [ ] **Stairs Up Tile** — `assets/sprites/terrain/stairs_up.png` (32x32)
- [ ] **Stairs Down Tile** — `assets/sprites/terrain/stairs_down.png` (32x32)
- [ ] **Stairs Left Tile** — `assets/sprites/terrain/stairs_left.png` (32x32)
- [ ] **Stairs Right Tile** — `assets/sprites/terrain/stairs_right.png` (32x32)
- [ ] **Gate North Tile** — `assets/sprites/terrain/gate_north.png` (32x32)
- [ ] **Gate South Tile** — `assets/sprites/terrain/gate_south.png` (32x32)
- [ ] **Gate West Tile** — `assets/sprites/terrain/gate_west.png` (32x32)
- [ ] **Gate East Tile** — `assets/sprites/terrain/gate_east.png` (32x32)

### Priority 3: UI Elements
- [x] **Main Menu Background** — `assets/sprites/ui/ui_main_menu_background.png`
- [x] **Battle Background** — `assets/sprites/ui/ui_battle_background.png`
- [ ] **Attack Button** — `assets/sprites/ui/ui_button_attack.png`
- [ ] **Defend Button** — `assets/sprites/ui/ui_button_defend.png`
- [ ] **Run Button** — `assets/sprites/ui/ui_button_run.png`

### Priority 4: Status & Effects
- [ ] **Health Icon** — `assets/sprites/ui/icon_health.png`
- [ ] **Experience Icon** — `assets/sprites/ui/icon_experience.png`
- [ ] **Level Icon** — `assets/sprites/ui/icon_level.png`
- [ ] **Hit Impact Effect** — `assets/sprites/effects/effect_hit_impact.png`
- [ ] **Magic Sparkles Effect** — `assets/sprites/effects/effect_magic_sparkles.png`
- [ ] **Level Up Effect** — `assets/sprites/effects/effect_level_up.png`

## Priority 1: Core Characters & Enemies

### Player Character
**Files**: 
- `assets/sprites/characters/player_hero/frame1.png` (96x96 - Idle/Standing)
- `assets/sprites/characters/player_hero/frame2.png` (96x96 - Left foot forward)
- `assets/sprites/characters/player_hero/frame3.png` (96x96 - Standing)  
- `assets/sprites/characters/player_hero/frame4.png` (96x96 - Right foot forward)

**AI Prompts** (Generate each frame separately):

**Main Frame (Standing Ready)**: "Create a 96x96 anime-style sprite of a young adventurer standing ready, top-down view, facing downward toward camera. Large expressive anime eyes, spiky brown hair, simple blue tunic, brown pants, small leather boots. Standing confidently with arms at sides, character oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Heroic and determined. Cel-shading with vibrant blues, browns, skin tones. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step)**: "Same character as main frame but: Left foot stepped forward, right foot back, slight body lean forward, arms swinging naturally with walking motion."

**Frame 3 (Standing Ready)**: "Same character as main frame but: Slightly different arm position, tunic settled differently from movement."

**Frame 4 (Right Step)**: "Same character as main frame but: Right foot stepped forward, left foot back, body lean opposite to frame 2, arms in opposite swing position."

### Starting Area Enemies

**Files**: 
- `assets/sprites/characters/enemy_goblin/frame1.png` (96x96 - Standing)
- `assets/sprites/characters/enemy_goblin/frame2.png` (96x96 - Left hop)
- `assets/sprites/characters/enemy_goblin/frame3.png` (96x96 - Standing)
- `assets/sprites/characters/enemy_goblin/frame4.png` (96x96 - Right hop)

**AI Prompts** (Generate each frame separately):

**Main Frame (Standing)**: "Create a 96x96 anime-style sprite of a cute goblin in top-down view, standing, facing downward toward camera. The goblin should have large round eyes, green skin, pointed ears, wearing simple brown rags. Include a small wooden club held at side. Character facing toward bottom of screen. Use bright anime colors with bold black outlines. Make it look mischievous but cute with anime kawaii style. Use cel-shading with vibrant greens and browns. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left hop)**: "Same character as main frame but: Left foot forward with slight bounce, wooden club raised slightly from movement."

**Frame 3 (Standing)**: "Same character as main frame but: Neutral standing pose with wooden club at side, slight variation in posture from main frame."

**Frame 4 (Right hop)**: "Same character as main frame but: Right foot forward with slight bounce, wooden club raised slightly, mirroring frame 2's motion."

**Files**: 
- `assets/sprites/characters/enemy_orc/frame1.png` (96x96 - Standing)
- `assets/sprites/characters/enemy_orc/frame2.png` (96x96 - Left step)
- `assets/sprites/characters/enemy_orc/frame3.png` (96x96 - Standing)
- `assets/sprites/characters/enemy_orc/frame4.png` (96x96 - Right step)

**AI Prompts** (Generate each frame separately):

**Main Frame (Standing)**: "Create a 96x96 anime-style sprite of an orc warrior in top-down view, standing, facing downward toward camera. The orc should have large expressive eyes, gray-green skin, small tusks, wearing crude armor. Include a simple axe held at side. Character facing toward bottom of screen. Use bright anime colors with bold black outlines. Make it look tough but with anime-style charm. Use cel-shading with vibrant grays and greens. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left step)**: "Same character as main frame but: Left foot stepping forward with slight lean, axe moved with stride motion."

**Frame 3 (Standing)**: "Same character as main frame but: Neutral standing pose with axe at side, similar to main frame positioning."

**Frame 4 (Right step)**: "Same character as main frame but: Right foot stepping forward with slight lean, axe moved with stride, mirroring frame 2's motion."

### Forest Area Enemies

**File**: `assets/sprites/characters/enemy_forest_spirit/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Idle)**: "Create a 96x96 anime-style sprite of a forest spirit in idle pose, top-down view, facing downward toward camera. Large gentle green eyes, translucent green-blue body, flower crown, leaf clothing. Arms at sides, gentle glow, character facing toward bottom of screen. Bright anime colors with soft black outlines. Mystical and beautiful with sparkles around. Cel-shading with ethereal greens, blues, magical light. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Float Up)**: "Same character as main frame but: Arms slightly raised, brighter glow, flower petals floating around from upward motion."

**Frame 3 (Idle Return)**: "Same character as main frame but: Arms at sides with gentle glow, slightly different sparkle positions around body."

**Frame 4 (Float Down)**: "Same character as main frame but: Arms gently swaying down, soft glow, leaves drifting downward from floating motion."

### Cave Area Enemies

**File**: `assets/sprites/characters/enemy_skeleton_warrior/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Ready Stance)**: "Create a 96x96 anime-style sprite of a skeleton warrior in ready stance, top-down view, facing downward toward camera. Large glowing red eye sockets, bone white skeleton, rusty armor pieces, bone sword raised. Standing proud and menacing, character facing toward bottom of screen. Bright anime colors with bold black outlines. Intimidating yet stylized. Cel-shading with bone whites, rusty browns, glowing red. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step Attack)**: "Same character as main frame but: Left foot forward, bone sword in mid-swing motion, armor pieces rattling from movement."

**Frame 3 (Ready Stance)**: "Same character as main frame but: Slightly different bone positioning, bone sword raised in ready position."

**Frame 4 (Right Step Attack)**: "Same character as main frame but: Right foot forward, bone sword completing swing motion, joints clicking from movement."

**File**: `assets/sprites/characters/enemy_cave_spider/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Crouched)**: "Create a 96x96 anime-style sprite of a cave spider crouched, top-down view, oriented with head facing downward toward camera. Large cute but menacing purple eyes, dark purple-black body, 8 legs visible, small web pattern on body. Crouched low with legs drawn in, spider oriented toward bottom of screen. Bright anime colors with bold black outlines. Creepy but stylized. Cel-shading with dark purples, blacks, subtle web shimmer. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Legs Spread Left)**: "Same character as main frame but: 8 legs extended leftward in scuttling motion, body shifted slightly left."

**Frame 3 (Centered)**: "Same character as main frame but: 8 legs in neutral position, balanced stance between movements."

**Frame 4 (Legs Spread Right)**: "Same character as main frame but: 8 legs extended rightward in scuttling motion, body shifted slightly right, mirroring frame 2."

**File**: `assets/sprites/characters/enemy_troll/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Standing Proud)**: "Create a 96x96 anime-style sprite of a cave troll standing proud, top-down view, facing downward toward camera. Large aggressive yellow eyes, gray-green skin, massive muscular build, stone club in hand. Standing tall and intimidating, character facing toward bottom of screen. Bright anime colors with bold black outlines. Powerful and brutish. Cel-shading with gray-greens, browns, stone textures. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Stomp)**: "Same character as main frame but: Left foot forward with ground impact effect, stone club raised from stomping motion."

**Frame 3 (Standing Proud)**: "Same character as main frame but: Stone club in slightly different ready position, standing menacingly."

**Frame 4 (Right Stomp)**: "Same character as main frame but: Right foot forward with ground impact, stone club swinging from stomping motion, mirroring frame 2."

### Desert Area Enemies

**File**: `assets/sprites/characters/enemy_desert_scorpion/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Standing Menace)**: "Create a 96x96 anime-style sprite of a desert scorpion standing menacingly, top-down view, facing downward toward camera. Glowing red eyes, sandy yellow-brown chitinous armor, large pincers ready to strike, segmented tail with stinger raised. Standing in threatening pose, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Chitinous texture with sandy browns, yellows. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Pincer Strike)**: "Same character as main frame but: Left pincer extended forward in attack motion, tail coiled for striking."

**Frame 3 (Standing Alert)**: "Same character as main frame but: Pincers raised defensively, tail swaying in alert position."

**Frame 4 (Right Pincer Strike)**: "Same character as main frame but: Right pincer extended in attack, tail position mirroring left strike, opposite motion to frame 2."

### Swamp Area Enemies

**File**: `assets/sprites/characters/enemy_swamp_wretch/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Hunched Standing)**: "Create a 96x96 anime-style sprite of a swamp wretch hunched standing, top-down view, facing downward toward camera. Large sad anime eyes, muddy green-brown skin, tattered robes, swamp vegetation clinging to body. Hunched posture, arms hanging, looking pitiful, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Mysterious and sad. Cel-shading with murky greens, browns, dark purples. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Foot Drag)**: "Same character as main frame but: Left foot struggling forward in dragging motion, tattered robes flowing from movement, vegetation swaying."

**Frame 3 (Hunched Standing)**: "Same character as main frame but: Robes settled differently from previous movement, slightly different hunched pose."

**Frame 4 (Right Foot Drag)**: "Same character as main frame but: Right foot struggling forward in shambling motion, robes flowing opposite direction, mirroring frame 2's movement."

### Mountain Area Enemies

**File**: `assets/sprites/characters/enemy_mountain_wyvern/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Wings Up)**: "Create a 96x96 anime-style sprite of a mountain wyvern with wings up, top-down view, facing downward toward camera. Large expressive dragon eyes, blue-gray scales, small wings spread upward, sleek body. Hovering majestically with wings at peak, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Majestic and noble. Cel-shading with vibrant blues, grays, white highlights. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Wings Mid-Down)**: "Same character as main frame but: Wings in downward motion, body slightly bobbing from flying motion with wings pushing down."

**Frame 3 (Wings Down)**: "Same character as main frame but: Wings at lowest point of stroke, tail swaying from hovering motion."

**Frame 4 (Wings Mid-Up)**: "Same character as main frame but: Wings rising upward, body adjusting height from flying motion with wings pulling up."

**File**: `assets/sprites/characters/enemy_dragon/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Standing Proud)**: "Create a 96x96 anime-style sprite of a red dragon standing proud, top-down view, facing downward toward camera. Large intimidating yet expressive eyes, crimson scales, small wings folded, powerful stance. Standing regally with small flame breath visible, creature oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Powerful and majestic. Cel-shading with vibrant reds, oranges, gold accents. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Left Step with Flame)**: "Same character as main frame but: Left foot forward, wings flutter from walking motion, flame puff from mouth during powerful walking."

**Frame 3 (Standing Proud)**: "Same character as main frame but: Wings in slightly different settled position, noble posture with subtle flame from mouth."

**Frame 4 (Right Step with Flame)**: "Same character as main frame but: Right foot forward, wings flutter, flame burst from mouth during walking, mirroring frame 2's motion."

### Dungeon Area Enemies

**File**: `assets/sprites/characters/enemy_dark_mage/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Main Frame (Robes Flowing)**: "Create a 96x96 anime-style sprite of a dark mage with robes flowing normally, top-down view, facing downward toward camera. Large glowing purple eyes, dark hooded robes, magical staff with crystal, mysterious aura. Gliding mystically with robes settled, character oriented with head facing downward toward camera. Bright anime colors with bold black outlines. Mysterious and powerful. Cel-shading with deep purples, blacks, magical glowing effects. Important: Use transparent background (PNG with alpha channel)."

**Frame 2 (Robes Billow Left, Staff Glow)**: "Same character as main frame but: Dark robes flowing leftward, staff crystal pulsing bright, magical sparkles from energy surge."

**Frame 3 (Robes Flowing)**: "Same character as main frame but: Robes calm with sparkles in different positions, staff with crystal in mystic aura."

**Frame 4 (Robes Billow Right, Staff Glow)**: "Same character as main frame but: Robes flowing rightward, staff crystal pulsing, magical energy swirling, opposite direction to frame 2."

**File**: `assets/sprites/characters/enemy_dungeon_guardian/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Frame 1 (Standing Ready)**: "Create a 96x96 anime-style sprite of a dungeon guardian standing ready, top-down view. Large glowing blue eyes, stone-gray body, ancient armor pieces, massive sword held ready. Standing mechanically with runes softly glowing. Bright anime colors with bold black outlines. Ancient and powerful. Cel-shading with grays, blues, mystical light effects."

**Frame 2 (Left Step Impact)**: "Create a 96x96 anime-style sprite of a dungeon guardian stepping left with ground impact, top-down view. Large glowing blue eyes, stone body, armor clanking, sword raised, left foot forward. Heavy mechanical step with dust/impact effects. Same character as frame 1. Bright anime colors with bold black outlines. Cel-shading with grays, blues, impact dust."

**Frame 3 (Standing Ready)**: "Create a 96x96 anime-style sprite of a dungeon guardian standing ready, top-down view. Same as frame 1 but with sword in slightly different ready position. Large glowing blue eyes, stone body, ancient armor, massive sword. Standing alert with runes pulsing. Bright anime colors with bold black outlines. Cel-shading with grays, blues, mystical light."

**Frame 4 (Right Step Impact)**: "Create a 96x96 anime-style sprite of a dungeon guardian stepping right with ground impact, top-down view. Large glowing blue eyes, stone body, armor pieces shifting, sword positioning, right foot forward. Heavy mechanical step with ground effects. Same character as previous frames. Bright anime colors with bold black outlines. Cel-shading with grays, blues, impact effects."

### Boss Area Enemies

**File**: `assets/sprites/characters/enemy_demon_lord/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Frame 1 (Menacing Stance)**: "Create a 96x96 anime-style sprite of a demon lord in menacing stance, top-down view. Large intimidating red eyes, dark red skin, ornate black spiked armor, flaming sword ready. Standing commandingly with dark aura pulsing. Bright anime colors with bold black outlines. Powerful and menacing. Cel-shading with deep reds, blacks, fiery highlights."

**Frame 2 (Left Step Flame Burst)**: "Create a 96x96 anime-style sprite of a demon lord stepping left with flame burst, top-down view. Large intimidating red eyes, dark red skin, spiked armor, flaming sword raised, left foot forward. Commanding stride with flame explosion from sword. Same character as frame 1. Bright anime colors with bold black outlines. Cel-shading with deep reds, flame effects."

**Frame 3 (Menacing Stance)**: "Create a 96x96 anime-style sprite of a demon lord in menacing stance, top-down view. Same as frame 1 but with cape flutter and different flame intensity. Large red eyes, dark red skin, spiked armor, flaming sword. Standing powerfully with cape billowing. Bright anime colors with bold black outlines. Cel-shading with deep reds, blacks, fiery highlights."

**Frame 4 (Right Step Flame Burst)**: "Create a 96x96 anime-style sprite of a demon lord stepping right with flame burst, top-down view. Large intimidating red eyes, dark red skin, ornate armor, flaming sword swinging, right foot forward. Powerful stride with major flame burst. Same character as previous frames. Bright anime colors with bold black outlines. Cel-shading with deep reds, flame explosions."

**File**: `assets/sprites/characters/enemy_ancient_dragon_king/`
**Individual Frames**: frame1.png through frame4.png
**AI Prompts**:

**Frame 1 (Hovering Regally)**: "Create a 96x96 anime-style sprite of a ancient dragon king hovering regally, top-down view. Large wise golden eyes, golden scales with ancient markings, ornate crown/horns, magical energy radiating. Floating majestically with divine aura. Bright anime colors with bold black outlines. Absolutely majestic. Cel-shading with brilliant golds, deep purples, divine light."

**Frame 2 (Slight Rise Energy Pulse)**: "Create a 96x96 anime-style sprite of a ancient dragon king rising slightly with energy pulse, top-down view. Large wise golden eyes, golden scales, ancient markings glowing, crown shimmering, magical energy surging. Floating higher with power pulse. Same character as frame 1. Bright anime colors with bold black outlines. Cel-shading with brilliant golds, energy effects."

**Frame 3 (Hovering Regally)**: "Create a 96x96 anime-style sprite of a ancient dragon king hovering regally, top-down view. Same as frame 1 but with different magical aura pattern. Large wise golden eyes, golden scales, ancient markings, ornate crown, divine energy. Floating serenely with scale shimmer. Bright anime colors with bold black outlines. Cel-shading with brilliant golds, divine light effects."

**Frame 4 (Slight Dip Energy Pulse)**: "Create a 96x96 anime-style sprite of a ancient dragon king dipping slightly with energy pulse, top-down view. Large wise golden eyes, golden scales with markings pulsing, crown radiating light, magical energy flowing. Floating lower with divine power wave. Same character as previous frames. Bright anime colors with bold black outlines. Cel-shading with brilliant golds, divine energy."

## Priority 2: Basic Terrain

### Floor Tiles

**File**: `assets/sprites/terrain/floor_starting_area.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a peaceful grass floor tile in top-down view. The tile should show short green grass with small colorful flowers scattered throughout. Use bright anime colors with subtle black outlines for texture details. Make it look welcoming and safe with anime pastoral aesthetics - cheerful, vibrant. Include tiny sparkles or light effects to give it a magical feel. Use cel-shading with bright greens, colorful flower accents, and subtle highlights."

**File**: `assets/sprites/terrain/wall_generic.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a stone wall tile in top-down view. The wall should be made of gray stone blocks with anime-style simplified details and subtle moss patches. Use bright anime colors with bold black outlines for block definition. Make it look sturdy but not intimidating with anime dungeon aesthetics - clean, stylized. Include subtle texture details and soft shading. Use cel-shading with grays, subtle greens for moss, and gentle highlights."

### Themed Floor Tiles

**File**: `assets/sprites/terrain/floor_forest.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a forest floor tile in top-down view. The tile should show dark earth with fallen leaves, small mushrooms, and tiny forest flowers. Use bright anime colors with subtle black outlines for natural details. Make it look mystical and natural with anime nature aesthetics - vibrant, alive. Include tiny glowing spores or fireflies for magical atmosphere. Use cel-shading with rich browns, vibrant greens, and magical light accents."

**File**: `assets/sprites/terrain/floor_cave.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a cave floor tile in top-down view. The tile should show rough stone surface with small crystals and mineral deposits. Use bright anime colors with bold black outlines for rock texture. Make it look mysterious and ancient with anime cave aesthetics - dramatic shadows, interesting textures. Include small glowing crystals for magical ambiance. Use cel-shading with grays, purples, and crystal highlights."

**File**: `assets/sprites/terrain/floor_desert.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a desert floor tile in top-down view. The tile should show golden sand with wind patterns and small scattered pebbles. Use bright anime colors with subtle black outlines for sand texture. Make it look hot and vast with anime desert aesthetics - warm, sun-bleached. Include tiny heat shimmer effects or sand sparkles. Use cel-shading with warm yellows, oranges, and sun highlights."

**File**: `assets/sprites/terrain/floor_swamp.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a swamp floor tile in top-down view. The tile should show muddy water with lily pads, cattails, and bubbles. Use bright anime colors with bold black outlines for water and vegetation. Make it look murky but alive with anime wetland aesthetics - mysterious, atmospheric. Include subtle water ripples and glowing marsh lights. Use cel-shading with murky greens, browns, and mysterious light effects."

**File**: `assets/sprites/terrain/floor_mountain.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a mountain floor tile in top-down view. The tile should show rocky ground with patches of snow and small alpine flowers. Use bright anime colors with bold black outlines for rock details. Make it look cold and high-altitude with anime mountain aesthetics - crisp, pristine. Include tiny snowflakes or ice crystals. Use cel-shading with grays, whites, and cool blue highlights."

**File**: `assets/sprites/terrain/floor_dungeon.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a dungeon floor tile in top-down view. The tile should show ancient stone blocks with glowing runes and mystic symbols. Use bright anime colors with bold black outlines for block definition. Make it look ancient and magical with anime dungeon aesthetics - mysterious, powerful. Include glowing magical symbols and subtle energy effects. Use cel-shading with dark grays, mystical blues, and glowing accents."

**File**: `assets/sprites/terrain/floor_boss_arena.png`
**AI Prompt**: "Create a 96x96 anime-style sprite of a boss arena floor tile in top-down view. The tile should show ornate stone with intricate patterns, magical circles, and energy lines. Use bright anime colors with bold black outlines for pattern definition. Make it look epic and powerful with anime final battle aesthetics - dramatic, imposing. Include glowing magical patterns and energy effects. Use cel-shading with deep reds, golds, and powerful light effects."

### Transition Tiles: Stairs & Gates

**Purpose**: Stairs enable floor-to-floor transitions (vertical movement between levels), while gates enable same-floor scene transitions (horizontal movement between areas on the same level).

#### Stairs (Floor-to-Floor Transitions)

**File**: `assets/sprites/terrain/stairs_up.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of upward stairs tile in top-down view. The tile should show stone steps ascending upward with an upward-pointing arrow symbol, magical glow around edges, and light emanating from the top. Use bright anime colors with bold black outlines. Make it look inviting and mystical with anime RPG aesthetics - clear, easy to identify. Include subtle sparkles rising upward and soft illumination. Use cel-shading with grays, blues, and ascending light effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/stairs_down.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of downward stairs tile in top-down view. The tile should show stone steps descending downward with a downward-pointing arrow symbol, shadowy depths visible below, and subtle dark aura around edges. Use bright anime colors with bold black outlines. Make it look mysterious but accessible with anime dungeon aesthetics - clear, identifiable. Include faint shadows descending and cool blue glow from depths. Use cel-shading with dark grays, deep blues, and descending shadow effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/stairs_left.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of leftward stairs tile in top-down view. The tile should show stone steps leading to the left with a left-pointing arrow symbol, architectural perspective showing left direction, and gentle glow indicating passage. Use bright anime colors with bold black outlines. Make it look directional and clear with anime navigation aesthetics - obvious direction. Include subtle motion lines pointing left and soft directional lighting. Use cel-shading with stone grays, warm lighting, and leftward flow effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/stairs_right.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of rightward stairs tile in top-down view. The tile should show stone steps leading to the right with a right-pointing arrow symbol, architectural perspective showing right direction, and gentle glow indicating passage. Use bright anime colors with bold black outlines. Make it look directional and clear with anime navigation aesthetics - obvious direction. Include subtle motion lines pointing right and soft directional lighting. Use cel-shading with stone grays, warm lighting, and rightward flow effects. Important: Use transparent background (PNG with alpha channel)."

#### Gates (Same-Floor Scene Transitions)

**File**: `assets/sprites/terrain/gate_north.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of a northern gate/doorway tile in top-down view. The tile should show an ornate doorway or archway leading upward/north on screen, with magical runes around the frame, soft energy barrier effect, and upward-pointing decorative elements. Use bright anime colors with bold black outlines. Make it look grand and portal-like with anime fantasy aesthetics - inviting, mystical. Include gentle particle effects floating upward and magical shimmer across the threshold. Use cel-shading with stone grays, mystical blues/purples, and magical energy effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/gate_south.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of a southern gate/doorway tile in top-down view. The tile should show an ornate doorway or archway leading downward/south on screen, with magical runes around the frame, soft energy barrier effect, and downward-pointing decorative elements. Use bright anime colors with bold black outlines. Make it look welcoming yet mysterious with anime fantasy aesthetics - accessible, enchanted. Include gentle particle effects floating downward and magical shimmer. Use cel-shading with stone grays, warm golds, and magical energy effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/gate_west.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of a western gate/doorway tile in top-down view. The tile should show an ornate doorway or archway leading left/west on screen, with magical runes around the frame, soft energy barrier effect, and leftward-pointing decorative elements. Use bright anime colors with bold black outlines. Make it look mystical and directional with anime portal aesthetics - clear passage. Include gentle particle effects drifting left and magical curtain effect. Use cel-shading with stone grays, cool blues, and magical energy effects. Important: Use transparent background (PNG with alpha channel)."

**File**: `assets/sprites/terrain/gate_east.png`
**AI Prompt**: "Create a 32x32 anime-style sprite of an eastern gate/doorway tile in top-down view. The tile should show an ornate doorway or archway leading right/east on screen, with magical runes around the frame, soft energy barrier effect, and rightward-pointing decorative elements. Use bright anime colors with bold black outlines. Make it look enchanted and directional with anime portal aesthetics - clear passage. Include gentle particle effects drifting right and magical curtain effect. Use cel-shading with stone grays, warm oranges, and magical energy effects. Important: Use transparent background (PNG with alpha channel)."

**Usage Notes**:
- **Stairs**: Place on StairLayer for vertical floor transitions (e.g., Ground Floor → 1st Floor)
- **Gates**: Place on StairLayer for horizontal scene transitions (e.g., Castle Exterior → Castle Interior, same floor)
- **Size**: 32x32 allows for tile-based placement (3 tiles fit in a 96x96 grid cell)
- **Visual Distinction**: Stairs show depth (up/down), gates show archways/portals (through/across)

## Priority 3: UI Elements

### Main Menu
**File**: `assets/sprites/ui/ui_main_menu_background.png`
**AI Prompt**: "Create a 320x240 anime-style background for an anime-style RPG main menu. The scene should show a beautiful anime landscape with rolling hills, a distant castle, and magical sky. Use bright anime colors with dramatic lighting. Include anime-style clouds, magical stars, and a large moon. Make it look epic and adventurous with typical anime opening scene aesthetics - inspiring, grand. Use cel-shading with vibrant blues, purples, and golden highlights."

### Battle Interface
**File**: `assets/sprites/ui/ui_battle_background.png`
**AI Prompt**: "Create a 320x240 anime-style background for anime-style battle scenes. The scene should show a mystical battleground with energy effects and dramatic sky. Use bright anime colors with dynamic lighting. Include anime-style energy auras, floating particles, and dramatic shadows. Make it look intense and exciting with anime battle aesthetics - dynamic, powerful. Use cel-shading with deep purples, electric blues, and energy highlights."

**File**: `assets/sprites/ui/ui_button_attack.png`
**AI Prompt**: "Create a 64x32 anime-style button sprite for 'ATTACK' in anime style. The button should have bright red background with bold yellow text, anime-style sword icon, and energy effects around the border. Use bright anime colors with bold black outlines. Make it look exciting and action-oriented with anime UI aesthetics - dynamic, attention-grabbing. Include subtle animation-ready highlights. Use cel-shading with vibrant reds, yellows, and energy effects."

**File**: `assets/sprites/ui/ui_button_defend.png`
**AI Prompt**: "Create a 64x32 anime-style button sprite for 'DEFEND' in anime style. The button should have bright blue background with white text, anime-style shield icon, and protective aura effects around the border. Use bright anime colors with bold black outlines. Make it look reliable and protective with anime UI aesthetics - solid, trustworthy. Include subtle glow effects. Use cel-shading with vibrant blues, whites, and protective light effects."

**File**: `assets/sprites/ui/ui_button_run.png`
**AI Prompt**: "Create a 64x32 anime-style button sprite for 'RUN' in anime style. The button should have bright green background with white text, anime-style wind/speed lines icon, and motion effects around the border. Use bright anime colors with bold black outlines. Make it look fast and urgent with anime UI aesthetics - energetic, swift. Include motion blur effects. Use cel-shading with vibrant greens, whites, and speed line effects."

## Priority 4: Status & Effects

### Status Icons
**File**: `assets/sprites/ui/icon_health.png`
**AI Prompt**: "Create a 16x16 anime-style heart icon in anime style. The heart should be bright red with anime-style highlights, small sparkles around it, and bold black outline. Use bright anime colors with cel-shading. Make it look vital and life-giving with anime magical item aesthetics - glowing, precious. Include subtle pulse effects. Use vibrant reds, pinks, and magical highlights."

**File**: `assets/sprites/ui/icon_experience.png`
**AI Prompt**: "Create a 16x16 anime-style star icon in anime style. The star should be bright yellow-gold with anime-style sparkles, energy radiating from points, and bold black outline. Use bright anime colors with cel-shading. Make it look valuable and empowering with anime power-up aesthetics - shining, magical. Include energy effects. Use vibrant golds, yellows, and light effects."

**File**: `assets/sprites/ui/icon_level.png`
**AI Prompt**: "Create a 16x16 anime-style upward arrow icon in anime style. The arrow should be bright blue with anime-style energy trails, upward motion lines, and bold black outline. Use bright anime colors with cel-shading. Make it look progressive and inspiring with anime growth aesthetics - ascending, powerful. Include upward energy effects. Use vibrant blues, whites, and ascending light trails."

### Combat Effects
**File**: `assets/sprites/effects/effect_hit_impact.png`
**AI Prompt**: "Create a 96x96 anime-style impact effect sprite in anime style. The effect should show explosive impact with anime-style action lines, energy bursts, and dynamic shapes. Use bright anime colors with bold black outlines. Make it look powerful and dramatic with anime battle effect aesthetics - intense, impactful. Include starburst patterns and energy waves. Use vibrant oranges, yellows, and white highlights."

**File**: `assets/sprites/effects/effect_magic_sparkles.png`
**AI Prompt**: "Create a 96x96 anime-style magical sparkle effect sprite in anime style. The effect should show floating sparkles, magical particles, and energy wisps. Use bright anime colors with soft glowing effects. Make it look mystical and beautiful with anime magic aesthetics - ethereal, enchanting. Include various sized sparkles and light particles. Use vibrant purples, blues, and golden magical effects."

**File**: `assets/sprites/effects/effect_level_up.png`
**AI Prompt**: "Create a 96x96 anime-style level up effect sprite in anime style. The effect should show triumphant light rays, rising stars, and celebratory sparkles. Use bright anime colors with bold highlights. Make it look victorious and empowering with anime achievement aesthetics - glorious, uplifting. Include ascending light beams and celebration particles. Use vibrant golds, whites, and rainbow highlights."

## File Organization

```
assets/sprites/
├── characters/
│   ├── player_hero/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_goblin/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_orc/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_skeleton_warrior/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_cave_spider/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_troll/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_forest_spirit/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_desert_scorpion/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_swamp_wretch/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_mountain_wyvern/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_dragon/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_dark_mage/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_dungeon_guardian/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   ├── enemy_demon_lord/
│   │   ├── frame1.png (96x96)
│   │   ├── frame2.png (96x96)
│   │   ├── frame3.png (96x96)
│   │   └── frame4.png (96x96)
│   └── enemy_ancient_dragon_king/
│       ├── frame1.png (96x96)
│       ├── frame2.png (96x96)
│       ├── frame3.png (96x96)
│       └── frame4.png (96x96)
├── terrain/
│   ├── floor_starting_area.png (96x96 - static)
│   ├── wall_generic.png (96x96 - static)
│   ├── floor_forest.png (96x96 - static)
│   ├── floor_cave.png (96x96 - static)
│   ├── floor_desert.png (96x96 - static)
│   ├── floor_swamp.png (96x96 - static)
│   ├── floor_mountain.png (96x96 - static)
│   ├── floor_dungeon.png (96x96 - static)
│   ├── floor_boss_arena.png (96x96 - static)
│   ├── stairs_up.png (32x32 - static, floor transition)
│   ├── stairs_down.png (32x32 - static, floor transition)
│   ├── stairs_left.png (32x32 - static, floor transition)
│   ├── stairs_right.png (32x32 - static, floor transition)
│   ├── gate_north.png (32x32 - static, scene transition)
│   ├── gate_south.png (32x32 - static, scene transition)
│   ├── gate_west.png (32x32 - static, scene transition)
│   └── gate_east.png (32x32 - static, scene transition)
├── ui/
│   ├── ui_main_menu_background.png (320x240 - static)
│   ├── ui_battle_background.png (320x240 - static)
│   ├── ui_button_attack.png (64x32 - static)
│   ├── ui_button_defend.png (64x32 - static)
│   ├── ui_button_run.png (64x32 - static)
│   ├── icon_health.png (16x16 - static)
│   ├── icon_experience.png (16x16 - static)
│   └── icon_level.png (16x16 - static)
└── effects/
    ├── effect_hit_impact.png (96x96 - static)
    ├── effect_magic_sparkles.png (96x96 - static)
    └── effect_level_up.png (96x96 - static)
```

## Implementation Priority Order

### Phase 1 (Start Here - 5 assets):
1. `player_hero/` folder with frame1-4.png - Immediate visual identity with smooth walking
2. `enemy_goblin/` folder with frame1-4.png - Most common enemy with bouncy movement
3. `enemy_orc/` folder with frame1-4.png - Starting progression with heavy steps
4. `floor_starting_area.png` (96x96 - static) - Basic world foundation
5. `wall_generic.png` (96x96 - static) - Complete maze structure

### Phase 2 (Enhanced Experience - 12 animated assets):
6-14. All remaining enemy character folders with frame1-4.png for complete creature roster
15. `floor_forest.png` through `floor_boss_arena.png` for themed areas

### Phase 3 (Professional Polish - 8 assets):
16-21. All UI elements for professional interface
22-24. All effect sprites for game juice and feedback

## Anime Style Guidelines Summary

- **Eyes**: Large, expressive, with anime shine highlights
- **Proportions**: Chibi/super deformed with exaggerated features
- **Colors**: Bright, saturated, with high contrast
- **Outlines**: Bold black lines for definition
- **Shading**: Cel-shading style with hard shadows and bright highlights
- **Background**: Transparent background (PNG with alpha channel) for all sprites
- **Effects**: Sparkles, energy auras, and magical glowing elements
- **Expressions**: Typical anime emotional ranges and poses
- **Animation**: 4-frame walking cycles with character-specific movement patterns
- **Details**: Clean, simplified, but with anime visual flair

## Animation Frame Breakdown

**Standard 4-Frame Walking Cycle**:
- **Frame 1**: Idle/Standing position (neutral pose)
- **Frame 2**: Left foot forward + character-specific motion
- **Frame 3**: Return to idle/standing (or slight variation)  
- **Frame 4**: Right foot forward + character-specific motion

**Character-Specific Movement Types**:
- **Hero**: Confident stride with cape flutter
- **Goblins/Orcs**: Bouncy hops with weapon bobbing
- **Spirits/Mages**: Floating/gliding motion with flowing robes
- **Spiders/Scorpions**: Multi-leg scuttling cycles
- **Dragons**: Wing-beat hovering with flame effects
- **Heavy units**: Ground-impact steps with armor clanking

Use these prompts with any AI image generator to create consistent anime-style assets for your RPG!

## Important Note for All Prompts

**CRITICAL**: For any prompts in this document that don't explicitly mention "transparent background (PNG with alpha channel)", please add this requirement to the end of each AI prompt before using it. All sprites, characters, enemies, terrain tiles, UI elements, and effects should have transparent backgrounds to ensure proper integration in the game engine.
