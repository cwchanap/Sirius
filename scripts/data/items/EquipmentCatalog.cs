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

    public static EquipmentItem CreateIronSword()
    {
        return new EquipmentItem
        {
            Id = "iron_sword",
            DisplayName = "Iron Sword",
            Description = "A sturdy blade forged from iron.",
            SlotType = EquipmentSlotType.Weapon,
            AttackBonus = 20,
            AssetPath = "res://assets/sprites/items/weapons/iron_sword.png",
            Rarity = ItemRarity.Uncommon
        };
    }

    public static EquipmentItem CreateIronArmor()
    {
        return new EquipmentItem
        {
            Id = "iron_armor",
            DisplayName = "Iron Armor",
            Description = "Heavy armor offering solid protection.",
            SlotType = EquipmentSlotType.Armor,
            DefenseBonus = 16,
            AssetPath = "res://assets/sprites/items/armor/iron_armor.png",
            Rarity = ItemRarity.Uncommon
        };
    }

    public static EquipmentItem CreateIronShield()
    {
        return new EquipmentItem
        {
            Id = "iron_shield",
            DisplayName = "Iron Shield",
            Description = "A reliable iron shield.",
            SlotType = EquipmentSlotType.Shield,
            DefenseBonus = 10,
            AssetPath = "res://assets/sprites/items/shields/iron_shield.png",
            Rarity = ItemRarity.Uncommon
        };
    }

    public static EquipmentItem CreateIronHelmet()
    {
        return new EquipmentItem
        {
            Id = "iron_helmet",
            DisplayName = "Iron Helmet",
            Description = "A solid iron helmet that protects the head.",
            SlotType = EquipmentSlotType.Helmet,
            HealthBonus = 100,
            AssetPath = "res://assets/sprites/items/helmet/iron_helmet.png",
            Rarity = ItemRarity.Uncommon
        };
    }

    public static EquipmentItem CreateIronBoots()
    {
        return new EquipmentItem
        {
            Id = "iron_boots",
            DisplayName = "Iron Boots",
            Description = "Heavy boots that keep you grounded.",
            SlotType = EquipmentSlotType.Shoe,
            SpeedBonus = 4,
            DefenseBonus = 3,
            AssetPath = "res://assets/sprites/items/shoes/iron_boots.png",
            Rarity = ItemRarity.Uncommon
        };
    }
}
