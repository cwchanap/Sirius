using Godot;

public partial class PlayerController : Node
{
    private GridMap _gridMap;
    private GameManager _gameManager;
    private FloorManager _floorManager;
    private bool _isProcessingMove = false;
    
    // Stair transition state
    private bool _pendingStairTransition = false;
    private int _targetFloor = -1;
    private bool _isGoingUp = false;
    
    public override void _Ready()
    {
        _gameManager = GameManager.Instance;
        _floorManager = GetNode<FloorManager>("../FloorManager");
        
        // GridMap will be set by Game.cs when floor loads
        GD.Print($"PlayerController ready - GridMap: {_gridMap != null}, GameManager: {_gameManager != null}, FloorManager: {_floorManager != null}");
        if (_gameManager != null)
        {
            GD.Print($"Initial battle state: {_gameManager.IsInBattle}");
        }
    }
    
    /// <summary>
    /// Called by Game.cs when a new floor loads to update the GridMap reference
    /// </summary>
    public void SetGridMap(GridMap gridMap)
    {
        _gridMap = gridMap;
        GD.Print($"PlayerController.SetGridMap: GridMap updated to {gridMap?.Name ?? "null"}");
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
        // Debug output to help track the issue
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            GD.Print($"Input received: {keyEvent.Keycode}, InBattle: {_gameManager.IsInBattle}, ProcessingMove: {_isProcessingMove}");
        }
        
        // Don't handle input during battle or while processing a move
        if (_gameManager.IsInBattle || _isProcessingMove) 
        {
            if (@event is InputEventKey key && key.Pressed)
            {
                GD.Print($"Input blocked - InBattle: {_gameManager.IsInBattle}, ProcessingMove: {_isProcessingMove}");
            }
            return;
        }
        
        if (@event is InputEventKey keyEvent2 && keyEvent2.Pressed)
        {
            Vector2I direction = Vector2I.Zero;
            
            // Handle stair interaction
            if (keyEvent2.Keycode == Key.E && _pendingStairTransition)
            {
                GD.Print($"Taking stairs {(_isGoingUp ? "up" : "down")} to floor {_targetFloor}");
                _floorManager?.TransitionToFloor(_targetFloor, _isGoingUp);
                _pendingStairTransition = false;
                return;
            }
            
            switch (keyEvent2.Keycode)
            {
                case Key.W:
                case Key.Up:
                    direction = Vector2I.Up;
                    break;
                case Key.S:
                case Key.Down:
                    direction = Vector2I.Down;
                    break;
                case Key.A:
                case Key.Left:
                    direction = Vector2I.Left;
                    break;
                case Key.D:
                case Key.Right:
                    direction = Vector2I.Right;
                    break;
                // ESC handling is now in Game.cs to check battle state properly
            }
            
            if (direction != Vector2I.Zero)
            {
                if (_gridMap == null)
                {
                    GD.Print("GridMap not yet loaded, ignoring movement input");
                    return;
                }
                
                GD.Print($"Processing movement: {direction}");
                _isProcessingMove = true;
                bool moveSuccessful = _gridMap.TryMovePlayer(direction);
                GD.Print($"Movement result: {moveSuccessful}");
                
                // After successful move, check for stairs
                if (moveSuccessful)
                {
                    CheckForStairs();
                }
                
                // Reset processing flag after a short delay to prevent rapid inputs
                GetTree().CreateTimer(0.1).Timeout += () => {
                    _isProcessingMove = false;
                    GD.Print("Movement processing flag reset");
                };
            }
        }
    }
    
    private void CheckForStairs()
    {
        if (_gridMap == null || _floorManager == null) return;
        
        Vector2I playerPos = _gridMap.GetPlayerPosition();
        
        // Check if player is standing on a stair tile
        if (_gridMap.IsOnStairs(playerPos))
        {
            // Check which direction and if target floor exists
            if (_floorManager.IsOnStairs(playerPos, out bool isUp, out int targetFloor))
            {
                _pendingStairTransition = true;
                _targetFloor = targetFloor;
                _isGoingUp = isUp;
                GD.Print($"🪜 Standing on stairs! Press E to go {(isUp ? "up" : "down")} to floor {targetFloor}");
            }
        }
        else
        {
            // Clear pending transition if we moved away from stairs
            _pendingStairTransition = false;
        }
    }
}
