# HPA-380 Main Menu and Deterministic Continue Design

**Status:** Implementation-ready planning candidate  
**Linear:** HPA-380  
**Scope:** Main Menu vertical slice plus one proven shared safe-frame metric extraction

## 1. Why HPA-380 is next

The Sirius UI foundation is already in place: HPA-354, HPA-377, HPA-378, HPA-382, and HPA-383 are complete, and HPA-381 delivered the compact exploration HUD. HPA-380 is the next high-priority player-facing slice in the project delivery order whose declared blocker is complete.

This remains a vertical screen migration, not another framework project. The Main Menu should prove the existing Theme and `UIScreenHost` contracts with the minimum root integration needed for its real flows.

## 2. Current state

The production Main Menu currently has a generic centred `VBoxContainer` with Start Game, Load Game, Settings, and Quit. `MainMenu.cs`:

- loads/configures its background at runtime;
- directly parents `SaveLoadDialog`;
- directly parents the scene-authored Settings screen;
- creates unthemed timer-driven `AcceptDialog` messages;
- changes scenes immediately.

Existing reusable pieces already cover the needed foundation:

- `resources/ui/theme/SiriusTheme.tres` and `SiriusThemeTypes`;
- `SiriusUiMetrics` for the 640×360 minimum, compact reflow, safe margins, target sizes, and 1600px content cap;
- `SiriusInputHint` for binding/device-aware hints;
- `UIScreenHost` for child presentation lifecycle, Cancel dispatch, focus restoration, embedded subwindows, and teardown;
- `SettingsMenu.tscn` / `SettingsMenuController`;
- legacy `SaveLoadDialog` until HPA-384 replaces its presentation;
- `SaveManager.GetSaveSlotInfo`, `LoadGame`, `LoadAutosave`, and `PendingLoadData`;
- the HPA-373 Main Menu composition: lower-left navigation, Continue summary, New Game, Load, Settings, Quit, and input hints.

The missing work is composition and orchestration, not new infrastructure.

## 3. Approaches considered

### A. Scene-owned Main Menu plus local hosted child flows — selected

Keep `MainMenu.tscn` and `MainMenu.cs` as the root owner. Scene-author the approved layout, add one local `UIScreenHost`, calculate Continue from existing `SaveSlotInfo`, and host the existing Load/Settings views plus one local themed error path.

This is the smallest change that satisfies HPA-380 and leaves HPA-384/HPA-572 free to replace their own presentation later.

### B. Register the entire Main Menu as a host screen — rejected

Reparenting the root content into `UIScreenHost.ScreenLayer` would add a base-screen lifecycle solely to gain lower-layer inerting. The Main Menu is already the scene root, and no second root consumer proves that abstraction useful.

### C. Add a Main Menu presenter/navigation coordinator — rejected

There are only five root actions. `SaveManager` and `UIScreenHost` already own the meaningful domain/presentation responsibilities. A presenter or router would mostly forward calls and add synchronization state.

## 4. Selected architecture

`MainMenu` remains the only root controller. Add one pure policy helper and two narrow virtual test seams:

```csharp
internal static SaveSlotInfo? SelectContinueSave(
    IReadOnlyList<SaveSlotInfo> slots);

protected virtual SaveData? LoadSlot(int slot);
protected virtual Error ChangeSceneToFile(string path);
```

`SelectContinueSave` implements deterministic Continue policy. `LoadSlot` enables full-load-failure coverage without a SaveManager interface. `ChangeSceneToFile` mirrors the existing `RequestApplicationQuit` seam and avoids replacing the test root scene.

Do not add a singleton, navigation service, save facade, settings facade, view model, compatibility layer, message framework, or transition framework.

## 5. Shared safe-frame metric

HPA-381 already owns the production safe-frame calculation for a capped centred content region:

```text
compact -> margin -> available width -> 1600px cap -> side inset clamped to margin
```

HPA-380 would be the second production consumer of exactly that calculation. This satisfies the project rule to generalize only after a second concrete consumer exists.

Add one small helper to `SiriusUiMetrics`:

```csharp
public static (bool Compact, float Margin, float SideInset)
    SafeFrameInsets(Vector2 viewportSize)
{
    var compact = IsCompact(viewportSize);
    var margin = SafeMargin(compact);
    var availableWidth = MathF.Max(0f, viewportSize.X - margin * 2f);
    var contentWidth = MathF.Min(availableWidth, MaximumContentWidth);
    var sideInset = MathF.Max(
        margin,
        (viewportSize.X - contentWidth) / 2f);
    return (compact, margin, sideInset);
}
```

Migrate `ExplorationHudController.RefreshLayout()` to this helper without changing its visible behavior. Main Menu uses the same helper. Do not create a layout service/class or migrate the showcase, whose `MarginContainer` composition is not the same offset contract.

## 6. Scene composition

`scenes/ui/MainMenu.tscn` becomes the canonical static layout and owns the Sirius Theme directly.

```text
MainMenu
├── Background
├── MainMenuContent
│   └── SafeFrame
│       └── MenuRail
│           ├── WordmarkLabel
│           ├── ContinueButton
│           ├── ContinueSummary
│           │   └── ContinueSummaryContent
│           │       ├── ContinueSlotLabel
│           │       ├── ContinueDetailLabel
│           │       └── ContinueTimestampLabel
│           ├── NewGameButton
│           ├── LoadButton
│           ├── SettingsButton
│           ├── QuitButton
│           └── SelectHint
├── UIScreenHost
└── BackgroundMusic
```

### 6.1 Background

Keep `ui_main_menu_background.png`, but reference it from the scene and use aspect-preserving cover crop. Remove runtime background loading/configuration from `MainMenu.cs`.

### 6.2 Navigation rail

Place the rail in the lower-left portion of the safe frame so it does not cover the castle/moon focal area. Use existing Theme variations and spacing rather than a bespoke orbital control.

Visual hierarchy:

1. `SIRIUS` wordmark;
2. Continue;
3. selected Continue save summary when eligible;
4. New Game;
5. Load;
6. Settings;
7. Quit;
8. binding-aware Select hint.

The five Buttons—Continue, New Game, Load, Settings, Quit—are the complete actionable root-control set for HPA-380. Background, wordmark, summary chrome/labels, and input-hint decoration are passive/non-focusable.

### 6.3 Continue summary

Use a themed `PanelContainer`, not a reusable component. Show only existing metadata:

- slot display name;
- player name and level;
- floor name;
- timestamp when it is not `DateTime.MinValue`.

The summary must not introduce derived gameplay metadata.

When no eligible Continue save exists, hide the summary and disable Continue.

## 7. Deterministic Continue policy

On Main Menu initialization, read exactly slots 0, 1, 2, and 3 through `SaveManager.GetSaveSlotInfo` and pass them to `SelectContinueSave`.

Eligibility:

```text
Exists == true AND IsCorrupted == false
```

Ordering:

1. greater stored `Timestamp` wins;
2. `DateTime.MinValue` is older than any usable timestamp;
3. equal timestamps rank autosave slot 3, then manual slots 0, 1, and 2.

### 7.1 Ordering versus display time

Comparison uses the stored `DateTime` values directly. Do not normalize, mutate, migrate, backfill, or convert timestamps before ranking.

Save creation currently writes `DateTime.UtcNow`, while `SaveLoadDialog` displays slot timestamps using `.ToLocalTime()`. The Main Menu Continue summary should therefore also display:

```csharp
info.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
```

This is presentation conversion only. It does not change storage or ranking policy and keeps the same save from showing two different clock times between Continue and Load.

Return the selected existing metadata object; do not create a parallel Continue DTO.

## 8. Root action availability and hard guards

Initial focus:

- eligible Continue → `ContinueButton`;
- otherwise → `NewGameButton`.

Root Cancel is a consumed no-op.

Because the Main Menu root itself is not a hosted entry, host lower-layer effects do not inert its controls. HPA-380 therefore owns one local predicate:

```csharp
private bool IsRootActionBlocked() =>
    _sceneChangeCommitted ||
    (_screenHost != null &&
     IsInstanceValid(_screenHost) &&
     _screenHost.ActiveEntries.Count != 0);
```

`RefreshActionAvailability()` derives Button disabled state from that predicate plus Continue eligibility.

Every root action handler—Continue, New Game, Load, Settings, and Quit—must also check `IsRootActionBlocked()` before doing work. Disabled state is presentation feedback, not the only correctness barrier: programmatic/double signal activation while a hosted child is active must also be a no-op.

When a child closes, cleanup refreshes action availability before host focus restoration runs.

## 9. Local `UIScreenHost` integration

Add one authored `UIScreenHost` child to `MainMenu.tscn`. Configure it from `MainMenu._EnterTree()` before the host reaches `_Ready()`:

```csharp
_screenHost.Configure(new UIScreenHostOptions
{
    CoreCancelActions = new HashSet<StringName> { "ui_cancel" },
    RootCancelFallback = _ => UIRootCancelResult.Consumed
});
```

No HUD policy, gameplay-input callback, pause ownership, or host API changes are needed.

The embedded-subwindow setting already used by gameplay remains the mechanism for hosting `SaveLoadDialog` and the local `AcceptDialog` message.

## 10. Hosted Load flow

HPA-380 integrates but does not redesign `SaveLoadDialog`.

Direct Load activation:

1. preserve the current SaveManager-availability check;
2. preserve the current “any save exists” check;
3. create one `SaveLoadDialog`;
4. register it as `UIScreenKinds.SaveLoad`;
5. use Modal layer/priority, `UIProcessPolicy.Always`, visible cursor, Cancel-to-close, and `NodeLifetime.QueueFree`;
6. preserve child-dialog Cancel interception;
7. use `SetPresented` with `ShowDialog(Load)` / `Hide()`;
8. call `ShowDialog(Load)` once after successful initial `TryPresent`;
9. restore focus to `LoadButton` when direct Load closes.

For Continue failure fallback, the same helper accepts an explicit restoration target and restores `ContinueButton`, because Continue—not Load—started that flow.

Recommended shape:

```csharp
private bool TryOpenHostedLoad(Control? restoreFocus = null)
{
    var restoreTarget = restoreFocus ?? _loadButton;
    // register RestoreFocus = () => restoreTarget
}
```

Do not add Save-mode behavior to Main Menu.

### 10.1 Manual Load success

Use the existing full-load path. On success:

- assign `SaveManager.PendingLoadData`;
- request Game through the guarded transition path;
- let host teardown close Load.

### 10.2 Manual Load failure

Keep the simpler existing behavior boundary: Load is terminal after the player chooses a slot and full loading fails.

1. close hosted Load with `ExplicitAction`;
2. defer one callback/frame so the host finishes its focus-restoration transaction;
3. open a root hosted `Load Failed` message restoring to `LoadButton`;
4. dismissing the error returns to Main Menu.

Do not keep a hidden `SaveLoadDialog` alive under the error and do not add a `closeParentOnDismiss` cleanup protocol.

### 10.3 HPA-384 handoff

HPA-384 must replace **two** production hosting call sites for the legacy `SaveLoadDialog`:

- `Game.TryOpenHostedSaveLoad(...)`;
- `MainMenu.TryOpenHostedLoad(...)` added by HPA-380.

HPA-380 deliberately does not extract a shared `UIScreenEntrySpec` factory. The two roots have different parent/restoration/domain callbacks, and HPA-384 is the owning migration that can remove both legacy paths together.

## 11. Hosted Settings flow

Reuse HPA-383 Settings exactly as the gameplay Pause host does:

- `UIScreenKinds.Settings`;
- Modal layer/priority;
- `UIProcessPolicy.Always`;
- visible cursor;
- Cancel-to-close;
- reserve Cancel while rebinding or an `OptionButton` popup is open;
- `InitialFocus = () => settings.InitialFocusTarget`;
- `RestoreFocus = () => _settingsButton`;
- `SetPresented` uses `OpenSettings(showOverlay: false)` / `Hide()`;
- call `OpenSettings(showOverlay: false)` once after successful initial `TryPresent`;
- queue-free lifetime.

Delete direct Main Menu child ownership and manual post-close focus handling.

## 12. Local themed message/error path

HPA-572 owns the future shared confirmation/warning/error family. HPA-380 must not create a reusable message framework.

Use one private helper around an embedded themed `AcceptDialog`:

```csharp
private bool TryOpenMessage(
    string title,
    string message,
    Control? restoreFocus,
    UIScreenHandle? parent = null);
```

The helper:

- applies `SiriusTheme.tres`;
- reuses existing `UIScreenKinds.SaveError` as the transitional concrete kind;
- uses Blocking priority and `UIScreenExclusiveGroups.BlockingPrompt`;
- uses `UIProcessPolicy.Always`;
- uses visible cursor and `UILowerLayerPolicy.VisibleInert`;
- focuses the built-in OK button;
- provides `SetPresented = visible => { if (visible) popup.PopupCentered(); else popup.Hide(); }`;
- still performs one explicit `PopupCentered()` after the initial successful `TryPresent` because the host does not present incoming views at open;
- maps Confirmed/Canceled/CloseRequested through one local `handled` latch so only one host close is requested;
- allows only one active message;
- never uses a timer;
- restores a supplied root control for root messages;
- relies on logical parent restoration for child messages.

No parent-close behavior lives in this helper.

## 13. Continue activation and failure fallback

When Continue is pressed:

1. return if `IsRootActionBlocked()` or no selected Continue metadata exists;
2. call `LoadSlot(_continueSave.SlotIndex)`;
3. on success, assign `PendingLoadData` and request Game;
4. on failure, never start New Game;
5. if `SaveManager` remains available, call `TryOpenHostedLoad(_continueButton)`;
6. when Load opens, show “Failed to load the selected save.” as a hosted child error;
7. dismissing that child error leaves Load open;
8. later closing fallback Load restores `ContinueButton` focus;
9. if the save system is unavailable or Load cannot be hosted, show the error as a root message and stay on Main Menu.

Do not scan for a second-best save after full-load failure. The player chooses explicitly from Load.

The fallback Load surface may contain no actionable save rows if the failed save was the only candidate or has become corrupted between metadata selection and full loading. This is acceptable: Cancel remains available, no silent New Game occurs, and HPA-384 owns richer recovery/repair presentation.

Testing must exercise both layers:

- the real `_on_continue_button_pressed` activation seam must call `LoadSlot` with the selected slot and obey the null/scene-committed guards;
- the hosted failure UI lifecycle may be tested separately through the real scene fixture once a null load result is routed into `HandleContinueLoadResult`.

## 14. Handler/task sequencing

`MainMenu.tscn` connects its final five Button signals in the scene-authorship task. Therefore those handler names must exist in the same task.

Task 2:

- rename current `_on_start_button_pressed` to `_on_new_game_button_pressed` while preserving current New Game behavior temporarily;
- add `_on_continue_button_pressed` with the final guard shape but no full-load call yet;
- keep `_on_load_button_pressed`, `_on_settings_button_pressed`, and `_on_quit_button_pressed` present.

Task 3 replaces the temporary `_sceneChangeCommitted` guard with `IsRootActionBlocked()` for all five handlers.

Task 4 fills Continue loading and routes New Game through the guarded host teardown path.

This keeps every intermediate task compilable/testable without inventing placeholder services.

## 15. Scene-transition gate and host teardown

Mirror the existing narrow pattern in `Game.cs`; do not extract a navigation service.

Declare the one-way gate before any task that reads it:

```csharp
private const string GameScenePath = "res://scenes/game/Game.tscn";
private string? _pendingScenePath;
private bool _sceneChangeCommitted;
```

`RequestSceneChange` commits once, refreshes action availability immediately, and starts host teardown.

`ContinueSceneChangeAfterUiTeardown`:

1. returns if Main Menu is invalid/out of tree;
2. calls `UIScreenHost.PrepareForTeardown()`;
3. retries with `CallDeferred()` only when teardown returns `Deferred`;
4. after `Complete`, invokes `ChangeSceneToFile(path)` once.

New Game clears `PendingLoadData`. Successful Continue/Load assign it.

No transition animation, loading screen, router, retry framework, or cross-scene navigation singleton is added.

## 16. Responsive behavior

Use the shared `SiriusUiMetrics.SafeFrameInsets(viewportSize)` result plus existing target/typography metrics.

At compact size the reduction is unconditional and deterministic—there is no runtime measurement/retry loop:

1. compact typography applies;
2. rail separation becomes 4px;
3. Continue timestamp is hidden;
4. slot/player/level/floor collapse into one compact detail line;
5. every action remains at the 40px minimum target;
6. the rail grows upward as needed but must remain fully enclosed by `SafeFrame`;
7. Select hint remains compact.

Do not add another breakpoint, shrink essential text below the existing compact Theme, or add per-resolution position tables.

## 17. Test design

### 17.1 Continue policy

Cover:

- no eligible slots;
- corrupted/missing slots excluded;
- newest stored timestamp wins;
- usable timestamp beats `DateTime.MinValue`;
- equal timestamps prefer autosave then manual 0/1/2;
- all-MinValue tie follows the same rank.

### 17.2 Shared safe-frame metric

Cover at least:

- 640×360 → compact, margin 12, side inset 12;
- 1280×720 → standard, margin 24, side inset 24;
- 2560×1080 → standard, side inset 480 from the 1600px cap.

Run existing `ExplorationHudControllerTest` after migrating HUD layout math and retain its full viewport enclosure coverage.

### 17.3 Scene/layout

`MainMenuSceneTest` uses the established `SettingsMenuSceneTest` fixture pattern: `SubViewportContainer` + `SubViewport`, explicit frame waits, and `[TestCase(width, height)]` matrices.

It proves:

- Theme/background are scene-authored;
- required menu/summary/host nodes exist before `_Ready()`;
- old centred `VBoxContainer` is gone;
- non-action chrome is passive;
- 640×360 and 1280×720 content fits with Continue summary forced visible;
- every shared verification viewport respects the safe frame/content cap;
- all five action targets meet shared minimum sizes.

The test file must define its own concrete helpers (`ResizeAndCreate`, `AwaitFrames`, `AssertEnclosed`, reflection invoke/set helpers) rather than referencing helpers private to another suite.

### 17.4 Deterministic initial focus

Do not let user/developer `user://saves` decide test expectations.

Use two deterministic cases with cleanup:

- delete slots 0–3, instantiate/open Main Menu, assert `NewGameButton` focus;
- seed one valid slot via `SaveManager.SaveGame`, instantiate/open Main Menu, assert `ContinueButton` focus;
- delete test saves in `finally`.

### 17.5 Host/focus/lifecycle

Cover:

- production scene owns exactly one `UIScreenHost`;
- Settings opens once and restores Settings focus;
- direct Load opens once and restores Load focus;
- missing SaveManager shows one root error and no SaveLoad entry;
- no save files shows one root message/error and no SaveLoad entry;
- valid save presence opens exactly one SaveLoad entry;
- Continue fallback Load restores Continue focus when closed;
- all five root actions are disabled while a hosted child is active;
- at least one programmatic root action is also proven to no-op while a child is active;
- root Cancel is a no-op;
- one root message at a time;
- one message terminal outcome produces one close even if another terminal signal also fires;
- manual Load failure closes Load first, then opens one root error; dismissing it restores Load focus and leaves no SaveLoad entry.

### 17.6 Continue/load/transition

Cover:

- `_on_continue_button_pressed` calls `LoadSlot` for `_continueSave.SlotIndex`;
- no selected Continue and committed-scene cases do not call `LoadSlot`;
- Continue full-load failure opens Load + child error and does not commit scene change;
- dismissing Continue failure error keeps Load open;
- closing fallback Load returns focus to Continue;
- Continue success sets `PendingLoadData` and requests Game once;
- New Game clears `PendingLoadData` and requests Game once;
- repeated activation after `_sceneChangeCommitted` cannot request a second scene change.

## 18. Existing Main Menu test migration

HPA-380 replaces several concrete private methods/fields and scene paths. Rewrite or remove these tests in the same task that removes the production contract:

- `ShowMessage_CreatesOneVisibleAcceptDialogAndKeepsRootVisible` → hosted `TryOpenMessage`/`UIScreenKinds.SaveError` coverage;
- `OnLoadDialogClosed_QueuesChildAndClearsReference` → hosted Load close/cleanup coverage;
- `SettingsPressed_DoesNotStackAndClosedCleansOnlySettingsChild` → hosted Settings lifecycle/focus coverage;
- all `VBoxContainer/...` node paths → `%...` authored Main Menu paths.

Final stale-path verification must reject `VBoxContainer/`, `ShowMessage`, `CleanupLoadDialog`, `OnLoadDialogClosed`, and `_on_start_button_pressed` in Main Menu production/tests/scene.

## 19. File ownership

Expected implementation changes:

```text
scenes/ui/MainMenu.tscn
scripts/ui/MainMenu.cs
scripts/ui/theme/SiriusUiMetrics.cs
scripts/ui/ExplorationHudController.cs
tests/ui/MainMenuTest.cs
tests/ui/MainMenuSceneTest.cs
tests/ui/ExplorationHudControllerTest.cs
```

Planning documents remain the two HPA-380 docs.

Do not modify `SaveManager`, `SaveLoadDialog`, `SettingsMenuController`, `UIScreenHost`, Theme resources, or `project.godot` unless implementation proves the design impossible; reassess scope before expanding.

## 20. Risks and mitigations

### Intermediate task compile break

Scene signal names must exist when the scene lands. Create/rename Continue and New Game handlers in Task 2, then fill final behavior in Task 4.

### Compact vertical overflow

Use the unconditional compact reduction ladder and test with Continue summary forced visible at 640×360.

### Root controls active behind child presentation

Disabled state alone is insufficient. Centralize `IsRootActionBlocked()` and guard every root handler.

### Hosted Window presentation drift

`AcceptDialog` requires explicit `SetPresented(PopupCentered/Hide)` plus the initial `PopupCentered()` and a terminal-signal latch.

### Focus/restoration overlap on manual Load failure

Close Load first and defer opening the root error until the next frame, after host restoration has settled.

### Wrong focus after Continue fallback

Pass the invoking control into `TryOpenHostedLoad`; direct Load restores Load, Continue fallback restores Continue.

### Ambient save-state tests

Initial-focus tests seed/delete slots explicitly; layout tests inject Continue state directly.

### Scope leakage into Save/Load redesign

Keep legacy `SaveLoadDialog` unchanged and record both HPA-384 hosting call sites.

### Scope leakage into messages

Keep the hosted `AcceptDialog` helper private. HPA-572 owns shared message variants.

## 21. Acceptance mapping

| HPA-380 acceptance | Design response |
| --- | --- |
| Main Menu owns one local production `UIScreenHost` | authored host configured in `_EnterTree()` |
| Layout uses approved visual system | scene-owned Theme/background/lower-left rail/Continue summary |
| Continue ordering/failure deterministic | pure stored-time selector + explicit Load/error fallback |
| Existing primary actions preserved | existing domain calls and Load/Settings flows retained |
| Focus/layout usable | deterministic focus tests + shared safe-frame metric + forced-summary viewport tests |
| Scene transitions cannot activate twice | hard root guard + one-way commit + host teardown |

## 22. Explicit non-goals

- save-format migration or timestamp mutation;
- cloud saves, extra slots, thumbnails, renaming, repair, or deletion;
- redesigning Load or Settings;
- reusable root-screen registration;
- generic navigation/router abstractions;
- shared message/error framework;
- generic SaveLoad host factory;
- transition animation/loading screen;
- new Theme tokens/components.