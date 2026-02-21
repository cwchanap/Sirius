using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines the possible item drops for an enemy encounter.
/// Constructed in code via LootTableCatalog or from EnemyBlueprint exports.
/// DropChance is clamped to [0.0, 1.0] on assignment.
/// MaxDrops is clamped to >= 0 on assignment; it caps only weighted draws,
/// not guaranteed drops.
/// </summary>
[System.Serializable]
public class LootTable
{
    private int _maxDrops = 3;
    public int MaxDrops
    {
        get => _maxDrops;
        set
        {
            if (value < 0)
                GD.PushWarning($"[LootTable] MaxDrops cannot be negative (got {value}); clamping to 0.");
            _maxDrops = Math.Max(0, value);
        }
    }

    private float _dropChance = 1.0f;
    public float DropChance
    {
        get => _dropChance;
        set => _dropChance = Math.Clamp(value, 0f, 1f);
    }

    private List<LootEntry> _entries = new();
    public List<LootEntry> Entries
    {
        get => _entries;
        set => _entries = value ?? new List<LootEntry>();
    }

    /// <summary>Returns non-null guaranteed entries (GuaranteedDrop = true).</summary>
    public List<LootEntry> GetGuaranteedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in _entries)
        {
            if (entry != null && entry.GuaranteedDrop)
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// Returns weighted (non-guaranteed) entries eligible for random selection.
    /// Entries with Weight = 0 are excluded.
    /// </summary>
    public List<LootEntry> GetWeightedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in _entries)
        {
            if (entry != null && !entry.GuaranteedDrop && entry.Weight > 0)
                result.Add(entry);
        }
        return result;
    }
}

/// <summary>
/// A single item drop entry in a LootTable.
/// Note: when GuaranteedDrop is true, Weight is ignored — set it to 0 by convention
/// to avoid the entry appearing in weighted draws.
/// </summary>
[System.Serializable]
public class LootEntry
{
    public string ItemId { get; set; } = string.Empty;

    private int _weight = 100;
    public int Weight
    {
        get => _weight;
        set => _weight = Math.Max(0, value);
    }

    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public bool GuaranteedDrop { get; set; } = false;
}
