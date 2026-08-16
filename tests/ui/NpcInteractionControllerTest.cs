using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class NpcInteractionControllerTest : Node
{
    private SceneTree _sceneTree = null!;
    private HostFixture _hostFixture = null!;
    private UIScreenHost _screenHost = null!;
    private Node _uiParent = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _hostFixture = await UIScreenHostTestSupport.CreateHost(this);
        _screenHost = _hostFixture.Host;

        _uiParent = new Node { Name = "LegacyNpcUiParent" };
        _sceneTree.Root.AddChild(_uiParent);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_uiParent != null && GodotObject.IsInstanceValid(_uiParent))
            _uiParent.QueueFree();

        await UIScreenHostTestSupport.DisposeFixture(_hostFixture);

        _uiParent = null!;
        _hostFixture = null!;
        _screenHost = null!;
        _sceneTree = null!;
    }

    [TestCase]
    public void Begin_HostsOneDialogueEntry()
    {
        var controller = CreateController("old_farmer");
        controller.Begin();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(1);
        AssertThat(HostedDialogueCount()).IsEqual(1);
    }

    [TestCase]
    public async Task DialogueCancel_ClosesHostedEntryAndCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        HostedDialogue().RequestCancel();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public async Task ShopOutcome_ClosesHostedDialogueBeforeNativeShopOpens()
    {
        int completed = 0;
        var controller = CreateController("village_shopkeeper");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(HostedDialogue(), "Browse your wares.").EmitSignal(Button.SignalName.Pressed);
        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);

        var shop = _uiParent.GetChildren().OfType<ShopDialog>().Single();
        shop.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public async Task HealOutcome_ClosesHostedDialogueBeforeNativeHealOpens()
    {
        int completed = 0;
        var controller = CreateController("village_healer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(HostedDialogue(), "Yes, heal me. (50 gold)").EmitSignal(Button.SignalName.Pressed);
        await AwaitTwoFrames();

        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);

        var heal = _uiParent.GetChildren().OfType<HealDialog>().Single();
        heal.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public async Task Finish_WhileDialogueActive_ClosesHostedEntryAndCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        controller.Finish();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    [TestCase]
    public void MissingTree_CreatesNoHostedEntryAndCompletesOnce()
    {
        var npc = new NpcData
        {
            NpcId = "missing_dialogue_test",
            DisplayName = "Missing Dialogue",
            NpcType = NpcType.Villager,
            DialogueTreeId = "missing_dialogue_tree",
            SpriteType = "villager"
        };
        int completed = 0;
        var controller = CreateController(npc);
        controller.InteractionComplete += () => completed++;
        controller.Begin();
        controller.Finish();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(0);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public void HostRejectsDialogue_CleansCandidateAndCompletesOnce()
    {
        var fixtureScreen = new Control();
        AssertThat(_screenHost.TryPresent(fixtureScreen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Dialogue,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Consume,
            NodeLifetime = UINodeLifetime.QueueFree
        }).Status).IsEqual(UIScreenOpenStatus.Opened);

        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        AssertThat(completed).IsEqual(1);
        AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
            .IsEqual(1); // only the pre-existing fixture entry
        AssertThat(HostedDialogueCount()).IsEqual(0);
    }

    private NpcInteractionController CreateController(string npcId)
    {
        var npc = NpcCatalog.GetById(npcId)
            ?? throw new InvalidOperationException($"NPC '{npcId}' not found.");
        return CreateController(npc);
    }

    private NpcInteractionController CreateController(NpcData npc)
    {
        return new NpcInteractionController(
            null!,
            _screenHost,
            _uiParent,
            npc,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>());
    }

    private DialogueScreenController HostedDialogue()
    {
        var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
        return modalLayer.GetChildren().OfType<DialogueScreenController>().Single();
    }

    private int HostedDialogueCount()
    {
        var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
        return modalLayer.GetChildren().OfType<DialogueScreenController>().Count();
    }

    private async Task AwaitTwoFrames()
    {
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static Button FindButton(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (Node child in node.GetChildren())
        {
            var found = FindButtonOrNull(child, text);
            if (found != null)
                return found;
        }

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
}
