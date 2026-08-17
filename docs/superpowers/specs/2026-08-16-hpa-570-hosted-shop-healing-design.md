# HPA-570 Hosted Shop and Healing Design

**Issue:** HPA-570  
**Status:** Proposed  
**Date:** 2026-08-16

## Context

HPA-570 is the next actionable HPA-358 secondary-screen migration after HPA-569. HPA-382 already provides the production gameplay `UIScreenHost`, and HPA-569 established the scene-authored NPC interaction pattern.

The remaining Shop and Healing paths still use runtime-built Godot `AcceptDialog` windows:

- `ShopDialog` owns Buy/Sell presentation and synchronous transaction callbacks.
- `HealDialog` owns healing presentation and its synchronous heal callback.
- `NpcInteractionController` closes hosted Dialogue, creates one of those native dialogs under a legacy UI parent, then finishes the NPC interaction when that native dialog terminates.

HPA-373 already defines Shop and Healing as in-game Sirius surfaces rather than desktop windows. HPA-570 migrates presentation and lifecycle ownership without redesigning the transaction model.

## Goals

- Replace `ShopDialog` and `HealDialog` with scene-authored Sirius surfaces.
- Present both through the existing gameplay `UIScreenHost` using `UIScreenKinds.Shop` and `UIScreenKinds.Heal`.
- Preserve current Shop Buy and Sell rules, prices, inventory/gold mutation order, rollback, feedback, and close behavior.
- Preserve current Healing cost, availability, full-heal effect, gold mutation, completion, and cancellation behavior.
- Explain disabled actions with standing readable text rather than disabled styling alone.
- Keep keyboard, gamepad, and mouse focus deterministic, including after Shop list rebuilds.
- Make every terminal path finish the NPC interaction exactly once, including cancel, invalid data, host rejection, publication exceptions, and teardown.

## Non-goals

- New shop stock rules, buyback, bargaining, quantity pickers, bulk transactions, or pricing rules.
- New healing rules, status recovery, party healing, or stat-system changes.
- A generic transaction framework, base transaction controller, presenter/view-model layer, navigation service, or host facade.
- A reusable shop-row component before another real consumer exists.
- Dialogue, Item Box, Pokémon Summary, puzzle, reward, or error redesign.
- New theme tokens or art requirements.
- Broad HPA-625 legacy-widget cleanup.

## Scope clarification: existing Sell behavior stays

HPA-570 currently lists “Selling” under Out of scope, but that conflicts with two shipped baselines:

1. `ShopDialog` already exposes a Sell tab and sells one item for `max(1, floor(Item.Value * 0.5))`.
2. HPA-373 §9.9 explicitly requires Buy and Sell tabs and says existing sale behavior remains unchanged.

For this migration, “Selling” means **no new selling mechanics**. Removing the existing Sell flow would be a gameplay regression. The migration preserves the current Sell tab and pricing exactly while adding no buyback, quantity selection, bulk sale, or new sell rule.

## Reuse decisions

- **Shop scene/controller:** extend the hosted NPC-surface pattern from `DialogueScreen`, but not Dialogue's bottom-band geometry. Port behavior from `ShopDialog`.
- **Healing scene/controller:** use the same centred small-shell composition shape as `SiriusPrompt`, including shell `ActionsHost`, but keep a dedicated Healing controller because `SiriusPrompt` has no HP/cost/gold model or disabled-primary contract. Port behavior from `HealDialog`.
- **Dynamic Shop rows:** create runtime row controls locally in `ShopScreenController`. `SiriusItemSlotController` is an icon/quantity/state slot, not a name/price/action row.
- **Focus restoration:** reuse Inventory's idea of semantic focus restoration, but only capture active tab + item id; do not copy Inventory's broader pending-focus record machinery.
- **Host lifecycle:** extend the explicit `Begin` / clear / close pattern already proven for hosted Dialogue in `NpcInteractionController`.
- **Host kinds:** reuse `UIScreenKinds.Shop` and `UIScreenKinds.Heal` exactly as defined.
- **Modal chrome and metrics:** reuse `SiriusModalShell` and `SiriusUiMetrics`; no new shell or breakpoint.
- **Transactions:** reuse `Character.TrySpendGold`, `TryAddItem`, `TryRemoveItem`, `GainGold`, `GetEffectiveMaxHealth`, `ShopCatalog`, and `ItemCatalog` directly.
- **Shop feedback timer:** preserve the existing `ShopDialog.ShowFeedback` two-second latest-message-wins behavior.

## Design decision

Use **two separate scene/controller pairs**:

- `ShopScreen.tscn` + `ShopScreenController`
- `HealingScreen.tscn` + `HealingScreenController`

Do not introduce a shared transaction controller. Shop is a repeatable Buy/Sell catalogue that stays open across many synchronous operations; Healing is a small one-shot confirmation that terminates after successful healing or cancellation. Sharing those state machines would add configuration without proven reuse.

## Shop surface

### Geometry and scene structure

`ShopScreen.tscn` is a full-viewport `Control` with an authored `%SafeFrame` and centred `SiriusModalShell`.

The geometry is explicit:

- `%SafeFrame` uses the same centred safe-frame offsets as `InventoryMenuController.RefreshLayout`: left/right = `SafeFrameInsets.SideInset`, top/bottom = `SafeFrameInsets.Margin`.
- `%ModalShell.SizeClass` is authored as **`SiriusModalSizeClass.Large` (960 px)**.
- Do **not** use `SiriusModalSizeClass.Full`; HPA-569 added `Full` for Dialogue's wide bottom band, and Shop does not use that placement.
- Do **not** copy Dialogue's lower-45%-height band. Shop is a centred service surface.
- The same scene is used in compact mode. `Compact` changes shell typography/targets and the `TabContainer` naturally exposes one active page; no second controller or scene is created.
- `ShopScreenController` passes the full viewport size to `SiriusModalShell.RefreshPresentation(...)`; the safe frame owns placement while the non-Full shell owns its existing 90%/compact width policy. This avoids subtracting compact margins twice.

Stable authored nodes:

- `%SafeFrame`
- `%ModalShell`
- `%GoldLabel`
- `%FeedbackLabel` — transient operation feedback only
- `%ShopTabs`
- `%BuyList`
- `%SellList`
- `%CloseButton` in the shell `ActionsHost`

The shell-owned body scroll remains the single overflow owner; do not add nested per-tab scroll containers unless a focused runtime test proves the tab content cannot scroll correctly through the shell.

Dynamic catalogue rows remain controller-created because their count and item data are runtime state. A Buy row contains item name, price, action, and a standing reason label when disabled. A Sell row contains item name/quantity, price, and action. Use existing Sirius button/text theme variations; do not add a row class.

### Transaction ownership

`ShopScreenController` receives the existing `ShopInventory` and `Character` and performs the same operations currently in `ShopDialog`:

- Buy price remains `Item.Value`.
- Buy calls `Character.TrySpendGold`, then `Character.TryAddItem`.
- If add fails, restore the spent gold and show `Inventory full!`.
- Sell price remains `max(1, floor(Item.Value * 0.5))`.
- Sell calls `Character.TryRemoveItem` before `GainGold`.
- Successful mutations continue to call `GameManager.Instance?.NotifyPlayerStatsChanged()`.
- Missing catalogue entries remain warnings/errors followed by a safe refresh, never invented substitute items.

No transaction service or price type is extracted.

### Standing disabled reason vs transient feedback

These are two different channels and must not share lifetime semantics.

**Standing Buy reason**

- Every unaffordable Buy row remains visibly disabled **and** shows `Not enough gold!` adjacent to that row.
- The reason is recomputed on every list refresh and remains visible as long as the row is unavailable.
- It is not routed through the two-second feedback timer.

**Transient Shop feedback**

`%FeedbackLabel` keeps the existing two-second latest-message-wins timer only for operation outcomes/revalidation such as:

- `Inventory full!`
- `Not enough gold!` when an activation reaches the callback after state changed since render
- `Item no longer available.`

Replacing transient feedback cancels the previous timeout before creating the new timer so an older timeout cannot hide a newer message.

Sell empty state remains `Nothing to sell.`.

### Double activation and focus

Use one controller-local `_operationInFlight` guard around each synchronous Buy/Sell callback. It prevents re-entrant activation while one callback is executing and resets when the synchronous work finishes. It is not async transaction infrastructure and does not prevent a later intentional Buy/Sell.

`ShopClosed` remains exactly-once through a terminal latch.

Before a Buy/Sell rebuild, capture only:

- active Buy/Sell tab
- focused item id when focus belongs to a row action

After refresh, restore the same semantic row when it still exists; otherwise choose the next valid action on the active page, then the tab/Close fallback. This is ephemeral in-instance focus state only.

## Healing surface

### Geometry and scene structure

`HealingScreen.tscn` is a full-viewport `Control` with a centred small `SiriusModalShell`.

The geometry is explicit:

- `%SafeFrame` uses the same centred safe-frame offsets as Inventory.
- `%ModalShell.SizeClass` is authored explicitly as **`SiriusModalSizeClass.Small` (420 px)**; do not rely on the shell's Medium default.
- Do not copy Dialogue's bottom band.
- The body contains NPC/title identity, current/max HP, cost, available gold, and `%FeedbackLabel`.
- `%HealButton` and `%CancelButton` live under the shell `ActionsHost`, matching the stable two-action composition used by `SiriusPrompt.tscn` without reusing `SiriusPrompt` itself.
- The same scene handles compact mode; no second controller is introduced.
- As with Shop, pass the full viewport size to `RefreshPresentation(...)` so the shell owns its existing non-Full compact margin policy exactly once.

### Availability and feedback

Healing has no timed feedback timer today; do not add one.

`%FeedbackLabel` is a **standing non-timed availability/validation channel**:

- full HP → `You are already at full health.`
- insufficient gold → `Not enough gold!`
- enabled Heal → clear the standing message unless another current validation message is required

Heal is disabled whenever either standing unavailable state applies. Initial focus is:

1. Heal when enabled
2. otherwise No Thanks

The controller still revalidates HP and affordability on activation because state may change after render.

### Healing behavior

`HealingScreenController` preserves current `HealDialog` semantics:

- warn when configured heal cost is non-positive, but do not invent a new rule
- disable Heal when HP is already full
- disable Heal when gold is insufficient
- on activation, re-check HP and affordability
- spend the configured NPC heal cost
- restore `CurrentHealth` to `GetEffectiveMaxHealth()`
- notify player-stat change
- emit successful completion once
- emit cancellation once from No Thanks or host Cancel

Use a controller-local `_operationInFlight` guard only for re-entrant synchronous activation. Successful Heal moves immediately to the terminal latch; failed validation clears the in-flight guard and leaves the screen open.

## Host integration

`NpcInteractionController` remains the sole Dialogue → Shop/Heal → interaction-complete orchestrator.

After Dialogue produces `OpenShop` or `Heal`:

1. Close the Dialogue host entry using the existing HPA-569 path.
2. Instantiate the corresponding authored scene.
3. Configure it before presentation with NPC/shop/player state.
4. Subscribe terminal signals.
5. Present it through `UIScreenHost` with the explicit policy below.
6. If presentation throws, unsubscribe/free the candidate, call `Finish()` so the domain latch cannot remain stuck, then preserve the existing exception behavior.
7. If the result is rejected, clean the candidate and `Finish()`.
8. If `TryPresent` returns `Opened` but `IsActive(handle)` is already false because a publication subscriber synchronously closed it, `Finish()` without retaining stale state.
9. Otherwise retain screen + handle.
10. On terminal screen signal, close the hosted entry, tolerate a post-cleanup publication exception as the current Dialogue path does, then converge on idempotent `Finish()`.

Use explicit Shop and Heal open/clear/close methods. Do not add a generic host facade merely to remove a few duplicated lines.

### Explicit screen policy

HPA-570 follows HPA-373 §7.3 for Shop and Healing HUD visibility. This intentionally differs from the current HPA-569 Dialogue implementation, which uses `Hud = Visible`. Do **not** silently propagate that Dialogue divergence into these two screens, and do not broaden HPA-570 into a Dialogue policy change.

Shop uses:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Shop,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    ExclusiveGroup = UIScreenExclusiveGroups.None,
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

Healing uses the same values with:

- `Kind = UIScreenKinds.Heal`
- `Cleanup = _ => ClearHealPresentation(screen)`
- `InitialFocus = () => screen.InitialFocusTarget`
- Cancel redirected to `HealingScreenController.RequestCancel()`

No `BlockingPrompt` exclusive group, parent handle, new incompatible kind, or host API is added.

`PauseTree = false` keeps the scene tree running while `BlockGameplayInput = true` makes the interaction modal. Configured Cancel is consumed at the hosted screen and cannot fall through to Pause.

### Exactly-once cleanup order

Keep the HPA-569 lifecycle details, not just its broad shape:

- cleanup callbacks unsubscribe screen signals and clear matching controller screen/handle state
- close methods tolerate `StaleHandle` by clearing matching local state
- terminal handlers attempt host close inside `try/catch`; by the time a close publication exception escapes, host cleanup has already committed, so the handler still calls `Finish()`
- `Finish()` sets `_finished = true` **before** cleanup to make re-entry a no-op, wraps close cleanup so publication exceptions cannot skip completion, and always invokes `InteractionComplete` once

Do not create a new lifecycle abstraction in this ticket.

## Legacy cleanup

Once equivalent hosted tests are green:

- delete `scripts/ui/ShopDialog.cs`
- delete `scripts/ui/HealDialog.cs`
- replace `ShopDialogTest` and `HealDialogTest` with screen-controller tests
- remove the now-unused legacy UI-parent dependency from `NpcInteractionController` and its production/test call sites
- retarget `tests/data/npc/ShopPricingTest.cs` comments/helper wording from `ShopDialog` to `ShopScreenController`; keep the existing formula helper rather than adding a pricing type
- remove or rewrite tests that document the retired native Shop/Heal cancel phase

No compatibility shim remains because there is no production caller for the native dialogs.

## Testing strategy

### Shop controller

Cover:

- pre-ready one-shot configuration
- centred SafeFrame + Large shell + no `AcceptDialog`
- successful Buy and list/gold refresh
- standing per-row `Not enough gold!` disabled reason
- inventory-full rollback with timed `Inventory full!`
- existing Sell formula, removal-before-gold, and last-item `Nothing to sell.`
- latest transient feedback timer wins without affecting standing row reasons
- local re-entrant activation ignored
- cancel/close exactly once
- tab + item-id focus restoration after rebuild

### Healing controller

Cover:

- pre-ready one-shot configuration
- centred SafeFrame + Small shell + Heal/No Thanks in `ActionsHost` + no `AcceptDialog`
- successful Heal spends gold, restores max HP, and completes once
- full HP disables Heal with `You are already at full health.` and focuses No Thanks
- insufficient gold disables Heal with `Not enough gold!` and focuses No Thanks
- no feedback timer is created
- Heal then cancel still emits only completion
- local re-entrant activation ignored

### NPC and production lifecycle

Replace the tests that currently assert native Shop/Heal children. Add production-route coverage proving:

- Dialogue closes before one hosted Shop/Heal entry opens
- Shop/Heal policy uses `PauseTree = false`, gameplay blocking, visible cursor, **hidden HUD**, inert lower layers, and consumed Cancel
- keyboard and gamepad Cancel close the real hosted Shop/Heal route without opening Pause
- `Finish()` while either hosted surface is active closes it and completes once
- invalid Shop data, host rejection, synchronous post-commit close, and close publication exceptions cannot leave stale handles or latch `GameManager.IsInNpcInteraction`

`ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler` must be deleted or rewritten because that native production phase no longer exists.

## Risks and mitigations

### Wrong sibling geometry gets copied

Mitigation: scene tests pin Shop to centred `Large` and Healing to centred `Small`. Dialogue's bottom band and `Full` width are explicitly forbidden here.

### Standing reason disappears with timed feedback

Mitigation: Shop row availability text and transient `%FeedbackLabel` have separate lifetimes. Healing has a standing non-timed label and no timer.

### Hosted entry can close during `TryPresent`

Mitigation: after an `Opened` result, verify `UIScreenHost.IsActive(handle)` before retaining screen/handle state.

### Host close publication can throw after cleanup committed

Mitigation: preserve current Dialogue clear/close/finish ordering so exactly-once domain completion still happens after publication failure.

### Shop refresh can leave stale focus

Mitigation: capture active tab + stable item id only; restore semantic focus or fall back to the next action/tab/Close. Do not retain queued node references as the restoration key.

### Scope can expand into a transaction framework

Mitigation: keep transaction code in the two concrete controllers. Revisit sharing only after another real screen demonstrates repeated behavior.

## Acceptance mapping

- **No desktop-window framing:** both native dialogs are deleted and replaced by authored `Control` + `SiriusModalShell` scenes.
- **Transactional parity:** current Buy, Sell, healing, pricing, rollback, cost, and completion semantics remain green under the new controllers.
- **Disabled reasons:** Buy affordability and Heal unavailability remain visible as standing text.
- **Input parity:** authored focus targets and host Cancel interception cover mouse/keyboard/gamepad.
- **Double activation:** controller-local in-flight guards protect synchronous Buy/Sell/Heal callbacks.
- **Lifecycle parity:** `NpcInteractionController` owns all host handles and completes exactly once on every terminal path.
