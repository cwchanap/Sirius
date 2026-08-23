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

`SiriusItemSlotController` also already exposes both behaviors HPA-375 needs. Its custom `Activated` signal is intentionally restricted to actionable states, while normal `Button.Pressed` still fires for focusable `Unsupported` entries. Inventory therefore listens to normal `Pressed` for selection and does **not** modify the shared slot component.

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

Use this one key type for both selected identity and focus restoration:

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

Selection and current focus may diverge during normal browsing. For example, an item may remain selected while `%InventorySort` owns focus. Sharing the key type does not make them the same state; it only prevents two representations of the same item/equipment identity.

### Reuse the current catalogue maps

Do **not** add another `Dictionary<string, InventoryEntry>`.

The existing maps are sufficient:

```csharp
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot;
private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId;
```

Resolve a selected inventory item by ID through `_inventorySlotByItemId`, then read its current `InventoryEntry` from `_inventoryEntryBySlot`. Both maps remain refresh-scoped; no mutable `InventoryEntry` is retained across refresh.

### Reuse the existing pending fallback index

Do **not** add `_pendingSelectionFallbackIndex`.

`PendingFocusRestore.PreviousCatalogueIndex` already represents the visible catalogue position needed after a disappearing item. Successful mutations set `_pendingFocusRestore` explicitly. Selection reconciliation can use the same pending record to choose the replacement semantic key, then update `Preferred` before `RestorePendingFocus()` runs.

This gives selection and focus one mutation-reconciliation path while leaving ordinary non-mutation focus independent.

## 4. Selection behavior

Pressing a slot selects; it never mutates inventory/equipment.

- Populated inventory item → select by item ID.
- Populated main equipment slot → select by slot type.
- Populated accessory slot → select by accessory index.
- Empty equipment/accessory slot → keep current selection unchanged.
- Unsupported/battle-only inventory entry → selectable so Details can explain why no action exists.
- Sort → preserve selected inventory item by item ID.
- Filter → preserve selection only if that inventory item remains visible; otherwise clear it. Equipped selection is unaffected by item filters.
- External refresh/removal → clear a selected key that no longer resolves unless a successful mutation supplies a replacement through `_pendingFocusRestore`.

`ButtonPressed` is the visual selection marker. After every refresh, normalize all Inventory-owned slot pressed states from `_selection`; do not trust toggle state left by the pressed control itself.

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

Update `BuildInventoryTooltip` in the same cutover task. Item tooltips may retain description/effect/battle restriction, but the interaction sentence becomes neutral selection copy:

```text
Select to view details
```

Do not leave any tooltip claiming slot press directly equips or consumes.

## 7. Contextual action

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

Before/after the existing domain operation, preserve the visible index locally as needed, then on **success** populate `_pendingFocusRestore` with the resulting semantic selection and call the existing `RefreshUI()` path:

- Equip → select/focus resulting main equipment slot or exact accessory index.
- Unequip → select/focus the returned inventory item.
- Use with remaining quantity → select/focus the same inventory item.
- Use of final quantity → selection reconciliation chooses the item now at the previous visible index, or the previous last item, or clears when none remain; update `_pendingFocusRestore.Preferred` to that replacement before focus restoration.

`RestorePendingFocus()` remains the page/focus authority. In compact mode it switches to Items/Equipment before grabbing the resulting slot. In standard mode it grabs the resulting visible slot directly.

Failed mutations do not change action semantics, selection, or page; clear/avoid pending mutation restore and leave focus where it is.

This keeps the existing HPA-357 safety seam: a second Confirm cannot immediately invert Equip → Unequip or Unequip → Equip on the same Details button.

## 8. Filtering and sorting

Add two private enums and two authored `OptionButton`s, reusing the same scene-owned control pattern as `ActiveSkillSelector`:

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

### Filtering

- `All` → every entry.
- Other values → exact existing `ItemCategory` match.
- Presentation only; never mutate Inventory.

### Sorting

- Name → `DisplayName` ordinal → `Category` numeric → `Id` ordinal.
- Category → `Category` numeric → `DisplayName` ordinal → `Id` ordinal.

No search, asc/desc toggle, rarity/value sort, custom category order, or persisted browse preference.

Filter/sort rebuild the current visible slot set using the existing grow/reuse/shrink mechanism.

## 9. Equipment comparison

Only a selected **inventory `EquipmentItem`** shows replacement comparison.

Resolve the target exactly as Equip will:

- non-accessory → `EquipmentItem.SlotType`;
- accessory → `ResolveAccessoryEquipIndex()` (first empty, otherwise slot 0).

Show target outcome plus ATK/DEF/SPD/HP delta against the item currently occupying that target, if any.

All four stats are always represented. Use a dedicated formatter; do not reuse `GetBonusText()` because that helper intentionally omits zero bonuses.

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

Comparison is presentation only; the existing equip operation remains authoritative.

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

Selecting a slot updates Details immediately because Details is already visible; selection does not steal focus.

### Compact

Add `%DetailsTab` to the current `ButtonGroup`; raw LB/RB cycles modulo 4.

Show exactly one of Equipment / Items / Skills / Details.

**Do not auto-jump to Details when a slot is selected.** Slot press updates semantic selection and pressed visuals while the user remains on the current Equipment/Items page. The user opens Details explicitly with `%DetailsTab` or LB/RB. This keeps filter → scan → select browsing usable without a page round-trip per item.

When the user opens Details:

- focus `%DetailsActionButton` when a supported action exists;
- otherwise focus `%DetailsTab`.

After a successful mutation, `RestorePendingFocus()` switches compact back to the resulting Equipment/Items page and focuses the resulting slot as described in §7.

### Focus-page mapping

Extend `ResolveFocusPage`, `RestoreFocusForPage`, and `RestoreCompactPageFocus` narrowly for the new controls:

- `%InventoryFilter` / `%InventorySort` → `InventoryPage.Items`;
- inventory slots / `%ItemsTab` → Items;
- `%DetailsTab` / `%DetailsActionButton` → Details;
- equipment/accessory slots / `%EquipmentTab` → Equipment;
- `ActiveSkillSelector` / `%SkillsTab` → Skills.

This mapping matters during compact breakpoint changes: a focused filter/sort/action must not become hidden because page resolution returned null.

For Details focus restoration, use `%DetailsActionButton` if visible/focusable; otherwise `%DetailsTab` while compact, then the normal Close fallback when needed.

Do not build a generic navigation graph.

## 11. File ownership

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

## 12. Required regression coverage

### Controller

- slot press selects without Equip/Unequip/Use;
- unsupported/battle-only entries select and explain missing action;
- tooltip interaction copy says selection, not direct mutation;
- details use current name/category/rarity/quantity/description/effect/slot data;
- all filters and both sort modes, including ID tie-break;
- sorting preserves selection by ID;
- filtering out selected inventory item clears selection;
- equipment comparison covers occupied, empty, first-empty accessory, and full-accessory slot-0 target;
- comparison pins positive, negative, and `unchanged` zero copy;
- Details action preserves current equip/unequip/use rollback ordering;
- successful Equip/Unequip/Use never leaves focus on an inverted Details action;
- final consumable selection/focus fallback uses the existing pending previous index;
- active-skill behavior remains unchanged.

### Scene/input

- standard layout contains Details inside SafeFrame;
- compact has exactly four pages and one visible page;
- LB/RB cycles all four while paused;
- compact slot selection does **not** auto-switch pages;
- selected slot remains visually selected when returning from Details;
- successful compact mutation switches/focuses the resulting Equipment/Items slot through existing restore logic;
- filter/sort focus survives compact breakpoint changes as Items-page focus;
- Details action/tab focus survives compact breakpoint changes as Details-page focus;
- keyboard/D-pad reaches filter, sort, slots, Details action, and Close using actual input outcomes;
- current verification viewports remain bounded.

No new E2E or screenshot framework.

## 13. Risks and mitigations

### Stale selected entry

Store only `InventorySemanticKey`; resolve through current slot maps/live equipment after every refresh.

### Selection/focus reconcilers drift

Use one semantic key type and the existing `PendingFocusRestore.PreviousCatalogueIndex` for mutation fallback. Do not add parallel selection-fallback state.

### Inverted action double-confirm

Every successful mutation routes focus away from `%DetailsActionButton` to the resulting slot before another activation can occur.

### Accessory preview disagrees with Equip

Comparison and Equip both call `ResolveAccessoryEquipIndex()`.

### Compact browsing becomes page-heavy

Selection does not auto-jump. Details is an explicit fourth page.

### Breakpoint hides focused new chrome

`ResolveFocusPage` explicitly maps filter/sort and Details controls.

## 14. YAGNI boundary

Explicitly excluded:

- browser/view-model/service/repository layers;
- generic item-details component shared with Battle;
- new host kind/modal;
- persisted selection/filter/sort;
- search or extra sort modes;
- colored comparison framework;
- nonexistent equipment requirements;
- Drop/Sell/Favourite/Lock/bulk actions;
- inventory/save-domain changes;
- speculative compatibility work.

The result is one focused extension of the screen HPA-357 already established, with less new state than the first draft and with mutation/navigation behavior pinned to the controller's existing semantic restore machinery.