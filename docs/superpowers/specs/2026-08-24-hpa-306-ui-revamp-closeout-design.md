# HPA-306 Sirius UI Revamp Closeout Design

**Issue:** HPA-306 — Revamp Sirius UI architecture and player-facing screen layouts  
**Date:** 2026-08-24  
**Scope:** Close the completed Sirius UI revamp umbrella on current `main` without creating another UI subsystem or repeating already-owned release work.

## Context

HPA-306 is an umbrella and completion checkpoint, not another screen implementation ticket. Its required vertical slices are complete: visual language/art, lifecycle baseline, Theme/components, root-local `UIScreenHost`, Main Menu, Exploration HUD, Pause, Settings, Save/Load, Inventory parity, Battle, shared prompts, Dialogue, Shop/Healing, Puzzle/Riddle, reward feedback, and HPA-359 final release validation.

The two items HPA-306 listed as optional backlog were also completed afterward:

- HPA-375 — inventory details/comparison/filter/sort, merged in PR #46;
- HPA-541 — persisted Reduced Motion and production motion policy, merged in PR #47.

HPA-359 already owns the expensive production walkthrough and durable evidence in `docs/ui/hpa-359/release-validation.md`: actual Main Menu → Game scene replacement, authored FloorGF entry, movement → goblin → Battle/result, Game → Main Menu replacement, compact Inventory, compact Save/overwrite Prompt, hosted joypad Cancel, and the legacy-path/documentation audit.

The only material closeout delta after HPA-359 is the code changed by HPA-375 and HPA-541. HPA-375 changed Inventory presentation/behavior. HPA-541 changed Settings persistence/presentation plus exploration and Battle motion. Re-running the complete HPA-359 walkthrough would mostly reproduce evidence that neither follow-up touched.

## Approaches considered

### 1. Delta closeout on current `main` — selected

Reuse HPA-359 for already-proven production seams, run fresh automated validation over the HPA-375/HPA-541 owners and the full solution, and perform one Inventory-only real-window composition check because HPA-375 changed the exact layout HPA-359 previously captured.

### 2. Repeat the full HPA-359 release walkthrough

Repeat Main Menu → Game, movement → Battle, Save/Prompt, Inventory, Return to Title, screenshots, and runtime observations. This costs more without materially increasing confidence: HPA-375 did not change the non-Inventory seams, and HPA-541 has deterministic Settings/world/Game/Battle owners.

### 3. Add a new end-to-end/release-validation framework

Build a reusable driver, screenshot harness, or umbrella UI certification layer. This is explicitly out of scope and contradicts the existing HPA-359/HPA-306 reuse decisions.

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

Do not manually replay those contracts unless a new defect in the HPA-375/HPA-541 delta can affect them.

HPA-359 also records compact Inventory as usable at `640×360` even though section-heading chrome is tight/clipped. HPA-306 inherits that disposition: heading-chrome clipping alone is **not** a closeout defect. It becomes blocking only if required content or controls become unreachable/unusable.

### Fresh HPA-375 ownership

PR #46 changed only:

- `scenes/ui/InventoryMenu.tscn`;
- `scripts/ui/InventoryMenuController.cs`;
- `tests/ui/InventoryMenuControllerTest.cs`;
- `tests/ui/InventoryMenuSceneTest.cs`;
- its design/plan documents.

Fresh automated validation covers Inventory behavior and deterministic layout/focus. In particular, existing tests own non-mutating selection, explicit compact Details navigation, standard/compact page shape, focus, and Close reachability.

The real-window check does **not** re-prove those behaviors. Its only purpose is final production-window composition that the SubViewport suite cannot provide.

### Fresh HPA-541 ownership

PR #47 changed:

- Settings data/manager/controller/scene;
- `Game`, `GridMap`, `PlayerDisplay`, `EnemySpawn`;
- `BattleManager`;
- matching Settings, world, Game, Battle, and scene tests.

Fresh closeout validation covers those deterministic owners plus real Game host integration. No extra manual Reduced Motion certification is required. Default production remains Reduced Motion disabled, so HPA-359 remains the default-motion production walkthrough while HPA-541 tests own the reduced branch.

## Current-head automated validation

Run all of the following against the HPA-306 branch based on current `main`:

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
4. `git diff --check main...HEAD`.

The focused set is a diagnostic gate. The full suite remains the umbrella completion gate because HPA-306's definition of done spans shared lifecycle/domain contracts, not only the two post-HPA-359 filesets.

## One real-window Inventory composition check

Only Inventory needs a fresh manual visual check because HPA-375 changed its authored layout after HPA-359.

Use the production game, not a SubViewport fixture. Behavioral assertions remain owned by Task 1 automated tests.

### `1280×720` pass criteria

- standard Character / Items / Details composition is visually usable;
- required content and item art are visible;
- Details presentation, filter/sort controls, and Close are visible/reachable in the production window;
- no severe overlap, zero-width region, or off-window placement makes required content unusable;
- no new runtime UI error is emitted.

Do not manually assert equip/use mutation semantics, selection state rules, compact-page behavior, or focus policy here; the existing Inventory suites own them.

### `640×360` pass criteria

- required Inventory content and item art remain visible;
- compact tabs/pages, Details presentation, filter/sort controls where shown, and Close remain visible/reachable;
- the production window remains usable even if section-heading chrome is tight/clipped as already accepted by HPA-359;
- clipping is a defect only when it makes required content or controls unreachable/unusable;
- no new runtime UI error is emitted.

This is a composition check, not a second behavioral oracle.

## Fresh visual evidence

The post-HPA-375 layout is the one unique manual delta, so preserve exactly two fresh captures:

- `docs/ui/hpa-306/evidence/inventory-1280x720.png`;
- `docs/ui/hpa-306/evidence/inventory-640x360.png`.

Record each file's dimensions and SHA-256 in `docs/ui/hpa-306/closeout.md`.

Do **not** overwrite `docs/ui/hpa-359/evidence/inventory-*.png`: HPA-359's release record pins hashes for those historical captures. HPA-306 adds a new evidence pair instead, keeping both evidence sets internally consistent.

Do not add a screenshot harness, recapture Battle/Save/Main Menu, or create a broader image matrix. If the real-window session cannot be completed, HPA-306 stays open rather than pretending prose is equivalent evidence.

## Closeout record

Implementation creates one concise durable record at:

`docs/ui/hpa-306/closeout.md`

It records:

- HPA-306 child/optional-ticket completion status;
- the HPA-359 evidence intentionally reused;
- exact current-head build/focused/full-suite results;
- the two Inventory real-window composition observations;
- dimensions and SHA-256 for the two HPA-306 Inventory captures;
- any defect and focused regression/fix, if required;
- the final recommendation to close or keep HPA-306 open.

Do not rewrite `docs/ui/hpa-359/release-validation.md` or recalculate the historical April completion snapshot in `docs/PRD.md` merely for this umbrella closeout.

## Defect handling

If automated validation exposes a real defect:

1. use the existing failing owner test as RED when it is already specific enough; otherwise add/tighten the smallest useful regression;
2. confirm the failure is for the intended reason;
3. make the smallest production fix in the existing owner;
4. rerun the focused owner suite;
5. rerun the HPA-306 focused set and full suite;
6. repeat only the affected Inventory real-window composition observation when relevant.

If the real-window check exposes a visual defect not already represented by automation, reproduce it first in `InventoryMenuSceneTest`, then make the minimal existing-owner fix.

The known compact section-heading clip is not a defect under this ticket unless required content or controls become unusable. New product polish remains outside HPA-306.

Keep any fix in the same HPA-306 PR. Do not introduce a reusable abstraction for a one-off closeout defect. If a finding is new product scope rather than a regression, record it separately and do not expand HPA-306 to absorb it.

## Definition-of-done mapping

HPA-306 can close when:

- **Shared launch/exploration/interaction/battle/inventory/pause/save/settings journey:** HPA-359 production evidence plus current-head full suite; Inventory gets the post-HPA-375 composition delta.
- **No normal debug/desktop-window final presentation:** HPA-359 legacy-path audit remains authoritative; neither HPA-375 nor HPA-541 reintroduced those paths.
- **One local `UIScreenHost` per Main Menu/Game root:** existing host/Game/Main Menu tests remain green.
- **Explicit pause/input/cursor/HUD/focus/Cancel ownership:** existing runtime-backed host/controller suites remain green.
- **Domain behavior preserved:** full suite plus HPA-375/HPA-541 focused owners remain green.
- **Primary/minimum viewports usable with deterministic focus:** Inventory scene tests own deterministic layout/focus behavior; the two fresh real-window captures own current production composition.
- **No duplicate activation, stuck input, orphaned presentation, or missing-resource regression:** full suite remains green; no new blocking issue appears in the composition delta.
- **Focused gameplay/persistence/host/lifecycle tests pass:** covered by the focused current-head gate and full suite.
- **Obsolete paths removed after replacement:** HPA-359 audit remains the owner; post-HPA-359 changes do not add compatibility paths.

After the closeout record is green and the HPA-306 PR is ready to merge, update Linear HPA-306 with the final evidence summary and mark it Done. Do not change the broader Sirius Linear project state.

## Non-goals

- No new screen, modal, host kind, UI service, presenter, view-model, navigation framework, or second host.
- No new E2E driver, screenshot framework, release framework, or manual certification matrix.
- No replay of the complete HPA-359 production walkthrough without a delta-specific reason.
- No manual re-testing of Inventory behavior already protected by GdUnit.
- No additional inventory, combat, settings, motion, save, dialogue, shop, puzzle, or reward feature work.
- No fix for known compact heading-chrome clipping unless it makes required content/controls unusable.
- No compatibility work for development saves or obsolete UI paths.
- No broad PRD rewrite or project-wide completion recalculation.
- No Linear project-status change; HPA-306 issue completion is the only Linear closeout action.

## Risks and mitigations

### HPA-359's Inventory captures are stale after HPA-375

Mitigation: perform exactly one fresh real-window Inventory composition check at `1280×720` and `640×360` and add exactly two HPA-306 evidence images with dimensions/hashes. Preserve the historical HPA-359 files unchanged.

### Known compact heading clipping gets mistaken for new product work

Mitigation: inherit HPA-359's usability disposition. Heading chrome may remain tight/clipped; only unreachable/unusable required content or controls block HPA-306.

### Manual checking becomes a second behavioral oracle

Mitigation: Task 1 GdUnit suites own selection, actions, filters/sorts, compact-page navigation, focus, and Close behavior. The live window owns composition only.

### HPA-541 touched several production owners after the final release walkthrough

Mitigation: run deterministic Settings/world/Game/Battle owner suites plus `GameplayPauseHostTest`, then require the full suite on current `main`.

### Umbrella closeout grows into another feature pass

Mitigation: defects are test-first regressions only. New product ideas remain outside HPA-306.
