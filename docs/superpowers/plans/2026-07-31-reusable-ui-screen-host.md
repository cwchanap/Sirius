# Reusable UI Screen Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable, scene-local `UIScreenHost` for HPA-378 with pure stack/policy logic, Godot Control and embedded-Window adapters, deterministic Cancel routing, exact pause/process/cursor/HUD restoration, per-viewport focus restoration, lifecycle diagnostics, and synthetic contract coverage.

**Architecture:** Keep `UIScreenStackModel` and `UIScreenPolicyResolver` free of live Godot objects. `UIScreenHost` owns `ProcessMode.Always`, scene attachment, adapters, input dispatch, state leases, focus coordination, mutation ordering, and teardown. HPA-378 stops at the reusable host and synthetic tests; HPA-379 owns MainMenu/Game/floor integration, runtime GridMap correction, embedded-subwindow project configuration, and production-flow migration.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, `Sirius.sln`, `test.runsettings.local`.

## Global Constraints

- Do not modify `scripts/game/Game.cs`, `scripts/ui/MainMenu.cs`, floor scenes, `project.godot`, or existing production screen controllers in HPA-378.
- Do not introduce an autoload, global UI singleton, cross-scene navigation history, implicit replacement, or detached native-window support.
- `UIScreenHost` is scene-local and `ProcessMode.Always`; `HUDLayer` is explicitly `Pausable`; active presentation layers process while paused.
- Registered `Window` and `AcceptDialog` entries require embedded subwindows. Disabled embedding returns `UnsupportedSubwindowMode` without stack mutation.
- The pure model may contain `StringName` and immutable values, but no `Node`, `Control`, `Window`, `Viewport`, `Callable`, or delegates.
- Concrete flow kinds are unique. Confirmation/error categories use normalized exclusive groups.
- Passive entries cannot pause, block gameplay, own Cancel, declare entry-scoped actions, affect lower layers, or request focus.
- One physical event yields one logical Cancel traversal. `ui_close_dialog` remains embedded-GUI pass-through.
- Lower-layer effects compose from all active owners using `Hidden > VisibleInert > VisibleInteractive`.
- Pause, process mode, cursor, HUD visibility, Control visibility/interactivity, Window flags, and focus restore exact incoming values.
- A generation-tagged restoration lease clears on success, invalid target, re-entrant close, stale callback, and teardown.
- No new third-party dependencies.
- Every task follows red-green-refactor, runs focused verification, and ends with a commit.

## File Map

### Create

- `scripts/ui/hosting/UIScreenKinds.cs` — canonical kinds and exclusive groups.
- `scripts/ui/hosting/UIScreenContracts.cs` — public enums, options, contexts, results, effective state, and diagnostics records.
- `scripts/ui/hosting/UIScreenHandle.cs` — opaque instance identity.
- `scripts/ui/hosting/UIScreenEntryPolicy.cs` — normalized pure-model policy.
- `scripts/ui/hosting/UIScreenEntrySpec.cs` — Godot-facing registration specification and normalization.
- `scripts/ui/hosting/UIScreenStackModel.cs` — pure ownership, compatibility, ordering, and close cascades.
- `scripts/ui/hosting/UIScreenPolicyResolver.cs` — pure effective and lower-layer policy reduction.
- `scripts/ui/hosting/UIScreenViewAdapter.cs` — live Control/embedded-Window snapshots and operations.
- `scripts/ui/hosting/UIScreenInputDispatcher.cs` — action matching and Cancel precedence.
- `scripts/ui/hosting/UIScreenFocusCoordinator.cs` — focus records, sinks, and restoration leases.
- `scripts/ui/hosting/UIScreenHost.cs` — orchestration, registration, state leases, mutation queue, lifecycle, and diagnostics.
- `scenes/ui/UIScreenHost.tscn` — host scene, layers, shield, and root focus sink.
- `tests/ui/hosting/UIScreenHostTestSupport.cs` — synthetic views, action setup, and cleanup helpers.
- `tests/ui/hosting/UIScreenStackModelTest.cs`.
- `tests/ui/hosting/UIScreenPolicyResolverTest.cs`.
- `tests/ui/hosting/UIScreenHostProcessModeTest.cs`.
- `tests/ui/hosting/UIScreenHostSubwindowTest.cs`.
- `tests/ui/hosting/UIScreenHostInputTest.cs`.
- `tests/ui/hosting/UIScreenHostFocusTest.cs`.
- `tests/ui/hosting/UIScreenHostLifecycleTest.cs`.
- `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`.
- `docs/ui/hpa-378/uiscreenhost-contract.md`.

### Modify

- `docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md` — mark approved only after implementation verification succeeds.

---

### Task 1: Lock Public Contracts and Normalization

**Files:**
- Create: `scripts/ui/hosting/UIScreenKinds.cs`
- Create: `scripts/ui/hosting/UIScreenContracts.cs`
- Create: `scripts/ui/hosting/UIScreenHandle.cs`
- Create: `scripts/ui/hosting/UIScreenEntryPolicy.cs`
- Create: `scripts/ui/hosting/UIScreenEntrySpec.cs`
- Test: `tests/ui/hosting/UIScreenStackModelTest.cs`

**Interfaces:**
- Consumes: Godot `StringName`, `InputEvent`, `Control`, and `Viewport` only in adapter-facing contracts.
- Produces: `UIScreenHandle`, `UIScreenEntryPolicy`, `UIScreenEntrySpec.Normalize()`, public statuses, `UIScreenKinds`, and `UIScreenExclusiveGroups`.

- [ ] **Step 1: Write failing normalization tests**

Create `tests/ui/hosting/UIScreenStackModelTest.cs`:

```csharp
using GdUnit4;
using Godot;
using System.Collections.Generic;
using static GdUnit4.Assertions;

[TestSuite]
public partial class UIScreenStackModelTest
{
    [TestCase]
    public void Normalize_DefaultGroup_BecomesEmptyGroup()
    {
        var result = Spec(UIScreenKinds.Pause) with { ExclusiveGroup = default };
        var normalized = result.Normalize();

        AssertThat(normalized.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(normalized.Policy!.ExclusiveGroup).IsEqual(UIScreenExclusiveGroups.None);
    }

    [TestCase]
    public void Normalize_PassiveBlockingPolicy_IsRejected()
    {
        var spec = Spec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            BlockGameplayInput = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_Collections_AreNeverNull()
    {
        var normalized = Spec(UIScreenKinds.Settings).Normalize();

        AssertThat(normalized.Policy!.IncompatibleKinds).IsNotNull();
        AssertThat(normalized.Policy.EntryCancelActions).IsNotNull();
    }

    private static UIScreenEntrySpec Spec(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        IncompatibleKinds = new HashSet<StringName>(),
        PauseTree = false,
        BlockGameplayInput = true,
        Cursor = UICursorPolicy.Visible,
        Hud = UIHudPolicy.Inherit,
        LowerLayers = UILowerLayerPolicy.VisibleInert,
        Cancel = UICancelPolicy.Close,
        EntryCancelActions = new HashSet<StringName>()
    };
}
```

- [ ] **Step 2: Run the test and verify the compile failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest"
```

Expected: FAIL because the contracts do not exist.

- [ ] **Step 3: Add canonical kinds and exclusive groups**

Create `UIScreenKinds.cs`:

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

- [ ] **Step 4: Add stable public contract types**

Create `UIScreenContracts.cs` with these exact public names:

```csharp
using Godot;
using System;
using System.Collections.Generic;

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

public enum UIScreenCloseStatus { Closed, AlreadyClosed, StaleHandle, HostTearingDown }
public enum UIScreenCloseReason { Cancel, ExplicitAction, Programmatic, NodeFreed, ParentClosed, HostTeardown }

public readonly record struct UIScreenOpenResult(UIScreenOpenStatus Status, UIScreenHandle? Handle);
public readonly record struct UIScreenCloseResult(UIScreenCloseStatus Status);

public sealed record UIScreenEffectiveState(
    bool IsTreePauseOwned,
    bool IsPresentationGameplayBlocked,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    bool IsFocusRestorationPending);

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

public sealed record UIScreenHostOptions
{
    public Control? HudRoot { get; init; }
    public IReadOnlySet<StringName> CoreCancelActions { get; init; } = new HashSet<StringName>();
    public Func<UIRootCancelContext, UIRootCancelResult>? RootCancelFallback { get; init; }
    public Action<bool>? GameplayInputBlockChanged { get; init; }
}
```

- [ ] **Step 5: Add handle, normalized policy, and normalization result**

Create `UIScreenHandle.cs`:

```csharp
using Godot;

public readonly record struct UIScreenHandle(long Token, StringName Kind);
```

Create `UIScreenEntryPolicy.cs` as a record containing every value field from the design, with non-null `IReadOnlySet<StringName>` collections.

Create `UIScreenEntrySpec.cs` with adapter delegates and:

```csharp
internal readonly record struct UIScreenSpecNormalizationResult(
    UIScreenOpenStatus Status,
    UIScreenEntryPolicy? Policy);
```

Normalization must:

1. reject an empty `Kind`;
2. normalize `ExclusiveGroup` null/default/empty to `UIScreenExclusiveGroups.None`;
3. replace null collections with empty immutable sets;
4. reject Passive entries unless every Passive constraint is satisfied;
5. copy only value fields into `UIScreenEntryPolicy`;
6. return `Opened` plus policy on success.

Use this exact group normalization:

```csharp
private static StringName NormalizeGroup(StringName? value) =>
    value is null || value.Value.IsEmpty
        ? UIScreenExclusiveGroups.None
        : value.Value;
```

- [ ] **Step 6: Run normalization tests**

Run Step 2 again. Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/hosting/UIScreenKinds.cs \
  scripts/ui/hosting/UIScreenContracts.cs \
  scripts/ui/hosting/UIScreenHandle.cs \
  scripts/ui/hosting/UIScreenEntryPolicy.cs \
  scripts/ui/hosting/UIScreenEntrySpec.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs
git commit -m "feat: define UIScreenHost contracts"
```

---

### Task 2: Implement the Pure Stack Model

**Files:**
- Create: `scripts/ui/hosting/UIScreenStackModel.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`

**Interfaces:**
- Consumes: `UIScreenEntryPolicy`, `UIScreenHandle`.
- Produces:
  - `UIScreenOpenResult Open(UIScreenEntryPolicy policy)`
  - `UIScreenStackCloseMutation Close(UIScreenHandle handle)`
  - `IReadOnlyList<UIScreenEntrySnapshot> Entries`
  - `IReadOnlyList<UIScreenEntrySnapshot> InputOrder`

- [ ] **Step 1: Add failing open/ordering tests**

```csharp
[TestCase]
public void Open_DuplicateKind_IsRejectedWithoutMutation()
{
    var model = new UIScreenStackModel();
    var first = model.Open(Policy(UIScreenKinds.Pause));
    var second = model.Open(Policy(UIScreenKinds.Pause));

    AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
    AssertThat(second.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
    AssertThat(model.Entries.Count).IsEqual(1);
}

[TestCase]
public void Open_DifferentConfirmKinds_SameGroup_Conflict()
{
    var model = new UIScreenStackModel();
    model.Open(Policy(UIScreenKinds.ConfirmOverwrite) with
    {
        InputPriority = UIInputPriority.Blocking,
        ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt
    });

    var result = model.Open(Policy(UIScreenKinds.ConfirmQuitToMain) with
    {
        InputPriority = UIInputPriority.Blocking,
        ExclusiveGroup = UIScreenExclusiveGroups.BlockingPrompt
    });

    AssertThat(result.Status).IsEqual(UIScreenOpenStatus.ExclusiveGroupConflict);
}

[TestCase]
public void InputOrder_ChildPrecedesParent()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(Policy(UIScreenKinds.Pause)).Handle!.Value;
    var settings = model.Open(Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;

    AssertThat(model.InputOrder[0].Handle).IsEqual(settings);
    AssertThat(model.InputOrder[1].Handle).IsEqual(pause);
}
```

- [ ] **Step 2: Run and verify failure**

Run the Task 1 filter. Expected: FAIL because the model is absent.

- [ ] **Step 3: Implement entry storage and open validation**

Create these exact internal records:

```csharp
public sealed record UIScreenEntrySnapshot(
    UIScreenHandle Handle,
    UIScreenEntryPolicy Policy,
    long Sequence);

internal sealed record UIScreenStackCloseMutation(
    UIScreenCloseStatus Status,
    IReadOnlyList<UIScreenEntrySnapshot> ClosedEntries);
```

Implement `Open` in this order:

1. reject an active duplicate concrete kind;
2. reject an invalid/inactive parent;
3. reject symmetric incompatibility if either entry names the other;
4. reject equal non-empty exclusive groups unless the entries form the explicitly requested parent-child relation;
5. allocate monotonically increasing token and sequence;
6. append one snapshot;
7. return `Opened` with the new handle.

Build `InputOrder` by sorting:

1. descendants before ancestors;
2. `Blocking`, `Modal`, `Screen`, `Passive`;
3. newest sequence first.

- [ ] **Step 4: Add failing close tests**

```csharp
[TestCase]
public void Close_Parent_ClosesDescendantsTopmostFirst()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(Policy(UIScreenKinds.Pause)).Handle!.Value;
    var settings = model.Open(Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;
    var confirm = model.Open(Policy(UIScreenKinds.ConfirmQuitToMain) with { Parent = settings }).Handle!.Value;

    var result = model.Close(pause);

    AssertThat(result.ClosedEntries.Select(e => e.Handle).ToArray())
        .ContainsExactly(confirm, settings, pause);
    AssertThat(model.Entries.Count).IsEqual(0);
}

[TestCase]
public void Close_SameHandleTwice_IsIdempotent()
{
    var model = new UIScreenStackModel();
    var handle = model.Open(Policy(UIScreenKinds.Settings)).Handle!.Value;

    AssertThat(model.Close(handle).Status).IsEqual(UIScreenCloseStatus.Closed);
    AssertThat(model.Close(handle).Status).IsEqual(UIScreenCloseStatus.AlreadyClosed);
}
```

- [ ] **Step 5: Implement close cascade**

`Close` must:

- return `AlreadyClosed` for a token in the closed-token set;
- return `StaleHandle` for an unknown token;
- collect all descendants recursively;
- order descendants deepest-first, then newest-first;
- remove every closed entry;
- remember every closed token;
- return snapshots in cleanup order.

- [ ] **Step 6: Run the full stack suite**

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/hosting/UIScreenStackModel.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs
git commit -m "feat: add UIScreenHost stack model"
```

---

### Task 3: Resolve Effective and Lower-Layer Policy

**Files:**
- Create: `scripts/ui/hosting/UIScreenPolicyResolver.cs`
- Create: `tests/ui/hosting/UIScreenPolicyResolverTest.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<UIScreenEntrySnapshot>`.
- Produces: `UIScreenResolvedPolicy Resolve(IReadOnlyList<UIScreenEntrySnapshot> entries)`.

- [ ] **Step 1: Write failing effective-policy tests**

```csharp
[TestSuite]
public partial class UIScreenPolicyResolverTest
{
    [TestCase]
    public void Resolve_PauseAndBlock_AreOrReduced()
    {
        var result = UIScreenPolicyResolver.Resolve(Snapshots(
            Policy(UIScreenKinds.Pause) with { PauseTree = true, BlockGameplayInput = true },
            Policy(UIScreenKinds.RewardToast) with { InputPriority = UIInputPriority.Passive }));

        AssertThat(result.PauseTree).IsTrue();
        AssertThat(result.BlockGameplayInput).IsTrue();
    }

    [TestCase]
    public void Resolve_CursorAndHud_UseHighestLogicalExplicitOverride()
    {
        var pause = Snapshot(Policy(UIScreenKinds.Pause) with
        {
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Visible
        }, 1);
        var inventory = Snapshot(Policy(UIScreenKinds.Inventory) with
        {
            Parent = pause.Handle,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden
        }, 2);

        var result = UIScreenPolicyResolver.Resolve(new[] { pause, inventory });

        AssertThat(result.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(result.Hud).IsEqual(UIHudPolicy.Hidden);
    }
}
```

- [ ] **Step 2: Add failing compositional-effect tests**

```csharp
[TestCase]
public void LowerLayers_ParentContributionSurvivesChildOpen()
{
    var gameplay = Snapshot(Policy("gameplay") with { Layer = UIScreenLayer.Hud }, 1);
    var pause = Snapshot(Policy(UIScreenKinds.Pause) with
    {
        LowerLayers = UILowerLayerPolicy.VisibleInert
    }, 2);
    var settings = Snapshot(Policy(UIScreenKinds.Settings) with
    {
        Parent = pause.Handle,
        LowerLayers = UILowerLayerPolicy.Hidden
    }, 3);

    var result = UIScreenPolicyResolver.Resolve(new[] { gameplay, pause, settings });

    AssertThat(result.LowerLayerEffects[gameplay.Handle]).IsEqual(UILowerLayerPolicy.Hidden);
    AssertThat(result.LowerLayerEffects[pause.Handle]).IsEqual(UILowerLayerPolicy.Hidden);
}

[TestCase]
public void LowerLayers_AfterChildClose_ParentEffectRemains()
{
    var gameplay = Snapshot(Policy("gameplay") with { Layer = UIScreenLayer.Hud }, 1);
    var pause = Snapshot(Policy(UIScreenKinds.Pause) with
    {
        LowerLayers = UILowerLayerPolicy.VisibleInert
    }, 2);

    var result = UIScreenPolicyResolver.Resolve(new[] { gameplay, pause });

    AssertThat(result.LowerLayerEffects[gameplay.Handle])
        .IsEqual(UILowerLayerPolicy.VisibleInert);
}
```

- [ ] **Step 3: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenPolicyResolverTest"
```

- [ ] **Step 4: Implement resolver records and algorithms**

Create:

```csharp
public sealed record UIScreenResolvedPolicy(
    bool PauseTree,
    bool BlockGameplayInput,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> LowerLayerEffects);
```

Implementation rules:

- pause and gameplay block are OR reductions;
- cursor/HUD use the first non-`Inherit` value in logical input order;
- top input owner is the first non-Passive entry in logical order;
- each target receives contributions from every active owner above it;
- reduce contributions with `Hidden=2`, `VisibleInert=1`, `VisibleInteractive=0`;
- return copied read-only collections;
- never retain live Godot state.

- [ ] **Step 5: Run stack and resolver suites together**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenPolicyResolverTest"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/ui/hosting/UIScreenPolicyResolver.cs \
  tests/ui/hosting/UIScreenPolicyResolverTest.cs
git commit -m "feat: resolve UIScreenHost policy"
```

---

### Task 4: Build the Host Scene and Process/Subwindow Gate

**Files:**
- Create: `scenes/ui/UIScreenHost.tscn`
- Create: `scripts/ui/hosting/UIScreenViewAdapter.cs`
- Create: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostTestSupport.cs`
- Create: `tests/ui/hosting/UIScreenHostProcessModeTest.cs`
- Create: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`

**Interfaces:**
- Consumes: normalized policy, adapter delegates, stack model, resolver.
- Produces:
  - `UIScreenOpenResult TryPresent(Node view, UIScreenEntrySpec spec)`
  - `UIScreenCloseResult TryClose(UIScreenHandle handle, UIScreenCloseReason reason)`
  - host layer node paths and adapter registry.

- [ ] **Step 1: Write failing scene/process tests**

```csharp
[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostProcessModeTest : Node
{
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
}
```

Add a test that opens a pausing entry, asserts the host can process while paused, then closes it and restores the incoming pause state.

- [ ] **Step 2: Write failing embedded-subwindow tests**

Test both paths:

```csharp
[TestCase]
public async Task Present_Window_WhenEmbeddingDisabled_IsRejectedWithoutMutation()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var viewport = fixture.Host.GetViewport();
    bool incoming = viewport.GuiEmbedSubwindows;
    viewport.GuiEmbedSubwindows = false;

    var result = fixture.Host.TryPresent(
        new AcceptDialog(),
        UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad));

    AssertThat(result.Status).IsEqual(UIScreenOpenStatus.UnsupportedSubwindowMode);
    AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
    viewport.GuiEmbedSubwindows = incoming;
}
```

With embedding enabled, assert `Opened` and the Window is its own focus viewport.

- [ ] **Step 3: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostProcessModeTest|FullyQualifiedName~UIScreenHostSubwindowTest"
```

- [ ] **Step 4: Create the host scene**

`UIScreenHost.tscn` must contain:

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

- [ ] **Step 5: Implement adapter creation and process-policy validation**

`UIScreenViewAdapter` stores the live view, normalized delegates, incoming process mode, incoming presentation/interactivity values, focus delegates, cleanup, and node lifetime.

Factory rules:

1. unparented Control attaches to the declared Control layer;
2. Control already beneath that exact layer is accepted;
3. Control parented elsewhere returns `InvalidControlParentage`;
4. embedded Window requires `GuiEmbedSubwindows=true`, parents beneath host, and uses itself as focus viewport;
5. disabled embedding returns `UnsupportedSubwindowMode`;
6. `UIProcessPolicy` snapshots before any change and restores on failure/close;
7. an unusable process policy returns `InvalidProcessPolicy` before model mutation.

- [ ] **Step 6: Implement atomic registration**

`TryPresent` order:

1. reject teardown or malformed scene;
2. validate node instance;
3. normalize spec;
4. create/validate adapter without stack mutation;
5. call model `Open`;
6. attach/parent view and apply process mode;
7. store adapter by handle token;
8. subscribe to `TreeExiting`;
9. recompute policy;
10. return `Opened`.

If attachment/subscription fails after model open, close the model entry, restore adapter snapshots, and return a stable failure status.

- [ ] **Step 7: Run process/subwindow tests**

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add scenes/ui/UIScreenHost.tscn \
  scripts/ui/hosting/UIScreenViewAdapter.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostTestSupport.cs \
  tests/ui/hosting/UIScreenHostProcessModeTest.cs \
  tests/ui/hosting/UIScreenHostSubwindowTest.cs
git commit -m "feat: add UIScreenHost scene and adapters"
```

---

### Task 5: Implement Deterministic Cancel Dispatch

**Files:**
- Create: `scripts/ui/hosting/UIScreenInputDispatcher.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostInputTest.cs`

**Interfaces:**
- Consumes: model input order, adapter interceptors, options, effective state.
- Produces: `UIInputDispatchResult TryHandleInput(InputEvent inputEvent)` and host `_Input`.

- [ ] **Step 1: Write failing one-event/one-attempt test**

```csharp
[TestCase]
public async Task Input_PauseAndUiCancelCoMatch_TraversesOnce()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(
        this,
        new[] { new StringName("pause_menu"), new StringName("ui_cancel") });
    int calls = 0;

    fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
    {
        InterceptCancel = _ =>
        {
            calls++;
            return UIInputInterception.ConsumeHere;
        }
    });

    var input = UIScreenHostTestSupport.EscapeBoundTo("pause_menu", "ui_cancel");
    var result = fixture.Host.TryHandleInput(input);

    AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
    AssertThat(calls).IsEqual(1);
}
```

- [ ] **Step 2: Write failing entry-action and precedence tests**

Cover these exact cases:

- active Inventory closes on `toggle_inventory`;
- Settings top plus `toggle_inventory` returns `NoOwner` and remains open;
- entry-scoped actions never invoke root fallback;
- `ConsumeHere` consumes without closing;
- `ReserveForNativeHandler` returns `ReservedForTopEntry`;
- `DeferToPolicy` then static `Close` closes;
- static `None` continues to parent;
- root fallback runs only for matched core actions with no owner;
- a pass-through event matching native `ui_close_dialog` produces one host traversal.

- [ ] **Step 3: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostInputTest"
```

- [ ] **Step 4: Implement dispatcher algorithm**

For each event:

1. match all core actions once into a set;
2. prune invalid entries;
3. consume when a live restoration lease matches core or top-entry action;
4. traverse model `InputOrder`;
5. match each candidate's entry-scoped actions;
6. skip candidates with no core or entry match;
7. invoke dynamic interceptor;
8. resolve `ConsumeHere`, `ReserveForNativeHandler`, or static policy;
9. stop at first owner/reservation/close;
10. invoke root fallback only for unmatched core ownership;
11. return `NoOwner` otherwise.

Static policy mapping:

- `None` → continue;
- `Close` → host close with `Cancel`, return `Consumed`;
- `Consume` → `Consumed`;
- `PassThrough` → `ReservedForTopEntry`.

- [ ] **Step 5: Wire host `_Input`**

```csharp
public override void _Input(InputEvent inputEvent)
{
    if (_tearingDown)
        return;

    if (TryHandleInput(inputEvent) == UIInputDispatchResult.Consumed)
        GetViewport().SetInputAsHandled();
}
```

Leave `ReservedForTopEntry` and `NoOwner` unhandled.

- [ ] **Step 6: Run input plus pure suites**

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/hosting/UIScreenInputDispatcher.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostInputTest.cs
git commit -m "feat: dispatch UIScreenHost cancel input"
```

---

### Task 6: Own Pause, Cursor, HUD, and Effective State

**Files:**
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Modify: `scripts/ui/hosting/UIScreenContracts.cs`
- Modify: `tests/ui/hosting/UIScreenHostProcessModeTest.cs`
- Create: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Consumes: `UIScreenResolvedPolicy`.
- Produces: exact state leases, `CurrentState`, `EffectiveStateChanged`, drift diagnostics, and teardown restoration.

- [ ] **Step 1: Write failing exact-restoration tests**

Add tests proving:

- incoming `SceneTree.Paused=true` restores true;
- incoming false restores false after the last pausing entry;
- non-pausing children do not replace the parent's pause baseline;
- first cursor override captures exact `Input.MouseMode`, last override restores it;
- first HUD override captures exact `HudRoot.Visible`, last override restores it;
- explicit HUD policy with null `HudRoot` rejects before mutation;
- block callback fires only on an effective boolean transition.

Use this pause-parent case:

```csharp
[TestCase]
public async Task PauseLease_ChildClose_DoesNotResumeParent()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    GetTree().Paused = false;

    var pause = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
    {
        PauseTree = true
    });
    var settings = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
    {
        Parent = pause.Handle,
        PauseTree = false
    });

    fixture.Host.TryClose(settings.Handle!.Value, UIScreenCloseReason.Programmatic);
    AssertThat(GetTree().Paused).IsTrue();

    fixture.Host.TryClose(pause.Handle!.Value, UIScreenCloseReason.Programmatic);
    AssertThat(GetTree().Paused).IsFalse();
}
```

- [ ] **Step 2: Write failing pause-drift test**

Open a pausing entry, force `GetTree().Paused=false`, advance one process frame, and assert:

- tree pause is reasserted true;
- drift count increments once;
- final close restores the original incoming baseline.

- [ ] **Step 3: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostProcessModeTest"
```

- [ ] **Step 4: Implement exact state leases**

Maintain separate host records:

```csharp
private sealed record PauseLease(bool IncomingPaused);
private sealed record CursorLease(Input.MouseModeEnum IncomingMode);
private sealed record HudLease(bool IncomingVisible);
```

Apply only on effective boundary transitions. Never replace an active baseline when another owner joins.

- [ ] **Step 5: Implement drift detection**

While a pause lease exists, an Always-processing check must:

```csharp
if (!GetTree().Paused)
{
    _pauseOwnershipDriftCount++;
    GD.PushError("[UIScreenHost] SceneTree.Paused changed while host pause lease is active; reasserting host ownership.");
    GetTree().Paused = true;
}
```

Keep the original incoming baseline for final restoration.

- [ ] **Step 6: Publish consistent effective state**

Expose:

```csharp
public UIScreenEffectiveState CurrentState { get; private set; } =
    new(false, false, UICursorPolicy.Inherit, UIHudPolicy.Inherit, null, false);

public event Action<UIScreenEffectiveState>? EffectiveStateChanged;
```

Publish only after adapter effects complete. Invoke `GameplayInputBlockChanged` only when the block component changes.

- [ ] **Step 7: Run and commit**

```bash
git add scripts/ui/hosting/UIScreenHost.cs \
  scripts/ui/hosting/UIScreenContracts.cs \
  tests/ui/hosting/UIScreenHostProcessModeTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: own UIScreenHost effective state"
```

---

### Task 7: Apply Compositional Control and Embedded-Window Effects

**Files:**
- Modify: `scripts/ui/hosting/UIScreenViewAdapter.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Modify: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Consumes: resolver `LowerLayerEffects`.
- Produces: exact baseline snapshots and effect weakening/restoration.

- [ ] **Step 1: Write failing nested-effect test**

Build gameplay, Pause, and Settings synthetic Controls. Assert:

1. Pause makes gameplay inert;
2. Settings hides Pause;
3. Pause's gameplay-inert contribution remains while Settings is open;
4. closing Settings shows Pause but keeps gameplay inert;
5. closing Pause restores gameplay exactly.

- [ ] **Step 2: Write failing embedded-Window restoration tests**

For an embedded `AcceptDialog`:

- start with `GuiDisableInput=true` and `Unfocusable=false`;
- apply Hidden and VisibleInert from different owners;
- remove the stronger owner;
- assert the weaker effect remains;
- remove the final owner;
- assert exact incoming values return;
- verify supplied `SetPresented(true)` is used when plain `Show()` is insufficient.

- [ ] **Step 3: Run and verify failure**

Run lifecycle and subwindow filters.

- [ ] **Step 4: Implement per-target effect baselines**

Use one baseline while any owner contributes:

```csharp
internal sealed record UIControlEffectBaseline(bool Visible, bool ProcessInputEnabled);
internal sealed record UIWindowEffectBaseline(bool Visible, bool GuiDisableInput, bool Unfocusable);
```

Transition rules:

- interactive → inert/hidden: capture once;
- hidden → inert: restore presentation, retain baseline, then apply inert;
- inert → hidden: retain baseline and hide;
- any effect → interactive: restore exact baseline and clear it.

- [ ] **Step 5: Implement Control and Window mechanisms**

Control:

- Hidden changes `Visible`;
- VisibleInert uses `InputShield` and `SetInteractive(false)` for direct `_Input` handlers;
- apply lower-handler disablement before publishing state.

Embedded Window:

- Hidden calls the presentation adapter;
- VisibleInert sets `GuiDisableInput=true` and `Unfocusable=true`;
- never rely on the Control shield for Window input;
- reject an owner open when required effect adapters are unavailable.

- [ ] **Step 6: Run and commit**

```bash
git add scripts/ui/hosting/UIScreenViewAdapter.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostSubwindowTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: apply UIScreenHost layer effects"
```

---

### Task 8: Implement Focus Coordination and Restoration Leases

**Files:**
- Create: `scripts/ui/hosting/UIScreenFocusCoordinator.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostFocusTest.cs`

**Interfaces:**
- Consumes: adapters, root sink, handle tokens, close transactions.
- Produces: initial focus, per-Window sinks, focus records, generation-tagged lease, and barrier state.

- [ ] **Step 1: Write failing sink tests**

```csharp
[TestCase]
public async Task BlockingControlWithoutFocusableChild_UsesRootSink()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.SaveError) with
    {
        InputPriority = UIInputPriority.Blocking,
        InitialFocus = null
    });

    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    AssertThat(fixture.Host.GetViewport().GuiGetFocusOwner())
        .IsEqual(fixture.Host.GetNode<Control>("FocusSink"));
}
```

Add an embedded-Window test asserting its dynamically created sink is visible, transparent/non-drawing, 1×1 or layout-neutral, `MouseFilter.Ignore`, and `FocusMode.All`.

- [ ] **Step 2: Write failing focus-order tests**

Verify restoration order:

1. explicit restore target;
2. captured parent focus in captured viewport;
3. parent initial target;
4. first focusable descendant;
5. correct viewport sink;
6. release focus.

Verify initial-focus deferral has no Cancel barrier and a stale initial callback cannot focus a closed entry.

- [ ] **Step 3: Write failing lease-release tests**

Named cases:

- valid target completes lease;
- target freed before callback completes lease;
- host teardown completes lease synchronously;
- re-entrant close supersedes prior generation;
- stale callback cannot clear newer lease;
- duplicate close creates one lease;
- next core Cancel works after every invalidation path.

- [ ] **Step 4: Run and verify failure**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostFocusTest"
```

- [ ] **Step 5: Implement focus records and sinks**

Create:

```csharp
internal sealed record UIFocusRecord(
    Viewport Viewport,
    Control? FocusOwner,
    UIScreenHandle ParentHandle);

internal sealed record UIFocusRestorationLease(
    long Generation,
    UIScreenHandle ClosedHandle);
```

On open, capture the active parent's registered viewport/focus before child effects. Defer initial target selection and token-check the callback.

- [ ] **Step 6: Implement guaranteed lease release**

Use generation checks and `try/finally`:

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

Before starting a newer lease, complete/invalidate the older generation. Teardown disables dispatch, cancels scheduled callbacks, and clears the live lease synchronously.

- [ ] **Step 7: Wire lease state into effective state and input dispatcher**

`CurrentState.IsFocusRestorationPending` derives from coordinator lease presence. Dispatcher consumes matching actions only while a live lease exists.

- [ ] **Step 8: Run and commit**

```bash
git add scripts/ui/hosting/UIScreenFocusCoordinator.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostFocusTest.cs
git commit -m "feat: coordinate UIScreenHost focus"
```

---

### Task 9: Harden External Deletion, Re-entrancy, and Diagnostics

**Files:**
- Modify: `scripts/ui/hosting/UIScreenContracts.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Consumes: stack close mutations, adapters, coordinator, state leases.
- Produces: mutation queue, once-only cleanup, pruning, teardown, and immutable diagnostics.

- [ ] **Step 1: Add failing deletion/re-entrancy tests**

Test:

- externally freed parent closes descendants first;
- cleanup runs at most once;
- invalid Godot object is never dereferenced;
- pause/lower layers/focus restore after pruning;
- stale handle returns `AlreadyClosed` after prune;
- cleanup callback can request another close without corrupting current mutation;
- duplicate queued closes collapse;
- opens during teardown return `HostTearingDown`;
- effective state emits after the complete transaction only.

Use this re-entrant case:

```csharp
[TestCase]
public async Task Cleanup_ReentrantClose_IsQueuedAfterCurrentMutation()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    UIScreenHandle second = default;
    var first = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec("first") with
    {
        Cleanup = _ => fixture.Host.TryClose(second, UIScreenCloseReason.Programmatic)
    });
    second = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec("second")).Handle!.Value;

    fixture.Host.TryClose(first.Handle!.Value, UIScreenCloseReason.Programmatic);

    AssertThat(fixture.Host.IsActive(first.Handle.Value)).IsFalse();
    AssertThat(fixture.Host.IsActive(second)).IsFalse();
}
```

- [ ] **Step 2: Add failing diagnostics tests**

Require immutable snapshots for:

- handles/order/kind/parent/layer/priority/process policy;
- normalized groups and incompatibilities;
- effective state;
- lower-layer contributors/effects;
- action ownership;
- focus viewport/control/sink/lease generation;
- process/subwindow validation;
- active state leases;
- pause drift count.

- [ ] **Step 3: Implement guarded mutation queue**

```csharp
private void EnqueueMutation(Action mutation)
{
    _mutationQueue.Enqueue(mutation);
    if (_isMutating)
        return;

    _isMutating = true;
    try
    {
        while (_mutationQueue.Count > 0)
            _mutationQueue.Dequeue().Invoke();
    }
    finally
    {
        _isMutating = false;
    }
}
```

Mark handles closing before callbacks. Deduplicate close tokens. Publish policy after each complete operation.

- [ ] **Step 4: Implement node-exit pruning and teardown**

On registered `TreeExiting`:

1. ignore exits already owned by a host close;
2. close descendants and model entry with `NodeFreed`;
3. skip live-object operations after invalidity;
4. invoke managed cleanup once;
5. recompute lower/effective policy;
6. complete focus restoration with fallback or no target.

On host `_ExitTree`:

1. set tearing-down and disable input;
2. close topmost-first with `HostTeardown`;
3. reject callback-driven opens;
4. complete restoration lease;
5. restore all state/adapter snapshots once;
6. unsubscribe node events and remove dynamic sinks.

- [ ] **Step 5: Add immutable diagnostics record**

Define `UIScreenHostDiagnostics` in `UIScreenContracts.cs`. Return arrays/read-only dictionaries copied from internals. Never expose adapter delegates or mutable model lists.

- [ ] **Step 6: Run lifecycle, focus, input, process, and subwindow suites**

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/hosting/UIScreenContracts.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: harden UIScreenHost lifecycle"
```

---

### Task 10: Prove Contract Scenarios and Publish Integration Docs

**Files:**
- Create: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Create: `docs/ui/hpa-378/uiscreenhost-contract.md`
- Modify: `docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md`

**Interfaces:**
- Consumes: complete host API.
- Produces: acceptance evidence and HPA-379 integration contract.

- [ ] **Step 1: Implement named synthetic scenario tests**

Create complete tests for this matrix:

| Test name | Setup | Required assertions |
|---|---|---|
| `InventoryChildOfPause_PausesWorldHidesHudAndReturnsToPause` | Pause parent plus Inventory child | tree paused; HUD hidden; Pause contribution retained; child close returns to Pause; parent close restores tree/HUD |
| `SettingsChildOfPause_PreservesPauseGameplayInertContribution` | gameplay + Pause + Settings | Settings hides Pause; gameplay remains blocked by Pause; closing Settings restores Pause only |
| `DestructiveConfirmation_CancelReturnsWithoutDestructiveCallback` | flow-specific confirmation child | safe focus; Cancel closes child; destructive callback count stays zero |
| `RewardToast_IsPassiveAndNeverBecomesInputOwner` | modal plus toast | modal remains top input owner; no pause/block/focus/lower-layer change from toast |
| `RequiredAcknowledgement_ConsumesCancelUntilContinue` | blocking acknowledgement | Cancel consumed without close; explicit Continue callback closes |
| `BattlePresentation_RemainsTopmostAfterDomainFlagClears` | synthetic Battle entry plus false domain flag | Battle still blocks and owns/reserves core Cancel until view termination |
| `EitherPresentationOrDomainBlock_SuppressesComposedGameplayPredicate` | toggle each source independently | OR predicate is true for either source and false only when both are false |

Each test must instantiate synthetic Controls/embedded AcceptDialogs and explicit callbacks. Do not instantiate production Game, Inventory, Pause, or Battle scenes.

- [ ] **Step 2: Run scenario suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostContractScenarioTest"
```

Expected: PASS. A failure requiring production-flow edits is documented for HPA-379 instead of expanding this task.

- [ ] **Step 3: Write public contract documentation**

`docs/ui/hpa-378/uiscreenhost-contract.md` must document:

1. scene node paths and process modes;
2. `TryPresent`, `TryClose`, `TryHandleInput`, `CurrentState`, events, and diagnostics;
3. every public enum/status;
4. Control defaults;
5. embedded-Window defaults and embedding precondition;
6. process-policy matrix;
7. core versus entry-scoped actions;
8. dynamic interception precedence;
9. parent/child and exclusive-group examples;
10. lower-layer reduction examples;
11. exact state restoration guarantees;
12. focus/sink/restoration-lease guarantees;
13. teardown/invalid-node behavior;
14. HPA-379 prerequisites: GridMap audit/correction, Pausable HUD, explicit embedding, composed gameplay block;
15. explicit out-of-scope items.

Include compilable synthetic registration examples for Pause, Inventory, Settings, embedded Save/Load, Battle lifetime, reward toast, and required acknowledgement.

- [ ] **Step 4: Run every focused host suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreen"
```

Expected: zero failures and zero skips.

- [ ] **Step 5: Run build and full suite**

```bash
dotnet build Sirius.sln --no-restore
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected:

- build exit code 0;
- full test exit code 0;
- no new orphan-warning signature relative to the branch baseline.

Record fresh counts; do not copy older PR counts.

- [ ] **Step 6: Perform completion review**

Verify:

- no `Game.cs`, MainMenu, floor, or `project.godot` diff;
- no production flow partially migrated;
- public signatures match design and docs;
- Passive validation, group normalization, embedded-window rejection, pause drift, lower-layer composition, focus lease release, and teardown have named tests;
- no placeholder text or empty implementation remains;
- docs use actual names/status codes.

- [ ] **Step 7: Mark design approved and commit final evidence**

Only after Step 5 succeeds, change the design header to:

```markdown
**Status:** Approved design
```

Then commit:

```bash
git add tests/ui/hosting/UIScreenHostContractScenarioTest.cs \
  docs/ui/hpa-378/uiscreenhost-contract.md \
  docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md
git commit -m "test: verify UIScreenHost contract"
```

- [ ] **Step 8: Update implementation PR evidence**

Report exact focused/full test counts, build result, orphan comparison, and confirmation that production integration remains HPA-379. Call out the HPA-379 prerequisites: embedded-subwindow pinning, runtime GridMap correction, Pausable HUD composition, and unified gameplay-block predicate.
