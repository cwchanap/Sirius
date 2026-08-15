# HPA-572: Host-managed confirmations, warnings, and errors

## Goal

Standardize Sirius confirmations, warnings, and errors on one scene-authored prompt surface that is presented through the invoking root's existing `UIScreenHost`.

This ticket replaces duplicate feature-specific confirmation scenes and the remaining ad hoc native `AcceptDialog` error paths that already belong to migrated Main Menu / gameplay flows. It does not pre-migrate dialogue, shop, healing, puzzle/riddle, reward, or other screens that have their own vertical tickets.

## Current state

Sirius already has the infrastructure HPA-572 needs:

- `SiriusModalShell` owns themed modal presentation, severity icon/panel styling, responsive width, bounded body height, and scrolling.
- `UIScreenHost` already owns logical parent/child ordering, topmost input, Cancel routing, lower-layer inertness, focus restoration, node lifetime, and teardown.
- `SiriusUiSeverity` already provides `Info`, `Warning`, and `Error` visual semantics.
- `UIScreenExclusiveGroups.BlockingPrompt` already prevents unrelated blocking prompts from stacking.

The remaining prompt implementations are fragmented:

- `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` provide one destructive confirmation under Pause.
- `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` provide another destructive confirmation under Save/Load.
- `MainMenu.TryOpenMessage(...)` builds an `AcceptDialog` at runtime and hosts it as `SaveError`.
- `Game.ShowSaveError(...)` builds an unhosted `AcceptDialog` directly under `UI`.
- `Game.ShowCorruptedSaveError()` builds another unhosted `AcceptDialog`, disables `Game` input manually, and owns separate double-terminal cleanup.

Those are enough concrete consumers to justify one shared prompt leaf. They do not justify a global notification service, presenter, router, or generic UI flow framework.

## Design decisions

### 1. One reusable scene and controller

Add:

- `scenes/ui/SiriusPrompt.tscn`
- `scripts/ui/SiriusPromptController.cs`
- `SiriusPromptVariant` in the existing UI type area or beside the controller, whichever keeps the enum local without creating a new abstraction file solely for one enum.

`SiriusPrompt.tscn` instances `SiriusModalShell` and authors one message label plus two action buttons. The controller configures the existing nodes; it does not construct controls at runtime.

The component owns only presentation state and terminal intent. Domain behavior stays in `Game`, `MainMenu`, `SaveLoadScreenController`, Pause, and future owning screens.

### 2. Exactly five variants

HPA-572 supports only these variants:

| Variant | Severity | Actions | Safe initial focus | Cancel behavior |
|---|---|---|---|---|
| `InformationalConfirmation` | `Info` | Cancel + configurable primary | Cancel | emit Cancel |
| `DestructiveConfirmation` | `Warning` | Cancel + destructive primary | Cancel | emit Cancel |
| `Warning` | `Warning` | one acknowledge action | Primary | emit Primary |
| `RecoverableError` | `Error` | one acknowledge action | Primary | emit Primary |
| `BlockingError` | `Error` | one required terminal action | Primary | emit Primary |

`BlockingError` deliberately treats configured Cancel as the same terminal action as its visible primary button. This preserves the current corrupted-save behavior where Confirm and Cancel both complete the mandatory return-to-title path while removing the manual input-disable special case.

No extra severity or prompt type is added for success messages, rewards, toasts, retries, yes/no business decisions, or future speculative cases.

### 3. Small configuration surface

The controller needs only the data required to render the prompt:

- `Variant`
- `Title`
- `Message`
- `PrimaryActionText`
- optional `CancelActionText` for the two confirmation variants

The scene provides sensible authored defaults so tests/showcase instantiation remains valid before configuration.

The controller exposes:

- `Control InitialFocusTarget`
- one terminal event for primary action
- one terminal event for cancel action
- a method/property configuration surface usable before or after `_Ready`

The controller guards terminal emission with one boolean latch. Button presses and host-routed Cancel therefore cannot invoke the owning action twice.

Do not add callbacks, async tasks, recovery delegates, host handles, navigation logic, or domain result payloads to `SiriusPromptController`.

### 4. Root-local hosting remains the integration model

Do not add `PromptService`, `PromptPresenter`, a singleton, or a generic host facade.

`Game` and `MainMenu` already own their local `UIScreenHost`, host handles, domain callbacks, and teardown behavior. Each root gets one small prompt-opening helper shaped around its existing code. Duplication between two roots is acceptable; a third genuinely similar root can justify extraction later.

All prompt entries use the existing host instead of adding themselves directly to a UI node.

Common host policy:

- `Kind = UIScreenKinds.Prompt`
- `Layer = UIScreenLayer.Modal`
- `InputPriority = UIInputPriority.Blocking`
- `ProcessPolicy = UIProcessPolicy.Always`
- `ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt`
- `PauseTree = false`
- `Cursor = UICursorPolicy.Visible`
- `LowerLayers = UILowerLayerPolicy.VisibleInert`
- `NodeLifetime = UINodeLifetime.QueueFree`
- `InitialFocus = () => prompt.InitialFocusTarget`

Parent/lease policy remains root-owned:

- A child prompt receives the active parent handle. The parent remains visible and active but inert; closing the prompt restores parent focus through the host.
- A Main Menu root prompt has no logical parent and restores the invoking Main Menu control when appropriate.
- The corrupted-save `BlockingError` is a gameplay-root prompt with `BlockGameplayInput = true`; it replaces the current manual `SetProcessInput(false)` guard.
- Other prompts inherit their already-blocking/paused parent context and do not acquire a second pause or gameplay-block lease.

### 5. One prompt kind, not one kind per message

Add `UIScreenKinds.Prompt` and delete the prompt-only kinds whose concrete presentation disappears:

- `ConfirmOverwrite`
- `ConfirmQuitToMain`
- `SaveError`
- `CorruptSaveError`

`BlockingPrompt` remains the exclusivity mechanism. Only one Sirius prompt should be active in a root host at a time; the domain reason belongs in root state/callbacks, not in the host kind taxonomy.

There is no backward-compatibility requirement for the old kinds because there are no external consumers.

## Concrete migration scope

### Save overwrite

Replace `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns:

- the selected slot number,
- the prompt text,
- overwrite confirmation callback,
- the call back into the existing save path.

The prompt remains a logical child of the active Save/Load handle. Cancel closes only the prompt and restores Save/Load focus.

Delete the feature-specific scene, controller, and their dedicated tests after equivalent shared-prompt and integration coverage exists.

### Pause return to title

Replace `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns the actual `ReturnToMainMenu()` navigation and teardown-safe scene transition. The prompt remains a logical child of Pause. Cancel closes only the prompt and restores Pause focus.

Delete the feature-specific scene, controller, and their dedicated tests after equivalent coverage exists.

### Main Menu messages

Replace the runtime `AcceptDialog` in `MainMenu.TryOpenMessage(...)` with `SiriusPromptController`.

Map current messages by intent:

- no save files: `Warning`
- Settings/Load screen unavailable: `RecoverableError`
- Continue/manual load failure: `RecoverableError`

When the failure occurs while hosted Load is active, keep Load active and present the error as its logical child instead of closing Load first and reopening an unrelated root message. This makes HPA-572's parent-retention behavior concrete and leaves the player at the failed Load flow after acknowledgement.

Root-level Main Menu messages restore the invoking button when they close.

### Gameplay save/load errors

Replace `Game.ShowSaveError(...)` with a recoverable hosted prompt.

Save/load validation and persistence decisions stay in `Game`; only presentation changes. When Save/Load is active, keep it as the parent so the player acknowledges the error and returns to the same screen.

This removes `_activeErrorPopup` and the root-Cancel branch that manually frees it.

### Corrupted-save startup error

Replace `Game.ShowCorruptedSaveError()` with a root `BlockingError`.

Keep existing domain semantics:

- show only once,
- do not continue normal initialization,
- require a terminal acknowledgement,
- return to Main Menu exactly once,
- use the existing teardown-safe `RequestSceneChange(...)` path.

Configured Cancel and the visible action both emit the same primary terminal result. The host owns gameplay input suppression while this prompt exists, so `SetProcessInput(false)` is removed.

## Deferred legacy call sites

Do not migrate these under HPA-572:

- `DialogueDialog`
- `ShopDialog`
- `HealDialog`
- `PuzzleRiddleDialog`
- reward presentation

HPA-569, HPA-570, HPA-571, and HPA-573 own those screen migrations. They should adopt `SiriusPrompt` when they need a confirmation/warning/error during their own vertical slice rather than HPA-572 pre-wrapping their legacy presentation now.

## Layout and visual behavior

`SiriusPrompt` reuses `SiriusModalShell`; no Theme token, modal-shell API, icon, or metric is added unless a real prompt test proves an existing shared contract is broken.

The controller maps variant to:

- `SiriusUiSeverity`
- button visibility/order
- primary button theme variation
- initial focus

Button styling uses existing variations:

- informational primary: `SiriusPrimaryButton`
- destructive primary: `SiriusDestructiveButton` (Save overwrite no longer needs its current warning-button special case)
- warning/error acknowledgement: `SiriusPrimaryButton`
- cancel: `SiriusSecondaryButton`

`SiriusUiMetrics.MinimumTarget(...)` continues to size actions. `SiriusModalShell` owns compact mode, safe modal width, body scrolling, and long-content fit.

The shared scene must remain usable at the repository's minimum verification viewport with representative long wrapped text and both confirmation buttons visible/reachable.

## Input, focus, and lifetime

### Child-first Cancel

The host remains the top-level Cancel authority. Prompt-specific Cancel handling maps the input to the same latched controller terminal event as the corresponding visible button:

- confirmation variants -> Cancel result,
- warning/recoverable error -> Primary acknowledgement,
- blocking error -> Primary mandatory action.

The root then closes the prompt through `UIScreenHost.TryClose(...)`. The same input event must not fall through to the parent or root fallback.

### Focus

- Confirmation variants focus Cancel first.
- Acknowledgement/error variants focus their only primary button.
- Closing a child prompt restores the still-active logical parent through the existing host focus coordinator.
- Closing a root Main Menu prompt restores the invoking root control when still valid.
- Programmatic close and parent teardown do not manually grab focus; host cleanup/focus finalization remains authoritative.

### Double activation

`SiriusPromptController` emits at most one terminal signal per presentation. The owning root also keeps its existing one-way navigation/teardown guards. Rapid button presses, Cancel followed by click, or a button signal racing host cleanup must not invoke a domain action twice.

### Parent teardown and programmatic close

No prompt owns its own host handle. `Game` / `MainMenu` own the handle and clear it from host cleanup.

If a parent closes, the host closes the prompt as a descendant with `ParentClosed`. If the root begins scene teardown, `PrepareForTeardown()` closes prompts with the rest of the stack. Cleanup disconnects signals and clears root references exactly once.

No queued retry, persistence, acknowledgement token, or cross-scene prompt state is introduced.

## Testing strategy

### Shared prompt component

Add focused controller/scene tests covering:

- all five variants map to the expected severity, buttons, button styles, and initial focus;
- informational and destructive confirmations emit Cancel/Primary exactly once;
- warning and recoverable error acknowledge exactly once;
- blocking error maps both visible primary and configured Cancel to the same primary terminal result;
- repeated activation after a terminal result is ignored;
- compact and standard layouts use existing minimum target sizing;
- long wrapped text at the minimum viewport remains inside the modal and is scrollable/reachable as needed.

Do not add trivial property/accessor tests.

### Host/integration regressions

Preserve or migrate coverage for these concrete journeys:

1. **Nested destructive confirmation:** Save/Load -> overwrite prompt; Cancel closes only prompt and restores Save/Load; Confirm saves once.
2. **Second destructive consumer:** Pause -> return-to-title prompt; Cancel restores Pause; Confirm requests navigation once.
3. **Recoverable error:** failed save/load while Save/Load is active leaves Save/Load as the parent and returns focus there after acknowledgement.
4. **Main Menu recoverable/warning path:** root message keeps Main Menu beneath it and restores the invoking button.
5. **Blocking error:** corrupted startup save suppresses gameplay presentation/input and one acknowledgement or configured Cancel requests Main Menu exactly once.
6. **Programmatic close:** closing a prompt through the root helper leaves no active prompt handle or blocking group entry.
7. **Parent/root teardown:** closing Save/Load/Pause or preparing root teardown removes descendant prompts and clears references without leaked input/focus leases.
8. **Double activation:** terminal callbacks cannot run twice under repeated action input.

Update the HPA-376 lifecycle contract only where the migrated prompt/error rows changed. Do not rewrite unrelated rows.

## Implementation shape

The implementation should fit into four independently verifiable slices:

1. Add the shared `SiriusPrompt` scene/controller/variant and focused responsive/terminal tests.
2. Replace Save overwrite and Pause return-to-title with the shared prompt; delete their dedicated scenes/controllers and migrate tests.
3. Replace Main Menu and gameplay recoverable native errors while preserving active parents and focus.
4. Replace corrupted-save blocking error, remove stale native prompt state/kinds, reconcile lifecycle docs, and run full verification/stale-reference audits.

The detailed TDD implementation plan is written only after this design spec is reviewed.

## Out of scope

- toast/reward queues or HPA-573 reward presentation
- global notification/prompt singleton
- generic prompt presenter/service/host facade
- retry/recovery business logic inside the prompt
- dialogue/shop/healing/puzzle/riddle migration
- new Theme tokens, new icons, or new `SiriusModalShell` APIs without a reproduced shared-component defect
- cross-scene prompt persistence or acknowledgement protocol
- localization/accessibility framework work beyond preserving existing responsive/focus behavior
- compatibility shims for deleted prompt scenes, controllers, or kinds

## Acceptance criteria

- One scene-authored `SiriusPrompt` provides the five HPA-572 variants using existing Sirius visual primitives.
- Save overwrite and Pause return-to-title use the shared destructive confirmation and their duplicate feature-specific prompt scenes/controllers are removed.
- Main Menu and gameplay migrated warning/recoverable-error paths no longer construct native `AcceptDialog`s.
- Corrupted-save startup uses a host-managed blocking error and no longer manually disables `Game` input.
- Child prompts keep their parent active/inert, Cancel is child-first, and focus restoration is deterministic.
- Prompt terminal actions are latched against double activation.
- Programmatic close, parent close, and root teardown leave no prompt host entry, exclusive-group ownership, input lock, or stale root reference.
- Representative long text remains usable at the minimum viewport.
- Legacy dialogue/shop/healing/puzzle/riddle presentation is unchanged under this ticket.
