# HPA-573 Simple Reward Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add non-blocking sequential treasure reward toasts while retaining Battle's existing blocking Result phase and proving presentation never grants rewards.

**Architecture:** Keep reward mutation in existing producers. `Game` captures `TreasureRewardGrantResult`, maps it into a private FIFO of title/message/severity requests, and drives one authored `SiriusToastShell` with one one-shot Timer. Battle stays production-unchanged because its existing Result phase already renders `BattleResultSummary` and requires Continue. No reward service, global queue, new host kind, persistence, identity, retry, or second battle modal.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusToastShell`, `SiriusUiMetrics`; existing `UIScreenHost` remains unrelated to production toast ownership.

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
- Keep the toast under `UI/GameUI`; do not route it through `UIScreenLayer.Toast`. Existing host HUD policy should hide exploration feedback when the whole `GameUI` HUD is hidden.
- Use `SiriusUiMetrics.SafeFrameInsets(...)` for 24 px standard / 12 px compact placement and ultrawide `SideInset`.
- Use 360 px standard / 280 px compact toast width as local Game layout constants; do not add a shared metric for one consumer and do not reuse `TooltipMaximum` just because the values match.
- Duration is 2.0 seconds. The one-shot Timer uses `ProcessMode = Always` so a hidden/paused toast cannot freeze and later reappear stale.
- Within one treasure result: gold first, then inventory-added items sorted by item ID ordinal, then Recovery Chest overflow sorted ordinal, then unrecovered overflow sorted ordinal.
- Resolve names through `ItemCatalog.CreateItemById(id)?.DisplayName`; do not introduce a second item registry.
- Do not surface `SkippedItemIds` as raw player-facing IDs.
- Do not add item-art support to `SiriusToastShell` in HPA-573.
- Reconcile the HPA-376 treasure/reward rows and the stale HPA-377 documentation that still assigns toast/reward queue ownership to canceled HPA-386.
- HPA-573 becomes the production treasure-toast owner. Existing `UIScreenKinds.RewardToast` remains a host-test fixture; do **not** delete or repurpose it for runtime treasure feedback.

---

## File Structure

### Modify

- `scenes/ui/components/SiriusToastShell.tscn` — make the reusable toast leaf explicitly mouse-transparent.
- `tests/ui/components/SiriusToastShellTest.cs` — pin the non-interactive component contract.
- `scenes/game/Game.tscn` — author `%RewardToastMargin`, `%RewardToastColumn`, hidden `%RewardToast`, and `%RewardToastTimer` under `UI/GameUI`.
- `scripts/game/Game.cs` — bind authored nodes; add private FIFO, layout, timer advancement, treasure-result mapping, and navigation/root cleanup.
- `tests/game/GameTest.cs` — queue/layout/navigation/teardown, overflow-copy coverage, and end-to-end treasure/no-regrant coverage.
- `tests/ui/BattleManagerTest.cs` — characterize the existing scene-instantiated battle Result presentation and prove rendering is mutation-free.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile `WORLD-TREASURE`, `REWARD-TOAST`, and `REWARD-BLOCKING`.
- `docs/ui/hpa-377/README.md` — replace the stale HPA-386 toast/reward queue handoff with HPA-573 production ownership while keeping `SiriusToastShell` presentation-only.
- `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md` — retarget stale HPA-386 toast/reward queue references to HPA-573 without changing the HPA-377 component contract.

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
- `scripts/ui/hosting/UIScreenKinds.cs`
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`

---

## Risk Checklist

### Toasts accidentally consume pointer input

`SiriusToastShell.tscn` has only been a showcase leaf, so make its whole reusable `Control` subtree mouse-transparent and test that invariant recursively before mounting it over gameplay.

### Item order depends on dictionary enumeration

Never use dictionary enumeration as the presentation contract. Sort each resolved item-result dictionary with `StringComparer.Ordinal`.

### Recovery / unrecovered copy silently regresses

`TreasureRewardGrantResult.ItemQuantitiesRecovered` and `UnrecoveredItemQuantities` are the only resolved failure-facing reward values. Do not force a real inventory-overflow scenario just to test presentation. Construct a resolved result directly in `GameTest`, invoke the private mapper, and assert Warning/Error copy, ordinal ordering, and zero player mutation.

### UI re-grants by reconstructing reward input

Do not call `BuildReward()` again. Capture `TreasureRewardGrantResult` from the one existing grant call and map only its resolved values.

### Ultrawide placement accidentally uses safe margin instead of side inset

640×360 and 1280×720 both have `sideInset == margin`; they cannot distinguish the two formulas. Add one 2560×1080 assertion where `SafeFrameInsets(...)` returns a 480 px side inset.

### Battle characterization uses an unready controller

Do not use `WithBattleManager(...)` for `RenderResult`; it creates a bare `new BattleManager()` and never binds `%ResultTitle`, `%ExperienceResult`, or the other authored nodes. Use `CreateReadyBattleManager()`, which instantiates `BattleScene.tscn`, awaits `_Ready()`, and calls `StartBattle(...)`.

### Battle gets duplicate presentation

Do not enqueue `BattleResultSummary` after Battle closes. Characterize the existing Result phase and leave production Battle code unchanged.

### Pending feedback survives title/navigation

Clear feedback immediately after `_sceneChangeCommitted` latches in `RequestSceneChange(...)`, before host teardown. `_ExitTree()` repeats idempotent cleanup for external/root removal.

### A pause freezes active reward feedback

Use an authored one-shot Timer with `ProcessMode = Always`; no second timer or scheduler is needed.

### Old docs imply a second future queue owner

Retarget the two HPA-377 ownership statements as part of the implementation. Do not delete `UIScreenKinds.RewardToast`; it remains useful to host contract tests but is not the production treasure-toast path.

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
  full viewport under UI/GameUI
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

Do not add CanvasLayer, scrim, modal, button, AnimationPlayer, second timer, or host entry.

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

- [ ] Reuse the suite's `_viewport`, temporarily setting its size and adding a real `Game.tscn` instance to it. Do not use `InstantiateRealGameScene()` here because it mounts under the root viewport.

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

At 2560×1080, pin the content-width cap rather than only the ordinary safe margin:

```csharp
AssertThat(toast.Compact).IsFalse();
AssertThat(margin.GetThemeConstant("margin_top")).IsEqual(24);
AssertThat(margin.GetThemeConstant("margin_right")).IsEqual(480);
AssertThat(column.CustomMinimumSize.X).IsEqual(360f);
```

`480` is the expected `SideInset`: `(2560 - 1600) / 2`. This test must fail if implementation mistakenly uses the standard `24` px margin for `margin_right`.

Restore `_viewport.Size` in `finally` and free each real Game before the next case. Do not expand this into all seven approved viewport sizes.

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

## 3.3 Add direct resolved-result coverage for recovered and unrecovered overflow

- [ ] Use a **real** `Game.tscn`; do not fill the player's inventory to manufacture an overflow. Construct the already-resolved result directly:

```csharp
var game = await InstantiateRealGameScene();
try
{
    var manager = game.GetNode<GameManager>("GameManager");
    var player = manager.Player;
    var toast = game.GetNode<SiriusToastShell>(
        "UI/GameUI/RewardToastMargin/RewardToastColumn/RewardToast");
    var timer = game.GetNode<Timer>("RewardToastTimer");

    int goldBefore = player.Gold;
    int healthPotionBefore = player.GetItemQuantity("health_potion");
    int manaPotionBefore = player.GetItemQuantity("mana_potion");

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
    AssertThat(toast.Title).IsEqual("Recovery Chest");
    AssertThat(toast.Message).IsEqual(
        "Mana Potion ×1 sent to the Recovery Chest");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Warning);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Title).IsEqual("Inventory Full");
    AssertThat(toast.Message).IsEqual(
        "Health Potion ×4 could not be stored");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Error);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Title).IsEqual("Inventory Full");
    AssertThat(toast.Message).IsEqual(
        "Mana Potion ×3 could not be stored");
    AssertThat(toast.Severity).IsEqual(SiriusUiSeverity.Error);

    timer.EmitSignal(Timer.SignalName.Timeout);
    AssertThat(toast.Visible).IsFalse();

    AssertThat(player.Gold).IsEqual(goldBefore);
    AssertThat(player.GetItemQuantity("health_potion")).IsEqual(healthPotionBefore);
    AssertThat(player.GetItemQuantity("mana_potion")).IsEqual(manaPotionBefore);
}
finally
{
    game.Free();
    await AwaitFrames(1);
}
```

This test pins category ordering (`recovered` before `unrecovered`), ordinal ordering inside each dictionary, Warning/Error copy, and the no-grant presentation boundary without depending on `Inventory.MaxItemTypes` or `RecoveryChest.Instance` setup.

Expected before mapper implementation: FAIL because `EnqueueTreasureRewardFeedback` does not exist.

## 3.4 Capture the resolved result exactly once

- [ ] In `OnTreasureBoxOpenRequested(...)`, replace the discarded return value only:

```csharp
var grantResult = box.GrantRewardTo(_gameManager.Player);
_gameManager.MarkTreasureBoxOpened(box.TreasureBoxId);
_gridMap.ClearTreasureBoxCell(treasurePosition);
_gameManager.NotifyPlayerStatsChanged();
EnqueueTreasureRewardFeedback(grantResult);
```

Do not call `box.BuildReward()` again and do not move domain mutation into presentation methods.

## 3.5 Implement deterministic mapping

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

## 3.6 Preserve aborted-open silence

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

## 4.1 Add a characterization test on a ready scene instance

- [ ] Use the existing `CreateReadyBattleManager()` / `FreeManager(...)` helpers. Do **not** use `WithBattleManager(...)`; that helper constructs an unready `BattleManager` without the authored Result labels.

```csharp
[TestCase]
public async Task RenderResult_ResolvedRewardsAreReadableAndDoNotMutatePlayer()
{
    var battleManager = await CreateReadyBattleManager();
    try
    {
        var player = GetPrivateField<Character>(battleManager, "_player");
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

        InvokePrivateMethod(battleManager, "RenderResult", result);

        AssertThat(battleManager.GetNode<Label>("%ResultTitle").Text)
            .IsEqual("VICTORY");
        AssertThat(battleManager.GetNode<Label>("%ExperienceResult").Text)
            .IsEqual("Experience: 40");
        AssertThat(battleManager.GetNode<Label>("%GoldResult").Text)
            .IsEqual("Gold: 12");
        AssertThat(battleManager.GetNode<Label>("%LevelResult").Text)
            .IsEqual("Level: 1 → 2");
        AssertThat(battleManager.GetNode<Label>("%LootResultList").Text)
            .Contains("2x Health Potion");

        AssertThat(player.Gold).IsEqual(goldBefore);
        AssertThat(player.Experience).IsEqual(experienceBefore);
        AssertThat(player.GetItemQuantity("health_potion")).IsEqual(potionBefore);
    }
    finally
    {
        await FreeManager(battleManager);
    }
}
```

The snapshot happens after `CreateReadyBattleManager()` has called `StartBattle(...)`; only `RenderResult(...)` is under the mutation assertion.

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~BattleManagerTest.RenderResult_ResolvedRewardsAreReadableAndDoNotMutatePlayer"
```

Expected: PASS against current `BattleManager.RenderResult`. Keep production Battle untouched; this task is characterization, not a reason to redesign Result copy or add another surface.

- [ ] Commit the test only:

```bash
git add tests/ui/BattleManagerTest.cs
git commit -m "test: pin battle reward result presentation"
```

---

# Task 5: Reconcile ownership documentation and validate the complete slice

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify: `docs/ui/hpa-377/README.md`
- Modify: `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`
- Verify all Task 1-4 files
- Reference: `scripts/ui/hosting/UIScreenKinds.cs`
- Reference: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`

## 5.1 Reconcile the three HPA-376 treasure/reward lifecycle rows

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

## 5.2 Retarget stale HPA-377 queue ownership

- [ ] In `docs/ui/hpa-377/README.md`, replace the HPA-386 handoff with the current ownership:

```text
HPA-573 owns production treasure reward queueing and timeout at the Game root.
SiriusToastShell remains the presentation-only visual shell; it does not queue,
time, dismiss, grant, or persist notifications. Battle result acknowledgement
remains Battle-owned through HPA-356 rather than using the treasure-toast path.
```

- [ ] In `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md`, retarget each stale HPA-386 toast/reward ownership reference:

  - header `Toast/reward queue handoff` → HPA-573;
  - demand-ledger `Toast visual shell` consumer/lifetime owner → HPA-573;
  - `SiriusToastShell` section queue/lifetime handoff → HPA-573.

Preserve the original HPA-377 architectural rule: the shell itself owns visual presentation only and no Timer/Tween/queue/lifecycle behavior.

- [ ] Verify `UIScreenKinds.RewardToast` still exists for host tests and is **not** used by production Game reward feedback. Do not delete or rename it.

Run:

```bash
rg -n "HPA-386|RewardToast" \
  docs/ui/hpa-377/README.md \
  docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md \
  scripts/ui/hosting/UIScreenKinds.cs \
  tests/ui/hosting
```

Expected: no stale HPA-386 production ownership remains in the two HPA-377 docs; host-test `RewardToast` references remain intact.

## 5.3 Run focused HPA-573 suites

- [ ] Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusToastShellTest|FullyQualifiedName~GameTest|FullyQualifiedName~BattleManagerTest"
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

Expected: tests/build pass, diff check is clean, and no unintended files appear.

- [ ] Review the final diff and verify there is **no** reward service/manager, host kind, persistence field, save-schema change, second battle result surface, or domain mutation from presentation.

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

- [ ] Existing treasure still grants exactly once.
- [ ] One gold + one item invocation shows two sequential toasts.
- [ ] Advancing/clearing presentation does not change gold, inventory, XP, or level.
- [ ] Re-interacting with an opened box neither grants nor enqueues again.
- [ ] Multiple granted items use explicit ordinal item-ID ordering.
- [ ] Recovery Chest and unrecovered overflow use Warning/Error copy in deterministic ordinal order without mutating player state.
- [ ] Aborted treasure opening grants nothing and queues nothing.
- [ ] Toasts never register with `UIScreenHost`, pause the tree, change cursor/focus, or intercept mouse input.
- [ ] 640×360 uses compact typography, 12 px inset, 280 px width.
- [ ] 1280×720 uses standard typography, 24 px inset, 360 px width.
- [ ] 2560×1080 uses standard typography, a 480 px right side inset from the 1600 px content cap, and 360 px width.
- [ ] Any scene-change request—including Return to Title through the same method—clears active + queued feedback before host teardown.
- [ ] Raw root exit clears active + queued feedback.
- [ ] Battle Result characterization uses `CreateReadyBattleManager()` and current `RenderResult(...)`; production Battle remains unchanged.
- [ ] Battle Result still renders XP/gold/level/loot and rendering itself mutates nothing.
- [ ] HPA-376 reward rows describe final HPA-573/HPA-356 ownership.
- [ ] HPA-377 docs identify HPA-573 as the production toast-queue owner while keeping `SiriusToastShell` presentation-only.
- [ ] `UIScreenKinds.RewardToast` remains a host-test fixture and is not the runtime treasure path.
- [ ] Full tests and build pass.
