# HPA-570 Hosted Shop and Healing Design

**Issue:** HPA-570  
**Status:** Proposed  
**Date:** 2026-08-16

## Context

HPA-570 is the next HPA-358 secondary-screen migration after HPA-569. HPA-382 already provides the production gameplay `UIScreenHost`, and HPA-569 established the first scene-authored NPC surface.

The remaining Shop and Healing paths still use runtime-built Godot `AcceptDialog` windows:

- `ShopDialog` owns Buy/Sell presentation and synchronous transaction callbacks.
- `HealDialog` owns healing presentation and its synchronous heal callback.
- `NpcInteractionController` closes hosted Dialogue, creates one of those native dialogs under a legacy UI parent, then finishes the NPC interaction when that native dialog terminates.

HPA-373 already defines Shop and Healing as in-game Sirius surfaces. HPA-570 migrates presentation and lifecycle ownership without redesigning the transaction model.

## Goals

- Replace `ShopDialog` and `HealDialog` with scene-authored Sirius surfaces.
- Present both through the existing gameplay `UIScreenHost` using `UIScreenKinds.Shop` and `UIScreenKinds.Heal`.
- Preserve shipped Shop Buy **and Sell** rules, prices, mutation order, rollback, feedback, and close behavior.
- Preserve shipped Healing cost, availability, full-heal effect, gold mutation, completion, and cancellation behavior.
- Explain unavailable actions with standing readable text rather than disabled styling alone.
- Keep mouse, keyboard, and gamepad focus deterministic, including after Shop list rebuilds.
- Preserve exactly-once NPC completion on success, cancel, invalid data, host rejection, publication exceptions, and teardown.

## Non-goals

- New selling mechanics, buyback, bargaining, quantity pickers, bulk transactions, stock mechanics, or pricing rules.
- New healing rules, status recovery, party healing, or stat-system changes.
- A generic transaction framework, base transaction controller, presenter/view-model layer, navigation service, or new host API.
- A reusable shop-row component before another real consumer exists.
- Dialogue, Item Box, Pokémon Summary, puzzle, reward, or error redesign.
- New theme tokens or art requirements.
- Broad HPA-625 cleanup.

## Scope clarification: existing Sell behavior stays

The original HPA-570 wording listed “Selling” under Out of scope, but that conflicts with two shipped baselines:

1. `ShopDialog` already exposes a Sell tab and sells one item for `max(1, floor(Item.Value * 0.5))`.
2. HPA-373 §9.9 explicitly requires Buy and Sell tabs and says existing sale behavior remains unchanged.

For HPA-570, the non-goal is therefore **new selling mechanics**, not existing Sell behavior. The migration preserves the shipped Sell flow without adding buyback, quantity selection, bulk sale, or new pricing rules.

## Reuse decisions

### Reuse directly

- `SiriusModalShell` for both screen frames.
- `SiriusUiMetrics` compact detection and modal widths.
- `UIScreenHost` and the already-defined `UIScreenKinds.Shop` / `UIScreenKinds.Heal`.
- Existing Sirius theme variations and shell `ActionsHost`.
- `Character.TrySpendGold`, `TryAddItem`, `TryRemoveItem`, `GainGold`, and `GetEffectiveMaxHealth`.
- `ShopCatalog`, `ItemCatalog`, and `NpcData` as current data sources.
- The current Shop two-second latest-message-wins feedback semantics.
- Inventory's **semantic focus idea** only: restore by meaning rather than retaining a queued `Control`.

### Extend locally

- `ShopScreenController` creates runtime name/price/action rows. `SiriusItemSlotController` is an icon/quantity/state slot and is not the right row abstraction.
- `HealingScreenController` uses the same small-shell/two-action composition as `SiriusPrompt.tscn`, but not the `SiriusPrompt` controller: prompt Cancel semantics and primary-button availability do not match healing.
- `NpcInteractionController` gains one private helper for the repeated hardened `TryPresent` protocol and one private helper for the repeated stale-handle close protocol. These remain implementation details of the same orchestration owner; per-surface specs, signals, state, and terminal handlers stay explicit.
- `TestHelpers` gains one viewport-mount helper for the two new responsive controller suites; do not refactor unrelated existing tests in this ticket.

### Do not extract

- No transaction service or base screen controller.
- No generic host facade/type/file.
- No reusable Shop row component.
- No broad UIScreenHost refactor across unrelated call sites.

## Architecture

Use two concrete scene/controller pairs:

- `ShopScreen.tscn` + `ShopScreenController`
- `HealingScreen.tscn` + `HealingScreenController`

`NpcInteractionController` remains the Dialogue → Shop/Heal → completion orchestrator.

Shop is a repeatable catalogue that remains open across many synchronous Buy/Sell operations. Healing is a one-shot service that terminates after a successful heal or cancellation. Sharing their state machines would add configuration without useful reuse.

## Shop surface

### Scene geometry

`ShopScreen.tscn` is a full-viewport `Control` containing a centred `SiriusModalShell` directly under the root.

Do **not** add a `%SafeFrame` node. For a non-`Full` centred modal, `SiriusModalShell` already:

- centres its panel;
- caps standard width to the configured modal width and 90% of the viewport;
- applies compact 12 px margins;
- caps body height and owns body scrolling.

That is the same geometry ownership used by centred shell-based screens such as Save/Load. A separate SafeFrame would add a second margin owner and create avoidable double-inset rules.

Author `%ModalShell.SizeClass = SiriusModalSizeClass.Large` (960 px). Do not use Dialogue's `Full` size class or lower-45% bottom band.

Stable authored nodes:

- `%ModalShell`
- `%GoldLabel`
- `%FeedbackLabel` — transient operation feedback only
- `%ShopTabs`
- `%BuyList`
- `%SellList`
- `%CloseButton` under the shell `ActionsHost`

The shell body scroll remains the overflow owner. Do not add nested list scroll containers unless a focused runtime test proves the shell cannot keep the active page reachable.

Dynamic catalogue rows remain controller-created because item count and availability are runtime state.

### Shop identity

Bind `%ModalShell.Title` from `ShopInventory.DisplayName` whenever stored Shop state is rendered. This preserves the existing `ShopDialog.Title = shop.DisplayName` behavior and keeps the service identity visible.

### Transaction ownership

`ShopScreenController` receives the existing `ShopInventory` and `Character` and preserves the current mutation sequence:

- Buy price is `Item.Value`.
- Buy calls `TrySpendGold`, then `TryAddItem`.
- If add fails, restore the spent gold and show `Inventory full!`.
- Sell calls `TryRemoveItem` before granting gold.
- Successful mutations call `GameManager.Instance?.NotifyPlayerStatsChanged()`.
- Missing catalogue items are skipped/revalidated safely; never fabricate substitute items.

The Sell price rule gets exactly one production definition on `ShopScreenController`:

```csharp
internal static int SellPrice(int itemValue) =>
    Mathf.Max(1, Mathf.FloorToInt(itemValue * 0.5f));
```

`ShopPricingTest` calls this production rule directly instead of maintaining a mirrored formula. This avoids a new pricing type while eliminating the duplicated oracle.

### Standing disabled reason vs transient feedback

These are separate channels with different lifetimes.

**Standing Buy reason**

- Every unaffordable Buy row is disabled and shows `Not enough gold!` adjacent to the row.
- The reason is recomputed on refresh and remains while the row is unavailable.
- It is never routed through the two-second timer.

**Transient Shop feedback**

`%FeedbackLabel` keeps the existing two-second latest-message-wins timer for operation outcomes/revalidation such as:

- `Inventory full!`
- `Not enough gold!` when state changed after render but before activation
- `Item no longer available.`

Replacing transient feedback cancels the prior timeout before starting the next one. The timer never clears row-local standing reasons.

Sell empty state remains `Nothing to sell.`.

### Double activation

Shop keeps a controller-local `_operationInFlight` guard because the screen remains open across repeated operations. It prevents re-entrant Buy/Sell callbacks while one synchronous mutation/refresh is executing, then resets in `finally`.

`ShopClosed` remains exactly-once through a terminal latch.

### Focus

Do not retain a `Control` as a restoration key.

Before a rebuild capture only:

- active Buy/Sell tab
- focused item id when focus belongs to a row action

Use one focus chain for initial focus and rebuild restoration:

1. same item row if it still exists **and can receive focus**
2. first focusable row on the active page
3. active tab control
4. Close

This deliberately uses “first focusable” rather than “next,” so no row index is required. Disabled Buy buttons are never selected as initial/restored focus. A zero-gold player therefore lands on a usable tab/Close fallback rather than an unfocusable row.

## Healing surface

### Scene geometry

`HealingScreen.tscn` is a full-viewport `Control` containing a centred `SiriusModalShell` directly under the root.

- No `%SafeFrame` node.
- `%ModalShell.SizeClass = SiriusModalSizeClass.Small` (420 px), authored explicitly rather than relying on the shell's Medium default.
- Do not copy Dialogue's bottom band.
- The same scene handles compact mode through `_shell.Compact` + `RefreshPresentation(viewportSize)`.

Body content:

- current/max HP
- heal cost
- available gold
- `%FeedbackLabel`

Actions under shell `ActionsHost`:

- `%CancelButton` — `No Thanks`
- `%HealButton` — primary action

Bind `%ModalShell.Title = npc.DisplayName`.

### Availability and feedback

Healing has no timed feedback timer today; do not add one.

`%FeedbackLabel` is a standing non-timed availability/validation channel:

- full HP → `You are already at full health.`
- insufficient gold → `Not enough gold!`
- otherwise clear the message

Heal is disabled whenever either unavailable state applies. Revalidate again on activation because HP/gold may change after render.

Initial focus:

1. Heal when enabled/focusable
2. otherwise No Thanks

### Healing behavior and double activation

Preserve the current mutation order:

- check terminal latch
- re-check full HP
- attempt `TrySpendGold`
- set `CurrentHealth = GetEffectiveMaxHealth()`
- notify player-stat change
- set terminal latch
- emit `HealComplete`

Cancellation sets the same terminal latch before emitting `HealCancelled`.

Do **not** add `_operationInFlight` to Healing. Godot button callbacks are synchronous; sequential duplicate activations encounter the terminal latch after the first successful callback completes. The terminal latch is therefore the local double-activation guard for this one-shot screen. Test duplicate Heal activation directly.

## Responsive lifecycle

Both new controllers follow the existing centred-shell pattern:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady() || _shell == null || !IsInsideTree())
        return;

    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _shell.RefreshPresentation(size);
}
```

Each controller:

- subscribes `Resized` in `_Ready()`;
- unsubscribes it in `_ExitTree()`;
- also disconnects button/timer handlers in `_ExitTree()`;
- supports configuration before `_Ready()` and renders stored state after node binding.

No new breakpoint or layout helper is added.

## Host integration

### Explicit per-surface policy

Shop and Healing follow HPA-373 §7.3. This intentionally differs from the current HPA-569 Dialogue implementation, which still uses `Hud = Visible`; HPA-570 does not widen scope into changing Dialogue.

Shop policy:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Shop,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Consume,
    InitialFocus = () => screen.InitialFocusTarget,
    InterceptCancel = _ =>
    {
        screen.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    Cleanup = _ => ClearShopPresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
}
```

Healing uses the same policy values with `Kind = UIScreenKinds.Heal`, Healing focus/cancel, and `ClearHealPresentation`.

No parent handle, `BlockingPrompt` group, incompatible-kind rule, or new host API is added.

### Consolidate the hardened presentation protocol inside the existing orchestrator

HPA-569's Dialogue path now contains a subtle but tested protocol around `TryPresent`:

- catch post-commit publication exceptions;
- unsubscribe/free the candidate and `Finish()` before rethrowing;
- handle rejected/no-handle results;
- after `Opened`, call `IsActive(handle)` because a subscriber may have synchronously closed the entry;
- retain screen/handle only when the returned handle is still active.

HPA-570 would otherwise copy that protocol twice. Instead, first refactor the existing Dialogue path behind one **private `NpcInteractionController` method**, while current Dialogue regression tests remain green:

```csharp
private bool TryHostSurface(
    Control screen,
    UIScreenEntrySpec spec,
    Action unsubscribe,
    out UIScreenHandle handle)
```

The helper owns only the mechanical present/failure/liveness protocol. It may call `Finish()` on failed presentation exactly as current `Begin()` does. It does **not** construct specs, subscribe signals, configure screens, own per-surface state, or become a shared host service.

Then Dialogue, Shop, and Healing each:

1. configure their concrete screen;
2. subscribe their own signals;
3. construct their own explicit `UIScreenEntrySpec`;
4. call `TryHostSurface(...)`;
5. retain concrete screen + returned handle only on success.

This is a three-use private extraction in the orchestration owner, not a generic facade.

### Consolidate stale-handle close mechanics

Likewise, centralize the mechanical `TryClose` + `StaleHandle` branch in one private helper:

```csharp
private void CloseHostedPresentation(
    UIScreenHandle? handle,
    UIScreenCloseReason reason,
    Action clear)
```

Per-surface `CloseDialoguePresentation`, `CloseShopPresentation`, and `CloseHealPresentation` remain explicit wrappers so their concrete state and cleanup callbacks stay readable.

Terminal handlers still catch close-publication exceptions and converge on idempotent `Finish()`.

`Finish()` keeps the HPA-569 ordering:

- return when already finished;
- set `_finished = true` before cleanup;
- attempt hosted presentation cleanup inside `try/catch`;
- always invoke `InteractionComplete` exactly once even when close publication throws.

## Legacy cleanup

After hosted behavior and real-route lifecycle tests are green:

- delete `scripts/ui/ShopDialog.cs`
- delete `scripts/ui/HealDialog.cs`
- replace their controller tests with new screen-controller tests
- remove the legacy `_uiParent` dependency from `NpcInteractionController`, `Game`, and test fixtures
- update `ShopPricingTest` to call `ShopScreenController.SellPrice` directly
- remove/rewrite tests that document the retired native Shop/Heal cancel phase

No compatibility shim remains.

## Test reuse

The two new responsive controller suites both need a `SubViewportContainer` + `SubViewport` mount. Add one test-only helper:

```csharp
public static (SubViewportContainer Container, SubViewport Viewport)
    MountInViewport(Node child, Vector2I size)
```

Use it in the new Shop and Healing tests. Do not perform a drive-by conversion of existing suites.

## Testing strategy

### Shop controller

Cover:

- pre-ready one-shot configuration
- Large centred shell, no `AcceptDialog`, no SafeFrame requirement
- shell title equals `ShopInventory.DisplayName`
- successful Buy and list/gold refresh
- standing per-row `Not enough gold!`
- inventory-full rollback with transient `Inventory full!`
- `ShopScreenController.SellPrice` directly tested by `ShopPricingTest`
- removal-before-gold and last-item `Nothing to sell.`
- latest transient feedback timer wins without affecting standing reasons
- `_operationInFlight` rejects re-entrant Shop mutation
- cancel/close exactly once
- initial/rebuild focus never targets disabled or queued controls

### Healing controller

Cover:

- pre-ready one-shot configuration
- Small centred shell, actions in `ActionsHost`, no `AcceptDialog`, no SafeFrame requirement
- shell title equals NPC display name
- successful Heal spends gold, restores max HP, completes once
- full HP / insufficient gold standing reasons
- initial focus Heal when enabled, No Thanks otherwise
- no feedback timer
- duplicate Heal activation still spends/emits once through terminal latch
- cancel exactly once

### NPC and real production lifecycle

The cutover task itself must add tests proving:

- Dialogue closes before one hosted Shop/Heal entry opens
- private present-helper refactor keeps all existing Dialogue publication-exception/liveness tests green
- Shop/Heal policy is `PauseTree = false`, gameplay blocking, visible cursor, **hidden HUD**, inert lower layers, consumed Cancel
- keyboard and gamepad Cancel close real Shop/Heal routes without opening Pause
- `Finish()` while either screen is active closes it and completes once
- invalid Shop data, host rejection, synchronous post-commit close, and close-publication exceptions leave no stale handle or latched NPC interaction

`ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler` is deleted or rewritten because that production phase no longer exists.

## Risks and mitigations

### Host lifecycle drift

Mitigation: extract only the existing hardened present/close mechanics into private methods in `NpcInteractionController`, guarded first by the existing Dialogue tests, then reuse those methods for Shop/Healing.

### Wrong modal geometry

Mitigation: pin `Large` and `Small` size classes in scene tests and follow the existing centred non-Full shell pattern. Do not introduce a second SafeFrame/margin owner.

### Standing reason disappears with timed feedback

Mitigation: Shop row availability and transient feedback are separate controls/lifetimes. Healing has one standing non-timed channel and no timer.

### Shop refresh leaves unusable focus

Mitigation: semantic item-id restore is accepted only when the target can receive focus; otherwise first focusable row → tab → Close.

### Pricing test drifts from production

Mitigation: one production `ShopScreenController.SellPrice` method; tests call it directly.

### Scope expands into framework work

Mitigation: no new production type/file for transactions or hosting. The only lifecycle extraction is private to the existing orchestration class and has exactly three concrete uses after migration.

## Acceptance mapping

- **No desktop-window framing:** native Shop/Heal dialogs are deleted and replaced by authored centred `SiriusModalShell` scenes.
- **Transactional parity:** shipped Buy, Sell, healing, rollback, pricing, and completion semantics stay unchanged.
- **Disabled reasons:** Buy affordability and Heal unavailability remain visible as standing text.
- **Input parity:** focus targets are focusable; host Cancel covers keyboard/gamepad without Pause fallthrough.
- **Double activation:** Shop uses `_operationInFlight`; Healing uses its one-shot terminal latch.
- **Lifecycle parity:** one hardened private presentation protocol serves Dialogue/Shop/Heal while per-surface orchestration stays explicit.
