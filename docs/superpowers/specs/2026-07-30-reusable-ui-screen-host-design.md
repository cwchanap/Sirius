# HPA-378 Reusable UI Screen Host Design

**Status:** Approved design
**Date:** 2026-07-30  
**Issue:** HPA-378  
**Repository:** `cwchanap/Sirius`  
**Runtime:** Godot 4.6, C#/.NET 8, GdUnit4  
**Depends on:** HPA-376  
**Downstream integration:** HPA-379

## 1. Summary

Sirius currently coordinates UI ownership through root-specific fields, direct scene-tree pause changes, domain flags, and priority-ordered conditionals in `Game._Input()`. HPA-376 documented and protected those behaviours with a 50-flow lifecycle contract. HPA-378 introduces a reusable, scene-local `UIScreenHost` that represents the same ordering and lifecycle rules explicitly.

The host owns presentation state only for the root scene in which it is instantiated. It manages:

- visual placement for Control-based UI;
- logical ordering for Control and Window presentations;
- parent-child presentation relationships;
- compatibility and duplicate-open rejection;
- effective tree-pause ownership;
- presentation-derived gameplay-input blocking;
- cursor and HUD policy;
- cancel dispatch across host, retained controller, and embedded-dialog surfaces;
- initial focus and deterministic focus restoration;
- process-mode validation for hosted presentation nodes;
- invalid-node pruning and idempotent cleanup.

The host does not own gameplay, save, settings, battle, inventory, NPC, reward, world-interaction, or scene-transition domain state. Existing managers and controllers remain responsible for domain outcomes and terminal signals.

This document is the proposed design artifact reviewed in PR #20. Subsequent HPA-378 implementation work delivers the reusable host, pure model, Godot adapters, tests, and public contract. HPA-379 then integrates the host into `MainMenu.tscn` and `Game.tscn`, audits the real scene's process modes, and removes competing legacy pause, cancel, cursor, HUD, and presentation-input authorities only after parity is proven.

## 2. Goals

1. Provide one reusable host implementation for both Main Menu and gameplay roots.
2. Encode the HPA-376 modal-priority matrix as explicit stack and dispatch rules.
3. Require every registered entry to declare visual layer, input priority, pause, gameplay block, cursor, HUD, lower-layer, focus, cancel, process, and cleanup behaviour.
4. Derive effective presentation policy centrally from active entries.
5. Support existing `Control`, embedded `Window`, and embedded `AcceptDialog` presentations without rewriting their domain controllers.
6. Keep cancel dispatch alive while the host owns `SceneTree.Paused` without making the gameplay root process while paused.
7. Prevent host-owned pause from producing a half-paused world through an explicit runtime process-mode audit.
8. Make duplicate requests, invalid nodes, repeated closes, dropped deferred callbacks, and root teardown deterministic and harmless.
9. Keep implementation locally owned by the scene root rather than introducing a UI autoload or gameplay-state singleton.
10. Make stack and policy logic independently testable without constructing the full Game scene.

## 3. Non-goals

HPA-378 does not:

- integrate existing Main Menu or gameplay flows into the host;
- modify `Game.cs`, `MainMenu.cs`, floor scenes, or existing production screen controllers;
- redesign or restyle any screen;
- add the production quit confirmation or reward presentation;
- replace `GameManager`, `SaveManager`, `SettingsManager`, or other domain managers;
- change save, battle, inventory, settings, NPC, puzzle, or reward rules;
- remove the HPA-376 legacy regression tests;
- introduce cross-scene navigation history;
- introduce a global UI service or autoload;
- support detached native operating-system subwindows;
- add implicit replacement, unrestricted multi-instance screen kinds, or general navigation history.

Production scene integration, including the runtime GridMap process-mode correction required before root Pause starts pausing the tree, belongs to HPA-379.

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

- configured core actions such as `pause_menu` and `ui_cancel`;
- flow toggles such as `toggle_inventory`;
- native `ui_close_dialog` GUI handling;
- `Window.CloseRequested`, `Canceled`, and guarded terminal signals;
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
    public required UIProcessPolicy ProcessPolicy { get; init; }

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

## 6. Scene composition, rendering, and subwindow precondition

### 6.1 Public host scene

`UIScreenHost.tscn` is a full-rect `Control` with `ProcessMode.Always`.

```text
UIScreenHost
├── HUDLayer
├── ScreenLayer
├── ModalLayer
├── ToastLayer
└── TransitionLayer
```

For `Control` entries, the containers have a fixed visual order. Input priority is independent and explicitly declared.

Embedded `Window` and `AcceptDialog` entries are exempt from the CanvasItem draw order. They render as embedded subwindows above ordinary CanvasLayer/Control content. Their registered `UIScreenLayer` is used for logical ordering, compatibility, lower-layer policy, diagnostics, and restoration—not to promise interleaving with Control layers.

Detached OS windows are unsupported by this design because the root host's input callback does not own events targeted at another native window. See section 6.5.

All required host scene references are validated in `_Ready()`. A malformed host scene fails closed: registration returns a stable infrastructure status and no global state is mutated.

### 6.2 Game placement

HPA-379 places the host under the existing gameplay `CanvasLayer`, not under the world `Node2D` hierarchy as an inheriting gameplay child:

```text
Game : Node2D                           # remains inherited/pausable
└── UI : CanvasLayer
    └── UIScreenHost : Control          # Always
        ├── HUDLayer : Control          # explicitly Pausable
        │   └── GameUI                  # visible but processing stops on pause
        ├── ScreenLayer : Control       # Always
        ├── ModalLayer : Control        # Always
        ├── ToastLayer : Control        # Always
        └── TransitionLayer : Control   # Always
```

This preserves one CanvasLayer domain for HUD and Control-based overlays. Existing draggable HUD content moves under `HUDLayer`.

`HUDLayer` is explicitly `Pausable`, not inherited from the Always host. Rendering remains visible when policy says the HUD is visible, but HUD `_Process`, inherited timers, tweens, and direct input do not silently continue through pause. A specific HUD child that must animate while paused requires an explicit reviewed override and a named test.

### 6.3 Existing explicit-Always world nodes

The statement “gameplay children inherit the Game root and stop” is not universally true in the current repository. Every current floor scene explicitly sets its `GridMap` node to `ProcessMode.Always`, and `GridMap._Process()` advances runtime animation outside `Engine.IsEditorHint()`.

Before HPA-379 enables `PauseTree` for root Pause, it must perform a real-scene process-mode audit and classify every explicit or effective Always node under the world hierarchy.

The current GridMap decision is explicit:

- editor preview may remain Always while `Engine.IsEditorHint()` is true;
- runtime GridMap processing must be `Inherit` or `Pausable` before host-owned root Pause is enabled;
- the floor scenes must no longer force runtime GridMap processing to Always;
- a runtime test must prove `GridMap.CanProcess()` is false while the host owns pause;
- an editor/tool test or focused characterization must preserve the intended editor preview behavior.

Any future Always world-node exception requires a documented reason, an owner, and a test proving it cannot mutate gameplay or advance world presentation unexpectedly while paused.

### 6.4 Main Menu placement

HPA-379 places one host under the Main Menu root and composes existing root content into the screen layer. Main Menu uses the same host implementation but normally never requests tree pause.

### 6.5 Embedded-subwindow precondition

HPA-378 supports registered `Window` and `AcceptDialog` views only when the root viewport embeds subwindows.

The project currently does not explicitly pin this setting, so HPA-379 must set `display/window/subwindows/embed_subwindows=true` or configure the root viewport equivalently before any legacy Window is registered.

Contract:

- `UIScreenHost` validates the root viewport's subwindow embedding state before accepting a Window entry;
- if embedding is disabled, Control-only hosting remains available but Window registration returns `UnsupportedSubwindowMode` with no stack mutation;
- HPA-379 adds an explicit project/runtime configuration rather than relying on the engine default;
- tests cover embedded AcceptDialog input and the disabled-embedding rejection path;
- future detached-native-window support requires a separate per-window input bridge and is out of scope.

### 6.6 Transition layer

`TransitionLayer` is reserved visual placement for fades, wipes, loading covers, and scene handoff surfaces. It has no implicit policy. A visual-only transition uses passive priority and no input block; a blocking transition uses blocking priority, explicitly blocks gameplay, and declares its cancel behavior. The layer name alone never pauses, blocks, hides HUD, or captures focus.

## 7. Identity, kinds, layers, priority, and groups

### 7.1 Handles and identity

Each active entry has:

- a stable `Kind` identifying the concrete presentation flow;
- a generated instance token in `UIScreenHandle`;
- an optional parent handle;
- a monotonic presentation sequence number.

The token prevents a stale handle from closing a later instance of the same kind.

Only one active entry of a concrete kind is allowed. A duplicate open is rejected without changing focus, pause, cursor, HUD, process mode, node parentage, or stack order.

Categories such as “confirmation” and “error” are represented by priority and exclusive groups, not by one generic kind.

### 7.2 Canonical kinds

The repository defines centralized flow-specific constants rather than scattering free-form strings:

```csharp
public static class UIScreenKinds
{
    public static readonly StringName Pause = "pause";
    public static readonly StringName Settings = "settings";
    public static readonly StringName Inventory = "inventory";
    public static readonly StringName SaveLoad = "save_load";

    public static readonly StringName ConfirmOverwrite = "confirm_overwrite";
    public static readonly StringName ConfirmQuitToMain = "confirm_quit_to_main";
    public static readonly StringName SaveError = "save_error";
    public static readonly StringName CorruptSaveError = "corrupt_save_error";

    public static readonly StringName Dialogue = "dialogue";
    public static readonly StringName Shop = "shop";
    public static readonly StringName Heal = "heal";
    public static readonly StringName PuzzleRiddle = "puzzle_riddle";
    public static readonly StringName Battle = "battle";

    public static readonly StringName RewardToast = "reward_toast";
    public static readonly StringName RewardAcknowledgement = "reward_acknowledgement";
    public static readonly StringName Transition = "transition";
}
```

Feature-specific kinds may be added centrally. The host does not attach domain behaviour to the constants.

### 7.3 Exclusive groups and normalization

```csharp
public static class UIScreenExclusiveGroups
{
    public static readonly StringName None = "";
    public static readonly StringName BlockingPrompt = "blocking_prompt";
}
```

`UIScreenEntrySpec.ExclusiveGroup` is nullable/optional adapter input. Before model projection, the host normalizes null, default, or zero-length values to `UIScreenExclusiveGroups.None`. `UIScreenEntryPolicy.ExclusiveGroup` is always a non-null normalized value.

Empty groups never conflict. Equal non-empty groups conflict unless the entries form an explicitly allowed parent-child relationship.

Overwrite and quit confirmations use distinct kinds but normally share `BlockingPrompt`, so one blocking prompt is active at a time without conflating identity with category.

### 7.4 Visual layer

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

Layer determines Control placement and same-layer draw order. For embedded Windows it is logical metadata only.

### 7.5 Input priority

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

A passive entry must satisfy all of the following:

- `PauseTree == false`;
- `BlockGameplayInput == false`;
- `Cancel == None`;
- no entry-scoped cancel actions;
- `LowerLayers == VisibleInteractive`;
- no initial focus request.

An input-blocking transition uses `InputPriority.Blocking` with an explicit cancel policy; it is not Passive.

## 8. Registration and process contract

### 8.1 Process policy

```csharp
public enum UIProcessPolicy
{
    PreserveAndValidate,
    InheritHost,
    Pausable,
    WhenPaused,
    Always
}
```

- `PreserveAndValidate`: keep the view's current `ProcessMode`; Pausable modes must satisfy the immediate post-open pause reduction, while WhenPaused modes require candidate-owned pause or a direct/transitive pausing ancestor that bounds their lifetime.
- `InheritHost`: set or require `ProcessMode.Inherit`; active UI layers make the view Always.
- `Pausable`: process only while the tree is unpaused.
- `WhenPaused`: process only while the tree is paused; valid only for a pause-only view that is never used in an unpaused context.
- `Always`: explicitly process in both states.

The adapter snapshots any process mode it changes and restores the exact value on unregister or teardown.

The candidate's immediate pause context includes every active pause owner, not
only its own `PauseTree` declaration, so a Pausable entry is rejected whenever
the tree will be paused as it opens. WhenPaused requires the stronger logical
lifetime bound: the candidate owns pause or descends, possibly transitively,
from an active pausing ancestor whose close cascades to the candidate. A
WhenPaused child of Pause is accepted, an unrelated root cannot borrow Pause's
temporary context, and a pause-owning candidate is valid with no prior owner.

Reusable Settings, Save/Load, confirmation, and dialog views normally use `InheritHost` or `Always` because they can appear under both Main Menu and Pause. `HUDLayer` is a separate explicitly Pausable subtree.

### 8.2 Entry specification

```csharp
public sealed record UIScreenEntrySpec
{
    public required StringName Kind { get; init; }
    public required UIScreenLayer Layer { get; init; }
    public required UIInputPriority InputPriority { get; init; }
    public required UIProcessPolicy ProcessPolicy { get; init; }

    public UIScreenHandle? Parent { get; init; }
    public StringName? ExclusiveGroup { get; init; }
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

Collections use empty immutable values rather than `null`. The host normalizes nullable scalar adapter values before model projection.

### 8.3 Default view adapters

For an ordinary `Control`:

- presentation state is `Visible`;
- `SetPresented` calls `Show()` or `Hide()`;
- interactivity uses pointer shielding and an optional explicit adapter for direct `_Input()` behaviour;
- focus viewport is `control.GetViewport()`.

For an ordinary embedded `Window` or `AcceptDialog`:

- presentation state is `Visible`;
- hiding calls `Hide()`;
- restoring uses an explicit adapter when the flow requires `Popup*()` rather than plain `Show()`;
- visible-inert behaviour snapshots and changes `GuiDisableInput` and `Unfocusable`;
- focus viewport is the Window itself.

A legacy flow must provide explicit adapters when defaults do not preserve popup, sizing, input, process, or restoration semantics. Registration is rejected before stack mutation when a requested policy cannot be applied safely.

### 8.4 Node lifetime

```csharp
public enum UINodeLifetime
{
    External,
    Hide,
    QueueFree
}
```

- `External`: caller owns disposal; terminal close removes registration and
  detaches the view when it remains under the host attachment parent. Failed
  registration rollback alone preserves caller-preparented parentage.
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
    UnsupportedSubwindowMode,
    InvalidProcessPolicy,
    InvalidSpecification,
    MalformedHost,
    HostMutating
}

public readonly record struct UIScreenOpenResult(
    UIScreenOpenStatus Status,
    UIScreenHandle? Handle);
```

Only `Opened` contains a handle. Every rejection is a strict no-op. `HostMutating`
means `TryPresent` ran during an active close/cleanup drain; the host does not
queue the open, and the owner may explicitly retry only after that transaction
returns. Tests assert stable status codes, not error strings. Model acceptance
occurs before a blocking Window's dynamic focus sink is created, so a rejected
already-in-tree Window retains its exact synchronous child identity/order and
receives no lifecycle callback.

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

There is no `Replaced` reason because the host has no replacement operation. Callers explicitly close an entry, observe completion, then present another entry.

The reason is delivered to managed cleanup at most once.

## 10. Runtime input ownership and Godot ordering

### 10.1 Hard ownership contract

`UIScreenHost` is the only always-processing cancel dispatcher for its root scene. It owns `_Input(InputEvent)`.

The Game root remains inherited/pausable. Gameplay `_Input`, `_Process`, and physics callbacks therefore stop under `SceneTree.Paused`, subject to the explicit-Always audit in section 6.3, while the host continues receiving core cancel input and can invoke the root fallback delegate.

The host never relies on a paused Game `_Input()` callback.

### 10.2 Host input callback

```csharp
public override void _Input(InputEvent inputEvent)
{
    var result = TryHandleInput(inputEvent);
    if (result == UIInputDispatchResult.Consumed)
        GetViewport().SetInputAsHandled();

    // ReservedForTopEntry remains unhandled for the registered retained
    // controller or embedded GUI owner. NoOwner is ignored by the host.
}
```

`TryHandleInput` remains public for deterministic tests. Production roots do not forward the same event again.

### 10.3 Deterministic node order, deliberate non-reliance

Within one Viewport, Godot dispatches node `_Input()` in reverse depth-first scene-tree order. A retained Control controller beneath the host therefore normally runs before the host ancestor. Windows and SubViewports use separate propagation paths.

The design states this ordering as a fact, but does not use it as the only correctness mechanism. Correctness comes from one terminal owner:

- a retained top controller may consume before the host;
- if it leaves the event unhandled, the host applies or reserves the registered policy;
- host-owned `Close` or `Consume` requires the competing retained terminal handler to be disabled before registration becomes active;
- hidden or inert lower handlers are disabled before effective state is published;
- embedded GUI pass-through is allowed only when no lower or unrelated `_Input()` owner can consume first;
- gameplay Pause fallback exists only in the host's root fallback.

Tree reordering must not create a second terminal outcome.

### 10.4 Root fallback under pause

`RootCancelFallback` is invoked synchronously by the Always host. The root object does not need to receive `_Input()`.

Gameplay fallback may:

- open Pause when no UI entry owns or reserves a core cancel and no domain blocker applies;
- consume core cancel during an external atomic world interaction;
- decline when no root action applies.

Once Pause is active, its host entry—not a residual `Game._Input()` ladder—owns close/back behaviour.

## 11. Cancel action surfaces

### 11.1 Core actions

```csharp
public sealed record UIScreenHostOptions
{
    public Control? HudRoot { get; init; }
    public IReadOnlySet<StringName> CoreCancelActions { get; init; }
    public Func<UIRootCancelContext, UIRootCancelResult>? RootCancelFallback { get; init; }
    public Action<bool>? GameplayInputBlockChanged { get; init; }
}
```

Expected Game configuration:

```csharp
CoreCancelActions = new HashSet<StringName>
{
    "pause_menu",
    "ui_cancel"
};
```

These are evaluated against the logical top entry and may reach `RootCancelFallback` when no entry owns or reserves them.

### 11.2 Entry-scoped actions

`EntryCancelActions` apply only to the active entry that declares them. `toggle_inventory` is the primary example.

- Active Inventory may close on `toggle_inventory`.
- Pressing `toggle_inventory` while Settings, Save/Load, NPC, Battle, or another top flow is active does not turn it into generic Cancel.
- Entry-scoped actions never invoke the root fallback.
- Opening Inventory remains a gameplay or explicit Pause action.
- A blocked non-active toggle cannot open Inventory through ordinary gameplay input.

### 11.3 Native dialog close action

`ui_close_dialog` is not added to core actions. One physical event may match `pause_menu`, `ui_cancel`, and `ui_close_dialog`.

The host performs one traversal:

- records matched host actions once;
- a pass-through/native reservation leaves the event unhandled;
- embedded GUI later consumes `ui_close_dialog`;
- the dialog terminal signal closes the handle without a second host cancel traversal.

### 11.4 Non-action terminal surfaces

`Window.CloseRequested`, `Canceled`, guarded terminal signals, explicit buttons, and programmatic outcomes are lifecycle surfaces, not remapped actions. Their integration adapters call `TryClose` once with the appropriate reason.

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

- `Consumed`: host marks the root viewport event handled.
- `ReservedForTopEntry`: parent/root fallback is suppressed, but the event remains unhandled for the registered retained or embedded GUI owner.
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
3. `DeferToPolicy`, or no interceptor, evaluates static policy.
4. Static `None` continues; `Close`, `Consume`, and `PassThrough` stop.
5. The first non-None outcome wins.

### 12.4 Algorithm

For each input event:

1. Determine matched core actions once.
2. Prune invalid entries.
3. If a focus-restoration lease is active and a core or top-entry action matches, consume as a no-op.
4. Traverse active entries in logical input order.
5. Determine each candidate's matched entry-scoped actions.
6. Skip candidates for which neither core nor entry action matched.
7. Invoke dynamic interception, then static policy.
8. Stop at the first owner, reservation, or close.
9. Invoke root fallback only when a core action matched and no entry owned or reserved it.
10. Return `NoOwner` otherwise.

A physical event matching multiple synchronized actions produces one traversal and one terminal result.

## 13. Stack, compatibility, and ordering

### 13.1 Logical ordering

Entries have independent orderings:

- Control visual order: layer rank, then presentation sequence;
- logical input order: active child before ancestor, then Blocking before Modal before Screen before Passive, then newest sequence.

Embedded Windows do not participate in Control draw interleaving; their layer remains logical metadata.

A child always outranks its parent. A passive toast does not displace a modal's cancel ownership merely because it draws above ordinary Control content.

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

- the same concrete kind is active;
- either side declares the other's kind incompatible;
- equal non-empty exclusive groups conflict;
- requested parent is invalid or inactive;
- required view/process/subwindow adapters are missing.

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

Lower-layer effects compose from every active owner.

### 14.1 Reduction rule

For each target entry:

1. Find every active entry above it whose lower-layer policy applies.
2. Reduce using:

```text
Hidden > VisibleInert > VisibleInteractive
```

3. Apply the strongest current effect.
4. Keep the target's incoming baseline snapshot while at least one owner affects it.
5. Restore the exact baseline only when no owner affects it.

Example:

- Pause keeps gameplay/HUD visible-inert according to its policies.
- Settings child hides Pause.
- Pause's gameplay-inert contribution remains active while Settings is open.
- Closing Settings restores Pause while gameplay remains inert until Pause closes.

### 14.2 Control mechanism

- `Hidden` snapshots and changes `Visible`.
- `VisibleInert` uses an input shield, transfers focus, and invokes `SetInteractive(false)` when direct `_Input()` exists.
- exact prior host-managed values restore as reduction weakens or ends.

### 14.3 Embedded-window mechanism

- `Hidden` snapshots presentation state and calls the registered hide adapter.
- restoration uses a flow-specific popup adapter where plain `Show()` is insufficient.
- `VisibleInert` snapshots and sets `GuiDisableInput` and `Unfocusable`.
- a Control-layer shield is never considered sufficient for a Window viewport.
- exact incoming values restore as effects weaken or end.

An owner open is rejected if its requested effect cannot be applied safely.

## 15. Pause, gameplay block, cursor, and HUD

### 15.1 Pause ownership

Effective pause is true when any active entry requests `PauseTree`.

The host uses an exact pause lease:

1. On false-to-true effective ownership, capture current `SceneTree.Paused`.
2. Set `SceneTree.Paused = true`.
3. Keep one baseline while any pausing entry remains.
4. On true-to-false, restore the captured baseline once.
5. On teardown, restore if still owned.

Only a presentation that introduces world pause needs `PauseTree=true`:

- root Pause requests it;
- Inventory opened directly from gameplay requests it;
- Settings or Save/Load under Pause normally rely on the still-active Pause parent;
- Settings under Main Menu remains unpaused.

This avoids redundant pause claims while preserving nested ownership.

HPA-379 must remove Inventory's direct pause mutation before registering Inventory with host pause ownership.

### 15.2 Pause drift detection

While a pause lease is active, no non-host UI authority may write `SceneTree.Paused`.

The Always host checks the invariant in debug/runtime diagnostics. If `SceneTree.Paused` becomes false while the lease is active:

- emit a clear ownership-drift error;
- increment a diagnostic drift counter;
- reassert `Paused=true` to preserve the active contract;
- retain the original incoming baseline for final restoration.

This is a contract violation, not a second supported owner. Tests deliberately mutate pause during a lease and verify detection, reassertion, and exact final restoration.

### 15.3 Unified gameplay-block predicate

Domain flags and host presentation state retain different meanings:

- `GameManager.IsInBattle`, `IsInNpcInteraction`, and `IsInWorldInteraction` describe domain lifecycle/atomic operation state.
- `UIScreenHost.IsGameplayInputBlocked` describes active presentation policy, including visible result UI after a domain flag clears.

HPA-379 replaces scattered gameplay-input guards with one root-level predicate:

```csharp
bool IsGameplayInputBlocked =>
    _uiScreenHost.IsGameplayInputBlocked ||
    _gameManager.IsInBattle ||
    _gameManager.IsInNpcInteraction ||
    _gameManager.IsInWorldInteraction;
```

Rules:

- gameplay movement, interaction, inventory-open, and similar commands consult this one predicate;
- flow-specific domain code may still inspect individual flags to decide domain eligibility;
- host compatibility/priority, not domain flags, determines UI stack ordering and cancel ownership;
- Battle result remains blocked through the host after `IsInBattle` clears;
- atomic treasure/switch work remains blocked through the domain flag without a presentation entry;
- there is no precedence conflict: effective gameplay suppression is the OR of the two sources.

The host callback may publish its component through `GameplayInputBlockChanged`, but Game owns the final composed predicate.

### 15.4 Cursor

The effective cursor policy is the highest logical-priority explicit override. The host captures exact incoming mouse mode before the first override and restores after the last.

### 15.5 HUD

The effective HUD policy is the highest logical-priority explicit override. The host captures exact incoming HUD visibility before the first override and restores after the last.

Visibility policy and processing policy are separate: a visible HUD may remain drawn while its explicitly Pausable subtree stops processing.

A non-inherit HUD policy is invalid when the host has no configured HUD root.

### 15.6 Effective state

```csharp
public sealed record UIScreenEffectiveState(
    bool IsTreePauseOwned,
    bool IsPresentationGameplayBlocked,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    bool IsFocusRestorationPending);
```

`EffectiveStateChanged` emits only after a complete consistent mutation and only when the value changes.

## 16. Focus model, sinks, and restoration lease

### 16.1 Focus records

Before a child opens, the host captures:

- the active parent's focus viewport;
- that viewport's focused `Control`;
- parent handle and instance token.

For a Control entry, default viewport is `view.GetViewport()`. For an embedded Window, default viewport is the Window itself.

### 16.2 Host-owned focus sinks

A focus sink must be focusable and visible in-tree; `Visible=false` is not permitted.

`UIScreenHost.tscn` contains one non-drawing root sink configured as:

- `Visible=true`;
- fully transparent/no drawing;
- 1×1 or zero-layout-impact size;
- `MouseFilter.Ignore`;
- `FocusMode.All`;
- no domain behavior.

For a blocking embedded Window with no focusable descendant, the adapter creates an equivalent transparent-but-visible sink inside that Window viewport and removes it on unregister.

Sink creation failure rejects the open before policy mutation. Diagnostics identify sink use and viewport.

### 16.3 Initial focus

After a view is in-tree, presented, and interactive, acquisition is deferred:

1. declared `InitialFocus`;
2. first valid focusable descendant;
3. appropriate sink for a blocking entry.

Passive entries cannot request focus. They register for lifetime and diagnostics,
but never schedule initial acquisition, steal an existing focus owner, or create a
restoration lease when they close.

Initial-focus deferral has no cancel barrier. The entry becomes logical owner
synchronously. Deferred callbacks carry the handle token and no-op after close
or after a higher entry becomes the current top input owner. A lower entry must
never transiently steal focus from a higher owner opened re-entrantly during
attachment or `_Ready()`.

### 16.4 Restoration

After close and lower-layer restoration:

1. explicit restore target;
2. captured control in captured viewport;
3. parent's initial target;
4. first focusable descendant of top entry;
5. sink if blocking entry remains;
6. release focus if no UI owner remains.

Embedded parent presentation/interactivity restores before focus acquisition.
Every declared or captured Godot target is checked with
`GodotObject.IsInstanceValid()` before any member is dereferenced. An invalid
target falls through to the next restoration candidate.

### 16.5 Restoration lease and guaranteed release

The temporary cancel barrier is implemented as a generation-tagged restoration lease, not a bare boolean.

Invariant: every close path that acquires a restoration lease releases it exactly once.

- A scheduled deferred restoration completes the lease in `finally`, even when no valid target exists.
- If the target is freed before the callback, the callback still executes against the live host, selects the next valid fallback or no target, and completes in `finally`.
- If the host begins teardown before the callback, teardown cancels the scheduled callable, disables dispatch, and completes the lease synchronously.
- A superseding close completes the prior generation without publishing an intermediate false barrier, installs the newer generation, and publishes once through the enclosing close; stale callbacks cannot clear the newer lease.
- Queue collapse and duplicate close paths cannot create a second lease for the same close transaction.
- `IsFocusRestorationPending` is derived from the active lease record, not independently mutated.

Matching Cancel is consumed only while a live lease exists.
Closing a passive entry never acquires or supersedes a restoration lease.

Named tests cover target deletion, host teardown, re-entrant close, duplicate close, and stale callback execution, and prove core Cancel works again after every path.

## 17. Node lifecycle and re-entrancy

### 17.1 Control attachment

An unparented Control is attached to its declared host layer. A Control already under that layer may register without reparenting. A Control parented elsewhere is rejected. On terminal close or prepared teardown, every externally owned Control is detached from the layer even when the caller pre-parented it; failed-registration rollback alone preserves the original parentage.

### 17.2 Embedded Window registration

Embedded Windows remain separate viewports and register in a logical layer. Adapter state includes presentation, process behavior, `GuiDisableInput`, `Unfocusable`, and focus viewport.

### 17.3 Idempotent close

A second close for the same handle returns `AlreadyClosed` and performs no callback, policy mutation, or node operation.

### 17.4 External deletion

When a registered node exits or becomes invalid outside host close:

1. descendants close first;
2. model entry is removed with `NodeFreed`;
3. managed cleanup is invoked once;
4. operations requiring a live object are skipped;
5. lower-layer and effective policy recompute;
6. restoration lease either restores focus or completes without target;
7. next valid owner regains input.

### 17.5 Mandatory scene-owner teardown

`UIScreenHost` exposes this public lifecycle API:

```csharp
public enum UIScreenTeardownPreparationStatus
{
    Deferred,
    Complete
}

public UIScreenTeardownPreparationStatus PrepareForTeardown();
```

Every containing scene owner **must** call `PrepareForTeardown()` before it
queues or frees the containing scene or any ancestor of the host, and may
proceed with deletion only after the call returns `Complete`. A re-entrant call
from an active close/cleanup mutation returns `Deferred`; the owner must defer
and retry after the current mutation finishes. HPA-379 integrations own this
completion check and retry at each scene-navigation/deletion boundary.
Godot recursively starts deleting children before the host receives
`_ExitTree()`, so `_ExitTree()` is only a defensive idempotent fallback and
cannot preserve an externally owned direct-child embedded Window by itself.
The typed `UIScreenHost.QueueFree()` convenience calls `PrepareForTeardown()`
and queues deletion only for `Complete`; it does not replace the scene-owner
obligation.

Prepared teardown:

- input dispatch is disabled;
- entries close topmost-first with `HostTeardown`;
- callbacks cannot reopen;
- all restoration leases complete;
- pause, cursor, HUD, Control, process-mode, Window, and focus snapshots restore once;
- deferred callbacks invalidate;
- subscriptions and dynamic sinks are removed;
- externally owned views are not freed.

User `InitialFocus` and `RestoreFocus` providers are part of the prepared
teardown transaction. If either throws there, the exception propagates, the
lease remains retryable, and `Complete` is not published. Ordinary deferred
runtime focus paths may retain safe catch-and-fallback behavior.

The method is idempotent. `Complete` means the model is empty, all adapters are
closed, external views are detached, leases and snapshots are restored, and
host bindings are finalized. After preparation begins, opens are rejected and
subsequent typed deletion or `_ExitTree()` performs no second cleanup.

A distinct active-finalization guard spans focus-restoration completion, state
lease restoration, focus teardown, sink/binding cleanup, and readiness reset.
User focus/state callbacks may re-enter during those steps; every such call
returns `Deferred`. The finalized flag is published only as the last successful
step. If a callback throws, the exception propagates, the active guard resets,
`Complete` remains unpublished, and a later owner retry resumes finalization.

### 17.6 Mutation queue

- current mutation completes before queued mutation;
- `TryPresent` during an active close/cleanup drain returns `HostMutating`
  synchronously and is never queued;
- closing handle is immediately ineligible for input;
- duplicate queued closes collapse;
- teardown rejects opens;
- active finalization is non-reentrant and publishes completion last;
- restoration leases are generation-tagged;
- effective state publishes only after consistent completion.
- focus bookkeeping is committed before any effective-state or gameplay-block
  callback can re-enter; callbacks and diagnostics therefore observe one
  complete transaction, and a re-entrant close cannot leave stale sinks.

## 18. Legacy compatibility and HPA-379 flow contract

### 18.1 Domain boundary

Outside the host remain:

- save-slot selection, serialization, pending-load handoff;
- staged settings edits and keybinding validation;
- battle timer, result, reward, enemy, and combatant cleanup;
- inventory and equipment mutations;
- NPC outcomes, shop transactions, healing;
- puzzle choices and world-interaction flags;
- scene navigation and quit decisions.

### 18.2 Required integration rows

| Flow | Host entry/lifetime | Cancel/process contract |
|---|---|---|
| Root Pause | Active from open until resume/teardown | Introduces `PauseTree`; core cancel; host closes/restores once |
| Inventory | Active while presented; may be Pause child | `toggle_inventory` entry-scoped; direct pause mutation removed; processes under host pause |
| Settings | Main Menu or Pause child | popup/capture interceptor reserves; retained handler or host close, never both |
| Save/Load | Main Menu or Pause child; flow-specific confirmations are children | core cancel pass-through or host close; child first; exact parent restoration |
| Errors/confirmations | flow-specific Blocking kinds in shared blocking-prompt group | topmost-only; safe focus; parent unaffected on Cancel |
| NPC dialogue/shop/heal | Modal embedded Window entry | GUI pass-through; domain flag and host block compose through root predicate |
| Puzzle riddle | Modal embedded Window entry | configured cancel cleans only riddle; atomic switch/treasure remain external domain blockers |
| Battle | One entry for full visible Battle lifetime, including result after `IsInBattle` clears | prep/auto escape preserved; visible result topmost; no Pause behind Continue |
| Reward toast | Passive entry | no pause, block, focus, cancel, cursor, HUD, or lower-layer effect |
| Required reward acknowledgement | Blocking child | generic Cancel consumed; Continue closes; producer retains grant authority |
| Transition | Passive visual-only or explicitly Blocking | policy declared; layer alone has no behavior |

### 18.3 Battle invariant

- Create the handle when Battle presentation opens.
- Keep it through preparation, auto-combat, and visible result UI.
- `BattleFinished` does not close the handle when result UI remains visible.
- Preparation/active escape preserves immediate close, once-only result, timer/effect cleanup, and enemy retention.
- After `IsInBattle` clears, the visible result entry still blocks gameplay and owns/reserves core Cancel.
- Continue, Window close, or node exit ends presentation and closes/prunes once.
- Defeat delayed navigation remains domain-owned and lifecycle-guarded.

HPA-379 adds physical-input regressions proving Cancel never opens Pause behind preparation, active Battle, or result Continue.

## 19. Pause migration risks and ordering

Host-owned root Pause changes runtime behavior: current Pause does not pause `SceneTree`; only Inventory currently does. This is the highest-risk integration step.

### 19.1 Risks

| Risk | Failure mode | Mitigation |
|---|---|---|
| Explicit Always world nodes | half-paused world; animation or state advances | real-scene process audit; normalize GridMap runtime mode before enabling Pause |
| HUD inherits Always | HUD timers/tweens continue silently | explicit Pausable HUDLayer; opt-in exceptions only |
| Legacy direct pause writes | stale snapshot or premature resume | remove Inventory/direct writes; drift detection and reassertion |
| Mixed block authorities | commands check inconsistent conditions | one composed root predicate used by all gameplay input entry points |
| Embedded Window assumption | host misses events in detached OS window | explicit embed setting, runtime validation, rejection test |
| Dropped restoration callback | Cancel swallowed permanently | restoration lease with guaranteed completion |
| Mixed legacy/host cancel ownership | duplicate terminal outcomes | one-owner migration rule and physical-input tests |
| Scene deletion bypasses completed teardown preparation | external embedded Window is recursively freed before host cleanup | every scene owner deletes only after `PrepareForTeardown()` returns `Complete`; `Deferred` schedules a later retry outside the active mutation |

### 19.2 HPA-379 first-migration sequence

1. Wire each scene owner to call `UIScreenHost.PrepareForTeardown()` before every containing-scene deletion or navigation handoff, proceed only on `Complete`, and defer/retry on `Deferred`.
2. Inventory and characterize every explicit/effective Always node in Game and floor scenes.
3. Normalize runtime GridMap to Inherit/Pausable while retaining editor-only preview behavior.
4. Make `HUDLayer` explicitly Pausable and verify HUD rendering versus processing under pause.
5. Introduce the composed root `IsGameplayInputBlocked` predicate and replace scattered input guards.
6. Integrate the host with `PauseTree=false` for existing flows and prove cancel/focus/lifecycle parity.
7. Remove direct Inventory pause ownership.
8. Enable `PauseTree=true` for root Pause and gameplay-opened Inventory.
9. Add child Settings/Save/Load/confirmation flows while the Pause parent retains the pause lease.
10. Run real-scene physical-input and timing tests before removing remaining legacy ladders.

No root Pause migration may enable tree pause before steps 1–5 pass.

## 20. Host option examples

### 20.1 Game

```csharp
var options = new UIScreenHostOptions
{
    HudRoot = gameUi,
    CoreCancelActions = new HashSet<StringName>
    {
        "pause_menu",
        "ui_cancel"
    },
    GameplayInputBlockChanged = blocked =>
        _presentationGameplayBlocked = blocked,
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

Game composes `_presentationGameplayBlocked` with domain flags through the single predicate in section 15.3.

### 20.2 Main Menu

```csharp
var options = new UIScreenHostOptions
{
    HudRoot = null,
    CoreCancelActions = new HashSet<StringName> { "ui_cancel" },
    RootCancelFallback = _ => UIRootCancelResult.Declined
};
```

## 21. Diagnostics

Debug-only read-only diagnostics expose:

- active handles in logical order;
- kind, parent, layer, priority, process policy, and sequence;
- normalized incompatibility and exclusive-group values;
- effective pause/presentation-block/cursor/HUD state;
- composed root gameplay block components in integration diagnostics;
- lower-layer contributors and reduced effect per target;
- core and entry-scoped action ownership;
- focus viewport/control, sink use, and restoration lease generation;
- process-mode audit and validation state;
- subwindow embedding state;
- pause/cursor/HUD/Control/Window/process snapshots;
- pause ownership-drift count and last observed violation.

Diagnostics never expose mutable internal collections. Public status codes, not log text, are the test contract.

## 22. Implementation phases

### Phase 0 — Process and platform characterization

- add synthetic process-mode matrix tests;
- add embedded-subwindow precondition tests;
- document HPA-379 real-scene GridMap/HUD audit requirements;
- establish stable result/status types and canonical constants.

### Phase 1 — Pure model

- handles and concrete kinds;
- normalized groups and compatibility;
- parent-child stack and close cascades;
- priority and compositional lower-layer reduction;
- pause/block/cursor/HUD value reduction;
- pure deterministic tests.

### Phase 2 — Control and Window adapters

- host scene/layers and process modes;
- Control attachment and shields;
- embedded Window visibility/interactivity/process adapters;
- exact snapshots and no-op rejection paths;
- adapter runtime tests.

### Phase 3 — Input dispatcher

- core and entry-scoped action matching;
- one-event/one-attempt deduplication;
- dynamic interception before static policy;
- root fallback and retained/native reservation;
- cancel-surface tests.

### Phase 4 — Pause and effective-state ownership

- pause lease and drift detection;
- gameplay-block publication;
- cursor and HUD snapshots;
- mutation publication ordering;
- teardown restoration tests.

### Phase 5 — Focus and lifecycle

- per-viewport focus records;
- transparent-but-visible sinks;
- generation-tagged restoration leases;
- external deletion and re-entrancy;
- focus/lifecycle tests.

### Phase 6 — Contract and full verification

- public integration documentation;
- named HPA-376 contract scenarios using synthetic views;
- full Sirius suite;
- orphan-warning comparison.

The implementation should remain reviewable by landing these phases as focused commits rather than one monolithic host commit.

## 23. Planned source layout

```text
scripts/ui/hosting/
├── UIScreenHost.cs
├── UIScreenHostOptions.cs
├── UIScreenKinds.cs
├── UIScreenExclusiveGroups.cs
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
├── UIScreenHostSubwindowTest.cs
├── UIScreenHostTest.cs
├── UIScreenHostInputTest.cs
├── UIScreenHostFocusTest.cs
└── UIScreenHostLifecycleTest.cs

docs/ui/hpa-378/
└── uiscreenhost-contract.md
```

Small enum/result files may consolidate when readability improves. Model, adapter registry, input dispatcher, and focus coordinator must not collapse into one large class.

## 24. Test strategy

### 24.1 Process-mode and embedding gate

| Scenario | Required result |
|---|---|
| Tree unpaused | host receives core Cancel |
| Tree paused by entry | host still receives core Cancel |
| Game remains inherited | Game `_Input()` need not run while paused |
| Synthetic inherited gameplay child | processing stops while paused |
| Real runtime GridMap after HPA-379 audit | `CanProcess()` false while paused |
| Editor GridMap preview | intended tool preview preserved |
| HUD under Pausable HUDLayer | visible policy retained; processing stops |
| Hosted Control under active UI layer | remains interactive while paused |
| Explicit Pausable hosted view that must work paused | rejected or safely overridden/restored |
| Embedded AcceptDialog | host/native one-attempt Cancel works |
| Subwindow embedding disabled | Window registration rejected without mutation |
| Last pausing entry closes | exact pause/process baselines restored |

### 24.2 Pure model tests

- push/pop ordering;
- child/ancestor priority;
- duplicate concrete kinds;
- flow-specific confirmation kinds with shared group conflict;
- null/default/empty group normalization;
- symmetric incompatibility;
- child cascade close;
- stale handles;
- compositional lower-layer reduction;
- policy reduction;
- stable result statuses;
- deterministic mutation ordering.

### 24.3 Runtime policy tests

- required layers, HUD process mode, and root focus sink;
- Control attachment and invalid parentage;
- embedded Window registration/adapters;
- exact pause, cursor, HUD, Control, Window, and process restoration;
- pause drift detection/reassertion;
- gameplay-block publication;
- nested lower-layer effect persistence;
- native hidden/inert restoration;
- initial focus and stale callback no-op;
- root and per-Window transparent focus sinks;
- cross-viewport focus restoration;
- external node deletion;
- idempotent close;
- host teardown;
- callback re-entrancy.

### 24.4 Restoration-lease tests

| Scenario | Required result |
|---|---|
| target valid | deferred focus restores and lease clears |
| target freed before callback | fallback/no-target path completes lease |
| host exits mid-restore | dispatch disabled and lease completes |
| re-entrant close supersedes restore | stale generation cannot clear newer lease |
| duplicate close collapses | one lease; no leak |
| next core Cancel after invalidation | root/entry handling works normally |

### 24.5 Cancel-surface tests

| Scenario | Required result |
|---|---|
| `pause_menu` and `ui_cancel` co-match | one logical attempt |
| `ui_close_dialog` also matches pass-through | embedded dialog and handle close once |
| Inventory active + `toggle_inventory` | Inventory closes |
| Settings top + `toggle_inventory` | no generic Cancel against Settings |
| OptionButton popup | reserve for popup; Settings remains |
| key capture | reserve/consume according to interceptor |
| blocking error over Pause | error closes; Pause remains |
| restoration lease active | matching Cancel consumed temporarily |
| toast above modal | modal remains owner |
| root empty | fallback may open Pause |
| root paused with Pause | host closes Pause without Game `_Input()` |
| required acknowledgement | Cancel consumed without close |

### 24.6 Contract scenarios

Use synthetic views to test:

- Inventory child of Pause: world paused, HUD hidden, Pause contribution retained, close returns to Pause;
- Settings child of Pause: child hide stacks over Pause gameplay-inert effect;
- destructive confirmation: flow-specific kind, safe focus, Cancel returns, no destructive callback;
- reward toast: passive and non-blocking;
- required acknowledgement: parent inert, Continue/sink focus, Cancel ignored;
- Battle result entry remains topmost after synthetic domain flag clears;
- presentation block false/domain block true and presentation block true/domain block false both suppress gameplay through the composed predicate.

### 24.7 HPA-379 physical integration tests

- real-scene explicit-Always audit and GridMap runtime correction;
- HUD processing under Pause;
- host input while tree paused;
- retained Settings/Inventory handler transition;
- embedded AcceptDialog close and subwindow setting;
- OptionButton/key-capture pass-through;
- no Pause behind NPC, riddle, Save/Load child, error, or Battle result;
- composed gameplay-block predicate across Battle/NPC/world/result states;
- exact restoration after child flows and teardown.

### 24.8 Isolation

Every runtime test restores:

- `SceneTree.Paused`;
- changed process modes;
- subwindow embedding state;
- mouse mode and HUD visibility;
- viewport focus and Window flags;
- host/view/sink nodes;
- temporary input actions/events;
- orphan-warning settings changed by the test.

The full Sirius suite must remain green and HPA-378 must not increase the HPA-376 orphan-warning signature.

## 25. Acceptance criteria mapping

| HPA-378 criterion | Design mechanism |
|---|---|
| HPA-376 matrix implemented | priority, action-surface, and contract tests |
| Entries declare pause/input/cursor/HUD/focus/cancel/process | entry spec and pure policy projection |
| Topmost Cancel and nested modal deterministic | handles, groups, priorities, compositional effects, one terminal owner |
| Pause/input derived centrally | pause lease, drift detection, Always host input owner |
| Focus restoration and fallback | per-viewport records, visible sinks, guaranteed lease release |
| No domain duplication | domain/presentation block composition and explicit boundaries |
| Main Menu/Game configurable without fork | same host, different options/fallbacks |
| Lifecycle tests | pure, process, subwindow, runtime, and physical matrices |

## 26. HPA-379 integration contract

HPA-379 begins after the HPA-378 host contract and tests are merged.

For each migrated flow it must:

1. register one explicit entry using a concrete canonical kind;
2. normalize runtime world process modes before enabling root Pause;
3. keep HUD processing explicitly Pausable unless an exception is reviewed;
4. remove or disable competing direct pause, cancel, cursor, HUD, process, and presentation-input authority;
5. preserve terminal signals and domain cleanup;
6. rely on host input rather than paused Game `_Input()`;
7. configure core versus entry-scoped actions correctly;
8. provide presentation/interactivity/process/focus adapters where defaults are insufficient;
9. close handles on actual presentation termination;
10. keep Battle registered while result UI remains visible;
11. compose host presentation block with domain flags through one root predicate;
12. pin embedded-subwindow configuration explicitly;
13. keep all HPA-376 regressions passing.
14. handle `HostMutating` with an explicit owner retry after the close
    transaction; managed cleanup must not assume a rejected open was deferred.

HPA-379 must not introduce a second stack, root-specific host fork, residual Battle cancel ladder, or scattered gameplay-block combinations.

## 27. Resolved decisions

- Host is scene-local, never an autoload.
- Host owns Always `_Input()`; Game is not made Always.
- Current runtime GridMap Always behavior must be normalized before root Pause is enabled.
- HUD remains visible by policy but lives in an explicitly Pausable process subtree.
- Embedded subwindows are a hard precondition; detached OS windows are unsupported.
- Control visual layer and logical input priority are independent.
- Embedded Windows are exempt from CanvasItem visual interleaving.
- Pure model stores normalized values; Godot state lives in adapters.
- Concrete flow kinds are unique; categories use exclusive groups.
- Core Cancel family and entry-scoped toggles are separate.
- `ui_close_dialog` remains embedded GUI pass-through.
- One physical event produces one logical Cancel attempt.
- Godot node input order is deterministic reverse depth-first, but correctness relies on one terminal owner rather than tree position.
- No implicit replacement; `Replaced` is not a close reason.
- Lower-layer effects compose using Hidden > Inert > Interactive.
- Pause, process, cursor, HUD, Control, Window, and focus states restore exact baselines.
- Pause ownership drift is detected and reasserted.
- Gameplay suppression is host presentation block OR domain lifecycle block through one root predicate.
- Initial-focus defer has no barrier; close restoration uses a guaranteed-release lease.
- Focus sinks are transparent-but-visible, mouse-ignoring, and focusable.
- Passive entries cannot pause or block gameplay.
- Battle remains registered through visible result presentation.
- Transition layer has no implicit behavior.

## 28. Completion definition

HPA-378 is complete when:

- synthetic process-mode and embedded-subwindow tests pass;
- reusable scene and C# host implement the responsibilities above;
- pure and runtime tests cover priority, policy, focus, lifecycle, Windows, Cancel surfaces, restoration leases, and nested effect composition;
- `docs/ui/hpa-378/uiscreenhost-contract.md` documents the public integration surface;
- full Sirius suite passes with no new orphan-warning signature;
- HPA-379 has an explicit real-scene process audit, GridMap correction, HUD process plan, and composed gameplay-block migration path;
- no production flow is partially migrated or left with competing ownership;
- HPA-379 can register Main Menu, Pause, Inventory, Settings, Save/Load, Battle, NPC, puzzle, errors, confirmations, rewards, and transitions without changing host architecture.
