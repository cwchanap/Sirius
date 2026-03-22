using System.Collections.Generic;

/// <summary>
/// Static registry of all dialogue trees keyed by tree ID.
/// Add a private factory method and call Register() in the static constructor to add a new tree.
/// </summary>
public static class DialogueCatalog
{
    private static readonly Dictionary<string, DialogueTree> _registry = new();

    static DialogueCatalog()
    {
        Register(CreateShopkeeperGreeting());
        Register(CreateHealerGreeting());
        Register(CreateVillager01());
        Register(CreateBlacksmithGreeting());
    }

    public static IReadOnlyCollection<DialogueTree> AllTrees => _registry.Values;

    /// <summary>Returns the DialogueTree for a given ID, or null if not found.</summary>
    public static DialogueTree? GetById(string? treeId)
    {
        if (string.IsNullOrEmpty(treeId)) return null;
        return _registry.TryGetValue(treeId, out var tree) ? tree : null;
    }

    private static void Register(DialogueTree tree) => _registry[tree.TreeId] = tree;

    // ---- Dialogue tree definitions -----------------------------------------------

    private static DialogueTree CreateShopkeeperGreeting() => new DialogueTree
    {
        TreeId = "shopkeeper_greeting",
        Nodes = new Dictionary<string, DialogueNode>
        {
            ["root"] = new DialogueNode
            {
                NodeId = "root",
                SpeakerName = "Mira the Merchant",
                Text = "Welcome, traveller! I have all manner of useful supplies. What can I do for you?",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "Browse your wares.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.OpenShop
                    },
                    new()
                    {
                        Label = "Any advice for a new adventurer?",
                        NextNodeId = "advice"
                    },
                    new()
                    {
                        Label = "Goodbye.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            },
            ["advice"] = new DialogueNode
            {
                NodeId = "advice",
                SpeakerName = "Mira the Merchant",
                Text = "Stock up on potions before heading deep into the dungeon. The creatures get nastier the further you go — and they won't wait for you to catch your breath.",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "I'll keep that in mind. Browse wares.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.OpenShop
                    },
                    new()
                    {
                        Label = "Thanks. Goodbye.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            }
        }
    };

    private static DialogueTree CreateHealerGreeting() => new DialogueTree
    {
        TreeId = "healer_greeting",
        Nodes = new Dictionary<string, DialogueNode>
        {
            ["root"] = new DialogueNode
            {
                NodeId = "root",
                SpeakerName = "Brother Aldric",
                Text = "The light of the temple watches over all who seek its blessing. I can restore your wounds for a small offering of 50 gold. Do you wish to be healed?",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "Yes, heal me. (50 gold)",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.Heal
                    },
                    new()
                    {
                        Label = "Not today. Farewell.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            }
        }
    };

    private static DialogueTree CreateVillager01() => new DialogueTree
    {
        TreeId = "villager_01",
        Nodes = new Dictionary<string, DialogueNode>
        {
            ["root"] = new DialogueNode
            {
                NodeId = "root",
                SpeakerName = "Old Farmer",
                Text = "Hmm? Oh, you're one of those adventurers. Brave souls, the lot of you. My grandson went into that dungeon last year... never came back.",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "I'm sorry to hear that.",
                        NextNodeId = "condolences"
                    },
                    new()
                    {
                        Label = "I'll be careful. Goodbye.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            },
            ["condolences"] = new DialogueNode
            {
                NodeId = "condolences",
                SpeakerName = "Old Farmer",
                Text = "Aye, well. Life goes on. You be careful down there, you hear? And if you find a silver locket with an oak leaf etching... you know where to bring it.",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "I'll keep an eye out. Goodbye.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn,
                        GrantFlag = "knows_about_locket"
                    }
                }
            }
        }
    };

    private static DialogueTree CreateBlacksmithGreeting() => new DialogueTree
    {
        TreeId = "blacksmith_greeting",
        Nodes = new Dictionary<string, DialogueNode>
        {
            ["root"] = new DialogueNode
            {
                NodeId = "root",
                SpeakerName = "Gareth the Smith",
                Text = "Need something forged, or just browsing? I've got iron gear ready to go. Quality work, fair price.",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "Show me what you've got.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.OpenShop
                    },
                    new()
                    {
                        Label = "What's the strongest gear you make?",
                        NextNodeId = "strongest",
                        Condition = new LevelCondition { MinLevel = 3 }
                    },
                    new()
                    {
                        Label = "Nothing right now. Thanks.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            },
            ["strongest"] = new DialogueNode
            {
                NodeId = "strongest",
                SpeakerName = "Gareth the Smith",
                Text = "Ha! An adventurer with an eye for quality. My iron set is what I've got in stock right now. Once the supply caravans start coming through again, I'll be making steel. But for now, iron will serve you well.",
                Choices = new List<DialogueChoice>
                {
                    new()
                    {
                        Label = "Let me see the iron gear.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.OpenShop
                    },
                    new()
                    {
                        Label = "I'll check back later. Goodbye.",
                        NextNodeId = null,
                        Outcome = DialogueOutcomeType.CloseAndReturn
                    }
                }
            }
        }
    };
}
