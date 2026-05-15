using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleTrapSpawnTest : Node
{
    [TestCase]
    public async Task PuzzleNodes_AddExpectedGroupsAndFloorOwnershipWorks()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var floorRoot = new Node2D { Name = "FloorRoot" };
        var gridMap = new GridMap { Name = "GridMap" };
        floorRoot.AddChild(gridMap);

        var trap = new TrapTileSpawn { Name = "TrapTile_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(3, 4) };
        var gate = new PuzzleGateSpawn { Name = "PuzzleGate_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(5, 4) };
        var lever = new PuzzleSwitchSpawn { Name = "PuzzleSwitch_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(4, 4) };
        var riddle = new PuzzleRiddleSpawn { Name = "PuzzleRiddle_Test", PuzzleId = "Puzzle_Test", GridPosition = new Vector2I(6, 4) };

        gridMap.AddChild(trap);
        gridMap.AddChild(gate);
        gridMap.AddChild(lever);
        gridMap.AddChild(riddle);

        sceneTree.Root.AddChild(floorRoot);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        try
        {
            AssertThat(trap.BelongsToFloor(floorRoot)).IsTrue();
            AssertThat(gate.BelongsToFloor(floorRoot)).IsTrue();
            AssertThat(lever.BelongsToFloor(floorRoot)).IsTrue();
            AssertThat(riddle.BelongsToFloor(floorRoot)).IsTrue();

            AssertThat(trap.IsInGroup("TrapTileSpawn")).IsTrue();
            AssertThat(gate.IsInGroup("PuzzleGateSpawn")).IsTrue();
            AssertThat(lever.IsInGroup("PuzzleSwitchSpawn")).IsTrue();
            AssertThat(riddle.IsInGroup("PuzzleRiddleSpawn")).IsTrue();
        }
        finally
        {
            floorRoot.Free();
        }
    }

    [TestCase]
    public void RiddleSpawn_EvaluatesCorrectChoiceId()
    {
        var riddle = new PuzzleRiddleSpawn
        {
            CorrectChoiceId = "east_stone",
            ChoiceIds = ["north_stone", "east_stone"],
            ChoiceLabels = ["North stone", "East stone"]
        };

        try
        {
            AssertThat(riddle.IsCorrectChoice("east_stone")).IsTrue();
            AssertThat(riddle.IsCorrectChoice("north_stone")).IsFalse();
            AssertThat(riddle.GetChoices().Count).IsEqual(2);
        }
        finally
        {
            riddle.Free();
        }
    }

    [TestCase]
    public void GateSpawn_ApplySolvedStateTracksBlockingState()
    {
        var gate = new PuzzleGateSpawn { StartsClosed = true };

        try
        {
            gate.ApplySolvedState(false);
            AssertThat(gate.IsOpen).IsFalse();
            AssertThat(gate.BlocksMovement).IsTrue();

            gate.ApplySolvedState(true);
            AssertThat(gate.IsOpen).IsTrue();
            AssertThat(gate.BlocksMovement).IsFalse();
        }
        finally
        {
            gate.Free();
        }
    }
}
