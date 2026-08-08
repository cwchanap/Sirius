using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameplayPauseHostTest : Node
{
    private SubViewport? _viewport;
    private Game? _game;
    private bool _incomingTreePaused;
    private Input.MouseModeEnum _originalMouseMode;

    [BeforeTest]
    public async Task SetUp()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        _incomingTreePaused = tree.Paused;
        _originalMouseMode = Input.MouseMode;
        tree.Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360),
            GuiEmbedSubwindows = true
        };
        tree.Root.AddChild(_viewport);

        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        _game = scene.Instantiate<Game>();
        _viewport.AddChild(_game);
        await AwaitFrames(8);
    }

    [AfterTest]
    public async Task TearDown()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = false;

        if (_game != null && GodotObject.IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
        {
            _viewport.Free();
            _viewport = null;
        }

        await AwaitFrames(2);
        Input.MouseMode = _originalMouseMode;
        tree.Paused = _incomingTreePaused;
    }

    [TestCase]
    public void GameSceneOwnsOneReadyHost()
    {
        var hosts = new List<UIScreenHost>();
        foreach (var child in _game!.GetNode<CanvasLayer>("UI").GetChildren())
        {
            if (child is UIScreenHost host)
                hosts.Add(host);
        }

        AssertThat(hosts.Count).IsEqual(1);
        AssertThat(hosts[0].ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public void GameSceneHostPrepareForTeardown_ClosesEntryAndRestoresIncomingState()
    {
        var host = _game!.GetNodeOrNull<UIScreenHost>("UI/UIScreenHost");
        if (host == null)
        {
            AssertThat(host).IsNotNull();
            return;
        }

        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var incomingHudVisible = gameUi.Visible;
        var incomingMouseMode = Input.MouseMode;
        var disposableEntry = new Control();
        var opened = host.TryPresent(disposableEntry, new UIScreenEntrySpec
        {
            Kind = new StringName("gameplay_pause_host_teardown_test"),
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            NodeLifetime = UINodeLifetime.QueueFree
        });

        AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(gameUi.Visible).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);

        var preparation = host.PrepareForTeardown();

        AssertThat(preparation).IsEqual(UIScreenTeardownPreparationStatus.Complete);
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(gameUi.Visible).IsEqual(incomingHudVisible);
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
    }

    [TestCase]
    public void RuntimeGridMapDoesNotRemainExplicitAlways()
    {
        var grid = _game!.GetNode<FloorManager>("FloorManager").CurrentGridMap;

        AssertThat(grid.ProcessMode).IsNotEqual(Node.ProcessModeEnum.Always);
    }

    [TestCase]
    public async Task DirectInventory_HostsDetachesAndReusesTheExternalView()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(tree.Paused).IsFalse();

        _viewport!.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
        AssertThat(inventory.GetParent()).IsEqual(modalLayer);
        AssertThat(inventory.Visible).IsTrue();
        AssertThat(tree.Paused).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);

        var entry = host.ActiveEntries[0];
        AssertThat(entry.Policy.Parent).IsNull();
        AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(entry.Policy.PauseTree).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsFalse();
        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(tree.Paused).IsFalse();

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
        AssertThat(inventory.GetParent()).IsEqual(modalLayer);
        AssertThat(inventory.Visible).IsTrue();

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(inventory.GetParent()).IsNull();
    }

    [TestCase]
    public async Task RootPauseOwnsTreePause_AndResumeRestoresIncomingState()
    {
        // This catches a hosted Pause path that fails to take tree-pause ownership,
        // skips the host policy effects, or leaves its view/entry behind after Resume.
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var incomingHudVisible = gameUi.Visible;
        var incomingMouseMode = Input.MouseMode;

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(pause.GetParent()).IsEqual(modalLayer);
        AssertThat(tree.Paused).IsTrue();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);
        AssertThat(gameUi.Visible).IsTrue();

        var entry = host.ActiveEntries[0];
        AssertThat(entry.Policy.Kind).IsEqual(UIScreenKinds.Pause);
        AssertThat(entry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(entry.Policy.PauseTree).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Visible);
        AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
        AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(GetPrivateField<PauseScreenController>(_game, "_pauseScreen"))
            .IsEqual(pause);

        pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
        AssertThat(gameUi.Visible).IsEqual(incomingHudVisible);
        AssertThat(GetPrivateField<PauseScreenController?>(_game, "_pauseScreen")).IsNull();
        AssertThat(GodotObject.IsInstanceValid(pause)).IsFalse();
    }

    [TestCase]
    public async Task RootPause_FreedGameplayFocusTargetCompletesRestorationWithoutLease()
    {
        // A stale focus record must not throw while Pause closes or strand the
        // host in its temporary cancel-blocking restoration state.
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var gameplayFocusTarget = new Control
        {
            Name = "FreedGameplayFocusTarget",
            FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = Vector2.One
        };
        gameUi.AddChild(gameplayFocusTarget);
        await AwaitFrames(1);

        gameplayFocusTarget.GrabFocus();
        AssertThat(_viewport!.GuiGetFocusOwner()).IsEqual(gameplayFocusTarget);

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        gameplayFocusTarget.Free();
        AssertThat(GodotObject.IsInstanceValid(gameplayFocusTarget)).IsFalse();

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsFocusRestorationPending).IsFalse();
        AssertThat(host.Diagnostics.RestorationLease).IsNull();
        AssertThat(host.Diagnostics.StateLeases.IncomingPaused).IsNull();
        AssertThat(tree.Paused).IsFalse();
    }

    [TestCase]
    public async Task PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var inventoryButton = pause.GetNode<Button>("%InventoryButton");
        inventoryButton.GrabFocus();
        inventoryButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
        var inventoryEntry = FindEntry(host, UIScreenKinds.Inventory);
        AssertThat(inventory.GetParent()).IsEqual(modalLayer);
        AssertThat(inventoryEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(inventoryEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(inventoryEntry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(inventoryEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(inventoryEntry.Policy.PauseTree).IsFalse();
        AssertThat(inventoryEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(inventoryEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(inventoryEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);
        AssertThat(tree.Paused).IsTrue();

        _viewport!.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(3);

        AssertThat(inventory.GetParent()).IsNull();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, inventoryButton);
    }

    [TestCase]
    public async Task HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var settingsButton = pause.GetNode<Button>("%SettingsButton");
        settingsButton.GrabFocus();
        settingsButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(modalLayer);
        var settingsEntry = FindEntry(host, UIScreenKinds.Settings);
        AssertThat(settings.GetParent()).IsEqual(modalLayer);
        AssertThat(settingsEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(settingsEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(settingsEntry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(settingsEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(settingsEntry.Policy.PauseTree).IsFalse();
        AssertThat(settingsEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(settingsEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(settingsEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        InvokePrivateVoid(settings, "OnCancelPressed");
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(settings)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, settingsButton);
    }

    [TestCase]
    public async Task HostPrepareForTeardown_WithHostedSettingsClosesDescendantsAndRestoresLeases()
    {
        // Teardown must cascade Pause's hosted child, release its root pause
        // ownership, and leave no lease behind for a later scene replacement.
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var incomingHudVisible = gameUi.Visible;
        var incomingMouseMode = Input.MouseMode;

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(host.GetNode<Control>("ModalLayer"));
        AssertThat(host.ActiveEntries.Count).IsEqual(2);
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
        AssertThat(tree.Paused).IsTrue();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);

        var preparation = host.PrepareForTeardown();
        await AwaitFrames(3);

        AssertThat(preparation).IsEqual(UIScreenTeardownPreparationStatus.Complete);
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(host.CurrentState.IsFocusRestorationPending).IsFalse();
        AssertThat(host.Diagnostics.RestorationLease).IsNull();
        AssertThat(host.Diagnostics.StateLeases.IncomingPaused).IsNull();
        AssertThat(host.Diagnostics.StateLeases.IncomingCursorMode).IsNull();
        AssertThat(host.Diagnostics.StateLeases.IncomingHudVisible).IsNull();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
        AssertThat(gameUi.Visible).IsEqual(incomingHudVisible);
        AssertThat(GetPrivateField<PauseScreenController?>(_game, "_pauseScreen")).IsNull();
        AssertThat(GetPrivateField<SettingsMenuController?>(_game, "_hostedSettingsMenu")).IsNull();
        AssertThat(GodotObject.IsInstanceValid(pause)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(settings)).IsFalse();
    }

    [TestCase]
    public async Task HostedSaveLoad_SaveAndLoadHostLogicalPauseChildrenAndRestoreExistingPause()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var saveButton = pause.GetNode<Button>("%SaveButton");
        saveButton.GrabFocus();
        saveButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveDialog = FindDirectChild<SaveLoadDialog>(host);
        var saveEntry = FindEntry(host, UIScreenKinds.SaveLoad);
        AssertThat(saveDialog.GetParent()).IsEqual(host);
        AssertThat(saveDialog.Title).IsEqual("Save Game");
        AssertThat(saveEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(saveEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(saveEntry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(saveEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(saveEntry.Policy.PauseTree).IsFalse();
        AssertThat(saveEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(saveEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(saveEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        GetPrivateField<Button>(saveDialog, "_cancelButton")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(saveDialog)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, saveButton);

        var loadButton = pause.GetNode<Button>("%LoadButton");
        loadButton.GrabFocus();
        loadButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadDialog = FindDirectChild<SaveLoadDialog>(host);
        var loadEntry = FindEntry(host, UIScreenKinds.SaveLoad);
        AssertThat(loadDialog.GetParent()).IsEqual(host);
        AssertThat(loadDialog.Title).IsEqual("Load Game");
        AssertThat(loadEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(loadEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(loadEntry.Policy.PauseTree).IsFalse();
        AssertThat(loadEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(loadEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);

        GetPrivateField<Button>(loadDialog, "_cancelButton")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(loadDialog)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, loadButton);
    }

    [TestCase]
    public async Task PauseChildReturnConfirmation_CancelClosesOnlyTheHostedChild()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var returnButton = pause.GetNode<Button>("%ReturnToTitleButton");
        returnButton.GrabFocus();
        returnButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var confirmation = FindDirectChild<PauseReturnToTitleConfirmationController>(modalLayer);
        var confirmationEntry = FindEntry(host, UIScreenKinds.ConfirmQuitToMain);
        AssertThat(confirmation.GetParent()).IsEqual(modalLayer);
        AssertThat(confirmationEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(confirmationEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(confirmationEntry.Policy.InputPriority).IsEqual(UIInputPriority.Blocking);
        AssertThat(confirmationEntry.Policy.ExclusiveGroup)
            .IsEqual(UIScreenExclusiveGroups.BlockingPrompt);
        AssertThat(confirmationEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(confirmationEntry.Policy.PauseTree).IsFalse();
        AssertThat(confirmationEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(confirmationEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(confirmationEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        confirmation.GetNode<Button>("%CancelButton")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(confirmation)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, returnButton);
    }

    [TestCase]
    public async Task PauseChildReturnConfirmation_ConfirmRoutesThroughReturnToMainMenuOnSinglePress()
    {
        var navigationGame = await CreateReturnTrackingGame();
        var navigationRequests = 0;
        navigationGame.MainMenuNavigationRequested = () => navigationRequests++;
        try
        {
            var host = navigationGame.GetNode<UIScreenHost>("UI/UIScreenHost");

            AssertThat(InvokePrivateBool(navigationGame, "TryOpenPause")).IsTrue();
            await AwaitFrames(2);

            var pause = GetPrivateField<PauseScreenController>(
                navigationGame,
                "_pauseScreen");
            pause.GetNode<Button>("%ReturnToTitleButton")
                .EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            var confirmation = FindDirectChild<PauseReturnToTitleConfirmationController>(
                host.GetNode<Control>("ModalLayer"));
            var confirmButton = confirmation.GetNode<Button>("%ReturnToTitleButton");

            confirmButton.EmitSignal(Button.SignalName.Pressed);

            AssertThat(navigationRequests).IsEqual(1);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(navigationGame))
                navigationGame.Free();

            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task PauseChildInventory_ToggleInventoryClosesOnlyTheHostedChild()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var inventoryButton = pause.GetNode<Button>("%InventoryButton");
        inventoryButton.GrabFocus();
        inventoryButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
        _viewport!.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(3);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsFalse();
        AssertHostedChildReturnedToSamePause(
            host,
            pause,
            pauseEntry.Handle,
            inventoryButton);
    }

    [TestCase]
    public async Task HostedSaveLoad_ActiveOverwriteChildConsumesCancelBeforeSaveLoad()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveDialog = FindDirectChild<SaveLoadDialog>(host);
        var slotInfos = GetPrivateField<SaveSlotInfo[]>(saveDialog, "_slotInfos");
        slotInfos[0] = new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 };
        InvokePrivateVoid(saveDialog, "OnSlotPressed", 0);
        await AwaitFrames(1);

        AssertThat(saveDialog.HasActiveChildDialog).IsTrue();
        var handled = host.TryHandleInput(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(handled).IsEqual(UIInputDispatchResult.Consumed);
        AssertThat(saveDialog.HasActiveChildDialog).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertHostedChildRemainsAboveSamePause(host, pause, pauseEntry.Handle);
    }

    [TestCase]
    public async Task HostedSettings_RebindingAndPopupReserveCancelForNativeHandlers()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(host.GetNode<Control>("ModalLayer"));
        GetPrivateField<Button>(settings, "_inventoryKeyBtn")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(settings.IsRebinding).IsTrue();
        var rebindCancel = new InputEventAction { Action = "ui_cancel", Pressed = true };
        AssertThat(host.TryHandleInput(rebindCancel))
            .IsEqual(UIInputDispatchResult.ReservedForTopEntry);
        settings._Input(rebindCancel);
        AssertThat(settings.IsRebinding).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();

        var resolution = GetPrivateField<OptionButton>(settings, "_resolutionOption");
        resolution.ShowPopup();
        await AwaitFrames(1);

        AssertThat(settings.IsPopupOpen).IsTrue();
        AssertThat(host.TryHandleInput(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        })).IsEqual(UIInputDispatchResult.ReservedForTopEntry);
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
        AssertHostedChildRemainsAboveSamePause(host, pause, pauseEntry.Handle);
        resolution.GetPopup().Hide();
    }

    [TestCase]
    public async Task RootCancel_PauseMenuActionOpensHostedPauseWhenUnblocked()
    {
        await AssertRootCancelOpensHostedPause("pause_menu");
    }

    [TestCase]
    public async Task RootCancel_UiCancelActionOpensHostedPauseWhenUnblocked()
    {
        await AssertRootCancelOpensHostedPause("ui_cancel");
    }

    [TestCase]
    public async Task GameplayProbe_FreezesDuringRootPauseAndResumesAfterConfiguredPauseAction()
    {
        // A root Pause policy that does not own tree pause, or a gameplay
        // parent that runs while paused, makes this real GridMap descendant
        // continue processing during the hosted pause presentation.
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var grid = _game.GetNode<FloorManager>("FloorManager").CurrentGridMap;
        var probe = new PausableProbe();
        grid.AddChild(probe);
        await AwaitFrames(3);

        AssertThat(probe.ProcessCount).IsGreater(0);
        var beforePause = probe.ProcessCount;

        PushAction("pause_menu");
        await AwaitFrames(3);

        AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
        AssertThat(tree.Paused).IsTrue();
        AssertThat(probe.ProcessCount).IsEqual(beforePause);

        var stablePausedCount = probe.ProcessCount;
        await AwaitFrames(3);
        AssertThat(probe.ProcessCount).IsEqual(stablePausedCount);

        PushAction("pause_menu");
        await AwaitFrames(3);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(probe.ProcessCount).IsGreater(stablePausedCount);
    }

    private async Task AssertRootCancelOpensHostedPause(StringName action)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

        _viewport!.PushInput(new InputEventAction { Action = action, Pressed = true });
        await AwaitFrames(2);

        AssertThat(_viewport.IsInputHandled()).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
        AssertThat(tree.Paused).IsTrue();
        AssertThat(FindEntry(host, UIScreenKinds.Pause).Policy.ProcessPolicy)
            .IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(FindEntry(host, UIScreenKinds.Pause).Policy.PauseTree).IsTrue();
    }

    private void PushAction(StringName action)
    {
        _viewport!.PushInput(new InputEventAction
        {
            Action = action,
            Pressed = true
        });
    }

    private void AssertHostedChildReturnedToSamePause(
        UIScreenHost host,
        PauseScreenController pause,
        UIScreenHandle pauseHandle,
        Control expectedFocus)
    {
        AssertHostedChildRemainsAboveSamePause(host, pause, pauseHandle);
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(_viewport!.GuiGetFocusOwner()).IsEqual(expectedFocus);
    }

    private void AssertHostedChildRemainsAboveSamePause(
        UIScreenHost host,
        PauseScreenController pause,
        UIScreenHandle pauseHandle)
    {
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(FindEntry(host, UIScreenKinds.Pause).Handle).IsEqual(pauseHandle);
        AssertThat(GetPrivateField<PauseScreenController>(_game!, "_pauseScreen")).IsEqual(pause);
    }

    private async Task<ReturnTrackingGame> CreateReturnTrackingGame()
    {
        var hostScene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var game = new ReturnTrackingGame();
        var ui = new CanvasLayer { Name = "UI" };
        ui.AddChild(new Control { Name = "GameUI" });
        ui.AddChild(hostScene.Instantiate<UIScreenHost>());
        game.AddChild(ui);
        _viewport!.AddChild(game);
        await AwaitFrames(2);
        return game;
    }

    private static UIScreenEntrySnapshot FindEntry(UIScreenHost host, StringName kind)
    {
        foreach (var entry in host.ActiveEntries)
        {
            if (entry.Policy.Kind == kind)
                return entry;
        }

        throw new InvalidOperationException($"Active entry '{kind}' was not found.");
    }

    private static T FindDirectChild<T>(Node parent) where T : Node
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T typed)
                return typed;
        }

        throw new InvalidOperationException($"Direct child '{typeof(T).Name}' was not found.");
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (var index = 0; index < frameCount; index++)
            await tree.Root.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = ResolvePrivateMember(
            instance.GetType(),
            (type, flags) => type.GetField(fieldName, flags));
        if (field == null)
            throw new MissingFieldException(instance.GetType().FullName, fieldName);

        return (T)field.GetValue(instance)!;
    }

    private static void InvokePrivateVoid(
        object instance,
        string methodName,
        params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);

        method.Invoke(instance, arguments);
    }

    private static bool InvokePrivateBool(object instance, string methodName)
    {
        var method = ResolvePrivateMember(
            instance.GetType(),
            (type, flags) => type.GetMethod(methodName, flags));
        if (method == null)
            throw new MissingMethodException(instance.GetType().FullName, methodName);

        if (method.Invoke(instance, null) is bool result)
            return result;

        throw new InvalidOperationException($"Method '{methodName}' did not return bool.");
    }

    // Walks the instance type's base-type chain so private members declared on
    // a base class (e.g. Game fields reached through a ReturnTrackingGame
    // subclass) are resolved without a Game-specific overload. DeclaredOnly
    // ensures each level contributes only its own members.
    private static TMember? ResolvePrivateMember<TMember>(
        Type type,
        Func<Type, BindingFlags, TMember?> select)
        where TMember : class
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var member = select(
                current,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (member != null)
                return member;
        }
        return null;
    }

    private sealed partial class PausableProbe : Node
    {
        public int ProcessCount { get; private set; }

        public override void _Process(double delta) => ProcessCount++;
    }

    private partial class ReturnTrackingGame : Game
    {
        public Action? MainMenuNavigationRequested { get; set; }

        protected override void ReturnToMainMenu() => MainMenuNavigationRequested?.Invoke();

        public override void _Ready()
        {
        }
    }
}
