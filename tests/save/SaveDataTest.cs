using GdUnit4;
using Godot;
using System.Text.Json;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveDataTest : Node
{
    [TestCase]
    public void TestVector2IDto_ToVector2I()
    {
        // Arrange
        var dto = new Vector2IDto { X = 10, Y = 20 };

        // Act
        var vector = dto.ToVector2I();

        // Assert
        AssertThat(vector.X).IsEqual(10);
        AssertThat(vector.Y).IsEqual(20);
    }

    [TestCase]
    public void TestVector2IDto_FromVector2I()
    {
        // Arrange
        var vector = new Vector2I(15, 25);

        // Act
        var dto = new Vector2IDto(vector);

        // Assert
        AssertThat(dto.X).IsEqual(15);
        AssertThat(dto.Y).IsEqual(25);
    }

    [TestCase]
    public void TestVector2IDto_SerializesCorrectly()
    {
        // Arrange
        var dto = new Vector2IDto { X = 5, Y = 10 };

        // Act
        string json = JsonSerializer.Serialize(dto);
        var deserialized = JsonSerializer.Deserialize<Vector2IDto>(json);

        // Assert
        AssertThat(deserialized).IsNotNull();
        var deserializedValue = deserialized!;
        AssertThat(deserializedValue.X).IsEqual(5);
        AssertThat(deserializedValue.Y).IsEqual(10);
    }

    [TestCase]
    public void TestCharacterSaveData_FromCharacter()
    {
        // Arrange
        var character = new Character
        {
            Name = "TestHero",
            Level = 5,
            MaxHealth = 150,
            CurrentHealth = 120,
            Attack = 30,
            Defense = 20,
            Speed = 18,
            Experience = 500,
            ExperienceToNext = 600,
            Gold = 250
        };

        // Act
        var saveData = CharacterSaveData.FromCharacter(character);

        // Assert
        AssertThat(saveData.Name).IsEqual("TestHero");
        AssertThat(saveData.Level).IsEqual(5);
        AssertThat(saveData.MaxHealth).IsEqual(150);
        AssertThat(saveData.CurrentHealth).IsEqual(120);
        AssertThat(saveData.Attack).IsEqual(30);
        AssertThat(saveData.Defense).IsEqual(20);
        AssertThat(saveData.Speed).IsEqual(18);
        AssertThat(saveData.Experience).IsEqual(500);
        AssertThat(saveData.ExperienceToNext).IsEqual(600);
        AssertThat(saveData.Gold).IsEqual(250);
    }

    [TestCase]
    public void TestCharacterSaveData_ToCharacter()
    {
        // Arrange
        var saveData = new CharacterSaveData
        {
            Name = "LoadedHero",
            Level = 10,
            MaxHealth = 200,
            CurrentHealth = 180,
            Attack = 50,
            Defense = 35,
            Speed = 25,
            Experience = 1000,
            ExperienceToNext = 1200,
            Gold = 500,
            Inventory = new InventorySaveData(),
            Equipment = new EquipmentSaveData()
        };

        // Act
        var character = saveData.ToCharacter();

        // Assert
        AssertThat(character.Name).IsEqual("LoadedHero");
        AssertThat(character.Level).IsEqual(10);
        AssertThat(character.MaxHealth).IsEqual(200);
        AssertThat(character.CurrentHealth).IsEqual(180);
        AssertThat(character.Attack).IsEqual(50);
        AssertThat(character.Defense).IsEqual(35);
        AssertThat(character.Speed).IsEqual(25);
        AssertThat(character.Experience).IsEqual(1000);
        AssertThat(character.ExperienceToNext).IsEqual(1200);
        AssertThat(character.Gold).IsEqual(500);
    }

    [TestCase]
    public void TestCharacterSaveData_RoundTrip()
    {
        // Arrange
        var original = new Character
        {
            Name = "RoundTripHero",
            Level = 7,
            MaxHealth = 175,
            CurrentHealth = 150,
            Attack = 40,
            Defense = 25,
            Speed = 20,
            Experience = 700,
            ExperienceToNext = 800,
            Gold = 350
        };

        // Act
        var saveData = CharacterSaveData.FromCharacter(original);
        var restored = saveData.ToCharacter();

        // Assert
        AssertThat(restored.Name).IsEqual(original.Name);
        AssertThat(restored.Level).IsEqual(original.Level);
        AssertThat(restored.MaxHealth).IsEqual(original.MaxHealth);
        AssertThat(restored.CurrentHealth).IsEqual(original.CurrentHealth);
        AssertThat(restored.Attack).IsEqual(original.Attack);
        AssertThat(restored.Defense).IsEqual(original.Defense);
        AssertThat(restored.Speed).IsEqual(original.Speed);
        AssertThat(restored.Experience).IsEqual(original.Experience);
        AssertThat(restored.ExperienceToNext).IsEqual(original.ExperienceToNext);
        AssertThat(restored.Gold).IsEqual(original.Gold);
    }

    [TestCase]
    public void TestCharacterSaveData_FromNullReturnsNull()
    {
        // Act
        var saveData = CharacterSaveData.FromCharacter(null);

        // Assert
        AssertThat(saveData).IsNull();
    }

    [TestCase]
    public void TestInventorySaveData_FromEmptyInventory()
    {
        // Arrange
        var inventory = new Inventory();

        // Act
        var saveData = InventorySaveData.FromInventory(inventory);

        // Assert
        AssertThat(saveData.Entries.Count).IsEqual(0);
        AssertThat(saveData.MaxItemTypes).IsEqual(100);
    }

    [TestCase]
    public void TestInventorySaveData_FromInventoryWithItems()
    {
        // Arrange
        var inventory = new Inventory();
        var item = new GeneralItem { Id = "test_potion", DisplayName = "Test Potion", MaxStackOverride = 99 };
        inventory.TryAddItem(item, 5, out _);

        // Act
        var saveData = InventorySaveData.FromInventory(inventory);

        // Assert
        AssertThat(saveData.Entries.Count).IsEqual(1);
        AssertThat(saveData.Entries[0].ItemId).IsEqual("test_potion");
        AssertThat(saveData.Entries[0].Quantity).IsEqual(5);
    }

    [TestCase]
    public void TestInventorySaveData_ToInventory_WithKnownItems()
    {
        // Arrange
        var saveData = new InventorySaveData
        {
            MaxItemTypes = 50,
            Entries = new System.Collections.Generic.List<InventoryEntryDto>
            {
                new InventoryEntryDto { ItemId = "wooden_sword", Quantity = 1 }
            }
        };

        // Act
        var inventory = saveData.ToInventory();

        // Assert
        AssertThat(inventory.MaxItemTypes).IsEqual(50);
        AssertThat(inventory.ContainsItem("wooden_sword")).IsTrue();
        AssertThat(inventory.GetQuantity("wooden_sword")).IsEqual(1);
    }

    [TestCase]
    public void TestInventorySaveData_ToInventory_SkipsUnknownItems()
    {
        // Arrange
        var saveData = new InventorySaveData
        {
            Entries = new System.Collections.Generic.List<InventoryEntryDto>
            {
                new InventoryEntryDto { ItemId = "unknown_item_xyz", Quantity = 10 }
            }
        };

        // Act
        var inventory = saveData.ToInventory();

        // Assert
        AssertThat(inventory.ItemTypeCount).IsEqual(0);
    }

    [TestCase]
    public void TestInventorySaveData_FromNullReturnsEmpty()
    {
        // Act
        var saveData = InventorySaveData.FromInventory(null);

        // Assert
        AssertThat(saveData).IsNotNull();
        AssertThat(saveData.Entries.Count).IsEqual(0);
    }

    [TestCase]
    public void TestEquipmentSaveData_FromEmptyEquipmentSet()
    {
        // Arrange
        var equipment = new EquipmentSet();

        // Act
        var saveData = EquipmentSaveData.FromEquipmentSet(equipment);

        // Assert
        AssertThat(saveData.WeaponId).IsNull();
        AssertThat(saveData.ShieldId).IsNull();
        AssertThat(saveData.ArmorId).IsNull();
        AssertThat(saveData.HelmetId).IsNull();
        AssertThat(saveData.ShoeId).IsNull();
        AssertThat(saveData.AccessoryIds.Count).IsEqual(4);
    }

    [TestCase]
    public void TestEquipmentSaveData_FromEquipmentSetWithWeapon()
    {
        // Arrange
        var equipment = new EquipmentSet();
        var weapon = EquipmentCatalog.CreateWoodenSword();
        equipment.TryEquip(weapon, out _);

        // Act
        var saveData = EquipmentSaveData.FromEquipmentSet(equipment);

        // Assert
        AssertThat(saveData.WeaponId).IsEqual("wooden_sword");
    }

    [TestCase]
    public void TestEquipmentSaveData_ToEquipmentSet_WithWeapon()
    {
        // Arrange
        var saveData = new EquipmentSaveData
        {
            WeaponId = "wooden_sword"
        };

        // Act
        var equipment = saveData.ToEquipmentSet();

        // Assert
        var weapon = equipment.GetEquipped(EquipmentSlotType.Weapon);
        AssertThat(weapon).IsNotNull();
        AssertThat(weapon.Id).IsEqual("wooden_sword");
    }

    [TestCase]
    public void TestEquipmentSaveData_ToEquipmentSet_WithAllSlots()
    {
        // Arrange
        var saveData = new EquipmentSaveData
        {
            WeaponId = "wooden_sword",
            ShieldId = "wooden_shield",
            ArmorId = "wooden_armor",
            HelmetId = "wooden_helmet",
            ShoeId = "wooden_shoes"
        };

        // Act
        var equipment = saveData.ToEquipmentSet();

        // Assert
        AssertThat(equipment.GetEquipped(EquipmentSlotType.Weapon)).IsNotNull();
        AssertThat(equipment.GetEquipped(EquipmentSlotType.Shield)).IsNotNull();
        AssertThat(equipment.GetEquipped(EquipmentSlotType.Armor)).IsNotNull();
        AssertThat(equipment.GetEquipped(EquipmentSlotType.Helmet)).IsNotNull();
        AssertThat(equipment.GetEquipped(EquipmentSlotType.Shoe)).IsNotNull();
    }

    [TestCase]
    public void TestEquipmentSaveData_RoundTrip()
    {
        // Arrange
        var original = new EquipmentSet();
        original.TryEquip(EquipmentCatalog.CreateWoodenSword(), out _);
        original.TryEquip(EquipmentCatalog.CreateWoodenArmor(), out _);

        // Act
        var saveData = EquipmentSaveData.FromEquipmentSet(original);
        var restored = saveData.ToEquipmentSet();

        // Assert
        AssertThat(restored.GetEquipped(EquipmentSlotType.Weapon)).IsNotNull();
        AssertThat(restored.GetEquipped(EquipmentSlotType.Weapon).Id).IsEqual("wooden_sword");
        AssertThat(restored.GetEquipped(EquipmentSlotType.Armor)).IsNotNull();
        AssertThat(restored.GetEquipped(EquipmentSlotType.Armor).Id).IsEqual("wooden_armor");
    }

    [TestCase]
    public void TestEquipmentSaveData_FromNullReturnsEmpty()
    {
        // Act
        var saveData = EquipmentSaveData.FromEquipmentSet(null);

        // Assert
        AssertThat(saveData).IsNotNull();
        AssertThat(saveData.WeaponId).IsNull();
    }

    [TestCase]
    public void TestSaveData_FullSerialization()
    {
        // Arrange
        var saveData = new SaveData
        {
            Version = 1,
            CurrentFloorIndex = 2,
            PlayerPosition = new Vector2IDto { X = 50, Y = 75 },
            SaveTimestamp = new System.DateTime(2024, 6, 15, 14, 30, 0),
            Character = new CharacterSaveData
            {
                Name = "TestHero",
                Level = 5,
                MaxHealth = 150,
                CurrentHealth = 120,
                Attack = 30,
                Defense = 20,
                Speed = 18,
                Experience = 500,
                ExperienceToNext = 600,
                Gold = 250,
                Inventory = new InventorySaveData(),
                Equipment = new EquipmentSaveData { WeaponId = "wooden_sword" }
            }
        };

        // Act
        string json = JsonSerializer.Serialize(saveData, new JsonSerializerOptions { WriteIndented = true });
        var deserialized = JsonSerializer.Deserialize<SaveData>(json);

        // Assert
        AssertThat(deserialized.Version).IsEqual(1);
        AssertThat(deserialized.CurrentFloorIndex).IsEqual(2);
        AssertThat(deserialized.PlayerPosition.X).IsEqual(50);
        AssertThat(deserialized.PlayerPosition.Y).IsEqual(75);
        AssertThat(deserialized.Character.Name).IsEqual("TestHero");
        AssertThat(deserialized.Character.Level).IsEqual(5);
        AssertThat(deserialized.Character.Equipment.WeaponId).IsEqual("wooden_sword");
    }

    [TestCase]
    public void TestSaveSlotInfo_GetDisplayName()
    {
        // Arrange & Act & Assert
        var slot0 = new SaveSlotInfo { SlotIndex = 0 };
        AssertThat(slot0.GetDisplayName()).IsEqual("Slot 1");

        var slot1 = new SaveSlotInfo { SlotIndex = 1 };
        AssertThat(slot1.GetDisplayName()).IsEqual("Slot 2");

        var slot2 = new SaveSlotInfo { SlotIndex = 2 };
        AssertThat(slot2.GetDisplayName()).IsEqual("Slot 3");

        var autosave = new SaveSlotInfo { SlotIndex = 3 };
        AssertThat(autosave.GetDisplayName()).IsEqual("Autosave");
    }

    [TestCase]
    public void TestSaveSlotInfo_GetFloorName()
    {
        // Arrange & Act & Assert
        var groundFloor = new SaveSlotInfo { FloorIndex = 0 };
        AssertThat(groundFloor.GetFloorName()).IsEqual("Ground Floor");

        var floor1 = new SaveSlotInfo { FloorIndex = 1 };
        AssertThat(floor1.GetFloorName()).IsEqual("Floor 1F");

        var floor5 = new SaveSlotInfo { FloorIndex = 5 };
        AssertThat(floor5.GetFloorName()).IsEqual("Floor 5F");
    }
}
