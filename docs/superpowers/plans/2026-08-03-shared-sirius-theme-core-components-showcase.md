# Shared Sirius Theme, Core Components, and UI Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement HPA-377 as one opt-in Godot Theme resource, seven thin presentation components, an isolated showcase, and focused deterministic tests without introducing another lifecycle framework.

**Architecture:** `SiriusTheme.tres` is the single visual source of truth. Small C# contracts expose stable Theme names, closed enum mappings, responsive metrics, and motion constants; components only map presentation state and compose scene-authored controls. `UIScreenHost`, settings persistence, notification queueing, navigation, focus restoration, and production-screen migration remain outside this work.

**Tech Stack:** Godot.NET SDK 4.6.2, Godot 4.6, C# 12, .NET 8, GdUnit4 5.0, `Sirius.sln`, `test.runsettings.local` locally and `test.runsettings` in CI.

## Global Constraints

- The design is already approved. Do not change its status as an implementation task.
- Do not modify `project.godot`, set `gui/theme/custom`, or opt a production screen into the Theme.
- Do not modify `MainMenu.tscn`, `Game.tscn`, `InventoryMenu.tscn`, `SettingsMenu.tscn`, `BattleScene.tscn`, or existing production controllers.
- Do not add an autoload, registry, coordinator, pure state model, lifecycle service, generic focus helper, or reusable loading state machine.
- Create exactly seven shared components: `SiriusActionButton`, `SiriusPanel`, `SiriusModalShell`, `SiriusStatBar`, `SiriusInputHint`, `SiriusContextPrompt`, and `SiriusToastShell`.
- Keep Ignition as a stock square `Button` using `SiriusIgnitionButton`; do not add an Ignition component.
- Shared stat kinds are only Health, Mana, and Experience. `SiriusInvalidBar` is an internal Theme variation, not a public stat kind.
- Shared panel surfaces are only Content, Feature, HudPlate, and Modal.
- `SiriusUiSeverity` is only Info, Success, Warning, and Error.
- Required fonts, ornaments, and icons fail resource tests when absent. Do not rely on `UiArtCatalog.LoadIcon()` to prove existence because it can substitute the Info icon.
- Only a `Viewport` or `SubViewport` owner computes compact mode. Controls in the same viewport inherit that decision.
- Components never read `GameManager`, `SaveManager`, `SettingsManager`, `RecoveryChest`, or `UIScreenHost`.
- Persisted reduced motion remains HPA-541 work. Toast/reward queueing and short confirmation seals remain HPA-386 work.
- Loading is a static showcase fixture only: a disabled Primary button labelled `Loading…`.
- Keep every HPA-377 test file below 500 lines. Split responsibility before the guard fails.
- Use structural/resource assertions rather than pixel equality.
- Style only Label, Button, Panel/PanelContainer, ProgressBar, TabBar/TabContainer, TooltipPanel/TooltipLabel, ScrollContainer, HScrollBar, and VScrollBar.
- Inline code below is implementation intent. Compile and run each red/green step instead of copying unverified snippets across task boundaries.

## File Map

Create:

```text
resources/ui/theme/SiriusTheme.tres

scripts/ui/theme/
├── SiriusThemeTypes.cs
├── SiriusUiTypes.cs
├── SiriusUiMetrics.cs
└── SiriusMotion.cs

scripts/ui/components/
├── SiriusActionButton.cs
├── SiriusPanel.cs
├── SiriusModalShell.cs
├── SiriusStatBar.cs
├── SiriusInputHint.cs
├── SiriusContextPrompt.cs
└── SiriusToastShell.cs

scenes/ui/components/
├── SiriusModalShell.tscn
├── SiriusStatBar.tscn
├── SiriusInputHint.tscn
├── SiriusContextPrompt.tscn
└── SiriusToastShell.tscn

scenes/ui/showcase/SiriusUiShowcase.tscn
scripts/ui/showcase/SiriusUiShowcase.cs

tests/ui/theme/
├── SiriusUiContractsTest.cs
├── SiriusThemeTypographyTest.cs
└── SiriusThemeControlsTest.cs

tests/ui/components/
├── SiriusComponentTestSupport.cs
├── SiriusActionButtonTest.cs
├── SiriusPanelTest.cs
├── SiriusModalShellTest.cs
├── SiriusStatBarTest.cs
├── SiriusInputHintTest.cs
├── SiriusContextPromptTest.cs
└── SiriusToastShellTest.cs

tests/ui/showcase/
├── SiriusUiShowcaseStructureTest.cs
├── SiriusUiShowcaseResponsiveTest.cs
└── SiriusUiShowcaseFocusTest.cs

docs/ui/hpa-377/README.md
```

---

### Task 1: Add Closed Contracts, Metrics, and Motion

**Files:**
- Create: `scripts/ui/theme/SiriusThemeTypes.cs`
- Create: `scripts/ui/theme/SiriusUiTypes.cs`
- Create: `scripts/ui/theme/SiriusUiMetrics.cs`
- Create: `scripts/ui/theme/SiriusMotion.cs`
- Create: `tests/ui/theme/SiriusUiContractsTest.cs`

**Interfaces:**
- `SiriusThemeTypes.ResourcePath` and stable `StringName` fields.
- Closed enums: `SiriusActionButtonVariant`, `SiriusPanelSurface`, `SiriusUiSeverity`, `SiriusModalSizeClass`, `SiriusStatBarKind`.
- Mappings: `ToThemeType()`, `ToIconId()`, `ToModalPanelThemeType()`, `ToToastPanelThemeType()`.
- Metrics: `IsCompact(Vector2)`, `SafeMargin(bool)`, `MinimumTarget(bool)`, `IgnitionSize(bool)`, `ModalWidth(SiriusModalSizeClass)`, `VerificationViewports`, `FullInteractionViewports`.
- Motion constants plus `EntrySeconds(bool)`, `ExitSeconds(bool)`, and `UseTransform(bool)`.

- [ ] **Step 1: Write the failing runtime-backed contract suite**

Create `tests/ui/theme/SiriusUiContractsTest.cs` with the required runtime declaration:

```csharp
using GdUnit4;
using Godot;
using System;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiContractsTest : Node
{
    [TestCase]
    public void ClosedEnums_ContainOnlyApprovedValues()
    {
        AssertThat(Enum.GetValues<SiriusActionButtonVariant>()).ContainsExactly(
            SiriusActionButtonVariant.Primary,
            SiriusActionButtonVariant.Secondary,
            SiriusActionButtonVariant.Tertiary,
            SiriusActionButtonVariant.Warning,
            SiriusActionButtonVariant.Destructive);
        AssertThat(Enum.GetValues<SiriusPanelSurface>()).ContainsExactly(
            SiriusPanelSurface.Content,
            SiriusPanelSurface.Feature,
            SiriusPanelSurface.HudPlate,
            SiriusPanelSurface.Modal);
        AssertThat(Enum.GetValues<SiriusUiSeverity>()).ContainsExactly(
            SiriusUiSeverity.Info,
            SiriusUiSeverity.Success,
            SiriusUiSeverity.Warning,
            SiriusUiSeverity.Error);
        AssertThat(Enum.GetValues<SiriusModalSizeClass>()).ContainsExactly(
            SiriusModalSizeClass.Small,
            SiriusModalSizeClass.Medium,
            SiriusModalSizeClass.Large);
        AssertThat(Enum.GetValues<SiriusStatBarKind>()).ContainsExactly(
            SiriusStatBarKind.Health,
            SiriusStatBarKind.Mana,
            SiriusStatBarKind.Experience);
    }

    [TestCase]
    public void Mappings_AreExactAndUnknownValuesThrow()
    {
        AssertThat(SiriusActionButtonVariant.Primary.ToThemeType())
            .IsEqual(SiriusThemeTypes.PrimaryButton);
        AssertThat(SiriusPanelSurface.HudPlate.ToThemeType())
            .IsEqual(SiriusThemeTypes.HudPlate);
        AssertThat(SiriusUiSeverity.Warning.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.WarningPanel);
        AssertThat(SiriusStatBarKind.Experience.ToThemeType())
            .IsEqual(SiriusThemeTypes.ExpBar);
        AssertThat(SiriusStatBarKind.Health.ToIconId()).IsEqual(UiIconId.Health);
        AssertThrown(() => ((SiriusActionButtonVariant)99).ToThemeType())
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => ((SiriusStatBarKind)99).ToThemeType())
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void Metrics_MatchApprovedBreakpointsAndViewports()
    {
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(799, 720))).IsTrue();
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(1280, 449))).IsTrue();
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(800, 450))).IsFalse();
        AssertThat(SiriusUiMetrics.SafeMargin(false)).IsEqual(24);
        AssertThat(SiriusUiMetrics.SafeMargin(true)).IsEqual(12);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Small)).IsEqual(420);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Medium)).IsEqual(640);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Large)).IsEqual(960);
        AssertThat(SiriusUiMetrics.VerificationViewports).ContainsExactly(
            new Vector2I(640, 360), new Vector2I(1024, 768),
            new Vector2I(1280, 720), new Vector2I(1440, 900),
            new Vector2I(1920, 1080), new Vector2I(2560, 1080),
            new Vector2I(2560, 1440));
    }

    [TestCase]
    public void Motion_MatchesApprovedDurations()
    {
        AssertThat(SiriusMotion.ControlFeedbackSeconds).IsEqualApprox(0.120);
        AssertThat(SiriusMotion.CalloutEntrySeconds).IsEqualApprox(0.220);
        AssertThat(SiriusMotion.CalloutExitSeconds).IsEqualApprox(0.180);
        AssertThat(SiriusMotion.ScreenTransitionSeconds).IsEqualApprox(0.280);
        AssertThat(SiriusMotion.OrreryMaximumSeconds).IsEqualApprox(0.400);
        AssertThat(SiriusMotion.EntrySeconds(true)).IsLessEqual(0.100);
        AssertThat(SiriusMotion.UseTransform(true)).IsFalse();
    }
}
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
```

Expected: compile failure because the contracts do not exist—not a runtime-host error.

- [ ] **Step 3: Implement stable names**

`SiriusThemeTypes.cs` defines:

```csharp
using Godot;

public static class SiriusThemeTypes
{
    public const string ResourcePath = "res://resources/ui/theme/SiriusTheme.tres";

    public static readonly StringName Display = "SiriusDisplay";
    public static readonly StringName DisplayCompact = "SiriusDisplayCompact";
    public static readonly StringName Title = "SiriusTitle";
    public static readonly StringName TitleCompact = "SiriusTitleCompact";
    public static readonly StringName Section = "SiriusSection";
    public static readonly StringName SectionCompact = "SiriusSectionCompact";
    public static readonly StringName Body = "SiriusBody";
    public static readonly StringName BodyCompact = "SiriusBodyCompact";
    public static readonly StringName Metadata = "SiriusMetadata";
    public static readonly StringName MetadataCompact = "SiriusMetadataCompact";
    public static readonly StringName Numeric = "SiriusNumeric";
    public static readonly StringName NumericCompact = "SiriusNumericCompact";
    public static readonly StringName Telemetry = "SiriusTelemetry";

    public static readonly StringName PrimaryButton = "SiriusPrimaryButton";
    public static readonly StringName SecondaryButton = "SiriusSecondaryButton";
    public static readonly StringName TertiaryButton = "SiriusTertiaryButton";
    public static readonly StringName WarningButton = "SiriusWarningButton";
    public static readonly StringName DestructiveButton = "SiriusDestructiveButton";
    public static readonly StringName IgnitionButton = "SiriusIgnitionButton";

    public static readonly StringName ContentPanel = "SiriusContentPanel";
    public static readonly StringName FeaturePanel = "SiriusFeaturePanel";
    public static readonly StringName HudPlate = "SiriusHudPlate";
    public static readonly StringName ModalPanel = "SiriusModalPanel";
    public static readonly StringName WarningPanel = "SiriusWarningPanel";
    public static readonly StringName ErrorPanel = "SiriusErrorPanel";
    public static readonly StringName Scrim = "SiriusScrim";
    public static readonly StringName ChildScrim = "SiriusChildScrim";

    public static readonly StringName HpBar = "SiriusHpBar";
    public static readonly StringName MpBar = "SiriusMpBar";
    public static readonly StringName ExpBar = "SiriusExpBar";
    public static readonly StringName InvalidBar = "SiriusInvalidBar";
}
```

- [ ] **Step 4: Implement closed enums and exhaustive mappings**

`SiriusUiTypes.cs` defines the five enums and extension methods using switch expressions whose default arm throws `ArgumentOutOfRangeException`. Use these severity icons exactly:

```text
Info -> UiIconId.Info
Success -> UiIconId.Confirm
Warning -> UiIconId.Warning
Error -> UiIconId.Error
```

- [ ] **Step 5: Implement metrics and motion**

Do not add slot metrics. Define spacing 4/8/12/16/24/32/48, margins 24/12, max width 1600, targets 44/40, modal widths 420/640/960, tooltips 360/280, Ignition 96/80, the seven viewports, and four interaction viewports.

- [ ] **Step 6: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
git add scripts/ui/theme tests/ui/theme/SiriusUiContractsTest.cs
git commit -m "feat: add Sirius UI contracts"
```

Expected: PASS.

---

### Task 2: Author Palette and Typography

**Files:**
- Create: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeTypographyTest.cs`

**Interfaces:**
- One Theme resource with direct references to the five approved fonts.
- Six standard/compact Label pairs plus fixed Telemetry.

- [ ] **Step 1: Write failing typography tests**

Declare `[RequireGodotRuntime]` and inherit `Node`. Load the Theme from `SiriusThemeTypes.ResourcePath` and assert:

```text
Display 44 / DisplayCompact 30
Title 32 / TitleCompact 24
Section 20 / SectionCompact 17
Body 16 / BodyCompact 14
Metadata 14 / MetadataCompact 12
Numeric 16 / NumericCompact 14
Telemetry 12
```

Assert direct font resource paths for Cinzel, Noto Sans Regular/Medium/SemiBold, and Noto Sans Mono Medium. Assert Display uses Cinzel; Body does not; Numeric and Telemetry use Mono.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
```

- [ ] **Step 3: Create the Theme resource**

Start with these direct ext-resources:

```text
res://assets/fonts/cinzel/Cinzel-Variable.ttf
res://assets/fonts/noto_sans/NotoSans-Regular.ttf
res://assets/fonts/noto_sans/NotoSans-Medium.ttf
res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf
res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf
```

Configure each Label variation with `Theme.set_type_variation(<variation>, "Label")` semantics encoded in the `.tres`. Use Noto Sans fallback for the Cinzel FontVariation. Set tracked telemetry and the approved line spacing. Do not add Entity or HUD-specific duplicate roles.

- [ ] **Step 4: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
git add resources/ui/theme/SiriusTheme.tres tests/ui/theme/SiriusThemeTypographyTest.cs
git commit -m "feat: add Sirius palette and typography"
```

Expected: PASS.

---

### Task 3: Author Native States, Ignition, Surfaces, Bars, and Scrims

**Files:**
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeControlsTest.cs`

**Interfaces:**
- Five conventional Button variations and stock Ignition.
- Four public panel surfaces plus internal Warning/Error and two scrims.
- HP, MP, EXP, and internal Invalid bar variations.
- Tab, tooltip, and scrollbar base styling.

- [ ] **Step 1: Write failing control-resource tests**

Declare runtime support. Assert every button variation has:

```text
normal hover pressed hover_pressed focus disabled
```

Assert panel/bar/scrim variations and exact scrim alpha. Assert `SiriusInvalidBar` exists.

Check required art and icon files directly:

```csharp
string[] requiredResources =
[
    "res://assets/sprites/ui/ornaments/ignition_seal.png",
    "res://assets/sprites/ui/ornaments/focus_halo.png",
    UiArtCatalog.GetIconPath(UiIconId.Info, UiIconSize.Default),
    UiArtCatalog.GetIconPath(UiIconId.Confirm, UiIconSize.Default),
    UiArtCatalog.GetIconPath(UiIconId.Warning, UiIconSize.Default),
    UiArtCatalog.GetIconPath(UiIconId.Error, UiIconSize.Default),
    UiArtCatalog.GetIconPath(UiIconId.Health, UiIconSize.Metadata),
    UiArtCatalog.GetIconPath(UiIconId.Mana, UiIconSize.Metadata),
    UiArtCatalog.GetIconPath(UiIconId.Experience, UiIconSize.Metadata)
];
foreach (var path in requiredResources)
{
    AssertThat(FileAccess.FileExists(path)).IsTrue();
    AssertThat(ResourceLoader.Exists(path)).IsTrue();
}
```

Do not prove existence by calling `UiArtCatalog.LoadIcon()` or `UiIconPresenter.Apply()`.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeControlsTest"
```

- [ ] **Step 3: Add conventional controls**

Use Theme-owned StyleBox resources for normal, hover, pressed, hover-pressed, focus, and disabled states. Disabled text/icon alpha is 45% and disabled styles have no glow. Focus is cyan and does not change content margins. Selection uses stock pressed/toggled state plus gold treatment.

- [ ] **Step 4: Add stock Ignition**

`SiriusIgnitionButton` is based on Button. Reuse `ignition_seal.png` for state StyleBoxTextures with state-specific modulation. Use `focus_halo.png` for focus. Preferred size remains a component/showcase metric, not embedded lifecycle logic.

- [ ] **Step 5: Add panels, bars, scrims, tabs, tooltips, and scrollbars**

```text
Panels: Content, Feature, HudPlate, Modal, Warning, Error
Scrims: 58% and 72% night-1000
Bars: Hp, Mp, Exp, Invalid
Native: TabBar/TabContainer, TooltipPanel/TooltipLabel,
        ScrollContainer/HScrollBar/VScrollBar
```

- [ ] **Step 6: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeControlsTest|FullyQualifiedName~SiriusThemeTypographyTest"
git add resources/ui/theme/SiriusTheme.tres tests/ui/theme
git commit -m "feat: add Sirius control theme states"
```

Expected: PASS.

---

### Task 4: Implement ActionButton and Panel

**Files:**
- Create: `scripts/ui/components/SiriusActionButton.cs`
- Create: `scripts/ui/components/SiriusPanel.cs`
- Create: `tests/ui/components/SiriusActionButtonTest.cs`
- Create: `tests/ui/components/SiriusPanelTest.cs`

**Interfaces:**
- `SiriusActionButton`: `Variant`, `ShowIcon`, `IconId`, `IconSize`, `DisabledReason`, `SetIcon(UiIconId?)`.
- `SiriusPanel`: `Surface`.

- [ ] **Step 1: Write failing ActionButton tests**

Use `[RequireGodotRuntime]` and `Node`. Assert:

```csharp
var button = new SiriusActionButton
{
    Variant = SiriusActionButtonVariant.Warning,
    DisabledReason = "Requires a valid target"
};
AddChild(button);
AssertThat(button.ThemeTypeVariation).IsEqual(SiriusThemeTypes.WarningButton);
AssertThat(button.GetTooltip()).IsEmpty();
button.Disabled = true;
AssertThat(button.GetTooltip()).IsEqual("Requires a valid target");
AssertThat(button.MouseFilter).IsNotEqual(Control.MouseFilterEnum.Ignore);
button.SetIcon(UiIconId.Warning);
AssertThat(button.ShowIcon).IsTrue();
AssertThat(button.IconId).IsEqual(UiIconId.Warning);
AssertThat(button.Icon!.ResourcePath)
    .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Warning, UiIconSize.Default));
button.SetIcon(null);
AssertThat(button.ShowIcon).IsFalse();
AssertThat(button.Icon).IsNull();
```

The enabled button must not expose the disabled reason. The test does not depend on property assignment order.

- [ ] **Step 2: Write failing Panel tests and run**

Assert all four surfaces map exactly and unknown enums throw.

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
```

- [ ] **Step 3: Implement ActionButton**

Use editor-safe exported fields and one runtime convenience method:

```csharp
using Godot;

[Tool]
public partial class SiriusActionButton : Button
{
    private SiriusActionButtonVariant _variant;
    private bool _showIcon;
    private UiIconId _iconId = UiIconId.Info;
    private UiIconSize _iconSize = UiIconSize.Default;
    private string _disabledReason = string.Empty;

    [Export] public SiriusActionButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; RefreshPresentation(); }
    }
    [Export] public bool ShowIcon
    {
        get => _showIcon;
        set { _showIcon = value; RefreshPresentation(); }
    }
    [Export] public UiIconId IconId
    {
        get => _iconId;
        set { _iconId = value; RefreshPresentation(); }
    }
    [Export] public UiIconSize IconSize
    {
        get => _iconSize;
        set { _iconSize = value; RefreshPresentation(); }
    }
    [Export] public string DisabledReason
    {
        get => _disabledReason;
        set => _disabledReason = value ?? string.Empty;
    }

    public override void _Ready() => RefreshPresentation();

    public void SetIcon(UiIconId? icon)
    {
        ShowIcon = icon.HasValue;
        if (icon.HasValue)
            IconId = icon.Value;
        RefreshPresentation();
    }

    public override string _GetTooltip(Vector2 atPosition) =>
        Disabled && !string.IsNullOrWhiteSpace(DisabledReason)
            ? DisabledReason
            : string.Empty;

    private void RefreshPresentation()
    {
        ThemeTypeVariation = Variant.ToThemeType();
        if (!IsNodeReady()) return;
        if (ShowIcon)
            UiIconPresenter.Apply(this, IconId, IconSize);
        else
            Icon = null;
    }
}
```

The nullable runtime method sets the two inspector properties atomically. `ShowIcon=false` makes `IconId` inert.

- [ ] **Step 4: Implement Panel**

```csharp
using Godot;

[Tool]
public partial class SiriusPanel : PanelContainer
{
    private SiriusPanelSurface _surface;
    [Export] public SiriusPanelSurface Surface
    {
        get => _surface;
        set { _surface = value; ThemeTypeVariation = value.ToThemeType(); }
    }
    public override void _Ready() => ThemeTypeVariation = Surface.ToThemeType();
}
```

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
git add scripts/ui/components/SiriusActionButton.cs scripts/ui/components/SiriusPanel.cs \
  tests/ui/components/SiriusActionButtonTest.cs tests/ui/components/SiriusPanelTest.cs
git commit -m "feat: add Sirius action and panel components"
```

Expected: PASS.

---

### Task 5: Implement ModalShell

**Files:**
- Create: `scenes/ui/components/SiriusModalShell.tscn`
- Create: `scripts/ui/components/SiriusModalShell.cs`
- Create: `tests/ui/components/SiriusModalShellTest.cs`

**Interfaces:**
- `Title`, `Severity`, `SizeClass`, `Compact`, `ReducedMotion`, `ShowCloseAffordance`.
- `BodyHost`, `ActionsHost`, `CloseRequested`, `RefreshPresentation(Vector2)`, `PlayEntry()`, `PlayExit()`.
- No scrim, host, focus, Cancel, dismissal, or domain ownership.

- [ ] **Step 1: Write failing tests**

Assert Error mapping, Small width, compact title variation, Error icon path, body/actions getters, and absence of `%Scrim`. Test normal motion starts with a 12 px offset; reduced motion starts without translation and uses at most 100 ms.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusModalShell : Control
└── Panel : SiriusPanel [%Panel, Surface=Modal]
    └── Margin : MarginContainer [24]
        └── RootLayout : VBoxContainer [separation=16]
            ├── Header : HBoxContainer
            │   ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            │   ├── TitleLabel : Label [%TitleLabel]
            │   └── CloseButton : SiriusActionButton [%CloseButton, Tertiary]
            ├── BodyScroll : ScrollContainer [%BodyScroll]
            │   └── BodyHost : VBoxContainer [%BodyHost]
            └── ActionsHost : HBoxContainer [%ActionsHost, separation=8]
```

No scrim node.

- [ ] **Step 4: Implement presentation and visual-only motion**

Severity selects panel and icon. Compact switches nested label variations. Width is viewport-minus-12-px margins in compact mode; otherwise `min(size class, 90% viewport)`. Close emits `CloseRequested` only.

Entry is alpha plus 12 px translation in normal mode; exit is alpha plus 8 px. Reduced motion uses alpha only and at most 100 ms. Kill only the shell's current Tween before starting another. Never queue-free from the component.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
git add scenes/ui/components/SiriusModalShell.tscn \
  scripts/ui/components/SiriusModalShell.cs tests/ui/components/SiriusModalShellTest.cs
git commit -m "feat: add Sirius modal shell"
```

Expected: PASS.

---

### Task 6: Implement StatBar

**Files:**
- Create: `scenes/ui/components/SiriusStatBar.tscn`
- Create: `scripts/ui/components/SiriusStatBar.cs`
- Create: `tests/ui/components/SiriusStatBarTest.cs`

**Interfaces:**
- `Kind`, `Current`, `Maximum`, `Label`, `ShowNumericValue`, `LowThreshold`, `Compact`, `RefreshPresentation()`.

- [ ] **Step 1: Write failing edge-case tests**

Assert:

```text
Health 20/100 -> SiriusHpBar, value 20, state Low, Health icon
120/100 -> value 100, text "120 / 100", state Overflow
-5/100 -> value 0, state Invalid value
10/0 -> SiriusInvalidBar, zero fill, state Invalid maximum
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusStatBarTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusStatBar : VBoxContainer
├── Header : HBoxContainer [separation=4]
│   ├── Icon : TextureRect [%Icon, 16×16]
│   ├── NameLabel : Label [%NameLabel]
│   ├── Spacer : Control [ExpandFill]
│   └── ValueLabel : Label [%ValueLabel]
├── Bar : ProgressBar [%Bar, ShowPercentage=false]
└── StateLabel : Label [%StateLabel, hidden]
```

- [ ] **Step 4: Implement deterministic rules**

For valid maximum, set `Bar.MaxValue=Maximum` and clamp only visual `Bar.Value`. Always preserve caller values in text. For `Maximum <= 0`, set range 0..1, zero fill, `SiriusInvalidBar`, and `Invalid maximum`. Set `Invalid value`, `Overflow`, or `Low` in that order. Use direct Kind-to-icon mapping and standard/compact nested label variations.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusStatBarTest"
git add scenes/ui/components/SiriusStatBar.tscn scripts/ui/components/SiriusStatBar.cs \
  tests/ui/components/SiriusStatBarTest.cs
git commit -m "feat: add Sirius stat bar"
```

Expected: PASS.

---

### Task 7: Implement InputHint and ContextPrompt

**Files:**
- Create: `scenes/ui/components/SiriusInputHint.tscn`
- Create: `scripts/ui/components/SiriusInputHint.cs`
- Create: `scenes/ui/components/SiriusContextPrompt.tscn`
- Create: `scripts/ui/components/SiriusContextPrompt.cs`
- Create: `tests/ui/components/SiriusInputHintTest.cs`
- Create: `tests/ui/components/SiriusContextPromptTest.cs`

**Interfaces:**
- InputHint: `Prompt`, `Actions`, `Compact`, `ActiveDevice`, `Observe(InputEvent)`, `Refresh()`.
- ContextPrompt: `ShowIcon`, `IconId`, `Prompt`, `Actions`, `Compact`, `Refresh()`.

- [ ] **Step 1: Write failing InputHint tests**

Use a temporary InputMap action and restore it in `finally`. Assert Keyboard K, Mouse 1, Gamepad A, fallback, and Unbound labels/icons.

- [ ] **Step 2: Write failing ContextPrompt tests and run**

Assert Dialogue icon, `Talk` prompt, and `interact` propagation.

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
```

- [ ] **Step 3: Author InputHint**

```text
SiriusInputHint : HBoxContainer [separation=4]
├── DeviceIcon : TextureRect [%DeviceIcon, 16×16]
├── PromptLabel : Label [%PromptLabel]
└── BindingLabel : Label [%BindingLabel]
```

Wrap `InputHintPresenter`. `Refresh()` calls `ResolveActions(Actions)`, applies the icon with `UiIconPresenter.Apply(TextureRect, ...)`, sets labels, and switches Metadata variations. Process input only while visible; connect/disconnect `VisibilityChanged` safely.

- [ ] **Step 4: Author ContextPrompt**

```text
SiriusContextPrompt : HBoxContainer [separation=8]
├── SemanticIcon : TextureRect [%SemanticIcon, 24×24]
├── PromptLabel : Label [%PromptLabel]
└── InputHint : SiriusInputHint [%InputHint]
```

`ShowIcon` is the authoritative inspector presence flag. `Refresh()` applies/clears the icon, sets Body standard/compact prompt variation, and propagates actions/compact to the child. It never discovers or invokes an interaction.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
git add scenes/ui/components/SiriusInputHint.tscn scenes/ui/components/SiriusContextPrompt.tscn \
  scripts/ui/components/SiriusInputHint.cs scripts/ui/components/SiriusContextPrompt.cs \
  tests/ui/components/SiriusInputHintTest.cs tests/ui/components/SiriusContextPromptTest.cs
git commit -m "feat: add Sirius input and context prompts"
```

Expected: PASS.

---

### Task 8: Implement ToastShell

**Files:**
- Create: `scenes/ui/components/SiriusToastShell.tscn`
- Create: `scripts/ui/components/SiriusToastShell.cs`
- Create: `tests/ui/components/SiriusToastShellTest.cs`

**Interfaces:**
- `Severity`, `Title`, `Message`, `Compact`, `ReducedMotion`, `RefreshPresentation()`, `PlayEntry()`, `PlayExit()`.
- No Timer, queue, deduplication, host registration, acknowledgement, or retention.

- [ ] **Step 1: Write failing tests**

Assert Warning mapping, icon/text, compact variations, no Timer, no scrim, normal translation, and reduced-motion opacity-only behavior.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusToastShell : Control
└── Panel : SiriusPanel [%Panel, Surface=Feature]
    └── Margin : MarginContainer [12]
        └── Row : HBoxContainer [separation=8]
            ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            └── TextColumn : VBoxContainer [separation=4]
                ├── TitleLabel : Label [%TitleLabel]
                └── MessageLabel : Label [%MessageLabel]
```

No Timer and no scrim.

- [ ] **Step 4: Implement presentation/motion and commit**

Severity selects panel/icon. Compact selects Section and Body variations. Motion mirrors ModalShell and owns only the current Tween.

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
git add scenes/ui/components/SiriusToastShell.tscn scripts/ui/components/SiriusToastShell.cs \
  tests/ui/components/SiriusToastShellTest.cs
git commit -m "feat: add Sirius toast shell"
```

Expected: PASS.

---

### Task 9: Build the Showcase with Split Structural, Responsive, and Focus Tests

**Files:**
- Create: `scenes/ui/showcase/SiriusUiShowcase.tscn`
- Create: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseStructureTest.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseFocusTest.cs`

**Interfaces:**
- `PreviewViewport`, `PreviewRoot`, `Compact`, `SetPreviewSize(Vector2I)`, `SetBackground(SiriusShowcaseBackground)`, `SetReducedMotion(bool)`.

- [ ] **Step 1: Write failing Structure tests**

Assert named sections and fixtures:

```text
%PaletteSection %TypographySection %ButtonSection
%IgnitionStandardFixture %IgnitionCompactFixture
%SelectedFocusedFixture %LoadingFixture %TabsSection
%StatBarSection %InputHintSection %ContextPromptSection
%ToastSection %ModalSection %MotionSection
```

Assert four backgrounds, the exact stress strings, component node types, Loading as disabled Primary labelled `Loading…`, and required resources.

- [ ] **Step 2: Write failing Responsive tests**

Create one fixture in `[BeforeTest]`, resize sequentially through all seven `VerificationViewports`, and at every size assert:

```text
viewport size
single compact decision
safe margins
1600 maximum frame
minimum target sizes
reachable primary examples
long body wraps/scrolls
metadata exposes full tooltip
```

- [ ] **Step 3: Write failing Focus tests**

At 640×360, 1280×720, 1024×768, and 2560×1080, push `ui_focus_next` through the preview and assert the explicit chain remains inside `PreviewRoot`, includes the selected-plus-focused toggle, and loops to the first focusable control.

Each file must remain below 500 lines from its first commit.

- [ ] **Step 4: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcase"
```

- [ ] **Step 5: Author the showcase root**

```text
SiriusUiShowcase : Control
├── ShowcaseToolbar : HBoxContainer
│   ├── ViewportSizeSelector : OptionButton [%ViewportSizeSelector]
│   ├── BackgroundSelector : OptionButton [%BackgroundSelector]
│   └── ReducedMotionToggle : CheckBox [%ReducedMotionToggle]
└── PreviewFrame : PanelContainer
    └── PreviewContainer : SubViewportContainer [%PreviewContainer]
        └── PreviewViewport : SubViewport [%PreviewViewport]
            └── PreviewRoot : Control [%PreviewRoot, Theme=SiriusTheme.tres]
                ├── Background : TextureRect [%Background]
                ├── SolidBackground : ColorRect [%SolidBackground]
                └── SafeFrame : MarginContainer [%SafeFrame]
                    └── ResponsiveScroll : ScrollContainer
                        └── ShowcaseContent : VBoxContainer [%ShowcaseContent]
```

Use real shared components, stock Ignition, stock selected toggle, TabContainer, tooltips, and both scrims.

- [ ] **Step 6: Implement deterministic controls**

```csharp
public enum SiriusShowcaseBackground
{
    NightSolid,
    MoonSolid,
    MainMenuScenic,
    BattleScenic
}
```

Map exactly:

```text
NightSolid -> #050714
MoonSolid -> #F7F5FF
MainMenuScenic -> res://assets/sprites/ui/ui_main_menu_background.png
BattleScenic -> res://assets/sprites/ui/ui_battle_background.png
```

`SetPreviewSize()` updates the SubViewport, computes compact only from that owned viewport, and calls `ApplyCompactState()`. The compact method propagates component flags, free Label variations, safe margins, target sizes, and Ignition size. It does not inspect child rectangles to decide compact mode.

- [ ] **Step 7: Add exact stress fixtures**

```text
Action: Bestätigungsaktion mit ausführlicher Beschreibung
Body: The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling.
Metadata: OBSERVATORY-CALIBRATION-IDENTIFIER-000000000000
```

Body uses WordSmart wrapping. Metadata clipping is limited to its fixture and the full value is available through tooltip.

- [ ] **Step 8: Add explicit focus neighbours**

Set next/previous paths through action variants, toggle, tabs, hint fixtures, and modal actions. Loop to the first control. Do not add a focus coordinator.

- [ ] **Step 9: Run tests, size guard, and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcase"
wc -l tests/ui/showcase/*.cs
git add scenes/ui/showcase/SiriusUiShowcase.tscn scripts/ui/showcase/SiriusUiShowcase.cs \
  tests/ui/showcase
git commit -m "feat: add Sirius UI showcase"
```

Expected: PASS; every showcase test file below 500 lines.

---

### Task 10: Add the Integration Guide and Run Final Verification

**Files:**
- Create: `docs/ui/hpa-377/README.md`
- Modify only when a verification failure identifies a concrete defect: files created in Tasks 1–9

- [ ] **Step 1: Write the concise guide**

Use these sections:

```text
# Sirius Theme and Shared Components
Design
Opt in
Compact authority
Components
Handoffs
Prohibited patterns
```

Include:

```csharp
var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
screenRoot.Theme = theme;
```

State:

```text
Do not set ProjectSettings.gui/theme/custom during an isolated migration.
Only a Viewport/SubViewport owner calls SiriusUiMetrics.IsCompact().
Ignition is SiriusIgnitionButton, not a component.
HPA-541 owns persisted reduced motion.
HPA-386 owns toast/reward queueing and short confirmation seals.
Do not repeat shared StyleBoxFlat resources or palette values.
```

- [ ] **Step 2: Run focused tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~Sirius"
```

Expected: zero failures.

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: exit 0 and zero errors.

- [ ] **Step 4: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Preserve exact summary output for the PR.

- [ ] **Step 5: Audit scope**

```bash
git diff --name-only main...HEAD
```

Only these prefixes may appear:

```text
resources/ui/theme/
scripts/ui/theme/
scripts/ui/components/
scripts/ui/showcase/
scenes/ui/components/
scenes/ui/showcase/
tests/ui/theme/
tests/ui/components/
tests/ui/showcase/
docs/ui/hpa-377/
docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md
docs/superpowers/plans/2026-08-03-shared-sirius-theme-core-components-showcase.md
```

Fail review if `project.godot` or a production screen/controller appears.

- [ ] **Step 6: Check the test-file guard**

```bash
find tests/ui/theme tests/ui/components tests/ui/showcase -name '*.cs' \
  -print0 | xargs -0 wc -l
```

Expected: every HPA-377 test file below 500 lines.

- [ ] **Step 7: Commit the guide**

```bash
git add docs/ui/hpa-377/README.md
git commit -m "docs: add Sirius theme integration guide"
```

- [ ] **Step 8: Update the draft PR**

Copy exact focused-test, build, and full-suite summaries. Confirm the scope audit. Do not estimate counts from memory.