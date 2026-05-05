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
    public void ImportToScene_PreservesExistingNpcsWhenNpcSpawnsAbsent()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing NPC spawn node
        var existingNpc = new NpcSpawn { Name = "NpcSpawn_Existing", GridPosition = new Vector2I(10, 10), NpcId = "village_merchant" };
        gridMap.AddChild(existingNpc);
        existingNpc.Owner = sceneRoot;

        // Import with NpcSpawns = null (field absent in JSON) — should NOT remove existing NPCs
        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                // NpcSpawns deliberately left null (simulates absent JSON field)
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("NpcSpawn_Existing")).IsTrue();
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

    [TestCase]
    public void ImportToScene_ReturnsInvalidParameterOnNullModel()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(null, gridMap);

        AssertThat(err).IsEqual(Godot.Error.InvalidParameter);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_ReturnsInvalidParameterOnNullGridMapNode()
    {
        var model = new FloorJsonModel
        {
            TileLayers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Sirius.TilemapJson.TileData>>(),
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, null);

        AssertThat(err).IsEqual(Godot.Error.InvalidParameter);
    }

    [TestCase]
    public void ImportToScene_ReturnsInvalidParameterOnNullTileLayers()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);

        var model = new FloorJsonModel
        {
            TileLayers = null,
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.InvalidParameter);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_ReturnsInvalidParameterOnNullEntities()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);

        var model = new FloorJsonModel
        {
            TileLayers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Sirius.TilemapJson.TileData>>(),
            Entities = null
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.InvalidParameter);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_CentersCreatedEnemySpawnPosition()
    {
        // ToCenteredCellPosition(x,y) = (x*32 + 16, y*32 + 16)
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
                        Id = "EnemySpawn_PosTest",
                        Position = new Vector2IData(10, 20),
                        EnemyType = "Goblin"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var node = gridMap.GetNode<Node2D>("EnemySpawn_PosTest");
        // 10*32+16=336, 20*32+16=656
        AssertThat(node.Position.X).IsEqual(336.0f);
        AssertThat(node.Position.Y).IsEqual(656.0f);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_CentersCreatedNpcSpawnPosition()
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
                        Id = "NpcSpawn_PosTest",
                        Position = new Vector2IData(5, 7),
                        NpcId = "test_npc"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var node = gridMap.GetNode<Node2D>("NpcSpawn_PosTest");
        // 5*32+16=176, 7*32+16=240
        AssertThat(node.Position.X).IsEqual(176.0f);
        AssertThat(node.Position.Y).IsEqual(240.0f);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_UpdatesExistingNpcSpawnPosition()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing NPC
        var existingNpc = new NpcSpawn { Name = "NpcSpawn_UpdateTest", GridPosition = new Vector2I(1, 1), NpcId = "old_npc" };
        gridMap.AddChild(existingNpc);
        existingNpc.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                NpcSpawns =
                [
                    new NpcSpawnData
                    {
                        Id = "NpcSpawn_UpdateTest",
                        Position = new Vector2IData(30, 40),
                        NpcId = "updated_npc"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var node = gridMap.GetNode<NpcSpawn>("NpcSpawn_UpdateTest");
        AssertThat(node.NpcId).IsEqual("updated_npc");
        AssertThat(node.GridPosition).IsEqual(new Vector2I(30, 40));
        // 30*32+16=976, 40*32+16=1296
        AssertThat(node.Position.X).IsEqual(976.0f);
        AssertThat(node.Position.Y).IsEqual(1296.0f);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_UpdatesExistingStairConnectionNode()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Pre-populate with an existing stair
        var existingStair = new StairConnection
        {
            Name = "StairUpdateTest",
            StairId = "stair_update",
            GridPosition = new Vector2I(5, 5),
            TargetFloor = 1,
            Direction = StairDirection.Up
        };
        gridMap.AddChild(existingStair);
        existingStair.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections =
                [
                    new StairConnectionData
                    {
                        Id = "stair_update",
                        Position = new Vector2IData(50, 60),
                        Direction = "down",
                        TargetFloor = 0,
                        DestinationStairId = "dest_stair"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("StairUpdateTest")).IsTrue();
        var node = gridMap.GetNode<StairConnection>("StairUpdateTest");
        AssertThat(node.GridPosition).IsEqual(new Vector2I(50, 60));
        AssertThat(node.TargetFloor).IsEqual(0);
        AssertThat(node.DestinationStairId).IsEqual("dest_stair");
        // 50*32+16=1616, 60*32+16=1936
        AssertThat(node.Position.X).IsEqual(1616.0f);
        AssertThat(node.Position.Y).IsEqual(1936.0f);
        sceneRoot.Free();
    }
}
