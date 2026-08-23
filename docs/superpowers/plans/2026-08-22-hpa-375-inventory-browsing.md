# HPA-375 Inventory Browsing Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit Inventory selection, item details, equipment comparison, category filters, deterministic sorting, and contextual actions without changing inventory/equipment domain rules.

**Architecture:** Extend the existing `InventoryMenuController` and `InventoryMenu.tscn` in place. Reuse the current semantic identity, catalogue maps, domain mutation methods, and `PendingFocusRestore`. Selection remains a required product behavior separate from activation; successful mutations reuse the existing focus restore pipeline, and filter/sort/comparison stay private presentation concerns.

**Tech Stack:** Godot 4.6.2, C# / .NET 8.0, GdUnit4, existing Sirius Theme/UI art/components.

**Spec:** `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md`

## Global Constraints

- Keep all implementation on this HPA-375 branch/PR; do not open a second PR.
- Keep one `InventoryMenuController`; no browser/view-model/service/repository layer.
- HPA-375 requires explicit selection independent from immediate equip/use; do not retain current one-press mutation semantics.
- Reuse the current semantic key shape, `_inventoryEntryBySlot`, `_inventorySlotByItemId`, and `PendingFocusRestore.PreviousCatalogueIndex`.
- Do not add a third item-ID → `InventoryEntry` map or `_pendingSelectionFallbackIndex`.
- Do not modify `SiriusItemSlotController`.
- Slot press selects; only `%DetailsActionButton` invokes Equip, Unequip, or Use.
- `ButtonPressed` is derived from `_selection`; normalize it after every select handler and refresh.
- Compact slot selection does not auto-jump to Details; tabs/LB/RB open Details.
- Successful mutation never leaves focus on an action button whose meaning changed.
- A mutation result hidden by the active filter does not reset the filter; clear selection and use normal focus fallback.
- Filter/sort OptionButtons store enum values in item metadata; handlers do not cast selected index directly.
- Sorting and bonus delta math are private static helpers; do not widen production API for tests.
- Comparison and Equip both call `ResolveAccessoryEquipIndex()`.
- Zero comparison deltas render as `unchanged`.
- Standard 1024×768 must not horizontally scroll the six-column item grid; author Character : Items : Details stretch ratios as `1 : 1.5 : 1`.
- No Drop/Sell/Favourite/Lock/bulk actions, search, extra sort modes, equipment requirements, new Theme tokens, new host APIs/kinds, or compatibility layers.
- Preserve current equip/unequip/consumable rollback, battle-only, active-skill, host, pause/input, HUD, and save semantics.
- Use existing runtime-backed Inventory tests; do not add a new E2E/screenshot framework.

---

## File Map

### Production

- Modify: `scenes/ui/InventoryMenu.tscn` — Details page/tab, Items browse toolbar, fourth compact page, standard stretch ratio.
- Modify: `scripts/ui/InventoryMenuController.cs` — shared semantic key, selection/details/actions, filter/sort, comparison, reconciliation/navigation.

### Tests

- Modify: `tests/ui/InventoryMenuControllerTest.cs` — selection, details, visual normalization, tooltip, actions, filters/sorts, comparison, domain parity.
- Modify: `tests/ui/InventoryMenuSceneTest.cs` — four-page geometry, standard width, breakpoint focus, compact navigation, post-mutation focus.

### Audit only

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Edit audit-only files only when a final grep/test proves a current assertion or statement stale.

---

### Task 1A: Prepare shared identity and the Details layout without changing activation

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: `InventoryFocusKey`, `PendingFocusRestore`, current `InventoryPage`, existing compact visibility helpers.
- Produces: `InventorySemanticKey`, `%DetailsTab`, `%DetailsPage` and Details child nodes, four-page visibility scaffolding, 1:1.5:1 standard layout.

- [ ] **Step 1: Add failing scene tests for the fourth page and 1024×768 catalogue width**

Update the test helper first so the new page is counted:

```csharp
private int VisiblePageCount() =>
    new[] { "%EquipmentPage", "%ItemsPage", "%SkillsPage", "%DetailsPage" }
        .Count(path => _menu.GetNode<Control>(path).Visible);
```

Add a standard-layout width regression with enough entries to make all six columns meaningful:

```csharp
[TestCase]
public async Task Standard1024_ItemsGridFitsWithoutHorizontalScroll()
{
    _gameManager.Player.Inventory.Clear();
    for (var i = 0; i < 6; i++)
    {
        AssertThat(_gameManager.Player.TryAddItem(new EquipmentItem
        {
            Id = $"width_item_{i}",
            DisplayName = $"Width Item {i}",
            SlotType = EquipmentSlotType.Weapon
        }, 1, out _)).IsTrue();
    }

    await Resize(new Vector2I(1024, 768));
    _menu.OpenMenu();
    await AwaitFrames(2);

    var scroll = _menu.GetNode<ScrollContainer>("%InventoryScroll");
    var grid = _menu.GetNode<GridContainer>("%InventoryGrid");
    AssertThat(grid.GetCombinedMinimumSize().X).IsLessEqual(scroll.Size.X);
    AssertThat(scroll.GetHScrollBar().Visible).IsFalse();
}
```

Add compact geometry expectations:

```csharp
[TestCase]
public async Task Compact_DetailsPageIsOneOfExactlyFourPages()
{
    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);

    _menu.GetNode<Button>("%DetailsTab").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    AssertThat(VisiblePageCount()).IsEqual(1);
    AssertThat(_menu.GetNode<Control>("%DetailsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%CharacterColumn").Visible).IsFalse();
}
```

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: FAIL because Details nodes/tab do not exist.

- [ ] **Step 2: Rename the existing semantic key without behavior change**

Rename `InventoryFocusKey` to `InventorySemanticKey` everywhere in `InventoryMenuController.cs`:

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

private readonly record struct PendingFocusRestore(
    InventorySemanticKey Preferred,
    int PreviousCatalogueIndex)
{
    public PendingFocusRestore WithPreferred(InventorySemanticKey preferred) =>
        this with { Preferred = preferred };
}
```

Do not add `_selection` yet. Existing activation/focus behavior remains unchanged in this task.

- [ ] **Step 3: Author Details and the fourth compact tab**

In `InventoryMenu.tscn`:

- add `%DetailsTab` to the existing `ButtonGroup` after Skills;
- add `%DetailsPage` as a sibling of `%CharacterColumn` and `%ItemsPage`;
- add `%DetailsIcon`, `%DetailsName`, `%DetailsMeta`, `%DetailsBody`, `%DetailsComparison`, `%DetailsActionReason`, `%DetailsActionButton`;
- keep `%DetailsActionButton` hidden initially with minimum height 44;
- initialize `%DetailsBody` to `Select an item or equipped slot to view details.`;
- use existing Theme variations only;
- set `size_flags_stretch_ratio = 1.5` on `%ItemsPage`; leave Character/Details at 1.0;
- keep the existing outer `%PageScroll` as the sole page scroll owner.

Extend the page enum:

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills,
    Details
}
```

Bind `_detailsTab`, `_detailsPage`, and Details child controls in `BindNodes()`. Bind `%DetailsTab.Pressed` to `SetCompactPage(InventoryPage.Details)`; do not bind the action yet.

- [ ] **Step 4: Make four-page visibility explicit**

Update `SetCompactPage` to set all four tab pressed states. Replace the compact branch of `ApplyPageVisibility()` with:

```csharp
_characterColumn.Visible =
    _activeCompactPage is InventoryPage.Equipment or InventoryPage.Skills;
_equipmentPage.Visible = _activeCompactPage == InventoryPage.Equipment;
_itemsPage.Visible = _activeCompactPage == InventoryPage.Items;
_skillsPage.Visible = _activeCompactPage == InventoryPage.Skills;
_detailsPage.Visible = _activeCompactPage == InventoryPage.Details;
```

Standard branch keeps Character/Equipment/Items/Skills/Details visible.

Update compact cycling to modulo four:

```csharp
var page = (int)_activeCompactPage;
page = (page + direction + 4) % 4;
SetCompactPage((InventoryPage)page);
RestoreCompactPageFocus();
```

For this structural task, Details focus target may fall back to `%DetailsTab`; Task 4 finalizes action-aware Details focus.

- [ ] **Step 5: Run the existing controller suite plus scene suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
git diff --check
```

Expected: PASS. Current slot press still immediately equips/uses/unequips; this task intentionally has not changed product behavior yet.

- [ ] **Step 6: Commit the structural checkpoint**

```bash
git add scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "refactor(ui): prepare inventory details layout"
```

---

### Task 1B: Atomically cut slot press over to selection and contextual actions

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: Task 1A `InventorySemanticKey`, Details nodes, current maps, `PendingFocusRestore`, existing mutation methods.
- Produces: `_selection`, selection-only presses, normalized selected visuals, Details rendering/action, successful-mutation focus restore.

- [ ] **Step 1: Add failing tests for selection-only semantics and visual normalization**

Add:

```csharp
[TestCase]
public void PressingInventoryEquipment_SelectsWithoutEquipping()
{
    var player = _gameManager.Player;
    var candidate = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
    var before = player.Equipment.GetEquipped(EquipmentSlotType.Weapon);
    _inventoryMenu.OpenMenu();

    var slot = FindInventorySlotByTooltip(candidate.DisplayName);
    slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(before);
    AssertThat(player.Inventory.ContainsItem(candidate.Id)).IsTrue();
    AssertThat(slot.ButtonPressed).IsTrue();
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(candidate.DisplayName);
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
        .IsEqual("Equip");
}
```

Add two normalization regressions:

```csharp
[TestCase]
public void PressingSelectedInventorySlotAgain_RemainsVisuallySelected()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();
    var slot = FindInventorySlotByTooltip(sword.DisplayName);

    slot.EmitSignal(Button.SignalName.Pressed);
    slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(slot.ButtonPressed).IsTrue();
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(sword.DisplayName);
}

[TestCase]
public void PressingEmptyEquipmentSlot_DoesNotStealSelectionVisual()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    AssertThat(_gameManager.Player.Unequip(EquipmentSlotType.Shield)).IsNull();
    _inventoryMenu.OpenMenu();
    var selected = FindInventorySlotByTooltip(sword.DisplayName);
    selected.EmitSignal(Button.SignalName.Pressed);

    var emptyShield = GetSlot("%ShieldSlot");
    emptyShield.EmitSignal(Button.SignalName.Pressed);

    AssertThat(selected.ButtonPressed).IsTrue();
    AssertThat(emptyShield.ButtonPressed).IsFalse();
}
```

Also add unsupported-entry selection and equipped-slot selection-without-unequip tests.

Run the controller suite and confirm FAIL because current `Activated` still mutates.

- [ ] **Step 2: Add `_selection` and resolve it through existing maps**

Add:

```csharp
private InventorySemanticKey? _selection;
```

Do not add another item-ID → entry dictionary.

Add:

```csharp
private bool TryResolveSelectedInventoryEntry(
    out SiriusItemSlotController slot,
    out InventoryEntry entry)
{
    slot = null!;
    entry = null!;

    if (_selection?.ItemId is not { } itemId ||
        !_inventorySlotByItemId.TryGetValue(itemId, out slot))
        return false;

    return _inventoryEntryBySlot.TryGetValue(slot, out entry);
}
```

- [ ] **Step 3: Replace Inventory-owned `Activated` subscriptions with selection `Pressed` subscriptions**

Equipment binding becomes:

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

Accessories capture their index and call `SelectEquipmentSlot(EquipmentSlotType.Accessory, index)`. Dynamic inventory slots use:

```csharp
slot.ToggleMode = true;
slot.Pressed += () => SelectInventorySlot(slot);
```

Delete `OnInventorySlotActivated`, `OnEquipmentSlotActivated`, and `OnAccessorySlotActivated` after all references are removed.

- [ ] **Step 4: Make `_selection` the only source of selected visuals**

Implement:

```csharp
private void SelectInventorySlot(SiriusItemSlotController slot)
{
    if (_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        _selection = InventorySemanticKey.ForItem(entry.Item.Id);

    RefreshSelectionDetails();
    RefreshSelectionVisuals();
}
```

`SelectEquipmentSlot` changes `_selection` only when the live equipment slot is populated, then always calls both refresh helpers.

Implement `RefreshSelectionVisuals()` by clearing `ButtonPressed` on every equipment/accessory/inventory slot, then setting it only on the control matching `_selection`.

Call `RefreshSelectionVisuals()` after every normal `RefreshUI()` catalogue/equipment bind as well.

- [ ] **Step 5: Render Details from current data and correct tooltip verbs**

`RefreshSelectionDetails()` resolves:

- inventory item through `TryResolveSelectedInventoryEntry`;
- main/accessory equipment through live `Player.Equipment`.

Render name/category/rarity/quantity/description/effect/slot/bonuses and current icon/fallback. Empty selection restores neutral copy and hides action/comparison.

Replace `Click to equip` / `Click to use` in `BuildInventoryTooltip` with:

```text
Select to view details
```

Keep `Battle use only` as separate informational copy for battle-only consumables.

Add assertions that no Inventory tooltip claims direct mutation.

- [ ] **Step 6: Route supported mutations through `%DetailsActionButton`**

Bind the action once and use:

```csharp
private void OnDetailsActionPressed()
{
    if (_selection is not { } selection)
        return;

    if (selection.ItemId != null && TryResolveSelectedInventoryEntry(out _, out var entry))
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

Details mapping:

- inventory equipment → visible `Equip`;
- equipped item → visible `Unequip`;
- usable consumable → visible `Use`;
- battle-only → hidden action + `Can only be used in battle.`;
- General/Quest/unsupported → hidden action + `No inventory action is available for this item.`;
- no selection → hidden action + neutral Details copy.

- [ ] **Step 7: Reuse `PendingFocusRestore` after successful mutations**

Add:

```csharp
private int ResolveVisibleInventoryIndex(string itemId) =>
    _inventorySlotByItemId.TryGetValue(itemId, out var slot)
        ? _inventorySlots.IndexOf(slot)
        : -1;
```

Keep existing domain ordering. On successful Equip:

```csharp
var resultingKey = item.SlotType == EquipmentSlotType.Accessory
    ? InventorySemanticKey.ForAccessory(accessoryIndex)
    : InventorySemanticKey.ForEquipment(item.SlotType);
_selection = resultingKey;
_pendingFocusRestore = new PendingFocusRestore(resultingKey, previousCatalogueIndex);
RefreshUI();
```

On successful Unequip:

```csharp
var resultingKey = InventorySemanticKey.ForItem(removed.Id);
_selection = resultingKey;
_pendingFocusRestore = new PendingFocusRestore(resultingKey, -1);
RefreshUI();
```

On successful consumable use:

```csharp
var previousIndex = ResolveVisibleInventoryIndex(item.Id);
var resultingKey = InventorySemanticKey.ForItem(item.Id);
_selection = resultingKey;
_pendingFocusRestore = new PendingFocusRestore(resultingKey, previousIndex);
RefreshUI();
```

Failed mutation/rollback leaves selection and focus semantics unchanged.

- [ ] **Step 8: Reconcile disappearing selection before `RestorePendingFocus()`**

After rebuilding catalogue maps:

1. if selected inventory item still resolves, keep it;
2. if it disappeared and pending preferred is the same item with `PreviousCatalogueIndex >= 0`, choose the visible entry at `min(previousIndex, count - 1)` and update both `_selection` and pending `Preferred`;
3. otherwise clear `_selection`.

Do not clear `_pendingFocusRestore` merely because selection clears. A hidden mutation result still needs the existing focus fallback to land on a visible item/page/tab/Close.

Call order inside `RefreshUI()` must be:

```text
refresh equipment/accessories/skills/catalogue
→ reconcile selection
→ refresh Details/selection visuals
→ refresh focus summary
→ restore pending focus
```

- [ ] **Step 9: Migrate existing direct-activation parity tests and add inverted-action focus regression**

Introduce:

```csharp
private Button DetailsActionButton() =>
    _inventoryMenu.GetNode<Button>("%DetailsActionButton");
```

Update existing equip/unequip/consumable rollback tests to press the slot once to select, then press the Details action. Preserve all original domain assertions.

Add:

```csharp
[TestCase]
public async Task EquipAction_MovesFocusToResultingEquipmentSlot()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(sword.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);
    var action = DetailsActionButton();
    action.GrabFocus();
    action.EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var weapon = GetSlot("%WeaponSlot");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weapon);
    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Weapon))
        .IsEqual(sword);
    AssertThat(action.Text).IsEqual("Unequip");
    AssertThat(action.HasFocus()).IsFalse();
}
```

- [ ] **Step 10: Run focused suites and commit the atomic behavior cutover**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
git diff --check
```

Expected: PASS.

```bash
git add scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add explicit inventory selection and details actions"
```

---

### Task 2: Add metadata-backed filters and pure deterministic sorting

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: Task 1B `_selection`, current slot maps, `InventorySemanticKey`.
- Produces: `%InventoryFilter`, `%InventorySort`, metadata-backed private browse state, private static comparator, filtered mutation-result rule.

- [ ] **Step 1: Add failing filter/sort tests and the hidden-mutation-result regression**

Add helpers:

```csharp
private OptionButton InventoryFilterControl() =>
    _inventoryMenu.GetNode<OptionButton>("%InventoryFilter");

private OptionButton InventorySortControl() =>
    _inventoryMenu.GetNode<OptionButton>("%InventorySort");
```

Add All/Equipment/Consumable/General/Quest filter coverage and Name/Category sort coverage.

For final ID tie-break, avoid item-art/import coupling. Use same-name/same-category items with distinct descriptions, then select the first rendered slot and assert Details describes the lower-ID item:

```csharp
[TestCase]
public void NameSort_UsesItemIdAsFinalOrdinalTieBreak()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var lower = new EquipmentItem
    {
        Id = "a_tie",
        DisplayName = "Same",
        Description = "Lower id item",
        SlotType = EquipmentSlotType.Weapon
    };
    var upper = new EquipmentItem
    {
        Id = "b_tie",
        DisplayName = "Same",
        Description = "Upper id item",
        SlotType = EquipmentSlotType.Weapon
    };
    AssertThat(player.TryAddItem(upper, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(lower, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var first = _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().First();
    first.EmitSignal(Button.SignalName.Pressed);

    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsBody").Text)
        .Contains("Lower id item");
}
```

Add the filter/mutation regression:

```csharp
[TestCase]
public void UnequipResultHiddenByFilter_ClearsSelectionWithoutResettingFilter()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var potion = ConsumableCatalog.CreateHealthPotion();
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(player.TryAddItem(potion, 1, out _)).IsTrue();
    AssertThat(player.TryEquip(sword, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var filter = InventoryFilterControl();
    // choose Consumable by its metadata-backed option, not a hard-coded semantic cast
    var consumableIndex = Enumerable.Range(0, filter.ItemCount)
        .Single(i => filter.GetItemText(i) == "Consumable");
    filter.Select(consumableIndex);
    filter.EmitSignal(OptionButton.SignalName.ItemSelected, (long)consumableIndex);

    GetSlot("%WeaponSlot").EmitSignal(Button.SignalName.Pressed);
    DetailsActionButton().EmitSignal(Button.SignalName.Pressed);

    AssertThat(player.Inventory.ContainsItem(sword.Id)).IsTrue();
    AssertThat(filter.GetItemText(filter.Selected)).IsEqual("Consumable");
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text).IsEmpty();
    AssertThat(DetailsActionButton().Visible).IsFalse();
}
```

Run controller tests; expected FAIL because browse controls do not exist.

- [ ] **Step 2: Author the Items toolbar**

Add an `HBoxContainer` between `%InventoryTitleRow` and `%InventoryScroll` with:

- `%InventoryFilter : OptionButton`
- `%InventorySort : OptionButton`

Both use existing Theme and minimum height 44. Do not add a reusable toolbar component.

- [ ] **Step 3: Add private browse enums and populate OptionButton metadata**

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

Populate exact labels and metadata in `_Ready()` initialization. Example:

```csharp
_inventoryFilterControl.AddItem("All");
_inventoryFilterControl.SetItemMetadata(
    _inventoryFilterControl.ItemCount - 1,
    (int)InventoryFilter.All);
```

Repeat explicitly for Equipment, Consumable, General, Quest and Name/Category sort.

Handlers read metadata and validate it before assignment:

```csharp
private void OnInventoryFilterSelected(long index)
{
    var metadata = _inventoryFilterControl.GetItemMetadata((int)index);
    if (metadata.VariantType != Variant.Type.Int)
        return;

    var value = (InventoryFilter)(int)metadata.AsInt64();
    if (!Enum.IsDefined(value))
        return;

    _inventoryFilter = value;
    RefreshUI();
}
```

Use equivalent explicit logic for sort. Do not cast `index` to the enum.

- [ ] **Step 4: Filter then sort using a private static comparator**

At the top of `RefreshInventoryCatalogue()`:

```csharp
var entries = _gameManager.Player.Inventory.GetAllEntries()
    .Where(entry => MatchesFilter(entry.Item.Category))
    .ToList();
entries.Sort((left, right) =>
    CompareVisibleEntries(left, right, _inventorySort));
```

Keep `MatchesFilter` feature-local. Make comparator pure:

```csharp
private static int CompareVisibleEntries(
    InventoryEntry left,
    InventoryEntry right,
    InventorySort sort)
{
    var first = sort == InventorySort.Name
        ? string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.Ordinal)
        : left.Item.Category.CompareTo(right.Item.Category);
    if (first != 0)
        return first;

    var second = sort == InventorySort.Name
        ? left.Item.Category.CompareTo(right.Item.Category)
        : string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.Ordinal);
    if (second != 0)
        return second;

    return string.Compare(left.Item.Id, right.Item.Id, StringComparison.Ordinal);
}
```

Keep it private; do not expose test-only API.

- [ ] **Step 5: Keep filter state when a mutation produces a hidden inventory item**

The Task 1B reconciliation rule already clears any selected inventory key that cannot resolve and lacks a final-consumable index fallback. Confirm Unequip uses `PreviousCatalogueIndex = -1`, so a sword hidden by `Consumable` filter clears `_selection` while `_pendingFocusRestore` still falls through to visible Items controls.

Do not reset `_inventoryFilter` to All.

- [ ] **Step 6: Map filter/sort to Items focus ownership and test the breakpoint**

Extend `ResolveFocusPage`:

```csharp
if (focused == _inventoryFilterControl ||
    focused == _inventorySortControl ||
    focused == _itemsTab)
    return InventoryPage.Items;
```

Add a 1280×720 → 640×360 resize test for each browse control proving Items remains the active compact page and the focused control remains visible.

- [ ] **Step 7: Run focused suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
git diff --check
```

Expected: PASS.

```bash
git add scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add inventory filters and deterministic sorting"
```

---

### Task 3: Add pure equipment comparison with explicit unchanged deltas

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: selected inventory `EquipmentItem`, `ResolveAccessoryEquipIndex()`, live `Player.Equipment`.
- Produces: private static bonus comparison, `%DetailsComparison` outcome text.

- [ ] **Step 1: Add failing comparison tests for occupied, empty, accessory, and zero deltas**

Add:

```csharp
[TestCase]
public void EquipmentComparison_ShowsGainsLossesAndUnchanged()
{
    var player = _gameManager.Player;
    var equipped = new EquipmentItem
    {
        Id = "equipped_compare",
        DisplayName = "Current Blade",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 3,
        DefenseBonus = 2,
        SpeedBonus = 1,
        HealthBonus = 0
    };
    var candidate = new EquipmentItem
    {
        Id = "candidate_compare",
        DisplayName = "Candidate Blade",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 5,
        DefenseBonus = 1,
        SpeedBonus = 1,
        HealthBonus = 0
    };
    AssertThat(player.TryEquip(equipped, out _)).IsTrue();
    AssertThat(player.TryAddItem(candidate, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(candidate.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);

    var comparison = _inventoryMenu.GetNode<Label>("%DetailsComparison").Text;
    AssertThat(comparison).Contains("Will replace Current Blade in Weapon");
    AssertThat(comparison).Contains("ATK +2");
    AssertThat(comparison).Contains("DEF -1");
    AssertThat(comparison).Contains("SPD unchanged");
    AssertThat(comparison).Contains("HP unchanged");
}
```

Add tests for:

- empty Weapon → `Will fill Weapon`;
- first empty accessory target;
- all accessories full → target/accessory index 0 replacement;
- comparison target and eventual Equip target match in both accessory cases.

Run controller suite; expected FAIL because comparison is not implemented.

- [ ] **Step 2: Add private pure bonus arithmetic and formatter**

```csharp
private static (int Attack, int Defense, int Speed, int Health)
    CompareEquipmentBonuses(EquipmentItem candidate, EquipmentItem? occupant)
{
    return (
        candidate.AttackBonus - (occupant?.AttackBonus ?? 0),
        candidate.DefenseBonus - (occupant?.DefenseBonus ?? 0),
        candidate.SpeedBonus - (occupant?.SpeedBonus ?? 0),
        candidate.HealthBonus - (occupant?.HealthBonus ?? 0));
}

private static string FormatDelta(string label, int delta) => delta switch
{
    > 0 => $"{label} +{delta}",
    < 0 => $"{label} {delta}",
    _ => $"{label} unchanged"
};
```

Keep both helpers private. Tests assert rendered behavior rather than widening API.

- [ ] **Step 3: Resolve comparison target through the same method as Equip**

```csharp
var accessoryIndex = candidate.SlotType == EquipmentSlotType.Accessory
    ? ResolveAccessoryEquipIndex()
    : 0;
```

Use that index to resolve occupant and format `Accessory {index + 1}`. Always render all four deltas.

Do not cache/reserve the target; Equip remains authoritative and calls `ResolveAccessoryEquipIndex()` again when acting.

- [ ] **Step 4: Run controller suite and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
git diff --check
```

Expected: PASS.

```bash
git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): add inventory equipment comparison"
```

---

### Task 4: Final compact navigation, breakpoint safety, and verification

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`
- Audit: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Audit: `tests/game/GameplayPauseHostTest.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:**
- Consumes: four-page enum, Details controls, Task 1B pending mutation restore, Task 2 browse controls.
- Produces: final Details focus ownership, no-auto-jump proof, compact mutation focus proof, final verification.

- [ ] **Step 1: Add compact no-auto-jump regression**

```csharp
[TestCase]
public async Task Compact_ItemSelectionStaysOnItemsUntilDetailsIsOpened()
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
    await AwaitFrames(1);

    AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();
    AssertThat(_menu.GetNode<Button>("%DetailsTab").ButtonPressed).IsFalse();
    AssertThat(slot.ButtonPressed).IsTrue();
}
```

- [ ] **Step 2: Complete Details focus ownership**

Extend `ResolveFocusPage`:

```csharp
if (focused == _detailsTab || focused == _detailsActionButton)
    return InventoryPage.Details;
```

Use:

```csharp
private Control? ResolveDetailsFocusTarget() =>
    CanGrabFocus(_detailsActionButton)
        ? _detailsActionButton
        : _detailsTab;
```

Use it in `RestoreFocusForPage` and `RestoreCompactPageFocus` for Details. Existing Close fallback remains final authority.

- [ ] **Step 3: Pin Details tab/LB-RB behavior**

Add tests proving:

- `%DetailsTab` shows only Details at 640×360;
- actionable selection focuses `%DetailsActionButton` when Details opens;
- selection with no action focuses `%DetailsTab`;
- raw LB/RB cycles all four pages and wraps while tree is paused.

- [ ] **Step 4: Add the highest-risk compact post-mutation regression**

```csharp
[TestCase]
public async Task Compact_EquipFromDetailsSwitchesToEquipmentAndFocusesResultSlot()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();

    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);
    _menu.GetNode<Button>("%ItemsTab").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    var itemSlot = _menu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>()
        .Single(slot => slot.TooltipText.Contains(sword.DisplayName, StringComparison.Ordinal));
    itemSlot.EmitSignal(Button.SignalName.Pressed);
    _menu.GetNode<Button>("%DetailsTab").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    var action = _menu.GetNode<Button>("%DetailsActionButton");
    action.GrabFocus();
    action.EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(2);

    var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");
    AssertThat(_menu.GetNode<Button>("%EquipmentTab").ButtonPressed).IsTrue();
    AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(weapon);
    AssertThat(action.HasFocus()).IsFalse();
}
```

Add equivalent compact final-consumable coverage if the controller test does not exercise page switching.

- [ ] **Step 5: Add standard→compact Details breakpoint regression**

With an actionable selection in standard layout:

```csharp
var action = _menu.GetNode<Button>("%DetailsActionButton");
action.GrabFocus();
await Resize(new Vector2I(640, 360));
await AwaitFrames(2);

AssertThat(_menu.GetNode<Button>("%DetailsTab").ButtonPressed).IsTrue();
AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(action);
AssertThat(action.IsVisibleInTree()).IsTrue();
```

Keep Task 2 filter/sort breakpoint tests. Together they pin every new focus-owning control.

- [ ] **Step 6: Re-run viewport containment and width assertions**

Run every `SiriusUiMetrics.VerificationViewports` entry. Standard assertions include `%DetailsPage`; compact shows exactly one of four pages. Re-run the 1024×768 no-horizontal-scroll test after final layout changes.

- [ ] **Step 7: Audit adjacent contracts only for actual stale references**

```bash
git grep -n "Click to equip\|Click to use" -- scripts tests docs
git grep -n "OnInventorySlotActivated\|OnEquipmentSlotActivated\|OnAccessorySlotActivated" -- scripts tests docs
git grep -n "three.*page\|Equipment / Items / Skills" -- tests/ui/art tests/game docs/ui/hpa-376
```

Expected:

- no final Inventory copy claims slot press directly mutates;
- removed handlers have no references;
- audit-only files change only when a grep reveals a current stale assertion/statement.

- [ ] **Step 8: Run focused UI/host regression suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~GameplayPauseHostTest"
```

Expected: PASS.

- [ ] **Step 9: Run full verification**

```bash
dotnet build Sirius.sln --no-restore --nologo
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
git diff --check main...HEAD
```

Expected:

- build: 0 errors;
- tests: 0 failed;
- diff check: clean.

Existing environment-only warnings may remain if unchanged from `main`; do not expand HPA-375 to fix unrelated warnings.

- [ ] **Step 10: Scope audit and final commit**

```bash
git diff --name-only main...HEAD
```

Expected production blast radius remains:

```text
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
```

plus the two Inventory test suites and only evidence-backed audit-file edits.

```bash
git add scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs \
  tests/ui/art/Hpa374RuntimeSmokeTest.cs \
  tests/game/GameplayPauseHostTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "test(ui): verify enhanced inventory navigation"
```

Omit unchanged audit-only paths from `git add`.

---

## Plan Self-Review

### Spec coverage

- Explicit non-mutating selection required by HPA-375 → Task 1B.
- Shared semantic key/current maps/current pending fallback → Tasks 1A/1B.
- Authored Details + fourth page + exact compact visibility → Task 1A.
- Standard 1:1.5:1 width rule + 1024 no-scroll proof → Tasks 1A/4.
- ToggleMode false-mark prevention → Task 1B.
- Tooltip mutation-copy correction → Task 1B.
- Contextual actions + current rollback semantics → Task 1B.
- Post-mutation inverted-action focus protection → Tasks 1B/4.
- Hidden mutation result under active filter clears selection without resetting filter → Task 2.
- Metadata-backed filter/sort options → Task 2.
- Deterministic Name/Category + ID tie-break through private static comparator → Task 2.
- Equipment comparison + private pure bonus arithmetic + accessory target parity → Task 3.
- Zero delta `unchanged` → Task 3.
- Compact no-auto-jump + Details/browse breakpoint focus → Task 4.
- Existing active-skill/host/domain behavior → focused/full verification.

### Review disposition

- F1 rejected: dropping explicit selection conflicts with the Linear scope and first acceptance criterion.
- F2 accepted: filtered-out mutation result clears selection while filter remains unchanged.
- F3 accepted: `_selection` is authoritative and every Select handler re-normalizes `ButtonPressed`.
- F4 accepted: Details explicitly hides CharacterColumn in compact and `VisiblePageCount()` includes four pages.
- F5 accepted: filter/sort enum values use OptionButton metadata.
- F6 accepted in intent: comparator and bonus math are private static pure helpers, but no test-only production visibility is added; tests assert observable UI behavior.
- F7 accepted: Items gets stretch ratio 1.5 and 1024×768 pins no horizontal item-grid scroll.
- F8 accepted in structure: Task 1 is split into a green structural Task 1A and atomic behavior Task 1B; no temporary focus-driven Details implementation is introduced.

### Placeholder scan

No `TBD`, `TODO`, generic "write tests", or undefined production abstraction remains.

### Type/state consistency

- `_selection` and `PendingFocusRestore.Preferred` use `InventorySemanticKey`.
- Selection resolution uses `_inventorySlotByItemId` + `_inventoryEntryBySlot`; there is no third entry map.
- Final-consumable selection and focus fallback share `PendingFocusRestore.PreviousCatalogueIndex`.
- Filter/sort handlers read metadata values, not selected indices.
- Details action routes only to existing `EquipFromInventory`, `HandleUnequip`, or `UseConsumableOutOfBattle`.
- Comparison and Equip both call `ResolveAccessoryEquipIndex()`.
- Compact page count/modulus is four everywhere.