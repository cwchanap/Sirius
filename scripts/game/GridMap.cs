using Godot;
using System;
using System.Collections.Generic;

public partial class GridMap : Node2D
{
    [Export] public int GridWidth { get; set; } = 160;
    [Export] public int GridHeight { get; set; } = 160;
    [Export] public int CellSize { get; set; } = 32; // Reduced cell size to fit larger grid
    [Export] public bool UseSprites { get; set; } = true; // Toggle for sprite rendering
    
    private int[,] _grid;
    private Vector2I _playerPosition;
    
    // Sprite dictionaries for different asset types
    private Dictionary<CellType, Texture2D> _cellSprites = new();
    private Dictionary<string, Texture2D> _enemySprites = new();
    private Dictionary<string, Texture2D> _terrainSprites = new();
    
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
        // Load sprites first
        LoadSprites();
        
        InitializeGrid();
        DrawGrid();
        
        // Connect to camera movement to trigger redraws when needed
        GetViewport().SizeChanged += () => QueueRedraw();
    }
    
    private void LoadSprites()
    {
        try
        {
            // Load character sprites (use sprite sheets)
            var playerTexture = GD.Load<Texture2D>("res://assets/sprites/characters/player_hero/sprite_sheet.png");
            if (playerTexture != null)
                _cellSprites[CellType.Player] = playerTexture;

            // Load basic terrain sprites
            var floorTexture = GD.Load<Texture2D>("res://assets/sprites/terrain/floor_starting_area.png");
            if (floorTexture != null)
                _terrainSprites["starting"] = floorTexture;
                
            var wallTexture = GD.Load<Texture2D>("res://assets/sprites/terrain/wall_generic.png");
            if (wallTexture != null)
                _cellSprites[CellType.Wall] = wallTexture;

            // Load enemy sprites
            LoadEnemySprites();
            
            // Load themed terrain
            LoadThemedTerrain();
            
            GD.Print("Sprites loaded successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error loading sprites: {ex.Message}");
            UseSprites = false; // Fallback to colored rectangles
        }
    }

    private void LoadEnemySprites()
    {
        var enemyNames = new string[]
        {
            "enemy_goblin",
            "enemy_orc", 
            "enemy_skeleton_warrior",
            "enemy_troll",
            "enemy_dragon",
            "enemy_forest_spirit",
            "enemy_cave_spider",
            "enemy_desert_scorpion",
            "enemy_swamp_wretch",
            "enemy_mountain_wyvern",
            "enemy_dark_mage",
            "enemy_dungeon_guardian",
            "enemy_demon_lord",
            "enemy_ancient_dragon_king"
        };
        
        foreach (var enemyName in enemyNames)
        {
            var texture = GD.Load<Texture2D>($"res://assets/sprites/characters/{enemyName}/sprite_sheet.png");
            if (texture != null)
            {
                var enemyType = enemyName.Replace("enemy_", "");
                _enemySprites[enemyType] = texture;
            }
        }
    }

    private void LoadThemedTerrain()
    {
        var terrainTypes = new string[]
        {
            "floor_forest.png",
            "floor_cave.png", 
            "floor_desert.png",
            "floor_swamp.png",
            "floor_mountain.png",
            "floor_dungeon.png",
            "floor_boss_arena.png"
        };
        
        foreach (var filename in terrainTypes)
        {
            var texture = GD.Load<Texture2D>($"res://assets/sprites/terrain/{filename}");
            if (texture != null)
            {
                var terrainType = filename.Replace("floor_", "").Replace(".png", "");
                _terrainSprites[terrainType] = texture;
            }
        }
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
        
        // Generate a complex maze structure
        GenerateComplexMaze();
        
        // Create themed areas with different enemy types and densities
        CreateStartingArea();
        CreateForestZone();
        CreateCaveSystem();
        CreateDesertArea();
        CreateSwampLands();
        CreateMountainPeak();
        CreateDungeonComplex();
        CreateBossArena();
        
        // Add additional scattered enemies in corridors
        AddCorridorEnemies();
        
        // Set player starting position
        _playerPosition = new Vector2I(5, GridHeight / 2);
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
    
    private void GenerateComplexMaze()
    {
        // Create a main pathway system with multiple routes
        CreateMainPathways();
        
        // Add winding corridors
        CreateWindingPaths();
        
        // Create interconnected chambers
        CreateInterconnectedChambers();
        
        // Add secret passages
        CreateSecretPassages();
    }
    
    private void CreateMainPathways()
    {
        // Primary horizontal corridor
        for (int x = 1; x < GridWidth - 1; x++)
        {
            _grid[x, GridHeight / 2] = (int)CellType.Empty;
        }
        
        // Primary vertical corridor
        for (int y = 1; y < GridHeight - 1; y++)
        {
            _grid[GridWidth / 2, y] = (int)CellType.Empty;
        }
        
        // Secondary pathways at quarters
        for (int x = 1; x < GridWidth - 1; x++)
        {
            _grid[x, GridHeight / 4] = (int)CellType.Empty;
            _grid[x, 3 * GridHeight / 4] = (int)CellType.Empty;
        }
        
        for (int y = 1; y < GridHeight - 1; y++)
        {
            _grid[GridWidth / 4, y] = (int)CellType.Empty;
            _grid[3 * GridWidth / 4, y] = (int)CellType.Empty;
        }
    }
    
    private void CreateWindingPaths()
    {
        var random = new Random();
        
        // Create winding paths connecting different areas
        for (int i = 0; i < 10; i++)
        {
            int startX = random.Next(10, GridWidth - 10);
            int startY = random.Next(10, GridHeight - 10);
            int endX = random.Next(10, GridWidth - 10);
            int endY = random.Next(10, GridHeight - 10);
            
            CreateWindingPath(startX, startY, endX, endY);
        }
    }
    
    private void CreateWindingPath(int startX, int startY, int endX, int endY)
    {
        int currentX = startX;
        int currentY = startY;
        var random = new Random();
        
        while (currentX != endX || currentY != endY)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            
            // Randomly choose direction, biased toward target
            if (random.NextDouble() < 0.7) // 70% chance to move toward target
            {
                if (currentX < endX) currentX++;
                else if (currentX > endX) currentX--;
                else if (currentY < endY) currentY++;
                else if (currentY > endY) currentY--;
            }
            else // 30% chance for random movement
            {
                int direction = random.Next(4);
                switch (direction)
                {
                    case 0: if (currentX > 1) currentX--; break;
                    case 1: if (currentX < GridWidth - 2) currentX++; break;
                    case 2: if (currentY > 1) currentY--; break;
                    case 3: if (currentY < GridHeight - 2) currentY++; break;
                }
            }
        }
    }
    
    private void CreateInterconnectedChambers()
    {
        // Create larger chamber areas connected by corridors
        var chambers = new (int x, int y, int w, int h)[]
        {
            (20, 20, 15, 15),
            (50, 30, 20, 18),
            (90, 25, 25, 20),
            (25, 60, 18, 22),
            (70, 70, 30, 25),
            (110, 80, 20, 15),
            (30, 110, 25, 20),
            (80, 120, 35, 25),
            (130, 130, 20, 20)
        };
        
        foreach (var (x, y, w, h) in chambers)
        {
            CreateChamber(x, y, w, h);
        }
    }
    
    private void CreateChamber(int startX, int startY, int width, int height)
    {
        for (int x = startX; x < startX + width && x < GridWidth - 1; x++)
        {
            for (int y = startY; y < startY + height && y < GridHeight - 1; y++)
            {
                _grid[x, y] = (int)CellType.Empty;
            }
        }
    }
    
    private void CreateSecretPassages()
    {
        var random = new Random();
        
        // Add some hidden shortcuts
        for (int i = 0; i < 5; i++)
        {
            int x1 = random.Next(20, GridWidth - 20);
            int y1 = random.Next(20, GridHeight - 20);
            int x2 = random.Next(20, GridWidth - 20);
            int y2 = random.Next(20, GridHeight - 20);
            
            // Create a narrow secret passage
            CreateStraightPath(x1, y1, x2, y2);
        }
    }
    
    private void CreateStraightPath(int startX, int startY, int endX, int endY)
    {
        // Create straight line paths for secret passages
        int dx = Math.Sign(endX - startX);
        int dy = Math.Sign(endY - startY);
        
        int currentX = startX;
        int currentY = startY;
        
        while (currentX != endX)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            currentX += dx;
        }
        
        while (currentY != endY)
        {
            _grid[currentX, currentY] = (int)CellType.Empty;
            currentY += dy;
        }
    }
    
    private void CreateStartingArea()
    {
        // Safe starting zone with weak enemies
        CreateThematicArea(5, GridHeight / 2 - 10, 30, 20, 3, "starting");
    }
    
    private void CreateForestZone()
    {
        // Forest area with goblins and orcs
        CreateThematicArea(40, 15, 35, 30, 12, "forest");
        CreateThematicArea(45, 50, 25, 25, 8, "forest");
    }
    
    private void CreateCaveSystem()
    {
        // Underground cave system with skeleton warriors
        CreateThematicArea(20, 90, 40, 35, 15, "cave");
        CreateThematicArea(70, 95, 30, 30, 10, "cave");
    }
    
    private void CreateDesertArea()
    {
        // Desert region with varied enemies
        CreateThematicArea(90, 40, 45, 40, 18, "desert");
    }
    
    private void CreateSwampLands()
    {
        // Swamp area with trolls and dark creatures
        CreateThematicArea(25, 130, 35, 25, 14, "swamp");
        CreateThematicArea(70, 135, 25, 20, 10, "swamp");
    }
    
    private void CreateMountainPeak()
    {
        // High-altitude area with dragons and powerful enemies
        CreateThematicArea(110, 15, 40, 35, 16, "mountain");
    }
    
    private void CreateDungeonComplex()
    {
        // Multi-level dungeon with various high-level enemies
        CreateThematicArea(120, 90, 35, 40, 20, "dungeon");
    }
    
    private void CreateBossArena()
    {
        // Final boss area
        CreateThematicArea(140, 140, 15, 15, 3, "boss");
    }
    
    private void CreateThematicArea(int startX, int startY, int width, int height, int enemyCount, string theme)
    {
        // Clear the area
        for (int x = startX; x < startX + width && x < GridWidth - 1; x++)
        {
            for (int y = startY; y < startY + height && y < GridHeight - 1; y++)
            {
                if (x > 0 && y > 0)
                    _grid[x, y] = (int)CellType.Empty;
            }
        }
        
        // Add enemies based on theme
        var random = new Random();
        for (int i = 0; i < enemyCount; i++)
        {
            int attempts = 0;
            while (attempts < 100)
            {
                int x = random.Next(startX + 1, Math.Min(startX + width - 1, GridWidth - 1));
                int y = random.Next(startY + 1, Math.Min(startY + height - 1, GridHeight - 1));
                
                if (x > 0 && y > 0 && x < GridWidth && y < GridHeight && _grid[x, y] == (int)CellType.Empty)
                {
                    _grid[x, y] = (int)CellType.Enemy;
                    break;
                }
                attempts++;
            }
        }
    }
    
    private void AddCorridorEnemies()
    {
        var random = new Random();
        int corridorEnemies = 30; // Add scattered enemies in corridors
        
        for (int i = 0; i < corridorEnemies; i++)
        {
            int attempts = 0;
            while (attempts < 200)
            {
                int x = random.Next(1, GridWidth - 1);
                int y = random.Next(1, GridHeight - 1);
                
                if (_grid[x, y] == (int)CellType.Empty)
                {
                    // Only place in corridors (areas with limited adjacent empty spaces)
                    int emptyNeighbors = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < GridWidth && ny >= 0 && ny < GridHeight &&
                                _grid[nx, ny] == (int)CellType.Empty)
                            {
                                emptyNeighbors++;
                            }
                        }
                    }
                    
                    // Place enemy in narrow corridors (2-4 empty neighbors)
                    if (emptyNeighbors >= 2 && emptyNeighbors <= 4)
                    {
                        _grid[x, y] = (int)CellType.Enemy;
                        break;
                    }
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
                CellType cellType = (CellType)_grid[x, y];
                
                if (UseSprites)
                {
                    DrawCellWithSprite(cellPos, x, y, cellType);
                }
                else
                {
                    DrawCellWithColor(cellPos, x, y, cellType);
                }
            }
        }
    }

    private void DrawCellWithSprite(Vector2 cellPos, int x, int y, CellType cellType)
    {
        // Draw terrain background first
        DrawTerrainSprite(cellPos, x, y);
        
        // Draw entity sprite on top
        if (cellType == CellType.Player && _cellSprites.ContainsKey(CellType.Player))
        {
            DrawTexture(_cellSprites[CellType.Player], cellPos);
        }
        else if (cellType == CellType.Enemy)
        {
            DrawEnemySprite(cellPos, x, y);
        }
        else if (cellType == CellType.Wall && _cellSprites.ContainsKey(CellType.Wall))
        {
            DrawTexture(_cellSprites[CellType.Wall], cellPos);
        }
    }

    private void DrawTerrainSprite(Vector2 cellPos, int x, int y)
    {
        string terrainType = GetTerrainType(x, y);
        
        if (_terrainSprites.ContainsKey(terrainType))
        {
            DrawTexture(_terrainSprites[terrainType], cellPos);
        }
        else if (_terrainSprites.ContainsKey("starting"))
        {
            // Fallback to starting area texture
            DrawTexture(_terrainSprites["starting"], cellPos);
        }
        else
        {
            // Fallback to colored rectangle
            Vector2 cellSize = new Vector2(CellSize, CellSize);
            DrawRect(new Rect2(cellPos, cellSize), GetAreaColor(x, y));
        }
    }

    private void DrawEnemySprite(Vector2 cellPos, int x, int y)
    {
        string enemyType = GetEnemyTypeForPosition(x, y);
        
        if (_enemySprites.ContainsKey(enemyType))
        {
            DrawTexture(_enemySprites[enemyType], cellPos);
        }
        else
        {
            // Fallback to colored rectangle
            Vector2 cellSize = new Vector2(CellSize, CellSize);
            DrawRect(new Rect2(cellPos, cellSize), GetEnemyColor(x, y));
        }
    }

    private void DrawCellWithColor(Vector2 cellPos, int x, int y, CellType cellType)
    {
        // Original colored rectangle drawing (fallback)
        Vector2 cellSize = new Vector2(CellSize, CellSize);
        Rect2 cellRect = new Rect2(cellPos, cellSize);
        
        Color cellColor = GetCellColor(x, y, cellType);
        DrawRect(cellRect, cellColor);
        DrawRect(cellRect, Colors.Black, false, 1.0f);
    }

    private string GetTerrainType(int x, int y)
    {
        // Determine terrain type based on area
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) return "starting";
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) return "forest";
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) return "cave";
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) return "desert";
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) return "swamp";
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) return "mountain";
        if (IsInAreaBounds(x, y, 120, 90, 35, 40)) return "dungeon";
        if (IsInAreaBounds(x, y, 140, 140, 15, 15)) return "boss_arena";
        
        return "starting"; // Default fallback
    }

    private string GetEnemyTypeForPosition(int x, int y)
    {
        // Return appropriate enemy sprite based on area
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) 
            return GD.Randf() < 0.8f ? "goblin" : "orc";
        
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25))
        {
            float rand = GD.Randf();
            if (rand < 0.4f) return "goblin";
            else if (rand < 0.7f) return "orc";
            else return "forest_spirit";
        }
        
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30))
        {
            float rand = GD.Randf();
            if (rand < 0.4f) return "skeleton_warrior";
            else if (rand < 0.7f) return "cave_spider";
            else return "troll";
        }
        
        if (IsInAreaBounds(x, y, 90, 40, 45, 40))
        {
            float rand = GD.Randf();
            if (rand < 0.5f) return "desert_scorpion";
            else return "orc";
        }
        
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20))
        {
            return GD.Randf() < 0.6f ? "swamp_wretch" : "troll";
        }
        
        if (IsInAreaBounds(x, y, 110, 15, 40, 35))
        {
            return GD.Randf() < 0.6f ? "mountain_wyvern" : "dragon";
        }
        
        if (IsInAreaBounds(x, y, 120, 90, 35, 40))
        {
            return GD.Randf() < 0.5f ? "dungeon_guardian" : "dark_mage";
        }
        
        if (IsInAreaBounds(x, y, 140, 140, 15, 15))
        {
            return GD.Randf() < 0.7f ? "demon_lord" : "ancient_dragon_king";
        }
        
        // Default corridor enemies
        return "goblin";
    }
    
    private Color GetCellColor(int x, int y, CellType cellType)
    {
        if (cellType == CellType.Player) return Colors.Blue;
        if (cellType == CellType.Enemy) return GetEnemyColor(x, y);
        if (cellType == CellType.Wall) return Colors.DarkGray;
        
        // Empty cells get themed colors based on area
        return GetAreaColor(x, y);
    }
    
    private Color GetEnemyColor(int x, int y)
    {
        // Different enemy colors based on area theme
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) return Colors.Pink; // Starting area - weak
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) return Colors.Green; // Forest
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) return Colors.Brown; // Caves
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) return Colors.Yellow; // Desert
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) return Colors.Purple; // Swamp
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) return Colors.White; // Mountain
        if (IsInAreaBounds(x, y, 120, 90, 35, 40)) return Colors.DarkRed; // Dungeon
        if (IsInAreaBounds(x, y, 140, 140, 15, 15)) return Colors.Gold; // Boss arena
        
        return Colors.Red; // Default corridor enemies
    }
    
    private Color GetAreaColor(int x, int y)
    {
        // Themed background colors for different areas
        if (IsInAreaBounds(x, y, 5, GridHeight / 2 - 10, 30, 20)) return new Color(0.9f, 0.9f, 0.9f); // Starting - light gray
        if (IsInAreaBounds(x, y, 40, 15, 35, 30) || IsInAreaBounds(x, y, 45, 50, 25, 25)) return new Color(0.8f, 0.9f, 0.8f); // Forest - light green
        if (IsInAreaBounds(x, y, 20, 90, 40, 35) || IsInAreaBounds(x, y, 70, 95, 30, 30)) return new Color(0.7f, 0.7f, 0.7f); // Caves - darker gray
        if (IsInAreaBounds(x, y, 90, 40, 45, 40)) return new Color(0.9f, 0.9f, 0.7f); // Desert - sandy
        if (IsInAreaBounds(x, y, 25, 130, 35, 25) || IsInAreaBounds(x, y, 70, 135, 25, 20)) return new Color(0.7f, 0.8f, 0.7f); // Swamp - murky green
        if (IsInAreaBounds(x, y, 110, 15, 40, 35)) return new Color(0.9f, 0.9f, 1.0f); // Mountain - light blue
        if (IsInAreaBounds(x, y, 120, 90, 35, 40)) return new Color(0.6f, 0.6f, 0.6f); // Dungeon - dark gray
        if (IsInAreaBounds(x, y, 140, 140, 15, 15)) return new Color(1.0f, 0.9f, 0.7f); // Boss arena - golden
        
        return Colors.LightGray; // Default corridor color
    }
    
    private bool IsInAreaBounds(int x, int y, int areaX, int areaY, int width, int height)
    {
        return x >= areaX && x < areaX + width && y >= areaY && y < areaY + height;
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
