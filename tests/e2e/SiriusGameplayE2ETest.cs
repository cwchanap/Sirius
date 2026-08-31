using System;
using System.Collections.Generic;
using System.Globalization;
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

    [TestCase]
    public async Task Shop_HealthPotionPurchase_UpdatesGoldAndCloses()
    {
        const string gridMapPath = "/root/Game/FloorGF/GridMap";
        const string gameManagerPath = "/root/Game/GameManager";
        const string dialogueScreenPath = "/root/Game/UI/UIScreenHost/ModalLayer/DialogueScreen";
        const string shopScreenPath = "/root/Game/UI/UIScreenHost/ModalLayer/ShopScreen";

        await E2EGame.RunAsync(LaunchOptions("res://scenes/game/Game.tscn"), async (game, _) =>
        {
            await E2EUi.WaitForNodeAsync(game, gridMapPath, 15);
            var shopkeeperSpawnPath = await E2EUi.FindExactlyOneAsync(
                game, "name", "NpcSpawn_Shopkeeper");
            var shopkeeperPosition = await E2EUi.GetVector2IPropertyAsync(
                game, shopkeeperSpawnPath, "GridPosition");

            await MoveRouteAsync(
                game, gridMapPath,
                (1, 0, 4), (0, -1, 3));

            var playerPosition = await E2EUi.CallVector2IMethodAsync(
                game, gridMapPath, "GetPlayerPosition");
            var targetTilemapPosition = await E2EUi.CallVector2IMethodAsync(
                game, gridMapPath, "InternalGridToTilemapCoords",
                E2EUi.Vector2IArgument(playerPosition.X, playerPosition.Y - 1));
            AssertThat(targetTilemapPosition).IsEqual(shopkeeperPosition);

            var moved = await game.CallMethodAsync<bool?>(
                gridMapPath, "TryMovePlayer", E2EUi.Vector2IArgument(0, -1));
            AssertThat(moved ?? false).IsFalse();

            await game.WaitForPropertyAsync(gameManagerPath, "IsInNpcInteraction", true, 15);
            await E2EUi.WaitForNodeAsync(game, dialogueScreenPath, 15);

            var browseButton = await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "Browse your wares.", "Button", dialogueScreenPath);
            await game.CallMethodAsync<object>(browseButton, "grab_focus");
            await game.PressActionAsync("ui_accept");
            await E2EUi.WaitForNodeAsync(game, shopScreenPath, 15);

            await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "Mira's General Store", "Label", shopScreenPath);
            var goldLabelPath = await E2EUi.FindExactlyOneAsync(
                game, "name", "GoldLabel", "Label", shopScreenPath);
            var beforeGoldText = await game.GetPropertyAsync<string>(goldLabelPath, "text");
            var beforeGold = ParseGoldLabel(beforeGoldText
                ?? throw new E2EException("GoldLabel text was null before purchase"));

            string? healthPotionButtonPath = null;
            foreach (var buttonPath in await E2EUi.FindNodesAsync(
                         game, "text", "Buy", "Button", shopScreenPath))
            {
                var visible = await game.CallMethodAsync<bool?>(
                    buttonPath, "is_visible_in_tree");
                if (visible != true)
                    continue;

                var itemId = await game.CallMethodAsync<string>(
                    buttonPath, "get_meta", "ItemId");
                if (itemId == "health_potion")
                {
                    if (healthPotionButtonPath != null)
                        throw new E2EException("Found multiple visible health potion Buy buttons");
                    healthPotionButtonPath = buttonPath;
                }
            }

            if (healthPotionButtonPath == null)
                throw new E2EException("Could not find a visible health potion Buy button");

            var rowPath = healthPotionButtonPath[..healthPotionButtonPath.LastIndexOf('/')];
            var priceLabelPath = await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "*g", "Label", rowPath);
            var priceText = await game.GetPropertyAsync<string>(priceLabelPath, "text");
            var renderedPrice = ParseGoldPrice(priceText
                ?? throw new E2EException("Health potion price text was null"));

            await game.ClickNodeAsync(healthPotionButtonPath);
            var expectedGold = beforeGold - renderedPrice;
            await game.WaitForPropertyAsync(
                goldLabelPath, "text", $"Your Gold: {expectedGold}", 10);
            var afterGoldText = await game.GetPropertyAsync<string>(goldLabelPath, "text");
            var afterGold = ParseGoldLabel(afterGoldText
                ?? throw new E2EException("GoldLabel text was null after purchase"));
            AssertThat(afterGold).IsEqual(expectedGold);

            var closeButton = await E2EUi.FindExactlyOneVisibleAsync(
                game, "text", "Close", "Button", shopScreenPath);
            await game.ClickNodeAsync(closeButton);
            await game.WaitForPropertyAsync(gameManagerPath, "IsInNpcInteraction", false, 15);
        });
    }

    private static int ParseGoldLabel(string text)
    {
        const string prefix = "Your Gold: ";
        if (!text.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(text.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var gold) ||
            gold < 0 || text != $"{prefix}{gold}")
        {
            throw new E2EException($"Expected exact gold label 'Your Gold: N', got '{text}'");
        }

        return gold;
    }

    private static int ParseGoldPrice(string text)
    {
        const string suffix = "g";
        if (!text.EndsWith(suffix, StringComparison.Ordinal) ||
            !int.TryParse(text.AsSpan(0, text.Length - suffix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var price) ||
            price <= 0 || text != $"{price}{suffix}")
        {
            throw new E2EException($"Expected exact positive gold price 'Ng', got '{text}'");
        }

        return price;
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
