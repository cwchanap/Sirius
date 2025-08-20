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
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
        // Don't handle input during battle or while processing a move
        if (_gameManager.IsInBattle || _isProcessingMove) return;
        
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            Vector2I direction = Vector2I.Zero;
            
            switch (keyEvent.Keycode)
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
                _isProcessingMove = true;
                bool moveSuccessful = _gridMap.TryMovePlayer(direction);
                
                // Reset processing flag after a short delay to prevent rapid inputs
                GetTree().CreateTimer(0.1).Timeout += () => {
                    _isProcessingMove = false;
                };
            }
        }
    }
}
