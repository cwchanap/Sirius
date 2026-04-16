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
    private Dictionary<string, List<InputEvent>> _originalBindings = new();
    private List<InputEvent> _originalUiCancelEvents = new();
    private DisplayServer.WindowMode _originalWindowMode;
    private Vector2I _originalWindowSize;
    private Dictionary<int, float> _originalBusVolumes = new();
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
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Q;

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
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.Q);
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

        // The good backup must be preserved — not overwritten with the corrupt primary.
        AssertThat(File.Exists(backupPath)).IsTrue();
        AssertThat(File.ReadAllText(backupPath)).Contains("\"MasterVolumePercent\": 55");
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
    public async Task SettingsManager_InteractRemappedToEscape_ReservedKeyRejected()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Reset all bindings to defaults to ensure a clean slate regardless of
        // what previous tests left in the live SettingsManager state.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        candidate.PrimaryKeybindings["interact"] = (long)Key.E;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        // Player tries to remap interact to Escape.  Escape is a reserved UI
        // key, and interact is not pause_menu, so NormalizeKeybindings rejects
        // it and resets interact to its default (E).  pause_menu is unaffected
        // — it keeps Escape (its default) because pause_menu is exempted from
        // reserved-key rejection for non-movement keys.
        candidate.PrimaryKeybindings["interact"] = (long)Key.Escape;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // interact is reset to its default E (reserved key rejected)
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        // pause_menu keeps Escape — no conflict because interact was reset
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        // InputMap reflects the normalized bindings
        AssertThat(GetPrimaryKey("interact")).IsEqual((long)Key.E);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_ToggleInventoryRemappedToEscape_ReservedKeyRejected()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // Player tries to remap toggle_inventory to Escape.  Escape is a
        // reserved UI key and toggle_inventory is not pause_menu, so it gets
        // reset to its default (I).  pause_menu keeps Escape (exempted).
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Escape;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // toggle_inventory is reset to its default I (reserved key rejected)
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        // pause_menu keeps Escape — no conflict because toggle_inventory was reset
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("toggle_inventory")).IsEqual((long)Key.I);
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
    public async Task SettingsManager_PauseMenuRemapRestored_UiCancelMirrorsChange()
    {
        // First save: bind pause_menu to P, which mirrors ui_cancel to P.
        var manager = await BootstrapSettingsManager();
        var firstCandidate = manager.GetSnapshot();
        firstCandidate.PrimaryKeybindings["pause_menu"] = (long)Key.P;

        AssertThat(manager.ApplyAndSave(firstCandidate)).IsTrue();
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.P);

        // Second save: restore pause_menu to its default (Escape).
        // ui_cancel must mirror to Escape rather than retaining the stale P
        // binding from the first save.
        var secondCandidate = manager.GetSnapshot();
        secondCandidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;

        AssertThat(manager.ApplyAndSave(secondCandidate)).IsTrue();

        AssertThat(manager.GetSnapshot().PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_DuplicateKeybindings_ThreeWayConflict_ResolvesCorrectly()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // All three actions on the same non-reserved key — only the first keeps it.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.T;
        candidate.PrimaryKeybindings["interact"] = (long)Key.T;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.T;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // toggle_inventory keeps T (first in default order)
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.T);
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
    public async Task SettingsManager_PauseMenuRemapToTab_ReservedKeyRejected()
    {
        // Tab is a UI key that backs ui_focus_next.  Remapping pause_menu to
        // Tab would mirror it onto ui_cancel, creating a dual-action where a
        // single Tab press both changes focus AND cancels the dialog.
        // NormalizeKeybindings must reject it and reset to default (Escape).
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Tab;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // Tab is rejected; pause_menu reset to default Escape
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        // ui_cancel mirrors the reset pause_menu key (Escape)
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuRemapToEnter_ReservedKeyRejected()
    {
        // Enter is a UI key that backs ui_accept.  Remapping pause_menu to
        // Enter would mirror it onto ui_cancel, creating a dual-action where a
        // single Enter press both confirms AND cancels modals (NPC AcceptDialog,
        // SaveLoadDialog, etc.).  NormalizeKeybindings must reject it and reset
        // to default (Escape).
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Enter;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // Enter is rejected; pause_menu reset to default Escape
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuRemapToSpace_ReservedKeyRejected()
    {
        // Space is a UI key that backs ui_accept.  Remapping pause_menu to
        // Space would mirror it onto ui_cancel, creating a dual-action where a
        // single Space press both confirms AND cancels modals.
        // NormalizeKeybindings must reject it and reset to default (Escape).
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Space;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var snapshot = manager.GetSnapshot();
        // Space is rejected; pause_menu reset to default Escape
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
    }

    [TestCase]
    public async Task SettingsManager_PauseMenuRemapToEscape_AcceptedAsDefault()
    {
        // Escape is the default pause_menu key and is also a UI key; remapping
        // back to it explicitly should work (no regression from the reserved
        // key fix).
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        // First remap away from default
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.P;
        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();
        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.P);

        // Now remap back to Escape
        candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.Escape;
        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        AssertThat(GetPrimaryKey("pause_menu")).IsEqual((long)Key.Escape);
        AssertThat(GetPrimaryKey("ui_cancel")).IsEqual((long)Key.Escape);
        AssertThat(manager.GetSnapshot().PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.Escape);
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
    public async Task SettingsManager_SaveToFile_BackupRotatedAfterSuccessfulSave()
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

        // Verify backup exists (should contain the first save, i.e. 60%)
        AssertThat(System.IO.File.Exists(backupPath)).IsTrue();

        // After the second save (80%), the backup should hold the previous settings (60%)
        var backupContentBefore = System.IO.File.ReadAllText(backupPath);
        var backupBefore = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(backupContentBefore);
        AssertThat(backupBefore).IsNotNull();
        AssertThat(backupBefore!.MasterVolumePercent).IsEqual(60);

        var third = manager.GetSnapshot();
        third.MasterVolumePercent = 90;
        AssertThat(manager.ApplyAndSave(third)).IsTrue();

        // After the third save (90%), the backup should hold the second save's values (80%)
        var backupContentAfter = System.IO.File.ReadAllText(backupPath);
        var backupAfter = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(backupContentAfter);
        AssertThat(backupAfter).IsNotNull();
        AssertThat(backupAfter!.MasterVolumePercent).IsEqual(80);
    }

    [TestCase]
    public async Task SettingsManager_Ready_NullJsonFileUsesDefaults()
    {
        // Write a settings file that contains only the JSON null token.
        // Explicitly ensure no backup exists so the manager falls to defaults
        // (not a leaked backup from a previous test).
        var primaryPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(primaryPath)!);
        System.IO.File.WriteAllText(primaryPath, "null");
        if (System.IO.File.Exists(backupPath))
        {
            System.IO.File.Delete(backupPath);
        }

        // Booting should not crash, and should produce defaults
        var manager = await BootstrapSettingsManager();
        var snapshot = manager.GetSnapshot();

        AssertThat(snapshot.MasterVolumePercent).IsEqual(100);
        AssertThat(snapshot.Difficulty).IsEqual("Normal");

        // The corrupt "null" file must be rewritten with defaults so the next
        // launch does not hit the same error path again.
        AssertThat(File.ReadAllText(primaryPath)).Contains("\"MasterVolumePercent\": 100");
    }

    [TestCase]
    public async Task SettingsManager_Ready_NullJsonFile_SelfHealsOnNextBoot()
    {
        // Write a settings file that contains only the JSON null token.
        var primaryPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(primaryPath)!);
        System.IO.File.WriteAllText(primaryPath, "null");
        if (System.IO.File.Exists(backupPath))
        {
            System.IO.File.Delete(backupPath);
        }

        // First boot — should rewrite defaults to disk.
        var first = await BootstrapSettingsManager();
        AssertThat(first.GetSnapshot().MasterVolumePercent).IsEqual(100);

        // Second boot — the file should no longer be "null"; settings should
        // load normally from the rewritten file without hitting the error path.
        var second = await RebootSettingsManager();
        AssertThat(second.GetSnapshot().MasterVolumePercent).IsEqual(100);
        AssertThat(second.GetSnapshot().Difficulty).IsEqual("Normal");
    }

    [TestCase]
    public async Task SettingsManager_Ready_NullJsonFileWithValidBackup_RecoversFromBackup()
    {
        // Primary contains "null" — deserialize succeeds but returns null.
        // A valid backup exists and should be recovered instead of defaults.
        var primaryPath = ProjectSettings.GlobalizePath("user://settings.json");
        var backupPath = ProjectSettings.GlobalizePath("user://settings.json.bak");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(primaryPath)!);
        System.IO.File.WriteAllText(primaryPath, "null");
        System.IO.File.WriteAllText(backupPath, """
            {
              "Version": 1,
              "MasterVolumePercent": 42,
              "MusicVolumePercent": 60,
              "SfxVolumePercent": 70,
              "Difficulty": "Hard",
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
            """);

        var manager = await BootstrapSettingsManager();
        var snapshot = manager.GetSnapshot();

        // Backup values must be recovered, not factory defaults
        AssertThat(snapshot.MasterVolumePercent).IsEqual(42);
        AssertThat(snapshot.Difficulty).IsEqual("Hard");

        // Primary must be rewritten with recovered settings
        AssertThat(File.ReadAllText(primaryPath)).Contains("\"MasterVolumePercent\": 42");
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_TwoUnboundActionsDoNotConflict()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();

        // Both W and A are reserved keys — both get reset to their defaults
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.W;
        candidate.PrimaryKeybindings["interact"] = (long)Key.A;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();
        var snapshot = manager.GetSnapshot();

        // toggle_inventory default (I) is not reserved — resets to I
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        // interact default (E) is not reserved — resets to E (not treated as duplicate of I)
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_UiKeysAreRejected()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();

        // Enter and Space back Godot's ui_accept; binding a game action to them
        // would let Game._Input() steal events from AcceptDialog controls.
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.Enter;
        candidate.PrimaryKeybindings["interact"] = (long)Key.Space;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();
        var snapshot = manager.GetSnapshot();

        // Both must be reset to their non-reserved defaults
        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
    }

    [TestCase]
    public async Task SettingsManager_NormalizeKeybindings_MultipleUnboundActionsNonConflicting()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();

        // toggle_inventory=T, interact=T (duplicate), pause_menu=E (takes interact's default)
        // → interact should resolve to -1 (unbound) because T is taken and E is taken by pause_menu
        // The -1 for interact must NOT collide with any other -1 in the HashSet
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.T;
        candidate.PrimaryKeybindings["interact"] = (long)Key.T;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.E;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();
        var snapshot = manager.GetSnapshot();

        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.T);
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual(-1L);
        AssertThat(snapshot.PrimaryKeybindings["pause_menu"]).IsEqual((long)Key.E);
    }

    [TestCase]
    public async Task SettingsManager_ApplyAndSave_UnboundActionHasNoEvents()
    {
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();

        // toggle_inventory=T, interact=T (duplicate), pause_menu=E (takes interact's default)
        // → interact resolves to -1 (unbound)
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.T;
        candidate.PrimaryKeybindings["interact"] = (long)Key.T;
        candidate.PrimaryKeybindings["pause_menu"] = (long)Key.E;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();
        var snapshot = manager.GetSnapshot();

        // interact must be unbound
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual(-1L);

        // The "interact" action in the InputMap must have NO events (not a broken -1 event)
        var events = InputMap.ActionGetEvents("interact");
        AssertThat(events.Count).IsEqual(0);
    }

    [TestCase]
    public async Task SettingsManager_InteractUnbound_DefaultAvailable_ForcedBackToDefault()
    {
        // Step 1: Create a conflict that leaves interact=-1.
        var manager = await BootstrapSettingsManager();
        var first = manager.GetSnapshot();
        first.PrimaryKeybindings["toggle_inventory"] = (long)Key.E;
        first.PrimaryKeybindings["interact"] = (long)Key.E;
        AssertThat(manager.ApplyAndSave(first)).IsTrue();
        var snap = manager.GetSnapshot();
        AssertThat(snap.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.E);
        // interact default (E) is taken by toggle_inventory → stays -1
        AssertThat(snap.PrimaryKeybindings["interact"]).IsEqual(-1L);

        // Step 2: Change toggle_inventory away from E but keep interact at -1.
        // On reload, the fallback should recover interact to E (now available).
        var second = manager.GetSnapshot();
        second.PrimaryKeybindings["toggle_inventory"] = (long)Key.I;
        AssertThat(manager.ApplyAndSave(second)).IsTrue();

        var reloadedManager = await RebootSettingsManager();
        var final = reloadedManager.GetSnapshot();
        AssertThat(final.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.I);
        // interact recovered from -1 to its default E because E is no longer taken
        AssertThat(final.PrimaryKeybindings["interact"]).IsEqual((long)Key.E);
        AssertThat(GetPrimaryKey("interact")).IsEqual((long)Key.E);
    }

    [TestCase]
    public async Task SettingsManager_InteractUnbound_DefaultTaken_StaysUnboundAfterReload()
    {
        // interact resolves to -1 because both its key and default are taken.
        // After save/reload, the -1 sentinel should survive normalization (Fix 2)
        // and the fallback should leave it -1 because the default is still taken.
        var manager = await BootstrapSettingsManager();
        var candidate = manager.GetSnapshot();
        candidate.PrimaryKeybindings["toggle_inventory"] = (long)Key.E;
        candidate.PrimaryKeybindings["interact"] = (long)Key.E;

        AssertThat(manager.ApplyAndSave(candidate)).IsTrue();

        var reloadedManager = await RebootSettingsManager();
        var snapshot = reloadedManager.GetSnapshot();

        AssertThat(snapshot.PrimaryKeybindings["toggle_inventory"]).IsEqual((long)Key.E);
        // interact stays -1: default E is taken by toggle_inventory
        AssertThat(snapshot.PrimaryKeybindings["interact"]).IsEqual(-1L);
        // InputMap should have no events for the unbound action
        AssertThat(InputMap.ActionGetEvents("interact").Count).IsEqual(0);
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
        _originalBindings = new Dictionary<string, List<InputEvent>>();
        foreach (var action in ManagedActions)
        {
            _originalBindings[action] = InputMap.HasAction(action)
                ? new List<InputEvent>(InputMap.ActionGetEvents(action))
                : new List<InputEvent>();
        }

        // Also capture ui_cancel since ApplyInputBindings now mirrors pause_menu onto it.
        _originalUiCancelEvents = InputMap.HasAction("ui_cancel")
            ? new List<InputEvent>(InputMap.ActionGetEvents("ui_cancel"))
            : new List<InputEvent>();

        _originalWindowMode = DisplayServer.WindowGetMode();
        _originalWindowSize = DisplayServer.WindowGetSize();
        _originalBusCount = AudioServer.BusCount;
        _originalBusVolumes = new Dictionary<int, float>();
        for (int i = 0; i < _originalBusCount; i++)
        {
            _originalBusVolumes[i] = AudioServer.GetBusVolumeDb(i);
        }
    }

    private void RestoreRuntimeState()
    {
        foreach (var action in ManagedActions)
        {
            if (InputMap.HasAction(action))
            {
                InputMap.EraseAction(action);
            }

            var originalEvents = _originalBindings[action];
            if (originalEvents.Count > 0)
            {
                if (!InputMap.HasAction(action))
                {
                    InputMap.AddAction(action);
                }

                foreach (var evt in originalEvents)
                {
                    InputMap.ActionAddEvent(action, evt);
                }
            }
        }

        // Restore ui_cancel to its original full event list.
        if (_originalUiCancelEvents.Count > 0)
        {
            if (InputMap.HasAction("ui_cancel"))
            {
                InputMap.EraseAction("ui_cancel");
            }

            InputMap.AddAction("ui_cancel");
            foreach (var evt in _originalUiCancelEvents)
            {
                InputMap.ActionAddEvent("ui_cancel", evt);
            }
        }

        while (AudioServer.BusCount > _originalBusCount)
        {
            AudioServer.RemoveBus(AudioServer.BusCount - 1);
        }

        DisplayServer.WindowSetMode(_originalWindowMode);
        DisplayServer.WindowSetSize(_originalWindowSize);

        // Restore all bus volumes (Master, Music, SFX, etc.)
        foreach (var (busIndex, volumeDb) in _originalBusVolumes)
        {
            if (busIndex < AudioServer.BusCount)
            {
                AudioServer.SetBusVolumeDb(busIndex, volumeDb);
            }
        }
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
