# Sirius UI Lifecycle Baseline Design

**Version:** 1.3
**Status:** Review candidate — design approved section by section; three written-artifact review passes incorporated
**Linear:** HPA-376, child of HPA-354  
**Design decisions approved:** 2026-07-26
**Lineage:** Supersedes nothing; implements HPA-373 section 7 as a behavioral baseline and feeds HPA-378.

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
- New focused test suites where none exists, plus a lifecycle suite when an existing broad fixture is technically suitable but would become a less maintainable cross-controller sink
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
- Treat silent gameplay-domain events without a presentation lifecycle, such as trap-tile damage, as standalone UI flows; their existing input guards and domain tests remain outside this audit
- Add screenshot, visual-regression, or full-playthrough tests
- Fix unrelated compiler warnings or pre-existing orphan-node warnings

## 4. Current evidence

The current checkout has no `UIScreenHost`. Presentation is owned locally by `MainMenu`, `Game`, individual `AcceptDialog` subclasses, and `InventoryMenuController`.

The most important current observations are:

- `Game._Input()` coordinates inventory and Pause actions through ordered special cases.
- Gameplay input is blocked by three independent `GameManager` flags: `IsInBattle`, `IsInNpcInteraction`, and `IsInWorldInteraction`. The world-interaction flag covers treasure opening, puzzle switches, and puzzle/riddle flows.
- Settings already distinguishes ordinary Cancel, active key capture, capture of the Pause binding itself, and open `OptionButton` popups.
- Save/load has a parent dialog and an overwrite-confirmation child, with special restoration when invoked from Pause.
- Inventory sets `SceneTree.Paused` on open and unconditionally clears it on close.
- Pause does not currently set `SceneTree.Paused`, despite the approved HPA-373 contract requiring a paused world.
- Battle emits its result before the result dialog is acknowledged. `Game.OnBattleFinished()` therefore clears `IsInBattle` while the Continue surface is still visible, after which `Game.HandlePauseMenuInput()` can open Pause behind it. Keeping the visible battle presentation topmost is a known `Fix in HPA-376`.
- NPC and puzzle dialogs rely on their own Godot cancellation paths while `Game._Input()` deliberately avoids opening Pause.
- Mouse visibility is not explicitly owned by the current flows.
- Treasure rewards have no standalone reward modal; battle loot remains part of the battle presentation.

At source baseline `bc82ead`, the suite passed 869 tests with Godot 4.6.2 when run outside the filesystem sandbox. This count is evidence for that commit, not a permanent expected total. Settings and `Game._Input()` already have substantial regression coverage. Inventory lifecycle, save/load dialog behavior, battle interruption and cleanup, and end-to-end NPC/puzzle cancellation have the largest gaps.

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

Regression tests protect the required migration contract only when HPA-376 owns that guarantee. They do not freeze a behavior marked for replacement. Each observed entry cites one concrete source location in `file:member` form; the audit may add supporting locations but cannot replace evidence with an inference about Godot defaults.

Examples:

- Settings dropdown and key-capture exceptions are `Preserve`.
- Central pause, mouse, HUD, and focus-fallback ownership are `Replace in HPA-378/379`.
- The current Pause dialog's failure to set `SceneTree.Paused` is explicitly `Replace in HPA-378/379`. Fixing it safely requires coordinated process-mode and child-flow ownership for Pause, Settings, save/load, and nested dialogs, which is host work rather than a narrow HPA-376 correction.
- The current battle Continue surface is a legacy, recoverable result layer whose existing Cancel dismissal is `Preserve`; it is not classified as a future required acknowledgement. Routing Cancel to that still-visible result instead of opening Pause behind it is `Fix in HPA-376`.
- A future reward or battle-result constellation that the producer marks as requiring acknowledgement does not dismiss through Cancel. That policy is `Replace in HPA-378/379`; its presentation is realized by the relevant downstream screen work after HPA-393 supplies any required reward handoff guarantees.
- Inventory restoring a previously paused tree is an explicitly licensed forward-compatibility correction. HPA-373 makes Inventory-from-Pause a legal parent context even though current Pause does not pause the tree and cannot open Inventory; HPA-376 proves the correction with a synthetic paused-parent state.
- Exactly-once close emission, timer cleanup, or restoration cleanup beyond the explicitly licensed Inventory seam may be `Fix in HPA-376` when a regression test proves the current behavior is unsafe for migration.

## 7. Lifecycle inventory

The lifecycle contract uses two linked matrices rather than one unreadably wide table.

### 7.0 Authoritative flow-by-phase checklist

The implementation contract must contain one row for every ID below. An ID may not be omitted because a phase has no standalone scene; that absence is itself observed behavior. A row may link to another row for shared mechanics, but it must still state its own parent context, input receiver, restoration target, evidence, disposition, and protecting test or downstream owner.

| ID | Flow and phase | Required context or variant | Initial classification |
|---|---|---|---|
| `MAIN-ROOT` | Main menu root | Startup and return-to-title; root Cancel | Audit from source |
| `MAIN-LOAD` | Main-menu Load entry and return | Empty, populated, and unavailable/corrupt slot | Audit from source |
| `MAIN-SETTINGS` | Main-menu Settings entry and return | Apply and discard staged edits | Audit from source |
| `MAIN-MESSAGE` | Main-menu information or error | Recoverable message above the root | Audit from source |
| `MAIN-QUIT` | Main-menu application quit | Explicit root Quit button; no active gameplay progress | `Preserve` |
| `EXP-GAMEPLAY` | Exploration HUD and gameplay fallback | Normal input; Pause request | Audit from source |
| `EXP-PROMPT` | Interaction prompt visibility | Show, hide during each blocking flow, and restore | Audit from source |
| `EXP-FLOOR-TRANSITION` | Exploration stair/floor transition | Live HUD and prompt while `FloorManager` replaces the world; deferred prompt restoration | Audit from source |
| `INV-GAMEPLAY` | Inventory from exploration | Open, active, Cancel/toggle close, and prior-pause restoration | Prior-pause restoration is `Fix in HPA-376` via synthetic paused-parent state; audit remaining behavior |
| `INV-BLOCKED` | Rejected Inventory entry | Settings, save/load, battle, NPC, and world-interaction blockers | Audit from source |
| `INV-PAUSE` | Inventory from Pause | Legal in HPA-373 but impossible in the current controller graph | `Replace in HPA-378/379` |
| `PAUSE-ROOT` | Pause root | Open, toggle/Resume, and gameplay restoration | Pause ownership is `Replace in HPA-378/379`; characterize current routing |
| `PAUSE-SETTINGS` | Pause to Settings and back | Parent inert; inherited pause state | Audit current return; root pause ownership is downstream |
| `PAUSE-SAVELOAD` | Pause to Save/Load and back | Parent restoration on close, success, and failure | Audit current return; root pause ownership is downstream |
| `PAUSE-QUIT-TO-MAIN` | Pause Quit to Main Menu | Direct cleanup and scene transition currently have no quit-with-risk confirmation; cleanup must survive replacement | `Replace in HPA-378/379` |
| `PAUSE-RESTORE-PENDING` | Deferred Pause restoration after Settings closes | Re-entrant `pause_menu` is consumed without opening or closing a layer; the host may replace the boolean mechanism | `Preserve` |
| `SET-MAIN` | Settings under Main Menu | Open, staged edit, Apply, Cancel, and return | Audit from source |
| `SET-PAUSE` | Settings under Pause | Open, staged edit, Apply, Cancel, and return | Audit from source |
| `SET-DROPDOWN` | Settings `OptionButton` popup | Both parent contexts; popup Cancel before Settings | `Preserve` |
| `SET-CAPTURE` | Ordinary key capture | Accept, cancel, duplicate, and reserved binding | `Preserve` |
| `SET-CAPTURE-PAUSE` | Capture of the Pause action | Escape/Pause is a candidate binding rather than generic Cancel | `Preserve` |
| `SAVE-MAIN` | Save/Load under Main Menu | Open, ordinary close, load handoff, and parent return | Audit from source |
| `SAVE-PAUSE` | Save/Load under Pause | Open, ordinary close, operation completion, and Pause restoration | Audit from source |
| `SAVE-OVERWRITE` | Overwrite confirmation child | Cancel/window close/button; parent remains open | Audit from source |
| `SAVE-CORRUPT` | Corrupt or unavailable load error | Main Menu and Pause parents | Audit from source |
| `SAVE-ERROR` | Ordinary save/load operation error | Main Menu and Pause parents | Audit from source |
| `SAVE-QUIT-TO-MAIN` | Save/Load Main Menu button | Current path cleans Save/Load and a hidden Pause parent, then transitions without quit-with-risk confirmation; cleanup must survive replacement | `Replace in HPA-378/379` |
| `BATTLE-PREP` | Battle preparation | Before automatic combat starts | Audit from source |
| `BATTLE-AUTO` | Automatic combat | Turn timer active and gameplay blocked | Audit from source |
| `BATTLE-ESC-PREP` | Escape during preparation | Result emission and cleanup | Audit; fix only on failing contract |
| `BATTLE-ESC-ACTIVE` | Escape during automatic combat | Timer/effect cleanup and exactly-once result | Audit; fix only on failing contract |
| `BATTLE-RESULT-VICTORY` | Visible victory/loot Continue surface | `IsInBattle` may already be false | Topmost routing is `Fix in HPA-376`; legacy Cancel dismissal is `Preserve` |
| `BATTLE-RESULT-DEFEAT` | Defeat result and delayed return-to-menu | Unretained two-second `SceneTreeTimer` callback can outlive the tested flow and trigger stale navigation | Topmost routing and owned/guarded delayed transition are `Fix in HPA-376` |
| `BATTLE-RESULT-ESCAPE` | Escape result Continue surface | `IsInBattle` may already be false | Topmost routing is `Fix in HPA-376`; legacy Cancel dismissal is `Preserve` |
| `BATTLE-CLEANUP` | Battle close/free cleanup | Continue, Cancel, window close, deferred callbacks, and repeat triggers | Audit; exactly-once defects may be fixed |
| `NPC-DIALOGUE` | Dialogue active | Continue/choice and permitted Cancel | Audit from source |
| `NPC-TO-SHOP` | Dialogue-to-Shop transition | Dialogue parent retained or replaced; focus/restoration | Audit from source |
| `NPC-SHOP` | Shop active and return | Buy/Sell, feedback timer, Cancel/window close | Audit; cleanup defects may be fixed |
| `NPC-TO-HEAL` | Dialogue-to-Healing transition | Dialogue parent retained or replaced; focus/restoration | Audit from source |
| `NPC-HEAL` | Healing active and return | Heal/No Thanks/Cancel/window close | Audit from source |
| `NPC-CLEANUP` | NPC interaction completion | Each exit and failure path clears `IsInNpcInteraction` once | Audit; stuck/duplicate cleanup may be fixed |
| `WORLD-TREASURE` | Atomic treasure open | Silent grant and cell clear under `IsInWorldInteraction` | Preserve domain behavior; presentation replacement is downstream |
| `WORLD-SWITCH` | Atomic puzzle-switch interaction | No modal; input block and cleanup | Audit from source |
| `WORLD-RIDDLE` | Puzzle/riddle modal | Open, answer/success/failure, Cancel, and window close | Audit from source |
| `WORLD-CLEANUP` | World-interaction completion | Treasure, switch, riddle, and failure paths clear `IsInWorldInteraction` once | Audit; stuck/duplicate cleanup may be fixed |
| `REWARD-TOAST` | Future brief single-reward presentation | No current standalone surface | `Replace in HPA-378/379`; display payload ownership remains downstream |
| `REWARD-BLOCKING` | Future important/multiple/result/required acknowledgement | No current shared constellation | `Replace in HPA-378/379`; coordinate with HPA-393 and downstream screen work |
| `CONFIRM-ORDINARY` | Recoverable confirmation or warning | Every invoking parent represented above | Audit current flows |
| `CONFIRM-DESTRUCTIVE` | Destructive or blocking confirmation | Pause and in-game Save/Load quit-to-title paths currently transition without the HPA-373 quit-with-risk confirmation | `Replace in HPA-378/379`; explicit safe action and generic Cancel must not confirm |
| `ERROR-TOPMOST` | Topmost recoverable error | Parent already restored to Pause or Main Menu, or absent over gameplay | Audit current flows; topmost routing defects may be fixed |

The row set is a minimum, not permission to collapse distinct observed phases. If implementation discovers another player-facing phase, it adds a new ID and documents why it was absent from this design before HPA-376 can close.

### 7.1 Per-flow lifecycle matrix

Each flow records:

- Flow and phase
- Entry point
- Parent context
- Current scene or controller owner
- Observed tree-pause behavior
- Required tree-pause behavior
- Gameplay-input blocking, including which of `IsInBattle`, `IsInNpcInteraction`, and `IsInWorldInteraction` owns the block
- HUD visibility
- Mouse policy
- Initial focus
- Input surface and Cancel/back receiver
- Nested popup behavior
- Close or result signal
- Close/result emission count under repeated or competing triggers
- Cleanup owner
- Focus, pause, and input restoration target
- Disposition
- Protecting regression-test evidence

Unknown or absent behavior is written explicitly. The audit does not infer focus, cursor, or restoration behavior merely because Godot may provide a transient default.

For rows marked for replacement, required cursor behavior is copied from the corresponding HPA-373 section 7.3 screen row rather than left as “unknown.” Current mouse ownership may still be recorded as absent.

### 7.2 Modal-priority matrix

The priority matrix records which state receives Cancel and what becomes active afterward.

“Cancel” is shorthand for a family of input surfaces, not a single action. Every lifecycle row identifies which of these can reach it and which owner consumes each:

1. The `pause_menu` input action routed by `Game`
2. The Godot `ui_cancel` action received by controls and dialogs
3. Flow-specific toggles that also close an active surface, currently `toggle_inventory`
4. Dialog/window `CloseRequested`
5. Explicit Close, Cancel, Resume, No Thanks, Continue, or confirmation buttons

The current settings binding layer mirrors the configured Pause binding into `ui_cancel`; the audit records that translation rather than assuming the two actions are interchangeable. Remapped, unbound, keyboard, and controller paths must continue to resolve to one topmost owner.

The required priority is:

1. Active child or capture surface, including an `OptionButton`, key capture, or overwrite confirmation
2. Topmost blocking error or confirmation
3. Active deferred restoration guard, which consumes re-entrant back input without changing the visible stack
4. Owning screen, modal, or world-interaction flow: settings, save/load, inventory, puzzle, treasure/world interaction, NPC, or battle
5. Parent screen such as Pause
6. Gameplay fallback, where Cancel opens Pause

Exactly one applicable layer handles each Cancel event.

Key exceptions remain explicit:

- An open `OptionButton` receives Cancel before Settings.
- Active key capture receives Cancel before Settings, except while capturing the Pause action when Escape is a valid candidate binding.
- An overwrite confirmation closes without dismissing save/load.
- While `_pauseMenuRestorePending` is true, `pause_menu` is consumed as a no-op so the closing child cannot immediately toggle the parent being restored. HPA-376 preserves that outcome; HPA-378 may replace the boolean mechanism with an atomic navigation transition.
- NPC and puzzle dialogs receive Cancel without Pause opening behind them.
- A cancelable world-interaction modal such as a puzzle/riddle receives Cancel itself. A non-cancelable atomic world interaction such as treasure opening blocks inventory and Pause until its `IsInWorldInteraction` cleanup completes.
- Battle preparation and automatic combat use the existing battle escape policy. The legacy battle Continue surface remains Cancel-dismissible, but it remains topmost even after the battle domain flag has cleared; HPA-376 routes it through `BattleManager.ForceCloseAsEscape()`, consumes the input, and prevents Pause from opening behind it.
- Pause and Save/Load quit-to-title actions retain their current cleanup semantics, but HPA-378/379 must insert HPA-373's explicit quit-with-risk confirmation before navigation.
- A producer-designated required acknowledgement is distinct from the legacy battle Continue surface. It does not dismiss through a generic Cancel path and is implemented by downstream host/reward presentation work.

## 8. Transition and restoration rules

Even while the legacy controllers remain in charge, lifecycle behavior follows these rules:

1. Opening a flow records the parent state relevant to later restoration before changing visibility, pause, or focus.
2. Opening a child makes its parent inert while preserving it for restoration.
3. Closing is idempotent. Cleanup and close/result signals occur once even if Cancel, window close, confirmation, and deferred cleanup overlap.
4. Closing a child restores only its parent.
5. Closing the outermost presentation restores gameplay.
6. Deferred restoration remains where the current event could otherwise close a child and immediately toggle its parent. While that restoration is pending, the same back input is consumed as an intentional no-op and cannot reach the parent or gameplay fallback.
7. Each controller owns its timers, signal disconnection, and transient child cleanup.
8. An invalid restoration target falls back to the documented safe target for the parent. Common host-owned focus fallback is deferred to HPA-378, but the required target is still recorded now.
9. Idempotency is measured by observable effects, not only by the absence of exceptions. Each closable controller records signal/result count and cleanup count for double Cancel, Cancel plus button, Cancel plus `CloseRequested`, and any overlapping deferred free/cleanup path that can occur in that flow.

The audit explicitly exercises both currently guarded controllers (`PauseMenuDialog`, `ShopDialog`, and `BattleManager`) and currently unguarded close paths (`DialogueDialog`, `HealDialog`, and `PuzzleRiddleDialog`). This is an audit target, not a pre-commitment to edit every controller: a production guard is added only when a failing contract test demonstrates duplicate emission or cleanup.

### 8.1 Known battle-result routing correction

HPA-376 owns one known cross-controller defect. While a battle result, defeat, or escape Continue surface is valid and visible, `Game` routes Cancel to that presentation before considering Pause even when `GameManager.IsInBattle` is already false.

The correction:

- Preserves the existing timing and payload of `BattleFinished`.
- Preserves the legacy Continue surface's existing Cancel dismissal.
- Does not turn that surface into the future reward constellation.
- Prevents Pause from being created, shown, or toggled behind the result.
- Does not emit another battle result when dismissal happens after `_resultEmitted`.
- Calls `BattleManager.ForceCloseAsEscape()`, then calls `GetViewport().SetInputAsHandled()` and returns. The correction does not depend on the same physical input also propagating as `ui_cancel` to `AcceptDialog`.

`GameInputLifecycleTest.BattleResultCancelIsHandledAndClosesResultWithoutOpeningPause` is the required protecting cross-controller test. Controller-local tests separately prove exactly-once battle result emission and cleanup.

### 8.2 Defeat return-timer ownership

The current defeat path subscribes `ReturnToMainMenu` to an unretained two-second `SceneTreeTimer`. The timer cannot be stopped, and the current code retains no timer reference through which to disconnect the known handler, so its callback can navigate after a test or flow has otherwise cleaned up.

HPA-376 makes that delayed transition lifecycle-owned without changing the two-second player-facing delay. The implementation may retain and disconnect the timer handler or guard the callback with an equivalent invalidated lifecycle token. It must:

- Schedule at most one defeat return.
- Prevent stale navigation after `Game` cleanup or replacement.
- Avoid retaining a freed `Game`.
- Remain deterministic under tests without performing a real late scene change.

`GameInputLifecycleTest.DefeatReturnTimerIsOwnedAndDoesNotNavigateAfterCleanup` is the required protecting test. This requirement does not imply that `SceneTreeTimer` signals are inherently undisconnectable; the defect is the current path's lack of retained ownership.

### 8.3 Reward and acknowledgement replacement contract

The lifecycle contract separates current reward domain behavior from downstream presentation:

| Row | Observed behavior | Required migration contract | Disposition |
|---|---|---|---|
| `WORLD-TREASURE` | Treasure is granted silently, its cell is cleared, and `IsInWorldInteraction` blocks competing input until cleanup. There is no reward modal. | Preserve grant, cell-clear, and cleanup semantics. The controller may later produce an already-resolved presentation request, but UI code never grants or reconstructs the reward. | Preserve domain behavior; presentation is downstream |
| `REWARD-TOAST` | No shared surface exists. | A brief single reward uses a queued, non-blocking toast: no tree pause, gameplay HUD retained, cursor unchanged, no initial focus, no Cancel ownership, and queue/lifetime cleanup that restores nothing because gameplay never yielded input. | `Replace in HPA-378/379` |
| `REWARD-BLOCKING` | Battle loot is embedded in the legacy battle presentation; no shared constellation exists. | Important or multiple rewards, battle results, and producer-designated required acknowledgements use a blocking constellation: parent inert/paused as required, competing HUD hidden, cursor visible, Continue focused when actionable, generic Cancel unable to dismiss a required acknowledgement, and restoration to the producer-provided continuation target. | `Replace in HPA-378/379`; coordinate presentation handoff guarantees with HPA-393 |

HPA-376 records these targets but does not create reward event identity, payloads, queues, constellations, grants, saves, or navigation.

## 9. Permitted production changes

Production edits must stay narrowly tied to a failing lifecycle regression.

Inventory prior-pause restoration is the one explicit forward-compatibility exception: a synthetic test starts with `SceneTree.Paused == true` because HPA-373 approves Pause as a legal Inventory parent even though that state is not player-reachable in the current controller graph. The correction may only remember and restore the incoming value; it does not authorize root Pause ownership or process-mode work.

Permitted changes include:

- Remembering and restoring a prior tree-pause value for a locally owned flow such as Inventory; this does not authorize changing root Pause ownership
- Exactly-once close or result guards
- Timer ownership, signal disconnection, or lifecycle guards needed to prevent stale callbacks, including the defeat return-to-menu delay
- Cleanup that prevents `GameManager` interaction flags from remaining stuck
- Topmost battle-result input routing while the legacy presentation remains visible
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

| Suite | Existing evidence to retain | HPA-376 gap |
|---|---|---|
| `tests/game/GameTest.cs` | Settings key-capture and dropdown guards; save child dismissal and Pause restoration; NPC, puzzle, and world-interaction blocking | Extend only fixture-adjacent cases; place broader topmost-Cancel, active-error, battle-result, floor-transition prompt, deferred-restoration, quit-path, and restoration scenarios in the focused Game lifecycle suite below |
| `tests/ui/InventoryMenuControllerTest.cs` | Active-skill selection behavior | Open/close visibility, Cancel/toggle handling, pausing, and prior-pause restoration |
| `tests/ui/PauseMenuDialogTest.cs` | Button signals, hide behavior, duplicate close guard, and Resume focus | Add only uncovered Cancel/idempotency cases found by the matrix |
| `tests/ui/SettingsMenuControllerTest.cs` | Extensive Apply/Cancel, focus, input blocking, key-capture, duplicate/reserved key, Pause-binding, and dropdown behavior | Add only a missing staged-state assertion if the audit identifies one |
| `tests/ui/BattleManagerTest.cs` | Combat scheduling, consumable, skill, and turn behavior | Pre-start escape, active escape, timer shutdown, effect cleanup, and exactly-once result emission |
| `tests/ui/ShopDialogTest.cs` | Sell-list refresh and feedback-timer behavior | Cancel, close idempotency, and close-time timer cleanup |

### 10.2 New focused suites

- `tests/ui/MainMenuTest.cs`
- `tests/ui/SaveLoadDialogTest.cs`
- `tests/ui/DialogueDialogTest.cs`
- `tests/ui/HealDialogTest.cs`
- `tests/ui/PuzzleRiddleDialogTest.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameInputLifecycleTest.cs`

Each UI suite tests its controller's local lifecycle. Cross-controller priority, interaction-prompt visibility, floor transitions, deferred restoration, quit cleanup, defeat navigation, and parent restoration belong under `tests/game/`. The focused `GameInputLifecycleTest` is preferred for new lifecycle scenarios so the already-large `GameTest` does not become the sole sink; an assertion may remain in `GameTest` when it directly extends an existing fixture and is materially clearer there.

### 10.3 Evidence mapping

Every lifecycle row classified as `Preserve` or `Fix in HPA-376` names at least one protecting test method. Rows classified as `Replace in HPA-378/379` name the downstream owner and must not claim coverage for behavior the current architecture does not provide.

A replacement row must still specify the target pause, input, HUD, mouse, focus, Cancel, cleanup, and restoration behavior precisely enough for HPA-378 or HPA-379 to implement it without repeating the source audit.

## 11. Failure handling

Failure behavior must be deterministic:

- A failed open leaves the parent usable or restores it before surfacing the error.
- Repeated close or cleanup calls are harmless.
- Dismissing a child does not dismiss its parent.
- Battle escape stops the turn timer, resolves battle state once, and emits one result.
- Defeat cleanup invalidates its delayed return-to-menu callback so a stale timer cannot navigate after the owning `Game` lifecycle ends.
- NPC and world-interaction failure paths clear their corresponding `GameManager` flags.
- Corrupted-save and save/load errors remain topmost. Their already-restored parent may be Pause, Main Menu, or gameplay; Cancel dismisses only the error and never opens, closes, or toggles Pause as a side effect.
- Test cleanup frees transient dialogs and disconnects timers so new tests introduce no orphan-node warnings.

Existing orphan-node warnings are recorded as baseline evidence. HPA-376 does not expand into unrelated test-suite cleanup. The contract records, for each applicable controller, the observed result of double Cancel, Cancel plus explicit button, Cancel plus `CloseRequested`, and repeat cleanup; “did not throw” is insufficient if a signal or domain result was emitted twice.

## 12. Verification

Implementation verification proceeds in layers:

1. Run each changed or new controller suite.
2. Run the affected `Game*` suites for cross-controller priority, prompt visibility, and restoration.
3. Run the complete suite with `dotnet test Sirius.sln --settings test.runsettings.local`.
4. Compare orphan-node output with the deterministic baseline capture below and reject newly introduced warnings.
5. Check the lifecycle contract against HPA-373 section 7 and this document's section 13 completion criteria.

The full-suite success condition is every test present at implementation start plus all newly added tests, with zero failures or skips. The historical reference is 869 passing tests at source baseline `bc82ead`; a changed upstream count is not itself a failure.

Before implementation edits, capture the full suite at the implementation-start commit:

```bash
zsh -o pipefail -c 'dotnet test Sirius.sln --settings test.runsettings.local 2>&1 | tee /tmp/hpa-376-test-baseline.log'
rg -i -c "orphan" /tmp/hpa-376-test-baseline.log
rg -i "orphan" /tmp/hpa-376-test-baseline.log
```

After implementation, repeat with `/tmp/hpa-376-test-after.log`. The committed lifecycle contract records both commit IDs, test totals, orphan-line counts, and distinct orphan messages. The `/tmp` logs are evidence inputs and are not committed.

## 13. Completion criteria

This section embeds the live Linear HPA-376 acceptance criteria fetched on 2026-07-26 and adds the design-specific evidence gates. HPA-376 is complete when:

- Every ID in section 7.0, plus any newly discovered player-facing phase, has a source-evidenced lifecycle row with explicit ownership.
- Pause, input, HUD, mouse, focus, Cancel, signal, cleanup, and restoration expectations are explicit.
- Existing key-rebinding and nested-popup exceptions are covered by tests.
- Battle preparation, active interruption, timer cleanup, result emission, still-visible result priority, handled-input semantics, and defeat-return ownership are covered before the dialog is replaced.
- Inventory open/close and synthetic prior-pause restoration, save/load restoration and quit paths, Pause quit and deferred restoration, Main Menu quit, NPC cancellation, floor-transition prompt restoration, world-interaction cleanup, error dismissal, interaction-prompt visibility, and topmost-only Cancel are covered at the approved boundary.
- The modal-priority/state matrix can be implemented directly by HPA-378.
- Every `Preserve` or `Fix in HPA-376` contract names automated evidence; every replacement names its downstream owner and complete target behavior.
- All existing tests and all HPA-376 tests pass with zero failures or skips, and HPA-376 introduces no orphan-node warning.
- No `UIScreenHost`, broad UI redesign, or domain-rule change is included.
