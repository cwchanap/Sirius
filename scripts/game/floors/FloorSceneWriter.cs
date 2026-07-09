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

            // Save the scene FIRST, then the .tres. If the .tscn save fails the
            // .tres has not been committed yet, avoiding a partial-update window
            // where the .tres reflects new metadata but the .tscn retains stale
            // grid content. The pack already succeeded, so the .tscn save is the
            // lowest-risk write, but ordering it first is strictly safer.
            // UID snapshots are passed into SaveResourceAtomic so UIDs are restored
            // on the temp file BEFORE the atomic move — the committed file never
            // appears with stripped UIDs, closing the non-atomic window that would
            // exist if Restore ran as a separate post-move step.
            var sceneSaveErr = SaveResourceAtomic(newPacked, outScenePath, sceneUidSnap);
            if (sceneSaveErr != Error.Ok)
                return new FloorSceneResult(false, validation, $"Failed to save scene ({sceneSaveErr}): {outScenePath}");

            // Sync .tres via typed API (skippable for --skip-floor-def parity).
            if (syncDef)
            {
                var def = ResourceLoader.Load<FloorDefinition>(paths.DefPath, cacheMode: ResourceLoader.CacheMode.Ignore);
                if (def == null)
                    return new FloorSceneResult(false, validation, $"Failed to load FloorDefinition: {paths.DefPath}");
                FloorResourceSyncService.Apply(def, model, options);
                var defSaveErr = SaveResourceAtomic(def, outDefPath, defUidSnap);
                if (defSaveErr != Error.Ok)
                    return new FloorSceneResult(false, validation, $"Failed to save FloorDefinition ({defSaveErr}): {outDefPath}");
            }

            if (writeJson)
                WriteJson(model, outJsonPath);
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
    private static Error SaveResourceAtomic(Resource res, string resPath, UidPreserver.Snapshot? uidSnap = null)
    {
        // Insert ".tmp" before the extension so Godot still sees .tscn/.tres
        // and dispatches to the correct ResourceSaver. e.g. "Floor3F.tscn"
        // -> "Floor3F.tmp.tscn". String-based (not Path.Get*) because resPath
        // is a res:// path and Path.* helpers are platform-dependent on those.
        int slashIdx = resPath.LastIndexOf('/');
        int dotIdx = resPath.LastIndexOf('.');
        string tempResPath = dotIdx > slashIdx
            ? resPath.Substring(0, dotIdx) + ".tmp" + resPath.Substring(dotIdx)
            : resPath + ".tmp";

        var err = ResourceSaver.Save(res, tempResPath);
        if (err != Error.Ok)
        {
            string tempAbs = ProjectSettings.GlobalizePath(tempResPath);
            if (File.Exists(tempAbs)) File.Delete(tempAbs);
            return err;
        }
        string tempAbs2 = ProjectSettings.GlobalizePath(tempResPath);
        string realAbs = ProjectSettings.GlobalizePath(resPath);
        try
        {
            // Restore UIDs on the temp file BEFORE the atomic move so the
            // committed file never appears with stripped UIDs. This closes
            // the non-atomic window that would exist if Restore ran as a
            // separate post-move step.
            if (uidSnap is not null)
                UidPreserver.Restore(tempResPath, uidSnap);
            File.Move(tempAbs2, realAbs, overwrite: true);
        }
        catch
        {
            // File.Move can throw on Windows when the target is locked by another
            // process. Delete the orphaned .tmp (and any .uidtmp left by Restore)
            // so they don't accumulate as working-tree noise, then rethrow so the
            // caller surfaces the real failure.
            if (File.Exists(tempAbs2)) File.Delete(tempAbs2);
            string uidTmp = tempAbs2 + ".uidtmp";
            if (File.Exists(uidTmp)) File.Delete(uidTmp);
            throw;
        }
        return Error.Ok;
    }

    private static void WriteJson(FloorJsonModel model, string path)
    {
        // Atomic write (temp → File.Move overwrite) so a crash mid-write cannot
        // truncate the committed .json. Matches SaveManager/UidPreserver pattern.
        AtomicFileWriter.WriteAllText(path, model.ToJson(indented: true));
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
