using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using System.Linq;

namespace Sirius.FloorTools;

public record FloorSceneResult(bool Success, ValidationResult Validation, string Summary);

public static class FloorSceneWriter
{
    public static FloorSceneResult Generate(int floorNumber, FloorSyncOptions options, bool writeJson = true, bool syncDef = true)
    {
        var paths = FloorRegistry.Get(floorNumber);
        var model = FloorGenerationService.Generate(floorNumber);
        var (width, height) = DimensionsFor(floorNumber);

        var validation = FloorValidationService.Validate(model, width, height);
        if (validation.HasErrors)
            return new FloorSceneResult(false, validation, $"Validation failed: {validation.Issues.Count} issue(s)");

        // Write into the scene via the existing importer.
        var packed = GD.Load<PackedScene>(paths.ScenePath);
        var scene = packed.Instantiate();
        var gridMap = scene.GetNode<GridMap>("GridMap");
        var importer = new TilemapJsonImporter();
        importer.ImportToScene(model, gridMap);

        // Sync .tres via typed API (skippable for --skip-floor-def parity).
        if (syncDef)
        {
            var def = ResourceLoader.Load<FloorDefinition>(paths.DefPath);
            FloorResourceSyncService.Apply(def, model, options);
            ResourceSaver.Save(def, paths.DefPath);
        }

        // Pack + save scene.
        var newPacked = new PackedScene();
        newPacked.Pack(scene);
        ResourceSaver.Save(newPacked, paths.ScenePath);
        scene.QueueFree();

        if (writeJson)
            WriteJson(model, paths.JsonPath);

        int walls = model.TileLayers.GetValueOrDefault("wall")?.Count ?? 0;
        int enemies = model.Entities.EnemySpawns?.Count ?? 0;
        return new FloorSceneResult(true, validation,
            $"Floor {floorNumber}: {walls} walls, {enemies} enemies generated");
    }

    public static FloorJsonModel GenerateToJson(int floorNumber)
        => FloorGenerationService.Generate(floorNumber);

    private static void WriteJson(FloorJsonModel model, string path)
    {
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(model.ToJson(indented: true));
    }

    private static (int Width, int Height) DimensionsFor(int floorNumber) => floorNumber switch
    {
        0 => (160, 160),            // GF ground is the full grid
        1 => (Layouts.Floor1Layout.Width, Layouts.Floor1Layout.Height),
        2 => (Layouts.Floor2Layout.Width, Layouts.Floor2Layout.Height),
        3 => (Layouts.Floor3Layout.Width, Layouts.Floor3Layout.Height),
        _ => (160, 160),
    };
}
