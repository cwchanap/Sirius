# HPA-572 Host-Managed Prompts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's duplicate confirmation scenes and remaining Main Menu/gameplay native error dialogs with one scene-authored, host-managed prompt surface while preserving domain ownership, parent retention, focus, Cancel priority, and teardown behavior.

**Architecture:** Add one reusable `SiriusPrompt.tscn` / `SiriusPromptController` leaf on top of the existing `SiriusModalShell`. `Game` and `MainMenu` keep local `UIScreenHost` registration, prompt handles, and domain callbacks; no prompt service, presenter, singleton, router, or host facade is introduced. Migrate only the already-proven Save overwrite, Pause return-to-title, Main Menu messages, gameplay save/load errors, and corrupted-save startup error.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and `UIScreenHost`.

## Global Constraints

- Support exactly five prompt variants: `InformationalConfirmation`, `DestructiveConfirmation`, `Warning`, `RecoverableError`, and `BlockingError`.
- Reuse `SiriusModalShell`, `SiriusUiSeverity`, existing button theme variations, `SiriusUiMetrics.MinimumTarget(...)`, and `UIScreenExclusiveGroups.BlockingPrompt`.
- Do not add Theme tokens, icons, metrics, `SiriusModalShell` APIs, `UIScreenHost` APIs, notification queues, global services, presenters, recovery delegates, persistence, or acknowledgement IDs.
- Keep domain actions in `Game` / `MainMenu`; `SiriusPromptController` owns presentation plus at-most-once terminal intent only.
- Prompt Cancel must be routed through `SiriusPromptController.RequestCancel()` and the same terminal latch as visible actions; host `Cancel = Close` is not sufficient.
- Confirmation variants focus Cancel first. Warning/error variants focus their only primary action.
- Child prompts keep their logical parent active/inert and restore the parent through `UIScreenHost` focus handling.
- Root Main Menu prompts may restore the invoking root control through `RestoreFocus`.
- `BlockingError` maps configured Cancel to the same primary terminal result as its visible action.
- Save/Load failures keep the active Save/Load screen open and show a recoverable child prompt instead of closing the parent first.
- Do not migrate `DialogueDialog`, `ShopDialog`, `HealDialog`, `PuzzleRiddleDialog`, or reward presentation; HPA-569/570/571/573 own those slices.
- No compatibility shims for deleted prompt scenes/controllers/kinds are required.

---

## File Map

### Create

- `scenes/ui/SiriusPrompt.tscn` — one scene-authored prompt layout built from `SiriusModalShell`.
- `scripts/ui/SiriusPromptController.cs` — five-variant presentation mapping, terminal latch, and Cancel mapping.
- `tests/ui/SiriusPromptControllerTest.cs` — variant, terminal, responsive, long-text, and scene-contract coverage.

### Modify

- `scripts/ui/hosting/UIScreenKinds.cs` — add `Prompt`; remove obsolete prompt-only kinds after migration.
- `scripts/game/Game.cs` — local hosted-prompt plumbing, Save overwrite, Pause return-to-title, save/load errors, corrupted-save blocking error.
- `scripts/ui/MainMenu.cs` — replace hosted native messages with `SiriusPrompt` and retain Load as parent on failures.
- `tests/game/GameplayPauseHostTest.cs` — nested confirmation/error parent retention, focus restoration, programmatic close/teardown.
- `tests/game/GameInputLifecycleTest.cs` — configured Cancel child-first behavior and corrupted-save terminal behavior.
- `tests/game/GameTest.cs` — root/gameplay error state and exactly-once transition coverage where existing Game fixtures are the better seam.
- `tests/ui/MainMenuTest.cs` — warning/recoverable prompt hosting, Load-parent retention, root-focus restoration, no native message dialog.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — update only migrated prompt/error rows.

### Delete after equivalent coverage exists

- `scenes/ui/SaveOverwriteConfirmation.tscn`
- `scripts/ui/SaveOverwriteConfirmationController.cs`
- `tests/ui/SaveOverwriteConfirmationControllerTest.cs`
- `scenes/ui/PauseReturnToTitleConfirmation.tscn`
- `scripts/ui/PauseReturnToTitleConfirmationController.cs`
- `tests/ui/PauseReturnToTitleConfirmationControllerTest.cs`

### Audit-only

- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/theme/SiriusUiTypes.cs`
- `scripts/ui/theme/SiriusThemeTypes.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenEntrySpec.cs`

Do not change audit-only files unless a focused RED test proves the existing shared contract is actually broken.

---

## Task 1: Add the shared `SiriusPrompt` leaf

**Files:**
- Create: `scenes/ui/SiriusPrompt.tscn`
- Create: `scripts/ui/SiriusPromptController.cs`
- Create: `tests/ui/SiriusPromptControllerTest.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`

**Interfaces:**
- Consumes: `SiriusModalShell`, `SiriusUiSeverity`, `SiriusThemeTypes`, `SiriusUiMetrics.MinimumTarget(...)`.
- Produces:

```csharp
public enum SiriusPromptVariant
{
    InformationalConfirmation,
    DestructiveConfirmation,
    Warning,
    RecoverableError,
    BlockingError
}

public partial class SiriusPromptController : Control
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

Also add:

```csharp
public static readonly StringName Prompt = "prompt";
```

to `UIScreenKinds`. Keep the old prompt-only kinds until all production call sites are migrated.

- [ ] **Step 1: Add RED shared-prompt tests**

Create `tests/ui/SiriusPromptControllerTest.cs` with a `SubViewportContainer` + `SubViewport` fixture at 640×360 by default and explicit helpers to resize to 1280×720.

Cover these tests before production code exists:

```text
AllVariants_MapSeverityButtonsStylesAndInitialFocus
InformationalConfirmation_PrimaryThenCancelEmitsOnlyPrimaryOnce
DestructiveConfirmation_CancelThenPrimaryEmitsOnlyCancelOnce
Warning_RequestCancelMapsToPrimaryOnce
RecoverableError_RequestCancelMapsToPrimaryOnce
BlockingError_RequestCancelMapsToPrimaryOnce
CompactViewport_UsesCompactShellAndMinimumTargets
CrossingCompactToStandard_RefreshesShellAndTargets
LongMessage_MinimumViewportRemainsInsideShellAndBodyCanScroll
Scene_UsesSiriusModalShellAndContainsNoAcceptDialog
```

The variant table asserted by the first test is:

```csharp
var cases = new[]
{
    (SiriusPromptVariant.InformationalConfirmation,
        SiriusUiSeverity.Info, true, SiriusThemeTypes.PrimaryButton, true),
    (SiriusPromptVariant.DestructiveConfirmation,
        SiriusUiSeverity.Warning, true, SiriusThemeTypes.DestructiveButton, true),
    (SiriusPromptVariant.Warning,
        SiriusUiSeverity.Warning, false, SiriusThemeTypes.PrimaryButton, false),
    (SiriusPromptVariant.RecoverableError,
        SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton, false),
    (SiriusPromptVariant.BlockingError,
        SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton, false)
};
```

For `hasCancel == true`, assert `%CancelButton.Visible` and `InitialFocusTarget == %CancelButton`; otherwise assert Cancel hidden and initial focus equals `%PrimaryButton`.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest"
```

Expected: compile/test failure because `SiriusPromptController`, `SiriusPromptVariant`, and the scene do not exist.

- [ ] **Step 3: Author the scene tree**

Create `scenes/ui/SiriusPrompt.tscn` with this stable shape:

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

Both buttons are authored at 44 px minimum height so the scene has a safe editor/default state before runtime compact mapping.

- [ ] **Step 4: Implement the minimal controller**

Use private stored configuration so `Configure(...)` works both before and after `_Ready()`:

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

public Control InitialFocusTarget => HasCancel(_variant) ? _cancel : _primary;
```

`Configure(...)` stores normalized non-null strings and calls `RefreshPresentation()` when ready. `_Ready()` binds scene nodes, wires the two `Pressed` signals and `Resized`, then calls `RefreshPresentation()`.

Use these pure mappings:

```csharp
private static bool HasCancel(SiriusPromptVariant variant) =>
    variant is SiriusPromptVariant.InformationalConfirmation
        or SiriusPromptVariant.DestructiveConfirmation;

private static SiriusUiSeverity SeverityFor(SiriusPromptVariant variant) =>
    variant switch
    {
        SiriusPromptVariant.InformationalConfirmation => SiriusUiSeverity.Info,
        SiriusPromptVariant.DestructiveConfirmation => SiriusUiSeverity.Warning,
        SiriusPromptVariant.Warning => SiriusUiSeverity.Warning,
        SiriusPromptVariant.RecoverableError => SiriusUiSeverity.Error,
        SiriusPromptVariant.BlockingError => SiriusUiSeverity.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
    };

private static StringName PrimaryThemeFor(SiriusPromptVariant variant) =>
    variant == SiriusPromptVariant.DestructiveConfirmation
        ? SiriusThemeTypes.DestructiveButton
        : SiriusThemeTypes.PrimaryButton;
```

Refresh compact presentation from the current viewport:

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
_cancel.Visible = HasCancel(_variant);
_cancel.Text = _cancelActionText;
_cancel.ThemeTypeVariation = SiriusThemeTypes.SecondaryButton;
_cancel.CustomMinimumSize = new Vector2(0, target.Y);
```

Terminal handling is one-shot for the lifetime of the prompt instance:

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
    if (HasCancel(_variant))
        EmitCancelOnce();
    else
        EmitPrimaryOnce();
}
```

`_ExitTree()` disconnects button/resize handlers. Do not reset `_terminalEmitted`; every host presentation uses a fresh prompt instance.

- [ ] **Step 5: Run GREEN and shared-shell regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~SiriusModalShellTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors. `SiriusModalShell.cs`, Theme, metrics, and host remain unchanged.

- [ ] **Step 6: Commit Task 1**

```bash
git add scenes/ui/SiriusPrompt.tscn \
  scripts/ui/SiriusPromptController.cs \
  scripts/ui/hosting/UIScreenKinds.cs \
  tests/ui/SiriusPromptControllerTest.cs
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
- Consumes: `SiriusPromptController.Configure(...)`, `RequestCancel()`, `PrimaryRequested`, `CancelRequested`, `UIScreenKinds.Prompt`.
- Produces one Game-local hosted prompt seam; later tasks reuse exactly this shape:

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

with Game-owned fields:

```csharp
private UIScreenHandle? _hostedPromptHandle;
private SiriusPromptController? _hostedPrompt;
private Action? _hostedPromptPrimaryAction;
private Action? _hostedPromptCancelAction;
```

- [ ] **Step 1: Add RED integration assertions before replacing anything**

In `GameplayPauseHostTest.cs`, add/adjust tests so the desired final contract is explicit:

```text
HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad
HostedOverwrite_PrimaryInvokesSavePathOnce
PauseReturnToTitle_UsesSharedPromptAndCancelRestoresPause
PauseReturnToTitle_PrimaryRequestsNavigationOnce
HostedPrompt_ProgrammaticCloseClearsHandleAndBlockingGroup
HostedPrompt_ParentCloseRemovesDescendantAndClearsReferences
```

Assertions for both destructive confirmations:

```csharp
AssertThat(host.ActiveEntries.Count(entry => entry.Kind == UIScreenKinds.Prompt))
    .IsEqual(1);
AssertThat(prompt.InitialFocusTarget)
    .IsEqual(prompt.GetNode<Button>("%CancelButton"));
AssertThat(parent.Visible).IsTrue();
```

After Cancel, assert the prompt kind is absent, the parent kind remains active, and viewport focus returns to a control inside that parent.

Update the configured-Cancel overwrite test in `GameInputLifecycleTest.cs` to expect the shared `Prompt` child rather than `ConfirmOverwrite`.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.HostedOverwrite|FullyQualifiedName~GameplayPauseHostTest.PauseReturnToTitle|FullyQualifiedName~GameplayPauseHostTest.HostedPrompt|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_SaveLoadOverwrite"
```

Expected: FAIL because production still opens feature-specific scenes/kinds.

- [ ] **Step 3: Add the Game-local prompt host helper**

`TryOpenHostedPrompt(...)` must:

1. reject missing/invalid host or scene teardown;
2. reject a still-active `_hostedPromptHandle` / `UIScreenKinds.Prompt`;
3. load `res://scenes/ui/SiriusPrompt.tscn`;
4. instantiate/configure a fresh `SiriusPromptController`;
5. subscribe `PrimaryRequested` / `CancelRequested`;
6. present with this policy:

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
    RestoreFocus = parent.HasValue || restoreFocus == null
        ? null
        : () => restoreFocus,
    Cleanup = _ => ClearHostedPrompt(prompt),
    NodeLifetime = UINodeLifetime.QueueFree
});
```

There is deliberately no custom `SetPresented`; Control defaults are sufficient.

Terminal handlers capture the root callback before closing because cleanup clears the stored delegates:

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

`ClearHostedPrompt(prompt)` disconnects signals and clears handle/view/actions only when `ReferenceEquals(_hostedPrompt, prompt)`. Failed registration disconnects and queues the candidate directly.

- [ ] **Step 4: Replace Save overwrite**

Replace `TryOpenHostedOverwriteConfirmation(slot)` and its dedicated fields/callbacks with:

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

Cancel has no domain callback: closing the child is the whole result. The captured slot remains root-owned; no payload is added to `SiriusPromptController`.

- [ ] **Step 5: Replace Pause return-to-title**

Replace the dedicated return-confirmation scene/controller/fields with:

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

`ReturnToMainMenu()` remains unchanged and still routes through teardown-safe `RequestSceneChange(...)`.

- [ ] **Step 6: Delete the two dedicated prompt implementations and migrate their tests**

Delete all six feature-specific scene/controller/test files listed above. Their useful latch/layout/focus assertions now live in `SiriusPromptControllerTest`; host/domain behavior lives in `GameplayPauseHostTest` / `GameInputLifecycleTest`.

Do not preserve compatibility wrappers or old scene paths.

- [ ] **Step 7: Run GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS; no references to either deleted controller/scene remain.

- [ ] **Step 8: Commit Task 2**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scenes/ui/SaveOverwriteConfirmation.tscn scripts/ui/SaveOverwriteConfirmationController.cs tests/ui/SaveOverwriteConfirmationControllerTest.cs \
  scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "refactor(ui): share destructive prompts"
```

---

## Task 3: Migrate recoverable Main Menu and gameplay errors while retaining parents

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/MainMenuTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Interfaces:**
- Consumes: the Game-local prompt seam from Task 2.
- Produces a MainMenu-local prompt seam with the same controller/host contract but no shared helper class.

- [ ] **Step 1: Add RED Main Menu warning/recoverable tests**

In `MainMenuTest.cs`, add/replace coverage for:

```text
LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus
LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog
ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsRecoverablePrompt
HostedLoadFailure_KeepsLoadParentAndRestoresLoadFocusAfterAcknowledge
RootPrompt_DoubleActivationInvokesNoRootActionTwice
```

The parent-retention tests must assert while the error is open:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(loadScreen.Visible).IsTrue();
```

After acknowledgement, assert `Prompt` is gone while `SaveLoad` remains.

- [ ] **Step 2: Add RED gameplay save/load error tests**

In `GameplayPauseHostTest.cs` / `GameInputLifecycleTest.cs`, cover one representative validation failure and one load failure:

```text
HostedSaveLoad_SaveValidationFailureShowsRecoverablePromptWithoutClosingSaveLoad
HostedSaveLoad_LoadFailureShowsRecoverablePromptWithoutClosingSaveLoad
ConfiguredCancel_RecoverablePromptClosesOnlyPromptAndLeavesSaveLoad
```

For configured Cancel, the prompt controller maps Cancel to primary acknowledgement; the same input must not then close Save/Load or Pause.

- [ ] **Step 3: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest.HostedSaveLoad|FullyQualifiedName~GameInputLifecycleTest.ConfiguredCancel_RecoverablePrompt"
```

Expected: FAIL because Main Menu still hosts native `AcceptDialog` messages and Game closes Save/Load before native save/load errors.

- [ ] **Step 4: Replace Main Menu message state with MainMenu-local prompt state**

Replace:

```text
_messageHandle
_messageDialog
_messageCloseDelegate
TryOpenMessage(...)
TryCloseHostedMessage(...)
ClearHostedMessage(...)
```

with the same four prompt fields/terminal handlers used in Game and a private `TryOpenHostedPrompt(...)` method. Keep this helper private to `MainMenu`; do not extract a cross-root service.

Use the same host policy as Task 2, including:

```csharp
Cancel = UICancelPolicy.Consume,
InterceptCancel = _ =>
{
    prompt.RequestCancel();
    return UIInputInterception.ConsumeHere;
},
```

Root Main Menu calls pass `restoreFocus`; child Load errors pass `parent: _loadHandle` and no explicit restore target.

- [ ] **Step 5: Map Main Menu call sites by intent**

Use exactly these variants:

```text
No save files                         -> Warning
Settings scene unavailable            -> RecoverableError
Load scene unavailable                -> RecoverableError
Continue load failed                  -> RecoverableError
Manual hosted Load failed             -> RecoverableError
```

Use primary text `OK` for warning/recoverable acknowledgement.

Rewrite `OnHostedLoadSlotSelected(int slot)` failure so it does **not** call `TryCloseHostedLoad(...)` and does not defer a root message. Instead:

```csharp
TryOpenHostedPrompt(
    SiriusPromptVariant.RecoverableError,
    "Load Failed",
    "Failed to load save file.",
    "OK",
    parent: _loadHandle);
```

`HandleContinueLoadResult(...)` already opens hosted Load as a fallback; its error should likewise be a child of that Load handle.

- [ ] **Step 6: Reimplement gameplay `ShowSaveError(...)` as a hosted recoverable child**

Keep the existing small domain-facing method name if it avoids call-site churn, but replace its body with hosted presentation only:

```csharp
private bool ShowSaveError(string message, string title = "Save Failed")
{
    if (!_hostedSaveLoadHandle.HasValue ||
        _screenHost == null ||
        !_screenHost.IsActive(_hostedSaveLoadHandle.Value))
        return false;

    return TryOpenHostedPrompt(
        SiriusPromptVariant.RecoverableError,
        title,
        message,
        "OK",
        parent: _hostedSaveLoadHandle);
}
```

For every save/load failure branch in `OnHostedSaveSlotSelected(...)` and `OnHostedLoadSlotSelected(...)`, remove the pre-error `TryCloseHostedSaveLoad(...)`. Preserve the existing successful Save close and successful Load scene-transition behavior.

- [ ] **Step 7: Remove `_activeErrorPopup` ownership for recoverable errors**

Delete native popup construction and the root-Cancel branch that frees `_activeErrorPopup`. Do **not** yet migrate/remove `ShowCorruptedSaveError()` in this step; Task 4 owns the blocking startup path atomically.

If `_activeErrorPopup` is still referenced only by `ShowCorruptedSaveError()` after this edit, leave the field until Task 4 rather than creating an intermediate compile break.

- [ ] **Step 8: Run GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusPromptControllerTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS. Save/Load error acknowledgement leaves the parent active; `scripts/ui/MainMenu.cs` contains no `AcceptDialog` construction.

- [ ] **Step 9: Commit Task 3**

```bash
git add scripts/ui/MainMenu.cs scripts/game/Game.cs \
  tests/ui/MainMenuTest.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "refactor(ui): host recoverable prompts"
```

---

## Task 4: Migrate corrupted-save blocking error and finish stale cleanup

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:**
- Consumes: Game-local `TryOpenHostedPrompt(...)` from Task 2.
- Produces: no new public API; this task removes native blocking-error ownership and obsolete prompt kinds.

- [ ] **Step 1: Add RED corrupted-save blocking tests**

Cover the production contract through the existing Game/host fixture seams:

```text
CorruptedSave_OpensRootBlockingPromptAndBlocksGameplayWithoutTreePause
CorruptedSave_PrimaryRequestsMainMenuExactlyOnce
CorruptedSave_ConfiguredCancelMapsToPrimaryAndRequestsMainMenuExactlyOnce
CorruptedSave_SecondDetectionDoesNotOpenSecondPrompt
CorruptedSave_RootTeardownClearsPromptAndGameplayBlock
```

While the prompt is active, assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(host.CurrentState.GameplayInputBlocked).IsTrue();
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.IsProcessingInput()).IsTrue();
```

The last assertion deliberately proves HPA-572 removed manual `SetProcessInput(false)`; presentation blocking comes from the host instead.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameTest.CorruptedSave|FullyQualifiedName~GameInputLifecycleTest.CorruptedSave|FullyQualifiedName~GameplayPauseHostTest.CorruptedSave"
```

Expected: FAIL because `ShowCorruptedSaveError()` still creates an unhosted native dialog and disables Game input.

- [ ] **Step 3: Replace `ShowCorruptedSaveError()` with `BlockingError`**

Keep `_hasShownCorruptedSaveError` and current initialization-abort/domain validation unchanged. Replace only presentation:

```csharp
private void ShowCorruptedSaveError()
{
    if (_hasShownCorruptedSaveError)
        return;

    _hasShownCorruptedSaveError = true;

    TryOpenHostedPrompt(
        SiriusPromptVariant.BlockingError,
        "Load Failed",
        "Save file is corrupted or invalid.\nReturning to main menu.",
        "Return to Title",
        onPrimary: ReturnToMainMenu,
        blockGameplayInput: true);
}
```

Do not call `SetProcessInput(false)`. Do not create a native fallback dialog. The production Game scene already owns `UIScreenHost`; if prompt creation unexpectedly fails, keep the existing logged failure behavior rather than introducing a second presentation path.

Configured Cancel is already handled by the shared host spec: `BlockingError.RequestCancel()` emits the same primary terminal signal, which closes the prompt and invokes `ReturnToMainMenu()` once.

- [ ] **Step 4: Remove stale native error state and cleanup branches**

Delete:

```text
_activeErrorPopup
manual QueueFree cleanup for that popup
root-Cancel special case for that popup
SetProcessInput(false) from corrupted-save handling
native Confirmed/Canceled handlers and local handled latch
```

The prompt controller latch plus root scene-transition guard now cover duplicate terminal activation.

- [ ] **Step 5: Remove obsolete prompt-only kinds**

After all active references are gone, delete from `UIScreenKinds.cs`:

```csharp
ConfirmOverwrite
ConfirmQuitToMain
SaveError
CorruptSaveError
```

Keep `UIScreenKinds.Prompt` and `UIScreenExclusiveGroups.BlockingPrompt`.

- [ ] **Step 6: Reconcile only the migrated HPA-376 lifecycle rows**

Update the existing rows that describe:

```text
MAIN-MESSAGE
PAUSE-SAVELOAD nested overwrite/error behavior
PAUSE-QUIT-TO-MAIN
corrupted-save/load blocking error behavior
```

Record the final facts: scene-authored `SiriusPrompt`, one prompt kind, logical parent retention, child-first Cancel, host focus restoration, host-owned input block for corrupted save, and exactly-once terminal latch. Do not rewrite dialogue/shop/healing/puzzle rows or claim they are migrated.

- [ ] **Step 7: Run focused GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: PASS.

- [ ] **Step 8: Run full validation**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: full suite PASS, 0 build errors, clean diff check.

- [ ] **Step 9: Run stale-reference and scope audits**

Dedicated prompt implementations must be fully gone:

```bash
rg -n "SaveOverwriteConfirmation|PauseReturnToTitleConfirmation" scripts scenes tests
```

Expected: no matches.

Old host kinds must be gone from active code/tests:

```bash
rg -n "UIScreenKinds\.(ConfirmOverwrite|ConfirmQuitToMain|SaveError|CorruptSaveError)" scripts tests
```

Expected: no matches.

No native prompt construction may remain in the migrated roots:

```bash
rg -n "new AcceptDialog|AcceptDialog" scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

Deferred legacy screens are explicitly allowed to retain their native dialog code:

```bash
rg -n "AcceptDialog" scripts/ui/DialogueDialog.cs scripts/ui/ShopDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs
```

Expected: matches are allowed and are **not** HPA-572 cleanup failures.

Final production diff scope should be limited to the shared prompt, two invoking roots, tests, `UIScreenKinds`, deletion of the two dedicated confirmations, and the lifecycle contract. Do not accept unrelated refactors discovered during implementation.

- [ ] **Step 10: Commit Task 4**

```bash
git add scripts/game/Game.cs scripts/ui/hosting/UIScreenKinds.cs \
  tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "refactor(ui): host blocking errors"
```

---

## Final Acceptance Review

Before opening the implementation PR, verify every HPA-572 acceptance item maps to evidence:

- [ ] One `SiriusPrompt` scene/controller implements all five variants.
- [ ] Save overwrite and Pause return-to-title use `DestructiveConfirmation`; dedicated scenes/controllers/tests are deleted.
- [ ] Main Menu warnings/errors and gameplay save/load errors no longer construct native `AcceptDialog`s.
- [ ] Failed Save/Load retains the active Save/Load parent under the recoverable prompt.
- [ ] Corrupted-save startup uses root `BlockingError`; Game input processing stays enabled while host gameplay blocking is active.
- [ ] Configured Cancel is child-first and reaches the same prompt terminal latch as visible actions.
- [ ] Confirmation Cancel restores the logical parent; root Main Menu prompt close restores the invoking root control.
- [ ] Repeated terminal activation cannot run a domain action twice.
- [ ] Programmatic close, parent close, and root teardown leave no active prompt handle, exclusive-group ownership, input block, or stale root reference.
- [ ] Long text remains usable at 640×360.
- [ ] Dialogue, Shop, Healing, Puzzle/Riddle, and Reward paths remain outside the implementation diff.

## Implementation Notes

- Use fresh prompt instances. Do not add reset/reuse state to `SiriusPromptController`.
- Keep the root callback capture-before-close ordering; host cleanup clears stored actions synchronously.
- A failed `TryPresent` must disconnect signals and queue-free only the candidate; it must not disturb an existing prompt/parent.
- Do not open replacement prompts from a host cleanup callback. If a later flow ever needs that behavior, return from the host mutation and defer at the owning root; HPA-572 does not need such a queue.
- If a shared-shell layout test fails, first prove the defect belongs to `SiriusModalShell` with its own focused regression before changing the shell. Do not add prompt-local sizing hacks or speculative shell APIs.
