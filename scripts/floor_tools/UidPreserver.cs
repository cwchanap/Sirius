using Godot;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Sirius.FloorTools;

/// <summary>
/// Preserves uid= attributes across ResourceSaver.Save / PackedScene.Pack writes.
/// Godot's headless save path strips file-level UIDs (the scene/resource header uid
/// and ext_resource uid= attributes) when re-saving a freshly packed scene or a
/// resource loaded from disk. Game.tscn references Floor*.tres by UID, so stripping
/// breaks reference stability and causes editor rewrites on next load.
///
/// This mirrors the extract_uid_map / restore_uids post-processing in
/// tools/tilemap_json_sync.py, extended to also cover .tres resource headers.
/// </summary>
public static class UidPreserver
{
    private static readonly Regex HeaderUidRegex = new(@"uid=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex LoadStepsRegex = new(@"load_steps=(\d+)", RegexOptions.Compiled);
    private static readonly Regex ExtResourceRegex = new(@"^\[ext_resource\s+", RegexOptions.Compiled);
    private static readonly Regex PathRegex = new(@"path=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex ExtResourceUidRegex = new(@"uid=""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex TypeAttrRegex = new(@"(type=""[^""]*"")", RegexOptions.Compiled);

    /// <summary>Snapshot of UID metadata from a .tscn or .tres file.</summary>
    public record Snapshot
    {
        public string? HeaderUid { get; init; }
        public int? HeaderLoadSteps { get; init; }
        public Dictionary<string, string> PathToUid { get; init; } = new();
    }

    /// <summary>
    /// Read UID metadata from the file at <paramref name="resPath"/> (a res:// path).
    /// Returns an empty snapshot if the file does not exist or has no UIDs.
    /// </summary>
    public static Snapshot Capture(string resPath)
    {
        var snap = new Snapshot();
        string absPath = ProjectSettings.GlobalizePath(resPath);
        if (!File.Exists(absPath)) return snap;

        foreach (var line in File.ReadAllLines(absPath))
        {
            if (line.StartsWith("[gd_scene") || line.StartsWith("[gd_resource"))
            {
                var m = HeaderUidRegex.Match(line);
                if (m.Success)
                    snap = snap with { HeaderUid = m.Groups[1].Value };
                var ls = LoadStepsRegex.Match(line);
                if (ls.Success && int.TryParse(ls.Groups[1].Value, out var steps))
                    snap = snap with { HeaderLoadSteps = steps };
                continue;
            }

            if (!ExtResourceRegex.IsMatch(line)) continue;
            var pathMatch = PathRegex.Match(line);
            var uidMatch = ExtResourceUidRegex.Match(line);
            if (pathMatch.Success && uidMatch.Success)
                snap.PathToUid[pathMatch.Groups[1].Value] = uidMatch.Groups[1].Value;
        }
        return snap;
    }

    /// <summary>
    /// Re-inject UIDs that ResourceSaver stripped into the file at <paramref name="resPath"/>.
    /// Restores the header uid=, header load_steps=, and ext_resource uid= attributes.
    /// No-op if the snapshot is empty or the target file does not exist.
    /// </summary>
    public static void Restore(string resPath, Snapshot snap)
    {
        if (snap is null || (snap.HeaderUid is null && snap.HeaderLoadSteps is null && snap.PathToUid.Count == 0)) return;
        string absPath = ProjectSettings.GlobalizePath(resPath);
        if (!File.Exists(absPath)) return;

        var lines = File.ReadAllLines(absPath);
        int restored = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("[gd_scene") || line.StartsWith("[gd_resource"))
            {
                // Re-inject load_steps= before format= if stripped.
                if (snap.HeaderLoadSteps is not null && !line.Contains("load_steps="))
                {
                    lines[i] = line.Replace(" format=", $" load_steps={snap.HeaderLoadSteps} format=");
                    restored++;
                    line = lines[i];
                }

                // Inject uid= before the closing bracket if stripped.
                if (snap.HeaderUid is not null && !line.Contains("uid="))
                {
                    lines[i] = line.TrimEnd(']') + $" uid=\"{snap.HeaderUid}\"]";
                    restored++;
                }
                continue;
            }

            if (ExtResourceRegex.IsMatch(line) && !line.Contains("uid="))
            {
                var pathMatch = PathRegex.Match(line);
                if (pathMatch.Success && snap.PathToUid.TryGetValue(pathMatch.Groups[1].Value, out var uid))
                {
                    lines[i] = TypeAttrRegex.Replace(line, $"$1 uid=\"{uid}\"", 1);
                    restored++;
                }
            }
        }

        if (restored > 0)
        {
            // Atomic write: temp file → rename, so a crash mid-write cannot
            // leave a half-written .tscn/.tres that corrupts the project.
            string tempPath = absPath + ".uidtmp";
            File.WriteAllLines(tempPath, lines);
            File.Move(tempPath, absPath, overwrite: true);
        }
    }
}
