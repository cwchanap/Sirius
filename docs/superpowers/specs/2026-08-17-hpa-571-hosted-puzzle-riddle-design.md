# HPA-571 Hosted Puzzle and Riddle Design

**Issue:** HPA-571  
**Status:** Proposed  
**Date:** 2026-08-17

## Context

HPA-571 is the next actionable child of the HPA-358 secondary-presentation workstream. HPA-569 (Dialogue), HPA-570 (Shop and Healing), and HPA-572 (shared confirmations/errors) are already complete, while HPA-573 Reward Feedback remains the next slice after Puzzle/Riddle.

The current riddle path is still the legacy desktop-style exception:

- `PuzzleRiddleDialog` derives from `AcceptDialog` and builds its labels and choice buttons in C# at runtime.
- `Game` creates that native dialog directly under the gameplay `UI` canvas rather than presenting it through `UIScreenHost`.
- `PuzzleTrapController` already owns switch arming, answer validation, solved persistence, and result messages.
- `Game` already owns wrong-answer damage, solved gate application, world-interaction state, and interaction-prompt restoration.
- `UIScreenKinds.PuzzleRiddle` already exists, so the migration needs no new host kind.

HPA-373 defines Puzzle as a medium Sirius modal with title, prompt, answers, validation feedback, a visible Cancel action, deterministic focus, and compact scrolling. HPA-376 records the existing world-interaction cleanup contract that this migration must preserve.

## Goals

- Replace the native `PuzzleRiddleDialog` with one scene-authored Sirius riddle surface.
- Present it through the existing gameplay `UIScreenHost` as `UIScreenKinds.PuzzleRiddle`.
- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch arming, answer validation, wrong-answer damage, solved persistence, and gate updates unchanged.
- Make dormant/unarmed, wrong-answer, and success feedback readable inside the riddle surface.
- Preserve the existing wrong-answer retry boundary: one answer attempt ends, then the player may start a fresh world interaction to retry.
- Keep long prompts and long/many choices usable at 640×360 through the existing shell body scroll.
- Give mouse, keyboard, and gamepad users deterministic initial focus and a visible Cancel affordance.
- Converge cancellation, answer completion, invalid presentation data, host rejection, publication exceptions, node teardown, and scene teardown on exactly-once world-interaction cleanup.

## Non-goals

- New puzzle types, free-text answers, timed puzzles, hints, multi-stage puzzle state, or puzzle rewards.
- Changes to `PuzzleTrapController`, `PuzzleRiddleSpawn`, puzzle persistence, trap damage rules, switch requirements, or gate logic.
- A generic puzzle presenter, puzzle state machine framework, screen base class, navigation service, host facade, or new `UIScreenHost` API.
- A reusable choice-row component; the riddle is the only current consumer.
- Reward queues/toasts from HPA-573 or prompt/error primitives from HPA-572.
- New theme tokens, icon art, save schema, or backward-compatibility shims.

## Existing behavior contract

The migration preserves these domain/lifecycle facts rather than reinterpreting them:

1. A blank `PuzzleId` or an already-solved riddle does not start a new puzzle interaction.
2. `PuzzleTrapController.TrySolveRiddle(...)` remains the single authority for:
   - invalid puzzle result;
   - already-solved result;
   - dormant/unarmed result;
   - wrong-answer result;
   - successful solve and `GameManager.MarkPuzzleSolved(...)`.
3. A wrong answer applies `WrongAnswerDamage` through `Game.ApplyPuzzleDamage(...)`, which floors the player at 1 HP, then notifies player-stat changes.
4. A successful answer applies the existing gate/grid solved-state update and notifies player-stat changes.
5. Dormant/unarmed feedback does not end the riddle attempt; the same screen can be rearmed with the controller result message.
6. A wrong answer remains terminal for the current answer attempt. The player can retry only after dismissing the result and starting another world interaction, matching the current retry boundary.
7. World interaction blocks competing gameplay while the riddle presentation is active and returns to gameplay exactly once when the presentation ends.

## Architecture

Add one concrete scene/controller pair:

- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`

`Game` remains the orchestration owner. It loads the scene, starts the existing world-interaction latch, presents the screen through `UIScreenHost`, invokes `PuzzleTrapController` when a choice is selected, applies existing Game-owned damage/gate effects, and closes/cleans up the hosted entry.

`PuzzleRiddleScreenController` is presentation-only. It binds an already-existing `PuzzleRiddleSpawn`, renders choices, exposes the current focus target, and emits presentation events. It never calls `PuzzleTrapController`, mutates `Character`, marks puzzles solved, changes the grid, or writes save state.

Do not move the riddle path into `NpcInteractionController`; current riddle interactions are world interactions owned directly by `Game`, and adding a second orchestration object would only obscure that existing boundary.

## Scene and visual composition

`PuzzleRiddleScreen.tscn` is a full-viewport `Control` with one centred `SiriusModalShell` directly under the root.

Use `SiriusModalSizeClass.Medium` (640 px). This matches HPA-373 §9.11 and is large enough for the prompt/choices without turning a small riddle into a full-screen flow.

Do **not** add `%SafeFrame`. `SiriusModalShell` already owns:

- standard 90%-of-viewport width capping;
- compact 12 px margins;
- maximum body height;
- body scrolling and `FollowFocus`.

Stable authored nodes:

```text
PuzzleRiddleScreen (full-viewport Control)
└── ModalShell (%ModalShell, SizeClass = Medium)
    ├── .../BodyHost
    │   ├── FeedbackLabel (%FeedbackLabel)
    │   ├── PromptLabel (%PromptLabel)
    │   ├── ChoicesContainer (%ChoicesContainer)
    │   └── CancelHint (%CancelHint)
    └── .../ActionsHost
        └── CancelButton (%CancelButton)
```

Presentation rules:

- `%ModalShell.Title` = `RiddleId` when nonblank, otherwise `Seal`, preserving the current title fallback.
- `%PromptLabel` stays a `RichTextLabel` with BBCode enabled so this migration does not silently remove the current prompt rendering capability.
- `%FeedbackLabel` is standing in-surface feedback. It is not timed.
- `%ChoicesContainer` receives runtime-created answer buttons because choice count/content are runtime data.
- `%CancelButton` is always visible. In the active-answer phase it says `Cancel`; after a terminal answer result it becomes `Close` (wrong answer) or `Continue` (success).
- `%CancelHint` reuses `SiriusInputHint` for the configured Cancel surface; no new binding presenter is added.
- The shell `BodyScroll` remains the only scroll owner. Do not add nested choice scroll containers unless a focused runtime failure proves the shell cannot keep focused choices reachable.

## Controller contract and presentation phases

Use one small local phase distinction rather than a general state machine.

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

The controller stores the configured riddle once. `TryOpenRiddle(...)` may run before `_Ready()`; authored nodes render stored state once ready. A second call with another riddle is rejected because each controller instance represents one world interaction.

### Awaiting choice

- Render prompt and choices.
- Clear or show standing feedback as provided by the latest rearm.
- Enable choice buttons.
- Set `InitialFocusTarget` to the first focusable choice, falling back to Cancel.
- Pressing a choice sets a local `_choicePending` latch before emitting `ChoiceSelected` so double activation cannot emit a second answer while Game is resolving the first.

### Dormant/unarmed response

`Game` receives `PuzzleRiddleResult(false, false, message)` and calls:

```csharp
screen.RearmWithFeedback(result.Message);
```

That method:

- shows the message;
- clears `_choicePending`;
- keeps/re-enables the same choices;
- restores focus to the first focusable choice (or Cancel).

No world-interaction cleanup occurs because this is explicitly the nonterminal current behavior.

### Wrong-answer terminal feedback

Game applies the existing damage/stat notification first, then calls:

```csharp
screen.ShowTerminalFeedback(
    $"{result.Message} (-{riddle.WrongAnswerDamage} HP)",
    "Close");
```

The screen disables/hides answer actions, keeps the result readable, changes the action label to `Close`, and focuses it. The answer attempt remains terminal: dismissing the result closes the hosted screen and ends the current world interaction; retry still requires interacting with the riddle again.

This adds a readable result acknowledgement without changing answer validation, damage, or the retry boundary.

### Successful terminal feedback

Game applies the existing solved-state/gate update first, then calls:

```csharp
screen.ShowTerminalFeedback(result.Message, "Continue");
```

The screen hides/disables answers and focuses `Continue`. Solved state is already committed exactly once; dismissing only ends presentation/world interaction.

### Cancellation

`RequestCancel()` and `%CancelButton` converge on `PuzzleRiddleClosed` with a final `_closedEmitted` latch. Configured keyboard/controller Cancel is intercepted by `UIScreenHost` and calls `RequestCancel()`; it never falls through to root Pause.

The final-close latch is separate from `_choicePending` because a choice result may transition to readable terminal feedback before the player dismisses the screen.

## Invalid presentation data

Keep runtime validation presentation-local and minimal:

- `Game` retains its existing blank-`PuzzleId` / already-solved checks.
- `PuzzleRiddleScreenController.TryOpenRiddle(...)` rejects a null riddle or a riddle whose `GetChoices()` is empty, because that surface would have no answer path.
- Do not add a second validator for `CorrectChoiceId`; `PuzzleRiddleSpawn._GetConfigurationWarnings()` already reports authoring mistakes and `PuzzleTrapController` remains the runtime answer authority.
- Scene load/instantiation failure, controller rejection, host rejection, and publication exceptions fail closed: log the failure, discard the candidate view, and end the world interaction if it was started.

This avoids changing malformed-content domain semantics beyond preventing a UI soft-lock.

## Responsive layout

Follow the existing centred-shell controller pattern:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady() || _shell == null || !IsInsideTree())
        return;

    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _cancelHint.Compact = _shell.Compact;
    _shell.RefreshPresentation(size);
}
```

The controller subscribes `Resized` in `_Ready()` and unsubscribes it in `_ExitTree()`.

At 640×360, the shell fills the usable compact width with 12 px margins and caps body height so prompt/choices scroll internally while title/actions remain reachable. No new breakpoint or size token is introduced.

## Host integration

Replace direct `GetNode("UI").AddChild(...)` presentation with one explicit `UIScreenEntrySpec`:

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

No parent handle or exclusive group is needed: a world riddle is a direct gameplay modal and `IsInWorldInteraction` already prevents a competing riddle/world interaction from starting.

### Keep lifecycle hardening local to Game

Do not extract a new generic `Game.TryHostSurface` helper for one new consumer. The riddle open path should explicitly handle the protocol proven necessary by recent hosted surfaces:

1. Require a live `_screenHost` before starting the interaction.
2. Load/instantiate/configure the riddle screen and subscribe signals.
3. Set `_activePuzzleRiddle`, call `StartWorldInteraction()`, and refresh the exploration prompt.
4. Call `_screenHost.TryPresent(...)` inside `try/catch`.
5. On rejected/no-handle result, unsubscribe/free the candidate and end the world interaction.
6. On a thrown post-commit publication callback, unsubscribe/free the candidate, converge on idempotent world cleanup, then log/rethrow only if the surrounding Game path expects propagation; the player must never remain latched in world interaction.
7. After `Opened`, call `_screenHost.IsActive(handle)` before retaining `_puzzleRiddleScreen` / `_puzzleRiddleHandle`, because a publication subscriber can synchronously close the entry.
8. Retain screen + handle only when the handle is still active.

This is intentionally explicit rather than another abstraction.

## Cleanup convergence

Replace `CleanupPuzzleRiddleDialog(...)` with hosted cleanup centered on the screen/handle pair.

Host cleanup callback responsibilities:

- unsubscribe `ChoiceSelected` and `PuzzleRiddleClosed` from the concrete screen;
- clear `_puzzleRiddleScreen` / `_puzzleRiddleHandle` when they refer to that screen;
- clear `_activePuzzleRiddle`;
- call `GameManager.EndWorldInteraction()` only when the flag is still active;
- refresh the interaction prompt when the Game is still inside the tree.

All close paths use host `TryClose(...)` when an active handle exists. A stale handle clears local state through the same idempotent cleanup.

Terminal handlers catch/log a close-publication exception after host cleanup has already run; they must not skip world-interaction restoration because an `EffectiveStateChanged` subscriber threw.

`Game._ExitTree()` closes the active riddle entry with `HostTeardown` when possible, then falls back to local idempotent cleanup. Raw node teardown therefore cannot leave `IsInWorldInteraction` latched.

Once the riddle is hosted, remove the old special case in `HandleGameplayRootCancel()` that checks `_puzzleRiddleDialog`. The host owns topmost Cancel. The existing bare `IsInWorldInteraction` fallback remains `Consumed` so a transient/failure window never opens Pause over a world interaction.

## Legacy cleanup

After hosted parity and lifecycle tests are green:

- delete `scripts/ui/PuzzleRiddleDialog.cs`;
- delete `tests/ui/PuzzleRiddleDialogTest.cs`;
- remove `_puzzleRiddleDialog` and native-dialog references from `Game`;
- remove riddle-specific reliance on `ui_close_dialog` from lifecycle tests while leaving Settings/native compatibility coverage untouched where still required;
- update the HPA-376 `WORLD-RIDDLE` and `WORLD-CLEANUP` rows to describe the hosted route.

No compatibility wrapper remains.

## Testing strategy

### `PuzzleRiddleScreenControllerTest`

Cover:

- scene loads as `PuzzleRiddleScreenController` and contains no `AcceptDialog`;
- pre-ready `TryOpenRiddle(...)` renders after `_Ready()` and a second start is rejected;
- blank `RiddleId` renders `Seal`;
- authored Medium shell and visible Cancel action;
- runtime choices use labels/ids from `GetChoices()` and first choice is the initial focus target;
- no-choice input is rejected rather than presenting a stuck surface;
- one choice emits once while `_choicePending` is set;
- `RearmWithFeedback(...)` shows dormant feedback and permits exactly one new choice;
- terminal feedback hides/disables answers, changes/focuses the action button, and does not emit another answer;
- `RequestCancel()` / action button emit close exactly once;
- 640×360 compact mode sets shell/hint compact and long prompt/choices remain reachable through the shell body scroll.

### `GameTest`

Retarget the existing real-domain puzzle tests rather than replacing their assertions with mocks:

- switch → correct riddle still marks solved, opens gate, disables trap, and shows success feedback before dismissal;
- wrong answer still applies exactly `WrongAnswerDamage`, floors no lower than 1 HP, shows wrong feedback, and allows a fresh retry after dismissal;
- dormant/unarmed answer keeps the same hosted entry active and shows the controller result message;
- closing success/wrong/cancel clears `IsInWorldInteraction` once;
- no-choice/controller-open failure does not leave world interaction active.

### `GameInputLifecycleTest`

Retarget configured Cancel to the hosted route:

- configured keyboard Cancel closes only `UIScreenKinds.PuzzleRiddle`, restores gameplay, and does not open Pause;
- configured controller Cancel does the same;
- a bare world-interaction latch with no hosted surface is still consumed by the root fallback.

### Host/teardown proof

Add focused coverage (in `GameTest` or the existing host lifecycle suite, whichever keeps the fixture smaller) proving:

- riddle uses Modal/Always/no-tree-pause/block-gameplay/hidden-HUD/visible-cursor/visible-inert policy;
- scene/host teardown closes the riddle entry and clears world interaction;
- a synchronously closed handle returned from `TryPresent` is not retained;
- post-commit presentation/close publication failure cannot soft-lock world interaction.

Do not add a broad new host test matrix.

## Verification

Focused first:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest"
```

Then full suite:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Stale-path checks:

```bash
git grep -n "PuzzleRiddleDialog\|AcceptDialog" -- scripts tests scenes

git grep -n "_puzzleRiddleDialog" -- scripts tests
```

The first command may still find unrelated native surfaces if any remain outside HPA-571; it must not find the retired riddle dialog/path.

## Acceptance mapping

| HPA-571 acceptance criterion | Design proof |
|---|---|
| No desktop-window framing | Scene-authored full-viewport `Control` + `SiriusModalShell`; native `AcceptDialog` deleted. |
| Existing solutions, penalties, switch gates, persistence, retry unchanged | `PuzzleTrapController`/spawn/domain APIs untouched; Game keeps existing damage/gate methods; wrong-answer retry remains a fresh interaction after terminal dismissal. |
| Long text usable at minimum viewport | Medium shell body owns compact scrolling at 640×360. |
| Mouse, keyboard, gamepad focus usable | authored Cancel, first-choice initial focus, host-configured Cancel interception, focus transfer to terminal action. |
| Every exit clears world interaction exactly once | host Cleanup + idempotent flag check is the convergence point for terminal, rejection, exception, stale handle, and teardown. |
| Existing puzzle-domain tests green | domain controller remains unchanged; current real-domain Game tests are retargeted rather than discarded. |

## Deferred work

- HPA-573 owns generic reward feedback/queueing.
- HPA-359 owns final cross-screen validation and release smoke coverage.
- New puzzle families or a reusable puzzle abstraction wait for a second real puzzle consumer.