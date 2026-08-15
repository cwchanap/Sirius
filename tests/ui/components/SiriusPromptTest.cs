using GdUnit4;
using Godot;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusPromptTest : Node
{
    private const string ScenePath = "res://scenes/ui/components/SiriusPrompt.tscn";

    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private SubViewport? _viewport;
    private SiriusPrompt? _prompt;

    [BeforeTest]
    public void SetUp() => _sceneTree = (SceneTree)Engine.GetMainLoop();

    [AfterTest]
    public async Task TearDown()
    {
        if (_prompt != null && GodotObject.IsInstanceValid(_prompt))
            _prompt.QueueFree();
        if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
            _viewport.QueueFree();
        if (_container != null && GodotObject.IsInstanceValid(_container))
            _container.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        _prompt = null;
        _viewport = null;
        _container = null;
    }

    [TestCase]
    public async Task Variants_MapSeverityButtonsThemeAndInitialFocus()
    {
        await Instantiate();

        var cases = new[]
        {
            (SiriusPromptVariant.DestructiveConfirmation,
                SiriusUiSeverity.Warning, true, SiriusThemeTypes.DestructiveButton),
            (SiriusPromptVariant.Warning,
                SiriusUiSeverity.Warning, false, SiriusThemeTypes.PrimaryButton),
            (SiriusPromptVariant.RecoverableError,
                SiriusUiSeverity.Error, false, SiriusThemeTypes.PrimaryButton)
        };

        foreach (var (variant, severity, showsCancel, primaryTheme) in cases)
        {
            _prompt!.Configure(variant, "Notice title", "Notice body", "Confirm");

            var shell = _prompt.GetNode<SiriusModalShell>("%ModalShell");
            var primary = _prompt.GetNode<Button>("%PrimaryButton");
            var cancel = _prompt.GetNode<Button>("%CancelButton");

            AssertThat(shell.Severity).IsEqual(severity);
            AssertThat(primary.Text).IsEqual("Confirm");
            AssertThat(primary.ThemeTypeVariation).IsEqual(primaryTheme);
            AssertThat(cancel.Text).IsEqual("Cancel");
            AssertThat(cancel.ThemeTypeVariation).IsEqual(SiriusThemeTypes.SecondaryButton);
            AssertThat(cancel.Visible).IsEqual(showsCancel);
            AssertThat(_prompt.InitialFocusTarget).IsEqual(showsCancel ? cancel : primary);
        }
    }

    [TestCase]
    public async Task Destructive_PrimaryThenCancelEmitsOnlyPrimaryOnce()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.DestructiveConfirmation,
            "Delete save", "Delete this save?", "Delete");

        var primaryCount = 0;
        var cancelCount = 0;
        _prompt.PrimaryRequested += () => primaryCount++;
        _prompt.CancelRequested += () => cancelCount++;

        _prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
        _prompt.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);

        AssertThat(primaryCount).IsEqual(1);
        AssertThat(cancelCount).IsEqual(0);
    }

    [TestCase]
    public async Task Destructive_CancelThenPrimaryEmitsOnlyCancelOnce()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.DestructiveConfirmation,
            "Delete save", "Delete this save?", "Delete");

        var primaryCount = 0;
        var cancelCount = 0;
        _prompt.PrimaryRequested += () => primaryCount++;
        _prompt.CancelRequested += () => cancelCount++;

        _prompt.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);
        _prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);

        AssertThat(cancelCount).IsEqual(1);
        AssertThat(primaryCount).IsEqual(0);
    }

    [TestCase]
    public async Task Warning_RequestCancelEmitsPrimaryOnce()
    {
        await Instantiate();
        _prompt!.Configure(SiriusPromptVariant.Warning, "Warning", "Careful", "Continue");

        var primaryCount = 0;
        var cancelCount = 0;
        _prompt.PrimaryRequested += () => primaryCount++;
        _prompt.CancelRequested += () => cancelCount++;

        _prompt.RequestCancel();
        _prompt.RequestCancel();

        AssertThat(primaryCount).IsEqual(1);
        AssertThat(cancelCount).IsEqual(0);
    }

    [TestCase]
    public async Task RecoverableError_RequestCancelEmitsPrimaryOnce()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.RecoverableError,
            "Connection lost", "Reconnect?", "Retry");

        var primaryCount = 0;
        var cancelCount = 0;
        _prompt.PrimaryRequested += () => primaryCount++;
        _prompt.CancelRequested += () => cancelCount++;

        _prompt.RequestCancel();
        _prompt.RequestCancel();

        AssertThat(primaryCount).IsEqual(1);
        AssertThat(cancelCount).IsEqual(0);
    }

    [TestCase]
    public async Task RepeatedPrimaryPress_EmitsOnce()
    {
        await Instantiate();
        _prompt!.Configure(SiriusPromptVariant.Warning, "Retry", "Connection lost", "Retry");

        var primaryCount = 0;
        _prompt.PrimaryRequested += () => primaryCount++;

        var primary = _prompt.GetNode<Button>("%PrimaryButton");
        primary.EmitSignal(Button.SignalName.Pressed);
        primary.EmitSignal(Button.SignalName.Pressed);

        AssertThat(primaryCount).IsEqual(1);
    }

    [TestCase]
    public async Task CompactViewport_UsesCompactShellAndMinimumTargets()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.DestructiveConfirmation,
            "Quit", "Quit to menu?", "Quit");

        var shell = _prompt.GetNode<SiriusModalShell>("%ModalShell");
        var primary = _prompt.GetNode<Button>("%PrimaryButton");
        var cancel = _prompt.GetNode<Button>("%CancelButton");
        var target = SiriusUiMetrics.MinimumTarget(true);

        AssertThat(shell.Compact).IsTrue();
        AssertThat(primary.CustomMinimumSize.Y).IsEqual(target.Y);
        AssertThat(cancel.CustomMinimumSize.Y).IsEqual(target.Y);
    }

    [TestCase]
    public async Task CrossingCompactToStandard_RefreshesShellAndTargets()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.DestructiveConfirmation,
            "Quit", "Quit to menu?", "Quit");

        var shell = _prompt!.GetNode<SiriusModalShell>("%ModalShell");
        var primary = _prompt.GetNode<Button>("%PrimaryButton");
        var cancel = _prompt.GetNode<Button>("%CancelButton");
        AssertThat(shell.Compact).IsTrue();

        await ResizeToStandardViewport();

        var target = SiriusUiMetrics.MinimumTarget(false);
        AssertThat(shell.Compact).IsFalse();
        AssertThat(primary.CustomMinimumSize.Y).IsEqual(target.Y);
        AssertThat(cancel.CustomMinimumSize.Y).IsEqual(target.Y);
    }

    [TestCase]
    public async Task LongMessage_MinimumViewportStaysInsideShellAndCanScroll()
    {
        await Instantiate();
        _prompt!.Configure(
            SiriusPromptVariant.Warning,
            "Connection interrupted",
            string.Join(" ", Enumerable.Repeat(
                "The observatory link to the celestial route relay was interrupted " +
                "before the calibration report could be synchronized.",
                8)),
            "Retry");

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var shell = _prompt!.GetNode<SiriusModalShell>("%ModalShell");
        var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
        var scrollBar = bodyScroll.GetVScrollBar();
        var rect = shell.GetNode<PanelContainer>("%Panel").GetGlobalRect();
        var margin = SiriusUiMetrics.SafeMargin(true);

        AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
        AssertThat(rect.End.Y).IsLessEqual(360f - margin + 0.5f);
        AssertThat(bodyScroll.Size.Y).IsGreater(0f);
        AssertThat(scrollBar.Page).IsGreater(0f);
        AssertThat(scrollBar.MaxValue).IsGreater(scrollBar.Page);
    }

    [TestCase]
    public void Scene_UsesModalShellAndContainsNoAcceptDialog()
    {
        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        var prompt = scene.Instantiate<SiriusPrompt>();
        try
        {
            AssertThat(prompt.GetNodeOrNull<SiriusModalShell>("%ModalShell")).IsNotNull();
            AssertThat(prompt.FindChild("AcceptDialog", true, false)).IsNull();
            AssertThat(ContainsAcceptDialog(prompt)).IsFalse();
        }
        finally
        {
            prompt.Free();
        }
    }

    private static bool ContainsAcceptDialog(Node node)
    {
        if (node is AcceptDialog)
            return true;

        foreach (var child in node.GetChildren())
        {
            if (ContainsAcceptDialog(child))
                return true;
        }

        return false;
    }

    private async Task Instantiate()
    {
        var size = new Vector2I(640, 360);
        _container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(_container);

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        _container.AddChild(_viewport);

        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _prompt = scene.Instantiate<SiriusPrompt>();
        _viewport.AddChild(_prompt);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private async Task ResizeToStandardViewport()
    {
        _container!.Size = new Vector2I(1280, 720);
        _viewport!.Size = new Vector2I(1280, 720);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
