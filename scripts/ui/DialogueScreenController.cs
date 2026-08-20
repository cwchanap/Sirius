using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DialogueScreenController : Control
{
    [Signal] public delegate void DialogueOutcomeEventHandler(int outcome);
    [Signal] public delegate void DialogueClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    private const float StandardDialogueHeightFraction = 0.45f;
    private const float StandardPortraitSize = 64f;
    private const float CompactPortraitSize = 40f;

    private Control _safeFrame = null!;
    private SiriusModalShell _shell = null!;
    private TextureRect _portrait = null!;
    private Label _speakerLabel = null!;
    private RichTextLabel _textLabel = null!;
    private VBoxContainer _choicesContainer = null!;

    private NpcData? _npc;
    private DialogueTree? _tree;
    private Character? _player;
    private HashSet<string>? _questFlags;
    private DialogueNode? _currentNode;
    private bool _started;
    private bool _terminalEmitted;

    public bool TryStartDialogue(
        NpcData npc,
        DialogueTree tree,
        Character player,
        HashSet<string> questFlags)
    {
        if (_started)
            return false;

        var root = tree.Root;
        if (root == null)
            return false;

        _started = true;
        _npc = npc;
        _tree = tree;
        _player = player;
        _questFlags = questFlags;
        _currentNode = root;

        if (IsNodeReady())
            ShowNode(root);
        return true;
    }

    public override void _Ready()
    {
        _safeFrame = GetNode<Control>("%SafeFrame");
        _shell = GetNode<SiriusModalShell>("%ModalShell");
        _portrait = GetNode<TextureRect>("%NpcPortrait");
        _speakerLabel = GetNode<Label>("%SpeakerLabel");
        _textLabel = GetNode<RichTextLabel>("%DialogueText");
        _choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");
        _shell.BodyHost.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Resized += OnResized;
        RefreshLayout();

        if (_currentNode != null)
            ShowNode(_currentNode);
    }

    public override void _ExitTree()
    {
        Resized -= OnResized;
    }

    private void OnResized() => RefreshLayout();

    private void RefreshLayout()
    {
        if (!IsNodeReady())
            return;

        var size = GetViewportRect().Size;
        var insets = SiriusUiMetrics.SafeFrameInsets(size);
        var safeHeight = Mathf.Max(0f, size.Y - insets.Margin * 2f);
        var bandHeight = insets.Compact
            ? safeHeight
            : safeHeight * StandardDialogueHeightFraction;
        var contentWidth = Mathf.Max(0f, size.X - insets.SideInset * 2f);

        _safeFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _safeFrame.OffsetLeft = insets.SideInset;
        _safeFrame.OffsetTop = size.Y - insets.Margin - bandHeight;
        _safeFrame.OffsetRight = -insets.SideInset;
        _safeFrame.OffsetBottom = -insets.Margin;

        _shell.Compact = insets.Compact;
        _shell.RefreshPresentation(new Vector2(contentWidth, bandHeight));

        _speakerLabel.ThemeTypeVariation = insets.Compact
            ? SiriusThemeTypes.SectionCompact
            : SiriusThemeTypes.Section;
        _textLabel.ThemeTypeVariation = insets.Compact
            ? SiriusThemeTypes.BodyCompact
            : SiriusThemeTypes.Body;

        var portraitSize = insets.Compact ? CompactPortraitSize : StandardPortraitSize;
        _portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);

        var minimumTarget = SiriusUiMetrics.MinimumTarget(insets.Compact);
        foreach (var child in _choicesContainer.GetChildren())
        {
            if (child is Button action)
                action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
        }
    }

    private void ShowNode(DialogueNode node)
    {
        _currentNode = node;
        _shell.Title = _npc?.DisplayName ?? string.Empty;
        RefreshPortrait();
        _speakerLabel.Text = node.SpeakerName ?? string.Empty;
        _speakerLabel.Visible = !string.IsNullOrWhiteSpace(node.SpeakerName);
        _textLabel.Text = node.Text ?? string.Empty;

        foreach (Node child in _choicesContainer.GetChildren())
        {
            _choicesContainer.RemoveChild(child);
            child.QueueFree();
        }

        var visibleChoices = new List<DialogueChoice>();
        foreach (var choice in node.Choices)
        {
            if (choice.Condition.Evaluate(_player!, _questFlags!))
                visibleChoices.Add(choice);
        }

        foreach (var choice in visibleChoices)
        {
            var button = CreateActionButton(choice.Label);
            var captured = choice;
            button.Pressed += () => OnChoicePressed(captured);
            _choicesContainer.AddChild(button);
        }

        if (visibleChoices.Count == 0)
        {
            var close = CreateActionButton("Farewell.");
            close.Pressed += EmitClosedOnce;
            _choicesContainer.AddChild(close);
        }

        InitialFocusTarget = _choicesContainer.GetChildren()
            .OfType<Button>()
            .FirstOrDefault();
        RefreshLayout();
        if (InitialFocusTarget != null)
            Callable.From(InitialFocusTarget.GrabFocus).CallDeferred();
    }

    private void RefreshPortrait()
    {
        var path = _npc?.PortraitPath;
        var texture = string.IsNullOrWhiteSpace(path)
            ? null
            : UiArtCatalog.LoadContentTexture(path);

        UiIconPresenter.ApplyItem(_portrait, texture);
        _portrait.Visible = texture != null;
    }

    private void OnChoicePressed(DialogueChoice choice)
    {
        if (_terminalEmitted)
            return;

        if (!string.IsNullOrEmpty(choice.GrantFlag))
            _questFlags?.Add(choice.GrantFlag);

        if (choice.Outcome != DialogueOutcomeType.None)
        {
            EmitOutcomeOnce(choice.Outcome);
            return;
        }

        if (choice.NextNodeId == null)
        {
            EmitClosedOnce();
            return;
        }

        var nextNode = _tree!.GetNode(choice.NextNodeId);
        if (nextNode == null)
        {
            GD.PushError($"[DialogueScreen] Broken dialogue tree '{_tree.TreeId}': choice '{choice.Label}' references NextNodeId '{choice.NextNodeId}' which does not exist. Closing dialogue.");
            EmitClosedOnce();
            return;
        }

        ShowNode(nextNode);
    }

    public void RequestCancel() => EmitClosedOnce();

    private bool TryBeginTerminal()
    {
        if (_terminalEmitted)
            return false;

        _terminalEmitted = true;
        return true;
    }

    private void EmitClosedOnce()
    {
        if (TryBeginTerminal())
            EmitSignal(SignalName.DialogueClosed);
    }

    private void EmitOutcomeOnce(DialogueOutcomeType outcome)
    {
        if (TryBeginTerminal())
            EmitSignal(SignalName.DialogueOutcome, (int)outcome);
    }

    private static Button CreateActionButton(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
        SizeFlagsHorizontal = SizeFlags.ExpandFill
    };
}
