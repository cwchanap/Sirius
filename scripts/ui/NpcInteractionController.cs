using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Orchestrates the full NPC interaction flow: dialogue → shop or heal → cleanup.
/// Not a Godot node; instantiated per interaction and discarded when done.
/// Follows the same pattern as Game.cs handles BattleManager (instantiate, wire signals, QueueFree on exit).
/// </summary>
public class NpcInteractionController
{
    private readonly GameManager _gameManager;
    private readonly UIScreenHost _screenHost;
    private readonly Node _uiParent;
    private readonly NpcData _npc;
    private readonly Character _player;
    private readonly HashSet<string> _questFlags;

    private DialogueScreenController? _dialogueScreen;
    private UIScreenHandle? _dialogueHandle;
    private ShopDialog _shopDialog;
    private HealDialog _healDialog;
    private bool _finished;

    /// <summary>Fired when the interaction is fully complete and all dialogs have been cleaned up.</summary>
    public event Action InteractionComplete;

    public NpcInteractionController(
        GameManager gameManager,
        UIScreenHost screenHost,
        Node uiParent,
        NpcData npc,
        Character player,
        HashSet<string> questFlags)
    {
        _gameManager = gameManager;
        _screenHost = screenHost;
        _uiParent = uiParent;
        _npc = npc;
        _player = player;
        _questFlags = questFlags;
    }

    /// <summary>Starts the interaction by showing the dialogue dialog.</summary>
    public void Begin()
    {
        var tree = DialogueCatalog.GetById(_npc.DialogueTreeId);
        if (tree == null)
        {
            GD.PushError($"[NpcInteractionController] DialogueTreeId '{_npc.DialogueTreeId}' not found for NPC '{_npc.NpcId}'. Ending interaction.");
            Finish();
            return;
        }

        var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
        if (packed == null)
        {
            GD.PushError("[NpcInteractionController] DialogueScreen.tscn not found.");
            Finish();
            return;
        }

        var screen = packed.Instantiate<DialogueScreenController>();
        if (!screen.TryStartDialogue(_npc, tree, _player, _questFlags))
        {
            GD.PushError($"[NpcInteractionController] Dialogue tree '{tree.TreeId}' has no usable root or screen was already started.");
            screen.QueueFree();
            Finish();
            return;
        }

        screen.DialogueOutcome += OnDialogueOutcome;
        screen.DialogueClosed += OnDialogueClosed;

        var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Dialogue,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.Always,
            PauseTree = false,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Consume,
            InitialFocus = () => screen.InitialFocusTarget,
            InterceptCancel = _ =>
            {
                screen.RequestCancel();
                return UIInputInterception.ConsumeHere;
            },
            Cleanup = _ => ClearDialoguePresentation(screen),
            NodeLifetime = UINodeLifetime.QueueFree
        });

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            screen.DialogueOutcome -= OnDialogueOutcome;
            screen.DialogueClosed -= OnDialogueClosed;
            screen.QueueFree();
            Finish();
            return;
        }

        _dialogueScreen = screen;
        _dialogueHandle = result.Handle.Value;
    }

    private void OnDialogueOutcome(int outcomeInt)
    {
        var outcome = (DialogueOutcomeType)outcomeInt;
        CloseDialoguePresentation(UIScreenCloseReason.Programmatic);

        switch (outcome)
        {
            case DialogueOutcomeType.OpenShop:
                OpenShop();
                break;
            case DialogueOutcomeType.Heal:
                OpenHeal();
                break;
            case DialogueOutcomeType.CloseAndReturn:
                Finish();
                break;
            default:
                GD.PushWarning($"[NpcInteractionController] Unhandled DialogueOutcomeType value {outcomeInt} — treating as CloseAndReturn.");
                Finish();
                break;
        }
    }

    private void OnDialogueClosed()
    {
        CloseDialoguePresentation(UIScreenCloseReason.Programmatic);
        Finish();
    }

    private void ClearDialoguePresentation(DialogueScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.DialogueOutcome -= OnDialogueOutcome;
            screen.DialogueClosed -= OnDialogueClosed;
        }

        if (ReferenceEquals(_dialogueScreen, screen))
        {
            _dialogueScreen = null;
            _dialogueHandle = null;
        }
    }

    private void CloseDialoguePresentation(UIScreenCloseReason reason)
    {
        if (_dialogueScreen == null || !_dialogueHandle.HasValue)
            return;

        var screen = _dialogueScreen;
        var result = _screenHost.TryClose(_dialogueHandle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
            ClearDialoguePresentation(screen);
    }

    private void OpenShop()
    {
        var shopInventory = ShopCatalog.GetById(_npc.ShopId);
        if (shopInventory == null)
        {
            GD.PushError($"[NpcInteractionController] ShopId '{_npc.ShopId}' not found for NPC '{_npc.NpcId}'.");
            Finish();
            return;
        }

        _shopDialog = new ShopDialog();
        _uiParent.AddChild(_shopDialog);
        _shopDialog.ShopClosed += OnShopClosed;
        _shopDialog.OpenShop(shopInventory, _player);
        _shopDialog.PopupCentered();
    }

    private void OnShopClosed()
    {
        CleanupShopDialog();
        Finish();
    }

    private void OpenHeal()
    {
        _healDialog = new HealDialog();
        _uiParent.AddChild(_healDialog);
        _healDialog.HealComplete += OnHealDone;
        _healDialog.HealCancelled += OnHealDone;
        _healDialog.OpenHeal(_npc, _player);
        _healDialog.PopupCentered();
    }

    private void OnHealDone()
    {
        CleanupHealDialog();
        Finish();
    }

    private void CleanupShopDialog()
    {
        if (_shopDialog == null) return;
        _shopDialog.ShopClosed -= OnShopClosed;
        if (GodotObject.IsInstanceValid(_shopDialog))
            _shopDialog.QueueFree();
        _shopDialog = null;
    }

    private void CleanupHealDialog()
    {
        if (_healDialog == null) return;
        _healDialog.HealComplete -= OnHealDone;
        _healDialog.HealCancelled -= OnHealDone;
        if (GodotObject.IsInstanceValid(_healDialog))
            _healDialog.QueueFree();
        _healDialog = null;
    }

    /// <summary>Cleans up all open dialogs and fires InteractionComplete. Safe to call multiple times.</summary>
    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        CloseDialoguePresentation(UIScreenCloseReason.Programmatic);
        CleanupShopDialog();
        CleanupHealDialog();
        InteractionComplete?.Invoke();
    }
}
