using Godot;
using System.Collections.Generic;
using System.Linq;

[Tool]
public partial class PuzzleRiddleSpawn : PuzzleSpawnBase
{
    [Export] public string RiddleId { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string PromptText { get; set; } = "";
    [Export] public Godot.Collections.Array<string> ChoiceIds { get; set; } = new();
    [Export] public Godot.Collections.Array<string> ChoiceLabels { get; set; } = new();
    [Export] public string CorrectChoiceId { get; set; } = "";
    [Export] public int WrongAnswerDamage { get; set; } = 12;

    protected override string GroupName => "PuzzleRiddleSpawn";
    protected override Color FallbackColor => Colors.Gold;

    public override string[] _GetConfigurationWarnings()
    {
        var warnings = new List<string>();

        if (ChoiceIds == null || ChoiceIds.Count == 0)
        {
            warnings.Add("ChoiceIds is empty — the riddle has no answers to choose from.");
        }

        if (string.IsNullOrWhiteSpace(CorrectChoiceId))
        {
            warnings.Add("CorrectChoiceId is empty — the riddle cannot be solved.");
        }
        else if (ChoiceIds != null && !ChoiceIds.Contains(CorrectChoiceId))
        {
            warnings.Add($"CorrectChoiceId '{CorrectChoiceId}' not found in ChoiceIds — the riddle is unsolvable.");
        }

        if (ChoiceIds != null && ChoiceLabels != null && ChoiceIds.Count != ChoiceLabels.Count)
        {
            warnings.Add($"ChoiceIds ({ChoiceIds.Count}) and ChoiceLabels ({ChoiceLabels.Count}) have different lengths — labels will fall back to IDs for mismatched entries.");
        }

        return warnings.ToArray();
    }

    public bool IsCorrectChoice(string choiceId)
    {
        return !string.IsNullOrWhiteSpace(choiceId) && choiceId == CorrectChoiceId;
    }

    public IReadOnlyList<PuzzleRiddleChoice> GetChoices()
    {
        var choices = new List<PuzzleRiddleChoice>();
        for (int i = 0; i < ChoiceIds.Count; i++)
        {
            string id = ChoiceIds[i];
            string label = i < ChoiceLabels.Count ? ChoiceLabels[i] : id;
            choices.Add(new PuzzleRiddleChoice(id, label));
        }

        return choices;
    }
}

public readonly record struct PuzzleRiddleChoice(string Id, string Label);
