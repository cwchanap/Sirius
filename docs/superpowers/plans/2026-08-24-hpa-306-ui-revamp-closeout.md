# HPA-306 Sirius UI Revamp Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close HPA-306 on current `main` by reusing HPA-359's production evidence, validating the post-HPA-359 HPA-375/HPA-541 delta, and recording one final current-head disposition without adding another UI feature or framework.

**Architecture:** Keep the shipped scene-authored UI, Theme/components, root-local `UIScreenHost`, and current controller/domain ownership unchanged. HPA-359 remains authoritative for the expensive production walkthrough; HPA-306 adds fresh automated current-head coverage for the Inventory and Reduced Motion follow-ups plus one Inventory-only real-window delta check. A no-production-code closeout is expected; any real defect is reproduced in its nearest existing suite and fixed minimally in this same PR.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, existing Sirius Theme/components/`UIScreenHost`, Linear HPA-306.

**Spec:** `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`

## Global Constraints

- [ ] Keep planning, validation evidence, and any regression fix in this single HPA-306 PR.
- [ ] Do not add a new screen, host kind, UI service, presenter/view-model layer, navigation framework, second host, E2E driver, screenshot harness, or release framework.
- [ ] Reuse `docs/ui/hpa-359/release-validation.md`; do not repeat the full HPA-359 production walkthrough without a delta-specific failure.
- [ ] HPA-375 and HPA-541 are already implemented; this ticket validates their current-head integration rather than extending their product scope.
- [ ] A clean closeout with no production C# or `.tscn` change is valid and preferred.
- [ ] Any production fix starts with the smallest useful failing regression in an existing owner suite.
- [ ] Do not recalculate the historical April PRD completion percentage or broadly rewrite `docs/PRD.md`.
- [ ] Do not change the Sirius Linear project state; only HPA-306 is closed by this plan.
- [ ] Do not create new screenshots unless they are needed to explain a reproduced defect.

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
- Consumes: HPA-359 final evidence plus the merged HPA-375/HPA-541 production code on current `main`.
- Produces: a green current-head build, focused delta result, and full-suite result used by Task 3.

- [ ] **Step 1: Read the existing release evidence before running anything**

Read `docs/ui/hpa-359/release-validation.md` and treat its real Main Menu → Game, movement → Battle, Save/Prompt, Return-to-Title, hosted joypad, and legacy-path audit as existing evidence. Do not turn those sections back into manual acceptance work.

- [ ] **Step 2: Ensure local GdUnit settings exist**

```bash
test -f test.runsettings.local || cp test.runsettings.local.template test.runsettings.local
```

If the file was copied, configure only the local Godot executable path required by the existing template. `test.runsettings.local` remains local environment configuration and must not be committed.

- [ ] **Step 3: Build current head**

```bash
dotnet build Sirius.sln
```

Expected: exit 0 and no compile errors. Existing environment warnings such as NuGet vulnerability-feed unavailability must be classified rather than treated as product regressions.

- [ ] **Step 4: Run the focused HPA-375/HPA-541 integration set**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~SettingsDataTest|FullyQualifiedName~SettingsManagerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~GridMapTest|FullyQualifiedName~PlayerDisplayTest|FullyQualifiedName~EnemySpawnTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest"
```

Expected: all discovered tests pass. Record the exact pass/fail/skip totals for Task 3.

This focused set intentionally covers:

- HPA-375 Inventory selection/details/filter/sort/layout/focus behavior;
- HPA-541 Settings persistence/staging and authored scene;
- Reduced Motion world propagation and animator frame reset;
- real Game integration;
- hosted lifecycle regression coverage;
- Battle reduced-motion and scene visibility behavior.

- [ ] **Step 5: Run the full umbrella completion gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: the full suite passes. Record the exact pass/fail/skip totals for Task 3.

Do not replace this with only the focused set. HPA-306's definition of done spans the shared lifecycle and domain contracts across all migrated screens.

- [ ] **Step 6: Check the committed PR range for whitespace errors**

```bash
git diff --check main...HEAD
```

Expected: no whitespace errors.

If Steps 3–6 are green, proceed to Task 2 without editing production code.

---

## Task 2: Re-check only the Inventory real-window delta

**Files:**
- Reference: `scenes/ui/InventoryMenu.tscn`
- Reference: `scripts/ui/InventoryMenuController.cs`
- Owning tests if a defect is found: `tests/ui/InventoryMenuControllerTest.cs`, `tests/ui/InventoryMenuSceneTest.cs`
- Modify only if a reproduced defect requires it: the nearest existing Inventory owner above

**Interfaces:**
- Consumes: the real gameplay Inventory path and the HPA-375 behavior already protected by Task 1.
- Produces: one current-head real-window observation at `1280×720` and `640×360` for Task 3.

- [ ] **Step 1: Launch the production project**

```bash
godot --path .
```

Use the real project/game window. Do not create a new test host or screenshot driver.

- [ ] **Step 2: Check Inventory at `1280×720`**

From real gameplay, open Inventory and verify the HPA-375 standard layout only:

- Character / Items / Details composition is visually usable;
- selecting an item updates selection/details without immediately equipping, unequipping, or consuming it;
- Details content and the contextual action are reachable;
- filter and sort controls are visible and usable;
- Close remains reachable;
- no new runtime UI error is emitted.

Do not replay Battle, Save/Prompt, Dialogue/Shop/Heal, Puzzle, or Return-to-Title flows; HPA-375 did not change them and HPA-359 already owns that evidence.

- [ ] **Step 3: Check compact Inventory at `640×360`**

Resize the same real game window to `640×360` and verify:

- Equipment / Items / Skills / Details pages remain navigable;
- selection does not force an automatic jump to Details;
- Details can be opened explicitly and remains understandable/actionable for the selected item;
- compact navigation and Close remain reachable;
- no content is clipped into an unusable state;
- no new runtime UI error is emitted.

Restore the normal viewport after the observation.

- [ ] **Step 4: Do not create new screenshots for a clean pass**

The closeout note records the observation in text. Existing HPA-359 screenshots remain the durable evidence for the wider release walkthrough. Capture an image only if it materially helps diagnose a reproduced Inventory defect.

- [ ] **Step 5: If the real-window check exposes a defect, reproduce it RED in the existing Inventory suite**

Choose the smallest owner:

- layout/breakpoint/focus geometry → `InventoryMenuSceneTest`;
- selection/action/filter/sort/state behavior → `InventoryMenuControllerTest`.

Add one regression that reproduces the observed defect and run only that test/class first:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuSceneTest"
```

or:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected before production edit: the new regression fails for the intended reason.

- [ ] **Step 6: If Step 5 is RED, make the minimal GREEN fix and revalidate**

Change only the existing Inventory owner required by the regression. Do not add a general UI abstraction for a closeout defect.

Run the owning class until green, then rerun Task 1 Steps 4–6 and repeat only the affected `1280×720` or `640×360` Inventory observation.

If Task 2 is clean, make no production edit.

---

## Task 3: Write the durable HPA-306 closeout record

**Files:**
- Create: `docs/ui/hpa-306/closeout.md`
- Reference: `docs/ui/hpa-359/release-validation.md`
- Reference: `docs/superpowers/specs/2026-08-24-hpa-306-ui-revamp-closeout-design.md`
- Reference: `docs/superpowers/plans/2026-08-24-hpa-306-ui-revamp-closeout.md`
- Include only if Task 2 found a defect: the focused regression and minimal production fix files

**Interfaces:**
- Consumes: exact Task 1 command results, Task 2 real-window observations, HPA-359 evidence, and Linear completion state for HPA-375/HPA-541.
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

## Inventory real-window delta check

## Defects and fixes

## HPA-306 definition-of-done mapping

## Linear closeout
```

Required factual content:

- state that the required HPA-306 migration chain is complete;
- state that optional HPA-375 and HPA-541 are complete;
- link/refer to `docs/ui/hpa-359/release-validation.md` for the wider production walkthrough rather than reproducing it;
- record the exact build result;
- record the exact focused test pass/fail/skip totals;
- record the exact full-suite pass/fail/skip totals;
- record the `1280×720` and `640×360` Inventory observations;
- state whether any defect was found and, if so, name the regression and minimal fix;
- map the final evidence back to HPA-306's definition of done;
- give one unambiguous disposition: close HPA-306, or keep it open because a named acceptance criterion is still failing.

Do not restate the entire HPA-359 evidence document and do not add a new screenshot matrix.

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

- the HPA-306 design spec;
- the HPA-306 implementation plan;
- `docs/ui/hpa-306/closeout.md`.

If a defect was found, the only extra files are its focused regression and the minimal existing production owner required to fix it.

- [ ] **Step 4: Commit the closeout evidence**

```bash
git add docs/ui/hpa-306/closeout.md
# If Task 2 produced a regression/fix, add only those exact owning files as well.
git commit -m "docs: close out HPA-306 UI revamp"
```

Do not create a second PR. Push this commit to the existing HPA-306 branch/draft PR.

---

## Task 4: Finish the Linear umbrella after the PR evidence is green

**Files:**
- No repository file change required.
- Linear: HPA-306 only.

**Interfaces:**
- Consumes: the green HPA-306 closeout record and the single HPA-306 PR.
- Produces: HPA-306 status `Done` with a concise evidence summary.

- [ ] **Step 1: Add one final HPA-306 Linear comment**

Summarize:

- HPA-359 production walkthrough reused;
- HPA-375/HPA-541 current-head focused validation result;
- full-suite result;
- Inventory `1280×720` / `640×360` delta result;
- any regression/fix, or explicitly that no production fix was needed;
- the HPA-306 PR link.

Keep the comment evidence-focused; do not copy the full repository closeout document into Linear.

- [ ] **Step 2: Mark HPA-306 Done only when the PR closeout is green**

Set Linear HPA-306 to `Done` after the closeout evidence is complete and the single PR is ready for/has completed merge according to the normal repository workflow.

Do not mark the Sirius Linear project itself complete and do not create another ticket simply to represent umbrella closure.
