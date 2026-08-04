# Shared Sirius Theme, Core Components, and UI Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement HPA-377 as one opt-in Godot Theme resource, seven thin presentation components, an isolated showcase, and focused deterministic tests without introducing another lifecycle framework.

**Architecture:** `SiriusTheme.tres` is the single visual source of truth. Small C# contracts expose stable theme names, closed enum mappings, responsive metrics, and motion constants; components only map presentation state and compose scene-authored controls. `UIScreenHost`, settings persistence, notification queueing, navigation, focus restoration, and production-screen migration remain outside this work.

**Tech Stack:** Godot.NET SDK 4.6.2, Godot 4.6, C# 12, .NET 8, GdUnit4 5.0, `Sirius.sln`, `test.runsettings.local` locally and `test.runsettings` in CI.

## Global Constraints

- Do not modify `project.godot`, set `gui/theme/custom`, or opt any production screen into the Theme.
- Do not modify `MainMenu.tscn`, `Game.tscn`, `InventoryMenu.tscn`, `SettingsMenu.tscn`, `BattleScene.tscn`, or existing production controllers.
- Do not add an autoload, registry, coordinator, pure state model, lifecycle service, generic focus helper, or reusable loading state machine.
- Create exactly seven shared components: `SiriusActionButton`, `SiriusPanel`, `SiriusModalShell`, `SiriusStatBar`, `SiriusInputHint`, `SiriusContextPrompt`, and `SiriusToastShell`.
- Keep Ignition as a stock square `Button` using `SiriusIgnitionButton`; do not add an Ignition component.
- Shared stat kinds are only Health, Mana, and Experience. Automatic-action progress belongs to HPA-356.
- Shared panel surfaces are only Content, Feature, HudPlate, and Modal. Telemetry callouts and catalogue rails belong to their first consumers.
- `SiriusUiSeverity` is only Info, Success, Warning, and Error. Destructive is a button treatment, not a toast severity.
- Required committed fonts and ornaments fail resource-contract tests when absent; do not add runtime fallback branches for them.
- Only a `Viewport` or `SubViewport` owner computes compact mode. Controls in the same viewport inherit that decision.
- Components never read `GameManager`, `SaveManager`, `SettingsManager`, `RecoveryChest`, or `UIScreenHost`.
- Persisted reduced motion remains HPA-541 work. Toast/reward queueing and short confirmation seals remain HPA-386 work.
- Loading is a static showcase fixture only: a disabled Primary button labelled `Loading…`.
- Keep each HPA-377 test file below 500 lines. If a test needs re-entrant or teardown combinatorics, revisit the production abstraction instead of expanding the matrix.
- Use structural/resource assertions rather than pixel equality.
- Style only Label, Button, Panel/PanelContainer, ProgressBar, TabBar/TabContainer, TooltipPanel/TooltipLabel, ScrollContainer, HScrollBar, and VScrollBar.

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

tests/ui/showcase/SiriusUiShowcaseTest.cs
docs/ui/hpa-377/README.md
```

Modify:

```text
docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md
```

---

### Task 1: Approve the Design and Add Closed Contracts

**Files:**
- Modify: `docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md:3`
- Create: `scripts/ui/theme/SiriusThemeTypes.cs`
- Create: `scripts/ui/theme/SiriusUiTypes.cs`
- Create: `scripts/ui/theme/SiriusUiMetrics.cs`
- Create: `scripts/ui/theme/SiriusMotion.cs`
- Create: `tests/ui/theme/SiriusUiContractsTest.cs`

**Interfaces:**
- `SiriusThemeTypes.ResourcePath` and stable `StringName` fields for every variation.
- Closed enums: `SiriusActionButtonVariant`, `SiriusPanelSurface`, `SiriusUiSeverity`, `SiriusModalSizeClass`, `SiriusStatBarKind`.
- Exhaustive mappings: `ToThemeType()`, `ToIconId()`, `ToModalPanelThemeType()`, `ToToastPanelThemeType()`.
- Metrics: `IsCompact(Vector2)`, `SafeMargin(bool)`, `MinimumTarget(bool)`, `IgnitionSize(bool)`, `ModalWidth(SiriusModalSizeClass)`, `VerificationViewports`, `FullInteractionViewports`.
- Motion: named constants plus `EntrySeconds(bool)`, `ExitSeconds(bool)`, and `UseTransform(bool)`.

- [ ] **Step 1: Write failing contract tests**

Create `tests/ui/theme/SiriusUiContractsTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System;
using static GdUnit4.Assertions;

[TestSuite]
public partial class SiriusUiContractsTest
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
        AssertThat(SiriusUiSeverity.Success.ToToastPanelThemeType())
            .IsEqual(SiriusThemeTypes.FeaturePanel);
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
    public void Motion_UsesApprovedDurationsAndReducedLimit()
    {
        AssertThat(SiriusMotion.ControlFeedbackSeconds).IsEqualApprox(0.12, 0.0001);
        AssertThat(SiriusMotion.CalloutEntrySeconds).IsEqualApprox(0.22, 0.0001);
        AssertThat(SiriusMotion.CalloutExitSeconds).IsEqualApprox(0.18, 0.0001);
        AssertThat(SiriusMotion.ScreenTransitionSeconds).IsEqualApprox(0.28, 0.0001);
        AssertThat(SiriusMotion.OrreryMaximumSeconds).IsEqualApprox(0.40, 0.0001);
        AssertThat(SiriusMotion.EntrySeconds(true)).IsLessEqual(0.10);
        AssertThat(SiriusMotion.ExitSeconds(true)).IsLessEqual(0.10);
        AssertThat(SiriusMotion.CalloutExitSeconds)
            .IsLess(SiriusMotion.CalloutEntrySeconds);
    }
}
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
```

Expected: FAIL because the contract types do not exist.

- [ ] **Step 3: Create `SiriusThemeTypes.cs`**

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

    public static readonly StringName[] TypographyVariations =
    [
        Display, DisplayCompact, Title, TitleCompact, Section, SectionCompact,
        Body, BodyCompact, Metadata, MetadataCompact, Numeric, NumericCompact,
        Telemetry
    ];

    public static readonly StringName[] ActionButtonVariations =
    [PrimaryButton, SecondaryButton, TertiaryButton, WarningButton, DestructiveButton];
}
```

- [ ] **Step 4: Create `SiriusUiTypes.cs`**

```csharp
using Godot;
using System;

public enum SiriusActionButtonVariant { Primary, Secondary, Tertiary, Warning, Destructive }
public enum SiriusPanelSurface { Content, Feature, HudPlate, Modal }
public enum SiriusUiSeverity { Info, Success, Warning, Error }
public enum SiriusModalSizeClass { Small, Medium, Large }
public enum SiriusStatBarKind { Health, Mana, Experience }

public static class SiriusUiMappings
{
    public static StringName ToThemeType(this SiriusActionButtonVariant value) => value switch
    {
        SiriusActionButtonVariant.Primary => SiriusThemeTypes.PrimaryButton,
        SiriusActionButtonVariant.Secondary => SiriusThemeTypes.SecondaryButton,
        SiriusActionButtonVariant.Tertiary => SiriusThemeTypes.TertiaryButton,
        SiriusActionButtonVariant.Warning => SiriusThemeTypes.WarningButton,
        SiriusActionButtonVariant.Destructive => SiriusThemeTypes.DestructiveButton,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static StringName ToThemeType(this SiriusPanelSurface value) => value switch
    {
        SiriusPanelSurface.Content => SiriusThemeTypes.ContentPanel,
        SiriusPanelSurface.Feature => SiriusThemeTypes.FeaturePanel,
        SiriusPanelSurface.HudPlate => SiriusThemeTypes.HudPlate,
        SiriusPanelSurface.Modal => SiriusThemeTypes.ModalPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static UiIconId ToIconId(this SiriusUiSeverity value) => value switch
    {
        SiriusUiSeverity.Info => UiIconId.Info,
        SiriusUiSeverity.Success => UiIconId.Confirm,
        SiriusUiSeverity.Warning => UiIconId.Warning,
        SiriusUiSeverity.Error => UiIconId.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static StringName ToModalPanelThemeType(this SiriusUiSeverity value) => value switch
    {
        SiriusUiSeverity.Info or SiriusUiSeverity.Success => SiriusThemeTypes.ModalPanel,
        SiriusUiSeverity.Warning => SiriusThemeTypes.WarningPanel,
        SiriusUiSeverity.Error => SiriusThemeTypes.ErrorPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static StringName ToToastPanelThemeType(this SiriusUiSeverity value) => value switch
    {
        SiriusUiSeverity.Info or SiriusUiSeverity.Success => SiriusThemeTypes.FeaturePanel,
        SiriusUiSeverity.Warning => SiriusThemeTypes.WarningPanel,
        SiriusUiSeverity.Error => SiriusThemeTypes.ErrorPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static StringName ToThemeType(this SiriusStatBarKind value) => value switch
    {
        SiriusStatBarKind.Health => SiriusThemeTypes.HpBar,
        SiriusStatBarKind.Mana => SiriusThemeTypes.MpBar,
        SiriusStatBarKind.Experience => SiriusThemeTypes.ExpBar,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static UiIconId ToIconId(this SiriusStatBarKind value) => value switch
    {
        SiriusStatBarKind.Health => UiIconId.Health,
        SiriusStatBarKind.Mana => UiIconId.Mana,
        SiriusStatBarKind.Experience => UiIconId.Experience,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };
}
```

- [ ] **Step 5: Create metrics and motion files**

`SiriusUiMetrics.cs`:

```csharp
using Godot;

public static class SiriusUiMetrics
{
    public const int Space4 = 4, Space8 = 8, Space12 = 12, Space16 = 16;
    public const int Space24 = 24, Space32 = 32, Space48 = 48;
    public const int CompactWidth = 800, CompactHeight = 450;
    public const int StandardSafeMargin = 24, CompactSafeMargin = 12;
    public const int UltrawideContentMaximum = 1600;
    public const int StandardMinimumTarget = 44, CompactMinimumTarget = 40;
    public const int StandardSlot = 56, CompactSlot = 48;
    public const int StandardIgnition = 96, CompactIgnition = 80;
    public const int TooltipStandardMaximum = 360, TooltipCompactMaximum = 280;

    public static readonly Vector2I[] VerificationViewports =
    [
        new(640, 360), new(1024, 768), new(1280, 720), new(1440, 900),
        new(1920, 1080), new(2560, 1080), new(2560, 1440)
    ];

    public static readonly Vector2I[] FullInteractionViewports =
    [new(640, 360), new(1024, 768), new(1280, 720), new(2560, 1080)];

    public static bool IsCompact(Vector2 safeFrameSize) =>
        safeFrameSize.X < CompactWidth || safeFrameSize.Y < CompactHeight;
    public static int SafeMargin(bool compact) => compact ? CompactSafeMargin : StandardSafeMargin;
    public static Vector2 MinimumTarget(bool compact) =>
        Vector2.One * (compact ? CompactMinimumTarget : StandardMinimumTarget);
    public static Vector2 IgnitionSize(bool compact) =>
        Vector2.One * (compact ? CompactIgnition : StandardIgnition);
    public static int ModalWidth(SiriusModalSizeClass sizeClass) => sizeClass switch
    {
        SiriusModalSizeClass.Small => 420,
        SiriusModalSizeClass.Medium => 640,
        SiriusModalSizeClass.Large => 960,
        _ => throw new System.ArgumentOutOfRangeException(nameof(sizeClass), sizeClass, null)
    };
}
```

`SiriusMotion.cs`:

```csharp
public static class SiriusMotion
{
    public const double ControlFeedbackSeconds = 0.12;
    public const double CalloutEntrySeconds = 0.22;
    public const double CalloutExitSeconds = 0.18;
    public const double ScreenTransitionSeconds = 0.28;
    public const double OrreryMaximumSeconds = 0.40;
    public const double ReducedOpacitySeconds = 0.10;

    public static double EntrySeconds(bool reducedMotion) =>
        reducedMotion ? ReducedOpacitySeconds : CalloutEntrySeconds;
    public static double ExitSeconds(bool reducedMotion) =>
        reducedMotion ? ReducedOpacitySeconds : CalloutExitSeconds;
    public static bool UseTransform(bool reducedMotion) => !reducedMotion;
}
```

- [ ] **Step 6: Mark the design approved**

Replace the status line with:

```markdown
**Status:** Approved design
```

- [ ] **Step 7: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
git add docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md \
  scripts/ui/theme tests/ui/theme/SiriusUiContractsTest.cs
git commit -m "feat: define Sirius UI theme contracts"
```

Expected: focused tests PASS.

---

### Task 2: Author Palette and Typography in `SiriusTheme.tres`

**Files:**
- Create: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeTypographyTest.cs`

**Interfaces:**
- A loadable `Theme` at `SiriusThemeTypes.ResourcePath`.
- Thirteen Label variations with exact base type, font, size, color, tracking, line spacing, and fallback behavior.

- [ ] **Step 1: Write failing resource tests**

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusThemeTypographyTest : Node
{
    private static Theme LoadTheme() =>
        ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath)!;

    [TestCase]
    public void Theme_LoadsWithApprovedDefaultFont()
    {
        var theme = LoadTheme();
        AssertThat(theme).IsNotNull();
        AssertThat(theme.DefaultFont.ResourcePath)
            .IsEqual("res://assets/fonts/noto_sans/NotoSans-Regular.ttf");
        AssertThat(theme.DefaultFontSize).IsEqual(16);
    }

    [TestCase]
    public void TypographyVariations_HaveExactBasesAndSizes()
    {
        var theme = LoadTheme();
        AssertLabel(theme, SiriusThemeTypes.Display, 44);
        AssertLabel(theme, SiriusThemeTypes.DisplayCompact, 30);
        AssertLabel(theme, SiriusThemeTypes.Title, 32);
        AssertLabel(theme, SiriusThemeTypes.TitleCompact, 24);
        AssertLabel(theme, SiriusThemeTypes.Section, 20);
        AssertLabel(theme, SiriusThemeTypes.SectionCompact, 17);
        AssertLabel(theme, SiriusThemeTypes.Body, 16);
        AssertLabel(theme, SiriusThemeTypes.BodyCompact, 14);
        AssertLabel(theme, SiriusThemeTypes.Metadata, 14);
        AssertLabel(theme, SiriusThemeTypes.MetadataCompact, 12);
        AssertLabel(theme, SiriusThemeTypes.Numeric, 16);
        AssertLabel(theme, SiriusThemeTypes.NumericCompact, 14);
        AssertLabel(theme, SiriusThemeTypes.Telemetry, 12);
    }

    [TestCase]
    public void TypographyVariations_UseApprovedFontFamilies()
    {
        var theme = LoadTheme();
        AssertBaseFont(theme, SiriusThemeTypes.Display,
            "res://assets/fonts/cinzel/Cinzel-Variable.ttf");
        AssertBaseFont(theme, SiriusThemeTypes.Title,
            "res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf");
        AssertBaseFont(theme, SiriusThemeTypes.Body,
            "res://assets/fonts/noto_sans/NotoSans-Regular.ttf");
        AssertBaseFont(theme, SiriusThemeTypes.Numeric,
            "res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf");
        var display = (FontVariation)theme.GetFont("font", SiriusThemeTypes.Display);
        AssertThat(display.Fallbacks.Count).IsGreater(0);
        AssertThat(display.Fallbacks[0].ResourcePath)
            .IsEqual("res://assets/fonts/noto_sans/NotoSans-Regular.ttf");
        var telemetry = (FontVariation)theme.GetFont("font", SiriusThemeTypes.Telemetry);
        AssertThat(telemetry.SpacingGlyph).IsEqual(1);
    }

    private static void AssertLabel(Theme theme, StringName type, int size)
    {
        AssertThat(theme.IsTypeVariation(type, "Label")).IsTrue();
        AssertThat(theme.GetFontSize("font_size", type)).IsEqual(size);
        AssertThat(theme.HasFont("font", type)).IsTrue();
        AssertThat(theme.HasColor("font_color", type)).IsTrue();
    }

    private static void AssertBaseFont(Theme theme, StringName type, string path)
    {
        var variation = (FontVariation)theme.GetFont("font", type);
        AssertThat(variation.BaseFont.ResourcePath).IsEqual(path);
    }
}
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
```

Expected: FAIL because the Theme resource does not exist.

- [ ] **Step 3: Create direct font resources and variations**

Set the Theme defaults:

```text
default_font = res://assets/fonts/noto_sans/NotoSans-Regular.ttf
default_font_size = 16
```

Create reusable `FontVariation` resources:

```text
Display: Cinzel-Variable.ttf, wght=600, NotoSans-Regular.ttf fallback
Title/Section: NotoSans-SemiBold.ttf
Body: NotoSans-Regular.ttf, spacing_top=3, spacing_bottom=3
Control: NotoSans-Medium.ttf
Metadata: NotoSans-Regular.ttf
Numeric: NotoSansMono-Medium.ttf, OpenType tnum=1
Telemetry: NotoSansMono-Medium.ttf, OpenType tnum=1, spacing_glyph=1
```

- [ ] **Step 4: Add exact Label variations**

Every variation has base `Label`:

```text
SiriusDisplay          44  Display    #F7F5FF
SiriusDisplayCompact   30  Display    #F7F5FF
SiriusTitle            32  Title      #F7F5FF
SiriusTitleCompact     24  Title      #F7F5FF
SiriusSection          20  Title      #F7F5FF
SiriusSectionCompact   17  Title      #F7F5FF
SiriusBody             16  Body       #F7F5FF
SiriusBodyCompact      14  Body       #F7F5FF
SiriusMetadata         14  Metadata   #C7CEE8
SiriusMetadataCompact  12  Metadata   #C7CEE8
SiriusNumeric          16  Numeric    #F7F5FF
SiriusNumericCompact   14  Numeric    #F7F5FF
SiriusTelemetry        12  Telemetry  #8F9AB8
```

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
git add resources/ui/theme/SiriusTheme.tres \
  tests/ui/theme/SiriusThemeTypographyTest.cs
git commit -m "feat: add Sirius typography theme"
```

Expected: PASS.

---

### Task 3: Add Interactive Controls, Ignition, Surfaces, Bars, Tabs, Tooltips, Scrollbars, and Scrims

**Files:**
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeControlsTest.cs`

**Interfaces:**
- Button state names: `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, `disabled`.
- ProgressBar style names: `background`, `fill`.
- Scrim variations: `SiriusScrim`, `SiriusChildScrim`, both based on `Panel`.

- [ ] **Step 1: Write failing control-resource tests**

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusThemeControlsTest : Node
{
    private static readonly StringName[] ButtonStates =
        ["normal", "hover", "pressed", "hover_pressed", "focus", "disabled"];
    private static Theme LoadTheme() =>
        ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath)!;

    [TestCase]
    public void ActionButtonVariations_DefineAllNativeStates()
    {
        var theme = LoadTheme();
        foreach (var type in SiriusThemeTypes.ActionButtonVariations)
        {
            AssertThat(theme.IsTypeVariation(type, "Button")).IsTrue();
            foreach (var state in ButtonStates)
                AssertThat(theme.HasStylebox(state, type)).IsTrue();
        }
    }

    [TestCase]
    public void Ignition_UsesCommittedSealAndFocusTextures()
    {
        var theme = LoadTheme();
        AssertThat(theme.IsTypeVariation(SiriusThemeTypes.IgnitionButton, "Button")).IsTrue();
        StringName[] states = ["normal", "hover", "pressed", "hover_pressed", "disabled"];
        foreach (var state in states)
        {
            var style = (StyleBoxTexture)theme.GetStylebox(state, SiriusThemeTypes.IgnitionButton);
            AssertThat(style.Texture.ResourcePath)
                .IsEqual("res://assets/sprites/ui/ornaments/ignition_seal.png");
        }
        var focus = (StyleBoxTexture)theme.GetStylebox("focus", SiriusThemeTypes.IgnitionButton);
        AssertThat(focus.Texture.ResourcePath)
            .IsEqual("res://assets/sprites/ui/ornaments/focus_halo.png");
    }

    [TestCase]
    public void PanelsBarsAndScrims_UseApprovedContracts()
    {
        var theme = LoadTheme();
        StringName[] panels =
        [
            SiriusThemeTypes.ContentPanel, SiriusThemeTypes.FeaturePanel,
            SiriusThemeTypes.HudPlate, SiriusThemeTypes.ModalPanel,
            SiriusThemeTypes.WarningPanel, SiriusThemeTypes.ErrorPanel
        ];
        foreach (var panel in panels)
        {
            AssertThat(theme.IsTypeVariation(panel, "PanelContainer")).IsTrue();
            AssertThat(theme.HasStylebox("panel", panel)).IsTrue();
        }
        StringName[] bars =
        [SiriusThemeTypes.HpBar, SiriusThemeTypes.MpBar, SiriusThemeTypes.ExpBar, SiriusThemeTypes.InvalidBar];
        foreach (var bar in bars)
        {
            AssertThat(theme.IsTypeVariation(bar, "ProgressBar")).IsTrue();
            AssertThat(theme.HasStylebox("background", bar)).IsTrue();
            AssertThat(theme.HasStylebox("fill", bar)).IsTrue();
        }
        AssertScrim(theme, SiriusThemeTypes.Scrim, 0.58f);
        AssertScrim(theme, SiriusThemeTypes.ChildScrim, 0.72f);
    }

    [TestCase]
    public void TabsTooltipsAndScrollbars_HaveRequiredBaseItems()
    {
        var theme = LoadTheme();
        AssertThat(theme.HasStylebox("tab_selected", "TabBar")).IsTrue();
        AssertThat(theme.HasStylebox("tab_unselected", "TabBar")).IsTrue();
        AssertThat(theme.HasStylebox("focus", "TabBar")).IsTrue();
        AssertThat(theme.HasStylebox("panel", "TooltipPanel")).IsTrue();
        AssertThat(theme.HasColor("font_color", "TooltipLabel")).IsTrue();
        AssertThat(theme.HasStylebox("scroll", "VScrollBar")).IsTrue();
        AssertThat(theme.HasStylebox("grabber", "VScrollBar")).IsTrue();
        AssertThat(theme.HasStylebox("scroll", "HScrollBar")).IsTrue();
        AssertThat(theme.HasStylebox("grabber", "HScrollBar")).IsTrue();
    }

    private static void AssertScrim(Theme theme, StringName type, float alpha)
    {
        AssertThat(theme.IsTypeVariation(type, "Panel")).IsTrue();
        var style = (StyleBoxFlat)theme.GetStylebox("panel", type);
        AssertThat(style.BgColor.A).IsEqualApprox(alpha, 0.001f);
    }
}
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeControlsTest"
```

- [ ] **Step 3: Add shared palette-backed StyleBoxes**

Use only:

```text
#050714 #0D1530 #18234A #27366C #F7F5FF #C7CEE8 #8F9AB8
#62DCFF #F5D784 #DFAE43 #F1B85B #F16D83
```

Geometry:

```text
control radius 8
panel radius 12
feature radius 16
normal border 1
focus border 2
focus expand margin 2
pressed content offset 1 down
```

- [ ] **Step 4: Add five conventional Button variations**

```text
Primary: gold-500 normal; gold-300 hover; gold-500 pressed; night text
Secondary: indigo-800 normal; indigo-700 hover; night-900 pressed; moon text
Tertiary: transparent normal; indigo-800 hover; night-900 pressed; moon text
Warning: night-900 with warning border; indigo-800 hover; warning marker
Destructive: night-900 with danger border; danger fill only when pressed
```

Every variation includes all six native states. Focus is an independent cyan ring. Disabled uses 45% opacity and no glow. `hover_pressed` uses pressed geometry plus hover emphasis.

- [ ] **Step 5: Add stock Ignition variation**

```text
base type: Button
seal texture: res://assets/sprites/ui/ornaments/ignition_seal.png
focus texture: res://assets/sprites/ui/ornaments/focus_halo.png
content inset: 16 px
normal modulate: gold-500 at 92%
hover: gold-300 at 100%
pressed: gold-500 at 100% + 1 px depression
hover_pressed: gold-300 at 100% + 1 px depression
disabled: moon-400 at 45%, no glow
focus expand margin: 6 px
```

Use one `StyleBoxTexture` per state; there is no runtime fallback.

- [ ] **Step 6: Add panels, scrims, and bars**

```text
SiriusContentPanel: night-900 90%, radius 12
SiriusFeaturePanel: indigo-800 96%, radius 16, soft shadow
SiriusHudPlate: night-900 82%, radius 12
SiriusModalPanel: night-900 96%, radius 12, soft shadow
SiriusWarningPanel: Modal geometry + warning border
SiriusErrorPanel: Modal geometry + danger border
SiriusScrim: night-1000 alpha 0.58
SiriusChildScrim: night-1000 alpha 0.72
SiriusHpBar fill: #F16D83
SiriusMpBar fill: #62DCFF
SiriusExpBar fill: #F5D784
SiriusInvalidBar fill: #8F9AB8
```

ProgressBar track is `night-1000`, radius 4.

- [ ] **Step 7: Add TabBar, tooltip, and scrollbar base items**

```text
TabBar: quiet unselected, indigo hover, gold selected, cyan focus
TooltipPanel: night-900 96%, radius 8, moon-400 border
TooltipLabel: Noto Sans 14, moon-50
H/VScrollBar: night scroll, indigo grabber, cyan highlight, gold pressed
```

- [ ] **Step 8: Run Theme tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusTheme"
git add resources/ui/theme/SiriusTheme.tres \
  tests/ui/theme/SiriusThemeControlsTest.cs
git commit -m "feat: add Sirius control theme"
```

Expected: all Theme tests PASS.

---

### Task 4: Implement `SiriusActionButton` and `SiriusPanel`

**Files:**
- Create: `scripts/ui/components/SiriusActionButton.cs`
- Create: `scripts/ui/components/SiriusPanel.cs`
- Create: `tests/ui/components/SiriusComponentTestSupport.cs`
- Create: `tests/ui/components/SiriusActionButtonTest.cs`
- Create: `tests/ui/components/SiriusPanelTest.cs`

**Interfaces:**
- `SiriusActionButton`: `Variant`, `ShowIcon`, `IconId`, `IconSize`, `DisabledReason`.
- `SiriusPanel`: `Surface`.
- No compact, loading, navigation, async, or host API.

- [ ] **Step 1: Add test support**

```csharp
using Godot;
using System.Threading.Tasks;

public static class SiriusComponentTestSupport
{
    public static async Task<T> AddToRoot<T>(T node) where T : Node
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        return node;
    }

    public static async Task<T> Instantiate<T>(string path) where T : Node
    {
        var packed = ResourceLoader.Load<PackedScene>(path)!;
        return await AddToRoot(packed.Instantiate<T>());
    }

    public static async Task Free(Node? node)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        if (node != null && GodotObject.IsInstanceValid(node))
            node.Free();
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
```

- [ ] **Step 2: Write failing component tests**

`SiriusActionButtonTest.cs` must assert:

```csharp
_button = await SiriusComponentTestSupport.AddToRoot(new SiriusActionButton
{
    Variant = SiriusActionButtonVariant.Warning,
    ShowIcon = true,
    IconId = UiIconId.Warning,
    IconSize = UiIconSize.Metadata,
    DisabledReason = "Requires a valid target"
});
AssertThat(_button.ThemeTypeVariation).IsEqual(SiriusThemeTypes.WarningButton);
AssertThat(_button.Icon!.ResourcePath)
    .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Warning, UiIconSize.Metadata));
AssertThat(_button.TooltipText).IsEqual("Requires a valid target");
```

Also assert stock `ToggleMode`/`ButtonPressed` remains unchanged.

`SiriusPanelTest.cs` must instantiate `Surface=HudPlate` and assert `ThemeTypeVariation == SiriusThemeTypes.HudPlate`.

- [ ] **Step 3: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
```

- [ ] **Step 4: Implement `SiriusActionButton`**

```csharp
using Godot;

[Tool]
public partial class SiriusActionButton : Button
{
    private SiriusActionButtonVariant _variant;
    private bool _showIcon;
    private UiIconId _iconId;
    private UiIconSize _iconSize = UiIconSize.Default;
    private string _disabledReason = string.Empty;

    [Export] public SiriusActionButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; ApplyPresentation(); }
    }
    [Export] public bool ShowIcon
    {
        get => _showIcon;
        set { _showIcon = value; ApplyPresentation(); }
    }
    [Export] public UiIconId IconId
    {
        get => _iconId;
        set { _iconId = value; ApplyPresentation(); }
    }
    [Export] public UiIconSize IconSize
    {
        get => _iconSize;
        set { _iconSize = value; ApplyPresentation(); }
    }
    [Export(PropertyHint.MultilineText)] public string DisabledReason
    {
        get => _disabledReason;
        set { _disabledReason = value ?? string.Empty; ApplyPresentation(); }
    }

    public override void _Ready() => ApplyPresentation();

    private void ApplyPresentation()
    {
        ThemeTypeVariation = Variant.ToThemeType();
        TooltipText = string.IsNullOrWhiteSpace(DisabledReason)
            ? string.Empty
            : DisabledReason;
        if (ShowIcon)
            UiIconPresenter.Apply(this, IconId, IconSize);
        else
            Icon = null;
    }
}
```

- [ ] **Step 5: Implement `SiriusPanel`**

```csharp
using Godot;

[Tool]
public partial class SiriusPanel : PanelContainer
{
    private SiriusPanelSurface _surface;
    [Export] public SiriusPanelSurface Surface
    {
        get => _surface;
        set { _surface = value; ApplySurface(); }
    }
    public override void _Ready() => ApplySurface();
    private void ApplySurface() => ThemeTypeVariation = Surface.ToThemeType();
}
```

- [ ] **Step 6: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
git add scripts/ui/components/SiriusActionButton.cs scripts/ui/components/SiriusPanel.cs \
  tests/ui/components
git commit -m "feat: add Sirius action and panel components"
```

Expected: PASS.

---

### Task 5: Implement the Scene-Authored Modal Shell

**Files:**
- Create: `scenes/ui/components/SiriusModalShell.tscn`
- Create: `scripts/ui/components/SiriusModalShell.cs`
- Create: `tests/ui/components/SiriusModalShellTest.cs`

**Interfaces:**
- `Title`, `Severity`, `SizeClass`, `Compact`, `ReducedMotion`, `ShowCloseAffordance`.
- `BodyHost`, `ActionsHost`, `CloseRequested`, `RefreshPresentation()`, `RefreshPresentation(Vector2)`, `PlayEntry()`, `PlayExit()`.
- No scrim, host, focus, Cancel, dismissal, or domain action ownership.

- [ ] **Step 1: Write failing tests**

Test Error severity, Small width, compact title variation, icon path, body/actions getters, and absence of `%Scrim`.

```csharp
_shell = await SiriusComponentTestSupport.Instantiate<SiriusModalShell>(
    "res://scenes/ui/components/SiriusModalShell.tscn");
_shell.Title = "Cannot load save";
_shell.Severity = SiriusUiSeverity.Error;
_shell.SizeClass = SiriusModalSizeClass.Small;
_shell.RefreshPresentation(new Vector2(1280, 720));
AssertThat(_shell.GetNode<PanelContainer>("%Panel").ThemeTypeVariation)
    .IsEqual(SiriusThemeTypes.ErrorPanel);
AssertThat(_shell.GetNode<Label>("%TitleLabel").Text).IsEqual("Cannot load save");
AssertThat(_shell.GetNode<TextureRect>("%SeverityIcon").Texture!.ResourcePath)
    .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Error, UiIconSize.Default));
AssertThat(_shell.GetNodeOrNull<Panel>("%Scrim")).IsNull();
```

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusModalShell : Control
└── Panel : SiriusPanel [%Panel, Surface=Modal]
    └── Margin : MarginContainer [24 px]
        └── RootLayout : VBoxContainer [separation=16]
            ├── Header : HBoxContainer
            │   ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            │   ├── TitleLabel : Label [%TitleLabel]
            │   └── CloseButton : SiriusActionButton [%CloseButton, Tertiary]
            ├── BodyScroll : ScrollContainer [%BodyScroll]
            │   └── BodyHost : VBoxContainer [%BodyHost]
            └── ActionsHost : HBoxContainer [%ActionsHost, separation=8, alignment=End]
```

No scrim node.

- [ ] **Step 4: Implement presentation and visual-only motion**

Required behavior:

```csharp
panel.ThemeTypeVariation = Severity.ToModalPanelThemeType();
title.Text = Title;
title.ThemeTypeVariation = Compact ? SiriusThemeTypes.TitleCompact : SiriusThemeTypes.Title;
UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
close.Visible = ShowCloseAffordance;
var width = Compact
    ? availableSize.X - SiriusUiMetrics.CompactSafeMargin * 2
    : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X * 0.90f);
panel.CustomMinimumSize = new Vector2(Mathf.Max(0, width), 0);
```

`PlayEntry()` animates alpha and, only in normal motion, a 12 px offset. `PlayExit()` animates alpha and, only in normal motion, an 8 px offset; it hides after finishing. Kill only the component's current Tween before starting another. Do not queue-free the node.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
git add scenes/ui/components/SiriusModalShell.tscn \
  scripts/ui/components/SiriusModalShell.cs \
  tests/ui/components/SiriusModalShellTest.cs
git commit -m "feat: add Sirius modal shell"
```

Expected: PASS.

---

### Task 6: Implement the Scene-Authored Stat Bar

**Files:**
- Create: `scenes/ui/components/SiriusStatBar.tscn`
- Create: `scripts/ui/components/SiriusStatBar.cs`
- Create: `tests/ui/components/SiriusStatBarTest.cs`

**Interfaces:**
- `Kind`, `Current`, `Maximum`, `Label`, `ShowNumericValue`, `LowThreshold`, `Compact`, `RefreshPresentation()`.
- Visual fill clamps; displayed values preserve caller data.

- [ ] **Step 1: Write failing tests**

Assert:

```text
Health 20/100 -> HpBar, fill 20, state Low, Health icon
120/100 -> fill 100, text "120 / 100", state Overflow
-5/100 -> fill 0, state Invalid value
10/0 -> InvalidBar, state Invalid maximum
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

- [ ] **Step 4: Implement deterministic state rules**

```csharp
if (Maximum <= 0)
{
    bar.MinValue = 0;
    bar.MaxValue = 1;
    bar.Value = 0;
    bar.ThemeTypeVariation = SiriusThemeTypes.InvalidBar;
    SetState(state, "Invalid maximum");
    return;
}
bar.MinValue = 0;
bar.MaxValue = Maximum;
bar.Value = Mathf.Clamp((float)Current, 0, (float)Maximum);
bar.ThemeTypeVariation = Kind.ToThemeType();
if (Current < 0) SetState(state, "Invalid value");
else if (Current > Maximum) SetState(state, "Overflow");
else if (Current / Maximum <= LowThreshold) SetState(state, "Low");
else SetState(state, string.Empty);
```

Always set value text to `$"{Current:0.##} / {Maximum:0.##}"`, icon from `Kind.ToIconId()`, and standard/compact Label variations. Default `ShowNumericValue=true`, `LowThreshold=0.25`.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusStatBarTest"
git add scenes/ui/components/SiriusStatBar.tscn \
  scripts/ui/components/SiriusStatBar.cs \
  tests/ui/components/SiriusStatBarTest.cs
git commit -m "feat: add Sirius stat bar"
```

Expected: PASS.

---

### Task 7: Implement Input Hints and Context Prompts

**Files:**
- Create: `scenes/ui/components/SiriusInputHint.tscn`
- Create: `scripts/ui/components/SiriusInputHint.cs`
- Create: `scenes/ui/components/SiriusContextPrompt.tscn`
- Create: `scripts/ui/components/SiriusContextPrompt.cs`
- Create: `tests/ui/components/SiriusInputHintTest.cs`
- Create: `tests/ui/components/SiriusContextPromptTest.cs`

**Interfaces:**
- `SiriusInputHint`: `Prompt`, `Actions`, `Compact`, `ActiveDevice`, `Observe(InputEvent)`, `Refresh()`.
- `SiriusContextPrompt`: `ShowIcon`, `IconId`, `Prompt`, `Actions`, `Compact`, `Refresh()`.

- [ ] **Step 1: Write failing tests**

Use a temporary InputMap action and restore it in `finally`. Assert Keyboard K, Mouse 1, Gamepad A, and Unbound labels. ContextPrompt must assert Dialogue icon, `Talk` prompt, and `interact` action propagation.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
```

- [ ] **Step 3: Author `SiriusInputHint.tscn`**

```text
SiriusInputHint : HBoxContainer [separation=4]
├── DeviceIcon : TextureRect [%DeviceIcon, 16×16]
├── PromptLabel : Label [%PromptLabel]
└── BindingLabel : Label [%BindingLabel]
```

- [ ] **Step 4: Implement `SiriusInputHint`**

```csharp
private readonly InputHintPresenter _presenter = new();
private StringName[] _actions = [];
public UiInputDevice ActiveDevice => _presenter.ActiveDevice;
public StringName[] Actions
{
    get => _actions;
    set { _actions = value ?? []; Refresh(); }
}
public bool Observe(InputEvent inputEvent)
{
    var changed = _presenter.Observe(inputEvent);
    if (changed) Refresh();
    return changed;
}
public void Refresh()
{
    if (!IsNodeReady()) return;
    var hint = _presenter.ResolveActions(Actions);
    GetNode<Label>("%PromptLabel").Text = Prompt;
    GetNode<Label>("%BindingLabel").Text = hint.BindingLabel;
    GetNode<Label>("%PromptLabel").ThemeTypeVariation =
        Compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    GetNode<Label>("%BindingLabel").ThemeTypeVariation =
        Compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    UiIconPresenter.Apply(GetNode<TextureRect>("%DeviceIcon"), hint.IconId, UiIconSize.Metadata);
}
```

Process input only while visible. Connect `VisibilityChanged` in `_Ready()`, call `SetProcessInput(IsVisibleInTree())`, and disconnect in `_ExitTree()`. `_Input()` calls `Observe()` only while visible.

- [ ] **Step 5: Author and implement `SiriusContextPrompt`**

```text
SiriusContextPrompt : HBoxContainer [separation=8]
├── SemanticIcon : TextureRect [%SemanticIcon, 24×24]
├── PromptLabel : Label [%PromptLabel]
└── InputHint : SiriusInputHint [%InputHint]
```

`Refresh()` sets optional icon, Body standard/compact prompt variation, child actions, and child compact state. It never discovers targets or invokes an interaction.

- [ ] **Step 6: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
git add scenes/ui/components/SiriusInputHint.tscn \
  scenes/ui/components/SiriusContextPrompt.tscn \
  scripts/ui/components/SiriusInputHint.cs \
  scripts/ui/components/SiriusContextPrompt.cs \
  tests/ui/components/SiriusInputHintTest.cs \
  tests/ui/components/SiriusContextPromptTest.cs
git commit -m "feat: add Sirius input and context prompts"
```

Expected: PASS.

---

### Task 8: Implement the Visual Toast Shell

**Files:**
- Create: `scenes/ui/components/SiriusToastShell.tscn`
- Create: `scripts/ui/components/SiriusToastShell.cs`
- Create: `tests/ui/components/SiriusToastShellTest.cs`

**Interfaces:**
- `Severity`, `Title`, `Message`, `Compact`, `ReducedMotion`, `RefreshPresentation()`, `PlayEntry()`, `PlayExit()`.
- No Timer, queue, deduplication, host registration, acknowledgement, or transition-retention logic.

- [ ] **Step 1: Write failing tests**

Assert Warning mapping, title/message text, compact variations, absence of a Timer, and reduced-motion entry starting with zero translation.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

- [ ] **Step 3: Author the scene**

```text
SiriusToastShell : Control
└── Panel : SiriusPanel [%Panel, Surface=Feature]
    └── Margin : MarginContainer [12 px]
        └── Row : HBoxContainer [separation=8]
            ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            └── TextColumn : VBoxContainer [separation=4]
                ├── TitleLabel : Label [%TitleLabel]
                └── MessageLabel : Label [%MessageLabel]
```

No Timer node.

- [ ] **Step 4: Implement presentation and visual-only motion**

```csharp
panel.ThemeTypeVariation = Severity.ToToastPanelThemeType();
title.Text = Title;
message.Text = Message;
title.ThemeTypeVariation = Compact ? SiriusThemeTypes.SectionCompact : SiriusThemeTypes.Section;
message.ThemeTypeVariation = Compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
```

Motion mirrors ModalShell: alpha plus 12 px entry/8 px exit only in normal motion. Do not hide on timeout, queue-free, or register with a host.

- [ ] **Step 5: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
git add scenes/ui/components/SiriusToastShell.tscn \
  scripts/ui/components/SiriusToastShell.cs \
  tests/ui/components/SiriusToastShellTest.cs
git commit -m "feat: add Sirius toast shell"
```

Expected: PASS.

---

### Task 9: Build the Isolated Showcase and Reused-Fixture Viewport Matrix

**Files:**
- Create: `scenes/ui/showcase/SiriusUiShowcase.tscn`
- Create: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseTest.cs`

**Interfaces:**
- `PreviewViewport`, `PreviewRoot`, `Compact`, `SetPreviewSize(Vector2I)`, `SetBackground(SiriusShowcaseBackground)`, `SetReducedMotion(bool)`.
- Compact mode is computed only from the owned SubViewport.

- [ ] **Step 1: Write failing showcase tests**

Use one fixture in `[BeforeTest]`. Resize it sequentially through `VerificationViewports`. At every size assert viewport size, compact state, safe-frame width, primary target size, reachable content, and no missing required resource. Assert named fixtures:

```text
%PaletteSection %TypographySection %ButtonSection
%IgnitionStandardFixture %IgnitionCompactFixture
%SelectedFocusedFixture %LoadingFixture %TabsSection
%StatBarSection %InputHintSection %ContextPromptSection
%ToastSection %ModalSection %MotionSection
```

At the four `FullInteractionViewports`, push `ui_focus_next` through `PreviewViewport` and assert the explicit chain remains inside `PreviewRoot`.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcaseTest"
```

- [ ] **Step 3: Author the showcase root**

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

Author the named sections with real shared components, stock Ignition Button, stock toggle Button, TabContainer, tooltips, and both scrim Panel variations. Loading fixture:

```text
SiriusActionButton Variant=Primary Text="Loading…" Disabled=true DisabledReason="Please wait"
```

- [ ] **Step 4: Implement deterministic background and viewport controls**

```csharp
public enum SiriusShowcaseBackground
{
    NightSolid,
    MoonSolid,
    MainMenuScenic,
    BattleScenic
}
```

Map backgrounds exactly:

```text
NightSolid -> #050714
MoonSolid -> #F7F5FF
MainMenuScenic -> res://assets/sprites/ui/ui_main_menu_background.png
BattleScenic -> res://assets/sprites/ui/ui_battle_background.png
```

`SetPreviewSize()`:

```csharp
PreviewViewport.Size = size;
GetNode<SubViewportContainer>("%PreviewContainer").CustomMinimumSize = size;
Compact = SiriusUiMetrics.IsCompact(size);
ApplyCompactState();
```

`ApplyCompactState()` sets safe margins, 1600 max content width, component Compact flags, free Label variations, ordinary button minimum targets, and Ignition sizes. It never reads a child rectangle to decide compact mode.

- [ ] **Step 5: Add deterministic stress fixtures**

```text
Action: Bestätigungsaktion mit ausführlicher Beschreibung
Body: The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling.
Metadata: OBSERVATORY-CALIBRATION-IDENTIFIER-000000000000
```

Body uses WordSmart wrapping. Metadata may clip only in its fixture and exposes the full value through TooltipText.

- [ ] **Step 6: Add explicit focus order**

Assign `FocusNeighborNext` and `FocusNeighborPrevious` through button variants, toggle, tabs, input fixtures, and modal actions, looping to the first control. Do not add a focus coordinator.

- [ ] **Step 7: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcaseTest"
git add scenes/ui/showcase/SiriusUiShowcase.tscn \
  scripts/ui/showcase/SiriusUiShowcase.cs \
  tests/ui/showcase/SiriusUiShowcaseTest.cs
git commit -m "feat: add Sirius UI showcase"
```

Expected: PASS with one instance resized through seven viewports.

---

### Task 10: Add the Concise Integration Guide and Run Final Verification

**Files:**
- Create: `docs/ui/hpa-377/README.md`
- Modify only if validation finds a concrete defect: files created in Tasks 1–9

**Interfaces:**
- A short usage guide linking to the approved design.
- Final focused/build/full-suite evidence.

- [ ] **Step 1: Write the integration guide**

Use these exact sections:

```text
# Sirius Theme and Shared Components
Design link
Opt in
Compact authority
Components
Handoffs
Prohibited patterns
```

Include the opt-in code:

```csharp
var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
screenRoot.Theme = theme;
```

State explicitly:

```text
Do not set ProjectSettings.gui/theme/custom during an isolated migration.
Only a Viewport/SubViewport owner calls SiriusUiMetrics.IsCompact().
Ignition is SiriusIgnitionButton, not a component.
HPA-541 owns persisted reduced motion.
HPA-386 owns toast/reward queueing and short confirmation seals.
Do not repeat shared StyleBoxFlat resources or palette values in migrated screens.
```

Keep the README concise and link to the design for variation tables and rationale.

- [ ] **Step 2: Run focused HPA-377 tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~Sirius"
```

Expected: zero failures.

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: exit 0, zero errors.

- [ ] **Step 4: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Save the exact summary lines from command output for the PR description.

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
```

Fail the review if `project.godot` or a production screen/controller appears.

- [ ] **Step 6: Check test-file size guard**

```bash
find tests/ui/theme tests/ui/components tests/ui/showcase -name '*.cs' \
  -print0 | xargs -0 wc -l
```

Expected: every individual HPA-377 test file is below 500 lines.

- [ ] **Step 7: Commit documentation**

```bash
git add docs/ui/hpa-377/README.md
git commit -m "docs: add Sirius theme integration guide"
```

- [ ] **Step 8: Update the draft PR description**

Copy the exact focused-test, build, and full-suite summary lines produced in Steps 2–4. Add a scope-audit statement confirming no `project.godot` or production-screen changes. Do not type or estimate numeric counts from memory.
