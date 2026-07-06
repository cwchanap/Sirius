using GdUnit4;
using Godot;
using Sirius.FloorTools;
using System.IO;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UidPreserverTest
{
    private string _tempDir = "";

    [BeforeTest]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sirius_uid_test_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [AfterTest]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [TestCase]
    public void TestCaptureExtractsHeaderAndExtResourceUids()
    {
        string path = Path.Combine(_tempDir, "Test.tscn");
        File.WriteAllText(path,
            "[gd_scene format=4 uid=\"uid://abc123\"]\n" +
            "[ext_resource type=\"Script\" uid=\"uid://scr1\" path=\"res://scripts/Foo.cs\" id=\"1\"]\n" +
            "[ext_resource type=\"Script\" path=\"res://scripts/NoUid.cs\" id=\"2\"]\n" +
            "[node name=\"Root\" type=\"Node2D\"]\n");

        string resPath = ProjectSettings.LocalizePath(path);
        var snap = UidPreserver.Capture(resPath);

        AssertThat(snap.HeaderUid).IsEqual("uid://abc123");
        AssertThat(snap.PathToUid["res://scripts/Foo.cs"]).IsEqual("uid://scr1");
        AssertThat(snap.PathToUid.ContainsKey("res://scripts/NoUid.cs")).IsFalse();
    }

    [TestCase]
    public void TestRestoreReinjectsHeaderAndExtResourceUids()
    {
        string path = Path.Combine(_tempDir, "Test.tscn");
        // Write a file WITH UIDs, capture, then write a STRIPPED version and restore.
        File.WriteAllText(path,
            "[gd_scene format=4 uid=\"uid://abc123\"]\n" +
            "[ext_resource type=\"Script\" uid=\"uid://scr1\" path=\"res://scripts/Foo.cs\" id=\"1\"]\n");
        string resPath = ProjectSettings.LocalizePath(path);
        var snap = UidPreserver.Capture(resPath);

        // Simulate ResourceSaver stripping UIDs.
        File.WriteAllText(path,
            "[gd_scene format=4]\n" +
            "[ext_resource type=\"Script\" path=\"res://scripts/Foo.cs\" id=\"1\"]\n");

        UidPreserver.Restore(resPath, snap);

        string restored = File.ReadAllText(path);
        AssertThat(restored.Contains("uid=\"uid://abc123\"")).IsTrue();
        AssertThat(restored.Contains("uid=\"uid://scr1\"")).IsTrue();
    }

    [TestCase]
    public void TestRestoreIsNoOpWhenSnapshotEmpty()
    {
        string path = Path.Combine(_tempDir, "Test.tscn");
        File.WriteAllText(path, "[gd_scene format=4]\n[node name=\"Root\" type=\"Node2D\"]\n");
        string resPath = ProjectSettings.LocalizePath(path);
        string before = File.ReadAllText(path);

        UidPreserver.Restore(resPath, new UidPreserver.Snapshot());

        string after = File.ReadAllText(path);
        AssertThat(after).IsEqual(before);
    }

    [TestCase]
    public void TestCaptureHandlesTresResourceHeader()
    {
        string path = Path.Combine(_tempDir, "Test.tres");
        File.WriteAllText(path,
            "[gd_resource type=\"Resource\" load_steps=3 format=3 uid=\"uid://res123\"]\n" +
            "[ext_resource type=\"Script\" uid=\"uid://scr1\" path=\"res://scripts/Bar.cs\" id=\"1\"]\n");
        string resPath = ProjectSettings.LocalizePath(path);
        var snap = UidPreserver.Capture(resPath);

        AssertThat(snap.HeaderUid).IsEqual("uid://res123");
        AssertThat(snap.PathToUid["res://scripts/Bar.cs"]).IsEqual("uid://scr1");
    }
}
