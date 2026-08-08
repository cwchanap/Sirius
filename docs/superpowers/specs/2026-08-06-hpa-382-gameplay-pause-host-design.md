# HPA-382 Gameplay Pause Host Integration Design

**Linear:** HPA-382  
**Status:** Implementation-ready after review  
**Date:** 2026-08-06  
**Foundation:** HPA-378 `UIScreenHost` contract and migration ordering

## Decision summary

Sirius will make the existing scene-local `UIScreenHost` the presentation authority for gameplay Pause and the legacy screens opened from Pause. The runtime-built `PauseMenuDialog` will be deleted and replaced by a scene-authored `PauseScreen` built from `SiriusModalShell`, the shared theme, icons, and input hints.

`Game` remains the flow orchestrator. `GameManager`, `InventoryMenuController`, `SaveLoadDialog`, `SettingsMenuController`, `SaveManager`, and scene navigation retain domain ownership. The host owns presentation stacking, pause, gameplay blocking, cursor/HUD policy, lower-layer interaction, Cancel priority, focus, and teardown.

The migration follows HPA-378's staged ordering:

1. bootstrap the production host without breaking synthetic `Game` tests;
2. centralize teardown-safe scene replacement;
3. compose presentation blocking into gameplay input;
4. normalize explicit `Always` runtime `GridMap` processing;
5. migrate direct Inventory atomically with removal of its private pause write and `Always` override;
6. build hosted Pause with `PauseTree=false`;
7. add all hosted Pause children while the legacy production Pause path still owns root input;
8. cut production Cancel/Pause to the complete hosted path and delete legacy Pause state;
9. enable `PauseTree=true` only after parity and freeze gates pass.

This is a vertical migration, not a new UI/navigation framework.

## Goals

- One local `UIScreenHost` in the real `Game.tscn`.
- Synthetic `new Game()` test fixtures may omit the host without failing `_EnterTree()`.
- Scene-authored Pause with Resume, Inventory, Save, Load, Settings, and Return to Title.
- Preserve child-screen domain and visual behavior; HPA-382 changes lifecycle ownership only.
- Keep Pause active as logical parent while hosted children are open.
- One-shot Return-to-Title navigation guard owned by `Game`.
- Remove Inventory's private `SceneTree.Paused` ownership safely.
- Normalize runtime processing before root Pause starts pausing the tree.
- Every production `Game` scene replacement waits for host teardown completion.
- Restore incoming pause/cursor/HUD/lower-layer/focus state exactly.
- Validate 1280×720 and 640×360 only.

## Non-goals

- Redesign Inventory, Settings, Save/Load, battle, dialogue, shop, healing, puzzles, or notifications.
- Hide gameplay HUD for Inventory in this ticket; HPA-357 owns that presentation migration.
- Navigation service, modal manager, screen registry, DI layer, generic confirmation framework, or compatibility shim.
- Host every legacy dialog.
- Extract a generic child-spec factory for future tickets.

## Existing contracts

### `UIScreenHost`

Reuse directly:

```csharp
public void Configure(UIScreenHostOptions options);
public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec);
public UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason);
public UIScreenTeardownPreparationStatus PrepareForTeardown();
```

The host remains scene-local.

### Host bootstrap must tolerate synthetic tests

`GameTest.TestableGame` and `GameInputLifecycleTest.LifecycleGame` are constructed with `new` and do not have the production UI subtree when inherited `_EnterTree()` runs.

Use nullable lookup:

```csharp
private UIScreenHost? _screenHost;

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

    // Existing pending-load setup remains below.
}
```

Host-dependent helpers decline safely when `_screenHost` is absent. A strict real-scene test asserts that production `Game.tscn` still contains exactly one host.

### Hosted view parentage

`UIScreenViewAdapter` accepts:

- `Control`: unparented or parented directly to the selected host layer;
- `Window`: unparented or parented directly to the host.

Therefore **no hosted view is `AddChild`ed by `Game` before `TryPresent`**.

| View | Type | Attachment |
|---|---|---|
| Pause | Control | host `ModalLayer` |
| Inventory | Control | host `ModalLayer`; `External` close detaches for reuse |
| Settings | Control | host `ModalLayer` |
| Save/Load | `AcceptDialog` / Window | `UIScreenHost` itself |
| Return confirmation | Control | host `ModalLayer` |

Do not add a manual reparent helper unless an observed need appears.

### Embedded subwindows

Production pins:

```ini
[display]
window/subwindows/embed_subwindows=true
```

Test-created `SubViewport` instances that present Save/Load also set:

```csharp
viewport.GuiEmbedSubwindows = true;
```

The project setting is not relied upon to configure arbitrary test SubViewports.

## Process-mode migration gate

Verified explicit `Always` runtime nodes:

- `GridMap` in FloorGF/1F/2F/3F;
- root `InventoryMenu`.

The four `GridMap` overrides are removed first. Current ordinary Pause does not pause the tree, so this is behavior-neutral.

The Inventory override moves later, **in the same commit** that deletes Inventory's private `SceneTree.Paused` snapshot/write and moves direct Inventory to host ownership. Removing the Inventory override earlier would create an intermediate soft-lock: Inventory pauses the tree and then stops processing its own input.

Before root `PauseTree=true`, tests prove:

1. teardown-safe scene replacement exists;
2. four `GridMap` nodes no longer pin `Always`;
3. Inventory no longer writes tree pause and no longer pins `Always`;
4. composed presentation blocking covers movement/interactions;
5. hosted Pause works with `PauseTree=false`;
6. hosted children and production Cancel cutover pass parity tests.

Only then does root Pause switch to `WhenPaused` + `PauseTree=true`.

## Pause presentation

`PauseScreen.tscn` is a full-rect Control containing `SiriusModalShell` and six vertical actions:

1. Resume
2. Inventory
3. Save
4. Load
5. Settings
6. Return to Title

Return to Title uses the existing destructive outline treatment. Its child confirmation owns the final destructive action.

`PauseScreenController` only binds nodes, emits signals, exposes Resume focus, and refreshes layout.

Reuse shared metrics:

```csharp
private void RefreshLayout()
{
    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _shell.RefreshPresentation(size);
}
```

Subscribe to `Resized` in `_Ready()` and unsubscribe in `_ExitTree()`.

Tests use `SiriusUiMetrics.MinimumTarget(compact)`: 44 at 1280×720, 40 at 640×360. They also resize after `_Ready()` to prove runtime reflow.

## Return-to-Title confirmation

`PauseReturnToTitleConfirmation` uses `SiriusModalShell` with `Cancel` and `Return to Title`.

Controller responsibilities stop at two signals and safe initial focus. `_sceneChangeCommitted` remains in `Game`.

No generic confirmation component is introduced ahead of HPA-572.

## Entry policies

### Direct Inventory

| Field | Value |
|---|---|
| Kind | `Inventory` |
| Parent | null |
| Layer / priority | Modal / Modal |
| Process | WhenPaused |
| PauseTree | true |
| BlockGameplayInput | true |
| HUD | Inherit |
| Cursor | Visible |
| LowerLayers | VisibleInert |
| Cancel | Close |
| EntryCancelActions | `toggle_inventory` |
| Lifetime | External |

`Hud=Inherit` preserves HPA-382's lifecycle-only scope.

### Inventory from Pause

Same reusable detached view, with:

```text
Parent = pauseHandle
Process = Always
PauseTree = false
BlockGameplayInput = false
HUD = Inherit
Lifetime = External
```

A logical parent is never changed in place. Close the active kind first, then present again.

### Pause parity phase

```text
Kind = Pause
Parent = null
Layer/Priority = Modal/Modal
Process = Always
PauseTree = false
BlockGameplayInput = true
HUD = Visible
Cursor = Visible
LowerLayers = VisibleInert
Cancel = Close
Lifetime = QueueFree
```

### Final Pause

After the gate, only:

```text
Process: Always -> WhenPaused
PauseTree: false -> true
```

Children keep `PauseTree=false`.

### Other Pause children

Keep the three policy literals explicit in `Game`:

| Child | Priority | Process | HUD | Lifetime | Special behavior |
|---|---|---|---|---|---|
| Save/Load | Modal | Always | Inherit | QueueFree | overwrite child first |
| Settings | Modal | Always | Inherit | QueueFree | popup/rebind reservation |
| Return confirmation | Blocking | Always | Inherit | QueueFree | blocking group + safe focus |

A shared `PauseChildSpec` factory is intentionally deferred. There are only three sibling policies and the confirmation differs materially; explicit literals keep pause/focus/cancel ownership reviewable. Extract later only if duplication becomes a measured cost.

## Inventory lifecycle adaptation

In one atomic slice:

- remove `_pauseSnapshotCaptured` and `_treeWasPausedBeforeOpen`;
- remove `RestoreTreePause()` and `GetTree().Paused = true`;
- remove root `process_mode = 3` from `InventoryMenu.tscn`;
- `OpenMenu()` refreshes/shows only;
- `CloseMenu()` hides only;
- Close UI emits `CloseRequested`;
- controller `_Input()` may observe input-device changes but no longer terminally closes on `ui_cancel` or `toggle_inventory`;
- `Game.SetupInventoryMenu()` instantiates but does not parent the reusable view.

Host kind state, not `Visible`, determines whether Inventory is open.

## Hosted Settings / Save-Load adaptation

### Settings

Keep editing/validation/key-capture behavior. Instantiate unparented, present through host, and call `OpenSettings(showOverlay:false)` only after host attachment. Its Cancel interceptor reserves for rebind/dropdown state and otherwise closes the hosted entry.

### Save/Load

Keep slot/domain/overwrite behavior. Instantiate unparented, present the Window through host, then call `ShowDialog(mode)`. Cancel dismisses `HasActiveChildDialog` first, otherwise closes the hosted entry.

Tests assert actual Godot parentage for every hosted child.

## Gameplay input composition

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

`PlayerController` receives one optional provider:

```csharp
public Func<bool>? GameplayInputSuppressedProvider { private get; set; }
```

Existing domain checks remain.

## Cancel migration and cutover

The legacy `HandlePauseMenuInput` remains the production root while new hosted behavior is constructed in tests.

### Stage 7A — Pause base

Add and test hosted Pause with `PauseTree=false`; connect Resume only. Production root input remains legacy, so dormant unconnected child buttons are not user-visible.

### Stage 7B — hosted children

Define all Inventory/Save/Load/Settings/confirmation handlers, then connect the remaining five Pause signals in one change. Production root input still remains legacy while the complete hosted stack is tested directly.

### Stage 7C — production cutover

Only after 7B is green:

1. route root core Cancel through `UIScreenHost`;
2. migrate `GameTest` and physical-input expectations;
3. retain remaining unhosted domain precedence;
4. delete `PauseMenuDialog`, `_pauseMenuRestorePending`, `_saveLoadFromPause`, and obsolete ladder branches.

Final root fallback order:

1. hosted entry traversal;
2. active error -> dismiss/consume;
3. battle -> existing escape/result path;
4. puzzle -> decline for retained native handler;
5. atomic world interaction -> consume/no Pause;
6. NPC -> decline for retained native handler;
7. no blocker -> either core Cancel action opens Pause.

This three-stage sequence keeps every production commit usable and keeps the cutover diff reviewable.

## Scene replacement and teardown

Do not add `PerformSceneChange` or another navigation abstraction.

Use one private helper:

```csharp
private const string MainMenuScenePath = "res://scenes/ui/MainMenu.tscn";
private const string GameScenePath = "res://scenes/game/Game.tscn";
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
        GetTree().ChangeSceneToFile(path);
}

protected virtual void ReturnToMainMenu() => RequestSceneChange(MainMenuScenePath);
```

`PrepareForTeardown()` finalization exceptions propagate; they are not converted to `Deferred`. No arbitrary retry-count policy is added.

Route production paths:

- dead-player/defeat/SaveLoad Main Menu/Return confirmation -> `ReturnToMainMenu()`;
- successful in-game Load -> set `PendingLoadData`, then `RequestSceneChange(GameScenePath)`;
- corrupted-save confirmation -> `RequestSceneChange(MainMenuScenePath)`.

`ReturnToMainMenu` remains the existing protected virtual test seam. No second virtual scene-change seam exists.

## Test fixture contract

Task 3 immediately runs `GameTest` and `GameInputLifecycleTest` after nullable host bootstrap to prove their existing synthetic construction still survives `_EnterTree()`.

Later host-aware cases either:

- use real `Game.tscn` in `GameplayPauseHostTest`; or
- create a minimal UI/host subtree before a synthetic Game enters the tree.

Do not make unrelated legacy tests load the full production scene.

Any test `SubViewport` that presents Save/Load sets `GuiEmbedSubwindows=true` explicitly.

Reuse existing frame-await and host-test idioms rather than adding a parallel harness.

## Testing strategy

### Component

- six Pause action signals and Resume focus;
- shared compact/minimum-target metrics at 1280×720 and 640×360;
- runtime resize after `_Ready()`;
- confirmation signals/focus.

### Integration

`GameplayPauseHostTest` covers:

- one production host;
- all hosted Godot parentage rules;
- Inventory detach/reuse;
- Pause parity with `PauseTree=false`;
- hosted child return/nested Cancel;
- one-shot Return confirmation;
- invalid prior focus;
- teardown with child;
- final real tree-pause freeze probe.

`GameTest` is a first-class production cutover suite because it contains many legacy Pause assumptions. `GameInputLifecycleTest` remains the physical-input/domain-order suite.

### Freeze regression

With final Pause active:

- `SceneTree.Paused == true`;
- host still receives Cancel;
- pausable probe beneath runtime `GridMap` stops;
- after Resume the probe advances again.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| strict host lookup breaks synthetic tests | nullable `GetNodeOrNull`; run both suites in Task 3 |
| Inventory `Always` removed before pause write | remove both atomically in Task 6 |
| hosted view pre-parented under `UI` | no manual `AddChild`; assert parent for every kind |
| test Window rejected for no embedding | set test SubViewport flag explicitly |
| `Always` GridMap creates half-paused world | remove four overrides + freeze probe |
| Inventory controller races host Cancel | delete terminal `_Input` close branch |
| Pause migration too large to bisect | 7A base, 7B children, 7C cutover/deletion |
| extra navigation seam drifts | keep only existing `ReturnToMainMenu` virtual seam |
| Inventory HUD change sneaks into lifecycle ticket | `Hud=Inherit`; defer to HPA-357 |
| premature spec factory hides policy differences | keep explicit specs |

## Implementation shape

1. Pause component with shared metrics and resize handling.
2. Flow-specific confirmation.
3. Nullable host bootstrap, subwindow fixture contract, centralized teardown-safe scene replacement.
4. Composed gameplay input suppression.
5. Remove four runtime `GridMap` `Always` overrides only.
6. Direct Inventory host migration; remove pause write, terminal `_Input` close, and Inventory `Always` override atomically.
7A. Hosted Pause base with `PauseTree=false`, production root still legacy.
7B. Hosted Inventory/Save/Load/Settings/confirmation children, production root still legacy.
7C. Production root Cancel cutover, legacy-suite migration, legacy Pause deletion.
8. Flip root Pause to `WhenPaused` + `PauseTree=true`; prove real gameplay freezes and host Cancel resumes.
9. Harden physical Cancel/focus/teardown regressions.
10. Update lifecycle docs and run build/focused/full gates.

This keeps architecture small, makes each risky transition independently reviewable, and avoids broken intermediate commits.