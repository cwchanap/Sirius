# Task 6 Report: NPC Transition and Shop Cleanup

## Status

Completed after the approved Task 6 plan revision authorized the single
ShopDialog timer-cleanup change.

## Implementation

- Added `NpcInteractionControllerTest` with a SceneTree-attached `_uiParent`
  fixture, two-frame cleanup, catalog NPC helpers, and the four requested
  dialogue, Shop, Heal, and missing-dialogue-tree lifecycle tests.
- Added Shop close idempotency and pending-feedback-timer tests, including the
  separate nullable reflection helper.
- Changed `ShopDialog.OnCloseRequested()` only to call
  `CancelFeedbackTimer()` after it acquires the existing `_closed` gate and
  before hiding/emitting `ShopClosed`.
- Changed the existing ShopDialog fixture from suite-level `[Before]/[After]`
  to per-test `[BeforeTest]/[AfterTest]`. This prevents a prior close test
  from carrying `_closed = true` into the timer-cleanup test.

## TDD Evidence

### RED

Before the approved production change, the focused runtime suite was run at
the unsandboxed Godot boundary (the filesystem sandbox could not connect to
the Godot runner):

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest" --logger "console;verbosity=minimal"
```

Result: 7 passed, 1 failed, 0 skipped. The required
`ShopDialogTest.Close_CancelsPendingFeedbackTimer` observed a non-null
`Godot.SceneTreeTimer` after `Canceled`, as expected because
`OnCloseRequested()` had no `CancelFeedbackTimer()` call.

### GREEN

After the approved close-time cleanup and the per-test fixture correction,
the same focused command passed:

- 8 passed, 0 failed, 0 skipped.

The timer test also passed in isolation. The temporary combined-suite failure
after the production change was traced to the previous test's `_closed` state
leaking through the existing suite-level fixture; it was not a production
failure. The compiled `ShopDialog` was inspected and confirmed to include the
new cleanup call before the fixture was corrected.

## Full-Suite Verification

```bash
rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory /private/tmp/hpa-376-task6-results-20260727 --logger "trx;LogFileName=task6.trx"
```

The console omitted its final aggregate after the existing orphan-node
warnings. The persisted
`/private/tmp/hpa-376-task6-results-20260727/task6.trx` records a Completed
run with 902 total/executed/passed, 0 failed, and 0 skipped.

`rtk git diff --check` and the equivalent untracked-file check both passed.

## Files Changed

- `scripts/ui/ShopDialog.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/ui/ShopDialogTest.cs`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-6-report.md`

## Self-Review

- The production diff is limited to immediate feedback-timer detachment after
  the pre-existing terminal gate; ownership and public signals are unchanged.
- The NPC tests use fresh catalog NPCs, characters, quest flags, and a
  SceneTree-attached UI parent that is freed after each case.
- The Shop fixture is now isolated per case, so terminal state cannot leak
  between close and timer assertions.
- No `NpcInteractionController` production code changed.

## Concerns

None. The full suite retains the repository's pre-existing orphan-node
warnings; the persisted TRX is the authoritative all-pass result.
