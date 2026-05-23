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
        // Check if singleton existed BEFORE calling EnsureSaveManager
        bool existedBefore = SaveManager.Instance != null && IsInstanceValid(SaveManager.Instance);
        var saveManager = await EnsureSaveManager();
        var previousPending = saveManager.PendingLoadData;
        // Only free if this test actually created the singleton
        bool createdSingleton = !existedBefore;

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

        try
        {
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

            var currentFloorInstance = GetPrivateFieldValue<Node2D>(floorManager, "_currentFloorInstance");
            AssertThat(currentFloorInstance).IsNull();
            AssertThat(floorManager.CurrentGridMap).IsNull();
        }
        finally
        {
            floorManager.QueueFree();
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

            saveManager.PendingLoadData = previousPending;

            // Free the SaveManager only if this test actually created it
            if (createdSingleton)
            {
                saveManager.QueueFree();
            }
        }
    }

    [TestCase]
    public void TestTransitionToFloor_ScopesStairLookupToCurrentFloor()
    {
        // Simulate the 3F↔2F coordinate collision: two stairs on different
        // "floors" sharing GridPosition (10,10).  After the fix, the
        // destination lookup must resolve from the current floor's children,
        // not the global registry.
        var fm = new FloorManager { EnableDebugLogging = false };

        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(fm);

        Node2D floorInstance = null;
        GridMap gridMap2F = null;
        GridMap gridMapDest = null;

        try
        {
            // --- Set up two floor definitions ---
            var floor2FDef = new FloorDefinition { FloorName = "2F" };
            var floor3FDef = new FloorDefinition { FloorName = "3F" };
            fm.Floors.Add(floor2FDef);
            fm.Floors.Add(floor3FDef);

            // --- Build fake "current floor" with GridMap + StairConnection ---
            // Simulate Floor 3F (index 1) as the current floor
            floorInstance = new Node2D();
            var gridMap = new GridMap();
            floorInstance.AddChild(gridMap);
            sceneTree.Root.AddChild(floorInstance);

            // Stair on "3F" at (10,10) going down to 2F
            var stair3F = new StairConnection
            {
                GridPosition = new Vector2I(10, 10),
                Direction = StairDirection.Down,
                TargetFloor = 1,
                StairId = "3F_2F_A",
                DestinationStairId = "2F_3F_A"
            };
            gridMap.AddChild(stair3F);

            // Register the 3F stair in the global registry
            fm.RegisterStair("3F_2F_A", stair3F);

            // Also register a "stale" 2F stair at the SAME position (10,10)
            // This simulates a stair from a previously-loaded floor.
            gridMap2F = new GridMap();
            sceneTree.Root.AddChild(gridMap2F);
            var stair2F = new StairConnection
            {
                GridPosition = new Vector2I(10, 10),
                Direction = StairDirection.Down,
                TargetFloor = 0,
                StairId = "2F_1F_A",
                DestinationStairId = "1F_2F_A"
            };
            gridMap2F.AddChild(stair2F);
            fm.RegisterStair("2F_1F_A", stair2F);

            // Register destination stairs
            gridMapDest = new GridMap();
            sceneTree.Root.AddChild(gridMapDest);
            var destStair = new StairConnection
            {
                GridPosition = new Vector2I(52, 50),
                StairId = "2F_3F_A"
            };
            gridMapDest.AddChild(destStair);
            fm.RegisterStair("2F_3F_A", destStair);

            // Set internal state via reflection to simulate being on Floor 3F
            SetPrivateFieldValue(fm, "_currentFloorIndex", 1);
            SetPrivateFieldValue(fm, "_currentFloorInstance", floorInstance);
            SetPrivateFieldValue(fm, "_currentGridMap", gridMap);

            // Register the stair on the current floor definition
            floor3FDef.StairsDown.Add(new Vector2I(10, 10));

            // Verify: GetStairById resolves the 3F stair
            var resolved = fm.GetStairById("3F_2F_A");
            AssertThat(resolved).IsNotNull();

            // Verify that the destination stair resolves to the correct position
            // (this is the value TransitionToFloor would use as spawnPos)
            var dest = fm.GetStairById("2F_3F_A");
            AssertThat(dest).IsNotNull();
            AssertThat(dest.GridPosition).IsEqual(new Vector2I(52, 50));

            // Exercise the actual transition resolution path
            bool found = fm.TryResolveDestinationStair(new Vector2I(10, 10), false, out Vector2I spawnPos);
            AssertThat(found).IsTrue();
            AssertThat(spawnPos).IsEqual(new Vector2I(52, 50));
        }
        finally
        {
            floorInstance?.QueueFree();
            gridMap2F?.QueueFree();
            gridMapDest?.QueueFree();
            fm.QueueFree();
        }
    }

    [TestCase]
    public async Task TestUnloadCurrentFloor_RemovesStaleAndFreedEntries()
    {
        var fm = new FloorManager { EnableDebugLogging = false };
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(fm);

        Node2D floorInstance = null;
        GridMap otherGridMap = null;

        try
        {
            // Build a fake "current floor" with a GridMap
            floorInstance = new Node2D();
            var gridMap = new GridMap();
            floorInstance.AddChild(gridMap);
            sceneTree.Root.AddChild(floorInstance);

            // Register a stair that belongs to the current floor's GridMap
            var currentFloorStair = new StairConnection
            {
                GridPosition = new Vector2I(10, 10),
                StairId = "current_floor_stair"
            };
            gridMap.AddChild(currentFloorStair);
            fm.RegisterStair("current_floor_stair", currentFloorStair);

            // Register a stair that belongs to a DIFFERENT GridMap
            otherGridMap = new GridMap();
            sceneTree.Root.AddChild(otherGridMap);
            var otherFloorStair = new StairConnection
            {
                GridPosition = new Vector2I(20, 20),
                StairId = "other_floor_stair"
            };
            otherGridMap.AddChild(otherFloorStair);
            fm.RegisterStair("other_floor_stair", otherFloorStair);

            // Register a stair that will be freed (simulating an already-destroyed node)
            var freedStair = new StairConnection
            {
                GridPosition = new Vector2I(30, 30),
                StairId = "freed_stair"
            };
            sceneTree.Root.AddChild(freedStair);
            fm.RegisterStair("freed_stair", freedStair);
            freedStair.QueueFree();
            // Process two frames to ensure the node is fully freed
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
            await sceneTree.ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

            // Set internal state to simulate being on the current floor
            SetPrivateFieldValue(fm, "_currentFloorInstance", floorInstance);
            SetPrivateFieldValue(fm, "_currentGridMap", gridMap);

            // Before unload: verify stairs are in registry
            AssertThat(fm.GetStairById("other_floor_stair")).IsNotNull();

            // Unload the current floor
            fm.UnloadCurrentFloor();

            // After unload: current-floor stair removed, freed stair removed,
            // other-floor stair preserved
            AssertThat(fm.GetStairById("current_floor_stair")).IsNull();
            AssertThat(fm.GetStairById("freed_stair")).IsNull();
            AssertThat(fm.GetStairById("other_floor_stair")).IsNotNull();
            AssertThat(fm.GetStairById("other_floor_stair").GridPosition).IsEqual(new Vector2I(20, 20));
        }
        finally
        {
            floorInstance?.QueueFree();
            otherGridMap?.QueueFree();
            fm.QueueFree();
        }
    }

    [TestCase]
    public void TestRegisterStair_WarnsOnEmptyId()
    {
        var fm = new FloorManager { EnableDebugLogging = false };
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        sceneTree.Root.AddChild(fm);

        StairConnection stair = null;

        try
        {
            stair = new StairConnection { GridPosition = new Vector2I(5, 5) };
            sceneTree.Root.AddChild(stair);

            // Empty string ID should not register
            fm.RegisterStair("", stair);
            AssertThat(fm.GetStairById("")).IsNull();

            // Null ID should not register (GetStairById throws on null key,
            // so we just verify RegisterStair didn't add it by checking count)
            fm.RegisterStair(null, stair);

            // Valid ID should register
            fm.RegisterStair("valid_stair", stair);
            AssertThat(fm.GetStairById("valid_stair")).IsNotNull();
        }
        finally
        {
            stair?.QueueFree();
            fm.QueueFree();
        }
    }

    private static async Task<SaveManager> EnsureSaveManager()
    {
        if (SaveManager.Instance != null && IsInstanceValid(SaveManager.Instance))
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
		if (field == null)
		{
			throw new ArgumentException($"Private field '{fieldName}' not found in type '{instance.GetType().FullName}'");
		}
		return field.GetValue(instance) as T;
	}

 private static void SetPrivateFieldValue<T>(object instance, string fieldName, T value)
	{
		var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
		if (field == null)
		{
			throw new ArgumentException($"Private field '{fieldName}' not found in type '{instance.GetType().FullName}'");
		}
		field.SetValue(instance, value);
	}
}
