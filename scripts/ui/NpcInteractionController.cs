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
    private readonly NpcData _npc;
    private readonly Character _player;
    private readonly HashSet<string> _questFlags;

    private DialogueScreenController? _dialogueScreen;
    private UIScreenHandle? _dialogueHandle;
    private ShopScreenController? _shopScreen;
    private UIScreenHandle? _shopHandle;
    private HealingScreenController? _healScreen;
    private UIScreenHandle? _healHandle;
    private bool _finished;

    /// <summary>Fired when the interaction is fully complete and all dialogs have been cleaned up.</summary>
    public event Action InteractionComplete;

    public NpcInteractionController(
        GameManager gameManager,
        UIScreenHost screenHost,
        NpcData npc,
        Character player,
        HashSet<string> questFlags)
    {
        _gameManager = gameManager;
        _screenHost = screenHost;
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

        if (TryHostSurface(screen, new UIScreenEntrySpec
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
            },
            () =>
            {
                screen.DialogueOutcome -= OnDialogueOutcome;
                screen.DialogueClosed -= OnDialogueClosed;
            },
            out var handle))
        {
            _dialogueScreen = screen;
            _dialogueHandle = handle;
        }
    }

    /// <summary>
    /// Presents <paramref name="screen"/> through the host and returns its
    /// active handle. Owns only the mechanical TryPresent protocol shared by
    /// every hosted surface: publication-throw recovery (unsubscribe, free
    /// the candidate, Finish, rethrow), rejected-open cleanup (unsubscribe,
    /// free, Finish), the post-commit IsActive recheck, and finishing without
    /// retaining state when the entry was closed synchronously during
    /// publication. Callers own screen creation/configuration, signal
    /// subscriptions, the explicit <see cref="UIScreenEntrySpec"/>, and
    /// per-surface screen/handle retention.
    /// </summary>
    private bool TryHostSurface(
        Control screen,
        UIScreenEntrySpec spec,
        Action unsubscribe,
        out UIScreenHandle handle)
    {
        UIScreenOpenResult result;
        try
        {
            result = _screenHost.TryPresent(screen, spec);
        }
        catch (Exception)
        {
            // TryPresent can throw when a post-commit publication subscriber
            // (GameplayInputBlockChanged / EffectiveStateChanged) fails. The
            // entry may already be committed; freeing the view triggers the
            // host's NodeFreed close, the designed recovery.
            unsubscribe();
            if (GodotObject.IsInstanceValid(screen))
                screen.QueueFree();
            Finish();
            throw;
        }

        if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
        {
            unsubscribe();
            if (GodotObject.IsInstanceValid(screen))
                screen.QueueFree();
            Finish();
            handle = default;
            return false;
        }

        // TryPresent() can return Opened even when an EffectiveStateChanged /
        // GameplayInputBlockChanged subscriber synchronously closed the entry
        // during the final post-commit publication. UIScreenHost documents and
        // tests this contract: the entry may already be closed when
        // TryPresent() returns. In that case the close path already ran the
        // spec Cleanup, which unsubscribed the surface signals but could not
        // clear the caller's per-surface state because it is assigned only
        // after this helper returns. Retaining the stale screen/handle would
        // leave no visible screen and no future terminal signal to call
        // Finish(), while GameManager.IsInNpcInteraction stays true — a
        // soft-lock. The close path already freed/queued the view and
        // unsubscribed the signals, so just finish without touching the
        // screen again.
        if (!_screenHost.IsActive(result.Handle.Value))
        {
            Finish();
            handle = default;
            return false;
        }

        handle = result.Handle.Value;
        return true;
    }

    /// <summary>
    /// Closes the hosted entry behind <paramref name="handle"/>; on a stale
    /// handle (already closed through another path) runs
    /// <paramref name="clear"/> so the caller's screen/handle state does not
    /// linger. Only the stale-close mechanics live here — callers keep their
    /// own null-state guards before delegating.
    /// </summary>
    private void CloseHostedPresentation(
        UIScreenHandle? handle,
        UIScreenCloseReason reason,
        Action clear)
    {
        if (!handle.HasValue)
            return;

        var result = _screenHost.TryClose(handle.Value, reason);
        if (result.Status == UIScreenCloseStatus.StaleHandle)
            clear();
    }

    private void OnDialogueOutcome(int outcomeInt)
    {
        var outcome = (DialogueOutcomeType)outcomeInt;
        // CloseDialoguePresentation calls _screenHost.TryClose, whose
        // Recompute publishes the unblocked state via GameplayInputBlockChanged
        // / EffectiveStateChanged. A throwing subscriber escapes TryClose and
        // would skip the outcome switch below — for CloseAndReturn that means
        // Finish() is never called and GameManager.IsInNpcInteraction latches.
        // The host's Cleanup callback (ClearDialoguePresentation) has already
        // run by the time Recompute publishes, so the Dialogue entry is gone
        // and its signals are disconnected; swallowing the publication
        // exception and proceeding to the outcome switch is safe.
        try
        {
            CloseDialoguePresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[NpcInteractionController] Close publication failed during dialogue outcome: {ex.Message}");
        }

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
        // Same publication-exception concern as OnDialogueOutcome: TryClose's
        // Recompute publishes the unblocked state and a throwing subscriber
        // escapes TryClose. Without catching it here, Finish() is never
        // called, InteractionComplete never fires, and
        // GameManager.IsInNpcInteraction latches. By the time Recompute
        // publishes, the host's Cleanup (ClearDialoguePresentation) has
        // already unsubscribed the dialogue signals and cleared
        // _dialogueScreen/_dialogueHandle, so the Dialogue is gone; swallowing
        // the publication exception and proceeding to Finish() is safe.
        try
        {
            CloseDialoguePresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[NpcInteractionController] Close publication failed during dialogue closed: {ex.Message}");
        }
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
        CloseHostedPresentation(
            _dialogueHandle,
            reason,
            () => ClearDialoguePresentation(screen));
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

        var screen = GD.Load<PackedScene>("res://scenes/ui/ShopScreen.tscn")
            .Instantiate<ShopScreenController>();
        screen.TryOpenShop(shopInventory, _player);
        screen.ShopClosed += OnShopClosed;

        if (TryHostSurface(screen, new UIScreenEntrySpec
            {
                Kind = UIScreenKinds.Shop,
                Layer = UIScreenLayer.Modal,
                InputPriority = UIInputPriority.Modal,
                ProcessPolicy = UIProcessPolicy.Always,
                PauseTree = false,
                BlockGameplayInput = true,
                Cursor = UICursorPolicy.Visible,
                Hud = UIHudPolicy.Hidden,
                LowerLayers = UILowerLayerPolicy.VisibleInert,
                Cancel = UICancelPolicy.Consume,
                InitialFocus = () => screen.InitialFocusTarget,
                InterceptCancel = _ =>
                {
                    screen.RequestCancel();
                    return UIInputInterception.ConsumeHere;
                },
                Cleanup = _ => ClearShopPresentation(screen),
                NodeLifetime = UINodeLifetime.QueueFree
            },
            () => screen.ShopClosed -= OnShopClosed,
            out var handle))
        {
            _shopScreen = screen;
            _shopHandle = handle;
        }
    }

    private void OnShopClosed()
    {
        // Same publication-exception concern as OnDialogueClosed: TryClose's
        // Recompute publishes the unblocked state and a throwing subscriber
        // escapes TryClose. By then the host's Cleanup
        // (ClearShopPresentation) has already unsubscribed the shop signal
        // and cleared _shopScreen/_shopHandle, so swallowing the exception
        // and proceeding to Finish() is safe — without it,
        // GameManager.IsInNpcInteraction would latch.
        try
        {
            CloseShopPresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[NpcInteractionController] Close publication failed during shop closed: {ex.Message}");
        }
        Finish();
    }

    private void ClearShopPresentation(ShopScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
            screen.ShopClosed -= OnShopClosed;

        if (ReferenceEquals(_shopScreen, screen))
        {
            _shopScreen = null;
            _shopHandle = null;
        }
    }

    private void CloseShopPresentation(UIScreenCloseReason reason)
    {
        if (_shopScreen == null || !_shopHandle.HasValue)
            return;

        var screen = _shopScreen;
        CloseHostedPresentation(
            _shopHandle,
            reason,
            () => ClearShopPresentation(screen));
    }

    private void OpenHeal()
    {
        var screen = GD.Load<PackedScene>("res://scenes/ui/HealingScreen.tscn")
            .Instantiate<HealingScreenController>();
        screen.TryOpenHeal(_npc, _player);
        screen.HealComplete += OnHealDone;
        screen.HealCancelled += OnHealDone;

        if (TryHostSurface(screen, new UIScreenEntrySpec
            {
                Kind = UIScreenKinds.Heal,
                Layer = UIScreenLayer.Modal,
                InputPriority = UIInputPriority.Modal,
                ProcessPolicy = UIProcessPolicy.Always,
                PauseTree = false,
                BlockGameplayInput = true,
                Cursor = UICursorPolicy.Visible,
                Hud = UIHudPolicy.Hidden,
                LowerLayers = UILowerLayerPolicy.VisibleInert,
                Cancel = UICancelPolicy.Consume,
                InitialFocus = () => screen.InitialFocusTarget,
                InterceptCancel = _ =>
                {
                    screen.RequestCancel();
                    return UIInputInterception.ConsumeHere;
                },
                Cleanup = _ => ClearHealPresentation(screen),
                NodeLifetime = UINodeLifetime.QueueFree
            },
            () =>
            {
                screen.HealComplete -= OnHealDone;
                screen.HealCancelled -= OnHealDone;
            },
            out var handle))
        {
            _healScreen = screen;
            _healHandle = handle;
        }
    }

    private void OnHealDone()
    {
        // HealComplete and HealCancelled both terminate the interaction.
        // Same publication-exception concern as OnDialogueClosed; by the
        // time a close publication throws, ClearHealPresentation has
        // already unsubscribed both signals and cleared the state, so
        // swallowing it and proceeding to Finish() is safe.
        try
        {
            CloseHealPresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[NpcInteractionController] Close publication failed during heal terminal: {ex.Message}");
        }
        Finish();
    }

    private void ClearHealPresentation(HealingScreenController screen)
    {
        if (GodotObject.IsInstanceValid(screen))
        {
            screen.HealComplete -= OnHealDone;
            screen.HealCancelled -= OnHealDone;
        }

        if (ReferenceEquals(_healScreen, screen))
        {
            _healScreen = null;
            _healHandle = null;
        }
    }

    private void CloseHealPresentation(UIScreenCloseReason reason)
    {
        if (_healScreen == null || !_healHandle.HasValue)
            return;

        var screen = _healScreen;
        CloseHostedPresentation(
            _healHandle,
            reason,
            () => ClearHealPresentation(screen));
    }

    /// <summary>Cleans up all open dialogs and fires InteractionComplete. Safe to call multiple times.</summary>
    public void Finish()
    {
        if (_finished) return;
        _finished = true;
        // The per-surface Close*Presentation calls can throw via the same
        // TryClose → Recompute publication path. _finished is set BEFORE
        // cleanup so a re-entrant Finish() (e.g. from a signal fired during
        // cleanup) is a no-op. But that means a throw here would skip
        // InteractionComplete and leave GameManager.IsInNpcInteraction
        // latched forever — every later Finish() retry is a no-op because
        // _finished is already true. Wrap the cleanup so InteractionComplete
        // always fires; each entry is already gone by the time a publication
        // exception escapes TryClose (the host's Cleanup ran first), so
        // proceeding to InteractionComplete is safe.
        try
        {
            CloseDialoguePresentation(UIScreenCloseReason.Programmatic);
            CloseShopPresentation(UIScreenCloseReason.Programmatic);
            CloseHealPresentation(UIScreenCloseReason.Programmatic);
        }
        catch (Exception ex)
        {
            GD.PushError($"[NpcInteractionController] Cleanup during finish failed: {ex.Message}");
        }
        InteractionComplete?.Invoke();
    }
}
