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
    private Dictionary<string, long?> _originalBindings = new();
    private DisplayServer.WindowMode _originalWindowMode;
    private Vector2I _originalWindowSize;
    private float _originalMasterDb;

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
        DeleteSettingsFiles();
        CaptureRuntimeState();
        await EnsureNoManagerInTree();
    }

    [After]
    public async Task Cleanup()
    {
        if (_settingsManager != null && IsInstanceValid(_settingsManager))
        {
            _settingsManager.QueueFree();
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _settingsManager = null;
        ResetSingleton();
        RestoreRuntimeState();
        DeleteSettingsFiles();
    }

    [TestCase]
    public async Task SettingsManager_CreatesExpectedDefaults()
    {
        var manager = await BootstrapSettingsManager();

        var defaults = manager.CreateDefaults();

        AssertThat(defaults.MasterVolumePercent).IsEqual(100);
        AssertThat(defaults.Difficulty).IsEqual("Normal");
        AssertThat(defaults.AutoSaveEnabled).IsTrue();
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

        var saved = manager.ApplyAndSave(candidate);
        AssertThat(saved).IsTrue();

        manager.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _settingsManager = null;
        ResetSingleton();

        var rebootedManager = await BootstrapSettingsManager();
        var snapshot = rebootedManager.GetSnapshot();

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
    }

    [TestCase]
    public async Task SettingsManager_SaveAndReload_PreservesSettings()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.MusicVolumePercent = 35;
        candidate.FullscreenEnabled = true;
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Q;

        var saved = manager.ApplyAndSave(candidate);
        AssertThat(saved).IsTrue();

        manager.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _settingsManager = null;
        ResetSingleton();

        var reloadedManager = await BootstrapSettingsManager();
        var snapshot = reloadedManager.GetSnapshot();

        AssertThat(snapshot.MusicVolumePercent).IsEqual(35);
        AssertThat(snapshot.FullscreenEnabled).IsTrue();
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Q);
    }

    [TestCase]
    public async Task SettingsManager_CorruptJson_FallsBackToDefaults()
    {
        var settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        File.WriteAllText(settingsPath, "{ this is not valid JSON !!! }");

        var manager = await BootstrapSettingsManager();
        var snapshot = manager.GetSnapshot();
        var defaults = manager.CreateDefaults();

        AssertThat(snapshot.MasterVolumePercent).IsEqual(defaults.MasterVolumePercent);
        AssertThat(snapshot.Difficulty).IsEqual(defaults.Difficulty);
        AssertThat(snapshot.AutoSaveEnabled).IsEqual(defaults.AutoSaveEnabled);
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
    public async Task SettingsManager_InvalidKeybinding_FallsBackToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["toggle_inventory"] = 0L; // Key.None — invalid

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.I);
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_WriteFailKeepsLiveStateUnchanged()
    {
        var manager = await BootstrapSettingsManager();
        AssertThat(manager.GetSnapshot().MasterVolumePercent).IsEqual(100);

        // Block writes by placing a directory where the temp file would go.
        var tmpPath = ProjectSettings.GlobalizePath("user://settings.json.tmp");
        Directory.CreateDirectory(tmpPath);

        try
        {
            var candidate = manager.GetSnapshot();
            candidate.MasterVolumePercent = 42;

            var result = manager.ApplyAndSave(candidate);

            AssertThat(result).IsFalse();
            AssertThat(manager.GetSnapshot().MasterVolumePercent).IsEqual(100);
        }
        finally
        {
            if (Directory.Exists(tmpPath))
                Directory.Delete(tmpPath, recursive: true);
        }
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

    private async Task EnsureNoManagerInTree()
    {
        if (_settingsManager != null && IsInstanceValid(_settingsManager))
        {
            _settingsManager.QueueFree();
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
            _settingsManager = null;
        }
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
                InputMap.AddAction(action);
                InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = (Key)originalKey.Value });
            }
        }

        DisplayServer.WindowSetMode(_originalWindowMode);
        DisplayServer.WindowSetSize(_originalWindowSize);
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), _originalMasterDb);
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
        }
    }
}
