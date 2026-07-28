# Sirius UI Lifecycle Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce the HPA-376 lifecycle/state contract and automated regression baseline that later `UIScreenHost` work can implement without rediscovering current Sirius UI ownership, input priority, restoration, and cleanup behavior.

**Architecture:** Keep the existing controllers and `Game._Input()` coordination in place. Characterize each local controller at its public signal/visibility/pause boundary, place cross-controller priority tests in a focused `GameInputLifecycleTest`, and make only the four HPA-376-owned corrections approved by the design: Inventory pause snapshots, exactly-once terminal signals, visible battle-result routing, and lifecycle-owned defeat navigation.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, the existing `Sirius.sln`, and `test.runsettings.local`.

## Global Constraints

- The normative design is `docs/superpowers/specs/2026-07-26-sirius-ui-lifecycle-baseline-design.md` version 1.3.
- Do not add `UIScreenHost`, a navigation service, a shared screen interface, a theme, or redesigned layouts.
- Do not move coordination out of `Game._Input()` or change combat, save, inventory, settings, NPC, puzzle, treasure, or reward domain rules.
- Preserve the three current input blockers: `GameManager.IsInBattle`, `IsInNpcInteraction`, and `IsInWorldInteraction`.
- Use only `Preserve`, `Fix in HPA-376`, and `Replace in HPA-378/379` dispositions in the lifecycle contract.
- Keep root Pause tree ownership, process-mode coordination, mouse policy, common focus fallback, and Inventory-from-Pause navigation as downstream HPA-378/379 work.
- Keep reward identity and handoff guarantees in HPA-393; HPA-376 documents toast/constellation targets but creates no reward event or presentation system.
- Trap-tile damage is a gameplay-domain event, not a standalone UI lifecycle row.
- Every `Preserve` or `Fix in HPA-376` row must name a test method; every replacement row must name its downstream owner and complete target behavior.
- New tests must not add orphan-node warnings. Preserve the historical evidence of 869 passing tests at `bc82ead`, but record the actual implementation-start total rather than hard-coding 869 as the expected total.
- Use TDD for each production correction: observe the failing test, implement the smallest correction, and rerun the focused suite before committing.

---

## File Structure

### Create

- `docs/ui/hpa-376/ui-lifecycle-contract.md`: committed 50-row lifecycle inventory, modal-priority matrix, evidence map, baseline/after verification, and downstream handoff.
- `tests/ui/MainMenuTest.cs`: root load/settings/message/quit lifecycle coverage without terminating the test runner.
- `tests/ui/SaveLoadDialogTest.cs`: parent/child dismissal, terminal-signal idempotency, slot handoff, and Main Menu action coverage.
- `tests/ui/DialogueDialogTest.cs`: dialogue close/outcome exclusivity.
- `tests/ui/HealDialogTest.cs`: heal/cancel terminal exclusivity.
- `tests/ui/PuzzleRiddleDialogTest.cs`: choice/cancel terminal exclusivity.
- `tests/ui/NpcInteractionControllerTest.cs`: dialogue-to-shop/heal transitions and exactly-once interaction completion.
- `tests/game/GameInputLifecycleTest.cs`: cross-controller Cancel priority, deferred restoration, errors, battle results, defeat navigation, prompt visibility, and floor replacement.

### Modify

- `scripts/ui/InventoryMenuController.cs`: snapshot and restore the incoming `SceneTree.Paused` value.
- `tests/ui/InventoryMenuControllerTest.cs`: open/close/input/cleanup pause lifecycle coverage.
- `scripts/ui/SettingsMenuController.cs`: emit `Closed` once per open cycle.
- `tests/ui/SettingsMenuControllerTest.cs`: staged-cancel and duplicate-terminal coverage.
- `tests/ui/PauseMenuDialogTest.cs`: complete button visibility and mixed-close evidence.
- `scripts/ui/MainMenu.cs`: route application quit through a protected testable operation without changing behavior.
- `scripts/ui/SaveLoadDialog.cs`: consume `Canceled` and emit exactly one terminal result per open cycle.
- `scripts/ui/DialogueDialog.cs`: unify dialogue outcome/close through one terminal guard.
- `scripts/ui/HealDialog.cs`: unify completion/cancel through one terminal guard.
- `scripts/ui/PuzzleRiddleDialog.cs`: unify choice/close through one terminal guard.
- `tests/ui/ShopDialogTest.cs`: preserve its existing close guard and feedback-timer cleanup.
- `tests/ui/BattleManagerTest.cs`: pre-start escape, active escape, cleanup, and result idempotency.
- `scripts/game/Game.cs`: route visible battle results before Pause, own the defeat delay, and refresh the interaction prompt at blocking-flow boundaries.
- `tests/game/GameTest.cs`: verify the existing fixture-adjacent save/Pause/settings, treasure, puzzle, and world-cleanup coverage without moving it.

---

### Task 1: Capture the Baseline and Seed the Lifecycle Contract

**Files:**
- Create: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Reference: `docs/superpowers/specs/2026-07-26-sirius-ui-lifecycle-baseline-design.md`

**Interfaces:**
- Consumes: current source at the implementation-start commit and the v1.3 row IDs.
- Produces: the stable row IDs and evidence names every later task updates.

- [ ] **Step 1: Record the implementation-start commit and full-suite baseline**

Run:

```bash
git rev-parse HEAD
zsh -o pipefail -c 'dotnet test Sirius.sln --settings test.runsettings.local 2>&1 | tee /tmp/hpa-376-test-baseline.log'
awk 'tolower($0) ~ /orphan/ { count++ } END { print count + 0 }' /tmp/hpa-376-test-baseline.log
rg -i "orphan" /tmp/hpa-376-test-baseline.log || true
```

Expected: `dotnet test` exits 0 with zero failed/skipped tests. Record the printed commit, pass total, orphan-line count, and distinct orphan messages in the contract. Do not commit the `/tmp` log.

- [ ] **Step 2: Create the contract document with the exact schema**

Start `docs/ui/hpa-376/ui-lifecycle-contract.md` with:

```markdown
# HPA-376 Sirius UI Lifecycle Contract

**Implementation-start commit:** Write the exact `git rev-parse HEAD` output captured in Step 1.
**Runtime:** Godot 4.6.2, .NET 8.0, GdUnit4
**Baseline result:** Write the exact passed, failed, and skipped totals captured in Step 1.
**Baseline orphan evidence:** Write the exact orphan-line count and distinct messages captured in Step 1.

## Contract policy

Each row records:

1. observed `file:member` evidence;
2. required migration behavior;
3. one disposition: `Preserve`, `Fix in HPA-376`, or `Replace in HPA-378/379`;
4. protecting test evidence or downstream owner.

## Flow lifecycle matrix

| ID | Observed evidence | Entry and parent | Pause and input block | HUD and cursor | Focus | Input surfaces and topmost receiver | Nested behavior | Terminal signal/count | Cleanup and restoration | Required migration contract | Disposition | Evidence/owner |
|---|---|---|---|---|---|---|---|---|---|---|---|---|

## Modal-priority matrix

| Priority | Active layer | `pause_menu` | `ui_cancel` | Window close | Explicit action | Result/restoration |
|---:|---|---|---|---|---|---|
| 1 | Child popup or key capture | Child/capture policy | Child/capture policy | Child only | Child only | Parent remains inert |
| 2 | Blocking error or confirmation | Error/confirmation policy | Error/confirmation policy | Topmost only | Safe action only | Invoking parent |
| 3 | Deferred Pause restoration | Consume as no-op | No additional owner | Not applicable | Not applicable | Restoring Pause |
| 4 | Owning modal or world interaction | Owning-flow policy | Owning dialog policy | Owning dialog policy | Owning dialog policy | Its documented parent |
| 5 | Parent screen | Parent policy | Parent policy | Parent policy | Parent policy | Its parent |
| 6 | Gameplay fallback | Open Pause | No duplicate action | Not applicable | Not applicable | Pause |

## Verification evidence

## HPA-378/379 handoff
```

Replace those three evidence instructions with the captured values before the file is first committed.

- [ ] **Step 3: Seed all 50 rows in the normative order**

Use these exact IDs once each:

```text
MAIN-ROOT
MAIN-LOAD
MAIN-SETTINGS
MAIN-MESSAGE
MAIN-QUIT
EXP-GAMEPLAY
EXP-PROMPT
EXP-FLOOR-TRANSITION
INV-GAMEPLAY
INV-BLOCKED
INV-PAUSE
PAUSE-ROOT
PAUSE-SETTINGS
PAUSE-SAVELOAD
PAUSE-QUIT-TO-MAIN
PAUSE-RESTORE-PENDING
SET-MAIN
SET-PAUSE
SET-DROPDOWN
SET-CAPTURE
SET-CAPTURE-PAUSE
SAVE-MAIN
SAVE-PAUSE
SAVE-OVERWRITE
SAVE-CORRUPT
SAVE-ERROR
SAVE-QUIT-TO-MAIN
BATTLE-PREP
BATTLE-AUTO
BATTLE-ESC-PREP
BATTLE-ESC-ACTIVE
BATTLE-RESULT-VICTORY
BATTLE-RESULT-DEFEAT
BATTLE-RESULT-ESCAPE
BATTLE-CLEANUP
NPC-DIALOGUE
NPC-TO-SHOP
NPC-SHOP
NPC-TO-HEAL
NPC-HEAL
NPC-CLEANUP
WORLD-TREASURE
WORLD-SWITCH
WORLD-RIDDLE
WORLD-CLEANUP
REWARD-TOAST
REWARD-BLOCKING
CONFIRM-ORDINARY
CONFIRM-DESTRUCTIVE
ERROR-TOPMOST
```

Use this source/evidence routing rather than guessing Godot defaults:

| Rows | Primary observed source | Protecting test owner |
|---|---|---|
| `MAIN-*` | `MainMenu._on_load_button_pressed`, `OnLoadDialogClosed`, `_on_settings_button_pressed`, `OnSettingsClosed`, `ShowMessage`, `_on_quit_button_pressed` | `MainMenuTest` |
| `EXP-*` | `Game.HandlePauseMenuInput`, `UpdateInteractionPrompt`, `OnFloorLoaded`, `OnFloorChanged` | `GameInputLifecycleTest`, existing treasure/puzzle `GameTest` methods |
| `INV-*` | `Game._Input`, `InventoryMenuController._Input`, `OpenMenu`, `CloseMenu` | `InventoryMenuControllerTest`, existing blocked-entry `GameTest` methods |
| `PAUSE-*` | `PauseMenuDialog`, `Game.ShowPauseMenu`, `CleanupPauseMenu`, `OnPause*`, `RestorePauseMenuAfterSettings` | `PauseMenuDialogTest`, `GameTest`, `GameInputLifecycleTest` |
| `SET-*` | `SettingsMenuController.OpenSettings`, `_Input`, `OnApplyPressed`, `OnCancelPressed`, `CancelKeyCapture`, `IsPopupOpen` | `SettingsMenuControllerTest` |
| `SAVE-*` | `SaveLoadDialog.ShowDialog`, `ShowOverwriteConfirmation`, terminal handlers; `Game.CleanupSaveDialogAndRestorePause`, `OnMainMenuRequested`, `ShowSaveError` | `SaveLoadDialogTest`, `GameTest`, `GameInputLifecycleTest` |
| `BATTLE-*` | `BattleManager.StartBattle`, `OnCloseRequested`, `EndBattle`, `EndBattleWithEscape`; `Game.HandlePauseMenuInput`, `OnBattleFinished` | `BattleManagerTest`, `GameInputLifecycleTest` |
| `NPC-*` | `DialogueDialog`, `ShopDialog`, `HealDialog`, `NpcInteractionController` | the four corresponding UI test suites and `GameInputLifecycleTest` |
| `WORLD-*` | `Game.OnTreasureBoxOpenRequested`, `HandlePuzzleSwitch`, `ShowPuzzleRiddle`, `CleanupPuzzleRiddleDialog` | existing world `GameTest` methods and `PuzzleRiddleDialogTest` |
| `REWARD-*` | current `Game` treasure grant and `BattleManager` result/loot display; HPA-373 sections 7.3 and 9.12 for the target | HPA-378/379 and HPA-393 |
| `CONFIRM-*`, `ERROR-TOPMOST` | `SaveLoadDialog.ShowOverwriteConfirmation`, `Game.ShowSaveError`, `HandlePauseMenuInput` | `SaveLoadDialogTest`, `GameInputLifecycleTest`, HPA-378/379 for destructive confirmation |

- [ ] **Step 4: Assign the initial dispositions**

Use these exact HPA-376-owned fixes:

```text
INV-GAMEPLAY: prior-pause snapshot and restoration
SET-MAIN / SET-PAUSE: exactly-once Closed emission
SAVE-MAIN / SAVE-PAUSE: ui_cancel handling and exactly-once terminal emission
BATTLE-RESULT-VICTORY: visible result routing
BATTLE-RESULT-DEFEAT: visible result routing and owned delayed navigation
NPC-DIALOGUE / NPC-HEAL / WORLD-RIDDLE: exactly-once mutually exclusive terminal emission
EXP-PROMPT: refresh at battle/NPC lifecycle boundaries
```

Mark `INV-PAUSE`, `PAUSE-ROOT`, `PAUSE-QUIT-TO-MAIN`, `SAVE-QUIT-TO-MAIN`, `REWARD-TOAST`, `REWARD-BLOCKING`, and `CONFIRM-DESTRUCTIVE` as `Replace in HPA-378/379`. Preserve current domain cleanup in their required-contract text. Mark the HPA-376-owned rows listed above as `Fix in HPA-376`; mark every other row `Preserve`. If characterization contradicts one of those assignments, stop and revise the reviewed plan instead of widening production scope ad hoc.

- [ ] **Step 5: Validate the contract seed**

Run:

```bash
awk 'BEGIN { in_matrix=0 } /^## Flow lifecycle matrix/ { in_matrix=1; next } /^## Modal-priority matrix/ { in_matrix=0 } in_matrix && /^\| `[A-Z]/ { id=$2; gsub(/[ `]/, "", id); count++; if (seen[id]++) duplicate=1 } END { print "row_count=" count ", duplicates=" (duplicate ? "yes" : "no"); exit (duplicate || count != 50) }' docs/ui/hpa-376/ui-lifecycle-contract.md
git diff --check
```

Expected: `row_count=50, duplicates=no`; `git diff --check` exits 0.

- [ ] **Step 6: Commit the baseline contract**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: add HPA-376 lifecycle contract baseline"
```

---

### Task 2: Make Inventory Pause Restoration Idempotent

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: `SceneTree.Paused`, `OpenMenu()`, `CloseMenu()`, and the existing `ui_cancel`/`toggle_inventory` input paths.
- Produces: one captured incoming pause value per open cycle and cleanup-safe restoration.

- [ ] **Step 1: Write the failing lifecycle tests**

Add these tests to `InventoryMenuControllerTest`:

```csharp
[TestCase]
public void OpenAndClose_FromRunningTree_RestoresRunningTree()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = false;

    _inventoryMenu.OpenMenu();
    AssertThat(_inventoryMenu.Visible).IsTrue();
    AssertThat(tree.Paused).IsTrue();

    _inventoryMenu.CloseMenu();
    AssertThat(_inventoryMenu.Visible).IsFalse();
    AssertThat(tree.Paused).IsFalse();
}

[TestCase]
public void OpenAndClose_FromPausedParent_RestoresPausedParent()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = true;
    try
    {
        _inventoryMenu.OpenMenu();
        _inventoryMenu.OpenMenu();
        _inventoryMenu.CloseMenu();

        AssertThat(tree.Paused).IsTrue();
    }
    finally
    {
        tree.Paused = false;
    }
}

[TestCase]
public void CloseMenu_CalledTwice_DoesNotOverwriteRestoredPauseState()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = true;
    try
    {
        _inventoryMenu.OpenMenu();
        _inventoryMenu.CloseMenu();
        _inventoryMenu.CloseMenu();

        AssertThat(tree.Paused).IsTrue();
    }
    finally
    {
        tree.Paused = false;
    }
}

[TestCase]
public void ExitTree_WhileOpen_RestoresIncomingPauseState()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = false;
    _inventoryMenu.OpenMenu();

    _inventoryMenu.Free();

    AssertThat(tree.Paused).IsFalse();
    _inventoryMenu = null!;
}

[TestCase]
public void UiCancelWhileVisible_ClosesAndRestoresIncomingPauseState()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = false;
    _inventoryMenu.OpenMenu();

    _inventoryMenu._Input(new InputEventAction
    {
        Action = "ui_cancel",
        Pressed = true
    });

    AssertThat(_inventoryMenu.Visible).IsFalse();
    AssertThat(tree.Paused).IsFalse();
}

[TestCase]
public void ToggleInventoryWhileVisible_ClosesAndRestoresIncomingPauseState()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    tree.Paused = false;
    _inventoryMenu.OpenMenu();

    _inventoryMenu._Input(new InputEventAction
    {
        Action = "toggle_inventory",
        Pressed = true
    });

    AssertThat(_inventoryMenu.Visible).IsFalse();
    AssertThat(tree.Paused).IsFalse();
}
```

Also add `InputWhileHidden_DoesNotChangePauseState`, and make `Cleanup()` unconditionally restore `((SceneTree)Engine.GetMainLoop()).Paused = false` after closing/freeing the fixture so the synthetic paused-parent test cannot leak state.

- [ ] **Step 2: Run the Inventory suite and observe the red test**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: `OpenAndClose_FromPausedParent_RestoresPausedParent` fails because `CloseMenu()` currently writes `false`.

- [ ] **Step 3: Implement one pause snapshot per open cycle**

Add:

```csharp
private bool _pauseSnapshotCaptured;
private bool _treeWasPausedBeforeOpen;

public void OpenMenu()
{
    if (!_pauseSnapshotCaptured)
    {
        _treeWasPausedBeforeOpen = GetTree().Paused;
        _pauseSnapshotCaptured = true;
    }

    RefreshUI();
    Show();
    GetTree().Paused = true;
}

public void CloseMenu()
{
    Hide();
    RestoreTreePause();
}

private void RestoreTreePause()
{
    if (!_pauseSnapshotCaptured)
        return;

    GetTree().Paused = _treeWasPausedBeforeOpen;
    _pauseSnapshotCaptured = false;
}

public override void _ExitTree()
{
    RestoreTreePause();
}
```

Replace the existing `OpenMenu()` and `CloseMenu()` bodies; do not change process modes or root Pause behavior.

- [ ] **Step 4: Run focused and blocked-entry tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest.InventoryToggle"
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```bash
git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "fix: restore inventory parent pause state"
```

---

### Task 3: Lock Settings and Pause Terminal Semantics

**Files:**
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `tests/ui/SettingsMenuControllerTest.cs`
- Modify: `tests/ui/PauseMenuDialogTest.cs`

**Interfaces:**
- Consumes: `SettingsMenuController.Closed`, `OpenSettings(...)`, `PauseMenuDialog` button signals.
- Produces: one `Closed` signal per Settings open cycle and complete Pause button characterization.

- [ ] **Step 1: Add failing Settings idempotency tests**

Add:

```csharp
[TestCase]
public void CancelAfterStagedEdit_DoesNotMutateSnapshot_AndEmitsClosedOnce()
{
    var snapshot = SettingsData.CreateDefaults();
    snapshot.MasterVolumePercent = 70;
    int closedCount = 0;
    _ctrl.Closed += () => closedCount++;
    _ctrl.OpenSettings(snapshot);

    GetField<HSlider>(_ctrl, "_masterSlider").Value = 15;
    InvokePrivate(_ctrl, "OnCancelPressed");
    InvokePrivate(_ctrl, "OnCancelPressed");

    AssertThat(snapshot.MasterVolumePercent).IsEqual(70);
    AssertThat(closedCount).IsEqual(1);
}

[TestCase]
public void ReopenAfterCancel_AllowsOneNewClosedEmission()
{
    int closedCount = 0;
    _ctrl.Closed += () => closedCount++;

    _ctrl.OpenSettings(SettingsData.CreateDefaults());
    InvokePrivate(_ctrl, "OnCancelPressed");
    _ctrl.OpenSettings(SettingsData.CreateDefaults());
    InvokePrivate(_ctrl, "OnCancelPressed");

    AssertThat(closedCount).IsEqual(2);
}
```

- [ ] **Step 2: Run the Settings suite and observe duplicate close**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: the first new test fails with `closedCount == 2`.

- [ ] **Step 3: Add an open-cycle close guard**

Add a `_closedEmitted` field, reset it in `OpenSettings`, and route successful Apply and Cancel through:

```csharp
private void EmitClosedOnce()
{
    if (_closedEmitted)
        return;

    _closedEmitted = true;
    SetProcessInput(false);
    EmitSignal(SignalName.Closed);
}
```

`OnCancelPressed()` must cancel key capture and call `EmitClosedOnce()`. The successful `OnApplyPressed()` path must call `EmitClosedOnce()` after `ApplyAndSave` succeeds. Error paths remain open and must not set `_closedEmitted`.

- [ ] **Step 4: Complete Pause button characterization**

Add tests proving Save, Load, and Quit hide before emitting, Settings remains visible, and mixed `Canceled` plus `CloseRequested` emits `ResumeRequested` once:

```csharp
[TestCase]
public async Task QuitToMenuButton_Pressed_HidesBeforeSignal()
{
    bool visibleWhenEmitted = true;
    _dialog.QuitToMenuRequested += () => visibleWhenEmitted = _dialog.Visible;
    await OpenDialog();

    FindButton("Quit to Main Menu").EmitSignal(Button.SignalName.Pressed);

    AssertThat(visibleWhenEmitted).IsFalse();
}

[TestCase]
public async Task CanceledThenCloseRequested_EmitsResumeRequestedOnce()
{
    int count = 0;
    _dialog.ResumeRequested += () => count++;
    await OpenDialog();

    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
    _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

    AssertThat(count).IsEqual(1);
}
```

Add equivalent hide-before-signal assertions for Save and Load. No `PauseMenuDialog` production change is expected.

- [ ] **Step 5: Run both suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SettingsMenuControllerTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseMenuDialogTest"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```bash
git add scripts/ui/SettingsMenuController.cs tests/ui/SettingsMenuControllerTest.cs tests/ui/PauseMenuDialogTest.cs
git commit -m "fix: make settings closure idempotent"
```

---

### Task 4: Make Main Menu and Save/Load Terminal Paths Deterministic

**Files:**
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `scripts/ui/SaveLoadDialog.cs`
- Create: `tests/ui/MainMenuTest.cs`
- Create: `tests/ui/SaveLoadDialogTest.cs`

**Interfaces:**
- Consumes: `SaveLoadDialog` terminal signals and Main Menu child cleanup.
- Produces: one terminal outcome per Save/Load open cycle and a safe application-quit test seam.

- [ ] **Step 1: Write Save/Load cancellation and idempotency tests**

The new `SaveLoadDialogTest` must instantiate a fresh dialog under the SceneTree root for each test and free it after two process frames. Include:

```csharp
[TestCase]
public void Canceled_HidesAndEmitsDialogClosed()
{
    int closed = 0;
    _dialog.DialogClosed += () => closed++;
    _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);

    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

    AssertThat(_dialog.Visible).IsFalse();
    AssertThat(closed).IsEqual(1);
}

[TestCase]
public void CanceledThenCloseRequested_EmitsOneTerminalSignal()
{
    int closed = 0;
    _dialog.DialogClosed += () => closed++;
    _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);

    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
    _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

    AssertThat(closed).IsEqual(1);
}

[TestCase]
public void MainMenuThenClose_EmitsMainMenuOnlyOnce()
{
    int menu = 0;
    int closed = 0;
    _dialog.MainMenuRequested += () => menu++;
    _dialog.DialogClosed += () => closed++;
    _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);

    FindButton("Main Menu").EmitSignal(Button.SignalName.Pressed);
    _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

    AssertThat(menu).IsEqual(1);
    AssertThat(closed).IsEqual(0);
}

[TestCase]
public void DismissOverwriteChild_LeavesParentVisibleAndEmitsNothing()
{
    int terminalCount = 0;
    _dialog.DialogClosed += () => terminalCount++;
    _dialog.SaveSlotSelected += _ => terminalCount++;
    _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
    SetSlotInfo(0, new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 });

    InvokePrivate(_dialog, "OnSlotPressed", 0);
    AssertThat(_dialog.HasActiveChildDialog).IsTrue();

    _dialog.DismissActiveChildDialog();

    AssertThat(_dialog.HasActiveChildDialog).IsFalse();
    AssertThat(_dialog.Visible).IsTrue();
    AssertThat(terminalCount).IsEqual(0);
}
```

Provide recursive `FindButton`, reflection `InvokePrivate`, and `SetSlotInfo` helpers in the test file. Also test that `ShowDialog(Load)` hides the Main Menu button and a second `ShowDialog` resets the terminal guard.

Add these success-path cases so slot selection and overwrite confirmation have named evidence:

- `EmptySaveSlotPressed_HidesAndEmitsSaveSlotOnce`
- `LoadSlotPressed_HidesAndEmitsLoadSlotOnce`
- `OverwriteConfirmed_HidesAndEmitsPendingSaveSlotOnce`
- `OverwriteConfirmedThenClose_EmitsOnlySaveSlot`

Use `OnSlotPressed` only to enter the real production branch; confirm the overwrite by emitting `AcceptDialog.SignalName.Confirmed` on the child stored in `_activeConfirmDialog`.

- [ ] **Step 2: Run the new Save/Load suite and observe the red tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveLoadDialogTest"
```

Expected: `Canceled_HidesAndEmitsDialogClosed` and mixed-terminal tests fail.

- [ ] **Step 3: Implement a single terminal gate in SaveLoadDialog**

Add:

```csharp
private bool _terminalEmitted;

private bool TryBeginTerminal()
{
    if (_terminalEmitted)
        return false;

    _terminalEmitted = true;
    return true;
}

private void EmitDialogClosedOnce()
{
    if (!TryBeginTerminal())
        return;

    Hide();
    EmitSignal(SignalName.DialogClosed);
}
```

Then:

- reset `_terminalEmitted = false` in `ShowDialog`;
- connect `Canceled += OnCloseRequested` in `_Ready` and disconnect it in `_ExitTree`;
- route explicit Cancel and `CloseRequested` through `EmitDialogClosedOnce`;
- guard `SaveSlotSelected`, `LoadSlotSelected`, and `MainMenuRequested` with `TryBeginTerminal()` before hiding/emitting;
- keep overwrite-child cancellation outside the parent terminal gate.

- [ ] **Step 4: Add safe Main Menu lifecycle tests**

Create a test subclass that avoids quitting the GdUnit process:

```csharp
private partial class TestableMainMenu : MainMenu
{
    public int QuitRequests { get; private set; }
    protected override void RequestApplicationQuit() => QuitRequests++;
}

[TestCase]
public void QuitButton_RequestsApplicationQuitOnce()
{
    var menu = new TestableMainMenu();

    InvokePrivateAcrossHierarchy(menu, "_on_quit_button_pressed");

    AssertThat(menu.QuitRequests).IsEqual(1);
    menu.Free();
}
```

Also instantiate `res://scenes/ui/MainMenu.tscn` for tests that:

- invoke `ShowMessage("Save system unavailable.")` and assert one visible `AcceptDialog` child above the root;
- set a `SaveLoadDialog` into `_loadDialog`, invoke `OnLoadDialogClosed`, and assert the child is queued and the field is null;
- invoke `_on_settings_button_pressed`, assert one visible `_settingsMenu` child and that a repeat press does not stack another child, then emit `Closed` and assert only that child is cleaned up;
- keep the Main Menu root visible throughout child cleanup.

- [ ] **Step 5: Route the production quit through the seam**

Change only the final operation:

```csharp
private void _on_quit_button_pressed()
{
    GD.Print("Quit button pressed");
    RequestApplicationQuit();
}

protected virtual void RequestApplicationQuit()
{
    GetTree().Quit();
}
```

Do not add a confirmation to Main Menu root quit; no gameplay progress is active there.

- [ ] **Step 6: Run local and existing Game save/Pause tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveLoadDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest.PauseMenu_WhenSave|FullyQualifiedName~GameTest.PauseMenu_WhenMainMenuRequested"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/MainMenu.cs scripts/ui/SaveLoadDialog.cs tests/ui/MainMenuTest.cs tests/ui/SaveLoadDialogTest.cs
git commit -m "fix: make menu terminal paths deterministic"
```

---

### Task 5: Make Dialogue, Heal, and Riddle Outcomes Exactly Once

**Files:**
- Modify: `scripts/ui/DialogueDialog.cs`
- Modify: `scripts/ui/HealDialog.cs`
- Modify: `scripts/ui/PuzzleRiddleDialog.cs`
- Create: `tests/ui/DialogueDialogTest.cs`
- Create: `tests/ui/HealDialogTest.cs`
- Create: `tests/ui/PuzzleRiddleDialogTest.cs`

**Interfaces:**
- Consumes: each dialog's existing public signals.
- Produces: one mutually exclusive terminal result per open/start cycle.

- [ ] **Step 1: Write the three red close-race suites**

`DialogueDialogTest` must include:

```csharp
[TestCase]
public void CanceledThenCloseRequested_EmitsDialogueClosedOnce()
{
    int closed = 0;
    _dialog.DialogueClosed += () => closed++;
    _dialog.StartDialogue(
        NpcCatalog.GetById("old_farmer")!,
        DialogueCatalog.GetById("villager_01")!,
        TestHelpers.CreateTestCharacter(),
        new System.Collections.Generic.HashSet<string>());

    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
    _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

    AssertThat(closed).IsEqual(1);
}

[TestCase]
public void OutcomeThenClose_EmitsOutcomeOnly()
{
    int outcomes = 0;
    int closed = 0;
    _dialog.DialogueOutcome += _ => outcomes++;
    _dialog.DialogueClosed += () => closed++;
    _dialog.StartDialogue(
        NpcCatalog.GetById("village_shopkeeper")!,
        DialogueCatalog.GetById("shopkeeper_greeting")!,
        TestHelpers.CreateTestCharacter(),
        new System.Collections.Generic.HashSet<string>());

    FindButton("Browse your wares.").EmitSignal(Button.SignalName.Pressed);
    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

    AssertThat(outcomes).IsEqual(1);
    AssertThat(closed).IsEqual(0);
}
```

`HealDialogTest` must prove Cancel plus `CloseRequested` emits `HealCancelled` once and successful Heal followed by Cancel emits only `HealComplete`.

`PuzzleRiddleDialogTest` must create a riddle with `ChoiceIds = ["a"]`, `ChoiceLabels = ["Answer"]`, prove Cancel plus `CloseRequested` emits `PuzzleRiddleClosed` once, and prove choice followed by Cancel emits only `ChoiceSelected("a")`.

- [ ] **Step 2: Run all three suites and observe duplicate terminals**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~PuzzleRiddleDialogTest"
```

Expected: duplicate/mixed terminal assertions fail.

- [ ] **Step 3: Add a terminal gate to DialogueDialog**

Add `_terminalEmitted`, reset it in `StartDialogue`, and use:

```csharp
private bool TryBeginTerminal()
{
    if (_terminalEmitted)
        return false;
    _terminalEmitted = true;
    return true;
}

private void EmitClosedOnce()
{
    if (TryBeginTerminal())
        EmitSignal(SignalName.DialogueClosed);
}

private void EmitOutcomeOnce(DialogueOutcomeType outcome)
{
    if (TryBeginTerminal())
        EmitSignal(SignalName.DialogueOutcome, (int)outcome);
}
```

Route the missing-root path, leaf close button, terminal choice, missing-next-node path, and `OnCloseRequested` through these helpers. Non-terminal choices still call `ShowNode`.

- [ ] **Step 4: Add mutually exclusive gates to HealDialog and PuzzleRiddleDialog**

For `HealDialog`, reset `_terminalEmitted` in `OpenHeal`, emit completion only after a successful purchase, and emit cancellation through:

```csharp
private void EmitCancelledOnce()
{
    if (_terminalEmitted)
        return;
    _terminalEmitted = true;
    EmitSignal(SignalName.HealCancelled);
}
```

The successful Heal path must set `_terminalEmitted` before emitting `HealComplete`.

For `PuzzleRiddleDialog`, reset `_terminalEmitted` in `OpenRiddle`, replace the choice lambda with `() => EmitChoiceOnce(capturedId)`, and guard `PuzzleRiddleClosed` with the same terminal boolean.

- [ ] **Step 5: Run the suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~PuzzleRiddleDialogTest"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```bash
git add scripts/ui/DialogueDialog.cs scripts/ui/HealDialog.cs scripts/ui/PuzzleRiddleDialog.cs tests/ui/DialogueDialogTest.cs tests/ui/HealDialogTest.cs tests/ui/PuzzleRiddleDialogTest.cs
git commit -m "fix: emit modal terminal outcomes once"
```

---

### Task 6: Characterize NPC Transitions and Fix Shop Cleanup

**Files:**
- Create: `tests/ui/NpcInteractionControllerTest.cs`
- Modify: `tests/ui/ShopDialogTest.cs`
- Modify: `scripts/ui/ShopDialog.cs`
- Reference: `scripts/ui/NpcInteractionController.cs`

**Interfaces:**
- Consumes: terminal guarantees from Task 5 and `NpcInteractionController.InteractionComplete`.
- Produces: executable dialogue-to-shop/heal restoration evidence and immediate Shop feedback-timer cleanup on close.

- [ ] **Step 1: Add NPC orchestration tests**

Create a SceneTree-root `Node` as `_uiParent` per test and use catalog NPCs. Include:

```csharp
[TestCase]
public async Task DialogueCancel_CompletesOnceAndFreesDialogue()
{
    int completed = 0;
    var controller = CreateController("old_farmer");
    controller.InteractionComplete += () => completed++;
    controller.Begin();

    var dialogue = _uiParent.GetChildren().OfType<DialogueDialog>().Single();
    dialogue.EmitSignal(AcceptDialog.SignalName.Canceled);
    controller.Finish();
    await AwaitTwoFrames();

    AssertThat(completed).IsEqual(1);
    AssertThat(_uiParent.GetChildren().OfType<DialogueDialog>().Any()).IsFalse();
}

[TestCase]
public async Task ShopOutcome_ReplacesDialogue_AndShopCancelCompletesOnce()
{
    int completed = 0;
    var controller = CreateController("village_shopkeeper");
    controller.InteractionComplete += () => completed++;
    controller.Begin();

    FindButton(_uiParent, "Browse your wares.").EmitSignal(Button.SignalName.Pressed);
    var shop = _uiParent.GetChildren().OfType<ShopDialog>().Single();
    shop.EmitSignal(AcceptDialog.SignalName.Canceled);
    await AwaitTwoFrames();

    AssertThat(completed).IsEqual(1);
    AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
}

[TestCase]
public async Task HealOutcome_ReplacesDialogue_AndCancelCompletesOnce()
{
    int completed = 0;
    var controller = CreateController("village_healer");
    controller.InteractionComplete += () => completed++;
    controller.Begin();

    FindButton(_uiParent, "Yes, heal me. (50 gold)").EmitSignal(Button.SignalName.Pressed);
    var heal = _uiParent.GetChildren().OfType<HealDialog>().Single();
    heal.EmitSignal(AcceptDialog.SignalName.Canceled);
    await AwaitTwoFrames();

    AssertThat(completed).IsEqual(1);
    AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
}

[TestCase]
public void MissingDialogueTree_CompletesOnceAndCreatesNoDialog()
{
    var npc = new NpcData
    {
        NpcId = "missing_dialogue_test",
        DisplayName = "Missing Dialogue",
        NpcType = NpcType.Villager,
        DialogueTreeId = "missing_dialogue_tree",
        SpriteType = "villager"
    };
    int completed = 0;
    var controller = CreateController(npc);
    controller.InteractionComplete += () => completed++;

    controller.Begin();
    controller.Finish();

    AssertThat(completed).IsEqual(1);
    AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
}
```

Provide `CreateController(string npcId)` that resolves with `NpcCatalog.GetById`, plus a `CreateController(NpcData npc)` overload shared by the invalid-tree case. Both use `TestHelpers.CreateTestCharacter()` and a fresh quest-flag set.

- [ ] **Step 2: Add Shop close/timer preservation tests**

Extend `ShopDialogTest`:

```csharp
[TestCase]
public void CanceledThenCloseRequested_EmitsShopClosedOnce()
{
    int count = 0;
    _dialog.ShopClosed += () => count++;

    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
    _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

    AssertThat(count).IsEqual(1);
}

[TestCase]
public void Close_CancelsPendingFeedbackTimer()
{
    InvokePrivateMethod(_dialog, "ShowFeedback", "Pending");
    _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

    AssertThat(GetNullablePrivateField<SceneTreeTimer>(_dialog, "_feedbackTimer")).IsNull();
}
```

Add this separate nullable helper so the existing non-null helper keeps detecting missing fixture state:

```csharp
private static T? GetNullablePrivateField<T>(object instance, string fieldName)
    where T : class
{
    var field = instance.GetType().GetField(
        fieldName,
        BindingFlags.NonPublic | BindingFlags.Instance);
    if (field == null)
        throw new InvalidOperationException($"Failed to locate private field '{fieldName}'.");

    return field.GetValue(instance) as T;
}
```

- [ ] **Step 3: Run the NPC and Shop suites and observe the timer-cleanup failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest"
```

Expected: the NPC transition and Shop close-idempotency cases pass, while `Close_CancelsPendingFeedbackTimer` fails because `OnCloseRequested()` leaves `_feedbackTimer` attached until `_ExitTree()`.

- [ ] **Step 4: Cancel pending feedback immediately on Shop close**

In `ShopDialog.OnCloseRequested()`, call `CancelFeedbackTimer()` after acquiring the existing `_closed` gate and before hiding/emitting `ShopClosed`:

```csharp
private void OnCloseRequested()
{
    if (_closed)
        return;

    _closed = true;
    CancelFeedbackTimer();
    Hide();
    EmitSignal(SignalName.ShopClosed);
}
```

Do not change Shop ownership, public signals, or `NpcInteractionController`.

- [ ] **Step 5: Run the focused and full suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: all selected tests and the full suite pass.

- [ ] **Step 6: Commit**

```bash
git add scripts/ui/ShopDialog.cs tests/ui/NpcInteractionControllerTest.cs tests/ui/ShopDialogTest.cs
git commit -m "fix: clean up shop feedback on close"
```

---

### Task 7: Characterize BattleManager Escape and Result Cleanup

**Files:**
- Modify: `tests/ui/BattleManagerTest.cs`
- Reference: `scripts/ui/BattleManager.cs`

**Interfaces:**
- Consumes: `BattleManager.BattleFinished`, `ForceCloseAsEscape()`, the private battle timer, and result guard.
- Produces: local battle evidence used by the Game routing task.

- [ ] **Step 1: Add a ready BattleManager fixture helper**

Add an async helper that instantiates the real scene:

```csharp
private async Task<BattleManager> CreateReadyBattleManager()
{
    var scene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn")
        ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
    var manager = scene.Instantiate<BattleManager>();
    ((SceneTree)Engine.GetMainLoop()).Root.AddChild(manager);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    manager.PopupCentered();
    manager.StartBattle(TestHelpers.CreateTestCharacter(), Enemy.CreateGoblin());
    return manager;
}

private async Task FreeManager(BattleManager manager)
{
    if (GodotObject.IsInstanceValid(manager) && !manager.IsQueuedForDeletion())
        manager.QueueFree();

    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
}
```

Each test uses `FreeManager` in `finally`, including paths where production already queued the dialog.

- [ ] **Step 2: Add pre-start, active, and result-phase tests**

```csharp
[TestCase]
public async Task ForceCloseDuringPreparation_EmitsEscapeOnce()
{
    var manager = await CreateReadyBattleManager();
    int count = 0;
    bool escaped = false;
    manager.BattleFinished += (_, wasEscaped) => { count++; escaped = wasEscaped; };
    try
    {
        manager.ForceCloseAsEscape();
        manager.ForceCloseAsEscape();

        AssertThat(count).IsEqual(1);
        AssertThat(escaped).IsTrue();
    }
    finally
    {
        await FreeManager(manager);
    }
}

[TestCase]
public async Task ForceCloseDuringAutomaticCombat_StopsTimerClearsEffectsAndEmitsOnce()
{
    var manager = await CreateReadyBattleManager();
    var player = GetPrivateField<Character>(manager, "_player");
    var enemy = GetPrivateField<Enemy>(manager, "_enemy");
    player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Strength, 2, 2));
    enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Poison, 1, 2));
    InvokePrivateMethod(manager, "OnStartButtonPressed");
    int count = 0;
    manager.BattleFinished += (_, _) => count++;
    try
    {
        manager.ForceCloseAsEscape();

        AssertThat(GetPrivateField<Timer>(manager, "_battleTimer").IsStopped()).IsTrue();
        AssertThat(player.ActiveBuffs.HasAny).IsFalse();
        AssertThat(enemy.ActiveStatusEffects.HasAny).IsFalse();
        AssertThat(count).IsEqual(1);
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

- [ ] **Step 3: Run the BattleManager suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~BattleManagerTest"
```

Expected: all lifecycle and existing combat tests pass. The current `_resultEmitted` and timer-stop behavior should satisfy these tests without production changes.

- [ ] **Step 4: Commit**

```bash
git add tests/ui/BattleManagerTest.cs
git commit -m "test: lock battle escape lifecycle"
```

---

### Task 8: Fix Game-Level Priority, Defeat Navigation, and Prompt Restoration

**Files:**
- Modify: `scripts/game/Game.cs`
- Create: `tests/game/GameInputLifecycleTest.cs`
- Verify: `tests/game/GameTest.cs`

**Interfaces:**
- Consumes: Task 7's `BattleManager.ForceCloseAsEscape()` guarantee and current `GameManager` flags.
- Produces: deterministic topmost routing, handled-input evidence, owned delayed defeat navigation, and prompt restoration.

- [ ] **Step 1: Create the cross-controller fixture**

Use a `SubViewport` fixture equivalent to `GameTest`, but keep it local:

```csharp
public partial class LifecycleGame : Game
{
    public Action? MainMenuNavigationRequested { get; set; }
    protected override double DefeatReturnDelaySeconds => 0.01;
    protected override void ReturnToMainMenu() => MainMenuNavigationRequested?.Invoke();
    public override void _Ready() { }
}
```

Set a fresh `GameManager` into `_gameManager` by reflection, add a `CanvasLayer` named `UI`, and free `LifecycleGame` before its `SubViewport` in cleanup.

- [ ] **Step 2: Write the failing battle-result routing test**

```csharp
[TestCase]
public async Task BattleResultCancelIsHandledAndClosesResultWithoutOpeningPause()
{
    var scene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn")
        ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
    var battle = scene.Instantiate<BattleManager>();
    _game!.GetNode<CanvasLayer>("UI").AddChild(battle);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    SetPrivateField(battle, "_resultEmitted", true);
    battle.PopupCentered();
    SetPrivateField(_game, "_battleManager", battle);
    AssertThat(_gameManager!.IsInBattle).IsFalse();

    PushPauseEvent();

    AssertThat(_viewport!.IsInputHandled()).IsTrue();
    AssertThat(battle.Visible).IsFalse();
    AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
}
```

Expected before the fix: Pause opens while the battle result remains visible.

- [ ] **Step 3: Write deferred-restoration and error priority tests**

```csharp
[TestCase]
public void PauseRestorePending_ConsumesWithoutChangingVisibleStack()
{
    SetPrivateField(_game!, "_pauseMenuRestorePending", true);

    PushPauseEvent();

    AssertThat(_viewport!.IsInputHandled()).IsTrue();
    AssertThat(GetPrivateField<PauseMenuDialog?>(_game, "_pauseMenuDialog")).IsNull();
}

[TestCase]
public void ErrorCancel_DismissesOnlyErrorAndLeavesPauseVisible()
{
    var pause = new PauseMenuDialog();
    var error = new AcceptDialog();
    _game!.GetNode<CanvasLayer>("UI").AddChild(pause);
    _game.GetNode<CanvasLayer>("UI").AddChild(error);
    pause.PopupCentered();
    error.PopupCentered();
    SetPrivateField(_game, "_pauseMenuDialog", pause);
    SetPrivateField(_game, "_activeErrorPopup", error);

    PushPauseEvent();

    AssertThat(_viewport!.IsInputHandled()).IsTrue();
    AssertThat(GetPrivateField<AcceptDialog?>(_game, "_activeErrorPopup")).IsNull();
    AssertThat(pause.Visible).IsTrue();
}
```

The restoration test should already pass and remains a characterization gate.

- [ ] **Step 4: Route a visible BattleManager before Pause fallback**

After error/settings/save/deferred-restoration handling and before puzzle/world/NPC/gameplay fallback, add:

```csharp
if (_battleManager != null
    && GodotObject.IsInstanceValid(_battleManager)
    && _battleManager.Visible)
{
    _battleManager.ForceCloseAsEscape();
    GetViewport().SetInputAsHandled();
    return;
}
```

Keep the existing in-battle missing-dialog fallback, but make any remaining `_battleManager` use validate `GodotObject.IsInstanceValid`.

- [ ] **Step 5: Write the red defeat-delay ownership tests**

```csharp
[TestCase]
public async Task DefeatReturnTimerIsOwnedAndDoesNotNavigateAfterCleanup()
{
    int navigations = 0;
    _game!.MainMenuNavigationRequested = () => navigations++;
    InvokePrivate(_game, "ScheduleDefeatReturnToMainMenu");

    _game.Free();
    _game = null;
    await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
        SceneTreeTimer.SignalName.Timeout);

    AssertThat(navigations).IsEqual(0);
}

[TestCase]
public async Task DefeatReturnTimer_NavigatesOnceWhileOwnerLives()
{
    int navigations = 0;
    _game!.MainMenuNavigationRequested = () => navigations++;
    InvokePrivate(_game, "ScheduleDefeatReturnToMainMenu");
    InvokePrivate(_game, "ScheduleDefeatReturnToMainMenu");

    await ToSignal(((SceneTree)Engine.GetMainLoop()).CreateTimer(0.05),
        SceneTreeTimer.SignalName.Timeout);

    AssertThat(navigations).IsEqual(1);
}
```

Expected before implementation: compile/reflection failure because the scheduling seam does not exist.

- [ ] **Step 6: Own and invalidate the defeat timer**

Add:

```csharp
private SceneTreeTimer? _defeatReturnTimer;
private Action? _defeatReturnHandler;
protected virtual double DefeatReturnDelaySeconds => 2.0;

private void ScheduleDefeatReturnToMainMenu()
{
    CancelDefeatReturnToMainMenu();
    _defeatReturnTimer = GetTree().CreateTimer(DefeatReturnDelaySeconds);
    _defeatReturnHandler = OnDefeatReturnTimeout;
    _defeatReturnTimer.Timeout += _defeatReturnHandler;
}

private void OnDefeatReturnTimeout()
{
    CancelDefeatReturnToMainMenu();
    if (IsInsideTree())
        ReturnToMainMenu();
}

private void CancelDefeatReturnToMainMenu()
{
    if (_defeatReturnTimer != null
        && GodotObject.IsInstanceValid(_defeatReturnTimer)
        && _defeatReturnHandler != null)
    {
        _defeatReturnTimer.Timeout -= _defeatReturnHandler;
    }

    _defeatReturnTimer = null;
    _defeatReturnHandler = null;
}

protected virtual void ReturnToMainMenu()
{
    GD.Print("Returning to main menu");
    GetTree().ChangeSceneToFile("res://scenes/ui/MainMenu.tscn");
}
```

Replace the inline `CreateTimer(2.0)` subscription in `OnBattleFinished` with `ScheduleDefeatReturnToMainMenu()`. Call `CancelDefeatReturnToMainMenu()` at the beginning of `_ExitTree()`.

- [ ] **Step 7: Add prompt lifecycle coverage**

Add integration tests that instantiate `res://scenes/game/Game.tscn` and await the existing floor deferred setup:

```csharp
[TestCase]
public async Task FloorReplacement_RebindsGridAndRefreshesPrompt()
{
    var game = await InstantiateGameScene();
    try
    {
        var floorManager = game.GetNode<FloorManager>("FloorManager");
        var originalGrid = floorManager.CurrentGridMap;

        AssertThat(floorManager.LoadFloor(1)).IsTrue();
        await AwaitFrames(8);

        var prompt = game.GetNode<Label>("UI/GameUI/InteractionPrompt");
        AssertThat(floorManager.CurrentGridMap).IsNotEqual(originalGrid);
        AssertThat(GetPrivateField<GridMap>(game, "_gridMap"))
            .IsEqual(floorManager.CurrentGridMap);
        AssertThat(prompt.Visible).IsFalse();
    }
    finally
    {
        await FreeGameScene(game);
    }
}
```

Add `InteractionPrompt_HidesDuringBattleAndRestoresAfterEscape` using the same runtime treasure setup already proven in `GameTest.Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt`: make the prompt visible, call `GameManager.StartBattle`, assert hidden, call the live `BattleManager.ForceCloseAsEscape`, and assert the prompt is recomputed after battle cleanup.

Keep the existing treasure and riddle prompt tests in `GameTest`; do not duplicate their domain assertions.

- [ ] **Step 8: Refresh the prompt at blocking-flow boundaries**

Call `UpdateInteractionPrompt()`:

- in `OnBattleStarted` after `IsInBattle` is set;
- at the end of `OnBattleFinished` after enemy removal/state cleanup;
- immediately after `StartNpcInteraction`;
- after the NPC Begin failure path clears the flag;
- after `OnNpcInteractionComplete` clears the flag.

`OnTreasureBoxOpenRequested`, puzzle cleanup, and `OnFloorLoaded` already refresh the prompt; preserve those calls.

- [ ] **Step 9: Run Game lifecycle and existing fixture-adjacent tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameInputLifecycleTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest"
```

Expected: both suites pass with no late scene transition or new orphan warning.

- [ ] **Step 10: Commit**

```bash
git add scripts/game/Game.cs tests/game/GameInputLifecycleTest.cs
git commit -m "fix: enforce Game UI lifecycle priority"
```

---

### Task 9: Reconcile the Contract and Run Whole-Branch Verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Verify: every source/test file changed by Tasks 2–8

**Interfaces:**
- Consumes: all test method names and verified behavior from Tasks 2–8.
- Produces: the final HPA-376 artifact and evidence ready for HPA-378.

- [ ] **Step 1: Replace planned evidence with exact final evidence**

For every `Preserve` and `Fix in HPA-376` row, record the actual `ClassName.TestMethodName` that passed. For replacement rows, record:

```text
INV-PAUSE -> HPA-378/379: host-owned Pause parent, paused world, visible cursor, Inventory focus/restoration
PAUSE-ROOT -> HPA-378/379: central pause/input/process-mode ownership
PAUSE-QUIT-TO-MAIN -> HPA-378/379: explicit quit-with-risk confirmation, current cleanup preserved
SAVE-QUIT-TO-MAIN -> HPA-378/379: explicit quit-with-risk confirmation, Save/Load and hidden Pause cleanup preserved
REWARD-TOAST -> HPA-378/379 plus downstream reward screen: non-blocking, HUD retained, cursor unchanged, no Cancel owner
REWARD-BLOCKING -> HPA-378/379 plus HPA-393 handoff: parent inert, cursor visible, Continue focus, required acknowledgement ignores Cancel
CONFIRM-DESTRUCTIVE -> HPA-378/379: explicit safe action; generic Cancel never confirms
```

Ensure every observed cell uses a current `file:member` citation and no row says merely “Godot default.”

- [ ] **Step 2: Validate the 50-row and priority matrices**

```bash
awk 'BEGIN { in_matrix=0 } /^## Flow lifecycle matrix/ { in_matrix=1; next } /^## Modal-priority matrix/ { in_matrix=0 } in_matrix && /^\| `[A-Z]/ { id=$2; gsub(/[ `]/, "", id); count++; if (seen[id]++) duplicate=1 } END { print "row_count=" count ", duplicates=" (duplicate ? "yes" : "no"); exit (duplicate || count != 50) }' docs/ui/hpa-376/ui-lifecycle-contract.md
rg -n "Observed behavior|Required migration contract|Disposition|Evidence/owner|pause_menu|ui_cancel|CloseRequested|toggle_inventory" docs/ui/hpa-376/ui-lifecycle-contract.md
```

Expected: 50 unique rows and all required input surfaces represented.

- [ ] **Step 3: Run every changed focused suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SettingsMenuControllerTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~PauseMenuDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveLoadDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~PuzzleRiddleDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~BattleManagerTest"
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest"
```

Expected: every command exits 0 with zero failed/skipped tests.

- [ ] **Step 4: Run and capture the full suite**

```bash
zsh -o pipefail -c 'dotnet test Sirius.sln --settings test.runsettings.local 2>&1 | tee /tmp/hpa-376-test-after.log'
awk 'tolower($0) ~ /orphan/ { count++ } END { print count + 0 }' /tmp/hpa-376-test-after.log
rg -i "orphan" /tmp/hpa-376-test-after.log || true
```

Expected: implementation-start tests plus all new tests pass with zero failures/skips. No new orphan line or distinct orphan message appears relative to `/tmp/hpa-376-test-baseline.log`.

- [ ] **Step 5: Record final verification in the contract**

Add the final commit-under-test, exact pass total, zero failure/skip totals, after orphan count/messages, and a statement that `/tmp` logs are uncommitted evidence inputs. If the upstream test total changed during implementation, explain the delta by named added tests rather than comparing only to 869.

- [ ] **Step 6: Run repository hygiene checks**

```bash
git diff --check
git status --short
```

Expected: no whitespace errors and only the intended contract update remains unstaged.

- [ ] **Step 7: Commit the final contract**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: finalize HPA-376 lifecycle evidence"
```

- [ ] **Step 8: Verify the committed branch**

```bash
git status --short --branch
git log --oneline --decorate -12
```

Expected: clean `codex/hpa-376-ui-lifecycle-baseline` worktree with the task commits in order and no implementation of HPA-378/379 host architecture.
