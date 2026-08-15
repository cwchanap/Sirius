# HPA-572: Host-managed confirmations, warnings, and errors

## Goal

Standardize Sirius confirmations, warnings, and errors on one scene-authored modal prompt component presented through the invoking root's existing `UIScreenHost`.

This ticket replaces the two duplicate feature-specific confirmation scenes and the remaining native `AcceptDialog` error paths that already belong to migrated Main Menu / gameplay flows. It does not pre-migrate Dialogue, Shop, Healing, Puzzle/Riddle, Reward, or other screens that have their own vertical tickets.

## Current state

Sirius already has the infrastructure HPA-572 needs:

- `SiriusModalShell` owns modal chrome, severity icon/panel styling, responsive width, bounded body height, and scrolling.
- `UIScreenHost` owns logical parent/child ordering, topmost input, Cancel routing, lower-layer inertness, focus restoration, process policy, node lifetime, and teardown.
- `SiriusUiSeverity` already provides the `Info`, `Warning`, and `Error` visual semantics needed by the current callers.
- `UIScreenExclusiveGroups.BlockingPrompt` already prevents unrelated blocking prompts from stacking.
- `Game` and `MainMenu` already own local `UIScreenHost` instances and feature-local `TryPresent` helpers; no third hosting abstraction is required.

The remaining prompt implementations are fragmented:

- `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` provide one destructive confirmation under Pause.
- `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` provide another destructive confirmation under Save/Load.
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

Do not add callbacks, async tasks, host handles, navigation logic, recovery delegates, or domain payloads to `SiriusPrompt`.

### 4. Root-local hosting remains the integration model

Do not add `PromptService`, `PromptPresenter`, a singleton, a notification queue, or a generic host facade.

`Game` and `MainMenu` each keep one private prompt-opening helper shaped around their existing local host state. Some duplicated plumbing between two roots is intentional; a third real root can justify extraction later.

Common host policy:

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

`Cancel = Consume` + `InterceptCancel -> RequestCancel()` is load-bearing. `Cancel = Close` would bypass the prompt latch and cannot implement mandatory Cancel-as-primary behavior for the corrupted-save path.

Terminal root handlers capture the domain closure before `TryClose(...)`, because host cleanup clears root-owned prompt callbacks synchronously. They close the prompt first, then invoke the captured domain closure.

### 5. Parent retention is the normal recoverable-error path

A child prompt receives the active logical parent handle. The parent remains visible and active but inert; `UIScreenHost` restores parent focus when the prompt closes.

In particular, failed Save/Load no longer closes Save/Load before presenting its error. The flow becomes:

```text
Pause -> Save/Load -> RecoverableError prompt
```

or, from Main Menu:

```text
Main Menu -> Load -> RecoverableError prompt
```

Acknowledgement closes only `Prompt`; `SaveLoad` remains active.

This also preserves the important paused-tree behavior: gameplay Pause can keep `SceneTree.Paused == true`, while Save/Load and its prompt both use `UIProcessPolicy.Always`, so the prompt remains dismissible without its own native `ProcessMode.Always` workaround.

`Game.ShowSaveError(...)` is only valid while hosted Save/Load is active after this migration. If that parent is unexpectedly missing, it must `GD.PushError(...)` and return without silently pretending presentation succeeded. No unhosted/native fallback is introduced.

### 6. Corrupted-save startup reuses recoverable-error chrome

`Game.ShowCorruptedSaveError()` presents a root `RecoverableError` prompt with:

- no logical parent;
- `BlockGameplayInput = true`;
- primary text `Return to Title`;
- `onPrimary: ReturnToMainMenu`.

Keep existing domain semantics:

- show once;
- abort ordinary initialization;
- require a terminal acknowledgement;
- configured Cancel performs the same primary action as the visible button because `RecoverableError.RequestCancel()` emits Primary;
- return to Main Menu exactly once through existing teardown-safe scene navigation.

The host's gameplay-block lease replaces `SetProcessInput(false)`. `Game` stays input-processing so `UIScreenHost` can remain the Cancel authority.

### 7. One product prompt kind; host tests use fixture-local kinds

Add `UIScreenKinds.Prompt` and remove the product-specific prompt kinds once production is migrated:

- `ConfirmOverwrite`
- `ConfirmQuitToMain`
- `SaveError`
- `CorruptSaveError`

Do not retarget every host unit-test fixture to `Prompt`. Several host tests deliberately require **two distinct kinds** to prove group conflicts or ordering; using `Prompt` for both would turn an `ExclusiveGroupConflict` assertion into `DuplicateKind` and weaken coverage.

When old product kinds disappear, host tests replace them with test-local `StringName` identities such as:

```csharp
private static readonly StringName ModalA = new("modal_a");
private static readonly StringName ModalB = new("modal_b");
```

The local kinds preserve each test's intended host behavior without keeping dead product identities in `UIScreenKinds`.

This applies to current fixture references in:

- `tests/ui/hosting/UIScreenStackModelTest.cs`
- `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- `tests/ui/hosting/UIScreenHostInputTest.cs`
- `tests/ui/hosting/UIScreenHostFocusTest.cs`
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- `tests/ui/hosting/UIScreenHostProcessModeTest.cs`

No shared test-kind registry is added; local fixture identities are cheaper and keep product/test vocabulary separate.

## Concrete migration scope

### Save overwrite

Replace `SaveOverwriteConfirmation.tscn` / `SaveOverwriteConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns the selected slot, text, overwrite closure, and call back into the existing save path. The prompt is a child of active Save/Load. Cancel closes only the prompt; primary closes the prompt and then executes the captured save closure once.

Delete the dedicated scene/controller/tests after equivalent component + integration coverage exists.

### Pause return to title

Replace `PauseReturnToTitleConfirmation.tscn` / `PauseReturnToTitleConfirmationController.cs` with `SiriusPromptVariant.DestructiveConfirmation`.

`Game` still owns `ReturnToMainMenu()` and teardown-safe navigation. The prompt is a child of Pause. Cancel restores Pause; primary closes the prompt and requests navigation once.

Delete the dedicated scene/controller/tests after equivalent coverage exists.

### Main Menu messages

Replace `MainMenu.TryOpenMessage(...)`'s native `AcceptDialog` with `SiriusPrompt`.

Map current calls:

- no save files -> `Warning`;
- Settings/Load screen unavailable -> `RecoverableError`;
- Continue/manual load failure -> `RecoverableError`.

If Load is active, a failure stays under the Load handle. Root messages have no parent and restore their invoking root button when still valid.

### Gameplay Save/Load errors

Replace `Game.ShowSaveError(...)` with a `RecoverableError` child of active Save/Load. Move every current production caller so it no longer closes Save/Load first.

The method logs loudly if the expected parent is absent. It does not create a root or native fallback.

Delete `_activeErrorPopup`, its cleanup, and `HandleGameplayRootCancel`'s special error-popup branch only after the configured-Cancel tests have moved to the hosted prompt path.

### Corrupted-save startup

Replace the native corrupted-save dialog with root `RecoverableError` chrome plus `BlockGameplayInput = true`. Remove manual `SetProcessInput(false)` and native Confirmed/Canceled plumbing.

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

The host remains the top-level Cancel authority. Every prompt entry uses `Cancel = Consume` and intercepts Cancel into `SiriusPrompt.RequestCancel()`:

- destructive confirmation -> Cancel signal;
- warning/recoverable error -> Primary acknowledgement signal.

The input event is consumed at the prompt and cannot fall through to Save/Load, Pause, or the gameplay root fallback.

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

Do not add trivial property tests.

### Integration and lifecycle

Preserve or migrate these journeys:

1. Save/Load -> overwrite prompt: Cancel closes only prompt/restores Save/Load; primary saves once.
2. Pause -> return-to-title prompt: Cancel restores Pause; primary requests navigation once.
3. Pause -> Save/Load -> recoverable error: tree remains paused; prompt remains dismissible because it is `Always`; configured Cancel closes only Prompt; Save/Load and Pause remain active.
4. Main Menu -> Load -> recoverable error: Load remains parent and resumes after acknowledgement.
5. Main Menu root warning/error: root stays beneath prompt and invoking button focus restores.
6. Corrupted startup: root RecoverableError + gameplay-block lease; Primary or configured Cancel requests Main Menu once; `Game.IsProcessingInput()` remains true.
7. Programmatic close and parent/root teardown clear prompt handle, exclusive-group ownership, and input/focus leases.
8. Host unit tests that formerly used prompt product kinds still prove distinct-kind group conflicts, ordering, subwindow, focus, lifecycle, and process behavior with local fixture kinds.

Update only the HPA-376 lifecycle rows changed by these flows.

## Implementation shape

Four independently verifiable slices:

1. Add the shared component under `ui/components` with three variants and focused responsive/terminal tests.
2. Replace Save overwrite and Pause return-to-title with the shared prompt; delete their feature-specific implementations.
3. Replace Main Menu/gameplay recoverable errors, keep Save/Load parents active, migrate the paused-tree/configured-Cancel tests, and delete `_activeErrorPopup` ownership.
4. Replace corrupted-save presentation, remove stale product kinds, retarget host test fixtures to local kinds, reconcile lifecycle docs, and run full/stale-reference verification.

## Out of scope

- informational two-button confirmation until a real caller exists;
- toast/reward queues or HPA-573 reward presentation;
- global prompt/notification service, presenter, queue, router, or host facade;
- retry/recovery business logic inside the prompt;
- Dialogue/Shop/Healing/Puzzle-Riddle migration;
- new Theme tokens/icons/metrics or new `SiriusModalShell` / `UIScreenHost` APIs without a reproduced shared defect;
- cross-scene prompt persistence or acknowledgement protocol;
- compatibility shims for deleted scenes/controllers/kinds.

## Acceptance criteria

- One scene-authored component under `scenes/ui/components` provides destructive confirmation, warning, and recoverable-error presentation using existing Sirius primitives.
- Save overwrite and Pause return-to-title use that component and their duplicate scenes/controllers are removed.
- Main Menu and gameplay migrated warning/error paths no longer construct native `AcceptDialog`s.
- Failed Save/Load stays active beneath its recoverable child prompt; missing expected gameplay parent logs an error rather than silently swallowing presentation.
- Corrupted-save startup reuses recoverable-error chrome with a host-owned gameplay-block lease and no manual `Game` input disable.
- Cancel is child-first and uses the same terminal latch as visible actions, including while Pause keeps the tree paused.
- Prompt terminal actions are latched against double activation.
- Programmatic close, parent close, and root teardown leave no prompt host entry, exclusive-group ownership, input lock, or stale root reference.
- Host tests preserve their original distinct-kind semantics with local fixture kinds after old product kinds are deleted.
- Representative long text remains usable at the minimum viewport.
- Legacy Dialogue/Shop/Healing/Puzzle-Riddle/Reward presentation remains unchanged.