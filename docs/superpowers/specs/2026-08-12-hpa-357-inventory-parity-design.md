# HPA-357 Inventory and Equipment Parity Design

**Status:** Planning candidate  
**Linear:** HPA-357 — Redesign Sirius inventory and equipment screen with feature parity  
**Date:** 2026-08-12

## 1. Decision summary

Replace the fixed editor-like Inventory screen with one scene-authored, full-screen Sirius surface while preserving the existing inventory, equipment, consumable, active-skill, pause, and rollback rules.

The implementation stays deliberately small:

- keep `InventoryMenuController` as the one feature controller and existing domain-operation caller;
- keep `Game` and the gameplay `UIScreenHost` as lifecycle owners;
- add one reusable presentational leaf, `SiriusItemSlot`, for primary equipment, accessories, and inventory entries;
- render exactly the current inventory entries dynamically instead of authoring 24 or 100 catalogue slots;
- use one shared scene tree for standard and compact layouts;
- use compact `Equipment`, `Items`, and `Skills` page buttons patterned after the existing Settings page-selection approach;
- keep focused-item detail passive and ephemeral rather than adding HPA-375's persistent selection/comparison model;
- preserve focus across refresh/reflow with screen-local identity keys, never raw dynamic `Control` references.

No inventory view model, presenter, repository/facade, generic collection renderer, navigation service, compatibility layer, or new domain protocol is introduced.

## 2. Why HPA-357 is next

The Sirius delivery order places Inventory parity immediately after Settings and Save/Load and before full-screen Battle. The Theme, `UIScreenHost`, Pause, Settings, Main Menu, exploration HUD, and Save/Load migrations are already available to reuse.

HPA-357 is blocked only by the completed UI foundation workstream. It also unlocks HPA-375 inventory enhancements and contributes to the final UI hardening gate.

## 3. Current-state findings

### 3.1 Presentation is still fixed and split between scene and controller

`scenes/ui/InventoryMenu.tscn` currently uses a fixed 1240×760 centred panel, local `StyleBoxFlat` resources, a two-pane `HSplitContainer`, six accessory placeholders, and 24 authored inventory slots.

`InventoryMenuController` then overrides slot geometry to 108×108 panels / 96×96 buttons and builds local base/equipped/locked `StyleBoxFlat` instances in C#. The same presentation concern therefore lives in both `.tscn` and C#.

HPA-357 removes that duplication and uses the shared Sirius Theme instead.

### 3.2 Domain operations are already narrow enough

The controller already delegates working behavior to existing domain objects:

- entries come from `Character.Inventory`;
- ordering is ordinal `DisplayName` sorting;
- equip delegates to `Character.TryEquip`;
- unequip delegates to `Character.Unequip` and rolls back when the removed item cannot return to inventory;
- consumable use removes first, applies the effect, and attempts rollback on failure;
- battle-only consumables are rejected outside battle;
- active-skill selection delegates to `Character.EquipActiveSkill`, with explicit `None` preserved.

Those rules stay where they are.

### 3.3 The fixed catalogue is a real parity limitation

`Inventory.MaxItemTypes` supports up to 100 item types, but the current UI displays only the 24 authored slot nodes and warns when additional entries are hidden.

HPA-357 removes this presentation cap by creating/reusing one visual slot per current entry inside a scrollable catalogue. It does not pre-create 100 empty nodes and does not change inventory capacity.

### 3.4 The current icon pipeline has two distinct contracts

Current Inventory tests and `Hpa374RuntimeSmokeTest` distinguish:

- generated slot/type glyphs: 32 px native art, centered without upscaling;
- populated item art: aspect-preserving scaling to the available slot region.

`UiIconPresenter.Apply(Button, ...)` is not a substitute because it uses the Button icon API and `icon_max_width`; that would erase the tested glyph-vs-item presentation distinction.

`SiriusItemSlot` therefore keeps the root as a focusable `Button` but owns a passive `%Icon` `TextureRect`. Glyph presentation uses `UiIconPresenter.Apply(TextureRect, ...)` and then `TextureRect.StretchModeEnum.KeepCentered`; item presentation uses the current loaded item texture with `KeepAspectCentered`. The root Button never uses `Button.Icon`.

### 3.5 Host integration already exists

`Game.TryOpenInventory` already registers Inventory with `UIScreenHost`, including root-vs-Pause parentage, tree-pause ownership, cursor ownership, topmost Cancel, `toggle_inventory`, focus restoration, and external node lifetime.

HPA-357 keeps that hosting path. The only host policy correction is changing Inventory from inherited HUD visibility to `UIHudPolicy.Hidden` while open.

### 3.6 The domain exposes four accessory slots, not six

`EquipmentSet.AccessorySlotCount` is `4`. The current fifth and sixth scene placeholders are always locked but have no unlock rule or persisted/domain state.

The redesigned screen authors exactly four accessory slots. `SiriusItemSlotVisualState.Locked` remains a valid reusable visual state and component contract, but HPA-357 does not render fake locked accessory positions for a progression system that does not exist.

## 4. Goals

1. Ship a responsive RPG-style character/equipment/inventory/active-skill screen using the existing Sirius Theme.
2. Preserve all currently supported inventory/equipment operations and rollback behavior.
3. Show every current inventory item type instead of truncating after 24 entries.
4. Preserve the existing HPA-374 icon behavior for native slot glyphs versus scaled item art.
5. Make item/equipment information reachable through mouse, keyboard, and gamepad focus.
6. Provide a deliberate compact navigation model at 640×360.
7. Keep lifecycle, pause, Cancel, cursor, HUD, and parent restoration owned by `UIScreenHost`.
8. Preserve valid focus through catalogue mutation/reflow without creating persistent selected-item state.
9. Create only the reusable slot primitive that this migration proves it needs.

## 5. Non-goals

HPA-357 does not add:

- persistent selected-item state;
- equipment comparison;
- category filtering;
- user sorting;
- Drop, Sell, Favourite, Lock, or bulk actions;
- inventory pagination as a domain concept;
- battle item-selection redesign;
- new accessory unlock rules or fake future accessory slots;
- passive-skill loadout editing beyond current supported behavior;
- inventory persistence changes;
- a generic UI collection renderer;
- a generic screen presenter/view-model layer;
- a navigation service;
- new InputMap actions for compact page cycling;
- new art assets;
- HPA-375 behavior.

## 6. Approaches considered

### Approach A — restyle the existing fixed grid

Keep the 24 authored slots, replace local colors with Theme values, and add a compact size mode.

**Rejected.** Item types beyond slot 24 remain invisible, and the fixed two-pane composition stays brittle at 640×360.

### Approach B — generic inventory presentation framework

Introduce inventory/equipment view models, a generic collection renderer, slot factories, navigation abstractions, and separate presenters for standard/compact modes.

**Rejected.** Only one screen needs these behaviors. The existing controller/domain boundaries are sufficient.

### Approach C — one screen controller plus one reusable slot leaf

Keep domain operations in `InventoryMenuController`, rewrite the scene around the Sirius Theme, introduce a small slot component that owns only visual/input presentation, and dynamically instantiate inventory slots from current sorted entries.

**Chosen.** It fixes the actual player-facing constraints without manufacturing another architecture layer.

## 7. Architecture

### 7.1 Ownership

`Game`
: Owns creation of the Inventory view, `UIScreenHost` registration, parentage, pause, gameplay blocking, HUD policy, cursor, topmost Cancel/toggle handling, and parent/gameplay focus restoration.

`InventoryMenuController`
: Owns binding current `Character` state into the scene, compact-page state, dynamic catalogue slot instances, ephemeral focus summary, screen-local focus identity/restoration, and invocation of the existing character/inventory operations.

`SiriusItemSlotController`
: Owns one slot's Theme variation, glyph/item-art presentation, quantity/state labels, tooltip, focusability, and guarded activation signal. It has no knowledge of `Item`, `Character`, `Inventory`, equipment transactions, or `GameManager`.

`Character`, `Inventory`, `EquipmentSet`, `SkillCatalog`
: Continue to own domain state and rules.

### 7.2 Slot component

Create:

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`

The root is a focusable `Button`. It contains passive `%Icon` `TextureRect`, `%QuantityLabel`, and `%StateLabel` children with `MouseFilter.Ignore`.

The public component surface is deliberately small:

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

    public void SetCompact(bool compact);

    public void PresentGlyph(
        UiIconId iconId,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable);

    public void PresentItem(
        Texture2D? texture,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable);
}
```

`PresentGlyph` calls `UiIconPresenter.Apply(%Icon, iconId, UiIconSize.Feature)` and then uses `KeepCentered`, so a 32 px glyph remains 32 px at both 56×56 and 48×48 slot sizes.

`PresentItem` uses the loaded item texture with `ExpandMode.IgnoreSize` and `KeepAspectCentered`, matching the current populated-item behavior without adding a new `UiIconPresenter` overload for one consumer.

`Pressed` emits `Activated` only when `Actionable` is true. Empty, Locked, and Unsupported remain natively focusable and non-disabled so keyboard/gamepad users can read the reason.

The Theme receives only three slot-specific Button variations: normal, equipped, and unavailable. Focus remains cyan and independent from the equipped gold state.

`SiriusUiMetrics` receives exactly one new proven metric:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

No generic slot configuration object or renderer is added.

### 7.3 Scene structure

Rewrite `scenes/ui/InventoryMenu.tscn` around the shared Theme and full-screen safe-frame policy. Do not use `SiriusModalShell`; Inventory is a screen, not a sized dialog.

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
            │   │   │   ├── EquipmentTitleRow
            │   │   │   ├── EquipmentSlots
            │   │   │   └── AccessorySlots
            │   │   └── SkillsPage
            │   │       ├── ActiveSkillSelector
            │   │       └── ActiveSkillSummary
            │   └── ItemsPage
            │       ├── InventoryTitleRow
            │       └── InventoryScroll
            │           └── InventoryGrid
            ├── FocusSummary
            └── Footer
                └── CloseButton
```

The root owns `SiriusTheme.tres`. Standard-mode heading nodes remain explicit and stable: `%EquipmentTitleIcon`, `%EquipmentTitleLabel`, `%InventoryTitleIcon`, and `%InventoryTitleLabel`.

The five primary equipment slots and exactly four accessory slots are explicit scene-authored `SiriusItemSlot` instances. `%InventoryGrid` starts empty and contains only current data-driven entries at runtime.

Reuse the hero sheet atlas crop `Rect2(0, 0, 96, 96)` already used by `ExplorationHud.tscn`.

### 7.4 Character summary and fallback contract

Reuse the exploration HUD's presentation fallbacks instead of creating slightly different Inventory behavior:

- blank/whitespace player name renders `Adventurer`;
- HP uses current value and effective maximum;
- MP is hidden when `MaxMana <= 0`, otherwise current/maximum is bound;
- EXP is hidden when `ExperienceToNext <= 0`, otherwise its range/value is bound without inventing a fake denominator;
- portrait is hidden only when its texture is unavailable;
- Gold keeps the existing Inventory copy exactly: `Gold: {value}`.

Effective ATK/DEF/SPD come from existing `Character.GetEffective*` methods so equipment changes immediately refresh the summary.

At compact size, name, level, HP, MP when supported, and Gold remain continuously readable. ATK/DEF/SPD and EXP stay reachable from Equipment without dropping essential text below the approved compact minimum.

### 7.5 Equipment and accessories

The five primary slots bind directly to their existing `EquipmentSlotType` values.

The accessory arc authors exactly `%AccessorySlot0` through `%AccessorySlot3`, matching `EquipmentSet.AccessorySlotCount`.

Visual mapping:

- populated primary/accessory slot → `Equipped`, actionable, `PresentItem`, existing tooltip/focus summary;
- empty primary slot → `Empty`, non-actionable, `PresentGlyph(UiArtCatalog.ForEquipmentSlot(slotType), ...)`;
- empty accessory slot → `Empty`, non-actionable, `PresentGlyph(UiIconId.Accessory, ...)`.

No `%AccessorySlot4` / `%AccessorySlot5` or permanent fake lock presentation remains.

`Locked` stays tested at the component level for a real future consumer, but this ticket does not pretend there is an unlock progression rule.

Activating an equipped slot invokes the existing unequip path. No new equipment transaction API is added.

### 7.6 Dynamic inventory catalogue

Replace `_inventorySlotEntries` and the 24 authored nodes with a dynamic list of `SiriusItemSlotController` instances under `%InventoryGrid`.

On refresh:

1. read `Player.Inventory.GetAllEntries()`;
2. sort by `Item.DisplayName` using `StringComparison.Ordinal`;
3. grow/reuse/shrink slots to match the current entry count;
4. bind item art with `PresentItem` and quantity;
5. map equipment to `Available` + actionable;
6. map supported out-of-battle consumables to `Available` + actionable;
7. map battle-only consumables and unsupported item categories to `Unsupported` + non-actionable with a readable reason;
8. remove extra visual slots when the count decreases.

The catalogue is inside a `ScrollContainer`; inventory capacity remains a domain concern.

A private screen-local binding from visual slot to current `InventoryEntry` is sufficient for activation. No item view model is introduced.

### 7.7 Screen-local focus identity and refresh restoration

Dynamic slots can be rebound to a different item or freed after equip/use. Therefore the screen must never remember a dynamic `Control` as semantic focus identity.

Use an in-instance key based only on existing identity:

```csharp
private readonly record struct InventoryFocusKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId);
```

Interpretation:

- primary equipment → `EquipmentSlot` only;
- accessory → `EquipmentSlotType.Accessory` + `AccessoryIndex`;
- inventory catalogue → `ItemId` only.

Before a mutation or responsive reflow, capture the current key and, for an inventory item, its current catalogue index. After `RefreshUI`/rebind:

1. resolve the same equipment/accessory/item identity when it still exists;
2. if an equipped inventory item disappeared because it moved into equipment, focus its resulting equipment slot (accessories use the existing slot-0 equip behavior unless the domain call changes);
3. if a consumed final item disappeared, focus the entry now occupying the previous index, otherwise the previous last entry;
4. if no matching content remains, use the active-page fallback.

After every refresh/rebind, explicitly refresh `%FocusSummary` from the resolved current focus. Do not rely on `FocusEntered` firing again when a surviving control stays focused.

This key is never persisted and never becomes selected-item state.

### 7.8 Focus summary

`%FocusSummary` is a passive `RichTextLabel`, updated by slot focus/mouse enter and active-skill focus/selection. It exposes the same description, quantity, bonuses, slot/category, effect, and availability reason that current tooltips expose.

It:

- does not survive screen destruction;
- does not control actions;
- does not add comparison;
- follows current focus/hover only;
- is re-pushed after catalogue rebind;
- never stores domain selection.

### 7.9 Responsive behavior

Use `SiriusUiMetrics.IsCompact` and `SafeFrameInsets`.

#### Standard

At 800×450 and above:

- compact page buttons hidden;
- Character/Equipment/Skills and Items visible together;
- Items receives flexible width and vertical scroll;
- content remains inside the 1600 px max-width safe frame;
- slots use 56×56 geometry.

#### Compact

Below 800 logical width or 450 logical height:

- a Settings-style three-button selector is visible;
- persistent identity strip remains visible;
- exactly one of Equipment, Items, or Skills is visible;
- the same content nodes are reused;
- slots use 48×48 geometry;
- supporting telemetry is reduced before essential state/action text.

Do not use a `TabContainer` as the content host because standard mode needs Equipment + Skills + Items visible simultaneously. The page buttons only control visibility.

### 7.10 Compact page navigation

Use scene-authored toggle buttons in one `ButtonGroup`, following the existing Settings controller pattern: named button handlers call one feature-local `SetCompactPage(InventoryPage)` method.

Input behavior:

- Mouse: click Equipment / Items / Skills.
- Keyboard/gamepad D-pad: normal focus navigation across the tab buttons; activation selects the page.
- Gamepad LB/RB: raw `JoyButton.LeftShoulder` / `RightShoulder` cycles pages while Inventory is visible and compact.
- No new `inventory_page_left/right` InputMap actions.

The existing `_Input` path already observes input hints while Inventory is visible. Shoulder handling remains in that controller callback and must work when root Inventory is hosted with `ProcessPolicy.WhenPaused`; tests exercise it with a paused SubViewport rather than moving page behavior into `GameplayPauseHostTest`.

Compact focus neighbours are explicit after each page/catalogue refresh:

- selected tab `ui_down` → first focusable control on its page;
- first focusable page control `ui_up` → that page's tab;
- final focusable control on the active page `ui_down` → Close;
- Close `ui_up` → final focusable control on the active page;
- tab left/right neighbours remain within the three-button selector.

Dynamic Items neighbours are recomputed after catalogue growth/shrink.

### 7.11 Initial focus

`InitialFocusTarget` resolves current visible content, not Close-first presentation.

Fallback order:

1. restorable identity on the active compact page / current standard layout;
2. first primary equipment slot for Equipment/standard;
3. first inventory slot for Items;
4. active-skill selector for Skills;
5. active compact page button;
6. Close.

No `Dictionary<InventoryPage, Control>` is used for dynamic catalogue identity.

### 7.12 Lifecycle integration

Keep `Game.SetupInventoryMenu`, `TryOpenInventory`, `TryCloseInventory`, parent handle behavior, external node lifetime, and `CloseRequested` ownership.

Change only:

```csharp
Hud = UIHudPolicy.Hidden,
InitialFocus = () => _inventoryMenu.InitialFocusTarget,
```

Root Inventory still owns tree pause and gameplay block. Pause-child Inventory inherits the already-paused parent and does not acquire another pause/gameplay-block lease.

`InventoryMenuController` never writes `SceneTree.Paused` and never consumes Cancel/toggle as a terminal action. `UIScreenHost` remains the lifecycle owner.

## 8. File ownership

### Create

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`
- `tests/ui/components/SiriusItemSlotControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Modify

- `resources/ui/theme/SiriusTheme.tres` — only proven slot Button variations.
- `scripts/ui/theme/SiriusThemeTypes.cs` — names for slot variations.
- `scripts/ui/theme/SiriusUiMetrics.cs` — one 56/48 slot-size helper.
- `tests/ui/theme/SiriusUiMetricsTest.cs` — slot metric contract.
- `scenes/ui/InventoryMenu.tscn` — responsive full-screen scene and exactly four accessories.
- `scripts/ui/InventoryMenuController.cs` — binding, dynamic slots, character fallback contract, compact navigation, identity-based focus restoration; existing domain calls stay here.
- `tests/ui/InventoryMenuControllerTest.cs` — migrate old `TextureButton`/heading assumptions and add parity/focus/navigation coverage.
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs` — migrate the Inventory art smoke to `%Icon` and four real accessories while retaining glyph-vs-item-art assertions.
- `scripts/game/Game.cs` — Inventory HUD policy only.
- `tests/game/GameplayPauseHostTest.cs` — direct/Pause-child host policy and parent restoration only.
- `tests/game/GameInputLifecycleTest.cs` — only if existing Inventory lifecycle assertions need path migration.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile Inventory evidence only if stale.

No `Character`, `Inventory`, `EquipmentSet`, save-format, skill-domain, generic UI host, or navigation framework file should need modification.

## 9. Test strategy

Use focused coverage rather than a viewport × input × item-state Cartesian matrix.

### Slot component

Cover:

- 56/48 sizing from `SiriusUiMetrics`;
- `PresentGlyph` keeps 32 px generated art centered without upscaling;
- `PresentItem` uses aspect-preserving scaled item art;
- Available emits one activation;
- Empty/Locked/Unsupported remain focusable but do not activate;
- Equipped and focused state can coexist;
- quantity/state labels update and clear without stale text;
- Button `Icon` is not used.

### Existing HPA-374 art contract

Migrate `Hpa374RuntimeSmokeTest.InventoryArtRendersAtVerificationSize` in the same atomic Inventory cutover:

- heading icons remain 24 px generated art;
- empty weapon/accessory `%Icon` textures remain 32 px and use `KeepCentered`;
- populated equipment uses the real item resource path and `KeepAspectCentered`;
- Close keeps binding-aware copy.

Do not keep the old fake `%AccessorySlot4` locked-state assertion.

### Inventory controller/domain parity

Preserve or add focused tests for:

- exact Gold copy (`Gold: 321`);
- blank-name → `Adventurer`;
- MP hidden when unsupported;
- EXP hidden when `ExperienceToNext <= 0`;
- deterministic alphabetical entry order;
- more than 24 item types render and are reachable;
- equipment activation equips through existing `TryEquip` path;
- equipment-slot activation unequips and returns the item to inventory;
- failed unequip return restores original equipment;
- out-of-battle consumable success removes one item and refreshes stats;
- failed consumable application attempts rollback;
- battle-only consumable is shown unavailable and cannot mutate inventory;
- active skill can be selected and explicitly cleared;
- the scene authors exactly `EquipmentSet.AccessorySlotCount` accessory controls and no fake extra positions;
- focus summary follows keyboard focus and mouse hover;
- activation that mutates catalogue restores focus by identity/fallback and never leaves focus on a freed/rebound semantic item;
- focus summary is refreshed after rebind even if the surviving control remains focused.

### Responsive scene and page input

Use all `SiriusUiMetrics.VerificationViewports` for fit smoke and the focus verification viewports for deeper checks.

At 1280×720 assert:

- standard heading/identity/Equipment/Skills/Items regions are visible;
- compact selector is hidden;
- content is inside safe frame;
- slots use 56×56;
- Items scroll has non-zero page size.

At 640×360 assert:

- compact selector is visible;
- exactly one content page is active;
- identity and Close remain usable;
- slots use 48×48;
- long focus summary wraps/scrolls without expanding the screen;
- tab ↔ page ↔ Close focus-neighbour path works;
- raw LB/RB cycles Equipment → Items → Skills without new InputMap actions;
- the shoulder path still works while the menu's process mode is `WhenPaused` and the scene tree is paused.

Shoulder/page tests live in `InventoryMenuControllerTest` / `InventoryMenuSceneTest`, not `GameplayPauseHostTest`.

### Host lifecycle

Update the real `Game.tscn` integration tests to prove:

- direct Inventory uses `UIHudPolicy.Hidden`, pauses the tree, blocks gameplay, and restores state on close;
- Pause-child Inventory keeps Pause as parent, keeps the tree paused, hides gameplay HUD while active, and restores Pause focus/HUD policy on close;
- `toggle_inventory` and Cancel still close only the topmost Inventory entry;
- reopening reuses the external Inventory view without stale handles;
- teardown leaves no Inventory entry or input/pause lease.

Do not test compact shoulder cycling in the host suite.

## 10. Risks and mitigations

### Risk: catalogue mutation leaves focus on freed or semantically rebound nodes

Use `InventoryFocusKey` (`EquipmentSlotType`, accessory index, item ID) plus previous catalogue index. Capture before mutation, resolve after rebind, and explicitly re-push the focus summary. Never use a dynamic `Control` as the semantic restoration key.

### Risk: icon rewrite regresses generated art sizing

Keep Button as the interactive root but move art to `%Icon` `TextureRect`; explicitly distinguish `PresentGlyph` (`KeepCentered`) from `PresentItem` (`KeepAspectCentered`) and migrate the HPA-374 smoke in the same task.

### Risk: compact layout duplicates content

Use one content tree and visibility selection. Do not use a second compact controller or a `TabContainer` that would hide standard-mode siblings.

### Risk: compact keyboard navigation strands focus on the tab row or page

Recompute explicit tab ↔ first page target ↔ last page target ↔ Close neighbours after page changes and dynamic catalogue refreshes.

### Risk: fake locked accessory presentation survives the redesign

Author exactly four accessory slots, matching `EquipmentSet.AccessorySlotCount`. Keep the generic Locked visual only as a component-level contract.

### Risk: HPA-357 expands into HPA-375

Focus identity is transient restoration infrastructure only. No persistent selected-item state, comparison, filters, user ordering, or new actions.

### Risk: host behavior is accidentally rewritten

Treat `TryOpenInventory` as an existing contract. Change HUD policy only; `InitialFocusTarget` continues to plug into the existing host spec.

## 11. Acceptance mapping

- Clear character/equipment/accessory/inventory/consumable/skill hierarchy → responsive full-screen scene + identity strip + three content areas.
- Preserve equip/unequip/consume/quantity/skill behavior → existing controller operations retained and parity-tested.
- Alphabetical ordering → same ordinal sort before dynamic slot binding.
- Every current item type visible → grow/reuse/shrink catalogue, no authored capacity cap.
- Consistent slot states → `SiriusItemSlot` normal/equipped/unavailable Theme plus glyph/item-art contract.
- Keyboard/gamepad traversal → focusable Button root, focus summary, Settings-style compact selector, explicit neighbours, raw shoulders.
- Narrow layout → compact Equipment/Items/Skills visibility pages in the same scene tree.
- Host restoration → existing `UIScreenHost` flow with HUD-hidden correction.
- Existing art behavior → HPA-374 smoke migrated atomically.
- No fake accessory progression → exactly four authored accessory slots.
- All approved viewports → shared verification viewport sources.

## 12. Design self-review

The proposal intentionally adds only one reusable leaf because there are three concrete consumers of the same slot geometry. Everything else stays feature-local.

The review-driven changes tighten existing contracts rather than add architecture:

- icon art keeps the tested native-glyph/scaled-item distinction;
- dynamic focus restoration uses stable in-instance identities, not raw `Control` memory;
- old Inventory/HPA-374 tests migrate in the same atomic screen cutover;
- compact navigation copies the existing Settings page-button pattern and adds only raw shoulder handling;
- accessory presentation matches the four-slot domain reality;
- identity/EXP/MP/Gold presentation matches existing HUD/Inventory fallbacks.

The design still does not introduce HPA-375 behavior, a generic presenter/renderer, a navigation service, or any domain/save-format change.