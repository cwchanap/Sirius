using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Autoload singleton for save/load operations.
/// Persists across scene transitions.
/// </summary>
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; }

    private const string SaveDir = "user://saves";
    private const string SlotFileFormat = "slot_{0}.json";
    private const string AutosaveFile = "autosave.json";

    /// <summary>
    /// Pending save data for scene transitions (MainMenu -> Game).
    /// Set before changing to Game scene, cleared after loading.
    /// </summary>
    public SaveData PendingLoadData { get; set; }

    [Signal]
    public delegate void SaveCompletedEventHandler(bool success, int slot);

    [Signal]
    public delegate void LoadCompletedEventHandler(bool success, int slot);

    public override void _Ready()
    {
        if (Instance == null)
        {
            Instance = this;
            EnsureSaveDirectoryExists();
            GD.Print("SaveManager initialized as autoload singleton");
        }
        else
        {
            GD.Print("SaveManager instance already exists, queueing free");
            QueueFree();
        }
    }

    private void EnsureSaveDirectoryExists()
    {
        using var dir = DirAccess.Open("user://");
        if (dir != null && !dir.DirExists("saves"))
        {
            var err = dir.MakeDir("saves");
            if (err == Error.Ok)
            {
                GD.Print("Created save directory: user://saves");
            }
            else
            {
                GD.PushError($"Failed to create save directory: {err}");
            }
        }
    }

    /// <summary>
    /// Gets the file name for a save slot.
    /// </summary>
    private string GetSlotFileName(int slot)
    {
        return slot == 3 ? AutosaveFile : string.Format(SlotFileFormat, slot);
    }

    /// <summary>
    /// Validates that a slot index is valid (0-3).
    /// </summary>
    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot <= 3;
    }

    /// <summary>
    /// Saves game to a manual slot (0-2).
    /// </summary>
    public bool SaveGame(int slot, SaveData data)
    {
        if (slot < 0 || slot > 2)
        {
            GD.PushError($"Invalid save slot: {slot} (valid: 0-2)");
            EmitSignal(SignalName.SaveCompleted, false, slot);
            return false;
        }

        string fileName = string.Format(SlotFileFormat, slot);
        bool success = SaveToFile(fileName, data);
        EmitSignal(SignalName.SaveCompleted, success, slot);
        return success;
    }

    /// <summary>
    /// Saves game to the autosave slot.
    /// </summary>
    public bool AutoSave(SaveData data)
    {
        bool success = SaveToFile(AutosaveFile, data);
        EmitSignal(SignalName.SaveCompleted, success, 3); // 3 = autosave slot
        return success;
    }

    private bool SaveToFile(string fileName, SaveData data)
    {
        try
        {
            string path = $"{SaveDir}/{fileName}";
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"Failed to open file for writing: {path} (Error: {FileAccess.GetOpenError()})");
                return false;
            }

            file.StoreString(json);
            GD.Print($"Game saved successfully to {path}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Save failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads game from a manual slot (0-2).
    /// </summary>
    public SaveData LoadGame(int slot)
    {
        if (slot < 0 || slot > 2)
        {
            GD.PushError($"Invalid save slot: {slot} (valid: 0-2)");
            EmitSignal(SignalName.LoadCompleted, false, slot);
            return null;
        }

        string fileName = string.Format(SlotFileFormat, slot);
        var data = LoadFromFile(fileName);
        EmitSignal(SignalName.LoadCompleted, data != null, slot);
        return data;
    }

    /// <summary>
    /// Loads game from the autosave slot.
    /// </summary>
    public SaveData LoadAutosave()
    {
        var data = LoadFromFile(AutosaveFile);
        EmitSignal(SignalName.LoadCompleted, data != null, 3);
        return data;
    }

    private SaveData LoadFromFile(string fileName)
    {
        try
        {
            string path = $"{SaveDir}/{fileName}";

            if (!FileAccess.FileExists(path))
            {
                GD.Print($"Save file not found: {path}");
                return null;
            }

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError($"Failed to open file for reading: {path} (Error: {FileAccess.GetOpenError()})");
                return null;
            }

            string json = file.GetAsText();
            var data = JsonSerializer.Deserialize<SaveData>(json);
            GD.Print($"Game loaded successfully from {path}");
            return data;
        }
        catch (Exception ex)
        {
            GD.PushError($"Load failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets metadata for a save slot without loading full data.
    /// </summary>
    /// <param name="slot">Slot index (0-2 for manual, 3 for autosave)</param>
    public SaveSlotInfo GetSaveSlotInfo(int slot)
    {
        if (!IsValidSlot(slot))
        {
            GD.PushError($"Invalid save slot: {slot} (valid: 0-3)");
            return new SaveSlotInfo { Exists = false, SlotIndex = slot };
        }

        string fileName = GetSlotFileName(slot);
        string path = $"{SaveDir}/{fileName}";

        if (!FileAccess.FileExists(path))
        {
            return new SaveSlotInfo { Exists = false, SlotIndex = slot };
        }

        try
        {
            var data = LoadFromFile(fileName);
            if (data == null)
            {
                return new SaveSlotInfo { Exists = false, SlotIndex = slot };
            }

            return new SaveSlotInfo
            {
                Exists = true,
                SlotIndex = slot,
                PlayerName = data.Character?.Name ?? "Unknown",
                PlayerLevel = data.Character?.Level ?? 1,
                FloorIndex = data.CurrentFloorIndex,
                Timestamp = data.SaveTimestamp
            };
        }
        catch
        {
            return new SaveSlotInfo { Exists = false, SlotIndex = slot };
        }
    }

    /// <summary>
    /// Checks if a save slot has data.
    /// </summary>
    public bool SaveExists(int slot)
    {
        if (!IsValidSlot(slot))
        {
            return false;
        }

        string fileName = GetSlotFileName(slot);
        return FileAccess.FileExists($"{SaveDir}/{fileName}");
    }

    /// <summary>
    /// Deletes a save file.
    /// </summary>
    public bool DeleteSave(int slot)
    {
        if (!IsValidSlot(slot))
        {
            GD.PushError($"Invalid save slot: {slot} (valid: 0-3)");
            return false;
        }

        string fileName = GetSlotFileName(slot);
        string path = $"{SaveDir}/{fileName}";

        if (!FileAccess.FileExists(path))
        {
            return false;
        }

        using var dir = DirAccess.Open(SaveDir);
        if (dir != null)
        {
            var err = dir.Remove(fileName);
            if (err == Error.Ok)
            {
                GD.Print($"Deleted save file: {path}");
                return true;
            }
            GD.PushError($"Failed to delete save file: {err}");
        }
        return false;
    }
}

/// <summary>
/// Metadata about a save slot for UI display.
/// </summary>
public class SaveSlotInfo
{
    public bool Exists { get; set; }
    public int SlotIndex { get; set; }
    public string PlayerName { get; set; }
    public int PlayerLevel { get; set; }
    public int FloorIndex { get; set; }
    public DateTime Timestamp { get; set; }

    public string GetDisplayName()
    {
        return SlotIndex == 3 ? "Autosave" : $"Slot {SlotIndex + 1}";
    }

    public string GetFloorName()
    {
        return FloorIndex == 0 ? "Ground Floor" : $"Floor {FloorIndex}F";
    }
}
