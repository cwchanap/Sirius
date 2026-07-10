using GdUnit4;
using Sirius.FloorTools.Addon;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

// Tests the baseline-merge helper used by SiriusFloorToolsDock.Export JSON.
// The dock is editor-coupled (EditorInterface.Singleton) and cannot run under
// GdUnit4, but the merge logic lives in the pure FloorExportMerge helper.
// These tests lock down the preservation of JSON-only fields
// (hidden_placeholders) and generator-authored metadata (floor_name /
// description) so a scene export of a generated baseline (Floor1F/2F/3F) does
// not drop hidden rooms or rewrite the generator's description with the
// .tres's hand-authored text — which would drift the committed JSON / parity
// tests even when the scene edit was unrelated.
[TestSuite]
public partial class FloorExportMergeTest
{
    [TestCase]
    public void MergeBaseline_PreservesHiddenPlaceholdersFromBaseline()
    {
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata { FloorName = "from tres", Description = "from tres" },
            Entities = new SceneEntities()
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities
            {
                HiddenPlaceholders = new()
                {
                    new HiddenPlaceholderData { Id = "hidden_room_north", Position = new Vector2IData(16, 8) },
                    new HiddenPlaceholderData { Id = "hidden_shortcut_east", Position = new Vector2IData(56, 30) }
                }
            }
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Entities.HiddenPlaceholders).IsNotNull();
        AssertThat(exported.Entities.HiddenPlaceholders!.Count).IsEqual(2);
        AssertThat(exported.Entities.HiddenPlaceholders[0].Id).IsEqual("hidden_room_north");
        AssertThat(exported.Entities.HiddenPlaceholders[1].Id).IsEqual("hidden_shortcut_east");
    }

    [TestCase]
    public void MergeBaseline_PreservesGeneratorAuthoredDescriptionOverTresText()
    {
        // The exporter fills floor_name/description from the .tres. The baseline
        // carries the generator's text. The merge must keep the baseline's text
        // so a scene export does not rewrite the committed description.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata
            {
                FloorName = "First Floor",
                Description = "The first floor above ground" // .tres text
            },
            Entities = new SceneEntities()
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata
            {
                FloorName = "First Floor",
                Description = "A compact combat-gated loop maze with two 2/F routes." // generator text
            },
            Entities = new SceneEntities()
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Metadata.Description)
            .IsEqual("A compact combat-gated loop maze with two 2/F routes.");
    }

    [TestCase]
    public void MergeBaseline_NullBaselineLeavesExportedUnchanged()
    {
        // First export of a brand-new floor: no baseline file exists. The merge
        // must keep the exporter's seeded (.tres) values as the seed baseline.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata { FloorName = "New Floor", Description = "seed" },
            Entities = new SceneEntities()
        };

        FloorExportMerge.MergeBaseline(exported, null);

        AssertThat(exported.Metadata.FloorName).IsEqual("New Floor");
        AssertThat(exported.Metadata.Description).IsEqual("seed");
        AssertThat(exported.Entities.HiddenPlaceholders).IsNull();
    }

    [TestCase]
    public void MergeBaseline_EmptyBaselineMetadataKeepsExportedValues()
    {
        // A baseline whose metadata strings are empty (e.g. a hand-created stub)
        // must not blank out the exporter's seeded values.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata { FloorName = "from tres", Description = "from tres" },
            Entities = new SceneEntities()
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata { FloorName = "", Description = "" },
            Entities = new SceneEntities()
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Metadata.FloorName).IsEqual("from tres");
        AssertThat(exported.Metadata.Description).IsEqual("from tres");
    }

    [TestCase]
    public void MergeBaseline_NullBaselineEntitiesDoesNotCreateHiddenPlaceholders()
    {
        // A baseline with no entities block must not synthesize an empty
        // hidden_placeholders list on the exported model.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities()
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = null
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Entities.HiddenPlaceholders).IsNull();
    }

    [TestCase]
    public void MergeBaseline_InitializesEntitiesWhenExportedHasNone()
    {
        // Defensive: if the exporter somehow produced a model with null Entities,
        // the merge must still be able to attach the baseline's hidden rooms.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = null
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities
            {
                HiddenPlaceholders = new() { new HiddenPlaceholderData { Id = "hp1" } }
            }
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Entities).IsNotNull();
        AssertThat(exported.Entities!.HiddenPlaceholders!.Count).IsEqual(1);
    }

    [TestCase]
    public void MergeBaseline_StripsBlueprintAndStatsFromEnemySpawns()
    {
        // TilemapJsonExporter populates Blueprint/Stats from EnemySpawn node
        // Blueprint resources, but FloorGenerationService emits neither. Without
        // stripping, exporting a generated floor (GF/1F) with blueprint-backed
        // enemies injects these fields into the committed baseline, causing
        // FloorGenerationParityTest to fail despite no gameplay-layout change.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities
            {
                EnemySpawns = new()
                {
                    new EnemySpawnData
                    {
                        Id = "EnemySpawn_1",
                        Position = new Vector2IData(10, 20),
                        EnemyType = "Goblin",
                        Blueprint = "res://resources/enemies/goblin.tres",
                        Stats = new EnemyStatsData { Level = 3, MaxHealth = 50, Attack = 12 }
                    },
                    new EnemySpawnData
                    {
                        Id = "EnemySpawn_2",
                        Position = new Vector2IData(30, 40),
                        EnemyType = "Slime",
                        Blueprint = "res://resources/enemies/slime.tres",
                        Stats = new EnemyStatsData { Level = 1, MaxHealth = 30, Attack = 8 }
                    }
                }
            }
        };
        var baseline = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities()
        };

        FloorExportMerge.MergeBaseline(exported, baseline);

        AssertThat(exported.Entities.EnemySpawns!.Count).IsEqual(2);
        foreach (var spawn in exported.Entities.EnemySpawns!)
        {
            AssertThat(spawn.Blueprint).IsNull();
            AssertThat(spawn.Stats).IsNull();
        }
    }

    [TestCase]
    public void MergeBaseline_StripsBlueprintAndStatsEvenWithNullBaseline()
    {
        // Stripping must happen regardless of whether a baseline exists — the
        // first export of a brand-new floor also must not seed blueprint/stats
        // into the baseline.
        var exported = new FloorJsonModel
        {
            Metadata = new FloorMetadata(),
            Entities = new SceneEntities
            {
                EnemySpawns = new()
                {
                    new EnemySpawnData
                    {
                        Id = "EnemySpawn_1",
                        EnemyType = "Goblin",
                        Blueprint = "res://resources/enemies/goblin.tres",
                        Stats = new EnemyStatsData { Level = 3 }
                    }
                }
            }
        };

        FloorExportMerge.MergeBaseline(exported, null);

        AssertThat(exported.Entities.EnemySpawns![0].Blueprint).IsNull();
        AssertThat(exported.Entities.EnemySpawns![0].Stats).IsNull();
    }
}
