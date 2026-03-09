using GdUnit4;
using Godot;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class LootTableCatalogTest : Node
{
    [TestCase]
    public void LootTableCatalog_GoblinTable_IncludesManaPotionDrop()
    {
        var table = LootTableCatalog.CreateGoblinTable();

        bool hasManaPotion = table.Entries.Any(entry => entry.ItemId == "mana_potion");

        AssertThat(hasManaPotion).IsTrue();
    }
}
