using GdUnit4;
using Godot;
using Sirius.FloorTools;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorCliTest
{
    [TestCase]
    public void TestParseFloorOnly()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "2" });
        AssertThat(result.IsHelp).IsFalse();
        AssertThat(result.Error).IsNull();
        AssertThat(result.Args).IsNotNull();
        AssertThat(result.Args!.Floor).IsEqual(2);
        AssertThat(result.Args.JsonOnly).IsFalse();
        AssertThat(result.Args.SkipFloorDef).IsFalse();
        AssertThat(result.Args.StairDest).IsNull();
    }

    [TestCase]
    public void TestParseAllFlags()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "1", "--json-only", "--skip-floor-def", "--stair-dest", "10,20" });
        AssertThat(result.Args).IsNotNull();
        AssertThat(result.Args!.Floor).IsEqual(1);
        AssertThat(result.Args.JsonOnly).IsTrue();
        AssertThat(result.Args.SkipFloorDef).IsTrue();
        AssertThat(result.Args.StairDest).IsEqual(new Vector2I(10, 20));
    }

    [TestCase]
    public void TestParseStairDestWithSpace()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "0", "--stair-dest", "5, 15" });
        AssertThat(result.Args).IsNotNull();
        AssertThat(result.Args!.StairDest).IsEqual(new Vector2I(5, 15));
    }

    [TestCase]
    public void TestHelpLongFlag()
    {
        var result = FloorCli.ParseArgs(new[] { "--help" });
        AssertThat(result.IsHelp).IsTrue();
        AssertThat(result.Args).IsNull();
    }

    [TestCase]
    public void TestHelpShortFlag()
    {
        var result = FloorCli.ParseArgs(new[] { "-h" });
        AssertThat(result.IsHelp).IsTrue();
    }

    [TestCase]
    public void TestUnknownFlagRejected()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "1", "--bogus" });
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
        AssertThat(result.Error!.Contains("Unknown flag")).IsTrue();
    }

    [TestCase]
    public void TestMissingFloorValue()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor" });
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
    }

    [TestCase]
    public void TestInvalidStairDestFormat()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "0", "--stair-dest", "abc" });
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
        AssertThat(result.Error!.Contains("x,y")).IsTrue();
    }

    [TestCase]
    public void TestFloorOutOfRange()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "9" });
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
    }

    [TestCase]
    public void TestNegativeFloor()
    {
        var result = FloorCli.ParseArgs(new[] { "--floor", "-1" });
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
    }

    [TestCase]
    public void TestNoArgsIsError()
    {
        var result = FloorCli.ParseArgs(System.Array.Empty<string>());
        AssertThat(result.Args).IsNull();
        AssertThat(result.Error).IsNotNull();
    }
}
