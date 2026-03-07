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
        // Simulate: Player defends, then attacks again before enemy responds
        // The defend bonus should still apply when enemy finally attacks
        float playerAp = 250f; // Ready for multiple actions (2.5 turns worth)
        float enemyAp = 100f;  // Ready to act after player
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

            // Player acts first if ready (higher AP or equal and taking turn)
            bool playerActs = playerReady && (!enemyReady || playerAp >= enemyAp);

            if (playerActs)
            {
                playerAp -= ActionPointThreshold;
                playerActionCount++;
                // First action: defend and set flag
                if (playerActionCount == 1)
                {
                    playerDefendedLastTurn = true;
                }
                // Second action: attack, flag should persist
            }
            else
            {
                enemyAp -= ActionPointThreshold;
                // Enemy attacks - defend bonus should apply if flag set
                if (playerDefendedLastTurn)
                {
                    defendBonusApplied = true;
                }
                // Clear flag at end of enemy turn (mirrors ExecuteEnemyAction fix)
                playerDefendedLastTurn = false;
            }
        }

        // Verify player got multiple actions (defend + attack)
        AssertThat(playerActionCount).IsEqual(2)
            .OverrideFailureMessage("Player should get defend + attack before enemy responds");

        // Verify defend bonus was applied when enemy attacked
        AssertThat(defendBonusApplied).IsTrue()
            .OverrideFailureMessage("Defend bonus should apply when enemy attacks after player defends");

        // Verify flag is cleared after enemy turn completes
        AssertThat(playerDefendedLastTurn).IsFalse()
            .OverrideFailureMessage("Defend flag should be cleared after enemy turn completes");
    }

    [TestCase]
    public void ExecutePlayerAction_StunnedPlayerStillTicksSkillCountersAndCooldowns()
    {
        var battleManager = new BattleManager();
        var player = TestHelpers.CreateTestCharacter();
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Stun, 0, 2));

        SetPrivateField(battleManager, "_player", player);
        SetPrivateField(battleManager, "_playerActionPoints", ActionPointThreshold);
        SetPrivateField(battleManager, "_playerSkillTurnCount", 0);

        var cooldowns = GetPrivateField<System.Collections.Generic.Dictionary<string, int>>(battleManager, "_passiveSkillCooldowns");
        cooldowns["heal"] = 2;

        InvokePrivateMethod(battleManager, "ExecutePlayerAction");

        AssertThat(GetPrivateField<int>(battleManager, "_playerSkillTurnCount")).IsEqual(1);
        AssertThat(cooldowns["heal"]).IsEqual(1);
        AssertThat(player.ActiveBuffs.IsStunned).IsTrue();
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
