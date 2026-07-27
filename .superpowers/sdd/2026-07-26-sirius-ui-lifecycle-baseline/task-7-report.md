# Task 7 Report: Battle Escape and Result Cleanup Characterization

## Status

Completed characterization-only coverage, corrected during final review to
match the public close path. No `BattleManager` production code changed.

## Implementation

- Added a real-scene `CreateReadyBattleManager()` fixture that instantiates
  `BattleScene.tscn`, attaches it to the `SceneTree`, waits for `_Ready()`,
  shows it, and starts a Goblin battle.
- Added `FreeManager()` cleanup that safely tolerates a manager already queued
  for deletion and waits two process frames before the next test.
- The current public-path lifecycle tests prove:
  - preparation escape emits one escaped result across repeated close requests,
    then immediately hides and queues the dialog;
  - automatic-combat escape stops the timer, clears both combatants' transient
    effects, emits one result, then immediately hides and queues the dialog.
- Final review removed the private `EndBattleWithEscape` seam test. The public
  `ForceCloseAsEscape` path calls `OnCloseRequested`, which continues after
  emitting the escape result and immediately hides/queues the presentation.
  Therefore no standalone, player-visible escape Continue surface exists to
  characterize.

## Verification

### Focused runtime suite

The direct RTK wrapper mangled the GdUnit class filter, so the transparent
proxy boundary was required. The filesystem sandbox could not connect to the
Godot runner, so the command ran outside that sandbox.

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~BattleManagerTest"
```

Current final-review result at implementation commit
`30e86d259f88259d6e2779f8280f6b975403b0e1`:
**14 passed, 0 failed, 0 skipped** (305 ms).

### Historical Task 7 full suite

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task7-results-20260727 --logger "trx;LogFileName=task7.trx"
```

The run completed through the repository's pre-existing orphan warnings
(`Detected <7>`, `Detected <10>`, and seven `Detected <1>` messages), but its
runner emitted no final aggregate and left the requested results directory
empty at this subtask's execution boundary.

### Controller validation

The controller reran the exact committed revision (`7bbbecd`) at its direct
unsandboxed runtime boundary with a persistent TRX artifact:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task7-controller-results --logger "trx;LogFileName=task7.trx"
```

Result: **905 passed, 0 failed, 0 skipped**, with 320 pre-existing warnings.
This remains the historical complete-suite verification for the original Task
7 revision.

### Final-review full suite

The authoritative full suite for the corrected executable commit was saved as
`.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts/final-review-fix-1-full.trx`.

Result: **914 passed, 0 failed, 0 skipped**. The TRX reports
`outcome="Completed"`, `total="914"`, `executed="914"`, `passed="914"`,
`failed="0"`, and `notExecuted="0"`.

The final orphan scan remains identical to baseline: one `<7>`, one `<10>`,
and seven `<1>` execution warnings.

`rtk git diff --check` passed.

## Files Changed

- `tests/ui/BattleManagerTest.cs`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-7-report.md`

## Self-Review

- Both current tests use the public close method and result signal with the real
  dialog scene; reflection is limited to the legacy timer/combatant state
  needed to establish preparation and active-combat fixtures.
- Each transient scene has `finally` cleanup that remains safe after production
  has queued it for deletion.
- The correction changes no Battle production behavior.

## Concern

None. The corrected public-path characterization is covered by the focused
suite and the persistent 914-test final-review TRX.
