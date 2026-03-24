using Godot;
using System;
using System.Collections.Generic;

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
    public bool Evaluate(Character player, HashSet<string> questFlags)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        return player.Level >= MinLevel;
    }
}

/// <summary>Visible based on whether a quest flag is present or absent.</summary>
public sealed class QuestFlagCondition : IDialogueCondition
{
    public string Flag { get; init; }
    /// <summary>When true, requires the flag to be present. When false, requires it to be absent.</summary>
    public bool RequirePresent { get; init; } = true;

    public bool Evaluate(Character player, HashSet<string> questFlags)
    {
        if (string.IsNullOrEmpty(Flag))
        {
            GD.PushError("[QuestFlagCondition] Flag is null or empty — condition will always evaluate incorrectly. Check DialogueCatalog definition.");
            return false;
        }
        bool contains = questFlags?.Contains(Flag) == true;
        return contains == RequirePresent;
    }
}

/// <summary>Visible only when all sub-conditions are satisfied.</summary>
public sealed class AndCondition : IDialogueCondition
{
    public IDialogueCondition[] Conditions { get; init; } = Array.Empty<IDialogueCondition>();
    public bool Evaluate(Character player, HashSet<string> questFlags)
    {
        foreach (var c in Conditions)
        {
            if (c == null)
            {
                GD.PushError("[AndCondition] Null sub-condition found — treating as false. Check DialogueCatalog.");
                return false;
            }
            if (!c.Evaluate(player, questFlags)) return false;
        }
        return true;
    }
}
