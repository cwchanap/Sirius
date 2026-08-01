using System.Collections.Generic;
using Godot;

public static class UIScreenHostTestSupport
{
    public static UIScreenEntrySpec Spec(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        LowerLayers = UILowerLayerPolicy.VisibleInteractive
    };

    public static UIScreenEntryPolicy Policy(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        ExclusiveGroup = UIScreenExclusiveGroups.None,
        IncompatibleKinds = EmptyStringNameSet.Value,
        LowerLayers = UILowerLayerPolicy.VisibleInteractive,
        EntryCancelActions = EmptyStringNameSet.Value
    };

    public static UIScreenEntrySnapshot Snapshot(
        UIScreenHandle handle,
        UIScreenEntryPolicy policy,
        long sequence) => new(handle, policy, sequence);

    public static IReadOnlyList<UIScreenEntrySnapshot> Snapshots(
        params UIScreenEntrySnapshot[] entries) => entries;
}
