using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SiriusUiShowcaseStructureTest : Node
{
    private const string ScenePath = "res://scenes/ui/showcase/SiriusUiShowcase.tscn";
    private const string StressAction = "Bestätigungsaktion mit ausführlicher Beschreibung";
    private const string StressBody = "The observatory records every celestial route before committing the next action. This representative paragraph is intentionally long enough to wrap across multiple lines at the minimum supported viewport while preserving readable body text, fixed modal actions, and vertical scrolling.";
    private const string StressMetadata = "OBSERVATORY-CALIBRATION-IDENTIFIER-000000000000";

    private SceneTree _sceneTree = null!;
    private SiriusUiShowcase _showcase = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _showcase = scene.Instantiate<SiriusUiShowcase>();
        _sceneTree.Root.AddChild(_showcase);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_showcase))
            _showcase.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void AuthoredShowcase_ContainsEveryRequiredSectionAndPreviewRoot()
    {
        string[] requiredNames =
        {
            "PaletteSection", "TypographySection", "ButtonSection",
            "DarkSurfaceFixture", "LightSurfaceFixture",
            "IgnitionStandardFixture", "IgnitionCompactFixture",
            "SelectedFocusedFixture", "LoadingFixture", "TabsSection",
            "StatBarSection", "InputHintSection", "ContextPromptSection",
            "ToastSection", "ModalSection", "MotionSection",
            "MotionModalWrapper", "MotionToastWrapper"
        };

        AssertThat(_showcase.PreviewViewport).IsNotNull();
        AssertThat(_showcase.PreviewRoot).IsNotNull();
        foreach (var requiredName in requiredNames)
            AssertThat(_showcase.GetNodeOrNull<Control>($"%{requiredName}")).IsNotNull();
    }

    [TestCase]
    public void PaletteFixtures_UseFixedColorRectsApprovedPanelsAndNoScenicResources()
    {
        AssertThat(_showcase.GetNode<ColorRect>("%DarkSurfaceFixture")).IsNotNull();
        AssertThat(_showcase.GetNode<ColorRect>("%LightSurfaceFixture")).IsNotNull();

        AssertPanelVariation("DarkContentPanel", SiriusThemeTypes.ContentPanel);
        AssertPanelVariation("DarkFeaturePanel", SiriusThemeTypes.FeaturePanel);
        AssertPanelVariation("DarkHudPlate", SiriusThemeTypes.HudPlate);
        AssertPanelVariation("LightContentPanel", SiriusThemeTypes.ContentPanel);
        AssertPanelVariation("LightFeaturePanel", SiriusThemeTypes.FeaturePanel);
        AssertPanelVariation("LightHudPlate", SiriusThemeTypes.HudPlate);

        AssertThat(_showcase.GetNodeOrNull<Control>("%BackgroundSelector")).IsNull();
        AssertThat(_showcase.FindChild("ScenicBackground", true, false)).IsNull();

        string[] allowedExternalResourcePaths =
        {
            "res://scripts/ui/showcase/SiriusUiShowcase.cs",
            "res://resources/ui/theme/SiriusTheme.tres",
            "res://scenes/ui/components/SiriusStatBar.tscn",
            "res://scenes/ui/components/SiriusInputHint.tscn",
            "res://scenes/ui/components/SiriusContextPrompt.tscn",
            "res://scenes/ui/components/SiriusToastShell.tscn",
            "res://scenes/ui/components/SiriusModalShell.tscn"
        };
        var sceneSource = FileAccess.GetFileAsString(ScenePath);
        var hasScenicResourcePath = false;
        var externalResourceCount = 0;
        foreach (var line in sceneSource.Split('\n'))
        {
            if (!line.StartsWith("[ext_resource", StringComparison.Ordinal))
                continue;

            externalResourceCount++;
            AssertThat(Array.Exists(
                    allowedExternalResourcePaths,
                    path => line.Contains($"path=\"{path}\"", StringComparison.Ordinal)))
                .IsTrue();
            hasScenicResourcePath |=
                line.Contains("background", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("scenic", StringComparison.OrdinalIgnoreCase);
        }

        AssertThat(externalResourceCount).IsEqual(allowedExternalResourcePaths.Length);
        AssertThat(hasScenicResourcePath).IsFalse();
    }

    [TestCase]
    public void StockControls_UseThemeVariationsAndKeepLoadingStatic()
    {
        AssertButtonVariation("PrimaryButtonFixture", SiriusThemeTypes.PrimaryButton);
        AssertButtonVariation("SecondaryButtonFixture", SiriusThemeTypes.SecondaryButton);
        AssertButtonVariation("TertiaryButtonFixture", SiriusThemeTypes.TertiaryButton);
        AssertButtonVariation("WarningButtonFixture", SiriusThemeTypes.WarningButton);
        AssertButtonVariation("DestructiveButtonFixture", SiriusThemeTypes.DestructiveButton);
        AssertButtonVariation("IgnitionStandardFixture", SiriusThemeTypes.IgnitionButton);
        AssertButtonVariation("IgnitionCompactFixture", SiriusThemeTypes.IgnitionButton);

        var selected = _showcase.GetNode<Button>("%SelectedFocusedFixture");
        AssertThat(selected.ToggleMode).IsTrue();
        AssertThat(selected.ButtonPressed).IsTrue();

        var loading = _showcase.GetNode<Button>("%LoadingFixture");
        AssertThat(loading.ThemeTypeVariation).IsEqual(SiriusThemeTypes.PrimaryButton);
        AssertThat(loading.Text).IsEqual("Loading…");
        AssertThat(loading.Disabled).IsTrue();
    }

    [TestCase]
    public void Showcase_ComposesTheFiveSharedComponentsAndExactStressFixtures()
    {
        AssertThat(_showcase.GetNode<SiriusStatBar>("%HealthStat")).IsNotNull();
        AssertThat(_showcase.GetNode<SiriusInputHint>("%KeyboardHint")).IsNotNull();
        AssertThat(_showcase.GetNode<SiriusContextPrompt>("%TalkPrompt")).IsNotNull();
        AssertThat(_showcase.GetNode<SiriusToastShell>("%InfoToast")).IsNotNull();
        AssertThat(_showcase.GetNode<SiriusModalShell>("%MediumModalFixture")).IsNotNull();

        var action = _showcase.GetNode<Button>("%StressAction");
        var body = _showcase.GetNode<Label>("%StressBody");
        var metadata = _showcase.GetNode<Label>("%StressMetadata");
        AssertThat(action.Text).IsEqual(StressAction);
        AssertThat(body.Text).IsEqual(StressBody);
        AssertThat(body.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
        AssertThat(metadata.Text).IsEqual(StressMetadata);
        AssertThat(metadata.ClipText).IsTrue();
        AssertThat(metadata.TooltipText).IsEqual(StressMetadata);
    }

    [TestCase]
    public void LocalMotionDemo_ReducedMotionStartsAtTheShowcaseWrapperBases()
    {
        var modalWrapper = _showcase.GetNode<Control>("%MotionModalWrapper");
        var toastWrapper = _showcase.GetNode<Control>("%MotionToastWrapper");
        var modalBasePosition = modalWrapper.Position;
        var toastBasePosition = toastWrapper.Position;
        _showcase.SetReducedMotion(true);
        _showcase.PlayMotionDemo();

        AssertThat(modalWrapper.Position).IsEqual(modalBasePosition);
        AssertThat(toastWrapper.Position).IsEqual(toastBasePosition);
        AssertThat(modalWrapper.Modulate.A).IsEqualApprox(0f, 0.001f);
        AssertThat(toastWrapper.Modulate.A).IsEqualApprox(0f, 0.001f);
    }

    [TestCase]
    public void LocalMotionDemo_NormalModeStartsFromTheApprovedEntryTranslation()
    {
        var modalWrapper = _showcase.GetNode<Control>("%MotionModalWrapper");
        var toastWrapper = _showcase.GetNode<Control>("%MotionToastWrapper");
        var modalBasePosition = modalWrapper.Position;
        var toastBasePosition = toastWrapper.Position;
        _showcase.SetReducedMotion(false);
        _showcase.PlayMotionDemo();

        AssertThat(modalWrapper.Position).IsEqual(modalBasePosition + new Vector2(0, 12));
        AssertThat(toastWrapper.Position).IsEqual(toastBasePosition + new Vector2(0, 12));
        AssertThat(modalWrapper.Modulate.A).IsEqualApprox(0f, 0.001f);
        AssertThat(toastWrapper.Modulate.A).IsEqualApprox(0f, 0.001f);
    }

    [TestCase]
    public void LocalMotionDemo_CompletesEntryBeforeStartingTheNormalExit()
    {
        var modalWrapper = _showcase.GetNode<Control>("%MotionModalWrapper");
        var toastWrapper = _showcase.GetNode<Control>("%MotionToastWrapper");
        var modalBasePosition = modalWrapper.Position;
        var toastBasePosition = toastWrapper.Position;
        _showcase.SetReducedMotion(false);
        _showcase.PlayMotionDemo();

        var motionTween = GetMotionTween();
        motionTween.Pause();
        motionTween.CustomStep(SiriusMotion.EntrySeconds);

        AssertWrapperIsVisibleAtBase(modalWrapper, modalBasePosition);
        AssertWrapperIsVisibleAtBase(toastWrapper, toastBasePosition);

        motionTween.CustomStep(SiriusMotion.ExitSeconds / 2d);

        AssertWrapperIsExiting(modalWrapper, modalBasePosition);
        AssertWrapperIsExiting(toastWrapper, toastBasePosition);

        motionTween.CustomStep(SiriusMotion.ExitSeconds / 2d);

        AssertWrapperHasExited(modalWrapper, modalBasePosition);
        AssertWrapperHasExited(toastWrapper, toastBasePosition);
    }

    [TestCase]
    public void LocalMotionDemo_ReducedMotionUsesLinearHundredMillisecondOpacity()
    {
        var modalWrapper = _showcase.GetNode<Control>("%MotionModalWrapper");
        var toastWrapper = _showcase.GetNode<Control>("%MotionToastWrapper");
        var modalBasePosition = modalWrapper.Position;
        var toastBasePosition = toastWrapper.Position;
        _showcase.SetReducedMotion(true);
        _showcase.PlayMotionDemo();

        var motionTween = GetMotionTween();
        motionTween.Pause();
        motionTween.CustomStep(SiriusMotion.ReducedOpacitySeconds / 2d);

        AssertThat(modalWrapper.Modulate.A).IsEqualApprox(0.5f, 0.001f);
        AssertThat(toastWrapper.Modulate.A).IsEqualApprox(0.5f, 0.001f);
        AssertThat(modalWrapper.Position).IsEqual(modalBasePosition);
        AssertThat(toastWrapper.Position).IsEqual(toastBasePosition);

        motionTween.CustomStep(SiriusMotion.ReducedOpacitySeconds / 2d);
        AssertWrapperIsVisibleAtBase(modalWrapper, modalBasePosition);
        AssertWrapperIsVisibleAtBase(toastWrapper, toastBasePosition);

        motionTween.CustomStep(SiriusMotion.ReducedOpacitySeconds / 2d);
        AssertThat(modalWrapper.Modulate.A).IsEqualApprox(0.5f, 0.001f);
        AssertThat(toastWrapper.Modulate.A).IsEqualApprox(0.5f, 0.001f);
        AssertThat(modalWrapper.Position).IsEqual(modalBasePosition);
        AssertThat(toastWrapper.Position).IsEqual(toastBasePosition);
    }

    private void AssertButtonVariation(string uniqueName, StringName variation)
    {
        var button = _showcase.GetNode<Button>($"%{uniqueName}");
        AssertThat(button.ThemeTypeVariation).IsEqual(variation);
    }

    private void AssertPanelVariation(string uniqueName, StringName variation)
    {
        var panel = _showcase.GetNode<PanelContainer>($"%{uniqueName}");
        AssertThat(panel.ThemeTypeVariation).IsEqual(variation);
    }

    private Tween GetMotionTween()
    {
        var tweenField = typeof(SiriusUiShowcase).GetField(
            "_motionTween",
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertThat(tweenField).IsNotNull();
        return (Tween)tweenField!.GetValue(_showcase)!;
    }

    private static void AssertWrapperIsVisibleAtBase(Control wrapper, Vector2 basePosition)
    {
        AssertThat(wrapper.Modulate.A).IsEqualApprox(1f, 0.001f);
        AssertThat(wrapper.Position).IsEqual(basePosition);
    }

    private static void AssertWrapperIsExiting(Control wrapper, Vector2 basePosition)
    {
        AssertThat(wrapper.Modulate.A).IsGreater(0f);
        AssertThat(wrapper.Modulate.A).IsLess(1f);
        AssertThat(wrapper.Position.Y).IsLess(basePosition.Y);
        AssertThat(wrapper.Position.Y).IsGreater(basePosition.Y - 8f);
    }

    private static void AssertWrapperHasExited(Control wrapper, Vector2 basePosition)
    {
        AssertThat(wrapper.Modulate.A).IsEqualApprox(0f, 0.001f);
        AssertThat(wrapper.Position).IsEqual(basePosition + new Vector2(0, -8));
    }
}
