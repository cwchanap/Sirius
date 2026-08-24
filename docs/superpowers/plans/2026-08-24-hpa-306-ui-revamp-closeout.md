# HPA-306 Sirius UI Revamp Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close HPA-306 on current `main` by reusing HPA-359's production evidence, validating the post-HPA-359 HPA-375/HPA-541 delta, and recording one final current-head disposition without adding another UI feature or framework.

**Architecture:** Keep the shipped scene-authored UI, Theme/components, root-local `UIScreenHost`, and current controller/domain ownership unchanged. HPA-359 remains authoritative for the expensive production walkthrough. HPA-306 adds fresh automated current-head coverage for the Inventory and Reduced Motion follow-ups plus one Inventory-only real-window **composition** check and two current evidence captures. A no-production-code closeout is expected; any real defect is reproduced in its nearest existing suite and fixed minimally in this same PR.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, existing Sirius Theme/components/`UIScreenHost`, Linear HPA-306.

**Spec:** `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`

## Global Constraints

- [ ] Keep planning, validation evidence, and any regression fix in this single HPA-306 PR.
- [ ] Do not add a new screen, host kind, UI service, presenter/view-model layer, navigation framework, second host, E2E driver, screenshot harness, or release framework.
- [ ] Reuse `docs/ui/hpa-359/release-validation.md`; do not repeat the full HPA-359 production walkthrough without a delta-specific failure.
- [ ] HPA-375 and HPA-541 are already implemented; validate their current-head integration rather than extending product scope.
- [ ] A clean closeout with no production C# or `.tscn` change is valid and preferred.
- [ ] Any production fix starts with the smallest useful failing regression in an existing owner suite.
- [ ] The real-window Inventory pass is composition-only; automated tests remain the behavior/focus oracle.
- [ ] Inherit HPA-359's compact Inventory disposition: tight/clipped section-heading chrome at `640×360` is not blocking unless required content or controls become unreachable/unusable.
- [ ] Create exactly two fresh HPA-306 Inventory evidence images; do not overwrite HPA-359's hash-pinned historical captures and do not recapture unrelated screens.
- [ ] Do not recalculate the historical April PRD completion percentage or broadly rewrite `docs/PRD.md`.
- [ ] Do not change the Sirius Linear project state; only HPA-306 is closed by this plan.

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
- Consumes: HPA-359 final evidence plus merged HPA-375/HPA-541 production code on current `main`.
- Produces: current-head build, focused delta result, and full-suite result used by Task 3.

- [ ] **Step 1: Read the existing release evidence before running anything**

Read `docs/ui/hpa-359/release-validation.md`. Reuse its Main Menu → Game, movement → Battle, Save/Prompt, Return-to-Title, hosted joypad, legacy-path, and default-motion production evidence.

Also carry forward its compact Inventory disposition: `640×360` remained usable even with tight/clipped section-heading chrome. Do not reinterpret that known visual concern as a new HPA-306 defect unless required content or controls become unreachable/unusable.

- [ ] **Step 2: Ensure local GdUnit settings exist**

```bash
test -f test.runsettings.local || cp test.runsettings.local.template test.runsettings.local
```

If copied, configure only the local Godot executable path required by the existing template. `test.runsettings.local` is local environment configuration and must not be committed.

- [ ] **Step 3: Build current head**

```bash
dotnet build Sirius.sln
```

Expected: exit 0 and no compile errors. Classify existing environment warnings such as NuGet vulnerability-feed unavailability rather than treating them as product regressions.

- [ ] **Step 4: Run the focused HPA-375/HPA-541 integration set**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: all discovered tests pass. Record exact pass/fail/skip totals for Task 3.

This focused set owns:

- HPA-375 selection/details/filter/sort/layout/focus behavior;
- Settings persistence/staging and authored scene;
- Reduced Motion world propagation and animator frame reset;
- real Game integration;
- hosted lifecycle regression coverage;
- Battle reduced-motion and scene visibility behavior.

- [ ] **Step 5: Run the full umbrella completion gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: full suite passes. Record exact pass/fail/skip totals for Task 3.

Do not replace this with only the focused set. HPA-306's definition of done spans shared lifecycle/domain contracts across all migrated screens.

- [ ] **Step 6: Check the committed PR range for whitespace errors**

```bash
git diff --check main...HEAD
```

Expected: no whitespace errors.

- [ ] **Step 7: If automated validation is RED, keep defect handling inside the existing owner**

If a focused/full-suite test already fails specifically for the regression, that failing test is the RED gate. If the failure is too broad to diagnose safely, add/tighten the smallest useful regression in the nearest existing Inventory/Settings/world/Game/Battle/host suite first.

Only after a specific RED exists:

1. make the smallest production fix in the existing owner;
2. rerun that owner suite;
3. rerun Steps 4–6;
4. run Task 2 only when the changed production files can affect Inventory composition.

Do not add a reusable abstraction for a one-off closeout regression.

If Steps 3–6 are green, proceed to Task 2 without editing production code.

---

## Task 2: Re-check only the Inventory real-window composition delta

**Files:**
- Reference: `scenes/ui/InventoryMenu.tscn`
- Reference: `scripts/ui/InventoryMenuController.cs`
- Owning visual regression suite: `tests/ui/InventoryMenuSceneTest.cs`
- Create: `docs/ui/hpa-306/evidence/inventory-1280x720.png`
- Create: `docs/ui/hpa-306/evidence/inventory-640x360.png`
- Modify only if a reproduced visual defect requires it: the nearest existing Inventory owner

**Interfaces:**
- Consumes: real gameplay Inventory path and HPA-375 behavior already protected by Task 1.
- Produces: current-head production-window composition observations plus exactly two auditable images for Task 3.

- [ ] **Step 1: Launch the production project**

```bash
godot --path .
```

Use the real project/game window. Do not create a new test host, driver, or screenshot harness.

- [ ] **Step 2: Check Inventory composition at `1280×720`**

From real gameplay, open Inventory and inspect composition only.

Pass when:

- standard Character / Items / Details regions are visually usable;
- required content and item art are visible;
- Details presentation, filter/sort controls, and Close are visible/reachable;
- no severe overlap, zero-width region, or off-window placement makes required content unusable;
- no new runtime UI error is emitted.

Do **not** manually re-prove non-mutating selection, equip/use behavior, filter/sort semantics, compact page policy, focus movement, or Close behavior. `InventoryMenuControllerTest` / `InventoryMenuSceneTest` already own those contracts.

Use normal interaction only as needed to expose representative populated Details composition; treat behavior correctness as Task 1 evidence, not a second manual oracle.

- [ ] **Step 3: Check compact Inventory composition at `640×360`**

Resize the same real game window to `640×360` and inspect composition only.

Pass when:

- required Inventory content and item art remain visible;
- compact tabs/pages, Details presentation, filter/sort controls where shown, and Close remain visible/reachable;
- required content/controls are not clipped into an unusable state;
- no new runtime UI error is emitted.

**Known accepted condition:** section-heading chrome may remain tight/clipped, matching HPA-359. Do not open product work for that alone. It is blocking only if required content or controls become unreachable/unusable.

Restore the normal viewport after the observation.

- [ ] **Step 4: Capture exactly the two current Inventory evidence images**

Create the directory if needed:

```bash
mkdir -p docs/ui/hpa-306/evidence
```

Save the clean production-window captures as:

```text
docs/ui/hpa-306/evidence/inventory-1280x720.png
docs/ui/hpa-306/evidence/inventory-640x360.png
```

Do not overwrite `docs/ui/hpa-359/evidence/inventory-*.png`; HPA-359's release record pins hashes for those historical files.

Do not capture Battle, Save/Prompt, Main Menu, or other screens.

- [ ] **Step 5: Verify image dimensions and calculate SHA-256**

Verify PNG dimensions without adding a dependency:

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
    size = struct.unpack(">II", data[16:24])
    assert size == wanted, f"{path}: expected {wanted}, got {size}"
    print(f"{path}: {size[0]}x{size[1]}")
PY
```

Then calculate hashes:

```bash
shasum -a 256 \
  docs/ui/hpa-306/evidence/inventory-1280x720.png \
  docs/ui/hpa-306/evidence/inventory-640x360.png
```

Record both dimensions and hashes in Task 3.

- [ ] **Step 6: If the window exposes a real composition defect, reproduce it RED in `InventoryMenuSceneTest`**

Do not treat the known heading-chrome clip as a defect unless required content/controls become unusable.

For a new blocking composition issue, add the smallest deterministic regression to `tests/ui/InventoryMenuSceneTest.cs` and run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuSceneTest"
```

Expected before production edit: the new regression fails for the intended layout reason.

- [ ] **Step 7: If Step 6 is RED, make the minimal GREEN fix and revalidate**

Change only the existing Inventory owner required by the regression. Do not add a general UI abstraction.

Run the owning scene suite until green, then rerun Task 1 Steps 4–6, repeat only the affected viewport observation, and refresh the affected HPA-306 image/hash.

If Task 2 is clean, make no production edit.

---

## Task 3: Write the durable HPA-306 closeout record

**Files:**
- Create: `docs/ui/hpa-306/closeout.md`
- Include: `docs/ui/hpa-306/evidence/inventory-1280x720.png`
- Include: `docs/ui/hpa-306/evidence/inventory-640x360.png`
- Reference: `docs/ui/hpa-359/release-validation.md`
- Reference: `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`
- Reference: `docs/superpowers/plans/2026-08-24-hpa-306-ui-revamp-closeout.md`
- Include only if validation found a defect: its focused regression and minimal production fix files

**Interfaces:**
- Consumes: exact Task 1 command results, Task 2 real-window composition observations/images, HPA-359 evidence, and Linear completion state for HPA-375/HPA-541.
- Produces: one concise repository record proving whether HPA-306 can close on current head.

- [ ] **Step 1: Create `docs/ui/hpa-306/closeout.md` from observed facts**

Write the file only after Tasks 1–2 are complete. It must contain these sections with actual observed values:

```markdown
# HPA-306 Sirius UI Revamp Closeout

## Final disposition

## Completed delivery chain

## HPA-359 evidence reused

## Post-HPA-359 delta

## Current-head automated validation

## Inventory real-window composition delta

## Inventory evidence

## Defects and fixes

## HPA-306 definition-of-done mapping

## Linear closeout
```

Required factual content:

- required HPA-306 migration chain is complete;
- optional HPA-375 and HPA-541 are complete;
- refer to `docs/ui/hpa-359/release-validation.md` for the wider production walkthrough rather than reproducing it;
- exact build result;
- exact focused test pass/fail/skip totals;
- exact full-suite pass/fail/skip totals;
- `1280×720` and `640×360` Inventory composition observations;
- explicitly state that known compact heading-chrome clipping remains non-blocking when content/controls stay usable;
- list the two HPA-306 Inventory evidence paths, dimensions, and SHA-256 values;
- state whether any defect was found and, if so, name the regression and minimal fix;
- map final evidence back to HPA-306's definition of done;
- give one unambiguous disposition: close HPA-306, or keep it open because a named acceptance criterion is still failing.

Do not restate the entire HPA-359 evidence document and do not add a broader screenshot matrix.

- [ ] **Step 2: Check the closeout document for unfinished result language**

```bash
rg -n "pending result|to be recorded|fill this|replace me" docs/ui/hpa-306/closeout.md
```

Expected: no matches.

- [ ] **Step 3: Check the final PR scope**

```bash
git status --short
git diff --stat main...HEAD
git diff --check main...HEAD
```

Expected clean shape when no defect was found:

- HPA-306 design spec;
- HPA-306 implementation plan;
- `docs/ui/hpa-306/closeout.md`;
- `docs/ui/hpa-306/evidence/inventory-1280x720.png`;
- `docs/ui/hpa-306/evidence/inventory-640x360.png`.

If a defect was found, the only extra files are its focused regression and the minimal existing production owner required to fix it.

- [ ] **Step 4: Commit the closeout evidence**

```bash
git add \
  docs/ui/hpa-306/closeout.md \
  docs/ui/hpa-306/evidence/inventory-1280x720.png \
  docs/ui/hpa-306/evidence/inventory-640x360.png
# If validation produced a regression/fix, add only those exact owning files as well.
git commit -m "docs: close out HPA-306 UI revamp"
```

Do not create a second PR. Push this commit to the existing HPA-306 branch/draft PR.

---

## Task 4: Finish the Linear umbrella after the PR evidence is green

**Files:**
- No repository file change required.
- Linear: HPA-306 only.

**Interfaces:**
- Consumes: green HPA-306 closeout record and the single HPA-306 PR.
- Produces: HPA-306 status `Done` with a concise evidence summary.

- [ ] **Step 1: Add one final HPA-306 Linear comment**

Summarize:

- HPA-359 production walkthrough reused;
- HPA-375/HPA-541 current-head focused validation result;
- full-suite result;
- Inventory `1280×720` / `640×360` composition result;
- two HPA-306 Inventory evidence paths/hashes;
- any regression/fix, or explicitly that no production fix was needed;
- HPA-306 PR link.

Keep the comment evidence-focused; do not copy the full repository closeout document into Linear.

- [ ] **Step 2: Mark HPA-306 Done only when the PR closeout is green**

Set Linear HPA-306 to `Done` after the closeout evidence is complete and the single PR is ready for/has completed merge according to the normal repository workflow.

Do not mark the Sirius Linear project itself complete and do not create another ticket simply to represent umbrella closure.
