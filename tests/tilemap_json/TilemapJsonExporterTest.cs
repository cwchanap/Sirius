using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TilemapJsonExporterTest : Node
{
    [TestCase]
    public void ExportScene_IncludesTreasureBoxes()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        try
        {
            var gridMap = new Node2D { Name = "GridMap" };
            sceneRoot.AddChild(gridMap);
            gridMap.Owner = sceneRoot;

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_NodeName",
                TreasureBoxId = "TreasureBox_Start",
                GridPosition = new Vector2I(15, 47),
                RewardGold = 125,
                RewardItemIds = ["minor_potion"],
                RewardItemQuantities = [2]
            };
            gridMap.AddChild(box);
            box.Owner = sceneRoot;

            var exporter = new TilemapJsonExporter();

            var model = exporter.ExportScene(gridMap);

            AssertThat(model).IsNotNull();
            AssertThat(model!.Entities.TreasureBoxes!.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Id).IsEqual("TreasureBox_Start");
            AssertThat(model.Entities.TreasureBoxes[0].Position.ToVector2I()).IsEqual(new Vector2I(15, 47));
            AssertThat(model.Entities.TreasureBoxes[0].Gold).IsEqual(125);
            AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("minor_potion");
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].Quantity).IsEqual(2);
        }
        finally
        {
            sceneRoot.Free();
        }
    }

    [TestCase]
    public void ExportScene_DefaultsTreasureBoxItemQuantityWhenQuantitiesAreNull()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        try
        {
            var gridMap = new Node2D { Name = "GridMap" };
            sceneRoot.AddChild(gridMap);
            gridMap.Owner = sceneRoot;

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_NodeFallback",
                TreasureBoxId = "",
                GridPosition = new Vector2I(3, 4),
                RewardItemIds = ["health_potion"],
                RewardItemQuantities = null
            };
            gridMap.AddChild(box);
            box.Owner = sceneRoot;

            var exporter = new TilemapJsonExporter();

            var model = exporter.ExportScene(gridMap);

            AssertThat(model).IsNotNull();
            AssertThat(model!.Entities.TreasureBoxes!.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Id).IsEqual("TreasureBox_NodeFallback");
            AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("health_potion");
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].Quantity).IsEqual(1);
        }
        finally
        {
            sceneRoot.Free();
        }
    }

    [TestCase]
    public void ExportScene_ExportsNoTreasureBoxItemsWhenItemIdsAreNull()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        try
        {
            var gridMap = new Node2D { Name = "GridMap" };
            sceneRoot.AddChild(gridMap);
            gridMap.Owner = sceneRoot;

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_EmptyItems",
                TreasureBoxId = "TreasureBox_EmptyItems",
                RewardItemIds = null,
                RewardItemQuantities = [3]
            };
            gridMap.AddChild(box);
            box.Owner = sceneRoot;

            var exporter = new TilemapJsonExporter();

            var model = exporter.ExportScene(gridMap);

            AssertThat(model).IsNotNull();
            AssertThat(model!.Entities.TreasureBoxes!.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(0);
        }
        finally
        {
            sceneRoot.Free();
        }
    }

    [TestCase]
    public void ExportScene_DefaultsTreasureBoxItemQuantityWhenQuantityIndexIsMissing()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        try
        {
            var gridMap = new Node2D { Name = "GridMap" };
            sceneRoot.AddChild(gridMap);
            gridMap.Owner = sceneRoot;

            var box = new TreasureBoxSpawn
            {
                Name = "TreasureBox_MissingQuantity",
                TreasureBoxId = "TreasureBox_MissingQuantity",
                RewardItemIds = ["health_potion", "mana_potion"],
                RewardItemQuantities = [4]
            };
            gridMap.AddChild(box);
            box.Owner = sceneRoot;

            var exporter = new TilemapJsonExporter();

            var model = exporter.ExportScene(gridMap);

            AssertThat(model).IsNotNull();
            AssertThat(model!.Entities.TreasureBoxes!.Count).IsEqual(1);
            AssertThat(model.Entities.TreasureBoxes[0].Items.Count).IsEqual(2);
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].ItemId).IsEqual("health_potion");
            AssertThat(model.Entities.TreasureBoxes[0].Items[0].Quantity).IsEqual(4);
            AssertThat(model.Entities.TreasureBoxes[0].Items[1].ItemId).IsEqual("mana_potion");
            AssertThat(model.Entities.TreasureBoxes[0].Items[1].Quantity).IsEqual(1);
        }
        finally
        {
            sceneRoot.Free();
        }
    }

    [TestCase]
    public void ExportScene_IncludesPuzzleTrapEntities()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        try
        {
            var gridMap = new Node2D { Name = "GridMap" };
            sceneRoot.AddChild(gridMap);
            gridMap.Owner = sceneRoot;

            var trap = new TrapTileSpawn
            {
                Name = "TrapTile_Test",
                PuzzleId = "Puzzle_Test",
                GridPosition = new Vector2I(3, 4),
                Damage = 18,
                StatusEffectId = "poison",
                StatusMagnitude = 2,
                StatusTurns = 3
            };
            var puzzleSwitch = new PuzzleSwitchSpawn
            {
                Name = "SwitchNode",
                SwitchId = "PuzzleSwitch_Test",
                PuzzleId = "Puzzle_Test",
                GridPosition = new Vector2I(4, 5),
                PromptText = "Pull",
                ActivatedText = "Opened"
            };
            var gate = new PuzzleGateSpawn
            {
                Name = "GateNode",
                GateId = "PuzzleGate_Test",
                PuzzleId = "Puzzle_Test",
                GridPosition = new Vector2I(5, 6),
                StartsClosed = true
            };
            var riddle = new PuzzleRiddleSpawn
            {
                Name = "RiddleNode",
                RiddleId = "PuzzleRiddle_Test",
                PuzzleId = "Puzzle_Test",
                GridPosition = new Vector2I(6, 7),
                PromptText = "Choose",
                ChoiceIds = ["a", "b"],
                ChoiceLabels = ["Alpha", "Beta"],
                CorrectChoiceId = "b",
                WrongAnswerDamage = 9
            };
            gridMap.AddChild(trap);
            gridMap.AddChild(puzzleSwitch);
            gridMap.AddChild(gate);
            gridMap.AddChild(riddle);
            trap.Owner = sceneRoot;
            puzzleSwitch.Owner = sceneRoot;
            gate.Owner = sceneRoot;
            riddle.Owner = sceneRoot;

            var exporter = new TilemapJsonExporter();

            var model = exporter.ExportScene(gridMap);

            AssertThat(model).IsNotNull();
            AssertThat(model!.Entities.TrapTiles!.Count).IsEqual(1);
            AssertThat(model.Entities.TrapTiles[0].Id).IsEqual("TrapTile_Test");
            AssertThat(model.Entities.TrapTiles[0].PuzzleId).IsEqual("Puzzle_Test");
            AssertThat(model.Entities.TrapTiles[0].Position.ToVector2I()).IsEqual(new Vector2I(3, 4));
            AssertThat(model.Entities.TrapTiles[0].Damage).IsEqual(18);
            AssertThat(model.Entities.TrapTiles[0].StatusEffect).IsEqual("poison");
            AssertThat(model.Entities.TrapTiles[0].StatusMagnitude).IsEqual(2);
            AssertThat(model.Entities.TrapTiles[0].StatusTurns).IsEqual(3);

            AssertThat(model.Entities.PuzzleSwitches!.Count).IsEqual(1);
            AssertThat(model.Entities.PuzzleSwitches[0].Id).IsEqual("PuzzleSwitch_Test");
            AssertThat(model.Entities.PuzzleSwitches[0].PuzzleId).IsEqual("Puzzle_Test");
            AssertThat(model.Entities.PuzzleSwitches[0].Position.ToVector2I()).IsEqual(new Vector2I(4, 5));
            AssertThat(model.Entities.PuzzleSwitches[0].PromptText).IsEqual("Pull");
            AssertThat(model.Entities.PuzzleSwitches[0].ActivatedText).IsEqual("Opened");

            AssertThat(model.Entities.PuzzleGates!.Count).IsEqual(1);
            AssertThat(model.Entities.PuzzleGates[0].Id).IsEqual("PuzzleGate_Test");
            AssertThat(model.Entities.PuzzleGates[0].PuzzleId).IsEqual("Puzzle_Test");
            AssertThat(model.Entities.PuzzleGates[0].Position.ToVector2I()).IsEqual(new Vector2I(5, 6));
            AssertThat(model.Entities.PuzzleGates[0].StartsClosed).IsTrue();

            AssertThat(model.Entities.PuzzleRiddles!.Count).IsEqual(1);
            AssertThat(model.Entities.PuzzleRiddles[0].Id).IsEqual("PuzzleRiddle_Test");
            AssertThat(model.Entities.PuzzleRiddles[0].PuzzleId).IsEqual("Puzzle_Test");
            AssertThat(model.Entities.PuzzleRiddles[0].Position.ToVector2I()).IsEqual(new Vector2I(6, 7));
            AssertThat(model.Entities.PuzzleRiddles[0].PromptText).IsEqual("Choose");
            AssertThat(model.Entities.PuzzleRiddles[0].Choices.Count).IsEqual(2);
            AssertThat(model.Entities.PuzzleRiddles[0].Choices[0].Id).IsEqual("a");
            AssertThat(model.Entities.PuzzleRiddles[0].Choices[0].Label).IsEqual("Alpha");
            AssertThat(model.Entities.PuzzleRiddles[0].Choices[1].Id).IsEqual("b");
            AssertThat(model.Entities.PuzzleRiddles[0].Choices[1].Label).IsEqual("Beta");
            AssertThat(model.Entities.PuzzleRiddles[0].CorrectChoiceId).IsEqual("b");
            AssertThat(model.Entities.PuzzleRiddles[0].WrongAnswerDamage).IsEqual(9);
        }
        finally
        {
            sceneRoot.Free();
        }
    }
}
