# HPA-569 Hosted Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `DialogueDialog` with one scene-authored wide bottom Dialogue surface hosted by the gameplay `UIScreenHost`, preserving current dialogue-tree behavior and exactly-once NPC-interaction cleanup.

**Architecture:** Keep `NpcInteractionController` as the single Dialogue → Shop/Heal orchestration owner. Move existing traversal/choice behavior into a pre-ready-safe scene-backed `Control`, present it through the existing gameplay host, and leave Shop/Heal native for HPA-570. Dialogue owns only one local responsive-layout method; `Game` remains the domain/root lifecycle owner.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme, `SiriusModalShell`, `SiriusUiMetrics`, and `UIScreenHost`.

## Global Constraints

- Reuse `SiriusModalShell`; do not add another shell, placement enum, or modal framework.
- Reuse `UIScreenKinds.Dialogue`; do not add host kinds, exclusive groups, policy factories, or host APIs.
- Keep `NpcInteractionController` as orchestration owner; do not move dialogue progression into `Game`.
- Preserve `IDialogueCondition.Evaluate(...)`, branching, `GrantFlag`, outcomes, leaf completion, and exactly-once terminal/domain side effects.
- Dialogue never pauses the scene tree.
- Keep world context and the gameplay HUD visible beneath Dialogue.
- Follow HPA-373 §9.8: final Dialogue is a **wide bottom panel centred inside the safe frame**, not a 960 px Pause modal moved downward.
- Set `SiriusModalShell.Compact` on every layout refresh before calling `RefreshPresentation(...)`.
- Derive final Dialogue width/bottom inset from `SiriusUiMetrics.SafeFrameInsets(...)`; add no new metric.
- Use the shell-owned body scroll as the single scroll owner; disable internal `RichTextLabel` scrolling.
- Resolve shell-owned `%Panel` / `%BodyScroll` through `%ModalShell`; do not treat them as DialogueScreen-owned unique names.
- Do not add portrait assets/model fields or infer portrait semantics from `NpcData.SpriteType`.
- Shop and Heal remain native dialogs in this ticket.
- No presenter, view model, interaction service, navigation service, event bus, host facade, typewriter/history/auto-advance, persistence, quest redesign, Theme token, or metric additions.
- No compatibility shim for deleted `DialogueDialog`.

---

## File Structure

### Create

- `scenes/ui/DialogueScreen.tscn` — static bottom Dialogue chrome using `SiriusModalShell`.
- `scripts/ui/DialogueScreenController.cs` — pre-ready configuration, traversal, dynamic choices, focus, responsive bottom layout, and terminal latch.
- `tests/ui/DialogueScreenControllerTest.cs` — terminal parity, conditions, progression, focus, ownership, wide-bottom layout, and compact-scroll coverage.

### Modify

- `scripts/ui/NpcInteractionController.cs` — host Dialogue while retaining Shop/Heal orchestration.
- `scripts/game/Game.cs` — pass the host in the Task 2 constructor cutover, then add the Task 3 host guard and guarded domain cleanup.
- `tests/ui/NpcInteractionControllerTest.cs` — reuse `UIScreenHostTestSupport.CreateHost(...)` and inspect `ModalLayer` / active entries.
- `tests/game/GameplayPauseHostTest.cs` — prove the actual `Game.tscn` NPC route and hosted lifecycle.
- `tests/game/GameInputLifecycleTest.cs` — prove configured physical keyboard/controller Cancel does not fall through to Pause.
- `docs/ui/hpa-376/ui-lifecycle-contract.md` — record the final hosted Dialogue/NPC cleanup contract.

### Delete after equivalent hosted coverage is green

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

### Audit-only unless a focused failing regression proves otherwise

- `scripts/data/npc/DialogueTree.cs`
- `scripts/data/npc/DialogueCatalog.cs`
- `scripts/data/npc/DialogueCondition.cs`
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

## Risks and Mitigations

### Pre-ready configuration can touch unbound scene nodes

**Risk:** the candidate is unparented before `TryPresent`, so `%ModalShell` and authored body nodes are not bound yet.

**Mitigation:** `TryStartDialogue(...)` validates/stores data only; `_Ready()` binds nodes and renders the stored root. A test calls `TryStartDialogue(...)` before `AddChild(...)`.

### Invalid root can emit before a host handle exists

**Risk:** emitting `DialogueClosed` during candidate preparation can re-enter orchestration before `_dialogueHandle` exists.

**Mitigation:** `TryStartDialogue(...)` returns `false` and emits nothing for a missing root. `NpcInteractionController` owns the terminal failure.

### Shell unique names belong to the shell instance

**Risk:** `screen.GetNode("%Panel")` / `screen.GetNode("%BodyScroll")` lookups fail because those nodes are owned by `SiriusModalShell.tscn`.

**Mitigation:** bind them through `_shell.GetNode<...>("%Panel")` and `_shell.GetNode<...>("%BodyScroll")`. Dialogue-authored nodes remain screen-owned unique names.

### Large modal width under-builds the approved Dialogue composition

**Risk:** `SiriusModalSizeClass.Large` caps standard width at 960 px, while HPA-373 specifies a wide safe-frame bottom surface.

**Mitigation:** `RefreshLayout()` sets shell compact state, lets the shell refresh chrome/body height, then overrides only this scene's panel width to the existing safe-frame content width and applies the safe bottom margin. No shell API changes.

### Queued old choices can remain focusable for one frame

**Risk:** `QueueFree()` without removal leaves stale buttons in layout/focus order until frame end.

**Mitigation:** `RemoveChild` each old dynamic action immediately, then `QueueFree()` it before creating/focusing replacements.

### Constructor cutover can leave an intermediate non-building commit

**Risk:** changing `NpcInteractionController` to require `UIScreenHost` without changing the sole production caller in `Game` breaks Task 2's build gate.

**Mitigation:** Task 2 changes the constructor and `Game` call together. Task 2 uses `_screenHost!` only as the compile-time bridge under the already-required production host; Task 3 replaces it with the explicit pre-`StartNpcInteraction` host validation.

### Teardown currently unsubscribes before domain completion

**Risk:** `Game._ExitTree()` removes `InteractionComplete`, then calls `Finish()`, so `OnNpcInteractionComplete()` cannot clear `IsInNpcInteraction`.

**Mitigation:** Task 3 adds one guarded `EndNpcInteractionIfActive()` helper used by normal completion, startup failure, reset fallback, and `_ExitTree()` after controller cleanup.

### Production integration tests can accidentally test only the flag

**Risk:** `GameManager.StartNpcInteraction()` does not open Dialogue, so a flag-only test cannot prove `OnNpcInteracted` / controller / host integration.

**Mitigation:** use the real `Game.tscn`, find an authored Floor GF `NpcSpawn`, derive the internal position, invoke private `OnNpcInteracted(...)`, and assert the hosted Dialogue entry before testing Cancel/completion/teardown. Keep the existing flag-only test for the native Shop/Heal phase.

---

## Task 1: Add the Scene-Authored Dialogue Screen Additively

**Files:**
- Create: `scenes/ui/DialogueScreen.tscn`
- Create: `scripts/ui/DialogueScreenController.cs`
- Create: `tests/ui/DialogueScreenControllerTest.cs`
- Read-only: `scripts/ui/DialogueDialog.cs`
- Read-only: `scripts/data/npc/DialogueCondition.cs`
- Read-only: `scripts/ui/PauseScreenController.cs`
- Read-only: `scripts/ui/BattleManager.cs`
- Read-only: `scripts/ui/components/SiriusModalShell.cs`
- Read-only: `docs/superpowers/specs/2026-07-25-sirius-ui-visual-language-design.md`

**Interfaces:**
- Consumes: `NpcData`, `DialogueTree`, `DialogueNode`, `DialogueChoice`, `IDialogueCondition.Evaluate(...)`, `Character`, `HashSet<string>`.
- Produces:

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

- [ ] **Step 1: Write RED pre-ready and terminal-parity tests**

Create `DialogueScreenControllerTest` with an unparented candidate helper plus a `SubViewport` fixture.

Add:

```text
TryStartDialogue_BeforeReady_RendersAfterAttach
TryStartDialogue_MissingRootReturnsFalseWithoutTerminalSignal
RequestCancelTwice_EmitsDialogueClosedOnce
OutcomeThenCancel_EmitsOutcomeOnly
SecondQueuedTerminalChoice_GrantsOnlyFirstFlag
Scene_UsesSiriusModalShellAndContainsNoAcceptDialog
```

The pre-ready test must execute in this order:

```csharp
var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
var screen = packed.Instantiate<DialogueScreenController>();

AssertThat(screen.TryStartDialogue(npc, tree, player, flags)).IsTrue();
AssertThat(screen.IsNodeReady()).IsFalse();

_viewport.AddChild(screen);
await AwaitFrames(2);

AssertThat(screen.GetNode<Label>("%SpeakerLabel").Text).IsEqual(tree.Root!.SpeakerName);
```

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest"
```

Expected: compile/test failure because the scene/controller do not exist.

- [ ] **Step 3: Author static scene chrome and ownership correctly**

Create:

```text
DialogueScreen (Control, full rect)
└── ModalShell (%ModalShell, SiriusModalShell)
    └── Panel (shell-owned %Panel; Dialogue scene overrides bottom placement)
        └── Margin/RootLayout/BodyScroll/BodyHost
            ├── SpeakerLabel (%SpeakerLabel)
            ├── DialogueText (%DialogueText)
            └── ChoicesContainer (%ChoicesContainer)
```

Author these properties:

```text
ModalShell: SizeClass = Large as editor/fallback chrome only
Panel: anchors bottom-centre; horizontal grow Both; vertical grow Begin
DialogueText: FitContent = true; word-smart wrap; selection disabled; internal scroll disabled
ChoicesContainer: vertical; horizontal ExpandFill
No scrim
No portrait node
```

`SizeClass = Large` is **not** the final runtime width contract; Step 5 overrides panel width from the safe frame.

- [ ] **Step 4: Implement pre-ready storage and correct shell-node binding**

```csharp
private SiriusModalShell _shell = null!;
private PanelContainer _panel = null!;
private ScrollContainer _bodyScroll = null!;
private Label _speakerLabel = null!;
private RichTextLabel _textLabel = null!;
private VBoxContainer _choicesContainer = null!;

private NpcData? _npc;
private DialogueTree? _tree;
private Character? _player;
private HashSet<string>? _questFlags;
private DialogueNode? _currentNode;
private bool _terminalEmitted;

public override void _Ready()
{
    _shell = GetNode<SiriusModalShell>("%ModalShell");
    _panel = _shell.GetNode<PanelContainer>("%Panel");
    _bodyScroll = _shell.GetNode<ScrollContainer>("%BodyScroll");
    _speakerLabel = GetNode<Label>("%SpeakerLabel");
    _textLabel = GetNode<RichTextLabel>("%DialogueText");
    _choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");

    Resized += OnResized;
    RefreshLayout();
    if (_currentNode != null)
        ShowNode(_currentNode);
}

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

`_ExitTree()` unsubscribes `Resized`. `TryStartDialogue(...)` emits nothing on invalid root.

- [ ] **Step 5: Implement local compact + safe-frame bottom layout**

```csharp
private void OnResized() => RefreshLayout();

private void RefreshLayout()
{
    if (!IsNodeReady())
        return;

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
    foreach (var child in _choicesContainer.GetChildren())
    {
        if (child is Button action)
            action.CustomMinimumSize = new Vector2(0f, minimumTarget.Y);
    }
}
```

Do not add `SiriusModalShell.Placement`, a Dialogue width metric, or a shared layout helper.

- [ ] **Step 6: Move `ShowNode` / `OnChoicePressed` semantics intact**

Clear old actions immediately:

```csharp
foreach (Node child in _choicesContainer.GetChildren())
{
    _choicesContainer.RemoveChild(child);
    child.QueueFree();
}
```

Use the existing condition contract exactly:

```csharp
var visibleChoices = new List<DialogueChoice>();
foreach (var choice in node.Choices)
{
    if (choice.Condition.Evaluate(_player!, _questFlags!))
        visibleChoices.Add(choice);
}
```

For each visible choice:

```csharp
var button = new Button
{
    Text = choice.Label,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton
};
var captured = choice;
button.Pressed += () => OnChoicePressed(captured);
_choicesContainer.AddChild(button);
```

When `visibleChoices.Count == 0`, create one `Farewell.` button wired to `EmitClosedOnce`.

After actions are created:

```csharp
InitialFocusTarget = _choicesContainer.GetChildren().OfType<Button>().FirstOrDefault();
RefreshLayout();
if (InitialFocusTarget != null)
    Callable.From(InitialFocusTarget.GrabFocus).CallDeferred();
```

Keep the existing early `_terminalEmitted` guard before `GrantFlag`. Preserve outcome ordering and broken-`NextNodeId` close behavior. `RequestCancel()` calls `EmitClosedOnce()`.

- [ ] **Step 7: Add condition, progression, focus, and ownership tests**

Add:

```text
ConditionalChoices_UsesEvaluateAndRendersOnlyMetConditions
NonterminalChoice_RemovesOldActionsBeforeRenderingNextNode
Leaf_RendersSingleFarewellAction
GamepadAccept_OnFocusedChoiceAdvancesOnce
BrokenNextNode_ClosesOnce
SpeakerName_BlankHidesSpeakerLabel
ShellInternals_AreResolvedThroughModalShell
```

For shell internals, use:

```csharp
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var panel = shell.GetNode<PanelContainer>("%Panel");
var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
```

Do not use `screen.GetNode("%Panel")` or `screen.GetNode("%BodyScroll")`.

- [ ] **Step 8: Add wide-bottom and 640×360 scroll regressions**

At 640×360, 1280×720, and 1920×1080, await at least two layout frames and assert:

```csharp
var size = new Vector2(viewport.Size.X, viewport.Size.Y);
var insets = SiriusUiMetrics.SafeFrameInsets(size);
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var panel = shell.GetNode<PanelContainer>("%Panel");

AssertThat(shell.Compact).IsEqual(insets.Compact);
AssertThat(panel.Size.X).IsApproximately(size.X - insets.SideInset * 2f, 2f);
AssertThat(panel.Position.X).IsGreaterEqual(insets.SideInset - 2f);
AssertThat(panel.Position.X + panel.Size.X).IsLessEqual(size.X - insets.SideInset + 2f);
AssertThat(panel.Position.Y + panel.Size.Y).IsApproximately(size.Y - insets.Margin, 2f);
```

At 1920×1080, also assert `panel.Size.X > SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Large)` so the test rejects a bottom-anchored 960 px Pause modal.

For long content at 640×360:

```csharp
var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
var bar = bodyScroll.GetVScrollBar();
AssertThat(bar.MaxValue).IsGreater(bar.Page);
```

Focus the final choice and await a frame; assert `bodyScroll.ScrollVertical > 0` to prove follow-focus scrolling.

- [ ] **Step 9: Run additive screen suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~DialogueDialogTest"
```

Expected: both suites pass while the native implementation still exists.

- [ ] **Step 10: Commit**

```bash
git add scenes/ui/DialogueScreen.tscn scripts/ui/DialogueScreenController.cs tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored dialogue screen"
```

---

## Task 2: Cut `NpcInteractionController` Over to Hosted Dialogue and Keep the Tree Building

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs` — constructor-call argument only in this task
- Modify: `tests/ui/NpcInteractionControllerTest.cs`
- Test: `tests/game/GameTest.cs` for compile/regression gate
- Delete after green: `scripts/ui/DialogueDialog.cs`
- Delete after green: `tests/ui/DialogueDialogTest.cs`

**Interfaces:**
- Consumes: Task 1 `DialogueScreenController`, existing `UIScreenHost`, `UIScreenKinds.Dialogue`, and `UIScreenHostTestSupport.CreateHost(...)`.
- Produces: hosted Dialogue lifetime owned by `NpcInteractionController`; native Shop/Heal unchanged.

- [ ] **Step 1: Reuse the existing host fixture and write RED controller tests**

Setup:

```csharp
private HostFixture _hostFixture = null!;
private UIScreenHost _screenHost = null!;

[BeforeTest]
public async Task Setup()
{
    _sceneTree = (SceneTree)Engine.GetMainLoop();
    _hostFixture = await UIScreenHostTestSupport.CreateHost(this);
    _screenHost = _hostFixture.Host;

    _uiParent = new Node { Name = "LegacyNpcUiParent" };
    _sceneTree.Root.AddChild(_uiParent);
}
```

Cleanup uses:

```csharp
await UIScreenHostTestSupport.DisposeFixture(_hostFixture);
```

Do not manually load/configure another `UIScreenHost.tscn` fixture.

Hosted-screen lookup:

```csharp
var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
var dialogue = modalLayer.GetChildren().OfType<DialogueScreenController>().Single();
```

Add/convert:

```text
Begin_HostsOneDialogueEntry
DialogueCancel_ClosesHostedEntryAndCompletesOnce
Finish_WhileDialogueActive_ClosesHostedEntryAndCompletesOnce
MissingTree_CreatesNoHostedEntryAndCompletesOnce
InvalidRoot_CreatesNoHostedEntryAndCompletesOnce
ShopOutcome_ClosesDialogueBeforeNativeShop
HealOutcome_ClosesDialogueBeforeNativeHeal
RejectedPresentation_FreesCandidateAndCompletesOnce
```

- [ ] **Step 2: Run controller suite RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: compile failures because the constructor/hosted fields have not moved yet.

- [ ] **Step 3: Change the constructor and sole production caller in the same slice**

Controller signature:

```csharp
public NpcInteractionController(
    GameManager gameManager,
    UIScreenHost screenHost,
    Node uiParent,
    NpcData npc,
    Character player,
    HashSet<string> questFlags)
```

Add:

```csharp
private readonly UIScreenHost _screenHost;
private DialogueScreenController? _dialogueScreen;
private UIScreenHandle? _dialogueHandle;
```

In `Game.OnNpcInteracted`, change only the constructor call in Task 2:

```csharp
_npcInteractionController = new NpcInteractionController(
    _gameManager,
    _screenHost!,
    GetNode("UI"),
    npcData,
    _gameManager.Player,
    _questFlags);
```

The null-forgiving operator is temporary sequencing only. Production `Game.tscn` already requires the host; Task 3 replaces this with the explicit pre-start guard. Do not move `StartNpcInteraction()` or teardown behavior yet.

- [ ] **Step 4: Replace native `Begin()` construction with configure → present**

```csharp
var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
if (packed == null)
{
    GD.PushError("[NpcInteractionController] DialogueScreen.tscn not found.");
    Finish();
    return;
}

var screen = packed.Instantiate<DialogueScreenController>();
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

Present:

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

On rejection, disconnect, queue-free the unhosted candidate, and call `Finish()`. Store `_dialogueScreen` / `_dialogueHandle` only after `Opened` with a handle.

- [ ] **Step 5: Route terminal outcomes through synchronous host close**

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
            GD.PushWarning($"[NpcInteractionController] Unhandled DialogueOutcomeType value {outcomeInt} — treating as CloseAndReturn.");
            Finish();
            break;
    }
}

private void OnDialogueClosed()
{
    CloseDialoguePresentation(UIScreenCloseReason.ExplicitAction);
    Finish();
}
```

`ClearDialoguePresentation(screen)` disconnects signals and clears local references only when they refer to that screen. `Finish()` closes active Dialogue, cleans native Shop/Heal, and emits `InteractionComplete` once.

- [ ] **Step 6: Prove atomic Dialogue → native child replacement**

Before touching Shop/Heal in the existing tests:

```csharp
AssertThat(_screenHost.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(
    _screenHost.GetNode<Control>("ModalLayer")
        .GetChildren().OfType<DialogueScreenController>().Any()).IsFalse();
```

Then preserve existing Shop/Heal Cancel and exactly-once completion assertions under `_uiParent`.

- [ ] **Step 7: Prove host rejection using the existing fixture**

Pre-register another view with the same kind:

```csharp
var existing = _hostFixture.Track(new Control());
var opened = _screenHost.TryPresent(
    existing,
    UIScreenHostTestSupport.Spec(UIScreenKinds.Dialogue));
AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
```

Then call `controller.Begin()` and assert the candidate does not remain in `ModalLayer`, no Shop/Heal opens, and completion emits once. Do not add a production injection seam.

- [ ] **Step 8: Run Task 2 compile/test gate before deleting native Dialogue**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: all selected tests pass and build has 0 errors. This gate proves the constructor call moved with the signature.

- [ ] **Step 9: Delete native Dialogue after the hosted cutover is green**

Delete:

```text
scripts/ui/DialogueDialog.cs
tests/ui/DialogueDialogTest.cs
```

Audit:

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests
```

Expected: zero active matches.

- [ ] **Step 10: Commit**

```bash
git add \
  scripts/ui/NpcInteractionController.cs \
  scripts/game/Game.cs \
  tests/ui/NpcInteractionControllerTest.cs \
  scripts/ui/DialogueDialog.cs \
  tests/ui/DialogueDialogTest.cs
git commit -m "feat(ui): host NPC dialogue through UIScreenHost"
```

---

## Task 3: Add Production Host Guard, Domain Teardown, and Real NPC-Route Regressions

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify only if evidence requires: `scripts/ui/DialogueScreenController.cs`
- Modify only if evidence requires: `scripts/ui/NpcInteractionController.cs`

**Interfaces:**
- Consumes: Task 2 hosted `NpcInteractionController` and current production `Game.tscn` / Floor GF `NpcSpawn` data.
- Produces: explicit host validation, one guarded domain-end helper, and end-to-end evidence that real NPC interaction owns Cancel/teardown correctly.

- [ ] **Step 1: Write RED production-route helper/tests before changing `Game` cleanup**

In `GameplayPauseHostTest`, add a helper that exercises the same coordinate contract as `OnNpcInteracted`:

```csharp
private static Vector2I FindNpcInternalPosition(Game game, string npcId)
{
    var grid = game.GetNode<FloorManager>("FloorManager").CurrentGridMap;
    var floorRoot = grid.GetParent();
    var spawn = game.GetTree().GetNodesInGroup("NpcSpawn")
        .OfType<NpcSpawn>()
        .Single(candidate =>
            candidate.BelongsToFloor(floorRoot) &&
            candidate.NpcId == npcId);

    var origin = GetPrivateField<Vector2I>(grid, "_tilemapOrigin");
    var internalPosition = spawn.GridPosition - origin;
    AssertThat(grid.InternalGridToTilemapCoords(internalPosition))
        .IsEqual(spawn.GridPosition);
    return internalPosition;
}
```

Use authored Floor GF `village_shopkeeper` or `village_healer`; do not create a fake host-only Dialogue entry.

Open production Dialogue with the existing reflection helper:

```csharp
var internalPosition = FindNpcInternalPosition(_game!, "village_shopkeeper");
InvokePrivateVoid(_game!, "OnNpcInteracted", internalPosition);
await AwaitFrames(2);
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();
```

Add:

```text
NpcDialogue_RealRouteHostsModalWithoutPausingTree
NpcDialogue_NormalTerminalRestoresGameplay
NpcDialogue_GameExitClearsDomainFlagAfterControllerUnsubscribe
NpcDialogue_MissingHostDoesNotStartDomainInteraction
```

- [ ] **Step 2: Implement the pre-`StartNpcInteraction` host guard and remove Task 2's null-forgiving call**

```csharp
var screenHost = _screenHost;
if (screenHost == null || !GodotObject.IsInstanceValid(screenHost))
{
    GD.PushError("[Game] Cannot start NPC interaction without UIScreenHost.");
    UpdateInteractionPrompt();
    return;
}

_gameManager.StartNpcInteraction();
UpdateInteractionPrompt();

_npcInteractionController = new NpcInteractionController(
    _gameManager,
    screenHost,
    GetNode("UI"),
    npcData,
    _gameManager.Player,
    _questFlags);
```

The guard runs before the domain flag is set. No fallback native Dialogue is created.

- [ ] **Step 3: Add one guarded domain-end helper and use it on every root-owned cleanup path**

```csharp
private void EndNpcInteractionIfActive()
{
    if (_gameManager != null &&
        GodotObject.IsInstanceValid(_gameManager) &&
        _gameManager.IsInNpcInteraction)
    {
        _gameManager.EndNpcInteraction();
    }
}
```

Update normal completion:

```csharp
private void OnNpcInteractionComplete()
{
    if (_npcInteractionController != null)
        _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;

    EndNpcInteractionIfActive();
    _npcInteractionController = null;
    UpdatePlayerUI();
    UpdateInteractionPrompt();
}
```

Update `Begin()` exception fallback to call `EndNpcInteractionIfActive()` instead of directly ending. Update `OnNpcInteractionResetRequested()` fallback the same way.

In `_ExitTree()` preserve the required order:

```csharp
if (_npcInteractionController != null)
{
    _npcInteractionController.InteractionComplete -= OnNpcInteractionComplete;
    _npcInteractionController.Finish();
    _npcInteractionController = null;
}
EndNpcInteractionIfActive();
```

This explicitly covers the existing unsubscribe-before-`Finish()` teardown path.

- [ ] **Step 4: Pin the production hosted policy on the real NPC route**

After `OnNpcInteracted(...)`:

```csharp
var tree = (SceneTree)Engine.GetMainLoop();
var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
var gameUi = _game.GetNode<Control>("UI/GameUI");
var gameManager = _game.GetNode<GameManager>("GameManager");
var entry = FindEntry(host, UIScreenKinds.Dialogue);

AssertThat(gameManager.IsInNpcInteraction).IsTrue();
AssertThat(tree.Paused).IsFalse();
AssertThat(entry.Policy.ProcessPolicy).IsEqual(UIProcessPolicy.Always);
AssertThat(entry.Policy.PauseTree).IsFalse();
AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Visible);
AssertThat(entry.Policy.LowerLayers).IsEqual(UILowerLayerPolicy.VisibleInert);
AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
AssertThat(gameUi.Visible).IsTrue();
```

Also assert the `DialogueScreenController` parent is `host.GetNode<Control>("ModalLayer")`.

- [ ] **Step 5: Prove normal completion restores the domain through the real route**

Use the shopkeeper root's existing `Goodbye.` choice:

```csharp
var dialogue = host.GetNode<Control>("ModalLayer")
    .GetChildren().OfType<DialogueScreenController>().Single();
FindButton(dialogue, "Goodbye.").EmitSignal(Button.SignalName.Pressed);
await AwaitFrames(2);

AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
AssertThat(tree.Paused).IsFalse();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
```

Use the existing interaction-prompt fixture/probe pattern to assert the prompt recomputes when an interactable remains adjacent; do not add a public Game getter.

- [ ] **Step 6: Prove physical keyboard and controller Cancel do not fall through to Pause**

In `GameInputLifecycleTest`, keep the existing flag-only `ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler` unchanged. It still documents the native Shop/Heal phase where `IsInNpcInteraction` is true but no hosted Dialogue entry exists.

Add a real hosted route using `InstantiateGameScene(_viewport!)`, the same `FindNpcInternalPosition` helper pattern, and private `OnNpcInteracted(...)` invocation.

Keyboard:

```csharp
ConfigureCancelBindings(Key.P);
// open real Dialogue
PushPhysicalKeyDown(Key.P);
await AwaitFrames(2);
try
{
    AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
    AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
    AssertThat(gameManager.IsInNpcInteraction).IsFalse();
}
finally
{
    ReleasePhysicalKey(Key.P);
}
```

Controller uses the existing helpers:

```csharp
var button = (JoyButton)10;
ConfigureCancelBindings(Key.P, new InputEventJoypadButton { ButtonIndex = button });
// open real Dialogue
PushPhysicalJoypadButtonPressAndRelease(button);
await AwaitFrames(2);

AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
```

Do not replace these with `GameManager.StartNpcInteraction()` alone.

- [ ] **Step 7: Prove teardown clears the flag after the completion subscriber is removed**

Open real hosted Dialogue, capture the `GameManager`, then detach the Game node without immediately freeing it:

```csharp
AssertThat(gameManager.IsInNpcInteraction).IsTrue();
_viewport!.RemoveChild(_game!);
await AwaitFrames(1);
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
```

The detached Game remains valid for the assertion and can be freed during test cleanup. This specifically exercises `Game._ExitTree()` after `InteractionComplete` is unsubscribed.

- [ ] **Step 8: Run production lifecycle/input gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: selected suites pass and build has 0 errors.

- [ ] **Step 9: Commit**

```bash
git add \
  scripts/game/Game.cs \
  tests/game/GameplayPauseHostTest.cs \
  tests/game/GameInputLifecycleTest.cs \
  scripts/ui/DialogueScreenController.cs \
  scripts/ui/NpcInteractionController.cs
git commit -m "test(ui): pin hosted dialogue gameplay lifecycle"
```

Only stage the two production controller files when the RED integration tests required a minimal fix there.

---

## Task 4: Reconcile Lifecycle Docs and Run Final Verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: every HPA-569 production/test file from Tasks 1–3

**Interfaces:**
- Consumes: final hosted behavior.
- Produces: accurate lifecycle documentation and verification evidence; no runtime API.

- [ ] **Step 1: Update only the Dialogue/NPC lifecycle rows**

Record:

```text
DialogueScreenController : Control is scene-authored and bottom-aligned.
NpcInteractionController owns the Dialogue host handle and Dialogue → Shop/Heal transition.
PauseTree=false; BlockGameplayInput=true; HUD/world retained; cursor visible.
Cancel=Consume and InterceptCancel→RequestCancel()→ConsumeHere.
Conditions continue to use IDialogueCondition.Evaluate(...).
Host closes Dialogue before native Shop/Heal opens.
Game.EndNpcInteractionIfActive() covers normal completion, startup failure, reset fallback, and teardown.
HPA-570 still owns Shop/Heal presentation migration.
```

Do not rewrite unrelated HPA-376 rows.

- [ ] **Step 2: Run stale-contract and scope audits**

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests
rg -n "Condition\.IsMet|UIInputInterception\.Consumed" scripts scenes tests
rg -n "UIScreenKinds\.Dialogue|DialogueScreenController|DialogueScreen\.tscn" scripts scenes tests docs/ui/hpa-376
```

Expected:

- zero `DialogueDialog` references;
- zero invented `Condition.IsMet` or invalid interception-enum references;
- every active Dialogue presentation path points at the hosted screen.

Scope:

```bash
git diff --name-only origin/main...HEAD
```

Reject unrelated Shop/Heal migration, portrait/model changes, host API/kind changes, Theme/metric additions, or new service/presenter/router abstractions.

- [ ] **Step 3: Run focused HPA-569 suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: 0 failures.

- [ ] **Step 4: Run dialogue-domain and neighboring interaction suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~Dialogue|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~HealDialogTest"
```

Expected: 0 failures. Shop/Heal domain/presentation behavior remains otherwise unchanged.

- [ ] **Step 5: Run full suite, build, and diff check**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
dotnet build Sirius.sln --no-restore --nologo
git diff --check
```

Expected:

- full suite passes;
- build has 0 errors;
- diff check is clean.

Record unchanged NuGet/orphan-node warning noise separately; do not turn pre-existing warning cleanup into HPA-569 work.

- [ ] **Step 6: Commit lifecycle documentation**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record hosted dialogue lifecycle"
```

## Final Review Checklist

Before HPA-569 implementation is considered complete:

- [ ] Static Dialogue chrome is scene-authored.
- [ ] Final panel is wide, bottom-aligned, safe-frame bounded, and uses compact mode at 640×360.
- [ ] Shell-owned `%Panel` / `%BodyScroll` are accessed through `%ModalShell`.
- [ ] `SiriusModalShell` API and `SiriusUiMetrics` remain unchanged.
- [ ] `NpcInteractionController` remains the single NPC interaction orchestration owner.
- [ ] The Task 2 constructor cutover and sole `Game` caller compile in the same commit.
- [ ] Conditions use `IDialogueCondition.Evaluate(...)`; no alias/wrapper is added.
- [ ] Dialogue tree semantics and quest-flag side effects match the retired implementation.
- [ ] Configured Cancel and visible terminal actions share one terminal latch.
- [ ] Dialogue never pauses the scene tree.
- [ ] Gameplay input is blocked while hosted Dialogue is active and restored once afterward.
- [ ] The real `OnNpcInteracted` path is covered through an authored `NpcSpawn`, not only a domain-flag fixture.
- [ ] The flag-only NPC Cancel regression remains for native Shop/Heal.
- [ ] Shop/Heal still work through their legacy dialogs after Dialogue outcomes.
- [ ] 640×360 long content scrolls through the shell body and follow-focus reaches the final choice.
- [ ] Teardown clears `IsInNpcInteraction` after `InteractionComplete` is unsubscribed.
- [ ] `UIScreenHostTestSupport.CreateHost(...)` is reused in controller tests.
- [ ] No native `DialogueDialog` references remain.
- [ ] Full suite/build/diff-check pass before implementation completion is claimed.
