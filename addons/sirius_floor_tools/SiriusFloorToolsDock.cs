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

    // Load the FloorDefinition (.tres) for a floor index. Used by Validate and
    // Export JSON so ExportMetadata fills floor_number and player_start from the
    // def rather than leaving them at defaults (0 / null). Ignore-cached so a
    // freshly-edited .tres in the editor is picked up without a filesystem scan.
    private static FloorDefinition LoadFloorDefinition(int floorNumber)
    {
        var paths = FloorRegistry.Get(floorNumber);
        return ResourceLoader.Load<FloorDefinition>(paths.DefPath,
            cacheMode: ResourceLoader.CacheMode.Ignore);
    }

    // Read the existing baseline JSON for a floor so FloorExportMerge can carry
    // forward JSON-only fields (hidden_placeholders) and generator-authored
    // metadata (floor_name/description). Returns null when the file does not
    // exist yet (e.g. first export of a brand-new floor) — the merge treats null
    // as "nothing to preserve" and keeps the exporter's seeded values.
    private static FloorJsonModel LoadBaselineJson(string jsonPath)
    {
        if (!FileAccess.FileExists(jsonPath))
            return null;
        using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;
        return FloorJsonModel.FromJson(file.GetAsText());
    }

    // Write a FloorJsonModel to disk atomically. Routes through
    // AtomicFileWriter (temp file + rename) so an editor crash/kill mid-write
    // cannot leave a truncated baseline — opening with ModeFlags.Write
    // truncates before StoreString completes, which would corrupt the
    // canonical JSON and bypass the atomic write used by the generator.
    private static Error WriteJson(FloorJsonModel model, string outputPath)
    {
        string json = model.ToJson(indented: true);
        try
        {
            AtomicFileWriter.WriteAllText(outputPath, json);
            return Error.Ok;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[SiriusFloorToolsDock] Failed to write output file: {outputPath}\n{ex.Message}");
            return Error.CantOpen;
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
            // If the generated floor's scene is currently open in the editor, the
            // in-memory EditedSceneRoot still points at the pre-generation grid.
            // A subsequent Bake/Save or Export JSON would then operate on that
            // stale grid and overwrite the freshly-generated artifacts. Reload the
            // open scene from disk so the editor reflects the new .tscn contents.
            var openScene = EditorInterface.Singleton?.GetEditedSceneRoot();
            if (openScene != null
                && FloorRegistry.FindByScenePath(openScene.SceneFilePath) == SelectedFloor)
            {
                EditorInterface.Singleton?.ReloadSceneFromPath(openScene.SceneFilePath);
                Log($"Reloaded open scene {openScene.SceneFilePath} from disk.");
            }
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

        // ExportScene fills FloorMetadata (floor_number, player_start) from the
        // FloorDefinition; without it, player_start is null and
        // FloorValidationService.Validate dereferences it (NRE) on Floor1/2/3.
        // Require a registered floor so the def can be loaded.
        int openFloor = FloorRegistry.FindByScenePath(scene.SceneFilePath);
        if (openFloor == -1) { Log("Open a registered floor scene to validate."); return; }
        var floorDef = LoadFloorDefinition(openFloor);
        if (floorDef == null) { Log($"[x] Failed to load FloorDefinition for floor {openFloor}."); return; }

        var exporter = new TilemapJsonExporter();
        var model = exporter.ExportScene(gridMap, floorDef);
        if (model == null) { Log("[x] Export returned null (tile config load failed)."); return; }
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
            // Revalidate the cross-floor guard: the confirmation dialog defers
            // execution, so the floor selector or edited scene may have changed
            // since OnExportJsonPressed ran the guard. Re-running it here ensures
            // the validated state is the state we actually write.
            int openFloor = FloorRegistry.FindByScenePath(scene.SceneFilePath);
            var abort = FloorDockGuard.MismatchAbortMessage(openFloor, SelectedFloor, $"Export would write the wrong grid to {paths.JsonPath}.");
            if (abort != null) { Log(abort); return; }
            // Pass the FloorDefinition so ExportMetadata fills floor_number and
            // player_start. Without it the exported JSON has floor_number=0 and
            // player_start=null, which would overwrite Floor1/2/3 baselines with
            // invalid metadata.
            var floorDef = LoadFloorDefinition(SelectedFloor);
            if (floorDef == null) { Log($"[x] Failed to load FloorDefinition for floor {SelectedFloor}."); return; }
            // Export the scene to a model, then merge JSON-only fields
            // (hidden_placeholders) and generator-authored metadata
            // (floor_name/description) from the existing baseline before writing.
            // Without this merge, exporting a generated baseline (Floor1F/2F/3F)
            // would drop hidden_placeholders (the exporter has no scene node for
            // them) and rewrite the baseline's generator description with the
            // .tres's hand-authored text, causing the committed JSON / parity
            // tests to drift even when the scene edit was unrelated.
            var exporter = new TilemapJsonExporter();
            var model = exporter.ExportScene(gridMap, floorDef);
            if (model == null) { Log("[x] Export returned null (tile config load failed)."); return; }
            FloorJsonModel baseline = LoadBaselineJson(paths.JsonPath);
            FloorExportMerge.MergeBaseline(model, baseline);
            var err = WriteJson(model, paths.JsonPath);
            Log(err == Error.Ok ? $"Exported JSON to {paths.JsonPath}" : $"[x] Export failed: {err}");
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
            // Revalidate the cross-floor guard: the confirmation dialog defers
            // execution, so the floor selector or edited scene may have changed
            // since OnImportJsonPressed ran the guard. Re-running it here ensures
            // the validated state is the state we actually import into.
            int openFloor = FloorRegistry.FindByScenePath(scene.SceneFilePath);
            var abort = FloorDockGuard.MismatchAbortMessage(openFloor, SelectedFloor, $"Import would pour {paths.JsonPath} into the wrong scene.");
            if (abort != null) { Log(abort); return; }
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
            // ResourceSaver strips file-level UIDs on re-save; capture before and
            // restore via SaveResourceAtomic (temp-save → restore-on-temp → atomic
            // move). This mirrors FloorSceneWriter's guard so the committed file
            // never appears with stripped UIDs — Game.tscn references Floor*.tres
            // by UID, so a stripped-UID window (editor kill or Restore failure
            // after a direct save) breaks reference stability.
            var uidSnap = UidPreserver.Capture(scene.SceneFilePath);
            var packed = new PackedScene();
            var packErr = packed.Pack(scene);
            if (packErr != Error.Ok)
            {
                Log($"[x] Failed to pack scene: {packErr}");
                return;
            }
            var saveErr = FloorSceneWriter.SaveResourceAtomic(packed, scene.SceneFilePath, uidSnap);
            if (saveErr != Error.Ok)
            {
                Log($"[x] Failed to save scene: {saveErr}");
                return;
            }
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
