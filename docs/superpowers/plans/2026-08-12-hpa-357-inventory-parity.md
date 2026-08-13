# HPA-357 Inventory and Equipment Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed Sirius Inventory workbench with a responsive, host-managed character/equipment/items/skills screen while preserving all current inventory-domain behavior.

**Architecture:** Keep `Game` as `UIScreenHost` owner and `InventoryMenuController` as the single feature controller. Add one small `SiriusItemSlot` presentation component used by equipment, accessories, and dynamic inventory entries. Standard and compact layouts reuse the same content nodes; compact mode exposes Equipment/Items/Skills pages rather than duplicating the screen.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI component stack.

## Global Constraints

- Preserve current equip, unequip, capacity rollback, consumable rollback, quantity, locked-accessory, and explicit no-active-skill behavior.
- Preserve ordinal `DisplayName` inventory ordering.
- Minimum supported logical resolution remains 640×360.
- Compact mode remains `safeFrameSize.X < 800 || safeFrameSize.Y < 450` through `SiriusUiMetrics.IsCompact`.
- Safe margins remain 24 px standard and 12 px compact; maximum content width remains 1600 px.
- Item/equipment slots are 56×56 standard and 48×48 compact.
- Essential compact text stays at least 14 px; 12 px is supporting metadata/telemetry only.
- Reuse `SiriusTheme.tres`, current UI art, hero sprite sheet, `SiriusStatBar`, `InputHintPresenter`, and the existing gameplay `UIScreenHost`.
- Inventory hides the gameplay HUD while open.
- `InventoryMenuController` does not write `SceneTree.Paused` and does not become the terminal Cancel/toggle owner.
- Do not add persistent selected-item state, comparison, filters, user sorting, Drop, Sell, Favourite, Lock, bulk actions, inventory persistence changes, or battle-item redesign.
- Do not add an inventory view model, presenter, domain facade, generic collection renderer, navigation service, or compatibility layer.
- Do not modify `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain code unless a discovered defect makes the design invalid; in that case re-review the design before proceeding.

---

## File map

### Create

- `scripts/ui/components/SiriusItemSlotController.cs` — slot visual state, labels, focusability, guarded activation.
- `scenes/ui/components/SiriusItemSlot.tscn` — reusable Button-root slot scene.
- `tests/ui/components/SiriusItemSlotControllerTest.cs` — slot contract.
- `tests/ui/InventoryMenuSceneTest.cs` — responsive layout and viewport contract.

### Modify

- `resources/ui/theme/SiriusTheme.tres` — slot Button variations only.
- `scripts/ui/theme/SiriusThemeTypes.cs` — typed slot variation names.
- `scripts/ui/theme/SiriusUiMetrics.cs` — 56/48 item-slot size helper.
- `tests/ui/theme/SiriusUiMetricsTest.cs` — size contract.
- `scenes/ui/InventoryMenu.tscn` — responsive Sirius Inventory scene.
- `scripts/ui/InventoryMenuController.cs` — binding, page/focus behavior, dynamic catalogue; existing domain operations stay here.
- `tests/ui/InventoryMenuControllerTest.cs` — parity, dynamic catalogue, focus-summary coverage.
- `scripts/game/Game.cs` — change Inventory HUD policy only.
- `tests/game/GameplayPauseHostTest.cs` — direct/Pause-child Inventory host contract.
- `tests/game/GameInputLifecycleTest.cs` — modify only if existing Inventory assertions require node/focus migration.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — update only if its Inventory row is stale after cutover.

---

### Task 1: Add one reusable Sirius item-slot component

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

    public void Present(
        Texture2D? icon,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable);
}
```

Later tasks may subscribe to `Activated`, `FocusEntered`, and `MouseEntered`. They do not reach into slot children to mutate presentation.

- [ ] **Step 1: Write the slot metric test**

Add to `tests/ui/theme/SiriusUiMetricsTest.cs`:

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

Expected: compile failure because `ItemSlotSize` is absent.

- [ ] **Step 3: Add the one shared slot metric**

Add to `scripts/ui/theme/SiriusUiMetrics.cs`:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

Do not add Inventory page/grid geometry to shared metrics.

- [ ] **Step 4: Add Theme variation names**

Add to `SiriusThemeTypes.cs`:

```csharp
public static readonly StringName ItemSlotButton = "SiriusItemSlotButton";
public static readonly StringName ItemSlotEquippedButton = "SiriusItemSlotEquippedButton";
public static readonly StringName ItemSlotUnavailableButton = "SiriusItemSlotUnavailableButton";
```

In `SiriusTheme.tres`, add the three Button variations with the existing palette:

- `SiriusItemSlotButton`: 4 px radius, indigo normal surface, muted border, cyan hover/focus.
- `SiriusItemSlotEquippedButton`: identical geometry, gold normal border, independent cyan focus ring.
- `SiriusItemSlotUnavailableButton`: identical geometry, muted/approximately 45% emphasis, still focusable.

Do not add a new color token.

- [ ] **Step 5: Write component tests**

Create a runtime fixture that instantiates `res://scenes/ui/components/SiriusItemSlot.tscn`. Add:

```csharp
[TestCase]
public void AvailableSlot_EmitsOneActivation()
{
    var activations = 0;
    void OnActivated() => activations++;
    _slot!.Activated += OnActivated;

    _slot.Present(null, "×2", "", "Potion x2", SiriusItemSlotVisualState.Available, true);
    _slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(activations).IsEqual(1);
    AssertThat(_slot.Actionable).IsTrue();
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
        _slot.Present(null, "", "UNAVAILABLE", "Reason", state, false);
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
    _slot!.Present(null, "×9", "LOCKED", "Locked", SiriusItemSlotVisualState.Locked, false);
    _slot.Present(null, "", "", "Empty", SiriusItemSlotVisualState.Empty, false);

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

- [ ] **Step 6: Run component RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest"
```

Expected: component scene/controller missing.

- [ ] **Step 7: Implement the slot controller**

Create `SiriusItemSlotController.cs`:

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

    private Label _quantityLabel = null!;
    private Label _stateLabel = null!;

    public bool Actionable { get; private set; }

    public override void _Ready()
    {
        _quantityLabel = GetNode<Label>("%QuantityLabel");
        _stateLabel = GetNode<Label>("%StateLabel");
        FocusMode = FocusModeEnum.All;
        Pressed += OnPressed;
    }

    public void SetCompact(bool compact) =>
        CustomMinimumSize = SiriusUiMetrics.ItemSlotSize(compact);

    public void Present(
        Texture2D? icon,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state,
        bool actionable)
    {
        Icon = icon;
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

The file does not import or reference `Item`, `Inventory`, `Character`, `EquipmentSet`, or `GameManager`.

- [ ] **Step 8: Author `SiriusItemSlot.tscn`**

Root: `Button` + `SiriusItemSlotController`, 56×56 default, `focus_mode = 2`, `theme_type_variation = &"SiriusItemSlotButton"`, aspect-preserving expanded icon. Add passive `%QuantityLabel` and `%StateLabel` children with `mouse_filter = 2`. The scene contains no domain-specific copy.

- [ ] **Step 9: Run GREEN**

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

### Task 2: Scene-author the responsive Inventory composition

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Create: `tests/ui/InventoryMenuSceneTest.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

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
%AccessorySlot0 .. %AccessorySlot5
```

`%FocusSummary` is a `RichTextLabel`; keep that type consistent in scene, controller, and tests.

- [ ] **Step 1: Write viewport tests first**

Create `InventoryMenuSceneTest.cs` using a `SubViewport` plus the same `GameManager` setup pattern as `InventoryMenuControllerTest`.

Use this concrete resize helper:

```csharp
private async Task ResizeViewport(Vector2I size)
{
    _viewport!.Size = size;
    await AwaitFrames(2);
}
```

Add:

```csharp
[TestCase]
public async Task FitsEveryVerificationViewport()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        await ResizeViewport(size);
        _menu!.OpenMenu();
        await AwaitFrames(2);

        var safeFrame = _menu.GetNode<Control>("%SafeFrame");
        AssertThat(new Rect2(Vector2.Zero, size).Encloses(safeFrame.GetGlobalRect())).IsTrue();
        AssertThat(safeFrame.Size.X).IsGreater(0f);
        AssertThat(safeFrame.Size.Y).IsGreater(0f);
    }
}

[TestCase]
public async Task Standard_ShowsEquipmentSkillsAndItemsTogether()
{
    await ResizeViewport(new Vector2I(1280, 720));
    _menu!.OpenMenu();
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsFalse();
    AssertThat(_menu.GetNode<Control>("%EquipmentPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%SkillsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%ItemsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
        .IsEqual(new Vector2(56, 56));
}

[TestCase]
public async Task Compact_ShowsOnePageWithPersistentIdentityAndClose()
{
    await ResizeViewport(new Vector2I(640, 360));
    _menu!.OpenMenu();
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%IdentityStrip").Visible).IsTrue();
    AssertThat(_menu.GetNode<Button>("%CloseButton").Visible).IsTrue();
    AssertThat(VisiblePageCount()).IsEqual(1);
    AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
        .IsEqual(new Vector2(48, 48));
}
```

`VisiblePageCount()` counts `%EquipmentPage`, `%ItemsPage`, and `%SkillsPage` only.

- [ ] **Step 2: Freeze character summary binding**

Add to `InventoryMenuControllerTest.cs`:

```csharp
[TestCase]
public void OpenMenu_BindsSupportedCharacterSummary()
{
    var player = _gameManager.Player;
    player.Name = "Lyra";
    player.Level = 7;
    player.CurrentHealth = 73;
    player.CurrentMana = 21;
    player.Gold = 321;

    _inventoryMenu.OpenMenu();

    AssertThat(_inventoryMenu.GetNode<Label>("%PlayerName").Text).IsEqual("Lyra");
    AssertThat(_inventoryMenu.GetNode<Label>("%PlayerLevel").Text).IsEqual("Lv 7");
    AssertThat(_inventoryMenu.GetNode<Label>("%AttackValue").Text)
        .IsEqual(player.GetEffectiveAttack().ToString());
    AssertThat(_inventoryMenu.GetNode<Label>("%DefenseValue").Text)
        .IsEqual(player.GetEffectiveDefense().ToString());
    AssertThat(_inventoryMenu.GetNode<Label>("%SpeedValue").Text)
        .IsEqual(player.GetEffectiveSpeed().ToString());
    AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text).Contains("321");
}
```

- [ ] **Step 3: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest.OpenMenu_BindsSupportedCharacterSummary"
```

Expected: new nodes/reflow do not exist.

- [ ] **Step 4: Rewrite `InventoryMenu.tscn`**

Use `SiriusTheme.tres` at the root. Replace the fixed 1240×760 panel, local `StyleBoxFlat` resources, fixed `HSplitContainer`, and 24 authored inventory slots with:

```text
InventoryMenu
├── Scrim
└── SafeFrame
    └── ScreenSurface
        └── Content
            ├── IdentityStrip
            ├── CompactTabs
            ├── ResponsiveContent
            │   ├── CharacterColumn
            │   │   ├── EquipmentPage
            │   │   └── SkillsPage
            │   └── ItemsPage
            │       └── InventoryScroll
            │           └── InventoryGrid
            ├── FocusSummary (RichTextLabel)
            └── Footer/CloseButton
```

Reuse the hero sheet/`AtlasTexture` crop `Rect2(0, 0, 96, 96)` already used by `ExplorationHud.tscn`. Reuse `SiriusStatBar` for HP/MP. Author five primary and six accessory `SiriusItemSlot` instances. `%InventoryGrid` begins empty.

- [ ] **Step 5: Remove runtime style/size ownership**

Delete the existing 108/96 size constants, `StyleBoxFlat` cache fields, `CacheStyles`, `ApplyPanelStyle`, and `ConfigureSlotButton` from `InventoryMenuController`.

Use:

```csharp
private readonly Dictionary<EquipmentSlotType, SiriusItemSlotController> _equipmentSlots = new();
private readonly List<SiriusItemSlotController> _accessorySlots = new();
```

- [ ] **Step 6: Bind character state through existing APIs**

Cache `%HealthBar`/`%ManaBar` as `SiriusStatBar`. Implement:

```csharp
private void RefreshCharacterSummary()
{
    var player = _gameManager.Player;
    _playerName.Text = player.Name;
    _playerLevel.Text = $"Lv {player.Level}";
    _healthBar.Current = player.CurrentHealth;
    _healthBar.Maximum = player.GetEffectiveMaxHealth();
    _manaBar.Current = player.CurrentMana;
    _manaBar.Maximum = player.MaxMana;
    _experienceBar.MaxValue = Math.Max(1, player.ExperienceToNext);
    _experienceBar.Value = Math.Clamp(player.Experience, 0, player.ExperienceToNext);
    _attackValue.Text = player.GetEffectiveAttack().ToString();
    _defenseValue.Text = player.GetEffectiveDefense().ToString();
    _speedValue.Text = player.GetEffectiveSpeed().ToString();
    _goldLabel.Text = $"Gold {player.Gold}";
}
```

In responsive layout, also set:

```csharp
_healthBar.Compact = _isCompact;
_manaBar.Compact = _isCompact;
```

- [ ] **Step 7: Add one local responsive page state**

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills
}

private InventoryPage _activeCompactPage = InventoryPage.Equipment;
private bool _isCompact;
```

`ApplyResponsiveLayout()` uses `SiriusUiMetrics.SafeFrameInsets(Size)`, applies safe-frame offsets, sizes all slots through `SetCompact`, and implements:

- standard: compact tabs hidden; Equipment + Skills + Items all visible;
- compact: compact tabs visible; exactly one of Equipment/Items/Skills visible;
- no duplicate compact content tree.

Wire `%EquipmentTab`, `%ItemsTab`, `%SkillsTab` to `SetCompactPage(InventoryPage)`.

- [ ] **Step 8: Add compact gamepad page cycling without new InputMap actions**

In `_Input`, after existing `InputHintPresenter.Observe` handling:

```csharp
if (Visible && _isCompact && @event is InputEventJoypadButton joy && joy.Pressed)
{
    if (joy.ButtonIndex == JoyButton.LeftShoulder)
        CycleCompactPage(-1);
    else if (joy.ButtonIndex == JoyButton.RightShoulder)
        CycleCompactPage(1);
}
```

Only call `GetViewport().SetInputAsHandled()` when a shoulder press actually changes a page. Do not handle `ui_cancel` or `toggle_inventory` as close actions.

- [ ] **Step 9: Implement content-first focus fallback**

Change:

```csharp
public Control? InitialFocusTarget => ResolveInitialFocusTarget();
```

Fallback order:

1. last valid visible focus for active page;
2. first primary equipment slot for Equipment/standard;
3. first dynamic inventory slot for Items;
4. `%ActiveSkillSelector` for Skills;
5. active compact tab;
6. `%CloseButton`.

Keep this memory inside the current screen instance only.

- [ ] **Step 10: Run GREEN and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: zero failures.

```bash
git add scenes/ui/InventoryMenu.tscn scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): scene-author responsive Inventory layout"
```

---

### Task 3: Replace the fixed 24-slot catalogue and preserve action parity

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**

```csharp
private readonly List<SiriusItemSlotController> _inventorySlots = new();
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot = new();
private PackedScene _itemSlotScene = null!;

private void RefreshInventoryCatalogue();
private void ActivateInventoryEntry(InventoryEntry entry);
private void PresentFocusSummary(string text);
```

- [ ] **Step 1: Write the >24 item regression first**

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
```

- [ ] **Step 2: Add ordering, quantity, action, and rollback coverage**

Add focused tests for:

- reverse insertion of `Zulu`, `Alpha`, `Beta` still renders `Alpha`, `Beta`, `Zulu`;
- a stackable consumable quantity 3 renders `%QuantityLabel` as `×3`;
- activating an equipment inventory slot equips it and removes that inventory entry;
- activating a populated equipment slot unequips and returns the item;
- when unequip cannot return to inventory, original equipment is restored;
- usable out-of-battle consumable decrements one and applies its existing effect;
- failed consumable application attempts existing rollback and restores quantity when rollback succeeds;
- battle-only consumable is non-actionable and does not mutate quantity;
- locked accessory is non-actionable and remains focusable;
- active skill selection and explicit `— None —` behavior remain covered by the existing tests.

Use rendered slot `EmitSignal(Button.SignalName.Pressed)` as the UI entry point. Do not call the private action methods directly in the new parity tests.

- [ ] **Step 3: Add keyboard focus-summary coverage**

```csharp
[TestCase]
public async Task KeyboardFocus_UpdatesReadableItemSummary()
{
    var sword = new EquipmentItem
    {
        Id = "focus_summary_sword",
        DisplayName = "Focus Sword",
        Description = "Readable without a mouse.",
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 4
    };
    AssertThat(_gameManager.Player.TryAddItem(sword, 1, out _)).IsTrue();

    _inventoryMenu.OpenMenu();
    var slot = _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren().OfType<SiriusItemSlotController>().Single();
    slot.GrabFocus();
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    var summary = _inventoryMenu.GetNode<RichTextLabel>("%FocusSummary");
    AssertThat(summary.Text).Contains("Focus Sword");
    AssertThat(summary.Text).Contains("Readable without a mouse.");
    AssertThat(summary.Text).Contains("+4 ATK");
}
```

- [ ] **Step 4: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: fixed authored slots fail the dynamic catalogue tests.

- [ ] **Step 5: Load one slot scene and delete the fixed-array model**

In `_Ready()`:

```csharp
_itemSlotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn")
    ?? throw new InvalidOperationException("Failed to load SiriusItemSlot.tscn.");
```

Delete `_inventorySlotEntries` and the old `InitializeInventorySlots()` child-count capacity assumption.

- [ ] **Step 6: Implement dynamic catalogue reuse**

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

    for (var i = 0; i < entries.Count; i++)
        BindInventorySlot(_inventorySlots[i], entries[i]);
}
```

`CreateInventorySlot()` instantiates `SiriusItemSlotController`, adds it to `%InventoryGrid`, connects `Activated` exactly once, connects `FocusEntered`/`MouseEntered` exactly once, and applies current compact size.

- [ ] **Step 7: Bind actionable state without changing domain rules**

```csharp
private void BindInventorySlot(SiriusItemSlotController slot, InventoryEntry entry)
{
    _inventoryEntryBySlot[slot] = entry;
    var icon = entry.Item.LoadAssetOrDefault<Texture2D>();
    var quantity = entry.Quantity > 1 ? $"×{entry.Quantity}" : string.Empty;
    var tooltip = BuildInventoryTooltip(entry);

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
    slot.Present(icon, quantity, stateText, tooltip, state, actionable);
}
```

Use the existing icon/fallback presentation when `AssetPath` is empty or invalid; do not add new art loading infrastructure.

- [ ] **Step 8: Route activation through existing methods**

```csharp
private void OnInventorySlotActivated(SiriusItemSlotController slot)
{
    if (_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        ActivateInventoryEntry(entry);
}

private void ActivateInventoryEntry(InventoryEntry entry)
{
    if (entry.Item is EquipmentItem equipment)
    {
        EquipFromInventory(equipment);
        return;
    }

    if (entry.Item is ConsumableItem consumable &&
        consumable.Effect?.RequiresBattle != true)
    {
        UseConsumableOutOfBattle(consumable);
    }
}
```

Keep transaction ordering inside `EquipFromInventory`, `HandleUnequip`, and `UseConsumableOutOfBattle` unchanged.

- [ ] **Step 9: Bind equipment/accessories through the same slot component**

Populated equipment/accessory: `Equipped`, actionable, item icon, existing tooltip. Empty active slot: `Empty`, non-actionable, type glyph and `Empty` reason. Inactive accessory: `Locked`, non-actionable, lock glyph, `Accessory Slot Locked` tooltip.

- [ ] **Step 10: Make focus summary passive and ephemeral**

```csharp
private void PresentFocusSummary(string text) =>
    _focusSummary.Text = text ?? string.Empty;
```

Update from slot `FocusEntered`/`MouseEntered` and active-skill selector focus/selection using existing tooltip builders. The summary never stores a selected item and never routes an action.

- [ ] **Step 11: Remove legacy fixed-grid diagnostics**

Delete logs/warnings that report authored slot count or hidden entries, including `Inventory UI only displays ... hidden.` Keep real domain failure and missing-asset warnings.

- [ ] **Step 12: Run GREEN and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: zero failures, including the 30-item regression.

```bash
git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): render the full Inventory catalogue"
```

---

### Task 4: Cut over the approved host lifecycle and finish verification

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `scripts/ui/InventoryMenuController.cs` only for final focus-memory fixes found by integration tests
- Modify: `tests/ui/InventoryMenuControllerTest.cs` only for those final focus-memory cases
- Modify: `tests/game/GameInputLifecycleTest.cs` only if its existing Inventory assertions require migration
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md` only if stale

- [ ] **Step 1: Change host tests before production policy**

In `DirectInventory_HostsDetachesAndReusesTheExternalView`, assert:

```csharp
var gameUi = _game!.GetNode<Control>("UI/GameUI");
// after Inventory opens
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(gameUi.Visible).IsFalse();
// after Inventory closes
AssertThat(gameUi.Visible).IsTrue();
```

In `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause`, assert the child uses `UIHudPolicy.Hidden`; after child close, Pause remains active, HUD returns to Pause's visible policy, and focus returns to `%InventoryButton`.

- [ ] **Step 2: Add content-first host focus coverage**

After direct Inventory opens:

```csharp
var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
var focus = _viewport!.GuiGetFocusOwner();
AssertThat(focus).IsNotNull();
AssertThat(focus).IsEqual(inventory.InitialFocusTarget);
AssertThat(focus).IsNotEqual(inventory.GetNode<Button>("%CloseButton"));
```

Keep the existing direct/Pause-child toggle and Cancel close paths as host-owned integration tests.

- [ ] **Step 3: Add compact shoulder page tests**

At 640×360, send `JoyButton.RightShoulder` twice and assert Items then Skills tab is selected while Inventory remains visible. At 1280×720, assert shoulder presses do not change compact-page state because compact navigation is inactive.

- [ ] **Step 4: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: current `Game` still reports `UIHudPolicy.Inherit` for Inventory.

- [ ] **Step 5: Make the only required `Game.TryOpenInventory` policy change**

Change:

```csharp
Hud = UIHudPolicy.Hidden,
```

Keep the existing parent-sensitive process/pause/block behavior, cursor, lower-layer policy, Cancel, `toggle_inventory`, external node lifetime, and `InitialFocus = () => _inventoryMenu.InitialFocusTarget` unchanged.

Do not add an Inventory host wrapper/factory.

- [ ] **Step 6: Keep focus memory screen-local**

If integration tests expose a reflow/refresh focus gap, use:

```csharp
private readonly Dictionary<InventoryPage, Control> _lastFocusByPage = new();
```

Before reusing a remembered control, require:

```csharp
GodotObject.IsInstanceValid(control) &&
control.IsVisibleInTree() &&
control.FocusMode != Control.FocusModeEnum.None
```

Remove remembered references when dynamic slots are freed. Do not persist item IDs or selected-item state across screen instances.

- [ ] **Step 7: Reconcile lifecycle documentation only if stale**

```bash
rg -n "Inventory|InventoryMenu|HUD" docs/ui/hpa-376/ui-lifecycle-contract.md
```

If needed, update only the Inventory row/evidence to state: HUD hidden; host-owned world pause and Cancel/toggle; content-first last-valid focus; parent/gameplay restoration.

- [ ] **Step 8: Run the HPA-357 focused suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 9: Run full tests and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
dotnet build Sirius.sln --no-restore
```

Expected: zero test failures and 0 build errors.

- [ ] **Step 10: Audit stale presentation code**

```bash
rg -n \
  "EquipmentPanelSize|EquipmentButtonSize|AccessoryPanelSize|AccessoryButtonSize|InventoryPanelSize|InventoryButtonSize|CacheStyles\(|_basePanelStyle|_equippedPanelStyle|_lockedPanelStyle|Inventory UI only displays|InitializeInventorySlots:|Inventory UI slots tracked" \
  scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn tests/ui
```

Expected: zero active-source matches.

```bash
rg -n "StyleBoxFlat" scenes/ui/InventoryMenu.tscn
```

Expected: zero local Inventory-screen `StyleBoxFlat` resources.

```bash
rg -n "GetTree\(\)\.Paused|SceneTree\.Paused" scripts/ui/InventoryMenuController.cs
```

Expected: zero matches.

- [ ] **Step 11: Audit HPA-375/framework creep**

```bash
rg -n -i \
  "favorite|favourite|compare|comparison|filter|sort mode|drop item|sell item|bulk|inventory viewmodel|inventory presenter|collection renderer" \
  scripts/ui/InventoryMenuController.cs scripts/ui/components/SiriusItemSlotController.cs scenes/ui/InventoryMenu.tscn
```

Expected: zero implementation matches.

- [ ] **Step 12: Check diff hygiene**

```bash
git diff --check
git status --short
git diff --name-only main...HEAD
```

Expected: only HPA-357 production/tests/docs plus directly required lifecycle evidence changed.

- [ ] **Step 13: Commit**

Stage only files actually changed:

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs \
  scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
```

Add `tests/game/GameInputLifecycleTest.cs` and/or `docs/ui/hpa-376/ui-lifecycle-contract.md` to that command only when Step 7 or existing lifecycle assertions required edits.

```bash
git commit -m "feat(ui): complete hosted Inventory parity migration"
```

---

## Final self-review checklist

- [ ] Every HPA-357 acceptance requirement maps to one task and a focused test.
- [ ] 30 current item types render without changing `Inventory.MaxItemTypes`.
- [ ] Equip/unequip/consume/rollback/active-skill paths still call the existing domain methods.
- [ ] Standard and compact modes use the same content nodes.
- [ ] Unavailable slots remain focusable but cannot mutate domain state.
- [ ] `%FocusSummary` follows focus/hover only; no persistent selected-item model exists.
- [ ] Inventory hides the gameplay HUD and `UIScreenHost` remains pause/cursor/Cancel/toggle/restoration owner.
- [ ] No domain/save-format file changed without a design re-review.
- [ ] No generic view model, presenter, renderer, navigation service, or Inventory facade was added.
- [ ] `SiriusItemSlotController` contains no item/inventory/equipment domain knowledge.
- [ ] `SiriusUiMetrics` gained only the proven 56/48 slot metric.
- [ ] Focused tests, full tests, build, `git diff --check`, stale-pattern search, and scope audit are green.
- [ ] No `TODO`, `TBD`, placeholder branch, or unresolved type choice remains in this plan.

The expected implementation remains one vertical slice: one responsive Inventory scene, one existing controller migration, one reusable slot leaf, one small Theme extension, one Inventory HUD-policy correction in `Game`, and focused tests.
