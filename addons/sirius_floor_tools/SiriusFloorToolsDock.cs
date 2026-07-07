using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsDock : Control
{
    private OptionButton _floorOption;
    private RichTextLabel _resultsLabel;

    public override void _Ready()
    {
        _floorOption = GetNodeOrNull<OptionButton>("%FloorOption");
        _resultsLabel = GetNodeOrNull<RichTextLabel>("%ResultsLabel");

        ConnectButton("GenerateButton", OnGenerate);
        ConnectButton("ValidateButton", OnValidate);
        ConnectButton("ExportJsonButton", OnExportJson);
        ConnectButton("ImportJsonButton", OnImportJson);
        ConnectButton("BakeSaveButton", OnBakeSave);
        Log("Sirius Floor Tools ready.");
    }

    private void ConnectButton(string ownerName, System.Action handler)
    {
        var btn = GetNodeOrNull<Button>("%" + ownerName);
        if (btn != null)
            btn.Pressed += handler;
    }

    private int SelectedFloor => _floorOption?.Selected ?? 0;

    private void OnGenerate()
    {
        var result = FloorSceneWriter.Generate(SelectedFloor, new FloorSyncOptions());
        Log(result.Summary);
        foreach (var issue in result.Validation.Issues)
            Log($"  {(issue.Severity == Severity.Error ? "[x]" : "[!]")} {issue.Code}: {issue.Message}");
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
    }

    private void OnValidate()
    {
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (scene == null) { Log("No scene open to validate."); return; }
        var gridMap = scene.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene (no GridMap found)."); return; }

        var exporter = new TilemapJsonExporter();
        var model = exporter.ExportScene(gridMap);
        var (w, h) = (gridMap.GridWidth, gridMap.GridHeight);
        var result = FloorValidationService.Validate(model, w, h);
        Log(result.HasErrors ? "Validation FAILED" : "Validation passed");
        foreach (var issue in result.Issues)
            Log($"  {(issue.Severity == Severity.Error ? "[x]" : "[!]")} {issue.Code}: {issue.Message}");
    }

    private void OnExportJson()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to export."); return; }
        var exporter = new TilemapJsonExporter();
        exporter.ExportToFile(gridMap, paths.JsonPath);
        Log($"Exported JSON to {paths.JsonPath}");
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
    }

    private void OnImportJson()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to import into."); return; }
        var importer = new TilemapJsonImporter();
        var err = importer.ImportFromFile(paths.JsonPath, gridMap);
        Log(err == Error.Ok ? $"Imported from {paths.JsonPath}" : $"Import failed: {err}");
    }

    private void OnBakeSave()
    {
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (scene == null) { Log("No scene open to save."); return; }
        var packed = new PackedScene();
        packed.Pack(scene);
        ResourceSaver.Save(packed, scene.SceneFilePath);
        EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
        Log($"Saved scene {scene.SceneFilePath}");
    }

    private void Log(string message)
    {
        if (_resultsLabel != null)
            _resultsLabel.AddText(message + "\n");
    }
}
