using System.Linq;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class NpcCatalogTest : Godot.Node
{
    // ---- NpcCatalog -------------------------------------------------------

    [TestCase]
    public void NpcCatalog_VillageShopkeeper_Resolves()
    {
        var npc = NpcCatalog.GetById("village_shopkeeper");
        AssertThat(npc).IsNotNull();
        AssertThat(npc.NpcType).IsEqual(NpcType.Shopkeeper);
        AssertThat(npc.ShopId).IsNotNull();
    }

    [TestCase]
    public void NpcCatalog_VillageHealer_Resolves()
    {
        var npc = NpcCatalog.GetById("village_healer");
        AssertThat(npc).IsNotNull();
        AssertThat(npc.NpcType).IsEqual(NpcType.Healer);
        AssertThat(npc.HealCost).IsGreater(0);
    }

    [TestCase]
    public void NpcCatalog_OldFarmer_Resolves()
    {
        var npc = NpcCatalog.GetById("old_farmer");
        AssertThat(npc).IsNotNull();
        AssertThat(npc.NpcType).IsEqual(NpcType.Villager);
    }

    [TestCase]
    public void NpcCatalog_VillageBlacksmith_Resolves()
    {
        var npc = NpcCatalog.GetById("village_blacksmith");
        AssertThat(npc).IsNotNull();
        AssertThat(npc.NpcType).IsEqual(NpcType.Blacksmith);
        AssertThat(npc.ShopId).IsNotNull();
    }

    [TestCase]
    public void NpcCatalog_UnknownId_ReturnsNull()
    {
        AssertThat(NpcCatalog.GetById("does_not_exist")).IsNull();
        AssertThat(NpcCatalog.GetById(null)).IsNull();
        AssertThat(NpcCatalog.GetById("")).IsNull();
    }

    [TestCase]
    public void NpcCatalog_AllNpcs_HaveDialogueTreeId()
    {
        foreach (var npc in NpcCatalog.AllNpcs)
        {
            AssertThat(npc.DialogueTreeId).IsNotNull();
            AssertThat(npc.DialogueTreeId).IsNotEmpty();
        }
    }

    [TestCase]
    public void NpcCatalog_AllAuthoredPortraits_AreLoadableAndOptional()
    {
        var npcs = NpcCatalog.AllNpcs.ToArray();

        foreach (var npc in npcs)
        {
            if (npc.PortraitPath == null)
                continue;

            AssertThat(npc.PortraitPath).IsNotEmpty();
            AssertThat(ResourceLoader.Exists(npc.PortraitPath)).IsTrue();
            AssertThat(ResourceLoader.Load<Texture2D>(npc.PortraitPath)).IsNotNull();
        }

        AssertThat(NpcCatalog.GetById("village_shopkeeper")!.PortraitPath)
            .IsEqual("res://assets/sprites/npcs/shopkeeper/frames/frame1.png");
        AssertThat(NpcCatalog.GetById("village_healer")!.PortraitPath)
            .IsEqual("res://assets/sprites/npcs/healer/frames/frame1.png");
    }

    [TestCase]
    public void NpcData_PortraitPath_IsOptional()
    {
        var npc = new NpcData
        {
            NpcId = "test_optional_portrait",
            DisplayName = "Test NPC",
            NpcType = NpcType.Villager,
            DialogueTreeId = "villager_01",
            SpriteType = "villager"
        };

        AssertThat(npc.PortraitPath).IsNull();
    }

    // ---- ShopCatalog -------------------------------------------------------

    [TestCase]
    public void ShopCatalog_VillageGeneralStore_Resolves()
    {
        var shop = ShopCatalog.GetById("village_general_store");
        AssertThat(shop).IsNotNull();
        AssertThat(shop.Entries.Count).IsGreater(0);
    }

    [TestCase]
    public void ShopCatalog_BlacksmithShop_Resolves()
    {
        var shop = ShopCatalog.GetById("blacksmith_shop");
        AssertThat(shop).IsNotNull();
        AssertThat(shop.Entries.Count).IsGreater(0);
    }

    [TestCase]
    public void ShopCatalog_UnknownId_ReturnsNull()
    {
        AssertThat(ShopCatalog.GetById("does_not_exist")).IsNull();
        AssertThat(ShopCatalog.GetById(null)).IsNull();
    }

    [TestCase]
    public void ShopCatalog_ShopkeeperShopId_MatchesRegisteredShop()
    {
        var npc = NpcCatalog.GetById("village_shopkeeper");
        var shop = ShopCatalog.GetById(npc.ShopId);
        AssertThat(shop).IsNotNull();
    }

    [TestCase]
    public void ShopCatalog_BlacksmithShopId_MatchesRegisteredShop()
    {
        var npc = NpcCatalog.GetById("village_blacksmith");
        var shop = ShopCatalog.GetById(npc.ShopId);
        AssertThat(shop).IsNotNull();
    }

    [TestCase]
    public void ShopCatalog_AllItemIds_ExistInItemCatalog()
    {
        var generalStore = ShopCatalog.GetById("village_general_store");
        foreach (var entry in generalStore.Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            AssertThat(item).IsNotNull();
        }

        var blacksmith = ShopCatalog.GetById("blacksmith_shop");
        foreach (var entry in blacksmith.Entries)
        {
            var item = ItemCatalog.CreateItemById(entry.ItemId);
            AssertThat(item).IsNotNull();
        }
    }
}
