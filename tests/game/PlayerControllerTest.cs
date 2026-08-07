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
    public void FacingDirection_DefaultsDownAndUpdatesOnMovementInput()
    {
        var controller = new PlayerController();
        var gameManager = new GameManager();
        SetPrivateField(controller, "_gameManager", gameManager);

        AssertThat(controller.FacingDirection).IsEqual(Vector2I.Down);

        controller._UnhandledInput(new InputEventKey { Keycode = Key.Left, Pressed = true });

        AssertThat(controller.FacingDirection).IsEqual(Vector2I.Left);

        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void Interact_RequestsTreasureBoxOpenWhenFacingTreasureBox()
    {
        var controller = new PlayerController();
        var gridMap = new GridMap();
        var grid = new int[gridMap.GridWidth, gridMap.GridHeight];
        grid[0, 1] = (int)GridMap.CellType.TreasureBox;
        var treasureOpenRequests = 0;
        gridMap.TreasureBoxOpenRequested += _ => treasureOpenRequests++;

        SetPrivateField(gridMap, "_grid", grid);
        SetPrivateField(gridMap, "_playerPosition", Vector2I.Zero);
        var gameManager = new GameManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_gridMap", gridMap);
        SetPrivateField(controller, "_lastFacingDirection", Vector2I.Down);

        controller._UnhandledInput(CreateInteractEvent());

        AssertThat(treasureOpenRequests).IsEqual(1);

        gridMap.Free();
        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void PresentationBlock_PreventsMovementAndFacingUpdate()
    {
        var controller = new PlayerController();
        var gridMap = CreateWalkableGridMap(new Vector2I(4, 4));
        var gameManager = new GameManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_gridMap", gridMap);
        controller.GameplayInputSuppressedProvider = () => true;

        controller._UnhandledInput(new InputEventKey { Keycode = Key.Right, Pressed = true });

        AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(4, 4));
        AssertThat(controller.FacingDirection).IsEqual(Vector2I.Down);

        gridMap.Free();
        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void PresentationBlock_PreventsTreasureInteraction()
    {
        var controller = new PlayerController();
        var gridMap = CreateWalkableGridMap(Vector2I.Zero);
        var grid = new int[gridMap.GridWidth, gridMap.GridHeight];
        grid[0, 1] = (int)GridMap.CellType.TreasureBox;
        var treasureOpenRequests = 0;
        gridMap.TreasureBoxOpenRequested += _ => treasureOpenRequests++;

        SetPrivateField(gridMap, "_grid", grid);
        SetPrivateField(controller, "_gridMap", gridMap);
        SetPrivateField(controller, "_lastFacingDirection", Vector2I.Down);
        var gameManager = new GameManager();
        SetPrivateField(controller, "_gameManager", gameManager);
        controller.GameplayInputSuppressedProvider = () => true;

        controller._UnhandledInput(CreateInteractEvent());

        AssertThat(treasureOpenRequests).IsEqual(0);

        gridMap.Free();
        gameManager.Free();
        controller.Free();
    }

    [TestCase]
    public void PresentationBlockFalse_PreservesWorldInteractionMovementGuard()
    {
        var controller = new PlayerController();
        var gridMap = CreateWalkableGridMap(new Vector2I(4, 4));
        var gameManager = new GameManager();
        gameManager.StartWorldInteraction();
        SetPrivateField(controller, "_gameManager", gameManager);
        SetPrivateField(controller, "_gridMap", gridMap);
        controller.GameplayInputSuppressedProvider = () => false;

        controller._UnhandledInput(new InputEventKey { Keycode = Key.Right, Pressed = true });

        AssertThat(gridMap.GetPlayerPosition()).IsEqual(new Vector2I(4, 4));
        AssertThat(controller.FacingDirection).IsEqual(Vector2I.Down);

        gameManager.EndWorldInteraction();
        gridMap.Free();
        gameManager.Free();
        controller.Free();
    }

    private static GridMap CreateWalkableGridMap(Vector2I playerPosition)
    {
        var gridMap = new GridMap();
        var grid = new int[gridMap.GridWidth, gridMap.GridHeight];
        grid[playerPosition.X, playerPosition.Y] = (int)GridMap.CellType.Player;
        SetPrivateField(gridMap, "_grid", grid);
        SetPrivateField(gridMap, "_playerPosition", playerPosition);
        return gridMap;
    }

    private static InputEventAction CreateInteractEvent()
    {
        return new InputEventAction
        {
            Action = "interact",
            Pressed = true
        };
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
}
