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
- `SiriusModalShell` owns modal panel width, chrome, body-height bounding, and body scrolling.
- `SiriusUiMetrics.SafeFrameInsets(...)` owns the centred safe-frame/max-content-width calculation used by Main Menu, Inventory, and Battle presentation.
- HPA-373 §9.8 specifies a wide bottom Dialogue panel inside the safe frame. The existing 960 px `Large` modal width is not that composition.
- HPA-373 §9.8 also specifies an NPC portrait. Current `NpcData` has no portrait reference; `SpriteType` is world-sprite lookup metadata. Portrait data/art is explicitly deferred to HPA-625 rather than inferred here.

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

- evaluate `DialogueChoice.Condition` through `IDialogueCondition.Evaluate(Character, HashSet<string>)`;
- add `GrantFlag` before progressing or terminating;
- emit `DialogueOutcome` for `OpenShop`, `Heal`, and `CloseAndReturn`;
- emit `DialogueClosed` for explicit cancellation, leaf completion, or a broken `NextNodeId`;
- keep the one-shot terminal latch before any second domain mutation.

It does not own host lifetime and never hides or frees itself from a terminal handler.

### 2. Make pre-tree configuration explicit and one-shot

`NpcInteractionController` configures the unparented screen before passing it to `UIScreenHost`. The screen API is deliberately pre-`_Ready()` safe:

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

`TryStartDialogue(...)`:

1. rejects a second successful start on the same screen instance;
2. validates `tree.Root`;
3. stores the supplied model;
4. marks the screen started only after root validation succeeds;
5. renders immediately only when already ready; otherwise `_Ready()` renders the stored root.

It returns `false` without emitting when the root is invalid or the screen has already been started. The terminal latch is never re-armed by a second `TryStartDialogue(...)` call.

This avoids touching scene nodes before `_Ready()`, avoids terminal emission before `NpcInteractionController` owns a host handle, and avoids accidentally turning a fresh-per-presentation controller into a reusable/re-armable dialogue instance.

Do not implement both pre-attach configuration and a second post-`TryPresent` start protocol. The stored pre-attach path is sufficient because host initial focus is applied on a deferred pass after attachment.

### 3. Extend `SiriusModalShell` with the width class Dialogue actually needs

The prior plan made Dialogue write `SiriusModalShell`'s private `%Panel.CustomMinimumSize` after `RefreshPresentation(...)`. That is the wrong ownership boundary: `Title`, `Severity`, `SizeClass`, and `Compact` all call `RefreshIfReady()`, which can recompute the shell's width later.

HPA-569 therefore adds one width affordance to the component that already owns modal width:

```csharp
public enum SiriusModalSizeClass
{
    Small,
    Medium,
    Large,
    Full
}
```

`Full` means: **fill the width supplied to `SiriusModalShell.RefreshPresentation(...)`, capped at `SiriusUiMetrics.MaximumContentWidth`.** It does not mean fullscreen height and it does not introduce placement behavior.

The existing classes remain unchanged:

- `Small = 420`
- `Medium = 640`
- `Large = 960`

`SiriusUiMetrics.ModalWidth(Full)` returns `MaximumContentWidth` (1600). `SiriusModalShell.RefreshPresentation(...)` handles `Full` before the existing compact/fixed-width branches:

```csharp
var width = SizeClass == SiriusModalSizeClass.Full
    ? Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X)
    : Compact
        ? availableSize.X - SiriusUiMetrics.SafeMargin(true) * 2
        : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X * 0.90f);
```

Dialogue passes an already-safe available width, so `Full` does not need to know safe-frame placement or subtract margins itself.

This extension is justified by the current Dialogue consumer itself. HPA-569 does not add `Full` because a future Shop/Puzzle/Reward screen might use it; those tickets may reuse it only if their concrete layouts fit later.

Because `SiriusModalSizeClass` is a closed tested enum, the contract change includes both:

- `tests/ui/theme/SiriusUiContractsTest.cs` — enum membership and `ModalWidth(Full)`;
- `tests/ui/components/SiriusModalShellTest.cs` — Full width and cached-width stability after a property mutation such as `Title`.

No placement enum or generic safe-frame helper is added to the shell.

### 4. Scene composition owns placement through a real `SafeFrame`

`DialogueScreen.tscn` follows the existing scene-owned safe-frame convention instead of overriding a node inside the `SiriusModalShell.tscn` instance:

```text
DialogueScreen (Control, full rect)
└── SafeFrame (%SafeFrame, Control)
    └── ModalShell (%ModalShell, SiriusModalShell, SizeClass = Full)
        └── Panel/Margin/RootLayout/BodyScroll/BodyHost
            ├── SpeakerLabel (%SpeakerLabel)
            ├── DialogueText (%DialogueText)
            └── ChoicesContainer (%ChoicesContainer)
```

Dialogue adds authored children under the shell instance's body path, matching the working Pause pattern. It does **not** override `%Panel` anchors/properties in the `.tscn`, does not require `[editable path="ModalShell"]`, and does not write `%Panel` geometry at runtime.

Presentation rules:

- no scrim; world context remains visible;
- `%SafeFrame` owns horizontal safe insets and the bottom interaction band;
- modal title is `NpcData.DisplayName`;
- speaker line is `DialogueNode.SpeakerName`, hidden when blank;
- `%DialogueText` is a wrapping `RichTextLabel` with `FitContent = true`, selection disabled, and internal scrolling disabled;
- the shell-owned `%BodyScroll` is the single scroll owner for dialogue text and choices;
- choices are dynamic wrapped `Button`s using `SiriusThemeTypes.SecondaryButton`;
- a leaf renders one themed `Farewell.` secondary action;
- old dynamic actions are removed from `%ChoicesContainer` immediately before `QueueFree()`, so stale buttons cannot remain in focus/layout order for another frame.

The Dialogue controller does not bind `%Panel` or `%BodyScroll`. Tests may inspect those shell-owned nodes through `%ModalShell` when they need layout/scroll evidence.

### 5. Dialogue owns one local safe-frame/height policy

`DialogueScreenController.RefreshLayout()` owns only the Dialogue-specific placement that the shared shell intentionally does not know.

Use:

```csharp
private const float StandardDialogueHeightFraction = 0.45f;
```

This value is local to Dialogue. It is grounded by the approved 1280×720 wireframe, whose standard Dialogue panel occupies roughly 45% of the safe-frame height. It is not added to `SiriusUiMetrics`.

Layout algorithm:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady())
        return;

    var size = GetViewportRect().Size;
    var insets = SiriusUiMetrics.SafeFrameInsets(size);
    var safeHeight = Mathf.Max(0f, size.Y - insets.Margin * 2f);
    var bandHeight = insets.Compact
        ? safeHeight
        : safeHeight * StandardDialogueHeightFraction;
    var contentWidth = Mathf.Max(0f, size.X - insets.SideInset * 2f);

    _safeFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    _safeFrame.OffsetLeft = insets.SideInset;
    _safeFrame.OffsetTop = size.Y - insets.Margin - bandHeight;
    _safeFrame.OffsetRight = -insets.SideInset;
    _safeFrame.OffsetBottom = -insets.Margin;

    _shell.Compact = insets.Compact;
    _shell.RefreshPresentation(new Vector2(contentWidth, bandHeight));

    _speakerLabel.ThemeTypeVariation = insets.Compact
        ? SiriusThemeTypes.SectionCompact
        : SiriusThemeTypes.Section;
    _textLabel.ThemeTypeVariation = insets.Compact
        ? SiriusThemeTypes.BodyCompact
        : SiriusThemeTypes.Body;

    var minimumTarget = SiriusUiMetrics.MinimumTarget(insets.Compact);
    foreach (var child in _choicesContainer.GetChildren())
    {
        if (child is Button action)
            action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
    }
}
```

Standard mode caps the Dialogue region to the lower 45% of the safe frame. Compact mode uses the full safe-frame height and relies on the shell body scroll so long content can grow upward without losing actions.

The tests assert the standard panel remains inside that lower band at both 1280×720 and 1920×1080. A nearly full-screen standard Dialogue is therefore a regression even if its width and bottom edge are otherwise valid.

### 6. HPA-373 portrait requirement is explicitly deferred, not silently claimed

HPA-569 implements the §9.8 geometry, speaker identity text, dialogue body, choices, focus, and overflow behavior, but **does not complete the portrait portion of §9.8**.

Reason:

- current `NpcData` has no portrait contract;
- HPA-569 explicitly excludes portrait production;
- inferring portrait semantics from `SpriteType` would couple UI identity art to world-sprite metadata.

Follow-up HPA-625 owns:

- an explicit optional NPC portrait reference/data seam;
- portrait artwork/integration for current NPCs;
- Dialogue portrait rendering when authored;
- compact portrait reduction.

HPA-569 must not be used as evidence that the HPA-373 portrait requirement is complete.

## Focus and input

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

Dialogue does not pause the tree. The NPC interaction flag still owns domain suppression, while the host lease makes the active presentation block explicit. Configured Cancel is consumed by Dialogue and cannot fall through to Pause.

No new host API, kind, exclusive group, parent relationship, or policy factory is required.

## Interaction flow

### Start

Final production flow:

1. `Game.OnNpcInteracted` resolves the authored NPC.
2. It verifies `_screenHost` is non-null and valid **before** `StartNpcInteraction()`.
3. `GameManager.StartNpcInteraction()` runs once.
4. `Game` creates `NpcInteractionController`, passing the validated host and existing `UI` parent.
5. `NpcInteractionController.Begin()` resolves the dialogue tree.
6. Missing tree or invalid root logs and calls `Finish()` once with no presentation.
7. Valid data instantiates `DialogueScreen.tscn`, calls one-shot pre-ready `TryStartDialogue(...)`, wires terminal signals, and presents the candidate.
8. Rejected presentation disconnects/frees the candidate and calls `Finish()`.
9. Successful presentation stores the returned handle and screen reference.

The constructor change, production `Game` caller, and null-host guard land in the same implementation task. There is no intermediate `_screenHost!` bridge or known latent null dereference.

### Normal progression

Choice presses stay entirely in `DialogueScreenController` until terminal. `ShowNode(...)` copies the retired behavior, including:

```csharp
if (choice.Condition.Evaluate(_player!, _questFlags!))
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

`Game._ExitTree()` currently unsubscribes `InteractionComplete` before calling `Finish()`. HPA-569 therefore performs controller cleanup and then calls `EndNpcInteractionIfActive()` explicitly. The same helper is used by startup failure and reset fallback so teardown cannot leave the domain flag latched.

## Error handling

Keep failures local and terminal:

- missing host before NPC start: log and return without setting the domain interaction flag;
- missing dialogue tree: controller log + `Finish()`;
- null root: `TryStartDialogue(...) == false`, controller log + `Finish()`;
- repeated `TryStartDialogue(...)`: reject without resetting the terminal latch;
- broken `NextNodeId`: screen log + `DialogueClosed` once;
- scene load/instantiate failure: controller log + `Finish()`;
- host `TryPresent` failure: disconnect/free rejected candidate + `Finish()`;
- repeated terminal input: ignored before a second `GrantFlag` or terminal emission;
- Game teardown: hosted presentation cleanup runs and the guarded root helper ends any remaining NPC domain flag.

No recoverable prompt is added for malformed developer-authored dialogue data.

## Testing

### Shared shell extension

Add focused coverage before Dialogue work:

- `SiriusModalSizeClass` contains `Full` in the closed enum contract;
- `ModalWidth(Full) == MaximumContentWidth`;
- `Full` fills the supplied safe width instead of using the 960 px `Large` cap;
- setting `Title` after a constrained `Full` refresh retains the cached Full width;
- existing Small/Medium/Large and compact behavior remain unchanged.

### `DialogueScreenControllerTest`

Migrate durable `DialogueDialogTest` semantics and add:

- pre-ready `TryStartDialogue(...)` stores data and renders after entering the tree;
- invalid root returns false and emits no premature terminal signal;
- a second valid start is rejected and cannot re-arm a spent terminal latch;
- cancel twice emits `DialogueClosed` once;
- outcome then cancel emits only the outcome;
- a second queued terminal choice cannot grant a second flag;
- conditional choices exercise `Condition.Evaluate(...)`;
- leaf renders one themed `Farewell.` secondary action;
- progression removes old actions before rendering/focusing the next set;
- the scene uses `%SafeFrame -> %ModalShell` and contains no `[editable]` dependency or `AcceptDialog`;
- Full width remains correct after the title is applied;
- at 1280×720 and 1920×1080 the panel remains inside the lower 45% standard Dialogue band;
- at 640×360 the safe frame expands vertically and the shell-owned body scroll remains usable/follow-focuses the final choice.

### `NpcInteractionControllerTest`

Use `UIScreenHostTestSupport.CreateHost(...)`. Inspect `ModalLayer` and `ActiveEntries` rather than rebuilding host setup or searching the legacy `_uiParent` for a host-owned screen:

- Begin hosts exactly one `UIScreenKinds.Dialogue` entry;
- explicit cancel completes once and removes the hosted screen;
- Shop outcome closes Dialogue before legacy Shop opens;
- Heal outcome closes Dialogue before legacy Heal opens;
- missing tree/invalid root creates no hosted entry and completes once;
- forced `Finish()` closes active Dialogue and completes once;
- duplicate-kind/rejected presentation leaves no candidate and completes once.

### Production gameplay integration

Keep the existing flag-only NPC Cancel regression because it documents the native Shop/Heal phase where `IsInNpcInteraction` is true but no hosted Dialogue entry exists.

For hosted Dialogue, use real `Game.tscn` plus an authored Floor GF `NpcSpawn`:

1. obtain `FloorManager.CurrentGridMap`;
2. find a current-floor `NpcSpawn` such as `village_shopkeeper`;
3. read the current `_tilemapOrigin` with the existing reflection helper;
4. derive `internalPosition = spawn.GridPosition - origin`;
5. assert `grid.InternalGridToTilemapCoords(internalPosition) == spawn.GridPosition`;
6. invoke private `Game.OnNpcInteracted(internalPosition)`;
7. assert `UIScreenKinds.Dialogue` is active before exercising Cancel/completion/teardown.

Production regressions cover:

- Dialogue blocks gameplay without pausing the tree;
- configured keyboard/controller Cancel closes Dialogue and never opens Pause in the same action;
- normal terminal completion restores gameplay/prompt state;
- Game teardown ends the NPC interaction even after `InteractionComplete` has been unsubscribed.

## File map

### Shared shell extension

Modify:

- `scripts/ui/theme/SiriusUiTypes.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `tests/ui/theme/SiriusUiContractsTest.cs`
- `tests/ui/components/SiriusModalShellTest.cs`

### Dialogue migration

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
- Theme resources/tokens beyond the size enum/metric change
- `UIScreenHost`, `UIScreenKinds`, `ShopDialog`, and `HealDialog`

## Scope boundaries

Out of scope:

- Shop/Heal migration (HPA-570)
- Puzzle/Riddle migration (HPA-571)
- reward feedback (HPA-573)
- NPC portrait contract/art/rendering (HPA-625)
- new dialogue nodes, quest rules, voice acting, typewriter effects, history/log, auto-advance, skip, backlog, speaker animation, or dialogue persistence
- generic interaction service/controller, presenter/view-model layer, navigation service, event bus, or host facade
- shell placement APIs, new Theme tokens/art, or new host APIs/kinds

## Acceptance mapping

- No desktop-window framing: scene-authored bottom `DialogueScreen` using `SiriusModalShell` inside a Dialogue-owned safe-frame band.
- Wide-bottom composition: `SiriusModalSizeClass.Full` fills the supplied safe width; Dialogue owns bottom placement and a standard 45% height cap.
- Branching/conditions/choices unchanged: current traversal and side-effect ordering move intact with `Condition.Evaluate(...)`.
- Mouse/keyboard/gamepad: focusable themed buttons, host initial focus, per-node refocus, and configured Cancel routing.
- Long dialogue readable: one bounded shell scroll owner with compact 640×360 coverage.
- Cancellation/completion/teardown restore once: screen terminal latch, one-shot start, controller idempotent `Finish()`, guarded root domain-end helper, and host cleanup tests.
- Portrait: intentionally **not complete** under HPA-569; HPA-625 owns that remaining HPA-373 §9.8 requirement.
- Existing domain behavior green: no dialogue model, condition, catalog, quest, Shop, or Heal changes.