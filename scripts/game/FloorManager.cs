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
    
    // Signals for floor transitions
    [Signal] public delegate void FloorChangedEventHandler(int oldFloorIndex, int newFloorIndex);
    [Signal] public delegate void FloorLoadedEventHandler(FloorDefinition floorDef, GridMap gridMap);
    
    [Export] public bool EnableDebugLogging { get; set; } = true;
    
    public int CurrentFloorIndex => _currentFloorIndex;
    public FloorDefinition CurrentFloorDefinition => Floors.Count > _currentFloorIndex ? Floors[_currentFloorIndex] : null;
    public GridMap CurrentGridMap => _currentGridMap;
    
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
