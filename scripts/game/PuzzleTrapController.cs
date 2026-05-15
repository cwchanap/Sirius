using System;
using System.Collections.Generic;

public sealed class PuzzleTrapController
{
    private readonly GameManager _gameManager;
    private readonly HashSet<string> _armedPuzzleIds = new(StringComparer.Ordinal);

    public PuzzleTrapController(GameManager gameManager)
    {
        _gameManager = gameManager ?? throw new ArgumentNullException(nameof(gameManager));
    }

    public bool IsPuzzleArmed(string puzzleId)
    {
        return !string.IsNullOrWhiteSpace(puzzleId) && _armedPuzzleIds.Contains(puzzleId);
    }

    public bool ActivateSwitch(PuzzleSwitchSpawn switchSpawn)
    {
        if (switchSpawn == null)
        {
            return false;
        }

        return ActivateSwitch(switchSpawn.PuzzleId);
    }

    public bool ActivateSwitch(string puzzleId)
    {
        if (string.IsNullOrWhiteSpace(puzzleId) || _gameManager.IsPuzzleSolved(puzzleId))
        {
            return false;
        }

        return _armedPuzzleIds.Add(puzzleId);
    }

    public PuzzleRiddleResult TrySolveRiddle(PuzzleRiddleSpawn riddle, string choiceId)
    {
        if (riddle == null || string.IsNullOrWhiteSpace(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(false, false, "Invalid puzzle.");
        }

        if (_gameManager.IsPuzzleSolved(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(true, false, "Already solved.");
        }

        if (!IsPuzzleArmed(riddle.PuzzleId))
        {
            return new PuzzleRiddleResult(false, false, "The mechanism is dormant.");
        }

        if (!riddle.IsCorrectChoice(choiceId))
        {
            return new PuzzleRiddleResult(false, true, "The seal rejects the answer.");
        }

        _gameManager.MarkPuzzleSolved(riddle.PuzzleId);
        return new PuzzleRiddleResult(true, false, "The gate opens.");
    }
}

public readonly record struct PuzzleRiddleResult(bool Solved, bool ShouldApplyPenalty, string Message);
