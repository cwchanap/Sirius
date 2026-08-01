using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenPolicyResolverTest : Node
{
    [TestCase]
    public void Resolve_PauseAndBlock_AreOrReduced()
    {
        var pause = UIScreenHostTestSupport.Snapshot(
            new UIScreenHandle(1, UIScreenKinds.Pause),
            UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
            {
                PauseTree = true
            },
            1);
        var settings = UIScreenHostTestSupport.Snapshot(
            new UIScreenHandle(2, UIScreenKinds.Settings),
            UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
            {
                BlockGameplayInput = true
            },
            2);
        var toast = UIScreenHostTestSupport.Snapshot(
            new UIScreenHandle(3, UIScreenKinds.RewardToast),
            UIScreenHostTestSupport.Policy(UIScreenKinds.RewardToast) with
            {
                InputPriority = UIInputPriority.Passive
            },
            2);

        var result = UIScreenPolicyResolver.Resolve(
            UIScreenHostTestSupport.Snapshots(pause, settings, toast));

        AssertThat(result.PauseTree).IsTrue();
        AssertThat(result.BlockGameplayInput).IsTrue();
    }

    [TestCase]
    public void Resolve_CursorAndHud_UseFirstExplicitPolicyInLogicalInputOrder()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            Layer = UIScreenLayer.Transition,
            InputPriority = UIInputPriority.Blocking,
            Cursor = UICursorPolicy.Hidden,
            Hud = UIHudPolicy.Hidden
        }).Handle!.Value;
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause,
            Layer = UIScreenLayer.Hud,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible
        });

        var result = UIScreenPolicyResolver.Resolve(model.InputOrder);

        AssertThat(result.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(result.Hud).IsEqual(UIHudPolicy.Visible);
    }

    [TestCase]
    public void Resolve_TopInputOwner_IsFirstNonPassiveEntryInLogicalInputOrder()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            Layer = UIScreenLayer.Transition,
            InputPriority = UIInputPriority.Blocking
        }).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause,
            Layer = UIScreenLayer.Hud
        }).Handle!.Value;
        model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive
        });

        var result = UIScreenPolicyResolver.Resolve(model.InputOrder);

        AssertThat(result.TopInputOwner).IsEqual(settings);
    }

    [TestCase]
    public void Resolve_LowerLayerEffects_ComposeParentPauseAndChildContributions()
    {
        var model = new UIScreenStackModel();
        var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
        {
            PauseTree = true,
            LowerLayers = UILowerLayerPolicy.VisibleInert
        }).Handle!.Value;
        var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
        {
            Parent = pause,
            Layer = UIScreenLayer.Modal,
            LowerLayers = UILowerLayerPolicy.Hidden
        }).Handle!.Value;
        var inventory = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Inventory) with
        {
            Parent = pause,
            Layer = UIScreenLayer.Modal,
            LowerLayers = UILowerLayerPolicy.VisibleInert
        }).Handle!.Value;

        var result = UIScreenPolicyResolver.Resolve(model.InputOrder);

        AssertThat(result.PauseTree).IsTrue();
        AssertThat(result.LowerLayerEffects[pause]).IsEqual(UILowerLayerPolicy.Hidden);
        AssertThat(result.LowerLayerEffects[settings]).IsEqual(UILowerLayerPolicy.VisibleInert);
        AssertThat(result.LowerLayerEffects[inventory]).IsEqual(UILowerLayerPolicy.VisibleInteractive);
    }

    [TestCase]
    public void Resolve_LowerLayerEffects_AreCopiedAndReadOnly()
    {
        var pause = UIScreenHostTestSupport.Snapshot(
            new UIScreenHandle(1, UIScreenKinds.Pause),
            UIScreenHostTestSupport.Policy(UIScreenKinds.Pause),
            1);
        var entries = new List<UIScreenEntrySnapshot> { pause };

        var result = UIScreenPolicyResolver.Resolve(entries);
        entries.Clear();
        var effects = (IDictionary<UIScreenHandle, UILowerLayerPolicy>)result.LowerLayerEffects;

        AssertThat(result.LowerLayerEffects.Count).IsEqual(1);
        AssertThat(result.LowerLayerEffects[pause.Handle])
            .IsEqual(UILowerLayerPolicy.VisibleInteractive);
        AssertThrown(() => effects.Add(
            new UIScreenHandle(2, UIScreenKinds.Settings),
            UILowerLayerPolicy.Hidden));
    }
}
