using Godot;

public partial class PlayerController : Node
{
    private GridMap _gridMap;
    private GameManager _gameManager;
    private bool _isProcessingMove = false;
    
    public override void _Ready()
    {
        _gridMap = GetNode<GridMap>("../GridMap");
        _gameManager = GameManager.Instance;
        
        // Debug output to ensure components are properly initialized
        GD.Print($"PlayerController ready - GridMap: {_gridMap != null}, GameManager: {_gameManager != null}");
        if (_gameManager != null)
        {
            GD.Print($"Initial battle state: {_gameManager.IsInBattle}");
        }
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
                GD.Print($"Processing movement: {direction}");
                _isProcessingMove = true;
                bool moveSuccessful = _gridMap.TryMovePlayer(direction);
                GD.Print($"Movement result: {moveSuccessful}");
                
                // Reset processing flag after a short delay to prevent rapid inputs
                GetTree().CreateTimer(0.1).Timeout += () => {
                    _isProcessingMove = false;
                    GD.Print("Movement processing flag reset");
                };
            }
        }
    }
}
