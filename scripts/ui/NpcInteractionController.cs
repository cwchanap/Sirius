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
    private readonly Node _uiParent;
    private readonly NpcData _npc;
    private readonly Character _player;
    private readonly HashSet<string> _questFlags;

    private DialogueDialog _dialogueDialog;
    private ShopDialog _shopDialog;
    private HealDialog _healDialog;
    private bool _finished;

    /// <summary>Fired when the interaction is fully complete and all dialogs have been cleaned up.</summary>
    public event Action InteractionComplete;

    public NpcInteractionController(GameManager gameManager, Node uiParent,
                                    NpcData npc, Character player,
                                    HashSet<string> questFlags)
    {
        _gameManager = gameManager;
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

        _dialogueDialog = new DialogueDialog();
        _uiParent.AddChild(_dialogueDialog);
        _dialogueDialog.DialogueOutcome += OnDialogueOutcome;
        _dialogueDialog.DialogueClosed += OnDialogueClosed;
        _dialogueDialog.StartDialogue(_npc, tree, _player, _questFlags);
        _dialogueDialog.PopupCentered();
    }

    private void OnDialogueOutcome(int outcomeInt)
    {
        var outcome = (DialogueOutcomeType)outcomeInt;
        CleanupDialogueDialog();

        switch (outcome)
        {
            case DialogueOutcomeType.OpenShop:
                OpenShop();
                break;
            case DialogueOutcomeType.Heal:
                OpenHeal();
                break;
            case DialogueOutcomeType.CloseAndReturn:
            default:
                Finish();
                break;
        }
    }

    private void OnDialogueClosed()
    {
        CleanupDialogueDialog();
        Finish();
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

    private void CleanupDialogueDialog()
    {
        if (_dialogueDialog == null) return;
        _dialogueDialog.DialogueOutcome -= OnDialogueOutcome;
        _dialogueDialog.DialogueClosed -= OnDialogueClosed;
        if (GodotObject.IsInstanceValid(_dialogueDialog))
            _dialogueDialog.QueueFree();
        _dialogueDialog = null;
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
        CleanupDialogueDialog();
        CleanupShopDialog();
        CleanupHealDialog();
        InteractionComplete?.Invoke();
    }
}
