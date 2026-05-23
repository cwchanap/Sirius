using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class FloorManager : Node
{
    // Array of floor definitions (set in editor)
    [Export] public Godot.Collections.Array<FloorDefinition> Floors { get; set; } = new();
    
    // Current state
    private int _currentFloorIndex = 0;
    private Node2D _currentFloorInstance;
    private GridMap _currentGridMap;
    
    // Stair connection registry (StairId -> StairConnection)
    private Dictionary<string, StairConnection> _stairRegistry = new();
    
    // Signals for floor transitions
    [Signal] public delegate void FloorChangedEventHandler(int oldFloorIndex, int newFloorIndex);
    [Signal] public delegate void FloorLoadedEventHandler(FloorDefinition floorDef, GridMap gridMap);
    
    [Export] public bool EnableDebugLogging { get; set; } = true;
    
    public int CurrentFloorIndex => _currentFloorIndex;
    public FloorDefinition CurrentFloorDefinition => Floors.Count > _currentFloorIndex ? Floors[_currentFloorIndex] : null;
    public GridMap CurrentGridMap => _currentGridMap;

    /// <summary>
    /// Set to true by the parent (Game) before _Ready runs to skip the default
    /// initial floor load. _Ready also checks PendingLoadData as a fallback
    /// safety net in case the flag was not set.
    /// </summary>
    public bool SkipInitialFloorLoad { get; set; }
    
    public override void _Ready()
    {
        GD.Print($"🏢 FloorManager._Ready() called");
        GD.Print($"   EnableDebugLogging: {EnableDebugLogging}");
        GD.Print($"   Floors.Count: {Floors.Count}");
        
        if (Floors.Count == 0)
        {
            GD.PushError("FloorManager has no floors defined! Add FloorDefinition resources.");
            return;
        }
        
        for (int i = 0; i < Floors.Count; i++)
        {
            var floor = Floors[i];
            GD.Print($"   Floor[{i}]: {floor?.FloorName ?? "null"}, Scene: {floor?.FloorScene?.ResourcePath ?? "null"}");
        }
        
        if (SaveManager.Instance?.PendingLoadData != null)
        {
            GD.Print("🏢 Pending save detected; skipping initial floor load.");
            return;
        }
        else if (SkipInitialFloorLoad)
        {
            GD.Print("🏢 SkipInitialFloorLoad set; skipping initial floor load.");
            return;
        }

        GD.Print($"🏢 Loading initial floor (index 0)...");
        // Load initial floor (index 0)
        LoadFloor(0);
    }
    
    /// <summary>
    /// Load a specific floor by index
    /// </summary>
    public bool LoadFloor(int floorIndex, Vector2I? playerSpawnOverride = null)
    {
        GD.Print($"🏢 LoadFloor called: floorIndex={floorIndex}, playerSpawnOverride={playerSpawnOverride}");
        
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
        
        if (EnableDebugLogging)
            GD.Print($"✓ Floor scene instantiated: {floorInstance.Name}");
        
        // Add to scene tree under Game node's GridMap parent position
        var game = GetParent();
        
        // Ensure floor is visible and positioned at origin
        floorInstance.Position = Vector2.Zero;
        floorInstance.Visible = true;
        
        // Use CallDeferred to avoid "Parent node is busy" error
        game.CallDeferred("add_child", floorInstance);
        _currentFloorInstance = floorInstance;
        
        if (EnableDebugLogging)
            GD.Print($"✓ Floor instance added to scene tree at position {floorInstance.Position}, visible: {floorInstance.Visible}");
        
        // Store spawn override for deferred finalization
        Vector2I spawnPos = playerSpawnOverride ?? floorDef.PlayerStartPosition;
        
        if (EnableDebugLogging)
            GD.Print($"📍 Player spawn position: {spawnPos}");
        
        // Wait for floor to be ready, then finalize
        if (!floorInstance.IsNodeReady())
        {
            if (EnableDebugLogging)
                GD.Print($"⏳ Floor not ready yet, deferring finalization...");
            CallDeferred(nameof(FinalizeFloorLoad), floorIndex, floorDef, spawnPos);
        }
        else
        {
            if (EnableDebugLogging)
                GD.Print($"✓ Floor ready, finalizing immediately...");
            FinalizeFloorLoad(floorIndex, floorDef, spawnPos);
        }
        
        return true;
    }
    
    private void FinalizeFloorLoad(int floorIndex, FloorDefinition floorDef, Vector2I playerSpawnPos)
    {
        if (EnableDebugLogging)
            GD.Print($"🔧 FinalizeFloorLoad called for floor {floorIndex}");
        
        int oldFloorIndex = _currentFloorIndex;
        _currentFloorIndex = floorIndex;
        
        if (_currentFloorInstance == null)
        {
            GD.PushError("FinalizeFloorLoad called but _currentFloorInstance is null!");
            return;
        }
        
        if (EnableDebugLogging)
            GD.Print($"🔍 Looking for GridMap in floor instance '{_currentFloorInstance.Name}'...");
        
        // Find GridMap by type (not by name, since Godot may rename it during instantiation)
        foreach (var child in _currentFloorInstance.GetChildren())
        {
            if (child is GridMap gridMap)
            {
                _currentGridMap = gridMap;
                break;
            }
        }
        
        if (_currentGridMap == null)
        {
            GD.PushError($"Floor '{floorDef.FloorName}' scene has no GridMap child!");
            GD.Print($"Available children: {string.Join(", ", _currentFloorInstance.GetChildren().Select(c => c.Name + " (" + c.GetType().Name + ")"))}");
            return;
        }
        
        // Ensure GridMap and all its children are visible
        _currentGridMap.Visible = true;
        _currentGridMap.Show();
        
        if (EnableDebugLogging)
            GD.Print($"✓ Found GridMap, calling LoadFloor with CallDeferred...");
        
        // Use CallDeferred to ensure the entire scene tree is ready
        CallDeferred(nameof(DeferredLoadFloor), floorDef, playerSpawnPos);
        
        if (EnableDebugLogging)
            GD.Print($"✅ Floor {floorIndex} loaded: {floorDef.FloorName}");
        
        EmitSignal(SignalName.FloorChanged, oldFloorIndex, floorIndex);
        EmitSignal(SignalName.FloorLoaded, floorDef, _currentGridMap);
    }
    
    private void DeferredLoadFloor(FloorDefinition floorDef, Vector2I playerSpawnPos)
    {
        if (EnableDebugLogging)
            GD.Print($"✅ Deferred floor load executing, GridMap in tree: {_currentGridMap.IsInsideTree()}, ready: {_currentGridMap.IsNodeReady()}");
        
        _currentGridMap.LoadFloor(_currentFloorInstance, floorDef, playerSpawnPos);
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
            
            // Remove stair entries belonging to the departing floor so stale
            // nodes don't pollute position-based lookups on future floors.
            // Also remove orphaned entries (freed/null nodes).
            var staleKeys = new List<string>();
            foreach (var kvp in _stairRegistry)
            {
                if (kvp.Value == null || !GodotObject.IsInstanceValid(kvp.Value))
                {
                    staleKeys.Add(kvp.Key);
                }
                else if (kvp.Value.GetParent() == _currentGridMap)
                {
                    staleKeys.Add(kvp.Key);
                }
            }
            foreach (var key in staleKeys)
            {
                _stairRegistry.Remove(key);
            }
            if (staleKeys.Count > 0 && EnableDebugLogging)
                GD.Print($"🗑️ Removed {staleKeys.Count} stale stair entries");
            
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
        
        // Try to find the source stair to check for DestinationStairId
        Vector2I spawnPos = Vector2I.Zero;
        bool foundDestination = false;
        
        // Get the stair position from current floor
        var currentFloor = CurrentFloorDefinition;
        if (currentFloor != null && _currentGridMap != null)
        {
            var playerPos = _currentGridMap.GetPlayerPosition();
            
            // Find which stair the player is on
            var stairPositions = isGoingUp ? currentFloor.StairsUp : currentFloor.StairsDown;
            int currentStairIndex = stairPositions.IndexOf(playerPos);
            
            if (currentStairIndex >= 0)
            {
                // Search only the current floor's StairConnection nodes to avoid
                // matching a stale entry from another floor that happens to share
                // the same GridPosition (e.g. 3F_2F_A and 2F_1F_A both at (10,10)).
                foreach (var child in _currentGridMap.GetChildren())
                {
                    if (child is StairConnection stair
                        && stair.GridPosition == playerPos
                        && !string.IsNullOrEmpty(stair.DestinationStairId))
                    {
                        var destStair = GetStairById(stair.DestinationStairId);
                        if (destStair != null)
                        {
                            spawnPos = destStair.GridPosition;
                            foundDestination = true;
                            if (EnableDebugLogging)
                                GD.Print($"🎯 Using DestinationStairId '{stair.DestinationStairId}' → spawn at {spawnPos}");
                            break;
                        }
                        else if (EnableDebugLogging)
                        {
                            GD.Print($"⚠️ Destination stair '{stair.DestinationStairId}' not found in registry!");
                        }
                    }
                }
            }
        }
        
        // Fallback to old method if no DestinationStairId was found
        if (!foundDestination)
        {
            GD.PushWarning($"TransitionToFloor: no DestinationStairId match for floor {targetFloorIndex}, using legacy GetStairDestination");
            var targetFloor = Floors[targetFloorIndex];
            spawnPos = targetFloor.GetStairDestination(isGoingUp, stairIndex);
        }
        
        LoadFloor(targetFloorIndex, spawnPos);
    }
    
    /// <summary>
    /// Check if player is on a stair tile
    /// </summary>
    public bool IsOnStairs(Vector2I position, out bool isUp, out int targetFloorIndex, out int stairIndex)
    {
        isUp = false;
        targetFloorIndex = -1;
        stairIndex = -1;
        
        var currentDef = CurrentFloorDefinition;
        if (currentDef == null)
            return false;
        
        if (currentDef.HasStairAt(position, out isUp, out stairIndex))
        {
            targetFloorIndex = _currentFloorIndex + (isUp ? 1 : -1);
            return targetFloorIndex >= 0 && targetFloorIndex < Floors.Count;
        }
        
        return false;
    }
    
    /// <summary>
    /// Legacy method for backward compatibility
    /// </summary>
    public bool IsOnStairs(Vector2I position, out bool isUp, out int targetFloorIndex)
    {
        return IsOnStairs(position, out isUp, out targetFloorIndex, out _);
    }
    
    /// <summary>
    /// Get total number of floors available
    /// </summary>
    public int GetFloorCount() => Floors.Count;
    
    /// <summary>
    /// Register a StairConnection in the global registry
    /// </summary>
    public void RegisterStair(string stairId, StairConnection stair)
    {
        if (string.IsNullOrEmpty(stairId))
        {
            GD.PushWarning("RegisterStair called with empty stairId — stair ignored.");
            return;
        }
        
        if (_stairRegistry.ContainsKey(stairId))
        {
            GD.PushWarning($"RegisterStair: overwriting existing stair '{stairId}'");
        }
        
        _stairRegistry[stairId] = stair;
        if (EnableDebugLogging)
            GD.Print($"📝 Registered stair '{stairId}' at {stair.GridPosition}");
    }
    
    /// <summary>
    /// Get a registered StairConnection by ID
    /// </summary>
    public StairConnection GetStairById(string stairId)
    {
        return _stairRegistry.GetValueOrDefault(stairId);
    }
    
    /// <summary>
    /// Get floor definition by index
    /// </summary>
    public FloorDefinition GetFloorByIndex(int index)
    {
        return (index >= 0 && index < Floors.Count) ? Floors[index] : null;
    }
    
    /// <summary>
    /// Find a stair on a specific floor with a specific direction
    /// </summary>
    public StairConnection FindStairOnFloor(int floorIndex, StairDirection direction)
    {
        // Search through all registered stairs
        foreach (var kvp in _stairRegistry)
        {
            var stair = kvp.Value;
            // Check if this stair belongs to the target floor and has the right direction
            // We need to check the stair's parent floor, which we can infer from the registry
            if (stair.Direction == direction && stair.TargetFloor != floorIndex)
            {
                // This is a stair FROM the target floor (going away from it)
                // We want stairs that lead TO somewhere else from our target floor
                continue;
            }
        }
        
        // Fallback: return any stair with matching direction that targets a different floor
        GD.PushWarning($"FindStairOnFloor: no exact match for floor {floorIndex} direction {direction}, falling back to any-floor match");
        foreach (var kvp in _stairRegistry)
        {
            var stair = kvp.Value;
            if (stair.Direction == direction)
            {
                return stair;
            }
        }
        
        return null;
    }
}
