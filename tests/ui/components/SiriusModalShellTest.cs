using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusModalShellTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusModalShell.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusModalShell _shell = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _shell = scene.Instantiate<SiriusModalShell>();
        _sceneTree.Root.AddChild(_shell);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_shell))
            _shell.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void RefreshPresentation_AppliesErrorPanelIconTitleAndSmallWidth()
    {
        _shell.Title = "Connection interrupted";
        _shell.Severity = SiriusUiSeverity.Error;
        _shell.SizeClass = SiriusModalSizeClass.Small;
        _shell.Compact = false;
        _shell.RefreshPresentation(new Vector2(1280, 720));

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var icon = _shell.GetNode<TextureRect>("%SeverityIcon");
        var title = _shell.GetNode<Label>("%TitleLabel");

        AssertThat(panel.ThemeTypeVariation).IsEqual(SiriusThemeTypes.ErrorPanel);
        AssertThat(panel.CustomMinimumSize.X).IsEqual(420f);
        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
        {
            AssertThat(icon.Texture.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Error, UiIconSize.Default));
        }
        AssertThat(title.Text).IsEqual("Connection interrupted");
        AssertThat(title.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Title);
    }

    [TestCase]
    public void RefreshPresentation_UsesCompactMarginsAndTitleVariation()
    {
        _shell.Title = "Compact status";
        _shell.Compact = true;
        _shell.RefreshPresentation(new Vector2(640, 360));

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var title = _shell.GetNode<Label>("%TitleLabel");

        AssertThat(panel.CustomMinimumSize.X).IsEqual(616f);
        AssertThat(title.Text).IsEqual("Compact status");
        AssertThat(title.ThemeTypeVariation).IsEqual(SiriusThemeTypes.TitleCompact);
    }

    [TestCase]
    public void Hosts_AreExposedFromTheAuthoredScene()
    {
        AssertThat(ReferenceEquals(_shell.BodyHost,
            _shell.GetNode<VBoxContainer>("%BodyHost"))).IsTrue();
        AssertThat(ReferenceEquals(_shell.ActionsHost,
            _shell.GetNode<HBoxContainer>("%ActionsHost"))).IsTrue();
    }

    [TestCase]
    public void Scene_HasNoScrimCloseOrLifecycleHelperNodes()
    {
        AssertThat(_shell.GetNodeOrNull<Node>("%Scrim")).IsNull();
        AssertThat(_shell.GetNodeOrNull<Node>("%CloseButton")).IsNull();
        AssertThat(_shell.FindChild("Timer", true, false)).IsNull();
        AssertThat(_shell.FindChild("Tween", true, false)).IsNull();
        AssertThat(ContainsTimer(_shell)).IsFalse();
    }

    private static bool ContainsTimer(Node node)
    {
        if (node is Timer)
            return true;

        foreach (Node child in node.GetChildren())
        {
            if (ContainsTimer(child))
                return true;
        }

        return false;
    }
}
