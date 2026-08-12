using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveOverwriteConfirmationControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/SaveOverwriteConfirmation.tscn";

    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private SubViewport? _viewport;
    private SaveOverwriteConfirmationController? _confirmation;

    [BeforeTest]
    public void SetUp() => _sceneTree = (SceneTree)Engine.GetMainLoop();

    [AfterTest]
    public async Task TearDown()
    {
        if (_confirmation != null && GodotObject.IsInstanceValid(_confirmation))
            _confirmation.QueueFree();
        if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
            _viewport.QueueFree();
        if (_container != null && GodotObject.IsInstanceValid(_container))
            _container.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        _confirmation = null;
        _viewport = null;
        _container = null;
    }

    [TestCase]
    public async Task InitialFocus_IsCancel()
    {
        await Instantiate(0);

        AssertThat(_confirmation!.InitialFocusTarget)
            .IsEqual(_confirmation.GetNode<Button>("%CancelButton"));
    }

    [TestCase]
    public async Task ConfiguredSlot_RendersSlotIdentity()
    {
        await Instantiate(2);

        AssertThat(_confirmation!.GetNode<Label>("%Message").Text)
            .Contains("Slot 3");
    }

    [TestCase]
    public async Task Cancel_EmitsOnce()
    {
        await Instantiate(1);

        var cancels = 0;
        _confirmation!.CancelRequested += () => cancels++;
        var cancel = _confirmation.GetNode<Button>("%CancelButton");

        cancel.EmitSignal(Button.SignalName.Pressed);
        cancel.EmitSignal(Button.SignalName.Pressed);

        AssertThat(cancels).IsEqual(1);
    }

    [TestCase]
    public async Task Overwrite_EmitsConfiguredSlotOnce()
    {
        await Instantiate(1);

        var confirmed = 0;
        _confirmation!.OverwriteConfirmed += slot =>
        {
            AssertThat(slot).IsEqual(1);
            confirmed++;
        };
        var overwrite = _confirmation.GetNode<Button>("%OverwriteButton");

        overwrite.EmitSignal(Button.SignalName.Pressed);
        overwrite.EmitSignal(Button.SignalName.Pressed);

        AssertThat(confirmed).IsEqual(1);
    }

    [TestCase]
    public async Task RepeatedTerminalPress_EmitsOnlyOnce()
    {
        await Instantiate(0);

        var confirmed = 0;
        var cancels = 0;
        _confirmation!.OverwriteConfirmed += _ => confirmed++;
        _confirmation.CancelRequested += () => cancels++;

        _confirmation.GetNode<Button>("%OverwriteButton")
            .EmitSignal(Button.SignalName.Pressed);
        _confirmation.GetNode<Button>("%CancelButton")
            .EmitSignal(Button.SignalName.Pressed);

        AssertThat(confirmed).IsEqual(1);
        AssertThat(cancels).IsEqual(0);
    }

    [TestCase]
    public void Scene_UsesSiriusModalShellAndNoAcceptDialog()
    {
        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        var confirmation = scene.Instantiate<SaveOverwriteConfirmationController>();
        try
        {
            AssertThat(confirmation.GetNodeOrNull<SiriusModalShell>("%ModalShell")).IsNotNull();
            AssertThat(confirmation.FindChild("AcceptDialog", true, false)).IsNull();
            AssertThat(ContainsAcceptDialog(confirmation)).IsFalse();
        }
        finally
        {
            confirmation.Free();
        }
    }

    private async Task Instantiate(int slot)
    {
        var size = new Vector2I(640, 360);
        _container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(_container);

        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Size = size
        };
        _container.AddChild(_viewport);

        var scene = GD.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return;

        _confirmation = scene.Instantiate<SaveOverwriteConfirmationController>();
        _confirmation.Slot = slot;
        _viewport.AddChild(_confirmation);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static bool ContainsAcceptDialog(Node node)
    {
        if (node is AcceptDialog)
            return true;

        foreach (var child in node.GetChildren())
        {
            if (ContainsAcceptDialog(child))
                return true;
        }

        return false;
    }
}
