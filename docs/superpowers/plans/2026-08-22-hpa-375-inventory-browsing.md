# HPA-375 Inventory Browsing Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit Inventory selection, item details, equipment comparison, category filters, deterministic sorting, and contextual actions without changing inventory/equipment domain rules.

**Architecture:** Extend the existing `InventoryMenuController` and `InventoryMenu.tscn` in place. Selection/filter/sort remain private screen-instance presentation state; live item data is re-resolved from current Inventory/equipment instead of retained in a new browser model. Existing equip/unequip/use/skill operations remain authoritative.

**Tech Stack:** Godot 4.6.2, C# / .NET 8.0, GdUnit4, existing Sirius Theme/UI art/components.

**Spec:** `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md`

## Global Constraints

- Keep implementation on this HPA-375 branch/PR; do not open a second PR for the ticket.
- Keep one `InventoryMenuController`; no browser/view-model/service/repository layer.
- Reuse `SiriusItemSlotController`, `UiArtCatalog`, `UiIconPresenter`, `UIScreenHost`, Theme, and existing domain operations.
- Selection/filter/sort are presentation-only and are not persisted.
- Slot press selects; only `%DetailsActionButton` invokes Equip, Unequip, or Use.
- `ActiveSkillSelector` keeps current assignment behavior.
- Comparison and Equip use the same `ResolveAccessoryEquipIndex()` rule.
- No Drop/Sell/Favourite/Lock/bulk actions, search, extra sort modes, equipment requirements, Theme tokens, host APIs/kinds, or compatibility work.
- Preserve current rollback, battle-only, active-skill, host, pause/input, HUD, and save semantics.
- Extend existing Inventory tests; do not add a new E2E/screenshot harness.

## File Map

### Production

- `scenes/ui/InventoryMenu.tscn` — Details page, filter/sort toolbar, fourth compact tab.
- `scripts/ui/InventoryMenuController.cs` — selection, filtering/sorting, details/comparison, contextual actions, reconciliation.

### Tests

- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Audit only

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Only edit an audit-only file if current wording/assertions become stale.

---

### Task 1: Make selection explicit and move mutation behind Details

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Produces:** private semantic selection, authored Details page/tab, one contextual action button, mutation-selection reconciliation.

- [ ] **Step 1: Add failing tests proving slot press selects without mutating**

Add these controller-level contracts before production edits:

```csharp
[TestCase]
public void PressingInventoryEquipment_SelectsWithoutEquipping()
{
    var player = _gameManager.Player;
    var candidate = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
    var before = player.Equipment.GetEquipped(EquipmentSlotType.Weapon);
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(candidate.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);

    AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(before);
    AssertThat(player.Inventory.ContainsItem(candidate.Id)).IsTrue();
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(candidate.DisplayName);
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
        .IsEqual("Equip");
}

[TestCase]
public void PressingEquippedSlot_SelectsWithoutUnequipping()
{
    var player = _gameManager.Player;
    var equipped = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryEquip(equipped, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    GetSlot("%WeaponSlot").EmitSignal(Button.SignalName.Pressed);

    AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(equipped);
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(equipped.DisplayName);
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
        .IsEqual("Unequip");
}
```

Also add an unsupported `GeneralItem` selection test: Details renders name/category/rarity/quantity/description, action is hidden, reason contains `No inventory action`, and quantity is unchanged.

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: FAIL because Details does not exist and current slot activation mutates immediately.

- [ ] **Step 2: Author the Details surface and fourth compact tab**

In `InventoryMenu.tscn`:

- add `%DetailsTab` to the current compact `ButtonGroup` after Skills;
- add `%DetailsPage` under `%ResponsiveContent`, beside `%CharacterColumn` and `%ItemsPage`;
- use the existing outer `%PageScroll` as scroll owner;
- author `%DetailsIcon`, `%DetailsName`, `%DetailsMeta`, `%DetailsBody`, `%DetailsComparison`, `%DetailsActionReason`, `%DetailsActionButton`;
- use existing Theme variations only;
- give `%DetailsActionButton` a 44 px minimum height;
- start comparison/reason/action hidden;
- put `Select an item or equipped slot to view details.` in the empty Details body;
- keep `%FocusSummary` and global Close footer unchanged.

Standard layout shows Character + Items + Details. Compact shows one of Equipment / Items / Skills / Details.

- [ ] **Step 3: Add semantic selection state; never retain a selected `InventoryEntry`**

Add:

```csharp
private readonly Dictionary<string, InventoryEntry> _visibleInventoryEntryByItemId =
    new(StringComparer.Ordinal);

private InventorySelectionKey? _selection;
private int? _pendingSelectionFallbackIndex;

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

Extend `InventoryPage` with `Details`. Bind the new scene nodes in `BindNodes()` and Details tab/action signals once for the controller lifetime.

- [ ] **Step 4: Change Inventory-owned slots from activation commands to selection buttons**

For each equipment/accessory/inventory slot:

- set `ToggleMode = true`;
- stop subscribing the screen to `SiriusItemSlotController.Activated`;
- subscribe to normal `Pressed` and update `_selection` only;
- keep existing focus/hover summary handlers.

Representative equipment binding:

```csharp
private void AddEquipmentSlot(string slotPath, EquipmentSlotType slotType)
{
    var slot = GetNode<SiriusItemSlotController>(slotPath);
    slot.ToggleMode = true;
    slot.Pressed += () => SelectEquipmentSlot(slotType, null);
    slot.FocusEntered += () => PresentFocusSummary(slot.TooltipText);
    slot.MouseEntered += () => PresentFocusSummary(slot.TooltipText);
    _equipmentSlots[slotType] = slot;
}
```

Accessories capture their index. Dynamic inventory slots call `SelectInventorySlot(slot)`. Do not modify `SiriusItemSlotController` itself.

Delete `OnInventorySlotActivated`, `OnEquipmentSlotActivated`, and `OnAccessorySlotActivated` once no signal references them.

- [ ] **Step 5: Render Details from live data and normalize selected visuals**

Add private helpers:

```csharp
private void SelectInventorySlot(SiriusItemSlotController slot);
private void SelectEquipmentSlot(EquipmentSlotType slotType, int? accessoryIndex);
private void RefreshSelectionDetails();
private void RefreshSelectionVisuals();
private void ReconcileSelectionAfterCatalogueRefresh();
private void ClearSelection();
```

`RefreshInventoryCatalogue()` rebuilds `_visibleInventoryEntryByItemId` with the existing slot maps. `RefreshSelectionDetails()` re-resolves inventory selection through that map and equipment selection through live `Player.Equipment`.

Render only current domain fields:

- name;
- category and rarity;
- inventory quantity;
- description;
- consumable effect + battle-only restriction;
- equipment slot + ATK/DEF/SPD/HP bonuses;
- concrete equipped slot identity.

For icon, use item art when present, otherwise existing category/equipment-slot glyphs through `UiIconPresenter`.

`RefreshSelectionVisuals()` sets `ButtonPressed` true only for the selected semantic key and false everywhere else. Programmatic normalization is authoritative over Button auto-toggle state.

If a key no longer resolves, clear it unless `_pendingSelectionFallbackIndex` was intentionally set by a successful consumable mutation.

- [ ] **Step 6: Put Equip/Unequip/Use behind `%DetailsActionButton`**

Use one handler:

```csharp
private void OnDetailsActionPressed()
{
    if (_selection is not { } selection)
        return;

    if (selection.ItemId is { } itemId &&
        _visibleInventoryEntryByItemId.TryGetValue(itemId, out var entry))
    {
        if (entry.Item is EquipmentItem equipment)
            EquipFromInventory(equipment);
        else if (entry.Item is ConsumableItem consumable && !IsBattleOnly(consumable))
            UseConsumableOutOfBattle(consumable);
        return;
    }

    if (selection.EquipmentSlot is { } slotType)
        HandleUnequip(slotType, selection.AccessoryIndex ?? 0);
}
```

Details action mapping:

| Selection | Button | Reason when no button |
| --- | --- | --- |
| Inventory equipment | Equip | — |
| Equipped item | Unequip | — |
| Non-battle consumable | Use | — |
| Battle-only consumable | hidden | `Can only be used in battle.` |
| General / Quest / unsupported | hidden | `No inventory action is available for this item.` |
| None | hidden | neutral Details copy |

Do not add confirmations for existing one-press actions.

- [ ] **Step 7: Reconcile selection inside existing mutation paths without changing domain order**

`EquipFromInventory`:

- keep remove → `TryEquip` → replaced-item return ordering;
- remove the old `_pendingFocusRestore` redirect that existed because activation started on the catalogue slot;
- on success select the resulting main slot or exact accessory index immediately before `RefreshUI()`;
- on failure/rollback retain inventory selection.

`HandleUnequip`:

- keep current unequip → inventory return → equipment rollback ordering;
- on successful inventory return select `removed.Id`;
- on rollback retain equipped selection.

`UseConsumableOutOfBattle`:

- before successful removal, resolve current visible index through `_inventorySlotByItemId` + `_inventorySlots.IndexOf` and store it in `_pendingSelectionFallbackIndex`;
- clear that pending index on remove/apply failure;
- after success keep the same item ID and call existing `RefreshUI()`;
- if quantity remains, reconciliation keeps the item and clears the pending index;
- if final quantity disappeared, select visible item at `min(previousIndex, count - 1)` or clear when count is zero.

This replaces the old direct-activation focus redirect; normal HPA-357 semantic focus restoration remains for actual slot-focused refreshes.

- [ ] **Step 8: Update old mutation/focus tests to the new explicit action contract**

Add:

```csharp
private Button DetailsActionButton() =>
    _inventoryMenu.GetNode<Button>("%DetailsActionButton");
```

Each equip/unequip/use parity test now performs:

```csharp
slot.EmitSignal(Button.SignalName.Pressed);        // select only
DetailsActionButton().EmitSignal(Button.SignalName.Pressed); // mutate
```

Keep existing rollback assertions intact.

Rename/update the HPA-357 expectations that are no longer correct:

- `AccessoryEquip_FillsFirstEmptySlotAndFocusesIt` → assert first empty slot is filled **and selected** (`ButtonPressed`/Details now identifies that accessory), not focused;
- `EquipActivation_RestoresFocusToResultingEquipmentSlot` → assert slot press does not equip, Details action equips, and resulting equipment is selected;
- `ConsumingFinalItem_RestoresFocusToNextCatalogueEntry` → assert final use selects the next catalogue item; Task 4 separately pins focus behavior.

Keep `Refresh_RePushesSummaryWhenFocusedSlotSurvives`; FocusSummary remains distinct from selection.

- [ ] **Step 9: Update scene tests for four logical pages and run both Inventory suites**

Include `%DetailsPage` in `VisiblePageCount()`. Standard expects Equipment, Skills, Items, and Details visible through Character + two side pages. Compact expects exactly one of four.

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 10: Commit Task 1**

```bash
git add scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add explicit inventory selection and details actions"
```

---

### Task 2: Add category filtering and deterministic sorting

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Produces:** `All/Equipment/Consumable/General/Quest` filters and `Name/Category` sort, with semantic selection reconciliation.

- [ ] **Step 1: Add failing sort/filter tests, including an observable ID tie-break**

Add:

```csharp
private SiriusItemSlotController[] VisibleInventorySlots() =>
    _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();
```

For the final Name-sort ID tie, use two same-category/same-name equipment items whose existing catalog art differs, so order is externally observable without exposing item IDs in UI:

```csharp
[TestCase]
public void NameSort_UsesIdAsFinalOrdinalTieBreak()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var first = EquipmentCatalog.CreateWoodenSword();
    first.Id = "a-tie";
    first.DisplayName = "Same";
    var second = EquipmentCatalog.CreateIronSword();
    second.Id = "b-tie";
    second.DisplayName = "Same";
    AssertThat(player.TryAddItem(second, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(first, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var slots = VisibleInventorySlots();
    AssertThat(slots[0].GetNode<TextureRect>("%Icon").Texture!.ResourcePath)
        .IsEqual(first.AssetPath);
    AssertThat(slots[1].GetNode<TextureRect>("%Icon").Texture!.ResourcePath)
        .IsEqual(second.AssetPath);
}
```

Also test:

- Name sort: ordinal `DisplayName` then Category then ID;
- Category sort: Category numeric order (`General`, `Equipment`, `Consumable`, `Quest`) then ordinal name then ID;
- All + each category filter returns only matching current entries.

- [ ] **Step 2: Add failing selection reconciliation tests**

Pin:

- selected item stays selected after changing Name → Category sort;
- filter that still includes selected item preserves it;
- filter that hides selected inventory item clears Details and pressed visual;
- equipped-item selection is unaffected by inventory filter.

Run the controller suite and confirm these fail because browse controls do not exist yet.

- [ ] **Step 3: Author `%InventoryFilter` and `%InventorySort` above InventoryScroll**

Add one `HBoxContainer` in `%ItemsContent`, between title and scroll, containing two expanding 44 px `OptionButton`s. No new reusable toolbar.

- [ ] **Step 4: Add private browse state and initialize controls before signal binding**

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

private InventoryFilter _inventoryFilter = InventoryFilter.All;
private InventorySort _inventorySort = InventorySort.Name;
```

Populate exact labels `All`, `Equipment`, `Consumable`, `General`, `Quest` and `Name`, `Category` after `BindNodes()` and before connecting `ItemSelected`, so initialization does not trigger refresh handlers.

Do not reset these controls in `OpenMenu()`; the host reuses the same screen instance.

- [ ] **Step 5: Filter and sort before the existing grow/reuse/shrink slot binding**

Use:

```csharp
var entries = _gameManager.Player.Inventory.GetAllEntries()
    .Where(entry => MatchesFilter(entry.Item.Category))
    .ToList();
entries.Sort(CompareVisibleEntries);
```

with:

```csharp
private bool MatchesFilter(ItemCategory category) => _inventoryFilter switch
{
    InventoryFilter.All => true,
    InventoryFilter.Equipment => category == ItemCategory.Equipment,
    InventoryFilter.Consumable => category == ItemCategory.Consumable,
    InventoryFilter.General => category == ItemCategory.General,
    InventoryFilter.Quest => category == ItemCategory.Quest,
    _ => false
};

private int CompareVisibleEntries(InventoryEntry a, InventoryEntry b)
{
    var primary = _inventorySort == InventorySort.Name
        ? string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal)
        : a.Item.Category.CompareTo(b.Item.Category);
    if (primary != 0)
        return primary;

    var secondary = _inventorySort == InventorySort.Name
        ? a.Item.Category.CompareTo(b.Item.Category)
        : string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal);
    if (secondary != 0)
        return secondary;

    return string.Compare(a.Item.Id, b.Item.Id, StringComparison.Ordinal);
}
```

- [ ] **Step 6: Refresh presentation only when filter/sort changes**

Each `ItemSelected` handler validates the index, updates private browse state, then:

1. `RefreshInventoryCatalogue()`;
2. `ReconcileSelectionAfterCatalogueRefresh()`;
3. `RefreshSelectionDetails()`;
4. `RefreshSelectionVisuals()`.

Do not call domain mutation or a full `RefreshUI()` for a presentation-only reorder. Leave focus on the initiating OptionButton.

- [ ] **Step 7: Add scene bounds/focus assertions for the toolbar**

At 1280×720 and 640×360, select Items and assert both OptionButtons are visible, focusable, and their rects remain inside the viewport/screen surface.

- [ ] **Step 8: Run focused suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"

git add scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add inventory filters and deterministic sorting"
```

---

### Task 3: Add equipment comparison using the real equip target

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Produces:** target/replacement copy plus ATK/DEF/SPD/HP deltas for selected inventory equipment only.

- [ ] **Step 1: Add failing main-slot comparison tests**

Use explicit current/candidate fixtures so all delta directions are deterministic:

```csharp
[TestCase]
public void EquipmentComparison_ShowsReplacementAndAllFourDeltas()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var current = new EquipmentItem
    {
        Id = "compare-current",
        DisplayName = "Current Blade",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 2,
        DefenseBonus = 3,
        SpeedBonus = 1,
        HealthBonus = 5
    };
    var candidate = new EquipmentItem
    {
        Id = "compare-candidate",
        DisplayName = "Candidate Blade",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 5,
        DefenseBonus = 1,
        SpeedBonus = 1,
        HealthBonus = 10
    };
    AssertThat(player.TryEquip(current, out _)).IsTrue();
    AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(candidate.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);

    var text = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
    AssertThat(text).Contains("Will replace Current Blade in Weapon");
    AssertThat(text).Contains("ATK +3");
    AssertThat(text).Contains("DEF -2");
    AssertThat(text).Contains("SPD 0");
    AssertThat(text).Contains("HP +5");
}
```

Add an empty main-slot case expecting `Will fill <slot>` and candidate-vs-zero deltas.

- [ ] **Step 2: Add failing accessory preview/action consistency tests**

Cover exactly:

1. slot 0 occupied, slot 1 empty → preview says `Will fill Accessory 2`; after Details Equip, candidate is in index 1 and selected there;
2. all four occupied → preview says it replaces slot-0 item in `Accessory 1`; after Equip, candidate is in index 0 and selected there.

This proves preview and action share the production target rule.

- [ ] **Step 3: Render comparison without a new DTO/component**

```csharp
private void PresentEquipmentComparison(EquipmentItem candidate)
{
    var accessoryIndex = candidate.SlotType == EquipmentSlotType.Accessory
        ? ResolveAccessoryEquipIndex()
        : 0;
    var current = candidate.SlotType == EquipmentSlotType.Accessory
        ? _gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, accessoryIndex)
        : _gameManager.Player.Equipment.GetEquipped(candidate.SlotType);

    var target = candidate.SlotType == EquipmentSlotType.Accessory
        ? $"Accessory {accessoryIndex + 1}"
        : SlotDisplayName(candidate.SlotType);
    var outcome = current == null
        ? $"Will fill {target}"
        : $"Will replace {current.DisplayName} in {target}";

    _detailsComparison.Text = string.Join("\n",
        outcome,
        FormatDelta("ATK", candidate.AttackBonus - (current?.AttackBonus ?? 0)),
        FormatDelta("DEF", candidate.DefenseBonus - (current?.DefenseBonus ?? 0)),
        FormatDelta("SPD", candidate.SpeedBonus - (current?.SpeedBonus ?? 0)),
        FormatDelta("HP", candidate.HealthBonus - (current?.HealthBonus ?? 0)));
    _detailsComparison.Visible = true;
}

private static string FormatDelta(string label, int delta) =>
    delta > 0 ? $"{label} +{delta}" : $"{label} {delta}";
```

Call it only for an inventory `EquipmentItem`. Clear/hide comparison for equipped items, consumables, unsupported items, and no selection.

- [ ] **Step 4: Keep `ResolveAccessoryEquipIndex()` as the single target rule**

Both `PresentEquipmentComparison()` and `EquipFromInventory()` call it. Do not cache a target in selection state or introduce preflight/reservation behavior.

- [ ] **Step 5: Run controller suite and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"

git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): compare selected equipment against its target slot"
```

---

### Task 4: Finish compact navigation, input modalities, and invalidation behavior

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Produces:** four-page compact navigation and explicit keyboard/gamepad/mouse evidence without a generic focus graph.

- [ ] **Step 1: Update the paused shoulder-cycle test before changing production modulo**

Pin:

```text
Right: Equipment → Items → Skills → Details → Equipment
Left from Equipment: Details
```

Then replace the current hardcoded `3` with:

```csharp
var pageCount = Enum.GetValues<InventoryPage>().Length;
var page = ((int)_activeCompactPage + direction + pageCount) % pageCount;
SetCompactPage((InventoryPage)page);
```

- [ ] **Step 2: Extend visibility/focus switches only for Details**

Update:

- `ApplyPageVisibility()` — CharacterColumn only for Equipment/Skills; Items only for Items; Details only for Details;
- `ResolveFocusPage()` — recognize `%DetailsTab` and `%DetailsActionButton`;
- `RestoreFocusForPage(Details)` / `RestoreCompactPageFocus()` — prefer visible enabled Details action, otherwise Details tab in compact, otherwise Close in standard.

Keep existing feature-local switch statements. No navigation registry.

- [ ] **Step 3: Make compact selection enter Details deterministically**

After a slot selection refreshes Details:

```csharp
if (_isCompact)
{
    SetCompactPage(InventoryPage.Details);
    RestoreCompactPageFocus();
}
```

Add scene tests:

- usable item → Details visible, Details tab pressed, action button focused;
- battle-only/unsupported item → Details visible, action hidden, Details tab focused.

- [ ] **Step 4: Protect focus when an action disappears after mutation/invalidation**

After selection reconciliation, if the current focus owner is `%DetailsActionButton` but the button became hidden/disabled:

- compact Details → focus `%DetailsTab`;
- standard → focus `%CloseButton`.

Do not redirect focus when the action remains usable.

- [ ] **Step 5: Pin final-consumable and external-invalidation selection behavior**

Controller tests must cover:

- quantity 2 → Use once → same item selected, Details shows quantity 1;
- quantity 1 + next visible entry → Use → next visible item selected;
- quantity 1 + no other visible entry → Use → selection clears;
- selected item removed externally then `OpenMenu()` refreshes → stale selection clears;
- sort change preserves same selected ID;
- filter-out clears selected inventory item.

- [ ] **Step 6: Add actual keyboard/D-pad navigation outcomes for new controls**

Use `SubViewport.PushInput`, not `FocusNeighbor*` property assertions. At 640×360 prove:

- Items tab can reach `%InventoryFilter`/Items controls with `ui_down`;
- browse controls can reach current inventory content;
- Details action can navigate toward Close;
- hidden pages never receive focus.

If one named boundary fails under Godot spatial navigation, add only the direct `FocusNeighbor*` pair for that boundary, matching the HPA-357 precedent. Do not compute a graph.

- [ ] **Step 7: Add real mouse selection/action evidence**

At 1280×720 push primary mouse press/release through the SubViewport at the center of a known inventory slot. Assert Details changes but domain state does not. Then click `%DetailsActionButton` through the same input path and assert the mutation happens once.

Use:

```csharp
var center = slot.GetGlobalRect().GetCenter();
_viewport.PushInput(new InputEventMouseMotion { Position = center });
_viewport.PushInput(new InputEventMouseButton
{
    Position = center,
    ButtonIndex = MouseButton.Left,
    Pressed = true
});
_viewport.PushInput(new InputEventMouseButton
{
    Position = center,
    ButtonIndex = MouseButton.Left,
    Pressed = false
});
```

- [ ] **Step 8: Visit Items and Details at every verification viewport**

Extend scene coverage over `SiriusUiMetrics.VerificationViewports` and assert:

- SafeFrame remains enclosed;
- filter/sort controls are reachable on Items;
- Details content/action-or-reason and global Close stay in the bounded/scrollable screen;
- visible page has non-zero geometry.

The existing PageScroll may scroll content; do not require all Details text to fit without scrolling.

- [ ] **Step 9: Run focused suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"

git add scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs tests/ui/InventoryMenuSceneTest.cs
git commit -m "test(ui): harden enhanced inventory navigation and selection"
```

---

### Task 5: Final scope audit and full verification

**Files:**
- Audit: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Audit: `tests/game/GameplayPauseHostTest.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify: `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md` status after verification

- [ ] **Step 1: Run unchanged adjacent smoke/host suites first**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: PASS. HPA-375 does not change heading/icon art, host policy, pause ownership, HUD policy, or close lifecycle.

- [ ] **Step 2: Audit HPA-376 for stale immediate-action wording**

```bash
git grep -n -E "Inventory|inventory" docs/ui/hpa-376/ui-lifecycle-contract.md
```

If it only describes host/open/close/pause/focus behavior, leave it untouched. If one sentence explicitly claims slot press immediately equips/unequips/uses, change only that sentence to `select → Details action → existing mutation`.

- [ ] **Step 3: Run full tests and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
```

Expected: 0 test failures and 0 build errors.

- [ ] **Step 4: Run stale-handler and scope checks**

```bash
git grep -n -E "OnInventorySlotActivated|OnEquipmentSlotActivated|OnAccessorySlotActivated" -- scripts tests || true
git grep -n -E "InventoryBrowser(Model|ViewModel|Service|Repository)|InventoryDetailsService" -- scripts tests || true
git diff --check
git diff --name-only main...HEAD
```

Expected runtime/test blast radius:

```text
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
tests/ui/InventoryMenuControllerTest.cs
tests/ui/InventoryMenuSceneTest.cs
```

plus the two HPA-375 planning docs and, only if proven stale, the narrow HPA-376 lifecycle document. No domain/save/settings/host/theme/shared-slot production change.

- [ ] **Step 5: Mark the design implemented only after verification passes**

Change:

```text
**Status:** Implemented
```

Do not rewrite the spec unless an actual design decision changed.

- [ ] **Step 6: Commit final docs if needed**

```bash
git add docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md
git commit -m "docs: record HPA-375 implementation verification"
```

Include `docs/ui/hpa-376/ui-lifecycle-contract.md` in that same commit only if Step 2 proved it stale.

- [ ] **Step 7: Final single-PR checklist**

Confirm before marking this existing draft PR ready:

- slot press never mutates item/equipment state;
- Details action is the Inventory Equip/Unequip/Use entry point;
- active-skill selector behavior is unchanged;
- selection is semantic and retains no `InventoryEntry` across refresh;
- filter/sort are deterministic and presentation-only;
- accessory preview and Equip use the same target rule;
- existing rollback tests remain green;
- compact has four usable pages at 640×360;
- all verification viewports remain bounded;
- no new domain/save/settings/host/Theme/generic inventory abstraction exists;
- full tests/build/diff checks are green;
- implementation remained on this single HPA-375 PR.
