using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ConsumableItemTest : Godot.Node
{
    // ---- ConsumableItem basics -----------------------------------------------

    [TestCase]
    public void ConsumableItem_Category_IsConsumable()
    {
        var item = ConsumableCatalog.CreateHealthPotion();
        AssertThat(item.Category).IsEqual(ItemCategory.Consumable);
    }

    [TestCase]
    public void ConsumableItem_CanStack_IsTrue()
    {
        var item = ConsumableCatalog.CreateHealthPotion();
        AssertThat(item.CanStack).IsTrue();
        AssertThat(item.MaxStackSize).IsGreater(1);
    }

    [TestCase]
    public void ConsumableItem_HasEffectDescription()
    {
        AssertThat(ConsumableCatalog.CreateHealthPotion().EffectDescription).IsNotEmpty();
        AssertThat(ConsumableCatalog.CreateStrengthTonic().EffectDescription).IsNotEmpty();
    }

    [TestCase]
    public void ConsumableItem_Apply_NullTarget_ReturnsFalse()
    {
        bool result = ConsumableCatalog.CreateHealthPotion().Apply(null);
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void ConsumableItem_Apply_NullEffect_ReturnsFalse()
    {
        var item = new ConsumableItem { Id = "test_no_effect", DisplayName = "Broken Item" };
        // Effect intentionally not set
        var character = TestHelpers.CreateTestCharacter();
        bool result = item.Apply(character);
        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void CureEffect_Apply_NothingToCure_ReturnsTrue()
    {
        var character = TestHelpers.CreateTestCharacter(); // no debuffs
        var antidote = ConsumableCatalog.CreateAntidote();

        // Antidote on a clean character: Apply returns true (item is consumed by caller),
        // but no debuff is actually removed.
        bool result = antidote.Apply(character);
        AssertThat(result).IsTrue();
        AssertThat(character.ActiveBuffs.HasAny).IsFalse();
    }

    [TestCase]
    public void CureEffect_Apply_WhenPoisoned_RemovesPoison()
    {
        var character = TestHelpers.CreateTestCharacter();
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Poison, 5, 3));

        var antidote = ConsumableCatalog.CreateAntidote();
        bool result = antidote.Apply(character);

        AssertThat(result).IsTrue();
        AssertThat(character.ActiveBuffs.HasAny).IsFalse();
    }

    // ---- HealEffect ----------------------------------------------------------

    [TestCase]
    public void HealEffect_Apply_RestoresHealth()
    {
        var character = TestHelpers.CreateTestCharacter();
        character.CurrentHealth = 50;
        var item = ConsumableCatalog.CreateHealthPotion(); // heals 50

        item.Apply(character);

        AssertThat(character.CurrentHealth).IsEqual(100);
    }

    [TestCase]
    public void HealEffect_Apply_CannotExceedMaxHealth()
    {
        var character = TestHelpers.CreateTestCharacter();
        character.CurrentHealth = 90;
        var item = ConsumableCatalog.CreateHealthPotion(); // heals 50

        item.Apply(character);

        AssertThat(character.CurrentHealth).IsEqual(character.MaxHealth);
    }

    // ---- Buff effects --------------------------------------------------------

    [TestCase]
    public void BuffAttackEffect_Apply_IncreasesEffectiveAttack()
    {
        var character = TestHelpers.CreateTestCharacter();
        int baseAttack = character.GetEffectiveAttack();

        ConsumableCatalog.CreateStrengthTonic().Apply(character); // +15 ATK for 3 turns

        AssertThat(character.GetEffectiveAttack()).IsEqual(baseAttack + 15);
    }

    [TestCase]
    public void BuffDefenseEffect_Apply_IncreasesEffectiveDefense()
    {
        var character = TestHelpers.CreateTestCharacter();
        int baseDef = character.GetEffectiveDefense();

        ConsumableCatalog.CreateIronSkin().Apply(character); // +10 DEF for 4 turns

        AssertThat(character.GetEffectiveDefense()).IsEqual(baseDef + 10);
    }

    [TestCase]
    public void BuffSpeedEffect_Apply_IncreasesEffectiveSpeed()
    {
        var character = TestHelpers.CreateTestCharacter();
        int baseSpeed = character.GetEffectiveSpeed();

        ConsumableCatalog.CreateSwiftnessDraught().Apply(character); // +8 SPD for 3 turns

        AssertThat(character.GetEffectiveSpeed()).IsEqual(baseSpeed + 8);
    }

    // ---- Out-of-battle inventory use -----------------------------------------

    [TestCase]
    public void UseFromInventory_RemovesOneFromStack_And_HealsCharacter()
    {
        var character = TestHelpers.CreateTestCharacter();
        character.CurrentHealth = 50;
        var potion = ConsumableCatalog.CreateHealthPotion();
        character.TryAddItem(potion, 3, out _);

        // Mirrors UseConsumableOutOfBattle logic
        character.TryRemoveItem(potion.Id, 1);
        potion.Apply(character);

        AssertThat(character.GetItemQuantity("health_potion")).IsEqual(2);
        AssertThat(character.CurrentHealth).IsEqual(100);
    }

    // ---- StatusEffectSet -----------------------------------------------------

    [TestCase]
    public void StatusEffectSet_Tick_DecreasesDurationAndExpiresCorrectly()
    {
        var character = TestHelpers.CreateTestCharacter();
        int baseAttack = character.Attack; // no equipment
        ConsumableCatalog.CreateStrengthTonic().Apply(character); // 3 turns

        character.ActiveBuffs.Tick(); // 2 remaining
        AssertThat(character.GetEffectiveAttack()).IsEqual(baseAttack + 15);

        character.ActiveBuffs.Tick(); // 1 remaining
        AssertThat(character.GetEffectiveAttack()).IsEqual(baseAttack + 15);

        character.ActiveBuffs.Tick(); // 0 → expired
        AssertThat(character.GetEffectiveAttack()).IsEqual(baseAttack);
    }

    [TestCase]
    public void StatusEffectSet_Tick_ReturnsExpiredEffects()
    {
        var character = TestHelpers.CreateTestCharacter();
        ConsumableCatalog.CreateStrengthTonic().Apply(character); // 3 turns

        character.ActiveBuffs.Tick();
        character.ActiveBuffs.Tick();
        var (expired, _, _) = character.ActiveBuffs.Tick();

        AssertThat(expired.Count).IsEqual(1);
        AssertThat((int)expired[0].Type).IsEqual((int)StatusEffectType.Strength);
    }

    [TestCase]
    public void StatusEffectSet_Clear_RemovesAllEffects()
    {
        var character = TestHelpers.CreateTestCharacter();
        ConsumableCatalog.CreateStrengthTonic().Apply(character);
        ConsumableCatalog.CreateIronSkin().Apply(character);

        AssertThat(character.ActiveBuffs.HasAny).IsTrue();
        character.ActiveBuffs.Clear();
        AssertThat(character.ActiveBuffs.HasAny).IsFalse();
    }

    [TestCase]
    public void StatusEffectSet_SameType_TakesHigherValues()
    {
        var character = TestHelpers.CreateTestCharacter();
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Strength, 10, 2));
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Strength, 15, 1)); // higher magnitude

        // Single stacked entry with max magnitude (15) and max turns (2)
        AssertThat(character.ActiveBuffs.GetAttackFlatBonus()).IsEqual(15);
        AssertThat(character.ActiveBuffs.Effects.Count).IsEqual(1);
    }

    // ---- ConsumableCatalog / ItemCatalog registration -----------------------

    [TestCase]
    public void ConsumableCatalog_HealthPotion_HasCorrectId()
    {
        var item = ConsumableCatalog.CreateHealthPotion();
        AssertThat(item.Id).IsEqual("health_potion");
        AssertThat(item.EffectDescription).IsEqual("Restores 50 HP");
    }

    [TestCase]
    public void ConsumableCatalog_StrengthTonic_HasCorrectId()
    {
        var item = ConsumableCatalog.CreateStrengthTonic();
        AssertThat(item.Id).IsEqual("strength_tonic");
        AssertThat(item.EffectDescription).IsEqual("+15 ATK for 3 turns");
    }

    [TestCase]
    public void ItemCatalog_RegistersAllConsumables()
    {
        AssertThat(ItemCatalog.ItemExists("health_potion")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("greater_health_potion")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("strength_tonic")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("iron_skin")).IsTrue();
        AssertThat(ItemCatalog.ItemExists("swiftness_draught")).IsTrue();
    }

    [TestCase]
    public void ItemCatalog_CreateItemById_ReturnsConsumableItem()
    {
        var item = ItemCatalog.CreateItemById("health_potion");
        AssertThat(item).IsNotNull();
        AssertThat(item is ConsumableItem).IsTrue();
        AssertThat(item!.Category).IsEqual(ItemCategory.Consumable);
    }

    // ---- TakeDamage with status effect buffs ------------------------------------

    [TestCase]
    public void TakeDamage_WithFortifyBuff_ReducesDamageByBonusDefense()
    {
        var character = TestHelpers.CreateTestCharacter(); // Defense = 10, HP = 100
        character.ActiveBuffs.Add(new ActiveStatusEffect(StatusEffectType.Fortify, 5, 3));

        // GetEffectiveDefense() = 10 (base) + 5 (Fortify) = 15
        // actual damage = max(1, 20 - 15) = 5
        character.TakeDamage(20);

        AssertThat(character.CurrentHealth).IsEqual(95);
    }

    [TestCase]
    public void TakeDamage_WithoutBuff_UsesBaseDefense()
    {
        var character = TestHelpers.CreateTestCharacter(); // Defense = 10, HP = 100
        // actual damage = max(1, 20 - 10) = 10
        character.TakeDamage(20);
        AssertThat(character.CurrentHealth).IsEqual(90);
    }
}
