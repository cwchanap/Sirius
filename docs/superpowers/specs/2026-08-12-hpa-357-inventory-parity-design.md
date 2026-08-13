# HPA-357 Inventory and Equipment Parity Design

**Status:** Planning candidate  
**Linear:** HPA-357 — Redesign Sirius inventory and equipment screen with feature parity  
**Date:** 2026-08-12

## 1. Decision summary

Replace the fixed editor-like Inventory workbench with one scene-authored, full-screen Sirius surface while preserving the existing inventory, equipment, consumable, active-skill, pause, and rollback rules.

Keep the architecture deliberately small:

- one `InventoryMenuController`; no presenter, view model, repository/facade, generic collection renderer, or navigation service;
- `Game` / gameplay `UIScreenHost` remain lifecycle owners;
- one reusable `SiriusItemSlot` leaf for primary equipment, accessories, and dynamic inventory entries;
- a dynamic current-entry catalogue rather than 24 authored slots or 100 placeholders;
- one scene tree; compact mode uses visibility-selected Equipment / Items / Skills pages;
- one small shared player-summary presenter because Exploration HUD and Inventory are now two concrete consumers of the same name/HP/MP/EXP fallback rules;
- screen-local semantic focus restoration for catalogue mutations; no persistent selected-item model;
- spatial Godot focus navigation first; explicit focus-neighbour overrides only if behavioural tests prove a specific boundary fails.

HPA-375 comparison, filtering, persistent selection, and user sorting remain out of scope.

## 2. Current-state findings

### 2.1 Presentation is fixed and split between scene and controller

`scenes/ui/InventoryMenu.tscn` currently uses a fixed 1240×760 centred panel, local `StyleBoxFlat` resources, a two-pane `HSplitContainer`, six accessory placeholders, and 24 authored inventory slots.

`InventoryMenuController` then overrides slot geometry to 108×108 panels / 96×96 buttons and creates base/equipped/locked `StyleBoxFlat` instances in C#. HPA-357 moves geometry and styling back into the shared Theme / scene system.

### 2.2 Domain operations are already narrow enough

Keep the existing domain seams:

- entries come from `Character.Inventory`;
- ordering is ordinal `DisplayName` sorting;
- equip delegates to `Character.TryEquip`;
- unequip delegates to `Character.Unequip`, retaining the existing inventory-return rollback;
- consumable use removes first, applies the effect, and attempts rollback on failure;
- battle-only consumables remain unavailable outside battle;
- active-skill selection delegates to `Character.EquipActiveSkill`, including explicit `None`.

Do not move those rules to a new layer.

### 2.3 The fixed catalogue is a real parity limitation

`Inventory.MaxItemTypes` supports up to 100 item types while the current UI displays only the 24 authored slot nodes and warns that extra entries are hidden.

HPA-357 removes only that presentation cap. It creates/reuses one slot per current entry under a scroll container; it does not change inventory capacity.

### 2.4 Slot art already has a tested two-mode contract

Current Inventory/HPA-374 coverage distinguishes:

- generated slot/type glyphs: 32 px native art, centered without upscaling;
- populated item art: aspect-preserving scaling inside the slot.

`UiIconPresenter` already owns this distinction for the legacy `TextureButton` path. HPA-357 should move that ownership to `TextureRect` rather than hand-implement the same rule inside the new slot and leave dead `TextureButton` presenter APIs behind.

Add two narrow APIs:

```csharp
public static bool ApplyGlyph(TextureRect target, UiIconId id, UiIconSize size);
public static void ApplyItem(TextureRect target, Texture2D? texture);
```

`ApplyGlyph` loads the generated icon, uses `ExpandMode.IgnoreSize`, and sets `KeepCentered`. `ApplyItem` uses `ExpandMode.IgnoreSize` and `KeepAspectCentered`.

After Inventory migrates, delete the now-unused `UiIconPresenter` `TextureButton` overload/helpers (`Apply(TextureButton, ...)`, `ApplyTexture`, `ApplyGlyphTexture`, `SetSlotTextures`). Keep the existing general `Apply(TextureRect, ...)` and `Apply(Button, ...)` APIs for their current consumers.

### 2.5 Host integration already exists

`Game.TryOpenInventory` already owns root-vs-Pause parentage, pause policy, gameplay block, cursor, topmost Cancel / `toggle_inventory`, external-node lifetime, and focus restoration through `UIScreenHost`.

The only host-policy change remains:

```csharp
Hud = UIHudPolicy.Hidden,
```

Do not add an Inventory host wrapper/factory.

### 2.6 Four accessory slots exist, but current Inventory only targets slot 0

`EquipmentSet.AccessorySlotCount` is `4`, and `Character.TryEquip(..., accessorySlot)` / `EquipmentSet.TryEquip(..., accessoryIndex)` already support all four indices. Current `InventoryMenuController.EquipFromInventory` uses the two-argument overload, so accessories always target index 0; a second accessory replaces the first even while slots 1–3 remain empty.

The redesign should not replace two fake locked placeholders with three visually reachable but functionally unreachable slots. This is a UI routing defect, not a new domain system.

For an accessory equipped from Inventory:

1. choose the first empty accessory index `0 .. AccessorySlotCount - 1`;
2. if all are occupied, fall back to index 0 to preserve the existing replacement behavior;
3. pass that index to the existing `Character.TryEquip` overload;
4. restore post-mutation focus to that exact accessory slot.

No unlock progression, new domain API, or persistence change is introduced.

### 2.7 Player-summary fallback rules already have a second consumer

`ExplorationHudController.ApplyPlayerState` already defines tested presentation rules for:

- blank name → `Adventurer`;
- HP current/maximum binding;
- MP visibility only when `MaxMana > 0`;
- EXP visibility only when `ExperienceToNext > 0`, with clamped value.

Inventory needs the same rules. Duplicating them in `RefreshCharacterSummary` would create two policies that can drift.

Add one small static `SiriusPlayerSummaryPresenter` that applies an existing `ExplorationHudPlayerState` to already-bound common controls (`Label` name/level, `SiriusStatBar` HP/MP, `ProgressBar` EXP). `ExplorationHudController.ApplyPlayerState` and `InventoryMenuController` both call it. Do not extract a new IdentityStrip scene/component.

Inventory still owns its additional effective ATK/DEF/SPD and exact `Gold: {value}` copy.

## 3. Goals

1. Ship a responsive RPG-style character/equipment/inventory/active-skill screen using the existing Sirius Theme.
2. Preserve current equip/unequip/consume/rollback/active-skill semantics.
3. Make all four existing accessory slots reachable using the existing indexed equip API.
4. Show every current inventory item type instead of truncating after 24 entries.
5. Preserve the HPA-374 native-glyph versus scaled-item-art contract.
6. Share the existing HUD name/HP/MP/EXP fallback policy rather than duplicate it.
7. Make tooltip/detail information reachable through mouse, keyboard, and gamepad focus.
8. Provide deliberate compact Equipment / Items / Skills navigation at 640×360.
9. Preserve valid focus after catalogue mutation/reflow without persistent selection state.
10. Keep lifecycle, pause, Cancel, cursor, HUD, and parent restoration owned by `UIScreenHost`.

## 4. Non-goals

HPA-357 does not add:

- persistent selected-item state;
- equipment comparison;
- category filters;
- user sorting;
- Drop, Sell, Favourite, Lock, or bulk actions;
- accessory unlock/progression rules;
- battle item-selection redesign;
- passive-skill editing beyond current supported behavior;
- inventory persistence changes;
- new InputMap actions for page cycling;
- a generic collection renderer;
- a generic screen presenter/view-model;
- a navigation service;
- new art assets;
- HPA-375 behavior.

## 5. Chosen architecture

### 5.1 Ownership

`Game`
: Keeps `UIScreenHost` registration/lifecycle ownership. HPA-357 changes only Inventory HUD policy to Hidden.

`InventoryMenuController`
: Binds player state, owns compact page state, creates/rebinds dynamic catalogue slots, owns ephemeral focus summary and screen-local semantic focus restoration, chooses the existing accessory index for accessory equip, and invokes existing domain operations.

`SiriusItemSlotController`
: Owns one slot's visual state, glyph/item-art presentation, labels, tooltip, focusability, and guarded activation. It has no `Item`, `Character`, `Inventory`, `EquipmentSet`, or `GameManager` dependency.

`SiriusPlayerSummaryPresenter`
: Owns only the already-shared name/level/HP/MP/EXP presentation policy. It does not own character state, mutation, scene structure, focus, or Inventory-specific statistics.

`Character`, `Inventory`, `EquipmentSet`, `SkillCatalog`
: Remain domain owners.

### 5.2 Slot component

Create:

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`

The root is a focusable `Button` with passive `%Icon: TextureRect`, `%QuantityLabel`, and `%StateLabel` children.

Use four states only:

```csharp
public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Unsupported
}
```

There is no production `Locked` consumer after fake accessory placeholders are removed; adding that enum later is trivial if a real consumer appears.

`Actionable` is derived, never passed independently:

```csharp
public bool Actionable =>
    _state is SiriusItemSlotVisualState.Available
        or SiriusItemSlotVisualState.Equipped;
```

Public binding surface:

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

`PresentGlyph` delegates to `UiIconPresenter.ApplyGlyph`; `PresentItem` delegates to `UiIconPresenter.ApplyItem`.

Empty/Unsupported remain natively focusable and non-disabled, but `Pressed` emits `Activated` only when `Actionable` is true.

The Theme still needs exactly three Button variations: normal, equipped, unavailable. The enum and Theme names are closed contracts and are pinned in `SiriusUiContractsTest`.

`SiriusUiMetrics` gains exactly:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

### 5.3 Scene structure

Inventory remains a full-screen SafeFrame screen, not `SiriusModalShell`.

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
            └── Footer/CloseButton
```

Stable heading names remain `%EquipmentTitleIcon`, `%EquipmentTitleLabel`, `%InventoryTitleIcon`, `%InventoryTitleLabel` because existing tests/HPA-374 smoke consume them.

Author five primary equipment slots and exactly `%AccessorySlot0` through `%AccessorySlot3`. `%InventoryGrid` starts empty.

Reuse the same hero sheet `AtlasTexture` crop `Rect2(0, 0, 96, 96)` as Exploration HUD.

`%FocusSummary` is a plain `Label` with `AutowrapMode.WordSmart`; no BBCode is produced, so `RichTextLabel` adds parsing/escaping surface without value. Keep its container bounded so long text cannot enlarge the screen.

### 5.4 Shared player summary

Inventory creates the existing `ExplorationHudPlayerState` from current player values (using effective max HP) and calls the same `SiriusPlayerSummaryPresenter.Apply(...)` used by Exploration HUD.

Inventory then adds:

```text
ATK = player.GetEffectiveAttack()
DEF = player.GetEffectiveDefense()
SPD = player.GetEffectiveSpeed()
Gold = $"Gold: {player.Gold}"
```

No duplicate blank-name, MP visibility, EXP visibility, or EXP clamp policy remains in Inventory.

### 5.5 Equipment and accessories

Primary slot mapping:

- populated → `Equipped`, item art, actionable;
- empty → `Empty`, type glyph, non-actionable.

Accessory mapping:

- populated → `Equipped`, item art, actionable;
- empty → `Empty`, accessory glyph, non-actionable.

Accessory equip target:

```text
first empty accessory index
else index 0 when all four are occupied
```

This makes all four authored slots real without adding an unlock system.

### 5.6 Dynamic inventory catalogue

Replace `_inventorySlotEntries` and 24 authored catalogue nodes with a dynamic list of `SiriusItemSlotController` instances.

Refresh algorithm:

1. read `Player.Inventory.GetAllEntries()`;
2. sort by `DisplayName` with `StringComparison.Ordinal`;
3. grow/reuse/shrink slot nodes to equal current entry count;
4. rebuild the refresh-scoped slot → `InventoryEntry` map;
5. bind equipment and supported out-of-battle consumables as `Available`;
6. bind battle-only/unsupported items as `Unsupported` with readable reason;
7. remove extra visual nodes when the count shrinks.

The slot → `InventoryEntry` map is valid only until the next catalogue refresh. Never retain an `InventoryEntry` from that map across an Inventory mutation; the domain owns those live mutable entries.

### 5.7 Semantic focus restoration

Dynamic controls may be rebound or freed. Semantic focus identity is therefore:

```csharp
private readonly record struct InventoryFocusKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId);
```

Before mutation/reflow, capture the key and current catalogue index. After refresh:

1. restore the same equipment/accessory/item identity if it still exists;
2. after equipment equip, focus the resulting equipment slot;
3. after accessory equip, focus the exact chosen accessory index;
4. after consuming the last copy of an item, focus the entry now at the previous index, else the previous last item;
5. otherwise use active-page fallback.

After rebind, explicitly recompute `%FocusSummary` from the current focus even if `FocusEntered` did not fire.

This state is per screen instance and is not HPA-375 selected-item state.

### 5.8 Compact pages and input

Follow Settings' page-button model: one scene-authored `ButtonGroup`, named handlers, one `SetCompactPage(InventoryPage)` method. Do not use a `TabContainer` as the content host because standard Inventory shows Equipment + Skills + Items simultaneously.

Input:

- mouse: click page button;
- keyboard / D-pad: normal Godot spatial focus + activate button;
- gamepad LB/RB: raw shoulder buttons cycle pages while visible + compact;
- no new `inventory_page_*` InputMap actions.

Do **not** pre-build a `LinkVertical`/focus-neighbour maintenance layer. First assert behavior through actual `SubViewport.PushInput`:

- focused page tab + `ui_down` reaches first page control;
- first page control + `ui_up` returns to the page tab;
- last page control + `ui_down` reaches Close.

If one of those concrete boundaries fails after the scene exists, add only the minimum explicit `FocusNeighbor*` assignment needed for that boundary. Do not introduce a generic focus-link helper or recompute-all-neighbours invariant unless a failing test proves it necessary.

### 5.9 Lifecycle integration

Keep `Game.SetupInventoryMenu`, `TryOpenInventory`, `TryCloseInventory`, external node lifetime, parent handle behavior, `CloseRequested`, Cancel/toggle ownership, and `InitialFocus = () => _inventoryMenu.InitialFocusTarget`.

Change only Inventory HUD policy to Hidden.

Root Inventory continues to own tree pause/gameplay block through the host. Pause-child Inventory inherits the paused parent and does not take a second pause/block lease.

## 6. File ownership

### Create

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`
- `scripts/ui/SiriusPlayerSummaryPresenter.cs`
- `tests/ui/components/SiriusItemSlotControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Modify

- `scripts/ui/art/UiIconPresenter.cs` — add TextureRect glyph/item APIs; remove dead TextureButton slot APIs after Inventory migration.
- `resources/ui/theme/SiriusTheme.tres` — three slot Button variations only.
- `scripts/ui/theme/SiriusThemeTypes.cs` — three typed slot variation names.
- `scripts/ui/theme/SiriusUiMetrics.cs` — one 56/48 slot metric.
- `tests/ui/theme/SiriusUiMetricsTest.cs` — slot metric.
- `tests/ui/theme/SiriusUiContractsTest.cs` — pin four-state enum and three Theme names.
- `scripts/ui/ExplorationHudController.cs` — delegate common fallback binding to `SiriusPlayerSummaryPresenter`.
- `tests/ui/ExplorationHudControllerTest.cs` — retain/adjust common fallback regressions around the shared presenter.
- `scenes/ui/InventoryMenu.tscn` — full responsive rewrite; four real accessory slots.
- `scripts/ui/InventoryMenuController.cs` — scene binding, dynamic catalogue, accessory target selection, focus identity, compact page behavior.
- `tests/ui/InventoryMenuControllerTest.cs` — parity/catalogue/accessory/focus/summary migration.
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs` — migrate art smoke to `%Icon` `TextureRect` and four real accessories.
- `scripts/game/Game.cs` — Inventory `Hud` policy only.
- `tests/game/GameplayPauseHostTest.cs` — Inventory host policy/restoration.

### Audit-only unless stale evidence exists

- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

No `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain production file changes are expected.

## 7. Test strategy

### Slot/presenter contracts

Cover:

- four and only four `SiriusItemSlotVisualState` values;
- exact three stable Theme names;
- 56/48 slot metric;
- `ApplyGlyph(TextureRect)` keeps generated 32 px glyphs native/centered;
- `ApplyItem(TextureRect)` scales item art aspect-preserving;
- `Actionable` is true only for Available/Equipped;
- Empty/Unsupported remain focusable but do not activate;
- labels clear stale content;
- shared player presenter retains Adventurer / MP / EXP / clamp behavior through existing HUD tests.

### Inventory parity

Cover:

- exact `Gold: 321` copy;
- shared HUD fallbacks visible through Inventory;
- deterministic alphabetical catalogue;
- >24 current item types render;
- primary equipment equip/unequip and existing rollback;
- first accessory fills slot 0, second fills slot 1 without replacing slot 0;
- with all four accessories occupied, the next accessory uses existing slot-0 replacement behavior;
- focus after accessory equip lands on the chosen index;
- consumable success/failure rollback;
- battle-only item unavailable;
- active skill select/explicit None;
- mutation focus restoration and summary refresh;
- `_inventoryEntryBySlot` is treated as refresh-scoped only.

### Responsive/keyboard/gamepad

Use `SiriusUiMetrics.VerificationViewports` for fit smoke and focus viewports for deeper checks.

At 640×360 use actual `SubViewport.PushInput` to prove:

- exactly one compact page visible;
- tab + `ui_down` reaches first page control;
- first page control + `ui_up` returns to tab;
- final page control + `ui_down` reaches Close;
- LB/RB cycles pages while Inventory is `ProcessMode.WhenPaused` and tree paused.

Tests assert focus outcomes, not `FocusNeighbor*` properties.

### HPA-374 / host

Migrate HPA-374 Inventory smoke atomically with the scene rewrite. Preserve heading sizes, native glyph size/stretch, real item asset/stretch, and binding-aware Close copy.

Update real Game host tests for `UIHudPolicy.Hidden` in direct and Pause-child Inventory; keep compact page tests out of host suite.

## 8. Implementation shape

Three reviewable slices:

1. **Slot/shared-presentation seam:** `SiriusItemSlot`, Theme/metric contracts, `UiIconPresenter` TextureRect APIs, dead TextureButton presenter removal, and shared player-summary presenter.
2. **Atomic Inventory cutover:** scene rewrite + old/HPA-374 test migration + dynamic catalogue + all four accessory targeting + compact pages + semantic focus restoration. End this task with the full test suite because it replaces a large `.tscn` used by the real game.
3. **Host policy/final verification:** change only Inventory HUD policy in `Game`, run lifecycle/full-suite/build/stale-path/scope audits.

## 9. Risks and mitigations

### Dynamic catalogue focus drift

Use semantic keys + previous index, never raw dynamic Control identity. Explicitly refresh summary after rebind.

### Accessory UI lies about usable slots

Select the first empty existing accessory index; fall back to slot 0 only when full. No new domain rule.

### Shared HUD/Inventory fallback drift

Move common fallback application to one tiny static presenter now that a second concrete screen consumes it; do not extract a scene framework.

### Focus-neighbour maintenance creep

Test spatial behavior first. Add only evidence-backed boundary overrides; no generic neighbour graph.

### Art regression / dead presenter path

Move glyph/item distinction into `UiIconPresenter` TextureRect APIs, migrate HPA-374 smoke in the same task, and delete/audit the legacy TextureButton slot presenter path.

### HPA-357 expands into HPA-375

Keep focus summary passive and semantic focus restoration transient. No selected-item domain/view model exists.

## 10. Acceptance mapping

- Clear character/equipment/accessory/inventory/skill hierarchy → full-screen responsive scene.
- Four real accessory slots → existing indexed `TryEquip` first-empty routing.
- Existing domain behavior → current controller operations retained and parity-tested.
- Alphabetical/full catalogue → current ordinal sort + dynamic grow/reuse/shrink.
- Consistent slot states → four-state leaf + three Theme variations.
- Keyboard/gamepad information → focusable slot root + plain wrapped focus summary.
- Compact usability → Settings-style page buttons + behavioral spatial focus tests + raw shoulders.
- Shared identity rules → one common player-summary presenter used by HUD and Inventory.
- Host restoration → existing `UIScreenHost`, only HUD policy corrected.
- HPA-374 art behavior → new TextureRect presenter APIs + migrated smoke.
- No speculative framework → explicit non-goals and small file boundaries.

## 11. Design self-review

The architecture remains one controller and one new UI leaf. The new shared player-summary helper and TextureRect presenter methods are justified by second concrete consumers and replace duplicated/dead code rather than establish frameworks.

The latest review reduces surface area: no `Locked` enum member, no independent `actionable` parameter, no pre-emptive focus-neighbour machinery, and no `RichTextLabel` for plain detail text. Against that, it fixes two real current behaviors: shared HUD fallback ownership and reachability of the four accessory indices that the domain already supports.
