using Godot;
using Sirius.TilemapJson;
using System.Collections.Generic;

public static class FloorEntityBuilders
{
    public static List<TreasureBoxData> TreasureBoxes(
        IEnumerable<(string Id, Vector2I Position, int Gold, Dictionary<string, int> Items)> boxes)
    {
        var result = new List<TreasureBoxData>();
        foreach (var (id, position, gold, items) in boxes)
        {
            var box = new TreasureBoxData
            {
                Id = id,
                Position = new Vector2IData(position),
                Gold = gold,
            };
            foreach (var (itemId, qty) in items)
                box.Items.Add(new TreasureBoxItemData { ItemId = itemId, Quantity = qty });
            result.Add(box);
        }
        return result;
    }

    public static List<TrapTileData> TrapTiles(
        IEnumerable<(string Id, Vector2I Position, int Damage, string StatusEffect, int Magnitude, int Turns)> traps,
        string puzzleId)
    {
        var result = new List<TrapTileData>();
        foreach (var (id, position, damage, effect, magnitude, turns) in traps)
        {
            result.Add(new TrapTileData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                Damage = damage,
                StatusEffect = effect,
                StatusMagnitude = magnitude,
                StatusTurns = turns,
            });
        }
        return result;
    }

    public static List<PuzzleSwitchData> Switches(
        IEnumerable<(string Id, Vector2I Position, string Prompt, string Activated)> switches,
        string puzzleId)
    {
        var result = new List<PuzzleSwitchData>();
        foreach (var (id, position, prompt, activated) in switches)
        {
            result.Add(new PuzzleSwitchData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                PromptText = prompt,
                ActivatedText = activated,
            });
        }
        return result;
    }

    public static List<PuzzleGateData> Gates(
        IEnumerable<(string Id, Vector2I Position, bool StartsClosed)> gates,
        string puzzleId)
    {
        var result = new List<PuzzleGateData>();
        foreach (var (id, position, startsClosed) in gates)
        {
            result.Add(new PuzzleGateData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                StartsClosed = startsClosed,
            });
        }
        return result;
    }

    public static List<PuzzleRiddleData> Riddles(
        IEnumerable<(string Id, Vector2I Position, string Prompt, List<PuzzleRiddleChoiceData> Choices, string CorrectChoiceId, int WrongDamage)> riddles,
        string puzzleId)
    {
        var result = new List<PuzzleRiddleData>();
        foreach (var (id, position, prompt, choices, correct, wrongDamage) in riddles)
        {
            var riddle = new PuzzleRiddleData
            {
                Id = id,
                PuzzleId = puzzleId,
                Position = new Vector2IData(position),
                PromptText = prompt,
                CorrectChoiceId = correct,
                WrongAnswerDamage = wrongDamage,
            };
            foreach (var choice in choices)
                riddle.Choices.Add(choice);
            result.Add(riddle);
        }
        return result;
    }
}
