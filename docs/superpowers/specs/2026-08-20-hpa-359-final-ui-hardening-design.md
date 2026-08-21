# HPA-359 Final Sirius UI Hardening and Release Validation Design

**Issue:** HPA-359 — Final Sirius UI hardening and release validation  
**Date:** 2026-08-20  
**Scope:** One final integration/hardening slice after the Sirius UI migrations

## Context

The concrete Sirius UI migration work is complete. Main has scene-authored production surfaces for Main Menu, Exploration HUD, Inventory, Battle, Pause, Settings, Save/Load, Dialogue, Shop, Healing, Puzzle/Riddle, shared prompts, reward feedback, and the root-local `UIScreenHost` path. HPA-355 and HPA-358 are completion checkpoints rather than additional implementation slices; their required children are complete, while HPA-375 and HPA-541 remain optional backlog work.

The migration tickets also left broad deterministic GdUnit coverage. HPA-359 should therefore validate the remaining production-scene composition seams and remove proven leftovers, not introduce another UI layer or another test architecture.

The remaining runtime value is narrower than the original draft implied:

- Puzzle/Riddle behavior and compact riddle layout already have direct controller and real-route coverage.
- Long Dialogue wrapping/scrolling at `640×360` is already protected by `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice`.
- Corrupted-save prompt behavior is already protected by the `GameTest.CorruptedSave_*` cases.
- Dialogue → Shop/Heal, and the HUD-policy transition from Dialogue to those hosted surfaces, are covered in isolated/real-route tests but have not received the same explicit production-scene smoke as the final cross-screen pass.

Known current-documentation drift is also broader than `CLAUDE.md` alone. `docs/PRD.md` still names `DialogueDialog` as current and describes Settings as lacking a scene/controller, while the introductory configured-Cancel paragraph in `docs/ui/hpa-376/ui-lifecycle-contract.md` still describes `AcceptDialog/ui_close_dialog` as a current owner even though the later matrix rows document the hosted paths. These are sentence-level current-status corrections; the PRD feature bodies and HPA-376 flow matrix are not being rewritten.

## Goals

1. Prove three named, executable player journeys on the shipped production paths.
2. Exercise the cross-screen composition seams that isolated migration tests cannot fully prove in a live `Game.tscn` session: HUD policy, topmost Cancel, focus restoration, pause ownership, gameplay-input blocking, child prompts, teardown, and duplicate activation.
3. Validate all three journeys at `1280×720`, with `640×360` only for the layout-sensitive production surfaces that add unique runtime evidence.
4. Remove only obsolete executable UI paths whose replacements are already proven.
5. Correct concise current developer/status documentation that still points at deleted UI architecture.
6. Leave a small, reviewable validation record with commands/results and at most six screenshots.
7. Permit a no-production-code closeout when the named journeys and audits are clean.

## Non-goals

- HPA-375 inventory browsing enhancements.
- HPA-541 Reduced Motion.
- New gameplay, save, battle, shop, puzzle, reward, or inventory behavior.
- A new global UI service, E2E framework, screenshot framework, test driver, or release-certification subsystem.
- New gameplay joypad bindings.
- Exhaustive input × viewport × screen matrices.
- Re-running synthetic long-text or corrupted-save scenarios manually when existing automated coverage already owns them.
- A broad rewrite of the April PRD, the HPA-376 flow matrix, historical specs/plans, or evidence appendices.
- Refactoring working controllers merely to make their shapes uniform.

## Decision: named production walks plus existing deterministic owners

HPA-359 uses the existing focused GdUnit suites as deterministic protection and exactly three named runtime journeys as the cross-screen release smoke. It does not build a new automated end-to-end harness.

This is intentionally asymmetric:

- **Automated tests** remain responsible for deterministic lifecycle, focus, input, compact/long-text layout, controller, corrupt-save, puzzle/riddle, and domain-adjacent contracts.
- **Runtime journeys** prove that independently migrated screens compose correctly in the actual Main Menu / `Game.tscn` scenes.
- **Production code changes** happen only after a concrete defect is reproduced by the narrowest useful failing test in its existing owning suite.

The manual routes are named so an implementer cannot satisfy the ticket by wandering FloorGF, picking a non-transition dialogue choice, or marking an avoidable case as naturally unreachable.

## Named FloorGF anchors

Use the committed Ground Floor layout as the runtime smoke fixture:

- New Game player start: `(8, 50)` (`Floor0Layout.PlayerStart`).
- Mira / `village_shopkeeper`: `(12, 46)`.
- Healer / `village_healer`: `(12, 54)`.
- First goblin: `(24, 45)`.
- Mira's `"Browse your wares."` choice resolves to `DialogueOutcomeType.OpenShop`.

Healing is an optional extra smoke at `(12, 54)`; it does not substitute for the required Dialogue → Shop composition check.

## Validation journeys

### Journey 1 — Main Menu → FloorGF → Inventory → exploration

`Launch → New Game → FloorGF spawn (8, 50) → Inventory → close → Exploration`

Run at `1280×720`, then repeat only the Inventory surface at `640×360`.

Check:

- Main Menu New Game action is usable and hands off to the real gameplay scene.
- The player lands on the Ground Floor at the expected new-game start.
- The production Exploration HUD is present rather than the retired debug presentation.
- Inventory opens once, hides the HUD according to host policy, blocks gameplay input, and receives usable focus.
- Configured Cancel/toggle closes only Inventory.
- Closing Inventory restores gameplay state without a stuck cursor, pause lease, input block, or stale focus.

Continue selection/failure remains protected by the existing Main Menu suites; this runtime journey uses New Game so the FloorGF route is deterministic.

### Journey 2 — Mira Dialogue → Shop → goblin Battle → exploration

Use one Ground Floor session:

`FloorGF → Mira (12, 46) → "Browse your wares." → Shop → close Shop → goblin (24, 45) → Battle preparation/combat/result → Exploration`

Run at `1280×720`.

Check:

- Mira opens the scene-authored Dialogue surface through `NpcInteractionController`.
- Choosing `"Browse your wares."` closes Dialogue before exactly one Shop entry opens.
- Dialogue's visible-HUD policy changes to Shop's hidden-HUD policy with no stale interaction view or focus owner.
- Shop is the sole topmost input owner, blocks gameplay without pausing the tree, and closes back to exploration cleanly.
- Walking into the named goblin starts the real Battle entry independently from the NPC interaction.
- Battle preparation/combat/result remains hosted, result/reward presentation occurs once, and exploration resumes cleanly.
- HUD, interaction prompt, cursor, and gameplay input recover after both hosted flows.

Puzzle/Riddle remains part of the focused automated baseline, including configured-Cancel and compact layout coverage, but is not a substitute for this production-scene Shop smoke.

### Journey 3 — Pause → Save overwrite Prompt → Pause → Settings → Return to Title

Run the full journey at `1280×720`:

1. Open Pause from FloorGF exploration.
2. Open Save and use one disposable manual slot. If it is empty, save once to populate it and return to the same Pause.
3. Reopen Save and select the same now-occupied manual slot to open the destructive overwrite Prompt.
4. Cancel/close the topmost overwrite Prompt, verify the Save screen remains alive, then close Save back to the same Pause instance.
5. Open Settings as a second Pause child, navigate one control, then Cancel back to the same Pause.
6. Use Return to Title and complete its production confirmation path.

Repeat only the nested Save/overwrite-Prompt surface at `640×360`.

Check:

- Pause owns the single tree-pause lease.
- Save and Settings remain logical children without taking a second pause lease.
- The overwrite Prompt is topmost, and Cancel affects the Prompt before its retained Save parent.
- Closing each child restores focus to the same live Pause instance.
- Return to Title tears the stack down before scene replacement and leaves no stale cursor/HUD/input state in Main Menu.

Use a disposable manual slot and do not overwrite developer progress merely to create evidence. Corrupted/unavailable-save behavior remains automated under `GameTest.CorruptedSave_*`; it is not a runtime success criterion.

## Automated baseline ownership

The focused baseline includes the existing owners for all three journeys and the automated-only regressions:

- Main Menu / exploration / Inventory: `MainMenuTest`, `MainMenuSceneTest`, `GameTest`, `GameInputLifecycleTest`, `GameplayPauseHostTest`, `ExplorationHudControllerTest`, `InventoryMenuControllerTest`, `InventoryMenuSceneTest`.
- NPC composition: `NpcInteractionControllerTest`, `DialogueScreenControllerTest`, `ShopScreenControllerTest`, `HealingScreenControllerTest`.
- Puzzle/Riddle automated-only coverage: `PuzzleRiddleScreenControllerTest` plus the real-route riddle cases in `GameInputLifecycleTest`.
- Battle: `BattleManagerTest`, `BattleSceneTest`.
- Pause / Save / Settings / Prompt: `PauseScreenControllerTest`, `SaveLoadScreenControllerTest`, `SaveLoadScreenSceneTest`, `SettingsMenuControllerTest`, `GameplayPauseHostTest`, host tests, and shared prompt tests.

The baseline must specifically retain the compact long-Dialogue test and corrupt-save Game tests rather than recreating those conditions manually.

## Viewport policy

Do not rerun every screen at every viewport. Existing scene tests already exercise `SiriusUiMetrics.VerificationViewports` and remain the exhaustive layout owner.

For HPA-359 runtime smoke:

- Run all three named journeys at `1280×720`.
- Run Inventory at `640×360` during Journey 1.
- Run the retained Save/overwrite-Prompt composition at `640×360` during Journey 3.
- Do not add a runtime long-Dialogue case; existing compact Dialogue automation owns it.
- Use an alternate 4:3/ultrawide runtime check only when a reproduced layout defect makes it useful.

## Gamepad policy

Default gameplay actions are keyboard-bound (`toggle_inventory = I`, `interact = E`, `pause_menu = Escape`), so HPA-359 does not claim controller-open gameplay coverage and does not add joypad bindings.

If a physical controller is attached during validation:

1. Open Pause or Inventory with the normal keyboard path.
2. Use Godot's existing `ui_*` joypad navigation to move focus at least once.
3. Use joypad `ui_cancel` to close the hosted surface.
4. Verify focus/input restores to the expected parent/gameplay state.

If no controller is attached, record the gamepad smoke as **N/A**. N/A is acceptable and must not be reported as a pass.

## Evidence

Implementation creates `docs/ui/hpa-359/release-validation.md` and records:

- baseline build and focused automated-test commands/results;
- the exact named FloorGF runtime routes and outcomes;
- the existing automated long-Dialogue, riddle, and corrupt-save coverage relied on instead of manual duplication;
- runtime warning/error observations;
- gamepad smoke result or explicit N/A;
- the legacy-path/current-doc audit result;
- final full-suite result;
- links to a deliberately small screenshot set.

Use at most six representative screenshots under `docs/ui/hpa-359/evidence/`:

1. Inventory at `1280×720`.
2. Inventory at `640×360`.
3. Mira-hosted Shop at `1280×720`.
4. Battle result/reward at `1280×720`.
5. Save/overwrite Prompt at `640×360`.
6. Pause at `1280×720` before Return to Title.

These are release evidence, not a golden-image test system.

## Cleanup policy

Cleanup happens only after a replacement is proven.

1. Audit `scripts/`, `scenes/`, and `project.godot` for legacy native-dialog/debug paths and stale fallback references.
2. Classify every match before changing it. Historical comments such as “replaces the former AcceptDialog” are not executable legacy paths and do not justify churn by themselves.
3. Delete or simplify only executable code/resources that are unreachable because the migrated screen already owns the production path.
4. Do not refactor intentional dynamic runtime UI such as dialogue choices or Shop/Inventory catalogue rows.
5. Correct the known current-documentation drift only where it falsely describes shipped architecture:
   - `CLAUDE.md`: current Battle/screen/host/prompt owners and deleted controller names.
   - `docs/PRD.md`: only current implementation-status sentences that still name `DialogueDialog` or claim Settings has no scene/controller; do not rewrite the feature requirements/body.
   - `docs/ui/hpa-376/ui-lifecycle-contract.md`: only the configured-Cancel introductory paragraph so it describes hosted `ui_cancel` ownership; do not rewrite the flow matrix or historical evidence.
6. Leave historical specs/plans intact unless they are falsely presented as current operating documentation.

At planning time, no still-live obsolete production scene/controller has been identified in `scripts/ui` or `scenes/ui`; production deletion is conditional on the implementation audit finding an actual executable leftover.

## Defect handling

A defect found during HPA-359 stays in this single PR when it is a direct UI-hardening issue.

For each defect:

1. Identify the existing owning controller/scene and nearest test suite.
2. Add the smallest failing regression test that reproduces the issue.
3. Confirm the focused test fails for the intended reason.
4. Make the smallest production fix; do not add a new abstraction for a one-off closeout defect.
5. Re-run the focused suite and the affected named runtime journey.

If the finding is new gameplay/product scope rather than a regression or migration defect, record it separately and leave it out of HPA-359.

## File ownership map for discovered defects

| Surface | Primary production owners | Primary tests |
| --- | --- | --- |
| Main Menu / Continue | `scripts/ui/MainMenu.cs`, `scenes/ui/MainMenu.tscn` | `tests/ui/MainMenuTest.cs`, `tests/ui/MainMenuSceneTest.cs` |
| Exploration / HUD / Inventory | `scripts/game/Game.cs`, `scripts/ui/ExplorationHudController.cs`, `scripts/ui/InventoryMenuController.cs` | `tests/game/GameTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/game/GameplayPauseHostTest.cs`, `tests/ui/ExplorationHudControllerTest.cs`, `tests/ui/InventoryMenuControllerTest.cs`, `tests/ui/InventoryMenuSceneTest.cs` |
| NPC / Dialogue / Shop / Heal | `scripts/ui/NpcInteractionController.cs`, `scripts/ui/DialogueScreenController.cs`, `scripts/ui/ShopScreenController.cs`, `scripts/ui/HealingScreenController.cs` | `tests/ui/NpcInteractionControllerTest.cs`, `tests/ui/DialogueScreenControllerTest.cs`, `tests/ui/ShopScreenControllerTest.cs`, `tests/ui/HealingScreenControllerTest.cs`, `tests/game/GameInputLifecycleTest.cs` |
| Puzzle/Riddle | `scripts/game/Game.cs`, `scripts/ui/PuzzleRiddleScreenController.cs` | `tests/ui/PuzzleRiddleScreenControllerTest.cs`, `tests/game/GameInputLifecycleTest.cs` |
| Battle / result | `scripts/ui/BattleManager.cs`, `scenes/ui/BattleScene.tscn` | `tests/ui/BattleManagerTest.cs`, `tests/ui/BattleSceneTest.cs` |
| Pause / nested children | `scripts/game/Game.cs`, `scripts/ui/hosting/UIScreenHost.cs`, `scripts/ui/PauseScreenController.cs` | `tests/game/GameplayPauseHostTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/ui/PauseScreenControllerTest.cs`, host tests under `tests/ui/hosting/` |
| Save/Load / Settings / Prompt | `scripts/ui/SaveLoadScreenController.cs`, `scripts/ui/SettingsMenuController.cs`, `scripts/ui/components/SiriusPrompt.cs` | `tests/ui/SaveLoadScreenControllerTest.cs`, `tests/ui/SaveLoadScreenSceneTest.cs`, `tests/ui/SettingsMenuControllerTest.cs`, prompt tests under `tests/ui/components/` |

## Acceptance

HPA-359 is complete when:

- all three named journeys succeed at `1280×720` on the shipped paths;
- Inventory and the nested Save/overwrite Prompt remain usable at `640×360`;
- the Mira Dialogue → Shop → goblin Battle route completes without stale host/focus/HUD/input state;
- topmost Cancel, focus restoration, pause/input/cursor/HUD policy, nested child behavior, and teardown are consistent;
- no normal-flow runtime warning/error, duplicate activation, missing-resource fallback, or stuck UI state is observed;
- long Dialogue, Puzzle/Riddle, and corrupted-save requirements remain green in their existing automated owners;
- if a physical controller is attached, keyboard-open → joypad navigate → joypad Cancel succeeds; otherwise the evidence explicitly records N/A;
- any executable obsolete UI path discovered by the audit is removed after its replacement is proven;
- `CLAUDE.md`, the narrow PRD current-status notes, and the HPA-376 configured-Cancel intro describe the shipped architecture without rewriting historical bodies/matrices;
- focused tests and the full test suite are green;
- `docs/ui/hpa-359/release-validation.md` contains factual final evidence and at most six screenshots.

A clean result with documentation/evidence changes only and no production C# change is a valid successful closeout.
