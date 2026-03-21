using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public partial class DialogueTreeTest : Godot.Node
{
    [TestCase]
    public void DialogueCatalog_AllRegisteredTrees_Resolve()
    {
        AssertThat(DialogueCatalog.AllTrees.Count).IsGreater(0);
        foreach (var tree in DialogueCatalog.AllTrees)
            AssertThat(tree).IsNotNull();
    }

    [TestCase]
    public void DialogueCatalog_UnknownId_ReturnsNull()
    {
        AssertThat(DialogueCatalog.GetById("does_not_exist")).IsNull();
        AssertThat(DialogueCatalog.GetById(null)).IsNull();
        AssertThat(DialogueCatalog.GetById("")).IsNull();
    }

    [TestCase]
    public void DialogueCatalog_AllTrees_HaveRootNode()
    {
        foreach (var tree in DialogueCatalog.AllTrees)
            AssertThat(tree.Root).IsNotNull();
    }

    [TestCase]
    public void DialogueCatalog_AllChoiceNextNodeIds_ResolveWithinTree()
    {
        foreach (var tree in DialogueCatalog.AllTrees)
        {
            foreach (var node in tree.Nodes.Values)
            {
                foreach (var choice in node.Choices)
                {
                    if (choice.NextNodeId != null)
                    {
                        var next = tree.GetNode(choice.NextNodeId);
                        AssertThat(next).IsNotNull();
                    }
                }
            }
        }
    }

    [TestCase]
    public void DialogueCatalog_ShopkeeperGreeting_HasOpenShopOutcome()
    {
        var tree = DialogueCatalog.GetById("shopkeeper_greeting");
        AssertThat(tree).IsNotNull();
        bool hasOpenShop = false;
        foreach (var node in tree!.Nodes.Values)
            foreach (var choice in node.Choices)
                if (choice.Outcome == DialogueOutcomeType.OpenShop)
                    hasOpenShop = true;
        AssertThat(hasOpenShop).IsTrue();
    }

    [TestCase]
    public void DialogueCatalog_HealerGreeting_HasHealOutcome()
    {
        var tree = DialogueCatalog.GetById("healer_greeting");
        AssertThat(tree).IsNotNull();
        bool hasHeal = false;
        foreach (var node in tree!.Nodes.Values)
            foreach (var choice in node.Choices)
                if (choice.Outcome == DialogueOutcomeType.Heal)
                    hasHeal = true;
        AssertThat(hasHeal).IsTrue();
    }

    [TestCase]
    public void DialogueCatalog_Villager01_GrantsFlagOnCondolencesPath()
    {
        var tree = DialogueCatalog.GetById("villager_01");
        AssertThat(tree).IsNotNull();
        var condolences = tree!.GetNode("condolences");
        AssertThat(condolences).IsNotNull();

        bool grantsFlag = false;
        foreach (var choice in condolences!.Choices)
            if (choice.GrantFlag == "knows_about_locket")
                grantsFlag = true;
        AssertThat(grantsFlag).IsTrue();
    }

    [TestCase]
    public void DialogueCatalog_BlacksmithGreeting_HasLevelConditionedChoice()
    {
        var tree = DialogueCatalog.GetById("blacksmith_greeting");
        AssertThat(tree).IsNotNull();
        var root = tree!.Root;
        AssertThat(root).IsNotNull();
        bool hasConditionalChoice = false;
        foreach (var choice in root!.Choices)
            if (choice.Condition is LevelCondition lc && lc.MinLevel >= 3)
                hasConditionalChoice = true;
        AssertThat(hasConditionalChoice).IsTrue();
    }
}
