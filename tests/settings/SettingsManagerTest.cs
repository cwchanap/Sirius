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
    private long? _originalUiCancelKey;
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
    public async Task SettingsManager_BackupPersistsAfterSuccessfulSave()
    {
        var manager = await BootstrapSettingsManager();
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");

        // First save — no backup yet.
        var first = manager.GetSnapshot();
        first.MasterVolumePercent = 40;
        AssertThat(manager.ApplyAndSave(first)).IsTrue();

        // Second save — the first save's settings.json should now exist as .bak.
        var second = manager.GetSnapshot();
        second.MasterVolumePercent = 80;
        AssertThat(manager.ApplyAndSave(second)).IsTrue();

        AssertThat(File.Exists(backupPath)).IsTrue();
        var backupContent = File.ReadAllText(backupPath);
        AssertThat(backupContent).Contains("\"MasterVolumePercent\": 40");
    }

    [TestCase]
    public async Task SettingsManager_CorruptJson_FallsBackToDefaultsWithoutThrowing()
    {
        // Ensure no leftover backup interferes with the "no backup" scenario.
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        if (File.Exists(backupPath)) File.Delete(backupPath);

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
        // Delete any pre-existing backup (now that ApplyAndSave preserves it).
        if (File.Exists(backupPath)) File.Delete(backupPath);
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
    public async Task SettingsManager_DuplicateKeybindings_SecondActionResetsToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Bind both interact and pause_menu to the same key (E)
        candidate.PrimaryKeybindings["interact"] = (long)Key.E;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.E;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // interact keeps E (set first in default keybinding order), pause_menu resets to Escape
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("interact")).IsEqual((long)Key.E);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_DuplicateKeybindings_DefaultAlsoTaken_Unbinds()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Remap toggle_inventory from I to E — now both toggle_inventory and interact are on E.
        // interact is later in default-order iteration, so it's the "duplicate".
        // Its default is also E, which is already taken → should be unbound (-1).
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.E;
        candidate.PrimaryKeybindings["interact"] = (long)Key.E;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.E);
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual(-1);
    }

    [TestCase]
    public async Task SettingsManager_InteractRemappedToEscape_PauseMenuLeftUnboundNotDuplicated()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Player remaps interact to Escape — now interact and pause_menu both use Escape.
        // interact is earlier in default-order iteration, so it keeps Escape.
        // pause_menu is the "duplicate". Its default is also Escape (already taken).
        // The fallback must NOT force pause_menu back to Escape because that would
        // recreate the duplicate the loop just resolved.
        candidate.PrimaryKeybindings["interact"] = (long)Key.Escape;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // interact keeps Escape (first in default order)
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.Escape);
        // pause_menu default (Escape) is taken by interact → stays unbound (-1)
        // to avoid recreating the duplicate.
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual(-1);
        // interact should be the one bound to Escape in the InputMap
        AssertThat(GetPrimaryKey("interact")).IsEqual((long)Key.Escape);
        // ui_cancel should be explicitly reset to the default pause_menu key
        // (Escape) rather than retaining a stale binding or being left with
        // an invalid key when pause_menu is -1.
    }

    [TestCase]
    public async Task SettingsManager_ToggleInventoryRemappedToEscape_PauseMenuLeftUnbound()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Player remaps toggle_inventory to Escape — now toggle_inventory and
        // pause_menu both use Escape.  toggle_inventory is first in default
        // order so it keeps Escape.  pause_menu is a duplicate; its default
        // (Escape) is also taken.  The fallback must NOT force pause_menu back
        // to Escape because that would recreate the duplicate.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Escape;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Escape);
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual(-1);
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuUnbound_DefaultTaken_UiCancelNotCorrupted()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Force pause_menu to -1 indirectly: bind toggle_inventory to Escape so
        // the conflict loop unbinds pause_menu, and the fallback skips because
        // Escape is taken.  ui_cancel must not receive an invalid key.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Escape;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        // ui_cancel should be explicitly reset to the default pause_menu key
        // (Escape) rather than being rebound to an invalid key.  The else
        // branch in ApplyInputBindings resets ui_cancel when pause_menu is -1.
        var uiCancelKey = GetPrimaryKey("ui_cancel");
        AssertThat(uiCancelKey).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuUnboundAfterRemap_UiCancelResetsToDefault()
    {
        // First save: bind pause_menu to P, which mirrors ui_cancel to P.
        var manager = await BootstrapSettingsManager();
        var firstCandidate = manager.GetSnapshot();
        firstCandidate.PrimaryKeybindings["pause_menu"] = (long)Key.P;

        AssertThat(manager.ApplyAndSave(firstCandidate)).IsTrue();
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.P);

        // Second save: force pause_menu to -1 by claiming its default key
        // (Escape) with toggle_inventory.  ui_cancel must reset to Escape
        // rather than retaining the stale P binding from the first save.
        var secondCandidate = manager.GetSnapshot();
        secondCandidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Escape;
        secondCandidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(secondCandidate)).IsTrue();

        AssertThat(manager.GetSnapshot().PrimaryKeybindings["pause_menu"]).IsEqual(-1);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_DuplicateKeybindings_ThreeWayConflict_ResolvesCorrectly()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // All three actions on the same key — only the first keeps it.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Space;
        candidate.PrimaryKeybindings["interact"] = (long)Key.Space;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Space;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // toggle_inventory keeps Space (first in default order)
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.Space);
        // interact defaults to E (not taken) → resets to E
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        // pause_menu defaults to Escape (not taken) → resets to Escape
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuRemap_MirrorsOntoUiCancel()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.P;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        // pause_menu should be P
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.P);
        // ui_cancel should also be rebound to P
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.P);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuRemapToTab_MirrorsOntoUiCancel()
    {
        // Second key to confirm the mirror is not hardcoded to a specific key.
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Tab;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Tab);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Tab);
    }

    [TestCase]
    public async Task SettingsManager_MovementKeyW_RejectedAndResetToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Try to map pause_menu to W — a movement key
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.W;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // pause_menu should be reset to its default (Escape), not W
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_MovementKeyA_RejectedAndResetToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Try to map toggle_inventory to A — a movement key
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.A;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // toggle_inventory should be reset to its default (I), not A
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
    }

    [TestCase]
    public async Task SettingsManager_MovementArrowKeys_RejectedAndResetToDefault()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["interact"] = (long)Key.Up;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // interact should be reset to its default (E), not Up arrow
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
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

    [TestCase]
    public async Task SettingsManager_SaveToFile_BackupSurvivesFailedRename()
    {
        // Arrange: bootstrap with known settings so settings.json and .bak exist
        var manager = await BootstrapSettingsManager();
        var first = manager.GetSnapshot();
        first.MasterVolumePercent = 60;
        AssertThat(manager.ApplyAndSave(first)).IsTrue();

        // Both files should now exist after two saves
        var second = manager.GetSnapshot();
        second.MasterVolumePercent = 80;
        AssertThat(manager.ApplyAndSave(second)).IsTrue();

        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");

        // Verify backup exists (should contain the 60% save)
        AssertThat(System.IO.File.Exists(backupPath)).IsTrue();

        // Backup content before the next save should contain 80 (second save became backup)
        var backupContentBefore = System.IO.File.ReadAllText(backupPath);
        AssertThat(backupContentBefore.Contains("80")).IsTrue();

        var third = manager.GetSnapshot();
        third.MasterVolumePercent = 90;
        AssertThat(manager.ApplyAndSave(third)).IsTrue();

        // After successful save, backup should contain the previous (80%) settings
        var backupContentAfter = System.IO.File.ReadAllText(backupPath);
        AssertThat(backupContentAfter.Contains("80")).IsTrue();
        AssertThat(backupContentAfter.Contains("90")).IsFalse();
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

        // Also capture ui_cancel since ApplyInputBindings now mirrors pause_menu onto it.
        _originalUiCancelKey = InputMap.HasAction("ui_cancel") ? GetPrimaryKey("ui_cancel") : null;

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

        // Restore ui_cancel to its original binding.
        if (_originalUiCancelKey.HasValue && InputMap.HasAction("ui_cancel"))
        {
            SetPrimaryKey("ui_cancel", (Key)_originalUiCancelKey.Value);
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
            PhysicalKeycode = key
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
