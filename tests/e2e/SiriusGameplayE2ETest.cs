using System;
using System.IO;
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
