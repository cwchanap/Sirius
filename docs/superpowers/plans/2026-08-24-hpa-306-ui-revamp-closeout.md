# HPA-306 Sirius UI Revamp Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close HPA-306 on current `main` by reusing HPA-359 production evidence, validating the post-HPA-359 HPA-375/HPA-541 delta, refreshing only the unique Inventory visual evidence, and recording one final disposition.

**Architecture:** Keep the shipped scene-authored UI, Theme/components, root-local `UIScreenHost`, and controller/domain ownership unchanged. HPA-359 remains authoritative for the wider production walkthrough. HPA-306 adds fresh current-head automated coverage plus one narrowly-scoped production Inventory usability/runtime check captured from the Godot root viewport. A no-production-code closeout is expected; any real defect must be RED first in its existing owner suite.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, `@cwchanap/godot-plugin@0.1.4`, existing Sirius Theme/components/`UIScreenHost`, Linear HPA-306.

**Spec:** `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`

## Global Constraints

- [ ] Keep planning, validation evidence, and any regression fix in this single HPA-306 PR.
- [ ] Do not add a screen, host kind, UI service, presenter/view-model, navigation framework, second host, E2E driver, screenshot harness, or release framework.
- [ ] Reuse `docs/ui/hpa-359/release-validation.md`; do not replay its full production journey without a delta-specific failure.
- [ ] HPA-375/HPA-541 are already implemented; validate them rather than extending product scope.
- [ ] A clean closeout with no production C# or `.tscn` change is valid and preferred.
- [ ] The live Inventory pass owns only human usability, runtime UI errors, and fresh root-viewport evidence. Existing tests own behavior/focus/geometry.
- [ ] Inherit HPA-359's compact Inventory disposition: tight/clipped section-heading chrome at `640×360` is non-blocking while required content/controls remain usable.
- [ ] Create exactly two HPA-306 Inventory evidence PNGs. Do not overwrite HPA-359 evidence and do not recapture unrelated screens.
- [ ] Use `origin/main...HEAD` after `git fetch origin main`; do not use a potentially stale local `main` for scope gates.
- [ ] Treat the Godot runtime bridge as temporary tooling and prove `project.godot` is restored before final scope verification.
- [ ] Do not load, modify, back up, or restore user saves for this closeout.
- [ ] Mark Linear HPA-306 Done only after PR #48 is merged.

---

## Task 1: Validate the post-HPA-359 delta on current head

**Files:**
- Reference: `docs/ui/hpa-359/release-validation.md`
- Reference: `scenes/ui/InventoryMenu.tscn`
- Reference: `scripts/ui/InventoryMenuController.cs`
- Reference: `scenes/ui/SettingsMenu.tscn`
- Reference: `scripts/ui/SettingsMenuController.cs`
- Reference: `scripts/settings/SettingsData.cs`
- Reference: `scripts/settings/SettingsManager.cs`
- Reference: `scripts/game/Game.cs`
- Reference: `scripts/game/GridMap.cs`
- Reference: `scripts/game/PlayerDisplay.cs`
- Reference: `scripts/game/EnemySpawn.cs`
- Reference: `scripts/ui/BattleManager.cs`
- Test: `tests/ui/InventoryMenuControllerTest.cs`
- Test: `tests/ui/InventoryMenuSceneTest.cs`
- Test: `tests/settings/SettingsDataTest.cs`
- Test: `tests/settings/SettingsManagerTest.cs`
- Test: `tests/ui/SettingsMenuControllerTest.cs`
- Test: `tests/ui/SettingsMenuSceneTest.cs`
- Test: `tests/game/GridMapTest.cs`
- Test: `tests/game/PlayerDisplayTest.cs`
- Test: `tests/game/EnemySpawnTest.cs`
- Test: `tests/game/GameTest.cs`
- Test: `tests/game/GameplayPauseHostTest.cs`
- Test: `tests/ui/BattleManagerTest.cs`
- Test: `tests/ui/BattleSceneTest.cs`

**Interfaces:**
- Consumes: HPA-359 evidence and merged HPA-375/HPA-541 production code.
- Produces: current-head build/focused/full-suite results plus a fresh remote base for later PR-scope checks.

- [ ] **Step 1: Refresh the remote base**

```bash
git fetch origin main
```

Expected: `origin/main` points at the current repository default-branch head. Do not require the local `main` branch to move.

- [ ] **Step 2: Read the existing HPA-359 evidence boundary**

Read `docs/ui/hpa-359/release-validation.md` and carry forward:

- Main Menu → Game and Game → Main Menu scene replacement;
- movement → goblin → Battle/result;
- Save/Prompt and hosted joypad evidence;
- default-motion production behavior;
- legacy-path audit;
- compact Inventory heading chrome as usable/non-blocking visual polish.

Do not convert these into new manual acceptance steps.

- [ ] **Step 3: Ensure local GdUnit settings exist**

```bash
test -f test.runsettings.local || cp test.runsettings.local.template test.runsettings.local
```

If copied, configure only the local Godot executable path required by the existing template. Do not commit `test.runsettings.local`.

- [ ] **Step 4: Build current head**

```bash
dotnet build Sirius.sln
```

Expected: exit 0 and no compile errors. Record the exact result for Task 3. Classify known environment warnings separately from product failures.

- [ ] **Step 5: Run the focused HPA-375/HPA-541 integration set**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: all discovered tests pass. Record exact pass/fail/skip totals.

This is the deterministic owner for Inventory behavior/geometry/focus and HPA-541 Settings/world/Game/Battle behavior. Do not recreate those assertions in Task 2.

- [ ] **Step 6: Run the full umbrella completion gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: full suite passes. Record exact pass/fail/skip totals.

The focused filter is diagnostic only; it does not replace this full-suite HPA-306 gate.

- [ ] **Step 7: Check committed PR whitespace against the refreshed remote base**

```bash
git diff --check origin/main...HEAD
```

Expected: no whitespace errors.

- [ ] **Step 8: If automation is RED, fix only through an existing owner**

If an existing test already fails specifically for the regression, use that failure as RED. If the failure is too broad, first add/tighten the smallest useful regression in the nearest existing Inventory/Settings/world/Game/Battle/host suite.

Only after a specific RED exists:

1. make the smallest production fix in the existing owner;
2. rerun that owner suite;
3. rerun Steps 5–7;
4. run Task 2 again only if the changed production files can affect Inventory composition.

Do not introduce a reusable abstraction for a one-off closeout defect.

---

## Task 2: Capture the unique Inventory production-window delta

**Files:**
- Reference: `project.godot`
- Reference: `scripts/game/GameManager.cs`
- Reference: `scenes/ui/InventoryMenu.tscn`
- Reference: `scripts/ui/InventoryMenuController.cs`
- Owning visual suite: `tests/ui/InventoryMenuSceneTest.cs`
- Create: `docs/ui/hpa-306/evidence/inventory-1280x720.png`
- Create: `docs/ui/hpa-306/evidence/inventory-640x360.png`
- Modify only after a RED visual regression: nearest existing Inventory owner

**Interfaces:**
- Consumes: a clean project, the published Godot MCP `0.1.4` runtime-control path, New Game starter equipment/consumables, and Task 1's automated behavior/geometry evidence.
- Produces: two exact-size root-viewport captures, live human-usability/runtime observations, and proof that runtime-bridge project mutations were removed.

- [ ] **Step 1: Start from a clean runtime-bridge state and record `project.godot`**

Before runtime control, ensure the project is not already carrying bridge residue. If Godot MCP reports an installed bridge, call `uninstall_runtime_bridge` first. Then run:

```bash
test ! -e .godot-mcp
git diff --exit-code -- project.godot
BASE_PROJECT_GODOT_SHA="$(shasum -a 256 project.godot | awk '{print $1}')"
printf '%s\n' "$BASE_PROJECT_GODOT_SHA" > /tmp/hpa-306-project-godot.sha
```

Expected: `.godot-mcp` is absent, `project.godot` has no local tracked diff, and the baseline hash is stored outside the repository.

- [ ] **Step 2: Launch the real project with the proven runtime-control capture path**

Use the Godot MCP server pinned by this plan (`npx -y @cwchanap/godot-plugin@0.1.4`). Start Sirius with its `run_project` tool and `runtimeControl: true`.

Runtime control automatically installs/repairs the managed bridge. Do **not** use a normal OS screenshot for evidence; Retina device-pixel scaling can make a logical `640×360` window produce a `1280×720` OS capture.

Use `get_debug_output` during the session to observe runtime errors.

- [ ] **Step 3: Start New Game; do not load a user save**

From the production Main Menu choose **New Game**.

`GameManager.InitializePlayer()` already equips starter gear and adds starter consumables, so the Inventory is populated enough for visual review. Do not open Load, Save, or overwrite flows, and do not touch user save files.

Current battle-victory autosave is disabled; no save backup/restore ceremony is needed for this task.

- [ ] **Step 4: Observe and capture `1280×720`**

Set the actual game viewport/window to logical `1280×720`, open Inventory through the production gameplay path, and use normal interaction only as needed to expose representative Details content.

Manual pass criteria are intentionally narrow:

- a human can use/read the visible Inventory composition;
- no new runtime UI error appears.

Do not manually score zero-width regions, bounds, selection semantics, filter/sort semantics, focus, page-navigation policy, or Close behavior. Task 1's GdUnit owners already cover those deterministically.

Create the evidence directory:

```bash
mkdir -p docs/ui/hpa-306/evidence
```

Call Godot MCP `capture_screenshot` with:

```json
{"saveTo":"project"}
```

The tool persists a unique root-viewport PNG beneath `.godot-mcp/captures/`. Copy the exact persisted path returned by the tool to:

```text
docs/ui/hpa-306/evidence/inventory-1280x720.png
```

- [ ] **Step 5: Observe and capture `640×360`**

Resize the same production game to logical `640×360` and inspect Inventory with the same narrow criteria:

- a human can still use/read required content and controls;
- no new runtime UI error appears.

Known accepted condition: section-heading chrome may remain tight/clipped as recorded by HPA-359. Do not open product work for that alone.

Call `capture_screenshot` again with:

```json
{"saveTo":"project"}
```

Copy the returned persisted capture to:

```text
docs/ui/hpa-306/evidence/inventory-640x360.png
```

- [ ] **Step 6: Verify exact root-viewport image dimensions**

Verify both PNGs without adding a dependency:

```bash
python3 - <<'PY'
from pathlib import Path
import struct

expected = {
    Path("docs/ui/hpa-306/evidence/inventory-1280x720.png"): (1280, 720),
    Path("docs/ui/hpa-306/evidence/inventory-640x360.png"): (640, 360),
}

for path, wanted in expected.items():
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", f"{path}: not PNG"
    actual = struct.unpack(">II", data[16:24])
    assert actual == wanted, f"{path}: expected {wanted}, got {actual}"
    print(f"{path}: {actual[0]}x{actual[1]}")
PY
```

Expected: exactly `1280x720` and `640x360`.

Do not compute screenshot SHA-256 values; they add no useful verification for this closeout.

- [ ] **Step 7: Tear down the runtime bridge before any scope check**

Stop the running project with Godot MCP `stop_project`, then call `uninstall_runtime_bridge` for Sirius.

Verify cleanup:

```bash
test ! -e .godot-mcp
CURRENT_PROJECT_GODOT_SHA="$(shasum -a 256 project.godot | awk '{print $1}')"
BASE_PROJECT_GODOT_SHA="$(cat /tmp/hpa-306-project-godot.sha)"
test "$CURRENT_PROJECT_GODOT_SHA" = "$BASE_PROJECT_GODOT_SHA"
git diff --exit-code -- project.godot
rm -f /tmp/hpa-306-project-godot.sha
```

Expected: bridge directory absent, exact pre/post `project.godot` hash match, no tracked `project.godot` diff.

This hash is load-bearing cleanup verification and is intentionally retained even though screenshot hashes were removed.

- [ ] **Step 8: If the live pass exposes a new blocking visual defect, make it RED first**

Do not treat the known compact heading clip as a defect unless it makes required content/controls unusable.

For a new blocking visual issue, add the smallest deterministic regression to `tests/ui/InventoryMenuSceneTest.cs` and run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuSceneTest"
```

Expected before production edit: new regression fails for the intended reason.

Then make the smallest existing-owner fix, rerun the scene suite and Task 1 Steps 5–7, repeat only the affected live viewport, refresh that capture, and repeat Step 7 teardown.

---

## Task 3: Write the durable closeout record and verify PR scope

**Files:**
- Create: `docs/ui/hpa-306/closeout.md`
- Include: `docs/ui/hpa-306/evidence/inventory-1280x720.png`
- Include: `docs/ui/hpa-306/evidence/inventory-640x360.png`
- Reference: `docs/ui/hpa-359/release-validation.md`
- Reference: planning spec/plan
- Include only if validation found a defect: its focused regression + minimal existing production owner

**Interfaces:**
- Consumes: Task 1 exact command results, Task 2 observations/dimensions/cleanup result, and HPA-359 evidence.
- Produces: one repository closeout record and a final five-file clean shape when no defect was found.

- [ ] **Step 1: Create `docs/ui/hpa-306/closeout.md` from observed facts**

Use exactly these sections:

```markdown
# HPA-306 Sirius UI Revamp Closeout

## Final disposition
## Completed delivery chain
## HPA-359 evidence reused
## Post-HPA-359 delta
## Current-head automated validation
## Inventory production-window delta
## Inventory evidence
## Runtime bridge cleanup
## Defects and fixes
## HPA-306 definition-of-done mapping
## Linear closeout
```

Required factual content:

- required HPA-306 chain complete;
- optional HPA-375/HPA-541 complete;
- HPA-359 reused rather than replayed;
- exact build result;
- exact focused pass/fail/skip totals;
- exact full-suite pass/fail/skip totals;
- `1280×720` and `640×360` human usability/runtime-error observations;
- known compact heading chrome remains non-blocking when required content/controls are usable;
- the two evidence paths + exact dimensions;
- bridge cleanup: `.godot-mcp` absent and `project.godot` restored to its pre-session hash;
- any concrete regression/fix, or explicitly none;
- DoD mapping;
- one disposition: close HPA-306 or keep open with a named failing criterion.

Do not record screenshot hashes and do not duplicate the full HPA-359 evidence text.

- [ ] **Step 2: Scan the closeout record for unfinished result language**

```bash
rg -n "pending result|to be recorded|fill this|replace me" docs/ui/hpa-306/closeout.md
```

Expected: no matches.

- [ ] **Step 3: Refresh the remote base immediately before final scope verification**

```bash
git fetch origin main
```

Expected: `origin/main` reflects the current remote default-branch head.

- [ ] **Step 4: Verify final PR scope against `origin/main`**

```bash
git status --short
git diff --stat origin/main...HEAD
git diff --check origin/main...HEAD
```

Expected clean no-defect shape:

- `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`;
- `docs/superpowers/plans/2026-08-24-hpa-306-ui-revamp-closeout.md`;
- `docs/ui/hpa-306/closeout.md`;
- `docs/ui/hpa-306/evidence/inventory-1280x720.png`;
- `docs/ui/hpa-306/evidence/inventory-640x360.png`.

`project.godot` and `.godot-mcp` must not appear.

If a regression/fix was required, the only additional files are the focused owner test and the minimal existing production owner needed for that fix.

- [ ] **Step 5: Commit the closeout evidence to the existing PR branch**

```bash
git add \
  docs/ui/hpa-306/closeout.md \
  docs/ui/hpa-306/evidence/inventory-1280x720.png \
  docs/ui/hpa-306/evidence/inventory-640x360.png
# Add a focused regression/production owner only if Task 1 or Task 2 proved a defect.
git commit -m "docs: close out HPA-306 UI revamp"
```

Push to the existing HPA-306 branch. Do not create a second PR.

---

## Task 4: Finish Linear only after PR #48 is merged

**Files:**
- No repository change.
- Linear: HPA-306 only.

**Interfaces:**
- Consumes: merged PR #48 and its green `docs/ui/hpa-306/closeout.md`.
- Produces: final HPA-306 evidence comment and status `Done`.

- [ ] **Step 1: Confirm PR #48 is merged**

Do not mark Linear Done merely because validation is green or the PR is ready for review. The repository default branch must contain the closeout evidence first.

- [ ] **Step 2: Add the final Linear evidence summary**

Summarize only:

- HPA-359 walkthrough reused;
- focused HPA-375/HPA-541 result;
- full-suite result;
- two Inventory viewport results;
- runtime-bridge cleanup result;
- any regression/fix or explicitly none;
- merged PR #48 link.

- [ ] **Step 3: Mark HPA-306 Done**

Set HPA-306 to `Done` after Step 2. Do not change the Sirius Linear project state and do not create another ticket solely for umbrella closure.
