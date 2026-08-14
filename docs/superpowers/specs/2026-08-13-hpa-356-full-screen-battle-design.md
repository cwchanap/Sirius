# HPA-356 Full-Screen Battle Flow Design

**Status:** Planning candidate  
**Linear:** HPA-356 — Replace the Sirius battle popup with a full-screen battle flow while preserving preparation  
**Date:** 2026-08-13

## 1. Decision

Replace the desktop `AcceptDialog` battle with one scene-authored, full-screen Sirius battle screen while preserving the existing combat rules, timing, item behavior, reward granting, and lifecycle semantics.

Keep the architecture deliberately small:

- one `BattleManager`, changed from `AcceptDialog` to `Control`;
- one `BattleScene.tscn` with Preparation, Automatic Combat, and Results presentation states;
- `Game` / the existing gameplay `UIScreenHost` own attachment, gameplay blocking, HUD visibility, cursor, focus, Cancel routing, and final node lifetime;
- one narrow immutable `BattleResultSummary` containing already-resolved victory/defeat rewards;
- existing `SiriusStatBar`, `SiriusItemSlotController`, Theme, art, `SiriusUiMetrics`, and focus/host infrastructure are reused;
- one local bounded string feed and timer-derived progress display; neither becomes a combat-domain abstraction.

No `BattleSession`, combat service, state-machine framework, event bus, battle view model, presenter layer, generic item picker, reward protocol, new Theme token, new global metric, or host API is introduced.

## 2. Review-driven corrections

The planning review found seven concrete gaps. All are accepted, with one sequencing refinement.

### 2.1 The base-class cutover and Game lifecycle cutover are atomic

Changing `BattleManager : AcceptDialog` to `Control` while leaving `Game` calling `Confirmed`, `PopupWindow`, `Exclusive`, `PopupCentered()`, and `ForceCloseAsEscape()` creates an intermediate commit that cannot build.

HPA-356 therefore moves the base-class change and `Game` host registration in the **same implementation task**. There is no compatibility shim and no temporary direct-parent lifecycle. `UIScreenKinds.Battle` already exists, so hosting immediately is simpler than creating a disposable `UI.AddChild` path.

### 2.2 Responsive layout follows the existing SafeFrame contract

Battle does not expose a public `SetCompact` API.

Like Inventory and Exploration HUD, Battle owns a private `RefreshLayout()` that:

1. returns if detached/not inside the tree;
2. reads `GetViewportRect().Size`;
3. calls `SiriusUiMetrics.SafeFrameInsets(viewportSize)`;
4. applies the returned side/top/bottom offsets to `%SafeFrame`;
5. stores private `_isCompact`;
6. applies compact presentation to stat bars, item slots, telemetry visibility, feed limit, and item page size.

This guarantees the existing 24 px standard / 12 px compact margins and centered 1600 px maximum content width.

### 2.3 Existing damage feedback remains part of the scene contract

The current `ShowDamageNumber` behavior is preserved. The full-screen scene authors:

- `%PlayerSpriteContainer`;
- `%EnemySpriteContainer`;
- `%PlayerDamageLabel`;
- `%EnemyDamageLabel`.

Visual tweens created by damage numbers and attack flashes are tracked by `BattleManager` and killed during terminal/teardown cleanup. A battle must not leave a tween callback targeting stale presentation after host teardown.

### 2.4 Cancel is child-first inside Battle

`RequestCancel()` first checks the local cure overlay.

- If `%CureOverlay` is visible, Cancel closes only that overlay and resumes the timer when combat is still active.
- Otherwise Preparation/Automatic Combat Cancel follows the existing immediate-escape path.
- Result Cancel dismisses only Results and never emits `BattleFinished` again.

The host always delegates Battle Cancel to this one method; it does not decide the phase itself.

### 2.5 Preparation errors stay in Preparation

The existing `ShowItemPanelError` path cannot survive removal of runtime `_itemPanel`.

Pre-battle remove/apply/rollback failures write a readable error to `%PreparationItemDetails`, keep Preparation visible, preserve the selected item when appropriate, and do not start the battle timer.

### 2.6 Lifecycle characterization precedes the cutover

Before changing the root type, retain the existing characterization for:

- preparation escape emits once and closes;
- automatic-combat escape stops timer/clears effects/emits once;
- configured Cancel on a visible result closes the result without opening Pause.

Add explicit characterization that victory/defeat `BattleFinished` emission leaves the current result surface visible for explicit dismissal. The same semantic tests are migrated to `RequestCancel` / hosted Results in the atomic cutover task.

### 2.7 Feed trimming and stale-popup verification are exact

`RefreshEventFeed()` trims `_combatEvents` to the current `EventFeedLimit` before joining. Resizing from standard (5) to compact (3) therefore immediately removes older rows.

Final popup greps are Battle-scoped. Unrelated `AcceptDialog` errors in `Game.cs` remain valid owners until HPA-572; HPA-356 does not delete them merely to satisfy a broad search.

## 3. Current-state facts that shape the migration

### 3.1 Battle is still a native window

`BattleScene.tscn` is rooted at `AcceptDialog` with a fixed 800×600 size. `Game.OnBattleStarted` instantiates it, parents it directly under `UI`, connects `Confirmed`, configures popup/exclusive behavior, calls `PopupCentered()`, and contains Battle-specific root-Cancel handling.

`BattleManager` also owns native-window details through `Title`, `GetOkButton()`, `CloseRequested`, `Canceled`, and `ForceCloseAsEscape()`.

HPA-356 removes those window responsibilities rather than wrapping the dialog.

### 3.2 The combat engine already has the required behavior

Keep the existing implementations for:

- 1.5-second battle timer;
- action-point accumulation and speed ordering;
- exact-tie alternation;
- automatic attack/defend behavior;
- active/passive skills, mana, cooldowns, and status effects;
- pre-battle consumable remove → apply → rollback ordering;
- enemy-targeting preparation items;
- mid-battle cure-only use;
- damage calculation and current damage/attack animations;
- victory XP/gold grant;
- level-up and skill unlock grant;
- loot roll and inventory award;
- transient effect cleanup.

The migration changes presentation/lifecycle ownership, not combat rules.

### 3.3 The host already has a Battle kind

`UIScreenKinds.Battle` exists. `Game` already hosts Pause, Inventory, Settings, Save/Load, and confirmations.

Battle uses the same scene-local host. No host capability is missing.

### 3.4 Escape remains immediate

The earlier HPA-373 visual wireframe includes escape among result concepts, but the later HPA-376 lifecycle contract and current tests freeze the public behavior more precisely:

- preparation Cancel escapes immediately;
- active-combat Cancel/Escape stops the timer, clears transient effects, emits `BattleFinished(false, true)` once, and closes immediately;
- there is no reachable escape-result Continue surface.

HPA-356 preserves that contract. Only victory and defeat enter Results.

### 3.5 Victory/defeat emit before dismissal

`EndBattle` currently grants rewards and emits `BattleFinished` before the result dialog is dismissed. `Game` uses that signal to clear `GameManager.IsInBattle`, remove the defeated enemy on victory, update player state, and schedule defeat navigation.

The hosted redesign preserves immediate result emission while keeping the Battle host entry active until explicit Result dismissal (or the existing defeat navigation tears down the scene).

## 4. Controller contract

After the atomic cutover:

```csharp
public partial class BattleManager : Control
{
    [Signal] public delegate void BattleFinishedEventHandler(
        bool playerWon,
        bool playerEscaped);

    [Signal] public delegate void DismissRequestedEventHandler();

    public Control? InitialFocusTarget { get; }
    public BattleResultSummary? ResolvedResult { get; }

    public void StartBattle(Character player, Enemy enemy);
    public void RequestCancel();
}
```

The phase remains private:

```csharp
private enum BattlePhase
{
    Preparation,
    AutomaticCombat,
    Result
}
```

`RequestCancel()` is the only Battle Cancel entry:

```text
Cure overlay visible     -> close cure overlay only
Preparation              -> immediate escape + dismiss
Automatic Combat         -> immediate escape + dismiss
Result (victory)         -> dismiss only; no second result emission
Result (defeat)          -> Cancel is consumed with no dismiss and no second
                            result emission; host teardown owns the exit
```

Continue is shown and focused only for victory Results; defeat never presents
Continue, so Cancel during a defeat Result has nothing to activate and is
consumed silently.

`InitialFocusTarget` resolves to Begin Battle, Escape, or Continue by phase, with the first actionable cure/Cancel owning focus while the cure overlay is visible.

Responsive state remains private (`_isCompact`) and is derived from `RefreshLayout()`.

## 5. Resolved result value

Create exactly:

```csharp
public sealed record BattleResultSummary(
    bool PlayerWon,
    int ExperienceGained,
    int GoldGained,
    int PreviousLevel,
    int NewLevel,
    LootResult Loot);
```

Rules:

- Victory captures already-granted XP, gold, old/new level, and already-awarded `LootResult`.
- Defeat uses zero XP/gold, unchanged level, and `LootResult.Empty`.
- Escape never creates a summary because it never enters Results.
- UI only reads this value.
- It has no IDs, timestamps, persistence, acknowledgement state, retries, or grant methods.

## 6. Scene structure

The final `BattleScene.tscn` is a full-rect `Control` owning `SiriusTheme.tres`.

```text
BattleScreen (BattleManager)
├── BattleBackground
├── Scrim
└── SafeFrame
    └── BattleContent
        ├── Header
        │   ├── EncounterLabel
        │   └── PhaseLabel
        ├── ActorField
        │   ├── PlayerAnchor
        │   │   ├── PlayerSpriteContainer
        │   │   │   └── PlayerSprite
        │   │   ├── PlayerDamageLabel
        │   │   ├── PlayerName / PlayerLevel
        │   │   ├── PlayerHealth / PlayerMana
        │   │   ├── PlayerAttack / PlayerDefense / PlayerSpeed
        │   │   └── PlayerStatus
        │   ├── CenterFlow
        │   │   ├── PreparationPanel
        │   │   │   ├── ActiveSkillSummary
        │   │   │   ├── PreparationItemRail
        │   │   │   ├── PreparationItemDetails
        │   │   │   ├── PreviousItemPage / NextItemPage
        │   │   │   ├── ClearPreparationItemButton
        │   │   │   └── BeginBattleButton
        │   │   ├── AutomaticCombatPanel
        │   │   │   ├── CurrentActionLabel
        │   │   │   ├── AutomaticActionProgress
        │   │   │   ├── EventFeed
        │   │   │   ├── CureButton
        │   │   │   └── EscapeButton
        │   │   └── ResultPanel
        │   │       ├── ResultTitle
        │   │       ├── ExperienceResult / GoldResult / LevelResult
        │   │       ├── LootResultList
        │   │       └── ContinueButton
        │   └── EnemyAnchor
        │       ├── EnemySpriteContainer
        │       │   └── EnemySprite
        │       ├── EnemyDamageLabel
        │       ├── EnemyName / EnemyLevel
        │       ├── EnemyHealth
        │       ├── EnemyAttack / EnemyDefense / EnemySpeed
        │       └── EnemyStatus
        └── CureOverlay
            └── CurePanel
                ├── CureItemList
                └── CancelCureButton
```

The background texture moves from runtime creation into the scene. No custom orbit renderer is added.

## 7. Preparation

Preparation preserves explicit Begin Battle.

1. Bind combatants and stats.
2. Build current consumables from `Character.Inventory`.
3. Standard renders four slots/page; compact renders three.
4. Grow/reuse/shrink `SiriusItemSlotController` exactly like Inventory's dynamic slot pattern.
5. Focus updates `%PreparationItemDetails`.
6. Selection changes only `_selectedConsumable` and slot presentation.
7. Begin Battle performs the existing remove/apply/rollback transaction.
8. Failure writes to `%PreparationItemDetails` and stays in Preparation.
9. Success recalculates effective speed and starts the existing timer.

No preparation modal, projected combat simulator, drag/drop, or new target-selection model is added.

## 8. Automatic Combat

The final Automatic Combat view:

- keeps player/enemy anchors spatially stable;
- runs the existing timer/AP logic unchanged;
- shows HP/MP/status/current action;
- preserves `ShowDamageNumber` and attack flash feedback;
- exposes a bounded event feed;
- exposes Cure and Escape;
- contains no manual Attack/Defend/battle-speed/general-pause/skill-editing controls.

### 8.1 Event feed

Use `Queue<string> _combatEvents`.

`AppendCombatEvent` appends presentation copy beside existing `GD.Print` sites. `RefreshEventFeed` always trims to the current `EventFeedLimit` before rendering, so resize changes are reflected immediately.

No combat behavior depends on the feed.

### 8.2 Progress

`AutomaticActionProgress` reads `_battleTimer.TimeLeft / _battleTimer.WaitTime` from `_Process` only while Automatic Combat is active. It never advances combat.

### 8.3 Cure overlay

Cure is a local child surface:

- opening stops the timer;
- only `CureStatusEffect` items are actionable;
- unsupported consumables remain focusable with a reason;
- selection uses the existing remove/apply/rollback path;
- Cancel closes the overlay before Battle escape is considered;
- timer restarts only if the battle is still in Automatic Combat and both combatants are alive.

No extra `UIScreenKind` or host entry is created.

## 9. Results

### Victory

Keep grant order:

1. stop runtime combat and clear battle-scoped effects;
2. record previous level;
3. grant XP/gold;
4. grant newly unlocked skills;
5. roll/award loot;
6. create `BattleResultSummary`;
7. render Results;
8. emit `BattleFinished(true, false)` once.

Results show Victory, XP, gold, optional level change, loot/no-loot, and Continue.

### Defeat

Create a zero-reward summary, render Defeat, emit `BattleFinished(false, false)` once, and preserve the existing delayed return-to-title behavior.

### Escape

Escape never enters Results:

1. close local cure overlay first if it is open;
2. otherwise stop timer;
3. clear transient effects;
4. emit `BattleFinished(false, true)` once;
5. request host dismissal;
6. keep the enemy and grant no rewards.

## 10. Host lifecycle

The base-class cutover and host registration happen together.

`Game` owns `_battleHandle`. It instantiates `BattleManager` unparented and presents it immediately:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Battle,
    Layer = UIScreenLayer.Screen,
    InputPriority = UIInputPriority.Blocking,
    ProcessPolicy = UIProcessPolicy.Always,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
    LowerLayers = UILowerLayerPolicy.Hidden,
    Cancel = UICancelPolicy.Consume,
    InitialFocus = () => battle.InitialFocusTarget,
    InterceptCancel = _ =>
    {
        battle.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    Cleanup = _ => ClearBattlePresentation(battle),
    NodeLifetime = UINodeLifetime.QueueFree
};
```

`DismissRequested` closes the Battle handle. `BattleFinished` continues to own world/domain consequences.

Delete native Battle responsibilities in that same cutover:

- direct `UI.AddChild`;
- `Confirmed`;
- `PopupWindow`;
- `Exclusive`;
- `PopupCentered()`;
- `OnBattleDialogConfirmed`;
- `ForceCloseAsEscape`;
- Battle-specific root-Cancel branches.

Unrelated native error dialogs remain untouched.

## 11. Runtime and visual teardown

Use one idempotent helper:

```text
StopBattleRuntime
  stop timer
  clear battle-scoped player/enemy effects
  kill tracked visual tweens
  prevent cure timer restart after terminal state
```

Track tweens created by both `ShowDamageNumber` and `PlayAttackAnimation`. Finished tweens remove themselves from the tracked set/list; teardown kills any survivors.

`_ExitTree()` calls `StopBattleRuntime()` but never emits `BattleFinished`.

Host teardown, escape, victory/defeat transition to Results, and owner scene replacement cannot leave timer/tween callbacks targeting stale Battle UI.

## 12. Responsive behavior

`RefreshLayout()` follows the existing Inventory/HUD safe-frame pattern:

```csharp
if (!GodotObject.IsInstanceValid(this) ||
    _safeFrame == null ||
    !IsInsideTree())
{
    return;
}

var insets = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
_isCompact = insets.Compact;

_safeFrame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
_safeFrame.OffsetLeft = insets.SideInset;
_safeFrame.OffsetTop = insets.Margin;
_safeFrame.OffsetRight = -insets.SideInset;
_safeFrame.OffsetBottom = -insets.Margin;
```

Then apply compact state to existing stat bars/slots and feature-local visibility. Guard preparation-item refresh until `_player` is bound.

Standard 1280×720:

- opposing anchors;
- four preparation items/page;
- five event rows;
- complete Results rows inside SafeFrame.

Compact 640×360:

- three preparation items/page;
- three event rows;
- nonessential ATK/DEF/SPD hidden first;
- essential text ≥14 px;
- action targets ≥40×40;
- Results reward list may scroll while Continue stays reachable.

No portrait/mobile layout.

## 13. File ownership

### Create

- `scripts/data/BattleResultSummary.cs`
- `tests/ui/BattleSceneTest.cs`

### Modify

- `scenes/ui/BattleScene.tscn`
- `scripts/ui/BattleManager.cs`
- `tests/ui/BattleManagerTest.cs`
- `scripts/game/Game.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Audit-only unless a concrete stale assumption is found

- `scripts/ui/hosting/UIScreenKinds.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`

No `Character`, `Enemy`, skill, status-effect, loot-table, `LootManager`, Inventory, save-format, or encounter-domain production changes are expected.

## 14. Test strategy

### Before cutover

Preserve existing characterization for preparation/active escape and configured result Cancel. Add an explicit current-dialog test proving `BattleFinished` result emission does not dismiss the visible result.

### Atomic cutover

Before implementation, write RED host tests for:

- one hosted `UIScreenKinds.Battle` entry;
- gameplay blocked / HUD hidden / no tree pause;
- configured Cancel delegates to Battle and never opens Pause;
- victory/defeat clears domain battle state but leaves Results hosted;
- Result Cancel closes only Battle and does not re-emit.

After cutover, migrate old `ForceCloseAsEscape`/native-result tests to `RequestCancel`/hosted equivalents.

### Presentation

Cover:

- root is `Control`, not `Window`;
- damage labels and sprite containers are authored;
- preparation failure stays visible in `%PreparationItemDetails`;
- 4/3 item paging;
- exact SafeFrame offsets from `SafeFrameInsets`;
- cure Cancel closes overlay first;
- timer restarts only when combat remains active;
- feed re-trims 5→3 on resize;
- progress remains presentation-only;
- tracked visual tweens die on runtime stop/teardown;
- result summary/reward ordering;
- deep 1280×720 / 640×360 containment and light checks at remaining shared viewports.

Do not build a phase × viewport × outcome matrix.

## 15. Implementation sequence

1. **Characterize lifecycle + add result value:** preserve current escape/result-Cancel tests, add result-visible-after-emission characterization, and add immutable `BattleResultSummary`.
2. **Atomic hosted-Control cutover:** change root type and `Game` integration together; replace native close/Continue with `RequestCancel` / `DismissRequested`; host immediately so the task builds and runs.
3. **Full presentation migration:** apply Inventory-style SafeFrame layout, shared stat/slot components, preparation paging/errors, authored damage labels, tracked visual tweens, feed/progress, local cure overlay, and responsive Results.
4. **Lifecycle reconciliation + verification:** update only HPA-376 Battle rows, run focused/full suites/build, use Battle-scoped stale-popup searches, and audit exact diff scope.

Every task ends buildable. No task introduces an `AcceptDialog` compatibility shim or temporary direct-parent lifecycle.

## 16. YAGNI review

Keep one `BattleManager`, private `BattlePhase`, one result record, one local string queue, the existing host, and existing Theme/components/metrics.

Do not add battle/session/service architecture, host APIs/kinds, generic pickers, event models/buses, Theme/metric expansion, visible escape Results, manual combat/battle speed/general pause/skill editing, or reward identity/persistence/acknowledgement.
