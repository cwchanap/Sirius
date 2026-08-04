using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusThemeTypographyTest : Node
{
    private const string FontProperty = "font";
    private const string FontSizeProperty = "font_size";
    private const string LineSpacingProperty = "line_spacing";
    private const string CinzelFontPath = "res://assets/fonts/cinzel/Cinzel-Variable.ttf";
    private const string NotoRegularFontPath = "res://assets/fonts/noto_sans/NotoSans-Regular.ttf";
    private const string NotoMediumFontPath = "res://assets/fonts/noto_sans/NotoSans-Medium.ttf";
    private const string NotoSemiBoldFontPath = "res://assets/fonts/noto_sans/NotoSans-SemiBold.ttf";
    private const string NotoMonoFontPath = "res://assets/fonts/noto_sans_mono/NotoSansMono-Medium.ttf";

    [TestCase]
    public void Theme_DefinesApprovedLabelVariationsAndSizes()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        AssertLabelVariation(theme, SiriusThemeTypes.Display, 44);
        AssertLabelVariation(theme, SiriusThemeTypes.DisplayCompact, 30);
        AssertLabelVariation(theme, SiriusThemeTypes.Title, 32);
        AssertLabelVariation(theme, SiriusThemeTypes.TitleCompact, 24);
        AssertLabelVariation(theme, SiriusThemeTypes.Section, 20);
        AssertLabelVariation(theme, SiriusThemeTypes.SectionCompact, 17);
        AssertLabelVariation(theme, SiriusThemeTypes.Body, 16);
        AssertLabelVariation(theme, SiriusThemeTypes.BodyCompact, 14);
        AssertLabelVariation(theme, SiriusThemeTypes.Metadata, 14);
        AssertLabelVariation(theme, SiriusThemeTypes.MetadataCompact, 12);
        AssertLabelVariation(theme, SiriusThemeTypes.Numeric, 16);
        AssertLabelVariation(theme, SiriusThemeTypes.NumericCompact, 14);
        AssertLabelVariation(theme, SiriusThemeTypes.Telemetry, 12);
    }

    [TestCase]
    public void Theme_UsesDirectApprovedFontResourcesForTypographyRoles()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        AssertFontFile(CinzelFontPath);
        AssertFontFile(NotoRegularFontPath);
        AssertFontFile(NotoMediumFontPath);
        AssertFontFile(NotoSemiBoldFontPath);
        AssertFontFile(NotoMonoFontPath);

        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Display)))
            .IsEqual(CinzelFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.DisplayCompact)))
            .IsEqual(CinzelFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Title)))
            .IsEqual(NotoSemiBoldFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.TitleCompact)))
            .IsEqual(NotoSemiBoldFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Section)))
            .IsEqual(NotoSemiBoldFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.SectionCompact)))
            .IsEqual(NotoSemiBoldFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Body)))
            .IsEqual(NotoRegularFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.BodyCompact)))
            .IsEqual(NotoMediumFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Metadata)))
            .IsEqual(NotoRegularFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.MetadataCompact)))
            .IsEqual(NotoRegularFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Numeric)))
            .IsEqual(NotoMonoFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.NumericCompact)))
            .IsEqual(NotoMonoFontPath);
        AssertThat(GetFontResourcePath(theme.GetFont(FontProperty, SiriusThemeTypes.Telemetry)))
            .IsEqual(NotoMonoFontPath);
    }

    [TestCase]
    public void Theme_ProvidesDisplayFallbackTrackedTelemetryAndMultilineSpacing()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        var displayFont = theme.GetFont(FontProperty, SiriusThemeTypes.Display);
        AssertThat(displayFont is FontVariation).IsTrue();
        if (displayFont is not FontVariation displayVariation)
            return;

        AssertThat(displayVariation.Fallbacks.Count).IsEqual(1);
        if (displayVariation.Fallbacks.Count != 1)
            return;

        AssertThat(displayVariation.Fallbacks[0].ResourcePath).IsEqual(NotoRegularFontPath);
        AssertThat(displayVariation.VariationOpentype["wght"].AsInt64()).IsEqual(600L);

        var telemetryFont = theme.GetFont(FontProperty, SiriusThemeTypes.Telemetry);
        AssertThat(telemetryFont is FontVariation).IsTrue();
        if (telemetryFont is not FontVariation telemetryVariation)
            return;

        AssertThat(telemetryVariation.SpacingGlyph).IsGreater(0);
        AssertThat(theme.GetConstant(LineSpacingProperty, SiriusThemeTypes.Body)).IsEqual(9);
        AssertThat(theme.GetConstant(LineSpacingProperty, SiriusThemeTypes.BodyCompact)).IsEqual(8);
    }

    private static Theme? LoadTheme()
    {
        var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
        AssertThat(theme).IsNotNull();
        return theme;
    }

    private static void AssertLabelVariation(Theme theme, StringName variation, int expectedSize)
    {
        AssertThat(theme.GetTypeVariationBase(variation).ToString()).IsEqual("Label");
        AssertThat(theme.GetFontSize(FontSizeProperty, variation)).IsEqual(expectedSize);
    }

    private static void AssertFontFile(string path)
    {
        var font = ResourceLoader.Load<FontFile>(path);
        AssertThat(font).IsNotNull();
        if (font is not null)
            AssertThat(font.ResourcePath).IsEqual(path);
    }

    private static string? GetFontResourcePath(Font? font) => font switch
    {
        FontVariation variation when variation.BaseFont is not null => variation.BaseFont.ResourcePath,
        not null => font.ResourcePath,
        _ => null
    };
}
