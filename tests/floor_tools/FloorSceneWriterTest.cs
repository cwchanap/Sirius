using GdUnit4;
using Godot;
using Sirius.FloorTools;
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
        // Floor 1 generation should produce a non-trivial scene.
        var fi = new FileInfo(Path.Combine(_tempDir, "Floor1F.tscn"));
        AssertThat(fi.Length > 100).IsTrue();
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
}
