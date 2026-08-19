# HPA-573 Simple Reward Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add non-blocking sequential treasure reward toasts while retaining Battle's existing blocking Result phase and proving presentation never grants rewards.

**Architecture:** Keep reward mutation in existing producers. `Game` captures `TreasureRewardGrantResult`, maps it into a private FIFO of title/message/severity requests, and drives one authored `SiriusToastShell` with one one-shot Timer. Battle stays production-unchanged because its existing Result phase already renders `BattleResultSummary` and requires Continue. No reward service, global queue, new host kind, persistence, identity, retry, or second battle modal.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusToastShell`, `SiriusUiMetrics`, existing `UIScreenHost` only for unrelated blocking screens.

**Spec:** `docs/superpowers/specs/2026-08-18-hpa-573-reward-feedback-design.md`

## Global Constraints

- Deliver HPA-573 in **one implementation PR**. The tasks below are review/commit boundaries inside that PR, not separate PRs.
- `TreasureReward.GrantTo(...)`, `TreasureBoxSpawn.GrantRewardTo(...)`, battle reward calculation, save state, and inventory/experience mutation remain domain-owned.
- Presentation never calls `GrantTo`, `GainGold`, `TryAddItem`, experience/level mutation, save/load, or navigation APIs.
- Keep `BattleManager`'s current Result phase as the important/required reward acknowledgement path; do not add a post-battle toast or shared reward modal.
- Add no `RewardManager`, notification singleton, event bus, presenter/view-model, public reward DTO, new `UIScreenKind`, exclusive group, or host policy.
- Treasure toasts are root-local FIFO presentation only and are discarded on navigation/root teardown.
- One authored `SiriusToastShell` is reused sequentially; do not stack multiple simultaneous reward nodes.
- Toasts have no focus, Cancel handling, tree pause, gameplay block, or mouse interception.
- Use `SiriusUiMetrics.SafeFrameInsets(...)` for 24 px standard / 12 px compact safe placement and ultrawide `SideInset`.
- Use 360 px standard / 280 px compact toast width as local Game layout constants; do not add shared theme metrics for one consumer.
- Duration is 2.0 seconds per toast. The timer uses Always processing so a toast cannot freeze and reappear stale after a paused/hidden-HUD screen.
- Within one treasure result: gold first, then inventory-added items sorted by item ID ordinal, then Recovery Chest overflow sorted ordinal, then unrecovered overflow sorted ordinal.
- Resolve item display names through existing `ItemCatalog.CreateItemById(id)?.DisplayName`; do not introduce a second item registry.
- Do not expose `SkippedItemIds` as raw player-facing IDs.
- Do not add item-art support to `SiriusToastShell` in HPA-573.
- Update only the HPA-376 reward/treasure lifecycle rows touched by the final implementation.

---

## File Structure

### Modify

- `scenes/ui/components/SiriusToastShell.tscn` — make the reusable toast leaf explicitly mouse-transparent.
- `tests/ui/components/SiriusToastShellTest.cs` — pin the non-interactive component contract.
- `scenes/game/Game.tscn` — author `%RewardToastMargin`, `%RewardToastColumn`, hidden `%RewardToast`, and `%RewardToastTimer`.
- `scripts/game/Game.cs` — bind the authored nodes; add private FIFO, layout, timer advancement, treasure-result mapping, navigation/root cleanup.
- `tests/game/GameTest.cs` — queue/layout/navigation/teardown tests plus end-to-end treasure feedback/no-regrant coverage.
- `tests/ui/BattleManagerTest.cs` — characterize existing readable battle result and prove rendering is mutation-free.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile `WORLD-TREASURE`, `REWARD-TOAST`, and `REWARD-BLOCKING` with HPA-573/HPA-356 reality.

### Reference only unless a focused test proves a defect

- `scripts/data/TreasureReward.cs`
- `scripts/game/TreasureBoxSpawn.cs`
- `scripts/data/BattleResultSummary.cs`
- `scripts/data/LootResult.cs`
- `scripts/data/items/ItemCatalog.cs`
- `scripts/ui/BattleManager.cs`
- `scripts/ui/components/SiriusToastShell.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/hosting/UIScreenHost.cs`

---

## Risk Checklist

### Toasts accidentally consume pointer input

`SiriusToastShell.tscn` currently has normal `Control`/`PanelContainer` mouse filtering because it has only been a showcase leaf. Make the whole reusable toast subtree mouse-transparent and test it recursively before mounting it over gameplay.

### Multiple rewards become nondeterministic because dictionaries are enumerated directly

Never rely on `Dictionary` enumeration as presentation order. The Game mapper explicitly sorts each item-result dictionary by key using `StringComparer.Ordinal`.

### UI re-grants because it reconstructs a `TreasureReward`

Do not call `BuildReward()` a second time and do not grant from presentation. Capture the existing `TreasureRewardGrantResult` once and map only its scalar/dictionary values.

### Battle gets duplicate reward presentation

Do not enqueue `BattleResultSummary` after Battle closes. Characterize the existing Result phase and leave its production route alone unless the test exposes a concrete readability bug.

### Pending toast survives title/navigation

Clear reward feedback synchronously after the existing `_sceneChangeCommitted` one-shot latch succeeds, before `UIScreenHost.PrepareForTeardown()`. `_ExitTree()` repeats idempotent cleanup for external/free-root teardown.

### Paused screen freezes an active toast

Use an authored one-shot Timer with `ProcessMode = Always`; the toast remains nonblocking and expires even if Pause or a HUD-hiding surface appears.

---

# Task 1: Make the existing toast leaf non-interactive and author the Game reward slot

**Files:**
- Modify: `scenes/ui/components/SiriusToastShell.tscn`
- Modify: `tests/ui/components/SiriusToastShellTest.cs`
- Modify: `scenes/game/Game.tscn`
- Modify: `tests/game/GameTest.cs`

**Interfaces:**

Authored Game nodes:

```text
UI/GameUI/RewardToastMargin (%RewardToastMargin)
UI/GameUI/RewardToastMargin/RewardToastColumn (%RewardToastColumn)
UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast (%RewardToast)
RewardToastTimer (%RewardToastTimer)
```

`%RewardToast` is a `SiriusToastShell` instance hidden initially. `%RewardToastTimer` is `OneShot = true`, `WaitTime = 2.0`, `ProcessMode = Always`.

## 1.1 Write the failing toast mouse-transparency test

- [ ] Add to `SiriusToastShellTest`:

```csharp
[TestCase]
public void Scene_AllControlsAreMouseTransparent()
{
    AssertMouseTransparent(_shell);
}

private static void AssertMouseTransparent(Node node)
{
    if (node is Control control)
        AssertThat(control.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);

    foreach (Node child in node.GetChildren())
        AssertMouseTransparent(child);
}
```

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest.Scene_AllControlsAreMouseTransparent"
```

Expected: FAIL because the existing toast scene has not declared the non-interactive contract.

## 1.2 Make the reusable toast subtree mouse-transparent

- [ ] Set `mouse_filter = 2` (`Ignore`) on every `Control` node in `SiriusToastShell.tscn`: root, `%Panel`, Margin, Row, `%SeverityIcon`, TextColumn, `%TitleLabel`, `%MessageLabel`.

Do not add timers, close buttons, focus controls, or queue behavior to the component.

- [ ] Re-run the full toast component suite:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

Expected: PASS.

## 1.3 Write RED Game scene-structure coverage

- [ ] Add a real `Game.tscn` structure test near `GameSceneUsesExplorationHudWithoutPrototypeHud`:

```csharp
[TestCase]
public async Task GameSceneAuthorsNonBlockingRewardToastRegion()
{
    var game = await InstantiateRealGameScene();
    try
    {
        var margin = game.GetNode<MarginContainer>("UI/GameUI/RewardToastMargin");
        var column = game.GetNode<VBoxContainer>("UI/GameUI/RewardToastMargin/RewardToastColumn");
        var toast = game.GetNode<SiriusToastShell>(
            "UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast");
        var timer = game.GetNode<Timer>("RewardToastTimer");

        AssertThat(margin.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(column.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(toast.Visible).IsFalse();
        AssertThat(timer.OneShot).IsTrue();
        AssertThat(timer.WaitTime).IsEqual(2.0d);
        AssertThat(timer.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
    }
    finally
    {
        game.Free();
        await AwaitFrames(1);
    }
}
```

- [ ] Run the single test. Expected: FAIL because the authored nodes do not exist.

## 1.4 Author the production slot

- [ ] Add `SiriusToastShell.tscn` as an ext_resource in `Game.tscn` and author:

```text
RewardToastMargin
  full-viewport anchors
  mouse_filter = Ignore
  top/right margins controlled by Game

RewardToastColumn
  horizontal size flag = ShrinkEnd
  vertical size flag = ShrinkBegin
  mouse_filter = Ignore

RewardToast
  instance SiriusToastShell.tscn
  visible = false
  horizontal size flag = ExpandFill

RewardToastTimer
  one_shot = true
  wait_time = 2.0
  process_mode = Always
```

Do not add a CanvasLayer, scrim, modal, button, animation player, or second timer.

- [ ] Re-run `GameSceneAuthorsNonBlockingRewardToastRegion`. Expected: PASS.

- [ ] Commit:

```bash
git add scenes/ui/components/SiriusToastShell.tscn \
  tests/ui/components/SiriusToastShellTest.cs \
  scenes/game/Game.tscn tests/game/GameTest.cs
git commit -m "ui: author nonblocking reward toast slot"
```

---

# Task 2: Add the Game-owned FIFO, responsive layout, and teardown contract

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameTest.cs`
- Reference: `scripts/ui/theme/SiriusUiMetrics.cs`

**Interfaces produced:**

```csharp
private sealed record RewardToastRequest(
    string Title,
    string Message,
    SiriusUiSeverity Severity);

private const double RewardToastDurationSeconds = 2.0;
private readonly Queue<RewardToastRequest> _rewardToastQueue = new();

private void EnqueueRewardToast(RewardToastRequest request);
private void ShowNextRewardToast();
private void ClearRewardFeedback();
private void RefreshRewardToastLayout();
```

Authored-node fields:

```csharp
private MarginContainer _rewardToastMargin = null!;
private VBoxContainer _rewardToastColumn = null!;
private SiriusToastShell _rewardToast = null!;
private Timer _rewardToastTimer = null!;
```

## 2.1 Write RED FIFO/no-input tests

- [ ] In `GameTest`, mount the real Game scene and invoke the private queue through the suite's existing reflection helper:

```csharp
var first = CreatePrivateRewardToastRequest(game, "First", "25 Gold", SiriusUiSeverity.Success);
var second = CreatePrivateRewardToastRequest(game, "Second", "Health Potion ×1", SiriusUiSeverity.Success);
InvokePrivate(game, "EnqueueRewardToast", first);
InvokePrivate(game, "EnqueueRewardToast", second);
```

Add a test-local reflection helper that constructs the nested private record by name; keep it in `GameTest`, not production:

```csharp
private static object CreatePrivateRewardToastRequest(
    Game game,
    string title,
    string message,
    SiriusUiSeverity severity)
{
    var type = typeof(Game).GetNestedType(
        "RewardToastRequest",
        BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RewardToastRequest not found.");

    return Activator.CreateInstance(type, title, message, severity)
        ?? throw new InvalidOperationException("Failed to create RewardToastRequest.");
}
```

Pin:

```csharp
AssertThat(toast.Visible).IsTrue();
AssertThat(toast.Title).IsEqual("First");
AssertThat(toast.Message).IsEqual("25 Gold");
AssertThat(game.GetTree().Paused).IsFalse();
AssertThat(game.GetNode<UIScreenHost>("UI/UIScreenHost").ActiveEntries.Count).IsEqual(0);

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Title).IsEqual("Second");

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Visible).IsFalse();
```

Expected: RED because the private queue type/methods do not exist.

## 2.2 Write RED responsive-layout coverage

- [ ] At 640×360, call `RefreshRewardToastLayout()` and assert:

```csharp
AssertThat(toast.Compact).IsTrue();
AssertThat(margin.GetThemeConstant("margin_top")).IsEqual(12);
AssertThat(margin.GetThemeConstant("margin_right")).IsEqual(12);
AssertThat(column.CustomMinimumSize.X).IsEqual(280f);
```

- [ ] At 1280×720, assert:

```csharp
AssertThat(toast.Compact).IsFalse();
AssertThat(margin.GetThemeConstant("margin_top")).IsEqual(24);
AssertThat(margin.GetThemeConstant("margin_right")).IsEqual(24);
AssertThat(column.CustomMinimumSize.X).IsEqual(360f);
```

Use the existing `SubViewport` fixture/real-scene helper rather than introducing a layout service.

## 2.3 Implement binding and queue mechanics

- [ ] In `_Ready()`, bind the four authored nodes before reward feedback can be used:

```csharp
_rewardToastMargin = GetNode<MarginContainer>("UI/GameUI/RewardToastMargin");
_rewardToastColumn = GetNode<VBoxContainer>("UI/GameUI/RewardToastMargin/RewardToastColumn");
_rewardToast = GetNode<SiriusToastShell>("UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast");
_rewardToastTimer = GetNode<Timer>("RewardToastTimer");
_rewardToast.Hide();
_rewardToastTimer.Timeout += ShowNextRewardToast;
GetViewport().SizeChanged += RefreshRewardToastLayout;
RefreshRewardToastLayout();
```

- [ ] Add the private record, queue, and methods exactly as specified by the design:

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

- [ ] Add responsive layout:

```csharp
private void RefreshRewardToastLayout()
{
    if (_rewardToastMargin == null || !IsInstanceValid(_rewardToastMargin))
        return;

    var (compact, margin, sideInset) = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
    _rewardToastMargin.AddThemeConstantOverride("margin_top", (int)margin);
    _rewardToastMargin.AddThemeConstantOverride("margin_right", (int)sideInset);
    _rewardToastColumn.CustomMinimumSize = new Vector2(compact ? 280f : 360f, 0f);
    _rewardToast.Compact = compact;
}
```

## 2.4 Implement synchronous navigation + root cleanup

- [ ] Add:

```csharp
private void ClearRewardFeedback()
{
    if (_rewardToastTimer != null && IsInstanceValid(_rewardToastTimer))
        _rewardToastTimer.Stop();

    _rewardToastQueue.Clear();

    if (_rewardToast != null && IsInstanceValid(_rewardToast))
        _rewardToast.Hide();
}
```

- [ ] In `RequestSceneChange(string path)`, after `_sceneChangeCommitted = true` and before `UpdateInteractionPrompt()` / host teardown, call `ClearRewardFeedback()`.

- [ ] In the existing `_ExitTree()`:

```csharp
ClearRewardFeedback();

if (_rewardToastTimer != null && IsInstanceValid(_rewardToastTimer))
    _rewardToastTimer.Timeout -= ShowNextRewardToast;

var viewport = GetViewport();
if (viewport != null)
    viewport.SizeChanged -= RefreshRewardToastLayout;
```

Keep all existing teardown logic in place.

## 2.5 Pin navigation and raw root teardown

- [ ] Add `RewardToast_RequestSceneChange_ClearsActiveAndQueuedFeedback` using `TestableGame`'s existing navigation override. Enqueue two requests, invoke `RequestSceneChange`, and assert toast hidden, timer stopped, and private queue count `0` before the test navigation hook reports the request.

- [ ] Add `RewardToast_RootExit_ClearsActiveAndQueuedFeedback`: enqueue two requests, remove the Game node from its viewport without freeing it, await one frame, then assert the still-valid detached Game has a hidden toast, stopped timer, and queue count `0`; reattach or free in `finally` so the suite cleanup remains deterministic.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest&Name~RewardToast"
```

Expected: PASS.

- [ ] Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "ui: add root reward toast queue"
```

---

# Task 3: Feed the existing treasure grant result into the FIFO without re-granting

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

## 3.1 Extend the existing real treasure regression before changing production

- [ ] Extend `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt` rather than adding a fake grant route.

Keep its existing assertions for:

```csharp
AssertThat(gameManager.Player.Gold).IsEqual(startingGold + 25);
AssertThat(gameManager.Player.GetItemQuantity("health_potion"))
    .IsEqual(startingPotionCount + 1);
AssertThat(gameManager.IsTreasureBoxOpened("TreasureBox_RuntimeTest")).IsTrue();
AssertThat(box.IsOpened).IsTrue();
```

Then assert the new presentation:

```csharp
var toast = gameScene.GetNode<SiriusToastShell>(
    "UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast");
var timer = gameScene.GetNode<Timer>("RewardToastTimer");

AssertThat(toast.Visible).IsTrue();
AssertThat(toast.Title).IsEqual("Treasure Acquired");
AssertThat(toast.Message).IsEqual("25 Gold");

int goldAfterGrant = gameManager.Player.Gold;
int potionsAfterGrant = gameManager.Player.GetItemQuantity("health_potion");

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Title).IsEqual("Item Acquired");
AssertThat(toast.Message).IsEqual("Health Potion ×1");
AssertThat(gameManager.Player.Gold).IsEqual(goldAfterGrant);
AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(potionsAfterGrant);

timer.EmitSignal(Timer.SignalName.Timeout);
AssertThat(toast.Visible).IsFalse();
AssertThat(gameManager.Player.Gold).IsEqual(goldAfterGrant);
AssertThat(gameManager.Player.GetItemQuantity("health_potion")).IsEqual(potionsAfterGrant);
```

Keep the existing second-interaction assertion and additionally verify it does not make the toast visible again.

- [ ] Run the single test. Expected: FAIL only on new presentation assertions; the existing grant assertions stay green.

## 3.2 Add a deterministic multi-item ordering test

- [ ] Add a second real treasure box fixture whose authored item list is intentionally reverse-alphabetical:

```csharp
RewardGold = 5,
RewardItemIds = new Godot.Collections.Array<string>
{
    "mana_potion",
    "health_potion"
},
RewardItemQuantities = new Godot.Collections.Array<int> { 1, 2 }
```

After opening, manually advance the timer and assert:

```text
5 Gold
Health Potion ×2
Mana Potion ×1
```

This pins `gold -> Ordinal(itemId)` rather than dictionary insertion/authored order.

## 3.3 Capture the resolved result exactly once

- [ ] In `OnTreasureBoxOpenRequested(...)`, replace only the discarded call:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Do not call `box.BuildReward()` again and do not move the existing grant/persist/cell/stat operations into UI code.

## 3.4 Implement deterministic mapping

- [ ] Add:

```csharp
private void EnqueueTreasureRewardFeedback(TreasureRewardGrantResult result)
{
    if (result.GoldGranted > 0)
    {
        EnqueueRewardToast(new RewardToastRequest(
            "Treasure Acquired",
            $"{result.GoldGranted} Gold",
            SiriusUiSeverity.Success));
    }

    foreach (var (itemId, quantity) in result.ItemQuantitiesGranted
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        EnqueueRewardToast(new RewardToastRequest(
            "Item Acquired",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity}",
            SiriusUiSeverity.Success));
    }

    foreach (var (itemId, quantity) in result.ItemQuantitiesRecovered
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        EnqueueRewardToast(new RewardToastRequest(
            "Recovery Chest",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity} sent to the Recovery Chest",
            SiriusUiSeverity.Warning));
    }

    foreach (var (itemId, quantity) in result.UnrecoveredItemQuantities
                 .OrderBy(pair => pair.Key, StringComparer.Ordinal))
    {
        EnqueueRewardToast(new RewardToastRequest(
            "Inventory Full",
            $"{ResolveRewardItemDisplayName(itemId)} ×{quantity} could not be stored",
            SiriusUiSeverity.Error));
    }
}

private static string ResolveRewardItemDisplayName(string itemId) =>
    ItemCatalog.CreateItemById(itemId)?.DisplayName ?? itemId;
```

`System.Linq` is already available in files that need it only if present; add it to `Game.cs` if not already imported. `System` is already present for `StringComparer`.

Do not enqueue `SkippedItemIds` or raw `Errors` as player-facing content.

## 3.5 Preserve aborted-open silence

- [ ] Extend `Game_AbortedTreasureOpeningDoesNotGrantRewardOrPersistOpenedId`:

```csharp
var toast = gameScene.GetNode<SiriusToastShell>(
    "UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast");
AssertThat(toast.Visible).IsFalse();
AssertThat(GetPrivateQueueCount(gameScene, "_rewardToastQueue")).IsEqual(0);
```

Use the suite's existing reflection style for the queue count; do not add a public count property.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest&Name~Treasure"
```

Expected: PASS.

- [ ] Commit:

```bash
git add scripts/game/Game.cs tests/game/GameTest.cs
git commit -m "ui: present resolved treasure rewards"
```

---

# Task 4: Pin Battle's existing required result path as HPA-573 coverage

**Files:**
- Modify: `tests/ui/BattleManagerTest.cs`
- Reference: `scripts/ui/BattleManager.cs`
- Reference: `scripts/data/BattleResultSummary.cs`
- Reference: `scripts/data/LootResult.cs`

**Interfaces:** No new production interface.

## 4.1 Add a characterization test for readable resolved result data

- [ ] In `BattleManagerTest`, use the existing `WithBattleManager(...)` and `InvokePrivateMethod(...)` helpers:

```csharp
[TestCase]
public void RenderResult_ResolvedRewardsAreReadableAndDoNotMutatePlayer()
{
    WithBattleManager(battleManager =>
    {
        var player = TestHelpers.CreateTestCharacter();
        player.Gold = 10;
        player.Experience = 7;
        int goldBefore = player.Gold;
        int experienceBefore = player.Experience;
        int potionBefore = player.GetItemQuantity("health_potion");

        var loot = new LootResult();
        loot.Add(ConsumableCatalog.CreateHealthPotion(), 2);
        var result = new BattleResultSummary(
            PlayerWon: true,
            ExperienceGained: 40,
            GoldGained: 12,
            PreviousLevel: 1,
            NewLevel: 2,
            Loot: loot);

        SetPrivateField(battleManager, "_player", player);
        InvokePrivateMethod(battleManager, "RenderResult", result);

        AssertThat(battleManager.GetNode<Label>("%ResultTitle").Text).IsEqual("VICTORY");
        AssertThat(battleManager.GetNode<Label>("%ExperienceResult").Text).IsEqual("Experience: 40");
        AssertThat(battleManager.GetNode<Label>("%GoldResult").Text).IsEqual("Gold: 12");
        AssertThat(battleManager.GetNode<Label>("%LevelResult").Text).IsEqual("Level: 1 → 2");
        AssertThat(battleManager.GetNode<Label>("%LootResultList").Text)
            .Contains("2x Health Potion");

        AssertThat(player.Gold).IsEqual(goldBefore);
        AssertThat(player.Experience).IsEqual(experienceBefore);
        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(potionBefore);
    });
}
```

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~BattleManagerTest.RenderResult_ResolvedRewardsAreReadableAndDoNotMutatePlayer"
```

Expected: PASS against current `BattleManager`. If it passes, **do not modify production Battle code**. If a label differs because current production copy is intentionally different but still readable, update the assertion to the exact current copy; do not use the test as a reason to build a second result surface.

- [ ] Commit the characterization only:

```bash
git add tests/ui/BattleManagerTest.cs
git commit -m "test: pin battle reward result presentation"
```

---

# Task 5: Reconcile the lifecycle contract and validate the whole HPA-573 slice

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Verify all files changed by Tasks 1-4

## 5.1 Update only the three reward-related lifecycle rows

- [ ] `WORLD-TREASURE` must state:

```text
Game captures TreasureRewardGrantResult after the existing one-time grant,
persists the opened ID/cell state, notifies player stats, then enqueues
presentation-only feedback. The world-interaction finally block remains the
sole gameplay-latch cleanup.
```

Protecting evidence includes the existing award-once and abort tests plus the new sequential-toast test.

- [ ] `REWARD-TOAST` must replace the stale future/HPA-378/379 wording with:

```text
Owner: Game root
Surface: one authored SiriusToastShell under GameUI
Queue: root-local FIFO; one active request; 2.0 s Always Timer
Input: no host entry, no pause, HUD retained when GameUI is visible,
       cursor unchanged, no focus/Cancel owner, mouse transparent
Cleanup: RequestSceneChange + _ExitTree clear active/pending feedback
Grant authority: producer only
Disposition: Preserve
```

- [ ] `REWARD-BLOCKING` must point at the existing HPA-356 `BattleManager` Result phase and `BattleResultSummary`, with Continue as the required acknowledgement. Remove stale references that imply HPA-393 or unfinished HPA-378/379 are required.

Do not edit unrelated lifecycle rows.

## 5.2 Run focused HPA-573 suites

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest|FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest"
```

Expected: PASS.

## 5.3 Run the full test suite

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS with no HPA-573 regressions.

## 5.4 Build and diff-check

- [ ] Run:

```bash
dotnet build Sirius.sln --no-restore --nologo
git diff --check main...HEAD
git status --short
```

Expected:

- build succeeds;
- `git diff --check` emits no whitespace errors;
- status contains no unintended files.

- [ ] Review the final diff and verify there is **no** new reward service/manager, host kind, persistence field, save-schema change, second battle result surface, or domain mutation from presentation.

## 5.5 Commit lifecycle reconciliation

- [ ] Commit:

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: finalize HPA-573 reward lifecycle"
```

---

## Final acceptance checklist

- [ ] Opening the existing test treasure still grants exactly 25 gold + one potion once.
- [ ] The same invocation shows gold then potion as sequential toasts.
- [ ] Advancing/clearing toasts does not change gold, inventory, XP, or level.
- [ ] A second interaction with an opened box neither grants nor enqueues again.
- [ ] Multiple item outcomes use explicit ordinal item-ID ordering.
- [ ] Aborted treasure opening produces no reward and no toast.
- [ ] Toasts never register with `UIScreenHost`, pause the tree, change cursor/focus, or intercept mouse input.
- [ ] 640×360 uses compact toast typography, 12 px safe inset, and 280 px width.
- [ ] 1280×720 uses standard typography, 24 px safe inset, and 360 px width.
- [ ] Return-to-Title/scene navigation clears active + queued feedback synchronously.
- [ ] Raw root exit clears active + queued feedback.
- [ ] Battle Result still renders resolved XP/gold/level/loot and rendering itself mutates nothing.
- [ ] HPA-376 reward rows describe final HPA-573/HPA-356 ownership instead of stale future handoffs.
- [ ] Full tests and build pass.
