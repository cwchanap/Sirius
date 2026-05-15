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
        AssertThat(box.Position).IsEqual(new Vector2(496, 1520));
        AssertThat(box.ZIndex).IsEqual(2);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_AssignsOwnerToCreatedPuzzleNodes()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TrapTiles =
                [
                    new TrapTileData
                    {
                        Id = "TrapTile_Test",
                        PuzzleId = "Puzzle_Test",
                        Position = new Vector2IData(3, 4),
                        Damage = 18,
                        StatusEffect = "poison",
                        StatusMagnitude = 2,
                        StatusTurns = 3
                    }
                ],
                PuzzleSwitches =
                [
                    new PuzzleSwitchData
                    {
                        Id = "PuzzleSwitch_Test",
                        PuzzleId = "Puzzle_Test",
                        Position = new Vector2IData(4, 5),
                        PromptText = "Pull",
                        ActivatedText = "Opened"
                    }
                ],
                PuzzleGates =
                [
                    new PuzzleGateData
                    {
                        Id = "PuzzleGate_Test",
                        PuzzleId = "Puzzle_Test",
                        Position = new Vector2IData(5, 6),
                        StartsClosed = true
                    }
                ],
                PuzzleRiddles =
                [
                    new PuzzleRiddleData
                    {
                        Id = "PuzzleRiddle_Test",
                        PuzzleId = "Puzzle_Test",
                        Position = new Vector2IData(6, 7),
                        PromptText = "Choose",
                        Choices =
                        [
                            new PuzzleRiddleChoiceData { Id = "a", Label = "Alpha" },
                            new PuzzleRiddleChoiceData { Id = "b", Label = "Beta" }
                        ],
                        CorrectChoiceId = "b",
                        WrongAnswerDamage = 9
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);

        var trap = gridMap.GetNode<TrapTileSpawn>("TrapTile_Test");
        AssertThat(trap.Owner).IsEqual(sceneRoot);
        AssertThat(trap.PuzzleId).IsEqual("Puzzle_Test");
        AssertThat(trap.GridPosition).IsEqual(new Vector2I(3, 4));
        AssertThat(trap.Damage).IsEqual(18);
        AssertThat(trap.StatusEffectId).IsEqual("poison");
        AssertThat(trap.StatusMagnitude).IsEqual(2);
        AssertThat(trap.StatusTurns).IsEqual(3);
        AssertThat(trap.Position).IsEqual(new Vector2(112, 144));
        AssertThat(trap.ZIndex).IsEqual(2);

        var puzzleSwitch = gridMap.GetNode<PuzzleSwitchSpawn>("PuzzleSwitch_Test");
        AssertThat(puzzleSwitch.Owner).IsEqual(sceneRoot);
        AssertThat(puzzleSwitch.SwitchId).IsEqual("PuzzleSwitch_Test");
        AssertThat(puzzleSwitch.PuzzleId).IsEqual("Puzzle_Test");
        AssertThat(puzzleSwitch.GridPosition).IsEqual(new Vector2I(4, 5));
        AssertThat(puzzleSwitch.PromptText).IsEqual("Pull");
        AssertThat(puzzleSwitch.ActivatedText).IsEqual("Opened");
        AssertThat(puzzleSwitch.Position).IsEqual(new Vector2(144, 176));
        AssertThat(puzzleSwitch.ZIndex).IsEqual(2);

        var gate = gridMap.GetNode<PuzzleGateSpawn>("PuzzleGate_Test");
        AssertThat(gate.Owner).IsEqual(sceneRoot);
        AssertThat(gate.GateId).IsEqual("PuzzleGate_Test");
        AssertThat(gate.PuzzleId).IsEqual("Puzzle_Test");
        AssertThat(gate.GridPosition).IsEqual(new Vector2I(5, 6));
        AssertThat(gate.StartsClosed).IsTrue();
        AssertThat(gate.Position).IsEqual(new Vector2(176, 208));
        AssertThat(gate.ZIndex).IsEqual(2);

        var riddle = gridMap.GetNode<PuzzleRiddleSpawn>("PuzzleRiddle_Test");
        AssertThat(riddle.Owner).IsEqual(sceneRoot);
        AssertThat(riddle.RiddleId).IsEqual("PuzzleRiddle_Test");
        AssertThat(riddle.PuzzleId).IsEqual("Puzzle_Test");
        AssertThat(riddle.GridPosition).IsEqual(new Vector2I(6, 7));
        AssertThat(riddle.PromptText).IsEqual("Choose");
        AssertThat(riddle.ChoiceIds[0]).IsEqual("a");
        AssertThat(riddle.ChoiceLabels[0]).IsEqual("Alpha");
        AssertThat(riddle.ChoiceIds[1]).IsEqual("b");
        AssertThat(riddle.ChoiceLabels[1]).IsEqual("Beta");
        AssertThat(riddle.CorrectChoiceId).IsEqual("b");
        AssertThat(riddle.WrongAnswerDamage).IsEqual(9);
        AssertThat(riddle.Position).IsEqual(new Vector2(208, 240));
        AssertThat(riddle.ZIndex).IsEqual(2);

        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_UpdatesExistingTreasureBoxByTreasureBoxId()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var existingBox = new TreasureBoxSpawn
        {
            Name = "TreasureBox_NodeName",
            TreasureBoxId = "TreasureBox_Start",
            GridPosition = new Vector2I(1, 1),
            RewardGold = 5,
            RewardItemIds = ["old_item"],
            RewardItemQuantities = [1]
        };
        gridMap.AddChild(existingBox);
        existingBox.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TreasureBoxes =
                [
                    new TreasureBoxData
                    {
                        Id = "TreasureBox_Start",
                        Position = new Vector2IData(9, 10),
                        Gold = 250,
                        Items =
                        [
                            new TreasureBoxItemData { ItemId = "minor_potion", Quantity = 3 }
                        ]
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TreasureBox_NodeName")).IsTrue();
        AssertThat(gridMap.HasNode("TreasureBox_Start")).IsFalse();
        var box = gridMap.GetNode<TreasureBoxSpawn>("TreasureBox_NodeName");
        AssertThat(box.TreasureBoxId).IsEqual("TreasureBox_Start");
        AssertThat(box.GridPosition).IsEqual(new Vector2I(9, 10));
        AssertThat(box.RewardGold).IsEqual(250);
        AssertThat(box.RewardItemIds![0]).IsEqual("minor_potion");
        AssertThat(box.RewardItemQuantities![0]).IsEqual(3);
        AssertThat(box.Position).IsEqual(new Vector2(304, 336));
        AssertThat(box.ZIndex).IsEqual(2);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_UpdatesExistingPuzzleNodesById()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var trap = new TrapTileSpawn { Name = "TrapTile_Update", PuzzleId = "OldPuzzle", GridPosition = new Vector2I(1, 1) };
        var puzzleSwitch = new PuzzleSwitchSpawn { Name = "SwitchNode", SwitchId = "PuzzleSwitch_Update", PuzzleId = "OldPuzzle", GridPosition = new Vector2I(1, 2) };
        var gate = new PuzzleGateSpawn { Name = "GateNode", GateId = "PuzzleGate_Update", PuzzleId = "OldPuzzle", GridPosition = new Vector2I(1, 3) };
        var riddle = new PuzzleRiddleSpawn { Name = "RiddleNode", RiddleId = "PuzzleRiddle_Update", PuzzleId = "OldPuzzle", GridPosition = new Vector2I(1, 4) };
        gridMap.AddChild(trap);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(gate);
        gridMap.AddChild(riddle);
        trap.Owner = sceneRoot;
        puzzleSwitch.Owner = sceneRoot;
        gate.Owner = sceneRoot;
        riddle.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TrapTiles =
                [
                    new TrapTileData { Id = "TrapTile_Update", PuzzleId = "Puzzle_New", Position = new Vector2IData(7, 8), Damage = 20, StatusEffect = "burn", StatusMagnitude = 5, StatusTurns = 2 }
                ],
                PuzzleSwitches =
                [
                    new PuzzleSwitchData { Id = "PuzzleSwitch_Update", PuzzleId = "Puzzle_New", Position = new Vector2IData(8, 9), PromptText = "New prompt", ActivatedText = "New active" }
                ],
                PuzzleGates =
                [
                    new PuzzleGateData { Id = "PuzzleGate_Update", PuzzleId = "Puzzle_New", Position = new Vector2IData(9, 10), StartsClosed = false }
                ],
                PuzzleRiddles =
                [
                    new PuzzleRiddleData
                    {
                        Id = "PuzzleRiddle_Update",
                        PuzzleId = "Puzzle_New",
                        Position = new Vector2IData(10, 11),
                        PromptText = "New riddle",
                        Choices = [new PuzzleRiddleChoiceData { Id = "new", Label = "New choice" }],
                        CorrectChoiceId = "new",
                        WrongAnswerDamage = 14
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TrapTile_Update")).IsTrue();
        AssertThat(gridMap.HasNode("SwitchNode")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleSwitch_Update")).IsFalse();
        AssertThat(gridMap.HasNode("GateNode")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleGate_Update")).IsFalse();
        AssertThat(gridMap.HasNode("RiddleNode")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleRiddle_Update")).IsFalse();

        AssertThat(trap.PuzzleId).IsEqual("Puzzle_New");
        AssertThat(trap.GridPosition).IsEqual(new Vector2I(7, 8));
        AssertThat(trap.Damage).IsEqual(20);
        AssertThat(trap.StatusEffectId).IsEqual("burn");
        AssertThat(trap.StatusMagnitude).IsEqual(5);
        AssertThat(trap.StatusTurns).IsEqual(2);
        AssertThat(trap.Position).IsEqual(new Vector2(240, 272));

        AssertThat(puzzleSwitch.SwitchId).IsEqual("PuzzleSwitch_Update");
        AssertThat(puzzleSwitch.PuzzleId).IsEqual("Puzzle_New");
        AssertThat(puzzleSwitch.GridPosition).IsEqual(new Vector2I(8, 9));
        AssertThat(puzzleSwitch.PromptText).IsEqual("New prompt");
        AssertThat(puzzleSwitch.ActivatedText).IsEqual("New active");
        AssertThat(puzzleSwitch.Position).IsEqual(new Vector2(272, 304));

        AssertThat(gate.GateId).IsEqual("PuzzleGate_Update");
        AssertThat(gate.PuzzleId).IsEqual("Puzzle_New");
        AssertThat(gate.GridPosition).IsEqual(new Vector2I(9, 10));
        AssertThat(gate.StartsClosed).IsFalse();
        AssertThat(gate.Position).IsEqual(new Vector2(304, 336));

        AssertThat(riddle.RiddleId).IsEqual("PuzzleRiddle_Update");
        AssertThat(riddle.PuzzleId).IsEqual("Puzzle_New");
        AssertThat(riddle.GridPosition).IsEqual(new Vector2I(10, 11));
        AssertThat(riddle.PromptText).IsEqual("New riddle");
        AssertThat(riddle.ChoiceIds[0]).IsEqual("new");
        AssertThat(riddle.ChoiceLabels[0]).IsEqual("New choice");
        AssertThat(riddle.CorrectChoiceId).IsEqual("new");
        AssertThat(riddle.WrongAnswerDamage).IsEqual(14);
        AssertThat(riddle.Position).IsEqual(new Vector2(336, 368));

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
    public void ImportToScene_RemovesStalePuzzleNodesSynchronously()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var trap = new TrapTileSpawn { Name = "TrapTile_Stale", PuzzleId = "Puzzle_Stale" };
        var puzzleSwitch = new PuzzleSwitchSpawn { Name = "PuzzleSwitch_Stale", SwitchId = "PuzzleSwitch_Stale", PuzzleId = "Puzzle_Stale" };
        var gate = new PuzzleGateSpawn { Name = "PuzzleGate_Stale", GateId = "PuzzleGate_Stale", PuzzleId = "Puzzle_Stale" };
        var riddle = new PuzzleRiddleSpawn { Name = "PuzzleRiddle_Stale", RiddleId = "PuzzleRiddle_Stale", PuzzleId = "Puzzle_Stale" };
        gridMap.AddChild(trap);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(gate);
        gridMap.AddChild(riddle);
        trap.Owner = sceneRoot;
        puzzleSwitch.Owner = sceneRoot;
        gate.Owner = sceneRoot;
        riddle.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TrapTiles = [],
                PuzzleSwitches = [],
                PuzzleGates = [],
                PuzzleRiddles = []
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TrapTile_Stale")).IsFalse();
        AssertThat(gridMap.HasNode("PuzzleSwitch_Stale")).IsFalse();
        AssertThat(gridMap.HasNode("PuzzleGate_Stale")).IsFalse();
        AssertThat(gridMap.HasNode("PuzzleRiddle_Stale")).IsFalse();
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_SkipsTreasureBoxWithEmptyId()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
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
                        Id = "",
                        Position = new Vector2IData(10, 20),
                        Gold = 50,
                        Items = []
                    },
                    new TreasureBoxData
                    {
                        Id = "   ",
                        Position = new Vector2IData(15, 25),
                        Gold = 75,
                        Items = []
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        // No nodes should be created for empty/whitespace IDs
        AssertThat(gridMap.GetChildCount()).IsEqual(0);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_SkipsPuzzleNodesWithEmptyIdOrPuzzleId()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TrapTiles =
                [
                    new TrapTileData { Id = "", PuzzleId = "Puzzle_Test", Position = new Vector2IData(1, 1) },
                    new TrapTileData { Id = "TrapTile_MissingPuzzle", PuzzleId = " ", Position = new Vector2IData(2, 2) }
                ],
                PuzzleSwitches =
                [
                    new PuzzleSwitchData { Id = "", PuzzleId = "Puzzle_Test", Position = new Vector2IData(3, 3) },
                    new PuzzleSwitchData { Id = "PuzzleSwitch_MissingPuzzle", PuzzleId = "", Position = new Vector2IData(4, 4) }
                ],
                PuzzleGates =
                [
                    new PuzzleGateData { Id = " ", PuzzleId = "Puzzle_Test", Position = new Vector2IData(5, 5) },
                    new PuzzleGateData { Id = "PuzzleGate_MissingPuzzle", PuzzleId = "", Position = new Vector2IData(6, 6) }
                ],
                PuzzleRiddles =
                [
                    new PuzzleRiddleData { Id = "", PuzzleId = "Puzzle_Test", Position = new Vector2IData(7, 7) },
                    new PuzzleRiddleData { Id = "PuzzleRiddle_MissingPuzzle", PuzzleId = " ", Position = new Vector2IData(8, 8) }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.GetChildCount()).IsEqual(0);
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_PreservesExistingPuzzleNodesWhenMatchingEntryHasEmptyPuzzleId()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var trap = new TrapTileSpawn
        {
            Name = "TrapTile_Existing",
            PuzzleId = "Puzzle_Existing",
            GridPosition = new Vector2I(1, 2),
            Damage = 7,
            StatusEffectId = "poison",
            StatusMagnitude = 2,
            StatusTurns = 3
        };
        var puzzleSwitch = new PuzzleSwitchSpawn
        {
            Name = "SwitchNode",
            SwitchId = "PuzzleSwitch_Existing",
            PuzzleId = "Puzzle_Existing",
            GridPosition = new Vector2I(3, 4),
            PromptText = "Old prompt",
            ActivatedText = "Old active"
        };
        var gate = new PuzzleGateSpawn
        {
            Name = "GateNode",
            GateId = "PuzzleGate_Existing",
            PuzzleId = "Puzzle_Existing",
            GridPosition = new Vector2I(5, 6),
            StartsClosed = true
        };
        var riddle = new PuzzleRiddleSpawn
        {
            Name = "RiddleNode",
            RiddleId = "PuzzleRiddle_Existing",
            PuzzleId = "Puzzle_Existing",
            GridPosition = new Vector2I(7, 8),
            PromptText = "Old riddle",
            ChoiceIds = ["old"],
            ChoiceLabels = ["Old choice"],
            CorrectChoiceId = "old",
            WrongAnswerDamage = 5
        };
        gridMap.AddChild(trap);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(gate);
        gridMap.AddChild(riddle);
        trap.Owner = sceneRoot;
        puzzleSwitch.Owner = sceneRoot;
        gate.Owner = sceneRoot;
        riddle.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities
            {
                TrapTiles =
                [
                    new TrapTileData { Id = "TrapTile_Existing", PuzzleId = "", Position = new Vector2IData(11, 12), Damage = 99, StatusEffect = "burn", StatusMagnitude = 9, StatusTurns = 9 }
                ],
                PuzzleSwitches =
                [
                    new PuzzleSwitchData { Id = "PuzzleSwitch_Existing", PuzzleId = "", Position = new Vector2IData(13, 14), PromptText = "New prompt", ActivatedText = "New active" }
                ],
                PuzzleGates =
                [
                    new PuzzleGateData { Id = "PuzzleGate_Existing", PuzzleId = "", Position = new Vector2IData(15, 16), StartsClosed = false }
                ],
                PuzzleRiddles =
                [
                    new PuzzleRiddleData
                    {
                        Id = "PuzzleRiddle_Existing",
                        PuzzleId = "",
                        Position = new Vector2IData(17, 18),
                        PromptText = "New riddle",
                        Choices = [new PuzzleRiddleChoiceData { Id = "new", Label = "New choice" }],
                        CorrectChoiceId = "new",
                        WrongAnswerDamage = 99
                    }
                ]
            }
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TrapTile_Existing")).IsTrue();
        AssertThat(gridMap.HasNode("SwitchNode")).IsTrue();
        AssertThat(gridMap.HasNode("GateNode")).IsTrue();
        AssertThat(gridMap.HasNode("RiddleNode")).IsTrue();

        AssertThat(trap.PuzzleId).IsEqual("Puzzle_Existing");
        AssertThat(trap.GridPosition).IsEqual(new Vector2I(1, 2));
        AssertThat(trap.Damage).IsEqual(7);
        AssertThat(trap.StatusEffectId).IsEqual("poison");
        AssertThat(trap.StatusMagnitude).IsEqual(2);
        AssertThat(trap.StatusTurns).IsEqual(3);

        AssertThat(puzzleSwitch.PuzzleId).IsEqual("Puzzle_Existing");
        AssertThat(puzzleSwitch.GridPosition).IsEqual(new Vector2I(3, 4));
        AssertThat(puzzleSwitch.PromptText).IsEqual("Old prompt");
        AssertThat(puzzleSwitch.ActivatedText).IsEqual("Old active");

        AssertThat(gate.PuzzleId).IsEqual("Puzzle_Existing");
        AssertThat(gate.GridPosition).IsEqual(new Vector2I(5, 6));
        AssertThat(gate.StartsClosed).IsTrue();

        AssertThat(riddle.PuzzleId).IsEqual("Puzzle_Existing");
        AssertThat(riddle.GridPosition).IsEqual(new Vector2I(7, 8));
        AssertThat(riddle.PromptText).IsEqual("Old riddle");
        AssertThat(riddle.ChoiceIds[0]).IsEqual("old");
        AssertThat(riddle.ChoiceLabels[0]).IsEqual("Old choice");
        AssertThat(riddle.CorrectChoiceId).IsEqual("old");
        AssertThat(riddle.WrongAnswerDamage).IsEqual(5);
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
    public void ImportToScene_PreservesExistingTreasureBoxesWhenTreasureBoxesAbsent()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var existingBox = new TreasureBoxSpawn
        {
            Name = "TreasureBox_Existing",
            TreasureBoxId = "TreasureBox_Existing",
            GridPosition = new Vector2I(10, 10),
            RewardGold = 25
        };
        gridMap.AddChild(existingBox);
        existingBox.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TreasureBox_Existing")).IsTrue();
        sceneRoot.Free();
    }

    [TestCase]
    public void ImportToScene_PreservesExistingPuzzleNodesWhenPuzzleListsAbsent()
    {
        var sceneRoot = new Node2D { Name = "TestFloor" };
        var gridMap = new Node2D { Name = "GridMap" };
        sceneRoot.AddChild(gridMap);
        gridMap.Owner = sceneRoot;

        var trap = new TrapTileSpawn { Name = "TrapTile_Existing", PuzzleId = "Puzzle_Existing" };
        var puzzleSwitch = new PuzzleSwitchSpawn { Name = "PuzzleSwitch_Existing", SwitchId = "PuzzleSwitch_Existing", PuzzleId = "Puzzle_Existing" };
        var gate = new PuzzleGateSpawn { Name = "PuzzleGate_Existing", GateId = "PuzzleGate_Existing", PuzzleId = "Puzzle_Existing" };
        var riddle = new PuzzleRiddleSpawn { Name = "PuzzleRiddle_Existing", RiddleId = "PuzzleRiddle_Existing", PuzzleId = "Puzzle_Existing" };
        gridMap.AddChild(trap);
        gridMap.AddChild(puzzleSwitch);
        gridMap.AddChild(gate);
        gridMap.AddChild(riddle);
        trap.Owner = sceneRoot;
        puzzleSwitch.Owner = sceneRoot;
        gate.Owner = sceneRoot;
        riddle.Owner = sceneRoot;

        var model = new FloorJsonModel
        {
            Entities = new SceneEntities()
        };

        var importer = new TilemapJsonImporter();
        var err = importer.ImportToScene(model, gridMap);

        AssertThat(err).IsEqual(Godot.Error.Ok);
        AssertThat(gridMap.HasNode("TrapTile_Existing")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleSwitch_Existing")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleGate_Existing")).IsTrue();
        AssertThat(gridMap.HasNode("PuzzleRiddle_Existing")).IsTrue();
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
