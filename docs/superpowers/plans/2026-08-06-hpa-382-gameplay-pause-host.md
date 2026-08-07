# Gameplay Pause Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make gameplay `UIScreenHost` the sole presentation authority for Pause and the screens Pause opens, while safely introducing root tree pause only after HPA-378 lifecycle/process gates pass.

**Architecture:** The real `Game.tscn` owns one scene-local host, but `Game` tolerates synthetic test fixtures with no host. Direct Inventory migrates atomically to host ownership. Hosted Pause is built and tested with `PauseTree=false` before production Cancel cuts over; root Pause flips to real tree pause only after `GridMap` processing and regression gates are green.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing `UIScreenHost`, `SiriusModalShell`, `SiriusUiMetrics`, `InputHintPresenter`.

## Global Constraints

- Follow `docs/superpowers/specs/2026-08-06-hpa-382-gameplay-pause-host-design.md`.
- No root `PauseTree=true` until teardown, process normalization, composed blocking, Inventory ownership, and `PauseTree=false` parity are proven.
- Keep domain ownership in `GameManager`, `SaveManager`, `InventoryMenuController`, `SaveLoadDialog`, and `SettingsMenuController`.
- No navigation service, modal manager, screen registry, DI container, generic confirmation framework, or compatibility wrapper.
- Do not redesign child screens in this ticket.
- Preserve current Inventory HUD behavior with `Hud = Inherit`; HPA-357 owns later Inventory presentation changes.
- Do not manually `AddChild` a view before hosting it. Controls attach to the host layer; Windows attach directly to the host.
- Any `SubViewport` fixture that presents `SaveLoadDialog` sets `GuiEmbedSubwindows = true` explicitly.
- Keep sibling `UIScreenEntrySpec` values explicit; do not extract a future-facing child-spec factory in this ticket.
- Required layout checks: 1280×720 and 640×360 only.
- Every task uses red → minimal implementation → green → commit.

---

## File Structure

### Create

- `scenes/ui/PauseScreen.tscn`
- `scripts/ui/PauseScreenController.cs`
- `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- `tests/ui/PauseScreenControllerTest.cs`
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`

### Modify

- `project.godot`
- `scenes/game/Game.tscn`
- `scripts/game/Game.cs`
- `scripts/game/PlayerController.cs`
- `scenes/game/floors/FloorGF.tscn`
- `scenes/game/floors/Floor1F.tscn`
- `scenes/game/floors/Floor2F.tscn`
- `scenes/game/floors/Floor3F.tscn`
- `scenes/ui/InventoryMenu.tscn`
- `scripts/ui/InventoryMenuController.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/PlayerControllerTest.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete at production cutover

- `scripts/ui/PauseMenuDialog.cs`
- `tests/ui/PauseMenuDialogTest.cs`

---

## Task 1: Build the scene-authored Pause component with shared metrics

**Files:**
- Create: `scenes/ui/PauseScreen.tscn`
- Create: `scripts/ui/PauseScreenController.cs`
- Create: `tests/ui/PauseScreenControllerTest.cs`

**Interfaces:**
- Produces: `Control InitialFocusTarget`
- Produces signals: `ResumeRequested`, `InventoryRequested`, `SaveRequested`, `LoadRequested`, `SettingsRequested`, `ReturnToTitleRequested`
- Consumes: `SiriusModalShell`, `SiriusUiMetrics`, `InputHintPresenter`

- [ ] **Step 1: Write the failing action/focus test**

Instantiate `PauseScreen.tscn` in a test viewport and assert Resume focus plus exactly one emission from each button.

```csharp
[TestCase]
public async Task SceneExposesSixActionsAndResumeFocus()
{
    var pause = await InstantiatePause(new Vector2I(1280, 720));
    AssertThat(pause.InitialFocusTarget)
        .IsEqual(pause.GetNode<Button>("%ResumeButton"));

    int resume = 0, inventory = 0, save = 0, load = 0, settings = 0, title = 0;
    pause.ResumeRequested += () => resume++;
    pause.InventoryRequested += () => inventory++;
    pause.SaveRequested += () => save++;
    pause.LoadRequested += () => load++;
    pause.SettingsRequested += () => settings++;
    pause.ReturnToTitleRequested += () => title++;

    pause.GetNode<Button>("%ResumeButton").EmitSignal(Button.SignalName.Pressed);
    pause.GetNode<Button>("%InventoryButton").EmitSignal(Button.SignalName.Pressed);
    pause.GetNode<Button>("%SaveButton").EmitSignal(Button.SignalName.Pressed);
    pause.GetNode<Button>("%LoadButton").EmitSignal(Button.SignalName.Pressed);
    pause.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
    pause.GetNode<Button>("%ReturnToTitleButton").EmitSignal(Button.SignalName.Pressed);

    AssertThat(new[] { resume, inventory, save, load, settings, title })
        .ContainsExactly(1, 1, 1, 1, 1, 1);
}
```

- [ ] **Step 2: Write failing responsive + resize tests using shared metrics**

For each focus viewport:

```csharp
var compact = SiriusUiMetrics.IsCompact(viewport.Size);
var minimum = SiriusUiMetrics.MinimumTarget(compact);
```

Assert all six buttons meet `minimum.Y`: 44 at 1280×720, 40 at 640×360.

Also instantiate at 1280×720, resize the viewport to 640×360 after `_Ready()`, await a frame, and assert the shell switches to compact mode. This prevents an implementation that refreshes only once at construction.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
```

Expected: FAIL because the component does not exist.

- [ ] **Step 4: Implement the presentation-only controller**

Use shared metrics and the Control resize signal:

```csharp
public partial class PauseScreenController : Control
{
    [Signal] public delegate void ResumeRequestedEventHandler();
    [Signal] public delegate void InventoryRequestedEventHandler();
    [Signal] public delegate void SaveRequestedEventHandler();
    [Signal] public delegate void LoadRequestedEventHandler();
    [Signal] public delegate void SettingsRequestedEventHandler();
    [Signal] public delegate void ReturnToTitleRequestedEventHandler();

    private SiriusModalShell _shell = null!;
    private Button _resume = null!;

    public Control InitialFocusTarget => _resume;

    public override void _Ready()
    {
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _resume = GetNode<Button>("%ResumeButton");
        Resized += OnResized;
        BindButtons();
        RefreshLayout();
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        var size = GetViewportRect().Size;
        _shell.Compact = SiriusUiMetrics.IsCompact(size);
        _shell.RefreshPresentation(size);
    }

    public override void _ExitTree()
    {
        Resized -= OnResized;
        UnbindButtons();
    }
}
```

`BindButtons`/`UnbindButtons` use six named methods and only emit the six signals.

- [ ] **Step 5: Author the scene**

```text
PauseScreen (Control, full rect)
└── ModalShell (%ModalShell, SiriusModalShell, title "Paused")
    └── .../BodyHost/PauseActions (VBoxContainer)
        ├── ResumeButton
        ├── InventoryButton
        ├── SaveButton
        ├── LoadButton
        ├── SettingsButton
        └── ReturnToTitleButton
```

Set button minimum target to `SiriusUiMetrics.MinimumTarget(false).Y` in the authored desktop scene. Runtime compact layout may reduce it to the shared compact target. Reuse the existing destructive variation for Return to Title.

- [ ] **Step 6: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
git add scenes/ui/PauseScreen.tscn scripts/ui/PauseScreenController.cs tests/ui/PauseScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored pause screen"
```

---

## Task 2: Build the flow-specific Return-to-Title confirmation

**Files:**
- Create: `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- Create: `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- Create: `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

**Interfaces:**
- Produces: `Control InitialFocusTarget`
- Signals: `ReturnToTitleConfirmed`, `CancelRequested`
- Does not navigate or guard duplicates.

- [ ] **Step 1: Write failing signal/focus tests**

Assert Cancel owns initial focus and each action emits once.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
```

- [ ] **Step 3: Implement the narrow controller**

```csharp
public partial class PauseReturnToTitleConfirmationController : Control
{
    [Signal] public delegate void ReturnToTitleConfirmedEventHandler();
    [Signal] public delegate void CancelRequestedEventHandler();

    private Button _return = null!;
    private Button _cancel = null!;
    public Control InitialFocusTarget => _cancel;

    public override void _Ready()
    {
        _return = GetNode<Button>("%ReturnToTitleButton");
        _cancel = GetNode<Button>("%CancelButton");
        _return.Pressed += OnReturn;
        _cancel.Pressed += OnCancel;
    }

    private void OnReturn() => EmitSignal(SignalName.ReturnToTitleConfirmed);
    private void OnCancel() => EmitSignal(SignalName.CancelRequested);

    public override void _ExitTree()
    {
        _return.Pressed -= OnReturn;
        _cancel.Pressed -= OnCancel;
    }
}
```

Author `SiriusModalShell` title `Return to Title?`, body `Unsaved progress will be lost.`, actions `Cancel | Return to Title`, destructive styling only on the final action.

- [ ] **Step 4: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
git add scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "feat(ui): add pause return-to-title confirmation"
```

---

## Task 3: Bootstrap the optional-in-tests gameplay host and centralize scene teardown

**Files:**
- Modify: `project.godot`
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Create: `tests/game/GameplayPauseHostTest.cs`
- Verify unchanged compatibility: `tests/game/GameTest.cs`, `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Production scene: `UI/UIScreenHost : UIScreenHost`
- `Game._screenHost : UIScreenHost?`
- Private: `RequestSceneChange(string path)`
- Existing protected seam retained: `ReturnToMainMenu()`

- [ ] **Step 1: Write the real-scene host test**

`GameplayPauseHostTest` loads `Game.tscn` and asserts exactly one host exists and starts empty.

When the fixture uses a `SubViewport`, configure:

```csharp
viewport.GuiEmbedSubwindows = true;
```

before adding the Game scene.

Do not rely on the project setting to mutate test-created SubViewports.

- [ ] **Step 2: Write teardown-preparation coverage**

Open a disposable host entry and call `PrepareForTeardown()` through the real host fixture. Assert `Complete` eventually leaves `ActiveEntries.Count == 0` and restores incoming state.

Scene-navigation one-shot behavior is covered later at the Return-to-Title integration boundary; do not add a second virtual `PerformSceneChange` seam just for this test.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.GameSceneOwnsOneReadyHost"
```

- [ ] **Step 4: Pin production embedded subwindows and add the host scene**

Add:

```ini
[display]
window/subwindows/embed_subwindows=true
```

Instance `res://scenes/ui/UIScreenHost.tscn` as `UI/UIScreenHost` after `GameUI`.

- [ ] **Step 5: Configure with `GetNodeOrNull` so synthetic tests survive `_EnterTree()`**

```csharp
private UIScreenHost? _screenHost;
private bool _presentationGameplayBlocked;
private static readonly IReadOnlySet<StringName> GameplayCoreCancelActions =
    new HashSet<StringName> { "pause_menu", "ui_cancel" };

public override void _EnterTree()
{
    _screenHost = GetNodeOrNull<UIScreenHost>("UI/UIScreenHost");
    var gameUi = GetNodeOrNull<Control>("UI/GameUI");

    if (_screenHost != null && gameUi != null)
    {
        _screenHost.Configure(new UIScreenHostOptions
        {
            HudRoot = gameUi,
            CoreCancelActions = GameplayCoreCancelActions,
            RootCancelFallback = HandleGameplayRootCancel,
            GameplayInputBlockChanged = blocked => _presentationGameplayBlocked = blocked
        });
    }

    // Keep the existing GetNodeOrNull FloorManager pending-load setup here.
}
```

For now `HandleGameplayRootCancel` returns `Declined`. Every later host-dependent helper first handles `_screenHost == null` safely.

- [ ] **Step 6: Replace direct scene changes with one private teardown helper**

Do **not** add `PerformSceneChange`.

```csharp
private const string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";
private const string GameScenePath = "res://scenes/game/Game.tscn";
private string? _pendingScenePath;
private bool _sceneChangeCommitted;
private bool _sceneChangeRetryScheduled;

private void RequestSceneChange(string path)
{
    if (_sceneChangeCommitted)
        return;

    _sceneChangeCommitted = true;
    _pendingScenePath = path;
    ContinueSceneChangeAfterUiTeardown();
}

private void ContinueSceneChangeAfterUiTeardown()
{
    _sceneChangeRetryScheduled = false;

    if (_screenHost != null && IsInstanceValid(_screenHost) &&
        _screenHost.PrepareForTeardown() == UIScreenTeardownPreparationStatus.Deferred)
    {
        if (!_sceneChangeRetryScheduled)
        {
            _sceneChangeRetryScheduled = true;
            Callable.From(ContinueSceneChangeAfterUiTeardown).CallDeferred();
        }
        return;
    }

    var path = _pendingScenePath;
    _pendingScenePath = null;
    if (!string.IsNullOrEmpty(path))
        GetTree().ChangeSceneToFile(path);
}

protected virtual void ReturnToMainMenu() => RequestSceneChange(MainMenuScenePath);
```

`PrepareForTeardown()` exceptions propagate; do not invent retry-count policy.

Route existing paths:

- dead-player encounter, defeat timeout, Save/Load Main Menu request continue calling `ReturnToMainMenu()`;
- corrupted-save confirmation calls `RequestSceneChange(MainMenuScenePath)`;
- successful in-game Load sets `PendingLoadData` then calls `RequestSceneChange(GameScenePath)`;
- hosted Return-to-Title confirmation later calls `ReturnToMainMenu()`.

- [ ] **Step 7: Immediately run the two large synthetic suites plus the new host suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: existing synthetic fixtures still enter the tree without an NRE; new real-scene host test passes.

- [ ] **Step 8: Commit**

```bash
git add project.godot scenes/game/Game.tscn scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): bootstrap gameplay screen host"
```

---

## Task 4: Compose presentation blocking into gameplay input

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/game/PlayerController.cs`
- Modify: `tests/game/PlayerControllerTest.cs`

**Interfaces:**
- `PlayerController.GameplayInputSuppressedProvider : Func<bool>?`
- `Game.IsGameplayInputSuppressed()`

- [ ] **Step 1: Write movement + interaction red tests**

When provider returns true, movement and `interact` do nothing. Existing domain guards remain when it returns false.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest.PresentationBlock"
```

- [ ] **Step 3: Implement the optional provider**

```csharp
public Func<bool>? GameplayInputSuppressedProvider { private get; set; }
```

At the top of `_UnhandledInput` after the manager-null guard:

```csharp
if (GameplayInputSuppressedProvider?.Invoke() == true)
    return;
```

Game composes:

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

Wire in `_Ready()`, clear in `_ExitTree()`.

- [ ] **Step 4: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest"
git add scripts/game/Game.cs scripts/game/PlayerController.cs tests/game/PlayerControllerTest.cs
git commit -m "feat(game): compose presentation input blocking"
```

---

## Task 5: Normalize only the four runtime `GridMap` Always overrides

**Files:**
- Modify: `scenes/game/floors/FloorGF.tscn`
- Modify: `scenes/game/floors/Floor1F.tscn`
- Modify: `scenes/game/floors/Floor2F.tscn`
- Modify: `scenes/game/floors/Floor3F.tscn`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Important:** Do **not** change `InventoryMenu.tscn` in this task. Current Inventory still writes `SceneTree.Paused`; removing its `Always` mode before Task 6 would make it unable to close itself.

- [ ] **Step 1: Write the red process audit**

```csharp
[TestCase]
public async Task RuntimeGridMapDoesNotRemainExplicitAlways()
{
    var game = await InstantiateGameScene();
    var grid = game.GetNode<FloorManager>("FloorManager").CurrentGridMap;
    AssertThat(grid.ProcessMode).IsNotEqual(Node.ProcessModeEnum.Always);
}
```

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RuntimeGridMapDoesNotRemainExplicitAlways"
```

- [ ] **Step 3: Remove `process_mode = 3` from each floor's `GridMap` node only**

Do not alter floor roots, `InventoryMenu.tscn`, host layers, or CanvasLayer.

- [ ] **Step 4: Run floor regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RuntimeGridMapDoesNotRemainExplicitAlways|FullyQualifiedName~FloorManagerTest|FullyQualifiedName~GridMap"
```

- [ ] **Step 5: Commit**

```bash
git add scenes/game/floors/FloorGF.tscn scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.tscn scenes/game/floors/Floor3F.tscn tests/game/GameplayPauseHostTest.cs
git commit -m "fix(game): normalize grid processing for pause"
```

---

## Task 6: Move direct Inventory lifecycle under the host atomically

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- `InventoryMenuController.CloseRequested`
- `Control? InitialFocusTarget`
- `Game.TryOpenInventory(UIScreenHandle? parent)`
- `UINodeLifetime.External`

- [ ] **Step 1: Write red ownership tests**

Assert `OpenMenu()`/`CloseMenu()` do not change `SceneTree.Paused`; Close button emits one `CloseRequested`.

- [ ] **Step 2: Write red host parentage + reuse test**

Open direct Inventory through `Game`, then assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
AssertThat(inventory.GetParent()).IsEqual(host.GetNode<Control>("ModalLayer"));
```

Close via `toggle_inventory`, await cleanup, then assert `inventory.GetParent()` is null.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest.DirectInventory"
```

- [ ] **Step 4: Remove all private Inventory pause/Cancel ownership in the same change**

Delete:

- `_pauseSnapshotCaptured`;
- `_treeWasPausedBeforeOpen`;
- `RestoreTreePause()`;
- `GetTree().Paused = true` in `OpenMenu()`;
- the `_Input()` branch that terminally closes on `ui_cancel` / `toggle_inventory`.

Keep `_Input()` only for input-device hint observation if still needed.

Use:

```csharp
[Signal] public delegate void CloseRequestedEventHandler();

public void OpenMenu()
{
    RefreshUI();
    RefreshCloseHint();
    Show();
}

public void CloseMenu() => Hide();
```

Close UI emits `CloseRequested` rather than closing itself.

- [ ] **Step 5: Remove `process_mode = 3` from `InventoryMenu.tscn` now**

This is intentionally in the same commit as deleting the controller pause write.

- [ ] **Step 6: Stop pre-parenting reusable Inventory**

`SetupInventoryMenu()` instantiates `_inventoryMenu` but does not call `UI.AddChild`.

- [ ] **Step 7: Present direct Inventory explicitly**

```csharp
var spec = new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Inventory,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.WhenPaused,
    Parent = null,
    PauseTree = true,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Inherit,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Close,
    EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
    InitialFocus = () => _inventoryMenu.InitialFocusTarget,
    SetPresented = visible =>
    {
        if (visible) _inventoryMenu.OpenMenu();
        else _inventoryMenu.CloseMenu();
    },
    Cleanup = _ => ClearInventoryHandle(),
    NodeLifetime = UINodeLifetime.External
};
```

Guard `_screenHost == null` and treat failure as no open.

- [ ] **Step 8: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest.DirectInventory|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~GameTest"
git add scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs scripts/game/Game.cs tests/ui/InventoryMenuControllerTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): host direct inventory lifecycle"
```

---

## Task 7A: Build the hosted Pause parity path without production cutover

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- `TryOpenPause() : bool`
- `_pauseHandle : UIScreenHandle?`
- `_pauseScreen : PauseScreenController?`
- Parity policy: `ProcessPolicy.Always`, `PauseTree=false`

Production `Game._Input()` / `HandlePauseMenuInput()` still opens the old `PauseMenuDialog` after this task. The new path is invoked directly by integration tests only, so there is no user-visible half-migration.

- [ ] **Step 1: Write red parity tests**

Directly invoke `TryOpenPause()` in the real host fixture. Assert:

- Pause kind active once;
- Pause view parent is `ModalLayer`;
- `IsTreePauseOwned == false`;
- `IsPresentationGameplayBlocked == true`;
- Cursor visible, HUD visible;
- Resume closes and restores state;
- repeated `TryOpenPause()` does not create a second kind.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.PauseParity"
```

- [ ] **Step 3: Implement Pause presentation with only the handlers needed in this task**

Use explicit parity spec:

```csharp
var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Pause,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Visible,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Close,
    InitialFocus = () => screen.InitialFocusTarget,
    Cleanup = _ => ClearPausePresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
});
```

Connect Resume now. Do not connect the remaining five action signals until Task 7B defines all five hosted handlers.

- [ ] **Step 4: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.PauseParity|FullyQualifiedName~PauseScreenControllerTest"
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): add hosted pause parity path"
```

---

## Task 7B: Complete all hosted Pause children while legacy production Pause remains active

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Hosted Inventory child via existing reusable view
- Hosted Settings child
- Hosted Save/Load child
- Hosted Return confirmation child

The legacy `PauseMenuDialog` remains the production root path during this task. Temporary legacy child constructors may coexist until Task 7C; do not route production Cancel to the new path yet.

- [ ] **Step 1: Write red child parentage + return tests**

For a directly invoked hosted Pause, press each action and assert:

- Inventory parent = `host/ModalLayer`, logical parent = Pause; close detaches reusable view.
- Settings parent = `host/ModalLayer`, logical parent = Pause.
- Save/Load parent = `host` itself, logical parent = Pause.
- Return confirmation parent = `host/ModalLayer`, logical parent = Pause.
- Closing each child returns to the same Pause entry/focus; no Pause recreation.

If the fixture uses `SubViewport`, set `GuiEmbedSubwindows = true` before opening Save/Load.

- [ ] **Step 2: Write red nested-Cancel tests**

- Save/Load active overwrite child dismisses that child first.
- Settings popup/rebinding reserves Cancel for the retained handler.
- Confirmation Cancel closes confirmation only.
- Inventory `toggle_inventory` closes Inventory child only.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.PauseChild|FullyQualifiedName~GameplayPauseHostTest.HostedSaveLoad|FullyQualifiedName~GameplayPauseHostTest.HostedSettings"
```

- [ ] **Step 4: Connect all five remaining Pause signals now**

Define all child handlers first, then bind `InventoryRequested`, `SaveRequested`, `LoadRequested`, `SettingsRequested`, and `ReturnToTitleRequested` in one change. No partially wired production-facing controller state remains after this task.

- [ ] **Step 5: Reuse detached Inventory with Pause parent**

Use the Task 6 spec with only these differences:

```csharp
Parent = _pauseHandle.Value,
ProcessPolicy = UIProcessPolicy.Always,
PauseTree = false,
BlockGameplayInput = false,
```

Keep `Hud = Inherit` and `NodeLifetime.External`.

- [ ] **Step 6: Instantiate Settings unparented and host it explicitly**

Do not call `UI.AddChild`.

```csharp
var settings = scene.Instantiate<SettingsMenuController>();
var result = _screenHost.TryPresent(settings, new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Settings,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    Parent = _pauseHandle,
    PauseTree = false,
    BlockGameplayInput = false,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Inherit,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Close,
    InterceptCancel = _ =>
        settings.IsRebinding || settings.IsPopupOpen
            ? UIInputInterception.ReserveForNativeHandler
            : UIInputInterception.DeferToPolicy,
    SetPresented = visible =>
    {
        if (visible) settings.OpenSettings(showOverlay: false);
        else settings.Hide();
    },
    Cleanup = _ => ClearHostedSettings(settings),
    NodeLifetime = UINodeLifetime.QueueFree
});
```

Keep existing settings validation/domain logic.

- [ ] **Step 7: Instantiate Save/Load unparented and host its Window**

Do not call `UI.AddChild`.

Use explicit Save/Load spec with `Layer=Modal`, `Parent=_pauseHandle`, `ProcessPolicy=Always`, `PauseTree=false`, `Hud=Inherit`, `NodeLifetime=QueueFree`.

`InterceptCancel` dismisses `HasActiveChildDialog` first; otherwise defers to `Cancel=Close`.

Presentation uses existing `ShowDialog(mode)` after host attachment.

- [ ] **Step 8: Host Return confirmation explicitly**

Use `InputPriority.Blocking`, `ExclusiveGroup=UIScreenExclusiveGroups.BlockingPrompt`, `InitialFocus=confirmation.InitialFocusTarget`, and `NodeLifetime.QueueFree`.

Confirm calls `ReturnToMainMenu()`. `_sceneChangeCommitted` in `RequestSceneChange` suppresses repeated commits.

- [ ] **Step 9: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): host pause child flows"
```

---

## Task 7C: Cut production Cancel/Pause over and delete the legacy dialog

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete: `scripts/ui/PauseMenuDialog.cs`
- Delete: `tests/ui/PauseMenuDialogTest.cs`

- [ ] **Step 1: Migrate `GameTest` fixture/assertions to host-aware cases**

Task 3 deliberately kept synthetic fixtures host-optional. For tests that now assert hosted Pause state, either:

- move the behavior to `GameplayPauseHostTest` when it needs the real scene, or
- create a minimal `UI/GameUI/UIScreenHost` subtree before adding `TestableGame` to its viewport.

Do not force every unrelated `GameTest` case to load `Game.tscn`.

Any synthetic `SubViewport` that hosts Save/Load sets `GuiEmbedSubwindows=true`.

Replace reflection on `_pauseMenuDialog`, `_pauseMenuRestorePending`, and `_saveLoadFromPause` with host kind/handle assertions.

- [ ] **Step 2: Write root physical/cancel red tests**

Prove both `pause_menu` and `ui_cancel` open hosted Pause when no blocker exists.

Preserve order:

```text
active error -> consume/dismiss
battle -> existing escape/result path
puzzle -> decline for retained native dialog
world interaction -> consume/no Pause
NPC -> decline for retained native dialog
otherwise -> open hosted Pause
```

- [ ] **Step 3: Run red before cutover**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RootCancel|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest"
```

- [ ] **Step 4: Move root Cancel to `UIScreenHost`**

Remove the production `pause_menu -> HandlePauseMenuInput()` branch from `Game._Input()`.

Implement `HandleGameplayRootCancel` using the final domain ladder above. Do not gate root Pause on `pause_menu` only; both configured core actions are valid.

- [ ] **Step 5: Remove legacy child attachment/restoration paths now that production is hosted**

Delete old `UI.AddChild` construction for legacy Settings/Save/Load paths that are no longer used from Pause. Keep domain callbacks, but terminal hosted cleanup closes host handles rather than restoring a hidden Pause dialog.

- [ ] **Step 6: Delete legacy Pause state**

Delete:

- `PauseMenuDialog.cs` and test;
- `_pauseMenuDialog`;
- `_pauseMenuRestorePending`;
- `_saveLoadFromPause`;
- `ShowPauseMenu`, `CleanupPauseMenu`, `RestorePauseMenuAfterSettings`;
- obsolete hosted-replaced branches from `HandlePauseMenuInput`.

Run:

```bash
rg -n "PauseMenuDialog|_pauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause" scripts tests
```

Expected: zero production/test matches.

- [ ] **Step 7: Run focused cutover suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
```

Expected: selected tests PASS while hosted Pause still has `PauseTree=false`.

- [ ] **Step 8: Commit**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs
git rm scripts/ui/PauseMenuDialog.cs tests/ui/PauseMenuDialogTest.cs
git commit -m "feat(ui): cut gameplay pause over to screen host"
```

---

## Task 8: Flip root Pause to host-owned tree pause and prove gameplay freezes

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameTest.cs`

**Interfaces:**
- Final Pause: `ProcessPolicy.WhenPaused`, `PauseTree=true`
- Children remain `PauseTree=false`.

- [ ] **Step 1: Change parity assertion to demand tree ownership**

Assert `host.CurrentState.IsTreePauseOwned` and `SceneTree.Paused` are true after opening Pause.

- [ ] **Step 2: Add real-scene freeze probe below runtime `GridMap`**

A test-only pausable Node increments in `_Process`.

```csharp
int before = probe.ProcessCount;
PushAction("pause_menu");
await AwaitFrames(3);
AssertThat(probe.ProcessCount).IsEqual(before);

PushAction("pause_menu");
await AwaitFrames(3);
AssertThat(probe.ProcessCount).IsGreater(before);
```

This simultaneously proves the Always host still receives Cancel while gameplay is frozen.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RootPauseOwnsTreePause|FullyQualifiedName~GameplayPauseHostTest.GameplayProbe"
```

- [ ] **Step 4: Flip only the gated Pause fields**

```csharp
ProcessPolicy = UIProcessPolicy.WhenPaused,
PauseTree = true,
```

Do not alter child pause ownership.

- [ ] **Step 5: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorManagerTest"
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameTest.cs
git commit -m "feat(ui): enable host-owned gameplay pause"
```

---

## Task 9: Harden physical Cancel, focus restoration, and teardown regressions

**Files:**
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `scripts/game/Game.cs` only if a test exposes a production defect.

- [ ] **Step 1: Add physical-input precedence cases**

Cover:

```text
Settings popup/key capture -> retained handler first
Save/Load overwrite -> child first
hosted child -> close child, Pause remains
Pause -> Resume
error -> dismiss, no Pause
battle -> escape/result path, no Pause
puzzle -> retained dialog, no Pause
world interaction -> consume, no Pause
NPC -> retained dialog, no Pause
root ui_cancel -> Pause
root pause_menu -> Pause
```

Use physical/configured events in `GameInputLifecycleTest`.

- [ ] **Step 2: Add invalid-focus regression**

Focus a gameplay Control, open Pause, free prior focus target, close Pause, assert no exception and no stuck restoration lease.

- [ ] **Step 3: Add teardown with child regression**

Open Pause + Settings or Save/Load, call host `PrepareForTeardown`, assert descendants close and leases restore. Separately verify Return confirmation calls the existing `ReturnToMainMenu` seam once when synthetic navigation counting is desired.

Do not add `PerformSceneChange`.

- [ ] **Step 4: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest"
git add tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs scripts/game/Game.cs
git commit -m "test(ui): lock gameplay pause lifecycle"
```

Omit `Game.cs` if unchanged.

---

## Task 10: Update lifecycle documentation and run final gates

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Record final ownership facts**

```text
Game owns one local production UIScreenHost but synthetic test Games may omit it.
Pause owns the final gameplay tree-pause lease.
Direct Inventory uses the host and no longer writes SceneTree.Paused.
Inventory HUD policy remains inherited in HPA-382.
Pause children do not acquire a second pause lease.
Hosted views are attached by UIScreenHost, never pre-parented by Game.
GridMap runtime nodes inherit pausable gameplay processing.
Scene replacement waits for UIScreenHost.PrepareForTeardown() == Complete.
```

- [ ] **Step 2: Search for stale ownership / Always modes**

```bash
rg -n "PauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause|_pauseSnapshotCaptured|_treeWasPausedBeforeOpen" scripts tests scenes
rg -n "process_mode = 3" scenes/game/floors scenes/ui/InventoryMenu.tscn
```

Expected: no legacy ownership matches; no runtime `GridMap`/Inventory root `Always` match.

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln
```

Expected: exit 0.

- [ ] **Step 4: Run focused migration suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorManagerTest"
```

Expected: zero failures.

- [ ] **Step 5: Run full .NET/GdUnit4 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: zero failures.

- [ ] **Step 6: Commit docs**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record gameplay screen host ownership"
```

---

## Review Gate Checklist

Before implementation is declared complete:

- [ ] Real `Game.tscn` owns exactly one host; synthetic `new Game()` fixtures do not NRE in `_EnterTree()`.
- [ ] Test SubViewports that host Save/Load explicitly enable embedded subwindows.
- [ ] Pause Control parent is `ModalLayer`.
- [ ] Inventory attaches to `ModalLayer`, detaches on `External` close, and reopens with a different logical parent only after a real close.
- [ ] Settings attaches to `ModalLayer` without `Game.UI.AddChild`.
- [ ] Save/Load Window attaches directly to `UIScreenHost` without `Game.UI.AddChild`.
- [ ] Return confirmation attaches to `ModalLayer`.
- [ ] Inventory process-mode change and private pause-write deletion land atomically.
- [ ] Inventory controller no longer terminally handles `ui_cancel` / `toggle_inventory` itself.
- [ ] `GameTest` and `GameInputLifecycleTest` pass immediately after Task 3 host bootstrap.
- [ ] Hosted Pause parity passes with `PauseTree=false` before production cutover.
- [ ] Production cutover passes before tree-pause flip.
- [ ] Four runtime `GridMap` nodes no longer pin `Always`.
- [ ] Final Pause owns `SceneTree.Paused`; real gameplay probe freezes; host Cancel still resumes.
- [ ] No extra `PerformSceneChange` or generic child-spec factory exists.
- [ ] Every `Game` scene replacement is teardown-safe in production.
- [ ] Focused and full suites pass before merge.

## Execution Handoff

Implement task-by-task. Do not combine Task 5 with Task 6, do not combine Task 7A/7B with Task 7C, and do not skip the `PauseTree=false` checkpoint before Task 8.