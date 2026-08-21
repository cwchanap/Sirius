using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public enum UiIconSize
{
    Metadata = 16,
    Default = 24,
    Feature = 32
}

public enum UiIconId
{
    Health, Mana, Experience, Level, Gold, Attack, Defense, Speed,
    Poison, Burn, Stun, Weaken, Slow, Blind, Regen, Haste, Strength, Fortify,
    General, Equipment, Consumable, Quest,
    Weapon, Shield, Armor, Helmet, Shoe, Accessory, ActiveSkill, Locked,
    Equip, Unequip, Use, Assign, Buy, Sell,
    Pause, Resume, Settings, Save, Load,
    Dialogue, Shop, Heal, Puzzle, Reward,
    Info, Warning, Error, Confirm, CancelClose,
    Keyboard, KeycapBlank, Mouse, MousePrimary, MouseSecondary, MouseWheel,
    Gamepad, GamepadFaceBlank, GamepadDpad, GamepadStick, GamepadShoulder
}

public enum UiOrnamentId
{
    CelestialAnchor, OrbitArc, TrajectoryLine, CalibrationTicks, CalloutFrame,
    CalloutConnector, CatalogueRailEndcap, IgnitionSeal, ConstellationCorner,
    ConstellationDivider, PartialSigil, FocusHalo, SelectionHalo
}

public enum UiEffectId
{
    EncounterBurst, HitImpact, StatusPulse, RewardLevelUp
}

public static class UiArtCatalog
{
    private static readonly HashSet<string> MissingPaths = new();
    private static Func<string, bool> ResourceExists = path => ResourceLoader.Exists(path);

    public static string GetIconPath(UiIconId id, UiIconSize size)
    {
        if (!Enum.IsDefined(id) || !Enum.IsDefined(size))
            throw new ArgumentOutOfRangeException();

        return $"res://assets/sprites/ui/icons/{CategoryFor(id)}/{(int)size}/{ToSnakeCase(id.ToString())}.png";
    }

    public static Texture2D? LoadIcon(UiIconId id, UiIconSize size)
    {
        if (!Enum.IsDefined(id))
            id = UiIconId.Info;

        var texture = LoadOnce<Texture2D>(GetIconPath(id, size));
        return texture ?? (id == UiIconId.Info
            ? null
            : LoadOnce<Texture2D>(GetIconPath(UiIconId.Info, size)));
    }

    public static Texture2D? LoadContentTexture(string path) => LoadOnce<Texture2D>(path);

    public static string GetOrnamentPath(UiOrnamentId id)
    {
        if (!Enum.IsDefined(id))
            throw new ArgumentOutOfRangeException(nameof(id), id, null);

        return $"res://assets/sprites/ui/ornaments/{ToSnakeCase(id.ToString())}.png";
    }

    public static Texture2D? LoadOrnament(UiOrnamentId id) =>
        LoadOnce<Texture2D>(GetOrnamentPath(id));

    public static string GetEffectPath(UiEffectId id)
    {
        if (!Enum.IsDefined(id))
            throw new ArgumentOutOfRangeException(nameof(id), id, null);

        return $"res://assets/sprites/effects/ui/{ToSnakeCase(id.ToString())}.png";
    }

    public static Texture2D? LoadEffect(UiEffectId id) =>
        LoadOnce<Texture2D>(GetEffectPath(id));

    public static bool TryForStatusEffect(StatusEffectType type, out UiIconId id)
    {
        var mapped = type switch
        {
            StatusEffectType.Poison => (UiIconId?)UiIconId.Poison,
            StatusEffectType.Burn => UiIconId.Burn,
            StatusEffectType.Stun => UiIconId.Stun,
            StatusEffectType.Weaken => UiIconId.Weaken,
            StatusEffectType.Slow => UiIconId.Slow,
            StatusEffectType.Blind => UiIconId.Blind,
            StatusEffectType.Regen => UiIconId.Regen,
            StatusEffectType.Haste => UiIconId.Haste,
            StatusEffectType.Strength => UiIconId.Strength,
            StatusEffectType.Fortify => UiIconId.Fortify,
            _ => null
        };
        id = mapped ?? UiIconId.Info;
        return mapped.HasValue;
    }

    public static UiIconId ForStatusEffect(StatusEffectType type) =>
        TryForStatusEffect(type, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(type), type, null);

    public static UiIconId ForItemCategory(ItemCategory category) => category switch
    {
        ItemCategory.General => UiIconId.General,
        ItemCategory.Equipment => UiIconId.Equipment,
        ItemCategory.Consumable => UiIconId.Consumable,
        ItemCategory.Quest => UiIconId.Quest,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    public static UiIconId ForEquipmentSlot(EquipmentSlotType slot) => slot switch
    {
        EquipmentSlotType.Weapon => UiIconId.Weapon,
        EquipmentSlotType.Shield => UiIconId.Shield,
        EquipmentSlotType.Armor => UiIconId.Armor,
        EquipmentSlotType.Helmet => UiIconId.Helmet,
        EquipmentSlotType.Shoe => UiIconId.Shoe,
        EquipmentSlotType.Accessory => UiIconId.Accessory,
        _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
    };

    private static string CategoryFor(UiIconId id) => id switch
    {
        UiIconId.Health or UiIconId.Mana or UiIconId.Experience or UiIconId.Level or
            UiIconId.Gold or UiIconId.Attack or UiIconId.Defense or UiIconId.Speed
            => "stats",
        UiIconId.Poison or UiIconId.Burn or UiIconId.Stun or UiIconId.Weaken or
            UiIconId.Slow or UiIconId.Blind or UiIconId.Regen or UiIconId.Haste or
            UiIconId.Strength or UiIconId.Fortify => "status",
        UiIconId.General or UiIconId.Equipment or UiIconId.Consumable or UiIconId.Quest or
            UiIconId.Weapon or UiIconId.Shield or UiIconId.Armor or UiIconId.Helmet or
            UiIconId.Shoe or UiIconId.Accessory or UiIconId.ActiveSkill or UiIconId.Locked
            => "inventory",
        UiIconId.Equip or UiIconId.Unequip or UiIconId.Use or UiIconId.Assign or
            UiIconId.Buy or UiIconId.Sell => "actions",
        UiIconId.Pause or UiIconId.Resume or UiIconId.Settings or UiIconId.Save or
            UiIconId.Load => "flow",
        UiIconId.Dialogue or UiIconId.Shop or UiIconId.Heal or UiIconId.Puzzle or
            UiIconId.Reward => "interaction",
        UiIconId.Info or UiIconId.Warning or UiIconId.Error or UiIconId.Confirm or
            UiIconId.CancelClose => "semantic",
        UiIconId.Keyboard or UiIconId.KeycapBlank or UiIconId.Mouse or
            UiIconId.MousePrimary or UiIconId.MouseSecondary or UiIconId.MouseWheel or
            UiIconId.Gamepad or UiIconId.GamepadFaceBlank or UiIconId.GamepadDpad or
            UiIconId.GamepadStick or UiIconId.GamepadShoulder => "input",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    private static string ToSnakeCase(string value) =>
        Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();

    private static T? LoadOnce<T>(string path) where T : Resource
    {
        if (ResourceExists(path))
        {
            var resource = ResourceLoader.Load<Resource>(path);
            if (resource is T typedResource)
                return typedResource;

            WarnOnce(path, $"[UiArtCatalog] Optional UI art resource has unexpected type: {path}");
            return null;
        }

        WarnOnce(path, $"[UiArtCatalog] Missing optional UI art resource: {path}");
        return null;
    }

    private static void WarnOnce(string path, string message)
    {
        if (MissingPaths.Add(path))
            GD.PushWarning(message);
    }
}
