using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
public partial class MazeBuilderTest
{
    [TestCase]
    public void TestStartsFullOfWalls()
    {
        var builder = new MazeBuilder(10, 10);
        // Footprint interior cells are walls until carved (border always wall).
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsTrue();
        AssertThat(builder.Walls.Count).IsEqual(100);
    }

    [TestCase]
    public void TestCarveCellRemovesFromWalls()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveCell(5, 5);
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsFalse();
    }

    [TestCase]
    public void TestCarveRect()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveRect(3, 3, 5, 5);
        AssertThat(builder.Walls.Contains(new Vector2I(4, 4))).IsFalse();
        AssertThat(builder.Walls.Contains(new Vector2I(2, 4))).IsTrue(); // outside rect
    }

    [TestCase]
    public void TestReinforcePerimeter()
    {
        var builder = new MazeBuilder(10, 10);
        builder.CarveRect(0, 0, 9, 9); // carve everything including border
        builder.ReinforcePerimeter();
        AssertThat(builder.Walls.Contains(new Vector2I(0, 0))).IsTrue();
        AssertThat(builder.Walls.Contains(new Vector2I(9, 9))).IsTrue();
        AssertThat(builder.Walls.Contains(new Vector2I(5, 5))).IsFalse(); // interior stays carved
    }
}
