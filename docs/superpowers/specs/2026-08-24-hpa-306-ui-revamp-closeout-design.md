# HPA-306 Sirius UI Revamp Closeout Design

**Issue:** HPA-306 — Revamp Sirius UI architecture and player-facing screen layouts  
**Date:** 2026-08-24  
**Scope:** Close the completed Sirius UI revamp umbrella on current `main` without creating another UI subsystem or repeating already-owned release work.

## Context

HPA-306 is an umbrella and completion checkpoint, not another screen implementation ticket. Its required vertical slices are complete: visual language/art, lifecycle baseline, Theme/components, root-local `UIScreenHost`, Main Menu, Exploration HUD, Pause, Settings, Save/Load, Inventory parity, Battle, shared prompts, Dialogue, Shop/Healing, Puzzle/Riddle, reward feedback, and HPA-359 final release validation.

The two items that HPA-306 listed as optional backlog were also completed afterward:

- HPA-375 — inventory details/comparison/filter/sort, merged in PR #46;
- HPA-541 — persisted Reduced Motion and production motion policy, merged in PR #47.

HPA-359 already owns the expensive final production walkthrough and durable evidence in `docs/ui/hpa-359/release-validation.md`. That walkthrough proved the real Main Menu → Game scene replacement, authored FloorGF entry, movement → goblin → Battle/result, Game → Main Menu replacement, compact Inventory, compact Save/overwrite Prompt, hosted joypad Cancel, and the final legacy-path/documentation audit.

The only material closeout delta after HPA-359 is therefore the code changed by HPA-375 and HPA-541. HPA-375 changed Inventory presentation and behavior. HPA-541 changed Settings persistence/presentation plus exploration and Battle motion. Re-running the entire HPA-359 walkthrough would mostly reproduce evidence that neither follow-up touched.

## Approaches considered

### 1. Delta closeout on current `main` — recommended

Reuse HPA-359 for already-proven production seams, run fresh automated validation over the HPA-375/HPA-541 owners and the full solution, and perform one Inventory-only real-window check because HPA-375 changed the exact layout HPA-359 previously captured.

This gives current-head confidence while keeping the closeout proportional to the actual post-validation change set.

### 2. Repeat the full HPA-359 release walkthrough

Repeat Main Menu → Game, movement → Battle, Save/Prompt, Inventory, Return to Title, screenshots, and all runtime observations.

This is higher cost with little additional signal. HPA-375 did not change scene replacement, Battle entry, Save/Prompt, Dialogue/Shop/Heal, or Return-to-Title ownership. HPA-541 changed motion behavior but has deterministic unit/scene/integration owners. Replaying every HPA-359 manual seam is not justified.

### 3. Add a new end-to-end/release-validation framework

Build a reusable driver, screenshot harness, or umbrella UI certification layer.

This is explicitly out of scope. HPA-359 already rejected this shape, and HPA-306's delivery guardrails require reuse of the existing GdUnit/runtime-backed owners rather than a new framework.

## Decision

Use the delta closeout.

A clean HPA-306 closeout with **no production C# or scene change** is the expected success case. Production code changes are allowed only when current-head validation exposes a concrete defect and the defect is reproduced first in its nearest existing owner suite.

No new UI feature, architecture layer, compatibility path, or follow-up polish is required merely to make the umbrella look active.

## Evidence ownership

### Reuse HPA-359 without replaying it

`docs/ui/hpa-359/release-validation.md` remains authoritative for:

- actual `MainMenu.tscn` → `Game.tscn` scene replacement;
- authored Ground Floor start `(8, 50)`;
- movement into the first goblin at `(24, 45)` and production Battle/result entry;
- actual Game → Main Menu replacement;
- hosted Pause/Save/Settings/Prompt lifecycle and joypad Cancel characterization;
- compact Save/overwrite Prompt real-window usability;
- legacy desktop-dialog/debug-path audit;
- final HPA-359 runtime warning/error observations.

Do not recapture or manually replay those contracts unless a new defect in the HPA-375/HPA-541 delta can affect them.

### Fresh HPA-375 ownership

PR #46 changed only:

- `scenes/ui/InventoryMenu.tscn`;
- `scripts/ui/InventoryMenuController.cs`;
- `tests/ui/InventoryMenuControllerTest.cs`;
- `tests/ui/InventoryMenuSceneTest.cs`;
- its design/plan documents.

Fresh closeout validation must therefore cover the Inventory controller and scene suites. Because the authored Inventory layout itself changed after HPA-359's real-window captures, HPA-306 adds one narrow real-window Inventory observation at the primary and minimum viewports.

### Fresh HPA-541 ownership

PR #47 changed:

- Settings data/manager/controller/scene;
- `Game`, `GridMap`, `PlayerDisplay`, `EnemySpawn`;
- `BattleManager`;
- the matching Settings, world, Game, Battle, and scene tests.

Fresh closeout validation covers those deterministic owners plus the real Game host integration. No extra manual motion certification is required: the HPA-541 design deliberately owns reduced motion through deterministic Settings persistence, world propagation, frame-reset, camera, Battle tween, and real-scene visibility tests.

## Current-head automated validation

Run all of the following against the HPA-306 branch after rebasing/starting from current `main`:

1. `dotnet build Sirius.sln`;
2. one focused HPA-375/HPA-541 regression set covering:
   - `InventoryMenuControllerTest`;
   - `InventoryMenuSceneTest`;
   - `SettingsDataTest`;
   - `SettingsManagerTest`;
   - `SettingsMenuControllerTest`;
   - `SettingsMenuSceneTest`;
   - `GridMapTest`;
   - `PlayerDisplayTest`;
   - `EnemySpawnTest`;
   - `GameTest`;
   - `GameplayPauseHostTest`;
   - `BattleManagerTest`;
   - `BattleSceneTest`;
3. the full `dotnet test Sirius.sln --settings test.runsettings.local` suite;
4. `git diff --check`.

The focused set is a fast diagnostic gate. The full suite is the completion gate because HPA-306 is the umbrella and its definition of done includes the shared lifecycle/domain contracts, not only the two post-HPA-359 filesets.

## One real-window delta check

Only Inventory needs a fresh manual visual check because HPA-375 changed the layout after HPA-359.

Use the production game, not a SubViewport fixture:

### `1280×720`

- open Inventory through the real gameplay path;
- verify the standard Character / Items / Details composition is visually usable;
- select an item and verify selection alone does not mutate inventory/equipment state;
- verify Details content and its contextual action are reachable;
- verify filter/sort controls are visible and usable;
- verify Close remains reachable.

### `640×360`

- verify compact Inventory remains usable;
- navigate the compact Equipment / Items / Skills / Details pages;
- verify Details is reachable explicitly rather than being forced on selection;
- verify the selected item's actionable Details state remains understandable;
- verify Close and compact navigation remain reachable without unusable clipping.

Do not turn this into another screenshot or E2E harness. Record the observation in the HPA-306 closeout note. New screenshots are unnecessary unless they help explain a reproduced defect.

## Closeout record

Implementation creates one concise durable record at:

`docs/ui/hpa-306/closeout.md`

It records:

- HPA-306 child/optional-ticket completion status;
- the HPA-359 evidence intentionally reused;
- exact current-head build/focused/full-suite results;
- the two Inventory real-window observations;
- any defect and its focused regression/fix, if one was required;
- the final recommendation to close or keep HPA-306 open.

Do not rewrite `docs/ui/hpa-359/release-validation.md` or recalculate the historical April completion snapshot in `docs/PRD.md` merely for this umbrella closeout.

## Defect handling

If validation exposes a real defect:

1. identify the nearest existing owner suite;
2. add or tighten the smallest failing regression if the current failure is not already specific enough;
3. confirm RED for the intended reason;
4. make the smallest production fix in the existing owner;
5. rerun the focused owner suite;
6. rerun the HPA-306 focused set and full suite;
7. repeat only the affected part of the Inventory real-window check when the changed production files can affect it.

Keep the fix in the same HPA-306 PR. Do not introduce a reusable abstraction for a one-off closeout defect. If the finding is actually new product scope rather than a regression, record it separately and do not expand HPA-306 to absorb it.

## Definition-of-done mapping

HPA-306 can close when the following mapping is satisfied:

- **Shared launch/exploration/interaction/battle/inventory/pause/save/settings journey:** HPA-359 real-window evidence plus current-head full suite; Inventory gets the post-HPA-375 delta check.
- **No normal debug/desktop-window final presentation:** HPA-359 legacy-path audit remains authoritative; neither HPA-375 nor HPA-541 reintroduced those paths.
- **One local `UIScreenHost` per Main Menu/Game root:** existing host/Game/Main Menu tests remain green.
- **Explicit pause/input/cursor/HUD/focus/Cancel ownership:** existing runtime-backed host/controller suites remain green.
- **Domain behavior preserved:** full suite plus the HPA-375/HPA-541 focused owners remain green.
- **Primary/minimum viewports usable with deterministic focus:** HPA-359 evidence plus current Inventory scene tests and the two fresh Inventory real-window observations.
- **No duplicate activation, stuck input, orphaned presentation, or missing-resource regression:** full suite remains green; no new issue appears in the delta manual check.
- **Focused gameplay/persistence/host/lifecycle tests pass:** covered by the focused current-head gate and full suite.
- **Obsolete paths removed after replacement:** HPA-359 audit remains the owner; post-HPA-359 changes do not add compatibility paths.

After the closeout record is green and the HPA-306 PR is ready to merge, update Linear HPA-306 with the final evidence summary and mark it Done. Do not change the broader Sirius Linear project state as part of this ticket.

## Non-goals

- No new screen, modal, host kind, UI service, presenter, view-model, navigation framework, or second host.
- No new E2E driver, screenshot framework, release framework, or manual certification matrix.
- No replay of the complete HPA-359 production walkthrough without a delta-specific reason.
- No additional inventory, combat, settings, motion, save, dialogue, shop, puzzle, or reward feature work.
- No compatibility work for development saves or obsolete UI paths.
- No broad PRD rewrite or project-wide completion recalculation.
- No Linear project-status change; HPA-306 issue completion is the only Linear closeout action.

## Risks and mitigations

### HPA-359's Inventory screenshot is stale after HPA-375

Mitigation: perform exactly one fresh real-window Inventory check at `1280×720` and `640×360`; do not repeat unrelated HPA-359 manual journeys.

### HPA-541 touched several production owners after the final release walkthrough

Mitigation: run its deterministic Settings/world/Game/Battle owner suites plus `GameplayPauseHostTest`, then require the full suite on current `main`.

### Umbrella closeout grows into another feature pass

Mitigation: defects are test-first regressions only. New product ideas remain outside HPA-306.
