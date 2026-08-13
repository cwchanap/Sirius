# HPA-357 Inventory and Equipment Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed Sirius Inventory workbench with a responsive, host-managed character/equipment/items/skills screen while preserving all current inventory-domain behavior.

**Architecture:** Keep `Game` as `UIScreenHost` owner and keep `InventoryMenuController` as the single feature controller. Add one small `SiriusItemSlot` presentational leaf used by equipment, accessories, and dynamic inventory entries; keep all item/equipment/consumable/skill rules in their existing domain owners. Standard and compact modes reuse the same content nodes, with compact mode exposing Equipment/Items/Skills pages instead of duplicating the screen.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI component stack.

## Global Constraints

- Preserve current equip, unequip, capacity rollback, consumable rollback, quantity, locked-accessory, and explicit no-active-skill behavior.
- Preserve deterministic inventory ordering using `string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.Ordinal)`.
- Minimum supported logical resolution remains 640×360.
- Compact mode remains `safeFrameSize.X < 800 || safeFrameSize.Y < 450` through `SiriusUiMetrics.IsCompact`.
- Safe margins remain 24 px standard and 12 px compact; ultrawide content remains capped at 1600 px.
- Item/equipment slots are 56×56 standard and 48×48 compact.
- Minimum control targets remain 44×44 standard and 40×40 compact.
- Essential compact text remains at least 14 px; 12 px is limited to supporting metadata/telemetry.
- Reuse `SiriusTheme.tres`, current UI art, hero sprite sheet, `SiriusStatBar`, `InputHintPresenter`, and the existing gameplay `UIScreenHost`.
- Inventory hides the gameplay HUD while open.
- `InventoryMenuController` must not write `SceneTree.Paused` or become the terminal Cancel owner.
- Do not add persistent selected-item state, comparison, filters, user sorting, Drop, Sell, Favourite, Lock, bulk actions, inventory persistence changes, or battle-item redesign.
- Do not add an inventory view model, presenter, domain facade, generic collection renderer, navigation service, or compatibility layer.
- No `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain change is expected; stop and re-review the design before adding one.

---

## File map

### New production files

- `scripts/ui/components/SiriusItemSlotController.cs` — one presentational slot, state styling, labels, tooltip, focusability, guarded activation.
- `scenes/ui/components/SiriusItemSlot.tscn` — Button-root slot scene.

### New test files

- `tests/ui/components/SiriusItemSlotControllerTest.cs` — component state/activation contract.
- `tests/ui/InventoryMenuSceneTest.cs` — viewport/reflow/fit/focus smoke coverage.

### Existing files to modify

- `resources/ui/theme/SiriusTheme.tres` — slot Button variations only.
- `scripts/ui/theme/SiriusThemeTypes.cs` — typed names for slot variations.
- `scripts/ui/theme/SiriusUiMetrics.cs` — approved 56/48 slot metric.
- `tests/ui/theme/SiriusUiMetricsTest.cs` — slot metric contract.
- `scenes/ui/InventoryMenu.tscn` — full responsive scene rewrite.
- `scripts/ui/InventoryMenuController.cs` — scene binding, responsive pages, dynamic catalogue, focus summary; retain domain operations.
- `tests/ui/InventoryMenuControllerTest.cs` — migrate existing coverage and add parity/catalogue/focus tests.
- `scripts/game/Game.cs` — Inventory HUD policy correction only.
- `tests/game/GameplayPauseHostTest.cs` — direct/Pause-child Inventory policy and restoration.
- `tests/game/GameInputLifecycleTest.cs` — update only if Inventory node/focus assumptions in existing lifecycle tests require migration.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — reconcile Inventory row/source evidence after cutover if stale.

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

**Interfaces:**
- Consumes: `SiriusTheme.tres`, `SiriusUiMetrics.ItemSlotSize(bool)`, native `Button` focus/hover/pressed behavior.
- Produces:

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

Later tasks may subscribe to `Activated`, `FocusEntered`, and `MouseEntered`. They must not reach into slot children to set visuals directly.

- [ ] **Step 1: Freeze the slot-size metric with a failing test**

Add to `tests/ui/theme/SiriusUiMetricsTest.cs`:

```csharp
[TestCase]
public void ItemSlotSize_UsesApprovedStandardAndCompactGeometry()
{
    AssertThat(SiriusUiMetrics.ItemSlotSize(compact: false))
        .IsEqual(new Vector2(56, 56));
    AssertThat(SiriusUiMetrics.ItemSlotSize(compact: true))
        .IsEqual(new Vector2(48, 48));
}
```

- [ ] **Step 2: Run the metric test and confirm RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiMetricsTest.ItemSlotSize_UsesApprovedStandardAndCompactGeometry"
```

Expected: compile failure because `SiriusUiMetrics.ItemSlotSize` does not exist.

- [ ] **Step 3: Add exactly one shared slot-size helper**

Add to `scripts/ui/theme/SiriusUiMetrics.cs`:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

Do not add inventory-grid, equipment-orbit, or page-layout metrics to this shared file.

- [ ] **Step 4: Add typed Theme variation names**

Add to `scripts/ui/theme/SiriusThemeTypes.cs`:

```csharp
public static readonly StringName ItemSlotButton = "SiriusItemSlotButton";
public static readonly StringName ItemSlotEquippedButton = "SiriusItemSlotEquippedButton";
public static readonly StringName ItemSlotUnavailableButton = "SiriusItemSlotUnavailableButton";
```

In `resources/ui/theme/SiriusTheme.tres`, add Button variations with these contracts:

- `SiriusItemSlotButton`: 4 px radius, indigo normal surface, muted normal border, cyan hover/focus treatment, no text padding that changes the 56/48 outer geometry.
- `SiriusItemSlotEquippedButton`: same geometry, gold normal/selected border, cyan focus ring retained independently.
- `SiriusItemSlotUnavailableButton`: same geometry, muted surface/border and approximately 45% visual emphasis, but do **not** set native `disabled=true` because unavailable slots must remain focusable for their reason.

Reuse the approved palette already present in the Theme. Do not introduce a new color token.

- [ ] **Step 5: Write the component contract tests**

Create `tests/ui/components/SiriusItemSlotControllerTest.cs` with a runtime scene fixture and these concrete cases:

```csharp
[TestCase]
public void AvailableSlot_EmitsOneActivatedSignal()
{
    var activations = 0;
    _slot!.Activated += () => activations++;
    _slot.Present(null, "×2", "", "Potion x2", SiriusItemSlotVisualState.Available, true);

    _slot.EmitSignal(Button.SignalName.Pressed);

    AssertThat(activations).IsEqual(1);
    AssertThat(_slot.Actionable).IsTrue();
}

[TestCase]
public void UnavailableStates_RemainFocusableButDoNotActivate()
{
    foreach (var state in new[]
             {
                 SiriusItemSlotVisualState.Empty,
                 SiriusItemSlotVisualState.Locked,
                 SiriusItemSlotVisualState.Unsupported
             })
    {
        var activations = 0;
        _slot!.Activated += () => activations++;
        _slot.Present(null, "", "Unavailable", "Reason", state, false);

        _slot.GrabFocus();
        _slot.EmitSignal(Button.SignalName.Pressed);

        AssertThat(_slot.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(_slot.HasFocus()).IsTrue();
        AssertThat(activations).IsEqual(0);
        _slot.Activated -= () => activations++;
    }
}

[TestCase]
public void Present_ClearsStaleQuantityAndStateText()
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

For the unavailable-state loop, use a fresh slot fixture or a named local event handler so signal unsubscribe is exact; do not keep accumulating anonymous handlers.

- [ ] **Step 6: Run the component tests and confirm RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest"
```

Expected: component scene/controller missing and tests fail.

- [ ] **Step 7: Implement the minimal slot controller**

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

    public void SetCompact(bool compact)
    {
        CustomMinimumSize = SiriusUiMetrics.ItemSlotSize(compact);
    }

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
            SiriusItemSlotVisualState.Locked or
            SiriusItemSlotVisualState.Unsupported or
            SiriusItemSlotVisualState.Empty => SiriusThemeTypes.ItemSlotUnavailableButton,
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

Do not import `Item`, `Character`, `Inventory`, or `GameManager` here.

- [ ] **Step 8: Author the slot scene**

Create `scenes/ui/components/SiriusItemSlot.tscn` with:

- root `Button` scripted by `SiriusItemSlotController`;
- `focus_mode = 2` (`All`);
- `theme_type_variation = &"SiriusItemSlotButton"`;
- 56×56 default minimum size;
- icon expanded and aspect-preserving, capped to fit within the slot;
- bottom-right `%QuantityLabel` using `SiriusMetadata`/numeric-readable typography;
- bottom `%StateLabel` using `SiriusTelemetry` or compact metadata typography;
- child labels `mouse_filter = 2` so the root Button owns pointer/focus input.

No domain-specific text such as `Weapon`, `Potion`, or `Locked` is authored in this reusable scene.

- [ ] **Step 9: Run Task 1 tests GREEN**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: all selected tests pass.

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

### Task 2: Scene-author the responsive Inventory composition

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Create: `tests/ui/InventoryMenuSceneTest.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusItemSlotController`, `SiriusUiMetrics.SafeFrameInsets`, `SiriusUiMetrics.IsCompact`, `SiriusStatBar`, current `Character.GetEffective*` methods.
- Produces:

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills
}

public Control? InitialFocusTarget => ResolveInitialFocusTarget();
```

Stable unique node names consumed by the controller/tests:

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
%EquipmentSlots
%AccessorySlots
%ActiveSkillSelector
%ActiveSkillSummary
%InventoryScroll
%InventoryGrid
%FocusSummary
%CloseButton
```

The five primary equipment slots keep stable names `%HelmetSlot`, `%WeaponSlot`, `%ArmorSlot`, `%ShieldSlot`, `%ShoeSlot`; six accessories keep `%AccessorySlot0` through `%AccessorySlot5`.

- [ ] **Step 1: Add responsive scene tests before rewriting the scene**

Create `tests/ui/InventoryMenuSceneTest.cs` using the same `GameManager` runtime setup pattern as `InventoryMenuControllerTest` and a `SubViewport` fixture.

Add a test that iterates all approved viewports:

```csharp
[TestCase]
public async Task InventoryMenu_FitsEveryVerificationViewport()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        await ResizeViewport(size);
        _menu!.OpenMenu();
        await AwaitFrames(2);

        var safeFrame = _menu.GetNode<Control>("%SafeFrame");
        var screenRect = new Rect2(Vector2.Zero, size);
        AssertThat(screenRect.Encloses(safeFrame.GetGlobalRect())).IsTrue();
        AssertThat(safeFrame.Size.X).IsGreater(0f);
        AssertThat(safeFrame.Size.Y).IsGreater(0f);
    }
}
```

Add deep layout cases:

```csharp
[TestCase]
public async Task StandardLayout_ShowsCharacterSkillsAndItemsTogether()
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
public async Task CompactLayout_ShowsOnePageAndKeepsIdentityAndCloseVisible()
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

`VisiblePageCount()` must count `EquipmentPage`, `ItemsPage`, and `SkillsPage` according to the compact active-page contract, not `CharacterColumn` itself.

- [ ] **Step 2: Add character-summary binding tests before implementation**

In `tests/ui/InventoryMenuControllerTest.cs` add:

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
    AssertThat(_inventoryMenu.GetNode<Label>("%GoldLabel").Text).Contains("321");
    AssertThat(_inventoryMenu.GetNode<Label>("%AttackValue").Text)
        .IsEqual(player.GetEffectiveAttack().ToString());
    AssertThat(_inventoryMenu.GetNode<Label>("%DefenseValue").Text)
        .IsEqual(player.GetEffectiveDefense().ToString());
    AssertThat(_inventoryMenu.GetNode<Label>("%SpeedValue").Text)
        .IsEqual(player.GetEffectiveSpeed().ToString());
}
```

Keep the existing active-skill tests; update only the node path if the scene rewrite changes it.

- [ ] **Step 3: Run Task 2 tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest.OpenMenu_BindsSupportedCharacterSummary"
```

Expected: new nodes and responsive behavior are missing.

- [ ] **Step 4: Rewrite `InventoryMenu.tscn` around the shared Theme**

Replace the fixed 1240×760 `MainPanel`, local `StyleBoxFlat` resources, and `HSplitContainer` with the structure defined in the design.

Required scene decisions:

- root `InventoryMenu` fills viewport and owns `SiriusTheme.tres`;
- background uses a themed Sirius scrim, not a local color/style resource;
- `%SafeFrame` fills the root; controller applies shared safe-frame offsets;
- `%IdentityStrip` is always visible;
- `%CompactTabs` is authored once and starts hidden at standard design size;
- `%CharacterColumn` contains `%EquipmentPage` and `%SkillsPage`;
- `%ItemsPage` contains `%InventoryScroll/%InventoryGrid` but **no authored inventory item slots**;
- five equipment and six accessory slots are instances of `SiriusItemSlot.tscn` with stable unique names;
- `%FocusSummary` is a bounded/wrapping or scrollable passive text surface;
- `%CloseButton` stays fixed and uses the existing close input-hint presentation;
- reuse the existing hero sheet and `AtlasTexture` region `Rect2(0, 0, 96, 96)` from `ExplorationHud.tscn`;
- use `SiriusStatBar` for HP and MP and `SiriusExpBar` for EXP;
- remove all production emoji and local slot-size/style resources from the old scene.

Do not author a second compact copy of equipment, items, or skills.

- [ ] **Step 5: Replace runtime style/sizing ownership in `InventoryMenuController`**

Delete:

```csharp
EquipmentPanelSize
EquipmentButtonSize
AccessoryPanelSize
AccessoryButtonSize
InventoryPanelSize
InventoryButtonSize
_basePanelStyle
_equippedPanelStyle
_lockedPanelStyle
CacheStyles()
ApplyPanelStyle(...)
ConfigureSlotButton(...)
```

Change equipment/accessory collections to `SiriusItemSlotController`:

```csharp
private readonly Dictionary<EquipmentSlotType, SiriusItemSlotController> _equipmentSlots = new();
private readonly List<SiriusItemSlotController> _accessorySlots = new();
```

Bind each explicit slot once in `_Ready()` and subscribe its `Activated`, `FocusEntered`, and `MouseEntered` events.

- [ ] **Step 6: Bind the character summary directly from `Character`**

Add cached nodes and one refresh method:

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

Use the actual public properties exposed by `SiriusStatBar`; if the component uses lowercase exported property names in C#, bind through those existing names rather than adding a new stat-bar API.

Call this from `RefreshUI()` before equipment/catalogue binding so post-equip stats refresh in the same render pass.

- [ ] **Step 7: Add responsive page state without duplicating content**

Add:

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

Add `ApplyResponsiveLayout()` that:

1. calculates `var (compact, margin, sideInset) = SiriusUiMetrics.SafeFrameInsets(Size);`;
2. applies safe-frame offsets using `sideInset` horizontally and `margin` vertically;
3. updates every equipment/accessory/dynamic item slot with `SetCompact(compact)`;
4. hides `%CompactTabs` in standard mode;
5. in standard mode shows `EquipmentPage`, `SkillsPage`, and `ItemsPage` together;
6. in compact mode shows only the active page's content;
7. selects the matching tab button without emitting page-change work recursively;
8. restores current focus if it remains visible, otherwise resolves the active-page fallback.

Hook it to the root/viewport resize signal already used by other responsive Sirius screens. Do not add a global resize service.

- [ ] **Step 8: Implement compact page activation**

Use three explicit button handlers:

```csharp
private void OnEquipmentTabPressed() => SetCompactPage(InventoryPage.Equipment);
private void OnItemsTabPressed() => SetCompactPage(InventoryPage.Items);
private void OnSkillsTabPressed() => SetCompactPage(InventoryPage.Skills);
```

`SetCompactPage` updates `_activeCompactPage`, reapplies visibility, and focuses the remembered/current fallback for that page.

In `_Input`, preserve `InputHintPresenter.Observe(@event)` behavior and add only this local gamepad shortcut:

```csharp
if (Visible && _isCompact && @event is InputEventJoypadButton joy && joy.Pressed)
{
    if (joy.ButtonIndex == JoyButton.LeftShoulder)
        CycleCompactPage(-1);
    else if (joy.ButtonIndex == JoyButton.RightShoulder)
        CycleCompactPage(1);
}
```

Call `GetViewport().SetInputAsHandled()` only when a shoulder press actually changes the page. Do not handle `ui_cancel` or `toggle_inventory` here; the host owns terminal close.

- [ ] **Step 9: Replace Close-first initial focus**

Implement:

```csharp
public Control? InitialFocusTarget => ResolveInitialFocusTarget();
```

`ResolveInitialFocusTarget()` must return, in order:

1. current still-valid visible focus owner when reopening/reflowing;
2. first valid remembered control for the active compact page;
3. `%WeaponSlot` or the first primary equipment slot for Equipment/standard;
4. first dynamic item slot for Items;
5. `%ActiveSkillSelector` for Skills;
6. active compact tab;
7. `%CloseButton`.

Do not persist this outside the current Inventory screen instance.

- [ ] **Step 10: Run Task 2 focused tests GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: responsive scene and existing controller behavior tests pass; catalogue-specific tests added in Task 3 may not exist yet.

- [ ] **Step 11: Commit Task 2**

```bash
git add \
  scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs \
  tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): scene-author responsive Inventory layout"
```

---

### Task 3: Replace the fixed 24-slot catalogue with dynamic parity binding

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`

**Interfaces:**
- Consumes: `%InventoryGrid`, `PackedScene` for `SiriusItemSlot.tscn`, `Character.Inventory.GetAllEntries()`, existing item/equipment/consumable methods.
- Produces:

```csharp
private readonly List<SiriusItemSlotController> _inventorySlots = new();
private PackedScene _itemSlotScene = null!;

private void RefreshInventoryCatalogue();
private void ActivateInventoryEntry(InventoryEntry entry);
private void PresentFocusSummary(string text);
```

No collection renderer or item-view model is introduced.

- [ ] **Step 1: Add a regression proving item types beyond 24 are visible**

Add to `InventoryMenuControllerTest`:

```csharp
[TestCase]
public void InventoryCatalogue_RendersEveryCurrentItemTypeBeyondLegacyTwentyFourLimit()
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

    var grid = _inventoryMenu.GetNode<Container>("%InventoryGrid");
    var slots = grid.GetChildren().OfType<SiriusItemSlotController>().ToArray();
    AssertThat(slots.Length).IsEqual(30);
    AssertThat(slots[^1].TooltipText).Contains("Item 29");
}
```

This deliberately exceeds the old 24-node presentation cap but stays far below the domain's 100 item-type limit.

- [ ] **Step 2: Add deterministic-order and quantity tests**

Seed three stackable/non-stackable entries in reverse insertion order and assert slot tooltips/quantity labels are rendered in ordinal `DisplayName` order:

```csharp
AssertThat(slots[0].TooltipText).Contains("Alpha");
AssertThat(slots[1].TooltipText).Contains("Beta");
AssertThat(slots[2].TooltipText).Contains("Zulu");
```

For a stackable consumable quantity of 3:

```csharp
AssertThat(slots[1].GetNode<Label>("%QuantityLabel").Text).IsEqual("×3");
```

Do not change sorting to category-first or locale-aware ordering in this ticket.

- [ ] **Step 3: Add action-parity tests around the new slot activation seam**

Add focused cases that activate the rendered `SiriusItemSlotController` rather than invoking private controller methods directly:

1. equipment item slot equips and disappears from inventory;
2. populated equipment slot unequips and returns the item;
3. inventory-full unequip restores the original equipment;
4. usable consumable decrements quantity and applies current effect;
5. failed consumable application restores quantity when rollback succeeds;
6. battle-only consumable is `Actionable == false`, exposes `Battle use only`, and quantity is unchanged;
7. locked accessory is `Actionable == false`, exposes `Accessory Slot Locked`, and focusable.

Reuse existing catalogue/test item builders where available. For synthetic equipment entries, construct `EquipmentItem` directly as in Step 1; do not add production fixtures.

- [ ] **Step 4: Add focus-summary accessibility coverage**

Add:

```csharp
[TestCase]
public async Task KeyboardFocus_UpdatesTheSameReadableItemSummaryAsPointerFocus()
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

If `%FocusSummary` is implemented as `Label` rather than `RichTextLabel`, keep the assertion semantics and use that exact control type consistently in scene/controller/tests.

- [ ] **Step 5: Run the new catalogue/parity tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuControllerTest"
```

Expected: legacy fixed-slot implementation fails the >24/dynamic-slot assertions.

- [ ] **Step 6: Load the slot scene once and remove the legacy fixed-array model**

In `_Ready()`:

```csharp
_itemSlotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn")
    ?? throw new InvalidOperationException("Failed to load SiriusItemSlot.tscn.");
```

Delete:

```csharp
private InventoryEntry[] _inventorySlotEntries = Array.Empty<InventoryEntry>();
```

Delete the old `InitializeInventorySlots()` assumption that `%InventoryGrid.GetChildCount()` is the inventory capacity.

- [ ] **Step 7: Implement dynamic catalogue refresh**

Use the existing sort rule and maintain exactly enough visual nodes:

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
        _inventorySlots.RemoveAt(_inventorySlots.Count - 1);
        last.QueueFree();
    }

    for (var i = 0; i < entries.Count; i++)
        BindInventorySlot(_inventorySlots[i], entries[i]);
}
```

`CreateInventorySlot()` instantiates `SiriusItemSlotController`, adds it to `%InventoryGrid`, applies current compact size, and connects focus/mouse summary callbacks once.

For activation, avoid accumulating duplicate per-refresh event handlers. Either:

- keep a dictionary `Dictionary<SiriusItemSlotController, InventoryEntry>` and one permanent `Activated` handler per slot; or
- disconnect the previous named handler before rebinding.

Prefer the dictionary because it is explicit and screen-local:

```csharp
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot = new();
```

- [ ] **Step 8: Map current item types to visual/actionable state**

`BindInventorySlot` must preserve current supported behavior:

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

    var reason = actionable
        ? string.Empty
        : entry.Item is ConsumableItem
            ? "BATTLE ONLY"
            : "UNSUPPORTED";

    slot.SetCompact(_isCompact);
    slot.Present(icon, quantity, reason, tooltip, state, actionable);
}
```

Use `UiIconPresenter` or existing item fallback behavior if a missing texture needs the same generated fallback as current runtime. Do not add asset downloading or new fallback art.

- [ ] **Step 9: Route dynamic activation back through existing operations**

```csharp
private void OnInventorySlotActivated(SiriusItemSlotController slot)
{
    if (!_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        return;

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

Keep `EquipFromInventory`, `HandleUnequip`, and `UseConsumableOutOfBattle` transaction ordering unchanged except for the UI entry point.

- [ ] **Step 10: Migrate equipment/accessory presentation to the same component**

For each primary slot, map:

```csharp
slot.Present(
    icon,
    string.Empty,
    equippedItem == null ? "EMPTY" : SlotDisplayName(slotType).ToUpperInvariant(),
    tooltip,
    equippedItem == null
        ? SiriusItemSlotVisualState.Empty
        : SiriusItemSlotVisualState.Equipped,
    actionable: equippedItem != null);
```

For inactive accessories:

```csharp
slot.Present(
    lockedIcon,
    string.Empty,
    "LOCKED",
    "Accessory Slot Locked",
    SiriusItemSlotVisualState.Locked,
    actionable: false);
```

Continue to invoke the existing `HandleUnequip` paths when an equipped slot activates.

- [ ] **Step 11: Implement one shared focus-summary writer**

Keep `BuildEquipmentTooltip` and `BuildInventoryTooltip` as the source text. Add:

```csharp
private void PresentFocusSummary(string text)
{
    _focusSummary.Text = text ?? string.Empty;
}
```

Update it on `FocusEntered` and `MouseEntered` for equipment/accessory/inventory slots and on active-skill selector focus/selection. Do not create selected-item state or make the summary control action routing.

- [ ] **Step 12: Run Task 3 focused tests GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~SiriusItemSlotControllerTest"
```

Expected: all Inventory/slot tests pass, including >24 item types.

- [ ] **Step 13: Remove obsolete logs and fixed-cap warnings**

Delete legacy development output tied to the fixed grid:

```text
InitializeInventorySlots: found ...
Inventory UI slots tracked: ...
Inventory slot N: ...
Inventory UI only displays ... hidden.
```

Keep warnings that indicate genuine domain/action failure or missing assets.

- [ ] **Step 14: Commit Task 3**

```bash
git add scripts/ui/InventoryMenuController.cs tests/ui/InventoryMenuControllerTest.cs
git commit -m "feat(ui): render the full Inventory catalogue"
```

---

### Task 4: Complete compact focus navigation and host lifecycle cutover

**Files:**
- Modify: `scripts/ui/InventoryMenuController.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs` only when required by current Inventory assertions
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md` if its Inventory row/evidence is stale

**Interfaces:**
- Consumes: existing `Game.TryOpenInventory`, `UIScreenHost`, `UIScreenKinds.Inventory`, current Pause parent handle.
- Produces: same hosting API with `Hud = UIHudPolicy.Hidden` and screen-provided content-first `InitialFocusTarget`.

- [ ] **Step 1: Update real-scene host tests to the approved HUD policy first**

In `GameplayPauseHostTest.DirectInventory_HostsDetachesAndReusesTheExternalView`, change the expected policy from inherited HUD to hidden and assert the actual `GameUI` visibility:

```csharp
var gameUi = _game!.GetNode<Control>("UI/GameUI");
AssertThat(gameUi.Visible).IsTrue();

// open Inventory
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(gameUi.Visible).IsFalse();

// close Inventory
AssertThat(gameUi.Visible).IsTrue();
```

In `PauseChildInventory_HostsLogicalPauseChildAndRestoresExistingPause`, assert:

```csharp
AssertThat(inventoryEntry.Policy.Hud).IsEqual(UIHudPolicy.Hidden);
AssertThat(gameUi.Visible).IsFalse();
```

After child Inventory closes while Pause remains open:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsTrue();
AssertThat(gameUi.Visible).IsTrue();
AssertThat(_viewport.GuiGetFocusOwner()).IsEqual(inventoryButton);
```

This proves the child's HUD-hidden lease is released back to Pause's visible-HUD policy.

- [ ] **Step 2: Add content-first initial-focus integration coverage**

After opening direct Inventory, assert the host focuses an Inventory content control rather than Close:

```csharp
var inventory = GetPrivateField<InventoryMenuController>(_game, "_inventoryMenu");
var focus = _viewport!.GuiGetFocusOwner();
AssertThat(focus).IsNotNull();
AssertThat(focus).IsNotEqual(inventory.GetNode<Button>("%CloseButton"));
AssertThat(focus).IsEqual(inventory.InitialFocusTarget);
```

At 640×360, add an Inventory scene/controller focus test that switches to Items and Skills and proves each page receives a valid visible fallback.

- [ ] **Step 3: Add shoulder-navigation coverage only in compact mode**

In `InventoryMenuControllerTest`:

```csharp
[TestCase]
public async Task CompactGamepadShoulders_CycleEquipmentItemsSkillsWithoutClosingInventory()
{
    await ResizeInventoryViewport(new Vector2I(640, 360));
    _inventoryMenu.OpenMenu();

    _inventoryMenu._Input(new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.RightShoulder,
        Pressed = true
    });
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    AssertThat(_inventoryMenu.GetNode<Button>("%ItemsTab").ButtonPressed).IsTrue();
    AssertThat(_inventoryMenu.Visible).IsTrue();

    _inventoryMenu._Input(new InputEventJoypadButton
    {
        ButtonIndex = JoyButton.RightShoulder,
        Pressed = true
    });
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    AssertThat(_inventoryMenu.GetNode<Button>("%SkillsTab").ButtonPressed).IsTrue();
    AssertThat(_inventoryMenu.Visible).IsTrue();
}
```

Add a standard-mode case proving the same shoulder input does not change page state at 1280×720.

- [ ] **Step 4: Run the host/focus tests and confirm RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~InventoryMenuSceneTest"
```

Expected: current `Game` still uses `UIHudPolicy.Inherit`; any remaining compact focus gaps fail.

- [ ] **Step 5: Make the minimal `Game.TryOpenInventory` policy change**

In `scripts/game/Game.cs`, change only:

```csharp
Hud = UIHudPolicy.Hidden,
```

Keep:

```csharp
ProcessPolicy = hasParent ? UIProcessPolicy.Always : UIProcessPolicy.WhenPaused,
Parent = hasParent ? parent : null,
PauseTree = !hasParent,
BlockGameplayInput = !hasParent,
Cursor = UICursorPolicy.Visible,
LowerLayers = UILowerLayerPolicy.VisibleInert,
Cancel = UICancelPolicy.Close,
EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
InitialFocus = () => _inventoryMenu.InitialFocusTarget,
NodeLifetime = UINodeLifetime.External
```

Do not add an Inventory-specific host wrapper or factory.

- [ ] **Step 6: Finish compact page/focus memory inside `InventoryMenuController`**

Track only live controls for the current screen instance:

```csharp
private readonly Dictionary<InventoryPage, Control> _lastFocusByPage = new();
```

On focus entry for a control belonging to a compact page, update that page's value. Before returning a remembered control, verify:

```csharp
GodotObject.IsInstanceValid(control) &&
control.IsVisibleInTree() &&
control.FocusMode != Control.FocusModeEnum.None
```

When dynamic inventory refresh frees a remembered item slot, remove/replace that remembered entry during the same refresh. Do not retain item IDs beyond what is needed to restore immediate focus after refresh.

- [ ] **Step 7: Verify host-owned Cancel and toggle remain unchanged**

Keep existing `InventoryMenuControllerTest` cases proving direct calls to `_Input(ui_cancel)` and `_Input(toggle_inventory)` do not self-close or change pause state.

Keep/extend `GameplayPauseHostTest` to prove real input closes the topmost Inventory entry through the host and restores:

- direct gameplay tree pause;
- Pause parent focus;
- gameplay HUD policy;
- external Inventory node parentage;
- stale-handle-free reopening.

If `GameInputLifecycleTest.cs` already asserts old Inventory paths/focus, migrate those exact assertions in the same commit. Do not add a second redundant lifecycle matrix.

- [ ] **Step 8: Reconcile the lifecycle contract document**

Search:

```bash
rg -n "Inventory|InventoryMenu|HUD" docs/ui/hpa-376/ui-lifecycle-contract.md
```

If the Inventory row still describes inherited gameplay HUD or Close-first focus, update it to:

- HUD hidden while Inventory is active;
- world paused through host policy;
- content-first/last-valid focus;
- host-owned Cancel/toggle;
- parent/gameplay restoration.

Keep source/test evidence paths current. Do not rewrite unrelated lifecycle rows.

- [ ] **Step 9: Run the complete HPA-357 focused suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 10: Run the full solution tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failures. Existing repository warning noise may remain, but HPA-357 must introduce no new test failure.

- [ ] **Step 11: Build the solution**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: 0 errors.

- [ ] **Step 12: Audit stale Inventory presentation patterns**

Run:

```bash
rg -n \
  "EquipmentPanelSize|EquipmentButtonSize|AccessoryPanelSize|AccessoryButtonSize|InventoryPanelSize|InventoryButtonSize|CacheStyles\(|_basePanelStyle|_equippedPanelStyle|_lockedPanelStyle|Inventory UI only displays|InitializeInventorySlots:|Inventory UI slots tracked" \
  scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn tests/ui
```

Expected: zero active-source matches.

Run:

```bash
rg -n "StyleBoxFlat" scenes/ui/InventoryMenu.tscn
```

Expected: zero local Inventory-screen `StyleBoxFlat` resources. Slot styling belongs to `SiriusTheme.tres`.

Run:

```bash
rg -n "GetTree\(\)\.Paused|SceneTree\.Paused" scripts/ui/InventoryMenuController.cs
```

Expected: zero matches.

- [ ] **Step 13: Audit scope against HPA-375/non-goals**

Run:

```bash
rg -n -i \
  "favorite|favourite|compare|comparison|filter|sort mode|drop item|sell item|bulk|selected item model|inventory viewmodel|inventory presenter|collection renderer" \
  scripts/ui/InventoryMenuController.cs scripts/ui/components/SiriusItemSlotController.cs scenes/ui/InventoryMenu.tscn
```

Expected: zero newly introduced feature/framework matches except comments explicitly naming an out-of-scope concept, which should be removed if they add no implementation value.

- [ ] **Step 14: Check formatting and branch scope**

```bash
git diff --check
git status --short
git diff --name-only main...HEAD
```

Expected changed production scope is limited to the HPA-357 file map plus the reviewed design/plan documents and any directly required lifecycle-test/document migration.

- [ ] **Step 15: Commit Task 4**

```bash
git add \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  scripts/game/Game.cs \
  tests/game/GameplayPauseHostTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "feat(ui): complete hosted Inventory parity migration"
```

If `GameInputLifecycleTest.cs` or the lifecycle document did not require a change, omit that untouched path from `git add` rather than creating a cosmetic edit.

---

## Final self-review checklist

Before publishing the implementation PR, re-read `docs/superpowers/specs/2026-08-12-hpa-357-inventory-parity-design.md` and confirm:

- [ ] Every acceptance requirement has a production owner and focused test.
- [ ] The screen renders more than 24 item types without changing `Inventory.MaxItemTypes`.
- [ ] All existing equip/unequip/consume/rollback/active-skill operations still call the same domain methods.
- [ ] Standard and compact modes use the same content nodes.
- [ ] Unavailable slots remain keyboard/gamepad focusable but cannot mutate domain state.
- [ ] Focus summary follows current focus/hover only; no persistent selected-item model exists.
- [ ] Inventory hides the gameplay HUD and lets `UIScreenHost` own pause, cursor, Cancel, toggle, and restoration.
- [ ] No domain/save-format file changed without an explicit design re-review.
- [ ] No generic view model, presenter, renderer, navigation service, or inventory facade was added.
- [ ] `SiriusItemSlot` contains no inventory/equipment domain knowledge.
- [ ] `SiriusUiMetrics` gained only the proven 56/48 slot metric.
- [ ] The plan contains no `TODO`, `TBD`, placeholder implementation step, or unnamed error-handling requirement.
- [ ] Focused tests, full tests, build, `git diff --check`, stale-pattern search, and scope audit are green.

## Expected implementation shape

The finished HPA-357 implementation should remain one vertical slice: one redesigned Inventory scene, one controller migration, one reusable slot leaf, one small Theme extension, one HUD-policy correction in `Game`, and focused tests. If implementation begins to require a new domain service or a generic screen/collection architecture, stop and revisit the design rather than expanding the ticket silently.
