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

    public static EquipmentItem CreateSteelLongsword() => new EquipmentItem
    {
        Id = "steel_longsword",
        DisplayName = "Steel Longsword",
        Description = "A balanced dungeon-forged blade with a keen edge.",
        Value = 180,
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 32,
        AssetPath = "res://assets/sprites/items/weapons/steel_longsword.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateChainMail() => new EquipmentItem
    {
        Id = "chain_mail",
        DisplayName = "Chain Mail",
        Description = "Interlocked steel rings that turn aside dungeon blades.",
        Value = 190,
        SlotType = EquipmentSlotType.Armor,
        DefenseBonus = 26,
        HealthBonus = 50,
        AssetPath = "res://assets/sprites/items/armor/chain_mail.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateSteelTowerShield() => new EquipmentItem
    {
        Id = "steel_tower_shield",
        DisplayName = "Steel Tower Shield",
        Description = "A tall steel shield built for holding narrow corridors.",
        Value = 170,
        SlotType = EquipmentSlotType.Shield,
        DefenseBonus = 18,
        HealthBonus = 30,
        AssetPath = "res://assets/sprites/items/shields/steel_tower_shield.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateKnightHelm() => new EquipmentItem
    {
        Id = "knight_helm",
        DisplayName = "Knight Helm",
        Description = "A sturdy helm worn by veteran dungeon guards.",
        Value = 160,
        SlotType = EquipmentSlotType.Helmet,
        DefenseBonus = 8,
        HealthBonus = 160,
        AssetPath = "res://assets/sprites/items/helmet/knight_helm.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateSwiftBoots() => new EquipmentItem
    {
        Id = "swift_boots",
        DisplayName = "Swift Boots",
        Description = "Light boots with steel caps and quiet soles.",
        Value = 165,
        SlotType = EquipmentSlotType.Shoe,
        DefenseBonus = 4,
        SpeedBonus = 8,
        AssetPath = "res://assets/sprites/items/shoes/swift_boots.png",
        Rarity = ItemRarity.Uncommon
    };

    public static EquipmentItem CreateObsidianBlade() => new EquipmentItem
    {
        Id = "obsidian_blade",
        DisplayName = "Obsidian Blade",
        Description = "A black glass blade that cuts with abyssal sharpness.",
        Value = 420,
        SlotType = EquipmentSlotType.Weapon,
        AttackBonus = 44,
        SpeedBonus = 4,
        AssetPath = "res://assets/sprites/items/weapons/obsidian_blade.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianCarapace() => new EquipmentItem
    {
        Id = "obsidian_carapace",
        DisplayName = "Obsidian Carapace",
        Description = "Dark armor layered like a dungeon construct's shell.",
        Value = 480,
        SlotType = EquipmentSlotType.Armor,
        DefenseBonus = 36,
        HealthBonus = 120,
        AssetPath = "res://assets/sprites/items/armor/obsidian_carapace.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianGuard() => new EquipmentItem
    {
        Id = "obsidian_guard",
        DisplayName = "Obsidian Guard",
        Description = "A polished black shield etched with warding lines.",
        Value = 430,
        SlotType = EquipmentSlotType.Shield,
        AttackBonus = 8,
        DefenseBonus = 26,
        AssetPath = "res://assets/sprites/items/shields/obsidian_guard.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianCrown() => new EquipmentItem
    {
        Id = "obsidian_crown",
        DisplayName = "Obsidian Crown",
        Description = "A crown-like helm humming with dungeon pressure.",
        Value = 460,
        SlotType = EquipmentSlotType.Helmet,
        HealthBonus = 240,
        SpeedBonus = 8,
        AssetPath = "res://assets/sprites/items/helmet/obsidian_crown.png",
        Rarity = ItemRarity.Rare
    };

    public static EquipmentItem CreateObsidianTreads() => new EquipmentItem
    {
        Id = "obsidian_treads",
        DisplayName = "Obsidian Treads",
        Description = "Black plated boots that move silently over stone.",
        Value = 410,
        SlotType = EquipmentSlotType.Shoe,
        DefenseBonus = 8,
        SpeedBonus = 12,
        AssetPath = "res://assets/sprites/items/shoes/obsidian_treads.png",
        Rarity = ItemRarity.Rare
    };
}
