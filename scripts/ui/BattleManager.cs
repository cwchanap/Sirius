using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattleManager : Control
{
    [Signal] public delegate void BattleFinishedEventHandler(bool playerWon, bool playerEscaped);
    [Signal] public delegate void DismissRequestedEventHandler();

    private enum BattlePhase
    {
        Preparation,
        AutomaticCombat,
        Result
    }

    private BattlePhase _phase = BattlePhase.Preparation;
    private bool _dismissRequested;
    
    private Character _player = null!;
    private Enemy _enemy = null!;
    private bool _playerTurn = true;

    // Action point system for speed-based turn frequency
    private float _playerActionPoints = 0f;
    private float _enemyActionPoints = 0f;
    private const float ACTION_POINT_THRESHOLD = 100f;

    // UI References. The battle screen is scene-authored; this controller only
    // binds behavior and responsive presentation to those stable nodes.
    private Control _safeFrame = null!;
    private Control _preparationPanel = null!;
    private Control _automaticCombatPanel = null!;
    private Control _resultPanel = null!;
    private Control _cureOverlay = null!;
    private Label _playerNameLabel = null!;
    private Label _playerLevelLabel = null!;
    private Label _playerAttack = null!;
    private Label _playerDefense = null!;
    private Label _playerSpeed = null!;
    private Label _enemyNameLabel = null!;
    private Label _enemyLevelLabel = null!;
    private Label _enemyAttack = null!;
    private Label _enemyDefense = null!;
    private Label _enemySpeed = null!;
    private Label _playerStatus = null!;
    private Label _enemyStatus = null!;
    private Label _phaseLabel = null!;
    private Label _activeSkillSummary = null!;
    private Label _preparationItemDetails = null!;
    private Label _currentActionLabel = null!;
    private Label _eventFeed = null!;
    private Label _resultTitle = null!;
    private Label _experienceResult = null!;
    private Label _goldResult = null!;
    private Label _levelResult = null!;
    private Label _lootResultList = null!;
    private ProgressBar _automaticActionProgress = null!;
    private SiriusStatBar _playerHealth = null!;
    private SiriusStatBar _playerMana = null!;
    private SiriusStatBar _enemyHealth = null!;
    private Container _preparationItemRail = null!;
    private Container _cureItemList = null!;
    private VBoxContainer _preparationContent = null!;
    private Button? _beginBattleButton;
    private Button? _cureButton;
    private Button? _continueButton;
    private Button? _escapeButton;
    private Button? _previousItemPage;
    private Button? _nextItemPage;
    private Button? _clearPreparationItemButton;
    private Button? _previousCurePage;
    private Button? _nextCurePage;
    private Button? _cancelCureButton;
    
    // Animation and Visual References
    private AnimatedSprite2D? _playerSprite;
    private AnimatedSprite2D? _enemySprite;
    private Label? _playerDamageLabel;
    private Label? _enemyDamageLabel;
    private Vector2 _playerDamageRestingPosition;
    private Vector2 _enemyDamageRestingPosition;
    private Vector2 _playerSpriteRestingScale = Vector2.One;
    private Vector2 _enemySpriteRestingScale = Vector2.One;
    private bool _reducedMotionEnabled;

    // Auto-battle properties
    private Timer _battleTimer = null!;
    private bool _battleInProgress = false;
    private bool _playerDefendedLastTurn = false;
    private bool _resultEmitted = false; // Guards against duplicate BattleFinished emissions.
    private readonly Random _rng = new();
    private bool _playerActedLast = false;

    // Refresh-scoped item presentation state. Never retain inventory entries
    // across a refresh; only the selected consumable itself is battle-scoped.
    private PackedScene _itemSlotScene = null!;
    private readonly List<SiriusItemSlotController> _preparationSlots = new();
    private readonly Dictionary<SiriusItemSlotController, ConsumableItem> _preparationItemBySlot = new();
    private readonly List<SiriusItemSlotController> _cureSlots = new();
    private readonly Dictionary<SiriusItemSlotController, ConsumableItem> _cureItemBySlot = new();
    private List<ConsumableItem> _preparationItems = new();
    private int _preparationPage;
    private int _curePage;
    private ConsumableItem? _selectedConsumable;
    private string? _preparationErrorMessage;

    private bool _isCompact;
    private readonly Queue<string> _combatEvents = new();
    private int EventFeedLimit => _isCompact ? 3 : 5;
    private readonly HashSet<Tween> _visualTweens = new();

    // Skill tracking (battle-scoped)
    private int _playerSkillTurnCount = 0;
    private readonly Dictionary<string, int> _passiveSkillCooldowns = new(); // skillId → turns until next fire
    public Control? InitialFocusTarget => _phase switch
    {
        BattlePhase.Preparation => _beginBattleButton,
        BattlePhase.AutomaticCombat => _cureOverlay.Visible
            ? FindFirstCureFocusTarget()
            : _escapeButton,
        BattlePhase.Result when _continueButton is { Visible: true, Disabled: false } => _continueButton,
        BattlePhase.Result => null,
        _ => null
    };

    public BattleResultSummary? ResolvedResult { get; private set; }

    private Control FindFirstCureFocusTarget()
    {
        foreach (var slot in _cureSlots)
        {
            if (GodotObject.IsInstanceValid(slot) && slot.IsVisibleInTree() &&
                !slot.Disabled && slot.Actionable)
                return slot;
        }

        return _cancelCureButton ?? _escapeButton!;
    }

    private void RequestDismiss()
    {
        if (_phase == BattlePhase.Result && ResolvedResult?.PlayerWon != true)
            return;

        if (_dismissRequested)
            return;

        _dismissRequested = true;
        EmitSignal(SignalName.DismissRequested);
    }

    private void EmitBattleFinishedOnce(bool playerWon, bool playerEscaped)
    {
        if (_resultEmitted)
            return;

        _resultEmitted = true;
        EmitSignal(SignalName.BattleFinished, playerWon, playerEscaped);
    }

    private void StopBattleRuntime()
    {
        if (_battleTimer != null && IsInstanceValid(_battleTimer))
            _battleTimer.Stop();

        _battleInProgress = false;
        _player?.ActiveBuffs.Clear();
        _enemy?.ActiveStatusEffects.Clear();
        KillVisualTweens();
        ResetVisualFeedback();
    }

    private void CaptureRestingVisualState()
    {
        if (_playerDamageLabel != null)
            _playerDamageRestingPosition = _playerDamageLabel.Position;
        if (_enemyDamageLabel != null)
            _enemyDamageRestingPosition = _enemyDamageLabel.Position;
        if (_playerSprite != null)
            _playerSpriteRestingScale = _playerSprite.Scale;
        if (_enemySprite != null)
            _enemySpriteRestingScale = _enemySprite.Scale;
    }

    private void ResetVisualFeedback()
    {
        if (_playerDamageLabel != null)
        {
            _playerDamageLabel.Position = _playerDamageRestingPosition;
            _playerDamageLabel.Modulate = new Color(1, 0, 0, 0);
        }
        if (_enemyDamageLabel != null)
        {
            _enemyDamageLabel.Position = _enemyDamageRestingPosition;
            _enemyDamageLabel.Modulate = new Color(1, 0, 0, 0);
        }
        if (_playerSprite != null)
            _playerSprite.Scale = _playerSpriteRestingScale;
        if (_enemySprite != null)
            _enemySprite.Scale = _enemySpriteRestingScale;
    }

    private Tween CreateTrackedTween()
    {
        var tween = CreateTween();
        _visualTweens.Add(tween);
        tween.Finished += () => _visualTweens.Remove(tween);
        return tween;
    }

    private void KillVisualTweens()
    {
        foreach (var tween in _visualTweens.ToArray())
            tween.Kill();
        _visualTweens.Clear();
    }

    public override void _ExitTree()
    {
        StopBattleRuntime();
    }
    
    public override void _Ready()
    {
        GD.Print("BattleManager _Ready called");
        BindNodes();
        _itemSlotScene = GD.Load<PackedScene>("res://scenes/ui/components/SiriusItemSlot.tscn")
            ?? throw new InvalidOperationException("Failed to load SiriusItemSlot.tscn.");

        _playerSprite = GetNodeOrNull<AnimatedSprite2D>("%PlayerSpriteContainer/PlayerSprite");
        _enemySprite = GetNodeOrNull<AnimatedSprite2D>("%EnemySpriteContainer/EnemySprite");
        _playerDamageLabel = GetNodeOrNull<Label>("%PlayerDamageLabel");
        _enemyDamageLabel = GetNodeOrNull<Label>("%EnemyDamageLabel");
        CaptureRestingVisualState();

        if (_playerSprite == null)
            GD.PushWarning("[BattleManager] PlayerSprite not found — attack animation visuals will be skipped.");
        if (_enemySprite == null)
            GD.PushWarning("[BattleManager] EnemySprite not found — attack animation visuals will be skipped.");

        var playerContainer = GetNodeOrNull<Control>("%PlayerSpriteContainer");
        var enemyContainer = GetNodeOrNull<Control>("%EnemySpriteContainer");
        if (playerContainer != null)
            playerContainer.Resized += () => PositionPlayerSprite(playerContainer);
        if (enemyContainer != null)
            enemyContainer.Resized += () => PositionEnemySprite(enemyContainer);
        CenterSprites();

        _beginBattleButton!.Pressed += OnStartButtonPressed;
        _cureButton!.Pressed += OpenCureOverlay;
        _continueButton!.Pressed += RequestDismiss;
        _escapeButton!.Pressed += RequestCancel;
        _previousItemPage!.Pressed += () => ChangePreparationPage(-1);
        _nextItemPage!.Pressed += () => ChangePreparationPage(1);
        _clearPreparationItemButton!.Pressed += ClearPreparationSelection;
        _previousCurePage!.Pressed += () => ChangeCurePage(-1);
        _nextCurePage!.Pressed += () => ChangeCurePage(1);
        _cancelCureButton!.Pressed += CloseCureOverlay;

        var viewport = GetViewport();
        if (viewport != null)
            viewport.SizeChanged += RefreshLayout;

        if (_playerDamageLabel != null)
            _playerDamageLabel.Modulate = new Color(1, 0, 0, 0);
        if (_enemyDamageLabel != null)
            _enemyDamageLabel.Modulate = new Color(1, 0, 0, 0);

        _battleTimer = new Timer();
        _battleTimer.WaitTime = 1.5; // 1.5 seconds between actions for visual feedback
        _battleTimer.Timeout += OnBattleTurnTimer;
        AddChild(_battleTimer);

        _phase = BattlePhase.Preparation;
        _dismissRequested = false;
        RefreshLayout();
        SetPhasePresentation();
    }

    private void BindNodes()
    {
        _safeFrame = GetNode<Control>("%SafeFrame");
        _preparationPanel = GetNode<Control>("%PreparationPanel");
        _automaticCombatPanel = GetNode<Control>("%AutomaticCombatPanel");
        _resultPanel = GetNode<Control>("%ResultPanel");
        _cureOverlay = GetNode<Control>("%CureOverlay");
        _playerNameLabel = GetNode<Label>("%PlayerName");
        _playerLevelLabel = GetNode<Label>("%PlayerLevel");
        _playerAttack = GetNode<Label>("%PlayerAttack");
        _playerDefense = GetNode<Label>("%PlayerDefense");
        _playerSpeed = GetNode<Label>("%PlayerSpeed");
        _enemyNameLabel = GetNode<Label>("%EnemyName");
        _enemyLevelLabel = GetNode<Label>("%EnemyLevel");
        _enemyAttack = GetNode<Label>("%EnemyAttack");
        _enemyDefense = GetNode<Label>("%EnemyDefense");
        _enemySpeed = GetNode<Label>("%EnemySpeed");
        _playerStatus = GetNode<Label>("%PlayerStatus");
        _enemyStatus = GetNode<Label>("%EnemyStatus");
        _phaseLabel = GetNode<Label>("%PhaseLabel");
        _activeSkillSummary = GetNode<Label>("%ActiveSkillSummary");
        _preparationItemDetails = GetNode<Label>("%PreparationItemDetails");
        _currentActionLabel = GetNode<Label>("%CurrentActionLabel");
        _eventFeed = GetNode<Label>("%EventFeed");
        _resultTitle = GetNode<Label>("%ResultTitle");
        _experienceResult = GetNode<Label>("%ExperienceResult");
        _goldResult = GetNode<Label>("%GoldResult");
        _levelResult = GetNode<Label>("%LevelResult");
        _lootResultList = GetNode<Label>("%LootResultList");
        _automaticActionProgress = GetNode<ProgressBar>("%AutomaticActionProgress");
        _playerHealth = GetNode<SiriusStatBar>("%PlayerHealth");
        _playerMana = GetNode<SiriusStatBar>("%PlayerMana");
        _enemyHealth = GetNode<SiriusStatBar>("%EnemyHealth");
        _preparationItemRail = GetNode<Container>("%PreparationItemRail");
        _cureItemList = GetNode<Container>("%CureItemList");
        _preparationContent = GetNode<VBoxContainer>("%PreparationContent");
        _beginBattleButton = GetNode<Button>("%BeginBattleButton");
        _cureButton = GetNode<Button>("%CureButton");
        _continueButton = GetNode<Button>("%ContinueButton");
        _escapeButton = GetNode<Button>("%EscapeButton");
        _previousItemPage = GetNode<Button>("%PreviousItemPage");
        _nextItemPage = GetNode<Button>("%NextItemPage");
        _clearPreparationItemButton = GetNode<Button>("%ClearPreparationItemButton");
        _previousCurePage = GetNode<Button>("%PreviousCurePage");
        _nextCurePage = GetNode<Button>("%NextCurePage");
        _cancelCureButton = GetNode<Button>("%CancelCureButton");
    }
    
    public void RequestCancel()
    {
        if (_cureOverlay.Visible)
        {
            CloseCureOverlay();
            return;
        }

        if (_phase == BattlePhase.Result)
        {
            if (ResolvedResult?.PlayerWon == true)
                RequestDismiss();
            return;
        }

        StopBattleRuntime();
        EmitBattleFinishedOnce(false, true);
        RequestDismiss();
    }

    public override void _Process(double delta)
    {
        if (_phase == BattlePhase.AutomaticCombat &&
            _battleTimer != null && IsInstanceValid(_battleTimer) &&
            !_battleTimer.IsStopped() && _battleTimer.WaitTime > 0)
        {
            _automaticActionProgress.Value = Mathf.Clamp(
                1.0 - (_battleTimer.TimeLeft / _battleTimer.WaitTime), 0.0, 1.0);
        }
        else if (_automaticActionProgress != null)
        {
            _automaticActionProgress.Value = 0;
        }
    }

    private void RefreshLayout()
    {
        if (!GodotObject.IsInstanceValid(this) || _safeFrame == null || !IsInsideTree())
            return;

        var insets = SiriusUiMetrics.SafeFrameInsets(GetViewportRect().Size);
        _isCompact = insets.Compact;
        var minimumTarget = SiriusUiMetrics.MinimumTarget(_isCompact);

        _preparationContent.AddThemeConstantOverride("separation", _isCompact ? 2 : 6);
        _previousItemPage!.CustomMinimumSize = minimumTarget;
        _nextItemPage!.CustomMinimumSize = minimumTarget;
        _clearPreparationItemButton!.CustomMinimumSize = minimumTarget;
        _previousCurePage!.CustomMinimumSize = minimumTarget;
        _nextCurePage!.CustomMinimumSize = minimumTarget;
        _cancelCureButton!.CustomMinimumSize = minimumTarget;
        _cureItemList.CustomMinimumSize = new Vector2(0, SiriusUiMetrics.ItemSlotSize(_isCompact).Y);

        _safeFrame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _safeFrame.OffsetLeft = insets.SideInset;
        _safeFrame.OffsetTop = insets.Margin;
        _safeFrame.OffsetRight = -insets.SideInset;
        _safeFrame.OffsetBottom = -insets.Margin;

        _playerHealth.Compact = _isCompact;
        _playerMana.Compact = _isCompact;
        _enemyHealth.Compact = _isCompact;
        foreach (var slot in _preparationSlots)
            slot.SetCompact(_isCompact);
        foreach (var slot in _cureSlots)
            slot.SetCompact(_isCompact);

        bool showPrepTelemetry = !_isCompact && _phase == BattlePhase.Preparation;
        _playerAttack.Visible = showPrepTelemetry;
        _playerDefense.Visible = showPrepTelemetry;
        _playerSpeed.Visible = showPrepTelemetry;
        _enemyAttack.Visible = showPrepTelemetry;
        _enemyDefense.Visible = showPrepTelemetry;
        _enemySpeed.Visible = showPrepTelemetry;

        if (_player != null)
        {
            RefreshPreparationItemsDeferred();
            if (_cureOverlay.Visible)
                RefreshCureItemsDeferred();
        }
        RefreshEventFeed();
        SetPhasePresentation();
    }

    private void SetPhasePresentation()
    {
        if (_safeFrame == null)
            return;

        _preparationPanel.Visible = _phase == BattlePhase.Preparation && !_cureOverlay.Visible;
        _automaticCombatPanel.Visible = _phase == BattlePhase.AutomaticCombat && !_cureOverlay.Visible;
        _resultPanel.Visible = _phase == BattlePhase.Result;
        _cureButton.Visible = _phase == BattlePhase.AutomaticCombat && !_cureOverlay.Visible;
        _escapeButton.Visible = _phase == BattlePhase.AutomaticCombat && !_cureOverlay.Visible;
        _beginBattleButton.Visible = _phase == BattlePhase.Preparation;
        bool resultCanDismiss = _phase == BattlePhase.Result && ResolvedResult?.PlayerWon == true;
        _continueButton.Visible = resultCanDismiss;
        _continueButton.Disabled = !resultCanDismiss;
        _automaticActionProgress.Value = _phase == BattlePhase.AutomaticCombat ? _automaticActionProgress.Value : 0;
        _phaseLabel.Text = _phase switch
        {
            BattlePhase.Preparation => "PREPARATION",
            BattlePhase.AutomaticCombat => "AUTOMATIC COMBAT",
            BattlePhase.Result => "RESULTS",
            _ => string.Empty
        };
    }

    private void AppendCombatEvent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        _combatEvents.Enqueue(text);
        RefreshEventFeed();
    }

    private void RefreshEventFeed()
    {
        while (_combatEvents.Count > EventFeedLimit)
            _combatEvents.Dequeue();
        if (_eventFeed != null)
            _eventFeed.Text = string.Join("\n", _combatEvents);
    }
    
    /// <summary>
    /// Initializes the battle with the given combatants and sets up UI.
    /// Player goes first if Speed &gt;= enemy Speed (ties favor the player).
    /// </summary>
    public void StartBattle(Character player, Enemy enemy, bool reducedMotionEnabled)
    {
        if (player == null || enemy == null)
        {
            GD.PrintErr($"[BattleManager] StartBattle called with null {(player == null ? "player" : "enemy")}; aborting battle.");
            EmitBattleFinishedOnce(false, true);
            RequestDismiss();
            return;
        }

        GD.Print($"BattleManager.StartBattle called: {player.Name} vs {enemy.Name}");

        _reducedMotionEnabled = reducedMotionEnabled;
        _player = player;
        _enemy = enemy;
        _playerTurn = true; // Placeholder; determined after pre-battle consumables in OnStartButtonPressed()
        _playerActedLast = false; // Reset turn tracking for dynamic speed-based turn order
        _battleInProgress = false;
        _phase = BattlePhase.Preparation;
        _dismissRequested = false;
        _resultEmitted = false;
        ResolvedResult = null;

        if (_continueButton != null)
        {
            _continueButton.Visible = false;
            _continueButton.Disabled = true;
        }
        if (_escapeButton != null)
            _escapeButton.Visible = false;
        if (_cureOverlay != null)
            _cureOverlay.Visible = false;

        // Initialize action points for speed-based turn frequency
        _playerActionPoints = 0f;
        _enemyActionPoints = 0f;
        _preparationPage = 0;
        _curePage = 0;
        _combatEvents.Clear();

        // Reset battle-scoped skill state
        _playerSkillTurnCount = 0;
        _passiveSkillCooldowns.Clear();

        // Setup character animations
        SetupCharacterAnimations();
        CaptureRestingVisualState();
        
        GD.Print($"Battle begins! {_player.Name} vs {_enemy.Name}");
        GD.Print($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first!");
        GD.Print("Auto-battle mode: Click Start to begin.");
        
        UpdateUI();
        _selectedConsumable = null;
        _preparationErrorMessage = null;
        _preparationItemDetails.Text = string.Empty;

        RefreshPreparationItemsDeferred();
        SetPhasePresentation();
    }

    private void RefreshPreparationItemsDeferred() => CallDeferred(nameof(RefreshPreparationItems));

    private void RefreshCureItemsDeferred() => CallDeferred(nameof(RefreshCureItems));

    private void RefreshPreparationItems()
    {
        if (_player == null || _preparationItemRail == null)
            return;

        _preparationItems = _player.Inventory.GetAllEntries()
            .Select(entry => entry.Item)
            .OfType<ConsumableItem>()
            .ToList();
        var pageSize = _isCompact ? 3 : 4;
        var pageCount = Math.Max(1, (_preparationItems.Count + pageSize - 1) / pageSize);
        _preparationPage = Math.Clamp(_preparationPage, 0, pageCount - 1);
        var pageItems = _preparationItems
            .Skip(_preparationPage * pageSize)
            .Take(pageSize)
            .ToList();

        var reconciliation = ReconcileSlots(_preparationItemRail, _preparationSlots, pageItems.Count,
            _preparationItemBySlot, OnPreparationItemPressed, OnPreparationItemFocused);
        for (var index = 0; index < pageItems.Count; index++)
        {
            var item = pageItems[index];
            var slot = _preparationSlots[index];
            _preparationItemBySlot[slot] = item;
            var quantity = _player.Inventory.GetQuantity(item.Id);
            var selected = _selectedConsumable?.Id == item.Id;
            slot.SetCompact(_isCompact);
            slot.Disabled = false;
            slot.PresentItem(
                item.LoadAssetOrDefault<Texture2D>(),
                quantity > 1 ? $"×{quantity}" : string.Empty,
                selected ? "SELECTED" : string.Empty,
                BuildConsumableTooltip(item),
                selected ? SiriusItemSlotVisualState.Equipped : SiriusItemSlotVisualState.Available);
        }

        _preparationItemDetails.Visible = true;
        if (_preparationErrorMessage != null)
            _preparationItemDetails.Text = _preparationErrorMessage;
        else if (_preparationItems.Count == 0)
            _preparationItemDetails.Text = "No consumables in inventory. Begin without one.";
        else if (_selectedConsumable != null)
            _preparationItemDetails.Text = $"Selected: {_selectedConsumable.DisplayName}";
        else if (string.IsNullOrWhiteSpace(_preparationItemDetails.Text))
            _preparationItemDetails.Text = "Choose an optional consumable, or begin without one.";

        // Resolve focus after the new bindings are installed. Responsive
        // repaging (standard↔compact) can rebind a surviving focused slot to a
        // different item without firing FocusEntered; in that case keep focus
        // on the slot but manually refresh %PreparationItemDetails to the new
        // binding so focus, details, and activation stay coherent.
        ApplyReconciledFocus(reconciliation, _preparationSlots, _preparationItemBySlot, OnPreparationItemFocused);

        _previousItemPage!.Visible = pageCount > 1;
        _nextItemPage!.Visible = pageCount > 1;
        _previousItemPage.Disabled = _preparationPage == 0;
        _nextItemPage.Disabled = _preparationPage >= pageCount - 1;
    }

    private SlotReconciliation ReconcileSlots(
        Container parent,
        List<SiriusItemSlotController> slots,
        int requiredCount,
        Dictionary<SiriusItemSlotController, ConsumableItem> bindings,
        Action<ConsumableItem> activated,
        Action<ConsumableItem>? focused = null)
    {
        // Capture the currently-focused slot and its bound item before any
        // mutation. Responsive repaging (standard↔compact) can rebind a
        // surviving focused slot to a different item without removing it; in
        // that case FocusEntered never fires and %PreparationItemDetails would
        // keep describing the old item while activation uses the new one. The
        // caller resolves the correct focus target after installing the new
        // page bindings (see ApplyReconciledFocus).
        ConsumableItem? focusedItem = null;
        SiriusItemSlotController? focusedSlot = null;
        foreach (var kvp in bindings)
        {
            if (GodotObject.IsInstanceValid(kvp.Key) && kvp.Key.HasFocus())
            {
                focusedSlot = kvp.Key;
                focusedItem = kvp.Value;
                break;
            }
        }

        while (slots.Count < requiredCount)
        {
            var slot = _itemSlotScene.Instantiate<SiriusItemSlotController>();
            parent.AddChild(slot);
            var captured = slot;
            slot.Activated += () =>
            {
                if (bindings.TryGetValue(captured, out var item))
                    activated(item);
            };
            if (focused != null)
            {
                slot.FocusEntered += () =>
                {
                    if (bindings.TryGetValue(captured, out var item))
                        focused(item);
                };
            }
            slots.Add(slot);
        }

        // If a slot that currently owns keyboard/controller focus is about to be
        // removed by the shrink below, remember it so focus can be moved to the
        // nearest surviving slot afterward. Otherwise Godot's focus-owner-based
        // UI navigation is left without an owner until the next explicit focus
        // transition.
        Control? focusToRestore = null;
        while (slots.Count > requiredCount)
        {
            var slot = slots[^1];
            if (focusToRestore == null && GodotObject.IsInstanceValid(slot) && slot.HasFocus())
                focusToRestore = slot;
            slots.RemoveAt(slots.Count - 1);
            bindings.Remove(slot);
            parent.RemoveChild(slot);
            slot.QueueFree();
        }

        bindings.Clear();
        for (var index = 0; index < slots.Count; index++)
            slots[index].SetCompact(_isCompact);

        // Report the removed-slot fallback (nearest surviving slot) rather than
        // calling GrabFocus here. GrabFocus fires FocusEntered synchronously,
        // which resolves the focused item through `bindings`; but `bindings`
        // was just cleared and is repopulated by the caller AFTER this method
        // returns. Calling GrabFocus now would fire FocusEntered while
        // `bindings` is empty, so the focused callback is skipped and the
        // details panel keeps describing the removed item. The caller applies
        // focus once the new bindings are installed.
        SiriusItemSlotController? removedSlotFallback = null;
        if (focusToRestore != null && slots.Count > 0)
            removedSlotFallback = slots[^1];

        return new SlotReconciliation(focusedItem, focusedSlot, removedSlotFallback);
    }

    /// <summary>
    /// Result of <see cref="ReconcileSlots"/>. The caller installs the new page
    /// bindings, then calls <see cref="ApplyReconciledFocus"/> to (re)establish
    /// focus on the slot bound to the previously-focused item, or the
    /// documented fallback when that item left the page.
    /// </summary>
    private readonly record struct SlotReconciliation(
        ConsumableItem? FocusedItem,
        SiriusItemSlotController? FocusedSlot,
        SiriusItemSlotController? RemovedSlotFallback);

    /// <summary>
    /// Resolves the slot that should own focus after a page rebuild. If the
    /// previously-focused item is still on the page, focus its (possibly new)
    /// slot so the user keeps the same item. Otherwise fall back to the nearest
    /// surviving slot when the focused slot was removed, or keep focus on the
    /// surviving focused slot (now rebound to a different item).
    /// </summary>
    private SiriusItemSlotController? ResolveReconciledFocus(
        SlotReconciliation reconciliation,
        List<SiriusItemSlotController> slots,
        Dictionary<SiriusItemSlotController, ConsumableItem> bindings)
    {
        if (reconciliation.FocusedItem == null)
            return null;

        foreach (var kvp in bindings)
        {
            if (kvp.Value.Id == reconciliation.FocusedItem.Id)
                return kvp.Key;
        }

        if (reconciliation.RemovedSlotFallback != null)
            return reconciliation.RemovedSlotFallback;
        if (reconciliation.FocusedSlot != null
            && GodotObject.IsInstanceValid(reconciliation.FocusedSlot)
            && slots.Contains(reconciliation.FocusedSlot))
            return reconciliation.FocusedSlot;
        return null;
    }

    /// <summary>
    /// Applies the reconciled focus target after new page bindings are
    /// installed. When the target already owns focus (a surviving slot that was
    /// rebound, or simply rebound to the same item), manually invoke
    /// <paramref name="onFocused"/> so the details panel matches the focused
    /// binding. This is required even when the binding did not change, because
    /// the caller (<see cref="RefreshPreparationItems"/>) overwrites
    /// %PreparationItemDetails with the selected-item text before this method
    /// runs; without re-pushing, a refresh with selected A and focused B would
    /// leave the details panel describing A while focus and activation use B.
    /// <see cref="OnPreparationItemFocused"/> preserves persistent error
    /// messages, so re-invoking it is safe. When the target is a different
    /// slot, GrabFocus fires FocusEntered, which resolves the item through the
    /// bindings.
    /// </summary>
    private void ApplyReconciledFocus(
        SlotReconciliation reconciliation,
        List<SiriusItemSlotController> slots,
        Dictionary<SiriusItemSlotController, ConsumableItem> bindings,
        Action<ConsumableItem>? onFocused = null)
    {
        var target = ResolveReconciledFocus(reconciliation, slots, bindings);
        if (target == null)
            return;

        if (target.HasFocus())
        {
            if (onFocused != null
                && bindings.TryGetValue(target, out var boundItem))
                onFocused(boundItem);
        }
        else
        {
            target.GrabFocus();
        }
    }

    private string BuildConsumableTooltip(ConsumableItem item) =>
        $"{item.DisplayName}\n{item.Description}\n{item.EffectDescription}";

    private void OnPreparationItemPressed(ConsumableItem item)
    {
        _preparationErrorMessage = null;
        _selectedConsumable = item;
        _preparationItemDetails.Text = $"Selected: {item.DisplayName}";
        _preparationItemDetails.Visible = true;
        RefreshPreparationItemsDeferred();
    }

    private void OnPreparationItemFocused(ConsumableItem item)
    {
        // Per HPA-356 §7: focus updates %PreparationItemDetails. Selection
        // (Activated) and slot presentation are separate concerns, so focus
        // shows the focused item's details without changing _selectedConsumable.
        // Persistent error messages stay visible until the next selection
        // attempt clears them (see OnPreparationItemPressed).
        if (_preparationErrorMessage != null)
            return;
        _preparationItemDetails.Text = BuildConsumableTooltip(item);
        _preparationItemDetails.Visible = true;
    }

    private void ClearPreparationSelection()
    {
        _preparationErrorMessage = null;
        _selectedConsumable = null;
        _preparationItemDetails.Text = "No item selected. Begin without one.";
        _preparationItemDetails.Visible = true;
        RefreshPreparationItemsDeferred();
    }

    private void ChangePreparationPage(int direction)
    {
        _preparationPage += direction;
        RefreshPreparationItemsDeferred();
    }


    private void OnStartButtonPressed()
    {
        if (_battleInProgress) return;
        if (_player == null || _enemy == null)
        {
            GD.PrintErr("Start pressed but battle participants not initialized");
            return;
        }

        if (_selectedConsumable != null)
        {
            bool consumableApplied = false;
            ConsumableEffect? effect;
            try
            {
                effect = _selectedConsumable.Effect;
            }
            catch (InvalidOperationException)
            {
                ShowPreparationError($"Could not use {_selectedConsumable.DisplayName}: item effect is unavailable.");
                return;
            }

            if (effect is EnemyDebuffEffect enemyEffect)
            {
                if (_player.TryRemoveItem(_selectedConsumable.Id, 1))
                {
                    if (enemyEffect.ApplyToEnemy(_enemy))
                    {
                        UpdateUI();
                        GD.Print($"[BattleManager] Applied '{_selectedConsumable.DisplayName}' to {_enemy.Name}");
                        AppendCombatEvent($"{_selectedConsumable.DisplayName} applied to {_enemy.Name}.");
                        consumableApplied = true;
                    }
                    else
                    {
                        GD.PushWarning($"[BattleManager] '{_selectedConsumable.DisplayName}' was consumed but could not be applied to enemy, attempting rollback");
                        bool rollbackSuccess = _player.TryAddItem(_selectedConsumable, 1, out _);
                        UpdateUI();
                        if (!rollbackSuccess)
                        {
                            GD.PrintErr($"[BattleManager] ROLLBACK FAILED for '{_selectedConsumable.DisplayName}' — item lost permanently!");
                            ShowPreparationError($"Error: {_selectedConsumable.DisplayName} was lost. This is a bug — please report it.");
                            return;
                        }

                        ShowPreparationError($"Could not apply {_selectedConsumable.DisplayName} to {_enemy.Name}. Item returned.");
                        return;
                    }
                }
                else
                {
                    GD.PushWarning($"[BattleManager] Could not consume '{_selectedConsumable.DisplayName}'; effect not applied to {_enemy.Name}");
                    ShowPreparationError($"Could not use {_selectedConsumable.DisplayName}.");
                    return;
                }
            }
            else
            {
                if (_player.TryRemoveItem(_selectedConsumable.Id, 1))
                {
                    if (_selectedConsumable.Apply(_player))
                    {
                        UpdateUI();
                        GD.Print($"[BattleManager] Applied '{_selectedConsumable.DisplayName}' to {_player.Name}");
                        AppendCombatEvent($"{_selectedConsumable.DisplayName} applied.");
                        consumableApplied = true;
                    }
                    else
                    {
                        GD.PushWarning($"[BattleManager] '{_selectedConsumable.DisplayName}' was consumed but could not be applied, attempting rollback");
                        bool rollbackSuccess = _player.TryAddItem(_selectedConsumable, 1, out _);
                        UpdateUI();
                        if (!rollbackSuccess)
                        {
                            GD.PrintErr($"[BattleManager] ROLLBACK FAILED for '{_selectedConsumable.DisplayName}' — item lost permanently!");
                            ShowPreparationError($"Error: {_selectedConsumable.DisplayName} was lost. This is a bug — please report it.");
                            return;
                        }
                        else
                        {
                            ShowPreparationError($"Could not apply {_selectedConsumable.DisplayName}. Item returned.");
                            return;
                        }
                    }
                }
                else
                {
                    GD.PushWarning($"[BattleManager] Could not consume '{_selectedConsumable.DisplayName}'; effect not applied");
                    ShowPreparationError($"Could not use {_selectedConsumable.DisplayName}.");
                    return;
                }
            }

            if (consumableApplied)
                _selectedConsumable = null;
        }

        _preparationErrorMessage = null;

        // Determine turn order using effective speed (accounts for pre-battle consumables)
        // If speeds are equal, alternate based on who acted last to avoid AP starvation
        int playerSpeed = _player.GetEffectiveSpeed();
        int enemySpeed = _enemy.GetEffectiveSpeed();
        if (playerSpeed == enemySpeed)
        {
            _playerTurn = !_playerActedLast; // Alternate turn order on ties
        }
        else
        {
            _playerTurn = playerSpeed > enemySpeed;
        }
        _playerActedLast = !_playerTurn; // Record who acts first so the AP tie-breaker alternates on the next tie
        GD.Print($"Turn order: {(_playerTurn ? "Player" : "Enemy")} goes first! (Player SPD: {_player.GetEffectiveSpeed()}, Enemy SPD: {_enemy.GetEffectiveSpeed()})");

        _battleInProgress = true;
        _phase = BattlePhase.AutomaticCombat;
        _currentActionLabel.Text = $"{(_playerTurn ? _player.Name : _enemy.Name)} acts first.";
        SetPhasePresentation();
        _escapeButton!.GrabFocus();
        GD.Print("Battle started by user");
        AppendCombatEvent("Automatic combat started.");
        _battleTimer.Start();
    }

    private void ShowPreparationError(string message)
    {
        _preparationErrorMessage = message;
        _preparationItemDetails.Text = message;
        _preparationItemDetails.Visible = true;
        _phase = BattlePhase.Preparation;
        _cureOverlay.Visible = false;
        SetPhasePresentation();
        _automaticActionProgress.Value = 0;
        if (_battleTimer != null && IsInstanceValid(_battleTimer))
            _battleTimer.Stop();
    }

    private void OpenCureOverlay()
    {
        if (!_battleInProgress || _phase != BattlePhase.AutomaticCombat)
            return;
        _battleTimer.Stop();
        _curePage = 0;
        _cureOverlay.Visible = true;
        SetPhasePresentation();
        CallDeferred(nameof(PopulateCureOverlayAndFocus));
    }

    private void PopulateCureOverlayAndFocus()
    {
        if (!_cureOverlay.Visible)
            return;
        RefreshCureItems();
        FindFirstCureFocusTarget().GrabFocus();
    }

    private void CloseCureOverlay()
    {
        _cureOverlay.Visible = false;
        SetPhasePresentation();
        if (_phase == BattlePhase.AutomaticCombat && _battleInProgress &&
            _player.IsAlive && _enemy.IsAlive)
        {
            _battleTimer.Start();
            _cureButton.GrabFocus();
        }
    }

    private void RefreshCureItems()
    {
        if (_player == null)
            return;

        var items = _player.Inventory.GetAllEntries()
            .Select(entry => entry.Item)
            .OfType<ConsumableItem>()
            .ToList();
        var pageSize = _isCompact ? 3 : 4;
        var pageCount = Math.Max(1, (items.Count + pageSize - 1) / pageSize);
        _curePage = Math.Clamp(_curePage, 0, pageCount - 1);
        var pageItems = items
            .Skip(_curePage * pageSize)
            .Take(pageSize)
            .ToList();

        var cureReconciliation = ReconcileSlots(_cureItemList, _cureSlots, pageItems.Count, _cureItemBySlot, OnCombatItemSelected);
        for (var index = 0; index < pageItems.Count; index++)
        {
            var item = pageItems[index];
            var slot = _cureSlots[index];
            _cureItemBySlot[slot] = item;
            var isCureItem = false;
            try
            {
                isCureItem = item.Effect is CureStatusEffect;
            }
            catch (InvalidOperationException)
            {
            }

            var quantity = _player.Inventory.GetQuantity(item.Id);
            slot.SetCompact(_isCompact);
            slot.Disabled = !isCureItem;
            slot.PresentItem(
                item.LoadAssetOrDefault<Texture2D>(),
                quantity > 1 ? $"×{quantity}" : string.Empty,
                isCureItem ? string.Empty : "BATTLE START ONLY",
                isCureItem
                    ? BuildConsumableTooltip(item)
                    : $"{BuildConsumableTooltip(item)}\nBATTLE START ONLY",
                isCureItem ? SiriusItemSlotVisualState.Available : SiriusItemSlotVisualState.Unsupported);
        }

        // Resolve focus after the new bindings are installed (see
        // RefreshPreparationItems). The Cure page has no details panel, so no
        // onFocused callback is needed; the identity rule still keeps focus on
        // the slot bound to the previously-focused item when it remains on the
        // page, or the documented fallback otherwise.
        ApplyReconciledFocus(cureReconciliation, _cureSlots, _cureItemBySlot);

        _previousCurePage!.Visible = pageCount > 1;
        _nextCurePage!.Visible = pageCount > 1;
        _previousCurePage.Disabled = _curePage == 0;
        _nextCurePage.Disabled = _curePage >= pageCount - 1;
    }

    private void ChangeCurePage(int direction)
    {
        _curePage += direction;
        RefreshCureItemsDeferred();
    }

    private void OnCombatItemSelected(ConsumableItem item)
    {
        if (_player == null || !_player.IsAlive)
            return;

        // Non-cure selections leave the overlay open; they are presented as
        // disabled "BATTLE START ONLY" slots and should never reach here.
        if (item.Effect is not CureStatusEffect cureEffect)
            return;

        if (!_player.TryRemoveItem(item.Id, 1))
        {
            GD.PushWarning($"[BattleManager] Could not consume '{item.DisplayName}'; item not removed.");
            return;
        }

        if (cureEffect.Apply(_player))
        {
            UpdateUI();
            AppendCombatEvent($"{item.DisplayName} cured status effects.");
        }
        else
        {
            var rollbackSuccess = _player.TryAddItem(item, 1, out _);
            if (!rollbackSuccess)
                GD.PrintErr($"[BattleManager] ROLLBACK FAILED for '{item.DisplayName}' — item lost permanently!");
            UpdateUI();
            return;
        }

        CloseCureOverlay();
    }

    private void CenterSprites()
    {
        var playerContainer = GetNodeOrNull<Control>("%PlayerSpriteContainer");
        var enemyContainer = GetNodeOrNull<Control>("%EnemySpriteContainer");
        if (playerContainer != null && enemyContainer != null)
            CallDeferred(nameof(PositionSpritesInCenter), playerContainer, enemyContainer);
    }
    
    private void PositionSpritesInCenter(Control playerContainer, Control enemyContainer)
    {
        PositionPlayerSprite(playerContainer);
        PositionEnemySprite(enemyContainer);
    }
    
    private void PositionPlayerSprite(Control container)
    {
        if (_playerSprite != null && container.Size.X > 0 && container.Size.Y > 0)
        {
            var center = container.Size / 2;
            _playerSprite.Position = center;
        }
    }
    
    private void PositionEnemySprite(Control container)
    {
        if (_enemySprite != null && container.Size.X > 0 && container.Size.Y > 0)
        {
            var center = container.Size / 2;
            _enemySprite.Position = center;
        }
    }
    
    private void SetupCharacterAnimations()
    {
        if (_playerSprite == null)
        {
            GD.PushWarning("[BattleManager] SetupCharacterAnimations: Player sprite node missing; skipping player animation setup.");
        }

        // Create animation resources for player
        var playerSpriteFrames = new SpriteFrames();

        // Load player sprite sheet and create animation - with fallback
        var playerTexture = GD.Load<Texture2D>("res://assets/sprites/characters/player_hero/sprite_sheet.png");
        if (_playerSprite != null && playerTexture != null)
        {
            playerSpriteFrames.AddAnimation("idle");

            // Derive frame size dynamically from texture (4 frames horizontally)
            var pSize = playerTexture.GetSize();
            int PLAYER_FRAME_W = Mathf.Max(1, Mathf.RoundToInt(pSize.X) / 4);
            int PLAYER_FRAME_H = Mathf.Max(1, Mathf.RoundToInt(pSize.Y));
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = playerTexture;
                atlasTexture.Region = new Rect2(i * PLAYER_FRAME_W, 0, PLAYER_FRAME_W, PLAYER_FRAME_H);
                atlasTexture.FilterClip = true;
                playerSpriteFrames.AddFrame("idle", atlasTexture);
            }

            playerSpriteFrames.SetAnimationSpeed("idle", 4.0);
            playerSpriteFrames.SetAnimationLoop("idle", true);
            _playerSprite.SpriteFrames = playerSpriteFrames;
            // Keep on-screen size ~96px width regardless of source resolution
            float targetPx = 96f;
            float pScale = targetPx / (float)PLAYER_FRAME_W;
            _playerSprite.Scale = new Vector2(pScale, pScale);
            _playerSprite.Modulate = new Color(1, 1, 1, 1);
            if (_reducedMotionEnabled)
            {
                _playerSprite.Animation = "idle";
                _playerSprite.Frame = 0;
                _playerSprite.Stop();
            }
            else
            {
                _playerSprite.Play("idle");
            }

            var material = new CanvasItemMaterial();
            material.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            material.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _playerSprite.Material = material;

            _playerSprite.SelfModulate = new Color(1, 1, 1, 1);
            _playerSprite.Visible = true;
            _playerSprite.Centered = true;
        }
        else
        {
            if (playerTexture == null)
                GD.PushWarning("[BattleManager] Player sprite sheet not found; using fallback rendering.");
        }

        if (_enemySprite == null)
        {
            GD.PushWarning("[BattleManager] SetupCharacterAnimations: Enemy sprite node missing; skipping enemy animation setup.");
        }

        // Create animation resources for enemy
        var enemySpriteFrames = new SpriteFrames();

        // Load enemy sprite sheet and create animation - prefer new enemies/ path with fallback to legacy characters/
        Texture2D? enemyTexture = null;
        string newGoblinPath = "res://assets/sprites/enemies/goblin/sprite_sheet.png";
        string legacyGoblinPath = "res://assets/sprites/characters/enemy_goblin/sprite_sheet.png";
        if (FileAccess.FileExists(newGoblinPath))
        {
            enemyTexture = GD.Load<Texture2D>(newGoblinPath);
        }
        else if (FileAccess.FileExists(legacyGoblinPath))
        {
            enemyTexture = GD.Load<Texture2D>(legacyGoblinPath);
        }
        if (_enemySprite != null && enemyTexture != null)
        {
            enemySpriteFrames.AddAnimation("idle");

            // Derive frame size dynamically from texture (4 frames horizontally)
            var eSize = enemyTexture.GetSize();
            int ENEMY_FRAME_W = Mathf.Max(1, Mathf.RoundToInt(eSize.X) / 4);
            int ENEMY_FRAME_H = Mathf.Max(1, Mathf.RoundToInt(eSize.Y));
            for (int i = 0; i < 4; i++)
            {
                var atlasTexture = new AtlasTexture();
                atlasTexture.Atlas = enemyTexture;
                atlasTexture.Region = new Rect2(i * ENEMY_FRAME_W, 0, ENEMY_FRAME_W, ENEMY_FRAME_H);
                atlasTexture.FilterClip = true;
                enemySpriteFrames.AddFrame("idle", atlasTexture);
            }

            enemySpriteFrames.SetAnimationSpeed("idle", 4.0);
            enemySpriteFrames.SetAnimationLoop("idle", true);
            _enemySprite.SpriteFrames = enemySpriteFrames;
            // Keep on-screen size ~96px width regardless of source resolution
            float eScale = 96f / (float)ENEMY_FRAME_W;
            _enemySprite.Scale = new Vector2(eScale, eScale);
            _enemySprite.Modulate = new Color(1, 1, 1, 1);
            if (_reducedMotionEnabled)
            {
                _enemySprite.Animation = "idle";
                _enemySprite.Frame = 0;
                _enemySprite.Stop();
            }
            else
            {
                _enemySprite.Play("idle");
            }

            var enemyMaterial = new CanvasItemMaterial();
            enemyMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Mix;
            enemyMaterial.LightMode = CanvasItemMaterial.LightModeEnum.Unshaded;
            _enemySprite.Material = enemyMaterial;

            _enemySprite.SelfModulate = new Color(1, 1, 1, 1);
            _enemySprite.Visible = true;
            _enemySprite.Centered = true;
        }
        else
        {
            if (enemyTexture == null)
            {
                // TODO: use _enemy.EnemyType to select the correct sprite path (currently always loads goblin)
                GD.PushWarning("[BattleManager] Enemy sprite sheet not found; using fallback rendering.");
                // Check if there are sprite files that need to be merged
                CheckAndCreateSpriteSheet();
            }
        }
    }
    
    private void CheckAndCreateSpriteSheet()
    {
        // Check if individual sprite frames exist for goblin
        string newGoblinDir = "res://assets/sprites/enemies/goblin/";
        string legacyGoblinDir = "res://assets/sprites/characters/enemy_goblin/";
        string goblinDir = DirAccess.DirExistsAbsolute(newGoblinDir) ? newGoblinDir : legacyGoblinDir;
        if (DirAccess.DirExistsAbsolute(goblinDir))
        {
            GD.Print($"Goblin sprite directory exists: {goblinDir}");
            GD.Print("You may need to run: python3 tools/sprite_sheet_merger.py");
        }
    }
    
    private void UpdateUI()
    {
        if (_player == null || _enemy == null)
            return;

        _playerNameLabel.Text = _player.Name;
        _playerLevelLabel.Text = $"Lv {_player.Level}";
        _playerHealth.Current = _player.CurrentHealth;
        _playerHealth.Maximum = _player.GetEffectiveMaxHealth();
        _playerMana.Current = _player.CurrentMana;
        _playerMana.Maximum = _player.MaxMana;
        _playerAttack.Text = $"ATK: {_player.GetEffectiveAttack()}";
        _playerDefense.Text = $"DEF: {_player.GetEffectiveDefense()}";
        _playerSpeed.Text = $"SPD: {_player.GetEffectiveSpeed()}";

        _enemyNameLabel.Text = _enemy.Name;
        _enemyLevelLabel.Text = $"Lv {_enemy.Level}";
        _enemyHealth.Current = _enemy.CurrentHealth;
        _enemyHealth.Maximum = _enemy.MaxHealth;
        _enemyAttack.Text = $"ATK: {_enemy.GetEffectiveAttack()}";
        _enemyDefense.Text = $"DEF: {_enemy.GetEffectiveDefense()}";
        _enemySpeed.Text = $"SPD: {_enemy.GetEffectiveSpeed()}";

        var activeSkill = _player.GetActiveSkill();
        _activeSkillSummary.Text = activeSkill == null
            ? "No active skill equipped."
            : $"{activeSkill.DisplayName} auto-fires every {activeSkill.ActivePeriod} player turns (MP {activeSkill.ManaCost}).";

        _playerStatus.Text = BuildStatusText(_player.ActiveBuffs);
        _enemyStatus.Text = BuildStatusText(_enemy.ActiveStatusEffects);
        _playerStatus.Visible = _playerStatus.Text.Length > 0;
        _enemyStatus.Visible = _enemyStatus.Text.Length > 0;
    }

    // -------------------------------------------------------------------------
    // Status effect UI helpers
    // -------------------------------------------------------------------------

    private static string BuildStatusText(StatusEffectSet effects)
    {
        if (!effects.HasAny) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var eff in effects.Effects)
        {
            string tag = eff.Type switch
            {
                StatusEffectType.Poison   => "PSN",
                StatusEffectType.Burn     => "BRN",
                StatusEffectType.Stun     => "STN",
                StatusEffectType.Weaken   => "WKN",
                StatusEffectType.Slow     => "SLW",
                StatusEffectType.Blind    => "BLD",
                StatusEffectType.Regen    => "RGN",
                StatusEffectType.Haste    => "HST",
                StatusEffectType.Strength => "STR",
                StatusEffectType.Fortify  => "FRT",
                _                         => "???",
            };
            sb.Append($"[{tag} {eff.TurnsRemaining}t] ");
        }
        return sb.ToString().TrimEnd();
    }
    
    private void OnBattleTurnTimer()
    {
        if (!_battleInProgress || !_player.IsAlive || !_enemy.IsAlive)
        {
            _battleTimer.Stop();
            return;
        }

        // Action point system: speed determines turn frequency, not just initial priority
        // Accumulate action points based on effective speed each turn (scaled for ~1-2 tick actions)
        _playerActionPoints += _player.GetEffectiveSpeed() * 6f;
        _enemyActionPoints += _enemy.GetEffectiveSpeed() * 6f;

        bool actedThisTick = false;
        int safetyLimit = 20;
        int actionCount = 0;
        while (actionCount < safetyLimit)
        {
            bool playerReady = _playerActionPoints >= ACTION_POINT_THRESHOLD;
            bool enemyReady = _enemyActionPoints >= ACTION_POINT_THRESHOLD;

            if (!playerReady && !enemyReady)
                break;

            bool playerActs;
            if (playerReady && enemyReady)
            {
                float apGap = Mathf.Abs(_playerActionPoints - _enemyActionPoints);
                if (apGap < 0.001f)
                {
                    // Alternate exact AP ties to avoid deterministic tie starvation.
                    playerActs = !_playerActedLast;
                }
                else
                {
                    playerActs = _playerActionPoints > _enemyActionPoints;
                }
            }
            else
            {
                playerActs = playerReady;
            }

            _playerTurn = playerActs;
            actedThisTick = true;

            _currentActionLabel.Text = playerActs
                ? $"{_player.Name} acts."
                : $"{_enemy.Name} acts.";
            AppendCombatEvent(_currentActionLabel.Text);

            if (playerActs)
            {
                ExecutePlayerAction();
            }
            else
            {
                ExecuteEnemyAction();
            }

            _playerActedLast = playerActs;
            actionCount++;

            if (!_player.IsAlive || !_enemy.IsAlive)
                break;
        }
        if (actionCount >= safetyLimit)
            GD.PushWarning($"[BattleManager] Safety limit of {safetyLimit} actions reached in one tick — possible AP accumulation bug.");

        if (!actedThisTick)
        {
            UpdateUI();
            return;
        }

        UpdateUI();
        
        // Check for battle end conditions
        if (!_player.IsAlive)
        {
            _battleTimer.Stop();
            EndBattle(false);
        }
        else if (!_enemy.IsAlive)
        {
            _battleTimer.Stop();
            EndBattle(true);
        }
    }

    private void ExecutePlayerAction()
    {
        HashSet<string>? triggeredPassiveSkillsThisTurn = null;

        // Stun check: stunned player loses their action but still ticks
        if (_player.ActiveBuffs.IsStunned)
        {
            GD.Print($"[BattleManager] {_player.Name} is Stunned and loses their turn!");
        }
        else
        {
            // Only advance the counter on turns the player actually acts.
            // A stunned turn must not advance the skill timer or the displayed countdown becomes wrong.
            _playerSkillTurnCount++;

            // Attempt to fire the active skill (every ActivePeriod turns)
            TryFireActiveSkill();

            // Attempt to fire any equipped passive skills whose trigger conditions are met
            if (_enemy.IsAlive)
            {
                triggeredPassiveSkillsThisTurn = TryFirePassiveSkills();
            }

            // Normal auto-attack (always happens regardless of skill activations)
            if (_enemy.IsAlive)
            {
                PlayerAutoAction();
            }
        }

        // Tick passive skill cooldowns after all skill checks so cooldowns wait full turns before re-triggering
        TickPassiveCooldowns(triggeredPassiveSkillsThisTurn);

        // Tick player status effects (DoT, HoT, duration countdown)
        var (expiredPlayer, dotPlayer, hotPlayer) = _player.ActiveBuffs.Tick();
        if (dotPlayer > 0)
        {
            _player.CurrentHealth = Godot.Mathf.Max(0, _player.CurrentHealth - dotPlayer);
            GD.Print($"[BattleManager] {_player.Name} takes {dotPlayer} status damage!");
            ShowDamageNumber(_playerDamageLabel, dotPlayer);
        }
        if (hotPlayer > 0 && _player.IsAlive)
        {
            _player.Heal(hotPlayer);
            GD.Print($"[BattleManager] {_player.Name} regenerates {hotPlayer} HP!");
        }
        foreach (var eff in expiredPlayer)
            GD.Print($"[BattleManager] Status effect expired: {eff.Type} on {_player.Name}");

        // Spend AP after acting
        _playerActionPoints -= ACTION_POINT_THRESHOLD;
    }

    private void ExecuteEnemyAction()
    {
        // Stun check: stunned enemy loses their action but still ticks
        if (_enemy.ActiveStatusEffects.IsStunned)
        {
            GD.Print($"[BattleManager] {_enemy.Name} is Stunned and loses their turn!");
        }
        else
        {
            EnemyTurn(_playerDefendedLastTurn);
        }

        // Tick enemy status effects
        var (expiredEnemy, dotEnemy, hotEnemy) = _enemy.ActiveStatusEffects.Tick();
        if (dotEnemy > 0)
        {
            _enemy.CurrentHealth = Godot.Mathf.Max(0, _enemy.CurrentHealth - dotEnemy);
            GD.Print($"[BattleManager] {_enemy.Name} takes {dotEnemy} status damage!");
            ShowDamageNumber(_enemyDamageLabel, dotEnemy);
        }
        if (hotEnemy > 0 && _enemy.IsAlive)
        {
            _enemy.CurrentHealth = Godot.Mathf.Min(_enemy.MaxHealth, _enemy.CurrentHealth + hotEnemy);
            GD.Print($"[BattleManager] {_enemy.Name} regenerates {hotEnemy} HP!");
        }
        foreach (var eff in expiredEnemy)
            GD.Print($"[BattleManager] Status effect expired: {eff.Type} on {_enemy.Name}");

        // Spend AP after acting
        _enemyActionPoints -= ACTION_POINT_THRESHOLD;

        // Clear defend flag AFTER enemy turn fully completes
        // This ensures defend bonus is applied even if player gets multiple actions per tick
        _playerDefendedLastTurn = false;
    }
    
    // -------------------------------------------------------------------------
    // Skill execution
    // -------------------------------------------------------------------------

    private void TryFireActiveSkill()
    {
        var skill = _player.GetActiveSkill();
        if (skill == null) return;

        if (_playerSkillTurnCount % skill.ActivePeriod != 0) return;

        if (!_player.TryUseMana(skill.ManaCost))
        {
            GD.Print($"[Skill] {_player.Name} tried to use '{skill.DisplayName}' but has insufficient mana ({_player.CurrentMana}/{skill.ManaCost}).");
            return;
        }

        GD.Print($"[Skill] {_player.Name} activates '{skill.DisplayName}'!");
        bool applied = skill.Apply(_player, _enemy, _rng);
        if (!applied)
        {
            _player.RestoreMana(skill.ManaCost);
            GD.PushWarning($"[Skill] '{skill.DisplayName}' Apply() returned false; mana restored.");
            return;
        }

        // Reset counter only on successful activation.
        _playerSkillTurnCount = 0;
    }

    private HashSet<string> TryFirePassiveSkills()
    {
        var triggeredSkillIds = new HashSet<string>();
        foreach (var skill in _player.GetEquippedPassiveSkills())
        {
            // Check cooldown
            if (_passiveSkillCooldowns.TryGetValue(skill.SkillId, out int cooldownLeft) && cooldownLeft > 0)
                continue;

            if (!skill.ShouldTriggerPassive(_player, _enemy, _rng)) continue;

            if (!_player.TryUseMana(skill.ManaCost))
            {
                GD.Print($"[Skill] {_player.Name} cannot trigger '{skill.DisplayName}': insufficient mana.");
                continue;
            }

            GD.Print($"[Skill] {_player.Name} passive triggers '{skill.DisplayName}'!");
            bool applied = skill.Apply(_player, _enemy, _rng);
            if (!applied)
            {
                _player.RestoreMana(skill.ManaCost);
                GD.PushWarning($"[Skill] '{skill.DisplayName}' Apply() returned false; mana restored.");
                continue;
            }

            if (skill.PassiveCooldown > 0)
            {
                _passiveSkillCooldowns[skill.SkillId] = skill.PassiveCooldown;
                triggeredSkillIds.Add(skill.SkillId);
            }
        }

        return triggeredSkillIds;
    }

    private void TickPassiveCooldowns(HashSet<string>? triggeredSkillIdsThisTurn = null)
    {
        var keys = new System.Collections.Generic.List<string>(_passiveSkillCooldowns.Keys);
        foreach (var key in keys)
        {
            if (triggeredSkillIdsThisTurn != null && triggeredSkillIdsThisTurn.Contains(key))
                continue;

            _passiveSkillCooldowns[key] = System.Math.Max(0, _passiveSkillCooldowns[key] - 1);
        }
    }

    private void PlayerAutoAction()
    {
        // Player auto-AI: defends with 30% probability when health drops below 40%, otherwise attacks.
        float healthPercentage = (float)_player.CurrentHealth / _player.GetEffectiveMaxHealth();
        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        
        // More likely to defend when health is low
        if (healthPercentage < 0.4f && GD.Randf() < 0.3f)
        {
            GD.Print($"{_player.Name} takes a defensive stance!");
            _playerDefendedLastTurn = true;
            return;
        }
        
        // Aggressive attack when enemy is low on health
        if (enemyHealthPercentage < 0.3f)
        {
            GD.Print($"{_player.Name} goes for a finishing blow!");
        }
        
        // Otherwise, normal attack
        PlayerAttack();
    }
    
    private void PlayerAttack()
    {
        // Blind miss check: Blind reduces accuracy to 55% (GetAccuracyMultiplier returns 1.0 when not blind)
        if (GD.Randf() > _player.ActiveBuffs.GetAccuracyMultiplier())
        {
            GD.Print($"{_player.Name} is Blinded and misses the attack!");
            AppendCombatEvent($"{_player.Name} misses.");
            return;
        }

        // Add some variation to attacks
        bool criticalHit = GD.Randf() < 0.15f; // 15% chance for critical hit
        int baseDamage = _player.GetEffectiveAttack() + GD.RandRange(-5, 5);
        
        if (criticalHit)
        {
            baseDamage = (int)(baseDamage * 1.5f);
        }
        
        baseDamage = Mathf.Max(1, baseDamage);
        int actualDamage = _enemy.TakeDamage(baseDamage);

        if (criticalHit)
        {
            GD.Print($"Critical hit! {_player.Name} deals {actualDamage} damage!");
        }
        else
        {
            GD.Print($"{_player.Name} attacks for {actualDamage} damage!");
        }
        AppendCombatEvent(criticalHit
            ? $"{_player.Name} critically hits for {actualDamage}."
            : $"{_player.Name} attacks for {actualDamage}.");
        
        // Show damage number on enemy
        ShowDamageNumber(_enemyDamageLabel, actualDamage, criticalHit);
        
        // Play attack animation (flash the player sprite)
        PlayAttackAnimation(_playerSprite);
    }
    
    private void EnemyTurn(bool playerDefended = false)
    {
        if (!_enemy.IsAlive || !_player.IsAlive) return;

        // Blind miss check: Blind reduces accuracy to 55% (GetAccuracyMultiplier returns 1.0 when not blind)
        if (GD.Randf() > _enemy.ActiveStatusEffects.GetAccuracyMultiplier())
        {
            GD.Print($"{_enemy.Name} is Blinded and misses the attack!");
            AppendCombatEvent($"{_enemy.Name} misses.");
            return;
        }

        float enemyHealthPercentage = (float)_enemy.CurrentHealth / _enemy.MaxHealth;
        // Note: uses base MaxHealth (not GetEffectiveMaxHealth()); equipment bonuses are not reflected in this threshold.
        float playerHealthPercentage = (float)_player.CurrentHealth / _player.MaxHealth;

        // Enemy AI: More aggressive when player is low on health
        bool aggressiveAttack = playerHealthPercentage < 0.3f && GD.Randf() < 0.4f;
        bool criticalHit = GD.Randf() < 0.1f; // 10% chance for enemy critical hit

        int damage = _enemy.GetEffectiveAttack() + GD.RandRange(-3, 3);
        
        if (aggressiveAttack)
        {
            damage = (int)(damage * 1.3f);
            GD.Print($"{_enemy.Name} attacks ferociously!");
        }
        else if (criticalHit)
        {
            damage = (int)(damage * 1.4f);
            GD.Print($"Critical hit! {_enemy.Name} strikes hard!");
        }
        
        if (playerDefended)
        {
            damage = damage / 2;
            GD.Print($"The attack is weakened by {_player.Name}'s defense!");
            // NOTE: _playerDefendedLastTurn cleared at end of ExecuteEnemyAction, not here
        }

        damage = Mathf.Max(1, damage);
        int actualDamage = _player.TakeDamage(damage);
        
        if (!aggressiveAttack && !criticalHit)
        {
            GD.Print($"{_enemy.Name} attacks for {actualDamage} damage!");
        }
        AppendCombatEvent(criticalHit
            ? $"{_enemy.Name} critically hits for {actualDamage}."
            : $"{_enemy.Name} attacks for {actualDamage}.");
        
        // Show damage number on player
        ShowDamageNumber(_playerDamageLabel, actualDamage, criticalHit);

        // Play attack animation (flash the enemy sprite)
        PlayAttackAnimation(_enemySprite);

        // Attempt to apply a debuff from this enemy's profile
        TryApplyEnemyDebuff();
    }

    private void TryApplyEnemyDebuff()
    {
        var abilities = EnemyDebuffProfile.GetAbilities(_enemy.EnemyType);
        if (abilities.Count == 0) return;

        foreach (var ability in abilities)
        {
            if (GD.Randf() < ability.Chance)
            {
                _player.ActiveBuffs.Add(new ActiveStatusEffect(ability.EffectType, ability.Magnitude, ability.Duration));
                GD.Print($"[BattleManager] {_enemy.Name} inflicts {ability.EffectType} on {_player.Name} ({ability.Duration} turns)!");
            }
        }
    }

    private void EndBattle(bool playerWon)
    {
        GD.Print($"BattleManager.EndBattle called: playerWon = {playerWon}");

        if (_resultEmitted)
            return;

        StopBattleRuntime();
        // Refresh before rewards/level math so PlayerStatus/EnemyStatus drop
        // the cleared effect tags while the result panel is being composed.
        UpdateUI();
        _phase = BattlePhase.Result;

        int previousLevel = _player.Level;
        int experienceGained = 0;
        int goldGained = 0;
        var resolvedLoot = LootResult.Empty;

        // Add spacing and clear result display
        GD.Print("=== BATTLE RESULT ===");

        if (playerWon)
        {
            GD.Print($"🎉 VICTORY! {_player.Name} wins the battle!");
            GD.Print($"Experience gained: {_enemy.ExperienceReward} XP");
            GD.Print($"Gold gained: {_enemy.GoldReward} Gold");
            
            int oldLevel = _player.Level;
            _player.GainExperience(_enemy.ExperienceReward);
            _player.GainGold(_enemy.GoldReward);
            experienceGained = _enemy.ExperienceReward;
            goldGained = _enemy.GoldReward;
            
            // Check if player leveled up
            if (_player.Level > oldLevel)
            {
                GD.Print($"⭐ LEVEL UP! {_player.Name} reached level {_player.Level}!");
                GD.Print($"New stats: HP {_player.MaxHealth}, ATK {_player.Attack}, DEF {_player.Defense}");

                // Grant any skills unlocked by the new level(s)
                SkillCatalog.GrantSkillsUpToLevel(_player, _player.Level);
            }

            // Roll and award loot
            var lootTable = LootTableCatalog.GetByEnemyType(_enemy.EnemyType);
            if (lootTable == null)
            {
                GD.PushWarning($"[BattleManager] No LootTable found for enemy type '{_enemy.EnemyType}'. Skipping loot roll.");
            }

            var lootResult = lootTable == null
                ? LootResult.Empty
                : LootManager.RollLoot(lootTable, _rng);
            resolvedLoot = lootResult;
            if (lootResult.HasDrops)
            {
                LootManager.AwardLootToCharacter(lootResult, _player);
                GD.Print("--- Loot Drops ---");
                foreach (var drop in lootResult.DroppedItems)
                {
                    GD.Print($"  {drop.Quantity}x {drop.Item.DisplayName}");
                }
                GD.Print("------------------");
            }
            else
            {
                GD.Print("No loot dropped.");
            }
        }
        else
        {
            GD.Print($"💀 DEFEAT! {_player.Name} was defeated by {_enemy.Name}...");
            GD.Print("Game Over - You will return to the main menu.");
        }
        
        GD.Print("=====================");

        ResolvedResult = new BattleResultSummary(
            playerWon,
            experienceGained,
            goldGained,
            previousLevel,
            _player.Level,
            resolvedLoot);

        RenderResult(ResolvedResult);

        GD.Print("BattleManager emitting BattleFinished signal immediately");
        EmitBattleFinishedOnce(playerWon, false);
    }

    private void RenderResult(BattleResultSummary result)
    {
        _resultTitle.Text = result.PlayerWon ? "VICTORY" : "DEFEAT";
        _experienceResult.Text = $"Experience: {result.ExperienceGained}";
        _goldResult.Text = $"Gold: {result.GoldGained}";
        _levelResult.Text = result.PreviousLevel == result.NewLevel
            ? $"Level: {result.NewLevel}"
            : $"Level: {result.PreviousLevel} → {result.NewLevel}";

        if (!result.Loot.HasDrops)
        {
            _lootResultList.Text = "No loot.";
        }
        else
        {
            var lines = new System.Text.StringBuilder();
            foreach (var drop in result.Loot.DroppedItems)
            {
                var rarityTag = drop.Rarity > ItemRarity.Common ? $" [{drop.Rarity}]" : string.Empty;
                lines.AppendLine($"{drop.Quantity}x {drop.Item.DisplayName}{rarityTag}");
            }
            _lootResultList.Text = lines.ToString().TrimEnd();
        }

        _phase = BattlePhase.Result;
        SetPhasePresentation();

        if (_continueButton != null)
        {
            if (result.PlayerWon)
                _continueButton.GrabFocus();
        }
        if (_escapeButton != null)
            _escapeButton.Visible = false;
    }

    private void ShowDamageNumber(Label? damageLabel, int damage, bool isCritical = false)
    {
        // Gracefully skip if damage label is not available (optional UI element)
        if (damageLabel == null) return;
        
        // Set damage text
        damageLabel.Text = $"-{damage}";
        
        // Set color based on critical hit
        if (isCritical)
        {
            damageLabel.Modulate = new Color(1, 1, 0, 1); // Yellow for critical
        }
        else
        {
            damageLabel.Modulate = new Color(1, 0, 0, 1); // Red for normal damage
        }
        
        // Create tween for damage number animation
        var tween = CreateTrackedTween();
        tween.SetParallel(true);
        
        // Animate position (move up) — skipped under reduced motion; the
        // 1-second opacity fade is kept so damage feedback stays readable.
        var startPos = damageLabel.Position;
        if (!_reducedMotionEnabled)
        {
            var endPos = startPos + new Vector2(0, -30);
            tween.TweenProperty(damageLabel, "position", endPos, 1.0);
        }

        // Animate opacity (fade out)
        tween.TweenProperty(damageLabel, "modulate:a", 0.0f, 1.0);
        
        // Reset position and hide when animation is done
        tween.TweenCallback(Callable.From(() => {
            damageLabel.Position = startPos;
            damageLabel.Modulate = new Color(1, 0, 0, 0);
        })).SetDelay(1.0);
    }
    
    private void PlayAttackAnimation(AnimatedSprite2D? sprite)
    {
        if (sprite == null) return;

        if (_reducedMotionEnabled)
            return;

        // Create a quick flash effect for attack
        var tween = CreateTrackedTween();
        tween.SetParallel(true);
        
        // Get current scale (should be 3.0f) and scale up slightly from that
        var currentScale = sprite.Scale;
        var attackScale = currentScale * 1.2f;
        
        // Scale up slightly and back
        tween.TweenProperty(sprite, "scale", attackScale, 0.1);
        tween.TweenProperty(sprite, "scale", currentScale, 0.1).SetDelay(0.1);
        
        // Flash white
        tween.TweenProperty(sprite, "modulate", new Color(2, 2, 2, 1), 0.1);
        tween.TweenProperty(sprite, "modulate", new Color(1, 1, 1, 1), 0.1).SetDelay(0.1);
    }
}
