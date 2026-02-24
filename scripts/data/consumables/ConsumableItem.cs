using Godot;

/// <summary>
/// An item that can be used to apply an effect (heal, buff, etc.) to the player.
/// Effects are assigned by ConsumableCatalog factory methods.
/// ConsumableEffect is not a Godot Resource, so it is not [Export]-ed or persisted.
/// </summary>
[System.Serializable]
public partial class ConsumableItem : Item
{
    private ConsumableEffect _effect;

    [Export]
    public int MaxStackOverride { get; set; } = 99;

    /// <summary>The effect applied when this item is used. Set by catalog factories.</summary>
    public ConsumableEffect Effect
    {
        get => _effect;
        internal set => _effect = value;
    }

    public ConsumableItem()
    {
        SetCategory(ItemCategory.Consumable);
    }

    public override bool CanStack => true;
    public override int MaxStackSize => Mathf.Max(1, MaxStackOverride);

    /// <summary>Human-readable effect summary for tooltips.</summary>
    public string EffectDescription => _effect?.Description ?? "No effect";

    /// <summary>
    /// Applies this item's effect to the target character.
    /// Returns true if the effect was applied successfully.
    /// Does NOT remove the item from inventory — callers are responsible for that.
    /// </summary>
    public bool Apply(Character target)
    {
        if (target == null)
        {
            GD.PushWarning($"[ConsumableItem] Apply called with null target for '{DisplayName}'");
            return false;
        }

        if (_effect == null)
        {
            GD.PushWarning($"[ConsumableItem] '{DisplayName}' has no effect configured");
            return false;
        }

        if (_effect is EnemyDebuffEffect)
        {
            GD.PushWarning($"[ConsumableItem] '{DisplayName}' targets enemies; use EnemyDebuffEffect.ApplyToEnemy() instead");
            return false;
        }

        _effect.Apply(target);
        return true;
    }
}
