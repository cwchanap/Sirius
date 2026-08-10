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
    public void SelectContinueSave_NoEligibleSlot_ReturnsNull()
    {
        var slots = new[]
        {
            new SaveSlotInfo { SlotIndex = 0, Exists = false },
            new SaveSlotInfo { SlotIndex = 1, Exists = true, IsCorrupted = true },
            new SaveSlotInfo { SlotIndex = 2, Exists = false },
            new SaveSlotInfo { SlotIndex = 3, Exists = true, IsCorrupted = true }
        };

        AssertThat(MainMenu.SelectContinueSave(slots)).IsNull();
    }

    [TestCase]
    public void SelectContinueSave_NewestStoredTimestampWins()
    {
        var older = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);

        var slots = new[]
        {
            EligibleSlot(0, older),
            EligibleSlot(1, newer),
            EligibleSlot(2, DateTime.MinValue),
            new SaveSlotInfo
            {
                SlotIndex = 3,
                Exists = true,
                IsCorrupted = true,
                Timestamp = newer.AddDays(1)
            }
        };

        AssertThat(MainMenu.SelectContinueSave(slots)!.SlotIndex).IsEqual(1);
    }

    [TestCase]
    public void SelectContinueSave_UsableTimestampBeatsMinValue()
    {
        var slots = new[]
        {
            EligibleSlot(0, DateTime.MinValue),
            EligibleSlot(1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            EligibleSlot(2, DateTime.MinValue),
            EligibleSlot(3, DateTime.MinValue)
        };

        AssertThat(MainMenu.SelectContinueSave(slots)!.SlotIndex).IsEqual(1);
    }

    [TestCase]
    public void SelectContinueSave_EqualTimestampsPreferAutosaveThenManualOrder()
    {
        var timestamp = new DateTime(2026, 8, 9, 20, 0, 0, DateTimeKind.Utc);

        AssertThat(MainMenu.SelectContinueSave(new[]
        {
            EligibleSlot(2, timestamp),
            EligibleSlot(1, timestamp),
            EligibleSlot(0, timestamp),
            EligibleSlot(3, timestamp)
        })!.SlotIndex).IsEqual(3);

        AssertThat(MainMenu.SelectContinueSave(new[]
        {
            EligibleSlot(2, timestamp),
            EligibleSlot(1, timestamp),
            EligibleSlot(0, timestamp)
        })!.SlotIndex).IsEqual(0);
    }

    [TestCase]
    public void SelectContinueSave_AllMinValueUsesSameTieOrder()
    {
        AssertThat(MainMenu.SelectContinueSave(new[]
        {
            EligibleSlot(2, DateTime.MinValue),
            EligibleSlot(0, DateTime.MinValue),
            EligibleSlot(1, DateTime.MinValue)
        })!.SlotIndex).IsEqual(0);
    }

    [TestCase]
    public async Task SettingsPressed_DoesNotStackAndClosedCleansOnlySettingsChild()
    {
        var settingsButton = _menu.GetNode<Button>("VBoxContainer/SettingsButton");
        settingsButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu");

        AssertThat(settings).IsNotNull();
        AssertThat(settings!.Visible).IsTrue();
        AssertThat(CountSettingsChildren(_menu)).IsEqual(1);
        AssertThat(_menu.GetViewport().GuiGetFocusOwner())
            .IsEqual(settings.InitialFocusTarget);
        AssertThat(settings.InitialFocusTarget)
            .IsEqual(settings.GetNode<HSlider>("%MasterSlider"));

        InvokePrivateAcrossHierarchy(_menu, "_on_settings_button_pressed");

        AssertThat(CountSettingsChildren(_menu)).IsEqual(1);

        InvokePrivateAcrossHierarchy(settings, "OnCancelPressed");

        AssertThat(settings.IsQueuedForDeletion()).IsTrue();
        AssertThat(GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu")).IsNull();
        AssertThat(_menu.Visible).IsTrue();

        await AwaitFrames(2);

        AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(settingsButton);
    }

    private partial class TestableMainMenu : MainMenu
    {
        public int QuitRequests { get; private set; }
        protected override void RequestApplicationQuit() => QuitRequests++;
    }

    private static SaveSlotInfo EligibleSlot(int slot, DateTime timestamp) => new()
    {
        SlotIndex = slot,
        Exists = true,
        IsCorrupted = false,
        PlayerName = $"Hero{slot}",
        PlayerLevel = slot + 1,
        FloorIndex = slot,
        Timestamp = timestamp
    };

    private static SaveData ValidSaveData() => new()
    {
        Version = SaveData.CurrentVersion,
        CurrentFloorIndex = 0,
        PlayerPosition = new Vector2IDto { X = 1, Y = 1 },
        SaveTimestamp = DateTime.UtcNow,
        Character = new CharacterSaveData
        {
            Name = "TestHero",
            Level = 1
        }
    };

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

    private async Task AwaitFrames(int frameCount)
    {
        for (var index = 0; index < frameCount; index++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
