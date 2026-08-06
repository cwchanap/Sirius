# HPA-382 Gameplay Pause Host Integration Design

**Linear:** HPA-382  
**Status:** Implementation-ready  
**Date:** 2026-08-06

## Decision summary

Sirius will make the existing scene-local `UIScreenHost` the production presentation authority for gameplay Pause and the legacy screens opened from Pause. The current runtime-built `PauseMenuDialog` will be deleted and replaced by a scene-authored `PauseScreen` composed from `SiriusModalShell`, the shared theme, approved icons, and input hints.

`Game` remains the flow orchestrator. `GameManager`, `InventoryMenuController`, `SaveLoadDialog`, `SettingsMenuController`, `SaveManager`, and scene navigation retain their domain responsibilities. The host owns only presentation state: tree pause, presentation input blocking, cursor, HUD visibility, lower-layer interaction, Cancel priority, focus, and teardown.

This is a vertical migration, not a second framework and not a blanket migration of every gameplay dialog.

## Why HPA-382 is the next actionable task

The shared foundation is complete: the theme, modal shell, `UIScreenHost`, focus/cancel policy, and lifecycle regression coverage already exist. HPA-382 has no remaining active blocker and unlocks the Settings, Save/Load, dialogue, shop/healing, puzzle, confirmation, and reward follow-up migrations.

The current Pause path is the highest-leverage production consumer because `Game` still manually coordinates visibility, pause restoration, Cancel precedence, focus restoration, and child return through `_pauseMenuRestorePending` and `_saveLoadFromPause`. Leaving that path in place would keep two presentation models alive and force later screens to integrate around legacy behavior.

## Goals

- Add exactly one local `UIScreenHost` to `Game.tscn` and configure it before the host enters `_Ready`.
- Replace `PauseMenuDialog` with a responsive, scene-authored Pause screen.
- Preserve Resume, Inventory, Save, Load, Settings, and Return to Title behavior.
- Keep Pause active as the logical parent while a child screen is open so closing the child restores Pause and focus deterministically.
- Make Return to Title an explicit child confirmation and guarantee one navigation request.
- Route hosted Cancel handling through `UIScreenHost`; retain existing domain-specific escape behavior for battle and unhosted legacy interactions.
- Restore the exact incoming tree pause, cursor, HUD, lower-layer, and focus state on close and teardown.
- Validate 1280×720 and the minimum supported 640×360 viewport without adding a full viewport matrix.

## Non-goals

- Redesigning Inventory, Settings, Save/Load, battle, dialogue, shop, healing, puzzles, or notifications.
- Creating another navigation service, modal manager, screen registry, or generic confirmation framework.
- Hosting every legacy dialog in this ticket.
- Changing save, settings, inventory, battle, or scene-navigation domain rules.
- Adding animation infrastructure, new Pause features, or navigation history.
- Preserving the deleted `PauseMenuDialog` API or adding compatibility wrappers; there are no production consumers outside this repository.

## Approaches considered

### 1. Restyle `PauseMenuDialog` and keep manual orchestration

This is the smallest visual change, but it leaves `Game._Input`, `_pauseMenuRestorePending`, `_saveLoadFromPause`, manual tree-pause behavior, and popup focus as the real lifecycle authority. Later migrations would still need to unwind it. Rejected.

### 2. Host only Pause, but hide/show unhosted children manually

This would make the host own Pause while Inventory or Save/Load still owns part of pause, visibility, and Cancel behavior. It creates the exact dual-authority state HPA-382 is intended to remove. Rejected.

### 3. Host Pause and only the legacy children Pause opens

This uses the foundation already delivered, removes the legacy restoration flags, and keeps the migration bounded. Inventory receives a lifecycle-only adaptation so it no longer mutates `SceneTree.Paused`; Settings and Save/Load keep their existing domain controllers and are registered through narrow adapters. Recommended.

## Scene composition

`Game.tscn` will contain one `UIScreenHost` instance under the existing `UI` `CanvasLayer`, after `GameUI` so hosted layers render above the HUD. `GameUI` remains at its existing path and is passed as `HudRoot`; reparenting the current HUD is unnecessary for this migration.

`Game._EnterTree()` configures the scene-authored host before child `_Ready()` executes:

- `HudRoot = UI/GameUI`
- `CoreCancelActions = { pause_menu, ui_cancel }`
- `RootCancelFallback = HandleGameplayRootCancel`
- `GameplayInputBlockChanged = SetPresentationGameplayBlocked`

Embedded subwindows are explicitly enabled in `project.godot` because the existing `SaveLoadDialog` is an `AcceptDialog` registered as a host child.

The root remains scene-local. No autoload or global presentation singleton is introduced.

## New Pause presentation

### `PauseScreen.tscn`

The scene is a full-rect `Control` that contains `SiriusModalShell.tscn`. It authors six labelled buttons in this order:

1. Resume
2. Inventory
3. Save
4. Load
5. Settings
6. Return to Title

The scene uses existing theme variations and approved icons. Return to Title uses destructive outline treatment; the final confirmation owns the filled destructive action. The action list is vertical so it remains readable at 640×360. Long content is not added, so the modal body should not require scrolling at either required viewport.

### `PauseScreenController.cs`

The controller has one responsibility: bind scene nodes, maintain responsive shell presentation, update input hints, expose the Resume focus target, and emit action requests. It does not pause the tree, change scenes, open child screens, or call domain managers.

`Resume` receives initial focus. Button signals are disconnected in `_ExitTree`. Compact mode is selected using the established threshold: width below 800 or height below 450.

### Return-to-title confirmation

A narrow `PauseReturnToTitleConfirmation.tscn` uses `SiriusModalShell` and two buttons: `Return to Title` and `Cancel`. It is specific to this flow; HPA-382 does not introduce a generic confirmation abstraction ahead of HPA-572.

The confirmation is a logical child of Pause with `UIScreenKinds.ConfirmQuitToMain`. The host's unique-kind rule prevents duplicate confirmation instances, while `Game` also guards the committed navigation action so rapid repeated button input cannot request navigation twice.

## Host entry policies

| Presentation | Parent | Layer / priority | Process | Pause | Gameplay block | HUD | Cursor | Lower layers | Cancel | Lifetime |
|---|---|---|---|---:|---:|---|---|---|---|---|
| Pause | none | Modal / Modal | WhenPaused | yes | yes | Visible | Visible | VisibleInert | Close | QueueFree |
| Inventory opened directly | none | Modal / Modal | WhenPaused | yes | yes | Hidden | Visible | VisibleInert | Close | External |
| Inventory from Pause | Pause | Modal / Modal | Always | no | no | Hidden | Visible | VisibleInert | Close | External |
| Save/Load from Pause | Pause | Modal / Modal | Always | no | no | Inherit | Visible | VisibleInert | Close | QueueFree |
| Settings from Pause | Pause | Modal / Modal | Always | no | no | Inherit | Visible | VisibleInert | Close | QueueFree |
| Return-to-title confirmation | Pause | Modal / Blocking | Always | no | no | Inherit | Visible | VisibleInert | Close | QueueFree |

Notes:

- Pause keeps the gameplay HUD visible but inert beneath the full-screen scrim, matching the approved lifecycle specification.
- Inventory hides the gameplay HUD in both direct and Pause-child contexts.
- Children do not acquire a second pause lease. The Pause parent remains active and owns pause while they are open.
- Child `LowerLayers = VisibleInert` keeps Pause visible beneath the child scrim and prevents interaction.
- Parent handles are explicit; Godot node parentage is not used as a navigation stack.
- Save/Load and Settings are hosted only in the Pause flow in this ticket. Their Main Menu migration remains owned by their later vertical tickets.

## Gameplay input and Cancel ownership

### Composed gameplay suppression

`Game` stores the host's presentation-block contribution and exposes one composed predicate:

```csharp
private bool IsGameplayInputSuppressed() =>
    _presentationGameplayBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

`PlayerController` receives this predicate through one optional provider configured by `Game`. It remains usable in isolated tests when no provider is supplied. This avoids a new service while ensuring movement and interaction commands respect hosted presentations as well as domain state.

Direct Inventory opening also uses the composed predicate.

### Root Cancel fallback

`UIScreenHost` owns Cancel for hosted entries. When no hosted entry claims a core action, `HandleGameplayRootCancel` preserves only the necessary domain behavior:

- dismiss an active save/load error first;
- close an active battle through its existing escape path;
- reserve or decline events that must reach an unhosted native NPC, puzzle, dropdown, or key-capture handler;
- decline while a world interaction cannot be canceled;
- otherwise present Pause.

`Game._Input()` no longer opens, hides, or restores Pause. It retains direct Inventory opening and unrelated domain input only. Hosted child precedence comes from logical parentage and host input priority rather than the current ordered chain of nullable fields.

### Child-specific Cancel interception

- Inventory: `ui_cancel` and `toggle_inventory` close its host handle.
- Settings: an open dropdown or active key capture reserves Cancel for the existing controller; otherwise Cancel closes the Settings host handle and discards staged edits through the existing close path.
- Save/Load: an overwrite confirmation is dismissed first through `HasActiveChildDialog` / `DismissActiveChildDialog`; otherwise Cancel closes Save/Load and returns to Pause.
- Return-to-title confirmation: Cancel closes only the confirmation and restores focus to Return to Title.

One physical event produces one host traversal and one logical close.

## Legacy child adaptation

### Inventory

`InventoryMenuController` currently snapshots and mutates `SceneTree.Paused`. That behavior is removed. The controller becomes presentation/domain UI only:

- `OpenMenu()` refreshes and shows the existing screen.
- `CloseRequested` is emitted by the Close button instead of directly restoring tree pause.
- `CloseMenu()` hides the screen and is used by the host adapter cleanup.
- Its `_Input()` keeps device-hint observation but does not own Cancel or `toggle_inventory` lifecycle.

Both direct Inventory and Pause-child Inventory are registered with the host, so no standalone pause compatibility mode is required.

### Settings

The existing `Closed` signal, `OpenSettings(showOverlay: false)`, `IsRebinding`, and `IsPopupOpen` remain the integration surface. No settings fields, validation, persistence, or layout are redesigned.

### Save/Load

The existing mode, slot signals, overwrite child dialog, and save/load domain callbacks remain. `Game` supplies host presentation callbacks so opening uses `ShowDialog(mode)` and closing uses the existing terminal cleanup. `_saveLoadFromPause` is deleted because logical parentage now identifies the return destination.

## Flow behavior

### Open and Resume

1. Root Cancel fallback validates domain eligibility.
2. `Game` instantiates `PauseScreen.tscn` and calls `TryPresent` with the Pause policy.
3. The host acquires pause, gameplay block, cursor, HUD, lower-layer, and focus leases atomically.
4. Resume or Cancel calls `TryClose` on the Pause handle.
5. The host closes descendants first, restores leases, and focuses gameplay.

A repeated Pause action while Pause is topmost closes it once. A repeated open request is rejected by the unique Pause kind rather than creating another screen.

### Open and return from a child

1. The Pause action handler instantiates or reuses the existing child controller.
2. `Game` calls `TryPresent` with `Parent = pauseHandle`.
3. The child becomes the top input owner; Pause remains active and visible inert.
4. Child terminal signals call `TryClose(childHandle)`.
5. The host restores Pause presentation and its captured focus target without rebuilding Pause or using deferred popup flags.

### Return to Title

1. Return to Title opens the dedicated confirmation child.
2. Cancel closes only the confirmation.
3. Confirm sets the one-shot navigation guard, closes the Pause subtree, prepares the host for teardown, then changes to `MainMenu.tscn`.
4. Further confirm input is ignored.

## Scene change and teardown

All `Game`-owned scene changes that can occur while hosted UI is active use one private helper:

```text
request scene change
→ PrepareForTeardown
→ retry deferred preparation after the current host mutation
→ change scene only after Complete
```

This covers Return to Title and in-game Load. Host cleanup restores the exact incoming pause, cursor, HUD, interaction, and focus state before the containing scene is replaced. `_ExitTree` remains a defensive disconnect path, not the primary place to begin host teardown after children have already started exiting.

No asynchronous navigation service is added; a small deferred retry in `Game` is sufficient.

## Error handling

- Any `TryPresent` result other than `Opened` is logged with kind and status.
- A failed Pause open leaves gameplay unchanged and frees the unregistered scene instance.
- A failed child open leaves Pause active, restores its focus, and frees the candidate.
- `HostMutating` is retried once through `CallDeferred`; it is never silently queued inside the host.
- Duplicate-kind results are treated as idempotent no-ops.
- If teardown preparation is deferred, scene navigation is deferred and retried; the scene is not changed early.
- Existing save/settings/inventory domain errors continue through their current controllers.

## Testing strategy

### Component tests

Replace `PauseMenuDialogTest` with `PauseScreenControllerTest` covering all six action signals, Resume initial focus, one signal per press, destructive action identity, input-hint refresh, and disconnect-safe teardown.

Add a focused confirmation controller test covering Confirm, Cancel, initial focus, and double-confirm suppression at the Game integration boundary.

### Gameplay integration tests

Extend the existing lifecycle suite instead of creating a parallel harness:

- Pause opens through the host and owns pause, gameplay block, cursor, HUD, lower layers, and focus.
- Pause action repeated closes the same entry and restores gameplay once.
- Direct Inventory is host-owned and no longer changes tree pause itself.
- Inventory, Save/Load, and Settings opened from Pause are logical children and return to the same Pause instance.
- Save overwrite confirmation and Settings dropdown/key capture consume Cancel before their parent.
- Return-to-title confirmation closes child-first and navigation commits once.
- Invalid previous focus falls back to Resume and later to the host focus sink/gameplay without an exception.
- Preparing scene teardown with Pause and a child open closes descendants, restores leases, and leaves no active host entries.
- Existing battle, NPC, puzzle, error, and corrupted-save escape behavior remains covered.

### Responsive verification

Instantiate `PauseScreen.tscn` at 1280×720 and 640×360. Assert that the shell stays inside safe margins, all six actions remain visible and at least 40 px high in compact mode, and no horizontal overflow is introduced.

### Commands

```bash
dotnet build Sirius.sln
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~InventoryMenuControllerTest"
dotnet test Sirius.sln --settings test.runsettings.local
```

## Acceptance mapping

- One local production host: scene-authored `UI/UIScreenHost`, configured in `Game._EnterTree`.
- No desktop Pause framing: `PauseMenuDialog` is deleted; `PauseScreen` uses `SiriusModalShell`.
- Existing actions preserved: Game handlers delegate to current controllers/managers.
- Deterministic Resume, child return, confirmation, Cancel, and teardown: logical parent handles and host close ordering replace restoration flags.
- One authority: Inventory stops mutating tree pause; hosted paths do not manually hide/re-popup Pause.
- Later migrations: the same gameplay host remains available for their own vertical registration.
- Focused tests: existing host/lifecycle suites plus Pause-specific component and responsive coverage.

## Files affected

### Create

- `scenes/ui/PauseScreen.tscn`
- `scripts/ui/PauseScreenController.cs`
- `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- `tests/ui/PauseScreenControllerTest.cs`
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`

### Modify

- `scenes/game/Game.tscn`
- `project.godot`
- `scripts/game/Game.cs`
- `scripts/game/PlayerController.cs`
- `scripts/ui/InventoryMenuController.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/PlayerControllerTest.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete

- `scripts/ui/PauseMenuDialog.cs`
- `tests/ui/PauseMenuDialogTest.cs`
