# HPA-570 Hosted Shop and Healing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `ShopDialog` and `HealDialog` windows with scene-authored, host-managed Sirius Shop and Healing surfaces while preserving shipped Buy/Sell/Heal behavior and exactly-once NPC lifecycle.

**Architecture:** Keep `NpcInteractionController` as the single Dialogue → Shop/Heal orchestration owner. Build two concrete controllers because Shop is a repeatable Buy/Sell catalogue while Healing is a one-shot confirmation. Reuse `SiriusModalShell`, `SiriusUiMetrics`, Sirius theme variations, `UIScreenHost`, and existing `UIScreenKinds.Shop` / `UIScreenKinds.Heal`. Keep current Character/catalog APIs as the mutation boundary; add no transaction service, presenter, generic row type, or host facade.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing Sirius Theme, `SiriusModalShell`, `SiriusUiMetrics`, and `UIScreenHost`.

## Global constraints

- Preserve Buy: `Item.Value`, affordability recheck, item add, gold rollback when inventory is full, stat notification, and list refresh.
- Preserve Sell: one item per activation at `max(1, floor(Item.Value * 0.5))`, removal before gold grant, stat notification, and `Nothing to sell.`.
- Treat HPA-570's “Selling” non-goal as “no new selling mechanics”; current production and HPA-373 require Sell parity.
- Preserve the Shop two-second latest-message-wins **transient** feedback timer.
- Keep standing disabled reasons separate from transient Shop feedback.
- Healing has **no** feedback timer; keep its unavailable/validation message standing until state changes.
- Preserve Heal: configured NPC cost, current/effective-max HP check, affordability check, full restore, stat notification, complete/cancel exactly once.
- Add only controller-local `_operationInFlight` guards for synchronous callbacks.
- Reuse `SiriusModalShell`; do not add another shell/frame abstraction.
- **Shop uses `SiriusModalSizeClass.Large`; Healing uses `Small`; neither uses Dialogue's `Full` bottom band.**
- Reuse `UIScreenKinds.Shop` and `UIScreenKinds.Heal`; do not add host kinds, `BlockingPrompt`, parent handles, incompatible-kind rules, or host APIs.
- Shop/Heal host policy is explicit: `PauseTree = false`, `BlockGameplayInput = true`, cursor visible, **HUD hidden per HPA-373 §7.3**, lower layers visible/inert, Cancel consumed/intercepted, node lifetime QueueFree.
- Do not change Dialogue's current `Hud = Visible` policy in this ticket; HPA-570 does not silently copy that known divergence either.
- One scene per screen. Compact behavior is responsive presentation, not a second controller/scene.
- Dynamic Shop rows stay controller-local; do not add a row component until another real consumer exists.
- Focus restoration uses only active tab + stable item id; do not copy Inventory's pending-focus record machinery.
- No stock mechanics, quantity picker, buyback, party/status heal, new pricing, or new domain rules.
- Delete native dialogs after hosted parity is green; keep no compatibility wrapper.

---

## File structure

### Create

- `scenes/ui/ShopScreen.tscn`
- `scripts/ui/ShopScreenController.cs`
- `tests/ui/ShopScreenControllerTest.cs`
- `scenes/ui/HealingScreen.tscn`
- `scripts/ui/HealingScreenController.cs`
- `tests/ui/HealingScreenControllerTest.cs`

### Modify

- `scripts/ui/NpcInteractionController.cs`
- `scripts/game/Game.cs`
- `tests/ui/NpcInteractionControllerTest.cs`
- `tests/game/GameplayPauseHostTest.cs`
- `tests/game/GameInputLifecycleTest.cs`
- `tests/data/npc/ShopPricingTest.cs`
- `docs/ui/hpa-376/ui-lifecycle-contract.md`

### Delete after replacement coverage is green

- `scripts/ui/ShopDialog.cs`
- `tests/ui/ShopDialogTest.cs`
- `scripts/ui/HealDialog.cs`
- `tests/ui/HealDialogTest.cs`

### Audit only unless a focused failure proves a production change is required

- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/data/npc/NpcData.cs`
- Shop/catalogue data types
- `Character` inventory/gold/health APIs

---

## Risk checklist

### Wrong sibling geometry gets copied

Shop is a centred `Large` service surface. Healing is a centred `Small` service surface. Dialogue's `Full` lower-45% band is not a reusable default.

### Standing disabled reason gets erased by transient feedback

Shop row affordability is standing per-row text. `%FeedbackLabel` is timed operation feedback only. Healing uses one non-timed standing feedback/reason label and no timer.

### `TryPresent` can return `Opened` after synchronous post-commit close

Supply cleanup, then call `IsActive(handle)` before retaining the screen/handle. Never keep stale host state.

### Host close publication can throw after cleanup already committed

Preserve current Dialogue clear/close/finish ordering. Terminal orchestration still reaches exactly-once `InteractionComplete` after publication failure.

### Rebuilding Shop rows can destroy the focused node

Capture active tab + item id before mutation. Restore by semantic identity after rebuild; otherwise next valid action, tab, then Close. Never retain a queued `Control` as the restoration key.

---

## Task 1: Build the authored Shop screen with transaction parity

**Files:**
- Create: `scenes/ui/ShopScreen.tscn`
- Create: `scripts/ui/ShopScreenController.cs`
- Create: `tests/ui/ShopScreenControllerTest.cs`
- Reference only: `scripts/ui/ShopDialog.cs`
- Reference only: `tests/ui/ShopDialogTest.cs`

**Controller contract:**

```csharp
public partial class ShopScreenController : Control
{
    [Signal] public delegate void ShopClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenShop(ShopInventory shop, Character player);
    public void RequestCancel();
}
```

The screen is single-start like `DialogueScreenController`. `TryOpenShop(...)` may run before `_Ready()` and stores validated state only; `_Ready()` binds authored nodes and renders it.

- [ ] **Step 1: Write RED scene/configuration tests**

Add tests that:

- load `res://scenes/ui/ShopScreen.tscn`
- instantiate `ShopScreenController`
- call `TryOpenShop(...)` before `AddChild(...)`
- reject a second start
- expose a non-null initial focus target
- assert `%SafeFrame` + `%ModalShell` exist
- assert the scene contains no `AcceptDialog`
- assert `%ModalShell.SizeClass == SiriusModalSizeClass.Large`
- assert standard layout is centred in the full safe frame, not Dialogue's bottom band

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest"
```

Expected: FAIL because the scene/controller do not exist.

- [ ] **Step 2: Author the stable Shop tree**

Create one full-viewport root with:

```text
ShopScreen
└── SafeFrame (%SafeFrame)
    └── ModalShell (%ModalShell, SizeClass = Large)
        ├── .../BodyHost
        │   ├── GoldLabel (%GoldLabel)
        │   ├── FeedbackLabel (%FeedbackLabel)
        │   └── ShopTabs (%ShopTabs)
        │       ├── Buy page → BuyList (%BuyList)
        │       └── Sell page → SellList (%SellList)
        └── .../ActionsHost
            └── CloseButton (%CloseButton)
```

Use the shell-owned body scroll as the single overflow owner. Do not author fixed item rows or nested per-tab scroll containers unless a focused runtime failure requires them.

- [ ] **Step 3: Bind the centred responsive layout**

Follow Inventory's safe-frame placement, not Dialogue's band:

```csharp
var size = GetViewportRect().Size;
var insets = SiriusUiMetrics.SafeFrameInsets(size);

_safeFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
_safeFrame.OffsetLeft = insets.SideInset;
_safeFrame.OffsetTop = insets.Margin;
_safeFrame.OffsetRight = -insets.SideInset;
_safeFrame.OffsetBottom = -insets.Margin;

_shell.Compact = insets.Compact;
_shell.RefreshPresentation(size);
```

Passing the full viewport size is deliberate for non-Full shells: the SafeFrame owns placement while `SiriusModalShell` owns its existing standard 90%/compact 12 px width/height margin policy once. Do not pass an already-inset compact width and subtract another shell margin.

On `_Ready()` also bind nodes, connect Close, subscribe `Resized`, and render stored state.

- [ ] **Step 4: Port Buy with separate standing reason and transient feedback**

Write RED tests for:

1. successful Buy deducts `Item.Value`, adds one item, and refreshes gold/lists
2. insufficient gold disables Buy and shows standing per-row text `Not enough gold!`
3. standing affordability remains visible after any unrelated transient feedback timeout
4. inventory-full Buy rolls spent gold back and shows transient `Inventory full!`
5. callback revalidation after state changed can show transient `Not enough gold!`
6. missing catalogue item is skipped safely

Each runtime Buy row should keep its own reason label (or equivalent non-timed row-local text). `%FeedbackLabel` must not own standing affordability.

Port the existing mutation order directly; add no service.

- [ ] **Step 5: Port Sell parity**

Add tests proving:

- price = `Mathf.Max(1, Mathf.FloorToInt(item.Value * 0.5f))`
- one activation removes one item before granting gold
- selling the last item immediately renders `Nothing to sell.`
- failed removal grants no gold, shows transient `Item no longer available.`, and refreshes

Do not add quantity selection, buyback, or a pricing abstraction.

- [ ] **Step 6: Preserve Shop transient timer semantics**

Port `ShowFeedback_KeepsLatestMessageVisible_UntilLatestTimerExpires` to `ShopScreenController`.

Keep one timer + handler. New transient feedback unsubscribes the prior timeout before creating the next two-second timer. Cancel it on close and `_ExitTree()`.

This timer may hide only `%FeedbackLabel`; it must never clear row-local standing reasons.

- [ ] **Step 7: Add local re-entrancy and terminal guards**

Use only:

```csharp
private bool _operationInFlight;
private bool _terminalEmitted;
```

Buy/Sell callbacks return when `_operationInFlight`. Set/reset it in `try/finally` around synchronous mutation/refresh. `RequestCancel()` emits `ShopClosed` once and cancels transient feedback.

Test re-entrant invocation and double `RequestCancel()`.

- [ ] **Step 8: Preserve semantic focus across rebuild**

Before Buy/Sell mutation capture:

- active Buy/Sell tab
- item id if focus is on a row action

After rebuild:

1. same item action when still present
2. otherwise next valid action on active page
3. otherwise active tab
4. otherwise Close

Test selling the focused last item and a Buy refresh that changes enabled states. Assert focus does not remain on a queued/freed node.

- [ ] **Step 9: Run Shop GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~ShopPricingTest"
```

Expected: new tests PASS and legacy tests remain green before deletion.

---

## Task 2: Build the authored Healing screen with one-shot parity

**Files:**
- Create: `scenes/ui/HealingScreen.tscn`
- Create: `scripts/ui/HealingScreenController.cs`
- Create: `tests/ui/HealingScreenControllerTest.cs`
- Reference only: `scripts/ui/HealDialog.cs`
- Reference only: `tests/ui/HealDialogTest.cs`

**Controller contract:**

```csharp
public partial class HealingScreenController : Control
{
    [Signal] public delegate void HealCompleteEventHandler();
    [Signal] public delegate void HealCancelledEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenHeal(NpcData npc, Character player);
    public void RequestCancel();
}
```

- [ ] **Step 1: Write RED scene/configuration tests**

Cover:

- pre-ready `TryOpenHeal(...)`
- second start rejected
- no `AcceptDialog`
- `%SafeFrame` + `%ModalShell`
- `%ModalShell.SizeClass == SiriusModalSizeClass.Small`
- `%HealButton` and `%CancelButton` are under shell `ActionsHost`
- centred standard placement, not Dialogue's bottom band
- initial focus Heal when enabled, No Thanks otherwise

- [ ] **Step 2: Author the Prompt-shaped small Healing scene**

Create:

```text
HealingScreen
└── SafeFrame (%SafeFrame)
    └── ModalShell (%ModalShell, SizeClass = Small)
        ├── .../BodyHost
        │   ├── HealthLabel (%HealthLabel)
        │   ├── CostLabel (%CostLabel)
        │   ├── GoldLabel (%GoldLabel)
        │   └── FeedbackLabel (%FeedbackLabel)
        └── .../ActionsHost
            ├── CancelButton (%CancelButton, "No Thanks")
            └── HealButton (%HealButton)
```

Reuse the stable two-action `ActionsHost` composition from `SiriusPrompt.tscn`, not the `SiriusPrompt` controller/type.

- [ ] **Step 3: Bind the same centred layout**

Use the same SafeFrame algorithm as Shop and call `_shell.RefreshPresentation(size)` with full viewport size. Author `Small` explicitly; `SiriusModalShell` defaults to Medium and relying on that default is a regression.

- [ ] **Step 4: Port standing availability presentation**

Render current/effective-max HP, cost, and gold. `%FeedbackLabel` is non-timed standing state:

- full HP → `You are already at full health.`
- insufficient gold → `Not enough gold!`
- otherwise clear it

Disable Heal for either unavailable state. Set `InitialFocusTarget` to Heal when enabled, else No Thanks.

Do not create a timer for Healing. Preserve the current warning for non-positive `HealCost` without inventing a new rule.

- [ ] **Step 5: Port Heal mutation and exactly-once terminal behavior**

Add tests proving:

- successful Heal deducts configured cost
- HP becomes `GetEffectiveMaxHealth()`
- successful Heal emits `HealComplete` once
- Heal then Cancel emits no cancellation
- RequestCancel twice emits `HealCancelled` once
- programmatic activation while full/poor cannot mutate HP/gold and leaves the standing current reason
- no `SceneTreeTimer` is created

Keep `GameManager.Instance?.NotifyPlayerStatsChanged()`.

- [ ] **Step 6: Add local in-flight guard**

Guard Heal with `_operationInFlight` plus terminal latch. Reset in-flight only when the screen remains non-terminal.

- [ ] **Step 7: Run Healing GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~HealDialogTest"
```

Expected: new and legacy behavior suites PASS before cutover.

---

## Task 3: Cut `NpcInteractionController` over to hosted Shop and Heal

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`

**Target state:** `NpcInteractionController` owns Dialogue, Shop, or Heal hosted screen/handle state, with one active in the normal NPC flow.

- [ ] **Step 1: Replace the two native-transition tests with RED hosted expectations**

Replace these current tests, not merely add alongside them:

- `ShopOutcome_ClosesHostedDialogueBeforeNativeShopOpens`
- `HealOutcome_ClosesHostedDialogueBeforeNativeHealOpens`

New assertions:

- Dialogue entry is gone before Shop/Heal is active
- exactly one `UIScreenKinds.Shop` or `UIScreenKinds.Heal` entry exists
- no native `ShopDialog`/`HealDialog` child is created
- cancel closes the hosted entry and completes once

- [ ] **Step 2: Replace native fields with explicit hosted state**

Replace native dialog fields with nullable pairs for:

- `ShopScreenController` + `UIScreenHandle?`
- `HealingScreenController` + `UIScreenHandle?`

Keep explicit methods:

- `OpenShop`, `ClearShopPresentation`, `CloseShopPresentation`
- `OpenHeal`, `ClearHealPresentation`, `CloseHealPresentation`

Do not extract a shared hosted-surface helper in advance.

- [ ] **Step 3: Host Shop with the full HPA-569 lifecycle contract**

`OpenShop()` order:

1. resolve `ShopCatalog.GetById(_npc.ShopId)`; invalid data logs then `Finish()`
2. load/instantiate `ShopScreen.tscn`
3. call `TryOpenShop(shopInventory, _player)` before presentation
4. subscribe `ShopClosed`
5. call `TryPresent` inside `try`
6. on thrown post-commit publication: unsubscribe/free candidate when valid, call `Finish()`, then preserve current exception behavior
7. on rejected/no-handle result: unsubscribe/free candidate and `Finish()`
8. on `Opened`, call `_screenHost.IsActive(handle)` before retaining state
9. if already inactive: `Finish()` and return
10. otherwise store screen + handle

Use this exact policy:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Shop,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
    ExclusiveGroup = UIScreenExclusiveGroups.None,
    PauseTree = false,
    BlockGameplayInput = true,
    Cursor = UICursorPolicy.Visible,
    Hud = UIHudPolicy.Hidden,
    LowerLayers = UILowerLayerPolicy.VisibleInert,
    Cancel = UICancelPolicy.Consume,
    InitialFocus = () => screen.InitialFocusTarget,
    InterceptCancel = _ =>
    {
        screen.RequestCancel();
        return UIInputInterception.ConsumeHere;
    },
    Cleanup = _ => ClearShopPresentation(screen),
    NodeLifetime = UINodeLifetime.QueueFree
}
```

`Hud = Hidden` follows HPA-373 §7.3. Current hosted Dialogue uses Visible; do not change Dialogue here and do not copy that value into Shop.

- [ ] **Step 4: Host Healing with the same explicit policy values**

Use:

- `Kind = UIScreenKinds.Heal`
- `Hud = UIHudPolicy.Hidden`
- `Cleanup = _ => ClearHealPresentation(screen)`
- initial focus from `HealingScreenController.InitialFocusTarget`
- Cancel intercepted to `RequestCancel()`
- no prompt exclusive group

Wire both `HealComplete` and `HealCancelled` to terminal handling.

- [ ] **Step 5: Preserve clear/close/finish ordering explicitly**

For each hosted surface:

- cleanup callback unsubscribes signals and clears matching screen/handle state
- close method calls `TryClose`; stale handle clears local matching state
- terminal handler calls close inside `try/catch`; a publication exception after cleanup must not skip `Finish()`
- `Finish()` remains idempotent by setting `_finished = true` before cleanup, wrapping hosted close cleanup, and invoking `InteractionComplete` once even if publication throws

Do not replace this with vague “mirror lifecycle” comments or a new host facade.

- [ ] **Step 6: Extend error/teardown coverage**

Add tests for both relevant surfaces where practical:

- `Finish()` while Shop active closes Shop and completes once
- `Finish()` while Heal active closes Heal and completes once
- invalid Shop id opens no Shop and completes once
- host rejection leaks no candidate
- synchronous `EffectiveStateChanged` close after commit leaves no stale handle
- close publication exception still completes exactly once

Reuse current Dialogue test technique; do not create a fake host.

- [ ] **Step 7: Remove legacy UI-parent dependency**

Once Shop/Heal no longer `AddChild` to `_uiParent`, delete that constructor/state parameter and update the sole `Game` caller plus test helpers in the same commit.

- [ ] **Step 8: Run orchestration GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: PASS with no native Shop/Heal children and no stale hosted entries.

---

## Task 4: Replace native-phase production tests and delete native dialogs

**Files:**
- Delete: `scripts/ui/ShopDialog.cs`
- Delete: `tests/ui/ShopDialogTest.cs`
- Delete: `scripts/ui/HealDialog.cs`
- Delete: `tests/ui/HealDialogTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`
- Modify: `tests/data/npc/ShopPricingTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Add real-route host policy/input-block tests before deleting native types**

Model the production path on `ConfiguredKeyboardCancel_ClosesHostedDialogueThroughRealRoute` rather than flag-only setup.

Add real-route Shop and Healing cases that actually:

1. instantiate `Game.tscn`
2. invoke the relevant authored NPC interaction
3. choose the Dialogue outcome that opens Shop/Heal
4. assert the hosted entry is active
5. assert `BlockGameplayInput = true`, `PauseTree = false`, `Hud = Hidden`, cursor visible, Cancel consumed, tree not paused
6. press configured keyboard Cancel and prove the hosted entry closes, Pause does not open, and `IsInNpcInteraction` clears once
7. add controller/gamepad Cancel coverage at least once across Shop/Healing to preserve device routing

Delete or rewrite `ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler`; once HPA-570 lands, it documents a production phase that no longer exists.

- [ ] **Step 2: Replace native-transition test names/assumptions everywhere**

Search for and rewrite assertions/comments describing:

```text
NativeShop
NativeHeal
native handler
ShopDialog
HealDialog
LegacyNpcUiParent
```

Do not leave a test whose name claims a native phase after the classes are removed.

- [ ] **Step 3: Retarget the pricing oracle comment**

In `tests/data/npc/ShopPricingTest.cs`:

- update summary/helper comments from “used by ShopDialog” / “mirrors ShopDialog” to `ShopScreenController`
- keep the existing local formula helper
- do **not** add a `ShopPricing` production type solely for the test

- [ ] **Step 4: Delete native classes/tests**

Only after Tasks 1–3 and the production-route host tests are green, delete:

- `ShopDialog.cs`
- `ShopDialogTest.cs`
- `HealDialog.cs`
- `HealDialogTest.cs`

- [ ] **Step 5: Update lifecycle documentation**

In `docs/ui/hpa-376/ui-lifecycle-contract.md`, document Shop/Heal as hosted modal entries:

- kinds Shop / Heal
- `PauseTree = false`
- gameplay input blocked
- HUD hidden
- cursor visible
- lower layers visible/inert
- Cancel intercepted/consumed
- QueueFree node lifetime
- `NpcInteractionController` orchestration owner

Remove native-dialog wording.

- [ ] **Step 6: Run migration blast radius**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest|FullyQualifiedName~ShopPricingTest"
```

Expected: PASS.

---

## Task 5: Responsive/input verification and final regression pass

**Files:**
- Modify only new scenes/controllers/tests or directly affected lifecycle tests if focused failures prove a fix is required.

- [ ] **Step 1: Pin representative viewport geometry**

Exercise at least:

- 640×360 compact
- 1280×720 reference
- 1920×1080 standard

Assert:

- Shop shell is Large in standard mode and centred inside SafeFrame
- Healing shell is Small in standard mode and centred inside SafeFrame
- neither uses the Dialogue bottom band or Full size class
- compact shell remains inside the 12 px safe frame without double-subtracted margins
- controls remain reachable and the shell body owns overflow

- [ ] **Step 2: Verify focus behavior**

Cover:

- Shop initial focus reaches a valid action
- Healing initial focus is Heal when enabled, otherwise No Thanks
- Buy/Sell tab switching retains a usable focus target
- Shop rebuild never leaves focus on queued/freed controls
- keyboard/gamepad host Cancel closes both screens without Pause fallthrough

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS with no production `ShopDialog`/`HealDialog` references.

- [ ] **Step 4: Run full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS.

- [ ] **Step 5: Inspect final diff for scope creep**

Expected implementation scope:

- two authored scenes/controllers + tests
- `NpcInteractionController` / `Game` host cutover
- native Shop/Heal deletion
- affected production host/input tests
- ShopPricing comment retarget
- lifecycle documentation

Reject transaction services, base controllers, row components, new host kinds/groups, new theme tokens, new shop/healing rules, or unrelated HPA-571/HPA-573/HPA-625 work.

---

## Done criteria

HPA-570 is implementation-complete when:

- Shop is a centred authored `Large` Sirius surface, not a desktop dialog or Dialogue bottom band.
- Healing is a centred authored `Small` Sirius surface with actions in shell `ActionsHost`.
- `NpcInteractionController` presents both through existing Shop/Heal host kinds with the explicit HPA-373 lifecycle policy.
- current Buy **and Sell** behavior is preserved without new transaction mechanics.
- current Healing behavior is preserved without new rules or a new feedback timer.
- disabled Buy/Heal actions have standing readable reasons that do not disappear with transient Shop feedback.
- local re-entrant activation is guarded.
- mouse/keyboard/gamepad focus remains usable after dynamic Shop refresh.
- real-route Cancel/input-block tests replace the retired native-phase test.
- every close/success/failure/teardown path restores gameplay and completes NPC interaction exactly once.
- native Shop/Heal classes/tests are deleted and `ShopPricingTest` no longer names the deleted class.
- focused migration tests, build, and full suite are green.
