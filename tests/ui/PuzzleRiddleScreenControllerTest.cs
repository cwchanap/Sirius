using GdUnit4;
using Godot;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleRiddleScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/PuzzleRiddleScreen.tscn";

    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public void Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    // ---- Scene & configuration --------------------------------------------

    [TestCase]
    public void Scene_InstantiatesPuzzleRiddleScreenController()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();

        var screen = scene!.Instantiate<PuzzleRiddleScreenController>();
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
    public async Task Scene_MediumShellDirectChild_CancelInActionsHost_NoSafeFrameOrAcceptDialog()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            var screen = fixture.Screen;

            AssertThat(TestHelpers.ContainsAcceptDialog(screen)).IsFalse();
            AssertThat(screen.GetNodeOrNull("%SafeFrame")).IsNull();

            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
            AssertThat(shell.GetParent()).IsEqual(screen);
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Medium);
            AssertThat(screen.GetNode<Button>("%CancelButton").Visible).IsTrue();
            AssertThat(screen.GetNode<Button>("%CancelButton").Text).IsEqual("Cancel");
            AssertThat(screen.GetNode<SiriusInputHint>("%CancelHint").GetParent())
                .IsEqual(shell.ActionsHost);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task TryOpenRiddle_BeforeReady_RendersAfterMount()
    {
        var screen = CreateUnparentedScreen();
        var riddle = CreateRiddle();
        int choices = 0;
        screen.ChoiceSelected += _ => choices++;

        AssertThat(screen.TryOpenRiddle(riddle)).IsTrue();
        AssertThat(choices).IsEqual(0);

        var (container, viewport) = TestHelpers.MountInViewport(screen, new Vector2I(1280, 720));
        try
        {
            await AwaitFrames(2);

            AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
                .IsEqual(riddle.RiddleId);
            AssertThat(screen.GetNode<RichTextLabel>("%PromptLabel").Text)
                .IsEqual(riddle.PromptText);

            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsFalse();

            var answers = AnswerButtons(screen);
            AssertThat(answers.Count).IsEqual(2);
            AssertThat(answers[0].Text).IsEqual("An echo");
            AssertThat(answers[1].Text).IsEqual("The wind");

            // Dialogue-style treatment at a standard viewport: 44 px targets.
            AssertThat(answers[0].ThemeTypeVariation).IsEqual(SiriusThemeTypes.SecondaryButton);
            AssertThat(answers[0].CustomMinimumSize.Y)
                .IsEqual(SiriusUiMetrics.MinimumTarget(false).Y);

            AssertThat(screen.InitialFocusTarget).IsEqual(answers[0]);
            AssertThat(viewport.GuiGetFocusOwner()).IsEqual(answers[0]);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(container))
                container.QueueFree();
            riddle.Free();
            await AwaitFrames(1);
        }
    }

    [TestCase]
    public void TryOpenRiddle_SecondStartRejected_EvenAfterClose()
    {
        var screen = CreateUnparentedScreen();
        var riddle = CreateRiddle();
        var second = CreateRiddle();
        try
        {
            int closed = 0;
            screen.PuzzleRiddleClosed += () => closed++;

            AssertThat(screen.TryOpenRiddle(riddle)).IsTrue();
            AssertThat(screen.TryOpenRiddle(second)).IsFalse();

            screen.RequestCancel();
            AssertThat(screen.TryOpenRiddle(riddle)).IsFalse();
            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            second.Free();
            riddle.Free();
            screen.Free();
        }
    }

    [TestCase]
    public void TryOpenRiddle_BlankRiddleId_RendersSealTitle()
    {
        var screen = CreateUnparentedScreen();
        var riddle = CreateRiddle(riddleId: "  ");
        try
        {
            AssertThat(screen.TryOpenRiddle(riddle)).IsTrue();

            var (container, _) = TestHelpers.MountInViewport(screen, new Vector2I(1280, 720));
            try
            {
                AssertThat(screen.GetNode<SiriusModalShell>("%ModalShell").Title)
                    .IsEqual("Seal");
            }
            finally
            {
                if (GodotObject.IsInstanceValid(container))
                    container.QueueFree();
            }
        }
        finally
        {
            riddle.Free();
            screen.Free();
        }
    }

    [TestCase]
    public void TryOpenRiddle_NullOrZeroChoices_Rejected_WithoutConsumingOneShot()
    {
        var screen = CreateUnparentedScreen();
        var empty = CreateRiddle(choiceIds: [], choiceLabels: []);
        var valid = CreateRiddle();
        try
        {
            AssertThat(screen.TryOpenRiddle(null!)).IsFalse();
            AssertThat(screen.TryOpenRiddle(empty)).IsFalse();

            // Rejection does not consume the one-shot start: a valid retry
            // on the same screen instance still opens.
            AssertThat(screen.TryOpenRiddle(valid)).IsTrue();
        }
        finally
        {
            valid.Free();
            empty.Free();
            screen.Free();
        }
    }

    // ---- Resolving phase contract ------------------------------------------

    [TestCase]
    public async Task ChoiceThenCancel_WhileResolving_EmitsChoiceOnly()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            var screen = fixture.Screen;
            string? selected = null;
            int choices = 0;
            int closed = 0;
            screen.ChoiceSelected += id => { selected = id; choices++; };
            screen.PuzzleRiddleClosed += () => closed++;

            var first = AnswerButtons(screen)[0];
            first.EmitSignal(Button.SignalName.Pressed);
            // A repeated press and Cancel arrive synchronously inside
            // Resolving and must not emit anything further.
            first.EmitSignal(Button.SignalName.Pressed);
            screen.RequestCancel();

            AssertThat(choices).IsEqual(1);
            AssertThat(selected).IsEqual("echo");
            AssertThat(closed).IsEqual(0);
            AssertThat(first.Disabled).IsTrue();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Dormant rearm & terminal feedback ---------------------------------

    [TestCase]
    public async Task RequestCancel_WhileAwaitingChoice_EmitsClosedOnce()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            int closed = 0;
            fixture.Screen.PuzzleRiddleClosed += () => closed++;

            fixture.Screen.RequestCancel();
            fixture.Screen.RequestCancel();

            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task RearmWithFeedback_DormantResult_RearmsForOneNewAnswer()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            var screen = fixture.Screen;
            int choices = 0;
            int closed = 0;
            screen.ChoiceSelected += _ => choices++;
            screen.PuzzleRiddleClosed += () => closed++;

            var first = AnswerButtons(screen)[0];
            first.EmitSignal(Button.SignalName.Pressed);
            AssertThat(choices).IsEqual(1);

            screen.RearmWithFeedback("The seal does not respond yet.");

            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("The seal does not respond yet.");
            AssertThat(first.Disabled).IsFalse();
            AssertThat(first.Visible).IsTrue();
            AssertThat(screen.GetNode<Button>("%CancelButton").Text).IsEqual("Cancel");
            AssertThat(screen.InitialFocusTarget).IsEqual(first);

            // The rearmed phase accepts exactly one new answer.
            first.EmitSignal(Button.SignalName.Pressed);
            AssertThat(choices).IsEqual(2);
            AssertThat(closed).IsEqual(0);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ShowTerminalFeedback_WrongAnswer_RetiresChoices_CloseClosesOnce()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            var screen = fixture.Screen;
            int choices = 0;
            int closed = 0;
            screen.ChoiceSelected += _ => choices++;
            screen.PuzzleRiddleClosed += () => closed++;

            AnswerButtons(screen)[0].EmitSignal(Button.SignalName.Pressed);
            screen.ShowTerminalFeedback("Wrong! (-5 HP)", "Close");

            var feedback = screen.GetNode<Label>("%FeedbackLabel");
            AssertThat(feedback.Visible).IsTrue();
            AssertThat(feedback.Text).IsEqual("Wrong! (-5 HP)");

            var first = AnswerButtons(screen)[0];
            AssertThat(first.Visible).IsFalse();
            AssertThat(first.Disabled).IsTrue();

            var cancel = screen.GetNode<Button>("%CancelButton");
            AssertThat(cancel.Text).IsEqual("Close");
            AssertThat(screen.InitialFocusTarget).IsEqual(cancel);
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(cancel);

            // No answer emissions from Terminal; repeated close stays one-shot.
            first.EmitSignal(Button.SignalName.Pressed);
            cancel.EmitSignal(Button.SignalName.Pressed);
            screen.RequestCancel();
            AssertThat(choices).IsEqual(1);
            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ShowTerminalFeedback_Success_ContinueLabel_ClosesOnce()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            var screen = fixture.Screen;
            int closed = 0;
            screen.PuzzleRiddleClosed += () => closed++;

            screen.ShowTerminalFeedback("The seal dissolves!", "Continue");

            var cancel = screen.GetNode<Button>("%CancelButton");
            AssertThat(cancel.Text).IsEqual("Continue");
            AssertThat(screen.InitialFocusTarget).IsEqual(cancel);

            cancel.EmitSignal(Button.SignalName.Pressed);
            screen.RequestCancel();
            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ShowTerminalFeedback_BlankActionLabel_ReadsClose()
    {
        var fixture = await OpenMountedRiddleAsync();
        try
        {
            fixture.Screen.ShowTerminalFeedback("Solved.", "  ");
            AssertThat(fixture.Screen.GetNode<Button>("%CancelButton").Text)
                .IsEqual("Close");
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Compact target/text/scroll behavior -------------------------------

    [TestCase]
    public async Task CompactLongRiddle_CompactTargetsAndThemes_BodyScrollKeepsLastAnswerReachable()
    {
        var longPrompt = string.Join("\n", Enumerable.Range(0, 10).Select(i =>
            $"Paragraph {i}: a long wrapped riddle passage that keeps talking about " +
            "stones, levers, and seals so the body grows tall enough to overflow."));
        var riddle = CreateRiddle(
            "compact_seal",
            longPrompt,
            Enumerable.Range(0, 8).Select(i => $"choice_{i}").ToArray(),
            Enumerable.Range(0, 8)
                .Select(i => $"A rather long answer option {i} that wraps at compact width")
                .ToArray());

        var fixture = await OpenMountedRiddleAsync(new Vector2I(640, 360), riddle);
        try
        {
            var screen = fixture.Screen;
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");

            AssertThat(shell.Compact).IsTrue();
            AssertThat(screen.GetNode<RichTextLabel>("%PromptLabel").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.BodyCompact);
            AssertThat(screen.GetNode<Label>("%FeedbackLabel").ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.MetadataCompact);

            var answer = AnswerButtons(screen)[^1];
            AssertThat(answer.ThemeTypeVariation).IsEqual(SiriusThemeTypes.SecondaryButton);
            AssertThat(answer.CustomMinimumSize.Y)
                .IsEqual(SiriusUiMetrics.MinimumTarget(true).Y);

            var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
            AssertThat(bodyScroll.FollowFocus).IsTrue();

            // The shell body is the only scroll owner: focusing the final
            // answer scrolls it back into the visible body viewport.
            answer.GrabFocus();
            await AwaitFrames(3);

            AssertThat(bodyScroll.ScrollVertical).IsGreater(0);
            var scrollRect = bodyScroll.GetGlobalRect();
            var answerRect = answer.GetGlobalRect();
            var visibleTop = Mathf.Max(answerRect.Position.Y, scrollRect.Position.Y);
            var visibleBottom = Mathf.Min(answerRect.End.Y, scrollRect.End.Y);
            var visibleHeight = Mathf.Max(0f, visibleBottom - visibleTop);
            var expectedVisibleHeight = Mathf.Min(answerRect.Size.Y, scrollRect.Size.Y);
            AssertThat(visibleHeight).IsGreaterEqual(expectedVisibleHeight - 1f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    // ---- Fixture helpers -------------------------------------------------------

    private sealed record RiddleFixture(
        SubViewportContainer Container,
        SubViewport Viewport,
        PuzzleRiddleScreenController Screen,
        PuzzleRiddleSpawn Riddle);

    private static PuzzleRiddleScreenController CreateUnparentedScreen()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        return scene!.Instantiate<PuzzleRiddleScreenController>();
    }

    private static PuzzleRiddleSpawn CreateRiddle(
        string riddleId = "test_seal",
        string prompt = "I speak without a mouth and hear without ears. What am I?",
        string[]? choiceIds = null,
        string[]? choiceLabels = null)
    {
        var ids = choiceIds ?? ["echo", "wind"];
        var labels = choiceLabels ?? ["An echo", "The wind"];
        return new PuzzleRiddleSpawn
        {
            RiddleId = riddleId,
            PromptText = prompt,
            ChoiceIds = new Godot.Collections.Array<string>(ids),
            ChoiceLabels = new Godot.Collections.Array<string>(labels),
            CorrectChoiceId = "echo"
        };
    }

    private async Task<RiddleFixture> OpenMountedRiddleAsync(
        Vector2I viewportSize, PuzzleRiddleSpawn? riddle = null)
    {
        var spawn = riddle ?? CreateRiddle();
        var screen = CreateUnparentedScreen();
        AssertThat(screen.TryOpenRiddle(spawn)).IsTrue();

        var (container, viewport) = TestHelpers.MountInViewport(screen, viewportSize);
        await AwaitFrames(1);
        return new RiddleFixture(container, viewport, screen, spawn);
    }

    private Task<RiddleFixture> OpenMountedRiddleAsync(PuzzleRiddleSpawn? riddle = null)
        => OpenMountedRiddleAsync(new Vector2I(1280, 720), riddle);

    private async Task FreeAsync(RiddleFixture fixture)
    {
        if (GodotObject.IsInstanceValid(fixture.Container))
            fixture.Container.QueueFree();
        if (GodotObject.IsInstanceValid(fixture.Riddle))
            fixture.Riddle.Free();
        await AwaitFrames(1);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static System.Collections.Generic.List<Button> AnswerButtons(
        PuzzleRiddleScreenController screen) =>
        screen.GetNode<VBoxContainer>("%ChoicesContainer")
            .GetChildren()
            .OfType<Button>()
            .ToList();
}
