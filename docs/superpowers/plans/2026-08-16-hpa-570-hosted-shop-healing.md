# HPA-570 Hosted Shop and Healing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `ShopDialog` and `HealDialog` windows with scene-authored, host-managed Sirius Shop and Healing surfaces while preserving the currently shipped transaction/healing behavior and exactly-once NPC lifecycle.

**Architecture:** Keep `NpcInteractionController` as the single Dialogue → Shop/Heal orchestration owner. Build two concrete controllers because Shop is a repeatable Buy/Sell catalogue while Healing is a one-shot confirmation. Reuse `SiriusModalShell`, `SiriusUiMetrics`, Sirius theme variations, `UIScreenHost`, and the already-defined `UIScreenKinds.Shop` / `UIScreenKinds.Heal`. Keep current Character/catalog APIs as the transaction boundary; add no transaction service, presenter, or generic host facade.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme, `SiriusModalShell`, `SiriusUiMetrics`, and `UIScreenHost`.

## Global constraints

- Preserve existing Buy behavior: `Item.Value`, affordability check, item add, gold rollback when inventory is full, player-stat notification, and list refresh.
- Preserve existing Sell behavior: one item per activation at `max(1, floor(Item.Value * 0.5))`, removal before gold grant, player-stat notification, and `Nothing to sell.` empty state.
- Treat HPA-570's “Selling” non-goal as “no new selling mechanics”; HPA-373 §9.9 and current production code both require existing Sell parity.
- Preserve current two-second Shop feedback and latest-message-wins timer behavior.
- Preserve Healing behavior: configured NPC cost, current/effective-max HP check, affordability check, full HP restore, stat notification, complete/cancel exactly once.
- Add readable disabled reasons for insufficient gold and unavailable healing.
- Add controller-local in-flight guards only; do not add async transaction infrastructure for synchronous operations.
- Reuse `SiriusModalShell`; do not add another shell/frame abstraction.
- Reuse `UIScreenKinds.Shop` and `UIScreenKinds.Heal`; do not add host kinds, exclusive groups, or host APIs.
- Keep gameplay scene-tree running while Shop/Heal is open; block gameplay input through the host.
- Keep one scene per screen. Compact behavior is responsive presentation, not a second controller or duplicated scene.
- Dynamic Shop rows may be constructed by `ShopScreenController`; do not add a row component until reuse exists.
- Do not add stock, quantity picker, buyback, party heal, status heal, new pricing, or new domain rules.
- Do not keep a compatibility wrapper around `ShopDialog` or `HealDialog` after hosted parity is green.

---

## File structure

### Create

- `scenes/ui/ShopScreen.tscn`
- `scripts/ui/ShopScreenController.cs`
- `tests/ui/ShopScreenControllerTest.cs`
- `scenes/ui/HealingScreen.tscn`
- `scripts/ui/HealingScreenController.cs`
- `tests/ui/HealingScreenControllerTest.cs`

### Modify

- `scripts/ui/NpcInteractionController.cs`
- `scripts/game/Game.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete after replacement coverage is green

- `scripts/ui/ShopDialog.cs`
- `tests/ui/ShopDialogTest.cs`
- `scripts/ui/HealDialog.cs`
- `tests/ui/HealDialogTest.cs`

### Audit only unless a focused failure proves a change is required

- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/data/npc/NpcData.cs`
- Shop/catalogue data types
- `Character` inventory/gold/health APIs

---

## Risk checklist

### Existing Sell behavior is easy to drop because the ticket wording is stale

Port an explicit Sell parity test before deleting `ShopDialogTest`. No new Sell mechanics are added, but current Sell cannot disappear.

### `TryPresent` can return `Opened` after a publication subscriber synchronously closed the entry

Use the HPA-569 pattern: supply host cleanup, then call `IsActive(handle)` before retaining the screen/handle. Never retain a stale handle.

### Host close publication can throw after cleanup has already committed

Follow the existing Dialogue close/finish pattern. Cleanup clears signals and controller state before publication; terminal orchestration catches publication exceptions where needed so `InteractionComplete` still fires exactly once.

### Rebuilding Shop rows can destroy the focused node

Capture the focused row's stable item id + Buy/Sell page before refresh. Restore that row when it still exists; otherwise focus the next available action, then the active tab/Close fallback. This state is ephemeral to the open screen only.

### Disabled controls can become unexplained dead ends

Every disabled Buy/Heal state gets readable reason text. Keep validation on activation too because gold/HP/inventory may change after render.

---

## Task 1: Build the authored Shop screen with transaction parity

**Files:**
- Create: `scenes/ui/ShopScreen.tscn`
- Create: `scripts/ui/ShopScreenController.cs`
- Create: `tests/ui/ShopScreenControllerTest.cs`
- Reference only: `scripts/ui/ShopDialog.cs`
- Reference only: `tests/ui/ShopDialogTest.cs`

**Controller contract:**

```csharp
public partial class ShopScreenController : Control
{
    [Signal] public delegate void ShopClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenShop(ShopInventory shop, Character player);
    public void RequestCancel();
}
```

Keep this screen single-start, like `DialogueScreenController`. Configuration may happen before `_Ready()`; `_Ready()` binds authored nodes and renders stored state.

- [ ] **Step 1: Write RED configuration and authored-scene tests**

Add tests that:

- load `res://scenes/ui/ShopScreen.tscn`
- instantiate it as `ShopScreenController`
- call `TryOpenShop(...)` before `AddChild(...)`
- assert the title/gold/tabs/lists bind after `_Ready()`
- reject a second start on the same screen instance
- expose a non-null initial focus target after render

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest"
```

Expected: FAIL because the scene/controller do not exist.

- [ ] **Step 2: Author the stable Shop scene tree**

Create a full-viewport `Control` with a safe-frame-owned `SiriusModalShell`. Author only stable structure:

- `%SafeFrame`
- `%ModalShell`
- `%GoldLabel`
- `%FeedbackLabel`
- `%ShopTabs`
- `%BuyList`
- `%SellList`
- `%CloseButton`

Use the shell body scroll/layout already provided by `SiriusModalShell`. Use Sirius theme variations and existing spacing/target metrics. Do not author fixed item rows.

- [ ] **Step 3: Bind responsive presentation in `ShopScreenController`**

On `_Ready()`:

- bind the unique-name nodes
- connect Close
- subscribe to `Resized`
- compute `SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size)`
- keep shell inside the safe frame
- set `Compact` from the metric result
- refresh shell using available safe size
- re-render stored Shop state if `TryOpenShop(...)` ran pre-ready

Do not add a new breakpoint or hard-code a second compact scene.

- [ ] **Step 4: Port Buy behavior as tests before implementation**

Add tests for:

1. successful Buy deducts `Item.Value`, adds one item, and refreshes gold/list state
2. insufficient gold disables the Buy action and exposes readable reason text
3. inventory-full Buy rolls the spent gold back and shows `Inventory full!`
4. missing catalog item is skipped safely rather than creating a substitute

Then port the existing `ShopDialog` mutation sequence directly into `ShopScreenController` using `Character` and `ItemCatalog`; do not extract a service.

Run the focused Shop suite after each RED/GREEN step.

- [ ] **Step 5: Port Sell behavior explicitly**

Add tests proving:

- sell price is `Mathf.Max(1, Mathf.FloorToInt(item.Value * 0.5f))`
- one activation removes one item before granting gold
- selling the last item immediately renders `Nothing to sell.`
- failed removal shows feedback and refreshes without granting gold

Implement from the current `ShopDialog` behavior without adding quantity selection, buyback, or new pricing.

- [ ] **Step 6: Preserve feedback timer semantics**

Port `ShowFeedback_KeepsLatestMessageVisible_UntilLatestTimerExpires` to the new controller. Keep one timer reference/handler; replacing feedback unsubscribes the previous timeout before creating the next two-second timer. Cancel the timer on close and `_ExitTree()`.

- [ ] **Step 7: Add local double-activation and terminal guards**

Use:

```csharp
private bool _operationInFlight;
private bool _terminalEmitted;
```

Buy/Sell callbacks return immediately when `_operationInFlight` is true. Set/reset the flag in `try/finally` around synchronous transaction/refresh work. `RequestCancel()` emits `ShopClosed` once and cancels pending feedback.

Test re-entrant button signal invocation and `RequestCancel()` twice.

- [ ] **Step 8: Preserve focus across list refresh**

Before Buy/Sell mutation, capture active tab + focused item id. After refresh:

1. restore the same item's action if still present
2. else focus the next valid action on the current page
3. else focus the page control/Close fallback

Test selling the currently focused last item and buying an item that causes list button enablement changes; the viewport must not retain focus on a queued node.

- [ ] **Step 9: Run Shop GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~ShopDialogTest"
```

Expected at this point: new Shop tests PASS and legacy Shop tests remain PASS because legacy deletion has not happened yet.

---

## Task 2: Build the authored Healing screen with one-shot parity

**Files:**
- Create: `scenes/ui/HealingScreen.tscn`
- Create: `scripts/ui/HealingScreenController.cs`
- Create: `tests/ui/HealingScreenControllerTest.cs`
- Reference only: `scripts/ui/HealDialog.cs`
- Reference only: `tests/ui/HealDialogTest.cs`

**Controller contract:**

```csharp
public partial class HealingScreenController : Control
{
    [Signal] public delegate void HealCompleteEventHandler();
    [Signal] public delegate void HealCancelledEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenHeal(NpcData npc, Character player);
    public void RequestCancel();
}
```

- [ ] **Step 1: Write RED authored-scene/configuration tests**

Cover pre-ready configuration, single-start behavior, initial focus, and required unique-name nodes.

- [ ] **Step 2: Author the small Healing scene**

Create one full-viewport root with a safe-frame-owned small `SiriusModalShell` containing:

- `%HealthLabel`
- `%CostLabel`
- `%GoldLabel`
- `%FeedbackLabel`
- `%HealButton`
- `%CancelButton`

Use Sirius primary/secondary button variations and existing minimum-target metrics.

- [ ] **Step 3: Port availability presentation**

Render current/effective-max HP, heal cost, and player gold. Disable Heal when:

- current HP is already at effective max → reason `Already at full health.`
- player gold is below `NpcData.HealCost` → reason `Not enough gold.`

Do not encode a new rule for non-positive `HealCost`; preserve the current warning and allow the configured behavior.

- [ ] **Step 4: Port Heal mutation and exactly-once terminal behavior**

Add RED tests proving:

- successful Heal deducts the configured cost
- `CurrentHealth` becomes `GetEffectiveMaxHealth()`
- successful Heal emits `HealComplete` once
- Heal then Cancel does not emit `HealCancelled`
- Cancel/RequestCancel twice emits `HealCancelled` once
- insufficient gold cannot mutate HP/gold even if activation is invoked programmatically

Implement the same mutation order as the current `HealDialog` and keep `GameManager.Instance?.NotifyPlayerStatsChanged()`.

- [ ] **Step 5: Add local in-flight guard**

Guard the Heal callback with `_operationInFlight` and the existing terminal latch. Reset the in-flight flag only when the screen remains non-terminal.

- [ ] **Step 6: Run Healing GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~HealDialogTest"
```

Expected: new and legacy Healing behavior tests PASS before cutover.

---

## Task 3: Cut `NpcInteractionController` over to hosted Shop and Heal

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`

**Target state:** `NpcInteractionController` owns three possible hosted surface/handle pairs—Dialogue, Shop, or Heal—but only one is active during the normal NPC flow.

- [ ] **Step 1: Convert native-flow tests to RED host expectations**

Replace the current tests that locate `ShopDialog`/`HealDialog` children beneath `LegacyNpcUiParent` with assertions that:

- Dialogue closes before Shop opens
- exactly one `UIScreenKinds.Shop` entry is active
- Dialogue closes before Healing opens
- exactly one `UIScreenKinds.Heal` entry is active
- cancel closes that hosted entry and emits `InteractionComplete` once

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: FAIL because Shop/Heal still use native dialogs.

- [ ] **Step 2: Replace native fields with hosted state**

In `NpcInteractionController`, replace:

```csharp
private ShopDialog _shopDialog;
private HealDialog _healDialog;
```

with nullable screen + handle pairs for `ShopScreenController` and `HealingScreenController`.

Keep explicit methods:

- `OpenShop`, `ClearShopPresentation`, `CloseShopPresentation`
- `OpenHeal`, `ClearHealPresentation`, `CloseHealPresentation`

Do not extract a generic transaction/presentation owner.

- [ ] **Step 3: Host Shop using the HPA-569 lifecycle contract**

`OpenShop()`:

1. resolve `ShopCatalog.GetById(_npc.ShopId)`; invalid data logs and `Finish()`es
2. load/instantiate `ShopScreen.tscn`
3. call `TryOpenShop(shopInventory, _player)` before presentation
4. subscribe `ShopClosed`
5. call `_screenHost.TryPresent(...)` with `Kind = UIScreenKinds.Shop`
6. configure modal policy, gameplay blocking, visible cursor, visible/inert lower layers, initial focus, Cancel interception, cleanup, and `QueueFree`
7. on exception, unsubscribe/queue-free candidate, `Finish()`, then preserve the existing exception behavior
8. if not opened, clean candidate and `Finish()`
9. if returned handle is already inactive, `Finish()` without retaining stale state
10. otherwise store screen + handle

- [ ] **Step 4: Host Healing using the same explicit contract**

Mirror the lifecycle shape with `HealingScreen.tscn`, `UIScreenKinds.Heal`, `HealComplete`, `HealCancelled`, and `RequestCancel()`.

On Heal complete/cancel, close the hosted entry and then call `Finish()` once.

- [ ] **Step 5: Extend teardown/error coverage**

Add focused tests for:

- `Finish()` while Shop active closes Shop and completes once
- `Finish()` while Heal active closes Heal and completes once
- invalid Shop id opens no Shop entry and completes once
- a pre-existing Shop/Heal entry that causes host rejection does not leak the candidate
- a post-commit `EffectiveStateChanged` subscriber that synchronously closes the entry leaves no stale handle
- a close publication exception still results in exactly-once `InteractionComplete`

Follow the existing Dialogue tests rather than creating a new fake host.

- [ ] **Step 6: Remove the legacy UI-parent constructor dependency**

Once Shop/Heal no longer call `AddChild` on `_uiParent`, delete `_uiParent` from `NpcInteractionController` state and constructor. Update the sole production construction in `scripts/game/Game.cs` and all test helpers in the same commit. Do not leave an unused compatibility parameter.

- [ ] **Step 7: Run orchestration GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: PASS with no native Shop/Heal children and no stale hosted entries.

---

## Task 4: Delete native dialogs and update production lifecycle coverage

**Files:**
- Delete: `scripts/ui/ShopDialog.cs`
- Delete: `tests/ui/ShopDialogTest.cs`
- Delete: `scripts/ui/HealDialog.cs`
- Delete: `tests/ui/HealDialogTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Prove no production references remain**

Search the repository for:

```text
ShopDialog
HealDialog
LegacyNpcUiParent
```

Only deletion targets/history docs should remain before deletion. Do not add wrappers or aliases.

- [ ] **Step 2: Delete native classes and superseded tests**

Delete the four files only after Tasks 1–3 are green.

- [ ] **Step 3: Update Game host/lifecycle tests left intentionally native by HPA-569**

Audit `GameplayPauseHostTest` and `GameInputLifecycleTest` for Shop/Heal assumptions. Update them to assert production NPC interaction opens `UIScreenKinds.Shop` / `UIScreenKinds.Heal`, blocks gameplay input while active, and restores input exactly once after cancel/completion.

Do not duplicate detailed transaction tests here; these are production host/lifecycle tests.

- [ ] **Step 4: Update the lifecycle contract**

In `docs/ui/hpa-376/ui-lifecycle-contract.md`, document Shop and Heal as gameplay-hosted modal entries alongside Dialogue:

- host kind
- gameplay-input blocking
- no scene-tree pause
- cancel interception
- node lifetime
- orchestration owner (`NpcInteractionController`)

Remove wording that describes Shop/Heal as native dialogs.

- [ ] **Step 5: Run the migration blast radius**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: PASS.

---

## Task 5: Responsive/input verification and final regression pass

**Files:**
- Modify only the new scenes/controllers/tests if focused failures prove a fix is required.

- [ ] **Step 1: Add representative viewport assertions**

For both authored scenes, exercise at least:

- 640×360 compact
- 1280×720 reference
- 1920×1080 standard

Assert the shell remains inside safe-frame bounds, compact metrics are applied at 640×360, required controls remain reachable, and list/body scrolling—not viewport clipping—handles overflow.

Do not add screenshot/golden infrastructure for this ticket unless it already exists and is required by a current test.

- [ ] **Step 2: Verify keyboard/gamepad focus behavior**

Cover:

- initial Shop focus reaches a valid action
- initial Healing focus prefers Heal when enabled and No Thanks when Heal is unavailable
- host Cancel closes each surface
- Shop row refresh never leaves focus on a queued/freed control
- Buy/Sell tab switching keeps a usable focus target

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS with no `ShopDialog`/`HealDialog` references.

- [ ] **Step 4: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS.

- [ ] **Step 5: Inspect the final diff for scope creep**

The implementation diff should contain only:

- two authored scenes/controllers and their tests
- NPC/Game host cutover
- legacy Shop/Heal deletion
- affected host/lifecycle tests and lifecycle documentation

Reject accidental additions of transaction services, base controllers, new host kinds, new theme tokens, new shop/healing rules, or unrelated HPA-571/HPA-573/HPA-625 work.

---

## Done criteria

HPA-570 is implementation-complete when:

- Shop and Healing are authored Sirius scenes, not desktop dialogs.
- `NpcInteractionController` presents them through `UIScreenHost` using existing Shop/Heal kinds.
- current Buy **and Sell** behavior is preserved without new transaction mechanics.
- current Healing behavior is preserved without new rules.
- disabled Buy/Heal actions have readable reasons.
- local re-entrant activation is guarded.
- mouse/keyboard/gamepad focus remains usable after dynamic Shop refresh.
- every close/success/failure/teardown path restores gameplay and completes NPC interaction exactly once.
- native Shop/Heal classes and their superseded tests are deleted.
- focused migration tests, build, and full test suite are green.
