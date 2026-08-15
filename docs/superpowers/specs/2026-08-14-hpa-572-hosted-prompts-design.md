# HPA-572: Host-managed confirmations, warnings, and errors

## Goal

Standardize Sirius confirmations, warnings, and errors on one scene-authored modal prompt component presented through the invoking root's existing `UIScreenHost`.

This ticket replaces the two duplicate feature-specific confirmation scenes and the remaining native `AcceptDialog` error paths that already belong to migrated Main Menu / gameplay flows. It does not pre-migrate Dialogue, Shop, Healing, Puzzle/Riddle, Reward, or other screens that have their own vertical tickets.

## Current state

Sirius already has the infrastructure HPA-572 needs:

- `SiriusModalShell` owns modal chrome, severity icon/panel styling, responsive width, bounded body height, and scrolling.
- `UIScreenHost` owns logical parent/child ordering, topmost input, Cancel routing, lower-layer inertness, focus restoration, process policy, node lifetime, gameplay-input leases, and teardown.
- `SiriusUiSeverity` already provides the `Info`, `Warning`, and `Error` visual semantics needed by the current callers.
- `UIScreenExclusiveGroups.BlockingPrompt` already prevents unrelated blocking prompts from stacking.
- `Game` and `MainMenu` already own local `UIScreenHost` instances and feature-local `TryPresent` helpers; no third hosting abstraction is required.

The remaining prompt implementations are fragmented:

- `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` provide one destructive confirmation under Pause. The controller currently emits terminal signals without a one-shot latch.
- `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` provide another destructive confirmation under Save/Load. This controller already has the one-shot latch pattern HPA-572 should reuse.
- `MainMenu.TryOpenMessage(...)` builds an `AcceptDialog` at runtime and hosts it as `SaveError`.
- `Game.ShowSaveError(...)` builds an unhosted `AcceptDialog` directly under `UI` and forces `ProcessMode.Always` so it remains dismissible while Pause owns the tree-pause lease.
- `Game.ShowCorruptedSaveError()` builds another unhosted `AcceptDialog`, disables `Game` input manually, and owns separate double-terminal cleanup.

Those concrete consumers justify one shared modal leaf. They do not justify a prompt service, presenter, queue, router, global singleton, or host facade.

`SiriusContextPrompt` is intentionally unrelated. It is a HUD `HBoxContainer` for contextual input hints, not modal presentation, and remains unchanged.

## Design decisions

### 1. Put the shared leaf with the other UI components

Add:

- `scenes/ui/components/SiriusPrompt.tscn`
- `scripts/ui/components/SiriusPrompt.cs`
- `tests/ui/components/SiriusPromptTest.cs`

Define `SiriusPromptVariant` in `SiriusPrompt.cs` immediately before the component. The enum is local to this leaf and does not justify another abstraction file.

`SiriusPrompt.tscn` instances `SiriusModalShell` and authors one wrapped message label plus two action buttons. `SiriusPrompt` configures those existing nodes; it does not build controls at runtime.

The component owns only chrome, button state, responsive sizing, and terminal intent. Domain behavior stays in `Game` / `MainMenu` closures.

### 2. Ship only three presentation variants

Current production consumers need three distinct presentation shapes:

| Variant | Severity | Actions | Safe initial focus | `RequestCancel()` |
|---|---|---|---|---|
| `DestructiveConfirmation` | `Warning` | Cancel + destructive primary | Cancel | emit Cancel |
| `Warning` | `Warning` | one acknowledgement action | Primary | emit Primary |
| `RecoverableError` | `Error` | one acknowledgement action | Primary | emit Primary |

Do **not** add `InformationalConfirmation` now: there is no current two-button Info confirmation consumer. Adding it later is one switch arm when a real caller appears.

Do **not** add `BlockingError`: gameplay blocking is host policy, not chrome. The corrupted-save startup path uses `RecoverableError` presentation plus `BlockGameplayInput = true` on its `UIScreenEntrySpec`. That keeps the visual component truthful and prevents an enum name from pretending it automatically acquires a host lease.

No success/reward/toast/retry variants are added.

### 3. Keep the component API small

Expose only:

```csharp
public enum SiriusPromptVariant
{
    DestructiveConfirmation,
    Warning,
    RecoverableError
}

public partial class SiriusPrompt : Control
{
    [Signal] public delegate void PrimaryRequestedEventHandler();
    [Signal] public delegate void CancelRequestedEventHandler();

    public Control InitialFocusTarget { get; }

    public void Configure(
        SiriusPromptVariant variant,
        string title,
        string message,
        string primaryActionText,
        string cancelActionText = "Cancel");

    public void RequestCancel();
}
```

`Configure(...)` works before or after `_Ready`: before-ready calls store values and `_Ready` applies them. `RequestCancel()` routes configured Cancel through the same terminal latch as visible buttons.

One boolean latch permits at most one terminal signal per prompt instance. Every host presentation creates a fresh prompt; the latch is never reset.

For Save overwrite, this preserves the existing one-shot behavior. For Pause return-to-title, adopting the shared latch is an intentional bug fix: the current controller emits on every button press and does not guard duplicate terminal activation.

Do not add callbacks, async tasks, host handles, navigation logic, recovery delegates, or domain payloads to `SiriusPrompt`.

### 4. Root-local hosting remains the integration model

Do not add `PromptService`, `PromptPresenter`, a singleton, a notification queue, a `SiriusPrompt.SpecFor(...)` host-policy factory, or a generic host facade.

`Game` and `MainMenu` each keep one private prompt-opening helper shaped around their existing local host state. Some duplicated `UIScreenEntrySpec` plumbing between two roots is intentional: host ownership, cleanup, restore-focus targets, and domain closures are root concerns, not visual-component concerns. A third real root can justify extraction later.

Common host policy is written explicitly in both roots:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Prompt,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Blocking,
    ProcessPolicy = UIProcessPolicy.Always,
    Parent = parent,
    ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt,
    PauseTree = false,
    BlockGameplayInput = blockGameplayInput,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Inherit,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Consume,
    InterceptCancel = _ =>
    {
        prompt.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    InitialFocus = () => prompt.InitialFocusTarget,
    RestoreFocus = restoreFocus == null ? null : () => restoreFocus,
    Cleanup = _ => ClearHostedPrompt(prompt),
    NodeLifetime = UINodeLifetime.QueueFree
};
```

`Cancel = Consume` + `InterceptCancel -> RequestCancel()` is load-bearing. `Cancel = Close` would bypass the prompt latch and cannot implement Cancel-as-primary behavior for one-action prompts.

Rather than centralizing the spec, both root integration suites must assert `Policy.Cancel == UICancelPolicy.Consume` and exercise configured Cancel end-to-end. This catches semantic drift without moving host policy into the component layer.

### 5. Terminal domain closures are not gated on close status

The component spends its one-shot terminal latch **before** emitting `PrimaryRequested` / `CancelRequested`. Therefore root handlers must not make the domain action conditional on `TryClose(...) == Closed`; a stale/already-closing/tearing-down host state would otherwise consume the only terminal intent and suppress the domain action permanently.

Root handlers use:

```csharp
private void OnHostedPromptPrimaryRequested()
{
    var action = _hostedPromptPrimaryAction;
    TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction);
    action?.Invoke();
}

private void OnHostedPromptCancelRequested()
{
    var action = _hostedPromptCancelAction;
    TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction);
    action?.Invoke();
}
```

Capture remains required because successful host cleanup clears root-owned closures synchronously. The close attempt still happens first so a normal terminal action removes the old Prompt before a domain callback opens another presentation.

Existing scene-transition guards and save-domain guards remain authoritative for duplicate domain effects.

### 6. Parent retention is the normal recoverable-error path

A child prompt receives the active logical parent handle. The parent remains visible and active but inert; `UIScreenHost` restores parent focus when the prompt closes.

In particular, failed Save/Load no longer closes Save/Load before presenting its error. The flow becomes:

```text
Pause -> Save/Load -> RecoverableError Prompt
```

or, from Main Menu:

```text
Main Menu -> Load -> RecoverableError Prompt
```

Acknowledgement closes only `Prompt`; `SaveLoad` remains active.

This also preserves the important paused-tree behavior: gameplay Pause can keep `SceneTree.Paused == true`, while Save/Load and its prompt both use `UIProcessPolicy.Always`, so the prompt remains dismissible without its own native `ProcessMode.Always` workaround.

`Game.ShowSaveError(...)` is only valid while hosted Save/Load is active after this migration. If that parent is unexpectedly missing, it must `GD.PushError(...)` and return `false`; it must not silently pretend presentation succeeded and must not create an unhosted/native fallback.

### 7. Corrupted-save startup reuses recoverable-error chrome and has a navigation fallback

`Game.ShowCorruptedSaveError()` presents a root `RecoverableError` prompt with:

- no logical parent;
- `BlockGameplayInput = true`;
- primary text `Return to Title`;
- `onPrimary: ReturnToMainMenu`.

Keep existing domain semantics:

- show once;
- abort ordinary initialization;
- require a terminal acknowledgement when the prompt opens;
- configured Cancel performs the same primary action as the visible button because `RecoverableError.RequestCancel()` emits Primary;
- return to Main Menu exactly once through existing teardown-safe scene navigation.

The host's gameplay-block lease replaces `SetProcessInput(false)`. `Game` stays input-processing so `UIScreenHost` can remain the Cancel authority.

The presentation attempt must not become a new dead-end. `_hasShownCorruptedSaveError` is set once before opening, but if `TryOpenHostedPrompt(...)` returns `false`, log the presentation failure and immediately call `ReturnToMainMenu()`. This preserves the current guaranteed exit even when the shared prompt scene/host is unavailable or rejects the entry.

### 8. One product prompt kind; host tests use fixture-local kinds

Add `UIScreenKinds.Prompt` and remove the product-specific prompt kinds once production is migrated:

- `ConfirmOverwrite`
- `ConfirmQuitToMain`
- `SaveError`
- `CorruptSaveError`

Do not retarget every host unit-test fixture to `Prompt`. Several host tests deliberately require **two distinct kinds** to prove group conflicts or ordering; using `Prompt` for both would turn an `ExclusiveGroupConflict` assertion into `DuplicateKind` and weaken coverage.

When old product kinds disappear, host tests replace them with file-local `StringName` identities such as:

```csharp
private static readonly StringName ModalA = "modal_a";
private static readonly StringName ModalB = "modal_b";
```

This applies to fixture references in:

- `tests/ui/hosting/UIScreenStackModelTest.cs`
- `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- `tests/ui/hosting/UIScreenHostInputTest.cs`
- `tests/ui/hosting/UIScreenHostFocusTest.cs`
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- `tests/ui/hosting/UIScreenHostProcessModeTest.cs`

No shared test-kind registry is added.

## Concrete migration scope

### Save overwrite

Replace `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns the selected slot, text, overwrite closure, and call back into the existing save path. The prompt is a child of active Save/Load. Cancel closes only the prompt; primary attempts to close the prompt and then executes the captured save closure once.

The current overwrite scene uses `SiriusWarningButton` while Pause return-to-title already uses `SiriusDestructiveButton`. HPA-572 intentionally normalizes overwrite to `SiriusDestructiveButton`; this is an approved visual change, not an accidental regression.

If the overwrite save operation fails, the retained Save/Load parent opens a new `RecoverableError` Prompt after the destructive Prompt's synchronous close finishes. This prompt-to-prompt chain is new and gets an explicit integration regression.

Delete the dedicated scene/controller/tests after equivalent component + integration coverage exists.

### Pause return to title

Replace `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns `ReturnToMainMenu()` and teardown-safe navigation. The prompt is a child of Pause. Cancel restores Pause; primary attempts to close the prompt and requests navigation once.

The shared component adds the one-shot terminal latch that the current Pause confirmation lacks. `PauseReturnToTitle_PrimaryRequestsNavigationOnce` therefore protects a new duplicate-activation guarantee, not merely presentation parity.

Delete the dedicated scene/controller/tests after equivalent coverage exists.

### Main Menu messages

Replace `MainMenu.TryOpenMessage(...)`'s native `AcceptDialog` with `SiriusPrompt`.

Map current calls:

- no save files -> `Warning`;
- Settings/Load screen unavailable -> `RecoverableError`;
- Continue/manual load failure -> `RecoverableError`.

If Load is active, a failure stays under the Load handle. Root messages have no parent and restore their invoking root button when still valid.

Preserve Main Menu's current action availability contract: opening Prompt and `ClearHostedMessage(...)` both call `RefreshActionAvailability()`. `IsRootActionBlocked()` is kind-agnostic and already derives blocking from `UIScreenHost.ActiveEntries.Count`, so no new root state is introduced.

The current manual-load failure path closes Load and then uses `Callable.From(...).CallDeferred()` before opening a root message to avoid opening during host close. HPA-572 removes the preceding Load close, so there is no close/drain transaction to escape; the recoverable child Prompt is opened synchronously under the still-active Load handle. Do not preserve or reintroduce the old defer wrapper without a new demonstrated host-mutation need.

### Gameplay Save/Load errors

Replace `Game.ShowSaveError(...)` with a bool-returning `RecoverableError` child of active Save/Load. Move every current production caller so it no longer closes Save/Load first.

The method logs loudly and returns `false` if the expected parent is absent. It does not create a root or native fallback.

Delete `_activeErrorPopup`, its cleanup, and `HandleGameplayRootCancel`'s special error-popup branch only after configured-Cancel and paused-tree tests have moved to the hosted prompt path.

### Corrupted-save startup

Replace the native corrupted-save dialog with root `RecoverableError` chrome plus `BlockGameplayInput = true`. Remove manual `SetProcessInput(false)` and native Confirmed/Canceled plumbing.

If prompt presentation fails, `ReturnToMainMenu()` is the mandatory fallback.

## Deferred legacy call sites

Do not migrate:

- `DialogueDialog`
- `ShopDialog`
- `HealDialog`
- `PuzzleRiddleDialog`
- Reward presentation

HPA-569, HPA-570, HPA-571, and HPA-573 own those vertical slices. They may consume `SiriusPrompt` when a real prompt need appears during their migration.

## Layout and visual behavior

`SiriusPrompt` reuses `SiriusModalShell`; no Theme token, icon, metric, or shell API is added unless a focused RED test proves an existing shared contract defect.

Variant mapping:

- `DestructiveConfirmation` -> `Warning` shell severity, visible Cancel, `SiriusDestructiveButton` primary, Cancel initial focus;
- `Warning` -> `Warning` shell severity, one `SiriusPrimaryButton`, primary initial focus;
- `RecoverableError` -> `Error` shell severity, one `SiriusPrimaryButton`, primary initial focus.

Cancel uses `SiriusSecondaryButton`. `SiriusUiMetrics.MinimumTarget(...)` sizes actions. `SiriusModalShell` owns compact mode, safe modal width, body scrolling, and long-content fit.

The shared component must remain usable at the minimum verification viewport with representative long wrapped text and all visible actions reachable.

## Input, focus, and lifetime

### Child-first Cancel

The host remains the top-level Cancel authority. Every Prompt entry uses `Cancel = Consume` and intercepts Cancel into `SiriusPrompt.RequestCancel()`:

- destructive confirmation -> Cancel signal;
- warning/recoverable error -> Primary acknowledgement signal.

The input event is consumed at the Prompt and cannot fall through to Save/Load, Pause, or the gameplay root fallback.

### Focus

- destructive confirmation focuses Cancel first;
- warning/error focuses the only primary button;
- closing a child prompt restores its active parent through the host focus coordinator;
- a root Main Menu prompt restores its invoking root control when valid;
- programmatic close and parent teardown rely on host focus finalization rather than manual `GrabFocus()` calls.

### Double activation

`SiriusPrompt` emits at most one terminal signal per instance. Root navigation already has its existing one-way guards. Rapid button presses, Cancel followed by click, or a signal racing host cleanup must not invoke domain behavior twice.

### Parent teardown and programmatic close

The prompt does not own its host handle. `Game` / `MainMenu` own the handle and clear it from cleanup.

Closing a parent closes its prompt descendant with `ParentClosed`. Root `PrepareForTeardown()` closes prompts with the rest of the stack. Cleanup disconnects signals and clears root references/actions exactly once.

No retries, persistence, acknowledgement token, or cross-scene prompt state is introduced.

## Testing strategy

### Shared component

`tests/ui/components/SiriusPromptTest.cs` covers:

- all three variants map to expected severity, buttons, styles, and initial focus;
- destructive Primary/Cancel terminal paths latch exactly once;
- Warning and RecoverableError map `RequestCancel()` to Primary exactly once;
- repeated terminal activation is ignored;
- compact/standard transitions use existing minimum target sizing;
- representative long text at 640x360 remains inside the modal and scrollable/reachable as needed;
- scene uses `SiriusModalShell` and contains no `AcceptDialog`.

Do not add host policy construction to this component test; the component does not own host policy.

### Integration and lifecycle

Preserve or migrate these journeys:

1. Save/Load -> overwrite Prompt: Cancel closes only Prompt/restores Save/Load; primary saves once.
2. Save/Load -> overwrite Prompt -> failing save -> recoverable Prompt: the first Prompt fully closes, Save/Load remains active, and the second Prompt opens under Save/Load without `HostMutating`/duplicate-kind failure.
3. Pause -> return-to-title Prompt: Cancel restores Pause; rapid/repeated Primary produces one navigation request.
4. Pause -> Save/Load -> recoverable error: tree remains paused; Prompt remains dismissible because it is `Always`; configured Cancel closes only Prompt; Save/Load and Pause remain active.
5. Main Menu -> Load -> recoverable error: Load remains parent and resumes after acknowledgement.
6. Main Menu root warning/error: root stays beneath Prompt, invoking button focus restores, root actions refresh disabled/enabled state on Prompt open/close, and configured Cancel exercises the same Prompt latch.
7. Corrupted startup: root RecoverableError + gameplay-block lease; Primary or configured Cancel requests Main Menu once; `Game.IsProcessingInput()` remains true.
8. Corrupted startup presentation failure: no Prompt/lease is available, but fallback still requests Main Menu once instead of leaving the half-loaded Game interactive.
9. Programmatic close and parent/root teardown clear prompt handle, exclusive-group ownership, and input/focus leases.
10. Both root suites assert active Prompt policy uses `UICancelPolicy.Consume`; configured-Cancel journeys prove the interceptor remains wired. This is the drift guard instead of a shared `SpecFor(...)` factory.
11. Host unit tests that formerly used prompt product kinds still prove distinct-kind group conflicts, ordering, subwindow, focus, lifecycle, and process behavior with local fixture kinds.

Update only the HPA-376 lifecycle rows changed by these flows.

## Risks and mitigations

### Corrupted-save presentation failure can strand the player

**Risk:** `_hasShownCorruptedSaveError` is a one-way latch. If prompt presentation fails after the latch is set, retrying later is not guaranteed and the game is only partially initialized.

**Mitigation:** prompt-open failure logs and immediately falls back to `ReturnToMainMenu()`. A synthetic-hostless Game test pins the fallback.

### Terminal latch can suppress domain work if action is gated on close status

**Risk:** `SiriusPrompt` emits once, while `TryClose` can return statuses other than `Closed` during races/teardown.

**Mitigation:** root handlers capture the closure, attempt close, then invoke the closure unconditionally. Existing domain/scene-transition guards remain the duplicate-effect protection.

### Prompt-to-prompt transition is new

**Risk:** overwrite Primary can call save logic that fails and opens a recoverable Prompt. Opening too early would hit `HostMutating` or the single-Prompt kind guard.

**Mitigation:** root handlers call `TryClose(...)` synchronously before invoking the domain closure; `UIScreenHost` drains cleanup before `TryClose` returns. An integration test pins overwrite -> failed save -> recoverable Prompt under Save/Load.

### Root host-policy copies can drift

**Risk:** `Game` and `MainMenu` both hand-author the load-bearing `Cancel = Consume` + interceptor pairing.

**Mitigation:** keep root ownership rather than introducing a two-caller policy factory, and assert `Policy.Cancel == Consume` plus configured-Cancel behavior in both root integration suites.

### Parent-retention changes legacy sequencing

**Risk:** old error paths closed Save/Load before opening a native/root error, sometimes with a deferred callback.

**Mitigation:** tests assert parent retention, paused-tree dismissibility, no input fall-through, and synchronous child opening now that the preceding close is removed.

## Implementation shape

Four independently verifiable slices, with Task 3 split into two reviewable sub-commits:

1. Add the shared component under `ui/components` with three variants and focused responsive/terminal tests.
2. Replace Save overwrite and Pause return-to-title with the shared Prompt; delete their feature-specific implementations and explicitly characterize the new Pause latch guarantee.
3. **3A:** replace Main Menu warning/error presentation while retaining Load parents and root action availability; **3B:** replace gameplay Save/Load errors, preserve paused-tree/configured-Cancel behavior, add the overwrite-failure prompt-chain regression, and delete `_activeErrorPopup` ownership. Commit 3A and 3B separately.
4. Replace corrupted-save presentation with fallback navigation, remove stale product kinds, retarget host test fixtures to local kinds, reconcile lifecycle docs, and run full/stale-reference verification.

## Out of scope

- informational two-button confirmation until a real caller exists;
- toast/reward queues or HPA-573 reward presentation;
- global prompt service/presenter/queue/router/host facade or `SiriusPrompt.SpecFor(...)` factory;
- retry/recovery business logic inside the component;
- Dialogue/Shop/Healing/Puzzle-Riddle migration;
- new Theme tokens, icons, or `SiriusModalShell` / `UIScreenHost` APIs without a reproduced shared-component defect;
- cross-scene prompt persistence or acknowledgement protocol;
- compatibility shims for removed feature-specific prompt scenes/controllers/kinds.

## Acceptance criteria

- One shared `ui/components/SiriusPrompt` provides destructive confirmation, warning, and recoverable-error presentation using existing Sirius primitives.
- Save overwrite and Pause return-to-title use the shared destructive confirmation; their duplicate scenes/controllers are removed.
- Return-to-title gains one-shot terminal protection, and overwrite intentionally uses `SiriusDestructiveButton` instead of its former warning-button variation.
- Main Menu and gameplay migrated warnings/errors no longer construct native `AcceptDialog`s.
- Failed Save/Load keeps its parent active and returns to it after acknowledgement.
- Corrupted-save startup uses RecoverableError chrome plus a host gameplay-block lease; presentation failure still falls back to Main Menu and `Game` no longer manually disables input.
- Child-first Cancel and parent-focus restoration are deterministic, including while Pause keeps `SceneTree.Paused == true`.
- Prompt terminal actions are latched against double activation, while root domain closures are not lost solely because Prompt close reports a non-`Closed` status.
- Overwrite -> failing save can transition to a recoverable Prompt under the retained Save/Load parent.
- Programmatic close and parent/root teardown leave no Prompt host entry, exclusive-group ownership, or input lock.
- Host fixture tests preserve distinct-kind coverage without depending on deleted product kinds.
- Representative long text remains usable at the minimum viewport.
- Legacy Dialogue/Shop/Healing/Puzzle/Riddle presentation is unchanged under this ticket.
