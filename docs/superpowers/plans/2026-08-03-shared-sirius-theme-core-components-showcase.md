# Shared Sirius Theme, Core Components, and UI Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement HPA-377 as one opt-in Godot Theme resource, five scene-authored presentation components, an isolated showcase, and focused deterministic tests without introducing wrapper controls or reusable lifecycle machinery.

**Architecture:** `SiriusTheme.tres` is the visual source of truth. Stock `Button`, `Panel`, and `PanelContainer` controls consume stable Theme variations directly. Five composite components add only presentation behavior stock controls cannot express; motion is demonstrated locally by the showcase rather than owned by components.

**Tech Stack:** Godot.NET SDK 4.6.2, Godot 4.6, C# 12, .NET 8, GdUnit4 5.0, `Sirius.sln`, `test.runsettings.local` locally and `test.runsettings` in CI.

## Global Constraints

- The design is approved; do not change its status during implementation.
- Do not modify `project.godot`, set `gui/theme/custom`, or opt a production screen into the Theme.
- Do not modify `MainMenu.tscn`, `Game.tscn`, `InventoryMenu.tscn`, `SettingsMenu.tscn`, `BattleScene.tscn`, or existing production controllers.
- Do not create `SiriusActionButton`, `SiriusPanel`, button/panel enums, an autoload, registry, coordinator, lifecycle service, generic focus helper, or reusable loading state machine.
- Create exactly five components: `SiriusModalShell`, `SiriusStatBar`, `SiriusInputHint`, `SiriusContextPrompt`, and `SiriusToastShell`.
- Stock Buttons and Panels select `SiriusThemeTypes` variations directly.
- Shared stat kinds are only Health, Mana, and Experience. Numeric values are always visible and the low threshold is fixed at 0.25.
- Required fonts, ornaments, and icons fail resource tests when absent. Do not use catalog fallback behavior to prove existence.
- Only a `Viewport` or `SubViewport` owner computes compact mode.
- Components never read `GameManager`, `SaveManager`, `SettingsManager`, `RecoveryChest`, or `UIScreenHost`.
- Components own no Tween, Timer, queue, dismissal, focus restoration, navigation, or asynchronous operation.
- Persisted reduced motion remains HPA-541 work. Toast/reward queueing and short confirmation seals remain HPA-386 work. Production modal lifecycle remains HPA-382 work.
- Loading is a static showcase fixture: a disabled stock Primary Button labelled `Loading…`.
- Keep every HPA-377 test file below 500 lines.
- Use structural/resource assertions rather than pixel equality.
- Style only Label, Button, Panel/PanelContainer, ProgressBar, TabBar/TabContainer, TooltipPanel/TooltipLabel, ScrollContainer, HScrollBar, and VScrollBar.
- Inline code is implementation intent; compile and run every red/green step.

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

### Task 1: Add Theme Names, Closed Types, Metrics, and HPA-377 Motion

**Files:**
- Create: `scripts/ui/theme/SiriusThemeTypes.cs`
- Create: `scripts/ui/theme/SiriusUiTypes.cs`
- Create: `scripts/ui/theme/SiriusUiMetrics.cs`
- Create: `scripts/ui/theme/SiriusMotion.cs`
- Create: `tests/ui/theme/SiriusUiContractsTest.cs`

**Interfaces:**
- `SiriusThemeTypes.ResourcePath` and stable `StringName` fields for every Label, Button, Panel, bar, and scrim variation.
- Closed enums: `SiriusUiSeverity`, `SiriusModalSizeClass`, `SiriusStatBarKind`.
- Mappings: `ToIconId()`, `ToModalPanelThemeType()`, `ToToastPanelThemeType()`, and stat `ToThemeType()`/`ToIconId()`.
- Metrics: `IsCompact(Vector2)`, `SafeMargin(bool)`, `MinimumTarget(bool)`, `IgnitionSize(bool)`, `TooltipMaximum(bool)`, `ModalWidth(SiriusModalSizeClass)`, `VerificationViewports`, and `FocusVerificationViewports`.
- Motion: entry, exit, reduced-opacity durations and transition/ease constants used by the showcase.

- [ ] **Step 1: Write the failing runtime-backed contract suite**

Create `tests/ui/theme/SiriusUiContractsTest.cs`:

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
        AssertThat(SiriusUiSeverity.Warning.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.WarningPanel);
        AssertThat(SiriusUiSeverity.Success.ToIconId()).IsEqual(UiIconId.Confirm);
        AssertThat(SiriusStatBarKind.Experience.ToThemeType())
            .IsEqual(SiriusThemeTypes.ExpBar);
        AssertThat(SiriusStatBarKind.Health.ToIconId()).IsEqual(UiIconId.Health);
        AssertThrown(() => ((SiriusUiSeverity)99).ToIconId())
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
        AssertThat(SiriusUiMetrics.FocusVerificationViewports).ContainsExactly(
            new Vector2I(640, 360), new Vector2I(1280, 720));
    }

    [TestCase]
    public void Motion_ContainsOnlyHpa377Durations()
    {
        AssertThat(SiriusMotion.EntrySeconds).IsEqualApprox(0.220);
        AssertThat(SiriusMotion.ExitSeconds).IsEqualApprox(0.180);
        AssertThat(SiriusMotion.ReducedOpacitySeconds).IsEqualApprox(0.100);
        AssertThat(SiriusMotion.Duration(true, true)).IsEqualApprox(0.100);
        AssertThat(SiriusMotion.UseTransform(true)).IsFalse();
    }
}
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
```

Expected: compile failure because the contracts do not exist.

- [ ] **Step 3: Implement stable Theme names**

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

- [ ] **Step 4: Implement three enums and exhaustive mappings**

Use these exact severity icons:

```text
Info -> UiIconId.Info
Success -> UiIconId.Confirm
Warning -> UiIconId.Warning
Error -> UiIconId.Error
```

Use Modal panel mapping:

```text
Info/Success -> SiriusModalPanel
Warning -> SiriusWarningPanel
Error -> SiriusErrorPanel
```

Use Toast panel mapping:

```text
Info -> SiriusContentPanel
Success -> SiriusFeaturePanel
Warning -> SiriusWarningPanel
Error -> SiriusErrorPanel
```

Use stat mappings:

```text
Health -> SiriusHpBar / UiIconId.Health
Mana -> SiriusMpBar / UiIconId.Mana
Experience -> SiriusExpBar / UiIconId.Experience
```

Every switch default throws `ArgumentOutOfRangeException`.

- [ ] **Step 5: Implement only consumed metrics and motion**

Do not add spacing, slot, control-feedback, screen-transition, or orrery constants. Define the values in the Interfaces block exactly. `SiriusMotion`:

```csharp
using Godot;

public static class SiriusMotion
{
    public const double EntrySeconds = 0.220;
    public const double ExitSeconds = 0.180;
    public const double ReducedOpacitySeconds = 0.100;
    public const Tween.TransitionType EntryTransition = Tween.TransitionType.Cubic;
    public const Tween.EaseType EntryEase = Tween.EaseType.Out;
    public const Tween.TransitionType ExitTransition = Tween.TransitionType.Quad;
    public const Tween.EaseType ExitEase = Tween.EaseType.In;

    public static double Duration(bool reducedMotion, bool entering) =>
        reducedMotion ? ReducedOpacitySeconds : entering ? EntrySeconds : ExitSeconds;

    public static bool UseTransform(bool reducedMotion) => !reducedMotion;
}
```

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
- Direct references to the five approved fonts.
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

Use direct ext-resources:

```text
res://assets/fonts/cinzel/Cinzel-Variable.ttf
res://assets/fonts/noto_sans/NotoSans-Regular.ttf
res://assets/fonts/noto_sans/NotoSans-Medium.ttf
res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf
res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf
```

Configure each Label variation with base type `Label`. Use Noto Sans fallback for the Cinzel `FontVariation`. Set tracked Telemetry and approved line spacing. Do not add Entity or HUD-specific roles.

- [ ] **Step 4: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
git add resources/ui/theme/SiriusTheme.tres tests/ui/theme/SiriusThemeTypographyTest.cs
git commit -m "feat: add Sirius palette and typography"
```

Expected: PASS.

---

### Task 3: Author Stock Control States, Surfaces, Bars, and Scrims

**Files:**
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeControlsTest.cs`

**Interfaces:**
- Six stock Button variations.
- Six panel surface variations plus two scrims.
- HP, MP, EXP, and internal Invalid bar variations.
- Tab, tooltip, and scrollbar base styling.

- [ ] **Step 1: Write failing resource tests**

Declare runtime support. Assert every Button variation has:

```text
normal hover pressed hover_pressed focus disabled
```

Assert all panel/bar/scrim variations and exact scrim alpha. Check required files directly:

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

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeControlsTest"
```

- [ ] **Step 3: Add conventional Button states**

Use Theme-owned StyleBoxes for normal, hover, pressed, hover-pressed, focus, and disabled states. Disabled text/icon alpha is 45% and disabled styles have no glow. Focus is cyan and does not change content margins. Selection uses stock pressed/toggled state plus gold treatment.

- [ ] **Step 4: Add stock Ignition**

Base `SiriusIgnitionButton` on Button. Reuse `ignition_seal.png` for state StyleBoxTextures with state-specific modulation and `focus_halo.png` for focus. Do not create a script or runtime fallback.

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

### Task 4: Implement the Static Modal Shell

**Files:**
- Create: `scenes/ui/components/SiriusModalShell.tscn`
- Create: `scripts/ui/components/SiriusModalShell.cs`
- Create: `tests/ui/components/SiriusModalShellTest.cs`

**Interfaces:**
- `Title`, `Severity`, `SizeClass`, `Compact`.
- `BodyHost`, `ActionsHost`, `RefreshPresentation(Vector2 availableSize)`.
- No scrim, close button/signal, Tween, host, focus, Cancel, dismissal, or domain ownership.

- [ ] **Step 1: Write failing tests**

Assert Error severity selects `SiriusErrorPanel` and Error icon; Small resolves to 420 at 1280×720; compact uses viewport minus 12 px on each side; title switches to compact variation; BodyHost and ActionsHost are exposed; `%Scrim`, `%CloseButton`, and Timer/Tween helper nodes are absent.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusModalShell : Control
└── Panel : PanelContainer [%Panel, ThemeTypeVariation=SiriusModalPanel]
    └── Margin : MarginContainer [24]
        └── RootLayout : VBoxContainer [separation=16]
            ├── Header : HBoxContainer [separation=8]
            │   ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            │   └── TitleLabel : Label [%TitleLabel]
            ├── BodyScroll : ScrollContainer [%BodyScroll]
            │   └── BodyHost : VBoxContainer [%BodyHost]
            └── ActionsHost : HBoxContainer [%ActionsHost, separation=8]
```

- [ ] **Step 4: Implement presentation**

```csharp
panel.ThemeTypeVariation = Severity.ToModalPanelThemeType();
title.Text = Title;
title.ThemeTypeVariation = Compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title;
UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
var width = Compact
    ? availableSize.X - SiriusUiMetrics.SafeMargin(true) * 2
    : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X * 0.90f);
panel.CustomMinimumSize = new Vector2(Mathf.Max(0, width), 0);
```

Setters call `RefreshPresentation(GetViewportRect().Size)` only when `IsNodeReady()`; tests call the explicit method with deterministic sizes.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
git add scenes/ui/components/SiriusModalShell.tscn \
  scripts/ui/components/SiriusModalShell.cs tests/ui/components/SiriusModalShellTest.cs
git commit -m "feat: add static Sirius modal shell"
```

Expected: PASS.

---

### Task 5: Implement the Fixed-Contract Stat Bar

**Files:**
- Create: `scenes/ui/components/SiriusStatBar.tscn`
- Create: `scripts/ui/components/SiriusStatBar.cs`
- Create: `tests/ui/components/SiriusStatBarTest.cs`

**Interfaces:**
- `Kind`, `Current`, `Maximum`, `Label`, `Compact`, `RefreshPresentation()`.
- Numeric value is always visible; low threshold is the internal constant `0.25`.

- [ ] **Step 1: Write failing edge-case tests**

Assert:

```text
Health 20/100 -> SiriusHpBar, value 20, text "20 / 100", state Low, Health icon
120/100 -> value 100, text "120 / 100", state Overflow
-5/100 -> value 0, text "-5 / 100", state Invalid value
10/0 -> SiriusInvalidBar, zero fill, text "10 / 0", state Invalid maximum
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

Use `private const double LowThreshold = 0.25;`. For valid maximum set `Bar.MaxValue=Maximum` and clamp only visual `Bar.Value`. Always preserve caller values in text. For `Maximum <= 0`, use range 0..1, zero fill, `SiriusInvalidBar`, and `Invalid maximum`. Apply state priority: invalid maximum, negative current, overflow, low, normal. Apply Kind icon and standard/compact nested Label variations.

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

### Task 6: Implement Input Hint and Context Prompt

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

Wrap `InputHintPresenter`. `Refresh()` calls `ResolveActions(Actions)`, applies the icon with `UiIconPresenter.Apply(TextureRect, ...)`, sets labels, and switches Metadata variations. Process input only while visible; connect `VisibilityChanged` in `_Ready()` and disconnect in `_ExitTree()`.

- [ ] **Step 4: Author ContextPrompt**

```text
SiriusContextPrompt : HBoxContainer [separation=8]
├── SemanticIcon : TextureRect [%SemanticIcon, 24×24]
├── PromptLabel : Label [%PromptLabel]
└── InputHint : SiriusInputHint [%InputHint]
```

`ShowIcon` is the authoritative inspector presence flag. `Refresh()` applies or clears the icon, sets Body standard/compact prompt variation, and propagates Actions/Compact to the child. It never discovers or invokes an interaction.

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

### Task 7: Implement the Static Toast Shell

**Files:**
- Create: `scenes/ui/components/SiriusToastShell.tscn`
- Create: `scripts/ui/components/SiriusToastShell.cs`
- Create: `tests/ui/components/SiriusToastShellTest.cs`

**Interfaces:**
- `Severity`, `Title`, `Message`, `Compact`, `RefreshPresentation()`.
- No Timer, Tween, queue, deduplication, host registration, acknowledgement, or retention.

- [ ] **Step 1: Write failing tests**

Assert Warning selects `SiriusWarningPanel` and Warning icon; title/message and compact variations update; no Timer, Tween helper, or scrim node exists.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusToastShell : Control
└── Panel : PanelContainer [%Panel, ThemeTypeVariation=SiriusContentPanel]
    └── Margin : MarginContainer [12]
        └── Row : HBoxContainer [separation=8]
            ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            └── TextColumn : VBoxContainer [separation=4]
                ├── TitleLabel : Label [%TitleLabel]
                └── MessageLabel : Label [%MessageLabel]
```

- [ ] **Step 4: Implement presentation**

```csharp
panel.ThemeTypeVariation = Severity.ToToastPanelThemeType();
title.Text = Title;
message.Text = Message;
title.ThemeTypeVariation = Compact ? SiriusThemeTypes.SectionCompact : SiriusThemeTypes.Section;
message.ThemeTypeVariation = Compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
```

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
git add scenes/ui/components/SiriusToastShell.tscn scripts/ui/components/SiriusToastShell.cs \
  tests/ui/components/SiriusToastShellTest.cs
git commit -m "feat: add static Sirius toast shell"
```

Expected: PASS.

---

### Task 8: Build the Isolated Showcase and Local Motion Demo

**Files:**
- Create: `scenes/ui/showcase/SiriusUiShowcase.tscn`
- Create: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseStructureTest.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseResponsiveTest.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseFocusTest.cs`

**Interfaces:**
- `PreviewViewport`, `PreviewRoot`, `Compact`, `ReducedMotion`.
- `SetPreviewSize(Vector2I)`, `SetReducedMotion(bool)`, `PlayMotionDemo()`.
- No background enum/selector and no reusable animation component.

- [ ] **Step 1: Write failing Structure tests**

Assert named sections:

```text
%PaletteSection %TypographySection %ButtonSection
%DarkSurfaceFixture %LightSurfaceFixture
%IgnitionStandardFixture %IgnitionCompactFixture
%SelectedFocusedFixture %LoadingFixture %TabsSection
%StatBarSection %InputHintSection %ContextPromptSection
%ToastSection %ModalSection %MotionSection
%MotionModalWrapper %MotionToastWrapper
```

Assert stock Button/Panel nodes use the required type variations; Loading is a disabled stock Primary Button labelled `Loading…`; the exact stress strings exist; no scenic-background selector or asset path is present.

- [ ] **Step 2: Write failing Responsive tests**

Create one fixture in `[BeforeTest]`, resize sequentially through all seven `VerificationViewports`, and at every size assert viewport size, single compact decision, safe margins, 1600 maximum frame, minimum target sizes, reachable primary examples, long-body wrapping/scrolling, and full metadata tooltip.

- [ ] **Step 3: Write failing Focus tests**

At 640×360 and 1280×720, push `ui_focus_next` through the preview. Assert the chain remains inside `PreviewRoot`, includes the selected-plus-focused toggle, and loops to the first focusable control. Do not repeat the same focus-tree test at other standard aspect ratios.

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
│   └── ReducedMotionToggle : CheckBox [%ReducedMotionToggle]
└── PreviewFrame : PanelContainer
    └── PreviewContainer : SubViewportContainer [%PreviewContainer]
        └── PreviewViewport : SubViewport [%PreviewViewport]
            └── PreviewRoot : Control [%PreviewRoot, Theme=SiriusTheme.tres]
                └── SafeFrame : MarginContainer [%SafeFrame]
                    └── ResponsiveScroll : ScrollContainer
                        └── ShowcaseContent : VBoxContainer [%ShowcaseContent]
```

In `%PaletteSection`, author both fixed fixtures:

```text
DarkSurfaceFixture: night-1000 ColorRect containing Content/Feature/HudPlate panels
LightSurfaceFixture: moon-50 ColorRect containing the same three panel variations
```

Use stock Buttons for all six button variations, Ignition, selected toggle, and Loading. Use the five real shared components for their sections.

- [ ] **Step 6: Implement compact propagation**

`SetPreviewSize()` updates the owned SubViewport, computes compact only from that size, and calls `ApplyCompactState()`. The compact method sets safe margins, max content width, free Label variations, component Compact properties, ordinary Button minimum targets, and Ignition size. It never inspects child rectangles to decide compact mode.

- [ ] **Step 7: Implement the local motion demonstration**

`PlayMotionDemo()` kills only the showcase's current Tween, resets wrapper alpha/position, and animates `%MotionModalWrapper` and `%MotionToastWrapper` using `SiriusMotion`. In reduced mode, wrappers start at zero translation and animate alpha only for 100 ms. In normal mode, use 12 px entry translation, 8 px exit translation, and the entry/exit constants. The static Modal/Toast components expose no motion API.

- [ ] **Step 8: Add exact stress fixtures and focus neighbours**

```text
Action: Bestätigungsaktion mit ausführlicher Beschreibung
Body: The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling.
Metadata: OBSERVATORY-CALIBRATION-IDENTIFIER-000000000000
```

Body uses WordSmart wrapping. Metadata clipping is limited to its fixture and exposes the full value through `TooltipText`. Set explicit next/previous focus paths; do not add a focus coordinator.

- [ ] **Step 9: Run, check size, and commit**

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

### Task 9: Add the Concise Guide and Run Final Verification

**Files:**
- Create: `docs/ui/hpa-377/README.md`
- Modify only when verification identifies a concrete defect: files created in Tasks 1–8

- [ ] **Step 1: Write the integration guide**

Use these sections:

```text
# Sirius Theme and Shared Components
Design
Opt in
Compact authority
Stock variations
Components
Handoffs
Prohibited patterns
```

Include:

```csharp
var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
screenRoot.Theme = theme;
button.ThemeTypeVariation = SiriusThemeTypes.PrimaryButton;
panel.ThemeTypeVariation = SiriusThemeTypes.ContentPanel;
```

State explicitly:

```text
Do not set ProjectSettings.gui/theme/custom during an isolated migration.
Do not create Button or Panel subclasses solely to assign a Theme variation.
Only a Viewport/SubViewport owner calls SiriusUiMetrics.IsCompact().
Ignition is SiriusIgnitionButton, not a component.
HPA-541 owns persisted reduced motion.
HPA-386 owns toast/reward queueing and short confirmation seals.
HPA-382 owns production modal lifecycle and dismissal.
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

Fail review if `project.godot`, a production screen/controller, `SiriusActionButton`, or `SiriusPanel` appears.

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