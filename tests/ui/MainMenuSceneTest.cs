using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class MainMenuSceneTest : Node
{
    private const string ScenePath = "res://scenes/ui/MainMenu.tscn";
    private SceneTree _sceneTree = null!;
    private SubViewportContainer? _container;
    private MainMenu? _menu;
    // ResizeAndCreate() adds the production MainMenu to the viewport, which runs
    // _Ready() -> RefreshContinueState() -> SaveManager.GetSaveSlotInfo() for all
    // four slots. GetSaveSlotInfo() is not read-only: when a primary save is
    // missing but its .bak exists, it renames the backup into the primary file.
    // Snapshot/restore user://saves so this layout suite cannot mutate a
    // developer's real save-file layout (e.g. a backup-only slot after an
    // interrupted save). Mirrors the protection in MainMenuTest.
    private TestHelpers.SaveFileSnapshot[]? _originalSaveFiles;

    [BeforeTest]
    public void Setup()
    {
        _originalSaveFiles = TestHelpers.CaptureSaveFiles();
        _sceneTree = (SceneTree)Engine.GetMainLoop();
    }

    [AfterTest]
    public async Task Cleanup()
    {
        try
        {
            if (_container != null && GodotObject.IsInstanceValid(_container))
                _container.QueueFree();
            _container = null;
            _menu = null;
            await AwaitFrames(2);
        }
        finally
        {
            var snapshots = _originalSaveFiles;
            _originalSaveFiles = null;
            if (snapshots != null)
            {
                TestHelpers.RestoreSaveFiles(snapshots);
                TestHelpers.ReportSaveFileMismatches(snapshots, nameof(MainMenuSceneTest));
            }
        }
    }

    [TestCase]
    public void SceneOwnsApprovedStructureBeforeReady()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();
        var menu = packed!.Instantiate<MainMenu>();
        try
        {
            foreach (var path in new[]
            {
                "%MainMenuContent", "%SafeFrame", "%MenuRail", "%WordmarkLabel",
                "%ContinueButton", "%ContinueSummary", "%ContinueSlotLabel",
                "%ContinueDetailLabel", "%ContinueTimestampLabel", "%NewGameButton",
                "%LoadButton", "%SettingsButton", "%QuitButton", "%SelectHint",
                "%UIScreenHost"
            })
            {
                AssertThat(menu.GetNodeOrNull(path)).IsNotNull();
            }

            AssertThat(menu.GetNodeOrNull("VBoxContainer")).IsNull();
            AssertThat(menu.Theme).IsNotNull();
            var background = menu.GetNode<TextureRect>("Background");
            AssertThat(background.Texture).IsNotNull();
            AssertThat(background.StretchMode)
                .IsEqual(TextureRect.StretchModeEnum.KeepAspectCovered);
        }
        finally
        {
            menu.Free();
        }
    }

    [TestCase]
    public void ProductionSceneOwnsExactlyOneUIScreenHost()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        var menu = packed.Instantiate<MainMenu>();
        try
        {
            var count = 0;
            foreach (var child in menu.GetChildren())
            {
                if (child is UIScreenHost)
                    count++;
            }

            AssertThat(count).IsEqual(1);
            AssertThat(menu.GetNode<UIScreenHost>("%UIScreenHost")).IsNotNull();
        }
        finally
        {
            menu.Free();
        }
    }

    [TestCase(640, 360)]
    [TestCase(1280, 720)]
    public async Task LayoutFitsWithContinueSummaryVisible(int width, int height)
    {
        var menu = await ResizeAndCreate(new Vector2I(width, height));
        var info = new SaveSlotInfo
        {
            SlotIndex = 3,
            Exists = true,
            IsCorrupted = false,
            PlayerName = "LayoutHero",
            PlayerLevel = 7,
            FloorIndex = 2,
            Timestamp = new DateTime(2026, 8, 9, 20, 0, 0, DateTimeKind.Utc)
        };

        SetPrivateField(menu, "_continueSave", info);
        InvokePrivateAcrossHierarchyWithResult(menu, "RefreshContinuePresentation");
        await AwaitFrames(2);

        var compact = SiriusUiMetrics.IsCompact(new Vector2(width, height));
        var safe = menu.GetNode<Control>("%SafeFrame");
        var rail = menu.GetNode<Control>("%MenuRail");
        var summary = menu.GetNode<Control>("%ContinueSummary");
        var slot = menu.GetNode<Label>("%ContinueSlotLabel");
        var detail = menu.GetNode<Label>("%ContinueDetailLabel");
        var timestamp = menu.GetNode<Label>("%ContinueTimestampLabel");

        AssertThat(summary.Visible).IsTrue();
        AssertEnclosed(safe, rail);
        AssertEnclosed(safe, summary);
        AssertThat(detail.Size.Y).IsGreater(0f);
        AssertThat(slot.Visible).IsEqual(!compact);
        AssertThat(timestamp.Visible).IsEqual(!compact);

        if (!compact)
        {
            AssertThat(timestamp.Text)
                .IsEqual(info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        }

        foreach (var name in new[]
        {
            "%ContinueButton", "%NewGameButton", "%LoadButton",
            "%SettingsButton", "%QuitButton"
        })
        {
            AssertThat(menu.GetNode<Button>(name).Size.Y)
                .IsGreaterEqual(SiriusUiMetrics.MinimumTarget(compact).Y);
        }
    }

    [TestCase(640, 360)]
    [TestCase(1024, 768)]
    [TestCase(1280, 720)]
    [TestCase(1440, 900)]
    [TestCase(1920, 1080)]
    [TestCase(2560, 1080)]
    [TestCase(2560, 1440)]
    public async Task LayoutRailStaysEnclosedAcrossApprovedViewports(int width, int height)
    {
        var menu = await ResizeAndCreate(new Vector2I(width, height));
        SetPrivateField(menu, "_continueSave", new SaveSlotInfo
        {
            SlotIndex = 0,
            Exists = true,
            PlayerName = "ViewportHero",
            PlayerLevel = 4,
            FloorIndex = 1,
            Timestamp = DateTime.UtcNow
        });
        InvokePrivateAcrossHierarchyWithResult(menu, "RefreshContinuePresentation");
        await AwaitFrames(2);

        var safeFrame = menu.GetNode<Control>("%SafeFrame");
        var rail = menu.GetNode<VBoxContainer>("%MenuRail");
        foreach (var child in rail.GetChildren())
        {
            if (child is Control control && control.Visible)
                AssertEnclosed(safeFrame, control);
        }
    }

    [TestCase]
    public async Task PassiveChromeDoesNotCaptureInputOrFocus()
    {
        var menu = await ResizeAndCreate(new Vector2I(1280, 720));
        foreach (var name in new[]
        {
            "%Background", "%WordmarkLabel", "%ContinueSummary",
            "%ContinueSlotLabel", "%ContinueDetailLabel", "%ContinueTimestampLabel"
        })
        {
            var control = menu.GetNode<Control>(name);
            AssertThat(control.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
            AssertThat(control.FocusMode).IsEqual(Control.FocusModeEnum.None);
        }
    }

    private async Task<MainMenu> ResizeAndCreate(Vector2I size)
    {
        _container = new SubViewportContainer
        {
            Size = size,
            Stretch = true
        };
        _sceneTree.Root.AddChild(_container);

        var viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            GuiEmbedSubwindows = true,
            Size = size
        };
        _container.AddChild(viewport);

        var packed = GD.Load<PackedScene>(ScenePath);
        AssertThat(packed).IsNotNull();
        _menu = packed!.Instantiate<MainMenu>();
        viewport.AddChild(_menu);
        await AwaitFrames(2);
        return _menu;
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    private static void AssertEnclosed(Control outer, Control inner)
    {
        var outerRect = outer.GetGlobalRect();
        var innerRect = inner.GetGlobalRect();
        AssertThat(innerRect.Position.X).IsGreaterEqual(outerRect.Position.X - 0.5f);
        AssertThat(innerRect.Position.Y).IsGreaterEqual(outerRect.Position.Y - 0.5f);
        AssertThat(innerRect.End.X).IsLessEqual(outerRect.End.X + 0.5f);
        AssertThat(innerRect.End.Y).IsLessEqual(outerRect.End.Y + 0.5f);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;

            field.SetValue(instance, value);
            return;
        }

        throw new MissingFieldException(instance.GetType().Name, fieldName);
    }

    private static object? InvokePrivateAcrossHierarchyWithResult(
        object instance,
        string methodName,
        params object[] arguments)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (method != null)
                return method.Invoke(instance, arguments);
        }
        throw new MissingMethodException(instance.GetType().Name, methodName);
    }
}
