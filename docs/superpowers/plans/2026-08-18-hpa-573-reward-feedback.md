# HPA-573 Simple Reward Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add non-blocking sequential treasure reward toasts while retaining Battle's existing blocking Result phase and proving presentation never grants rewards.

**Architecture:** Keep reward mutation in existing producers. `Game` captures `TreasureRewardGrantResult`, maps it into a private FIFO of title/message/severity requests, and drives one authored `SiriusToastShell` with one one-shot Timer. Battle stays production-unchanged because its existing Result phase already renders `BattleResultSummary` and requires Continue. No reward service, global queue, new host kind, persistence, identity, retry, or second battle modal.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusToastShell`, `SiriusUiMetrics`; existing `UIScreenHost` remains unrelated to toast ownership.

**Spec:** `docs/superpowers/specs/2026-08-18-hpa-573-reward-feedback-design.md`

## Global Constraints

- Deliver HPA-573 in **one implementation PR**. Tasks below are review/commit boundaries inside that PR, not separate PRs.
- `TreasureReward.GrantTo(...)`, `TreasureBoxSpawn.GrantRewardTo(...)`, battle reward calculation, save state, and inventory/experience mutation remain domain-owned.
- Presentation never calls `GrantTo`, `GainGold`, `TryAddItem`, experience/level mutation, save/load, or navigation APIs.
- Keep `BattleManager`'s current Result phase as the important/required reward acknowledgement path; do not add a post-battle toast or shared reward modal.
- Add no `RewardManager`, notification singleton, event bus, presenter/view-model, public reward DTO, new `UIScreenKind`, exclusive group, or host policy.
- Treasure toasts are root-local FIFO presentation only and are discarded on navigation/root teardown.
- One authored `SiriusToastShell` is reused sequentially; do not stack simultaneous reward nodes.
- Toasts have no focus, Cancel handling, tree pause, gameplay block, or mouse interception.
- Use `SiriusUiMetrics.SafeFrameInsets(...)` for 24 px standard / 12 px compact placement and ultrawide `SideInset`.
- Use 360 px standard / 280 px compact toast width as local Game layout constants; do not add a shared metric for one consumer.
- Duration is 2.0 seconds. The one-shot Timer uses `ProcessMode = Always` so a hidden/paused toast cannot freeze and later reappear stale.
- Within one treasure result: gold first, then inventory-added items sorted by item ID ordinal, then Recovery Chest overflow sorted ordinal, then unrecovered overflow sorted ordinal.
- Resolve names through `ItemCatalog.CreateItemById(id)?.DisplayName`; do not introduce a second item registry.
- Do not surface `SkippedItemIds` as raw player-facing IDs.
- Do not add item-art support to `SiriusToastShell` in HPA-573.
- Update only the HPA-376 treasure/reward lifecycle rows touched by the final implementation.

---

## File Structure

### Modify

- `scenes/ui/components/SiriusToastShell.tscn` — make the reusable toast leaf explicitly mouse-transparent.
- `tests/ui/components/SiriusToastShellTest.cs` — pin the non-interactive component contract.
- `scenes/game/Game.tscn` — author `%RewardToastMargin`, `%RewardToastColumn`, hidden `%RewardToast`, and `%RewardToastTimer`.
- `scripts/game/Game.cs` — bind authored nodes; add private FIFO, layout, timer advancement, treasure-result mapping, and navigation/root cleanup.
- `tests/game/GameTest.cs` — queue/layout/navigation/teardown plus end-to-end treasure/no-regrant coverage.
- `tests/ui/BattleManagerTest.cs` — characterize existing readable battle result and prove rendering is mutation-free.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile `WORLD-TREASURE`, `REWARD-TOAST`, and `REWARD-BLOCKING`.

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

`SiriusToastShell.tscn` has only been a showcase leaf, so make its whole reusable `Control` subtree mouse-transparent and test that invariant recursively before mounting it over gameplay.

### Item order depends on dictionary enumeration

Never use dictionary enumeration as the presentation contract. Sort each resolved item-result dictionary with `StringComparer.Ordinal`.

### UI re-grants by reconstructing reward input

Do not call `BuildReward()` again. Capture `TreasureRewardGrantResult` from the one existing grant call and map only its resolved values.

### Battle gets duplicate presentation

Do not enqueue `BattleResultSummary` after Battle closes. Characterize the existing Result phase and leave production Battle code alone unless the focused test reveals an actual readability defect.

### Pending feedback survives title/navigation

Clear feedback immediately after `_sceneChangeCommitted` latches in `RequestSceneChange(...)`, before host teardown. `_ExitTree()` repeats idempotent cleanup for external/root removal.

### A pause freezes active reward feedback

Use an authored one-shot Timer with `ProcessMode = Always`; no second timer or scheduler is needed.

---

# Task 1: Make the existing toast leaf non-interactive and author the Game reward slot

**Files:**
- Modify: `scenes/ui/components/SiriusToastShell.tscn`
- Modify: `tests/ui/components/SiriusToastShellTest.cs`
- Modify: `scenes/game/Game.tscn`
- Modify: `tests/game/GameTest.cs`

**Interfaces:**

```text
UI/GameUI/RewardToastMargin (%RewardToastMargin)
UI/GameUI/RewardToastMargin/RewardToastColumn (%RewardToastColumn)
UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast (%RewardToast)
RewardToastTimer (%RewardToastTimer)
```

`%RewardToast` is a hidden `SiriusToastShell`. `%RewardToastTimer` is `OneShot = true`, `WaitTime = 2.0`, `ProcessMode = Always`.

## 1.1 Write the failing component-input test

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

Expected: FAIL because the current scene does not declare this invariant.

## 1.2 Make the reusable toast subtree transparent

- [ ] Set `mouse_filter = 2` (`Ignore`) on every `Control` in `SiriusToastShell.tscn`: root, `%Panel`, Margin, Row, `%SeverityIcon`, TextColumn, `%TitleLabel`, `%MessageLabel`.

Do not add timer, close button, focus, queue, or domain behavior to the component.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

Expected: PASS.

## 1.3 Write RED Game scene-structure coverage

- [ ] Add near `GameSceneUsesExplorationHudWithoutPrototypeHud`:

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

- [ ] Run that test. Expected: FAIL because nodes do not exist.

## 1.4 Author the production slot

- [ ] Add `SiriusToastShell.tscn` as an ext-resource in `Game.tscn` and author:

```text
RewardToastMargin
  full viewport
  mouse_filter = Ignore
  margins updated by Game

RewardToastColumn
  horizontal = ShrinkEnd
  vertical = ShrinkBegin
  mouse_filter = Ignore

RewardToast
  SiriusToastShell instance
  visible = false
  horizontal = ExpandFill

RewardToastTimer
  one_shot = true
  wait_time = 2.0
  process_mode = Always
```

Do not add CanvasLayer, scrim, modal, button, AnimationPlayer, or second timer.

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

Fields:

```csharp
private MarginContainer _rewardToastMargin = null!;
private VBoxContainer _rewardToastColumn = null!;
private SiriusToastShell _rewardToast = null!;
private Timer _rewardToastTimer = null!;
```

## 2.1 Write RED FIFO/no-host coverage

- [ ] Use a real `Game.tscn` and the suite's existing reflection style. Add this test-only constructor helper:

```csharp
private static object CreatePrivateRewardToastRequest(
    string title,
    string message,
    SiriusUiSeverity severity)
{
    var type = typeof(Game).GetNestedType(
        "RewardToastRequest",
        BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("RewardToastRequest not found.");

    return Activator.CreateInstance(
        type,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        args: new object[] { title, message, severity },
        culture: null)
        ?? throw new InvalidOperationException("Failed to create RewardToastRequest.");
}
```

Then:

```csharp
var first = CreatePrivateRewardToastRequest("First", "25 Gold", SiriusUiSeverity.Success);
var second = CreatePrivateRewardToastRequest("Second", "Health Potion ×1", SiriusUiSeverity.Success);
InvokePrivate(game, "EnqueueRewardToast", first);
InvokePrivate(game, "EnqueueRewardToast", second);

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

## 2.2 Write RED responsive-layout coverage in controlled SubViewports

- [ ] Reuse the suite's `_viewport`, temporarily setting its size and adding a real `Game.tscn` instance to it (do not use `InstantiateRealGameScene()`, which mounts under the root viewport).

At 640×360:

```csharp
AssertThat(toast.Compact).IsTrue();
AssertThat(margin.GetThemeConstant("margin_top")).IsEqual(12);
AssertThat(margin.GetThemeConstant("margin_right")).IsEqual(12);
AssertThat(column.CustomMinimumSize.X).IsEqual(280f);
```

At 1280×720:

```csharp
AssertThat(toast.Compact).IsFalse();
AssertThat(margin.GetThemeConstant("margin_top")).IsEqual(24);
AssertThat(margin.GetThemeConstant("margin_right")).IsEqual(24);
AssertThat(column.CustomMinimumSize.X).IsEqual(360f);
```

Restore `_viewport.Size` in `finally` and free each real Game before the next case.

## 2.3 Implement binding and FIFO

- [ ] In `Game._Ready()`:

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

- [ ] Add:

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

## 2.4 Implement responsive safe-frame placement

- [ ] Add:

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

## 2.5 Implement synchronous scene-change + root cleanup

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

- [ ] In `RequestSceneChange(string path)`, call `ClearRewardFeedback()` immediately after `_sceneChangeCommitted = true` and before `UpdateInteractionPrompt()` / `ContinueSceneChangeAfterUiTeardown()`.

- [ ] In existing `_ExitTree()` call `ClearRewardFeedback()`, unsubscribe `_rewardToastTimer.Timeout`, and unsubscribe viewport `SizeChanged` while the referenced objects are still valid. Preserve all existing teardown logic.

## 2.6 Pin scene-change and raw-root teardown without navigating the test runner

- [ ] Add `RewardToast_SceneChangeRequestClearsActiveAndQueuedFeedback` using a **real** Game. Enqueue two requests, then invoke:

```csharp
InvokePrivate(game, "RequestSceneChange", string.Empty);
```

An empty path exercises the same one-shot cleanup used by `ReturnToMainMenu()` but `ContinueSceneChangeAfterUiTeardown()` does not call `ChangeSceneToFile`, so the test runner stays on its fixture scene.

Assert toast hidden, timer stopped, and private queue `Count == 0`.

- [ ] Add `RewardToast_RootExitClearsActiveAndQueuedFeedback`: enqueue two requests, remove the real Game from its parent without freeing it, await one frame, then inspect the still-valid detached node and assert hidden toast, stopped timer, and queue `Count == 0`. Free the detached Game in `finally`; do not reattach a fully exited Game scene.

Use a test-only reflection helper for queue count; do not add a public production count.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest.RewardToast"
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

## 3.1 Extend the existing real treasure regression first

- [ ] Extend `Game_OpeningAdjacentTreasureAwardsOnceAndShowsOpenPrompt` and keep every existing grant/opened/prompt assertion.

After the existing opening completes:

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

Keep the existing second-interaction assertion and additionally assert it does not show `%RewardToast` again.

Expected before production change: new presentation assertions fail; existing grant-once assertions remain green.

## 3.2 Add deterministic multi-item ordering coverage

- [ ] Add a real treasure fixture with reverse-alphabetical authored items:

```csharp
RewardGold = 5,
RewardItemIds = new Godot.Collections.Array<string>
{
    "mana_potion",
    "health_potion"
},
RewardItemQuantities = new Godot.Collections.Array<int> { 1, 2 }
```

After opening, manually advance the Timer and assert this display sequence:

```text
5 Gold
Health Potion ×2
Mana Potion ×1
```

This pins `gold -> Ordinal(itemId)` rather than dictionary insertion or authored order.

## 3.3 Capture the resolved result exactly once

- [ ] In `OnTreasureBoxOpenRequested(...)`, replace the discarded return value only:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Do not call `box.BuildReward()` again and do not move domain mutation into presentation methods.

## 3.4 Implement deterministic mapping

- [ ] Add `using System.Linq;` to `Game.cs` and implement:

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

Do not enqueue `SkippedItemIds` or raw `Errors`.

## 3.5 Preserve aborted-open silence

- [ ] Extend `Game_AbortedTreasureOpeningDoesNotGrantRewardOrPersistOpenedId` to assert `%RewardToast.Visible == false` and private `_rewardToastQueue.Count == 0` after the abort.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~GameTest"
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

- [ ] Use existing `WithBattleManager(...)`, `SetPrivateField(...)`, and `InvokePrivateMethod(...)`:

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

Expected: PASS against current `BattleManager`. If the exact current copy differs while conveying the same value, align the assertion to production copy; do not use this task to create another result surface.

- [ ] If the test passes, commit the test only:

```bash
git add tests/ui/BattleManagerTest.cs
git commit -m "test: pin battle reward result presentation"
```

---

# Task 5: Reconcile lifecycle documentation and validate the complete slice

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Verify all Task 1-4 files

## 5.1 Update only the three reward-related lifecycle rows

- [ ] `WORLD-TREASURE` must record that Game captures `TreasureRewardGrantResult` after the one-time grant, preserves opened-ID/cell/stat updates, then hands resolved values to root-local presentation. Existing `finally` remains world-latch cleanup.

- [ ] `REWARD-TOAST` must replace future/HPA-378/379 wording with:

```text
Owner: Game root
Surface: one authored SiriusToastShell under GameUI
Queue: root-local FIFO; one active request; 2.0 s Always Timer
Input: no host entry, no pause, no focus/Cancel owner, mouse transparent
HUD/cursor: existing GameUI/host policy; toast itself changes neither
Cleanup: RequestSceneChange + _ExitTree
Grant authority: producer only
Disposition: Preserve
```

- [ ] `REWARD-BLOCKING` must point to HPA-356's current `BattleManager` Result phase / `BattleResultSummary` and Continue acknowledgement. Remove stale language implying canceled HPA-393 or completed HPA-378/379 still need to build a shared reward surface.

Do not rewrite unrelated lifecycle rows.

## 5.2 Run focused HPA-573 suites

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest|FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest"
```

Expected: PASS.

## 5.3 Run full suite, build, and diff checks

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check main...HEAD
git status --short
```

Expected: tests/build pass, diff check is clean, and no unintended files appear.

- [ ] Review the final diff and verify there is **no** reward service/manager, host kind, persistence field, save-schema change, second battle result surface, or domain mutation from presentation.

## 5.4 Commit lifecycle reconciliation

- [ ] Commit:

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: finalize HPA-573 reward lifecycle"
```

---

## Final Acceptance Checklist

- [ ] Existing treasure still grants exactly once.
- [ ] One gold + one item invocation shows two sequential toasts.
- [ ] Advancing/clearing presentation does not change gold, inventory, XP, or level.
- [ ] Re-interacting with an opened box neither grants nor enqueues again.
- [ ] Multiple items use explicit ordinal item-ID ordering.
- [ ] Aborted treasure opening grants nothing and queues nothing.
- [ ] Toasts never register with `UIScreenHost`, pause the tree, change cursor/focus, or intercept mouse input.
- [ ] 640×360 uses compact typography, 12 px inset, 280 px width.
- [ ] 1280×720 uses standard typography, 24 px inset, 360 px width.
- [ ] Any scene-change request—including Return to Title through the same method—clears active + queued feedback before host teardown.
- [ ] Raw root exit clears active + queued feedback.
- [ ] Battle Result still renders XP/gold/level/loot and rendering itself mutates nothing.
- [ ] HPA-376 reward rows describe final HPA-573/HPA-356 ownership.
- [ ] Full tests and build pass.
