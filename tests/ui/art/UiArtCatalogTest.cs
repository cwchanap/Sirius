using GdUnit4;
using Godot;
using System;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UiArtCatalogTest : Node
{
    [TestCase]
    public void Catalog_ContainsExactReleaseInventory()
    {
        AssertThat(Enum.GetValues<UiIconId>().Length).IsEqual(62);
        AssertThat(Enum.GetValues<UiOrnamentId>().Length).IsEqual(13);
        AssertThat(Enum.GetValues<UiEffectId>().Length).IsEqual(4);
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
    public void Catalog_MapsRuntimeEnumsAndRejectsReservedStatusValue()
    {
        AssertThat(UiArtCatalog.ForStatusEffect(StatusEffectType.Poison)).IsEqual(UiIconId.Poison);
        AssertThat(UiArtCatalog.ForItemCategory(ItemCategory.Quest)).IsEqual(UiIconId.Quest);
        AssertThat(UiArtCatalog.ForEquipmentSlot(EquipmentSlotType.Shoe)).IsEqual(UiIconId.Shoe);
        AssertThat(UiArtCatalog.TryForStatusEffect((StatusEffectType)11, out _)).IsFalse();
    }
}
