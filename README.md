# Sirius - 2D Turn-Based RPG

## Project Structure

```
sirius/
├── scenes/           # Scene files (.tscn)
│   ├── game/        # Game-related scenes
│   │   └── Game.tscn    # Main game scene
│   └── ui/          # UI scenes
│       └── BattleScene.tscn # Battle interface
├── scripts/         # C# scripts (.cs)
│   ├── data/        # Data classes
│   │   ├── Character.cs # Player character data
│   │   └── Enemy.cs     # Enemy data
│   ├── game/        # Game logic
│   │   ├── GameManager.cs     # Overall game state
│   │   ├── GridMap.cs         # Grid-based map system
│   │   └── PlayerController.cs # Player input handling
│   └── ui/          # UI controllers
│       └── BattleManager.cs   # Battle system UI
├── assets/          # Game assets
│   ├── sprites/     # Image files
│   └── audio/       # Sound files
├── MainMenu.tscn    # Main menu scene
├── MainMenu.cs      # Main menu controller
└── Game.cs          # Legacy game controller (updated)
```

## Features Implemented

### Core Systems
- ✅ Main menu with Start Game, Settings, Quit
- ✅ Grid-based maze exploration
- ✅ Turn-based battle system
- ✅ Character progression (leveling up)
- ✅ Enemy encounters at fixed positions

### Game Mechanics
- ✅ Player movement (WASD/Arrow keys)
- ✅ Enemy encounters trigger battles
- ✅ Battle actions: Attack, Defend, Run
- ✅ Experience gain and leveling
- ✅ Health/damage system
- ✅ Game over on player defeat

### Technical Features
- ✅ Structured C# codebase
- ✅ Signal-based communication
- ✅ Scene management
- ✅ UI state management
- ✅ Modular enemy system

## How to Play

1. **Main Menu**: Choose "Start Game" to begin
2. **Exploration**: Use WASD or arrow keys to move through the maze
3. **Combat**: When you encounter a red enemy square:
   - **Attack**: Deal damage to the enemy
   - **Defend**: Reduce incoming damage by 50%
   - **Run**: 50% chance to escape the battle
4. **Progression**: Gain experience from victories to level up
5. **Controls**: Press ESC to return to main menu

## Enemy Types

- **Goblin** (Level 1): 50 HP, 15 ATK, 5 DEF - Awards 25 XP
- **Orc** (Level 2): 80 HP, 22 ATK, 8 DEF - Awards 40 XP  
- **Dragon** (Level 5): 200 HP, 45 ATK, 20 DEF - Awards 150 XP

## Character Stats

- **Health**: Current/Maximum hit points
- **Attack**: Base damage dealt to enemies
- **Defense**: Damage reduction from enemy attacks
- **Speed**: Determines turn order in battle
- **Level**: Character progression level
- **Experience**: Progress toward next level

## Next Steps for Development

1. **Visual Assets**: Replace colored rectangles with actual sprites
2. **Audio**: Add background music and sound effects
3. **More Content**: Additional enemy types, larger maps, items
4. **Save System**: Persist game progress
5. **Settings Menu**: Audio/video options
6. **Enhanced Combat**: Skills, magic, items
7. **Story Elements**: NPCs, quests, dialogue
