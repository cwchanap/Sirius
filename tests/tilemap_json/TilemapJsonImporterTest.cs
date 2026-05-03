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

    [TestCase]
    public void ImportToScene_AssignsOwnerToCreatedNpcSpawnNodes()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                NpcSpawns =
                [
                    new NpcSpawnData
                    {
                        Id = "NpcSpawn_Shopkeeper_Test",
                        Position = new Vector2IData(12, 46),
                        NpcId = "village_shopkeeper"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();

        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var npcNode = gridMap.GetNode<Node>("NpcSpawn_Shopkeeper_Test");
        AssertThat(npcNode).IsNotNull();
        AssertThat(npcNode.Owner).IsEqual(sceneRoot);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_RemovesStaleEnemySpawnsSynchronously()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing enemy spawn node
        var staleSpawn = new Node2D { Name = "EnemySpawn_Stale" };
        staleSpawn.Set("GridPosition", new Vector2I(10, 10));
        gridMap.AddChild(staleSpawn);
        staleSpawn.Owner = sceneRoot;

        // Import with empty enemy spawns — should remove the stale one
        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                EnemySpawns = []
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        // Node must be gone immediately (synchronous Free), not queued
        AssertThat(gridMap.HasNode("EnemySpawn_Stale")).IsFalse();
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_RemovesStaleNpcSpawnsSynchronously()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing NPC spawn node
        var staleNpc = new NpcSpawn { Name = "NpcSpawn_Stale", GridPosition = new Vector2I(5, 5) };
        gridMap.AddChild(staleNpc);
        staleNpc.Owner = sceneRoot;

        // Import with empty NPC spawns — should remove the stale one
        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                NpcSpawns = []
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("NpcSpawn_Stale")).IsFalse();
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_RemovesStaleStairConnectionsSynchronously()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing stair connection node
        var staleStair = new StairConnection { Name = "StairConnection_Stale", StairId = "stale_stair", GridPosition = new Vector2I(3, 3) };
        gridMap.AddChild(staleStair);
        staleStair.Owner = sceneRoot;

        // Import with empty stair connections — should remove the stale one
        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections = []
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("StairConnection_Stale")).IsFalse();
        sceneRoot.Free();
    }
}
