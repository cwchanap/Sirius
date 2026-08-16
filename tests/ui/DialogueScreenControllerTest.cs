using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class DialogueScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/DialogueScreen.tscn";

    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public void Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [TestCase]
    public async Task TryStartDialogue_BeforeReady_RendersAfterAttach()
    {
        var screen = CreateUnparentedCandidate();
        var npc = NpcCatalog.GetById("old_farmer")!;
        var tree = DialogueCatalog.GetById("villager_01")!;
        var player = TestHelpers.CreateTestCharacter();

        AssertThat(screen.TryStartDialogue(npc, tree, player, new HashSet<string>())).IsTrue();

        var fixture = Mount(screen, new Vector2I(1280, 720));
        try
        {
            await AwaitFrames(2);

            AssertThat(screen.GetNode<Label>("%SpeakerLabel").Text).IsEqual("Old Farmer");
            AssertThat(screen.GetNode<RichTextLabel>("%DialogueText").Text)
                .IsEqual(tree.Root!.Text);
            var choices = screen.GetNode<VBoxContainer>("%ChoicesContainer")
                .GetChildren()
                .OfType<Button>()
                .ToList();
            AssertThat(choices.Select(b => b.Text)).ContainsExactly(
                "I'm sorry to hear that.",
                "I'll be careful. Goodbye.");
            AssertThat(screen.InitialFocusTarget).IsEqual(choices[0]);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public void TryStartDialogue_MissingRootReturnsFalseWithoutTerminalSignal()
    {
        var screen = CreateUnparentedCandidate();
        try
        {
            int closed = 0;
            screen.DialogueClosed += () => closed++;

            var tree = new DialogueTree
            {
                TreeId = "test_missing_root",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["other"] = new DialogueNode
                    {
                        NodeId = "other",
                        SpeakerName = "X",
                        Text = "T"
                    }
                }
            };

            AssertThat(screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>())).IsFalse();
            AssertThat(closed).IsEqual(0);
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public void TryStartDialogue_SecondSuccessfulStartIsRejected()
    {
        var screen = CreateUnparentedCandidate();
        try
        {
            int closedCount = 0;
            screen.DialogueClosed += () => closedCount++;
            var npc = NpcCatalog.GetById("old_farmer")!;
            var tree = DialogueCatalog.GetById("villager_01")!;
            var player = TestHelpers.CreateTestCharacter();
            var flags = new HashSet<string>();

            AssertThat(screen.TryStartDialogue(npc, tree, player, flags)).IsTrue();
            screen.RequestCancel();
            AssertThat(screen.TryStartDialogue(npc, tree, player, flags)).IsFalse();
            screen.RequestCancel();
            AssertThat(closedCount).IsEqual(1);
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public void RequestCancelTwice_EmitsDialogueClosedOnce()
    {
        var screen = CreateUnparentedCandidate();
        try
        {
            int closed = 0;
            screen.DialogueClosed += () => closed++;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                DialogueCatalog.GetById("villager_01")!,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            screen.RequestCancel();
            screen.RequestCancel();

            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public async Task OutcomeThenCancel_EmitsOutcomeOnly()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            int outcomes = 0;
            int closed = 0;
            screen.DialogueOutcome += _ => outcomes++;
            screen.DialogueClosed += () => closed++;
            screen.TryStartDialogue(
                NpcCatalog.GetById("village_shopkeeper")!,
                DialogueCatalog.GetById("shopkeeper_greeting")!,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            FindButton(screen, "Browse your wares.").EmitSignal(Button.SignalName.Pressed);
            screen.RequestCancel();

            AssertThat(outcomes).IsEqual(1);
            AssertThat(closed).IsEqual(0);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task SecondQueuedTerminalChoice_GrantsOnlyFirstFlag()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            var flags = new HashSet<string>();
            var tree = new DialogueTree
            {
                TreeId = "test_two_terminal_choices",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["root"] = new DialogueNode
                    {
                        NodeId = "root",
                        SpeakerName = "Test",
                        Text = "Pick one.",
                        Choices = new[]
                        {
                            new DialogueChoice
                            {
                                Label = "First terminal.",
                                Outcome = DialogueOutcomeType.CloseAndReturn,
                                GrantFlag = "first_flag"
                            },
                            new DialogueChoice
                            {
                                Label = "Second terminal.",
                                Outcome = DialogueOutcomeType.CloseAndReturn,
                                GrantFlag = "second_flag"
                            }
                        }
                    }
                }
            };

            int outcomes = 0;
            screen.DialogueOutcome += _ => outcomes++;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                flags);

            FindButton(screen, "First terminal.").EmitSignal(Button.SignalName.Pressed);
            FindButton(screen, "Second terminal.").EmitSignal(Button.SignalName.Pressed);

            AssertThat(outcomes).IsEqual(1);
            AssertThat(flags.Contains("first_flag")).IsTrue();
            AssertThat(flags.Contains("second_flag")).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Scene_UsesSafeFrameModalShellAndContainsNoAcceptDialog()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            var safeFrame = screen.GetNode<Control>("%SafeFrame");
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");

            AssertThat(shell.GetParent()).IsEqual(safeFrame);
            AssertThat(shell.SizeClass).IsEqual(SiriusModalSizeClass.Full);
            AssertThat(ContainsAcceptDialog(screen)).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task ConditionalChoices_UsesEvaluateAndRendersOnlyMetConditions()
    {
        var npc = NpcCatalog.GetById("village_shopkeeper")!;
        var tree = DialogueCatalog.GetById("blacksmith_greeting")!;

        var lowFixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = lowFixture.Screen;
            var lowPlayer = TestHelpers.CreateTestCharacter();
            screen.TryStartDialogue(npc, tree, lowPlayer, new HashSet<string>());

            AssertThat(ActionLabels(screen)).ContainsExactly(
                "Show me what you've got.",
                "Nothing right now. Thanks.");
        }
        finally
        {
            await FreeAsync(lowFixture);
        }

        var highFixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = highFixture.Screen;
            var highPlayer = TestHelpers.CreateTestCharacter();
            highPlayer.Level = 3;
            screen.TryStartDialogue(npc, tree, highPlayer, new HashSet<string>());

            AssertThat(ActionLabels(screen)).ContainsExactly(
                "Show me what you've got.",
                "What's the strongest gear you make?",
                "Nothing right now. Thanks.");
        }
        finally
        {
            await FreeAsync(highFixture);
        }
    }

    [TestCase]
    public async Task NonterminalChoice_RemovesOldActionsBeforeRenderingNextNode()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            screen.TryStartDialogue(
                NpcCatalog.GetById("village_shopkeeper")!,
                DialogueCatalog.GetById("shopkeeper_greeting")!,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            FindButton(screen, "Any advice for a new adventurer?")
                .EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(1);

            AssertThat(FindButtonOrNull(screen, "Browse your wares.")).IsNull();
            AssertThat(FindButtonOrNull(screen, "Goodbye.")).IsNull();
            AssertThat(FindButtonOrNull(screen, "I'll keep that in mind. Browse wares."))
                .IsNotNull();
            AssertThat(FindButtonOrNull(screen, "Thanks. Goodbye.")).IsNotNull();
            AssertThat(screen.InitialFocusTarget).IsEqual(
                FindButton(screen, "I'll keep that in mind. Browse wares."));
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task Leaf_RendersSingleThemedFarewellAction()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            var tree = new DialogueTree
            {
                TreeId = "test_leaf",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["root"] = new DialogueNode
                    {
                        NodeId = "root",
                        SpeakerName = "Guide",
                        Text = "That is all."
                    }
                }
            };

            int closed = 0;
            screen.DialogueClosed += () => closed++;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            var actions = screen.GetNode<VBoxContainer>("%ChoicesContainer")
                .GetChildren()
                .OfType<Button>()
                .ToList();
            AssertThat(actions.Count).IsEqual(1);
            var farewell = FindButton(screen, "Farewell.");
            AssertThat(farewell.ThemeTypeVariation)
                .IsEqual(SiriusThemeTypes.SecondaryButton);

            farewell.EmitSignal(Button.SignalName.Pressed);
            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task GamepadAccept_OnFocusedChoiceAdvancesOnce()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                DialogueCatalog.GetById("villager_01")!,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());
            await AwaitFrames(2);

            var firstChoice = FindButton(screen, "I'm sorry to hear that.");
            AssertThat(firstChoice.HasFocus()).IsTrue();
            AssertThat(fixture.Viewport.GuiGetFocusOwner()).IsEqual(firstChoice);

            firstChoice.EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            AssertThat(FindButtonOrNull(screen, "I'm sorry to hear that.")).IsNull();
            AssertThat(FindButtonOrNull(screen, "I'll keep an eye out. Goodbye.")).IsNotNull();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task BrokenNextNode_ClosesOnce()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            var tree = new DialogueTree
            {
                TreeId = "test_broken_link",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["root"] = new DialogueNode
                    {
                        NodeId = "root",
                        SpeakerName = "Test",
                        Text = "T",
                        Choices = new[]
                        {
                            new DialogueChoice
                            {
                                Label = "Nowhere.",
                                NextNodeId = "missing"
                            }
                        }
                    }
                }
            };

            int closed = 0;
            screen.DialogueClosed += () => closed++;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            FindButton(screen, "Nowhere.").EmitSignal(Button.SignalName.Pressed);

            AssertThat(closed).IsEqual(1);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task SpeakerName_BlankHidesSpeakerLabel()
    {
        var fixture = await InstantiateDialogue(new Vector2I(1280, 720));
        try
        {
            var screen = fixture.Screen;
            var tree = new DialogueTree
            {
                TreeId = "test_blank_speaker",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["root"] = new DialogueNode
                    {
                        NodeId = "root",
                        SpeakerName = "",
                        Text = "T",
                        Choices = new[]
                        {
                            new DialogueChoice
                            {
                                Label = "Okay.",
                                NextNodeId = null,
                                Outcome = DialogueOutcomeType.CloseAndReturn
                            }
                        }
                    }
                }
            };

            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());

            AssertThat(screen.GetNode<Label>("%SpeakerLabel").Visible).IsFalse();
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase(1280, 720)]
    [TestCase(1920, 1080)]
    public async Task StandardDialogue_StaysWithinLowerBand(int width, int height)
    {
        var fixture = await InstantiateDialogue(new Vector2I(width, height));
        try
        {
            var screen = fixture.Screen;
            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                DialogueCatalog.GetById("villager_01")!,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());
            await AwaitFrames(2);

            var insets = SiriusUiMetrics.SafeFrameInsets(fixture.Viewport.Size);
            var safeHeight = fixture.Viewport.Size.Y - insets.Margin * 2f;
            var expectedBandHeight = safeHeight * 0.45f;
            var safeFrame = screen.GetNode<Control>("%SafeFrame");
            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
            var panel = shell.GetNode<PanelContainer>("%Panel");

            AssertThat(safeFrame.Size.X)
                .IsEqualApprox(fixture.Viewport.Size.X - insets.SideInset * 2f, 1f);
            AssertThat(safeFrame.Size.Y).IsEqualApprox(expectedBandHeight, 1f);
            AssertThat(panel.Size.X).IsEqualApprox(safeFrame.Size.X, 1f);
            AssertThat(panel.Size.Y).IsLessEqual(expectedBandHeight + 1f);
            AssertThat(panel.GetGlobalRect().Position.Y)
                .IsGreaterEqual(safeFrame.GetGlobalRect().Position.Y - 1f);
            AssertThat(panel.GetGlobalRect().End.Y)
                .IsLessEqual(safeFrame.GetGlobalRect().End.Y + 1f);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    [TestCase]
    public async Task CompactDialogue_FillsSafeHeightAndScrollsToFocusedChoice()
    {
        var fixture = await InstantiateDialogue(new Vector2I(640, 360));
        try
        {
            var screen = fixture.Screen;
            var tree = new DialogueTree
            {
                TreeId = "test_overflow",
                Nodes = new Dictionary<string, DialogueNode>
                {
                    ["root"] = new DialogueNode
                    {
                        NodeId = "root",
                        SpeakerName = "Overflowing Sage",
                        Text = string.Join("\n", Enumerable.Range(0, 5).Select(i =>
                            $"Paragraph {i}: a very long multi-line dialogue passage that keeps " +
                            $"talking about the dungeon, the creatures inside it, and the " +
                            $"supplies the adventurer should bring before descending.")),
                        Choices = Enumerable.Range(0, 10).Select(i => new DialogueChoice
                        {
                            Label = $"Choice {i}: a long wrapped option label that keeps going " +
                                    "and going so the action list grows tall enough to overflow",
                            NextNodeId = null,
                            Outcome = DialogueOutcomeType.CloseAndReturn
                        }).ToArray()
                    }
                }
            };

            screen.TryStartDialogue(
                NpcCatalog.GetById("old_farmer")!,
                tree,
                TestHelpers.CreateTestCharacter(),
                new HashSet<string>());
            await AwaitFrames(2);

            var insets = SiriusUiMetrics.SafeFrameInsets(fixture.Viewport.Size);
            var safeHeight = fixture.Viewport.Size.Y - insets.Margin * 2f;
            var safeFrame = screen.GetNode<Control>("%SafeFrame");
            AssertThat(insets.Compact).IsTrue();
            AssertThat(safeFrame.Size.Y).IsEqualApprox(safeHeight, 1f);

            var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
            var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
            var bar = bodyScroll.GetVScrollBar();
            AssertThat(bar.MaxValue).IsGreater(bar.Page);

            var lastChoice = screen.GetNode<VBoxContainer>("%ChoicesContainer")
                .GetChildren()
                .OfType<Button>()
                .Last();
            lastChoice.GrabFocus();
            await AwaitFrames(2);

            AssertThat(bodyScroll.ScrollVertical).IsGreater(0);
        }
        finally
        {
            await FreeAsync(fixture);
        }
    }

    private async Task<DialogueFixture> InstantiateDialogue(Vector2I size)
    {
        var screen = CreateUnparentedCandidate();
        var fixture = Mount(screen, size);
        await AwaitFrames(1);
        return fixture;
    }

    private static DialogueScreenController CreateUnparentedCandidate()
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return null!;

        return scene.Instantiate<DialogueScreenController>();
    }

    private DialogueFixture Mount(DialogueScreenController screen, Vector2I size)
    {
        var container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(container);

        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        container.AddChild(viewport);
        viewport.AddChild(screen);
        return new DialogueFixture(container, viewport, screen);
    }

    private async Task FreeAsync(DialogueFixture fixture)
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

    private static string[] ActionLabels(DialogueScreenController screen) =>
        screen.GetNode<VBoxContainer>("%ChoicesContainer")
            .GetChildren()
            .OfType<Button>()
            .Select(b => b.Text)
            .ToArray();

    private static Button FindButton(DialogueScreenController screen, string text)
    {
        var button = FindButtonOrNull(screen, text);
        if (button != null)
            return button;

        throw new InvalidOperationException($"Button '{text}' not found.");
    }

    private static Button? FindButtonOrNull(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (Node child in node.GetChildren())
        {
            var found = FindButtonOrNull(child, text);
            if (found != null)
                return found;
        }

        return null;
    }

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

    private sealed record DialogueFixture(
        SubViewportContainer Container,
        SubViewport Viewport,
        DialogueScreenController Screen);
}
