using Godot;
using System.Text.RegularExpressions;

namespace Sirius.FloorTools;

public partial class FloorCli : RefCounted
{
    public int Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int floor = -1;
        bool jsonOnly = false;
        bool skipFloorDef = false;
        Vector2I? stairDest = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--floor": floor = int.Parse(args[++i]); break;
                case "--json-only": jsonOnly = true; break;
                case "--skip-floor-def": skipFloorDef = true; break;
                case "--stair-dest":
                    var m = Regex.Match(args[++i], @"^(\d+),\s*(\d+)$");
                    stairDest = new Vector2I(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
                    break;
            }
        }

        if (floor < 0)
        {
            GD.PrintErr("Usage: --floor <0|1|2|3> [--json-only] [--skip-floor-def] [--stair-dest x,y]");
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
