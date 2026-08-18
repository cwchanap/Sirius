# HPA-571 Hosted Puzzle and Riddle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the native puzzle/riddle `AcceptDialog` with a scene-authored, `UIScreenHost`-managed Sirius riddle surface while preserving puzzle rules, answer/Cancel mutual exclusion, and deterministic world-interaction cleanup.

**Architecture:** Keep `Game` as the world-riddle orchestration owner and `PuzzleTrapController` as the domain authority. Add one `PuzzleRiddleScreen.tscn` + `PuzzleRiddleScreenController`, present it through existing `UIScreenKinds.PuzzleRiddle`, and land the final wrong/success acknowledgement behavior in the same compile-complete Game cutover. Use a tiny presentation phase (`AwaitingChoice`, `Resolving`, `Terminal`) so answer resolution cannot race Cancel. No puzzle framework, presenter/view-model, host facade, new host API, or compatibility shim.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusModalShell`, `SiriusInputHint`, `SiriusUiMetrics`, `UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-17-hpa-571-hosted-puzzle-riddle-design.md`

## Global Constraints

- Keep `PuzzleTrapController`, `PuzzleRiddleSpawn`, switch arming, answer validation, persistence, gate/trap behavior, and wrong-answer damage rules unchanged.
- Keep `Game` as the riddle orchestration owner; do not route world riddles through `NpcInteractionController`.
- Add exactly one production scene/controller pair for HPA-571.
- Reuse centred `SiriusModalShell` with `SiriusModalSizeClass.Medium`; no `%SafeFrame`, new theme token, or size metric.
- Keep `%CancelHint` because HPA-373 explicitly requires the active device Cancel hint; mount it in fixed `ActionsHost` chrome beside Cancel.
- Reuse `UIScreenKinds.PuzzleRiddle`; no new kind, exclusive group, parent handle, incompatible-kind rule, or host API.
- Host policy: Modal / Modal priority / Always / no tree pause / block gameplay / cursor visible / HUD hidden / lower layers visible-inert / Cancel consumed + intercepted / QueueFree lifetime.
- Dynamic answer buttons stay controller-local and copy Dialogue's runtime button treatment.
- Runtime answer height comes from `SiriusUiMetrics.MinimumTarget(compact)`: 44 px standard, 40 px compact.
- Prompt and feedback use compact theme variations at compact viewports.
- AwaitingChoice → Resolving → AwaitingChoice or Terminal is presentation state only; do not move puzzle domain state into the UI.
- Dormant/unarmed response rearms the same hosted screen.
- Wrong answer and success land readable terminal feedback in the **same Game cutover task**; do not create an intermediate immediate-close implementation that a later task deletes.
- Wrong-answer text reports actual HP lost after the existing 1-HP floor.
- Do not extract or parameterize `NpcInteractionController.TryHostSurface` in HPA-571. Its `_finished` abandoned-entry recovery is NPC-owner-specific. Keep the narrow Game-shaped host hardening inline; extract only when a second Game-owned surface needs the exact same protocol.
- Root teardown intentionally ends an active riddle world-interaction latch before the Game owner exits; document this as an HPA-571 lifecycle normalization.
- Delete native `PuzzleRiddleDialog` only after hosted coverage is green; keep no compatibility shim.

---

## File Structure

### Create

- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`
- `tests/ui/PuzzleRiddleScreenControllerTest.cs`

### Modify

- `scripts/game/Game.cs`
- `tests/TestHelpers.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete after replacement coverage is green

- `scripts/ui/PuzzleRiddleDialog.cs`
- `tests/ui/PuzzleRiddleDialogTest.cs`

### Reference only unless a focused failure proves otherwise

- `scripts/game/PuzzleTrapController.cs`
- `scripts/game/PuzzleRiddleSpawn.cs`
- `scripts/ui/DialogueScreenController.cs`
- `scripts/ui/HealingScreenController.cs`
- `scripts/ui/NpcInteractionController.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/components/SiriusInputHint.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`

---

## Risk Checklist

### Choice and Cancel both emit while Game resolves

The controller enters `Resolving` before emitting a choice. While resolving, repeated choices and Cancel do nothing. Only `RearmWithFeedback(...)` or `ShowTerminalFeedback(...)` can leave that phase.

### Game cutover compiles but temporarily implements the wrong UX

Do not stage an immediate `RequestCancel()` after answer resolution. Task 2 lands final terminal feedback directly and retargets both Game test suites in the same compile-complete commit.

### Host hardening becomes another abstraction

Use `NpcInteractionController.TryHostSurface` as behavioral reference only. The NPC helper is coupled to `Finish()` and `_finished` recovery. For this one Game-owned surface, keep the smaller Game-specific rejection/exception/IsActive protocol inline.

### Compact answers become undersized or default-themed

Copy Dialogue's `CreateActionButton`, reapply `MinimumTarget` during `RefreshLayout`, and switch prompt/feedback to compact theme variations.

### Root teardown silently changes behavior

Current native `_ExitTree()` skips `EndWorldInteraction`. The hosted migration intentionally normalizes teardown to end the live latch before owner exit; pin it in a focused test and HPA-376.

---

# Task 1: Build the scene-authored riddle screen and phase contract

**Files:**
- Create: `scenes/ui/PuzzleRiddleScreen.tscn`
- Create: `scripts/ui/PuzzleRiddleScreenController.cs`
- Create: `tests/ui/PuzzleRiddleScreenControllerTest.cs`
- Modify: `tests/TestHelpers.cs`
- Reference: `scripts/ui/DialogueScreenController.cs`
- Reference: `scripts/ui/HealingScreenController.cs`

**Interfaces:**

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

Local phase:

```csharp
private enum PuzzleRiddlePresentationPhase
{
    AwaitingChoice,
    Resolving,
    Terminal
}
```

## 1.1 Add shared recursive native-dialog test helper

- [ ] Add this to `tests/TestHelpers.cs`:

```csharp
public static bool ContainsAcceptDialog(Node node)
{
    if (node is AcceptDialog)
        return true;

    foreach (Node child in node.GetChildren())
        if (ContainsAcceptDialog(child))
            return true;

    return false;
}
```

Use it only from the new riddle suite in HPA-571. Do not drive-by migrate the four existing local copies.

## 1.2 Write RED scene/configuration tests

- [ ] Create `PuzzleRiddleScreenControllerTest` with a real `SubViewport` fixture via `TestHelpers.MountInViewport(...)`.

Pin:

```csharp
AssertThat(TestHelpers.ContainsAcceptDialog(screen)).IsFalse();
AssertThat(screen.GetNodeOrNull("%SafeFrame")).IsNull();
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
AssertThat(shell.GetParent()).IsEqual(screen);
AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Medium);
AssertThat(screen.GetNode<Button>("%CancelButton").Visible).IsTrue();
AssertThat(screen.GetNode<SiriusInputHint>("%CancelHint").GetParent())
    .IsEqual(shell.ActionsHost);
```

Also cover:

- `TryOpenRiddle(...)` before `_Ready()` renders after mount;
- second start is rejected;
- blank `RiddleId` renders `Seal`;
- zero choices is rejected;
- first valid answer becomes `InitialFocusTarget`.

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

Expected: RED because the scene/controller do not exist.

## 1.3 Author the stable scene

- [ ] Create:

```text
PuzzleRiddleScreen (Control; full viewport)
└── ModalShell (%ModalShell; Medium)
    ├── .../BodyHost
    │   ├── FeedbackLabel (%FeedbackLabel; hidden; SiriusMetadata)
    │   ├── PromptLabel (%PromptLabel; RichTextLabel; BBCode enabled)
    │   └── ChoicesContainer (%ChoicesContainer; VBoxContainer)
    └── .../ActionsHost
        ├── CancelHint (%CancelHint; SiriusInputHint)
        └── CancelButton (%CancelButton; SiriusSecondaryButton; "Cancel")
```

`%FeedbackLabel` uses Healing-style standing defaults:

```text
visible = false
SiriusMetadata
autowrap = WordSmart
```

Do not add SafeFrame, a second panel, or nested ScrollContainer.

## 1.4 Implement pre-ready binding and responsive layout

- [ ] Bind the scene once in `_Ready()`, subscribe `Resized`, connect Cancel, render stored state, and unsubscribe in `_ExitTree()`.

Use:

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
    foreach (Node child in _choicesContainer.GetChildren())
        if (child is Button button)
            button.CustomMinimumSize = new Vector2(0f, target.Y);

    _shell.RefreshPresentation(size);
}
```

## 1.5 Create Dialogue-style answer buttons and enforce Resolving

- [ ] Copy the local runtime button helper:

```csharp
private static Button CreateActionButton(string text) => new()
{
    Text = text,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
```

- [ ] On an answer press:

```csharp
private void OnChoicePressed(string choiceId)
{
    if (_phase != PuzzleRiddlePresentationPhase.AwaitingChoice || _closedEmitted)
        return;

    _phase = PuzzleRiddlePresentationPhase.Resolving;
    SetChoicesEnabled(false);
    EmitSignal(SignalName.ChoiceSelected, choiceId);
}
```

- [ ] `RequestCancel()` only closes in `AwaitingChoice` or `Terminal`; it ignores `Resolving`.

Pin the existing invariant with:

```csharp
[TestCase]
public void ChoiceThenCancel_WhileResolving_EmitsChoiceOnly()
```

Press a choice, immediately call `RequestCancel()`, and assert one choice / zero close emissions.

This controller test is the deterministic Resolving contract. Do **not** add a later integration test that tries to inject input mid-synchronous Game callback.

## 1.6 Implement dormant rearm and terminal feedback

- [ ] `RearmWithFeedback(...)`:

```csharp
_phase = PuzzleRiddlePresentationPhase.AwaitingChoice;
SetFeedback(message);
SetChoicesVisible(true);
SetChoicesEnabled(true);
_cancelButton.Text = "Cancel";
RestoreChoiceFocus();
```

- [ ] `ShowTerminalFeedback(...)`:

```csharp
_phase = PuzzleRiddlePresentationPhase.Terminal;
SetFeedback(message);
SetChoicesEnabled(false);
SetChoicesVisible(false);
_cancelButton.Text = string.IsNullOrWhiteSpace(actionLabel) ? "Close" : actionLabel;
InitialFocusTarget = _cancelButton;
_cancelButton.GrabFocus();
```

- [ ] Final close remains one-shot through `_closedEmitted`.

Test dormant rearm, wrong `Close`, success `Continue`, repeat close, and no answer emissions from Terminal.

## 1.7 Prove compact target/text/scroll behavior

- [ ] Mount a long riddle at 640×360.

Assert:

```csharp
AssertThat(shell.Compact).IsTrue();
AssertThat(prompt.ThemeTypeVariation).IsEqual(SiriusThemeTypes.BodyCompact);
AssertThat(feedback.ThemeTypeVariation).IsEqual(SiriusThemeTypes.MetadataCompact);
AssertThat(answer.ThemeTypeVariation).IsEqual(SiriusThemeTypes.SecondaryButton);
AssertThat(answer.CustomMinimumSize.Y)
    .IsEqual(SiriusUiMetrics.MinimumTarget(true).Y);
AssertThat(shell.GetNode<ScrollContainer>("%BodyScroll").FollowFocus).IsTrue();
```

Focus the final answer, await layout frames, and assert the shell body can scroll to keep it reachable. Do not introduce a second scroll owner.

## 1.8 Run Task 1 GREEN and commit

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleRiddleDialogTest"
```

Expected before legacy deletion: new screen tests PASS and native dialog tests still PASS.

```bash
git add scenes/ui/PuzzleRiddleScreen.tscn \
  scripts/ui/PuzzleRiddleScreenController.cs \
  tests/ui/PuzzleRiddleScreenControllerTest.cs \
  tests/TestHelpers.cs
git commit -m "feat: add hosted puzzle riddle screen"
```

---

# Task 2: Perform one compile-complete Game cutover with final result UX

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Reference: `scripts/game/PuzzleTrapController.cs`
- Reference: `scripts/ui/NpcInteractionController.cs`

**Produces:** final hosted riddle route, including wrong/success acknowledgement. There is no temporary immediate-close implementation.

## 2.1 Write RED hosted-open/policy test

- [ ] Add a real Game-scene test that opens a riddle and asserts one active host entry:

```csharp
var host = game.GetNode<UIScreenHost>("UI/UIScreenHost");
var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.PuzzleRiddle);

AssertThat(entry.Policy.Layer).IsEqual(UIScreenLayer.Modal);
AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(entry.Policy.PauseTree).IsFalse();
AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
AssertThat(gameManager.IsInWorldInteraction).IsTrue();
```

Run the focused test and confirm RED against the native route.

## 2.2 Replace native Game fields and every test field/type lookup together

- [ ] Replace:

```csharp
private PuzzleRiddleDialog? _puzzleRiddleDialog;
```

with:

```csharp
private PuzzleRiddleScreenController? _puzzleRiddleScreen;
private UIScreenHandle? _puzzleRiddleHandle;
```

Keep `_activePuzzleRiddle` and `_puzzleTrapController` in Game.

- [ ] In the **same edit**, retarget every `_puzzleRiddleDialog` / `PuzzleRiddleDialog` reflection lookup in both `GameTest.cs` and `GameInputLifecycleTest.cs` to the new screen field/type. Do not leave a compile-broken intermediate commit.

## 2.3 Implement hosted `OpenPuzzleRiddle` inline

- [ ] Preserve blank-id and already-solved guards.
- [ ] Require a live `_screenHost` before `StartWorldInteraction()`.
- [ ] Load/instantiate/configure `PuzzleRiddleScreen.tscn`.
- [ ] Subscribe `ChoiceSelected` / `PuzzleRiddleClosed`.
- [ ] Set `_activePuzzleRiddle`, start world interaction, refresh prompt.
- [ ] Call `TryPresent(...)` with the final policy from the spec.

Keep the narrow hardening inline:

```csharp
UIScreenOpenResult openResult;
try
{
    openResult = _screenHost.TryPresent(screen, spec);
}
catch (Exception ex)
{
    GD.PushError($"[Game] Failed to host puzzle riddle '{riddle.RiddleId}': {ex}");
    ClearRejectedPuzzleRiddleCandidate(screen);
    EndWorldInteractionIfActive();
    UpdateInteractionPrompt();
    return;
}

if (openResult.Status != UIScreenOpenStatus.Opened || !openResult.Handle.HasValue)
{
    ClearRejectedPuzzleRiddleCandidate(screen);
    EndWorldInteractionIfActive();
    UpdateInteractionPrompt();
    return;
}

if (!_screenHost.IsActive(openResult.Handle.Value))
{
    // A publication subscriber may already have closed the committed entry;
    // its Cleanup callback has released the candidate. Retain nothing and only
    // ensure the world latch is down.
    EndWorldInteractionIfActive();
    UpdateInteractionPrompt();
    return;
}

_puzzleRiddleScreen = screen;
_puzzleRiddleHandle = openResult.Handle.Value;
```

`ClearRejectedPuzzleRiddleCandidate` is used only for candidate views that were not already cleaned by a committed host close. It unsubscribes the candidate's signals and frees it if still valid; it is riddle-local, not a generic host service.

Do not change `NpcInteractionController.TryHostSurface`.

## 2.4 Replace native cleanup with hosted clear/close

- [ ] Add an idempotent helper:

```csharp
private void EndWorldInteractionIfActive()
{
    if (_gameManager != null &&
        GodotObject.IsInstanceValid(_gameManager) &&
        _gameManager.IsInWorldInteraction)
    {
        _gameManager.EndWorldInteraction();
    }
}
```

- [ ] Add `ClearPuzzleRiddlePresentation(PuzzleRiddleScreenController screen)` to unsubscribe signals, clear matching screen/handle/active-riddle state, end the world interaction if active, and refresh prompt when still in tree.

- [ ] Add one riddle-specific close wrapper around `UIScreenHost.TryClose(...)`; `StaleHandle` converges on `ClearPuzzleRiddlePresentation(screen)`.

Delete calls to `CleanupPuzzleRiddleDialog(...)` in this same cutover.

## 2.5 Land final answer behavior directly

- [ ] Keep the domain call/order in `OnPuzzleRiddleChoiceSelected`:

```csharp
var result = _puzzleTrapController.TrySolveRiddle(riddle, choiceId);
```

For wrong answer:

```csharp
var healthBefore = _gameManager.Player.CurrentHealth;
ApplyPuzzleDamage(riddle.WrongAnswerDamage);
_gameManager.NotifyPlayerStatsChanged();
var healthLost = healthBefore - _gameManager.Player.CurrentHealth;

screen.ShowTerminalFeedback(
    $"{result.Message} (-{healthLost} HP)",
    "Close");
```

For solve:

```csharp
ApplyPuzzleSolvedState(riddle.PuzzleId);
_gameManager.NotifyPlayerStatsChanged();
screen.ShowTerminalFeedback(result.Message, "Continue");
```

For dormant/unarmed:

```csharp
screen.RearmWithFeedback(result.Message);
```

Do **not** call `RequestCancel()` after success or wrong answer. The player-visible terminal result is the final HPA-571 behavior from this first Game cutover.

## 2.6 Retarget real Game tests to final behavior

- [ ] Update the existing solve fixture:
  - answer solves puzzle and opens gate immediately;
  - hosted riddle remains active in Terminal;
  - feedback contains the success message;
  - `Continue`/`RequestCancel()` then closes it;
  - only after dismissal is `IsInWorldInteraction == false`.

- [ ] Update wrong-answer fixture:
  - HP drops by exactly existing domain behavior;
  - puzzle remains unsolved;
  - feedback reports actual loss;
  - screen remains Terminal until `Close`;
  - after dismissal, fresh interaction opens a new screen and retry remains possible.

- [ ] Update dormant fixture:
  - same `_puzzleRiddleScreen` instance/host handle stays active;
  - feedback contains `dormant`;
  - world interaction stays active;
  - another choice is accepted after rearm.

## 2.7 Retarget configured Cancel test so the assembly compiles and behavior is hosted

- [ ] In `GameInputLifecycleTest`, replace native window setup/type lookup with:

```csharp
var host = _realGame.GetNode<UIScreenHost>("UI/UIScreenHost");
InvokePrivate(_realGame, "OpenPuzzleRiddle", riddle);
var screen = GetPrivateField<PuzzleRiddleScreenController>(
    _realGame, "_puzzleRiddleScreen");
```

Keep the existing configured-key Cancel flow, but assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsTrue();
// push configured Cancel
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
```

## 2.8 Run compile-complete GREEN and commit

```bash
dotnet build Sirius.sln --no-restore --nologo

dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest"
```

Expected: build succeeds and the final hosted success/wrong/dormant/Cancel behavior is green. No throwaway immediate-close assertions exist.

```bash
git add scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs
git commit -m "feat: host puzzle riddles through gameplay UI"
```

---

# Task 3: Harden host failure and teardown boundaries

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

Do not add an integration test for Cancel during the synchronous `Resolving` call chain; Task 1 already pins that invariant deterministically.

## 3.1 Missing/unavailable host fails before world latch starts

- [ ] Add a synthetic Game fixture without `UIScreenHost` and invoke `OpenPuzzleRiddle`.

Assert:

```csharp
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(game, "_puzzleRiddleScreen"))
    .IsNull();
```

## 3.2 Publication subscriber synchronously closes the newly opened entry

- [ ] Add a real-host regression modeled on the existing host liveness contract:

```csharp
void CloseRiddleDuringPublication(UIScreenEffectiveState _)
{
    var entry = host.ActiveEntries
        .FirstOrDefault(e => e.Policy.Kind == UIScreenKinds.PuzzleRiddle);
    if (entry != null)
        host.TryClose(entry.Handle, UIScreenCloseReason.Programmatic);
}

host.EffectiveStateChanged += CloseRiddleDuringPublication;
try
{
    InvokePrivate(game, "OpenPuzzleRiddle", riddle);
}
finally
{
    host.EffectiveStateChanged -= CloseRiddleDuringPublication;
}
```

Assert no screen/handle is retained and `IsInWorldInteraction == false`.

This test proves the post-`Opened` `IsActive(handle)` recheck.

## 3.3 Publication exception cannot strand world interaction

- [ ] Subscribe a throwing `EffectiveStateChanged` handler before opening:

```csharp
Action<UIScreenEffectiveState> throwing = _ =>
    throw new InvalidOperationException("fixture publication failure");

host.EffectiveStateChanged += throwing;
try
{
    InvokePrivate(game, "OpenPuzzleRiddle", riddle);
}
finally
{
    host.EffectiveStateChanged -= throwing;
}
```

`OpenPuzzleRiddle` catches/logs the publication failure. The host may already have committed the candidate; freeing the still-valid candidate triggers host `NodeFreed` cleanup just as the existing NPC recovery relies on. Then the Game-specific catch ensures the world latch is down.

Assert:

```csharp
AssertThat(gameManager.IsInWorldInteraction).IsFalse();
AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(game, "_puzzleRiddleScreen"))
    .IsNull();
AssertThat(host.IsKindActive(UIScreenKinds.PuzzleRiddle)).IsFalse();
```

Do not generalize this into a new Game host helper in HPA-571.

## 3.4 Root teardown intentionally ends the world latch

- [ ] Use a real Game scene. Open a hosted riddle and confirm the latch is active, then invoke the production teardown method directly while the child `GameManager` is still valid:

```csharp
InvokePrivate(game, "OpenPuzzleRiddle", riddle);
AssertThat(gameManager.IsInWorldInteraction).IsTrue();

game._ExitTree();

AssertThat(gameManager.IsInWorldInteraction).IsFalse();
AssertThat(GetPrivateField<PuzzleRiddleScreenController?>(game, "_puzzleRiddleScreen"))
    .IsNull();
```

The fixture's normal cleanup may call `_ExitTree()` again when it frees the scene; the production cleanup must therefore remain idempotent. Do not introduce a test-only production seam for this assertion.

## 3.5 Controller/gamepad configured Cancel uses the same hosted close path

- [ ] Add/retarget the controller binding equivalent of the keyboard test using the existing `ConfigureCancelBindings` / joypad fixture helpers.

Assert one hosted close, no Pause, world latch false.

## 3.6 Run Task 3 GREEN and commit

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

```bash
git add scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs
git commit -m "test: harden hosted riddle lifecycle"
```

---

# Task 4: Delete the native dialog, reconcile HPA-376, and verify the branch

**Files:**
- Delete: `scripts/ui/PuzzleRiddleDialog.cs`
- Delete: `tests/ui/PuzzleRiddleDialogTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify as required for stale references: `scripts/game/Game.cs`, `tests/game/GameTest.cs`, `tests/game/GameInputLifecycleTest.cs`

## 4.1 Delete native files with no shim

- [ ] Delete `PuzzleRiddleDialog.cs` and its dedicated test after Tasks 1–3 are green.
- [ ] Keep no alias, wrapper, subclass, or compatibility path.

## 4.2 Reconcile `WORLD-RIDDLE`

- [ ] Update the HPA-376 row to describe:

- `PuzzleRiddleScreenController` hosted as `UIScreenKinds.PuzzleRiddle`;
- `PauseTree = false`, gameplay blocked, HUD hidden, cursor visible;
- host-intercepted configured Cancel;
- `AwaitingChoice → Resolving → AwaitingChoice|Terminal` presentation phase;
- dormant same-screen rearm;
- wrong/success terminal acknowledgement before close;
- exactly-once close/world restoration.

Reference the new controller tests and retargeted Game/Input lifecycle tests.

## 4.3 Reconcile `WORLD-CLEANUP` and name the teardown change

- [ ] Explicitly record that HPA-571 changes Game root teardown from native `CleanupPuzzleRiddleDialog(endWorldInteraction: false)` to hosted idempotent cleanup that ends an active riddle world-interaction latch before the Game owner exits.

Do not describe this as an autoload leak fix: `GameManager` is scene-local and clears its singleton instance during its own `_ExitTree()`.

## 4.4 Run stale-reference audit

```bash
rg -n "PuzzleRiddleDialog|_puzzleRiddleDialog|CleanupPuzzleRiddleDialog" \
  scripts scenes tests docs/ui/hpa-376
```

Expected: zero active implementation/test references. Historical planning docs outside the active lifecycle contract may retain historical names.

Also verify no new framework symbols:

```bash
rg -n "class .*Puzzle.*Presenter|class .*Puzzle.*ViewModel|GameTryHostSurface|HostLifecycleHelper" \
  scripts tests
```

Expected: no HPA-571 abstraction additions.

## 4.5 Run focused and full verification

```bash
dotnet build Sirius.sln --no-restore --nologo

dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~PuzzleTrapControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest"

dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo

git diff --check
git status --short
```

Expected:

- build: 0 errors;
- focused suites: PASS;
- full suite: PASS;
- `git diff --check`: clean;
- only HPA-571 production/test/docs files are modified.

## 4.6 Commit cleanup

```bash
git add -A scripts/ui/PuzzleRiddleDialog.cs \
  tests/ui/PuzzleRiddleDialogTest.cs \
  scripts/game/Game.cs \
  tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md

git commit -m "refactor: remove native puzzle riddle dialog"
```

---

## Final Acceptance Checklist

- [ ] Puzzle/riddle no longer uses `AcceptDialog` or direct native-window presentation.
- [ ] One `PuzzleRiddleScreenController` remains presentation-only.
- [ ] `Game` remains world-riddle orchestration owner.
- [ ] `PuzzleTrapController` remains the answer/domain authority.
- [ ] Medium centred shell, no SafeFrame, fixed Cancel hint/action chrome.
- [ ] Runtime answers use Sirius SecondaryButton, WordSmart wrapping, ExpandFill, and standard/compact minimum targets.
- [ ] Compact prompt/feedback theme variations are applied.
- [ ] Choice and Cancel are mutually exclusive while Resolving.
- [ ] Dormant result rearms the same hosted screen.
- [ ] Wrong answer applies existing damage, reports actual HP lost, remains unsolved, then requires dismissal + fresh interaction to retry.
- [ ] Success applies solved state before `Continue` acknowledgement.
- [ ] Configured keyboard/controller Cancel closes only the hosted riddle and never opens Pause.
- [ ] Host rejection, post-commit synchronous close, publication exception, and root teardown cannot leave an active world interaction.
- [ ] Root teardown cleanup change is documented explicitly in HPA-376.
- [ ] Native dialog/tests are deleted with no shim.
- [ ] Focused tests, full tests, build, stale-reference audit, and diff check pass.

## Review Disposition

The second follow-up review was checked against current `main`:

- **Accepted:** merge the old Tasks 2+3 into one final-UX Game cutover; add `TestHelpers.ContainsAcceptDialog`; name the `_ExitTree` world-latch completion as an intentional cleanup normalization; remove the unreachable mid-synchronous-resolve integration test; add compact prompt/feedback theme variations.
- **Not adopted:** moving `NpcInteractionController.TryHostSurface` into a cross-class helper or creating a one-call-site private `Game.TryHostSurface`. The existing helper has NPC `_finished` semantics beyond the Game requirement, so HPA-571 keeps only the proven Game-shaped subset inline.
