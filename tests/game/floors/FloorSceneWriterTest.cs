using GdUnit4;
using Godot;
using Sirius.FloorTools;
using Sirius.TilemapJson;
using System.IO;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorSceneWriterTest
{
    private string _tempDir = "";

    [BeforeTest]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sirius_floor_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [AfterTest]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestCase]
    public void TestGenerateFloor3WritesSceneAndDefAndJsonToTempDir()
    {
        // Uses the temp-path seam so committed artifacts are NOT touched.
        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions(), outputDir: _tempDir);

        AssertThat(result.Success).IsTrue();
        AssertThat(result.Validation.HasErrors).IsFalse();
        AssertThat(File.Exists(Path.Combine(_tempDir, "Floor3F.tscn"))).IsTrue();
        AssertThat(File.Exists(Path.Combine(_tempDir, "Floor3F.tres"))).IsTrue();
        AssertThat(File.Exists(Path.Combine(_tempDir, "Floor3F.json"))).IsTrue();
    }

    [TestCase]
    public void TestGenerateFloor3PreservesSceneUidInOutput()
    {
        // The source Floor3F.tscn (committed) has uid="uid://dgojrfkh4qf5u" in its
        // header. ResourceSaver strips it; UidPreserver must re-inject it.
        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions(), outputDir: _tempDir);

        AssertThat(result.Success).IsTrue();
        string sceneText = File.ReadAllText(Path.Combine(_tempDir, "Floor3F.tscn"));
        AssertThat(sceneText.Contains("uid=\"uid://dgojrfkh4qf5u\"")).IsTrue();
    }

    [TestCase]
    public void TestGenerateFloor3PreservesDefUidInOutput()
    {
        // The source Floor3F.tres (committed) has uid="uid://b8hrre0k7p4pk" in its
        // header (referenced by Game.tscn). UidPreserver must re-inject it.
        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions(), outputDir: _tempDir);

        AssertThat(result.Success).IsTrue();
        string defText = File.ReadAllText(Path.Combine(_tempDir, "Floor3F.tres"));
        AssertThat(defText.Contains("uid=\"uid://b8hrre0k7p4pk\"")).IsTrue();
    }

    [TestCase]
    public void TestGenerateFloor1ProducesEntities()
    {
        // Floor 1 has enemies, NPCs, treasures, puzzle traps — exercises the
        // scene-writer entity handling path that Floor 3 (empty) does not cover.
        var result = FloorSceneWriter.Generate(1, new FloorSyncOptions(), outputDir: _tempDir);

        AssertThat(result.Success).IsTrue();
        AssertThat(result.Validation.HasErrors).IsFalse();
        AssertThat(File.Exists(Path.Combine(_tempDir, "Floor1F.tscn"))).IsTrue();

        // Assert content, not just file existence: an empty/partial scene would
        // pass a bare File.Exists + length check. Verify (a) the co-written JSON
        // has real entity counts and ground tiles, and (b) the .tscn text actually
        // contains EnemySpawn_/stair node declarations (entities made it into the
        // scene file, not just the JSON source).
        string jsonPath = Path.Combine(_tempDir, "Floor1F.json");
        AssertThat(File.Exists(jsonPath)).IsTrue();
        var model = FloorJsonModel.FromJson(File.ReadAllText(jsonPath));
        // Floor 1 is a 60x60 floor; ground tile count should be in the thousands.
        AssertThat(model.TileLayers["ground"].Count).IsGreater(100);
        AssertThat(model.Entities.EnemySpawns?.Count ?? 0).IsGreater(0);
        AssertThat(model.Entities.StairConnections?.Count ?? 0).IsGreater(0);

        string sceneText = File.ReadAllText(Path.Combine(_tempDir, "Floor1F.tscn"));
        // Entities must appear as node declarations in the scene file, not just in JSON.
        AssertThat(sceneText.Contains("[node name=\"EnemySpawn_")).IsTrue();
        AssertThat(sceneText.Contains("type=\"TileMapLayer\"")).IsTrue();
    }

    [TestCase]
    public void TestGenerateReturnsFailureWhenSceneMissingGridMap()
    {
        // Exercises the try/finally failure path: a scene without a GridMap node
        // triggers the null-guard return inside the try block; the finally must
        // still run (scene.QueueFree) without throwing.
        string tempScene = Path.Combine(_tempDir, "NoGridMap.tscn");
        File.WriteAllText(tempScene,
            "[gd_scene format=4]\n" +
            "[node name=\"Root\" type=\"Node2D\"]\n");

        var sourcePaths = new FloorPaths(
            ScenePath: ProjectSettings.LocalizePath(tempScene),
            DefPath: "res://resources/floors/Floor3F.tres",
            JsonPath: "res://scenes/game/floors/Floor3F.json");

        var result = FloorSceneWriter.Generate(3, new FloorSyncOptions(),
            writeJson: false, syncDef: false, sourcePaths: sourcePaths);

        AssertThat(result.Success).IsFalse();
        AssertThat(result.Summary).Contains("GridMap");
    }

    [TestCase]
    public void TestGenerateIsIdempotentForUidPreservation()
    {
        // Running the generator twice must produce identical UID metadata
        // (header uid= + ext_resource uid=) in both the .tscn and .tres.
        // If UidPreserver.Restore is not idempotent (e.g., generates new UIDs
        // instead of preserving captured ones, or drops them on re-save),
        // the second run's output would differ from the first.
        //
        // Uses Floor 3 (simplest floor) and the temp-dir seam so committed
        // artifacts are not touched. Both runs read from the same committed
        // source (registry paths) and write to the temp dir — this verifies
        // output UID stability across consecutive generate calls.
        var opts = new FloorSyncOptions();

        // First run.
        var first = FloorSceneWriter.Generate(3, opts, outputDir: _tempDir);
        AssertThat(first.Success).IsTrue();
        string scenePath = Path.Combine(_tempDir, "Floor3F.tscn");
        string defPath = Path.Combine(_tempDir, "Floor3F.tres");
        var sceneUid1 = UidPreserver.Capture(ProjectSettings.LocalizePath(scenePath));
        var defUid1 = UidPreserver.Capture(ProjectSettings.LocalizePath(defPath));

        // Second run to the same temp dir (overwrites first run's output).
        var second = FloorSceneWriter.Generate(3, opts, outputDir: _tempDir);
        AssertThat(second.Success).IsTrue();
        var sceneUid2 = UidPreserver.Capture(ProjectSettings.LocalizePath(scenePath));
        var defUid2 = UidPreserver.Capture(ProjectSettings.LocalizePath(defPath));

        // Header UIDs must be identical across runs.
        AssertThat(sceneUid2.HeaderUid).IsEqual(sceneUid1.HeaderUid);
        AssertThat(defUid2.HeaderUid).IsEqual(defUid1.HeaderUid);
        AssertThat(defUid2.HeaderLoadSteps!.Value).IsEqual(defUid1.HeaderLoadSteps!.Value);

        // ext_resource UID maps must be identical (same paths, same UIDs).
        AssertThat(sceneUid2.PathToUid.Count).IsEqual(sceneUid1.PathToUid.Count);
        foreach (var kv in sceneUid1.PathToUid)
        {
            AssertThat(sceneUid2.PathToUid.ContainsKey(kv.Key)).IsTrue();
            AssertThat(sceneUid2.PathToUid[kv.Key]).IsEqual(kv.Value);
        }
        AssertThat(defUid2.PathToUid.Count).IsEqual(defUid1.PathToUid.Count);
        foreach (var kv in defUid1.PathToUid)
        {
            AssertThat(defUid2.PathToUid.ContainsKey(kv.Key)).IsTrue();
            AssertThat(defUid2.PathToUid[kv.Key]).IsEqual(kv.Value);
        }
    }
}
