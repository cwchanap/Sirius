using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sirius.FloorTools;

public record FloorSceneResult(bool Success, ValidationResult Validation, string Summary);

public static class FloorSceneWriter
{
    /// <summary>
    /// Generate the floor model and run validation. Shared by Generate (full path)
    /// and FloorCli --json-only so the validation gate stays in sync.
    /// </summary>
    public static (FloorJsonModel Model, ValidationResult Validation) GenerateAndValidate(int floorNumber)
    {
        var model = FloorGenerationService.Generate(floorNumber);
        var (width, height) = DimensionsFor(floorNumber);
        var validation = FloorValidationService.Validate(model, width, height);
        return (model, validation);
    }

    /// <param name="outputDir">If non-null, scene/.tres/.json are written under this
    /// absolute directory (basenames preserved) instead of the canonical registry paths.
    /// The source scene/.tres are still loaded from the registry paths. Used by tests
    /// to avoid dirtying committed artifacts.</param>
    /// <param name="sourcePaths">If non-null, overrides the load paths for the source
    /// scene/.tres (used by tests to point at a temp scene lacking GridMap, exercising
    /// the try/finally failure path). Defaults to FloorRegistry.Get(floorNumber).</param>
    public static FloorSceneResult Generate(int floorNumber, FloorSyncOptions options,
        bool writeJson = true, bool syncDef = true, string? outputDir = null,
        FloorPaths? sourcePaths = null)
    {
        var paths = sourcePaths ?? FloorRegistry.Get(floorNumber);
        var (model, validation) = GenerateAndValidate(floorNumber);
        if (validation.HasErrors)
            return new FloorSceneResult(false, validation, $"Validation failed: {validation.Issues.Count} issue(s)");

        // Resolve output paths (temp seam for tests; canonical paths by default).
        string outScenePath = outputDir is null
            ? paths.ScenePath
            : ToResPath(outputDir, Path.GetFileName(paths.ScenePath));
        string outDefPath = outputDir is null
            ? paths.DefPath
            : ToResPath(outputDir, Path.GetFileName(paths.DefPath));
        string outJsonPath = outputDir is null
            ? paths.JsonPath
            : ToResPath(outputDir, Path.GetFileName(paths.JsonPath));

        if (outputDir is not null)
            Directory.CreateDirectory(outputDir);

        // Capture UID metadata from the source files BEFORE saving (ResourceSaver strips
        // file-level UIDs on re-save; Game.tscn references Floor*.tres by UID).
        var sceneUidSnap = UidPreserver.Capture(paths.ScenePath);
        var defUidSnap = syncDef ? UidPreserver.Capture(paths.DefPath) : null;

        // Write into the scene via the existing importer.
        var packed = GD.Load<PackedScene>(paths.ScenePath);
        if (packed == null)
            return new FloorSceneResult(false, validation, $"Failed to load scene: {paths.ScenePath}");
        var scene = packed.Instantiate();
        if (scene == null)
            return new FloorSceneResult(false, validation, $"Failed to instantiate scene: {paths.ScenePath}");
        try
        {
            var gridMap = scene.GetNodeOrNull<GridMap>("GridMap");
            if (gridMap == null)
                return new FloorSceneResult(false, validation, "Scene missing GridMap node");
            var importer = new TilemapJsonImporter();
            var importErr = importer.ImportToScene(model, gridMap);
            if (importErr != Error.Ok)
            {
                // ImportToScene returns non-OK when the tile config is missing or
                // the generated model contains an unmapped tile. By that point it
                // has already cleared/partially imported layers, so proceeding to
                // pack/save would commit a stale or partially empty scene and sync
                // the .tres/JSON as a success. Abort before any file is written.
                return new FloorSceneResult(false, validation,
                    $"Import failed ({importErr}): scene not committed");
            }

            // Pack the scene BEFORE any save so a pack failure (the most likely
            // failure point) returns early without having committed any file.
            var newPacked = new PackedScene();
            var packErr = newPacked.Pack(scene);
            if (packErr != Error.Ok)
                return new FloorSceneResult(false, validation, $"Failed to pack scene ({packErr}): {outScenePath}");

            // Pre-load the FloorDefinition BEFORE committing the scene so a
            // missing, corrupt, or locked .tres aborts before any file is
            // written. Without this, the scene would already be committed when
            // the .tres load fails, leaving the generated scene and its metadata
            // artifacts out of sync after a reported failed run.
            FloorDefinition def = null;
            if (syncDef)
            {
                def = ResourceLoader.Load<FloorDefinition>(paths.DefPath, cacheMode: ResourceLoader.CacheMode.Ignore);
                if (def == null)
                    return new FloorSceneResult(false, validation, $"Failed to load FloorDefinition: {paths.DefPath}");
            }

            // Stage all outputs to temp files BEFORE committing any. This
            // ensures that if any single save fails (disk-full, permission,
            // lock), no committed artifact is left stale — the caller sees a
            // failure and the on-disk files remain unchanged. Only the final
            // commit phase (File.Move) can partially commit, and moves are
            // near-instant, shrinking the inconsistency window to milliseconds
            // versus the previous approach where the scene committed long
            // before the .tres/JSON saves were even attempted.
            string sceneTempPath = null;
            string defTempPath = null;
            string jsonTempPath = null;
            try
            {
                // 1. Stage scene to temp (UIDs restored on temp before move).
                var (sceneTemp, sceneSaveErr) = SaveResourceToTemp(newPacked, outScenePath, sceneUidSnap);
                if (sceneSaveErr != Error.Ok)
                    return new FloorSceneResult(false, validation, $"Failed to save scene ({sceneSaveErr}): {outScenePath}");
                sceneTempPath = sceneTemp;

                // 2. Stage .tres to temp (skippable for --skip-floor-def parity).
                if (syncDef)
                {
                    FloorResourceSyncService.Apply(def, model, options);
                    var (defTemp, defSaveErr) = SaveResourceToTemp(def, outDefPath, defUidSnap);
                    if (defSaveErr != Error.Ok)
                        return new FloorSceneResult(false, validation, $"Failed to save FloorDefinition ({defSaveErr}): {outDefPath}");
                    defTempPath = defTemp;
                }

                // 3. Stage JSON to temp.
                if (writeJson)
                {
                    jsonTempPath = WriteJsonToTemp(model, outJsonPath);
                }

                // Commit phase: move all staged temps into their final paths.
                // Ordered scene → def → JSON. If a move throws (locked target
                // on Windows), earlier moves may have already committed — but
                // the window is near-instant compared to the previous design
                // where saves (slow) happened between commits.
                if (sceneTempPath != null)
                    CommitResourceTemp(sceneTempPath, outScenePath);
                if (defTempPath != null)
                    CommitResourceTemp(defTempPath, outDefPath);
                if (jsonTempPath != null)
                    CommitJsonTemp(jsonTempPath, outJsonPath);
            }
            catch (System.Exception ex)
            {
                // A move threw during the commit phase. Clean up any uncommitted
                // temps so they don't linger as working-tree noise.
                CleanupTemp(sceneTempPath);
                CleanupTemp(defTempPath);
                CleanupTemp(jsonTempPath);
                return new FloorSceneResult(false, validation, $"Failed to commit floor artifacts: {ex.Message}");
            }
        }
        finally
        {
            // Ensures the instantiated scene is freed even on exception or early
            // return from inside the try block (null-guard failure paths). Free()
            // (not QueueFree) because the scene is never added to the scene tree,
            // so deferred deletion would never process in headless CLI runs.
            scene.Free();
        }

        int walls = model.TileLayers.GetValueOrDefault("wall")?.Count ?? 0;
        int enemies = model.Entities.EnemySpawns?.Count ?? 0;
        int treasures = model.Entities.TreasureBoxes?.Count ?? 0;
        return new FloorSceneResult(true, validation,
            $"Floor {floorNumber}: {walls} walls, {enemies} enemies, {treasures} treasures generated");
    }

    private static string ToResPath(string outputDir, string basename)
    {
        string abs = Path.Combine(outputDir, basename);
        return ProjectSettings.LocalizePath(abs);
    }

    /// <summary>
    /// Save <paramref name="res"/> to <paramref name="resPath"/> atomically:
    /// ResourceSaver writes to a sibling temp path (extension preserved so Godot
    /// picks the correct saver), UIDs are restored on the temp file, then
    /// <see cref="File.Move"/> with overwrite swaps it into place. A crash at any
    /// point cannot leave a half-written or UID-stripped committed file.
    /// Each save is independently atomic; the caller (Generate) orders saves so
    /// the most likely failure (scene pack) happens before any file is committed.
    /// </summary>
    internal static Error SaveResourceAtomic(Resource res, string resPath, UidPreserver.Snapshot? uidSnap = null)
    {
        var (tempResPath, saveErr) = SaveResourceToTemp(res, resPath, uidSnap);
        if (saveErr != Error.Ok)
            return saveErr;
        try
        {
            CommitResourceTemp(tempResPath, resPath);
        }
        catch
        {
            return Error.CantCreate;
        }
        return Error.Ok;
    }

    /// <summary>
    /// Phase 1 of the two-phase atomic save: write the resource to a sibling
    /// temp path and restore UIDs on the temp file. Returns the temp res://
    /// path on success, or null + an error code on failure. The caller commits
    /// via <see cref="CommitResourceTemp"/> once all outputs are staged.
    /// </summary>
    internal static (string TempResPath, Error Err) SaveResourceToTemp(
        Resource res, string resPath, UidPreserver.Snapshot? uidSnap = null)
    {
        string tempResPath = TempPathFor(resPath);
        var err = ResourceSaver.Save(res, tempResPath);
        if (err != Error.Ok)
        {
            string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
            if (File.Exists(tempAbs)) File.Delete(tempAbs);
            return (tempResPath, err);
        }
        try
        {
            // Restore UIDs on the temp file BEFORE the atomic move so the
            // committed file never appears with stripped UIDs. This closes
            // the non-atomic window that would exist if Restore ran as a
            // separate post-move step.
            if (uidSnap is not null)
                UidPreserver.Restore(tempResPath, uidSnap);
        }
        catch
        {
            // Restore failure: clean up the temp so it doesn't linger.
            string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
            if (File.Exists(tempAbs)) File.Delete(tempAbs);
            string uidTmp = tempAbs + ".uidtmp";
            if (File.Exists(uidTmp)) File.Delete(uidTmp);
            return (tempResPath, Error.CantCreate);
        }
        return (tempResPath, Error.Ok);
    }

    /// <summary>
    /// Phase 2 of the two-phase atomic save: move the staged temp file into
    /// its final path with overwrite. <see cref="File.Move"/> is atomic on most
    /// filesystems for same-volume renames. On Windows, a locked target can
    /// throw; the orphaned temp is cleaned up before rethrowing.
    /// </summary>
    internal static void CommitResourceTemp(string tempResPath, string finalResPath)
    {
        string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
        string realAbs = ProjectSettings.GlobalizePath(finalResPath);
        try
        {
            File.Move(tempAbs, realAbs, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempAbs)) File.Delete(tempAbs);
            string uidTmp = tempAbs + ".uidtmp";
            if (File.Exists(uidTmp)) File.Delete(uidTmp);
            throw;
        }
    }

    /// <summary>
    /// Compute the sibling temp res:// path for a given res:// path, inserting
    /// ".tmp" before the extension so Godot dispatches to the correct saver.
    /// e.g. "res://scenes/Floor3F.tscn" -> "res://scenes/Floor3F.tmp.tscn".
    /// </summary>
    private static string TempPathFor(string resPath)
    {
        int slashIdx = resPath.LastIndexOf('/');
        int dotIdx = resPath.LastIndexOf('.');
        return dotIdx > slashIdx
            ? resPath.Substring(0, dotIdx) + ".tmp" + resPath.Substring(dotIdx)
            : resPath + ".tmp";
    }

    /// <summary>
    /// Write JSON to a sibling temp file (staging phase). Returns the temp
    /// res:// path. The caller commits via <see cref="CommitJsonTemp"/>.
    /// </summary>
    private static string WriteJsonToTemp(FloorJsonModel model, string resPath)
    {
        string tempResPath = TempPathFor(resPath);
        string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
        string absDir = Path.GetDirectoryName(tempAbs) ?? string.Empty;
        if (!Directory.Exists(absDir))
            Directory.CreateDirectory(absDir);
        File.WriteAllText(tempAbs, model.ToJson(indented: true));
        return tempResPath;
    }

    /// <summary>
    /// Move a staged JSON temp file into its final path (commit phase).
    /// </summary>
    private static void CommitJsonTemp(string tempResPath, string finalResPath)
    {
        string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
        string realAbs = ProjectSettings.GlobalizePath(finalResPath);
        try
        {
            File.Move(tempAbs, realAbs, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempAbs)) File.Delete(tempAbs);
            throw;
        }
    }

    /// <summary>
    /// Delete a temp file if it exists (cleanup on staging/commit failure).
    /// </summary>
    private static void CleanupTemp(string tempResPath)
    {
        if (tempResPath == null) return;
        string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
        if (File.Exists(tempAbs)) File.Delete(tempAbs);
        string uidTmp = tempAbs + ".uidtmp";
        if (File.Exists(uidTmp)) File.Delete(uidTmp);
    }

    public static (int Width, int Height) DimensionsFor(int floorNumber) => floorNumber switch
    {
        0 => (Floor0Layout.GridWidth, Floor0Layout.GridHeight),            // GF ground is the full grid
        1 => (Floor1Layout.Width, Floor1Layout.Height),
        2 => (Floor2Layout.Width, Floor2Layout.Height),
        3 => (Floor3Layout.Width, Floor3Layout.Height),
        _ => throw new System.ArgumentOutOfRangeException(nameof(floorNumber), floorNumber,
            $"No layout dimensions registered for floor {floorNumber}"),
    };
}
