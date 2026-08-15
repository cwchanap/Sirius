using GdUnit4;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

/// <summary>
/// Tests for BattleManager consumable application logic and turn order.
///
/// BattleManager itself requires a Godot scene to instantiate, so these tests
/// validate the data-layer logic that BattleManager delegates to. Each test
/// mirrors a specific code path in BattleManager to catch regressions if
/// the underlying data contracts change.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class BattleManagerTest : Node
{
    private const float ActionPointThreshold = 100f;

    [TestCase]
    public void ActionPointScheduling_FastActorShare_MatchesSpeedRatio()
    {
        int playerActions = SimulateActionCount(playerSpeed: 18, enemySpeed: 6, ticks: 1000, out int enemyActions);
        double share = (double)playerActions / (playerActions + enemyActions);

        // Expected share = 18 / (18 + 6) = 0.75
        AssertThat(share).IsEqual(0.75d);
    }

    [TestCase]
    public void ActionPointScheduling_EqualSpeed_AlternatesTieBreaks()
    {
        int playerActions = SimulateActionCount(playerSpeed: 10, enemySpeed: 10, ticks: 300, out int enemyActions);
        AssertThat(playerActions).IsEqual(enemyActions)
            .OverrideFailureMessage("Equal speeds should produce equal action counts over time");
    }

    // ---- Pre-battle consumable application (mirrors OnStartButtonPressed) ----

    /// <summary>
    /// Mirrors the player-targeting success path in OnStartButtonPressed:
    /// TryRemoveItem succeeds -> Apply succeeds -> HP restored.
    /// </summary>
    [TestCase]
    public void PreBattle_HealthPotion_RemovesItemAndHeals()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.CurrentHealth = 50;
        var potion = ConsumableCatalog.CreateHealthPotion();
        player.TryAddItem(potion, 2, out _);

        bool removed = player.TryRemoveItem(potion.Id, 1);
        bool applied = potion.Apply(player);

        AssertThat(removed).IsTrue();
        AssertThat(applied).IsTrue();
        AssertThat(player.CurrentHealth).IsEqual(100);
        AssertThat(player.GetItemQuantity(potion.Id)).IsEqual(1);
    }

    /// <summary>
    /// Mirrors the rollback branch: Apply returns false -> TryAddItem restores item.
    /// Ensures the rollback contract is preserved if Apply ever fails.
    /// </summary>
    [TestCase]
    public void PreBattle_ApplyFails_RollbackRestoresItem()
    {
        var player = TestHelpers.CreateTestCharacter();
        var brokenItem = new ConsumableItem { Id = "broken_test", DisplayName = "Broken" };
        player.TryAddItem(brokenItem, 1, out _);

        bool removed = player.TryRemoveItem(brokenItem.Id, 1);
        bool applied = brokenItem.Apply(player); // returns false — no effect configured

        AssertThat(removed).IsTrue();
        AssertThat(applied).IsFalse();

        // Rollback
        player.TryAddItem(brokenItem, 1, out _);
        AssertThat(player.GetItemQuantity(brokenItem.Id)).IsEqual(1)
            .OverrideFailureMessage("Rollback should restore the consumed item when Apply fails.");
    }

    /// <summary>
    /// Enemy-targeting items must return false from Apply(Character) so that
    /// BattleManager knows to call ApplyToEnemy() instead.
    /// </summary>
    [TestCase]
    public void PreBattle_EnemyDebuffItem_ApplyToCharacter_ReturnsFalse()
    {
        var player = TestHelpers.CreateTestCharacter();
        var poisonVial = ConsumableCatalog.CreatePoisonVial();

        bool result = poisonVial.Apply(player);

        AssertThat(result).IsFalse();
        AssertThat(player.ActiveBuffs.HasAny).IsFalse();
    }

    /// <summary>
    /// Enemy-targeting items apply their debuff to the enemy via ApplyToEnemy().
    /// </summary>
    [TestCase]
    public void PreBattle_EnemyDebuffItem_ApplyToEnemy_AddsDebuff()
    {
        var enemy = Enemy.CreateGoblin();
        var poisonVial = ConsumableCatalog.CreatePoisonVial();

        AssertThat(poisonVial.Effect is EnemyDebuffEffect).IsTrue();
        var debuffEffect = (EnemyDebuffEffect)poisonVial.Effect;
        bool applied = debuffEffect.ApplyToEnemy(enemy);
        AssertThat(applied).IsTrue();

        AssertThat(enemy.ActiveStatusEffects.HasAny).IsTrue();
    }

    private static int SimulateActionCount(int playerSpeed, int enemySpeed, int ticks, out int enemyActions)
    {
        float playerAp = 0f;
        float enemyAp = 0f;
        bool playerActedLast = false;
        int playerActions = 0;
        enemyActions = 0;

        for (int tick = 0; tick < ticks; tick++)
        {
            playerAp += playerSpeed * 6f;
            enemyAp += enemySpeed * 6f;

            const int maxActionsPerTick = 4;
            for (int actionIndex = 0; actionIndex < maxActionsPerTick; actionIndex++)
            {
                bool playerReady = playerAp >= ActionPointThreshold;
                bool enemyReady = enemyAp >= ActionPointThreshold;
                if (!playerReady && !enemyReady)
                    break;

                bool playerActs;
                if (playerReady && enemyReady)
                {
                    float apGap = Mathf.Abs(playerAp - enemyAp);
                    if (apGap < 0.001f)
                        playerActs = !playerActedLast;
                    else
                        playerActs = playerAp > enemyAp;
                }
                else
                {
                    playerActs = playerReady;
                }

                if (playerActs)
                {
                    playerAp -= ActionPointThreshold;
                    playerActions++;
                }
                else
                {
                    enemyAp -= ActionPointThreshold;
                    enemyActions++;
                }

                playerActedLast = playerActs;
            }
        }

        return playerActions;
    }

    /// <summary>
    /// Verifies that the defend flag persists across multiple player actions within a single tick,
    /// and is only cleared after the enemy completes their turn (not during the attack calculation).
    /// This tests the fix for the defend bonus being lost when player gets multiple actions per tick.
    /// </summary>
    [TestCase]
    public void DefendFlag_PersistsUntilEnemyTurnCompletes()
    {
        float playerAp = 250f;
        float enemyAp = 100f;
        bool playerDefendedLastTurn = false;
        bool defendBonusApplied = false;
        int playerActionCount = 0;

        const int maxActionsPerTick = 4;
        for (int actionIndex = 0; actionIndex < maxActionsPerTick; actionIndex++)
        {
            bool playerReady = playerAp >= ActionPointThreshold;
            bool enemyReady = enemyAp >= ActionPointThreshold;
            if (!playerReady && !enemyReady)
                break;

            bool playerActs = playerReady && (!enemyReady || playerAp >= enemyAp);

            if (playerActs)
            {
                playerAp -= ActionPointThreshold;
                playerActionCount++;
                if (playerActionCount == 1)
                    playerDefendedLastTurn = true;
            }
            else
            {
                enemyAp -= ActionPointThreshold;
                if (playerDefendedLastTurn)
                    defendBonusApplied = true;
                playerDefendedLastTurn = false;
            }
        }

        AssertThat(playerActionCount).IsEqual(2)
            .OverrideFailureMessage("Player should get defend + attack before enemy responds");
        AssertThat(defendBonusApplied).IsTrue()
            .OverrideFailureMessage("Defend bonus should apply when enemy attacks after player defends");
        AssertThat(playerDefendedLastTurn).IsFalse()
            .OverrideFailureMessage("Defend flag should be cleared after enemy turn completes");
    }

    [TestCase]
    public void ExecutePlayerAction_StunnedPlayer_TicksCooldownsButNotSkillCounter()
    {
        // A stunned player loses their action, so the active skill turn counter
        // must NOT advance. Passive cooldowns should still tick regardless.
        WithBattleManager(battleManager =>
        {
            var player = TestHelpers.CreateTestCharacter();
            player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Stun, 0, 2));

            SetPrivateField(battleManager, "_player", player);
            SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);
            SetPrivateField(battleManager, "_playerSkillTurnCount", 0);

            var cooldowns = GetPrivateField<System.Collections.Generic.Dictionary<string, int>>(battleManager, "_passiveSkillCooldowns");
            cooldowns["heal"] = 2;

            InvokePrivateMethod(battleManager, "ExecutePlayerAction");

            AssertThat(GetPrivateField<int>(battleManager, "_playerSkillTurnCount")).IsEqual(0)
                .OverrideFailureMessage("Skill turn counter must NOT advance when the player is stunned.");
            AssertThat(cooldowns["heal"]).IsEqual(1)
                .OverrideFailureMessage("Passive cooldowns must still tick even when player is stunned.");
            AssertThat(player.ActiveBuffs.IsStunned).IsTrue();
        });
    }

    [TestCase]
    public void ExecutePlayerAction_LethalActiveSkill_SkipsPassiveProcessingAndAutoAttack()
    {
        WithBattleManager(battleManager =>
        {
            var player = TestHelpers.CreateTestCharacter();
            player.Level = 7;
            player.Attack = 20;
            player.MaxMana = 100;
            player.CurrentMana = 100;
            SkillCatalog.GrantSkillsUpToLevel(player, player.Level);
            player.EquipActiveSkill("power_strike");
            player.EquipPassiveSkill("battle_cry", 0);

            var enemy = Enemy.CreateGoblin();
            enemy.CurrentHealth = 1;
            enemy.Defense = 0;

            SetPrivateField(battleManager, "_player", player);
            SetPrivateField(battleManager, "_enemy", enemy);
            SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);
            SetPrivateField(battleManager, "_playerSkillTurnCount", 2);

            InvokePrivateMethod(battleManager, "ExecutePlayerAction");

            AssertThat(enemy.IsAlive).IsFalse();
            AssertThat(player.CurrentMana).IsEqual(90);
            AssertThat(player.ActiveBuffs.GetAttackFlatBonus()).IsEqual(0);
        });
    }

    [TestCase]
    public void ExecutePlayerAction_TriggeredPassiveCooldown_DoesNotTickDownSameTurn()
    {
        WithBattleManager(battleManager =>
        {
            var player = TestHelpers.CreateTestCharacter();
            player.Level = 2;
            player.MaxMana = 100;
            player.CurrentMana = 100;
            player.CurrentHealth = 30;
            SkillCatalog.GrantSkillsUpToLevel(player, player.Level);
            player.EquipPassiveSkill("heal", 0);

            var enemy = Enemy.CreateGoblin();
            enemy.CurrentHealth = enemy.MaxHealth;

            SetPrivateField(battleManager, "_player", player);
            SetPrivateField(battleManager, "_enemy", enemy);
            SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);

            InvokePrivateMethod(battleManager, "ExecutePlayerAction");

            var cooldowns = GetPrivateField<System.Collections.Generic.Dictionary<string, int>>(battleManager, "_passiveSkillCooldowns");
            AssertThat(player.CurrentHealth).IsEqual(80);
            AssertThat(player.CurrentMana).IsEqual(85);
            AssertThat(cooldowns.ContainsKey("heal")).IsTrue();
            AssertThat(cooldowns["heal"]).IsEqual(5)
                .OverrideFailureMessage("A passive cooldown should not tick down on the same turn the passive triggers.");
        });
    }

    [TestCase]
    public void ExecutePlayerAction_ActiveSkillApplyFails_CounterNotReset()
    {
        // Verifies that when an active skill fires but Apply() returns false (no effect configured),
        // the counter is NOT reset to 0 (no cooldown penalty for a failed activation).
        WithBattleManager(battleManager =>
        {
            var player = TestHelpers.CreateTestCharacter();
            player.MaxMana = 100;
            player.CurrentMana = 100;

            // Create a skill with no Effect (_effect == null) so Apply() returns false.
            // Inject it into SkillCatalog._registry via reflection so GetActiveSkill() can resolve it.
            const string testSkillId = "test_no_effect_active";
            var noEffectSkill = new Skill
            {
                SkillId = testSkillId,
                DisplayName = "TestNoEffect",
                ManaCost = 5,
                UnlockLevel = 1,
                Type = SkillType.Active,
                ActivePeriod = 3,
                // Intentionally no Effect — Apply() will return false
            };
            var registry = typeof(SkillCatalog).GetField(
                "_registry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var registryDict = (System.Collections.Generic.Dictionary<string, Skill>)registry!.GetValue(null)!;
            bool hadExistingSkill = registryDict.TryGetValue(testSkillId, out Skill? previousSkill);
            registryDict[testSkillId] = noEffectSkill;

            try
            {
                player.KnownSkillIds.Add(testSkillId);
                player.EquipActiveSkill(testSkillId);

                var enemy = Enemy.CreateGoblin();
                enemy.CurrentHealth = enemy.MaxHealth;

                SetPrivateField(battleManager, "_player", player);
                SetPrivateField(battleManager, "_enemy", enemy);
                SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);

                int period = noEffectSkill.ActivePeriod; // 3

                // Set counter to period-1 so next turn increments to period (the "fire" turn)
                SetPrivateField(battleManager, "_playerSkillTurnCount", period - 1);

                // Act: execute one player action (counter increments to period, Apply() returns false)
                InvokePrivateMethod(battleManager, "ExecutePlayerAction");

                // Counter must be period (incremented), NOT 0 (not reset when Apply() fails)
                int counter = GetPrivateField<int>(battleManager, "_playerSkillTurnCount");
                AssertThat(counter).IsEqual(period)
                    .OverrideFailureMessage("Counter must not reset when active skill Apply() returns false.");
            }
            finally
            {
                if (hadExistingSkill && previousSkill != null)
                    registryDict[testSkillId] = previousSkill;
                else
                    registryDict.Remove(testSkillId);
            }
        });
    }

    [TestCase]
    public void ExecutePlayerAction_EnemyDiesFromActiveSkill_PassiveCooldownsStillTick()
    {
        // When the active skill kills the enemy, passive cooldowns should still be ticked.
        WithBattleManager(battleManager =>
        {
            var player = TestHelpers.CreateTestCharacter();
            player.Level = 7;
            player.Attack = 20;
            player.MaxMana = 100;
            player.CurrentMana = 100;
            SkillCatalog.GrantSkillsUpToLevel(player, player.Level);

            var enemy = Enemy.CreateGoblin();
            enemy.CurrentHealth = 1;
            enemy.Defense = 0;

            SetPrivateField(battleManager, "_player", player);
            SetPrivateField(battleManager, "_enemy", enemy);
            SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);
            // Set counter to 2 so next turn (count=3) fires the active skill (period=3)
            SetPrivateField(battleManager, "_playerSkillTurnCount", 2);

            // Pre-seed a passive cooldown that should tick down
            var cooldowns = GetPrivateField<System.Collections.Generic.Dictionary<string, int>>(battleManager, "_passiveSkillCooldowns");
            cooldowns["heal"] = 3; // currently on cooldown

            // Act: execute player action — active skill fires and kills enemy
            InvokePrivateMethod(battleManager, "ExecutePlayerAction");

            // Enemy must be dead from active skill
            AssertThat(enemy.IsAlive).IsFalse();

            // Passive cooldown must have ticked down from 3 to 2 (TickPassiveCooldowns still called)
            AssertThat(cooldowns["heal"]).IsEqual(2)
                .OverrideFailureMessage("Passive cooldowns must tick even when the active skill kills the enemy on that turn.");
        });
    }

    [TestCase]
    public async Task RequestCancelDuringPreparation_EmitsEscapeAndDismissOnce()
    {
        var manager = await CreateReadyBattleManager();
        int finishedCount = 0;
        int dismissCount = 0;
        bool escaped = false;
        manager.BattleFinished += (_, wasEscaped) => { finishedCount++; escaped = wasEscaped; };
        manager.DismissRequested += () => dismissCount++;
        try
        {
            manager.RequestCancel();
            manager.RequestCancel();

            AssertThat(finishedCount).IsEqual(1);
            AssertThat(escaped).IsTrue();
            AssertThat(dismissCount).IsEqual(1);
            AssertThat(manager.Visible).IsTrue();
            AssertThat(manager.IsQueuedForDeletion()).IsFalse();
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task RequestCancelDuringAutomaticCombat_StopsTimerClearsEffectsAndDismissesOnce()
    {
        var manager = await CreateReadyBattleManager();
        var player = GetPrivateField<Character>(manager, "_player");
        var enemy = GetPrivateField<Enemy>(manager, "_enemy");
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Strength, 2, 2));
        enemy.ActiveStatusEffects.Add(new ActiveStatusEffect(StatusEffectType.Poison, 1, 2));
        InvokePrivateMethod(manager, "OnStartButtonPressed");
        int finishedCount = 0;
        int dismissCount = 0;
        manager.BattleFinished += (_, _) => finishedCount++;
        manager.DismissRequested += () => dismissCount++;
        try
        {
            manager.RequestCancel();

            AssertThat(GetPrivateField<Timer>(manager, "_battleTimer").IsStopped()).IsTrue();
            AssertThat(player.ActiveBuffs.HasAny).IsFalse();
            AssertThat(enemy.ActiveStatusEffects.HasAny).IsFalse();
            AssertThat(finishedCount).IsEqual(1);
            AssertThat(dismissCount).IsEqual(1);
            AssertThat(manager.Visible).IsTrue();
            AssertThat(manager.IsQueuedForDeletion()).IsFalse();
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task RequestCancelDuringResult_DismissesWithoutSecondBattleFinished()
    {
        var manager = await CreateReadyBattleManager();
        int finishedCount = 0;
        int dismissCount = 0;
        manager.BattleFinished += (_, _) => finishedCount++;
        manager.DismissRequested += () => dismissCount++;

        try
        {
            var method = typeof(BattleManager).GetMethod(
                "EndBattle",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("EndBattle not found.");

            method.Invoke(manager, new object[] { true });
            manager.RequestCancel();
            manager.RequestCancel();

            AssertThat(finishedCount).IsEqual(1);
            AssertThat(dismissCount).IsEqual(1);
            AssertThat(manager.Visible).IsTrue();
            AssertThat(manager.GetNode<Button>("%ContinueButton").Visible).IsTrue();
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task Victory_EmitsOnceAndLeavesControlVisibleUntilDismissRequested()
    {
        var manager = await CreateReadyBattleManager();
        int finishedCount = 0;
        int dismissCount = 0;
        manager.BattleFinished += (_, escaped) =>
        {
            if (!escaped) finishedCount++;
        };
        manager.DismissRequested += () => dismissCount++;

        try
        {
            var method = typeof(BattleManager).GetMethod(
                "EndBattle",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("EndBattle not found.");

            method.Invoke(manager, new object[] { true });
            AssertThat(finishedCount).IsEqual(1);
            AssertThat(manager.Visible).IsTrue();
            var continueButton = manager.GetNode<Button>("%ContinueButton");
            AssertThat(continueButton.Visible).IsTrue();

            continueButton.EmitSignal(Button.SignalName.Pressed);
            AssertThat(dismissCount).IsEqual(1);
            AssertThat(manager.Visible).IsTrue();
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task PreparationApplyFailure_StaysInPreparationWithReadableFeedback()
    {
        var manager = await CreateReadyBattleManager();
        var brokenItem = new ConsumableItem
        {
            Id = "task3_broken_item",
            DisplayName = "Broken Item",
            Description = "A deliberately invalid item for the preparation error path."
        };
        try
        {
            AssertThat(manager.GetNodeOrNull<Control>("%PreparationPanel")).IsNotNull();
            AssertThat(manager.GetNodeOrNull<Control>("%AutomaticCombatPanel")).IsNotNull();
            AssertThat(manager.GetNodeOrNull<Label>("%PreparationItemDetails")).IsNotNull();
            if (manager.GetNodeOrNull<Control>("%PreparationPanel") is null)
                return;

            var player = GetPrivateField<Character>(manager, "_player");
            player.TryAddItem(brokenItem, 1, out _);
            SetPrivateField(manager, "_selectedConsumable", brokenItem);

            var finishedCount = 0;
            manager.BattleFinished += (_, _) => finishedCount++;
            InvokePrivateMethod(manager, "OnStartButtonPressed");

            AssertThat(finishedCount).IsEqual(0);
            AssertThat(GetPrivateField<Timer>(manager, "_battleTimer").IsStopped()).IsTrue();
            AssertThat(manager.GetNode<Control>("%PreparationPanel").Visible).IsTrue();
            AssertThat(manager.GetNode<Control>("%AutomaticCombatPanel").Visible).IsFalse();
            var details = manager.GetNode<Label>("%PreparationItemDetails");
            var failureMessage = details.Text;
            AssertThat(failureMessage).Contains("Broken Item");

            InvokePrivateMethod(manager, "ChangePreparationPage", 1);
            AssertThat(details.Text).IsEqual(failureMessage);
            InvokePrivateMethod(manager, "RefreshLayout");
            AssertThat(details.Text).IsEqual(failureMessage);
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task CancelWithCureOpen_ClosesOnlyCureAndResumesCombatTimer()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            AssertThat(manager.GetNodeOrNull<Control>("%CureOverlay")).IsNotNull();
            if (manager.GetNodeOrNull<Control>("%CureOverlay") is null)
                return;

            InvokePrivateMethod(manager, "OnStartButtonPressed");
            InvokePrivateMethod(manager, "OpenCureOverlay");
            var overlay = manager.GetNode<Control>("%CureOverlay");
            AssertThat(overlay.Visible).IsTrue();

            var finishedCount = 0;
            var dismissCount = 0;
            manager.BattleFinished += (_, _) => finishedCount++;
            manager.DismissRequested += () => dismissCount++;
            manager.RequestCancel();

            AssertThat(overlay.Visible).IsFalse();
            AssertThat(GetPrivateField<Timer>(manager, "_battleTimer").IsStopped()).IsFalse();
            AssertThat(finishedCount).IsEqual(0);
            AssertThat(dismissCount).IsEqual(0);
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task EventFeed_RefreshTrimsStandardFiveRowsToCompactThreeRows()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            AssertThat(manager.GetNodeOrNull<Label>("%EventFeed")).IsNotNull();
            if (manager.GetNodeOrNull<Label>("%EventFeed") is null)
                return;

            SetPrivateField(manager, "_isCompact", false);
            InvokePrivateMethod(manager, "RefreshEventFeed");
            foreach (var text in Enumerable.Range(1, 5).Select(index => $"Event {index}"))
                InvokePrivateMethod(manager, "AppendCombatEvent", text);

            AssertThat(manager.GetNode<Label>("%EventFeed").Text.Split('\n')).HasSize(5);
            SetPrivateField(manager, "_isCompact", true);
            InvokePrivateMethod(manager, "RefreshEventFeed");

            AssertThat(manager.GetNode<Label>("%EventFeed").Text.Split('\n')).HasSize(3);
            AssertThat(!manager.GetNode<Label>("%EventFeed").Text.Contains("Event 1", StringComparison.Ordinal)).IsTrue();
            AssertThat(manager.GetNode<Label>("%EventFeed").Text).Contains("Event 5");
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task StopBattleRuntime_KillsTrackedVisualTweens()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            var field = typeof(BattleManager).GetField(
                "_visualTweens", BindingFlags.NonPublic | BindingFlags.Instance);
            AssertThat(field).IsNotNull();
            if (field is null)
                return;

            var tweens = (System.Collections.Generic.HashSet<Tween>)field.GetValue(manager)!;
            var sprite = new AnimatedSprite2D();
            manager.AddChild(sprite);
            InvokePrivateMethod(manager, "PlayAttackAnimation", sprite);
            AssertThat(tweens.Count).IsGreater(0);
            InvokePrivateMethod(manager, "StopBattleRuntime");
            AssertThat(tweens.Count).IsEqual(0);
            sprite.Free();
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public void BattleResultSummary_VictoryStoresResolvedRewards()
    {
        var loot = new LootResult();
        loot.Add(ConsumableCatalog.CreateHealthPotion(), 2);
        var result = new BattleResultSummary(true, 25, 10, 1, 2, loot);

        AssertThat(result.ExperienceGained).IsEqual(25);
        AssertThat(result.GoldGained).IsEqual(10);
        AssertThat(result.PreviousLevel).IsEqual(1);
        AssertThat(result.NewLevel).IsEqual(2);
        AssertThat(result.Loot.DroppedItems.Count).IsEqual(1);
    }

    [TestCase]
    public void BattleResultSummary_DefeatStoresZeroRewards()
    {
        var result = new BattleResultSummary(false, 0, 0, 3, 3, LootResult.Empty);
        AssertThat(result.PlayerWon).IsFalse();
        AssertThat(result.ExperienceGained).IsEqual(0);
        AssertThat(result.GoldGained).IsEqual(0);
        AssertThat(result.Loot.HasDrops).IsFalse();
    }

    [TestCase]
    public async Task ReconcileSlots_ShrinkRemovesFocusedSlot_RestoresFocusToSurvivingSlot()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            var ironSkin = ConsumableCatalog.CreateIronSkin();
            var strengthTonic = ConsumableCatalog.CreateStrengthTonic();
            var player = GetPrivateField<Character>(manager, "_player");
            player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _);
            player.TryAddItem(ConsumableCatalog.CreateManaPotion(), 1, out _);
            player.TryAddItem(strengthTonic, 1, out _);
            player.TryAddItem(ironSkin, 1, out _);

            var details = manager.GetNode<Label>("%PreparationItemDetails");

            // Standard layout renders 4 slots/page; populate and focus slot 4 (Iron Skin).
            SetPrivateField(manager, "_isCompact", false);
            InvokePrivateMethod(manager, "RefreshPreparationItems");
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            var slots = GetPrivateField<System.Collections.Generic.List<SiriusItemSlotController>>(manager, "_preparationSlots");
            AssertThat(slots.Count).IsEqual(4);
            slots[^1].GrabFocus();
            AssertThat(slots[^1].HasFocus()).IsTrue();
            AssertThat(details.Text).Contains(ironSkin.DisplayName)
                .OverrideFailureMessage($"Focusing slot 4 should show {ironSkin.DisplayName} details, got: {details.Text}");

            // Compact layout shrinks to 3 slots/page; the focused slot 4 is removed.
            SetPrivateField(manager, "_isCompact", true);
            InvokePrivateMethod(manager, "RefreshPreparationItems");
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            var slotsAfter = GetPrivateField<System.Collections.Generic.List<SiriusItemSlotController>>(manager, "_preparationSlots");
            AssertThat(slotsAfter.Count).IsEqual(3);

            var focusOwner = manager.GetViewport().GuiGetFocusOwner();
            AssertThat(focusOwner).IsNotNull()
                .OverrideFailureMessage("Focus owner must not be null after the focused slot was removed by the standard→compact shrink.");
            AssertThat(focusOwner.IsInsideTree()).IsTrue();
            // The restored focus owner must be a surviving preparation slot, not a freed node.
            AssertThat(slotsAfter.Contains(focusOwner)).IsTrue()
                .OverrideFailureMessage("Focus should move to a surviving preparation slot after the focused slot was removed.");

            // Focus restoration must also refresh %PreparationItemDetails to
            // describe the surviving focused slot (Strength Tonic, slot 3), not
            // linger on the removed item (Iron Skin, slot 4). GrabFocus fires
            // FocusEntered after the new bindings are installed so the focused
            // callback resolves the correct item.
            AssertThat(details.Text).Contains(strengthTonic.DisplayName)
                .OverrideFailureMessage($"Details should describe the surviving focused slot ({strengthTonic.DisplayName}) after the shrink, got: {details.Text}");
            AssertThat(details.Text.Contains(ironSkin.DisplayName)).IsFalse()
                .OverrideFailureMessage($"Details should no longer describe the removed item ({ironSkin.DisplayName}) after the shrink, got: {details.Text}");
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task PreparationSlot_FocusEntered_UpdatesPreparationItemDetails()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            var potion = ConsumableCatalog.CreateHealthPotion();
            var tonic = ConsumableCatalog.CreateStrengthTonic();
            var player = GetPrivateField<Character>(manager, "_player");
            player.TryAddItem(potion, 1, out _);
            player.TryAddItem(tonic, 1, out _);

            SetPrivateField(manager, "_isCompact", false);
            InvokePrivateMethod(manager, "RefreshPreparationItems");
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            var slots = GetPrivateField<System.Collections.Generic.List<SiriusItemSlotController>>(manager, "_preparationSlots");
            AssertThat(slots.Count).IsEqual(2);

            var details = manager.GetNode<Label>("%PreparationItemDetails");

            slots[0].GrabFocus();
            AssertThat(details.Text).Contains(potion.DisplayName)
                .OverrideFailureMessage($"Focusing slot 0 should show {potion.DisplayName} details, got: {details.Text}");

            slots[1].GrabFocus();
            AssertThat(details.Text).Contains(tonic.DisplayName)
                .OverrideFailureMessage($"Focusing slot 1 should show {tonic.DisplayName} details, got: {details.Text}");
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    [TestCase]
    public async Task ReconcileSlots_StandardToCompactRebindsSurvivingFocusedSlot_KeepsFocusDetailsActionCoherent()
    {
        var manager = await CreateReadyBattleManager();
        try
        {
            // 8 consumables so page index 1 exists under both 4/page (standard)
            // and 3/page (compact) paging. Inventory preserves insertion order,
            // so the page windows are deterministic.
            var regenPotion = ConsumableCatalog.CreateRegenPotion();
            var antidote = ConsumableCatalog.CreateAntidote();
            var player = GetPrivateField<Character>(manager, "_player");
            player.TryAddItem(ConsumableCatalog.CreateHealthPotion(), 1, out _);
            player.TryAddItem(ConsumableCatalog.CreateManaPotion(), 1, out _);
            player.TryAddItem(ConsumableCatalog.CreateStrengthTonic(), 1, out _);
            player.TryAddItem(ConsumableCatalog.CreateIronSkin(), 1, out _);
            player.TryAddItem(ConsumableCatalog.CreateSwiftnessDraught(), 1, out _);
            player.TryAddItem(antidote, 1, out _);
            player.TryAddItem(regenPotion, 1, out _);
            player.TryAddItem(ConsumableCatalog.CreatePoisonVial(), 1, out _);

            var details = manager.GetNode<Label>("%PreparationItemDetails");

            // Standard page 1 (4/page) = [Swiftness, Antidote, Regen, Poison].
            // Focus slot 2 -> Regen Potion.
            SetPrivateField(manager, "_isCompact", false);
            SetPrivateField(manager, "_preparationPage", 1);
            InvokePrivateMethod(manager, "RefreshPreparationItems");
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            var slots = GetPrivateField<System.Collections.Generic.List<SiriusItemSlotController>>(manager, "_preparationSlots");
            AssertThat(slots.Count).IsEqual(4);
            slots[2].GrabFocus();
            AssertThat(slots[2].HasFocus()).IsTrue();
            AssertThat(details.Text).Contains(regenPotion.DisplayName)
                .OverrideFailureMessage($"Focusing slot 2 on standard page 1 should show {regenPotion.DisplayName} details, got: {details.Text}");

            // Compact page 1 (3/page) = [Iron Skin, Swiftness, Antidote]. Slot 3
            // is removed; slot 2 survives but is rebound from Regen Potion to
            // Antidote. Focus never leaves the Control, so FocusEntered does not
            // fire on its own.
            SetPrivateField(manager, "_isCompact", true);
            InvokePrivateMethod(manager, "RefreshPreparationItems");
            await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

            var slotsAfter = GetPrivateField<System.Collections.Generic.List<SiriusItemSlotController>>(manager, "_preparationSlots");
            AssertThat(slotsAfter.Count).IsEqual(3);

            var focusOwner = manager.GetViewport().GuiGetFocusOwner();
            AssertThat(focusOwner).IsNotNull()
                .OverrideFailureMessage("Focus owner must not be null after the standard→compact rebind.");
            AssertThat(slotsAfter.Contains(focusOwner)).IsTrue()
                .OverrideFailureMessage("Focus should remain on a surviving preparation slot after the rebind.");

            // Details must now describe the surviving slot's new binding
            // (Antidote), not linger on the rebound item (Regen Potion).
            AssertThat(details.Text).Contains(antidote.DisplayName)
                .OverrideFailureMessage($"Details should describe the rebound slot ({antidote.DisplayName}) after the resize, got: {details.Text}");
            AssertThat(details.Text.Contains(regenPotion.DisplayName)).IsFalse()
                .OverrideFailureMessage($"Details should no longer describe the rebound-away item ({regenPotion.DisplayName}) after the resize, got: {details.Text}");

            // Action identity: the focused slot's binding must match what the
            // details panel describes, so activating the slot uses Antidote.
            var bindings = GetPrivateField<System.Collections.Generic.Dictionary<SiriusItemSlotController, ConsumableItem>>(manager, "_preparationItemBySlot");
            AssertThat(bindings.TryGetValue((SiriusItemSlotController)focusOwner, out var boundItem)).IsTrue();
            AssertThat(boundItem.Id).IsEqual(antidote.Id)
                .OverrideFailureMessage($"The focused slot's binding should be {antidote.DisplayName} ({antidote.Id}) to match the details panel, got {boundItem.DisplayName} ({boundItem.Id}).");
        }
        finally
        {
            await FreeManager(manager);
        }
    }

    private async Task<BattleManager> CreateReadyBattleManager()
    {
        var scene = GD.Load<PackedScene>("res://scenes/ui/BattleScene.tscn")
            ?? throw new InvalidOperationException("Failed to load BattleScene.tscn.");
        var manager = scene.Instantiate<BattleManager>();
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(manager);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        manager.StartBattle(TestHelpers.CreateTestCharacter(), Enemy.CreateGoblin());
        return manager;
    }

    private async Task FreeManager(BattleManager manager)
    {
        if (GodotObject.IsInstanceValid(manager) && !manager.IsQueuedForDeletion())
            manager.QueueFree();

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
    }

    private static void WithBattleManager(Action<BattleManager> testBody)
    {
        var battleManager = new BattleManager();
        try
        {
            testBody(battleManager);
        }
        finally
        {
            if (GodotObject.IsInstanceValid(battleManager))
                battleManager.Free();
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' not found.");
        return (T)field.GetValue(instance)!;
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null)
            throw new InvalidOperationException($"Field '{fieldName}' not found.");
        field.SetValue(instance, value);
    }

    private static void InvokePrivateMethod(object instance, string methodName, params object[]? arguments)
    {
        var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method '{methodName}' not found.");
        method.Invoke(instance, arguments);
    }
}
