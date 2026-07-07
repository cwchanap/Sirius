using Godot;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sirius.FloorTools;

// partial is required by the Godot C# source generator for any class deriving
// from GodotObject (RefCounted derives from it); the generator synthesizes the
// registration glue in the generated partial.
public partial class FloorCli : RefCounted
{
    private const string Usage =
        "Usage: --floor <0|1|2|3> [--json-only] [--skip-floor-def] [--stair-dest x,y]";

    /// <summary>Parsed CLI arguments.</summary>
    public record FloorCliArgs(int Floor, bool JsonOnly, bool SkipFloorDef, Vector2I? StairDest);

    /// <summary>Result of arg parsing: either valid args, a help request, or an error.</summary>
    public record FloorCliParseResult(FloorCliArgs? Args, string? Error, bool IsHelp);

    /// <summary>
    /// Pure arg parser — no Godot I/O, unit-testable. Parses the --floor/--json-only/
    /// --skip-floor-def/--stair-dest flags. Returns Args on success, IsHelp on -h/--help,
    /// or Error with a message on invalid input.
    /// </summary>
    public static FloorCliParseResult ParseArgs(string[] args)
    {
        int floor = -1;
        bool jsonOnly = false;
        bool skipFloorDef = false;
        Vector2I? stairDest = null;

        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--help":
                    case "-h":
                        return new FloorCliParseResult(null, null, IsHelp: true);
                    case "--floor":
                        floor = int.Parse(args[++i]);
                        break;
                    case "--json-only":
                        jsonOnly = true;
                        break;
                    case "--skip-floor-def":
                        skipFloorDef = true;
                        break;
                    case "--stair-dest":
                        {
                            var raw = args[++i];
                            var m = Regex.Match(raw, @"^(\d+),\s*(\d+)$");
                            if (!m.Success)
                                throw new FormatException($"--stair-dest expects 'x,y', got '{raw}'");
                            stairDest = new Vector2I(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                            break;
                        }
                    default:
                        throw new FormatException($"Unknown flag: '{args[i]}'");
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException)
        {
            return new FloorCliParseResult(null, ex.Message, IsHelp: false);
        }

        if (!FloorRegistry.AllFloors.Contains(floor))
            return new FloorCliParseResult(null, $"Floor must be one of: {string.Join(", ", FloorRegistry.AllFloors)}", IsHelp: false);

        return new FloorCliParseResult(new FloorCliArgs(floor, jsonOnly, skipFloorDef, stairDest), null, IsHelp: false);
    }

    public int Run()
    {
        var parsed = ParseArgs(OS.GetCmdlineUserArgs());

        if (parsed.IsHelp)
        {
            GD.Print(Usage);
            return 0;
        }
        if (parsed.Args is null)
        {
            GD.PrintErr($"Invalid arguments: {parsed.Error}");
            GD.PrintErr(Usage);
            return 1;
        }

        var (floor, jsonOnly, skipFloorDef, stairDest) = parsed.Args;

        if (jsonOnly)
        {
            var model = FloorGenerationService.Generate(floor);
            var paths = FloorRegistry.Get(floor);
            using var file = FileAccess.Open(paths.JsonPath, FileAccess.ModeFlags.Write);
            file.StoreString(model.ToJson(indented: true));
            GD.Print($"Wrote {paths.JsonPath}");
            return 0;
        }

        var result = FloorSceneWriter.Generate(floor, new FloorSyncOptions(stairDest), writeJson: true, syncDef: !skipFloorDef);
        GD.Print(result.Summary);
        foreach (var issue in result.Validation.Issues)
            GD.PrintErr($"  [{issue.Severity}] {issue.Code}: {issue.Message}");
        return result.Success ? 0 : 1;
    }
}
