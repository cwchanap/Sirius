using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using Godot;

public enum UIScreenLayer { Hud, Screen, Modal, Toast, Transition }
public enum UIInputPriority { Passive, Screen, Modal, Blocking }
public enum UIProcessPolicy { PreserveAndValidate, InheritHost, Pausable, WhenPaused, Always }
public enum UICursorPolicy { Inherit, Visible, Hidden }
public enum UIHudPolicy { Inherit, Visible, Hidden }
// Members must remain ordered from weakest to strongest; UIScreenPolicyResolver.Strongest
// compares their integer values using Math.Max.
public enum UILowerLayerPolicy { VisibleInteractive, VisibleInert, Hidden }
public enum UICancelPolicy { None, Close, Consume, PassThrough }
public enum UIInputInterception { DeferToPolicy, ConsumeHere, ReserveForNativeHandler }
public enum UIInputDispatchResult { NoOwner, Consumed, ReservedForTopEntry }
public enum UIRootCancelResult { Declined, Consumed }
public enum UINodeLifetime { External, Hide, QueueFree }
public enum UIScreenCloseReason { Cancel, ExplicitAction, Programmatic, NodeFreed, ParentClosed, HostTeardown }
public enum UIScreenTeardownPreparationStatus { Deferred, Complete }

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
    MalformedHost,
    HostMutating
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

public sealed record UIScreenFocusRestorationDiagnostics(
    long Generation,
    UIScreenHandle ClosedHandle);

public sealed record UIScreenLowerLayerEffectDiagnostics(
    UIScreenHandle Target,
    UILowerLayerPolicy ReducedEffect,
    IReadOnlyList<UIScreenHandle> Contributors);

public sealed record UIScreenActionOwnershipDiagnostics(
    IReadOnlySet<StringName> CoreActions,
    IReadOnlyDictionary<UIScreenHandle, IReadOnlySet<StringName>> EntryActions,
    UIScreenHandle? TopInputOwner);

public sealed record UIScreenFocusStateDiagnostics(
    UIScreenHandle Handle,
    ulong ViewportInstanceId,
    ulong? FocusOwnerInstanceId,
    ulong? SinkInstanceId,
    bool IsSinkFocused);

public sealed record UIScreenProcessStateDiagnostics(
    UIScreenHandle Handle,
    Node.ProcessModeEnum IncomingMode,
    Node.ProcessModeEnum RegisteredMode,
    Node.ProcessModeEnum? CurrentMode,
    bool IsEmbeddedSubwindow);

public sealed record UIControlEffectLeaseDiagnostics(
    bool Visible,
    bool ProcessInputEnabled);

public sealed record UIWindowEffectLeaseDiagnostics(
    bool Visible,
    bool GuiDisableInput,
    bool Unfocusable);

public sealed record UIScreenStateLeaseDiagnostics(
    bool? IncomingPaused,
    Input.MouseModeEnum? IncomingCursorMode,
    bool? IncomingHudVisible,
    IReadOnlyDictionary<UIScreenHandle, UIControlEffectLeaseDiagnostics> ControlEffects,
    IReadOnlyDictionary<UIScreenHandle, UIWindowEffectLeaseDiagnostics> WindowEffects);

public sealed record UIScreenHostDiagnostics(
    IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries,
    UIScreenEffectiveState EffectiveState,
    IReadOnlyList<UIScreenLowerLayerEffectDiagnostics> LowerLayerEffects,
    UIScreenActionOwnershipDiagnostics ActionOwnership,
    IReadOnlyList<UIScreenFocusStateDiagnostics> FocusStates,
    UIScreenFocusRestorationDiagnostics? RestorationLease,
    IReadOnlyList<UIScreenProcessStateDiagnostics> ProcessStates,
    bool SubwindowEmbeddingEnabled,
    UIScreenStateLeaseDiagnostics StateLeases,
    int PauseOwnershipDriftCount,
    string? LastPauseOwnershipViolation);

internal static class EmptyStringNameSet
{
    public static readonly IReadOnlySet<StringName> Value = Array.Empty<StringName>().ToFrozenSet();
}
