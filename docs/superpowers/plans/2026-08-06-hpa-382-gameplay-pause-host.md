# Gameplay Pause Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make gameplay `UIScreenHost` the sole presentation authority for Pause and the screens Pause opens, while safely introducing root tree pause only after the HPA-378 process and lifecycle gates pass.

**Architecture:** `Game.tscn` owns one scene-local host configured by `Game._EnterTree()`. `Game` translates UI actions into host entries and delegates domain work to existing controllers. Direct Inventory moves to the host first; production Pause and its children then migrate with `PauseTree=false` for lifecycle parity; root Pause flips to `PauseTree=true` only after explicit-`Always` gameplay processing is normalized and freeze regressions pass.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing `UIScreenHost`, `SiriusModalShell`, `SiriusTheme`, `InputHintPresenter`.

## Global Constraints

- Follow `docs/superpowers/specs/2026-08-06-hpa-382-gameplay-pause-host-design.md`.
- Honor HPA-378 section 19 migration order: no root `PauseTree=true` before teardown, process audit, HUD/host wiring, and composed input blocking are proven.
- Keep `GameManager`, `SaveManager`, `InventoryMenuController`, `SaveLoadDialog`, and `SettingsMenuController` as domain owners.
- Do not add an autoload, navigation service, modal manager, screen registry, DI container, or generic confirmation framework.
- Do not redesign child screens or migrate battle/NPC/puzzle presentation in this ticket.
- Do not preserve `PauseMenuDialog` compatibility after the hosted replacement is green.
- Keep legacy Cancel ladder arms until the corresponding hosted replacement is active in the same task.
- Required viewport checks are 1280×720 and 640×360 only.
- Use test-first changes and commit each task after its focused tests pass.

---

## File Structure

### Create

- `scenes/ui/PauseScreen.tscn` — scene-authored Pause composition.
- `scripts/ui/PauseScreenController.cs` — presentation-only action signals, focus target, hints, compact refresh.
- `scenes/ui/PauseReturnToTitleConfirmation.tscn` — flow-specific destructive confirmation.
- `scripts/ui/PauseReturnToTitleConfirmationController.cs` — confirmation signals and safe focus.
- `tests/ui/PauseScreenControllerTest.cs` — component behavior and 1280×720 / 640×360 checks.
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs` — confirmation component behavior.
- `tests/game/GameplayPauseHostTest.cs` — production host, parentage, stack, process, teardown, and reuse regressions.

### Modify

- `project.godot` — pin embedded subwindows.
- `scenes/game/Game.tscn` — add one `UIScreenHost` instance.
- `scripts/game/Game.cs` — host configuration, composed predicate, Inventory/Pause child adapters, Cancel fallback, teardown-safe scene changes.
- `scripts/game/PlayerController.cs` — optional presentation-suppression provider.
- `scenes/game/floors/FloorGF.tscn` — remove explicit `Always` from runtime `GridMap`.
- `scenes/game/floors/Floor1F.tscn` — remove explicit `Always` from runtime `GridMap`.
- `scenes/game/floors/Floor2F.tscn` — remove explicit `Always` from runtime `GridMap`.
- `scenes/game/floors/Floor3F.tscn` — remove explicit `Always` from runtime `GridMap`.
- `scenes/ui/InventoryMenu.tscn` — remove explicit root `Always`; host chooses process mode.
- `scripts/ui/InventoryMenuController.cs` — remove private tree-pause ownership and expose host close signal/focus target.
- `tests/game/GameTest.cs` — migrate legacy Pause assertions and Cancel/child regressions to host state.
- `tests/game/GameInputLifecycleTest.cs` — keep physical-input/domain precedence coverage against hosted Pause.
- `tests/game/PlayerControllerTest.cs` — presentation-block coverage.
- `tests/ui/InventoryMenuControllerTest.cs` — no private pause ownership and close signal coverage.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — record production host ownership and PauseTree gate.

### Delete

- `scripts/ui/PauseMenuDialog.cs`
- `tests/ui/PauseMenuDialogTest.cs`

---

## Task 1: Build the scene-authored Pause component

**Files:**
- Create: `scenes/ui/PauseScreen.tscn`
- Create: `scripts/ui/PauseScreenController.cs`
- Create: `tests/ui/PauseScreenControllerTest.cs`

**Interfaces:**
- Produces: `Control InitialFocusTarget`
- Produces signals: `ResumeRequested`, `InventoryRequested`, `SaveRequested`, `LoadRequested`, `SettingsRequested`, `ReturnToTitleRequested`
- Consumes: `SiriusModalShell`, `InputHintPresenter`

- [ ] **Step 1: Write the failing component test**

Create `tests/ui/PauseScreenControllerTest.cs` with exact action/focus assertions:

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

    AssertThat(resume).IsEqual(1);
    AssertThat(inventory).IsEqual(1);
    AssertThat(save).IsEqual(1);
    AssertThat(load).IsEqual(1);
    AssertThat(settings).IsEqual(1);
    AssertThat(title).IsEqual(1);
}
```

Add a compact test that instantiates at 640×360 and asserts each action has at least 40 logical pixels height and the modal panel fits the viewport.

- [ ] **Step 2: Run the focused test and confirm red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
```

Expected: FAIL because scene/controller do not exist.

- [ ] **Step 3: Implement the presentation-only controller**

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
        GetNode<Button>("%ResumeButton").Pressed += OnResume;
        GetNode<Button>("%InventoryButton").Pressed += OnInventory;
        GetNode<Button>("%SaveButton").Pressed += OnSave;
        GetNode<Button>("%LoadButton").Pressed += OnLoad;
        GetNode<Button>("%SettingsButton").Pressed += OnSettings;
        GetNode<Button>("%ReturnToTitleButton").Pressed += OnReturnToTitle;
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        var size = GetViewportRect().Size;
        _shell.Compact = size.X < 800 || size.Y < 450;
        _shell.RefreshPresentation(size);
    }

    private void OnResume() => EmitSignal(SignalName.ResumeRequested);
    private void OnInventory() => EmitSignal(SignalName.InventoryRequested);
    private void OnSave() => EmitSignal(SignalName.SaveRequested);
    private void OnLoad() => EmitSignal(SignalName.LoadRequested);
    private void OnSettings() => EmitSignal(SignalName.SettingsRequested);
    private void OnReturnToTitle() => EmitSignal(SignalName.ReturnToTitleRequested);
}
```

Disconnect the six button subscriptions in `_ExitTree()` using the same bound methods. Do not add navigation or domain references.

- [ ] **Step 4: Author the scene**

Use this authored structure:

```text
PauseScreen (Control, full rect)
└── ModalShell (SiriusModalShell instance, %ModalShell, title "Paused")
    └── .../BodyHost/PauseActions (VBoxContainer)
        ├── ResumeButton        "Resume"
        ├── InventoryButton     "Inventory"
        ├── SaveButton          "Save"
        ├── LoadButton          "Load"
        ├── SettingsButton      "Settings"
        └── ReturnToTitleButton "Return to Title"
```

Set each button minimum height to 40 and `ExpandFill`. Reuse the existing destructive variation for Return to Title.

- [ ] **Step 5: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest"
git add scenes/ui/PauseScreen.tscn scripts/ui/PauseScreenController.cs tests/ui/PauseScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored pause screen"
```

Expected: selected tests PASS.

---

## Task 2: Build the flow-specific Return-to-Title confirmation

**Files:**
- Create: `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- Create: `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- Create: `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

**Interfaces:**
- Produces: `Control InitialFocusTarget`
- Produces signals: `ReturnToTitleConfirmed`, `CancelRequested`
- Navigation duplicate suppression remains in `Game`, not this controller.

- [ ] **Step 1: Write failing controller tests**

```csharp
[TestCase]
public async Task CancelOwnsInitialFocusAndButtonsOnlyEmitSignals()
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

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
```

Expected: FAIL because the component is missing.

- [ ] **Step 3: Implement the narrow controller and scene**

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
}
```

Author `SiriusModalShell` title `Return to Title?`, body `Unsaved progress will be lost.`, actions `Cancel | Return to Title`, with destructive styling only on the final action.

- [ ] **Step 4: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
git add scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "feat(ui): add pause return-to-title confirmation"
```

Expected: selected tests PASS.

---

## Task 3: Bootstrap the gameplay host and teardown-safe scene changes

**Files:**
- Modify: `project.godot`
- Modify: `scenes/game/Game.tscn`
- Modify: `scripts/game/Game.cs`
- Create: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Produces scene node: `UI/UIScreenHost : UIScreenHost`
- Produces: `RequestSceneChange(string path)`
- Produces: `ContinueSceneChangeAfterUiTeardown()`
- Produces test seam: `protected virtual void PerformSceneChange(string path)`

- [ ] **Step 1: Write failing production-host and teardown tests**

```csharp
[TestCase]
public async Task GameSceneOwnsOneReadyHostWithEmbeddedSubwindows()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

    AssertThat(host).IsNotNull();
    AssertThat(host.Diagnostics.SubwindowEmbeddingEnabled).IsTrue();
    AssertThat(host.ActiveEntries.Count).IsEqual(0);
}
```

Add a teardown test using a `TestableGame` override of `PerformSceneChange` and a hosted disposable entry. Assert navigation callback is not invoked until `PrepareForTeardown()` has emptied the host.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.GameSceneOwnsOneReadyHost|FullyQualifiedName~GameplayPauseHostTest.SceneChange"
```

Expected: FAIL because the host/helper do not exist.

- [ ] **Step 3: Pin embedded subwindows and add host scene instance**

Add:

```ini
[display]
window/subwindows/embed_subwindows=true
```

Add `res://scenes/ui/UIScreenHost.tscn` as the final child of `UI`, named `UIScreenHost`. Keep `GameUI` at `UI/GameUI`.

- [ ] **Step 4: Configure the host in `_EnterTree()`**

Add:

```csharp
private UIScreenHost _screenHost = null!;
private bool _presentationGameplayBlocked;
private static readonly IReadOnlySet<StringName> GameplayCoreCancelActions =
    new HashSet<StringName> { "pause_menu", "ui_cancel" };
```

At the start of `_EnterTree()`:

```csharp
_screenHost = GetNode<UIScreenHost>("UI/UIScreenHost");
_screenHost.Configure(new UIScreenHostOptions
{
    HudRoot = GetNode<Control>("UI/GameUI"),
    CoreCancelActions = GameplayCoreCancelActions,
    RootCancelFallback = HandleGameplayRootCancel,
    GameplayInputBlockChanged = blocked => _presentationGameplayBlocked = blocked
});
```

For now `HandleGameplayRootCancel` returns `Declined`.

- [ ] **Step 5: Add the scene-change helper**

```csharp
private string? _pendingScenePath;
private bool _sceneChangeCommitted;

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
    if (_screenHost != null && IsInstanceValid(_screenHost) &&
        _screenHost.PrepareForTeardown() == UIScreenTeardownPreparationStatus.Deferred)
    {
        Callable.From(ContinueSceneChangeAfterUiTeardown).CallDeferred();
        return;
    }

    var path = _pendingScenePath;
    _pendingScenePath = null;
    if (!string.IsNullOrEmpty(path))
        PerformSceneChange(path);
}

protected virtual void PerformSceneChange(string path) =>
    GetTree().ChangeSceneToFile(path);
```

Route in-game Load and the concrete Return-to-Title path through `RequestSceneChange`; keep existing domain validation and pending-save handoff unchanged.

- [ ] **Step 6: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest"
git add project.godot scenes/game/Game.tscn scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): bootstrap gameplay screen host"
```

Expected: selected host/teardown tests PASS.

---

## Task 4: Compose presentation blocking into gameplay input

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/game/PlayerController.cs`
- Modify: `tests/game/PlayerControllerTest.cs`

**Interfaces:**
- Produces: `PlayerController.GameplayInputSuppressedProvider : Func<bool>?`
- Consumes: `Game.IsGameplayInputSuppressed`

- [ ] **Step 1: Write failing movement and interaction tests**

```csharp
[TestCase]
public void PresentationBlockPreventsMovement()
{
    var controller = CreateReadyController();
    controller.GameplayInputSuppressedProvider = () => true;
    var before = _gridMap.GetPlayerPosition();

    controller._UnhandledInput(new InputEventKey
    {
        Keycode = Key.Right,
        Pressed = true
    });

    AssertThat(_gridMap.GetPlayerPosition()).IsEqual(before);
}
```

Add a second test that sends `interact` and asserts no treasure/puzzle interaction request occurs while the provider returns true.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest.PresentationBlock"
```

Expected: FAIL because the provider is missing.

- [ ] **Step 3: Implement the composed predicate**

In `Game`:

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

In `PlayerController`:

```csharp
public Func<bool>? GameplayInputSuppressedProvider { private get; set; }
```

At the start of `_UnhandledInput`, after the null manager guard:

```csharp
if (GameplayInputSuppressedProvider?.Invoke() == true)
    return;
```

Wire in `Game._Ready()` and clear in `_ExitTree()`.

- [ ] **Step 4: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PlayerControllerTest"
git add scripts/game/Game.cs scripts/game/PlayerController.cs tests/game/PlayerControllerTest.cs
git commit -m "feat(game): compose presentation input blocking"
```

Expected: all selected player-controller tests PASS.

---

## Task 5: Normalize explicit `Always` gameplay processing before root Pause

**Files:**
- Modify: `scenes/game/floors/FloorGF.tscn`
- Modify: `scenes/game/floors/Floor1F.tscn`
- Modify: `scenes/game/floors/Floor2F.tscn`
- Modify: `scenes/game/floors/Floor3F.tscn`
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Produces: gameplay/floor nodes inherit pausable process mode.
- Preserves: host/presentation layers remain `Always` through `UIScreenHost.tscn`.

- [ ] **Step 1: Write the failing process audit regression**

Add a real-scene assertion before editing scenes:

```csharp
[TestCase]
public async Task RuntimeGridMapDoesNotRemainExplicitAlways()
{
    var game = await InstantiateGameScene();
    var grid = game.GetNode<FloorManager>("FloorManager").CurrentGridMap;

    AssertThat(grid.ProcessMode).IsNotEqual(Node.ProcessModeEnum.Always);
}
```

Add the same assertion for an instantiated `InventoryMenu.tscn` root.

- [ ] **Step 2: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RuntimeGridMapDoesNotRemainExplicitAlways"
```

Expected: FAIL because current `GridMap` / Inventory roots are `Always`.

- [ ] **Step 3: Remove only the known explicit gameplay overrides**

In each floor scene, remove:

```ini
process_mode = 3
```

from the `GridMap` node. Do not change the floor root, host, CanvasLayer, or presentation layers.

Remove the same line from the root `InventoryMenu` Control; host registration will set its process mode when presented.

- [ ] **Step 4: Run the audit plus floor regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RuntimeGridMapDoesNotRemainExplicitAlways|FullyQualifiedName~FloorManagerTest|FullyQualifiedName~GridMap"
```

Expected: selected tests PASS.

- [ ] **Step 5: Commit the process normalization**

```bash
git add scenes/game/floors/FloorGF.tscn scenes/game/floors/Floor1F.tscn scenes/game/floors/Floor2F.tscn scenes/game/floors/Floor3F.tscn scenes/ui/InventoryMenu.tscn tests/game/GameplayPauseHostTest.cs
git commit -m "fix(game): normalize gameplay process modes for pause"
```

---

## Task 6: Move direct Inventory lifecycle under the host

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Interfaces:**
- Produces signal: `InventoryMenuController.CloseRequested`
- Produces: `Control? InitialFocusTarget`
- Produces: `Game.TryOpenInventory(UIScreenHandle? parent) : bool`
- Reusable Inventory lifetime: `UINodeLifetime.External`

- [ ] **Step 1: Write failing controller ownership tests**

```csharp
[TestCase]
public void OpenAndCloseDoNotWriteSceneTreePause()
{
    bool incoming = GetTree().Paused;
    _menu.OpenMenu();
    AssertThat(GetTree().Paused).IsEqual(incoming);
    _menu.CloseMenu();
    AssertThat(GetTree().Paused).IsEqual(incoming);
}
```

Add a Close-button test asserting one `CloseRequested` signal instead of direct pause restoration.

- [ ] **Step 2: Write failing host-parentage/reuse test**

```csharp
[TestCase]
public async Task DirectInventoryAttachesToModalLayerAndDetachesOnClose()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

    InvokePrivate(game, "TryOpenInventory", null);
    var inventory = GetPrivateField<InventoryMenuController>(game, "_inventoryMenu");

    AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsTrue();
    AssertThat(inventory.GetParent())
        .IsEqual(host.GetNode<Control>("ModalLayer"));

    PushAction("toggle_inventory");
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.Inventory)).IsFalse();
    AssertThat(inventory.GetParent()).IsNull();
}
```

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest.DirectInventory"
```

Expected: FAIL because Inventory still owns pause and is pre-parented under `UI`.

- [ ] **Step 4: Remove private Inventory pause ownership**

Delete `_pauseSnapshotCaptured`, `_treeWasPausedBeforeOpen`, and `RestoreTreePause()`.

Make lifecycle methods presentation-only:

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

The controller does not terminally close itself from `ui_cancel` / `toggle_inventory` while hosted. Close button emits `CloseRequested`.

- [ ] **Step 5: Stop pre-parenting the reusable Inventory view**

Change `SetupInventoryMenu()` so it instantiates the scene and stores `_inventoryMenu`, but does **not** call:

```csharp
GetNode("UI").AddChild(_inventoryMenu);
```

Do not use `_inventoryMenu.Visible` as the open-state source.

- [ ] **Step 6: Implement direct host presentation**

Use this policy when `parent == null`:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Inventory,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.WhenPaused,
    Parent = null,
    PauseTree = true,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
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
}
```

On `CloseRequested`, call `TryClose` on the current Inventory handle.

Game's direct `toggle_inventory` path opens Inventory only when no hosted Inventory is already active and `IsGameplayInputSuppressed()` is false.

- [ ] **Step 7: Run green and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest.DirectInventory|FullyQualifiedName~PlayerControllerTest"
git add scripts/ui/InventoryMenuController.cs scripts/game/Game.cs tests/ui/InventoryMenuControllerTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): host direct inventory lifecycle"
```

Expected: selected tests PASS; direct Inventory still pauses the world, now through the host.

---

## Task 7: Migrate production Pause and all direct Pause children with `PauseTree=false`

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete: `scripts/ui/PauseMenuDialog.cs`
- Delete: `tests/ui/PauseMenuDialogTest.cs`

**Interfaces:**
- Produces: `TryOpenPause() : bool`
- Produces child helpers: `TryOpenInventory(pauseHandle)`, `TryOpenSaveLoad(...)`, `TryOpenSettings(...)`, `TryOpenReturnToTitleConfirmation(...)`
- Parity Pause policy uses `ProcessPolicy.Always`, `PauseTree=false`.

- [ ] **Step 1: Write failing parity and parent/reuse tests**

```csharp
[TestCase]
public async Task PauseParityBlocksGameplayWithoutOwningTreePause()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");
    bool incomingPaused = GetTree().Paused;

    PushAction("pause_menu");
    await AwaitFrames(2);

    AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
    AssertThat(host.CurrentState.IsTreePauseOwned).IsFalse();
    AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
    AssertThat(GetTree().Paused).IsEqual(incomingPaused);
    AssertThat(FindEntryView(host, UIScreenKinds.Pause).GetParent())
        .IsEqual(host.GetNode<Control>("ModalLayer"));
}
```

Add:

- repeated Pause closes the same host entry;
- Resume closes it;
- direct Inventory close -> Pause -> Inventory reuses the same detached instance with `Parent=pauseHandle`;
- child close restores the same Pause instance/focus;
- Return-to-Title confirmation is child-first and `_sceneChangeCommitted` allows one navigation callback;
- Save overwrite child is dismissed before Save/Load closes;
- Settings dropdown/key capture reserves Cancel before Settings closes;
- `ui_cancel` at gameplay root opens Pause just like `pause_menu`.

- [ ] **Step 2: Migrate `GameTest.cs` expectations before deleting legacy fields**

Replace direct legacy reflection with host assertions. For example:

```csharp
AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
```

becomes:

```csharp
var host = _game.GetNode<UIScreenHost>("UI/UIScreenHost");
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
```

Replace `InvokePauseMenu()`-style legacy helpers with a production root-cancel event or `TryOpenPause()` invocation.

Run:

```bash
rg -n "PauseMenuDialog|_pauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause" tests/game/GameTest.cs
```

Expected after migration: no references to deleted Pause fields/types.

- [ ] **Step 3: Implement Pause host presentation with parity policy**

```csharp
private bool TryOpenPause()
{
    if (_screenHost.IsKindActive(UIScreenKinds.Pause))
        return true;

    var screen = GD.Load<PackedScene>("res://scenes/ui/PauseScreen.tscn")
        ?.Instantiate<PauseScreenController>();
    if (screen == null)
        return false;

    ConnectPauseScreen(screen);
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

    if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
    {
        DisconnectPauseScreen(screen);
        screen.Free();
        return false;
    }

    _pauseScreen = screen;
    _pauseHandle = result.Handle.Value;
    return true;
}
```

Wire all six Pause signals **in this task**, after all six handlers exist. Do not leave partially wired buttons in an earlier task.

- [ ] **Step 4: Present Inventory from Pause using the reusable detached instance**

Use the direct Inventory policy from Task 6 with these differences:

```csharp
Parent = _pauseHandle.Value,
ProcessPolicy = UIProcessPolicy.Always,
PauseTree = false,
BlockGameplayInput = false,
```

Keep `NodeLifetime.External`. Do not reparent or replace an already-active Inventory entry; close first, then present with the new parent.

- [ ] **Step 5: Host Save/Load and Settings as logical children**

Save/Load policy:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.SaveLoad,
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
    InterceptCancel = context =>
        _saveLoadDialog?.HasActiveChildDialog == true
            ? DismissSaveChildAndConsume()
            : UIInputInterception.DeferToPolicy,
    NodeLifetime = UINodeLifetime.QueueFree
}
```

Settings policy uses the same parent/layer/process values and:

```csharp
InterceptCancel = context =>
    _settingsMenu != null && (_settingsMenu.IsRebinding || _settingsMenu.IsPopupOpen)
        ? UIInputInterception.ReserveForNativeHandler
        : UIInputInterception.DeferToPolicy
```

Keep existing save/settings domain callbacks and validation. Remove `_saveLoadFromPause` after hosted parentage replaces it.

- [ ] **Step 6: Host the Return-to-Title confirmation**

Use:

```csharp
new UIScreenEntrySpec
{
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
    NodeLifetime = UINodeLifetime.QueueFree
}
```

`ReturnToTitleConfirmed` calls `RequestSceneChange("res://scenes/ui/MainMenu.tscn")`. Duplicate suppression comes from `Game._sceneChangeCommitted`, not the controller.

- [ ] **Step 7: Preserve the complete unhosted domain Cancel ladder in root fallback**

After child adapters are live, replace only the hosted branches with host ownership and keep the remaining order:

```csharp
private UIRootCancelResult HandleGameplayRootCancel(UIRootCancelContext context)
{
    if (_activeErrorPopup != null && IsInstanceValid(_activeErrorPopup))
    {
        _activeErrorPopup.QueueFree();
        _activeErrorPopup = null;
        return UIRootCancelResult.Consumed;
    }

    if ((_battleManager != null && IsInstanceValid(_battleManager) && _battleManager.Visible)
        || _gameManager.IsInBattle)
    {
        EscapeBattleUsingExistingPath();
        return UIRootCancelResult.Consumed;
    }

    if (_puzzleRiddleDialog != null && IsInstanceValid(_puzzleRiddleDialog))
        return UIRootCancelResult.Declined;

    if (_gameManager.IsInWorldInteraction)
        return UIRootCancelResult.Consumed;

    if (_gameManager.IsInNpcInteraction)
        return UIRootCancelResult.Declined;

    return TryOpenPause()
        ? UIRootCancelResult.Consumed
        : UIRootCancelResult.Declined;
}
```

Do not gate Pause opening on `pause_menu` only. Both configured core actions are valid gameplay-root Cancel actions.

- [ ] **Step 8: Delete legacy Pause state only after hosted replacements compile**

Delete:

- `PauseMenuDialog.cs` and its test;
- `_pauseMenuDialog`;
- `_pauseMenuRestorePending`;
- `_saveLoadFromPause`;
- `ShowPauseMenu`, `CleanupPauseMenu`, `RestorePauseMenuAfterSettings`, and the old `HandlePauseMenuInput` ladder branches now replaced by the host.

Run:

```bash
rg -n "PauseMenuDialog|_pauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause" scripts tests
```

Expected: zero production/test references.

- [ ] **Step 9: Run focused parity suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~PauseReturnToTitleConfirmationControllerTest"
```

Expected: selected tests PASS while root Pause still reports `IsTreePauseOwned == false`.

- [ ] **Step 10: Commit the parity migration**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs
git rm scripts/ui/PauseMenuDialog.cs tests/ui/PauseMenuDialogTest.cs
git commit -m "feat(ui): migrate pause stack to screen host"
```

---

## Task 8: Flip root Pause to host-owned tree pause and prove gameplay freezes

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameTest.cs`

**Interfaces:**
- Final Pause policy: `ProcessPolicy.WhenPaused`, `PauseTree=true`.
- Children retain `PauseTree=false`.

- [ ] **Step 1: Change the parity test to demand real tree-pause ownership**

```csharp
[TestCase]
public async Task RootPauseOwnsTreePauseAfterMigrationGate()
{
    var game = await InstantiateGameScene();
    var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");

    PushAction("pause_menu");
    await AwaitFrames(2);

    AssertThat(host.CurrentState.IsTreePauseOwned).IsTrue();
    AssertThat(GetTree().Paused).IsTrue();
}
```

This must fail against Task 7's parity policy.

- [ ] **Step 2: Add a real-scene gameplay freeze probe**

Attach a small test-only `PausableProbe : Node` below the active runtime `GridMap` with `_Process` incrementing a counter. Record the counter before Pause, wait frames while paused, then Resume and wait frames again:

```csharp
int beforePause = probe.ProcessCount;
PushAction("pause_menu");
await AwaitFrames(3);
AssertThat(probe.ProcessCount).IsEqual(beforePause);

PushAction("pause_menu");
await AwaitFrames(3);
AssertThat(probe.ProcessCount).IsGreater(beforePause);
```

The host must still respond to the second Pause action while gameplay processing is frozen.

- [ ] **Step 3: Run red**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.RootPauseOwnsTreePause|FullyQualifiedName~GameplayPauseHostTest.GameplayProbe"
```

Expected: FAIL because root Pause is still `PauseTree=false`.

- [ ] **Step 4: Flip only the two gated Pause policy fields**

Change:

```csharp
ProcessPolicy = UIProcessPolicy.Always,
PauseTree = false,
```

to:

```csharp
ProcessPolicy = UIProcessPolicy.WhenPaused,
PauseTree = true,
```

Do not change child pause ownership.

- [ ] **Step 5: Run focused freeze/lifecycle suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest|FullyQualifiedName~PlayerControllerTest|FullyQualifiedName~FloorManagerTest"
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameTest.cs
git commit -m "feat(ui): enable host-owned gameplay pause"
```

Expected: selected tests PASS; Pause freezes gameplay while host Cancel still resumes.

---

## Task 9: Harden physical Cancel, restoration, and teardown regressions without new abstractions

**Files:**
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `scripts/game/Game.cs` only if a regression exposes a production defect.

**Interfaces:**
- Reuses final host/root Cancel policy.
- Reuses `RequestSceneChange` teardown helper.

- [ ] **Step 1: Add physical-input order regressions**

Cover exact outcomes:

```text
hosted Settings popup/key capture -> reserved for current handler
hosted Save/Load overwrite child -> dismiss child only
hosted child -> closes child, Pause remains
Pause -> closes Pause
active error -> dismiss error, no Pause
battle -> existing escape/result close, no Pause
puzzle -> retained dialog receives Cancel, no Pause
world interaction -> event consumed, no Pause
NPC interaction -> retained native dialog receives Cancel, no Pause
root ui_cancel -> opens Pause
root pause_menu -> opens Pause
```

Use real `InputEventKey` / configured action bindings in `GameInputLifecycleTest`, not direct method calls for this step.

- [ ] **Step 2: Add invalid-focus and teardown tests**

In `GameplayPauseHostTest`:

- focus a temporary gameplay Control;
- open Pause;
- free the prior focus target;
- close Pause;
- assert no exception and no stuck `RestorationLease`.

Then open Pause + Settings (or Save/Load), invoke `RequestSceneChange`, and assert descendants close, `ActiveEntries.Count == 0`, pause restores, and exactly one `PerformSceneChange` callback occurs.

- [ ] **Step 3: Run focused lifecycle suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameTest"
```

Expected: selected tests PASS.

- [ ] **Step 4: Commit only if test/code changes were needed**

```bash
git add tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs scripts/game/Game.cs
git commit -m "test(ui): lock gameplay pause lifecycle"
```

---

## Task 10: Update ownership documentation and run final repository gates

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:**
- Documents the final production ownership model; no runtime interface added.

- [ ] **Step 1: Update the lifecycle contract**

Record these final facts:

```text
Game owns one local UIScreenHost.
Pause owns the gameplay tree-pause lease.
Direct Inventory uses the same host and no longer writes SceneTree.Paused.
Pause children inherit the Pause lease and do not acquire another.
Hosted Cancel runs before remaining unhosted domain fallback.
GridMap runtime nodes inherit pausable gameplay processing.
Scene replacement waits for UIScreenHost.PrepareForTeardown() == Complete.
```

Do not rewrite unrelated historical HPA-376 content.

- [ ] **Step 2: Search for stale legacy ownership**

```bash
rg -n "PauseMenuDialog|_pauseMenuRestorePending|_saveLoadFromPause|_pauseSnapshotCaptured|_treeWasPausedBeforeOpen" scripts tests scenes
```

Expected: zero matches.

Check explicit Always gameplay nodes:

```bash
rg -n "process_mode = 3" scenes/game/floors scenes/ui/InventoryMenu.tscn
```

Expected: no runtime `GridMap` or Inventory root match.

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

- [ ] **Step 5: Run the full .NET/GdUnit4 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: zero failures.

- [ ] **Step 6: Commit documentation**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record gameplay screen host ownership"
```

---

## Review Gate Checklist

Before implementation is declared complete, verify each item with fresh command output:

- [ ] `PauseScreen` is a child of `UIScreenHost/ModalLayer`, not a free-floating `CanvasLayer` peer.
- [ ] Direct Inventory begins unparented, attaches to `ModalLayer`, and detaches on `External` close.
- [ ] Direct Inventory can close and later reopen as a Pause child without replacing an active kind.
- [ ] `GameTest.cs` contains no legacy Pause field/type assumptions.
- [ ] Root Pause parity passed with `PauseTree=false` before the final flip.
- [ ] Four runtime `GridMap` scenes no longer pin `Always`.
- [ ] Final root Pause owns `SceneTree.Paused` and gameplay probes stop processing.
- [ ] Host still receives Cancel while the tree is paused.
- [ ] Error/battle/puzzle/world/NPC Cancel precedence matches the production contract.
- [ ] Settings and Save/Load nested Cancel precedence remains intact.
- [ ] Return-to-Title duplicate navigation suppression is owned by `Game`.
- [ ] Return to Title and in-game Load wait for host teardown completion.
- [ ] Focused tests and full suite pass before merge.

## Execution Handoff

Implement task-by-task with `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans`. Do not skip Task 5 or the Task 7 `PauseTree=false` parity checkpoint even if the final two-line PauseTree flip appears trivial.