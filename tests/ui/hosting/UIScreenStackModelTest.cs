using GdUnit4;
using Godot;
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
}
