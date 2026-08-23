# HPA-375 Inventory Browsing Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit Inventory selection, item details, equipment comparison, category filters, deterministic sorting, and contextual actions without changing inventory/equipment domain rules.

**Architecture:** Extend the existing `InventoryMenuController` and `InventoryMenu.tscn` in place. Selection/filter/sort remain private screen-instance presentation state; live item data is re-resolved from the current Inventory/equipment rather than retained in a new browser model. Existing equip/unequip/use/skill domain paths remain authoritative, and the current semantic focus machinery is extended only where the fourth compact Details page requires it.

**Tech Stack:** Godot 4.6.2, C# / .NET 8.0, GdUnit4, existing Sirius Theme/UI art/components.

**Spec:** `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md`

## Global Constraints

- Keep this work in the existing HPA-375 draft PR/branch; do not open a second implementation PR for the ticket.
- Keep one `InventoryMenuController`; do not add an inventory browser/view-model/service/repository layer.
- Reuse `SiriusItemSlotController`, `UiArtCatalog`, `UiIconPresenter`, `UIScreenHost`, Theme, and existing domain operations.
- Selection, filter, and sort state are presentation-only and are not persisted to settings/save data.
- Slot press selects; only `%DetailsActionButton` may invoke Equip, Unequip, or Use.
- `ActiveSkillSelector` keeps its current assignment behavior; skills are not forced into the item selection model.
- Comparison and Equip must use the same `ResolveAccessoryEquipIndex()` rule.
- Do not add Drop, Sell, Favourite, Lock, bulk actions, search, rarity/value sort, ascending/descending options, equipment requirements, Theme tokens, host APIs/kinds, or compatibility layers.
- Preserve current equip swap, unequip rollback, consumable rollback, battle-only restriction, active-skill, host, pause/input, HUD, and save semantics.
- Use current verification viewports and existing runtime-backed Inventory suites; do not add an E2E or screenshot framework.

---

## File Map

### Production files modified

- `scenes/ui/InventoryMenu.tscn` — author Details, filter/sort controls, and fourth compact tab.
- `scripts/ui/InventoryMenuController.cs` — own semantic selection, catalogue filtering/sorting, details/comparison rendering, contextual action handoff, and reconciliation.

### Test files modified

- `tests/ui/InventoryMenuControllerTest.cs` — controller behavior, current domain-path preservation, filters/sorts, details, comparison, selection reconciliation.
- `tests/ui/InventoryMenuSceneTest.cs` — standard/compact geometry, four-page navigation, input/focus behavior.

### Audit-only files

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Only edit an audit-only file if the implementation makes one of its current assertions or statements stale.

---

### Task 1: Make selection explicit and move mutation behind Details action

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: existing `InventoryEntry`, `Character.TryEquip`, `Character.Unequip`, `ConsumableItem`, `SiriusItemSlotController`, `UiIconPresenter`, current `InventoryFocusKey`.
- Produces: private `InventorySelectionKey`, `%DetailsPage`, `%DetailsTab`, `%DetailsIcon`, `%DetailsName`, `%DetailsMeta`, `%DetailsBody`, `%DetailsComparison`, `%DetailsActionReason`, `%DetailsActionButton`, selection-to-action handoff used by later tasks.

- [ ] **Step 1: Replace direct-activation expectations with failing selection tests**

Add controller tests that prove pressing a slot changes presentation but not domain state. Keep the fixture names concrete so later assertions can reuse them:

```csharp
[TestCase]
public void PressingInventoryEquipment_SelectsWithoutEquipping()
{
    var player = _gameManager.Player;
    var candidate = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
    var previouslyEquipped = player.Equipment.GetEquipped(EquipmentSlotType.Weapon);
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(candidate.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);

    AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon))
        .IsEqual(previouslyEquipped);
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

[TestCase]
public void UnsupportedEntry_IsSelectableAndExplainsMissingAction()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var item = new GeneralItem
    {
        Id = "details_general_item",
        DisplayName = "Old Map",
        Description = "A marked dungeon map.",
        Rarity = ItemRarity.Uncommon
    };
    AssertThat(player.TryAddItem(item, 2, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(item.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);

    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text).IsEqual("Old Map");
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Visible).IsFalse();
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsActionReason").Text)
        .Contains("No inventory action");
    AssertThat(player.Inventory.GetQuantity(item.Id)).IsEqual(2);
}
```

Also change the old equip/unequip/use tests so they no longer expect the slot's first press to mutate. Do not delete their rollback assertions; they move to the explicit action step below.

- [ ] **Step 2: Run the focused controller suite and verify the new tests fail for the intended reason**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: the new tests fail because `%Details*` nodes do not exist and current slot activation still performs mutation.

- [ ] **Step 3: Author the Details page and fourth compact tab**

In `InventoryMenu.tscn`:

- add `%DetailsTab` to the existing compact `ButtonGroup` after Skills;
- add `%DetailsPage` under `%ResponsiveContent`, beside `%CharacterColumn` and `%ItemsPage`;
- put the Details content inside the existing outer `%PageScroll` rather than adding another modal/screen;
- author the exact unique node names from the Interfaces block;
- use existing Theme variations (`SiriusContentPanel`, `SiriusTitleCompact`, `SiriusMetadata`, `SiriusBodyCompact`, `SiriusPrimaryButton`);
- set `%DetailsActionButton` to a minimum height of 44 so it remains an accessible target;
- initialize `%DetailsComparison` and `%DetailsActionReason` hidden/empty and `%DetailsActionButton` hidden;
- keep `%FocusSummary` and global `%CloseButton` unchanged.

Use neutral initial details copy:

```text
Select an item or equipped slot to view details.
```

- [ ] **Step 4: Add semantic selection state and Details bindings**

Add controller fields for the new nodes plus:

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

Bind the Details nodes in `BindNodes()`. Extend `InventoryPage` to include `Details` and bind `%DetailsTab` / `%DetailsActionButton` in `BindSignals()`.

Change Inventory-owned slot wiring from the custom mutation signal to normal selection presses:

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

For accessories capture the index and call `SelectEquipmentSlot(EquipmentSlotType.Accessory, capturedIndex)`. For dynamic inventory slots use normal `Pressed` to call `SelectInventorySlot(slot)` and keep the existing focus/hover handlers.

Do not change `SiriusItemSlotController` itself.

- [ ] **Step 5: Render current selection from live data and normalize pressed visuals**

Add private helpers with these responsibilities:

```csharp
private void SelectInventorySlot(SiriusItemSlotController slot);
private void SelectEquipmentSlot(EquipmentSlotType slotType, int? accessoryIndex);
private void RefreshSelectionDetails();
private void RefreshSelectionVisuals();
private void ClearSelection();
```

`RefreshInventoryCatalogue()` must rebuild `_visibleInventoryEntryByItemId` together with the existing slot maps.

`RefreshSelectionDetails()` re-resolves:

- inventory selection through `_visibleInventoryEntryByItemId`;
- main equipment through `Player.Equipment.GetEquipped(slot)`;
- accessory equipment through `Player.Equipment.GetEquipped(Accessory, index)`.

If resolution fails, clear selection rather than retaining stale `InventoryEntry` or item references.

Render supported base details now:

- name;
- `Category: ... · Rarity: ...`;
- quantity for inventory selection;
- description;
- consumable `EffectDescription` and battle restriction;
- equipment slot plus existing bonus values;
- equipped slot identity.

Use item art when available; otherwise use existing `UiArtCatalog.ForEquipmentSlot(...)` or `UiArtCatalog.ForItemCategory(...)` glyphs through `UiIconPresenter`.

`RefreshSelectionVisuals()` sets `ButtonPressed` only for the slot matching `_selection`; all other slot buttons are false. Programmatic normalization is the source of truth, not the Button's automatic toggle state.

Call selection refresh after the normal equipment/catalogue refresh in `RefreshUI()`.

- [ ] **Step 6: Move Equip/Unequip/Use behind one contextual action button**

Add:

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

`RefreshSelectionDetails()` configures the action exactly:

- inventory equipment → visible `Equip`;
- equipped item → visible `Unequip`;
- non-battle consumable → visible `Use`;
- battle-only consumable → hidden action + `Can only be used in battle.`;
- General/Quest/unsupported → hidden action + `No inventory action is available for this item.`;
- no selection → hidden action and neutral copy.

Remove the old `OnInventorySlotActivated`, `OnEquipmentSlotActivated`, and `OnAccessorySlotActivated` mutation entry points once no signal references them.

- [ ] **Step 7: Reconcile selection inside existing mutation methods**

Update the existing private mutation paths without changing domain ordering:

- `EquipFromInventory`: on successful equip set `_selection` to `ForEquipment(slot)` or `ForAccessory(resolvedIndex)` immediately before `RefreshUI()`; on failed remove/equip rollback leave inventory selection unchanged.
- `HandleUnequip`: after a successful inventory return set `_selection = ForInventoryItem(removed.Id)`; on failed return + successful equipment rollback leave equipped selection unchanged; then `RefreshUI()` as today.
- `UseConsumableOutOfBattle`: before removing, record the selected item's current visible catalogue index in `_pendingSelectionFallbackIndex`; after a successful use keep the same item-ID selection and let refresh reconciliation choose a fallback only if quantity reached zero.

Do not set `_pendingFocusRestore` merely because a Details action ran. Mutation now starts from the stable Details action button rather than from a catalogue slot, so keep focus in Details when that button remains usable.

After `RefreshInventoryCatalogue()`, if selected inventory item no longer exists and `_pendingSelectionFallbackIndex` is set, resolve the new selection using:

```csharp
var fallbackIndex = Math.Min(
    _pendingSelectionFallbackIndex.Value,
    _inventorySlots.Count - 1);
```

Select the entry currently bound at that index when one exists; otherwise clear. Then clear `_pendingSelectionFallbackIndex`.

- [ ] **Step 8: Update existing domain-parity tests to invoke the explicit Details action**

Introduce a small test helper:

```csharp
private Button DetailsActionButton() =>
    _inventoryMenu.GetNode<Button>("%DetailsActionButton");
```

For rollback/parity tests, press the slot once to select, then press the Details action. For example:

```csharp
GetSlot("%WeaponSlot").EmitSignal(Button.SignalName.Pressed);
DetailsActionButton().EmitSignal(Button.SignalName.Pressed);
```

Preserve assertions for:

- full-inventory unequip rollback;
- accessory unequip rollback;
- failed consumable application rollback;
- first-empty accessory equip and slot-0 fallback when full;
- explicit active-skill None behavior.

Replace the old "activation restores focus to resulting equipment slot" expectation with a Details-oriented focus assertion: after an action whose next selection still has a supported action, `%DetailsActionButton` remains visible and usable; compact focus behavior is pinned in Task 4.

- [ ] **Step 9: Update the scene suite for the fourth page and run both Inventory suites**

Change `VisiblePageCount()` to include `%DetailsPage`. Update standard-layout expectations to assert Details is visible. Update the compact count to exactly one of four pages.

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 10: Commit the explicit-selection vertical slice**

```bash
git add scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add explicit inventory selection and details actions"
```

---

### Task 2: Add category filtering and deterministic Name/Category sorting

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: Task 1 semantic item-ID selection and visible-entry map.
- Produces: private `InventoryFilter`, private `InventorySort`, `%InventoryFilter`, `%InventorySort`, deterministic visible-catalogue ordering.

- [ ] **Step 1: Add failing controller tests for every filter and both sort modes**

Use a mixed inventory with stable IDs/names and assert visible slot tooltips. Add a helper:

```csharp
private string[] VisibleInventoryNames() =>
    _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren()
        .OfType<SiriusItemSlotController>()
        .Select(slot => slot.TooltipText.Split('\n')[0])
        .ToArray();
```

Pin Name sorting with a tie:

```csharp
[TestCase]
public void NameSort_UsesCategoryThenIdAsDeterministicTieBreaks()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    AssertThat(player.TryAddItem(new GeneralItem { Id = "z-general", DisplayName = "Same" }, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(new EquipmentItem { Id = "a-equip", DisplayName = "Same" }, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(new EquipmentItem { Id = "b-equip", DisplayName = "Same" }, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    _inventoryMenu.GetNode<OptionButton>("%InventorySort").Select(0);
    _inventoryMenu.GetNode<OptionButton>("%InventorySort")
        .EmitSignal(OptionButton.SignalName.ItemSelected, 0L);

    AssertThat(VisibleInventoryNames()).IsEqual(new[] { "Same", "Same", "Same" });
    var ids = _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>()
        .Select(slot => slot.TooltipText)
        .ToArray();
    AssertThat(ids.Length).IsEqual(3);
}
```

Because identical names do not expose IDs in current tooltip copy, add a test-only helper that selects each slot and reads a `%DetailsMeta`/stable details field only if the implementation exposes the ID; otherwise use distinct tie names/categories where ordering is externally visible. Do not add item IDs to player-facing copy solely for testing.

For Category sort, use distinct names and assert enum order is `General`, `Equipment`, `Consumable`, `Quest`, then ordinal name/id within each category.

For filters, assert All plus each current `ItemCategory` exposes only matching entries.

- [ ] **Step 2: Add failing selection reconciliation tests for sort/filter changes**

Add:

```csharp
[TestCase]
public void SortingPreservesSelectedItemById()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var selected = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(selected, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();
    FindInventorySlotByTooltip(selected.DisplayName).EmitSignal(Button.SignalName.Pressed);

    var sort = _inventoryMenu.GetNode<OptionButton>("%InventorySort");
    sort.Select(1);
    sort.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);

    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(selected.DisplayName);
    AssertThat(FindInventorySlotByTooltip(selected.DisplayName).ButtonPressed).IsTrue();
}

[TestCase]
public void FilteringOutSelectedInventoryItem_ClearsSelection()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var selected = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(selected, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();
    FindInventorySlotByTooltip(selected.DisplayName).EmitSignal(Button.SignalName.Pressed);

    var filter = _inventoryMenu.GetNode<OptionButton>("%InventoryFilter");
    filter.Select(2); // Consumable
    filter.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);

    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text).IsEmpty();
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Visible).IsFalse();
}
```

Keep the exact OptionButton index contract aligned with the enum order below.

- [ ] **Step 3: Run the controller suite and verify the new tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: FAIL because filter/sort controls and behavior are not authored yet.

- [ ] **Step 4: Author the Items toolbar**

In `%ItemsContent`, between the title row and `%InventoryScroll`, add an `HBoxContainer` containing:

- `%InventoryFilter : OptionButton`
- `%InventorySort : OptionButton`

Use existing controls/Theme only. Give both controls a 44 px minimum height. Let the row wrap only through available container sizing; do not add another reusable toolbar component.

- [ ] **Step 5: Add private browse enums and initialize the controls**

Add:

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

Add one initialization method called from `_Ready()` after `BindSignals()`:

```csharp
private void InitializeInventoryBrowseControls()
{
    _inventoryFilterControl.Clear();
    foreach (var text in new[] { "All", "Equipment", "Consumable", "General", "Quest" })
        _inventoryFilterControl.AddItem(text);
    _inventoryFilterControl.Select((int)_inventoryFilter);

    _inventorySortControl.Clear();
    _inventorySortControl.AddItem("Name");
    _inventorySortControl.AddItem("Category");
    _inventorySortControl.Select((int)_inventorySort);
}
```

Bind `ItemSelected` handlers once for the controller lifetime, consistent with the existing reattach behavior.

- [ ] **Step 6: Filter and sort before reusing/growing/shrinking slots**

Refactor only the top of `RefreshInventoryCatalogue()`:

```csharp
var entries = _gameManager.Player.Inventory.GetAllEntries()
    .Where(entry => MatchesFilter(entry.Item.Category))
    .ToList();
entries.Sort(CompareVisibleEntries);
```

Implement:

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

Do not change `Inventory` itself.

- [ ] **Step 7: Reconcile selection and focus after browse-control changes**

`OnInventoryFilterSelected(long index)` and `OnInventorySortSelected(long index)` must:

1. validate the enum index;
2. update controller-local state;
3. refresh the visible catalogue;
4. keep selected inventory item only if `_visibleInventoryEntryByItemId` still contains it;
5. refresh Details and pressed visuals;
6. leave focus on the OptionButton that initiated the change.

Equipped-item selection remains valid across inventory filtering.

Do not call a domain mutation or `RefreshUI()` just to change presentation ordering.

- [ ] **Step 8: Add scene assertions that toolbar controls remain bounded at standard and compact viewports**

At 1280×720 and 640×360, select Items and assert `%InventoryFilter` and `%InventorySort` global rects are inside the viewport and remain visible/focusable.

- [ ] **Step 9: Run focused suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 10: Commit filtering/sorting**

```bash
git add scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add inventory filters and deterministic sorting"
```

---

### Task 3: Add equipment replacement comparison using the real target rule

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: selected inventory `EquipmentItem`, existing `ResolveAccessoryEquipIndex()`, live `EquipmentSet`.
- Produces: `%DetailsComparison` copy that matches the exact target the Equip action will use.

- [ ] **Step 1: Add failing main-slot comparison tests covering gain, loss, and unchanged values**

Build explicit current/candidate items rather than depending on catalog balance:

```csharp
[TestCase]
public void EquipmentComparison_ShowsTargetReplacementAndAllFourDeltas()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var current = new EquipmentItem
    {
        Id = "compare_current_weapon",
        DisplayName = "Current Blade",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 2,
        DefenseBonus = 3,
        SpeedBonus = 1,
        HealthBonus = 5
    };
    var candidate = new EquipmentItem
    {
        Id = "compare_candidate_weapon",
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

    var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
    AssertThat(comparison).Contains("Will replace Current Blade in Weapon");
    AssertThat(comparison).Contains("ATK +3");
    AssertThat(comparison).Contains("DEF -2");
    AssertThat(comparison).Contains("SPD 0");
    AssertThat(comparison).Contains("HP +5");
}
```

Add an empty-slot test that expects `Will fill <slot>` and candidate bonuses relative to zero.

- [ ] **Step 2: Add failing accessory comparison tests for both targeting modes**

Test two concrete cases:

1. accessory slot 0 occupied and slot 1 empty → candidate comparison says it will fill `Accessory 2` and deltas compare against zero;
2. all four occupied → candidate comparison says it will replace slot 0's item in `Accessory 1` and deltas compare against that item.

After rendering each preview, press `%DetailsActionButton` and assert the item actually lands in the same slot the comparison named. This is the load-bearing preview/action consistency test.

- [ ] **Step 3: Run controller tests and verify comparison tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: FAIL because `%DetailsComparison` does not yet render equipment deltas/target copy.

- [ ] **Step 4: Add one comparison renderer for selected inventory equipment**

Use the existing target resolution immediately when Details is refreshed:

```csharp
private void PresentEquipmentComparison(EquipmentItem candidate)
{
    var accessoryIndex = candidate.SlotType == EquipmentSlotType.Accessory
        ? ResolveAccessoryEquipIndex()
        : 0;
    var current = candidate.SlotType == EquipmentSlotType.Accessory
        ? _gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, accessoryIndex)
        : _gameManager.Player.Equipment.GetEquipped(candidate.SlotType);

    var targetName = candidate.SlotType == EquipmentSlotType.Accessory
        ? $"Accessory {accessoryIndex + 1}"
        : SlotDisplayName(candidate.SlotType);

    var outcome = current == null
        ? $"Will fill {targetName}"
        : $"Will replace {current.DisplayName} in {targetName}";

    _detailsComparison.Text = string.Join("\n",
        outcome,
        FormatDelta("ATK", candidate.AttackBonus - (current?.AttackBonus ?? 0)),
        FormatDelta("DEF", candidate.DefenseBonus - (current?.DefenseBonus ?? 0)),
        FormatDelta("SPD", candidate.SpeedBonus - (current?.SpeedBonus ?? 0)),
        FormatDelta("HP", candidate.HealthBonus - (current?.HealthBonus ?? 0)));
    _detailsComparison.Visible = true;
}
```

Use a tiny text helper only:

```csharp
private static string FormatDelta(string label, int delta) =>
    delta > 0 ? $"{label} +{delta}" : $"{label} {delta}";
```

Do not introduce a comparison DTO/component/theme.

Hide/clear `%DetailsComparison` for non-equipment inventory selections, equipped-item selections, and no selection.

- [ ] **Step 5: Verify accessory preview and Equip call share target resolution**

Keep `ResolveAccessoryEquipIndex()` as the single private rule used by both `PresentEquipmentComparison()` and `EquipFromInventory()`.

Do not cache the previewed accessory index across arbitrary refreshes. The action re-resolves immediately before mutation; the single-threaded UI cannot change equipment between the button press and the synchronous operation without another refresh.

- [ ] **Step 6: Run the controller suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: PASS.

- [ ] **Step 7: Commit comparison**

```bash
git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): compare selected equipment against its target slot"
```

---

### Task 4: Finish compact navigation, modality coverage, and mutation-selection reconciliation

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: four `InventoryPage` values, Task 1 Details action, Task 2 browse controls, existing `RestoreFocusForPage` / `RestoreCompactPageFocus` / `ResolveFocusPage`.
- Produces: deterministic compact Details entry/exit and tested keyboard/gamepad/mouse interaction without a generic focus graph.

- [ ] **Step 1: Pin four-page compact cycling before changing the modulo**

Update the existing shoulder-cycle test so repeated Right Shoulder presses prove:

```text
Equipment → Items → Skills → Details → Equipment
```

and Left Shoulder from Equipment proves wraparound to Details.

Run only `InventoryMenuSceneTest` and confirm the new Details step fails while the controller still cycles modulo 3.

- [ ] **Step 2: Extend compact page visibility/focus helpers to Details**

Change page cycling to use the enum count explicitly rather than another magic 4:

```csharp
var pageCount = Enum.GetValues<InventoryPage>().Length;
var page = ((int)_activeCompactPage + direction + pageCount) % pageCount;
SetCompactPage((InventoryPage)page);
```

Update:

- `ApplyPageVisibility()` so CharacterColumn is visible only for Equipment/Skills, Items only for Items, Details only for Details;
- `ResolveFocusPage()` for `%DetailsTab` and `%DetailsActionButton`;
- `RestoreFocusForPage(Details)` and `RestoreCompactPageFocus()` to prefer the visible enabled Details action, otherwise Details tab in compact, otherwise Close in standard;
- `ResolveInitialFocusTarget()` only as needed to keep its existing Equipment-first behavior.

Do not replace the feature-local switch statements with a generic navigation registry.

- [ ] **Step 3: Add a compact selection-to-Details focus test**

Add a scene test using a known usable inventory item:

```csharp
[TestCase]
public async Task CompactSelectingItem_OpensDetailsAndFocusesSupportedAction()
{
    _gameManager.Player.Inventory.Clear();
    var potion = ConsumableCatalog.CreateHealthPotion();
    AssertThat(_gameManager.Player.TryAddItem(potion, 1, out _)).IsTrue();
    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);

    _menu.GetNode<Button>("%ItemsTab").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);
    var slot = _menu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().Single();
    slot.EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Button>("%DetailsTab").ButtonPressed).IsTrue();
    AssertThat(_menu.GetNode<Control>("%DetailsPage").Visible).IsTrue();
    AssertThat(_viewport.GuiGetFocusOwner())
        .IsEqual(_menu.GetNode<Button>("%DetailsActionButton"));
}
```

For a selected battle-only/unsupported item, assert Details opens and `%DetailsTab` receives focus because no action is available.

- [ ] **Step 4: Add keyboard/D-pad browse-control navigation outcomes**

Use `_viewport.PushInput` rather than checking `FocusNeighbor*` properties. Pin these outcomes at 640×360:

- Items tab → `ui_down` reaches `%InventoryFilter` or another authored Items control, never a hidden page;
- filter/sort controls can move to the current visible Inventory grid through normal spatial navigation;
- Details action → `ui_down` can reach Close;
- `ui_up` from the first Details action can return toward the Details tab or Details content boundary without leaving the active page.

If Godot's spatial result fails one named boundary, add only the direct `FocusNeighbor*` pair for that boundary in the controller, following the HPA-357 precedent. Do not compute a full neighbor graph.

- [ ] **Step 5: Add mouse selection coverage against the real scene**

At 1280×720, position a mouse event at the center of a known inventory slot and send a primary press/release through the `SubViewport`:

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

After frames settle, assert Details shows the item and the domain is unchanged. Then click `%DetailsActionButton` the same way and assert the existing mutation occurs once.

- [ ] **Step 6: Pin final-consumable fallback and external invalidation**

Add/adjust controller tests for the exact selection rules:

- quantity 2 → Use once → same item remains selected with `Quantity: 1`;
- quantity 1 with another visible item at the next index → Use → next item selected;
- quantity 1 and no other visible item → Use → neutral empty Details;
- select item, remove it directly through `Character.TryRemoveItem`, call `OpenMenu()`/refresh → stale selection clears;
- select item, switch sort → same item stays selected;
- select item, filter it out → selection clears.

Keep the current focus-summary refresh test; FocusSummary and selection are separate states.

- [ ] **Step 7: Verify all Sirius verification viewports with Details and toolbar visible**

Extend `FitsEveryVerificationViewport()` or add a focused companion test that visits Items and Details at every `SiriusUiMetrics.VerificationViewports` size and asserts:

- SafeFrame remains enclosed;
- visible page rect is non-zero;
- filter/sort controls are reachable on Items;
- Details action/reason and Close remain inside the scrollable/bounded screen surface;
- no requirement is added for all content to be simultaneously visible without the existing `PageScroll`.

- [ ] **Step 8: Run focused Inventory suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: PASS.

- [ ] **Step 9: Commit navigation/reconciliation coverage**

```bash
git add scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "test(ui): harden enhanced inventory navigation and selection"
```

---

### Task 5: Final scope audit and full verification

**Files:**
- Audit: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Audit: `tests/game/GameplayPauseHostTest.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Modify if required by stale wording/assertion: the specific audit file only
- Modify: `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md` (status only after implementation is verified)

**Interfaces:**
- Consumes: completed Tasks 1–4.
- Produces: one verified HPA-375 branch with no shared architecture/domain drift.

- [ ] **Step 1: Run existing Inventory-adjacent smoke/host suites without changing them first**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: PASS. HPA-375 does not change heading/icon art, host policy, pause ownership, HUD policy, or close lifecycle.

- [ ] **Step 2: Audit the HPA-376 lifecycle contract for stale immediate-action wording**

Run:

```bash
git grep -n -E "Inventory|inventory" docs/ui/hpa-376/ui-lifecycle-contract.md
```

If the current text only describes host/open/close/pause/focus behavior, leave it untouched. If it explicitly says an item/equipment slot press immediately equips, unequips, or uses, change only that sentence to describe `select → Details action → existing mutation`.

- [ ] **Step 3: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: all tests pass with 0 failures.

- [ ] **Step 4: Build the solution**

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: 0 build errors. Existing environment/analyzer warnings are not HPA-375 failures unless this branch introduces a new warning in touched code.

- [ ] **Step 5: Run stale-path and scope greps**

Confirm no direct Inventory slot mutation handlers remain and no forbidden abstraction was introduced:

```bash
git grep -n -E "OnInventorySlotActivated|OnEquipmentSlotActivated|OnAccessorySlotActivated" -- scripts tests || true
git grep -n -E "InventoryBrowser(Model|ViewModel|Service|Repository)|InventoryDetailsService" -- scripts tests || true
git diff --check
git diff --name-only main...HEAD
```

Expected production/test blast radius is:

```text
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
tests/ui/InventoryMenuControllerTest.cs
tests/ui/InventoryMenuSceneTest.cs
```

plus these planning docs and, only if proven stale, the narrow HPA-376 lifecycle document. No domain/save/settings/host/theme/shared-slot production file should change.

- [ ] **Step 6: Mark the design implemented after verification passes**

Change only the design header:

```text
**Status:** Implemented
```

Do not rewrite the design to mirror every implementation detail discovered during execution unless an actual design decision changed.

- [ ] **Step 7: Commit final audit/docs if there are changes**

If only the design status changed:

```bash
git add docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md
git commit -m "docs: record HPA-375 implementation verification"
```

If HPA-376 required one evidence-backed wording correction, include that file in the same final documentation commit.

- [ ] **Step 8: Final single-PR review checklist**

Confirm before marking the existing draft PR ready:

- slot press never mutates item/equipment state;
- Details action is the only Inventory Equip/Unequip/Use entry point;
- active-skill selector remains unchanged;
- selection is semantic and holds no `InventoryEntry` across refresh;
- filter/sort are deterministic and presentation-only;
- accessory preview and Equip use the same target rule;
- existing rollback behavior still passes;
- compact has four usable pages at 640×360;
- standard and every verification viewport remain bounded;
- no new domain, save, settings, host, Theme, or generic inventory abstraction was added;
- full tests/build/diff checks are green;
- implementation stayed on the same HPA-375 PR.
