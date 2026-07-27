# Task 9 Report: Final HPA-376 Lifecycle Reconciliation

## Status

DONE after final whole-branch review fix 1.

## Commits

- Implementation-start commit:
  `33e9567b04d06bf532ce1d7deb3b6432cb5a927a`.
- Executable implementation commit under test:
  `30e86d259f88259d6e2779f8280f6b975403b0e1`
  (`fix: route configured cancel to modal owners`).
- Initial Task 9 contract commit:
  `b9bc4c09e0b5fbaff5e6be38d53526d7d8b9645e`.

## Final-review corrections

- `SettingsManager` now preserves the effective `ui_cancel` key/controller
  bindings while synchronizing them into Godot's actual
  `ui_close_dialog` action for `AcceptDialog` owners.
- Synchronization tracks and removes only previously injected events, preserves
  native close bindings, and removes stale configured bindings on reapply.
- `GameInputLifecycleTest` now uses `[BeforeTest]`/`[AfterTest]`, a fresh
  tree-owned fixture per case, InputMap/audio/override snapshots, explicit
  restoration, and awaited cleanup.
- Three real physical-input cases use `SubViewport.PushInput`:
  - configured keyboard closes an embedded `AcceptDialog` exactly once;
  - configured controller closes `SaveLoadDialog` exactly once;
  - configured keyboard closes the topmost real Game riddle, clears the world
    interaction flag, restores the prompt, and does not open Pause.
- Battle characterization now matches the public path. Preparation and active
  escape emit once and immediately hide/queue; active escape also stops the
  timer and clears transient effects. The private-seam post-result test was
  removed because public escape exposes no standalone player-visible Continue
  surface.
- `BATTLE-ESC-ACTIVE` and `BATTLE-RESULT-ESCAPE` no longer claim a visible
  escape-result acknowledgement. Victory/defeat result routing remains
  documented separately.
- `NPC-SHOP` is classified `Fix in HPA-376` because its once-only close and
  feedback-timer cleanup were implemented in this work.

The resulting 50-row disposition split is:

- 30 `Preserve`
- 13 `Fix in HPA-376`
- 7 `Replace in HPA-378/379`

## TDD evidence

Before the production mapping change:

- the three physical-input lifecycle tests failed 3/3 with terminal counts
  remaining 0;
- the four selected SettingsManager mapping assertions failed 4/4 because the
  configured key/controller events were absent from `ui_close_dialog`;
- the pre-existing lifecycle routing subset still passed 3/3, isolating the
  failure to the missing action bridge.

After the change:

- each new physical-input test passed alone (1/1 each);
- their combined subset passed 3/3;
- the complete per-test-isolated `GameInputLifecycleTest` suite passed 10/10
  without an orphan warning.

## Matrix and evidence validation

The final audits report:

```text
row_count=50, duplicates=no
dispositions: Preserve=30, Fix in HPA-376=13, Replace in HPA-378/379=7
nonreplacement_rows=43, evidence_tokens=91, missing_evidence=0
unique_source_citations=66, unresolved=0
```

All seven exact replacement handoff strings remain present. The input-surface
scan covers `pause_menu`, direct `ui_cancel`, dialog `ui_close_dialog`,
`CloseRequested`/window close, explicit actions, and `toggle_inventory`.

## Focused verification

All commands used `rtk proxy dotnet test`; later commands used
`--no-build --no-restore` against the same compiled implementation commit.

| Focused filter | Passed | Failed | Skipped |
|---|---:|---:|---:|
| `InventoryMenuControllerTest` | 8 | 0 | 0 |
| `SettingsManagerTest` | 51 | 0 | 0 |
| `SettingsDataTest` | 4 | 0 | 0 |
| `SettingsMenuControllerTest` | 54 | 0 | 0 |
| `PauseMenuDialogTest` | 14 | 0 | 0 |
| `MainMenuTest` | 4 | 0 | 0 |
| `SaveLoadDialogTest` | 10 | 0 | 0 |
| `DialogueDialogTest\|HealDialogTest\|PuzzleRiddleDialogTest` | 6 | 0 | 0 |
| `NpcInteractionControllerTest\|ShopDialogTest` | 8 | 0 | 0 |
| `BattleManagerTest` | 14 | 0 | 0 |
| `GameInputLifecycleTest\|GameTest` | 55 | 0 | 0 |

Every focused process exited 0.

## Full-suite evidence

The authoritative suite ran outside the filesystem sandbox on executable commit
`30e86d259f88259d6e2779f8280f6b975403b0e1`:

```text
rtk zsh -o pipefail -c 'rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory .superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts --logger "trx;LogFileName=final-review-fix-1-full.trx" --logger "console;verbosity=minimal" 2>&1 | rtk tee /tmp/hpa-376-final-review-fix-1.log'
```

Console result:

```text
Passed!  - Failed: 0, Passed: 914, Skipped: 0, Total: 914
```

Persistent TRX result:

```text
outcome="Completed"
total="914" executed="914" passed="914" failed="0" notExecuted="0"
```

Evidence provenance:

- baseline console log: `/tmp/hpa-376-test-baseline.log`;
- final console log: `/tmp/hpa-376-final-review-fix-1.log`;
- final ignored local TRX:
  `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts/final-review-fix-1-full.trx`.

The logs and TRX are local evidence, not branch content.

## Orphan comparison

Baseline and final logs each contain exactly nine matching warnings:

- one `Detected <7> orphan nodes during test execution!`;
- one `Detected <10> orphan nodes during test execution!`;
- seven `Detected <1> orphan nodes during test execution!`.

No new orphan line or distinct message was introduced.

## Test-total delta accounting

The final 914 total is the 863-test implementation start plus 51 named HPA-376
tests:

- Task 2: +7 Inventory lifecycle tests
- Task 3: +2 Settings and +4 Pause tests
- Task 4: +4 Main Menu and +10 Save/Load tests
- Task 5: +2 Dialogue, +2 Heal, and +2 Puzzle Riddle tests
- Task 6: +4 NPC orchestration and +2 Shop tests
- Task 7 after correction: +2 public-path BattleManager lifecycle tests
- Task 8: +7 Game input/prompt/floor/defeat lifecycle tests
- Final review fix 1: +3 physical configured-cancel lifecycle tests

These groups sum to 51. The historical 869-test count at `bc82ead` is not used
as the implementation baseline.

## Scope and hygiene

- No HPA-378/379 host, navigation, theme, process-mode, mouse, or reward
  architecture was implemented.
- No Battle production behavior was changed.
- The action bridge is confined to Settings runtime binding application.
- The ignored console/TRX evidence did not enter branch content.
- `git diff --check`, row/disposition/evidence/source audits, and clean final
  status are required before the documentation commit.

## Concerns

No correctness or scope concern remains.

The repository still emits its pre-existing compiler warnings and environmental
NuGet `NU1900` vulnerability-source warning. The nine orphan warnings are also
pre-existing and unchanged from the captured baseline.
