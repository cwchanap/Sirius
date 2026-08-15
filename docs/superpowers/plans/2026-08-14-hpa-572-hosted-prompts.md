# HPA-572 Host-Managed Prompts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's duplicate confirmation scenes and remaining Main Menu/gameplay native error dialogs with one scene-authored modal prompt component while preserving domain ownership, parent retention, paused-tree input, focus, Cancel priority, and teardown behavior.

**Architecture:** Add one reusable `SiriusPrompt` component beside `SiriusModalShell` and `SiriusToastShell`. `Game` and `MainMenu` keep local `UIScreenHost` registration, prompt handles, and domain closures; no prompt service, presenter, queue, singleton, router, or host facade is introduced. Ship only the three presentation variants used by current consumers; corrupted-save blocking is a host policy, not a prompt variant.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and `UIScreenHost`.

## Global Constraints

- Support exactly `DestructiveConfirmation`, `Warning`, and `RecoverableError`.
- Do not add `InformationalConfirmation` until a real two-button Info caller exists.
- Do not add `BlockingError`; corrupted-save uses `RecoverableError` plus `BlockGameplayInput = true`.
- Reuse `SiriusModalShell`, `SiriusUiSeverity`, current button variations, `SiriusUiMetrics.MinimumTarget(...)`, and `UIScreenExclusiveGroups.BlockingPrompt`.
- `SiriusContextPrompt` remains a HUD input-hint component; do not reuse or modify it.
- `SiriusPrompt` owns chrome and one-shot terminal intent only. `Game` / `MainMenu` own domain closures and host handles.
- Every Prompt entry uses `Cancel = UICancelPolicy.Consume` plus `InterceptCancel -> prompt.RequestCancel()`; never use `Cancel = Close` for this component.
- `DestructiveConfirmation` focuses Cancel. One-action variants focus Primary.
- Child prompts keep their logical parent active/inert and rely on host focus restoration.
- Failed Save/Load keeps active Save/Load open. Missing expected gameplay Save/Load parent is a logged error, not a silent no-op or native fallback.
- Pause -> Save/Load -> Prompt must remain dismissible while `SceneTree.Paused == true`; Prompt uses `UIProcessPolicy.Always`.
- Corrupted-save leaves `Game` input processing enabled and acquires only the host gameplay-block lease.
- Collapse product prompt kinds to `UIScreenKinds.Prompt`; host unit tests that need distinct kinds use test-local `StringName`s.
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

Do not change audit-only files without a focused failing regression.

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

Add these tests:

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

Use this mapping in `Variants_MapSeverityButtonsThemeAndInitialFocus`:

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

Create:

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

Use:

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

Update the overwrite configured-Cancel test in `GameInputLifecycleTest.cs` to expect one `UIScreenKinds.Prompt` child. One configured Cancel closes only Prompt; Save/Load and Pause remain active.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.HostedOverwrite|FullyQualifiedName~GameplayPauseHostTest.PauseReturnToTitle|FullyQualifiedName~GameplayPauseHostTest.HostedPrompt|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_SaveLoadOverwrite"
```

Expected: FAIL because production still uses dedicated confirmation scenes/kinds.

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

- [ ] **Step 4: Implement callback capture-before-close**

```csharp
private void OnHostedPromptPrimaryRequested()
{
    var action = _hostedPromptPrimaryAction;
    if (TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction))
        action?.Invoke();
}

private void OnHostedPromptCancelRequested()
{
    var action = _hostedPromptCancelAction;
    if (TryCloseHostedPrompt(UIScreenCloseReason.ExplicitAction))
        action?.Invoke();
}
```

`ClearHostedPrompt(SiriusPrompt prompt)` unsubscribes and clears handle/view/actions only for the currently-owned prompt. Capture is required because host cleanup runs synchronously inside close.

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

Keep `ReturnToMainMenu()` / `RequestSceneChange(...)` unchanged.

- [ ] **Step 7: Delete dedicated implementations and migrate useful assertions**

Delete all six feature-specific confirmation files. Leaf latch/layout/focus coverage now belongs to `SiriusPromptTest`; parent/domain behavior belongs to host/input tests. Do not keep wrapper scenes or old type names.

- [ ] **Step 8: Run GREEN/build and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 errors.

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scenes/ui/SaveOverwriteConfirmation.tscn scripts/ui/SaveOverwriteConfirmationController.cs tests/ui/SaveOverwriteConfirmationControllerTest.cs \
  scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "refactor(ui): share destructive prompts"
```

---

## Task 3: Migrate recoverable errors without losing paused-tree coverage

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/MainMenuTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameTest.cs`

**Produces:** MainMenu-local prompt helper using the same component/host policy; `Game.ShowSaveError(...)` becomes a `bool`-returning hosted-child seam.

- [ ] **Step 1: Write RED Main Menu tests**

Add/replace:

```text
LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus
LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog
ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsPrompt
HostedLoadFailure_KeepsLoadParentAfterAcknowledge
RootPrompt_TerminalCannotRunTwice
```

For Load-parent failures assert while open:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(loadScreen.Visible).IsTrue();
```

After acknowledgement Prompt is gone and SaveLoad remains.

- [ ] **Step 2: Rewrite RED gameplay error tests onto the hosted chain**

Replace the current `_activeErrorPopup` tests with:

```text
ConfiguredKeyboardCancel_RecoverablePromptDoesNotFallThroughToParents
ConfiguredKeyboardCancel_PausedRecoverablePromptRemainsDismissible
HostedSaveLoad_LoadFailureKeepsSaveLoadAndHostsPrompt
```

Build the real chain:

```text
Pause -> Save/Load -> RecoverableError Prompt
```

Before Cancel assert Pause + SaveLoad + Prompt active and `SceneTree.Paused == true`. After one configured Cancel assert Prompt inactive, SaveLoad + Pause still active, tree still paused, and the same event did not close a parent/open another Pause.

This replaces the old native `ProcessMode.Always` regression: Prompt's `UIProcessPolicy.Always` now proves dismissibility under the active Pause lease.

- [ ] **Step 3: Add RED missing-parent test using the existing reflection helper**

In `GameTest.cs`:

```csharp
[TestCase]
public async Task ShowSaveError_WithoutActiveSaveLoadReturnsFalseAndDoesNotOpenPrompt()
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

The production implementation in Step 8 must also call `GD.PushError`; the boolean gives the test a deterministic seam without inventing log-capture infrastructure.

- [ ] **Step 4: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest.ShowSaveError"
```

Expected: FAIL against current native/close-parent behavior.

- [ ] **Step 5: Replace Main Menu native message state with a root-local Prompt helper**

Replace `_messageDialog : AcceptDialog` / `_messageCloseDelegate` with `SiriusPrompt` state. Load `res://scenes/ui/components/SiriusPrompt.tscn` and use the same `BlockingPrompt`, `Always`, `Cancel = Consume`, `InterceptCancel -> RequestCancel()` policy as Game.

Map:

```text
no save files                 -> Warning
Settings/Load unavailable     -> RecoverableError
Continue/manual load failure  -> RecoverableError
```

For root messages use invoking button `RestoreFocus`; for a child message pass the parent handle and no root restore target.

- [ ] **Step 6: Keep Main Menu Load active on load failure**

Change the failure branch in `OnHostedLoadSlotSelected` from close + deferred root message to direct child presentation:

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

Do not close Load and do not defer a new root message.

- [ ] **Step 7: Move every gameplay failure before Save/Load close**

In `OnHostedSaveSlotSelected` and `OnHostedLoadSlotSelected`, remove `TryCloseHostedSaveLoad(...)` immediately before every failure `ShowSaveError(...)` call. Successful save/load keeps existing terminal close/navigation behavior.

- [ ] **Step 8: Change `ShowSaveError` to a loud, testable hosted-child seam**

Use exactly:

```csharp
private bool ShowSaveError(string message, string title = "Save Failed")
{
    if (_screenHost == null ||
        !GodotObject.IsInstanceValid(_screenHost) ||
        !_hostedSaveLoadHandle.HasValue ||
        !_screenHost.IsActive(_hostedSaveLoadHandle.Value))
    {
        GD.PushError("[Game] Cannot show save/load error without an active Save/Load parent.");
        return false;
    }

    var opened = TryOpenHostedPrompt(
        SiriusPromptVariant.RecoverableError,
        title,
        message,
        "OK",
        parent: _hostedSaveLoadHandle);

    if (!opened)
        GD.PushError("[Game] Failed to present hosted save/load error prompt.");

    return opened;
}
```

Existing production callers may ignore the return value. Do not create a root/native fallback.

- [ ] **Step 9: Delete native `_activeErrorPopup` ownership after hosted tests exist**

Delete the field, its `_ExitTree` cleanup, `ShowSaveError` native dialog/signals, and `HandleGameplayRootCancel`'s special branch. Do not replace that root branch: active Prompt is now the top input owner and consumes Cancel before root fallback.

- [ ] **Step 10: Run GREEN/build and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~SiriusPromptTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 errors. A recoverable child remains dismissible while Pause keeps the tree paused; SaveLoad stays active after acknowledgement.

```bash
git add scripts/ui/MainMenu.cs scripts/game/Game.cs tests/ui/MainMenuTest.cs \
  tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameTest.cs
git commit -m "refactor(ui): host recoverable prompts"
```

---

## Task 4: Migrate corrupted-save presentation and retire product prompt kinds safely

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify all seven host fixture test files from the File Map.
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Write RED corrupted-save tests**

Add:

```text
CorruptedSave_OpensRecoverablePromptWithGameplayBlockWithoutTreePause
CorruptedSave_PrimaryRequestsMainMenuOnce
CorruptedSave_ConfiguredCancelRequestsMainMenuOnce
CorruptedSave_SecondDetectionDoesNotStackPrompt
CorruptedSave_RootTeardownClearsPromptAndGameplayBlock
```

While active assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.IsProcessingInput()).IsTrue();
```

The last assertion proves manual `SetProcessInput(false)` is gone.

- [ ] **Step 2: Run corrupted-save RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameTest.CorruptedSave|FullyQualifiedName~GameInputLifecycleTest.CorruptedSave|FullyQualifiedName~GameplayPauseHostTest.CorruptedSave"
```

Expected: FAIL against current native/manual-input behavior.

- [ ] **Step 3: Replace only corrupted-save presentation**

Keep `_hasShownCorruptedSaveError` and current validation/abort semantics. Use:

```csharp
private void ShowCorruptedSaveError()
{
    if (_hasShownCorruptedSaveError)
        return;

    _hasShownCorruptedSaveError = true;

    if (!TryOpenHostedPrompt(
            SiriusPromptVariant.RecoverableError,
            "Load Failed",
            "Save file is corrupted or invalid.\nReturning to main menu.",
            "Return to Title",
            onPrimary: ReturnToMainMenu,
            blockGameplayInput: true))
    {
        GD.PushError("[Game] Failed to present corrupted-save error prompt.");
    }
}
```

Delete `SetProcessInput(false)` and native Confirmed/Canceled/local `handled` plumbing. Do not add a native fallback. `RecoverableError.RequestCancel()` already maps Cancel to Primary.

- [ ] **Step 4: Add test-local host kinds before deleting product constants**

Do not add a shared test-kind registry. Add only file-local identities. In `UIScreenStackModelTest.cs` use:

```csharp
private static readonly StringName BlockingPromptA = new("blocking_prompt_a");
private static readonly StringName BlockingPromptB = new("blocking_prompt_b");
private static readonly StringName ModalFixture = new("modal_fixture");
```

For the other affected suites, a local `ModalFixture = new("modal_fixture")` is enough unless the test needs multiple simultaneous kinds.

- [ ] **Step 5: Preserve the exclusive-group test's actual contract**

Rewrite `Open_DifferentConfirmationKindsShareBlockingPromptGroup` to open `BlockingPromptA` then `BlockingPromptB`, both in `UIScreenExclusiveGroups.BlockingPrompt`.

Keep:

```csharp
AssertThat(second.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
```

Never use `UIScreenKinds.Prompt` for both sides; that would exercise `DuplicateKind` instead.

- [ ] **Step 6: Retarget all remaining generic prompt-kind fixtures**

Replace product-kind stand-ins without changing the tested host policy:

```text
UIScreenStackModelTest.cs              -> local blocking/modal kinds
UIScreenHostSubwindowTest.cs           -> local modal kind
UIScreenHostInputTest.cs               -> local modal kind
UIScreenHostFocusTest.cs               -> local modal/confirmation kind(s)
UIScreenHostLifecycleTest.cs           -> local modal kind
UIScreenHostContractScenarioTest.cs    -> local modal/confirmation kind(s)
UIScreenHostProcessModeTest.cs         -> local modal kind
```

- [ ] **Step 7: Run host suites before deleting constants**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenHostSubwindowTest|FullyQualifiedName~UIScreenHostInputTest|FullyQualifiedName~UIScreenHostFocusTest|FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostContractScenarioTest|FullyQualifiedName~UIScreenHostProcessModeTest"
```

Expected: PASS, including the original `ExclusiveGroupConflict` assertion.

- [ ] **Step 8: Delete obsolete product kinds**

Remove from `UIScreenKinds.cs`:

```csharp
ConfirmOverwrite
ConfirmQuitToMain
SaveError
CorruptSaveError
```

Keep `Prompt` and `UIScreenExclusiveGroups.BlockingPrompt`.

- [ ] **Step 9: Reconcile only migrated HPA-376 rows**

Update `MAIN-MESSAGE`, Pause Save/Load nested prompt behavior, Pause return-to-title, and corrupted-save/error rows. Record:

```text
scene-authored SiriusPrompt under ui/components
one product Prompt kind
three presentation variants only
Save/Load parent retention
Pause remains sole tree-pause owner; Prompt uses Always
configured Cancel -> RequestCancel child-first
corrupted save = RecoverableError chrome + host gameplay block
one-shot terminal latch + host focus/teardown cleanup
```

Do not rewrite Dialogue/Shop/Heal/Puzzle/Reward rows.

- [ ] **Step 10: Run focused + full verification**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenHost"

dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: focused/full suites PASS, 0 build errors, clean diff check.

- [ ] **Step 11: Run stale-reference audits**

```bash
rg -n "SaveOverwriteConfirmation|PauseReturnToTitleConfirmation" scripts scenes tests
```

Expected: no matches.

```bash
rg -n "UIScreenKinds\.(ConfirmOverwrite|ConfirmQuitToMain|SaveError|CorruptSaveError)" scripts tests
```

Expected: no matches.

```bash
rg -n "AcceptDialog" scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

```bash
rg -n "SiriusPrompt" scripts/ui/components scenes/ui/components tests/ui/components scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: shared component + root consumers only; no top-level `scripts/ui/SiriusPromptController.cs` or `scenes/ui/SiriusPrompt.tscn`.

Deferred legacy native dialogs are allowed:

```bash
rg -n "AcceptDialog" scripts/ui/DialogueDialog.cs scripts/ui/ShopDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs
```

- [ ] **Step 12: Scope audit and commit Task 4**

Allowed implementation scope:

```text
shared SiriusPrompt component + test
Game / MainMenu integrations
UIScreenKinds
integration tests
seven host fixture test files
six deleted dedicated confirmation files
HPA-376 lifecycle contract
```

Reject unrelated refactors or new shared APIs.

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

## Final implementation handoff

After all four task commits, rerun Task 4's complete validation from the final branch head. The implementation is complete only when the full suite/build pass, the paused-tree Prompt regression remains green, the exclusive-group test still proves `ExclusiveGroupConflict`, stale-reference audits return the expected results, and the changed-file scope matches this plan.
