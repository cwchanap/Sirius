using System;
using System.Collections.Generic;

/// <summary>
/// Outcome action triggered when a dialogue choice is selected.
/// </summary>
public enum DialogueOutcomeType
{
    None,
    OpenShop,
    Heal,
    CloseAndReturn
}

/// <summary>
/// A single player-selectable option within a dialogue node.
/// </summary>
public class DialogueChoice
{
    public string Label { get; init; }

    /// <summary>ID of the next node to navigate to. Null means close the dialogue.</summary>
    public string? NextNodeId { get; init; }

    /// <summary>Condition controlling whether this choice is shown. Defaults to always visible.</summary>
    public IDialogueCondition Condition { get; init; } = AlwaysCondition.Instance;

    /// <summary>Action to take when this choice is selected.</summary>
    public DialogueOutcomeType Outcome { get; init; } = DialogueOutcomeType.None;

    /// <summary>Optional quest flag granted to the player when this choice is selected.</summary>
    public string? GrantFlag { get; init; }
}

/// <summary>
/// A single node in a dialogue tree, representing one exchange of text.
/// </summary>
public class DialogueNode
{
    public string NodeId { get; init; }
    public string SpeakerName { get; init; }
    public string Text { get; init; }

    /// <summary>
    /// Player choices shown after the text. Empty list = leaf node (shows a "Goodbye" close button).
    /// </summary>
    public IReadOnlyList<DialogueChoice> Choices { get; init; } = Array.Empty<DialogueChoice>();
}

/// <summary>
/// A graph of DialogueNodes keyed by ID. The root node must have id "root".
/// </summary>
public class DialogueTree
{
    public string TreeId { get; init; }
    public IReadOnlyDictionary<string, DialogueNode> Nodes { get; init; }

    public DialogueNode? Root => Nodes.TryGetValue("root", out var n) ? n : null;

    public DialogueNode? GetNode(string? id)
        => !string.IsNullOrEmpty(id) && Nodes.TryGetValue(id, out var node) ? node : null;
}
