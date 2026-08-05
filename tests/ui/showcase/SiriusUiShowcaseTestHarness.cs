using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

internal static class SiriusUiShowcaseTestHarness
{
    public const string ScenePath = "res://scenes/ui/showcase/SiriusUiShowcase.tscn";

    public static async Task<SiriusUiShowcase> InstantiateAsync(SceneTree sceneTree)
    {
        var scene = ResourceLoader.Load<PackedScene>(ScenePath);
        AssertThat(scene).IsNotNull();
        if (scene is null)
            return null!;

        var showcase = scene.Instantiate<SiriusUiShowcase>();
        sceneTree.Root.AddChild(showcase);
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        return showcase;
    }

    public static async Task FreeAsync(SceneTree sceneTree, SiriusUiShowcase showcase)
    {
        if (GodotObject.IsInstanceValid(showcase))
            showcase.QueueFree();

        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
