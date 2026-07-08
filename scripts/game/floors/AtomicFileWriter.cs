using Godot;
using System.IO;

namespace Sirius.FloorTools;

/// <summary>
/// Atomic text-file writer for res:// paths. Writes to a sibling .tmp file
/// then <see cref="File.Move(string, string, bool)"/> with overwrite, so a
/// crash mid-write cannot leave a half-written/truncated committed file.
/// Mirrors the temp→rename pattern in <c>UidPreserver.Restore</c> and <c>SaveManager</c>.
/// </summary>
/// <remarks>
/// Unlike <c>SaveManager</c>, this writer does NOT keep a <c>.bak</c> backup.
/// <c>SaveManager</c> needs <c>.bak</c> because user save files are not in git
/// and require crash recovery. Floor artifacts (.json) written here are
/// git-tracked, so recovery from a rare mid-write crash is <c>git checkout</c>;
/// emitting <c>.bak</c> files would create untracked working-tree noise.
/// </remarks>
public static class AtomicFileWriter
{
    /// <summary>Write <paramref name="content"/> to the file at <paramref name="resPath"/>
    /// atomically. <paramref name="resPath"/> is a res:// path; it is globalized
    /// to an absolute path for the underlying <see cref="System.IO.File"/> calls.</summary>
    public static void WriteAllText(string resPath, string content)
    {
        string absPath = ProjectSettings.GlobalizePath(resPath);
        string absDir = Path.GetDirectoryName(absPath) ?? string.Empty;
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);

        string tempPath = absPath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, absPath, overwrite: true);
    }
}
