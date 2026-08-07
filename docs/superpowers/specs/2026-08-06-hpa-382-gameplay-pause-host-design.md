# HPA-382 Gameplay Pause Host Integration Design

**Linear:** HPA-382  
**Status:** Implementation-ready  
**Date:** 2026-08-06  
**Foundation:** HPA-378 `UIScreenHost` contract and migration ordering

## Decision summary

Sirius will make the existing scene-local `UIScreenHost` the production presentation authority for gameplay Pause and the screens opened from Pause. The runtime-built `PauseMenuDialog` will be deleted and replaced by a scene-authored `PauseScreen` built from `SiriusModalShell`, the shared theme, approved icons, and input hints.

`Game` remains the flow orchestrator. `GameManager`, `InventoryMenuController`, `SaveLoadDialog`, `SettingsMenuController`, `SaveManager`, and scene navigation retain their domain responsibilities. The host owns presentation state only: tree pause, presentation gameplay blocking, cursor, HUD policy, lower-layer interaction, Cancel priority, focus, and teardown.

The migration follows the HPA-378 foundation ordering instead of enabling root tree pause immediately:

1. bootstrap host and teardown safety;
2. compose presentation blocking into gameplay input;
3. audit and normalize explicit `Always` gameplay processing;
4. migrate direct Inventory to the host and remove its private pause ownership;
5. migrate Pause and its children through the host with `PauseTree=false` first and prove lifecycle/cancel parity;
6. enable `PauseTree=true` only after the parity and process-freeze gates pass.

This is one vertical migration. It is not a new navigation framework, modal framework, service locator, or blanket migration of every gameplay dialog.

## Why HPA-382 is the next actionable task

The shared theme, `SiriusModalShell`, `UIScreenHost`, focus/cancel policies, and lifecycle regression coverage are already merged. HPA-382 has no active blocker and unlocks Settings, Save/Load, dialogue, shop/healing, puzzle, confirmation/error, and reward migrations.

The current Pause path still manually coordinates visibility, Cancel precedence, restoration, and child return through `PauseMenuDialog`, `_pauseMenuRestorePending`, and `_saveLoadFromPause`. Keeping that path would force later migrations to integrate around two presentation authorities.

## Goals

- Add exactly one local `UIScreenHost` to `Game.tscn` and configure it before host `_Ready()`.
- Replace `PauseMenuDialog` with a responsive scene-authored Pause screen.
- Preserve Resume, Inventory, Save, Load, Settings, and Return to Title behavior.
- Preserve the existing domain Cancel ladder until each corresponding child has moved under the host.
- Keep Pause as the logical parent while a hosted child is open.
- Make Return to Title an explicit child confirmation and guarantee one navigation request.
- Remove Inventory's private `SceneTree.Paused` snapshot/write behavior once direct Inventory is hosted.
- Make gameplay tree pause safe by normalizing explicit `Always` gameplay processing before root Pause acquires a pause lease.
- Restore exact incoming pause, cursor, HUD, lower-layer, and focus state on close/teardown.
- Validate 1280×720 and 640×360 without adding a viewport cross-product matrix.

## Non-goals

- Redesigning Inventory, Settings, Save/Load, battle, dialogue, shop, healing, puzzles, or notifications.
- Creating a navigation service, modal manager, screen registry, DI layer, or generic confirmation framework.
- Hosting every legacy dialog in this ticket.
- Changing save, settings, inventory, battle, or scene-navigation domain rules.
- Adding animation infrastructure or new Pause features.
- Preserving the deleted `PauseMenuDialog` API or adding compatibility wrappers.

## Existing contracts that must be reused

### `UIScreenHost`

Use the existing production contract:

```csharp
public void Configure(UIScreenHostOptions options);
public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec);
public UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason);
public UIScreenTeardownPreparationStatus PrepareForTeardown();
```

The gameplay host is scene-local, not an autoload.

### Host Control parentage

A hosted `Control` is valid only when it is unparented or already parented directly to the selected host layer. `UIScreenViewAdapter.TryCreate` rejects any other parent with `InvalidControlParentage`.

Therefore the current Inventory setup is incompatible with hosting because `Game.SetupInventoryMenu()` currently adds the reusable `InventoryMenuController` under `UI`.

**Chosen fix:** instantiate Inventory once but do **not** pre-parent it. `TryPresent` attaches it to the host `ModalLayer`. `UINodeLifetime.External` detaches it on close so the same instance can later reopen with a different logical parent.

Do not add a manual reparent helper unless a concrete failure proves it necessary.

### Host teardown

Every `Game` scene change that can occur while hosted UI exists must call `PrepareForTeardown()` and change scenes only after `Complete`. A `Deferred` result schedules one later retry from `Game`; no navigation service is introduced.

This applies to Return to Title and in-game Load.

## Process-mode migration gate

HPA-378 explicitly identifies root Pause tree pausing as the highest-risk migration step because current Pause does not pause `SceneTree` while current Inventory does.

Current repository state includes explicit `Always` processing where gameplay must freeze under root Pause:

- `GridMap` nodes in `FloorGF.tscn`, `Floor1F.tscn`, `Floor2F.tscn`, and `Floor3F.tscn` use `process_mode = 3` (`Always`).
- `InventoryMenu.tscn` also uses `process_mode = 3`.

The floor scene roots themselves are not the verified issue; the explicit `GridMap` children are.

### Chosen normalization

- Remove the explicit `Always` override from each runtime `GridMap` node so it inherits the pausable gameplay tree.
- Remove the `Always` override from `InventoryMenu.tscn`; host registration supplies the process mode required for each presentation context.
- Keep the host and its presentation layers `Always` as defined by HPA-378.
- Any future `Always` gameplay exception must be explicit, justified by a current runtime need, and covered by a focused test.

### Required gate before root `PauseTree=true`

Before root Pause acquires tree pause, tests must prove:

1. every `Game` scene-change path that can run with hosted UI waits for `PrepareForTeardown() == Complete`;
2. the explicit-`Always` gameplay audit/normalization is green;
3. the gameplay HUD remains visually available while host-owned presentation blocking controls interaction;
4. the composed gameplay-input predicate blocks movement/interactions under hosted UI;
5. the production Game scene can open/close host Pause with `PauseTree=false` and preserve Cancel/focus/child-return behavior;
6. Inventory no longer writes `SceneTree.Paused` itself.

Only then does the production Pause policy change to `PauseTree=true`; the final real-scene freeze test proves the normalized gameplay tree actually stops.

## Scene composition

`Game.tscn` contains one `UIScreenHost` instance under the existing `UI` `CanvasLayer`, after `GameUI`, so host layers render above the HUD.

`Game._EnterTree()` configures it before child `_Ready()`:

```csharp
_screenHost.Configure(new UIScreenHostOptions
{
    HudRoot = GetNode<Control>("UI/GameUI"),
    CoreCancelActions = GameplayCoreCancelActions,
    RootCancelFallback = HandleGameplayRootCancel,
    GameplayInputBlockChanged = blocked => _presentationGameplayBlocked = blocked
});
```

`GameplayCoreCancelActions` contains both `pause_menu` and `ui_cancel`.

Embedded subwindows are pinned with:

```ini
[display]
window/subwindows/embed_subwindows=true
```

because the retained `SaveLoadDialog` is an `AcceptDialog` hosted as a Pause child.

## New Pause presentation

### `PauseScreen.tscn`

A full-rect `Control` contains `SiriusModalShell.tscn` and six labelled actions:

1. Resume
2. Inventory
3. Save
4. Load
5. Settings
6. Return to Title

The action list is vertical and remains usable at 640×360. Return to Title uses the existing destructive outline treatment; the child confirmation owns the final filled destructive action.

### `PauseScreenController.cs`

Presentation-only responsibilities:

- bind authored nodes;
- update responsive shell state and input hints;
- expose the Resume initial-focus target;
- emit six action signals.

It does not pause the tree, change scenes, open child screens, or call domain managers.

### Return-to-title confirmation

`PauseReturnToTitleConfirmation.tscn` uses `SiriusModalShell` and two buttons: `Cancel` and `Return to Title`.

The controller emits `CancelRequested` and `ReturnToTitleConfirmed` only. It does **not** own duplicate navigation suppression. The one-shot `_sceneChangeCommitted` guard lives in `Game`, where scene navigation is owned.

The confirmation is a logical child of Pause with `UIScreenKinds.ConfirmQuitToMain`. No generic confirmation abstraction is introduced ahead of HPA-572.

## Entry-policy matrix

### Direct Inventory

| Field | Value |
|---|---|
| Kind | `UIScreenKinds.Inventory` |
| Parent | `null` |
| Layer / priority | `Modal / Modal` |
| Process | `WhenPaused` |
| PauseTree | `true` |
| BlockGameplayInput | `true` |
| HUD | `Hidden` |
| Cursor | `Visible` |
| LowerLayers | `VisibleInert` |
| Cancel | `Close` |
| EntryCancelActions | `toggle_inventory` |
| Lifetime | `External` |

Direct Inventory is migrated only after the gameplay process audit passes. It replaces the controller's old private pause ownership with the host pause lease.

### Inventory from Pause

| Field | Value |
|---|---|
| Kind | `UIScreenKinds.Inventory` |
| Parent | `pauseHandle` |
| Layer / priority | `Modal / Modal` |
| Process | `Always` |
| PauseTree | `false` |
| BlockGameplayInput | `false` |
| HUD | `Hidden` |
| Cursor | `Visible` |
| LowerLayers | `VisibleInert` |
| Cancel | `Close` |
| EntryCancelActions | `toggle_inventory` |
| Lifetime | `External` |

The same Inventory instance is reused. Host close detaches an `External` view. A later open therefore presents an unparented view again and may use a different logical `Parent`.

The host never reparents an already-active Inventory entry. Transitioning between direct and Pause-child contexts requires a real close followed by a new `TryPresent`.

### Pause parity phase

Before the tree-pause flip:

| Field | Value |
|---|---|
| Kind | `UIScreenKinds.Pause` |
| Parent | `null` |
| Layer / priority | `Modal / Modal` |
| Process | `Always` |
| PauseTree | `false` |
| BlockGameplayInput | `true` |
| HUD | `Visible` |
| Cursor | `Visible` |
| LowerLayers | `VisibleInert` |
| Cancel | `Close` |
| Lifetime | `QueueFree` |

This phase proves host ownership, input blocking, focus, Cancel, children, and restoration without changing the runtime tree-pause behavior yet.

### Final Pause policy

After the process gate passes, only these Pause fields change:

```text
ProcessPolicy: Always -> WhenPaused
PauseTree:      false  -> true
```

Children keep `PauseTree=false`; the Pause parent owns the pause lease.

### Other Pause children

| Presentation | Parent | Process | PauseTree | Block | HUD | Lower layers | Lifetime |
|---|---|---|---:|---:|---|---|---|
| Save/Load | Pause | Always | false | false | Inherit | VisibleInert | QueueFree |
| Settings | Pause | Always | false | false | Inherit | VisibleInert | QueueFree |
| Return-to-title confirmation | Pause | Always | false | false | Inherit | VisibleInert | QueueFree |

## Inventory lifecycle adaptation

`InventoryMenuController` loses all `SceneTree.Paused` ownership:

- remove `_pauseSnapshotCaptured` and `_treeWasPausedBeforeOpen`;
- `OpenMenu()` refreshes and shows only;
- `CloseMenu()` hides only;
- Close UI emits `CloseRequested` so `Game` closes the host handle;
- controller `_Input()` observes device changes but does not terminally own `ui_cancel` or `toggle_inventory` while hosted.

`Game.SetupInventoryMenu()` instantiates one controller and keeps it unparented. Do not call `UI.AddChild(_inventoryMenu)`.

Direct-open state is determined with `host.IsKindActive(UIScreenKinds.Inventory)`, never `_inventoryMenu.Visible`, because an unparented reusable view has no meaningful production visibility until hosted.

## Gameplay input composition

`Game` stores the presentation contribution and exposes one predicate:

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

`PlayerController` receives it through one optional provider:

```csharp
public Func<bool>? GameplayInputSuppressedProvider { private get; set; }
```

The provider is a narrow hook, not a service. Existing domain checks remain for isolated tests and domain-specific decisions.

## Cancel ownership and migration ordering

The old `HandlePauseMenuInput` ladder must not be deleted piecemeal before its hosted replacements exist.

### Final dispatch order

1. **Hosted entry traversal** — `UIScreenHost` owns Cancel for Pause, Inventory, Settings, Save/Load, and confirmation.
2. **Active error popup** — root fallback dismisses and consumes.
3. **Battle** — existing escape/result behavior runs and consumes.
4. **Puzzle riddle** — root fallback declines so the retained native dialog can receive Cancel.
5. **Atomic world interaction** — consume without opening Pause.
6. **NPC interaction** — decline so the retained native NPC dialog can receive Cancel.
7. **No blocker** — either matched core action (`pause_menu` or `ui_cancel`) opens Pause.

Settings and Save/Load child interceptors preserve their nested precedence:

- Settings dropdown/key capture reserves Cancel for the current controller/native popup; otherwise close Settings.
- Save/Load dismisses an active overwrite child first; otherwise close Save/Load.
- Inventory owns `toggle_inventory` only while it is the applicable hosted entry.
- Confirmation Cancel closes only the confirmation.

The final root behavior intentionally does not depend on `SettingsManager` mirroring the configured Pause key onto `ui_cancel`; both approved core actions can open Pause at the gameplay root.

### Migration rule

Until the hosted child replacement is active in the same implementation slice, keep the corresponding old ladder arm. Remove an old branch only after its host policy and regression test are green.

This prevents intermediate commits from changing ESC behavior.

## Flow behavior

### Open and Resume

1. Root fallback checks unhosted domain blockers.
2. `Game` instantiates `PauseScreen.tscn` and presents it.
3. During parity phase, the host blocks gameplay but does not pause the tree.
4. Resume or Cancel closes the Pause handle.
5. Focus and presentation effects restore through the host.
6. After the tree-pause gate passes, the same flow additionally owns the host pause lease.

### Open and return from child

1. Pause remains active as logical parent.
2. Child is presented with `Parent = pauseHandle`.
3. Child becomes top input owner; Pause remains visible inert.
4. Child terminal signal closes its handle.
5. Host restores the existing Pause instance and Return/Save/etc. focus target.

No `_pauseMenuRestorePending` or `_saveLoadFromPause` flag remains in the final implementation.

### Inventory reuse

- Direct Inventory: unparented reusable view -> host ModalLayer -> close -> detached.
- Pause Inventory: same detached view -> host ModalLayer with `Parent=pauseHandle` -> close -> detached.
- If Inventory is already active, a second open request is an idempotent no-op; do not attempt to replace its parent in place.

### Return to Title

1. Return to Title opens the dedicated confirmation child.
2. Cancel closes only that child.
3. Confirm checks `_sceneChangeCommitted`; first confirm commits, later input is ignored.
4. `Game` closes/prepares hosted UI.
5. Scene changes only after `PrepareForTeardown()` returns `Complete`.

## Scene-change helper

Use one small `Game` helper:

```csharp
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
```

Tests may keep using the existing virtual navigation seam where useful; do not create another navigation abstraction.

## Error handling

- Log every `TryPresent` rejection with kind/status.
- On failed Pause open, free the unregistered Pause scene and leave gameplay unchanged.
- On failed child open, leave Pause active and restore its focus target.
- On `HostMutating`, defer one retry from the owning `Game` flow; the host itself never queues the open.
- Treat duplicate kind as idempotent only when the already-active presentation is the intended one.
- On deferred teardown, retry before scene replacement.
- Existing save/settings/inventory domain errors stay in their current controllers.

## Testing strategy

### Existing suites that must move with the migration

`tests/game/GameTest.cs` contains direct reflection and behavior assertions against `_pauseMenuDialog`, Pause settings restore, Save/Load child behavior, and current Cancel ordering. It is a first-class migration suite, not a late full-suite surprise.

Every implementation slice that removes or replaces legacy Pause state includes `GameTest` in its focused filter.

`GameInputLifecycleTest` remains the physical-input/lifecycle suite.

### New integration coverage

`GameplayPauseHostTest` covers:

- one production gameplay host;
- Pause host layer parentage (`PauseScreen` is under `UIScreenHost/ModalLayer`);
- parity phase has `PauseTree=false` but presentation block/focus/cursor/HUD work;
- final phase owns tree pause;
- direct Inventory opens from an unparented view, attaches to `ModalLayer`, closes/detaches, then reopens from Pause;
- unique Inventory kind never changes logical parent in place;
- Settings/Save/Load/confirmation are logical children;
- nested Cancel precedence;
- invalid prior focus;
- teardown with Pause + child;
- 1280×720 and 640×360 Pause layout.

### Process-freeze regression

Use the real `Game.tscn` / floor fixture. With final Pause active:

- `SceneTree.Paused` is true;
- the host still processes input;
- a gameplay probe under the runtime `GridMap` does not advance while paused;
- after Resume it advances again.

This specifically guards against reintroducing explicit `Always` gameplay processing.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Inventory pre-parented under `UI` causes `InvalidControlParentage` | keep reusable Inventory unparented; host attaches to `ModalLayer`; test parent and detachment |
| Explicit `Always` GridMap produces half-paused world | remove four GridMap overrides before root pause; add real-scene freeze regression |
| Legacy direct Inventory pause write competes with host lease | delete snapshot/write fields when direct Inventory moves to host |
| `GameTest` remains coupled to deleted Pause fields | migrate it in the same slice as legacy Pause deletion and include in focused filters |
| Cancel ladder removed before children are hosted | retain each legacy arm until its host replacement is active and tested |
| Reused `External` Inventory is reopened with stale parent assumptions | assert detached after close; new `TryPresent` supplies logical parent each time |
| Confirmation controller accidentally owns navigation guard | guard remains in `Game`; controller emits only signals |
| Hosted scene deleted before teardown completes | all Game scene changes use `PrepareForTeardown` helper |

## Acceptance mapping

- **One local production host:** `Game.tscn` + host bootstrap test.
- **Pause no longer desktop-window framed:** `PauseScreen.tscn`; delete `PauseMenuDialog`.
- **Existing actions preserved:** `GameTest`, `GameplayPauseHostTest`, child controller suites.
- **Deterministic Resume/child/Cancel/teardown:** host integration and lifecycle tests.
- **Single pause/input/cursor authority:** Inventory private pause deletion + final pause lease assertions.
- **Later screens reuse same host:** child registrations use existing kinds/layers without new framework.
- **Focused lifecycle/integration tests:** new host suite + migrated `GameTest` + existing lifecycle suite.

## Implementation shape

1. Pause component.
2. Flow-specific Return-to-Title confirmation.
3. Gameplay host bootstrap, embedded subwindows, and scene-change teardown helper.
4. Composed gameplay input suppression.
5. Explicit-`Always` gameplay audit and normalization.
6. Direct Inventory host migration, including parentage and removal of private pause ownership.
7. Production Pause + child migration with `PauseTree=false`, preserving the full legacy domain ladder until replacements are live; migrate `GameTest` in this slice.
8. Flip root Pause to `PauseTree=true` only after parity/process tests pass.
9. Remove remaining legacy restoration/cancel code and update lifecycle contract.
10. Run focused then full regression gates.

This keeps the architecture lean while honoring the foundation's deliberately staged PauseTree migration.