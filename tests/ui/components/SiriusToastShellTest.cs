using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusToastShellTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusToastShell.tscn";

    private SceneTree _sceneTree = null!;
    private SiriusToastShell _shell = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _shell = scene.Instantiate<SiriusToastShell>();
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
    public async Task RefreshPresentation_AppliesWarningPanelIconAndText()
    {
        _shell.Severity = SiriusUiSeverity.Warning;
        _shell.Title = "Signal loss";
        _shell.Message = "The observatory link is unstable.";
        _shell.Compact = false;
        _shell.RefreshPresentation();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var icon = _shell.GetNode<TextureRect>("%SeverityIcon");
        var title = _shell.GetNode<Label>("%TitleLabel");
        var message = _shell.GetNode<Label>("%MessageLabel");

        AssertThat(panel.ThemeTypeVariation).IsEqual(SiriusThemeTypes.WarningPanel);
        AssertThat(panel.Size.X).IsGreater(0f);
        AssertThat(panel.Size.Y).IsGreater(0f);
        AssertThat(icon.Texture).IsNotNull();
        if (icon.Texture is not null)
        {
            AssertThat(icon.Texture.ResourcePath)
                .IsEqual(UiArtCatalog.GetIconPath(UiIconId.Warning, UiIconSize.Default));
        }
        AssertThat(title.Text).IsEqual("Signal loss");
        AssertThat(message.Text).IsEqual("The observatory link is unstable.");
        AssertThat(title.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Section);
        AssertThat(message.ThemeTypeVariation).IsEqual(SiriusThemeTypes.Body);
    }

    [TestCase]
    public async Task RefreshPresentation_UsesCompactTextVariations()
    {
        _shell.Title = "Compact status";
        _shell.Message = "Connection restored.";
        _shell.Compact = true;
        _shell.RefreshPresentation();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var title = _shell.GetNode<Label>("%TitleLabel");
        var message = _shell.GetNode<Label>("%MessageLabel");

        AssertThat(title.Text).IsEqual("Compact status");
        AssertThat(message.Text).IsEqual("Connection restored.");
        AssertThat(title.ThemeTypeVariation).IsEqual(SiriusThemeTypes.SectionCompact);
        AssertThat(message.ThemeTypeVariation).IsEqual(SiriusThemeTypes.BodyCompact);
    }

    [TestCase]
    public void Scene_HasNoScrimTimerOrTweenHelperNodes()
    {
        AssertThat(_shell.GetNodeOrNull<Node>("%Scrim")).IsNull();
        AssertThat(_shell.FindChild("Timer", true, false)).IsNull();
        AssertThat(_shell.FindChild("Tween", true, false)).IsNull();
        AssertThat(ContainsTimer(_shell)).IsFalse();
    }

    [TestCase]
    public async Task MultipleUntouchedShellsInAVBoxContainer_ReceivePositiveNonOverlappingAllocations()
    {
        // A production toast queue instantiates the shell without the
        // showcase's hard-coded custom_minimum_size override. The bare Control
        // root must still propagate its nested panel's minimum so each entry
        // gets a positive, non-overlapping rect inside a VBoxContainer.
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        var vbox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(360, 0),
            Theme = ResourceLoader.Load<Theme>("res://resources/ui/theme/SiriusTheme.tres")
        };
        _sceneTree.Root.AddChild(vbox);
        try
        {
            var toasts = new SiriusToastShell[3];
            for (var i = 0; i < toasts.Length; i++)
            {
                toasts[i] = scene.Instantiate<SiriusToastShell>();
                toasts[i].Title = $"Toast {i}";
                toasts[i].Message = "Status update for the observatory calibration sequence.";
                vbox.AddChild(toasts[i]);
            }

            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var previousBottom = float.NegativeInfinity;
            foreach (var toast in toasts)
            {
                AssertThat(toast.Size.X).IsGreater(0f);
                AssertThat(toast.Size.Y).IsGreater(0f);
                var rect = toast.GetGlobalRect();
                AssertThat(rect.Size.Y).IsGreater(0f);
                AssertThat(rect.Position.Y).IsGreaterEqual(previousBottom);
                previousBottom = rect.End.Y;
            }
        }
        finally
        {
            vbox.QueueFree();
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        }
    }

    [TestCase]
    public async Task LongMessage_WrapsInsideTheBoundedHostWidth()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        var host = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(240, 0),
            Theme = ResourceLoader.Load<Theme>("res://resources/ui/theme/SiriusTheme.tres")
        };
        _sceneTree.Root.AddChild(host);
        try
        {
            var toast = scene.Instantiate<SiriusToastShell>();
            toast.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
            toast.Title = "Calibration";
            toast.Message =
                "The observatory link is unstable and requires immediate " +
                "recalibration before the next celestial route survey can proceed safely.";
            host.AddChild(toast);

            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var title = toast.GetNode<Label>("%TitleLabel");
            var message = toast.GetNode<Label>("%MessageLabel");

            AssertThat(title.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
            AssertThat(message.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
            AssertThat(toast.Size.X).IsLessEqual(host.Size.X + 0.5f);
            AssertThat(toast.Size.Y).IsGreater(0f);
            AssertThat(message.GetLineCount()).IsGreater(1);
        }
        finally
        {
            host.QueueFree();
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        }
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
