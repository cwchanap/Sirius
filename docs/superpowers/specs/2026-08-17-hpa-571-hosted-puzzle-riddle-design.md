# HPA-571 Hosted Puzzle and Riddle Design

**Issue:** HPA-571  
**Status:** Proposed  
**Date:** 2026-08-17

## Context

HPA-571 is the next actionable child of the HPA-358 secondary-presentation workstream. HPA-569 (Dialogue), HPA-570 (Shop and Healing), and HPA-572 (shared confirmations/errors) are complete; HPA-573 Reward Feedback follows Puzzle/Riddle.

The current riddle path is still the legacy desktop-style exception:

- `PuzzleRiddleDialog` derives from `AcceptDialog` and builds its prompt/choices at runtime.
- `Game` creates that native dialog directly under the gameplay `UI` canvas instead of using `UIScreenHost`.
- `PuzzleTrapController` already owns switch arming, answer validation, solved persistence, and result messages.
- `Game` already owns wrong-answer damage, solved gate/grid application, world-interaction state, and interaction-prompt restoration.
- `UIScreenKinds.PuzzleRiddle` already exists.

HPA-373 defines Puzzle as a centred Medium Sirius panel with title, prompt, answer choices, validation feedback, a visible Cancel control, and the active device's Cancel hint. HPA-376 records the current world-interaction lifecycle that this migration must preserve except for one explicit root-teardown cleanup normalization described below.

## Goals

- Replace `PuzzleRiddleDialog` with one scene-authored Sirius riddle surface.
- Present it through the existing gameplay `UIScreenHost` as `UIScreenKinds.PuzzleRiddle`.
- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch requirements, answer validation, wrong-answer damage rules, solved persistence, and gate behavior unchanged.
- Make dormant/unarmed, wrong-answer, and success feedback readable inside the riddle surface.
- Preserve wrong-answer retry semantics: one answer attempt ends, then a fresh interaction is required to retry.
- Keep long prompts and many/long choices usable at 640×360 through the existing shell scroll owner.
- Provide deterministic keyboard/gamepad focus, a visible Cancel button, and the HPA-373 Cancel binding hint.
- Converge cancellation, answer completion, invalid presentation data, host rejection, publication exceptions, stale handles, node teardown, and scene teardown on idempotent cleanup.

## Non-goals

- New puzzle types, free-text answers, timed puzzles, hints, multi-stage domain state, or puzzle rewards.
- Changes to `PuzzleTrapController`, `PuzzleRiddleSpawn`, puzzle persistence, trap damage, switch requirements, or gate logic.
- A generic puzzle presenter, base controller, navigation service, host facade, or new `UIScreenHost` API.
- A reusable choice-row component.
- Reward queues/toasts from HPA-573 or new confirmation/error primitives.
- New theme tokens, icon art, save schema, or compatibility shims.
- Refactoring existing Game-hosted Battle/Pause/Save/Load/Prompt surfaces into a new host abstraction.

## Ownership

Use one concrete scene/controller pair:

- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`

`Game` remains the world-riddle orchestration owner. It loads the scene, starts the existing world-interaction latch, presents through `UIScreenHost`, resolves a selected choice through `PuzzleTrapController`, applies the existing Game-owned damage/gate effects, and closes/cleans the hosted entry.

`PuzzleRiddleScreenController` is presentation-only. It renders an already-existing `PuzzleRiddleSpawn`, creates local answer buttons, tracks its small presentation phase, and emits presentation events. It never calls `PuzzleTrapController`, mutates `Character`, marks puzzles solved, changes the grid, or writes persistence.

Do not route world riddles through `NpcInteractionController`; NPC interaction uses a different domain latch and orchestration lifecycle.

## Existing behavior contract

Preserve these facts:

1. Blank `PuzzleId` or an already-solved riddle does not start a new interaction.
2. `PuzzleTrapController.TrySolveRiddle(...)` remains the single answer authority for invalid, already-solved, dormant, wrong, and successful results.
3. Wrong answers apply `WrongAnswerDamage` through `Game.ApplyPuzzleDamage(...)`, which floors HP at 1, then notify player stats.
4. Successful answers apply the existing solved gate/grid state and notify player stats.
5. Dormant/unarmed results do not end the current riddle interaction; the same presentation rearms with feedback.
6. Wrong answers remain terminal for the current attempt. Retry requires closing the result and interacting again.
7. While the riddle is active, `IsInWorldInteraction` blocks competing gameplay and the exploration prompt stays hidden.
8. Choice and Cancel are mutually exclusive while an answer is being resolved: once a choice starts resolution, a second choice or Cancel cannot emit another terminal presentation event.

### Intentional root-teardown cleanup normalization

Current `Game._ExitTree()` calls `CleanupPuzzleRiddleDialog(endWorldInteraction: false)`. HPA-571 intentionally changes the hosted riddle route so root teardown closes/clears the presentation **and ends an active world-interaction latch before the Game tree finishes exiting**.

This is a deliberate lifecycle cleanup change, not a domain-rule change. `GameManager` is a scene-local singleton and clears `GameManager.Instance` in its own `_ExitTree()`, so the motivation is not an autoload leaking state across scenes. The reason is consistency: every riddle terminal/failure/teardown path should leave the still-live owner in a completed state before it is discarded, matching the HPA-376 `WORLD-CLEANUP` contract and making teardown deterministic to test.

## Scene composition

`PuzzleRiddleScreen.tscn` is a full-viewport `Control` containing one centred `SiriusModalShell` directly under the root.

Use `SiriusModalSizeClass.Medium` (640 px). Do **not** add `%SafeFrame`; centred non-Full shell geometry already owns width capping, compact margins, body height, scrolling, and `FollowFocus`.

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

Rules:

- `%ModalShell.Title` = nonblank `RiddleId`, otherwise `Seal`.
- `%PromptLabel` remains a BBCode-enabled `RichTextLabel`.
- `%FeedbackLabel` follows the existing Healing standing-feedback defaults: hidden initially, `SiriusMetadata`, word-wrapped, non-timed.
- `%ChoicesContainer` receives runtime answer buttons.
- `%CancelHint` reuses `SiriusInputHint` and stays in fixed `ActionsHost` chrome so long body content cannot scroll it away.
- `%CancelButton` is always visible; it reads `Cancel` while awaiting an answer and `Close` / `Continue` in terminal result presentation.
- The shell `BodyScroll` is the only scroll owner.

## Runtime answer buttons

Copy Dialogue's local six-line runtime button pattern; do not create a shared component:

```csharp
private static Button CreateActionButton(string text) => new()
{
    Text = text,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
```

`RefreshLayout()` reapplies `SiriusUiMetrics.MinimumTarget(compact).Y` to every runtime answer button. This yields 44 px standard and 40 px compact targets instead of hard-coding 44 px.

## Presentation phase

A single terminal boolean is insufficient because the new UX remains open after answer resolution. Use one local enum plus one final-close latch:

```csharp
private enum PuzzleRiddlePresentationPhase
{
    AwaitingChoice,
    Resolving,
    Terminal
}

private PuzzleRiddlePresentationPhase _phase = PuzzleRiddlePresentationPhase.AwaitingChoice;
private bool _closedEmitted;
```

This is local presentation state, not a puzzle-domain state machine.

### AwaitingChoice

- Choices are visible and enabled.
- First focusable choice is `InitialFocusTarget`; Cancel is fallback.
- One answer may transition to `Resolving` and emit `ChoiceSelected`.
- Cancel may emit `PuzzleRiddleClosed` exactly once.

### Resolving

- Choice buttons are disabled immediately before `ChoiceSelected` emits.
- Further choices are ignored.
- Cancel / `RequestCancel()` is ignored.
- The only legal exits are `RearmWithFeedback(...)` or `ShowTerminalFeedback(...)` after Game finishes synchronous domain resolution.

This preserves the current `ChoiceThenCancel_EmitsChoiceSelectedOnly` invariant even though the final result remains visible afterward.

### Dormant/unarmed response

`Game` receives `PuzzleRiddleResult(false, false, message)` and calls:

```csharp
screen.RearmWithFeedback(result.Message);
```

That method:

- shows standing feedback;
- returns phase to `AwaitingChoice`;
- re-enables the same choices;
- restores a valid choice focus target.

World interaction remains active.

### Wrong-answer terminal feedback

Game records HP before applying the existing penalty, applies `ApplyPuzzleDamage(...)`, then calculates the **actual** loss:

```csharp
var healthBefore = _gameManager.Player.CurrentHealth;
ApplyPuzzleDamage(riddle.WrongAnswerDamage);
var healthLost = healthBefore - _gameManager.Player.CurrentHealth;
```

Then:

```csharp
screen.ShowTerminalFeedback(
    $"{result.Message} (-{healthLost} HP)",
    "Close");
```

Using actual loss matters because puzzle damage floors the player at 1 HP and the gameplay HUD is hidden while this modal is active.

Choices become unavailable; `Close` is focused. Dismissal ends the current world interaction. Retry requires a fresh interaction.

### Successful terminal feedback

Game applies existing solved state first, then:

```csharp
screen.ShowTerminalFeedback(result.Message, "Continue");
```

Solved state is already committed. `Continue` only dismisses presentation and ends the world interaction.

### Terminal

- Choices are hidden/disabled.
- `Close` / `Continue` is focused.
- Choice emissions are ignored.
- Cancel and the visible action both converge on one `_closedEmitted` close signal.

## Responsive layout

Follow the existing centred-shell pattern:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady() || _shell == null || !IsInsideTree())
        return;

    var size = GetViewportRect().Size;
    var compact = SiriusUiMetrics.IsCompact(size);
    _shell.Compact = compact;
    _cancelHint.Compact = compact;
    _promptLabel.ThemeTypeVariation = compact
        ? SiriusThemeTypes.BodyCompact
        : SiriusThemeTypes.Body;
    _feedbackLabel.ThemeTypeVariation = compact
        ? SiriusThemeTypes.MetadataCompact
        : SiriusThemeTypes.Metadata;

    var target = SiriusUiMetrics.MinimumTarget(compact);
    foreach (var child in _choicesContainer.GetChildren())
        if (child is Button button)
            button.CustomMinimumSize = new Vector2(0f, target.Y);

    _shell.RefreshPresentation(size);
}
```

Subscribe `Resized` in `_Ready()` and unsubscribe in `_ExitTree()`.

At 640×360 the shell uses compact margins and internal body scrolling; no additional breakpoint or nested scroll container is introduced.

## Host integration

Use the existing kind and explicit policy:

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

No parent handle or exclusive group is needed; `IsInWorldInteraction` already prevents a competing world interaction from starting.

## Host lifecycle hardening: keep the Game-shaped subset local

`NpcInteractionController.TryHostSurface(...)` is the correct behavioral reference but is not directly reusable. It is not just `TryPresent + Finish()`: after the `IsActive` recheck it also inspects the NPC owner's `_finished` state and mechanically closes an entry when a publication subscriber synchronously finished the NPC interaction before the caller retained its screen/handle. Generalizing that private helper for Game would therefore need both failure callbacks and owner-finished state/callback semantics, and would modify the already-shipped NPC lifecycle for one new Game consumer.

HPA-571 therefore does **not** extract a shared helper and does not create a one-call-site private `Game.TryHostSurface`. The riddle path keeps the small Game-specific subset inline:

1. Require a live `_screenHost` before starting world interaction.
2. Load/instantiate/configure the screen and subscribe signals.
3. Set `_activePuzzleRiddle`, call `StartWorldInteraction()`, refresh prompt.
4. Call `TryPresent(...)` inside `try/catch`.
5. On rejection/no handle, unsubscribe/free candidate and end world interaction.
6. On publication throw, converge on idempotent local cleanup so the world latch cannot remain active; log the failure.
7. After `Opened`, call `IsActive(handle)` before retaining `_puzzleRiddleScreen` / `_puzzleRiddleHandle`, because final publication can synchronously close the entry.
8. Retain screen/handle only when still active.

If a second **Game-owned** surface later needs this exact post-commit protocol, that second consumer is the point to extract a Game/shared helper. HPA-571 does not refactor the six existing Game host call sites.

## Cleanup convergence

Replace native-dialog cleanup with hosted screen/handle cleanup.

`ClearPuzzleRiddlePresentation(screen)`:

- unsubscribes `ChoiceSelected` and `PuzzleRiddleClosed`;
- clears `_puzzleRiddleScreen` / `_puzzleRiddleHandle` when they refer to that screen;
- clears `_activePuzzleRiddle`;
- ends world interaction only if still active;
- refreshes the interaction prompt only while Game is still inside the tree.

All explicit close paths use `UIScreenHost.TryClose(...)` when a live handle exists. A stale handle converges on the same local clear routine. Close-publication exceptions are logged after cleanup rather than allowed to skip world restoration.

`Game._ExitTree()` closes the active hosted riddle with `HostTeardown` when possible, then falls back to local idempotent cleanup. This is the intentional teardown normalization described above.

Once hosted, remove the old `_puzzleRiddleDialog` special case in `HandleGameplayRootCancel()`. The host owns topmost Cancel. The bare `IsInWorldInteraction` fallback remains `Consumed`, so a transient failure window never opens Pause.

## Invalid presentation data

Keep validation narrow:

- `Game` keeps blank-`PuzzleId` / already-solved guards.
- `TryOpenRiddle(...)` rejects null or zero-choice input to prevent a stuck surface.
- Do not add a second `CorrectChoiceId` validator; authoring warnings and `PuzzleTrapController` already own that concern.
- Scene load/instantiation failure, controller rejection, host rejection, publication exceptions, and stale handles fail closed with idempotent world cleanup.

## Test reuse

Add one shared test-only helper:

```csharp
public static bool ContainsAcceptDialog(Node node)
```

to `tests/TestHelpers.cs` and use `TestHelpers.ContainsAcceptDialog(screen)` in the new riddle suite. Existing duplicate local helpers in Dialogue/Healing/Shop/Prompt do not need drive-by migration in HPA-571.

The scene test also asserts:

- `%SafeFrame` does not exist;
- `%ModalShell` is a direct child of the screen;
- shell size class is Medium.

## Testing strategy

### `PuzzleRiddleScreenControllerTest`

Cover:

- scene loads with no recursive `AcceptDialog`;
- no SafeFrame and direct Medium shell parent;
- pre-ready one-shot configuration and second-start rejection;
- blank `RiddleId` fallback;
- no-choice input rejection;
- Dialogue-style answer button theme/autowrap/expand-fill;
- standard/compact minimum target sizing;
- first valid answer initial focus;
- `ChoiceThenCancel_WhileResolving_EmitsChoiceOnly`;
- dormant rearm returns to `AwaitingChoice` and permits one new answer;
- terminal feedback hides choices and focuses final action;
- final action/Cancel closes once;
- 640×360 long content remains reachable through shell scroll;
- prompt and feedback switch to compact theme variations.

### `GameTest`

Retarget existing real puzzle fixtures:

- hosted open policy and `IsInWorldInteraction`;
- switch → correct answer still solves, opens gate, disables trap, then shows success feedback until dismissal;
- wrong answer applies actual domain damage, shows actual HP-loss feedback, remains unsolved, then fresh interaction can retry after dismissal;
- dormant answer keeps the same hosted entry and world latch active;
- controller/open failure leaves no active world interaction.

### `GameInputLifecycleTest`

Retarget configured Cancel to the hosted route:

- keyboard Cancel closes hosted riddle once and never opens Pause;
- controller Cancel does the same;
- terminal Close/Continue uses the same one-shot close path;
- bare `IsInWorldInteraction` without an entry remains consumed by root fallback.

Do **not** add an integration test that tries to inject Cancel in the middle of the synchronous `Pressed → ChoiceSelected → Game handler → Rearm/Terminal` call chain. Godot cannot dispatch another input event inside that synchronous chain; the controller-level Resolving test is the deterministic contract proof.

### Teardown/failure coverage

Cover:

- host unavailable/rejection before world latch starts;
- post-commit inactive-handle recheck;
- publication throw cleanup;
- stale close handling through the idempotent riddle close/clear path;
- root teardown while riddle is open intentionally ends the world latch before owner exit.

## Legacy cleanup

After hosted parity/lifecycle tests are green:

- delete `scripts/ui/PuzzleRiddleDialog.cs`;
- delete `tests/ui/PuzzleRiddleDialogTest.cs`;
- remove native field/type references from Game and tests;
- remove riddle-specific native `ui_close_dialog` assumptions while retaining unrelated Settings compatibility coverage;
- update HPA-376 `WORLD-RIDDLE` and `WORLD-CLEANUP` to the hosted route and explicitly record root-teardown world-latch completion.

No compatibility wrapper remains.

## Risks and mitigations

- **UI becomes a second puzzle owner:** controller never calls puzzle domain APIs; Game resolves choice ids.
- **Choice/Cancel race:** local `Resolving` phase structurally ignores Cancel/repeated choices.
- **Wrong feedback overstates damage:** calculate actual HP loss after the 1-HP floor.
- **Compact choices become undersized/unreadable:** reuse Dialogue button treatment, `MinimumTarget`, compact body/metadata variations, and shell scroll.
- **Post-commit host publication returns a stale handle:** recheck `IsActive` before retention.
- **Presentation failure leaves gameplay blocked:** every rejection/throw/stale/teardown path converges on idempotent world cleanup.
- **Host abstraction grows for one consumer:** keep Game-specific protocol inline until a second Game consumer proves extraction.

## Review disposition

The second follow-up review was checked against current `main`:

- **Accepted:** merge the old Tasks 2+3 into one final-UX Game cutover; add `TestHelpers.ContainsAcceptDialog`; name the `_ExitTree` world-latch completion as an intentional cleanup normalization; remove the unreachable mid-synchronous-resolve integration test; add compact prompt/feedback theme variations.
- **Not adopted:** moving `NpcInteractionController.TryHostSurface` into a cross-class helper or creating a one-call-site private `Game.TryHostSurface`. The existing helper has NPC `_finished` semantics beyond the Game requirement, so HPA-571 keeps only the proven Game-shaped subset inline.

## Follow-up ownership

- HPA-573 owns generic reward feedback/queueing.
- HPA-359 owns final cross-screen validation and release smoke coverage; if multiple Game-owned surfaces prove they need the same post-commit host hardening, consolidation belongs there or in the first concrete follow-up that supplies the second Game consumer.
- New puzzle families or a reusable puzzle abstraction wait for a second real puzzle consumer.
