using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class DialogueDialogTest : Node
{
    private DialogueDialog _dialog = null!;
    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new DialogueDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void CanceledThenCloseRequested_EmitsDialogueClosedOnce()
    {
        int closed = 0;
        _dialog.DialogueClosed += () => closed++;
        _dialog.StartDialogue(
            NpcCatalog.GetById("old_farmer")!,
            DialogueCatalog.GetById("villager_01")!,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>());

        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    public void OutcomeThenClose_EmitsOutcomeOnly()
    {
        int outcomes = 0;
        int closed = 0;
        _dialog.DialogueOutcome += _ => outcomes++;
        _dialog.DialogueClosed += () => closed++;
        _dialog.StartDialogue(
            NpcCatalog.GetById("village_shopkeeper")!,
            DialogueCatalog.GetById("shopkeeper_greeting")!,
            TestHelpers.CreateTestCharacter(),
            new HashSet<string>());

        FindButton("Browse your wares.").EmitSignal(Button.SignalName.Pressed);
        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

        AssertThat(outcomes).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    private Button FindButton(string text)
    {
        var button = FindButton(_dialog, text);
        if (button != null)
            return button;

        throw new InvalidOperationException($"Button '{text}' not found.");
    }

    private static Button? FindButton(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (Node child in node.GetChildren())
        {
            var found = FindButton(child, text);
            if (found != null)
                return found;
        }

        return null;
    }
}
