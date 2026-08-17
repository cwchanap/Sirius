# HPA-570 Hosted Shop and Healing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace native `ShopDialog` and `HealDialog` windows with scene-authored, host-managed Sirius Shop and Healing surfaces while preserving shipped Buy/Sell/Heal behavior and exactly-once NPC lifecycle.

**Architecture:** Keep `NpcInteractionController` as the Dialogue → Shop/Heal orchestration owner. Build two concrete screen controllers. Reuse centred non-Full `SiriusModalShell` presentation, existing Shop/Heal host kinds, Character/catalog mutation APIs, and one small private presentation-protocol helper inside `NpcInteractionController`. Add no transaction framework, generic screen base, host facade/type, or reusable Shop row component.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, Sirius Theme, `SiriusModalShell`, `SiriusUiMetrics`, `UIScreenHost`.

## Global constraints

- Preserve Buy: `Item.Value`, affordability recheck, item add, gold rollback when inventory is full, stat notification, and list refresh.
- Preserve Sell: one item per activation at `max(1, floor(Item.Value * 0.5))`, removal before gold grant, stat notification, and `Nothing to sell.`.
- Treat the ticket's old “Selling” non-goal as **no new selling mechanics**; current production and HPA-373 require Sell parity.
- Keep one production Sell-price definition: `ShopScreenController.SellPrice(int)`; `ShopPricingTest` calls it directly.
- Preserve Shop's two-second latest-message-wins **transient** feedback timer.
- Keep standing disabled reasons separate from transient Shop feedback.
- Healing has **no** feedback timer.
- Preserve Heal: configured NPC cost, current/effective-max HP check, affordability check, full restore, stat notification, complete/cancel exactly once.
- Shop uses `_operationInFlight`; Healing uses only its terminal latch for duplicate activation.
- Reuse `SiriusModalShell`; Shop = `Large`, Healing = `Small`.
- Do **not** add `%SafeFrame` to either centred non-Full screen; pass viewport size directly to `RefreshPresentation`.
- Reuse `UIScreenKinds.Shop` / `Heal`; no new kinds, groups, parent handles, incompatible-kind rules, or host APIs.
- Shop/Heal policy: `PauseTree = false`, `BlockGameplayInput = true`, cursor visible, **HUD hidden per HPA-373 §7.3**, lower layers visible/inert, Cancel consumed/intercepted, `QueueFree` lifetime.
- Do not change Dialogue's current `Hud = Visible` policy in HPA-570.
- Keep one scene/controller per surface; compact mode is responsive presentation only.
- Dynamic Shop rows remain controller-local.
- Focus restore uses semantic identity and focusability, never queued `Control` references.
- No stock mechanics, buyback, quantity picker, bulk sale, party/status heal, new pricing, or new domain rules.
- Delete native dialogs after hosted parity + real-route lifecycle tests are green; keep no compatibility wrapper.

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
- `tests/TestHelpers.cs`
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

### Reference only unless a focused failure proves otherwise

- `scripts/ui/SaveLoadScreenController.cs`
- `scenes/ui/SaveLoadScreen.tscn`
- `scenes/ui/components/SiriusPrompt.tscn`
- `scripts/ui/DialogueScreenController.cs`
- `scripts/ui/InventoryMenuController.cs`
- `scripts/ui/hosting/UIScreenHost.cs`
- `scripts/ui/hosting/UIScreenKinds.cs`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/theme/SiriusUiMetrics.cs`
- `scripts/data/npc/NpcData.cs`
- `scripts/data/npc/ShopInventory.cs`
- `scripts/data/npc/ShopCatalog.cs`
- `Character` inventory/gold/health APIs

---

## Risk checklist

### Host lifecycle protocol drifts when copied

Refactor the currently tested Dialogue present/close mechanics into private `NpcInteractionController` helpers **before** reusing them for Shop/Heal. Keep specs/signals/state explicit at each call site.

### Wrong sibling geometry gets copied

Shop is centred `Large`; Healing is centred `Small`. Follow Save/Load's non-Full shell ownership, not Dialogue's `Full` lower band. No second SafeFrame/margin owner.

### Standing disabled reason gets erased by transient feedback

Shop row affordability is standing row-local text. `%FeedbackLabel` is timed operation feedback only. Healing uses one standing non-timed label.

### Rebuilding Shop rows leaves unusable focus

Restore same item only when focusable; otherwise first focusable row → active tab → Close. Do not claim “next row” without storing an index.

### Pricing test mirrors instead of testing production

Move the three-line rule onto `ShopScreenController`; delete the test-local copy and call production directly.

---

## Task 1: Build the authored Shop screen with transaction parity

**Files:**
- Create: `scenes/ui/ShopScreen.tscn`
- Create: `scripts/ui/ShopScreenController.cs`
- Create: `tests/ui/ShopScreenControllerTest.cs`
- Modify: `tests/TestHelpers.cs`
- Modify: `tests/data/npc/ShopPricingTest.cs`
- Reference: `scripts/ui/ShopDialog.cs`
- Reference: `tests/ui/ShopDialogTest.cs`
- Reference: `scripts/ui/SaveLoadScreenController.cs`

**Controller contract:**

```csharp
public partial class ShopScreenController : Control
{
    [Signal] public delegate void ShopClosedEventHandler();

    public Control? InitialFocusTarget { get; private set; }

    public bool TryOpenShop(ShopInventory shop, Character player);
    public void RequestCancel();

    internal static int SellPrice(int itemValue) =>
        Mathf.Max(1, Mathf.FloorToInt(itemValue * 0.5f));
}
```

Configuration is one-shot and may happen before `_Ready()`.

- [ ] **Step 1: Add one shared viewport-mount test helper**

In `tests/TestHelpers.cs` add only:

```csharp
public static (SubViewportContainer Container, SubViewport Viewport)
    MountInViewport(Node child, Vector2I size)
```

It creates a stretched `SubViewportContainer`, a 2D `SubViewport`, adds both to the runtime tree, attaches `child`, and returns the two fixture nodes.

Use it in the new Shop and Healing suites. Do not refactor existing suites in this ticket.

- [ ] **Step 2: Write RED Shop scene/configuration tests**

Cover:

- load `res://scenes/ui/ShopScreen.tscn`
- instantiate `ShopScreenController`
- `TryOpenShop(...)` before attachment renders after `_Ready()`
- second start rejected
- no `AcceptDialog`
- `%ModalShell.SizeClass == SiriusModalSizeClass.Large`
- no Dialogue-style bottom-band geometry assumption
- title renders `ShopInventory.DisplayName`
- initial focus target is non-null **and focusable**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest"
```

Expected: FAIL because the screen does not exist.

- [ ] **Step 3: Author the stable Shop scene**

Create:

```text
ShopScreen (full-viewport Control)
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

No `%SafeFrame` and no fixed item rows.

The shell body scroll remains the overflow owner. Do not add per-tab scroll containers unless a focused runtime test demonstrates a real failure.

- [ ] **Step 4: Bind pre-ready state and centred responsive layout**

On `_Ready()`:

- bind authored nodes
- connect Close
- subscribe `Resized`
- render stored Shop state
- call `RefreshLayout()`

Use:

```csharp
private void RefreshLayout()
{
    if (!IsNodeReady() || _shell == null || !IsInsideTree())
        return;

    var size = GetViewportRect().Size;
    _shell.Compact = SiriusUiMetrics.IsCompact(size);
    _shell.RefreshPresentation(size);
}
```

In `_ExitTree()` unsubscribe `Resized`, button handlers, and any timer callback.

Rendering sets:

```csharp
_shell.Title = _shop.DisplayName;
```

Do not compute SafeFrameInsets in this controller.

- [ ] **Step 5: Port Buy with separate standing reason and transient feedback**

Write RED tests for:

1. successful Buy deducts `Item.Value`, adds one item, and refreshes gold/lists
2. insufficient gold disables Buy and shows standing row-local `Not enough gold!`
3. standing affordability remains visible after unrelated transient feedback expires
4. inventory-full Buy rolls spent gold back and shows transient `Inventory full!`
5. callback revalidation after state changed can show transient `Not enough gold!`
6. missing catalogue item is skipped/revalidated safely

Port the existing mutation order directly. Add no service.

- [ ] **Step 6: Move Sell price to one production definition and port Sell**

Use `ShopScreenController.SellPrice(int)` for production rendering/mutation.

Update `ShopPricingTest`:

- remove its mirrored `SellPrice` helper
- call `ShopScreenController.SellPrice(...)` directly
- retarget comments away from deleted `ShopDialog`

Add/retain tests proving:

- even/odd/minimum sell values
- one activation removes one item before granting gold
- selling the last item immediately renders `Nothing to sell.`
- failed removal grants no gold, shows transient `Item no longer available.`, and refreshes

Do not create a `ShopPricing` type/file.

- [ ] **Step 7: Preserve Shop transient timer semantics**

Port the current latest-message-wins behavior:

- one timer reference/handler
- replacing transient feedback disconnects the previous timeout
- next timeout is two seconds
- close / `_ExitTree()` cancels it
- timeout clears `%FeedbackLabel` only, never row-local reasons

Port the existing latest-message regression test.

- [ ] **Step 8: Add Shop re-entrancy and terminal guards**

Use:

```csharp
private bool _operationInFlight;
private bool _terminalEmitted;
```

Buy/Sell return when in-flight. Set/reset `_operationInFlight` in `try/finally` around synchronous mutation/refresh.

`RequestCancel()` emits `ShopClosed` once and stops transient feedback.

Test re-entrant activation and double `RequestCancel()`.

- [ ] **Step 9: Use one focus chain for initial focus and rebuild restore**

Capture before rebuild:

- active tab
- focused item id, when focus belongs to a row

Resolve after render:

1. same item action if present and focusable
2. first focusable row on active page
3. active tab
4. Close

Use a local focusability predicate equivalent to the existing Inventory guard (visible, enabled, focusable); do not copy Inventory's pending-focus record machinery.

Test:

- zero-gold Shop never selects a disabled Buy button
- selling focused last item does not leave focus on a queued node
- rebuild with changed affordability lands on a focusable target

- [ ] **Step 10: Run Shop GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~ShopDialogTest|FullyQualifiedName~ShopPricingTest"
```

Expected before deletion: new Shop + pricing tests PASS and legacy Shop tests still PASS.

---

## Task 2: Build the authored Healing screen with one-shot parity

**Files:**
- Create: `scenes/ui/HealingScreen.tscn`
- Create: `scripts/ui/HealingScreenController.cs`
- Create: `tests/ui/HealingScreenControllerTest.cs`
- Reuse: `tests/TestHelpers.cs`
- Reference: `scripts/ui/HealDialog.cs`
- Reference: `tests/ui/HealDialogTest.cs`
- Reference: `scenes/ui/components/SiriusPrompt.tscn`

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
- `%ModalShell.SizeClass == SiriusModalSizeClass.Small`
- `%HealButton` / `%CancelButton` are under shell `ActionsHost`
- title equals `NpcData.DisplayName`
- initial focus = Heal when enabled, No Thanks otherwise

- [ ] **Step 2: Author the small Healing scene**

Create:

```text
HealingScreen (full-viewport Control)
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

No `%SafeFrame`.

Reuse Prompt's stable `ActionsHost` composition, not `SiriusPrompt` behavior.

- [ ] **Step 3: Bind pre-ready state and centred layout**

Use the same guarded `RefreshLayout()` as Shop:

- `IsNodeReady` / `IsInsideTree` guard
- `_shell.Compact = SiriusUiMetrics.IsCompact(size)`
- `_shell.RefreshPresentation(size)`
- `Resized` subscribe/unsubscribe

Render:

```csharp
_shell.Title = _npc.DisplayName;
```

- [ ] **Step 4: Port standing availability presentation**

Render HP, cost, gold. `%FeedbackLabel` is non-timed standing state:

- full HP → `You are already at full health.`
- insufficient gold → `Not enough gold!`
- otherwise clear

Disable Heal for either unavailable state.

Set `InitialFocusTarget`:

1. Heal if enabled/focusable
2. otherwise No Thanks

Preserve the current warning for non-positive `HealCost`; do not invent a new rule.

- [ ] **Step 5: Port Heal mutation and use the terminal latch as the duplicate guard**

Write tests proving:

- successful Heal deducts configured cost
- HP becomes `GetEffectiveMaxHealth()`
- successful Heal emits `HealComplete` once
- two sequential Heal activations spend/emit once
- Heal then Cancel emits no cancellation
- double `RequestCancel()` emits `HealCancelled` once
- programmatic activation while full/poor cannot mutate HP/gold
- no timer is created

Preserve current order and keep `GameManager.Instance?.NotifyPlayerStatsChanged()`.

Do **not** add `_operationInFlight` to Healing; the terminal latch is the one-shot local duplicate guard.

- [ ] **Step 6: Run Healing GREEN**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~HealDialogTest"
```

Expected before deletion: new and legacy Healing behavior tests PASS.

---

## Task 3: Consolidate NPC hosting, cut over Shop/Heal, and prove the real route

**Files:**
- Modify: `scripts/ui/NpcInteractionController.cs`
- Modify: `scripts/game/Game.cs`
- Modify: `tests/ui/NpcInteractionControllerTest.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/game/GameInputLifecycleTest.cs`

This is the riskiest task. Its production-route tests land here, not one task later.

- [ ] **Step 1: Refactor the existing hosted Dialogue path without changing behavior**

Before adding Shop/Heal, extract the hardened mechanical `TryPresent` protocol from `Begin()` into:

```csharp
private bool TryHostSurface(
    Control screen,
    UIScreenEntrySpec spec,
    Action unsubscribe,
    out UIScreenHandle handle)
```

It owns only:

- `TryPresent` try/catch
- unsubscribe + free valid candidate on thrown publication
- `Finish()` before rethrow
- rejected/no-handle cleanup + `Finish()`
- `IsActive(handle)` recheck after `Opened`
- `Finish()` without retaining state when already inactive
- returning the active handle on success

Dialogue still owns:

- screen creation/configuration
- signal subscriptions
- its explicit `UIScreenEntrySpec`
- `_dialogueScreen` / `_dialogueHandle`

Also extract only the stale-close mechanics:

```csharp
private void CloseHostedPresentation(
    UIScreenHandle? handle,
    UIScreenCloseReason reason,
    Action clear)
```

Per-surface `Close*Presentation` wrappers remain.

Run **all existing `NpcInteractionControllerTest` tests now**. The HPA-569 publication-throw, synchronous post-commit close, stale-handle, and Finish tests are the refactor safety net.

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest"
```

Expected: PASS before Shop/Heal semantics change.

- [ ] **Step 2: Replace native-transition tests with RED hosted expectations**

Replace, not duplicate:

- `ShopOutcome_ClosesHostedDialogueBeforeNativeShopOpens`
- `HealOutcome_ClosesHostedDialogueBeforeNativeHealOpens`

New expectations:

- Dialogue closes first
- exactly one Shop/Heal host entry opens
- no native child is created
- cancel closes hosted entry and completes once

Expected: FAIL while production still opens native dialogs.

- [ ] **Step 3: Host Shop through the private presentation helper**

`OpenShop()`:

1. resolve `ShopCatalog.GetById(_npc.ShopId)`; invalid data logs + `Finish()`
2. instantiate `ShopScreen.tscn`
3. call `TryOpenShop(shopInventory, _player)` before presentation
4. subscribe `ShopClosed`
5. construct this explicit policy:

```csharp
new UIScreenEntrySpec
{
    Kind = UIScreenKinds.Shop,
    Layer = UIScreenLayer.Modal,
    InputPriority = UIInputPriority.Modal,
    ProcessPolicy = UIProcessPolicy.Always,
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

6. call `TryHostSurface(...)`
7. retain `_shopScreen` + `_shopHandle` only on success

Keep explicit `ClearShopPresentation` / `CloseShopPresentation`.

- [ ] **Step 4: Host Healing the same way**

Use:

- `Kind = UIScreenKinds.Heal`
- the same policy values including `Hud = Hidden`
- `InitialFocus = () => screen.InitialFocusTarget`
- Cancel → `screen.RequestCancel()`
- `Cleanup = _ => ClearHealPresentation(screen)`
- `HealComplete` + `HealCancelled` terminal signals

Retain only active screen/handle state returned by `TryHostSurface`.

- [ ] **Step 5: Preserve terminal close/Finish ordering**

For Dialogue/Shop/Heal:

- cleanup callbacks unsubscribe signals and clear matching screen/handle state
- `Close*Presentation` delegates mechanical close/stale handling to `CloseHostedPresentation`
- terminal handlers catch close-publication exceptions and still call `Finish()`
- `Finish()` sets `_finished = true` before cleanup, catches cleanup publication failures, and always invokes `InteractionComplete` once

Do not move this protocol into `UIScreenHost` or a new class.

- [ ] **Step 6: Remove the legacy UI-parent dependency**

After both native `AddChild` paths are gone:

- delete `_uiParent` field/constructor parameter from `NpcInteractionController`
- update the sole production construction in `Game.cs`
- update `NpcInteractionControllerTest` fixtures/helpers

No compatibility parameter.

- [ ] **Step 7: Add real-route Shop/Heal host and Cancel coverage now**

Model these on `ConfiguredKeyboardCancel_ClosesHostedDialogueThroughRealRoute`.

For real Shop and Healing routes:

1. instantiate `Game.tscn`
2. locate `village_shopkeeper` / `village_healer` with `TestHelpers.FindNpcInternalPosition`
3. invoke the authored NPC interaction
4. press the Dialogue choice that opens Shop/Heal
5. assert Dialogue is gone and the expected hosted entry is active
6. assert entry policy:
   - `BlockGameplayInput = true`
   - `PauseTree = false`
   - `Hud = UIHudPolicy.Hidden`
   - cursor visible
   - Cancel consumed
   - tree not paused
7. press configured keyboard Cancel
8. assert Shop/Heal closes, Pause does not open, and `IsInNpcInteraction` clears
9. cover controller/gamepad Cancel on at least one Shop/Heal route

Delete or rewrite `ConfiguredKeyboardCancel_NpcInteractionDeclinesForNativeHandler`; the native phase no longer exists.

`GameplayPauseHostTest` should assert active Shop/Heal entries block presentation input without pausing the tree. Do not duplicate transaction semantics there.

- [ ] **Step 8: Extend hosted error/teardown coverage**

Add focused tests for:

- `Finish()` while Shop active closes it and completes once
- `Finish()` while Heal active closes it and completes once
- invalid Shop id opens no Shop and completes once
- host rejection leaks no candidate
- synchronous post-commit `EffectiveStateChanged` close leaves no stale Shop/Heal handle
- close publication exception still reaches exactly-once completion

Reuse existing Dialogue test techniques. No fake host.

- [ ] **Step 9: Run cutover GREEN including the real route**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: PASS with hosted Shop/Heal, no native route, consumed Cancel, hidden HUD, and no stale interaction latch.

---

## Task 4: Delete native dialogs and update the lifecycle contract

**Files:**
- Delete: `scripts/ui/ShopDialog.cs`
- Delete: `tests/ui/ShopDialogTest.cs`
- Delete: `scripts/ui/HealDialog.cs`
- Delete: `tests/ui/HealDialogTest.cs`
- Modify: `docs/ui/hpa-376/ui-lifecycle-contract.md`

- [ ] **Step 1: Prove no live native assumptions remain**

Search for:

```text
ShopDialog
HealDialog
LegacyNpcUiParent
NativeShop
NativeHeal
native handler
```

Production/test references should now be deletion targets or historical documentation only.

`ShopPricingTest` should already call `ShopScreenController.SellPrice` from Task 1.

- [ ] **Step 2: Delete native classes and superseded focused tests**

Delete the four files after Tasks 1–3 are green.

Do not add wrappers or aliases.

- [ ] **Step 3: Update HPA-376 lifecycle contract**

Retarget the Shop/Heal transition/state rows (`NPC-TO-SHOP`, `NPC-SHOP`, `NPC-TO-HEAL`, `NPC-HEAL`) to the hosted implementation:

- host kinds Shop / Heal
- no scene-tree pause
- gameplay input blocked
- HUD hidden
- cursor visible
- lower layers visible/inert
- Cancel intercepted/consumed
- QueueFree node lifetime
- `NpcInteractionController` orchestration owner

Remove native-dialog wording.

- [ ] **Step 4: Run migration blast radius**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo \
  --filter "FullyQualifiedName~ShopScreenControllerTest|FullyQualifiedName~HealingScreenControllerTest|FullyQualifiedName~ShopPricingTest|FullyQualifiedName~NpcInteractionControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~GameInputLifecycleTest"
```

Expected: PASS with no `ShopDialog` / `HealDialog` types.

---

## Task 5: Responsive/focus verification and full regression pass

**Files:**
- Modify only directly affected files when a focused failure proves a fix is needed.

- [ ] **Step 1: Pin representative viewport geometry**

Exercise:

- 640×360 compact
- 1280×720 reference
- 1920×1080 standard

Assert:

- Shop shell is `Large` in standard mode
- Healing shell is `Small`
- both panels are centred and remain inside viewport-safe shell margins
- neither copies Dialogue's bottom band
- compact mode leaves the shell's 12 px margin exactly once
- shell body scrolling keeps required controls reachable

Do not duplicate `SiriusModalShellTest`'s exhaustive clamp math in the new screen suites; assert integration-level geometry only.

- [ ] **Step 2: Verify focus matrix**

Cover:

- Shop initial focus is focusable with normal gold
- zero-gold Shop does not target a disabled Buy action
- Shop tab switching leaves a usable focus target
- row rebuild never leaves focus on queued/freed controls
- Healing initial focus = Heal when enabled, No Thanks when unavailable
- keyboard/gamepad host Cancel closes both without Pause fallthrough

- [ ] **Step 3: Build**

```bash
dotnet build Sirius.sln --no-restore --nologo
```

Expected: PASS with no production `ShopDialog`/`HealDialog` references.

- [ ] **Step 4: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore --nologo
```

Expected: PASS.

- [ ] **Step 5: Inspect final diff for scope creep**

Expected implementation scope:

- two authored scenes/controllers + tests
- one small test viewport helper
- one three-line production Sell-price function on `ShopScreenController`
- private `NpcInteractionController` present/close helpers + Shop/Heal cutover
- `Game` constructor call update
- affected real-route host/input tests
- native Shop/Heal deletion
- lifecycle contract update

Reject:

- transaction services/base controllers
- generic host types/facades
- broad refactors of unrelated `TryPresent` call sites
- new host kinds/groups
- extra SafeFrame/layout abstraction
- reusable Shop row component
- new theme tokens
- new gameplay rules
- unrelated HPA-571/HPA-573/HPA-625 work

---

## Done criteria

HPA-570 is implementation-complete when:

- Shop is a centred authored `Large` Sirius surface and binds the Shop display name.
- Healing is a centred authored `Small` Sirius surface and binds NPC display name.
- Neither screen adds an unnecessary SafeFrame or copies Dialogue's bottom band.
- `NpcInteractionController` presents Dialogue/Shop/Heal through one tested private mechanical hosting protocol while keeping per-surface specs/state explicit.
- current Buy **and Sell** behavior is preserved; one production Sell-price definition is tested directly.
- current Healing behavior is preserved without a timer or redundant in-flight flag.
- standing disabled reasons remain readable and are not erased by Shop transient feedback.
- initial/restored focus always resolves to a focusable control.
- real-route Shop/Heal host/input tests land with the cutover and prove hidden HUD, gameplay blocking, and Cancel behavior.
- every success/cancel/failure/teardown path clears NPC interaction exactly once.
- native Shop/Heal classes/tests are deleted.
- focused migration tests, build, and full suite are green.
