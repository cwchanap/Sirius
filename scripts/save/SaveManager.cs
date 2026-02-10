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

    private static readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Pending save data for scene transitions (MainMenu -> Game).
    /// Set before changing to Game scene, cleared after loading.
    /// </summary>
    public SaveData? PendingLoadData { get; internal set; }

    [Signal]
    public delegate void SaveCompletedEventHandler(bool success, int slot);

    [Signal]
    public delegate void LoadCompletedEventHandler(bool success, int slot);

    public override void _Ready()
    {
        if (Instance == null || !IsInstanceValid(Instance))
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

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    internal void EnsureSaveDirectoryExists()
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
            if (data == null)
            {
                GD.PushError("Save failed: SaveData is null");
                return false;
            }

            string path = $"{SaveDir}/{fileName}";
            string tempFileName = $"{fileName}.tmp";
            string tempPath = $"{SaveDir}/{tempFileName}";
            string json = JsonSerializer.Serialize(data, _jsonOptions);

            using var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError($"Failed to open file for writing: {tempPath} (Error: {FileAccess.GetOpenError()})");
                return false;
            }

            file.StoreString(json);
            file.Flush();
            file.Close();  // Close before renaming to avoid file lock on Windows

            using var dir = DirAccess.Open(SaveDir);
            if (dir == null)
            {
                GD.PushError($"Failed to open save directory: {SaveDir}");
                // Attempt fallback cleanup using System.IO.File.Delete
                try
                {
                    string systemTempPath = ProjectSettings.GlobalizePath(tempPath);
                    if (System.IO.File.Exists(systemTempPath))
                    {
                        System.IO.File.Delete(systemTempPath);
                        GD.Print($"Cleaned up orphaned temp file: {systemTempPath}");
                    }
                    else
                    {
                        GD.PushWarning($"Temp file not found for cleanup: {systemTempPath}");
                    }
                }
                catch (System.IO.IOException ex)
                {
                    GD.PushError($"Failed to cleanup orphaned temp file {tempPath}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    GD.PushError($"Unexpected error during temp file cleanup: {ex.Message}");
                }
                return false;
            }

            // Safely replace existing file: rename old to backup, then rename temp to target.
            // If rename fails, restore from backup to avoid data loss.
            string backupFileName = $"{fileName}.bak";
            bool hadExisting = dir.FileExists(fileName);

            if (hadExisting)
            {
                // Remove stale backup if present
                if (dir.FileExists(backupFileName))
                {
                    dir.Remove(backupFileName);
                }

                var backupErr = dir.Rename(fileName, backupFileName);
                if (backupErr != Error.Ok)
                {
                    GD.PushError($"Failed to back up existing save file: {backupErr}");
                    dir.Remove(tempFileName);
                    return false;
                }
            }

            var renameErr = dir.Rename(tempFileName, fileName);
            if (renameErr != Error.Ok)
            {
                GD.PushError($"Failed to finalize save file: {renameErr}");
                dir.Remove(tempFileName);

                // Restore original file from backup
                if (hadExisting && dir.FileExists(backupFileName))
                {
                    var restoreErr = dir.Rename(backupFileName, fileName);
                    if (restoreErr != Error.Ok)
                    {
                        GD.PushError($"Failed to restore backup save file: {restoreErr}");
                    }
                    else
                    {
                        GD.Print("Restored original save file from backup after failed rename");
                    }
                }
                return false;
            }

            // Clean up backup after successful rename
            if (hadExisting && dir.FileExists(backupFileName))
            {
                dir.Remove(backupFileName);
            }

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
    public SaveData? LoadGame(int slot)
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
    public SaveData? LoadAutosave()
    {
        var data = LoadFromFile(AutosaveFile);
        EmitSignal(SignalName.LoadCompleted, data != null, 3);
        return data;
    }

    private SaveData? LoadFromFile(string fileName)
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

            // Validate save file version
            if (data == null)
            {
                GD.PushError($"Failed to deserialize save data from {fileName}");
                return null;
            }

            const int CurrentVersion = 1;
            if (data.Version > CurrentVersion)
            {
                GD.PushError($"Save file version {data.Version} is newer than supported version {CurrentVersion}");
                return null;
            }

            // Add version migration logic here if needed in the future
            // if (data.Version < CurrentVersion) { /* migrate */ }

            GD.Print($"Game loaded successfully from {path} (version {data.Version})");
            return data;
        }
        catch (JsonException ex)
        {
            GD.PushError($"Load failed for {fileName}: Invalid JSON format");
            GD.Print($"JSON error: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            GD.PushError($"Load failed for {fileName}: {ex.GetType().Name}: {ex.Message}");
            GD.Print($"Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Loads and inspects full SaveData via LoadFromFile to build SaveSlotInfo.
    /// Note: This deserializes the entire save file to read metadata.
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
                // File exists but couldn't be loaded - it's corrupted
                return new SaveSlotInfo
                {
                    Exists = true,
                    IsCorrupted = true,
                    SlotIndex = slot,
                    PlayerName = "Corrupted Save",
                    PlayerLevel = 0
                };
            }

            // Validate critical save data fields
            if (data.PlayerPosition == null || data.Character == null || data.CurrentFloorIndex < 0)
            {
                return new SaveSlotInfo
                {
                    Exists = true,
                    IsCorrupted = true,
                    SlotIndex = slot,
                    PlayerName = "Corrupted Save",
                    PlayerLevel = 0
                };
            }

            // Guard against null or empty character names with a safe fallback
            string playerName = data.Character.Name;
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Unknown";
            }

            return new SaveSlotInfo
            {
                Exists = true,
                SlotIndex = slot,
                PlayerName = playerName,
                PlayerLevel = data.Character.Level,
                FloorIndex = data.CurrentFloorIndex,
                Timestamp = data.SaveTimestamp
            };
        }
        catch (Exception ex)
        {
            GD.PushError($"Failed to read save slot {slot}: {ex.GetType().Name}: {ex.Message}");
            GD.Print($"Stack trace: {ex.StackTrace}");
            return new SaveSlotInfo
            {
                Exists = true,
                IsCorrupted = true,
                SlotIndex = slot,
                PlayerName = "Corrupted Save",
                PlayerLevel = 0
            };
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
    public bool IsCorrupted { get; set; }
    public int SlotIndex { get; set; }
    public string? PlayerName { get; set; }
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
