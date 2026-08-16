using GdUnit4;
using Godot;
using System;
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
    public async Task RefreshPresentation_AppliesErrorPanelIconTitleAndSmallWidth()
    {
        _shell.Title = "Connection interrupted";
        _shell.Severity = SiriusUiSeverity.Error;
        _shell.SizeClass = SiriusModalSizeClass.Small;
        _shell.Compact = false;
        _shell.RefreshPresentation(new Vector2(1280, 720));
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var icon = _shell.GetNode<TextureRect>("%SeverityIcon");
        var title = _shell.GetNode<Label>("%TitleLabel");

        AssertThat(panel.ThemeTypeVariation).IsEqual(SiriusThemeTypes.ErrorPanel);
        AssertThat(panel.CustomMinimumSize.X).IsEqual(420f);
        AssertThat(panel.Size.X).IsGreaterEqual(420f);
        AssertThat(panel.Size.Y).IsGreater(0f);
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
    public async Task RefreshPresentation_UsesCompactMarginsAndTitleVariation()
    {
        _shell.Title = "Compact status";
        _shell.Compact = true;
        _shell.RefreshPresentation(new Vector2(640, 360));
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var title = _shell.GetNode<Label>("%TitleLabel");

        AssertThat(panel.CustomMinimumSize.X).IsEqual(616f);
        AssertThat(panel.Size.X).IsGreaterEqual(616f);
        AssertThat(panel.Size.Y).IsGreater(0f);
        AssertThat(title.Text).IsEqual("Compact status");
        AssertThat(title.ThemeTypeVariation).IsEqual(SiriusThemeTypes.TitleCompact);
    }

    [TestCase]
    public async Task RefreshPresentation_LongTitleWrapsAndStaysWithinTheWidthTarget()
    {
        // A long title must wrap inside the panel instead of driving the
        // panel's horizontal minimum above the computed width target.
        _shell.SizeClass = SiriusModalSizeClass.Small;
        _shell.Compact = false;
        _shell.Title =
            "Observatory calibration report for the celestial route alignment " +
            "procedure across the northern hemisphere survey grid";
        var available = new Vector2(1280, 720);
        var target = Mathf.Min(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Small), available.X * 0.90f);
        _shell.RefreshPresentation(available);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        var title = _shell.GetNode<Label>("%TitleLabel");

        AssertThat(title.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(panel.Size.X).IsLessEqual(target + 0.5f);
        AssertThat(panel.Size.X).IsLess(available.X);
    }

    [TestCase]
    public async Task PropertyMutationAfterConstrainedRefresh_ReusesTheCachedAvailableSize()
    {
        // Constrain to an 800px safe frame; a subsequent property mutation
        // must not recompute against the full viewport and expand the panel.
        _shell.SizeClass = SiriusModalSizeClass.Large;
        _shell.Compact = false;
        var safeFrame = new Vector2(800, 600);
        _shell.RefreshPresentation(safeFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var constrainedTarget = Mathf.Min(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Large), safeFrame.X * 0.90f);
        var panel = _shell.GetNode<PanelContainer>("%Panel");
        AssertThat(panel.CustomMinimumSize.X).IsEqual(constrainedTarget);

        _shell.Title = "Updated after constraint";
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        AssertThat(panel.CustomMinimumSize.X).IsEqual(constrainedTarget);
        AssertThat(panel.CustomMinimumSize.X).IsLess(
            SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Large));
    }

    [TestCase]
    public async Task RefreshPresentation_FullFillsSuppliedSafeWidthAndSurvivesTitleMutation()
    {
        _shell.SizeClass = SiriusModalSizeClass.Full;
        _shell.Compact = false;
        var available = new Vector2(1232, 320);

        _shell.RefreshPresentation(available);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        AssertThat(panel.CustomMinimumSize.X).IsEqual(1232f);

        _shell.Title = "Mira the Merchant";
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        AssertThat(panel.CustomMinimumSize.X).IsEqual(1232f);
    }

    [TestCase]
    public async Task RefreshPresentation_FullCompactUsesSuppliedSafeWidthWithoutSecondMargin()
    {
        _shell.SizeClass = SiriusModalSizeClass.Full;
        _shell.Compact = true;
        _shell.RefreshPresentation(new Vector2(616, 336));
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

        var panel = _shell.GetNode<PanelContainer>("%Panel");
        AssertThat(panel.CustomMinimumSize.X).IsEqual(616f);
    }

    [TestCase]
    public async Task RefreshPresentation_TallBodyAtMinimumViewportFitsAndScrolls()
    {
        var (viewport, shell) = await CreateViewportShell(new Vector2I(640, 360));
        try
        {
            shell.Compact = true;
            shell.Title = "Save Game";

            for (var i = 0; i < 12; i++)
            {
                shell.BodyHost.AddChild(new Label
                {
                    Text = $"Tall body fixture row {i}: representative wrapped modal content",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    CustomMinimumSize = new Vector2(0, 32)
                });
            }

            var cancel = new Button
            {
                Text = "Cancel",
                CustomMinimumSize = SiriusUiMetrics.MinimumTarget(true)
            };
            shell.ActionsHost.AddChild(cancel);

            shell.RefreshPresentation(viewport.Size);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var panel = shell.GetNode<PanelContainer>("%Panel");
            var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
            var scrollBar = bodyScroll.GetVScrollBar();
            var rect = panel.GetGlobalRect();
            var margin = SiriusUiMetrics.SafeMargin(true);

            GD.Print(
                $"[SiriusModalShellTest] 640x360 panelY={rect.Position.Y:F1} " +
                $"panelH={rect.Size.Y:F1} page={scrollBar.Page:F1} max={scrollBar.MaxValue:F1}");

            AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.End.Y).IsLessEqual(360f - margin + 0.5f);
            AssertThat(bodyScroll.Size.Y).IsGreater(0f);
            AssertThat(scrollBar.Page).IsGreater(0f);
            AssertThat(scrollBar.MaxValue).IsGreater(scrollBar.Page);
            AssertThat(cancel.GetGlobalRect().End.Y).IsLessEqual(rect.End.Y + 0.5f);
        }
        finally
        {
            viewport.Free();
        }
    }

    [TestCase]
    public async Task RefreshPresentation_ShortBodyRemainsContentFitAtMinimumViewport()
    {
        var (viewport, shell) = await CreateViewportShell(new Vector2I(640, 360));
        try
        {
            shell.Compact = true;
            shell.Title = "Confirm";
            shell.BodyHost.AddChild(new Label { Text = "Short message" });
            shell.ActionsHost.AddChild(new Button
            {
                Text = "Cancel",
                CustomMinimumSize = SiriusUiMetrics.MinimumTarget(true)
            });

            shell.RefreshPresentation(viewport.Size);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var panel = shell.GetNode<PanelContainer>("%Panel");
            var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
            var scrollBar = bodyScroll.GetVScrollBar();
            AssertThat(panel.Size.Y).IsGreater(0f);
            AssertThat(panel.Size.Y).IsLess(360f - SiriusUiMetrics.SafeMargin(true) * 2f);
            AssertThat(bodyScroll.Size.Y).IsGreater(0f);
            AssertThat(scrollBar.Page).IsGreater(0f);
        }
        finally
        {
            viewport.Free();
        }
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

    private async Task<(SubViewport viewport, SiriusModalShell shell)> CreateViewportShell(
        Vector2I size)
    {
        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = size,
            GuiEmbedSubwindows = true
        };
        _sceneTree.Root.AddChild(viewport);

        var scene = ResourceLoader.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException("Failed to load SiriusModalShell.tscn.");
        var shell = scene.Instantiate<SiriusModalShell>();
        viewport.AddChild(shell);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        return (viewport, shell);
    }
}
