# Sirius UI Lifecycle Baseline Design

**Version:** 1.0  
**Status:** Review candidate — design approved section by section; written artifact review pending  
**Linear:** HPA-376, child of HPA-354  
**Design decisions approved:** 2026-07-26

## 1. Purpose

This specification defines the audit and regression-testing work required before Sirius introduces `UIScreenHost`.

HPA-376 establishes two things:

1. An honest inventory of how every current player-facing flow opens, owns input, handles Cancel, closes, and restores its parent.
2. An executable migration contract that protects approved behavior without permanently locking accidental legacy behavior.

The resulting lifecycle contract is the direct behavioral input to HPA-378. It does not introduce the host, navigation stack, shared theme, or redesigned screens.

## 2. Scope

The implementation will produce:

- `docs/ui/hpa-376/ui-lifecycle-contract.md`, containing the lifecycle inventory and modal-priority matrix
- Targeted regression coverage in the existing Godot/GdUnit4 suite
- New controller test suites only where no suitable suite currently exists
- Narrow production fixes or testability hooks only when deterministic lifecycle testing requires them

The inventory covers:

- Main-menu settings, load, and informational or error messages
- Exploration HUD and interaction prompt
- Inventory
- Pause and its child flows
- Settings, including staged edits, key capture, and dropdowns
- Save/load, overwrite confirmation, errors, and pause restoration
- Battle preparation, automatic combat, results, defeat, escape, and cleanup
- Dialogue, shop, and healing
- Puzzle/riddle and world-interaction cleanup
- Treasure/reward behavior
- Confirmation, warning, and error presentation

## 3. Non-goals

HPA-376 will not:

- Add `UIScreenHost`, a navigation service, a screen stack, or shared lifecycle interfaces
- Move coordination out of `Game._Input()`
- Implement project-wide mouse, HUD-layer, focus-fallback, or pause ownership
- Redesign or restyle any screen
- Change combat, inventory, save, settings, dialogue, shop, healing, puzzle, or reward domain rules
- Add screenshot, visual-regression, or full-playthrough tests
- Fix unrelated compiler warnings or pre-existing orphan-node warnings

## 4. Current evidence

The current checkout has no `UIScreenHost`. Presentation is owned locally by `MainMenu`, `Game`, individual `AcceptDialog` subclasses, and `InventoryMenuController`.

The most important current observations are:

- `Game._Input()` coordinates inventory and Pause actions through ordered special cases.
- Settings already distinguishes ordinary Cancel, active key capture, capture of the Pause binding itself, and open `OptionButton` popups.
- Save/load has a parent dialog and an overwrite-confirmation child, with special restoration when invoked from Pause.
- Inventory sets `SceneTree.Paused` on open and unconditionally clears it on close.
- Pause does not currently set `SceneTree.Paused`, despite the approved HPA-373 contract requiring a paused world.
- Battle emits its result before the result dialog is acknowledged, so result-phase Cancel must still belong to the battle presentation rather than opening Pause behind it.
- NPC and puzzle dialogs rely on their own Godot cancellation paths while `Game._Input()` deliberately avoids opening Pause.
- Mouse visibility is not explicitly owned by the current flows.
- Treasure rewards have no standalone reward modal; battle loot remains part of the battle presentation.

The baseline suite passes 869 tests with Godot 4.6.2 when run outside the filesystem sandbox. Settings and `Game._Input()` already have substantial regression coverage. Inventory lifecycle, save/load dialog behavior, battle interruption and cleanup, and NPC/puzzle cancellation have the largest gaps.

## 5. Chosen approach

Use a contract-first audit with targeted regression seams.

This approach separates observed behavior from the required migration contract, tests the required behavior at the narrowest useful boundary, and permits only the production changes needed for reliable lifecycle guarantees.

Two alternatives were rejected:

- A pure characterization approach would avoid production edits but would depend heavily on reflection and could lock known flaws into tests.
- Early host-contract extraction would create shared records or interfaces before HPA-378 and would risk over-generalizing from incomplete evidence.

## 6. Contract policy

Every audited behavior is classified in three columns:

| Column | Meaning |
|---|---|
| Observed behavior | What the current checkout actually does, including missing or accidental behavior |
| Required migration contract | The behavior later host integration must preserve or establish, aligned with HPA-373 |
| Disposition | `Preserve`, `Replace in HPA-378/379`, or `Fix in HPA-376` |

Regression tests protect the required migration contract only when HPA-376 owns that guarantee. They do not freeze a behavior marked for replacement.

Examples:

- Settings dropdown and key-capture exceptions are `Preserve`.
- Central pause, mouse, HUD, and focus-fallback ownership are `Replace in HPA-378/379`.
- Restoring the tree's previous pause value, exactly-once close emission, or timer cleanup may be `Fix in HPA-376` when a regression test proves the current behavior is unsafe for migration.

## 7. Lifecycle inventory

The lifecycle contract uses two linked matrices rather than one unreadably wide table.

### 7.1 Per-flow lifecycle matrix

Each flow records:

- Flow and phase
- Entry point
- Parent context
- Current scene or controller owner
- Observed tree-pause behavior
- Required tree-pause behavior
- Gameplay-input blocking
- HUD visibility
- Mouse policy
- Initial focus
- Cancel/back receiver
- Nested popup behavior
- Close or result signal
- Cleanup owner
- Focus, pause, and input restoration target
- Disposition
- Protecting regression-test evidence

Unknown or absent behavior is written explicitly. The audit does not infer focus, cursor, or restoration behavior merely because Godot may provide a transient default.

### 7.2 Modal-priority matrix

The priority matrix records which state receives Cancel and what becomes active afterward.

The required priority is:

1. Active child or capture surface, including an `OptionButton`, key capture, or overwrite confirmation
2. Topmost blocking error or confirmation
3. Owning screen or modal: settings, save/load, inventory, puzzle, NPC, or battle
4. Parent screen such as Pause
5. Gameplay fallback, where Cancel opens Pause

Exactly one applicable layer handles each Cancel event.

Key exceptions remain explicit:

- An open `OptionButton` receives Cancel before Settings.
- Active key capture receives Cancel before Settings, except while capturing the Pause action when Escape is a valid candidate binding.
- An overwrite confirmation closes without dismissing save/load.
- NPC and puzzle dialogs receive Cancel without Pause opening behind them.
- Battle preparation, automatic combat, and results use the existing battle escape policy; a result dialog remains topmost even after the battle domain flag has cleared.
- Required acknowledgements do not become accidentally dismissible through a generic Cancel path.

## 8. Transition and restoration rules

Even while the legacy controllers remain in charge, lifecycle behavior follows these rules:

1. Opening a flow records the parent state relevant to later restoration before changing visibility, pause, or focus.
2. Opening a child makes its parent inert while preserving it for restoration.
3. Closing is idempotent. Cleanup and close/result signals occur once even if Cancel, window close, confirmation, and deferred cleanup overlap.
4. Closing a child restores only its parent.
5. Closing the outermost presentation restores gameplay.
6. Deferred restoration remains where the current event could otherwise close a child and immediately toggle its parent.
7. Each controller owns its timers, signal disconnection, and transient child cleanup.
8. An invalid restoration target falls back to the documented safe target for the parent. Common host-owned focus fallback is deferred to HPA-378, but the required target is still recorded now.

## 9. Permitted production changes

Production edits must stay narrowly tied to a failing lifecycle regression.

Permitted changes include:

- Remembering and restoring a prior tree-pause value
- Exactly-once close or result guards
- Timer shutdown and signal disconnection needed to prevent stale callbacks
- Cleanup that prevents `GameManager` interaction flags from remaining stuck
- Small internal read-only properties or helpers that expose lifecycle state without changing ownership

The preferred assertion surface remains public behavior:

- Visibility
- `SceneTree.Paused`
- Input handled state
- Emitted signals and their payloads
- Restored parent state
- Valid or freed transient nodes

Reflection is a last resort for legacy private state. Test-only abstractions and speculative host interfaces are not permitted.

## 10. Regression-test architecture

### 10.1 Existing suites to extend

| Suite | Added responsibility |
|---|---|
| `tests/game/GameTest.cs` | Topmost-only Cancel, parent restoration, error dismissal, NPC cancellation, and gameplay-input blocking |
| `tests/ui/InventoryMenuControllerTest.cs` | Open/close visibility, Cancel/toggle handling, pausing, and prior-pause restoration |
| `tests/ui/PauseMenuDialogTest.cs` | Idempotent close signaling and existing initial-focus behavior |
| `tests/ui/SettingsMenuControllerTest.cs` | Only missing staged Apply/Cancel behavior; retain existing key-capture and dropdown coverage |
| `tests/ui/BattleManagerTest.cs` | Pre-start escape, active escape, timer shutdown, effect cleanup, and exactly-once result emission |
| `tests/ui/ShopDialogTest.cs` | Cancel, close idempotency, and timer cleanup |

### 10.2 New focused suites

- `tests/ui/MainMenuTest.cs`
- `tests/ui/SaveLoadDialogTest.cs`
- `tests/ui/DialogueDialogTest.cs`
- `tests/ui/HealDialogTest.cs`
- `tests/ui/PuzzleRiddleDialogTest.cs`
- `tests/ui/NpcInteractionControllerTest.cs`

Each suite tests the controller's local lifecycle. Cross-controller priority and restoration belong in `GameTest`.

### 10.3 Evidence mapping

Every lifecycle row classified as `Preserve` or `Fix in HPA-376` names at least one protecting test method. Rows classified as `Replace in HPA-378/379` name the downstream owner and must not claim coverage for behavior the current architecture does not provide.

## 11. Failure handling

Failure behavior must be deterministic:

- A failed open leaves the parent usable or restores it before surfacing the error.
- Repeated close or cleanup calls are harmless.
- Dismissing a child does not dismiss its parent.
- Battle escape stops the turn timer, resolves battle state once, and emits one result.
- NPC and world-interaction failure paths clear their corresponding `GameManager` flags.
- Corrupted-save and save/load errors remain topmost and cannot open Pause behind themselves.
- Test cleanup frees transient dialogs and disconnects timers so new tests introduce no orphan-node warnings.

Existing orphan-node warnings are recorded as baseline evidence. HPA-376 does not expand into unrelated test-suite cleanup.

## 12. Verification

Implementation verification proceeds in layers:

1. Run each changed or new controller suite.
2. Run `GameTest` for cross-controller priority and restoration.
3. Run the complete suite with `dotnet test Sirius.sln --settings test.runsettings.local`.
4. Compare orphan-node output with the baseline and reject newly introduced warnings.
5. Check the lifecycle contract against HPA-373 section 7 and the HPA-376 acceptance criteria.

The full-suite success condition is the existing 869 passing tests plus all newly added tests, with zero failures or skips.

## 13. Completion criteria

HPA-376 is complete when:

- Every listed player-facing flow and phase has an observed lifecycle row.
- Pause, input, HUD, mouse, focus, Cancel, signal, cleanup, and restoration expectations are explicit.
- The modal-priority matrix can be implemented directly by HPA-378.
- Every preserved or HPA-376-fixed contract has automated evidence.
- Existing settings, save/load, battle, inventory, NPC, puzzle, error, and topmost-Cancel exceptions are covered at the approved boundary.
- No host architecture, broad redesign, or domain-rule change is included.
- Focused and full verification pass.

