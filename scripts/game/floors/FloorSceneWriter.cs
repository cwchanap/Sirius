using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public record FloorSceneResult(bool Success, ValidationResult Validation, string Summary);

public static class FloorSceneWriter
{
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
        var model = FloorGenerationService.Generate(floorNumber);
        var (width, height) = DimensionsFor(floorNumber);

        var validation = FloorValidationService.Validate(model, width, height);
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
            importer.ImportToScene(model, gridMap);

            // Sync .tres via typed API (skippable for --skip-floor-def parity).
            if (syncDef)
            {
                var def = ResourceLoader.Load<FloorDefinition>(paths.DefPath, cacheMode: ResourceLoader.CacheMode.Ignore);
                if (def == null)
                    return new FloorSceneResult(false, validation, $"Failed to load FloorDefinition: {paths.DefPath}");
                FloorResourceSyncService.Apply(def, model, options);
                var defSaveErr = SaveResourceAtomic(def, outDefPath);
                if (defSaveErr != Error.Ok)
                    return new FloorSceneResult(false, validation, $"Failed to save FloorDefinition ({defSaveErr}): {outDefPath}");
                if (defUidSnap is not null)
                    UidPreserver.Restore(outDefPath, defUidSnap);
            }

            // Pack + save scene.
            var newPacked = new PackedScene();
            var packErr = newPacked.Pack(scene);
            if (packErr != Error.Ok)
                return new FloorSceneResult(false, validation, $"Failed to pack scene ({packErr}): {outScenePath}");
            var sceneSaveErr = SaveResourceAtomic(newPacked, outScenePath);
            if (sceneSaveErr != Error.Ok)
                return new FloorSceneResult(false, validation, $"Failed to save scene ({sceneSaveErr}): {outScenePath}");
            UidPreserver.Restore(outScenePath, sceneUidSnap);

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
    /// picks the correct saver), then <see cref="File.Move"/> with overwrite swaps
    /// it into place. A crash mid-save cannot leave a half-written committed file,
    /// and a .tscn failure cannot corrupt an already-written .tres (each save is
    /// independently atomic).
    /// </summary>
    private static Error SaveResourceAtomic(Resource res, string resPath)
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
            File.Move(tempAbs2, realAbs, overwrite: true);
        }
        catch
        {
            // File.Move can throw on Windows when the target is locked by another
            // process. Delete the orphaned .tmp so it doesn't accumulate as working-
            // tree noise, then rethrow so the caller surfaces the real failure.
            if (File.Exists(tempAbs2)) File.Delete(tempAbs2);
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
