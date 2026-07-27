using GdUnit4;
using Godot;
using System;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class HealDialogTest : Node
{
    private HealDialog _dialog = null!;
    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new HealDialog();
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
    public void CanceledThenCloseRequested_EmitsHealCancelledOnce()
    {
        int cancelled = 0;
        _dialog.HealCancelled += () => cancelled++;
        _dialog.OpenHeal(NpcCatalog.GetById("village_healer")!, CreateInjuredPlayer());

        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(cancelled).IsEqual(1);
    }

    [TestCase]
    public void HealThenCancel_EmitsHealCompleteOnly()
    {
        int complete = 0;
        int cancelled = 0;
        _dialog.HealComplete += () => complete++;
        _dialog.HealCancelled += () => cancelled++;
        _dialog.OpenHeal(NpcCatalog.GetById("village_healer")!, CreateInjuredPlayer());

        FindButton("Heal").EmitSignal(Button.SignalName.Pressed);
        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

        AssertThat(complete).IsEqual(1);
        AssertThat(cancelled).IsEqual(0);
    }

    private static Character CreateInjuredPlayer()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.CurrentHealth = 50;
        player.Gold = 50;
        return player;
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
