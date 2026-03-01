using GdUnit4;
using Godot;
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
    // ---- Turn order (speed-based) --------------------------------------------

    [TestCase]
    public void TestTurnOrder_PlayerFaster_GoesFirst()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.Speed = 20;
        var enemy = Enemy.CreateGoblin();
        enemy.Speed = 10;
        bool playerGoesFirst = player.GetEffectiveSpeed() >= enemy.GetEffectiveSpeed();
        AssertThat(playerGoesFirst).IsTrue()
            .OverrideFailureMessage("Player with higher speed should go first");
    }

    [TestCase]
    public void TestTurnOrder_EnemyFaster_GoesFirst()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.Speed = 10;
        var enemy = Enemy.CreateGoblin();
        enemy.Speed = 20;
        bool playerGoesFirst = player.GetEffectiveSpeed() >= enemy.GetEffectiveSpeed();
        AssertThat(playerGoesFirst).IsFalse()
            .OverrideFailureMessage("Enemy with higher speed should go first");
    }

    [TestCase]
    public void TestTurnOrder_EqualSpeeds_PlayerGoesFirst()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.Speed = 15;
        var enemy = Enemy.CreateGoblin();
        enemy.Speed = 15;
        bool playerGoesFirst = player.GetEffectiveSpeed() >= enemy.GetEffectiveSpeed();
        AssertThat(playerGoesFirst).IsTrue()
            .OverrideFailureMessage("Player should go first on speed ties (>= operator)");
    }

    [TestCase]
    public void TestTurnOrder_UsesEffectiveSpeed_WithStatusEffects()
    {
        var player = TestHelpers.CreateTestCharacter();
        player.Speed = 20;
        var enemy = Enemy.CreateGoblin();
        enemy.Speed = 15;
        AssertThat(player.GetEffectiveSpeed() >= enemy.GetEffectiveSpeed()).IsTrue();
        player.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Slow, 50, 3));
        int effectivePlayerSpeed = player.GetEffectiveSpeed();
        int effectiveEnemySpeed = enemy.GetEffectiveSpeed();
        AssertThat(effectivePlayerSpeed).IsEqual(10);
        AssertThat(effectiveEnemySpeed).IsEqual(15);
        bool playerGoesFirst = effectivePlayerSpeed >= effectiveEnemySpeed;
        AssertThat(playerGoesFirst).IsFalse()
            .OverrideFailureMessage("Slowed player should go second despite higher base speed");
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
        AssertThat(player.GetItemQuantity(brokenItem.Id)).IsEqual(1);
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
        debuffEffect.ApplyToEnemy(enemy);

        AssertThat(enemy.ActiveStatusEffects.HasAny).IsTrue();
    }
}
