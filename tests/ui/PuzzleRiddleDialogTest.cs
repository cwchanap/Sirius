using GdUnit4;
using Godot;
using System;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleRiddleDialogTest : Node
{
    private PuzzleRiddleDialog _dialog = null!;
    private SceneTree _sceneTree = null!;
    private PuzzleRiddleSpawn? _riddle;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new PuzzleRiddleDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();
        if (_riddle != null && GodotObject.IsInstanceValid(_riddle))
            _riddle.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void CanceledThenCloseRequested_EmitsPuzzleRiddleClosedOnce()
    {
        int closed = 0;
        _dialog.PuzzleRiddleClosed += () => closed++;
        var riddle = CreateRiddle();
        _dialog.OpenRiddle(riddle);

        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    public void ChoiceThenCancel_EmitsChoiceSelectedOnly()
    {
        int choices = 0;
        int closed = 0;
        _dialog.ChoiceSelected += choiceId =>
        {
            AssertThat(choiceId).IsEqual("a");
            choices++;
        };
        _dialog.PuzzleRiddleClosed += () => closed++;
        var riddle = CreateRiddle();
        _dialog.OpenRiddle(riddle);

        FindButton("Answer").EmitSignal(Button.SignalName.Pressed);
        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

        AssertThat(choices).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    private PuzzleRiddleSpawn CreateRiddle()
    {
        _riddle = new PuzzleRiddleSpawn
        {
            ChoiceIds = ["a"],
            ChoiceLabels = ["Answer"]
        };
        _sceneTree.Root.AddChild(_riddle);
        return _riddle;
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
