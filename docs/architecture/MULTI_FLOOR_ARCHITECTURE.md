# Multi-Floor Architecture Implementation Plan

## Overview

This document outlines the architecture for supporting multiple floors (G/F, 1/F, 2/F, etc.) in Sirius while minimizing code duplication and maximizing scalability.

## Design Principles

1. **Separate floor data from game logic** - Each floor is a separate scene containing only layout data
2. **Reuse all game systems** - Keep battle, inventory, UI in the main Game scene
3. **Dynamic floor loading** - Load/unload floor scenes as needed
4. **Minimal duplication** - Share GridMap script, UI, and all game logic
5. **Scene-based workflow** - Leverage Godot's scene instancing system

## Architecture Components

### 1. File Structure

```
scenes/
  game/
    Game.tscn                     # Main game scene (no floor-specific data)
    floors/
      FloorGF.tscn               # Ground Floor layout (TileMaps + EnemySpawns)
      Floor1F.tscn               # 1st Floor layout
      Floor2F.tscn               # 2nd Floor layout (future)
      Floor3F.tscn               # 3rd Floor layout (future)
      ...
  
scripts/
  game/
    FloorDefinition.cs           # NEW: Resource class for floor metadata
    FloorManager.cs              # NEW: Floor loading and transition logic
    Game.cs                      # MODIFIED: Delegates to FloorManager
    GridMap.cs                   # MODIFIED: Add LoadFloor() method
    PlayerController.cs          # MINIMAL CHANGES: Handle stair transitions

resources/
  floors/
    FloorGF.tres                 # FloorDefinition resource for Ground Floor
    Floor1F.tres                 # FloorDefinition resource for First Floor
    Floor2F.tres                 # FloorDefinition resource for 2nd Floor (future)
    ...
```

### 2. New Components

#### FloorDefinition.cs (Resource)

**Purpose**: Defines metadata and configuration for each floor as a reusable resource

```csharp
using Godot;

[GlobalClass]
public partial class FloorDefinition : Resource
{
    // Floor identification
    [Export] public string FloorName { get; set; } = "Ground Floor";
    [Export] public int FloorNumber { get; set; } = 0;
    
    // Floor scene reference
    [Export] public PackedScene FloorScene { get; set; }
    
    // Player spawn configuration
    [Export] public Vector2I PlayerStartPosition { get; set; } = new Vector2I(5, 80);
    
    // Stair/transition points to other floors
    [Export] public Godot.Collections.Array<Vector2I> StairsUp { get; set; } = new();
    [Export] public Godot.Collections.Array<Vector2I> StairsDown { get; set; } = new();
    
    // Visual/audio theming (optional)
    [Export] public AudioStream BackgroundMusic { get; set; }
    [Export] public Color AmbientTint { get; set; } = new Color(1, 1, 1, 1);
    [Export] public string FloorDescription { get; set; } = "";
    
    /// <summary>
    /// Check if the given position has stairs
    /// </summary>
    public bool HasStairAt(Vector2I position, out bool isUp)
    {
        isUp = false;
        if (StairsUp.Contains(position))
        {
            isUp = true;
            return true;
        }
        if (StairsDown.Contains(position))
        {
            isUp = false;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Get the destination spawn position for stairs going in the specified direction
    /// </summary>
    public Vector2I GetStairDestination(bool goingUp, int stairIndex = 0)
    {
        // Return corresponding stair position on this floor
        // If going up, we arrived via StairsDown
        // If going down, we arrived via StairsUp
        var targetStairs = goingUp ? StairsDown : StairsUp;
        if (targetStairs.Count > stairIndex)
        {
            return targetStairs[stairIndex];
        }
        // Fallback to default spawn
        return PlayerStartPosition;
    }
}
```

#### FloorManager.cs

**Purpose**: Handles floor loading, unloading, and transitions using FloorDefinition resources

```csharp
using Godot;
using System.Collections.Generic;

public partial class FloorManager : Node
{
    // Array of floor definitions (set in editor)
    [Export] public Godot.Collections.Array<FloorDefinition> Floors { get; set; } = new();
    
    // Current state
    private int _currentFloorIndex = 0;
    private Node2D _currentFloorInstance;
    private GridMap _currentGridMap;
    
    // Signals for floor transitions
    [Signal] public delegate void FloorChangedEventHandler(int oldFloorIndex, int newFloorIndex);
    [Signal] public delegate void FloorLoadedEventHandler(FloorDefinition floorDef, GridMap gridMap);
    
    [Export] public bool EnableDebugLogging { get; set; } = true;
    
    public int CurrentFloorIndex => _currentFloorIndex;
    public FloorDefinition CurrentFloorDefinition => Floors.Count > _currentFloorIndex ? Floors[_currentFloorIndex] : null;
    public GridMap CurrentGridMap => _currentGridMap;
    
    public override void _Ready()
    {
        if (Floors.Count == 0)
        {
            GD.PushError("FloorManager has no floors defined! Add FloorDefinition resources.");
            return;
        }
        
        // Load initial floor (index 0)
        LoadFloor(0);
    }
    
    /// <summary>
    /// Load a specific floor by index
    /// </summary>
    public bool LoadFloor(int floorIndex, Vector2I? playerSpawnOverride = null)
    {
        if (floorIndex < 0 || floorIndex >= Floors.Count)
        {
            GD.PushError($"Floor index {floorIndex} out of range (0-{Floors.Count - 1})!");
            return false;
        }
        
        var floorDef = Floors[floorIndex];
        if (floorDef == null)
        {
            GD.PushError($"Floor definition at index {floorIndex} is null!");
            return false;
        }
        
        if (floorDef.FloorScene == null)
        {
            GD.PushError($"Floor '{floorDef.FloorName}' has no scene assigned!");
            return false;
        }
        
        if (EnableDebugLogging)
            GD.Print($"🏢 Loading floor {floorIndex}: {floorDef.FloorName}...");
        
        // Unload current floor
        UnloadCurrentFloor();
        
        // Instantiate new floor scene
        var floorInstance = floorDef.FloorScene.Instantiate<Node2D>();
        if (floorInstance == null)
        {
            GD.PushError($"Failed to instantiate floor scene for '{floorDef.FloorName}'!");
            return false;
        }
        
        // Add to scene tree under Game node's GridMap parent position
        var game = GetParent();
        game.AddChild(floorInstance);
        _currentFloorInstance = floorInstance;
        
        // Wait for floor to be ready, then finalize
        if (!floorInstance.IsNodeReady())
        {
            CallDeferred(nameof(FinalizeFloorLoad), floorIndex, floorDef, playerSpawnOverride);
        }
        else
        {
            FinalizeFloorLoad(floorIndex, floorDef, playerSpawnOverride);
        }
        
        return true;
    }
    
    private void FinalizeFloorLoad(int floorIndex, FloorDefinition floorDef, Vector2I? playerSpawnOverride)
    {
        int oldFloorIndex = _currentFloorIndex;
        _currentFloorIndex = floorIndex;
        
        // Find GridMap in the instantiated floor scene
        _currentGridMap = _currentFloorInstance.GetNodeOrNull<GridMap>("GridMap");
        if (_currentGridMap == null)
        {
            GD.PushError($"Floor '{floorDef.FloorName}' scene has no GridMap child!");
            return;
        }
        
        // Call GridMap.LoadFloor to initialize grid and register enemies
        var spawnPos = playerSpawnOverride ?? floorDef.PlayerStartPosition;
        _currentGridMap.LoadFloor(_currentFloorInstance, floorDef, spawnPos);
        
        if (EnableDebugLogging)
            GD.Print($"✅ Floor {floorIndex} loaded: {floorDef.FloorName}");
        
        EmitSignal(SignalName.FloorChanged, oldFloorIndex, floorIndex);
        EmitSignal(SignalName.FloorLoaded, floorDef, _currentGridMap);
    }
    
    /// <summary>
    /// Unload the current floor and free resources
    /// </summary>
    public void UnloadCurrentFloor()
    {
        if (_currentFloorInstance != null)
        {
            if (EnableDebugLogging)
                GD.Print($"🗑️ Unloading floor {_currentFloorIndex}");
            
            _currentFloorInstance.QueueFree();
            _currentFloorInstance = null;
            _currentGridMap = null;
        }
    }
    
    /// <summary>
    /// Transition to a different floor (for stairs)
    /// </summary>
    public void TransitionToFloor(int targetFloorIndex, bool isGoingUp, int stairIndex = 0)
    {
        if (targetFloorIndex < 0 || targetFloorIndex >= Floors.Count)
        {
            GD.Print($"Cannot transition to floor {targetFloorIndex} - not available!");
            return;
        }
        
        // Get destination spawn from target floor definition
        var targetFloor = Floors[targetFloorIndex];
        Vector2I spawnPos = targetFloor.GetStairDestination(isGoingUp, stairIndex);
        
        LoadFloor(targetFloorIndex, spawnPos);
    }
    
    /// <summary>
    /// Check if player is on a stair tile
    /// </summary>
    public bool IsOnStairs(Vector2I position, out bool isUp, out int targetFloorIndex)
    {
        isUp = false;
        targetFloorIndex = -1;
        
        var currentDef = CurrentFloorDefinition;
        if (currentDef == null)
            return false;
        
        if (currentDef.HasStairAt(position, out isUp))
        {
            targetFloorIndex = _currentFloorIndex + (isUp ? 1 : -1);
            return targetFloorIndex >= 0 && targetFloorIndex < Floors.Count;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get total number of floors available
    /// </summary>
    public int GetFloorCount() => Floors.Count;
}
```

### 3. Floor Scene Structure

Each floor scene (`FloorGF.tscn`, `Floor1F.tscn`, etc.) is a lightweight Node2D containing only layout:

```
FloorGF (Node2D) - NO SCRIPT NEEDED
  └─ GridMap (Node2D with GridMap.cs)
      ├─ GroundLayer (TileMapLayer) - unique floor tiles
      ├─ WallLayer (TileMapLayer) - unique wall layout  
      └─ EnemySpawn_* (Sprite2D nodes) - unique enemy placements
```

**Floor scenes contain ONLY visual/layout data. All metadata lives in the .tres resource!**

### 4. FloorDefinition Resource (.tres)

Create a FloorDefinition resource for each floor in `resources/floors/`:

**FloorGF.tres Properties**:
- `FloorName`: "Ground Floor"
- `FloorNumber`: 0
- `FloorScene`: res://scenes/game/floors/FloorGF.tscn
- `PlayerStartPosition`: Vector2I(5, 80)
- `StairsUp`: [Vector2I(50, 50), Vector2I(120, 30)] (positions leading to 1/F)
- `StairsDown`: [] (empty on G/F)
- `BackgroundMusic`: (optional AudioStream)
- `AmbientTint`: Color(1, 1, 1, 1)
- `FloorDescription`: "The entrance level"

### 5. Modified Game.tscn

The main Game scene becomes lighter:

```
Game (Node2D with Game.cs)
  ├─ GameManager (Node with GameManager.cs) - unchanged
  ├─ FloorManager (Node with FloorManager.cs) - NEW
  ├─ Camera2D - unchanged
  ├─ PlayerController (Node with PlayerController.cs) - minimal changes
  └─ UI (CanvasLayer) - unchanged
      └─ GameUI, TopPanel, etc. - unchanged
```

**Removed**: GridMap node (now loaded dynamically by FloorManager)

### 6. Modifications to Existing Scripts

#### Game.cs Changes

```csharp
// Replace direct GridMap reference
private FloorManager _floorManager;
private GridMap _gridMap; // Now becomes dynamic reference

public override void _Ready()
{
    // ... existing code ...
    
    // Get FloorManager instead of direct GridMap
    _floorManager = GetNode<FloorManager>("FloorManager");
    
    // Connect to floor loading events
    _floorManager.FloorLoaded += OnFloorLoaded;
    _floorManager.FloorChanged += OnFloorChanged;
    
    // GridMap reference will be set when floor loads
    // Remove direct GetNode<GridMap> calls
    
    // ... rest of existing code ...
}

private void OnFloorLoaded(FloorDefinition floorDef, GridMap gridMap)
{
    // Update dynamic reference
    _gridMap = gridMap;
    
    // Reconnect signals
    if (_gridMap != null)
    {
        _gridMap.EnemyEncountered += OnEnemyEncountered;
        _gridMap.PlayerMoved += OnPlayerMoved;
    }
    
    // Update camera position
    CallDeferred(nameof(SetInitialCameraPosition));
    
    // Optional: Update floor indicator UI
    UpdateFloorIndicator(floorDef.FloorName);
    
    GD.Print($"Floor '{floorDef.FloorName}' ready for gameplay");
}

private void OnFloorChanged(int oldFloor, int newFloor)
{
    GD.Print($"Transitioned from floor {oldFloor} to floor {newFloor}");
    UpdatePlayerUI();
}
```

#### GridMap.cs Changes

```csharp
/// <summary>
/// Initialize grid from loaded floor instance
/// </summary>
public void LoadFloor(Node2D floorInstance, FloorDefinition floorDef, Vector2I playerSpawn)
{
    // Cache TileMapLayers from the floor instance
    _groundLayer = floorInstance.GetNodeOrNull<TileMapLayer>("GridMap/GroundLayer");
    _wallLayer = floorInstance.GetNodeOrNull<TileMapLayer>("GridMap/WallLayer");
    
    // Rebuild grid from baked TileMaps
    BuildGridFromBakedTileMaps();
    
    // Set player position from floor definition
    _playerPosition = playerSpawn;
    if (IsWithinGrid(playerSpawn))
    {
        _grid[playerSpawn.X, playerSpawn.Y] = (int)CellType.Player;
    }
    
    // Register all EnemySpawn nodes from this floor
    CallDeferred(nameof(RegisterStaticEnemySpawns));
    
    if (EnableDebugLogging)
        GD.Print($"GridMap loaded for floor: {floorDef.FloorName}, player at {playerSpawn}");
}
```

#### PlayerController.cs Changes

```csharp
// Add check for stair transitions
private void HandleMovement()
{
    // ... existing movement code ...
    
    // After successful move, check for stairs
    var floorManager = GetNode<FloorManager>("../FloorManager");
    if (floorManager.IsOnStairs(newPosition, out bool isUp, out int targetFloor))
    {
        // Show prompt to player
        GD.Print($"Press E to take stairs {(isUp ? "up" : "down")}");
        // Store pending transition
    }
}

public override void _Input(InputEvent @event)
{
    // Add stair interaction
    if (@event.IsActionPressed("interact") && _pendingStairTransition)
    {
        var floorManager = GetNode<FloorManager>("../FloorManager");
        floorManager.TransitionToFloor(_targetFloor, _isGoingUp);
    }
}
```

## Implementation Steps

### Phase 1: Setup Foundation (1-2 hours)

1. **Create new scripts**
   - [ ] Create `scripts/game/FloorDefinition.cs` (Resource class)
   - [ ] Create `scripts/game/FloorManager.cs`

2. **Create directories**
   - [ ] Create `scenes/game/floors/` directory
   - [ ] Create `resources/floors/` directory

3. **Build the project**
   - Rebuild C# project so FloorDefinition becomes available in editor
   - Verify [GlobalClass] makes it appear in resource creation menu

### Phase 2: Extract Ground Floor (2-3 hours)

4. **Create FloorGF.tscn**
   - Create new scene with Node2D root (no script needed)
   - Move existing GridMap from Game.tscn to FloorGF.tscn as child
   - GridMap should contain GroundLayer, WallLayer, EnemySpawn nodes
   - Save as `scenes/game/floors/FloorGF.tscn`

5. **Create FloorGF.tres resource**
   - In Godot: FileSystem → resources/floors/ → Create New Resource
   - Choose FloorDefinition
   - Set FloorScene: res://scenes/game/floors/FloorGF.tscn
   - Set FloorName: "Ground Floor"
   - Set FloorNumber: 0
   - Set PlayerStartPosition: Vector2I(5, 80) or current spawn
   - Leave StairsUp/Down empty for now
   - Save as `resources/floors/FloorGF.tres`

6. **Modify Game.tscn**
   - Remove GridMap node from Game scene
   - Add FloorManager node (Node type)
   - Attach FloorManager.cs script
   - In FloorManager inspector, add FloorGF.tres to Floors array

7. **Update Game.cs**
   - Implement OnFloorLoaded signal handler
   - Implement OnFloorChanged signal handler
   - Replace _gridMap = GetNode<GridMap>() with dynamic assignment

8. **Add GridMap.LoadFloor() method**
   - Add the LoadFloor method to GridMap.cs as shown above
   - Should cache layers, rebuild grid, set player position

9. **Test Ground Floor**
   - Run game, verify G/F loads
   - Verify movement, battles, enemies work
   - Verify camera follows player
   - Check console for load messages

### Phase 3: Create First Floor (2-3 hours)

10. **Design 1/F layout**
    - Duplicate FloorGF.tscn → Floor1F.tscn
    - Clear GroundLayer and repaint with different terrain
    - Clear WallLayer and repaint with different maze layout
    - Delete existing EnemySpawn nodes
    - Add new EnemySpawn nodes at different positions/types
    - Save scene

11. **Create Floor1F.tres resource**
    - Create New Resource → FloorDefinition
    - Set FloorScene: res://scenes/game/floors/Floor1F.tscn
    - Set FloorName: "First Floor"
    - Set FloorNumber: 1
    - Set PlayerStartPosition: Vector2I(appropriate spawn)
    - Set StairsDown: positions where player arrives from G/F
    - Leave StairsUp empty (or add if 2/F exists)
    - Save as `resources/floors/Floor1F.tres`

12. **Add stair markers to FloorGF.tres**
    - Open FloorGF.tres in inspector
    - Set StairsUp: [Vector2I(50, 50)] (coordinates where stairs are)
    - Save

13. **Register Floor1F in FloorManager**
    - Open Game.tscn
    - Select FloorManager node
    - Add Floor1F.tres to Floors array (index 1)
    - Now Floors = [FloorGF.tres, Floor1F.tres]

### Phase 4: Implement Transitions (3-4 hours)

14. **Add stair detection to PlayerController**
    - After successful move, check FloorManager.IsOnStairs()
    - Store pending transition state
    - Show UI prompt "Press E to go up/down stairs"

15. **Add stair interaction**
    - Listen for "interact" action (E key)
    - Call FloorManager.TransitionToFloor(targetIndex, isGoingUp)
    - Clear pending state after transition

16. **Test basic transitions**
    - Walk to stair position on G/F
    - Press E, verify transition to 1/F
    - Verify player spawns at correct StairsDown position
    - Walk back to stairs, press E
    - Verify return to G/F at StairsUp position

17. **Polish transitions** (optional)
    - Add fade-out/fade-in screen effect
    - Add transition sound effect
    - Show "Loading floor..." message

18. **Test multi-floor gameplay**
    - Defeat enemy on G/F, go upstairs
    - Verify enemy stays defeated when returning
    - Verify player stats (HP, gold, XP) persist
    - Test battle on each floor

### Phase 5: Polish & Scale (1-2 hours)

19. **Add floor indicator UI**
    - Add label to TopPanel showing floor name
    - Update in OnFloorLoaded handler
    - Example: "Current Floor: Ground Floor"

20. **Create template for new floors**
    - Duplicate Floor1F.tscn → FloorTemplate.tscn
    - Clear to minimal layout
    - Document creation process below

21. **Add additional floors** (2/F, 3/F as needed)
    - Follow "Creating New Floors" guide below
    - Each new floor = scene + resource + array entry

## How to Create FloorDefinition Resources

Since FloorDefinition is a custom C# Resource class, here's how to create `.tres` files in the Godot editor:

### Method 1: Via FileSystem Panel
1. Right-click in `resources/floors/` directory
2. Select "Create New" → "Resource"
3. In the dialog, search for "FloorDefinition"
4. Select it and click "Create"
5. Name it (e.g., `FloorGF.tres`)
6. Select the created resource in FileSystem
7. Configure properties in the Inspector panel

### Method 2: Via Inspector
1. Select FloorManager node in Game.tscn
2. In Inspector, find the "Floors" array property
3. Click "Add Element"
4. Click the dropdown on the new element
5. Choose "New FloorDefinition"
6. Click the save icon to save as .tres file
7. Configure the properties

### Required Properties for Each FloorDefinition
- **FloorScene**: Drag the floor .tscn file from FileSystem
- **FloorName**: Human-readable name (e.g., "Ground Floor")
- **FloorNumber**: Numeric identifier (0, 1, 2, etc.)
- **PlayerStartPosition**: Vector2I(x, y) spawn coordinates
- **StairsUp**: Array of Vector2I positions leading to floor above
- **StairsDown**: Array of Vector2I positions leading to floor below
- **BackgroundMusic**: (Optional) AudioStream resource
- **AmbientTint**: (Optional) Color overlay
- **FloorDescription**: (Optional) Text description

## Creating New Floors (Quick Reference)

### To add a new floor (e.g., Floor 2/F):

1. **Create scene**: Duplicate `Floor1F.tscn` → `Floor2F.tscn`
   - Edit GroundLayer tiles (different terrain)
   - Edit WallLayer tiles (different maze)
   - Reposition/replace EnemySpawn nodes

2. **Create resource**: New Resource → FloorDefinition → `Floor2F.tres`
   - FloorScene: res://scenes/game/floors/Floor2F.tscn
   - FloorName: "Second Floor"
   - FloorNumber: 2
   - PlayerStartPosition: Vector2I(x, y)
   - StairsDown: [positions connecting to 1/F]
   - StairsUp: [positions connecting to 3/F] (if applicable)

3. **Update previous floor**: Edit `Floor1F.tres`
   - Add StairsUp: [position(s) leading to 2/F]

4. **Register**: Open Game.tscn → FloorManager
   - Add Floor2F.tres to Floors array at index 2

5. **Test**: Run game
   - Navigate to 1/F stairs
   - Verify transition to 2/F works
   - Verify back-transition works

## Benefits Summary

✅ **Zero Code Duplication**: All systems (battle, inventory, UI, camera) shared  
✅ **Data-Driven Design**: Floor metadata in reusable .tres resources  
✅ **Easy Expansion**: New floor = scene + resource + array entry  
✅ **Clean Separation**: Layout (scene) separate from metadata (resource)  
✅ **Memory Efficient**: Only one floor loaded at a time  
✅ **Designer-Friendly**: Non-programmers can create floors via editor  
✅ **Version Control**: .tres resources merge better than embedded scene data  

## Alternative Approaches Considered

### Alt 1: Multiple GridMaps in Single Scene
- **Pros**: Simpler, everything in one file
- **Cons**: Scene bloat, harder to edit, all floors in memory

### Alt 2: Procedural Floor Generation
- **Pros**: Infinite floors possible
- **Cons**: Loses hand-crafted design, more complex

### Alt 3: Separate Game Scenes Per Floor
- **Pros**: Complete isolation
- **Cons**: UI/battle duplication, complex state management

**Chosen approach balances flexibility, performance, and ease of use.**

## Future Enhancements

- **Floor-specific music/ambiance**: Use FloorData.BackgroundMusicPath
- **Environmental hazards**: Lava floors, ice floors, etc.
- **Floor-specific mechanics**: Locked doors, keys, switches
- **Save/load floor state**: Track defeated enemies per floor
- **Vertical map view**: Show all floors stacked in mini-map

## Notes

- Existing memories mention static enemy spawns work via EnemySpawn nodes - this is preserved per-floor
- Camera should follow player across floors - handled by Game.cs
- Battle system unchanged - works the same on any floor
- Gold/XP system unchanged - persistent across floors via GameManager singleton

---

**Document Version**: 2.0  
**Created**: 2025-10-18  
**Last Updated**: 2025-10-18  
**Architecture**: Resource-based FloorDefinition (adopted from codex plan)  
**Key Change**: Separated metadata (.tres resources) from layout (.tscn scenes)
