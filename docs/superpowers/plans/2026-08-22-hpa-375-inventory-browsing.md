# HPA-375 Inventory Browsing Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explicit Inventory selection, item details, equipment comparison, category filters, deterministic sorting, and contextual actions without changing inventory/equipment domain rules.

**Architecture:** Extend the existing `InventoryMenuController` and `InventoryMenu.tscn` in place. Reuse the controller's existing semantic identity, catalogue maps, mutation paths, and `PendingFocusRestore` rather than adding parallel selection machinery. Compact selection stays on the browsing page; Details is opened explicitly, and successful mutations route focus back to the resulting slot through the existing restore pipeline.

**Tech Stack:** Godot 4.6.2, C# / .NET 8.0, GdUnit4, existing Sirius Theme/UI art/components.

**Spec:** `docs/superpowers/specs/2026-08-22-hpa-375-inventory-browsing-design.md`

## Global Constraints

- Keep implementation on this HPA-375 branch/PR; do not open a second PR for the ticket.
- Keep one `InventoryMenuController`; no browser/view-model/service/repository layer.
- Reuse `SiriusItemSlotController`, `UiArtCatalog`, `UiIconPresenter`, current catalogue maps, `PendingFocusRestore`, Theme, and existing domain operations.
- Rename/extend the current semantic focus key for selection; do not add a duplicate selection-key record.
- Do not add a third item-ID → `InventoryEntry` map or a separate pending selection index.
- Selection/filter/sort are presentation-only and are not persisted.
- Slot press selects; only `%DetailsActionButton` invokes Equip, Unequip, or Use.
- Compact slot selection does not auto-jump to Details; tabs/LB/RB open Details.
- After successful mutation, never leave focus on a `%DetailsActionButton` whose meaning just inverted.
- `ActiveSkillSelector` keeps current assignment behavior.
- Comparison and Equip use the same `ResolveAccessoryEquipIndex()` rule.
- Zero comparison deltas render as `unchanged`.
- No Drop/Sell/Favourite/Lock/bulk actions, search, extra sort modes, equipment requirements, Theme tokens, host APIs/kinds, or compatibility work.
- Preserve current equip/unequip/consumable rollback, battle-only, active-skill, host, pause/input, HUD, and save semantics.
- Extend existing Inventory tests; do not add a new E2E/screenshot harness.

---

## File Map

### Production

- Modify: `scenes/ui/InventoryMenu.tscn` — Details page, filter/sort toolbar, fourth compact tab.
- Modify: `scripts/ui/InventoryMenuController.cs` — shared semantic selection/focus identity, details, filters/sorts, comparison, contextual action, reconciliation.

### Tests

- Modify: `tests/ui/InventoryMenuControllerTest.cs` — selection, details, tooltip copy, filter/sort, comparison, action/domain parity.
- Modify: `tests/ui/InventoryMenuSceneTest.cs` — compact four-page behavior, no auto-jump, breakpoint focus mapping, post-mutation focus.

### Audit only

- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Only edit an audit-only file if implementation makes a current assertion or statement stale.

---

### Task 1: Explicit selection, Details action, and one mutation restore path

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: current `InventoryFocusKey`, `PendingFocusRestore`, `_inventoryEntryBySlot`, `_inventorySlotByItemId`, `EquipFromInventory`, `HandleUnequip`, `UseConsumableOutOfBattle`.
- Produces: renamed `InventorySemanticKey`, private `_selection`, authored Details page/tab/action, selection-only slot press, successful-mutation focus restore.

- [ ] **Step 1: Write failing tests for selection-only slot presses and stale tooltip verbs**

Add controller tests before production edits:

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
    AssertThat(_inventoryMenu.GetNode<Label>("%DetailsName").Text)
        .IsEqual(candidate.DisplayName);
    AssertThat(_inventoryMenu.GetNode<Button>("%DetailsActionButton").Text)
        .IsEqual("Equip");
    AssertThat(slot.TooltipText).Contains("Select to view details");
    AssertThat(slot.TooltipText).DoesNotContain("Click to equip");
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
public void UnsupportedInventoryEntry_SelectsAndExplainsNoAction()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var item = new GeneralItem
    {
        Id = "old_map",
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

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: FAIL because Details nodes do not exist and current slot activation still mutates.

- [ ] **Step 2: Author Details and the fourth compact tab**

In `InventoryMenu.tscn`:

- add `%DetailsTab` to the existing page `ButtonGroup` after Skills;
- add `%DetailsPage` under `%ResponsiveContent` beside `%CharacterColumn` and `%ItemsPage`;
- keep the existing outer `%PageScroll` as scroll owner;
- author `%DetailsIcon`, `%DetailsName`, `%DetailsMeta`, `%DetailsBody`, `%DetailsComparison`, `%DetailsActionReason`, `%DetailsActionButton`;
- use existing Theme variations only;
- set `%DetailsActionButton` minimum height to 44;
- initialize action/comparison/reason hidden;
- initialize body to `Select an item or equipped slot to view details.`;
- keep `%FocusSummary` and `%CloseButton` unchanged.

Extend the private page enum immediately so the scene/controller remains compile-safe:

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills,
    Details
}
```

For now, update compact page visibility/tab pressed state and LB/RB modulo from 3 to 4. Task 4 pins final navigation behavior.

- [ ] **Step 3: Reuse one semantic key for selection and focus**

Rename the current key; do not add `InventorySelectionKey`:

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

private InventorySemanticKey? _selection;

private readonly record struct PendingFocusRestore(
    InventorySemanticKey Preferred,
    int PreviousCatalogueIndex)
{
    public PendingFocusRestore WithPreferred(InventorySemanticKey preferred) =>
        this with { Preferred = preferred };
}
```

Update existing focus code from `InventoryFocusKey` to `InventorySemanticKey` without changing behavior.

Do **not** add `_visibleInventoryEntryByItemId` or `_pendingSelectionFallbackIndex`.

Add one resolver using the existing maps:

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

- [ ] **Step 4: Change Inventory-owned slot press from mutation to semantic selection**

For equipment/accessory/dynamic item slots:

- set `ToggleMode = true`;
- stop subscribing Inventory to `SiriusItemSlotController.Activated`;
- subscribe to normal `Pressed`;
- keep focus/hover summary handlers.

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

Dynamic inventory slot binding uses:

```csharp
slot.ToggleMode = true;
slot.Pressed += () => SelectInventorySlot(slot);
```

Do not change `SiriusItemSlotController`.

Delete `OnInventorySlotActivated`, `OnEquipmentSlotActivated`, and `OnAccessorySlotActivated` when no references remain.

- [ ] **Step 5: Render selection from current maps/live equipment and normalize pressed state**

Add:

```csharp
private void SelectInventorySlot(SiriusItemSlotController slot);
private void SelectEquipmentSlot(EquipmentSlotType slotType, int? accessoryIndex);
private void RefreshSelectionDetails();
private void RefreshSelectionVisuals();
private void ReconcileSelectionAfterCatalogueRefresh();
private void ClearSelection();
```

`SelectInventorySlot` reads the current `_inventoryEntryBySlot` value and stores only `InventorySemanticKey.ForItem(entry.Item.Id)`.

`SelectEquipmentSlot` ignores empty slots and stores the main/accessory semantic key only when current equipment resolves.

`RefreshSelectionDetails()` resolves inventory via `TryResolveSelectedInventoryEntry` and equipment via live `Player.Equipment`. Render current supported fields only.

`RefreshSelectionVisuals()` first sets every Inventory-owned slot `ButtonPressed = false`, then sets true only for the control currently matching `_selection`.

Call after equipment/catalogue refresh and before final focus restoration.

- [ ] **Step 6: Correct tooltip interaction copy in the same cutover**

Replace direct-mutation verbs in `BuildInventoryTooltip`:

```csharp
if (entry.Item is EquipmentItem equipmentItem)
{
    var bonuses = GetBonusText(equipmentItem);
    if (!string.IsNullOrEmpty(bonuses))
        sb.AppendLine(bonuses);
    sb.Append("Select to view details");
}
else if (entry.Item is ConsumableItem consumable)
{
    sb.AppendLine(consumable.EffectDescription);
    if (IsBattleOnly(consumable))
        sb.AppendLine("Battle use only");
    sb.Append("Select to view details");
}
else
{
    sb.Append("Select to view details");
}
```

No tooltip may retain `Click to equip` or `Click to use`.

- [ ] **Step 7: Put existing mutations behind one Details action**

Bind `%DetailsActionButton.Pressed` once and use:

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

Details action mapping is exact:

- inventory equipment → `Equip`;
- equipped item → `Unequip`;
- usable consumable → `Use`;
- battle-only → hidden + `Can only be used in battle.`;
- General/Quest/unsupported → hidden + `No inventory action is available for this item.`;
- none → hidden + neutral copy.

- [ ] **Step 8: Reuse `PendingFocusRestore` for successful mutation selection + focus**

Add a small visible-index helper:

```csharp
private int ResolveVisibleInventoryIndex(string itemId) =>
    _inventorySlotByItemId.TryGetValue(itemId, out var slot)
        ? _inventorySlots.IndexOf(slot)
        : -1;
```

Update the existing mutation methods without changing their domain ordering.

Equip success, immediately before `RefreshUI()`:

```csharp
var resultingKey = item.SlotType == EquipmentSlotType.Accessory
    ? InventorySemanticKey.ForAccessory(accessoryIndex)
    : InventorySemanticKey.ForEquipment(item.SlotType);
_selection = resultingKey;
_pendingFocusRestore = new PendingFocusRestore(resultingKey, previousCatalogueIndex);
RefreshUI();
```

Unequip success after the item has been returned to inventory:

```csharp
var resultingKey = InventorySemanticKey.ForItem(removed.Id);
_selection = resultingKey;
_pendingFocusRestore = new PendingFocusRestore(resultingKey, -1);
RefreshUI();
```

If inventory return fails and equipment rollback succeeds, do **not** replace `_selection` or create mutation pending focus; keep the current equipped selection and refresh as today.

Consumable success:

```csharp
var previousIndex = ResolveVisibleInventoryIndex(item.Id);
// existing remove -> Apply -> Notify order stays unchanged
var selectedKey = InventorySemanticKey.ForItem(item.Id);
_selection = selectedKey;
_pendingFocusRestore = new PendingFocusRestore(selectedKey, previousIndex);
RefreshUI();
```

In `ReconcileSelectionAfterCatalogueRefresh`, if selected item ID disappeared and pending focus refers to the same item with `PreviousCatalogueIndex >= 0`, select the visible entry at:

```csharp
var fallbackIndex = Math.Min(
    _pendingFocusRestore.Value.PreviousCatalogueIndex,
    _inventorySlots.Count - 1);
```

When a fallback entry exists:

```csharp
var fallbackEntry = _inventoryEntryBySlot[_inventorySlots[fallbackIndex]];
var fallbackKey = InventorySemanticKey.ForItem(fallbackEntry.Item.Id);
_selection = fallbackKey;
_pendingFocusRestore = _pendingFocusRestore.Value.WithPreferred(fallbackKey);
```

When the visible catalogue is empty, clear selection and pending preferred resolution; `RestorePendingFocus` falls through to the normal page/tab/Close safety fallback.

This is the only selection-index fallback state.

- [ ] **Step 9: Add a standard-layout regression for inverted-action focus**

Replace the old direct-activation focus expectation with:

```csharp
[TestCase]
public async Task EquipAction_MovesFocusToResultingEquipmentSlot()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip(sword.DisplayName)
        .EmitSignal(Button.SignalName.Pressed);
    var action = _inventoryMenu.GetNode<Button>("%DetailsActionButton");
    action.GrabFocus();
    action.EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var weapon = GetSlot("%WeaponSlot");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weapon);
    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Weapon)).IsEqual(sword);
    AssertThat(action.Text).IsEqual("Unequip");
    AssertThat(action.HasFocus()).IsFalse();
}
```

Rewire current rollback/use/accessory tests to select the slot first and then press `%DetailsActionButton`; preserve all existing domain assertions.

- [ ] **Step 10: Run focused suites and commit Task 1**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
git diff --check
```

Expected: PASS and no whitespace errors.

```bash
git add scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/InventoryMenuSceneTest.cs
git commit -m "feat(ui): add explicit inventory selection and details actions"
```

---

### Task 2: Category filters, deterministic sorting, and Items focus ownership

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`

**Interfaces:**
- Consumes: Task 1 `_selection`, existing slot maps, `InventorySemanticKey`.
- Produces: `%InventoryFilter`, `%InventorySort`, private filter/sort state, deterministic visible ordering, Items-page focus mapping.

- [ ] **Step 1: Add failing filter/sort tests including observable ID tie-break**

Add helpers:

```csharp
private string[] VisibleInventoryNames() =>
    _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren()
        .OfType<SiriusItemSlotController>()
        .Select(slot => slot.TooltipText.Split('\n')[0])
        .ToArray();

private OptionButton InventoryFilterControl() =>
    _inventoryMenu.GetNode<OptionButton>("%InventoryFilter");

private OptionButton InventorySortControl() =>
    _inventoryMenu.GetNode<OptionButton>("%InventorySort");
```

Pin Name ordering and final ID tie-break with two same-name/same-category items using different existing item-art paths:

```csharp
[TestCase]
public void NameSort_UsesItemIdAsFinalOrdinalTieBreak()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();
    var a = EquipmentCatalog.CreateIronSword();
    var b = EquipmentCatalog.CreateWoodenSword();
    a.Id = "a_tie";
    b.Id = "b_tie";
    a.DisplayName = "Same";
    b.DisplayName = "Same";
    AssertThat(player.TryAddItem(b, 1, out _)).IsTrue();
    AssertThat(player.TryAddItem(a, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var slots = _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();

    AssertThat(slots[0].GetNode<TextureRect>("%Icon").Texture!.ResourcePath)
        .IsEqual(a.AssetPath);
    AssertThat(slots[1].GetNode<TextureRect>("%Icon").Texture!.ResourcePath)
        .IsEqual(b.AssetPath);
}
```

Also add:

- Category sort fixture proving `ItemCategory` numeric order then ordinal name/id;
- All + Equipment + Consumable + General + Quest filter tests;
- sorting preserves selected item ID/pressed state;
- filtering selected inventory item out clears Details/action/pressed state.

Run focused controller tests and confirm FAIL because controls/behavior are absent.

- [ ] **Step 2: Author the Items toolbar**

Add an `HBoxContainer` between `%InventoryTitleRow` and `%InventoryScroll` with:

- `%InventoryFilter : OptionButton`
- `%InventorySort : OptionButton`

Both get minimum height 44 and existing Theme only. Do not add a reusable toolbar component.

- [ ] **Step 3: Add private browse enums and initialize the controls once**

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

Initialize exact labels and bind `ItemSelected` for the controller lifetime, following `ActiveSkillSelector`'s reattach-safe pattern.

- [ ] **Step 4: Filter/sort before the existing grow/reuse/shrink loop**

At the start of `RefreshInventoryCatalogue()`:

```csharp
var entries = _gameManager.Player.Inventory.GetAllEntries()
    .Where(entry => MatchesFilter(entry.Item.Category))
    .ToList();
entries.Sort(CompareVisibleEntries);
```

Implement exact comparators:

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

private int CompareVisibleEntries(InventoryEntry left, InventoryEntry right)
{
    var first = _inventorySort == InventorySort.Name
        ? string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.Ordinal)
        : left.Item.Category.CompareTo(right.Item.Category);
    if (first != 0)
        return first;

    var second = _inventorySort == InventorySort.Name
        ? left.Item.Category.CompareTo(right.Item.Category)
        : string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.Ordinal);
    if (second != 0)
        return second;

    return string.Compare(left.Item.Id, right.Item.Id, StringComparison.Ordinal);
}
```

Handlers update private state then call the existing refresh path. Sorting keeps semantic selection; filtering calls normal reconciliation and clears hidden inventory selection.

- [ ] **Step 5: Map new Items chrome in `ResolveFocusPage`**

Bind fields `_inventoryFilterControl` / `_inventorySortControl`, then extend page resolution:

```csharp
if (focused == _inventoryFilterControl || focused == _inventorySortControl || focused == _itemsTab)
    return InventoryPage.Items;
```

Keep current item-slot mapping to Items.

- [ ] **Step 6: Add a standard→compact breakpoint regression for filter focus**

In `InventoryMenuSceneTest`:

```csharp
[TestCase]
public async Task StandardToCompact_FilterFocusKeepsItemsPageVisible()
{
    await Resize(new Vector2I(1280, 720));
    _menu.OpenMenu();
    await AwaitFrames(2);

    var filter = _menu.GetNode<OptionButton>("%InventoryFilter");
    filter.GrabFocus();
    await Resize(new Vector2I(640, 360));
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();
    AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(filter);
    AssertThat(filter.IsVisibleInTree()).IsTrue();
}
```

Add the same assertion for `%InventorySort` if one parameterized/helper-backed test is simpler in the existing suite.

- [ ] **Step 7: Run focused suites and commit Task 2**

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

### Task 3: Equipment comparison with explicit unchanged deltas

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: selected inventory `EquipmentItem`, `ResolveAccessoryEquipIndex()`, live `Player.Equipment`.
- Produces: `%DetailsComparison` outcome copy and ATK/DEF/SPD/HP delta formatter.

- [ ] **Step 1: Add failing comparison tests for occupied, empty, accessory, and zero deltas**

Pin exact behavior:

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

- empty main slot → `Will fill Weapon`;
- accessory selection with first empty slot → same slot named by both comparison and eventual Equip;
- all accessory slots full → comparison targets `Accessory 1` / index 0, matching Equip replacement.

Run controller suite; expected FAIL because comparison is not implemented.

- [ ] **Step 2: Add dedicated comparison formatting; do not reuse `GetBonusText`**

Implement:

```csharp
private static string FormatDelta(string label, int delta) => delta switch
{
    > 0 => $"{label} +{delta}",
    < 0 => $"{label} {delta}",
    _ => $"{label} unchanged"
};
```

Add a comparison builder that always appends all four stats.

For an empty target, compare candidate bonuses against zero. For an occupied target, subtract target bonus from candidate bonus.

- [ ] **Step 3: Resolve accessory target through the same production method as Equip**

In comparison code:

```csharp
var accessoryIndex = candidate.SlotType == EquipmentSlotType.Accessory
    ? ResolveAccessoryEquipIndex()
    : 0;
```

Use that index both to resolve the current target and to format `Accessory {index + 1}`.

Do not cache this as a reservation. Equip calls `ResolveAccessoryEquipIndex()` again when acting; the Inventory is single-threaded presentation and current mutation remains authoritative.

- [ ] **Step 4: Run controller tests and commit Task 3**

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

### Task 4: Compact Details navigation, breakpoint safety, and final verification

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuSceneTest.cs`
- Audit: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Audit: `tests/game/GameplayPauseHostTest.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:**
- Consumes: four-page `InventoryPage`, Details controls, Task 1 mutation pending restore, Task 2 Items controls.
- Produces: explicit no-auto-jump browsing, Details focus mapping, compact post-mutation focus safety, final viewport/input proof.

- [ ] **Step 1: Add failing compact test proving selection does not auto-jump**

Use a deterministic item fixture:

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

If Task 1 implementation accidentally auto-jumps, this fails and must be corrected before proceeding.

- [ ] **Step 2: Complete Details focus ownership in page helpers**

Extend `ResolveFocusPage`:

```csharp
if (focused == _detailsTab || focused == _detailsActionButton)
    return InventoryPage.Details;
```

Extend page restore target logic:

```csharp
private Control? ResolveDetailsFocusTarget() =>
    CanGrabFocus(_detailsActionButton) ? _detailsActionButton : _detailsTab;
```

Use it in `ResolveInitialFocusTarget`, `RestoreFocusForPage`, and `RestoreCompactPageFocus` only when page is Details. Existing Close fallback remains final authority.

- [ ] **Step 3: Pin Details tab/LB-RB behavior**

Add scene tests:

- clicking `%DetailsTab` shows only Details in compact;
- when selected item has an action, Details page focus lands on `%DetailsActionButton`;
- when selected item has no action, Details page focus lands on `%DetailsTab`;
- two LB/RB cycles traverse all four pages and wrap correctly while the tree is paused.

Update `CycleCompactPage` modulus to 4 if Task 1 did not already do so:

```csharp
var page = (int)_activeCompactPage;
page = (page + direction + 4) % 4;
SetCompactPage((InventoryPage)page);
RestoreCompactPageFocus();
```

- [ ] **Step 4: Add the riskiest compact post-mutation focus regression**

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

Add equivalent focused coverage for final consumable removal falling to the next/previous visible item if the controller suite alone does not exercise compact page switching.

- [ ] **Step 5: Add standard→compact Details-control breakpoint regression**

With an actionable selected item in standard layout:

```csharp
var action = _menu.GetNode<Button>("%DetailsActionButton");
action.GrabFocus();
await Resize(new Vector2I(640, 360));
await AwaitFrames(2);

AssertThat(_menu.GetNode<Button>("%DetailsTab").ButtonPressed).IsTrue();
AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(action);
AssertThat(action.IsVisibleInTree()).IsTrue();
```

Also retain Task 2 filter/sort breakpoint tests. These together pin every new focus-owning chrome control named by `ResolveFocusPage`.

- [ ] **Step 6: Re-run viewport containment and existing spatial-navigation coverage**

Keep existing `SiriusUiMetrics.VerificationViewports`. Extend the standard assertion to include `%DetailsPage`; compact must still show exactly one of four pages and keep Close visible/bounded.

Do not add screenshot assertions or a second viewport harness.

- [ ] **Step 7: Audit adjacent current contracts only for actual stale statements**

Run:

```bash
git grep -n "Click to equip\|Click to use" -- scripts tests docs
git grep -n "Inventory.*Activated\|OnInventorySlotActivated\|OnEquipmentSlotActivated\|OnAccessorySlotActivated" -- scripts tests docs
git grep -n "three.*page\|Equipment / Items / Skills" -- tests/ui/art tests/game docs/ui/hpa-376
```

Expected:

- no final player-facing `Click to equip` / `Click to use` Inventory copy;
- removed mutation handlers have no references;
- audit-only files are changed only when these greps reveal a real stale contract.

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

Existing environment-only warnings may remain if unchanged from main; do not expand HPA-375 to fix unrelated warnings.

- [ ] **Step 10: Scope audit and commit final navigation/verification changes**

```bash
git diff --name-only main...HEAD
```

Expected production blast radius is still:

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

When an audit-only file has no change, omit it from `git add` rather than creating noise.

---

## Plan Self-Review

### Spec coverage

- Explicit non-mutating selection → Task 1.
- Details/current domain fields → Task 1.
- Reuse one semantic key/current maps/current pending fallback → Task 1.
- Post-mutation focus cannot invert action on second Confirm → Tasks 1 and 4.
- Correct selection tooltip verbs → Task 1.
- Filters/sorts + ID tie-break → Task 2.
- New filter/sort focus-page ownership → Tasks 2 and 4.
- Equipment comparison + accessory target parity → Task 3.
- Zero delta `unchanged` → Task 3.
- Compact four pages with no selection auto-jump → Task 4.
- Viewport/controller/keyboard/gamepad/mouse regression → Tasks 1, 2, 4.
- Existing equip/unequip/use/active-skill/domain behavior → Task 1 plus final full suite.

### Placeholder scan

No `TBD`, `TODO`, generic "add tests", or undefined production abstraction remains. Every new private type/control/helper used by a later task is introduced in an earlier task or the same task.

### Type/state consistency

- `_selection` and `PendingFocusRestore.Preferred` both use `InventorySemanticKey`.
- Selection resolution uses `_inventorySlotByItemId` + `_inventoryEntryBySlot`; no third item-entry map exists.
- Final-consumable selection fallback and focus fallback share `PendingFocusRestore.PreviousCatalogueIndex`.
- Details action always routes to existing `EquipFromInventory`, `HandleUnequip`, or `UseConsumableOutOfBattle`.
- Comparison and Equip both use `ResolveAccessoryEquipIndex()`.
- Compact page count is four everywhere.