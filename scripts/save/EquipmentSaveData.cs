using Godot;
using System.Collections.Generic;

/// <summary>
/// DTO for equipped items.
/// </summary>
public class EquipmentSaveData
{
    public string WeaponId { get; set; }
    public string ShieldId { get; set; }
    public string ArmorId { get; set; }
    public string HelmetId { get; set; }
    public string ShoeId { get; set; }
    public List<string> AccessoryIds { get; set; } = new();

    public static EquipmentSaveData FromEquipmentSet(EquipmentSet eq)
    {
        if (eq == null) return new EquipmentSaveData();

        var data = new EquipmentSaveData
        {
            WeaponId = eq.GetEquipped(EquipmentSlotType.Weapon)?.Id,
            ShieldId = eq.GetEquipped(EquipmentSlotType.Shield)?.Id,
            ArmorId = eq.GetEquipped(EquipmentSlotType.Armor)?.Id,
            HelmetId = eq.GetEquipped(EquipmentSlotType.Helmet)?.Id,
            ShoeId = eq.GetEquipped(EquipmentSlotType.Shoe)?.Id
        };

        for (int i = 0; i < EquipmentSet.AccessorySlotCount; i++)
        {
            data.AccessoryIds.Add(eq.GetEquipped(EquipmentSlotType.Accessory, i)?.Id);
        }

        return data;
    }

    public EquipmentSet ToEquipmentSet()
    {
        var equipmentSet = new EquipmentSet();

        TryEquipById(equipmentSet, WeaponId);
        TryEquipById(equipmentSet, ShieldId);
        TryEquipById(equipmentSet, ArmorId);
        TryEquipById(equipmentSet, HelmetId);
        TryEquipById(equipmentSet, ShoeId);

        for (int i = 0; i < AccessoryIds.Count && i < EquipmentSet.AccessorySlotCount; i++)
        {
            if (!string.IsNullOrEmpty(AccessoryIds[i]))
            {
                TryEquipById(equipmentSet, AccessoryIds[i], i);
            }
        }

        return equipmentSet;
    }

    private void TryEquipById(EquipmentSet eq, string itemId, int accessorySlot = 0)
    {
        if (string.IsNullOrEmpty(itemId)) return;

        var item = ItemCatalog.CreateItemById(itemId) as EquipmentItem;
        if (item != null)
        {
            eq.TryEquip(item, out _, accessorySlot);
        }
        else
        {
            GD.PushWarning($"Save load: Unknown equipment ID '{itemId}', skipping");
        }
    }
}
