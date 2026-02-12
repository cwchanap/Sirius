using Godot;
using System;

public partial class GameManager : Node
{
    [Signal] public delegate void BattleStartedEventHandler(Enemy enemy);
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);
    [Signal] public delegate void PlayerStatsChangedEventHandler();

    internal event Action<Enemy> BattleStartedManaged;
    internal Enemy LastBattleStartedEnemy { get; private set; }
    internal int BattleStartedCount { get; private set; }
    internal bool AutoSaveEnabled { get; set; } = true;
    private bool _isAutoSaveSubscribed = false;

    public static GameManager Instance { get; private set; }

    public Character Player { get; private set; }
    public bool IsInBattle { get; private set; } = false;

    private FloorManager _floorManager;

    public override void _Ready()
    {
        GD.Print("GameManager _Ready called");

        if (Instance == null || !IsInstanceValid(Instance))
        {
            Instance = this;
            InitializePlayer();

            // Connect to battle ended signal for auto-save
            if (AutoSaveEnabled && !_isAutoSaveSubscribed)
            {
                BattleEnded += OnBattleEnded;
                _isAutoSaveSubscribed = true;
            }

            GD.Print("GameManager initialized as singleton");
        }
        else
        {
            GD.Print("GameManager instance already exists, queueing free");
            QueueFree();
        }
    }

    /// <summary>
    /// Sets the FloorManager reference for save/load operations.
    /// Called by Game.cs after scene is ready.
    /// </summary>
    public void SetFloorManager(FloorManager floorManager)
    {
        _floorManager = floorManager;
    }

    private void OnBattleEnded(bool playerWon)
    {
        if (playerWon)
        {
            GD.Print("Battle victory - triggering auto-save");
            CallDeferred(nameof(TriggerAutoSave));
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
            ExperienceToNext = 100 * 1 + 10 * (1 * 1), // 100 + 10 = 110 for level 1
            Gold = 100 // Starting gold
        };

        EquipStarterGear(Player);

        GD.Print("Player character initialized!");
    }

    private void EquipStarterGear(Character player)
    {
        if (player == null)
        {
            return;
        }

        EquipAndStore(player, EquipmentCatalog.CreateWoodenSword());
        EquipAndStore(player, EquipmentCatalog.CreateWoodenArmor());
        EquipAndStore(player, EquipmentCatalog.CreateWoodenShield());
        EquipAndStore(player, EquipmentCatalog.CreateWoodenHelmet());
        EquipAndStore(player, EquipmentCatalog.CreateWoodenShoes());

        player.CurrentHealth = player.GetEffectiveMaxHealth();
    }

    private void EquipAndStore(Character player, EquipmentItem item)
    {
        if (player == null || item == null)
        {
            return;
        }

        // Add to inventory first so TryEquip can reference the Item instance
        player.TryAddItem(item, 1, out _);

        if (player.TryEquip(item, out var replacedItem))
        {
            // Equipped item should no longer appear in inventory
            player.TryRemoveItem(item.Id, 1);

            // Store any replaced item back into inventory
            if (replacedItem != null)
            {
                player.TryAddItem(replacedItem, 1, out _);
            }
        }
    }
    
    public void StartBattle(Enemy enemy)
    {
        if (IsInBattle) 
        {
            return;
        }
        
        EnsureFreshPlayer();
        
        IsInBattle = true;
        LastBattleStartedEnemy = enemy;
        BattleStartedCount++;
        GD.Print($"Battle started against {enemy.Name}! IsInBattle: {IsInBattle}");
        BattleStartedManaged?.Invoke(enemy);
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
        if (_isAutoSaveSubscribed)
        {
            BattleEnded -= OnBattleEnded;
            _isAutoSaveSubscribed = false;
        }

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

    public void NotifyPlayerStatsChanged()
    {
        EmitSignal(SignalName.PlayerStatsChanged);
    }

    /// <summary>
    /// Triggers an auto-save to the autosave slot.
    /// </summary>
    // TODO: Save data does not track defeated enemies. Loading an auto-save after
    // a battle victory will respawn the defeated enemy, allowing infinite XP/gold.
    // Consider adding a DefeatedEnemies list to SaveData to persist removal state.
    public void TriggerAutoSave()
    {
        var saveData = CollectSaveData();
        if (saveData == null)
        {
            GD.PushWarning("Auto-save skipped: Could not collect save data");
            return;
        }

        if (SaveManager.Instance == null)
        {
            GD.PushError("Auto-save failed: SaveManager not initialized");
            return;
        }

        bool success = SaveManager.Instance.AutoSave(saveData);
        if (!success)
        {
            GD.PushWarning("Auto-save failed to write to disk");
        }
        else
        {
            GD.Print("Auto-save completed successfully");
        }
    }

    /// <summary>
    /// Collects current game state into a SaveData object.
    /// Returns null if player or floor manager is not available.
    /// </summary>
    public SaveData? CollectSaveData()
    {
        if (Player == null)
        {
            GD.PushWarning("Cannot collect save data: Player is null");
            return null;
        }

        if (_floorManager?.CurrentGridMap == null)
        {
            GD.PushWarning("Cannot collect save data: FloorManager or GridMap not available");
            return null;
        }

        return new SaveData
        {
            Version = SaveData.CurrentVersion,
            Character = CharacterSaveData.FromCharacter(Player),
            CurrentFloorIndex = _floorManager.CurrentFloorIndex,
            PlayerPosition = new Vector2IDto(_floorManager.CurrentGridMap.GetPlayerPosition()),
            SaveTimestamp = System.DateTime.UtcNow
        };
    }

    /// <summary>
    /// Restores player state from save data.
    /// Called after Game scene loads when loading a save.
    /// </summary>
    public void LoadFromSaveData(SaveData? data)
    {
        if (data?.Character == null)
        {
            GD.PushError("Cannot load from null or invalid SaveData");
            return;
        }

        // Validate save file version - allow older versions for migration support
        // (SaveManager.LoadFromFile already rejects newer versions)
        if (data.Version > SaveData.CurrentVersion)
        {
            GD.PushError($"Save file version {data.Version} is newer than supported version {SaveData.CurrentVersion}");
            return;
        }

        // Log version info for migration debugging
        if (data.Version < SaveData.CurrentVersion)
        {
            GD.Print($"Loading older save file version {data.Version} (current: {SaveData.CurrentVersion}) - migration will be applied");
        }

        // Defensive: ensure battle state is reset when loading a save
        // (GameManager is scene-local, but this guards against future persistence changes)
        ResetBattleState();

        Player = data.Character.ToCharacter();
        GD.Print($"Player loaded from save: {Player.Name}, Level {Player.Level}");

        NotifyPlayerStatsChanged();
    }
}
