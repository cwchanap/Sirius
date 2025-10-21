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
