namespace Sirius.FloorTools.Addon;

/// <summary>
/// Pure, editor-API-free guard logic extracted from <see cref="SiriusFloorToolsDock"/>
/// so the cross-floor mismatch safety checks are unit-testable without an editor
/// runtime. The dock calls <see cref="MismatchAbortMessage"/> before Export/Import
/// and aborts (logging the returned message) when the open scene's floor does not
/// match the selected floor.
/// </summary>
public static class FloorDockGuard
{
    /// <summary>
    /// Returns null when the open scene's floor matches the selected floor (safe to
    /// proceed). Returns a formatted abort message (prefixed "[x]") when they differ,
    /// describing the open scene and the consequence string supplied by the caller
    /// (e.g. "Export would write the wrong grid to ..."). <paramref name="openFloor"/>
    /// of -1 means the open scene is not a registered floor.
    /// </summary>
    public static string? MismatchAbortMessage(int openFloor, int selectedFloor, string consequence)
    {
        if (openFloor == selectedFloor)
            return null;
        string openDesc = openFloor == -1 ? "not a floor" : $"floor {openFloor}";
        return $"[x] Aborted: open scene is {openDesc} but selected floor is {selectedFloor}. {consequence}";
    }
}
