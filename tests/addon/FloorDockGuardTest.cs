using GdUnit4;
using Sirius.FloorTools.Addon;
using static GdUnit4.Assertions;

// Tests the cross-floor mismatch guard extracted from SiriusFloorToolsDock.
// The dock itself is editor-coupled (EditorInterface.Singleton) and cannot run
// under GdUnit4, but the safety logic lives in the pure FloorDockGuard helper.
// These tests lock down the abort behavior so a future refactor cannot silently
// drop the guard that prevents Export/Import from writing the wrong floor's data.
[TestSuite]
public partial class FloorDockGuardTest
{
    private const string ExportConsequence = "Export would write the wrong grid to res://scenes/game/floors/Floor2F.json.";
    private const string ImportConsequence = "Import would pour res://scenes/game/floors/Floor2F.json into the wrong scene.";

    [TestCase]
    public void TestMatchingFloorReturnsNull()
    {
        AssertThat(FloorDockGuard.MismatchAbortMessage(2, 2, ExportConsequence)).IsNull();
    }

    [TestCase]
    public void TestExportAbortOnFloorMismatch()
    {
        var msg = FloorDockGuard.MismatchAbortMessage(1, 2, ExportConsequence);
        AssertThat(msg).IsNotNull();
        AssertThat(msg!.Contains("[x]")).IsTrue();
        AssertThat(msg.Contains("floor 1")).IsTrue();
        AssertThat(msg.Contains("selected floor is 2")).IsTrue();
        AssertThat(msg.Contains("Export would write the wrong grid")).IsTrue();
    }

    [TestCase]
    public void TestImportAbortOnFloorMismatch()
    {
        var msg = FloorDockGuard.MismatchAbortMessage(1, 2, ImportConsequence);
        AssertThat(msg).IsNotNull();
        AssertThat(msg!.Contains("Import would pour")).IsTrue();
    }

    [TestCase]
    public void TestNonFloorSceneAborts()
    {
        // openFloor -1 = open scene is not a registered floor (e.g. MainMenu.tscn).
        var msg = FloorDockGuard.MismatchAbortMessage(-1, 0, ExportConsequence);
        AssertThat(msg).IsNotNull();
        AssertThat(msg!.Contains("not a floor")).IsTrue();
        AssertThat(msg.Contains("selected floor is 0")).IsTrue();
    }

    [TestCase]
    public void TestNonFloorSceneDoesNotAbortWhenSelectedIsAlsoNone()
    {
        // Edge case: if both resolve to -1 (no real floor selected), no mismatch.
        // In practice SelectedFloor is always a valid floor, but the guard should
        // not produce a spurious abort for equal inputs.
        AssertThat(FloorDockGuard.MismatchAbortMessage(-1, -1, ExportConsequence)).IsNull();
    }
}
