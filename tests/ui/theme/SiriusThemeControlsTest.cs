using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusThemeControlsTest : Node
{
    private static readonly StringName[] ButtonStates =
    [
        "normal", "hover", "pressed", "hover_pressed", "focus", "disabled"
    ];

    private static readonly StringName[] ButtonVariations =
    [
        SiriusThemeTypes.PrimaryButton,
        SiriusThemeTypes.SecondaryButton,
        SiriusThemeTypes.TertiaryButton,
        SiriusThemeTypes.WarningButton,
        SiriusThemeTypes.DestructiveButton,
        SiriusThemeTypes.IgnitionButton
    ];

    private static readonly StringName[] PanelVariations =
    [
        SiriusThemeTypes.ContentPanel,
        SiriusThemeTypes.FeaturePanel,
        SiriusThemeTypes.HudPlate,
        SiriusThemeTypes.ModalPanel,
        SiriusThemeTypes.WarningPanel,
        SiriusThemeTypes.ErrorPanel,
        SiriusThemeTypes.Scrim,
        SiriusThemeTypes.ChildScrim
    ];

    private static readonly StringName[] StatBarVariations =
    [
        SiriusThemeTypes.HpBar,
        SiriusThemeTypes.MpBar,
        SiriusThemeTypes.ExpBar,
        SiriusThemeTypes.InvalidBar
    ];

    private const string IgnitionSealPath = "res://assets/sprites/ui/ornaments/ignition_seal.png";
    private const string FocusHaloPath = "res://assets/sprites/ui/ornaments/focus_halo.png";
    private const string PanelStyle = "panel";

    [TestCase]
    public void Theme_DefinesEveryApprovedButtonState()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        foreach (var variation in ButtonVariations)
        {
            AssertThat(theme.GetTypeVariationBase(variation).ToString()).IsEqual("Button");
            foreach (var state in ButtonStates)
                AssertThat(theme.HasStylebox(state, variation)).IsTrue();

            AssertThat(theme.HasColor("font_disabled_color", variation)).IsTrue();
            AssertThat(theme.GetColor("font_disabled_color", variation).A).IsEqualApprox(0.45f, 0.001f);
            AssertThat(theme.HasColor("icon_disabled_color", variation)).IsTrue();
            AssertThat(theme.GetColor("icon_disabled_color", variation).A).IsEqualApprox(0.45f, 0.001f);
        }
    }

    [TestCase]
    public void Theme_UsesRequiredOrnamentsForIgnitionStates()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        AssertStyleBoxTexturePath(theme, "normal", IgnitionSealPath);
        AssertStyleBoxTexturePath(theme, "hover", IgnitionSealPath);
        AssertStyleBoxTexturePath(theme, "pressed", IgnitionSealPath);
        AssertStyleBoxTexturePath(theme, "hover_pressed", IgnitionSealPath);
        AssertStyleBoxTexturePath(theme, "disabled", IgnitionSealPath);
        AssertStyleBoxTexturePath(theme, "focus", FocusHaloPath);
    }

    [TestCase]
    public void Theme_DefinesApprovedPanelsBarsAndScrims()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        foreach (var variation in PanelVariations)
        {
            AssertThat(theme.GetTypeVariationBase(variation).ToString()).IsEqual("Panel");
            AssertThat(theme.HasStylebox(PanelStyle, variation)).IsTrue();
        }

        foreach (var variation in StatBarVariations)
        {
            AssertThat(theme.GetTypeVariationBase(variation).ToString()).IsEqual("ProgressBar");
            AssertThat(theme.HasStylebox("background", variation)).IsTrue();
            AssertThat(theme.HasStylebox("fill", variation)).IsTrue();
        }
    }

    [TestCase]
    public void Theme_UsesExactApprovedScrimAlphas()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        AssertScrimColor(theme, SiriusThemeTypes.Scrim, 0.58f);
        AssertScrimColor(theme, SiriusThemeTypes.ChildScrim, 0.72f);
    }

    [TestCase]
    public void Theme_StylesApprovedNativeControls()
    {
        var theme = LoadTheme();
        if (theme is null)
            return;

        AssertStyleBoxes(theme, "TabBar", "tab_unselected", "tab_selected", "tab_hovered", "tab_focus", "tab_disabled");
        AssertStyleBoxes(theme, "TabContainer", PanelStyle);
        AssertStyleBoxes(theme, "TooltipPanel", PanelStyle);
        AssertThat(theme.HasColor("font_color", "TooltipLabel")).IsTrue();
        AssertStyleBoxes(theme, "ScrollContainer", PanelStyle, "focus");
        AssertScrollBarStyles(theme, "HScrollBar");
        AssertScrollBarStyles(theme, "VScrollBar");
    }

    [TestCase]
    public void RequiredThemeResources_ExistAndLoadDirectly()
    {
        string[] requiredResources =
        [
            IgnitionSealPath,
            FocusHaloPath,
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
    }

    private static Theme? LoadTheme()
    {
        var theme = ResourceLoader.Load<Theme>(SiriusThemeTypes.ResourcePath);
        AssertThat(theme).IsNotNull();
        return theme;
    }

    private static void AssertStyleBoxTexturePath(Theme theme, StringName state, string expectedPath)
    {
        var styleBox = theme.GetStylebox(state, SiriusThemeTypes.IgnitionButton);
        AssertThat(styleBox is StyleBoxTexture).IsTrue();
        if (styleBox is not StyleBoxTexture textureStyleBox)
            return;

        AssertThat(textureStyleBox.Texture).IsNotNull();
        if (textureStyleBox.Texture is not null)
            AssertThat(textureStyleBox.Texture.ResourcePath).IsEqual(expectedPath);
    }

    private static void AssertScrimColor(Theme theme, StringName variation, float alpha)
    {
        var styleBox = theme.GetStylebox(PanelStyle, variation);
        AssertThat(styleBox is StyleBoxFlat).IsTrue();
        if (styleBox is not StyleBoxFlat flatStyleBox)
            return;

        AssertThat(flatStyleBox.BgColor.R).IsEqualApprox(0.019608f, 0.001f);
        AssertThat(flatStyleBox.BgColor.G).IsEqualApprox(0.027451f, 0.001f);
        AssertThat(flatStyleBox.BgColor.B).IsEqualApprox(0.078431f, 0.001f);
        AssertThat(flatStyleBox.BgColor.A).IsEqualApprox(alpha, 0.001f);
    }

    private static void AssertStyleBoxes(Theme theme, StringName type, params StringName[] names)
    {
        foreach (var name in names)
            AssertThat(theme.HasStylebox(name, type)).IsTrue();
    }

    private static void AssertScrollBarStyles(Theme theme, StringName type) =>
        AssertStyleBoxes(theme, type, "scroll", "scroll_focus", "grabber", "grabber_highlight", "grabber_pressed");
}
