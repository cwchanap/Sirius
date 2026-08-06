# Gameplay Pause Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the gameplay `UIScreenHost` the sole presentation authority for Pause and the legacy child screens Pause opens, while replacing the desktop dialog with a responsive `SiriusModalShell` screen.

**Architecture:** `Game.tscn` owns one scene-local host configured by `Game._EnterTree()`. `Game` translates Pause actions into host entries and delegates domain operations to existing controllers. Pause is the pausing parent; Inventory, Save/Load, Settings, and Return-to-Title confirmation are logical children whose close signals return through the host stack.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing `UIScreenHost`, `SiriusModalShell`, `SiriusTheme`, and `InputHintPresenter`.

## Global Constraints

- Follow `docs/superpowers/specs/2026-08-06-hpa-382-gameplay-pause-host-design.md`.
- Keep `GameManager`, `SaveManager`, `InventoryMenuController`, `SaveLoadDialog`, and `SettingsMenuController` as domain owners.
- Do not add an autoload, navigation service, modal manager, screen registry, or generic confirmation framework.
- Do not redesign child screens or migrate unrelated battle/NPC/puzzle dialogs.
- Do not preserve `PauseMenuDialog` compatibility; delete it after the host path is covered.
- Pause keeps the gameplay HUD visible but inert; Inventory hides it.
- Required viewport checks are 1280×720 and 640×360 only.
- Use test-first changes and commit each task after its focused tests pass.

---

## File structure

### Create

- `scenes/ui/PauseScreen.tscn` — scene-authored Pause composition.
- `scripts/ui/PauseScreenController.cs` — Pause button signals, focus target, input hints, responsive shell refresh.
- `scenes/ui/PauseReturnToTitleConfirmation.tscn` — flow-specific destructive confirmation.
- `scripts/ui/PauseReturnToTitleConfirmationController.cs` — Confirm/Cancel signals and safe initial focus.
- `tests/ui/PauseScreenControllerTest.cs` — Pause component behavior and responsive assertions.
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs` — confirmation component behavior.
- `tests/game/GameplayPauseHostTest.cs` — production host integration, stack behavior, and teardown.

### Modify

- `project.godot` — pin embedded subwindows.
- `scenes/game/Game.tscn` — add one `UIScreenHost` instance.
- `scripts/game/Game.cs` — configure the host and orchestrate Pause/children/navigation.
- `scripts/game/PlayerController.cs` — consume the root gameplay-suppression provider.
- `scripts/ui/InventoryMenuController.cs` — remove private tree-pause ownership and emit close requests.
- `tests/game/GameInputLifecycleTest.cs` — retain domain escape coverage without legacy Pause fields.
- `tests/game/PlayerControllerTest.cs` — presentation-block coverage.
- `tests/ui/InventoryMenuControllerTest.cs` — host-owned lifecycle coverage.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — record the production ownership change.

### Delete

- `scripts/ui/PauseMenuDialog.cs`
- `tests/ui/PauseMenuDialogTest.cs`

---

### Task 1: Build the scene-authored Pause screen

**Files:**
- Create: `scenes/ui/PauseScreen.tscn`
- Create: `scripts/ui/PauseScreenController.cs`
- Create: `tests/ui/PauseScreenControllerTest.cs`

**Interfaces:**
- Produces: `PauseScreenController.InitialFocusTarget : Control`
- Produces signals: `ResumeRequested`, `InventoryRequested`, `SaveRequested`, `LoadRequested`, `SettingsRequested`, `ReturnToTitleRequested`
- Consumes: `SiriusModalShell`, `InputHintPresenter`, existing theme and icon helpers

- [ ] **Step 1: Write a failing scene/controller test**

Create `tests/ui/PauseScreenControllerTest.cs` with a fixture that instantiates `res://scenes/ui/PauseScreen.tscn` under a `SubViewport`. Assert exact node names and each signal:

```csharp
[TestCase]
public async Task ButtonsEmitOneTypedRequest()
{
    var screen = await InstantiatePauseScreen(new Vector2I(1280, 720));
    int resume = 0;
    int inventory = 0;
    int save = 0;
    int load = 0;
    int settings = 0;
    int returnToTitle = 0;

    screen.ResumeRequested += () => resume++;
    screen.InventoryRequested += () => inventory++;
    screen.SaveRequested += () => save++;
    screen.LoadRequested += () => load++;
    screen.SettingsRequested += () => settings++;
    screen.ReturnToTitleRequested += () => returnToTitle++;

    screen.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
    screen.GetNode<Button>("%InventoryButton").EmitSignal(Button.SignalName.Pressed);
    screen.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
    screen.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
    screen.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
    screen.GetNode<Button>("%ReturnToTitleButton").EmitSignal(Button.SignalName.Pressed);

    AssertThat((resume, inventory, save, load, settings, returnToTitle))
        .IsEqual((1, 1, 1, 1, 1, 1));
}
```

Add tests that `InitialFocusTarget` is `%ResumeButton`, the destructive button has the existing destructive theme variation, and all six buttons remain visible with minimum height at least 40 at 640×360.

- [ ] **Step 2: Run the focused test and verify the missing scene failure**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
```

Expected: FAIL because `PauseScreen.tscn` and `PauseScreenController` do not exist.

- [ ] **Step 3: Create the controller with presentation-only behavior**

Create `scripts/ui/PauseScreenController.cs` with this public surface:

```csharp
using Godot;

public partial class PauseScreenController : Control
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void InventoryRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void ReturnToTitleRequestedEventHandler();

    private readonly InputHintPresenter _inputHints = new();
    private SiriusModalShell _shell = null!;
    private Button _resumeButton = null!;
    private Button _inventoryButton = null!;
    private Button _saveButton = null!;
    private Button _loadButton = null!;
    private Button _settingsButton = null!;
    private Button _returnToTitleButton = null!;

    public Control InitialFocusTarget => _resumeButton;

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _resumeButton = GetNode<Button>("%ResumeButton");
        _inventoryButton = GetNode<Button>("%InventoryButton");
        _saveButton = GetNode<Button>("%SaveButton");
        _loadButton = GetNode<Button>("%LoadButton");
        _settingsButton = GetNode<Button>("%SettingsButton");
        _returnToTitleButton = GetNode<Button>("%ReturnToTitleButton");

        _resumeButton.Pressed += OnResumePressed;
        _inventoryButton.Pressed += OnInventoryPressed;
        _saveButton.Pressed += OnSavePressed;
        _loadButton.Pressed += OnLoadPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _returnToTitleButton.Pressed += OnReturnToTitlePressed;

        RefreshPresentation(GetViewportRect().Size);
    }

    public void RefreshPresentation(Vector2 availableSize)
    {
        bool compact = availableSize.X < 800 || availableSize.Y < 450;
        _shell.Compact = compact;
        _shell.RefreshPresentation(availableSize);
        _inputHints.ApplyCompactButton(_resumeButton, "Resume", "pause_menu", "ui_cancel");
    }

    private void OnResumePressed() => EmitSignal(SignalName.ResumeRequested);
    private void OnInventoryPressed() => EmitSignal(SignalName.InventoryRequested);
    private void OnSavePressed() => EmitSignal(SignalName.SaveRequested);
    private void OnLoadPressed() => EmitSignal(SignalName.LoadRequested);
    private void OnSettingsPressed() => EmitSignal(SignalName.SettingsRequested);
    private void OnReturnToTitlePressed() => EmitSignal(SignalName.ReturnToTitleRequested);

    public override void _ExitTree()
    {
        if (_resumeButton != null) _resumeButton.Pressed -= OnResumePressed;
        if (_inventoryButton != null) _inventoryButton.Pressed -= OnInventoryPressed;
        if (_saveButton != null) _saveButton.Pressed -= OnSavePressed;
        if (_loadButton != null) _loadButton.Pressed -= OnLoadPressed;
        if (_settingsButton != null) _settingsButton.Pressed -= OnSettingsPressed;
        if (_returnToTitleButton != null) _returnToTitleButton.Pressed -= OnReturnToTitlePressed;
    }
}
```

Do not add pause, navigation, manager, or child-screen references.

- [ ] **Step 4: Create the scene with six authored buttons**

Create `scenes/ui/PauseScreen.tscn` as a full-rect `Control`, instance `SiriusModalShell.tscn` as `%ModalShell`, set `Title = "Paused"` and `SizeClass = Medium`, and add a vertical `VBoxContainer` under the shell body with unique-name buttons:

```text
PauseScreen
└── ModalShell
    └── Panel/Margin/RootLayout/BodyScroll/BodyHost
        └── PauseActions
            ├── ResumeButton          "Resume"
            ├── InventoryButton       "Inventory"
            ├── SaveButton            "Save"
            ├── LoadButton            "Load"
            ├── SettingsButton        "Settings"
            └── ReturnToTitleButton   "Return to Title"
```

Set every button's minimum height to 40 and horizontal size flag to `ExpandFill`. Apply the existing destructive button variation to `ReturnToTitleButton`; do not add a new theme token.

- [ ] **Step 5: Run component tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
```

Expected: PASS.

- [ ] **Step 6: Commit the Pause component**

```bash
git add scenes/ui/PauseScreen.tscn scripts/ui/PauseScreenController.cs tests/ui/PauseScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored pause screen"
```

---

### Task 2: Build the flow-specific Return-to-Title confirmation

**Files:**
- Create: `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- Create: `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- Create: `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

**Interfaces:**
- Produces: `PauseReturnToTitleConfirmationController.InitialFocusTarget : Control`
- Produces signals: `ReturnToTitleConfirmed`, `CancelRequested`
- Does not navigate or close host entries itself

- [ ] **Step 1: Write failing confirmation tests**

Cover both signals and safe initial focus:

```csharp
[TestCase]
public async Task CancelOwnsInitialFocusAndActionsEmitOnce()
{
    var confirmation = await InstantiateConfirmation();
    int confirmed = 0;
    int canceled = 0;
    confirmation.ReturnToTitleConfirmed += () => confirmed++;
    confirmation.CancelRequested += () => canceled++;

    AssertThat(confirmation.InitialFocusTarget)
        .IsEqual(confirmation.GetNode<Button>("%CancelButton"));

    confirmation.GetNode<Button>("%ReturnToTitleButton")
        .EmitSignal(Button.SignalName.Pressed);
    confirmation.GetNode<Button>("%CancelButton")
        .EmitSignal(Button.SignalName.Pressed);

    AssertThat(confirmed).IsEqual(1);
    AssertThat(canceled).IsEqual(1);
}
```

- [ ] **Step 2: Verify the focused test fails**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
```

Expected: FAIL because the scene/controller are missing.

- [ ] **Step 3: Implement the narrow controller**

Use this surface:

```csharp
public partial class PauseReturnToTitleConfirmationController : Control
{
    [Signal] public delegate void ReturnToTitleConfirmedEventHandler();
    [Signal] public delegate void CancelRequestedEventHandler();

    private Button _returnButton = null!;
    private Button _cancelButton = null!;

    public Control InitialFocusTarget => _cancelButton;

    public override void _Ready()
    {
        _returnButton = GetNode<Button>("%ReturnToTitleButton");
        _cancelButton = GetNode<Button>("%CancelButton");
        _returnButton.Pressed += OnReturnPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    private void OnReturnPressed() => EmitSignal(SignalName.ReturnToTitleConfirmed);
    private void OnCancelPressed() => EmitSignal(SignalName.CancelRequested);

    public override void _ExitTree()
    {
        if (_returnButton != null) _returnButton.Pressed -= OnReturnPressed;
        if (_cancelButton != null) _cancelButton.Pressed -= OnCancelPressed;
    }
}
```

- [ ] **Step 4: Author the confirmation scene**

Use `SiriusModalShell` with title `Return to Title?`, a concise body label stating that unsaved progress will be lost, and two actions in this order:

```text
Cancel | Return to Title
```

Use the existing destructive theme variation only on the final Return button. Keep the scene specific to Pause; do not create a generic confirmation base class.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
git add scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "feat(ui): add pause return-to-title confirmation"
```

Expected: focused tests PASS.

---

### Task 3: Add and configure the production gameplay host

**Files:**
- Modify: `project.godot`
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Create: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Produces: scene node `UI/UIScreenHost : UIScreenHost`
- Produces: `Game.IsGameplayInputSuppressed()` private composed predicate
- Produces: `Game.HandleGameplayRootCancel(UIRootCancelContext) : UIRootCancelResult`

- [ ] **Step 1: Write a failing production-scene host test**

Instantiate `Game.tscn` under a 1280×720 `SubViewport` and assert:

```csharp
[TestCase]
public async Task GameSceneOwnsOneReadyScreenHost()
{
    var game = await InstantiateGameScene();
    var hosts = game.GetNode<CanvasLayer>("UI")
        .GetChildren()
        .OfType<UIScreenHost>()
        .ToArray();

    AssertThat(hosts.Length).IsEqual(1);
    AssertThat(hosts[0].Diagnostics.SubwindowEmbeddingEnabled).IsTrue();
    AssertThat(hosts[0].ActiveEntries.Count).IsEqual(0);
}
```

Also assert `HudRoot` behavior by opening a temporary test entry with `Hud = UIHudPolicy.Hidden`, then closing it and confirming `UI/GameUI.Visible` restores to its incoming value.

- [ ] **Step 2: Run the host test and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.GameSceneOwnsOneReadyScreenHost"
```

Expected: FAIL because `UI/UIScreenHost` is absent.

- [ ] **Step 3: Pin embedded subwindows**

Add this project setting:

```ini
[display]

window/subwindows/embed_subwindows=true
```

Do not change renderer, viewport dimensions, or window mode.

- [ ] **Step 4: Add the host instance to `Game.tscn`**

Add a packed-scene resource for `res://scenes/ui/UIScreenHost.tscn` and instance it as the final child of `UI`:

```ini
[ext_resource type="PackedScene" path="res://scenes/ui/UIScreenHost.tscn" id="15_screen_host"]

[node name="UIScreenHost" parent="UI" instance=ExtResource("15_screen_host")]
```

Keep `GameUI` at `UI/GameUI`.

- [ ] **Step 5: Configure the host in `Game._EnterTree()`**

Add fields:

```csharp
private UIScreenHost _screenHost = null!;
private bool _presentationGameplayBlocked;
private static readonly IReadOnlySet<StringName> GameplayCoreCancelActions =
    new HashSet<StringName> { "pause_menu", "ui_cancel" };
```

At the start of `_EnterTree()`, before existing pending-load setup:

```csharp
_gameUI = GetNode<Control>("UI/GameUI");
_screenHost = GetNode<UIScreenHost>("UI/UIScreenHost");
_screenHost.Configure(new UIScreenHostOptions
{
    HudRoot = _gameUI,
    CoreCancelActions = GameplayCoreCancelActions,
    RootCancelFallback = HandleGameplayRootCancel,
    GameplayInputBlockChanged = blocked => _presentationGameplayBlocked = blocked
});
```

Add the composed predicate:

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

Initially make `HandleGameplayRootCancel` return `Declined`; Pause opening is added in Task 5.

- [ ] **Step 6: Run the focused host test**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.GameSceneOwnsOneReadyScreenHost"
```

Expected: PASS.

- [ ] **Step 7: Commit the host bootstrap**

```bash
git add project.godot scenes/game/Game.tscn scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): configure gameplay screen host"
```

---

### Task 4: Compose presentation blocking into player input

**Files:**
- Modify: `scripts/game/PlayerController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/PlayerControllerTest.cs`

**Interfaces:**
- Produces: `PlayerController.GameplayInputSuppressedProvider : Func<bool>?`
- Consumes: `Game.IsGameplayInputSuppressed`

- [ ] **Step 1: Write failing provider tests**

Add tests that movement and interaction are ignored when the provider returns true, while existing domain checks still work when it returns false:

```csharp
[TestCase]
public void PresentationBlockPreventsMovement()
{
    var controller = CreateReadyController();
    controller.GameplayInputSuppressedProvider = () => true;

    controller._UnhandledInput(new InputEventKey
    {
        Keycode = Key.Right,
        Pressed = true
    });

    AssertThat(_gridMap.GetPlayerPosition()).IsEqual(_startingPosition);
}
```

Add a second assertion for the `interact` action so hosted UI cannot leak interaction commands.

- [ ] **Step 2: Verify the tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest.PresentationBlock"
```

Expected: FAIL because the provider does not exist.

- [ ] **Step 3: Add the optional provider**

In `PlayerController`:

```csharp
public Func<bool>? GameplayInputSuppressedProvider { private get; set; }
```

At the start of `_UnhandledInput`, after the null manager guard:

```csharp
if (GameplayInputSuppressedProvider?.Invoke() == true)
{
    return;
}
```

Keep the existing battle/NPC/world checks; the provider is additive and defaults to no block for isolated fixtures.

In `Game._Ready()` after resolving `_playerController`:

```csharp
_playerController.GameplayInputSuppressedProvider = IsGameplayInputSuppressed;
```

In `Game._ExitTree()`:

```csharp
if (_playerController != null)
{
    _playerController.GameplayInputSuppressedProvider = null;
}
```

- [ ] **Step 4: Run focused tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest"
git add scripts/game/PlayerController.cs scripts/game/Game.cs tests/game/PlayerControllerTest.cs
git commit -m "feat(game): compose presentation input blocking"
```

Expected: all `PlayerControllerTest` cases PASS.

---

### Task 5: Present and close Pause through `UIScreenHost`

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete: `scripts/ui/PauseMenuDialog.cs`
- Delete: `tests/ui/PauseMenuDialogTest.cs`

**Interfaces:**
- Produces: `Game.TryOpenPause() : bool`
- Produces: `Game.ClosePause(UIScreenCloseReason) : void`
- Consumes: `PauseScreenController` signals and `UIScreenHost.TryPresent/TryClose`

- [ ] **Step 1: Write failing Pause host integration tests**

Add tests for open state and repeated toggle:

```csharp
[TestCase]
public async Task RootPauseActionOpensOneHostEntryAndOwnsGameplayState()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

    PushAction("pause_menu");
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
    AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
    AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
    AssertThat(host.CurrentState.Cursor).IsEqual(UICursorPolicy.Visible);
    AssertThat(host.CurrentState.Hud).IsEqual(UIHudPolicy.Visible);
    AssertThat(host.ActiveEntries.Count).IsEqual(1);
}

[TestCase]
public async Task SecondPauseActionClosesSameEntryAndRestoresGameplay()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");
    bool incomingPaused = GetTree().Paused;

    PushAction("pause_menu");
    await AwaitFrames(1);
    PushAction("pause_menu");
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
    AssertThat(host.ActiveEntries.Count).IsEqual(0);
    AssertThat(GetTree().Paused).IsEqual(incomingPaused);
}
```

Add a Resume-button test and an invalid-prior-focus test that frees the prior focused control before closing Pause and verifies no exception plus an empty restoration lease.

- [ ] **Step 2: Run tests and verify legacy behavior fails the new contract**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest"
```

Expected: FAIL because Pause still uses `PauseMenuDialog` outside the host.

- [ ] **Step 3: Add Pause fields and connection helpers**

Replace `_pauseMenuDialog`, `_pauseMenuRestorePending`, and related restoration state with:

```csharp
private UIScreenHandle? _pauseHandle;
private PauseScreenController? _pauseScreen;
```

Add one connector and one disconnect helper for the six signals. The disconnect helper must tolerate a freed controller.

- [ ] **Step 4: Implement `TryOpenPause()` with the exact policy**

Use:

```csharp
private bool TryOpenPause()
{
    if (_screenHost.IsKindActive(UIScreenKinds.Pause))
        return true;

    var packed = GD.Load<PackedScene>("res://scenes/ui/PauseScreen.tscn");
    var screen = packed?.Instantiate<PauseScreenController>();
    if (screen == null)
        return false;

    ConnectPauseScreen(screen);
    var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
    {
        Kind = UIScreenKinds.Pause,
        Layer = UIScreenLayer.Modal,
        InputPriority = UIInputPriority.Modal,
        ProcessPolicy = UIProcessPolicy.WhenPaused,
        PauseTree = true,
        BlockGameplayInput = true,
        Cursor = UICursorPolicy.Visible,
        Hud = UIHudPolicy.Visible,
        LowerLayers = UILowerLayerPolicy.VisibleInert,
        Cancel = UICancelPolicy.Close,
        InitialFocus = () => screen.InitialFocusTarget,
        Cleanup = _ => ClearPausePresentation(screen),
        NodeLifetime = UINodeLifetime.QueueFree
    });

    if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
    {
        DisconnectPauseScreen(screen);
        screen.Free();
        GD.PushError($"[Game] Failed to open Pause: {result.Status}");
        return false;
    }

    _pauseScreen = screen;
    _pauseHandle = result.Handle.Value;
    return true;
}
```

`ClearPausePresentation` nulls the handle/reference only when they still refer to this instance.

- [ ] **Step 5: Move root Pause opening to the host fallback**

Implement `HandleGameplayRootCancel` so only a matched `pause_menu` opens Pause after preserving required legacy domain precedence:

```csharp
private UIRootCancelResult HandleGameplayRootCancel(UIRootCancelContext context)
{
    if (!context.MatchedCoreActions.Contains(new StringName("pause_menu")))
        return UIRootCancelResult.Declined;

    if (TryHandleLegacyRootCancelBeforePause())
        return UIRootCancelResult.Consumed;

    if (_gameManager.IsInWorldInteraction || _gameManager.IsInNpcInteraction)
        return UIRootCancelResult.Declined;

    return TryOpenPause()
        ? UIRootCancelResult.Consumed
        : UIRootCancelResult.Declined;
}
```

`TryHandleLegacyRootCancelBeforePause` keeps only active error dismissal and the existing battle escape behavior. Puzzle/NPC native dialogs continue receiving declined input.

Remove the `pause_menu` branch that calls `HandlePauseMenuInput()` from `Game._Input()`. Delete the old Pause toggle/hide/restore methods and flags after tests compile.

- [ ] **Step 6: Wire Resume and Cancel to host close**

Resume calls:

```csharp
private void OnPauseResumeRequested()
{
    if (_pauseHandle.HasValue)
        _screenHost.TryClose(_pauseHandle.Value, UIScreenCloseReason.ExplicitAction);
}
```

Host Cancel uses `UICancelPolicy.Close`, so no controller `_Input` implementation is added.

- [ ] **Step 7: Update lifecycle tests away from legacy fields**

Replace reflection assertions on `_pauseMenuDialog` with `host.IsKindActive(UIScreenKinds.Pause)`. Keep tests for battle result, error dismissal, puzzle/native Cancel, and remapped input. Remove tests specific to `AcceptDialog.CloseRequested` from the deleted Pause component.

- [ ] **Step 8: Delete the legacy dialog and run focused suites**

```bash
git rm scripts/ui/PauseMenuDialog.cs tests/ui/PauseMenuDialogTest.cs
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: all selected cases PASS and no source reference to `PauseMenuDialog` remains:

```bash
rg "PauseMenuDialog|_pauseMenuRestorePending" .
```

Expected: no matches outside historical planning documents.

- [ ] **Step 9: Commit the Pause migration**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "feat(ui): migrate gameplay pause to screen host"
```

---

### Task 6: Move Inventory lifecycle under the host

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Produces signal: `InventoryMenuController.CloseRequested`
- Produces: `InventoryMenuController.InitialFocusTarget : Control?`
- Produces: `Game.TryOpenInventory(UIScreenHandle? parent) : bool`

- [ ] **Step 1: Write failing controller ownership tests**

Add tests proving `OpenMenu` and `CloseMenu` do not mutate the tree pause baseline:

```csharp
[TestCase]
public void OpenAndCloseDoNotOwnSceneTreePause()
{
    bool incoming = GetTree().Paused;

    _menu.OpenMenu();
    AssertThat(GetTree().Paused).IsEqual(incoming);

    _menu.CloseMenu();
    AssertThat(GetTree().Paused).IsEqual(incoming);
}
```

Add a Close-button test that emits `CloseRequested` exactly once without hiding the screen before the host acts.

- [ ] **Step 2: Verify controller tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest.OpenAndCloseDoNotOwnSceneTreePause|FullyQualifiedName~InventoryMenuControllerTest.CloseButton"
```

Expected: FAIL because Inventory currently snapshots and restores `SceneTree.Paused`.

- [ ] **Step 3: Remove private pause ownership and expose close intent**

In `InventoryMenuController`:

```csharp
[Signal] public delegate void CloseRequestedEventHandler();
public Control? InitialFocusTarget =>
    GetNodeOrNull<Control>("%WeaponSlot/Button") ?? _closeButton;
```

Delete `_pauseSnapshotCaptured`, `_treeWasPausedBeforeOpen`, `RestoreTreePause`, and the `_ExitTree` pause restore. `OpenMenu()` only refreshes, shows, and focuses `InitialFocusTarget`. `CloseMenu()` only hides.

Change the Close button to emit `CloseRequested`. `_Input()` may continue observing device changes, but remove direct closing for `ui_cancel` and `toggle_inventory`; the host owns those actions.

- [ ] **Step 4: Add Inventory host fields and helper**

In `Game` add:

```csharp
private UIScreenHandle? _inventoryHandle;
```

Implement `TryOpenInventory(UIScreenHandle? parent)` using the existing `_inventoryMenu` instance and this policy:

```csharp
var result = _screenHost.TryPresent(_inventoryMenu, new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Inventory,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = parent.HasValue ? UIProcessPolicy.Always : UIProcessPolicy.WhenPaused,
    Parent = parent,
    PauseTree = !parent.HasValue,
    BlockGameplayInput = !parent.HasValue,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Close,
    EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
    InitialFocus = () => _inventoryMenu.InitialFocusTarget,
    SetPresented = presented =>
    {
        if (presented) _inventoryMenu.OpenMenu();
        else _inventoryMenu.CloseMenu();
    },
    SetInteractive = interactive => _inventoryMenu.SetProcessInput(interactive),
    Cleanup = _ => ClearInventoryHandle(),
    NodeLifetime = UINodeLifetime.External
});
```

Connect `CloseRequested` once during `SetupInventoryMenu`; its handler closes `_inventoryHandle` through the host.

- [ ] **Step 5: Route both Inventory entry points through the helper**

- Direct `toggle_inventory` opens Inventory with `parent: null` only when `IsGameplayInputSuppressed()` is false.
- Pause's Inventory action calls `TryOpenInventory(_pauseHandle)`.
- An active Inventory owns `toggle_inventory` as an entry action, so a second toggle closes it without reaching `Game._Input()`.

Do not create separate direct and child controller instances.

- [ ] **Step 6: Add integration tests**

Cover:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
AssertThat(host.CurrentState.Hud).IsEqual(UIHudPolicy.Hidden);
```

For Pause-child Inventory, assert both kinds remain active, Inventory has `Parent = pauseHandle`, closing Inventory restores Pause as top owner, and the same Pause node instance remains alive.

- [ ] **Step 7: Run focused tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest"
git add scripts/ui/InventoryMenuController.cs scripts/game/Game.cs tests/ui/InventoryMenuControllerTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): host inventory lifecycle"
```

Expected: selected tests PASS.

---

### Task 7: Register Save/Load and Settings as Pause children

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Produces: `Game.TryOpenSaveLoadFromPause(SaveLoadDialog.DialogMode) : bool`
- Produces: `Game.TryOpenSettingsFromPause() : bool`
- Consumes existing `SaveLoadDialog` and `SettingsMenuController` terminal signals

- [ ] **Step 1: Write failing child-return tests**

Add one test per child. The Save example:

```csharp
[TestCase]
public async Task SaveChildClosesBackToSamePauseEntry()
{
    var game = await OpenPause();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");
    var pauseBefore = GetActiveHandle(host, UIScreenKinds.Pause);

    GetPauseScreen(game).GetNode<Button>("%SaveButton")
        .EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
    AssertThat(GetEntry(host, UIScreenKinds.SaveLoad).Parent).IsEqual(pauseBefore);

    GetSaveLoadDialog(game).EmitSignal(SaveLoadDialog.SignalName.DialogClosed);
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
    AssertThat(GetActiveHandle(host, UIScreenKinds.Pause)).IsEqual(pauseBefore);
}
```

Add equivalent Settings and Load cases. Add Cancel-priority tests for Save overwrite child and Settings popup/key capture.

- [ ] **Step 2: Verify tests fail under manual hide/restore behavior**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.SaveChild|FullyQualifiedName~GameplayPauseHostTest.SettingsChild"
```

Expected: FAIL because children are not registered with parent handles.

- [ ] **Step 3: Implement Save/Load child registration**

Replace `_saveLoadFromPause` and `CleanupSaveDialogAndRestorePause` with a nullable host handle:

```csharp
private UIScreenHandle? _saveLoadHandle;
```

Instantiate and connect the existing dialog, then present it with:

```csharp
Parent = _pauseHandle,
Kind = UIScreenKinds.SaveLoad,
Layer = UIScreenLayer.Modal,
InputPriority = UIInputPriority.Modal,
ProcessPolicy = UIProcessPolicy.Always,
PauseTree = false,
BlockGameplayInput = false,
Cursor = UICursorPolicy.Visible,
Hud = UIHudPolicy.Inherit,
LowerLayers = UILowerLayerPolicy.VisibleInert,
Cancel = UICancelPolicy.Close,
InterceptCancel = _ =>
{
    if (!_saveLoadDialog!.HasActiveChildDialog)
        return UIInputInterception.DeferToPolicy;

    _saveLoadDialog.DismissActiveChildDialog();
    return UIInputInterception.ConsumeHere;
},
IsPresented = () => _saveLoadDialog!.Visible,
SetPresented = presented =>
{
    if (presented) _saveLoadDialog!.ShowDialog(mode);
    else _saveLoadDialog!.Hide();
},
FocusViewport = () => _saveLoadDialog!,
Cleanup = _ => DisconnectAndClearSaveLoadDialog(),
NodeLifetime = UINodeLifetime.QueueFree
```

`DialogClosed` closes the host handle. Save/load slot callbacks retain their current domain logic. After successful save or ordinary cancel, close only the Save/Load handle; Pause remains active.

- [ ] **Step 4: Implement Settings child registration**

Add:

```csharp
private UIScreenHandle? _settingsHandle;
```

Present the existing `SettingsMenu.tscn` controller with the same parent/layer/process/HUD/lower-layer policy as Save/Load. Use:

```csharp
InterceptCancel = _ =>
    _settingsMenu!.IsRebinding || _settingsMenu.IsPopupOpen
        ? UIInputInterception.ReserveForNativeHandler
        : UIInputInterception.DeferToPolicy,
IsPresented = () => _settingsMenu!.Visible,
SetPresented = presented =>
{
    if (presented) _settingsMenu!.OpenSettings(showOverlay: false);
    else _settingsMenu!.Hide();
},
Cleanup = _ => DisconnectAndClearSettings(),
NodeLifetime = UINodeLifetime.QueueFree
```

The existing `Closed` signal closes the host handle. Delete `RestorePauseMenuAfterSettings` and `_pauseMenuRestorePending` remnants.

- [ ] **Step 5: Update error and successful-operation paths**

Replace every call that previously restored Pause by popup visibility with one of:

```csharp
CloseSaveLoadChild(UIScreenCloseReason.ExplicitAction);
CloseSettingsChild(UIScreenCloseReason.ExplicitAction);
```

Errors may open after the child closes, but they must not reconstruct Pause. The active Pause entry remains in the host stack.

- [ ] **Step 6: Run focused tests and remove restoration flags**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
rg "_saveLoadFromPause|CleanupSaveDialogAndRestorePause|RestorePauseMenuAfterSettings|_pauseMenuRestorePending" scripts tests
```

Expected: tests PASS and `rg` returns no matches.

- [ ] **Step 7: Commit child integration**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "feat(ui): host pause child screens"
```

---

### Task 8: Add idempotent Return-to-Title navigation and safe host teardown

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Produces: `Game.TryOpenReturnToTitleConfirmation() : bool`
- Produces: `Game.ChangeSceneAfterUiTeardown(string scenePath) : void`
- Consumes: `UIScreenHost.PrepareForTeardown()`

- [ ] **Step 1: Write failing child-first and one-shot tests**

Create a `LifecycleGame` override that counts scene-navigation requests. Test:

```csharp
[TestCase]
public async Task ReturnToTitleConfirmationIsChildAndCommitsOnce()
{
    var game = await OpenPause<LifecycleGame>();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

    GetPauseScreen(game).GetNode<Button>("%ReturnToTitleButton")
        .EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    var confirmation = GetReturnConfirmation(game);
    var entry = GetEntry(host, UIScreenKinds.ConfirmQuitToMain);
    AssertThat(entry.Parent).IsEqual(GetActiveHandle(host, UIScreenKinds.Pause));

    confirmation.GetNode<Button>("%ReturnToTitleButton")
        .EmitSignal(Button.SignalName.Pressed);
    confirmation.GetNode<Button>("%ReturnToTitleButton")
        .EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(3);

    AssertThat(game.MainMenuNavigationCount).IsEqual(1);
    AssertThat(host.ActiveEntries.Count).IsEqual(0);
}
```

Add a Cancel test that closes only `ConfirmQuitToMain` and restores focus to Pause's Return-to-Title button.

- [ ] **Step 2: Verify tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.ReturnToTitle"
```

Expected: FAIL because the confirmation is not hosted and quit is direct.

- [ ] **Step 3: Present the confirmation as a blocking child**

Add fields:

```csharp
private UIScreenHandle? _returnToTitleConfirmationHandle;
private bool _sceneChangeCommitted;
```

Present `PauseReturnToTitleConfirmation.tscn` with:

```csharp
Kind = UIScreenKinds.ConfirmQuitToMain,
Layer = UIScreenLayer.Modal,
InputPriority = UIInputPriority.Blocking,
ProcessPolicy = UIProcessPolicy.Always,
Parent = _pauseHandle,
ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt,
PauseTree = false,
BlockGameplayInput = false,
Cursor = UICursorPolicy.Visible,
Hud = UIHudPolicy.Inherit,
LowerLayers = UILowerLayerPolicy.VisibleInert,
Cancel = UICancelPolicy.Close,
InitialFocus = () => confirmation.InitialFocusTarget,
Cleanup = _ => ClearReturnConfirmation(confirmation),
NodeLifetime = UINodeLifetime.QueueFree
```

Duplicate-kind and exclusive-group conflicts are idempotent no-ops.

- [ ] **Step 4: Add the one-shot confirm handler**

```csharp
private void OnReturnToTitleConfirmed()
{
    if (_sceneChangeCommitted)
        return;

    _sceneChangeCommitted = true;
    ChangeSceneAfterUiTeardown("res://scenes/ui/MainMenu.tscn");
}
```

Cancel closes only `_returnToTitleConfirmationHandle` with `ExplicitAction`.

- [ ] **Step 5: Add a deferred teardown-aware scene-change helper**

Use one helper for Return to Title and in-game Load:

```csharp
private void ChangeSceneAfterUiTeardown(string scenePath)
{
    var status = _screenHost.PrepareForTeardown();
    if (status == UIScreenTeardownPreparationStatus.Deferred)
    {
        Callable.From(() => ChangeSceneAfterUiTeardown(scenePath)).CallDeferred();
        return;
    }

    PerformSceneChange(scenePath);
}

protected virtual void PerformSceneChange(string scenePath)
{
    GetTree().ChangeSceneToFile(scenePath);
}
```

Route `ReturnToMainMenu`, successful in-game Load, and corrupted-save return through this helper. Keep `_ExitTree` as a disconnect fallback and clear all controller/provider subscriptions there.

- [ ] **Step 6: Add teardown restoration coverage**

Open Pause plus Settings, set non-default incoming values for tree pause, mouse mode, and HUD visibility, call `PrepareForTeardown`, and assert:

```csharp
AssertThat(status).IsEqual(UIScreenTeardownPreparationStatus.Complete);
AssertThat(host.ActiveEntries.Count).IsEqual(0);
AssertThat(GetTree().Paused).IsEqual(incomingPaused);
AssertThat(Input.MouseMode).IsEqual(incomingMouseMode);
AssertThat(game.GetNode<Control>("UI/GameUI").Visible).IsEqual(incomingHudVisible);
AssertThat(host.Diagnostics.StateLeases.IncomingPaused).IsNull();
```

If the first call is `Deferred`, await one process frame and retry exactly as production does.

- [ ] **Step 7: Run focused tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "feat(ui): add safe pause navigation teardown"
```

Expected: selected tests PASS.

---

### Task 9: Update lifecycle documentation and run the complete verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Review all files changed in Tasks 1–8

**Interfaces:**
- Documents the production ownership boundary for later HPA-383/HPA-384/HPA-569–573 work

- [ ] **Step 1: Update the lifecycle contract**

Add a dated HPA-382 production note stating:

```text
Game.tscn now owns one scene-local UIScreenHost. Pause, direct Inventory, and
Pause-launched Inventory/Save/Load/Settings/Return-to-Title confirmation are
hosted entries. Pause is the pausing parent. GameManager and existing child
controllers retain domain authority. Unmigrated battle/NPC/puzzle flows keep
their existing controllers until their vertical tickets.
```

Document that `_pauseMenuRestorePending`, `_saveLoadFromPause`, and direct Inventory tree-pause mutation were removed.

- [ ] **Step 2: Run build and focused verification**

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: build succeeds and all selected tests PASS.

- [ ] **Step 3: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: PASS with no new orphan-node report.

- [ ] **Step 4: Run source and scope checks**

```bash
rg "PauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause|CleanupSaveDialogAndRestorePause|RestorePauseMenuAfterSettings" scripts scenes tests
rg "GetTree\(\)\.Paused" scripts/ui/InventoryMenuController.cs
rg "UIScreenHost" scenes/game/Game.tscn scripts/game/Game.cs docs/ui/hpa-376/ui-lifecycle-contract.md
```

Expected:

- first command: no matches;
- second command: no matches;
- third command: the one production host, its configuration, and the lifecycle note.

- [ ] **Step 5: Review the implementation against acceptance criteria**

Confirm in the diff:

- exactly one gameplay host exists;
- Pause uses `SiriusModalShell` and six current actions;
- Resume initial focus is explicit;
- Pause is retained as parent while children are active;
- Return-to-Title confirmation is child-first and guarded once;
- Inventory no longer owns tree pause;
- scene navigation waits for host teardown completion;
- no unrelated screen redesign or generic framework was added.

- [ ] **Step 6: Commit documentation and final test adjustments**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record gameplay pause host ownership"
```

- [ ] **Step 7: Prepare the implementation PR description**

Use this structure:

```markdown
## Summary
- make `UIScreenHost` the production Pause and direct-Inventory presentation authority
- replace `PauseMenuDialog` with a responsive `SiriusModalShell` screen
- host Pause children and add idempotent Return-to-Title teardown

## Validation
- `dotnet build Sirius.sln`
- focused Pause/host/lifecycle/controller tests
- `dotnet test Sirius.sln --settings test.runsettings.local`

## Scope
- no child-screen redesign
- no generic modal/navigation framework
- no unrelated legacy-dialog migration
```
