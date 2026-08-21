# HPA-359 Final Sirius UI Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the Sirius UI migration with deterministic automated coverage plus one narrow real-window walkthrough for the production seams that are not already protected.

**Architecture:** Keep the shipped root-local `UIScreenHost`, scene-authored screens, Theme/components, and controller ownership unchanged. Existing runtime-backed GdUnit suites remain authoritative for Dialogue→Shop/Heal, Pause children, overwrite Prompt retention, focus restoration, topmost Cancel, long Dialogue, Puzzle/Riddle, corrupt-save prompts, and normal hosted lifecycle. HPA-359 adds one host-level joypad characterization and manually checks only actual scene replacement, movement→encounter→Battle, and real-window composition. Any production defect is reproduced test-first in its existing owner and fixed minimally in this same HPA-359 PR.

**Tech Stack:** Godot 4.6.2, C#/.NET 8.0, GdUnit4, existing Sirius Theme/components/`UIScreenHost`.

**Spec:** `docs/superpowers/specs/2026-08-20-hpa-359-final-ui-hardening-design.md`

## Global Constraints

- [ ] Keep all HPA-359 work in one PR.
- [ ] Do not implement HPA-375 or HPA-541.
- [ ] Do not add a global UI singleton, second host, E2E driver, screenshot harness, compatibility layer, or release framework.
- [ ] Do not change gameplay/domain rules while hardening presentation.
- [ ] A clean closeout with no production C# change is valid.
- [ ] Any production fix must start with the narrowest useful failing regression in the owning existing suite.
- [ ] Do not manually repeat a contract already protected by a runtime-backed automated test merely to create evidence.
- [ ] Do not add gameplay joypad bindings; the new controller coverage is hosted-UI characterization only.
- [ ] Do not delete historical comments or intentional dynamic C# UI rows such as Dialogue choices or Shop/Inventory catalogue entries.
- [ ] Do not rewrite the HPA-376 flow matrix, historical evidence, PRD feature requirements, or the April PRD change log.
- [ ] Keep final screenshot evidence to at most six real-window images.

---

## Task 1: Establish the automated release baseline and evidence scaffold

**Files:**
- Create: `docs/ui/hpa-359/release-validation.md`
- Reference: `scripts/ui/theme/SiriusUiMetrics.cs`
- Reference: existing test suites only

**Interfaces:**
- Consumes: current `main` UI/game behavior.
- Produces: factual baseline results and the evidence structure used by Tasks 2–5.

- [ ] **Step 1: Build the current branch**

```bash
dotnet build Sirius.sln
```

Expected: build succeeds with no new compile errors.

- [ ] **Step 2: Run the focused release baseline**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~MainMenuTest|FullyQualifiedName~MainMenuSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~PuzzleRiddleScreenControllerTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SaveLoadScreenControllerTest|FullyQualifiedName~SaveLoadScreenSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~UIScreenHost|FullyQualifiedName~SiriusPrompt|FullyQualifiedName~Hpa374RuntimeSmokeTest"
```

Expected: green. Record exact pass/fail/skip totals. If a pre-existing failure appears, classify it before editing production code.

- [ ] **Step 3: Record which manual checks are intentionally reused from automation**

In the evidence document, cite the current automated owners rather than manually re-running them:

- `GameplayPauseHostTest.NpcShopOutcome_HostsAsBlockingScreenWithoutPausingTree` — real `Game.tscn` Dialogue→Shop composition, HUD visible→hidden, gameplay block, no tree pause, restore on close.
- `GameplayPauseHostTest.HostedSaveLoad_CloseReturnsFocusToSamePause` and adjacent Pause-child cases — Save/Load/Settings child ownership and same-Pause focus restoration.
- `GameplayPauseHostTest` overwrite-Prompt cases — topmost Cancel closes Prompt while retaining Save/Load and Pause.
- `GameplayPauseHostTest.ReturnToTitle_ClosesUiStackAndRestoresIncomingStateBeforeSceneChange` — teardown before scene-change request.
- `DialogueScreenControllerTest.CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice` — long compact Dialogue.
- `GameTest.CorruptedSave_*` — corrupt-save hosted Prompt behavior.
- `PuzzleRiddleScreenControllerTest` plus `GameInputLifecycleTest` — compact/cancel Puzzle/Riddle behavior.

Do not turn these citations into additional manual acceptance steps.

- [ ] **Step 4: Create `docs/ui/hpa-359/release-validation.md`**

Use this structure:

```markdown
# HPA-359 Sirius UI Release Validation

## Automated baseline
- Build:
- Focused release suites:
- Full suite:

## Existing runtime-backed coverage reused
- Dialogue → Shop / Heal:
- Pause children / focus restoration:
- Save overwrite Prompt retention / topmost Cancel:
- Long Dialogue / Puzzle / corrupt-save:

## Hosted joypad characterization
- Result:

## One production walkthrough
- Actual Main Menu → Game scene replacement:
- FloorGF new-game start `(8, 50)`:
- Movement → goblin `(24, 45)` → Battle:
- Actual Game → Main Menu scene replacement:

## Real-window visual checks
- Inventory 1280×720:
- Inventory 640×360:
- Battle/result 1280×720:
- Save/overwrite Prompt 640×360:

## Runtime observations
- Warnings/errors:
- Duplicate activation:
- Stuck focus/pause/input/cursor/HUD state:

## Legacy-path and documentation audit
- Executable leftovers:
- CLAUDE.md:
- docs/PRD.md:
- HPA-376 Cancel intro:

## Evidence screenshots
- paths:
```

Leave result fields blank until the corresponding observation is made; do not pre-declare success.

- [ ] **Step 5: Commit the evidence scaffold**

```bash
git add docs/ui/hpa-359/release-validation.md
git commit -m "docs: start HPA-359 release validation"
```

---

## Task 2: Run one real-window production walkthrough for uncovered seams

**Primary production owners if a defect is found:**
- `scripts/ui/MainMenu.cs`
- `scenes/ui/MainMenu.tscn`
- `scripts/game/Game.cs`
- `scripts/game/GridMap.cs`
- `scripts/ui/InventoryMenuController.cs`
- `scenes/ui/InventoryMenu.tscn`
- `scripts/ui/BattleManager.cs`
- `scenes/ui/BattleScene.tscn`
- `scripts/ui/SaveLoadScreenController.cs`
- `scenes/ui/SaveLoadScreen.tscn`
- `scripts/ui/components/SiriusPrompt.cs`

**Primary tests if a defect is found:**
- `tests/ui/MainMenuTest.cs`
- `tests/ui/MainMenuSceneTest.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/ui/BattleSceneTest.cs`
- `tests/ui/SaveLoadScreenControllerTest.cs`
- `tests/ui/SaveLoadScreenSceneTest.cs`
- prompt tests under `tests/ui/components/`

**Interfaces:**
- Consumes: authored FloorGF start `(8, 50)` and first goblin `(24, 45)` from `Floor0Layout` / `FloorGF.tscn`.
- Produces: one real-window observation covering actual scene replacement, real movement encounter entry, and visual composition.

- [ ] **Step 1: Launch at `1280×720` and exercise the actual Main Menu → Game replacement**

Run the production game, not a SubViewport fixture. From the real Main Menu choose **New Game**.

Verify only the uncovered seam:

- the running scene actually becomes `Game.tscn`;
- Ground Floor starts at the authored new-game position `(8, 50)`;
- no normal-flow warning/error is emitted during the replacement.

Do not re-check Main Menu focus/action contracts already owned by `MainMenuTest` / `MainMenuSceneTest`.

- [ ] **Step 2: Inspect Inventory in the real window**

At `1280×720`, open Inventory and capture its final production appearance.

Resize the real window to `640×360` and inspect Inventory again. Verify only visible usability: content is present, controls are reachable, and nothing is clipped into an unusable state. Restore `1280×720` after the compact check.

Lifecycle/HUD/pause/focus behavior remains owned by the automated host and Inventory suites; do not manually duplicate their assertions.

- [ ] **Step 3: Exercise movement → encounter → Battle through FloorGF**

Move through the real world to the authored first goblin at `(24, 45)` so `Game.OnPlayerMoved` reaches `_gridMap.CheckEncounter(position)` and opens Battle through the production encounter path.

Complete Battle preparation/combat/result and return to exploration.

Verify only:

- movement, not a direct debug/test call, triggers Battle;
- the Battle/result composition is visually usable in the real window;
- the flow returns to exploration without a new runtime warning/error, duplicate activation, or stuck presentation state.

Capture one Battle/result screenshot at `1280×720`.

- [ ] **Step 4: Inspect the real nested Save/overwrite Prompt at `640×360`**

From Pause:

1. open Save;
2. choose one disposable manual slot and save once if it is empty;
3. reopen Save and select that occupied slot to open the destructive overwrite Prompt;
4. resize to `640×360` and inspect the real nested composition;
5. restore `1280×720`, close the Prompt/Save flow through normal UI, and return to Pause.

Verify only visual usability at compact size. Parent retention, topmost Cancel, Pause lease, and focus restoration are already runtime-backed automated contracts.

- [ ] **Step 5: Exercise the actual Game → Main Menu replacement**

Use Pause → Return to Title and its production confirmation flow.

Verify the running scene actually becomes `MainMenu.tscn` and no new runtime warning/error is emitted during replacement. Do not manually repeat the already-covered host teardown ordering assertions.

- [ ] **Step 6: If any uncovered seam fails, add focused RED before production changes**

Choose the nearest owning suite from the file map above. The regression should reproduce the specific defect, not recreate the entire manual journey.

Run only that class/test and confirm failure for the intended reason before editing production code.

- [ ] **Step 7: Apply the minimal GREEN fix only when Step 6 reproduced a defect**

Edit the smallest owning production surface. Preserve current host/domain ownership and do not add a generic helper for a one-off closeout defect.

Re-run the focused failing test plus the relevant Task 1 focused classes.

- [ ] **Step 8: Record evidence and commit**

Use at most these five real-window screenshots:

- `docs/ui/hpa-359/evidence/inventory-1280x720.png`
- `docs/ui/hpa-359/evidence/inventory-640x360.png`
- `docs/ui/hpa-359/evidence/battle-result-1280x720.png`
- `docs/ui/hpa-359/evidence/save-overwrite-prompt-640x360.png`
- optional `docs/ui/hpa-359/evidence/main-menu-return-1280x720.png`

Update `release-validation.md` with the exact walkthrough observations.

```bash
git add docs/ui/hpa-359 tests scripts scenes
git commit -m "test: validate uncovered Sirius UI production seams"
```

If no test/production files changed, keep the commit limited to evidence.

---

## Task 3: Pin hosted joypad navigation and Cancel in the real Game host

**Files:**
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify only if the new test exposes a real defect: `scripts/game/Game.cs` and/or existing host/focus owner

**Interfaces:**
- Consumes: existing `pause_menu` opening path, Godot `ui_down`/`ui_cancel`, `UIScreenHost`, `PauseScreenController`.
- Produces: one deterministic regression proving hosted joypad navigation/cancel without physical hardware or new gameplay bindings.

- [ ] **Step 1: Add a real-host characterization test**

Add a test named along these lines:

```csharp
[TestCase]
public async Task HostedPause_JoypadNavigationAndCancelRestoreGameplay()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");

    var joyDownBinding = new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.DpadDown
    };
    var joyCancelBinding = new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.B
    };

    InputMap.ActionAddEvent("ui_down", joyDownBinding);
    InputMap.ActionAddEvent("ui_cancel", joyCancelBinding);
    try
    {
        // Open through the configured gameplay action. The production default
        // for pause_menu is keyboard Escape; no joypad gameplay binding is added.
        _viewport!.PushInput(new InputEventAction
        {
            Action = "pause_menu",
            Pressed = true
        });
        await AwaitFrames(2);

        var pause = GetPrivateField<PauseScreenController>(_game, "_pauseScreen");
        var initialFocus = _viewport.GuiGetFocusOwner();
        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
        AssertThat(initialFocus).IsNotNull();

        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.DpadDown,
            Pressed = true
        });
        await AwaitFrames(2);

        var movedFocus = _viewport.GuiGetFocusOwner();
        AssertThat(movedFocus).IsNotNull();
        AssertThat(movedFocus).IsNotEqual(initialFocus);
        AssertThat(pause.IsAncestorOf(movedFocus)).IsTrue();

        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.B,
            Pressed = true
        });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
        AssertThat(tree.Paused).IsFalse();
        AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
    }
    finally
    {
        InputMap.ActionEraseEvent("ui_down", joyDownBinding);
        InputMap.ActionEraseEvent("ui_cancel", joyCancelBinding);
    }
}
```

Use the exact existing helper names/types in the file when implementing. The important contract is configured gameplay-action open → joypad focus movement → joypad Cancel → gameplay restored.

This is characterization coverage, so it may pass immediately on current production. Do not manufacture a production RED merely to justify the test.

- [ ] **Step 2: Run the new test**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest.HostedPause_JoypadNavigationAndCancelRestoreGameplay"
```

Expected: PASS on healthy current production. If it fails, treat that failure as the RED regression and investigate the existing host/focus owner before changing production code.

- [ ] **Step 3: Re-run the host/input suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~UIScreenHost"
```

Expected: green.

- [ ] **Step 4: Record and commit the characterization**

Update the Hosted joypad characterization section in `release-validation.md`.

```bash
git add tests/game/GameplayPauseHostTest.cs docs/ui/hpa-359/release-validation.md
git commit -m "test: cover hosted joypad navigation and cancel"
```

If the characterization exposed a real production defect, explicitly add only the changed production owner(s) to the same commit after the failing test proves the need.

---

## Task 4: Audit obsolete paths and correct current documentation

**Files:**
- Modify: `CLAUDE.md`
- Modify: `docs/PRD.md`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: `scripts/`, `scenes/`, `project.godot`
- Conditionally modify: exact obsolete executable files only if the audit proves they are tracked/live

**Interfaces:**
- Consumes: shipped scene/host architecture and current repository tree.
- Produces: current developer/status documentation and a classified legacy-path audit.

- [ ] **Step 1: Audit current production content**

```bash
rg -n 'DialogueDialog|SaveLoadDialog|SaveOverwriteConfirmationController|DraggablePanel|Player HUD|Settings menu coming soon' scripts scenes project.godot CLAUDE.md docs/PRD.md
rg -n 'AcceptDialog|ConfirmationDialog' scripts scenes project.godot docs/ui/hpa-376/ui-lifecycle-contract.md
```

Classify matches before editing:

- executable obsolete path → protect replacement first, then remove;
- useful historical comment → retain;
- current input-compatibility code → retain;
- current documentation naming a deleted path → correct.

At planning time the tracked recursive `main` tree contains **no `.uid` files**, including none of the legacy `.cs.uid` names reported in review. HPA-359 therefore has no planned `.uid` deletion. If execution occurs in a workspace with ignored/generated orphan UIDs, treat them as local hygiene unless they are tracked PR content.

- [ ] **Step 2: Correct `CLAUDE.md` current architecture**

Make only the current-path corrections:

- Battle is the scene-authored full-screen hosted flow, not a modal dialog.
- Scene flow uses hosted UI/screens rather than “Battle dialogs”.
- `scripts/ui` examples name current controllers such as `DialogueScreenController`, `ShopScreenController`, `HealingScreenController`, `SaveLoadScreenController`, `PauseScreenController`, and `UIScreenHost` rather than deleted dialog/controller names.
- Save overwrite/recoverable feedback is owned by the shared hosted `SiriusPrompt` path.
- Fix other stale statements only when they occur in the same edited current-architecture paragraphs.

Do not rewrite unrelated gameplay guidance.

- [ ] **Step 3: Correct only the explicit current-status locations in `docs/PRD.md`**

Change these current claims:

1. Top implementation summary table: change the Settings Menu row from `⚠️ Partial (60%)` to `✅ Complete`.
2. Top NPC system note: replace `DialogueDialog`/old screen names with current hosted `DialogueScreenController`, `ShopScreenController`, and `HealingScreenController` ownership.
3. Top Settings system note: remove the claim that the UI scene is missing/Main Menu is a stub; state that the scene-authored Settings UI is available from Main Menu and hosted Pause.
4. Feature 3.2 NPC current `Implementation Status` sentence: use the current hosted Dialogue/Shop/Heal controller names.
5. Feature 4.1 Settings heading/current `Implementation Status`: mark the currently scoped Settings Menu complete; remove only “Not Yet Implemented” bullets that falsely claim the Settings scene, Main Menu access, or Pause access are absent.
6. Quarter 3 roadmap row: change `Settings Menu | ⚠️ Partial — backend/persistence complete; UI scene not yet built` to a Complete current status.

Keep these records unchanged:

- `**Overall completion: ~65% of PRD scope**` — retain as the historical April snapshot; do not recalculate project-wide completion in HPA-359.
- the v1.2 April change-log row saying the UI scene was still missing at that historical version;
- unrelated feature requirements, success metrics, schedules, and roadmap rows.

If needed, add one concise note near the summary clarifying that HPA-359 applies later status corrections without recomputing the April aggregate. Do not turn this into a PRD refresh.

- [ ] **Step 4: Correct only the HPA-376 configured-Cancel introduction**

Update the introductory paragraph that currently presents `AcceptDialog/ui_close_dialog` as the active runtime contract. Describe the shipped rule instead:

- hosted screens and prompts route configured Cancel through `ui_cancel` / `UIScreenHost`;
- Settings still preserves its required binding synchronization behavior where applicable;
- `ui_close_dialog`/AcceptDialog wording is historical compatibility context, not the current migrated-screen owner.

Do not edit the flow lifecycle matrix or historical evidence rows.

- [ ] **Step 5: Remove executable obsolete production code only if the audit proves it**

Before deleting any source/scene/reference, run or add the nearest existing test showing the shipped replacement owns that production path. Then remove the proven dead path and rerun its owner suite.

Do not manufacture a deletion to satisfy HPA-359.

- [ ] **Step 6: Repeat the content audit and record the result**

Re-run the Step 1 commands and record:

- executable leftovers removed, if any;
- historical/compatibility matches intentionally retained;
- current-documentation corrections made;
- confirmation that no tracked `.uid` cleanup was required unless the current branch actually contained one.

- [ ] **Step 7: Commit cleanup/documentation**

```bash
git add CLAUDE.md docs/PRD.md docs/ui/hpa-376/ui-lifecycle-contract.md docs/ui/hpa-359
git commit -m "docs: align Sirius UI closeout guidance"
```

If the audit proves an executable leftover, explicitly stage only that dead source/scene/reference and its protecting test in the same commit.

---

## Task 5: Final verification and conditional closeout

**Files:**
- Modify: `docs/ui/hpa-359/release-validation.md`
- Verify: all HPA-359 changed files

**Interfaces:**
- Consumes: Tasks 1–4 final state.
- Produces: final factual release evidence; no new architecture.

- [ ] **Step 1: Re-run the focused release baseline**

Use Task 1 Step 2's exact command.

Expected: green, including the new hosted joypad characterization.

- [ ] **Step 2: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local
```

Record exact pass/fail/skip totals. Investigate new HPA-359 warnings/errors; historical test-runner noise may be recorded when unchanged.

- [ ] **Step 3: Re-run manual evidence only when production changes can affect it**

Do **not** repeat Task 2 merely because this is final verification.

Use this gate:

- If Tasks 3–4 changed only tests/docs/evidence, cite the already-recorded Task 2 walkthrough and do not rerun it.
- If a production fix changed Main Menu/Game scene replacement, rerun only the affected scene-transition portion.
- If a production fix changed encounter/Battle ownership, rerun only movement→goblin→Battle/result.
- If a production fix changed Inventory/Save/Prompt/theme layout, rerun only the affected real-window viewport check.
- If multiple production areas changed, rerun the corresponding affected portions only.

Do not recapture screenshots unless the visible result changed.

- [ ] **Step 4: Finish `release-validation.md`**

Ensure it contains:

- exact build/focused/full-suite results;
- existing automated coverage reused instead of manual duplication;
- hosted joypad characterization result;
- actual Main Menu→Game and Game→Main Menu observations;
- movement→encounter→Battle observation;
- real-window `1280×720` and targeted `640×360` visual observations;
- runtime warnings/errors and any defects/fixes with their owning regression test;
- audit/documentation disposition;
- no more than six screenshot paths.

- [ ] **Step 5: Review the final diff for scope**

```bash
git diff --check
git status --short
git diff --stat main...HEAD
git diff main...HEAD -- docs/superpowers docs/ui/hpa-359 CLAUDE.md docs/PRD.md scripts scenes tests project.godot
```

Confirm:

- exactly one HPA-359 PR;
- no HPA-375/HPA-541 work;
- no new generic architecture or new gameplay/controller bindings;
- every production edit maps to a reproduced HPA-359 defect or proven obsolete executable path;
- manual evidence covers only gaps not already automated;
- evidence matches the final code state.

- [ ] **Step 6: Commit final evidence if needed**

```bash
git add docs/ui/hpa-359
git commit -m "docs: record HPA-359 release validation"
```

Do not create an empty commit.

## Expected final change shape

The minimum successful implementation can contain no production C# change. In that case HPA-359 still delivers:

- focused and full-suite verification;
- one additional deterministic real-host joypad characterization test;
- one narrow real-window pass for actual scene replacement, movement→Battle, and visual composition;
- at most six real-window screenshots;
- a classified obsolete-path audit;
- narrow corrections to current `CLAUDE.md`, PRD status claims, and the HPA-376 Cancel introduction.

If a real defect is found, keep its focused regression and minimal fix inside this same PR.