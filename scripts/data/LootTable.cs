using System;
using System.Collections.Generic;

/// <summary>
/// Defines the possible item drops for an enemy encounter.
/// Constructed in code via LootTableCatalog or from EnemyBlueprint exports.
/// </summary>
[System.Serializable]
public class LootTable
{
    public int MaxDrops { get; set; } = 3;

    private float _dropChance = 1.0f;
    public float DropChance
    {
        get => _dropChance;
        set => _dropChance = Math.Clamp(value, 0f, 1f);
    }

    public List<LootEntry> Entries { get; set; } = new();

    public List<LootEntry> GetGuaranteedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in Entries)
        {
            if (entry != null && entry.GuaranteedDrop)
                result.Add(entry);
        }
        return result;
    }

    public List<LootEntry> GetWeightedEntries()
    {
        var result = new List<LootEntry>();
        foreach (var entry in Entries)
        {
            if (entry != null && !entry.GuaranteedDrop && entry.Weight > 0)
                result.Add(entry);
        }
        return result;
    }
}

/// <summary>
/// A single item drop entry in a LootTable.
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

    public void ValidateAndNormalizeQuantityRange()
    {
        if (MinQuantity <= MaxQuantity)
            return;

        (MinQuantity, MaxQuantity) = (MaxQuantity, MinQuantity);
    }
}
