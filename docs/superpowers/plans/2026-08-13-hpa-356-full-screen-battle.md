# HPA-356 Full-Screen Battle Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the desktop `AcceptDialog` battle with one responsive, full-screen, host-managed Preparation → Automatic Combat → Results flow while preserving combat, item, reward, escape, feedback, and defeat behavior.

**Architecture:** Keep `BattleManager` as the one combat/rule owner. Make the `AcceptDialog` → `Control` change and `Game` → `UIScreenHost` registration in one atomic task so every intermediate commit builds. Then migrate presentation onto the existing SafeFrame/stat/slot system; add only `BattleResultSummary` as already-resolved Results data.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and gameplay `UIScreenHost`.

## Global Constraints

- Preserve the 1.5-second timer, AP scheduling, speed tie behavior, auto attack/defend, skills, mana, cooldowns, status effects, damage rules, and existing damage/attack feedback.
- Preserve pre-battle remove → apply → rollback ordering and mid-battle cure-only behavior.
- Preserve victory XP/gold/level/skills/loot, enemy removal, victory autosave, and defeat-return behavior.
- Preserve `BattleFinished(bool playerWon, bool playerEscaped)` and exactly-once emission.
- Preparation Cancel and Automatic Combat Cancel/Escape remain immediate escape; no escape Results screen.
- Victory/defeat emit once while hosted Results remains until dismissal or defeat teardown.
- Hosted Battle blocks gameplay, hides HUD, shows cursor, and does not pause the tree.
- Cure overlay consumes Cancel before Battle escape.
- Compact state is private and derived from `SiriusUiMetrics.SafeFrameInsets`; no public `SetCompact`.
- Standard/compact safe margins remain 24/12 px; maximum content width remains 1600 px.
- Minimum targets remain 44/40 px; essential compact text remains at least 14 px.
- Preparation page size is 4 standard / 3 compact. Event feed is 5 standard / 3 compact and re-trims on resize.
- Progress reads the existing timer only and never drives combat.
- Reuse `SiriusStatBar`, `SiriusItemSlotController`, `UIScreenKinds.Battle`, Theme, art, metrics, and current host APIs.
- No battle session/service/state-machine, event bus, generic item picker, new Theme token, new metric, new host API/kind, reward protocol, manual combat, battle speed, general combat pause, or skill editing.
- Do not change combat-domain/save/inventory-domain production files unless a failing parity test proves a concrete necessity.

---

## File Map

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

### Audit-only

- `scripts/ui/hosting/UIScreenKinds.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`

---

## Task 1: Characterize current lifecycle and add the resolved result value

**Files:**
- Create: `scripts/data/BattleResultSummary.cs`
- Modify: `tests/ui/BattleManagerTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

**Produces:**

```csharp
public sealed record BattleResultSummary(
    bool PlayerWon,
    int ExperienceGained,
    int GoldGained,
    int PreviousLevel,
    int NewLevel,
    LootResult Loot);
```

- [ ] **Step 1: Keep existing escape characterization unchanged**

Require these current tests to remain green before any production edit:

```text
ForceCloseDuringPreparation_EmitsOnceAndClosesImmediately
ForceCloseDuringAutomaticCombat_StopsTimerClearsEffectsEmitsOnceAndClosesImmediately
ConfiguredKeyboardCancel_BattleResultClosesNativeDialogWithoutOpeningHostedPause
```

- [ ] **Step 2: Add current result-linger characterization**

Add to `BattleManagerTest.cs`:

```csharp
[TestCase]
public async Task BattleFinished_VictoryLeavesResultVisibleUntilExplicitDismissal()
{
    var manager = await CreateReadyBattleManager();
    int count = 0;
    manager.BattleFinished += (_, escaped) =>
    {
        if (!escaped) count++;
    };

    try
    {
        var method = typeof(BattleManager).GetMethod(
            "EndBattle",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("EndBattle not found.");

        method.Invoke(manager, new object[] { true });

        AssertThat(count).IsEqual(1);
        AssertThat(manager.Visible).IsTrue();
        AssertThat(manager.GetOkButton().Visible).IsTrue();
    }
    finally
    {
        await FreeManager(manager);
    }
}
```

Add `using System.Reflection;`.

- [ ] **Step 3: Run current lifecycle characterization**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_BattleResult"
```

Expected: PASS.

- [ ] **Step 4: Add RED `BattleResultSummary` tests**

```csharp
[TestCase]
public void BattleResultSummary_VictoryStoresResolvedRewards()
{
    var loot = new LootResult();
    loot.Add(ConsumableCatalog.CreateHealthPotion(), 2);
    var result = new BattleResultSummary(true, 25, 10, 1, 2, loot);

    AssertThat(result.ExperienceGained).IsEqual(25);
    AssertThat(result.GoldGained).IsEqual(10);
    AssertThat(result.PreviousLevel).IsEqual(1);
    AssertThat(result.NewLevel).IsEqual(2);
    AssertThat(result.Loot.DroppedItems.Count).IsEqual(1);
}

[TestCase]
public void BattleResultSummary_DefeatStoresZeroRewards()
{
    var result = new BattleResultSummary(false, 0, 0, 3, 3, LootResult.Empty);
    AssertThat(result.PlayerWon).IsFalse();
    AssertThat(result.ExperienceGained).IsEqual(0);
    AssertThat(result.GoldGained).IsEqual(0);
    AssertThat(result.Loot.HasDrops).IsFalse();
}
```

- [ ] **Step 5: Run RED, add the six-field record, then run GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest.BattleResultSummary"
```

Expected before implementation: compile failure because `BattleResultSummary` is missing.

Create `scripts/data/BattleResultSummary.cs` with exactly the record above; add no methods/IDs/persistence/ack state.

Then:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest"
```

Expected: PASS.

- [ ] **Step 6: Commit Task 1**

```bash
git add scripts/data/BattleResultSummary.cs tests/ui/BattleManagerTest.cs tests/game/GameInputLifecycleTest.cs
git commit -m "test(battle): freeze lifecycle before full-screen cutover"
```

---

## Task 2: Atomically cut Battle from native dialog to hosted `Control`

**Files:**
- Modify: `scenes/ui/BattleScene.tscn`
- Modify: `scripts/ui/BattleManager.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/BattleManagerTest.cs`
- Modify: `tests/game/GameTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`

**Produces:**

```csharp
public partial class BattleManager : Control
{
    [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, bool playerEscaped);
    [Signal] public delegate void DismissRequestedEventHandler();

    public Control? InitialFocusTarget { get; }
    public BattleResultSummary? ResolvedResult { get; }

    public void StartBattle(Character player, Enemy enemy);
    public void RequestCancel();
}
```

### 2A — host RED tests first

- [ ] **Step 1: Add RED host/lifecycle tests**

Add:

```text
Battle_HostsAsBlockingScreenWithoutPausingTree
BattleVictory_RemainsHostedAfterBattleFinishedUntilDismissal
BattleResultCancel_ClosesBattleWithoutOpeningPauseOrReemittingResult
ConfiguredCancel_DuringHostedBattleEscapesWithoutOpeningPause
```

Assertions: one `UIScreenKinds.Battle` entry, host parentage, no tree pause, gameplay blocked, HUD hidden, cursor visible, victory clears `GameManager.IsInBattle` while Battle stays active, and Cancel never opens Pause.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest.Battle|FullyQualifiedName~GameInputLifecycleTest.Battle|FullyQualifiedName~GameInputLifecycleTest.ConfiguredCancel_DuringHostedBattle"
```

Expected: FAIL because Battle is still native/direct-parented.

### 2B — root type and Game call sites change together

- [ ] **Step 3: Change only the root/window contract first**

Change `BattleScene.tscn` root to full-rect `Control`, preserving current `BattleContent/BattleArena/...` paths for this task.

Add scene-authored `ContinueButton` (hidden initially) and `EscapeButton` (Preparation hidden, Automatic Combat visible). Do not perform final SafeFrame re-layout yet.

- [ ] **Step 4: Replace native Battle API and define every helper Task 2 needs**

Change `BattleManager : AcceptDialog` → `BattleManager : Control` and delete use of:

```text
Title
GetOkButton
CloseRequested
Canceled
PopupCentered
PopupWindow
Exclusive
ForceCloseAsEscape
```

Add:

```csharp
private enum BattlePhase { Preparation, AutomaticCombat, Result }
private BattlePhase _phase = BattlePhase.Preparation;
private bool _dismissRequested;

public BattleResultSummary? ResolvedResult { get; private set; }

public Control? InitialFocusTarget => _phase switch
{
    BattlePhase.Preparation => _startButton,
    BattlePhase.AutomaticCombat => _itemPanel != null && _itemPanel.Visible
        ? FindFirstLegacyItemPanelFocusTarget()
        : _escapeButton,
    BattlePhase.Result => _continueButton,
    _ => null
};

private Control FindFirstLegacyItemPanelFocusTarget()
{
    if (_itemPanel != null && IsInstanceValid(_itemPanel))
    {
        foreach (var child in _itemPanel.GetChildren())
        {
            if (child is Button button && button.Visible && !button.Disabled)
                return button;
        }
    }
    return _escapeButton;
}

private void RequestDismiss()
{
    if (_dismissRequested) return;
    _dismissRequested = true;
    EmitSignal(SignalName.DismissRequested);
}

private void EmitBattleFinishedOnce(bool playerWon, bool playerEscaped)
{
    if (_resultEmitted) return;
    _resultEmitted = true;
    EmitSignal(SignalName.BattleFinished, playerWon, playerEscaped);
}

private void StopBattleRuntime()
{
    if (_battleTimer != null && IsInstanceValid(_battleTimer))
        _battleTimer.Stop();

    _player?.ActiveBuffs.Clear();
    _enemy?.ActiveStatusEffects.Clear();
}
```

Task 3 extends `StopBattleRuntime()` with visual-tween cleanup; it already exists here because Task 2 calls it.

- [ ] **Step 5: Make Cancel child-first immediately**

```csharp
public void RequestCancel()
{
    if (_phase == BattlePhase.Result)
    {
        RequestDismiss();
        return;
    }

    if (_phase == BattlePhase.AutomaticCombat &&
        _itemPanel != null && IsInstanceValid(_itemPanel) && _itemPanel.Visible)
    {
        _itemPanel.Visible = false;
        if (_player.IsAlive && _enemy.IsAlive)
            _battleTimer.Start();
        _escapeButton.GrabFocus();
        return;
    }

    StopBattleRuntime();
    EmitBattleFinishedOnce(false, true);
    RequestDismiss();
}
```

This temporary internal `_itemPanel` check disappears in Task 3 when `%CureOverlay` replaces the runtime panel.

- [ ] **Step 6: Replace native Continue**

At victory/defeat tail:

```csharp
_phase = BattlePhase.Result;
_continueButton.Visible = true;
_continueButton.Disabled = false;
_continueButton.GrabFocus();
EmitBattleFinishedOnce(playerWon, false);
```

Connect `_continueButton.Pressed += RequestDismiss` and `_escapeButton.Pressed += RequestCancel`.

- [ ] **Step 7: Add one Game handle and host cleanup**

```csharp
private UIScreenHandle? _battleHandle;

private void ClearBattlePresentation(BattleManager battle)
{
    battle.BattleFinished -= OnBattleFinished;
    battle.DismissRequested -= OnBattleDismissRequested;
    if (ReferenceEquals(_battleManager, battle))
        _battleManager = null;
    _battleHandle = null;
}

private void OnBattleDismissRequested()
{
    if (_screenHost == null || !_battleHandle.HasValue)
        return;
    _screenHost.TryClose(_battleHandle.Value, UIScreenCloseReason.ExplicitAction);
}
```

- [ ] **Step 8: Present Battle through `UIScreenHost` in the same edit**

```csharp
var result = _screenHost.TryPresent(battle, new UIScreenEntrySpec
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
});
```

On success set `_battleManager`, `_battleHandle`, then call `StartBattle`. On failure disconnect/free the unhosted node, run the existing battle-state safety reset, and refresh interaction prompt. No direct-parent fallback.

- [ ] **Step 9: Delete stale Game native call sites in this same task**

Delete direct `UI.AddChild`, `Confirmed`, `OnBattleDialogConfirmed`, popup flags, `PopupCentered`, `ForceCloseAsEscape`, Battle-specific root-Cancel branches, and duplicate manual QueueFree ownership. Leave unrelated error `AcceptDialog` code intact.

Preserve `OnBattleFinished` world/domain consequences and do not close Battle on ordinary victory/defeat.

- [ ] **Step 10: Migrate Task 1 tests to the new semantics**

Rename/rewrite to:

```text
RequestCancelDuringPreparation_EmitsEscapeAndDismissOnce
RequestCancelDuringAutomaticCombat_StopsTimerClearsEffectsAndDismissesOnce
RequestCancelDuringResult_DismissesWithoutSecondBattleFinished
Victory_EmitsOnceAndLeavesControlVisibleUntilDismissRequested
```

Rewrite the native result-Cancel lifecycle test to the hosted equivalent.

- [ ] **Step 11: Run Task 2 GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS and 0 build errors. This is the explicit intermediate-build gate.

- [ ] **Step 12: Commit Task 2**

```bash
git add scenes/ui/BattleScene.tscn scripts/ui/BattleManager.cs scripts/game/Game.cs \
  tests/ui/BattleManagerTest.cs tests/game/GameTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "refactor(ui): host battle as full-screen control"
```

---

## Task 3: Migrate final Battle presentation and feedback

**Files:**
- Modify: `scenes/ui/BattleScene.tscn`
- Modify: `scripts/ui/BattleManager.cs`
- Modify: `tests/ui/BattleManagerTest.cs`
- Create: `tests/ui/BattleSceneTest.cs`
- Audit: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`

### 3A — final scene and SafeFrame

- [ ] **Step 1: Add RED scene tests**

Create `BattleSceneTest` using a real `SubViewport` and assert the final scene authors:

```text
SafeFrame
PreparationPanel / AutomaticCombatPanel / ResultPanel / CureOverlay
PlayerSpriteContainer / EnemySpriteContainer
PlayerDamageLabel / EnemyDamageLabel
BeginBattleButton / CureButton / EscapeButton / ContinueButton
```

Also assert `AttackButton`, `DefendButton`, `RunButton`, and `BattleSpeed` are absent.

- [ ] **Step 2: Author the final scene**

Use the stable names from the design, move `ui_battle_background.png` into the scene, preserve player/enemy sprite containers and damage labels, and use existing Theme variations only.

- [ ] **Step 3: Implement Inventory-style private `RefreshLayout()`**

```csharp
private bool _isCompact;

private void RefreshLayout()
{
    if (!GodotObject.IsInstanceValid(this) || _safeFrame == null || !IsInsideTree())
        return;

    var insets = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
    _isCompact = insets.Compact;

    _safeFrame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    _safeFrame.OffsetLeft = insets.SideInset;
    _safeFrame.OffsetTop = insets.Margin;
    _safeFrame.OffsetRight = -insets.SideInset;
    _safeFrame.OffsetBottom = -insets.Margin;

    _playerHealth.Compact = _isCompact;
    _playerMana.Compact = _isCompact;
    _enemyHealth.Compact = _isCompact;

    foreach (var slot in _preparationSlots) slot.SetCompact(_isCompact);
    foreach (var slot in _cureSlots) slot.SetCompact(_isCompact);

    bool showPrepTelemetry = !_isCompact && _phase == BattlePhase.Preparation;
    _playerAttack.Visible = showPrepTelemetry;
    _playerDefense.Visible = showPrepTelemetry;
    _playerSpeed.Visible = showPrepTelemetry;
    _enemyAttack.Visible = showPrepTelemetry;
    _enemyDefense.Visible = showPrepTelemetry;
    _enemySpeed.Visible = showPrepTelemetry;

    if (_player != null)
        RefreshPreparationItems();
    RefreshEventFeed();
}
```

Connect viewport `SizeChanged`; no public compact API.

### 3B — preparation slots and error surface

- [ ] **Step 4: Replace runtime preparation buttons with dynamic `SiriusItemSlotController` nodes**

Use refresh-scoped slot → consumable mapping, current inventory order, `entry.Item.LoadAssetOrDefault<Texture2D>()`, grow/reuse/shrink, and page size `_isCompact ? 3 : 4`.

- [ ] **Step 5: Replace `ShowItemPanelError` with authored preparation feedback**

```csharp
private void ShowPreparationError(string message)
{
    _preparationItemDetails.Text = message;
    _preparationItemDetails.Visible = true;
    _phase = BattlePhase.Preparation;
    _preparationPanel.Visible = true;
    _automaticCombatPanel.Visible = false;
    _resultPanel.Visible = false;
}
```

Every pre-battle remove/apply/rollback failure uses this helper and returns before timer start.

Add a test asserting no result emission, timer stopped, Preparation visible, Automatic Combat hidden, and readable error text.

### 3C — preserve damage feedback and teardown tweens

- [ ] **Step 6: Keep authored damage labels and sprite positioning wired to existing methods**

`ShowDamageNumber` uses `%PlayerDamageLabel` / `%EnemyDamageLabel`; position helpers use `%PlayerSpriteContainer` / `%EnemySpriteContainer`.

- [ ] **Step 7: Track visual tweens**

```csharp
private readonly HashSet<Tween> _visualTweens = new();

private Tween CreateTrackedTween()
{
    var tween = CreateTween();
    _visualTweens.Add(tween);
    tween.Finished += () => _visualTweens.Remove(tween);
    return tween;
}

private void KillVisualTweens()
{
    foreach (var tween in _visualTweens.ToArray())
        tween.Kill();
    _visualTweens.Clear();
}
```

Replace `CreateTween()` in `ShowDamageNumber` and `PlayAttackAnimation`. Add `System.Linq` for `ToArray()`.

Extend the Task 2 helper:

```csharp
private void StopBattleRuntime()
{
    if (_battleTimer != null && IsInstanceValid(_battleTimer))
        _battleTimer.Stop();
    _player?.ActiveBuffs.Clear();
    _enemy?.ActiveStatusEffects.Clear();
    KillVisualTweens();
}
```

`_ExitTree()` calls it and never emits a result. Add a test proving tracked tweens are empty after stop/teardown.

### 3D — feed, progress, cure overlay

- [ ] **Step 8: Add bounded feed that trims on every refresh**

```csharp
private readonly Queue<string> _combatEvents = new();
private int EventFeedLimit => _isCompact ? 3 : 5;

private void AppendCombatEvent(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return;
    _combatEvents.Enqueue(text);
    RefreshEventFeed();
}

private void RefreshEventFeed()
{
    while (_combatEvents.Count > EventFeedLimit)
        _combatEvents.Dequeue();
    _eventFeed.Text = string.Join("\n", _combatEvents);
}
```

Add a 5→3 resize test.

- [ ] **Step 9: Add timer-derived progress only**

In `_Process`, while Automatic Combat and timer running:

```csharp
_automaticActionProgress.Value =
    1.0 - (_battleTimer.TimeLeft / _battleTimer.WaitTime);
```

Otherwise set 0. Do not execute combat from `_Process`.

- [ ] **Step 10: Replace runtime combat item panel with `%CureOverlay`**

Use dynamic `SiriusItemSlotController` nodes. Only `CureStatusEffect` is actionable; unsupported items remain focusable with `BATTLE START ONLY` reason.

Opening stops timer. Closing restarts timer only when phase is Automatic Combat and both actors are alive.

- [ ] **Step 11: Finalize child-first `RequestCancel()`**

```csharp
public void RequestCancel()
{
    if (_cureOverlay.Visible)
    {
        CloseCureOverlay();
        return;
    }

    if (_phase == BattlePhase.Result)
    {
        RequestDismiss();
        return;
    }

    StopBattleRuntime();
    EmitBattleFinishedOnce(false, true);
    RequestDismiss();
}
```

Test: Cancel with Cure open closes Cure, emits/dismisses nothing, and resumes timer.

### 3E — structured Results and responsive validation

- [ ] **Step 12: Build `BattleResultSummary` after grant**

Keep existing grant ordering. Victory creates summary after XP/gold/skills/loot award. Defeat creates zero-reward summary. Render scene-authored Result rows, then emit once. Dismissal never grants.

- [ ] **Step 13: Deep-check 1280×720 and 640×360**

Assert exact SafeFrame offsets from `SafeFrameInsets`, actor/action containment, minimum target sizes, preparation page 4/3, compact Result Continue reachable, and damage labels inside actor regions at rest. Other shared viewports get light containment only.

- [ ] **Step 14: Run Task 3 GREEN and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS and 0 build errors.

- [ ] **Step 15: Commit Task 3**

```bash
git add scenes/ui/BattleScene.tscn scripts/ui/BattleManager.cs tests/ui/BattleManagerTest.cs \
  tests/ui/BattleSceneTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): finish responsive battle presentation"
```

---

## Task 4: Reconcile lifecycle docs and verify exact scope

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: Task 1–3 files

- [ ] **Step 1: Update only HPA-376 Battle rows**

Update `BATTLE-PREP`, `BATTLE-AUTO`, both escape rows, victory/defeat/escape result rows, and `BATTLE-CLEANUP`. Do not rewrite NPC/shop/error/save rows.

- [ ] **Step 2: Run focused, then full tests/build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
```

Expected: 0 failed tests, 0 build errors.

- [ ] **Step 3: Use Battle-scoped stale-popup search**

```bash
rg -n \
  'BattleManager\s*:\s*AcceptDialog|GetOkButton\(|ForceCloseAsEscape|OnBattleDialogConfirmed|_battleManager\.Confirmed|_battleManager\.PopupCentered|_battleManager\.PopupWindow|_battleManager\.Exclusive' \
  scripts/ui/BattleManager.cs scripts/game/Game.cs tests/ui/BattleManagerTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs
```

Expected: zero matches. Do not require zero generic `PopupCentered()`/`Confirmed +=` in all of `Game.cs`; unrelated error dialogs are out of scope.

- [ ] **Step 4: Verify no unsupported Battle UI or speculative architecture**

```bash
rg -n 'AttackButton|DefendButton|RunButton|BattleSpeed|PauseBattle|EditSkill' \
  scenes/ui/BattleScene.tscn scripts/ui/BattleManager.cs

find scripts -type f \( -iname '*BattleSession*' -o -iname '*BattlePresenter*' \
  -o -iname '*CombatEvent*' -o -iname '*BattleStateMachine*' \) -print
```

Expected: no production UI matches and no new speculative files.

- [ ] **Step 5: Diff hygiene and scope audit**

```bash
git diff --check main...HEAD
git diff --name-status main...HEAD
```

Expected production changes: `BattleResultSummary.cs`, `BattleScene.tscn`, `BattleManager.cs`, `Game.cs`, Battle-focused tests, and HPA-376 Battle lifecycle documentation. Theme, metrics, `UIScreenKinds`, combat-domain, save, and Inventory-domain files stay unchanged absent concrete failing-test evidence.

- [ ] **Step 6: Whole-branch review**

Review specifically for:

```text
intermediate build break from stale AcceptDialog call sites
reward/result double application
BattleFinished double emission
victory/defeat auto-dismiss
escape entering Results
Cure Cancel escaping instead of closing child
prep rollback error disappearing/starting combat
timer or visual tween surviving teardown
Cancel falling through to Pause
HUD/gameplay restoring before Results dismissal
feed staying at 5 after compact resize
SafeFrame margins not applied
damage labels/sprite positioning disappearing
unrelated popup code changed by stale grep
manual combat/battle speed/skill editing returning
session/event/theme/metric expansion
```

- [ ] **Step 7: Commit docs reconciliation**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: reconcile hosted battle lifecycle"
```

---

## Final Acceptance Checklist

- [ ] Every implementation task ends buildable; root-type and Game host migration are atomic.
- [ ] Battle is one full-screen host-managed `Control` with one `BattleManager`.
- [ ] Timer/AP/skills/status/damage and current damage/attack feedback are preserved.
- [ ] Preparation rollback errors remain visible and do not start combat.
- [ ] Cure Cancel closes Cure before escape.
- [ ] Victory grants once and exposes one read-only `BattleResultSummary`.
- [ ] Defeat keeps delayed title return; escape remains immediate with no Results.
- [ ] `BattleFinished` semantics remain exactly-once.
- [ ] Victory/defeat Results remains hosted after domain battle state clears.
- [ ] Battle blocks gameplay, hides HUD, shows cursor, and does not pause tree.
- [ ] `RefreshLayout()` uses `SafeFrameInsets`; no public compact API.
- [ ] Feed re-trims 5→3 on compact resize.
- [ ] Damage labels/sprite containers remain authored and visual tweens die on teardown.
- [ ] Battle-scoped stale-popup grep passes without touching unrelated error dialogs.
- [ ] No new Theme/metric/host/session/event/picker/reward architecture.
- [ ] Focused tests, full suite, build, diff hygiene, and scope audit pass.
