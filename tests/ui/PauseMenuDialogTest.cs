using GdUnit4;
using Godot;
using System.Linq;
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
    public void ResumeButton_Pressed_EmitsResumeRequested()
    {
        bool fired = false;
        _dialog.ResumeRequested += () => fired = true;
        FindButton("Resume").EmitSignal(Button.SignalName.Pressed);
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void SaveButton_Pressed_EmitsSaveRequested()
    {
        bool fired = false;
        _dialog.SaveRequested += () => fired = true;
        FindButton("Save Game").EmitSignal(Button.SignalName.Pressed);
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void LoadButton_Pressed_EmitsLoadRequested()
    {
        bool fired = false;
        _dialog.LoadRequested += () => fired = true;
        FindButton("Load Game").EmitSignal(Button.SignalName.Pressed);
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void SettingsButton_Pressed_EmitsSettingsRequested()
    {
        bool fired = false;
        _dialog.SettingsRequested += () => fired = true;
        FindButton("Settings").EmitSignal(Button.SignalName.Pressed);
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public void QuitToMenuButton_Pressed_EmitsQuitToMenuRequested()
    {
        bool fired = false;
        _dialog.QuitToMenuRequested += () => fired = true;
        FindButton("Quit to Main Menu").EmitSignal(Button.SignalName.Pressed);
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public async Task ResumeButton_Pressed_HidesDialog()
    {
        await OpenDialog();
        FindButton("Resume").EmitSignal(Button.SignalName.Pressed);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(_dialog.Visible).IsFalse();
    }

    [TestCase]
    public async Task SettingsButton_Pressed_DoesNotHideDialog()
    {
        await OpenDialog();
        FindButton("Settings").EmitSignal(Button.SignalName.Pressed);
        AssertThat(_dialog.Visible).IsTrue();
    }

    [TestCase]
    public async Task CloseRequestedSignal_HidesDialogAndEmitsResumeRequested()
    {
        bool fired = false;
        _dialog.ResumeRequested += () => fired = true;
        await OpenDialog();
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(fired).IsTrue();
    }

    [TestCase]
    public async Task CloseRequestedSignal_EmittedTwice_EmitsResumeRequestedOnlyOnce()
    {
        int count = 0;
        _dialog.ResumeRequested += () => count++;
        await OpenDialog();
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);
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

    /// <summary>
    /// Finds a Button child of the PauseMenuDialog by its display text.
    /// Buttons are created in _Ready() inside a VBoxContainer.
    /// </summary>
    private Button FindButton(string text)
    {
        foreach (var child in _dialog.GetChildren())
        {
            if (child is VBoxContainer vbox)
            {
                var btn = vbox.GetChildren().OfType<Button>().FirstOrDefault(b => b.Text == text);
                if (btn != null) return btn;
            }
        }
        throw new System.InvalidOperationException($"Button '{text}' not found on PauseMenuDialog.");
    }
}
