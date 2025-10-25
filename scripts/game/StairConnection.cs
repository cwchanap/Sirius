using Godot;

/// <summary>
/// Visual stair connection node that can be placed in scenes to define floor transitions.
/// Place this as a child of GridMap at the stair tile position.
/// </summary>
[GlobalClass]
public partial class StairConnection : Node2D
{
    /// <summary>
    /// Grid position of this stair (will be auto-calculated from world position)
    /// </summary>
    [Export] public Vector2I GridPosition { get; set; }
    
    /// <summary>
    /// Direction of this stair
    /// </summary>
    [Export] public StairDirection Direction { get; set; } = StairDirection.Up;
    
    /// <summary>
    /// Target floor number (0 = Ground Floor, 1 = First Floor, etc.)
    /// </summary>
    [Export] public int TargetFloor { get; set; } = 1;
    
    /// <summary>
    /// Link to another StairConnection node (can be in a different scene/floor)
    /// When set, player will spawn at the linked stair's position
    /// </summary>
    [Export] public NodePath LinkedStairPath { get; set; } = "";
    
    /// <summary>
    /// Optional: Override with specific destination position
    /// Only used if UseCustomDestination is true
    /// </summary>
    [Export] public bool UseCustomDestination { get; set; } = false;
    
    /// <summary>
    /// Custom destination position (only used if UseCustomDestination is true)
    /// </summary>
    [Export] public Vector2I CustomDestination { get; set; } = Vector2I.Zero;
    
    /// <summary>
    /// Unique identifier for this stair (used for cross-scene linking)
    /// </summary>
    [Export] public string StairId { get; set; } = "";
    
    /// <summary>
    /// ID of the destination stair on the target floor
    /// When player uses this stair, they spawn at the stair with this ID
    /// Example: "1f_stair_a" or "gf_main_entrance"
    /// </summary>
    [Export] public string DestinationStairId { get; set; } = "";
    
    /// <summary>
    /// Visual indicator color in editor
    /// </summary>
    [Export] public Color IndicatorColor { get; set; } = new Color(0, 1, 1, 0.5f);
    
    public override void _Ready()
    {
        // Auto-calculate grid position from world position if not set
        if (GridPosition == Vector2I.Zero && GetParent() is Node2D parent)
        {
            // Assuming 32 pixel grid with 0.333 scale
            Vector2 worldPos = GlobalPosition;
            GridPosition = new Vector2I(
                Mathf.RoundToInt(worldPos.X / 32.0f),
                Mathf.RoundToInt(worldPos.Y / 32.0f)
            );
            GD.Print($"🪜 StairConnection auto-calculated GridPosition: {GridPosition} from world pos {worldPos}");
        }
    }
    
    public override void _Draw()
    {
        // Draw visual indicator in editor
        if (Engine.IsEditorHint())
        {
            // Draw arrow pointing in direction
            Vector2 arrowSize = new Vector2(20, 20);
            Color color = IndicatorColor;
            
            // Draw circle
            DrawCircle(Vector2.Zero, 15, color);
            
            // Draw arrow based on direction
            Vector2 arrowDir = Direction == StairDirection.Up ? Vector2.Up : Vector2.Down;
            DrawLine(Vector2.Zero, arrowDir * 20, color, 3);
            
            // Draw arrow head
            Vector2 perpendicular = new Vector2(-arrowDir.Y, arrowDir.X);
            DrawLine(arrowDir * 20, arrowDir * 15 + perpendicular * 5, color, 3);
            DrawLine(arrowDir * 20, arrowDir * 15 - perpendicular * 5, color, 3);
            
            // Draw label
            var font = ThemeDB.FallbackFont;
            string label = $"{Direction}\n→ Floor {TargetFloor}";
            if (!string.IsNullOrEmpty(DestinationStairId))
            {
                label += $"\n🎯 {DestinationStairId}";
            }
            DrawString(font, new Vector2(-40, 30), label, HorizontalAlignment.Center, -1, 10, color);
        }
    }
}

public enum StairDirection
{
    Up,
    Down
}
