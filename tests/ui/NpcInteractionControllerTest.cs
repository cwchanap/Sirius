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
    private Node _uiParent = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _uiParent = new Node();
        _sceneTree.Root.AddChild(_uiParent);
        await AwaitTwoFrames();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_uiParent != null && GodotObject.IsInstanceValid(_uiParent))
            _uiParent.QueueFree();

        await AwaitTwoFrames();
        _uiParent = null!;
        _sceneTree = null!;
    }

    [TestCase]
    public async Task DialogueCancel_CompletesOnceAndFreesDialogue()
    {
        int completed = 0;
        var controller = CreateController("old_farmer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        var dialogue = _uiParent.GetChildren().OfType<DialogueDialog>().Single();
        dialogue.EmitSignal(AcceptDialog.SignalName.Canceled);
        controller.Finish();
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().OfType<DialogueDialog>().Any()).IsFalse();
    }

    [TestCase]
    public async Task ShopOutcome_ReplacesDialogue_AndShopCancelCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("village_shopkeeper");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(_uiParent, "Browse your wares.").EmitSignal(Button.SignalName.Pressed);
        var shop = _uiParent.GetChildren().OfType<ShopDialog>().Single();
        shop.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public async Task HealOutcome_ReplacesDialogue_AndCancelCompletesOnce()
    {
        int completed = 0;
        var controller = CreateController("village_healer");
        controller.InteractionComplete += () => completed++;
        controller.Begin();

        FindButton(_uiParent, "Yes, heal me. (50 gold)").EmitSignal(Button.SignalName.Pressed);
        var heal = _uiParent.GetChildren().OfType<HealDialog>().Single();
        heal.EmitSignal(AcceptDialog.SignalName.Canceled);
        await AwaitTwoFrames();

        AssertThat(completed).IsEqual(1);
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
    }

    [TestCase]
    public void MissingDialogueTree_CompletesOnceAndCreatesNoDialog()
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
        AssertThat(_uiParent.GetChildren().Count).IsEqual(0);
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
            _uiParent,
            npc,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>());
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
