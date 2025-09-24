using Godot;

public partial class GameManager : Node
{
    [Signal] public delegate void BattleStartedEventHandler(Enemy enemy);
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);
    
    public static GameManager Instance { get; private set; }
    
    public Character Player { get; private set; }
    public bool IsInBattle { get; private set; } = false;
    
    public override void _Ready()
    {
        GD.Print("GameManager _Ready called");

        if (Instance == null)
        {
            Instance = this;
            InitializePlayer();
            GD.Print("GameManager initialized as singleton");
        }
        else
        {
            GD.Print("GameManager instance already exists, queueing free");
            QueueFree();
        }
    }
    
    private void InitializePlayer()
    {
        Player = new Character
        {
            Name = "Hero",
            Level = 1,
            MaxHealth = 100,
            CurrentHealth = 100,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            Experience = 0,
            ExperienceToNext = 100 * 1 + 10 * (1 * 1) // 100 + 10 = 110 for level 1
        };
        
        GD.Print("Player character initialized!");
    }
    
    public void StartBattle(Enemy enemy)
    {
        if (IsInBattle) 
        {
            GD.Print("Warning: Already in battle, ignoring StartBattle call");
            return;
        }
        
        EnsureFreshPlayer();
        
        IsInBattle = true;
        GD.Print($"Battle started against {enemy.Name}! IsInBattle: {IsInBattle}");
        EmitSignal(SignalName.BattleStarted, enemy);
    }
    
    public void EndBattle(bool playerWon)
    {
        if (!IsInBattle)
        {
            GD.Print("Warning: Not in battle, but forcing EndBattle to ensure state consistency");
        }

        IsInBattle = false;
        GD.Print($"Battle ended. Player won: {playerWon}. IsInBattle: {IsInBattle}");
        EmitSignal(SignalName.BattleEnded, playerWon);
    }

    public void ResetBattleState()
    {
        IsInBattle = false;
        GD.Print("Battle state reset. IsInBattle: false");
    }

    public override void _ExitTree()
    {
        // Clear the singleton reference when this scene unloads so a fresh GameManager
        // can be created next time the Game scene is loaded.
        if (Instance == this)
        {
            GD.Print("GameManager exiting tree, clearing singleton Instance");
            Instance = null;
        }
    }

    // Ensure that when a new game starts (e.g., after defeat), we have a live player.
    public void EnsureFreshPlayer()
    {
        if (Player == null || !Player.IsAlive)
        {
            GD.Print("Initializing fresh player for new game");
            InitializePlayer();
        }
    }
}
