# HPA-357 Inventory and Equipment Parity Design

**Status:** Planning candidate  
**Linear:** HPA-357 — Redesign Sirius inventory and equipment screen with feature parity  
**Date:** 2026-08-12

## 1. Decision summary

Replace the fixed editor-like Inventory screen with one scene-authored, full-screen Sirius surface while preserving the existing inventory, equipment, consumable, active-skill, pause, and rollback rules.

The implementation stays deliberately small:

- keep `InventoryMenuController` as the screen controller and domain-operation caller;
- keep `Game` and the existing gameplay `UIScreenHost` as lifecycle owners;
- add one reusable presentational leaf, `SiriusItemSlot`, for equipment, accessories, and inventory entries;
- render inventory entries dynamically instead of committing a fixed 24-slot grid;
- use one shared scene tree for standard and compact layouts rather than duplicating screen controllers or content;
- add compact `Equipment`, `Items`, and `Skills` pages with local page/focus memory;
- keep focused-item detail passive and ephemeral rather than adding HPA-375's persistent selection/comparison model.

No inventory view model, presenter, repository/facade, generic collection renderer, navigation service, or new domain protocol is introduced.

## 2. Why HPA-357 is next

The Sirius project delivery order places Inventory parity immediately after Settings and Save/Load and before full-screen Battle. The shared Theme and gameplay `UIScreenHost` foundation are already merged, as are Pause, Settings, Main Menu, exploration HUD, and Save/Load migrations.

HPA-357 is blocked only by the completed UI foundation workstream. It also unlocks HPA-375 inventory enhancements and contributes to the final UI hardening gate.

## 3. Current-state findings

### 3.1 Presentation is still fixed and screen-local

`scenes/ui/InventoryMenu.tscn` currently has a 1240×760 fixed centred panel, local `StyleBoxFlat` resources, a two-pane `HSplitContainer`, six accessory placeholders, and 24 manually authored inventory slots. That shape cannot satisfy the 640×360 minimum layout without shrinking or clipping.

`InventoryMenuController` then overrides those authored sizes again at runtime with 108×108 panels and 96×96 buttons and creates its own base/equipped/locked `StyleBoxFlat` states in C#. This leaves style and geometry split between scene and controller.

### 3.2 The domain operations are already narrow enough

The controller already delegates the important behavior to existing domain objects:

- inventory entries come from `Character.Inventory`;
- deterministic ordering is `DisplayName` ordinal sorting;
- equip delegates to `Character.TryEquip`;
- unequip delegates to `Character.Unequip` and rolls back when the item cannot return to inventory;
- consumable use removes first, applies the effect, and attempts rollback on failure;
- battle-only consumables are rejected outside battle;
- active-skill selection delegates to `Character.EquipActiveSkill`, with explicit `None` preserved.

Those rules should not move into a new layer for this ticket.

### 3.3 The fixed catalogue is a real parity limitation

`Inventory.MaxItemTypes` supports up to 100 item types, but the current Inventory UI displays only the scene's 24 authored slot nodes and logs a warning for any remaining entries. HPA-357 should remove this presentation cap by creating one visual slot per current entry inside a scrollable catalogue. It should not pre-create 100 empty slots.

### 3.4 Host integration already exists

`Game.TryOpenInventory` already registers Inventory with `UIScreenHost`, including root-vs-Pause parentage, tree-pause ownership, cursor ownership, topmost Cancel, `toggle_inventory`, focus restoration, and external node lifetime.

HPA-357 should keep that hosting path. The only policy correction required by the approved HPA-373 lifecycle contract is changing Inventory from inherited HUD visibility to `UIHudPolicy.Hidden` while it is open.

## 4. Goals

1. Ship a responsive RPG-style character/equipment/inventory/active-skill screen that uses the existing Sirius Theme.
2. Preserve all currently supported inventory and equipment operations and rollback behavior.
3. Show every current inventory item type instead of truncating after 24 entries.
4. Make item/equipment information reachable through mouse, keyboard, and gamepad focus.
5. Provide a deliberate compact navigation model at 640×360 instead of shrinking the desktop composition.
6. Keep lifecycle, pause, Cancel, cursor, HUD, and focus restoration owned by `UIScreenHost`.
7. Create only the reusable slot primitive that this migration proves it needs.

## 5. Non-goals

HPA-357 does not add:

- persistent selected-item state;
- equipment comparison;
- category filtering;
- user sorting;
- Drop, Sell, Favourite, Lock, or bulk actions;
- inventory pagination as a domain concept;
- battle item-selection redesign;
- passive-skill loadout editing beyond what the current screen already supports;
- inventory persistence changes;
- a generic UI collection renderer;
- a generic screen presenter/view-model layer;
- new art assets;
- HPA-375 behavior.

## 6. Approaches considered

### Approach A — restyle the existing fixed grid

Keep the 24 authored slots, replace local colors with Theme values, and add a compact size mode.

**Rejected.** This is the smallest diff but retains two structural defects: item types beyond slot 24 stay invisible, and the fixed two-pane composition remains brittle at 640×360. It would likely be replaced again as soon as HPA-375 needs richer browsing.

### Approach B — generic inventory presentation framework

Introduce inventory/equipment view models, a generic collection renderer, slot factories, navigation abstractions, and separate presenters for standard/compact modes.

**Rejected.** Only one screen currently needs this behavior. The existing controller/domain boundaries are already sufficient, and a framework would add more code than the migration itself.

### Approach C — one screen controller plus one reusable slot leaf

Keep domain operations in `InventoryMenuController`, rewrite the scene around the Sirius Theme, introduce a small slot component that owns only slot visuals and activation guarding, and dynamically instantiate inventory slots from the current sorted entries.

**Chosen.** It fixes the real presentation constraints while preserving the existing domain seams and gives later Battle/Inventory work one proven slot primitive without pre-building a component system.

## 7. Architecture

### 7.1 Ownership

`Game`
: Owns creation of the Inventory view, `UIScreenHost` registration, parentage, pause, gameplay blocking, HUD policy, cursor, topmost Cancel, and restoration.

`InventoryMenuController`
: Owns binding current `Character` state into the scene, compact-page state, ephemeral focus summary, creating/binding catalogue slot instances, and invoking the existing character/inventory operations.

`SiriusItemSlotController`
: Owns only one slot's visual state, icon, quantity/state labels, tooltip, focusability, and guarded activation signal. It has no knowledge of `Item`, `Character`, `Inventory`, equipment rules, or `GameManager`.

`Character`, `Inventory`, `EquipmentSet`, `SkillCatalog`
: Continue to own domain state and rules.

### 7.2 Slot component

Create:

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`

The root is a focusable `Button` so keyboard/gamepad focus, hover, pressed, and focus rendering come from one native control. Quantity and state labels are passive child controls with `MouseFilter.Ignore`.

The component exposes one visual enum and one binding method:

```csharp
public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Locked,
    Unsupported
}

public partial class SiriusItemSlotController : Button
{
    [Signal] public delegate void ActivatedEventHandler();

    public bool Actionable { get; private set; }

    public void Present(
        Texture2D? icon,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable);
}
```

`Pressed` emits `Activated` only when `Actionable` is true. Non-actionable Empty, Locked, and Unsupported slots remain focusable so their reason is reachable without a mouse; the visual state communicates that activation is unavailable.

The Theme receives only slot-specific native Button variations needed to express the approved 4 px slot geometry and normal/equipped/unavailable treatment. Focus remains a distinct cyan treatment, so an equipped gold state can coexist with focus.

`SiriusUiMetrics` receives one proven slot metric:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

No generic slot configuration object or renderer is added.

### 7.3 Inventory scene structure

Rewrite `scenes/ui/InventoryMenu.tscn` around the shared Theme and safe-frame policy.

Conceptual tree:

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
            │   │   │   ├── EquipmentSlots
            │   │   │   └── AccessorySlots
            │   │   └── SkillsPage
            │   │       ├── ActiveSkillSelector
            │   │       └── ActiveSkillSummary
            │   └── ItemsPage
            │       └── InventoryScroll
            │           └── InventoryGrid
            ├── FocusSummary
            └── Footer
                └── CloseButton
```

The root owns `SiriusTheme.tres`. Shared `SiriusContentPanel`, `SiriusFeaturePanel`, text roles, bars, icons, and input-hint presentation are reused rather than recreating local styles.

The five primary equipment slots and six accessory positions remain explicit scene-authored `SiriusItemSlot` instances because their positions and meanings are fixed. Inventory catalogue slots are dynamic because their count and contents are data-driven.

The existing hero sheet/96×96 atlas crop already used by `ExplorationHud.tscn` is reused directly in the Inventory scene; no portrait service or asset lookup abstraction is added.

### 7.4 Character summary

Bind the current supported character state directly:

- name;
- level;
- current HP / effective maximum HP;
- current MP / maximum MP;
- experience / next-level experience;
- effective ATK;
- effective DEF;
- effective SPD;
- gold.

Use existing `SiriusStatBar` components for HP/MP and the themed EXP bar for EXP. Derived combat values come from the existing `Character.GetEffective*` methods so equipment changes visibly refresh the summary.

At compact size, keep name, level, HP, MP, and Gold continuously readable. ATK/DEF/SPD and EXP may use the compact metadata treatment but must remain reachable on the Equipment page; do not shrink essential text below the approved compact minimum.

### 7.5 Equipment and accessories

The primary slots stay explicit and bind to their existing `EquipmentSlotType` values.

Visual mapping:

- populated primary/accessory slot → `Equipped`, actionable, item icon, full tooltip/focus summary;
- empty active slot → `Empty`, non-actionable, slot-type glyph and `Empty` reason;
- inactive accessory placeholder → `Locked`, non-actionable, lock glyph and `Accessory Slot Locked` reason.

Activating an equipped slot invokes the existing unequip path. No new equipment transaction API is added.

### 7.6 Dynamic inventory catalogue

Replace the fixed `_inventorySlotEntries` array and 24 pre-authored nodes with a dynamic list of `SiriusItemSlotController` instances under `%InventoryGrid`.

On refresh:

1. read `Player.Inventory.GetAllEntries()`;
2. sort by `Item.DisplayName` using the current `StringComparison.Ordinal` rule;
3. create/rebind exactly one slot for each current entry;
4. set icon and quantity;
5. map equipment to `Available` + actionable;
6. map supported out-of-battle consumables to `Available` + actionable;
7. map battle-only consumables and unknown item categories to `Unsupported` + non-actionable with a readable reason;
8. remove extra visual slots when the item-type count decreases.

The catalogue is inside a `ScrollContainer`; therefore inventory capacity remains a domain concern and the UI does not create 100 placeholders.

A private screen-local binding from slot instance to current `InventoryEntry` is sufficient. Do not add IDs or generic item-view models solely for rendering.

### 7.7 Focus summary

`%FocusSummary` is passive presentation, updated by slot focus/mouse enter and active-skill focus. It exposes the same description, quantity, bonuses, slot/category, effect, and availability reason that the current mouse tooltip exposes.

It is deliberately not a selected-item model:

- it does not survive screen destruction;
- it does not control actions;
- it does not add comparison;
- it follows current focus/hover only;
- actions continue to operate on the activated slot.

This satisfies keyboard/gamepad information parity without pulling HPA-375 into the migration.

### 7.8 Responsive behavior

Use `SiriusUiMetrics.IsCompact` and `SafeFrameInsets`.

#### Standard

At 800×450 and above:

- `CompactTabs` hidden;
- `CharacterColumn` and `ItemsPage` visible together;
- `EquipmentPage` and `SkillsPage` both visible in the character column;
- Items receives the flexible width and vertical scroll;
- content remains inside the 1600 px max-width safe frame.

#### Compact

Below 800 logical width or 450 logical height:

- `CompactTabs` visible;
- persistent identity strip remains visible;
- exactly one of `Equipment`, `Items`, or `Skills` content is visible at once;
- `CharacterColumn` is visible for Equipment/Skills and hides its inactive sibling page;
- `ItemsPage` fills the available body when selected;
- slot size is 48×48;
- supporting telemetry is reduced before essential state/action text.

The same page/content nodes are reused in both modes. There is no duplicate compact controller or duplicate set of inventory/equipment controls.

### 7.9 Compact page navigation and focus

Keep page navigation local to the Inventory screen.

- Mouse: click the three tab buttons.
- Keyboard: when a compact tab has focus, normal `ui_left`/`ui_right` navigation moves among tabs; Enter/Space activates the page.
- Gamepad: LB/RB while Inventory is visible and compact cycles the pages.

Do not add remappable `inventory_page_left/right` actions in this ticket.

The controller remembers the active compact page and the last still-valid focus owner for the screen instance. On standard↔compact reflow, preserve focus when that control remains visible. Otherwise use this fallback order:

1. last valid focus on the active page;
2. first primary equipment slot for Equipment;
3. first inventory item slot for Items;
4. active-skill selector for Skills;
5. active compact tab;
6. Close.

`InitialFocusTarget` uses the same resolution. This replaces the current Close-first default and matches the HPA-373 lifecycle contract.

### 7.10 Lifecycle integration

Keep `Game.SetupInventoryMenu`, `TryOpenInventory`, `TryCloseInventory`, parent handle behavior, external node lifetime, and `CloseRequested` ownership.

Change only policy that the redesign proves is wrong:

```csharp
Hud = UIHudPolicy.Hidden,
InitialFocus = () => _inventoryMenu.InitialFocusTarget,
```

Root Inventory still owns tree pause and gameplay block. Pause-child Inventory inherits the already-paused parent and does not acquire another pause/gameplay-block lease.

`InventoryMenuController` never writes `SceneTree.Paused` and never consumes Cancel/toggle as a terminal action. The host remains the terminal lifecycle owner.

## 8. File ownership

### Create

- `scripts/ui/components/SiriusItemSlotController.cs` — presentational slot state and guarded activation only.
- `scenes/ui/components/SiriusItemSlot.tscn` — reusable themed slot scene.
- `tests/ui/components/SiriusItemSlotControllerTest.cs` — leaf behavior/state/focus contract.
- `tests/ui/InventoryMenuSceneTest.cs` — responsive layout, fit, visibility, and focus smoke coverage.

### Modify

- `resources/ui/theme/SiriusTheme.tres` — only proven slot Button variations.
- `scripts/ui/theme/SiriusThemeTypes.cs` — names for the new slot variations.
- `scripts/ui/theme/SiriusUiMetrics.cs` — 56/48 slot-size helper.
- `scenes/ui/InventoryMenu.tscn` — replace fixed workbench with themed responsive scene.
- `scripts/ui/InventoryMenuController.cs` — bind scene, dynamic slots, character summary, page/focus behavior; preserve domain calls.
- `tests/ui/InventoryMenuControllerTest.cs` — migrate existing assertions and add dynamic/parity/focus tests.
- `scripts/game/Game.cs` — Inventory HUD policy only, unless tests prove another host correction is required.
- `tests/game/GameplayPauseHostTest.cs` — direct/Pause child Inventory policy and focus restoration.
- `tests/game/GameInputLifecycleTest.cs` — only if existing Inventory toggle/Cancel lifecycle coverage needs node/path migration.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile Inventory presentation details after cutover if the current row is stale.

No `Character`, `Inventory`, `EquipmentSet`, save format, or skill-domain file should need modification.

## 9. Test strategy

Use focused coverage rather than a viewport × input × item-state Cartesian matrix.

### Slot component

Cover:

- 56/48 sizing source comes from `SiriusUiMetrics`;
- Available emits one activation;
- Empty/Locked/Unsupported remain focusable but do not emit activation;
- Equipped and focused state can coexist;
- quantity/state labels update and clear without stale text;
- tooltip text remains readable.

### Inventory controller/domain parity

Preserve or add focused tests for:

- current Gold and character stats;
- deterministic alphabetical entry order;
- more than 24 item types are rendered and reachable;
- equipment activation equips through existing `TryEquip` path;
- equipment-slot activation unequips and returns the item to inventory;
- failed unequip return restores the original equipment;
- out-of-battle consumable success removes one item and refreshes stats;
- failed consumable application attempts rollback;
- battle-only consumables are shown unavailable and do not mutate inventory;
- active skill can be selected and explicitly cleared;
- locked accessories show reason and cannot mutate equipment;
- focus summary follows keyboard focus as well as mouse hover.

Do not duplicate deep `Character`, `Inventory`, and `EquipmentSet` unit tests already owned by those domain suites.

### Responsive scene

Use `SiriusUiMetrics.VerificationViewports` for fit/layout smoke and `FocusVerificationViewports` for deeper focus checks.

At 1280×720 assert:

- identity, character/equipment/skills, and Items regions are visible;
- compact tabs are hidden;
- content is within safe frame;
- slots use 56×56 geometry;
- Items scroll has non-zero page size.

At 640×360 assert:

- compact tabs are visible;
- exactly one content page is active;
- identity and Close remain usable;
- slots use 48×48 geometry;
- the active page fits inside the safe frame;
- long focus-summary text scrolls/wraps instead of expanding the screen;
- tab → first actionable/focusable content → Close traversal remains possible.

At the other approved viewports perform bounds/non-zero-size smoke checks without repeating every state/input combination.

### Host lifecycle

Update the existing real `Game.tscn` integration tests to prove:

- direct Inventory hides the gameplay HUD, pauses the tree, blocks gameplay, and restores all state on close;
- Pause-child Inventory has Pause as its parent, keeps the tree paused, hides the gameplay HUD, and returns focus to the Pause Inventory button;
- `toggle_inventory` and Cancel close only the topmost Inventory entry;
- reopening reuses the external Inventory view without stale handles;
- teardown leaves no Inventory entry or input/pause lease.

## 10. Risks and mitigations

### Risk: dynamic rebuilding loses focus after an equip/use action

Keep focus restoration screen-local. Capture the focused item/slot before refresh, rebind/rebuild, then restore the matching still-valid visual node; otherwise use the active-page fallback. Do not introduce persistent selection state.

### Risk: compact layout duplicates content

Use one `CharacterColumn` plus one `ItemsPage`; toggle visibility of `EquipmentPage`/`SkillsPage` inside the same character column. Do not author a second compact scene tree.

### Risk: unavailable slots become inaccessible to keyboard/gamepad

Represent actionability separately from native focusability. `SiriusItemSlotController` stays focusable and guards activation itself while its visual state and focus summary explain why the action is unavailable.

### Risk: HPA-357 expands into HPA-375

Keep the detail surface focus-driven only. No selected-item persistence, comparison, filters, user ordering, category model, or additional actions.

### Risk: host behavior is accidentally rewritten

Treat `TryOpenInventory` as an existing contract. Change HUD policy and initial-focus target only; add integration tests before considering any other host modification.

## 11. Acceptance mapping

- Clear character/equipment/accessory/inventory/consumable/skill hierarchy → responsive scene + identity strip + three content areas.
- Preserve equip/unequip/consume/quantity/locked/skill behavior → existing controller operations retained and parity-tested.
- Alphabetical ordering → same ordinal sort before dynamic slot binding.
- No new domain actions → explicit non-goals and no domain-file changes.
- Consistent slot states → `SiriusItemSlot` visual contract.
- Keyboard/gamepad traversal → native focusable slot Button, compact tabs, focus summary, focused tests.
- Narrow layout → compact Equipment/Items/Skills pages.
- Host restoration → existing `UIScreenHost` flow with HUD correction and integration coverage.
- All approved viewports → shared verification viewport test source.

## 12. Design self-review

The proposal intentionally adds only one reusable leaf because the migration has three concrete consumers of the same slot geometry: primary equipment, accessories, and inventory entries. Everything else stays feature-local.

The design fixes the current 24-item presentation cap without changing the 100-item domain capacity, keeps transaction/rollback semantics in their current owners, avoids duplicate compact content, and does not pre-implement HPA-375. The only cross-screen API change is one slot-size metric and the slot Theme variations required by the first real slot consumer.
