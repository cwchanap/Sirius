# HPA-382 Gameplay Pause Host Integration Design

**Linear:** HPA-382  
**Status:** Implementation-ready after review  
**Date:** 2026-08-06  
**Foundation:** HPA-378 `UIScreenHost` contract and migration ordering

## Decision summary

Sirius will make the existing scene-local `UIScreenHost` the production presentation authority for gameplay Pause and the legacy screens opened from Pause. The runtime-built `PauseMenuDialog` will be deleted and replaced by a scene-authored `PauseScreen` built from `SiriusModalShell`, the shared theme, approved icons, and input hints.

`Game` remains the flow orchestrator. `GameManager`, `InventoryMenuController`, `SaveLoadDialog`, `SettingsMenuController`, `SaveManager`, and scene navigation retain domain ownership. The host owns presentation state only: presentation stacking, tree pause, gameplay blocking, cursor/HUD policy, lower-layer interaction, Cancel priority, focus, and teardown.

The migration deliberately follows the HPA-378 ordering instead of enabling root tree pause immediately:

1. bootstrap the host without breaking synthetic `Game` test fixtures;
2. centralize teardown-safe scene replacement;
3. compose presentation blocking into gameplay input;
4. normalize explicit `Always` runtime `GridMap` processing;
5. migrate direct Inventory in one atomic slice that also removes its private pause write and `Always` scene override;
6. build a complete hosted Pause + child path with `PauseTree=false` while the legacy production path remains intact;
7. cut production Cancel/Pause over to the hosted path and delete the legacy dialog/flags;
8. enable `PauseTree=true` only after parity and freeze gates pass.

This is one vertical migration. It is not a navigation framework, modal framework, screen registry, DI layer, or blanket migration of every gameplay dialog.

## Goals

- Add exactly one local `UIScreenHost` to the real `Game.tscn`.
- Configure the host before its `_Ready()` while allowing existing synthetic `new Game()` test fixtures to omit it.
- Replace `PauseMenuDialog` with a responsive scene-authored Pause screen.
- Preserve Resume, Inventory, Save, Load, Settings, and Return to Title behavior.
- Preserve current child-screen visual behavior; HPA-382 changes lifecycle ownership, not child-screen design.
- Keep Pause as the logical parent while a hosted child is open.
- Make Return to Title an explicit child confirmation and guarantee one navigation request.
- Remove Inventory's private `SceneTree.Paused` snapshot/write behavior in the same slice that removes its explicit `Always` scene mode.
- Make gameplay tree pause safe by normalizing explicit `Always` `GridMap` processing before root Pause acquires a pause lease.
- Route every `Game` scene replacement through teardown preparation without adding a second navigation test seam.
- Restore exact incoming pause, cursor, HUD, lower-layer, and focus state on close/teardown.
- Validate 1280×720 and 640×360 only.

## Non-goals

- Redesigning Inventory, Settings, Save/Load, battle, dialogue, shop, healing, puzzles, or notifications.
- Hiding the gameplay HUD for Inventory before HPA-357 owns that presentation migration.
- Creating a navigation service, modal manager, screen registry, DI layer, or generic confirmation framework.
- Hosting every legacy dialog in this ticket.
- Changing save, settings, inventory, battle, or scene-navigation domain rules.
- Adding animation infrastructure or new Pause features.
- Preserving the deleted `PauseMenuDialog` API or adding compatibility wrappers.
- Extracting a generic Pause-child spec factory in anticipation of future tickets.

## Existing contracts to reuse

### `UIScreenHost`

Use the existing contract directly:

```csharp
public void Configure(UIScreenHostOptions options);
public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec);
public UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason);
public UIScreenTeardownPreparationStatus PrepareForTeardown();
```

The gameplay host is scene-local, not an autoload.

### Host configuration must tolerate synthetic tests

The real `Game.tscn` must contain `UI/UIScreenHost`, but existing `GameTest` and `GameInputLifecycleTest` fixtures construct subclasses with `new` and do not build the scene subtree before `_EnterTree()` runs.

Therefore production integration uses nullable lookup:

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

    // Existing pending-load setup remains after this block.
}
```

Host-dependent helpers return/decline safely when `_screenHost` is absent. The real-scene integration test remains strict and asserts that production `Game.tscn` contains exactly one configured host.

This keeps production scene requirements explicit without breaking test-only `Game` subclasses.

### Hosted view attachment rule applies to every child

`UIScreenViewAdapter.TryCreate` accepts:

- a `Control` only when unparented or already parented directly to the selected host layer;
- a `Window` only when unparented or already parented directly to the host.

Therefore **no hosted view is manually `AddChild`ed by `Game` before `TryPresent`**.

Concretely:

- Inventory `Control`: instantiate once, keep unparented, host attaches to `ModalLayer`, `External` close detaches for reuse.
- Settings `Control`: instantiate unparented, host attaches to `ModalLayer`, `QueueFree` on terminal close.
- Save/Load `AcceptDialog`: instantiate unparented, host attaches directly to `UIScreenHost`, `QueueFree` on terminal close.
- Pause/confirmation Controls: instantiate unparented and let the host attach them to `ModalLayer`.

Do not add a manual reparent helper unless an observed runtime need appears.

### Embedded subwindows

Production pins:

```ini
[display]
window/subwindows/embed_subwindows=true
```

because hosted `SaveLoadDialog` is a `Window`.

This setting does not configure arbitrary test `SubViewport` instances. Every host integration fixture that uses a `SubViewport` and presents a `Window` explicitly sets:

```csharp
viewport.GuiEmbedSubwindows = true;
```

The project setting is an explicit production contract, not a substitute for fixture setup.

## Process-mode migration gate

HPA-378 identifies root Pause tree pausing as the highest-risk migration step because current Pause does not pause `SceneTree`, while current Inventory does.

Verified current explicit `Always` runtime nodes:

- `GridMap` in `FloorGF.tscn`;
- `GridMap` in `Floor1F.tscn`;
- `GridMap` in `Floor2F.tscn`;
- `GridMap` in `Floor3F.tscn`;
- root `InventoryMenu` in `InventoryMenu.tscn`.

The floor roots themselves are not the verified issue; the `GridMap` children are.

### Atomic normalization order

The four `GridMap` overrides are removed first because ordinary Pause does not yet pause the tree and this is behavior-neutral for current gameplay.

The Inventory `process_mode = 3` override is **not** removed in that commit. It is removed only in the same implementation slice that:

- moves direct Inventory under `UIScreenHost`;
- deletes the controller's private `SceneTree.Paused` write/snapshot;
- makes host registration choose the Inventory process mode.

This prevents an intermediate commit where Inventory pauses the tree and then becomes unable to process its own Close/Cancel input.

### Gate before root `PauseTree=true`

Before root Pause acquires tree pause, tests must prove:

1. teardown-safe scene replacement is wired;
2. four runtime `GridMap` nodes no longer pin `Always`;
3. Inventory no longer writes `SceneTree.Paused` and its `Always` scene override is gone;
4. the composed gameplay-input predicate blocks movement/interactions under hosted UI;
5. the complete hosted Pause + child path works with `PauseTree=false`;
6. production Cancel/focus/child-return parity passes after cutover.

Only then do the two Pause policy fields change to `WhenPaused` + `PauseTree=true`.

## Scene composition

`Game.tscn` contains one `UIScreenHost` instance under the existing `UI` `CanvasLayer`, after `GameUI`, so host layers render above the HUD.

`GameplayCoreCancelActions` contains both `pause_menu` and `ui_cancel`.

`Game._EnterTree()` uses `GetNodeOrNull` as described above. The real scene test is the strict guard against accidentally removing the production host.

## Pause presentation

### `PauseScreen.tscn`

A full-rect `Control` contains `SiriusModalShell.tscn` and six labelled actions:

1. Resume
2. Inventory
3. Save
4. Load
5. Settings
6. Return to Title

Return to Title uses the existing destructive outline treatment. The child confirmation owns the final filled destructive action.

### `PauseScreenController`

Presentation-only responsibilities:

- bind authored nodes;
- expose Resume as initial focus;
- emit six action signals;
- refresh the shell on initial layout and later resize;
- use shared UI metrics rather than restating thresholds.

```csharp
private void RefreshLayout()
{
    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _shell.RefreshPresentation(size);
}
```

Subscribe to the root Control's `Resized` signal in `_Ready()` and unsubscribe in `_ExitTree()` so resizing a window after the screen opens updates compact state.

Tests use:

```csharp
var compact = SiriusUiMetrics.IsCompact(viewport.Size);
var minimumTarget = SiriusUiMetrics.MinimumTarget(compact);
```

They verify 44×44 minimum targets at 1280×720 and 40×40 at 640×360, rather than hard-coding 40 for both.

### Return-to-title confirmation

`PauseReturnToTitleConfirmation.tscn` uses `SiriusModalShell` and two actions: `Cancel` and `Return to Title`.

Its controller only emits `CancelRequested` and `ReturnToTitleConfirmed`. Duplicate navigation suppression remains in `Game` through `_sceneChangeCommitted`.

No generic confirmation abstraction is introduced ahead of HPA-572.

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
| HUD | `Inherit` |
| Cursor | `Visible` |
| LowerLayers | `VisibleInert` |
| Cancel | `Close` |
| EntryCancelActions | `toggle_inventory` |
| Lifetime | `External` |

`HUD = Inherit` intentionally preserves HPA-382's lifecycle-only scope. HPA-357 may later implement the approved inventory-specific HUD-hidden presentation.

### Inventory from Pause

| Field | Value |
|---|---|
| Kind | `UIScreenKinds.Inventory` |
| Parent | `pauseHandle` |
| Layer / priority | `Modal / Modal` |
| Process | `Always` |
| PauseTree | `false` |
| BlockGameplayInput | `false` |
| HUD | `Inherit` |
| Cursor | `Visible` |
| LowerLayers | `VisibleInert` |
| Cancel | `Close` |
| EntryCancelActions | `toggle_inventory` |
| Lifetime | `External` |

The same Inventory instance is reused. Host close detaches an `External` view. A later open presents an unparented view again and may use a different logical parent.

The host never changes parentage of an active Inventory entry in place; close first, then present again.

### Pause parity phase

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

### Final Pause policy

After the gate passes, only these fields change:

```text
ProcessPolicy: Always -> WhenPaused
PauseTree:      false  -> true
```

Children keep `PauseTree=false`; the Pause parent owns the pause lease.

### Other Pause children

Keep these policies explicit in `Game` rather than hiding them behind a speculative factory:

| Presentation | Parent | Priority | Process | PauseTree | HUD | Lifetime | Special policy |
|---|---|---|---|---:|---|---|---|
| Save/Load | Pause | Modal | Always | false | Inherit | QueueFree | overwrite-child interceptor |
| Settings | Pause | Modal | Always | false | Inherit | QueueFree | popup/rebind interceptor |
| Return confirmation | Pause | Blocking | Always | false | Inherit | QueueFree | blocking group + initial focus |

There are only three sibling policies in this ticket and the confirmation already differs materially. Explicit literals keep review of pause/block/focus ownership obvious. If later migrations create proven harmful duplication, extract then.

## Inventory lifecycle adaptation

`InventoryMenuController` loses all direct pause and terminal Cancel ownership in the same slice:

- remove `_pauseSnapshotCaptured` and `_treeWasPausedBeforeOpen`;
- remove `RestoreTreePause()`;
- remove `process_mode = 3` from `InventoryMenu.tscn`;
- `OpenMenu()` refreshes and shows only;
- `CloseMenu()` hides only;
- Close UI emits `CloseRequested`;
- `_Input()` may observe input-device changes for hint updates, but it no longer closes on `ui_cancel` or `toggle_inventory` while hosted.

`Game.SetupInventoryMenu()` instantiates one controller and keeps it unparented. Direct-open state comes from host kind state, not `Visible`.

## Hosted Settings and Save/Load adaptation

### Settings

Keep all existing editing, validation, dropdown, key-capture, and `Closed` behavior. Change only attachment/lifecycle:

1. instantiate `SettingsMenuController`;
2. do not call `UI.AddChild`;
3. call `TryPresent` so the host attaches it to `ModalLayer`;
4. use `SetPresented` to call `OpenSettings(showOverlay: false)` / hide as appropriate;
5. use the existing `IsRebinding` / `IsPopupOpen` interceptor behavior;
6. host terminal cleanup queues the controller.

### Save/Load

Keep existing modes, slot signals, overwrite child dialog, and domain callbacks. Change only attachment/lifecycle:

1. instantiate `SaveLoadDialog`;
2. do not call `UI.AddChild`;
3. call `TryPresent` so the host attaches the `Window` directly to itself;
4. use existing `ShowDialog(mode)` as the presentation callback;
5. dismiss `HasActiveChildDialog` first on Cancel;
6. host terminal cleanup queues the dialog.

Tests assert the actual parent after successful presentation for Pause, Inventory, Settings, Save/Load, and confirmation.

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

The provider is a narrow hook, not a service. Existing domain checks remain.

## Cancel ownership and cutover

The old `HandlePauseMenuInput` ladder remains production-owned while the new hosted Pause + child path is built and tested directly.

Only after the complete hosted path works does the cutover task:

1. route root core Cancel to `UIScreenHost`;
2. retain remaining unhosted domain precedence;
3. migrate `GameTest` expectations;
4. delete `PauseMenuDialog`, `_pauseMenuRestorePending`, `_saveLoadFromPause`, and obsolete ladder code.

### Final dispatch order

1. Hosted entry traversal.
2. Active error popup — dismiss and consume.
3. Battle — existing escape/result behavior and consume.
4. Puzzle riddle — decline so retained native dialog receives Cancel.
5. Atomic world interaction — consume without opening Pause.
6. NPC interaction — decline so retained native dialog receives Cancel.
7. No blocker — either core action opens Pause.

Settings and Save/Load interceptors preserve nested precedence. Inventory's `toggle_inventory` is entry-scoped while active.

This two-step build/cutover avoids both a huge single commit and an intermediate production Pause whose child actions are only partially migrated.

## Scene replacement and teardown

Do not add `PerformSceneChange` or another navigation abstraction.

Use one private helper for teardown-safe commit:

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

A finalization callback exception from `PrepareForTeardown()` propagates; it is not converted into `Deferred`. Do not add arbitrary retry-count policy for an unobserved failure mode.

Replace every direct `Game` scene change:

- defeat/dead-player/SaveLoad Main Menu paths continue to call `ReturnToMainMenu()` and therefore become teardown-safe in production;
- Return-to-Title confirmation calls `ReturnToMainMenu()`;
- in-game successful Load calls `RequestSceneChange(GameScenePath)` after setting `PendingLoadData`;
- corrupted-save return calls `RequestSceneChange(MainMenuScenePath)`.

`ReturnToMainMenu` remains the existing protected virtual seam used by synthetic lifecycle tests. No second virtual scene-change method is introduced.

## Test fixture contract

### Synthetic `Game` suites

Task 3 runs `GameTest` and `GameInputLifecycleTest` immediately after the nullable host bootstrap change to prove their existing `new TestableGame()` / `new LifecycleGame()` setup still survives `_EnterTree()` without a scene host.

When host-aware assertions are introduced at cutover, the relevant fixture setup creates a minimal UI subtree before attaching the `Game` to its viewport, or moves that case to `GameplayPauseHostTest`. Do not make every unrelated legacy test depend on the production host.

### Host integration fixtures

Reuse existing real-scene / host-test idioms instead of inventing another harness:

- load `Game.tscn` for production integration cases;
- use existing frame-await helpers;
- when using a `SubViewport`, set `GuiEmbedSubwindows = true` explicitly before presenting `SaveLoadDialog`;
- use `UIScreenHostTestSupport` patterns for host-specific setup/cleanup where applicable.

The production project setting and test viewport flag are intentionally separate.

## Testing strategy

### Component

`PauseScreenControllerTest` covers:

- six action signals;
- Resume initial focus;
- shared compact metrics at 1280×720 and 640×360;
- 44/40 px minimum target through `SiriusUiMetrics.MinimumTarget`;
- runtime viewport resize updates compact state;
- safe disconnect on teardown.

Confirmation tests cover signals and safe initial focus. Duplicate navigation is tested at `Game` integration, not in the controller.

### Integration

`GameplayPauseHostTest` covers:

- exactly one production gameplay host;
- `SubViewport` embedding setup where needed;
- Pause parent = `ModalLayer`;
- Inventory parent = `ModalLayer`, then detached on `External` close;
- Settings parent = `ModalLayer`;
- Save/Load parent = `UIScreenHost`;
- confirmation parent = `ModalLayer`;
- complete hosted path works before production cutover with `PauseTree=false`;
- repeated Pause/Resume and child return;
- nested Settings/Save Cancel precedence;
- Return-to-Title one-shot commit;
- invalid prior focus;
- teardown with Pause + child;
- final tree-pause freeze probe.

`GameTest` remains a first-class cutover suite because it contains substantial legacy Pause assumptions. `GameInputLifecycleTest` remains the physical-input/domain-order suite.

### Process-freeze regression

Use the real Game/floor fixture. With final Pause active:

- `SceneTree.Paused` is true;
- host still responds to Cancel;
- a pausable test probe beneath runtime `GridMap` stops processing;
- after Resume it advances again.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Strict `_EnterTree()` host lookup breaks synthetic tests | nullable `GetNodeOrNull`; run both large legacy suites in Task 3 |
| Inventory process override removed before pause write | remove both in the same Inventory migration commit |
| Inventory pre-parented under `UI` | keep unparented; host attaches; test detach/reuse |
| Settings pre-parented under `UI` | remove manual `AddChild`; host attaches to `ModalLayer` |
| Save/Load pre-parented under `UI` | remove manual `AddChild`; host attaches Window directly to itself |
| Test `SubViewport` rejects Window | explicitly set `GuiEmbedSubwindows=true` in fixtures |
| Explicit `Always` GridMap produces half-paused world | remove four overrides and add real-scene freeze probe |
| Legacy Inventory `_Input` races host Cancel | remove terminal Cancel/toggle branch in the Inventory migration slice |
| One giant Pause migration commit is hard to bisect | build complete hosted path first; production cutover/deletion in a second commit |
| Extra scene-change test seam drifts from existing tests | keep only `ReturnToMainMenu` virtual; private helper owns teardown |
| Inventory HUD change sneaks into lifecycle task | use `Hud=Inherit`; defer visual behavior to HPA-357 |
| Premature child-spec factory obscures policy | keep three explicit specs; extract only after measured duplication |

## Acceptance mapping

- **One local production host:** real `Game.tscn` host test.
- **Pause no longer desktop-window framed:** `PauseScreen.tscn`; delete `PauseMenuDialog` at cutover.
- **Existing actions preserved:** complete hosted-path integration plus migrated `GameTest`.
- **Deterministic Resume/child/Cancel/teardown:** host + lifecycle suites.
- **Single pause/input/cursor authority:** Inventory private pause deletion + final Pause lease assertions.
- **Later screens reuse same host:** explicit child policies use existing host contract, no new framework.
- **Focused lifecycle/integration tests:** new host suite + migrated `GameTest` + `GameInputLifecycleTest`.

## Implementation shape

1. Pause component using shared metrics + resize handling.
2. Flow-specific Return-to-Title confirmation.
3. Nullable production host bootstrap, production/test subwindow contract, and centralized teardown-safe scene replacement.
4. Composed gameplay input suppression.
5. Remove four runtime `GridMap` `Always` overrides only.
6. Direct Inventory host migration: remove its pause write, terminal `_Input` Cancel branch, and `InventoryMenu.tscn` `Always` override atomically.
7A. Build and test the complete hosted Pause + Inventory/Save/Load/Settings/confirmation path with `PauseTree=false`, without changing production root Cancel yet.
7B. Cut production root Cancel/Pause over to the hosted path, migrate legacy suites, then delete `PauseMenuDialog` and restoration flags.
8. Flip root Pause to `WhenPaused` + `PauseTree=true`; prove real gameplay freezes and host Cancel still resumes.
9. Harden physical Cancel/focus/teardown regressions.
10. Update lifecycle ownership documentation and run build/focused/full gates.

This keeps the architecture lean, keeps every intermediate commit usable, and honors the foundation's staged PauseTree migration.