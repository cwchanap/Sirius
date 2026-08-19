# HPA-573 Simple Reward Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic non-blocking treasure reward toasts through the existing Exploration HUD while retaining Battle's existing blocking Result phase and proving both reward paths cannot re-grant from presentation/re-entry.

**Architecture:** Keep reward mutation in existing producers. `Game` captures `TreasureRewardGrantResult` and maps its already-resolved values to primitive toast copy; `ExplorationHudController` owns the FIFO, authored `SiriusToastShell`, timer, safe-frame placement, passivation, compact propagation, and teardown because it already owns those exploration-HUD mechanisms. Battle remains production-unchanged; its existing `_resultEmitted` latch and Result phase are characterized directly. Do not route production treasure feedback through `UIScreenHost`; remove the now-unused reward-specific host kinds while preserving generic Toast-layer fixture coverage.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusToastShell`, `SiriusUiMetrics`, existing `ExplorationHudController`, existing `UIScreenHost` generic layer contracts.

**Spec:** `docs/superpowers/specs/2026-08-18-hpa-573-reward-feedback-design.md`

## Global Constraints

- Deliver HPA-573 in **one implementation PR**. Tasks below are review/commit boundaries inside that PR, not separate PRs.
- `TreasureReward.GrantTo(...)`, `TreasureBoxSpawn.GrantRewardTo(...)`, `BattleManager.EndBattle(...)`, save state, inventory mutation, XP/level mutation, and loot award remain domain-owned.
- Presentation never calls `GrantTo`, `GainGold`, `TryAddItem`, `GainExperience`, `AwardLootToCharacter`, save/load, or navigation APIs.
- Keep `BattleManager`'s current Result phase as the required battle acknowledgement path; no post-battle toast or shared reward modal.
- Add no `RewardManager`, notification singleton, event bus, presenter/view-model, public reward DTO, global queue, persistence, stable event identity, retry, or acknowledgement protocol.
- `Game` maps treasure results only. It does **not** own a toast node, queue, timer, resize subscription, or reward-specific `_ExitTree` unsubscribe.
- `ExplorationHudController` owns the root-local treasure toast FIFO because it already owns SafeFrame, compact layout, recursive passivation, timed transient feedback, viewport resize, and HUD teardown.
- Keep the reward toast under `ExplorationHud/%SafeFrame`; it must hide automatically whenever the existing `GameUI` HUD root is hidden.
- Reward and area/session transient feedback are separate lanes and may coexist. At 640×360 they must not overlap.
- One authored `SiriusToastShell` is reused sequentially; do not stack simultaneous reward nodes.
- Reward toasts have no focus, Cancel handling, tree pause, gameplay block, cursor policy, or mouse interception.
- Standard toast width is 360 px; compact width is 280 px. These remain HUD-local values; do not add a shared metric or alias `TooltipMaximum`.
- Reward toast duration is exactly 2.0 seconds. `%RewardToastTimer` is one-shot and `ProcessMode = Always`.
- Within one treasure result: gold first, then granted items sorted ordinal by item ID, then recovered items sorted ordinal, then unrecovered items sorted ordinal.
- Resolve display names through `ItemCatalog.CreateItemById`; if a defensive result contains an unknown ID, emit `GD.PushWarning` before falling back to the ID.
- Do not surface `SkippedItemIds` or raw domain `Errors` as reward copy.
- Do not add item-art support to `SiriusToastShell`.
- Do not modify `SiriusToastShell.tscn` for passivation; existing `ExplorationHudController.MakePassive(this)` applies both mouse-ignore and no-focus recursively after the toast subtree is instanced.
- Generic `UIScreenLayer.Toast` remains. Remove only unused reward-specific `UIScreenKinds.RewardToast` and `UIScreenKinds.RewardAcknowledgement`, and retarget host tests to test-only fixture kinds.

---

## File Structure

### Task 1 — Exploration HUD owner

- Modify: `scenes/ui/ExplorationHud.tscn`
- Modify: `scripts/ui/ExplorationHudController.cs`
- Modify: `tests/ui/ExplorationHudControllerTest.cs`

### Task 2 — Treasure integration

- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`

### Task 3 — Battle characterization

- Modify: `tests/ui/BattleManagerTest.cs`

### Task 4 — Retire unused reward-specific host kinds

- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/ui/hosting/UIScreenHostTestSupport.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`
- Modify: `tests/ui/hosting/UIScreenPolicyResolverTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostInputTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostFocusTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Modify: `docs/ui/hpa-378/uiscreenhost-contract.md`

### Task 5 — Lifecycle/ownership docs and final validation

- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify: `docs/ui/hpa-377/README.md`
- Modify: `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`

### Reference only unless a focused test proves a defect

- `scripts/data/TreasureReward.cs`
- `scripts/game/TreasureBoxSpawn.cs`
- `scripts/data/BattleResultSummary.cs`
- `scripts/data/LootResult.cs`
- `scripts/data/Inventory.cs`
- `scripts/data/items/ItemCatalog.cs`
- `scripts/ui/BattleManager.cs`
- `scripts/ui/components/SiriusToastShell.cs`
- `scenes/ui/components/SiriusToastShell.tscn`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/hosting/UIScreenHost.cs`

---

## Risk Checklist

### A second HUD lifecycle grows in `Game`

Do not add Game-side SafeFrame math, `SizeChanged`, toast timer binding, recursive passivation, or reward teardown subscriptions. The existing Exploration HUD already owns all of those mechanisms.

### Reward toast collides with area/session transient copy

Keep a distinct top-right lane under `%SafeFrame`. The existing transient plate is 60 px high; author reward top offset at 68 px (60 + 8 gap). Extend the existing 640×360 no-overlap test.

### Dictionary enumeration leaks into UI ordering

Every granted/recovered/unrecovered dictionary is sorted with `StringComparer.Ordinal` before enqueue.

### Recovery / unrecovered copy is never exercised

Do not manufacture a full real inventory. Construct a resolved `TreasureRewardGrantResult` directly in `GameTest`, invoke the private Game mapper, and inspect the public HUD queue output.

### Scene-change cleanup breaks synthetic TestableGame

`GameTest.TestableGame._Ready()` is intentionally empty, so `_explorationHud` is unbound. `RequestSceneChange(...)` must guard `_explorationHud != null && IsInstanceValid(...)` before calling `ClearRewardFeedback()`.

### Battle test checks only a mutation-free label helper

Do not test `RenderResult(...)` alone. Use a ready Battle scene and call `EndBattle(true)` twice. Snapshot state after the first grant and assert the second call changes nothing.

### Host reward path remains as dead product API

Production treasure intentionally bypasses the host because ToastLayer sits above ModalLayer and because the toast changes no host state. Remove reward-specific product kinds now; keep generic Toast-layer behavior tested via fixture identities.

### Unknown IDs become silent content-ID UI

Keep the defensive raw-ID fallback but emit `GD.PushWarning` before returning it.

---

# Task 1: Extend Exploration HUD with one passive reward-toast FIFO

**Files:**
- Modify: `scenes/ui/ExplorationHud.tscn`
- Modify: `scripts/ui/ExplorationHudController.cs`
- Modify: `tests/ui/ExplorationHudControllerTest.cs`

**Interfaces produced:**

```csharp
public void EnqueueRewardToast(
    string title,
    string message,
    SiriusUiSeverity severity);

public void ClearRewardFeedback();
```

Private state:

```csharp
private readonly record struct RewardToastRequest(
    string Title,
    string Message,
    SiriusUiSeverity Severity);

private const double RewardToastDurationSeconds = 2.0;
private readonly Queue<RewardToastRequest> _rewardToastQueue = new();

private VBoxContainer _rewardToastSlot = null!;
private SiriusToastShell _rewardToast = null!;
private Timer _rewardToastTimer = null!;
```

## 1.1 Write RED scene/queue/passivity coverage

- [ ] Add these paths to `RequiredNodes` in `ExplorationHudControllerTest`:

```csharp
"%RewardToastSlot",
"%RewardToast",
"%RewardToastTimer"
```

- [ ] Add `RewardToastQueue_IsPassiveSequentialAndClearable`:

```csharp
[TestCase]
public async Task RewardToastQueue_IsPassiveSequentialAndClearable()
{
    var hud = await InstantiateHud(new Vector2I(1280, 720));
    var toast = hud.GetNode<SiriusToastShell>("%RewardToast");
    var timer = hud.GetNode<Timer>("%RewardToastTimer");

    AssertThat(toast.Visible).IsFalse();
    AssertThat(timer.OneShot).IsTrue();
    AssertThat(timer.WaitTime).IsEqual(2.0d);
    AssertThat(timer.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

    hud.EnqueueRewardToast("First", "25 Gold", SiriusUiSeverity.Success);
    hud.EnqueueRewardToast("Second", "Health Potion ×1", SiriusUiSeverity.Success);

    AssertThat(toast.Visible).IsTrue();
    AssertThat(toast.Title).IsEqual("First");
    AssertThat(toast.Message).IsEqual("25 Gold");

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Visible).IsTrue();
    AssertThat(toast.Title).IsEqual("Second");
    AssertThat(toast.Message).IsEqual("Health Potion ×1");

    hud.ClearRewardFeedback();
    AssertThat(toast.Visible).IsFalse();
    AssertThat(timer.IsStopped()).IsTrue();

    // The queued request was cleared, so another timeout cannot resurrect it.
    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Visible).IsFalse();

    AssertPassive(hud);
}
```

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ExplorationHudControllerTest.RewardToastQueue_IsPassiveSequentialAndClearable"
```

Expected: FAIL because the authored nodes/API do not exist.

## 1.2 Extend the existing viewport test instead of creating Game layout tests

- [ ] In `LayoutFitsApprovedViewportsAndKeepsCompactHeroSeparated`, enqueue one visible reward toast before the layout assertions:

```csharp
hud.EnqueueRewardToast(
    "Treasure Acquired",
    "Health Potion ×2",
    SiriusUiSeverity.Success);
```

- [ ] Include `%RewardToast` in SafeFrame containment:

```csharp
var rewardToast = hud.GetNode<SiriusToastShell>("%RewardToast");

foreach (var surface in new Control[] { hero, prompt, transient, rewardToast })
{
    var rect = surface.GetGlobalRect();
    AssertThat(rect.Size.X).IsGreater(0f);
    AssertThat(rect.Size.Y).IsGreater(0f);
    AssertThat(rect.Position.X).IsGreaterEqual(safeRect.Position.X - 0.5f);
    AssertThat(rect.Position.Y).IsGreaterEqual(safeRect.Position.Y - 0.5f);
    AssertThat(rect.End.X).IsLessEqual(safeRect.End.X + 0.5f);
    AssertThat(rect.End.Y).IsLessEqual(safeRect.End.Y + 0.5f);
}
```

- [ ] At 640×360 extend the existing no-overlap assertions:

```csharp
AssertThat(rewardToast.GetGlobalRect().Intersects(hero.GetGlobalRect())).IsFalse();
AssertThat(rewardToast.GetGlobalRect().Intersects(prompt.GetGlobalRect())).IsFalse();
AssertThat(rewardToast.GetGlobalRect().Intersects(transient.GetGlobalRect())).IsFalse();
AssertThat(rewardToast.Compact).IsTrue();
AssertThat(hud.GetNode<VBoxContainer>("%RewardToastSlot").Size.X).IsEqual(280f);
```

- [ ] At 1280×720 assert standard width/compact state:

```csharp
AssertThat(rewardToast.Compact).IsFalse();
AssertThat(hud.GetNode<VBoxContainer>("%RewardToastSlot").Size.X).IsEqual(360f);
```

- [ ] At 2560×1080 pin the existing SafeFrame content cap rather than duplicating its formula:

```csharp
AssertThat(safeRect.Position.X).IsEqual(480f);
AssertThat(safeRect.End.X).IsEqual(2080f);
AssertThat(rewardToast.GetGlobalRect().End.X).IsEqual(safeRect.End.X);
AssertThat(rewardToast.Compact).IsFalse();
AssertThat(hud.GetNode<VBoxContainer>("%RewardToastSlot").Size.X).IsEqual(360f);
```

Use the existing seven-viewport loop already present in this test; do not create a second viewport matrix.

Expected before scene/controller implementation: FAIL on missing reward nodes.

## 1.3 Author the HUD reward lane

- [ ] Add `SiriusToastShell.tscn` as an ext-resource in `ExplorationHud.tscn`.

- [ ] Under `%SafeFrame`, add:

```text
RewardToastSlot (VBoxContainer; %RewardToastSlot)
  anchor_left = 1.0
  anchor_right = 1.0
  offset_left = -360.0
  offset_top = 68.0
  offset_right = 0.0
  top-right lane; child determines height

  RewardToast (SiriusToastShell; %RewardToast)
    visible = false
    horizontal size = ExpandFill
```

The 68 px top offset is the existing 60 px `%TransientPlate` band plus one 8 px local gap.

- [ ] Add under `ExplorationHud` root:

```text
RewardToastTimer (Timer; %RewardToastTimer)
  process_mode = Always
  wait_time = 2.0
  one_shot = true
```

Do not add another SafeFrame, full-viewport margin, CanvasLayer, scrim, modal, button, or AnimationPlayer.

## 1.4 Implement the HUD queue by extending existing lifecycle methods

- [ ] Add `using System.Collections.Generic;` to `ExplorationHudController.cs`.

- [ ] Add the private request/queue/fields from the task interface.

- [ ] Extend `BindNodes()`:

```csharp
_rewardToastSlot = GetNode<VBoxContainer>("%RewardToastSlot");
_rewardToast = GetNode<SiriusToastShell>("%RewardToast");
_rewardToastTimer = GetNode<Timer>("%RewardToastTimer");
```

- [ ] In `_Ready()`, keep existing ordering and add only the reward timeout subscription before `RefreshLayout()`:

```csharp
_rewardToastTimer.Timeout += ShowNextRewardToast;
```

`MakePassive(this)` already runs after `BindNodes()` and therefore covers the complete reward subtree. Do not add component-local mouse-filter edits.

- [ ] Implement:

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

public void ClearRewardFeedback()
{
    _rewardToastQueue.Clear();

    if (_rewardToastTimer != null && GodotObject.IsInstanceValid(_rewardToastTimer))
        _rewardToastTimer.Stop();

    if (_rewardToast != null && GodotObject.IsInstanceValid(_rewardToast))
        _rewardToast.Hide();
}
```

- [ ] Extend `_ExitTree()` without creating a second owner:

```csharp
ClearRewardFeedback();

if (_rewardToastTimer != null && GodotObject.IsInstanceValid(_rewardToastTimer))
    _rewardToastTimer.Timeout -= ShowNextRewardToast;
```

Keep the existing transient timer and viewport unsubscriptions.

- [ ] Extend existing `RefreshLayout()` only:

```csharp
_rewardToastSlot.OffsetLeft = _compact ? -280f : -360f;
_rewardToast.Compact = _compact;
```

Do not call `SafeFrameInsets(...)` a second time for the reward lane.

## 1.5 Run the full HUD suite and commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ExplorationHudControllerTest"
```

Expected: PASS.

- [ ] Commit:

```bash
git add scenes/ui/ExplorationHud.tscn \
  scripts/ui/ExplorationHudController.cs \
  tests/ui/ExplorationHudControllerTest.cs
git commit -m "ui: add exploration reward toast queue"
```

---

# Task 2: Map the existing treasure grant result into the HUD queue

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Reference: `scripts/data/TreasureReward.cs`
- Reference: `scripts/game/TreasureBoxSpawn.cs`
- Reference: `scripts/data/items/ItemCatalog.cs`

**Interfaces produced:**

```csharp
private void EnqueueTreasureRewardFeedback(TreasureRewardGrantResult result);
private static string ResolveRewardItemDisplayName(string itemId);
```

## 2.1 Extend the existing real treasure regression first

- [ ] Keep all current assertions in `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt`.

- [ ] After the existing opening finishes, read the existing HUD-owned nodes:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
var toast = hud.GetNode<SiriusToastShell>("%RewardToast");
var timer = hud.GetNode<Timer>("%RewardToastTimer");

AssertThat(toast.Visible).IsTrue();
AssertThat(toast.Title).IsEqual("Treasure Acquired");
AssertThat(toast.Message).IsEqual("25 Gold");

int goldAfterGrant = gameManager.Player.Gold;
int potionAfterGrant = gameManager.Player.GetItemQuantity("health_potion");

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Title).IsEqual("Item Acquired");
AssertThat(toast.Message).IsEqual("Health Potion ×1");
AssertThat(gameManager.Player.Gold).IsEqual(goldAfterGrant);
AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(potionAfterGrant);

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Visible).IsFalse();
```

- [ ] Preserve the current second-interaction no-regrant assertion and add:

```csharp
AssertThat(toast.Visible).IsFalse();
```

Expected before production mapping: existing grant assertions pass; new toast assertions fail.

## 2.2 Add deterministic granted-item ordering coverage

- [ ] Add a real treasure fixture with reverse-alphabetical authored order:

```csharp
RewardGold = 5,
RewardItemIds = new Godot.Collections.Array<string>
{
    "mana_potion",
    "health_potion"
},
RewardItemQuantities = new Godot.Collections.Array<int> { 1, 2 }
```

- [ ] Open it through the normal interaction path and manually advance `%RewardToastTimer`. Assert:

```text
5 Gold
Health Potion ×2
Mana Potion ×1
```

This must fail if presentation uses dictionary/insertion/authored ordering rather than ordinal item-ID ordering.

## 2.3 Add direct resolved-result coverage for recovered/unrecovered copy

- [ ] Use a **real** `Game.tscn`; do not fill `Inventory.MaxItemTypes` just to manufacture overflow.

```csharp
var game = await InstantiateRealGameScene();
try
{
    var manager = game.GetNode<GameManager>("GameManager");
    var player = manager.Player;
    var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
    var toast = hud.GetNode<SiriusToastShell>("%RewardToast");
    var timer = hud.GetNode<Timer>("%RewardToastTimer");

    int goldBefore = player.Gold;
    int healthBefore = player.GetItemQuantity("health_potion");
    int manaBefore = player.GetItemQuantity("mana_potion");

    var result = new TreasureRewardGrantResult();
    result.ItemQuantitiesRecovered["mana_potion"] = 1;
    result.ItemQuantitiesRecovered["health_potion"] = 2;
    result.UnrecoveredItemQuantities["mana_potion"] = 3;
    result.UnrecoveredItemQuantities["health_potion"] = 4;

    InvokePrivate(game, "EnqueueTreasureRewardFeedback", result);

    AssertThat(toast.Title).IsEqual("Recovery Chest");
    AssertThat(toast.Message).IsEqual(
        "Health Potion ×2 sent to the Recovery Chest");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Warning);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Message).IsEqual(
        "Mana Potion ×1 sent to the Recovery Chest");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Warning);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Title).IsEqual("Inventory Full");
    AssertThat(toast.Message).IsEqual(
        "Health Potion ×4 could not be stored");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Error);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Message).IsEqual(
        "Mana Potion ×3 could not be stored");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Error);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Visible).IsFalse();

    AssertThat(player.Gold).IsEqual(goldBefore);
    AssertThat(player.GetItemQuantity("health_potion")).IsEqual(healthBefore);
    AssertThat(player.GetItemQuantity("mana_potion")).IsEqual(manaBefore);
}
finally
{
    game.Free();
    await AwaitFrames(1);
}
```

Expected before mapper implementation: FAIL because `EnqueueTreasureRewardFeedback` does not exist.

## 2.4 Capture the one existing grant result and implement the mapper

- [ ] Add `using System.Linq;` to `Game.cs`.

- [ ] In `OnTreasureBoxOpenRequested(...)`, replace only the discarded return value:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Do not call `BuildReward()` again and do not move any mutation below/inside presentation code.

- [ ] Implement:

```csharp
private void EnqueueTreasureRewardFeedback(TreasureRewardGrantResult result)
{
    if (_explorationHud == null || !GodotObject.IsInstanceValid(_explorationHud))
        return;

    if (result.GoldGranted > 0)
    {
        _explorationHud.EnqueueRewardToast(
            "Treasure Acquired",
            $"{result.GoldGranted} Gold",
            SiriusUiSeverity.Success);
    }

    foreach (var (itemId, quantity) in result.ItemQuantitiesGranted
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        _explorationHud.EnqueueRewardToast(
            "Item Acquired",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity}",
            SiriusUiSeverity.Success);
    }

    foreach (var (itemId, quantity) in result.ItemQuantitiesRecovered
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        _explorationHud.EnqueueRewardToast(
            "Recovery Chest",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity} sent to the Recovery Chest",
            SiriusUiSeverity.Warning);
    }

    foreach (var (itemId, quantity) in result.UnrecoveredItemQuantities
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        _explorationHud.EnqueueRewardToast(
            "Inventory Full",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity} could not be stored",
            SiriusUiSeverity.Error);
    }
}

private static string ResolveRewardItemDisplayName(string itemId)
{
    var item = ItemCatalog.CreateItemById(itemId);
    if (item != null)
        return item.DisplayName;

    GD.PushWarning($"[Game] Reward feedback could not resolve item '{itemId}'.");
    return itemId;
}
```

Do not enqueue `SkippedItemIds` or raw `Errors`.

## 2.5 Add scene-change cleanup with the synthetic-Game guard

- [ ] Immediately after `_sceneChangeCommitted = true` in `RequestSceneChange(...)`, add:

```csharp
if (_explorationHud != null && GodotObject.IsInstanceValid(_explorationHud))
    _explorationHud.ClearRewardFeedback();
```

Do not add reward timer/viewport unsubscriptions to `Game._ExitTree()`.

- [ ] Add `RewardToast_SceneChangeRequestClearsHudQueueWithoutNavigation` using a real Game:

```csharp
var game = await InstantiateRealGameScene();
try
{
    var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
    var toast = hud.GetNode<SiriusToastShell>("%RewardToast");
    var timer = hud.GetNode<Timer>("%RewardToastTimer");

    hud.EnqueueRewardToast("First", "A", SiriusUiSeverity.Success);
    hud.EnqueueRewardToast("Second", "B", SiriusUiSeverity.Success);

    InvokePrivate(game, "RequestSceneChange", string.Empty);

    AssertThat(toast.Visible).IsFalse();
    AssertThat(timer.IsStopped()).IsTrue();

    // Empty path uses the real scene-change latch/teardown path but never calls ChangeSceneToFile.
    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Visible).IsFalse();
}
finally
{
    game.Free();
    await AwaitFrames(1);
}
```

The existing TestableGame suite will also exercise the null guard because its `_Ready()` remains a no-op.

## 2.6 Pin real root teardown and aborted-open silence

- [ ] Add `RewardToast_GameRootExitClearsHudFeedback`:

```csharp
var game = await InstantiateRealGameScene();
var parent = game.GetParent();
try
{
    var hud = game.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
    var toast = hud.GetNode<SiriusToastShell>("%RewardToast");
    var timer = hud.GetNode<Timer>("%RewardToastTimer");

    hud.EnqueueRewardToast("First", "A", SiriusUiSeverity.Success);
    hud.EnqueueRewardToast("Second", "B", SiriusUiSeverity.Success);

    parent.RemoveChild(game);
    await AwaitFrames(1);

    AssertThat(toast.Visible).IsFalse();
    AssertThat(timer.IsStopped()).IsTrue();
}
finally
{
    if (GodotObject.IsInstanceValid(game))
        game.Free();
    await AwaitFrames(1);
}
```

- [ ] Extend `Game_AbortedTreasureOpeningDoesNotGrantRewardOrPersistOpenedId`:

```csharp
var hud = gameScene.GetNode<ExplorationHudController>("UI/GameUI/ExplorationHud");
AssertThat(hud.GetNode<SiriusToastShell>("%RewardToast").Visible).IsFalse();
```

## 2.7 Run Game coverage and commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest"
```

Expected: PASS, including existing TestableGame teardown/navigation cases.

- [ ] Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "ui: present resolved treasure rewards"
```

---

# Task 3: Characterize Battle's real exactly-once reward boundary

**Files:**
- Modify: `tests/ui/BattleManagerTest.cs`
- Reference: `scripts/ui/BattleManager.cs`
- Reference: `scripts/data/BattleResultSummary.cs`
- Reference: `scripts/data/Inventory.cs`

**Interfaces:** No new production interface.

## 3.1 Add a double-EndBattle characterization test

- [ ] Use existing `CreateReadyBattleManager()`, `FreeManager(...)`, and `InvokePrivateMethod(...)`. Do not use `WithBattleManager(...)` and do not test `RenderResult(...)` in isolation.

```csharp
[TestCase]
public async Task EndBattle_VictoryRewardsRenderAndSecondEndDoesNotReapply()
{
    var manager = await CreateReadyBattleManager();
    int finishedCount = 0;
    manager.BattleFinished += (_, escaped) =>
    {
        if (!escaped)
            finishedCount++;
    };

    try
    {
        var player = GetPrivateField<Character>(manager, "_player");

        InvokePrivateMethod(manager, "EndBattle", true);

        var result = manager.ResolvedResult;
        AssertThat(result).IsNotNull();
        AssertThat(result!.PlayerWon).IsTrue();
        AssertThat(result.ExperienceGained).IsGreater(0);
        AssertThat(result.GoldGained).IsGreater(0);
        AssertThat(finishedCount).IsEqual(1);

        AssertThat(manager.GetNode<Label>("%ResultTitle").Text)
            .IsEqual("VICTORY");
        AssertThat(manager.GetNode<Label>("%ExperienceResult").Text)
            .IsEqual($"Experience: {result.ExperienceGained}");
        AssertThat(manager.GetNode<Label>("%GoldResult").Text)
            .IsEqual($"Gold: {result.GoldGained}");
        AssertThat(manager.GetNode<Label>("%LevelResult").Text)
            .IsEqual(result.PreviousLevel == result.NewLevel
                ? $"Level: {result.NewLevel}"
                : $"Level: {result.PreviousLevel} → {result.NewLevel}");

        var goldAfterFirst = player.Gold;
        var experienceAfterFirst = player.Experience;
        var levelAfterFirst = player.Level;
        var skillsAfterFirst = string.Join("|", player.KnownSkillIds);
        var inventoryAfterFirst = player.Inventory.Entries.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Quantity,
            StringComparer.Ordinal);
        var lootTextAfterFirst = manager.GetNode<Label>("%LootResultList").Text;

        InvokePrivateMethod(manager, "EndBattle", true);

        AssertThat(finishedCount).IsEqual(1);
        AssertThat(player.Gold).IsEqual(goldAfterFirst);
        AssertThat(player.Experience).IsEqual(experienceAfterFirst);
        AssertThat(player.Level).IsEqual(levelAfterFirst);
        AssertThat(string.Join("|", player.KnownSkillIds)).IsEqual(skillsAfterFirst);
        AssertThat(player.Inventory.Entries.Count).IsEqual(inventoryAfterFirst.Count);
        foreach (var (itemId, quantity) in inventoryAfterFirst)
            AssertThat(player.GetItemQuantity(itemId)).IsEqual(quantity);

        AssertThat(manager.GetNode<Label>("%LootResultList").Text)
            .IsEqual(lootTextAfterFirst);
        AssertThat(manager.GetNode<Button>("%ContinueButton").Visible).IsTrue();
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

The first call must perform a real positive XP/gold award; the second call is the no-reapply assertion. Loot may be empty or non-empty depending on the existing RNG, but whatever inventory/result text exists after the first call must be unchanged after the second.

## 3.2 Run and commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~BattleManagerTest.EndBattle_VictoryRewardsRenderAndSecondEndDoesNotReapply"
```

Expected: PASS against current `_resultEmitted` behavior. Do not edit `BattleManager.cs` unless this characterization exposes a real existing defect.

- [ ] Commit the test only:

```bash
git add tests/ui/BattleManagerTest.cs
git commit -m "test: pin battle reward exactly once"
```

---

# Task 4: Retire unused reward-specific host kinds while preserving generic host coverage

**Files:**
- Modify: `scripts/ui/hosting/UIScreenKinds.cs`
- Modify: `tests/ui/hosting/UIScreenHostTestSupport.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`
- Modify: `tests/ui/hosting/UIScreenPolicyResolverTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostInputTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostFocusTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Modify: `docs/ui/hpa-378/uiscreenhost-contract.md`

**Interfaces produced (test-only):**

```csharp
public static readonly StringName ToastFixture = "toast_fixture";
public static readonly StringName AcknowledgementFixture = "acknowledgement_fixture";
```

## 4.1 Add test fixture identities before removing product kinds

- [ ] Add to `UIScreenHostTestSupport`:

```csharp
public static readonly StringName ToastFixture = "toast_fixture";
public static readonly StringName AcknowledgementFixture = "acknowledgement_fixture";
```

These are test-only semantic identities. They do not become production `UIScreenKinds`.

## 4.2 Retarget existing host tests mechanically

- [ ] Replace every `UIScreenKinds.RewardToast` occurrence in these six files with `UIScreenHostTestSupport.ToastFixture`:

```text
tests/ui/hosting/UIScreenStackModelTest.cs
tests/ui/hosting/UIScreenPolicyResolverTest.cs
tests/ui/hosting/UIScreenHostInputTest.cs
tests/ui/hosting/UIScreenHostFocusTest.cs
tests/ui/hosting/UIScreenHostLifecycleTest.cs
tests/ui/hosting/UIScreenHostContractScenarioTest.cs
```

- [ ] Replace `UIScreenKinds.RewardAcknowledgement` in `UIScreenHostContractScenarioTest.cs` with `UIScreenHostTestSupport.AcknowledgementFixture`.

- [ ] Rename the scenario test:

```text
RewardToast_IsPassiveAndNeverBecomesInputOwner
→ ToastLayerPassiveEntry_IsPassiveAndNeverBecomesInputOwner
```

Do not change the tested `UIScreenLayer.Toast`, `UIInputPriority.Passive`, effective-state, restoration-lease, or input-owner assertions.

## 4.3 Remove dead reward product kinds

- [ ] Delete only these two members from `UIScreenKinds.cs`:

```csharp
public static readonly StringName RewardToast = "reward_toast";
public static readonly StringName RewardAcknowledgement = "reward_acknowledgement";
```

Keep `UIScreenLayer.Toast` and the host's `ToastLayer` node/logic unchanged.

## 4.4 Update the live HPA-378 contract example

- [ ] In `docs/ui/hpa-378/uiscreenhost-contract.md`, replace the product-specific `ShowRewardToast` example with a generic host-layer fixture example. Its policy remains:

```csharp
Layer = UIScreenLayer.Toast,
InputPriority = UIInputPriority.Passive,
ProcessPolicy = UIProcessPolicy.InheritHost,
LowerLayers = UILowerLayerPolicy.VisibleInteractive,
Cancel = UICancelPolicy.None,
NodeLifetime = UINodeLifetime.QueueFree
```

Use a generic fixture kind such as `new StringName("toast_fixture")`, and explicitly state that HPA-573 production treasure feedback does **not** use this host path because it must follow `GameUI` HUD visibility and avoid ToastLayer-over-Modal z-order.

- [ ] Retarget the required-acknowledgement example from the removed product kind to `new StringName("acknowledgement_fixture")`; it remains a generic host contract example, not the Battle Result implementation.

## 4.5 Verify no live product-kind references remain

- [ ] Run:

```bash
rg -n "UIScreenKinds\.(RewardToast|RewardAcknowledgement)" \
  scripts tests docs/ui/hpa-378/uiscreenhost-contract.md
```

Expected: zero matches.

Historical superpowers planning documents are not runtime/live-contract sources and are not rewritten solely for this symbol removal.

## 4.6 Run hosting suites and commit

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~UIScreen"
```

Expected: PASS; generic Toast-layer/acknowledgement fixture behavior is unchanged.

- [ ] Commit:

```bash
git add scripts/ui/hosting/UIScreenKinds.cs \
  tests/ui/hosting/UIScreenHostTestSupport.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs \
  tests/ui/hosting/UIScreenPolicyResolverTest.cs \
  tests/ui/hosting/UIScreenHostInputTest.cs \
  tests/ui/hosting/UIScreenHostFocusTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs \
  tests/ui/hosting/UIScreenHostContractScenarioTest.cs \
  docs/ui/hpa-378/uiscreenhost-contract.md
git commit -m "refactor: retire unused reward host kinds"
```

---

# Task 5: Reconcile reward ownership docs and validate the complete slice

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify: `docs/ui/hpa-377/README.md`
- Modify: `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`
- Verify every Task 1-4 file

## 5.1 Reconcile HPA-376 reward rows

- [ ] Update `WORLD-TREASURE` to record:

```text
Grant/opened-ID/cell-clear/stat mutation remains Game/domain-owned.
Game captures the one TreasureRewardGrantResult and maps only resolved values.
ExplorationHudController owns the passive toast FIFO.
World-interaction finally cleanup remains unchanged.
```

- [ ] Replace `REWARD-TOAST` future ownership with:

```text
Owner: ExplorationHudController
Surface: one SiriusToastShell under ExplorationHud/%SafeFrame
Queue: HUD-local FIFO; one active request; 2.0 s Always Timer
Input: no host entry, no pause, no focus/Cancel owner; MakePassive covers the subtree
HUD/cursor: follows existing GameUI HUD visibility; toast changes neither policy itself
Layout: existing HUD SafeFrame/compact owner; reward lane is top-right below transient band
Cleanup: Game RequestSceneChange calls ClearRewardFeedback; HUD _ExitTree clears its own queue/timer
Grant authority: producer only; Game maps resolved copy
Disposition: Preserve
```

- [ ] Update `REWARD-BLOCKING` to point at the existing HPA-356 Battle Result phase:

```text
Owner: BattleManager
Grant boundary: EndBattle + _resultEmitted guard
Display: BattleResultSummary through RenderResult
Acknowledgement: existing Continue action on Battle entry
No shared reward modal / RewardAcknowledgement kind
```

Remove stale HPA-393/HPA-378 future handoff language from those rows only.

## 5.2 Retarget stale HPA-377 ownership

- [ ] In `docs/ui/hpa-377/README.md`, replace the HPA-386 handoff with:

```text
HPA-573 owns production treasure reward queueing in ExplorationHudController.
SiriusToastShell remains the presentation-only visual shell; it does not queue,
time, dismiss, grant, or persist notifications. Battle result acknowledgement
remains Battle-owned through HPA-356.
```

- [ ] In `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`, retarget all HPA-386 toast/reward ownership references:

```text
Toast/reward queue handoff: HPA-573
Toast visual shell consumer: HPA-573
SiriusToastShell queue/lifetime handoff: HPA-573
```

Preserve the original component contract: no Timer/Tween/queue/navigation/lifecycle inside `SiriusToastShell`.

## 5.3 Run focused HPA-573 suites

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest|FullyQualifiedName~UIScreen"
```

Expected: PASS.

## 5.4 Run full suite, build, and diff checks

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check main...HEAD
git status --short
```

Expected: full tests/build pass, diff check clean, no unintended files.

- [ ] Run ownership/stale-path audits:

```bash
rg -n "HPA-386" \
  docs/ui/hpa-377/README.md \
  docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md

rg -n "UIScreenKinds\.(RewardToast|RewardAcknowledgement)" scripts tests

rg -n "RewardToastMargin|RefreshRewardToastLayout|CreatePrivateRewardToastRequest" \
  scripts tests
```

Expected:

- no stale HPA-386 reward-queue ownership in current HPA-377 docs;
- no removed reward-specific `UIScreenKinds` references;
- no Game-side parallel reward layout method/margin or private-record reflection constructor.

- [ ] Review final diff and confirm there is **no** reward manager/service/global queue/persistence, Game-owned reward timer/resize subscription, second battle result surface, or presentation-owned grant operation.

## 5.5 Commit documentation reconciliation

- [ ] Commit:

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md \
  docs/ui/hpa-377/README.md \
  docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md
git commit -m "docs: finalize HPA-573 reward ownership"
```

---

## Final Acceptance Checklist

- [ ] Existing treasure still grants exactly once through the original producer.
- [ ] Game captures the existing `TreasureRewardGrantResult` exactly once; it never rebuilds/grants a reward for UI.
- [ ] One gold + one item invocation shows two sequential HUD toasts.
- [ ] Advancing/clearing toast presentation does not change player gold/inventory/XP/level.
- [ ] Re-interacting with an opened box neither grants nor enqueues again.
- [ ] Multiple granted items use explicit ordinal item-ID ordering.
- [ ] Recovery Chest and unrecovered overflow use Warning/Error copy in deterministic ordinal order without mutating player state.
- [ ] Unknown defensive item IDs emit a warning before raw-ID fallback copy.
- [ ] Aborted treasure opening grants nothing and shows no toast.
- [ ] Toast queue/layout/passivation/timer ownership lives in `ExplorationHudController`, not `Game`.
- [ ] Reward toast uses existing `%SafeFrame`, `MakePassive`, compact propagation, viewport subscription, and HUD teardown.
- [ ] Area/session transient feedback and reward toast are separate non-overlapping lanes at 640×360.
- [ ] At 2560×1080 reward right edge follows the existing 1600px SafeFrame content cap (480px physical side inset).
- [ ] Scene-change request clears active/pending HUD rewards with a null-safe guard for synthetic TestableGame.
- [ ] HUD/root exit clears its own active/pending reward feedback.
- [ ] Battle first `EndBattle(true)` renders resolved XP/gold/level/loot and grants normally.
- [ ] Battle second `EndBattle(true)` leaves gold/XP/level/skills/inventory/result presentation unchanged.
- [ ] Production treasure feedback does not register with `UIScreenHost`.
- [ ] Generic `UIScreenLayer.Toast` remains covered by host fixture tests.
- [ ] Unused `UIScreenKinds.RewardToast` and `RewardAcknowledgement` are removed rather than retained as dead product APIs.
- [ ] HPA-376/HPA-377/HPA-378 live ownership docs match the final production paths.
- [ ] Full tests and build pass.