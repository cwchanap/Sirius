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
