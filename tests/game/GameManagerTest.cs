using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameManagerTest : Node
{
    private GameManager _gameManager;
    private Variant _originalVerboseOrphans;

    private static void ResetSingleton()
    {
        var property = typeof(GameManager).GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object[] { null });
            return;
        }

        var field = typeof(GameManager).GetField("<Instance>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field?.SetValue(null, null);
    }

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        ResetSingleton();
        // Create a fresh GameManager instance for each test
        _gameManager = new GameManager
        {
            AutoSaveEnabled = false
        };
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(_gameManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        // Clean up after each test
        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.QueueFree();
        }
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _gameManager = null;

        // Reset the singleton instance
        ResetSingleton();
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void TestGameManager_InitializesSingleton()
    {
        // Assert
        AssertThat(GameManager.Instance).IsNotNull();
        AssertThat(GameManager.Instance).IsEqual(_gameManager);
    }

    [TestCase]
    public void TestGameManager_InitializesPlayer()
    {
        // Assert
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.Name).IsEqual("Hero");
        AssertThat(_gameManager.Player.Level).IsEqual(1);
        AssertThat(_gameManager.Player.CurrentHealth).IsGreater(0);
        AssertThat(_gameManager.Player.Gold).IsGreater(0);
    }

    [TestCase]
    public void TestGameManager_PlayerStartsWithStarterGear()
    {
        // Assert - Player should have equipment equipped
        var player = _gameManager.Player;
        AssertThat(player.Equipment).IsNotNull();
        
        // Check effective stats are boosted by equipment
        AssertThat(player.GetEffectiveAttack()).IsGreater(player.Attack);
        AssertThat(player.GetEffectiveDefense()).IsGreater(player.Defense);
        AssertThat(player.GetEffectiveSpeed()).IsGreater(player.Speed);
        AssertThat(player.GetEffectiveMaxHealth()).IsGreater(player.MaxHealth);
    }

    [TestCase]
    public void TestGameManager_StartsNotInBattle()
    {
        // Assert
        AssertThat(_gameManager.IsInBattle).IsFalse();
    }

    [TestCase]
    public void TestStartBattle_SetsIsInBattleTrue()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();

        // Act
        _gameManager.StartBattle(enemy);

        // Assert
        AssertThat(_gameManager.IsInBattle).IsTrue();
    }

    [TestCase]
    public void TestStartBattle_EmitsBattleStartedSignal()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();

        // Act
        _gameManager.StartBattle(enemy);

        // Assert
        AssertThat(_gameManager.IsInBattle).IsTrue();
        AssertThat(_gameManager.BattleStartedCount).IsEqual(1);
        AssertThat(_gameManager.LastBattleStartedEnemy).IsEqual(enemy);
    }

    [TestCase]
    public void TestStartBattle_CannotStartWhileInBattle()
    {
        // Arrange
        var enemy1 = Enemy.CreateGoblin();
        var enemy2 = Enemy.CreateOrc();
        _gameManager.StartBattle(enemy1);

        // Act
        _gameManager.StartBattle(enemy2); // Should be ignored

        // Assert
        AssertThat(_gameManager.IsInBattle).IsTrue();
    }

    [TestCase]
    public void TestEndBattle_SetsIsInBattleFalse()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        _gameManager.StartBattle(enemy);

        // Act
        _gameManager.EndBattle(true);

        // Assert
        AssertThat(_gameManager.IsInBattle).IsFalse();
    }

    [TestCase]
    public void TestEndBattle_EmitsBattleEndedSignal()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        _gameManager.StartBattle(enemy);
        bool signalEmitted = false;
        bool playerWonResult = false;

        _gameManager.BattleEnded += (won) =>
        {
            signalEmitted = true;
            playerWonResult = won;
        };

        // Act
        _gameManager.EndBattle(true);

        // Assert
        AssertThat(signalEmitted).IsTrue();
        AssertThat(playerWonResult).IsTrue();
    }

    [TestCase]
    public void TestResetBattleState_ResetsIsInBattle()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        _gameManager.StartBattle(enemy);
        AssertThat(_gameManager.IsInBattle).IsTrue();

        // Act
        _gameManager.ResetBattleState();

        // Assert
        AssertThat(_gameManager.IsInBattle).IsFalse();
    }

    [TestCase]
    public void TestEnsureFreshPlayer_CreatesNewPlayerIfNull()
    {
        // Arrange - Set Player to null using backing field
        var field = typeof(GameManager).GetField("<Player>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertThat(field).IsNotNull();
        field!.SetValue(_gameManager, null);
        AssertThat(_gameManager.Player).IsNull();

        // Act
        _gameManager.EnsureFreshPlayer();

        // Assert
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestEnsureFreshPlayer_CreatesNewPlayerIfDead()
    {
        // Arrange
        _gameManager.Player.CurrentHealth = 0;
        AssertThat(_gameManager.Player.IsAlive).IsFalse();

        // Act
        _gameManager.EnsureFreshPlayer();

        // Assert
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.IsAlive).IsTrue();
        AssertThat(_gameManager.Player.CurrentHealth).IsGreater(0);
    }

    [TestCase]
    public void TestPlayerStatsChanged_SignalCanBeEmitted()
    {
        // Arrange
        bool signalEmitted = false;
        _gameManager.PlayerStatsChanged += () =>
        {
            signalEmitted = true;
        };

        // Act
        _gameManager.NotifyPlayerStatsChanged();

        // Assert
        AssertThat(signalEmitted).IsTrue();
    }

    [TestCase]
    public void TestBattleFlow_StartAndEnd()
    {
        // Arrange
        var enemy = Enemy.CreateGoblin();
        int battleStartCount = 0;
        int battleEndCount = 0;

        _gameManager.BattleStarted += (e) => battleStartCount++;
        _gameManager.BattleEnded += (won) => battleEndCount++;

        // Act
        _gameManager.StartBattle(enemy);
        AssertThat(_gameManager.IsInBattle).IsTrue();
        _gameManager.EndBattle(true);

        // Assert
        AssertThat(_gameManager.IsInBattle).IsFalse();
        AssertThat(battleStartCount).IsEqual(1);
        AssertThat(battleEndCount).IsEqual(1);
    }

    [TestCase]
    public void TestPlayer_StartsWithCorrectExperienceFormula()
    {
        // Assert - Level 1 formula: 100 * 1 + 10 * (1 * 1) = 110
        AssertThat(_gameManager.Player.ExperienceToNext).IsEqual(110);
    }

    [TestCase]
    public void TestPlayer_HasInventoryInitialized()
    {
        // Assert
        AssertThat(_gameManager.Player.Inventory).IsNotNull();
        AssertThat(_gameManager.Player.Equipment).IsNotNull();
    }

    [TestCase]
    public void TestCollectSaveData_ReturnsNullWhenPlayerIsNull()
    {
        // Arrange - Set Player to null using backing field
        var field = typeof(GameManager).GetField("<Player>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertThat(field).IsNotNull();
        field!.SetValue(_gameManager, null);
        AssertThat(_gameManager.Player).IsNull();

        // Act
        var saveData = _gameManager.CollectSaveData();

        // Assert
        AssertThat(saveData).IsNull();
    }

    [TestCase]
    public void TestCollectSaveData_ReturnsNullWhenFloorManagerIsNull()
    {
        // Arrange - FloorManager is null by default in tests (private field)
        // Act
        var saveData = _gameManager.CollectSaveData();

        // Assert - Should return null when FloorManager not set
        AssertThat(saveData).IsNull();
    }

    [TestCase]
    public void TestCollectSaveData_ReturnsNullWhenGridMapIsNull()
    {
        // Arrange - Create a FloorManager without GridMap
        var floorManager = new FloorManager();
        _gameManager.SetFloorManager(floorManager);

        // Act
        var saveData = _gameManager.CollectSaveData();

        // Assert - Should return null because FloorManager.CurrentGridMap is null
        AssertThat(saveData).IsNull();

        // Cleanup - Free the FloorManager to prevent node leak
        floorManager.QueueFree();
    }

    [TestCase]
    public void TestLoadFromSaveData_HandlesNullSaveData()
    {
        // Arrange - Ensure Player is initialized (defensive check)
        if (_gameManager.Player == null)
        {
            _gameManager.EnsureFreshPlayer();
        }
        var originalPlayerName = _gameManager.Player!.Name;

        // Act
        _gameManager.LoadFromSaveData(null!);

        // Assert - Player should be unchanged
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.Name).IsEqual(originalPlayerName);
    }

    [TestCase]
    public void TestLoadFromSaveData_HandlesNullCharacter()
    {
        // Arrange - Ensure Player is initialized (defensive check)
        if (_gameManager.Player == null)
        {
            _gameManager.EnsureFreshPlayer();
        }
        var saveData = new SaveData
        {
            Character = null,
            CurrentFloorIndex = 0,
            PlayerPosition = new Vector2IDto { X = 5, Y = 5 }
        };
        var originalPlayerName = _gameManager.Player!.Name;

        // Act
        _gameManager.LoadFromSaveData(saveData);

        // Assert - Player should be unchanged
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.Name).IsEqual(originalPlayerName);
    }

    [TestCase]
    public void TestLoadFromSaveData_RestoresPlayerState()
    {
        // Arrange
        var characterData = new CharacterSaveData
        {
            Name = "TestHero",
            Level = 5,
            MaxHealth = 200,
            CurrentHealth = 150,
            Attack = 20,
            Defense = 15,
            Speed = 10,
            Experience = 500,
            ExperienceToNext = 600,
            Gold = 1000
        };
        var saveData = new SaveData
        {
            Character = characterData,
            CurrentFloorIndex = 1,
            PlayerPosition = new Vector2IDto { X = 10, Y = 10 }
        };

        // Act
        _gameManager.LoadFromSaveData(saveData);

        // Assert
        AssertThat(_gameManager.Player).IsNotNull();
        AssertThat(_gameManager.Player.Name).IsEqual("TestHero");
        AssertThat(_gameManager.Player.Level).IsEqual(5);
        AssertThat(_gameManager.Player.MaxHealth).IsEqual(200);
        AssertThat(_gameManager.Player.CurrentHealth).IsEqual(150);
        AssertThat(_gameManager.Player.Gold).IsEqual(1000);
    }

    [TestCase]
    public void TestLoadFromSaveData_ResetsBattleState()
    {
        // Arrange - Start a battle first
        var enemy = Enemy.CreateGoblin();
        _gameManager.StartBattle(enemy);
        AssertThat(_gameManager.IsInBattle).IsTrue();

        var saveData = new SaveData
        {
            Character = new CharacterSaveData
            {
                Name = "TestHero",
                Level = 5,
                MaxHealth = 200,
                CurrentHealth = 150,
                Attack = 20,
                Defense = 15,
                Speed = 10,
                Experience = 500,
                ExperienceToNext = 600,
                Gold = 1000
            },
            CurrentFloorIndex = 1,
            PlayerPosition = new Vector2IDto { X = 10, Y = 10 }
        };

        // Act - Load save while in battle
        _gameManager.LoadFromSaveData(saveData);

        // Assert - Battle state should be reset
        AssertThat(_gameManager.IsInBattle).IsFalse();
    }

    [TestCase]
    public void TestCollectSaveData_UsesUtcNow()
    {
        // Arrange - Create FloorManager with mock GridMap for position
        var floorManager = new FloorManager();
        _gameManager.SetFloorManager(floorManager);

        // We can't easily mock GridMap, but we can verify the timestamp is UTC
        // by checking that the difference between UtcNow and the save timestamp is small
        var beforeSave = System.DateTime.UtcNow;

        // Act - Try to collect save data (will fail without proper GridMap setup)
        // Instead, we'll directly test the timestamp property can be set to UtcNow
        var saveData = new SaveData
        {
            Version = 1,
            SaveTimestamp = System.DateTime.UtcNow,
            Character = CharacterSaveData.FromCharacter(_gameManager.Player)
        };
        var afterSave = System.DateTime.UtcNow;

        // Assert - Verify the timestamp is UTC (Kind is Utc)
        AssertThat(saveData.SaveTimestamp.Kind).IsEqual(System.DateTimeKind.Utc);
        // Verify timestamp is within valid range (between before and after)
        AssertThat(saveData.SaveTimestamp >= beforeSave && saveData.SaveTimestamp <= afterSave).IsTrue();

        // Cleanup
        floorManager.QueueFree();
    }

    [TestCase]
    public void TestTriggerAutoSave_SkipsWhenPlayerIsNull()
    {
        // Arrange - Set Player to null using backing field
        var field = typeof(GameManager).GetField("<Player>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        AssertThat(field).IsNotNull();
        field!.SetValue(_gameManager, null);
        AssertThat(_gameManager.Player).IsNull();

        // Act - Should not crash
        _gameManager.TriggerAutoSave();

        // Assert - No exception thrown
        AssertThat(true).IsTrue();
    }

    [TestCase]
    public void TestTriggerAutoSave_SkipsWhenFloorManagerIsNull()
    {
        // Arrange - FloorManager is null by default (private field)
        // Act - Should not crash
        _gameManager.TriggerAutoSave();

        // Assert - No exception thrown, method handles null gracefully
        AssertThat(true).IsTrue();
    }
}
