using GdUnit4;
using Godot;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class TreasureBoxSpawnTest : Node
{
    [TestCase]
    public void BuildReward_UsesExportedGoldAndItems()
    {
        var box = new TreasureBoxSpawn
        {
            TreasureBoxId = "TreasureBox_Test",
            RewardGold = 25,
            RewardItemIds = ["health_potion", "mana_potion"],
            RewardItemQuantities = [2, 1]
        };

        try
        {
            var reward = box.BuildReward();

            AssertThat(reward.Gold).IsEqual(25);
            AssertThat(reward.Items.Count).IsEqual(2);
            AssertThat(reward.Items[0].ItemId).IsEqual("health_potion");
            AssertThat(reward.Items[0].Quantity).IsEqual(2);
            AssertThat(reward.Items[1].ItemId).IsEqual("mana_potion");
            AssertThat(reward.Items[1].Quantity).IsEqual(1);
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void BuildReward_MissingQuantityDefaultsToOne()
    {
        var box = new TreasureBoxSpawn
        {
            TreasureBoxId = "TreasureBox_Test",
            RewardItemIds = ["health_potion"],
            RewardItemQuantities = []
        };

        try
        {
            var reward = box.BuildReward();

            AssertThat(reward.Items.Count).IsEqual(1);
            AssertThat(reward.Items[0].Quantity).IsEqual(1);
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void BuildReward_NullRewardItemIdsReturnsEmptyItems()
    {
        var box = new TreasureBoxSpawn
        {
            RewardGold = 10,
            RewardItemIds = null,
            RewardItemQuantities = [2]
        };

        try
        {
            var reward = box.BuildReward();

            AssertThat(reward.Gold).IsEqual(10);
            AssertThat(reward.Items.Count).IsEqual(0);
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void BuildReward_NullRewardItemQuantitiesDefaultsToOne()
    {
        var box = new TreasureBoxSpawn
        {
            RewardItemIds = ["health_potion"],
            RewardItemQuantities = null
        };

        try
        {
            var reward = box.BuildReward();

            AssertThat(reward.Items.Count).IsEqual(1);
            AssertThat(reward.Items[0].Quantity).IsEqual(1);
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void ApplyOpenedState_SetsOpenedAndFrame()
    {
        var box = new TreasureBoxSpawn();

        try
        {
            box.ApplyOpenedState(true);

            AssertThat(box.IsOpened).IsTrue();
            AssertThat(box.CurrentFrameIndex).IsEqual(3);
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void BelongsToFloor_ReturnsTrueForAncestor()
    {
        var floor = new Node2D { Name = "Floor" };
        var grid = new GridMap { Name = "GridMap" };
        var box = new TreasureBoxSpawn { TreasureBoxId = "TreasureBox_Test" };
        floor.AddChild(grid);
        grid.AddChild(box);

        AssertThat(box.BelongsToFloor(floor)).IsTrue();

        floor.Free();
    }

    [TestCase]
    public void BelongsToFloor_ReturnsFalseForNullFloor()
    {
        var box = new TreasureBoxSpawn { TreasureBoxId = "TreasureBox_Test" };

        try
        {
            AssertThat(box.BelongsToFloor(null)).IsFalse();
        }
        finally
        {
            box.Free();
        }
    }

    [TestCase]
    public void BelongsToFloor_ReturnsFalseForDifferentFloor()
    {
        var floor = new Node2D { Name = "Floor" };
        var otherFloor = new Node2D { Name = "OtherFloor" };
        var box = new TreasureBoxSpawn { TreasureBoxId = "TreasureBox_Test" };
        otherFloor.AddChild(box);

        AssertThat(box.BelongsToFloor(floor)).IsFalse();

        floor.Free();
        otherFloor.Free();
    }

    [TestCase]
    public async Task OpenAsync_AbortsWhenRemovedFromTreeDuringOpening()
    {
        var sceneTree = (SceneTree)Engine.GetMainLoop();
        var root = new Node2D { Name = "TreasureBoxTestRoot" };
        var box = new TreasureBoxSpawn { TreasureBoxId = "TreasureBox_Test" };

        sceneTree.Root.AddChild(root);
        root.AddChild(box);
        await ToSignal(sceneTree, SceneTree.SignalName.ProcessFrame);

        var opening = box.OpenAsync();
        await ToSignal(sceneTree.CreateTimer(0.02), Timer.SignalName.Timeout);
        root.RemoveChild(box);
        await opening;

        AssertThat(box.IsOpened).IsFalse();
        AssertThat(box.IsOpening).IsFalse();

        box.Free();
        root.Free();
    }
}
