using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System;

namespace Sirius.FloorTools.Addon;

[Tool]
public partial class SiriusFloorToolsDock : Control
{
    private OptionButton _floorOption;
    private RichTextLabel _resultsLabel;
    private ConfirmationDialog _confirmDialog;
    private Action _pendingAction;

    public override void _Ready()
    {
        _floorOption = GetNodeOrNull<OptionButton>("%FloorOption");
        _resultsLabel = GetNodeOrNull<RichTextLabel>("%ResultsLabel");
        _confirmDialog = GetNodeOrNull<ConfirmationDialog>("%ConfirmDialog");

        ConnectButton("GenerateButton", OnGeneratePressed);
        ConnectButton("ValidateButton", OnValidate);
        ConnectButton("ExportJsonButton", OnExportJsonPressed);
        ConnectButton("ImportJsonButton", OnImportJsonPressed);
        ConnectButton("BakeSaveButton", OnBakeSavePressed);

        if (_confirmDialog != null)
            _confirmDialog.Confirmed += OnConfirmAccepted;

        Log("Sirius Floor Tools ready.");
    }

    private void ConnectButton(string ownerName, Action handler)
    {
        var btn = GetNodeOrNull<Button>("%" + ownerName);
        if (btn != null)
            btn.Pressed += handler;
    }

    private int SelectedFloor => _floorOption?.Selected ?? 0;

    // Returns the floor index derived from the currently edited scene's file path
    // basename (e.g. "Floor2F.tscn" -> 2), or -1 if no scene is open or the scene
    // does not match any registered floor. Used to guard Export/Import against
    // cross-floor data corruption (open scene must match the selected floor).
    private int OpenSceneFloor
    {
        get
        {
            var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
            if (scene == null) return -1;
            return FloorRegistry.FindByScenePath(scene.SceneFilePath);
        }
    }

    // Pops the confirmation dialog. The destructive action runs only after the
    // user accepts. Pre-flight aborts (no scene, mismatch) happen before this so
    // we never prompt for an action that would immediately bail out.
    private void Confirm(string title, string body, Action onConfirm)
    {
        if (_confirmDialog == null)
        {
            // No dialog available (e.g. scene modified outside editor) — proceed
            // without confirmation rather than silently swallowing the action.
            onConfirm();
            return;
        }
        _confirmDialog.Title = title;
        _confirmDialog.DialogText = body;
        _pendingAction = onConfirm;
        _confirmDialog.PopupCentered();
    }

    private void OnConfirmAccepted()
    {
        var action = _pendingAction;
        _pendingAction = null;
        action?.Invoke();
    }

    private void OnGeneratePressed()
    {
        // Generate is deterministic from the floor index and does NOT read the open
        // scene, so a mismatch is not a corruption path — but warn to avoid confusion
        // (e.g. user has 1F open, selects 2F, hits Generate, sees 2F files change).
        int openFloor = OpenSceneFloor;
        if (openFloor != -1 && openFloor != SelectedFloor)
            Log($"[!] Warning: open scene is floor {openFloor} but selected floor is {SelectedFloor}. Generate writes to the selected floor only.");

        var paths = FloorRegistry.Get(SelectedFloor);
        Confirm("Generate Floor",
            $"Regenerate floor {SelectedFloor} from C# and overwrite:\n  {paths.ScenePath}\n  {paths.DefPath}\n  {paths.JsonPath}\n\nUnsaved editor changes to these files will be lost.",
            DoGenerate);
    }

    private void DoGenerate()
    {
        try
        {
            var result = FloorSceneWriter.Generate(SelectedFloor, new FloorSyncOptions());
            Log(result.Summary);
            foreach (var issue in result.Validation.Issues)
                Log($"  {(issue.Severity == Severity.Error ? "[x]" : "[!]")} {issue.Code}: {issue.Message}");
            EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
        }
        catch (Exception ex)
        {
            // FloorSceneWriter.Generate can throw (e.g. SupplementalEnemyPlanner
            // throws InvalidOperationException when a layout edit shrinks walkable
            // space below the supplemental-enemy budget). Without this catch the
            // exception escapes the editor signal callback and aborts silently
            // with no user-visible message. Mirrors FloorCli.Run's catch.
            Log($"[x] Generate failed: {ex.Message}");
        }
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

    private void OnExportJsonPressed()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to export."); return; }
        // Guard against cross-floor corruption: the open scene's grid contents would
        // be written to the selected floor's JSON path. Abort on mismatch.
        int openFloor = FloorRegistry.FindByScenePath(scene.SceneFilePath);
        var abort = FloorDockGuard.MismatchAbortMessage(openFloor, SelectedFloor, $"Export would write the wrong grid to {paths.JsonPath}.");
        if (abort != null) { Log(abort); return; }
        Confirm("Export JSON",
            $"Overwrite {paths.JsonPath} with the open scene's grid contents?",
            DoExportJson);
    }

    private void DoExportJson()
    {
        try
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
        catch (Exception ex)
        {
            Log($"[x] Export failed: {ex.Message}");
        }
    }

    private void OnImportJsonPressed()
    {
        var paths = FloorRegistry.Get(SelectedFloor);
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
        if (gridMap == null) { Log("Open a floor scene to import into."); return; }
        // Guard against cross-floor corruption: the selected floor's JSON would be
        // imported into the open scene's grid. Abort on mismatch.
        int openFloor = FloorRegistry.FindByScenePath(scene.SceneFilePath);
        var abort = FloorDockGuard.MismatchAbortMessage(openFloor, SelectedFloor, $"Import would pour {paths.JsonPath} into the wrong scene.");
        if (abort != null) { Log(abort); return; }
        Confirm("Import JSON",
            $"Import {paths.JsonPath} into the open scene's grid?\nThis replaces the current grid contents in the editor (save the scene afterwards to persist).",
            DoImportJson);
    }

    private void DoImportJson()
    {
        try
        {
            var paths = FloorRegistry.Get(SelectedFloor);
            var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
            var gridMap = scene?.GetNodeOrNull<GridMap>("GridMap");
            if (gridMap == null) { Log("Open a floor scene to import into."); return; }
            var importer = new TilemapJsonImporter();
            var err = importer.ImportFromFile(paths.JsonPath, gridMap);
            Log(err == Error.Ok ? $"Imported from {paths.JsonPath}" : $"Import failed: {err}");
        }
        catch (Exception ex)
        {
            Log($"[x] Import failed: {ex.Message}");
        }
    }

    private void OnBakeSavePressed()
    {
        var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
        if (scene == null) { Log("No scene open to save."); return; }
        Confirm("Bake / Save Scene",
            $"Pack and save the open scene to:\n  {scene.SceneFilePath}\n\nUnsaved editor changes will be baked in; the file on disk will be overwritten.",
            DoBakeSave);
    }

    private void DoBakeSave()
    {
        try
        {
            var scene = EditorInterface.Singleton?.GetEditedSceneRoot();
            if (scene == null) { Log("No scene open to save."); return; }
            // ResourceSaver strips file-level UIDs on re-save; capture before and restore
            // after, mirroring FloorSceneWriter's guard. Game.tscn references Floor*.tres
            // by UID, so stripping breaks reference stability.
            var uidSnap = UidPreserver.Capture(scene.SceneFilePath);
            var packed = new PackedScene();
            var packErr = packed.Pack(scene);
            if (packErr != Error.Ok)
            {
                Log($"[x] Failed to pack scene: {packErr}");
                return;
            }
            var saveErr = ResourceSaver.Save(packed, scene.SceneFilePath);
            if (saveErr != Error.Ok)
            {
                Log($"[x] Failed to save scene: {saveErr}");
                return;
            }
            UidPreserver.Restore(scene.SceneFilePath, uidSnap);
            EditorInterface.Singleton?.GetResourceFilesystem()?.Scan();
            Log($"Saved scene {scene.SceneFilePath}");
        }
        catch (Exception ex)
        {
            Log($"[x] Bake/Save failed: {ex.Message}");
        }
    }

    private void Log(string message)
    {
        if (_resultsLabel != null)
            _resultsLabel.AddText(message + "\n");
    }
}
