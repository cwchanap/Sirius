using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsManagerTest : Node
{
    private SettingsManager? _settingsManager;
    private GameManager? _gameManager;
    private Dictionary<string, long?> _originalBindings = new();
    private DisplayServer.WindowMode _originalWindowMode;
    private Vector2I _originalWindowSize;
    private float _originalMasterDb;
    private int _originalBusCount;

    private static readonly string[] ManagedActions =
    {
        "toggle_inventory",
        "interact",
        "pause_menu"
    };

    [Before]
    public async Task Setup()
    {
        ResetSingleton();
        ResetGameManagerSingleton();
        DeleteSettingsFiles();
        CaptureRuntimeState();
        await EnsureManagersFreed();
    }

    [After]
    public async Task Cleanup()
    {
        await EnsureManagersFreed();
        ResetSingleton();
        ResetGameManagerSingleton();
        RestoreRuntimeState();
        DeleteSettingsFiles();
    }

    [TestCase]
    public async Task SettingsManager_CreatesExpectedDefaults()
    {
        var manager = await BootstrapSettingsManager();

        var defaults = manager.CreateDefaults();

        AssertThat(defaults.MasterVolumePercent).IsEqual(100);
        AssertThat(defaults.MusicVolumePercent).IsEqual(100);
        AssertThat(defaults.SfxVolumePercent).IsEqual(100);
        AssertThat(defaults.Difficulty).IsEqual("Normal");
        AssertThat(defaults.FullscreenEnabled).IsFalse();
        AssertThat(defaults.ResolutionWidth).IsEqual(1280);
        AssertThat(defaults.ResolutionHeight).IsEqual(720);
        AssertThat(defaults.AutoSaveEnabled).IsTrue();
        AssertThat(defaults.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(defaults.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        AssertThat(defaults.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_Ready_LoadsPersistedSettingsOnBoot()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.MasterVolumePercent = 75;
        candidate.MusicVolumePercent = 45;
        candidate.SfxVolumePercent = 20;
        candidate.Difficulty = "Hard";
        candidate.AutoSaveEnabled = false;
        candidate.FullscreenEnabled = true;
        candidate.ResolutionWidth = 1600;
        candidate.ResolutionHeight = 900;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.P;
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Tab;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var rebootedManager = await RebootSettingsManager();
        var snapshot = rebootedManager.GetSnapshot();
        var expectedMasterDb = Mathf.LinearToDb(0.75f);
        var expectedMusicDb = Mathf.LinearToDb(0.45f);
        var expectedSfxDb = Mathf.LinearToDb(0.20f);

        AssertThat(snapshot.MasterVolumePercent).IsEqual(75);
        AssertThat(snapshot.MusicVolumePercent).IsEqual(45);
        AssertThat(snapshot.SfxVolumePercent).IsEqual(20);
        AssertThat(snapshot.Difficulty).IsEqual("Hard");
        AssertThat(snapshot.AutoSaveEnabled).IsFalse();
        AssertThat(snapshot.FullscreenEnabled).IsTrue();
        AssertThat(snapshot.ResolutionWidth).IsEqual(1600);
        AssertThat(snapshot.ResolutionHeight).IsEqual(900);
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.P);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.P);
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.Tab);
        AssertThat(rebootedManager.LastAppliedWindowMode).IsEqual(DisplayServer.WindowMode.Fullscreen);
        AssertThat(rebootedManager.LastAppliedWindowSize).IsEqual(new Vector2I(1600, 900));
        AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")) - expectedMasterDb)).IsLess(0.01f);
        AssertThat(AudioServer.GetBusIndex("Music")).IsGreaterEqual(0);
        AssertThat(AudioServer.GetBusIndex("SFX")).IsGreaterEqual(0);
        AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Music")) - expectedMusicDb)).IsLess(0.01f);
        AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("SFX")) - expectedSfxDb)).IsLess(0.01f);

        var gameManager = await BootstrapGameManager();
        AssertThat(gameManager.AutoSaveEnabled).IsFalse();
    }

    [TestCase]
    public async Task SettingsManager_SaveAndReload_PreservesSettings()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.MusicVolumePercent = 35;
        candidate.FullscreenEnabled = true;
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Q;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var reloadedManager = await RebootSettingsManager();
        var snapshot = reloadedManager.GetSnapshot();

        AssertThat(snapshot.MusicVolumePercent).IsEqual(35);
        AssertThat(snapshot.FullscreenEnabled).IsTrue();
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Q);
    }

    [TestCase]
    public async Task SettingsManager_CorruptJson_FallsBackToDefaultsWithoutThrowing()
    {
        var gameManager = await BootstrapGameManager(autoSaveEnabled: false);
        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        File.WriteAllText(settingsPath, "{ invalid json");
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        DisplayServer.WindowSetSize(new Vector2I(1600, 900));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), -20.0f);
        SetPrimaryKey("pause_menu", Key.P);

        var manager = await BootstrapSettingsManager();
        var snapshot = manager.GetSnapshot();

        AssertThat(snapshot.MasterVolumePercent).IsEqual(100);
        AssertThat(snapshot.Difficulty).IsEqual("Normal");
        AssertThat(snapshot.AutoSaveEnabled).IsTrue();
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        AssertThat(manager.LastAppliedWindowMode).IsEqual(DisplayServer.WindowMode.Windowed);
        AssertThat(manager.LastAppliedWindowSize).IsEqual(new Vector2I(1280, 720));
        AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")))).IsLess(0.01f);
        AssertThat(gameManager.AutoSaveEnabled).IsTrue();
    }

    [TestCase]
    public async Task SettingsManager_Ready_CorruptPrimaryUsesValidBackup()
    {
        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        var validBackupJson = """
            {
              "Version": 1,
              "MasterVolumePercent": 55,
              "MusicVolumePercent": 40,
              "SfxVolumePercent": 30,
              "Difficulty": "Normal",
              "FullscreenEnabled": false,
              "ResolutionWidth": 1280,
              "ResolutionHeight": 720,
              "AutoSaveEnabled": true,
              "PrimaryKeybindings": {
                "toggle_inventory": 73,
                "interact": 69,
                "pause_menu": 4194305
              }
            }
            """;
        File.WriteAllText(settingsPath, "{ invalid primary json");
        File.WriteAllText(backupPath, validBackupJson);

        var manager = await BootstrapSettingsManager();

        AssertThat(manager.GetSnapshot().MasterVolumePercent).IsEqual(55);
        AssertThat(File.ReadAllText(settingsPath)).Contains("\"MasterVolumePercent\": 55");
    }

    [TestCase]
    public async Task SettingsManager_Ready_CorruptBackupFallsBackToDefaults()
    {
        var gameManager = await BootstrapGameManager(autoSaveEnabled: false);
        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        if (File.Exists(settingsPath))
        {
            File.Delete(settingsPath);
        }

        File.WriteAllText(backupPath, "{ invalid backup json");
        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        DisplayServer.WindowSetSize(new Vector2I(1600, 900));
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), -20.0f);
        SetPrimaryKey("pause_menu", Key.P);

        var manager = await BootstrapSettingsManager();
        var snapshot = manager.GetSnapshot();

        AssertThat(snapshot.MasterVolumePercent).IsEqual(100);
        AssertThat(snapshot.Difficulty).IsEqual("Normal");
        AssertThat(snapshot.AutoSaveEnabled).IsTrue();
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        AssertThat(manager.LastAppliedWindowMode).IsEqual(DisplayServer.WindowMode.Windowed);
        AssertThat(manager.LastAppliedWindowSize).IsEqual(new Vector2I(1280, 720));
        AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")))).IsLess(0.01f);
        AssertThat(gameManager.AutoSaveEnabled).IsTrue();
        AssertThat(File.ReadAllText(settingsPath)).Contains("\"MasterVolumePercent\": 100");
    }

    [TestCase]
    public async Task SettingsManager_Ready_UsesBackupAsFallbackWhenPrimaryIsMissing()
    {
        var manager = await BootstrapSettingsManager();
        var olderSettings = manager.GetSnapshot();
        olderSettings.MasterVolumePercent = 25;
        AssertThat(manager.ApplyAndSave(olderSettings)).IsTrue();
        var olderJson = File.ReadAllText(ProjectSettings.GlobalizePath("user://settings.json"));

        var newerSettings = manager.GetSnapshot();
        newerSettings.MasterVolumePercent = 75;
        AssertThat(manager.ApplyAndSave(newerSettings)).IsTrue();

        await FreeSettingsManager();

        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        File.WriteAllText(backupPath, olderJson);
        File.Delete(settingsPath);

        var recoveredManager = await BootstrapSettingsManager();

        AssertThat(recoveredManager.GetSnapshot().MasterVolumePercent).IsEqual(25);
    }

    [TestCase]
    public async Task SettingsManager_Ready_RecoversBackupWhenPrimaryMissing()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.MasterVolumePercent = 65;
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Y;
        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        await FreeSettingsManager();

        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        File.Move(settingsPath, backupPath);

        var recoveredManager = await BootstrapSettingsManager();
        var snapshot = recoveredManager.GetSnapshot();

        AssertThat(snapshot.MasterVolumePercent).IsEqual(65);
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Y);
        AssertThat(File.Exists(settingsPath)).IsTrue();
    }

    [TestCase]
    public async Task SettingsManager_VolumeOutOfRange_IsClamped()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.MasterVolumePercent = 150;
        candidate.MusicVolumePercent = -10;
        candidate.SfxVolumePercent = 999;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        AssertThat(snapshot.MasterVolumePercent).IsEqual(100);
        AssertThat(snapshot.MusicVolumePercent).IsEqual(0);
        AssertThat(snapshot.SfxVolumePercent).IsEqual(100);
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_RoundTripsToggleInventoryBinding()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Y;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var reloadedManager = await RebootSettingsManager();
        var snapshot = reloadedManager.GetSnapshot();

        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Y);
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.Y);
    }

    [TestCase]
    public async Task SettingsManager_InvalidPauseMenuBinding_FallsBackToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = 0;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_InvalidResolution_ReturnsFalseWithoutChangingLiveSettings()
    {
        var manager = await BootstrapSettingsManager();
        var originalSnapshot = manager.GetSnapshot();
        var originalWindowSize = manager.LastAppliedWindowSize;
        var candidate = manager.GetSnapshot();
        candidate.ResolutionWidth = 0;
        candidate.ResolutionHeight = -50;

        var saved = manager.ApplyAndSave(candidate);

        AssertThat(saved).IsFalse();
        AssertThat(manager.GetSnapshot().ResolutionWidth).IsEqual(originalSnapshot.ResolutionWidth);
        AssertThat(manager.GetSnapshot().ResolutionHeight).IsEqual(originalSnapshot.ResolutionHeight);
        AssertThat(manager.LastAppliedWindowSize).IsEqual(originalWindowSize);
    }

    [TestCase]
    public async Task SettingsManager_UnsupportedPositiveResolution_ReturnsFalseWithoutChangingLiveSettings()
    {
        var manager = await BootstrapSettingsManager();
        var originalSnapshot = manager.GetSnapshot();
        var candidate = manager.GetSnapshot();
        candidate.ResolutionWidth = 320;
        candidate.ResolutionHeight = 200;

        var saved = manager.ApplyAndSave(candidate);

        AssertThat(saved).IsFalse();
        AssertThat(manager.GetSnapshot().ResolutionWidth).IsEqual(originalSnapshot.ResolutionWidth);
        AssertThat(manager.GetSnapshot().ResolutionHeight).IsEqual(originalSnapshot.ResolutionHeight);
    }

    [TestCase]
    public async Task SettingsManager_OversizedResolution_ReturnsFalseWithoutChangingLiveSettings()
    {
        var manager = await BootstrapSettingsManager();
        var originalSnapshot = manager.GetSnapshot();
        var candidate = manager.GetSnapshot();
        candidate.ResolutionWidth = 10000;
        candidate.ResolutionHeight = 8000;

        var saved = manager.ApplyAndSave(candidate);

        AssertThat(saved).IsFalse();
        AssertThat(manager.GetSnapshot().ResolutionWidth).IsEqual(originalSnapshot.ResolutionWidth);
        AssertThat(manager.GetSnapshot().ResolutionHeight).IsEqual(originalSnapshot.ResolutionHeight);
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_WriteFailKeepsLiveStateUnchanged()
    {
        var manager = await BootstrapSettingsManager();
        var baseline = manager.GetSnapshot();
        baseline.MasterVolumePercent = 60;
        AssertThat(manager.ApplyAndSave(baseline)).IsTrue();

        var tmpPath = ProjectSettings.GlobalizePath("user://settings.json.tmp");
        Directory.CreateDirectory(tmpPath);

        try
        {
            var candidate = manager.GetSnapshot();
            candidate.MasterVolumePercent = 42;

            AssertThat(manager.ApplyAndSave(candidate)).IsFalse();
            AssertThat(manager.GetSnapshot().MasterVolumePercent).IsEqual(60);
            AssertThat(Mathf.Abs(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")) - Mathf.LinearToDb(0.6f))).IsLess(0.01f);
        }
        finally
        {
            if (Directory.Exists(tmpPath))
            {
                Directory.Delete(tmpPath, true);
            }
        }

        var reloadedManager = await RebootSettingsManager();
        AssertThat(reloadedManager.GetSnapshot().MasterVolumePercent).IsEqual(60);
    }

    private async Task<SettingsManager> BootstrapSettingsManager()
    {
        ResetSingleton();
        _settingsManager = new SettingsManager();
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(_settingsManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        return _settingsManager;
    }

    private async Task<SettingsManager> RebootSettingsManager()
    {
        await FreeSettingsManager();
        ResetSingleton();
        return await BootstrapSettingsManager();
    }

    private async Task<GameManager> BootstrapGameManager(bool autoSaveEnabled = true)
    {
        ResetGameManagerSingleton();
        _gameManager = new GameManager
        {
            AutoSaveEnabled = autoSaveEnabled
        };
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(_gameManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        return _gameManager;
    }

    private async Task FreeSettingsManager()
    {
        if (_settingsManager != null && IsInstanceValid(_settingsManager))
        {
            _settingsManager.QueueFree();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }

        _settingsManager = null;
    }

    private async Task FreeGameManager()
    {
        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.QueueFree();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        }

        _gameManager = null;
    }

    private async Task EnsureManagersFreed()
    {
        await FreeSettingsManager();
        await FreeGameManager();

        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        foreach (var child in root.GetChildren())
        {
            if (child is SettingsManager || child is GameManager)
            {
                child.QueueFree();
            }
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    private static void ResetSingleton()
    {
        var property = typeof(SettingsManager).GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object?[] { null });
            return;
        }

        var field = typeof(SettingsManager).GetField("<Instance>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
            return;
        }

        throw new InvalidOperationException("Failed to reset SettingsManager singleton.");
    }

    private static void ResetGameManagerSingleton()
    {
        var property = typeof(GameManager).GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object?[] { null });
            return;
        }

        var field = typeof(GameManager).GetField("<Instance>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
            return;
        }

        throw new InvalidOperationException("Failed to reset GameManager singleton.");
    }

    private void CaptureRuntimeState()
    {
        _originalBindings = new Dictionary<string, long?>();
        foreach (var action in ManagedActions)
        {
            _originalBindings[action] = InputMap.HasAction(action) ? GetPrimaryKey(action) : null;
        }

        _originalWindowMode = DisplayServer.WindowGetMode();
        _originalWindowSize = DisplayServer.WindowGetSize();
        _originalMasterDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
        _originalBusCount = AudioServer.BusCount;
    }

    private void RestoreRuntimeState()
    {
        foreach (var action in ManagedActions)
        {
            if (InputMap.HasAction(action))
            {
                InputMap.EraseAction(action);
            }

            var originalKey = _originalBindings[action];
            if (originalKey.HasValue)
            {
                SetPrimaryKey(action, (Key)originalKey.Value);
            }
        }

        while (AudioServer.BusCount > _originalBusCount)
        {
            AudioServer.RemoveBus(AudioServer.BusCount - 1);
        }

        DisplayServer.WindowSetMode(_originalWindowMode);
        DisplayServer.WindowSetSize(_originalWindowSize);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), _originalMasterDb);
    }

    private static void SetPrimaryKey(string actionName, Key key)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }

        foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
        {
            InputMap.ActionEraseEvent(actionName, inputEvent);
        }

        InputMap.ActionAddEvent(actionName, new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key
        });
    }

    private static long GetPrimaryKey(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            return 0L;
        }

        foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                return (long)keyEvent.PhysicalKeycode;
            }
        }

        return 0L;
    }

    private static void DeleteSettingsFiles()
    {
        foreach (var path in new[]
                 {
                     ProjectSettings.GlobalizePath("user://settings.json"),
                     ProjectSettings.GlobalizePath("user://settings.json.tmp"),
                     ProjectSettings.GlobalizePath("user://settings.json.bak")
                 })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
