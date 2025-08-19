using Godot;

public partial class Game : Node2D
{
    private GameManager _gameManager;
    private GridMap _gridMap;
    private Control _gameUI;
    private Label _playerNameLabel;
    private Label _playerLevelLabel;
    private Label _playerHealthLabel;
    private BattleManager _battleManager;

    public override void _Ready()
    {
        GD.Print("Game scene loaded");
        
        // Get references
        _gameManager = GetNode<GameManager>("GameManager");
        _gridMap = GetNode<GridMap>("GridMap");
        _gameUI = GetNode<Control>("UI/GameUI");
        
        // Get UI labels
        _playerNameLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerName");
        _playerLevelLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerLevel");
        _playerHealthLabel = GetNode<Label>("UI/GameUI/TopPanel/PlayerStats/PlayerHealth");
        
        // Connect signals
        _gameManager.BattleStarted += OnBattleStarted;
        _gameManager.BattleEnded += OnBattleEnded;
        
        // Update UI
        UpdatePlayerUI();
    }

    public override void _Input(InputEvent @event)
    {
        // Return to main menu when ESC is pressed (only if not in battle)
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Escape && !_gameManager.IsInBattle)
            {
                ReturnToMainMenu();
            }
        }
    }

    private void OnBattleStarted(Enemy enemy)
    {
        GD.Print($"Starting battle with {enemy.Name}");
        
        // Hide game UI
        _gameUI.Visible = false;
        
        // Load battle scene
        var battleScene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn");
        _battleManager = battleScene.Instantiate<BattleManager>();
        GetNode("UI").AddChild(_battleManager);
        
        // Connect battle signals
        _battleManager.BattleFinished += OnBattleFinished;
        
        // Start the battle
        _battleManager.StartBattle(_gameManager.Player, enemy);
    }

    private void OnBattleEnded(bool playerWon)
    {
        GD.Print($"Battle ended. Player won: {playerWon}");
        
        if (!playerWon)
        {
            // Game over - return to main menu
            GetTree().CreateTimer(2.0).Timeout += ReturnToMainMenu;
        }
    }

    private void OnBattleFinished(bool playerWon)
    {
        // Clean up battle UI
        if (_battleManager != null)
        {
            _battleManager.QueueFree();
            _battleManager = null;
        }
        
        // Show game UI again
        _gameUI.Visible = true;
        
        if (playerWon)
        {
            // Remove enemy from grid
            var playerPos = _gridMap.GetPlayerPosition();
            // Find and remove nearby enemy (this is simplified)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    var checkPos = new Vector2I(playerPos.X + dx, playerPos.Y + dy);
                    _gridMap.RemoveEnemy(checkPos);
                }
            }
        }
        
        // Update UI
        UpdatePlayerUI();
        
        // End the battle in game manager
        _gameManager.EndBattle(playerWon);
    }

    private void UpdatePlayerUI()
    {
        if (_gameManager?.Player != null)
        {
            _playerNameLabel.Text = _gameManager.Player.Name;
            _playerLevelLabel.Text = $"Level: {_gameManager.Player.Level}";
            _playerHealthLabel.Text = $"HP: {_gameManager.Player.CurrentHealth}/{_gameManager.Player.MaxHealth}";
        }
    }

    private void ReturnToMainMenu()
    {
        GD.Print("Returning to main menu");
        GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
    }
}
