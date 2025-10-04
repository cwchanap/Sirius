using System.Collections.Generic;

public static class EquipmentCatalog
{
    public static EquipmentItem CreateWoodenSword()
    {
        return new EquipmentItem
        {
            Id = "wooden_sword",
            DisplayName = "Wooden Sword",
            Description = "A basic training sword.",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 10
        };
    }

    public static EquipmentItem CreateWoodenArmor()
    {
        return new EquipmentItem
        {
            Id = "wooden_armor",
            DisplayName = "Wooden Armor",
            Description = "Light armor carved from sturdy wood.",
            SlotType = EquipmentSlotType.Armor,
            DefenseBonus = 8
        };
    }

    public static EquipmentItem CreateWoodenShield()
    {
        return new EquipmentItem
        {
            Id = "wooden_shield",
            DisplayName = "Wooden Shield",
            Description = "A simple wooden shield.",
            SlotType = EquipmentSlotType.Shield,
            DefenseBonus = 5
        };
    }

    public static EquipmentItem CreateWoodenHelmet()
    {
        return new EquipmentItem
        {
            Id = "wooden_helmet",
            DisplayName = "Wooden Helmet",
            Description = "Protective wooden helmet.",
            SlotType = EquipmentSlotType.Helmet,
            HealthBonus = 50
        };
    }

    public static EquipmentItem CreateWoodenShoes()
    {
        return new EquipmentItem
        {
            Id = "wooden_shoes",
            DisplayName = "Wooden Shoes",
            Description = "Wooden footwear that somehow aids movement.",
            SlotType = EquipmentSlotType.Shoe,
            SpeedBonus = 2
        };
    }
}
