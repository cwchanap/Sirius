# Task 5 Report: Exactly-Once Dialogue, Heal, and Riddle Outcomes

## Implementation

Implemented the Task 5 terminal-result contract without changing Game-level
restoration or any public dialog signal.

- `DialogueDialog` now resets a per-start terminal gate and routes missing
  roots, leaf close, terminal outcomes, missing next nodes, Cancel, and window
  close through guarded helpers. Non-terminal choices still render the next
  node normally.
- `HealDialog` resets its terminal gate per open. A successful purchase marks
  the terminal state before `HealComplete`; Cancel/window close uses the
  guarded `HealCancelled` path. Full-health and insufficient-gold feedback
  remain non-terminal.
- `PuzzleRiddleDialog` resets its terminal gate per open, guards both the
  choice callback and Cancel/window close, and therefore still permits the
  existing Game retry path to call `OpenRiddle` for a fresh cycle.

## TDD evidence

### RED

Added the three requested runtime controller suites before production changes,
then ran:

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~PuzzleRiddleDialogTest" --logger "console;verbosity=minimal"
```

Result: 0 passed, 6 failed. The cancel-plus-close races each observed `2`
instead of `1`, and the action-plus-cancel races each observed an unwanted
second terminal result (`closed`/`cancelled` was `1` instead of `0`). This was
the expected pre-fix failure.

The direct `rtk dotnet test` form from the brief did not pass GdUnit's class
filter through the RTK wrapper; `rtk proxy` preserves the identical dotnet
arguments. Sandboxed runtime execution also aborted with exit 134, so the
authoritative runtime runs used the approved unsandboxed boundary.

### GREEN

After the minimal terminal gates, the same proxied focused command passed:

- 6 passed, 0 failed, 0 skipped.

The individual `DialogueDialogTest`, `HealDialogTest`, and
`PuzzleRiddleDialogTest` suites also passed 2/2 each. The riddle fixture is
attached to the test scene tree and queued for cleanup, avoiding new orphan
warnings in its isolated suite.

## Verification

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --filter "FullyQualifiedName~GameTest.Game_WrongRiddleAnswerAppliesPenaltyAndAllowsRetry|FullyQualifiedName~GameTest.Game_DormantRiddleShowsMessageAndKeepsDialogOpen|FullyQualifiedName~GameTest.PauseMenu_WhenPuzzleRiddleOpen_DoesNotConsumeInput" --logger "console;verbosity=minimal"
```

Result: 3 passed, 0 failed, 0 skipped.

```bash
rtk git diff --check
```

Result: clean.

Full suite attempt:

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task5-results --logger "trx;LogFileName=task5.trx"
```

The console lost its final aggregate after existing orphan-node notices. The
persisted TRX at `/private/tmp/hpa-376-task5-results/task5.trx` records a
Completed run with 896 total/executed/passed, 0 failed, and 0 skipped.

## Files changed

- `scripts/ui/DialogueDialog.cs`
- `scripts/ui/HealDialog.cs`
- `scripts/ui/PuzzleRiddleDialog.cs`
- `tests/ui/DialogueDialogTest.cs`
- `tests/ui/HealDialogTest.cs`
- `tests/ui/PuzzleRiddleDialogTest.cs`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-5-report.md`

## Self-review

- Verified every Task 5 terminal exit route is guarded.
- Verified resets occur only at `StartDialogue`, `OpenHeal`, and `OpenRiddle`.
- Verified Heal's domain validation remains before terminal emission.
- Verified the existing wrong-answer retry and Cancel input regression tests.
- No Game-level restoration code was changed.

## Concerns

None for the implementation. The suite retains pre-existing orphan-node
warnings; the task's full-suite console wrapper did not print the aggregate,
but the saved TRX confirms all 896 tests passed.
