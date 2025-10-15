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
            AttackBonus = 10,
            AssetPath = "res://assets/sprites/items/weapons/wooden_sword.png"
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
            DefenseBonus = 8,
            AssetPath = "res://assets/sprites/items/armor/wooden_armor.png"
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
            DefenseBonus = 5,
            AssetPath = "res://assets/sprites/items/shields/wooden_shield.png"
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
            HealthBonus = 50,
            AssetPath = "res://assets/sprites/items/helmet/wooden_helmet.png"
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
            SpeedBonus = 2,
            AssetPath = "res://assets/sprites/items/shoes/wooden_shoes.png"
        };
    }
}
