using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

internal sealed class UIScreenInputDispatcher
{
    public UIInputDispatchResult TryHandleInput(
        InputEvent inputEvent,
        IReadOnlySet<StringName> coreCancelActions,
        Action pruneInvalidEntries,
        Func<IReadOnlyList<UIScreenEntrySnapshot>> inputOrder,
        Func<UIScreenHandle, Func<UIInputContext, UIInputInterception>?> interceptorFor,
        Func<UIScreenHandle, UIScreenCloseResult> close,
        Func<UIScreenEffectiveState> effectiveState,
        Func<UIRootCancelContext, UIRootCancelResult>? rootFallback)
    {
        ArgumentNullException.ThrowIfNull(inputEvent);

        var matchedCoreActions = MatchActions(inputEvent, coreCancelActions);
        pruneInvalidEntries();
        var currentState = effectiveState();

        var entries = inputOrder();
        var topInputOwner = FindTopInputOwner(entries);
        var matchedTopEntryActions = MatchTopEntryActions(
            inputEvent,
            entries,
            topInputOwner);
        if (currentState.IsFocusRestorationPending &&
            (matchedCoreActions.Count != 0 || matchedTopEntryActions.Count != 0))
        {
            return UIInputDispatchResult.Consumed;
        }

        foreach (var candidate in entries)
        {
            var matchedEntryActions = topInputOwner == candidate.Handle
                ? matchedTopEntryActions
                : EmptyStringNameSet.Value;
            if (matchedCoreActions.Count == 0 && matchedEntryActions.Count == 0)
                continue;

            var context = new UIInputContext(
                inputEvent,
                matchedCoreActions,
                matchedEntryActions,
                candidate.Handle,
                currentState);
            var interception = interceptorFor(candidate.Handle)?.Invoke(context) ??
                               UIInputInterception.DeferToPolicy;
            switch (interception)
            {
                case UIInputInterception.ConsumeHere:
                    return UIInputDispatchResult.Consumed;
                case UIInputInterception.ReserveForNativeHandler:
                    return UIInputDispatchResult.ReservedForTopEntry;
            }

            switch (candidate.Policy.Cancel)
            {
                case UICancelPolicy.None:
                    continue;
                case UICancelPolicy.Close:
                    close(candidate.Handle);
                    return UIInputDispatchResult.Consumed;
                case UICancelPolicy.Consume:
                    return UIInputDispatchResult.Consumed;
                case UICancelPolicy.PassThrough:
                    return UIInputDispatchResult.ReservedForTopEntry;
            }
        }

        if (matchedCoreActions.Count != 0 && rootFallback != null)
        {
            var result = rootFallback(new UIRootCancelContext(
                inputEvent,
                matchedCoreActions,
                currentState));
            if (result == UIRootCancelResult.Consumed)
                return UIInputDispatchResult.Consumed;
        }

        return UIInputDispatchResult.NoOwner;
    }

    private static IReadOnlySet<StringName> MatchActions(
        InputEvent inputEvent,
        IReadOnlySet<StringName> actions)
    {
        if (actions.Count == 0)
            return EmptyStringNameSet.Value;

        var matched = new HashSet<StringName>();
        foreach (var action in actions)
        {
            if (inputEvent.IsActionPressed(action))
                matched.Add(action);
        }

        return matched.Count == 0
            ? EmptyStringNameSet.Value
            : matched.ToFrozenSet();
    }

    private static UIScreenHandle? FindTopInputOwner(
        IReadOnlyList<UIScreenEntrySnapshot> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.Policy.InputPriority != UIInputPriority.Passive)
                return entry.Handle;
        }

        return null;
    }

    private static IReadOnlySet<StringName> MatchTopEntryActions(
        InputEvent inputEvent,
        IReadOnlyList<UIScreenEntrySnapshot> entries,
        UIScreenHandle? topInputOwner)
    {
        if (!topInputOwner.HasValue)
            return EmptyStringNameSet.Value;

        foreach (var entry in entries)
        {
            if (entry.Handle == topInputOwner.Value)
                return MatchActions(inputEvent, entry.Policy.EntryCancelActions);
        }

        return EmptyStringNameSet.Value;
    }
}
