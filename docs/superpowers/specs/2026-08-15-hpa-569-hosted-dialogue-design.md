# HPA-569 Hosted Dialogue Design

## Goal

Replace the runtime-built `DialogueDialog : AcceptDialog` with one scene-authored, Sirius-themed, host-managed dialogue surface while preserving the existing dialogue-tree and NPC-interaction semantics.

This is a presentation migration only. HPA-570 continues to own Shop/Heal presentation.

## Current state

- `DialogueDialog` owns both dialogue-tree traversal and runtime UI construction.
- `NpcInteractionController` owns the interaction sequence: dialogue first, then optional Shop/Heal, then exactly-once completion.
- `Game` owns the production gameplay `UIScreenHost` and already blocks direct gameplay input while `GameManager.IsInNpcInteraction` is true.
- `UIScreenKinds.Dialogue` already exists.
- `SiriusModalShell` already provides responsive width, bounded body scrolling, actions, theme styling, and focus-friendly body scrolling.
- Current `NpcData` exposes `DisplayName` and `SpriteType`, but no portrait texture/reference contract. No new portrait lookup or asset convention is introduced in this ticket.

## Options considered

### A. Keep `NpcInteractionController` as orchestration owner and give it the gameplay host — selected

`NpcInteractionController` instantiates the scene-authored dialogue screen and presents it through `UIScreenHost`. It remains responsible for dialogue terminal signals, transition to legacy Shop/Heal, and final cleanup.

Why this is preferred:

- preserves the existing interaction boundary;
- keeps `Game` from learning dialogue-tree progression details;
- makes the HPA-569 cutover local to the existing owner;
- allows HPA-570 to migrate Shop/Heal through the same controller later without introducing a new framework now.

### B. Move dialogue hosting and terminal routing into `Game`

This would keep all host handles in the scene root, but it would split one NPC interaction across `Game` and `NpcInteractionController`, forcing new callbacks or state exposure for dialogue outcomes. That is more coupling for no product benefit.

### C. Add a generic interaction host/service

Rejected as speculative. Dialogue, Shop, and Heal have different behaviors and HPA-570 has not demonstrated a reusable abstraction beyond the already-existing `UIScreenHost`.

## Architecture

### `DialogueScreenController`

Replace `DialogueDialog.cs` with `DialogueScreenController.cs` and add `scenes/ui/DialogueScreen.tscn`.

The controller remains the single owner of dialogue-node presentation and the existing domain-affecting choice semantics:

- evaluate `DialogueChoice.Condition`;
- add `GrantFlag` before progressing/terminating;
- emit `DialogueOutcome` for `OpenShop`, `Heal`, and `CloseAndReturn`;
- emit `DialogueClosed` for explicit cancellation, leaf completion, invalid root, or broken `NextNodeId`;
- preserve the current one-shot terminal latch so a second queued choice cannot grant another flag after termination.

It must not own host lifetime or directly `QueueFree()` itself.

Public presentation seam:

```csharp
public partial class DialogueScreenController : Control
{
    [Signal] public delegate void DialogueOutcomeEventHandler(int outcome);
    [Signal] public delegate void DialogueClosedEventHandler();

    public Control? InitialFocusTarget { get; }

    public void StartDialogue(
        NpcData npc,
        DialogueTree tree,
        Character player,
        HashSet<string> questFlags);

    public void RequestCancel();
}
```

`RequestCancel()` routes through the same one-shot `EmitClosedOnce()` path as visible completion/cancel actions.

### Scene structure

`DialogueScreen.tscn` uses the existing Sirius Theme and one `SiriusModalShell` rather than another shell.

Stable named nodes:

- `%ModalShell`
- `%SpeakerLabel`
- `%DialogueText`
- `%ChoicesContainer`

Presentation rules:

- modal title: NPC `DisplayName`;
- speaker line: current node `SpeakerName`, hidden when blank;
- portrait: omitted for HPA-569 because current domain data has no portrait contract; do not infer a portrait from `SpriteType` or add assets;
- body: `RichTextLabel` with wrapping and selection disabled;
- choices: explicit Buttons in a vertical container, word-wrapped;
- leaf node: one `Farewell.` action, preserving current behavior;
- long text and long choice sets scroll inside the existing `SiriusModalShell` body at the minimum viewport;
- initial focus: first currently visible choice/leaf action after `StartDialogue`; no extra selection model.

The scene may use a script-authored dynamic set of choice Buttons because the number and labels are dialogue data. Static chrome stays scene-authored.

## Hosting policy

`NpcInteractionController` receives the existing gameplay `UIScreenHost` in addition to the existing UI parent used by legacy Shop/Heal during this ticket.

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
        return UIInputInterception.Consumed;
    },
    Cleanup = _ => ClearDialoguePresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
}
```

Rationale:

- NPC dialogue did not historically pause the scene tree, so `PauseTree` stays false;
- the existing NPC interaction flag already suppresses gameplay, while host blocking makes the presentation contract explicit and prevents root fallback from competing;
- HUD remains visible beneath the modal, matching the existing in-world interaction presentation;
- `Cancel = Consume` plus `InterceptCancel -> RequestCancel()` makes configured Cancel and visible terminal actions share the screen's one-shot latch.

No new `UIScreenHost` API or screen kind is required.

## Interaction flow

### Start

1. `Game.OnNpcInteracted` calls `GameManager.StartNpcInteraction()` as today.
2. `Game` creates `NpcInteractionController`, passing `_screenHost` and the existing `UI` parent.
3. `NpcInteractionController.Begin()` resolves the dialogue tree.
4. Missing tree ends the interaction once and creates no presentation.
5. Valid tree instantiates `DialogueScreen.tscn`, wires terminal signals, and asks the host to present it.
6. Host-presentation failure logs an error and calls `Finish()` so the NPC interaction cannot remain latched.

### Normal progression

Choice presses stay entirely in `DialogueScreenController` until a terminal outcome is reached.

### Dialogue → Shop/Heal

On `OpenShop` or `Heal`:

1. capture the outcome;
2. close the hosted Dialogue entry;
3. after the host close transaction, open the existing `ShopDialog` or `HealDialog` using the current controller code;
4. keep `GameManager.IsInNpcInteraction` true across the transition;
5. HPA-570 later replaces these two native dialogs without changing the HPA-569 dialogue contract.

Do not introduce a host parent relationship between Dialogue and Shop/Heal: Dialogue is terminal before those surfaces open.

### Cancel/completion/invalid data

All terminal paths converge on existing exactly-once controller cleanup:

- screen emits one terminal signal;
- controller closes/clears the hosted dialogue handle;
- `Finish()` remains idempotent;
- `InteractionComplete` fires once;
- `Game.OnNpcInteractionComplete` remains the owner that calls `GameManager.EndNpcInteraction()` and refreshes the exploration prompt.

Teardown calling `Finish()` must close any active hosted dialogue without requiring another terminal signal.

## Focus and input

- host requests first visible dialogue action after the screen has been populated;
- each node transition explicitly focuses the first newly rendered choice/leaf action;
- queued old choice nodes are removed before focus moves to the new choices;
- mouse, keyboard, and gamepad all use normal Godot Button behavior;
- configured Cancel is consumed by the active Dialogue screen and must never fall through to Pause in the same input event;
- closing Dialogue restores gameplay ownership; there is no previous UI control to restore for direct NPC interaction.

No manual directional focus graph is added unless a failing runtime test proves Godot's vertical container order is insufficient.

## Error handling

Keep failures local and terminal:

- missing dialogue tree: existing `NpcInteractionController` error + `Finish()`;
- null root: screen emits `DialogueClosed` once;
- broken `NextNodeId`: screen logs and emits `DialogueClosed` once;
- scene load/instantiate failure: controller logs and `Finish()`;
- host `TryPresent` failure: controller disconnects/frees the unhosted screen and `Finish()`;
- repeated terminal input: ignored by the existing terminal latch before any second domain side effect.

No recoverable prompt is added for malformed content; this is developer-authored data and the existing behavior already terminates safely.

## Testing

### `DialogueScreenControllerTest`

Migrate the current `DialogueDialogTest` semantics and add presentation coverage:

- cancel then second terminal path emits `DialogueClosed` once;
- outcome then cancel emits only the outcome;
- second queued terminal choice cannot grant a second quest flag;
- conditional choices include/exclude the correct Buttons;
- leaf renders `Farewell.`;
- node progression replaces the choice set and focuses a new action;
- long dialogue / long choices at 640×360 remain bounded by the modal and produce a scrollable body.

### `NpcInteractionControllerTest`

Use a real `UIScreenHost` fixture:

- Begin hosts exactly one `UIScreenKinds.Dialogue` entry;
- configured/explicit dialogue cancel completes once and frees the hosted screen;
- Shop outcome closes Dialogue before legacy Shop opens;
- Heal outcome closes Dialogue before legacy Heal opens;
- missing tree creates no hosted entry and completes once;
- forced `Finish()` closes an active Dialogue and emits completion once;
- host-presentation failure does not leave the interaction active.

### Gameplay integration

Extend `GameplayPauseHostTest` / `GameInputLifecycleTest` only for root-level behavior that the controller fixture cannot prove:

- interacting with an NPC hosts Dialogue and blocks gameplay without pausing the tree;
- configured Cancel closes Dialogue and does not open Pause in the same action;
- closing the interaction restores gameplay/prompt behavior.

Existing dialogue-domain tests remain green.

## File map

Create:

- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `tests/ui/DialogueScreenControllerTest.cs`

Modify:

- `scripts/ui/NpcInteractionController.cs`
- `scripts/game/Game.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs` and/or `tests/game/GameInputLifecycleTest.cs` only for the production host/input contract
- `docs/ui/hpa-376/ui-lifecycle-contract.md` to replace the native Dialogue lifecycle rows with the final hosted contract

Delete:

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

No changes are planned to `DialogueTree`, `DialogueCatalog`, conditions, `NpcData`, `UIScreenHost`, `UIScreenKinds`, Theme tokens, metrics, save data, quest persistence, Shop, or Heal.

## Scope boundaries

Out of scope:

- Shop/Heal migration (HPA-570)
- Puzzle/riddle migration (HPA-571)
- reward feedback (HPA-573)
- new dialogue nodes, quest rules, voice acting, portraits, portrait lookup conventions, typewriter effects, history/log, auto-advance, skip, backlog, speaker animation, or dialogue persistence
- generic interaction service/controller, presenter/view-model layer, navigation service, event bus, or host facade
- new host APIs, theme tokens, metrics, or art assets

## Acceptance mapping

- No desktop-window framing: `DialogueScreen.tscn` + `SiriusModalShell`.
- Branching/conditions/choices unchanged: existing traversal logic moves intact into `DialogueScreenController` and retains domain tests.
- Mouse/keyboard/gamepad: normal focusable Buttons plus host initial focus and configured Cancel routing.
- Long dialogue readable: bounded modal body with 640×360 scroll regression.
- Cancellation/completion restore once: screen terminal latch + controller idempotent `Finish()` + host close tests.
- Existing domain behavior green: no changes to dialogue data model/evaluation contracts.
