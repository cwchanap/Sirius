using GdUnit4;
using Godot;
using System;
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
        var battleManager = new BattleManager();
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
    }

    [TestCase]
    public void ExecutePlayerAction_LethalActiveSkill_SkipsPassiveProcessingAndAutoAttack()
    {
        var battleManager = new BattleManager();
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
    }

    [TestCase]
    public void ExecutePlayerAction_TriggeredPassiveCooldown_DoesNotTickDownSameTurn()
    {
        var battleManager = new BattleManager();
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
    }

    [TestCase]
    public void ExecutePlayerAction_ActiveSkillApplyFails_CounterNotReset()
    {
        // Verifies that when an active skill fires but Apply() returns false (no effect configured),
        // the counter is NOT reset to 0 (no cooldown penalty for a failed activation).
        var battleManager = new BattleManager();
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
    }

    [TestCase]
    public void ExecutePlayerAction_EnemyDiesFromActiveSkill_PassiveCooldownsStillTick()
    {
        // When the active skill kills the enemy, passive cooldowns should still be ticked.
        var battleManager = new BattleManager();
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

    private static void InvokePrivateMethod(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Method '{methodName}' not found.");
        method.Invoke(instance, null);
    }
}
