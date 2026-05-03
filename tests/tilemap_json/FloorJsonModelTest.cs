using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class FloorJsonModelTest : Node
{
    [TestCase]
    public void FromJson_ParsesNpcSpawns()
    {
        const string json = """
        {
          "schema_version": "1.0",
          "floor_metadata": {
            "floor_name": "Ground Floor",
            "floor_number": 0,
            "player_start": { "x": 8, "y": 50 }
          },
          "tile_layers": {},
          "entities": {
            "npc_spawns": [
              {
                "id": "NpcSpawn_Shopkeeper",
                "position": { "x": 12, "y": 46 },
                "npc_id": "village_shopkeeper"
              }
            ]
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.NpcSpawns!.Count).IsEqual(1);
        AssertThat(model.Entities.NpcSpawns[0].Id).IsEqual("NpcSpawn_Shopkeeper");
        AssertThat(model.Entities.NpcSpawns[0].NpcId).IsEqual("village_shopkeeper");
        AssertThat(model.Entities.NpcSpawns[0].Position.ToVector2I().X).IsEqual(12);
        AssertThat(model.Entities.NpcSpawns[0].Position.ToVector2I().Y).IsEqual(46);
    }

    [TestCase]
    public void FromJson_NpcSpawnsAbsent_DeserializesToNull()
    {
        const string json = """
        {
          "schema_version": "1.0",
          "floor_metadata": {
            "floor_name": "Ground Floor",
            "floor_number": 0,
            "player_start": { "x": 8, "y": 50 }
          },
          "tile_layers": {},
          "entities": {
            "enemy_spawns": []
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.NpcSpawns).IsNull();
    }

    [TestCase]
    public void FromJson_NpcSpawnsEmptyArray_DeserializesToEmptyList()
    {
        const string json = """
        {
          "schema_version": "1.0",
          "floor_metadata": {
            "floor_name": "Ground Floor",
            "floor_number": 0,
            "player_start": { "x": 8, "y": 50 }
          },
          "tile_layers": {},
          "entities": {
            "npc_spawns": []
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.NpcSpawns).IsNotNull();
        AssertThat(model.Entities.NpcSpawns!.Count).IsEqual(0);
    }
}
