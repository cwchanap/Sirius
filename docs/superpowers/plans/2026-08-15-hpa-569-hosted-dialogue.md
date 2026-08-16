# HPA-569 Hosted Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `DialogueDialog` with one scene-authored bottom Dialogue surface hosted by the gameplay `UIScreenHost`, preserving current dialogue-tree behavior and exactly-once NPC-interaction cleanup.

**Architecture:** Keep `NpcInteractionController` as the single Dialogue → Shop/Heal orchestration owner. Move the existing traversal/choice logic into a scene-backed `Control`, configure it safely before it enters the scene tree, present it through the existing host, and leave Shop/Heal native for HPA-570. `Game` remains the domain-flag/root lifecycle owner and gains only the host guard plus teardown-safe interaction ending.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme, `SiriusModalShell`, and `UIScreenHost`.

## Global constraints

- Reuse `SiriusModalShell`; do not add another shell or modal framework.
- Reuse `UIScreenKinds.Dialogue`; do not add host kinds, host APIs, policy factories, or exclusive groups.
- Keep `NpcInteractionController` as orchestration owner; do not move dialogue progression into `Game`.
- Preserve condition evaluation, branching, `GrantFlag`, outcomes, leaf completion, and exactly-once terminal/domain side effects.
- Dialogue never pauses the scene tree.
- Keep world and gameplay HUD visible beneath the Dialogue surface.
- Follow the HPA-373 bottom interaction composition; do not leave the shell centered like a desktop dialog.
- Use the shell's existing body scroll as the only scroll owner.
- Do not add portrait assets, portrait model fields, or infer a portrait contract from `NpcData.SpriteType`.
- Shop and Heal remain native dialogs in this ticket.
- No presenter, view model, interaction service, navigation service, event bus, host facade, typewriter/history/auto-advance, persistence, quest redesign, Theme token, or metric additions.
- No compatibility shim for deleted `DialogueDialog`.

---

## File structure

### Create

- `scenes/ui/DialogueScreen.tscn` — scene-authored bottom Dialogue surface using `SiriusModalShell`.
- `scripts/ui/DialogueScreenController.cs` — pre-ready-safe configuration, dialogue traversal, dynamic choices, focus, and one-shot terminal signals.
- `tests/ui/DialogueScreenControllerTest.cs` — migrated terminal regressions plus condition, progression, focus, structure, and compact-scroll coverage.

### Modify

- `scripts/ui/NpcInteractionController.cs` — host Dialogue while retaining Shop/Heal orchestration.
- `scripts/game/Game.cs` — validate host before starting interaction and end the NPC flag safely on completion/reset/teardown.
- `tests/ui/NpcInteractionControllerTest.cs` — use a real host fixture and inspect `ModalLayer`.
- `tests/game/GameplayPauseHostTest.cs` — prove production host policy, HUD/world retention, no tree pause, and restoration.
- `tests/game/GameInputLifecycleTest.cs` — prove configured physical Cancel does not fall through to Pause and teardown clears the domain interaction.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — record the final hosted Dialogue/NPC cleanup contract.

### Delete after equivalent hosted coverage is green

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

### Audit-only unless a focused failing regression proves otherwise

- `scripts/data/npc/DialogueTree.cs`
- `scripts/data/npc/DialogueCatalog.cs`
- `scripts/data/npc/NpcData.cs`
- `scripts/game/NpcSpawn.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/ShopDialog.cs`
- `scripts/ui/HealDialog.cs`

---

## Risks and mitigations

### Configuring an unparented scene can dereference unbound nodes

**Risk:** `NpcInteractionController` must configure the candidate before `UIScreenHost.TryPresent(...)`, but `%ModalShell`, `%DialogueText`, and `%ChoicesContainer` are not bound until `_Ready()`.

**Mitigation:** expose `TryStartDialogue(...)`, store validated model state before `_Ready()`, and render from `_Ready()` when the host attaches the node. Add a direct pre-ready regression.

### Invalid root can emit before a host handle exists

**Risk:** emitting `DialogueClosed` during `TryPresent(...)` would re-enter orchestration before `_dialogueHandle` is stored.

**Mitigation:** `TryStartDialogue(...)` returns `false` for a null root and emits nothing. `NpcInteractionController` terminates the interaction before presentation.

### Host-attached Dialogue is not a child of the legacy UI parent

**Risk:** tests that search `_uiParent.GetChildren()` will falsely report no screen because `UIScreenHost` reparents the `Control` under `ModalLayer`.

**Mitigation:** controller tests inspect `host.GetNode<Control>("ModalLayer")` and `host.ActiveEntries`.

### Queued old choices can remain focusable for one frame

**Risk:** `QueueFree()` alone leaves old buttons in the tree/layout until the frame ends, allowing duplicate choice sets or stale focus during progression.

**Mitigation:** remove each old button from `%ChoicesContainer` immediately, then queue it for deletion before adding/focusing the new actions.

### Teardown currently unsubscribes before ending the domain interaction

**Risk:** `Game._ExitTree()` currently detaches `InteractionComplete` and calls `Finish()`, so `GameManager.EndNpcInteraction()` is not reached through the normal callback.

**Mitigation:** add one guarded `EndNpcInteractionIfActive()` helper used by normal completion, reset fallback, startup failure, and `_ExitTree()` after controller cleanup.

### Long text can accidentally create nested scrolling

**Risk:** an internally scrolling `RichTextLabel` inside `SiriusModalShell.BodyScroll` produces poor keyboard/gamepad behavior and unreliable measured height.

**Mitigation:** use `FitContent = true`, disable `RichTextLabel` internal scrolling, and keep `%BodyScroll` as the single scroll owner. Test measured compact layout and follow-focus scrolling.

---

## Task 1: Add the scene-authored Dialogue screen additively

**Files:**
- Create: `scenes/ui/DialogueScreen.tscn`
- Create: `scripts/ui/DialogueScreenController.cs`
- Create: `tests/ui/DialogueScreenControllerTest.cs`
- Read-only reference: `scripts/ui/DialogueDialog.cs`
- Read-only reference: `scripts/ui/components/SiriusModalShell.cs`
- Read-only reference: `docs/ui/hpa-373/wireframes/screen-wireframes.svg`

**Produces:**

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

- [ ] **Step 1: Write RED tests for pre-ready configuration and terminal parity**

Create `DialogueScreenControllerTest` with both an unparented candidate helper and a 640×360 `SubViewport` fixture.

Add these first:

```text
TryStartDialogue_BeforeReady_RendersRootAfterEnteringTree
TryStartDialogue_MissingRootReturnsFalseWithoutTerminalSignal
RequestCancelTwice_EmitsDialogueClosedOnce
OutcomeThenCancel_EmitsOutcomeOnly
SecondQueuedTerminalChoice_GrantsOnlyFirstFlag
Scene_UsesSiriusModalShellAndContainsNoAcceptDialog
```

The pre-ready test must call `TryStartDialogue(...)` before `AddChild(screen)`, then add it and await layout frames before asserting title, text, and choices.

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest"
```

Expected: compile/test failure because the scene and controller do not exist.

- [ ] **Step 3: Author static scene chrome**

Create:

```text
DialogueScreen (Control, full rect)
└── ModalShell (%ModalShell, SiriusModalShell, SizeClass=Large)
    └── Panel (%Panel, scene-specific bottom-center anchor override)
        └── Margin/RootLayout
            └── BodyScroll (%BodyScroll, inherited single scroll owner)
                └── BodyHost
                    ├── SpeakerLabel (%SpeakerLabel)
                    ├── DialogueText (%DialogueText)
                    └── ChoicesContainer (%ChoicesContainer)
```

Requirements:

- no scrim;
- `%Panel` is bottom-centered with the normal Sirius safe bottom margin at standard and compact viewports;
- `%DialogueText` is a wrapping `RichTextLabel` with `fit_content = true`, selection disabled, and internal scrolling disabled;
- `%ChoicesContainer` is a `VBoxContainer` with existing Sirius separation/theme behavior;
- no portrait node;
- no hard-coded 480×320 native-dialog size;
- static controls are scene-authored; only data-driven choice buttons are dynamic.

Do not change `SiriusModalShell` to support this one composition. Override/adjust the inherited `%Panel` only from `DialogueScreen`.

- [ ] **Step 4: Implement pre-ready-safe stored configuration**

Use stored state rather than touching bound nodes from an unparented candidate:

```csharp
private NpcData? _npc;
private DialogueTree? _tree;
private Character? _player;
private HashSet<string>? _questFlags;
private DialogueNode? _currentNode;
private bool _terminalEmitted;

public bool TryStartDialogue(
    NpcData npc,
    DialogueTree tree,
    Character player,
    HashSet<string> questFlags)
{
    var root = tree.Root;
    if (root == null)
        return false;

    _npc = npc;
    _tree = tree;
    _player = player;
    _questFlags = questFlags;
    _currentNode = root;
    _terminalEmitted = false;

    if (IsNodeReady())
        ShowNode(root);
    return true;
}
```

`_Ready()` binds `%ModalShell`, `%Panel`, `%SpeakerLabel`, `%DialogueText`, and `%ChoicesContainer`, applies the bottom-layout/compact presentation, subscribes resize handling, and calls `ShowNode(_currentNode)` when stored data exists.

Do not emit a terminal signal from `TryStartDialogue(...)` when the root is invalid.

- [ ] **Step 5: Move current traversal and side-effect order intact**

`ShowNode(...)` must:

```csharp
_modalShell.Title = _npc?.DisplayName ?? string.Empty;
_speakerLabel.Text = node.SpeakerName ?? string.Empty;
_speakerLabel.Visible = !string.IsNullOrWhiteSpace(node.SpeakerName);
_textLabel.Text = node.Text ?? string.Empty;
```

Then:

1. remove every old dynamic action from `%ChoicesContainer` immediately;
2. queue removed buttons for deletion;
3. evaluate `choice.Condition.IsMet(_player, _questFlags)` exactly as the retired implementation;
4. add one wrapped Sirius button per visible choice;
5. create one `Farewell.` button when no choices are visible;
6. set `InitialFocusTarget` to the first live action;
7. defer focus to that live action.

`OnChoicePressed(...)` keeps the terminal guard before `GrantFlag`, preserves outcome ordering, follows `NextNodeId`, and logs/closes once on a broken next ID.

`RequestCancel()` calls the same `EmitClosedOnce()` latch used by the leaf action.

The controller never hides or frees itself.

- [ ] **Step 6: Add condition, progression, leaf, and gamepad-focus tests**

Add:

```text
ConditionalChoices_RenderOnlyMetConditions
NonterminalChoice_ReplacesOldActionsAndFocusesFirstNewAction
Leaf_RendersSingleFarewellAction
GamepadAccept_OnFocusedChoiceAdvancesOnce
BrokenNextNode_ClosesOnce
SpeakerName_BlankHidesSpeakerLabel
```

After progression, assert old buttons are no longer children of `%ChoicesContainer` before waiting for their queued deletion.

- [ ] **Step 7: Add responsive bottom-surface and long-content tests**

At both 640×360 and 1280×720:

- await at least two process frames after layout;
- assert `%Panel` is inside the viewport safe frame;
- assert the panel bottom is aligned to the safe bottom margin within a small layout tolerance;
- assert it is not vertically centered;
- assert all action targets meet `SiriusUiMetrics.MinimumTarget(compact).Y`.

For long content at 640×360, create multi-paragraph text plus enough long choices to exceed the body. Assert:

```csharp
var bodyScroll = screen.GetNode<ScrollContainer>("%BodyScroll");
var bar = bodyScroll.GetVScrollBar();
AssertThat(bar.MaxValue).IsGreater(bar.Page);
```

Focus the final choice and await a frame; assert `bodyScroll.ScrollVertical > 0` so follow-focus behavior is real, not only theoretically scrollable.

- [ ] **Step 8: Run the additive screen suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~DialogueDialogTest"
```

Expected: both suites pass while the native implementation still exists.

- [ ] **Step 9: Commit**

```bash
git add scenes/ui/DialogueScreen.tscn scripts/ui/DialogueScreenController.cs tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored dialogue screen"
```

---

## Task 2: Cut `NpcInteractionController` over to hosted Dialogue

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`
- Delete after green: `scripts/ui/DialogueDialog.cs`
- Delete after green: `tests/ui/DialogueDialogTest.cs`

**Produces:** one hosted Dialogue handle owned by the existing interaction controller; Shop/Heal remain unchanged.

- [ ] **Step 1: Convert the controller fixture to a real host and make it RED**

Load `res://scenes/ui/UIScreenHost.tscn`, configure it using the same options pattern as current host tests, and keep the legacy `_uiParent` only for Shop/Heal.

Update `CreateController(...)` to pass `_screenHost`.

Hosted Dialogue lookup must use the host layer:

```csharp
var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
var dialogue = modalLayer.GetChildren()
    .OfType<DialogueScreenController>()
    .Single();
```

Do not search `_uiParent.GetChildren()` for a host-owned screen.

Rewrite/add:

```text
Begin_HostsOneDialogueEntry
DialogueCancel_ClosesHostedEntryAndCompletesOnce
Finish_WhileDialogueActive_ClosesHostedEntryAndCompletesOnce
MissingTree_CreatesNoHostedEntryAndCompletesOnce
InvalidRoot_CreatesNoHostedEntryAndCompletesOnce
```

Run the suite and expect constructor/type failures.

- [ ] **Step 2: Add the narrow host dependency and hosted state**

Keep existing Shop/Heal fields. Add only:

```csharp
private readonly UIScreenHost _screenHost;
private DialogueScreenController? _dialogueScreen;
private UIScreenHandle? _dialogueHandle;
```

Constructor:

```csharp
public NpcInteractionController(
    GameManager gameManager,
    UIScreenHost screenHost,
    Node uiParent,
    NpcData npc,
    Character player,
    HashSet<string> questFlags)
```

Do not add callbacks, interfaces, a presenter, or a host wrapper.

- [ ] **Step 3: Replace `Begin()` with load → configure → present**

After resolving the tree:

```csharp
var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
if (packed == null)
{
    GD.PushError("[NpcInteractionController] DialogueScreen.tscn not found.");
    Finish();
    return;
}

var screen = packed.InstantiateOrNull<DialogueScreenController>();
if (screen == null)
{
    GD.PushError("[NpcInteractionController] Failed to instantiate DialogueScreenController.");
    Finish();
    return;
}

screen.DialogueOutcome += OnDialogueOutcome;
screen.DialogueClosed += OnDialogueClosed;

if (!screen.TryStartDialogue(_npc, tree, _player, _questFlags))
{
    GD.PushError($"[NpcInteractionController] Dialogue tree '{tree.TreeId}' has no root node.");
    DisconnectDialogueSignals(screen);
    screen.QueueFree();
    Finish();
    return;
}
```

Use the existing repository's preferred packed-scene instantiate helper; if `InstantiateOrNull<T>()` is not present, use `Instantiate<T>()` with the established error handling. Do not add a helper solely for this call.

Then call:

```csharp
var result = _screenHost.TryPresent(screen, new UIScreenEntrySpec
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
});
```

If `result.Status != UIScreenOpenStatus.Opened` or `result.Handle == null`, disconnect/free the rejected candidate if it is still valid and call `Finish()`.

Only after a successful return assign:

```csharp
_dialogueScreen = screen;
_dialogueHandle = result.Handle;
```

The pre-ready screen contract guarantees `_Ready()` renders without emitting a terminal signal during this open transaction.

- [ ] **Step 4: Close through the host before continuing orchestration**

`OnDialogueOutcome(...)` captures the enum, closes Dialogue, then opens the existing child or finishes:

```csharp
private void OnDialogueOutcome(int outcomeValue)
{
    var outcome = (DialogueOutcomeType)outcomeValue;
    CloseDialoguePresentation(UIScreenCloseReason.ExplicitAction);

    switch (outcome)
    {
        case DialogueOutcomeType.OpenShop:
            OpenShop();
            return;
        case DialogueOutcomeType.Heal:
            OpenHeal();
            return;
        case DialogueOutcomeType.CloseAndReturn:
            Finish();
            return;
        default:
            GD.PushWarning($"[NpcInteractionController] Unsupported dialogue outcome: {outcome}.");
            Finish();
            return;
    }
}
```

`OnDialogueClosed()` closes with `ExplicitAction` and calls `Finish()`.

`CloseDialoguePresentation(...)` must:

- copy and clear the local handle before calling the host, preventing re-entrant double-close;
- call `TryClose(...)` when a handle exists;
- tolerate `AlreadyClosed`, `StaleHandle`, and `HostTearingDown` by clearing local state/signals;
- never emit a second terminal signal.

`ClearDialoguePresentation(screen)` disconnects signals and clears fields only when they still reference that screen. Host `Cleanup` remains the node-lifetime authority.

`Finish()` sets `_finished` before cleanup, closes active Dialogue with `Programmatic`, cleans legacy Shop/Heal, and invokes `InteractionComplete` once.

- [ ] **Step 5: Preserve Dialogue → Shop/Heal sequencing**

Update existing tests to press hosted Dialogue choices, then assert before using the native child:

```csharp
AssertThat(_screenHost.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(_screenHost.GetNode<Control>("ModalLayer")
    .GetChildren().OfType<DialogueScreenController>().Any()).IsFalse();
```

Then retain current Shop/Heal close/cancel and exactly-once completion assertions. `GameManager.IsInNpcInteraction` remains owned by `Game`, so the controller fixture only proves it does not emit `InteractionComplete` during the successful transition.

- [ ] **Step 6: Add presentation-rejection coverage using host state**

Open a fixture entry with `Kind = UIScreenKinds.Dialogue`, then call `Begin()` on the candidate controller. Assert:

- the candidate is rejected with no second Dialogue child;
- `InteractionComplete` emits once;
- no Shop/Heal child is created;
- the pre-existing fixture entry remains active.

Do not add dependency injection solely to synthesize a host failure.

- [ ] **Step 7: Run controller-focused gates**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: all pass, build has 0 errors.

- [ ] **Step 8: Delete native Dialogue after hosted cutover is green**

Delete:

```text
scripts/ui/DialogueDialog.cs
tests/ui/DialogueDialogTest.cs
```

Run:

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests
```

Expected: no active references.

- [ ] **Step 9: Commit**

```bash
git add scripts/ui/NpcInteractionController.cs tests/ui/NpcInteractionControllerTest.cs \
  scripts/ui/DialogueDialog.cs tests/ui/DialogueDialogTest.cs
git commit -m "feat(ui): host NPC dialogue through UIScreenHost"
```

---

## Task 3: Integrate Game ownership and pin input/teardown behavior

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify only when a focused test requires it: `scripts/ui/NpcInteractionController.cs`
- Modify only when a focused test requires it: `scripts/ui/DialogueScreenController.cs`

- [ ] **Step 1: Add root lifecycle tests first**

Add production/synthetic fixture regressions for:

```text
NpcInteraction_HostsDialogueWithoutPausingAndKeepsHudVisible
ConfiguredKeyboardCancel_ClosesDialogueWithoutOpeningPause
ConfiguredControllerCancel_ClosesDialogueWithoutOpeningPause
DialogueCompletion_RestoresPromptAndGameplaySuppression
GameTeardown_DuringDialogueEndsNpcInteractionAndClosesHostEntry
```

While active, assert the real host policy:

```csharp
var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Dialogue);
AssertThat(entry.Policy.InputPriority).IsEqual(UIInputPriority.Modal);
AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(entry.Policy.PauseTree).IsFalse();
AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Visible);
AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
AssertThat(sceneTree.Paused).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsTrue();
```

- [ ] **Step 2: Validate the host before starting the domain interaction**

In `OnNpcInteracted(...)`, check `_screenHost` before `StartNpcInteraction()`:

```csharp
if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
{
    GD.PushError("[Game] Cannot start NPC interaction without UIScreenHost.");
    UpdateInteractionPrompt();
    return;
}

_gameManager.StartNpcInteraction();
_npcInteractionController = new NpcInteractionController(
    _gameManager,
    _screenHost,
    GetNode("UI"),
    npcData,
    _gameManager.Player,
    _questFlags);
```

Keep the existing try/catch, but route its failure through the guarded domain-end helper from Step 3.

Do not add a Dialogue handle or outcome callbacks to `Game`.

- [ ] **Step 3: Centralize one guarded domain-end operation in `Game`**

Add:

```csharp
private void EndNpcInteractionIfActive()
{
    if (_gameManager != null && _gameManager.IsInNpcInteraction)
        _gameManager.EndNpcInteraction();
}
```

Use it in:

- `OnNpcInteractionComplete()` after disconnecting the completed controller;
- `OnNpcInteractionResetRequested()` when no controller exists;
- the `OnNpcInteracted(...)` catch/failure path;
- `_ExitTree()` after controller cleanup.

`OnNpcInteractionComplete()` remains the normal owner of prompt/player-UI refresh. The helper only owns the flag transition.

- [ ] **Step 4: Fix teardown order explicitly**

In `_ExitTree()`:

1. capture `_npcInteractionController`;
2. unsubscribe `InteractionComplete` to prevent UI refresh during teardown;
3. call `Finish()` so any hosted Dialogue closes;
4. clear the field;
5. call `EndNpcInteractionIfActive()`.

This is intentionally different from normal completion: teardown performs cleanup without relying on an event whose subscriber has been removed.

Add an assertion that a second reset/teardown attempt does not re-open, re-close, or leave `IsInNpcInteraction` true.

- [ ] **Step 5: Prove configured Cancel is consumed at the host**

Use the existing input-map helpers in `GameInputLifecycleTest` for one keyboard binding and one joypad binding. For each:

- start a real NPC interaction;
- confirm Dialogue is topmost;
- push the configured physical event;
- await host cleanup/restoration;
- assert Dialogue closed, Pause never opened, `IsInNpcInteraction` is false, and the viewport reports the input handled.

Do not call `RequestCancel()` directly in these tests; Task 2 already covers that seam.

- [ ] **Step 6: Prove normal terminal restoration**

Progress a villager path to its terminal action and assert:

- Dialogue entry is gone;
- tree is still unpaused;
- `host.CurrentState.IsPresentationGameplayBlocked` is false;
- `GameManager.IsInNpcInteraction` is false;
- the exploration interaction prompt is recomputed when the NPC remains adjacent;
- no stale Dialogue focus owner remains.

Use existing fixture probes/reflection patterns; do not add public test-only state.

- [ ] **Step 7: Run integration gates**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest"
```

Expected: 0 failures.

If focus fails after node replacement, adjust only the screen's deferred first-live-action focus. Do not add manual directional neighbors without a failing navigation regression.

If compact scrolling fails, fix only the Dialogue scene/controller layout. Do not modify `SiriusModalShell` without a separate shell-level RED test.

- [ ] **Step 8: Commit**

```bash
git add scripts/game/Game.cs tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs \
  scripts/ui/NpcInteractionController.cs scripts/ui/DialogueScreenController.cs
git commit -m "test(ui): pin hosted dialogue gameplay lifecycle"
```

---

## Task 4: Reconcile lifecycle docs and complete verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: all files changed in Tasks 1–3

- [ ] **Step 1: Update only the Dialogue/NPC lifecycle rows**

Record:

- scene-authored `DialogueScreenController : Control`;
- bottom `SiriusModalShell` composition with world/HUD visible;
- `NpcInteractionController` owns the host handle and Dialogue → Shop/Heal transition;
- `PauseTree = false`, `BlockGameplayInput = true`, `UIInputPriority.Modal`;
- `Cancel = Consume` and `InterceptCancel -> RequestCancel() -> ConsumeHere`;
- first-live-action focus on open and node progression;
- one-shot terminal/domain side effects;
- host close before native Shop/Heal open;
- invalid data/presentation failure finish once;
- `Finish()` closes hosted Dialogue;
- `Game._ExitTree()` ends the NPC domain flag after unsubscribed cleanup;
- HPA-570 still owns Shop/Heal migration.

Do not rewrite unrelated lifecycle rows.

- [ ] **Step 2: Run stale-path and scope audits**

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests docs
rg -n "UIScreenKinds\.Dialogue|DialogueScreenController|DialogueScreen\.tscn" scripts scenes tests docs/ui/hpa-376
rg -n "UIInputInterception\.Consumed" docs scripts tests
```

Expected:

- no `DialogueDialog` references;
- every active Dialogue presentation path uses the hosted screen;
- no invalid `UIInputInterception.Consumed` references.

Audit the diff for prohibited scope:

```bash
git diff --name-only origin/main...HEAD
```

Confirm there are no Shop/Heal presentation migrations, portrait/model/assets, host API/kind changes, Theme/metric additions, quest changes, or new presentation/service abstractions.

- [ ] **Step 3: Run focused Dialogue and neighboring interaction suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~Dialogue|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: 0 failures. Shop/Heal behavior remains unchanged.

- [ ] **Step 4: Run full suite, build, and diff checks**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected:

- full suite passes;
- build has 0 errors;
- diff check is clean.

Record unchanged NuGet/orphan-node warning noise separately; do not convert it into HPA-569 scope.

- [ ] **Step 5: Commit lifecycle documentation/final cleanup**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record hosted dialogue lifecycle"
```

## Final review checklist

- [ ] Static Dialogue chrome is scene-authored and contains no `AcceptDialog`.
- [ ] The Dialogue panel is a bottom in-world surface at 640×360 and 1280×720.
- [ ] `SiriusModalShell` is reused without a new shared API unless a shell-level RED test required one.
- [ ] Pre-ready configuration cannot touch unbound nodes or emit before host ownership exists.
- [ ] `NpcInteractionController` remains the single Dialogue/Shop/Heal orchestration owner.
- [ ] Host-attached tests inspect `ModalLayer`, not the legacy UI parent.
- [ ] Dialogue conditions, branching, flags, outcomes, and leaf behavior match the retired implementation.
- [ ] Configured Cancel and visible terminal actions share one terminal latch and use `ConsumeHere`.
- [ ] Dialogue never pauses the scene tree; world/HUD remain visible and gameplay is inert.
- [ ] Shop/Heal continue through their legacy dialogs after Dialogue closes.
- [ ] 640×360 long content scrolls through the shell's single body scroll owner.
- [ ] Normal completion, invalid data, host rejection, reset, and Game teardown end the NPC interaction once.
- [ ] No native `DialogueDialog` references remain.
- [ ] Focused suites, neighboring suites, full suite, build, stale-reference audit, scope audit, and diff check pass.