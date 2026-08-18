# HPA-571 Hosted Puzzle and Riddle Design

**Issue:** HPA-571  
**Status:** Proposed  
**Date:** 2026-08-17

## Context

HPA-571 is the next actionable child of the HPA-358 secondary-presentation workstream. HPA-569 (Dialogue), HPA-570 (Shop and Healing), and HPA-572 (shared prompts) are complete; HPA-573 Reward Feedback follows Puzzle/Riddle.

The remaining riddle path is still a legacy desktop-style exception:

- `PuzzleRiddleDialog : AcceptDialog` builds its labels and answer buttons in C#.
- `Game.OpenPuzzleRiddle(...)` creates that native dialog directly under the gameplay `UI` canvas.
- `Game.CleanupPuzzleRiddleDialog(...)` owns riddle-node cleanup and the `IsInWorldInteraction` latch.
- `PuzzleTrapController` already owns switch arming, answer validation, solved persistence, and result messages.
- `Game` already owns wrong-answer damage, gate/grid solved-state application, and exploration prompt restoration.
- `UIScreenKinds.PuzzleRiddle` already exists.

HPA-373 §9.11 defines Puzzle as a medium Sirius panel with title, prompt, choices/input, validation feedback, a visible Cancel control, **and the active device's cancel hint**. HPA-376 freezes current answer/cancel mutual exclusion and world-interaction cleanup behavior.

## Goals

- Replace `PuzzleRiddleDialog` with one scene-authored Sirius riddle surface.
- Present it through the existing gameplay `UIScreenHost` as `UIScreenKinds.PuzzleRiddle`.
- Keep `Game` as the world-riddle orchestration owner.
- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch arming, answer validation, wrong-answer damage, solved persistence, and gate logic unchanged.
- Make dormant/unarmed, wrong-answer, and success feedback readable in the riddle surface.
- Preserve the current wrong-answer retry boundary: a wrong answer ends the current attempt; a retry starts from a fresh interaction after dismissing the result.
- Preserve the proven mutual-exclusion rule: once an answer begins resolving, Cancel and further answers cannot race it.
- Keep long prompts and choices usable at 640×360 through the existing shell body scroll.
- Give keyboard/gamepad/mouse users deterministic focus, a visible Cancel action, and the HPA-373-required active-device cancel hint.
- Converge cancellation, answer completion, invalid presentation data, host rejection, stale handles, publication exceptions, node teardown, and scene teardown on idempotent world-interaction cleanup.

## Non-goals

- New puzzle types, free-text answers, timed puzzles, hints, multi-stage puzzle state, or puzzle rewards.
- Changes to `PuzzleTrapController`, `PuzzleRiddleSpawn`, puzzle persistence, trap damage rules, switch requirements, or gate logic.
- A generic puzzle presenter, state-machine framework, screen base class, navigation service, host facade, or new `UIScreenHost` API.
- A reusable choice-row component.
- Reward queues/toasts from HPA-573 or new prompt/error primitives.
- New theme tokens, icon art, save schema, or compatibility shims.
- A broad modal-wide input-hint redesign. HPA-571 satisfies the Puzzle-specific HPA-373 requirement locally with the existing `SiriusInputHint`.

## Existing behavior contract

The migration preserves these domain and lifecycle facts:

1. A blank `PuzzleId` or already-solved riddle does not start a new world interaction.
2. `PuzzleTrapController.TrySolveRiddle(...)` remains the authority for invalid, solved, dormant, wrong-answer, and successful results.
3. A wrong answer applies `WrongAnswerDamage` through `Game.ApplyPuzzleDamage(...)`, which floors health at 1 HP, then notifies player-stat changes.
4. A successful answer applies the existing gate/grid solved state and notifies player-stat changes.
5. Dormant/unarmed feedback does not end the world interaction; the same surface rearms the same answer set.
6. A wrong answer is terminal for the current answer attempt. HPA-571 adds a readable acknowledgement before cleanup, but a second answer still requires a fresh interaction.
7. Current `PuzzleRiddleDialog` uses one terminal latch so answer and Cancel cannot both emit. HPA-571 must preserve that mutual exclusion while allowing a post-resolution terminal feedback phase.
8. `IsInWorldInteraction` blocks competing gameplay until the riddle presentation actually ends.

## Architecture

Add one concrete scene/controller pair:

- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`

`Game` remains the orchestration owner. It loads/configures the screen, starts the world-interaction latch, presents through `UIScreenHost`, resolves choices through `PuzzleTrapController`, applies existing Game-owned damage/gate effects, and closes the hosted entry.

`PuzzleRiddleScreenController` is presentation-only. It binds one existing `PuzzleRiddleSpawn`, renders choices, owns presentation phase/focus, and emits choice/close events. It never calls `PuzzleTrapController`, mutates `Character`, marks puzzles solved, changes the grid, or writes save state.

Do not route world riddles through `NpcInteractionController`. NPC presentation uses a different domain latch (`IsInNpcInteraction`) and its private `TryHostSurface(...)` is coupled to `Finish()`. For one Game-owned world surface, inline only the Game-shaped host-open subset: log-and-return, post-open `IsActive` recheck, and idempotent `EndWorldInteraction` cleanup.

## Scene composition

`PuzzleRiddleScreen.tscn` is a full-viewport `Control` with one centred `SiriusModalShell` directly under the root.

Use `SiriusModalSizeClass.Medium` (640 px). Do **not** add `%SafeFrame`: the centred non-Full shell already owns standard width capping, compact margins, body height, scrolling, and `FollowFocus`.

Stable authored nodes:

```text
PuzzleRiddleScreen (full-viewport Control)
└── ModalShell (%ModalShell, SizeClass = Medium)
    ├── .../BodyHost
    │   ├── FeedbackLabel (%FeedbackLabel)
    │   ├── PromptLabel (%PromptLabel)
    │   └── ChoicesContainer (%ChoicesContainer)
    └── .../ActionsHost
        ├── CancelHint (%CancelHint)
        └── CancelButton (%CancelButton)
```

`%CancelHint` stays in the fixed shell action chrome, **not** in `BodyHost`, so long prompt/choice scrolling cannot move the hint away from the always-visible Cancel button. This is intentionally Puzzle-specific because HPA-373 §9.11 explicitly requires the active device's cancel hint; HPA-571 does not generalize that policy to other modals.

Presentation details:

- `%ModalShell.Title` = nonblank `RiddleId`, otherwise `Seal`.
- `%PromptLabel` is a BBCode-enabled `RichTextLabel`, preserving current prompt rendering capability.
- `%FeedbackLabel` starts `visible = false`, uses `SiriusMetadata`, autowraps, and follows the same standing-feedback defaults as Healing. Puzzle feedback is not timed.
- `%ChoicesContainer` receives runtime-created answer buttons.
- `%CancelButton` starts as `Cancel`; terminal wrong/success presentation changes it to `Close` / `Continue`.
- `%CancelHint` uses existing `SiriusInputHint` with the configured Cancel action and remains fixed with the action bar.
- `SiriusModalShell.BodyScroll` remains the only scroll owner.

## Runtime answer buttons

Use the same local runtime-button pattern already proven by Dialogue; do not introduce a shared choice-row component.

```csharp
private static Button CreateActionButton(string text) => new()
{
    Text = text,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
```

`RefreshLayout()` reapplies the existing responsive minimum target to every answer button:

```csharp
var minimumTarget = SiriusUiMetrics.MinimumTarget(_shell.Compact);
foreach (var child in _choicesContainer.GetChildren())
{
    if (child is Button action)
        action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
}
```

That yields 44 px standard / 40 px compact targets, matching the existing Sirius UI metric instead of hard-coding 44 px for every viewport.

## Controller contract

```csharp
public partial class PuzzleRiddleScreenController : Control
{
    [Signal] public delegate void ChoiceSelectedEventHandler(string choiceId);
    [Signal] public delegate void PuzzleRiddleClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenRiddle(PuzzleRiddleSpawn riddle);
    public void RearmWithFeedback(string message);
    public void ShowTerminalFeedback(string message, string actionLabel);
    public void RequestCancel();
}
```

One controller instance represents one world interaction. `TryOpenRiddle(...)` may run before `_Ready()`; a second configuration is rejected.

## Presentation phase and answer/cancel mutual exclusion

The old native dialog has one terminal latch because an answer and Cancel must be mutually exclusive. HPA-571 needs a post-answer terminal acknowledgement, so a single terminal boolean is no longer sufficient; two independent booleans are also unsafe because Cancel can race answer resolution.

Use one small local phase enum plus the final close latch:

```csharp
private enum PuzzleRiddlePresentationPhase
{
    AwaitingChoice,
    Resolving,
    Terminal
}

private PuzzleRiddlePresentationPhase _phase;
private bool _closedEmitted;
```

Legal transitions:

```text
AwaitingChoice --choice--> Resolving
Resolving --dormant--> AwaitingChoice
Resolving --wrong/success--> Terminal
AwaitingChoice --Cancel--> closed
Terminal --Cancel/Close/Continue--> closed
Resolving --Cancel/choice--> ignored
```

This preserves the existing `ChoiceThenCancel_EmitsChoiceSelectedOnly` contract while allowing terminal feedback to remain visible after resolution.

### AwaitingChoice

- Choices are visible/enabled.
- First focusable choice is `InitialFocusTarget`, falling back to Cancel.
- Pressing one answer changes phase to `Resolving` **before** emitting `ChoiceSelected`.
- Further choice activation is ignored until Game returns a resolution.
- Cancel emits close once.

### Resolving

- Choices are disabled while Game synchronously resolves the answer.
- `RequestCancel()` is ignored.
- `RearmWithFeedback(...)` and `ShowTerminalFeedback(...)` are the only legal exits.

This prevents a configured Cancel event from closing/clearing `_activePuzzleRiddle` in the middle of `Game.OnPuzzleRiddleChoiceSelected(...)`.

### Dormant/unarmed

Game receives `PuzzleRiddleResult(false, false, message)` and calls:

```csharp
screen.RearmWithFeedback(result.Message);
```

The controller shows standing feedback, returns to `AwaitingChoice`, reenables answers, and restores focus to a valid answer (or Cancel). No world cleanup occurs.

### Wrong answer

Game captures health before applying the existing penalty, applies `ApplyPuzzleDamage(...)`, then computes **actual HP lost** from the post-damage value because health is floored at 1.

Example:

```csharp
var healthBefore = _gameManager.Player.CurrentHealth;
ApplyPuzzleDamage(riddle.WrongAnswerDamage);
var healthLost = healthBefore - _gameManager.Player.CurrentHealth;
```

Then Game shows terminal feedback using the actual loss, not the configured maximum:

```csharp
screen.ShowTerminalFeedback(
    healthLost > 0
        ? $"{result.Message} (-{healthLost} HP)"
        : result.Message,
    "Close");
```

The controller transitions `Resolving -> Terminal`, hides/disables answers, and focuses `Close`. Dismissal ends the world interaction; retry requires a fresh interaction.

### Success

Game applies the existing solved-state/gate/grid update first, then calls:

```csharp
screen.ShowTerminalFeedback(result.Message, "Continue");
```

The controller transitions `Resolving -> Terminal`, hides/disables answers, and focuses `Continue`. Solved state is already committed once; dismissal only ends presentation/world interaction.

### Final close

`RequestCancel()` behavior is phase-aware:

- `AwaitingChoice`: emit `PuzzleRiddleClosed` once.
- `Resolving`: ignore.
- `Terminal`: emit `PuzzleRiddleClosed` once.

`_closedEmitted` protects the final signal from repeated button/host Cancel delivery.

## Invalid presentation data

Keep validation minimal:

- `Game` retains blank-`PuzzleId` and already-solved guards.
- `TryOpenRiddle(...)` rejects null or `GetChoices().Count == 0`, because a screen with no answer path would soft-lock.
- Do not duplicate `CorrectChoiceId` validation; `PuzzleRiddleSpawn._GetConfigurationWarnings()` already covers authoring mistakes and `PuzzleTrapController` remains runtime authority.
- Scene load/instantiation failure, controller rejection, host rejection, stale post-open handle, and publication exception fail closed: log, discard/close the candidate, and clear `IsInWorldInteraction` if it was started.

## Responsive layout

Follow the centred-shell pattern:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady() || _shell == null || !IsInsideTree())
        return;

    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _cancelHint.Compact = _shell.Compact;
    _shell.RefreshPresentation(size);

    var minimumTarget = SiriusUiMetrics.MinimumTarget(_shell.Compact);
    foreach (var child in _choicesContainer.GetChildren())
    {
        if (child is Button action)
            action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
    }
}
```

Subscribe `Resized` in `_Ready()` and unsubscribe in `_ExitTree()`.

At 640×360 the Medium shell uses compact margins and body height. Long prompt/choices scroll inside the shell while the action bar (`CancelHint` + `CancelButton`) stays fixed.

## Host integration

Use one explicit `UIScreenEntrySpec`:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.PuzzleRiddle,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Consume,
    InitialFocus = () => screen.InitialFocusTarget,
    InterceptCancel = _ =>
    {
        screen.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    Cleanup = _ => ClearPuzzleRiddlePresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
}
```

No parent handle or new exclusive group is needed. `IsInWorldInteraction` already prevents competing world interactions.

### Keep host hardening local to Game

Do not extract `Game.TryHostSurface`. Inline the one riddle-specific host open:

1. Require a live `_screenHost` before starting the world interaction.
2. Load/instantiate/configure the screen and subscribe signals.
3. Set `_activePuzzleRiddle`, call `StartWorldInteraction()`, refresh the exploration prompt.
4. Call `TryPresent(...)` inside `try/catch`.
5. On rejected/no-handle result, unsubscribe/free the candidate and converge on world cleanup.
6. On publication exception, converge on cleanup and log; this Game path does not need NpcInteractionController's `Finish()`/rethrow semantics.
7. After `Opened`, recheck `_screenHost.IsActive(handle)` before retaining screen/handle because a publication subscriber can synchronously close the entry.
8. Retain `_puzzleRiddleScreen` / `_puzzleRiddleHandle` only for an active handle.

## Cleanup convergence

Replace `CleanupPuzzleRiddleDialog(...)` in the same Game cutover that replaces the old field.

`ClearPuzzleRiddlePresentation(screen)`:

- unsubscribes `ChoiceSelected` / `PuzzleRiddleClosed`;
- clears `_puzzleRiddleScreen` / `_puzzleRiddleHandle` when they refer to that screen;
- clears `_activePuzzleRiddle`;
- calls `EndWorldInteraction()` only when the flag is still active;
- refreshes the exploration prompt only while Game is still inside the tree.

`ClosePuzzleRiddlePresentation(reason)` uses host `TryClose(...)`; stale handle falls back through the same local idempotent clear.

Terminal handlers catch/log close-publication exceptions after cleanup and must not leave world interaction latched.

`Game._ExitTree()` closes the active hosted riddle with `HostTeardown` when possible, then falls back to idempotent local cleanup.

Once hosted, remove the `_puzzleRiddleDialog` special case in `HandleGameplayRootCancel()`. Host Cancel owns the active riddle. The existing bare `IsInWorldInteraction` fallback remains `Consumed` so a failure window cannot open Pause on top of a world interaction.

## Compile-complete migration boundary

The `_puzzleRiddleDialog` field is referenced by both production and tests. The field/handle cutover must therefore be one compile-complete change, not split across tasks.

When `Game` replaces `_puzzleRiddleDialog` with `_puzzleRiddleScreen` + `_puzzleRiddleHandle`, the same task retargets **all** old private-field/type lookups in:

- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`

and replaces all `CleanupPuzzleRiddleDialog(...)` calls in `Game` with hosted close/clear logic.

The initial Game-host cutover preserves the current external success/wrong lifecycle so its focused tests can go green before the new acknowledgement behavior is enabled. It may resolve the answer, call `ShowTerminalFeedback(...)`, and immediately `RequestCancel()` for solved/wrong results. The following presentation task removes that immediate final close and retargets assertions to `Terminal -> dismiss`.

This avoids a compile-broken intermediate commit and avoids rewriting the same field references twice.

## Legacy cleanup

After hosted behavior and lifecycle tests are green:

- delete `scripts/ui/PuzzleRiddleDialog.cs`;
- delete `tests/ui/PuzzleRiddleDialogTest.cs`;
- require zero active-source `_puzzleRiddleDialog` / `PuzzleRiddleDialog` references under `scripts`, `scenes`, and `tests`;
- update HPA-376 `WORLD-RIDDLE` / `WORLD-CLEANUP` rows for the hosted route.

No compatibility wrapper remains.

## Testing strategy

### `PuzzleRiddleScreenControllerTest`

Cover:

- recursive `ContainsAcceptDialog(screen) == false`;
- no `%SafeFrame`, and `%ModalShell.GetParent() == screen`;
- Medium shell;
- `%FeedbackLabel` starts hidden and uses `SiriusMetadata`;
- pre-ready configuration and second-start rejection;
- `RiddleId` fallback to `Seal`;
- runtime answer text/ids;
- runtime answer buttons use `SiriusSecondaryButton`, `WordSmart`, expand-fill, and `SiriusUiMetrics.MinimumTarget(...)` height;
- first answer initial focus;
- no-choice rejection;
- one answer changes `AwaitingChoice -> Resolving` and emits once;
- **answer then Cancel while Resolving emits only ChoiceSelected and no close** (replacement for `PuzzleRiddleDialogTest.ChoiceThenCancel_EmitsChoiceSelectedOnly`);
- repeated answers while Resolving are ignored;
- dormant feedback returns to `AwaitingChoice`, restores focus, and permits one fresh answer;
- terminal feedback moves to `Terminal`, hides/disables answers, and focuses Close/Continue;
- terminal Cancel/action closes once;
- configured Cancel hint exists in fixed action chrome and reflects compact mode;
- 640×360 long content remains reachable via shell body scroll with 40 px answer targets.

Use the same recursive helper pattern already present in Healing/Shop tests:

```csharp
private static bool ContainsAcceptDialog(Node node)
{
    if (node is AcceptDialog)
        return true;

    foreach (Node child in node.GetChildren())
    {
        if (ContainsAcceptDialog(child))
            return true;
    }

    return false;
}
```

Do not use `FindChild("*") is AcceptDialog`; that only type-checks one found descendant.

### `GameTest`

Retarget real fixtures, not mocks:

- switch -> correct riddle still marks solved, opens gate, disables trap, and notifies once;
- wrong answer still applies actual domain damage and floors no lower than 1 HP;
- dormant answer keeps the same hosted entry/world interaction active;
- after the presentation retarget, success/wrong keep the hosted entry/world interaction active until Continue/Close is dismissed;
- wrong feedback prints **actual HP lost**;
- after wrong-result dismissal, a fresh interaction can open the riddle again;
- Cancel clears the hosted entry/world interaction once;
- invalid/no-choice open never leaves world interaction latched.

### `GameInputLifecycleTest`

Retarget all old private-field/type lookups in the Game host cutover task. Preserve/extend:

- configured keyboard Cancel closes an `AwaitingChoice` hosted riddle and never opens Pause;
- controller Cancel does the same;
- answer then configured Cancel during `Resolving` does not close the riddle or clear world interaction before Game resolves it;
- terminal Close/Continue ends only the riddle and restores gameplay/prompt;
- bare `IsInWorldInteraction` with no active hosted entry remains consumed by the root fallback.

## Review-driven corrections

Validated against current `main`:

- **Accepted:** replace `_choicePending + _closedEmitted` resolution semantics with explicit `AwaitingChoice / Resolving / Terminal` phase plus one final-close latch.
- **Accepted:** make the Game field/handle cutover compile-complete across `Game.cs`, `GameTest.cs`, and `GameInputLifecycleTest.cs`; do not defer old cleanup calls or field lookups.
- **Accepted:** copy Dialogue's local runtime answer-button helper and apply `SiriusUiMetrics.MinimumTarget(...)` at layout refresh.
- **Not accepted as proposed:** removing `%CancelHint`. HPA-373 §9.11 explicitly requires Puzzle's active-device cancel hint. Keep the existing component, but move it from scrollable body content to fixed `ActionsHost` chrome beside Cancel.
- **Accepted:** use recursive `ContainsAcceptDialog`, plus no-SafeFrame/direct-shell-parent assertions.
- **Accepted:** report actual HP lost after the 1-HP floor and use Healing-style hidden `SiriusMetadata` defaults for `%FeedbackLabel`.

## Completion criteria

- Puzzle/riddle no longer uses desktop-window framing.
- Existing puzzle rules, damage, gates, persistence, and retry boundary are unchanged.
- Choice/cancel mutual exclusion remains protected through the new resolving phase.
- Long content remains usable at 640×360.
- Keyboard/controller Cancel is host-owned and does not open Pause.
- Every exit/failure/teardown path clears world interaction exactly once.
- Native `PuzzleRiddleDialog` and its tests are deleted with zero active references.
- Existing puzzle-domain tests remain green.
