using GdUnit4;
using Godot;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuControllerTest : Node
{
    private SettingsMenuController _ctrl = null!;
    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();

        var scene = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        _ctrl = scene.Instantiate<SettingsMenuController>();
        _sceneTree.Root.AddChild(_ctrl);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_ctrl != null && GodotObject.IsInstanceValid(_ctrl))
            _ctrl.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _ctrl = null!;
        _sceneTree = null!;
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void SceneLoads_ControllerIsNotNull()
    {
        AssertThat(_ctrl).IsNotNull();
    }

    [TestCase]
    public void OpenSettings_ShowsController()
    {
        _ctrl.Hide();
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        AssertThat(_ctrl.Visible).IsTrue();
    }

    [TestCase]
    public void OnCancelPressed_EmitsClosed()
    {
        bool fired = false;
        _ctrl.Closed += () => fired = true;
        _ctrl.OpenSettings(SettingsData.CreateDefaults());
        InvokePrivate(_ctrl, "OnCancelPressed");
        AssertThat(fired).IsTrue();
    }

    protected static void InvokePrivate(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        m.Invoke(obj, args);
    }

    protected static T GetField<T>(object obj, string field) where T : class
    {
        var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Field '{field}' not found.");
        return (T)f.GetValue(obj)!;
    }
}
