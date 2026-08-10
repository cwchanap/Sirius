# HPA-380 Main Menu and Deterministic Continue Design

**Status:** Implementation-ready planning candidate  
**Linear:** HPA-380  
**Scope:** Main Menu vertical slice only

## 1. Why HPA-380 is next

The Sirius UI foundation is already in place: HPA-354, HPA-377, HPA-378, HPA-382, and HPA-383 are complete, and HPA-381 has delivered the compact exploration HUD. HPA-380 is therefore the next high-priority player-facing slice in the project delivery order whose declared blocker is complete.

This remains a vertical screen migration, not another framework project. The Main Menu should prove the existing Theme and `UIScreenHost` contracts with the minimum root integration needed for its real flows.

## 2. Current state

The production Main Menu currently has a generic centred `VBoxContainer` with Start Game, Load Game, Settings, and Quit. `MainMenu.cs`:

- loads/configures its background at runtime;
- directly parents `SaveLoadDialog`;
- directly parents the scene-authored Settings screen;
- creates unthemed `AcceptDialog` messages;
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

`SelectContinueSave` implements deterministic Continue policy. `LoadSlot` enables full-load-failure coverage without a SaveManager interface. `ChangeSceneToFile` mirrors the existing `RequestApplicationQuit` test seam and avoids replacing the test scene.

Do not add a singleton, navigation service, save facade, settings facade, view model, compatibility layer, or transition framework.

## 5. Scene composition

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

### 5.1 Background

Keep `ui_main_menu_background.png`, but reference it from the scene and use aspect-preserving cover crop. Remove runtime background loading/configuration from `MainMenu.cs`.

### 5.2 Navigation rail

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

### 5.3 Continue summary

Use a themed `PanelContainer`, not a reusable component. Show only existing metadata:

- slot display name;
- player name and level;
- floor name;
- stored timestamp when it is not `DateTime.MinValue`.

The Continue summary must not introduce derived gameplay metadata. At compact size, hide timestamp first; keep slot/player/level/floor visible.

When no eligible Continue save exists, hide the summary and disable Continue.

## 6. Deterministic Continue policy

On Main Menu initialization, read exactly slots 0, 1, 2, and 3 through `SaveManager.GetSaveSlotInfo` and pass them to `SelectContinueSave`.

Eligibility:

```text
Exists == true AND IsCorrupted == false
```

Ordering:

1. greater stored `Timestamp` wins;
2. `DateTime.MinValue` is older than any usable timestamp;
3. equal timestamps rank autosave slot 3, then manual slots 0, 1, and 2.

Compare and format the **Continue selection/summary** using stored `DateTime` values directly. Do not call `ToLocalTime`, `ToUniversalTime`, migrate timestamps, backfill missing timestamps, or introduce a compatibility wrapper.

This rule applies to HPA-380's new Continue path only. The legacy `SaveLoadDialog` currently formats its own slot timestamps and is intentionally unchanged until HPA-384; HPA-380 must not modify that dialog just to align its display formatting.

Recommended helper shape:

```csharp
internal static SaveSlotInfo? SelectContinueSave(
    IReadOnlyList<SaveSlotInfo> slots)
{
    SaveSlotInfo? best = null;
    foreach (var candidate in slots)
    {
        if (!candidate.Exists || candidate.IsCorrupted)
            continue;

        if (best == null || IsBetterContinueCandidate(candidate, best))
            best = candidate;
    }

    return best;
}
```

Return the selected existing metadata object; do not create a parallel Continue model.

## 7. Root action availability and hard guards

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

`RefreshActionAvailability()` derives button disabled state from that predicate plus Continue eligibility.

Every root action handler—Continue, New Game, Load, Settings, and Quit—must also check `IsRootActionBlocked()` before doing work. Disabled buttons are presentation feedback, not the only correctness barrier: programmatic/double signal activation while a hosted child is active must also be a no-op.

When a child closes, cleanup refreshes action availability before host focus restoration runs.

## 8. Local `UIScreenHost` integration

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

## 9. Hosted Load flow

HPA-380 integrates but does not redesign `SaveLoadDialog`.

Direct Load activation:

1. preserve the current SaveManager-availability check;
2. preserve the current “any save exists” check;
3. create one `SaveLoadDialog`;
4. register it as `UIScreenKinds.SaveLoad`;
5. use Modal layer/priority, `UIProcessPolicy.Always`, visible cursor, Cancel-to-close, and `NodeLifetime.QueueFree`;
6. preserve child-dialog Cancel interception;
7. call `ShowDialog(Load)` once after successful initial `TryPresent`;
8. restore focus to `LoadButton` when direct Load closes.

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

### 9.1 Manual Load success

Use the existing full-load path. On success:

- assign `SaveManager.PendingLoadData`;
- request Game through the guarded transition path;
- let host teardown close Load.

### 9.2 Manual Load failure

Show the local themed error as a logical child of Load. On acknowledgement, close the error and its Load parent so the player returns to the correct Main Menu restoration target.

## 10. Hosted Settings flow

Reuse HPA-383 Settings exactly as the gameplay Pause host does:

- `UIScreenKinds.Settings`;
- Modal layer/priority;
- `UIProcessPolicy.Always`;
- visible cursor;
- Cancel-to-close;
- reserve Cancel while rebinding or an `OptionButton` popup is open;
- `InitialFocus = () => settings.InitialFocusTarget`;
- `SetPresented` uses `OpenSettings(showOverlay: false)` for later host-driven presentation changes;
- call `OpenSettings(showOverlay: false)` once after successful initial `TryPresent`;
- restore focus to `SettingsButton`;
- queue-free lifetime.

Delete direct Main Menu child ownership and manual post-close focus handling.

## 11. Local themed message/error path

HPA-572 owns the future shared confirmation/warning/error family. HPA-380 must not create a reusable message framework.

Use one private helper around an embedded themed `AcceptDialog`:

```csharp
private bool TryOpenMessage(
    string title,
    string message,
    Control? restoreFocus,
    UIScreenHandle? parent = null,
    bool closeParentOnDismiss = false);
```

The helper:

- applies `SiriusTheme.tres`;
- reuses existing `UIScreenKinds.SaveError` as the transitional concrete kind;
- uses Blocking priority and `UIScreenExclusiveGroups.BlockingPrompt`;
- uses `UIProcessPolicy.Always`;
- uses visible cursor and `UILowerLayerPolicy.VisibleInert`;
- focuses the built-in OK button;
- maps Confirmed/Canceled/CloseRequested to one host close;
- allows only one active message;
- never uses a timer;
- restores a supplied root control for root messages;
- relies on logical parent restoration for child messages.

This stays private to `MainMenu` so HPA-572 can later replace concrete call sites cleanly.

## 12. Continue activation and failure fallback

When Continue is pressed:

1. return if `IsRootActionBlocked()` or no selected Continue metadata exists;
2. call `LoadSlot(_continueSave.SlotIndex)`;
3. on success, assign `PendingLoadData` and request Game;
4. on failure, never start New Game;
5. if `SaveManager` remains available, call `TryOpenHostedLoad(_continueButton)`;
6. when Load opens, show “Failed to load the selected save.” as a hosted child error;
7. dismissing that error leaves Load open;
8. later closing fallback Load restores `ContinueButton` focus;
9. if the save system is unavailable or Load cannot be hosted, show the error as a root message and stay on Main Menu.

Do not scan for a second-best save after full-load failure. The player chooses explicitly from Load.

## 13. Scene-transition gate and host teardown

Mirror the existing narrow pattern in `Game.cs`; do not extract a navigation service.

```csharp
private const string GameScenePath = "res://scenes/game/Game.tscn";
private string? _pendingScenePath;
private bool _sceneChangeCommitted;

private void RequestSceneChange(string path);
private void ContinueSceneChangeAfterUiTeardown();
```

`RequestSceneChange` commits once, refreshes action availability immediately, and starts host teardown.

`ContinueSceneChangeAfterUiTeardown`:

1. returns if Main Menu is invalid/out of tree;
2. calls `UIScreenHost.PrepareForTeardown()`;
3. retries with `CallDeferred()` only when teardown returns `Deferred`;
4. after `Complete`, invokes `ChangeSceneToFile(path)` once.

New Game clears `PendingLoadData`. Successful Continue/Load assign it.

No transition animation, loading screen, router, retry framework, or cross-scene navigation singleton is added.

## 14. Responsive behavior

Use existing `SiriusUiMetrics` only:

- minimum logical viewport: 640×360;
- compact decision: `SiriusUiMetrics.IsCompact(viewportSize)`;
- safe margin: `SiriusUiMetrics.SafeMargin(compact)`;
- max centred content width: `SiriusUiMetrics.MaximumContentWidth`;
- minimum action height: `SiriusUiMetrics.MinimumTarget(compact).Y`.

`SafeFrame` is authored FullRect and receives runtime offsets. At compact size:

- use compact typography;
- reduce rail spacing;
- keep every action at the 40px minimum target;
- hide Continue timestamp first;
- keep slot/player/level/floor summary visible;
- keep Select hint compact.

Do not add another breakpoint or per-resolution position table.

## 15. Test design

### 15.1 Continue policy

Cover:

- no eligible slots;
- corrupted/missing slots excluded;
- newest stored timestamp wins;
- usable timestamp beats `DateTime.MinValue`;
- equal timestamps prefer autosave then manual 0/1/2;
- all-MinValue tie follows the same rank.

### 15.2 Scene/layout

`MainMenuSceneTest` proves:

- Theme/background are scene-authored;
- required menu/summary/host nodes exist before `_Ready()`;
- old centred `VBoxContainer` is gone;
- 640×360 and 1280×720 content fits the safe frame and target-size contract;
- every shared verification viewport respects the content cap.

The deep responsive fixture must explicitly inject an eligible Continue save and refresh presentation/layout before assertions. This forces `ContinueSummary` visible so the 640×360 test cannot pass merely because the new summary is hidden.

At compact size assert:

- summary is visible;
- slot/detail labels have non-zero size and remain inside the summary/safe frame;
- timestamp is hidden;
- all five actions meet the 40px target;
- the whole rail remains enclosed by the safe frame.

At 1280×720 also assert the timestamp is visible for a non-MinValue save.

### 15.3 Host/focus/lifecycle

Cover:

- production scene owns exactly one `UIScreenHost`;
- Settings opens once and restores Settings focus;
- direct Load opens once and restores Load focus;
- Continue fallback Load restores Continue focus when closed;
- all root actions are disabled while a hosted child is active;
- at least one programmatic root action (New Game or Quit) is also proven to no-op while a child is active, validating the hard guard rather than only disabled state;
- root Cancel is a no-op;
- one root message at a time.

### 15.4 Continue/load/transition

Cover:

- Continue full-load failure opens Load + child error and does not commit scene change;
- dismissing Continue failure error keeps Load open;
- closing fallback Load returns focus to Continue;
- manual Load failure dismisses back to Main Menu;
- Continue success sets `PendingLoadData` and requests Game once;
- New Game clears `PendingLoadData` and requests Game once;
- repeated activation after `_sceneChangeCommitted` cannot request a second scene change.

## 16. File ownership

Expected implementation changes:

```text
scenes/ui/MainMenu.tscn
scripts/ui/MainMenu.cs
tests/ui/MainMenuTest.cs
tests/ui/MainMenuSceneTest.cs
```

Do not modify `SaveManager`, `SaveLoadDialog`, `SettingsMenuController`, `UIScreenHost`, Theme resources, or `project.godot` unless implementation proves the design impossible; reassess scope before expanding.

## 17. Risks and mitigations

### Compact vertical overflow

The Continue summary adds height to a five-action rail. Mitigate with compact typography, 4px compact spacing, hidden timestamp, and a test that forces the summary visible at 640×360.

### Root controls active behind child presentation

Disabled state alone is insufficient for programmatic/double signals. Centralize `IsRootActionBlocked()` and guard every root handler.

### Wrong focus after Continue fallback

A single hard-coded Load restoration target would return focus to Load even when Continue started the flow. Pass the restoration control into `TryOpenHostedLoad` and test the Continue path explicitly.

### Scope leakage into legacy Load

`SaveLoadDialog` currently owns legacy slot formatting and runtime construction. Do not change it for HPA-380; HPA-384 owns that redesign.

### Scope leakage into messages

Keep the themed `AcceptDialog` helper private and transitional. HPA-572 owns shared typed confirmation/warning/error presentation.

## 18. Acceptance mapping

| HPA-380 acceptance | Design response |
| --- | --- |
| Main Menu owns one local production `UIScreenHost` | authored host configured in `_EnterTree()` |
| Approved visual system | scene-authored Theme/background/lower-left rail/Continue summary |
| Deterministic Continue | pure `SaveSlotInfo` selector with exact eligibility/order/ties |
| Existing actions preserved | existing domain methods and legacy child flows retained |
| Focus/layout usable | explicit initial/return focus and forced-summary responsive tests |
| No double activation | hard root-action guard + one-way scene commitment + host teardown |

## 19. Explicit non-goals

- Save-format migration or timestamp normalization;
- cloud saves, extra slots, thumbnails, renaming, repair, or deletion;
- redesigning Load or Settings;
- reusable root-screen registration;
- generic navigation/router abstractions;
- shared message/error framework;
- transition animation/loading screen;
- new Theme tokens/components.
