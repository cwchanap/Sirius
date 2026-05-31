using Godot;

public partial class PlayerController : Node
{
    [Signal] public delegate void FacingChangedEventHandler(Vector2I facingDirection);

    private GridMap _gridMap;
    private GameManager _gameManager;
    private FloorManager _floorManager;
    private bool _isProcessingMove = false;
    private Vector2I _lastFacingDirection = Vector2I.Down;
    
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

    public Vector2I FacingDirection => _lastFacingDirection;
    
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_gameManager == null) return;

        // Debug output to help track the issue
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            GD.Print($"Input received: {keyEvent.Keycode}, InBattle: {_gameManager.IsInBattle}, ProcessingMove: {_isProcessingMove}");
        }
        
        // Don't handle input during battle, NPC interaction, or world interaction.
        if (_gameManager.IsInBattle || _gameManager.IsInNpcInteraction || _gameManager.IsInWorldInteraction)
        {
            if (@event is InputEventKey key && key.Pressed)
            {
                GD.Print($"Input blocked - InBattle: {_gameManager.IsInBattle}, InNpcInteraction: {_gameManager.IsInNpcInteraction}, InWorldInteraction: {_gameManager.IsInWorldInteraction}, ProcessingMove: {_isProcessingMove}");
            }
            return;
        }
        
        if (@event.IsActionPressed("interact"))
        {
            if (_gridMap != null && _gridMap.TryRequestTreasureBoxOpen(_lastFacingDirection))
            {
                return;
            }

            if (_gridMap != null && _gridMap.TryRequestPuzzleInteraction(_lastFacingDirection))
            {
                return;
            }
            return;
        }

        if (_isProcessingMove)
        {
            if (@event is InputEventKey key && key.Pressed)
            {
                GD.Print($"Input blocked - InBattle: {_gameManager.IsInBattle}, InNpcInteraction: {_gameManager.IsInNpcInteraction}, ProcessingMove: {_isProcessingMove}");
            }
            return;
        }

        if (@event is InputEventKey keyEvent2 && keyEvent2.Pressed)
        {
            Vector2I direction = Vector2I.Zero;
            
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
                if (_lastFacingDirection != direction)
                {
                    _lastFacingDirection = direction;
                    EmitSignal(SignalName.FacingChanged, _lastFacingDirection);
                }

                if (_gridMap == null)
                {
                    GD.Print("GridMap not yet loaded, ignoring movement input");
                    return;
                }
                
                GD.Print($"Processing movement: {direction}");
                _isProcessingMove = true;
                bool moveSuccessful = _gridMap.TryMovePlayer(direction);
                GD.Print($"Movement result: {moveSuccessful}");
                
                // After successful move, transition immediately if standing on stairs.
                if (moveSuccessful)
                {
                    TransitionIfOnStairs();
                }
                
                // Reset processing flag after a short delay to prevent rapid inputs
                GetTree().CreateTimer(0.1).Timeout += () => {
                    _isProcessingMove = false;
                    GD.Print("Movement processing flag reset");
                };
            }
        }
    }
    
    private bool TransitionIfOnStairs()
    {
        if (_gridMap == null || _floorManager == null)
        {
            GD.Print("TransitionIfOnStairs: GridMap or FloorManager is null");
            return false;
        }
        
        Vector2I playerPos = _gridMap.GetPlayerPosition();

        if (!_gridMap.IsOnStairs(playerPos))
        {
            return false;
        }

        if (!_floorManager.IsOnStairs(playerPos, out bool isUp, out int targetFloor, out int stairIndex))
        {
            return false;
        }

        GD.Print($"Taking stairs {(isUp ? "up" : "down")} to floor {targetFloor}");
        _floorManager.TransitionToFloor(targetFloor, isUp, stairIndex);
        return true;
    }
}
