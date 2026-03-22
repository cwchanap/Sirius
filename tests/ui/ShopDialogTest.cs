using GdUnit4;
using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class ShopDialogTest : Node
{
    private ShopDialog _dialog = null!;
    private SceneTree _sceneTree = null!;
    private Variant _originalVerboseOrphans;

    [Before]
    public async Task Setup()
    {
        _originalVerboseOrphans = ProjectSettings.GetSetting("gdunit4/report/verbose_orphans");
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", false);

        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _dialog = new ShopDialog();
        _sceneTree.Root.AddChild(_dialog);
        await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [After]
    public async Task Cleanup()
    {
        if (_dialog != null && GodotObject.IsInstanceValid(_dialog))
            _dialog.QueueFree();

        await ToSignal(Engine.GetMainLoop(), SceneTree.SignalName.ProcessFrame);
        _dialog = null!;
        _sceneTree = null!;
        ProjectSettings.SetSetting("gdunit4/report/verbose_orphans", _originalVerboseOrphans);
    }

    [TestCase]
    public void RefreshSellList_ShowsEmptyLabelImmediately_AfterSellingLastItem()
    {
        var player = CreatePlayer(gold: 0);
        var item = ItemCatalog.CreateItemById("health_potion");
        var shop = ShopCatalog.GetById("village_general_store");

        AssertThat(item).IsNotNull();
        AssertThat(shop).IsNotNull();

        bool addedAll = player.TryAddItem(item!, 1, out int addedQuantity);
        AssertThat(addedAll).IsTrue();
        AssertThat(addedQuantity).IsEqual(1);

        _dialog.OpenShop(shop!, player);
        InvokePrivateMethod(_dialog, "OnSellPressed", item!.Id, SellPrice(item.Value));

        var sellList = GetPrivateField<VBoxContainer>(_dialog, "_sellList");
        AssertThat(ContainsLabelText(sellList, "Nothing to sell.")).IsTrue();
    }

    [TestCase]
    public async Task ShowFeedback_KeepsLatestMessageVisible_UntilLatestTimerExpires()
    {
        InvokePrivateMethod(_dialog, "ShowFeedback", "First message");

        var feedbackLabel = GetPrivateField<Label>(_dialog, "_feedbackLabel");
        AssertThat(feedbackLabel.Text).IsEqual("First message");
        AssertThat(feedbackLabel.Visible).IsTrue();

        await ToSignal(_sceneTree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

        InvokePrivateMethod(_dialog, "ShowFeedback", "Second message");
        AssertThat(feedbackLabel.Text).IsEqual("Second message");
        AssertThat(feedbackLabel.Visible).IsTrue();

        await ToSignal(_sceneTree.CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);

        AssertThat(feedbackLabel.Text).IsEqual("Second message");
        AssertThat(feedbackLabel.Visible).IsTrue();

        await ToSignal(_sceneTree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

        AssertThat(feedbackLabel.Visible).IsFalse();
    }

    private static Character CreatePlayer(int gold) => new Character
    {
        Name = "ShopDialogTester",
        Level = 1,
        MaxHealth = 100,
        CurrentHealth = 100,
        Attack = 10,
        Defense = 5,
        Speed = 10,
        Gold = gold
    };

    private static int SellPrice(int itemValue)
        => Mathf.Max(1, Mathf.FloorToInt(itemValue * 0.5f));

    private static bool ContainsLabelText(VBoxContainer container, string expectedText)
    {
        foreach (Node child in container.GetChildren())
        {
            if (child is Label label && label.Text == expectedText)
                return true;
        }

        return false;
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field?.GetValue(instance) is T value)
            return value;

        throw new InvalidOperationException($"Failed to read private field '{fieldName}'.");
    }

    private static void InvokePrivateMethod(object instance, string methodName, params object[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Failed to locate private method '{methodName}'.");

        method.Invoke(instance, args);
    }
}
