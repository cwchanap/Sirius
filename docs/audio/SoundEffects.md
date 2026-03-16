# Sirius Game Sound Effects Specification

## Overview
Derived from `scenes/game/Game.tscn` gameplay nodes (`GameManager`, `GridMap`, `PlayerController`, `EnemySpawn`, `UI/GameUI`). Covers movement, combat, UI feedback, and biome ambience.

## Sound Asset Checklist

### Footsteps
- [ ] `assets/audio/sfx/footsteps/footstep_ground_stone.wav`
- [ ] `assets/audio/sfx/footsteps/footstep_forest_grass.wav`
- [ ] `assets/audio/sfx/footsteps/footstep_desert_sand.wav`
- [ ] `assets/audio/sfx/footsteps/footstep_dungeon_stone.wav`

### Environment & Combat
- [ ] `assets/audio/sfx/environment/wall_collision_thud.wav`
- [ ] `assets/audio/sfx/ui/enemy_encounter_trigger.wav`
- [ ] `assets/audio/sfx/combat/battle_victory_stinger.wav`
- [ ] `assets/audio/sfx/combat/battle_defeat_stinger.wav`
- [ ] `assets/audio/sfx/combat/player_attack_impact.wav`
- [ ] `assets/audio/sfx/combat/enemy_attack_impact.wav`
- [ ] `assets/audio/sfx/combat/healing_effect.wav`

### UI Feedback
- [ ] `assets/audio/sfx/ui/damage_tick.wav`
- [ ] `assets/audio/sfx/ui/level_up_stinger.wav`
- [ ] `assets/audio/sfx/ui/exp_gain_tick.wav`
- [ ] `assets/audio/sfx/ui/panel_drag_start_stop.wav`
- [ ] `assets/audio/sfx/ui/menu_confirm.wav`
- [ ] `assets/audio/sfx/ui/menu_cancel.wav`

### Ambience
- [ ] `assets/audio/background/ambient_starting_area.ogg`
- [ ] `assets/audio/background/ambient_dungeon.ogg`
- [ ] `assets/audio/background/ambient_forest.ogg`

## Sound Effect Inventory
- **Player Footsteps – Ground Tiles**  
  Asset Path: `assets/audio/sfx/footsteps/footstep_ground_stone.wav`  
  Usage: Standard traversal on default floor tiles.  
  Prompt: ```Generate a 0.6-second stereo 44.1kHz clip of light leather-boot footsteps on stone tiles in a cavernous environment, minimal reverb, seamless loopable tail.```

- **Player Footsteps – Forest/Grass Transition**  
  Asset Path: `assets/audio/sfx/footsteps/footstep_forest_grass.wav`  
  Usage: When `GridMap` area color indicates forest terrain.  
  Prompt: ```Produce a 0.6-second stereo 44.1kHz loop of soft boot steps on slightly damp grass with light foliage rustle, gentle ambience.```

- **Player Footsteps – Desert Sand**  
  Asset Path: `assets/audio/sfx/footsteps/footstep_desert_sand.wav`  
  Usage: Movement over sand biome tiles.  
  Prompt: ```Create a 0.6-second stereo 44.1kHz loop of footsteps on dry sand, subtle grit crunch, warm airy ambience.```

- **Player Footsteps – Dungeon Stone**  
  Asset Path: `assets/audio/sfx/footsteps/footstep_dungeon_stone.wav`  
  Usage: Dark dungeon exploration.  
  Prompt: ```Generate a 0.6-second stereo 44.1kHz clip of cautious footfalls on hollow stone, low echo, ominous undertone.```

- **Wall Collision Thud**  
  Asset Path: `assets/audio/sfx/environment/wall_collision_thud.wav`  
  Usage: `PlayerController` attempts to move into `GridMap/WallLayer`.  
  Prompt: ```Render a 0.4-second stereo 44.1kHz dull thud with brief friction scrape, no harsh transients, conveys bumping into a stone wall.```

- **Enemy Encounter Trigger**  
  Asset Path: `assets/audio/sfx/ui/enemy_encounter_trigger.wav`  
  Usage: `GameManager.StartBattle()` launches combat.  
  Prompt: ```Design a 1.2-second stereo 44.1kHz rising whoosh with arcane chime hits, ending in a tense sting to signal battle initiation.```

- **Battle Victory Fanfare Stinger**  
  Asset Path: `assets/audio/sfx/combat/battle_victory_stinger.wav`  
  Usage: `BattleManager` reports victory.  
  Prompt: ```Create a 1.5-second stereo 44.1kHz triumphant brass + light percussion stinger, quick swell then resolve, heroic but concise.```

- **Battle Defeat Stinger**  
  Asset Path: `assets/audio/sfx/combat/battle_defeat_stinger.wav`  
  Usage: Loss outcome returning to main menu.  
  Prompt: ```Generate a 1.5-second stereo 44.1kHz somber low-string drop with distant bell toll, fading into silence.```

- **Player Attack Impact**  
  Asset Path: `assets/audio/sfx/combat/player_attack_impact.wav`  
  Usage: Player strike animation during combat.  
  Prompt: ```Produce a 0.5-second stereo 44.1kHz sharp melee impact with metallic ring and subtle grunt, suitable for sword hit.```

- **Enemy Attack Impact**  
  Asset Path: `assets/audio/sfx/combat/enemy_attack_impact.wav`  
  Usage: Enemy counter-attack effects.  
  Prompt: ```Craft a 0.5-second stereo 44.1kHz crunchy impact with darker tone, hint of monster snarl layered softly.```

- **Damage Taken UI Tick**  
  Asset Path: `assets/audio/sfx/ui/damage_tick.wav`  
  Usage: HP bar updates within `UI/GameUI/TopPanel`.  
  Prompt: ```Make a 0.3-second stereo 44.1kHz percussive tick with slight downward pitch glide to convey damage.```

- **Healing / Item Use**  
  Asset Path: `assets/audio/sfx/combat/healing_effect.wav`  
  Usage: Player regains HP via skills/items.  
  Prompt: ```Create a 0.6-second stereo 44.1kHz soft ascending shimmer with warm chime sparkles indicating healing.```

- **Level Up Celebration**  
  Asset Path: `assets/audio/sfx/ui/level_up_stinger.wav`  
  Usage: `Game.UpdatePlayerUI()` level increment.  
  Prompt: ```Generate a 1.0-second stereo 44.1kHz bright arpeggiated synth gliss with gentle burst, uplifting tone.```

- **EXP Gain Tick**  
  Asset Path: `assets/audio/sfx/ui/exp_gain_tick.wav`  
  Usage: `ExpBar` progress increases.  
  Prompt: ```Produce a 0.3-second stereo 44.1kHz subtle plucked tone with quick upward pitch for experience gain.```

- **UI Panel Drag Start/Stop**  
  Asset Path: `assets/audio/sfx/ui/panel_drag_start_stop.wav`  
  Usage: `UI/GameUI/TopPanel` drag interactions.  
  Prompt: ```Render two 0.25-second stereo 44.1kHz interface blips: one soft click for grab, one slightly higher click for release, clean and modern.```

- **Menu Button Confirm**  
  Asset Path: `assets/audio/sfx/ui/menu_confirm.wav`  
  Usage: Menu selections and confirmations.  
  Prompt: ```Make a 0.25-second stereo 44.1kHz crisp UI confirmation ping with quick decay, no reverb.```

- **Escape / Cancel**  
  Asset Path: `assets/audio/sfx/ui/menu_cancel.wav`  
  Usage: `BattleManager.EndBattleWithEscape()` and ESC actions.  
  Prompt: ```Generate a 0.3-second stereo 44.1kHz soft negative blip with downward pitch glide, unobtrusive.```

- **Ambient Loop – Starting Area**  
  Asset Path: `assets/audio/background/ambient_starting_area.ogg`  
  Usage: Background ambience for neutral zones.  
  Prompt: ```Create a 20-second stereo 44.1kHz ambient loop of gentle wind with faint distant wildlife, seamless looping tail.```

- **Ambient Loop – Dungeon**  
  Asset Path: `assets/audio/background/ambient_dungeon.ogg`  
  Usage: High-danger dungeon zones.  
  Prompt: ```Produce a 20-second stereo 44.1kHz atmospheric loop with low drones, dripping echoes, sparse metallic creaks, loopable.```

- **Ambient Loop – Forest**  
  Asset Path: `assets/audio/background/ambient_forest.ogg`  
  Usage: Forest biome background.  
  Prompt: ```Craft a 20-second stereo 44.1kHz ambient loop featuring rustling leaves, distant birds, soft breeze, loop-ready.```
