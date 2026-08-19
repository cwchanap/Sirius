using GdUnit4;
using Godot;
using System.Collections.Generic;
using System.Linq;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenStackModelTest : Node
{
    private static readonly StringName BlockingPromptA = "blocking_prompt_a";
    private static readonly StringName BlockingPromptB = "blocking_prompt_b";
    private static readonly StringName ModalFixture = "modal_fixture";

    [TestCase]
    public void Open_DuplicateKind_IsRejectedWithoutMutation()
    {
        var model = new UIScreenStackModel();
        var first = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));
        var second = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));

        AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(second.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
        AssertThat(model.Entries.Count).IsEqual(1);
    }

    [TestCase]
    public void Open_IncompatibilityIsSymmetric()
    {
        var activeDeclarationModel = new UIScreenStackModel();
        activeDeclarationModel.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            IncompatibleKinds = new HashSet<StringName> { UIScreenKinds.Settings }
        });

        var requestedDeclarationModel = new UIScreenStackModel();
        requestedDeclarationModel.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));

        var activeDeclaration = activeDeclarationModel.Open(
            UIScreenHostTestSupport.Policy(UIScreenKinds.Settings));
        var requestedDeclaration = requestedDeclarationModel.Open(
            UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
            {
                IncompatibleKinds = new HashSet<StringName> { UIScreenKinds.Pause }
            });

        AssertThat(activeDeclaration.Status).IsEqual(UIScreenOpenStatus.IncompatibleEntry);
        AssertThat(requestedDeclaration.Status).IsEqual(UIScreenOpenStatus.IncompatibleEntry);
        AssertThat(activeDeclarationModel.Entries.Count).IsEqual(1);
        AssertThat(requestedDeclarationModel.Entries.Count).IsEqual(1);
    }

    [TestCase]
    public void Open_EqualNonEmptyExclusiveGroupsConflict()
    {
        var model = new UIScreenStackModel();
        var group = new StringName("menu");
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            ExclusiveGroup = group
        });

        var result = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            ExclusiveGroup = group
        });

        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
        AssertThat(model.Entries.Count).IsEqual(1);
    }

    [TestCase]
    public void Open_EqualEmptyExclusiveGroupsDoNotConflict()
    {
        var model = new UIScreenStackModel();
        var first = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));
        var second = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings));

        AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(second.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(model.Entries.Count).IsEqual(2);
    }

    [TestCase]
    public void Open_ActiveParentAllowsMatchingExclusiveGroup()
    {
        var model = new UIScreenStackModel();
        var group = new StringName("wizard");
        var parent = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            ExclusiveGroup = group
        }).Handle!.Value;

        var child = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = parent,
            ExclusiveGroup = group
        });

        AssertThat(child.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(model.Entries.Count).IsEqual(2);
    }

    [TestCase]
    public void Open_InactiveParentIsRejectedWithoutMutation()
    {
        var model = new UIScreenStackModel();
        var parent = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        model.Close(parent);

        var result = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = parent
        });

        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.InvalidParent);
        AssertThat(model.Entries.Count).IsEqual(0);
    }

    [TestCase]
    public void Open_DifferentFixtureKindsShareBlockingPromptGroup()
    {
        var model = new UIScreenStackModel();
        var first = model.Open(UIScreenHostTestSupport.Policy(BlockingPromptA) with
        {
            InputPriority = UIInputPriority.Blocking,
            ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt
        });

        var second = model.Open(UIScreenHostTestSupport.Policy(BlockingPromptB) with
        {
            InputPriority = UIInputPriority.Blocking,
            ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt
        });

        AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(second.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
        AssertThat(model.Entries.Count).IsEqual(1);
    }

    [TestCase]
    public void Open_AssignsIncreasingTokensAndSequences()
    {
        var model = new UIScreenStackModel();
        var first = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        var second = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings)).Handle!.Value;

        AssertThat(second.Token).IsGreater(first.Token);
        AssertThat(model.Entries[1].Sequence).IsGreater(model.Entries[0].Sequence);
    }

    [TestCase]
    public void InputOrder_ChildPrecedesParent()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            InputPriority = UIInputPriority.Blocking
        }).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause
        }).Handle!.Value;

        AssertThat(model.InputOrder[0].Handle).IsEqual(settings);
        AssertThat(model.InputOrder[1].Handle).IsEqual(pause);
    }

    [TestCase]
    public void InputOrder_UnrelatedBlockingRootPrecedesScreenChild()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause
        }).Handle!.Value;
        var transition = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Transition) with
        {
            InputPriority = UIInputPriority.Blocking
        }).Handle!.Value;

        AssertThat(model.InputOrder.Select(entry => entry.Handle).ToArray())
            .ContainsExactly(transition, settings, pause);
    }

    [TestCase]
    public void InputOrder_UsesPriorityBeforePresentationSequence()
    {
        var model = new UIScreenStackModel();
        model.Open(UIScreenHostTestSupport.Policy(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive
        });
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            InputPriority = UIInputPriority.Modal
        });
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Transition) with
        {
            InputPriority = UIInputPriority.Blocking
        });

        AssertThat(model.InputOrder.Select(entry => entry.Policy.InputPriority).ToArray())
            .ContainsExactly(
                UIInputPriority.Blocking,
                UIInputPriority.Modal,
                UIInputPriority.Screen,
                UIInputPriority.Passive);
    }

    [TestCase]
    public void InputOrder_NewerSiblingPrecedesOlderSibling()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings)).Handle!.Value;

        AssertThat(model.InputOrder.Select(entry => entry.Handle).ToArray())
            .ContainsExactly(settings, pause);
    }

    [TestCase]
    public void Close_Parent_ClosesDescendantsTopmostFirst()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;
        var modal = model.Open(UIScreenHostTestSupport.Policy(ModalFixture) with { Parent = settings }).Handle!.Value;

        var result = model.Close(pause);

        AssertThat(result.Status).IsEqual(UIScreenCloseStatus.Closed);
        AssertThat(result.ClosedEntries.Select(entry => entry.Handle).ToArray())
            .ContainsExactly(modal, settings, pause);
        AssertThat(model.Entries.Count).IsEqual(0);
    }

    [TestCase]
    public void Close_Parent_ClosesNewerSiblingBeforeOlderSibling()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;
        var inventory = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Inventory) with { Parent = pause }).Handle!.Value;

        var result = model.Close(pause);

        AssertThat(result.ClosedEntries.Select(entry => entry.Handle).ToArray())
            .ContainsExactly(inventory, settings, pause);
    }

    [TestCase]
    public void Close_KnownClosedTokenIsAlreadyClosedAndUnknownTokenIsStale()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
        model.Close(pause);

        var alreadyClosed = model.Close(pause);
        var stale = model.Close(new UIScreenHandle(999, UIScreenKinds.Settings));

        AssertThat(alreadyClosed.Status).IsEqual(UIScreenCloseStatus.AlreadyClosed);
        AssertThat(stale.Status).IsEqual(UIScreenCloseStatus.StaleHandle);
    }

    [TestCase]
    public void Normalize_DefaultGroup_BecomesEmptyGroup()
    {
        var normalized = UIScreenHostTestSupport.Spec(UIScreenKinds.Pause)
            with { ExclusiveGroup = default };

        var result = normalized.Normalize();

        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(result.Policy!.ExclusiveGroup).IsEqual(UIScreenExclusiveGroups.None);
    }

    [TestCase]
    public void Normalize_PassiveBlockingPolicy_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            BlockGameplayInput = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_EmptyKind_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(default);

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_EmptyTextKind_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(new StringName(""));

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_NullAndDefaultCollections_BecomeEmptySets()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
        {
            IncompatibleKinds = null,
            EntryCancelActions = default
        };

        var policy = spec.Normalize().Policy!;

        AssertThat(policy.IncompatibleKinds.Count).IsEqual(0);
        AssertThat(policy.EntryCancelActions.Count).IsEqual(0);
    }

    [TestCase]
    public void Normalize_CopiesCollectionsBeforeProjectingPolicy()
    {
        var incompatibleKinds = new HashSet<StringName> { UIScreenKinds.Settings };
        var entryCancelActions = new HashSet<StringName> { "toggle_inventory" };
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
        {
            IncompatibleKinds = incompatibleKinds,
            EntryCancelActions = entryCancelActions
        };

        var policy = spec.Normalize().Policy!;
        incompatibleKinds.Add(UIScreenKinds.Inventory);
        entryCancelActions.Add("ui_cancel");

        AssertThat(policy.IncompatibleKinds.Contains(UIScreenKinds.Settings)).IsTrue();
        AssertThat(policy.EntryCancelActions.Contains("toggle_inventory")).IsTrue();
        AssertThat(policy.IncompatibleKinds.Contains(UIScreenKinds.Inventory)).IsFalse();
        AssertThat(policy.EntryCancelActions.Contains("ui_cancel")).IsFalse();
    }

    [TestCase]
    public void Normalize_PassivePauseTreePolicy_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            PauseTree = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveCancelPolicy_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            Cancel = UICancelPolicy.Close
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveEntryCancelActions_AreRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            EntryCancelActions = new HashSet<StringName> { "toggle_inventory" }
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveLowerLayerEffects_AreRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            LowerLayers = UILowerLayerPolicy.VisibleInert
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveInitialFocus_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            InitialFocus = () => null
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveRestoreFocus_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenHostTestSupport.ToastFixture) with
        {
            InputPriority = UIInputPriority.Passive,
            RestoreFocus = () => null
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Open_CopiesPolicyCollectionsBeforeStoring()
    {
        var model = new UIScreenStackModel();
        var incompatibleKinds = new HashSet<StringName> { UIScreenKinds.Settings };
        var entryCancelActions = new HashSet<StringName> { "toggle_inventory" };
        var policy = UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            IncompatibleKinds = incompatibleKinds,
            EntryCancelActions = entryCancelActions
        };

        model.Open(policy);
        incompatibleKinds.Add(UIScreenKinds.Inventory);
        entryCancelActions.Add("ui_cancel");

        var stored = model.Entries[0].Policy;
        AssertThat(stored.IncompatibleKinds.Contains(UIScreenKinds.Settings)).IsTrue();
        AssertThat(stored.EntryCancelActions.Contains("toggle_inventory")).IsTrue();
        AssertThat(stored.IncompatibleKinds.Contains(UIScreenKinds.Inventory)).IsFalse();
        AssertThat(stored.EntryCancelActions.Contains("ui_cancel")).IsFalse();
    }

    [TestCase]
    public void InputOrder_ChildInheritsBlockingAncestorBeforeLaterModalRoot()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            InputPriority = UIInputPriority.Blocking
        }).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause
        }).Handle!.Value;
        var modal = model.Open(UIScreenHostTestSupport.Policy(ModalFixture) with
        {
            InputPriority = UIInputPriority.Modal
        }).Handle!.Value;

        AssertThat(model.InputOrder.Select(entry => entry.Handle).ToArray())
            .ContainsExactly(settings, pause, modal);
    }
}
