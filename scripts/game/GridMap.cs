using Godot;
using System;

public partial class GridMap : Node2D
{
    [Export] public int GridWidth { get; set; } = 160;
    [Export] public int GridHeight { get; set; } = 160;
    [Export] public int CellSize { get; set; } = 32; // Reduced cell size to fit larger grid
    
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
        
        // Connect to camera movement to trigger redraws when needed
        GetViewport().SizeChanged += () => QueueRedraw();
    }
    
    private void InitializeGrid()
    {
        _grid = new int[GridWidth, GridHeight];
        
        // Create a more complex maze pattern for larger grid
        // Fill with walls first
        for (int x = 0; x < GridWidth; x++)
        {
            for (int y = 0; y < GridHeight; y++)
            {
                _grid[x, y] = (int)CellType.Wall;
            }
        }
        
        // Create main pathways - horizontal and vertical corridors
        for (int x = 1; x < GridWidth - 1; x++)
        {
            // Main horizontal corridors
            _grid[x, GridHeight / 4] = (int)CellType.Empty;
            _grid[x, GridHeight / 2] = (int)CellType.Empty;
            _grid[x, 3 * GridHeight / 4] = (int)CellType.Empty;
        }
        
        for (int y = 1; y < GridHeight - 1; y++)
        {
            // Main vertical corridors
            _grid[GridWidth / 4, y] = (int)CellType.Empty;
            _grid[GridWidth / 2, y] = (int)CellType.Empty;
            _grid[3 * GridWidth / 4, y] = (int)CellType.Empty;
        }
        
        // Create room-like areas with enemies
        CreateRoom(10, 10, 30, 30, 3); // Small room with low-level enemies
        CreateRoom(50, 20, 30, 30, 5); // Medium room with mid-level enemies
        CreateRoom(90, 50, 40, 40, 8); // Large room with high-level enemies
        CreateRoom(20, 80, 35, 35, 4); // Another medium room
        CreateRoom(70, 100, 45, 45, 6); // High-level area
        CreateRoom(120, 120, 30, 30, 10); // Boss area
        
        // Set player starting position
        _playerPosition = new Vector2I(1, GridHeight / 2);
        _grid[_playerPosition.X, _playerPosition.Y] = (int)CellType.Player;
    }
    
    private void CreateRoom(int startX, int startY, int width, int height, int enemyCount)
    {
        // Clear the room area
        for (int x = startX; x < startX + width && x < GridWidth - 1; x++)
        {
            for (int y = startY; y < startY + height && y < GridHeight - 1; y++)
            {
                _grid[x, y] = (int)CellType.Empty;
            }
        }
        
        // Add enemies randomly in the room
        var random = new Random();
        for (int i = 0; i < enemyCount; i++)
        {
            int attempts = 0;
            while (attempts < 50) // Prevent infinite loop
            {
                int x = random.Next(startX + 1, Mathf.Min(startX + width - 1, GridWidth - 1));
                int y = random.Next(startY + 1, Mathf.Min(startY + height - 1, GridHeight - 1));
                
                if (_grid[x, y] == (int)CellType.Empty)
                {
                    _grid[x, y] = (int)CellType.Enemy;
                    break;
                }
                attempts++;
            }
        }
    }
    
    private void DrawGrid()
    {
        // Just queue a redraw - the actual drawing happens in _Draw()
        QueueRedraw();
    }
    
    public override void _Draw()
    {
        // Get camera for viewport culling, with fallback if not available
        Camera2D camera = GetViewport().GetCamera2D();
        Vector2 cameraPos = camera?.GlobalPosition ?? Vector2.Zero;
        Vector2 viewportSize = GetViewportRect().Size;
        float zoom = camera?.Zoom.X ?? 0.5f;
        
        // Calculate visible area with padding
        int padding = 10;
        int startX, endX, startY, endY;
        
        if (camera != null)
        {
            // Use viewport culling for performance
            startX = Mathf.Max(0, (int)((cameraPos.X - viewportSize.X / zoom / 2) / CellSize) - padding);
            endX = Mathf.Min(GridWidth, (int)((cameraPos.X + viewportSize.X / zoom / 2) / CellSize) + padding);
            startY = Mathf.Max(0, (int)((cameraPos.Y - viewportSize.Y / zoom / 2) / CellSize) - padding);
            endY = Mathf.Min(GridHeight, (int)((cameraPos.Y + viewportSize.Y / zoom / 2) / CellSize) + padding);
        }
        else
        {
            // Fallback: draw a smaller area around the player if no camera
            Vector2I playerPos = GetPlayerPosition();
            int range = 20;
            startX = Mathf.Max(0, playerPos.X - range);
            endX = Mathf.Min(GridWidth, playerPos.X + range);
            startY = Mathf.Max(0, playerPos.Y - range);
            endY = Mathf.Min(GridHeight, playerPos.Y + range);
        }
        
        // Draw only visible cells
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                Vector2 cellPos = new Vector2(x * CellSize, y * CellSize);
                Vector2 cellSize = new Vector2(CellSize, CellSize);
                Rect2 cellRect = new Rect2(cellPos, cellSize);
                
                Color cellColor = (CellType)_grid[x, y] switch
                {
                    CellType.Empty => Colors.LightGray,
                    CellType.Wall => Colors.DarkGray,
                    CellType.Enemy => Colors.Red,
                    CellType.Player => Colors.Blue,
                    _ => Colors.White
                };
                
                // Draw cell
                DrawRect(cellRect, cellColor);
                
                // Draw border
                DrawRect(cellRect, Colors.Black, false, 1.0f);
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
        
        QueueRedraw();
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
                QueueRedraw();
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
