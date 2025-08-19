using Godot;

public partial class PlayerController : Node
{
    private GridMap _gridMap;
    private GameManager _gameManager;
    
    public override void _Ready()
    {
        _gridMap = GetNode<GridMap>("../GridMap");
        _gameManager = GameManager.Instance;
        
        if (_gridMap != null)
        {
            _gridMap.EnemyEncountered += OnEnemyEncountered;
        }
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
        // Don't handle input during battle
        if (_gameManager.IsInBattle) return;
        
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
                case Key.Escape:
                    GetTree().ChangeSceneToFile("res://MainMenu.tscn");
                    return;
            }
            
            if (direction != Vector2I.Zero)
            {
                _gridMap.TryMovePlayer(direction);
            }
        }
    }
    
    private void OnEnemyEncountered(Vector2I enemyPosition)
    {
        GD.Print($"Enemy encountered at position: {enemyPosition}");
        
        // Create enemy based on position (simple logic for now)
        Enemy enemy;
        
        // Vary enemies based on position
        if (enemyPosition.X < 5)
        {
            enemy = Enemy.CreateGoblin();
        }
        else if (enemyPosition.Y < 5)
        {
            enemy = Enemy.CreateOrc();
        }
        else
        {
            enemy = Enemy.CreateDragon();
        }
        
        _gameManager.StartBattle(enemy);
    }
}
