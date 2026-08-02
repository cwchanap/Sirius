using System.Collections.Frozen;
using System.Collections.Generic;

public sealed record UIScreenEntrySnapshot(
    UIScreenHandle Handle,
    UIScreenEntryPolicy Policy,
    long Sequence);

internal sealed record UIScreenStackCloseMutation(
    UIScreenCloseStatus Status,
    IReadOnlyList<UIScreenEntrySnapshot> ClosedEntries);

internal sealed class UIScreenStackModel
{
    private readonly List<UIScreenEntrySnapshot> _entries = new();
    private readonly HashSet<long> _closedTokens = new();
    private long _nextToken;
    private long _nextSequence;

    // Increments on every successful Open/Close so callers can detect that the
    // active entry set changed while they were iterating a resolved snapshot
    // (e.g. UIScreenHost.Recompute re-entrant callbacks). Read-only consumers
    // compare the value before and after a potentially mutating operation.
    public long MutationGeneration { get; private set; }

    public IReadOnlyList<UIScreenEntrySnapshot> Entries => _entries.AsReadOnly();

    public IReadOnlyList<UIScreenEntrySnapshot> InputOrder
    {
        get
        {
            var ordered = new List<UIScreenEntrySnapshot>(_entries);
            SortInputOrder(ordered);
            return ordered.AsReadOnly();
        }
    }

    public UIScreenOpenResult Open(UIScreenEntryPolicy policy)
    {
        var copiedPolicy = CopyPolicy(policy);

        if (ContainsKind(copiedPolicy.Kind))
            return new(UIScreenOpenStatus.DuplicateKind, null);

        if (copiedPolicy.Parent.HasValue && FindActive(copiedPolicy.Parent.Value) is null)
            return new(UIScreenOpenStatus.InvalidParent, null);

        foreach (var entry in _entries)
        {
            if (entry.Policy.IncompatibleKinds.Contains(copiedPolicy.Kind) ||
                copiedPolicy.IncompatibleKinds.Contains(entry.Policy.Kind))
            {
                return new(UIScreenOpenStatus.IncompatibleEntry, null);
            }

            if (HasConflictingExclusiveGroup(entry, copiedPolicy))
                return new(UIScreenOpenStatus.ExclusiveGroupConflict, null);
        }

        var handle = new UIScreenHandle(++_nextToken, copiedPolicy.Kind);
        _entries.Add(new UIScreenEntrySnapshot(handle, copiedPolicy, ++_nextSequence));
        MutationGeneration++;
        return new(UIScreenOpenStatus.Opened, handle);
    }

    private static UIScreenEntryPolicy CopyPolicy(UIScreenEntryPolicy policy) => policy with
    {
        IncompatibleKinds = policy.IncompatibleKinds is { Count: > 0 }
            ? policy.IncompatibleKinds.ToFrozenSet()
            : EmptyStringNameSet.Value,
        EntryCancelActions = policy.EntryCancelActions is { Count: > 0 }
            ? policy.EntryCancelActions.ToFrozenSet()
            : EmptyStringNameSet.Value
    };

    public UIScreenStackCloseMutation Close(UIScreenHandle handle)
    {
        var entry = FindActive(handle);
        if (entry is null)
        {
            return _closedTokens.Contains(handle.Token)
                ? new(UIScreenCloseStatus.AlreadyClosed, EmptySnapshots.Value)
                : new(UIScreenCloseStatus.StaleHandle, EmptySnapshots.Value);
        }

        var closedEntries = new List<UIScreenEntrySnapshot>();
        foreach (var candidate in _entries)
        {
            if (IsDescendantOrSelf(candidate, handle))
                closedEntries.Add(candidate);
        }

        SortCloseOrder(closedEntries);
        foreach (var closed in closedEntries)
        {
            RemoveActive(closed.Handle);
            _closedTokens.Add(closed.Handle.Token);
        }

        MutationGeneration++;
        return new(UIScreenCloseStatus.Closed, closedEntries.AsReadOnly());
    }

    private bool ContainsKind(Godot.StringName kind)
    {
        foreach (var entry in _entries)
        {
            if (entry.Policy.Kind == kind)
                return true;
        }

        return false;
    }

    private UIScreenEntrySnapshot? FindActive(UIScreenHandle handle)
    {
        foreach (var entry in _entries)
        {
            if (entry.Handle == handle)
                return entry;
        }

        return null;
    }

    private static bool HasConflictingExclusiveGroup(
        UIScreenEntrySnapshot active,
        UIScreenEntryPolicy requested)
    {
        if (active.Policy.ExclusiveGroup == UIScreenExclusiveGroups.None ||
            active.Policy.ExclusiveGroup != requested.ExclusiveGroup)
        {
            return false;
        }

        return !IsDirectParentChild(active, requested);
    }

    private static bool IsDirectParentChild(
        UIScreenEntrySnapshot active,
        UIScreenEntryPolicy requested) =>
        requested.Parent.HasValue && requested.Parent.Value == active.Handle;

    private bool IsDescendantOrSelf(UIScreenEntrySnapshot entry, UIScreenHandle ancestor)
    {
        var current = entry;
        while (current.Policy.Parent.HasValue)
        {
            if (current.Policy.Parent.Value == ancestor)
                return true;

            var parent = FindActive(current.Policy.Parent.Value);
            if (parent is null)
                return false;

            current = parent;
        }

        return entry.Handle == ancestor;
    }

    private int GetDepth(UIScreenEntrySnapshot entry)
    {
        var depth = 0;
        var current = entry;
        while (current.Policy.Parent.HasValue)
        {
            var parent = FindActive(current.Policy.Parent.Value);
            if (parent is null)
                break;

            depth++;
            current = parent;
        }

        return depth;
    }

    private void SortInputOrder(List<UIScreenEntrySnapshot> entries)
    {
        for (var index = 1; index < entries.Count; index++)
        {
            var candidate = entries[index];
            var insertionIndex = index - 1;
            while (insertionIndex >= 0 && InputPrecedes(candidate, entries[insertionIndex]))
            {
                entries[insertionIndex + 1] = entries[insertionIndex];
                insertionIndex--;
            }

            entries[insertionIndex + 1] = candidate;
        }
    }

    private bool InputPrecedes(UIScreenEntrySnapshot first, UIScreenEntrySnapshot second)
    {
        var firstPriority = EffectiveInputPriority(first);
        var secondPriority = EffectiveInputPriority(second);
        if (firstPriority != secondPriority)
            return firstPriority > secondPriority;

        if (IsDescendantOrSelf(first, second.Handle))
            return true;

        if (IsDescendantOrSelf(second, first.Handle))
            return false;

        return first.Sequence > second.Sequence;
    }

    private UIInputPriority EffectiveInputPriority(UIScreenEntrySnapshot entry)
    {
        var priority = entry.Policy.InputPriority;
        var current = entry;
        while (current.Policy.Parent.HasValue)
        {
            var parent = FindActive(current.Policy.Parent.Value);
            if (parent is null)
                break;

            if (parent.Policy.InputPriority > priority)
                priority = parent.Policy.InputPriority;
            current = parent;
        }

        return priority;
    }

    private void SortCloseOrder(List<UIScreenEntrySnapshot> entries)
    {
        for (var index = 1; index < entries.Count; index++)
        {
            var candidate = entries[index];
            var insertionIndex = index - 1;
            while (insertionIndex >= 0 && ClosePrecedes(candidate, entries[insertionIndex]))
            {
                entries[insertionIndex + 1] = entries[insertionIndex];
                insertionIndex--;
            }

            entries[insertionIndex + 1] = candidate;
        }
    }

    private bool ClosePrecedes(UIScreenEntrySnapshot first, UIScreenEntrySnapshot second)
    {
        var firstDepth = GetDepth(first);
        var secondDepth = GetDepth(second);
        return firstDepth != secondDepth
            ? firstDepth > secondDepth
            : first.Sequence > second.Sequence;
    }

    private void RemoveActive(UIScreenHandle handle)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Handle != handle)
                continue;

            _entries.RemoveAt(index);
            return;
        }
    }

    private static class EmptySnapshots
    {
        public static readonly IReadOnlyList<UIScreenEntrySnapshot> Value =
            new List<UIScreenEntrySnapshot>().AsReadOnly();
    }
}
