# HPA-306 Sirius UI Revamp Closeout Design

**Issue:** HPA-306 — Revamp Sirius UI architecture and player-facing screen layouts\
**Date:** 2026-08-24\
**Scope:** Close the completed Sirius UI revamp umbrella on current `main` without creating another UI subsystem or replaying already-owned release work.

## Context

HPA-306 is an umbrella/completion checkpoint, not another screen implementation ticket. Its required vertical slices are complete, and both optional follow-ups are also merged:

- HPA-375 — Inventory details/comparison/filter/sort, PR #46;
- HPA-541 — persisted Reduced Motion and production motion policy, PR #47.

HPA-359 already owns the expensive final production walkthrough in `docs/ui/hpa-359/release-validation.md`: actual Main Menu → Game replacement, Ground Floor start, movement → goblin → Battle/result, Game → Main Menu replacement, Save/Prompt, hosted joypad Cancel, compact viewport checks, runtime observations, and the legacy-path audit.

The only material post-HPA-359 delta is HPA-375 Inventory work plus HPA-541 Settings/world/Battle motion work. Replaying the entire release walkthrough would mostly retest untouched seams.

## Decision

Use a **delta closeout**:

1. fresh current-head build + focused HPA-375/HPA-541 owners + full solution suite;
2. one production Inventory usability/runtime-error check at `1280×720` and `640×360`;
3. exactly two new root-viewport Inventory captures;
4. one concise `docs/ui/hpa-306/closeout.md`;
5. no production code/scene changes unless a concrete defect is RED first in an existing owner suite.

Do not add another UI layer, E2E driver, screenshot framework, release framework, compatibility path, or broader manual certification matrix.

## Evidence ownership

### HPA-359 remains authoritative

Reuse HPA-359 without replaying it for:

- actual `MainMenu.tscn` → `Game.tscn` scene replacement;
- authored FloorGF start `(8, 50)`;
- movement into the first goblin `(24, 45)` and production Battle/result entry;
- actual Game → Main Menu replacement;
- hosted Pause/Save/Settings/Prompt lifecycle and joypad Cancel;
- compact Save/overwrite Prompt real-window usability;
- legacy desktop-dialog/debug-path audit;
- default-motion production behavior and final runtime warning/error observations.

HPA-359 also records compact Inventory as usable at `640×360` even with tight/clipped section-heading chrome. HPA-306 inherits that disposition. Heading chrome alone is not a closeout defect; it becomes blocking only when required content or controls become unusable/unreachable.

### HPA-375 automated owners

Fresh automated validation owns Inventory behavior and deterministic geometry/focus:

- `InventoryMenuControllerTest` — selection/actions/filter/sort/state;
- `InventoryMenuSceneTest` — standard/compact page shape, viewport bounds, focus, Close reachability, compact Details navigation.

The live check does not re-prove those contracts. Its residual purpose is only:

- human judgment that the actual production window remains visually usable;
- detection of new runtime UI errors in that production session;
- current visual evidence for the post-HPA-375 authored layout.

`project.godot` does not configure stretch/content-scale overrides, so the existing `1280×720` / `640×360` SubViewport geometry remains the deterministic layout oracle.

### HPA-541 automated owners

Fresh validation covers Settings persistence/staging, world propagation, real Game integration, Battle reduced-motion behavior, and real-scene visibility through the existing Settings/world/Game/Battle suites.

No full manual Reduced Motion replay is required. Default production remains Reduced Motion disabled, so HPA-359 remains the default-motion production record while HPA-541 tests own the reduced branch.

## Current-head validation baseline

Before any PR-range check:

```bash
git fetch origin main
```

Use `origin/main...HEAD`, not a potentially stale local `main`.

Completion validation is:

1. `dotnet build Sirius.sln`;
2. focused HPA-375/HPA-541 owner filter;
3. full `dotnet test Sirius.sln --settings test.runsettings.local`;
4. `git diff --check origin/main...HEAD`.

The focused set is diagnostic. The full suite remains the umbrella gate because HPA-306's DoD spans shared lifecycle/domain contracts.

## Production Inventory check

Start from **New Game**. `GameManager.InitializePlayer()` already equips starter gear and adds starter consumables, which is enough to populate a representative Inventory. Do not load or modify a user save. Victory autosave is currently disabled, so this closeout does not need HPA-359's save backup/restore ceremony.

At `1280×720` and `640×360`, the live pass asks only:

- is the production Inventory visually usable to a human;
- is there any new runtime UI error;
- does the known compact heading-chrome concern remain merely cosmetic rather than making required content/controls unusable.

Do not manually score zero-width geometry, off-window bounds, selection mutation, filter/sort semantics, page navigation, focus, or Close behavior; those are existing GdUnit responsibilities.

## Root-viewport capture mechanism

Use the same published Godot MCP runtime-control path used by HPA-359, pinned to `@cwchanap/godot-plugin@0.1.4`.

The runtime-control contract is:

1. start the project with `run_project` and `runtimeControl: true`;
2. the server automatically installs/repairs the managed runtime bridge;
3. use `capture_screenshot` to capture the rendered **root viewport** rather than an OS window screenshot;
4. use `capture_screenshot` with `saveTo: "project"` so the PNG is persisted under `.godot-mcp/captures/`;
5. copy the exact returned capture path to the HPA-306 evidence filename;
6. after capture, stop the project and call `uninstall_runtime_bridge`.

Root-viewport capture is required because an OS window screenshot on a Retina display can return device-pixel dimensions rather than the logical `640×360` viewport.

Before runtime control, record the current `project.godot` SHA-256. After `uninstall_runtime_bridge`, require:

- `.godot-mcp` absent;
- `project.godot` SHA-256 exactly equal to the pre-session value;
- no tracked `project.godot` diff.

The `project.godot` hash is load-bearing teardown verification. It is separate from screenshot hashing.

## Fresh visual evidence

Add exactly:

- `docs/ui/hpa-306/evidence/inventory-1280x720.png`;
- `docs/ui/hpa-306/evidence/inventory-640x360.png`.

Verify only that each file is a PNG with the exact logical dimensions its filename claims. Do **not** mint SHA-256 values for the new screenshots; they do not gate correctness in this solo pre-1.0 closeout.

Do not overwrite HPA-359's historical images and do not recapture Battle, Save/Prompt, Main Menu, or other screens.

## Closeout record

Create `docs/ui/hpa-306/closeout.md` containing:

- required/optional delivery-chain completion;
- HPA-359 evidence intentionally reused;
- exact build/focused/full-suite totals;
- `1280×720` and `640×360` live usability/runtime observations;
- paths + exact dimensions for the two HPA-306 images;
- runtime-bridge teardown result (`.godot-mcp` absent, `project.godot` restored unchanged);
- any concrete regression/fix, if required;
- HPA-306 DoD mapping;
- one unambiguous disposition: close or keep open.

Do not rewrite `docs/ui/hpa-359/release-validation.md`, broadly rewrite `docs/PRD.md`, or recalculate the historical April `~65%` snapshot.

## Defect handling

If automated validation fails specifically, that failure is the RED gate. If a live visual finding is not already represented deterministically, add the smallest regression to `InventoryMenuSceneTest` first.

Only after a specific RED exists:

1. make the smallest fix in the existing owner;
2. rerun the owner suite;
3. rerun the focused set + full suite;
4. repeat only the affected Inventory live observation/capture.

The known compact heading clip does not qualify unless it makes required content/controls unusable. New product polish stays outside HPA-306.

## Definition-of-done mapping

HPA-306 can close when:

- HPA-359 evidence plus the current full suite cover the complete shared player journey;
- current Inventory owner tests are green and the two production-window observations are usable;
- current HPA-541 Settings/world/Game/Battle owners are green;
- the two new root-viewport captures have exact `1280×720` and `640×360` dimensions;
- the temporary runtime bridge is fully removed and `project.godot` is unchanged;
- no concrete regression remains open.

After PR #48 is **merged**, add the final evidence summary to Linear HPA-306 and mark HPA-306 Done. Do not change the Sirius Linear project state.

## Non-goals

- No new screen, modal, host kind, service, presenter/view-model, navigation framework, or second host.
- No E2E/screenshot/release framework.
- No full HPA-359 replay.
- No manual duplicate of Inventory behavior/geometric assertions already owned by GdUnit.
- No extra Inventory, Settings, motion, Battle, save, dialogue, shop, puzzle, or reward features.
- No user-save manipulation.
- No compatibility work for development saves or obsolete UI paths.
- No broad PRD rewrite or project-wide completion recalculation.

## Risks and mitigations

### Runtime capture mutates the project temporarily

Mitigation: baseline `project.godot`, use the known 0.1.4 runtime bridge, stop/uninstall it, assert `.godot-mcp` absent, and require the original `project.godot` SHA before any final scope check.

### Retina screenshot scaling invalidates evidence dimensions

Mitigation: use Godot MCP `capture_screenshot` root-viewport output, not an OS window grab, then verify PNG dimensions.

### Stale local `main` creates false scope alarms

Mitigation: fetch `origin main` and use `origin/main...HEAD` for scope/whitespace checks.

### Manual checking grows into product work

Mitigation: the live pass owns only human usability + runtime UI errors. Deterministic geometry/behavior remains in existing tests, and known heading chrome remains non-blocking.
