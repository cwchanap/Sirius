using GdUnit4;
using Godot;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenStackModelTest : Node
{
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
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
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
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            PauseTree = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveCancelPolicy_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            Cancel = UICancelPolicy.Close
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveEntryCancelActions_AreRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            EntryCancelActions = new HashSet<StringName> { "toggle_inventory" }
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveLowerLayerEffects_AreRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            LowerLayers = UILowerLayerPolicy.VisibleInert
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_PassiveInitialFocus_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            InitialFocus = () => null
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }
}
