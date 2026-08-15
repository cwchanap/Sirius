# HPA-572 Host-Managed Prompts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's duplicate confirmation scenes and remaining Main Menu/gameplay native error dialogs with one scene-authored modal prompt component while preserving domain ownership, parent retention, paused-tree input, focus, Cancel priority, guaranteed corrupted-save exit, and teardown behavior.

**Architecture:** Add one reusable `SiriusPrompt` component beside `SiriusModalShell` and `SiriusToastShell`. `Game` and `MainMenu` keep local `UIScreenHost` registration, prompt handles, domain closures, and explicit host specs; no prompt service, presenter, queue, singleton, router, `SpecFor(...)` factory, or host facade is introduced. Ship only the three presentation variants used by current consumers; corrupted-save blocking remains a host policy, not a prompt variant.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and `UIScreenHost`.

## Global Constraints

- Support exactly `DestructiveConfirmation`, `Warning`, and `RecoverableError`.
- Do not add `InformationalConfirmation` until a real two-button Info caller exists.
- Do not add `BlockingError`; corrupted-save uses `RecoverableError` plus `BlockGameplayInput = true`.
- Reuse `SiriusModalShell`, `SiriusUiSeverity`, current button variations, `SiriusUiMetrics.MinimumTarget(...)`, and `UIScreenExclusiveGroups.BlockingPrompt`.
- `SiriusContextPrompt` remains a HUD input-hint component; do not reuse or modify it.
- `SiriusPrompt` owns chrome and one-shot terminal intent only. `Game` / `MainMenu` own domain closures and host handles.
- Every Prompt entry uses `Cancel = UICancelPolicy.Consume` plus `InterceptCancel -> prompt.RequestCancel()`; never use `Cancel = Close` for this component.
- Do not move host-spec construction into `SiriusPrompt`; root integration tests pin the common policy instead.
- `DestructiveConfirmation` focuses Cancel. One-action variants focus Primary.
- Child prompts keep their logical parent active/inert and rely on host focus restoration.
- Failed Save/Load keeps active Save/Load open. Missing expected gameplay Save/Load parent is a logged error + `false`, not a silent no-op or native fallback.
- Pause -> Save/Load -> Prompt must remain dismissible while `SceneTree.Paused == true`; Prompt uses `UIProcessPolicy.Always`.
- Corrupted-save leaves `Game` input processing enabled and acquires only the host gameplay-block lease.
- If corrupted-save Prompt presentation fails, fallback immediately to `ReturnToMainMenu()`; never strand the half-loaded Game.
- Root terminal handlers capture the domain closure, attempt Prompt close, then invoke the captured closure **regardless of close status**.
- Collapse product prompt kinds to `UIScreenKinds.Prompt`; host unit tests that need distinct kinds use file-local `StringName`s.
- Do not migrate Dialogue, Shop, Heal, Puzzle/Riddle, or Reward presentation.
- Add no Theme/icon/metric, `SiriusModalShell`, or `UIScreenHost` API unless a focused RED test proves a shared defect.
- No compatibility shims for deleted prompt scenes/controllers/kinds.

---

## File Map

### Create

- `scenes/ui/components/SiriusPrompt.tscn`
- `scripts/ui/components/SiriusPrompt.cs`
- `tests/ui/components/SiriusPromptTest.cs`

### Modify — production/integration

- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/game/Game.cs`
- `scripts/ui/MainMenu.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameTest.cs`
- `tests/ui/MainMenuTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Modify — host fixture identities

- `tests/ui/hosting/UIScreenStackModelTest.cs`
- `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- `tests/ui/hosting/UIScreenHostInputTest.cs`
- `tests/ui/hosting/UIScreenHostFocusTest.cs`
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- `tests/ui/hosting/UIScreenHostProcessModeTest.cs`

### Delete after equivalent coverage exists

- `scenes/ui/SaveOverwriteConfirmation.tscn`
- `scripts/ui/SaveOverwriteConfirmationController.cs`
- `tests/ui/SaveOverwriteConfirmationControllerTest.cs`
- `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

### Audit-only

- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/components/SiriusContextPrompt.cs`
- `scripts/ui/theme/SiriusUiTypes.cs`
- `scripts/ui/theme/SiriusThemeTypes.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenEntrySpec.cs`
- `scripts/ui/hosting/UIScreenInputDispatcher.cs`

Do not change audit-only files without a focused failing regression.

---

## Risks and mitigations

### Corrupted-save presentation failure can strand the player

**Risk:** `_hasShownCorruptedSaveError` is set before presentation. If hosting fails and no fallback runs, later detections are suppressed and the partially initialized Game remains active.

**Mitigation:** `ShowCorruptedSaveError()` falls back to `ReturnToMainMenu()` when `TryOpenHostedPrompt(...)` returns false. `CorruptedSave_PresentationFailureStillReturnsToTitle` pins this using a synthetic Game without a production host.

### Terminal latch can suppress domain work if action is gated on close status

**Risk:** `SiriusPrompt` consumes its one terminal intent before emitting, while `TryClose(...)` may report `AlreadyClosed`, `StaleHandle`, or `HostTearingDown` under races/teardown.

**Mitigation:** handlers capture the closure, call `TryCloseHostedPrompt(...)`, then invoke the closure unconditionally. Existing scene-transition/save guards protect duplicate domain effects.

### Prompt -> Prompt transition is new

**Risk:** overwrite Primary closes one Prompt, save eligibility can change before the press is handled, and the save path can immediately open a recoverable Prompt. Opening while the host is still draining would return `HostMutating`; opening before cleanup would also hit the single `Prompt` kind guard.

**Mitigation:** `TryClose(...)` drains synchronously before returning; domain closure runs afterward. Task 3B pins the chain by opening overwrite, starting NPC interaction, then pressing Primary so `OnHostedSaveSlotSelected` immediately follows its existing “cannot save during NPC interaction” error branch and must open a new recoverable Prompt under retained Save/Load.

### Root host-policy copies can drift

**Risk:** `Game` and `MainMenu` both hand-author `Cancel = Consume` + interception.

**Mitigation:** keep ownership local instead of adding a two-caller policy factory. Both root integration suites assert `ActiveEntries[...] .Policy.Cancel == UICancelPolicy.Consume` and exercise configured Cancel through the Prompt latch.

### Parent retention changes legacy sequencing

**Risk:** legacy paths close Save/Load before errors and Main Menu defers the later message open.

**Mitigation:** parent-retention tests assert Save/Load stays active; paused-tree tests prove Prompt `Always` processing; Main Menu opens the child synchronously because the preceding close is removed.

---

## Task 1: Add the shared `SiriusPrompt` component

**Files:**
- Create: `scenes/ui/components/SiriusPrompt.tscn`
- Create: `scripts/ui/components/SiriusPrompt.cs`
- Create: `tests/ui/components/SiriusPromptTest.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`

**Produces:**

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

Also add `UIScreenKinds.Prompt = "prompt"`; keep old prompt kinds until Task 4.

- [ ] **Step 1: Write RED variant/terminal tests**

Create `tests/ui/components/SiriusPromptTest.cs` using the same `SubViewportContainer` + `SubViewport` style as other component tests. Default to `640x360`; provide a resize helper for `1280x720` that awaits two process frames.

Add:

```text
Variants_MapSeverityButtonsThemeAndInitialFocus
Destructive_PrimaryThenCancelEmitsOnlyPrimaryOnce
Destructive_CancelThenPrimaryEmitsOnlyCancelOnce
Warning_RequestCancelEmitsPrimaryOnce
RecoverableError_RequestCancelEmitsPrimaryOnce
RepeatedPrimaryPress_EmitsOnce
CompactViewport_UsesCompactShellAndMinimumTargets
CrossingCompactToStandard_RefreshesShellAndTargets
LongMessage_MinimumViewportStaysInsideShellAndCanScroll
Scene_UsesModalShellAndContainsNoAcceptDialog
```

Use:

```csharp
var cases = new[]
{
    (SiriusPromptVariant.DestructiveConfirmation,
        SiriusUiSeverity.Warning, true, SiriusThemeTypes.DestructiveButton),
    (SiriusPromptVariant.Warning,
        SiriusUiSeverity.Warning, false, SiriusThemeTypes.PrimaryButton),
    (SiriusPromptVariant.RecoverableError,
        SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton)
};
```

For destructive assert `%CancelButton.Visible` and `InitialFocusTarget == %CancelButton`; otherwise assert Cancel hidden and initial focus equals `%PrimaryButton`.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest"
```

Expected: compile/test failure because the enum/class/scene do not exist.

- [ ] **Step 3: Author the scene**

```text
SiriusPrompt (Control, full rect)
└── ModalShell (SiriusModalShell, unique)
    └── Panel/Margin/RootLayout
        ├── BodyScroll/BodyHost
        │   └── Message (Label, unique, SiriusBody, word-smart wrap)
        └── ActionsHost
            ├── CancelButton (Button, unique, SiriusSecondaryButton)
            └── PrimaryButton (Button, unique, SiriusPrimaryButton)
```

Author both buttons at 44px minimum height for a safe default editor state.

- [ ] **Step 4: Implement stored configuration**

```csharp
private SiriusPromptVariant _variant = SiriusPromptVariant.Warning;
private string _title = "Notice";
private string _message = string.Empty;
private string _primaryActionText = "OK";
private string _cancelActionText = "Cancel";
private bool _terminalEmitted;

private SiriusModalShell _shell = null!;
private Label _messageLabel = null!;
private Button _primary = null!;
private Button _cancel = null!;

public Control InitialFocusTarget =>
    _variant == SiriusPromptVariant.DestructiveConfirmation ? _cancel : _primary;
```

`Configure(...)` stores values and refreshes when ready. `_Ready()` binds `%ModalShell`, `%Message`, `%PrimaryButton`, `%CancelButton`, subscribes button/resize handlers, and refreshes.

- [ ] **Step 5: Implement exact visual mapping**

```csharp
private static SiriusUiSeverity SeverityFor(SiriusPromptVariant variant) => variant switch
{
    SiriusPromptVariant.DestructiveConfirmation => SiriusUiSeverity.Warning,
    SiriusPromptVariant.Warning => SiriusUiSeverity.Warning,
    SiriusPromptVariant.RecoverableError => SiriusUiSeverity.Error,
    _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
};

private static StringName PrimaryThemeFor(SiriusPromptVariant variant) =>
    variant == SiriusPromptVariant.DestructiveConfirmation
        ? SiriusThemeTypes.DestructiveButton
        : SiriusThemeTypes.PrimaryButton;
```

In `RefreshPresentation()` derive compact from the viewport, assign shell title/severity/compact, message text, button text/visibility/themes, and both action heights from `SiriusUiMetrics.MinimumTarget(compact)`; call `_shell.RefreshPresentation(size)`.

- [ ] **Step 6: Implement one-shot terminal behavior**

```csharp
private void EmitPrimaryOnce()
{
    if (_terminalEmitted) return;
    _terminalEmitted = true;
    EmitSignal(SignalName.PrimaryRequested);
}

private void EmitCancelOnce()
{
    if (_terminalEmitted) return;
    _terminalEmitted = true;
    EmitSignal(SignalName.CancelRequested);
}

public void RequestCancel()
{
    if (_variant == SiriusPromptVariant.DestructiveConfirmation)
        EmitCancelOnce();
    else
        EmitPrimaryOnce();
}
```

Wire Primary/Cancel buttons to the two methods. `_ExitTree()` unsubscribes handlers. Never reset `_terminalEmitted`.

- [ ] **Step 7: Run GREEN and shared-component regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~SiriusModalShellTest|FullyQualifiedName~SiriusContextPromptTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors with audit-only files unchanged.

- [ ] **Step 8: Commit Task 1**

```bash
git add scenes/ui/components/SiriusPrompt.tscn scripts/ui/components/SiriusPrompt.cs \
  tests/ui/components/SiriusPromptTest.cs scripts/ui/hosting/UIScreenKinds.cs
git commit -m "feat(ui): add shared Sirius prompt"
```

---

## Task 2: Replace the two dedicated destructive confirmations

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete the six SaveOverwrite/PauseReturn confirmation scene/controller/test files listed in the File Map.

**Produces Game-local seam:**

```csharp
private bool TryOpenHostedPrompt(
    SiriusPromptVariant variant,
    string title,
    string message,
    string primaryActionText,
    Action? onPrimary = null,
    string cancelActionText = "Cancel",
    Action? onCancel = null,
    UIScreenHandle? parent = null,
    Control? restoreFocus = null,
    bool blockGameplayInput = false);

private UIScreenHandle? _hostedPromptHandle;
private SiriusPrompt? _hostedPrompt;
private Action? _hostedPromptPrimaryAction;
private Action? _hostedPromptCancelAction;
```

- [ ] **Step 1: Write RED host tests**

Add/replace in `GameplayPauseHostTest.cs`:

```text
HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad
HostedOverwrite_PrimaryInvokesSaveOnce
PauseReturnToTitle_UsesSharedPromptAndCancelRestoresPause
PauseReturnToTitle_PrimaryRequestsNavigationOnce
HostedPrompt_ProgrammaticCloseClearsHandleAndGroup
HostedPrompt_ParentCloseClearsDescendantReferences
```

For active Prompt entries assert:

```csharp
var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
```

Update the overwrite configured-Cancel test in `GameInputLifecycleTest.cs` to expect one `UIScreenKinds.Prompt` child. One configured Cancel closes only Prompt; Save/Load and Pause remain active.

`PauseReturnToTitle_PrimaryRequestsNavigationOnce` is a **new guarantee**: the legacy Pause controller has no latch. Emit Primary twice and assert only one navigation request reaches the Game seam.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.HostedOverwrite|FullyQualifiedName~GameplayPauseHostTest.PauseReturnToTitle|FullyQualifiedName~GameplayPauseHostTest.HostedPrompt|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_SaveLoadOverwrite"
```

Expected: FAIL because production still uses dedicated confirmation scenes/kinds and Pause lacks one-shot terminal behavior.

- [ ] **Step 3: Implement `TryOpenHostedPrompt` with the existing host contract**

Load `res://scenes/ui/components/SiriusPrompt.tscn`, configure the prompt, store root closures, subscribe signals, and present exactly as:

```csharp
var result = _screenHost.TryPresent(prompt, new UIScreenEntrySpec
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
});
```

Reject missing/invalid host, committed teardown, an active Prompt handle, or `IsKindActive(UIScreenKinds.Prompt)`. On failed registration disconnect/clear only this candidate and queue it directly.

Do **not** extract this spec to `SiriusPrompt.SpecFor(...)`; Main Menu will hand-author the same pairing and test it independently.

- [ ] **Step 4: Implement terminal handlers without close-status gating**

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

`ClearHostedPrompt(SiriusPrompt prompt)` unsubscribes and clears handle/view/actions only for the currently-owned prompt. Capture before close is mandatory because successful cleanup runs synchronously. Invocation after the close attempt is mandatory because the component latch has already spent the terminal intent.

- [ ] **Step 5: Replace Save overwrite**

```csharp
private void OnHostedOverwriteRequested(int slot)
{
    if (!_hostedSaveLoadHandle.HasValue)
        return;

    TryOpenHostedPrompt(
        SiriusPromptVariant.DestructiveConfirmation,
        "Overwrite Save?",
        $"Slot {slot + 1} already contains save data. Overwrite it?",
        "Overwrite",
        onPrimary: () => OnHostedSaveSlotSelected(slot),
        parent: _hostedSaveLoadHandle);
}
```

Cancel has no domain closure. The slot remains captured by `Game`, not the component.

The shared Prompt deliberately renders the primary with `SiriusDestructiveButton`. This changes overwrite from its current `SiriusWarningButton` variation and aligns it with Return to Title; treat that as intended visual normalization.

- [ ] **Step 6: Replace Pause return-to-title**

```csharp
private void OnHostedPauseReturnToTitleRequested()
{
    if (!_pauseHandle.HasValue)
        return;

    TryOpenHostedPrompt(
        SiriusPromptVariant.DestructiveConfirmation,
        "Return to Title?",
        "Unsaved progress will be lost.",
        "Return to Title",
        onPrimary: ReturnToMainMenu,
        parent: _pauseHandle);
}
```

`ReturnToMainMenu()` remains teardown-safe. The shared Prompt supplies the one-shot latch missing from the legacy Pause controller.

- [ ] **Step 7: Delete dedicated confirmation implementations**

Delete all six scene/controller/test files. Move reusable layout/latch assertions into `SiriusPromptTest`; keep host/domain behavior in gameplay tests. No compatibility wrapper.

- [ ] **Step 8: Run GREEN/build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors; no references to deleted confirmation classes/scenes remain.

- [ ] **Step 9: Commit Task 2**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scenes/ui/SaveOverwriteConfirmation.tscn scripts/ui/SaveOverwriteConfirmationController.cs tests/ui/SaveOverwriteConfirmationControllerTest.cs \
  scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "refactor(ui): share destructive prompts"
```

---

## Task 3A: Migrate Main Menu warnings and recoverable errors

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `tests/ui/MainMenuTest.cs`

**Produces:** MainMenu-local Prompt fields/helper with the same component and host-policy semantics as Game, but no shared hosting class/factory.

- [ ] **Step 1: Write RED root and parent-retention tests**

Add/replace:

```text
LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus
LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog
ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsPrompt
HostedLoadFailure_KeepsLoadParentAfterAcknowledge
RootPrompt_ConfiguredCancelUsesPromptLatchAndRestoresRoot
RootPrompt_TerminalCannotRunTwice
```

For active Prompt assert:

```csharp
var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
```

For Load-parent tests assert SaveLoad + Prompt active while error is shown, then Prompt gone and SaveLoad still active after acknowledgement.

For root prompts assert root actions become unavailable while Prompt is active and become available again after cleanup, preserving the current `RefreshActionAvailability()` behavior.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest"
```

Expected: new Prompt tests FAIL against native `AcceptDialog` and close+defer behavior.

- [ ] **Step 3: Replace Main Menu native message state**

Replace `_messageDialog : AcceptDialog` and `_messageCloseDelegate` with Prompt view/action fields. Keep `_messageHandle` root-owned.

Load `res://scenes/ui/components/SiriusPrompt.tscn`, configure `Warning` / `RecoverableError`, and present with the same explicit common host policy from Task 2, including:

```csharp
Cancel = UICancelPolicy.Consume,
InterceptCancel = _ =>
{
    prompt.RequestCancel();
    return UIInputInterception.ConsumeHere;
},
```

One-action prompts route both visible Primary and configured Cancel through Primary terminal handling.

Use capture -> close attempt -> invoke unconditionally if a Main Menu domain closure is ever supplied; do not gate on close result.

- [ ] **Step 4: Preserve action-availability refreshes**

After successful Prompt open, call:

```csharp
RefreshActionAvailability();
```

`ClearHostedMessage(...)` must also call `RefreshActionAvailability()` after clearing Prompt state. Do not add another root-block flag: `IsRootActionBlocked()` already reads `_screenHost.ActiveEntries.Count`.

- [ ] **Step 5: Keep hosted Load active on failure**

Change `OnHostedLoadSlotSelected` failure from close + deferred root message to synchronous child Prompt:

```csharp
if (saveData == null || manager == null)
{
    if (_loadHandle.HasValue)
    {
        TryOpenMessage(
            SiriusPromptVariant.RecoverableError,
            "Load Failed",
            "Failed to load save file.",
            restoreFocus: null,
            parent: _loadHandle);
    }
    return;
}
```

Do not call `TryCloseHostedLoad(...)` first.

Do not retain the old `Callable.From(...).CallDeferred()` wrapper: that wrapper existed because the code first closed Load and had to escape the host's close transaction. With no preceding close, there is no drain transaction to escape; opening the child synchronously under active Load is the intended path.

- [ ] **Step 6: Map remaining Main Menu calls**

```text
No save files                    -> Warning
Settings/Load screen unavailable -> RecoverableError
Continue/manual load failure     -> RecoverableError
```

Root prompts use invoking root control as `RestoreFocus`; child prompts pass parent and no root restore target.

- [ ] **Step 7: Run GREEN/build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~SiriusPromptTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS; `scripts/ui/MainMenu.cs` contains no `AcceptDialog` construction and action availability refreshes still run on Prompt open/cleanup.

- [ ] **Step 8: Commit Task 3A**

```bash
git add scripts/ui/MainMenu.cs tests/ui/MainMenuTest.cs
git commit -m "refactor(ui): host main menu prompts"
```

---

## Task 3B: Migrate gameplay Save/Load errors and preserve paused-tree behavior

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameTest.cs`

- [ ] **Step 1: Rewrite RED `_activeErrorPopup` tests onto the hosted chain**

Replace old direct-popup tests with:

```text
ConfiguredKeyboardCancel_RecoverablePromptDoesNotFallThroughToParents
ConfiguredKeyboardCancel_PausedRecoverablePromptRemainsDismissible
HostedSaveLoad_LoadFailureKeepsSaveLoadAndHostsPrompt
HostedOverwrite_PrimaryWithNpcInteractionOpensErrorPromptUnderSaveLoad
```

Build the real paused chain:

```text
Pause -> Save/Load -> RecoverableError Prompt
```

Before Cancel assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
AssertThat(promptEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
```

After one configured Cancel assert Prompt inactive, SaveLoad + Pause still active, tree still paused, and no root Pause fallback ran.

This replaces the old native `ProcessModeEnum.Always` proof with the hosted `UIProcessPolicy.Always` contract.

- [ ] **Step 2: Pin the Prompt -> Prompt chain with existing domain state**

Implement `HostedOverwrite_PrimaryWithNpcInteractionOpensErrorPromptUnderSaveLoad` without adding a save-manager seam:

1. open Pause;
2. open Save mode;
3. ensure slot 0 contains a valid save so selecting it opens the destructive overwrite Prompt;
4. after the overwrite Prompt is active, call `_gameManager.StartNpcInteraction()`;
5. emit `%PrimaryButton.Pressed` on the overwrite Prompt.

The first Prompt's Primary closure calls `OnHostedSaveSlotSelected(0)`. Because NPC interaction is now active, that existing domain branch must call `ShowSaveError("Cannot save during NPC interaction.")`.

After the press assert:

```text
Pause active
SaveLoad active
old destructive Prompt no longer alive/registered
one new Prompt active
new Prompt parent == SaveLoad handle
new Prompt message == "Cannot save during NPC interaction."
```

This test fails if the follow-up open occurs during `_drainingCloseQueue` (`HostMutating`) or before cleanup clears the old `Prompt` kind.

End NPC interaction in test cleanup so fixture state does not leak.

- [ ] **Step 3: Add missing-parent loud-failure test**

`ShowSaveError(...)` becomes private `bool`, so `GameTest` can reuse its existing `InvokePrivate<T>` helper:

```csharp
[TestCase]
public async Task ShowSaveError_WithoutHostedSaveLoadReturnsFalseAndDoesNotOpenPrompt()
{
    await ReplaceWithHostedFixture();
    var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

    var opened = InvokePrivate<bool>(
        _game,
        "ShowSaveError",
        "Failed to save game.",
        "Save Failed");

    AssertThat(opened).IsFalse();
    AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
}
```

Production implementation also calls `GD.PushError("[Game] Cannot show save/load error without an active Save/Load parent.")`; do not invent a log-capture framework solely to assert that string.

- [ ] **Step 4: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest.ShowSaveError"
```

Expected: new parent-retention/Prompt tests FAIL against the current close-parent/native-popup behavior.

- [ ] **Step 5: Implement bool-returning hosted `ShowSaveError`**

```csharp
private bool ShowSaveError(string message, string title = "Save Failed")
{
    if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost) ||
        !_hostedSaveLoadHandle.HasValue ||
        !_screenHost.IsActive(_hostedSaveLoadHandle.Value))
    {
        GD.PushError(
            "[Game] Cannot show save/load error without an active Save/Load parent.");
        return false;
    }

    return TryOpenHostedPrompt(
        SiriusPromptVariant.RecoverableError,
        title,
        message,
        "OK",
        parent: _hostedSaveLoadHandle);
}
```

No native/root fallback.

- [ ] **Step 6: Remove all pre-error Save/Load closes**

For every failure branch inside `OnHostedSaveSlotSelected` / `OnHostedLoadSlotSelected`, remove:

```csharp
TryCloseHostedSaveLoad(UIScreenCloseReason.ExplicitAction);
```

before `ShowSaveError(...)`.

Keep the successful save/load terminal closes unchanged.

- [ ] **Step 7: Delete `_activeErrorPopup` ownership only after hosted tests exist**

Delete:

```text
_activeErrorPopup field
HandleGameplayRootCancel special branch that frees it
_ExitTree native error cleanup
native ShowSaveError construction/signals
```

Configured Cancel now belongs to Prompt through the host and cannot fall through to open/close Pause.

- [ ] **Step 8: Run GREEN/build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~SiriusPromptTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors. The overwrite -> NPC-blocked save chain opens the second Prompt only after the first Prompt close returns.

- [ ] **Step 9: Commit Task 3B**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameTest.cs
git commit -m "refactor(ui): host gameplay error prompts"
```

---

## Task 4: Migrate corrupted-save fallback, retire old kinds, and finish host fixtures

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify all seven host fixture files from the File Map.
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Add RED corrupted-save tests**

Add:

```text
CorruptedSave_OpensRootRecoverablePromptAndBlocksGameplayWithoutTreePause
CorruptedSave_PrimaryRequestsMainMenuExactlyOnce
CorruptedSave_ConfiguredCancelMapsToPrimaryAndRequestsMainMenuExactlyOnce
CorruptedSave_SecondDetectionDoesNotOpenSecondPrompt
CorruptedSave_RootTeardownClearsPromptAndGameplayBlock
CorruptedSave_PresentationFailureStillReturnsToTitle
```

For a successfully hosted corrupted Prompt assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.IsProcessingInput()).IsTrue();
var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
```

For presentation failure, use `TestableGame` without a production host and add a narrow override seam:

```csharp
public int ReturnToMainMenuCalls { get; private set; }

protected override void ReturnToMainMenu()
{
    ReturnToMainMenuCalls++;
}
```

Invoke `ShowCorruptedSaveError()` through reflection and assert `ReturnToMainMenuCalls == 1`, no Prompt exists, and repeated invocation does not produce a second call.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameTest.CorruptedSave|FullyQualifiedName~GameInputLifecycleTest.CorruptedSave|FullyQualifiedName~GameplayPauseHostTest.CorruptedSave"
```

Expected: FAIL because corrupted save is still native/manual-input-disabled.

- [ ] **Step 3: Replace corrupted-save presentation with mandatory fallback**

```csharp
private void ShowCorruptedSaveError()
{
    if (_hasShownCorruptedSaveError)
        return;

    _hasShownCorruptedSaveError = true;

    var opened = TryOpenHostedPrompt(
        SiriusPromptVariant.RecoverableError,
        "Load Failed",
        "Save file is corrupted or invalid.\nReturning to main menu.",
        "Return to Title",
        onPrimary: ReturnToMainMenu,
        blockGameplayInput: true);

    if (!opened)
    {
        GD.PushError("[Game] Failed to present corrupted-save error; returning to title.");
        ReturnToMainMenu();
    }
}
```

Delete `SetProcessInput(false)` and native Confirmed/Canceled/local handled plumbing. Do not add a native fallback.

- [ ] **Step 4: Verify terminal handler semantics under corrupted-save action**

The Task 2 handler remains:

```csharp
private void OnHostedPromptPrimaryRequested()
{
    var action = _hostedPromptPrimaryAction;
    TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction);
    action?.Invoke();
}
```

Do not regress this to `if (TryCloseHostedPrompt(...)) action?.Invoke();`.

- [ ] **Step 5: Retarget host fixture identities before deleting product kinds**

Use file-local identities in each affected host test. Example for `UIScreenStackModelTest.cs`:

```csharp
private static readonly StringName BlockingPromptA = "blocking_prompt_a";
private static readonly StringName BlockingPromptB = "blocking_prompt_b";
private static readonly StringName ModalFixture = "modal_fixture";
```

Rewrite the exclusive-group test to open A and B under the same `BlockingPrompt` group, preserving:

```csharp
AssertThat(second.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
```

Use local fixture kinds—not `UIScreenKinds.Prompt`—for generic modal/subwindow/focus/lifecycle/process tests in:

```text
UIScreenHostSubwindowTest.cs
UIScreenHostInputTest.cs
UIScreenHostFocusTest.cs
UIScreenHostLifecycleTest.cs
UIScreenHostContractScenarioTest.cs
UIScreenHostProcessModeTest.cs
```

No shared fixture-kind registry.

- [ ] **Step 6: Delete obsolete product prompt kinds**

Remove:

```csharp
ConfirmOverwrite
ConfirmQuitToMain
SaveError
CorruptSaveError
```

Keep `Prompt` and `UIScreenExclusiveGroups.BlockingPrompt`.

- [ ] **Step 7: Reconcile only migrated HPA-376 rows**

Update current lifecycle rows describing:

```text
MAIN-MESSAGE
PAUSE-SAVELOAD nested overwrite/error behavior
PAUSE-QUIT-TO-MAIN
corrupted-save blocking/error behavior
```

Record:

- shared component path;
- three variants;
- overwrite intentional WarningButton -> DestructiveButton normalization;
- Pause return-to-title newly gains one-shot latch;
- parent retention + paused-tree `Always` behavior;
- root handlers close-attempt then invoke closure regardless of close status;
- corrupted-save host block + presentation-failure navigation fallback;
- one product Prompt kind.

Do not rewrite legacy Dialogue/Shop/Heal/Puzzle rows.

- [ ] **Step 8: Run focused GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenHostSubwindowTest|FullyQualifiedName~UIScreenHostInputTest|FullyQualifiedName~UIScreenHostFocusTest|FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostContractScenarioTest|FullyQualifiedName~UIScreenHostProcessModeTest"
```

Expected: PASS.

- [ ] **Step 9: Run full validation**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: full suite PASS, 0 build errors, clean diff check.

- [ ] **Step 10: Run stale-reference and scope audits**

Dedicated prompt implementations gone:

```bash
rg -n "SaveOverwriteConfirmation|PauseReturnToTitleConfirmation" scripts scenes tests
```

Expected: no matches.

Old host kinds gone:

```bash
rg -n "UIScreenKinds\.(ConfirmOverwrite|ConfirmQuitToMain|SaveError|CorruptSaveError)" scripts tests
```

Expected: no matches.

No native prompt construction remains in migrated roots:

```bash
rg -n "new AcceptDialog|AcceptDialog" scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

No speculative host-spec factory:

```bash
rg -n "SpecFor|PromptService|PromptPresenter" scripts/ui/components/SiriusPrompt.cs scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

Deferred legacy screens may still use native dialogs:

```bash
rg -n "AcceptDialog" scripts/ui/DialogueDialog.cs scripts/ui/ShopDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs
```

Expected: matches allowed.

Final scope is shared Prompt + two invoking roots + tests + kinds + deleted dedicated confirmations + lifecycle doc. Reject unrelated refactors.

- [ ] **Step 11: Commit Task 4**

```bash
git add scripts/game/Game.cs scripts/ui/hosting/UIScreenKinds.cs \
  tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs tests/ui/hosting/UIScreenHostSubwindowTest.cs \
  tests/ui/hosting/UIScreenHostInputTest.cs tests/ui/hosting/UIScreenHostFocusTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs tests/ui/hosting/UIScreenHostContractScenarioTest.cs \
  tests/ui/hosting/UIScreenHostProcessModeTest.cs docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "refactor(ui): finish hosted prompt migration"
```

---

## Final implementation review checklist

Before merging implementation:

- [ ] Exactly three `SiriusPromptVariant` values exist.
- [ ] `SiriusPrompt` lives under `ui/components`; `SiriusContextPrompt` is unchanged.
- [ ] Both roots use `Cancel = Consume` and `InterceptCancel -> RequestCancel()`; root tests assert policy + behavior.
- [ ] No `SiriusPrompt.SpecFor(...)` / prompt service / host facade exists.
- [ ] Root handlers capture -> close attempt -> invoke closure unconditionally.
- [ ] Save/Load errors retain Save/Load parent; paused Prompt is `Always` and Cancel does not fall through.
- [ ] Overwrite -> NPC-interaction error -> recoverable Prompt chain is covered without a new save-manager seam.
- [ ] Pause return-to-title has explicit one-shot terminal coverage.
- [ ] Overwrite's primary is intentionally `SiriusDestructiveButton`.
- [ ] Main Menu calls `RefreshActionAvailability()` on Prompt open and cleanup and no longer defers child error open after a removed parent close.
- [ ] Corrupted-save hosting failure still requests Main Menu once.
- [ ] Host fixture tests use local distinct identities and still prove `ExclusiveGroupConflict`.
- [ ] Old prompt kinds/scenes/controllers/native root dialogs are absent.
- [ ] Full tests/build/diff/stale audits have fresh passing evidence.
