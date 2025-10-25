using Godot;
using Godot.Collections;

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
    
    // Destination positions for each stair (optional, uses default if empty)
    [Export] public Godot.Collections.Array<Vector2I> StairsUpDestinations { get; set; } = new();
    [Export] public Godot.Collections.Array<Vector2I> StairsDownDestinations { get; set; } = new();
    
    // Visual/audio theming (optional)
    [Export] public AudioStream BackgroundMusic { get; set; }
    [Export] public Color AmbientTint { get; set; } = new Color(1, 1, 1, 1);
    [Export] public string FloorDescription { get; set; } = "";
    
    /// <summary>
    /// Check if the given position has stairs and return the stair index
    /// </summary>
    public bool HasStairAt(Vector2I position, out bool isUp, out int stairIndex)
    {
        isUp = false;
        stairIndex = -1;
        
        int upIndex = StairsUp.IndexOf(position);
        if (upIndex >= 0)
        {
            isUp = true;
            stairIndex = upIndex;
            return true;
        }
        
        int downIndex = StairsDown.IndexOf(position);
        if (downIndex >= 0)
        {
            isUp = false;
            stairIndex = downIndex;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Legacy method for backward compatibility
    /// </summary>
    public bool HasStairAt(Vector2I position, out bool isUp)
    {
        return HasStairAt(position, out isUp, out _);
    }
    
    /// <summary>
    /// Get the destination spawn position for stairs going in the specified direction
    /// </summary>
    public Vector2I GetStairDestination(bool goingUp, int stairIndex = 0)
    {
        // If going up, we arrived via StairsDown (check for custom destination)
        // If going down, we arrived via StairsUp (check for custom destination)
        var targetStairs = goingUp ? StairsDown : StairsUp;
        var targetDestinations = goingUp ? StairsDownDestinations : StairsUpDestinations;
        
        // First check if there's a custom destination for this stair
        if (targetDestinations.Count > stairIndex)
        {
            return targetDestinations[stairIndex];
        }
        
        // Fall back to the stair position itself
        if (targetStairs.Count > stairIndex)
        {
            return targetStairs[stairIndex];
        }
        
        // Final fallback to default spawn
        return PlayerStartPosition;
    }
}
