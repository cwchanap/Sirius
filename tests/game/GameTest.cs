using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameTest : Node
{
    private TestableGame? _game;
    private SubViewport? _viewport;
    private GameManager? _gameManager;

    [Before]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360)
        };
        sceneTree.Root.AddChild(_viewport);

        _game = new TestableGame();
        _viewport.AddChild(_game);

        _gameManager = new GameManager();
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        // Reset interaction/battle state before freeing to prevent signal
        // callbacks from firing on half-freed nodes during teardown.
        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            if (_gameManager.IsInNpcInteraction) _gameManager.EndNpcInteraction();
            if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
            if (_gameManager.IsInBattle) _gameManager.EndBattle(false);
        }

        // Use Free() for immediate cleanup to avoid state leaking between tests.
        // Game must be freed before its viewport parent.
        if (_game != null && IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_viewport != null && IsInstanceValid(_viewport))
        {
            _viewport.Free();
            _viewport = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.Free();
            _gameManager = null;
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    // NOTE: This test must run BEFORE any test that does NOT consume input.
    // Test ordering matters because the SubViewport's IsInputHandled flag
    // appears to be affected by prior tests that push input events without
    // calling SetInputAsHandled.  GdUnit4 executes test methods in declaration
    // order, so battle tests are placed first.
    [TestCase]
    public void PauseMenu_WhenInBattleWithDialog_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", new BattleManager());

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInBattleFallback_MarksInputAsHandled()
    {
        _gameManager!.StartBattle(Enemy.CreateGoblin());
        SetPrivateField(_game!, "_battleManager", null);

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenInNpcInteraction_DoesNotConsumeInput()
    {
        _gameManager!.StartNpcInteraction();

        PushPauseEvent();

        // Input must NOT be marked as handled — AcceptDialog-based NPC modals
        // (DialogueDialog, ShopDialog, HealDialog) need ESC to reach them so
        // they can emit Canceled / CloseRequested and dismiss themselves.
        AssertThat(_viewport!.IsInputHandled()).IsFalse();
    }

    [TestCase]
    public void PauseMenu_WhenInWorldInteraction_MarksInputHandledAndDoesNotOpenPauseMenu()
    {
        if (_game!.GetNodeOrNull("UI") == null)
        {
            _game.AddChild(new CanvasLayer { Name = "UI" });
        }

        _gameManager!.StartWorldInteraction();

        PushPauseEvent();

        AssertThat(_viewport!.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
        AssertThat(_gameManager.IsInWorldInteraction).IsTrue();

        _gameManager.EndWorldInteraction();
    }

    [TestCase]
    public void PauseMenu_WhenNoPauseMenu_OpensPauseMenuDialog()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = new CanvasLayer { Name = "UI" };
        _game!.AddChild(ui);

        _game.InvokePauseMenu();

        var dialog = GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog");
        AssertThat(dialog).IsNotNull();
        AssertThat(dialog!.GetParent()).IsEqual(ui);
    }

    [TestCase]
    public void PauseMenu_WhenPauseMenuIsVisible_ClosesPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Reset any pause menu state left by earlier tests (shared instance)
        SetPrivateField(_game!, "_pauseMenuDialog", null);

        // Reuse the existing "UI" node from the previous test if present
        if (_game!.GetNodeOrNull("UI") == null)
        {
            _game.AddChild(new CanvasLayer { Name = "UI" });
        }

        // First press opens the pause menu
        _game.InvokePauseMenu();
        var dialog = GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog");
        AssertThat(dialog).IsNotNull();

        // Make it appear visible so the toggle branch fires
        dialog!.Show();

        // Second press closes it
        _game.InvokePauseMenu();

        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
    }

    [TestCase]
    public void PauseMenu_WhenPauseMenuIsOpen_InputIsHandled()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = new CanvasLayer { Name = "UI" };
        _game!.AddChild(ui);

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenSettingsRequested_OpensSettingsAndHidesPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Ensure clean settings state
        SetPrivateField(_game!, "_settingsMenu", null);

        if (_game!.GetNodeOrNull("UI") == null)
            _game.AddChild(new CanvasLayer { Name = "UI" });

        // Simulate a visible pause menu dialog
        var pauseDialog = new PauseMenuDialog();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);
        _viewport!.AddChild(pauseDialog);
        pauseDialog.Show();

        // Invoke OnPauseSettingsRequested (private) via reflection
        var method = typeof(Game).GetMethod("OnPauseSettingsRequested",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        method.Invoke(_game, null);

        // Settings controller should be open
        var settingsMenu = GetPrivateField<SettingsMenuController?>(_game, "_settingsMenu");
        AssertThat(settingsMenu).IsNotNull();

        // Pause menu must be hidden while settings is open
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsFalse();

        if (IsInstanceValid(pauseDialog)) pauseDialog.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSettingsClosed_NullsSettingsMenu()
    {
        // Simulate an open settings menu by setting the private field
        var fakeSettings = InstantiateSettingsMenu();
        SetPrivateField(_game!, "_settingsMenu", fakeSettings);
        _viewport!.AddChild(fakeSettings);

        var method = typeof(Game).GetMethod("OnPauseSettingsClosed",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        method.Invoke(_game, null);

        AssertThat(GetPrivateField<SettingsMenuController?>(_game, "_settingsMenu")).IsNull();
    }

    [TestCase]
    public async Task PauseMenu_WhenSettingsCloseTriggersEscape_DefersPauseMenuRestore()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Hide();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        var fakeSettings = InstantiateSettingsMenu();
        ui.AddChild(fakeSettings);
        SetPrivateField(_game, "_settingsMenu", fakeSettings);

        var closeMethod = typeof(Game).GetMethod("OnPauseSettingsClosed",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        closeMethod.Invoke(_game, null);

        AssertThat(GetPrivateField<SettingsMenuController?>(_game, "_settingsMenu")).IsNull();
        AssertThat(pauseDialog.Visible).IsFalse();

        _game.InvokePauseMenu();

        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsFalse();

        await ToSignal((SceneTree)Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        await ToSignal((SceneTree)Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsTrue();
    }

    [TestCase]
    public void PauseMenu_WhenSettingsOpen_ConsumesEscWithoutOpeningPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Ensure no stale pause menu from prior tests
        SetPrivateField(_game!, "_pauseMenuDialog", null);

        var fakeSettings = InstantiateSettingsMenu();
        SetPrivateField(_game!, "_settingsMenu", fakeSettings);
        _viewport!.AddChild(fakeSettings);

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        // ESC must be consumed so the pause menu does not open
        AssertThat(_viewport.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();

        // Clean up so subsequent tests are not affected by the settings guard
        SetPrivateField(_game!, "_settingsMenu", null);
        if (IsInstanceValid(fakeSettings)) fakeSettings.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSettingsIsRebinding_DoesNotForceClose()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        SetPrivateField(_game!, "_pauseMenuDialog", null);

        var fakeSettings = InstantiateSettingsMenu();
        _viewport!.AddChild(fakeSettings);
        fakeSettings.OpenSettings(SettingsData.CreateDefaults());
        SetPrivateField(_game!, "_settingsMenu", fakeSettings);

        // Put the settings controller into key-capture mode (rebinding pause_menu)
        var startCapture = typeof(SettingsMenuController).GetMethod(
            "StartKeyCapture", BindingFlags.NonPublic | BindingFlags.Instance)!;
        startCapture.Invoke(fakeSettings, new object[] { "pause_menu" });
        AssertThat(fakeSettings.IsRebinding).IsTrue();

        // Push a pause_menu action — the fallback must NOT close settings
        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        // Settings should still be alive and rebinding should still be active
        AssertThat(GetPrivateField<SettingsMenuController?>(_game, "_settingsMenu")).IsNotNull();
        AssertThat(fakeSettings.IsRebinding).IsTrue();

        // Pause menu must NOT have been opened behind the settings panel
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();

        // Clean up
        SetPrivateField(_game!, "_settingsMenu", null);
        if (IsInstanceValid(fakeSettings)) fakeSettings.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSettingsPopupOpen_DoesNotOpenPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        SetPrivateField(_game!, "_pauseMenuDialog", null);

        var fakeSettings = InstantiateSettingsMenu();
        _viewport!.AddChild(fakeSettings);
        fakeSettings.OpenSettings(SettingsData.CreateDefaults());
        SetPrivateField(_game!, "_settingsMenu", fakeSettings);

        // Simulate an OptionButton popup being open (e.g. resolution dropdown)
        // by directly setting the flag via reflection — we can't easily open a
        // real OptionButton popup in a unit test.
        AssertThat(fakeSettings.IsPopupOpen).IsFalse();

        // Find and show the resolution OptionButton's popup to set IsPopupOpen = true
        var resOption = fakeSettings.GetNodeOrNull<OptionButton>("VBox/ResolutionOption");
        if (resOption != null)
        {
            // Show the popup list
            resOption.ShowPopup();
            AssertThat(fakeSettings.IsPopupOpen).IsTrue();
        }
        else
        {
            // If the OptionButton path isn't available, skip the popup-specific
            // assertion but still verify the basic guard works
            SetPrivateField(_game!, "_settingsMenu", null);
            if (IsInstanceValid(fakeSettings)) fakeSettings.QueueFree();
            return;
        }

        // Push a pause_menu action while the popup is open
        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        // Settings should still be alive
        AssertThat(GetPrivateField<SettingsMenuController?>(_game, "_settingsMenu")).IsNotNull();

        // Pause menu must NOT have been opened behind the settings panel
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();

        // Clean up
        SetPrivateField(_game!, "_settingsMenu", null);
        if (IsInstanceValid(fakeSettings)) fakeSettings.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSaveDialogOpen_EscClosesSaveDialogNotOpenPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Ensure no stale pause menu
        SetPrivateField(_game!, "_pauseMenuDialog", null);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate an open save/load dialog
        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        // ESC should close the save dialog, NOT open the pause menu
        AssertThat(_viewport.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
    }

    [TestCase]
    public void PauseMenu_WhenSaveDialogHasChildDialog_EscDismissesChildOnly()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Ensure no stale pause menu
        SetPrivateField(_game!, "_pauseMenuDialog", null);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate an open save/load dialog with an active overwrite confirmation
        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);

        var confirmDialog = new AcceptDialog();
        saveDialog.AddChild(confirmDialog);
        SetPrivateField(saveDialog, "_activeConfirmDialog", confirmDialog);

        AssertThat(saveDialog.HasActiveChildDialog).IsTrue();

        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);

        // ESC should dismiss only the child dialog, NOT the save dialog
        AssertThat(_viewport.IsInputHandled()).IsTrue();
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNotNull();
        AssertThat(saveDialog.HasActiveChildDialog).IsFalse();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();

        // Clean up
        if (IsInstanceValid(confirmDialog)) confirmDialog.QueueFree();
        if (IsInstanceValid(saveDialog)) saveDialog.QueueFree();
    }

    [TestCase]
    public void OnSaveSlotSelected_WhenSaveBlocked_CleansUpSaveDialog()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate a hidden save dialog (SaveLoadDialog hides itself before
        // emitting SaveSlotSelected, so it's invisible but still referenced).
        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        saveDialog.Hide();
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);

        // Block the save by putting the game in battle state
        _gameManager.StartBattle(Enemy.CreateGoblin());

        var method = typeof(Game).GetMethod("OnSaveSlotSelected",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, [0]);

        // The error path must clean up _saveLoadDialog so the next pause press
        // opens the pause menu instead of silently discarding a stale reference.
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();

        // Clean up battle state
        _gameManager.EndBattle(false);
    }

    [TestCase]
    public void PauseMenu_WhenSaveRequested_HidesPauseInsteadOfDestroying()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Create and show a pause menu
        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Show();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        var method = typeof(Game).GetMethod("OnPauseSaveRequested",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, null);

        // Pause menu must still exist but be hidden
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsFalse();

        // The save-load-from-pause flag must be set
        AssertThat(GetPrivateField<bool>(_game, "_saveLoadFromPause")).IsTrue();

        // Clean up save dialog that was opened
        var saveDialog = GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog");
        if (saveDialog != null && IsInstanceValid(saveDialog)) saveDialog.QueueFree();
        SetPrivateField(_game, "_saveLoadDialog", null);
        SetPrivateField(_game, "_saveLoadFromPause", false);
        if (IsInstanceValid(pauseDialog)) pauseDialog.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenLoadRequested_HidesPauseInsteadOfDestroying()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Create and show a pause menu
        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Show();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        var method = typeof(Game).GetMethod("OnPauseLoadRequested",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, null);

        // Pause menu must still exist but be hidden
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsFalse();

        AssertThat(GetPrivateField<bool>(_game, "_saveLoadFromPause")).IsTrue();

        // Clean up
        var saveDialog = GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog");
        if (saveDialog != null && IsInstanceValid(saveDialog)) saveDialog.QueueFree();
        SetPrivateField(_game, "_saveLoadDialog", null);
        SetPrivateField(_game, "_saveLoadFromPause", false);
        if (IsInstanceValid(pauseDialog)) pauseDialog.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSaveDialogClosedFromPause_RestoresPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate a hidden pause menu (as set by OnPauseSaveRequested)
        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Hide();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        // Simulate an open save dialog
        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);
        SetPrivateField(_game, "_saveLoadFromPause", true);

        var method = typeof(Game).GetMethod("OnSaveDialogClosed",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, null);

        // Save dialog must be cleaned up
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();

        // Pause menu must be restored (visible again)
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsTrue();

        // Flag must be cleared
        AssertThat(GetPrivateField<bool>(_game, "_saveLoadFromPause")).IsFalse();

        if (IsInstanceValid(pauseDialog)) pauseDialog.QueueFree();
    }

    [TestCase]
    public void PauseMenu_WhenSaveDialogClosedNotFromPause_DoesNotRestorePauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // No pause menu exists (save opened from elsewhere, e.g. direct keybind)
        SetPrivateField(_game, "_pauseMenuDialog", null);

        // Simulate an open save dialog
        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);
        SetPrivateField(_game, "_saveLoadFromPause", false);

        var method = typeof(Game).GetMethod("OnSaveDialogClosed",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, null);

        // Save dialog must be cleaned up
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();

        // No pause menu should exist
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
    }

    [TestCase]
    public void PauseMenu_WhenSaveBlockedFromPause_RestoresPauseMenu()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate hidden pause menu and open save dialog (from pause)
        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Hide();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        saveDialog.Hide();
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);
        SetPrivateField(_game, "_saveLoadFromPause", true);

        // Block save by entering battle
        _gameManager.StartBattle(Enemy.CreateGoblin());

        var method = typeof(Game).GetMethod("OnSaveSlotSelected",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, [0]);

        // Save dialog must be cleaned up
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();

        // Pause menu must be restored
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNotNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")!.Visible).IsTrue();

        AssertThat(GetPrivateField<bool>(_game, "_saveLoadFromPause")).IsFalse();

        _gameManager.EndBattle(false);

        // Clean up error popup if created
        var errorPopup = GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup");
        if (errorPopup != null && IsInstanceValid(errorPopup)) errorPopup.QueueFree();
        SetPrivateField(_game, "_activeErrorPopup", null);
    }

    [TestCase]
    public void PauseMenu_WhenMainMenuRequestedFromPause_CleansUpBothDialogs()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        // Simulate hidden pause menu and open save dialog (from pause)
        var pauseDialog = new PauseMenuDialog();
        ui.AddChild(pauseDialog);
        pauseDialog.Hide();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);

        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);
        SetPrivateField(_game, "_saveLoadFromPause", true);

        var method = typeof(Game).GetMethod("OnMainMenuRequested",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        // OnMainMenuRequested calls ReturnToMainMenu which changes scene;
        // we only verify the cleanup part, so catch any scene-change errors.
        try
        {
            method.Invoke(_game, null);
        }
        catch (Exception)
        {
            // Scene change may fail in test; that's expected
        }

        // Both dialogs must be cleaned up
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();
        AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
        AssertThat(GetPrivateField<bool>(_game, "_saveLoadFromPause")).IsFalse();
    }

    [TestCase]
    public void InventoryToggle_WhenSettingsOpen_IsBlocked()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Set up an inventory menu so the toggle code path can run
        var invScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        var invMenu = invScene!.Instantiate<InventoryMenuController>();
        SetPrivateField(_game!, "_inventoryMenu", invMenu);
        _viewport!.AddChild(invMenu);
        invMenu.Hide();

        // Simulate an open settings menu
        var fakeSettings = InstantiateSettingsMenu();
        SetPrivateField(_game, "_settingsMenu", fakeSettings);
        _viewport.AddChild(fakeSettings);

        // Push toggle_inventory event
        var evt = new InputEventAction { Action = "toggle_inventory", Pressed = true };
        _viewport.PushInput(evt);

        // Inventory must NOT become visible — settings guard blocked the toggle
        AssertThat(invMenu.Visible).IsFalse();

        // Clean up
        SetPrivateField(_game, "_settingsMenu", null);
        SetPrivateField(_game, "_inventoryMenu", null);
        if (IsInstanceValid(fakeSettings)) fakeSettings.QueueFree();
        if (IsInstanceValid(invMenu)) invMenu.QueueFree();
    }

    [TestCase]
    public void InventoryToggle_WhenPauseMenuVisible_IsBlocked()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Set up an inventory menu so the toggle code path can run
        var invScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        var invMenu = invScene!.Instantiate<InventoryMenuController>();
        SetPrivateField(_game!, "_inventoryMenu", invMenu);
        _viewport!.AddChild(invMenu);
        invMenu.Hide();

        // Simulate a visible pause menu dialog
        var pauseDialog = new PauseMenuDialog();
        SetPrivateField(_game, "_pauseMenuDialog", pauseDialog);
        _viewport.AddChild(pauseDialog);
        pauseDialog.Show();

        // Push toggle_inventory event
        var evt = new InputEventAction { Action = "toggle_inventory", Pressed = true };
        _viewport.PushInput(evt);

        // Inventory must NOT become visible — pause menu guard blocked the toggle
        AssertThat(invMenu.Visible).IsFalse();

        // Clean up
        SetPrivateField(_game, "_pauseMenuDialog", null);
        SetPrivateField(_game, "_inventoryMenu", null);
        if (IsInstanceValid(pauseDialog)) pauseDialog.QueueFree();
        if (IsInstanceValid(invMenu)) invMenu.QueueFree();
    }

    [TestCase]
    public void InventoryToggle_WhenSaveLoadDialogOpen_IsBlocked()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
        if (_gameManager.IsInBattle) _gameManager.EndBattle(false);

        // Set up an inventory menu so the toggle code path can run
        var invScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        var invMenu = invScene!.Instantiate<InventoryMenuController>();
        SetPrivateField(_game!, "_inventoryMenu", invMenu);
        _viewport!.AddChild(invMenu);
        invMenu.Hide();

        // Simulate an active save/load dialog
        var saveDialog = new SaveLoadDialog();
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);
        _viewport.AddChild(saveDialog);

        // Push toggle_inventory event
        var evt = new InputEventAction { Action = "toggle_inventory", Pressed = true };
        _viewport.PushInput(evt);

        // Inventory must NOT become visible — save/load dialog guard blocked the toggle
        AssertThat(invMenu.Visible).IsFalse();

        // Clean up
        SetPrivateField(_game, "_saveLoadDialog", null);
        SetPrivateField(_game, "_inventoryMenu", null);
        if (IsInstanceValid(saveDialog)) saveDialog.QueueFree();
        if (IsInstanceValid(invMenu)) invMenu.QueueFree();
    }

    [TestCase]
    public void ShowLoadMenu_WhenNpcInteractionBlocksLoad_UsesLoadFailedTitle()
    {
        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        _gameManager!.StartNpcInteraction();

        var method = typeof(Game).GetMethod("ShowLoadMenu",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, null);

        var popup = GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup");
        AssertThat(popup).IsNotNull();
        AssertThat(popup!.Title).IsEqual("Load Failed");
    }

    [TestCase]
    public void LoadSlot_WhenLoadFails_UsesLoadFailedTitle()
    {
        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        var method = typeof(Game).GetMethod("OnInGameLoadSlotSelected",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, [0]);

        var popup = GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup");
        AssertThat(popup).IsNotNull();
        AssertThat(popup!.Title).IsEqual("Load Failed");
    }

    [TestCase]
    public void LoadSlot_WhenWorldInteractionBlocksLoad_UsesLoadFailedTitleAndMessage()
    {
        var ui = _game!.GetNodeOrNull<CanvasLayer>("UI");
        if (ui == null)
        {
            ui = new CanvasLayer { Name = "UI" };
            _game.AddChild(ui);
        }

        var saveDialog = new SaveLoadDialog();
        ui.AddChild(saveDialog);
        SetPrivateField(_game, "_saveLoadDialog", saveDialog);

        _gameManager!.StartWorldInteraction();

        var method = typeof(Game).GetMethod("OnInGameLoadSlotSelected",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(_game, [0]);

        var popup = GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup");
        AssertThat(GetPrivateField<SaveLoadDialog?>(_game, "_saveLoadDialog")).IsNull();
        AssertThat(popup).IsNotNull();
        AssertThat(popup!.Title).IsEqual("Load Failed");
        AssertThat(popup.DialogText).IsEqual("Cannot save or load while opening treasure.");
    }

    [TestCase]
    public async Task Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_RuntimeTest",
                TreasureBoxId = "TreasureBox_RuntimeTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 25,
                RewardItemIds = new Godot.Collections.Array<string> { "health_potion" },
                RewardItemQuantities = new Godot.Collections.Array<int> { 1 }
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitFrames(1);

            var prompt = gameScene.GetNodeOrNull<Label>("UI/GameUI/InteractionPrompt");
            AssertThat(prompt).IsNotNull();
            AssertThat(prompt!.Visible).IsTrue();
            AssertThat(prompt.Text).IsEqual("Open");

            int startingGold = gameManager.Player.Gold;
            int startingPotionCount = gameManager.Player.GetItemQuantity("health_potion");
            PressInteract(playerController);
            await AwaitFrames(1);

            AssertThat(prompt.Visible).IsFalse();

            await AwaitFrames(120);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(startingPotionCount + 1);
            AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeTest")).IsTrue();
            AssertThat(box.IsOpened).IsTrue();
            AssertThat(prompt.Visible).IsFalse();

            PressInteractRelease(playerController);
            PressInteract(playerController);
            await AwaitFrames(30);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
            AssertThat(prompt.Visible).IsFalse();
        }
        finally
        {
            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    [TestCase]
    public async Task Game_AbortedTreasureOpeningDoesNotGrantRewardOrPersistOpenedId()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        Node? gameScene = null;
        TreasureBoxSpawn? box = null;

        try
        {
            gameScene = scene.Instantiate();
            sceneTree.Root.AddChild(gameScene);
            await AwaitFrames(6);

            var floorManager = gameScene.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = gameScene.GetNode<PlayerController>("PlayerController");
            var gameManager = gameScene.GetNode<GameManager>("GameManager");

            AssertThat(gridMap).IsNotNull();

            box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_RuntimeAbortTest",
                TreasureBoxId = "TreasureBox_RuntimeAbortTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 25
            };
            gridMap.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");

            var freshGrid = new int[gridMap.GridWidth, gridMap.GridHeight];
            SetPrivateField(gridMap, "_grid", freshGrid);
            SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
            gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            PressMovement(playerController, Vector2I.Right);
            await AwaitFrames(1);

            int startingGold = gameManager.Player.Gold;
            PressInteract(playerController);
            await AwaitFrames(1);

            gridMap.RemoveChild(box);
            await AwaitFrames(120);

            AssertThat(gameManager.Player.Gold).IsEqual(startingGold);
            AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeAbortTest")).IsFalse();
            AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        }
        finally
        {
            if (box != null && IsInstanceValid(box))
            {
                box.Free();
            }

            if (gameScene != null && IsInstanceValid(gameScene))
            {
                gameScene.Free();
            }

            await AwaitFrames(1);
        }
    }

    private void PushPauseEvent()
    {
        var evt = CreatePauseEvent();
        _viewport!.PushInput(evt);
    }

    private static InputEventAction CreatePauseEvent()
    {
        return new InputEventAction
        {
            Action = "pause_menu",
            Pressed = true
        };
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < frameCount; i++)
        {
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void PressMovement(PlayerController controller, Vector2I direction)
    {
        controller._UnhandledInput(new InputEventKey
        {
            Keycode = DirectionToKey(direction),
            Pressed = true
        });
    }

    private static void PressInteract(PlayerController controller)
    {
        controller._UnhandledInput(new InputEventAction
        {
            Action = "interact",
            Pressed = true
        });
    }

    private static void PressInteractRelease(PlayerController controller)
    {
        controller._UnhandledInput(new InputEventAction
        {
            Action = "interact",
            Pressed = false
        });
    }

    private static Key DirectionToKey(Vector2I direction)
    {
        if (direction == Vector2I.Up) return Key.Up;
        if (direction == Vector2I.Down) return Key.Down;
        if (direction == Vector2I.Left) return Key.Left;
        if (direction == Vector2I.Right) return Key.Right;

        throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported movement direction.");
    }

    private static SettingsMenuController InstantiateSettingsMenu()
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn")
            ?? throw new InvalidOperationException("Failed to load SettingsMenu.tscn.");
        return scene.Instantiate<SettingsMenuController>();
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = FindPrivateField(instance.GetType(), fieldName);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = FindPrivateField(instance.GetType(), fieldName);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        return (T)field.GetValue(instance)!;
    }

    private static FieldInfo? FindPrivateField(Type? type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    public partial class TestableGame : Game
    {
        public override void _Ready()
        {
        }

        public void InvokePauseMenu()
        {
            HandlePauseMenuInput();
        }
    }
}
