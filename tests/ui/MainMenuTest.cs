using GdUnit4;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class MainMenuTest : Node
{
    private static readonly string[] MainMenuSavePaths =
    {
        "user://saves/slot_0.json",
        "user://saves/slot_0.json.bak",
        "user://saves/slot_0.json.tmp",
        "user://saves/slot_1.json",
        "user://saves/slot_1.json.bak",
        "user://saves/slot_1.json.tmp",
        "user://saves/slot_2.json",
        "user://saves/slot_2.json.bak",
        "user://saves/slot_2.json.tmp",
        "user://saves/autosave.json",
        "user://saves/autosave.json.bak",
        "user://saves/autosave.json.tmp"
    };

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
    public void TryOpenMessage_CreatesOneHostedSaveErrorAndKeepsRootVisible()
    {
        InvokePrivateAcrossHierarchy(
            _menu,
            "TryOpenMessage",
            "Load Failed",
            "Save system unavailable.",
            _menu.GetNode<Button>("%LoadButton"),
            null!);

        AssertThat(_menu.Visible).IsTrue();
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsTrue();
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveError))
            .IsEqual(1);
    }

    [TestCase]
    public async Task HostedLoadClosed_ClearsEntryReferenceAndQueuesDialog()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var loadDialog = GetPrivateField<SaveLoadDialog?>(_menu, "_loadDialog");
            AssertThat(loadDialog).IsNotNull();
            AssertThat(_menu.GetNode<UIScreenHost>("%UIScreenHost")
                .IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();

            InvokePrivateAcrossHierarchy(_menu, "OnHostedLoadClosed");
            AssertThat(loadDialog!.IsQueuedForDeletion()).IsTrue();
            AssertThat(GetPrivateField<SaveLoadDialog?>(_menu, "_loadDialog")).IsNull();
            AssertThat(_menu.GetNode<UIScreenHost>("%UIScreenHost")
                .IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        }
        finally
        {
            manager.DeleteSave(0);
        }
    }

    [TestCase]
    public async Task SettingsPressed_DoesNotStackAndHostedCloseRestoresFocus()
    {
        var settingsButton = _menu.GetNode<Button>("%SettingsButton");
        settingsButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var settings = GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu");
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");

        AssertThat(settings).IsNotNull();
        AssertThat(settings!.Visible).IsTrue();
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Settings))
            .IsEqual(1);
        foreach (var button in new[]
        {
            "%ContinueButton", "%NewGameButton", "%LoadButton",
            "%SettingsButton", "%QuitButton"
        })
        {
            AssertThat(_menu.GetNode<Button>(button).Disabled).IsTrue();
        }

        AssertThat(_menu.GetViewport().GuiGetFocusOwner())
            .IsEqual(settings.InitialFocusTarget);

        settingsButton.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(1);
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Settings))
            .IsEqual(1);

        InvokePrivateAcrossHierarchy(settings, "OnCancelPressed");
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Settings)).IsFalse();
        AssertThat(GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu")).IsNull();
        AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(settingsButton);
    }

    [TestCase]
    public async Task RootNewGameHandlerDoesNothingWhileHostedChildIsActive()
    {
        _menu.GetNode<Button>("%SettingsButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        var pendingBefore = SaveManager.Instance?.PendingLoadData;
        InvokePrivateAcrossHierarchy(_menu, "_on_new_game_button_pressed");

        AssertThat(GetPrivateField<bool>(_menu, "_sceneChangeCommitted")).IsFalse();
        AssertThat(SaveManager.Instance?.PendingLoadData).IsEqual(pendingBefore);
    }

    [TestCase]
    public async Task LoadPressedWithNoSavesShowsMessageOnly()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
        await AwaitFrames(2);

        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
    }

    [TestCase]
    public async Task LoadPressedWithSaveOpensExactlyOneSaveLoadEntry()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveLoad))
                .IsEqual(1);
        }
        finally
        {
            manager.DeleteSave(0);
        }
    }

    [TestCase]
    public async Task LoadPressedWithoutSaveManagerShowsOneErrorOnly()
    {
        var property = typeof(SaveManager).GetProperty(
            nameof(SaveManager.Instance),
            BindingFlags.Public | BindingFlags.Static)!;
        var original = SaveManager.Instance;
        property.SetValue(null, null);
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveError))
                .IsEqual(1);
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        }
        finally
        {
            property.SetValue(null, original);
        }
    }

    [TestCase]
    public async Task ContinuePressedUsesSelectedSlot()
    {
        var menu = await CreateTestableRootMenu();
        try
        {
            SetPrivateField(menu, "_continueSave", EligibleSlot(3, DateTime.UtcNow));
            menu.NextLoadResult = null;

            InvokePrivateAcrossHierarchy(menu, "_on_continue_button_pressed");

            AssertThat(menu.LoadRequests).IsEqual(1);
            AssertThat(menu.LastLoadedSlot).IsEqual(3);
            AssertThat(menu.SceneChangeRequests).IsEqual(0);
        }
        finally
        {
            menu.QueueFree();
            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task ContinuePressedWithoutSelectedSaveDoesNothing()
    {
        var menu = await CreateTestableRootMenu();
        try
        {
            menu.NextLoadResult = ValidSaveData();

            InvokePrivateAcrossHierarchy(menu, "_on_continue_button_pressed");

            AssertThat(menu.LoadRequests).IsEqual(0);
            AssertThat(menu.SceneChangeRequests).IsEqual(0);
        }
        finally
        {
            menu.QueueFree();
            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task ContinuePressedAfterSceneChangeCommitDoesNothing()
    {
        var menu = await CreateTestableRootMenu();
        try
        {
            SetPrivateField(menu, "_continueSave", EligibleSlot(3, DateTime.UtcNow));
            SetPrivateField(menu, "_sceneChangeCommitted", true);

            InvokePrivateAcrossHierarchy(menu, "_on_continue_button_pressed");

            AssertThat(menu.LoadRequests).IsEqual(0);
            AssertThat(menu.SceneChangeRequests).IsEqual(0);
        }
        finally
        {
            menu.QueueFree();
            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task ContinueSuccessSetsPendingLoadAndRequestsGameOnce()
    {
        var menu = await CreateTestableRootMenu();
        var previousPending = SaveManager.Instance?.PendingLoadData;
        try
        {
            var loaded = new SaveData();
            menu.NextLoadResult = loaded;
            SetPrivateField(menu, "_continueSave", EligibleSlot(3, DateTime.UtcNow));

            InvokePrivateAcrossHierarchy(menu, "_on_continue_button_pressed");
            InvokePrivateAcrossHierarchy(menu, "_on_continue_button_pressed");

            AssertThat(SaveManager.Instance!.PendingLoadData).IsEqual(loaded);
            AssertThat(menu.LoadRequests).IsEqual(1);
            AssertThat(menu.SceneChangeRequests).IsEqual(1);
            AssertThat(menu.LastScenePath).IsEqual("res://scenes/game/Game.tscn");
        }
        finally
        {
            SaveManager.Instance!.PendingLoadData = previousPending;
            menu.QueueFree();
            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task NewGameClearsPendingLoadAndRequestsGameOnce()
    {
        var menu = await CreateTestableRootMenu();
        var previousPending = SaveManager.Instance?.PendingLoadData;
        try
        {
            SaveManager.Instance!.PendingLoadData = ValidSaveData();

            InvokePrivateAcrossHierarchy(menu, "_on_new_game_button_pressed");
            InvokePrivateAcrossHierarchy(menu, "_on_new_game_button_pressed");

            AssertThat(SaveManager.Instance.PendingLoadData).IsNull();
            AssertThat(menu.SceneChangeRequests).IsEqual(1);
            AssertThat(menu.LastScenePath).IsEqual("res://scenes/game/Game.tscn");
        }
        finally
        {
            SaveManager.Instance!.PendingLoadData = previousPending;
            menu.QueueFree();
            await AwaitFrames(2);
        }
    }

    [TestCase]
    public async Task ContinueFailureOpensHostedFallbackAndRestoresContinueFocus()
    {
        SetPrivateField(_menu, "_continueSave", EligibleSlot(3, DateTime.UtcNow));
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var continueButton = _menu.GetNode<Button>("%ContinueButton");

        InvokePrivateAcrossHierarchy(
            _menu, "HandleContinueLoadResult", new object[] { null! });
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsTrue();
        AssertThat(GetPrivateField<bool>(_menu, "_sceneChangeCommitted")).IsFalse();

        var fallbackError = GetPrivateField<AcceptDialog?>(_menu, "_messageDialog");
        AssertThat(fallbackError).IsNotNull();
        fallbackError!.EmitSignal(AcceptDialog.SignalName.Confirmed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();

        InvokePrivateAcrossHierarchy(_menu, "OnHostedLoadClosed");
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(continueButton);
    }

    [TestCase]
    public async Task ManualLoadFailureClosesLoadThenOpensRootErrorAndRestoresLoadFocus()
    {
        var manager = SaveManager.Instance!;
        var originalSaveFiles = CaptureSaveFiles();
        try
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);

            AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();

            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();

            InvokePrivateAcrossHierarchy(_menu, "OnHostedLoadSlotSelected", 1);
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveError))
                .IsEqual(1);

            var error = GetPrivateField<AcceptDialog?>(_menu, "_messageDialog");
            AssertThat(error).IsNotNull();
            error!.EmitSignal(AcceptDialog.SignalName.Confirmed);
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsFalse();
            AssertThat(_menu.GetNode<Button>("%LoadButton").Disabled).IsFalse();
            AssertThat(_menu.GetViewport().GuiGetFocusOwner())
                .IsEqual(_menu.GetNode<Button>("%LoadButton"));
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);

            RestoreSaveFiles(originalSaveFiles);
        }

        AssertSaveFilesMatch(originalSaveFiles);
    }

    [TestCase]
    public void RootCancelFallbackConsumesWithoutOpeningAnything()
    {
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var input = new InputEventAction { Action = "ui_cancel", Pressed = true };

        AssertThat(host.TryHandleInput(input)).IsEqual(UIInputDispatchResult.Consumed);
        AssertThat(host.ActiveEntries.Count).IsEqual(0);
    }

    [TestCase]
    public async Task TryOpenMessageIsLatchedAndTerminalSignalsCloseOnce()
    {
        var loadButton = _menu.GetNode<Button>("%LoadButton");
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");

        AssertThat((bool)InvokePrivateAcrossHierarchy(
            _menu, "TryOpenMessage", "Load Failed", "First", loadButton, null!)!).IsTrue();
        AssertThat((bool)InvokePrivateAcrossHierarchy(
            _menu, "TryOpenMessage", "Load Failed", "Second", loadButton, null!)!).IsFalse();
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveError))
            .IsEqual(1);

        var popup = GetPrivateField<AcceptDialog?>(_menu, "_messageDialog");
        AssertThat(popup).IsNotNull();
        popup!.EmitSignal(AcceptDialog.SignalName.Confirmed);
        popup.EmitSignal(AcceptDialog.SignalName.Canceled);

        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.SaveError)).IsFalse();
        AssertThat(GetPrivateField<AcceptDialog?>(_menu, "_messageDialog")).IsNull();
        AssertThat(_menu.GetNode<Button>("%LoadButton").Disabled).IsFalse();
        AssertThat(_menu.GetViewport().GuiGetFocusOwner())
            .IsEqual(loadButton);
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
    public async Task InitialFocusWithoutSaveIsNewGame()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        try
        {
            var menu = await RecreateProductionMenu();
            AssertThat(menu.GetViewport().GuiGetFocusOwner())
                .IsEqual(menu.GetNode<Button>("%NewGameButton"));
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);
        }
    }

    [TestCase]
    public async Task InitialFocusWithEligibleSaveIsContinue()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            var menu = await RecreateProductionMenu();
            AssertThat(menu.GetViewport().GuiGetFocusOwner())
                .IsEqual(menu.GetNode<Button>("%ContinueButton"));
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);
        }
    }

    private async Task<MainMenu> RecreateProductionMenu()
    {
        if (GodotObject.IsInstanceValid(_menu))
            _menu.QueueFree();
        await AwaitFrames(2);

        var scene = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
        _menu = scene.Instantiate<MainMenu>();
        _sceneTree.Root.AddChild(_menu);
        await AwaitFrames(2);
        return _menu;
    }

    private partial class TestableMainMenu : MainMenu
    {
        public int QuitRequests { get; private set; }
        public int SceneChangeRequests { get; private set; }
        public int LoadRequests { get; private set; }
        public int LastLoadedSlot { get; private set; } = -1;
        public string? LastScenePath { get; private set; }
        public SaveData? NextLoadResult { get; set; }

        public override void _Ready()
        {
            // Controller-only fixture. Production scene binding/layout has separate tests.
        }

        protected override void RequestApplicationQuit() => QuitRequests++;

        protected override SaveData? LoadSlot(int slot)
        {
            LoadRequests++;
            LastLoadedSlot = slot;
            return NextLoadResult;
        }

        protected override Error ChangeSceneToFile(string path)
        {
            SceneChangeRequests++;
            LastScenePath = path;
            return Error.Ok;
        }
    }

    private async Task<TestableMainMenu> CreateTestableRootMenu()
    {
        var menu = new TestableMainMenu();
        _sceneTree.Root.AddChild(menu);
        await AwaitFrames(1);
        return menu;
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

    private sealed record SaveFileSnapshot(string VirtualPath, bool Exists, byte[]? Data);

    private static SaveFileSnapshot[] CaptureSaveFiles() =>
        MainMenuSavePaths.Select(path =>
        {
            var absolutePath = ProjectSettings.GlobalizePath(path);
            var exists = System.IO.File.Exists(absolutePath);
            return new SaveFileSnapshot(
                path,
                exists,
                exists ? System.IO.File.ReadAllBytes(absolutePath) : null);
        }).ToArray();

    private static void RestoreSaveFiles(SaveFileSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var absolutePath = ProjectSettings.GlobalizePath(snapshot.VirtualPath);
            if (snapshot.Exists)
            {
                var directory = System.IO.Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    System.IO.Directory.CreateDirectory(directory);

                System.IO.File.WriteAllBytes(absolutePath, snapshot.Data!);
            }
            else if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
        }
    }

    private static void AssertSaveFilesMatch(SaveFileSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var absolutePath = ProjectSettings.GlobalizePath(snapshot.VirtualPath);
            var exists = System.IO.File.Exists(absolutePath);
            AssertThat(exists).IsEqual(snapshot.Exists);
            if (snapshot.Exists)
            {
                var bytes = System.IO.File.ReadAllBytes(absolutePath);
                AssertThat(bytes.SequenceEqual(snapshot.Data!)).IsTrue();
            }
        }
    }

    private static object? InvokePrivateAcrossHierarchy(object instance, string methodName, params object[] arguments)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(methodName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method != null)
            {
                return method.Invoke(instance, arguments);
            }
        }

        throw new MissingMethodException(instance.GetType().Name, methodName);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(instance, value);
            return;
        }

        throw new MissingFieldException(instance.GetType().Name, fieldName);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (T)field.GetValue(instance)!;
    }

    private async Task AwaitFrames(int frameCount)
    {
        for (var index = 0; index < frameCount; index++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
