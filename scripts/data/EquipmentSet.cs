using Godot;
using System;
using System.Collections.Generic;

[System.Serializable]
public partial class EquipmentSet : Resource
{
    public const int AccessorySlotCount = 4;

    private EquipmentItem _weapon;
    private EquipmentItem _shield;
    private EquipmentItem _armor;
    private EquipmentItem _helmet;
    private EquipmentItem _shoe;
    private readonly EquipmentItem[] _accessories = new EquipmentItem[AccessorySlotCount];

    public EquipmentItem GetEquipped(EquipmentSlotType slot, int accessoryIndex = 0)
    {
        return slot switch
        {
            EquipmentSlotType.Weapon => _weapon,
            EquipmentSlotType.Shield => _shield,
            EquipmentSlotType.Armor => _armor,
            EquipmentSlotType.Helmet => _helmet,
            EquipmentSlotType.Shoe => _shoe,
            EquipmentSlotType.Accessory => GetAccessory(accessoryIndex),
            _ => null
        };
    }

    public bool TryEquip(EquipmentItem item, out EquipmentItem replacedItem, int accessoryIndex = 0)
    {
        replacedItem = null;

        if (item == null)
        {
            return false;
        }

        if (item.SlotType == EquipmentSlotType.Accessory)
        {
            ValidateAccessoryIndex(accessoryIndex);
            replacedItem = _accessories[accessoryIndex];
            _accessories[accessoryIndex] = item;
            return true;
        }

        if (!SlotMatchesItem(item.SlotType))
        {
            GD.PushWarning($"Unsupported equipment slot type: {item.SlotType}");
            return false;
        }

        switch (item.SlotType)
        {
            case EquipmentSlotType.Weapon:
                replacedItem = _weapon;
                _weapon = item;
                return true;
            case EquipmentSlotType.Shield:
                replacedItem = _shield;
                _shield = item;
                return true;
            case EquipmentSlotType.Armor:
                replacedItem = _armor;
                _armor = item;
                return true;
            case EquipmentSlotType.Helmet:
                replacedItem = _helmet;
                _helmet = item;
                return true;
            case EquipmentSlotType.Shoe:
                replacedItem = _shoe;
                _shoe = item;
                return true;
            default:
                return false;
        }
    }

    public EquipmentItem Unequip(EquipmentSlotType slot, int accessoryIndex = 0)
    {
        if (slot == EquipmentSlotType.Accessory)
        {
            ValidateAccessoryIndex(accessoryIndex);
            var removed = _accessories[accessoryIndex];
            _accessories[accessoryIndex] = null;
            return removed;
        }

        switch (slot)
        {
            case EquipmentSlotType.Weapon:
                var weapon = _weapon;
                _weapon = null;
                return weapon;
            case EquipmentSlotType.Shield:
                var shield = _shield;
                _shield = null;
                return shield;
            case EquipmentSlotType.Armor:
                var armor = _armor;
                _armor = null;
                return armor;
            case EquipmentSlotType.Helmet:
                var helmet = _helmet;
                _helmet = null;
                return helmet;
            case EquipmentSlotType.Shoe:
                var shoe = _shoe;
                _shoe = null;
                return shoe;
            default:
                return null;
        }
    }

    public IEnumerable<EquipmentItem> GetEquippedItems()
    {
        if (_weapon != null) yield return _weapon;
        if (_shield != null) yield return _shield;
        if (_armor != null) yield return _armor;
        if (_helmet != null) yield return _helmet;
        if (_shoe != null) yield return _shoe;

        for (int i = 0; i < AccessorySlotCount; i++)
        {
            var accessory = _accessories[i];
            if (accessory != null)
            {
                yield return accessory;
            }
        }
    }

    public int GetAttackBonus()
    {
        return SumBonus(static item => item.AttackBonus);
    }

    public int GetDefenseBonus()
    {
        return SumBonus(static item => item.DefenseBonus);
    }

    public int GetSpeedBonus()
    {
        return SumBonus(static item => item.SpeedBonus);
    }

    public int GetHealthBonus()
    {
        return SumBonus(static item => item.HealthBonus);
    }

    private EquipmentItem GetAccessory(int index)
    {
        ValidateAccessoryIndex(index);
        return _accessories[index];
    }

    private int SumBonus(Func<EquipmentItem, int> selector)
    {
        int sum = 0;

        foreach (var item in GetEquippedItems())
        {
            sum += selector(item);
        }

        return sum;
    }

    private static bool SlotMatchesItem(EquipmentSlotType slot)
    {
        return slot == EquipmentSlotType.Weapon
            || slot == EquipmentSlotType.Shield
            || slot == EquipmentSlotType.Armor
            || slot == EquipmentSlotType.Helmet
            || slot == EquipmentSlotType.Shoe
            || slot == EquipmentSlotType.Accessory;
    }

    private static void ValidateAccessoryIndex(int index)
    {
        if (index < 0 || index >= AccessorySlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Accessory index must be between 0 and {AccessorySlotCount - 1}.");
        }
    }
}
