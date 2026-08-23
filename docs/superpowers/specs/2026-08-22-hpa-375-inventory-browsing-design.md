# HPA-375 Inventory Browsing Enhancements Design

**Status:** Planning candidate  
**Linear:** HPA-375 — Enhance Sirius inventory browsing with details, comparison, filters, and sorting  
**Date:** 2026-08-22

## 1. Decision

Enhance the existing HPA-357 Inventory screen in place. Keep one `InventoryMenuController`, one `InventoryMenu.tscn`, the existing `SiriusItemSlotController`, and the current `Character` / `Inventory` / `EquipmentSet` domain operations.

The feature adds four things only:

1. controller-local selected-item state that is separate from activation;
2. an authored Details page with supported item metadata, equipment comparison, and one contextual action;
3. category filtering plus Name / Category sorting with deterministic tie-breaks;
4. selection and focus reconciliation when the visible catalogue or inventory contents change.

The selected item, filter, and sort choice are screen-instance presentation state. They may survive normal refreshes and `UIScreenHost` detach/reattach while the same `InventoryMenuController` instance lives, but they are not written to save data or settings.

This draft PR is intended to become the **single HPA-375 PR**. It starts with the design and implementation plan; implementation commits should be added to this same branch after review rather than opening a second PR for the ticket.

## 2. Why HPA-375 is next

The required HPA-306 UI migration sequence is complete through HPA-359. HPA-306 leaves only two optional backlog enhancements, ordered as HPA-375 Inventory browsing followed by HPA-541 Reduced Motion. HPA-375's only implementation blocker, HPA-357, is complete.

HPA-375 therefore has a stable scene, controller, slot component, host lifecycle, and regression suite to extend without reopening the shared UI architecture.

## 3. Current-state facts that constrain the design

### The existing controller already owns the right domain handoff

`InventoryMenuController` already owns:

- current-entry catalogue binding;
- ordinal `DisplayName` ordering;
- equipment and accessory routing;
- equip swap and rollback behavior;
- unequip inventory-capacity rollback;
- consumable remove/apply/rollback ordering;
- battle-only consumable rejection;
- active-skill assignment;
- semantic focus restoration after catalogue mutation.

HPA-375 should route explicit actions through those existing paths rather than moving mutations into a new service, presenter, or view model.

### The slot component can select unsupported entries without changing it

`SiriusItemSlotController` is a `Button`. Its custom `Activated` signal is intentionally limited to `Available` / `Equipped`, but the normal `Pressed` signal still fires for focusable `Unsupported` entries because those buttons are not disabled.

HPA-375 can therefore:

- stop treating `Activated` as the Inventory screen's mutation command;
- subscribe the Inventory screen to each slot's normal `Pressed` signal;
- set `ToggleMode = true` for Inventory-owned item/equipment slot instances;
- use `ButtonPressed` only as the visual selection marker;
- leave `SiriusItemSlotController`, its enum, and other consumers unchanged.

### The current FocusSummary is not a selected-item model

`%FocusSummary` is a bounded 32–40 px focus/hover preview. HPA-357 deliberately made its state ephemeral.

Do not repurpose it into the persistent HPA-375 details surface. Keep it as the quick focus/hover summary and add a real Details page for persistent selection.

### The data needed by HPA-375 already exists

`Item` already exposes ID, display name, description, category, rarity, value, and asset path. `EquipmentItem` exposes slot plus ATK/DEF/SPD/HP bonuses. `ConsumableItem` exposes effect copy and the battle-only restriction. `UiArtCatalog` already maps item categories and equipment slots to fallback icons.

There is no equipment requirement model and no generic item-action model. The UI must not invent either one.

### Accessory targeting already has deterministic production semantics

`ResolveAccessoryEquipIndex()` currently chooses the first empty accessory slot and falls back to slot 0 when all four are full. Comparison and the eventual Equip action must call the same target-resolution rule so the preview cannot disagree with the mutation.

## 4. Alternatives considered

### A. Controller-owned browsing state + authored Details page — chosen

Add private browsing state and small helper methods to `InventoryMenuController`, plus a fourth `DetailsPage` in the current scene.

Benefits:

- reuses every existing mutation and focus seam;
- no new production abstraction;
- one owner for selection reconciliation and catalogue refresh;
- compact Details can occupy the existing paged area instead of squeezing the 640×360 layout;
- easiest path to preserve existing HPA-357 behavior and tests.

Cost: `InventoryMenuController` grows. That is acceptable for this one consumer; split it only if implementation produces a concrete independent responsibility that is hard to test in place.

### B. New `InventoryBrowserModel` / view model — rejected for now

A pure C# browser model could own selection/filter/sort/comparison, but there is no second consumer and the existing controller already owns all live item resolution and mutation handoff. It would duplicate catalogue identity, refresh semantics, and accessory targeting for testability we already get through the runtime-backed controller suite.

### C. Generic reusable item-details component or hosted modal — rejected

No second screen currently needs the same details/action contract. A reusable component or additional hosted screen would add lifecycle, focus, and API surface without evidence that it will be reused.

## 5. Selection model

Add a private semantic key; never retain a mutable `InventoryEntry` as selected state:

```csharp
private readonly record struct InventorySelectionKey(
    string? ItemId,
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex)
{
    public static InventorySelectionKey ForInventoryItem(string itemId) =>
        new(itemId, null, null);

    public static InventorySelectionKey ForEquipment(EquipmentSlotType slot) =>
        new(null, slot, null);

    public static InventorySelectionKey ForAccessory(int index) =>
        new(null, EquipmentSlotType.Accessory, index);
}
```

The key represents either:

- one current inventory item by stable item ID;
- one equipped main-slot item by slot;
- one equipped accessory item by accessory index.

Keep a refresh-scoped `Dictionary<string, InventoryEntry>` for **visible** catalogue entries alongside the existing slot maps. Every details refresh re-resolves live data from that map or from `Character.Equipment`.

### Selection rules

- Pressing a populated Inventory slot selects it and does **not** equip/use it.
- Pressing a populated equipment/accessory slot selects it and does **not** unequip it.
- Pressing an empty equipment/accessory slot leaves the current selection unchanged.
- Unsupported and battle-only inventory entries can still be selected so the Details page can explain why no action is available.
- Sorting preserves inventory selection by item ID.
- Filtering preserves selection only when the selected inventory item remains visible. If the filter hides it, clear selection and clear the pressed visual; equipped-item selection is unaffected by inventory filters.
- An ordinary refresh or reopen re-resolves the key. If the selected object no longer exists, clear it unless a mutation below provides an explicit replacement selection.

### Mutation reconciliation

- **Equip succeeds:** selection moves from inventory item ID to the resulting equipment slot; accessory selection uses the exact chosen accessory index.
- **Equip fails and rolls the item back:** retain the inventory-item selection.
- **Unequip succeeds:** selection moves to the returned inventory item ID.
- **Unequip fails because inventory return cannot be completed and the equipment rollback succeeds:** retain the equipped-slot selection.
- **Consumable use leaves quantity > 0:** retain the same item-ID selection and refresh quantity/details.
- **Final consumable is removed:** choose the next visible item at the previous visible catalogue index; if none exists choose the previous last visible item; if the filtered catalogue is empty, clear selection.

That fallback mirrors the semantic focus behavior already proven by HPA-357 without coupling selection to a `Control` instance.

## 6. Filtering and sorting

Add two controller-private enums:

```csharp
private enum InventoryFilter
{
    All,
    Equipment,
    Consumable,
    General,
    Quest
}

private enum InventorySort
{
    Name,
    Category
}
```

Author `%InventoryFilter` and `%InventorySort` `OptionButton`s above `%InventoryScroll`.

### Filter behavior

`All` shows every current entry. The other four values match the existing `ItemCategory` values exactly. Filtering is presentation-only; it never changes `Inventory` contents.

### Sort behavior

Use deterministic ordinal rules:

- **Name:** `DisplayName` ordinal → `Category` numeric value → `Id` ordinal.
- **Category:** `Category` numeric value → `DisplayName` ordinal → `Id` ordinal.

No ascending/descending toggle, custom ordering, rarity sorting, search box, favorites, or persisted browse preference is added in this ticket.

Filter/sort changes rebuild only the current visible slot set using the HPA-357 grow/reuse/shrink mechanism. Selection is reconciled after the rebuild; focus on the filter/sort control remains usable through the normal Godot focus model.

## 7. Details page

Add `%DetailsPage` as a third standard-layout column beside the existing Character and Items columns. In compact layout, Details becomes a fourth visibility page alongside Equipment / Items / Skills.

Suggested authored structure:

```text
DetailsPage
└── DetailsContent
    ├── DetailsTitleRow
    │   ├── DetailsIcon
    │   └── DetailsName
    ├── DetailsMeta
    ├── DetailsBody
    ├── DetailsComparison
    ├── DetailsActionReason
    └── DetailsActionButton
```

The existing outer `%PageScroll` remains the scroll owner. Do not add a nested modal or another screen host entry.

### Empty selection

Show neutral copy such as `Select an item or equipped slot to view details.` Hide the action button and comparison region.

### Inventory item details

Display only supported data:

- name;
- category;
- rarity;
- quantity;
- item art, falling back to existing category/equipment-slot glyphs;
- description when present.

For consumables also show `EffectDescription` and whether it is battle-only.

For equipment also show slot and the existing ATK/DEF/SPD/HP bonus values. There are no equipment requirements in the current domain, so no requirement row is authored.

### Equipped item details

Show the same available item metadata plus the concrete equipped slot (`Weapon`, `Accessory 2`, etc.). The contextual action becomes Unequip.

### Active skill information

Keep the HPA-357 Skills page and `ActiveSkillSelector` as the active-skill assignment UI. It already shows skill description, mana cost, period, current assignment, and explicit None. Do not force skills into the item selection key; skills are catalog IDs, not `Item` objects.

## 8. Equipment comparison

Only an **inventory equipment selection** produces replacement comparison.

Resolve the target exactly as Equip will:

- non-accessory: `EquipmentItem.SlotType`;
- accessory: `ResolveAccessoryEquipIndex()` — first empty, otherwise slot 0.

Read the currently equipped target item, if any, and render:

- target slot;
- `Will fill <slot>` when empty or `Will replace <item> in <slot>` when occupied;
- ATK, DEF, SPD, and HP delta for the candidate versus the target item.

Always show all four deltas so gains, losses, and unchanged values are unambiguous. A zero delta is presented as unchanged rather than omitted.

Use text/sign semantics only; do not add new Theme tokens or stat-comparison components for one screen.

The comparison is advisory presentation. The existing equip operation remains authoritative and may still fail; HPA-375 does not add reservation or preflight protocols.

## 9. Contextual actions

Author exactly one `%DetailsActionButton`. Its label and behavior come from the current selection:

| Selection | Action | Behavior |
| --- | --- | --- |
| Inventory `EquipmentItem` | Equip | Existing `EquipFromInventory` path |
| Equipped item | Unequip | Existing `HandleUnequip` path |
| Inventory non-battle `ConsumableItem` | Use | Existing `UseConsumableOutOfBattle` path |
| Battle-only consumable | none | Explain `Can only be used in battle.` |
| General / Quest / unsupported item | none | Explain that there is no supported inventory action |
| No selection | none | Neutral selection prompt |

The action button is hidden or disabled when no supported action exists. Do not add confirmation dialogs for existing one-press actions.

`Assign Active Skill` remains the current Skills-page selector behavior rather than becoming an item-details action.

No Drop, Sell, Favourite, Lock, bulk operation, or new item-domain action is introduced.

## 10. Responsive layout and navigation

Extend the existing private `InventoryPage` enum to:

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills,
    Details
}
```

### Standard

At non-compact viewports show:

- CharacterColumn (Equipment + Skills);
- ItemsPage;
- DetailsPage.

Keep `%FocusSummary` and the global Close footer as they are.

### Compact

Add `%DetailsTab` to the existing `ButtonGroup`. Show exactly one of Equipment / Items / Skills / Details. Update raw LB/RB cycling from modulus 3 to modulus 4.

When a slot is selected in compact mode:

1. update the semantic selection;
2. show Details;
3. focus `%DetailsActionButton` when a supported action exists;
4. otherwise focus `%DetailsTab`, keeping LB/RB and normal navigation available.

Returning to Items/Equipment shows the selected slot using the button's pressed state.

Extend the existing focus-page resolution/restoration code only for the new Details page. Do not replace it with a generic navigation graph.

## 11. File ownership

### Modify

- `scenes/ui/InventoryMenu.tscn` — Details page, filter/sort controls, fourth compact tab.
- `scripts/ui/InventoryMenuController.cs` — selection/filter/sort/details/comparison/action orchestration and reconciliation.
- `tests/ui/InventoryMenuControllerTest.cs` — selection, details, filters/sorts, comparison, contextual action, rollback, mutation reconciliation.
- `tests/ui/InventoryMenuSceneTest.cs` — four-page compact behavior, standard Details layout, viewport/focus/input coverage.

### Audit only unless a real stale assertion is found

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs` — existing Inventory icon/heading smoke should remain valid.
- `tests/game/GameplayPauseHostTest.cs` — HPA-375 does not change host policy or screen lifetime.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — update only if it explicitly claims slot press immediately mutates inventory/equipment.

### No production changes expected

- `scripts/data/Item.cs`
- `scripts/data/Character.cs`
- `scripts/data/Inventory.cs`
- `scripts/data/EquipmentSet.cs`
- `scripts/data/consumables/ConsumableItem.cs`
- `scripts/ui/components/SiriusItemSlotController.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- save/settings data
- shared Theme/art/metric contracts

## 12. Test strategy

Use the existing runtime-backed Inventory suites as the deterministic owner.

### Controller behavior

Cover:

- pressing an inventory equipment/consumable no longer mutates before Details action;
- pressing a populated equipped slot no longer unequips before Details action;
- unsupported/battle-only entries are selectable and explain the unavailable action;
- name/category/quantity/rarity/description/effect/slot data uses current domain values;
- Name and Category sort orders, including deterministic ties;
- every category filter and All;
- selection survives sorting and visible refresh;
- selection clears when filtered out or externally removed;
- equipment comparison with occupied and empty main slots;
- accessory comparison uses first-empty then slot-0 replacement targeting;
- comparison renders positive, negative, and zero deltas;
- Equip/Unequip/Use action paths preserve all existing rollback/domain behavior;
- selection transitions after equip/unequip/use exactly as defined above;
- active-skill selector behavior remains unchanged.

### Scene/input behavior

Cover:

- standard layout includes Details without breaking SafeFrame containment;
- compact layout has exactly four pages and exactly one visible at a time;
- LB/RB cycles through all four while paused;
- selecting by Button press opens Details in compact and leaves readable/focusable state;
- keyboard/D-pad can reach filter, sort, item selection, Details action, and Close through actual input outcomes;
- mouse/Button press selects without mutation and the explicit action button performs the mutation;
- existing primary and minimum verification viewports remain bounded.

Do not add a new E2E harness or screenshot framework.

## 13. Risks and mitigations

### Selection accidentally holds stale `InventoryEntry`

Mitigation: selection stores only semantic identity; details/action resolution always uses the current refresh map or live equipment.

### Comparison and Equip target different accessory slots

Mitigation: both call the existing `ResolveAccessoryEquipIndex()` rule immediately before presenting/acting.

### Compact screen becomes vertically crowded

Mitigation: Details is a fourth page inside the existing outer `PageScroll`; it is not appended below the 112 px identity strip and global footer.

### Filter/sort rebuild loses focus or selection

Mitigation: keep current semantic focus restoration; reconcile selection by item ID after rebuilding; do not retain slot controls as selected state.

### Controller size grows

Mitigation: keep helpers private and feature-local. Do not extract a browser model pre-emptively; reassess only if implementation reveals an independently testable responsibility with another consumer or a concrete maintenance problem.

## 14. YAGNI review

Explicitly excluded:

- new inventory/view-model/service/repository layers;
- generic item-details component;
- new host kind or modal;
- persisted selection/filter/sort settings;
- search text;
- ascending/descending or rarity/value sort;
- colored comparison framework;
- equipment requirements that do not exist in domain data;
- Drop/Sell/Favourite/Lock/bulk actions;
- inventory/save-domain changes;
- speculative compatibility or migration work.

The result is one focused extension of the screen HPA-357 already established, with new behavior living beside the code that already owns its mutations and focus lifecycle.
