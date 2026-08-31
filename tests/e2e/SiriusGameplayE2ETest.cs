using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GdUnit4;
using GodotE2E;
using static GdUnit4.Assertions;

[TestSuite]
public sealed class SiriusGameplayE2ETest
{
    [TestCase]
    public async Task MainMenu_NewGame_LoadsGroundFloor()
    {
        await E2EGame.RunAsync(LaunchOptions("res://scenes/ui/MainMenu.tscn"), async (game, _) =>
        {
            await E2EUi.FindExactlyOneVisibleAsync(game, "text", "SIRIUS");
            var newGame = await E2EUi.FindExactlyOneVisibleAsync(game, "text", "New Game", "Button");
            await game.ClickNodeAsync(newGame);
            await E2EUi.WaitForNodeAsync(game, "/root/Game/FloorGF/GridMap", 15);
            AssertThat(await game.GetSceneAsync()).IsEqual("res://scenes/game/Game.tscn");
        });
    }

    [TestCase]
    public async Task Battle_OrcEncounter_StartsAndEscapes()
    {
        const string gridMapPath = "/root/Game/FloorGF/GridMap";
        const string gameManagerPath = "/root/Game/GameManager";
        const string battleScreenPath = "/root/Game/UI/UIScreenHost/ScreenLayer/BattleScreen";

        await E2EGame.RunAsync(LaunchOptions("res://scenes/game/Game.tscn"), async (game, _) =>
        {
            await E2EUi.WaitForNodeAsync(game, gridMapPath, 15);
            var orcSpawnPath = await E2EUi.FindExactlyOneAsync(
                game, "name", "EnemySpawn_Orc_East");
            var orcPosition = await E2EUi.GetVector2IPropertyAsync(
                game, orcSpawnPath, "GridPosition");

            await MoveRouteAsync(
                game, gridMapPath,
                (1, 0, 6), (0, -1, 1), (1, 0, 7), (0, -1, 10),
                (1, 0, 24), (0, -1, 15),
                (1, 0, 7), (0, -1, 4), (1, 0, 5), (0, 1, 28), (1, 0, 17));

            var playerPosition = await E2EUi.CallVector2IMethodAsync(
                game, gridMapPath, "GetPlayerPosition");
            var targetTilemapPosition = await E2EUi.CallVector2IMethodAsync(
                game, gridMapPath, "InternalGridToTilemapCoords",
                E2EUi.Vector2IArgument(playerPosition.X, playerPosition.Y + 1));
            AssertThat(targetTilemapPosition).IsEqual(orcPosition);

            var moved = await game.CallMethodAsync<bool?>(
                gridMapPath, "TryMovePlayer", E2EUi.Vector2IArgument(0, 1));
            AssertThat(moved ?? false).IsFalse();

            await game.WaitForPropertyAsync(gameManagerPath, "IsInBattle", true, 15);
            await E2EUi.WaitForNodeAsync(game, battleScreenPath, 15);

            var enemyName = await E2EUi.FindExactlyOneVisibleAsync(
                game, "name", "EnemyName", "Label", battleScreenPath);
            var enemyLevel = await E2EUi.FindExactlyOneVisibleAsync(
                game, "name", "EnemyLevel", "Label", battleScreenPath);
            AssertThat(await game.GetPropertyAsync<string>(enemyName, "text")).IsEqual("Orc");
            AssertThat(await game.GetPropertyAsync<string>(enemyLevel, "text")).IsEqual("Lv 2");

            var beginBattle = await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "Begin Battle", "Button", battleScreenPath);
            await game.ClickNodeAsync(beginBattle);

            var automaticCombatPanel = await E2EUi.FindExactlyOneAsync(
                game, "name", "AutomaticCombatPanel", "PanelContainer", battleScreenPath);
            var automaticCombatVisible = await game.CallMethodAsync<bool?>(
                automaticCombatPanel, "is_visible_in_tree");
            AssertThat(automaticCombatVisible ?? false).IsTrue();

            var escapeButton = await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "Escape", "Button", battleScreenPath);
            await game.ClickNodeAsync(escapeButton);
            await game.WaitForPropertyAsync(gameManagerPath, "IsInBattle", false, 15);

            for (var frame = 0;
                 frame < 30 && await game.NodeExistsAsync(battleScreenPath);
                 frame++)
            {
                var result = await game.SendCommandAsync(
                    "wait_process_frames",
                    new Dictionary<string, JsonElement>
                    {
                        ["count"] = JsonSerializer.SerializeToElement(1),
                    });
                AssertThat(result.Success).IsTrue();
            }

            AssertThat(await game.NodeExistsAsync(battleScreenPath)).IsFalse();
        });
    }

    private static async Task MoveRouteAsync(
        E2EGame game, string gridMapPath, params (int dx, int dy, int count)[] runs)
    {
        foreach (var (dx, dy, count) in runs)
        {
            for (var step = 0; step < count; step++)
            {
                var moved = await game.CallMethodAsync<bool?>(
                    gridMapPath, "TryMovePlayer", E2EUi.Vector2IArgument(dx, dy));
                AssertThat(moved ?? false).IsTrue();
            }
        }
    }

    private static E2ELaunchOptions LaunchOptions(string scenePath) => new()
    {
        ScenePath = scenePath,
        ProjectPath = FindProjectRoot(),
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static string FindProjectRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
                    return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate project.godot from the current directory or testhost base directory.");
    }
}
