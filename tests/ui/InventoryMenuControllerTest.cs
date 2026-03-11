using GdUnit4;
using Godot;
using System;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class InventoryMenuControllerTest : Node
{
    private GameManager _gameManager = null!;
    private InventoryMenuController _inventoryMenu = null!;
    private Variant _originalVerboseOrphans;

    private static void ResetSingleton()
    {
        var property = typeof(GameManager).GetProperty("Instance",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var setter = property?.GetSetMethod(true);
        if (setter != null)
        {
            setter.Invoke(null, new object[] { null! });
            return;
        }

        var field = typeof(GameManager).GetField("<Instance>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field != null)
        {
            field.SetValue(null, null);
            return;
        }

        throw new InvalidOperationException("Failed to reset GameManager singleton for InventoryMenuController tests.");
    }

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);

        ResetSingleton();

        var sceneTree = (SceneTree)Engine.GetMainLoop();

        _gameManager = new GameManager
        {
            AutoSaveEnabled = false
        };
        sceneTree.Root.AddChild(_gameManager);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        var inventoryScene = GD.Load<PackedScene>("res://scenes/ui/InventoryMenu.tscn");
        AssertThat(inventoryScene).IsNotNull();

        _inventoryMenu = inventoryScene!.Instantiate<InventoryMenuController>();
        sceneTree.Root.AddChild(_inventoryMenu);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_inventoryMenu != null && IsInstanceValid(_inventoryMenu))
        {
            if (_inventoryMenu.Visible)
            {
                _inventoryMenu.CloseMenu();
            }

            _inventoryMenu.QueueFree();
        }

        if (_gameManager != null && IsInstanceValid(_gameManager))
        {
            _gameManager.QueueFree();
        }

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _inventoryMenu = null!;
        _gameManager = null!;

        ResetSingleton();
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public async Task InventoryMenu_ActiveSkillSelector_EquipsLaterLearnedActiveSkill()
    {
        var player = _gameManager.Player;
        SkillCatalog.GrantSkillsUpToLevel(player, 3);

        _inventoryMenu.OpenMenu();
        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);

        var selector = _inventoryMenu.GetNode<OptionButton>("%ActiveSkillSelector");
        AssertThat(selector.Disabled).IsFalse();
        AssertThat(selector.ItemCount).IsEqual(3);
        AssertThat(selector.GetItemText(0)).IsEqual("— None —");
        AssertThat(selector.GetItemText(1)).IsEqual("Power Strike");
        AssertThat(selector.GetItemText(2)).IsEqual("Fire Bolt");
        AssertThat(player.ActiveSkillId).IsEqual("power_strike");

        selector.Select(2);
        selector.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);

        AssertThat(player.ActiveSkillId).IsEqual("fire_bolt");
        AssertThat(selector.TooltipText).Contains("Currently equipped");
    }
}
