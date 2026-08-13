using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static GdUnit4.Assertions;

public static class TestHelpers
{
    /// <summary>
    /// Every persistent save file (primary, .bak, .tmp) across the three manual
    /// slots and the autosave slot. Tests that instantiate production MainMenu
    /// (or anything else that touches <see cref="SaveManager"/>) snapshot/restore
    /// this set so they cannot mutate a developer's real <c>user://saves</c>.
    /// Kept in sync with <see cref="SaveManager"/> symbols by
    /// <c>MainMenuTest.MainMenuSavePathsAgreeWithSaveManagerSymbols</c>.
    /// </summary>
    public static readonly string[] UserSaveFilePaths =
    {
        "user://saves/slot_0.json",
        "user://saves/slot_0.json.bak",
        "user://saves/slot_0.json.tmp",
        "user://saves/slot_1.json",
        "user://saves/slot_1.json.bak",
        "user://saves/slot_1.json.tmp",
        "user://saves/slot_2.json",
        "user://saves/slot_2.json.bak",
        "user://saves/slot_2.json.tmp",
        "user://saves/autosave.json",
        "user://saves/autosave.json.bak",
        "user://saves/autosave.json.tmp"
    };

    /// <summary>
    /// Immutable snapshot of one save file's on-disk state at capture time.
    /// </summary>
    public sealed record SaveFileSnapshot(string VirtualPath, bool Exists, byte[]? Data);

    /// <summary>
    /// Capture the current on-disk state of every path in
    /// <see cref="UserSaveFilePaths"/>. Call in <c>[BeforeTest]</c> before any
    /// code that could mutate <c>user://saves</c>.
    /// </summary>
    public static SaveFileSnapshot[] CaptureSaveFiles() =>
        UserSaveFilePaths.Select(path =>
        {
            var absolutePath = ProjectSettings.GlobalizePath(path);
            var exists = System.IO.File.Exists(absolutePath);
            return new SaveFileSnapshot(
                path,
                exists,
                exists ? System.IO.File.ReadAllBytes(absolutePath) : null);
        }).ToArray();

    /// <summary>
    /// Restore the captured on-disk state: rewrite files that existed, delete
    /// files that did not. Call in <c>[AfterTest]</c> (ideally in a
    /// <c>finally</c> block so cleanup runs even on assertion failure).
    /// </summary>
    public static void RestoreSaveFiles(SaveFileSnapshot[] snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var absolutePath = ProjectSettings.GlobalizePath(snapshot.VirtualPath);
            if (snapshot.Exists)
            {
                var directory = System.IO.Path.GetDirectoryName(absolutePath);
                if (!string.IsNullOrEmpty(directory))
                    System.IO.Directory.CreateDirectory(directory);

                System.IO.File.WriteAllBytes(absolutePath, snapshot.Data!);
            }
            else if (System.IO.File.Exists(absolutePath))
            {
                System.IO.File.Delete(absolutePath);
            }
        }
    }

    /// <summary>
    /// Push <see cref="GD.PushError"/> for any snapshot entry whose restored
    /// on-disk state (existence or content) does not match the capture. Used to
    /// surface silent restore failures. <paramref name="tag"/> labels the
    /// originating fixture in the error messages.
    /// </summary>
    public static void ReportSaveFileMismatches(SaveFileSnapshot[] snapshots, string tag)
    {
        foreach (var snapshot in snapshots)
        {
            var absolutePath = ProjectSettings.GlobalizePath(snapshot.VirtualPath);
            var exists = System.IO.File.Exists(absolutePath);
            if (exists != snapshot.Exists)
            {
                GD.PushError(
                    $"[{tag}] save-file restore mismatch for {snapshot.VirtualPath}: " +
                    $"expected Exists={snapshot.Exists}, actual Exists={exists}");
                continue;
            }

            if (snapshot.Exists)
            {
                var bytes = System.IO.File.ReadAllBytes(absolutePath);
                if (!bytes.SequenceEqual(snapshot.Data!))
                {
                    GD.PushError(
                        $"[{tag}] save-file restore content mismatch for " +
                        $"{snapshot.VirtualPath}");
                }
            }
        }
    }

    /// <summary>
    /// Reset the <see cref="GameManager"/> singleton to null via reflection.
    /// Tries the public Instance setter first, then falls back to the
    /// compiler-generated backing field, and throws if neither is available.
    /// </summary>
    public static void ResetGameManagerSingleton()
    {
        var property = typeof(GameManager).GetProperty(
            "Instance",
            BindingFlags.Public | BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object[] { null! });
            return;
        }

        var field = typeof(GameManager).GetField(
            "<Instance>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
            return;
        }

        throw new InvalidOperationException(
            "Failed to reset GameManager singleton: no Instance setter or backing field found.");
    }

    public static Character CreateTestCharacter() => new Character
    {
        Name             = "TestHero",
        Level            = 1,
        MaxHealth        = 100,
        CurrentHealth    = 100,
        Attack           = 20,
        Defense          = 10,
        Speed            = 15,
        Experience       = 0,
        ExperienceToNext = 100,
        Gold             = 0,
    };

    public static Dictionary<string, int> TreasureBoxRewardItems(TreasureBoxSpawn box)
    {
        var result = new Dictionary<string, int>();
        if (box.RewardItemIds == null)
        {
            return result;
        }

        for (var i = 0; i < box.RewardItemIds.Count; i++)
        {
            var itemId = box.RewardItemIds[i];
            var quantity = box.RewardItemQuantities != null && i < box.RewardItemQuantities.Count
                ? box.RewardItemQuantities[i]
                : 1;
            result[itemId] = quantity;
        }

        return result;
    }

    /// <summary>
    /// Build a SaveData fixture suitable for writing to any slot. Centralized
    /// so save-touching tests share one source of truth for the shape of a
    /// "valid" save (version, character stats, position). The caller may override
    /// <paramref name="characterName"/> when a test asserts on a specific hero
    /// name; all other fields use the canonical fixture values.
    /// </summary>
    public static SaveData CreateValidSaveData(string characterName = "Aster") => new()
    {
        Version = SaveData.CurrentVersion,
        CurrentFloorIndex = 0,
        PlayerPosition = new Vector2IDto { X = 6, Y = 50 },
        Character = new CharacterSaveData
        {
            Name = characterName,
            Level = 4,
            MaxHealth = 100,
            CurrentHealth = 100,
            Attack = 20,
            Defense = 10,
            Speed = 15,
            ExperienceToNext = 100
        },
        SaveTimestamp = DateTime.UtcNow
    };

    /// <summary>
    /// Write a valid SaveData fixture (via <see cref="CreateValidSaveData"/>)
    /// to <paramref name="slot"/>. Slot 3 routes through
    /// <see cref="SaveManager.AutoSave"/>; all other slots go through
    /// <see cref="SaveManager.SaveGame"/>. Asserts the underlying write succeeded.
    /// </summary>
    public static void WriteValidSlot(int slot, string characterName = "Aster")
    {
        var manager = SaveManager.Instance;
        AssertThat(manager).IsNotNull();
        if (manager == null)
            return;

        var data = CreateValidSaveData(characterName);
        var success = slot == 3
            ? manager.AutoSave(data)
            : manager.SaveGame(slot, data);
        AssertThat(success).IsTrue();
    }
}
