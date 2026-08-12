using GdUnit4;
using Godot;
using System;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveLoadScreenControllerTest : Node
{
    private const string ScenePath = "res://scenes/ui/SaveLoadScreen.tscn";

    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private SubViewport? _viewport;
    private SaveLoadScreenController? _screen;
    private TestHelpers.SaveFileSnapshot[] _saveFiles = null!;

    [BeforeTest]
    public void SetUp()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _saveFiles = TestHelpers.CaptureSaveFiles();
    }

    [AfterTest]
    public async Task TearDown()
    {
        try
        {
            if (_screen != null && GodotObject.IsInstanceValid(_screen))
                _screen.QueueFree();
            if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
                _viewport.QueueFree();
            if (_container != null && GodotObject.IsInstanceValid(_container))
                _container.QueueFree();

            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            TestHelpers.RestoreSaveFiles(_saveFiles);
            TestHelpers.ReportSaveFileMismatches(
                _saveFiles,
                nameof(SaveLoadScreenControllerTest));
            _screen = null;
            _viewport = null;
            _container = null;
        }
    }

    [TestCase]
    public async Task SaveMode_EmptyManualSlotEmitsSaveOnce()
    {
        DeleteSlot(0);
        await InstantiateScreen(SaveLoadMode.Save);

        var saves = 0;
        _screen!.SaveSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(0);
            saves++;
        };

        PressCard(0);
        PressCard(0);

        AssertThat(saves).IsEqual(1);
    }

    [TestCase]
    public async Task SaveMode_ValidManualSlotEmitsOverwriteRequestedAndStaysActive()
    {
        WriteValidSlot(0, "Aster", 4);
        await InstantiateScreen(SaveLoadMode.Save);

        var overwriteRequests = 0;
        var saves = 0;
        _screen!.OverwriteRequested += slot =>
        {
            AssertThat(slot).IsEqual(0);
            overwriteRequests++;
        };
        _screen.SaveSlotSelected += _ => saves++;

        PressCard(0);
        PressCard(0);

        AssertThat(overwriteRequests).IsEqual(2);
        AssertThat(saves).IsEqual(0);
        AssertThat(_screen.InitialFocusTarget).IsEqual(_screen.GetNode<Button>("%Slot0Card"));
    }

    [TestCase]
    public async Task SaveMode_CorruptedManualSlotEmitsSaveWithoutConfirmation()
    {
        WriteRawSlot(0, "{not valid json");
        await InstantiateScreen(SaveLoadMode.Save);

        var saves = 0;
        var overwrites = 0;
        _screen!.SaveSlotSelected += _ => saves++;
        _screen.OverwriteRequested += _ => overwrites++;

        PressCard(0);

        AssertThat(saves).IsEqual(1);
        AssertThat(overwrites).IsEqual(0);
    }

    [TestCase]
    public async Task SaveMode_IncompatibleManualSlotEmitsSaveWithoutConfirmation()
    {
        WriteRawSlot(0, "{\"Version\":999,\"Character\":{\"Name\":\"Future\",\"Level\":9}}");
        await InstantiateScreen(SaveLoadMode.Save);

        var saves = 0;
        var overwrites = 0;
        _screen!.SaveSlotSelected += _ => saves++;
        _screen.OverwriteRequested += _ => overwrites++;

        PressCard(0);

        AssertThat(saves).IsEqual(1);
        AssertThat(overwrites).IsEqual(0);
        AssertThat(_screen.GetNode<Button>("%Slot0Card").GetNode<Label>("Margin/Content/StateLabel").Text)
            .Contains("Incompatible");
    }

    [TestCase]
    public async Task SaveMode_AutosaveDisabledWithReason()
    {
        DeleteSlot(3);
        await InstantiateScreen(SaveLoadMode.Save);

        var autosave = _screen!.GetNode<Button>("%Slot3Card");
        var saves = 0;
        _screen.SaveSlotSelected += _ => saves++;

        AssertThat(autosave.Disabled).IsTrue();
        AssertThat(autosave.GetNode<Label>("Margin/Content/ActionLabel").Text)
            .Contains("read-only");

        PressCard(3);
        AssertThat(saves).IsEqual(0);
    }

    [TestCase]
    public async Task LoadMode_ValidManualSlotEmitsLoadOnce()
    {
        WriteValidSlot(0, "Aster", 4);
        await InstantiateScreen(SaveLoadMode.Load);

        var loads = 0;
        _screen!.LoadSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(0);
            loads++;
        };

        PressCard(0);
        PressCard(0);

        AssertThat(loads).IsEqual(1);
    }

    [TestCase]
    public async Task LoadMode_ValidAutosaveEmitsLoadOnce()
    {
        WriteValidSlot(3, "Aster", 4);
        await InstantiateScreen(SaveLoadMode.Load);

        var loads = 0;
        _screen!.LoadSlotSelected += slot =>
        {
            AssertThat(slot).IsEqual(3);
            loads++;
        };

        PressCard(3);

        AssertThat(loads).IsEqual(1);
    }

    [TestCase]
    public async Task LoadMode_CorruptedDisabledWithReadableReason()
    {
        WriteRawSlot(0, "{not valid json");
        await InstantiateScreen(SaveLoadMode.Load);

        var card = _screen!.GetNode<Button>("%Slot0Card");
        var loads = 0;
        _screen.LoadSlotSelected += _ => loads++;

        AssertThat(card.Disabled).IsTrue();
        AssertThat(card.GetNode<Label>("Margin/Content/StateLabel").Text)
            .Contains("Corrupted");
        AssertThat(card.GetNode<Label>("Margin/Content/ActionLabel").Text)
            .Contains("Unavailable");

        PressCard(0);
        AssertThat(loads).IsEqual(0);
    }

    [TestCase]
    public async Task LoadMode_IncompatibleDisabledWithReadableReason()
    {
        WriteRawSlot(0, "{\"Version\":999,\"Character\":{\"Name\":\"Future\",\"Level\":9}}");
        await InstantiateScreen(SaveLoadMode.Load);

        var card = _screen!.GetNode<Button>("%Slot0Card");
        var loads = 0;
        _screen.LoadSlotSelected += _ => loads++;

        AssertThat(card.Disabled).IsTrue();
        AssertThat(card.GetNode<Label>("Margin/Content/StateLabel").Text)
            .Contains("Incompatible");
        AssertThat(card.GetNode<Label>("Margin/Content/ActionLabel").Text)
            .Contains("Unavailable");

        PressCard(0);
        AssertThat(loads).IsEqual(0);
    }

    [TestCase]
    public async Task NoActionableLoad_InitialFocusIsCancel()
    {
        DeleteSlot(0);
        DeleteSlot(1);
        DeleteSlot(2);
        DeleteSlot(3);
        await InstantiateScreen(SaveLoadMode.Load);

        AssertThat(_screen!.InitialFocusTarget)
            .IsEqual(_screen.GetNode<Button>("%CancelButton"));
    }

    [TestCase]
    public async Task Close_EmitsOnce()
    {
        await InstantiateScreen(SaveLoadMode.Load);

        var closed = 0;
        _screen!.Closed += () => closed++;
        var cancel = _screen.GetNode<Button>("%CancelButton");

        cancel.EmitSignal(Button.SignalName.Pressed);
        cancel.EmitSignal(Button.SignalName.Pressed);

        AssertThat(closed).IsEqual(1);
    }

    [TestCase]
    public async Task SaveMode_MainMenu_EmitsOnce()
    {
        await InstantiateScreen(SaveLoadMode.Save);

        var mainMenu = 0;
        var closed = 0;
        _screen!.MainMenuRequested += () => mainMenu++;
        _screen.Closed += () => closed++;
        var button = _screen.GetNode<Button>("%MainMenuButton");

        button.EmitSignal(Button.SignalName.Pressed);
        button.EmitSignal(Button.SignalName.Pressed);

        AssertThat(mainMenu).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    [TestCase]
    public async Task TerminalActivation_IgnoresLaterTerminalPresses()
    {
        DeleteSlot(0);
        await InstantiateScreen(SaveLoadMode.Save);

        var saves = 0;
        var closed = 0;
        _screen!.SaveSlotSelected += _ => saves++;
        _screen.Closed += () => closed++;

        PressCard(0);
        _screen.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed);

        AssertThat(saves).IsEqual(1);
        AssertThat(closed).IsEqual(0);
    }

    private async Task InstantiateScreen(SaveLoadMode mode, Vector2I size = default)
    {
        if (size == default)
            size = new Vector2I(1280, 720);

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

        _screen = scene.Instantiate<SaveLoadScreenController>();
        _screen.Mode = mode;
        _viewport.AddChild(_screen);
        await AwaitFrames(2);
    }

    private void PressCard(int slot) =>
        _screen!.GetNode<Button>($"%Slot{slot}Card")
            .EmitSignal(Button.SignalName.Pressed);

    private static void DeleteSlot(int slot)
    {
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager != null)
            manager.DeleteSave(slot);
    }

    private static void WriteValidSlot(int slot, string playerName, int level)
    {
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        var data = new SaveData
        {
            Version = SaveData.CurrentVersion,
            CurrentFloorIndex = 2,
            PlayerPosition = new Vector2IDto { X = 4, Y = 5 },
            Character = new CharacterSaveData { Name = playerName, Level = level },
            SaveTimestamp = DateTime.UtcNow
        };

        if (slot == 3)
            AssertThat(manager.AutoSave(data)).IsTrue();
        else
            AssertThat(manager.SaveGame(slot, data)).IsTrue();
    }

    private static void WriteRawSlot(int slot, string text)
    {
        var path = slot == 3
            ? "user://saves/autosave.json"
            : $"user://saves/slot_{slot}.json";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        AssertThat(file).IsNotNull();
        file?.StoreString(text);
        file?.Flush();
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
