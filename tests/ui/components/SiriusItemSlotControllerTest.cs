using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusItemSlotControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusItemSlot.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusItemSlotController _slot = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _slot = scene.Instantiate<SiriusItemSlotController>();
        _sceneTree.Root.AddChild(_slot);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_slot))
            _slot.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void PresentGlyph_UsesNativeCenteredFeatureGlyph()
    {
        _slot!.PresentGlyph(
            UiIconId.Weapon, "", "", "Weapon\nEmpty",
            SiriusItemSlotVisualState.Empty);

        var icon = _slot.GetNode<TextureRect>("%Icon");
        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
            AssertThat(icon.Texture.GetSize()).IsEqual(new Vector2(32, 32));
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
}
