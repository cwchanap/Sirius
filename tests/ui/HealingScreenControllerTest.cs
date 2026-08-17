using GdUnit4;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class HealingScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/HealingScreen.tscn";

    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [BeforeTest]
    public void Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    // ---- Scene & configuration --------------------------------------------

    [TestCase]
    public void Scene_InstantiatesHealingScreenController()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();

        var screen = scene!.Instantiate<HealingScreenController>();
        try
        {
            AssertThat(screen).IsNotNull();
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task TryOpenHeal_BeforeReady_RendersAfterAttach()
    {
        var screen = CreateUnparentedScreen();
        var npc = Healer();
        var player = CreatePlayer(currentHealth: 40, gold: 60);

        AssertThat(screen.TryOpenHeal(npc, player)).IsTrue();

        var (container, viewport) = TestHelpers.MountInViewport(screen, new Vector2I(1280, 720));
        try
        {
            await AwaitFrames(2);

            AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
                .IsEqual(npc.DisplayName);
            AssertThat(screen.GetNode<Label>("%HealthLabel").Text)
                .IsEqual($"Current HP: 40/{player.GetEffectiveMaxHealth()}");
            AssertThat(screen.GetNode<Label>("%CostLabel").Text)
                .IsEqual("Restore all HP for 50 gold?");
            AssertThat(screen.GetNode<Label>("%GoldLabel").Text).IsEqual("Your Gold: 60");

            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsFalse();
            AssertThat(feedback.Text).IsEqual(string.Empty);

            var heal = screen.GetNode<Button>("%HealButton");
            AssertThat(heal.Disabled).IsFalse();
            AssertThat(screen.InitialFocusTarget).IsEqual(heal);
            AssertThat(viewport.GuiGetFocusOwner()).IsEqual(heal);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(container))
                container.QueueFree();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public void TryOpenHeal_SecondStartRejected_EvenAfterCancel()
    {
        var screen = CreateUnparentedScreen();
        try
        {
            var npc = Healer();
            var player = CreatePlayer(currentHealth: 40, gold: 60);
            int cancelled = 0;
            screen.HealCancelled += () => cancelled++;

            AssertThat(screen.TryOpenHeal(npc, player)).IsTrue();
            AssertThat(screen.TryOpenHeal(npc, player)).IsFalse();

            screen.RequestCancel();
            AssertThat(screen.TryOpenHeal(npc, player)).IsFalse();
            AssertThat(cancelled).IsEqual(1);
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task Scene_UsesSmallCentredModalShell_ActionsUnderHost_WithoutSafeFrameOrAcceptDialog()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 60);
        try
        {
            var screen = fixture.Screen;
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");

            // Centred small screen: no SafeFrame node, shell is a direct child
            // of the screen root, and both actions live in the shell's
            // ActionsHost (stable Prompt-style composition).
            AssertThat(screen.GetNodeOrNull("%SafeFrame")).IsNull();
            AssertThat(shell.GetParent()).IsEqual(screen);
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Small);
            AssertThat(screen.GetNode<Button>("%CancelButton").GetParent()).IsEqual(shell.ActionsHost);
            AssertThat(screen.GetNode<Button>("%HealButton").GetParent()).IsEqual(shell.ActionsHost);
            AssertThat(ContainsAcceptDialog(screen)).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Viewport geometry ----------------------------------------------------
    // Integration-level geometry only: SiriusModalShellTest owns the exhaustive
    // clamp math; these pin the authored screen's centring, size class, margin
    // and reachability outcomes at the representative verification viewports.

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    public async Task StandardHealing_IsCentredSmallWithinSafeMargins(int width, int height)
    {
        var fixture = await OpenMountedHealAsync(
            CreatePlayer(currentHealth: 40, gold: 60), new Vector2I(width, height));
        try
        {
            var shell = fixture.Screen.GetNode<SiriusModalShell>("%ModalShell");
            var panel = shell.GetNode<PanelContainer>("%Panel");

            AssertThat(shell.Compact).IsFalse();
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Small);

            var margin = SiriusUiMetrics.SafeMargin(false);
            var rect = panel.GetGlobalRect();

            // Centred on the viewport in both axes — a Dialogue-style bottom
            // band would pin the panel far below mid-screen.
            AssertThat(rect.GetCenter().X).IsEqualApprox(width / 2f, 1f);
            AssertThat(rect.GetCenter().Y).IsEqualApprox(height / 2f, 1f);

            AssertThat(rect.Position.X).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.End.X).IsLessEqual(width - margin + 0.5f);
            AssertThat(rect.End.Y).IsLessEqual(height - margin + 0.5f);

            foreach (var buttonName in new[] { "%HealButton", "%CancelButton" })
            {
                var buttonRect = fixture.Screen.GetNode<Button>(buttonName).GetGlobalRect();
                AssertThat(buttonRect.End.X).IsLessEqual(width - margin + 0.5f);
                AssertThat(buttonRect.End.Y).IsLessEqual(height - margin + 0.5f);
            }
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task CompactHealing_UsesSingleTwelvePxMargin_ActionsStayReachable()
    {
        var fixture = await OpenMountedHealAsync(
            CreatePlayer(currentHealth: 40, gold: 60), new Vector2I(640, 360));
        try
        {
            var shell = fixture.Screen.GetNode<SiriusModalShell>("%ModalShell");
            var panel = shell.GetNode<PanelContainer>("%Panel");

            AssertThat(shell.Compact).IsTrue();

            // The shell owns the compact margin; the screen must not re-apply
            // it. One 12 px inset per side → width is exactly viewport minus
            // 24, not 48.
            var margin = SiriusUiMetrics.SafeMargin(true);
            var rect = panel.GetGlobalRect();
            AssertThat(rect.Position.X).IsEqualApprox(margin, 1f);
            AssertThat(rect.End.X).IsEqualApprox(640f - margin, 1f);
            AssertThat(rect.Size.X).IsEqualApprox(640f - margin * 2f, 1f);

            AssertThat(rect.Position.Y).IsGreaterEqual(margin - 0.5f);
            AssertThat(rect.End.Y).IsLessEqual(360f - margin + 0.5f);

            // The short body never scrolls; both actions must remain fully
            // visible inside the viewport-safe band.
            foreach (var buttonName in new[] { "%HealButton", "%CancelButton" })
            {
                var buttonRect = fixture.Screen.GetNode<Button>(buttonName).GetGlobalRect();
                AssertThat(buttonRect.Position.Y).IsGreaterEqual(margin - 0.5f);
                AssertThat(buttonRect.End.Y).IsLessEqual(360f - margin + 0.5f);
            }

            // The whole body content stays visible above the actions (the
            // shell body is never squeezed behind the action row).
            var gold = fixture.Screen.GetNode<Label>("%GoldLabel");
            AssertThat(gold.GetGlobalRect().End.Y).IsLessEqual(
                fixture.Screen.GetNode<Button>("%HealButton").GetGlobalRect().Position.Y + 0.5f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Standing availability presentation --------------------------------

    [TestCase]
    public async Task FullHp_StandingFeedback_DisabledHeal_FocusesNoThanks()
    {
        // Rich but unharmed: full-HP takes precedence over affordability and
        // the standing message is the full-health one.
        var fixture = await OpenMountedHealAsync(currentHealth: 100, gold: 500);
        try
        {
            var screen = fixture.Screen;
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("You are already at full health.");

            var cancel = screen.GetNode<Button>("%CancelButton");
            AssertThat(screen.GetNode<Button>("%HealButton").Disabled).IsTrue();
            AssertThat(screen.InitialFocusTarget).IsEqual(cancel);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(cancel);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task InsufficientGold_StandingFeedback_DisabledHeal_FocusesNoThanks()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 49);
        try
        {
            var screen = fixture.Screen;
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Not enough gold!");

            var cancel = screen.GetNode<Button>("%CancelButton");
            AssertThat(screen.GetNode<Button>("%HealButton").Disabled).IsTrue();
            AssertThat(screen.InitialFocusTarget).IsEqual(cancel);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(cancel);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task StandingFeedback_NeverTimesOut_AndNoTimerExists()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 100, gold: 500);
        try
        {
            var feedback = fixture.Screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();

            // Past the Shop-style 2s transient window the standing label
            // must still be shown: Healing feedback is not timer-driven.
            await ToSignal(_sceneTree.CreateTimer(2.2), SceneTreeTimer.SignalName.Timeout);
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("You are already at full health.");

            bool hasTimerField = typeof(HealingScreenController)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                           BindingFlags.Instance | BindingFlags.Static)
                .Any(field => field.FieldType == typeof(SceneTreeTimer));
            AssertThat(hasTimerField).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Heal mutation & one-shot guards ------------------------------------

    [TestCase]
    public async Task Heal_Success_DeductsCost_RestoresEffectiveMax_EmitsCompleteOnce()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 60);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            int complete = 0;
            int cancelled = 0;
            screen.HealComplete += () => complete++;
            screen.HealCancelled += () => cancelled++;

            screen.GetNode<Button>("%HealButton").EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(10);
            AssertThat(player.CurrentHealth).IsEqual(player.GetEffectiveMaxHealth());
            AssertThat(complete).IsEqual(1);
            AssertThat(cancelled).IsEqual(0);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Heal_PressedTwice_SpendsAndEmitsOnce()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 60);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            int complete = 0;
            screen.HealComplete += () => complete++;

            var heal = screen.GetNode<Button>("%HealButton");
            heal.EmitSignal(Button.SignalName.Pressed);
            heal.EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.Gold).IsEqual(10);
            AssertThat(player.CurrentHealth).IsEqual(player.GetEffectiveMaxHealth());
            AssertThat(complete).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task HealThenCancel_EmitsNoCancellation()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 60);
        try
        {
            var screen = fixture.Screen;
            int complete = 0;
            int cancelled = 0;
            screen.HealComplete += () => complete++;
            screen.HealCancelled += () => cancelled++;

            screen.GetNode<Button>("%HealButton").EmitSignal(Button.SignalName.Pressed);
            screen.RequestCancel();

            AssertThat(complete).IsEqual(1);
            AssertThat(cancelled).IsEqual(0);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task RequestCancelTwice_EmitsCancelledOnce()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 60);
        try
        {
            int cancelled = 0;
            fixture.Screen.HealCancelled += () => cancelled++;

            fixture.Screen.RequestCancel();
            fixture.Screen.RequestCancel();

            AssertThat(cancelled).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ProgrammaticHeal_AtFullHp_CannotMutateHpOrGold()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 100, gold: 500);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            int complete = 0;
            screen.HealComplete += () => complete++;

            screen.GetNode<Button>("%HealButton").EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.CurrentHealth).IsEqual(100);
            AssertThat(player.Gold).IsEqual(500);
            AssertThat(complete).IsEqual(0);
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("You are already at full health.");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ProgrammaticHeal_InsufficientGold_CannotMutateHpOrGold()
    {
        var fixture = await OpenMountedHealAsync(currentHealth: 40, gold: 49);
        try
        {
            var screen = fixture.Screen;
            var player = fixture.Player;
            int complete = 0;
            screen.HealComplete += () => complete++;

            screen.GetNode<Button>("%HealButton").EmitSignal(Button.SignalName.Pressed);

            AssertThat(player.CurrentHealth).IsEqual(40);
            AssertThat(player.Gold).IsEqual(49);
            AssertThat(complete).IsEqual(0);
            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Not enough gold!");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Fixture helpers -------------------------------------------------------

    private sealed record HealFixture(
        SubViewportContainer Container,
        SubViewport Viewport,
        HealingScreenController Screen,
        Character Player);

    private static NpcData Healer() => NpcCatalog.GetById("village_healer")!;

    private static HealingScreenController CreateUnparentedScreen()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        return scene!.Instantiate<HealingScreenController>();
    }

    private async Task<HealFixture> OpenMountedHealAsync(int currentHealth, int gold)
        => await OpenMountedHealAsync(CreatePlayer(currentHealth, gold));

    private async Task<HealFixture> OpenMountedHealAsync(Character player)
        => await OpenMountedHealAsync(player, new Vector2I(1280, 720));

    private async Task<HealFixture> OpenMountedHealAsync(Character player, Vector2I viewportSize)
    {
        var screen = CreateUnparentedScreen();
        AssertThat(screen.TryOpenHeal(Healer(), player)).IsTrue();

        var (container, viewport) = TestHelpers.MountInViewport(screen, viewportSize);
        await AwaitFrames(1);
        return new HealFixture(container, viewport, screen, player);
    }

    private async Task FreeAsync(HealFixture fixture)
    {
        if (GodotObject.IsInstanceValid(fixture.Container))
            fixture.Container.QueueFree();
        await AwaitFrames(1);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static Character CreatePlayer(int currentHealth, int gold) => new Character
    {
        Name = "HealingScreenTester",
        Level = 1,
        MaxHealth = 100,
        CurrentHealth = currentHealth,
        Attack = 10,
        Defense = 5,
        Speed = 10,
        Gold = gold
    };

    private static bool ContainsAcceptDialog(Node node)
    {
        if (node is AcceptDialog)
            return true;

        foreach (Node child in node.GetChildren())
        {
            if (ContainsAcceptDialog(child))
                return true;
        }

        return false;
    }
}
