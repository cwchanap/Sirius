# Task 4 Report: Deterministic Main Menu and Save/Load Terminal Paths

## Implementation

- Added `MainMenu.RequestApplicationQuit()` as the protected quit seam. The root Quit button remains a direct quit route and delegates only its final operation to this seam.
- Added a per-open-cycle `_terminalEmitted` gate to `SaveLoadDialog` and reset it in `ShowDialog`.
- Routed `Canceled`, explicit Cancel, and `CloseRequested` through `EmitDialogClosedOnce()`.
- Guarded empty-save selection, load selection, overwrite-confirmed save selection, and Main Menu selection before hiding/emitting their terminal signals.
- Kept overwrite-child cancellation local to the child: `DismissActiveChildDialog()` clears only child state and leaves the parent visible without a terminal signal.
- Added runtime controller-local suites for the Save/Load terminal contract and Main Menu child cleanup/quit seam.

## TDD evidence

### RED

1. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest"`
   - Failed to compile as expected: `TestableMainMenu.RequestApplicationQuit()` had no method to override (`CS0115`).
2. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveLoadDialogTest"`
   - Failed as expected: `Canceled_HidesAndEmitsDialogClosed` left the dialog visible; `MainMenuThenClose_EmitsMainMenuOnlyOnce` and `OverwriteConfirmedThenClose_EmitsOnlySaveSlot` emitted a second `DialogClosed` terminal signal.
   - Result: 7 passed, 3 failed, 10 total.

### GREEN

1. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~SaveLoadDialogTest"`
   - Passed: 10 tests.
2. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest"`
   - Passed: 4 tests.
3. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest.PauseMenu_WhenSave|FullyQualifiedName~GameTest.PauseMenu_WhenMainMenuRequested"`
   - Result: 6 passed, 1 failed. The failure was `PauseMenu_WhenSaveRequested_HidesPauseInsteadOfDestroying`, before its assertion: `Game.ShowSaveMenu()` attempted `QueueFree()` on a disposed pre-existing `_saveLoadDialog` (Game.cs:1398).
4. `rtk dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameTest.PauseMenu_WhenSaveRequested_HidesPauseInsteadOfDestroying"`
   - Passed: 1 test. This distinguishes the combined-filter failure as existing order/harness lifecycle leakage rather than the Task 4 controller changes.
5. `rtk dotnet test Sirius.sln --settings test.runsettings.local`
   - Started as required, but the subagent execution harness lost the full-suite output/status after the process exited. `rtk ps -Ao pid,etime,command | rtk rg "dotnet test Sirius.sln|vstest|Godot|Sirius.dll"` found no remaining test process. A controller-level persistent TRX capture is required for an authoritative whole-suite result.

## Files changed

- `scripts/ui/MainMenu.cs`
- `scripts/ui/SaveLoadDialog.cs`
- `tests/ui/MainMenuTest.cs`
- `tests/ui/SaveLoadDialogTest.cs`

## Self-review

- `rtk git diff --check` completed cleanly.
- Verified each requested terminal parent route uses the shared gate before hide/emit.
- Confirmed child overwrite cancellation remains outside the gate.
- No tests invoke `SceneTree.ChangeSceneToFile()` or `SceneTree.Quit()`; the quit test overrides the protected seam.
- No gameplay/Game restoration implementation was changed.

## Concerns

- The combined Game pause filter has an order-sensitive disposed-dialog failure in existing `Game.ShowSaveMenu()` cleanup. Its isolated test passes, and Task 4 intentionally does not alter Game-level restoration/cleanup.
- Full-suite status is unavailable because the harness dropped the test output after process completion; capture a persistent TRX from the controller environment before treating the whole suite as authoritative.

## Controller Validation

The controller captured the required persistent TRX at commit `82c0bbe`:

`rtk dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task4-results --logger "trx;LogFileName=task4.trx"`

Result: 890 tests passed; zero failures and zero skips. The run reported 320 pre-existing compiler warnings.
