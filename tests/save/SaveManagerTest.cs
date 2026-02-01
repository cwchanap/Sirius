using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SaveManagerTest : Node
{
    [TestCase]
    public void TestIsValidSlot_ValidSlots()
    {
        // Valid slots are 0, 1, 2 (manual) and 3 (autosave)
        var saveManager = new SaveManager();
        saveManager.DeleteSave(0);
        saveManager.DeleteSave(1);
        saveManager.DeleteSave(2);
        saveManager.DeleteSave(3);
        AssertThat(saveManager.SaveExists(0)).IsFalse();
        AssertThat(saveManager.SaveExists(1)).IsFalse();
        AssertThat(saveManager.SaveExists(2)).IsFalse();
        AssertThat(saveManager.SaveExists(3)).IsFalse();

        // Invalid slots should return false
        AssertThat(saveManager.SaveExists(-1)).IsFalse();
        AssertThat(saveManager.SaveExists(4)).IsFalse();
        saveManager.Free();
    }

    [TestCase]
    public void TestSaveSlotInfo_Initialization()
    {
        // Arrange & Act
        var info = new SaveSlotInfo();

        // Assert - default values
        AssertThat(info.Exists).IsFalse();
        AssertThat(info.SlotIndex).IsEqual(0);
        AssertThat(info.PlayerLevel).IsEqual(0);
        AssertThat(info.FloorIndex).IsEqual(0);
    }

    [TestCase]
    public void TestSaveSlotInfo_WithValues()
    {
        // Arrange & Act
        var info = new SaveSlotInfo
        {
            Exists = true,
            SlotIndex = 2,
            PlayerName = "TestHero",
            PlayerLevel = 10,
            FloorIndex = 3,
            Timestamp = new System.DateTime(2024, 6, 15, 12, 0, 0)
        };

        // Assert
        AssertThat(info.Exists).IsTrue();
        AssertThat(info.SlotIndex).IsEqual(2);
        AssertThat(info.PlayerName).IsEqual("TestHero");
        AssertThat(info.PlayerLevel).IsEqual(10);
        AssertThat(info.FloorIndex).IsEqual(3);
        AssertThat(info.GetDisplayName()).IsEqual("Slot 3");
        AssertThat(info.GetFloorName()).IsEqual("Floor 3F");
    }

    [TestCase]
    public void TestSaveSlotInfo_AutosaveDisplayName()
    {
        // Arrange
        var info = new SaveSlotInfo { SlotIndex = 3 };

        // Act & Assert
        AssertThat(info.GetDisplayName()).IsEqual("Autosave");
    }

    [TestCase]
    public void TestSaveSlotInfo_GroundFloorDisplayName()
    {
        // Arrange
        var info = new SaveSlotInfo { FloorIndex = 0 };

        // Act & Assert
        AssertThat(info.GetFloorName()).IsEqual("Ground Floor");
    }

    [TestCase]
    public void TestSaveSlotInfo_UpperFloorDisplayName()
    {
        // Arrange
        var info = new SaveSlotInfo { FloorIndex = 5 };

        // Act & Assert
        AssertThat(info.GetFloorName()).IsEqual("Floor 5F");
    }

    [TestCase]
    public void TestSaveData_DefaultVersion()
    {
        // Arrange & Act
        var saveData = new SaveData();

        // Assert
        AssertThat(saveData.Version).IsEqual(1);
    }

    [TestCase]
    public void TestSaveData_CanSetAllFields()
    {
        // Arrange & Act
        var saveData = new SaveData
        {
            Version = 2,
            CurrentFloorIndex = 3,
            PlayerPosition = new Vector2IDto { X = 100, Y = 50 },
            SaveTimestamp = new System.DateTime(2024, 12, 25, 10, 30, 0),
            Character = new CharacterSaveData { Name = "Santa", Level = 99 }
        };

        // Assert
        AssertThat(saveData.Version).IsEqual(2);
        AssertThat(saveData.CurrentFloorIndex).IsEqual(3);
        AssertThat(saveData.PlayerPosition.X).IsEqual(100);
        AssertThat(saveData.PlayerPosition.Y).IsEqual(50);
        AssertThat(saveData.Character.Name).IsEqual("Santa");
        AssertThat(saveData.Character.Level).IsEqual(99);
    }

    [TestCase]
    public void TestSaveAndLoad_CompleteCycle()
    {
        // Arrange
        var saveManager = new SaveManager();
        var saveData = new SaveData
        {
            Version = 1,
            CurrentFloorIndex = 2,
            PlayerPosition = new Vector2IDto { X = 50, Y = 25 },
            SaveTimestamp = System.DateTime.UtcNow,
            Character = new CharacterSaveData { Name = "TestHero", Level = 5 }
        };

        // Act - Save the data
        bool saveSuccess = saveManager.SaveGame(0, saveData);
        AssertThat(saveSuccess).IsTrue();

        // Assert - Verify save file exists
        AssertThat(saveManager.SaveExists(0)).IsTrue();

        // Act - Load the data
        var loadedData = saveManager.LoadGame(0);

        // Assert - Verify loaded data matches saved data
        AssertThat(loadedData).IsNotNull();
        AssertThat(loadedData.Version).IsEqual(saveData.Version);
        AssertThat(loadedData.CurrentFloorIndex).IsEqual(saveData.CurrentFloorIndex);
        AssertThat(loadedData.PlayerPosition.X).IsEqual(saveData.PlayerPosition.X);
        AssertThat(loadedData.PlayerPosition.Y).IsEqual(saveData.PlayerPosition.Y);
        AssertThat(loadedData.Character.Name).IsEqual(saveData.Character.Name);
        AssertThat(loadedData.Character.Level).IsEqual(saveData.Character.Level);

        // Cleanup
        saveManager.DeleteSave(0);
        saveManager.Free();
    }

    [TestCase]
    public void TestAutoSave_CompleteCycle()
    {
        // Arrange
        var saveManager = new SaveManager();
        var saveData = new SaveData
        {
            Version = 1,
            CurrentFloorIndex = 1,
            PlayerPosition = new Vector2IDto { X = 10, Y = 10 },
            SaveTimestamp = System.DateTime.UtcNow,
            Character = new CharacterSaveData { Name = "AutoHero", Level = 3 }
        };

        // Act - Auto save
        bool saveSuccess = saveManager.AutoSave(saveData);
        AssertThat(saveSuccess).IsTrue();

        // Assert - Verify autosave exists (slot 3)
        AssertThat(saveManager.SaveExists(3)).IsTrue();

        // Act - Load autosave
        var loadedData = saveManager.LoadAutosave();

        // Assert - Verify loaded data
        AssertThat(loadedData).IsNotNull();
        AssertThat(loadedData.Character.Name).IsEqual(saveData.Character.Name);

        // Cleanup
        saveManager.DeleteSave(3);
        saveManager.Free();
    }

    [TestCase]
    public void TestSaveData_NullSaveData()
    {
        // Arrange
        var saveManager = new SaveManager();

        // Act - Try to save null data
        bool result = saveManager.SaveGame(0, null);

        // Assert - Should fail gracefully
        AssertThat(result).IsFalse();
        AssertThat(saveManager.SaveExists(0)).IsFalse();

        saveManager.Free();
    }

    [TestCase]
    public void TestSaveData_InvalidSlots()
    {
        // Arrange
        var saveManager = new SaveManager();
        var saveData = new SaveData { Version = 1 };

        // Act & Assert - Invalid slots should fail
        AssertThat(saveManager.SaveGame(-1, saveData)).IsFalse();
        AssertThat(saveManager.SaveGame(3, saveData)).IsFalse(); // 3 is autosave, use AutoSave() instead
        AssertThat(saveManager.SaveGame(4, saveData)).IsFalse();

        // Load operations should also handle invalid slots
        AssertThat(saveManager.LoadGame(-1)).IsNull();
        AssertThat(saveManager.LoadGame(3)).IsNull(); // Use LoadAutosave() instead
        AssertThat(saveManager.LoadGame(4)).IsNull();

        saveManager.Free();
    }

    [TestCase]
    public void TestGetSaveSlotInfo_EmptySlot()
    {
        // Arrange
        var saveManager = new SaveManager();

        // Ensure slot is empty
        saveManager.DeleteSave(0);

        // Act
        var info = saveManager.GetSaveSlotInfo(0);

        // Assert
        AssertThat(info.Exists).IsFalse();
        AssertThat(info.SlotIndex).IsEqual(0);

        saveManager.Free();
    }

    [TestCase]
    public void TestGetSaveSlotInfo_FilledSlot()
    {
        // Arrange
        var saveManager = new SaveManager();
        var saveData = new SaveData
        {
            Version = 1,
            CurrentFloorIndex = 5,
            Character = new CharacterSaveData { Name = "Hero", Level = 10 }
        };

        // Act - Save and get info
        saveManager.SaveGame(0, saveData);
        var info = saveManager.GetSaveSlotInfo(0);

        // Assert
        AssertThat(info.Exists).IsTrue();
        AssertThat(info.SlotIndex).IsEqual(0);
        AssertThat(info.PlayerName).IsEqual("Hero");
        AssertThat(info.PlayerLevel).IsEqual(10);
        AssertThat(info.FloorIndex).IsEqual(5);
        AssertThat(info.GetDisplayName()).IsEqual("Slot 1");

        // Cleanup
        saveManager.DeleteSave(0);
        saveManager.Free();
    }
}
