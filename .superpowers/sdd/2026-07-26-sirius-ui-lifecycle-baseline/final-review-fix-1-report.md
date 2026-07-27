# Final Review Fix 1 Report: Configured Cancel and Lifecycle Evidence

## Status

DONE.

Executable implementation commit under test:
`30e86d259f88259d6e2779f8280f6b975403b0e1`
(`fix: route configured cancel to modal owners`).

This report preserves the fix-1 evidence at that commit. Final-review fix 2
subsequently hardened injected-event ownership in
`61b6e3d9658f9451f7182f12a47a7724a939e55e`; current branch totals and evidence
are recorded in the addendum below and in `final-review-fix-2-report.md`.

## Findings resolved

1. `SettingsManager` mirrored the configured `pause_menu` key only to
   `ui_cancel`, while Godot `AcceptDialog` closes through
   `ui_close_dialog`. Configured keyboard/controller Cancel therefore did not
   reach native modal owners.
2. The lifecycle integration suite used suite-level setup/cleanup and synthetic
   action events. It did not prove physical InputMap delivery, per-test
   isolation, topmost ownership, or cleanup.
3. Battle evidence claimed that active escape left a player-visible Continue
   result. The public `ForceCloseAsEscape` path actually emits once and then
   immediately hides/queues the dialog.
4. `NPC-SHOP` remained classified `Preserve` even though HPA-376 implemented
   once-only close and feedback-timer cleanup.

## Executable changes

### Configured Cancel bridge

`SettingsManager.ApplyInputBindings` now:

- keeps the configured key mirrored to `ui_cancel`;
- preserves non-key `ui_cancel` events;
- removes only events previously injected into `ui_close_dialog`;
- preserves native `ui_close_dialog` bindings;
- duplicates every currently effective `ui_cancel` event missing from
  `ui_close_dialog`;
- replaces stale configured events safely on later apply/rebind.

No modal host architecture or HPA-378/379 behavior was introduced.

### Physical lifecycle coverage

`GameInputLifecycleTest` now uses `[BeforeTest]`/`[AfterTest]` with a fresh
SubViewport, lifecycle Game, non-singleton tree-owned GameManager, and
InputMap/audio/settings-override snapshots per case. Cleanup frees real and
fixture Game trees, awaits process frames, and restores every captured global.

The three new tests use real InputMap mappings and physical
`SubViewport.PushInput` events:

- `ConfiguredKeyboardCancel_ClosesAcceptDialogExactlyOnce`
- `ConfiguredControllerCancel_ClosesSaveLoadDialogExactlyOnce`
- `ConfiguredKeyboardCancel_ClosesTopmostRiddleAndRestoresGameplay`

Window-derived dialogs are explicitly embedded in the fresh SubViewport so
Godot forwards `PushInput` to the child window. The tests assert the physical
event matches `ui_close_dialog`, send a second close input to prove exactly-once
termination, and verify visibility/flags/prompt/Pause restoration.

### Battle characterization

The current public-path tests are:

- `ForceCloseDuringPreparation_EmitsOnceAndClosesImmediately`
- `ForceCloseDuringAutomaticCombat_StopsTimerClearsEffectsEmitsOnceAndClosesImmediately`

The private `EndBattleWithEscape` post-result characterization was removed.
`BattleManager` production code was not changed.

## TDD evidence

### RED

Before production changes, the physical-input subset returned:

```text
Failed: 3, Passed: 0, Skipped: 0, Total: 3
```

Each expected a terminal count of 1 and observed 0. The pre-existing lifecycle
routing subset still passed 3/3, isolating the missing action bridge.

The four selected SettingsManager mapping assertions also failed 4/4 because
the expected key/controller events were absent from `ui_close_dialog`.

### GREEN

- each new physical-input case alone: 1/1 passed;
- combined physical-input subset: 3/3 passed;
- `GameInputLifecycleTest`: 10/10 passed, no orphan warning;
- `SettingsManagerTest`: 51/51 passed;
- `BattleManagerTest`: 14/14 passed;
- `SettingsDataTest|SettingsMenuControllerTest`: 58/58 passed;
- Main Menu/Save/Dialogue/Heal/Riddle/NPC/Shop group: 28/28 passed;
- Inventory/Pause group: 22/22 passed;
- `GameInputLifecycleTest|GameTest`: 55/55 passed.

Every focused process exited 0.

## Authoritative full-suite verification

Command:

```text
rtk zsh -o pipefail -c 'rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory .superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts --logger "trx;LogFileName=final-review-fix-1-full.trx" --logger "console;verbosity=minimal" 2>&1 | rtk tee /tmp/hpa-376-final-review-fix-1.log'
```

Console:

```text
Passed!  - Failed: 0, Passed: 914, Skipped: 0, Total: 914
```

Persistent TRX:

```text
outcome="Completed"
total="914" executed="914" passed="914" failed="0" notExecuted="0"
```

The ignored local TRX is
`.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts/final-review-fix-1-full.trx`.

## Orphan comparison

Baseline and final console logs have the same nine-line signature:

- one `Detected <7> orphan nodes during test execution!`;
- one `Detected <10> orphan nodes during test execution!`;
- seven `Detected <1> orphan nodes during test execution!`.

No new orphan warning or distinct signature was introduced.

## Contract reconciliation

- Matrix rows: 50; duplicate IDs: none.
- Dispositions: 30 `Preserve`, 13 `Fix in HPA-376`,
  7 `Replace in HPA-378/379`.
- Non-replacement rows: 43.
- Exact evidence tokens: 91; missing: 0; rows without current evidence: 0.
- Unique source citations: 66; unresolved: 0.
- Exact replacement handoffs: 7; missing/unpaired: 0.
- Test delta: 863 implementation-start + 51 named additions = 914.

The contract now distinguishes direct `ui_cancel` consumers from
`AcceptDialog`'s synchronized `ui_close_dialog`, describes public Battle escape
as immediate close, retains `BATTLE-RESULT-ESCAPE` as an explicit
non-player-visible lifecycle-inventory row, and classifies `NPC-SHOP` as
`Fix in HPA-376`.

## Files

Executable commit:

- `scripts/settings/SettingsManager.cs`
- `tests/settings/SettingsManagerTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/ui/BattleManagerTest.cs`

Documentation correction:

- `docs/ui/hpa-376/ui-lifecycle-contract.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-7-report.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-9-report.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/final-review-fix-1-report.md`

## Concerns

No correctness or scope concern remains. Pre-existing compiler warnings,
environmental NuGet `NU1900`, and the unchanged baseline orphan signature
remain visible in the authoritative run.

## Final-review-fix-2 addendum

The fix-1 bridge originally retained injected `InputEvent` references.
`InputMap.ActionEraseEvent` matches equivalent bindings, so external action
reconstruction could leave a stale tracked reference that later erased a
distinct native replacement. Fix 2 records exact stored instance IDs, erases
only a currently present owned ID, and drops missing ownership without touching
an equivalent replacement.

TDD evidence:

- RED: the new ownership regression failed 1/1; after native P was
  reconstructed, the next resync returned instance ID `0` instead of the
  expected native P ID.
- GREEN: the regression passed 1/1; `SettingsManagerTest` passed 52/52;
  `GameInputLifecycleTest` passed 10/10; both suites passed together 62/62; and
  `SettingsDataTest|SettingsMenuControllerTest` passed 58/58.

The authoritative suite on executable commit
`61b6e3d9658f9451f7182f12a47a7724a939e55e` passed 915/915 with TRX
`outcome="Completed"` and
`total="915" executed="915" passed="915" failed="0" notExecuted="0"`.
The implementation-start accounting is now 863 + 52 = 915, with fix 2 adding
`SettingsManagerTest.SettingsManager_ReplacedEquivalentDialogCloseBinding_RemainsNativeAcrossResyncs`.
The orphan signature remains the same nine lines as baseline: one `<7>`, one
`<10>`, and seven `<1>` warnings.
