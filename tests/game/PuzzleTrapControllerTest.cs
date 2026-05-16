using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class PuzzleTrapControllerTest : Node
{
    [TestCase]
    public void SwitchArmsPuzzleAndCorrectRiddleSolvesIt()
    {
        var manager = new GameManager();
        var controller = new PuzzleTrapController(manager);
        var riddle = new PuzzleRiddleSpawn
        {
            PuzzleId = "Puzzle_Test",
            CorrectChoiceId = "east_stone"
        };

        try
        {
            AssertThat(controller.TrySolveRiddle(riddle, "east_stone").Solved).IsFalse();

            controller.ActivateSwitch("Puzzle_Test");
            var result = controller.TrySolveRiddle(riddle, "east_stone");

            AssertThat(result.Solved).IsTrue();
            AssertThat(manager.IsPuzzleSolved("Puzzle_Test")).IsTrue();
        }
        finally
        {
            riddle.Free();
            manager.Free();
        }
    }

    [TestCase]
    public void WrongRiddleChoiceDoesNotSolvePuzzle()
    {
        var manager = new GameManager();
        var controller = new PuzzleTrapController(manager);
        var riddle = new PuzzleRiddleSpawn
        {
            PuzzleId = "Puzzle_Test",
            CorrectChoiceId = "east_stone"
        };

        try
        {
            controller.ActivateSwitch("Puzzle_Test");
            var result = controller.TrySolveRiddle(riddle, "north_stone");

            AssertThat(result.Solved).IsFalse();
            AssertThat(result.ShouldApplyPenalty).IsTrue();
            AssertThat(manager.IsPuzzleSolved("Puzzle_Test")).IsFalse();
        }
        finally
        {
            riddle.Free();
            manager.Free();
        }
    }

    [TestCase]
    public void DormantRiddleReturnsMessageWithoutPenalty()
    {
        var manager = new GameManager();
        var controller = new PuzzleTrapController(manager);
        var riddle = new PuzzleRiddleSpawn
        {
            PuzzleId = "Puzzle_Test",
            CorrectChoiceId = "east_stone"
        };

        try
        {
            // Do NOT arm the switch — riddle is dormant
            var result = controller.TrySolveRiddle(riddle, "east_stone");

            AssertThat(result.Solved).IsFalse();
            AssertThat(result.ShouldApplyPenalty).IsFalse();
            AssertThat(result.Message).IsEqual("The mechanism is dormant.");
            AssertThat(manager.IsPuzzleSolved("Puzzle_Test")).IsFalse();
        }
        finally
        {
            riddle.Free();
            manager.Free();
        }
    }
}
