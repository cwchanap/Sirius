using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ExplorationHudControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/ExplorationHud.tscn";
    private const string HeroSheetPath =
        "res://assets/sprites/characters/player_hero/sprite_sheet.png";

    private static readonly string[] RequiredNodes =
    {
        "%SafeFrame",
        "%HeroOrbitArc",
        "%HeroPlate",
        "%Portrait",
        "%PlayerName",
        "%PlayerLevel",
        "%HealthBar",
        "%ManaBar",
        "%ExperienceBar",
        "%PromptPlate",
        "%ContextPrompt",
        "%PromptConnector",
        "%TransientPlate",
        "%TransientLabel",
        "%TransientTimer"
    };

    private SceneTree _sceneTree = null!;
    private readonly List<SubViewportContainer> _containers = new();

    [BeforeTest]
    public void Setup()
        => _sceneTree = (SceneTree)Engine.GetMainLoop();

    [AfterTest]
    public async Task Cleanup()
    {
        foreach (var container in _containers)
        {
            if (GodotObject.IsInstanceValid(container))
                container.QueueFree();
        }

        _containers.Clear();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void SceneOwnsRequiredHudThemeAndSingleFramePortrait()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();

        var hud = packed!.Instantiate<ExplorationHudController>();
        try
        {
            foreach (var path in RequiredNodes)
                AssertThat(hud.GetNodeOrNull(path)).IsNotNull();

            AssertThat(hud.Theme).IsNotNull();
            AssertThat(hud.Theme.ResourcePath).IsEqual(SiriusThemeTypes.ResourcePath);
            AssertThat(SiriusUiMetrics.MaximumContentWidth).IsEqual(1600f);

            var portrait = hud.GetNode<TextureRect>("%Portrait");
            AssertThat(portrait.Texture is AtlasTexture).IsTrue();
            var atlas = (AtlasTexture)portrait.Texture;
            AssertThat(atlas.Atlas).IsNotNull();
            AssertThat(atlas.Atlas.ResourcePath).IsEqual(HeroSheetPath);
            AssertThat(atlas.Region).IsEqual(new Rect2(0, 0, 96, 96));

            AssertThat(hud.GetNode<PanelContainer>("%HeroPlate").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.HudPlate);
            AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.ExpBar);
        }
        finally
        {
            hud.Free();
        }
    }

    [TestCase]
    public void SharedSafeFrameInsetsMatchApprovedSizes()
    {
        var compact = SiriusUiMetrics.SafeFrameInsets(new Vector2(640, 360));
        AssertThat(compact.Compact).IsTrue();
        AssertThat(compact.Margin).IsEqual(12f);
        AssertThat(compact.SideInset).IsEqual(12f);

        var standard = SiriusUiMetrics.SafeFrameInsets(new Vector2(1280, 720));
        AssertThat(standard.Compact).IsFalse();
        AssertThat(standard.Margin).IsEqual(24f);
        AssertThat(standard.SideInset).IsEqual(24f);

        var ultrawide = SiriusUiMetrics.SafeFrameInsets(new Vector2(2560, 1080));
        AssertThat(ultrawide.Compact).IsFalse();
        AssertThat(ultrawide.Margin).IsEqual(24f);
        AssertThat(ultrawide.SideInset).IsEqual(480f);
    }

    [TestCase]
    public async Task ApplyPlayerStateBindsStatsAndUsesThinExpBar()
    {
        var hud = await InstantiateHud(new Vector2I(1280, 720));

        hud.ApplyPlayerState(new ExplorationHudPlayerState(
            "Aster", 7, 73, 120, 21, 50, 340, 500));

        AssertThat(hud.GetNode<Label>("%PlayerName").Text).IsEqual("Aster");
        AssertThat(hud.GetNode<Label>("%PlayerLevel").Text).IsEqual("Lv 7");
        AssertThat(hud.GetNode<SiriusStatBar>("%HealthBar").Current).IsEqual(73);
        AssertThat(hud.GetNode<SiriusStatBar>("%ManaBar").Current).IsEqual(21);
        AssertThat(hud.GetNode<ProgressBar>("%ExperienceBar").Value).IsEqual(340);
        AssertThat(hud.GetNodeOrNull("%ExperienceLabel")).IsNull();
    }

    [TestCase]
    public async Task MissingPortraitCollapsesButIdentityRemainsReadable()
    {
        var hud = await InstantiateHud(new Vector2I(1280, 720));
        var portrait = hud.GetNode<TextureRect>("%Portrait");
        portrait.Texture = null;

        hud.ApplyPlayerState(new ExplorationHudPlayerState(
            "Aster", 7, 73, 120, 0, 0, 10, 100));

        AssertThat(portrait.Visible).IsFalse();
        AssertThat(hud.GetNode<Label>("%PlayerName").Visible).IsTrue();
        AssertThat(hud.GetNode<Label>("%PlayerLevel").Visible).IsTrue();
    }

    [TestCase]
    public async Task AreaTitlePrecedesQueuedSessionHintInOneRegion()
    {
        var hud = await InstantiateHud(new Vector2I(640, 360));

        hud.ShowSessionHint("Move with WASD or Arrow Keys");
        hud.ShowAreaTitle("Ground Floor");

        var plate = hud.GetNode<PanelContainer>("%TransientPlate");
        var label = hud.GetNode<Label>("%TransientLabel");
        var timer = hud.GetNode<Timer>("%TransientTimer");

        AssertThat(plate.Visible).IsTrue();
        AssertThat(label.Text).IsEqual("Ground Floor");
        AssertThat(timer.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

        timer.EmitSignal(Timer.SignalName.Timeout);
        AssertThat(plate.Visible).IsTrue();
        AssertThat(label.Text).IsEqual("Move with WASD or Arrow Keys");

        timer.EmitSignal(Timer.SignalName.Timeout);
        AssertThat(plate.Visible).IsFalse();
    }

    [TestCase]
    public async Task PromptUsesInteractActionAndHudRemainsPassive()
    {
        var hud = await InstantiateHud(new Vector2I(1280, 720));

        hud.ShowInteractionPrompt("Talk", UiIconId.Dialogue);

        var plate = hud.GetNode<PanelContainer>("%PromptPlate");
        var prompt = hud.GetNode<SiriusContextPrompt>("%ContextPrompt");
        AssertThat(plate.Visible).IsTrue();
        AssertThat(prompt.Prompt).IsEqual("Talk");
        AssertThat(prompt.ShowIcon).IsTrue();
        AssertThat(prompt.IconId).IsEqual(UiIconId.Dialogue);
        AssertThat(prompt.Actions.Length).IsEqual(1);
        AssertThat(prompt.Actions[0]).IsEqual(new StringName("interact"));

        AssertPassive(hud);
    }

    [TestCase]
    public async Task LayoutFitsApprovedViewportsAndKeepsCompactHeroSeparated()
    {
        foreach (var size in SiriusUiMetrics.VerificationViewports)
        {
            var hud = await InstantiateHud(size);
            hud.ApplyPlayerState(new ExplorationHudPlayerState(
                "Aster", 7, 73, 120, 21, 50, 340, 500));
            hud.ShowInteractionPrompt("Talk", UiIconId.Dialogue);
            hud.ShowAreaTitle("Ground Floor");
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

            var safeFrame = hud.GetNode<Control>("%SafeFrame");
            var safeRect = safeFrame.GetGlobalRect();
            var hero = hud.GetNode<PanelContainer>("%HeroPlate");
            var prompt = hud.GetNode<PanelContainer>("%PromptPlate");
            var transient = hud.GetNode<PanelContainer>("%TransientPlate");

            foreach (var surface in new Control[] { hero, prompt, transient })
            {
                var rect = surface.GetGlobalRect();
                AssertThat(rect.Size.X).IsGreater(0f);
                AssertThat(rect.Size.Y).IsGreater(0f);
                AssertThat(rect.Position.X).IsGreaterEqual(safeRect.Position.X - 0.5f);
                AssertThat(rect.Position.Y).IsGreaterEqual(safeRect.Position.Y - 0.5f);
                AssertThat(rect.End.X).IsLessEqual(safeRect.End.X + 0.5f);
                AssertThat(rect.End.Y).IsLessEqual(safeRect.End.Y + 0.5f);
            }

            var portrait = hud.GetNode<TextureRect>("%Portrait");
            var compact = SiriusUiMetrics.IsCompact(size);
            AssertThat(portrait.CustomMinimumSize)
                .IsEqual(compact ? new Vector2(40, 40) : new Vector2(56, 56));

            if (compact && size == new Vector2I(640, 360))
            {
                AssertThat(hero.GetGlobalRect().Intersects(prompt.GetGlobalRect())).IsFalse();
                AssertThat(hero.GetGlobalRect().Intersects(transient.GetGlobalRect())).IsFalse();
                AssertThat(prompt.GetGlobalRect().Intersects(transient.GetGlobalRect())).IsFalse();
            }
        }
    }

    private async Task<ExplorationHudController> InstantiateHud(Vector2I size)
    {
        var container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(container);
        _containers.Add(container);

        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        container.AddChild(viewport);

        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();
        if (packed is null)
            return null!;

        var hud = packed.Instantiate<ExplorationHudController>();
        viewport.AddChild(hud);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        return hud;
    }

    private static void AssertPassive(Node node)
    {
        if (node is Control control)
        {
            AssertThat(control.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(control.FocusMode).IsEqual(Control.FocusModeEnum.None);
        }

        foreach (var child in node.GetChildren())
            AssertPassive(child);
    }
}
