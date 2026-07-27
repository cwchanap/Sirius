using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveLoadDialogTest : Node
{
    private SaveLoadDialog _dialog = null!;
    private SceneTree _sceneTree = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new SaveLoadDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();

        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void Canceled_HidesAndEmitsDialogClosed()
    {
        int closed = 0;
        _dialog.DialogClosed += () => closed++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);

        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);

        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    public void CanceledThenCloseRequested_EmitsOneTerminalSignal()
    {
        int closed = 0;
        _dialog.DialogClosed += () => closed++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);

        _dialog.EmitSignal(AcceptDialog.SignalName.Canceled);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    public void MainMenuThenClose_EmitsMainMenuOnlyOnce()
    {
        int menu = 0;
        int closed = 0;
        _dialog.MainMenuRequested += () => menu++;
        _dialog.DialogClosed += () => closed++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);

        FindButton("Main Menu").EmitSignal(Button.SignalName.Pressed);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(menu).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    [TestCase]
    public void DismissOverwriteChild_LeavesParentVisibleAndEmitsNothing()
    {
        int terminalCount = 0;
        _dialog.DialogClosed += () => terminalCount++;
        _dialog.SaveSlotSelected += _ => terminalCount++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
        SetSlotInfo(0, new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 });

        InvokePrivate(_dialog, "OnSlotPressed", 0);
        AssertThat(_dialog.HasActiveChildDialog).IsTrue();

        _dialog.DismissActiveChildDialog();

        AssertThat(_dialog.HasActiveChildDialog).IsFalse();
        AssertThat(_dialog.Visible).IsTrue();
        AssertThat(terminalCount).IsEqual(0);
    }

    [TestCase]
    public void ShowDialog_Load_HidesMainMenuButton()
    {
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);

        AssertThat(FindButton("Main Menu").Visible).IsFalse();
    }

    [TestCase]
    public void ShowDialog_SecondOpen_ResetsTerminalGuard()
    {
        int closed = 0;
        _dialog.DialogClosed += () => closed++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(closed).IsEqual(2);
    }

    [TestCase]
    public void EmptySaveSlotPressed_HidesAndEmitsSaveSlotOnce()
    {
        int saves = 0;
        _dialog.SaveSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(0);
            saves++;
        };
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
        SetSlotInfo(0, new SaveSlotInfo { Exists = false, SlotIndex = 0 });

        InvokePrivate(_dialog, "OnSlotPressed", 0);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(saves).IsEqual(1);
    }

    [TestCase]
    public void LoadSlotPressed_HidesAndEmitsLoadSlotOnce()
    {
        int loads = 0;
        _dialog.LoadSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(1);
            loads++;
        };
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Load);
        SetSlotInfo(1, new SaveSlotInfo { Exists = true, SlotIndex = 1, PlayerLevel = 3 });

        InvokePrivate(_dialog, "OnSlotPressed", 1);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(loads).IsEqual(1);
    }

    [TestCase]
    public void OverwriteConfirmed_HidesAndEmitsPendingSaveSlotOnce()
    {
        int saves = 0;
        _dialog.SaveSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(0);
            saves++;
        };
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
        SetSlotInfo(0, new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 });

        InvokePrivate(_dialog, "OnSlotPressed", 0);
        GetActiveConfirmDialog().EmitSignal(AcceptDialog.SignalName.Confirmed);

        AssertThat(_dialog.Visible).IsFalse();
        AssertThat(saves).IsEqual(1);
    }

    [TestCase]
    public void OverwriteConfirmedThenClose_EmitsOnlySaveSlot()
    {
        int saves = 0;
        int closed = 0;
        _dialog.SaveSlotSelected += _ => saves++;
        _dialog.DialogClosed += () => closed++;
        _dialog.ShowDialog(SaveLoadDialog.DialogMode.Save);
        SetSlotInfo(0, new SaveSlotInfo { Exists = true, SlotIndex = 0, PlayerLevel = 2 });

        InvokePrivate(_dialog, "OnSlotPressed", 0);
        GetActiveConfirmDialog().EmitSignal(AcceptDialog.SignalName.Confirmed);
        _dialog.EmitSignal(AcceptDialog.SignalName.CloseRequested);

        AssertThat(saves).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    private Button FindButton(string text)
    {
        foreach (var child in _dialog.GetChildren())
        {
            var found = FindButton(child, text);
            if (found != null)
                return found;
        }

        throw new InvalidOperationException($"Button '{text}' not found.");
    }

    private static Button? FindButton(Node node, string text)
    {
        if (node is Button button && button.Text == text)
            return button;

        foreach (var child in node.GetChildren())
        {
            var found = FindButton(child, text);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void InvokePrivate(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new MissingMethodException(instance.GetType().Name, methodName);

        method.Invoke(instance, arguments);
    }

    private void SetSlotInfo(int slot, SaveSlotInfo info)
    {
        var field = typeof(SaveLoadDialog).GetField("_slotInfos",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var slotInfos = (SaveSlotInfo[])field.GetValue(_dialog)!;
        slotInfos[slot] = info;
    }

    private AcceptDialog GetActiveConfirmDialog()
    {
        var field = typeof(SaveLoadDialog).GetField("_activeConfirmDialog",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (AcceptDialog)field.GetValue(_dialog)!;
    }
}
