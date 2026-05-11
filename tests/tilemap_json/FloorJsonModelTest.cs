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

    [TestCase]
    public void FromJson_ParsesTreasureBoxes()
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
            "treasure_boxes": [
              {
                "id": "TreasureBox_Start",
                "position": { "x": 15, "y": 47 },
                "gold": 125,
                "items": [
                  { "item_id": "minor_potion", "quantity": 2 },
                  { "item_id": "rusty_key", "quantity": 1 }
                ]
              }
            ]
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.TreasureBoxes!.Count).IsEqual(1);
        AssertThat(model.Entities.TreasureBoxes[0].Id).IsEqual("TreasureBox_Start");
        AssertThat(model.Entities.TreasureBoxes[0].Position.ToVector2I()).IsEqual(new Vector2I(15, 47));
        AssertThat(model.Entities.TreasureBoxes[0].Gold).IsEqual(125);
        AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(2);
        AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("minor_potion");
        AssertThat(model.Entities.TreasureBoxes[0].Items[0].Quantity).IsEqual(2);
        AssertThat(model.Entities.TreasureBoxes[0].Items[1].ItemId).IsEqual("rusty_key");
        AssertThat(model.Entities.TreasureBoxes[0].Items[1].Quantity).IsEqual(1);
    }
}
