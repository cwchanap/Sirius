using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;
using static GdUnit4.Assertions;
using FloorTileData = Sirius.TilemapJson.TileData;

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
    public void ImportToScene_SetsGridMapBoundsFromGroundFootprint()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new GridMap { Name = "GridMap", GridWidth = 160, GridHeight = 160 };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            TileLayers = new Dictionary<string, List<FloorTileData>>
            {
                ["ground"] =
                [
                    new FloorTileData(0, 0, "starting_area"),
                    new FloorTileData(59, 59, "starting_area")
                ]
            },
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();

        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.GridWidth).IsEqual(60);
        AssertThat(gridMap.GridHeight).IsEqual(60);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_ComputesGridDimsFromMinToMaxRange()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new GridMap { Name = "GridMap", GridWidth = 160, GridHeight = 160 };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        // Tiles span x:0-14 (width=15), y:0-7 (height=8) — GridMap indexes from origin
        var model = new FloorJsonModel
        {
            TileLayers = new Dictionary<string, List<FloorTileData>>
            {
                ["ground"] =
                [
                    new FloorTileData(5, 3, "starting_area"),
                    new FloorTileData(14, 7, "starting_area")
                ]
            },
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();

        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.GridWidth).IsEqual(15);
        AssertThat(gridMap.GridHeight).IsEqual(8);
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
    public void ImportToScene_AssignsOwnerToCreatedTreasureBoxNodes()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new GridMap { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TreasureBoxes =
                [
                    new TreasureBoxData
                    {
                        Id = "TreasureBox_Start",
                        Position = new Vector2IData(15, 47),
                        Gold = 125,
                        Items =
                        [
                            new TreasureBoxItemData { ItemId = "minor_potion", Quantity = 2 }
                        ]
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();

        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var box = gridMap.GetNode<TreasureBoxSpawn>("TreasureBox_Start");
        AssertThat(box.Owner).IsEqual(sceneRoot);
        AssertThat(box.GridPosition).IsEqual(new Vector2I(15, 47));
        AssertThat(box.RewardGold).IsEqual(125);
        AssertThat(box.RewardItemIds![0]).IsEqual("minor_potion");
        AssertThat(box.RewardItemQuantities![0]).IsEqual(2);
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
    public void ImportToScene_RemovesStaleTreasureBoxesSynchronously()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var staleBox = new TreasureBoxSpawn { Name = "TreasureBox_Stale", GridPosition = new Vector2I(5, 5) };
        gridMap.AddChild(staleBox);
        staleBox.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TreasureBoxes = []
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TreasureBox_Stale")).IsFalse();
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

    [TestCase]
    public void ImportToScene_CreatesMissingStairConnectionNode()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections =
                [
                    new StairConnectionData
                    {
                        Id = "1F_2F_A",
                        Position = new Vector2IData(49, 12),
                        Direction = "up",
                        TargetFloor = 2,
                        DestinationStairId = "2F_1F_A"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("1F_2F_A")).IsTrue();

        var stair = gridMap.GetNode<StairConnection>("1F_2F_A");
        AssertThat(stair.Owner).IsEqual(sceneRoot);
        AssertThat(stair.StairId).IsEqual("1F_2F_A");
        AssertThat(stair.GridPosition).IsEqual(new Vector2I(49, 12));
        AssertThat(stair.Direction).IsEqual(StairDirection.Up);
        AssertThat(stair.TargetFloor).IsEqual(2);
        AssertThat(stair.DestinationStairId).IsEqual("2F_1F_A");
        AssertThat(stair.Position).IsEqual(new Vector2(1584, 400));

        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_CreatesGenericEnemySpawn_WhenDedicatedSceneIsMissing()
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
                        Id = "EnemySpawn_Skeleton_StairA",
                        Position = new Vector2IData(43, 12),
                        EnemyType = "skeleton_warrior"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("EnemySpawn_Skeleton_StairA")).IsTrue();

        var spawn = gridMap.GetNode<EnemySpawn>("EnemySpawn_Skeleton_StairA");
        AssertThat(spawn.Owner).IsEqual(sceneRoot);
        AssertThat(spawn.GridPosition).IsEqual(new Vector2I(43, 12));
        AssertThat(spawn.EnemyType).IsEqual("skeleton_warrior");
        AssertThat(spawn.Position).IsEqual(new Vector2(1392, 400));

        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_StairDirectionDefaultsToUpOnInvalidValue()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections =
                [
                    new StairConnectionData
                    {
                        Id = "Stair_BadDir",
                        Position = new Vector2IData(5, 5),
                        Direction = "sideways",
                        TargetFloor = 1,
                        DestinationStairId = "dest"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var stair = gridMap.GetNode<StairConnection>("Stair_BadDir");
        AssertThat(stair.Direction).IsEqual(StairDirection.Up);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_StairDirectionParsesUpAndDown()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                StairConnections =
                [
                    new StairConnectionData
                    {
                        Id = "Stair_Up",
                        Position = new Vector2IData(1, 1),
                        Direction = "up",
                        TargetFloor = 2,
                        DestinationStairId = "dest_up"
                    },
                    new StairConnectionData
                    {
                        Id = "Stair_Down",
                        Position = new Vector2IData(2, 2),
                        Direction = "down",
                        TargetFloor = 0,
                        DestinationStairId = "dest_down"
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var upStair = gridMap.GetNode<StairConnection>("Stair_Up");
        AssertThat(upStair.Direction).IsEqual(StairDirection.Up);
        var downStair = gridMap.GetNode<StairConnection>("Stair_Down");
        AssertThat(downStair.Direction).IsEqual(StairDirection.Down);
        sceneRoot.Free();
    }
}
