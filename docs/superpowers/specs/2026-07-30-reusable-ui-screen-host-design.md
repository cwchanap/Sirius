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
- topmost-only cancel dispatch;
- initial focus and focus restoration;
- invalid-node pruning and idempotent cleanup.

The host does not own gameplay, save, settings, battle, inventory, NPC, reward, or scene-transition domain state. Existing managers and controllers remain responsible for domain outcomes and terminal signals.

HPA-378 delivers the reusable host, its pure stack/policy model, compatibility seams, automated tests, and contract documentation. HPA-379 integrates the host into `MainMenu.tscn` and `Game.tscn` and removes competing legacy pause, cancel, cursor, visibility, and interactivity authorities only after parity is proven.

## 2. Goals

1. Provide one reusable host implementation for both Main Menu and gameplay roots.
2. Encode the HPA-376 modal-priority matrix as explicit stack and dispatch rules.
3. Require every registered entry to declare visual layer, input priority, pause, input, cursor, HUD, focus, cancel, and cleanup behaviour.
4. Derive effective presentation policy centrally from active entries.
5. Support existing `Control`, `Window`, and `AcceptDialog` presentations without rewriting their domain controllers.
6. Make duplicate requests, invalid nodes, repeated closes, and root teardown deterministic and harmless.
7. Keep the implementation locally owned by the scene root rather than introducing a UI autoload or gameplay-state singleton.
8. Make host policy logic independently testable without constructing the full Game scene or binding live Godot nodes into the stack model.

## 3. Non-goals

HPA-378 does not:

- integrate existing Main Menu or gameplay flows into the host;
- modify `Game.cs`, `MainMenu.cs`, or existing screen controllers;
- redesign or restyle any screen;
- add the production quit confirmation, reward presentation, or scene-transition presenter;
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

The “owning modal or world interaction” row includes two categories. Presentation-backed interactions such as a riddle dialog are host entries. Atomic world interactions with no presentation, such as treasure opening or switch activation, remain external domain blockers and are consulted by the gameplay root fallback rather than represented as synthetic modal entries.

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
└── Godot view adapters
```

### 5.1 Model/adapter boundary

The public host API accepts live Godot objects, but the pure model does not retain them. `UIScreenHost.TryPresent(...)` validates the request and separates it into two runtime records:

```text
UIScreenEntryPolicy
├── kind and generated handle identity
├── visual layer and input priority
├── parent, exclusive group, and incompatibilities
├── pause and gameplay-input block
├── cursor, HUD, and lower-layer policy
└── static cancel policy

UIScreenViewAdapter
├── live Node/Control/Window reference
├── presentation visibility adapter
├── interactivity adapter
├── focus viewport and focus delegates
├── dynamic cancel interceptor
├── cleanup delegate
└── node-lifetime policy
```

`UIScreenStackModel` and `UIScreenPolicyResolver` consume only `UIScreenEntryPolicy`, opaque handles, value enums, and immutable collections. They never inspect a `Node`, `Control`, `Window`, `Viewport`, or callback delegate. `UIScreenHost`, `UIScreenFocusCoordinator`, and the view adapter retain the live Godot references.

This separation provides three benefits:

- priority and policy tests do not depend on frame timing or live scene-tree state;
- Godot node lifecycle code remains small and reviewable;
- future layouts can replace registered views without changing stack semantics.

### 5.2 Rejected alternatives

#### Global UI autoload

A global manager would outlive individual root scenes, require explicit cross-scene reset logic, and risk retaining stale nodes or pause ownership. It also violates the requirement that Main Menu and gameplay each own a local host.

#### One monolithic `UIScreenHost`

Putting stack mutation, policy reduction, Godot node attachment, focus, and input dispatch into one class would recreate the conditional complexity currently concentrated in `Game`. The model and adapters must be testable independently.

#### Mandatory screen interface

Requiring all existing dialogs to implement a new interface would make HPA-379 a broad rewrite. Existing screens instead register through an entry specification and optional view adapters. New screens may adopt a convenience interface later, but the host contract does not require one.

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

`TransitionLayer` reserves deterministic topmost placement for scene-local fades, wipes, and similar transition surfaces. HPA-378 does not add a production transition presenter. A visual-only transition is passive and changes no input policy; a blocking transition must explicitly declare `InputPriority.Blocking`, `BlockGameplayInput = true`, and the appropriate cancel/lower-layer policy. Merely choosing `TransitionLayer` grants no pause, input, or cancel behaviour.

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

- `Passive`: no generic cancel ownership, such as a toast or visual-only transition;
- `Screen`: a parent/full-screen flow;
- `Modal`: an owning presentation-backed modal or interaction flow;
- `Blocking`: a topmost error, confirmation, required acknowledgement, or blocking transition.

`Passive` alone blocks nothing. A passive entry affects gameplay input only when it separately declares `BlockGameplayInput = true`; it still must use `Cancel.None`. Parent-child ancestry outranks this enum: an active child is considered before its parent even if both have the same input priority.

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

    public Func<bool>? IsPresented { get; init; }
    public Action<bool>? SetPresented { get; init; }
    public Action<bool>? SetInteractive { get; init; }
    public Func<Viewport>? FocusViewport { get; init; }

    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }
}
```

Required collections use empty immutable values rather than `null`.

The Godot-facing `UIScreenEntrySpec` is an adapter input, not the object stored by the pure stack model. `UIScreenHost` projects its value fields into `UIScreenEntryPolicy` and stores the live delegates and view reference in `UIScreenViewAdapter`.

Default adapters are inferred for ordinary `Control` and `Window` nodes. A legacy flow supplies explicit `SetPresented`, `SetInteractive`, or `FocusViewport` delegates when its restoration semantics differ from the safe defaults, such as restoring an existing `AcceptDialog` through `PopupCentered()` instead of plain `Show()`.

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

`VisibleInert` keeps lower presentation visible but prevents pointer and direct controller interaction. `Hidden` hides affected lower entries without removing them. Hidden parents remain active and can be restored when a child closes.

For this policy, “lower” means an active `Control` entry in a visually lower layer, or an earlier active entry in the same visual layer. Native windows use their logical visual layer and presentation sequence for the same calculation. Higher visual layers, including passive toasts, are not hidden or disabled merely because a lower modal is active.

The mechanism depends on the view type:

- For `Control`, `Hidden` snapshots and changes `Visible`; `VisibleInert` places an input shield below the owner, transfers focus into the owner, and invokes `SetInteractive(false)` when supplied.
- For `Window` and `AcceptDialog`, `Hidden` snapshots presentation visibility and invokes `SetPresented(false)` or `Hide()`. Restoration invokes the registered `SetPresented(true)` adapter, or the default `Show()` only when that preserves the flow's existing popup semantics.
- For visible inert native windows, the host snapshots the window viewport's `GuiDisableInput` state and the window's `Unfocusable` flag, sets both to prevent GUI and native-window interaction, and restores both exact incoming values later. A Control-layer input shield cannot inert a separate native-window viewport.
- A native window that can be hidden or inerted but cannot use the defaults must provide deterministic presentation/interactivity adapters. The host rejects the affected child open rather than asserting policy it cannot apply safely.

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
- `Consume`: the host consumes Cancel but leaves the entry open.
- `PassThrough`: the root must not run a lower-priority or gameplay fallback, but the event remains unhandled so the entry's native popup, key-capture logic, dialog binding, or retained legacy `_Input()` path can process it.

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

## 8. Host configuration and input contexts

Main Menu and gameplay use the same host class. Root-specific behaviour is supplied through configuration rather than subclasses.

```csharp
public readonly record struct UIInputContext(
    InputEvent Event,
    IReadOnlyList<StringName> MatchedActions,
    UIScreenHandle Candidate,
    UIScreenEffectiveState EffectiveState);

public readonly record struct UIRootCancelContext(
    InputEvent Event,
    IReadOnlyList<StringName> MatchedActions,
    UIScreenEffectiveState EffectiveState);

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
- Game provides its gameplay HUD and a fallback that opens Pause only when no UI entry owns or reserves Cancel and no external domain blocker, such as an atomic world interaction, is active.

### 8.1 Forwarding callback and ordering contract

HPA-379 forwards configured cancel candidates from the owning root's `_Input(InputEvent)` callback, not `_UnhandledInput()`. Godot dispatches `_Input()` before GUI handling, followed by shortcut and unhandled phases. Forwarding in `_Input()` therefore lets the root suppress gameplay fallback while still leaving `ReservedForTopEntry` unhandled for a native popup or `AcceptDialog` to receive during GUI dispatch.

The host itself does not override `_Input()` as its primary contract. The root contains one forwarding path:

```csharp
public override void _Input(InputEvent event)
{
    var result = _uiScreenHost.TryHandleInput(event);
    if (result == UIInputDispatchResult.Consumed)
    {
        GetViewport().SetInputAsHandled();
        return;
    }

    if (result == UIInputDispatchResult.ReservedForTopEntry)
    {
        return; // deliberately unhandled for the registered native owner
    }

    // Continue with genuine non-UI gameplay input only.
}
```

The design does not assume a deterministic relative order among different nodes' `_Input()` callbacks. Instead, HPA-379 must establish one terminal owner per active cancel path:

- A registered legacy controller that retains its own `_Input()` terminal handling uses `Cancel.PassThrough`. Whether that controller runs before or after the root, the host/root does not perform the same terminal action.
- An entry using host-owned `Cancel.Close` or `Cancel.Consume` must have its competing legacy cancel handler disabled or removed before registration becomes active.
- Lower hidden or inert entries with direct `_Input()` handling must be disabled through `SetInteractive(false)` before the new effective stack state is published.
- A native `Window` or `AcceptDialog` using pass-through may rely on later GUI dispatch only when compatibility rules and interactivity adapters ensure no lower or unrelated `_Input()` owner can consume the event first.
- The previous root-specific cancel chain is removed for each migrated flow. Gameplay Pause fallback exists only in `RootCancelFallback`, not as a second branch after host dispatch.

If a top legacy controller consumes the event before the root callback, the root is not called and the intended top owner has already completed the attempt. If it leaves the event unhandled, the root reserves or handles it according to the same registered entry. Correctness therefore comes from eliminating competing terminal authorities, not from assuming sibling callback order.

HPA-378 tests the dispatch contract with synthetic adapters. HPA-379 must add physical-input integration tests covering root forwarding with retained legacy `_Input()` controllers, OptionButton/key-capture pass-through, native `AcceptDialog` GUI close, and lower-entry input disablement.

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
- missing native-window adapter required by the requested policy;
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
- `ReservedForTopEntry`: do not execute gameplay or parent fallback; leave the event unhandled for the registered native child/controller.
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

This distinction prevents the host from becoming a gameplay input manager. External domain blockers with no host presentation remain the responsibility of the root fallback and gameplay controllers.

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

For every affected entry, the host snapshots only the visibility/interactivity values it changes and restores those exact values. Control and native-window state are tracked separately because native windows are independent viewports.

An entry's presentation and interactivity adapters must be idempotent. They may enable or disable view input and visibility, but they must not mutate domain data. When no Control interactivity adapter is supplied, the host still supplies pointer shielding and focus ownership; the entry must not contain independent global `_Input()` behaviour that can act while inert. HPA-379 must provide an adapter for legacy controllers that do.

## 12. Cancel and modal-priority algorithm

The root forwards an event only once to the host. The host checks all configured cancel actions and treats one physical event as one cancel attempt even when the event matches multiple synchronized actions. The matched action names are included in both input-context records.

A non-matching event returns `NoOwner` without invoking the root cancel fallback.

For a matching event, the dispatcher executes:

1. prune invalid entries;
2. if focus restoration is pending, consume as a no-op;
3. inspect active entries in logical input order;
4. for each candidate, invoke `InterceptCancel` when present;
5. resolve the dynamic result before the static `Cancel` policy;
6. stop at the first candidate that consumes, reserves, or closes;
7. if no entry owns or reserves Cancel, invoke the configured root fallback;
8. return `Consumed` when the fallback consumes, otherwise `NoOwner`.

### 12.1 Dynamic interception precedence

```csharp
public enum UIInputInterception
{
    DeferToPolicy,
    ConsumeHere,
    ReserveForNativeHandler
}
```

For each candidate entry:

1. `ConsumeHere` returns `Consumed` without closing the entry.
2. `ReserveForNativeHandler` returns `ReservedForTopEntry` and leaves the event unhandled.
3. `DeferToPolicy`, or no interceptor, evaluates the entry's static `UICancelPolicy`:
   - `None`: continue searching;
   - `Close`: close the candidate and return `Consumed`;
   - `Consume`: return `Consumed` without closing;
   - `PassThrough`: return `ReservedForTopEntry`.

The first non-`None` result wins. The distinct interception names avoid conflating a dynamic short-circuit decision with the entry's static cancel contract.

### 12.2 HPA-376 priority representation

| HPA-376 priority | Host representation |
|---|---|
| Child popup or key capture | top entry interceptor returns `ReserveForNativeHandler` or `ConsumeHere` |
| Blocking error or confirmation | child entry with `InputPriority.Blocking` |
| Deferred Pause restoration | host-level restoration barrier |
| Owning modal or presentation-backed interaction | entry with `InputPriority.Modal` |
| Atomic world interaction without UI | external blocker consulted by gameplay root fallback |
| Parent screen | entry with `InputPriority.Screen` |
| Gameplay fallback | configured root cancel fallback |

### 12.3 Required examples

#### OptionButton popup

Settings' interceptor detects an open popup and returns `ReserveForNativeHandler`. The root skips Pause fallback. Godot's popup receives the event and Settings remains active.

#### Keybinding capture

Settings' interceptor detects active capture and returns `ReserveForNativeHandler` or `ConsumeHere`, according to the existing controller's input surface. The event cannot close Settings or open Pause behind it.

#### Legacy AcceptDialog

The entry uses `Cancel.PassThrough`. The existing synchronized `ui_close_dialog` binding closes the dialog during GUI dispatch. Its existing terminal signal causes the integration adapter to close the host handle exactly once.

#### Required reward acknowledgement

The entry uses `InputPriority.Blocking` and `Cancel.Consume`. Generic Cancel does not close it and cannot reach the parent. An explicit Continue action closes it programmatically.

#### Non-blocking toast

The entry uses `InputPriority.Passive` and `Cancel.None`, does not pause, does not block gameplay, does not request focus, and does not alter cursor or HUD. The dispatcher skips it.

#### Destructive confirmation

The confirmation is a blocking child of the invoking Pause or Save/Load entry. Generic Cancel closes only the confirmation and restores the parent. Only an explicit destructive action performs navigation.

## 13. Focus model

### 13.1 Capture on open

Before attaching or revealing a new entry, the host captures a focus record containing both the focus viewport and its current focused `Control`.

- For a `Control` entry, the default focus viewport is `view.GetViewport()`.
- For a native `Window` or `AcceptDialog`, the default focus viewport is the window itself because each native window is a `Viewport` with its own GUI focus owner.
- Before a child opens, the captured parent focus comes from the active parent's registered focus viewport, not unconditionally from the host root viewport.

The captured record is associated with the new handle and is not replaced by later pointer activity.

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
- belongs to an active and interactive entry;
- belongs to the focus viewport registered for that entry.

Passive entries must not request initial focus.

Initial-focus deferral intentionally does not create a cancel barrier. The entry becomes the logical input owner synchronously when the open mutation commits, so Cancel during the deferred-focus interval still routes to that entry and may close it. Every deferred focus callback carries the handle token and becomes a no-op if the entry closed or was replaced before the callback runs.

### 13.3 Restoration

After an entry closes and its parent becomes visible and interactive, restoration is deferred. Native parent visibility/interactivity is restored before focus acquisition.

The host tries:

1. the entry's explicit `RestoreFocus` target when provided;
2. the focused `Control` captured before the entry opened, in its captured viewport;
3. the active parent's declared initial-focus target;
4. the first valid focusable descendant of the now-top entry;
5. the host focus sink when a blocking Control entry remains;
6. release focus when no UI owner remains.

An explicit restore target supports cases such as Settings returning to Pause's Resume button instead of the invoking Settings button. A transient native window may return OS window focus to its parent automatically, but the host still restores deterministic GUI `Control` focus inside the parent's viewport.

### 13.4 Restoration barrier

From the start of a close mutation until deferred restoration completes, `IsFocusRestorationPending` is true. Cancel during that interval is consumed as a no-op.

This replaces root-specific guards such as `_pauseMenuRestorePending` with a general lifecycle invariant.

### 13.5 Mouse coexistence

Showing the cursor does not call `ReleaseFocus()` and does not hide the focus indicator. Mouse interaction may temporarily move focus within the active entry, but it does not overwrite the focus record captured for parent restoration.

## 14. Node lifecycle and cleanup

### 14.1 Control attachment

An unparented `Control` is attached to its declared host layer. A `Control` already under that same host layer may be registered without reparenting. A `Control` parented elsewhere is rejected; the host does not silently reparent an arbitrary live scene subtree.

This keeps visual ownership explicit. HPA-379 must compose or instantiate migrated controls under the appropriate host layer.

### 14.2 Native-window registration

A native `Window` or `AcceptDialog` remains a native viewport and is registered in a logical visual layer. Its adapter snapshots and restores presentation visibility, `GuiDisableInput`, `Unfocusable`, and focus viewport state when lower-layer policies affect it. The host does not attempt to cover it with a Control-layer shield.

### 14.3 Programmatic close

`TryClose` is idempotent. A second close for the same handle returns `AlreadyClosed` and performs no callback, policy mutation, or node operation.

### 14.4 External node deletion

The host observes registered node exit. When a node leaves the tree or becomes invalid outside a host close:

1. descendants are closed first;
2. the entry is removed with reason `NodeFreed`;
3. the managed cleanup delegate is invoked once;
4. node lifetime operations that require a live object are skipped when invalid;
5. effective policy is recomputed;
6. focus is restored to the next valid owner.

Integration cleanup delegates must validate any captured Godot objects before dereferencing them. Host bookkeeping cleanup always completes even when the view is already freed.

### 14.5 Host teardown

On `_ExitTree()`:

- input dispatch is disabled;
- entries close topmost-first with reason `HostTeardown`;
- callbacks cannot re-open entries;
- pause, cursor, HUD, Control visibility/interactivity, and native-window snapshots are restored once;
- pending deferred focus callbacks are invalidated;
- node-exit subscriptions are removed;
- externally owned views are not freed by the host.

### 14.6 Re-entrancy

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
- missing required native-window adapter;
- malformed specification.

Programming and infrastructure faults produce one clear Godot error with the entry kind and host path, then leave the previous stack and global state unchanged where possible.

Debug-only stack diagnostics expose:

- active handles in logical order;
- parent relationships;
- visual layer and input priority;
- effective policy;
- focus viewport and current focus owner;
- pause/cursor/HUD snapshot ownership;
- Control and native-window presentation/interactivity snapshots.

The diagnostic API is read-only and must not expose mutable internal collections.

## 16. Legacy compatibility boundary

HPA-378 supports legacy views through registration; it does not infer domain semantics from concrete screen types.

### 16.1 Control views

A migrated `Control` is instantiated under or composed into its requested host layer. The integration adapter supplies presentation, cleanup, focus, and interactivity delegates where the controller has direct `_Input()` behaviour or non-default restore semantics.

### 16.2 Window and AcceptDialog views

A native window remains responsible for its popup lifecycle and built-in close input. The host tracks it logically, applies shared pause/cursor/HUD/input policy, and observes its node lifecycle.

For lower-layer policy:

- `Hidden` calls the registered presentation adapter to hide the window and later restore it through the flow-appropriate `Show()` or `Popup*()` path.
- `VisibleInert` sets the window viewport's `GuiDisableInput` and the window's `Unfocusable` flag while preserving visibility.
- exact incoming visibility, input-disable, and unfocusable values are restored when the child closes or the host tears down.

The focus coordinator captures and restores GUI focus through the native window's own viewport. Root-viewport focus is not used as a substitute.

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

The host may block gameplay while presentation-backed flows are active, but it does not own their result. Atomic world-interaction flags with no UI remain external root/gameplay policy inputs.

## 17. Planned source layout

The implementation plan may refine filenames, but responsibility remains divided as follows:

```text
scripts/ui/hosting/
├── UIScreenHost.cs
├── UIScreenHostOptions.cs
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

No live Godot node or scene timing is required for:

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

These tests construct `UIScreenEntryPolicy` values directly and do not instantiate `Node`, `Control`, `Window`, or callback delegates.

### 18.2 Runtime host tests

Runtime tests cover:

- required layer structure, including the reserved Transition layer;
- Control attachment and invalid parentage rejection;
- native Window registration;
- native Window hide/restore through the registered presentation adapter;
- native Window `GuiDisableInput` and `Unfocusable` exact restoration;
- exact pause snapshot and restoration from running and paused trees;
- exact cursor snapshot and restoration;
- exact HUD visibility restoration;
- gameplay block state notifications;
- lower-layer visibility and interactivity;
- deferred initial focus and cancellation before focus acquisition;
- per-viewport initial focus for Control and native Window entries;
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
| dynamic interceptor defers | static cancel policy is evaluated |
| dynamic interceptor consumes | static policy and lower entries are skipped |
| dynamic interceptor reserves | event remains unhandled and fallback is skipped |

Synthetic forwarding tests cover host-owned cancel, retained legacy `_Input()` pass-through, and lower-entry disablement without relying on callback ordering. Physical tests against production legacy controllers and native dialog GUI dispatch belong to HPA-379.

### 18.4 Contract scenarios

The following policies require named tests:

- Inventory child of Pause: tree paused, HUD hidden, cursor visible, Pause inert, close returns to Pause.
- Root Pause: tree paused, gameplay HUD visible but inert, cursor visible, Resume focused, close restores gameplay once.
- Destructive confirmation: safe action focused, generic Cancel returns to parent, no destructive callback.
- Non-blocking reward toast: passive priority, no pause, no focus, no cancel owner, cursor/HUD inherited.
- Blocking reward acknowledgement: parent inert, cursor visible, Continue focused, generic Cancel ignored.
- Visual-only transition: Transition layer, passive priority, no implicit input effect.
- Blocking transition: explicit blocking/input/cancel policy; Transition layer alone is insufficient.

These tests use synthetic views and callbacks. They do not implement production Inventory, Pause, reward, confirmation, or transition screens.

### 18.5 Test isolation

Every runtime test restores:

- `SceneTree.Paused`;
- mouse mode;
- every affected viewport's GUI input and focus state;
- native-window unfocusable and visibility state;
- HUD visibility;
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
| Valid focus restoration and fallback | per-viewport focus coordinator and restoration chain |
| No domain-manager duplication | explicit compatibility and domain boundary |
| Main Menu and gameplay configuration without fork | `UIScreenHostOptions` and root fallback delegate |
| Automated lifecycle tests | pure model and Godot runtime suites |

## 20. HPA-379 integration contract

HPA-379 may begin only after this host contract and tests are merged.

For each migrated flow, HPA-379 must:

1. register one explicit host entry with visual layer and input priority;
2. remove or disable that flow's competing direct pause, cancel, cursor, HUD, visibility, and interactivity authority;
3. preserve the flow's existing terminal signals and domain cleanup;
4. forward configured cancel candidates exactly once from the root's `_Input()` callback;
5. use `Cancel.PassThrough` only when a retained legacy `_Input()` or native GUI owner remains the sole terminal owner;
6. provide `SetInteractive(false)` before a lower legacy controller can act while hidden or inert;
7. provide native-window presentation/focus adapters when default `Hide()`/`Show()` restoration is not behaviourally equivalent;
8. close the host handle when the legacy view terminates externally;
9. make `RootCancelFallback` decline for external domain blockers such as atomic world interactions;
10. add physical-input tests for retained legacy `_Input()`, OptionButton/key capture, native `AcceptDialog`, and lower-entry disablement;
11. keep HPA-376 regression tests passing.

HPA-379 must not introduce a second stack or root-specific fork of `UIScreenHost`.

## 21. Resolved design decisions

The following decisions are final for HPA-378 once this proposal is approved:

- The host is scene-local, never an autoload.
- Visual layer order and input priority are independent and explicitly declared.
- The root forwards cancel candidates from `_Input()`, before GUI dispatch; `_UnhandledInput()` is not the forwarding seam.
- Correctness does not assume relative ordering among sibling `_Input()` callbacks; it requires exactly one terminal owner and disables lower competing handlers.
- One active entry per kind is the only generic duplicate policy.
- Incompatible opens are rejected; the host does not implicitly replace active entries.
- Pause, cursor, HUD, Control state, and native-window state restore exact captured incoming values.
- `PassThrough` reserves an event from parent/gameplay fallback without marking it handled.
- Dynamic interception resolves before static cancel policy with explicit short-circuit semantics.
- Initial-focus deferral permits Cancel and invalidates stale focus callbacks; only close/restoration creates a cancel barrier.
- Deferred focus restoration is a host-level input barrier.
- Native windows use their own viewport for GUI focus and input-disable state.
- Legacy dialogs keep their guarded terminal signals and native close bindings.
- Arbitrarily parented Controls are rejected rather than silently reparented.
- Atomic world interactions without presentation remain external root/gameplay blockers.
- TransitionLayer is a reserved visual surface and grants no implicit lifecycle policy.
- Domain managers retain all gameplay and persistence authority.
- HPA-378 provides capability for reward and destructive-confirmation flows but does not build their production UI.

## 22. Completion definition

HPA-378 is complete when:

- the reusable scene and C# host are implemented with the responsibilities above;
- pure and runtime tests cover the specified priority, policy, focus, native-window, and lifecycle cases;
- `docs/ui/hpa-378/uiscreenhost-contract.md` documents the public integration surface;
- the full Sirius test suite passes with no new orphan-warning signature;
- no existing production flow has been partially migrated or left with competing ownership;
- HPA-379 can register Main Menu and gameplay views without changing the host architecture.
