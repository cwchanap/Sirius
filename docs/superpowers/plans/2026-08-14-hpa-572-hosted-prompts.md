# HPA-572 Host-Managed Prompts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's duplicate confirmation scenes and remaining Main Menu/gameplay native error dialogs with one scene-authored, host-managed prompt surface while preserving domain ownership, parent retention, focus, Cancel priority, and teardown behavior.

**Architecture:** Add one reusable `SiriusPrompt.tscn` / `SiriusPromptController` leaf on top of the existing `SiriusModalShell`. `Game` and `MainMenu` keep local `UIScreenHost` registration, prompt handles, and domain callbacks; no prompt service, presenter, singleton, router, or host facade is introduced. Migrate only the already-proven Save overwrite, Pause return-to-title, Main Menu messages, gameplay save/load errors, and corrupted-save startup error.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and `UIScreenHost`.

## Global Constraints

- Support exactly five variants: `InformationalConfirmation`, `DestructiveConfirmation`, `Warning`, `RecoverableError`, `BlockingError`.
- Reuse `SiriusModalShell`, `SiriusUiSeverity`, existing button theme variations, `SiriusUiMetrics.MinimumTarget(...)`, and `UIScreenExclusiveGroups.BlockingPrompt`.
- Do not add Theme tokens, icons, metrics, `SiriusModalShell` APIs, `UIScreenHost` APIs, global services, presenters, queues, recovery delegates, persistence, or acknowledgement IDs.
- `SiriusPromptController` owns presentation plus at-most-once terminal intent only. `Game` / `MainMenu` own every domain callback.
- Prompt Cancel must call `SiriusPromptController.RequestCancel()` and share the same terminal latch as visible actions. Do not use host `Cancel = Close` for prompts.
- Confirmation variants focus Cancel first. Warning/error variants focus their only primary action.
- Child prompts retain their logical parent; the host makes lower layers inert and restores parent focus when the prompt closes.
- Root Main Menu prompts may use `RestoreFocus` for the invoking root button.
- `BlockingError` maps configured Cancel to the same primary result as its visible action.
- Failed Save/Load operations keep the active Save/Load screen and present a recoverable child prompt.
- Do not migrate `DialogueDialog`, `ShopDialog`, `HealDialog`, `PuzzleRiddleDialog`, or reward presentation; HPA-569/570/571/573 own those slices.
- No compatibility shims for deleted prompt scenes/controllers/kinds.

---

## File Map

### Create

- `scenes/ui/SiriusPrompt.tscn` — shared scene-authored prompt layout.
- `scripts/ui/SiriusPromptController.cs` — five-variant mapping, responsive presentation, terminal latch, Cancel mapping.
- `tests/ui/SiriusPromptControllerTest.cs` — shared prompt behavior/layout coverage.

### Modify

- `scripts/ui/hosting/UIScreenKinds.cs` — add `Prompt`; remove obsolete prompt-only kinds after migration.
- `scripts/game/Game.cs` — Game-local prompt hosting and all gameplay prompt consumers.
- `scripts/ui/MainMenu.cs` — MainMenu-local prompt hosting and root/Load messages.
- `tests/game/GameplayPauseHostTest.cs` — nested prompt lifecycle, focus, programmatic close/parent teardown.
- `tests/game/GameInputLifecycleTest.cs` — configured Cancel child-first behavior and blocking error input behavior.
- `tests/game/GameTest.cs` — corrupted-save exactly-once/root-transition behavior where the existing Game fixture is the best seam.
- `tests/ui/MainMenuTest.cs` — warning/recoverable prompt hosting and Load-parent retention.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — update only migrated prompt/error rows.

### Delete after equivalent shared/integration coverage exists

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
- `scripts/ui/hosting/UIScreenContracts.cs`

Do not change audit-only files unless a focused RED test proves an existing shared contract is broken.

---

## Task 1: Add the shared `SiriusPrompt` leaf

**Files:**
- Create: `scenes/ui/SiriusPrompt.tscn`
- Create: `scripts/ui/SiriusPromptController.cs`
- Create: `tests/ui/SiriusPromptControllerTest.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`

**Interfaces:**

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

Add `public static readonly StringName Prompt = "prompt";` to `UIScreenKinds`. Keep old prompt-only kinds until Task 4 so intermediate commits compile.

- [ ] **Step 1: Write RED shared-prompt tests**

Create a `SubViewportContainer` + `SubViewport` fixture at 640×360, with a resize helper for 1280×720. Add:

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

Use this mapping table:

```csharp
var cases = new[]
{
    (SiriusPromptVariant.InformationalConfirmation,
        SiriusUiSeverity.Info, true, SiriusThemeTypes.PrimaryButton),
    (SiriusPromptVariant.DestructiveConfirmation,
        SiriusUiSeverity.Warning, true, SiriusThemeTypes.DestructiveButton),
    (SiriusPromptVariant.Warning,
        SiriusUiSeverity.Warning, false, SiriusThemeTypes.PrimaryButton),
    (SiriusPromptVariant.RecoverableError,
        SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton),
    (SiriusPromptVariant.BlockingError,
        SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton)
};
```

For `hasCancel == true`, assert `%CancelButton.Visible` and `InitialFocusTarget == %CancelButton`; otherwise assert Cancel hidden and initial focus is `%PrimaryButton`.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest"
```

Expected: compile/test failure because the controller/variant/scene do not exist.

- [ ] **Step 3: Author the scene**

Use this stable scene shape:

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

Author both buttons with a safe 44px minimum height. Runtime compact mapping may reduce them to the existing compact minimum target.

- [ ] **Step 4: Implement the minimal controller**

Store configuration so `Configure(...)` works before or after `_Ready()`:

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

Use only these mappings:

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

`RefreshPresentation()` applies title/message/severity, `SiriusUiMetrics.IsCompact(size)`, `MinimumTarget(compact)`, button visibility/text/theme, and calls `_shell.RefreshPresentation(size)`.

Terminal methods are at-most-once:

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

Wire `_primary.Pressed` to `EmitPrimaryOnce`, `_cancel.Pressed` to `EmitCancelOnce`, and `Resized` to presentation refresh. `_ExitTree()` disconnects those handlers. Do not add reuse/reset state; every presentation gets a fresh instance.

- [ ] **Step 5: Run GREEN and shell regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~SiriusModalShellTest"

dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS / 0 build errors; audit-only files remain unchanged.

- [ ] **Step 6: Commit**

```bash
git add scenes/ui/SiriusPrompt.tscn scripts/ui/SiriusPromptController.cs \
  scripts/ui/hosting/UIScreenKinds.cs tests/ui/SiriusPromptControllerTest.cs
git commit -m "feat(ui): add shared Sirius prompt"
```

---

## Task 2: Migrate Save overwrite and Pause return-to-title

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Delete the six dedicated confirmation scene/controller/test files from the File Map.

**Produces one Game-local seam reused by Tasks 3-4:**

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

with:

```csharp
private UIScreenHandle? _hostedPromptHandle;
private SiriusPromptController? _hostedPrompt;
private Action? _hostedPromptPrimaryAction;
private Action? _hostedPromptCancelAction;
```

- [ ] **Step 1: Add RED integration tests**

Add/adjust:

```text
HostedOverwrite_UsesSharedPromptAndCancelRestoresSaveLoad
HostedOverwrite_PrimaryInvokesSavePathOnce
PauseReturnToTitle_UsesSharedPromptAndCancelRestoresPause
PauseReturnToTitle_PrimaryRequestsNavigationOnce
HostedPrompt_ProgrammaticCloseClearsHandleAndBlockingGroup
HostedPrompt_ParentCloseRemovesDescendantAndClearsReferences
```

Update configured-Cancel overwrite coverage to expect `UIScreenKinds.Prompt`, not `ConfirmOverwrite`.

While a destructive prompt is open, assert one `Prompt` entry, parent still active/visible, Cancel is initial focus. After Cancel, assert prompt gone, parent remains, focus is restored into the parent.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.HostedOverwrite|FullyQualifiedName~GameplayPauseHostTest.PauseReturnToTitle|FullyQualifiedName~GameplayPauseHostTest.HostedPrompt|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_SaveLoadOverwrite"
```

Expected: FAIL because production still uses dedicated scenes/kinds.

- [ ] **Step 3: Implement Game-local prompt hosting**

The helper rejects invalid/missing host, teardown, an active `_hostedPromptHandle`, or an active `UIScreenKinds.Prompt`; loads/configures a fresh `SiriusPrompt`; subscribes terminal signals; then presents:

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

Use the host's default Control show/hide behavior; no custom `SetPresented` is needed.

Capture callbacks before close because cleanup clears them synchronously:

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

`ClearHostedPrompt(prompt)` disconnects signals and clears the handle/view/actions only when `ReferenceEquals(_hostedPrompt, prompt)`. Failed registration disconnects/queues only the candidate.

- [ ] **Step 4: Replace Save overwrite**

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

Keep slot/domain ownership in Game. Cancel just closes the child.

- [ ] **Step 5: Replace Pause return-to-title**

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

`ReturnToMainMenu()` and teardown-safe `RequestSceneChange(...)` remain unchanged.

- [ ] **Step 6: Delete dedicated confirmation implementations**

Delete the two scenes, two controllers, and two dedicated tests. Move reusable layout/latch/focus expectations into `SiriusPromptControllerTest`; keep host/domain assertions in gameplay lifecycle tests. Add no compatibility wrappers.

- [ ] **Step 7: Run GREEN/build and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
dotnet build Sirius.sln --no-restore --nologo

git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scenes/ui/SaveOverwriteConfirmation.tscn scripts/ui/SaveOverwriteConfirmationController.cs tests/ui/SaveOverwriteConfirmationControllerTest.cs \
  scenes/ui/PauseReturnToTitleConfirmation.tscn scripts/ui/PauseReturnToTitleConfirmationController.cs tests/ui/PauseReturnToTitleConfirmationControllerTest.cs
git commit -m "refactor(ui): share destructive prompts"
```

Expected: PASS / 0 build errors / no deleted-scene references.

---

## Task 3: Migrate recoverable Main Menu and gameplay errors while retaining parents

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/MainMenuTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Produces:** a MainMenu-local copy of the same private prompt-hosting contract. Do not extract a shared presenter/helper class.

- [ ] **Step 1: Add RED Main Menu tests**

```text
LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus
LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog
ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsRecoverablePrompt
HostedLoadFailure_KeepsLoadParentAndRestoresLoadFocusAfterAcknowledge
RootPrompt_RepeatedActivationDoesNotRunRootActionTwice
```

For Load-child failures, assert while open:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(loadScreen.Visible).IsTrue();
```

After acknowledgement, `Prompt` is gone and `SaveLoad` remains.

- [ ] **Step 2: Add RED gameplay save/load error tests**

```text
HostedSaveLoad_SaveValidationFailureShowsRecoverablePromptWithoutClosingSaveLoad
HostedSaveLoad_LoadFailureShowsRecoverablePromptWithoutClosingSaveLoad
ConfiguredCancel_RecoverablePromptClosesOnlyPromptAndLeavesSaveLoad
```

Configured Cancel maps to primary acknowledgement and must not fall through to close Save/Load or Pause.

- [ ] **Step 3: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest.HostedSaveLoad|FullyQualifiedName~GameInputLifecycleTest.ConfiguredCancel_RecoverablePrompt"
```

Expected: FAIL because Main Menu still hosts native messages and Game still closes Save/Load before native errors.

- [ ] **Step 4: Replace Main Menu native message state**

Replace `_messageHandle`, `_messageDialog`, `_messageCloseDelegate`, `TryOpenMessage`, `TryCloseHostedMessage`, and `ClearHostedMessage` with MainMenu-local prompt fields/helper/terminal handlers using the same host spec from Task 2.

Map current Main Menu messages exactly:

```text
No save files              -> Warning
Settings unavailable       -> RecoverableError
Load screen unavailable    -> RecoverableError
Continue load failed       -> RecoverableError
Manual hosted load failed  -> RecoverableError
```

Use primary text `OK`. Root messages pass the invoking root button as `restoreFocus`. Load-child failures pass `parent: _loadHandle` and no explicit restore target.

Rewrite manual hosted Load failure to keep Load active and open the error directly as a child; remove the current close-then-deferred-root-message ordering.

- [ ] **Step 5: Replace gameplay recoverable error presentation**

Reimplement the existing domain-facing method as hosted presentation:

```csharp
private bool ShowSaveError(string message, string title = "Save Failed")
{
    if (_screenHost == null ||
        !_hostedSaveLoadHandle.HasValue ||
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

In every Save/Load failure branch, remove the pre-error `TryCloseHostedSaveLoad(...)`. Preserve successful Save close and successful Load transition.

Because `ShowSaveError(...)` was the only owner of `_activeErrorPopup`, delete `_activeErrorPopup`, its root-Cancel special case, and its `_ExitTree()` cleanup in this task. `ShowCorruptedSaveError()` uses a separate local popup and remains for Task 4.

- [ ] **Step 6: Run GREEN/build and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusPromptControllerTest"
dotnet build Sirius.sln --no-restore --nologo

git add scripts/ui/MainMenu.cs scripts/game/Game.cs \
  tests/ui/MainMenuTest.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "refactor(ui): host recoverable prompts"
```

Expected: PASS; `scripts/ui/MainMenu.cs` contains no `AcceptDialog`; failed Save/Load acknowledgement returns to the same parent.

---

## Task 4: Migrate corrupted-save blocking error and finish stale cleanup

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Produces:** no new API. This removes the final native migrated-root prompt and obsolete host kinds.

- [ ] **Step 1: Add RED blocking-error tests**

```text
CorruptedSave_OpensRootBlockingPromptAndBlocksGameplayWithoutTreePause
CorruptedSave_PrimaryRequestsMainMenuExactlyOnce
CorruptedSave_ConfiguredCancelMapsToPrimaryAndRequestsMainMenuExactlyOnce
CorruptedSave_SecondDetectionDoesNotOpenSecondPrompt
CorruptedSave_RootTeardownClearsPromptAndGameplayBlock
```

While the prompt is active:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsTrue();
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.IsProcessingInput()).IsTrue();
```

`IsProcessingInput()` proves the old manual `SetProcessInput(false)` path is gone; the host provides the presentation gameplay block.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameTest.CorruptedSave|FullyQualifiedName~GameInputLifecycleTest.CorruptedSave|FullyQualifiedName~GameplayPauseHostTest.CorruptedSave"
```

Expected: FAIL because corrupted-save presentation is still an unhosted native dialog.

- [ ] **Step 3: Replace `ShowCorruptedSaveError()`**

Keep `_hasShownCorruptedSaveError` and all save-validation/domain behavior unchanged:

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

Delete only the native local popup construction, `SetProcessInput(false)`, its local Confirmed/Canceled handlers, and its local `handled` latch. Do not add a native fallback. Production `Game.tscn` owns the required `UIScreenHost`.

Configured Cancel already maps `BlockingError.RequestCancel()` to `PrimaryRequested`; the root handler closes the prompt, then invokes `ReturnToMainMenu()` once.

- [ ] **Step 4: Remove obsolete prompt-only kinds**

After all active call sites are gone, delete:

```csharp
UIScreenKinds.ConfirmOverwrite
UIScreenKinds.ConfirmQuitToMain
UIScreenKinds.SaveError
UIScreenKinds.CorruptSaveError
```

Keep `UIScreenKinds.Prompt` and `UIScreenExclusiveGroups.BlockingPrompt`.

- [ ] **Step 5: Reconcile only migrated lifecycle rows**

Update the current rows covering `MAIN-MESSAGE`, Save/Load nested overwrite/error behavior, `PAUSE-QUIT-TO-MAIN`, and corrupted-save blocking behavior. Record: one scene-authored prompt kind, logical-parent retention, child-first Cancel, host focus restoration, host-owned corrupted-save input block, and the controller terminal latch.

Do not rewrite dialogue/shop/healing/puzzle/reward rows or claim they are migrated.

- [ ] **Step 6: Run focused GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusPromptControllerTest|FullyQualifiedName~MainMenuTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: PASS.

- [ ] **Step 7: Run full verification**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected: full suite PASS, 0 build errors, clean diff check.

- [ ] **Step 8: Run stale-reference/scope audits**

Dedicated prompt implementations:

```bash
rg -n "SaveOverwriteConfirmation|PauseReturnToTitleConfirmation" scripts scenes tests
```

Expected: no matches.

Old kinds:

```bash
rg -n "UIScreenKinds\.(ConfirmOverwrite|ConfirmQuitToMain|SaveError|CorruptSaveError)" scripts tests
```

Expected: no matches.

No native prompt construction in migrated roots:

```bash
rg -n "new AcceptDialog|AcceptDialog" scripts/game/Game.cs scripts/ui/MainMenu.cs
```

Expected: no matches.

Deferred legacy screens are allowed to retain native dialogs:

```bash
rg -n "AcceptDialog" scripts/ui/DialogueDialog.cs scripts/ui/ShopDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs
```

Expected: matches are allowed and are **not** HPA-572 failures.

Audit the final production diff: shared prompt, `Game`, `MainMenu`, `UIScreenKinds`, targeted tests, deletion of the two dedicated confirmations, and lifecycle contract only. Reject unrelated refactors.

- [ ] **Step 9: Commit**

```bash
git add scripts/game/Game.cs scripts/ui/hosting/UIScreenKinds.cs \
  tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "refactor(ui): host blocking errors"
```

---

## Final Acceptance Review

Before opening the implementation PR, verify evidence for every HPA-572 requirement:

- [ ] One `SiriusPrompt` scene/controller provides all five variants.
- [ ] Save overwrite and Pause return-to-title use the shared destructive confirmation; duplicate scenes/controllers/tests are gone.
- [ ] Main Menu and gameplay migrated warning/recoverable paths no longer construct native `AcceptDialog`s.
- [ ] Failed Save/Load retains active Save/Load under the recoverable child prompt.
- [ ] Corrupted-save startup uses root `BlockingError`; Game input processing remains enabled while host gameplay blocking is active.
- [ ] Configured Cancel is child-first and reaches the same controller terminal latch as visible actions.
- [ ] Closing a confirmation restores its logical parent; closing a root Main Menu prompt restores its invoking button when valid.
- [ ] Repeated terminal activation cannot run a domain action twice.
- [ ] Programmatic close, parent close, and root teardown leave no prompt entry, blocking-group ownership, input block, or stale root reference.
- [ ] Representative long text remains usable at 640×360.
- [ ] Dialogue, Shop, Healing, Puzzle/Riddle, and Reward paths stay outside the implementation diff.

## Implementation Notes

- Use fresh prompt instances; do not add controller reset/reuse machinery.
- Keep callback capture-before-close ordering because host cleanup clears root-owned callbacks synchronously.
- Failed `TryPresent` disconnects and frees only its candidate; it must not disturb an existing prompt or parent.
- Do not open a replacement prompt from a host cleanup callback. If a future flow needs replacement presentation, return from the host mutation first; HPA-572 needs no queue.
- If a layout test exposes a shared-shell defect, first pin it in `SiriusModalShellTest`. Do not add prompt-local sizing hacks or speculative shell APIs.
