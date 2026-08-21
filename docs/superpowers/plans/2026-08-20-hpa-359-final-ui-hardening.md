# HPA-359 Final Sirius UI Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the final Sirius UI release smoke, fix only concrete cross-screen defects found, remove proven obsolete UI leftovers, and record concise release evidence.

**Architecture:** Keep the shipped root-local `UIScreenHost`, scene-authored screens, shared Theme/components, and existing controller ownership unchanged. Reuse existing GdUnit scene/lifecycle suites for deterministic protection; use exactly three manual runtime journeys for cross-screen integration instead of adding a new E2E or screenshot framework. Any defect is handled test-first in its current owning suite and fixed minimally in the same HPA-359 PR.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, existing Sirius Theme/components/`UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-20-hpa-359-final-ui-hardening-design.md`

## Global Constraints

- [ ] Keep all HPA-359 implementation and evidence in one PR.
- [ ] Do not implement HPA-375 or HPA-541.
- [ ] Do not add a global UI singleton, new host layer, E2E harness, screenshot harness, compatibility layer, or generic release framework.
- [ ] Do not change gameplay/domain rules while hardening presentation.
- [ ] Do not manufacture production changes when the shipped path is already correct.
- [ ] Any production defect fix must start with the narrowest failing regression test in the owning existing suite.
- [ ] Dynamic runtime UI that is still intentional (for example dialogue choices or inventory catalogue entries) is not legacy merely because it is created in C#.
- [ ] Do not rewrite the historical HPA-376 contract or the full PRD as part of this ticket.
- [ ] Keep the final evidence set to at most six screenshots.

## Task 1: Establish the release baseline and evidence record

**Files:**
- Create: `docs/ui/hpa-359/release-validation.md`
- Reference: `scripts/ui/theme/SiriusUiMetrics.cs`

- [ ] **Step 1: Confirm the project builds before hardening**

Run:

```bash
dotnet build Sirius.sln
```

Expected: build succeeds with no new compile errors.

- [ ] **Step 2: Run the focused cross-screen baseline suites**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: green. If a pre-existing failure appears, record it before changing production code and determine whether it is actually in HPA-359 scope.

- [ ] **Step 3: Create the evidence document with fixed scope**

Create `docs/ui/hpa-359/release-validation.md` with this structure:

```markdown
# HPA-359 Sirius UI Release Validation

## Automated baseline
- Build: pending
- Focused UI/game lifecycle suite: pending
- Full suite: pending

## Journey 1 — Launch → Exploration → Inventory → Exploration
- 1280×720: pending
- 640×360 Inventory: pending
- Keyboard/mouse: pending

## Journey 2 — Interaction → Battle → Result → Exploration
- 1280×720: pending
- 640×360 long Dialogue/Message: pending
- Reward/result single presentation: pending

## Journey 3 — Pause → child → Prompt → Pause → Return to Title
- 1280×720: pending
- 640×360 nested Save/Load Prompt: pending
- Corrupted/unavailable save path when naturally reachable: pending

## Gamepad smoke
- Hosted open → navigate → Cancel/close: pending

## Runtime observations
- Warnings/errors: pending
- Duplicate activations: pending
- Stuck focus/pause/input/cursor/HUD state: pending

## Legacy-path audit
- Production executable leftovers: pending
- Developer-doc drift: pending

## Evidence screenshots
- pending
```

Replace `pending` values only with observed results. Do not pre-declare success.

- [ ] **Step 4: Record baseline results**

Write the exact commands, pass/fail counts, and any relevant runtime/test warning notes into the evidence document.

- [ ] **Step 5: Commit the baseline/evidence scaffold**

```bash
git add docs/ui/hpa-359/release-validation.md
git commit -m "docs: start HPA-359 release validation"
```

## Task 2: Validate Journey 1 — launch, exploration, inventory, return

**Primary production owners:**
- `scripts/ui/MainMenu.cs`
- `scenes/ui/MainMenu.tscn`
- `scripts/game/Game.cs`
- `scripts/ui/ExplorationHudController.cs`
- `scenes/ui/ExplorationHud.tscn`
- `scripts/ui/InventoryMenuController.cs`
- `scenes/ui/InventoryMenu.tscn`

**Primary tests if a defect is found:**
- `tests/ui/MainMenuTest.cs`
- `tests/ui/MainMenuSceneTest.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/ui/ExplorationHudControllerTest.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

- [ ] **Step 1: Run the existing Journey 1 protection before manual smoke**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: green.

- [ ] **Step 2: Run the production journey at `1280×720`**

From the real Main Menu scene:

1. Use Continue when a valid save exists; otherwise use New Game.
2. Enter exploration and verify the compact production HUD rather than any debug panel.
3. Open Inventory through the configured action.
4. Navigate at least one equipment/inventory focus target.
5. Close Inventory using configured Cancel or the inventory toggle.
6. Continue moving/interacting in exploration.

Verify all of these while running:

- Inventory opens once.
- Gameplay input does not leak through while Inventory is topmost.
- HUD visibility follows the host policy while Inventory is active and restores after close.
- Cursor state restores after close.
- Tree pause state is unchanged by direct Inventory.
- Focus does not remain on a freed/hidden Inventory control.
- No warning/error or missing-resource fallback is emitted.

- [ ] **Step 3: Repeat only Inventory layout at `640×360`**

Verify the visible catalogue, equipment area, action/focus target, and any summary remain enclosed and usable. Do not repeat unrelated Main Menu/exploration steps solely to create a matrix.

- [ ] **Step 4: If a Journey 1 defect appears, write the focused RED test**

Choose the nearest owner from the file map above. Examples of ownership:

- host/focus/pause/input leak → `GameplayPauseHostTest.cs` or `GameInputLifecycleTest.cs`;
- Main Menu handoff/focus → `MainMenuTest.cs` / `MainMenuSceneTest.cs`;
- Inventory layout/focus → `InventoryMenuControllerTest.cs` / `InventoryMenuSceneTest.cs`;
- HUD restore → `ExplorationHudControllerTest.cs` or the gameplay lifecycle suite.

Run only the chosen test class and confirm it fails for the observed defect before editing production code.

- [ ] **Step 5: Apply the minimal GREEN fix only if Step 4 produced RED**

Edit only the owning production file(s). Preserve current screen/host/domain ownership; do not introduce a new abstraction for a one-off final-pass defect.

Re-run the focused failing class until green, then rerun the Journey 1 protection command from Step 1.

- [ ] **Step 6: Capture two screenshots and record observed results**

Add:

- `docs/ui/hpa-359/evidence/journey-1-inventory-1280x720.png`
- `docs/ui/hpa-359/evidence/journey-1-inventory-640x360.png`

Update the Journey 1 and runtime-observation sections of `release-validation.md`.

- [ ] **Step 7: Commit Journey 1 evidence/fixes**

```bash
git add docs/ui/hpa-359 tests scripts scenes
git commit -m "test: validate Sirius launch inventory journey"
```

If no production/test files changed, keep the commit limited to evidence.

## Task 3: Validate Journey 2 — interaction, battle, result, return

**Primary production owners:**
- `scripts/ui/NpcInteractionController.cs`
- `scripts/ui/DialogueScreenController.cs`
- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/PuzzleRiddleScreenController.cs`
- `scenes/ui/PuzzleRiddleScreen.tscn`
- `scripts/ui/BattleManager.cs`
- `scenes/ui/BattleScene.tscn`
- `scripts/game/Game.cs`

**Primary tests if a defect is found:**
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/ui/DialogueScreenControllerTest.cs`
- `tests/ui/PuzzleRiddleScreenControllerTest.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/ui/BattleSceneTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameTest.cs`

- [ ] **Step 1: Run the existing interaction/battle suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest"
```

Expected: green.

- [ ] **Step 2: Run a real interaction → battle → exploration journey at `1280×720`**

Use one naturally reachable NPC or Puzzle/Riddle interaction, then complete a battle through result/reward and return to exploration.

Verify:

- the interaction surface is the sole topmost receiver while active;
- configured Cancel does not also open Pause or trigger gameplay;
- transitioning away does not leave an old interaction view/focus owner alive;
- Battle owns input/focus while active;
- result/reward feedback is presented once;
- exploration HUD/prompt/input/cursor state returns afterward;
- no duplicate battle/result/reward activation occurs.

- [ ] **Step 3: Exercise long text at `640×360`**

Use the existing Dialogue screen or shared Prompt with a deliberately long, representative string through its normal presentation API. Verify text wraps/scrolls as designed, primary choices remain reachable, and Cancel/action hints remain usable.

Do not add localization infrastructure or a synthetic long-text framework.

- [ ] **Step 4: If a defect appears, write the focused RED test**

Use the nearest existing owner. Prefer extending an existing test class over creating a new cross-screen harness.

Confirm RED with a class-filtered `dotnet test` invocation.

- [ ] **Step 5: Apply the minimal GREEN fix only for reproduced defects**

Preserve battle/domain resolution and reward-grant ownership. UI code must not start granting rewards or changing puzzle/NPC domain rules.

Re-run the focused class plus the Step 1 suite.

- [ ] **Step 6: Capture two screenshots and record evidence**

Add:

- `docs/ui/hpa-359/evidence/journey-2-dialogue-long-640x360.png`
- `docs/ui/hpa-359/evidence/journey-2-battle-result-1280x720.png`

Update Journey 2 and runtime observations in `release-validation.md`.

- [ ] **Step 7: Commit Journey 2 evidence/fixes**

```bash
git add docs/ui/hpa-359 tests scripts scenes
git commit -m "test: validate Sirius interaction battle journey"
```

## Task 4: Validate Journey 3 — Pause children, nested prompt, Return to Title

**Primary production owners:**
- `scripts/game/Game.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/PauseScreenController.cs`
- `scenes/ui/PauseScreen.tscn`
- `scripts/ui/SaveLoadScreenController.cs`
- `scenes/ui/SaveLoadScreen.tscn`
- `scripts/ui/SettingsMenuController.cs`
- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/components/SiriusPrompt.cs`

**Primary tests if a defect is found:**
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/ui/PauseScreenControllerTest.cs`
- `tests/ui/SaveLoadScreenControllerTest.cs`
- `tests/ui/SaveLoadScreenSceneTest.cs`
- `tests/ui/SettingsMenuControllerTest.cs`
- existing host tests under `tests/ui/hosting/`
- existing prompt tests under `tests/ui/components/`

- [ ] **Step 1: Run existing Pause/host/child suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt"
```

Expected: green.

- [ ] **Step 2: Run the real nested journey at `1280×720`**

1. Open Pause from exploration.
2. Open Save/Load or Settings as a child.
3. Trigger one natural nested confirmation or recoverable error.
4. Close the topmost prompt.
5. Close the child and verify focus returns to the same Pause instance.
6. Reopen a different child to prove the stack remains usable.
7. Return to Title through the production Pause flow.

Verify:

- exactly one Pause tree-pause lease;
- child screens remain interactive while the tree is paused without acquiring another lease;
- Cancel affects only the topmost eligible entry;
- the parent child remains active under its nested prompt;
- focus restoration is to the still-live parent, not a stale/freed control;
- teardown closes the stack before scene replacement;
- cursor/HUD/input state does not leak into Main Menu.

- [ ] **Step 3: Re-run the nested Save/Load Prompt at `640×360`**

Use overwrite confirmation or a naturally reachable recoverable error. If corrupted/unavailable save handling is naturally reachable without fabricating a new persistence format, exercise it here.

Verify the prompt and underlying Save/Load surface remain readable and action targets satisfy the existing compact layout policy.

- [ ] **Step 4: Perform one gamepad smoke if support is still enabled**

From a hosted screen:

1. Open it using the supported controller path.
2. Move focus at least once.
3. Activate or Cancel/close.
4. Verify focus/input returns to the expected parent/gameplay state.

This is one smoke, not a controller matrix.

- [ ] **Step 5: If a defect appears, write focused RED before production changes**

Use `GameplayPauseHostTest` for stack/lease/focus defects, `GameInputLifecycleTest` for topmost input/cancel defects, or the direct screen/controller suite for local behavior.

- [ ] **Step 6: Apply the minimal GREEN fix and re-run affected coverage**

Do not add another prompt or host abstraction. Reuse `UIScreenHost`, `SiriusModalShell`, and `SiriusPrompt` as already shipped.

- [ ] **Step 7: Capture two screenshots and record evidence**

Add:

- `docs/ui/hpa-359/evidence/journey-3-save-prompt-640x360.png`
- `docs/ui/hpa-359/evidence/journey-3-pause-1280x720.png`

Update Journey 3, gamepad, and runtime-observation sections in `release-validation.md`.

- [ ] **Step 8: Commit Journey 3 evidence/fixes**

```bash
git add docs/ui/hpa-359 tests scripts scenes
git commit -m "test: validate Sirius pause child journey"
```

## Task 5: Audit legacy production paths and correct current developer documentation

**Files:**
- Modify: `CLAUDE.md`
- Audit: `scripts/`
- Audit: `scenes/`
- Audit: `project.godot`
- Update only if an executable leftover is proven: the exact owning legacy source/scene and nearest existing test

- [ ] **Step 1: Search for retired/debug path indicators**

Run:

```bash
rg -n 'DialogueDialog|SaveLoadDialog|SaveOverwriteConfirmationController|DraggablePanel|Player HUD|Settings menu coming soon' scripts scenes project.godot CLAUDE.md
rg -n 'AcceptDialog|ConfirmationDialog' scripts scenes project.godot
```

Classify matches before editing them:

- executable native-dialog/debug path → candidate for removal;
- historical comment saying a new screen replaces an old path → allowed unless misleading;
- engine input compatibility code still required by current behavior → keep;
- developer documentation naming a deleted current type → update.

At planning time, the known guaranteed drift is in `CLAUDE.md`; do not invent a source deletion merely to satisfy this task.

- [ ] **Step 2: Correct the known `CLAUDE.md` architecture drift**

Make these narrow updates:

- describe Battle as the scene-authored full-screen battle flow instead of a modal dialog;
- describe scene flow using the hosted/screen presentation model rather than “Battle dialogs”;
- update the `scripts/ui` examples to current controllers such as `DialogueScreenController`, `SaveLoadScreenController`, `PauseScreenController`, and `UIScreenHost` ownership; remove `DialogueDialog.cs` and `SaveOverwriteConfirmationController.cs` as current files;
- update the Save System paragraph so overwrite/recoverable feedback is owned by the shared hosted prompt path rather than the deleted confirmation controller;
- remove any other statement encountered in the same edited paragraphs that falsely describes a deleted UI path.

Do not modernize unrelated PRD/gameplay prose in this ticket.

- [ ] **Step 3: If the audit finds executable obsolete production code, protect its replacement first**

Before deletion, run or add the nearest existing test proving the migrated owner handles the production path. Only then remove the dead source/scene/reference and rerun that focused suite.

- [ ] **Step 4: Repeat the audit**

Re-run the `rg` commands. Expected: no unclassified executable legacy path remains. Historical “former X” comments may remain when useful.

- [ ] **Step 5: Record the audit result**

In `docs/ui/hpa-359/release-validation.md`, list:

- the commands used;
- executable leftovers removed, if any;
- intentionally retained historical/comment/input-compatibility matches;
- the `CLAUDE.md` current-architecture correction.

- [ ] **Step 6: Commit cleanup/documentation**

```bash
git add CLAUDE.md docs/ui/hpa-359 scripts scenes project.godot tests
git commit -m "docs: align Sirius UI development guidance"
```

## Task 6: Final verification and closeout evidence

**Files:**
- Modify: `docs/ui/hpa-359/release-validation.md`
- Verify: all files changed by HPA-359

- [ ] **Step 1: Re-run the focused cross-screen suite**

Use the Task 1 focused command. Expected: green.

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: all tests pass. Record exact passed/failed/skipped totals.

Do not fail HPA-359 merely because historical test infrastructure emits a pre-existing non-runtime warning; investigate and record it. New warnings/errors caused by HPA-359 or warnings observed in the normal production journeys must be resolved.

- [ ] **Step 3: Re-run all three production journeys after the final code state**

Do one final `1280×720` pass of each journey. Do not recapture screenshots unless the visible state changed after a fix.

Expected:

- no duplicate activation;
- no stuck focus, pause, input, cursor, or HUD policy;
- no normal-flow warning/error or missing-resource fallback;
- Return to Title completes cleanly.

- [ ] **Step 4: Finish `release-validation.md`**

Replace all remaining `pending` values with actual observations. List the six-or-fewer screenshot paths and any defects fixed, with the focused test that protects each fix.

- [ ] **Step 5: Review the complete diff for scope**

```bash
git diff --check
git status --short
git diff --stat main...HEAD
git diff main...HEAD -- docs/superpowers/specs/2026-08-20-hpa-359-final-ui-hardening-design.md docs/superpowers/plans/2026-08-20-hpa-359-final-ui-hardening.md docs/ui/hpa-359 CLAUDE.md scripts scenes tests
```

Confirm:

- one HPA-359 PR only;
- no HPA-375/HPA-541 work;
- no new generic architecture;
- every production change corresponds to a reproduced HPA-359 defect or proven dead path;
- evidence is factual and matches the final code state.

- [ ] **Step 6: Commit final evidence**

```bash
git add docs/ui/hpa-359
git commit -m "docs: record HPA-359 release validation"
```

If the evidence document was already complete in the previous commit, do not create an empty commit.

## Expected final change shape

The minimum successful HPA-359 implementation may legitimately contain **no production C# change** if all shipped UI journeys are already correct. In that case the ticket still delivers value through:

- focused and full-suite verification;
- runtime journey evidence;
- six-or-fewer representative screenshots;
- the legacy-path audit;
- current `CLAUDE.md` architecture corrections.

If defects are discovered, keep their regression tests and minimal fixes inside this same PR and record them in the release evidence. Do not split follow-up PRs for defects that are directly required to satisfy HPA-359.
