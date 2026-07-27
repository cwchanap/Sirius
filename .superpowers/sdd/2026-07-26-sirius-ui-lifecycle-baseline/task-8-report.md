# Task 8 Report: Game-Level Priority, Defeat Navigation, and Prompt Restoration

**Status:** DONE
**Implementation commit:** `3f8778e` (`fix: enforce Game UI lifecycle priority`)

## Implementation

- Added deterministic `pause_menu` routing for a valid, visible `BattleManager` after error/save/settings/deferred-restoration handling and before gameplay Pause fallback.
- Marked the routed battle-result input handled and retained the existing in-battle missing-dialog fallback.
- Added instance-validity guards around remaining `BattleManager` lifecycle access in routing, result handling, confirmation cleanup, and `Game._ExitTree()`.
- Replaced the unowned defeat callback with an owned `SceneTreeTimer`, retained handler, single-schedule semantics, cleanup invalidation, and protected delay/navigation seams.
- Refreshed the interaction prompt when battle and NPC blocking flags begin or end, including the NPC `Begin()` failure path.
- Added real-scene integration coverage for floor replacement rebinding/deferred prompt refresh and battle prompt hide/restore.
- Did not add `UIScreenHost`, navigation-stack abstractions, shared host interfaces, or HPA-378/379 behavior.

## TDD Evidence

### RED: visible battle result priority

Command:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameInputLifecycleTest" \
  --logger "console;verbosity=normal"
```

Before the routing fix:

- `BattleResultCancelIsHandledAndClosesResultWithoutOpeningPause` failed because `battle.Visible` remained `true`; the legacy fallback attempted to open Pause behind the exclusive battle result.
- `ErrorCancel_DismissesOnlyErrorAndLeavesPauseVisible` passed.
- `PauseRestorePending_ConsumesWithoutChangingVisibleStack` passed when isolated; its first combined run was affected by the preceding still-open exclusive battle window.

Result: 1 passed, 2 failed. The battle failure matched the expected missing topmost-routing behavior.

### GREEN: priority routing

After adding visible-battle routing and validity guards, the three initial lifecycle tests passed: 3 passed, 0 failed.

### RED: owned defeat seams

Command:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameInputLifecycleTest" \
  --logger "console;verbosity=minimal"
```

Compilation failed as expected:

```text
CS0115: 'LifecycleGame.DefeatReturnDelaySeconds': no suitable method found to override
CS0115: 'LifecycleGame.ReturnToMainMenu()': no suitable method found to override
```

This proved the requested deterministic delay and navigation seams did not yet exist.

### GREEN: complete focused lifecycle suite

Command:

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameInputLifecycleTest" \
  --logger "console;verbosity=minimal"
```

Result: 7 passed, 0 failed, 0 skipped.

## Verification

### Existing adjacent suite

```bash
rtk dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameTest" \
  --logger "console;verbosity=minimal"
```

Result: 45 passed, 0 failed, 0 skipped.

### Full suite

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local \
  --results-directory .superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts \
  --logger "trx;LogFileName=task-8-full.trx" \
  --logger "console;verbosity=minimal"
```

TRX result:

```text
outcome="Completed"
total="912" executed="912" passed="912" failed="0" notExecuted="0"
```

Artifact:

`.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts/task-8-full.trx`

The full-suite summary contains the same nine baseline orphan-warning lines recorded by the lifecycle contract, with the existing 7-node, 10-node, and 1-node messages. Task 8 did not increase the baseline count. The build continues to report the repository's pre-existing compiler warnings and the transient NuGet vulnerability-source warning.

An initial full-suite attempt hit GdUnit4's 20-second Godot compilation timeout before execution. A warm rerun completed successfully and produced the retained 912/912 TRX above.

### Diff hygiene

- `rtk git diff --check`: clean.
- Staged scope before commit: only `scripts/game/Game.cs` and `tests/game/GameInputLifecycleTest.cs`.

## Files

- `scripts/game/Game.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-8-report.md`

## Self-Review

- Each new test names an observable lifecycle break: wrong Cancel recipient, parent fallthrough, stale navigation, duplicate navigation, stale floor binding, or missing prompt restoration.
- Defeat tests override navigation and never perform a real scene transition.
- Cleanup frees the local `LifecycleGame` before its `SubViewport`, immediately frees real Game scenes, and waits for deferred cleanup.
- The visible battle route follows the approved modal priority exactly and does not alter battle result payload or timing.
- The timer retains and disconnects its exact handler, cancels on reschedule and `_ExitTree()`, and checks owner tree membership before navigation.
- Prompt refreshes are limited to the specified battle/NPC boundaries; existing treasure, puzzle, and floor refresh behavior remains intact.
- No combat, reward, inventory, save, NPC, puzzle, or floor domain contract changed.

## Concerns

No correctness or scope concerns remain. The only verification concern was the transient first-run GdUnit4 compilation timeout; the subsequent retained full-suite artifact is complete and green.
