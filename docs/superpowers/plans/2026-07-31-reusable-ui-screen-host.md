# Reusable UI Screen Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the reusable, scene-local `UIScreenHost` contract for HPA-378, including pure stack/policy logic, Godot Control and embedded-Window adapters, deterministic Cancel routing, exact pause/cursor/HUD/process restoration, focus restoration leases, diagnostics, and synthetic lifecycle coverage.

**Architecture:** Keep `UIScreenStackModel` and `UIScreenPolicyResolver` free of live Godot objects. `UIScreenHost` owns `ProcessMode.Always`, scene attachment, adapter state, input dispatch, pause and presentation snapshots, focus coordination, and teardown. HPA-378 stops at the reusable host and synthetic contract tests; HPA-379 owns MainMenu/Game/floor integration, runtime GridMap process correction, and production-flow migration.

**Tech Stack:** Godot 4.6, C#/.NET 8, GdUnit4, existing `Sirius.sln`, `test.runsettings.local`.

## Global Constraints

- Do not modify `scripts/game/Game.cs`, `scripts/ui/MainMenu.cs`, floor scenes, `project.godot`, or existing production screen controllers in HPA-378.
- Do not introduce an autoload, global UI singleton, cross-scene navigation history, implicit replacement, or detached native-window support.
- `UIScreenHost` must be scene-local and `ProcessMode.Always`; `HUDLayer` must be explicitly `Pausable`; active presentation layers must process while paused.
- Registered `Window` and `AcceptDialog` entries require embedded subwindows. Disabled embedding returns `UnsupportedSubwindowMode` without stack mutation.
- The pure model may contain `StringName` and immutable value types, but no `Node`, `Control`, `Window`, `Viewport`, `Callable`, or delegates.
- Concrete flow kinds are unique. Categories such as confirmations use normalized exclusive groups.
- A passive entry cannot pause, block gameplay, own Cancel, declare entry-scoped actions, affect lower layers, or request focus.
- One physical event yields one logical Cancel traversal. `ui_close_dialog` remains a native GUI pass-through surface.
- Lower-layer effects compose from every active owner using `Hidden > VisibleInert > VisibleInteractive`.
- Pause, process mode, cursor, HUD visibility, Control visibility/interactivity, Window flags, and focus state restore exact incoming values.
- A generation-tagged restoration lease must clear on success, invalid target, re-entrant close, stale callback, and teardown.
- No new third-party dependencies.
- Every task follows red-green-refactor and ends with focused verification plus a commit.

## File Map

### Create

- `scripts/ui/hosting/UIScreenKinds.cs` — canonical flow-specific kinds and exclusive-group constants.
- `scripts/ui/hosting/UIScreenContracts.cs` — public enums, options, contexts, results, effective state, and diagnostics records.
- `scripts/ui/hosting/UIScreenHandle.cs` — opaque instance identity.
- `scripts/ui/hosting/UIScreenEntryPolicy.cs` — normalized pure-model policy.
- `scripts/ui/hosting/UIScreenEntrySpec.cs` — Godot-facing registration specification and normalization.
- `scripts/ui/hosting/UIScreenStackModel.cs` — pure entry ownership, compatibility, parent/child ordering, and close cascades.
- `scripts/ui/hosting/UIScreenPolicyResolver.cs` — pure effective policy and lower-layer reduction.
- `scripts/ui/hosting/UIScreenViewAdapter.cs` — live Control/embedded-Window snapshots and adapter operations.
- `scripts/ui/hosting/UIScreenInputDispatcher.cs` — core and entry-scoped Cancel matching and precedence.
- `scripts/ui/hosting/UIScreenFocusCoordinator.cs` — per-viewport focus records, sinks, and restoration leases.
- `scripts/ui/hosting/UIScreenHost.cs` — scene-local orchestration, registration, lifecycle, pause/cursor/HUD ownership, mutation queue, and diagnostics.
- `scenes/ui/UIScreenHost.tscn` — host scene, layers, shields, and root focus sink.
- `tests/ui/hosting/UIScreenHostTestSupport.cs` — shared synthetic views, action setup, and cleanup helpers.
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

- No production file outside `scripts/ui/hosting/`, `scenes/ui/`, `tests/ui/hosting/`, and `docs/ui/hpa-378/` in this implementation.

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
- Consumes: Godot `StringName`, `Node.ProcessModeEnum`, `InputEvent`, `Control`, and `Viewport` only in adapter-facing contracts.
- Produces: `UIScreenHandle`, `UIScreenEntryPolicy`, `UIScreenEntrySpec.Normalize()`, all public enums/status records, `UIScreenKinds`, and `UIScreenExclusiveGroups` used by every later task.

- [ ] **Step 1: Write failing normalization and validation tests**

Create `tests/ui/hosting/UIScreenStackModelTest.cs` with the contract-level cases first:

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
        var spec = TestSpec(UIScreenKinds.Pause) with { ExclusiveGroup = default };
        var result = spec.Normalize();

        AssertThat(result.Status).IsEqual(UIScreenOpenStatus.Opened);
        AssertThat(result.Policy!.ExclusiveGroup).IsEqual(UIScreenExclusiveGroups.None);
    }

    [TestCase]
    public void Normalize_PassiveBlockingPolicy_IsRejected()
    {
        var spec = TestSpec(UIScreenKinds.RewardToast) with
        {
            InputPriority = UIInputPriority.Passive,
            BlockGameplayInput = true
        };

        AssertThat(spec.Normalize().Status).IsEqual(UIScreenOpenStatus.InvalidSpecification);
    }

    [TestCase]
    public void Normalize_Collections_AreNeverNullInPolicy()
    {
        var result = TestSpec(UIScreenKinds.Settings).Normalize();

        AssertThat(result.Policy!.IncompatibleKinds).IsNotNull();
        AssertThat(result.Policy.EntryCancelActions).IsNotNull();
    }

    private static UIScreenEntrySpec TestSpec(StringName kind) => new()
    {
        Kind = kind,
        Layer = UIScreenLayer.Screen,
        InputPriority = UIInputPriority.Screen,
        ProcessPolicy = UIProcessPolicy.InheritHost,
        PauseTree = false,
        BlockGameplayInput = true,
        Cursor = UICursorPolicy.Visible,
        Hud = UIHudPolicy.Inherit,
        LowerLayers = UILowerLayerPolicy.VisibleInert,
        Cancel = UICancelPolicy.Close,
        IncompatibleKinds = new HashSet<StringName>(),
        EntryCancelActions = new HashSet<StringName>()
    };
}
```

- [ ] **Step 2: Run the focused test and verify the compile failure**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest"
```

Expected: FAIL because `UIScreenEntrySpec`, `UIScreenKinds`, and result types do not exist.

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

- [ ] **Step 4: Add public contract types with stable result codes**

Create `UIScreenContracts.cs` containing exactly these names:

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

- [ ] **Step 5: Add handle, normalized policy, and spec normalization**

Create `UIScreenHandle.cs`:

```csharp
using Godot;

public readonly record struct UIScreenHandle(long Token, StringName Kind);
```

Create `UIScreenEntryPolicy.cs` with immutable, non-null collections. Create `UIScreenEntrySpec.Normalize()` returning an internal `UIScreenSpecNormalizationResult` with `Status` and `Policy`.

Normalization rules:

```csharp
private static StringName NormalizeGroup(StringName? value) =>
    value is null || value.Value.IsEmpty ? UIScreenExclusiveGroups.None : value.Value;

private bool IsPassiveValid() =>
    InputPriority != UIInputPriority.Passive ||
    (!PauseTree &&
     !BlockGameplayInput &&
     Cancel == UICancelPolicy.None &&
     EntryCancelActions.Count == 0 &&
     LowerLayers == UILowerLayerPolicy.VisibleInteractive &&
     InitialFocus is null);
```

Reject empty `Kind`, null collections after normalization failure, `WhenPaused` entries intended for an unpaused context only when the host later validates context, and invalid Passive combinations here.

- [ ] **Step 6: Run normalization tests**

Run the focused command from Step 2.

Expected: PASS for normalization cases.

- [ ] **Step 7: Commit the public contract foundation**

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
- Modify: `scripts/ui/hosting/UIScreenContracts.cs`
- Create: `scripts/ui/hosting/UIScreenStackModel.cs`
- Modify: `tests/ui/hosting/UIScreenStackModelTest.cs`

**Interfaces:**
- Consumes: `UIScreenEntryPolicy`, `UIScreenHandle`, stable open/close statuses.
- Produces: `UIScreenStackModel.Open`, `UIScreenStackModel.Close`, `InputOrder`, `Entries`, and close cascades used by resolver and host.

- [ ] **Step 1: Add failing duplicate, compatibility, and parent tests**

Add:

```csharp
[TestCase]
public void Open_DuplicateConcreteKind_IsRejectedWithoutMutation()
{
    var model = new UIScreenStackModel();
    var first = model.Open(Policy(UIScreenKinds.Pause));
    var second = model.Open(Policy(UIScreenKinds.Pause));

    AssertThat(first.Status).IsEqual(UIScreenOpenStatus.Opened);
    AssertThat(second.Status).IsEqual(UIScreenOpenStatus.DuplicateKind);
    AssertThat(model.Entries.Count).IsEqual(1);
}

[TestCase]
public void Open_DifferentConfirmationKinds_SameBlockingGroup_Conflict()
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
public void Open_ChildOutranksParentInInputOrder()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(Policy(UIScreenKinds.Pause)).Handle!.Value;
    var child = model.Open(Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;

    AssertThat(model.InputOrder[0].Handle).IsEqual(child);
    AssertThat(model.InputOrder[1].Handle).IsEqual(pause);
}
```

- [ ] **Step 2: Run and confirm failures**

Run the Task 1 focused command.

Expected: FAIL because `UIScreenStackModel` and helper snapshots are absent.

- [ ] **Step 3: Implement entry storage and open validation**

Create `UIScreenStackModel.cs` with:

```csharp
public sealed record UIScreenEntrySnapshot(
    UIScreenHandle Handle,
    UIScreenEntryPolicy Policy,
    long Sequence);

internal sealed record UIScreenStackCloseMutation(
    UIScreenCloseStatus Status,
    IReadOnlyList<UIScreenEntrySnapshot> ClosedEntries);

public sealed class UIScreenStackModel
{
    private readonly List<UIScreenEntrySnapshot> _entries = new();
    private readonly HashSet<long> _closedTokens = new();
    private long _nextToken = 1;
    private long _nextSequence = 1;

    public IReadOnlyList<UIScreenEntrySnapshot> Entries => _entries;
    public IReadOnlyList<UIScreenEntrySnapshot> InputOrder => BuildInputOrder();

    public UIScreenOpenResult Open(UIScreenEntryPolicy policy) { /* exact rules below */ }
    internal UIScreenStackCloseMutation Close(UIScreenHandle handle) { /* exact rules below */ }
}
```

`Open` must validate in this order:

1. active duplicate kind;
2. parent token exists and is active;
3. symmetric incompatibility;
4. equal non-empty exclusive group, except the requested parent/ancestor relation explicitly allows the child;
5. assign token and sequence;
6. append exactly once.

`BuildInputOrder` sorts by:

1. descendants before ancestors;
2. `Blocking > Modal > Screen > Passive`;
3. newest sequence first.

Do not mutate `_entries` on rejection.

- [ ] **Step 4: Add failing close-cascade tests**

```csharp
[TestCase]
public void Close_Parent_ClosesDescendantsTopmostFirst()
{
    var model = new UIScreenStackModel();
    var pause = model.Open(Policy(UIScreenKinds.Pause)).Handle!.Value;
    var settings = model.Open(Policy(UIScreenKinds.Settings) with { Parent = pause }).Handle!.Value;
    var confirm = model.Open(Policy(UIScreenKinds.ConfirmQuitToMain) with { Parent = settings }).Handle!.Value;

    var mutation = model.Close(pause);

    AssertThat(mutation.ClosedEntries.Select(e => e.Handle).ToArray())
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

- [ ] **Step 5: Implement close cascade and stale-handle distinction**

`Close` must:

- return `AlreadyClosed` when token is in `_closedTokens`;
- return `StaleHandle` when no active or previously closed token exists;
- collect every descendant recursively;
- sort descendants by depth descending, then sequence descending;
- remove each entry and add its token to `_closedTokens`;
- return each closed snapshot for host-side cleanup.

- [ ] **Step 6: Run all stack-model tests**

Expected: PASS.

- [ ] **Step 7: Commit the stack model**

```bash
git add scripts/ui/hosting/UIScreenStackModel.cs \
  scripts/ui/hosting/UIScreenContracts.cs \
  tests/ui/hosting/UIScreenStackModelTest.cs
git commit -m "feat: add UIScreenHost stack model"
```

---

### Task 3: Resolve Effective and Lower-Layer Policy Purely

**Files:**
- Create: `scripts/ui/hosting/UIScreenPolicyResolver.cs`
- Create: `tests/ui/hosting/UIScreenPolicyResolverTest.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<UIScreenEntrySnapshot>` from the stack model.
- Produces: `UIScreenResolvedPolicy` and per-target `UILowerLayerPolicy` contributions used by `UIScreenHost`.

- [ ] **Step 1: Write failing effective-policy tests**

```csharp
[TestSuite]
public partial class UIScreenPolicyResolverTest
{
    [TestCase]
    public void Resolve_PauseAndBlock_AreOrReduced()
    {
        var entries = Snapshots(
            Policy(UIScreenKinds.Pause) with { PauseTree = true, BlockGameplayInput = true },
            Policy(UIScreenKinds.RewardToast) with { InputPriority = UIInputPriority.Passive });

        var result = UIScreenPolicyResolver.Resolve(entries);

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
        }, sequence: 1);
        var inventory = Snapshot(Policy(UIScreenKinds.Inventory) with
        {
            Parent = pause.Handle,
            Cursor = UICursorPolicy.Visible,
            Hud = UIHudPolicy.Hidden
        }, sequence: 2);

        var result = UIScreenPolicyResolver.Resolve(new[] { pause, inventory });

        AssertThat(result.Cursor).IsEqual(UICursorPolicy.Visible);
        AssertThat(result.Hud).IsEqual(UIHudPolicy.Hidden);
    }
}
```

- [ ] **Step 2: Add failing compositional lower-layer tests**

```csharp
[TestCase]
public void ResolveLowerLayers_ParentContributionSurvivesChildOpen()
{
    var gameplay = Snapshot(Policy("gameplay") with
    {
        Layer = UIScreenLayer.Hud,
        InputPriority = UIInputPriority.Passive
    }, sequence: 1);
    var pause = Snapshot(Policy(UIScreenKinds.Pause) with
    {
        LowerLayers = UILowerLayerPolicy.VisibleInert
    }, sequence: 2);
    var settings = Snapshot(Policy(UIScreenKinds.Settings) with
    {
        Parent = pause.Handle,
        LowerLayers = UILowerLayerPolicy.Hidden
    }, sequence: 3);

    var result = UIScreenPolicyResolver.Resolve(new[] { gameplay, pause, settings });

    AssertThat(result.LowerLayerEffects[gameplay.Handle]).IsEqual(UILowerLayerPolicy.Hidden);
    AssertThat(result.LowerLayerEffects[pause.Handle]).IsEqual(UILowerLayerPolicy.Hidden);
}

[TestCase]
public void ResolveLowerLayers_AfterChildClose_ParentInertEffectRemains()
{
    var gameplay = Snapshot(Policy("gameplay") with { Layer = UIScreenLayer.Hud }, 1);
    var pause = Snapshot(Policy(UIScreenKinds.Pause) with
    {
        LowerLayers = UILowerLayerPolicy.VisibleInert
    }, 2);

    var result = UIScreenPolicyResolver.Resolve(new[] { gameplay, pause });

    AssertThat(result.LowerLayerEffects[gameplay.Handle]).IsEqual(UILowerLayerPolicy.VisibleInert);
}
```

- [ ] **Step 3: Run and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenPolicyResolverTest"
```

Expected: FAIL because resolver types do not exist.

- [ ] **Step 4: Implement the resolver**

Create:

```csharp
public sealed record UIScreenResolvedPolicy(
    bool PauseTree,
    bool BlockGameplayInput,
    UICursorPolicy Cursor,
    UIHudPolicy Hud,
    UIScreenHandle? TopInputOwner,
    IReadOnlyDictionary<UIScreenHandle, UILowerLayerPolicy> LowerLayerEffects);

public static class UIScreenPolicyResolver
{
    public static UIScreenResolvedPolicy Resolve(IReadOnlyList<UIScreenEntrySnapshot> entries)
    {
        var inputOrder = UIScreenOrdering.BuildInputOrder(entries);
        var cursor = FirstExplicit(inputOrder, e => e.Policy.Cursor, UICursorPolicy.Inherit);
        var hud = FirstExplicit(inputOrder, e => e.Policy.Hud, UIHudPolicy.Inherit);
        var effects = ResolveLowerLayerEffects(entries);

        return new(
            entries.Any(e => e.Policy.PauseTree),
            entries.Any(e => e.Policy.BlockGameplayInput),
            cursor,
            hud,
            inputOrder.FirstOrDefault(e => e.Policy.InputPriority != UIInputPriority.Passive)?.Handle,
            effects);
    }
}
```

For each target, inspect every active owner above it by ancestry, logical priority, and presentation sequence. Reduce all applicable effects with numeric strength:

```csharp
private static int Strength(UILowerLayerPolicy policy) => policy switch
{
    UILowerLayerPolicy.Hidden => 2,
    UILowerLayerPolicy.VisibleInert => 1,
    _ => 0
};
```

Do not retain snapshots or mutate Godot state in this class.

- [ ] **Step 5: Run resolver and stack suites together**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenStackModelTest|FullyQualifiedName~UIScreenPolicyResolverTest"
```

Expected: PASS.

- [ ] **Step 6: Commit the pure policy layer**

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
- Consumes: normalized entry policy, view-facing delegates, stack model, policy resolver.
- Produces: `UIScreenHost.TryPresent`, host scene layer paths, process-policy validation, and embedded-Window registration.

- [ ] **Step 1: Write the failing host-scene structure test**

```csharp
[TestSuite]
[RequireGodotRuntime]
public partial class UIScreenHostProcessModeTest : Node
{
    [TestCase]
    public async Task HostScene_HasRequiredProcessModesAndVisibleFocusSink()
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

- [ ] **Step 2: Write the failing paused-input test**

```csharp
[TestCase]
public async Task HostInput_RemainsEnabledWhileTreePaused()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var tree = GetTree();
    bool incoming = tree.Paused;

    var view = new Control();
    var opened = fixture.Host.TryPresent(view, UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
    {
        PauseTree = true,
        Cancel = UICancelPolicy.Consume
    });

    AssertThat(opened.Status).IsEqual(UIScreenOpenStatus.Opened);
    AssertThat(fixture.Host.CanProcess()).IsTrue();
    AssertThat(tree.Paused).IsTrue();

    fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);
    tree.Paused = incoming;
}
```

- [ ] **Step 3: Write embedded-subwindow acceptance and rejection tests**

```csharp
[TestCase]
public async Task Present_Window_WhenEmbeddingDisabled_IsRejectedWithoutMutation()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var viewport = fixture.Host.GetViewport();
    bool incoming = viewport.GuiEmbedSubwindows;
    viewport.GuiEmbedSubwindows = false;

    var result = fixture.Host.TryPresent(new AcceptDialog(), UIScreenHostTestSupport.Spec(UIScreenKinds.SaveLoad));

    AssertThat(result.Status).IsEqual(UIScreenOpenStatus.UnsupportedSubwindowMode);
    AssertThat(fixture.Host.ActiveEntries.Count).IsEqual(0);
    viewport.GuiEmbedSubwindows = incoming;
}
```

Also test enabled embedding returns `Opened` and stores the Window viewport as the focus viewport.

- [ ] **Step 4: Run the new suites and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostProcessModeTest|FullyQualifiedName~UIScreenHostSubwindowTest"
```

Expected: FAIL because scene, host, and adapters are absent.

- [ ] **Step 5: Create the host scene**

`UIScreenHost.tscn` must contain:

```text
UIScreenHost (Control, ProcessMode=Always, full rect)
├── HUDLayer (Control, ProcessMode=Pausable, full rect)
├── ScreenLayer (Control, ProcessMode=Always, full rect)
├── ModalLayer (Control, ProcessMode=Always, full rect)
├── ToastLayer (Control, ProcessMode=Always, full rect)
├── TransitionLayer (Control, ProcessMode=Always, full rect)
├── InputShield (Control, hidden, full rect, MouseFilter=Stop)
└── FocusSink (Control, visible, transparent, 1x1, MouseFilter=Ignore, FocusMode=All)
```

Attach `UIScreenHost.cs` to the root.

- [ ] **Step 6: Implement view adapter snapshots**

`UIScreenViewAdapter` stores:

```csharp
internal sealed class UIScreenViewAdapter
{
    public required Node View { get; init; }
    public required Func<bool> IsPresented { get; init; }
    public required Action<bool> SetPresented { get; init; }
    public required Action<bool> SetInteractive { get; init; }
    public required Func<Viewport> FocusViewport { get; init; }
    public required Node.ProcessModeEnum IncomingProcessMode { get; init; }
    public bool? IncomingControlVisible { get; init; }
    public bool? IncomingWindowGuiDisabled { get; init; }
    public bool? IncomingWindowUnfocusable { get; init; }
    public Func<Control?>? InitialFocus { get; init; }
    public Func<Control?>? RestoreFocus { get; init; }
    public Func<UIInputContext, UIInputInterception>? InterceptCancel { get; init; }
    public Action<UIScreenCloseReason>? Cleanup { get; init; }
    public UINodeLifetime NodeLifetime { get; init; }
}
```

Factory rules:

- unparented `Control`: attach to declared Control layer;
- Control already under that exact layer: accept;
- Control parented elsewhere: `InvalidControlParentage`;
- embedded `Window`: require `GetViewport().GuiEmbedSubwindows == true`, parent beneath host, focus viewport is Window;
- detached mode: `UnsupportedSubwindowMode`;
- apply `UIProcessPolicy` and snapshot incoming mode before any change;
- if process policy cannot meet the entry's paused/unpaused requirements, return `InvalidProcessPolicy` before stack mutation.

- [ ] **Step 7: Implement minimal host registration**

`UIScreenHost.TryPresent` order:

1. reject teardown/malformed scene;
2. validate node instance;
3. normalize spec;
4. build adapter without mutating stack;
5. call model `Open`;
6. attach/parent and apply process mode;
7. store adapter by handle token;
8. subscribe to `TreeExiting`;
9. recompute effective policy;
10. return `Opened`.

If steps 6–8 fail, roll back the model entry and restore the adapter snapshot before returning an error.

- [ ] **Step 8: Run process and subwindow tests**

Expected: PASS.

- [ ] **Step 9: Commit host scene and registration gate**

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
- Consumes: model input order, adapter interceptors, `CoreCancelActions`, `EntryCancelActions`, effective state.
- Produces: `TryHandleInput`, host `_Input`, one-event/one-result behavior, root fallback.

- [ ] **Step 1: Write failing core-action deduplication tests**

```csharp
[TestCase]
public async Task Input_EventMatchingPauseAndUiCancel_TraversesOnce()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this,
        coreActions: new[] { new StringName("pause_menu"), new StringName("ui_cancel") });
    int interceptions = 0;

    fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
    {
        InterceptCancel = _ =>
        {
            interceptions++;
            return UIInputInterception.ConsumeHere;
        }
    });

    var key = UIScreenHostTestSupport.KeyPress(Key.Escape);
    UIScreenHostTestSupport.BindSameEvent(key, "pause_menu", "ui_cancel");

    var result = fixture.Host.TryHandleInput(key);

    AssertThat(result).IsEqual(UIInputDispatchResult.Consumed);
    AssertThat(interceptions).IsEqual(1);
}
```

- [ ] **Step 2: Add entry-scoped toggle tests**

```csharp
[TestCase]
public async Task Input_InventoryToggle_OnlyAppliesToActiveInventoryEntry()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var settings = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Settings));
    var toggle = UIScreenHostTestSupport.ActionPress("toggle_inventory");

    AssertThat(fixture.Host.TryHandleInput(toggle)).IsEqual(UIInputDispatchResult.NoOwner);
    AssertThat(fixture.Host.IsActive(settings.Handle!.Value)).IsTrue();
}

[TestCase]
public async Task Input_InventoryToggle_ClosesActiveInventory()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var inventory = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Inventory) with
    {
        EntryCancelActions = new HashSet<StringName> { "toggle_inventory" },
        Cancel = UICancelPolicy.Close
    });

    AssertThat(fixture.Host.TryHandleInput(UIScreenHostTestSupport.ActionPress("toggle_inventory")))
        .IsEqual(UIInputDispatchResult.Consumed);
    AssertThat(fixture.Host.IsActive(inventory.Handle!.Value)).IsFalse();
}
```

- [ ] **Step 3: Add dynamic/static precedence and pass-through tests**

Test these exact outcomes:

- `ConsumeHere` consumes without close;
- `ReserveForNativeHandler` returns `ReservedForTopEntry`;
- `DeferToPolicy` then `Close` closes;
- static `None` continues to parent;
- OptionButton-style reservation leaves parent active;
- root fallback runs only for matched core action with no owner;
- entry-scoped action never invokes root fallback.

- [ ] **Step 4: Run and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostInputTest"
```

- [ ] **Step 5: Implement dispatcher matching and precedence**

Create `UIScreenInputDispatcher.Dispatch`:

```csharp
internal UIInputDispatchResult Dispatch(InputEvent inputEvent)
{
    var matchedCore = Match(inputEvent, _options.CoreCancelActions);
    _pruneInvalidEntries();

    if (_focus.IsRestorationPending && MatchesBarrier(inputEvent, matchedCore))
        return UIInputDispatchResult.Consumed;

    foreach (var entry in _model.InputOrder)
    {
        var matchedEntry = Match(inputEvent, entry.Policy.EntryCancelActions);
        if (matchedCore.Count == 0 && matchedEntry.Count == 0)
            continue;

        var context = new UIInputContext(inputEvent, matchedCore, matchedEntry, entry.Handle, _effectiveState());
        var dynamicResult = _adapters[entry.Handle.Token].InterceptCancel?.Invoke(context)
            ?? UIInputInterception.DeferToPolicy;

        var result = Resolve(dynamicResult, entry.Policy.Cancel, entry.Handle);
        if (result != UIInputDispatchResult.NoOwner)
            return result;
    }

    if (matchedCore.Count > 0 && _options.RootCancelFallback is not null)
    {
        return _options.RootCancelFallback(new(inputEvent, matchedCore, _effectiveState()))
            == UIRootCancelResult.Consumed
            ? UIInputDispatchResult.Consumed
            : UIInputDispatchResult.NoOwner;
    }

    return UIInputDispatchResult.NoOwner;
}
```

`Match` returns a set and never triggers more than one traversal. Do not add `ui_close_dialog` to core actions.

- [ ] **Step 6: Wire host `_Input`**

```csharp
public override void _Input(InputEvent inputEvent)
{
    if (_tearingDown)
        return;

    if (TryHandleInput(inputEvent) == UIInputDispatchResult.Consumed)
        GetViewport().SetInputAsHandled();
}
```

Do nothing for `ReservedForTopEntry` and `NoOwner`.

- [ ] **Step 7: Run input, stack, and resolver tests**

Expected: PASS.

- [ ] **Step 8: Commit Cancel dispatch**

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

- [ ] **Step 1: Write failing exact pause-restoration tests**

```csharp
[TestCase]
public async Task PauseLease_FromIncomingPaused_RestoresPaused()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    GetTree().Paused = true;

    var opened = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
    {
        PauseTree = true
    });
    fixture.Host.TryClose(opened.Handle!.Value, UIScreenCloseReason.Programmatic);

    AssertThat(GetTree().Paused).IsTrue();
}

[TestCase]
public async Task PauseLease_LastOwnerClose_RestoresExactlyOnce()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    GetTree().Paused = false;
    var parent = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
    {
        PauseTree = true
    });
    var child = fixture.Host.TryPresent(new Control(), UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
    {
        Parent = parent.Handle,
        PauseTree = false
    });

    fixture.Host.TryClose(child.Handle!.Value, UIScreenCloseReason.Programmatic);
    AssertThat(GetTree().Paused).IsTrue();
    fixture.Host.TryClose(parent.Handle!.Value, UIScreenCloseReason.Programmatic);
    AssertThat(GetTree().Paused).IsFalse();
}
```

- [ ] **Step 2: Add pause drift and exact cursor/HUD tests**

Test:

- external `GetTree().Paused = false` during active lease increments drift count and is reasserted;
- final close restores original incoming value, not the drift value;
- first cursor override snapshots exact `Input.MouseMode` and last override restores it;
- first HUD override snapshots exact `HudRoot.Visible` and last override restores it;
- `HudRoot == null` plus explicit HUD policy rejects registration before mutation;
- block callback fires only when effective block changes.

- [ ] **Step 3: Run lifecycle tests and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostLifecycleTest|FullyQualifiedName~UIScreenHostProcessModeTest"
```

- [ ] **Step 4: Implement state lease records**

Inside `UIScreenHost`, maintain dedicated records:

```csharp
private sealed record PauseLease(bool IncomingPaused);
private sealed record CursorLease(Input.MouseModeEnum IncomingMode);
private sealed record HudLease(bool IncomingVisible);

private PauseLease? _pauseLease;
private CursorLease? _cursorLease;
private HudLease? _hudLease;
private int _pauseOwnershipDriftCount;
```

Apply transitions only on effective boundary changes:

- no pause owners → first pause owner: capture and set true;
- pause owners remain: do not replace baseline;
- last pause owner closes: restore baseline and clear lease;
- explicit cursor/HUD override begins/ends using the same pattern.

- [ ] **Step 5: Implement drift detection**

In an Always-processing notification or `_Process`, while `_pauseLease != null`:

```csharp
if (!GetTree().Paused)
{
    _pauseOwnershipDriftCount++;
    GD.PushError("[UIScreenHost] SceneTree.Paused changed while host pause lease is active; reasserting host ownership.");
    GetTree().Paused = true;
}
```

Do not change `_pauseLease.IncomingPaused`.

- [ ] **Step 6: Publish effective state after consistent mutations**

Expose:

```csharp
public UIScreenEffectiveState CurrentState { get; private set; } =
    new(false, false, UICursorPolicy.Inherit, UIHudPolicy.Inherit, null, false);

public event Action<UIScreenEffectiveState>? EffectiveStateChanged;
```

Update state after adapter/lower-layer changes finish. Invoke `GameplayInputBlockChanged` only on boolean transition.

- [ ] **Step 7: Verify and commit state ownership**

Run Task 6 suites, then:

```bash
git add scripts/ui/hosting/UIScreenHost.cs \
  scripts/ui/hosting/UIScreenContracts.cs \
  tests/ui/hosting/UIScreenHostProcessModeTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: own UIScreenHost effective state"
```

---

### Task 7: Apply Compositional Control and Window Effects

**Files:**
- Modify: `scripts/ui/hosting/UIScreenViewAdapter.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Modify: `tests/ui/hosting/UIScreenHostSubwindowTest.cs`
- Modify: `tests/ui/hosting/UIScreenHostLifecycleTest.cs`

**Interfaces:**
- Consumes: resolver `LowerLayerEffects`.
- Produces: exact Control/Window baseline snapshots, hidden/inert application, and weakening/restoration behavior.

- [ ] **Step 1: Add failing nested-effect tests**

```csharp
[TestCase]
public async Task LowerLayerEffects_ChildClose_RestoresParentButKeepsGameplayInert()
{
    using var fixture = await UIScreenHostTestSupport.CreateHost(this);
    var gameplay = new Control { Visible = true };
    var pause = new Control { Visible = true };
    var settings = new Control { Visible = true };

    fixture.Host.TryPresent(gameplay, UIScreenHostTestSupport.Spec("gameplay") with
    {
        Layer = UIScreenLayer.Hud,
        InputPriority = UIInputPriority.Passive
    });
    var pauseOpen = fixture.Host.TryPresent(pause, UIScreenHostTestSupport.Spec(UIScreenKinds.Pause) with
    {
        LowerLayers = UILowerLayerPolicy.VisibleInert
    });
    var settingsOpen = fixture.Host.TryPresent(settings, UIScreenHostTestSupport.Spec(UIScreenKinds.Settings) with
    {
        Parent = pauseOpen.Handle,
        LowerLayers = UILowerLayerPolicy.Hidden
    });

    AssertThat(pause.Visible).IsFalse();
    fixture.Host.TryClose(settingsOpen.Handle!.Value, UIScreenCloseReason.Programmatic);

    AssertThat(pause.Visible).IsTrue();
    AssertThat(fixture.Host.Diagnostics.LowerLayerEffects[gameplay.GetInstanceId()])
        .IsEqual(UILowerLayerPolicy.VisibleInert);
}
```

- [ ] **Step 2: Add exact embedded-Window restoration tests**

For an embedded `AcceptDialog`, set incoming `GuiDisableInput=true` or `Unfocusable=true`, apply and remove another effect, and assert exact incoming values return. Also test hidden restoration uses supplied `SetPresented(true)` callback rather than plain `Show()` when provided.

- [ ] **Step 3: Run and verify failures**

Run lifecycle and subwindow suites.

- [ ] **Step 4: Implement per-target effect leases**

Maintain one adapter-side baseline while any owner affects the target:

```csharp
internal sealed record UIControlEffectBaseline(bool Visible, bool ProcessInputEnabled);
internal sealed record UIWindowEffectBaseline(bool Visible, bool GuiDisableInput, bool Unfocusable);
```

When reduction changes:

- interactive → inert/hidden: capture baseline once;
- hidden → inert: show/restore presentation first, keep baseline, then apply inert;
- inert → hidden: keep baseline and hide;
- effect → interactive: restore baseline and clear lease.

Do not overwrite baselines when a second owner contributes.

- [ ] **Step 5: Implement Control shield and interactivity callback**

Use `InputShield` only for Control-layer pointer blocking. For lower Controls with direct `_Input`, invoke their `SetInteractive(false)` before publishing the effective state. Restore exact callback state when the reduction ends.

- [ ] **Step 6: Implement embedded-Window effects**

For Window targets:

- `Hidden`: call registered presentation adapter;
- `VisibleInert`: set `GuiDisableInput=true` and `Unfocusable=true`;
- never claim the Control shield covers Window input;
- reject owner open before stack mutation when a required Window effect cannot be safely represented.

- [ ] **Step 7: Verify and commit lower-layer adapters**

```bash
git add scripts/ui/hosting/UIScreenViewAdapter.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostSubwindowTest.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: apply UIScreenHost layer effects"
```

---

### Task 8: Implement Focus Coordination and Guaranteed Restoration Leases

**Files:**
- Create: `scripts/ui/hosting/UIScreenFocusCoordinator.cs`
- Modify: `scripts/ui/hosting/UIScreenHost.cs`
- Create: `tests/ui/hosting/UIScreenHostFocusTest.cs`

**Interfaces:**
- Consumes: view adapters, host `FocusSink`, handle tokens, close transactions.
- Produces: initial focus, root and per-Window sinks, focus records, generation-tagged restoration lease, and barrier status.

- [ ] **Step 1: Write failing initial-focus and sink tests**

```csharp
[TestCase]
public async Task BlockingControlWithoutFocusableChild_UsesVisibleRootSink()
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

Add a native Window case that verifies a transparent-but-visible, `MouseFilter.Ignore`, `FocusMode.All` sink is created inside the Window viewport.

- [ ] **Step 2: Write failing restoration-order tests**

Test this order:

1. explicit restore target;
2. captured parent focus owner in captured viewport;
3. parent initial target;
4. first focusable descendant;
5. sink;
6. release focus.

Also verify initial-focus deferral has no Cancel barrier and stale initial-focus callbacks no-op after close.

- [ ] **Step 3: Write failing lease-release tests**

Named tests:

- valid target completes lease;
- target freed before callback completes lease;
- host teardown completes lease synchronously;
- re-entrant close supersedes prior generation;
- stale callback cannot clear newer lease;
- duplicate close creates one lease;
- next core Cancel works after every invalidation path.

- [ ] **Step 4: Run and verify failures**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostFocusTest"
```

- [ ] **Step 5: Implement focus records and initial acquisition**

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

On open:

- capture active parent's registered viewport and current focus owner before applying child visibility;
- after view is ready/presented/interactable, defer `InitialFocus`, first focusable descendant, then appropriate sink;
- deferred callback checks handle token is still active.

- [ ] **Step 6: Implement guaranteed-release restoration**

`BeginRestoration` increments generation and stores a lease. The deferred callable must use `try/finally`:

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

Before a superseding lease starts, complete or invalidate the older lease without allowing its callback to clear the new generation. On teardown, disable dispatch, clear scheduled callbacks, and clear the active lease synchronously.

- [ ] **Step 7: Wire barrier state into effective state and dispatcher**

`CurrentState.IsFocusRestorationPending` derives from coordinator lease presence. The input dispatcher consumes matching core/top-entry actions only while a live lease exists.

- [ ] **Step 8: Run focus and input suites together**

Expected: PASS.

- [ ] **Step 9: Commit focus lifecycle**

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
- Consumes: stack close mutations, adapter registry, focus coordinator, effective state leases.
- Produces: guarded mutation queue, exact once-only cleanup, invalid-node pruning, teardown behavior, and read-only diagnostics.

- [ ] **Step 1: Add failing external-deletion tests**

Test:

- externally freed parent closes descendants first;
- cleanup callback runs at most once;
- invalid Godot object is never dereferenced;
- policy and pause restore after pruning;
- focus lease completes even with no target;
- stale handle returns `AlreadyClosed` after prune.

- [ ] **Step 2: Add failing re-entrant mutation tests**

```csharp
[TestCase]
public async Task CleanupCallback_ReentrantClose_IsQueuedAfterCurrentMutation()
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

Also test opens during teardown return `HostTearingDown`, duplicate queued closes collapse, and effective state emits only after the complete transaction.

- [ ] **Step 3: Add diagnostics tests**

Diagnostics must expose read-only snapshots for:

- active handles/order;
- kind/parent/layer/priority/process policy;
- normalized group and incompatibilities;
- effective state;
- lower-layer contributors/effect;
- core and entry action ownership;
- focus viewport/control/sink/lease generation;
- process and embedded-subwindow validation;
- active state leases;
- pause drift count.

Mutation of returned collections must be impossible by type or must not affect host internals.

- [ ] **Step 4: Implement the mutation queue**

Use one queue of operations and one `_isMutating` guard:

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

Mark handles closing before callbacks. Collapse duplicate close tokens. Publish policy only after each complete operation.

- [ ] **Step 5: Implement node-exit pruning and teardown**

On registered `TreeExiting`:

- ignore exits initiated by an already-running host close;
- close descendants and model entry with `NodeFreed`;
- skip live-object operations after validity fails;
- invoke managed cleanup once;
- recalculate lower layers/effective state;
- begin or complete focus restoration.

On host `_ExitTree`:

- set `_tearingDown=true` and disable input;
- close topmost-first with `HostTeardown`;
- reject callback-driven opens;
- complete restoration lease;
- restore all global/adapter snapshots once;
- unsubscribe node events and remove dynamic sinks.

- [ ] **Step 6: Add immutable diagnostics record**

Define `UIScreenHostDiagnostics` in contracts, returning arrays or read-only dictionaries copied from internal state. Do not expose adapters, delegates, or mutable model lists.

- [ ] **Step 7: Run lifecycle, focus, input, process, and subwindow suites**

Expected: PASS.

- [ ] **Step 8: Commit lifecycle hardening**

```bash
git add scripts/ui/hosting/UIScreenContracts.cs \
  scripts/ui/hosting/UIScreenHost.cs \
  tests/ui/hosting/UIScreenHostLifecycleTest.cs
git commit -m "feat: harden UIScreenHost lifecycle"
```

---

### Task 10: Prove HPA-376 Contract Scenarios and Publish the Integration Contract

**Files:**
- Create: `tests/ui/hosting/UIScreenHostContractScenarioTest.cs`
- Create: `docs/ui/hpa-378/uiscreenhost-contract.md`
- Modify: `docs/superpowers/specs/2026-07-30-reusable-ui-screen-host-design.md` only to change `Status` from `Proposed design` to `Approved design` after all focused and full verification succeeds.

**Interfaces:**
- Consumes: complete host public surface.
- Produces: named synthetic evidence for HPA-376/HPA-378 acceptance and the HPA-379 adapter contract.

- [ ] **Step 1: Add the synthetic contract scenario suite**

Create named tests for:

```csharp
[TestCase]
public async Task InventoryChildOfPause_PausesWorldHidesHudAndReturnsToPause() { }

[TestCase]
public async Task SettingsChildOfPause_PreservesPauseGameplayInertContribution() { }

[TestCase]
public async Task DestructiveConfirmation_CancelReturnsWithoutDestructiveCallback() { }

[TestCase]
public async Task RewardToast_IsPassiveAndNeverBecomesInputOwner() { }

[TestCase]
public async Task RequiredRewardAcknowledgement_ConsumesCancelUntilContinue() { }

[TestCase]
public async Task BattlePresentation_RemainsTopmostAfterSyntheticDomainFlagClears() { }

[TestCase]
public async Task DomainBlockAndPresentationBlock_EitherSourceSuppressesGameplayPredicate() { }
```

Use synthetic Controls/AcceptDialogs and callbacks. Do not instantiate production Inventory, Pause, Battle, or Game scenes in HPA-378.

- [ ] **Step 2: Run the scenario suite and fix only host-contract defects**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreenHostContractScenarioTest"
```

Expected: PASS. Any failure requiring `Game.cs`, floor, or production-screen edits is an HPA-379 concern; document the dependency rather than expanding HPA-378 scope.

- [ ] **Step 3: Write the public integration contract**

`docs/ui/hpa-378/uiscreenhost-contract.md` must include:

1. scene composition and exact node paths;
2. `TryPresent`, `TryClose`, `TryHandleInput`, `CurrentState`, diagnostics, and events;
3. every public enum/status with meaning;
4. Control adapter defaults;
5. embedded Window adapter defaults and embedding precondition;
6. process-policy matrix;
7. core versus entry-scoped action examples;
8. dynamic interception precedence;
9. parent-child and exclusive-group examples;
10. lower-layer reduction examples;
11. pause/cursor/HUD/process restoration guarantees;
12. focus and restoration-lease guarantees;
13. teardown and invalid-node behavior;
14. HPA-379 checklist, including GridMap/HUD process audit and composed gameplay-block predicate;
15. explicit out-of-scope items.

Include compilable registration examples for Pause, Inventory, Settings, embedded Save/Load, Battle lifetime, reward toast, and required acknowledgement using synthetic callbacks—not production integration code.

- [ ] **Step 4: Run every focused host suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore \
  --filter "FullyQualifiedName~UIScreen"
```

Expected: all host tests pass with zero failures and zero skips.

- [ ] **Step 5: Run build and full solution verification**

```bash
dotnet build Sirius.sln --no-restore
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected:

- build exit code 0;
- full test exit code 0;
- no new orphan-warning signature relative to the current branch baseline.

Capture the exact pass count and orphan signature in the implementation PR body; do not copy an older count.

- [ ] **Step 6: Perform plan/spec completion review**

Verify line by line:

- no production flow was partially migrated;
- no `Game.cs`, MainMenu, floor, or `project.godot` diff exists;
- no placeholder text or unimplemented method remains;
- public signatures match this plan and the design;
- passive validation, group normalization, embedded-window rejection, pause drift, lower-layer composition, focus lease release, and teardown all have named tests;
- documentation matches actual names and status codes.

- [ ] **Step 7: Mark the design approved and commit final contract evidence**

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

- [ ] **Step 8: Update PR validation evidence**

Update the implementation PR with:

- exact focused and full test counts;
- build result;
- orphan-warning comparison;
- confirmation that no production flow was integrated;
- explicit HPA-379 prerequisites: embedded subwindow pinning, GridMap runtime process correction, Pausable HUD composition, and unified gameplay-block predicate.
