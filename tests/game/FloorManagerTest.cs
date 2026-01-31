using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorManagerTest : Node
{
    [TestCase]
    public async Task TestReady_SkipsInitialLoad_WhenPendingSaveData()
    {
        var saveManager = await EnsureSaveManager();
        var previousPending = saveManager.PendingLoadData;
        saveManager.PendingLoadData = new SaveData
        {
            CurrentFloorIndex = 1,
            PlayerPosition = new Vector2IDto(new Vector2I(1, 1)),
            SaveTimestamp = DateTime.UtcNow
        };

        var floorManager = new FloorManager
        {
            EnableDebugLogging = false
        };
        floorManager.Floors.Add(new FloorDefinition { FloorName = "Test Floor" });

        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(floorManager);
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        var currentFloorInstance = GetPrivateFieldValue<Node2D>(floorManager, "_currentFloorInstance");
        AssertThat(currentFloorInstance).IsNull();
        AssertThat(floorManager.CurrentGridMap).IsNull();

        floorManager.QueueFree();
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        saveManager.PendingLoadData = previousPending;
        if (!ReferenceEquals(saveManager, SaveManager.Instance))
        {
            saveManager.Free();
        }
    }

    private static async Task<SaveManager> EnsureSaveManager()
    {
        if (SaveManager.Instance != null)
        {
            return SaveManager.Instance;
        }

        var saveManager = new SaveManager();
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(saveManager);
        await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
        return SaveManager.Instance ?? saveManager;
    }

    private static T GetPrivateFieldValue<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(instance) as T;
    }
}
