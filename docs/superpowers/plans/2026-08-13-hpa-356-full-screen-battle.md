# HPA-356 Full-Screen Battle Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the desktop `AcceptDialog` battle with one responsive, full-screen, host-managed Preparation → Automatic Combat → Results flow while preserving combat, item, reward, escape, feedback, and defeat behavior.

**Architecture:** Keep one `BattleManager`. Cut `AcceptDialog` → `Control` and `Game` → `UIScreenHost` atomically. Then land final scene/SafeFrame/preparation separately from feedback/Cure/Results. Add only `BattleResultSummary` as non-granting resolved result data.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components and gameplay `UIScreenHost`.

## Global Constraints

- Preserve timer/AP/skills/status/damage and current damage/attack feedback.
- Preserve pre-battle remove → apply → rollback and cure-only mid-battle use.
- Preserve XP/gold/level/skills/loot, enemy removal, victory autosave, and defeat return.
- Preserve `BattleFinished(bool,bool)` exactly once.
- Escape stays immediate and never enters Results.
- Hosted Results stays active after `GameManager.IsInBattle` clears; gameplay remains suppressed through host `BlockGameplayInput`.
- Cure consumes Cancel before escape.
- Use private `RefreshLayout()` + `SiriusUiMetrics.SafeFrameInsets`; no public compact API.
- Reuse `SiriusStatBar`, `SiriusItemSlotController`, `UIScreenKinds.Battle`, Theme/art/metrics/host APIs.
- Do not extend `SiriusPlayerSummaryPresenter`; Battle has no EXP bar required by its current signature.
- `BattleResultSummary` is not deeply immutable because it contains mutable `LootResult`.
- No session/service/state machine/event bus/generic picker/new Theme token/new metric/new host kind/API/reward protocol/manual combat/battle speed/general combat pause/skill editing.

## File Map

Create:
- `scripts/data/BattleResultSummary.cs`
- `tests/ui/BattleSceneTest.cs`

Modify:
- `scenes/ui/BattleScene.tscn`
- `scripts/ui/BattleManager.cs`
- `scripts/game/Game.cs`
- `tests/ui/BattleManagerTest.cs`
- `tests/game/GameTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Audit only:
- `scripts/ui/hosting/UIScreenKinds.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/SiriusPlayerSummaryPresenter.cs`
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`

---

## Task 1: Freeze current terminal behavior and add `BattleResultSummary`

- [ ] Run existing durable characterizations before any production edit:

```text
ForceCloseDuringPreparation_EmitsOnceAndClosesImmediately
ForceCloseDuringAutomaticCombat_StopsTimerClearsEffectsEmitsOnceAndClosesImmediately
ConfiguredKeyboardCancel_BattleResultClosesNativeDialogWithoutOpeningHostedPause
```

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest.ForceCloseDuringPreparation|FullyQualifiedName~BattleManagerTest.ForceCloseDuringAutomaticCombat|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_BattleResult"
```

- [ ] Create exactly:

```csharp
public sealed record BattleResultSummary(
    bool PlayerWon,
    int ExperienceGained,
    int GoldGained,
    int PreviousLevel,
    int NewLevel,
    LootResult Loot);
```

Do not add trivial record-accessor tests. Do not add a native `GetOkButton().Visible` test that disappears in Task 2. Real summary coverage belongs to Task 4 after grants occur.

- [ ] Build + rerun Battle characterization.

```bash
dotnet build Sirius.sln --no-restore --nologo
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~GameInputLifecycleTest.ConfiguredKeyboardCancel_BattleResult"
```

- [ ] Commit:

```bash
git add scripts/data/BattleResultSummary.cs
git commit -m "feat(battle): add resolved result record"
```

---

## Task 2: Atomically cut native Battle to hosted `Control`

### Tests first

- [ ] Add RED tests:

```text
Battle_HostsAsBlockingScreenWithoutPausingTree
BattleVictory_RemainsHostedAfterBattleFinishedUntilDismissal
BattleResultCancel_ClosesBattleWithoutOpeningPauseOrReemittingResult
ConfiguredCancel_DuringHostedBattleEscapesWithoutOpeningPause
```

The victory test must assert:

```text
GameManager.IsInBattle == false
UIScreenKinds.Battle still active
host IsPresentationGameplayBlocked == true
Game._presentationGameplayBlocked == true
Game.IsGameplayInputSuppressed() == true
```

Use test reflection helpers; do not expose new production APIs.

### Atomic production cutover

- [ ] Change scene root to full-rect `Control`, preserving current `BattleContent/BattleArena/...` paths for this task. Add scene-authored `ContinueButton` and `EscapeButton`.

- [ ] Change `BattleManager : AcceptDialog` → `BattleManager : Control` and delete:

```text
Title
GetOkButton()
CloseRequested
Canceled
PopupCentered()
PopupWindow
Exclusive
ForceCloseAsEscape()
OnCloseRequested()
```

- [ ] Add exact phase/focus/terminal helpers:

```csharp
private enum BattlePhase { Preparation, AutomaticCombat, Result }
private BattlePhase _phase = BattlePhase.Preparation;
private bool _dismissRequested;

public BattleResultSummary? ResolvedResult { get; private set; }

public Control? InitialFocusTarget => _phase switch
{
    BattlePhase.Preparation => _startButton,
    BattlePhase.AutomaticCombat =>
        _itemPanel != null && IsInstanceValid(_itemPanel) && _itemPanel.Visible
            ? FindFirstLegacyItemPanelFocusTarget()
            : _escapeButton,
    BattlePhase.Result => _continueButton,
    _ => null
};
```

`FindFirstLegacyItemPanelFocusTarget()` returns the first visible enabled legacy panel button, otherwise `_escapeButton`.

Use one exactly-once helper and one runtime-stop helper:

```csharp
private void EmitBattleFinishedOnce(bool won, bool escaped)
{
    if (_resultEmitted) return;
    _resultEmitted = true;
    EmitSignal(SignalName.BattleFinished, won, escaped);
}

private void StopBattleRuntime()
{
    if (_battleTimer != null && IsInstanceValid(_battleTimer))
        _battleTimer.Stop();
    _player?.ActiveBuffs.Clear();
    _enemy?.ActiveStatusEffects.Clear();
}
```

- [ ] Make invalid `StartBattle` host-safe:

```csharp
EmitBattleFinishedOnce(false, true);
RequestDismiss();
return;
```

No `Hide()` / `QueueFree()`.

- [ ] Keep `EndBattleWithEscape()` and remove only its native UI:

```csharp
private void EndBattleWithEscape()
{
    StopBattleRuntime();
    EmitBattleFinishedOnce(false, true);
}
```

Delete its `GetOkButton()` writes/direct `_resultEmitted` assignment. `RequestCancel()` calls this method; it does not duplicate escape cleanup.

- [ ] Temporary child-first Cancel while legacy combat `_itemPanel` still exists:

```csharp
public void RequestCancel()
{
    if (_phase == BattlePhase.AutomaticCombat &&
        _itemPanel != null && IsInstanceValid(_itemPanel) && _itemPanel.Visible)
    {
        _itemPanel.Visible = false;
        if (_player.IsAlive && _enemy.IsAlive) _battleTimer.Start();
        _escapeButton.GrabFocus();
        return;
    }

    if (_phase == BattlePhase.Result)
    {
        RequestDismiss();
        return;
    }

    EndBattleWithEscape();
    RequestDismiss();
}
```

- [ ] Replace native Continue tail in `EndBattle`:

```csharp
_phase = BattlePhase.Result;
_continueButton.Visible = true;
_continueButton.GrabFocus();
EmitBattleFinishedOnce(playerWon, false);
```

- [ ] Add `_battleHandle`, `DismissRequested` cleanup, and present unparented Battle through existing `UIScreenHost`:

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

- [ ] In the same edit delete Game's direct `UI.AddChild`, `Confirmed`, `OnBattleDialogConfirmed`, popup flags, `PopupCentered`, `ForceCloseAsEscape` branches, and duplicate manual free ownership. Leave unrelated error dialogs intact.

- [ ] Migrate the three durable native tests to `RequestCancel` / hosted Result semantics. The hosted victory test owns Result-linger coverage.

- [ ] Gate + commit:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore --nologo

git add scenes/ui/BattleScene.tscn scripts/ui/BattleManager.cs scripts/game/Game.cs \
  tests/ui/BattleManagerTest.cs tests/game/GameTest.cs \
  tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs
git commit -m "refactor(ui): host battle as full-screen control"
```

---

## Task 3: Final scene + SafeFrame + preparation

- [ ] Add RED `BattleSceneTest` for final unique names, including `%SafeFrame`, actor anchors/sprite containers/damage labels, three phase panels, Cure overlay, and required actions. Assert no authored Attack/Defend/Run/BattleSpeed.

- [ ] Rewrite scene to final names and **in the same edit** migrate every old `BattleContent/BattleArena/...` node binding/position path in `BattleManager`. Move battle background into scene; delete runtime `AddBattleBackground()`.

Gate node-path migration:

```bash
rg -n 'BattleContent/BattleArena' scripts/ui/BattleManager.cs
```

Expected: zero matches.

- [ ] Implement private Inventory-style `RefreshLayout()` with exact `SafeFrameInsets` offsets, `_isCompact`, compact stat bars, compact prep slots, and nonessential telemetry hiding. No public setter.

- [ ] Bind Battle name/level/HP/MP directly. Reuse `SiriusStatBar`; do not alter `SiriusPlayerSummaryPresenter` or add fake EXP UI.

- [ ] Replace runtime preparation buttons with grow/reuse/shrink `SiriusItemSlotController` nodes using current inventory order and `entry.Item.LoadAssetOrDefault<Texture2D>()`. Page size = 4/3.

- [ ] Keep existing Begin Battle transaction; successful tail becomes:

```csharp
_phase = BattlePhase.AutomaticCombat;
_preparationPanel.Visible = false;
_automaticCombatPanel.Visible = true;
_resultPanel.Visible = false;
_battleInProgress = true;
_battleTimer.Start();
_escapeButton.GrabFocus();
```

Legacy `_itemPanel` remains only for mid-battle cure until Task 4.

- [ ] Replace pre-battle `ShowItemPanelError` calls with `%PreparationItemDetails` feedback that remains in Preparation and returns before timer start. Delete `BuildConsumablePanel()` after all preparation paths use authored slots.

- [ ] Test exact SafeFrame offsets at 1280×720/640×360, 4/3 item page size, and rollback/validation failure staying in Preparation with timer stopped.

- [ ] Gate + commit:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore --nologo

git add scenes/ui/BattleScene.tscn scripts/ui/BattleManager.cs \
  tests/ui/BattleManagerTest.cs tests/ui/BattleSceneTest.cs
git commit -m "feat(ui): author battle layout and preparation"
```

---

## Task 4: Feedback + Cure + Results + legacy presentation deletion

- [ ] Track damage/attack tweens. Replace `CreateTween()` in `ShowDamageNumber`/`PlayAttackAnimation` with a tracked helper. Extend `StopBattleRuntime()` to kill tracked tweens. Replace `EndBattle(bool)`'s direct timer/effect cleanup with `StopBattleRuntime()` too, so result transition kills visual callbacks immediately. `_ExitTree()` calls it and never emits a result.

- [ ] Add local event feed; `RefreshEventFeed()` must trim to current limit every time so standard 5 → compact 3 removes old rows on resize.

- [ ] Add timer-derived progress in `_Process`; never drive combat from it.

- [ ] Replace legacy combat `_itemPanel` with dynamic `%CureOverlay` slots. Only `CureStatusEffect` is actionable; unsupported items remain focusable with reason. Opening stops timer; closing restarts only if Automatic Combat + both actors alive.

- [ ] Final `RequestCancel()`:

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
    EndBattleWithEscape();
    RequestDismiss();
}
```

Delete remaining `_itemPanel`, `ShowCombatItemPanel`, and generic runtime item-panel helpers after cutover.

- [ ] Keep victory grant order, then build `BattleResultSummary`. Render `%LootResultList` from `ResolvedResult.Loot`, then emit once. Defeat builds zero-reward summary. Dismissal never grants.

- [ ] Explicitly delete legacy runtime loot presentation:

```text
_lootLabel
_pendingLootDisplay
StartBattle reset/cleanup for those fields
victory _pendingLootDisplay assignment
CallDeferred(nameof(ShowPendingLootDisplay))
ShowPendingLootDisplay()
ShowLootDisplay(LootResult)
```

Exactly one loot presentation remains: `%LootResultList`.

- [ ] Add meaningful tests: granted values == summary values; summary after grant; result stays hosted; one result emission; loot rows match summary; no stray runtime label; Cure Cancel child-first; visual tweens dead after result/teardown; feed 5→3 on resize.

- [ ] Deep-check 1280×720/640×360 actor/action containment, targets, Continue reachability, damage labels, feed limit. Other shared viewports get light containment only.

- [ ] Gate + commit:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet build Sirius.sln --no-restore --nologo

git add scripts/ui/BattleManager.cs tests/ui/BattleManagerTest.cs \
  tests/ui/BattleSceneTest.cs tests/game/GameInputLifecycleTest.cs \
  tests/game/GameplayPauseHostTest.cs
git commit -m "feat(ui): finish battle feedback and results"
```

---

## Task 5: Lifecycle reconciliation and final verification

- [ ] Update only HPA-376 Battle rows: `BATTLE-PREP`, `BATTLE-AUTO`, both escape rows, victory/defeat/escape Result rows, `BATTLE-CLEANUP`. Document hosted Control, child-first Cure, immediate escape, hosted Results, gameplay suppression after domain battle clears, and host-owned lifetime.

- [ ] Run focused then full tests/build:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~BattleManagerTest|FullyQualifiedName~BattleSceneTest|FullyQualifiedName~GameTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameplayPauseHostTest"
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
```

- [ ] Battle-scoped native grep only:

```bash
rg -n \
  'BattleManager\s*:\s*AcceptDialog|GetOkButton\(|ForceCloseAsEscape|OnCloseRequested|OnBattleDialogConfirmed|_battleManager\.Confirmed|_battleManager\.PopupCentered|_battleManager\.PopupWindow|_battleManager\.Exclusive' \
  scripts/ui/BattleManager.cs scripts/game/Game.cs tests/ui/BattleManagerTest.cs tests/game/GameInputLifecycleTest.cs tests/game/GameplayPauseHostTest.cs
```

Expected: zero. Do not purge unrelated Game error popups.

- [ ] Legacy-presentation deletion grep:

```bash
rg -n \
  '_lootLabel|_pendingLootDisplay|ShowPendingLootDisplay|ShowLootDisplay|BuildConsumablePanel|ShowCombatItemPanel|ShowItemPanelError|_itemPanel' \
  scripts/ui/BattleManager.cs
```

Expected: zero.

- [ ] Verify unsupported UI/speculative architecture stays absent; run `git diff --check main...HEAD` and exact name-status scope audit. Theme, metrics, `UIScreenKinds`, `SiriusPlayerSummaryPresenter`, combat-domain/save/inventory-domain files stay unchanged absent failing-test evidence.

- [ ] Whole-branch review specifically for: stale native calls, invalid startup self-free, duplicate loot/result grant, double result emission, result auto-dismiss, escape entering Results, duplicated escape cleanup, Cure Cancel escaping, prep errors starting combat, timer/tween survival, gameplay unblocking while Results hosted, feed not re-trimming, SafeFrame/node-path drift, damage feedback loss, unrelated popup deletion, or architecture expansion.

- [ ] Commit lifecycle docs:

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs: reconcile hosted battle lifecycle"
```

## Final Acceptance Checklist

- [ ] Every task ends buildable; native→hosted cutover is atomic.
- [ ] `InitialFocusTarget` is defined and used by host.
- [ ] `OnCloseRequested` is gone; de-native-ized `EndBattleWithEscape` is the one escape cleanup/result owner.
- [ ] Invalid `StartBattle` never self-frees.
- [ ] Preparation errors remain visible with timer stopped.
- [ ] Cure Cancel is child-first.
- [ ] Victory/defeat Results stays hosted after `IsInBattle` clears and gameplay stays suppressed.
- [ ] Runtime loot label/deferred path is deleted; loot renders once through `%LootResultList`.
- [ ] Final node paths/SafeFrame/preparation have their own gate before feedback/Cure/Results.
- [ ] Visual tweens die on result transition/teardown; feed re-trims 5→3.
- [ ] No new Theme/metric/host/session/event/picker/reward/shared-presenter architecture.
- [ ] Focused/full tests, build, scoped greps, diff hygiene, and scope audit pass.
