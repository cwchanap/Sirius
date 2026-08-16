# HPA-569 Hosted Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `DialogueDialog` with one scene-authored `DialogueScreenController` hosted by the gameplay `UIScreenHost`, preserving all current dialogue-tree and NPC-interaction semantics.

**Architecture:** Keep `NpcInteractionController` as the single dialogue → Shop/Heal orchestration owner. Move current dialogue traversal/choice logic into a scene-backed `Control`, let the controller present/close it through the existing gameplay host, and leave legacy Shop/Heal untouched for HPA-570.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme / `SiriusModalShell` / `UIScreenHost`.

## Global Constraints

- Reuse `SiriusModalShell`; do not add another shell or modal framework.
- Reuse `UIScreenKinds.Dialogue`; do not add host kinds or host APIs.
- Keep `NpcInteractionController` as orchestration owner; do not move dialogue progression into `Game`.
- Preserve condition evaluation, branching, `GrantFlag`, outcomes, leaf completion, and exactly-once terminal/domain side effects.
- Dialogue does not pause the scene tree.
- Keep the gameplay HUD visible beneath Dialogue.
- Do not add portrait assets or infer portraits from `NpcData.SpriteType`; current data has no portrait contract.
- Shop and Heal remain native dialogs in this ticket.
- No presenter, view model, interaction service, navigation service, event bus, host facade, or persistence changes.

---

## File Structure

**Create**

- `scenes/ui/DialogueScreen.tscn` — static dialogue chrome using `SiriusModalShell`.
- `scripts/ui/DialogueScreenController.cs` — dialogue-tree traversal, dynamic choice rendering, focus, and one-shot terminal signals.
- `tests/ui/DialogueScreenControllerTest.cs` — migrated domain/presentation regressions.

**Modify**

- `scripts/ui/NpcInteractionController.cs` — host the dialogue screen while retaining Shop/Heal orchestration.
- `scripts/game/Game.cs` — pass the production `_screenHost` into `NpcInteractionController`; no dialogue behavior moves here.
- `tests/ui/NpcInteractionControllerTest.cs` — use a real host fixture and prove dialogue replacement/cleanup.
- `tests/game/GameplayPauseHostTest.cs` — prove production host policy and no tree pause.
- `tests/game/GameInputLifecycleTest.cs` — prove configured Cancel closes Dialogue without opening Pause, if this behavior is not already fully covered by `GameplayPauseHostTest`.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — replace the legacy Dialogue lifecycle description after implementation is green.

**Delete**

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

---

### Task 1: Build the scene-authored Dialogue screen without changing production ownership

**Files:**
- Create: `scenes/ui/DialogueScreen.tscn`
- Create: `scripts/ui/DialogueScreenController.cs`
- Create: `tests/ui/DialogueScreenControllerTest.cs`
- Read-only reference: `scripts/ui/DialogueDialog.cs`
- Read-only reference: `scripts/ui/components/SiriusModalShell.cs`

**Interfaces:**
- Consumes: `NpcData`, `DialogueTree`, `DialogueNode`, `DialogueChoice`, `Character`, `HashSet<string>`.
- Produces:

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

- [ ] **Step 1: Port the three durable terminal regressions as failing scene-based tests**

Create `tests/ui/DialogueScreenControllerTest.cs` with a fixture that loads the real scene:

```csharp
private DialogueScreenController InstantiateScreen()
{
    var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
    AssertThat(packed).IsNotNull();
    var screen = packed.Instantiate<DialogueScreenController>();
    _sceneTree.Root.AddChild(screen);
    return screen;
}
```

Port these tests from `DialogueDialogTest` using `RequestCancel()` instead of native `AcceptDialog` signals:

```csharp
[TestCase]
public void RequestCancelTwice_EmitsDialogueClosedOnce()
{
    var screen = InstantiateScreen();
    int closed = 0;
    screen.DialogueClosed += () => closed++;

    screen.StartDialogue(
        NpcCatalog.GetById("old_farmer")!,
        DialogueCatalog.GetById("villager_01")!,
        TestHelpers.CreateTestCharacter(),
        new HashSet<string>());

    screen.RequestCancel();
    screen.RequestCancel();

    AssertThat(closed).IsEqual(1);
}
```

Also port:

- outcome then cancel emits only outcome;
- second queued terminal choice grants only the first flag.

- [ ] **Step 2: Run the new test file and verify RED**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~DialogueScreenControllerTest"
```

Expected: FAIL because `DialogueScreen.tscn` / `DialogueScreenController` do not exist.

- [ ] **Step 3: Author the static scene chrome**

Create `DialogueScreen.tscn` with one full-rect `DialogueScreenController` root and one `SiriusModalShell` child. The shell body must contain stable named nodes:

```text
DialogueScreen
└── ModalShell (%ModalShell)
    └── BodyHost
        ├── SpeakerLabel (%SpeakerLabel)
        ├── DialogueText (%DialogueText)
        └── ChoicesContainer (%ChoicesContainer)
```

Requirements:

- Theme uses the existing Sirius theme resource/pattern used by current modal scenes.
- `%DialogueText` is a wrapping `RichTextLabel`.
- `%ChoicesContainer` is a vertical container.
- Do not author portrait nodes in this ticket.
- Do not hard-code 480×320 or another desktop-dialog size; let `SiriusModalShell` own responsive sizing.

- [ ] **Step 4: Implement the minimal controller by moving current traversal logic intact**

Move the existing `DialogueDialog` fields and traversal semantics into `DialogueScreenController`. Bind scene nodes in `_Ready()`:

```csharp
public override void _Ready()
{
    _modalShell = GetNode<SiriusModalShell>("%ModalShell");
    _speakerLabel = GetNode<Label>("%SpeakerLabel");
    _textLabel = GetNode<RichTextLabel>("%DialogueText");
    _choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");
}
```

`StartDialogue(...)` must reset `_terminalEmitted`, set `_modalShell.Title = npc.DisplayName`, validate `tree.Root`, and call `ShowNode(rootNode)`.

`ShowNode(...)` must:

```csharp
_speakerLabel.Text = node.SpeakerName;
_speakerLabel.Visible = !string.IsNullOrWhiteSpace(node.SpeakerName);
_textLabel.Text = node.Text;
```

Then queue-free prior dynamic buttons, evaluate conditions, and create wrapped Buttons exactly as today. For a leaf:

```csharp
var close = new Button { Text = "Farewell." };
close.Pressed += EmitClosedOnce;
_choicesContainer.AddChild(close);
InitialFocusTarget = close;
close.CallDeferred(Control.MethodName.GrabFocus);
```

For choices, set `InitialFocusTarget` to the first created Button and deferred-focus it after the node is populated.

Keep the existing early `_terminalEmitted` guard before any `GrantFlag` mutation in `OnChoicePressed`.

`RequestCancel()` is:

```csharp
public void RequestCancel() => EmitClosedOnce();
```

The controller must never call `Hide()` or `QueueFree()` as part of terminal behavior.

- [ ] **Step 5: Add condition/progression/leaf presentation tests**

Add tests that assert:

- an unmet condition omits its Button;
- selecting a nonterminal choice replaces the old choice set with the next node's choices;
- a leaf renders exactly one `Farewell.` Button;
- `InitialFocusTarget` points at the first live action after every node transition.

Use the existing recursive Button finder helper rather than adding a presentation abstraction.

- [ ] **Step 6: Add the 640×360 long-content regression**

Instantiate the scene under a 640×360 viewport fixture, create a dialogue node with multi-line text plus enough long choice labels to exceed the body height, process layout frames, and assert:

```csharp
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");

AssertThat(shell.Size.Y).IsLessEqual(360f);
AssertThat(bodyScroll.GetVScrollBar().MaxValue)
    .IsGreater(bodyScroll.GetVScrollBar().Page);
```

Use real measured layout values; do not add new modal sizing constants.

- [ ] **Step 7: Run Dialogue screen tests and the existing native tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~DialogueDialogTest"
```

Expected: both suites pass. Native `DialogueDialog` remains temporarily so this task is an additive, reviewable slice.

- [ ] **Step 8: Commit**

```bash
git add scenes/ui/DialogueScreen.tscn scripts/ui/DialogueScreenController.cs tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored dialogue screen"
```

---

### Task 2: Cut `NpcInteractionController` over to hosted Dialogue

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`
- Delete after green: `scripts/ui/DialogueDialog.cs`
- Delete after green: `tests/ui/DialogueDialogTest.cs`

**Interfaces:**
- Consumes: Task 1 `DialogueScreenController` and existing `UIScreenHost` / `UIScreenKinds.Dialogue`.
- Produces: hosted dialogue lifetime managed entirely inside `NpcInteractionController`; Shop/Heal remain unchanged.

- [ ] **Step 1: Rewrite the controller fixture around a real `UIScreenHost` and make the tests RED**

In `NpcInteractionControllerTest`, create a `Control` host root and configure a real `UIScreenHost`. Update `CreateController(...)` to pass it:

```csharp
return new NpcInteractionController(
    null!,
    _screenHost,
    _uiParent,
    npc,
    TestHelpers.CreateTestCharacter(),
    new HashSet<string>());
```

Replace native-dialog assertions with hosted-screen assertions:

```csharp
var dialogue = _uiParent.GetChildren()
    .OfType<DialogueScreenController>()
    .Single();
AssertThat(_screenHost.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();
```

Add `Finish_WhileDialogueActive_ClosesHostedDialogueAndCompletesOnce`.

Run the suite and expect constructor/signature failures.

- [ ] **Step 2: Add host fields and constructor input without changing Shop/Heal ownership**

Change controller state to:

```csharp
private readonly UIScreenHost _screenHost;
private readonly Node _uiParent;
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

Do not change `ShopDialog` or `HealDialog` fields/call sites in this task.

- [ ] **Step 3: Replace `Begin()` native construction with scene load + host presentation**

After resolving the tree:

```csharp
var scene = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
if (scene == null)
{
    GD.PushError("[NpcInteractionController] DialogueScreen.tscn not found.");
    Finish();
    return;
}

var screen = scene.Instantiate<DialogueScreenController>();
if (screen == null)
{
    GD.PushError("[NpcInteractionController] Failed to instantiate DialogueScreenController.");
    Finish();
    return;
}

screen.DialogueOutcome += OnDialogueOutcome;
screen.DialogueClosed += OnDialogueClosed;
screen.StartDialogue(_npc, tree, _player, _questFlags);
```

Present it with the explicit policy from the design:

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
        return UIInputInterception.Consumed;
    },
    Cleanup = _ => ClearDialoguePresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
});
```

If not opened, disconnect the screen signals, queue-free the unhosted screen, and call `Finish()`.

Store `_dialogueScreen` / `_dialogueHandle` only after a successful open.

- [ ] **Step 4: Make terminal handlers close through the host before continuing**

`OnDialogueOutcome(...)`:

```csharp
private void OnDialogueOutcome(int outcomeInt)
{
    var outcome = (DialogueOutcomeType)outcomeInt;
    CloseDialoguePresentation(UIScreenCloseReason.ExplicitAction);

    switch (outcome)
    {
        case DialogueOutcomeType.OpenShop:
            OpenShop();
            break;
        case DialogueOutcomeType.Heal:
            OpenHeal();
            break;
        case DialogueOutcomeType.CloseAndReturn:
            Finish();
            break;
        default:
            GD.PushWarning(...);
            Finish();
            break;
    }
}
```

`OnDialogueClosed()` closes the hosted entry then calls `Finish()`.

Implement `CloseDialoguePresentation(...)` so stale handles clear local references and `ClearDialoguePresentation(screen)` disconnects signals exactly once.

`Finish()` must close active Dialogue before cleaning legacy Shop/Heal and invoking `InteractionComplete` once.

- [ ] **Step 5: Prove Dialogue → Shop and Dialogue → Heal sequencing**

Update the existing tests so they press the hosted Dialogue choice, then assert before interacting with the native child:

```csharp
AssertThat(_screenHost.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(_uiParent.GetChildren().OfType<DialogueScreenController>().Any()).IsFalse();
```

Then preserve the existing Shop/Heal cancellation and exactly-once completion assertions.

- [ ] **Step 6: Add host-presentation failure coverage without a production service seam**

Use a fixture host state that rejects another Dialogue kind, then call `Begin()` and assert:

- no `DialogueScreenController` remains;
- `InteractionComplete` emits once;
- no Shop/Heal is created.

Do not add dependency injection solely to make host failure testable.

- [ ] **Step 7: Update `Game` constructor call only**

Change `OnNpcInteracted(...)` from:

```csharp
new NpcInteractionController(
    _gameManager, GetNode("UI"), npcData, _gameManager.Player, _questFlags);
```

to:

```csharp
if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
{
    GD.PushError("[Game] Cannot start NPC interaction without UIScreenHost.");
    _gameManager.EndNpcInteraction();
    UpdateInteractionPrompt();
    return;
}

_npcInteractionController = new NpcInteractionController(
    _gameManager,
    _screenHost,
    GetNode("UI"),
    npcData,
    _gameManager.Player,
    _questFlags);
```

Do not add dialogue handles or callbacks to `Game`.

- [ ] **Step 8: Run focused controller and Game compile/test gates**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: all pass, 0 build errors.

- [ ] **Step 9: Delete native Dialogue only after the hosted cutover is green**

Delete:

```text
scripts/ui/DialogueDialog.cs
tests/ui/DialogueDialogTest.cs
```

Run:

```bash
rg -n "DialogueDialog|AcceptDialog.*Dialogue|PopupCentered\(\)" scripts scenes tests
```

Expected: no active `DialogueDialog` references; unrelated `PopupCentered()` references may remain for legacy Shop/Heal or other tickets.

- [ ] **Step 10: Commit**

```bash
git add scripts/ui/NpcInteractionController.cs scripts/game/Game.cs tests/ui/NpcInteractionControllerTest.cs scripts/ui/DialogueDialog.cs tests/ui/DialogueDialogTest.cs
git commit -m "feat(ui): host NPC dialogue through UIScreenHost"
```

---

### Task 3: Pin production gameplay input, pause, focus, and compact behavior

**Files:**
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs` only if needed for configured physical Cancel coverage
- Modify: `tests/ui/DialogueScreenControllerTest.cs` only for issues exposed by integration tests
- Modify: `scripts/ui/DialogueScreenController.cs` only for minimal fixes exposed by tests
- Modify: `scripts/ui/NpcInteractionController.cs` only for minimal host lifecycle fixes exposed by tests

**Interfaces:**
- Consumes: hosted Dialogue implementation from Tasks 1–2.
- Produces: regression evidence for HPA-569 acceptance criteria.

- [ ] **Step 1: Add a production-host lifecycle test and verify RED/GREEN deliberately**

Add a test that starts an NPC interaction through a gameplay fixture and asserts while Dialogue is active:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();
AssertThat(sceneTree.Paused).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsTrue();
```

Inspect the active entry policy using the existing host-test helper and assert:

```csharp
AssertThat(policy.BlockGameplayInput).IsTrue();
AssertThat(policy.PauseTree).IsFalse();
AssertThat(policy.Hud).IsEqual(UIHudPolicy.Visible);
AssertThat(policy.Cancel).IsEqual(UICancelPolicy.Consume);
```

- [ ] **Step 2: Prove configured Cancel cannot fall through to Pause**

Use the existing input-map helper pattern in `GameInputLifecycleTest` to emit the configured keyboard Cancel while Dialogue is topmost.

Assert after the input is handled:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
```

Also assert `InteractionComplete` / game cleanup occurred once through the existing observable seam.

If `GameplayPauseHostTest` already dispatches through the exact configured-action path, keep the regression there and do not duplicate it in `GameInputLifecycleTest`.

- [ ] **Step 3: Prove normal terminal completion restores gameplay**

Start a simple villager dialogue, press through to `Farewell.`, then assert:

- Dialogue host entry is gone;
- `GameManager.IsInNpcInteraction == false`;
- tree remains unpaused;
- interaction prompt is recomputed/visible when an adjacent interactable remains;
- player movement/input suppression is no longer active.

Do not introduce a new public getter solely for these assertions; use existing fixture probes/providers.

- [ ] **Step 4: Run compact/long-content and host integration together**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: all pass with the 640×360 regression included.

- [ ] **Step 5: Fix only evidence-backed focus/layout issues**

If gamepad focus fails after node replacement, fix only the controller's explicit first-action focus timing. Do not add manual directional neighbor wiring unless the failing test proves vertical container focus order is insufficient.

If the 640×360 body does not scroll, fix the Dialogue scene's body content/layout usage; do not change `SiriusModalShell` unless a shell-level regression demonstrates a shared-shell defect.

- [ ] **Step 6: Commit**

```bash
git add tests/game/GameplayPauseHostTest.cs tests/game/GameInputLifecycleTest.cs tests/ui/DialogueScreenControllerTest.cs scripts/ui/DialogueScreenController.cs scripts/ui/NpcInteractionController.cs
git commit -m "test(ui): pin hosted dialogue gameplay lifecycle"
```

---

### Task 4: Reconcile lifecycle docs and run final stale-path/full-suite verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: all files changed in Tasks 1–3

**Interfaces:**
- Consumes: final hosted behavior.
- Produces: accurate lifecycle documentation and release confidence; no new runtime API.

- [ ] **Step 1: Update only the Dialogue/NPC rows in HPA-376 lifecycle documentation**

Record the final contract explicitly:

- scene-authored `DialogueScreenController : Control`;
- `NpcInteractionController` owns host handle and dialogue → Shop/Heal transition;
- `PauseTree = false`;
- `BlockGameplayInput = true`;
- HUD visible, lower gameplay inert, cursor visible;
- configured Cancel is consumed through `RequestCancel()`;
- one-shot terminal/domain side effects remain guaranteed;
- host close precedes native Shop/Heal open;
- `Finish()` closes hosted Dialogue during teardown;
- HPA-570 still owns Shop/Heal migration.

Do not rewrite unrelated rows.

- [ ] **Step 2: Run stale native-reference audit**

Run:

```bash
rg -n "DialogueDialog|new DialogueDialog|AcceptDialog" scripts/ui scripts/game scenes/ui tests/ui tests/game
```

Expected:

- zero `DialogueDialog` references;
- `AcceptDialog` may remain only in unrelated legacy surfaces such as Shop/Heal/Puzzle until their own tickets.

Run:

```bash
rg -n "UIScreenKinds\.Dialogue|DialogueScreenController|DialogueScreen\.tscn" scripts scenes tests docs/ui/hpa-376
```

Expected: every active Dialogue presentation path points at the hosted screen.

- [ ] **Step 3: Run focused HPA-569 suites**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: 0 failures.

- [ ] **Step 4: Run existing dialogue-domain and neighboring interaction suites**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo --filter "FullyQualifiedName~Dialogue|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~HealDialogTest"
```

Expected: 0 failures. Shop/Heal behavior remains unchanged.

- [ ] **Step 5: Run full test suite and build**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected:

- full suite passes;
- build has 0 errors;
- diff check is clean.

Record any pre-existing NuGet/orphan-node warning noise separately; do not treat unchanged warning noise as HPA-569 work.

- [ ] **Step 6: Scope audit**

Confirm the final diff does **not** contain:

- Shop/Heal presentation migration;
- portrait asset/model changes;
- `NpcData` schema changes;
- new host API/kinds;
- new theme tokens/metrics;
- presenter/view-model/service/router/event-bus abstractions;
- dialogue history, typewriter, auto-advance, voice, persistence, or quest redesign.

- [ ] **Step 7: Commit documentation/final cleanup**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record hosted dialogue lifecycle"
```

## Final review checklist

Before marking HPA-569 implementation complete:

- [ ] Static dialogue chrome is scene-authored.
- [ ] `SiriusModalShell` is reused unchanged unless a shared-shell regression required a fix.
- [ ] `NpcInteractionController` remains the single NPC interaction orchestration owner.
- [ ] Dialogue tree semantics and quest-flag side effects match the retired implementation.
- [ ] Configured Cancel and visible terminal actions share one terminal latch.
- [ ] Dialogue never pauses the scene tree.
- [ ] Gameplay input is blocked while Dialogue is active and restored once afterward.
- [ ] Shop/Heal still work through their legacy dialogs after Dialogue outcomes.
- [ ] 640×360 long content is readable/scrollable.
- [ ] No native `DialogueDialog` references remain.
- [ ] Full suite/build/diff-check pass.
