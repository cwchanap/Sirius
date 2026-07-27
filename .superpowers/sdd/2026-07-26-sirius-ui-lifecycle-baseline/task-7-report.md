# Task 7 Report: Battle Escape and Result Cleanup Characterization

## Status

Completed characterization-only test coverage. No `BattleManager` production
code changed.

## Implementation

- Added a real-scene `CreateReadyBattleManager()` fixture that instantiates
  `BattleScene.tscn`, attaches it to the `SceneTree`, waits for `_Ready()`,
  shows it, and starts a Goblin battle.
- Added `FreeManager()` cleanup that safely tolerates a manager already queued
  for deletion and waits two process frames before the next test.
- Added the requested lifecycle tests:
  - preparation escape emits one escaped result across repeated close requests;
  - automatic-combat escape stops the timer, clears both combatants' transient
    effects, and emits one result;
  - post-result close hides/frees the presentation without emitting a second
    result.

## Verification

### Focused runtime suite

The direct RTK wrapper mangled the GdUnit class filter, so the transparent
proxy boundary was required. The filesystem sandbox could not connect to the
Godot runner, so the command ran outside that sandbox.

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~BattleManagerTest"
```

Result: **15 passed, 0 failed, 0 skipped** (342 ms).

### Full suite

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task7-results-20260727 --logger "trx;LogFileName=task7.trx"
```

The run completed through the repository's pre-existing orphan warnings
(`Detected <7>`, `Detected <10>`, and seven `Detected <1>` messages), but its
runner emitted no final aggregate and left the requested results directory
empty. This prevents a trustworthy exact all-suite count in this subtask.
The controller will rerun the exact committed revision at its direct runtime
boundary with a persistent TRX artifact.

`rtk git diff --check` passed.

## Files Changed

- `tests/ui/BattleManagerTest.cs`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-7-report.md`

## Self-Review

- The three tests use the requested public result signal and real dialog scene;
  reflection is limited to legacy private timer/combatant/result seams required
  by the task brief.
- Each transient scene has `finally` cleanup that remains safe after production
  has queued it for deletion.
- The diff is restricted to the Task 7 test suite, its missing `Task` import,
  and this report.

## Concern

The focused suite is verified green. Full-suite completion and the preserved
orphan-warning pattern were observed, but the requested Task 7 TRX artifact
was not produced by this execution boundary; exact full-suite totals remain
for the controller's direct post-commit rerun.
