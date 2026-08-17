# HPA-570 Hosted Shop and Healing Design

**Issue:** HPA-570  
**Status:** Proposed  
**Date:** 2026-08-16

## Context

HPA-570 is the next actionable HPA-358 secondary-screen migration after HPA-569. Its host prerequisite, HPA-382, is complete, and HPA-569 has established the production pattern for scene-authored NPC surfaces presented through the gameplay `UIScreenHost`.

The remaining Shop and Healing paths still use runtime-built Godot `AcceptDialog` windows:

- `ShopDialog` owns Buy/Sell presentation plus the existing transaction callbacks.
- `HealDialog` owns healing presentation plus the existing heal callback.
- `NpcInteractionController` closes hosted Dialogue, creates one of those native dialogs under a legacy UI parent, then finishes the NPC interaction when the dialog terminates.

HPA-373 already defines Shop and Healing as in-game Sirius surfaces rather than desktop windows. HPA-570 should migrate that presentation without redesigning the transaction model.

## Goals

- Replace `ShopDialog` and `HealDialog` with scene-authored Sirius surfaces.
- Present both surfaces through the existing gameplay `UIScreenHost` using `UIScreenKinds.Shop` and `UIScreenKinds.Heal`.
- Preserve current Shop Buy and Sell rules, prices, inventory mutation, gold mutation, rollback, feedback, and close behavior.
- Preserve current Healing cost, availability, full-heal effect, gold mutation, completion, and cancellation behavior.
- Explain disabled actions such as insufficient gold and healing at full HP.
- Keep keyboard, gamepad, and mouse focus deterministic.
- Make every terminal path finish the NPC interaction exactly once, including cancel, invalid data, host rejection, exceptions, and teardown.

## Non-goals

- New shop stock rules, buyback, bargaining, quantity pickers, bulk transactions, or new pricing rules.
- New healing rules, status recovery, party healing, or stat-system changes.
- A generic transaction framework, base transaction controller, presenter/view-model layer, or navigation service.
- Dialogue, Item Box, Pokémon Summary, puzzle, reward, or error redesign.
- New theme tokens or art requirements.
- Broad HPA-625 legacy-widget cleanup.

## Scope clarification: existing Sell behavior stays

HPA-570 currently lists “Selling” under Out of scope, but that conflicts with two shipped baselines:

1. `ShopDialog` already exposes a Sell tab and sells one item for `max(1, floor(Item.Value * 0.5))`.
2. HPA-373 §9.9 explicitly requires Buy and Sell tabs and says existing sale behavior remains unchanged.

For this migration, “Selling” is interpreted as **no new selling mechanics**. Removing the existing Sell flow would be a gameplay regression, not a simplification. The migration therefore preserves the current Sell tab and pricing exactly, while adding no new sell behavior.

## Design decision

Use **two separate scene/controller pairs** and reuse only the existing host and Sirius presentation primitives:

- `ShopScreen.tscn` + `ShopScreenController`
- `HealingScreen.tscn` + `HealingScreenController`

Do not introduce a shared transaction controller. The two screens share visual language and host policy, but their state machines are different: Shop is a reusable catalogue that remains open across many buy/sell operations; Healing is a small one-shot confirmation that terminates after a successful heal or cancellation.

This is less code and easier to maintain than forcing both flows through configurable transaction abstractions.

## Shop surface

### Scene structure

`ShopScreen.tscn` is a full-viewport `Control` containing a safe-frame-owned `SiriusModalShell`.

The authored shell body contains:

- merchant/title identity
- player gold label
- feedback label
- `TabContainer` with Buy and Sell pages
- scrollable/dynamic Buy list container
- scrollable/dynamic Sell list container
- Close action

Use the existing `SiriusModalShell`; do not add a new shell or shop-specific frame component. Dynamic catalogue rows remain controller-created because their count and item data are runtime state. They use existing Sirius button/text theme variations rather than introducing a new row class before reuse exists.

At standard widths, use the existing large/full modal sizing inside the safe frame. At compact widths, use the same scene and shell in compact mode; the active Buy or Sell tab owns the list page. Do not create a second compact controller or duplicate scene.

### Transaction ownership

`ShopScreenController` receives the existing `ShopInventory` and `Character` and performs the same operations currently in `ShopDialog`:

- Buy price remains `Item.Value`.
- Buy uses `Character.TrySpendGold` and `Character.TryAddItem`.
- If add fails, restore the spent gold and show `Inventory full!`.
- Sell price remains `max(1, floor(Item.Value * 0.5))`.
- Sell uses `Character.TryRemoveItem`, then `GainGold`.
- Successful mutations continue to notify `GameManager.Instance?.NotifyPlayerStatsChanged()`.
- Missing catalogue entries remain warnings/errors followed by a safe refresh, never invented substitute items.

No transaction service is extracted. `Character` and the existing catalogues remain the domain APIs.

### Disabled reasons and feedback

A disabled Buy action shows a readable affordability reason adjacent to the row or in the screen feedback/detail area; it is not communicated only by disabled styling. Inventory-full remains operation feedback because capacity can change between render and activation.

The current two-second Shop feedback lifetime is preserved. Replacing one feedback message cancels the prior timer so an older timeout cannot hide a newer message.

Sell empty state remains `Nothing to sell.`.

### Double activation

Add one controller-local `_operationInFlight` guard around each synchronous buy/sell callback. It prevents re-entrant activation of the same operation while it is executing, then resets after refresh/feedback. It does not prevent the player from intentionally buying or selling another unit after the first operation completes.

`ShopClosed` remains exactly-once through a terminal close latch.

## Healing surface

### Scene structure

`HealingScreen.tscn` is a full-viewport `Control` containing a small `SiriusModalShell` with:

- NPC/title identity
- current/max HP
- heal cost
- available gold
- feedback/disabled reason
- Heal primary action
- No Thanks secondary action

The controller uses the same scene at standard and compact sizes and only adjusts shell compact presentation/target sizing. No second layout controller is introduced.

### Healing behavior

`HealingScreenController` preserves the current `HealDialog` semantics:

- warn when configured heal cost is non-positive, but do not invent a new rule
- disable Heal when HP is already full
- disable Heal when gold is insufficient
- on activation, re-check HP and affordability
- spend the configured NPC heal cost
- restore `CurrentHealth` to `GetEffectiveMaxHealth()`
- notify player-stat change
- emit successful completion once
- emit cancellation once from No Thanks or host Cancel

A controller-local `_operationInFlight` guard prevents re-entrant Heal activation. Successful Heal transitions immediately to the terminal latch; failed validation clears the in-flight guard and leaves the surface open with readable feedback.

## Host integration

`NpcInteractionController` remains the sole Dialogue → Shop/Heal → interaction-complete orchestrator.

After Dialogue produces `OpenShop` or `Heal`:

1. Close the Dialogue host entry using the existing HPA-569 path.
2. Instantiate the corresponding authored scene.
3. Configure it before presentation with NPC/shop/player state.
4. Present it through the existing `UIScreenHost`.
5. Retain the screen + `UIScreenHandle` only if the returned handle is still active.
6. On screen completion/cancel, close the host entry, clear signal subscriptions/state, then call `Finish()`.

Use explicit Shop and Heal open/clear/close methods. Do not add a generic host facade merely to remove a small amount of duplicated orchestration code.

Both entries use the existing modal gameplay policy shape:

- `Layer = Modal`
- modal input priority
- `ProcessPolicy = Always`
- no scene-tree pause
- block gameplay input
- visible cursor
- current HUD policy preserved
- lower layers visible but inert
- Cancel consumed and redirected to the screen's cancel request
- initial focus supplied by the controller
- `QueueFree` node lifetime

`UIScreenKinds.Shop` and `UIScreenKinds.Heal` already exist; no new host kind or exclusive group is needed.

## Legacy cleanup

Once equivalent hosted tests are green:

- delete `scripts/ui/ShopDialog.cs`
- delete `scripts/ui/HealDialog.cs`
- replace `ShopDialogTest` and `HealDialogTest` with screen-controller tests
- remove the now-unused legacy UI-parent dependency from `NpcInteractionController` and its production/test call sites

No compatibility shim is kept because there is no remaining production caller for the native dialogs.

## Testing strategy

### Controller behavior

Shop tests cover:

- authored scene can be configured before `_Ready()`
- successful Buy updates gold/inventory and refreshes lists
- insufficient gold disables Buy and exposes a reason
- inventory-full Buy rolls gold back and shows feedback
- existing Sell pricing and last-item empty state remain unchanged
- latest feedback timer wins
- cancel/close emits once
- local re-entrant activation is ignored

Healing tests cover:

- successful Heal spends gold, restores max HP, and completes once
- full HP disables Heal with a readable reason
- insufficient gold disables Heal with a readable reason
- cancel emits once
- Heal followed by cancel still completes only once
- local re-entrant activation is ignored

### NPC lifecycle

`NpcInteractionControllerTest` moves from native-child assertions to host-entry assertions:

- Shop outcome closes Dialogue before one `UIScreenKinds.Shop` entry opens
- Heal outcome closes Dialogue before one `UIScreenKinds.Heal` entry opens
- cancel closes the hosted surface and completes once
- successful healing completes once
- `Finish()` while Shop/Heal is active closes the hosted entry
- invalid Shop data, host rejection, and post-commit publication failure cannot leave stale handles or a latched NPC interaction

Update the existing gameplay host/lifecycle tests that were intentionally left on the native Shop/Heal phase by HPA-569.

## Risks and mitigations

### Native-dialog behavior can disappear during visual migration

Mitigation: port the existing focused Shop/Heal tests first, then delete the native classes only after the new controllers pass equivalent behavior tests.

### Hosted entry can close during `TryPresent`

Mitigation: copy the proven HPA-569 contract: after an `Opened` result, verify `UIScreenHost.IsActive(handle)` before retaining controller state. Cleanup callbacks own signal removal and stale-state clearing.

### Shop refresh can leave stale focus after a transaction

Mitigation: after rebuilding a list, restore focus by stable item id when that action still exists; otherwise choose the next available action, then the tab/Close fallback. This is ephemeral in-instance focus only, not persisted selection state.

### A disabled control can hide why it is disabled

Mitigation: compute and display a textual reason from current HP/gold state whenever Heal/Buy is disabled.

### Scope can expand into a transaction framework

Mitigation: keep transaction code in the two concrete controllers. Revisit sharing only after another real screen demonstrates repeated behavior rather than repeated visual primitives.

## Acceptance mapping

- **No desktop-window framing:** both native dialogs are deleted and replaced by authored `Control` + `SiriusModalShell` scenes.
- **Transactional parity:** current Buy, Sell, healing, pricing, rollback, cost, and completion tests remain green under the new controllers.
- **Disabled reasons:** Buy affordability and Heal unavailability are visible as text.
- **Input parity:** authored focus targets, host Cancel interception, and standard Sirius controls support mouse/keyboard/gamepad.
- **Double activation:** controller-local in-flight guards protect Buy/Sell/Heal callbacks.
- **Lifecycle parity:** `NpcInteractionController` owns all host handles and completes exactly once on every terminal path.
