using GdUnit4;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UiArtCatalogTest : Node
{
    [TestCase]
    public void Catalog_ContainsExactReleaseInventory()
    {
        AssertThat(Enum.GetValues<UiIconId>()).ContainsExactly(
            UiIconId.Health, UiIconId.Mana, UiIconId.Experience, UiIconId.Level,
            UiIconId.Gold, UiIconId.Attack, UiIconId.Defense, UiIconId.Speed,
            UiIconId.Poison, UiIconId.Burn, UiIconId.Stun, UiIconId.Weaken,
            UiIconId.Slow, UiIconId.Blind, UiIconId.Regen, UiIconId.Haste,
            UiIconId.Strength, UiIconId.Fortify,
            UiIconId.General, UiIconId.Equipment, UiIconId.Consumable, UiIconId.Quest,
            UiIconId.Weapon, UiIconId.Shield, UiIconId.Armor, UiIconId.Helmet,
            UiIconId.Shoe, UiIconId.Accessory, UiIconId.ActiveSkill, UiIconId.Locked,
            UiIconId.Equip, UiIconId.Unequip, UiIconId.Use, UiIconId.Assign,
            UiIconId.Buy, UiIconId.Sell,
            UiIconId.Pause, UiIconId.Resume, UiIconId.Settings, UiIconId.Save,
            UiIconId.Load,
            UiIconId.Dialogue, UiIconId.Shop, UiIconId.Heal, UiIconId.Puzzle,
            UiIconId.Reward,
            UiIconId.Info, UiIconId.Warning, UiIconId.Error, UiIconId.Confirm,
            UiIconId.CancelClose,
            UiIconId.Keyboard, UiIconId.KeycapBlank, UiIconId.Mouse,
            UiIconId.MousePrimary, UiIconId.MouseSecondary, UiIconId.MouseWheel,
            UiIconId.Gamepad, UiIconId.GamepadFaceBlank, UiIconId.GamepadDpad,
            UiIconId.GamepadStick, UiIconId.GamepadShoulder);
        AssertThat(Enum.GetValues<UiOrnamentId>()).ContainsExactly(
            UiOrnamentId.CelestialAnchor, UiOrnamentId.OrbitArc,
            UiOrnamentId.TrajectoryLine, UiOrnamentId.CalibrationTicks,
            UiOrnamentId.CalloutFrame, UiOrnamentId.CalloutConnector,
            UiOrnamentId.CatalogueRailEndcap, UiOrnamentId.IgnitionSeal,
            UiOrnamentId.ConstellationCorner, UiOrnamentId.ConstellationDivider,
            UiOrnamentId.PartialSigil, UiOrnamentId.FocusHalo,
            UiOrnamentId.SelectionHalo);
        AssertThat(Enum.GetValues<UiEffectId>()).ContainsExactly(
            UiEffectId.EncounterBurst, UiEffectId.HitImpact,
            UiEffectId.StatusPulse, UiEffectId.RewardLevelUp);
        AssertThat(Enum.GetValues<UiIconSize>().Select(value => (int)value).ToArray())
            .ContainsExactly(16, 24, 32);
    }

    [TestCase]
    public void Catalog_BuildsStableSnakeCasePaths()
    {
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.Health, UiIconSize.Metadata))
            .IsEqual("res://assets/sprites/ui/icons/stats/16/health.png");
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.ActiveSkill, UiIconSize.Default))
            .IsEqual("res://assets/sprites/ui/icons/inventory/24/active_skill.png");
        AssertThat(UiArtCatalog.GetIconPath(UiIconId.GamepadFaceBlank, UiIconSize.Feature))
            .IsEqual("res://assets/sprites/ui/icons/input/32/gamepad_face_blank.png");
        AssertThat(UiArtCatalog.GetOrnamentPath(UiOrnamentId.CalloutFrame))
            .IsEqual("res://assets/sprites/ui/ornaments/callout_frame.png");
        AssertThat(UiArtCatalog.GetEffectPath(UiEffectId.RewardLevelUp))
            .IsEqual("res://assets/sprites/effects/ui/reward_level_up.png");
    }

    [TestCase]
    public void Catalog_MapsEveryRuntimeEnumAndRejectsUnsupportedValues()
    {
        var statusMappings = new[]
        {
            (StatusEffectType.Poison, UiIconId.Poison),
            (StatusEffectType.Burn, UiIconId.Burn),
            (StatusEffectType.Stun, UiIconId.Stun),
            (StatusEffectType.Weaken, UiIconId.Weaken),
            (StatusEffectType.Slow, UiIconId.Slow),
            (StatusEffectType.Blind, UiIconId.Blind),
            (StatusEffectType.Regen, UiIconId.Regen),
            (StatusEffectType.Haste, UiIconId.Haste),
            (StatusEffectType.Strength, UiIconId.Strength),
            (StatusEffectType.Fortify, UiIconId.Fortify)
        };
        AssertThat(Enum.GetValues<StatusEffectType>())
            .ContainsExactly(statusMappings.Select(mapping => mapping.Item1).ToArray());
        foreach (var (statusEffect, icon) in statusMappings)
        {
            AssertThat(UiArtCatalog.ForStatusEffect(statusEffect)).IsEqual(icon);
            AssertThat(UiArtCatalog.TryForStatusEffect(statusEffect, out var mappedIcon)).IsTrue();
            AssertThat(mappedIcon).IsEqual(icon);
        }

        AssertThat(UiArtCatalog.TryForStatusEffect((StatusEffectType)11, out _)).IsFalse();
        AssertThrown(() => UiArtCatalog.ForStatusEffect((StatusEffectType)11))
            .IsInstanceOf<ArgumentOutOfRangeException>();

        var categoryMappings = new[]
        {
            (ItemCategory.General, UiIconId.General),
            (ItemCategory.Equipment, UiIconId.Equipment),
            (ItemCategory.Consumable, UiIconId.Consumable),
            (ItemCategory.Quest, UiIconId.Quest)
        };
        AssertThat(Enum.GetValues<ItemCategory>())
            .ContainsExactly(categoryMappings.Select(mapping => mapping.Item1).ToArray());
        foreach (var (category, icon) in categoryMappings)
            AssertThat(UiArtCatalog.ForItemCategory(category)).IsEqual(icon);
        AssertThrown(() => UiArtCatalog.ForItemCategory((ItemCategory)99))
            .IsInstanceOf<ArgumentOutOfRangeException>();

        var equipmentMappings = new[]
        {
            (EquipmentSlotType.Weapon, UiIconId.Weapon),
            (EquipmentSlotType.Shield, UiIconId.Shield),
            (EquipmentSlotType.Armor, UiIconId.Armor),
            (EquipmentSlotType.Helmet, UiIconId.Helmet),
            (EquipmentSlotType.Shoe, UiIconId.Shoe),
            (EquipmentSlotType.Accessory, UiIconId.Accessory)
        };
        AssertThat(Enum.GetValues<EquipmentSlotType>())
            .ContainsExactly(equipmentMappings.Select(mapping => mapping.Item1).ToArray());
        foreach (var (slot, icon) in equipmentMappings)
            AssertThat(UiArtCatalog.ForEquipmentSlot(slot)).IsEqual(icon);
        AssertThrown(() => UiArtCatalog.ForEquipmentSlot((EquipmentSlotType)99))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    public void Catalog_LoadIcon_DeduplicatesMissingWarningsAndFallsBackToSizeMatchedInfo()
    {
        var missingPaths = GetMissingPaths();
        var originalResourceExists = GetResourceExists();
        missingPaths.Clear();
        SetResourceExists(_ => false);

        try
        {
            AssertThat(UiArtCatalog.LoadIcon(UiIconId.Health, UiIconSize.Feature)).IsNull();
            AssertThat(missingPaths.SetEquals(new[]
            {
                "res://assets/sprites/ui/icons/stats/32/health.png",
                "res://assets/sprites/ui/icons/semantic/32/info.png"
            })).IsTrue();

            AssertThat(UiArtCatalog.LoadIcon(UiIconId.Health, UiIconSize.Feature)).IsNull();
            AssertThat(missingPaths.Count).IsEqual(2);
        }
        finally
        {
            SetResourceExists(originalResourceExists);
            missingPaths.Clear();
        }
    }

    [TestCase]
    public void Catalog_LoadIcon_InvalidIdUsesReadableInfoFallbackWithoutInvalidPath()
    {
        var missingPaths = GetMissingPaths();
        var originalResourceExists = GetResourceExists();
        missingPaths.Clear();
        SetResourceExists(_ => false);

        try
        {
            AssertThat(UiArtCatalog.LoadIcon((UiIconId)999, UiIconSize.Metadata)).IsNull();
            AssertThat(missingPaths.SetEquals(new[]
            {
                "res://assets/sprites/ui/icons/semantic/16/info.png"
            })).IsTrue();
            AssertThrown(() => UiArtCatalog.GetIconPath((UiIconId)999, UiIconSize.Metadata))
                .IsInstanceOf<ArgumentOutOfRangeException>();
        }
        finally
        {
            SetResourceExists(originalResourceExists);
            missingPaths.Clear();
        }
    }

    [TestCase]
    public void Effects_LoadAtDocumentedSizeWithMipmaps()
    {
        foreach (var id in Enum.GetValues<UiEffectId>())
        {
            var texture = UiArtCatalog.LoadEffect(id);
            AssertThat(texture).IsNotNull();
            AssertThat(texture!.GetSize()).IsEqual(new Vector2(256, 256));
            var image = texture.GetImage();
            AssertThat(image).IsNotNull();
            AssertThat(image!.HasMipmaps()).IsTrue();
        }
    }

    private static HashSet<string> GetMissingPaths()
    {
        var field = typeof(UiArtCatalog).GetField(
            "MissingPaths", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (HashSet<string>)field.GetValue(null)!;
    }

    private static Func<string, bool> GetResourceExists()
    {
        var field = typeof(UiArtCatalog).GetField(
            "ResourceExists", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Func<string, bool>)field.GetValue(null)!;
    }

    private static void SetResourceExists(Func<string, bool> resourceExists)
    {
        var field = typeof(UiArtCatalog).GetField(
            "ResourceExists", BindingFlags.NonPublic | BindingFlags.Static)!;
        field.SetValue(null, resourceExists);
    }
}
