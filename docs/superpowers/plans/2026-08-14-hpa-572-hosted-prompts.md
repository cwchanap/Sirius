# HPA-572 Host-Managed Prompts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's duplicate confirmation scenes and remaining Main Menu/gameplay native error dialogs with one scene-authored modal prompt component while preserving domain ownership, parent retention, paused-tree input, focus, Cancel priority, and teardown behavior.

**Architecture:** Add one reusable `SiriusPrompt` component beside `SiriusModalShell` and `SiriusToastShell`. `Game` and `MainMenu` keep local `UIScreenHost` registration, prompt handles, and domain closures; no prompt service, presenter, queue, singleton, router, or host facade is introduced. Ship only the three presentation variants used by current consumers; corrupted-save blocking remains a host policy, not a prompt variant.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and `UIScreenHost`.

## Global Constraints

- Support exactly three presentation variants: `DestructiveConfirmation`, `Warning`, and `RecoverableError`.
- Do not add `InformationalConfirmation` until a real two-button Info confirmation caller exists.
- Do not add `BlockingError`; corrupted-save uses `RecoverableError` chrome plus `UIScreenEntrySpec.BlockGameplayInput = true`.
- Reuse `SiriusModalShell`, `SiriusUiSeverity`, existing button theme variations, `SiriusUiMetrics.MinimumTarget(...)`, and `UIScreenExclusiveGroups.BlockingPrompt`.
- `SiriusContextPrompt` remains a HUD input-hint component and is not reused or modified.
- Keep domain actions in `Game` / `MainMenu`; `SiriusPrompt` owns chrome and at-most-once terminal intent only.
- Every hosted prompt uses `Cancel = UICancelPolicy.Consume` plus `InterceptCancel -> prompt.RequestCancel()`; do not use `Cancel = Close`.
- `DestructiveConfirmation` focuses Cancel first; `Warning` and `RecoverableError` focus their only primary action.
- Child prompts keep their logical parent active/inert and restore the parent through `UIScreenHost` focus handling.
- Save/Load errors keep active Save/Load open. `Game.ShowSaveError(...)` must log an error if the expected Save/Load parent is absent; no silent no-op and no native fallback.
- A recoverable prompt under Pause -> Save/Load must remain dismissible while `SceneTree.Paused == true`; `UIProcessPolicy.Always` is the replacement for native `ProcessMode.Always`.
- Corrupted-save keeps `Game` input processing enabled and acquires only the host gameplay-block lease.
- Collapse product prompt kinds to one `UIScreenKinds.Prompt`; host unit tests that need multiple identities use test-local `StringName`s.
- Do not migrate `DialogueDialog`, `ShopDialog`, `HealDialog`, `PuzzleRiddleDialog`, or Reward presentation.
- Do not add Theme tokens, icons, metrics, `SiriusModalShell` APIs, or `UIScreenHost` APIs unless a focused RED test proves a shared contract defect.
- No compatibility shims are required for deleted prompt scenes/controllers/kinds.

---

## File Map

### Create

- `scenes/ui/components/SiriusPrompt.tscn` — shared modal leaf built on `SiriusModalShell`.
- `scripts/ui/components/SiriusPrompt.cs` — three-variant presentation mapping, responsive sizing, terminal latch, Cancel mapping.
- `tests/ui/components/SiriusPromptTest.cs` — variant, terminal, responsive, long-text, and scene-contract coverage.

### Modify: production and integration

- `scripts/ui/hosting/UIScreenKinds.cs` — add `Prompt`; remove old prompt-only product kinds after all references migrate.
- `scripts/game/Game.cs` — local hosted-prompt plumbing, overwrite, return-to-title, Save/Load errors, corrupted-save error.
- `scripts/ui/MainMenu.cs` — replace hosted native messages with `SiriusPrompt`; retain Load parent on failures.
- `tests/game/GameplayPauseHostTest.cs` — nested prompt parent retention, paused-tree behavior, focus, programmatic close/teardown.
- `tests/game/GameInputLifecycleTest.cs` — configured Cancel child-first behavior; replace `_activeErrorPopup` characterization.
- `tests/game/GameTest.cs` — corrupted-save gameplay block and exactly-once transition coverage where the existing fixture is the best seam.
- `tests/ui/MainMenuTest.cs` — root warning/error, Load-parent retention, root focus, no native message dialog.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — update only migrated prompt/error rows.

### Modify: host test fixture identities

These tests currently use prompt product kinds as generic host fixtures. Retarget those references to test-local `StringName`s before deleting the product constants:

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

Do not change audit-only files unless a focused RED test proves their current contract is broken.

---

## Task 1: Add the shared `SiriusPrompt` component

**Files:**
- Create: `scenes/ui/components/SiriusPrompt.tscn`
- Create: `scripts/ui/components/SiriusPrompt.cs`
- Create: `tests/ui/components/SiriusPromptTest.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`

**Interfaces:**
- Consumes: `SiriusModalShell`, `SiriusUiSeverity`, `SiriusThemeTypes`, `SiriusUiMetrics.MinimumTarget(...)`.
- Produces:

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

Also add, but do **not** yet remove old kinds:

```csharp
public static readonly StringName Prompt = "prompt";
```

### 1A — RED component tests

- [ ] **Step 1: Create the component test fixture**

Create `tests/ui/components/SiriusPromptTest.cs` with a `SubViewportContainer` + `SubViewport` fixture. Default the fixture to `640x360`; provide a helper that resizes both container and viewport to `1280x720` and awaits two process frames.

Use:

```csharp
private const string ScenePath = "res://scenes/ui/components/SiriusPrompt.tscn";
```

- [ ] **Step 2: Add RED variant mapping coverage**

Add one table-driven test with these cases:

```csharp
var cases = new[]
{
    (SiriusPromptVariant.DestructiveConfirmation,
        SiriusUiSeverity.Warning,
        true,
        SiriusThemeTypes.DestructiveButton),
    (SiriusPromptVariant.Warning,
        SiriusUiSeverity.Warning,
        false,
        SiriusThemeTypes.PrimaryButton),
    (SiriusPromptVariant.RecoverableError,
        SiriusUiSeverity.Error,
        false,
        SiriusThemeTypes.PrimaryButton)
};
```

For each case configure title/message/action text and assert:

```text
%ModalShell.Severity
%Message.Text
%PrimaryButton.Text
%PrimaryButton.ThemeTypeVariation
%CancelButton.Visible
InitialFocusTarget
```

`DestructiveConfirmation` must focus `%CancelButton`; one-action variants must focus `%PrimaryButton`.

- [ ] **Step 3: Add RED terminal-latch tests**

Add:

```text
DestructiveConfirmation_PrimaryThenCancelEmitsOnlyPrimaryOnce
DestructiveConfirmation_CancelThenPrimaryEmitsOnlyCancelOnce
Warning_RequestCancelMapsToPrimaryOnce
RecoverableError_RequestCancelMapsToPrimaryOnce
RepeatedPrimaryPress_EmitsOnlyOnce
```

Each test subscribes counters to `PrimaryRequested` / `CancelRequested`, emits the relevant button signal and/or calls `RequestCancel()`, and asserts total terminal emissions equal one.

- [ ] **Step 4: Add RED layout/scene tests**

Add:

```text
CompactViewport_UsesCompactShellAndMinimumTargets
CrossingCompactToStandard_RefreshesShellAndTargets
LongMessage_MinimumViewportRemainsInsideShellAndScrollableIfNeeded
Scene_UsesSiriusModalShellAndContainsNoAcceptDialog
```

The long-message test should use several wrapped sentences, await layout frames, assert the shell panel stays within the `640x360` viewport, and assert either the full message fits or the shell body scroll has positive overflow. Do not hard-code an invented scroll amount.

- [ ] **Step 5: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest"
```

Expected: compile/test failure because the component and enum do not exist.

### 1B — minimal component implementation

- [ ] **Step 6: Author the component scene**

Create this stable tree:

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

Author both buttons with 44px minimum height as a safe editor/default state.

- [ ] **Step 7: Implement stored configuration and node binding**

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

`Configure(...)` stores non-null strings and calls `RefreshPresentation()` when ready. `_Ready()` binds `%ModalShell`, `%Message`, `%PrimaryButton`, `%CancelButton`, wires the two `Pressed` signals and `Resized`, then refreshes.

- [ ] **Step 8: Implement exact visual mappings**

```csharp
private static SiriusUiSeverity SeverityFor(SiriusPromptVariant variant) =>
    variant switch
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

Refresh from `GetViewportRect().Size`:

```csharp
var size = GetViewportRect().Size;
var compact = SiriusUiMetrics.IsCompact(size);
var target = SiriusUiMetrics.MinimumTarget(compact);

_shell.Title = _title;
_shell.Severity = SeverityFor(_variant);
_shell.Compact = compact;
_shell.RefreshPresentation(size);

_messageLabel.Text = _message;
_primary.Text = _primaryActionText;
_primary.ThemeTypeVariation = PrimaryThemeFor(_variant);
_primary.CustomMinimumSize = new Vector2(0, target.Y);

_cancel.Visible = _variant == SiriusPromptVariant.DestructiveConfirmation;
_cancel.Text = _cancelActionText;
_cancel.ThemeTypeVariation = SiriusThemeTypes.SecondaryButton;
_cancel.CustomMinimumSize = new Vector2(0, target.Y);
```

- [ ] **Step 9: Implement the one-shot terminal contract**

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

Wire `_primary.Pressed` to `EmitPrimaryOnce` and `_cancel.Pressed` to `EmitCancelOnce`. `_ExitTree()` disconnects button/resize handlers. Never reset `_terminalEmitted`.

- [ ] **Step 10: Run GREEN plus shell regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~SiriusModalShellTest|FullyQualifiedName~SiriusContextPromptTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors. No audit-only file changes.

- [ ] **Step 11: Commit Task 1**

```bash
git add scenes/ui/components/SiriusPrompt.tscn \
  scripts/ui/components/SiriusPrompt.cs \
  scripts/ui/hosting/UIScreenKinds.cs \
  tests/ui/components/SiriusPromptTest.cs
git commit -m "feat(ui): add shared Sirius prompt"
```

---

## Task 2: Migrate Save overwrite and Pause return-to-title

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete: `scenes/ui/SaveOverwriteConfirmation.tscn`
- Delete: `scripts/ui/SaveOverwriteConfirmationController.cs`
- Delete: `tests/ui/SaveOverwriteConfirmationControllerTest.cs`
- Delete: `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- Delete: `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- Delete: `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusPrompt.Configure(...)`, `RequestCancel()`, `PrimaryRequested`, `CancelRequested`, `UIScreenKinds.Prompt`.
- Produces this Game-local seam for Tasks 3-4:

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
```

with fields:

```csharp
private UIScreenHandle? _hostedPromptHandle;
private SiriusPrompt? _hostedPrompt;
private Action? _hostedPromptPrimaryAction;
private Action? _hostedPromptCancelAction;
```

### 2A — RED integration tests

- [ ] **Step 1: Add shared-prompt destructive confirmation tests**

In `GameplayPauseHostTest.cs`, add/replace:

```text
HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad
HostedOverwrite_PrimaryInvokesSavePathOnce
PauseReturnToTitle_UsesSharedPromptAndCancelRestoresPause
PauseReturnToTitle_PrimaryRequestsNavigationOnce
HostedPrompt_ProgrammaticCloseClearsHandleAndBlockingGroup
HostedPrompt_ParentCloseRemovesDescendantAndClearsReferences
```

For the first/third tests assert `UIScreenKinds.Prompt` is the only prompt kind active, the prompt's parent is the expected Save/Load or Pause handle, Cancel is initial focus, and closing the prompt leaves the parent active.

- [ ] **Step 2: Migrate the configured overwrite Cancel expectation**

In `GameInputLifecycleTest.cs`, change the overwrite journey to expect a `Prompt` child. One configured Cancel must close only Prompt; Save/Load and Pause remain active and the input event is handled.

- [ ] **Step 3: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.HostedOverwrite|FullyQualifiedName~GameplayPauseHostTest.PauseReturnToTitle|FullyQualifiedName~GameplayPauseHostTest.HostedPrompt|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_SaveLoadOverwrite"
```

Expected: FAIL because production still opens dedicated scenes/kinds.

### 2B — Game-local prompt host seam

- [ ] **Step 4: Add Game prompt fields and loading path**

`TryOpenHostedPrompt(...)` must:

1. reject missing/invalid `_screenHost` or committed scene teardown;
2. reject an active `_hostedPromptHandle` or active `UIScreenKinds.Prompt`;
3. load `res://scenes/ui/components/SiriusPrompt.tscn`;
4. instantiate/configure a fresh `SiriusPrompt`;
5. store and subscribe the two root-owned closures;
6. call `TryPresent` with the policy below.

- [ ] **Step 5: Use the exact host policy**

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

On registration failure disconnect signals, clear stored closures only if they belong to this candidate, and queue the unhosted node directly.

- [ ] **Step 6: Implement close/cleanup with callback capture-before-close**

Use:

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

`ClearHostedPrompt(SiriusPrompt prompt)` disconnects signals and clears handle/view/actions only when `ReferenceEquals(_hostedPrompt, prompt)`. The capture before `TryClose(...)` is mandatory because cleanup clears the stored action synchronously.

- [ ] **Step 7: Replace Save overwrite**

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
        cancelActionText: "Cancel",
        parent: _hostedSaveLoadHandle);
}
```

The captured slot remains root-owned. Cancel has no domain callback.

- [ ] **Step 8: Replace Pause return-to-title**

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
        cancelActionText: "Cancel",
        parent: _pauseHandle);
}
```

`ReturnToMainMenu()` remains teardown-safe and unchanged.

- [ ] **Step 9: Delete dedicated confirmation implementations**

Delete all six scene/controller/test files listed in this task. Useful leaf behavior is now in `SiriusPromptTest`; host/domain behavior remains in Game integration tests. Do not keep wrapper scenes or old class names.

- [ ] **Step 10: Run GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS; no reference to either deleted controller/scene in active scripts/scenes/tests.

- [ ] **Step 11: Commit Task 2**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scenes/ui/SaveOverwriteConfirmation.tscn scripts/ui/SaveOverwriteConfirmationController.cs tests/ui/SaveOverwriteConfirmationControllerTest.cs \
  scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "refactor(ui): share destructive prompts"
```

---

## Task 3: Migrate recoverable Main Menu/gameplay errors and preserve paused-tree behavior

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/MainMenuTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Consumes: Game-local prompt seam from Task 2.
- Produces: one MainMenu-local prompt seam with the same `SiriusPrompt`/host contract but no shared helper class.
- Removes: `_activeErrorPopup` and its root-Cancel special case after hosted regressions replace them.

### 3A — Main Menu RED tests

- [ ] **Step 1: Add root warning/recoverable tests**

In `MainMenuTest.cs`, add/replace:

```text
LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus
LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog
RootPrompt_ConfiguredTerminalCannotRunTwice
```

Assert `UIScreenKinds.Prompt`, no native `AcceptDialog`, root remains present/inert while prompt is active, and the invoking button regains focus after acknowledgement.

- [ ] **Step 2: Add Load-parent retention tests**

Add:

```text
ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsRecoverablePrompt
HostedLoadFailure_KeepsLoadParentAndRestoresLoadAfterAcknowledge
```

While the error is open:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(loadScreen.Visible).IsTrue();
```

After acknowledgement Prompt is gone and SaveLoad remains active.

### 3B — gameplay paused-tree RED tests

- [ ] **Step 3: Rewrite the old unhosted error tests before deleting `_activeErrorPopup`**

Replace the current tests that directly assign or inspect `_activeErrorPopup` with hosted journeys:

```text
ConfiguredKeyboardCancel_RecoverablePromptClosesBeforePauseOrSaveLoad
ConfiguredKeyboardCancel_PausedRecoverablePromptRemainsDismissibleWhilePauseActive
```

Both tests must construct the real chain:

```text
Pause -> Save/Load -> RecoverableError Prompt
```

and assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsTrue();
```

After one configured Cancel, assert Prompt is gone, SaveLoad and Pause remain, the tree is still paused, and the same input did not fall through to close the parent or invoke the root fallback.

This is the replacement for the old native `ProcessModeEnum.Always` characterization: the prompt's `UIProcessPolicy.Always` is now what makes the child responsive under Pause.

- [ ] **Step 4: Rewrite the gameplay load-failure expectation**

Change `HostedSaveLoad_LoadFailureClosesChildBeforeError` into:

```text
HostedSaveLoad_LoadFailureKeepsChildAndHostsRecoverablePrompt
```

Trigger the same missing slot data, then assert Pause + SaveLoad + Prompt are all active. Acknowledge Prompt and assert SaveLoad + Pause remain.

- [ ] **Step 5: Add missing-parent loud-failure coverage**

Add a focused Game test that invokes `ShowSaveError(...)` without active hosted Save/Load and captures Godot error output using the repository's existing error-monitoring helper/pattern. Assert no `UIScreenKinds.Prompt` entry is created and the expected error text identifies the missing Save/Load parent.

Do not add a fallback root prompt solely to satisfy this test.

- [ ] **Step 6: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest.HostedSaveLoad_LoadFailure|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_.*Recoverable|FullyQualifiedName~GameTest.ShowSaveError"
```

Expected: FAIL against the current native/close-parent behavior.

### 3C — Main Menu hosted prompt seam

- [ ] **Step 7: Replace Main Menu native message fields/helper**

Replace `_messageDialog : AcceptDialog` and `_messageCloseDelegate` with:

```csharp
private UIScreenHandle? _messageHandle;
private SiriusPrompt? _messagePrompt;
private Action? _messagePrimaryAction;
private Action? _messageCancelAction;
```

Keep the helper root-local; do not extract a shared presenter.

- [ ] **Step 8: Implement `TryOpenMessage` on the shared component**

Load `res://scenes/ui/components/SiriusPrompt.tscn`, configure either `Warning` or `RecoverableError`, and present with the same common host policy from Task 2. For a logical parent, pass `Parent = parent` and omit root `RestoreFocus`; for a root message, use the invoking control as `RestoreFocus`.

Main Menu only needs one-action prompts in this task, so both Primary and configured Cancel acknowledge through the same Primary signal.

- [ ] **Step 9: Preserve Main Menu Load as parent on failure**

Change `OnHostedLoadSlotSelected` failure from close + deferred root message to direct child presentation:

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

Do not close Load and do not defer a root prompt.

Map no-save to `Warning`; unavailable/failure paths to `RecoverableError`.

### 3D — gameplay Save/Load errors

- [ ] **Step 10: Move every gameplay failure before parent close**

For each failure branch in `OnHostedSaveSlotSelected` and `OnHostedLoadSlotSelected`, delete the preceding `TryCloseHostedSaveLoad(...)` and call `ShowSaveError(...)` while `_hostedSaveLoadHandle` is still active.

Successful save/load terminal paths keep their existing close/navigation behavior.

- [ ] **Step 11: Rewrite `ShowSaveError` as a hosted child with loud missing-parent handling**

Keep a simple private method:

```csharp
private void ShowSaveError(string message, string title = "Save Failed")
{
    if (_screenHost == null ||
        !GodotObject.IsInstanceValid(_screenHost) ||
        !_hostedSaveLoadHandle.HasValue ||
        !_screenHost.IsActive(_hostedSaveLoadHandle.Value))
    {
        GD.PushError("[Game] Cannot show save/load error without an active Save/Load parent.");
        return;
    }

    if (!TryOpenHostedPrompt(
            SiriusPromptVariant.RecoverableError,
            title,
            message,
            "OK",
            parent: _hostedSaveLoadHandle))
    {
        GD.PushError("[Game] Failed to present hosted save/load error prompt.");
    }
}
```

No root/native fallback.

- [ ] **Step 12: Delete `_activeErrorPopup` ownership only after hosted tests exist**

Delete:

```text
_activeErrorPopup field
HandleGameplayRootCancel branch that frees _activeErrorPopup
ShowSaveError native AcceptDialog creation
native Confirmed/Canceled handlers
_ExitTree cleanup for _activeErrorPopup
```

Do not add a replacement branch to `HandleGameplayRootCancel`: active Prompt is now the top input owner and consumes configured Cancel through the host before root fallback.

- [ ] **Step 13: Run GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~SiriusPromptTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS. The paused recoverable prompt closes while Pause remains tree-pause owner; SaveLoad remains active after recoverable acknowledgement; `MainMenu.cs` has no native message `AcceptDialog`.

- [ ] **Step 14: Commit Task 3**

```bash
git add scripts/ui/MainMenu.cs scripts/game/Game.cs \
  tests/ui/MainMenuTest.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameTest.cs
git commit -m "refactor(ui): host recoverable prompts"
```

---

## Task 4: Migrate corrupted-save presentation, retire product prompt kinds, and preserve host-test semantics

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostInputTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostFocusTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostProcessModeTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:**
- Consumes: Game-local `TryOpenHostedPrompt(...)` from Task 2.
- Produces: no new public API; removes native corrupted-save ownership and dead product prompt kinds.

### 4A — corrupted-save RED coverage

- [ ] **Step 1: Add root corrupted-save prompt tests**

Cover:

```text
CorruptedSave_OpensRecoverablePromptWithGameplayBlockWithoutTreePause
CorruptedSave_PrimaryRequestsMainMenuExactlyOnce
CorruptedSave_ConfiguredCancelMapsToPrimaryAndRequestsMainMenuExactlyOnce
CorruptedSave_SecondDetectionDoesNotOpenSecondPrompt
CorruptedSave_RootTeardownClearsPromptAndGameplayBlock
```

While active assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.IsProcessingInput()).IsTrue();
```

The last assertion proves `SetProcessInput(false)` is gone; host presentation blocking is the replacement.

- [ ] **Step 2: Run corrupted-save RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameTest.CorruptedSave|FullyQualifiedName~GameInputLifecycleTest.CorruptedSave|FullyQualifiedName~GameplayPauseHostTest.CorruptedSave"
```

Expected: FAIL because production still uses an unhosted native dialog/manual input disable.

- [ ] **Step 3: Replace `ShowCorruptedSaveError()` presentation only**

Keep `_hasShownCorruptedSaveError` and the existing domain validation/initialization-abort flow. Replace native presentation with:

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

Do not call `SetProcessInput(false)`. Do not add a native fallback. `RecoverableError.RequestCancel()` maps configured Cancel to Primary, so both visible action and Cancel take the same one-shot return path.

### 4B — retarget host fixture kinds before deleting constants

- [ ] **Step 4: Add test-local kind identities in every affected host suite**

Do **not** add a shared test-kind registry and do **not** add more product constants. Define only the identities a test file needs, for example:

```csharp
private static readonly StringName ModalFixture = new("modal_fixture");
```

and in `UIScreenStackModelTest` where two simultaneous kinds are required:

```csharp
private static readonly StringName BlockingPromptA = new("blocking_prompt_a");
private static readonly StringName BlockingPromptB = new("blocking_prompt_b");
private static readonly StringName ModalFixture = new("modal_fixture");
```

- [ ] **Step 5: Preserve `ExclusiveGroupConflict` coverage explicitly**

Rewrite `Open_DifferentConfirmationKindsShareBlockingPromptGroup` to use `BlockingPromptA` and `BlockingPromptB`, both in `UIScreenExclusiveGroups.BlockingPrompt`.

Keep the assertion:

```csharp
AssertThat(second.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
```

Never use `UIScreenKinds.Prompt` for both sides; that would produce `DuplicateKind` and stop testing the group contract.

- [ ] **Step 6: Retarget the remaining generic fixture references file-by-file**

Replace deleted `SaveError`, `ConfirmOverwrite`, or `ConfirmQuitToMain` identities only where they are generic host stand-ins:

```text
UIScreenStackModelTest.cs              -> local blocking/modal fixture kinds
UIScreenHostSubwindowTest.cs           -> local modal fixture kind
UIScreenHostInputTest.cs               -> local modal fixture kind
UIScreenHostFocusTest.cs               -> local modal/confirmation fixture kind(s)
UIScreenHostLifecycleTest.cs           -> local modal fixture kind
UIScreenHostContractScenarioTest.cs    -> local modal/confirmation fixture kind(s)
UIScreenHostProcessModeTest.cs         -> local modal fixture kind
```

Do not change the host policy under test—only the arbitrary identity used by the fixture.

- [ ] **Step 7: Run host tests before removing product constants**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenHostSubwindowTest|FullyQualifiedName~UIScreenHostInputTest|FullyQualifiedName~UIScreenHostFocusTest|FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostContractScenarioTest|FullyQualifiedName~UIScreenHostProcessModeTest"
```

Expected: PASS with original semantic assertions intact, especially `ExclusiveGroupConflict`.

### 4C — delete old product kinds and finish lifecycle docs

- [ ] **Step 8: Remove prompt-only product kinds**

After production and test fixtures no longer require them, delete:

```csharp
ConfirmOverwrite
ConfirmQuitToMain
SaveError
CorruptSaveError
```

Keep `UIScreenKinds.Prompt` and `UIScreenExclusiveGroups.BlockingPrompt`.

- [ ] **Step 9: Reconcile only changed HPA-376 rows**

Update rows describing:

```text
MAIN-MESSAGE
PAUSE-SAVELOAD nested overwrite/error behavior
PAUSE-QUIT-TO-MAIN
corrupted-save/load blocking-error behavior
```

Record final facts:

- one scene-authored `SiriusPrompt` component;
- one product `Prompt` kind;
- destructive/warning/recoverable presentation only;
- Save/Load parent retention;
- Pause keeps the single tree-pause lease while child prompt uses `Always`;
- configured Cancel is child-first through `RequestCancel()`;
- corrupted save uses RecoverableError chrome plus host gameplay block;
- exactly-once terminal latch and host focus/teardown cleanup.

Do not rewrite Dialogue/Shop/Healing/Puzzle/Reward rows or claim they are migrated.

### 4D — final verification

- [ ] **Step 10: Run focused GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptTest|FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenHost"
```

Expected: PASS.

- [ ] **Step 11: Run full validation**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: full suite PASS, 0 build errors, clean diff check.

- [ ] **Step 12: Run stale-reference audits**

Dedicated confirmations must be gone:

```bash
rg -n "SaveOverwriteConfirmation|PauseReturnToTitleConfirmation" scripts scenes tests
```

Expected: no matches.

Old product kinds must be gone from active code/tests:

```bash
rg -n "UIScreenKinds\.(ConfirmOverwrite|ConfirmQuitToMain|SaveError|CorruptSaveError)" scripts tests
```

Expected: no matches.

Migrated roots must contain no native prompt construction:

```bash
rg -n "AcceptDialog" scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

The new modal component must live only under component paths:

```bash
rg -n "SiriusPrompt" scripts/ui/components scenes/ui/components tests/ui/components scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: matches for the shared component and its two root consumers; no top-level `scripts/ui/SiriusPromptController.cs` / `scenes/ui/SiriusPrompt.tscn` files.

Deferred legacy dialogs are allowed:

```bash
rg -n "AcceptDialog" scripts/ui/DialogueDialog.cs scripts/ui/ShopDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs
```

Expected: matches are allowed and are not HPA-572 cleanup failures.

- [ ] **Step 13: Scope-audit the production diff**

The implementation diff may contain only:

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

- [ ] **Step 14: Commit Task 4**

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

After the four task commits, run the complete validation in Task 4 again from the final branch head. The implementation is complete only when the full suite/build pass, stale-reference audits return the expected results, and the changed-file scope matches this plan.
