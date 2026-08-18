# HPA-571 Hosted Puzzle and Riddle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the native puzzle/riddle `AcceptDialog` with a scene-authored, `UIScreenHost`-managed Sirius riddle surface while preserving puzzle rules, answer/cancel mutual exclusion, and exactly-once world-interaction cleanup.

**Architecture:** Keep `Game` as the world-riddle orchestration owner and `PuzzleTrapController` as the domain authority. Add one `PuzzleRiddleScreen.tscn` + `PuzzleRiddleScreenController`, present it through existing `UIScreenKinds.PuzzleRiddle`, and keep host open/close mechanics explicit in `Game`. Use a tiny presentation phase (`AwaitingChoice`, `Resolving`, `Terminal`) so answer resolution cannot race Cancel. No puzzle framework, presenter/view-model, host facade, new host API, or compatibility shim.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusModalShell`, `SiriusInputHint`, `SiriusUiMetrics`, `UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-17-hpa-571-hosted-puzzle-riddle-design.md`

## Global Constraints

- Keep `Game` as the world-riddle owner; do not route riddles through `NpcInteractionController`.
- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch arming, answer validation, `MarkPuzzleSolved`, persistence, gate application, trap behavior, and wrong-answer damage rules unchanged.
- Add exactly one new production scene/controller pair.
- Reuse `SiriusModalShell` with `SiriusModalSizeClass.Medium`; no `%SafeFrame`, new theme tokens, or new metrics.
- Reuse `UIScreenKinds.PuzzleRiddle`; no new kind, group, parent handle, incompatible-kind rule, or host API.
- Host policy: `Modal` layer, `Modal` input priority, `Always`, no tree pause, gameplay blocked, HUD hidden, cursor visible, lower layers visible/inert, Cancel consumed/intercepted, `QueueFree` lifetime.
- Dynamic answers remain controller-local.
- Runtime answer buttons use Dialogue's local pattern: `SiriusSecondaryButton`, `WordSmart`, `ExpandFill`, and `SiriusUiMetrics.MinimumTarget(...)` height.
- `%FeedbackLabel` starts hidden, uses `SiriusMetadata`, autowraps, and is never timed.
- Keep `%CancelHint`: HPA-373 §9.11 explicitly requires Puzzle's active-device cancel hint. Mount it in fixed `ActionsHost` chrome beside the Cancel button, not in scrolling body content.
- Presentation phase is `AwaitingChoice -> Resolving -> (AwaitingChoice | Terminal)`. Cancel and repeated choices are ignored during `Resolving`.
- Dormant/unarmed response stays on the same hosted screen and returns to `AwaitingChoice`.
- Wrong answer stays terminal for the current attempt. HPA-571 adds readable feedback before dismissal; retry still requires a fresh interaction.
- Success commits solved state before readable terminal feedback; dismissal only ends presentation.
- Wrong-answer feedback reports actual HP lost after the 1-HP floor, not configured `WrongAnswerDamage`.
- Every close, stale handle, rejected open, invalid surface, publication exception, host teardown, and root teardown must leave `GameManager.IsInWorldInteraction == false` exactly once.
- The `_puzzleRiddleDialog` field cutover must be compile-complete across production and both test suites in one task.
- Delete `PuzzleRiddleDialog` only after hosted tests are green; leave no compatibility wrapper.

---

## File Structure

### Create

- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`
- `tests/ui/PuzzleRiddleScreenControllerTest.cs`

### Modify

- `scripts/game/Game.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete after replacement coverage is green

- `scripts/ui/PuzzleRiddleDialog.cs`
- `tests/ui/PuzzleRiddleDialogTest.cs`

### Reference only

- `scripts/game/PuzzleTrapController.cs`
- `scripts/game/PuzzleRiddleSpawn.cs`
- `scripts/ui/DialogueScreenController.cs`
- `scripts/ui/HealingScreenController.cs`
- `scenes/ui/HealingScreen.tscn`
- `scripts/ui/components/SiriusModalShell.cs`
- `scenes/ui/components/SiriusModalShell.tscn`
- `scripts/ui/components/SiriusInputHint.cs`
- `scenes/ui/components/SiriusInputHint.tscn`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/NpcInteractionController.cs` — lifecycle-hardening reference only; do not reuse its `Finish()`-specific helper.

---

## Risk Checklist

### Answer and Cancel race while Game resolves a choice

The native dialog protects answer/Cancel mutual exclusion with one latch. A pair of independent `_choicePending` / `_closedEmitted` booleans does not: Cancel could close the screen while `Game.OnPuzzleRiddleChoiceSelected(...)` is still resolving the answer.

Use the explicit local phase. `RequestCancel()` ignores `Resolving`; only `RearmWithFeedback(...)` or `ShowTerminalFeedback(...)` can leave `Resolving`.

### Game cutover does not compile

`_puzzleRiddleDialog` is referenced from `Game.cs`, `GameTest.cs`, and `GameInputLifecycleTest.cs`. Replace every field/type lookup and old cleanup call in the same task. Run a solution build at that gate.

### Runtime answer styling regresses at compact size

Copy Dialogue's local `CreateActionButton` pattern and reapply `SiriusUiMetrics.MinimumTarget(_shell.Compact)` in `RefreshLayout()`. Assert 44 px standard / 40 px compact through the existing metric rather than hard-coded scene values.

### Cancel hint scrolls away

HPA-373 requires the active-device cancel hint for Puzzle. Keep it, but place it in fixed `ActionsHost` chrome beside Cancel rather than in the shell body.

### Native-dialog test gives a false negative

Use recursive `ContainsAcceptDialog(...)`, plus explicit `GetNodeOrNull("%SafeFrame") == null` and `shell.GetParent() == screen`. Do not use `FindChild("*") is AcceptDialog`.

### Wrong feedback overstates damage

Capture health before `ApplyPuzzleDamage`, then subtract post-damage health. Display actual HP lost because the damage path floors at 1 HP.

---

## Task 1: Build the Authored Riddle Screen and Preserve Mutual Exclusion

**Files:**
- Create: `scenes/ui/PuzzleRiddleScreen.tscn`
- Create: `scripts/ui/PuzzleRiddleScreenController.cs`
- Create: `tests/ui/PuzzleRiddleScreenControllerTest.cs`
- Reference: `scripts/ui/PuzzleRiddleDialog.cs`
- Reference: `tests/ui/PuzzleRiddleDialogTest.cs`
- Reference: `scripts/ui/DialogueScreenController.cs`
- Reference: `scenes/ui/HealingScreen.tscn`

**Interfaces:**
- Consumes: `PuzzleRiddleSpawn.RiddleId`, `PromptText`, `GetChoices()`.
- Produces:

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

### 1.1 RED: authored structure and one-shot configuration

- [ ] Create `PuzzleRiddleScreenControllerTest` with a `SubViewportContainer` + `SubViewport` fixture following existing UI tests.
- [ ] Add recursive native-dialog detection:

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

- [ ] Add a failing structure test that asserts:

```csharp
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
AssertThat(ContainsAcceptDialog(screen)).IsFalse();
AssertThat(screen.GetNodeOrNull("%SafeFrame")).IsNull();
AssertThat(shell.GetParent()).IsEqual(screen);
AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Medium);
AssertThat(screen.GetNode<Button>("%CancelButton").Visible).IsTrue();
AssertThat(screen.GetNode<SiriusInputHint>("%CancelHint").GetParent())
    .IsEqual(shell.ActionsHost);
```

- [ ] Add failing tests for pre-ready `TryOpenRiddle(...)`, second-start rejection, blank `RiddleId -> Seal`, no-choice rejection, and first-answer initial focus.
- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

Expected: FAIL because the new scene/controller do not exist.

### 1.2 GREEN: author the centred Medium scene

- [ ] Create:

```text
PuzzleRiddleScreen (full-viewport Control)
└── ModalShell (%ModalShell; Medium)
    ├── .../BodyHost
    │   ├── FeedbackLabel (%FeedbackLabel)
    │   ├── PromptLabel (%PromptLabel; RichTextLabel; BBCode enabled)
    │   └── ChoicesContainer (%ChoicesContainer; VBoxContainer)
    └── .../ActionsHost
        ├── CancelHint (%CancelHint; SiriusInputHint)
        └── CancelButton (%CancelButton; "Cancel")
```

- [ ] Give `%FeedbackLabel` the Healing defaults:

```text
visible = false
theme_type_variation = "SiriusMetadata"
autowrap_mode = WordSmart
```

- [ ] Keep `%CancelButton` as `SiriusSecondaryButton`. Do not hard-code answer rows in the scene.

### 1.3 GREEN: local runtime-answer helper and responsive targets

- [ ] Add the Dialogue-shaped local helper:

```csharp
private static Button CreateActionButton(string text) => new()
{
    Text = text,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
```

- [ ] In `RefreshLayout()`:

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

- [ ] Subscribe `Resized` in `_Ready()` and unsubscribe in `_ExitTree()`.
- [ ] Add tests for `SecondaryButton`, `WordSmart`, `ExpandFill`, 44 px standard target and 40 px compact target.

### 1.4 RED/GREEN: encode presentation phase, not two independent latches

- [ ] Add:

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

- [ ] Choice activation must transition before emission:

```csharp
private void EmitChoice(string choiceId)
{
    if (_closedEmitted || _phase != PuzzleRiddlePresentationPhase.AwaitingChoice)
        return;

    _phase = PuzzleRiddlePresentationPhase.Resolving;
    SetChoicesEnabled(false);
    EmitSignal(SignalName.ChoiceSelected, choiceId);
}
```

- [ ] `RequestCancel()` must be phase-aware:

```csharp
public void RequestCancel()
{
    if (_closedEmitted || _phase == PuzzleRiddlePresentationPhase.Resolving)
        return;

    _closedEmitted = true;
    EmitSignal(SignalName.PuzzleRiddleClosed);
}
```

- [ ] `RearmWithFeedback(...)` only exits `Resolving -> AwaitingChoice`; it shows standing feedback, reenables choices, restores first-answer/Cancel focus, and resets action label to `Cancel`.
- [ ] `ShowTerminalFeedback(...)` only exits `Resolving -> Terminal`; it shows feedback, hides/disables answers, changes action label, and focuses the action button.

### 1.5 RED: port the proven choice/cancel mutual-exclusion contract

- [ ] Add the replacement for `PuzzleRiddleDialogTest.ChoiceThenCancel_EmitsChoiceSelectedOnly`:

```csharp
[TestCase]
public void ChoiceThenCancel_WhileResolving_EmitsChoiceOnly()
{
    int choices = 0;
    int closed = 0;
    screen.ChoiceSelected += _ => choices++;
    screen.PuzzleRiddleClosed += () => closed++;

    FindAnswer("Answer").EmitSignal(Button.SignalName.Pressed);
    screen.RequestCancel();

    AssertThat(choices).IsEqual(1);
    AssertThat(closed).IsEqual(0);
}
```

- [ ] Add repeated-choice-while-resolving coverage.
- [ ] Add dormant rearm coverage proving one fresh answer is accepted after `RearmWithFeedback(...)`.
- [ ] Add terminal coverage proving Close/Continue can emit final close once after `ShowTerminalFeedback(...)`.

### 1.6 GREEN: compact long-content proof

- [ ] At 640×360 create a long BBCode prompt and enough long choices to overflow the body.
- [ ] Assert shell compact mode, CancelHint compact mode, `BodyScroll.FollowFocus == true`, 40 px answer targets, and final focused answer can be brought into the scroll viewport.
- [ ] Do not add a nested ScrollContainer.

### 1.7 Task 1 gate

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleRiddleDialogTest"
```

Expected: PASS. The legacy dialog still exists only as pre-cutover characterization.

- [ ] Commit:

```bash
git add scenes/ui/PuzzleRiddleScreen.tscn \
  scripts/ui/PuzzleRiddleScreenController.cs \
  tests/ui/PuzzleRiddleScreenControllerTest.cs
git commit -m "feat: add hosted puzzle riddle screen"
```

---

## Task 2: Perform One Compile-Complete Game Host Cutover

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Reference: `scripts/game/PuzzleTrapController.cs`
- Reference: `scripts/ui/NpcInteractionController.cs`

**Interfaces:**
- Consumes: Task 1 `PuzzleRiddleScreenController` contract.
- Produces: Game-owned hosted `_puzzleRiddleScreen` / `_puzzleRiddleHandle` lifecycle.

This task changes presentation ownership but intentionally preserves the **current external success/wrong close timing**. Task 3 enables the new readable terminal acknowledgement. This gives Task 2 a real GREEN gate instead of mixing field migration with changed UX assertions.

### 2.1 RED: hosted route and policy

- [ ] Retarget/add a real `GameTest` that opens a riddle and asserts:

```csharp
var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");
var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.PuzzleRiddle);

AssertThat(entry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(entry.Policy.PauseTree).IsFalse();
AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
AssertThat(gameManager.IsInWorldInteraction).IsTrue();
```

Expected before cutover: FAIL because the native dialog bypasses the host.

### 2.2 Replace the field and cleanup surface atomically

- [ ] In `Game` replace:

```csharp
private PuzzleRiddleDialog? _puzzleRiddleDialog;
```

with:

```csharp
private PuzzleRiddleScreenController? _puzzleRiddleScreen;
private UIScreenHandle? _puzzleRiddleHandle;
```

Keep `_activePuzzleRiddle` and `_puzzleTrapController` unchanged.

- [ ] In the **same edit**, replace `CleanupPuzzleRiddleDialog(...)` and every caller with:

```csharp
private void ClearPuzzleRiddlePresentation(PuzzleRiddleScreenController screen)
private void ClosePuzzleRiddlePresentation(UIScreenCloseReason reason)
```

`ClearPuzzleRiddlePresentation` unsubscribes signals, clears matching screen/handle, clears `_activePuzzleRiddle`, ends world interaction if active, and refreshes the prompt only while Game is inside the tree.

`ClosePuzzleRiddlePresentation` uses host `TryClose(...)`; stale handle converges on the same clear path.

### 2.3 Replace `OpenPuzzleRiddle(...)` with the explicit Game-owned host route

- [ ] Preserve blank-`PuzzleId` and already-solved guards.
- [ ] Require a live `_screenHost` **before** `StartWorldInteraction()`.
- [ ] Load/instantiate `res://scenes/ui/PuzzleRiddleScreen.tscn` and call `TryOpenRiddle(riddle)` before starting the latch.
- [ ] Subscribe:

```csharp
screen.ChoiceSelected += OnPuzzleRiddleChoiceSelected;
screen.PuzzleRiddleClosed += OnPuzzleRiddleClosed;
```

- [ ] Set `_activePuzzleRiddle`, call `StartWorldInteraction()`, and refresh the exploration prompt.
- [ ] Present with the exact spec from the design.
- [ ] Inline only this Game-shaped failure protocol:
  - rejected/no-handle -> unsubscribe/free candidate, end world interaction, log/return;
  - publication exception -> converge on idempotent cleanup, log/return;
  - `Opened` but `!IsActive(handle)` -> retain nothing and ensure world interaction is ended;
  - otherwise retain `_puzzleRiddleScreen` and `_puzzleRiddleHandle`.
- [ ] Do **not** extract `Game.TryHostSurface`.

### 2.4 Keep Task 2 success/wrong externally immediate

- [ ] Port existing answer/domain order unchanged.
- [ ] Dormant result calls `screen.RearmWithFeedback(result.Message)` and stays open.
- [ ] For solved/wrong results in this task only, move through the new controller's legal resolution path and immediately dismiss:

```csharp
screen.ShowTerminalFeedback(result.Message, result.Solved ? "Continue" : "Close");
screen.RequestCancel();
```

This preserves existing Game tests' `IsInWorldInteraction == false` after the answer while avoiding an illegal `RequestCancel()` during `Resolving`.

### 2.5 Retarget every old test field/type lookup in this same task

- [ ] In `GameTest.cs`, replace every `_puzzleRiddleDialog` / `PuzzleRiddleDialog` private-field lookup with `_puzzleRiddleScreen` / `PuzzleRiddleScreenController`.
- [ ] In `GameInputLifecycleTest.cs`, do the same **now**, not in a later task.
- [ ] Retarget button lookup to the new answer controls.
- [ ] Retarget explicit close calls/signals to `screen.RequestCancel()`.
- [ ] Keep success/wrong assertions immediate for this gate; Task 3 changes only those assertions that intentionally adopt the new acknowledgement UX.

### 2.6 Remove native-root Cancel special case

- [ ] Delete the `_puzzleRiddleDialog` check from `HandleGameplayRootCancel()`.
- [ ] Keep the bare `_gameManager.IsInWorldInteraction -> Consumed` fallback.

### 2.7 Task 2 compile/GREEN gate

- [ ] Run build first:

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: 0 errors. This is the explicit proof that no old field/type reference was deferred.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-build --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

Expected: PASS with current success/wrong immediate-close semantics and hosted dormant/cancel behavior.

- [ ] Commit:

```bash
git add scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs
git commit -m "feat: host puzzle riddles through ui screen host"
```

---

## Task 3: Enable Readable Wrong/Success Terminal Feedback

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Consumes: hosted Game route from Task 2 and Task 1 `ShowTerminalFeedback(...)`.
- Produces: HPA-571 terminal acknowledgement UX without moving domain logic.

### 3.1 RED: success remains hosted until Continue

- [ ] Change the correct-answer real `GameTest` to assert, immediately after pressing the answer:

```csharp
AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsTrue();
AssertThat(gate.BlocksMovement).IsFalse();
AssertThat(gameManager.IsInWorldInteraction).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();

var screen = GetPrivateField<PuzzleRiddleScreenController>(game, "_puzzleRiddleScreen");
AssertThat(screen.GetNode<Button>("%CancelButton").Text).IsEqual("Continue");
AssertThat(screen.GetNode<Label>("%FeedbackLabel").Text)
    .Contains("gate opens");
```

Then press `%CancelButton` and assert hosted entry/world interaction close exactly once.

Expected before Task 3 implementation: FAIL because Task 2 immediately dismisses terminal feedback.

### 3.2 RED: wrong answer remains hosted and reports actual HP lost

- [ ] Use a health value where configured damage exceeds available damage-to-1, e.g. health = 5 and `WrongAnswerDamage = 7`.
- [ ] Assert after answer:

```csharp
AssertThat(gameManager.Player.CurrentHealth).IsEqual(1);
AssertThat(gameManager.IsPuzzleSolved(puzzleId)).IsFalse();
AssertThat(gameManager.IsInWorldInteraction).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
AssertThat(screen.GetNode<Button>("%CancelButton").Text).IsEqual("Close");
AssertThat(screen.GetNode<Label>("%FeedbackLabel").Text).Contains("-4 HP");
```

The test must **not** expect `-7 HP`.

Then dismiss and assert a fresh interaction can reopen the same unsolved riddle.

### 3.3 GREEN: remove Task 2's immediate final dismissal

- [ ] In `OnPuzzleRiddleChoiceSelected(...)` keep the existing domain order.
- [ ] Wrong answer:

```csharp
var healthBefore = _gameManager.Player.CurrentHealth;
ApplyPuzzleDamage(riddle.WrongAnswerDamage);
var healthLost = healthBefore - _gameManager.Player.CurrentHealth;
_gameManager.NotifyPlayerStatsChanged();

screen.ShowTerminalFeedback(
    healthLost > 0
        ? $"{result.Message} (-{healthLost} HP)"
        : result.Message,
    "Close");
```

- [ ] Success:

```csharp
ApplyPuzzleSolvedState(riddle.PuzzleId);
_gameManager.NotifyPlayerStatsChanged();
screen.ShowTerminalFeedback(result.Message, "Continue");
```

- [ ] Dormant remains:

```csharp
screen.RearmWithFeedback(result.Message);
```

- [ ] Remove the Task 2 immediate `screen.RequestCancel()` for solved/wrong results.

### 3.4 Confirm domain mutation occurs before presentation only

- [ ] Preserve/extend tests proving:
  - wrong damage/stat notification happens once before terminal display;
  - solved state/gate/grid mutation happens once before terminal display;
  - repeated Close/Continue cannot reapply any domain mutation;
  - the UI controller never calls `PuzzleTrapController` or grants/mutates domain state.

### 3.5 Task 3 GREEN gate

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest"
```

Expected: PASS.

- [ ] Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "feat: show puzzle resolution feedback"
```

---

## Task 4: Harden Hosted Cancel, Failure, and Teardown Lifecycle

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Reference: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

### 4.1 Configured Cancel owns only the hosted riddle

- [ ] Retarget the existing configured-keyboard Cancel test to assert:
  - `UIScreenKinds.PuzzleRiddle` is active before Cancel;
  - Cancel is consumed by the host;
  - `PuzzleRiddleClosed` / hosted close happens once;
  - world interaction clears;
  - exploration prompt restores;
  - Pause never opens.

- [ ] Add/retarget controller Cancel with the same outcome.

### 4.2 Preserve answer/cancel mutual exclusion through host interception

The deterministic contract is owned by the screen unit test because Game's answer resolution is synchronous. Add an integration-level proof only at the host boundary: while the screen is manually held in `Resolving` by emitting a choice without returning a Game result, host `InterceptCancel -> RequestCancel()` must consume the input but leave the entry/world latch active. Then explicitly rearm/close the fixture.

- [ ] Assert Cancel during `Resolving` does not emit close, does not clear `_activePuzzleRiddle`, and does not open Pause.

### 4.3 Host rejection and invalid surface fail closed

- [ ] Add tests for:
  - missing/unavailable host -> no world interaction starts;
  - scene/controller rejection for no choices -> no world interaction starts;
  - `TryPresent` rejected/no handle after latch start -> world interaction ends and candidate is freed;
  - post-open `IsActive(handle) == false` -> retain no stale screen/handle and world interaction is clear.

Use existing host test seams if available; do not add a production host facade solely for these tests.

### 4.4 Close-publication exception still restores domain state

- [ ] Add a regression where a host state-change subscriber throws during riddle close.
- [ ] Assert host cleanup has cleared the riddle screen/handle and `IsInWorldInteraction == false` despite the publication exception.
- [ ] Log the exception; do not rethrow from the user-input terminal handler in a way that skips restoration.

### 4.5 Root teardown closes hosted riddle safely

- [ ] Update `_ExitTree()` to close the active hosted riddle with `HostTeardown` when the host/handle are valid, then fall back to local idempotent clear.
- [ ] Add a real-route teardown test proving no hosted riddle entry and no world latch survive root teardown.

### 4.6 Task 4 GREEN gate

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~UIScreenHostLifecycleTest"
```

Expected: PASS.

- [ ] Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "test: harden hosted puzzle riddle lifecycle"
```

---

## Task 5: Remove Native Riddle UI and Reconcile Lifecycle Documentation

**Files:**
- Delete: `scripts/ui/PuzzleRiddleDialog.cs`
- Delete: `tests/ui/PuzzleRiddleDialogTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Verify: all HPA-571 production/test files

### 5.1 Delete the legacy path

- [ ] Delete `PuzzleRiddleDialog.cs` and its dedicated test only after Tasks 1-4 are green.
- [ ] Do not add an alias, wrapper, or compatibility class.

### 5.2 Update HPA-376

- [ ] Rewrite `WORLD-RIDDLE` to record:
  - hosted `UIScreenKinds.PuzzleRiddle` modal;
  - no tree pause; gameplay blocked; HUD hidden; cursor visible;
  - first answer initial focus;
  - host-consumed/intercepted configured Cancel;
  - `AwaitingChoice -> Resolving -> AwaitingChoice/Terminal` presentation behavior;
  - answer/Cancel mutual exclusion while resolving;
  - dormant same-entry rearm;
  - wrong/success terminal acknowledgement before final close;
  - exactly-once hosted cleanup/world restoration.

- [ ] Rewrite `WORLD-CLEANUP` to point at the hosted clear/close path and root teardown behavior.

### 5.3 Active-source stale-reference audit

- [ ] Run:

```bash
rg -n "PuzzleRiddleDialog|_puzzleRiddleDialog|CleanupPuzzleRiddleDialog|ui_close_dialog" \
  scripts scenes tests
```

Expected:

- zero `PuzzleRiddleDialog`, `_puzzleRiddleDialog`, and `CleanupPuzzleRiddleDialog` matches;
- any remaining `ui_close_dialog` matches belong only to other still-valid settings/native compatibility paths, not riddle handling.

### 5.4 Focused verification

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: PASS.

### 5.5 Full verification

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: all tests pass, build has 0 errors, diff check is clean.

- [ ] Run scope audit:

```bash
git diff --name-status main...HEAD
```

Expected HPA-571 scope only: new riddle screen/controller/tests, Game + two Game test suites, lifecycle doc, and legacy riddle deletion.

### 5.6 Commit final cleanup

- [ ] Commit:

```bash
git add -A scripts/ui/PuzzleRiddleDialog.cs \
  tests/ui/PuzzleRiddleDialogTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "refactor: remove native puzzle riddle dialog"
```

---

## Review-Driven Changes Applied to This Plan

Validated against current `main` before editing:

1. **Accepted — choice/cancel latch hole.** Replace the two-boolean resolution model with `AwaitingChoice | Resolving | Terminal` plus one final-close latch. Port `ChoiceThenCancel_EmitsChoiceSelectedOnly` onto the new controller.
2. **Accepted — Task 2 compile break.** The Game field/handle cutover now retargets all `GameTest` and `GameInputLifecycleTest` old-field/type lookups and all old Game cleanup calls in the same task. Task 2 preserves immediate success/wrong close so it has a real GREEN gate; Task 3 alone changes the acknowledgement UX.
3. **Accepted — runtime answer machinery.** Copy Dialogue's local `CreateActionButton` pattern and apply `SiriusUiMetrics.MinimumTarget(...)` in `RefreshLayout()`.
4. **Not accepted as proposed — remove `%CancelHint`.** HPA-373 §9.11 explicitly requires Puzzle's active-device cancel hint. Keep it using the existing component, but move it to fixed `ActionsHost` chrome so it cannot scroll away. No modal-wide abstraction is added.
5. **Accepted — native-dialog test bug.** Use recursive `ContainsAcceptDialog` plus no-SafeFrame/direct-shell-parent assertions.
6. **Accepted nits.** Show actual HP lost after the 1-HP floor and copy Healing's hidden `SiriusMetadata` feedback-label defaults.

## Completion Checklist

- [ ] One scene/controller; Game orchestration and PuzzleTrapController ownership unchanged.
- [ ] Medium centred shell; no SafeFrame/new host API/puzzle framework.
- [ ] Runtime choices use Sirius SecondaryButton + responsive minimum targets.
- [ ] Puzzle-specific cancel hint stays fixed in action chrome per HPA-373.
- [ ] Choice and Cancel remain mutually exclusive while resolving.
- [ ] Dormant feedback rearms the same hosted entry.
- [ ] Wrong/success results remain readable until explicit dismissal.
- [ ] Wrong feedback reports actual HP lost.
- [ ] Task 2 build/test gate is compile-complete across both Game test suites.
- [ ] Every close/failure/teardown clears world interaction once.
- [ ] Native riddle dialog removed with no compatibility shim.
- [ ] HPA-376 documents the hosted route.
- [ ] Focused tests, full suite, build, `git diff --check`, and stale-reference audit pass.
