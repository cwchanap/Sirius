# UIScreenHost integration contract

This document is the public HPA-378 contract for Sirius presentation
coordination. `UIScreenHost` is a scene-local coordinator: it owns presentation
stacking, Cancel routing, pause/cursor/HUD leases, lower-layer effects, focus,
and teardown. Game-domain decisions and production-flow migration remain
outside the host and belong to HPA-379.

The implementation is shared by gameplay and Main Menu scenes. It is not an
autoload, does not create native detached windows, and does not replace battle,
save, settings, inventory, NPC, or scene-navigation domain logic.

## Scene and process contract

Instantiate `res://scenes/ui/UIScreenHost.tscn`, call `Configure` before adding
it to the scene tree, and keep the node under the scene's UI/CanvasLayer
hierarchy. The shipped scene has these required direct paths:

| Path | Purpose | Declared process mode |
|---|---|---|
| `UIScreenHost` | stack, `_Input`, leases, diagnostics | `Always` |
| `UIScreenHost/HUDLayer` | gameplay HUD composition target | `Pausable` |
| `UIScreenHost/ScreenLayer` | full-screen presentations | `Always` |
| `UIScreenHost/ModalLayer` | dialogs and blocking prompts | `Always` |
| `UIScreenHost/ToastLayer` | passive notifications | `Always` |
| `UIScreenHost/TransitionLayer` | transition presentations | `Always` |
| `UIScreenHost/InputShield` | inert-Control pointer barrier | `Inherit` when idle; moved beside the target and set to `Always` while active |
| `UIScreenHost/FocusSink` | transparent root-viewport focus fallback | `Inherit` |

The layer for `UIScreenLayer.Hud`, `Screen`, `Modal`, `Toast`, or `Transition`
selects the attachment/visual layer for Controls. Layer does not imply input,
pause, blocking, or Cancel behaviour; every entry declares those separately.

`HUDLayer` is deliberately Pausable even though the host and presentation
layers are Always. HPA-379 must compose the real HUD beneath this layer and
audit opt-in exceptions instead of making the whole HUD Always.

## Constructing and configuring a host

```csharp
using System.Collections.Generic;
using Godot;

public static UIScreenHost CreateScreenHost(Node sceneUiRoot, Control hudRoot)
{
    var packed = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn");
    var host = packed.Instantiate<UIScreenHost>();
    host.Configure(new UIScreenHostOptions
    {
        HudRoot = hudRoot,
        CoreCancelActions = new HashSet<StringName>
        {
            "pause_menu",
            "ui_cancel"
        },
        GameplayInputBlockChanged = blocked =>
            GD.Print($"Presentation gameplay block: {blocked}")
    });
    sceneUiRoot.AddChild(host);
    return host;
}
```

`Configure` throws if it is called after `_Ready`, after teardown begins, or
while entries exist. `HudRoot` may be null, but an entry with an explicit HUD
policy is then rejected as `InvalidSpecification`.

`CoreCancelActions` is copied to immutable host state. `RootCancelFallback`
runs from the host's Always `_Input` path when a matched core action has no
entry owner; a gameplay root can therefore open Pause or consume an atomic
domain interaction while the tree is paused. `GameplayInputBlockChanged`
publishes only the presentation component. The Game root owns the final OR
composition with domain flags.

## Public host surface

The integration surface is:

```csharp
public void Configure(UIScreenHostOptions options);
public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec);
public UIScreenCloseResult TryClose(
    UIScreenHandle handle,
    UIScreenCloseReason reason);
public UIInputDispatchResult TryHandleInput(InputEvent inputEvent);
public UIScreenTeardownPreparationStatus PrepareForTeardown();

public IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries { get; }
public UIScreenEffectiveState CurrentState { get; }
public UIScreenHostDiagnostics Diagnostics { get; }
public bool IsActive(UIScreenHandle handle);
public bool IsKindActive(StringName kind);
public event Action<UIScreenEffectiveState>? EffectiveStateChanged;
```

The typed `UIScreenHost.QueueFree()` convenience first prepares teardown and
queues deletion only when preparation is complete. It does not make ancestor
deletion safe; scene owners must follow the explicit preparation protocol
below.

### Stable statuses

Only `UIScreenOpenStatus.Opened` returns a handle. Every rejected open is an
atomic no-op.

| Open status | Meaning |
|---|---|
| `Opened` | entry registered and adapters committed |
| `DuplicateKind` | the same concrete kind is already active |
| `IncompatibleEntry` | either active or requested policy declares the other kind incompatible |
| `ExclusiveGroupConflict` | a non-parent/child entry already owns the requested non-empty group |
| `InvalidNode` | view is invalid, deleting, the host itself, failed attachment, or the candidate was closed or invalidated by a callback during the final recompute pass |
| `InvalidParent` | requested parent handle is not active |
| `NodeAlreadyRegistered` | view already belongs to this host |
| `NodeOwnedByAnotherHost` | view belongs to another live host |
| `InvalidControlParentage` | view is parented outside its permitted attachment parent |
| `MissingRequiredAdapter` | requested lower-layer effect cannot be applied safely |
| `UnsupportedSubwindowMode` | a Window was requested without embedded subwindows |
| `InvalidProcessPolicy` | requested process policy cannot satisfy its pause context |
| `InvalidSpecification` | normalized entry values violate the contract |
| `HostMutating` | an open was attempted during an active close/cleanup transaction; retry after that transaction returns |
| `MalformedHost` | required scene children are missing/wrong, host is not ready, or teardown began |

`HostMutating` is an explicit synchronous rejection, never an implicit deferred
open. The host does not queue the view or spec. In particular, cleanup code may
request a later open only by returning from cleanup and retrying from its owner.
Model acceptance precedes creation of any per-Window focus sink, so duplicate,
compatibility, group, and parent rejection leave an already-in-tree Window's
exact child list and lifecycle untouched.

Close statuses are `Closed`, `AlreadyClosed`, `StaleHandle`, and
`HostTearingDown`. Cleanup receives exactly one of `Cancel`, `ExplicitAction`,
`Programmatic`, `NodeFreed`, `ParentClosed`, or `HostTeardown`. Closing a parent
closes descendants deepest/newest first with `ParentClosed`; a duplicate close
does not repeat cleanup or node operations.

Input dispatch returns:

- `Consumed`: the host owns the outcome and `_Input` marks the viewport event
  handled;
- `ReservedForTopEntry`: the root/parent path is suppressed while a retained
  controller or embedded GUI handler receives the still-unhandled event;
- `NoOwner`: the host takes no action.

Teardown preparation returns:

- `Deferred`: preparation is currently inside an active close/finalization
  mutation; deleting the containing scene is unsafe and the owner must retry;
- `Complete`: entries are closed, external views detached, leases restored,
  focus/bindings finalized, and ancestor deletion is safe.

## Entry declaration

Each `UIScreenEntrySpec` declares one concrete presentation:

| Field | Contract |
|---|---|
| `Kind` | required canonical concrete kind; unique while active |
| `Layer` | attachment/visual layer |
| `InputPriority` | `Passive`, `Screen`, `Modal`, or `Blocking` |
| `ProcessPolicy` | registration-time process rule |
| `Parent` | optional active logical parent |
| `ExclusiveGroup` | optional category conflict group |
| `IncompatibleKinds` | symmetric compatibility veto after normalization |
| `PauseTree` | contributes to the host's exact pause lease |
| `BlockGameplayInput` | contributes to presentation gameplay suppression |
| `Cursor`, `Hud` | explicit lease-backed override or `Inherit` |
| `LowerLayers` | effect contributed to every visually lower active entry |
| `Cancel` | static Cancel policy |
| `EntryCancelActions` | actions owned only when this entry is the applicable top entry |
| `InitialFocus`, `RestoreFocus` | optional validated focus targets |
| `InterceptCancel` | dynamic decision before static Cancel policy |
| `IsPresented`, `SetPresented` | presentation adapter overrides |
| `SetInteractive` | required when a Control with direct input must become inert |
| `FocusViewport` | viewport override; defaults to Control viewport or Window itself |
| `Cleanup` | at-most-once managed cleanup callback |
| `NodeLifetime` | `External`, `Hide`, or `QueueFree` terminal ownership |

Passive entries are visual/lifetime records only. They must not pause, block
gameplay, own Cancel/actions, change lower layers, request initial focus, or
intercept Cancel. Visual-only reward toasts and transitions should use
`Cursor = UICursorPolicy.Inherit` and `Hud = UIHudPolicy.Inherit` unless that
presentation intentionally authors an explicit visual policy.

## View, parentage, embedding, and lifetime defaults

An unparented `Control` is attached under the layer selected by `Layer`. An
already-parented Control is accepted only when its parent is exactly that layer.
An embedded `Window`/`AcceptDialog` is a separate focus viewport and is a direct
child of the host; it may be unparented or already parented directly to that
host. Other parentage is rejected.

For a Control, the defaults use `Visible`, `Show`/`Hide`, and
`control.GetViewport()`. Pointer shielding handles ordinary GUI input, but a
Control whose own `_Input` must be disabled supplies `SetInteractive`.

For an embedded Window, the defaults use `Visible`, `Show`/`Hide`, and the
Window as its focus viewport. `VisibleInert` snapshots and changes
`GuiDisableInput` and `Unfocusable`. Supply explicit presentation callbacks if
restoring the flow requires `Popup*()` positioning or another operation beyond
plain `Show()`.

Window registration requires
`host.GetViewport().GuiEmbedSubwindows == true`. HPA-379 must explicitly pin
`display/window/subwindows/embed_subwindows=true` (or configure the root
viewport equivalently); detached OS windows are unsupported.

`UINodeLifetime.External` is the default: caller owns disposal, and terminal
close/prepared teardown detaches a live view from the host attachment parent.
`Hide` hides the terminal view. `QueueFree` queues it after cleanup. Failed
registration rollback restores the incoming process mode and only undoes
attachment performed by that registration; it does not terminally hide/free or
detach a caller-preparented view.

## Process policy

The adapter snapshots the incoming `Node.ProcessMode` and restores it on close
or teardown.

| Policy | Registered mode and validity |
|---|---|
| `PreserveAndValidate` | preserves the incoming mode only when it satisfies both immediate aggregate pause and lifetime-bounded pause requirements |
| `InheritHost` | sets `Inherit`; invalid when the effective paused context would inherit from a Pausable attachment parent |
| `Pausable` | sets `Pausable`; invalid when the effective post-open context is paused |
| `WhenPaused` | sets `WhenPaused`; valid only when the candidate owns pause or descends from an active pausing ancestor |
| `Always` | sets `Always` |

Pausable modes use the immediate post-open pause reduction of every active
entry plus the candidate, because they must be able to process as soon as they
open. WhenPaused modes require a stronger lifetime bound: the candidate must
own pause or have a direct/transitive logical ancestor that owns pause. Closing
that ancestor also closes the candidate, so it cannot outlive its process
context. An unrelated root cannot borrow another root's temporary pause. A
pause-owning candidate is evaluated as paused even when no prior pause lease
exists.

Reusable Settings, Save/Load, and dialogs generally use `InheritHost` or
`Always` because they can appear under both Main Menu and Pause. The host is the
Always Cancel authority; the Game root must remain pausable.

## Parent, group, ordering, and reduction rules

Parentage is logical, not inferred from the Godot tree. A child requires an
active parent handle. Descendants precede ancestors for input and close before
them. Among unrelated entries, higher `InputPriority` wins, then newer sequence.
Visual ordering is `UIScreenLayer`, then sequence; input priority and visual
layer are deliberately independent.

Kinds are unique. Compatibility is symmetric even if only one entry lists the
other kind. A non-empty exclusive group permits only one unrelated member;
direct parent/child prompts may share the group. The host never implicitly
replaces an entry—close first, observe completion, then open the next.

For each active target, all visually higher owners contribute a lower-layer
effect. The reduction is:

```text
Hidden > VisibleInert > VisibleInteractive
```

The first affecting owner captures one baseline. Stronger/weaker owners update
the effective effect without replacing that baseline. The exact incoming
Control visibility/input state or Window presentation/`GuiDisableInput`/
`Unfocusable` state restores only after the final contribution ends. A Pause
can therefore keep gameplay inert while child Settings hides Pause; closing
Settings restores Pause without making gameplay interactive.

## Pause, cursor, HUD, and gameplay block restoration

Pause, cursor, and HUD overrides are leases. The first active contribution
captures the exact incoming value, every active contribution reduces to one
effective value, and ending the last contribution restores the captured value
once. If another system unpauses the tree while the host owns a pause lease,
the Always host records a drift violation and reasserts pause without replacing
the baseline.

Presentation gameplay block is separate from domain lifecycle. HPA-379 must
compose one root predicate equivalent to:

```csharp
bool IsGameplaySuppressed(
    UIScreenHost host,
    bool isInBattle,
    bool isInNpcInteraction,
    bool isInWorldInteraction) =>
    host.CurrentState.IsPresentationGameplayBlocked ||
    isInBattle ||
    isInNpcInteraction ||
    isInWorldInteraction;
```

Movement, interaction, and presentation-opening commands use that composed
predicate. Flow code may still consult individual flags for domain eligibility.

## Cancel ownership and precedence

One physical input event produces one host traversal and at most one logical
Cancel attempt.

- `CoreCancelActions` normally contains `pause_menu` and `ui_cancel`.
- `EntryCancelActions` belongs only to the applicable active entry;
  `toggle_inventory` never becomes generic Cancel against Settings or Battle.
- `ui_close_dialog` is not a core action. For embedded GUI close, the host
  reserves/pass-throughs the event and the Window's terminal signal closes the
  handle once.
- Window `CloseRequested`/`Canceled`, explicit buttons, and programmatic
  outcomes are lifecycle surfaces that call `TryClose`; they are not remapped
  action traversals.

For each logical candidate, `InterceptCancel` runs before the static policy:

1. `ConsumeHere` returns `Consumed`;
2. `ReserveForNativeHandler` returns `ReservedForTopEntry`;
3. `DeferToPolicy` applies `Cancel`;
4. `None` continues toward the logical parent/next candidate;
5. `Close`, `Consume`, and `PassThrough` stop with one outcome.

`PassThrough` returns `ReservedForTopEntry`. A root fallback is considered only
for matched core actions after no entry owns/reserves the event. Entry-scoped
actions never invoke it. While a live focus-restoration lease exists, a matching
core/top-entry Cancel is consumed as a temporary no-op.

## Focus acquisition and restoration

Non-passive initial focus is deferred until the view is in-tree. The host tries:

1. valid `InitialFocus`;
2. first valid focusable descendant;
3. a transparent visible focus sink for a Blocking entry.

The root sink is `MouseFilter.Ignore`, `FocusMode.All`, and has no domain
behaviour. Blocking embedded Windows receive an equivalent temporary sink in
their own viewport. Passive entries never acquire focus or create a restoration
lease. A deferred acquisition validates both the handle and current top input
owner so a lower re-entrant open cannot steal focus.

Closing captures a generation-tagged restoration lease. Restoration order is:

1. valid explicit `RestoreFocus` target;
2. valid control captured in the captured viewport;
3. active parent's valid initial target;
4. first focusable descendant of the top entry;
5. that entry's correct viewport sink when Blocking;
6. release focus when no UI owner remains.

Every Godot target is instance-validity checked before dereference. Each
ordinary runtime lease releases exactly once; stale generations cannot clear
newer work, duplicate closes cannot create another lease, teardown completes
pending work, and no-target restoration still removes the temporary Cancel
barrier. Superseding restoration completes the prior generation without an
intermediate state publication, installs the newer lease, then publishes once
through the enclosing close transaction. Core Cancel therefore observes one
continuous barrier across generations.

## Lifecycle and mandatory scene-owner teardown

External node exit prunes its entry with `NodeFreed`, closes descendants first,
runs managed cleanup once, recomputes policy, and restores the next owner.
Closing handles are ineligible for input immediately. Re-entrant close requests
use one FIFO queue; duplicate requests collapse.

Godot starts recursively deleting children before a host receives `_ExitTree`.
Consequently `_ExitTree` is only a defensive fallback and cannot preserve an
externally owned embedded Window during containing-scene deletion. Every HPA-379
scene owner must prepare first:

```csharp
public void DeleteContainingSceneWhenPrepared(
    UIScreenHost host,
    Node containingScene)
{
    var status = host.PrepareForTeardown();
    if (status == UIScreenTeardownPreparationStatus.Deferred)
    {
        Callable.From(() =>
            DeleteContainingSceneWhenPrepared(host, containingScene))
            .CallDeferred();
        return;
    }

    containingScene.QueueFree();
}
```

Preparation disables input and rejects opens, closes entries topmost-first with
`HostTeardown`, completes focus work, restores pause/cursor/HUD/process/Control/
Window state, removes subscriptions/sinks, and detaches externally owned views.
`Complete` is published only after finalization succeeds. Re-entry from close,
cleanup, state, or focus callbacks returns `Deferred`. If a finalization callback
throws, completion remains unpublished and a later call retries. During this
prepared-teardown boundary, exceptions from user `InitialFocus` or
`RestoreFocus` providers propagate and retain the restoration lease for the
retry; ordinary deferred runtime focus acquisition/restoration may log and use
the safe fallback chain.

HPA-379 must call this boundary before every navigation handoff or deletion of
the containing scene/ancestor, proceed only after `Complete`, and schedule the
retry outside the active mutation after `Deferred`.

## Diagnostics

`Diagnostics` returns copied, read-only records suitable for tests and runtime
inspection. It includes:

- active entries in logical input order, including normalized policy/sequence;
- effective pause, presentation block, cursor, HUD, top owner, and restoration
  pending state;
- reduced lower-layer effect and contributors for every target;
- copied core and entry-scoped action ownership;
- focus viewport/owner/sink records and restoration generation;
- incoming, registered, and current process modes plus Window classification;
- embedded-subwindow state;
- exact pause/cursor/HUD/Control/Window state-lease baselines;
- pause-drift count and last violation identifier.

Diagnostics do not expose mutable internal collections or live Godot object
references. Stable status codes—not log text—are the integration/test contract.
A `HostMutating` rejection leaves the diagnostic snapshot unchanged; mutation
phase is communicated by that synchronous result rather than a second mutable
diagnostic flag.

## Compilable synthetic flow registrations

The following class uses only synthetic Controls and embedded AcceptDialogs and
compiles against the HPA-378 API. Production adapters may add flow-specific
presentation, cleanup, and focus behaviour without changing the host contract.
Callers must inspect each result. If it is `HostMutating`, they retain the
unmodified view/spec and retry from the owning flow only after the current
`TryClose` call has returned; they never retry inline from `Cleanup`.

```csharp
using System.Collections.Generic;
using Godot;

public sealed class SyntheticUIScreenRegistrations
{
    private readonly UIScreenHost _host;
    private UIScreenHandle? _battleHandle;

    public SyntheticUIScreenRegistrations(UIScreenHost host) => _host = host;

    public UIScreenOpenResult OpenPause(Control pause, Control resumeButton) =>
        _host.TryPresent(pause, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Pause,
            Layer = UIScreenLayer.Screen,
            InputPriority = UIInputPriority.Screen,
            ProcessPolicy = UIProcessPolicy.InheritHost,
            PauseTree = true,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => resumeButton,
            NodeLifetime = UINodeLifetime.External
        });

    public UIScreenOpenResult OpenInventory(
        Control inventory,
        UIScreenHandle? pauseParent = null) =>
        _host.TryPresent(inventory, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Inventory,
            Layer = UIScreenLayer.Screen,
            InputPriority = UIInputPriority.Screen,
            ProcessPolicy = UIProcessPolicy.InheritHost,
            Parent = pauseParent,
            PauseTree = !pauseParent.HasValue,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.VisibleInert,
            Cancel = UICancelPolicy.Close,
            EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
            NodeLifetime = UINodeLifetime.External
        });

    public UIScreenOpenResult OpenSettings(
        Control settings,
        Control firstSetting,
        UIScreenHandle pauseParent) =>
        _host.TryPresent(settings, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Settings,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.InheritHost,
            Parent = pauseParent,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.Hidden,
            Cancel = UICancelPolicy.Close,
            InitialFocus = () => firstSetting,
            NodeLifetime = UINodeLifetime.External
        });

    public UIScreenOpenResult OpenEmbeddedSaveLoad(
        AcceptDialog saveLoad,
        Control cancelButton,
        UIScreenHandle pauseParent)
    {
        if (!_host.GetViewport().GuiEmbedSubwindows)
        {
            return new UIScreenOpenResult(
                UIScreenOpenStatus.UnsupportedSubwindowMode,
                null);
        }

        UIScreenHandle? handle = null;
        var nativeDismissalHandled = false;
        void DisconnectNativeDismissal()
        {
            saveLoad.Canceled -= CloseFromNativeDismissal;
            saveLoad.CloseRequested -= CloseFromNativeDismissal;
        }

        void CloseFromNativeDismissal()
        {
            // AcceptDialog can emit both Canceled and CloseRequested for one
            // dismissal. Commit the guard before closing so the host sees one
            // terminal request even if signal delivery is re-entrant.
            if (nativeDismissalHandled || !handle.HasValue)
                return;

            nativeDismissalHandled = true;
            _host.TryClose(handle.Value, UIScreenCloseReason.Cancel);
        }

        saveLoad.Canceled += CloseFromNativeDismissal;
        saveLoad.CloseRequested += CloseFromNativeDismissal;
        var result = _host.TryPresent(saveLoad, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.SaveLoad,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Modal,
            ProcessPolicy = UIProcessPolicy.InheritHost,
            Parent = pauseParent,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.Hidden,
            Cancel = UICancelPolicy.PassThrough,
            InitialFocus = () => cancelButton,
            InterceptCancel = _ =>
                UIInputInterception.ReserveForNativeHandler,
            Cleanup = _ => DisconnectNativeDismissal(),
            NodeLifetime = UINodeLifetime.External
        });
        handle = result.Handle;

        if (result.Status != UIScreenOpenStatus.Opened)
        {
            DisconnectNativeDismissal();
            return result;
        }

        saveLoad.Show();
        return result;
    }

    public UIScreenOpenResult BeginBattle(Control battle)
    {
        var result = _host.TryPresent(battle, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.Battle,
            Layer = UIScreenLayer.Modal,
            InputPriority = UIInputPriority.Blocking,
            ProcessPolicy = UIProcessPolicy.Always,
            BlockGameplayInput = true,
            Cursor = UICursorPolicy.Visible,
            LowerLayers = UILowerLayerPolicy.Hidden,
            Cancel = UICancelPolicy.Consume,
            NodeLifetime = UINodeLifetime.External
        });
        _battleHandle = result.Handle;
        return result;
    }

    // A cleared domain battle flag does not call this. Call it only when the
    // Battle presentation actually terminates (Continue, close, or node exit).
    public UIScreenCloseResult EndBattlePresentation()
    {
        if (!_battleHandle.HasValue)
            return new UIScreenCloseResult(UIScreenCloseStatus.StaleHandle);

        var result = _host.TryClose(
            _battleHandle.Value,
            UIScreenCloseReason.ExplicitAction);
        _battleHandle = null;
        return result;
    }

    public UIScreenOpenResult ShowRewardToast(Control toast) =>
        _host.TryPresent(toast, new UIScreenEntrySpec
        {
            Kind = UIScreenKinds.RewardToast,
            Layer = UIScreenLayer.Toast,
            InputPriority = UIInputPriority.Passive,
            ProcessPolicy = UIProcessPolicy.InheritHost,
            LowerLayers = UILowerLayerPolicy.VisibleInteractive,
            Cancel = UICancelPolicy.None,
            NodeLifetime = UINodeLifetime.QueueFree
        });

    public UIScreenOpenResult ShowRequiredAcknowledgement(
        Control acknowledgement,
        Button continueButton,
        UIScreenHandle parent)
    {
        UIScreenHandle? handle = null;
        void Continue()
        {
            if (handle.HasValue)
            {
                _host.TryClose(
                    handle.Value,
                    UIScreenCloseReason.ExplicitAction);
            }
        }

        continueButton.Pressed += Continue;
        var result = _host.TryPresent(
            acknowledgement,
            new UIScreenEntrySpec
            {
                Kind = UIScreenKinds.RewardAcknowledgement,
                Layer = UIScreenLayer.Modal,
                InputPriority = UIInputPriority.Blocking,
                ProcessPolicy = UIProcessPolicy.InheritHost,
                Parent = parent,
                BlockGameplayInput = true,
                Cursor = UICursorPolicy.Visible,
                LowerLayers = UILowerLayerPolicy.VisibleInert,
                Cancel = UICancelPolicy.Consume,
                InitialFocus = () => continueButton,
                Cleanup = _ => continueButton.Pressed -= Continue,
                NodeLifetime = UINodeLifetime.External
            });
        handle = result.Handle;
        if (result.Status != UIScreenOpenStatus.Opened)
            continueButton.Pressed -= Continue;
        return result;
    }
}
```

## HPA-379 prerequisites and migration gate

HPA-379 must complete these prerequisites before enabling host-owned root pause
or removing legacy authorities:

1. Call `PrepareForTeardown` at every containing-scene navigation/deletion
   boundary, proceed only on `Complete`, and defer/retry after `Deferred`.
2. Pin embedded-subwindow mode explicitly and reject unsupported detached
   Windows.
3. Audit the real Game/floor scene process tree, including effective modes, and
   correct runtime `GridMap` from Always to Inherit/Pausable before root Pause
   sets `SceneTree.Paused`.
4. Compose the production HUD beneath the explicitly Pausable `HUDLayer`, with
   reviewed exceptions only.
5. Introduce one gameplay-block predicate that ORs
   `CurrentState.IsPresentationGameplayBlocked` with battle, NPC, and atomic
   world-interaction domain flags; migrate every relevant gameplay input guard.
6. Configure core versus entry-scoped actions, remove competing pause/Cancel/
   cursor/HUD/presentation-input authorities one flow at a time, and preserve
   each terminal signal/domain cleanup.
7. Keep one Battle entry through visible result presentation even after the
   domain battle flag clears; close it only when the view terminates.
8. Run real-scene physical-input and timing regressions while keeping all
   HPA-376/HPA-378 synthetic contract tests green.
9. Treat `HostMutating` as an explicit retry signal: never open from a managed
   cleanup callback and never assume the host deferred a rejected open.

HPA-378 intentionally performs no `Game.cs`, `MainMenu.cs`, floor,
`project.godot`, or existing production screen-controller migration.
