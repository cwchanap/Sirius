# Sirius - 2D Turn-Based RPG

A tactical RPG game built with Godot and C# featuring a complex maze world with themed areas and diverse enemies.

## Features

### Enhanced World Design
- **Complex Maze World**: Navigate through a large 160x160 grid with intricate pathways and winding corridors
- **Themed Combat Zones**: 8 distinct areas, each with unique enemies and visual themes
- **Dynamic Enemy Encounters**: Enemy types and difficulty scale based on location and distance from starting area
- **Visual Area Indication**: Different background colors indicate themed areas and enemy difficulty
- **Strategic Pathfinding**: Multiple routes to objectives with secret passages and interconnected chambers

### Themed Areas
- **Starting Area** (Light Gray): Safe zone with weak enemies (Goblins, Orcs)
- **Forest Zones** (Light Green): Nature-themed enemies (Forest Spirits, Goblins, Orcs)
- **Cave Systems** (Dark Gray): Underground creatures (Cave Spiders, Skeleton Warriors)
- **Desert Areas** (Sandy): Heat-adapted foes (Desert Scorpions, various mid-level enemies)
- **Swamp Lands** (Murky Green): Dark creatures (Swamp Wretches, Trolls, Dark Mages)
- **Mountain Peaks** (Light Blue): Flying enemies (Mountain Wyverns, Dragons)
- **Dungeon Complex** (Very Dark Gray): High-level enemies (Dungeon Guardians, Dark Mages, Demon Lords)
- **Boss Arena** (Golden): Ultimate challenges (Ancient Dragon King, Demon Lords)

### Combat System
- **Turn-based Combat**: Strategic battle system with multiple actions
- **Battle Actions**: Attack, Defend (50% damage reduction), Run (50% escape chance)
- **Character Progression**: Level up system with experience and stat growth
- **Enemy Variety**: 14 different enemy types with unique stats and rewards

## Controls

- **WASD** or **Arrow Keys**: Move around the maze
- **ESC**: Return to main menu (when not in battle)

## Visual Guide

- **Blue Square**: Player character
- **Colored Enemies**: Different colors indicate area themes and enemy difficulty levels
  - Pink = Starting area enemies (weak)
  - Green = Forest enemies
  - Brown = Cave enemies  
  - Yellow = Desert enemies
  - Purple = Swamp enemies
  - White = Mountain enemies
  - Dark Red = Dungeon enemies
  - Gold = Boss arena enemies (strongest)

## Enemy Types (Weakest to Strongest)

1. **Goblin** (Level 1): 50 HP, 15 ATK, 5 DEF, 10 SPD - 25 XP
2. **Orc** (Level 2): 80 HP, 22 ATK, 8 DEF, 8 SPD - 45 XP
3. **Forest Spirit** (Level 2): 90 HP, 20 ATK, 10 DEF, 15 SPD - 50 XP
4. **Skeleton Warrior** (Level 3): 120 HP, 28 ATK, 12 DEF, 9 SPD - 70 XP
5. **Cave Spider** (Level 3): 110 HP, 25 ATK, 8 DEF, 18 SPD - 65 XP
6. **Troll** (Level 4): 150 HP, 35 ATK, 15 DEF, 6 SPD - 120 XP
7. **Desert Scorpion** (Level 4): 130 HP, 32 ATK, 14 DEF, 11 SPD - 95 XP
8. **Dragon** (Level 5): 200 HP, 45 ATK, 20 DEF, 12 SPD - 180 XP
9. **Swamp Wretch** (Level 5): 160 HP, 38 ATK, 16 DEF, 7 SPD - 140 XP
10. **Dark Mage** (Level 6): 180 HP, 50 ATK, 18 DEF, 14 SPD - 220 XP
11. **Mountain Wyvern** (Level 6): 220 HP, 48 ATK, 22 DEF, 16 SPD - 200 XP
12. **Dungeon Guardian** (Level 7): 280 HP, 55 ATK, 28 DEF, 10 SPD - 300 XP
13. **Demon Lord** (Level 8): 300 HP, 65 ATK, 25 DEF, 15 SPD - 400 XP
14. **Ancient Dragon King** (Level 10): 500 HP, 80 ATK, 35 DEF, 18 SPD - 800 XP

## Character Stats

- **Health**: Current/Maximum hit points
- **Attack**: Base damage dealt to enemies
- **Defense**: Damage reduction from enemy attacks
- **Speed**: Determines turn order in battle
- **Level**: Character progression level
- **Experience**: Progress toward next level

## Getting Started

The game starts you in a safe starting area. Explore different themed zones to encounter various enemies and gain experience. Each area offers unique challenges and enemy types. Plan your route carefully as enemies get progressively stronger the further you venture from the starting area!

**Strategy Tips**:
- Start in the safe gray area to gain initial levels
- Forest and cave areas offer good early-game progression
- Desert and swamp areas are mid-game challenges
- Mountain and dungeon areas require high-level characters
- The golden boss arena contains the ultimate challenge

## Project Structure

```
sirius/
├── scenes/           # Scene files (.tscn)
│   ├── game/        # Game-related scenes
│   │   └── Game.tscn    # Main game scene
│   └── ui/          # UI scenes
│       ├── BattleScene.tscn # Battle interface
│       └── MainMenu.tscn    # Main menu
├── scripts/         # C# scripts (.cs)
│   ├── data/        # Data classes
│   │   ├── Character.cs # Player character data
│   │   └── Enemy.cs     # Enemy data and factory methods
│   ├── game/        # Game logic
│   │   ├── Game.cs          # Main game controller
│   │   ├── GameManager.cs   # Overall game state
│   │   ├── GridMap.cs       # Enhanced grid-based map system
│   │   └── PlayerController.cs # Player input handling
│   └── ui/          # UI controllers
│       ├── BattleManager.cs # Battle system UI
│       └── MainMenu.cs      # Main menu controller
├── assets/          # Game assets
│   ├── sprites/     # Image files
│   └── audio/       # Sound files
└── README.md        # This file
```

## Technical Features

- ✅ Complex procedural maze generation
- ✅ Themed area system with visual feedback
- ✅ Enhanced enemy AI and variety
- ✅ Optimized viewport culling for large maps
- ✅ Modular enemy factory system
- ✅ Area-based enemy spawning logic
- ✅ Turn-based battle system
- ✅ Character progression system
- ✅ Signal-based communication
- ✅ Scene management

## Development

Built with:
- Godot 4.x
- C# scripting
- Modular architecture
- Object-oriented design patterns

## Future Enhancements

1. **Visual Assets**: Replace colored rectangles with actual sprites
2. **Audio**: Add background music and themed area soundtracks
3. **Save System**: Persist game progress and character stats
4. **Items & Equipment**: Weapons, armor, consumables
5. **Skills & Magic**: Special abilities and spell system
6. **Quests & NPCs**: Story elements and interactive characters
7. **Minimap**: Navigation aid for the large world
8. **Difficulty Settings**: Customizable challenge levels
