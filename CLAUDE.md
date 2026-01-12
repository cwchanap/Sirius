# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sirius is a 2D turn-based tactical RPG built with Godot 4.4.1 and C# scripting (.NET 8.0). The game features a 160x160 grid-based maze world with 8 themed areas, 14+ enemy types, automated turn-based combat, and a sprite animation system with fallback to colored rectangles.

## Development Commands

```bash
# Build the project
dotnet build Sirius.sln

# Run tests (GdUnit4 framework)
dotnet test Sirius.sln

# Run from Godot editor (F5) or launch editor
godot

# Merge individual 32x32 sprite frames into 128x32 sprite sheets
python3 tools/sprite_sheet_merger.py
```

**Shell issues**: If you encounter `zsh: command not found`, restart shell with `zsh -il`.

## Architecture Overview

### Core Systems
- **GameManager (Singleton)**: Global state management, player data persistence across battles, battle state tracking (`IsInBattle` flag)
- **GridMap**: 160x160 grid with viewport culling for performance, themed area system, position-based enemy spawning
- **FloorManager**: Multi-floor system with stair connections between floors (FloorGF.tscn, Floor1F.tscn, etc.)
- **Battle System**: Modal battle dialogs as popup overlays with automated AI combat decisions
- **Scene Flow**: MainMenu.tscn → Game.tscn → Battle dialogs → back to game

### Key Design Patterns
- **Signal-based communication** for loose coupling between systems
- **Singleton pattern** for GameManager global state
- **Factory pattern** for enemy creation (Enemy.CreateGoblin(), etc.)
- **Viewport culling** for large grid performance optimization
- **Position-based deterministic systems** (enemy types, area detection using seed-based pseudo-random)

### Project Structure
```
scripts/
├── data/          # Data classes (Character.cs, Enemy.cs, Item.cs, Inventory.cs)
├── game/          # Core game logic (GameManager.cs, GridMap.cs, FloorManager.cs, PlayerController.cs)
├── tilemap_json/  # Tilemap import/export (TilemapJsonImporter.cs, TilemapJsonExporter.cs)
└── ui/            # UI controllers (BattleManager.cs, MainMenu.cs, InventoryMenuController.cs)

scenes/
├── game/          # Game.tscn, floors/ (FloorGF.tscn, Floor1F.tscn)
├── ui/            # MainMenu.tscn, BattleScene.tscn, InventoryMenu.tscn
└── spawns/        # Enemy spawn scenes (EnemySpawn_Goblin.tscn, etc.)

tests/             # GdUnit4 tests mirroring scripts/ structure
├── data/          # CharacterTest.cs, EnemyTest.cs, InventoryTest.cs, ItemTest.cs
└── game/          # GameManagerTest.cs

assets/sprites/    # Sprite sheets and individual frames
tools/             # sprite_sheet_merger.py for asset pipeline
```

## Critical Development Patterns

### Battle State Management
Always check `GameManager.IsInBattle` before allowing player movement:
```csharp
if (_gameManager.IsInBattle) return;
_battleManager.BattleFinished += OnBattleFinished;
```

### Grid Coordinate System
- Grid coordinates: (0,0) top-left, (159,159) bottom-right
- World coordinates: multiply by CellSize (32 pixels)
- Player starts at (5, GridHeight/2)
- Always check bounds before accessing grid arrays

### Sprite System
- Asset pipeline: 32x32 individual frames → merge to 128x32 sprite sheets
- 4-frame animation cycles (idle → left step → idle variant → right step)
- Graceful fallback to colored rectangles when sprites missing
- Animation timing: 0.2 seconds per frame (5 FPS)

### Performance Requirements
- **Viewport culling** essential for 160x160 grid (must maintain 60fps)
- **Sprite caching** in dictionaries, load once in _Ready()
- **Position-based** enemy determination (avoid GD.Randf() in drawing loops)
- **Scene reuse**: instantiate battle dialogs, don't reload scenes

## Godot/C# Requirements

- C# partial classes required for Godot nodes: `public partial class`
- Resource classes need `[System.Serializable]` for data persistence
- Signal connections should be made in _Ready() methods
- Use deferred calls when modifying scene tree during signal callbacks

## Testing

Tests use GdUnit4 framework with `[TestSuite]` and `[TestCase]` attributes:
```csharp
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class YourTest : Node
{
    [TestCase]
    public void TestYourFeature()
    {
        var obj = new YourClass();
        obj.DoSomething();
        AssertThat(obj.Result).IsEqual(expectedValue);
    }
}
```

Test files must mirror source structure: `scripts/data/Character.cs` → `tests/data/CharacterTest.cs`

## Debugging

- Walk into colored enemies on grid to trigger battles
- Auto-combat proceeds without input, ESC returns to menu
- Console provides detailed battle logs and state transitions
- **Battle state stuck**: Use `GameManager.ResetBattleState()` method
- **Grid bounds errors**: Always validate coordinates before accessing grid arrays