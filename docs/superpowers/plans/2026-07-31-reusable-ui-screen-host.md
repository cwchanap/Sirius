# Reusable UI Screen Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable, scene-local `UIScreenHost` for HPA-378 with pure stack/policy logic, Godot Control and embedded-Window adapters, deterministic Cancel routing, exact state restoration, per-viewport focus lifecycle, diagnostics, and synthetic contract coverage.

**Architecture:** `UIScreenStackModel` and `UIScreenPolicyResolver` hold immutable value state only. `UIScreenHost` owns `ProcessMode.Always`, view adapters, input dispatch, state leases, lower-layer effects, focus restoration, mutation ordering, and teardown. HPA-378 does not migrate MainMenu/Game/floor production flows; HPA-379 owns that integration and its GridMap/HUD/subwindow prerequisites.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, `Sirius.sln`, `test.runsettings.local`.

## Global Constraints

- Do not modify `scripts/game/Game.cs`, `scripts/ui/MainMenu.cs`, floor scenes, `project.godot`, or existing production screen controllers.
- Do not add an autoload, global UI singleton, implicit replacement, cross-scene navigation history, or detached native-window support.
- `UIScreenHost` is `ProcessMode.Always`; `HUDLayer` is explicitly `Pausable`; presentation layers process while paused.
- `Window` and `AcceptDialog` registration requires embedded subwindows; otherwise return `UnsupportedSubwindowMode` without mutation.
- The pure model contains no `Node`, `Control`, `Window`, `Viewport`, delegate, or `Callable`.
- Concrete flow kinds are unique; categories use normalized exclusive groups.
- Passive entries cannot pause, block gameplay, own Cancel, declare entry actions, affect lower layers, or request focus.
- One physical input produces one logical Cancel traversal. `ui_close_dialog` remains GUI pass-through.
- Lower-layer effects compose with `Hidden > VisibleInert > VisibleInteractive`.
- Pause, process mode, cursor, HUD, Control/Window interactivity, and focus restore exact incoming values.
- Restoration leases are generation-tagged and must clear on every completion/invalidation path.
- No new dependencies.

## File Map

Create:

```text
scripts/ui/hosting/
├── UIScreenKinds.cs
├── UIScreenContracts.cs
├── UIScreenHandle.cs
├── UIScreenEntryPolicy.cs
├── UIScreenEntrySpec.cs
├── UIScreenStackModel.cs
├── UIScreenPolicyResolver.cs
├── UIScreenViewAdapter.cs
├── UIScreenInputDispatcher.cs
├── UIScreenFocusCoordinator.cs
└── UIScreenHost.cs

scenes/ui/UIScreenHost.tscn

tests/ui/hosting/
├── UIScreenHostTestSupport.cs
├── UIScreenStackModelTest.cs
├── UIScreenPolicyResolverTest.cs
├── UIScreenHostProcessModeTest.cs
├── UIScreenHostSubwindowTest.cs
├── UIScreenHostInputTest.cs
├── UIScreenHostFocusTest.cs
├── UIScreenHostLifecycleTest.cs
└── UIScreenHostContractScenarioTest.cs

docs/ui/hpa-378/uiscreenhost-contract.md
```

Modify only after full verification:

```text
docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md
```

---

### Task 1: Define Contracts, Normalization, and Test Support

**Files:**
- Create: `scripts/ui/hosting/UIScreenKinds.cs`
- Create: `scripts/ui/hosting/UIScreenContracts.cs`
- Create: `scripts/ui/hosting/UIScreenHandle.cs`
- Create: `scripts/ui/hosting/UIScreenEntryPolicy.cs`
- Create: `scripts/ui/hosting/UIScreenEntrySpec.cs`
- Create: `tests/ui/hosting/UIScreenHostTestSupport.cs`
- Create: `tests/ui/hosting/UIScreenStackModelTest.cs`

**Interfaces:**
- Produces `UIScreenHandle`, `UIScreenEntrySpec.Normalize()`, `UIScreenEntryPolicy`, all enums/status records, constants, and shared test helpers.
- `UIScreenHostTestSupport` produces:
  - `Task<HostFixture> CreateHost(Node owner, IEnumerable<StringName>? coreActions = null)`
  - `UIScreenEntrySpec Spec(StringName kind)`
  - `UIScreenEntryPolicy Policy(StringName kind)`
  - `UIScreenEntrySnapshot Snapshot(UIScreenEntryPolicy policy, long sequence, long token = 0)`
  - `IReadOnlyList<UIScreenEntrySnapshot> Snapshots(params UIScreenEntryPolicy[] policies)`
  - `InputEventAction ActionPress(StringName action)`
  - `InputEventKey EscapeBoundTo(HostFixture fixture, params StringName[] actions)`
- `HostFixture : IDisposable` restores temporary `InputMap` actions, pause, mouse mode, HUD visibility, subwindow embedding, and queues host/view cleanup.

- [ ] **Step 1: Write failing normalization tests**

```csharp
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
public partial class UIScreenStackModelTest
{
    [TestCase]
    public void Normalize_DefaultGroup_BecomesEmptyGroup()
    {
        var normalized = UIScreenHostTestSupport.Spec(UIScreenKinds.Pause)
            with { ExclusiveGroup = default };

        var result = normalized.Normalize();

        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(result.Policy!.ExclusiveGroup).IsEqual(UIScreenExclusiveGroups.None);
    }

    [TestCase]
    public void Normalize_PassiveBlockingPolicy_IsRejected()
    {
        var spec = UIScreenHostTestSupport.Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            BlockGameplayInput = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }
}
```

- [ ] **Step 2: Run and confirm compile failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest"
```

Expected: FAIL because contract types do not exist.

- [ ] **Step 3: Implement canonical constants**

`UIScreenKinds.cs` must define:

```csharp
using Godot;

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

public static class UIScreenExclusiveGroups
{
    public static readonly StringName None = "";
    public static readonly StringName BlockingPrompt = "blocking_prompt";
}
```

- [ ] **Step 4: Implement public enums and stable statuses**

`UIScreenContracts.cs` defines these exact enums:

```csharp
public enum UIScreenLayer { Hud, Screen, Modal, Toast, Transition }
public enum UIInputPriority { Passive, Screen, Modal, Blocking }
public enum UIProcessPolicy { PreserveAndValidate, InheritHost, Pausable, WhenPaused, Always }
public enum UICursorPolicy { Inherit, Visible, Hidden }
public enum UIHudPolicy { Inherit, Visible, Hidden }
public enum UILowerLayerPolicy { VisibleInteractive, VisibleInert, Hidden }
public enum UICancelPolicy { None, Close, Consume, PassThrough }
public enum UIInputInterception { DeferToPolicy, ConsumeHere, ReserveForNativeHandler }
public enum UIInputDispatchResult { NoOwner, Consumed, ReservedForTopEntry }
public enum UIRootCancelResult { Declined, Consumed }
public enum UINodeLifetime { External, Hide, QueueFree }
public enum UIScreenCloseReason { Cancel, ExplicitAction, Programmatic, NodeFreed, ParentClosed, HostTeardown }
```

Open statuses:

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
    MalformedHost
}
```

Close statuses:

```csharp
public enum UIScreenCloseStatus { Closed, AlreadyClosed, StaleHandle, HostTearingDown }
```

Also define `UIScreenOpenResult`, `UIScreenCloseResult`, `UIScreenEffectiveState`, `UIInputContext`, `UIRootCancelContext`, `UIScreenHostOptions`, and an immutable `UIScreenHostDiagnostics` record.

- [ ] **Step 5: Implement handle, spec, policy, and normalization**

```csharp
public readonly record struct UIScreenHandle(long Token, StringName Kind);

internal readonly record struct UIScreenSpecNormalizationResult(
    UIScreenOpenStatus Status,
    UIScreenEntryPolicy? Policy);
```

`UIScreenEntryPolicy` contains normalized value fields only. `UIScreenEntrySpec` adds Godot delegates (`InitialFocus`, `RestoreFocus`, `InterceptCancel`, `IsPresented`, `SetPresented`, `SetInteractive`, `FocusViewport`, `Cleanup`).

Normalize group text without relying on nullable `StringName` representation:

```csharp
private static StringName NormalizeGroup(StringName? value)
{
    var text = value?.ToString();
    return string.IsNullOrEmpty(text)
        ? UIScreenExclusiveGroups.None
        : new StringName(text);
}
```

Normalization rules:

1. reject empty kind;
2. normalize null/default collections to empty read-only sets;
3. normalize group;
4. reject invalid Passive combinations;
5. project value fields only;
6. return `Opened` plus policy.

- [ ] **Step 6: Implement shared test helpers**

`Policy` returns a valid Screen policy with empty sets. `Spec` returns the matching adapter spec. `Snapshot` uses the provided token or sequence as a deterministic test token. `Snapshots` assigns tokens/sequences from 1 upward. `EscapeBoundTo` records and restores prior bindings through `HostFixture`.

- [ ] **Step 7: Run tests and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest"

git add scripts/ui/hosting/UIScreenKinds.cs \
  scripts/ui/hosting/UIScreenContracts.cs \
  scripts/ui/hosting/UIScreenHandle.cs \
  scripts/ui/hosting/UIScreenEntryPolicy.cs \
  scripts/ui/hosting/UIScreenEntrySpec.cs \
  tests/ui/hosting/UIScreenHostTestSupport.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs
git commit -m "feat: define UIScreenHost contracts"
```

Expected: tests pass.

---

### Task 2: Implement the Pure Stack Model

**Files:**
- Create: `scripts/ui/hosting/UIScreenStackModel.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`

**Interfaces:**
- `UIScreenOpenResult Open(UIScreenEntryPolicy policy)`
- `UIScreenStackCloseMutation Close(UIScreenHandle handle)`
- `IReadOnlyList<UIScreenEntrySnapshot> Entries`
- `IReadOnlyList<UIScreenEntrySnapshot> InputOrder`

- [ ] **Step 1: Add failing model tests**

```csharp
[TestCase]
public void Open_DuplicateKind_IsRejectedWithoutMutation()
{
    var model = new UIScreenStackModel();
    var first = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));
    var second = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause));

    AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
    AssertThat(second.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
    AssertThat(model.Entries.Count).IsEqual(1);
}

[TestCase]
public void InputOrder_ChildPrecedesParent()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
    var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with
    {
        Parent = pause
    }).Handle!.Value;

    AssertThat(model.InputOrder[0].Handle).IsEqual(settings);
    AssertThat(model.InputOrder[1].Handle).IsEqual(pause);
}
```

Add tests for symmetric incompatibility, non-empty group conflicts, empty groups, invalid parent, and different flow-specific confirmation kinds sharing `BlockingPrompt`.

- [ ] **Step 2: Run and confirm failure**

Use the Task 1 filter. Expected: FAIL because model is missing.

- [ ] **Step 3: Implement model records and open rules**

```csharp
public sealed record UIScreenEntrySnapshot(
    UIScreenHandle Handle,
    UIScreenEntryPolicy Policy,
    long Sequence);

internal sealed record UIScreenStackCloseMutation(
    UIScreenCloseStatus Status,
    IReadOnlyList<UIScreenEntrySnapshot> ClosedEntries);
```

Open validation order:

1. duplicate concrete kind;
2. active parent;
3. symmetric incompatibility;
4. equal non-empty exclusive group unless explicitly allowed parent-child relation;
5. allocate monotonic token/sequence;
6. append exactly once.

Input order:

1. descendants before ancestors;
2. Blocking → Modal → Screen → Passive;
3. newest sequence first.

- [ ] **Step 4: Add and implement close tests**

```csharp
[TestCase]
public void Close_Parent_ClosesDescendantsTopmostFirst()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Pause)).Handle!.Value;
    var settings = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;
    var confirm = model.Open(UIScreenHostTestSupport.Policy(UIScreenKinds.ConfirmQuitToMain) with { Parent = settings }).Handle!.Value;

    var result = model.Close(pause);

    AssertThat(result.ClosedEntries.Select(e => e.Handle).ToArray())
        .ContainsExactly(confirm, settings, pause);
}
```

`Close` returns `AlreadyClosed` for known closed tokens and `StaleHandle` for unknown tokens. Remove descendants deepest/newest first and remember closed tokens.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest"

git add scripts/ui/hosting/UIScreenStackModel.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs
git commit -m "feat: add UIScreenHost stack model"
```

---

### Task 3: Implement Pure Policy Resolution

**Files:**
- Create: `scripts/ui/hosting/UIScreenPolicyResolver.cs`
- Create: `tests/ui/hosting/UIScreenPolicyResolverTest.cs`

**Interfaces:**
- `UIScreenResolvedPolicy Resolve(IReadOnlyList<UIScreenEntrySnapshot> entries)`

- [ ] **Step 1: Write failing reduction tests**

```csharp
[TestCase]
public void Resolve_PauseAndBlock_AreOrReduced()
{
    var result = UIScreenPolicyResolver.Resolve(
        UIScreenHostTestSupport.Snapshots(
            UIScreenHostTestSupport.Policy(UIScreenKinds.Pause) with
            {
                PauseTree = true,
                BlockGameplayInput = true
            },
            UIScreenHostTestSupport.Policy(UIScreenKinds.RewardToast) with
            {
                InputPriority = UIInputPriority.Passive
            }));

    AssertThat(result.PauseTree).IsTrue();
    AssertThat(result.BlockGameplayInput).IsTrue();
}
```

Add cursor/HUD highest-explicit tests and compositional lower-layer tests proving Pause contribution survives Settings/Inventory child opens.

- [ ] **Step 2: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenPolicyResolverTest"
```

- [ ] **Step 3: Implement resolved policy**

```csharp
public sealed record UIScreenResolvedPolicy(
    bool PauseTree,
    bool BlockGameplayInput,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> LowerLayerEffects);
```

Rules:

- pause and block are OR reductions;
- cursor/HUD use first non-Inherit in logical input order;
- top input owner is first non-Passive entry;
- each target receives every applicable owner contribution;
- reduce effects as Hidden=2, VisibleInert=1, VisibleInteractive=0;
- return copied read-only collections.

- [ ] **Step 4: Run pure suites and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenPolicyResolverTest"

git add scripts/ui/hosting/UIScreenPolicyResolver.cs \
  tests/ui/hosting/UIScreenPolicyResolverTest.cs
git commit -m "feat: resolve UIScreenHost policy"
```

---

### Task 4: Build Host Scene, Registration, Process, and Subwindow Gate

**Files:**
- Create: `scenes/ui/UIScreenHost.tscn`
- Create: `scripts/ui/hosting/UIScreenViewAdapter.cs`
- Create: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostProcessModeTest.cs`
- Create: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`

**Interfaces:**
- `UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec)`
- `UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason)`
- `bool IsActive(UIScreenHandle handle)`
- `bool IsKindActive(StringName kind)`
- `IReadOnlyList<UIScreenEntrySnapshot> ActiveEntries`

- [ ] **Step 1: Write failing scene/process test**

```csharp
[TestCase]
public async Task Scene_HasRequiredProcessModesAndVisibleSink()
{
    var scene = GD.Load<PackedScene>("res://scenes/ui/UIScreenHost.tscn");
    var host = scene.Instantiate<UIScreenHost>();
    AddChild(host);
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    AssertThat(host.ProcessMode).IsEqual(Node.ProcessModeEnum.Always);
    AssertThat(host.GetNode<Control>("HUDLayer").ProcessMode).IsEqual(Node.ProcessModeEnum.Pausable);
    AssertThat(host.GetNode<Control>("ModalLayer").ProcessMode).IsEqual(Node.ProcessModeEnum.Always);

    var sink = host.GetNode<Control>("FocusSink");
    AssertThat(sink.Visible).IsTrue();
    AssertThat(sink.FocusMode).IsEqual(Control.FocusModeEnum.All);
    AssertThat(sink.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
}
```

- [ ] **Step 2: Write failing embedded-subwindow tests**

When `GuiEmbedSubwindows=false`, presenting `AcceptDialog` returns `UnsupportedSubwindowMode` and `ActiveEntries` stays empty. When true, registration succeeds and the Window is its own focus viewport.

- [ ] **Step 3: Run and confirm failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostProcessModeTest|FullyQualifiedName~UIScreenHostSubwindowTest"
```

- [ ] **Step 4: Create scene structure**

```text
UIScreenHost (Control, Always, full rect)
├── HUDLayer (Control, Pausable, full rect)
├── ScreenLayer (Control, Always, full rect)
├── ModalLayer (Control, Always, full rect)
├── ToastLayer (Control, Always, full rect)
├── TransitionLayer (Control, Always, full rect)
├── InputShield (Control, hidden, full rect, MouseFilter=Stop)
└── FocusSink (Control, visible, transparent, 1x1, MouseFilter=Ignore, FocusMode=All)
```

- [ ] **Step 5: Implement adapter validation**

Rules:

1. unparented Control attaches to declared layer;
2. Control already under exact layer is accepted;
3. Control parented elsewhere returns `InvalidControlParentage`;
4. embedded Window requires embedding and parents beneath host;
5. disabled embedding returns `UnsupportedSubwindowMode`;
6. process policy snapshots/restores exact mode;
7. an unusable policy returns `InvalidProcessPolicy` before model mutation.

- [ ] **Step 6: Implement atomic registration and close skeleton**

`TryPresent` sequence: validate host/node → normalize → build adapter → model open → attach/apply process → store adapter → subscribe `TreeExiting` → recompute. Roll back model and adapter snapshot if post-open setup fails.

`TryClose` obtains model close cascade and cleans each returned snapshot in order, but later tasks fill state/focus/lifecycle details.

- [ ] **Step 7: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostProcessModeTest|FullyQualifiedName~UIScreenHostSubwindowTest"

git add scenes/ui/UIScreenHost.tscn \
  scripts/ui/hosting/UIScreenViewAdapter.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostProcessModeTest.cs \
  tests/ui/hosting/UIScreenHostSubwindowTest.cs
git commit -m "feat: add UIScreenHost scene and adapters"
```

---

### Task 5: Implement Cancel Dispatch and Effective State Leases

**Files:**
- Create: `scripts/ui/hosting/UIScreenInputDispatcher.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostInputTest.cs`
- Create: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- `UIInputDispatchResult TryHandleInput(InputEvent inputEvent)`
- `UIScreenEffectiveState CurrentState`
- `event Action<UIScreenEffectiveState> EffectiveStateChanged`

- [ ] **Step 1: Write failing Cancel tests**

Test:

- `pause_menu` and `ui_cancel` co-match but interceptor runs once;
- active Inventory closes on entry-scoped `toggle_inventory`;
- Settings plus `toggle_inventory` returns `NoOwner`;
- entry actions never invoke root fallback;
- dynamic interception precedes static policy;
- static None continues to parent;
- pass-through returns `ReservedForTopEntry`;
- root fallback runs only for unmatched core ownership;
- `ui_close_dialog` is not a core action.

- [ ] **Step 2: Implement dispatch algorithm**

For each event: match core once → prune invalid → consume live restoration barrier → traverse input order → match entry actions → dynamic interception → static policy → root fallback only for core → `NoOwner`.

Static mapping:

```text
None        -> continue
Close       -> TryClose(Cancel), Consumed
Consume     -> Consumed
PassThrough -> ReservedForTopEntry
```

Host callback:

```csharp
public override void _Input(InputEvent inputEvent)
{
    if (_tearingDown)
        return;

    if (TryHandleInput(inputEvent) == UIInputDispatchResult.Consumed)
        GetViewport().SetInputAsHandled();
}
```

- [ ] **Step 3: Write failing exact-state tests**

Test incoming true/false pause restoration, nested non-pausing child, exact cursor restore, exact HUD restore, null-HUD rejection, block callback transition count, and pause drift reassertion.

- [ ] **Step 4: Implement state leases**

```csharp
private sealed record PauseLease(bool IncomingPaused);
private sealed record CursorLease(Input.MouseModeEnum IncomingMode);
private sealed record HudLease(bool IncomingVisible);
```

Capture on first owner, retain baseline while owners remain, restore on last owner/teardown. While pause lease active, detect false pause, increment drift count, log, and reassert true without replacing baseline.

Publish effective state only after complete mutations.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostInputTest|FullyQualifiedName~UIScreenHostLifecycleTest"

git add scripts/ui/hosting/UIScreenInputDispatcher.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostInputTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: own UIScreenHost input and state"
```

---

### Task 6: Apply Compositional Control and Window Effects

**Files:**
- Modify: `scripts/ui/hosting/UIScreenViewAdapter.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Modify: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Consumes `UIScreenResolvedPolicy.LowerLayerEffects`.
- Produces exact effect baselines and weakening/restoration transitions.

- [ ] **Step 1: Write failing nested-effect tests**

Prove Pause makes gameplay inert, Settings hides Pause, Pause contribution remains while Settings is open, closing Settings restores Pause only, and closing Pause restores gameplay.

- [ ] **Step 2: Write embedded-Window baseline tests**

Start with non-default `GuiDisableInput`/`Unfocusable`, apply Hidden/Inert from different owners, remove strongest then final owner, and assert exact baseline. Verify custom `SetPresented(true)` is used when supplied.

- [ ] **Step 3: Implement effect baseline records**

```csharp
internal sealed record UIControlEffectBaseline(bool Visible, bool ProcessInputEnabled);
internal sealed record UIWindowEffectBaseline(bool Visible, bool GuiDisableInput, bool Unfocusable);
```

Capture once while any contribution exists. Transition hidden↔inert without replacing baseline. Restore and clear only when reduction becomes interactive.

- [ ] **Step 4: Implement mechanisms**

Control Hidden changes visibility; Control Inert uses shield plus `SetInteractive(false)` before publish. Window Hidden uses presentation adapter; Window Inert sets `GuiDisableInput=true` and `Unfocusable=true`. Reject owner open if required adapter is unavailable.

- [ ] **Step 5: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostSubwindowTest|FullyQualifiedName~UIScreenHostLifecycleTest"

git add scripts/ui/hosting/UIScreenViewAdapter.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostSubwindowTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: apply UIScreenHost layer effects"
```

---

### Task 7: Implement Focus, Restoration Leases, and Lifecycle Hardening

**Files:**
- Create: `scripts/ui/hosting/UIScreenFocusCoordinator.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostFocusTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Produces per-viewport focus records, visible sinks, generation-tagged lease, mutation queue, pruning, teardown, and diagnostics.

- [ ] **Step 1: Write failing focus/sink tests**

A blocking Control with no focusable child uses root sink. A blocking Window creates an equivalent visible, transparent/non-drawing, layout-neutral, `MouseFilter.Ignore`, `FocusMode.All` sink in its viewport.

- [ ] **Step 2: Write focus-order and lease tests**

Restoration order: explicit target → captured parent focus → parent initial target → first descendant → correct sink → release. Test valid target, freed target, teardown, re-entrant supersession, stale callback, duplicate close, and next Cancel after invalidation.

- [ ] **Step 3: Implement records**

```csharp
internal sealed record UIFocusRecord(
    Viewport Viewport,
    Control? FocusOwner,
    UIScreenHandle ParentHandle);

internal sealed record UIFocusRestorationLease(
    long Generation,
    UIScreenHandle ClosedHandle);
```

Initial focus is deferred without a barrier and token-checked. Restoration uses generation and `try/finally`:

```csharp
private void CompleteRestoration(long generation, UIScreenHandle closedHandle)
{
    try
    {
        if (_activeLease?.Generation != generation)
            return;

        RestoreBestAvailableTarget(closedHandle);
    }
    finally
    {
        if (_activeLease?.Generation == generation)
            _activeLease = null;
    }
}
```

- [ ] **Step 4: Implement mutation/lifecycle rules**

Use one guarded queue. Mark handles closing before callbacks. Deduplicate closes. On `TreeExiting`, close descendants, prune model, run cleanup once, recompute, and complete restoration. On `_ExitTree`, disable input, reject opens, close topmost-first, complete lease, restore snapshots, unsubscribe, and remove sinks.

- [ ] **Step 5: Implement immutable diagnostics**

Expose copied read-only snapshots for active order, normalized policy, effective state, lower effects, action ownership, focus/sink/lease, process/subwindow state, state leases, and pause drift count.

- [ ] **Step 6: Run and commit**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostFocusTest|FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostInputTest"

git add scripts/ui/hosting/UIScreenFocusCoordinator.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostFocusTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: harden UIScreenHost lifecycle"
```

---

### Task 8: Prove Contract Scenarios, Document, and Fully Verify

**Files:**
- Create: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Create: `docs/ui/hpa-378/uiscreenhost-contract.md`
- Modify after verification: `docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md`

**Interfaces:**
- Consumes complete host API.
- Produces HPA-376/HPA-378 acceptance evidence and HPA-379 contract.

- [ ] **Step 1: Implement complete synthetic scenario tests**

| Test | Required assertions |
|---|---|
| `InventoryChildOfPause_PausesWorldHidesHudAndReturnsToPause` | paused; HUD hidden; Pause retained; child close returns to Pause; parent close restores |
| `SettingsChildOfPause_PreservesPauseGameplayInertContribution` | Settings hides Pause; gameplay remains blocked by Pause; close restores Pause only |
| `DestructiveConfirmation_CancelReturnsWithoutDestructiveCallback` | safe focus; child closes; destructive count zero |
| `RewardToast_IsPassiveAndNeverBecomesInputOwner` | modal remains owner; toast changes no policy |
| `RequiredAcknowledgement_ConsumesCancelUntilContinue` | Cancel consumed; explicit Continue closes |
| `BattlePresentation_RemainsTopmostAfterDomainFlagClears` | Battle still blocks/owns Cancel until view termination |
| `EitherPresentationOrDomainBlock_SuppressesComposedPredicate` | OR semantics true for either source, false only for neither |

Use synthetic Controls/embedded AcceptDialogs only.

- [ ] **Step 2: Run scenario tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostContractScenarioTest"
```

Expected: PASS. Production-flow defects are documented for HPA-379 rather than expanding scope.

- [ ] **Step 3: Write `uiscreenhost-contract.md`**

Document node paths/process modes, public API/statuses, Control/Window defaults, embedding gate, process policy, Cancel surfaces/precedence, parent/group rules, lower-layer reduction, exact restoration, focus leases, teardown, diagnostics, and HPA-379 prerequisites. Include compilable synthetic registration examples for Pause, Inventory, Settings, embedded Save/Load, Battle lifetime, toast, and acknowledgement.

- [ ] **Step 4: Run focused, build, and full verification**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreen"
dotnet build Sirius.sln --no-restore
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero focused failures/skips, build exit 0, full suite exit 0, no new orphan-warning signature. Record fresh counts.

- [ ] **Step 5: Review scope and consistency**

Confirm no Game/MainMenu/floor/project diff, no production migration, no placeholder/empty implementation, signatures match docs, and every critical invariant has a named test.

- [ ] **Step 6: Mark design approved and commit final evidence**

Only after Step 4 succeeds, change the spec header to:

```markdown
**Status:** Approved design
```

Commit:

```bash
git add tests/ui/hosting/UIScreenHostContractScenarioTest.cs \
  docs/ui/hpa-378/uiscreenhost-contract.md \
  docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md
git commit -m "test: verify UIScreenHost contract"
```

- [ ] **Step 7: Update implementation PR evidence**

Report exact focused/full counts, build result, orphan comparison, HPA-378 scope confirmation, and HPA-379 prerequisites: embedded-subwindow pinning, runtime GridMap correction, Pausable HUD composition, and unified gameplay-block predicate.
