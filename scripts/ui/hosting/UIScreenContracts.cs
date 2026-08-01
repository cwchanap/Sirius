using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using Godot;

public enum UIScreenLayer { Hud, Screen, Modal, Toast, Transition }
public enum UIInputPriority { Passive, Screen, Modal, Blocking }
public enum UIProcessPolicy { PreserveAndValidate, InheritHost, Pausable, WhenPaused, Always }
public enum UICursorPolicy { Inherit, Visible, Hidden }
public enum UIHudPolicy { Inherit, Visible, Hidden }
public enum UILowerLayerPolicy { VisibleInteractive, VisibleInert, Hidden }
public enum UICancelPolicy { None, Close, Consume, PassThrough }
public enum UIInputInterception { DeferToPolicy, ConsumeHere, ReserveForNativeHandler }
public enum UIInputDispatchResult { NoOwner, Consumed, ReservedForTopEntry }
public enum UIRootCancelResult { Declined, Consumed }
public enum UINodeLifetime { External, Hide, QueueFree }
public enum UIScreenCloseReason { Cancel, ExplicitAction, Programmatic, NodeFreed, ParentClosed, HostTeardown }

public enum UIScreenOpenStatus
{
    Opened,
    DuplicateKind,
    IncompatibleEntry,
    ExclusiveGroupConflict,
    InvalidNode,
    InvalidParent,
    NodeAlreadyRegistered,
    NodeOwnedByAnotherHost,
    InvalidControlParentage,
    MissingRequiredAdapter,
    UnsupportedSubwindowMode,
    InvalidProcessPolicy,
    InvalidSpecification,
    MalformedHost
}

public enum UIScreenCloseStatus { Closed, AlreadyClosed, StaleHandle, HostTearingDown }

public readonly record struct UIScreenOpenResult(
    UIScreenOpenStatus Status,
    UIScreenHandle? Handle);

public readonly record struct UIScreenCloseResult(UIScreenCloseStatus Status);

public sealed record UIScreenEffectiveState(
    bool IsTreePauseOwned,
    bool IsPresentationGameplayBlocked,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    bool IsFocusRestorationPending);

public readonly record struct UIInputContext(
    InputEvent Event,
    IReadOnlySet<StringName> MatchedCoreActions,
    IReadOnlySet<StringName> MatchedEntryActions,
    UIScreenHandle Candidate,
    UIScreenEffectiveState EffectiveState);

public readonly record struct UIRootCancelContext(
    InputEvent Event,
    IReadOnlySet<StringName> MatchedCoreActions,
    UIScreenEffectiveState EffectiveState);

public sealed record UIScreenHostOptions
{
    public Control? HudRoot { get; init; }
    public IReadOnlySet<StringName> CoreCancelActions { get; init; } = EmptyStringNameSet.Value;
    public Func<UIRootCancelContext, UIRootCancelResult>? RootCancelFallback { get; init; }
    public Action<bool>? GameplayInputBlockChanged { get; init; }
}

public sealed record UIScreenHostDiagnostics(
    UIScreenEffectiveState EffectiveState,
    int PauseOwnershipDriftCount);

internal static class EmptyStringNameSet
{
    public static readonly IReadOnlySet<StringName> Value = Array.Empty<StringName>().ToFrozenSet();
}
