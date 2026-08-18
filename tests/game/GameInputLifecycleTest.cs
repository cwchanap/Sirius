using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GameInputLifecycleTest : Node
{
    private LifecycleGame? _game;
    private SubViewport? _viewport;
    private GameManager? _gameManager;
    private Game? _realGame;
    private readonly Dictionary<string, InputActionSnapshot> _inputActionSnapshots = new();
    private readonly Dictionary<int, float> _audioBusVolumes = new();
    private bool _treeWasPaused;
    private Input.MouseModeEnum _originalMouseMode;
    private int _audioBusCount;
    private DisplayServer.WindowMode _simulatedWindowMode;
    private Vector2I _simulatedWindowSize;
    private Action<DisplayServer.WindowMode>? _previousWindowSetModeOverride;
    private Action<Vector2I>? _previousWindowSetSizeOverride;
    private Func<DisplayServer.WindowMode>? _previousWindowGetModeOverride;
    private Func<Vector2I>? _previousWindowGetSizeOverride;
    private Action<string, string>? _previousFileWriteTextOverride;
    private Action<string, string, bool>? _previousFileMoveWithOverwriteOverride;
    private Action<string, string>? _previousFileMoveOverride;
    private Action<string>? _previousFileDeleteOverride;
    private TestHelpers.SaveFileSnapshot[] _saveFiles = null!;
    private SaveData? _incomingPendingLoadData;

    [BeforeTest]
    public async Task Setup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        _treeWasPaused = sceneTree.Paused;
        _originalMouseMode = Input.MouseMode;
        sceneTree.Paused = false;
        CaptureInputActions("toggle_inventory", "interact", "pause_menu", "ui_cancel", "ui_close_dialog");
        CaptureAudioState();
        CaptureAndInstallSettingsOverrides();
        _saveFiles = TestHelpers.CaptureSaveFiles();
        _incomingPendingLoadData = SaveManager.Instance?.PendingLoadData;
        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = null;

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(640, 360)
        };
        sceneTree.Root.AddChild(_viewport);

        _game = new LifecycleGame();
        _viewport.AddChild(_game);
        _game.AddChild(new CanvasLayer { Name = "UI" });

        _gameManager = new LifecycleGameManager();
        _game.AddChild(_gameManager);
        SetPrivateField(_game, "_gameManager", _gameManager);

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Paused = false;

        if (_realGame != null && IsInstanceValid(_realGame))
        {
            _realGame.Free();
            _realGame = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            if (_gameManager.IsInNpcInteraction) _gameManager.EndNpcInteraction();
            if (_gameManager.IsInWorldInteraction) _gameManager.EndWorldInteraction();
            if (_gameManager.IsInBattle) _gameManager.EndBattle(false);
        }

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

        await AwaitFrames(2);
        RestoreInputActions();
        RestoreAudioState();
        RestoreSettingsOverrides();
        Input.MouseMode = _originalMouseMode;
        sceneTree.Paused = _treeWasPaused;
        if (SaveManager.Instance != null)
            SaveManager.Instance.PendingLoadData = _incomingPendingLoadData;
        TestHelpers.RestoreSaveFiles(_saveFiles);
        TestHelpers.ReportSaveFileMismatches(_saveFiles, nameof(GameInputLifecycleTest));
    }
    [TestCase]
    public async Task ConfiguredCancel_DuringHostedBattleEscapesWithoutOpeningPause()
    {
        ConfigureCancelBindings(Key.P);
        _realGame = await InstantiateGameScene(_viewport!);
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");

        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(gameManager.IsInBattle).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task BattleResultCancel_ClosesBattleWithoutOpeningPauseOrReemittingResult()
    {
        ConfigureCancelBindings(Key.P);
        _realGame = await InstantiateGameScene(_viewport!);

        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        gameManager.StartBattle(Enemy.CreateGoblin());
        await AwaitFrames(2);
        var battle = GetPrivateField<BattleManager>(_realGame, "_battleManager");
        int finishedCount = 0;
        battle.BattleFinished += (_, _) => finishedCount++;
        InvokePrivate(battle, "EndBattle", true);
        await AwaitFrames(1);

        AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsTrue();
        AssertThat(gameManager.IsInBattle).IsFalse();
        AssertThat(finishedCount).IsEqual(1);

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Battle)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
            AssertThat(finishedCount).IsEqual(1);
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    // Replaces the native error-popup cancel proof. The hosted RecoverableError
    // Prompt owns Cancel==Consume, so a configured cancel dismisses only the
    // Prompt: Save/Load stays open and the Pause entry is untouched (no root
    // Pause fallback toggling the chain).
    [TestCase]
    public async Task ConfiguredKeyboardCancel_RecoverablePromptDoesNotFallThroughToParents()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        SaveManager.Instance?.DeleteSave(0);

        var host = await OpenPausedSaveLoadErrorPrompt();

        var pauseEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Pause);
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
        AssertThat(promptEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);

        PushPhysicalKey(Key.P);
        await AwaitFrames(3);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
        // No root Pause fallback ran: the same Pause entry still owns the chain.
        AssertThat(host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Pause).Handle)
            .IsEqual(pauseEntry.Handle);
    }

    // Distinct from the preceding fall-through test: proves the hosted
    // UIProcessPolicy.Always contract resolves to the live Prompt Control's
    // Godot ProcessModeEnum.Always, which is what keeps it dismissible while
    // SceneTree.Paused is true beneath the Pause lease.
    [TestCase]
    public async Task ConfiguredKeyboardCancel_PausedRecoverablePromptRemainsDismissible()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        SaveManager.Instance?.DeleteSave(0);

        var host = await OpenPausedSaveLoadErrorPrompt();

        // The live Prompt Control must resolve to Always so it processes input
        // while the tree is paused. The preceding test checks the policy entry;
        // this one checks the actual Godot process mode on the node.
        var prompt = FindDirectChild<SiriusPrompt>(host.GetNode<Control>("ModalLayer"));
        AssertThat(prompt.ProcessMode).IsEqual(ProcessModeEnum.Always);

        PushPhysicalKey(Key.P);
        await AwaitFrames(3);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
    }

    // When ShowSaveError is called but TryOpenHostedPrompt cannot present
    // (e.g. a Prompt is already active), the retained Save/Load screen must
    // still be rearmed so the user can select another slot. Without the
    // fallback the terminal latch stays consumed and all actions stay disabled.
    [TestCase]
    public async Task ShowSaveError_PromptOpenFails_RearmsSaveLoadScreen()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        SaveManager.Instance?.DeleteSave(0);

        // Opens Pause → Load → slot 0 failure → RecoverableError Prompt.
        // The Save/Load terminal latch is now consumed and a Prompt is active.
        var host = await OpenPausedSaveLoadErrorPrompt();
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var loadScreen = FindDirectChild<SaveLoadScreenController>(modalLayer);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(GetPrivateField<bool>(loadScreen, "_terminalEmitted")).IsTrue();

        // A second ShowSaveError while the Prompt is still open cannot present
        // a new Prompt — TryOpenHostedPrompt returns false. The fallback must
        // rearm the Save/Load screen so it is not permanently disabled.
        var opened = InvokePrivate<bool>(
            _game, "ShowSaveError", "Another error.", "Save Failed");

        AssertThat(opened).IsFalse();
        AssertThat(GetPrivateField<bool>(loadScreen, "_terminalEmitted")).IsFalse();

        // The rearm must have re-enabled the slot cards so a new selection is
        // accepted. Slot 1 is empty in Load mode, so we inject a Valid slot
        // info to make the card pressable, mirroring the existing rearm test.
        var slotInfos = GetPrivateField<SaveSlotInfo[]>(loadScreen, "_slotInfos");
        slotInfos[1] = new SaveSlotInfo
        {
            Exists = true,
            State = SaveSlotState.Valid,
            SlotIndex = 1,
            PlayerName = "Missing",
            PlayerLevel = 1
        };
        var slot1Card = loadScreen.GetNode<Button>("%Slot1Card");
        slot1Card.Disabled = false;
        slot1Card.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        // A fresh error Prompt opened under the same retained Save/Load parent.
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_WorldInteractionConsumesWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        _gameManager!.StartWorldInteraction();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(_gameManager.IsInWorldInteraction).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    // The native NPC-dialog phase is gone: hosted Dialogue/Shop/Heal entries
    // consume a configured cancel at the host. What remains of the old
    // "declines for native handler" contract is Game's root-fallback guard
    // for a bare interaction latch — IsInNpcInteraction set with no hosted
    // entry (e.g. a controller failure window) must decline rather than open
    // Pause on top of a stuck interaction.
    [TestCase]
    public async Task ConfiguredKeyboardCancel_BareNpcInteractionLatchDeclinesRootPause()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        _gameManager!.StartNpcInteraction();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsFalse();
            AssertThat(_gameManager.IsInNpcInteraction).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_ClosesHostedDialogueThroughRealRoute()
    {
        ConfigureCancelBindings(Key.P);
        _realGame = await InstantiateGameScene(_viewport!);
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        var internalPosition = TestHelpers.FindNpcInternalPosition(_realGame, "village_shopkeeper");

        InvokePrivate(_realGame, "OnNpcInteracted", internalPosition);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(2);

        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
            AssertThat(gameManager.IsInNpcInteraction).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredControllerCancel_ClosesHostedDialogueThroughRealRoute()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        _realGame = await InstantiateGameScene(_viewport!);
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        var internalPosition = TestHelpers.FindNpcInternalPosition(_realGame, "village_shopkeeper");

        InvokePrivate(_realGame, "OnNpcInteracted", internalPosition);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();

        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(gameManager.IsInNpcInteraction).IsFalse();
    }

    // Shared setup for the hosted Shop/Heal cancel tests: instantiate the
    // real Game, drive the real NPC interaction route, and pick the dialogue
    // choice that opens the hosted surface.
    private async Task<(Game Game, UIScreenHost Host)> OpenHostedNpcSurface(
        string npcId, string choiceLabel)
    {
        _realGame = await InstantiateGameScene(_viewport!);
        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        var internalPosition = TestHelpers.FindNpcInternalPosition(_realGame, npcId);

        InvokePrivate(_realGame, "OnNpcInteracted", internalPosition);
        await AwaitFrames(2);

        var dialogue = FindDirectChild<DialogueScreenController>(
            host.GetNode<Control>("ModalLayer"));
        TestHelpers.FindButton(dialogue, choiceLabel)
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        return (_realGame, host);
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_ClosesHostedShopThroughRealRoute()
    {
        ConfigureCancelBindings(Key.P);
        var (game, host) = await OpenHostedNpcSurface("village_shopkeeper", "Browse your wares.");
        var gameManager = game.GetNode<GameManager>("GameManager");
        var gameUi = game.GetNode<Control>("UI/GameUI");

        // Dialogue is gone; exactly the hosted Shop entry remains, blocking
        // gameplay input without pausing the tree, with the HUD hidden and
        // the cursor visible.
        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Shop);
        AssertThat(gameManager.IsInNpcInteraction).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.PauseTree).IsFalse();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
        AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(gameUi.Visible).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(2);

        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Shop)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
            AssertThat(gameManager.IsInNpcInteraction).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredControllerCancel_ClosesHostedShopThroughRealRoute()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        var (game, host) = await OpenHostedNpcSurface("village_shopkeeper", "Browse your wares.");
        var gameManager = game.GetNode<GameManager>("GameManager");

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
        AssertThat(host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Shop))
            .IsNotNull();

        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Shop)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(gameManager.IsInNpcInteraction).IsFalse();
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_ClosesHostedHealingThroughRealRoute()
    {
        ConfigureCancelBindings(Key.P);
        var (game, host) = await OpenHostedNpcSurface("village_healer", "Yes, heal me. (50 gold)");
        var gameManager = game.GetNode<GameManager>("GameManager");
        var gameUi = game.GetNode<Control>("UI/GameUI");

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Heal);
        AssertThat(gameManager.IsInNpcInteraction).IsTrue();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
        AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
        AssertThat(entry.Policy.PauseTree).IsFalse();
        AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
        AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(gameUi.Visible).IsFalse();
        AssertThat(Input.MouseMode).IsEqual(Input.MouseModeEnum.Visible);
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();

        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(2);

        try
        {
            AssertThat(_viewport!.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Heal)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
            AssertThat(gameManager.IsInNpcInteraction).IsFalse();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }
    }

    [TestCase]
    public async Task ConfiguredControllerCancel_ClosesHostedHealingThroughRealRoute()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        var (game, host) = await OpenHostedNpcSurface("village_healer", "Yes, heal me. (50 gold)");
        var gameManager = game.GetNode<GameManager>("GameManager");

        AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
        AssertThat(host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Heal))
            .IsNotNull();

        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Heal)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(gameManager.IsInNpcInteraction).IsFalse();
    }

    [TestCase]
    public async Task ConfiguredKeyboardPauseMenu_OpensHostedPauseThenResumesTreeOnSecondPhysicalAction()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        var pressedEvent = new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("pause_menu")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        await AwaitFrames(1);

        try
        {
            AssertThat(_viewport.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
            AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
            AssertThat(host.ActiveEntries.Count).IsEqual(1);
            AssertThat(host.ActiveEntries[0].Policy.ProcessPolicy)
                .IsEqual(UIProcessPolicy.WhenPaused);
            AssertThat(host.ActiveEntries[0].Policy.PauseTree).IsTrue();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
        }

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public async Task ConfiguredControllerUiCancel_OpensHostedPauseThenResumesTreeOnSecondPhysicalAction()
    {
        var controllerButton = JoyButton.B;
        ConfigureCancelBindings(Key.P, new InputEventJoypadButton
        {
            ButtonIndex = controllerButton
        });
        await ReplaceWithHostedLifecycleFixture();

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        var pressedEvent = new InputEventJoypadButton
        {
            ButtonIndex = controllerButton,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        await AwaitFrames(1);

        try
        {
            AssertThat(_viewport.IsInputHandled()).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
            AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
            AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
            AssertThat(host.ActiveEntries.Count).IsEqual(1);
            AssertThat(host.ActiveEntries[0].Policy.ProcessPolicy)
                .IsEqual(UIProcessPolicy.WhenPaused);
            AssertThat(host.ActiveEntries[0].Policy.PauseTree).IsTrue();
        }
        finally
        {
            ReleasePhysicalJoypadButton(controllerButton);
        }

        PushPhysicalJoypadButtonPressAndRelease(controllerButton);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_SettingsKeyCaptureAndPopupKeepHostedSettingsForNativeHandlers()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = FindDirectChild<SettingsMenuController>(host.GetNode<Control>("ModalLayer"));
        GetPrivateField<Button>(settings, "_inventoryKeyBtn")
            .EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);

        AssertThat(settings.IsRebinding).IsTrue();
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(settings.IsRebinding).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();

        var resolution = GetPrivateField<OptionButton>(settings, "_resolutionOption");
        resolution.ShowPopup();
        await AwaitFrames(1);

        AssertThat(settings.IsPopupOpen).IsTrue();
        PushPhysicalKeyDown(Key.P);
        await AwaitFrames(1);
        try
        {
            // Unlike ConsumeHere, ReserveForNativeHandler leaves the physical
            // input unhandled while keeping the hosted Settings and Pause
            // entries active for the native popup's handler.
            AssertThat(_viewport!.IsInputHandled()).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        }
        finally
        {
            ReleasePhysicalKey(Key.P);
            resolution.GetPopup().Hide();
        }
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_SaveLoadOverwriteDismissesChildThenClosesHostedChild()
    {
        ConfigureCancelBindings(Key.P);
        await ReplaceWithHostedLifecycleFixture();
        TestHelpers.WriteValidSlot(0);

        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var saveLoad = FindDirectChild<SaveLoadScreenController>(modalLayer);
        saveLoad.GetNode<Button>("%Slot0Card").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
            .IsEqual(1);
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();

        PushPhysicalKey(Key.P);
        await AwaitFrames(3);

        AssertThat(GodotObject.IsInstanceValid(saveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(host.ActiveEntries.Count).IsEqual(1);
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
    }

    [TestCase]
    public async Task ConfiguredControllerCancel_ClosesHostedSaveLoadBeforePause()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        await ReplaceWithHostedLifecycleFixture();

        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(3);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
    }

    [TestCase]
    public async Task ConfiguredKeyboardCancel_DeclinesToTopmostRiddleWithoutOpeningHostedPause()
    {
        ConfigureCancelBindings(Key.P);
        _viewport!.GuiEmbedSubwindows = true;
        await FreeLifecycleFixture();
        _realGame = await InstantiateGameScene(_viewport);

        var floorManager = _realGame.GetNode<FloorManager>("FloorManager");
        var gridMap = floorManager.CurrentGridMap;
        var playerController = _realGame.GetNode<PlayerController>("PlayerController");
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_ConfiguredCancelTest",
            "Puzzle_ConfiguredCancelTest",
            new Vector2I(8, 51));
        gridMap.AddChild(riddle);
        riddle.AddToGroup("PuzzleRiddleSpawn");
        SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
        SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Down);
        gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
        await AwaitFrames(3);
        InvokePrivate(_realGame, "UpdateInteractionPrompt");

        var hud = _realGame.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
        var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
        var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
        AssertThat(promptPlate.Visible).IsTrue();
        AssertThat(prompt.Prompt).IsEqual("Solve");

        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        InvokePrivate(_realGame, "OpenPuzzleRiddle", riddle);
        var screen = GetPrivateField<PuzzleRiddleScreenController>(
            _realGame, "_puzzleRiddleScreen");
        int closedCount = 0;
        screen.PuzzleRiddleClosed += () => closedCount++;
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
        AssertThat(gameManager.IsInWorldInteraction).IsTrue();
        AssertThat(promptPlate.Visible).IsFalse();
        await AwaitFrames(1);
        AssertThat(screen.Visible).IsTrue();

        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = true
        });
        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = Key.P,
            Pressed = false
        });
        await AwaitFrames(2);

        AssertThat(closedCount).IsEqual(1);
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
        AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        AssertThat(promptPlate.Visible).IsTrue();
        AssertThat(prompt.Prompt).IsEqual("Solve");
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
    }

    // Controller/gamepad equivalent of the keyboard riddle-cancel test: a
    // configured joypad cancel must close exactly one hosted riddle through
    // the same hosted close path without opening Pause or leaking the latch.
    [TestCase]
    public async Task ConfiguredControllerCancel_DeclinesToTopmostRiddleWithoutOpeningHostedPause()
    {
        var controllerBinding = new InputEventJoypadButton
        {
            ButtonIndex = (JoyButton)10
        };
        ConfigureCancelBindings(Key.P, controllerBinding);
        _viewport!.GuiEmbedSubwindows = true;
        await FreeLifecycleFixture();
        _realGame = await InstantiateGameScene(_viewport);

        var floorManager = _realGame.GetNode<FloorManager>("FloorManager");
        var gridMap = floorManager.CurrentGridMap;
        var playerController = _realGame.GetNode<PlayerController>("PlayerController");
        var gameManager = _realGame.GetNode<GameManager>("GameManager");
        var riddle = CreateRuntimeRiddle(
            "PuzzleRiddle_ConfiguredControllerCancelTest",
            "Puzzle_ConfiguredControllerCancelTest",
            new Vector2I(8, 51));
        gridMap.AddChild(riddle);
        riddle.AddToGroup("PuzzleRiddleSpawn");
        SetPrivateField(gridMap, "_grid", new int[gridMap.GridWidth, gridMap.GridHeight]);
        SetPrivateField(gridMap, "_playerPosition", new Vector2I(8, 50));
        SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Down);
        gridMap.CallDeferred(nameof(GridMap.RegisterStaticPuzzleEntities));
        await AwaitFrames(3);
        InvokePrivate(_realGame, "UpdateInteractionPrompt");

        var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
        InvokePrivate(_realGame, "OpenPuzzleRiddle", riddle);
        var screen = GetPrivateField<PuzzleRiddleScreenController>(
            _realGame, "_puzzleRiddleScreen");
        int closedCount = 0;
        screen.PuzzleRiddleClosed += () => closedCount++;
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
        AssertThat(gameManager.IsInWorldInteraction).IsTrue();
        await AwaitFrames(1);
        AssertThat(screen.Visible).IsTrue();

        PushPhysicalJoypadButtonPressAndRelease((JoyButton)10);
        await AwaitFrames(2);

        AssertThat(closedCount).IsEqual(1);
        AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
        AssertThat(gameManager.IsInWorldInteraction).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
    }

    [TestCase]
    public async Task DefeatReturnTimerIsOwnedAndDoesNotNavigateAfterCleanup()
    {
        int navigations = 0;
        var game = new LifecycleGame
        {
            MainMenuNavigationRequested = () => navigations++
        };
        _viewport!.AddChild(game);
        InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");

        game.Free();
        await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
            SceneTreeTimer.SignalName.Timeout);

        AssertThat(navigations).IsEqual(0);
    }

    [TestCase]
    public async Task DefeatReturnTimer_NavigatesOnceWhileOwnerLives()
    {
        int navigations = 0;
        var game = new LifecycleGame
        {
            MainMenuNavigationRequested = () => navigations++
        };
        _viewport!.AddChild(game);
        try
        {
            InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");
            InvokePrivate(game, "ScheduleDefeatReturnToMainMenu");

            await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
                SceneTreeTimer.SignalName.Timeout);

            AssertThat(navigations).IsEqual(1);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(game))
            {
                game.Free();
            }
        }
    }

    [TestCase]
    public async Task FloorReplacement_RebindsGridAndRefreshesPrompt()
    {
        var game = await InstantiateGameScene(_viewport!);
        try
        {
            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var originalGrid = floorManager.CurrentGridMap;
            ulong originalGridId = originalGrid.GetInstanceId();
            var playerController = game.GetNode<PlayerController>("PlayerController");
            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_FloorReplacementPromptTest",
                TreasureBoxId = "TreasureBox_FloorReplacementPromptTest",
                GridPosition = new Vector2I(9, 50),
                RewardGold = 1
            };
            originalGrid.AddChild(box);
            box.AddToGroup("TreasureBoxSpawn");
            SetPrivateField(originalGrid, "_grid", new int[originalGrid.GridWidth, originalGrid.GridHeight]);
            SetPrivateField(originalGrid, "_playerPosition", new Vector2I(8, 50));
            SetPrivateField(playerController, "_lastFacingDirection", Vector2I.Right);
            originalGrid.CallDeferred(nameof(GridMap.RegisterStaticTreasureBoxes));
            await AwaitFrames(3);

            var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
            var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
            InvokePrivate(game, "UpdateInteractionPrompt");
            var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");

            AssertThat(floorManager.LoadFloor(1)).IsTrue();
            await AwaitFrames(8);

            AssertThat(floorManager.CurrentGridMap.GetInstanceId()).IsNotEqual(originalGridId);
            AssertThat(GetPrivateField<GridMap>(game, "_gridMap"))
                .IsEqual(floorManager.CurrentGridMap);
            AssertThat(promptPlate.Visible).IsFalse();
        }
        finally
        {
            await FreeGameScene(game);
        }
    }

    [TestCase]
    public async Task InteractionPrompt_HidesDuringBattleAndRestoresAfterEscape()
    {
        var game = await InstantiateGameScene(_viewport!);
        try
        {
            var floorManager = game.GetNode<FloorManager>("FloorManager");
            var gridMap = floorManager.CurrentGridMap;
            var playerController = game.GetNode<PlayerController>("PlayerController");
            var gameManager = game.GetNode<GameManager>("GameManager");
            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_BattlePromptTest",
                TreasureBoxId = "TreasureBox_BattlePromptTest",
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
            InvokePrivate(game, "UpdateInteractionPrompt");

            var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
            var promptPlate = hud.GetNode<PanelContainer>("%PromptPlate");
            var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");

            gameManager.StartBattle(Enemy.CreateGoblin());

            AssertThat(promptPlate.Visible).IsFalse();
            var battle = GetPrivateField<BattleManager>(game, "_battleManager");
            AssertThat(GodotObject.IsInstanceValid(battle)).IsTrue();

            battle.RequestCancel();
            await AwaitFrames(2);

            AssertThat(promptPlate.Visible).IsTrue();
            AssertThat(prompt.Prompt).IsEqual("Open");
        }
        finally
        {
            await FreeGameScene(game);
        }
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

    private void ConfigureCancelBindings(Key pauseKey, InputEvent? controllerBinding = null)
    {
        if (controllerBinding != null)
        {
            EnsureInputAction("ui_cancel");
            InputMap.ActionAddEvent("ui_cancel", controllerBinding);
        }

        var settingsManager = new SettingsManager();
        try
        {
            var candidate = settingsManager.GetSnapshot();
            candidate.PrimaryKeybindings["pause_menu"] = (long)pauseKey;

            AssertThat(settingsManager.ApplyAndSave(candidate)).IsTrue();
        }
        finally
        {
            settingsManager.Free();
        }
    }

    private void PushPhysicalKey(Key physicalKey)
    {
        PushPhysicalKeyDown(physicalKey);
        ReleasePhysicalKey(physicalKey);
    }

    private void PushPhysicalKeyDown(Key physicalKey)
    {
        var pressedEvent = new InputEventKey
        {
            PhysicalKeycode = physicalKey,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("pause_menu")).IsTrue();
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        _viewport!.PushInput(pressedEvent);
    }

    private void ReleasePhysicalKey(Key physicalKey)
    {
        _viewport.PushInput(new InputEventKey
        {
            PhysicalKeycode = physicalKey,
            Pressed = false
        });
    }

    private void PushPhysicalJoypadButtonPressAndRelease(JoyButton button)
    {
        var pressedEvent = new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = true
        };
        AssertThat(pressedEvent.IsActionPressed("ui_cancel")).IsTrue();
        _viewport!.PushInput(pressedEvent);
        ReleasePhysicalJoypadButton(button);
    }

    private void ReleasePhysicalJoypadButton(JoyButton button)
    {
        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = button,
            Pressed = false
        });
    }

    private async Task FreeLifecycleFixture()
    {
        if (_game != null && IsInstanceValid(_game))
        {
            _game.Free();
            _game = null;
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.Free();
            _gameManager = null;
        }

        await AwaitFrames(2);
    }

    private async Task ReplaceWithHostedLifecycleFixture()
    {
        await FreeLifecycleFixture();

        var hostScene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn")
            ?? throw new InvalidOperationException("Failed to load UIScreenHost.tscn.");
        var game = new LifecycleGame();
        var ui = new CanvasLayer { Name = "UI" };
        ui.AddChild(new Control { Name = "GameUI" });
        ui.AddChild(hostScene.Instantiate<UIScreenHost>());
        game.AddChild(ui);

        var gameManager = new LifecycleGameManager();
        game.AddChild(gameManager);
        SetPrivateField(game, "_gameManager", gameManager);

        _game = game;
        _gameManager = gameManager;
        _viewport!.AddChild(game);
        await AwaitFrames(2);
    }

    // Builds the real paused chain Pause -> Save/Load -> RecoverableError
    // Prompt through production handlers: open Pause with the configured key,
    // open Load mode, then force a load failure so OnHostedLoadSlotSelected
    // surfaces ShowSaveError beneath the still-open Save/Load parent.
    private async Task<UIScreenHost> OpenPausedSaveLoadErrorPrompt()
    {
        PushPhysicalKey(Key.P);
        await AwaitFrames(2);

        var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var loadScreen = FindDirectChild<SaveLoadScreenController>(
            host.GetNode<Control>("ModalLayer"));
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

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        return host;
    }

    private static async Task<Game> InstantiateGameScene(Node parent)
    {
        var scene = GD.Load<PackedScene>("res://scenes/game/Game.tscn")
            ?? throw new InvalidOperationException("Failed to load Game.tscn.");
        var game = scene.Instantiate<Game>();
        parent.AddChild(game);
        await AwaitFrames(8);
        return game;
    }

    private static async Task FreeGameScene(Game game)
    {
        if (GodotObject.IsInstanceValid(game))
        {
            game.Free();
        }

        await AwaitFrames(2);
    }

    private static async Task AwaitFrames(int frameCount)
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        for (int i = 0; i < frameCount; i++)
        {
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static void InvokePrivate(object instance, string methodName, params object?[] arguments)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        method.Invoke(instance, arguments);
    }

    private static T InvokePrivate<T>(object instance, string methodName, params object?[] arguments)
    {
        var method = FindPrivateMethod(instance.GetType(), methodName)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        return (T)method.Invoke(instance, arguments)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = FindPrivateField(instance.GetType(), fieldName)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = FindPrivateField(instance.GetType(), fieldName)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
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

    private static MethodInfo? FindPrivateMethod(Type? type, string methodName)
    {
        while (type != null)
        {
            var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }

    private void CaptureInputActions(params string[] actionNames)
    {
        _inputActionSnapshots.Clear();
        foreach (var actionName in actionNames)
        {
            var snapshot = new InputActionSnapshot
            {
                Existed = InputMap.HasAction(actionName),
                Deadzone = InputMap.HasAction(actionName)
                    ? InputMap.ActionGetDeadzone(actionName)
                    : 0.5f
            };

            if (snapshot.Existed)
            {
                foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
                {
                    snapshot.Events.Add((InputEvent)inputEvent.Duplicate());
                }
            }

            _inputActionSnapshots[actionName] = snapshot;
        }
    }

    private void RestoreInputActions()
    {
        foreach (var (actionName, snapshot) in _inputActionSnapshots)
        {
            if (!snapshot.Existed)
            {
                if (InputMap.HasAction(actionName))
                {
                    InputMap.EraseAction(actionName);
                }
                continue;
            }

            EnsureInputAction(actionName);
            InputMap.ActionSetDeadzone(actionName, snapshot.Deadzone);
            foreach (var inputEvent in InputMap.ActionGetEvents(actionName))
            {
                InputMap.ActionEraseEvent(actionName, inputEvent);
            }
            foreach (var inputEvent in snapshot.Events)
            {
                InputMap.ActionAddEvent(actionName, (InputEvent)inputEvent.Duplicate());
            }
        }

        _inputActionSnapshots.Clear();
    }

    private static void EnsureInputAction(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            InputMap.AddAction(actionName);
        }
    }

    private void CaptureAudioState()
    {
        _audioBusCount = AudioServer.BusCount;
        _audioBusVolumes.Clear();
        for (int i = 0; i < _audioBusCount; i++)
        {
            _audioBusVolumes[i] = AudioServer.GetBusVolumeDb(i);
        }
    }

    private void RestoreAudioState()
    {
        while (AudioServer.BusCount > _audioBusCount)
        {
            AudioServer.RemoveBus(AudioServer.BusCount - 1);
        }

        foreach (var (busIndex, volumeDb) in _audioBusVolumes)
        {
            if (busIndex < AudioServer.BusCount)
            {
                AudioServer.SetBusVolumeDb(busIndex, volumeDb);
            }
        }
    }

    private void CaptureAndInstallSettingsOverrides()
    {
        _previousWindowSetModeOverride = SettingsManager.WindowSetModeOverride;
        _previousWindowSetSizeOverride = SettingsManager.WindowSetSizeOverride;
        _previousWindowGetModeOverride = SettingsManager.WindowGetModeOverride;
        _previousWindowGetSizeOverride = SettingsManager.WindowGetSizeOverride;
        _previousFileWriteTextOverride = SettingsManager.FileWriteTextOverride;
        _previousFileMoveWithOverwriteOverride = SettingsManager.FileMoveWithOverwriteOverride;
        _previousFileMoveOverride = SettingsManager.FileMoveOverride;
        _previousFileDeleteOverride = SettingsManager.FileDeleteOverride;

        _simulatedWindowMode = DisplayServer.WindowGetMode();
        _simulatedWindowSize = DisplayServer.WindowGetSize();
        SettingsManager.WindowSetModeOverride = mode => _simulatedWindowMode = mode;
        SettingsManager.WindowSetSizeOverride = size => _simulatedWindowSize = size;
        SettingsManager.WindowGetModeOverride = () => _simulatedWindowMode;
        SettingsManager.WindowGetSizeOverride = () => _simulatedWindowSize;
        SettingsManager.FileWriteTextOverride = (_, _) => { };
        SettingsManager.FileMoveWithOverwriteOverride = (_, _, _) => { };
        SettingsManager.FileMoveOverride = (_, _) => { };
        SettingsManager.FileDeleteOverride = _ => { };
    }

    private void RestoreSettingsOverrides()
    {
        SettingsManager.WindowSetModeOverride = _previousWindowSetModeOverride;
        SettingsManager.WindowSetSizeOverride = _previousWindowSetSizeOverride;
        SettingsManager.WindowGetModeOverride = _previousWindowGetModeOverride;
        SettingsManager.WindowGetSizeOverride = _previousWindowGetSizeOverride;
        SettingsManager.FileWriteTextOverride = _previousFileWriteTextOverride;
        SettingsManager.FileMoveWithOverwriteOverride = _previousFileMoveWithOverwriteOverride;
        SettingsManager.FileMoveOverride = _previousFileMoveOverride;
        SettingsManager.FileDeleteOverride = _previousFileDeleteOverride;
    }

    private static PuzzleRiddleSpawn CreateRuntimeRiddle(
        string name,
        string puzzleId,
        Vector2I gridPosition)
    {
        return new PuzzleRiddleSpawn
        {
            Name = name,
            RiddleId = name,
            PuzzleId = puzzleId,
            GridPosition = gridPosition,
            PromptText = "Which stone opens the old gate?",
            ChoiceIds = new Godot.Collections.Array<string> { "east_stone" },
            ChoiceLabels = new Godot.Collections.Array<string> { "East Stone" },
            CorrectChoiceId = "east_stone",
            WrongAnswerDamage = 12
        };
    }

    private sealed class InputActionSnapshot
    {
        public bool Existed { get; init; }
        public float Deadzone { get; init; }
        public List<InputEvent> Events { get; } = new();
    }

    public partial class LifecycleGame : Game
    {
        public Action? MainMenuNavigationRequested { get; set; }
        protected override double DefeatReturnDelaySeconds => 0.01;
        protected override void ReturnToMainMenu() => MainMenuNavigationRequested?.Invoke();

        public override void _Ready()
        {
        }
    }

    public partial class LifecycleGameManager : GameManager
    {
        public override void _Ready()
        {
        }
    }
}
