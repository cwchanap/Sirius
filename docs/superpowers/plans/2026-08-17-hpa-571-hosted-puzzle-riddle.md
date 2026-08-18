# HPA-571 Hosted Puzzle and Riddle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the native puzzle/riddle `AcceptDialog` with a scene-authored, `UIScreenHost`-managed Sirius riddle surface while preserving puzzle domain rules and exactly-once world-interaction cleanup.

**Architecture:** Keep `Game` as the world-riddle orchestration owner and keep `PuzzleTrapController` as the domain authority. Add one concrete `PuzzleRiddleScreen.tscn` + `PuzzleRiddleScreenController`, present it through the already-defined `UIScreenKinds.PuzzleRiddle`, and converge every close/failure/teardown path through the hosted cleanup callback. Do not add a puzzle framework, presenter/view-model layer, host facade, or domain service.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusModalShell`, `SiriusInputHint`, `SiriusUiMetrics`, `UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-17-hpa-571-hosted-puzzle-riddle-design.md`

## Global constraints

- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch arming, answer validation, `MarkPuzzleSolved`, persistence, gate application, trap behavior, and wrong-answer damage rules unchanged.
- Keep `Game` as the riddle orchestration owner; do not route world riddles through `NpcInteractionController`.
- Add exactly one new production scene/controller pair for HPA-571.
- Reuse `SiriusModalShell` with `SiriusModalSizeClass.Medium`; no new theme tokens or size metrics.
- Do not add `%SafeFrame`; the centred non-Full shell owns width, compact margins, and body scrolling.
- Reuse `UIScreenKinds.PuzzleRiddle`; no new kind, exclusive group, parent handle, incompatible-kind rule, or host API.
- Host policy: Modal / Modal priority / Always / no tree pause / block gameplay / cursor visible / HUD hidden / lower layers visible-inert / Cancel consumed + intercepted / QueueFree lifetime.
- Dynamic answer buttons stay controller-local; do not create a reusable choice-row component.
- `%PromptLabel` remains BBCode-capable so current prompt rendering capability is not silently removed.
- Dormant/unarmed response stays on the same hosted screen and rearms choices.
- Wrong answer stays terminal for the current answer attempt; after readable terminal feedback, dismissal ends the world interaction and a retry requires a fresh interaction.
- Success commits solved state before readable terminal feedback; dismissing the feedback only ends presentation.
- No timers for puzzle feedback.
- Every close, stale handle, rejected open, invalid surface, exception, root teardown, and host teardown must leave `GameManager.IsInWorldInteraction == false` exactly once.
- Delete the native riddle dialog after replacement coverage is green; keep no compatibility shim.

---

## File structure

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

### Reference only unless a focused failure proves otherwise

- `scripts/game/PuzzleTrapController.cs`
- `scripts/game/PuzzleRiddleSpawn.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scenes/ui/components/SiriusModalShell.tscn`
- `scripts/ui/components/SiriusInputHint.cs`
- `scenes/ui/components/SiriusInputHint.tscn`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/NpcInteractionController.cs` — lifecycle-hardening reference only
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs` — host contract reference only

---

## Risk checklist

### The UI accidentally becomes a second puzzle-domain owner

Do not call `PuzzleTrapController` from the new screen. The screen emits choice ids; `Game` resolves them through the existing controller and tells the screen which feedback phase to render.

### Clear feedback changes retry semantics

Wrong answer may display a terminal result before dismissal, but choices stay unavailable. A new answer requires closing and starting a fresh world interaction, matching the existing attempt boundary.

### Host publication closes the entry before Game retains it

After `TryPresent` returns `Opened`, re-check `IsActive(handle)` before assigning `_puzzleRiddleScreen` / `_puzzleRiddleHandle`. If it is already closed, end/verify world-interaction cleanup and retain nothing.

### A close-publication exception skips domain restoration

By the time host close publication throws, the entry Cleanup may already have run. Terminal handlers must catch/log the exception and rely on the idempotent cleanup convergence rather than aborting before `EndWorldInteraction`.

### Compact content becomes unreachable

Use one `SiriusModalShell` body scroll, not nested scroll owners. At 640×360, assert compact shell state and that focused final choices can be scrolled into view.

---

## Task 1: Build the scene-authored riddle surface

**Files:**
- Create: `scenes/ui/PuzzleRiddleScreen.tscn`
- Create: `scripts/ui/PuzzleRiddleScreenController.cs`
- Create: `tests/ui/PuzzleRiddleScreenControllerTest.cs`
- Reference: `scripts/ui/PuzzleRiddleDialog.cs`
- Reference: `scripts/game/PuzzleRiddleSpawn.cs`
- Reference: `scripts/ui/HealingScreenController.cs`
- Reference: `scenes/ui/HealingScreen.tscn`

**Interfaces:**

- Consumes: `PuzzleRiddleSpawn.RiddleId`, `PromptText`, and `GetChoices()`.
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

The controller is presentation-only and one instance represents one world interaction.

- [ ] **Step 1: Write RED scene/configuration tests**

Create `tests/ui/PuzzleRiddleScreenControllerTest.cs` and cover:

```csharp
[TestCase]
public async Task Scene_IsAuthoredMediumSiriusSurface()
{
    var packed = GD.Load<PackedScene>("res://scenes/ui/PuzzleRiddleScreen.tscn");
    AssertThat(packed).IsNotNull();

    var screen = packed!.Instantiate<PuzzleRiddleScreenController>();
    _viewport.AddChild(screen);
    await AwaitFrames(2);

    AssertThat(screen.FindChild("*", recursive: true, owned: false) is AcceptDialog)
        .IsFalse();
    AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").SizeClass)
        .IsEqual(SiriusModalSizeClass.Medium);
    AssertThat(screen.GetNode<Button>("%CancelButton").Visible).IsTrue();
}
```

Also add tests for:

- `TryOpenRiddle(...)` before `_Ready()` renders after attachment;
- second `TryOpenRiddle(...)` is rejected;
- blank `RiddleId` renders `Seal`;
- no-choice riddle returns `false`;
- first valid choice becomes `InitialFocusTarget`.

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

Expected: FAIL because the scene/controller do not exist.

- [ ] **Step 2: Author the stable scene tree**

Create:

```text
PuzzleRiddleScreen (Control; full viewport)
└── ModalShell (%ModalShell; Medium)
    ├── .../BodyHost
    │   ├── FeedbackLabel (%FeedbackLabel)
    │   ├── PromptLabel (%PromptLabel; RichTextLabel; BBCode enabled)
    │   ├── ChoicesContainer (%ChoicesContainer; VBoxContainer)
    │   └── CancelHint (%CancelHint; SiriusInputHint)
    └── .../ActionsHost
        └── CancelButton (%CancelButton; "Cancel")
```

Use existing Sirius body/metadata/secondary-button theme variations and 44 px minimum action height. Do not add a SafeFrame, second panel, or nested scroll container.

- [ ] **Step 3: Implement pre-ready one-shot binding and responsive layout**

Add stored configuration and bind authored nodes in `_Ready()`.

Use this layout shape:

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

Subscribe `Resized` in `_Ready()` and unsubscribe it plus button handlers in `_ExitTree()`.

`TryOpenRiddle` validates `riddle != null` and `riddle.GetChoices().Count > 0`, stores the riddle once, and renders immediately when ready.

- [ ] **Step 4: Render choices and guard answer activation**

Create one `Button` per `PuzzleRiddleChoice` in `%ChoicesContainer`.

Use a local `_choicePending` guard:

```csharp
private void EmitChoice(string choiceId)
{
    if (_choicePending || _closedEmitted)
        return;

    _choicePending = true;
    SetChoicesEnabled(false);
    EmitSignal(SignalName.ChoiceSelected, choiceId);
}
```

Set `InitialFocusTarget` to the first focusable answer, falling back to `%CancelButton`.

Test two sequential emissions from the same button produce one `ChoiceSelected` until Game resolves the result.

- [ ] **Step 5: Implement nonterminal dormant rearm**

Implement:

```csharp
public void RearmWithFeedback(string message)
{
    if (_closedEmitted)
        return;

    _feedbackLabel.Text = message ?? string.Empty;
    _feedbackLabel.Visible = !string.IsNullOrWhiteSpace(_feedbackLabel.Text);
    _choicePending = false;
    SetChoicesVisible(true);
    SetChoicesEnabled(true);
    _cancelButton.Text = "Cancel";
    RefreshChoiceFocus();
}
```

Test that `RearmWithFeedback("The mechanism is dormant.")`:

- keeps the same prompt/choices;
- displays the message;
- permits exactly one new choice emission;
- restores a valid choice focus target.

- [ ] **Step 6: Implement terminal result presentation and final close latch**

Implement:

```csharp
public void ShowTerminalFeedback(string message, string actionLabel)
{
    if (_closedEmitted)
        return;

    _feedbackLabel.Text = message ?? string.Empty;
    _feedbackLabel.Visible = !string.IsNullOrWhiteSpace(_feedbackLabel.Text);
    _choicePending = true;
    SetChoicesEnabled(false);
    SetChoicesVisible(false);
    _cancelButton.Text = string.IsNullOrWhiteSpace(actionLabel) ? "Close" : actionLabel;
    InitialFocusTarget = _cancelButton;
    _cancelButton.GrabFocus();
}

public void RequestCancel()
{
    if (_closedEmitted)
        return;

    _closedEmitted = true;
    EmitSignal(SignalName.PuzzleRiddleClosed);
}
```

Wire `%CancelButton.Pressed` to `RequestCancel()`.

Test:

- wrong terminal feedback hides/disables answers and focuses `Close`;
- success terminal feedback focuses `Continue`;
- terminal feedback never emits another answer;
- button + repeated `RequestCancel()` emit `PuzzleRiddleClosed` once.

- [ ] **Step 7: Prove 640×360 long-content behavior**

Mount the screen in a 640×360 `SubViewport` and create a riddle with a long prompt plus enough long choices to exceed body height.

Assert:

```csharp
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
AssertThat(shell.Compact).IsTrue();
AssertThat(bodyScroll.FollowFocus).IsTrue();
```

Focus the final choice and await layout frames; assert the shell body can scroll so the focused control is within its visible body region. Do not introduce a second ScrollContainer to make the test pass.

- [ ] **Step 8: Run Task 1 GREEN and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleRiddleDialogTest"
```

Expected before legacy deletion: new screen tests PASS and old dialog tests still PASS.

Commit:

```bash
git add scenes/ui/PuzzleRiddleScreen.tscn \
  scripts/ui/PuzzleRiddleScreenController.cs \
  tests/ui/PuzzleRiddleScreenControllerTest.cs
git commit -m "feat: add hosted puzzle riddle screen"
```

---

## Task 2: Host the riddle from Game without changing puzzle rules

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Reference: `scripts/game/PuzzleTrapController.cs`
- Reference: `scripts/ui/NpcInteractionController.cs`

**Interfaces:**

- Consumes: Task 1 `PuzzleRiddleScreenController` interface and existing `UIScreenKinds.PuzzleRiddle`.
- Produces: Game-owned `_puzzleRiddleScreen` / `_puzzleRiddleHandle` lifecycle and hosted world-riddle route.

- [ ] **Step 1: Write RED hosted-open/policy tests in `GameTest`**

Retarget/add a real Game scene test that invokes an adjacent riddle and asserts:

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

Also assert the native `PuzzleRiddleDialog` is no longer parented under `UI` once implementation lands.

Run the focused test and confirm RED against the current native route.

- [ ] **Step 2: Replace native riddle fields with hosted state**

In `Game` replace:

```csharp
private PuzzleRiddleDialog? _puzzleRiddleDialog;
```

with:

```csharp
private PuzzleRiddleScreenController? _puzzleRiddleScreen;
private UIScreenHandle? _puzzleRiddleHandle;
```

Keep:

```csharp
private PuzzleRiddleSpawn? _activePuzzleRiddle;
```

Do not move `_puzzleTrapController` or `_activePuzzleRiddle` into the UI controller.

- [ ] **Step 3: Replace `OpenPuzzleRiddle` with the scene + host route**

Preserve existing blank-id / already-solved guards. Before starting world interaction, require a live `_screenHost` and load:

```csharp
var packed = GD.Load<PackedScene>("res://scenes/ui/PuzzleRiddleScreen.tscn");
var screen = packed?.Instantiate<PuzzleRiddleScreenController>();
```

Reject/log when load/instantiation or `screen.TryOpenRiddle(riddle)` fails.

Subscribe:

```csharp
screen.ChoiceSelected += OnPuzzleRiddleChoiceSelected;
screen.PuzzleRiddleClosed += OnPuzzleRiddleClosed;
```

Then set `_activePuzzleRiddle`, call `_gameManager.StartWorldInteraction()`, refresh the prompt, and present with exactly:

```csharp
var spec = new UIScreenEntrySpec
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
};
```

Do not add a parent or exclusive group.

- [ ] **Step 4: Harden `TryPresent` locally without extracting a Game host facade**

Wrap only this new riddle publication with the same protocol already documented by the hosted NPC path:

```csharp
UIScreenOpenResult result;
try
{
    result = _screenHost.TryPresent(screen, spec);
}
catch (Exception ex)
{
    screen.ChoiceSelected -= OnPuzzleRiddleChoiceSelected;
    screen.PuzzleRiddleClosed -= OnPuzzleRiddleClosed;
    if (GodotObject.IsInstanceValid(screen))
        screen.QueueFree();
    EndPuzzleRiddleWorldInteraction();
    GD.PushError($"[Game] Failed to host puzzle riddle '{riddle.RiddleId}': {ex}");
    return;
}
```

For rejected/no-handle results, perform the same unsubscribe/free/interaction cleanup and return.

Before retaining state:

```csharp
if (!_screenHost.IsActive(result.Handle.Value))
{
    EndPuzzleRiddleWorldInteraction();
    return;
}

_puzzleRiddleScreen = screen;
_puzzleRiddleHandle = result.Handle.Value;
```

Do not create `Game.TryHostSurface(...)` in this ticket.

- [ ] **Step 5: Add one idempotent world-interaction cleanup primitive**

Add a private helper with no presentation knowledge:

```csharp
private void EndPuzzleRiddleWorldInteraction()
{
    _activePuzzleRiddle = null;
    if (_gameManager != null &&
        GodotObject.IsInstanceValid(_gameManager) &&
        _gameManager.IsInWorldInteraction)
    {
        _gameManager.EndWorldInteraction();
    }

    if (IsInsideTree())
        UpdateInteractionPrompt();
}
```

Host cleanup becomes:

```csharp
private void ClearPuzzleRiddlePresentation(PuzzleRiddleScreenController screen)
{
    if (GodotObject.IsInstanceValid(screen))
    {
        screen.ChoiceSelected -= OnPuzzleRiddleChoiceSelected;
        screen.PuzzleRiddleClosed -= OnPuzzleRiddleClosed;
    }

    if (ReferenceEquals(_puzzleRiddleScreen, screen))
    {
        _puzzleRiddleScreen = null;
        _puzzleRiddleHandle = null;
    }

    EndPuzzleRiddleWorldInteraction();
}
```

When cleanup runs before `_puzzleRiddleScreen` is retained, `EndPuzzleRiddleWorldInteraction()` still clears the domain latch; the subsequent `IsActive` recheck prevents stale retention.

- [ ] **Step 6: Add stale-safe hosted close**

Implement a riddle-specific close method only:

```csharp
private void ClosePuzzleRiddlePresentation(UIScreenCloseReason reason)
{
    var screen = _puzzleRiddleScreen;
    if (screen == null || !_puzzleRiddleHandle.HasValue ||
        _screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
    {
        EndPuzzleRiddleWorldInteraction();
        return;
    }

    try
    {
        var result = _screenHost.TryClose(_puzzleRiddleHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
            ClearPuzzleRiddlePresentation(screen);
    }
    catch (Exception ex)
    {
        GD.PushError($"[Game] Puzzle riddle close publication failed: {ex.Message}");
        ClearPuzzleRiddlePresentation(screen);
    }
}
```

Do not manually `QueueFree` an actively hosted screen; `UINodeLifetime.QueueFree` belongs to the host.

- [ ] **Step 7: Run hosted-open GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest"
```

Expected: hosted policy/open tests PASS; existing domain tests may still need the result-presentation retarget in Task 3.

Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat: host puzzle riddle from gameplay"
```

---

## Task 3: Preserve result semantics with readable feedback

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Reference: `scripts/game/PuzzleTrapController.cs`

**Interfaces:**

- Consumes: `PuzzleTrapController.TrySolveRiddle(...)` and Task 1 feedback methods.
- Produces: exact existing puzzle mutations plus dormant/terminal presentation transitions.

- [ ] **Step 1: Write RED dormant/wrong/success tests against the real domain route**

Keep the existing real puzzle fixtures and assert the new presentation behavior around the same domain outcomes.

Dormant/unarmed:

```csharp
AssertThat(gameManager.IsInWorldInteraction).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
AssertThat(screen.GetNode<Label>("%FeedbackLabel").Text)
    .IsEqual("The mechanism is dormant.");
```

Wrong answer:

- health decreases by exactly `WrongAnswerDamage` and not below 1;
- puzzle remains unsolved;
- gate remains closed;
- feedback contains `The seal rejects the answer.` and damage;
- choices cannot emit another answer;
- after `Close`, hosted riddle/world interaction end;
- a fresh interaction can open a new riddle attempt.

Success:

- `GameManager.IsPuzzleSolved(puzzleId)` is true exactly once;
- existing gate solved state and grid registration still occur;
- success feedback is `The gate opens.`;
- choices are unavailable;
- `Continue` dismisses presentation without re-applying solved state.

Run those exact test methods and confirm RED.

- [ ] **Step 2: Retarget `OnPuzzleRiddleChoiceSelected` without moving domain logic**

Keep this existing resolution order:

```csharp
var result = _puzzleTrapController.TrySolveRiddle(riddle, choiceId);

if (result.ShouldApplyPenalty)
{
    ApplyPuzzleDamage(riddle.WrongAnswerDamage);
    _gameManager.NotifyPlayerStatsChanged();
}

if (result.Solved)
{
    ApplyPuzzleSolvedState(riddle.PuzzleId);
    _gameManager.NotifyPlayerStatsChanged();
}
```

Then branch presentation only:

```csharp
if (!result.Solved && !result.ShouldApplyPenalty)
{
    _puzzleRiddleScreen?.RearmWithFeedback(result.Message);
    return;
}

if (result.ShouldApplyPenalty)
{
    _puzzleRiddleScreen?.ShowTerminalFeedback(
        $"{result.Message} (-{riddle.WrongAnswerDamage} HP)",
        "Close");
    return;
}

_puzzleRiddleScreen?.ShowTerminalFeedback(result.Message, "Continue");
```

Do not call close automatically for wrong/success; the readable terminal state is dismissed through the existing final-close signal.

- [ ] **Step 3: Fail closed if the active screen/riddle disappears during resolution**

At the top of the handler, if `_activePuzzleRiddle`, `_puzzleTrapController`, or `_puzzleRiddleScreen` is missing/invalid, call `ClosePuzzleRiddlePresentation(UIScreenCloseReason.Programmatic)` / `EndPuzzleRiddleWorldInteraction()` as applicable and return.

Keep the existing broad exception boundary because a presentation failure must not crash gameplay. On exception, log and close the riddle presentation so the world latch cannot remain active.

- [ ] **Step 4: Make `OnPuzzleRiddleClosed` final-only**

Replace native cleanup with:

```csharp
private void OnPuzzleRiddleClosed() =>
    ClosePuzzleRiddlePresentation(UIScreenCloseReason.ExplicitAction);
```

The host Cleanup callback owns final state clearing and prompt restoration.

- [ ] **Step 5: Run domain + real-route GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GameTest"
```

Expected: existing puzzle-domain assertions plus hosted result-state assertions PASS.

Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "feat: present puzzle riddle outcomes"
```

---

## Task 4: Move configured Cancel and teardown to UIScreenHost

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameTest.cs`
- Reference: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**

- Consumes: hosted riddle handle/cleanup from Tasks 2–3.
- Produces: no native-dialog input dependency; teardown-safe final lifecycle.

- [ ] **Step 1: Write RED configured keyboard/controller Cancel tests**

Retarget `ConfiguredKeyboardCancel_ClosesTopmostRiddleAndRestoresGameplay` to assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
// push configured keyboard Cancel
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
```

Add the equivalent configured controller Cancel proof. Use the same physical binding helpers already used by Dialogue tests.

Do not route these through `ui_close_dialog`; the host intercepts configured core Cancel directly.

- [ ] **Step 2: Remove the native riddle special case from root Cancel**

Delete:

```csharp
if (_puzzleRiddleDialog != null && IsInstanceValid(_puzzleRiddleDialog))
    return UIRootCancelResult.Declined;
```

Keep:

```csharp
if (_gameManager.IsInWorldInteraction)
    return UIRootCancelResult.Consumed;
```

The hosted entry receives Cancel before root fallback; the bare world latch remains fail-closed.

- [ ] **Step 3: Add host-teardown cleanup to `Game._ExitTree()`**

Replace native `CleanupPuzzleRiddleDialog(endWorldInteraction: false)` with a hosted close attempt:

```csharp
ClosePuzzleRiddlePresentation(UIScreenCloseReason.HostTeardown);
EndPuzzleRiddleWorldInteraction();
```

Both operations are idempotent. Do not rely on child free order to clear the GameManager latch.

- [ ] **Step 4: Prove synchronous close during publication is not retained**

Use the existing `UIScreenHost.EffectiveStateChanged`/gameplay-block publication seam in a focused fixture: when the PuzzleRiddle entry first publishes, synchronously close it. Invoke the real riddle-open method and assert after `TryPresent` returns:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
AssertThat(GetPrivateField<UIScreenHandle?>(game, "_puzzleRiddleHandle")).IsNull();
AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(game, "_puzzleRiddleScreen")).IsNull();
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
```

This is the regression for the post-commit `IsActive(handle)` recheck. Do not modify `UIScreenHost` to make the test easier.

- [ ] **Step 5: Prove close-publication failure cannot latch world interaction**

Attach a one-shot throwing `EffectiveStateChanged` or configured gameplay-block callback for the close publication, then dismiss the riddle.

Assert the exception is logged/contained by the riddle close path and:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
```

Reuse existing host-test patterns for installing/removing the throwing callback; do not add production hooks.

- [ ] **Step 6: Prove scene/host teardown clears the riddle**

Open a real hosted riddle, call the same teardown route used by scene replacement (`PrepareForTeardown()` or free the Game root as appropriate to the existing fixture), and assert no active PuzzleRiddle entry and no world latch remain.

- [ ] **Step 7: Run lifecycle GREEN and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~UIScreenHostLifecycleTest"
```

Expected: configured keyboard/controller Cancel, publication race/failure, and teardown regressions PASS.

Commit:

```bash
git add scripts/game/Game.cs tests/game/GameInputLifecycleTest.cs tests/game/GameTest.cs
git commit -m "test: harden hosted riddle lifecycle"
```

---

## Task 5: Delete the native riddle path and reconcile lifecycle documentation

**Files:**
- Delete: `scripts/ui/PuzzleRiddleDialog.cs`
- Delete: `tests/ui/PuzzleRiddleDialogTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Verify: `scripts/game/Game.cs`
- Verify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**

- Consumes: completed hosted riddle route.
- Produces: one production riddle presentation path and updated contract evidence.

- [ ] **Step 1: Delete the native dialog and its superseded unit suite**

```bash
git rm scripts/ui/PuzzleRiddleDialog.cs tests/ui/PuzzleRiddleDialogTest.cs
```

Do not leave a wrapper subclass or forwarding adapter.

- [ ] **Step 2: Update HPA-376 `WORLD-RIDDLE`**

Replace native `AcceptDialog` evidence with:

- `Game.OpenPuzzleRiddle` + `PuzzleRiddleScreenController` + `UIScreenHost`;
- hosted Modal/Always/no-tree-pause/block-gameplay policy;
- HUD hidden, cursor visible, first-answer focus;
- host-intercepted configured Cancel;
- dormant in-place rearm;
- wrong/success terminal feedback then final dismissal;
- host Cleanup as the world-interaction convergence point.

Keep the contract statement that puzzle-domain rules remain in `PuzzleTrapController`/`Game`.

- [ ] **Step 3: Update HPA-376 `WORLD-CLEANUP` and evidence names**

Document that host rejection, stale handles, publication exceptions, configured Cancel, and teardown all converge on idempotent `EndWorldInteraction()`.

Replace `PuzzleRiddleDialogTest` evidence with `PuzzleRiddleScreenControllerTest` and the real-route hosted Game/Input tests.

- [ ] **Step 4: Run stale-path searches**

```bash
git grep -n "PuzzleRiddleDialog\|_puzzleRiddleDialog" -- scripts tests scenes docs/ui/hpa-376 || true
```

Expected: no production/test/lifecycle-contract references to the retired riddle dialog.

Check riddle-specific `ui_close_dialog` coupling:

```bash
git grep -n "ui_close_dialog" -- tests/game/GameInputLifecycleTest.cs scripts/game/Game.cs
```

Expected: no riddle-specific native-dialog dispatch remains. Other settings compatibility coverage may remain.

- [ ] **Step 5: Run focused HPA-571 verification**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: PASS.

- [ ] **Step 6: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS with no new failures or riddle-owned orphan warnings.

- [ ] **Step 7: Review the final diff for scope**

```bash
git diff main...HEAD -- \
  scenes/ui/PuzzleRiddleScreen.tscn \
  scripts/ui/PuzzleRiddleScreenController.cs \
  scripts/game/Game.cs \
  tests/ui/PuzzleRiddleScreenControllerTest.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
```

Confirm there are no changes to:

- `PuzzleTrapController.cs`;
- `PuzzleRiddleSpawn.cs`;
- save schema/persistence;
- reward presentation;
- `UIScreenHost` APIs;
- theme tokens.

- [ ] **Step 8: Commit legacy removal and documentation**

```bash
git add -A
git commit -m "refactor: remove native puzzle riddle dialog"
```

---

## Final acceptance checklist

- [ ] `PuzzleRiddleDialog` / `AcceptDialog` riddle path is gone.
- [ ] The new surface is authored in `PuzzleRiddleScreen.tscn` and uses the existing Medium `SiriusModalShell`.
- [ ] `PuzzleTrapController` and `PuzzleRiddleSpawn` have no HPA-571 behavior changes.
- [ ] Switch requirements, answer validation, damage, solved persistence, gate state, and trap behavior match current tests.
- [ ] Dormant feedback rearms the same hosted screen.
- [ ] Wrong answer is readable and remains terminal for that attempt; retry starts a fresh interaction.
- [ ] Success is readable and does not reapply solved state on dismissal.
- [ ] Long prompt/choice content is usable at 640×360 through the shell scroll.
- [ ] First answer / terminal action focus is deterministic for keyboard and gamepad.
- [ ] Configured keyboard and controller Cancel close only the riddle and never open Pause.
- [ ] Rejected/invalid/exception/stale/teardown paths clear world interaction exactly once.
- [ ] HPA-376 documents the hosted route.
- [ ] Focused tests pass.
- [ ] Full suite passes.
