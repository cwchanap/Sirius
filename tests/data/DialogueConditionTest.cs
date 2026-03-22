using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;
using System;
using Godot;

[TestSuite]
[RequireGodotRuntime]
public partial class DialogueConditionTest : Node
{
    private Character CreatePlayer(int level = 1, int gold = 100)
    {
        return new Character { Level = level, Gold = gold };
    }

    [TestCase]
    public void AlwaysCondition_AlwaysReturnsTrue()
    {
        var player = CreatePlayer();
        var flags = new HashSet<string>();
        AssertThat(AlwaysCondition.Instance.Evaluate(player, flags)).IsTrue();
    }

    [TestCase]
    public void LevelCondition_PassesWhenPlayerMeetsMinLevel()
    {
        var cond = new LevelCondition { MinLevel = 3 };
        AssertThat(cond.Evaluate(CreatePlayer(3), new HashSet<string>())).IsTrue();
        AssertThat(cond.Evaluate(CreatePlayer(5), new HashSet<string>())).IsTrue();
    }

    [TestCase]
    public void LevelCondition_FailsWhenPlayerBelowMinLevel()
    {
        var cond = new LevelCondition { MinLevel = 5 };
        AssertThat(cond.Evaluate(CreatePlayer(1), new HashSet<string>())).IsFalse();
        AssertThat(cond.Evaluate(CreatePlayer(4), new HashSet<string>())).IsFalse();
    }

    [TestCase]
    public void LevelCondition_NullPlayer_ThrowsArgumentNullException()
    {
        var cond = new LevelCondition { MinLevel = 1 };
        AssertThrown(() => cond.Evaluate(null, new HashSet<string>()))
            .IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    public void QuestFlagCondition_PassesWhenFlagPresent()
    {
        var cond = new QuestFlagCondition { Flag = "quest_done", RequirePresent = true };
        var flags = new HashSet<string> { "quest_done" };
        AssertThat(cond.Evaluate(CreatePlayer(), flags)).IsTrue();
    }

    [TestCase]
    public void QuestFlagCondition_FailsWhenFlagAbsent()
    {
        var cond = new QuestFlagCondition { Flag = "quest_done", RequirePresent = true };
        var flags = new HashSet<string>();
        AssertThat(cond.Evaluate(CreatePlayer(), flags)).IsFalse();
    }

    [TestCase]
    public void QuestFlagCondition_RequireAbsent_PassesWhenFlagMissing()
    {
        var cond = new QuestFlagCondition { Flag = "talked_to_elder", RequirePresent = false };
        var flags = new HashSet<string>();
        AssertThat(cond.Evaluate(CreatePlayer(), flags)).IsTrue();
    }

    [TestCase]
    public void QuestFlagCondition_RequireAbsent_FailsWhenFlagPresent()
    {
        var cond = new QuestFlagCondition { Flag = "talked_to_elder", RequirePresent = false };
        var flags = new HashSet<string> { "talked_to_elder" };
        AssertThat(cond.Evaluate(CreatePlayer(), flags)).IsFalse();
    }

    [TestCase]
    public void QuestFlagCondition_NullQuestFlags_TreatedAsEmptySet()
    {
        var presentCond = new QuestFlagCondition { Flag = "quest_done", RequirePresent = true };
        var absentCond = new QuestFlagCondition { Flag = "quest_done", RequirePresent = false };

        AssertThat(presentCond.Evaluate(CreatePlayer(), null)).IsFalse();
        AssertThat(absentCond.Evaluate(CreatePlayer(), null)).IsTrue();
    }

    [TestCase]
    public void AndCondition_PassesWhenAllConditionsPass()
    {
        var cond = new AndCondition
        {
            Conditions = new IDialogueCondition[]
            {
                new LevelCondition { MinLevel = 2 },
                new QuestFlagCondition { Flag = "met_merchant", RequirePresent = true }
            }
        };
        var flags = new HashSet<string> { "met_merchant" };
        AssertThat(cond.Evaluate(CreatePlayer(3), flags)).IsTrue();
    }

    [TestCase]
    public void AndCondition_FailsWhenAnyConditionFails()
    {
        var cond = new AndCondition
        {
            Conditions = new IDialogueCondition[]
            {
                new LevelCondition { MinLevel = 10 },
                AlwaysCondition.Instance
            }
        };
        AssertThat(cond.Evaluate(CreatePlayer(1), new HashSet<string>())).IsFalse();
    }
}
