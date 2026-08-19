# HPA-573 Simple Reward Feedback Design

**Issue:** HPA-573  
**Status:** Draft  
**Date:** 2026-08-18

## Context

HPA-573 is the next actionable child of the HPA-358 secondary-presentation workstream. HPA-569 (Dialogue), HPA-570 (Shop and Healing), HPA-571 (Puzzle and Riddle), and HPA-572 (shared prompts) are complete. HPA-573 is the remaining `Todo` interaction/feedback slice before the HPA-358 checkpoint can close and HPA-359 final UI validation can become actionable.

The current code is already closer to the target than the original reward wireframe implies:

- `BattleManager` owns a full-screen Result phase and already renders `BattleResultSummary`: victory/defeat, experience, gold, level change, loot lines, and an explicit Continue action.
- `TreasureBoxSpawn.GrantRewardTo(Character)` already returns a resolved `TreasureRewardGrantResult` after domain mutation.
- `Game.OnTreasureBoxOpenRequested(...)` currently discards that grant result, then persists the opened box, clears the grid cell, and refreshes player stats.
- `SiriusToastShell` already exists as the reusable non-interactive visual leaf; it has severity, title, message, compact typography, and minimum-size propagation, but no production queue owner.
- `Game.tscn` already owns the exploration HUD and production `UIScreenHost`; there is no reward-toast region yet.
- HPA-376 already records the desired `REWARD-TOAST` lifecycle: non-blocking, HUD retained, cursor unchanged, no focus/Cancel owner, deterministic queue, and producer-owned grants.

HPA-573 should therefore close the one missing production path instead of introducing a reward framework.

## Goals

- Show treasure gold/item results as readable Sirius toasts after the existing grant succeeds.
- Present multiple resolved treasure outcomes sequentially in deterministic order.
- Keep brief reward feedback completely outside `UIScreenHost`: no tree pause, gameplay block, cursor change, focus, or Cancel ownership.
- Reuse the existing `SiriusToastShell` rather than creating a reward-specific modal or generic notification service.
- Keep Battle's existing Result phase as the important/required reward acknowledgement path and pin it with focused coverage.
- Clear active and queued treasure feedback before scene navigation and during root teardown.
- Prove that advancing/dismissing presentation never grants gold, items, experience, or levels.

## Non-goals

- A reward manager, notification singleton, presenter/view-model layer, event bus, global queue, or new `UIScreenHost` kind.
- A second battle-result modal or post-battle toast sequence.
- Cross-session reward identity, replay, retry, acknowledgement barriers, persistence, save coordination, or deduplication.
- Changes to treasure contents, loot tables, balance, item stacking, Recovery Chest rules, experience, level-up rules, or battle reward calculation.
- Achievements, rarity/comparison presentation, persistent history, or reward filtering.
- New theme tokens, icon assets, audio cues, tweens, or reduced-motion work.
- Expanding `SiriusToastShell` into an arbitrary custom-content component.

## Architecture decision

Use two existing presentation paths based on importance:

1. **Battle Result remains blocking and battle-owned.** `BattleManager` already has the correct parent lifecycle and required Continue action. HPA-573 does not extract it into a shared reward surface and does not display the same result again after Battle closes.
2. **Treasure feedback becomes one Game-owned sequential toast queue.** `Game` already owns treasure orchestration and the gameplay root lifetime, so it is the smallest correct owner for an in-memory queue that must be discarded with that root.

This deliberately does not implement the full decorative "reward constellation" concept from the early HPA-373 wireframe. HPA-573's final ticket scope is narrower: reuse `SiriusToastShell` for brief results and use an existing result/modal only where acknowledgement is already required. Battle already provides that required acknowledgement.

## Ownership

### Domain producers

Domain ownership does not move:

- `TreasureReward.GrantTo(...)` and `TreasureBoxSpawn.GrantRewardTo(...)` grant treasure and return the already-resolved result.
- `BattleManager` / battle-domain code grant and resolve battle outcomes exactly as today.
- Save/opened-box/inventory/experience state remains producer-owned.

### `Game`

`Game` owns only treasure presentation orchestration:

- capture the returned `TreasureRewardGrantResult`;
- translate resolved values to a local toast request sequence;
- show one request at a time through the authored `SiriusToastShell`;
- advance on a local one-shot timer;
- clear active and queued requests on scene-change request and root teardown.

The queue is root-scoped presentation state only. It never calls `GrantTo`, `GainGold`, `TryAddItem`, experience mutation, save APIs, or navigation APIs.

### `SiriusToastShell`

`SiriusToastShell` remains a visual leaf. HPA-573 may make its scene explicitly mouse-transparent because a toast is non-interactive by contract, but it does not gain queue/timer/domain behavior.

## Scene composition

Add a small production toast region to `scenes/game/Game.tscn` under `UI/GameUI`, beside the Exploration HUD:

```text
Game
└── UI (CanvasLayer)
    ├── GameUI (Control)
    │   ├── ExplorationHud
    │   └── RewardToastMargin (%RewardToastMargin; MarginContainer; full viewport; mouse-transparent)
    │       └── RewardToastColumn (%RewardToastColumn; VBoxContainer; top/right)
    │           └── RewardToast (%RewardToast; SiriusToastShell; hidden initially)
    └── UIScreenHost

Game
└── RewardToastTimer (%RewardToastTimer; Timer; one-shot; Always processing)
```

Rules:

- Only one `SiriusToastShell` instance is needed because HPA-573 displays requests sequentially rather than stacking them.
- The toast is hidden initially.
- The toast region and shell do not accept focus or mouse input.
- The timer runs while tree-pause UI is open so a hidden/obscured toast cannot become stale and reappear much later.
- `UIScreenHost` does not own the toast. Existing host HUD policy naturally determines whether `GameUI` is visible beneath full-screen/hidden-HUD surfaces.

### Responsive placement

Reuse `SiriusUiMetrics.SafeFrameInsets(...)`:

- standard: top/right inside the 24 px safe frame;
- compact: top/right inside the 12 px safe frame;
- ultrawide: use `SideInset` so reward feedback stays with the centred maximum-width content area instead of drifting to the monitor edge.

Use a local presentation width of 360 px standard and 280 px compact. These are screen-layout constants, not new shared theme metrics.

`RefreshRewardToastLayout()` updates:

```csharp
var (compact, margin, sideInset) = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
_rewardToastMargin.AddThemeConstantOverride("margin_top", (int)margin);
_rewardToastMargin.AddThemeConstantOverride("margin_right", (int)sideInset);
_rewardToastColumn.CustomMinimumSize = new Vector2(compact ? 280f : 360f, 0f);
_rewardToast.Compact = compact;
```

Subscribe to viewport `SizeChanged` while `Game` is alive and unsubscribe during teardown.

## Local toast request model

Keep the model private to `Game`:

```csharp
private sealed record RewardToastRequest(
    string Title,
    string Message,
    SiriusUiSeverity Severity);

private const double RewardToastDurationSeconds = 2.0;
private readonly Queue<RewardToastRequest> _rewardToastQueue = new();
```

No public DTO, stable ID, timestamp, acknowledgement token, source enum, or retry metadata is needed.

The active request is represented by the single authored `%RewardToast`; the queue contains only waiting requests.

## Treasure result mapping

Change the existing grant call from discard to capture:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Preserve the current ordering of domain mutation/persistence/cell cleanup. Presentation is appended after the existing resolved result is available; it cannot affect whether the reward is granted.

### Display ordering

Produce requests in this deterministic order:

1. Gold, when `GoldGranted > 0`.
2. Inventory-added items, ordered by item ID using `StringComparer.Ordinal`.
3. Recovery Chest overflow, ordered by item ID.
4. Unrecovered overflow, ordered by item ID.

Dictionary iteration order is not treated as the presentation contract. Explicit ordinal ordering makes tests/runtime deterministic without changing `TreasureRewardGrantResult`.

### Display copy

Use existing catalog data only:

```csharp
private static string ResolveRewardItemDisplayName(string itemId) =>
    ItemCatalog.CreateItemById(itemId)?.DisplayName ?? itemId;
```

`ItemQuantitiesGranted`, `ItemQuantitiesRecovered`, and `UnrecoveredItemQuantities` contain IDs that passed item creation in the domain grant path, so normal runtime output gets the authored `DisplayName`. The fallback is defensive only.

Recommended requests:

```text
Success | Treasure Acquired | 25 Gold
Success | Item Acquired     | Health Potion ×1
Warning | Recovery Chest    | Health Potion ×2 sent to the Recovery Chest
Error   | Inventory Full    | Health Potion ×2 could not be stored
```

Do not surface `SkippedItemIds` as raw player-facing content IDs. Existing domain warnings remain the authoring/debug signal for unknown items. `Errors` remain domain failures; the normal Game treasure path cannot grant to a null player.

### Item art

Do not widen `SiriusToastShell` for item art in HPA-573. The final Linear wording allows supported item icon/name data "where available"; the existing toast leaf exposes a semantic severity icon and current item results always expose a display name through `ItemCatalog`. A custom leading-art API would be a one-consumer component expansion with no gameplay benefit required by acceptance criteria.

## Queue behavior

`EnqueueRewardToast(...)` appends requests and starts presentation only when the toast is currently inactive.

Pseudo-contract:

```csharp
private void EnqueueRewardToast(RewardToastRequest request)
{
    _rewardToastQueue.Enqueue(request);
    if (!_rewardToast.Visible)
        ShowNextRewardToast();
}

private void ShowNextRewardToast()
{
    if (_rewardToastQueue.Count == 0)
    {
        _rewardToast.Hide();
        _rewardToastTimer.Stop();
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

`RewardToastTimer.Timeout` calls `ShowNextRewardToast()`.

Properties:

- one active toast at a time;
- FIFO across producer invocations;
- deterministic order within one treasure invocation;
- no focus request;
- no input consumption;
- no tree pause or gameplay-input block;
- no acknowledgement callback into the producer.

A second treasure opened while feedback from the first is still active simply appends to the same root queue. This is in-session FIFO, not event identity/deduplication.

## Input policy

Toasts must not handle input.

Make `SiriusToastShell.tscn` explicitly mouse-transparent at the reusable component boundary. The focused test should recursively assert that all Control nodes in the shell use `MouseFilterEnum.Ignore`. The scene contains no focusable controls, so no focus owner is introduced.

The full-viewport `%RewardToastMargin` is also mouse-transparent. Keyboard/gamepad input remains gameplay-owned because the toast is never registered with `UIScreenHost` and `Game._Input()` gains no toast branch.

## Battle results

Do not change Battle result ownership or add a post-battle queue.

`BattleManager.RenderResult(BattleResultSummary)` already renders:

- victory/defeat title;
- `ExperienceGained`;
- `GoldGained`;
- previous/new level;
- every `LootResult.DroppedItems` entry;
- explicit Continue for the result phase.

HPA-573 adds a focused `BattleManagerTest` that invokes the existing result renderer with a resolved `BattleResultSummary`, asserts the labels/loot are readable, and snapshots player gold/experience/inventory before and after rendering. The snapshot must remain unchanged. That closes the ticket's battle-result and "UI never grants" requirements without production changes to `BattleManager` unless the characterization test exposes a real defect.

## Teardown and navigation

Add one idempotent presentation cleanup:

```csharp
private void ClearRewardFeedback()
{
    _rewardToastTimer?.Stop();
    _rewardToastQueue.Clear();

    if (_rewardToast != null && IsInstanceValid(_rewardToast))
        _rewardToast.Hide();
}
```

Call it in two places:

1. `RequestSceneChange(...)` immediately after the one-shot `_sceneChangeCommitted` latch succeeds and before host teardown begins. This clears Return-to-Title and other scene-navigation feedback synchronously.
2. `_ExitTree()` as the external/free-root safety net, then disconnect toast timer and viewport resize signals.

Do not persist pending requests and do not transfer them to Main Menu or a newly loaded Game scene.

## Interaction with world cleanup

The treasure world-interaction lifecycle is unchanged:

- opening remains atomic;
- grant happens once only after the box finishes opening and is still valid;
- opened-box ID and cell clear remain exactly once;
- `finally` still ends `IsInWorldInteraction` and refreshes the prompt.

The toast may become visible before that `finally` runs, but it owns no input. Gameplay resumes normally as soon as the existing world latch ends.

Aborted treasure opening still produces no reward result and therefore no toast.

## Test strategy

### `SiriusToastShellTest`

Add one component invariant:

- every Control in the toast shell is mouse-transparent;
- existing minimum-size/severity/compact tests remain green.

### `GameTest`

Extend the existing real-scene treasure test instead of creating a parallel fake producer:

- treasure grants 25 gold + one health potion exactly once;
- after the opening completes, `%RewardToast` shows the gold request first;
- advancing `%RewardToastTimer` shows the item request second;
- advancing again hides the toast and empties the queue;
- gold and potion quantities do not change while either toast advances;
- repeated interaction still does not grant or enqueue the opened treasure again.

Add focused cases for:

- compact/standard safe-frame placement and `Compact` propagation;
- `RequestSceneChange(MainMenuScenePath)` clears active + queued feedback before the scene commit hook;
- root teardown clears active + queued feedback;
- aborted treasure opening grants nothing and queues nothing.

Use the existing `TestableGame` navigation override where possible; do not add a production test seam solely for HPA-573.

### `BattleManagerTest`

Pin the existing blocking result path:

- resolved XP/gold/level/loot values render;
- rendering does not mutate the character.

### Full validation

Run focused suites first, then the existing repository test command and build. No exhaustive viewport matrix is added to HPA-573; 640×360 and 1280×720 are the representative compact/standard checks required for this small overlay.

## HPA-376 lifecycle reconciliation

Update the existing rows after implementation:

- `WORLD-TREASURE`: still producer-owned grant/persistence/cell cleanup; now hands the resolved result to root-local presentation.
- `REWARD-TOAST`: change from future/`Replace in HPA-378/379` wording to the actual HPA-573 Game-owned queue and protecting tests.
- `REWARD-BLOCKING`: record Battle's existing full-screen Result phase as the required acknowledgement path; remove stale dependency language that points at canceled HPA-393 or already-completed HPA-378/379.

Do not rewrite unrelated lifecycle rows.

## File map

### Modify

- `scenes/game/Game.tscn` — author one hidden toast shell region and one one-shot timer.
- `scripts/game/Game.cs` — capture treasure grant result, map requests, run root-local queue, responsive layout, navigation/teardown cleanup.
- `scenes/ui/components/SiriusToastShell.tscn` — make the reusable non-interactive leaf explicitly mouse-transparent.
- `tests/ui/components/SiriusToastShellTest.cs` — pin mouse transparency.
- `tests/game/GameTest.cs` — extend real treasure flow and add queue/layout/cleanup coverage.
- `tests/ui/BattleManagerTest.cs` — characterize readable resolved battle result and no mutation.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — replace stale reward handoff rows with final ownership/evidence.

### Reference only unless a focused test exposes a defect

- `scripts/data/TreasureReward.cs`
- `scripts/game/TreasureBoxSpawn.cs`
- `scripts/data/BattleResultSummary.cs`
- `scripts/data/LootResult.cs`
- `scripts/data/items/ItemCatalog.cs`
- `scripts/ui/BattleManager.cs`
- `scripts/ui/components/SiriusToastShell.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/hosting/UIScreenHost.cs`

## Acceptance mapping

- **Current battle and treasure rewards are readable:** Battle Result stays as the blocking result surface; treasure grants become sequential toasts.
- **UI never grants or reapplies rewards:** only the existing producer call grants; tests snapshot state while presentation advances.
- **Brief feedback does not block gameplay input:** no host entry, no pause, no focus/Cancel owner, mouse-transparent shell/region.
- **Multiple results from one invocation display in deterministic order:** gold first, then explicitly ordinal-sorted item result groups, one active toast at a time.
- **Pending feedback does not survive Return to Title or root teardown:** clear on one-shot `RequestSceneChange(...)` and `_ExitTree()`.

## Deferred decisions

Do not add any of the following unless a later concrete producer requires them:

- shared/global queue owner;
- producer/source IDs;
- persisted notifications;
- acknowledgement callbacks;
- custom item-art toast content;
- manual toast dismissal;
- stacking multiple simultaneous toasts;
- notification history.
