# Shared Sirius Theme, Core Components, and UI Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement HPA-377 as one opt-in Godot Theme resource, seven thin presentation components, an isolated UI showcase, and focused deterministic tests without introducing another lifecycle framework.

**Architecture:** `SiriusTheme.tres` is the single visual source of truth. Small C# contracts expose stable theme names, enum mappings, responsive metrics, and motion constants; components only map presentation state and compose scene-authored controls. `UIScreenHost`, settings persistence, notification queueing, navigation, focus restoration, and production-screen migration remain outside this work.

**Tech Stack:** Godot.NET SDK 4.6.2, Godot 4.6, C# 12, .NET 8, GdUnit4 5.0, `Sirius.sln`, `test.runsettings.local` for local runs and `test.runsettings` in CI.

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
- Only a `Viewport` or `SubViewport` owner computes compact mode. Controls in the same viewport inherit the decision.
- Components never read `GameManager`, `SaveManager`, `SettingsManager`, `RecoveryChest`, or `UIScreenHost`.
- Persisted reduced motion remains HPA-541 work. Toast/reward queueing and short confirmation seals remain HPA-386 work.
- Loading is a static showcase fixture only: a disabled Primary button labelled `Loading…`.
- Keep each HPA-377 test file below 500 lines. If a test needs re-entrant or teardown combinatorics, revisit the production abstraction instead of expanding the matrix.
- Use structural/resource assertions rather than pixel equality.
- Use tabs only where the Theme/showcase requires them; do not pre-style unrelated Godot control types.

## File Map

Create:

```text
resources/ui/theme/
└── SiriusTheme.tres

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

scenes/ui/showcase/
└── SiriusUiShowcase.tscn

scripts/ui/showcase/
└── SiriusUiShowcase.cs

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
└── SiriusUiShowcaseTest.cs

docs/ui/hpa-377/
└── README.md
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
- Produces `SiriusThemeTypes.ResourcePath` and all stable `StringName` values used by resources, controls, and tests.
- Produces closed enums `SiriusActionButtonVariant`, `SiriusPanelSurface`, `SiriusUiSeverity`, `SiriusModalSizeClass`, and `SiriusStatBarKind`.
- Produces exhaustive extension methods `ToThemeType()`, `ToIconId()`, `ToModalPanelThemeType()`, and `ToToastPanelThemeType()`.
- Produces `SiriusUiMetrics.IsCompact(Vector2)`, `SafeMargin(bool)`, `MinimumTarget(bool)`, `IgnitionSize(bool)`, `ModalWidth(SiriusModalSizeClass)`, `VerificationViewports`, and `FullInteractionViewports`.
- Produces motion constants and `SiriusMotion.EntrySeconds(bool)` / `ExitSeconds(bool)`.

- [ ] **Step 1: Write the failing contract tests**

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

- [ ] **Step 2: Run the focused test and verify it fails**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
```

Expected: FAIL because the Sirius theme contract types do not exist.

- [ ] **Step 3: Add stable Theme identifiers**

Create `scripts/ui/theme/SiriusThemeTypes.cs`:

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
    [
        PrimaryButton, SecondaryButton, TertiaryButton,
        WarningButton, DestructiveButton
    ];
}
```

- [ ] **Step 4: Add closed enums and exhaustive mappings**

Create `scripts/ui/theme/SiriusUiTypes.cs`:

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

- [ ] **Step 5: Add metrics and compact helpers**

Create `scripts/ui/theme/SiriusUiMetrics.cs`:

```csharp
using Godot;

public static class SiriusUiMetrics
{
    public const int Space4 = 4;
    public const int Space8 = 8;
    public const int Space12 = 12;
    public const int Space16 = 16;
    public const int Space24 = 24;
    public const int Space32 = 32;
    public const int Space48 = 48;

    public const int CompactWidth = 800;
    public const int CompactHeight = 450;
    public const int StandardSafeMargin = 24;
    public const int CompactSafeMargin = 12;
    public const int UltrawideContentMaximum = 1600;
    public const int StandardMinimumTarget = 44;
    public const int CompactMinimumTarget = 40;
    public const int StandardSlot = 56;
    public const int CompactSlot = 48;
    public const int StandardIgnition = 96;
    public const int CompactIgnition = 80;
    public const int TooltipStandardMaximum = 360;
    public const int TooltipCompactMaximum = 280;

    public static readonly Vector2I[] VerificationViewports =
    [
        new(640, 360), new(1024, 768), new(1280, 720), new(1440, 900),
        new(1920, 1080), new(2560, 1080), new(2560, 1440)
    ];

    public static readonly Vector2I[] FullInteractionViewports =
    [
        new(640, 360), new(1024, 768), new(1280, 720), new(2560, 1080)
    ];

    public static bool IsCompact(Vector2 safeFrameSize) =>
        safeFrameSize.X < CompactWidth || safeFrameSize.Y < CompactHeight;

    public static int SafeMargin(bool compact) =>
        compact ? CompactSafeMargin : StandardSafeMargin;

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

- [ ] **Step 6: Add motion constants without a state machine**

Create `scripts/ui/theme/SiriusMotion.cs`:

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

- [ ] **Step 7: Mark the approved design**

Change only the status line in the design document:

```markdown
**Status:** Approved design
```

Do not otherwise rewrite the approved design during implementation.

- [ ] **Step 8: Run the focused tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiContractsTest"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add docs/superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md \
  scripts/ui/theme tests/ui/theme/SiriusUiContractsTest.cs
git commit -m "feat: define Sirius UI theme contracts"
```

---

### Task 2: Author Palette and Typography in the Theme Resource

**Files:**
- Create: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeTypographyTest.cs`

**Interfaces:**
- Produces a loadable `Theme` at `SiriusThemeTypes.ResourcePath`.
- Produces all thirteen typography variations with exact Label bases, font resources, sizes, colors, tracking, line spacing, and fallback behavior.
- Later tasks extend the same resource; they do not replace it or generate it at runtime.

- [ ] **Step 1: Write the failing typography resource tests**

Create `tests/ui/theme/SiriusThemeTypographyTest.cs`:

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
        AssertBaseFont(theme, SiriusThemeTypes.Telemetry,
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

- [ ] **Step 2: Run and verify the resource test fails**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
```

Expected: FAIL because `SiriusTheme.tres` does not exist.

- [ ] **Step 3: Create the Theme and direct font references**

Create `resources/ui/theme/SiriusTheme.tres` as an authored Godot `Theme` resource. Set:

```text
default_font = res://assets/fonts/noto_sans/NotoSans-Regular.ttf
default_font_size = 16
```

Add direct external resources for:

```text
res://assets/fonts/cinzel/Cinzel-Variable.ttf
res://assets/fonts/noto_sans/NotoSans-Regular.ttf
res://assets/fonts/noto_sans/NotoSans-Medium.ttf
res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf
res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf
```

Create reusable `FontVariation` subresources with these exact responsibilities:

```text
Display: Cinzel variable, wght=600, Noto Sans Regular fallback
Title/Section: Noto Sans SemiBold
Body: Noto Sans Regular, spacing_top=3, spacing_bottom=3
Control: Noto Sans Medium
Metadata: Noto Sans Regular
Numeric: Noto Sans Mono Medium, OpenType tnum=1
Telemetry: Noto Sans Mono Medium, OpenType tnum=1, spacing_glyph=1
```

Disable system fallback on the committed `FontFile` resources used by the Theme where the editor exposes that import/resource option; use the explicit Noto Sans fallback for Display.

- [ ] **Step 4: Add Label type variations and exact sizes**

Use `Theme.set_type_variation()` through the Theme inspector so every variation has base `Label`. Configure:

```text
SiriusDisplay          44  Display font  moon-50
SiriusDisplayCompact   30  Display font  moon-50
SiriusTitle            32  Title font    moon-50
SiriusTitleCompact     24  Title font    moon-50
SiriusSection          20  Title font    moon-50
SiriusSectionCompact   17  Title font    moon-50
SiriusBody             16  Body font     moon-50
SiriusBodyCompact      14  Body font     moon-50
SiriusMetadata         14  Metadata      moon-200
SiriusMetadataCompact  12  Metadata      moon-200
SiriusNumeric          16  Numeric       moon-50
SiriusNumericCompact   14  Numeric       moon-50
SiriusTelemetry        12  Telemetry     moon-400
```

Use exact colors:

```text
moon-50  = #F7F5FF
moon-200 = #C7CEE8
moon-400 = #8F9AB8
```

- [ ] **Step 5: Run the typography test**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeTypographyTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add resources/ui/theme/SiriusTheme.tres \
  tests/ui/theme/SiriusThemeTypographyTest.cs
git commit -m "feat: add Sirius typography theme"
```

---

### Task 3: Add Buttons, Ignition, Surfaces, Bars, Tabs, Tooltips, Scrollbars, and Scrims

**Files:**
- Modify: `resources/ui/theme/SiriusTheme.tres`
- Create: `tests/ui/theme/SiriusThemeControlsTest.cs`

**Interfaces:**
- Produces all Theme items consumed by Tasks 4–9.
- Required Button state names are `normal`, `hover`, `pressed`, `hover_pressed`, `focus`, and `disabled`.
- Required ProgressBar style names are `background` and `fill`.
- Required scrim types are `SiriusScrim` and `SiriusChildScrim`, each based on `Panel`.

- [ ] **Step 1: Write failing control-resource tests**

Create `tests/ui/theme/SiriusThemeControlsTest.cs`:

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
        foreach (StringName state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
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
        foreach (StringName panel in new[]
        {
            SiriusThemeTypes.ContentPanel, SiriusThemeTypes.FeaturePanel,
            SiriusThemeTypes.HudPlate, SiriusThemeTypes.ModalPanel,
            SiriusThemeTypes.WarningPanel, SiriusThemeTypes.ErrorPanel
        })
        {
            AssertThat(theme.IsTypeVariation(panel, "PanelContainer")).IsTrue();
            AssertThat(theme.HasStylebox("panel", panel)).IsTrue();
        }

        foreach (StringName bar in new[]
        {
            SiriusThemeTypes.HpBar, SiriusThemeTypes.MpBar,
            SiriusThemeTypes.ExpBar, SiriusThemeTypes.InvalidBar
        })
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

- [ ] **Step 2: Run and verify the new tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusThemeControlsTest"
```

Expected: FAIL because the control variations are missing.

- [ ] **Step 3: Add shared palette-backed StyleBoxFlat resources**

Use only these approved colors and derived alpha values:

```text
night-1000 #050714
night-900  #0D1530
indigo-800 #18234A
indigo-700 #27366C
moon-50    #F7F5FF
moon-200   #C7CEE8
moon-400   #8F9AB8
cyan-400   #62DCFF
gold-300   #F5D784
gold-500   #DFAE43
warning    #F1B85B
danger     #F16D83
```

Create reusable style resources with exact geometry:

```text
control radius 8
panel radius 12
feature radius 16
normal border 1
focus border 2
focus expand margin 2
pressed content offset 1 downward
```

- [ ] **Step 4: Add five conventional Button variations**

For each variation, set base type `Button`, the six required styleboxes, fonts from Task 2, and minimum readable contrast:

```text
Primary:
  normal gold-500 fill, night-1000 text
  hover gold-300 fill
  pressed gold-500 fill + 1 px depression
  focus transparent fill, 2 px cyan border, expand 2
  disabled normal colors at 45% alpha, no glow

Secondary:
  normal indigo-800 fill, moon-50 text, moon-400 border
  hover indigo-700 fill + restrained cyan border
  pressed night-900 fill + 1 px depression
  focus independent cyan ring
  disabled 45% alpha

Tertiary:
  normal transparent fill, moon-200 text
  hover indigo-800 fill
  pressed night-900 fill
  focus independent cyan ring
  disabled 45% alpha

Warning:
  normal night-900 fill, warning border/text marker
  hover indigo-800 fill, warning border
  pressed night-900 fill + 1 px depression
  focus independent cyan ring
  disabled 45% alpha

Destructive:
  normal night-900 fill, danger border/text marker
  hover indigo-800 fill, danger border
  pressed danger fill, night-1000 text, 1 px depression
  focus independent cyan ring
  disabled 45% alpha
```

Set `hover_pressed` to pressed geometry plus the corresponding hover border/light treatment.

- [ ] **Step 5: Add the stock Ignition Button variation**

Set base type `Button`. Use:

```text
texture: res://assets/sprites/ui/ornaments/ignition_seal.png
focus:  res://assets/sprites/ui/ornaments/focus_halo.png
preferred size: applied by showcase/consumer via SiriusUiMetrics.IgnitionSize()
content inset: 16 px all sides
normal modulate: gold-500 at 92% alpha
hover modulate: gold-300 at 100% alpha
pressed modulate: gold-500 at 100% alpha
hover_pressed: gold-300 at 100% alpha
pressed content offset: 1 px down
disabled modulate: moon-400 at 45% alpha
focus expand margin: 6 px standard visual allowance
```

Use one `StyleBoxTexture` per state. Every non-focus state references the same seal texture; the focus state references the focus-halo texture. Do not add a missing-texture fallback.

- [ ] **Step 6: Add panels and scrims**

Set PanelContainer variations:

```text
SiriusContentPanel: night-900 at 90%, radius 12, border moon-400/1
SiriusFeaturePanel: indigo-800 at 96%, radius 16, soft shadow, border moon-400/1
SiriusHudPlate: night-900 at 82%, radius 12, border moon-400/1
SiriusModalPanel: night-900 at 96%, radius 12, soft shadow, border moon-400/1
SiriusWarningPanel: Modal geometry with warning border
SiriusErrorPanel: Modal geometry with danger border
```

Set Panel variations:

```text
SiriusScrim: night-1000 alpha 0.58
SiriusChildScrim: night-1000 alpha 0.72
```

- [ ] **Step 7: Add HP, MP, EXP, and invalid ProgressBar variations**

Use base `ProgressBar`, visible `background` track, and these fills:

```text
SiriusHpBar: danger #F16D83
SiriusMpBar: cyan #62DCFF
SiriusExpBar: gold #F5D784
SiriusInvalidBar: moon-400 #8F9AB8
```

Use a 4 px bar radius, `night-1000` track, and no flashing or looping effects.

- [ ] **Step 8: Add TabBar, tooltip, and scrollbar base items**

Configure only the controls demanded by the approved design:

```text
TabBar:
  tab_unselected: night-900/indigo quiet state
  tab_hovered: indigo-800 + restrained cyan edge
  tab_selected: gold marker/border
  focus: independent cyan ring

TooltipPanel:
  panel: night-900 at 96%, radius 8, moon-400 border

TooltipLabel:
  font: Noto Sans Regular
  font_size: 14
  font_color: moon-50

HScrollBar/VScrollBar:
  scroll: night-900
  grabber: indigo-700
  grabber_highlight: cyan-400 restrained
  grabber_pressed: gold-500
```

- [ ] **Step 9: Run all Theme tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusTheme"
```

Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add resources/ui/theme/SiriusTheme.tres \
  tests/ui/theme/SiriusThemeControlsTest.cs
git commit -m "feat: add Sirius control theme"
```

---

### Task 4: Implement `SiriusActionButton` and `SiriusPanel`

**Files:**
- Create: `scripts/ui/components/SiriusActionButton.cs`
- Create: `scripts/ui/components/SiriusPanel.cs`
- Create: `tests/ui/components/SiriusComponentTestSupport.cs`
- Create: `tests/ui/components/SiriusActionButtonTest.cs`
- Create: `tests/ui/components/SiriusPanelTest.cs`

**Interfaces:**
- `SiriusActionButton` exports `Variant`, `ShowIcon`, `IconId`, `IconSize`, and `DisabledReason`.
- `SiriusPanel` exports `Surface`.
- Neither component exposes compact state, loading state, navigation, async operations, or host behavior.

- [ ] **Step 1: Add shared component test support**

Create `tests/ui/components/SiriusComponentTestSupport.cs`:

```csharp
using Godot;
using System.Threading.Tasks;

public static class SiriusComponentTestSupport
{
    public static async Task<T> AddToRoot<T>(T node) where T : Node
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(node);
        await node.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        return node;
    }

    public static async Task<T> Instantiate<T>(string path) where T : Node
    {
        var packed = ResourceLoader.Load<PackedScene>(path)!;
        return await AddToRoot(packed.Instantiate<T>());
    }

    public static async Task Free(Node? node)
    {
        if (node != null && GodotObject.IsInstanceValid(node))
            node.Free();
        await ((SceneTree)Engine.GetMainLoop()).ToSignal(
            Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }
}
```

- [ ] **Step 2: Write failing ActionButton tests**

Create `tests/ui/components/SiriusActionButtonTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusActionButtonTest : Node
{
    private SiriusActionButton? _button;

    [AfterTest]
    public async Task Cleanup() => await SiriusComponentTestSupport.Free(_button);

    [TestCase]
    public async Task VariantIconAndDisabledReason_MapToNativeButtonPresentation()
    {
        _button = await SiriusComponentTestSupport.AddToRoot(new SiriusActionButton
        {
            Variant = SiriusActionButtonVariant.Warning,
            ShowIcon = true,
            IconId = UiIconId.Warning,
            IconSize = UiIconSize.Metadata,
            Disabled = true,
            DisabledReason = "Requires a valid target"
        });

        AssertThat(_button.ThemeTypeVariation).IsEqual(SiriusThemeTypes.WarningButton);
        AssertThat(_button.Icon!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Warning, UiIconSize.Metadata));
        AssertThat(_button.TooltipText).IsEqual("Requires a valid target");
    }

    [TestCase]
    public async Task NativeToggleState_RemainsOwnedByButton()
    {
        _button = await SiriusComponentTestSupport.AddToRoot(new SiriusActionButton
        {
            Variant = SiriusActionButtonVariant.Primary,
            ToggleMode = true,
            ButtonPressed = true
        });

        AssertThat(_button.ButtonPressed).IsTrue();
        AssertThat(_button.ThemeTypeVariation).IsEqual(SiriusThemeTypes.PrimaryButton);
    }
}
```

- [ ] **Step 3: Write failing Panel tests**

Create `tests/ui/components/SiriusPanelTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusPanelTest : Node
{
    private SiriusPanel? _panel;

    [AfterTest]
    public async Task Cleanup() => await SiriusComponentTestSupport.Free(_panel);

    [TestCase]
    public async Task Surface_MapsToThemeVariation()
    {
        _panel = await SiriusComponentTestSupport.AddToRoot(new SiriusPanel
        {
            Surface = SiriusPanelSurface.HudPlate
        });

        AssertThat(_panel.ThemeTypeVariation).IsEqual(SiriusThemeTypes.HudPlate);
    }
}
```

- [ ] **Step 4: Run and verify the tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
```

Expected: FAIL because the components do not exist.

- [ ] **Step 5: Implement `SiriusActionButton`**

Create `scripts/ui/components/SiriusActionButton.cs`:

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

    [Export]
    public SiriusActionButtonVariant Variant
    {
        get => _variant;
        set { _variant = value; ApplyPresentation(); }
    }

    [Export]
    public bool ShowIcon
    {
        get => _showIcon;
        set { _showIcon = value; ApplyPresentation(); }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set { _iconId = value; ApplyPresentation(); }
    }

    [Export]
    public UiIconSize IconSize
    {
        get => _iconSize;
        set { _iconSize = value; ApplyPresentation(); }
    }

    [Export(PropertyHint.MultilineText)]
    public string DisabledReason
    {
        get => _disabledReason;
        set { _disabledReason = value ?? string.Empty; ApplyPresentation(); }
    }

    public override void _Ready() => ApplyPresentation();

    private void ApplyPresentation()
    {
        ThemeTypeVariation = Variant.ToThemeType();
        TooltipText = Disabled && !string.IsNullOrWhiteSpace(DisabledReason)
            ? DisabledReason
            : string.Empty;
        if (ShowIcon)
            UiIconPresenter.Apply(this, IconId, IconSize);
        else
            Icon = null;
    }
}
```

- [ ] **Step 6: Implement `SiriusPanel`**

Create `scripts/ui/components/SiriusPanel.cs`:

```csharp
using Godot;

[Tool]
public partial class SiriusPanel : PanelContainer
{
    private SiriusPanelSurface _surface;

    [Export]
    public SiriusPanelSurface Surface
    {
        get => _surface;
        set { _surface = value; ApplySurface(); }
    }

    public override void _Ready() => ApplySurface();

    private void ApplySurface() => ThemeTypeVariation = Surface.ToThemeType();
}
```

- [ ] **Step 7: Run component tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusActionButtonTest|FullyQualifiedName~SiriusPanelTest"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add scripts/ui/components/SiriusActionButton.cs \
  scripts/ui/components/SiriusPanel.cs tests/ui/components
git commit -m "feat: add Sirius action and panel components"
```

---

### Task 5: Implement the Scene-Authored Modal Shell

**Files:**
- Create: `scenes/ui/components/SiriusModalShell.tscn`
- Create: `scripts/ui/components/SiriusModalShell.cs`
- Create: `tests/ui/components/SiriusModalShellTest.cs`

**Interfaces:**
- Exports `Title`, `Severity`, `SizeClass`, `Compact`, `ReducedMotion`, and `ShowCloseAffordance`.
- Exposes `BodyHost`, `ActionsHost`, `CloseRequested`, `PlayEntry()`, and `PlayExit()`.
- Does not create a scrim, register with a host, select focus, intercept Cancel, dismiss itself, or choose a domain action.

- [ ] **Step 1: Write failing modal-shell tests**

Create `tests/ui/components/SiriusModalShellTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusModalShellTest : Node
{
    private SiriusModalShell? _shell;

    [AfterTest]
    public async Task Cleanup() => await SiriusComponentTestSupport.Free(_shell);

    [TestCase]
    public async Task SeverityAndSize_MapWithoutOwningScrim()
    {
        _shell = await SiriusComponentTestSupport.Instantiate<SiriusModalShell>(
            "res://scenes/ui/components/SiriusModalShell.tscn");
        _shell.Title = "Cannot load save";
        _shell.Severity = SiriusUiSeverity.Error;
        _shell.SizeClass = SiriusModalSizeClass.Small;
        _shell.Compact = false;
        _shell.RefreshPresentation();

        AssertThat(_shell.GetNode<PanelContainer>("%Panel").ThemeTypeVariation)
            .IsEqual(SiriusThemeTypes.ErrorPanel);
        AssertThat(_shell.GetNode<Label>("%TitleLabel").Text)
            .IsEqual("Cannot load save");
        AssertThat(_shell.GetNode<TextureRect>("%SeverityIcon").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Error, UiIconSize.Default));
        AssertThat(_shell.GetNodeOrNull<Panel>("%Scrim")).IsNull();
        AssertThat(_shell.BodyHost).IsNotNull();
        AssertThat(_shell.ActionsHost).IsNotNull();
    }

    [TestCase]
    public async Task Compact_UsesCompactTitleAndSafeWidth()
    {
        _shell = await SiriusComponentTestSupport.Instantiate<SiriusModalShell>(
            "res://scenes/ui/components/SiriusModalShell.tscn");
        _shell.Compact = true;
        _shell.SizeClass = SiriusModalSizeClass.Large;
        _shell.RefreshPresentation(new Vector2(640, 360));

        AssertThat(_shell.GetNode<Label>("%TitleLabel").ThemeTypeVariation)
            .IsEqual(SiriusThemeTypes.TitleCompact);
        AssertThat(_shell.GetNode<PanelContainer>("%Panel").CustomMinimumSize.X)
            .IsLessEqual(640 - SiriusUiMetrics.CompactSafeMargin * 2);
    }
}
```

- [ ] **Step 2: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
```

Expected: FAIL because the scene and script do not exist.

- [ ] **Step 3: Author the modal scene**

Create this exact scene structure and mark `%` nodes as unique names:

```text
SiriusModalShell : Control [script=SiriusModalShell.cs]
└── Panel : SiriusPanel [%Panel, Surface=Modal]
    └── Margin : MarginContainer
        └── RootLayout : VBoxContainer
            ├── Header : HBoxContainer
            │   ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            │   ├── TitleLabel : Label [%TitleLabel]
            │   └── CloseButton : SiriusActionButton [%CloseButton, Tertiary]
            ├── BodyScroll : ScrollContainer [%BodyScroll]
            │   └── BodyHost : VBoxContainer [%BodyHost]
            └── ActionsHost : HBoxContainer [%ActionsHost]
```

Set `MarginContainer` margins to `Space24`; set `RootLayout` separation to `Space16`; set `ActionsHost` alignment to End and separation to `Space8`. The shell itself contains no scrim node.

- [ ] **Step 4: Implement presentation and sizing**

Create `scripts/ui/components/SiriusModalShell.cs` with these required methods and properties:

```csharp
using Godot;

[Tool]
public partial class SiriusModalShell : Control
{
    [Signal] public delegate void CloseRequestedEventHandler();

    private Tween? _motionTween;
    private string _title = string.Empty;
    private SiriusUiSeverity _severity;
    private SiriusModalSizeClass _sizeClass = SiriusModalSizeClass.Medium;
    private bool _compact;

    [Export] public string Title { get => _title; set { _title = value ?? string.Empty; RefreshPresentation(); } }
    [Export] public SiriusUiSeverity Severity { get => _severity; set { _severity = value; RefreshPresentation(); } }
    [Export] public SiriusModalSizeClass SizeClass { get => _sizeClass; set { _sizeClass = value; RefreshPresentation(); } }
    [Export] public bool Compact { get => _compact; set { _compact = value; RefreshPresentation(); } }
    [Export] public bool ReducedMotion { get; set; }
    [Export] public bool ShowCloseAffordance { get; set; }

    public Control BodyHost => GetNode<Control>("%BodyHost");
    public Control ActionsHost => GetNode<Control>("%ActionsHost");

    public override void _Ready()
    {
        GetNode<Button>("%CloseButton").Pressed += OnClosePressed;
        RefreshPresentation();
    }

    public override void _ExitTree()
    {
        _motionTween?.Kill();
        if (IsNodeReady())
            GetNode<Button>("%CloseButton").Pressed -= OnClosePressed;
    }

    public void RefreshPresentation() =>
        RefreshPresentation(GetViewportRect().Size);

    public void RefreshPresentation(Vector2 availableSize)
    {
        if (!IsNodeReady())
            return;

        var panel = GetNode<PanelContainer>("%Panel");
        var title = GetNode<Label>("%TitleLabel");
        var icon = GetNode<TextureRect>("%SeverityIcon");
        var close = GetNode<Button>("%CloseButton");

        panel.ThemeTypeVariation = Severity.ToModalPanelThemeType();
        title.Text = Title;
        title.ThemeTypeVariation = Compact
            ? SiriusThemeTypes.TitleCompact
            : SiriusThemeTypes.Title;
        UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
        close.Visible = ShowCloseAffordance;

        var maximum = availableSize.X * 0.90f;
        var width = Compact
            ? availableSize.X - SiriusUiMetrics.CompactSafeMargin * 2
            : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), maximum);
        panel.CustomMinimumSize = new Vector2(Mathf.Max(0, width), 0);
    }

    public Tween PlayEntry()
    {
        _motionTween?.Kill();
        Visible = true;
        Modulate = new Color(1, 1, 1, 0);
        Position = SiriusMotion.UseTransform(ReducedMotion) ? new Vector2(0, 12) : Vector2.Zero;
        _motionTween = CreateTween();
        _motionTween.SetParallel(true);
        _motionTween.TweenProperty(this, "modulate:a", 1.0f, SiriusMotion.EntrySeconds(ReducedMotion));
        if (SiriusMotion.UseTransform(ReducedMotion))
            _motionTween.TweenProperty(this, "position", Vector2.Zero, SiriusMotion.EntrySeconds(false));
        return _motionTween;
    }

    public Tween PlayExit()
    {
        _motionTween?.Kill();
        _motionTween = CreateTween();
        _motionTween.SetParallel(true);
        _motionTween.TweenProperty(this, "modulate:a", 0.0f, SiriusMotion.ExitSeconds(ReducedMotion));
        if (SiriusMotion.UseTransform(ReducedMotion))
            _motionTween.TweenProperty(this, "position", new Vector2(0, 8), SiriusMotion.ExitSeconds(false));
        _motionTween.Finished += Hide;
        return _motionTween;
    }

    private void OnClosePressed() => EmitSignal(SignalName.CloseRequested);
}
```

- [ ] **Step 5: Run modal tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusModalShellTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scenes/ui/components/SiriusModalShell.tscn \
  scripts/ui/components/SiriusModalShell.cs \
  tests/ui/components/SiriusModalShellTest.cs
git commit -m "feat: add Sirius modal shell"
```

---

### Task 6: Implement the Scene-Authored Stat Bar

**Files:**
- Create: `scenes/ui/components/SiriusStatBar.tscn`
- Create: `scripts/ui/components/SiriusStatBar.cs`
- Create: `tests/ui/components/SiriusStatBarTest.cs`

**Interfaces:**
- Exports `Kind`, `Current`, `Maximum`, `Label`, `ShowNumericValue`, `LowThreshold`, and `Compact`.
- Exposes no domain mutation. Visual fill clamps while displayed numbers preserve caller values.

- [ ] **Step 1: Write failing edge-case tests**

Create `tests/ui/components/SiriusStatBarTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusStatBarTest : Node
{
    private SiriusStatBar? _bar;

    [AfterTest]
    public async Task Cleanup() => await SiriusComponentTestSupport.Free(_bar);

    [TestCase]
    public async Task HealthKind_MapsThemeIconAndLowState()
    {
        _bar = await SiriusComponentTestSupport.Instantiate<SiriusStatBar>(
            "res://scenes/ui/components/SiriusStatBar.tscn");
        _bar.Kind = SiriusStatBarKind.Health;
        _bar.Label = "HP";
        _bar.Current = 20;
        _bar.Maximum = 100;
        _bar.RefreshPresentation();

        AssertThat(_bar.GetNode<ProgressBar>("%Bar").ThemeTypeVariation)
            .IsEqual(SiriusThemeTypes.HpBar);
        AssertThat(_bar.GetNode<ProgressBar>("%Bar").Value).IsEqual(20);
        AssertThat(_bar.GetNode<Label>("%StateLabel").Text).IsEqual("Low");
        AssertThat(_bar.GetNode<TextureRect>("%Icon").Texture!.ResourcePath)
            .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Health, UiIconSize.Metadata));
    }

    [TestCase]
    public async Task InvalidNegativeAndOverflow_PreserveCallerValues()
    {
        _bar = await SiriusComponentTestSupport.Instantiate<SiriusStatBar>(
            "res://scenes/ui/components/SiriusStatBar.tscn");

        _bar.Current = 120;
        _bar.Maximum = 100;
        _bar.RefreshPresentation();
        AssertThat(_bar.GetNode<ProgressBar>("%Bar").Value).IsEqual(100);
        AssertThat(_bar.GetNode<Label>("%ValueLabel").Text).IsEqual("120 / 100");
        AssertThat(_bar.GetNode<Label>("%StateLabel").Text).IsEqual("Overflow");

        _bar.Current = -5;
        _bar.Maximum = 100;
        _bar.RefreshPresentation();
        AssertThat(_bar.GetNode<ProgressBar>("%Bar").Value).IsEqual(0);
        AssertThat(_bar.GetNode<Label>("%StateLabel").Text).IsEqual("Invalid value");

        _bar.Current = 10;
        _bar.Maximum = 0;
        _bar.RefreshPresentation();
        AssertThat(_bar.GetNode<ProgressBar>("%Bar").ThemeTypeVariation)
            .IsEqual(SiriusThemeTypes.InvalidBar);
        AssertThat(_bar.GetNode<Label>("%StateLabel").Text).IsEqual("Invalid maximum");
    }
}
```

- [ ] **Step 2: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusStatBarTest"
```

- [ ] **Step 3: Author the stat-bar scene**

```text
SiriusStatBar : VBoxContainer [script=SiriusStatBar.cs]
├── Header : HBoxContainer
│   ├── Icon : TextureRect [%Icon, 16×16]
│   ├── NameLabel : Label [%NameLabel]
│   ├── Spacer : Control [ExpandFill]
│   └── ValueLabel : Label [%ValueLabel]
├── Bar : ProgressBar [%Bar, ShowPercentage=false]
└── StateLabel : Label [%StateLabel]
```

Set header separation to `Space4`. The state label starts hidden and uses Metadata typography.

- [ ] **Step 4: Implement deterministic presentation logic**

Create `scripts/ui/components/SiriusStatBar.cs` with exported properties and this core method:

```csharp
public void RefreshPresentation()
{
    if (!IsNodeReady())
        return;

    var bar = GetNode<ProgressBar>("%Bar");
    var name = GetNode<Label>("%NameLabel");
    var value = GetNode<Label>("%ValueLabel");
    var state = GetNode<Label>("%StateLabel");
    var icon = GetNode<TextureRect>("%Icon");

    name.Text = Label;
    name.ThemeTypeVariation = Compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
    value.ThemeTypeVariation = Compact ? SiriusThemeTypes.NumericCompact : SiriusThemeTypes.Numeric;
    value.Visible = ShowNumericValue;
    value.Text = $"{Current:0.##} / {Maximum:0.##}";
    state.ThemeTypeVariation = Compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    UiIconPresenter.Apply(icon, Kind.ToIconId(), UiIconSize.Metadata);

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

    if (Current < 0)
        SetState(state, "Invalid value");
    else if (Current > Maximum)
        SetState(state, "Overflow");
    else if (Current / Maximum <= LowThreshold)
        SetState(state, "Low");
    else
        SetState(state, string.Empty);
}

private static void SetState(Label label, string text)
{
    label.Text = text;
    label.Visible = !string.IsNullOrEmpty(text);
}
```

Define the properties exactly as specified by the task interface, default `ShowNumericValue = true`, default `LowThreshold = 0.25`, and call `RefreshPresentation()` from `_Ready()` and every setter.

- [ ] **Step 5: Run stat-bar tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusStatBarTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scenes/ui/components/SiriusStatBar.tscn \
  scripts/ui/components/SiriusStatBar.cs \
  tests/ui/components/SiriusStatBarTest.cs
git commit -m "feat: add Sirius stat bar"
```

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
- `SiriusInputHint` exposes `Prompt`, `Actions`, `Compact`, `ActiveDevice`, `Observe(InputEvent)`, and `Refresh()`.
- `SiriusContextPrompt` exposes `ShowIcon`, `IconId`, `Prompt`, `Actions`, and `Compact` and delegates binding presentation to its child input hint.

- [ ] **Step 1: Write failing InputHint tests**

Create `tests/ui/components/SiriusInputHintTest.cs` using the existing `InputMap` save/restore pattern from `Hpa374RuntimeSmokeTest`:

```csharp
[TestCase]
public async Task KeyboardMouseGamepadAndUnbound_RefreshReadablePresentation()
{
    const string action = "hpa377_hint_test";
    InputMap.AddAction(action);
    try
    {
        _hint = await SiriusComponentTestSupport.Instantiate<SiriusInputHint>(
            "res://scenes/ui/components/SiriusInputHint.tscn");
        _hint.Prompt = "Close";
        _hint.Actions = [action];

        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = Key.K });
        _hint.Observe(new InputEventKey { PhysicalKeycode = Key.K, Pressed = true });
        AssertThat(_hint.GetNode<Label>("%BindingLabel").Text).IsEqual("K");

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = MouseButton.Left });
        _hint.Observe(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        AssertThat(_hint.GetNode<Label>("%BindingLabel").Text).IsEqual("Mouse 1");

        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        _hint.Observe(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
        AssertThat(_hint.GetNode<Label>("%BindingLabel").Text).IsEqual("A");

        InputMap.ActionEraseEvents(action);
        _hint.Refresh();
        AssertThat(_hint.GetNode<Label>("%BindingLabel").Text).IsEqual("Unbound");
    }
    finally
    {
        InputMap.EraseAction(action);
    }
}
```

Include `[BeforeTest]`/`[AfterTest]` cleanup and restore pre-existing actions if the test action unexpectedly exists.

- [ ] **Step 2: Write failing ContextPrompt test**

Create `tests/ui/components/SiriusContextPromptTest.cs`:

```csharp
[TestCase]
public async Task ContextPrompt_ComposesIconPromptAndActions()
{
    _prompt = await SiriusComponentTestSupport.Instantiate<SiriusContextPrompt>(
        "res://scenes/ui/components/SiriusContextPrompt.tscn");
    _prompt.ShowIcon = true;
    _prompt.IconId = UiIconId.Dialogue;
    _prompt.Prompt = "Talk";
    _prompt.Actions = ["interact"];
    _prompt.Refresh();

    AssertThat(_prompt.GetNode<Label>("%PromptLabel").Text).IsEqual("Talk");
    AssertThat(_prompt.GetNode<TextureRect>("%SemanticIcon").Texture!.ResourcePath)
        .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Dialogue, UiIconSize.Default));
    AssertThat(_prompt.GetNode<SiriusInputHint>("%InputHint").Actions)
        .ContainsExactly(new StringName("interact"));
}
```

- [ ] **Step 3: Run and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
```

- [ ] **Step 4: Author `SiriusInputHint.tscn`**

```text
SiriusInputHint : HBoxContainer [script=SiriusInputHint.cs]
├── DeviceIcon : TextureRect [%DeviceIcon, 16×16]
├── PromptLabel : Label [%PromptLabel]
└── BindingLabel : Label [%BindingLabel]
```

Use `Space4` separation. Set `MouseFilter=Ignore` on labels/icon.

- [ ] **Step 5: Implement `SiriusInputHint` around the existing presenter**

Implement:

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
    if (changed)
        Refresh();
    return changed;
}

public void Refresh()
{
    if (!IsNodeReady())
        return;
    var hint = _presenter.ResolveActions(Actions);
    GetNode<Label>("%PromptLabel").Text = Prompt;
    GetNode<Label>("%BindingLabel").Text = hint.BindingLabel;
    GetNode<Label>("%PromptLabel").ThemeTypeVariation =
        Compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    GetNode<Label>("%BindingLabel").ThemeTypeVariation =
        Compact ? SiriusThemeTypes.MetadataCompact : SiriusThemeTypes.Metadata;
    UiIconPresenter.Apply(GetNode<TextureRect>("%DeviceIcon"), hint.IconId, UiIconSize.Metadata);
}

public override void _Input(InputEvent inputEvent)
{
    if (IsVisibleInTree())
        Observe(inputEvent);
}
```

Call `SetProcessInput(IsVisibleInTree())` from `_Ready()` and a `VisibilityChanged` handler; disconnect that handler in `_ExitTree()`. Do not create a global device service.

- [ ] **Step 6: Author and implement `SiriusContextPrompt`**

Scene:

```text
SiriusContextPrompt : HBoxContainer [script=SiriusContextPrompt.cs]
├── SemanticIcon : TextureRect [%SemanticIcon, 24×24]
├── PromptLabel : Label [%PromptLabel]
└── InputHint : SiriusInputHint [%InputHint]
```

The script exports `ShowIcon`, `IconId`, `Prompt`, and `Compact`, stores `StringName[] Actions`, and implements `Refresh()`:

```csharp
public void Refresh()
{
    if (!IsNodeReady())
        return;
    var icon = GetNode<TextureRect>("%SemanticIcon");
    icon.Visible = ShowIcon;
    if (ShowIcon)
        UiIconPresenter.Apply(icon, IconId, UiIconSize.Default);
    var label = GetNode<Label>("%PromptLabel");
    label.Text = Prompt;
    label.ThemeTypeVariation = Compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
    var hint = GetNode<SiriusInputHint>("%InputHint");
    hint.Prompt = string.Empty;
    hint.Actions = Actions;
    hint.Compact = Compact;
    hint.Refresh();
}
```

- [ ] **Step 7: Run focused tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusInputHintTest|FullyQualifiedName~SiriusContextPromptTest"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add scenes/ui/components/SiriusInputHint.tscn \
  scenes/ui/components/SiriusContextPrompt.tscn \
  scripts/ui/components/SiriusInputHint.cs \
  scripts/ui/components/SiriusContextPrompt.cs \
  tests/ui/components/SiriusInputHintTest.cs \
  tests/ui/components/SiriusContextPromptTest.cs
git commit -m "feat: add Sirius input and context prompts"
```

---

### Task 8: Implement the Visual Toast Shell

**Files:**
- Create: `scenes/ui/components/SiriusToastShell.tscn`
- Create: `scripts/ui/components/SiriusToastShell.cs`
- Create: `tests/ui/components/SiriusToastShellTest.cs`

**Interfaces:**
- Exports `Severity`, `Title`, `Message`, `Compact`, and `ReducedMotion`.
- Exposes `PlayEntry()` and `PlayExit()` only for visual motion.
- Contains no Timer, queue, deduplication, host registration, acknowledgement, or transition-retention logic.

- [ ] **Step 1: Write failing toast tests**

Create `tests/ui/components/SiriusToastShellTest.cs`:

```csharp
[TestCase]
public async Task SeverityAndCompact_MapVisualPresentationOnly()
{
    _toast = await SiriusComponentTestSupport.Instantiate<SiriusToastShell>(
        "res://scenes/ui/components/SiriusToastShell.tscn");
    _toast.Severity = SiriusUiSeverity.Warning;
    _toast.Title = "Inventory full";
    _toast.Message = "The item remains in the recovery chest.";
    _toast.Compact = true;
    _toast.RefreshPresentation();

    AssertThat(_toast.GetNode<PanelContainer>("%Panel").ThemeTypeVariation)
        .IsEqual(SiriusThemeTypes.WarningPanel);
    AssertThat(_toast.GetNode<Label>("%TitleLabel").Text).IsEqual("Inventory full");
    AssertThat(_toast.GetNode<Label>("%MessageLabel").Text)
        .IsEqual("The item remains in the recovery chest.");
    AssertThat(_toast.GetNodeOrNull<Timer>("Timer")).IsNull();
}
```

Add cases for Info/Success/Error mappings and reduced-motion entry starting with zero translation.

- [ ] **Step 2: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

- [ ] **Step 3: Author the toast scene**

```text
SiriusToastShell : Control [script=SiriusToastShell.cs]
└── Panel : SiriusPanel [%Panel, Surface=Feature]
    └── Margin : MarginContainer [12 px]
        └── Row : HBoxContainer [separation=8]
            ├── SeverityIcon : TextureRect [%SeverityIcon, 24×24]
            └── TextColumn : VBoxContainer [separation=4]
                ├── TitleLabel : Label [%TitleLabel]
                └── MessageLabel : Label [%MessageLabel]
```

Do not add a Timer or queue container.

- [ ] **Step 4: Implement presentation and visual-only motion**

Mirror the small Tween implementation from `SiriusModalShell` but keep no close signal or lifecycle policy. `RefreshPresentation()` must:

```csharp
panel.ThemeTypeVariation = Severity.ToToastPanelThemeType();
title.Text = Title;
message.Text = Message;
title.ThemeTypeVariation = Compact ? SiriusThemeTypes.SectionCompact : SiriusThemeTypes.Section;
message.ThemeTypeVariation = Compact ? SiriusThemeTypes.BodyCompact : SiriusThemeTypes.Body;
UiIconPresenter.Apply(icon, Severity.ToIconId(), UiIconSize.Default);
```

`PlayEntry()` uses alpha plus 12 px translation only when normal motion is enabled. `PlayExit()` uses alpha plus 8 px translation only when normal motion is enabled. Neither method schedules timeout or removes the node.

- [ ] **Step 5: Run toast tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusToastShellTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scenes/ui/components/SiriusToastShell.tscn \
  scripts/ui/components/SiriusToastShell.cs \
  tests/ui/components/SiriusToastShellTest.cs
git commit -m "feat: add Sirius toast shell"
```

---

### Task 9: Build the Isolated Showcase and Reused-Fixture Viewport Matrix

**Files:**
- Create: `scenes/ui/showcase/SiriusUiShowcase.tscn`
- Create: `scripts/ui/showcase/SiriusUiShowcase.cs`
- Create: `tests/ui/showcase/SiriusUiShowcaseTest.cs`

**Interfaces:**
- Exposes `PreviewViewport`, `PreviewRoot`, `Compact`, `SetPreviewSize(Vector2I)`, `SetBackground(SiriusShowcaseBackground)`, and `SetReducedMotion(bool)`.
- Computes compact mode only from the owned `SubViewport` safe frame.
- Uses stable unique fixture names consumed by tests.

- [ ] **Step 1: Write the failing showcase test scaffold**

Create `tests/ui/showcase/SiriusUiShowcaseTest.cs` with one fixture created in `[BeforeTest]` and reused inside each test:

```csharp
[TestCase]
public async Task Showcase_ResizesSequentiallyAcrossApprovedMatrix()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        _showcase!.SetPreviewSize(size);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        AssertThat(_showcase.PreviewViewport.Size).IsEqual(size);
        AssertThat(_showcase.Compact)
            .IsEqual(SiriusUiMetrics.IsCompact(size));
        AssertThat(_showcase.GetNode<Control>("%ShowcaseContent").Size.X)
            .IsLessEqual(Mathf.Min(
                size.X - SiriusUiMetrics.SafeMargin(_showcase.Compact) * 2,
                SiriusUiMetrics.UltrawideContentMaximum));
        AssertThat(_showcase.GetNode<Control>("%PrimaryButtonFixture").CustomMinimumSize.X)
            .IsGreaterEqual(SiriusUiMetrics.MinimumTarget(_showcase.Compact).X);
    }
}

[TestCase]
public void Showcase_ContainsEveryRequiredFixture()
{
    string[] names =
    [
        "%PaletteSection", "%TypographySection", "%ButtonSection",
        "%IgnitionStandardFixture", "%IgnitionCompactFixture",
        "%SelectedFocusedFixture", "%LoadingFixture", "%TabsSection",
        "%StatBarSection", "%InputHintSection", "%ContextPromptSection",
        "%ToastSection", "%ModalSection", "%MotionSection"
    ];
    foreach (var name in names)
        AssertThat(_showcase!.GetNodeOrNull(name)).IsNotNull();
}
```

Add one test that iterates `SiriusUiMetrics.FullInteractionViewports`, grabs `%PrimaryButtonFixture`, pushes `ui_focus_next` through `PreviewViewport`, and asserts focus advances through the explicit focus chain without leaving `PreviewRoot`.

- [ ] **Step 2: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcaseTest"
```

- [ ] **Step 3: Author the showcase root scene**

Create:

```text
SiriusUiShowcase : Control [script=SiriusUiShowcase.cs]
├── ShowcaseToolbar : HBoxContainer
│   ├── ViewportSizeSelector : OptionButton [%ViewportSizeSelector]
│   ├── BackgroundSelector : OptionButton [%BackgroundSelector]
│   └── ReducedMotionToggle : CheckBox [%ReducedMotionToggle]
└── PreviewFrame : PanelContainer
    └── SubViewportContainer : SubViewportContainer [%PreviewContainer]
        └── PreviewViewport : SubViewport [%PreviewViewport]
            └── PreviewRoot : Control [%PreviewRoot, Theme=SiriusTheme.tres]
                ├── Background : TextureRect [%Background]
                ├── SolidBackground : ColorRect [%SolidBackground]
                └── SafeFrame : MarginContainer [%SafeFrame]
                    └── ResponsiveScroll : ScrollContainer
                        └── ShowcaseContent : VBoxContainer [%ShowcaseContent]
```

Inside `ShowcaseContent`, author named sections and fixtures from the test list. Use real shared components, stock Ignition Button, stock toggle Button, TabContainer, TooltipText, and both scrim Panel variations. The Loading fixture is:

```text
SiriusActionButton
Variant=Primary
Text="Loading…"
Disabled=true
DisabledReason="Please wait"
```

- [ ] **Step 4: Implement deterministic background and viewport controls**

Define:

```csharp
public enum SiriusShowcaseBackground
{
    NightSolid,
    MoonSolid,
    MainMenuScenic,
    BattleScenic
}
```

`SetBackground()` maps exactly to:

```text
NightSolid: ColorRect #050714, TextureRect hidden
MoonSolid: ColorRect #F7F5FF, TextureRect hidden
MainMenuScenic: res://assets/sprites/ui/ui_main_menu_background.png
BattleScenic: res://assets/sprites/ui/ui_battle_background.png
```

Implement `SetPreviewSize()`:

```csharp
public void SetPreviewSize(Vector2I size)
{
    PreviewViewport.Size = size;
    GetNode<SubViewportContainer>("%PreviewContainer").CustomMinimumSize = size;
    Compact = SiriusUiMetrics.IsCompact(size);
    ApplyCompactState();
}
```

`ApplyCompactState()` must:

- set SafeFrame margins to `SafeMargin(Compact)`;
- cap ShowcaseContent width at `UltrawideContentMaximum`;
- set `Compact` on ModalShell, StatBar, InputHint, ContextPrompt, and ToastShell fixtures;
- switch free-standing Label variations between standard and compact pairs;
- set ordinary button minimum targets from `MinimumTarget(Compact)`;
- set Ignition fixture sizes from `IgnitionSize(Compact)`;
- never compute compact mode from a child rectangle.

- [ ] **Step 5: Add deterministic long-text fixtures**

Use these exact fixture values:

```text
Action label: "Bestätigungsaktion mit ausführlicher Beschreibung"
Body: "The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling."
Metadata token: "OBSERVATORY-CALIBRATION-IDENTIFIER-000000000000"
```

Set body wrap to WordSmart. Permit the metadata token to clip only in its metadata fixture and expose the full value through TooltipText.

- [ ] **Step 6: Add explicit focus order**

For the interactive fixtures, assign `FocusNeighborNext`/`FocusNeighborPrevious` in visible order. The chain begins at `%PrimaryButtonFixture`, includes button variants, toggle, tabs, input fixtures, and modal actions, and loops to the first control. Do not add a focus coordinator.

- [ ] **Step 7: Run showcase tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~SiriusUiShowcaseTest"
```

Expected: PASS with one showcase instance resized through seven viewports.

- [ ] **Step 8: Commit**

```bash
git add scenes/ui/showcase/SiriusUiShowcase.tscn \
  scripts/ui/showcase/SiriusUiShowcase.cs \
  tests/ui/showcase/SiriusUiShowcaseTest.cs
git commit -m "feat: add Sirius UI showcase"
```

---

### Task 10: Add the Concise Integration Guide and Run Final Verification

**Files:**
- Create: `docs/ui/hpa-377/README.md`
- Modify only if validation finds a real defect: files created in Tasks 1–9

**Interfaces:**
- Produces a short usage guide linking to the approved design rather than duplicating its rationale.
- Produces final build/test evidence for HPA-377.

- [ ] **Step 1: Write the integration guide**

Create `docs/ui/hpa-377/README.md` with this structure and concrete snippets:

```markdown
# Sirius Theme and Shared Components

Design: [HPA-377 approved design](../../superpowers/specs/2026-08-03-shared-sirius-theme-core-components-design.md)

## Opt in

```csharp
var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
screenRoot.Theme = theme;
```

Do not set `ProjectSettings.gui/theme/custom` during an isolated screen migration.

## Compact authority

Only a root owning a `Viewport` or `SubViewport` calls:

```csharp
var compact = SiriusUiMetrics.IsCompact(safeFrameSize);
```

Hosted controls in the same viewport inherit that value.

## Components

- `SiriusActionButton`: five conventional action variants, optional icon, disabled reason.
- `SiriusPanel`: Content, Feature, HudPlate, Modal.
- `SiriusModalShell`: rectangular content shell; caller/host owns scrim and lifecycle.
- `SiriusStatBar`: HP, MP, EXP presentation only.
- `SiriusInputHint`: binding/device presentation using `InputHintPresenter`.
- `SiriusContextPrompt`: icon + prompt + input hint.
- `SiriusToastShell`: visual shell only; HPA-386 owns queueing/lifetime.

Ignition is the stock `SiriusIgnitionButton` Theme variation, not a component.

## Handoffs

- HPA-541: persisted reduced motion and production-root propagation.
- HPA-386: toast/reward queue, notification lifetime, and short seal confirmations.

## Prohibited patterns

- repeated shared `StyleBoxFlat` resources in migrated screens;
- local palette copies;
- component access to gameplay/settings/save singletons;
- component-owned navigation, queueing, pause, or focus restoration;
- speculative shared controls without a current consumer.
```

Keep the file concise; do not copy the full variation tables or test matrix from the design.

- [ ] **Step 2: Run all HPA-377 focused tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~Sirius"
```

Expected: all Sirius Theme/component/showcase tests pass with zero failures.

- [ ] **Step 3: Build the solution**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: exit 0 with zero errors.

- [ ] **Step 4: Run the complete repository suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Record the final passed/skipped/failed counts in the PR description.

- [ ] **Step 5: Verify project scope did not drift**

```bash
git diff --name-only main...HEAD
```

Expected paths are limited to:

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

- [ ] **Step 7: Commit documentation and any verified corrections**

```bash
git add docs/ui/hpa-377/README.md
git commit -m "docs: add Sirius theme integration guide"
```

- [ ] **Step 8: Update the draft PR description**

Add:

```markdown
## Validation

- Focused HPA-377 tests: <record actual count> passed
- `dotnet build Sirius.sln --no-restore`: 0 errors
- Full suite: <record actual count> passed, <record skipped count> skipped, 0 failed
- Scope audit: no `project.godot` or production-screen changes
```

Use actual command output; do not estimate counts.
