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

    [TestCase]
    public void FromJson_ParsesPuzzleTrapEntities()
    {
        const string json = """
        {
          "schema_version": "1.0",
          "floor_metadata": {
            "floor_name": "First Floor",
            "floor_number": 1,
            "player_start": { "x": 8, "y": 50 }
          },
          "tile_layers": {},
          "entities": {
            "trap_tiles": [
              {
                "id": "TrapTile_1F_Burn",
                "puzzle_id": "Puzzle_1F_South",
                "position": { "x": 20, "y": 51 },
                "damage": 18,
                "status_effect": "burn",
                "status_magnitude": 4,
                "status_turns": 3
              }
            ],
            "puzzle_switches": [
              {
                "id": "PuzzleSwitch_1F_South",
                "puzzle_id": "Puzzle_1F_South",
                "position": { "x": 21, "y": 52 },
                "prompt_text": "Pull lever",
                "activated_text": "A gate opens."
              }
            ],
            "puzzle_gates": [
              {
                "id": "PuzzleGate_1F_South",
                "puzzle_id": "Puzzle_1F_South",
                "position": { "x": 22, "y": 53 },
                "starts_closed": true
              }
            ],
            "puzzle_riddles": [
              {
                "id": "PuzzleRiddle_1F_South",
                "puzzle_id": "Puzzle_1F_South",
                "position": { "x": 23, "y": 54 },
                "prompt_text": "Which path is safe?",
                "choices": [
                  { "id": "left", "label": "Left" },
                  { "id": "right", "label": "Right" }
                ],
                "correct_choice_id": "right",
                "wrong_answer_damage": 16
              }
            ]
          }
        }
        """;

        var model = FloorJsonModel.FromJson(json);

        AssertThat(model).IsNotNull();
        AssertThat(model!.Entities.TrapTiles!.Count).IsEqual(1);
        AssertThat(model.Entities.TrapTiles[0].Id).IsEqual("TrapTile_1F_Burn");
        AssertThat(model.Entities.TrapTiles[0].PuzzleId).IsEqual("Puzzle_1F_South");
        AssertThat(model.Entities.TrapTiles[0].Position.ToVector2I()).IsEqual(new Vector2I(20, 51));
        AssertThat(model.Entities.TrapTiles[0].Damage).IsEqual(18);
        AssertThat(model.Entities.TrapTiles[0].StatusEffect).IsEqual("burn");
        AssertThat(model.Entities.TrapTiles[0].StatusMagnitude).IsEqual(4);
        AssertThat(model.Entities.TrapTiles[0].StatusTurns).IsEqual(3);

        AssertThat(model.Entities.PuzzleSwitches!.Count).IsEqual(1);
        AssertThat(model.Entities.PuzzleSwitches[0].Id).IsEqual("PuzzleSwitch_1F_South");
        AssertThat(model.Entities.PuzzleSwitches[0].PuzzleId).IsEqual("Puzzle_1F_South");
        AssertThat(model.Entities.PuzzleSwitches[0].Position.ToVector2I()).IsEqual(new Vector2I(21, 52));
        AssertThat(model.Entities.PuzzleSwitches[0].PromptText).IsEqual("Pull lever");
        AssertThat(model.Entities.PuzzleSwitches[0].ActivatedText).IsEqual("A gate opens.");

        AssertThat(model.Entities.PuzzleGates!.Count).IsEqual(1);
        AssertThat(model.Entities.PuzzleGates[0].Id).IsEqual("PuzzleGate_1F_South");
        AssertThat(model.Entities.PuzzleGates[0].PuzzleId).IsEqual("Puzzle_1F_South");
        AssertThat(model.Entities.PuzzleGates[0].Position.ToVector2I()).IsEqual(new Vector2I(22, 53));
        AssertThat(model.Entities.PuzzleGates[0].StartsClosed).IsTrue();

        AssertThat(model.Entities.PuzzleRiddles!.Count).IsEqual(1);
        AssertThat(model.Entities.PuzzleRiddles[0].Id).IsEqual("PuzzleRiddle_1F_South");
        AssertThat(model.Entities.PuzzleRiddles[0].PuzzleId).IsEqual("Puzzle_1F_South");
        AssertThat(model.Entities.PuzzleRiddles[0].Position.ToVector2I()).IsEqual(new Vector2I(23, 54));
        AssertThat(model.Entities.PuzzleRiddles[0].PromptText).IsEqual("Which path is safe?");
        AssertThat(model.Entities.PuzzleRiddles[0].Choices.Count).IsEqual(2);
        AssertThat(model.Entities.PuzzleRiddles[0].Choices[0].Id).IsEqual("left");
        AssertThat(model.Entities.PuzzleRiddles[0].Choices[0].Label).IsEqual("Left");
        AssertThat(model.Entities.PuzzleRiddles[0].Choices[1].Id).IsEqual("right");
        AssertThat(model.Entities.PuzzleRiddles[0].Choices[1].Label).IsEqual("Right");
        AssertThat(model.Entities.PuzzleRiddles[0].CorrectChoiceId).IsEqual("right");
        AssertThat(model.Entities.PuzzleRiddles[0].WrongAnswerDamage).IsEqual(16);
    }
}
