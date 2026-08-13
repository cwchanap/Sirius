# HPA-357 Inventory and Equipment Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed Sirius Inventory workbench with a responsive, host-managed character/equipment/items/skills screen while preserving all current inventory-domain behavior and the existing HPA-374 art contract.

**Architecture:** Keep `Game` as `UIScreenHost` owner and `InventoryMenuController` as the single feature controller. Add one small `SiriusItemSlot` presentational leaf used by primary equipment, the four real accessory slots, and dynamic inventory entries. Perform the Inventory scene rewrite and dynamic-catalogue conversion atomically so no intermediate commit depends on obsolete 24-slot or `TextureButton` test assumptions.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI component stack.

## Global Constraints

- Preserve current equip, unequip, capacity rollback, consumable rollback, quantity, and explicit no-active-skill behavior.
- Preserve ordinal `DisplayName` inventory ordering.
- Minimum supported logical resolution remains 640×360.
- Compact mode remains `safeFrameSize.X < 800 || safeFrameSize.Y < 450` through `SiriusUiMetrics.IsCompact`.
- Safe margins remain 24 px standard and 12 px compact; maximum content width remains 1600 px.
- Item/equipment slots are 56×56 standard and 48×48 compact.
- Minimum target sizes remain 44×44 standard and 40×40 compact.
- Essential compact text stays at least 14 px; 12 px is supporting metadata/telemetry only.
- Reuse `SiriusTheme.tres`, current UI art, hero sprite sheet, `SiriusStatBar`, `InputHintPresenter`, and the existing gameplay `UIScreenHost`.
- Reuse the hero atlas crop `Rect2(0, 0, 96, 96)` from `ExplorationHud.tscn`.
- Inventory hides the gameplay HUD while open.
- `InventoryMenuController` does not write `SceneTree.Paused` and does not become terminal Cancel/toggle owner.
- Author exactly `EquipmentSet.AccessorySlotCount` accessory slots; current value is four. Do not preserve fake fifth/sixth locked placeholders.
- Preserve native 32 px generated slot glyphs without upscaling and aspect-scaled populated item art.
- Blank player name renders `Adventurer`; MP is hidden when `MaxMana <= 0`; EXP is hidden when `ExperienceToNext <= 0`; Gold copy remains `Gold: {value}`.
- Do not add persistent selected-item state, comparison, filters, user sorting, Drop, Sell, Favourite, Lock, bulk actions, inventory persistence changes, or battle-item redesign.
- Do not add an inventory view model, presenter, domain facade, generic collection renderer, navigation service, compatibility layer, or new InputMap actions for Inventory page cycling.
- Do not modify `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain code. A discovered need to do so invalidates this plan and requires design review before implementation continues.

---

## File map

### Create

- `scripts/ui/components/SiriusItemSlotController.cs` — slot Theme state, glyph/item-art presentation, labels, focusability, guarded activation.
- `scenes/ui/components/SiriusItemSlot.tscn` — reusable Button-root slot with passive `%Icon`, `%QuantityLabel`, `%StateLabel`.
- `tests/ui/components/SiriusItemSlotControllerTest.cs` — slot contract.
- `tests/ui/InventoryMenuSceneTest.cs` — responsive layout, page navigation, focus-neighbour, and paused-input contract.

### Modify

- `resources/ui/theme/SiriusTheme.tres` — slot Button variations only.
- `scripts/ui/theme/SiriusThemeTypes.cs` — typed slot variation names.
- `scripts/ui/theme/SiriusUiMetrics.cs` — exactly one new 56/48 item-slot size helper.
- `tests/ui/theme/SiriusUiMetricsTest.cs` — slot-size contract.
- `scenes/ui/InventoryMenu.tscn` — full responsive scene rewrite; four accessories; empty runtime catalogue.
- `scripts/ui/InventoryMenuController.cs` — scene binding, dynamic catalogue, compact pages, screen-local focus identity/restoration; existing domain calls stay here.
- `tests/ui/InventoryMenuControllerTest.cs` — migrate old `PanelContainer -> TextureButton` and heading assumptions, remove fake-lock test, preserve parity tests, add focus-restoration tests.
- `tests/ui/art/Hpa374RuntimeSmokeTest.cs` — migrate Inventory smoke to `%Icon` while preserving native glyph/scaled item-art assertions.
- `scripts/game/Game.cs` — change Inventory HUD policy only.
- `tests/game/GameplayPauseHostTest.cs` — direct/Pause-child Inventory host policy and parent restoration only.
- `tests/game/GameInputLifecycleTest.cs` — edit only when the explicit stale-node audit in Task 3 finds Inventory path assertions.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — edit only when the explicit lifecycle audit in Task 3 finds stale Inventory presentation evidence.

---

### Task 1: Add the reusable Sirius item-slot leaf without regressing art presentation

**Files:**
- Create: `scripts/ui/components/SiriusItemSlotController.cs`
- Create: `scenes/ui/components/SiriusItemSlot.tscn`
- Create: `tests/ui/components/SiriusItemSlotControllerTest.cs`
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Modify: `scripts/ui/theme/SiriusThemeTypes.cs`
- Modify: `scripts/ui/theme/SiriusUiMetrics.cs`
- Modify: `tests/ui/theme/SiriusUiMetricsTest.cs`

**Interfaces:**

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

The root Button owns focus/hover/pressed/activation. `%Icon`, `%QuantityLabel`, and `%StateLabel` are passive children. Later tasks do not set `Button.Icon` or reach into these children directly.

- [ ] **Step 1: Write the shared slot-size metric test**

Add to `tests/ui/theme/SiriusUiMetricsTest.cs`:

```csharp
[TestCase]
public void ItemSlotSize_UsesApprovedGeometry()
{
    AssertThat(SiriusUiMetrics.ItemSlotSize(false)).IsEqual(new Vector2(56, 56));
    AssertThat(SiriusUiMetrics.ItemSlotSize(true)).IsEqual(new Vector2(48, 48));
}
```

- [ ] **Step 2: Run the metric test RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiMetricsTest.ItemSlotSize_UsesApprovedGeometry"
```

Expected: compile failure because `ItemSlotSize` is absent.

- [ ] **Step 3: Add exactly one shared slot metric**

Add to `scripts/ui/theme/SiriusUiMetrics.cs`:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

Do not add Inventory grid/page/orbit metrics to this shared file.

- [ ] **Step 4: Add exactly three slot Theme variations**

Add to `scripts/ui/theme/SiriusThemeTypes.cs`:

```csharp
public static readonly StringName ItemSlotButton = "SiriusItemSlotButton";
public static readonly StringName ItemSlotEquippedButton = "SiriusItemSlotEquippedButton";
public static readonly StringName ItemSlotUnavailableButton = "SiriusItemSlotUnavailableButton";
```

In `resources/ui/theme/SiriusTheme.tres` add Button variations using existing palette/style resources:

```text
SiriusItemSlotButton
  radius: 4
  normal: indigo + muted 1px border
  hover/focus: existing cyan interaction/focus treatment

SiriusItemSlotEquippedButton
  same geometry
  normal: gold selected/equipped border
  focus: cyan remains independently visible

SiriusItemSlotUnavailableButton
  same geometry
  muted approximately 45% emphasis
  still focusable; do not set native disabled=true
```

Do not add a new color token.

- [ ] **Step 5: Write component tests for activation and the two art modes**

Create `tests/ui/components/SiriusItemSlotControllerTest.cs` with a runtime fixture loading `res://scenes/ui/components/SiriusItemSlot.tscn`.

Add:

```csharp
[TestCase]
public void PresentGlyph_KeepsGeneratedFeatureIconNativeAndCentered()
{
    _slot!.SetCompact(false);
    _slot.PresentGlyph(
        UiIconId.Weapon,
        "",
        "",
        "Weapon\nEmpty",
        SiriusItemSlotVisualState.Empty,
        false);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.GetSize()).IsEqual(new Vector2(32, 32));
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
    AssertThat(_slot.Icon).IsNull();
}

[TestCase]
public void PresentItem_UsesAspectCenteredScaling()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    var texture = sword.LoadAssetOrDefault<Texture2D>();

    _slot!.PresentItem(
        texture,
        "",
        "",
        sword.DisplayName,
        SiriusItemSlotVisualState.Equipped,
        true);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
    AssertThat(_slot.Icon).IsNull();
}

[TestCase]
public void AvailableSlot_EmitsOneActivation()
{
    var activations = 0;
    void OnActivated() => activations++;
    _slot!.Activated += OnActivated;

    _slot.PresentItem(null, "×2", "", "Potion x2", SiriusItemSlotVisualState.Available, true);
    _slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(activations).IsEqual(1);
    AssertThat(_slot.Actionable).IsTrue();
    _slot.Activated -= OnActivated;
}

[TestCase]
public void EmptyLockedAndUnsupported_RemainFocusableButDoNotActivate()
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

[TestCase]
public void SetCompact_UsesSharedMetric()
{
    _slot!.SetCompact(false);
    AssertThat(_slot.CustomMinimumSize).IsEqual(new Vector2(56, 56));
    _slot.SetCompact(true);
    AssertThat(_slot.CustomMinimumSize).IsEqual(new Vector2(48, 48));
}
```

- [ ] **Step 6: Run component tests RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest"
```

Expected: missing slot scene/controller and/or Theme variation failures.

- [ ] **Step 7: Implement `SiriusItemSlotController`**

Create `scripts/ui/components/SiriusItemSlotController.cs`:

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

Do not import or reference `Item`, `Inventory`, `Character`, `EquipmentSet`, or `GameManager` in this component.

- [ ] **Step 8: Author `SiriusItemSlot.tscn`**

Use this structure and ownership:

```text
SiriusItemSlot (Button, SiriusItemSlotController)
├── Icon (TextureRect, unique=%Icon, full inner rect, mouse_filter=Ignore)
├── QuantityLabel (Label, unique=%QuantityLabel, bottom-right, mouse_filter=Ignore)
└── StateLabel (Label, unique=%StateLabel, bottom, mouse_filter=Ignore)
```

Root properties:

```text
custom_minimum_size = Vector2(56, 56)
focus_mode = All
theme_type_variation = SiriusItemSlotButton
```

`%Icon` defaults to `ExpandMode.IgnoreSize` + `KeepAspectCentered`; `PresentGlyph` overrides stretch to `KeepCentered`. The scene contains no domain-specific copy.

- [ ] **Step 9: Run Task 1 GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 10: Commit Task 1**

```bash
git add \
  scripts/ui/components/SiriusItemSlotController.cs \
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

**Stable scene contract:**

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

`%FocusSummary` is a `RichTextLabel`. There is no `%AccessorySlot4`, `%AccessorySlot5`, or authored Inventory catalogue slot.

**Feature-local interfaces:**

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

public Control? InitialFocusTarget => ResolveInitialFocusTarget();
```

- [ ] **Step 1: Write new scene/fallback/accessory tests before changing production**

Create `tests/ui/InventoryMenuSceneTest.cs` using a `SubViewport` fixture and the same `GameManager` runtime setup as `InventoryMenuControllerTest`.

Add:

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
public async Task Standard_ShowsAllThreeContentAreasAndStableHeadings()
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
public void Scene_AuthorsExactlyDomainAccessorySlotCount()
{
    var accessories = _menu!.GetNode<Container>("%AccessorySlots")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();

    AssertThat(accessories.Length).IsEqual(EquipmentSet.AccessorySlotCount);
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot5")).IsNull();
}
```

Add controller fallback tests:

```csharp
[TestCase]
public void OpenMenu_UsesHudCompatibleIdentityFallbacksAndExactGoldCopy()
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

- [ ] **Step 2: Write dynamic-catalogue and focus-identity tests before production**

Add to `InventoryMenuControllerTest.cs`:

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

    var inventorySlot = FindInventorySlotByTooltip(sword.DisplayName);
    inventorySlot.GrabFocus();
    inventorySlot.EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var weaponSlot = _inventoryMenu.GetNode<SiriusItemSlotController>("%WeaponSlot");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(weaponSlot);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains(sword.DisplayName);
}

[TestCase]
public async Task ConsumingFinalItem_RestoresFocusToNextCatalogueEntry()
{
    // Use two deterministic test consumables whose first entry is fully consumed.
    var first = ConsumableCatalog.CreateMinorHealingPotion();
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

    var remaining = FindInventorySlotByTooltip("B Second");
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(remaining);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains("B Second");
}
```

If current `ConsumableCatalog` factory names differ, use the existing concrete consumable factories already referenced by `InventoryMenuControllerTest`; do not create a new production consumable solely for this test.

Add a surviving-control regression:

```csharp
[TestCase]
public async Task Refresh_RePushesSummaryWhenFocusedSlotSurvives()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    var slot = FindInventorySlotByTooltip(sword.DisplayName);
    slot.GrabFocus();
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    _inventoryMenu.OpenMenu(); // Refreshes current bindings without requiring a new FocusEntered event.

    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner()).IsEqual(slot);
    AssertThat(_inventoryMenu.GetNode<RichTextLabel>("%FocusSummary").Text)
        .Contains(sword.DisplayName);
}
```

- [ ] **Step 3: Write compact page/keyboard/shoulder tests in Inventory-owned suites**

Add to `InventoryMenuSceneTest.cs`:

```csharp
[TestCase]
public async Task Compact_UsesSettingsStylePagesAndExplicitVerticalFocusLinks()
{
    _viewport!.Size = new Vector2I(640, 360);
    _menu!.OpenMenu();
    await AwaitFrames(2);

    var equipmentTab = _menu.GetNode<Button>("%EquipmentTab");
    var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");
    var close = _menu.GetNode<Button>("%CloseButton");

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
    AssertThat(VisiblePageCount()).IsEqual(1);
    AssertThat(weapon.FocusNeighborTop).IsEqual(weapon.GetPathTo(equipmentTab));
    AssertThat(equipmentTab.FocusNeighborBottom).IsEqual(equipmentTab.GetPathTo(weapon));
    AssertThat(close.FocusNeighborTop.IsEmpty).IsFalse();
}
```

Add to `InventoryMenuControllerTest.cs`:

```csharp
[TestCase]
public async Task CompactShoulders_CyclePagesWhileTreeIsPausedWithoutInputMapActions()
{
    var tree = (SceneTree)Engine.GetMainLoop();
    _inventoryMenu.ProcessMode = Node.ProcessModeEnum.WhenPaused;
    _inventoryMenu.Size = new Vector2(640, 360);
    _inventoryMenu.OpenMenu();
    tree.Paused = true;

    try
    {
        _inventoryMenu._Input(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.RightShoulder,
            Pressed = true
        });
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        AssertThat(_inventoryMenu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();

        _inventoryMenu._Input(new InputEventJoypadButton
        {
            ButtonIndex = JoyButton.RightShoulder,
            Pressed = true
        });
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        AssertThat(_inventoryMenu.GetNode<Button>("%SkillsTab").ButtonPressed).IsTrue();
    }
    finally
    {
        tree.Paused = false;
    }
}
```

Do not add `inventory_page_left` or `inventory_page_right` InputMap actions.

- [ ] **Step 4: Rewrite the HPA-374 runtime smoke before changing production**

In `tests/ui/art/Hpa374RuntimeSmokeTest.cs`, replace old `PanelContainer -> TextureButton` and fake `%AccessorySlot4` assumptions with:

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

After equipping the wooden sword:

```csharp
_inventoryMenu.OpenMenu();
AssertThat(weaponIcon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
AssertThat(weaponIcon.StretchMode)
    .IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
```

- [ ] **Step 5: Run the atomic cutover tests RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest"
```

Expected: new nodes/component bindings/dynamic catalogue/fallback behavior are absent.

- [ ] **Step 6: Rewrite `InventoryMenu.tscn` as one full-screen safe-frame screen**

Use `SiriusTheme.tres` at the root. Remove the fixed 1240×760 modal-like panel, local `StyleBoxFlat` resources, old fixed `HSplitContainer`, all 24 authored inventory slots, and the two fake locked accessory placeholders.

Author this hierarchy:

```text
InventoryMenu
├── Scrim
└── SafeFrame
    └── ScreenSurface
        └── Content
            ├── IdentityStrip
            │   ├── Portrait
            │   ├── PlayerName / PlayerLevel
            │   ├── HealthBar / ManaBar / ExperienceBar
            │   ├── AttackValue / DefenseValue / SpeedValue
            │   └── GoldLabel
            ├── CompactTabs
            │   ├── EquipmentTab
            │   ├── ItemsTab
            │   └── SkillsTab
            ├── ResponsiveContent
            │   ├── CharacterColumn
            │   │   ├── EquipmentPage
            │   │   │   ├── EquipmentTitleRow
            │   │   │   ├── EquipmentSlots (5 SiriusItemSlot instances)
            │   │   │   └── AccessorySlots (4 SiriusItemSlot instances)
            │   │   └── SkillsPage
            │   │       ├── ActiveSkillSelector
            │   │       └── ActiveSkillSummary
            │   └── ItemsPage
            │       ├── InventoryTitleRow
            │       └── InventoryScroll
            │           └── InventoryGrid (empty at authoring time)
            ├── FocusSummary (RichTextLabel)
            └── Footer/CloseButton
```

Use one scene-authored `ButtonGroup` shared by Equipment/Items/Skills buttons; set all three `ToggleMode=true` and Equipment pressed by default.

Reuse the Exploration HUD hero texture and `AtlasTexture` region `Rect2(0, 0, 96, 96)`. Reuse `SiriusStatBar` for HP/MP and the themed `SiriusExpBar` `ProgressBar` for EXP.

- [ ] **Step 7: Replace old controller presentation ownership and bind the stable scene**

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
private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId =
    new(StringComparer.Ordinal);

private PackedScene _itemSlotScene = null!;
private RichTextLabel _focusSummary = null!;
private InventoryPage _activeCompactPage = InventoryPage.Equipment;
private bool _isCompact;
private PendingFocusRestore? _pendingFocusRestore;
```

Load the slot scene once in `_Ready()`:

```csharp
_itemSlotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn")
    ?? throw new InvalidOperationException("Failed to load SiriusItemSlot.tscn.");
```

Bind exactly four accessory nodes and assert the authored count matches the domain constant:

```csharp
for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
    _accessorySlots.Add(GetNode<SiriusItemSlotController>($"%AccessorySlot{index}"));
```

Do not probe or create indexes 4/5.

- [ ] **Step 8: Implement HUD-compatible character summary behavior**

Use:

```csharp
private void RefreshCharacterSummary()
{
    var player = _gameManager.Player;
    _playerName.Text = string.IsNullOrWhiteSpace(player.Name)
        ? "Adventurer"
        : player.Name;
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
        _experienceBar.Value = Math.Clamp(
            player.Experience,
            0,
            player.ExperienceToNext);
    }

    _attackValue.Text = player.GetEffectiveAttack().ToString();
    _defenseValue.Text = player.GetEffectiveDefense().ToString();
    _speedValue.Text = player.GetEffectiveSpeed().ToString();
    _goldLabel.Text = $"Gold: {player.Gold}";
}
```

Set `_healthBar.Compact` / `_manaBar.Compact` from the responsive layout method.

- [ ] **Step 9: Bind primary equipment and exactly four accessories through `SiriusItemSlot`**

For an empty primary slot:

```csharp
slot.PresentGlyph(
    UiArtCatalog.ForEquipmentSlot(slotType),
    "",
    "",
    $"{SlotDisplayName(slotType)}\nEmpty",
    SiriusItemSlotVisualState.Empty,
    false);
```

For an empty accessory:

```csharp
slot.PresentGlyph(
    UiIconId.Accessory,
    "",
    "",
    $"Accessory Slot {index + 1}\nEmpty",
    SiriusItemSlotVisualState.Empty,
    false);
```

For populated equipment/accessory:

```csharp
slot.PresentItem(
    item.LoadAssetOrDefault<Texture2D>(),
    "",
    "",
    BuildEquipmentTooltip(item),
    SiriusItemSlotVisualState.Equipped,
    true);
```

Keep existing item-load warnings when an authored `AssetPath` cannot be loaded. Activating populated fixed slots continues to call the existing unequip path.

- [ ] **Step 10: Implement the dynamic catalogue with item-ID bindings**

Use:

```csharp
private void RefreshInventoryCatalogue()
{
    var entries = new List<InventoryEntry>(_gameManager.Player.Inventory.GetAllEntries());
    entries.Sort((a, b) => string.Compare(
        a.Item.DisplayName,
        b.Item.DisplayName,
        StringComparison.Ordinal));

    while (_inventorySlots.Count < entries.Count)
        _inventorySlots.Add(CreateInventorySlot());

    while (_inventorySlots.Count > entries.Count)
    {
        var last = _inventorySlots[^1];
        _inventoryEntryBySlot.Remove(last);
        _inventorySlots.RemoveAt(_inventorySlots.Count - 1);
        last.QueueFree();
    }

    _inventoryEntryBySlot.Clear();
    _inventorySlotByItemId.Clear();

    for (var i = 0; i < entries.Count; i++)
        BindInventorySlot(_inventorySlots[i], entries[i]);
}
```

`CreateInventorySlot()`:

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

Each slot is created once, so the closures above are connected once for that slot lifetime.

Bind:

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

    var state = actionable
        ? SiriusItemSlotVisualState.Available
        : SiriusItemSlotVisualState.Unsupported;
    var stateText = actionable
        ? string.Empty
        : entry.Item is ConsumableItem ? "BATTLE ONLY" : "UNSUPPORTED";

    slot.SetCompact(_isCompact);
    slot.PresentItem(
        entry.Item.LoadAssetOrDefault<Texture2D>(),
        entry.Quantity > 1 ? $"×{entry.Quantity}" : string.Empty,
        stateText,
        BuildInventoryTooltip(entry),
        state,
        actionable);
}
```

Do not add generic item-display records.

- [ ] **Step 11: Implement semantic focus capture/restore before every mutation**

Add factories/helpers around the one key type:

```csharp
private static InventoryFocusKey EquipmentFocus(EquipmentSlotType slot) =>
    new(slot, null, null);

private static InventoryFocusKey AccessoryFocus(int index) =>
    new(EquipmentSlotType.Accessory, index, null);

private static InventoryFocusKey ItemFocus(string itemId) =>
    new(null, null, itemId);
```

`CaptureCurrentFocusKey()` returns the identity corresponding to the current equipment/accessory/dynamic item slot and returns `null` for tabs/selector/Close.

Before inventory-item activation:

```csharp
private void OnInventorySlotActivated(SiriusItemSlotController slot)
{
    if (!_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        return;

    var index = _inventorySlots.IndexOf(slot);
    InventoryFocusKey? mutationFallback = entry.Item is EquipmentItem equipment
        ? equipment.SlotType == EquipmentSlotType.Accessory
            ? AccessoryFocus(0)
            : EquipmentFocus(equipment.SlotType)
        : null;

    _pendingFocusRestore = new PendingFocusRestore(
        ItemFocus(entry.Item.Id),
        index,
        mutationFallback);

    ActivateInventoryEntry(entry);
}
```

Before fixed equipment/accessory activation, arm `_pendingFocusRestore` with that fixed slot identity and `InventoryIndex = -1`; the existing `HandleUnequip` path may then refresh safely.

At the end of `RefreshUI()` after equipment/accessory/catalogue/skill bindings:

```csharp
RestorePendingFocus();
RefreshFocusSummaryFromCurrentFocus();
```

Resolution rules in `RestorePendingFocus()` are exact:

```text
1. Resolve the same primary equipment slot / accessory index / item ID.
2. If the item ID vanished and MutationFallback resolves, use it.
3. If the item ID vanished without MutationFallback, use the catalogue slot now at min(previousIndex, count - 1).
4. If catalogue is empty, use the active-page fallback.
5. GrabFocus only on a valid visible FocusMode != None control.
6. Clear _pendingFocusRestore after one attempt.
```

Never store a dynamic `Control` as semantic focus memory.

- [ ] **Step 12: Implement the passive focus summary and explicitly re-push it after rebind**

Use existing tooltip builders as the single text source.

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

`RefreshFocusSummaryFromCurrentFocus()` checks the viewport focus owner after refresh and calls the corresponding slot/active-skill summary method even when focus never left the surviving control.

The summary never controls activation and never persists selection.

- [ ] **Step 13: Implement Settings-style compact page buttons and visibility**

Bind `%EquipmentTab`, `%ItemsTab`, `%SkillsTab` to one method:

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

`ApplyResponsiveLayout()`:

```text
standard:
  CompactTabs hidden
  EquipmentPage visible
  SkillsPage visible
  ItemsPage visible
  slot size 56

compact:
  CompactTabs visible
  exactly active page visible
  CharacterColumn visible for Equipment/Skills
  ItemsPage fills body when active
  slot size 48
```

Do not use a `TabContainer` as the content host.

- [ ] **Step 14: Implement explicit compact focus neighbours and raw shoulder cycling**

Add:

```csharp
private static void LinkVertical(Control upper, Control lower)
{
    upper.FocusNeighborBottom = upper.GetPathTo(lower);
    lower.FocusNeighborTop = lower.GetPathTo(upper);
}
```

`RefreshCompactFocusNeighbors()` resolves first and last focusable controls for each page and sets:

```text
page tab down -> first page control
first page control up -> page tab
last active-page control down -> Close
Close up -> last active-page control
```

Recompute after page changes and catalogue growth/shrink.

Keep tab left/right neighbours within Equipment/Items/Skills. Do not add InputMap actions.

Extend current `_Input`:

```csharp
public override void _Input(InputEvent @event)
{
    if (_inputHintPresenter.Observe(@event) && Visible)
        RefreshCloseHint();

    if (!Visible || !_isCompact ||
        @event is not InputEventJoypadButton joy ||
        !joy.Pressed)
    {
        return;
    }

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

`CycleCompactPage` wraps through exactly three pages.

- [ ] **Step 15: Migrate all existing Inventory tests that encode the old node shape**

Replace the old helper:

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

Rewrite existing icon-transition assertions to check `%Icon.Texture.ResourcePath` and `%Icon.StretchMode`, not `TextureNormal/Hover/Pressed/Disabled/Focused` paths.

Keep `InventoryHeadings_UseReadableLabelsAndGeneratedIcons`, using the stable `%EquipmentTitleIcon`, `%InventoryTitleIcon`, `%EquipmentTitleLabel`, `%InventoryTitleLabel` names.

Replace `InactiveAccessoryPlaceholders_ShowLockWithoutUnlockRule` with:

```csharp
[TestCase]
public void AccessorySlots_MatchDomainCountWithoutFakeLockedPositions()
{
    var grid = _inventoryMenu.GetNode<Container>("%AccessorySlots");
    AssertThat(grid.GetChildren().OfType<SiriusItemSlotController>().Count())
        .IsEqual(EquipmentSet.AccessorySlotCount);
    AssertThat(_inventoryMenu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4"))
        .IsNull();
}
```

Preserve existing active-skill, Close-hint, equip/unequip/consumable tests; route new UI parity cases through `EmitSignal(Button.SignalName.Pressed)` on the rendered `SiriusItemSlotController`.

- [ ] **Step 16: Remove legacy fixed-grid diagnostics and presentation code**

Delete warnings/logs tied only to the authored-capacity model:

```text
InitializeInventorySlots: found ...
Inventory UI slots tracked: ...
Inventory slot ...
Inventory UI only displays ... hidden.
```

Keep real domain failure and missing-asset warnings.

- [ ] **Step 17: Run the complete atomic Inventory cutover suite GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: zero failures. Do not commit this task while any old `PanelContainer -> TextureButton`, fake accessory 4/5, fixed 24-slot, heading-node, art-stretch, or focus-restoration test is red.

- [ ] **Step 18: Commit Task 2**

```bash
git add \
  scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/art/Hpa374RuntimeSmokeTest.cs
git commit -m "feat(ui): redesign responsive Inventory screen"
```

---

### Task 3: Change only the Inventory host HUD policy and run final verification

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Audit and modify only on an exact stale match: `tests/game/GameInputLifecycleTest.cs`
- Audit and modify only on an exact stale match: `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Interfaces:** Keep the existing `Game.TryOpenInventory(UIScreenHandle? parent)` entry and all existing parent-sensitive host policies. No new wrapper/factory/API is introduced.

- [ ] **Step 1: Change host tests before production policy**

In `DirectInventory_HostsDetachesAndReusesTheExternalView`, assert after opening:

```csharp
var gameUi = _game!.GetNode<Control>("UI/GameUI");
var entry = FindEntry(host, UIScreenKinds.Inventory);

AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(gameUi.Visible).IsFalse();
```

After closing:

```csharp
AssertThat(gameUi.Visible).IsTrue();
```

In `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause`, assert:

```csharp
AssertThat(inventoryEntry.Policy.Parent).IsEqual(pauseEntry.Handle);
AssertThat(inventoryEntry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(inventoryEntry.Policy.PauseTree).IsFalse();
AssertThat(inventoryEntry.Policy.BlockGameplayInput).IsFalse();
AssertThat(inventoryEntry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
```

After child close, assert Pause remains active, the HUD returns to Pause's visible policy, and focus returns to `%InventoryButton`.

Do not add compact shoulder/page behavior to `GameplayPauseHostTest`.

- [ ] **Step 2: Keep content-first host focus coverage**

After direct Inventory opens:

```csharp
var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
var focus = _viewport!.GuiGetFocusOwner();
AssertThat(focus).IsNotNull();
AssertThat(focus).IsEqual(inventory.InitialFocusTarget);
AssertThat(focus).IsNotEqual(inventory.GetNode<Button>("%CloseButton"));
```

Keep existing direct/Pause-child Cancel and `toggle_inventory` close paths unchanged.

- [ ] **Step 3: Run host tests RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest"
```

Expected: Inventory still reports `UIHudPolicy.Inherit` until production changes.

- [ ] **Step 4: Make the only required `Game.TryOpenInventory` policy edit**

Change exactly:

```csharp
Hud = UIHudPolicy.Hidden,
```

Keep existing parent-sensitive `ProcessPolicy`, `PauseTree`, `BlockGameplayInput`, cursor, lower-layer policy, Cancel policy, `EntryCancelActions = { "toggle_inventory" }`, external node lifetime, and `InitialFocus = () => _inventoryMenu.InitialFocusTarget` unchanged.

Do not add an Inventory host wrapper/factory.

- [ ] **Step 5: Audit `GameInputLifecycleTest` for stale Inventory node assumptions**

Run:

```bash
rg -n "InventoryMenu|WeaponSlot|AccessorySlot[45]|InventorySlot|EquipmentTitleIcon|InventoryTitleIcon" \
  tests/game/GameInputLifecycleTest.cs
```

If zero matches: do not edit the file.

If matches exist: replace only old scene-node assertions with the stable HPA-357 names from Task 2. Do not add page-navigation behavior to this lifecycle suite.

- [ ] **Step 6: Audit lifecycle documentation for stale Inventory presentation evidence**

Run:

```bash
rg -n "Inventory|InventoryMenu|HUD|24 slot|AccessorySlot[45]" \
  docs/ui/hpa-376/ui-lifecycle-contract.md
```

If the Inventory row still states inherited HUD or old presentation paths, update only that row/evidence to:

```text
Inventory: UIScreenHost-owned pause/cursor/Cancel/toggle lifecycle; HUD hidden while active;
content-first screen focus; parent/gameplay focus restored on close.
```

Do not rewrite unrelated lifecycle rows.

- [ ] **Step 7: Run the focused HPA-357 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 8: Run full tests and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
dotnet build Sirius.sln --no-restore
```

Expected: zero test failures and zero build errors.

- [ ] **Step 9: Audit stale fixed Inventory presentation**

```bash
rg -n \
  "EquipmentPanelSize|EquipmentButtonSize|AccessoryPanelSize|AccessoryButtonSize|InventoryPanelSize|InventoryButtonSize|CacheStyles\(|_basePanelStyle|_equippedPanelStyle|_lockedPanelStyle|Inventory UI only displays|InitializeInventorySlots:|Inventory UI slots tracked" \
  scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn tests/ui
```

Expected: zero active-source matches.

```bash
rg -n "StyleBoxFlat" scenes/ui/InventoryMenu.tscn
```

Expected: zero Inventory-local `StyleBoxFlat` resources.

```bash
rg -n "AccessorySlot4|AccessorySlot5" \
  scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs tests/ui/art/Hpa374RuntimeSmokeTest.cs
```

Expected: zero active production/test dependencies on fake fifth/sixth accessory slots. A negative `GetNodeOrNull` assertion in `InventoryMenuSceneTest` is allowed.

```bash
rg -n "GetTree\(\)\.Paused|SceneTree\.Paused" scripts/ui/InventoryMenuController.cs
```

Expected: zero matches.

- [ ] **Step 10: Audit icon-pipeline and HPA-375/framework creep**

```bash
rg -n "\.Icon\s*=|UiIconPresenter\.Apply\([^,]*Button" \
  scripts/ui/components/SiriusItemSlotController.cs scripts/ui/InventoryMenuController.cs
```

Expected: no Button-icon usage for `SiriusItemSlot`; glyph art is on `%Icon` `TextureRect`.

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

Expected changed production/test scope:

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

`tests/game/GameInputLifecycleTest.cs` and `docs/ui/hpa-376/ui-lifecycle-contract.md` appear only when Steps 5/6 found the exact stale evidence described above. Design/plan docs are expected because this implementation follows the planning PR.

- [ ] **Step 12: Commit Task 3**

Stage required files:

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
```

Add `tests/game/GameInputLifecycleTest.cs` and/or `docs/ui/hpa-376/ui-lifecycle-contract.md` only if the exact audits required edits.

```bash
git commit -m "feat(ui): complete hosted Inventory parity migration"
```

---

## Final self-review checklist

- [ ] Every HPA-357 acceptance requirement maps to a task and focused test.
- [ ] One `InventoryMenuController`; no presenter/view-model/collection renderer/navigation service.
- [ ] `Game` / `UIScreenHost` remain lifecycle owners; the only `Game.TryOpenInventory` policy edit is `Hud = UIHudPolicy.Hidden`.
- [ ] Dynamic catalogue grows/reuses/shrinks exactly current entries; no authored 24-slot or 100-placeholder capacity.
- [ ] Standard and compact modes use the same content nodes.
- [ ] `SiriusItemSlot` is the only new UI leaf.
- [ ] `SiriusUiMetrics` gains only `ItemSlotSize`.
- [ ] `%Icon` `TextureRect` preserves native 32 px glyphs with `KeepCentered` and populated item art with `KeepAspectCentered`; Button `Icon` is unused.
- [ ] HPA-374 Inventory smoke is migrated in the same atomic Task 2 cutover.
- [ ] Exactly four accessory slots are authored, matching `EquipmentSet.AccessorySlotCount`; fake slot 4/5 presentation is removed.
- [ ] Blank name / unsupported MP / invalid EXP denominator / Gold copy match existing HUD/Inventory fallbacks.
- [ ] Focus restoration uses equipment slot type / accessory index / item ID plus previous catalogue index; no dynamic `Control` identity is persisted.
- [ ] Catalogue mutation restores focus to a valid semantic target and explicitly re-pushes `%FocusSummary` after rebind.
- [ ] Compact page selection copies the Settings-style button pattern while keeping visibility-based content, not a `TabContainer` host.
- [ ] Compact focus neighbours connect tab ↔ page ↔ Close; raw LB/RB shoulders work while paused; no new InputMap actions exist.
- [ ] Shoulder/page behavior is tested in Inventory controller/scene suites, not the host suite.
- [ ] Existing equip/unequip/consume/rollback/active-skill paths still invoke the existing domain methods.
- [ ] No domain/save-format file changes.
- [ ] Focused tests, full tests, build, `git diff --check`, stale-pattern search, and scope audit are green.
