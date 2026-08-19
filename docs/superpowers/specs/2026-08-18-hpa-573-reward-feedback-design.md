# HPA-573 Simple Reward Feedback Design

**Issue:** HPA-573  
**Status:** Draft — revised after reuse review  
**Date:** 2026-08-19

## Context

HPA-573 is the remaining `Todo` interaction/feedback slice in the HPA-358 secondary-presentation workstream. HPA-569 (Dialogue), HPA-570 (Shop and Healing), HPA-571 (Puzzle and Riddle), and HPA-572 (shared prompts) are complete.

The current code already owns almost every mechanism this slice needs:

- `TreasureBoxSpawn.GrantRewardTo(Character)` returns one already-resolved `TreasureRewardGrantResult` after domain mutation.
- `Game.OnTreasureBoxOpenRequested(...)` owns the one-time treasure invocation and currently discards that returned result.
- `BattleManager.EndBattle(...)` already grants battle XP/gold/loot exactly once behind `_resultEmitted`, creates `BattleResultSummary`, and renders a blocking Result phase with Continue.
- `SiriusToastShell` already provides severity/title/message presentation.
- `ExplorationHudController` already owns the exploration HUD safe frame, compact propagation, recursive passivation, viewport resize subscription, timed transient feedback, and teardown of that presentation lifecycle.
- `ExplorationHud.tscn` is already mounted at `Game/UI/GameUI/ExplorationHud`, so it naturally follows the gameplay HUD visibility policy used by Inventory, Battle, riddles, and other hosted screens.

The original HPA-573 draft put the toast queue beside `ExplorationHudController` in `Game`. That would duplicate SafeFrame math, viewport subscriptions, recursive passivation, timer teardown, and compact propagation in the same HUD root. The revised design keeps `Game` as the treasure/domain-to-presentation mapper but moves the toast queue and visual lifetime into the existing exploration HUD presentation owner.

## Goals

- Show resolved treasure gold/item outcomes as readable Sirius toasts after the existing grant succeeds.
- Present multiple resolved treasure outcomes sequentially and deterministically.
- Keep treasure feedback passive: no tree pause, gameplay block, cursor change, focus, Cancel ownership, or mouse interception.
- Reuse `ExplorationHudController` for safe-frame/layout/passive/timed HUD ownership instead of creating a parallel Game-side feedback channel.
- Keep Battle's existing Result phase as the required blocking battle reward presentation.
- Clear active/pending treasure feedback on scene-change request and HUD/root teardown.
- Pin the real battle reward re-application guard by proving a second `EndBattle(true)` does not grant again.
- Remove unused reward-specific `UIScreenKinds` now that HPA-573 establishes the production reward paths.

## Non-goals

- A reward manager, notification singleton, presenter/view-model, event bus, global queue, or new host service.
- A second battle-result modal or post-battle toast sequence.
- Cross-session reward identity, replay, retry, acknowledgement barriers, persistence, save coordination, or deduplication.
- Changes to treasure contents, loot tables, inventory capacity, Recovery Chest rules, experience, level-up rules, or battle reward calculation.
- Achievements, rarity/comparison metadata, history, filtering, item-art expansion, audio cues, tweens, or reduced-motion work.
- Converting the existing exploration area/session transient plate into a generic notification framework.
- Removing the generic `UIScreenLayer.Toast`; only unused reward-specific product kinds are retired.

## Architecture decision

Use two existing presentation owners based on lifecycle:

1. **Battle remains blocking and Battle-owned.** `BattleManager.EndBattle(...)` grants once, creates `BattleResultSummary`, calls `RenderResult(...)`, and keeps the battle screen active until Continue. HPA-573 adds characterization only; it does not move Battle Results into another surface.
2. **Treasure toast lifetime belongs to `ExplorationHudController`.** The HUD already owns SafeFrame, compact mode, passivation, viewport resize, transient timers, and `GameUI` visibility. HPA-573 adds only the missing FIFO/toast-specific state there.
3. **`Game` remains the mapper.** It captures the existing `TreasureRewardGrantResult`, translates domain-resolved values into presentation strings/severity, and calls the HUD's enqueue API. It never grants from presentation.

This is smaller than a Game-owned toast lane and smaller than routing every two-second cosmetic acknowledgement through `UIScreenHost`.

## Why treasure toasts bypass `UIScreenHost`

This is an explicit production decision rather than an accidental bypass.

- `UIScreenHost` orders `ToastLayer` after `ModalLayer`. A host-routed reward toast would therefore remain visible above Inventory/Battle/other modal presentation instead of disappearing with the exploration HUD.
- Treasure toasts change no host effective state: they do not pause, block gameplay, own focus, change cursor/HUD policy, intercept Cancel, or require focus restoration.
- Opening/closing a host entry for every two-second FIFO item would add host publication/cleanup bookkeeping without providing a product behavior HPA-573 needs.
- Housing the toast under `ExplorationHud` means existing `HudRoot = GameUI` visibility automatically hides exploration feedback whenever a hosted screen hides the gameplay HUD.

The generic `UIScreenLayer.Toast` remains a valid host capability and continues to have fixture coverage. The product-specific `UIScreenKinds.RewardToast` and `RewardAcknowledgement` are no longer production concepts and should be removed rather than maintained as dead parallel reward paths.

## Ownership

### Domain producers

No grant ownership moves:

- `TreasureReward.GrantTo(...)` and `TreasureBoxSpawn.GrantRewardTo(...)` grant treasure and return the resolved result.
- `BattleManager.EndBattle(...)` grants battle XP/gold/skills/loot.
- Save/opened-box/inventory/experience state remains in existing owners.

### `Game`

`Game` owns only treasure result mapping:

- capture the one returned `TreasureRewardGrantResult`;
- preserve opened-ID, cell-clear, stat-notification, and world-interaction ordering;
- map gold / granted / recovered / unrecovered result values to strings and severity;
- call `ExplorationHudController.EnqueueRewardToast(...)`;
- call `ClearRewardFeedback()` after the existing `_sceneChangeCommitted` latch succeeds.

`Game` does **not** own a reward queue, timer, toast node, resize subscription, or reward-specific teardown subscription.

### `ExplorationHudController`

The HUD owns the presentation lifetime:

```csharp
public void EnqueueRewardToast(
    string title,
    string message,
    SiriusUiSeverity severity);

public void ClearRewardFeedback();
```

Internally it keeps one private FIFO request type and one authored one-shot timer. It reuses its existing `RefreshLayout()`, `MakePassive(this)`, viewport `SizeChanged` subscription, and `_ExitTree()` lifecycle.

### `SiriusToastShell`

`SiriusToastShell` remains unchanged. It is a visual leaf only. The HUD's existing recursive `MakePassive(this)` makes the instanced toast subtree mouse-transparent and non-focusable at runtime, so HPA-573 does not add eight redundant `mouse_filter` edits to the reusable component.

## Exploration HUD scene composition

Extend `scenes/ui/ExplorationHud.tscn`:

```text
ExplorationHud
├── SafeFrame (%SafeFrame)
│   ├── HeroPlate
│   ├── PromptPlate
│   ├── TransientPlate
│   └── RewardToastSlot (%RewardToastSlot; top-right VBoxContainer)
│       └── RewardToast (%RewardToast; SiriusToastShell; hidden initially)
├── TransientTimer (%TransientTimer)
└── RewardToastTimer (%RewardToastTimer; one-shot; Always)
```

Rules:

- `RewardToastSlot` is a sibling of `%TransientPlate` under `%SafeFrame`.
- It is right-aligned to the SafeFrame edge, so ultrawide content capping comes from the existing SafeFrame geometry rather than a second `SafeFrameInsets(...)` formula.
- The authored top offset is 68 px: the existing transient band is 60 px high and the remaining 8 px is the normal local gap. This permits area/session transient copy and reward feedback to coexist without overlap.
- Standard toast width is 360 px; compact width is 280 px. Those remain HUD-local layout values, not `SiriusUiMetrics` tokens and not aliases for `TooltipMaximum`.
- Only one `SiriusToastShell` exists because requests display sequentially.
- `%RewardToastTimer` is `OneShot = true`, `WaitTime = 2.0`, `ProcessMode = Always`.

## Interaction with existing area/session transient feedback

Reward toasts and `%TransientPlate` are intentionally independent lanes:

- area title/session hint keeps its existing precedence (`ShowAreaTitle` may defer a session hint);
- reward FIFO never preempts or rewrites that transient state;
- both may be visible together;
- the reward lane is top-right and starts below the transient band's vertical footprint;
- 640×360 layout coverage must prove reward, transient, prompt, and hero surfaces do not overlap.

This avoids coupling reward timing to area/session timing while still using one HUD lifecycle owner.

## HUD queue behavior

Keep the request model private to `ExplorationHudController`:

```csharp
private readonly record struct RewardToastRequest(
    string Title,
    string Message,
    SiriusUiSeverity Severity);

private const double RewardToastDurationSeconds = 2.0;
private readonly Queue<RewardToastRequest> _rewardToastQueue = new();
```

Public enqueue takes primitives so tests/callers never construct the private request type:

```csharp
public void EnqueueRewardToast(
    string title,
    string message,
    SiriusUiSeverity severity)
{
    _rewardToastQueue.Enqueue(new RewardToastRequest(title, message, severity));
    if (!_rewardToast.Visible)
        ShowNextRewardToast();
}
```

Advancement:

```csharp
private void ShowNextRewardToast()
{
    if (_rewardToastQueue.Count == 0)
    {
        _rewardToastTimer.Stop();
        _rewardToast.Hide();
        return;
    }

    var request = _rewardToastQueue.Dequeue();
    _rewardToast.Title = request.Title;
    _rewardToast.Message = request.Message;
    _rewardToast.Severity = request.Severity;
    _rewardToast.Show();
    _rewardToastTimer.Start(RewardToastDurationSeconds);
}
```

Cleanup:

```csharp
public void ClearRewardFeedback()
{
    _rewardToastQueue.Clear();

    if (_rewardToastTimer != null && GodotObject.IsInstanceValid(_rewardToastTimer))
        _rewardToastTimer.Stop();

    if (_rewardToast != null && GodotObject.IsInstanceValid(_rewardToast))
        _rewardToast.Hide();
}
```

`_ExitTree()` calls `ClearRewardFeedback()` and unsubscribes the reward timer with the same guarded pattern as the existing transient/viewport teardown.

## Reuse of layout and passivation

No `RefreshRewardToastLayout()` is introduced.

The existing `ExplorationHudController.RefreshLayout()` already computes:

```csharp
var layout = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
_safeFrame.OffsetLeft = layout.SideInset;
_safeFrame.OffsetRight = -layout.SideInset;
_safeFrame.OffsetTop = layout.Margin;
_safeFrame.OffsetBottom = -layout.Margin;
```

HPA-573 only extends the existing compact branch:

```csharp
_rewardToastSlot.OffsetLeft = _compact ? -280f : -360f;
_rewardToast.Compact = _compact;
```

The slot remains anchored to `%SafeFrame`'s right edge. At 2560×1080, `%SafeFrame` already ends 480 px from the physical viewport edge because of the 1600 px content cap; the reward toast therefore inherits the correct inset automatically.

`MakePassive(this)` already recursively assigns both `MouseFilter = Ignore` and `FocusMode = None` after the entire HUD subtree is instanced. The reward subtree receives the same contract without modifying `SiriusToastShell.tscn`.

## Treasure result mapping

Capture the existing grant result once:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Presentation remains appended after existing domain mutation/persistence/cell cleanup.

### Deterministic order

Map in this exact order:

1. `GoldGranted` when positive.
2. `ItemQuantitiesGranted`, ordered by item ID with `StringComparer.Ordinal`.
3. `ItemQuantitiesRecovered`, ordered by item ID with `StringComparer.Ordinal`.
4. `UnrecoveredItemQuantities`, ordered by item ID with `StringComparer.Ordinal`.

Never use `Dictionary` enumeration order as UI behavior.

### Copy and severity

```text
Success | Treasure Acquired | 25 Gold
Success | Item Acquired     | Health Potion ×1
Warning | Recovery Chest    | Health Potion ×2 sent to the Recovery Chest
Error   | Inventory Full    | Health Potion ×2 could not be stored
```

`SkippedItemIds` and raw `Errors` are not player-facing reward copy in this slice.

### Item display-name fallback

Resolve via the existing catalog:

```csharp
private static string ResolveRewardItemDisplayName(string itemId)
{
    var item = ItemCatalog.CreateItemById(itemId);
    if (item != null)
        return item.DisplayName;

    GD.PushWarning($"[Game] Reward feedback could not resolve item '{itemId}'.");
    return itemId;
}
```

The fallback remains readable enough for defensive failure, but it is no longer silent. Normal `TreasureReward.GrantTo(...)` already warns/skips unknown authored item IDs before they can become granted/recovered values.

## Scene navigation and root teardown

`RequestSceneChange(...)` keeps its existing one-shot latch. Immediately after `_sceneChangeCommitted = true`:

```csharp
if (_explorationHud != null && GodotObject.IsInstanceValid(_explorationHud))
    _explorationHud.ClearRewardFeedback();
```

The guard is mandatory because `GameTest.TestableGame` overrides `_Ready()` to a no-op; `_explorationHud` is therefore unbound in those synthetic tests.

No Game-side reward cleanup is added to `Game._ExitTree()`. A real Game root owns `ExplorationHud` as a child; `ExplorationHudController._ExitTree()` clears its own queue/timer when the HUD leaves the tree. This avoids duplicate teardown ownership and removes the null-prone Game timer/viewport unsubscribe from the previous draft.

## Battle Result correctness

HPA-573 should test the real grant boundary, not just a label-only renderer.

`BattleManager.EndBattle(bool playerWon)` begins with:

```csharp
if (_resultEmitted)
    return;
```

The first successful call performs XP/gold/skills/loot mutation and renders `ResolvedResult`. A second call must return before any of those effects repeat.

The characterization uses `CreateReadyBattleManager()` because it instantiates `BattleScene.tscn`, binds authored nodes in `_Ready()`, and starts a real battle fixture.

Test shape:

1. call `EndBattle(true)` once by reflection;
2. assert `ResolvedResult` exists and result labels match its XP/gold/level/loot values;
3. snapshot player gold, experience, level, known skills, and all inventory quantities **after** the first call;
4. call `EndBattle(true)` again;
5. assert every snapshot is unchanged and result labels remain readable.

This directly pins the re-grant latch. `RenderResult(...)` stays production-unchanged and does not need a separate mutation test because it contains no grant path.

## Retire unused reward-specific host kinds

HPA-573 resolves the final production ownership, so do not preserve dead product paths.

Remove from `UIScreenKinds`:

```csharp
RewardToast
RewardAcknowledgement
```

Keep `UIScreenLayer.Toast` and its generic passive-layer behavior. Host tests that need a toast or acknowledgement identity use test-local fixtures through `UIScreenHostTestSupport`:

```csharp
public static readonly StringName ToastFixture = "toast_fixture";
public static readonly StringName AcknowledgementFixture = "acknowledgement_fixture";
```

Retarget existing host tests from the removed product kinds to these fixture kinds. This preserves host policy coverage without implying a second production reward implementation.

Update `docs/ui/hpa-378/uiscreenhost-contract.md` so its toast/acknowledgement snippets are explicitly generic host fixtures, not HPA-573 production guidance.

## Test strategy

### `ExplorationHudControllerTest`

Extend the existing cheap HUD fixture:

- required nodes include `%RewardToastSlot`, `%RewardToast`, `%RewardToastTimer`;
- `AssertPassive(hud)` continues to prove the new subtree is mouse-transparent and non-focusable;
- enqueue two primitive requests; first displays immediately, timeout advances to second, second timeout hides;
- `ClearRewardFeedback()` hides, stops the timer, and prevents a queued second request from appearing;
- `%RewardToastTimer.ProcessMode == Always` and `WaitTime == 2.0`;
- standard width is 360, compact width is 280;
- existing all-approved-viewports test includes the visible reward toast in SafeFrame containment;
- at 640×360 reward does not intersect hero/prompt/transient surfaces;
- at 2560×1080 SafeFrame begins/ends 480 px from the viewport edge and the reward toast's right edge equals the SafeFrame right edge.

No Game/FloorManager/UIScreenHost fixture is needed for queue/layout/passivity mechanics.

### `GameTest`

Keep integration-only behavior here:

- existing treasure grants 25 gold + one health potion once;
- gold toast then item toast appear from the captured resolved result;
- advancing HUD reward timeout does not change gold/inventory;
- re-interaction with opened treasure neither grants nor enqueues again;
- a directly constructed `TreasureRewardGrantResult` exercises recovered/unrecovered Warning/Error copy and ordinal ordering without manufacturing a full inventory;
- aborted treasure opening queues nothing;
- `RequestSceneChange(string.Empty)` clears active/pending HUD reward feedback without navigating the test runner;
- real Game root exit causes HUD teardown to clear its active feedback.

### `BattleManagerTest`

Use `CreateReadyBattleManager()` and call `EndBattle(true)` twice. Pin first-call readability and second-call no-regrant state.

### Host tests

Retarget reward-specific kind references to `UIScreenHostTestSupport.ToastFixture` / `AcknowledgementFixture`. The generic `ToastLayer` passive-state test remains.

## Documentation reconciliation

After implementation:

- `docs/ui/hpa-376/ui-lifecycle-contract.md`
  - `WORLD-TREASURE`: capture already-resolved result and hand presentation copy to Exploration HUD.
  - `REWARD-TOAST`: owner becomes `ExplorationHudController`; no host entry; Game maps only.
  - `REWARD-BLOCKING`: point to Battle's current Result phase and `_resultEmitted` guard.
- `docs/ui/hpa-377/README.md`
  - HPA-573 owns production treasure queueing at the Exploration HUD; `SiriusToastShell` remains visual-only.
- `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`
  - retarget stale HPA-386 toast/reward handoff to HPA-573 without changing component responsibilities.
- `docs/ui/hpa-378/uiscreenhost-contract.md`
  - generic Toast/acknowledgement examples use fixture identities; they are not production reward paths.

## File map

### Production modifications

- `scenes/ui/ExplorationHud.tscn`
- `scripts/ui/ExplorationHudController.cs`
- `scripts/game/Game.cs`
- `scripts/ui/hosting/UIScreenKinds.cs` — remove dead reward-specific product kinds only.

### Test modifications

- `tests/ui/ExplorationHudControllerTest.cs`
- `tests/game/GameTest.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/ui/hosting/UIScreenHostTestSupport.cs`
- `tests/ui/hosting/UIScreenStackModelTest.cs`
- `tests/ui/hosting/UIScreenPolicyResolverTest.cs`
- `tests/ui/hosting/UIScreenHostInputTest.cs`
- `tests/ui/hosting/UIScreenHostFocusTest.cs`
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`

### Documentation modifications

- `docs/ui/hpa-376/ui-lifecycle-contract.md`
- `docs/ui/hpa-377/README.md`
- `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`
- `docs/ui/hpa-378/uiscreenhost-contract.md`

### Reference only unless a focused test exposes a defect

- `scripts/data/TreasureReward.cs`
- `scripts/game/TreasureBoxSpawn.cs`
- `scripts/data/BattleResultSummary.cs`
- `scripts/data/LootResult.cs`
- `scripts/data/items/ItemCatalog.cs`
- `scripts/ui/BattleManager.cs`
- `scripts/ui/components/SiriusToastShell.cs`
- `scenes/ui/components/SiriusToastShell.tscn`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/hosting/UIScreenHost.cs`

## Deferred decisions

- General notification service: only if a second root needs cross-feature queuing.
- Item art in toasts: only if a concrete reward flow needs more than severity + title/message.
- Cross-session identity/retry/persistence: only after a reproduced defect.
- Shared reward modal/constellation: not needed while Battle already owns required acknowledgement.
- Reduced-motion reward animation/audio: HPA-541 or later motion work, not HPA-573.

## Acceptance criteria

- Current treasure gold/items are readable after grant.
- Treasure presentation never grants/reapplies rewards.
- Multiple outcomes from one invocation display FIFO in deterministic category/ordinal order.
- Recovered and unrecovered overflow have Warning/Error copy.
- Brief treasure feedback does not block gameplay input or own focus/cursor/Cancel.
- Reward feedback follows the existing exploration HUD SafeFrame and visibility policy.
- Pending reward feedback is cleared by scene-change request and HUD/root teardown.
- Battle Result remains readable and a second `EndBattle(true)` cannot reapply rewards.
- No reward manager/service/global queue/persistence or second battle surface exists.
- No unused reward-specific `UIScreenKinds` remain; generic host Toast behavior is covered with test fixtures.