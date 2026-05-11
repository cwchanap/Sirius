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
        sceneRoot.Free();
    }
}
