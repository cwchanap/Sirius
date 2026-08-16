# HPA-569 Hosted Dialogue Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `DialogueDialog` with one scene-authored wide-bottom Dialogue surface hosted by the gameplay `UIScreenHost`, preserving current dialogue-tree behavior and exactly-once NPC-interaction cleanup.

**Architecture:** Keep `NpcInteractionController` as the single Dialogue → Shop/Heal orchestration owner. First extend `SiriusModalShell` with one concrete `Full` width class required by Dialogue; then build Dialogue inside a scene-owned bottom `SafeFrame`, present it through the existing gameplay host, and leave Shop/Heal native for HPA-570. `Game` owns only the host prerequisite and NPC domain lifecycle cleanup.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme, `SiriusModalShell`, `SiriusUiMetrics`, and `UIScreenHost`.

## Global Constraints

- Reuse `SiriusModalShell`; do not add another shell, placement enum, or modal framework.
- Add exactly one new shell width class: `SiriusModalSizeClass.Full`.
- `Full` fills the width supplied to `RefreshPresentation(...)`, capped at `SiriusUiMetrics.MaximumContentWidth`; it owns no placement or height policy.
- Preserve existing Small/Medium/Large width behavior.
- Reuse `UIScreenKinds.Dialogue`; do not add host kinds, exclusive groups, policy factories, or host APIs.
- Keep `NpcInteractionController` as orchestration owner; do not move dialogue progression into `Game`.
- Preserve `IDialogueCondition.Evaluate(...)`, branching, `GrantFlag`, outcomes, leaf completion, and exactly-once terminal/domain side effects.
- Dialogue never pauses the scene tree.
- Keep world context and the gameplay HUD visible beneath Dialogue.
- Follow HPA-373 §9.8 geometry as a wide bottom surface, but do not claim its portrait requirement is complete; HPA-625 owns portrait data/art/rendering.
- `DialogueScreen` owns placement with a scene-authored `%SafeFrame`; never write shell-owned `%Panel` geometry from `DialogueScreenController`.
- Standard Dialogue stays within the lower 45% of the safe-frame height; compact uses the full safe-frame height and scrolls.
- Use the shell-owned body scroll as the single scroll owner; disable internal `RichTextLabel` scrolling.
- Do not add portrait assets/model fields or infer portrait semantics from `NpcData.SpriteType`.
- Shop and Heal remain native dialogs in this ticket.
- No presenter, view model, interaction service, navigation service, event bus, host facade, typewriter/history/auto-advance, persistence, quest redesign, new Theme token/art, or compatibility shim.

---

## File Structure

### Task 0 — shared width extension

**Modify**

- `scripts/ui/theme/SiriusUiTypes.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `tests/ui/theme/SiriusUiContractsTest.cs`
- `tests/ui/components/SiriusModalShellTest.cs`

### Dialogue migration

**Create**

- `scenes/ui/DialogueScreen.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `tests/ui/DialogueScreenControllerTest.cs`

**Modify**

- `scripts/ui/NpcInteractionController.cs`
- `scripts/game/Game.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

**Delete after equivalent hosted coverage is green**

- `scripts/ui/DialogueDialog.cs`
- `tests/ui/DialogueDialogTest.cs`

**Audit-only unless a focused failing test proves otherwise**

- `scripts/data/npc/DialogueTree.cs`
- `scripts/data/npc/DialogueCatalog.cs`
- `scripts/data/npc/DialogueCondition.cs`
- `scripts/data/npc/NpcData.cs`
- `scripts/game/NpcSpawn.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/ShopDialog.cs`
- `scripts/ui/HealDialog.cs`

---

## Risks and Mitigations

### Shell width can be overwritten by shell property setters

**Risk:** `Title`, `Severity`, `SizeClass`, and `Compact` call `RefreshIfReady()`. A screen-local `%Panel.CustomMinimumSize` write is not a stable width contract.

**Mitigation:** `Full` is implemented inside `SiriusModalShell.RefreshPresentation(...)`. Dialogue never writes shell-owned panel width. A shell test mutates `Title` after a constrained `Full` refresh and asserts the width remains unchanged.

### `SiriusModalSizeClass` is a closed tested contract

**Risk:** adding `Full` only in production code makes `SiriusUiContractsTest.ClosedEnums_ContainOnlyApprovedValues` fail and leaves the metric contract incomplete.

**Mitigation:** Task 0 updates the closed enum test and `ModalWidth(Full)` assertion in the same TDD slice.

### Editing an instanced shell child would require editable-instance state

**Risk:** overriding `%Panel` anchors/properties inside `SiriusModalShell.tscn` from `DialogueScreen.tscn` depends on editable-child scene serialization not used elsewhere in this repository.

**Mitigation:** author a `%SafeFrame` in `DialogueScreen.tscn`, put `%ModalShell` inside it, and add Dialogue content under the existing shell `BodyHost` path exactly as Pause does. No `[editable path="ModalShell"]` and no shell-child geometry override.

### Standard long Dialogue can erase world context

**Risk:** the shell's generic body-height policy can grow near viewport height. Width-only assertions would still pass.

**Mitigation:** Dialogue owns `StandardDialogueHeightFraction = 0.45f`. Standard `%SafeFrame` is a lower safe-frame band of that height; compact uses the full safe height. Tests assert the actual panel remains inside the band at 1280×720 and 1920×1080.

### Pre-ready configuration can touch unbound scene nodes

**Risk:** the candidate is unparented before `TryPresent`, so authored nodes are not bound yet.

**Mitigation:** `TryStartDialogue(...)` validates/stores data only; `_Ready()` binds nodes and renders the stored root. A test calls `TryStartDialogue(...)` before `AddChild(...)`.

### A second start could re-arm a spent terminal latch

**Risk:** resetting `_terminalEmitted` in a reusable `TryStartDialogue(...)` would permit a second terminal sequence on the same screen.

**Mitigation:** each screen is single-start. `_started` rejects every second successful start and `_terminalEmitted` is never reset after the instance has started.

### Queued old choices can remain focusable for one frame

**Risk:** `QueueFree()` without removal leaves stale buttons in layout/focus order until frame end.

**Mitigation:** remove each old action from `%ChoicesContainer` immediately, then `QueueFree()` it before creating/focusing replacements.

### Constructor cutover can create a known nullable-host bridge

**Risk:** changing `NpcInteractionController` to require a host while passing `_screenHost!` from `Game` leaves a latent NRE that the production scene test cannot expose.

**Mitigation:** Task 2 moves the constructor change, sole production caller, and pre-`StartNpcInteraction` host guard together. There is no `_screenHost!` bridge commit.

### Teardown currently unsubscribes before domain completion

**Risk:** `Game._ExitTree()` removes `InteractionComplete`, then calls `Finish()`, so `OnNpcInteractionComplete()` cannot clear `IsInNpcInteraction`.

**Mitigation:** Task 3 adds one guarded `EndNpcInteractionIfActive()` helper used by normal completion, startup failure, reset fallback, and `_ExitTree()` after controller cleanup.

### Production integration tests can accidentally test only the flag

**Risk:** `GameManager.StartNpcInteraction()` does not open Dialogue.

**Mitigation:** production tests use real `Game.tscn`, an authored Floor GF `NpcSpawn`, the current `_tilemapOrigin`, and private `OnNpcInteracted(...)`. Keep the existing flag-only Cancel test for the native Shop/Heal phase.

---

## Task 0: Add the `Full` Sirius Modal Width Class

**Files:**
- Modify: `scripts/ui/theme/SiriusUiTypes.cs`
- Modify: `scripts/ui/theme/SiriusUiMetrics.cs`
- Modify: `scripts/ui/components/SiriusModalShell.cs`
- Modify: `tests/ui/theme/SiriusUiContractsTest.cs`
- Modify: `tests/ui/components/SiriusModalShellTest.cs`

**Interfaces:**
- Produces: `SiriusModalSizeClass.Full`.
- Contract: `Full` fills the caller-supplied available width up to `MaximumContentWidth`; existing size classes are unchanged.

- [ ] **Step 1: Write RED enum/metric contract assertions**

Update `SiriusUiContractsTest.ClosedEnums_ContainOnlyApprovedValues`:

```csharp
AssertThat(Enum.GetValues<SiriusModalSizeClass>()).ContainsExactly(
    SiriusModalSizeClass.Small,
    SiriusModalSizeClass.Medium,
    SiriusModalSizeClass.Large,
    SiriusModalSizeClass.Full);
```

Update `Metrics_MatchApprovedBreakpointsSizesAndViewports`:

```csharp
AssertThat(SiriusUiMetrics.ModalWidth(SiriusModalSizeClass.Full))
    .IsEqual((int)SiriusUiMetrics.MaximumContentWidth);
```

- [ ] **Step 2: Write RED shell behavior tests**

Add to `SiriusModalShellTest`:

```csharp
[TestCase]
public async Task RefreshPresentation_FullFillsSuppliedSafeWidthAndSurvivesTitleMutation()
{
    _shell.SizeClass = SiriusModalSizeClass.Full;
    _shell.Compact = false;
    var available = new Vector2(1232, 320);

    _shell.RefreshPresentation(available);
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

    var panel = _shell.GetNode<PanelContainer>("%Panel");
    AssertThat(panel.CustomMinimumSize.X).IsEqual(1232f);

    _shell.Title = "Mira the Merchant";
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

    AssertThat(panel.CustomMinimumSize.X).IsEqual(1232f);
}

[TestCase]
public async Task RefreshPresentation_FullCompactUsesSuppliedSafeWidthWithoutSecondMargin()
{
    _shell.SizeClass = SiriusModalSizeClass.Full;
    _shell.Compact = true;
    _shell.RefreshPresentation(new Vector2(616, 336));
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

    var panel = _shell.GetNode<PanelContainer>("%Panel");
    AssertThat(panel.CustomMinimumSize.X).IsEqual(616f);
}
```

- [ ] **Step 3: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusUiContractsTest|FullyQualifiedName~SiriusModalShellTest"
```

Expected: FAIL because `Full` does not exist.

- [ ] **Step 4: Add the enum and metric**

In `SiriusUiTypes.cs`:

```csharp
public enum SiriusModalSizeClass
{
    Small,
    Medium,
    Large,
    Full
}
```

In `SiriusUiMetrics.ModalWidth(...)`:

```csharp
public static int ModalWidth(SiriusModalSizeClass sizeClass) => sizeClass switch
{
    SiriusModalSizeClass.Small => 420,
    SiriusModalSizeClass.Medium => 640,
    SiriusModalSizeClass.Large => 960,
    SiriusModalSizeClass.Full => (int)MaximumContentWidth,
    _ => throw new ArgumentOutOfRangeException(nameof(sizeClass), sizeClass, null)
};
```

- [ ] **Step 5: Make shell width ownership explicit**

Replace only the width calculation in `SiriusModalShell.RefreshPresentation(...)`:

```csharp
var width = SizeClass == SiriusModalSizeClass.Full
    ? Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X)
    : Compact
        ? availableSize.X - SiriusUiMetrics.SafeMargin(true) * 2
        : Mathf.Min(SiriusUiMetrics.ModalWidth(SizeClass), availableSize.X * 0.90f);

_panel.CustomMinimumSize = new Vector2(Mathf.Max(0, width), 0);
```

Do not change panel anchors, body-height calculation, or existing class behavior.

- [ ] **Step 6: Run shared component regression gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusUiContractsTest|FullyQualifiedName~SiriusModalShellTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SiriusPromptTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: tests pass and build has 0 errors.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/theme/SiriusUiTypes.cs \
        scripts/ui/theme/SiriusUiMetrics.cs \
        scripts/ui/components/SiriusModalShell.cs \
        tests/ui/theme/SiriusUiContractsTest.cs \
        tests/ui/components/SiriusModalShellTest.cs
git commit -m "feat(ui): add full modal width class"
```

---

## Task 1: Add the Scene-Authored Dialogue Screen Additively

**Files:**
- Create: `scenes/ui/DialogueScreen.tscn`
- Create: `scripts/ui/DialogueScreenController.cs`
- Create: `tests/ui/DialogueScreenControllerTest.cs`
- Read-only: `scripts/ui/DialogueDialog.cs`
- Read-only: `scripts/data/npc/DialogueCondition.cs`
- Read-only: `scripts/ui/PauseScreenController.cs`
- Read-only: `scripts/ui/components/SiriusModalShell.cs`
- Read-only: `docs/superpowers/specs/2026-07-25-sirius-ui-visual-language-design.md`

**Interfaces:**
- Consumes: `SiriusModalSizeClass.Full`, `NpcData`, `DialogueTree`, `DialogueNode`, `DialogueChoice`, `IDialogueCondition.Evaluate(...)`, `Character`, `HashSet<string>`.
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

- [ ] **Step 1: Write RED pre-ready/start/terminal tests**

Create `DialogueScreenControllerTest` with a `SubViewport` fixture and an unparented candidate helper.

Add:

```text
TryStartDialogue_BeforeReady_RendersAfterAttach
TryStartDialogue_MissingRootReturnsFalseWithoutTerminalSignal
TryStartDialogue_SecondSuccessfulStartIsRejected
RequestCancelTwice_EmitsDialogueClosedOnce
OutcomeThenCancel_EmitsOutcomeOnly
SecondQueuedTerminalChoice_GrantsOnlyFirstFlag
Scene_UsesSafeFrameModalShellAndContainsNoAcceptDialog
```

The pre-ready test order is load → instantiate → `TryStartDialogue(...)` → add to viewport → await frames → inspect authored nodes.

For second-start protection:

```csharp
AssertThat(screen.TryStartDialogue(npc, tree, player, flags)).IsTrue();
screen.RequestCancel();
AssertThat(screen.TryStartDialogue(npc, tree, player, flags)).IsFalse();
screen.RequestCancel();
AssertThat(closedCount).IsEqual(1);
```

- [ ] **Step 2: Run RED**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest"
```

Expected: FAIL because the scene/controller do not exist.

- [ ] **Step 3: Author scene-owned placement, not editable shell internals**

Create this hierarchy:

```text
DialogueScreen (Control, full rect)
└── SafeFrame (%SafeFrame, Control, full rect initially)
    └── ModalShell (%ModalShell, SiriusModalShell, SizeClass = Full)
        └── Panel/Margin/RootLayout/BodyScroll/BodyHost
            ├── SpeakerLabel (%SpeakerLabel)
            ├── DialogueText (%DialogueText)
            └── ChoicesContainer (%ChoicesContainer)
```

Required authored state:

```text
DialogueScreen: full rect
SafeFrame: full rect; mouse_filter Ignore
ModalShell: SizeClass Full
SpeakerLabel: SiriusSection; word-smart wrap
DialogueText: SiriusBody; FitContent true; selection disabled; Scroll Active false
ChoicesContainer: VBoxContainer; horizontal ExpandFill; separation 8
No scrim
No portrait node
No [editable path="ModalShell"]
```

The Dialogue scene may add children under the shell's existing `BodyHost` path, matching `PauseScreen.tscn`. It must not override `%Panel` properties.

- [ ] **Step 4: Implement one-shot pre-ready state**

Use:

```csharp
private const float StandardDialogueHeightFraction = 0.45f;

private Control _safeFrame = null!;
private SiriusModalShell _shell = null!;
private Label _speakerLabel = null!;
private RichTextLabel _textLabel = null!;
private VBoxContainer _choicesContainer = null!;

private NpcData? _npc;
private DialogueTree? _tree;
private Character? _player;
private HashSet<string>? _questFlags;
private DialogueNode? _currentNode;
private bool _started;
private bool _terminalEmitted;

public bool TryStartDialogue(
    NpcData npc,
    DialogueTree tree,
    Character player,
    HashSet<string> questFlags)
{
    if (_started)
        return false;

    var root = tree.Root;
    if (root == null)
        return false;

    _started = true;
    _npc = npc;
    _tree = tree;
    _player = player;
    _questFlags = questFlags;
    _currentNode = root;

    if (IsNodeReady())
        ShowNode(root);
    return true;
}
```

Do not assign `_terminalEmitted = false` inside `TryStartDialogue(...)`; the fresh screen instance already starts false and cannot be started twice.

- [ ] **Step 5: Bind only Dialogue-owned placement/body nodes**

```csharp
public override void _Ready()
{
    _safeFrame = GetNode<Control>("%SafeFrame");
    _shell = GetNode<SiriusModalShell>("%ModalShell");
    _speakerLabel = GetNode<Label>("%SpeakerLabel");
    _textLabel = GetNode<RichTextLabel>("%DialogueText");
    _choicesContainer = GetNode<VBoxContainer>("%ChoicesContainer");

    Resized += OnResized;
    RefreshLayout();

    if (_currentNode != null)
        ShowNode(_currentNode);
}

public override void _ExitTree()
{
    Resized -= OnResized;
}
```

Do not bind `%Panel` or `%BodyScroll` in production Dialogue code.

- [ ] **Step 6: Implement the lower safe-frame band**

```csharp
private void OnResized() => RefreshLayout();

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

No shell-owned node geometry is written here.

- [ ] **Step 7: Move `ShowNode` / `OnChoicePressed` behavior intact**

Start `ShowNode(...)` with:

```csharp
_currentNode = node;
_shell.Title = _npc?.DisplayName ?? string.Empty;
_speakerLabel.Text = node.SpeakerName ?? string.Empty;
_speakerLabel.Visible = !string.IsNullOrWhiteSpace(node.SpeakerName);
_textLabel.Text = node.Text ?? string.Empty;

foreach (Node child in _choicesContainer.GetChildren())
{
    _choicesContainer.RemoveChild(child);
    child.QueueFree();
}
```

Evaluate conditions with the existing API only:

```csharp
var visibleChoices = new List<DialogueChoice>();
foreach (var choice in node.Choices)
{
    if (choice.Condition.Evaluate(_player!, _questFlags!))
        visibleChoices.Add(choice);
}
```

Use one local button creator so choices and the leaf share styling:

```csharp
private static Button CreateActionButton(string text) => new()
{
    Text = text,
    AutowrapMode = TextServer.AutowrapMode.WordSmart,
    ThemeTypeVariation = SiriusThemeTypes.SecondaryButton,
    SizeFlagsHorizontal = SizeFlags.ExpandFill
};
```

For choices:

```csharp
foreach (var choice in visibleChoices)
{
    var button = CreateActionButton(choice.Label);
    var captured = choice;
    button.Pressed += () => OnChoicePressed(captured);
    _choicesContainer.AddChild(button);
}
```

For a leaf:

```csharp
var close = CreateActionButton("Farewell.");
close.Pressed += EmitClosedOnce;
_choicesContainer.AddChild(close);
```

Then:

```csharp
InitialFocusTarget = _choicesContainer.GetChildren()
    .OfType<Button>()
    .FirstOrDefault();
RefreshLayout();
if (InitialFocusTarget != null)
    Callable.From(InitialFocusTarget.GrabFocus).CallDeferred();
```

Keep the retired `OnChoicePressed(...)` order exactly:

1. return if `_terminalEmitted`;
2. add `GrantFlag` if present;
3. emit terminal outcome if non-None;
4. close when `NextNodeId == null`;
5. resolve next node;
6. broken next ID logs + closes once;
7. otherwise `ShowNode(nextNode)`.

`RequestCancel()` calls `EmitClosedOnce()`.

- [ ] **Step 8: Add semantic/focus tests**

Add:

```text
ConditionalChoices_UsesEvaluateAndRendersOnlyMetConditions
NonterminalChoice_RemovesOldActionsBeforeRenderingNextNode
Leaf_RendersSingleThemedFarewellAction
GamepadAccept_OnFocusedChoiceAdvancesOnce
BrokenNextNode_ClosesOnce
SpeakerName_BlankHidesSpeakerLabel
```

For the leaf:

```csharp
var farewell = FindButton(screen, "Farewell.");
AssertThat(farewell.ThemeTypeVariation)
    .IsEqual(SiriusThemeTypes.SecondaryButton);
```

- [ ] **Step 9: Add layout/overflow tests that can catch a full-screen Dialogue**

At 1280×720 and 1920×1080:

```csharp
var insets = SiriusUiMetrics.SafeFrameInsets(viewport.Size);
var safeHeight = viewport.Size.Y - insets.Margin * 2f;
var expectedBandHeight = safeHeight * 0.45f;
var safeFrame = screen.GetNode<Control>("%SafeFrame");
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var panel = shell.GetNode<PanelContainer>("%Panel");

AssertThat(safeFrame.Size.X)
    .IsEqualApprox(viewport.Size.X - insets.SideInset * 2f, 1f);
AssertThat(safeFrame.Size.Y).IsEqualApprox(expectedBandHeight, 1f);
AssertThat(panel.Size.X).IsEqualApprox(safeFrame.Size.X, 1f);
AssertThat(panel.Size.Y).IsLessEqual(expectedBandHeight + 1f);
AssertThat(panel.GetGlobalRect().Position.Y)
    .IsGreaterEqual(safeFrame.GetGlobalRect().Position.Y - 1f);
AssertThat(panel.GetGlobalRect().End.Y)
    .IsLessEqual(safeFrame.GetGlobalRect().End.Y + 1f);
```

These assertions run after `TryStartDialogue(...)` has applied the NPC title, proving a shell property write does not revert Full width.

At 640×360 assert `%SafeFrame` fills the full safe height, then create multi-paragraph text plus enough wrapped choices to overflow. Resolve scroll only through the shell:

```csharp
var shell = screen.GetNode<SiriusModalShell>("%ModalShell");
var bodyScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
var bar = bodyScroll.GetVScrollBar();
AssertThat(bar.MaxValue).IsGreater(bar.Page);
```

Focus the final choice, await a frame, and assert `bodyScroll.ScrollVertical > 0`.

- [ ] **Step 10: Run additive Dialogue/native suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~SiriusModalShellTest"
```

Expected: hosted screen and retired native characterization both pass.

- [ ] **Step 11: Commit**

```bash
git add scenes/ui/DialogueScreen.tscn \
        scripts/ui/DialogueScreenController.cs \
        tests/ui/DialogueScreenControllerTest.cs
git commit -m "feat(ui): add scene-authored dialogue screen"
```

---

## Task 2: Cut `NpcInteractionController` and the Sole `Game` Caller Over Together

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`
- Test: `tests/game/GameTest.cs`
- Delete after green: `scripts/ui/DialogueDialog.cs`
- Delete after green: `tests/ui/DialogueDialogTest.cs`

**Interfaces:**
- Consumes: Task 1 `DialogueScreenController`, existing `UIScreenHost`, `UIScreenKinds.Dialogue`, and `UIScreenHostTestSupport.CreateHost(...)`.
- Produces: hosted Dialogue lifetime owned by `NpcInteractionController`; native Shop/Heal unchanged; production Game never starts an NPC interaction without a valid host.

- [ ] **Step 1: Reuse the existing host fixture in controller tests**

Setup:

```csharp
private HostFixture _hostFixture = null!;
private UIScreenHost _screenHost = null!;
private Node _uiParent = null!;

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

Cleanup uses `UIScreenHostTestSupport.DisposeFixture(_hostFixture)` and frees `_uiParent`.

Hosted screen lookup:

```csharp
var modalLayer = _screenHost.GetNode<Control>("ModalLayer");
var dialogue = modalLayer.GetChildren()
    .OfType<DialogueScreenController>()
    .Single();
```

Add/update:

```text
Begin_HostsOneDialogueEntry
DialogueCancel_ClosesHostedEntryAndCompletesOnce
ShopOutcome_ClosesHostedDialogueBeforeNativeShopOpens
HealOutcome_ClosesHostedDialogueBeforeNativeHealOpens
Finish_WhileDialogueActive_ClosesHostedEntryAndCompletesOnce
MissingTree_CreatesNoHostedEntryAndCompletesOnce
HostRejectsDialogue_CleansCandidateAndCompletesOnce
```

- [ ] **Step 2: Run RED for constructor/cutover tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameTest"
```

Expected: FAIL until the constructor/caller cutover is implemented.

- [ ] **Step 3: Add the host dependency without changing Shop/Heal**

Controller fields:

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

Keep `_shopDialog`, `_healDialog`, `OpenShop()`, and `OpenHeal()` native.

- [ ] **Step 4: Move the production host guard into this same compile slice**

In `Game.OnNpcInteracted(...)`, after resolving `npcData` and **before** `StartNpcInteraction()`:

```csharp
if (_screenHost == null || !GodotObject.IsInstanceValid(_screenHost))
{
    GD.PushError("[Game] Cannot start NPC interaction without UIScreenHost.");
    return;
}

_gameManager.StartNpcInteraction();
UpdateInteractionPrompt();

_npcInteractionController = new NpcInteractionController(
    _gameManager,
    _screenHost,
    GetNode("UI"),
    npcData,
    _gameManager.Player,
    _questFlags);
```

Do not use `_screenHost!` and do not set `IsInNpcInteraction` before the guard.

- [ ] **Step 5: Replace native `Begin()` with configure → wire → present**

After tree resolution:

```csharp
var packed = GD.Load<PackedScene>("res://scenes/ui/DialogueScreen.tscn");
if (packed == null)
{
    GD.PushError("[NpcInteractionController] DialogueScreen.tscn not found.");
    Finish();
    return;
}

var screen = packed.Instantiate<DialogueScreenController>();
if (!screen.TryStartDialogue(_npc, tree, _player, _questFlags))
{
    GD.PushError($"[NpcInteractionController] Dialogue tree '{tree.TreeId}' has no usable root or screen was already started.");
    screen.QueueFree();
    Finish();
    return;
}

screen.DialogueOutcome += OnDialogueOutcome;
screen.DialogueClosed += OnDialogueClosed;
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

if (result.Status != UIScreenOpenStatus.Opened || !result.Handle.HasValue)
{
    screen.DialogueOutcome -= OnDialogueOutcome;
    screen.DialogueClosed -= OnDialogueClosed;
    screen.QueueFree();
    Finish();
    return;
}

_dialogueScreen = screen;
_dialogueHandle = result.Handle.Value;
```

Do not pre-parent the screen under `_uiParent`.

- [ ] **Step 6: Close Dialogue through the host before routing terminal outcomes**

Use one cleanup helper:

```csharp
private void ClearDialoguePresentation(DialogueScreenController screen)
{
    if (GodotObject.IsInstanceValid(screen))
    {
        screen.DialogueOutcome -= OnDialogueOutcome;
        screen.DialogueClosed -= OnDialogueClosed;
    }

    if (ReferenceEquals(_dialogueScreen, screen))
    {
        _dialogueScreen = null;
        _dialogueHandle = null;
    }
}
```

Close helper:

```csharp
private void CloseDialoguePresentation(UIScreenCloseReason reason)
{
    if (_dialogueScreen == null || !_dialogueHandle.HasValue)
        return;

    var screen = _dialogueScreen;
    var result = _screenHost.TryClose(_dialogueHandle.Value, reason);
    if (result.Status == UIScreenCloseStatus.StaleHandle)
        ClearDialoguePresentation(screen);
}
```

`OnDialogueOutcome(...)` captures the enum, closes Dialogue, then routes to existing `OpenShop()`, `OpenHeal()`, or `Finish()`.

`OnDialogueClosed()` closes Dialogue then calls `Finish()`.

`Finish()` remains `_finished`-guarded, closes active Dialogue, cleans legacy Shop/Heal, then invokes `InteractionComplete` once.

- [ ] **Step 7: Pin host rejection without adding injection seams**

Before `controller.Begin()`, present a fixture `Control` using `UIScreenKinds.Dialogue`. Then call `Begin()` and assert:

```csharp
AssertThat(completed).IsEqual(1);
AssertThat(_screenHost.ActiveEntries.Count(e => e.Policy.Kind == UIScreenKinds.Dialogue))
    .IsEqual(1); // only the pre-existing fixture entry
AssertThat(_screenHost.GetNode<Control>("ModalLayer").GetChildren()
    .OfType<DialogueScreenController>().Any()).IsFalse();
```

No host interface or injectable scene loader is added.

- [ ] **Step 8: Run cutover gate before deleting native Dialogue**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~DialogueDialogTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameTest"
dotnet build Sirius.sln --no-restore --nologo
```

Expected: all pass; build has 0 errors.

- [ ] **Step 9: Delete the retired native implementation**

Delete:

```text
scripts/ui/DialogueDialog.cs
tests/ui/DialogueDialogTest.cs
```

Audit:

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests
```

Expected: zero matches.

`PopupCentered()` may remain in native Shop/Heal or other later-ticket surfaces.

- [ ] **Step 10: Commit**

```bash
git add scripts/ui/NpcInteractionController.cs \
        scripts/game/Game.cs \
        tests/ui/NpcInteractionControllerTest.cs \
        scripts/ui/DialogueDialog.cs \
        tests/ui/DialogueDialogTest.cs
git commit -m "feat(ui): host NPC dialogue through UIScreenHost"
```

---

## Task 3: Pin Real Gameplay Route, Cancel, and Domain Teardown

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify only if a focused failure requires it: `scripts/ui/NpcInteractionController.cs`

**Interfaces:**
- Consumes: hosted Dialogue from Task 2.
- Produces: one root-owned `EndNpcInteractionIfActive()` cleanup seam and production-route regressions.

- [ ] **Step 1: Add the real authored-NPC route helper**

In `GameplayPauseHostTest`:

```csharp
private static Vector2I FindNpcInternalPosition(Game game, string npcId)
{
    var grid = game.GetNode<FloorManager>("FloorManager").CurrentGridMap;
    var floorRoot = grid.GetParent();
    var spawn = game.GetTree().GetNodesInGroup("NpcSpawn")
        .OfType<NpcSpawn>()
        .Single(node => node.NpcId == npcId && node.BelongsToFloor(floorRoot));
    var origin = GetPrivateField<Vector2I>(grid, "_tilemapOrigin");
    var internalPosition = spawn.GridPosition - origin;

    AssertThat(grid.InternalGridToTilemapCoords(internalPosition))
        .IsEqual(spawn.GridPosition);
    return internalPosition;
}
```

Use the authored `village_shopkeeper` or `village_healer`; do not create a fake Dialogue host entry.

- [ ] **Step 2: Write RED production host-policy test**

```csharp
var host = _game!.GetNode<UIScreenHost>("UI/UIScreenHost");
var gameUi = _game.GetNode<Control>("UI/GameUI");
var gameManager = _game.GetNode<GameManager>("GameManager");
var internalPosition = FindNpcInternalPosition(_game, "village_shopkeeper");

InvokePrivateVoid(_game, "OnNpcInteracted", internalPosition);
await AwaitFrames(2);

var entry = host.ActiveEntries.Single(e => e.Policy.Kind == UIScreenKinds.Dialogue);
AssertThat(gameManager.IsInNpcInteraction).IsTrue();
AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
AssertThat(entry.Policy.BlockGameplayInput).IsTrue();
AssertThat(entry.Policy.PauseTree).IsFalse();
AssertThat(entry.Policy.Hud).IsEqual(UIHudPolicy.Visible);
AssertThat(entry.Policy.Cursor).IsEqual(UICursorPolicy.Visible);
AssertThat(entry.Policy.Cancel).IsEqual(UICancelPolicy.Consume);
AssertThat(gameUi.Visible).IsTrue();
```

- [ ] **Step 3: Add guarded domain-end helper**

In `Game.cs`:

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

Use it in:

1. `OnNpcInteractionComplete()` instead of unconditional `EndNpcInteraction()`;
2. the `Begin()` exception catch;
3. `OnNpcInteractionResetRequested()` when no controller remains;
4. `_ExitTree()` after unsubscribing and finishing/clearing `_npcInteractionController`.

Do not move `GameManager` interaction ownership into the UI controller.

- [ ] **Step 4: Pin normal completion through the real route**

Open `village_shopkeeper`, get the hosted `DialogueScreenController` from `ModalLayer`, press `Goodbye.`, await frames, then assert:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
AssertThat(((SceneTree)Engine.GetMainLoop()).Paused).IsFalse();
AssertThat(host.CurrentState.IsPresentationGameplayBlocked).IsFalse();
AssertThat(gameUi.Visible).IsTrue();
```

Use the existing interaction-prompt helpers to assert an adjacent valid target is recomputed when the fixture supports it; do not add a public getter solely for this test.

- [ ] **Step 5: Pin actual tree-exit cleanup while the manager is still inspectable**

Open hosted Dialogue through `OnNpcInteracted(...)`, keep references to `GameManager` and `UIScreenHost`, then remove the Game from its viewport instead of freeing it immediately:

```csharp
AssertThat(gameManager.IsInNpcInteraction).IsTrue();
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsTrue();

_viewport!.RemoveChild(_game!); // executes real _ExitTree() on the Game subtree

AssertThat(gameManager.IsInNpcInteraction).IsFalse();
AssertThat(host.ActiveEntries.Count).IsEqual(0);
```

The node objects remain valid after `RemoveChild`, so the test can inspect the domain flag after the real exit callback. Test teardown can then free the detached `_game` normally.

- [ ] **Step 6: Keep the existing flag-only native-child Cancel regression**

Do **not** delete or rewrite `ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler` in `GameInputLifecycleTest`. It still documents the later Shop/Heal phase: `IsInNpcInteraction == true`, no hosted Dialogue entry, root Pause fallback declines so native dialog handling can receive Cancel.

- [ ] **Step 7: Add real hosted keyboard Cancel regression**

In `GameInputLifecycleTest`, instantiate real `Game.tscn`, use the same local `FindNpcInternalPosition(...)` pattern, invoke `OnNpcInteracted(...)`, assert Dialogue is active, then:

```csharp
ConfigureCancelBindings(Key.P);
PushPhysicalKeyDown(Key.P);
await AwaitFrames(2);

try
{
    AssertThat(_viewport!.IsInputHandled()).IsTrue();
    AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
    AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
    AssertThat(gameManager.IsInNpcInteraction).IsFalse();
}
finally
{
    ReleasePhysicalKey(Key.P);
}
```

- [ ] **Step 8: Add real hosted controller Cancel regression**

Bind a controller button using the existing helper pattern, open real Dialogue, then call `PushPhysicalJoypadButtonPressAndRelease(button)` and assert the same terminal state:

```csharp
AssertThat(host.IsKindActive(UIScreenKinds.Dialogue)).IsFalse();
AssertThat(host.IsKindActive(UIScreenKinds.Pause)).IsFalse();
AssertThat(gameManager.IsInNpcInteraction).IsFalse();
```

No `ui_close_dialog` synchronization is added for the hosted Dialogue path.

- [ ] **Step 9: Run gameplay lifecycle gate**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: all pass, including the 640×360 Dialogue layout test and real authored-NPC route.

- [ ] **Step 10: Commit**

```bash
git add scripts/game/Game.cs \
        tests/game/GameplayPauseHostTest.cs \
        tests/game/GameInputLifecycleTest.cs
git commit -m "test(ui): pin hosted dialogue gameplay lifecycle"
```

---

## Task 4: Reconcile Lifecycle Docs and Run Final Verification

**Files:**
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`
- Audit: all Task 0–3 files

**Interfaces:**
- Produces: accurate lifecycle documentation and final implementation evidence.

- [ ] **Step 1: Update only Dialogue/NPC rows in HPA-376**

Record:

- `DialogueScreenController : Control` under `UIScreenKinds.Dialogue`;
- `NpcInteractionController` owns the host handle and Dialogue → native Shop/Heal transition;
- `PauseTree = false`, `BlockGameplayInput = true`, `Hud = Visible`, `Cursor = Visible`, lower gameplay inert;
- configured Cancel routes through `RequestCancel()` and `ConsumeHere`;
- `SiriusModalSizeClass.Full` + Dialogue-owned `%SafeFrame` produce the wide-bottom surface;
- standard height is bounded to the lower 45% safe-frame band; compact may use full safe height with body scrolling;
- `EndNpcInteractionIfActive()` covers completion/failure/reset/teardown;
- HPA-625, not HPA-569, owns the missing HPA-373 portrait requirement;
- HPA-570 still owns Shop/Heal migration.

Do not rewrite unrelated lifecycle rows.

- [ ] **Step 2: Run stale/ownership audits**

```bash
rg -n "DialogueDialog|new DialogueDialog" scripts scenes tests
rg -n "choice\.Condition\.IsMet|UIInputInterception\.Consumed" scripts scenes tests docs/superpowers/specs/2026-08-15-hpa-569-hosted-dialogue-design.md docs/superpowers/plans/2026-08-15-hpa-569-hosted-dialogue.md
rg -n '^\[editable path="ModalShell"\]' scenes/ui/DialogueScreen.tscn
```

Expected: zero matches for all three commands.

Verify the intended symbols:

```bash
rg -n "SiriusModalSizeClass\.Full|UIScreenKinds\.Dialogue|DialogueScreenController|DialogueScreen\.tscn|EndNpcInteractionIfActive" \
  scripts scenes tests docs/ui/hpa-376
```

Expected: matches only in the intended shared-width, hosted Dialogue, lifecycle-test, and documentation paths.

- [ ] **Step 3: Run shared component and Dialogue-focused suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~SiriusUiContractsTest|FullyQualifiedName~SiriusModalShellTest|FullyQualifiedName~DialogueScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: 0 failures.

- [ ] **Step 4: Run neighboring interaction regressions**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~Dialogue|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~HealDialogTest|FullyQualifiedName~PauseScreenControllerTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~SiriusPromptTest"
```

Expected: 0 failures. Shop/Heal remain native and unchanged.

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

Record unchanged NuGet/orphan-node warning noise separately; do not treat pre-existing warnings as HPA-569 work.

- [ ] **Step 6: Scope audit**

```bash
git diff --name-only origin/main...HEAD
```

Allowed runtime scope:

```text
scripts/ui/theme/SiriusUiTypes.cs
scripts/ui/theme/SiriusUiMetrics.cs
scripts/ui/components/SiriusModalShell.cs
tests/ui/theme/SiriusUiContractsTest.cs
tests/ui/components/SiriusModalShellTest.cs
scenes/ui/DialogueScreen.tscn
scripts/ui/DialogueScreenController.cs
scripts/ui/NpcInteractionController.cs
scripts/game/Game.cs
tests/ui/DialogueScreenControllerTest.cs
tests/ui/NpcInteractionControllerTest.cs
tests/game/GameplayPauseHostTest.cs
tests/game/GameInputLifecycleTest.cs
docs/ui/hpa-376/ui-lifecycle-contract.md
```

Plus the HPA-569 design/plan documents already on the branch.

Reject unexpected changes to:

- `NpcData`, Dialogue model/catalog/conditions;
- portrait assets/data/rendering (HPA-625);
- Shop/Heal presentation;
- Puzzle/Reward presentation;
- `UIScreenHost` or `UIScreenKinds`;
- Theme tokens/art;
- shell placement APIs;
- dialogue history/typewriter/auto-advance/persistence/quest behavior.

- [ ] **Step 7: Commit final lifecycle documentation**

```bash
git add docs/ui/hpa-376/ui-lifecycle-contract.md
git commit -m "docs(ui): record hosted dialogue lifecycle"
```

## Final Review Checklist

Before marking HPA-569 implementation complete:

- [ ] `SiriusModalSizeClass.Full` is tested in both the closed enum/metric contract and `SiriusModalShell` behavior.
- [ ] Existing Small/Medium/Large shell behavior remains unchanged.
- [ ] Static Dialogue chrome is scene-authored under a Dialogue-owned `%SafeFrame`.
- [ ] Dialogue never writes shell-owned `%Panel` geometry and needs no `[editable]` instance path.
- [ ] Standard Dialogue cannot exceed the lower 45% safe-frame band; compact long content scrolls.
- [ ] HPA-625 is recorded as the owner of the deferred portrait requirement.
- [ ] `NpcInteractionController` remains the single NPC interaction orchestration owner.
- [ ] `TryStartDialogue(...)` is pre-ready safe and single-start; it never re-arms the terminal latch.
- [ ] Conditions use `Evaluate(...)`; no `IsMet` wrapper exists.
- [ ] Choice and `Farewell.` actions use the existing Sirius button theme.
- [ ] Configured Cancel and visible terminal actions share the screen latch.
- [ ] Dialogue does not pause the scene tree and keeps the HUD/world visible.
- [ ] `Game` validates the host before `StartNpcInteraction()`; no `_screenHost!` bridge remains.
- [ ] The real `OnNpcInteracted` path is covered through an authored `NpcSpawn`.
- [ ] The flag-only NPC Cancel regression remains for native Shop/Heal.
- [ ] Teardown clears `IsInNpcInteraction` after `InteractionComplete` is unsubscribed.
- [ ] Shop/Heal still work through their legacy dialogs after Dialogue outcomes.
- [ ] No native `DialogueDialog` references remain.
- [ ] Full suite/build/diff-check pass before implementation completion is claimed.