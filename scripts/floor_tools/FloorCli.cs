using Godot;
using System;
using System.Text.RegularExpressions;

namespace Sirius.FloorTools;

// partial is required by the Godot C# source generator for any class deriving
// from GodotObject (RefCounted derives from it); the generator synthesizes the
// registration glue in the generated partial.
public partial class FloorCli : RefCounted
{
    private const string Usage =
        "Usage: --floor <0|1|2|3> [--json-only] [--skip-floor-def] [--stair-dest x,y]";

    public int Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
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
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or FormatException)
        {
            GD.PrintErr($"Invalid arguments: {ex.Message}");
            GD.PrintErr(Usage);
            return 1;
        }

        if (floor < 0)
        {
            GD.PrintErr(Usage);
            return 1;
        }

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
