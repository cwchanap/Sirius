using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusStatBarTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusStatBar.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusStatBar _statBar = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _statBar = scene.Instantiate<SiriusStatBar>();
        _sceneTree.Root.AddChild(_statBar);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_statBar))
            _statBar.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void RefreshPresentation_UsesHealthPresentationForLowValue()
    {
        _statBar.Kind = SiriusStatBarKind.Health;
        _statBar.Current = 20;
        _statBar.Maximum = 100;
        _statBar.Label = "Health";
        _statBar.Compact = false;
        _statBar.RefreshPresentation();

        var icon = _statBar.GetNode<TextureRect>("%Icon");
        var name = _statBar.GetNode<Label>("%NameLabel");
        var value = _statBar.GetNode<Label>("%ValueLabel");
        var bar = _statBar.GetNode<ProgressBar>("%Bar");
        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
        {
            AssertThat(icon.Texture.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Health, UiIconSize.Metadata));
        }
        AssertThat(name.Text).IsEqual("Health");
        AssertThat(name.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Metadata);
        AssertThat(value.Text).IsEqual("20 / 100");
        AssertThat(value.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Numeric);
        AssertThat(bar.ThemeTypeVariation).IsEqual(SiriusThemeTypes.HpBar);
        AssertThat(bar.MaxValue).IsEqual(100.0);
        AssertThat(bar.Value).IsEqual(20.0);
        AssertThat(bar.ShowPercentage).IsFalse();
        AssertThat(state.Text).IsEqual("Low");
        AssertThat(state.Visible).IsTrue();
        AssertThat(state.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Metadata);
    }

    [TestCase]
    public void RefreshPresentation_ClampsOverflowFillButPreservesCallerValue()
    {
        _statBar.Kind = SiriusStatBarKind.Mana;
        _statBar.Current = 120;
        _statBar.Maximum = 100;
        _statBar.RefreshPresentation();

        var bar = _statBar.GetNode<ProgressBar>("%Bar");
        var value = _statBar.GetNode<Label>("%ValueLabel");
        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(bar.ThemeTypeVariation).IsEqual(SiriusThemeTypes.MpBar);
        AssertThat(bar.MaxValue).IsEqual(100.0);
        AssertThat(bar.Value).IsEqual(100.0);
        AssertThat(value.Text).IsEqual("120 / 100");
        AssertThat(state.Text).IsEqual("Overflow");
        AssertThat(state.Visible).IsTrue();
    }

    [TestCase]
    public void RefreshPresentation_ClampsNegativeFillButPreservesCallerValue()
    {
        _statBar.Kind = SiriusStatBarKind.Experience;
        _statBar.Current = -5;
        _statBar.Maximum = 100;
        _statBar.RefreshPresentation();

        var bar = _statBar.GetNode<ProgressBar>("%Bar");
        var value = _statBar.GetNode<Label>("%ValueLabel");
        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(bar.ThemeTypeVariation).IsEqual(SiriusThemeTypes.ExpBar);
        AssertThat(bar.MaxValue).IsEqual(100.0);
        AssertThat(bar.Value).IsEqual(0.0);
        AssertThat(value.Text).IsEqual("-5 / 100");
        AssertThat(state.Text).IsEqual("Invalid value");
        AssertThat(state.Visible).IsTrue();
    }

    [TestCase]
    public void RefreshPresentation_UsesInvalidBarForNonPositiveMaximum()
    {
        _statBar.Kind = SiriusStatBarKind.Health;
        _statBar.Current = 10;
        _statBar.Maximum = 0;
        _statBar.RefreshPresentation();

        var bar = _statBar.GetNode<ProgressBar>("%Bar");
        var value = _statBar.GetNode<Label>("%ValueLabel");
        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(bar.ThemeTypeVariation).IsEqual(SiriusThemeTypes.InvalidBar);
        AssertThat(bar.MinValue).IsEqual(0.0);
        AssertThat(bar.MaxValue).IsEqual(1.0);
        AssertThat(bar.Value).IsEqual(0.0);
        AssertThat(value.Text).IsEqual("10 / 0");
        AssertThat(state.Text).IsEqual("Invalid maximum");
        AssertThat(state.Visible).IsTrue();
    }

    [TestCase]
    public void RefreshPresentation_PrioritizesInvalidMaximumOverNegativeCurrent()
    {
        _statBar.Current = -5;
        _statBar.Maximum = 0;
        _statBar.RefreshPresentation();

        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(state.Text).IsEqual("Invalid maximum");
    }

    [TestCase]
    public void RefreshPresentation_UsesCompactNestedLabelVariations()
    {
        _statBar.Current = 50;
        _statBar.Maximum = 100;
        _statBar.Label = "Mana";
        _statBar.Compact = true;
        _statBar.RefreshPresentation();

        var name = _statBar.GetNode<Label>("%NameLabel");
        var value = _statBar.GetNode<Label>("%ValueLabel");
        var state = _statBar.GetNode<Label>("%StateLabel");

        AssertThat(name.ThemeTypeVariation).IsEqual(SiriusThemeTypes.MetadataCompact);
        AssertThat(value.ThemeTypeVariation).IsEqual(SiriusThemeTypes.NumericCompact);
        AssertThat(state.ThemeTypeVariation).IsEqual(SiriusThemeTypes.MetadataCompact);
        AssertThat(state.Text).IsEqual("Normal");
        AssertThat(state.Visible).IsFalse();
    }
}
