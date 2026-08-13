# HPA-357 Inventory and Equipment Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed Sirius Inventory workbench with one responsive, host-managed character/equipment/items/skills screen while preserving current inventory behavior and HPA-374 art presentation.

**Architecture:** Keep `InventoryMenuController` as the one feature controller and `Game` / `UIScreenHost` as lifecycle owners. Add one presentational `SiriusItemSlot` leaf shared by equipment, the four real accessory slots, and dynamic inventory entries. Perform the Inventory scene rewrite, old-test migration, and dynamic catalogue conversion atomically so no intermediate commit depends on obsolete 24-slot or `TextureButton` structure.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components.

## Global Constraints

- Preserve equip, unequip, capacity rollback, consumable rollback, quantity, and explicit no-active-skill behavior.
- Preserve ordinal `DisplayName` ordering.
- Minimum supported resolution: 640×360.
- Compact rule: `SiriusUiMetrics.IsCompact` (`width < 800 || height < 450`).
- Safe margins: 24 px standard / 12 px compact; maximum content width: 1600 px.
- Slot size: 56×56 standard / 48×48 compact.
- Minimum target: 44×44 standard / 40×40 compact.
- Essential compact text remains at least 14 px.
- Reuse `SiriusTheme.tres`, UI art, hero crop `Rect2(0, 0, 96, 96)`, `SiriusStatBar`, `InputHintPresenter`, and gameplay `UIScreenHost`.
- Inventory HUD policy becomes `UIHudPolicy.Hidden`; no other `Game.TryOpenInventory` policy changes.
- `InventoryMenuController` never owns `SceneTree.Paused`, terminal Cancel, or terminal `toggle_inventory`.
- Author exactly `EquipmentSet.AccessorySlotCount` accessory slots (currently four); remove fake slot 4/5 presentation.
- Preserve generated 32 px glyphs at native size and populated item art with aspect-preserving scaling.
- Identity fallbacks match the HUD: blank name → `Adventurer`; hide MP when `MaxMana <= 0`; hide EXP when `ExperienceToNext <= 0`.
- Gold copy remains exactly `Gold: {value}`.
- Focus restoration uses screen-local semantic identity (equipment slot type / accessory index / inventory item ID), never a dynamic `Control` as identity.
- No persistent selection, comparison, filters, user sorting, Drop, Sell, Favourite, Lock, bulk actions, new InputMap actions, inventory persistence changes, battle-item redesign, view model, presenter, collection renderer, navigation service, facade, or compatibility layer.
- Do not modify `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain code. A need to do so requires design review before continuing.

---

## File Map

### Create

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`
- `tests/ui/components/SiriusItemSlotControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Modify

- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusThemeTypes.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `tests/ui/theme/SiriusUiMetricsTest.cs`
- `scenes/ui/InventoryMenu.tscn`
- `scripts/ui/InventoryMenuController.cs`
- `tests/ui/InventoryMenuControllerTest.cs`
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- `scripts/game/Game.cs`
- `tests/game/GameplayPauseHostTest.cs`

### Audit-only unless stale evidence is found

- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

---

### Task 1: Add the reusable Sirius item-slot leaf

**Files:**
- Create: `scripts/ui/components/SiriusItemSlotController.cs`
- Create: `scenes/ui/components/SiriusItemSlot.tscn`
- Create: `tests/ui/components/SiriusItemSlotControllerTest.cs`
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Modify: `scripts/ui/theme/SiriusThemeTypes.cs`
- Modify: `scripts/ui/theme/SiriusUiMetrics.cs`
- Modify: `tests/ui/theme/SiriusUiMetricsTest.cs`

**Produces:**

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

The Button root owns input/focus. `%Icon`, `%QuantityLabel`, `%StateLabel` are passive children. The component never references `Item`, `Character`, `Inventory`, `EquipmentSet`, or `GameManager`.

- [ ] **Step 1: Write the slot metric test**

Add to `SiriusUiMetricsTest.cs`:

```csharp
[TestCase]
public void ItemSlotSize_UsesApprovedGeometry()
{
    AssertThat(SiriusUiMetrics.ItemSlotSize(false)).IsEqual(new Vector2(56, 56));
    AssertThat(SiriusUiMetrics.ItemSlotSize(true)).IsEqual(new Vector2(48, 48));
}
```

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiMetricsTest.ItemSlotSize_UsesApprovedGeometry"
```

Expected: compile failure because `ItemSlotSize` does not exist.

- [ ] **Step 3: Add exactly one shared metric**

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

Do not add Inventory-specific grid/page/orbit metrics.

- [ ] **Step 4: Add exactly three Theme variation names**

```csharp
public static readonly StringName ItemSlotButton = "SiriusItemSlotButton";
public static readonly StringName ItemSlotEquippedButton = "SiriusItemSlotEquippedButton";
public static readonly StringName ItemSlotUnavailableButton = "SiriusItemSlotUnavailableButton";
```

In `SiriusTheme.tres`, use existing palette resources for:

```text
SiriusItemSlotButton
  4px radius; indigo/muted normal; cyan hover/focus

SiriusItemSlotEquippedButton
  same geometry; gold equipped border; cyan focus remains independent

SiriusItemSlotUnavailableButton
  same geometry; muted ~45% emphasis; still focusable; native disabled=false
```

- [ ] **Step 5: Write component tests for glyph art, item art, activation, and clearing**

```csharp
[TestCase]
public void PresentGlyph_KeepsGeneratedFeatureIconNativeAndCentered()
{
    _slot!.PresentGlyph(
        UiIconId.Weapon, "", "", "Weapon\nEmpty",
        SiriusItemSlotVisualState.Empty, false);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.GetSize()).IsEqual(new Vector2(32, 32));
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
    AssertThat(_slot.Icon).IsNull();
}

[TestCase]
public void PresentItem_UsesAspectCenteredItemArt()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    _slot!.PresentItem(
        sword.LoadAssetOrDefault<Texture2D>(), "", "", sword.DisplayName,
        SiriusItemSlotVisualState.Equipped, true);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
    AssertThat(_slot.Icon).IsNull();
}

[TestCase]
public void Available_EmitsOneActivation()
{
    var activations = 0;
    void OnActivated() => activations++;
    _slot!.Activated += OnActivated;

    _slot.PresentItem(null, "×2", "", "Potion x2", SiriusItemSlotVisualState.Available, true);
    _slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(activations).IsEqual(1);
    _slot.Activated -= OnActivated;
}

[TestCase]
public void UnavailableStates_RemainFocusableAndDoNotActivate()
{
    var activations = 0;
    void OnActivated() => activations++;
    _slot!.Activated += OnActivated;

    foreach (var state in new[]
             {
                 SiriusItemSlotVisualState.Empty,
                 SiriusItemSlotVisualState.Locked,
                 SiriusItemSlotVisualState.Unsupported
             })
    {
        _slot.PresentGlyph(UiIconId.Locked, "", "UNAVAILABLE", "Reason", state, false);
        _slot.GrabFocus();
        _slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(_slot.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(_slot.HasFocus()).IsTrue();
        AssertThat(activations).IsEqual(0);
    }

    _slot.Activated -= OnActivated;
}

[TestCase]
public void Present_ClearsStaleLabels()
{
    _slot!.PresentGlyph(UiIconId.Locked, "×9", "LOCKED", "Locked", SiriusItemSlotVisualState.Locked, false);
    _slot.PresentGlyph(UiIconId.Weapon, "", "", "Empty", SiriusItemSlotVisualState.Empty, false);

    AssertThat(_slot.GetNode<Label>("%QuantityLabel").Visible).IsFalse();
    AssertThat(_slot.GetNode<Label>("%StateLabel").Visible).IsFalse();
    AssertThat(_slot.TooltipText).IsEqual("Empty");
}
```

- [ ] **Step 6: Run component RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest"
```

Expected: missing component/Theme contract failures.

- [ ] **Step 7: Implement the component**

```csharp
using Godot;

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

    private TextureRect _icon = null!;
    private Label _quantityLabel = null!;
    private Label _stateLabel = null!;

    public bool Actionable { get; private set; }

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("%Icon");
        _quantityLabel = GetNode<Label>("%QuantityLabel");
        _stateLabel = GetNode<Label>("%StateLabel");
        FocusMode = FocusModeEnum.All;
        Pressed += OnPressed;
    }

    public void SetCompact(bool compact) =>
        CustomMinimumSize = SiriusUiMetrics.ItemSlotSize(compact);

    public void PresentGlyph(
        UiIconId iconId,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable)
    {
        UiIconPresenter.Apply(_icon, iconId, UiIconSize.Feature);
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
        PresentCore(quantityText, stateText, tooltipText, state, actionable);
    }

    public void PresentItem(
        Texture2D? texture,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable)
    {
        _icon.Texture = texture;
        _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        PresentCore(quantityText, stateText, tooltipText, state, actionable);
    }

    private void PresentCore(
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable)
    {
        Actionable = actionable;
        TooltipText = tooltipText ?? string.Empty;
        _quantityLabel.Text = quantityText ?? string.Empty;
        _quantityLabel.Visible = !string.IsNullOrWhiteSpace(_quantityLabel.Text);
        _stateLabel.Text = stateText ?? string.Empty;
        _stateLabel.Visible = !string.IsNullOrWhiteSpace(_stateLabel.Text);
        ThemeTypeVariation = state switch
        {
            SiriusItemSlotVisualState.Equipped => SiriusThemeTypes.ItemSlotEquippedButton,
            SiriusItemSlotVisualState.Empty or
            SiriusItemSlotVisualState.Locked or
            SiriusItemSlotVisualState.Unsupported => SiriusThemeTypes.ItemSlotUnavailableButton,
            _ => SiriusThemeTypes.ItemSlotButton
        };
    }

    private void OnPressed()
    {
        if (Actionable)
            EmitSignal(SignalName.Activated);
    }
}
```

- [ ] **Step 8: Author `SiriusItemSlot.tscn`**

```text
SiriusItemSlot (Button + SiriusItemSlotController)
├── Icon (TextureRect, unique=%Icon, full inner rect, mouse_filter=Ignore)
├── QuantityLabel (Label, unique=%QuantityLabel, bottom-right, mouse_filter=Ignore)
└── StateLabel (Label, unique=%StateLabel, bottom, mouse_filter=Ignore)

root:
  custom_minimum_size = Vector2(56, 56)
  focus_mode = All
  theme_type_variation = SiriusItemSlotButton

Icon default:
  expand_mode = IgnoreSize
  stretch_mode = KeepAspectCentered
```

No domain-specific copy belongs in this scene.

- [ ] **Step 9: Run Task 1 GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 10: Commit**

```bash
git add scripts/ui/components/SiriusItemSlotController.cs \
  scenes/ui/components/SiriusItemSlot.tscn \
  tests/ui/components/SiriusItemSlotControllerTest.cs \
  resources/ui/theme/SiriusTheme.tres \
  scripts/ui/theme/SiriusThemeTypes.cs \
  scripts/ui/theme/SiriusUiMetrics.cs \
  tests/ui/theme/SiriusUiMetricsTest.cs
git commit -m "feat(ui): add Sirius item slot component"
```

---

### Task 2: Atomically cut Inventory over to the responsive scene and dynamic catalogue

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Create: `tests/ui/InventoryMenuSceneTest.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`

**Stable unique-name contract:**

```text
%SafeFrame
%IdentityStrip
%Portrait
%PlayerName
%PlayerLevel
%HealthBar
%ManaBar
%ExperienceBar
%AttackValue
%DefenseValue
%SpeedValue
%GoldLabel
%CompactTabs
%EquipmentTab
%ItemsTab
%SkillsTab
%CharacterColumn
%EquipmentPage
%SkillsPage
%ItemsPage
%EquipmentTitleIcon
%EquipmentTitleLabel
%InventoryTitleIcon
%InventoryTitleLabel
%EquipmentSlots
%AccessorySlots
%ActiveSkillSelector
%ActiveSkillSummary
%InventoryScroll
%InventoryGrid
%FocusSummary
%CloseButton
%HelmetSlot
%WeaponSlot
%ArmorSlot
%ShieldSlot
%ShoeSlot
%AccessorySlot0
%AccessorySlot1
%AccessorySlot2
%AccessorySlot3
```

`%FocusSummary` is a `RichTextLabel`. `%InventoryGrid` is empty in the authored scene. `%AccessorySlot4` and `%AccessorySlot5` do not exist.

**Feature-local types:**

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills
}

private readonly record struct InventoryFocusKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId);

private readonly record struct PendingFocusRestore(
    InventoryFocusKey? Key,
    int InventoryIndex,
    InventoryFocusKey? MutationFallback);
```

- [ ] **Step 1: Write scene, fallback, and four-accessory tests**

Create `InventoryMenuSceneTest.cs` with a `SubViewport` fixture.

```csharp
[TestCase]
public async Task FitsEveryVerificationViewport()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        _viewport!.Size = size;
        _menu!.OpenMenu();
        await AwaitFrames(2);

        var safeFrame = _menu.GetNode<Control>("%SafeFrame");
        AssertThat(new Rect2(Vector2.Zero, size).Encloses(safeFrame.GetGlobalRect())).IsTrue();
        AssertThat(safeFrame.Size.X).IsGreater(0f);
        AssertThat(safeFrame.Size.Y).IsGreater(0f);
    }
}

[TestCase]
public async Task Standard_ShowsAllContentAreasAndStableHeadings()
{
    _viewport!.Size = new Vector2I(1280, 720);
    _menu!.OpenMenu();
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsFalse();
    AssertThat(_menu.GetNode<Control>("%EquipmentPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%SkillsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%ItemsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Label>("%EquipmentTitleLabel").Text).IsEqual("Equipment");
    AssertThat(_menu.GetNode<Label>("%InventoryTitleLabel").Text).IsEqual("Items");
}

[TestCase]
public void AuthorsExactlyDomainAccessorySlotCount()
{
    var slots = _menu!.GetNode<Container>("%AccessorySlots")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();

    AssertThat(slots.Length).IsEqual(EquipmentSet.AccessorySlotCount);
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot5")).IsNull();
}
```

Add to `InventoryMenuControllerTest.cs`:

```csharp
[TestCase]
public void OpenMenu_UsesHudCompatibleFallbacksAndExactGoldCopy()
{
    var player = _gameManager.Player;
    player.Name = "   ";
    player.MaxMana = 0;
    player.ExperienceToNext = 0;
    player.Gold = 321;

    _inventoryMenu.OpenMenu();

    AssertThat(_inventoryMenu.GetNode<Label>("%PlayerName").Text).IsEqual("Adventurer");
    AssertThat(_inventoryMenu.GetNode<SiriusStatBar>("%ManaBar").Visible).IsFalse();
    AssertThat(_inventoryMenu.GetNode<ProgressBar>("%ExperienceBar").Visible).IsFalse();
    AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text).IsEqual("Gold: 321");
}
```

- [ ] **Step 2: Write dynamic-catalogue and focus-restoration tests**

Add helper:

```csharp
private SiriusItemSlotController FindInventorySlotByTooltip(string text) =>
    _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren()
        .OfType<SiriusItemSlotController>()
        .Single(slot => slot.TooltipText.Contains(text, StringComparison.Ordinal));
```

Add:

```csharp
[TestCase]
public void Catalogue_RendersEveryCurrentItemTypeBeyondLegacyTwentyFourLimit()
{
    var player = _gameManager.Player;
    player.Inventory.Clear();

    for (var i = 29; i >= 0; i--)
    {
        var item = new EquipmentItem
        {
            Id = $"inventory_test_{i:00}",
            DisplayName = $"Item {i:00}",
            SlotType = EquipmentSlotType.Weapon
        };
        AssertThat(player.TryAddItem(item, 1, out var added)).IsTrue();
        AssertThat(added).IsEqual(1);
    }

    _inventoryMenu.OpenMenu();

    var slots = _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();
    AssertThat(slots.Length).IsEqual(30);
    AssertThat(slots[0].TooltipText).Contains("Item 00");
    AssertThat(slots[^1].TooltipText).Contains("Item 29");
}

[TestCase]
public async Task EquipActivation_RestoresFocusToResultingEquipmentSlot()
{
    var sword = EquipmentCatalog.CreateIronSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var itemSlot = FindInventorySlotByTooltip(sword.DisplayName);
    itemSlot.GrabFocus();
    itemSlot.EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var weapon = _inventoryMenu.GetNode<SiriusItemSlotController>("%WeaponSlot");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weapon);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains(sword.DisplayName);
}

[TestCase]
public async Task ConsumingFinalItem_RestoresFocusToNextCatalogueEntry()
{
    var first = ConsumableCatalog.CreateHealthPotion();
    var second = ConsumableCatalog.CreateManaPotion();
    first.DisplayName = "A First";
    second.DisplayName = "B Second";

    AssertThat(_gameManager.Player.TryAddItem(first, 1, out _)).IsTrue();
    AssertThat(_gameManager.Player.TryAddItem(second, 1, out _)).IsTrue();
    _gameManager.Player.CurrentHealth = 1;
    _inventoryMenu.OpenMenu();

    var firstSlot = FindInventorySlotByTooltip("A First");
    firstSlot.GrabFocus();
    firstSlot.EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var secondSlot = FindInventorySlotByTooltip("B Second");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(secondSlot);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains("B Second");
}

[TestCase]
public void Refresh_RePushesSummaryWhenFocusedSlotSurvives()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var slot = FindInventorySlotByTooltip(sword.DisplayName);
    slot.GrabFocus();
    _inventoryMenu.OpenMenu();

    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(slot);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains(sword.DisplayName);
}
```

- [ ] **Step 3: Write compact page/focus-neighbour/paused-input tests in Inventory suites**

In `InventoryMenuSceneTest.cs`:

```csharp
[TestCase]
public async Task Compact_LinksTabToPageAndPageToClose()
{
    _viewport!.Size = new Vector2I(640, 360);
    _menu!.OpenMenu();
    await AwaitFrames(2);

    var equipmentTab = _menu.GetNode<Button>("%EquipmentTab");
    var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");
    var close = _menu.GetNode<Button>("%CloseButton");

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
    AssertThat(VisiblePageCount()).IsEqual(1);
    AssertThat(equipmentTab.FocusNeighborBottom).IsEqual(equipmentTab.GetPathTo(weapon));
    AssertThat(weapon.FocusNeighborTop).IsEqual(weapon.GetPathTo(equipmentTab));
    AssertThat(close.FocusNeighborTop.ToString()).IsNotEqual(string.Empty);
}
```

Also in `InventoryMenuSceneTest.cs`, exercise actual viewport input while paused instead of calling `_Input` directly:

```csharp
[TestCase]
public async Task CompactShoulders_CyclePagesWhenProcessModeIsWhenPaused()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    _viewport!.Size = new Vector2I(640, 360);
    _menu!.ProcessMode = Node.ProcessModeEnum.WhenPaused;
    _menu.OpenMenu();
    tree.Paused = true;

    try
    {
        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.RightShoulder,
            Pressed = true
        });
        await AwaitFrames(2);
        AssertThat(_menu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();

        _viewport.PushInput(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.RightShoulder,
            Pressed = true
        });
        await AwaitFrames(2);
        AssertThat(_menu.GetNode<Button>("%SkillsTab").ButtonPressed).IsTrue();
    }
    finally
    {
        tree.Paused = false;
    }
}
```

Shoulder tests do not belong in `GameplayPauseHostTest` and no `inventory_page_*` InputMap actions are added.

- [ ] **Step 4: Rewrite HPA-374 Inventory smoke expectations before production**

Replace old `PanelContainer -> TextureButton` and fake `%AccessorySlot4` access with:

```csharp
var equipmentHeading = _inventoryMenu.GetNode<TextureRect>("%EquipmentTitleIcon");
var itemHeading = _inventoryMenu.GetNode<TextureRect>("%InventoryTitleIcon");
var weapon = _inventoryMenu.GetNode<SiriusItemSlotController>("%WeaponSlot");
var accessory = _inventoryMenu.GetNode<SiriusItemSlotController>("%AccessorySlot0");
var weaponIcon = weapon.GetNode<TextureRect>("%Icon");
var accessoryIcon = accessory.GetNode<TextureRect>("%Icon");
var close = _inventoryMenu.GetNode<Button>("%CloseButton");

AssertThat(equipmentHeading.Texture!.GetSize()).IsEqual(new Vector2(24, 24));
AssertThat(itemHeading.Texture!.GetSize()).IsEqual(new Vector2(24, 24));
AssertThat(weaponIcon.Texture!.GetSize()).IsEqual(new Vector2(32, 32));
AssertThat(weaponIcon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
AssertThat(accessoryIcon.Texture!.GetSize()).IsEqual(new Vector2(32, 32));
AssertThat(accessoryIcon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
AssertThat(close.Text).StartsWith("Close [");
```

After equipping a wooden sword:

```csharp
_inventoryMenu.OpenMenu();
AssertThat(weaponIcon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
AssertThat(weaponIcon.StretchMode)
    .IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
```

- [ ] **Step 5: Run the atomic cutover suite RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest"
```

Expected: new scene nodes, dynamic catalogue, fallback behavior, and `%Icon` contract are absent.

- [ ] **Step 6: Rewrite `InventoryMenu.tscn` as a full-screen SafeFrame screen**

Remove the fixed 1240×760 panel, Inventory-local `StyleBoxFlat`s, fixed `HSplitContainer`, 24 authored inventory slots, and fake fifth/sixth accessory placeholders.

Author:

```text
InventoryMenu (Theme = SiriusTheme.tres)
├── Scrim
└── SafeFrame
    └── ScreenSurface
        └── Content
            ├── IdentityStrip
            │   ├── Portrait (same hero AtlasTexture region Rect2(0,0,96,96))
            │   ├── PlayerName / PlayerLevel
            │   ├── HealthBar / ManaBar / ExperienceBar
            │   ├── AttackValue / DefenseValue / SpeedValue
            │   └── GoldLabel
            ├── CompactTabs (one ButtonGroup; 3 toggle buttons; Equipment pressed)
            ├── ResponsiveContent
            │   ├── CharacterColumn
            │   │   ├── EquipmentPage
            │   │   │   ├── EquipmentTitleRow
            │   │   │   ├── EquipmentSlots (Helmet/Weapon/Armor/Shield/Shoe)
            │   │   │   └── AccessorySlots (AccessorySlot0..3)
            │   │   └── SkillsPage
            │   │       ├── ActiveSkillSelector
            │   │       └── ActiveSkillSummary
            │   └── ItemsPage
            │       ├── InventoryTitleRow
            │       └── InventoryScroll
            │           └── InventoryGrid (no authored children)
            ├── FocusSummary (RichTextLabel)
            └── Footer/CloseButton
```

Do not use `SiriusModalShell` or a `TabContainer` as the content host.

- [ ] **Step 7: Replace controller-owned style/size/fixed-array presentation**

Delete:

```text
EquipmentPanelSize / EquipmentButtonSize
AccessoryPanelSize / AccessoryButtonSize
InventoryPanelSize / InventoryButtonSize
CacheStyles
ApplyPanelStyle
ConfigureSlotButton
_basePanelStyle / _equippedPanelStyle / _lockedPanelStyle
_inventorySlotEntries
InitializeInventorySlots authored-capacity logic
```

Bind:

```csharp
private readonly Dictionary<EquipmentSlotType, SiriusItemSlotController> _equipmentSlots = new();
private readonly List<SiriusItemSlotController> _accessorySlots = new();
private readonly List<SiriusItemSlotController> _inventorySlots = new();
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot = new();
private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId = new(StringComparer.Ordinal);

private PackedScene _itemSlotScene = null!;
private RichTextLabel _focusSummary = null!;
private InventoryPage _activeCompactPage = InventoryPage.Equipment;
private bool _isCompact;
private PendingFocusRestore? _pendingFocusRestore;
```

Load `SiriusItemSlot.tscn` once in `_Ready()`.

Bind accessories only with:

```csharp
for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
    _accessorySlots.Add(GetNode<SiriusItemSlotController>($"%AccessorySlot{index}"));
```

- [ ] **Step 8: Bind the identity strip with existing fallbacks**

```csharp
private void RefreshCharacterSummary()
{
    var player = _gameManager.Player;
    _playerName.Text = string.IsNullOrWhiteSpace(player.Name) ? "Adventurer" : player.Name;
    _playerLevel.Text = $"Lv {player.Level}";

    _healthBar.Current = player.CurrentHealth;
    _healthBar.Maximum = player.GetEffectiveMaxHealth();

    _manaBar.Visible = player.MaxMana > 0;
    if (_manaBar.Visible)
    {
        _manaBar.Current = player.CurrentMana;
        _manaBar.Maximum = player.MaxMana;
    }

    _experienceBar.Visible = player.ExperienceToNext > 0;
    if (_experienceBar.Visible)
    {
        _experienceBar.MaxValue = player.ExperienceToNext;
        _experienceBar.Value = Math.Clamp(player.Experience, 0, player.ExperienceToNext);
    }

    _attackValue.Text = player.GetEffectiveAttack().ToString();
    _defenseValue.Text = player.GetEffectiveDefense().ToString();
    _speedValue.Text = player.GetEffectiveSpeed().ToString();
    _goldLabel.Text = $"Gold: {player.Gold}";
}
```

- [ ] **Step 9: Bind fixed equipment/accessories through the slot leaf**

Empty primary:

```csharp
slot.PresentGlyph(
    UiArtCatalog.ForEquipmentSlot(slotType),
    "", "", $"{SlotDisplayName(slotType)}\nEmpty",
    SiriusItemSlotVisualState.Empty, false);
```

Empty accessory:

```csharp
slot.PresentGlyph(
    UiIconId.Accessory,
    "", "", $"Accessory Slot {index + 1}\nEmpty",
    SiriusItemSlotVisualState.Empty, false);
```

Populated:

```csharp
slot.PresentItem(
    item.LoadAssetOrDefault<Texture2D>(),
    "", "", BuildEquipmentTooltip(item),
    SiriusItemSlotVisualState.Equipped, true);
```

Activating a populated fixed slot continues to invoke the existing unequip path.

- [ ] **Step 10: Implement grow/reuse/shrink dynamic catalogue binding**

```csharp
private void RefreshInventoryCatalogue()
{
    var entries = new List<InventoryEntry>(_gameManager.Player.Inventory.GetAllEntries());
    entries.Sort((a, b) => string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal));

    while (_inventorySlots.Count < entries.Count)
        _inventorySlots.Add(CreateInventorySlot());

    while (_inventorySlots.Count > entries.Count)
    {
        var last = _inventorySlots[^1];
        _inventorySlots.RemoveAt(_inventorySlots.Count - 1);
        _inventoryEntryBySlot.Remove(last);
        last.QueueFree();
    }

    _inventoryEntryBySlot.Clear();
    _inventorySlotByItemId.Clear();

    for (var i = 0; i < entries.Count; i++)
        BindInventorySlot(_inventorySlots[i], entries[i]);
}
```

```csharp
private SiriusItemSlotController CreateInventorySlot()
{
    var slot = _itemSlotScene.Instantiate<SiriusItemSlotController>();
    _inventoryGrid.AddChild(slot);
    slot.Activated += () => OnInventorySlotActivated(slot);
    slot.FocusEntered += () => RefreshFocusSummaryFor(slot);
    slot.MouseEntered += () => RefreshFocusSummaryFor(slot);
    slot.SetCompact(_isCompact);
    return slot;
}
```

```csharp
private void BindInventorySlot(SiriusItemSlotController slot, InventoryEntry entry)
{
    _inventoryEntryBySlot[slot] = entry;
    _inventorySlotByItemId[entry.Item.Id] = slot;

    var actionable = entry.Item switch
    {
        EquipmentItem => true,
        ConsumableItem consumable when consumable.Effect?.RequiresBattle != true => true,
        _ => false
    };

    slot.SetCompact(_isCompact);
    slot.PresentItem(
        entry.Item.LoadAssetOrDefault<Texture2D>(),
        entry.Quantity > 1 ? $"×{entry.Quantity}" : string.Empty,
        actionable ? string.Empty : entry.Item is ConsumableItem ? "BATTLE ONLY" : "UNSUPPORTED",
        BuildInventoryTooltip(entry),
        actionable ? SiriusItemSlotVisualState.Available : SiriusItemSlotVisualState.Unsupported,
        actionable);
}
```

Keep existing real missing-asset warnings; remove fixed-capacity diagnostics.

- [ ] **Step 11: Implement semantic focus capture/restoration before mutations**

Factories:

```csharp
private static InventoryFocusKey EquipmentFocus(EquipmentSlotType slot) => new(slot, null, null);
private static InventoryFocusKey AccessoryFocus(int index) => new(EquipmentSlotType.Accessory, index, null);
private static InventoryFocusKey ItemFocus(string itemId) => new(null, null, itemId);
```

Before dynamic item activation:

```csharp
private void OnInventorySlotActivated(SiriusItemSlotController slot)
{
    if (!_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        return;

    var index = _inventorySlots.IndexOf(slot);
    InventoryFocusKey? fallback = entry.Item is EquipmentItem equipment
        ? equipment.SlotType == EquipmentSlotType.Accessory
            ? AccessoryFocus(0)
            : EquipmentFocus(equipment.SlotType)
        : null;

    _pendingFocusRestore = new PendingFocusRestore(ItemFocus(entry.Item.Id), index, fallback);
    ActivateInventoryEntry(entry);
}
```

Before fixed equipment/accessory activation, arm `_pendingFocusRestore` with the fixed slot identity and index `-1`, then keep the existing domain mutation order.

At the end of `RefreshUI()`:

```csharp
RestorePendingFocus();
RefreshFocusSummaryFromCurrentFocus();
```

`RestorePendingFocus()` resolves in this order:

```text
1. Same equipment slot / accessory index / item ID.
2. MutationFallback (e.g. item moved into equipment).
3. For a vanished consumable: catalogue slot at min(previousIndex, count - 1).
4. Active-page fallback when no catalogue entry remains.
5. Only GrabFocus on valid + visible + FocusMode != None.
6. Clear pending restore after one attempt.
```

No dictionary of dynamic `Control` references is used as semantic memory.

- [ ] **Step 12: Re-push the passive focus summary after every rebind**

Use current tooltip builders as the text source:

```csharp
private void RefreshFocusSummaryFor(SiriusItemSlotController slot)
{
    if (_inventoryEntryBySlot.TryGetValue(slot, out var entry))
    {
        _focusSummary.Text = BuildInventoryTooltip(entry);
        return;
    }

    if (TryBuildFixedSlotSummary(slot, out var text))
    {
        _focusSummary.Text = text;
        return;
    }

    _focusSummary.Text = string.Empty;
}
```

`RefreshFocusSummaryFromCurrentFocus()` inspects `GetViewport().GuiGetFocusOwner()` after `RefreshUI()` and updates the summary even when focus stayed on the same surviving control and `FocusEntered` did not fire.

- [ ] **Step 13: Implement Settings-style compact page buttons without `TabContainer`**

Use one scene-authored `ButtonGroup`. Named button handlers call:

```csharp
private void SetCompactPage(InventoryPage page)
{
    _activeCompactPage = page;
    _equipmentTab.ButtonPressed = page == InventoryPage.Equipment;
    _itemsTab.ButtonPressed = page == InventoryPage.Items;
    _skillsTab.ButtonPressed = page == InventoryPage.Skills;
    ApplyPageVisibility();
    RefreshCompactFocusNeighbors();
}
```

Responsive behavior:

```text
standard:
  CompactTabs hidden
  EquipmentPage + SkillsPage + ItemsPage visible
  slot size 56

compact:
  CompactTabs visible
  exactly one page visible
  same nodes reused
  slot size 48
```

- [ ] **Step 14: Link compact tab ↔ page ↔ Close focus and raw LB/RB shoulders**

```csharp
private static void LinkVertical(Control upper, Control lower)
{
    upper.FocusNeighborBottom = upper.GetPathTo(lower);
    lower.FocusNeighborTop = lower.GetPathTo(upper);
}
```

After each page/catalogue refresh:

```text
active page tab down -> first focusable page control
first page control up -> its tab
last focusable active-page control down -> Close
Close up -> last focusable active-page control
```

Keep tab left/right neighbours inside the three-tab row.

Extend existing `_Input`:

```csharp
if (Visible && _isCompact && @event is InputEventJoypadButton joy && joy.Pressed)
{
    if (joy.ButtonIndex == JoyButton.LeftShoulder)
    {
        CycleCompactPage(-1);
        GetViewport().SetInputAsHandled();
    }
    else if (joy.ButtonIndex == JoyButton.RightShoulder)
    {
        CycleCompactPage(1);
        GetViewport().SetInputAsHandled();
    }
}
```

`CycleCompactPage` wraps exactly Equipment → Items → Skills. No new InputMap actions.

- [ ] **Step 15: Migrate every existing test that encodes the old node shape**

Replace:

```csharp
private TextureButton GetSlotButton(string slotPath) =>
    _inventoryMenu.GetNode<PanelContainer>(slotPath).GetNode<TextureButton>("Button");
```

with:

```csharp
private SiriusItemSlotController GetSlot(string slotPath) =>
    _inventoryMenu.GetNode<SiriusItemSlotController>(slotPath);

private TextureRect GetSlotIcon(string slotPath) =>
    GetSlot(slotPath).GetNode<TextureRect>("%Icon");
```

Rewrite texture-state tests to assert `%Icon.Texture.ResourcePath` and `%Icon.StretchMode` rather than `TextureNormal/Hover/Pressed/Disabled/Focused`.

Keep heading tests on `%EquipmentTitleIcon`, `%InventoryTitleIcon`, `%EquipmentTitleLabel`, `%InventoryTitleLabel`.

Replace `InactiveAccessoryPlaceholders_ShowLockWithoutUnlockRule` with:

```csharp
[TestCase]
public void AccessorySlots_MatchDomainCountWithoutFakeLockedPositions()
{
    var grid = _inventoryMenu.GetNode<Container>("%AccessorySlots");
    AssertThat(grid.GetChildren().OfType<SiriusItemSlotController>().Count())
        .IsEqual(EquipmentSet.AccessorySlotCount);
    AssertThat(_inventoryMenu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
}
```

Preserve active-skill and Close-hint tests. New UI parity tests activate rendered slots through `EmitSignal(Button.SignalName.Pressed)` rather than calling private domain methods.

- [ ] **Step 16: Remove fixed-grid diagnostics**

Delete logs/warnings tied only to authored slot capacity:

```text
InitializeInventorySlots: found ...
Inventory UI slots tracked: ...
Inventory slot ...
Inventory UI only displays ... hidden.
```

Keep real domain and missing-asset warnings.

- [ ] **Step 17: Run the complete Task 2 suite GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: zero failures. Do not commit while any old `PanelContainer -> TextureButton`, fake accessory 4/5, fixed 24-slot, heading-node, art-stretch, compact-input, or focus-restoration assertion is red.

- [ ] **Step 18: Commit**

```bash
git add scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/art/Hpa374RuntimeSmokeTest.cs
git commit -m "feat(ui): redesign responsive Inventory screen"
```

---

### Task 3: Change only the Inventory HUD host policy and finish verification

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Audit: `tests/game/GameInputLifecycleTest.cs`
- Audit: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Change host tests before production**

Direct Inventory after open:

```csharp
var gameUi = _game!.GetNode<Control>("UI/GameUI");
var entry = FindEntry(host, UIScreenKinds.Inventory);
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(gameUi.Visible).IsFalse();
```

After close:

```csharp
AssertThat(gameUi.Visible).IsTrue();
```

Pause-child Inventory:

```csharp
AssertThat(inventoryEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
AssertThat(inventoryEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(inventoryEntry.Policy.PauseTree).IsFalse();
AssertThat(inventoryEntry.Policy.BlockGameplayInput).IsFalse();
AssertThat(inventoryEntry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
```

After child close, assert Pause remains active, HUD returns to Pause's visible policy, and focus returns to `%InventoryButton`.

Do not put shoulder/page tests in `GameplayPauseHostTest`.

- [ ] **Step 2: Keep content-first host initial-focus coverage**

```csharp
var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
var focus = _viewport!.GuiGetFocusOwner();
AssertThat(focus).IsNotNull();
AssertThat(focus).IsEqual(inventory.InitialFocusTarget);
AssertThat(focus).IsNotEqual(inventory.GetNode<Button>("%CloseButton"));
```

- [ ] **Step 3: Run host RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest"
```

Expected: current Inventory policy is still `UIHudPolicy.Inherit`.

- [ ] **Step 4: Make the one `Game.TryOpenInventory` policy edit**

Change only:

```csharp
Hud = UIHudPolicy.Hidden,
```

Keep parent-sensitive process/pause/block policy, cursor, lower layers, Cancel, `toggle_inventory`, external node lifetime, and `InitialFocus` unchanged.

- [ ] **Step 5: Audit `GameInputLifecycleTest` for stale Inventory paths**

```bash
rg -n "InventoryMenu|WeaponSlot|AccessorySlot[45]|InventorySlot|EquipmentTitleIcon|InventoryTitleIcon" \
  tests/game/GameInputLifecycleTest.cs
```

If no matches: leave the file unchanged.

If matches exist: replace only obsolete Inventory node paths with Task 2 stable names. Do not add page-navigation behavior to the lifecycle suite.

- [ ] **Step 6: Audit lifecycle documentation for stale Inventory presentation**

```bash
rg -n "Inventory|InventoryMenu|HUD|24 slot|AccessorySlot[45]" \
  docs/ui/hpa-376/ui-lifecycle-contract.md
```

If the Inventory row still says HUD inherited or cites removed paths, update only that row/evidence to state:

```text
Inventory: UIScreenHost-owned pause/cursor/Cancel/toggle lifecycle; HUD hidden while active;
content-first focus; parent/gameplay focus restored on close.
```

- [ ] **Step 7: Run focused HPA-357 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 8: Run full suite and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
dotnet build Sirius.sln --no-restore
```

Expected: zero test failures and zero build errors.

- [ ] **Step 9: Audit stale fixed presentation and fake accessory slots**

```bash
rg -n \
  "EquipmentPanelSize|EquipmentButtonSize|AccessoryPanelSize|AccessoryButtonSize|InventoryPanelSize|InventoryButtonSize|CacheStyles\(|_basePanelStyle|_equippedPanelStyle|_lockedPanelStyle|Inventory UI only displays|InitializeInventorySlots:|Inventory UI slots tracked" \
  scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn tests/ui
```

Expected: zero matches.

```bash
rg -n "StyleBoxFlat" scenes/ui/InventoryMenu.tscn
```

Expected: zero local Inventory `StyleBoxFlat` resources.

```bash
rg -n "AccessorySlot4|AccessorySlot5" \
  scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs tests/ui/art/Hpa374RuntimeSmokeTest.cs
```

Expected: zero positive production/test dependencies. Negative `GetNodeOrNull` assertions in `InventoryMenuSceneTest` are allowed.

```bash
rg -n "GetTree\(\)\.Paused|SceneTree\.Paused" scripts/ui/InventoryMenuController.cs
```

Expected: zero matches.

- [ ] **Step 10: Audit slot icon path and HPA-375/framework creep**

```bash
rg -n "\.Icon\s*=" scripts/ui/components/SiriusItemSlotController.cs
```

Expected: zero matches; slot art lives on `%Icon` `TextureRect`.

```bash
rg -n -i \
  "favorite|favourite|compare|comparison|filter|sort mode|drop item|sell item|bulk|inventory viewmodel|inventory presenter|collection renderer|navigation service" \
  scripts/ui/InventoryMenuController.cs scripts/ui/components/SiriusItemSlotController.cs scenes/ui/InventoryMenu.tscn
```

Expected: zero implementation matches.

- [ ] **Step 11: Check diff hygiene**

```bash
git diff --check
git status --short
git diff --name-only main...HEAD
```

Expected production/test scope:

```text
resources/ui/theme/SiriusTheme.tres
scripts/ui/theme/SiriusThemeTypes.cs
scripts/ui/theme/SiriusUiMetrics.cs
scripts/ui/components/SiriusItemSlotController.cs
scenes/ui/components/SiriusItemSlot.tscn
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
scripts/game/Game.cs
tests/ui/theme/SiriusUiMetricsTest.cs
tests/ui/components/SiriusItemSlotControllerTest.cs
tests/ui/InventoryMenuSceneTest.cs
tests/ui/InventoryMenuControllerTest.cs
tests/ui/art/Hpa374RuntimeSmokeTest.cs
tests/game/GameplayPauseHostTest.cs
```

`tests/game/GameInputLifecycleTest.cs` and `docs/ui/hpa-376/ui-lifecycle-contract.md` appear only when Steps 5/6 found the exact stale evidence described above. The HPA-357 design/plan docs are expected as implementation inputs.

- [ ] **Step 12: Commit**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
```

Add `tests/game/GameInputLifecycleTest.cs` and/or `docs/ui/hpa-376/ui-lifecycle-contract.md` only when their audits required edits.

```bash
git commit -m "feat(ui): complete hosted Inventory parity migration"
```

---

## Final Self-Review Checklist

- [ ] One `InventoryMenuController`; no presenter/view-model/collection renderer/navigation service.
- [ ] `Game` / `UIScreenHost` remain lifecycle owners; only Inventory HUD policy changes to Hidden.
- [ ] Dynamic catalogue grows/reuses/shrinks exactly current entries; no authored 24-slot or 100-placeholder capacity.
- [ ] Standard and compact reuse one content tree.
- [ ] `SiriusItemSlot` is the only new UI leaf.
- [ ] `SiriusUiMetrics` gains only `ItemSlotSize`.
- [ ] `%Icon` preserves native 32 px glyphs with `KeepCentered` and item art with `KeepAspectCentered`; Button `Icon` is unused.
- [ ] HPA-374 Inventory smoke migrates in the same atomic Task 2 cutover.
- [ ] Exactly four accessory slots are authored; fake fifth/sixth slots are gone.
- [ ] Blank name / unsupported MP / invalid EXP denominator / Gold copy match existing HUD/Inventory behavior.
- [ ] Focus restoration uses equipment slot type / accessory index / item ID plus prior catalogue index; no dynamic `Control` identity is persisted.
- [ ] Mutation restores focus to a valid semantic target and explicitly re-pushes `%FocusSummary` after rebind.
- [ ] Compact page selection copies Settings-style buttons while content remains visibility-based.
- [ ] Compact focus links tab ↔ page ↔ Close; raw LB/RB works through actual paused viewport input; no new InputMap actions exist.
- [ ] Shoulder/page tests live in Inventory suites, not the host suite.
- [ ] Existing equip/unequip/consume/rollback/active-skill methods remain the domain path.
- [ ] No domain/save-format files changed.
- [ ] Focused tests, full tests, build, `git diff --check`, stale-pattern search, and scope audit are green.
