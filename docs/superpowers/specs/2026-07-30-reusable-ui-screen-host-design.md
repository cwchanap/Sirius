# HPA-378 Reusable UI Screen Host Design

**Status:** Proposed design
**Date:** 2026-07-30
**Issue:** HPA-378
**Repository:** `cwchanap/Sirius`
**Runtime:** Godot 4.6, C#/.NET 8, GdUnit4
**Depends on:** HPA-376
**Downstream integration:** HPA-379

## 1. Summary

Sirius currently coordinates UI ownership through root-specific fields, direct scene-tree pause changes, and priority-ordered conditionals in `Game._Input()`. HPA-376 documented and protected those behaviours with a 50-flow lifecycle contract. HPA-378 introduces a reusable, scene-local `UIScreenHost` that represents the same ordering and lifecycle rules explicitly.

The host owns presentation state only for the root scene in which it is instantiated. It manages:

- visual layer placement;
- active screen and modal ordering;
- parent-child presentation relationships;
- compatibility and duplicate-open rejection;
- effective tree-pause ownership;
- gameplay-input blocking state;
- cursor and HUD policy;
- cancel dispatch across host and native-dialog input surfaces;
- initial focus and focus restoration;
- invalid-node pruning and idempotent cleanup.

The host does not own gameplay, save, settings, battle, inventory, NPC, reward, or scene-transition domain state. Existing managers and controllers remain responsible for domain outcomes and terminal signals.

This document is the proposed design artifact reviewed in PR #20. Subsequent HPA-378 implementation work delivers the reusable host, pure model, Godot adapters, tests, and public contract. HPA-379 then integrates the host into `MainMenu.tscn` and `Game.tscn` and removes competing legacy pause, cancel, cursor, and HUD authorities only after parity is proven.

## 2. Goals

1. Provide one reusable host implementation for both Main Menu and gameplay roots.
2. Encode the HPA-376 modal-priority matrix as explicit stack and dispatch rules.
3. Require every registered entry to declare visual layer, input priority, pause, gameplay block, cursor, HUD, lower-layer, focus, cancel, process, and cleanup behaviour.
4. Derive effective presentation policy centrally from active entries.
5. Support existing `Control`, `Window`, and `AcceptDialog` presentations without rewriting their domain controllers.
6. Keep cancel dispatch alive while the host owns `SceneTree.Paused` without making the gameplay root process while paused.
7. Make duplicate requests, invalid nodes, repeated closes, and root teardown deterministic and harmless.
8. Keep implementation locally owned by the scene root rather than introducing a UI autoload or gameplay-state singleton.
9. Make stack and policy logic independently testable without constructing the full Game scene.

## 3. Non-goals

HPA-378 does not:

- integrate existing Main Menu or gameplay flows into the host;
- modify `Game.cs`, `MainMenu.cs`, or existing production screen controllers;
- redesign or restyle any screen;
- add the production quit confirmation or reward presentation;
- replace `GameManager`, `SaveManager`, `SettingsManager`, or other domain managers;
- change save, battle, inventory, settings, NPC, puzzle, or reward rules;
- remove the HPA-376 legacy regression tests;
- introduce cross-scene navigation history;
- introduce a global UI service or autoload;
- add implicit replacement, multi-instance screen kinds, or general navigation history.

## 4. Grounding in HPA-376

The HPA-376 contract defines six input-priority levels:

1. child popup or key capture;
2. blocking error or confirmation;
3. deferred Pause restoration;
4. owning modal or world interaction;
5. parent screen;
6. gameplay fallback.

It also hands seven behaviours to HPA-378/379:

- Inventory opened from Pause;
- host-owned root Pause policy;
- quit-with-risk confirmation from Pause;
- quit-with-risk confirmation from in-game Save/Load;
- non-blocking reward toast;
- blocking reward acknowledgement;
- destructive confirmation with a safe default action.

The host represents these behaviours without embedding their domain actions. For example, it can represent a destructive confirmation whose generic Cancel returns to its parent, but it does not decide whether leaving the current game is safe or perform the scene transition.

The HPA-376 cancel family spans multiple surfaces:

- configured root actions such as `pause_menu` and `ui_cancel`;
- flow toggles such as `toggle_inventory`;
- native `ui_close_dialog` GUI handling;
- `Window.CloseRequested` and guarded terminal signals;
- explicit buttons and controller-owned actions.

The host coordinates those surfaces without converting all of them into one global action list.

## 5. Architectural decision

Use a pure presentation-state model with a thin Godot host adapter.

```text
UIScreenHost : Control
├── HUDLayer
├── ScreenLayer
├── ModalLayer
├── ToastLayer
└── TransitionLayer

UIScreenHost
├── UIScreenStackModel
├── UIScreenPolicyResolver
├── UIScreenFocusCoordinator
├── UIScreenInputDispatcher
├── UIScreenViewRegistry
└── Godot node/layer adapter
```

### 5.1 Pure model boundary

The pure model stores only immutable value data:

```csharp
public sealed record UIScreenEntryPolicy
{
    public required StringName Kind { get; init; }
    public required UIScreenLayer Layer { get; init; }
    public required UIInputPriority InputPriority { get; init; }
    public UIScreenHandle? Parent { get; init; }
    public required StringName ExclusiveGroup { get; init; }
    public required IReadOnlySet<StringName> IncompatibleKinds { get; init; }
    public bool PauseTree { get; init; }
    public bool BlockGameplayInput { get; init; }
    public UICursorPolicy Cursor { get; init; }
    public UIHudPolicy Hud { get; init; }
    public UILowerLayerPolicy LowerLayers { get; init; }
    public UICancelPolicy Cancel { get; init; }
    public required IReadOnlySet<StringName> EntryCancelActions { get; init; }
}
```

`UIScreenStackModel` owns ordering, parent-child relationships, compatibility, duplicate detection, close cascades, and policy inputs. It has no live `Node`, `Control`, `Viewport`, delegate, or scene-tree dependency.

The Godot adapter stores live state separately:

```csharp
internal sealed record UIScreenViewAdapter
{
    public required Node View { get; init; }
    public required Func<bool> IsPresented { get; init; }
    public required Action<bool> SetPresented { get; init; }
    public required Action<bool> SetInteractive { get; init; }
    public required Func<Viewport> FocusViewport { get; init; }
    public Func<Control?>? InitialFocus { get; init; }
    public Func<Control?>? RestoreFocus { get; init; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; init; }
    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }
}
```

This separation keeps priority and policy tests independent from frame timing and keeps Godot lifecycle code small enough to review directly.

### 5.2 Rejected alternatives

#### Global UI autoload

A global manager would outlive individual root scenes, require explicit cross-scene reset logic, and risk retaining stale nodes or pause ownership. It also violates the requirement that Main Menu and gameplay each own a local host.

#### Gameplay root as always-processing input forwarder

Setting the Game root to `ProcessMode.Always` would cause default-inheriting gameplay descendants to continue processing while the tree is paused. Keeping the root at its normal process mode while asking it to forward from `_Input()` would fail because that callback stops when the root cannot process. Therefore the Game root is not the host's runtime input-forwarding seam.

#### One monolithic `UIScreenHost`

Putting stack mutation, policy reduction, node attachment, focus, input dispatch, and diagnostics into one class would recreate the conditional complexity currently concentrated in `Game`.

#### Mandatory screen interface

Requiring every existing dialog to implement a new interface would make HPA-379 a broad rewrite. Existing screens register through an entry specification and view adapters. New screens may adopt a convenience interface later, but the host contract does not require one.

## 6. Scene composition and visual layers

### 6.1 Public host scene

`UIScreenHost.tscn` is a full-rect `Control` with `ProcessMode.Always`. Its public children are full-rect layer containers in fixed visual order:

```text
UIScreenHost
├── HUDLayer
├── ScreenLayer
├── ModalLayer
├── ToastLayer
└── TransitionLayer
```

The layer order is visual, not input priority. Toasts may render above modals while owning no cancel or gameplay input. Input priority is declared separately on each active entry.

All layer references are validated in `_Ready()`. A malformed host scene fails closed: registration returns an infrastructure status and no global state is mutated.

### 6.2 Game placement

HPA-379 places the host under the existing gameplay `CanvasLayer`, not under the world `Node2D` hierarchy as an inheriting gameplay child:

```text
Game : Node2D                         # remains normal/pausable
└── UI : CanvasLayer
    └── UIScreenHost : Control        # ProcessMode.Always
        ├── HUDLayer
        │   └── GameUI                # existing HUD is moved/composed here
        ├── ScreenLayer
        ├── ModalLayer
        ├── ToastLayer
        └── TransitionLayer
```

This preserves one CanvasLayer ordering domain for HUD and Control-based overlays. Existing draggable HUD content remains under `HUDLayer`. Native windows are direct children of the host but retain their own viewport rendering and focus behavior.

The Game root and world nodes are not changed to `Always`; gameplay processing must stop under host-owned pause.

### 6.3 Main Menu placement

HPA-379 places one host under the Main Menu root and composes existing root content into its screen or HUD layer. Main Menu uses the same host implementation but normally never requests tree pause.

### 6.4 Transition layer

`TransitionLayer` is reserved visual placement for fades, wipes, loading covers, and scene handoff surfaces. It has no implicit policy. A visual-only transition uses passive priority and no input block; a blocking transition uses blocking priority, explicitly blocks gameplay, and declares its cancel behavior. The layer name alone never pauses, blocks, hides HUD, or captures focus.

## 7. Identity, kinds, layers, and priority

### 7.1 Handles and identity

Each active entry has:

- a stable `Kind` (`StringName`) identifying the presentation type;
- a generated instance token in `UIScreenHandle`;
- an optional parent handle;
- a monotonic presentation sequence number.

The token prevents a stale handle from closing a later instance of the same kind.

Only one active entry of a kind is allowed. A duplicate open is rejected without changing focus, pause, cursor, HUD, process mode, node parentage, or stack order.

A downstream toast presenter queues or coalesces payloads and presents at most one host entry for a given toast kind at a time.

### 7.2 Canonical kinds

The repository defines shared constants rather than scattering free-form strings:

```csharp
public static class UIScreenKinds
{
    public static readonly StringName Pause = "pause";
    public static readonly StringName Settings = "settings";
    public static readonly StringName Inventory = "inventory";
    public static readonly StringName SaveLoad = "save_load";
    public static readonly StringName Confirmation = "confirmation";
    public static readonly StringName Error = "error";
    public static readonly StringName Battle = "battle";
    public static readonly StringName RewardToast = "reward_toast";
    public static readonly StringName RewardAcknowledgement = "reward_acknowledgement";
    public static readonly StringName Transition = "transition";
}
```

Feature-specific kinds may be added centrally. The host does not attach domain meaning to the constants.

### 7.3 Visual layer

```csharp
public enum UIScreenLayer
{
    Hud,
    Screen,
    Modal,
    Toast,
    Transition
}
```

Layer determines visual placement and same-layer draw order only.

### 7.4 Input priority

```csharp
public enum UIInputPriority
{
    Passive,
    Screen,
    Modal,
    Blocking
}
```

- `Passive`: a non-owning presentation such as a toast or visual-only transition;
- `Screen`: a parent or full-screen flow;
- `Modal`: an owning presentation-backed modal or interaction;
- `Blocking`: a topmost error, confirmation, required acknowledgement, or blocking transition.

Parent-child ancestry outranks the enum: an active child is considered before its parent even if both use the same priority.

`Passive` is deliberately narrow. A passive entry must satisfy all of the following:

- `PauseTree == false`;
- `BlockGameplayInput == false`;
- `Cancel == None`;
- no entry-scoped cancel actions;
- `LowerLayers == VisibleInteractive`;
- no initial focus request.

An input-blocking transition uses `InputPriority.Blocking` with `Cancel.None`; it is not Passive. This prevents invisible input black holes.

### 7.5 Exclusive groups

`ExclusiveGroup` uses an empty `StringName` for no group. Empty groups never conflict. Two non-empty equal groups conflict unless one entry is an ancestor of the other and the specifications explicitly permit that parent-child flow.

## 8. Registration contract

### 8.1 Entry specification

```csharp
public sealed record UIScreenEntrySpec
{
    public required StringName Kind { get; init; }
    public required UIScreenLayer Layer { get; init; }
    public required UIInputPriority InputPriority { get; init; }

    public UIScreenHandle? Parent { get; init; }
    public StringName ExclusiveGroup { get; init; }
    public IReadOnlySet<StringName> IncompatibleKinds { get; init; }

    public bool PauseTree { get; init; }
    public bool BlockGameplayInput { get; init; }

    public UICursorPolicy Cursor { get; init; }
    public UIHudPolicy Hud { get; init; }
    public UILowerLayerPolicy LowerLayers { get; init; }
    public UICancelPolicy Cancel { get; init; }
    public IReadOnlySet<StringName> EntryCancelActions { get; init; }

    public Func<Control?>? InitialFocus { get; init; }
    public Func<Control?>? RestoreFocus { get; init; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; init; }

    public Func<bool>? IsPresented { get; init; }
    public Action<bool>? SetPresented { get; init; }
    public Action<bool>? SetInteractive { get; init; }
    public Func<Viewport>? FocusViewport { get; init; }

    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }
}
```

Collections use empty immutable values rather than `null`.

`UIScreenHost` validates the spec, projects value fields into `UIScreenEntryPolicy`, and stores live state in `UIScreenViewAdapter`.

### 8.2 Default view adapters

For an ordinary `Control`:

- presentation state is `Visible`;
- `SetPresented` calls `Show()` or `Hide()`;
- interactivity uses pointer shielding and an optional explicit adapter for direct `_Input()` behavior;
- focus viewport is `control.GetViewport()`.

For an ordinary `Window` or `AcceptDialog`:

- presentation state is `Visible`;
- hiding calls `Hide()`;
- restoring uses an explicit adapter when the flow requires `Popup*()` rather than plain `Show()`;
- visible-inert behavior snapshots and changes `GuiDisableInput` and `Unfocusable`;
- focus viewport is the window itself.

A legacy flow must provide explicit adapters when defaults do not preserve its popup, sizing, input, or restoration semantics. Registration is rejected before stack mutation when a requested policy cannot be applied safely.

### 8.3 Node lifetime

```csharp
public enum UINodeLifetime
{
    External,
    Hide,
    QueueFree
}
```

- `External`: caller owns disposal; host removes registration only.
- `Hide`: host hides the view on terminal close.
- `QueueFree`: host queues the view after cleanup.

Hidden parents remain active; `NodeLifetime` applies only when their own handles terminally close.

## 9. Public results and stable status codes

### 9.1 Open result

```csharp
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
    InvalidSpecification,
    MalformedHost
}

public readonly record struct UIScreenOpenResult(
    UIScreenOpenStatus Status,
    UIScreenHandle? Handle);
```

Only `Opened` contains a handle. Every rejection is a strict no-op. Tests assert stable status codes, not error strings.

### 9.2 Close result

```csharp
public enum UIScreenCloseStatus
{
    Closed,
    AlreadyClosed,
    StaleHandle,
    HostTearingDown
}

public readonly record struct UIScreenCloseResult(UIScreenCloseStatus Status);
```

### 9.3 Close reasons

```csharp
public enum UIScreenCloseReason
{
    Cancel,
    ExplicitAction,
    Programmatic,
    NodeFreed,
    ParentClosed,
    HostTeardown
}
```

There is no `Replaced` reason because the host has no replacement operation. Callers explicitly close an entry, observe completion, then present another entry. Future replacement APIs are out of scope.

The reason is delivered to managed cleanup at most once.

## 10. Process-mode and runtime input ownership

### 10.1 Hard process-mode contract

`UIScreenHost` is the only always-processing cancel dispatcher for its root scene. It sets `ProcessMode.Always` in its scene and owns `_Input(InputEvent)`.

The gameplay root remains at its normal inherited/pausable mode. This ensures gameplay `_Input`, `_Process`, and physics callbacks stop when `SceneTree.Paused == true`, while the host still receives cancel input and can invoke the configured root fallback delegate.

Host layer containers inherit `Always` from the host. An attached `Control` that keeps `ProcessMode.Inherit` therefore remains able to receive UI input while the tree is paused. A reusable view may explicitly use `Always`; an explicitly `Pausable` view that must remain interactive under any host-owned pause is invalid unless its adapter snapshots and changes process mode safely.

A pure pause-only screen may use `WhenPaused`, but this is not the default because Settings, Save/Load, confirmations, and other views are reused in both paused and unpaused roots. Reusable host views should normally inherit `Always` from the host.

Native `Window` and `AcceptDialog` nodes are parented beneath the host and must be able to process while paused. The adapter validates effective process behavior and separately manages native viewport GUI input through `GuiDisableInput` and `Unfocusable`.

The host never changes the Game root to `Always` and never relies on a paused root's `_Input()` callback.

### 10.2 Host input callback

```csharp
public override void _Input(InputEvent inputEvent)
{
    var result = TryHandleInput(inputEvent);
    if (result == UIInputDispatchResult.Consumed)
    {
        GetViewport().SetInputAsHandled();
    }
    // ReservedForTopEntry is deliberately left unhandled for the registered
    // legacy _Input or native GUI owner. NoOwner is ignored by the host.
}
```

`TryHandleInput` remains public for deterministic unit/runtime tests and synthetic integration adapters. Production roots do not forward the same event a second time.

### 10.3 Root fallback under pause

`RootCancelFallback` is a delegate invoked synchronously by the always-processing host. The target root object does not need to receive `_Input()` for the delegate to execute.

Gameplay uses the fallback to:

- open Pause when no UI entry owns or reserves a core cancel action and no domain blocker is active;
- consume a core cancel while an external atomic world interaction blocks fallback;
- decline when no root action applies.

Once Pause is active, its host entry—not a residual `Game._Input()` ladder—owns close/back behavior.

### 10.4 Sibling `_Input()` ordering

The design does not assume deterministic relative ordering among different nodes' `_Input()` callbacks.

Correctness comes from one terminal owner per active cancel path:

- A registered legacy controller retaining its own `_Input()` terminal handling uses `Cancel.PassThrough` or a dynamic native reservation.
- An entry using host-owned `Cancel.Close` or `Cancel.Consume` must have its competing legacy cancel handler removed or disabled before registration becomes active.
- Hidden or inert lower entries with direct `_Input()` handling are disabled before the new effective stack state is published.
- Native GUI pass-through is valid only when no lower or unrelated `_Input()` owner can consume the event first.
- Gameplay Pause fallback exists only in the host's root fallback.

If a retained top legacy controller consumes before the host, that intended owner has completed the attempt. If the host runs first, it reserves the event and leaves it for the same registered owner. No second authority performs the terminal action.

## 11. Cancel action surfaces

### 11.1 Core root cancel actions

Host options declare the root cancel family:

```csharp
public sealed record UIScreenHostOptions
{
    public Control? HudRoot { get; init; }
    public IReadOnlySet<StringName> CoreCancelActions { get; init; }
    public Func<UIRootCancelContext, UIRootCancelResult>? RootCancelFallback { get; init; }
    public Action<bool>? GameplayInputBlockChanged { get; init; }
}
```

Expected Game configuration includes both:

```csharp
CoreCancelActions = new HashSet<StringName>
{
    "pause_menu",
    "ui_cancel"
};
```

These are evaluated against the logical top entry and may reach `RootCancelFallback` when no entry owns or reserves them.

Main Menu may use the same pair with no fallback, or only the UI action family needed by its registered children.

### 11.2 Entry-scoped cancel actions

`EntryCancelActions` apply only to that active entry. `toggle_inventory` is the primary example.

Rules:

- An active Inventory entry may declare `toggle_inventory` as an additional close action.
- Pressing `toggle_inventory` while Settings, Save/Load, NPC, Battle, or another top flow is active does not turn the toggle into a generic cancel against that top entry.
- Entry-scoped actions never invoke the root cancel fallback.
- Opening Inventory remains a gameplay or explicit Pause action outside the generic cancel algorithm.
- If gameplay input is blocked, a non-active entry's toggle cannot open it through ordinary gameplay input.

The dispatcher checks the core family and each candidate entry's own additional actions without promoting those actions globally.

### 11.3 Native dialog close action

`ui_close_dialog` is not added to `CoreCancelActions`. HPA-376's synchronization may cause one physical keyboard/controller event to match `pause_menu`, `ui_cancel`, and `ui_close_dialog`.

The host treats the event as one logical attempt:

- it records all matched host action names once;
- a `PassThrough` or dynamic native reservation leaves the event unhandled;
- the native GUI later consumes `ui_close_dialog`;
- the host does not dispatch a second logical cancel when the dialog's terminal signal closes the handle.

### 11.4 Non-action terminal surfaces

`Window.CloseRequested`, `Canceled`, guarded controller terminal signals, explicit buttons, and programmatic domain outcomes are not remapped into input actions. Their integration adapters call `TryClose` once with the appropriate close reason.

## 12. Input contexts and dispatch algorithm

```csharp
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

public enum UIRootCancelResult
{
    Declined,
    Consumed
}
```

### 12.1 Dispatch result

```csharp
public enum UIInputDispatchResult
{
    NoOwner,
    Consumed,
    ReservedForTopEntry
}
```

- `Consumed`: host marks the viewport event handled.
- `ReservedForTopEntry`: host suppresses parent/root fallback but leaves the event unhandled for the registered native/legacy owner.
- `NoOwner`: host takes no action.

### 12.2 Static cancel policy

```csharp
public enum UICancelPolicy
{
    None,
    Close,
    Consume,
    PassThrough
}
```

- `None`: continue searching.
- `Close`: host closes candidate and consumes.
- `Consume`: host consumes without closing.
- `PassThrough`: host reserves for candidate's native or retained handler.

### 12.3 Dynamic interception

```csharp
public enum UIInputInterception
{
    DeferToPolicy,
    ConsumeHere,
    ReserveForNativeHandler
}
```

For each candidate:

1. `ConsumeHere` returns `Consumed` without closing.
2. `ReserveForNativeHandler` returns `ReservedForTopEntry`.
3. `DeferToPolicy`, or no interceptor, evaluates static `UICancelPolicy`.
4. The first non-`None` result wins.

Distinct names avoid conflating dynamic interception with static policy.

### 12.4 Algorithm

For each input event:

1. Determine matched core actions once.
2. Prune invalid entries.
3. If focus restoration is pending and a core or top-entry action matches, consume as a no-op.
4. Traverse active entries in logical input order.
5. For each candidate, determine its matched entry-scoped actions.
6. Skip candidates for which neither core nor entry action matched.
7. Invoke dynamic interception, then static policy.
8. Stop at the first owner, reservation, or close.
9. Invoke root fallback only when a core action matched and no entry owned or reserved it.
10. Return `NoOwner` otherwise.

A physical event matching multiple synchronized actions produces one traversal and one terminal result.

## 13. Stack, compatibility, and ordering

### 13.1 Logical ordering

Entries have two independent orderings:

- visual order: layer rank, then presentation sequence;
- logical input order: active child before ancestor, then `Blocking` before `Modal` before `Screen` before `Passive`, then newest sequence.

A child always outranks its parent. A passive toast does not displace a modal's cancel ownership merely because it draws above it.

Unrelated input-owning entries should normally be prevented by incompatibility or exclusive groups. Explicit priority still makes behavior deterministic if compatible entries coexist.

### 13.2 Parent-child relationships

- A child requires an active parent handle.
- A parent may have sequential compatible children.
- Closing a child restores only its invoking parent or next valid active ancestor.
- Closing a parent closes descendants topmost-first.
- Child cleanup runs before parent cleanup.
- A child cannot outlive an invalid or closed parent.
- Hidden or inert parents remain registered and retain domain state.

### 13.3 Compatibility

An open is rejected when:

- the same kind is active;
- either side declares the other's kind incompatible;
- equal non-empty exclusive groups conflict;
- requested parent is invalid or inactive;
- required view/process/native adapters are missing.

Compatibility is symmetric even if only one side declares a conflict.

### 13.4 No implicit replacement

The host never closes an active entry because an incompatible open was requested. Caller closes explicitly, waits for completion, then opens the next entry.

## 14. Compositional lower-layer policy

```csharp
public enum UILowerLayerPolicy
{
    VisibleInteractive,
    VisibleInert,
    Hidden
}
```

Lower-layer effects are not chosen from one topmost owner. They are composed from every active owner.

### 14.1 Reduction rule

For each target entry:

1. Find every active entry visually/logically above it whose lower-layer policy applies to the target.
2. Reduce those effects using:

```text
Hidden > VisibleInert > VisibleInteractive
```

3. Apply the strongest current effect.
4. Keep the target's incoming baseline snapshot while at least one owner affects it.
5. Restore the exact baseline only when no active owner affects it.

This preserves parent effects across child opens. Example:

- Pause keeps gameplay visible-inert.
- Settings child hides Pause.
- While Settings is open, Pause's visible-inert effect on gameplay remains part of the reduction.
- Closing Settings restores Pause while leaving gameplay inert until Pause itself closes.

The same applies to Inventory-from-Pause and destructive confirmations.

### 14.2 Control mechanism

For `Control` targets:

- `Hidden` snapshots and changes `Visible`;
- `VisibleInert` uses an input shield, transfers focus to the owner, and invokes `SetInteractive(false)` when direct `_Input()` exists;
- exact prior host-managed values are restored when reduction weakens or ends.

### 14.3 Native-window mechanism

For `Window`/`AcceptDialog` targets:

- `Hidden` snapshots presentation state and calls the registered hide adapter;
- restoration uses a flow-specific popup adapter where plain `Show()` is insufficient;
- `VisibleInert` snapshots and sets `GuiDisableInput` and `Unfocusable`;
- a Control-layer shield is never treated as sufficient for a separate native viewport;
- exact incoming values are restored as effects weaken or end.

An owner open is rejected if its requested effect cannot be applied safely to a native target.

## 15. Effective policy derivation

Policy recomputes after every successful open, close, prune, cascade, and teardown mutation.

### 15.1 Pause ownership

Effective pause is true when any active entry requests `PauseTree`.

The host uses exact snapshot ownership:

1. On false→true, capture current `SceneTree.Paused`.
2. Set `SceneTree.Paused = true`.
3. Keep one snapshot while any pausing entry remains.
4. On true→false, restore captured value once.
5. On teardown, restore it if still owned.

This preserves an already-paused parent and matches the HPA-376 Inventory restoration contract.

HPA-379 must remove direct UI-owned pause mutation from a flow before registering that flow with `PauseTree`.

### 15.2 Gameplay-input blocking

Effective gameplay block is the OR of `BlockGameplayInput` across active entries.

The host publishes this value; gameplay controllers use it for non-cancel input. The host does not become a movement, interaction, combat, or inventory-open command manager.

External atomic domain blockers without presentations remain root/domain responsibility.

### 15.3 Cursor

The effective cursor policy is the highest logical-priority active explicit override. The host snapshots the exact incoming mouse mode before the first override and restores it after the last.

### 15.4 HUD

The effective HUD policy is the highest logical-priority explicit override. The host snapshots exact incoming HUD visibility before the first override and restores it after the last.

A non-inherit HUD policy is invalid when the host has no configured HUD root.

### 15.5 Effective state

```csharp
public sealed record UIScreenEffectiveState(
    bool IsTreePauseOwned,
    bool IsGameplayInputBlocked,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    bool IsFocusRestorationPending);
```

`EffectiveStateChanged` emits only when the value changes and only after a complete consistent mutation.

## 16. Focus model and focus sinks

### 16.1 Focus records

Before a child opens, the host captures:

- the active parent's registered focus viewport;
- that viewport's current focused `Control`;
- the parent handle and instance token.

For a Control entry, default viewport is `view.GetViewport()`. For a native Window, default viewport is the window itself.

### 16.2 Host-owned sinks

The focus sink is an internal host responsibility, not caller domain state.

- `UIScreenHost.tscn` contains one invisible, mouse-ignoring, focusable root sink for Control-layer entries.
- For a blocking native window with no focusable descendant, the adapter creates one non-drawing focusable sink inside that window's viewport and removes it on unregister.
- Sink creation failure rejects the open before policy mutation.
- Diagnostics identify whether focus currently rests on a sink and in which viewport.

Cancel dispatch is stack-driven and does not depend on sink focus, but a blocking entry must never leave keyboard/gamepad navigation focused in a lower entry.

### 16.3 Initial focus

After a view is in tree, presented, and interactive, acquisition is deferred:

1. declared `InitialFocus`;
2. first valid focusable descendant;
3. appropriate host-owned sink when blocking and no target exists.

Passive entries cannot request focus.

Initial-focus deferral intentionally has no cancel barrier. The new entry becomes the logical owner synchronously. Cancel during the defer routes to it and may close it. Deferred callbacks carry the handle token and no-op after close/replacement.

### 16.4 Restoration

After close and lower-layer restoration, focus restoration is deferred:

1. explicit restore target;
2. captured focus control in captured viewport;
3. parent's initial target;
4. first valid focusable descendant of top entry;
5. appropriate sink if a blocking entry remains;
6. release focus when no UI owner remains.

Native parent presentation/interactivity is restored before acquiring GUI focus inside its viewport. OS window focus restoration does not replace deterministic Control focus restoration.

### 16.5 Restoration barrier

From close mutation start until deferred restoration completes, matching Cancel is consumed as a no-op. This generalizes `_pauseMenuRestorePending`.

Mouse activity may change focus inside the active entry but never overwrites the parent focus record.

## 17. Node lifecycle and cleanup

### 17.1 Control attachment

An unparented Control is attached to its declared host layer. A Control already under that layer may register without reparenting. A Control parented elsewhere is rejected; the host does not silently move arbitrary live subtrees.

### 17.2 Native registration

Native windows remain native viewports and register in a logical visual layer. Adapter state includes presentation, process behavior, `GuiDisableInput`, `Unfocusable`, and focus viewport.

### 17.3 Idempotent close

A second close for the same handle returns `AlreadyClosed` and performs no callback, policy mutation, or node operation.

### 17.4 External deletion

When a registered node exits or becomes invalid outside host close:

1. descendants close first;
2. model entry is removed with `NodeFreed`;
3. managed cleanup is invoked once;
4. operations requiring a live object are skipped when invalid;
5. policy recomputes;
6. focus restores to next valid owner.

Integration cleanup delegates must validate captured Godot objects before dereferencing them.

### 17.5 Teardown

On host `_ExitTree()`:

- `_Input` dispatch is disabled;
- entries close topmost-first with `HostTeardown`;
- callbacks cannot reopen;
- pause, cursor, HUD, Control, process-mode, native-window, and focus snapshots restore once;
- deferred focus callbacks invalidate;
- subscriptions and dynamic sinks are removed;
- externally owned views are not freed.

### 17.6 Re-entrancy

Mutations use a guarded queue:

- current mutation completes before queued mutation;
- closing handle is immediately ineligible for input;
- duplicate queued closes collapse;
- teardown rejects opens;
- effective state publishes only after consistent completion.

## 18. Legacy compatibility and HPA-379 flow contract

HPA-378 supports legacy views through registration; it does not infer domain semantics from concrete types.

### 18.1 Existing cancel synchronization

The host does not edit `InputMap`, synthesize `ui_close_dialog`, or replace HPA-376 synchronization. It coordinates host core actions and leaves native dialog handling to GUI pass-through.

### 18.2 Domain boundary

Outside the host remain:

- save-slot selection, serialization, pending-load handoff;
- staged settings edits and keybinding validation;
- battle timer, result, reward, enemy, and combatant cleanup;
- inventory and equipment mutations;
- NPC outcomes, shop transactions, healing;
- puzzle choices and world-interaction flags;
- scene navigation and quit decisions.

### 18.3 Required integration rows

| Flow | Host entry/lifetime | Cancel/process contract |
|---|---|---|
| Root Pause | Active from popup open until resume/teardown | `PauseTree`, core cancel, always-processing host closes/restores once |
| Inventory | Active while presented; may be Pause child | `toggle_inventory` is entry-scoped; direct pause mutation removed; view processes under host pause |
| Settings | Main Menu or Pause child | popup/capture interceptor reserves; retained direct handler or host-owned close, never both |
| Save/Load | Main Menu or Pause child; confirmations are children | core cancel pass-through or host close; child first; parent restoration exact |
| Error/confirmation | Blocking child | topmost-only; safe action focus; parent unaffected on cancel |
| NPC dialog/shop/heal | Modal entry for visible native window | native GUI pass-through; gameplay block remains until orchestration completes |
| Puzzle riddle | Modal entry for visible native window | configured cancel cleans only riddle; atomic switch/treasure interactions remain external blockers |
| Battle | One host entry remains active for the entire visible Battle window lifetime, including result UI after `IsInBattle` clears | prep/auto cancel retains immediate escape semantics; visible result remains topmost; no Pause fallback behind Continue; handle closes on actual presentation termination, not merely `BattleFinished` |
| Reward toast | Passive entry | no pause, block, focus, cancel, cursor, or HUD change |
| Required reward acknowledgement | Blocking child | generic cancel consumed; Continue closes; producer keeps grant authority |
| Transition | Passive visual-only or explicitly Blocking | policy declared; layer alone has no behavior |

### 18.4 Battle-specific invariant

Battle must not return to a residual root cancel ladder.

- The Battle host handle is created when the Battle presentation opens.
- It remains registered through preparation, automatic combat, and any visible victory/defeat/result presentation.
- `BattleFinished` does not close the handle when the result surface remains visible.
- Preparation/active escape keeps current immediate-close, once-only result, timer, effect, and enemy-retention semantics.
- While the result remains visible after `IsInBattle` clears, the host entry still blocks gameplay and owns/reserves core cancel.
- Continue, native close, or node exit ends presentation and closes/prunes the handle exactly once.
- Defeat delayed navigation remains domain-owned and lifecycle-guarded.

HPA-379 adds physical-input regression tests proving Cancel never opens Pause behind preparation, active Battle, or result Continue.

## 19. Host option examples

### 19.1 Game

```csharp
var options = new UIScreenHostOptions
{
    HudRoot = gameUi,
    CoreCancelActions = new HashSet<StringName>
    {
        "pause_menu",
        "ui_cancel"
    },
    GameplayInputBlockChanged = blocked => _gameplayInputBlocked = blocked,
    RootCancelFallback = context =>
    {
        if (_gameManager.IsInWorldInteraction)
            return UIRootCancelResult.Consumed;

        if (CanOpenPause())
        {
            OpenPauseThroughHost();
            return UIRootCancelResult.Consumed;
        }

        return UIRootCancelResult.Declined;
    }
};
```

The fallback is invoked by `UIScreenHost._Input()` and remains live while Game itself is paused.

### 19.2 Main Menu

```csharp
var options = new UIScreenHostOptions
{
    HudRoot = null,
    CoreCancelActions = new HashSet<StringName>
    {
        "ui_cancel"
    },
    RootCancelFallback = _ => UIRootCancelResult.Declined
};
```

Registered Main Menu child entries still own their cancel policy; the root itself remains a no-op.

## 20. Diagnostics

Debug-only read-only diagnostics expose:

- active handles in logical order;
- canonical kind, parent, layer, priority, and sequence;
- incompatibility and exclusive-group values;
- effective pause/gameplay/cursor/HUD state;
- lower-layer contributors and reduced effect per target;
- core and entry-scoped action ownership;
- current focus viewport/control and sink use;
- process-mode validation state;
- pause/cursor/HUD/Control/native/process snapshot ownership.

Diagnostics never expose mutable internal collections. Public open/close statuses are stable for tests; log text is not an assertion contract.

## 21. Planned source layout

```text
scripts/ui/hosting/
├── UIScreenHost.cs
├── UIScreenHostOptions.cs
├── UIScreenKinds.cs
├── UIScreenEntrySpec.cs
├── UIScreenEntryPolicy.cs
├── UIScreenViewAdapter.cs
├── UIScreenHandle.cs
├── UIScreenStackModel.cs
├── UIScreenPolicyResolver.cs
├── UIScreenFocusCoordinator.cs
├── UIScreenInputDispatcher.cs
├── UIScreenEffectiveState.cs
└── UIScreenResults.cs

scenes/ui/
└── UIScreenHost.tscn

tests/ui/hosting/
├── UIScreenStackModelTest.cs
├── UIScreenPolicyResolverTest.cs
├── UIScreenHostProcessModeTest.cs
├── UIScreenHostTest.cs
├── UIScreenHostInputTest.cs
├── UIScreenHostFocusTest.cs
└── UIScreenHostLifecycleTest.cs

docs/ui/hpa-378/
└── uiscreenhost-contract.md
```

Small enum/result files may consolidate when readability improves. Model, adapter registry, input dispatcher, and focus coordinator must not collapse into one large class.

## 22. Test strategy

### 22.1 Process-mode matrix — first implementation gate

Before broader implementation, runtime tests establish:

| Scenario | Required result |
|---|---|
| Tree unpaused, host active | host receives core cancel |
| Tree paused by host entry | host still receives core cancel |
| Game/root uses normal inherited mode | Game `_Input()` does not need to run while paused |
| Gameplay child inherits root | gameplay processing stops while paused |
| Control entry inherits host | entry remains interactive while paused |
| Explicit Pausable hosted view under pause | registration rejected or adapter safely overrides/restores mode |
| Native AcceptDialog under host pause | native GUI Cancel remains functional through pass-through |
| Last pausing entry closes | exact incoming pause and process states restore |

No implementation phase may assume pause/input correctness until these tests pass.

### 22.2 Pure model tests

No Godot timing is required for:

- push/pop ordering;
- layer-independent input priority;
- parent validation;
- duplicate rejection;
- symmetric incompatibility;
- empty and non-empty exclusive groups;
- child cascade close;
- stale handles;
- policy reduction;
- compositional lower-layer reduction;
- deterministic mutation ordering;
- stable result statuses.

### 22.3 Runtime policy tests

Cover:

- required layer and root focus sink structure;
- Game CanvasLayer-style host composition;
- Control attachment and invalid parentage;
- native Window registration and adapters;
- exact pause restore from running and paused trees;
- exact cursor and HUD restore;
- gameplay block notifications;
- nested lower-layer effect persistence;
- native hidden/inert exact restoration;
- deferred initial focus and stale-callback no-op;
- Control and per-window focus sinks;
- explicit/captured focus restoration across viewports;
- restoration barrier input consumption;
- invalid node pruning;
- idempotent close;
- host teardown;
- callback re-entrancy.

### 22.4 Cancel-surface tests

| Scenario | Expected result |
|---|---|
| `pause_menu` and `ui_cancel` match same event | one logical attempt |
| `ui_close_dialog` also matches pass-through event | native dialog closes once; handle closes once |
| Inventory active + `toggle_inventory` | Inventory closes |
| Settings top + `toggle_inventory` | Settings does not receive it as generic Cancel |
| OptionButton popup | reserve for native popup; Settings remains |
| key capture | reserve/consume according to interceptor |
| blocking error over Pause | error closes; Pause remains |
| restoration pending | matching Cancel consumed; stack unchanged |
| toast above modal | modal remains owner |
| root unpaused and empty | gameplay fallback may open Pause |
| root paused with Pause entry | host closes Pause without Game `_Input()` |
| required acknowledgement | Cancel consumed without close |

### 22.5 Contract scenario tests

Use synthetic views to test:

- Inventory child of Pause: world paused, HUD hidden, Pause policy retained, close returns to Pause;
- Settings child of Pause: child hide effect stacks over Pause's gameplay-inert effect;
- destructive confirmation: safe focus, Cancel returns to parent, no destructive callback;
- reward toast: passive and non-blocking;
- blocking reward acknowledgement: parent inert, per-viewport sink/Continue focus, Cancel ignored;
- Battle result entry remains topmost after synthetic domain flag clears.

These tests do not implement production screens or domain rules.

### 22.6 HPA-379 physical-input integration tests

HPA-379 must additionally cover real scenes and physical events for:

- host `_Input()` while tree paused;
- retained Settings/Inventory handlers during migration;
- OptionButton and key-capture pass-through;
- native AcceptDialog `ui_close_dialog` handling;
- lower-entry handler disablement;
- Battle prep, active, and result cancel;
- no Pause behind NPC, riddle, Save/Load child, error, or Battle result;
- exact restoration after child flows and teardown.

### 22.7 Test isolation

Every runtime test restores:

- `SceneTree.Paused`;
- process modes changed by adapter;
- mouse mode;
- HUD visibility;
- viewport focus and native flags;
- host/view/sink nodes;
- temporary input actions/events;
- orphan-warning settings changed by test.

The full Sirius suite must remain green and HPA-378 must not increase the HPA-376 orphan-warning signature.

## 23. Acceptance criteria mapping

| HPA-378 criterion | Design mechanism |
|---|---|
| HPA-376 matrix implemented | explicit priority, action-surface, and contract tests |
| Entries declare pause/input/cursor/HUD/focus/cancel | `UIScreenEntrySpec` and pure policy projection |
| Topmost cancel and nested modal deterministic | handles, priorities, compositional effects, one terminal owner |
| Pause/input derived centrally | resolver, exact snapshots, Always host input owner |
| Focus restoration and fallback | per-viewport records and host-owned sinks |
| No domain duplication | explicit domain and Battle boundaries |
| Main Menu/Game configurable without fork | same host, different options/fallbacks |
| Lifecycle tests | pure, process, runtime, and physical integration matrices |

## 24. HPA-379 integration contract

HPA-379 begins after the HPA-378 host contract and tests are merged.

For each migrated flow it must:

1. register one explicit entry using canonical kind constants;
2. remove or disable competing direct pause, cancel, cursor, HUD, and process authority;
3. preserve terminal signals and domain cleanup;
4. rely on host `_Input()` rather than forwarding from paused Game;
5. configure core versus entry-scoped cancel actions correctly;
6. provide presentation/interactivity/process/focus adapters where defaults are insufficient;
7. close the handle on actual presentation termination;
8. keep Battle registered while result UI remains visible;
9. retain external atomic world blockers only in domain/root fallback;
10. keep all HPA-376 regressions passing.

HPA-379 must not introduce a second stack, root-specific host fork, or residual cancel ladder for Battle or other migrated flows.

## 25. Resolved decisions

- Host is scene-local, never an autoload.
- Host itself owns always-processing `_Input()`; Game is not made Always.
- Visual layer and input priority are independent.
- Pure model stores values only; Godot state lives in adapters.
- Core cancel family and entry-scoped toggles are separate.
- `ui_close_dialog` remains a native pass-through surface.
- One physical event produces one logical cancel attempt.
- One active entry per kind; no implicit replacement.
- `Replaced` is not a close reason.
- Lower-layer effects compose from all active owners using Hidden > Inert > Interactive.
- Pause, process, cursor, HUD, Control, native-window, and focus state restore exact captured values.
- Initial-focus defer has no barrier; close restoration does.
- Host owns root and per-window focus sinks.
- Passive entries cannot block gameplay.
- Battle remains registered through visible result presentation.
- Domain managers retain gameplay and persistence authority.
- Transition layer has no implicit behavior.

## 26. Completion definition

HPA-378 is complete when:

- process-mode matrix tests prove host input remains alive while gameplay is paused;
- reusable scene and C# host implement the responsibilities above;
- pure and runtime tests cover priority, policy, focus, lifecycle, native windows, cancel surfaces, and nested effect composition;
- `docs/ui/hpa-378/uiscreenhost-contract.md` documents the public integration surface;
- full Sirius test suite passes with no new orphan-warning signature;
- no existing production flow is partially migrated or left with competing ownership;
- HPA-379 can register Main Menu, Pause, Inventory, Settings, Save/Load, Battle, NPC, puzzle, error, confirmation, reward, and transition views without changing host architecture.
