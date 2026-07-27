# Final Review Fix 2 Report: Dialog Binding Ownership

## Status

DONE.

Executable implementation commit under test:
`61b6e3d9658f9451f7182f12a47a7724a939e55e`
(`fix: preserve native dialog close bindings`).

## Finding resolved

The configured-Cancel bridge tracked injected `ui_close_dialog` events by
retaining their `InputEvent` references. Godot's
`InputMap.ActionEraseEvent` locates an equivalent event rather than requiring
object identity. If another owner or a test restored the action with a distinct
equivalent native binding, the static tracker became stale and a later resync
could erase that native replacement.

Godot 4.6.2 source confirms the relevant boundary:

- `action_add_event` stores the supplied `Ref<InputEvent>` without cloning it;
- `action_get_events` returns those stored references;
- `action_erase_event` uses `_find_event(..., exact_match: true)`, which is
  input equivalence rather than object identity.

Source:
[`core/input/input_map.cpp` at 4.6.2-stable](https://github.com/godotengine/godot/blob/4.6.2-stable/core/input/input_map.cpp#L602-L664).

## Executable change

`SettingsManager.SynchronizeDialogCloseBindings` now:

1. stores only the instance IDs of exact injected events;
2. scans the current `ui_close_dialog` events on resync;
3. erases a current event only when its exact instance ID is owned;
4. clears ownership after that scan, so absent IDs are discarded without
   touching equivalent replacements;
5. skips adding a mirror when an equivalent native event already exists;
6. after adding a mirror, confirms the exact instance is present in the
   current `InputMap` before recording its ID.

No public test-only reset, dialog-host behavior, or HPA-378/379 architecture was
added.

## TDD evidence

### RED

The dedicated regression first created an injected P mirror, rebuilt
`ui_close_dialog` with a distinct equivalent native P event, then changed the
configured binding to Q.

```text
Failed: 1, Passed: 0, Skipped: 0, Total: 1
Expecting be equal: '9223372064570738138' but is '0'
```

The missing P instance demonstrated that stale ownership erased the native
replacement.

### GREEN

After exact-instance ownership:

- dedicated ownership regression: 1/1 passed;
- `SettingsManagerTest`: 52/52 passed;
- `GameInputLifecycleTest`: 10/10 passed;
- `SettingsManagerTest|GameInputLifecycleTest` in one process: 62/62 passed;
- `SettingsDataTest|SettingsMenuControllerTest`: 58/58 passed.

The regression also remaps Q to R and verifies that the same native P instance
survives both resyncs while the owned configured mirror advances.

## Authoritative full-suite verification

Command:

```text
rtk zsh -o pipefail -c 'rtk proxy dotnet test Sirius.sln --settings test.runsettings.local --no-restore --results-directory .superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts --logger "trx;LogFileName=final-review-fix-2-full.trx" --logger "console;verbosity=minimal" 2>&1 | rtk tee /tmp/hpa-376-final-review-fix-2.log'
```

Console:

```text
Passed!  - Failed: 0, Passed: 915, Skipped: 0, Total: 915
```

Persistent TRX:

```text
outcome="Completed"
total="915" executed="915" passed="915" failed="0" notExecuted="0"
```

The ignored local TRX is
`.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/artifacts/final-review-fix-2-full.trx`.

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
- Non-replacement rows: 43; every row retains current evidence.
- Exact replacement handoffs: 7; missing/unpaired: 0.
- Test delta: 863 implementation-start + 52 named additions = 915.
- Fix 2 accounts for +1:
  `SettingsManagerTest.SettingsManager_ReplacedEquivalentDialogCloseBinding_RemainsNativeAcrossResyncs`.

## Files

Executable commit:

- `scripts/settings/SettingsManager.cs`
- `tests/settings/SettingsManagerTest.cs`

Documentation correction:

- `docs/ui/hpa-376/ui-lifecycle-contract.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/task-9-report.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/final-review-fix-1-report.md`
- `.superpowers/sdd/2026-07-26-sirius-ui-lifecycle-baseline/final-review-fix-2-report.md`

## Concerns

No correctness or scope concern remains. The authoritative run still reports
the repository's pre-existing compiler warnings, environmental NuGet `NU1900`
warning, and unchanged baseline orphan signature.
