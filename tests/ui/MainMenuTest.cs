using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class MainMenuTest : Node
{
    private MainMenu _menu = null!;
    private SceneTree _sceneTree = null!;
    private TestHelpers.SaveFileSnapshot[]? _originalSaveFiles;

    [BeforeTest]
    public async Task Setup()
    {
        // Main-menu tests intentionally exercise clean-room save setup. Own the
        // persistent-file snapshot at fixture scope so every save-mutating path
        // is restored even when a test fails before reaching its local cleanup.
        _originalSaveFiles = TestHelpers.CaptureSaveFiles();
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        var scene = GD.Load<PackedScene>("res://scenes/ui/MainMenu.tscn");
        _menu = scene.Instantiate<MainMenu>();
        _sceneTree.Root.AddChild(_menu);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        try
        {
            if (GodotObject.IsInstanceValid(_menu))
                _menu.QueueFree();

            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
        }
        finally
        {
            RestoreOriginalSaveFiles();
        }
    }

    private void RestoreOriginalSaveFiles()
    {
        var snapshots = _originalSaveFiles;
        _originalSaveFiles = null;
        if (snapshots == null)
            return;

        TestHelpers.RestoreSaveFiles(snapshots);
        TestHelpers.ReportSaveFileMismatches(snapshots, nameof(MainMenuTest));
    }

    [TestCase]
    public void MainMenuSavePathsAgreeWithSaveManagerSymbols()
    {
        var saveDir = GetSaveManagerConstant("SaveDir");
        var slotFileFormat = GetSaveManagerConstant("SlotFileFormat");
        var autosaveFile = GetSaveManagerConstant("AutosaveFile");

        var expected = new List<string>();
        foreach (var slot in new[] { 0, 1, 2 })
        {
            var fileName = string.Format(slotFileFormat, slot);
            expected.Add($"{saveDir}/{fileName}");
            expected.Add($"{saveDir}/{fileName}.bak");
            expected.Add($"{saveDir}/{fileName}.tmp");
        }
        expected.Add($"{saveDir}/{autosaveFile}");
        expected.Add($"{saveDir}/{autosaveFile}.bak");
        expected.Add($"{saveDir}/{autosaveFile}.tmp");

        var unexpected = TestHelpers.UserSaveFilePaths.Except(expected).ToList();
        var missing = expected.Except(TestHelpers.UserSaveFilePaths).ToList();
        AssertThat(unexpected.Count).IsEqual(0);
        AssertThat(missing.Count).IsEqual(0);
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
    public async Task RootPrompt_ConfiguredCancelUsesPromptLatchAndRestoresRoot()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
        await AwaitFrames(2);

        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var loadButton = _menu.GetNode<Button>("%LoadButton");
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Parent).IsNull();
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(_menu.Visible).IsTrue();

        foreach (var button in new[]
        {
            "%ContinueButton", "%NewGameButton", "%LoadButton",
            "%SettingsButton", "%QuitButton"
        })
        {
            AssertThat(_menu.GetNode<Button>(button).Disabled).IsTrue();
        }

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(prompt.GetNode<Label>("%Message").Text).IsEqual("No save files found!");

        // Warning prompts are one-action: the configured Cancel latches to the
        // Primary terminal handling and must not fall through to the root.
        var cancel = new InputEventAction { Action = "ui_cancel", Pressed = true };
        AssertThat(host.TryHandleInput(cancel)).IsEqual(UIInputDispatchResult.Consumed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        // ContinueButton is independently gated by the absence of a save;
        // the remaining root actions must be re-enabled by cleanup.
        foreach (var button in new[]
        {
            "%NewGameButton", "%LoadButton", "%SettingsButton", "%QuitButton"
        })
        {
            AssertThat(_menu.GetNode<Button>(button).Disabled).IsFalse();
        }

        AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(loadButton);
    }

    [TestCase]
    public async Task RootPrompt_TerminalCannotRunTwice()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
        await AwaitFrames(2);

        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);

        var primary = prompt.GetNode<Button>("%PrimaryButton");
        primary.EmitSignal(Button.SignalName.Pressed);
        primary.EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
            .IsEqual(0);
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(prompt)).IsFalse();
        // ContinueButton is independently gated by the absence of a save;
        // the remaining root actions must be re-enabled by cleanup.
        foreach (var button in new[]
        {
            "%NewGameButton", "%LoadButton", "%SettingsButton", "%QuitButton"
        })
        {
            AssertThat(_menu.GetNode<Button>(button).Disabled).IsFalse();
        }
    }

    [TestCase]
    public async Task HostedLoad_CloseRestoresLoadButtonFocus()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var loadScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
            AssertThat(loadScreen).IsNotNull();
            AssertThat(loadScreen!.Mode).IsEqual(SaveLoadMode.Load);
            AssertThat(_menu.GetNode<UIScreenHost>("%UIScreenHost")
                .IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();

            InvokePrivateAcrossHierarchy(_menu, "OnHostedLoadClosed");
            AssertThat(loadScreen.IsQueuedForDeletion()).IsTrue();
            AssertThat(GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen")).IsNull();
            AssertThat(_menu.GetNode<UIScreenHost>("%UIScreenHost")
                .IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();

            await AwaitFrames(2);
            AssertThat(_menu.GetViewport().GuiGetFocusOwner())
                .IsEqual(_menu.GetNode<Button>("%LoadButton"));
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
    public async Task LoadPressed_NoSaveFilesOpensWarningPromptAndRestoresLoadFocus()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
        await AwaitFrames(2);

        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
        AssertThat(promptEntry.Policy.Parent).IsNull();

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(prompt.GetNode<Label>("%Message").Text).IsEqual("No save files found!");
        AssertThat(prompt.GetNode<Button>("%CancelButton").Visible).IsFalse();
        AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(prompt.InitialFocusTarget);

        prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
        AssertThat(_menu.GetNode<Button>("%LoadButton").Disabled).IsFalse();
        AssertThat(_menu.GetViewport().GuiGetFocusOwner())
            .IsEqual(_menu.GetNode<Button>("%LoadButton"));
    }

    [TestCase]
    public async Task LoadPressed_HostsSceneAuthoredLoadScreen()
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
            var loadScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
            AssertThat(loadScreen).IsNotNull();
            AssertThat(loadScreen!.Mode).IsEqual(SaveLoadMode.Load);
            AssertThat(loadScreen.GetNodeOrNull<Button>("%Slot0Card")).IsNotNull();
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
    public async Task HostedLoad_InitialFocusUsesFirstActionableCard()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var loadScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
            AssertThat(loadScreen).IsNotNull();
            var firstCard = loadScreen!.GetNode<Button>("%Slot0Card");
            AssertThat(firstCard.Disabled).IsFalse();
            AssertThat(loadScreen.InitialFocusTarget).IsEqual(firstCard);
            AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(firstCard);
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);
        }
    }

    [TestCase]
    public async Task LoadPressedTwice_DoesNotStackSaveLoadEntries()
    {
        var manager = SaveManager.Instance!;
        for (var slot = 0; slot <= 3; slot++)
            manager.DeleteSave(slot);

        AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();
        try
        {
            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);
            var firstScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
            AssertThat(firstScreen).IsNotNull();

            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(1);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            AssertThat(GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen"))
                .IsEqual(firstScreen);
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.SaveLoad))
                .IsEqual(1);
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);
        }
    }

    [TestCase]
    public async Task LoadUnavailable_OpensRecoverablePromptWithoutAcceptDialog()
    {
        PropertyInfo? property = null;
        object? original = null;
        try
        {
            property = typeof(SaveManager).GetProperty(
                nameof(SaveManager.Instance),
                BindingFlags.Public | BindingFlags.Static)!;
            original = SaveManager.Instance;
            property.SetValue(null, null);

            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            var modalLayer = host.GetNode<Control>("ModalLayer");
            AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
                .IsEqual(1);
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsFalse();
            var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
            AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
            AssertThat(modalLayer.FindChildren("*", "AcceptDialog", true, false).Count)
                .IsEqual(0);

            var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
            AssertThat(prompt.GetNode<Label>("%Message").Text)
                .IsEqual("Save system unavailable.");
            AssertThat(prompt.GetNode<SiriusModalShell>("%ModalShell").Severity)
                .IsEqual(SiriusUiSeverity.Error);
        }
        finally
        {
            property?.SetValue(null, original);
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
    public async Task ContinueFailure_WithHostedLoadKeepsLoadParentAndShowsPrompt()
    {
        SetPrivateField(_menu, "_continueSave", EligibleSlot(3, DateTime.UtcNow));
        var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
        var modalLayer = host.GetNode<Control>("ModalLayer");

        InvokePrivateAcrossHierarchy(
            _menu, "HandleContinueLoadResult", new object[] { null! });
        await AwaitFrames(2);

        AssertThat(GetPrivateField<bool>(_menu, "_sceneChangeCommitted")).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
        var loadEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.SaveLoad);
        var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
        AssertThat(promptEntry.Policy.Parent).IsEqual(loadEntry.Handle);
        AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);

        var loadScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
        AssertThat(loadScreen).IsNotNull();
        AssertThat(loadScreen!.Mode).IsEqual(SaveLoadMode.Load);
        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(prompt.GetNode<Label>("%Message").Text)
            .IsEqual("Failed to load the selected save.");

        prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
        AssertThat(GodotObject.IsInstanceValid(loadScreen)).IsTrue();
    }

    [TestCase]
    public async Task HostedLoadFailure_KeepsLoadParentAfterAcknowledge()
    {
        var manager = SaveManager.Instance!;
        try
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);

            AssertThat(manager.SaveGame(0, ValidSaveData())).IsTrue();

            InvokePrivateAcrossHierarchy(_menu, "_on_load_button_pressed");
            await AwaitFrames(2);

            var host = _menu.GetNode<UIScreenHost>("%UIScreenHost");
            var modalLayer = host.GetNode<Control>("ModalLayer");
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
            var loadScreen = GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen");
            AssertThat(loadScreen).IsNotNull();

            // Slot 1 has no save file: the failure Prompt must open synchronously
            // under the still-active Load entry (no close + deferred fallback).
            InvokePrivateAcrossHierarchy(_menu, "OnHostedLoadSlotSelected", 1);

            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
            AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsTrue();
            AssertThat(loadScreen!.IsQueuedForDeletion()).IsFalse();
            AssertThat(GetPrivateField<SaveLoadScreenController?>(_menu, "_loadScreen"))
                .IsEqual(loadScreen);
            var loadEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.SaveLoad);
            var promptEntry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Prompt);
            AssertThat(promptEntry.Policy.Parent).IsEqual(loadEntry.Handle);
            AssertThat(promptEntry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
            AssertThat(FindDirectChild<SiriusPrompt>(modalLayer)
                .GetNode<Label>("%Message").Text).IsEqual("Failed to load save file.");

            FindDirectChild<SiriusPrompt>(modalLayer)
                .GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);
            await AwaitFrames(2);

            AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
            AssertThat(host.IsKindActive(UIScreenKinds.SaveLoad)).IsTrue();
            AssertThat(GodotObject.IsInstanceValid(loadScreen)).IsTrue();
        }
        finally
        {
            for (var slot = 0; slot <= 3; slot++)
                manager.DeleteSave(slot);
        }
    }

    [TestCase]
    public async Task HostedLoadSlotSelected_SetsPendingLoadAndUsesExistingSceneTransition()
    {
        var menu = await CreateTestableRootMenu();
        var previousPending = SaveManager.Instance?.PendingLoadData;
        try
        {
            var loaded = ValidSaveData();
            menu.NextLoadResult = loaded;

            InvokePrivateAcrossHierarchy(menu, "OnHostedLoadSlotSelected", 2);

            AssertThat(SaveManager.Instance!.PendingLoadData).IsEqual(loaded);
            AssertThat(menu.LoadRequests).IsEqual(1);
            AssertThat(menu.LastLoadedSlot).IsEqual(2);
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
        var modalLayer = host.GetNode<Control>("ModalLayer");

        AssertThat((bool)InvokePrivateAcrossHierarchy(
            _menu, "TryOpenMessage", SiriusPromptVariant.Warning,
            "Load Failed", "First", loadButton, null!, null!)!).IsTrue();
        AssertThat((bool)InvokePrivateAcrossHierarchy(
            _menu, "TryOpenMessage", SiriusPromptVariant.Warning,
            "Load Failed", "Second", loadButton, null!, null!)!).IsFalse();
        AssertThat(host.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Prompt))
            .IsEqual(1);

        var prompt = FindDirectChild<SiriusPrompt>(modalLayer);
        AssertThat(prompt.GetNode<Label>("%Message").Text).IsEqual("First");
        prompt.GetNode<Button>("%PrimaryButton").EmitSignal(Button.SignalName.Pressed);

        await AwaitFrames(2);

        AssertThat(host.IsKindActive(UIScreenKinds.Prompt)).IsFalse();
        AssertThat(GetPrivateField<SiriusPrompt?>(_menu, "_messagePrompt")).IsNull();
        AssertThat(GetPrivateField<UIScreenHandle?>(_menu, "_messageHandle")).IsNull();
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

    private static T FindDirectChild<T>(Node parent) where T : Node
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T typed)
                return typed;
        }

        throw new InvalidOperationException($"Direct child '{typeof(T).Name}' was not found.");
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
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null)
                return (T)field.GetValue(instance)!;
        }

        throw new MissingFieldException(instance.GetType().Name, fieldName);
    }

    private static string GetSaveManagerConstant(string fieldName)
    {
        var field = typeof(SaveManager).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field == null || field.GetValue(null) is not string value)
            throw new InvalidOperationException(
                $"SaveManager constant '{fieldName}' not found via reflection.");

        return value;
    }

    private async Task AwaitFrames(int frameCount)
    {
        for (var index = 0; index < frameCount; index++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }
}
