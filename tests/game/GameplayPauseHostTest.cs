using System;
using System.Collections.Generic;
using System.Linq;
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
    private TestHelpers.SaveFileSnapshot[] _saveFiles = null!;
    private SaveData? _incomingPendingLoadData;

    [BeforeTest]
    public async Task SetUp()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        _incomingTreePaused = tree.Paused;
        _originalMouseMode = Input.MouseMode;
        _saveFiles = TestHelpers.CaptureSaveFiles();
        _incomingPendingLoadData = SaveManager.Instance?.PendingLoadData;
        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = null;
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
        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = _incomingPendingLoadData;
        TestHelpers.RestoreSaveFiles(_saveFiles);
        TestHelpers.ReportSaveFileMismatches(_saveFiles, nameof(GameplayPauseHostTest));
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
    public async Task Battle_HostsAsBlockingScreenWithoutPausingTree()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameUi = _game.GetNode<Control>("UI/GameUI");
        var gameManager = _game.GetNode<GameManager>("GameManager");

        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);

        var battle = GetPrivateField<BattleManager>(_game, "_battleManager");
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        var entry = FindEntry(host, UIScreenKinds.Battle);
        AssertThat(battle.GetParent()).IsEqual(host.GetNode<Control>("ScreenLayer"));
        AssertThat(tree.Paused).IsFalse();
        AssertThat(entry.Policy.PauseTree).IsFalse();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
        AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(gameUi.Visible).IsFalse();

        var close = host.TryClose(entry.Handle, UIScreenCloseReason.ExplicitAction);
        AssertThat(close.Status).IsEqual(UIScreenCloseStatus.Closed);
        if (gameManager.IsInBattle)
            gameManager.EndBattle(false);
    }

    [TestCase]
    public async Task BattleVictory_RemainsHostedAfterBattleFinishedUntilDismissal()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameManager = _game.GetNode<GameManager>("GameManager");

        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);
        var battle = GetPrivateField<BattleManager>(_game, "_battleManager");
        InvokePrivateVoid(battle, "EndBattle", true);
        await AwaitFrames(2);

        AssertThat(gameManager.IsInBattle).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(battle.Visible).IsTrue();
        AssertThat(tree.Paused).IsFalse();

        var close = host.TryClose(
            FindEntry(host, UIScreenKinds.Battle).Handle,
            UIScreenCloseReason.ExplicitAction);
        AssertThat(close.Status).IsEqual(UIScreenCloseStatus.Closed);
    }

    [TestCase]
    public async Task BattleDefeat_ResultCancelLeavesBattleHostedAndBlockedUntilTeardown()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameManager = _game.GetNode<GameManager>("GameManager");

        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);
        var battle = GetPrivateField<BattleManager>(_game, "_battleManager");
        InvokePrivateVoid(battle, "EndBattle", false);
        await AwaitFrames(2);

        AssertThat(gameManager.IsInBattle).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(tree.Paused).IsFalse();

        battle.RequestCancel();
        await AwaitFrames(1);

        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(battle.Visible).IsTrue();
    }

    [TestCase]
    public async Task BattleDefeat_ContinueLeavesBattleHostedAndBlockedUntilTeardown()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var gameManager = _game.GetNode<GameManager>("GameManager");

        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);
        var battle = GetPrivateField<BattleManager>(_game, "_battleManager");
        InvokePrivateVoid(battle, "EndBattle", false);
        await AwaitFrames(2);

        var continueButton = battle.GetNode<Button>("%ContinueButton");
        continueButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(battle.Visible).IsTrue();
        AssertThat(continueButton.Visible).IsFalse();
        AssertThat(continueButton.Disabled).IsTrue();
        AssertThat(tree.Paused).IsFalse();
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
        var gameUi = _game.GetNode<Control>("UI/GameUI");
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

        var entry = FindEntry(host, UIScreenKinds.Inventory);
        AssertThat(entry.Policy.Parent).IsNull();
        AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.WhenPaused);
        AssertThat(entry.Policy.PauseTree).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
        AssertThat(gameUi.Visible).IsFalse();

        var focus = _viewport!.GuiGetFocusOwner();
        AssertThat(focus).IsNotNull();
        AssertThat(focus).IsEqual(inventory.InitialFocusTarget);
        AssertThat(focus).IsNotEqual(inventory.GetNode<Button>("%CloseButton"));

        _viewport.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsFalse();
        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(gameUi.Visible).IsTrue();

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
    public async Task RootPauseGameplayBlock_SuppressesAndRestoresInteractionPrompt()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var floorManager = _game!.GetNode<FloorManager>("FloorManager");
        var gridMap = floorManager.CurrentGridMap;
        var playerController = _game.GetNode<PlayerController>("PlayerController");
        var box = new TreasureBoxSpawn
        {
            Name = "TreasureBox_HostPromptTest",
            TreasureBoxId = "TreasureBox_HostPromptTest",
            GridPosition = new Vector2I(9, 50),
            RewardGold = 1
        };
        gridMap.AddChild(box);
        box.AddToGroup("TreasureBoxSpawn");
        SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
        SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Right);
        gridMap.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
        await AwaitFrames(3);
        InvokePrivateVoid(_game, "UpdateInteractionPrompt");

        var hud = _game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
        var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
        var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
        AssertThat(promptPlate.Visible).IsTrue();
        AssertThat(prompt.Prompt).IsEqual("Open");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var host = _game.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(promptPlate.Visible).IsFalse();

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(tree.Paused).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(promptPlate.Visible).IsTrue();
        AssertThat(prompt.Prompt).IsEqual("Open");
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
        var gameUi = _game.GetNode<Control>("UI/GameUI");
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
        AssertThat(inventoryEntry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
        AssertThat(inventoryEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);
        AssertThat(tree.Paused).IsTrue();
        AssertThat(gameUi.Visible).IsFalse();

        _viewport!.PushInput(new InputEventAction
        {
            Action = "toggle_inventory",
            Pressed = true
        });
        await AwaitFrames(3);

        AssertThat(inventory.GetParent()).IsNull();
        AssertThat(gameUi.Visible).IsTrue();
        AssertThat(FindEntry(host, UIScreenKinds.Pause).Policy.Hud)
            .IsEqual(UIHudPolicy.Visible);
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, inventoryButton);
    }

    [TestCase]
    public async Task HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause()
    {
        const int firstPresentationSentinel = -1;
        var tree = (SceneTree)Engine.GetMainLoop();
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        void MarkFirstPresentation(Node node)
        {
            if (node is not SettingsMenuController settings)
                return;

            settings.VisibilityChanged += () =>
            {
                if (settings.Visible)
                    settings.EditedSettings.MasterVolumePercent = firstPresentationSentinel;
            };
        }

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var settingsButton = pause.GetNode<Button>("%SettingsButton");
        settingsButton.GrabFocus();

        tree.NodeAdded += MarkFirstPresentation;
        try
        {
            settingsButton.EmitSignal(Button.SignalName.Pressed);
        }
        finally
        {
            tree.NodeAdded -= MarkFirstPresentation;
        }

        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(modalLayer);
        await AwaitFrames(2);

        // OpenSettings() shows the controller only after it has populated
        // EditedSettings. A second call would replace this invalid sentinel
        // with a normalized SettingsManager snapshot, proving presentation ran once.
        AssertThat(settings.EditedSettings.MasterVolumePercent)
            .IsEqual(firstPresentationSentinel);
        AssertThat(_viewport!.GuiGetFocusOwner())
            .IsEqual(settings.InitialFocusTarget);
        AssertThat(settings.InitialFocusTarget)
            .IsEqual(settings.GetNode<HSlider>("%MasterSlider"));

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
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var saveButton = pause.GetNode<Button>("%SaveButton");
        saveButton.GrabFocus();
        saveButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        var saveEntry = FindEntry(host, UIScreenKinds.SaveLoad);
        AssertThat(saveScreen.GetParent()).IsEqual(modalLayer);
        AssertThat(saveScreen.Mode).IsEqual(SaveLoadMode.Save);
        AssertThat(saveEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(saveEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(saveEntry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
        AssertThat(saveEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(saveEntry.Policy.PauseTree).IsFalse();
        AssertThat(saveEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(saveEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(saveEntry.Policy.Cancel).IsEqual(UICancelPolicy.Close);

        saveScreen.GetNode<Button>("%CancelButton")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(saveScreen)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, saveButton);

        var loadButton = pause.GetNode<Button>("%LoadButton");
        loadButton.GrabFocus();
        loadButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        var loadEntry = FindEntry(host, UIScreenKinds.SaveLoad);
        AssertThat(loadScreen.GetParent()).IsEqual(modalLayer);
        AssertThat(loadScreen.Mode).IsEqual(SaveLoadMode.Load);
        AssertThat(loadEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(loadEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(loadEntry.Policy.PauseTree).IsFalse();
        AssertThat(loadEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(loadEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);

        loadScreen.GetNode<Button>("%CancelButton")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(loadScreen)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, loadButton);
    }

    [TestCase]
    public async Task HostedSaveLoad_CloseReturnsFocusToSamePause()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        var saveButton = pause.GetNode<Button>("%SaveButton");
        saveButton.GrabFocus();
        saveButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(saveScreen)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, saveButton);
    }

    [TestCase]
    public async Task HostedSaveLoad_SaveIntentInvokesExistingSavePathOnce()
    {
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        manager.DeleteSave(0);
        var saveCompletions = 0;
        void OnSaveCompleted(bool success, int slot)
        {
            if (success && slot == 0)
                saveCompletions++;
        }

        manager.SaveCompleted += OnSaveCompleted;
        try
        {
            var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
            AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
            await AwaitFrames(2);
            var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
            pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            var saveScreen = FindDirectChild<SaveLoadScreenController>(host.GetNode<Control>("ModalLayer"));
            saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(saveCompletions).IsEqual(1);
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        }
        finally
        {
            manager.SaveCompleted -= OnSaveCompleted;
        }
    }

    [TestCase]
    public async Task HostedSaveLoad_ManualLoadUsesExistingPendingLoadTransition()
    {
        TestHelpers.WriteValidSlot(0);
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        // Suppress the actual scene swap while retaining the production
        // PendingLoadData handoff that OnHostedLoadSlotSelected owns.
        SetPrivateField(_game!, "_sceneChangeCommitted", true);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadScreen = FindDirectChild<SaveLoadScreenController>(host.GetNode<Control>("ModalLayer"));
        loadScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(manager.PendingLoadData).IsNotNull();
        AssertThat(manager.PendingLoadData!.Character!.Name).IsEqual("Aster");
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
    }

    [TestCase]
    public async Task HostedSaveLoad_AutosaveLoadUsesExistingPendingLoadTransition()
    {
        TestHelpers.WriteValidSlot(3);
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        SetPrivateField(_game!, "_sceneChangeCommitted", true);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadScreen = FindDirectChild<SaveLoadScreenController>(host.GetNode<Control>("ModalLayer"));
        loadScreen.GetNode<Button>("%Slot3Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(manager.PendingLoadData).IsNotNull();
        AssertThat(manager.PendingLoadData!.Character!.Name).IsEqual("Aster");
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
    }

    [TestCase]
    public async Task HostedSaveLoad_LoadFailureKeepsSaveLoadAndHostsPrompt()
    {
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        manager.DeleteSave(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadScreen = FindDirectChild<SaveLoadScreenController>(host.GetNode<Control>("ModalLayer"));
        var slotInfos = GetPrivateField<SaveSlotInfo[]>(loadScreen, "_slotInfos");
        slotInfos[0] = new SaveSlotInfo
        {
            Exists = true,
            State = SaveSlotState.Valid,
            SlotIndex = 0,
            PlayerName = "Missing",
            PlayerLevel = 1
        };
        loadScreen.GetNode<Button>("%Slot0Card").Disabled = false;
        loadScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        // The failed load must keep Save/Load open and host the error Prompt
        // as its child instead of closing the parent first.
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        var prompt = FindDirectChild<SiriusPrompt>(host.GetNode<Control>("ModalLayer"));
        AssertThat(prompt.GetNode<Label>("%Message").Text).IsEqual("Failed to load save file.");
        AssertThat(host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt).Policy.Parent)
            .IsEqual(FindEntry(host, UIScreenKinds.SaveLoad).Handle);

        // Dismissing the error returns to the same open Save/Load parent.
        prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();

        // The retained screen must be rearmed after the error: the one-way
        // terminal latch is released and selecting another slot is accepted.
        slotInfos[1] = new SaveSlotInfo
        {
            Exists = true,
            State = SaveSlotState.Valid,
            SlotIndex = 1,
            PlayerName = "Missing",
            PlayerLevel = 1
        };
        loadScreen.GetNode<Button>("%Slot1Card").Disabled = false;
        loadScreen.GetNode<Button>("%Slot1Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        // The new selection was accepted: a fresh error Prompt opened under
        // the same retained Save/Load parent.
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
    }

    [TestCase]
    public async Task HostedSaveLoad_TerminalActivationCannotRunTwice()
    {
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveScreen = FindDirectChild<SaveLoadScreenController>(host.GetNode<Control>("ModalLayer"));
        var saves = 0;
        saveScreen.SaveSlotSelected += _ => saves++;
        saveScreen.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);

        AssertThat(saves).IsEqual(0);
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
    }

    [TestCase]
    public async Task HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad()
    {
        TestHelpers.WriteValidSlot(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);
        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(prompt.GetParent()).IsEqual(modalLayer);
        AssertThat(promptEntry.Policy.Parent).IsEqual(FindEntry(host, UIScreenKinds.SaveLoad).Handle);
        AssertThat(promptEntry.Policy.InputPriority).IsEqual(UIInputPriority.Blocking);
        AssertThat(promptEntry.Policy.ExclusiveGroup)
            .IsEqual(UIScreenExclusiveGroups.BlockingPrompt);
        AssertThat(promptEntry.Policy.PauseTree).IsFalse();
        AssertThat(promptEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(prompt.GetNode<Label>("%Message").Text)
            .IsEqual("Slot 1 already contains save data. Overwrite it?");
        AssertThat(_viewport!.GuiGetFocusOwner()).IsEqual(prompt.InitialFocusTarget);

        prompt.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertHostedChildRemainsAboveSamePause(host, pause, pauseEntry.Handle);
    }

    [TestCase]
    public async Task HostedOverwrite_PrimaryInvokesSaveOnce()
    {
        TestHelpers.WriteValidSlot(0);
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        var completions = 0;
        void OnSaveCompleted(bool success, int slot)
        {
            if (success && slot == 0)
                completions++;
        }

        manager.SaveCompleted += OnSaveCompleted;
        try
        {
            var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
            var modalLayer = host.GetNode<Control>("ModalLayer");
            AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
            await AwaitFrames(2);
            var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
            pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);
            var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
            saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);
            var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
            var primary = prompt.GetNode<Button>("%PrimaryButton");

            primary.EmitSignal(Button.SignalName.Pressed);
            primary.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(4);

            AssertThat(completions).IsEqual(1);
            AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        }
        finally
        {
            manager.SaveCompleted -= OnSaveCompleted;
        }
    }

    [TestCase]
    public async Task HostedOverwrite_PrimaryWithNpcInteractionOpensErrorPromptUnderSaveLoad()
    {
        TestHelpers.WriteValidSlot(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);
        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var overwritePrompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(overwritePrompt.GetNode<Label>("%Message").Text)
            .IsEqual("Slot 1 already contains save data. Overwrite it?");
        var gameManager = _game!.GetNode<GameManager>("GameManager");
        gameManager.StartNpcInteraction();
        try
        {
            // The first Prompt's Primary closure calls OnHostedSaveSlotSelected(0);
            // the NPC-interaction branch must open a second Prompt beneath the
            // still-open Save/Load instead of closing it.
            overwritePrompt.GetNode<Button>("%PrimaryButton")
                .EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(3);

            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
            AssertThat(GodotObject.IsInstanceValid(overwritePrompt)).IsFalse();
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
                .IsEqual(1);

            var errorPrompt = FindDirectChild<SiriusPrompt>(modalLayer);
            AssertThat(host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt).Policy.Parent)
                .IsEqual(FindEntry(host, UIScreenKinds.SaveLoad).Handle);
            AssertThat(errorPrompt.GetNode<Label>("%Message").Text)
                .IsEqual("Cannot save during NPC interaction.");
        }
        finally
        {
            gameManager.EndNpcInteraction();
        }
    }

    [TestCase]
    public async Task HostedPrompt_ProgrammaticCloseClearsHandleAndGroup()
    {
        TestHelpers.WriteValidSlot(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);
        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        var promptEntry = FindEntry(host, UIScreenKinds.Prompt);
        AssertThat(GetPrivateField<UIScreenHandle?>(_game, "_hostedPromptHandle"))
            .IsEqual(promptEntry.Handle);
        AssertThat(GetPrivateField<SiriusPrompt?>(_game, "_hostedPrompt")).IsEqual(prompt);

        var close = host.TryClose(promptEntry.Handle, UIScreenCloseReason.ExplicitAction);
        AssertThat(close.Status).IsEqual(UIScreenCloseStatus.Closed);
        await AwaitFrames(2);

        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(GetPrivateField<UIScreenHandle?>(_game, "_hostedPromptHandle")).IsNull();
        AssertThat(GetPrivateField<SiriusPrompt?>(_game, "_hostedPrompt")).IsNull();
        AssertThat(host.ActiveEntries.Any(e =>
            e.Policy.ExclusiveGroup == UIScreenExclusiveGroups.BlockingPrompt)).IsFalse();

        // The released group must admit a fresh prompt presentation.
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
    }

    [TestCase]
    public async Task HostedPrompt_ParentCloseClearsDescendantReferences()
    {
        TestHelpers.WriteValidSlot(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseHandle = FindEntry(host, UIScreenKinds.Pause).Handle;
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);
        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();

        saveScreen.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(saveScreen)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(GetPrivateField<UIScreenHandle?>(_game, "_hostedPromptHandle")).IsNull();
        AssertThat(GetPrivateField<SiriusPrompt?>(_game, "_hostedPrompt")).IsNull();
        AssertThat(GetPrivateField<UIScreenHandle?>(_game, "_hostedSaveLoadHandle")).IsNull();
        AssertThat(GetPrivateField<SaveLoadScreenController?>(_game, "_hostedSaveLoadScreen")).IsNull();
        AssertHostedChildRemainsAboveSamePause(host, pause, pauseHandle);
    }

    [TestCase]
    public async Task PauseReturnToTitle_UsesSharedPromptAndCancelRestoresPause()
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

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(prompt.GetParent()).IsEqual(modalLayer);
        AssertThat(promptEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
        AssertThat(promptEntry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
        AssertThat(promptEntry.Policy.InputPriority).IsEqual(UIInputPriority.Blocking);
        AssertThat(promptEntry.Policy.ExclusiveGroup)
            .IsEqual(UIScreenExclusiveGroups.BlockingPrompt);
        AssertThat(promptEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(promptEntry.Policy.PauseTree).IsFalse();
        AssertThat(promptEntry.Policy.BlockGameplayInput).IsFalse();
        AssertThat(promptEntry.Policy.Hud).IsEqual(UIHudPolicy.Inherit);
        AssertThat(prompt.GetNode<Label>("%Message").Text)
            .IsEqual("Unsaved progress will be lost.");
        AssertThat(_viewport!.GuiGetFocusOwner()).IsEqual(prompt.InitialFocusTarget);

        prompt.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertHostedChildReturnedToSamePause(host, pause, pauseEntry.Handle, returnButton);
    }

    [TestCase]
    public async Task PauseReturnToTitle_PrimaryRequestsNavigationOnce()
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

            var prompt = FindDirectChild<SiriusPrompt>(
                host.GetNode<Control>("ModalLayer"));
            var primary = prompt.GetNode<Button>("%PrimaryButton");

            primary.EmitSignal(Button.SignalName.Pressed);
            primary.EmitSignal(Button.SignalName.Pressed);

            AssertThat(navigationRequests).IsEqual(1);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(navigationGame))
                navigationGame.Free();

            await AwaitFrames(2);
        }
    }

    // Protects the one-shot navigation guarantee in Game.RequestSceneChange:
    // the first request arms _sceneChangeCommitted, and any subsequent request
    // must be suppressed without overwriting _pendingScenePath or re-entering
    // the teardown-driven scene change. The sibling ReturnToTitle confirmation
    // test above overrides ReturnToMainMenu and only presses Confirm once, so
    // it never reaches this guard. This test drives the real first invocation
    // (with an empty path so ContinueSceneChangeAfterUiTeardown skips the
    // actual ChangeSceneToFile via its !string.IsNullOrEmpty(path) guard) and
    // then asserts a second commit is suppressed.
    [TestCase]
    public async Task RequestSceneChange_OneShotGuardSuppressesRepeatCommit()
    {
        var navigationGame = await CreateReturnTrackingGame();
        try
        {
            // Drive the real first request. An empty path arms the one-shot
            // guard through production code while ContinueSceneChangeAfterUiTeardown
            // skips ChangeSceneToFile, so no scene swap occurs in the test host.
            InvokePrivateVoid(navigationGame, "RequestSceneChange", "");

            // The first request must have armed the guard. This catches a
            // regression where the _sceneChangeCommitted assignment is removed
            // from RequestSceneChange: without it, the second request below
            // would proceed and overwrite the pending path.
            AssertThat(GetPrivateField<bool>(navigationGame, "_sceneChangeCommitted"))
                .IsTrue();

            // A second request must be suppressed by the guard without
            // overwriting the pending path or re-entering the scene change.
            InvokePrivateVoid(navigationGame, "RequestSceneChange", "res://second.tscn");

            AssertThat(GetPrivateField<bool>(navigationGame, "_sceneChangeCommitted"))
                .IsTrue();
            AssertThat(GetPrivateField<string>(navigationGame, "_pendingScenePath"))
                .IsNull();
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
    public async Task HostedOverwrite_ActiveChildConsumesCancelBeforeSaveLoad()
    {
        TestHelpers.WriteValidSlot(0);
        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat(InvokePrivateBool(_game, "TryOpenPause")).IsTrue();
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var pauseEntry = FindEntry(host, UIScreenKinds.Pause);
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveScreen.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        var handled = host.TryHandleInput(new InputEventAction
        {
            Action = "ui_cancel",
            Pressed = true
        });

        AssertThat(handled).IsEqual(UIInputDispatchResult.Consumed);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
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

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = ResolvePrivateMember(
            instance.GetType(),
            (type, flags) => type.GetField(fieldName, flags));
        if (field == null)
            throw new MissingFieldException(instance.GetType().FullName, fieldName);

        field.SetValue(instance, value);
    }

    private static void InvokePrivateVoid(
        object instance,
        string methodName,
        params object?[] arguments)
    {
        var method = ResolvePrivateMember(
            instance.GetType(),
            (type, flags) => type.GetMethod(methodName, flags));
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
