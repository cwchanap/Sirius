# HPA-356 Full-Screen Battle Flow Design

**Status:** Planning candidate  
**Linear:** HPA-356 — Replace the Sirius battle popup with a full-screen battle flow while preserving preparation  
**Date:** 2026-08-13

## 1. Decision

Replace the desktop `AcceptDialog` battle with one scene-authored, full-screen Sirius battle screen while preserving the existing combat rules, timing, item behavior, feedback, reward granting, and lifecycle semantics.

Keep the architecture deliberately small:

- one `BattleManager`, changed from `AcceptDialog` to `Control`;
- one `BattleScene.tscn` with Preparation, Automatic Combat, and Results presentation states;
- `Game` / the existing gameplay `UIScreenHost` own attachment, gameplay blocking, HUD visibility, cursor, focus, Cancel routing, and final node lifetime;
- one narrow `BattleResultSummary` record containing already-resolved victory/defeat data for Results presentation;
- existing `SiriusStatBar`, `SiriusItemSlotController`, Theme, art, `SiriusUiMetrics`, and focus/host infrastructure are reused;
- one local bounded string feed and timer-derived progress display; neither becomes a combat-domain abstraction.

No `BattleSession`, combat service, state-machine framework, event bus, battle view model, presenter layer, generic item picker, reward protocol, new Theme token, new global metric, or host API is introduced.

## 2. Review-driven corrections

Two planning reviews were checked against current `main`. The architecture stays the same; the accepted changes make deletion and sequencing explicit.

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

This preserves the existing 24 px standard / 12 px compact margins and centered 1600 px maximum content width.

### 2.3 Existing damage feedback remains part of the scene contract

The current `ShowDamageNumber` and attack-flash behavior is preserved. The full-screen scene authors:

- `%PlayerSpriteContainer`;
- `%EnemySpriteContainer`;
- `%PlayerDamageLabel`;
- `%EnemyDamageLabel`.

Visual tweens created by damage numbers and attack flashes are tracked by `BattleManager` and killed during terminal/teardown cleanup. A battle must not leave a tween callback targeting stale presentation after host teardown.

### 2.4 Cancel is child-first inside Battle

`RequestCancel()` first checks the local cure surface.

During the atomic cutover, the still-existing runtime combat item panel is child-first. After the final cure rewrite, `%CureOverlay` is child-first.

- Cure surface visible → Cancel closes only that surface and resumes the timer when combat is still active.
- Preparation / Automatic Combat → call the de-native-ized existing `EndBattleWithEscape()`, then request dismissal.
- Result → request dismissal only; never emit `BattleFinished` again.

The host always delegates Battle Cancel to this one method; it does not inspect phase.

### 2.5 Preparation errors stay in Preparation

The existing `ShowItemPanelError` path cannot survive removal of runtime `_itemPanel` preparation UI.

Pre-battle remove/apply/rollback failures write a readable error to `%PreparationItemDetails`, keep Preparation visible, preserve the selected item when appropriate, and do not start the battle timer.

### 2.6 Existing characterization is enough before cutover

Before changing the root type, retain and run the existing characterization for:

- preparation escape emits once and closes;
- automatic-combat escape stops timer/clears effects/emits once;
- configured Cancel on a visible result closes the result without opening Pause.

Do not add constructor/property round-trip tests for `BattleResultSummary`; those test C# record behavior rather than Sirius behavior. Do not add a native `GetOkButton().Visible` result-linger test that is deleted one task later. Hosted result persistence is covered by the Task 2 host test.

### 2.7 Feed trimming and stale-popup verification are exact

`RefreshEventFeed()` trims `_combatEvents` to the current `EventFeedLimit` before joining. Resizing from standard (5) to compact (3) therefore immediately removes older rows.

Final popup greps are Battle-scoped. Unrelated `AcceptDialog` errors in `Game.cs` remain valid owners until HPA-572; HPA-356 does not delete them merely to satisfy a broad search.

### 2.8 Legacy runtime loot presentation is deleted, not layered under Results

Current victory behavior stores `_pendingLootDisplay`, defers `ShowPendingLootDisplay()`, and dynamically adds `_lootLabel` to `BattleContent` through `ShowLootDisplay()`.

The final Results panel replaces that presentation completely. HPA-356 deletes:

- `_pendingLootDisplay`;
- `_lootLabel`;
- the `StartBattle` reset/cleanup block for those fields;
- `ShowPendingLootDisplay()`;
- `ShowLootDisplay()`;
- the deferred call from victory.

Loot is rolled/awarded exactly once, stored in `BattleResultSummary.Loot`, and rendered only through `%LootResultList`.

### 2.9 Invalid combatants must not self-free a hosted node

Current `StartBattle` emits escape and then calls `Hide(); QueueFree();` when player or enemy is null. That is unsafe after `UIScreenHost` owns the node with `UINodeLifetime.QueueFree`.

After the atomic cutover the invalid-combatant path is:

```csharp
EmitBattleFinishedOnce(false, true);
RequestDismiss();
return;
```

It never directly hides/frees the hosted node.

### 2.10 `OnCloseRequested` is deleted; `EndBattleWithEscape` survives without native UI

`OnCloseRequested` exists only for native-window close semantics and is removed with `AcceptDialog`.

`EndBattleWithEscape` remains the one battle cleanup/result owner for escape, but its `GetOkButton()` writes are removed. It stops battle runtime, logs the escape, and emits `BattleFinished(false, true)` through the exactly-once gate. `RequestCancel()` delegates to it rather than duplicating timer/effect/result logic.

### 2.11 Final presentation is split at a meaningful review boundary

The final scene rewrite, SafeFrame, preparation-slot migration, and preparation-error migration are one task with their own focused test/build gate and commit.

Damage/tween feedback, event feed/progress, cure overlay, and structured Results are a following task with another gate and commit.

This keeps node-path migration independently reviewable from later feedback/result behavior.

### 2.12 Gameplay suppression after result emission is a named invariant

`Game.OnBattleFinished` clears `GameManager.IsInBattle` before Results is dismissed. During that interval, player input remains suppressed because the active host entry still has `BlockGameplayInput = true`, which drives `Game._presentationGameplayBlocked` and therefore `IsGameplayInputSuppressed()`.

The hosted victory test explicitly asserts:

```text
GameManager.IsInBattle == false
Battle host entry still active
presentation gameplay blocked == true
IsGameplayInputSuppressed() == true
```

Results must never restore player movement before dismissal.

### 2.13 `BattleResultSummary` is not claimed to be deeply immutable

`LootResult` is a mutable class with public `Add`. A record containing a `LootResult` reference is therefore not deeply immutable.

`BattleResultSummary` is a **non-granting resolved result record**: `BattleManager` constructs it after reward mutation and the Battle UI only reads it. HPA-356 does not change `LootResult` or its chest consumers merely to make the summary deeply immutable.

### 2.14 `SiriusPlayerSummaryPresenter` was surveyed and intentionally not reused

`SiriusPlayerSummaryPresenter.Apply(...)` binds name, level, HP, MP, **and a required EXP `ProgressBar`**. Battle has no actor-panel EXP control.

Using it would require a fake/hidden EXP node or expanding an existing shared API solely for a partial third consumer. HPA-356 instead binds Battle name/level directly and reuses `SiriusStatBar` for HP/MP. Do not change `SiriusPlayerSummaryPresenter` for this ticket.

## 3. Current-state facts that shape the migration

### 3.1 Battle is still a native window

`BattleScene.tscn` is rooted at `AcceptDialog` with a fixed 800×600 size. `Game.OnBattleStarted` instantiates it, parents it directly under `UI`, connects `Confirmed`, configures popup/exclusive behavior, calls `PopupCentered()`, and contains Battle-specific root-Cancel handling.

`BattleManager` also owns native-window details through `Title`, `GetOkButton()`, `CloseRequested`, `Canceled`, `OnCloseRequested`, and `ForceCloseAsEscape()`.

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

The hosted redesign preserves immediate result emission while keeping the Battle host entry active and gameplay blocked until explicit Result dismissal (or the existing defeat navigation tears down the scene).

### 3.6 Current loot has a second runtime presentation path

Victory currently awards loot and then separately defers `ShowPendingLootDisplay()`, which creates an unthemed runtime label. The full-screen Results migration must delete this path, not leave it alongside `%LootResultList`.

### 3.7 Current invalid-combatant cleanup owns its node directly

`StartBattle` currently hides and queues itself on null combatants. Once hosted, node lifetime belongs only to `UIScreenHost`; invalid startup requests a host dismissal instead.

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
Cure surface visible      -> close cure only
Preparation               -> EndBattleWithEscape(); dismiss
Automatic Combat          -> EndBattleWithEscape(); dismiss
Result                    -> dismiss only; no second result emission
```

`InitialFocusTarget` resolves to Begin Battle, Escape, or Continue by phase, with the first actionable cure/Cancel owning focus while the cure overlay is visible.

Responsive state remains private (`_isCompact`) and is derived from `RefreshLayout()`.

## 5. Resolved result record

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
- Battle presentation only reads this record after construction.
- `LootResult` remains the existing mutable domain type; no deep-immutability claim is made.
- The summary has no IDs, timestamps, persistence, acknowledgement state, retries, or grant methods.

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

1. Bind combatants and stats directly to Battle controls; reuse `SiriusStatBar`, not `SiriusPlayerSummaryPresenter`.
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
6. create `BattleResultSummary` from the awarded values and `LootResult`;
7. render `%LootResultList` and the other authored Results fields;
8. emit `BattleFinished(true, false)` once.

The legacy `_pendingLootDisplay` / `_lootLabel` / deferred `ShowLootDisplay` path is deleted. Results has exactly one loot presentation.

### Defeat

Create a zero-reward summary, render Defeat, emit `BattleFinished(false, false)` once, and preserve the existing delayed return-to-title behavior.

### Escape

Escape never enters Results:

1. close local cure surface first if it is open;
2. otherwise call the existing `EndBattleWithEscape()` after its native UI writes are removed;
3. `EndBattleWithEscape()` stops runtime, clears transient effects, and emits `BattleFinished(false, true)` once;
4. `RequestCancel()` requests host dismissal;
5. keep the enemy and grant no rewards.

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
- `OnCloseRequested`;
- `ForceCloseAsEscape`;
- Battle-specific root-Cancel branches;
- invalid-`StartBattle` direct `Hide()` / `QueueFree()`.

Unrelated native error dialogs remain untouched.

### 10.1 Results remains the gameplay-input owner after domain battle ends

When victory/defeat emits `BattleFinished`, `GameManager.IsInBattle` becomes false but the Battle host entry remains active. `BlockGameplayInput = true` keeps `_presentationGameplayBlocked` true, and `PlayerController.GameplayInputSuppressedProvider` continues to resolve true through `Game.IsGameplayInputSuppressed()`.

Only Battle dismissal (or defeat scene teardown) restores gameplay input.

## 11. Runtime and visual teardown

Use one idempotent helper:

```text
StopBattleRuntime
  stop timer
  clear battle-scoped player/enemy effects
  kill tracked visual tweens
  prevent cure timer restart after terminal state
```

`EndBattleWithEscape()` calls this helper after the final feedback task adds tween tracking. Victory/defeat terminal logic also stops runtime before entering Results.

Track tweens created by both `ShowDamageNumber` and `PlayAttackAnimation`. Finished tweens remove themselves from the tracked set; teardown kills any survivors.

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

Then apply stat/slot compact APIs and hide nonessential telemetry before essential text shrinks.

### Standard

At 1280×720 and larger:

- opposing anchors remain stable;
- four preparation items/page;
- five feed entries;
- all Result rows fit or scroll inside the safe frame.

### Compact

At 640×360:

- safe margin = 12 px;
- three preparation items/page;
- feed trims immediately to latest three;
- stat bars use compact presentation;
- nonessential ATK/DEF/SPD hides;
- essential text >= 14 px;
- action targets >= 40×40;
- Result Continue remains reachable.

No portrait/mobile layout is added.

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

### Audit-only unless actual stale evidence is found

- `scripts/ui/hosting/UIScreenKinds.cs` — `Battle` already exists.
- `resources/ui/theme/SiriusTheme.tres` — no new token.
- `scripts/ui/theme/SiriusUiMetrics.cs` — no new metric.
- `scripts/ui/SiriusPlayerSummaryPresenter.cs` — surveyed and intentionally unchanged.
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs` — update only if real battle-node assumptions fail.

No combat-domain/save/inventory-domain production changes are expected.

## 14. Test strategy

Protect behavior rather than compiler-generated data accessors or a combinatorial layout matrix.

### Pre-cutover characterization

Run the existing tests for:

- preparation escape once + immediate close;
- automatic-combat escape timer/effect cleanup + once-only emission;
- visible native result Cancel consumes input before Pause.

No new `BattleResultSummary` property round-trip test and no one-task-only native result-linger test.

### Hosted lifecycle

Cover:

- one `UIScreenKinds.Battle` entry;
- host parentage;
- no tree-pause lease;
- gameplay block + HUD hidden + cursor visible;
- preparation/automatic Cancel escapes without Pause;
- Result Cancel dismisses without a second result;
- victory/defeat clears `GameManager.IsInBattle` while Battle remains hosted;
- while that Results entry remains hosted, `_presentationGameplayBlocked` and `IsGameplayInputSuppressed()` remain true;
- victory enemy removal/autosave remains victory-only;
- escape retains enemy;
- invalid combatants emit/dismiss without self-freeing the host-owned node.

### Scene/layout/preparation task

Cover:

- final unique node paths exist;
- no unsupported manual controls;
- exact `SafeFrameInsets` at 1280×720 / 640×360;
- stat/slot compact behavior;
- preparation page size 4/3;
- pre-battle validation/rollback errors remain visible in Preparation and timer stays stopped.

### Feedback/cure/results task

Cover:

- damage labels remain connected;
- tracked visual tweens are empty after stop/teardown;
- feed re-trims 5→3 on compact resize;
- progress is timer-derived only;
- cure Cancel closes Cure without escape/result/dismiss;
- summary values match what was actually granted and are constructed after grant;
- loot is rendered exactly once through `%LootResultList`;
- no legacy runtime loot presentation remains.

Use deep 1280×720 and 640×360 layout checks. Other shared verification viewports get light containment only.

## 15. Implementation sequence

1. **Characterization + result record:** run the three existing terminal characterizations; add `BattleResultSummary` without trivial property tests.
2. **Atomic hosted Control cutover:** base class + `Game` call sites + host lifecycle + invalid-combatant lifetime + de-native-ized `EndBattleWithEscape`; explicit build gate.
3. **Final scene/layout/preparation:** authored final node paths, SafeFrame, shared stat/item components, preparation paging/error surface; focused build/test gate and commit.
4. **Feedback/cure/results:** damage/tween lifecycle, feed/progress, Cure overlay, structured Results, delete legacy runtime loot/item-panel presentation; second focused build/test gate and commit.
5. **Lifecycle reconciliation/final verification:** update only HPA-376 Battle rows; full tests/build; Battle-scoped native and legacy-presentation greps; scope audit.

## 16. YAGNI review

Keep one `BattleManager`, private `BattlePhase`, one result record, one local string queue, the existing host, and existing Theme/components/metrics.

Do not add battle/session/service architecture, host APIs/kinds, generic pickers, event models/buses, Theme/metric expansion, visible escape Results, manual combat/battle speed/general pause/skill editing, reward identity/persistence/acknowledgement, or a new player-summary abstraction.
