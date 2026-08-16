# HPA-569 Hosted Dialogue Design

## Goal

Replace the runtime-built `DialogueDialog : AcceptDialog` with one scene-authored, Sirius-themed, host-managed dialogue surface while preserving the existing dialogue-tree and NPC-interaction semantics.

This is a presentation migration only. HPA-570 continues to own Shop and Heal presentation.

## Why this is the next slice

The shared Theme, `SiriusModalShell`, gameplay `UIScreenHost`, Pause, Settings, Save/Load, Inventory, Battle, and hosted Prompt work are complete. HPA-569 is the first remaining unblocked interaction surface in the Sirius delivery order; its only declared blocker, HPA-382, is done. Shop/Heal, Puzzle/Riddle, and Reward remain independent later slices.

## Current state

- `DialogueDialog` owns both dialogue-tree traversal and runtime UI construction.
- `NpcInteractionController` owns the interaction sequence: Dialogue first, then optional Shop/Heal, then exactly-once completion.
- `Game` owns the production gameplay `UIScreenHost` and already suppresses direct gameplay while `GameManager.IsInNpcInteraction` is true.
- `UIScreenKinds.Dialogue` already exists.
- `SiriusModalShell` already provides themed modal chrome, responsive width, bounded body scrolling, and follow-focus scrolling.
- The canonical HPA-373 wireframe places Dialogue in a bottom interaction surface that keeps the world visible.
- `NpcData` exposes `DisplayName` and `SpriteType`, but no portrait texture/reference contract. `SpriteType` is world-sprite lookup metadata, not a portrait contract.

## Options considered

### A. Keep `NpcInteractionController` as orchestration owner and give it the gameplay host — selected

`NpcInteractionController` instantiates the scene-authored Dialogue screen and presents it through `UIScreenHost`. It remains responsible for terminal signals, the transition to legacy Shop/Heal, and final cleanup.

This is the smallest coherent change because it:

- preserves the existing interaction boundary;
- keeps `Game` from learning dialogue-tree progression details;
- localizes the HPA-569 cutover to the current owner;
- leaves a straightforward HPA-570 follow-up without creating a framework now.

### B. Move Dialogue hosting and terminal routing into `Game`

Rejected. It would split one NPC interaction across `Game` and `NpcInteractionController`, requiring callbacks or exposed state solely to route Dialogue outcomes.

### C. Add a generic interaction presenter/service

Rejected as speculative. Dialogue, Shop, and Heal have different behavior, and the existing `UIScreenHost` is already the reusable lifecycle primitive.

## Architecture

### 1. Replace the native window with one scene-backed `Control`

Create:

- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `tests/ui/DialogueScreenControllerTest.cs`

Delete after the hosted path is green:

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

`DialogueScreenController` remains the single owner of dialogue-node presentation and the current domain-affecting choice semantics:

- evaluate `DialogueChoice.Condition`;
- add `GrantFlag` before progressing or terminating;
- emit `DialogueOutcome` for `OpenShop`, `Heal`, and `CloseAndReturn`;
- emit `DialogueClosed` for explicit cancellation, leaf completion, or a broken `NextNodeId`;
- keep the one-shot terminal latch before any second domain mutation.

It does not own host lifetime and never frees itself from a terminal handler.

### 2. Make pre-tree configuration explicit

`NpcInteractionController` must configure the unparented screen before passing it to `UIScreenHost`. Therefore the screen API is deliberately pre-`_Ready()` safe:

```csharp
public partial class DialogueScreenController : Control
{
    [Signal] public delegate void DialogueOutcomeEventHandler(int outcome);
    [Signal] public delegate void DialogueClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryStartDialogue(
        NpcData npc,
        DialogueTree tree,
        Character player,
        HashSet<string> questFlags);

    public void RequestCancel();
}
```

`TryStartDialogue(...)` validates `tree.Root`, stores the supplied model, and returns `false` without emitting when the root is invalid. If the node is ready it renders immediately; otherwise `_Ready()` binds the authored controls and renders the stored root.

This avoids two invalid sequences:

- touching `%ModalShell` or other scene nodes before `_Ready()`;
- emitting `DialogueClosed` during `TryPresent(...)` before `NpcInteractionController` owns a host handle.

Invalid-root completion remains an orchestration responsibility: `NpcInteractionController` logs and calls its idempotent `Finish()` once.

`RequestCancel()` routes through the same one-shot `EmitClosedOnce()` path as the visible leaf action.

### 3. Scene composition follows the approved in-world wireframe

`DialogueScreen.tscn` uses one full-rect `Control` and one `SiriusModalShell` with `SizeClass = Large`.

Stable named nodes:

- `%ModalShell`
- `%Panel` from the shell instance
- `%SpeakerLabel`
- `%DialogueText`
- `%ChoicesContainer`

Presentation rules:

- no scrim: the world remains visible;
- the shell panel is overridden only for this scene to anchor bottom-center with the normal Sirius safe bottom margin;
- the shared shell API and metrics stay unchanged;
- modal title is `NpcData.DisplayName`;
- speaker line is the current node's `SpeakerName`, hidden when blank;
- no portrait node is added in HPA-569 because current data has no portrait contract; do not infer UI portrait semantics from `SpriteType` or add assets/model fields;
- `%DialogueText` is a wrapping `RichTextLabel` with `FitContent = true` and internal scrolling disabled;
- the shell's existing `%BodyScroll` is the single scroll owner for text plus choices;
- choices are dynamic wrapped `Button`s in a vertical container;
- a leaf renders one `Farewell.` action, preserving current behavior;
- old dynamic buttons are removed from `%ChoicesContainer` immediately before they are queued for deletion, so stale actions cannot remain in layout/focus order for another frame;
- action minimum height uses `SiriusUiMetrics.MinimumTarget(compact)`; no new metric is introduced.

At 640×360, the panel remains within the safe frame and the shared body scroll handles long text/choice sets. At larger viewports it remains a bottom interaction surface rather than reverting to a centered desktop dialog.

### 4. Focus stays local and deterministic

- Host initial focus resolves from `InitialFocusTarget` on the deferred host focus pass, after `_Ready()` rendered the stored root.
- Every nonterminal node transition updates `InitialFocusTarget` and defers focus to the first newly rendered action.
- The shell's existing `FollowFocus` behavior scrolls a focused long-choice action into view.
- Mouse, keyboard, and gamepad use ordinary Godot `Button` behavior.
- No selection model or manual directional neighbor graph is added unless a focused runtime test proves the vertical container order is insufficient.

## Hosting policy

`NpcInteractionController` receives the existing gameplay `UIScreenHost` in addition to the existing UI parent used by legacy Shop/Heal.

Dialogue opens with one explicit spec:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Dialogue,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Visible,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Consume,
    InitialFocus = () => screen.InitialFocusTarget,
    InterceptCancel = _ =>
    {
        screen.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    Cleanup = _ => ClearDialoguePresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
}
```

Rationale:

- NPC Dialogue did not historically pause the scene tree, so `PauseTree` remains false;
- the NPC interaction flag still owns domain suppression, while the host lease makes the active presentation's gameplay block explicit;
- HUD and world remain visible beneath the bottom surface;
- configured Cancel is consumed by Dialogue and cannot fall through to Pause;
- `Cancel = Consume` plus `InterceptCancel -> RequestCancel()` makes configured Cancel and visible completion share the screen's one-shot latch.

No new host API, kind, exclusive group, parent relationship, or policy factory is required.

## Interaction flow

### Start

1. `Game.OnNpcInteracted` verifies the production host exists before setting `IsInNpcInteraction`.
2. `GameManager.StartNpcInteraction()` runs once.
3. `Game` creates `NpcInteractionController`, passing `_screenHost` and the existing `UI` parent.
4. `NpcInteractionController.Begin()` resolves the dialogue tree.
5. Missing tree or invalid root logs and calls `Finish()` once with no presentation.
6. Valid data instantiates `DialogueScreen.tscn`, wires signals, and calls pre-ready-safe `TryStartDialogue(...)`.
7. The controller asks the host to present the configured screen.
8. Host-presentation failure disconnects/frees the rejected candidate and calls `Finish()` so the domain interaction cannot remain latched.
9. On success, the controller stores the returned handle and screen reference.

### Normal progression

Choice presses stay entirely in `DialogueScreenController` until a terminal outcome is reached. Conditions, branching, `GrantFlag`, and broken-next-node behavior are copied without a new domain layer.

### Dialogue → Shop/Heal

On `OpenShop` or `Heal`:

1. capture the outcome;
2. close the hosted Dialogue entry synchronously;
3. after the host close transaction, open the existing `ShopDialog` or `HealDialog` through the current controller code;
4. keep `GameManager.IsInNpcInteraction` true across the transition.

Dialogue is terminal before Shop/Heal opens, so no host parent relationship is introduced. HPA-570 later replaces the native child surfaces.

### Cancel/completion/invalid data

All terminal paths converge on existing exactly-once orchestration:

- the screen emits one terminal signal;
- the controller closes or clears the hosted Dialogue entry;
- `Finish()` remains idempotent;
- `InteractionComplete` fires once;
- `Game.OnNpcInteractionComplete` guards `EndNpcInteraction()` with `IsInNpcInteraction`, clears the controller, and refreshes gameplay presentation once.

### Teardown

`NpcInteractionController.Finish()` closes an active hosted Dialogue without requiring another terminal signal.

`Game._ExitTree()` must not leave the domain flag set after unsubscribing from `InteractionComplete`. It performs controller cleanup, then calls a guarded `EndNpcInteractionIfActive()` helper. The same guard is used by ordinary completion/reset paths, giving one domain end even when teardown and a terminal signal converge.

## Error handling

Keep failures local and terminal:

- missing dialogue tree: controller log + `Finish()`;
- null root: `TryStartDialogue(...) == false`, then controller log + `Finish()`;
- broken `NextNodeId`: screen log + `DialogueClosed` once;
- scene load/instantiate failure: controller log + `Finish()`;
- host `TryPresent` failure: disconnect/free rejected candidate + `Finish()`;
- repeated terminal input: ignored before a second `GrantFlag` or terminal emission;
- Game teardown: hosted entry closes and `IsInNpcInteraction` ends through the guarded root cleanup.

No recoverable prompt is added for malformed developer-authored data.

## Testing

### `DialogueScreenControllerTest`

Migrate the durable `DialogueDialogTest` semantics and add:

- pre-ready `TryStartDialogue(...)` stores data and renders after entering the tree;
- invalid root returns false and emits no premature terminal signal;
- cancel twice emits `DialogueClosed` once;
- outcome then cancel emits only the outcome;
- a second queued terminal choice cannot grant a second flag;
- conditional choices include/exclude the expected buttons;
- leaf renders `Farewell.`;
- progression removes old actions, renders the next set, and focuses a live new action;
- 640×360 long text/choices keep `%Panel` inside the safe frame and make `%BodyScroll` scrollable;
- the scene contains no `AcceptDialog` and keeps the panel bottom-aligned at compact and standard viewports.

### `NpcInteractionControllerTest`

Use a real `UIScreenHost` fixture and inspect the host's `ModalLayer`, not the legacy `_uiParent`:

- Begin hosts exactly one `UIScreenKinds.Dialogue` entry;
- explicit cancel completes once and removes the hosted screen;
- Shop outcome closes Dialogue before legacy Shop opens;
- Heal outcome closes Dialogue before legacy Heal opens;
- missing tree/invalid root creates no hosted entry and completes once;
- forced `Finish()` closes active Dialogue and completes once;
- duplicate-kind/rejected presentation leaves no candidate and completes once.

### Gameplay integration

Extend `GameplayPauseHostTest` / `GameInputLifecycleTest` only for behavior the controller fixture cannot prove:

- NPC interaction hosts Dialogue, blocks gameplay, preserves HUD/world, and does not pause the tree;
- configured keyboard and controller Cancel close Dialogue without opening Pause;
- normal completion restores the interaction prompt and gameplay suppression exactly once;
- freeing/tearing down Game during Dialogue clears the hosted entry and NPC domain flag.

Existing dialogue-domain, Shop, and Heal tests remain green.

## File map

Create:

- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `tests/ui/DialogueScreenControllerTest.cs`

Modify:

- `scripts/ui/NpcInteractionController.cs`
- `scripts/game/Game.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs` when needed for configured physical-input coverage
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Delete after equivalent coverage exists:

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

Audit-only unless a focused failing test proves otherwise:

- `scripts/data/npc/DialogueTree.cs`
- `scripts/data/npc/DialogueCatalog.cs`
- `scripts/data/npc/NpcData.cs`
- `scripts/game/NpcSpawn.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- Theme tokens and `SiriusUiMetrics`
- `ShopDialog` and `HealDialog`

## Scope boundaries

Out of scope:

- Shop/Heal migration (HPA-570)
- Puzzle/Riddle migration (HPA-571)
- reward feedback (HPA-573)
- new dialogue nodes, quest rules, voice acting, portrait production/lookup, typewriter effects, history/log, auto-advance, skip, backlog, speaker animation, or dialogue persistence
- generic interaction service/controller, presenter/view-model layer, navigation service, event bus, or host facade
- new host APIs, theme tokens, metrics, or art assets

## Acceptance mapping

- No desktop-window framing: scene-authored bottom `DialogueScreen` using `SiriusModalShell`.
- Branching/conditions/choices unchanged: current traversal/side-effect logic moves intact and retains domain tests.
- Mouse/keyboard/gamepad: focusable buttons, host initial focus, per-node refocus, and configured Cancel routing.
- Long dialogue readable: one bounded shell scroll owner with a 640×360 regression.
- Cancellation/completion/teardown restore once: screen terminal latch, controller idempotent `Finish()`, guarded root domain-end helper, and host cleanup tests.
- Existing domain behavior green: no dialogue model, condition, catalog, quest, Shop, or Heal changes.