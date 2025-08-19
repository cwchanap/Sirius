using Godot;

public partial class GridMap : Node2D
{
    [Export] public int GridWidth { get; set; } = 10;
    [Export] public int GridHeight { get; set; } = 10;
    [Export] public int CellSize { get; set; } = 64;
    
    private int[,] _grid;
    private Vector2I _playerPosition;
    
    // Grid cell types
    public enum CellType
    {
        Empty = 0,
        Wall = 1,
        Enemy = 2,
        Player = 3
    }
    
    [Signal] public delegate void PlayerMovedEventHandler(Vector2I newPosition);
    [Signal] public delegate void EnemyEncounteredEventHandler(Vector2I enemyPosition);
    
    public override void _Ready()
    {
        InitializeGrid();
        DrawGrid();
    }
    
    private void InitializeGrid()
    {
        _grid = new int[GridWidth, GridHeight];
        
        // Create a simple maze pattern
        // Fill with walls first
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _grid[x, y] = (int)CellType.Wall;
            }
        }
        
        // Create paths (simple cross pattern for now)
        for (int x = 1; x < GridWidth - 1; x++)
        {
            _grid[x, GridHeight / 2] = (int)CellType.Empty;
        }
        
        for (int y = 1; y < GridHeight - 1; y++)
        {
            _grid[GridWidth / 2, y] = (int)CellType.Empty;
        }
        
        // Add some additional paths
        for (int x = 1; x < GridWidth / 2; x++)
        {
            _grid[x, 2] = (int)CellType.Empty;
            _grid[x, GridHeight - 3] = (int)CellType.Empty;
        }
        
        for (int x = GridWidth / 2 + 1; x < GridWidth - 1; x++)
        {
            _grid[x, 2] = (int)CellType.Empty;
            _grid[x, GridHeight - 3] = (int)CellType.Empty;
        }
        
        // Place enemies at specific positions
        _grid[3, GridHeight / 2] = (int)CellType.Enemy;
        _grid[7, GridHeight / 2] = (int)CellType.Enemy;
        _grid[GridWidth / 2, 2] = (int)CellType.Enemy;
        _grid[GridWidth / 2, GridHeight - 3] = (int)CellType.Enemy;
        
        // Set player starting position
        _playerPosition = new Vector2I(1, GridHeight / 2);
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
    }
    
    private void DrawGrid()
    {
        // Clear existing children
        foreach (Node child in GetChildren())
        {
            child.QueueFree();
        }
        
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                var cellRect = new ColorRect();
                cellRect.Size = new Vector2(CellSize, CellSize);
                cellRect.Position = new Vector2(x * CellSize, y * CellSize);
                
                switch ((CellType)_grid[x, y])
                {
                    case CellType.Empty:
                        cellRect.Color = Colors.LightGray;
                        break;
                    case CellType.Wall:
                        cellRect.Color = Colors.DarkGray;
                        break;
                    case CellType.Enemy:
                        cellRect.Color = Colors.Red;
                        break;
                    case CellType.Player:
                        cellRect.Color = Colors.Blue;
                        break;
                }
                
                AddChild(cellRect);
                
                // Add border
                var border = new Line2D();
                border.AddPoint(Vector2.Zero);
                border.AddPoint(new Vector2(CellSize, 0));
                border.AddPoint(new Vector2(CellSize, CellSize));
                border.AddPoint(new Vector2(0, CellSize));
                border.AddPoint(Vector2.Zero);
                border.DefaultColor = Colors.Black;
                border.Width = 1;
                border.Position = new Vector2(x * CellSize, y * CellSize);
                AddChild(border);
            }
        }
    }
    
    public bool TryMovePlayer(Vector2I direction)
    {
        Vector2I newPosition = _playerPosition + direction;
        
        // Check bounds
        if (newPosition.X < 0 || newPosition.X >= GridWidth || 
            newPosition.Y < 0 || newPosition.Y >= GridHeight)
        {
            return false;
        }
        
        CellType targetCell = (CellType)_grid[newPosition.X, newPosition.Y];
        
        // Check if movement is valid
        if (targetCell == CellType.Wall)
        {
            return false;
        }
        
        // Handle enemy encounter
        if (targetCell == CellType.Enemy)
        {
            EmitSignal(SignalName.EnemyEncountered, newPosition);
            return false; // Don't move onto enemy, handle in battle
        }
        
        // Move player
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Empty;
        _playerPosition = newPosition;
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
        
        DrawGrid();
        EmitSignal(SignalName.PlayerMoved, newPosition);
        return true;
    }
    
    public void RemoveEnemy(Vector2I position)
    {
        if (position.X >= 0 && position.X < GridWidth && 
            position.Y >= 0 && position.Y < GridHeight)
        {
            if (_grid[position.X, position.Y] == (int)CellType.Enemy)
            {
                _grid[position.X, position.Y] = (int)CellType.Empty;
                DrawGrid();
            }
        }
    }
    
    public Vector2I GetPlayerPosition()
    {
        return _playerPosition;
    }
    
    public Vector2 GetWorldPosition(Vector2I gridPosition)
    {
        return new Vector2(gridPosition.X * CellSize + CellSize / 2, 
                          gridPosition.Y * CellSize + CellSize / 2);
    }
}
