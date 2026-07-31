# HPA-378 Reusable UI Screen Host Design

**Status:** Approved design
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
- topmost-only cancel dispatch;
- initial focus and focus restoration;
- invalid-node pruning and idempotent cleanup.

The host does not own gameplay, save, settings, battle, inventory, NPC, reward, or scene-transition domain state. Existing managers and controllers remain responsible for domain outcomes and terminal signals.

HPA-378 delivers the reusable host, its pure stack/policy model, compatibility seams, automated tests, and contract documentation. HPA-379 integrates the host into `MainMenu.tscn` and `Game.tscn` and removes competing legacy pause, cancel, and cursor authorities only after parity is proven.

## 2. Goals

1. Provide one reusable host implementation for both Main Menu and gameplay roots.
2. Encode the HPA-376 modal-priority matrix as explicit stack and dispatch rules.
3. Require every registered entry to declare visual layer, input priority, pause, input, cursor, HUD, focus, cancel, and cleanup behaviour.
4. Derive effective presentation policy centrally from active entries.
5. Support existing `Control`, `Window`, and `AcceptDialog` presentations without rewriting their domain controllers.
6. Make duplicate requests, invalid nodes, repeated closes, and root teardown deterministic and harmless.
7. Keep the implementation locally owned by the scene root rather than introducing a UI autoload or gameplay-state singleton.
8. Make host logic independently testable without constructing the full Game scene.

## 3. Non-goals

HPA-378 does not:

- integrate existing Main Menu or gameplay flows into the host;
- modify `Game.cs`, `MainMenu.cs`, or existing screen controllers;
- redesign or restyle any screen;
- add the production quit confirmation or reward presentation;
- replace `GameManager`, `SaveManager`, `SettingsManager`, or other domain managers;
- change save, battle, inventory, settings, NPC, puzzle, or reward rules;
- remove the HPA-376 legacy regression tests;
- introduce cross-scene navigation history;
- introduce a global UI service or autoload.

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

The host must represent these behaviours without embedding their domain actions. For example, it can represent a destructive confirmation whose generic Cancel returns to its parent, but it does not decide whether leaving the current game is safe or perform the scene transition.

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
└── Godot node/layer adapter
```

### 5.1 Why the model is separate

`UIScreenStackModel` owns ordering, parent-child relationships, compatibility, duplicate detection, and effective-policy inputs. It has no dependency on the live scene tree beyond opaque entry identities.

This separation provides three benefits:

- priority and policy tests do not depend on frame timing;
- Godot node lifecycle code remains small and reviewable;
- future layouts can replace registered views without changing stack semantics.

### 5.2 Rejected alternatives

#### Global UI autoload

A global manager would outlive individual root scenes, require explicit cross-scene reset logic, and risk retaining stale nodes or pause ownership. It also violates the requirement that Main Menu and gameplay each own a local host.

#### One monolithic `UIScreenHost`

Putting stack mutation, policy reduction, Godot node attachment, focus, and input dispatch into one class would recreate the conditional complexity currently concentrated in `Game`. The model and adapters must be testable independently.

#### Mandatory screen interface

Requiring all existing dialogs to implement a new interface would make HPA-379 a broad rewrite. Existing screens instead register through an entry specification and optional delegates. New screens may adopt a convenience interface later, but the host contract does not require one.

## 6. Public scene structure

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

`Control` views are attached to the matching layer. Legacy `Window` and `AcceptDialog` views remain native windows and are registered logically with the host; their rendering is not forced into a `Control` layer. When the host owns their parentage, they are direct children of the host so teardown remains local.

All layer node references are validated in `_Ready()`. A malformed host scene fails closed: registration returns an infrastructure error and no global state is mutated.

## 7. Core data model

### 7.1 Identity

Each active entry has:

- a required stable `Kind` (`StringName`) identifying the presentation type;
- a generated instance token contained in `UIScreenHandle`;
- an optional parent handle;
- a monotonic presentation sequence number.

The generated token prevents a stale handle from closing a later instance of the same kind.

Only one active entry of a kind is allowed. A duplicate open is rejected without changing focus, pause, cursor, HUD, node parentage, or stack order. HPA-378 does not add a generic multi-instance mode. A downstream toast presenter queues or coalesces payloads and presents at most one host entry for that toast kind at a time.

### 7.2 Visual layer

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

### 7.3 Input priority

```csharp
public enum UIInputPriority
{
    Passive,
    Screen,
    Modal,
    Blocking
}
```

Input priority is independent from visual layer:

- `Passive`: no generic cancel ownership, such as a toast or non-interactive transition;
- `Screen`: a parent/full-screen flow;
- `Modal`: an owning modal or interaction flow;
- `Blocking`: a topmost error, confirmation, or required acknowledgement.

An entry with `InputPriority.Passive` must use `Cancel.None`. It may still block gameplay during a non-interactive transition.

Parent-child ancestry outranks this enum: an active child is considered before its parent even if both have the same input priority.

### 7.4 Entry specification

The implementation may adjust exact C# names, but every behaviour below is part of the contract.

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

    public Func<Control?>? InitialFocus { get; init; }
    public Func<Control?>? RestoreFocus { get; init; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; init; }

    public Action<bool>? SetInteractive { get; init; }
    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }
}
```

Required collections use empty immutable values rather than `null`.

### 7.5 Cursor and HUD policies

Cursor and HUD policies are tri-state because some presentations preserve the current root state while others override it.

```csharp
public enum UICursorPolicy
{
    Inherit,
    Visible,
    Hidden
}

public enum UIHudPolicy
{
    Inherit,
    Visible,
    Hidden
}
```

Examples:

- a reward toast uses `Inherit` for both;
- Pause uses visible cursor and visible HUD;
- Inventory from Pause uses visible cursor and hidden HUD;
- a full-screen blocking reward may use visible cursor and hidden HUD.

### 7.6 Lower-layer policy

```csharp
public enum UILowerLayerPolicy
{
    VisibleInteractive,
    VisibleInert,
    Hidden
}
```

`VisibleInteractive` is valid for non-owning overlays such as toasts.

`VisibleInert` keeps lower presentation visible but prevents pointer and direct controller interaction. The host places an input shield below the owning entry, transfers focus into the owning entry, and calls each affected entry's `SetInteractive(false)` adapter when provided.

`Hidden` hides affected lower entries without removing them. Hidden parents remain active and can be restored when a child closes.

For this policy, “lower” means an active `Control` entry in a visually lower layer, or an earlier active entry in the same visual layer. Native windows use their logical visual layer and presentation sequence for the same calculation. Higher visual layers, including passive toasts, are not hidden or disabled merely because a lower modal is active.

The host records every visibility or interactivity change it applies and restores the exact prior host-managed value. It never rewrites a screen's domain state.

### 7.7 Cancel policy

```csharp
public enum UICancelPolicy
{
    None,
    Close,
    Consume,
    PassThrough
}
```

- `None`: this entry is skipped while searching for a cancel owner.
- `Close`: the host closes this entry with reason `Cancel`.
- `Consume`: the host reserves and consumes Cancel but leaves the entry open.
- `PassThrough`: the root must not run a lower-priority or gameplay fallback, but the event remains unhandled so the entry's native popup, key-capture logic, or dialog binding can process it.

`PassThrough` is essential for existing `OptionButton`, keybinding capture, direct `ui_cancel` controllers, and `AcceptDialog`/`ui_close_dialog` behaviour.

### 7.8 Node lifetime

```csharp
public enum UINodeLifetime
{
    External,
    Hide,
    QueueFree
}
```

- `External`: the caller owns view disposal; the host removes only its registration.
- `Hide`: the host hides the view on terminal close.
- `QueueFree`: the host queues the view for deletion after cleanup.

Hidden parent entries are not terminally closed and therefore do not apply `NodeLifetime` until their own handle closes.

### 7.9 Effective state

```csharp
public sealed record UIScreenEffectiveState(
    bool IsTreePauseOwned,
    bool IsGameplayInputBlocked,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    bool IsFocusRestorationPending);
```

The host exposes the current value and emits `EffectiveStateChanged` only when the effective value changes.

## 8. Host configuration

Main Menu and gameplay use the same host class. Root-specific behaviour is supplied through configuration rather than subclasses.

```csharp
public enum UIRootCancelResult
{
    Declined,
    Consumed
}

public sealed record UIScreenHostOptions
{
    public Control? HudRoot { get; init; }
    public IReadOnlyList<StringName> CancelActions { get; init; }
    public Func<UIRootCancelContext, UIRootCancelResult>? RootCancelFallback { get; init; }
    public Action<bool>? GameplayInputBlockChanged { get; init; }
}
```

Expected configuration:

- Main Menu may provide no HUD root and no root cancel fallback.
- Game provides its gameplay HUD and a fallback that opens Pause only when no UI entry owns or reserves Cancel.

The host does not override `_Input()` as its primary contract. The owning root forwards candidate input to `TryHandleInput(InputEvent)`. This avoids scene-tree callback-order races between `Game`, the host, legacy controllers, and native windows.

HPA-379 may add a thin `_Input()` forwarding method to each root. There must be only one forwarding path per host instance.

## 9. Presentation API

```csharp
public UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec);
public UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason);
public bool IsActive(UIScreenHandle handle);
public bool IsKindActive(StringName kind);
public UIInputDispatchResult TryHandleInput(InputEvent inputEvent);
```

### 9.1 Open result

`UIScreenOpenResult` distinguishes:

- opened with a new handle;
- duplicate kind;
- incompatible active entry;
- exclusive-group conflict;
- invalid or freed node;
- invalid or inactive parent;
- node already registered by this host;
- node owned by another host;
- invalid parentage for a `Control` view;
- malformed host scene;
- invalid entry specification.

A rejected open is a strict no-op.

### 9.2 Close reasons

```csharp
public enum UIScreenCloseReason
{
    Cancel,
    ExplicitAction,
    Programmatic,
    Replaced,
    NodeFreed,
    ParentClosed,
    HostTeardown
}
```

The reason is delivered to cleanup at most once.

### 9.3 Input dispatch result

```csharp
public enum UIInputDispatchResult
{
    NoOwner,
    Consumed,
    ReservedForTopEntry
}
```

Root behaviour:

- `Consumed`: call `GetViewport().SetInputAsHandled()` and stop.
- `ReservedForTopEntry`: do not execute gameplay or parent fallback; leave the event unhandled for the native child/controller.
- `NoOwner`: the event was not a configured cancel candidate, or no UI entry/root fallback claimed it; the root may continue with genuine gameplay input.

This result prevents the current failure mode where an event is intentionally left for a popup but accidentally opens Pause behind it.

## 10. Stack and compatibility semantics

### 10.1 Ordering

Entries have two independent orderings:

- visual order: visual layer rank, then presentation sequence;
- logical input order: active child before ancestor, then `Blocking` before `Modal` before `Screen` before `Passive`, then newest presentation sequence.

A child always outranks its parent for cancel and focus ownership even when both use the same input priority.

Passive entries do not become cancel owners merely because they render above a modal. A toast in `ToastLayer` therefore does not displace a modal's cancel ownership.

Unrelated input-owning entries should normally be prevented by compatibility or exclusive groups. The explicit priority rule still makes behaviour deterministic if compatible entries coexist.

### 10.2 Parent-child relationships

- A child requires an active parent handle.
- A parent can have multiple sequential children, but only compatible children may remain active together.
- Closing a child restores only its invoking parent or the next valid active ancestor.
- Closing a parent closes descendants from topmost to lowest before closing the parent.
- Child cleanup runs before parent cleanup.
- A child cannot outlive an invalid or closed parent.
- Hidden or inert parents remain registered and retain their domain state.

### 10.3 Compatibility

An open is rejected when any of the following is true:

- the same `Kind` is already active;
- an active entry names the new kind as incompatible;
- the new entry names an active kind as incompatible;
- both entries use the same non-empty exclusive group and neither is an ancestor of the other;
- the requested parent is invalid or not active.

Compatibility is symmetric at evaluation time even if only one side declares the conflict. This avoids order-dependent results.

### 10.4 No implicit replacement

The host never closes an active entry merely because a new incompatible entry was requested. The caller must close the existing entry explicitly before presenting its replacement. This prevents duplicate requests from discarding staged edits or domain state.

## 11. Effective policy derivation

Policy is recomputed after every successful open, close, prune, child cascade, and teardown mutation.

### 11.1 Pause ownership

Effective pause is true when any active entry requests `PauseTree`.

The host uses exact snapshot ownership:

1. When effective pause changes from false to true, capture the current `SceneTree.Paused` value.
2. Set `SceneTree.Paused = true`.
3. Keep the snapshot unchanged while one or more pausing entries remain.
4. When effective pause changes from true to false, restore the captured value exactly once.
5. On host teardown, restore the captured value if pause is still owned.

This preserves an already-paused parent and matches the inventory pause-restoration contract from HPA-376.

HPA-379 must remove direct UI-owned pause mutations from each migrated flow before that flow registers `PauseTree`. Two independent pause authorities for one flow are not permitted.

### 11.2 Gameplay-input blocking

Effective gameplay-input blocking is the logical OR of `BlockGameplayInput` across active entries.

The host exposes this value; it does not consume every gameplay action globally. The root and gameplay controllers use the effective state to suppress genuine gameplay input. Cancel continues through the dedicated host dispatcher.

This distinction prevents the host from becoming a gameplay input manager.

### 11.3 Cursor

The effective cursor policy is the highest logical-priority active entry whose cursor value is not `Inherit`.

When the first explicit cursor override becomes effective, the host captures the exact current mouse mode. When no active explicit override remains, it restores that captured mode exactly once.

Changing cursor mode must not release keyboard/gamepad focus.

### 11.4 HUD

The effective HUD policy is the highest logical-priority active entry whose HUD value is not `Inherit`.

- `Visible` shows the configured HUD root.
- `Hidden` hides it.
- no explicit override restores the root's captured visibility.

The host captures the HUD root's incoming visibility before applying the first override and restores it after the last override. It does not assume gameplay HUDs start visible. When `HudRoot` is `null`, a non-`Inherit` HUD policy is rejected as an invalid specification for that host.

### 11.5 Lower layers

The topmost active entry whose lower-layer policy differs from `VisibleInteractive` controls the visually lower entries described in section 7.6.

For every affected lower entry, the host records whether it changed visibility or interactivity. Restoration returns the exact prior host-managed state rather than assuming visible and interactive defaults.

An entry's `SetInteractive` adapter must be idempotent. It may enable or disable that view's direct input handling, but it must not mutate domain data. When no adapter is supplied, the host still supplies pointer shielding and focus ownership; the entry must not contain independent global `_Input()` behaviour that can act while inert. HPA-379 must provide an adapter for legacy controllers that do.

## 12. Cancel and modal-priority algorithm

The root forwards an event only once to the host. The host checks all configured cancel actions and treats one physical event as one cancel attempt even when the event matches multiple synchronized actions.

A non-matching event returns `NoOwner` without invoking the root cancel fallback.

For a matching event, the dispatcher executes:

1. prune invalid entries;
2. if focus restoration is pending, consume as a no-op;
3. inspect active entries in logical input order;
4. for each applicable entry, invoke `InterceptCancel` when present;
5. apply the first resulting owner policy;
6. if no entry owns or reserves Cancel, invoke the configured root fallback;
7. return `Consumed` when the fallback consumes, otherwise `NoOwner`.

### 12.1 Interception results

```csharp
public enum UIInputInterception
{
    ContinuePolicy,
    Consume,
    PassThrough
}
```

`Consume` and `PassThrough` stop the stack search. They never fall through to a parent or gameplay in the same event.

### 12.2 HPA-376 priority representation

| HPA-376 priority | Host representation |
|---|---|
| Child popup or key capture | top entry interceptor returns `PassThrough` or `Consume` |
| Blocking error or confirmation | child entry with `InputPriority.Blocking` |
| Deferred Pause restoration | host-level restoration barrier |
| Owning modal or interaction | entry with `InputPriority.Modal` |
| Parent screen | entry with `InputPriority.Screen` |
| Gameplay fallback | configured root cancel fallback |

### 12.3 Required examples

#### OptionButton popup

Settings' interceptor detects an open popup and returns `PassThrough`. The root skips Pause fallback. Godot's popup receives the event and Settings remains active.

#### Keybinding capture

Settings' interceptor detects active capture and returns `PassThrough` or `Consume`, according to the existing controller's input surface. The event cannot close Settings or open Pause behind it.

#### Legacy AcceptDialog

The entry uses `PassThrough`. The existing synchronized `ui_close_dialog` binding closes the dialog. Its existing terminal signal causes the integration adapter to close the host handle exactly once.

#### Required reward acknowledgement

The entry uses `InputPriority.Blocking` and `Cancel.Consume`. Generic Cancel does not close it and cannot reach the parent. An explicit Continue action closes it programmatically.

#### Non-blocking toast

The entry uses `InputPriority.Passive` and `Cancel.None`, does not pause, does not block gameplay, does not request focus, and does not alter cursor or HUD. The dispatcher skips it.

#### Destructive confirmation

The confirmation is a blocking child of the invoking Pause or Save/Load entry. Generic Cancel closes only the confirmation and restores the parent. Only an explicit destructive action performs navigation.

## 13. Focus model

### 13.1 Capture on open

Before attaching or revealing a new entry, the host captures the viewport's current focus owner. The captured reference is associated with the new handle and is not replaced by later pointer activity.

### 13.2 Initial focus

After the view is in-tree, ready, visible, and interactive, focus acquisition is deferred to a later frame.

The host tries, in order:

1. the entry's declared `InitialFocus` target;
2. the first valid focusable descendant of the entry view;
3. a host-owned focus sink when the entry blocks input but has no focusable control.

A candidate is valid only when it:

- is a live `Control`;
- is inside the host's active presentation tree or native registered window;
- is visible in tree;
- accepts focus;
- is not disabled by its control type;
- belongs to an active and interactive entry.

Passive entries must not request initial focus.

### 13.3 Restoration

After an entry closes and its parent becomes visible and interactive, restoration is deferred. The host tries:

1. the entry's explicit `RestoreFocus` target when provided;
2. the focus owner captured before the entry opened;
3. the active parent's declared initial-focus target;
4. the first valid focusable descendant of the now-top entry;
5. the host focus sink when a blocking entry remains;
6. release focus when no UI owner remains.

An explicit restore target supports cases such as Settings returning to Pause's Resume button instead of the invoking Settings button.

### 13.4 Restoration barrier

From the start of a close mutation until deferred restoration completes, `IsFocusRestorationPending` is true. Cancel during that interval is consumed as a no-op.

This replaces root-specific guards such as `_pauseMenuRestorePending` with a general lifecycle invariant.

### 13.5 Mouse coexistence

Showing the cursor does not call `ReleaseFocus()` and does not hide the focus indicator. Mouse interaction may temporarily move focus within the active entry, but it does not overwrite the focus target captured for parent restoration.

## 14. Node lifecycle and cleanup

### 14.1 Control attachment

An unparented `Control` is attached to its declared host layer. A `Control` already under that same host layer may be registered without reparenting. A `Control` parented elsewhere is rejected; the host does not silently reparent an arbitrary live scene subtree.

This keeps visual ownership explicit. HPA-379 must compose or instantiate migrated controls under the appropriate host layer.

### 14.2 Programmatic close

`TryClose` is idempotent. A second close for the same handle returns `AlreadyClosed` and performs no callback, policy mutation, or node operation.

### 14.3 External node deletion

The host observes registered node exit. When a node leaves the tree or becomes invalid outside a host close:

1. descendants are closed first;
2. the entry is removed with reason `NodeFreed`;
3. the managed cleanup delegate is invoked once;
4. node lifetime operations that require a live object are skipped when invalid;
5. effective policy is recomputed;
6. focus is restored to the next valid owner.

Integration cleanup delegates must validate any captured Godot objects before dereferencing them. Host bookkeeping cleanup always completes even when the view is already freed.

### 14.4 Host teardown

On `_ExitTree()`:

- input dispatch is disabled;
- entries close topmost-first with reason `HostTeardown`;
- callbacks cannot re-open entries;
- pause, cursor, and HUD snapshots are restored once;
- pending deferred focus callbacks are invalidated;
- node-exit subscriptions are removed;
- externally owned views are not freed by the host.

### 14.5 Re-entrancy

Cleanup callbacks and node signals may request another close. Stack mutations therefore run through a guarded mutation queue.

Rules:

- the current mutation completes before the next queued mutation;
- a handle marked closing is no longer eligible for input dispatch;
- duplicate queued closes collapse to one terminal close;
- opens requested during host teardown are rejected;
- policy is published only from a consistent post-mutation state.

## 15. Error handling and diagnostics

Expected caller errors return structured results rather than throwing:

- duplicate open;
- incompatibility;
- stale handle;
- invalid parent;
- invalid node;
- node already registered;
- invalid Control parentage;
- malformed specification.

Programming and infrastructure faults produce one clear Godot error with the entry kind and host path, then leave the previous stack and global state unchanged where possible.

Debug-only stack diagnostics expose:

- active handles in logical order;
- parent relationships;
- visual layer and input priority;
- effective policy;
- current focus owner;
- pause/cursor/HUD snapshot ownership.

The diagnostic API is read-only and must not expose mutable internal collections.

## 16. Legacy compatibility boundary

HPA-378 supports legacy views through registration; it does not infer domain semantics from concrete screen types.

### 16.1 Control views

A migrated `Control` is instantiated under or composed into its requested host layer. The integration adapter supplies cleanup and interactivity delegates where the controller has direct `_Input()` behaviour.

### 16.2 Window and AcceptDialog views

A native window remains responsible for its popup lifecycle and built-in close input. The host tracks it logically, applies shared pause/cursor/HUD/input policy, and observes its node lifecycle.

When a dialog emits its guarded terminal signal, the caller closes the matching host handle. The host does not emit or reinterpret domain terminal signals.

### 16.3 Existing cancel synchronization

The host does not edit `InputMap`, synthesize `ui_close_dialog`, or replace HPA-376's configured Cancel synchronization. It treats configured cancel actions as candidate surfaces and dispatches one logical cancel attempt per physical event.

### 16.4 Domain managers

The following remain outside the host:

- save-slot selection, serialization, and pending-load handoff;
- staged settings edits and keybinding validation;
- battle timer, result, reward, and combatant cleanup;
- inventory and equipment mutations;
- NPC dialogue outcomes, shop transactions, and healing;
- puzzle choices and world-interaction flags;
- scene navigation and quit decisions.

The host may block gameplay while these flows are presented, but it does not own their result.

## 17. Planned source layout

The implementation plan may refine filenames, but responsibility remains divided as follows:

```text
scripts/ui/hosting/
├── UIScreenHost.cs
├── UIScreenHostOptions.cs
├── UIScreenEntrySpec.cs
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
├── UIScreenHostTest.cs
├── UIScreenHostInputTest.cs
├── UIScreenHostFocusTest.cs
└── UIScreenHostLifecycleTest.cs

docs/ui/hpa-378/
└── uiscreenhost-contract.md
```

Small result and enum types may be consolidated when that improves readability. The model, Godot adapter, input dispatcher, and focus coordinator must not be collapsed into one large file.

## 18. Test strategy

### 18.1 Pure model tests

No Godot scene timing is required for:

- push/pop ordering;
- visual-layer-independent input priority;
- parent-child validation;
- duplicate rejection;
- symmetric incompatibility;
- exclusive groups;
- child cascade close;
- stale handle rejection;
- policy reduction;
- deterministic mutation ordering.

### 18.2 Runtime host tests

Runtime tests cover:

- required layer structure;
- Control attachment and invalid parentage rejection;
- native Window registration;
- exact pause snapshot and restoration from running and paused trees;
- exact cursor snapshot and restoration;
- exact HUD visibility restoration;
- gameplay block state notifications;
- lower-layer visibility and interactivity;
- deferred initial focus;
- explicit and captured focus restoration;
- fallback for hidden, disabled, or freed controls;
- restoration barrier input consumption;
- invalid node pruning;
- programmatic and repeated close;
- host teardown with active entries;
- callback re-entrancy.

### 18.3 Input-priority tests

Tests reproduce the HPA-376 matrix directly:

| Scenario | Expected result |
|---|---|
| child popup over parent | child reserves Cancel; parent remains |
| key capture | capture receives event; Settings remains |
| blocking error over Pause | error closes; Pause remains visible |
| restoration pending | Cancel consumed; stack unchanged |
| owning modal | modal receives exactly one cancel attempt |
| parent screen | parent receives Cancel only when no child applies |
| empty gameplay host | root fallback opens Pause once |
| empty Main Menu host | root fallback declines; root remains |
| toast above modal | modal remains cancel owner |
| required acknowledgement | Cancel consumed without close |
| unrelated modal and blocking entry | blocking entry wins despite visual layer |

### 18.4 Contract scenarios

The following policies require named tests:

- Inventory child of Pause: tree paused, HUD hidden, cursor visible, Pause inert, close returns to Pause.
- Root Pause: tree paused, gameplay HUD visible but inert, cursor visible, Resume focused, close restores gameplay once.
- Destructive confirmation: safe action focused, generic Cancel returns to parent, no destructive callback.
- Non-blocking reward toast: passive priority, no pause, no focus, no cancel owner, cursor/HUD inherited.
- Blocking reward acknowledgement: parent inert, cursor visible, Continue focused, generic Cancel ignored.

These tests use synthetic views and callbacks. They do not implement production Inventory, Pause, reward, or confirmation screens.

### 18.5 Test isolation

Every runtime test restores:

- `SceneTree.Paused`;
- mouse mode;
- viewport focus;
- host and view nodes;
- any temporary input actions or events;
- orphan-warning settings changed by the test.

The full existing solution suite must remain green. HPA-378 must not increase the HPA-376 baseline orphan-warning signature.

## 19. Acceptance criteria mapping

| HPA-378 acceptance criterion | Design mechanism |
|---|---|
| Implements HPA-376 lifecycle/state matrix | explicit six-level dispatcher and contract tests |
| Entries declare pause, input, cursor, HUD, focus, cancel | required `UIScreenEntrySpec` behaviour fields |
| Deterministic topmost cancel and nested modals | parent handles, input priority, one dispatch result |
| Central effective pause/input state | policy resolver and snapshot ownership |
| Valid focus restoration and fallback | focus coordinator and restoration chain |
| No domain-manager duplication | explicit compatibility and domain boundary |
| Main Menu and gameplay configuration without fork | `UIScreenHostOptions` and root fallback delegate |
| Automated lifecycle tests | pure model and Godot runtime suites |

## 20. HPA-379 integration contract

HPA-379 may begin only after this host contract and tests are merged.

For each migrated flow, HPA-379 must:

1. register one explicit host entry with visual layer and input priority;
2. remove or disable that flow's competing direct pause, cancel, cursor, and HUD authority;
3. preserve the flow's existing terminal signals and domain cleanup;
4. forward root cancel input exactly once;
5. provide a `SetInteractive` adapter when the legacy controller processes direct input while hidden or inert;
6. close the host handle when the legacy view terminates externally;
7. keep HPA-376 regression tests passing.

HPA-379 must not introduce a second stack or root-specific fork of `UIScreenHost`.

## 21. Resolved design decisions

The following decisions are final for HPA-378:

- The host is scene-local, never an autoload.
- Visual layer order and input priority are independent and explicitly declared.
- Roots forward cancel candidates; the host does not depend on `_Input()` callback order.
- One active entry per kind is the only generic duplicate policy.
- Incompatible opens are rejected; the host does not implicitly replace active entries.
- Pause, cursor, and HUD restore exact captured incoming values.
- `PassThrough` reserves an event from parent/gameplay fallback without marking it handled.
- Deferred focus restoration is a host-level input barrier.
- Legacy dialogs keep their guarded terminal signals and native close bindings.
- Arbitrarily parented Controls are rejected rather than silently reparented.
- Domain managers retain all gameplay and persistence authority.
- HPA-378 provides capability for reward and destructive-confirmation flows but does not build their production UI.

## 22. Completion definition

HPA-378 is complete when:

- the reusable scene and C# host are implemented with the responsibilities above;
- pure and runtime tests cover the specified priority, policy, focus, and lifecycle cases;
- `docs/ui/hpa-378/uiscreenhost-contract.md` documents the public integration surface;
- the full Sirius test suite passes with no new orphan-warning signature;
- no existing production flow has been partially migrated or left with competing ownership;
- HPA-379 can register Main Menu and gameplay views without changing the host architecture.
