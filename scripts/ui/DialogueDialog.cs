using Godot;
using System.Collections.Generic;

/// <summary>
/// Modal dialogue dialog for branching NPC conversations.
/// Instantiated per NPC interaction; QueueFree'd by NpcInteractionController when done.
/// </summary>
public partial class DialogueDialog : AcceptDialog
{
    [Signal] public delegate void DialogueOutcomeEventHandler(int outcome);
    [Signal] public delegate void DialogueClosedEventHandler();

    private Label _speakerLabel;
    private RichTextLabel _textLabel;
    private VBoxContainer _choicesContainer;

    private DialogueTree _tree;
    private Character _player;
    private HashSet<string> _questFlags;

    public override void _Ready()
    {
        Size = new Vector2I(480, 320);
        Exclusive = true;
        GetOkButton().Visible = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        _speakerLabel = new Label();
        _speakerLabel.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(_speakerLabel);

        var separator = new HSeparator();
        root.AddChild(separator);

        _textLabel = new RichTextLabel();
        _textLabel.BbcodeEnabled = true;
        _textLabel.FitContent = true;
        _textLabel.CustomMinimumSize = new Vector2(440, 80);
        _textLabel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(_textLabel);

        var choicesSep = new HSeparator();
        root.AddChild(choicesSep);

        _choicesContainer = new VBoxContainer();
        _choicesContainer.AddThemeConstantOverride("separation", 4);
        root.AddChild(_choicesContainer);

        CloseRequested += OnCloseRequested;
        Canceled += OnCloseRequested;
    }

    /// <summary>Begins the dialogue from the tree's root node.</summary>
    public void StartDialogue(NpcData npc, DialogueTree tree, Character player, HashSet<string> questFlags)
    {
        Title = npc.DisplayName;
        _tree = tree;
        _player = player;
        _questFlags = questFlags;
        var rootNode = tree.Root;
        if (rootNode == null)
        {
            GD.PushError($"[DialogueDialog] Dialogue tree '{tree.TreeId}' has no 'root' node.");
            EmitSignal(SignalName.DialogueClosed);
            return;
        }

        ShowNode(rootNode);
    }

    private void ShowNode(DialogueNode node)
    {
        _speakerLabel.Text = node.SpeakerName + ":";
        _textLabel.Text = node.Text;

        // Clear previous choice buttons (Godot auto-disconnects signals on QueueFree)
        foreach (Node child in _choicesContainer.GetChildren())
            child.QueueFree();

        var visibleChoices = new List<DialogueChoice>();
        foreach (var choice in node.Choices)
        {
            if (choice.Condition.Evaluate(_player, _questFlags))
                visibleChoices.Add(choice);
        }

        if (visibleChoices.Count == 0)
        {
            // Leaf node — show a close button
            var closeBtn = new Button();
            closeBtn.Text = "Farewell.";
            closeBtn.Pressed += () => EmitSignal(SignalName.DialogueClosed);
            _choicesContainer.AddChild(closeBtn);
        }
        else
        {
            foreach (var choice in visibleChoices)
            {
                var btn = new Button();
                btn.Text = choice.Label;
                btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                var captured = choice;
                btn.Pressed += () => OnChoicePressed(captured);
                _choicesContainer.AddChild(btn);
            }
        }
    }

    private void OnChoicePressed(DialogueChoice choice)
    {
        // Grant quest flag if specified
        if (!string.IsNullOrEmpty(choice.GrantFlag))
            _questFlags?.Add(choice.GrantFlag);

        if (choice.Outcome != DialogueOutcomeType.None)
        {
            EmitSignal(SignalName.DialogueOutcome, (int)choice.Outcome);
            return;
        }

        if (choice.NextNodeId == null)
        {
            EmitSignal(SignalName.DialogueClosed);
            return;
        }

        var nextNode = _tree.GetNode(choice.NextNodeId);
        if (nextNode == null)
        {
            GD.PushError($"[DialogueDialog] Broken dialogue tree '{_tree.TreeId}': choice '{choice.Label}' references NextNodeId '{choice.NextNodeId}' which does not exist. Closing dialogue.");
            EmitSignal(SignalName.DialogueClosed);
            return;
        }

        ShowNode(nextNode);
    }

    private void OnCloseRequested()
    {
        EmitSignal(SignalName.DialogueClosed);
    }

    public override void _ExitTree()
    {
        CloseRequested -= OnCloseRequested;
        Canceled -= OnCloseRequested;
    }
}
