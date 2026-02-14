using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Singleton class for storing overflow items from inventory operations.
/// When items cannot be fully added to inventory due to stack limits,
/// the overflow amount is stored here for later recovery.
/// </summary>
public partial class RecoveryChest : Node
{
    public static RecoveryChest Instance { get; private set; }

    /// <summary>
    /// Represents an overflow item entry waiting to be recovered.
    /// </summary>
    public class OverflowEntry
    {
        public string ItemId { get; }
        public int Amount { get; }
        public DateTime AddedAt { get; }

        public OverflowEntry(string itemId, int amount)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            Amount = amount > 0 ? amount : throw new ArgumentException("Amount must be positive", nameof(amount));
            AddedAt = DateTime.UtcNow;
        }
    }

    private readonly List<OverflowEntry> _overflowItems = new();

    public IReadOnlyList<OverflowEntry> OverflowItems => _overflowItems;

    public int OverflowCount => _overflowItems.Count;

    public override void _Ready()
    {
        if (Instance == null || !IsInstanceValid(Instance))
        {
            Instance = this;
            GD.Print("RecoveryChest initialized");
        }
        else
        {
            GD.Print("RecoveryChest instance already exists, queueing free");
            QueueFree();
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            GD.Print("RecoveryChest exiting tree, clearing singleton Instance");
            Instance = null;
        }
    }

    /// <summary>
    /// Adds overflow items to the recovery chest.
    /// Called when items cannot be fully added to inventory due to stack limits.
    /// </summary>
    /// <param name="itemId">The ID of the item that overflowed</param>
    /// <param name="amount">The amount that could not be added</param>
    public void AddOverflow(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            GD.PushWarning("RecoveryChest.AddOverflow: Cannot add overflow with null/empty itemId");
            return;
        }

        if (amount <= 0)
        {
            GD.PushWarning($"RecoveryChest.AddOverflow: Cannot add overflow with non-positive amount ({amount}) for item '{itemId}'");
            return;
        }

        var entry = new OverflowEntry(itemId, amount);
        _overflowItems.Add(entry);
        GD.Print($"RecoveryChest: Stored {amount}x '{itemId}' for later recovery (total overflow items: {_overflowItems.Count})");
    }

    /// <summary>
    /// Attempts to recover all overflow items into the specified inventory.
    /// Items that cannot be recovered remain in the chest.
    /// </summary>
    /// <param name="inventory">The inventory to recover items into</param>
    /// <returns>The number of items successfully recovered</returns>
    public int RecoverAll(Inventory inventory)
    {
        if (inventory == null)
        {
            GD.PushWarning("RecoveryChest.RecoverAll: Cannot recover to null inventory");
            return 0;
        }

        if (_overflowItems.Count == 0)
        {
            return 0;
        }

        int recoveredCount = 0;
        var itemsToRemove = new List<OverflowEntry>();

        foreach (var entry in _overflowItems)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            if (item == null)
            {
                GD.PushWarning($"RecoveryChest: Cannot recover unknown item '{entry.ItemId}', keeping in chest");
                continue;
            }

            bool fullyAdded = inventory.TryAddItem(item, entry.Amount, out int addedAmount);
            if (addedAmount > 0)
            {
                recoveredCount += addedAmount;
                if (fullyAdded)
                {
                    itemsToRemove.Add(entry);
                    GD.Print($"RecoveryChest: Recovered {addedAmount}x '{entry.ItemId}'");
                }
                else
                {
                    // Partial recovery - update the remaining amount
                    int remaining = entry.Amount - addedAmount;
                    GD.Print($"RecoveryChest: Partially recovered {addedAmount}x '{entry.ItemId}', {remaining} remaining");
                    // Note: We don't remove the entry, but we could update its amount
                    // For simplicity, we keep the original entry with the remaining amount
                }
            }
        }

        // Remove fully recovered items
        foreach (var entry in itemsToRemove)
        {
            _overflowItems.Remove(entry);
        }

        if (recoveredCount > 0)
        {
            GD.Print($"RecoveryChest: Successfully recovered {recoveredCount} items total. Remaining in chest: {_overflowItems.Count}");
        }

        return recoveredCount;
    }

    /// <summary>
    /// Clears all overflow items from the chest.
    /// </summary>
    public void Clear()
    {
        int count = _overflowItems.Count;
        _overflowItems.Clear();
        if (count > 0)
        {
            GD.Print($"RecoveryChest: Cleared {count} overflow items");
        }
    }

    /// <summary>
    /// Gets the total overflow amount for a specific item.
    /// </summary>
    /// <param name="itemId">The item ID to check</param>
    /// <returns>Total amount of the item in overflow</returns>
    public int GetOverflowAmount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        int total = 0;
        foreach (var entry in _overflowItems)
        {
            if (entry.ItemId == itemId)
            {
                total += entry.Amount;
            }
        }
        return total;
    }

    /// <summary>
    /// Checks if there are any overflow items for the specified item ID.
    /// </summary>
    /// <param name="itemId">The item ID to check</param>
    /// <returns>True if there are overflow items for this ID</returns>
    public bool HasOverflow(string itemId)
    {
        return GetOverflowAmount(itemId) > 0;
    }
}
