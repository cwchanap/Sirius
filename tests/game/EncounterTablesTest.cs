using GdUnit4;
using Godot;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class EncounterTablesTest : Node
{
    [TestCase]
    public void CreateEnemyByType_CreatesNewDungeonEnemies()
    {
        string[] types =
        {
            EnemyTypeId.CryptSentinel,
            EnemyTypeId.GraveHexer,
            EnemyTypeId.BoneArcher,
            EnemyTypeId.IronRevenant,
            EnemyTypeId.CursedGargoyle,
            EnemyTypeId.AbyssAcolyte,
        };

        foreach (var type in types)
        {
            var enemy = EncounterTables.CreateEnemyByType(type);
            AssertThat(enemy).IsNotNull();
            AssertThat(enemy!.EnemyType).IsEqual(type);
        }
    }

    [TestCase]
    public void CreateEnemyByType_ReturnsNullForUnknownType()
    {
        AssertThat(EncounterTables.CreateEnemyByType("unknown_dungeon_enemy")).IsNull();
        AssertThat(EncounterTables.CreateEnemyByType(null)).IsNull();
        AssertThat(EncounterTables.CreateEnemyByType("")).IsNull();
    }

    [TestCase]
    public void SelectDungeonEnemyType_CoversAllNewDungeonEnemies()
    {
        var selected = new[]
        {
            EncounterTables.SelectDungeonEnemyType(0.05f),
            EncounterTables.SelectDungeonEnemyType(0.18f),
            EncounterTables.SelectDungeonEnemyType(0.31f),
            EncounterTables.SelectDungeonEnemyType(0.48f),
            EncounterTables.SelectDungeonEnemyType(0.64f),
            EncounterTables.SelectDungeonEnemyType(0.80f),
            EncounterTables.SelectDungeonEnemyType(0.94f),
        };

        AssertThat(selected.Contains(EnemyTypeId.DungeonGuardian)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.CryptSentinel)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.GraveHexer)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.BoneArcher)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.IronRevenant)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.CursedGargoyle)).IsTrue();
        AssertThat(selected.Contains(EnemyTypeId.AbyssAcolyte)).IsTrue();
    }
}
