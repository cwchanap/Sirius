# HPA-357 Inventory and Equipment Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed Sirius Inventory workbench with one responsive, host-managed character/equipment/items/skills screen while preserving current domain behavior and the tested HPA-374 art contract.

**Architecture:** Keep `InventoryMenuController` as the one feature controller and `Game` / `UIScreenHost` as lifecycle owners. Add one `SiriusItemSlot` UI leaf, one tiny shared player-summary presenter now that HUD and Inventory are two concrete consumers, and TextureRect glyph/item helpers on the existing `UiIconPresenter`. Perform the Inventory scene rewrite, old-test migration, dynamic catalogue conversion, accessory-index routing, and legacy TextureButton presenter deletion atomically.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing Sirius Theme/UI components.

## Global Constraints

- Preserve current primary equip, unequip, capacity rollback, consumable rollback, quantity, and explicit no-active-skill behavior.
- Make the four existing `EquipmentSet.AccessorySlotCount` indices reachable using the existing indexed `Character.TryEquip` overload; do not add an unlock/progression system.
- Accessory equip chooses the first empty index and falls back to index 0 only when all four are occupied.
- Preserve ordinal `DisplayName` inventory ordering.
- Minimum supported resolution: 640×360.
- Compact rule: `SiriusUiMetrics.IsCompact` (`width < 800 || height < 450`).
- Safe margins: 24 px standard / 12 px compact; maximum content width: 1600 px.
- Slot size: 56×56 standard / 48×48 compact.
- Minimum target: 44×44 standard / 40×40 compact.
- Essential compact text remains at least 14 px.
- Reuse `SiriusTheme.tres`, current art, hero crop `Rect2(0, 0, 96, 96)`, `SiriusStatBar`, `InputHintPresenter`, and gameplay `UIScreenHost`.
- Inventory host policy changes only from `UIHudPolicy.Inherit` to `UIHudPolicy.Hidden`.
- `InventoryMenuController` never owns `SceneTree.Paused`, terminal Cancel, or terminal `toggle_inventory`.
- Generated slot glyphs remain 32 px native/centered; item art remains aspect-preserving scaled.
- Shared player-summary rules remain: blank name → `Adventurer`; hide MP when `MaxMana <= 0`; hide EXP when `ExperienceToNext <= 0`; clamp visible EXP.
- Gold copy remains exactly `Gold: {value}`.
- Focus restoration uses equipment slot type / accessory index / item ID, never a dynamic `Control` as semantic identity.
- Focus summary is plain text, passive, and ephemeral.
- Start with Godot spatial focus navigation. Add explicit `FocusNeighbor*` only when an actual input test fails at a named boundary.
- No persistent selection, comparison, filters, user sorting, Drop, Sell, Favourite, Lock action, bulk actions, accessory unlock rules, new InputMap page actions, inventory persistence changes, battle-item redesign, view model, presenter layer, collection renderer, navigation service, facade, or compatibility layer.
- Do not modify `Character`, `Inventory`, `EquipmentSet`, save-format, or skill-domain production code.

---

## File Map

### Create

- `scripts/ui/components/SiriusItemSlotController.cs`
- `scenes/ui/components/SiriusItemSlot.tscn`
- `scripts/ui/SiriusPlayerSummaryPresenter.cs`
- `tests/ui/components/SiriusItemSlotControllerTest.cs`
- `tests/ui/InventoryMenuSceneTest.cs`

### Modify

- `scripts/ui/art/UiIconPresenter.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusThemeTypes.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `tests/ui/theme/SiriusUiMetricsTest.cs`
- `tests/ui/theme/SiriusUiContractsTest.cs`
- `scripts/ui/ExplorationHudController.cs`
- `tests/ui/ExplorationHudControllerTest.cs`
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

## Task 1: Add the reusable slot and shared presentation seams

**Files:**
- Create: `scripts/ui/components/SiriusItemSlotController.cs`
- Create: `scenes/ui/components/SiriusItemSlot.tscn`
- Create: `scripts/ui/SiriusPlayerSummaryPresenter.cs`
- Create: `tests/ui/components/SiriusItemSlotControllerTest.cs`
- Modify: `scripts/ui/art/UiIconPresenter.cs`
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Modify: `scripts/ui/theme/SiriusThemeTypes.cs`
- Modify: `scripts/ui/theme/SiriusUiMetrics.cs`
- Modify: `tests/ui/theme/SiriusUiMetricsTest.cs`
- Modify: `tests/ui/theme/SiriusUiContractsTest.cs`
- Modify: `scripts/ui/ExplorationHudController.cs`
- Modify: `tests/ui/ExplorationHudControllerTest.cs`

**Produces:**

```csharp
public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Unsupported
}

public partial class SiriusItemSlotController : Button
{
    [Signal] public delegate void ActivatedEventHandler();

    public bool Actionable { get; }

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
}

public static class SiriusPlayerSummaryPresenter
{
    public static void Apply(
        ExplorationHudPlayerState state,
        Label nameLabel,
        Label levelLabel,
        SiriusStatBar healthBar,
        SiriusStatBar manaBar,
        ProgressBar experienceBar);
}
```

`SiriusItemSlotController` has no domain dependency. `SiriusPlayerSummaryPresenter` applies presentation only; it does not read `GameManager` or mutate `Character`.

### Task 1A — freeze closed contracts first

- [ ] **Step 1: Add slot metric and closed-contract tests**

Add to `tests/ui/theme/SiriusUiMetricsTest.cs`:

```csharp
[TestCase]
public void ItemSlotSize_UsesApprovedGeometry()
{
    AssertThat(SiriusUiMetrics.ItemSlotSize(false)).IsEqual(new Vector2(56, 56));
    AssertThat(SiriusUiMetrics.ItemSlotSize(true)).IsEqual(new Vector2(48, 48));
}
```

Add to `tests/ui/theme/SiriusUiContractsTest.cs`:

```csharp
[TestCase]
public void ItemSlotVisualState_ContainsOnlyApprovedValues()
{
    AssertThat(string.Join(",", Enum.GetNames<SiriusItemSlotVisualState>()))
        .IsEqual("Empty,Available,Equipped,Unsupported");
}

[TestCase]
public void ItemSlotThemeTypes_ExposeExactStableNames()
{
    AssertThat(SiriusThemeTypes.ItemSlotButton.ToString())
        .IsEqual("SiriusItemSlotButton");
    AssertThat(SiriusThemeTypes.ItemSlotEquippedButton.ToString())
        .IsEqual("SiriusItemSlotEquippedButton");
    AssertThat(SiriusThemeTypes.ItemSlotUnavailableButton.ToString())
        .IsEqual("SiriusItemSlotUnavailableButton");
}
```

- [ ] **Step 2: Run contract RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiMetricsTest.ItemSlotSize_UsesApprovedGeometry|FullyQualifiedName~SiriusUiContractsTest.ItemSlot"
```

Expected: compile/test failure because the new enum/metric/Theme names do not exist.

- [ ] **Step 3: Add exactly one shared slot metric and three Theme names**

Add to `SiriusUiMetrics.cs`:

```csharp
public static Vector2 ItemSlotSize(bool compact) =>
    compact ? new Vector2(48, 48) : new Vector2(56, 56);
```

Add to `SiriusThemeTypes.cs`:

```csharp
public static readonly StringName ItemSlotButton = "SiriusItemSlotButton";
public static readonly StringName ItemSlotEquippedButton = "SiriusItemSlotEquippedButton";
public static readonly StringName ItemSlotUnavailableButton = "SiriusItemSlotUnavailableButton";
```

In `SiriusTheme.tres`, add exactly these three Button variations using existing palette values:

```text
SiriusItemSlotButton
  4px radius; indigo/muted normal; cyan hover/focus

SiriusItemSlotEquippedButton
  same geometry; gold equipped border; cyan focus remains independent

SiriusItemSlotUnavailableButton
  same geometry; muted ~45% treatment; root still focusable / native disabled=false
```

Do not add new palette tokens.

### Task 1B — move glyph/item distinction onto TextureRect

- [ ] **Step 4: Add the two narrow TextureRect presenter APIs**

Add to `scripts/ui/art/UiIconPresenter.cs` without deleting the current TextureButton APIs yet:

```csharp
public static bool ApplyGlyph(TextureRect target, UiIconId id, UiIconSize size)
{
    var texture = UiArtCatalog.LoadIcon(id, size);
    target.Texture = texture;
    target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
    target.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
    return texture != null;
}

public static void ApplyItem(TextureRect target, Texture2D? texture)
{
    target.Texture = texture;
    target.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
    target.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
}
```

Keep `Apply(TextureButton, ...)`, `ApplyTexture(TextureButton, ...)`, `ApplyGlyphTexture(TextureButton, ...)`, and `SetSlotTextures(...)` temporarily because the production Inventory controller still uses them until Task 2.

### Task 1C — add component tests and implementation

- [ ] **Step 5: Write slot component tests**

Create `tests/ui/components/SiriusItemSlotControllerTest.cs` with a runtime fixture for `res://scenes/ui/components/SiriusItemSlot.tscn` and these cases:

```csharp
[TestCase]
public void PresentGlyph_UsesNativeCenteredFeatureGlyph()
{
    _slot!.PresentGlyph(
        UiIconId.Weapon, "", "", "Weapon\nEmpty",
        SiriusItemSlotVisualState.Empty);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.GetSize()).IsEqual(new Vector2(32, 32));
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepCentered);
    AssertThat(_slot.Actionable).IsFalse();
    AssertThat(_slot.Icon).IsNull();
}

[TestCase]
public void PresentItem_UsesAspectCenteredItemArt()
{
    var sword = EquipmentCatalog.CreateWoodenSword();
    _slot!.PresentItem(
        sword.LoadAssetOrDefault<Texture2D>(), "", "", sword.DisplayName,
        SiriusItemSlotVisualState.Equipped);

    var icon = _slot.GetNode<TextureRect>("%Icon");
    AssertThat(icon.Texture!.ResourcePath).IsEqual(sword.AssetPath);
    AssertThat(icon.StretchMode).IsEqual(TextureRect.StretchModeEnum.KeepAspectCentered);
    AssertThat(_slot.Actionable).IsTrue();
}

[TestCase]
public void Actionable_IsDerivedOnlyFromVisualState()
{
    _slot!.PresentGlyph(UiIconId.Weapon, "", "", "Empty", SiriusItemSlotVisualState.Empty);
    AssertThat(_slot.Actionable).IsFalse();

    _slot.PresentGlyph(UiIconId.General, "", "", "Available", SiriusItemSlotVisualState.Available);
    AssertThat(_slot.Actionable).IsTrue();

    _slot.PresentItem(null, "", "", "Equipped", SiriusItemSlotVisualState.Equipped);
    AssertThat(_slot.Actionable).IsTrue();

    _slot.PresentGlyph(UiIconId.General, "", "UNAVAILABLE", "Unsupported", SiriusItemSlotVisualState.Unsupported);
    AssertThat(_slot.Actionable).IsFalse();
}

[TestCase]
public void EmptyAndUnsupported_RemainFocusableButDoNotActivate()
{
    var activations = 0;
    void OnActivated() => activations++;
    _slot!.Activated += OnActivated;

    foreach (var state in new[]
             {
                 SiriusItemSlotVisualState.Empty,
                 SiriusItemSlotVisualState.Unsupported
             })
    {
        _slot.PresentGlyph(UiIconId.General, "", "UNAVAILABLE", "Reason", state);
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
    _slot!.PresentGlyph(
        UiIconId.General, "×9", "UNAVAILABLE", "Unsupported",
        SiriusItemSlotVisualState.Unsupported);
    _slot.PresentGlyph(
        UiIconId.Weapon, "", "", "Empty",
        SiriusItemSlotVisualState.Empty);

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
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: missing component/enum/scene failures.

- [ ] **Step 7: Implement `SiriusItemSlotController`**

```csharp
using Godot;

public enum SiriusItemSlotVisualState
{
    Empty,
    Available,
    Equipped,
    Unsupported
}

public partial class SiriusItemSlotController : Button
{
    [Signal] public delegate void ActivatedEventHandler();

    private SiriusItemSlotVisualState _state;
    private TextureRect _icon = null!;
    private Label _quantityLabel = null!;
    private Label _stateLabel = null!;

    public bool Actionable =>
        _state is SiriusItemSlotVisualState.Available
            or SiriusItemSlotVisualState.Equipped;

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
        SiriusItemSlotVisualState state)
    {
        UiIconPresenter.ApplyGlyph(_icon, iconId, UiIconSize.Feature);
        PresentCore(quantityText, stateText, tooltipText, state);
    }

    public void PresentItem(
        Texture2D? texture,
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state)
    {
        UiIconPresenter.ApplyItem(_icon, texture);
        PresentCore(quantityText, stateText, tooltipText, state);
    }

    private void PresentCore(
        string quantityText,
        string stateText,
        string tooltipText,
        SiriusItemSlotVisualState state)
    {
        _state = state;
        TooltipText = tooltipText ?? string.Empty;
        _quantityLabel.Text = quantityText ?? string.Empty;
        _quantityLabel.Visible = !string.IsNullOrWhiteSpace(_quantityLabel.Text);
        _stateLabel.Text = stateText ?? string.Empty;
        _stateLabel.Visible = !string.IsNullOrWhiteSpace(_stateLabel.Text);
        ThemeTypeVariation = state switch
        {
            SiriusItemSlotVisualState.Equipped => SiriusThemeTypes.ItemSlotEquippedButton,
            SiriusItemSlotVisualState.Empty or SiriusItemSlotVisualState.Unsupported
                => SiriusThemeTypes.ItemSlotUnavailableButton,
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
├── Icon (TextureRect, unique=%Icon, mouse_filter=Ignore)
├── QuantityLabel (Label, unique=%QuantityLabel, bottom-right, mouse_filter=Ignore)
└── StateLabel (Label, unique=%StateLabel, bottom, mouse_filter=Ignore)

root:
  custom_minimum_size = Vector2(56, 56)
  focus_mode = All
  theme_type_variation = SiriusItemSlotButton

Icon:
  expand_mode = IgnoreSize
  stretch_mode = KeepAspectCentered
```

No domain-specific copy belongs in the reusable scene.

### Task 1D — share HUD/Inventory fallback policy

- [ ] **Step 9: Add the static player-summary presenter**

Create `scripts/ui/SiriusPlayerSummaryPresenter.cs`:

```csharp
using Godot;
using System;

public static class SiriusPlayerSummaryPresenter
{
    public static void Apply(
        ExplorationHudPlayerState state,
        Label nameLabel,
        Label levelLabel,
        SiriusStatBar healthBar,
        SiriusStatBar manaBar,
        ProgressBar experienceBar)
    {
        nameLabel.Text = string.IsNullOrWhiteSpace(state.Name)
            ? "Adventurer"
            : state.Name;
        levelLabel.Text = $"Lv {state.Level}";

        healthBar.Current = state.CurrentHealth;
        healthBar.Maximum = state.MaxHealth;

        manaBar.Visible = state.MaxMana > 0;
        if (manaBar.Visible)
        {
            manaBar.Current = state.CurrentMana;
            manaBar.Maximum = state.MaxMana;
        }

        experienceBar.Visible = state.ExperienceToNext > 0;
        if (experienceBar.Visible)
        {
            experienceBar.MaxValue = state.ExperienceToNext;
            experienceBar.Value = Math.Clamp(
                state.Experience,
                0,
                state.ExperienceToNext);
        }
    }
}
```

- [ ] **Step 10: Move Exploration HUD common binding onto the presenter**

Replace the duplicated name/level/HP/MP/EXP body of `ExplorationHudController.ApplyPlayerState` with:

```csharp
SiriusPlayerSummaryPresenter.Apply(
    state,
    _playerName,
    _playerLevel,
    _healthBar,
    _manaBar,
    _experienceBar);

_portrait.Visible = _portrait.Texture != null;
```

Do not change the `ExplorationHudPlayerState` shape or Game's current state construction in this task.

- [ ] **Step 11: Run HUD fallback regressions plus Task 1 suite GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures. Existing HUD tests remain the regression gate for Adventurer, MP visibility, EXP visibility/clamp, and portrait behavior.

- [ ] **Step 12: Commit Task 1**

```bash
git add \
  scripts/ui/components/SiriusItemSlotController.cs \
  scenes/ui/components/SiriusItemSlot.tscn \
  scripts/ui/SiriusPlayerSummaryPresenter.cs \
  tests/ui/components/SiriusItemSlotControllerTest.cs \
  scripts/ui/art/UiIconPresenter.cs \
  resources/ui/theme/SiriusTheme.tres \
  scripts/ui/theme/SiriusThemeTypes.cs \
  scripts/ui/theme/SiriusUiMetrics.cs \
  tests/ui/theme/SiriusUiMetricsTest.cs \
  tests/ui/theme/SiriusUiContractsTest.cs \
  scripts/ui/ExplorationHudController.cs \
  tests/ui/ExplorationHudControllerTest.cs
git commit -m "feat(ui): add shared Inventory presentation seams"
```

---

## Task 2: Atomically cut Inventory over to the responsive dynamic screen

**Files:**
- Modify: `scenes/ui/InventoryMenu.tscn`
- Modify: `scripts/ui/InventoryMenuController.cs`
- Create: `tests/ui/InventoryMenuSceneTest.cs`
- Modify: `tests/ui/InventoryMenuControllerTest.cs`
- Modify: `tests/ui/art/Hpa374RuntimeSmokeTest.cs`
- Modify: `scripts/ui/art/UiIconPresenter.cs` — delete legacy TextureButton slot APIs after production cutover.

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

`%FocusSummary` is a plain `Label` using `AutowrapMode.WordSmart` inside a bounded layout region.

### Task 2A — build a complete scene-test fixture before assertions

- [ ] **Step 1: Create the complete `InventoryMenuSceneTest` fixture**

Create `tests/ui/InventoryMenuSceneTest.cs` with these fields/helpers before adding test cases:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class InventoryMenuSceneTest : Node
{
    private SceneTree _sceneTree = null!;
    private GameManager _gameManager = null!;
    private SubViewportContainer _viewportContainer = null!;
    private SubViewport _viewport = null!;
    private InventoryMenuController _menu = null!;

    [BeforeTest]
    public async Task SetUp()
    {
        TestHelpers.ResetGameManagerSingleton();
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _sceneTree.Paused = false;

        _gameManager = new GameManager { AutoSaveEnabled = false };
        _sceneTree.Root.AddChild(_gameManager);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        _viewportContainer = new SubViewportContainer
        {
            Size = new Vector2(1280, 720),
            Stretch = true
        };
        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(1280, 720),
            GuiEmbedSubwindows = true
        };
        _viewportContainer.AddChild(_viewport);
        _sceneTree.Root.AddChild(_viewportContainer);

        var packed = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn")
            ?? throw new InvalidOperationException("Failed to load InventoryMenu.tscn.");
        _menu = packed.Instantiate<InventoryMenuController>();
        _viewport.AddChild(_menu);
        await AwaitFrames(2);
    }

    [AfterTest]
    public async Task TearDown()
    {
        _sceneTree.Paused = false;
        if (GodotObject.IsInstanceValid(_menu)) _menu.Free();
        if (GodotObject.IsInstanceValid(_viewportContainer)) _viewportContainer.Free();
        if (GodotObject.IsInstanceValid(_gameManager)) _gameManager.Free();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        TestHelpers.ResetGameManagerSingleton();
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private async Task Resize(Vector2I size)
    {
        _viewport.Size = size;
        _viewportContainer.Size = new Vector2(size.X, size.Y);
        await AwaitFrames(2);
    }

    private int VisiblePageCount() =>
        new[] { "%EquipmentPage", "%ItemsPage", "%SkillsPage" }
            .Count(path => _menu.GetNode<Control>(path).Visible);

    private void PushAction(StringName action)
    {
        _viewport.PushInput(new InputEventAction
        {
            Action = action,
            Pressed = true
        });
    }
}
```

This fixture owns `AwaitFrames`, `Resize`, and `VisiblePageCount`; later steps do not assume hidden helper code.

### Task 2B — write the scene/parity tests against the target shape

- [ ] **Step 2: Write responsive scene tests**

```csharp
[TestCase]
public async Task FitsEveryVerificationViewport()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        await Resize(size);
        _menu.OpenMenu();
        await AwaitFrames(2);

        var safe = _menu.GetNode<Control>("%SafeFrame");
        AssertThat(new Rect2(Vector2.Zero, size).Encloses(safe.GetGlobalRect())).IsTrue();
        AssertThat(safe.Size.X).IsGreater(0f);
        AssertThat(safe.Size.Y).IsGreater(0f);
    }
}

[TestCase]
public async Task Standard_ShowsAllThreeContentAreasTogether()
{
    await Resize(new Vector2I(1280, 720));
    _menu.OpenMenu();
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsFalse();
    AssertThat(_menu.GetNode<Control>("%EquipmentPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%SkillsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Control>("%ItemsPage").Visible).IsTrue();
    AssertThat(_menu.GetNode<Label>("%EquipmentTitleLabel").Text).IsEqual("Equipment");
    AssertThat(_menu.GetNode<Label>("%InventoryTitleLabel").Text).IsEqual("Items");
    AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
        .IsEqual(new Vector2(56, 56));
}

[TestCase]
public async Task Compact_ShowsOnePageAndApprovedSlotSize()
{
    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);

    AssertThat(_menu.GetNode<Control>("%CompactTabs").Visible).IsTrue();
    AssertThat(VisiblePageCount()).IsEqual(1);
    AssertThat(_menu.GetNode<Control>("%IdentityStrip").Visible).IsTrue();
    AssertThat(_menu.GetNode<Button>("%CloseButton").Visible).IsTrue();
    AssertThat(_menu.GetNode<SiriusItemSlotController>("%WeaponSlot").CustomMinimumSize)
        .IsEqual(new Vector2(48, 48));
}

[TestCase]
public void AuthorsExactlyDomainAccessorySlotCount()
{
    var slots = _menu.GetNode<Container>("%AccessorySlots")
        .GetChildren().OfType<SiriusItemSlotController>().ToArray();

    AssertThat(slots.Length).IsEqual(EquipmentSet.AccessorySlotCount);
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot4")).IsNull();
    AssertThat(_menu.GetNodeOrNull<SiriusItemSlotController>("%AccessorySlot5")).IsNull();
}
```

- [ ] **Step 3: Write behavioral compact focus tests — do not inspect `FocusNeighbor*`**

```csharp
[TestCase]
public async Task Compact_EquipmentTabDownAndFirstControlUpUseSpatialNavigation()
{
    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);

    var tab = _menu.GetNode<Button>("%EquipmentTab");
    var weapon = _menu.GetNode<SiriusItemSlotController>("%WeaponSlot");

    tab.GrabFocus();
    PushAction("ui_down");
    await AwaitFrames(2);
    AssertThat(weapon.HasFocus()).IsTrue();

    PushAction("ui_up");
    await AwaitFrames(2);
    AssertThat(tab.HasFocus()).IsTrue();
}

[TestCase]
public async Task Compact_LastEquipmentControlDownReachesClose()
{
    await Resize(new Vector2I(640, 360));
    _menu.OpenMenu();
    await AwaitFrames(2);

    var lastAccessory = _menu.GetNode<SiriusItemSlotController>("%AccessorySlot3");
    var close = _menu.GetNode<Button>("%CloseButton");

    lastAccessory.GrabFocus();
    PushAction("ui_down");
    await AwaitFrames(2);

    AssertThat(close.HasFocus()).IsTrue();
}
```

These tests define the required behavior. Do not add explicit neighbours merely because the old plan mentioned them.

- [ ] **Step 4: Write paused-process shoulder test through real viewport input**

```csharp
[TestCase]
public async Task CompactShoulders_CyclePagesWhenProcessModeIsWhenPaused()
{
    await Resize(new Vector2I(640, 360));
    _menu.ProcessMode = Node.ProcessModeEnum.WhenPaused;
    _menu.OpenMenu();
    _sceneTree.Paused = true;

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
        _sceneTree.Paused = false;
    }
}
```

No new InputMap actions are added.

- [ ] **Step 5: Add Inventory fallback + dynamic catalogue tests**

In `InventoryMenuControllerTest.cs`, add:

```csharp
private SiriusItemSlotController FindInventorySlotByTooltip(string text) =>
    _inventoryMenu.GetNode<Container>("%InventoryGrid")
        .GetChildren()
        .OfType<SiriusItemSlotController>()
        .Single(slot => slot.TooltipText.Contains(text, StringComparison.Ordinal));

[TestCase]
public void OpenMenu_UsesSharedFallbacksAndExactGoldCopy()
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

- [ ] **Step 6: Add the accessory-reachability regression**

```csharp
private static EquipmentItem CreateAccessory(string id, string name) => new()
{
    Id = id,
    DisplayName = name,
    SlotType = EquipmentSlotType.Accessory,
    AssetPath = "res://assets/sprites/items/consumables/warding_charm.png"
};

[TestCase]
public async Task AccessoryEquip_FillsFirstEmptySlotAndFocusesIt()
{
    var first = CreateAccessory("accessory_first", "Accessory First");
    var second = CreateAccessory("accessory_second", "Accessory Second");
    AssertThat(_gameManager.Player.TryAddItem(first, 1, out _)).IsTrue();
    AssertThat(_gameManager.Player.TryAddItem(second, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();

    FindInventorySlotByTooltip("Accessory First")
        .EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
        .IsEqual(first);

    FindInventorySlotByTooltip("Accessory Second")
        .EmitSignal(Button.SignalName.Pressed);
    await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
        .IsEqual(first);
    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 1))
        .IsEqual(second);
    AssertThat(_inventoryMenu.GetViewport().GuiGetFocusOwner())
        .IsEqual(_inventoryMenu.GetNode<SiriusItemSlotController>("%AccessorySlot1"));
}

[TestCase]
public void AccessoryEquip_WhenAllSlotsAreFullFallsBackToExistingSlotZeroReplacement()
{
    var originals = new EquipmentItem[EquipmentSet.AccessorySlotCount];
    for (var i = 0; i < originals.Length; i++)
    {
        originals[i] = CreateAccessory($"accessory_original_{i}", $"Original {i}");
        AssertThat(_gameManager.Player.TryEquip(originals[i], out _, i)).IsTrue();
    }

    var replacement = CreateAccessory("accessory_replacement", "Replacement");
    AssertThat(_gameManager.Player.TryAddItem(replacement, 1, out _)).IsTrue();
    _inventoryMenu.OpenMenu();
    FindInventorySlotByTooltip("Replacement")
        .EmitSignal(Button.SignalName.Pressed);

    AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, 0))
        .IsEqual(replacement);
    for (var i = 1; i < originals.Length; i++)
        AssertThat(_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, i))
            .IsEqual(originals[i]);
}
```

This uses only the indexed equip seam already present in the domain.

- [ ] **Step 7: Add catalogue-mutation focus tests**

```csharp
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
    AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
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
    AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
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
    AssertThat(_inventoryMenu.GetNode<Label>("%FocusSummary").Text)
        .Contains(sword.DisplayName);
}
```

Keep the existing equip/unequip/capacity rollback, consumable rollback, active skill, Close hint, and alphabetical-order tests; rewrite their node access to the new slot shape rather than deleting parity coverage.

- [ ] **Step 8: Rewrite HPA-374 Inventory smoke expectations**

Use:

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

After equipping the wooden sword, assert its resource path and `KeepAspectCentered`.

- [ ] **Step 9: Run target-shape RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest"
```

Expected: target scene/dynamic/accessory/focus behavior is absent.

### Task 2C — implement the atomic screen cutover

- [ ] **Step 10: Rewrite `InventoryMenu.tscn`**

Remove fixed panel dimensions, local `StyleBoxFlat`s, the `HSplitContainer`, 24 authored inventory slots, and fake `%AccessorySlot4`/`%AccessorySlot5`.

Author:

```text
InventoryMenu (Theme = SiriusTheme.tres)
├── Scrim
└── SafeFrame
    └── ScreenSurface
        └── Content
            ├── IdentityStrip
            │   ├── Portrait (same 96×96 AtlasTexture region)
            │   ├── PlayerName / PlayerLevel
            │   ├── HealthBar / ManaBar / ExperienceBar
            │   ├── AttackValue / DefenseValue / SpeedValue
            │   └── GoldLabel
            ├── CompactTabs (one ButtonGroup; Equipment initially pressed)
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
            ├── FocusSummary (Label, autowrap=WordSmart)
            └── Footer/CloseButton
```

Do not use `SiriusModalShell` or a `TabContainer` content host.

- [ ] **Step 11: Remove runtime fixed-style/fixed-capacity ownership**

Delete from `InventoryMenuController`:

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

// Refresh-scoped only. Inventory owns these mutable entries; never retain one
// from this map across a mutation / RefreshInventoryCatalogue call.
private readonly Dictionary<SiriusItemSlotController, InventoryEntry> _inventoryEntryBySlot = new();
private readonly Dictionary<string, SiriusItemSlotController> _inventorySlotByItemId = new(StringComparer.Ordinal);

private PackedScene _itemSlotScene = null!;
private Label _focusSummary = null!;
private InventoryPage _activeCompactPage = InventoryPage.Equipment;
private bool _isCompact;
private PendingFocusRestore? _pendingFocusRestore;
```

Bind accessory nodes with exactly:

```csharp
for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
    _accessorySlots.Add(GetNode<SiriusItemSlotController>($"%AccessorySlot{index}"));
```

- [ ] **Step 12: Reuse the shared player-summary presenter in Inventory**

Build the existing payload with named arguments:

```csharp
var state = new ExplorationHudPlayerState(
    Name: player.Name,
    Level: player.Level,
    CurrentHealth: player.CurrentHealth,
    MaxHealth: player.GetEffectiveMaxHealth(),
    CurrentMana: player.CurrentMana,
    MaxMana: player.MaxMana,
    Experience: player.Experience,
    ExperienceToNext: player.ExperienceToNext);

SiriusPlayerSummaryPresenter.Apply(
    state,
    _playerName,
    _playerLevel,
    _healthBar,
    _manaBar,
    _experienceBar);

_attackValue.Text = player.GetEffectiveAttack().ToString();
_defenseValue.Text = player.GetEffectiveDefense().ToString();
_speedValue.Text = player.GetEffectiveSpeed().ToString();
_goldLabel.Text = $"Gold: {player.Gold}";
```

Do not re-implement Adventurer / MP / EXP fallback logic in Inventory.

- [ ] **Step 13: Bind fixed equipment/accessories through the slot leaf**

Empty primary:

```csharp
slot.PresentGlyph(
    UiArtCatalog.ForEquipmentSlot(slotType),
    "", "", $"{SlotDisplayName(slotType)}\nEmpty",
    SiriusItemSlotVisualState.Empty);
```

Empty accessory:

```csharp
slot.PresentGlyph(
    UiIconId.Accessory,
    "", "", $"Accessory Slot {index + 1}\nEmpty",
    SiriusItemSlotVisualState.Empty);
```

Populated:

```csharp
slot.PresentItem(
    item.LoadAssetOrDefault<Texture2D>(),
    "", "", BuildEquipmentTooltip(item),
    SiriusItemSlotVisualState.Equipped);
```

- [ ] **Step 14: Make accessory equip target the first empty existing index**

Add:

```csharp
private int ResolveAccessoryEquipIndex()
{
    for (var index = 0; index < EquipmentSet.AccessorySlotCount; index++)
    {
        if (_gameManager.Player.Equipment.GetEquipped(EquipmentSlotType.Accessory, index) == null)
            return index;
    }

    return 0;
}
```

In `EquipFromInventory`, choose the index before `TryEquip`:

```csharp
var accessoryIndex = item.SlotType == EquipmentSlotType.Accessory
    ? ResolveAccessoryEquipIndex()
    : 0;

_pendingFocusRestore = _pendingFocusRestore?.WithPreferred(
    item.SlotType == EquipmentSlotType.Accessory
        ? InventoryFocusKey.ForAccessory(accessoryIndex)
        : InventoryFocusKey.ForEquipment(item.SlotType));

if (!_gameManager.Player.TryEquip(item, out var replacedItem, accessoryIndex))
    return;
```

Keep the existing replaced-item return, inventory removal ordering, warnings, and refresh semantics unchanged.

Define the focus types explicitly:

```csharp
private readonly record struct InventoryFocusKey(
    EquipmentSlotType? EquipmentSlot,
    int? AccessoryIndex,
    string? ItemId)
{
    public static InventoryFocusKey ForEquipment(EquipmentSlotType slot) =>
        new(slot, null, null);

    public static InventoryFocusKey ForAccessory(int index) =>
        new(EquipmentSlotType.Accessory, index, null);

    public static InventoryFocusKey ForItem(string itemId) =>
        new(null, null, itemId);
}

private readonly record struct PendingFocusRestore(
    InventoryFocusKey Preferred,
    int PreviousCatalogueIndex)
{
    public PendingFocusRestore WithPreferred(InventoryFocusKey preferred) =>
        this with { Preferred = preferred };
}
```

Before any catalogue activation, seed `_pendingFocusRestore` from the focused item ID + its current index; equipment/accessory activation replaces only the preferred key as above.

- [ ] **Step 15: Implement grow/reuse/shrink dynamic catalogue**

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
        _inventoryEntryBySlot.Remove(last);
        last.QueueFree();
    }

    _inventoryEntryBySlot.Clear();
    _inventorySlotByItemId.Clear();

    for (var index = 0; index < entries.Count; index++)
        BindInventorySlot(_inventorySlots[index], entries[index]);
}
```

`CreateInventorySlot()` instantiates `SiriusItemSlot`, adds it to `%InventoryGrid`, connects `Activated`, `FocusEntered`, and `MouseEntered` exactly once, and applies current compact size.

`BindInventorySlot`:

```csharp
private void BindInventorySlot(SiriusItemSlotController slot, InventoryEntry entry)
{
    _inventoryEntryBySlot[slot] = entry;
    _inventorySlotByItemId[entry.Item.Id] = slot;

    var quantity = entry.Quantity > 1 ? $"×{entry.Quantity}" : string.Empty;
    var state = entry.Item switch
    {
        EquipmentItem => SiriusItemSlotVisualState.Available,
        ConsumableItem consumable when consumable.Effect?.RequiresBattle != true
            => SiriusItemSlotVisualState.Available,
        _ => SiriusItemSlotVisualState.Unsupported
    };

    var stateText = state == SiriusItemSlotVisualState.Unsupported
        ? entry.Item is ConsumableItem ? "BATTLE ONLY" : "UNSUPPORTED"
        : string.Empty;

    slot.SetCompact(_isCompact);
    slot.PresentItem(
        entry.Item.LoadAssetOrDefault<Texture2D>(),
        quantity,
        stateText,
        BuildInventoryTooltip(entry),
        state);
}
```

- [ ] **Step 16: Route activation through existing methods**

```csharp
private void OnInventorySlotActivated(SiriusItemSlotController slot)
{
    if (!_inventoryEntryBySlot.TryGetValue(slot, out var entry))
        return;

    var index = _inventorySlots.IndexOf(slot);
    _pendingFocusRestore = new PendingFocusRestore(
        InventoryFocusKey.ForItem(entry.Item.Id),
        index);

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

No action method takes a new view-model or DTO.

- [ ] **Step 17: Restore focus semantically after `RefreshUI`**

Resolution order:

```text
1. exact preferred equipment slot / accessory index / surviving item ID
2. if preferred item disappeared, current entry at PreviousCatalogueIndex
3. otherwise previous last catalogue entry
4. active-page fallback: Equipment first slot / Items first item / Skills selector
5. active compact page button
6. Close
```

Before `GrabFocus`, require:

```csharp
GodotObject.IsInstanceValid(target) &&
target.IsVisibleInTree() &&
target.FocusMode != Control.FocusModeEnum.None
```

Clear `_pendingFocusRestore` after one restore attempt.

After every catalogue rebind, call `RefreshFocusSummaryFromCurrentFocus()` so a surviving focused node gets fresh content even when `FocusEntered` did not fire.

- [ ] **Step 18: Keep focus summary plain and passive**

Use existing tooltip builders as source text:

```csharp
private void PresentFocusSummary(string text) =>
    _focusSummary.Text = text ?? string.Empty;
```

Update it from slot `FocusEntered` / `MouseEntered` and active-skill focus/selection. It never routes an action or stores selection.

- [ ] **Step 19: Implement Settings-style compact page visibility and raw shoulders**

```csharp
private enum InventoryPage
{
    Equipment,
    Items,
    Skills
}

private void SetCompactPage(InventoryPage page)
{
    _activeCompactPage = page;
    _equipmentTab.ButtonPressed = page == InventoryPage.Equipment;
    _itemsTab.ButtonPressed = page == InventoryPage.Items;
    _skillsTab.ButtonPressed = page == InventoryPage.Skills;
    ApplyPageVisibility();
}
```

Responsive rules:

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

Extend existing `_Input` only for compact shoulders:

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

Do not handle Cancel/toggle here.

- [ ] **Step 20: Run behavioral focus tests before adding any explicit neighbors**

Run only the compact focus tests first:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest.Compact_EquipmentTabDownAndFirstControlUpUseSpatialNavigation|FullyQualifiedName~InventoryMenuSceneTest.Compact_LastEquipmentControlDownReachesClose"
```

If both pass: add **no** `FocusNeighbor*` assignments.

If a boundary fails solely because Godot spatial focus chooses the wrong target, add only the direct assignment for that failing boundary. Examples:

```csharp
_equipmentTab.FocusNeighborBottom = _equipmentTab.GetPathTo(_weaponSlot);
_weaponSlot.FocusNeighborTop = _weaponSlot.GetPathTo(_equipmentTab);
_accessorySlots[^1].FocusNeighborBottom = _accessorySlots[^1].GetPathTo(_closeButton);
```

Do not add `LinkVertical`, a generic neighbor graph, or a refresh-all-neighbours helper. If dynamic Items require an override, update only the first/last Items boundary when catalogue count changes and keep the behavioral test as its justification.

- [ ] **Step 21: Migrate existing node-shape tests atomically**

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

Rewrite texture-state assertions to `%Icon.Texture.ResourcePath` / `%Icon.StretchMode`. Preserve heading, active-skill, Close-hint, equip/unequip/rollback/consumable tests.

Replace fake accessory-lock tests with the four-real-slot and accessory-fill regressions from Steps 2/6.

- [ ] **Step 22: Delete the now-dead TextureButton presenter path**

After `InventoryMenuController` has no `TextureButton` slot usage, delete from `UiIconPresenter.cs`:

```text
Apply(TextureButton target, UiIconId id, UiIconSize size)
ApplyTexture(TextureButton target, Texture2D? texture)
ApplyGlyphTexture(TextureButton target, Texture2D? texture)
SetSlotTextures(...)
```

Keep `Apply(TextureRect, ...)`, `ApplyGlyph(TextureRect, ...)`, `ApplyItem(TextureRect, ...)`, and `Apply(Button, ...)`.

- [ ] **Step 23: Remove fixed-grid diagnostics**

Delete logs/warnings tied only to authored capacity:

```text
InitializeInventorySlots: found ...
Inventory UI slots tracked: ...
Inventory slot ...
Inventory UI only displays ... hidden.
```

Keep real domain and missing-asset warnings.

### Task 2D — verification gate before the atomic commit

- [ ] **Step 24: Run Task 2 focused suites GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 25: Run the full suite at the riskiest cutover boundary**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failures. This is required before the Task 2 commit because the `.tscn`/controller migration is the largest cross-scene risk in HPA-357.

- [ ] **Step 26: Commit Task 2**

```bash
git add \
  scenes/ui/InventoryMenu.tscn \
  scripts/ui/InventoryMenuController.cs \
  tests/ui/InventoryMenuSceneTest.cs \
  tests/ui/InventoryMenuControllerTest.cs \
  tests/ui/art/Hpa374RuntimeSmokeTest.cs \
  scripts/ui/art/UiIconPresenter.cs
git commit -m "feat(ui): redesign responsive Inventory screen"
```

---

## Task 3: Change only the Inventory HUD host policy and finish verification

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

If matches exist: replace only obsolete Inventory node paths with Task 2 stable names. Do not add compact-page behavior to the lifecycle suite.

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
  --filter "FullyQualifiedName~SiriusItemSlotControllerTest|FullyQualifiedName~InventoryMenuSceneTest|FullyQualifiedName~InventoryMenuControllerTest|FullyQualifiedName~Hpa374RuntimeSmokeTest|FullyQualifiedName~ExplorationHudControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~SiriusUiMetricsTest|FullyQualifiedName~SiriusUiContractsTest"
```

Expected: zero failures.

- [ ] **Step 8: Run final full suite and build**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
dotnet build Sirius.sln --no-restore
```

Expected: zero test failures and zero build errors.

- [ ] **Step 9: Audit stale fixed presentation, fake accessory slots, and dead presenter APIs**

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

Expected: zero positive production/test dependencies. Negative absence assertions in `InventoryMenuSceneTest` are allowed.

```bash
rg -n "TextureButton|ApplyTexture\(|ApplyGlyphTexture\(|SetSlotTextures" \
  scripts/ui/art/UiIconPresenter.cs scripts/ui/InventoryMenuController.cs
```

Expected: zero legacy slot-presentation matches.

```bash
rg -n "GetTree\(\)\.Paused|SceneTree\.Paused" scripts/ui/InventoryMenuController.cs
```

Expected: zero matches.

- [ ] **Step 10: Audit visual-state and focus-neighbour YAGNI**

```bash
rg -n "SiriusItemSlotVisualState\.Locked|\bLocked,|bool actionable|bool Actionable \{ get; private set; \}" \
  scripts/ui/components/SiriusItemSlotController.cs tests/ui/components/SiriusItemSlotControllerTest.cs
```

Expected: zero matches.

```bash
rg -n "FocusNeighbor|LinkVertical" \
  scripts/ui/InventoryMenuController.cs scenes/ui/InventoryMenu.tscn
```

Expected by default: zero matches. If Task 2 Step 20 required a specific evidence-backed override, only those direct assignments may remain; no helper/general neighbor graph is allowed.

- [ ] **Step 11: Audit HPA-375/framework creep and refresh-scoped map comment**

```bash
rg -n -i \
  "favorite|favourite|compare|comparison|filter|sort mode|drop item|sell item|bulk|inventory viewmodel|inventory presenter|collection renderer|navigation service" \
  scripts/ui/InventoryMenuController.cs scripts/ui/components/SiriusItemSlotController.cs scenes/ui/InventoryMenu.tscn
```

Expected: zero implementation matches.

```bash
rg -n "Refresh-scoped only|never retain one.*across.*mutation" \
  scripts/ui/InventoryMenuController.cs
```

Expected: the `_inventoryEntryBySlot` lifetime comment is present.

- [ ] **Step 12: Check diff hygiene and domain-file boundary**

```bash
git diff --check
git status --short
git diff --name-only main...HEAD
```

Expected production/test scope:

```text
scripts/ui/components/SiriusItemSlotController.cs
scenes/ui/components/SiriusItemSlot.tscn
scripts/ui/SiriusPlayerSummaryPresenter.cs
scripts/ui/art/UiIconPresenter.cs
resources/ui/theme/SiriusTheme.tres
scripts/ui/theme/SiriusThemeTypes.cs
scripts/ui/theme/SiriusUiMetrics.cs
scripts/ui/ExplorationHudController.cs
scenes/ui/InventoryMenu.tscn
scripts/ui/InventoryMenuController.cs
scripts/game/Game.cs
tests/ui/components/SiriusItemSlotControllerTest.cs
tests/ui/theme/SiriusUiMetricsTest.cs
tests/ui/theme/SiriusUiContractsTest.cs
tests/ui/ExplorationHudControllerTest.cs
tests/ui/InventoryMenuSceneTest.cs
tests/ui/InventoryMenuControllerTest.cs
tests/ui/art/Hpa374RuntimeSmokeTest.cs
tests/game/GameplayPauseHostTest.cs
```

`tests/game/GameInputLifecycleTest.cs` and `docs/ui/hpa-376/ui-lifecycle-contract.md` appear only if their audits found stale references.

Explicitly require no changed path under:

```text
scripts/data/Character.cs
scripts/data/Inventory.cs
scripts/data/EquipmentSet.cs
scripts/save/
```

The design/plan docs are expected implementation inputs.

- [ ] **Step 13: Commit Task 3**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs
```

Add `tests/game/GameInputLifecycleTest.cs` and/or `docs/ui/hpa-376/ui-lifecycle-contract.md` only if Steps 5/6 required edits.

```bash
git commit -m "feat(ui): complete hosted Inventory parity migration"
```

---

## Final Self-Review Checklist

- [ ] One `InventoryMenuController`; no presenter/view-model/collection renderer/navigation service.
- [ ] `Game` / `UIScreenHost` remain lifecycle owners; only Inventory HUD policy changes in `Game`.
- [ ] Dynamic catalogue grows/reuses/shrinks exactly current entries; no authored 24-slot or 100-placeholder capacity.
- [ ] Standard and compact reuse one content tree.
- [ ] `SiriusItemSlot` is the only new UI leaf.
- [ ] `SiriusItemSlotVisualState` has exactly Empty / Available / Equipped / Unsupported.
- [ ] `Actionable` is derived from state; no independent actionable parameter exists.
- [ ] `SiriusUiMetrics` gains only `ItemSlotSize`.
- [ ] TextureRect glyph/item behavior is owned by `UiIconPresenter`; legacy TextureButton slot presenter APIs are deleted only after Inventory cutover.
- [ ] HPA-374 Inventory smoke migrates in the same atomic Task 2 cutover.
- [ ] Exactly four accessory slots are authored and all four are reachable through first-empty indexed equip routing.
- [ ] Full accessory set preserves existing slot-0 replacement behavior.
- [ ] Exploration HUD and Inventory share one common name/HP/MP/EXP fallback presenter.
- [ ] Gold copy remains exact.
- [ ] Focus restoration uses equipment slot type / accessory index / item ID + prior catalogue index; no dynamic `Control` identity is persisted.
- [ ] Mutation restores focus to a valid semantic target and re-pushes `%FocusSummary` after rebind.
- [ ] `%FocusSummary` is plain wrapped Label text; no BBCode parser is introduced.
- [ ] Compact page selection copies Settings-style buttons while content remains visibility-based.
- [ ] Behavioral `ui_up` / `ui_down` tests are used; explicit FocusNeighbor overrides exist only if those tests prove a boundary needs one.
- [ ] Raw LB/RB works through actual paused viewport input; no new InputMap actions exist.
- [ ] Shoulder/page tests live in Inventory suites, not the host suite.
- [ ] Task 2 full suite passes before its large scene/controller commit.
- [ ] Existing equip/unequip/consume/rollback/active-skill methods remain the domain path.
- [ ] No domain/save-format production file changed.
- [ ] Focused tests, full tests, build, `git diff --check`, stale-pattern searches, and scope audit are green.
