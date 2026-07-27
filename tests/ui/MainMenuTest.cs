using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class MainMenuTest : Node
{
    private MainMenu _menu = null!;
    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
        _menu = scene.Instantiate<MainMenu>();
        _sceneTree.Root.AddChild(_menu);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_menu))
            _menu.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void QuitButton_RequestsApplicationQuitOnce()
    {
        var menu = new TestableMainMenu();

        InvokePrivateAcrossHierarchy(menu, "_on_quit_button_pressed");

        AssertThat(menu.QuitRequests).IsEqual(1);
        menu.Free();
    }

    [TestCase]
    public void ShowMessage_CreatesOneVisibleAcceptDialogAndKeepsRootVisible()
    {
        InvokePrivateAcrossHierarchy(_menu, "ShowMessage", "Save system unavailable.");

        AssertThat(_menu.Visible).IsTrue();
        AssertThat(CountVisibleAcceptDialogs(_menu)).IsEqual(1);
    }

    [TestCase]
    public void OnLoadDialogClosed_QueuesChildAndClearsReference()
    {
        var loadDialog = new SaveLoadDialog();
        _menu.AddChild(loadDialog);
        SetPrivateField(_menu, "_loadDialog", loadDialog);

        InvokePrivateAcrossHierarchy(_menu, "OnLoadDialogClosed");

        AssertThat(loadDialog.IsQueuedForDeletion()).IsTrue();
        AssertThat(GetPrivateField<SaveLoadDialog?>(_menu, "_loadDialog")).IsNull();
        AssertThat(_menu.Visible).IsTrue();
    }

    [TestCase]
    public void SettingsPressed_DoesNotStackAndClosedCleansOnlySettingsChild()
    {
        InvokePrivateAcrossHierarchy(_menu, "_on_settings_button_pressed");
        var settings = GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu");

        AssertThat(settings).IsNotNull();
        AssertThat(settings!.Visible).IsTrue();
        AssertThat(CountSettingsChildren(_menu)).IsEqual(1);

        InvokePrivateAcrossHierarchy(_menu, "_on_settings_button_pressed");

        AssertThat(CountSettingsChildren(_menu)).IsEqual(1);

        settings.EmitSignal(SettingsMenuController.SignalName.Closed);

        AssertThat(settings.IsQueuedForDeletion()).IsTrue();
        AssertThat(GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu")).IsNull();
        AssertThat(_menu.Visible).IsTrue();
    }

    private partial class TestableMainMenu : MainMenu
    {
        public int QuitRequests { get; private set; }
        protected override void RequestApplicationQuit() => QuitRequests++;
    }

    private static void InvokePrivateAcrossHierarchy(object instance, string methodName, params object[] arguments)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method != null)
            {
                method.Invoke(instance, arguments);
                return;
            }
        }

        throw new MissingMethodException(instance.GetType().Name, methodName);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)field.GetValue(instance)!;
    }

    private static int CountVisibleAcceptDialogs(Node node)
    {
        int count = node is AcceptDialog dialog && dialog.Visible ? 1 : 0;
        foreach (var child in node.GetChildren())
            count += CountVisibleAcceptDialogs(child);
        return count;
    }

    private static int CountSettingsChildren(Node node)
    {
        int count = node is SettingsMenuController ? 1 : 0;
        foreach (var child in node.GetChildren())
            count += CountSettingsChildren(child);
        return count;
    }
}
