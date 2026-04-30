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

    [BeforeTest]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        await PurgePauseMenuDialogs(_sceneTree);
        _dialog = new PauseMenuDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (_dialog != null && GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        await PurgePauseMenuDialogs((SceneTree)Engine.GetMainLoop());
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
    public async Task OnResumePressed_HidesDialog()
    {
        await OpenDialog();
        InvokePrivate(_dialog, "OnResumePressed");
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(_dialog.Visible).IsFalse();
    }

    [TestCase]
    public async Task OnSettingsPressed_DoesNotHideDialog()
    {
        await OpenDialog();
        InvokePrivate(_dialog, "OnSettingsPressed");
        AssertThat(_dialog.Visible).IsTrue();
    }

    [TestCase]
    public async Task OnCloseRequested_HidesDialogAndEmitsResumeRequested()
    {
        bool fired = false;
        _dialog.ResumeRequested += () => fired = true;
        await OpenDialog();
        InvokePrivate(_dialog, "OnCloseRequested");
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public async Task OnCloseRequested_CalledTwice_EmitsResumeRequestedOnlyOnce()
    {
        int count = 0;
        _dialog.ResumeRequested += () => count++;
        await OpenDialog();
        InvokePrivate(_dialog, "OnCloseRequested");
        InvokePrivate(_dialog, "OnCloseRequested");
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(count).IsEqual(1);
    }

    private async Task OpenDialog()
    {
        _dialog.PopupCentered();
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private async Task PurgePauseMenuDialogs(SceneTree sceneTree)
    {
        foreach (var child in sceneTree.Root.GetChildren())
        {
            if (child is PauseMenuDialog dialog && GodotObject.IsInstanceValid(dialog))
            {
                dialog.QueueFree();
            }
        }

        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static void InvokePrivate(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new System.InvalidOperationException($"Method '{method}' not found.");
        m.Invoke(obj, args);
    }
}
