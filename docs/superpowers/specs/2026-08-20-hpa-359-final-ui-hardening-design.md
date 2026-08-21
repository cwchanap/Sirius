# HPA-359 Final Sirius UI Hardening and Release Validation Design

**Issue:** HPA-359 — Final Sirius UI hardening and release validation  
**Date:** 2026-08-20  
**Scope:** One final integration/hardening slice after the Sirius UI migrations

## Context

The concrete Sirius UI migration work is complete. Main has scene-authored production surfaces for Main Menu, Exploration HUD, Inventory, Battle, Pause, Settings, Save/Load, Dialogue, Shop, Healing, Puzzle/Riddle, shared prompts, reward feedback, and the root-local `UIScreenHost` path. HPA-375 and HPA-541 remain optional backlog work.

The final pass should not manually repeat integration behavior that the existing runtime-backed GdUnit suites already prove. `GameplayPauseHostTest` instantiates the real `res://scenes/game/Game.tscn` and already protects the important hosted composition contracts, including:

- Dialogue → Shop: `NpcShopOutcome_HostsAsBlockingScreenWithoutPausingTree` drives the real `village_shopkeeper` route, presses `Browse your wares.`, closes Dialogue, opens exactly one Shop, changes HUD policy from visible to hidden, blocks gameplay without pausing the tree, and restores interaction/HUD state on close.
- Pause children: the hosted Settings and Save/Load cases keep those screens under the same live Pause lease; `HostedSaveLoad_CloseReturnsFocusToSamePause` pins focus restoration to that exact Pause.
- Save overwrite Prompt: the overwrite-Prompt cases keep Save/Load and Pause alive while topmost Cancel dismisses only the Prompt.
- Return to Title: `ReturnToTitle_ClosesUiStackAndRestoresIncomingStateBeforeSceneChange` protects teardown before the scene-change request.

Long Dialogue overflow, Puzzle/Riddle compact/cancel behavior, and corrupted-save prompts also have dedicated automated owners. Repeating those as manual acceptance steps would add time without increasing confidence.

## Remaining validation gaps

HPA-359 is valuable only where the current suite does not fully cover production behavior:

1. **Actual Main Menu ↔ Game scene replacement.** Main Menu tests assert requested scene paths through a test seam; they do not exercise the real `SceneTree.ChangeSceneToFile` transition in a running game window. Likewise, the Return-to-Title host tests protect teardown ordering without performing the real scene replacement.
2. **Movement → encounter → Battle.** `Game.OnPlayerMoved` calls `_gridMap.CheckEncounter(position)` and then `StartBattle(encounter)`, but the battle integration tests enter by calling `GameManager.StartBattle(...)` or another direct seam. Walking into the Ground Floor goblin is the remaining production entry path worth observing.
3. **Real-window visual composition.** Existing scene and runtime-smoke tests render through `SubViewport`; they are deterministic layout owners but do not replace one final visual pass in the actual game window.
4. **Hosted-UI joypad navigation/cancel characterization.** Gameplay actions are intentionally keyboard-bound by default, but hosted controls inherit Godot `ui_*` navigation. Existing tests exercise joypad input locally; HPA-359 should pin the host-level path once without requiring physical hardware.

## Decision

Use the existing automated suites for every covered contract, add one deterministic joypad characterization test, and perform **one narrow real-window walkthrough** for the three production gaps above.

Do not add a new E2E driver, screenshot harness, test host, global UI service, or second `UIScreenHost`.

A clean closeout with no production C# change is valid. Production code changes happen only after a concrete failure is reproduced by the nearest existing owning test.

## Automated ownership

The final baseline includes the existing suites for:

- Main Menu and scene composition: `MainMenuTest`, `MainMenuSceneTest`.
- Real `Game.tscn` host composition: `GameplayPauseHostTest`, `GameInputLifecycleTest`, `GameTest`.
- HUD and Inventory: `ExplorationHudControllerTest`, `InventoryMenuControllerTest`, `InventoryMenuSceneTest`.
- NPC surfaces: `NpcInteractionControllerTest`, `DialogueScreenControllerTest`, `ShopScreenControllerTest`, `HealingScreenControllerTest`.
- Puzzle/Riddle: `PuzzleRiddleScreenControllerTest`.
- Battle/result: `BattleManagerTest`, `BattleSceneTest`.
- Pause/Save/Settings/Prompt: `PauseScreenControllerTest`, `SaveLoadScreenControllerTest`, `SaveLoadScreenSceneTest`, `SettingsMenuControllerTest`, host tests, and prompt tests.
- Art/render sanity: `Hpa374RuntimeSmokeTest`.

When these tests are green, HPA-359 does not manually re-prove their lifecycle assertions.

## One real-window walkthrough

Run the production game at `1280×720` and keep the walkthrough focused on uncovered seams.

### 1. Actual Main Menu → Game transition

- Launch the real `MainMenu.tscn` path.
- Choose **New Game**.
- Verify the actual running scene becomes `Game.tscn`, not merely that a requested path was recorded.
- Confirm Ground Floor entry at the authored new-game start `(8, 50)`.

### 2. Real-window layout checks

- Open Inventory once at `1280×720` and confirm the shipped composition is visually usable.
- Resize to `640×360`; inspect Inventory only, then close it.
- From Pause, open Save, populate one disposable manual slot if needed, reopen Save, and select the occupied slot to display the overwrite Prompt.
- At `640×360`, inspect the nested Save/Prompt composition only. Lifecycle behavior beneath this Prompt remains owned by `GameplayPauseHostTest`.
- Restore `1280×720` before continuing.

### 3. Movement → encounter → Battle

- Starting from FloorGF, move through the real world to the authored goblin at `(24, 45)`.
- Enter the encounter by movement rather than a direct battle/test seam.
- Complete Battle preparation/combat/result and return to exploration.
- Confirm the transition is visually coherent and no runtime warning/error, duplicate activation, or stuck UI state is observed.

### 4. Actual Game → Main Menu transition

- Open Pause and use the production Return-to-Title confirmation flow.
- Verify the actual running scene returns to `MainMenu.tscn`.

Mira/Shop, Heal, Puzzle/Riddle, long Dialogue, corrupted-save Prompt behavior, Pause-child focus restoration, overwrite-parent retention, and topmost Cancel are **not** separate manual acceptance steps; their existing runtime-backed tests remain authoritative.

## Joypad policy

Do not add controller bindings for `toggle_inventory`, `interact`, or `pause_menu`; their configured defaults remain keyboard-only.

Add one deterministic test to `GameplayPauseHostTest` against the real hosted Game scene:

1. open Pause through the configured gameplay action (whose production default is keyboard Escape);
2. inject a joypad navigation event and verify focus moves within the live Pause;
3. inject the existing joypad `ui_cancel` event;
4. verify Pause closes, the tree unpauses, and focus/input state restores to gameplay.

This is a characterization test, not a new controller feature. It replaces the previous physical-controller/N/A manual requirement.

## Viewport and screenshot policy

`SiriusUiMetrics.VerificationViewports` remains the exhaustive automated viewport owner.

HPA-359 manually checks only:

- the single production walkthrough at `1280×720`;
- Inventory at `640×360`;
- the nested Save/overwrite Prompt at `640×360`.

Keep at most six screenshots under `docs/ui/hpa-359/evidence/`. Screenshots should document the **real-window** pass because that is the uncovered visual seam. Existing `Hpa374RuntimeSmokeTest` remains the cheaper deterministic SubViewport render gate; it does not replace the real-window evidence.

## Cleanup policy

After validation, audit the current tracked production paths and current developer-facing status documentation.

### Production audit

Search `scripts/`, `scenes/`, and `project.godot` for retired native-dialog/debug names and `AcceptDialog`/`ConfirmationDialog` usage. Classify every match before deleting anything:

- remove only executable obsolete paths whose migrated replacement is already protected;
- retain useful historical comments such as “replaces the former AcceptDialog”;
- retain intentional dynamic C# rows such as Dialogue choices and Shop/Inventory catalogue entries;
- retain input compatibility code that still serves current behavior.

Current tracked `main` contains no `.uid` files in the recursive Git tree, including none of the legacy `.cs.uid` paths named in review. Do not add tracked `.uid` deletion work unless execution against the current branch proves a tracked orphan exists. Local ignored/generated files are local workspace hygiene, not HPA-359 PR scope.

### Current-documentation corrections

Update only current claims that are factually stale:

- `CLAUDE.md`: current Battle/screen flow, current UI controller examples, and hosted Save/Prompt ownership.
- `docs/ui/hpa-376/ui-lifecycle-contract.md`: only the introductory configured-Cancel paragraph so it describes hosted `ui_cancel` ownership rather than treating `AcceptDialog/ui_close_dialog` as the current presentation path. Leave the flow matrix and historical evidence unchanged.
- `docs/PRD.md`: make the following current-status corrections explicit:
  - update the top Settings summary-table row to Complete;
  - update the top NPC system note to current hosted Dialogue/Shop/Heal controllers;
  - update the top Settings system note so it no longer claims the UI is missing;
  - update Feature 3.2's current Implementation Status sentence to current hosted NPC UI names;
  - update the Settings section heading/current Implementation Status and remove only “Not Yet Implemented” bullets that falsely say the Settings UI/Main Menu/Pause access are absent;
  - update the Quarter 3 Settings roadmap row from “UI scene not yet built” to Complete.

Keep these historical PRD records unchanged:

- the `~65%` overall-completion figure, which remains the April snapshot and is not recomputed by HPA-359;
- the v1.2 April change-log row stating that the Settings UI was still missing at that historical version;
- unrelated feature requirements, schedules, metrics, and roadmap rows.

## Defect handling

For any defect observed by the automated baseline or the one real-window walkthrough:

1. identify the existing owning controller/scene and nearest test suite;
2. add the smallest useful regression test and confirm it reproduces the problem;
3. make the minimal production fix;
4. rerun the focused owner suite;
5. rerun only the affected portion of the real-window walkthrough when the changed production files can affect it.

Do not create a follow-up PR for a direct HPA-359 regression; keep the ticket as one PR. New product/gameplay scope is documented and left out.

## Evidence and acceptance

Create `docs/ui/hpa-359/release-validation.md` containing:

- exact build/focused/full-suite commands and results;
- the new hosted joypad characterization result;
- one real-window walkthrough result covering actual Main Menu → Game, movement → Battle, and Game → Main Menu transitions;
- `640×360` Inventory and nested Save/Prompt visual observations;
- runtime warning/error observations;
- audit/doc-correction results;
- links to at most six real-window screenshots.

HPA-359 is complete when the automated owners are green, the one uncovered production walkthrough is clean, the joypad host characterization is pinned, any proven obsolete executable path is removed, current-status docs are corrected, and any reproduced defect has a focused regression test plus minimal fix.