using GdUnit4;
using Godot;
using System;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiContractsTest : Node
{
    [TestCase]
    public void ThemeTypes_ExposeExactStableNames()
    {
        AssertThat(SiriusThemeTypes.ResourcePath)
            .IsEqual("res://resources/ui/theme/SiriusTheme.tres");

        AssertThat(SiriusThemeTypes.Display.ToString()).IsEqual("SiriusDisplay");
        AssertThat(SiriusThemeTypes.DisplayCompact.ToString()).IsEqual("SiriusDisplayCompact");
        AssertThat(SiriusThemeTypes.Title.ToString()).IsEqual("SiriusTitle");
        AssertThat(SiriusThemeTypes.TitleCompact.ToString()).IsEqual("SiriusTitleCompact");
        AssertThat(SiriusThemeTypes.Section.ToString()).IsEqual("SiriusSection");
        AssertThat(SiriusThemeTypes.SectionCompact.ToString()).IsEqual("SiriusSectionCompact");
        AssertThat(SiriusThemeTypes.Body.ToString()).IsEqual("SiriusBody");
        AssertThat(SiriusThemeTypes.BodyCompact.ToString()).IsEqual("SiriusBodyCompact");
        AssertThat(SiriusThemeTypes.Metadata.ToString()).IsEqual("SiriusMetadata");
        AssertThat(SiriusThemeTypes.MetadataCompact.ToString()).IsEqual("SiriusMetadataCompact");
        AssertThat(SiriusThemeTypes.Numeric.ToString()).IsEqual("SiriusNumeric");
        AssertThat(SiriusThemeTypes.NumericCompact.ToString()).IsEqual("SiriusNumericCompact");
        AssertThat(SiriusThemeTypes.Telemetry.ToString()).IsEqual("SiriusTelemetry");

        AssertThat(SiriusThemeTypes.PrimaryButton.ToString()).IsEqual("SiriusPrimaryButton");
        AssertThat(SiriusThemeTypes.SecondaryButton.ToString()).IsEqual("SiriusSecondaryButton");
        AssertThat(SiriusThemeTypes.TertiaryButton.ToString()).IsEqual("SiriusTertiaryButton");
        AssertThat(SiriusThemeTypes.WarningButton.ToString()).IsEqual("SiriusWarningButton");
        AssertThat(SiriusThemeTypes.DestructiveButton.ToString())
            .IsEqual("SiriusDestructiveButton");
        AssertThat(SiriusThemeTypes.IgnitionButton.ToString()).IsEqual("SiriusIgnitionButton");

        AssertThat(SiriusThemeTypes.ContentPanel.ToString()).IsEqual("SiriusContentPanel");
        AssertThat(SiriusThemeTypes.FeaturePanel.ToString()).IsEqual("SiriusFeaturePanel");
        AssertThat(SiriusThemeTypes.HudPlate.ToString()).IsEqual("SiriusHudPlate");
        AssertThat(SiriusThemeTypes.ModalPanel.ToString()).IsEqual("SiriusModalPanel");
        AssertThat(SiriusThemeTypes.WarningPanel.ToString()).IsEqual("SiriusWarningPanel");
        AssertThat(SiriusThemeTypes.ErrorPanel.ToString()).IsEqual("SiriusErrorPanel");
        AssertThat(SiriusThemeTypes.Scrim.ToString()).IsEqual("SiriusScrim");
        AssertThat(SiriusThemeTypes.ChildScrim.ToString()).IsEqual("SiriusChildScrim");

        AssertThat(SiriusThemeTypes.HpBar.ToString()).IsEqual("SiriusHpBar");
        AssertThat(SiriusThemeTypes.MpBar.ToString()).IsEqual("SiriusMpBar");
        AssertThat(SiriusThemeTypes.ExpBar.ToString()).IsEqual("SiriusExpBar");
        AssertThat(SiriusThemeTypes.InvalidBar.ToString()).IsEqual("SiriusInvalidBar");
    }

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
            SiriusModalSizeClass.Large,
            SiriusModalSizeClass.Full);
        AssertThat(Enum.GetValues<SiriusStatBarKind>()).ContainsExactly(
            SiriusStatBarKind.Health,
            SiriusStatBarKind.Mana,
            SiriusStatBarKind.Experience);
    }

    [TestCase]
    public void Mappings_AreExactAndUnknownValuesThrow()
    {
        AssertThat(SiriusUiSeverity.Info.ToIconId()).IsEqual(UiIconId.Info);
        AssertThat(SiriusUiSeverity.Success.ToIconId()).IsEqual(UiIconId.Confirm);
        AssertThat(SiriusUiSeverity.Warning.ToIconId()).IsEqual(UiIconId.Warning);
        AssertThat(SiriusUiSeverity.Error.ToIconId()).IsEqual(UiIconId.Error);
        AssertThat(SiriusUiSeverity.Info.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.ModalPanel);
        AssertThat(SiriusUiSeverity.Success.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.ModalPanel);
        AssertThat(SiriusUiSeverity.Warning.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.WarningPanel);
        AssertThat(SiriusUiSeverity.Error.ToModalPanelThemeType())
            .IsEqual(SiriusThemeTypes.ErrorPanel);
        AssertThat(SiriusUiSeverity.Info.ToToastPanelThemeType())
            .IsEqual(SiriusThemeTypes.ContentPanel);
        AssertThat(SiriusUiSeverity.Success.ToToastPanelThemeType())
            .IsEqual(SiriusThemeTypes.FeaturePanel);
        AssertThat(SiriusUiSeverity.Warning.ToToastPanelThemeType())
            .IsEqual(SiriusThemeTypes.WarningPanel);
        AssertThat(SiriusUiSeverity.Error.ToToastPanelThemeType())
            .IsEqual(SiriusThemeTypes.ErrorPanel);
        AssertThat(SiriusStatBarKind.Health.ToThemeType()).IsEqual(SiriusThemeTypes.HpBar);
        AssertThat(SiriusStatBarKind.Mana.ToThemeType()).IsEqual(SiriusThemeTypes.MpBar);
        AssertThat(SiriusStatBarKind.Experience.ToThemeType()).IsEqual(SiriusThemeTypes.ExpBar);
        AssertThat(SiriusStatBarKind.Health.ToIconId()).IsEqual(UiIconId.Health);
        AssertThat(SiriusStatBarKind.Mana.ToIconId()).IsEqual(UiIconId.Mana);
        AssertThat(SiriusStatBarKind.Experience.ToIconId()).IsEqual(UiIconId.Experience);

        AssertThrown(() => ((SiriusUiSeverity)99).ToIconId())
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => ((SiriusUiSeverity)99).ToModalPanelThemeType())
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => ((SiriusUiSeverity)99).ToToastPanelThemeType())
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => ((SiriusStatBarKind)99).ToThemeType())
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => ((SiriusStatBarKind)99).ToIconId())
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void Metrics_MatchApprovedBreakpointsSizesAndViewports()
    {
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(799, 720))).IsTrue();
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(1280, 449))).IsTrue();
        AssertThat(SiriusUiMetrics.IsCompact(new Vector2(800, 450))).IsFalse();
        AssertThat(SiriusUiMetrics.SafeMargin(false)).IsEqual(24);
        AssertThat(SiriusUiMetrics.SafeMargin(true)).IsEqual(12);
        AssertThat(SiriusUiMetrics.MinimumTarget(false)).IsEqual(new Vector2(44, 44));
        AssertThat(SiriusUiMetrics.MinimumTarget(true)).IsEqual(new Vector2(40, 40));
        AssertThat(SiriusUiMetrics.IgnitionSize(false)).IsEqual(new Vector2(96, 96));
        AssertThat(SiriusUiMetrics.IgnitionSize(true)).IsEqual(new Vector2(80, 80));
        AssertThat(SiriusUiMetrics.TooltipMaximum(false)).IsEqual(360);
        AssertThat(SiriusUiMetrics.TooltipMaximum(true)).IsEqual(280);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Small)).IsEqual(420);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Medium)).IsEqual(640);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Large)).IsEqual(960);
        AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Full))
            .IsEqual((int)SiriusUiMetrics.MaximumContentWidth);
        AssertThrown(() => SiriusUiMetrics.ModalWidth((SiriusModalSizeClass)99))
            .IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThat(SiriusUiMetrics.VerificationViewports).ContainsExactly(
            new Vector2I(640, 360), new Vector2I(1024, 768),
            new Vector2I(1280, 720), new Vector2I(1440, 900),
            new Vector2I(1920, 1080), new Vector2I(2560, 1080),
            new Vector2I(2560, 1440));
        AssertThat(SiriusUiMetrics.FocusVerificationViewports).ContainsExactly(
            new Vector2I(640, 360), new Vector2I(1280, 720));
    }

    [TestCase]
    public void Motion_ContainsOnlyHpa377DurationsAndBehavior()
    {
        AssertThat(SiriusMotion.EntrySeconds).IsEqualApprox(0.220, 0.001);
        AssertThat(SiriusMotion.ExitSeconds).IsEqualApprox(0.180, 0.001);
        AssertThat(SiriusMotion.ReducedOpacitySeconds).IsEqualApprox(0.100, 0.001);
        AssertThat(SiriusMotion.EntryTransition).IsEqual(Tween.TransitionType.Cubic);
        AssertThat(SiriusMotion.EntryEase).IsEqual(Tween.EaseType.Out);
        AssertThat(SiriusMotion.ExitTransition).IsEqual(Tween.TransitionType.Quad);
        AssertThat(SiriusMotion.ExitEase).IsEqual(Tween.EaseType.In);
        AssertThat(SiriusMotion.Duration(false, true)).IsEqualApprox(0.220, 0.001);
        AssertThat(SiriusMotion.Duration(false, false)).IsEqualApprox(0.180, 0.001);
        AssertThat(SiriusMotion.Duration(true, true)).IsEqualApprox(0.100, 0.001);
        AssertThat(SiriusMotion.Duration(true, false)).IsEqualApprox(0.100, 0.001);
        AssertThat(SiriusMotion.UseTransform(false)).IsTrue();
        AssertThat(SiriusMotion.UseTransform(true)).IsFalse();
    }
}
