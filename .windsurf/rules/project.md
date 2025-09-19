---
trigger: always_on
---

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sirius is a 2D turn-based tactical RPG built with Godot 4.x and C# scripting (.NET 8.0). The game features a complex 160x160 grid-based maze world with 8 themed areas, 14+ enemy types, automated turn-based combat, and a sprite animation system with fallback to colored rectangles.

## Development Commands

### Building and Running
```bash
# Build the project (requires dotnet CLI)
dotnet build Sirius.sln

# Run from Godot editor (F5) or command line
godot  # Launch Godot editor to run project
# Note: Godot path configured in .vscode/settings.json as "/Applications/Godot_mono.app"

# Headless builds/exports
godot --headless
```

### Asset Generation
```bash
# Merge individual 32x32 sprite frames into 128x32 sprite sheets
python3 tools/sprite_sheet_merger.py
```

## Architecture Overview

### Core Systems
- **GameManager (Singleton)**: Global state management, player data persistence across battles, battle state tracking (`IsInBattle` flag)
- **GridMap**: 160x160 grid with viewport culling for performance, themed area system, position-based enemy spawning
- **Battle System**: Modal battle dialogs as popup overlays with automated AI combat decisions
- **Scene Flow**: MainMenu.tscn → Game.tscn → Battle dialogs → back to game

### Key Design Patterns
- **Signal-based communication** for loose coupling between systems
- **Singleton pattern** for GameManager global state
- **Factory pattern** for enemy creation (Enemy.CreateGoblin(), etc.)
- **Viewport culling** for large grid performance optimization
- **Position-based deterministic systems** (enemy types, area detection)

### Project Structure
```
scripts/
├── data/          # Data classes (Character.cs, Enemy.cs)
├── game/          # Core game logic (GameManager.cs, GridMap.cs, Game.cs, PlayerController.cs)
└── ui/            # UI controllers (BattleManager.cs, MainMenu.cs)

scenes/
├── game/          # Game.tscn (main game scene)
└── ui/            # MainMenu.tscn, BattleScene.tscn (battle dialog)

assets/sprites/    # Sprite sheets and individual frames
tools/             # sprite_sheet_merger.py for asset pipeline
```

## Critical Development Patterns

### Battle State Management
Always check `GameManager.IsInBattle` before allowing player movement. Use signals to avoid race conditions:
```csharp
if (_gameManager.IsInBattle) return;
_battleManager.BattleFinished += OnBattleFinished;
```

### Grid Coordinate System
- Grid coordinates: (0,0) top-left, (159,159) bottom-right
- World coordinates: multiply by CellSize (32 pixels)
- Player starts at (5, GridHeight/2)
- Always check bounds before accessing grid arrays

### Sprite System Integration
- Asset pipeline: 32x32 individual frames → merge to 128x32 sprite sheets
- 4-frame animation cycles with character-specific movement patterns
- Graceful fallback to colored rectangles when sprites missing
- Animation timing: 0.2 seconds per frame (5 FPS)

### Performance Considerations
- **Viewport culling** essential for 160x160 grid performance
- **Sprite caching** in dictionaries, load once in _Ready()
- **Position-based** enemy determination (avoid GD.Randf() in drawing loops)
- **Scene reuse** for battle dialogs

## Godot-Specific Requirements

- C# partial classes required for Godot nodes: `public partial class`
- Resource classes need `[System.Serializable]` for data persistence  
- Mobile rendering method configured for broader compatibility
- Signal connections should be made in _Ready() methods
- Use deferred calls for battle system timing issues

## Common Integration Points

- Camera follows player via PlayerMoved signal
- GameManager coordinates between Game, GridMap, and UI systems
- Battle system uses modal overlays, doesn't hide main game
- Enemy encounters trigger through GridMap → BattleManager signal flow
- Asset loading hierarchy: sprites → themed colors based on grid position

## Testing and Debugging

### Running and Testing
```bash
# Build and run the project
dotnet build Sirius.sln
# Then run from Godot editor (F5) or open Godot and use "Run Project"

# Asset generation when individual sprite frames exist
python3 tools/sprite_sheet_merger.py
```

### Debugging Workflow
- Walk into colored enemies on grid to trigger battles
- Auto-combat proceeds without input, ESC returns to menu
- Console provides detailed battle logs and state transitions
- Enemy types and spawning are deterministic based on grid position
- Check GameManager.IsInBattle flag for state debugging

### Common Troubleshooting
- **Battle state stuck**: Use `GameManager.ResetBattleState()` method
- **Sprite rendering issues**: Ensure sprite sheets are 128x32 (4 frames of 32x32)
- **Performance problems**: Verify viewport culling is active in GridMap._Draw()
- **Grid bounds errors**: Always validate coordinates before accessing grid arrays
- **Asset pipeline**: Individual 32x32 frames → merge via Python script → import in Godot