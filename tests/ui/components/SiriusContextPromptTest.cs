using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusContextPromptTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusContextPrompt.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusContextPrompt _contextPrompt = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _contextPrompt = scene.Instantiate<SiriusContextPrompt>();
        _sceneTree.Root.AddChild(_contextPrompt);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_contextPrompt))
            _contextPrompt.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void Refresh_UsesDialogueIconTalkPromptAndPropagatesInteractAction()
    {
        _contextPrompt.ShowIcon = true;
        _contextPrompt.IconId = UiIconId.Dialogue;
        _contextPrompt.Prompt = "Talk";
        _contextPrompt.Actions = new StringName[] { "interact" };
        _contextPrompt.Compact = false;
        _contextPrompt.Refresh();

        var icon = _contextPrompt.GetNode<TextureRect>("%SemanticIcon");
        var prompt = _contextPrompt.GetNode<Label>("%PromptLabel");
        var inputHint = _contextPrompt.GetNode<SiriusInputHint>("%InputHint");

        AssertThat(icon.Visible).IsTrue();
        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
        {
            AssertThat(icon.Texture.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Dialogue, UiIconSize.Default));
        }
        AssertThat(prompt.Text).IsEqual("Talk");
        AssertThat(prompt.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Body);
        AssertThat(inputHint.Actions.Length).IsEqual(1);
        AssertThat(inputHint.Actions[0]).IsEqual(new StringName("interact"));
        AssertThat(inputHint.Compact).IsFalse();
    }

    [TestCase]
    public void Refresh_ClearsHiddenIconAndUsesCompactBodyPresentation()
    {
        _contextPrompt.ShowIcon = false;
        _contextPrompt.IconId = UiIconId.Dialogue;
        _contextPrompt.Prompt = "Talk";
        _contextPrompt.Actions = new StringName[] { "interact" };
        _contextPrompt.Compact = true;
        _contextPrompt.Refresh();

        var icon = _contextPrompt.GetNode<TextureRect>("%SemanticIcon");
        var prompt = _contextPrompt.GetNode<Label>("%PromptLabel");
        var inputHint = _contextPrompt.GetNode<SiriusInputHint>("%InputHint");

        AssertThat(icon.Visible).IsFalse();
        AssertThat(icon.Texture).IsNull();
        AssertThat(prompt.Text).IsEqual("Talk");
        AssertThat(prompt.ThemeTypeVariation).IsEqual(SiriusThemeTypes.BodyCompact);
        AssertThat(inputHint.Compact).IsTrue();
    }
}
