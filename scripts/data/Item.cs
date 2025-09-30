using Godot;
using System;

public enum ItemCategory
{
    General = 0,
    Equipment = 1,
    Consumable = 2,
    Quest = 3
}

public enum EquipmentSlotType
{
    Weapon = 0,
    Shield = 1,
    Armor = 2,
    Helmet = 3,
    Shoe = 4,
    Accessory = 5
}

[System.Serializable]
public abstract partial class Item : Resource
{
    [Export]
    public string Id
    {
        get => _id;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _id = value.Trim();
            }
        }
    }

    [Export]
    public string DisplayName { get; set; } = "New Item";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export]
    public int Value { get; set; } = 0;

    [Export]
    private ItemCategory _category = ItemCategory.General;

    [Export(PropertyHint.File, "*.png,*.webp,*.jpg,*.jpeg,*.svg,*.tres,*.res")]
    public string AssetPath
    {
        get => _assetPath;
        set => _assetPath = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    protected Item()
    {
    }

    public ItemCategory Category => _category;

    public virtual bool CanStack => MaxStackSize > 1;

    public virtual int MaxStackSize => 99;

    protected void SetCategory(ItemCategory category)
    {
        _category = category;
    }

    public override string ToString()
    {
        return $"{DisplayName} ({Category})";
    }

    public bool TryLoadAsset<T>(out T asset) where T : Resource
    {
        asset = null;

        if (string.IsNullOrEmpty(_assetPath))
        {
            return false;
        }

        if (!ResourceLoader.Exists(_assetPath))
        {
            GD.PushWarning($"Asset at path '{_assetPath}' could not be found.");
            return false;
        }

        asset = ResourceLoader.Load<T>(_assetPath);
        return asset != null;
    }

    public T LoadAssetOrDefault<T>() where T : Resource
    {
        return TryLoadAsset<T>(out var asset) ? asset : null;
    }

    private string _id = Guid.NewGuid().ToString("N");
    private string _assetPath = string.Empty;
}

[System.Serializable]
public partial class GeneralItem : Item
{
    [Export]
    public int MaxStackOverride { get; set; } = 99;

    public GeneralItem()
    {
        SetCategory(ItemCategory.General);
    }

    public override int MaxStackSize => Mathf.Max(1, MaxStackOverride);
}

[System.Serializable]
public partial class EquipmentItem : Item
{
    [Export]
    public EquipmentSlotType SlotType { get; set; } = EquipmentSlotType.Weapon;

    [Export]
    public int AttackBonus { get; set; } = 0;

    [Export]
    public int DefenseBonus { get; set; } = 0;

    [Export]
    public int SpeedBonus { get; set; } = 0;

    [Export]
    public int HealthBonus { get; set; } = 0;

    public EquipmentItem()
    {
        SetCategory(ItemCategory.Equipment);
    }

    public override bool CanStack => false;

    public override int MaxStackSize => 1;
}
