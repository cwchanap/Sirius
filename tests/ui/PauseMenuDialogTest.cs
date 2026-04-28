using GdUnit4;
using Godot;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PauseMenuDialogTest : Node
{
    private PauseMenuDialog _dialog = null!;
    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new PauseMenuDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_dialog != null && GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _dialog = null!;
        _sceneTree = null!;
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void OnResumePressed_EmitsResumeRequested()
    {
        bool fired = false;
        _dialog.ResumeRequested += () => fired = true;
        InvokePrivate(_dialog, "OnResumePressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnSavePressed_EmitsSaveRequested()
    {
        bool fired = false;
        _dialog.SaveRequested += () => fired = true;
        InvokePrivate(_dialog, "OnSavePressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnLoadPressed_EmitsLoadRequested()
    {
        bool fired = false;
        _dialog.LoadRequested += () => fired = true;
        InvokePrivate(_dialog, "OnLoadPressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnSettingsPressed_EmitsSettingsRequested()
    {
        bool fired = false;
        _dialog.SettingsRequested += () => fired = true;
        InvokePrivate(_dialog, "OnSettingsPressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnQuitToMenuPressed_EmitsQuitToMenuRequested()
    {
        bool fired = false;
        _dialog.QuitToMenuRequested += () => fired = true;
        InvokePrivate(_dialog, "OnQuitToMenuPressed");
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnResumePressed_HidesDialog()
    {
        _dialog.Show();
        InvokePrivate(_dialog, "OnResumePressed");
        AssertThat(_dialog.Visible).IsFalse();
    }

    [TestCase]
    public void OnSettingsPressed_DoesNotHideDialog()
    {
        _dialog.Show();
        InvokePrivate(_dialog, "OnSettingsPressed");
        AssertThat(_dialog.Visible).IsTrue();
    }

    [TestCase]
    public void OnCloseRequested_HidesDialogAndEmitsResumeRequested()
    {
        bool fired = false;
        _dialog.ResumeRequested += () => fired = true;
        _dialog.Show();
        InvokePrivate(_dialog, "OnCloseRequested");
        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void OnCloseRequested_CalledTwice_EmitsResumeRequestedOnlyOnce()
    {
        int count = 0;
        _dialog.ResumeRequested += () => count++;
        _dialog.Show();
        InvokePrivate(_dialog, "OnCloseRequested");
        InvokePrivate(_dialog, "OnCloseRequested");
        AssertThat(count).IsEqual(1);
    }

    private static void InvokePrivate(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        m.Invoke(obj, args);
    }
}
