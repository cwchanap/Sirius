# Sirius RPG - AI Coding Agent Instructions

## Project Overview

Sirius is a 2D turn-based tactical RPG built with **Godot 4.x** and **C# scripting**. The game features a complex 160x160 grid-based maze world with 8 themed areas, 14+ enemy types, automatic turn-based combat, and sprite animation system.

## Architecture & Core Systems

### 🎮 **Game State Management (Singleton Pattern)**
- `GameManager` (singleton) manages global state: player data, battle state (`IsInBattle` flag)
- Scene flow: `MainMenu.tscn` → `Game.tscn` → Battle dialogs → back to game
- Player character persists across battles via GameManager singleton

### 🗺️ **Grid-Based World System**
- `GridMap.cs` handles 160x160 grid with **viewport culling** for performance
- **Themed areas** with position-based enemy spawning (forest, cave, desert, swamp, mountain, dungeon, boss)
- **Sprite system** with fallback to colored rectangles: loads 128x32 sprite sheets (4 frames of 32x32)
- **Complex maze generation**: main pathways, winding corridors, interconnected chambers, secret passages

### ⚔️ **Battle System (Auto-Combat)**
- **Modal battle dialogs** (`BattleScene.tscn`) as popup overlays
- **Automated combat** with AI decision-making (no manual button clicks)
- Turn order based on Speed stats, critical hits, defensive stances
- Battle end handling: experience gain, level-ups, enemy removal from grid

### 🎨 **Asset Pipeline**
- **Individual frame workflow**: Generate 32x32 frames → merge via `sprite_sheet_merger.py` → 128x32 sprite sheets
- **Animation system**: 4-frame walking cycles with character-specific movement patterns
- **Sprite loading hierarchy**: sprites → fallback to themed colors based on grid position

## Key Development Patterns

### **Signal-Based Communication**
```csharp
// Connect signals in _Ready()
_gameManager.BattleStarted += OnBattleStarted;
_gridMap.EnemyEncountered += OnEnemyEncountered;

// Emit signals for loose coupling
EmitSignal(SignalName.PlayerMoved, newPosition);
```

### **Position-Based Systems**
```csharp
// Area detection for enemy spawning and terrain
private bool IsInArea(int x, int y, int areaX, int areaY, int width, int height)

// Deterministic enemy types based on position (not random each frame)
int seed = x * 1000 + y;
float pseudoRand = (seed % 100) / 100.0f;
```

### **Viewport Culling Pattern**
```csharp
// Only draw visible cells for performance on large 160x160 grid
Camera2D camera = GetViewport().GetCamera2D();
int startX = Max(0, (int)((cameraPos.X - viewportSize.X/zoom/2) / CellSize) - padding);
```

### **Factory Pattern for Enemies**
```csharp
// Static factory methods in Enemy.cs
Enemy.CreateGoblin(), Enemy.CreateBoss(), etc.
```

## Critical Developer Workflows

### **Running the Game**
```bash
# Godot must be in PATH or use full path
godot --headless  # For headless builds/exports
godot             # Launch Godot editor
# Or use "Run Project" from Godot editor (F5)
```

### **Asset Generation Workflow**
```bash
# 1. Generate individual 32x32 frames using AI (see ASSET_REQUIREMENTS.md)
# 2. Place frames in assets/sprites/characters/{character_name}/
# 3. Merge frames into sprite sheets:
python3 tools/sprite_sheet_merger.py
# 4. Import in Godot: Filter: Off, Fix Alpha Border: On
```

### **Battle System Testing**
- Walk into colored enemies on the grid to trigger battles
- Auto-combat proceeds without input, ESC to return to menu
- Check console for detailed battle logs and state transitions

### **Scene Structure Navigation**
- Main scenes: `scenes/ui/MainMenu.tscn`, `scenes/game/Game.tscn`
- Battle UI: `scenes/ui/BattleScene.tscn` (instantiated as dialog)
- Scripts organized by domain: `scripts/game/`, `scripts/ui/`, `scripts/data/`

## Project-Specific Conventions

### **File Organization**
```
scripts/
├── data/          # Data classes (Character.cs, Enemy.cs)
├── game/          # Game logic (GameManager.cs, GridMap.cs, Game.cs)
└── ui/            # UI controllers (BattleManager.cs, MainMenu.cs)
```

### **Grid Coordinate System**
- Grid coordinates: (0,0) top-left, (159,159) bottom-right
- World coordinates: multiply by `CellSize` (32 pixels)
- Player starts at (5, GridHeight/2) - left side, center vertically

### **Animation Conventions**
- 4-frame cycles: idle → left step → idle variant → right step
- Character-specific motion: hero strides, goblins hop, spirits float, dragons hover
- Frame timing: 0.2 seconds per frame (5 FPS) for smooth movement

### **Memory & Performance Patterns**
- **Viewport culling** essential for 160x160 grid performance
- **Sprite caching** in dictionaries, load once in `_Ready()`
- **Position-based** enemy determination (avoid `GD.Randf()` in drawing loops)
- **Scene reuse**: instantiate battle dialogs, don't reload scenes

### **State Management**
- `GameManager.IsInBattle` prevents movement during combat
- Battle dialogs are **modal overlays**, don't hide main game
- **Signal-driven** state transitions prevent tight coupling

## Integration Points & Dependencies

### **Godot-Specific Considerations**
- **.NET 8.0** target framework (see `Sirius.csproj`)
- **Mobile rendering** method for broader compatibility
- **C# partial classes** required for Godot nodes (`public partial class`)
- **Resource classes** for data persistence (`[System.Serializable]`)

### **Asset Dependencies**
- **PIL/Pillow** for sprite sheet merging (auto-installed by script)
- **32x32 sprite frames** → **128x32 sprite sheets** pipeline
- **Transparent PNG** backgrounds essential for proper rendering

### **Scene Connections**
- `Game.cs` coordinates between GameManager, GridMap, and UI
- Camera follows player via `PlayerMoved` signal
- Battle system uses **deferred calls** to avoid timing issues

## Common Pitfalls & Solutions

### **Battle State Management**
```csharp
// ALWAYS check battle state before allowing movement
if (_gameManager.IsInBattle) return;

// Use signals to avoid race conditions
_battleManager.BattleFinished += OnBattleFinished;
```

### **Grid Boundary Safety**
```csharp
// Always check bounds before accessing grid
if (x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
    _grid[x, y] = value;
```

### **Sprite Loading Fallbacks**
```csharp
// Graceful degradation when sprites missing
if (UseSprites && _cellSprites.ContainsKey(cellType))
    DrawAnimatedSprite(_cellSprites[cellType], cellPos);
else
    DrawRect(cellRect, GetCellColor(x, y, cellType)); // Fallback
```

When working on this project, prioritize understanding the signal flow between GameManager → Game → GridMap → BattleManager, as this drives the core gameplay loop. The sprite system is modular and can be developed incrementally while the colored rectangle fallbacks provide immediate visual feedback.
