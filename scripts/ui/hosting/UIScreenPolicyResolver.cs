using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed record UIScreenResolvedPolicy(
    bool PauseTree,
    bool BlockGameplayInput,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> LowerLayerEffects);

public static class UIScreenPolicyResolver
{
    public static UIScreenResolvedPolicy Resolve(IReadOnlyList<UIScreenEntrySnapshot> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var pauseTree = false;
        var blockGameplayInput = false;
        var cursor = UICursorPolicy.Inherit;
        var hud = UIHudPolicy.Inherit;
        UIScreenHandle? topInputOwner = null;

        foreach (var entry in entries)
        {
            pauseTree |= entry.Policy.PauseTree;
            blockGameplayInput |= entry.Policy.BlockGameplayInput;

            if (cursor == UICursorPolicy.Inherit && entry.Policy.Cursor != UICursorPolicy.Inherit)
                cursor = entry.Policy.Cursor;

            if (hud == UIHudPolicy.Inherit && entry.Policy.Hud != UIHudPolicy.Inherit)
                hud = entry.Policy.Hud;

            if (!topInputOwner.HasValue && entry.Policy.InputPriority != UIInputPriority.Passive)
                topInputOwner = entry.Handle;
        }

        var lowerLayerEffects = new Dictionary<UIScreenHandle, UILowerLayerPolicy>(entries.Count);
        foreach (var target in entries)
        {
            var effect = UILowerLayerPolicy.VisibleInteractive;
            foreach (var owner in entries)
            {
                if (IsVisuallyAbove(owner, target))
                    effect = Strongest(effect, owner.Policy.LowerLayers);
            }

            lowerLayerEffects.Add(target.Handle, effect);
        }

        return new UIScreenResolvedPolicy(
            pauseTree,
            blockGameplayInput,
            cursor,
            hud,
            topInputOwner,
            new ReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy>(lowerLayerEffects));
    }

    private static bool IsVisuallyAbove(UIScreenEntrySnapshot owner, UIScreenEntrySnapshot target) =>
        owner.Policy.Layer > target.Policy.Layer ||
        (owner.Policy.Layer == target.Policy.Layer && owner.Sequence > target.Sequence);

    private static UILowerLayerPolicy Strongest(
        UILowerLayerPolicy first,
        UILowerLayerPolicy second) =>
        (UILowerLayerPolicy)Math.Max((int)first, (int)second);
}
