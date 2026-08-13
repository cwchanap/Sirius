# HPA-357 Inventory and Equipment Parity Design

**Status:** Planning candidate  
**Linear:** HPA-357 — Redesign Sirius inventory and equipment screen with feature parity  
**Date:** 2026-08-12

## 1. Decision

Replace the fixed Inventory workbench with one scene-authored, full-screen Sirius screen while preserving current inventory/equipment/consumable/skill semantics.

Keep the design small:

- one `InventoryMenuController`;
- `Game` / `UIScreenHost` remain lifecycle owners;
- one reusable `SiriusItemSlot` UI leaf;
- dynamic current-entry catalogue, not 24 authored or 100 placeholder slots;
- one scene tree with compact Equipment / Items / Skills visibility pages;
- one tiny shared player-summary presenter because HUD and Inventory are now two concrete consumers of the same fallback rules;
- semantic focus restoration only inside the current screen instance;
- native Godot spatial focus first; explicit `FocusNeighbor*` only when an input test proves a concrete boundary fails.

No view model, presenter layer, collection renderer, navigation service, repository/facade, compatibility layer, or HPA-375 browsing model is introduced.

## 2. Current-state facts that shape the migration

### Fixed presentation

`InventoryMenu.tscn` currently owns a fixed 1240×760 panel, local `StyleBoxFlat`s, six accessory placeholders, and 24 catalogue slots. `InventoryMenuController` then overwrites slot sizes/styles again in C#.

The redesign removes this split presentation ownership and uses the shared Theme/scene system.

### Domain seams already exist

Keep the existing operations in `InventoryMenuController`:

- ordinal `DisplayName` sorting;
- `Character.TryEquip`;
- `Character.Unequip` + current inventory-return rollback;
- remove/apply/rollback consumable ordering;
- battle-only consumable rejection;
- `Character.EquipActiveSkill` and explicit None.

No domain rewrite is needed.

### Dynamic catalogue fixes a real cap

`Inventory.MaxItemTypes` supports 100 item types but the UI renders only 24. HPA-357 grows/reuses/shrinks visual slots to exactly the current sorted entries inside a `ScrollContainer`. Inventory capacity remains unchanged.

### Slot art has a tested two-mode contract

HPA-374 tests distinguish:

- generated type glyphs: 32 px, native-size centered;
- real item art: aspect-preserving scaled.

Move that distinction onto `TextureRect` APIs owned by the existing `UiIconPresenter`:

```csharp
public static bool ApplyGlyph(TextureRect target, UiIconId id, UiIconSize size);
public static void ApplyItem(TextureRect target, Texture2D? texture);
```

Task 1 adds these APIs while the legacy Inventory still needs the old `TextureButton` helpers. Task 2 deletes the `TextureButton` presenter path only after Inventory has cut over to `%Icon: TextureRect`.

### Four accessory indices exist but current Inventory only targets index 0

`EquipmentSet.AccessorySlotCount == 4`. `Character.TryEquip(..., accessorySlot)` already routes to any of those four slots, but current `EquipFromInventory` uses the default index 0.

HPA-357 makes the existing four slots honest:

1. choose the first empty accessory index;
2. if all four are occupied, fall back to index 0 to preserve current replacement behavior;
3. call the existing indexed `TryEquip` overload;
4. restore focus to the chosen accessory slot.

This is UI routing through an existing domain seam, not an accessory progression feature.

### HUD and Inventory share player-summary fallbacks

`ExplorationHudController.ApplyPlayerState` already owns tested behavior for:

- blank name → `Adventurer`;
- HP binding;
- hide MP when `MaxMana <= 0`;
- hide EXP when `ExperienceToNext <= 0`;
- clamp visible EXP.

Add a small static `SiriusPlayerSummaryPresenter` that applies the existing `ExplorationHudPlayerState` to already-bound common controls. HUD and Inventory both call it. Do not extract a new IdentityStrip scene/component.

Inventory separately binds effective ATK/DEF/SPD and exact `Gold: {value}` copy.

## 3. Slot component

Create `SiriusItemSlotController : Button` plus `SiriusItemSlot.tscn` with passive `%Icon`, `%QuantityLabel`, and `%StateLabel` children.

Use only four visual states:

```csharp
public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Unsupported
}
```

There is no `Locked` production consumer after fake accessory placeholders are removed.

`Actionable` is derived:

```csharp
public bool Actionable =>
    _state is SiriusItemSlotVisualState.Available
        or SiriusItemSlotVisualState.Equipped;
```

Binding surface:

```csharp
public void SetCompact(bool compact);

public void PresentGlyph(
    UiIconId iconId,
    string quantityText,
    string stateText,
    string tooltipText,
    SiriusItemSlotVisualState state);

public void PresentItem(
    Texture2D? texture,
    string quantityText,
    string stateText,
    string tooltipText,
    SiriusItemSlotVisualState state);
```

Empty/Unsupported remain focusable and non-disabled so their reason is reachable; activation is guarded by derived `Actionable`.

The Theme gains three Button variations only: normal, equipped, unavailable. `SiriusUiMetrics` gains only `ItemSlotSize(bool)` returning 56×56 standard / 48×48 compact. The enum and Theme names are pinned in `SiriusUiContractsTest`.

## 4. Screen structure

Inventory is a full-screen SafeFrame screen, not a `SiriusModalShell` dialog.

```text
InventoryMenu
├── Scrim
└── SafeFrame
    └── ScreenSurface
        └── Content
            ├── IdentityStrip
            │   ├── Portrait
            │   ├── PlayerName / PlayerLevel
            │   ├── HP / MP / EXP
            │   ├── ATK / DEF / SPD
            │   └── Gold
            ├── CompactTabs
            │   ├── EquipmentTab
            │   ├── ItemsTab
            │   └── SkillsTab
            ├── ResponsiveContent
            │   ├── CharacterColumn
            │   │   ├── EquipmentPage
            │   │   │   ├── EquipmentTitleRow
            │   │   │   ├── EquipmentSlots
            │   │   │   └── AccessorySlots
            │   │   └── SkillsPage
            │   └── ItemsPage
            │       ├── InventoryTitleRow
            │       └── InventoryScroll/InventoryGrid
            ├── FocusSummary
            └── Footer/CloseButton
```

Preserve stable heading names consumed by current tests/HPA-374 smoke. Author five primary equipment slots and exactly `%AccessorySlot0` through `%AccessorySlot3`; `%InventoryGrid` starts empty.

Reuse the HUD hero crop `Rect2(0, 0, 96, 96)`.

`%FocusSummary` is a plain `Label` with `AutowrapMode.WordSmart` inside bounded layout. No BBCode is produced, so `RichTextLabel` adds no value.

## 5. Dynamic catalogue and action mapping

Refresh:

1. read current `InventoryEntry` values;
2. sort ordinal by `DisplayName`;
3. grow/reuse/shrink `SiriusItemSlotController` nodes;
4. rebuild refresh-scoped slot → entry and item-id → slot maps;
5. bind equipment / usable consumables as Available;
6. bind battle-only / unsupported entries as Unsupported with readable reason.

The slot → `InventoryEntry` map is explicitly refresh-scoped. Never retain an entry from that map across a mutation; `Inventory` owns those live mutable objects.

Primary equipment activation uses current equip/unequip paths. Accessory activation uses first-empty-index routing above.

## 6. Focus identity and summary

Dynamic controls may be rebound or freed, so remember semantic identity rather than a `Control`:

```csharp
private readonly record struct InventoryFocusKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId);
```

Before mutation capture the semantic key and catalogue index. After refresh:

1. same surviving identity;
2. resulting equipment/accessory target after equip;
3. next item at prior catalogue index after final-item consumption;
4. previous last item;
5. active-page fallback;
6. page button;
7. Close.

After rebind, explicitly refresh `%FocusSummary` from current focus even if `FocusEntered` did not fire.

This is transient restoration state, not persistent selected-item state.

## 7. Compact behavior

Follow Settings' feature-local page-button pattern: one `ButtonGroup`, named button handlers, one `SetCompactPage(InventoryPage)` method. Do not use a `TabContainer` as the content host because standard Inventory displays Equipment + Skills + Items simultaneously.

Input:

- click page buttons;
- normal keyboard/D-pad spatial focus + activate;
- raw LB/RB cycles compact pages;
- no new InputMap actions.

Start with no explicit focus-neighbour graph. Behavioral tests use `SubViewport.PushInput` to prove:

- page tab + `ui_down` → first page control;
- first page control + `ui_up` → page tab;
- last page control + `ui_down` → Close.

If a named boundary fails after the real scene exists, add only the direct `FocusNeighbor*` assignment needed for that failing boundary. Do not add `LinkVertical`, a generic neighbor graph, or a recompute-all-neighbours invariant.

## 8. Lifecycle

Keep existing `Game.SetupInventoryMenu`, `TryOpenInventory`, `TryCloseInventory`, parentage, pause/block policies, cursor, Cancel/toggle ownership, external-node lifetime, and `InitialFocus` hook.

Change only:

```csharp
Hud = UIHudPolicy.Hidden,
```

Root Inventory remains host-paused/blocking; Pause-child Inventory inherits the already-paused parent.

## 9. File ownership

### Create

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`
- `scripts/ui/SiriusPlayerSummaryPresenter.cs`
- `tests/ui/components/SiriusItemSlotControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Modify

- `scripts/ui/art/UiIconPresenter.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusThemeTypes.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `tests/ui/theme/SiriusUiMetricsTest.cs`
- `tests/ui/theme/SiriusUiContractsTest.cs`
- `scripts/ui/ExplorationHudController.cs`
- `tests/ui/ExplorationHudControllerTest.cs`
- `scenes/ui/InventoryMenu.tscn`
- `scripts/ui/InventoryMenuController.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `scripts/game/Game.cs` — Inventory HUD policy only.
- `tests/game/GameplayPauseHostTest.cs`

Audit `GameInputLifecycleTest` and the HPA-376 lifecycle document only for stale Inventory paths/evidence.

No `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain production file changes are expected.

## 10. Test strategy

Cover narrowly:

- four exact slot enum values and three exact Theme names;
- 56/48 metric;
- TextureRect native glyph vs scaled item art;
- derived Actionable and non-actionable focusability;
- shared HUD fallback presenter regressions;
- exact `Gold: 321` Inventory copy;
- >24 catalogue entries + ordinal order;
- current equip/unequip/rollback/consume/skill parity;
- first accessory fills slot 0, second fills slot 1 without replacing slot 0;
- full accessory set falls back to current slot-0 replacement semantics;
- focus lands on the chosen accessory index;
- semantic focus restoration after equip/use and explicit summary refresh;
- all verification viewports, with deep 1280×720 / 640×360 checks;
- actual `ui_up`/`ui_down` focus outcomes, not `FocusNeighbor*` properties;
- LB/RB through a paused `SubViewport` while menu is `WhenPaused`;
- migrated HPA-374 heading/glyph/item/Close smoke;
- direct and Pause-child host HUD-hidden/restoration behavior.

## 11. Implementation sequence

1. **Slot/shared presentation:** add `SiriusItemSlot`, metric/Theme/contracts, TextureRect glyph/item presenter APIs, and shared HUD/Inventory player-summary presenter. Keep the legacy TextureButton presenter APIs because current Inventory still consumes them.
2. **Atomic Inventory cutover:** rewrite scene/controller/tests/HPA-374 smoke, dynamic catalogue, four-slot accessory routing, compact behavior, and semantic focus restoration; then delete the now-dead TextureButton presenter APIs. Run the full suite before this commit.
3. **Host/final verification:** change only Inventory HUD policy in `Game`, run lifecycle/full-suite/build/stale-path/scope audits.

## 12. YAGNI review

The review reduces surface area rather than expanding architecture:

- no `Locked` enum member;
- no independent `actionable` parameter;
- no pre-emptive focus-neighbour machinery;
- no `RichTextLabel` for plain text;
- no fake accessory placeholders.

The two added shared seams are earned by existing second consumers: HUD + Inventory share player-summary fallback presentation, and the existing icon presenter already owns glyph/item sizing semantics that move from TextureButton to TextureRect.
