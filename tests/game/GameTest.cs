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
    public void PauseMenu_WhenNoPauseMenu_OpensPauseMenuDialog()
    {
        if (_gameManager!.IsInNpcInteraction) _gameManager.EndNpcInteraction();
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
