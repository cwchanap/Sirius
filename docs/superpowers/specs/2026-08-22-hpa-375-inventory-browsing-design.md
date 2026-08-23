# HPA-375 Inventory Browsing Enhancements Design

**Status:** Planning candidate  
**Linear:** HPA-375 — Enhance Sirius inventory browsing with details, comparison, filters, and sorting  
**Date:** 2026-08-22

## 1. Decision

Enhance the existing HPA-357 Inventory screen in place. Keep one `InventoryMenuController`, one `InventoryMenu.tscn`, the existing `SiriusItemSlotController`, and the current `Character` / `Inventory` / `EquipmentSet` domain operations.

The feature adds four things only:

1. persistent screen-instance selection that is separate from activation;
2. an authored Details page with supported metadata, equipment comparison, and one contextual action;
3. category filtering plus Name / Category sorting with deterministic tie-breaks;
4. selection/focus reconciliation when the visible catalogue or inventory contents change.

Selection, filter, and sort are presentation-only. They may survive normal refresh and `UIScreenHost` detach/reattach while the same controller instance lives, but they are not persisted to save/settings data.

This branch remains the **single HPA-375 PR**: implementation commits belong here after planning review.

### Product requirement that stays locked

HPA-375 explicitly requires an item-selection state independent from immediately equipping or consuming, and its first acceptance criterion requires selection not to mutate inventory/equipment. A focus-only Details subject while keeping one-press equip/use would violate the ticket rather than simplify its implementation.

Therefore:

- slot press becomes selection only;
- Equip / Unequip / Use happen only through `%DetailsActionButton`;
- the implementation must handle the focus/selection consequences of that product decision rather than removing the decision.

## 2. Why this stays in the existing controller

`InventoryMenuController` already owns the required seams:

- dynamic current-entry catalogue binding;
- ordinal item ordering;
- equipment/accessory routing;
- equip swap and rollback;
- unequip inventory-capacity rollback;
- consumable remove/apply/rollback ordering;
- battle-only rejection;
- active-skill assignment;
- semantic focus restoration after catalogue mutation.

HPA-375 extends those seams instead of adding an inventory browser/view-model/service/repository layer.

`SiriusItemSlotController` remains unchanged. Its custom `Activated` signal still serves other consumers, while normal `Button.Pressed` can select focusable `Unsupported` entries. Inventory changes which signal it consumes; the shared component does not gain another API.

## 3. Reuse decisions

The implementation must reuse current Inventory identity and refresh machinery rather than create parallel copies.

### Shared semantic identity

Rename the existing private `InventoryFocusKey` to describe its wider responsibility:

```csharp
private readonly record struct InventorySemanticKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId)
{
    public static InventorySemanticKey ForEquipment(EquipmentSlotType slot) =>
        new(slot, null, null);

    public static InventorySemanticKey ForAccessory(int index) =>
        new(EquipmentSlotType.Accessory, index, null);

    public static InventorySemanticKey ForItem(string itemId) =>
        new(null, null, itemId);
}
```

Use the same key type for selected identity and the existing pending focus record:

```csharp
private InventorySemanticKey? _selection;

private readonly record struct PendingFocusRestore(
    InventorySemanticKey Preferred,
    int PreviousCatalogueIndex)
{
    public PendingFocusRestore WithPreferred(InventorySemanticKey preferred) =>
        this with { Preferred = preferred };
}
```

Selection and current focus remain separate state. An item can stay selected while `%InventorySort` owns focus. Sharing the key type only prevents duplicate representations of item/equipment identity.

### Reuse the current catalogue maps

Do **not** add another `Dictionary<string, InventoryEntry>`.

Use the existing refresh-scoped maps:

```csharp
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot;
private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId;
```

Resolve a selected inventory item by ID through `_inventorySlotByItemId`, then read the current `InventoryEntry` from `_inventoryEntryBySlot`. Never retain a mutable `InventoryEntry` across refresh.

### Reuse the existing pending fallback index

Do **not** add `_pendingSelectionFallbackIndex`.

`PendingFocusRestore.PreviousCatalogueIndex` already carries the visible catalogue position needed when a selected item disappears after a mutation. Successful mutation reconciliation updates the pending preferred key before the existing `RestorePendingFocus()` runs.

## 4. Selection behavior

Pressing a slot selects; it never mutates inventory/equipment.

- Populated inventory item → select by item ID.
- Populated main equipment slot → select by slot type.
- Populated accessory slot → select by accessory index.
- Empty equipment/accessory slot → keep current selection unchanged.
- Unsupported/battle-only inventory entry → selectable so Details can explain why no action exists.
- Sort → preserve selected inventory item by item ID.
- Filter → preserve selection only if that inventory item remains visible; otherwise clear it. Equipped selection is unaffected by item filters.
- External refresh/removal → clear a selected key that no longer resolves unless a successful consumable removal has an explicit visible-index fallback.

### Selection visual ownership

Inventory-owned slots use `ToggleMode = true`, but Godot's automatic toggle state is **not** authoritative. `_selection` is authoritative.

`RefreshSelectionVisuals()` is the only normalization writer and runs:

- after every Inventory refresh;
- at the end of every `SelectInventorySlot(...)` handler;
- at the end of every `SelectEquipmentSlot(...)` handler, including empty-slot presses.

This explicitly repairs both automatic-toggle edge cases:

1. pressing the already-selected slot cannot visually toggle it off while `_selection` remains set;
2. pressing an empty equipment slot cannot leave that empty slot visually pressed while selection remains elsewhere.

## 5. Details surface

`%FocusSummary` remains the bounded HPA-357 focus/hover preview. It is not promoted into selected-item state.

Add `%DetailsPage` to the existing screen:

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

Reuse the current outer `%PageScroll`; do not add a modal, host entry, or reusable details component.

Display only fields supported by current domain data:

- name;
- category;
- rarity;
- quantity for inventory entries;
- description when present;
- consumable `EffectDescription` and battle-only restriction;
- equipment slot and ATK/DEF/SPD/HP bonuses;
- concrete equipped slot identity (`Weapon`, `Accessory 2`, etc.);
- item art, falling back through existing `UiArtCatalog`/`UiIconPresenter` category or equipment-slot glyphs.

There is no equipment-requirement model; do not invent one.

Skills remain on the existing Skills page and `ActiveSkillSelector`. They are skill IDs, not Items, and do not join the item selection key.

## 6. Tooltip copy after selection replaces activation

Current inventory tooltips end in `Click to equip` / `Click to use`. Those become false once slot press only selects.

Replace the interaction sentence with neutral copy:

```text
Select to view details
```

Keep useful description/effect/battle-only information. Do not leave player-facing copy claiming slot press directly equips or consumes.

## 7. Contextual action and mutation reconciliation

Author exactly one `%DetailsActionButton`:

| Selection | Action | Existing path |
| --- | --- | --- |
| Inventory `EquipmentItem` | Equip | `EquipFromInventory` |
| Equipped item | Unequip | `HandleUnequip` |
| Inventory non-battle `ConsumableItem` | Use | `UseConsumableOutOfBattle` |
| Battle-only consumable | none | explain battle-only restriction |
| General / Quest / unsupported | none | explain no supported inventory action |
| None | none | neutral Details prompt |

No confirmation is added for existing one-press actions.

### Successful mutation focus rule

A successful mutation must never leave focus on `%DetailsActionButton` after its meaning changes.

Populate `_pendingFocusRestore` with the resulting semantic key and call the existing `RefreshUI()` path:

- Equip → resulting main equipment slot or exact accessory index.
- Unequip → returned inventory item ID.
- Use with remaining quantity → same inventory item ID and previous visible index.
- Use of final quantity → use `PreviousCatalogueIndex` to select/focus the item now occupying that visible position, or the previous last visible item, or clear when none remain.

`RestorePendingFocus()` remains the page/focus authority. In compact mode it switches to Items/Equipment before grabbing the resulting slot; in standard mode it grabs the visible target directly.

Failed mutations do not change selection/action meaning and do not create a mutation pending restore.

### Mutation result hidden by active filter

A successful domain mutation does not override the user's filter just to reveal its result.

If a resulting **inventory item** is not present in the current filtered catalogue:

- keep the current filter unchanged;
- clear `_selection` rather than retaining an invisible actionable subject;
- leave `_pendingFocusRestore` to fall through the existing visible-item/page/tab/Close focus fallback.

Example: with `Consumable` filter active, select the equipped Weapon and choose Unequip. The sword returns to Inventory, but because it is hidden by the active filter, Details clears instead of describing/acting on an invisible sword.

This rule does not affect Equip, whose resulting equipment slot remains independently visible, or a consumable that remains in a filter which already contains it.

## 8. Filtering and sorting

Add two private enums and two authored `OptionButton`s:

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

`%InventoryFilter` and `%InventorySort` live above `%InventoryScroll`.

### Metadata-backed options

Do not derive enum values by casting `ItemSelected` indices.

When each option is authored in `_Ready()`, store the enum numeric value in item metadata, matching the screen's existing `ActiveSkillSelector` metadata pattern. Each handler reads and validates metadata, then updates the private filter/sort field.

Reordering display labels later must not silently change behavior.

### Filtering

- `All` → every entry.
- Other values → exact existing `ItemCategory` match.
- Presentation only; never mutate Inventory.

### Sorting

- Name → `DisplayName` ordinal → `Category` numeric → `Id` ordinal.
- Category → `Category` numeric → `DisplayName` ordinal → `Id` ordinal.

Keep comparison logic as a private pure helper rather than reading `_inventorySort` implicitly:

```csharp
private static int CompareVisibleEntries(
    InventoryEntry left,
    InventoryEntry right,
    InventorySort sort);
```

The caller passes `_inventorySort` into `entries.Sort(...)`. Do not widen the helper's visibility only for tests; behavior remains asserted through the existing controller surface.

No search, asc/desc toggle, rarity/value sort, custom category order, or persisted browse preference.

## 9. Equipment comparison

Only a selected **inventory `EquipmentItem`** shows replacement comparison.

Resolve the target exactly as Equip will:

- non-accessory → `EquipmentItem.SlotType`;
- accessory → `ResolveAccessoryEquipIndex()` (first empty, otherwise slot 0).

Show target outcome plus ATK/DEF/SPD/HP delta against the item currently occupying that target, if any.

Keep the bonus arithmetic pure and private:

```csharp
private static (int Attack, int Defense, int Speed, int Health)
    CompareEquipmentBonuses(EquipmentItem candidate, EquipmentItem? occupant);
```

Format every stat; do not reuse `GetBonusText()` because that helper intentionally omits zero values:

```csharp
private static string FormatDelta(string label, int delta) => delta switch
{
    > 0 => $"{label} +{delta}",
    < 0 => $"{label} {delta}",
    _ => $"{label} unchanged"
};
```

Examples:

```text
ATK +2
DEF -1
SPD unchanged
HP unchanged
```

Comparison is advisory presentation only. The existing equip operation remains authoritative.

## 10. Responsive layout and navigation

Extend the existing page enum:

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

Show CharacterColumn (Equipment + Skills), ItemsPage, and DetailsPage together. Keep `%FocusSummary` and global Close footer.

The current 1024×768 verification viewport is standard, but three equal-width children would make the six-column 56 px item grid horizontally scroll. Preserve useful catalogue width by giving `%ItemsPage` a larger authored stretch ratio:

```text
CharacterColumn = 1.0
ItemsPage       = 1.5
DetailsPage     = 1.0
```

No new theme metric is needed. At 1024×768, the existing viewport test must prove `%InventoryGrid`'s combined minimum width fits within `%InventoryScroll` without horizontal scrolling.

Selecting a slot updates Details immediately because Details is already visible; selection does not steal focus.

### Compact

Add `%DetailsTab` to the current `ButtonGroup`; raw LB/RB cycles modulo 4.

Show exactly one of Equipment / Items / Skills / Details. Make the visibility rule explicit:

```csharp
_characterColumn.Visible =
    _activeCompactPage is InventoryPage.Equipment or InventoryPage.Skills;
_equipmentPage.Visible = _activeCompactPage == InventoryPage.Equipment;
_skillsPage.Visible = _activeCompactPage == InventoryPage.Skills;
_itemsPage.Visible = _activeCompactPage == InventoryPage.Items;
_detailsPage.Visible = _activeCompactPage == InventoryPage.Details;
```

**Do not auto-jump to Details when a slot is selected.** Slot press updates semantic selection/pressed visuals while the user stays on Equipment/Items. Details opens explicitly through `%DetailsTab` or LB/RB.

When Details opens:

- focus `%DetailsActionButton` when a supported action exists;
- otherwise focus `%DetailsTab`.

After a successful mutation, `RestorePendingFocus()` switches compact back to the resulting Equipment/Items page and focuses the resulting slot/fallback.

### Focus-page mapping

Extend `ResolveFocusPage`, `RestoreFocusForPage`, and `RestoreCompactPageFocus` narrowly:

- `%InventoryFilter` / `%InventorySort` → Items;
- inventory slots / `%ItemsTab` → Items;
- `%DetailsTab` / `%DetailsActionButton` → Details;
- equipment/accessory slots / `%EquipmentTab` → Equipment;
- `ActiveSkillSelector` / `%SkillsTab` → Skills.

A focused filter/sort/action must not become hidden during a standard↔compact breakpoint change.

Do not build a generic navigation graph.

## 11. Implementation review boundaries

The implementation plan uses two separate reviewable Task 1 gates without adding a second PR:

### Task 1A — foundation only

- rename `InventoryFocusKey` → `InventorySemanticKey`;
- author `%DetailsPage` / `%DetailsTab`;
- extend four-page visibility/focus scaffolding;
- add standard width ratio and compact geometry tests;
- **do not change current slot activation semantics yet**.

This is a green structural checkpoint and introduces no temporary focus-driven Details behavior.

### Task 1B — atomic behavior cutover

- change Inventory-owned slot presses to selection;
- normalize `ButtonPressed` from `_selection` after every select handler/refresh;
- render Details from selected semantic identity;
- update tooltip verbs;
- route Equip/Unequip/Use through `%DetailsActionButton`;
- migrate existing direct-activation parity tests;
- add post-mutation focus reconciliation.

The press cutover and action replacement land together so there is no intermediate state where normal Inventory actions disappear or selection immediately mutates.

Do **not** implement a temporary focus-driven Details subject only to replace it in Task 1B.

## 12. File ownership

### Modify

- `scenes/ui/InventoryMenu.tscn`
- `scripts/ui/InventoryMenuController.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Audit only unless actually stale

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### No production changes expected

- item/character/inventory/equipment domain files;
- `SiriusItemSlotController`;
- `UIScreenHost`;
- save/settings data;
- shared Theme/art/metric contracts.

## 13. Required regression coverage

### Controller

- slot press selects without Equip/Unequip/Use;
- double-press selected slot remains visually selected;
- pressing an empty equipment slot cannot steal the pressed visual;
- unsupported/battle-only entries select and explain missing action;
- tooltip interaction copy says selection, not direct mutation;
- details use current name/category/rarity/quantity/description/effect/slot data;
- all filters and both sort modes, including ID tie-break;
- filter/sort values are read from OptionButton metadata rather than index casts;
- sorting preserves selection by ID;
- filtering out selected inventory item clears selection;
- successful mutation whose inventory result is hidden by the current filter clears selection without changing the filter;
- equipment comparison covers occupied, empty, first-empty accessory, and full-accessory slot-0 target;
- comparison pins positive, negative, and `unchanged` zero copy;
- Details action preserves current equip/unequip/use rollback ordering;
- successful Equip/Unequip/Use never leaves focus on an inverted Details action;
- final consumable selection/focus fallback uses the existing pending previous index;
- active-skill behavior remains unchanged.

### Scene/input

- standard layout contains Details inside SafeFrame;
- 1024×768 catalogue has no horizontal item-grid scroll with the authored 1:1.5:1 column ratios;
- compact has exactly four pages and one visible page, including `%DetailsPage` in `VisiblePageCount()`;
- Details compact page hides `%CharacterColumn` completely;
- LB/RB cycles all four while paused;
- compact slot selection does **not** auto-switch pages;
- selected slot remains visually selected when returning from Details;
- successful compact mutation switches/focuses the resulting Equipment/Items slot through existing restore logic;
- filter/sort focus survives compact breakpoint changes as Items-page focus;
- Details action/tab focus survives compact breakpoint changes as Details-page focus;
- keyboard/D-pad reaches filter, sort, slots, Details action, and Close using actual input outcomes;
- current verification viewports remain bounded.

No new E2E or screenshot framework.

## 14. Risks and mitigations

### Stale selected entry

Store only `InventorySemanticKey`; resolve through current slot maps/live equipment after every refresh.

### Filter hides a mutation result

Keep the filter, clear selection, and let existing pending focus fallback choose a visible control. Never retain an invisible actionable Details subject.

### ToggleMode creates false pressed state

Normalize pressed visuals from `_selection` after each selection handler and refresh.

### Selection/focus reconcilers drift

Use one semantic key type and the existing `PendingFocusRestore.PreviousCatalogueIndex`; do not add parallel selection fallback state.

### Inverted action double-confirm

Every successful mutation routes focus away from `%DetailsActionButton` before another activation can occur.

### Accessory preview disagrees with Equip

Comparison and Equip both call `ResolveAccessoryEquipIndex()`.

### Standard three-column layout squeezes catalogue

Give Items a 1.5 stretch ratio and pin 1024×768 no-horizontal-scroll behavior.

### Breakpoint hides focused new chrome

`ResolveFocusPage` explicitly maps filter/sort and Details controls.

## 15. YAGNI boundary

Explicitly excluded:

- browser/view-model/service/repository layers;
- generic item-details component shared with Battle;
- temporary focus-driven Details implementation;
- new host kind/modal;
- persisted selection/filter/sort;
- search or extra sort modes;
- colored comparison framework;
- public/internal test-only comparator APIs;
- nonexistent equipment requirements;
- Drop/Sell/Favourite/Lock/bulk actions;
- inventory/save-domain changes;
- speculative compatibility work.

The result remains one focused extension of HPA-357's Inventory screen. The second review adds concrete edge-case rules and reviewable implementation gates without changing the ticket's required explicit-selection product behavior.