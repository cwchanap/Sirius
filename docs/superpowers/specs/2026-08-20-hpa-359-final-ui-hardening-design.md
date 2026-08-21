# HPA-359 Final Sirius UI Hardening and Release Validation Design

**Issue:** HPA-359 — Final Sirius UI hardening and release validation  
**Date:** 2026-08-20  
**Scope:** One final integration/hardening slice after the Sirius UI migrations

## Context

The concrete Sirius UI migration work is complete. Main now has scene-authored production surfaces for Main Menu, Exploration HUD, Inventory, Battle, Pause, Settings, Save/Load, Dialogue, Shop, Healing, Puzzle/Riddle, shared prompts, reward feedback, and the root-local `UIScreenHost` path. HPA-355 and HPA-358 are completion checkpoints rather than additional implementation slices; their required children are complete, while HPA-375 and HPA-541 remain optional backlog work.

The latest migration PR also leaves the repository with broad automated coverage. HPA-359 should therefore validate integration seams and remove proven leftovers, not introduce another UI layer or redesign already-shipped screens.

One known cleanup item already exists: `CLAUDE.md` still describes deleted or superseded UI types such as `DialogueDialog` and `SaveOverwriteConfirmationController`, and still describes the current battle flow as a dialog. The current `scripts/ui` and `scenes/ui` production trees use the migrated screen/host model instead.

## Goals

1. Prove the three representative player journeys from HPA-359 on the shipped production paths.
2. Re-check the cross-screen invariants that individual migration tickets cannot prove alone: topmost Cancel, focus restoration, pause ownership, gameplay-input blocking, cursor/HUD policy, child prompt behavior, teardown, and duplicate activation.
3. Validate the primary `1280×720` and minimum `640×360` layouts without creating an exhaustive viewport/input matrix.
4. Exercise one supported gamepad smoke path and one intentionally long dialogue/message case.
5. Remove only obsolete executable UI paths whose replacements are already proven.
6. Bring concise developer-facing UI documentation in line with the shipped architecture.
7. Leave a small, reviewable validation record with commands/results and representative screenshots.

## Non-goals

- HPA-375 inventory browsing enhancements.
- HPA-541 Reduced Motion.
- New gameplay, save, battle, shop, puzzle, reward, or inventory behavior.
- A new global UI service, E2E framework, screenshot framework, test driver, or release-certification subsystem.
- Exhaustive input × viewport × screen matrices.
- A broad rewrite of the April PRD or historical HPA-376 design records.
- Refactoring working controllers merely to make their shapes uniform.

## Decision: journey-first, evidence-driven hardening

HPA-359 will use the existing focused GdUnit suites as automated protection and three runtime journeys as the cross-screen release smoke. It will not build a new automated end-to-end harness.

This is intentionally asymmetric:

- **Automated tests** remain responsible for deterministic lifecycle, focus, input, layout, controller, and domain-adjacent contracts.
- **Runtime journeys** prove that the independently migrated screens compose correctly in the actual game scene and expose visual/runtime defects that unit or scene tests would not naturally catch.
- **Production code changes** are made only when one of those checks exposes a concrete defect. Every such defect first gets the narrowest useful regression test in its existing owning suite, then the minimal fix.

That keeps the final pass cheap and maintainable instead of creating a second test architecture just before closeout.

## Validation journeys

### Journey 1 — launch, exploration, inventory, return

`Launch → Continue or New Game → Exploration → HUD → Inventory → Exploration`

Check:

- Main Menu action availability and focus.
- Successful scene handoff into gameplay.
- Exploration HUD visibility and interaction prompt behavior.
- Inventory opens once, hides HUD according to host policy, blocks gameplay input, and receives usable focus.
- Configured Cancel/toggle closes only Inventory.
- Closing Inventory restores gameplay state without a stuck cursor, pause lease, input block, or duplicate activation.

Automated owners already include `MainMenuTest`, `MainMenuSceneTest`, `GameTest`, `GameInputLifecycleTest`, `GameplayPauseHostTest`, `ExplorationHudControllerTest`, `InventoryMenuControllerTest`, and `InventoryMenuSceneTest`.

### Journey 2 — interaction, battle, result, return

`Exploration → NPC or Puzzle/Riddle → Battle preparation/combat/result → Exploration`

Check:

- NPC/Dialogue or Puzzle/Riddle owns the topmost input surface while active.
- One long dialogue/message wraps and remains usable at the minimum viewport.
- Transition into Battle does not leave an interaction surface, stale focus, or conflicting gameplay input owner behind.
- Battle result/reward presentation occurs once and returns cleanly to exploration.
- HUD, interaction prompt, cursor, and gameplay input recover after the flow.

Automated owners already include `NpcInteractionControllerTest`, `DialogueScreenControllerTest`, `PuzzleRiddleScreenControllerTest`, `BattleManagerTest`, `BattleSceneTest`, `GameInputLifecycleTest`, and the existing reward/lifecycle tests.

### Journey 3 — pause children and nested prompt

`Exploration → Pause → Save/Load or Settings → nested confirmation/error → Pause → Return to Title`

Check:

- Pause owns the single tree-pause lease.
- Save/Load and Settings remain logical children without taking a second pause lease.
- Nested destructive/recoverable prompts are topmost and Cancel affects only the topmost eligible entry.
- Closing a child restores focus to the same Pause instance.
- Corrupted/unavailable save handling stays on the hosted prompt path when naturally reachable.
- Return to Title tears the stack down without stale handles, cursor/HUD leaks, warnings, or duplicate actions.

Automated owners already include `GameplayPauseHostTest`, `GameInputLifecycleTest`, `PauseScreenControllerTest`, `SaveLoadScreenControllerTest`, `SaveLoadScreenSceneTest`, `SettingsMenuControllerTest`, `MainMenuTest`, and the shared prompt/host tests.

## Viewport and input policy

Do not rerun every screen at every viewport. Existing scene tests already exercise `SiriusUiMetrics.VerificationViewports`, including `640×360`, `1280×720`, `1024×768`, and `2560×1080` where relevant.

For HPA-359 runtime smoke:

- Run all three journeys at `1280×720`.
- Re-run the layout-sensitive surfaces at `640×360`: Inventory, long Dialogue/Message, and nested Save/Load Prompt.
- Use one alternate aspect ratio only when a real layout concern is observed; prefer `1024×768` for 4:3 or `2560×1080` for ultrawide based on the defect.
- Keyboard/mouse is the primary end-to-end path.
- Perform one gamepad smoke through open → navigate → Cancel/close on a hosted screen if gamepad support remains enabled.

## Evidence

Implementation creates `docs/ui/hpa-359/release-validation.md` and records:

- baseline build and focused automated-test commands/results;
- the three runtime journey outcomes;
- runtime warning/error observations;
- gamepad smoke result;
- the legacy-path audit result;
- final full-suite result;
- links to a deliberately small screenshot set.

Use at most six representative screenshots under `docs/ui/hpa-359/evidence/`:

1. Inventory at `1280×720`.
2. Inventory at `640×360`.
3. Long Dialogue/Message at `640×360`.
4. Battle result/reward at `1280×720`.
5. Save/Load nested prompt at `640×360`.
6. Pause at `1280×720` before Return to Title.

These are release evidence, not a new golden-image test system.

## Cleanup policy

Cleanup happens after replacement is proven, matching HPA-359's wording.

1. Audit `scripts/`, `scenes/`, and `project.godot` for legacy native-dialog/debug paths and stale fallback references.
2. Classify every match before changing it. Historical comments such as “replaces the former AcceptDialog” are not executable legacy paths and do not justify churn by themselves.
3. Delete or simplify only executable code/resources that are unreachable because the migrated screen already owns the production path.
4. Do not refactor unrelated runtime UI construction that is still intentionally dynamic, such as dialogue choices or catalogue entries.
5. Update `CLAUDE.md` to describe the actual shipped screen/host/prompt architecture and remove references to deleted controller names.
6. Leave historical specs/plans intact unless they are falsely presented as current operating documentation.

At planning time, no still-live obsolete production scene/controller has been identified in `scripts/ui` or `scenes/ui`; the known guaranteed cleanup is developer-documentation drift. Production deletion is therefore conditional on the implementation audit finding an actual executable leftover.

## Defect handling

A defect found during HPA-359 stays in this single PR when it is a direct UI-hardening issue.

For each defect:

1. Identify the existing owning controller/scene and its nearest test suite.
2. Add the smallest failing regression test that reproduces the issue.
3. Confirm the focused test fails for the intended reason.
4. Make the smallest production fix; do not add a new abstraction unless a second real consumer already requires it.
5. Re-run the focused suite and the affected runtime journey.

If the finding is actually new gameplay/product scope rather than a regression or migration defect, document it and leave it out of HPA-359.

## File ownership map for discovered defects

| Surface | Primary production owners | Primary tests |
| --- | --- | --- |
| Main Menu / Continue | `scripts/ui/MainMenu.cs`, `scenes/ui/MainMenu.tscn` | `tests/ui/MainMenuTest.cs`, `tests/ui/MainMenuSceneTest.cs` |
| Exploration / HUD / Inventory | `scripts/game/Game.cs`, `scripts/ui/ExplorationHudController.cs`, `scripts/ui/InventoryMenuController.cs` | `tests/game/GameTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/game/GameplayPauseHostTest.cs`, `tests/ui/ExplorationHudControllerTest.cs`, `tests/ui/InventoryMenuControllerTest.cs`, `tests/ui/InventoryMenuSceneTest.cs` |
| NPC / Dialogue / Puzzle | `scripts/ui/NpcInteractionController.cs`, `scripts/ui/DialogueScreenController.cs`, `scripts/ui/PuzzleRiddleScreenController.cs` | `tests/ui/NpcInteractionControllerTest.cs`, `tests/ui/DialogueScreenControllerTest.cs`, `tests/ui/PuzzleRiddleScreenControllerTest.cs` |
| Battle / result | `scripts/ui/BattleManager.cs`, `scenes/ui/BattleScene.tscn` | `tests/ui/BattleManagerTest.cs`, `tests/ui/BattleSceneTest.cs` |
| Pause / nested children | `scripts/game/Game.cs`, `scripts/ui/hosting/UIScreenHost.cs`, `scripts/ui/PauseScreenController.cs` | `tests/game/GameplayPauseHostTest.cs`, `tests/game/GameInputLifecycleTest.cs`, `tests/ui/PauseScreenControllerTest.cs`, host tests under `tests/ui/hosting/` |
| Save/Load / Settings / Prompt | `scripts/ui/SaveLoadScreenController.cs`, `scripts/ui/SettingsMenuController.cs`, `scripts/ui/components/SiriusPrompt.cs` | `tests/ui/SaveLoadScreenControllerTest.cs`, `tests/ui/SaveLoadScreenSceneTest.cs`, `tests/ui/SettingsMenuControllerTest.cs`, prompt tests under `tests/ui/components/` |

## Acceptance

HPA-359 is complete when:

- all three representative journeys succeed on the shipped paths;
- `1280×720` is clean and the selected `640×360` surfaces remain usable;
- keyboard/mouse works end-to-end and one supported gamepad smoke passes;
- topmost Cancel, focus restoration, pause/input/cursor/HUD policy, nested child behavior, and teardown are consistent;
- no normal-flow runtime warning/error, duplicate activation, missing-resource fallback, or stuck UI state is observed;
- any executable obsolete UI path discovered by the audit is removed after its replacement is proven;
- concise current developer docs describe the shipped screen/host architecture;
- focused tests and the full test suite are green;
- `docs/ui/hpa-359/release-validation.md` contains the final evidence and small screenshot set.
