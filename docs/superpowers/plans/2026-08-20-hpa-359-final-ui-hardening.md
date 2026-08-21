# HPA-359 Final Sirius UI Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the final Sirius UI release smoke using three named production journeys, fix only concrete cross-screen defects found, remove proven obsolete UI leftovers, and record concise release evidence.

**Architecture:** Keep the shipped root-local `UIScreenHost`, scene-authored screens, shared Theme/components, and existing controller ownership unchanged. Reuse existing GdUnit scene/lifecycle suites for deterministic protection; use exactly three manual production journeys for composition seams instead of adding a new E2E or screenshot framework. Any defect is handled test-first in its current owning suite and fixed minimally in the same HPA-359 PR.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, existing Sirius Theme/components/`UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-20-hpa-359-final-ui-hardening-design.md`

## Global Constraints

- [ ] Keep all HPA-359 implementation and evidence in one PR.
- [ ] Do not implement HPA-375 or HPA-541.
- [ ] Do not add a global UI singleton, new host layer, E2E harness, screenshot harness, compatibility layer, or generic release framework.
- [ ] Do not add gameplay joypad bindings in this ticket.
- [ ] Do not change gameplay/domain rules while hardening presentation.
- [ ] Do not manufacture production changes when the shipped path is already correct.
- [ ] Any production defect fix must start with the narrowest failing regression test in the existing owning suite.
- [ ] Dynamic runtime UI that is still intentional (dialogue choices, Shop/Inventory catalogue rows) is not legacy merely because it is created in C#.
- [ ] Do not rewrite the HPA-376 flow matrix, historical evidence, or the April PRD feature requirements/body.
- [ ] Correct only current-status documentation that falsely describes deleted/superseded UI architecture.
- [ ] Keep the final evidence set to at most six screenshots.
- [ ] A clean closeout with no production C# changes is valid.

---

## Task 1: Establish the automated baseline and release record

**Files:**
- Create: `docs/ui/hpa-359/release-validation.md`
- Reference: `scripts/ui/theme/SiriusUiMetrics.cs`
- Reference: `scripts/data/floors/Floor0Layout.cs`
- Reference: `scenes/game/floors/FloorGF.tscn`

**Interfaces:**
- Consumes: existing GdUnit suites and the committed FloorGF route.
- Produces: a factual release-validation record that later tasks update; no production API.

- [ ] **Step 1: Confirm the project builds before hardening**

Run:

```bash
dotnet build Sirius.sln
```

Expected: build succeeds with no new compile errors.

- [ ] **Step 2: Run the focused cross-screen baseline suites**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt"
```

Expected: green. If a pre-existing failure appears, record it before changing production code and determine whether it is actually in HPA-359 scope.

This baseline deliberately includes `ShopScreenControllerTest` and `HealingScreenControllerTest`; they are the direct owners if the live NPC composition smoke exposes a defect.

- [ ] **Step 3: Confirm the automated-only requirements are already present**

Verify the focused suite still contains and passes:

- `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice` at `640×360`;
- the `GameTest.CorruptedSave_*` hosted-prompt cases;
- Puzzle/Riddle controller and real-route configured-Cancel coverage.

Do not create a manual long-text or corrupt-save fixture for HPA-359 when these tests are green.

- [ ] **Step 4: Create the evidence document with the named routes**

Create `docs/ui/hpa-359/release-validation.md` with this structure:

```markdown
# HPA-359 Sirius UI Release Validation

## Automated baseline
- Build: pending
- Focused UI/game lifecycle suite: pending
- Compact long-Dialogue regression: pending
- Corrupted-save hosted Prompt regressions: pending
- Puzzle/Riddle regressions: pending
- Full suite: pending

## Journey 1 — Main Menu → FloorGF → Inventory → Exploration
- Route: New Game → spawn (8, 50) → Inventory → close
- 1280×720: pending
- 640×360 Inventory: pending
- Keyboard/mouse: pending

## Journey 2 — Mira → Shop → Goblin Battle → Exploration
- Route: Mira (12, 46) → Browse your wares → Shop → close → goblin (24, 45) → Battle/result
- 1280×720: pending
- Dialogue → Shop HUD/focus/input handoff: pending
- Battle/result single presentation: pending

## Journey 3 — Pause → Save overwrite Prompt → Pause → Settings → Return to Title
- 1280×720: pending
- Disposable manual slot used: pending
- 640×360 nested Save/overwrite Prompt: pending
- Same-Pause focus restoration: pending

## Gamepad smoke
- Physical controller attached: pending
- Keyboard-open hosted UI → joypad focus move → joypad Cancel: pending

## Runtime observations
- Warnings/errors: pending
- Duplicate activations: pending
- Stuck focus/pause/input/cursor/HUD state: pending

## Legacy/current-doc audit
- Production executable leftovers: pending
- CLAUDE.md current-architecture drift: pending
- PRD current-status drift: pending
- HPA-376 configured-Cancel intro drift: pending

## Evidence screenshots
- pending
```

`pending` is the runtime evidence state, not an unresolved plan decision. Replace it only with observed results; gamepad is `N/A` when no physical controller is attached.

- [ ] **Step 5: Record baseline results**

Write the exact commands, pass/fail counts, and relevant warning notes into the evidence document. Record the automated long-Dialogue, corrupt-save, and Puzzle/Riddle cases as automation evidence rather than manual journey requirements.

- [ ] **Step 6: Commit the baseline/evidence scaffold**

```bash
git add docs/ui/hpa-359/release-validation.md
git commit -m "docs: start HPA-359 release validation"
```

---

## Task 2: Validate Journey 1 — Main Menu, FloorGF, Inventory, return

**Files:**
- Production owners if a defect is reproduced: `scripts/ui/MainMenu.cs`, `scenes/ui/MainMenu.tscn`, `scripts/game/Game.cs`, `scripts/ui/ExplorationHudController.cs`, `scenes/ui/ExplorationHud.tscn`, `scripts/ui/InventoryMenuController.cs`, `scenes/ui/InventoryMenu.tscn`
- Primary tests if a defect is reproduced: `tests/ui/MainMenuTest.cs`, `tests/ui/MainMenuSceneTest.cs`, `tests/game/GameTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/game/GameplayPauseHostTest.cs`, `tests/ui/ExplorationHudControllerTest.cs`, `tests/ui/InventoryMenuControllerTest.cs`, `tests/ui/InventoryMenuSceneTest.cs`
- Evidence: `docs/ui/hpa-359/release-validation.md`, `docs/ui/hpa-359/evidence/`

**Interfaces:**
- Consumes: New Game production handoff and `Floor0Layout.PlayerStart == (8, 50)`.
- Produces: Journey 1 evidence and, only if RED is reproduced, a focused regression plus minimal owning-code fix.

- [ ] **Step 1: Run the existing Journey 1 protection**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: green.

- [ ] **Step 2: Run the exact production journey at `1280×720`**

From the real Main Menu scene:

1. Choose **New Game** so the route is deterministic.
2. Confirm FloorGF exploration starts at `(8, 50)`.
3. Confirm the production Exploration HUD is present and no retired debug panel/instructions are visible.
4. Open Inventory through the configured keyboard action.
5. Move focus across at least one real equipment/inventory target.
6. Close Inventory with configured Cancel or the Inventory toggle.
7. Move in exploration after close.

Verify:

- Inventory opens exactly once.
- Gameplay input does not leak through while Inventory is topmost.
- HUD is hidden while Inventory is active and restores after close.
- Cursor state restores after close.
- Direct Inventory does not acquire a tree-pause lease.
- Focus does not remain on a freed/hidden Inventory control.
- No normal-flow warning/error or missing-resource fallback is emitted.

- [ ] **Step 3: Repeat only Inventory at `640×360`**

Verify the visible page/catalogue, equipment area, action/focus target, and summary remain enclosed and usable. Do not rerun Main Menu or unrelated exploration just to create a viewport matrix.

- [ ] **Step 4: If Journey 1 exposes a defect, write the focused RED test**

Use the nearest existing owner:

- host/focus/pause/input leak → `GameplayPauseHostTest.cs` or `GameInputLifecycleTest.cs`;
- Main Menu handoff/focus → `MainMenuTest.cs` / `MainMenuSceneTest.cs`;
- Inventory layout/focus → `InventoryMenuControllerTest.cs` / `InventoryMenuSceneTest.cs`;
- HUD restoration → `ExplorationHudControllerTest.cs` or gameplay lifecycle tests.

Run the selected class and confirm it fails for the observed behavior before editing production code.

- [ ] **Step 5: Apply the minimal GREEN fix only after RED**

Edit only the owning production file(s). Preserve screen/host/domain ownership and do not add an abstraction for a one-off hardening defect.

Re-run the focused failing class, then the Step 1 Journey 1 suite.

- [ ] **Step 6: Capture Journey 1 evidence**

Add:

- `docs/ui/hpa-359/evidence/journey-1-inventory-1280x720.png`
- `docs/ui/hpa-359/evidence/journey-1-inventory-640x360.png`

Update Journey 1 and runtime observations in `release-validation.md`.

- [ ] **Step 7: Commit Journey 1 evidence/fixes**

Stage only paths actually changed, for example:

```bash
git add docs/ui/hpa-359 tests/ui/InventoryMenuControllerTest.cs tests/ui/InventoryMenuSceneTest.cs scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn
git commit -m "test: validate Sirius launch inventory journey"
```

If there was no production/test change, stage only the evidence paths that exist.

---

## Task 3: Validate Journey 2 — Mira Dialogue, Shop, goblin Battle, return

**Files:**
- Production owners if a defect is reproduced: `scripts/ui/NpcInteractionController.cs`, `scripts/ui/DialogueScreenController.cs`, `scenes/ui/DialogueScreen.tscn`, `scripts/ui/ShopScreenController.cs`, `scenes/ui/ShopScreen.tscn`, `scripts/ui/HealingScreenController.cs`, `scenes/ui/HealingScreen.tscn`, `scripts/ui/BattleManager.cs`, `scenes/ui/BattleScene.tscn`, `scripts/game/Game.cs`
- Primary tests if a defect is reproduced: `tests/ui/NpcInteractionControllerTest.cs`, `tests/ui/DialogueScreenControllerTest.cs`, `tests/ui/ShopScreenControllerTest.cs`, `tests/ui/HealingScreenControllerTest.cs`, `tests/ui/BattleManagerTest.cs`, `tests/ui/BattleSceneTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/game/GameTest.cs`
- Evidence: `docs/ui/hpa-359/release-validation.md`, `docs/ui/hpa-359/evidence/`

**Interfaces:**
- Consumes: Mira at `(12, 46)`, `"Browse your wares." → DialogueOutcomeType.OpenShop`, Shop HUD-hidden host policy, goblin at `(24, 45)`.
- Produces: live Dialogue→Shop and independent exploration→Battle composition evidence; optional healer smoke does not replace Shop.

- [ ] **Step 1: Run the existing NPC/Shop/Heal/Battle protection**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest"
```

Expected: green.

Puzzle/Riddle remains in this command because it is still part of release protection; it is not part of the manual route.

- [ ] **Step 2: Run the exact FloorGF production route at `1280×720`**

Starting from the Ground Floor session:

1. Walk to Mira / `village_shopkeeper` at `(12, 46)`.
2. Open Dialogue and choose **Browse your wares.**
3. Observe Dialogue close and exactly one Shop surface open.
4. Verify Shop is usable, then close it with configured Cancel/Close.
5. Confirm exploration controls/HUD return.
6. Walk to the goblin at `(24, 45)`.
7. Complete Battle preparation/combat/result and return to exploration.

Verify the NPC seam specifically:

- Dialogue is the sole hosted interaction entry before the choice.
- Dialogue's visible HUD is replaced by Shop's hidden HUD policy without a stale Dialogue view or focus owner.
- Shop blocks gameplay, does not pause the tree, owns topmost Cancel, and closes exactly once.
- `GameManager.IsInNpcInteraction` clears when Shop closes.

Verify the Battle seam independently:

- walking into the named goblin starts Battle from exploration rather than as an NPC/Puzzle continuation;
- Battle owns input/focus while active;
- result/reward presentation occurs once;
- exploration HUD/prompt/input/cursor state restores afterward;
- no duplicate battle/result/reward activation occurs.

- [ ] **Step 3: Optional healer smoke is extra evidence only**

If useful while already on FloorGF, walk to `village_healer` at `(12, 54)` and verify Dialogue → Heal → exploration. Do not use Heal as a substitute for the required Mira → Shop route and do not add another screenshot solely for this optional check.

- [ ] **Step 4: If Journey 2 exposes a defect, write the focused RED test**

Use the owning suite:

- Dialogue→Shop/Heal host/HUD/focus lifecycle → `NpcInteractionControllerTest.cs` or `GameInputLifecycleTest.cs`;
- Shop-local layout/action behavior → `ShopScreenControllerTest.cs`;
- Heal-local behavior → `HealingScreenControllerTest.cs`;
- Battle lifecycle/result behavior → `BattleManagerTest.cs`, `BattleSceneTest.cs`, or gameplay lifecycle tests.

Confirm RED with a class-filtered command before editing production code.

- [ ] **Step 5: Apply the minimal GREEN fix only for reproduced defects**

Preserve NPC, Shop/Heal transaction, Battle, and reward ownership. UI code must not grant rewards or change NPC/Battle domain rules.

Re-run the focused failing class, then the Step 1 suite and the affected production route.

- [ ] **Step 6: Capture Journey 2 evidence**

Add:

- `docs/ui/hpa-359/evidence/journey-2-shop-1280x720.png`
- `docs/ui/hpa-359/evidence/journey-2-battle-result-1280x720.png`

Update Journey 2 and runtime observations in `release-validation.md`.

- [ ] **Step 7: Commit Journey 2 evidence/fixes**

Stage only changed paths, then commit:

```bash
git commit -m "test: validate Sirius shop battle journey"
```

---

## Task 4: Validate Journey 3 — overwrite Prompt, Settings, Return to Title

**Files:**
- Production owners if a defect is reproduced: `scripts/game/Game.cs`, `scripts/ui/hosting/UIScreenHost.cs`, `scripts/ui/PauseScreenController.cs`, `scenes/ui/PauseScreen.tscn`, `scripts/ui/SaveLoadScreenController.cs`, `scenes/ui/SaveLoadScreen.tscn`, `scripts/ui/SettingsMenuController.cs`, `scenes/ui/SettingsMenu.tscn`, `scripts/ui/components/SiriusPrompt.cs`
- Primary tests if a defect is reproduced: `tests/game/GameplayPauseHostTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/ui/PauseScreenControllerTest.cs`, `tests/ui/SaveLoadScreenControllerTest.cs`, `tests/ui/SaveLoadScreenSceneTest.cs`, `tests/ui/SettingsMenuControllerTest.cs`, host tests under `tests/ui/hosting/`, prompt tests under `tests/ui/components/`
- Evidence: `docs/ui/hpa-359/release-validation.md`, `docs/ui/hpa-359/evidence/`

**Interfaces:**
- Consumes: Pause-owned tree-pause lease, Save overwrite request for an occupied manual slot, shared destructive `SiriusPrompt`, Settings as a Pause child.
- Produces: exact nested-stack/focus/teardown evidence and conditional hosted-UI gamepad evidence.

- [ ] **Step 1: Run the existing Pause/host/Save/Settings/Prompt suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt|FullyQualifiedName~GameTest"
```

Expected: green, including corrupted-save prompt automation.

- [ ] **Step 2: Run the exact nested journey at `1280×720`**

1. Open Pause from FloorGF exploration.
2. Open **Save**.
3. Choose a disposable manual slot. If it is empty, save once so it becomes occupied and return to Pause.
4. Reopen **Save** and select the same occupied slot.
5. Confirm the destructive overwrite Prompt opens above the retained Save screen.
6. Cancel/close only the Prompt and verify Save remains active.
7. Close Save and verify focus returns to the same Pause instance.
8. Open **Settings** from that Pause, move focus/change no persisted value, then Cancel.
9. Verify focus again returns to the same Pause instance.
10. Choose **Return to Title** and complete its production confirmation flow.

Use a disposable slot; do not destroy developer progress for release evidence. Record the slot chosen in the evidence document.

Verify:

- exactly one Pause tree-pause lease;
- Save/Settings remain interactive while paused without a second pause lease;
- topmost Cancel closes the overwrite Prompt before Save;
- the retained Save parent remains alive under the Prompt;
- child close restores focus to the same live Pause instance;
- Return to Title tears the host stack down before scene replacement;
- cursor/HUD/input state does not leak into Main Menu.

- [ ] **Step 3: Repeat only the nested Save/overwrite Prompt at `640×360`**

Open the same Save path and occupied disposable slot at `640×360`. Verify the destructive Prompt and retained Save surface remain readable, enclosed, and operable.

Do **not** manufacture a corrupted save or a long Dialogue for this runtime pass; their existing automated owners already cover those requirements.

- [ ] **Step 4: Perform the bounded gamepad smoke or record N/A**

Inspect whether a physical controller is attached.

If attached:

1. Open Pause or Inventory with the normal keyboard gameplay binding.
2. Use the controller's existing Godot `ui_*` navigation to move focus at least once.
3. Use joypad `ui_cancel` to close the hosted screen.
4. Verify focus/input returns to the expected parent/gameplay state.

If no controller is attached, write `N/A — no physical controller attached` in `release-validation.md`.

Do not require opening Inventory/Interact/Pause from the controller: the default gameplay bindings are keyboard-only, and adding joypad gameplay bindings is outside HPA-359.

- [ ] **Step 5: If Journey 3 exposes a defect, write focused RED before production changes**

Use:

- `GameplayPauseHostTest.cs` for stack/lease/focus defects;
- `GameInputLifecycleTest.cs` for topmost Cancel/input defects;
- Save/Settings/Prompt direct suites for local behavior.

Confirm the observed defect fails before production edits.

- [ ] **Step 6: Apply the minimal GREEN fix and re-run affected coverage**

Reuse `UIScreenHost`, `SiriusModalShell`, and `SiriusPrompt`. Do not add another prompt/host abstraction.

Re-run the focused class, the Step 1 suite, and the affected production route.

- [ ] **Step 7: Capture Journey 3 evidence**

Add:

- `docs/ui/hpa-359/evidence/journey-3-save-overwrite-prompt-640x360.png`
- `docs/ui/hpa-359/evidence/journey-3-pause-1280x720.png`

Update Journey 3, gamepad, and runtime-observation sections in `release-validation.md`.

- [ ] **Step 8: Commit Journey 3 evidence/fixes**

Stage only changed paths, then commit:

```bash
git commit -m "test: validate Sirius pause child journey"
```

---

## Task 5: Audit legacy production paths and correct current documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify narrowly: `docs/PRD.md`
- Modify narrowly: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: `scripts/`, `scenes/`, `project.godot`
- Update production/test files only if an executable leftover is proven

**Interfaces:**
- Consumes: shipped scene-authored/hosted architecture already proven by Tasks 1–4.
- Produces: current operating docs that stop directing developers toward deleted UI owners; no new runtime interface.

- [ ] **Step 1: Search for retired/debug/current-status indicators**

Run:

```bash
rg -n 'DialogueDialog|SaveLoadDialog|SaveOverwriteConfirmationController|DraggablePanel|Player HUD|Settings menu coming soon' scripts scenes project.godot CLAUDE.md docs/PRD.md docs/ui/hpa-376/ui-lifecycle-contract.md
rg -n 'AcceptDialog|ConfirmationDialog|ui_close_dialog' scripts scenes project.godot docs/ui/hpa-376/ui-lifecycle-contract.md
```

Classify each match before editing:

- executable native-dialog/debug path → candidate for removal after replacement proof;
- historical comment saying a new screen replaces an old path → keep unless it falsely claims current ownership;
- intentional engine/input compatibility code still required by current behavior → keep;
- current developer/status documentation naming a deleted owner → correct.

- [ ] **Step 2: Correct `CLAUDE.md` current architecture only**

Make these narrow updates:

- describe Battle as the scene-authored full-screen hosted battle flow rather than a modal dialog;
- describe scene flow using hosted/screen presentation rather than “Battle dialogs”;
- update `scripts/ui` examples to current controllers (`DialogueScreenController`, `SaveLoadScreenController`, `PauseScreenController`, `ShopScreenController`, `HealingScreenController`) and `UIScreenHost`; remove deleted controller names as current files;
- describe overwrite/recoverable feedback through the shared hosted `SiriusPrompt` path rather than `SaveOverwriteConfirmationController`;
- fix other false current-architecture statements only when they occur in the same edited paragraphs.

`AGENTS.md` is the symlink; edit `CLAUDE.md` once.

- [ ] **Step 3: Correct only false current-status sentences in `docs/PRD.md`**

Do not modernize/rewrite the April PRD feature requirements.

Narrowly update current implementation-status prose that currently:

- names `DialogueDialog` as the implemented Dialogue UI; replace it with the scene-authored `DialogueScreenController` / hosted Dialogue path and current Shop/Healing screens;
- says Settings has no `.tscn`/controller or that Main Menu still shows “Settings menu coming soon!”; state that `SettingsMenu.tscn` / `SettingsMenuController` are implemented and hosted from Main Menu/gameplay.

If an immediately adjacent status bullet directly repeats one of those false claims, correct/remove that bullet for internal consistency. Do not recompute the historical overall-completion metric or rewrite unrelated feature-body prose.

- [ ] **Step 4: Correct only the HPA-376 configured-Cancel introduction**

Update the paragraph before the flow matrix so it no longer presents `AcceptDialog/ui_close_dialog` as a current production owner.

The replacement paragraph must describe the shipped contract:

- Settings mirrors the configured `pause_menu` keyboard binding onto `ui_cancel` while preserving non-key/controller `ui_cancel` events;
- hosted screens route topmost Cancel through `UIScreenHost` and their entry policies/interceptors;
- `ui_close_dialog` synchronization remains only where native dialog compatibility is still intentionally needed, not as the owner of migrated Sirius screens.

Do not modify the flow matrix, dispositions, historical evidence, or appendices in HPA-376.

- [ ] **Step 5: If the audit finds executable obsolete production code, protect the replacement before deletion**

Run or add the nearest existing test proving the migrated owner handles the path. Confirm green replacement ownership, then delete only the dead source/scene/reference and rerun that focused suite.

Do not delete historical comments or intentional dynamic rows merely because they contain legacy terms or are runtime-created.

- [ ] **Step 6: Repeat the audit**

Re-run the Step 1 `rg` commands. Expected: no unclassified executable legacy path or false current-status documentation remains. Historical “former X” comments may remain when useful.

- [ ] **Step 7: Record the audit result**

In `docs/ui/hpa-359/release-validation.md`, record:

- commands used;
- executable leftovers removed, if any;
- intentionally retained historical/comment/input-compatibility matches;
- `CLAUDE.md` corrections;
- PRD current-status corrections;
- HPA-376 configured-Cancel intro correction.

- [ ] **Step 8: Commit cleanup/documentation**

Stage only paths actually changed, then commit:

```bash
git commit -m "docs: align Sirius UI development guidance"
```

---

## Task 6: Final verification and closeout evidence

**Files:**
- Modify: `docs/ui/hpa-359/release-validation.md`
- Verify: every file changed by HPA-359

**Interfaces:**
- Consumes: final code/docs/evidence state from Tasks 1–5.
- Produces: closeout evidence only; no runtime API.

- [ ] **Step 1: Re-run the Task 1 focused cross-screen suite**

Use the exact Task 1 Step 2 command. Expected: green.

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Expected: all tests pass. Record exact passed/failed/skipped totals.

Do not fail HPA-359 merely because historical test infrastructure emits a known pre-existing non-runtime warning; investigate and record it. New warnings/errors caused by HPA-359 or observed in the normal production journeys must be resolved.

- [ ] **Step 3: Re-run the three exact production routes after the final code state**

At `1280×720`, rerun:

1. New Game → FloorGF `(8, 50)` → Inventory → Exploration.
2. Mira `(12, 46)` → Browse your wares → Shop → close → goblin `(24, 45)` → Battle/result → Exploration.
3. Pause → Save occupied disposable slot → overwrite Prompt → Save → Pause → Settings → Pause → Return to Title.

Do not recapture screenshots unless a visible state changed after a fix.

Expected:

- no duplicate activation;
- no stuck focus, pause, input, cursor, or HUD policy;
- no normal-flow warning/error or missing-resource fallback;
- Return to Title completes cleanly.

- [ ] **Step 4: Finish `release-validation.md`**

Replace every remaining runtime `pending` with an actual observation. For gamepad, record either the bounded hosted-UI result or explicit N/A because no controller was attached.

List the six-or-fewer screenshot paths and every defect fixed, with the focused regression test that protects that fix.

- [ ] **Step 5: Review the complete diff for scope**

Run:

```bash
git diff --check
git status --short
git diff --stat main...HEAD
git diff main...HEAD -- docs/superpowers/specs/2026-08-20-hpa-359-final-ui-hardening-design.md docs/superpowers/plans/2026-08-20-hpa-359-final-ui-hardening.md docs/ui/hpa-359 CLAUDE.md docs/PRD.md docs/ui/hpa-376/ui-lifecycle-contract.md scripts scenes tests
```

Confirm:

- one HPA-359 PR only;
- no HPA-375/HPA-541 work;
- no gameplay joypad-binding expansion;
- no new generic UI/test architecture;
- every production change corresponds to a reproduced HPA-359 defect or proven dead path;
- PRD/HPA-376 edits are limited to the named current-status/intro corrections;
- evidence is factual and matches the final code state.

- [ ] **Step 6: Commit final evidence**

If the evidence document changed since the last commit:

```bash
git add docs/ui/hpa-359/release-validation.md
git commit -m "docs: record HPA-359 release validation"
```

Do not create an empty commit.

## Expected final change shape

The minimum successful HPA-359 implementation may legitimately contain **no production C# change** if the shipped UI paths are already correct. In that case the ticket still delivers value through:

- focused and full-suite verification;
- three named, executable production journeys;
- six-or-fewer representative screenshots;
- the legacy-path audit;
- narrow current-documentation corrections in `CLAUDE.md`, `docs/PRD.md`, and the HPA-376 configured-Cancel introduction.

If defects are discovered, keep their regression tests and minimal fixes inside this same PR and record them in release evidence. Do not split follow-up PRs for defects directly required to satisfy HPA-359.
