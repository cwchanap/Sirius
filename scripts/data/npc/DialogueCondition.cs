using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Evaluates whether a dialogue choice should be visible to the player.
/// </summary>
public interface IDialogueCondition
{
    bool Evaluate(Character player, HashSet<string> questFlags);
}

/// <summary>Always visible. Use as default when no condition is needed.</summary>
public sealed class AlwaysCondition : IDialogueCondition
{
    public static readonly AlwaysCondition Instance = new();
    private AlwaysCondition() { }
    public bool Evaluate(Character player, HashSet<string> questFlags) => true;
}

/// <summary>Visible only when the player has reached a minimum level.</summary>
public sealed class LevelCondition : IDialogueCondition
{
    public int MinLevel { get; init; }
    public bool Evaluate(Character player, HashSet<string> questFlags) => player.Level >= MinLevel;
}

/// <summary>Visible based on whether a quest flag is present or absent.</summary>
public sealed class QuestFlagCondition : IDialogueCondition
{
    public string Flag { get; init; }
    /// <summary>When true, requires the flag to be present. When false, requires it to be absent.</summary>
    public bool RequirePresent { get; init; } = true;

    public bool Evaluate(Character player, HashSet<string> questFlags)
        => questFlags.Contains(Flag) == RequirePresent;
}

/// <summary>Visible only when all sub-conditions are satisfied.</summary>
public sealed class AndCondition : IDialogueCondition
{
    public IDialogueCondition[] Conditions { get; init; } = Array.Empty<IDialogueCondition>();
    public bool Evaluate(Character player, HashSet<string> questFlags)
        => Conditions.All(c => c.Evaluate(player, questFlags));
}
