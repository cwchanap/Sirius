using GdUnit4;
using Godot;
using System;
using System.Reflection;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class GridMapTest : Node
{
    [TestCase]
    public void ReducedMotion_RuntimeProcessResetsFrameAndStopsCycling()
    {
        var grid = new GridMap { ReducedMotionEnabled = true };
        AddChild(grid);
        try
        {
            SetPrivateField(grid, "_currentFrame", 2);
            SetPrivateField(grid, "_animationTime", 0.19f);

            grid._Process(0.2);

            AssertThat(GetPrivateField<int>(grid, "_currentFrame")).IsEqual(0);
            AssertThat(GetPrivateField<float>(grid, "_animationTime")).IsEqual(0f);
        }
        finally
        {
            grid.Free();
        }
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new MissingFieldException(instance.GetType().FullName, fieldName);
        }

        return (T)field.GetValue(instance)!;
    }
}
