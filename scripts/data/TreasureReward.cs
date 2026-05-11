using Godot;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public sealed class TreasureRewardItem
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; } = 1;

    public TreasureRewardItem() { }

    public TreasureRewardItem(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

[System.Serializable]
public sealed class TreasureReward
{
    public int Gold { get; set; }
    public List<TreasureRewardItem> Items { get; set; } = new();

    public bool HasAnyReward => Gold > 0 || (Items?.Any(item => item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.ItemId)) ?? false);

    public IReadOnlyList<string> ValidateAuthoredContent()
    {
        var errors = new List<string>();

        if (Gold < 0)
        {
            errors.Add($"Gold reward cannot be negative: {Gold}");
        }

        foreach (var item in Items ?? Enumerable.Empty<TreasureRewardItem>())
        {
            if (item == null)
            {
                errors.Add("Treasure reward item entry cannot be null");
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                errors.Add("Treasure reward item id cannot be empty");
                continue;
            }

            if (!ItemCatalog.ItemExists(item.ItemId))
            {
                errors.Add($"Treasure reward item id '{item.ItemId}' does not exist in ItemCatalog");
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Treasure reward item '{item.ItemId}' has invalid quantity {item.Quantity}");
            }
        }

        return errors;
    }

    public TreasureRewardGrantResult GrantTo(Character player)
    {
        var result = new TreasureRewardGrantResult();
        if (player == null)
        {
            result.Errors.Add("Cannot grant treasure reward to null player");
            return result;
        }

        if (Gold > 0)
        {
            player.GainGold(Gold);
            result.GoldGranted = Gold;
        }

        foreach (var rewardItem in Items ?? Enumerable.Empty<TreasureRewardItem>())
        {
            if (rewardItem == null || string.IsNullOrWhiteSpace(rewardItem.ItemId) || rewardItem.Quantity <= 0)
            {
                if (rewardItem?.ItemId != null)
                {
                    result.SkippedItemIds.Add(rewardItem.ItemId);
                }
                continue;
            }

            var item = ItemCatalog.CreateItemById(rewardItem.ItemId);
            if (item == null)
            {
                GD.PushWarning($"Treasure reward skipped unknown item '{rewardItem.ItemId}'");
                result.SkippedItemIds.Add(rewardItem.ItemId);
                continue;
            }

            player.TryAddItem(item, rewardItem.Quantity, out int addedQuantity);
            if (addedQuantity > 0)
            {
                result.ItemQuantitiesGranted[item.Id] = result.ItemQuantitiesGranted.TryGetValue(item.Id, out int existing)
                    ? existing + addedQuantity
                    : addedQuantity;
            }

            int overflow = rewardItem.Quantity - addedQuantity;
            if (overflow > 0)
            {
                if (RecoveryChest.Instance != null && GodotObject.IsInstanceValid(RecoveryChest.Instance))
                {
                    RecoveryChest.Instance.AddOverflow(item.Id, overflow);
                    result.ItemQuantitiesRecovered[item.Id] = result.ItemQuantitiesRecovered.TryGetValue(item.Id, out int existing)
                        ? existing + overflow
                        : overflow;
                }
                else
                {
                    GD.PushWarning($"Treasure reward overflow for '{item.Id}' could not be recovered because RecoveryChest is unavailable");
                    result.UnrecoveredItemQuantities[item.Id] = result.UnrecoveredItemQuantities.TryGetValue(item.Id, out int existing)
                        ? existing + overflow
                        : overflow;
                }
            }
        }

        return result;
    }
}

public sealed class TreasureRewardGrantResult
{
    public int GoldGranted { get; set; }
    public Dictionary<string, int> ItemQuantitiesGranted { get; } = new();
    public Dictionary<string, int> ItemQuantitiesRecovered { get; } = new();
    public Dictionary<string, int> UnrecoveredItemQuantities { get; } = new();
    public List<string> SkippedItemIds { get; } = new();
    public List<string> Errors { get; } = new();
}
