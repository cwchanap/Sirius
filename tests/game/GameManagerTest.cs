using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameManagerTest : Node
{
    private GameManager _gameManager;

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
        // Arrange
        // Use reflection to set Player to null
        var field = typeof(GameManager).GetField("Player", 
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_gameManager, null);

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
}
