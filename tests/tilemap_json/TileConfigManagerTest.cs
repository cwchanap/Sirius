using GdUnit4;
using Godot;
using Sirius.TilemapJson;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TileConfigManagerTest : Node
{
    [TestCase]
    public void LoadConfig_ReadsSnakeCaseTileMappingConfig()
    {
        var manager = new TileConfigManager();

        var err = manager.LoadConfig("res://config/tile_mapping.json");

        AssertThat(err).IsEqual(Godot.Error.Ok);
        var ground = manager.GetMapping("ground", "starting_area");
        AssertThat(ground).IsNotNull();
        AssertThat(ground!.SourceId).IsEqual(0);
        AssertThat(ground.GetAtlasCoord()).IsEqual(new Godot.Vector2I(0, 0));
        var wall = manager.GetMapping("wall", "generic");
        AssertThat(wall).IsNotNull();
        AssertThat(wall!.SourceId).IsEqual(7);
    }
}
