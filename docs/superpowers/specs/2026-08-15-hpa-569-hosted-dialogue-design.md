# HPA-569 Hosted Dialogue Design

## Goal

Replace the runtime-built `DialogueDialog : AcceptDialog` with one scene-authored, Sirius-themed, host-managed Dialogue surface while preserving the existing dialogue-tree and NPC-interaction semantics.

This is a presentation migration only. HPA-570 continues to own Shop and Heal presentation.

## Why this is the next slice

The shared Theme, `SiriusModalShell`, gameplay `UIScreenHost`, Pause, Settings, Save/Load, Inventory, Battle, and hosted Prompt work are complete. HPA-569 is the first remaining unblocked interaction surface in the Sirius delivery order; its HPA-382 prerequisite is complete. Shop/Heal, Puzzle/Riddle, and Reward remain independent later slices.

## Current state

- `DialogueDialog` owns both dialogue-tree traversal and runtime UI construction.
- `NpcInteractionController` owns the interaction sequence: Dialogue first, then optional Shop/Heal, then exactly-once completion.
- `Game` owns the production gameplay `UIScreenHost` and already suppresses direct gameplay while `GameManager.IsInNpcInteraction` is true.
- `UIScreenKinds.Dialogue` already exists.
- `SiriusModalShell` already provides themed modal chrome, compact presentation, bounded body scrolling, and follow-focus scrolling.
- `SiriusUiMetrics.SafeFrameInsets(...)` already owns the centred maximum-content-width calculation used by full-screen gameplay presentation.
- The approved HPA-373 Dialogue composition is a **wide bottom panel centred inside the safe frame**, not a centred Pause-style modal.
- `NpcData.SpriteType` is world-sprite lookup metadata. There is no portrait texture/reference contract.

## Options considered

### A. Keep `NpcInteractionController` as orchestration owner and give it the gameplay host — selected

`NpcInteractionController` instantiates the scene-authored Dialogue screen and presents it through `UIScreenHost`. It remains responsible for terminal signals, transition to legacy Shop/Heal, and final cleanup.

This is the smallest coherent change because it preserves the existing interaction boundary, keeps `Game` out of dialogue-tree progression, and leaves HPA-570 a straightforward follow-up.

### B. Move Dialogue hosting and terminal routing into `Game`

Rejected. It would split one NPC interaction across `Game` and `NpcInteractionController`, requiring callbacks or exposed state solely to route Dialogue outcomes.

### C. Add a generic interaction presenter/service

Rejected as speculative. Dialogue, Shop, and Heal have different behavior, and `UIScreenHost` is already the reusable lifecycle primitive.

## Architecture

### 1. Replace the native window with one scene-backed `Control`

Create:

- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `tests/ui/DialogueScreenControllerTest.cs`

Delete after the hosted path is green:

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

`DialogueScreenController` remains the single owner of dialogue-node presentation and current choice semantics:

- evaluate `DialogueChoice.Condition` through the existing `Evaluate(Character, HashSet<string>)` API;
- add `GrantFlag` before progressing or terminating;
- emit `DialogueOutcome` for `OpenShop`, `Heal`, and `CloseAndReturn`;
- emit `DialogueClosed` for explicit cancellation, leaf completion, or a broken `NextNodeId`;
- keep the one-shot terminal latch before any second domain mutation.

It does not own host lifetime and never hides or frees itself from a terminal handler.

### 2. Make pre-tree configuration explicit

`NpcInteractionController` configures the unparented screen before passing it to `UIScreenHost`. The screen API is therefore deliberately pre-`_Ready()` safe:

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

This avoids touching scene nodes before `_Ready()` and avoids emitting a terminal signal before `NpcInteractionController` owns a host handle. Invalid-root completion remains an orchestration responsibility: the controller logs and calls its idempotent `Finish()` once.

Do not implement both pre-attach configuration and a second post-`TryPresent` start protocol. The stored pre-attach path is sufficient because host initial focus is applied on a deferred pass after attachment.

### 3. Scene composition uses shell internals without copying their ownership contract

`DialogueScreen.tscn` uses one full-rect root and one `SiriusModalShell`. Static Dialogue content is authored under the shell body.

Unique names owned by `DialogueScreen.tscn`:

- `%ModalShell`
- `%SpeakerLabel`
- `%DialogueText`
- `%ChoicesContainer`

`%Panel` and `%BodyScroll` remain unique names owned by the instantiated `SiriusModalShell.tscn`. The Dialogue controller resolves them **through the shell instance**, not from the Dialogue root:

```csharp
_shell = GetNode<SiriusModalShell>("%ModalShell");
_panel = _shell.GetNode<PanelContainer>("%Panel");
_bodyScroll = _shell.GetNode<ScrollContainer>("%BodyScroll");
_speakerLabel = GetNode<Label>("%SpeakerLabel");
_textLabel = GetNode<RichTextLabel>("%DialogueText");
_choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");
```

Presentation rules:

- no scrim; world context remains visible;
- the shell panel receives a scene-local bottom-centre anchor override and grows upward from the bottom edge;
- modal title is `NpcData.DisplayName`;
- speaker line is `DialogueNode.SpeakerName`, hidden when blank;
- no portrait node, asset, or model convention is added;
- `%DialogueText` is a wrapping `RichTextLabel` with `FitContent = true`, selection disabled, and internal scrolling disabled;
- the shell-owned `%BodyScroll` is the **single** scroll owner for dialogue text and choices;
- choices are dynamic wrapped `Button`s in a vertical container;
- a leaf renders one `Farewell.` action, preserving current behavior;
- old dynamic actions are removed from `%ChoicesContainer` immediately before `QueueFree()`, so stale buttons cannot remain in focus/layout order for another frame.

### 4. Dialogue owns a narrow local `RefreshLayout`, not a new shell placement API

`SiriusModalShell.SizeClass = Large` is not the final Dialogue-width policy. Its 960 px token is a centred-modal width and would under-build the approved wide Dialogue surface on 1280+ viewports.

`DialogueScreenController.RefreshLayout()` reuses the two existing contracts directly:

1. Pause/Settings compact behavior: set `_shell.Compact` before calling `_shell.RefreshPresentation(size)`.
2. Battle/safe-frame behavior: derive the final Dialogue width and bottom inset from `SiriusUiMetrics.SafeFrameInsets(size)`.

Final local policy:

```csharp
private void RefreshLayout()
{
    var size = GetViewportRect().Size;
    var insets = SiriusUiMetrics.SafeFrameInsets(size);

    _shell.Compact = insets.Compact;
    _shell.RefreshPresentation(size);

    var contentWidth = Mathf.Max(0f, size.X - insets.SideInset * 2f);
    _panel.CustomMinimumSize = new Vector2(
        contentWidth,
        _panel.CustomMinimumSize.Y);
    _panel.OffsetBottom = -insets.Margin;

    var minimumTarget = SiriusUiMetrics.MinimumTarget(insets.Compact);
    foreach (var button in _choicesContainer.GetChildren())
    {
        if (button is Button action)
            action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
    }
}
```

The scene-authored bottom anchor and grow direction remain local to `DialogueScreen.tscn`. `SafeFrameInsets` already caps content to `MaximumContentWidth`, so the panel becomes wide at ordinary/ultrawide sizes without moving to distant screen edges. No `Placement` enum, shell API, Theme token, or metric is added.

At 640×360, the same method sets compact mode, uses 12 px safe margins, and keeps the body scroll bounded. At larger viewports the surface stays bottom-aligned and wide rather than becoming a Pause modal moved to the lower edge.

### 5. Focus stays local and deterministic

- Host initial focus resolves from `InitialFocusTarget` on the deferred host focus pass, after `_Ready()` renders the stored root.
- Every nonterminal node transition updates `InitialFocusTarget` and defers focus to the first newly rendered action.
- The shell's existing follow-focus behavior scrolls a focused long-choice action into view.
- Mouse, keyboard, and gamepad use ordinary Godot `Button` behavior.
- No selection model or manual directional graph is added unless a focused runtime test proves the vertical container order insufficient.

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

Dialogue does not pause the tree. The existing NPC interaction flag still owns domain suppression, while the host lease makes the active presentation block explicit. Configured Cancel is consumed by Dialogue and cannot fall through to Pause.

No new host API, kind, exclusive group, parent relationship, or policy factory is required.

## Interaction flow

### Start

Final production flow:

1. `Game.OnNpcInteracted` verifies a valid gameplay host before setting `IsInNpcInteraction`.
2. `GameManager.StartNpcInteraction()` runs once.
3. `Game` creates `NpcInteractionController`, passing the already-validated host and existing `UI` parent.
4. `NpcInteractionController.Begin()` resolves the dialogue tree.
5. Missing tree or invalid root logs and calls `Finish()` once with no presentation.
6. Valid data instantiates `DialogueScreen.tscn`, wires terminal signals, and calls pre-ready-safe `TryStartDialogue(...)`.
7. The controller presents the configured candidate through the host.
8. Rejected presentation disconnects/frees the candidate and calls `Finish()`.
9. Successful presentation stores the returned handle and screen reference.

Implementation sequencing must keep every intermediate commit buildable: the `NpcInteractionController` constructor change and the sole production `Game` constructor call move together. The stronger pre-`StartNpcInteraction` null-host guard remains the subsequent production-lifecycle slice.

### Normal progression

Choice presses stay entirely in `DialogueScreenController` until terminal. `ShowNode(...)` copies the retired behavior, including:

```csharp
if (choice.Condition.Evaluate(_player, _questFlags))
    visibleChoices.Add(choice);
```

Do not introduce an `IsMet` alias or wrapper.

### Dialogue → Shop/Heal

On `OpenShop` or `Heal`:

1. capture the outcome;
2. close the hosted Dialogue entry synchronously;
3. after the host close transaction, open the existing `ShopDialog` or `HealDialog` through the current controller code;
4. keep `GameManager.IsInNpcInteraction` true across the transition.

Dialogue is terminal before Shop/Heal opens, so no host parent relationship is introduced. HPA-570 later replaces those native surfaces.

### Completion and teardown

All terminal paths converge on exactly-once orchestration:

- the screen emits one terminal signal;
- the controller closes or clears the hosted Dialogue entry;
- `Finish()` remains idempotent;
- `InteractionComplete` fires once;
- `Game.OnNpcInteractionComplete` clears the interaction through a guarded `EndNpcInteractionIfActive()` helper and refreshes gameplay presentation.

`Game._ExitTree()` currently unsubscribes `InteractionComplete` before calling `Finish()`. HPA-569 therefore must perform controller cleanup and then call `EndNpcInteractionIfActive()` explicitly. The same helper is used by startup failure and reset fallback so teardown cannot leave the domain flag latched.

## Error handling

Keep failures local and terminal:

- missing dialogue tree: controller log + `Finish()`;
- null root: `TryStartDialogue(...) == false`, controller log + `Finish()`;
- broken `NextNodeId`: screen log + `DialogueClosed` once;
- scene load/instantiate failure: controller log + `Finish()`;
- host `TryPresent` failure: disconnect/free rejected candidate + `Finish()`;
- repeated terminal input: ignored before a second `GrantFlag` or terminal emission;
- Game teardown: hosted presentation cleanup runs and the guarded root helper ends any remaining NPC domain flag.

No recoverable prompt is added for malformed developer-authored dialogue data.

## Testing

### `DialogueScreenControllerTest`

Migrate the durable `DialogueDialogTest` semantics and add:

- pre-ready `TryStartDialogue(...)` stores data and renders after entering the tree;
- invalid root returns false and emits no premature terminal signal;
- cancel twice emits `DialogueClosed` once;
- outcome then cancel emits only the outcome;
- a second queued terminal choice cannot grant a second flag;
- conditional choices exercise `Condition.Evaluate(...)` and include/exclude the expected buttons;
- leaf renders `Farewell.`;
- progression removes old actions, renders the next set, and focuses a live new action;
- compact/standard layout explicitly refreshes `_shell.Compact`;
- final panel width equals the safe-frame content width, bottom offset equals the safe margin, and the panel is not vertically centred;
- 640×360 long text/choices make the shell-owned `%BodyScroll` scroll and follow focus;
- the scene contains no `AcceptDialog`.

### `NpcInteractionControllerTest`

Use the existing `UIScreenHostTestSupport.CreateHost(...)` fixture instead of re-describing host construction. Inspect the returned host's `ModalLayer` and `ActiveEntries`:

- Begin hosts exactly one `UIScreenKinds.Dialogue` entry;
- explicit cancel completes once and removes the hosted screen;
- Shop outcome closes Dialogue before legacy Shop opens;
- Heal outcome closes Dialogue before legacy Heal opens;
- missing tree/invalid root creates no hosted entry and completes once;
- forced `Finish()` closes active Dialogue and completes once;
- duplicate-kind/rejected presentation leaves no candidate and completes once.

### Production gameplay integration

Do not model a real Dialogue by calling only `GameManager.StartNpcInteraction()`. Keep that existing flag-only test because it documents Pause decline while a native Shop/Heal child owns input after Dialogue has closed.

For hosted Dialogue integration, use the real `Game.tscn` fixture and the authored Floor GF `NpcSpawn`:

1. obtain `FloorManager.CurrentGridMap`;
2. find a current-floor `NpcSpawn` such as `village_shopkeeper`;
3. derive the internal grid coordinate from the current `_tilemapOrigin` using existing reflection helpers;
4. assert `grid.InternalGridToTilemapCoords(internalPosition) == spawn.GridPosition`;
5. invoke private `Game.OnNpcInteracted(internalPosition)` with the existing reflection helper;
6. assert `UIScreenKinds.Dialogue` is active before testing Cancel, normal completion, or teardown.

This exercises the production route that resolves `NpcSpawn`, starts the domain interaction, constructs the controller, and presents Dialogue.

Production regressions cover:

- Dialogue blocks gameplay without pausing the tree;
- configured keyboard/controller Cancel closes Dialogue and never opens Pause in the same action;
- normal terminal completion restores gameplay/prompt state;
- detaching/tearing down Game while Dialogue is active clears the NPC interaction flag even though `InteractionComplete` has been unsubscribed.

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
- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

Delete after equivalent coverage exists:

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

Audit-only unless a focused failing test proves otherwise:

- `scripts/data/npc/DialogueTree.cs`
- `scripts/data/npc/DialogueCatalog.cs`
- `scripts/data/npc/DialogueCondition.cs`
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
- new host APIs, Theme tokens, metrics, or art assets

## Acceptance mapping

- No desktop-window framing: scene-authored wide bottom `DialogueScreen` using `SiriusModalShell`.
- Branching/conditions/choices unchanged: current traversal and `Condition.Evaluate(...)` behavior move intact.
- Mouse/keyboard/gamepad: focusable buttons, host initial focus, per-node refocus, and configured Cancel routing.
- Long dialogue readable: one bounded shell scroll owner with a 640×360 regression.
- Cancellation/completion/teardown restore once: screen terminal latch, controller idempotent `Finish()`, guarded root domain-end helper, and production-route cleanup tests.
- Existing domain behavior green: no dialogue model, condition, catalog, quest, Shop, or Heal changes.
