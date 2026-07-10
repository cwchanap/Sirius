using Sirius.TilemapJson;

namespace Sirius.FloorTools.Addon;

/// <summary>
/// Merges JSON-only fields from the existing baseline into a freshly
/// scene-exported <see cref="FloorJsonModel"/> so the dock's Export JSON
/// does not drop metadata that has no scene representation or clobber
/// generator-authored presentation text with the .tres values the exporter
/// filled in. Pure (no editor API) so it is unit-testable alongside
/// <see cref="FloorDockGuard"/>.
/// </summary>
public static class FloorExportMerge
{
    /// <summary>
    /// Copy forward baseline-only fields into <paramref name="exported"/>.
    /// Fields preserved from <paramref name="baseline"/>:
    /// <list type="bullet">
    /// <item><description><c>floor_name</c> / <c>description</c> — the exporter
    /// fills these from the FloorDefinition (.tres), which is intentionally kept
    /// hand-authored (<c>FloorResourceSyncService</c> defaults
    /// <c>SyncMetadata=false</c>). The committed JSON baseline instead carries
    /// the generator's presentation text, so a scene export must not rewrite it
    /// with the .tres text or the parity tests drift.</description></item>
    /// <item><description><c>hidden_placeholders</c> — a JSON-only entity field
    /// with no scene representation; <c>TilemapJsonExporter</c> never populates
    /// it, so without this merge a scene export would erase the generator's
    /// hidden-room markers (e.g. Floor1F's hidden_room_north /
    /// hidden_shortcut_east).</description></item>
    /// </list>
    /// When <paramref name="baseline"/> is null (no existing file, e.g. the
    /// first export of a brand-new floor), <paramref name="exported"/> is left
    /// unchanged so the .tres values the exporter filled become the seed.
    /// </summary>
    public static void MergeBaseline(FloorJsonModel exported, FloorJsonModel baseline)
    {
        // Strip scene-only enemy enrichment (blueprint/stats) that the exporter
        // populates from EnemySpawn node Blueprint resources but
        // FloorGenerationService does not emit. Without this, exporting a
        // generated floor (GF/1F) whose scene has blueprint-backed enemies
        // injects blueprint/stats into the committed baseline, causing
        // FloorGenerationParityTest to fail despite no gameplay-layout change.
        // Runs unconditionally (even when baseline is null) so the first export
        // of a brand-new floor also does not seed these fields.
        if (exported.Entities?.EnemySpawns != null)
        {
            foreach (var spawn in exported.Entities.EnemySpawns)
            {
                spawn.Blueprint = null;
                spawn.Stats = null;
            }
        }

        if (baseline == null) return;

        // Preserve generator-authored presentation text. Only overwrite when
        // the baseline actually carries a value, so a brand-new floor whose
        // baseline is empty keeps the .tres text the exporter filled in.
        if (!string.IsNullOrEmpty(baseline.Metadata.FloorName))
            exported.Metadata.FloorName = baseline.Metadata.FloorName;
        if (!string.IsNullOrEmpty(baseline.Metadata.Description))
            exported.Metadata.Description = baseline.Metadata.Description;

        // hidden_placeholders has no scene representation; carry the baseline
        // forward so a scene edit does not erase the generator's hidden rooms.
        // A null baseline.Entities means the baseline file had no entities
        // block — nothing to carry forward.
        if (baseline.Entities?.HiddenPlaceholders != null)
        {
            exported.Entities ??= new SceneEntities();
            exported.Entities.HiddenPlaceholders = baseline.Entities.HiddenPlaceholders;
        }
    }
}
