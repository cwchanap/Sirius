using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TilemapJsonImporterTest : Node
{
    [TestCase]
    public void ImportToScene_AssignsOwnerToCreatedEnemySpawnNodes()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                EnemySpawns =
                [
                    new EnemySpawnData
                    {
                        Id = "EnemySpawn_Goblin_Test",
                        Position = new Vector2IData(24, 45),
                        EnemyType = "Goblin"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();

        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.GetNode<Node>("EnemySpawn_Goblin_Test").Owner).IsEqual(sceneRoot);
        sceneRoot.Free();
    }
}
