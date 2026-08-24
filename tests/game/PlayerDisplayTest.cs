using GdUnit4;
using Godot;
using System;
using System.Reflection;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PlayerDisplayTest : Node
{
    [TestCase]
    public void Process_ReducedMotionKeepsFrameZero()
    {
        var grid = new GridMap { ReducedMotionEnabled = true };
        var display = new PlayerDisplay
        {
            Texture = CreateFourFrameTexture(),
            RegionEnabled = true,
            RegionRect = new Rect2(0, 0, 96, 96)
        };
        grid.AddChild(display);
        SetPrivateField(display, "_gridMap", grid);
        try
        {
            display._Process(0.2);

            AssertThat(display.RegionRect.Position.X).IsEqual(0f);
        }
        finally
        {
            display.Free();
            grid.Free();
        }
    }

    [TestCase]
    public void Process_DefaultMotionAdvancesOneFrame()
    {
        var grid = new GridMap { ReducedMotionEnabled = false };
        var display = new PlayerDisplay
        {
            Texture = CreateFourFrameTexture(),
            RegionEnabled = true,
            RegionRect = new Rect2(0, 0, 96, 96)
        };
        grid.AddChild(display);
        SetPrivateField(display, "_gridMap", grid);
        try
        {
            display._Process(0.2);

            AssertThat(display.RegionRect.Position.X).IsEqual(96f);
        }
        finally
        {
            display.Free();
            grid.Free();
        }
    }

    private static Texture2D CreateFourFrameTexture()
    {
        var image = Image.CreateEmpty(384, 96, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        return ImageTexture.CreateFromImage(image);
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
}
