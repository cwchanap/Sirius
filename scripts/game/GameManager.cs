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
        if (Instance == null)
        {
            Instance = this;
            InitializePlayer();
        }
        else
        {
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
            ExperienceToNext = 100
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
        
        IsInBattle = true;
        GD.Print($"Battle started against {enemy.Name}! IsInBattle: {IsInBattle}");
        EmitSignal(SignalName.BattleStarted, enemy);
    }
    
    public void EndBattle(bool playerWon)
    {
        if (!IsInBattle)
        {
            GD.Print("Warning: Not in battle, ignoring EndBattle call");
            return;
        }
        
        IsInBattle = false;
        GD.Print($"Battle ended. Player won: {playerWon}. IsInBattle: {IsInBattle}");
        EmitSignal(SignalName.BattleEnded, playerWon);
    }
}
