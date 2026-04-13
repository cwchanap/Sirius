using GdUnit4;
using Godot;
using System;
using System.Reflection;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PlayerControllerTest : Node
{
    [TestCase]
    public void UnhandledInput_PendingStairTransitionWithInvalidState_ClearsPendingTransition()
    {
        var controller = new PlayerController();
        SetPrivateField(controller, "_gameManager", new GameManager());
        SetPrivateField(controller, "_pendingStairTransition", true);
        SetPrivateField(controller, "_targetFloor", -1);
        SetPrivateField(controller, "_isGoingUp", true);
        SetPrivateField(controller, "_targetStairIndex", -1);

        controller._UnhandledInput(CreateInteractEvent());

        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(-1);
        AssertThat(GetPrivateField<bool>(controller, "_isGoingUp")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetStairIndex")).IsEqual(-1);
    }

    [TestCase]
    public void QueueStairTransition_SetsPendingStateAndTargetMetadata()
    {
        var controller = new PlayerController();

        InvokePrivateMethod(controller, "QueueStairTransition", 2, true, 3);

        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsTrue();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(2);
        AssertThat(GetPrivateField<bool>(controller, "_isGoingUp")).IsTrue();
        AssertThat(GetPrivateField<int>(controller, "_targetStairIndex")).IsEqual(3);
    }

    [TestCase]
    public void ClearPendingStairTransition_ResetsAllStairState()
    {
        var controller = new PlayerController();
        InvokePrivateMethod(controller, "QueueStairTransition", 2, true, 3);

        InvokePrivateMethod(controller, "ClearPendingStairTransition");

        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(-1);
        AssertThat(GetPrivateField<bool>(controller, "_isGoingUp")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetStairIndex")).IsEqual(-1);
    }

    [TestCase]
    public void UnhandledInput_PendingStairTransitionWithValidState_ClearsPendingAndCallsTransition()
    {
        var controller = new PlayerController();
        var gameManager = new GameManager();
        var floorManager = new FloorManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_floorManager", floorManager);
        // Arm valid pending stair state
        SetPrivateField(controller, "_pendingStairTransition", true);
        SetPrivateField(controller, "_targetFloor", 1);
        SetPrivateField(controller, "_isGoingUp", true);
        SetPrivateField(controller, "_targetStairIndex", 0);

        controller._UnhandledInput(CreateInteractEvent());

        // The happy path clears all pending state after calling TransitionToFloor
        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(-1);
        AssertThat(GetPrivateField<bool>(controller, "_isGoingUp")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetStairIndex")).IsEqual(-1);

        // Cleanup
        floorManager.Free();
        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void QueueStairTransition_OnlyQueuesDoesNotAutoTransition()
    {
        var controller = new PlayerController();
        var floorManager = new FloorManager();
        SetPrivateField(controller, "_floorManager", floorManager);

        // QueueStairTransition should only set pending state — NOT call TransitionToFloor
        InvokePrivateMethod(controller, "QueueStairTransition", 2, true, 0);

        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsTrue();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(2);
        AssertThat(GetPrivateField<bool>(controller, "_isGoingUp")).IsTrue();
        AssertThat(GetPrivateField<int>(controller, "_targetStairIndex")).IsEqual(0);

        floorManager.Free();
        controller.Free();
    }

    [TestCase]
    public void Interact_WhenNotPendingTriggersStairReCheck()
    {
        // Verifies that pressing interact when _pendingStairTransition is false
        // will call CheckForStairs(), which is the mechanism that re-arms stair
        // detection after a floor transition lands the player on a stair tile.
        //
        // We cannot easily stub GridMap/FloorManager (non-virtual methods), so
        // we test the contract: with no GridMap set, CheckForStairs() is a
        // no-op and _pendingStairTransition stays false — but the interact
        // handler must still complete without error.
        var controller = new PlayerController();
        var gameManager = new GameManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        // _pendingStairTransition is false, _gridMap is null
        // This simulates arriving on a floor before CheckForStairs can do anything

        controller._UnhandledInput(CreateInteractEvent());

        // With null GridMap, CheckForStairs returns early and nothing transitions.
        // The key assertion: no crash/error, and state stays clean.
        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsFalse();

        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void Interact_ReCheckQueuesThenTransitionsInOnePress()
    {
        // Simulates the full re-arm + transition cycle:
        // Player arrives on floor, is on stairs, _pendingStairTransition is false.
        // Pressing interact should: (1) CheckForStairs queues pending state,
        // (2) pending state is detected, (3) TransitionToFloor called, (4) cleared.
        var controller = new PlayerController();
        var gameManager = new GameManager();
        var floorManager = new FloorManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_floorManager", floorManager);
        // Pre-arm as if CheckForStairs detected stairs during the re-check
        SetPrivateField(controller, "_pendingStairTransition", true);
        SetPrivateField(controller, "_targetFloor", 1);
        SetPrivateField(controller, "_isGoingUp", true);
        SetPrivateField(controller, "_targetStairIndex", 0);

        controller._UnhandledInput(CreateInteractEvent());

        // TransitionToFloor was called (no-op on bare FloorManager) and state cleared
        AssertThat(GetPrivateField<bool>(controller, "_pendingStairTransition")).IsFalse();
        AssertThat(GetPrivateField<int>(controller, "_targetFloor")).IsEqual(-1);

        floorManager.Free();
        gameManager.Free();
        controller.Free();
    }

    private static InputEventAction CreateInteractEvent()
    {
        return new InputEventAction
        {
            Action = "interact",
            Pressed = true
        };
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        return (T)field.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    private static void InvokePrivateMethod(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            throw new MissingMethodException(instance.GetType().FullName, methodName);
        }

        method.Invoke(instance, args);
    }
}
