using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sirius.FloorTools;

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
                var def = ResourceLoader.Load<FloorDefinition>(paths.DefPath);
                if (def == null)
                    return new FloorSceneResult(false, validation, $"Failed to load FloorDefinition: {paths.DefPath}");
                FloorResourceSyncService.Apply(def, model, options);
                ResourceSaver.Save(def, outDefPath);
                if (defUidSnap is not null)
                    UidPreserver.Restore(outDefPath, defUidSnap);
            }

            // Pack + save scene.
            var newPacked = new PackedScene();
            newPacked.Pack(scene);
            ResourceSaver.Save(newPacked, outScenePath);
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
        return new FloorSceneResult(true, validation,
            $"Floor {floorNumber}: {walls} walls, {enemies} enemies generated");
    }

    private static string ToResPath(string outputDir, string basename)
    {
        string abs = Path.Combine(outputDir, basename);
        return ProjectSettings.LocalizePath(abs);
    }

    private static void WriteJson(FloorJsonModel model, string path)
    {
        // Atomic write (temp → File.Move overwrite) so a crash mid-write cannot
        // truncate the committed .json. Matches SaveManager/UidPreserver pattern.
        AtomicFileWriter.WriteAllText(path, model.ToJson(indented: true));
    }

    private static (int Width, int Height) DimensionsFor(int floorNumber) => floorNumber switch
    {
        0 => (Layouts.Floor0Layout.GridWidth, Layouts.Floor0Layout.GridHeight),            // GF ground is the full grid
        1 => (Layouts.Floor1Layout.Width, Layouts.Floor1Layout.Height),
        2 => (Layouts.Floor2Layout.Width, Layouts.Floor2Layout.Height),
        3 => (Layouts.Floor3Layout.Width, Layouts.Floor3Layout.Height),
        _ => throw new System.ArgumentOutOfRangeException(nameof(floorNumber), floorNumber,
            $"No layout dimensions registered for floor {floorNumber}"),
    };
}
