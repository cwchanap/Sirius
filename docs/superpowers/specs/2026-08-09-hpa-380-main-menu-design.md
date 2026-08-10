# HPA-380 Main Menu and Deterministic Continue Design

**Status:** Implementation-ready planning candidate  
**Linear:** HPA-380  
**Scope:** Main Menu vertical slice only

## 1. Why HPA-380 is next

HPA-381 has delivered the compact exploration HUD implementation, while the foundation work HPA-354, shared Theme/components HPA-377, and reusable `UIScreenHost` HPA-378 are already complete. HPA-380 is the next independent, high-priority player-facing slice in the Sirius delivery order whose only declared blocker is complete.

This task should remain a vertical screen migration, not a second UI-foundation project. The Main Menu is the first root outside gameplay that consumes `UIScreenHost`, so it should prove the existing host contract with the minimum root integration needed for its real child flows.

## 2. Current state

The production Main Menu currently has four buttons in a generic centred `VBoxContainer`: Start Game, Load Game, Settings, and Quit. `MainMenu.cs` directly creates/parents `SaveLoadDialog`, directly parents the scene-authored Settings screen, directly creates unthemed `AcceptDialog` messages, and changes scenes immediately.

Existing reusable pieces already cover almost everything HPA-380 needs:

- `resources/ui/theme/SiriusTheme.tres` and `SiriusThemeTypes`
- `SiriusUiMetrics` for 640×360 minimum, compact reflow, safe margins, target sizes, and the 1600px maximum content width
- `SiriusInputHint` for binding/device-aware hints
- `UIScreenHost` for child presentation lifecycle, Cancel dispatch, focus restoration, embedded subwindows, and teardown
- scene-authored `SettingsMenu.tscn` / `SettingsMenuController`
- legacy `SaveLoadDialog` for the current Load flow until HPA-384 redesigns it
- `SaveManager.GetSaveSlotInfo`, `LoadGame`, `LoadAutosave`, and `PendingLoadData`
- the HPA-373 approved Main Menu composition: lower-left navigation, Continue summary, New Game, Load, Settings, Quit, and input hints

The missing work is therefore composition and orchestration, not new infrastructure.

## 3. Approaches considered

### A. Scene-owned Main Menu plus local hosted child flows — selected

Keep `MainMenu.tscn` and `MainMenu.cs` as the root presentation owner. Scene-author the approved layout, add one local `UIScreenHost`, calculate Continue from existing `SaveSlotInfo`, and host the existing Load/Settings views plus one local themed message path.

Advantages:

- smallest change that satisfies HPA-380
- reuses every relevant foundation already merged
- keeps Save/Settings domain ownership unchanged
- lets HPA-384 and HPA-572 replace their own legacy presentation later
- avoids introducing a new presenter/view-model/navigation service for five buttons

Trade-off: because the Main Menu surface itself is not a hosted entry, host lower-layer effects do not make those root controls inert. HPA-380 will explicitly disable the five root actions while a hosted child is active. This is a local root responsibility, not a second stack system.

### B. Register the entire Main Menu surface as a `UIScreenHost` screen entry

Split or dynamically reparent the root content into the host `ScreenLayer`, then rely on host lower-layer effects to inert it.

Rejected for HPA-380. It adds a new base-screen scene/lifecycle solely to avoid a tiny local action-disable rule. The Main Menu is already the scene root, and there is no second root consumer proving that an application-root entry abstraction is useful.

### C. Add a reusable Main Menu presenter/navigation coordinator

Extract Continue selection, modal routing, action state, and scene transitions into a service or presenter.

Rejected. There are only five actions and one root. `SaveManager` and `UIScreenHost` already own the domain/presentation responsibilities that would justify separate abstractions. A coordinator would mostly forward calls and create more state to synchronize.

## 4. Selected architecture

`MainMenu` remains the single root controller. The implementation adds one pure policy helper and only two narrow virtual test seams:

```csharp
internal static SaveSlotInfo? SelectContinueSave(
    IReadOnlyList<SaveSlotInfo> slots);

protected virtual SaveData? LoadSlot(int slot);
protected virtual Error ChangeSceneToFile(string path);
```

`SelectContinueSave` is the deterministic Continue policy. `LoadSlot` allows a focused full-load-failure test without introducing a SaveManager interface. `ChangeSceneToFile` follows the existing `RequestApplicationQuit` test seam and allows scene-transition/double-activation tests without actually replacing the test scene.

No new singleton, navigation framework, save service, settings facade, view model, or compatibility layer is introduced.

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

The existing `ui_main_menu_background.png` stays the background. Move the resource reference into the scene and use aspect-preserving cover crop. `MainMenu.cs` no longer loads or configures the image at runtime.

### 5.2 Navigation rail

The rail occupies the lower-left portion of the safe frame instead of the centre of the castle/moon focal area. It uses the approved Sirius typography and button variations rather than introducing a bespoke radial-control implementation.

The visual hierarchy is:

1. `SIRIUS` wordmark
2. Continue
3. selected Continue save summary, only when Continue is eligible
4. New Game
5. Load
6. Settings
7. Quit
8. binding-aware Select hint

The “partial navigation orbit” direction is expressed through placement, spacing, existing Sirius panel/border language, and the open scenic background. HPA-380 does not add a custom orbital navigation widget, shader, or drawing system.

### 5.3 Continue summary

`ContinueSummary` is a normal themed `PanelContainer`, not a new reusable component. It exposes only existing metadata:

- slot display name (`Autosave` or `Slot N`)
- player name and level
- floor name
- stored timestamp when it is not `DateTime.MinValue`

The timestamp is formatted as stored; HPA-380 does not convert/normalize time zones. At compact size the timestamp is hidden first because it is supporting metadata. Slot/player/level/floor remain visible.

When no eligible Continue save exists, the summary is hidden and Continue is disabled.

## 6. Deterministic Continue policy

On Main Menu initialization, read exactly slots 0, 1, 2, and 3 through `SaveManager.GetSaveSlotInfo` and pass the four values to `SelectContinueSave`.

A candidate is eligible only when:

```text
Exists == true AND IsCorrupted == false
```

Selection order is:

1. greater `Timestamp` wins
2. `DateTime.MinValue` is naturally older than usable timestamps
3. equal timestamps use this fixed slot rank:
   - autosave slot 3
   - manual slot 0
   - manual slot 1
   - manual slot 2

The comparison uses the stored `DateTime` values directly. There is no timezone conversion, migration, malformed-save repair, timestamp backfill, or compatibility wrapper.

Recommended implementation shape:

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

The helper returns the selected existing metadata object rather than creating a parallel Continue model.

## 7. Root focus and action availability

Initial focus is deterministic:

- eligible Continue → `ContinueButton`
- otherwise → `NewGameButton`

The Main Menu root Cancel action is a consumed no-op. It does not quit, start a game, or open another surface.

While any hosted child flow is active, all five root actions are disabled so mouse, keyboard, and gamepad cannot activate the scene behind the child. Do not overwrite Continue eligibility permanently: centralize action state in a small `RefreshActionAvailability()` method that derives disabled state from:

- `_sceneChangeCommitted`
- whether the host currently has an active child entry
- whether `_continueSave` is present

When a child closes, its host cleanup runs before focus restoration. Cleanup refreshes root action availability, then the host restores focus to the invoking action.

## 8. Local `UIScreenHost` integration

Add one authored `UIScreenHost` child to `MainMenu.tscn`. Configure it from `MainMenu._EnterTree()` before the host reaches `_Ready()`.

Main Menu configuration:

```csharp
_screenHost.Configure(new UIScreenHostOptions
{
    CoreCancelActions = new HashSet<StringName> { "ui_cancel" },
    RootCancelFallback = _ => UIRootCancelResult.Consumed
});
```

No HUD policy, gameplay-input callback, pause ownership, or new host API is needed in Main Menu.

The globally enabled embedded-subwindow policy already used by gameplay remains the mechanism for hosting `SaveLoadDialog` and the local `AcceptDialog` message.

## 9. Hosted Load flow

HPA-380 integrates, but does not redesign, `SaveLoadDialog`.

When Load is activated:

1. keep the current SaveManager-availability check
2. keep the current “any save file exists” check
3. create one `SaveLoadDialog`
4. register it with the local host as `UIScreenKinds.SaveLoad`
5. use `UIProcessPolicy.Always`, Modal layer/priority, visible cursor, Cancel-to-close, and `NodeLifetime.QueueFree`
6. preserve child overwrite-dialog Cancel interception even though Main Menu uses Load mode; this keeps the existing dialog contract intact
7. call `ShowDialog(Load)` once after a successful initial `TryPresent`, matching the existing host first-presentation contract
8. restore focus to `LoadButton` when the flow closes

No HPA-384 save-card layout, delete/repair operation, thumbnails, or extra slot behavior is pulled into this ticket.

### 9.1 Manual Load success

Use the existing full-load path through `LoadGame`/`LoadAutosave`. On success:

- assign `SaveManager.PendingLoadData`
- request the Game scene transition through the new guarded transition path
- let host teardown close the Load flow rather than manually racing focus restoration

### 9.2 Manual Load failure

Show the local themed error as a logical child of the Load entry. On acknowledgement, close both the message and its Load parent so the player returns to Main Menu, matching the existing terminal failure behavior without a free-floating popup.

## 10. Hosted Settings flow

Reuse the merged HPA-383 Settings scene/controller exactly as the gameplay Pause host does:

- `UIScreenKinds.Settings`
- Modal layer/priority
- `UIProcessPolicy.Always`
- visible cursor
- Cancel-to-close
- `InterceptCancel` reserves Cancel while Settings is rebinding or an `OptionButton` popup is open
- `InitialFocus = () => settings.InitialFocusTarget`
- `SetPresented` calls `OpenSettings(showOverlay: false)` for host-driven presentation changes
- call `OpenSettings(showOverlay: false)` once after successful initial `TryPresent`
- restore focus to `SettingsButton`
- queue-free lifetime

Delete the direct Main Menu `AddChild(settings)` lifecycle and the manual post-close focus handoff; the host owns those concerns after HPA-380.

## 11. Local themed message/error path

HPA-572 owns the future shared confirmation/warning/error component family. HPA-380 therefore does not create `SiriusMessageDialog`, a message service, or another modal shell.

For the concrete Main Menu availability/save/load messages in this ticket, use one private host helper around an embedded `AcceptDialog`:

```csharp
private bool TryOpenMessage(
    string title,
    string message,
    Control? restoreFocus,
    UIScreenHandle? parent = null,
    bool closeParentOnDismiss = false);
```

The helper:

- applies `SiriusTheme.tres` directly to the embedded dialog
- reuses existing `UIScreenKinds.SaveError` as the transitional concrete error kind
- uses Blocking input priority and `UIScreenExclusiveGroups.BlockingPrompt`
- uses `UIProcessPolicy.Always`
- sets visible cursor and `UILowerLayerPolicy.VisibleInert`
- focuses the built-in OK button
- maps Confirmed/Canceled/CloseRequested to one host close
- allows only one active message
- never uses a timer
- restores the invoking Main Menu control when it is a root message
- relies on logical parent focus restoration when it is a child of Load

This is deliberately local transitional code. HPA-572 can replace the concrete call sites later without first dismantling a new generic framework or extra kind taxonomy.

## 12. Continue activation and failure fallback

When Continue is pressed:

1. ignore the action if no selected Continue metadata exists or a scene change is already committed
2. call `LoadSlot(_continueSave.SlotIndex)` using the existing full-load path
3. on success, assign `PendingLoadData` and request the Game scene transition
4. on failure, do **not** clear into New Game
5. if `SaveManager` remains available, open the existing Load flow
6. if Load opens, show “Failed to load the selected save.” as a hosted child error over Load; dismissing the error leaves Load open
7. if the save system is unavailable or Load cannot be hosted, show the same failure as a root hosted message and stay on Main Menu

This makes Continue deterministic without creating a fallback-save scan after full-load failure. HPA-380 selects exactly one candidate from metadata; if its full load fails, the player chooses explicitly from Load.

## 13. Scene-transition gate and host teardown

Mirror the already-proven narrow teardown pattern in `Game.cs`; do not extract it into a shared navigation service.

`MainMenu` owns:

```csharp
private const string GameScenePath = "res://scenes/game/Game.tscn";
private string? _pendingScenePath;
private bool _sceneChangeCommitted;

private void RequestSceneChange(string path);
private void ContinueSceneChangeAfterUiTeardown();
```

`RequestSceneChange` is idempotent after the first committed request. It refreshes action availability immediately so all five actions become disabled.

`ContinueSceneChangeAfterUiTeardown`:

1. stops if the Main Menu object is no longer valid/in-tree
2. calls `UIScreenHost.PrepareForTeardown()`
3. if teardown returns `Deferred`, schedules the same continuation with `CallDeferred()`
4. after `Complete`, calls the virtual `ChangeSceneToFile(path)` seam once

New Game clears `PendingLoadData` before requesting Game. Continue/Load success set `PendingLoadData` before requesting Game.

No transition animation, loading screen, router, retry framework, or cross-scene navigation singleton is added.

## 14. Responsive behavior

Use the existing `SiriusUiMetrics` values only.

- minimum logical viewport: 640×360
- compact decision: `SiriusUiMetrics.IsCompact(viewportSize)`
- side/top/bottom safe margin: `SiriusUiMetrics.SafeMargin(compact)`
- max centred content width: `SiriusUiMetrics.MaximumContentWidth`
- minimum action height: `SiriusUiMetrics.MinimumTarget(compact).Y`

`SafeFrame` is authored FullRect and receives runtime offsets calculated from the existing margin/max-width contract. At standard sizes the rail uses the left/lower portion of the safe frame. At compact size:

- use compact typography variations
- reduce rail spacing
- keep every action at the shared 40px minimum target
- hide `ContinueTimestampLabel` before reducing essential text
- keep slot/player/level/floor summary visible
- keep the Select hint compact

Do not add another breakpoint or per-resolution layout table.

Validation is deep at 640×360 and 1280×720 and light at every existing `SiriusUiMetrics.VerificationViewports` size. The Main Menu does not need a unique third aspect-ratio test because its layout logic is only standard versus compact and the shared viewport list already includes 4:3, 16:10, 16:9, and ultrawide.

## 15. Tests

### 15.1 Pure Continue policy

Cover:

- no eligible slots → null
- corrupted/missing slots excluded
- newest stored timestamp wins
- usable timestamp beats `DateTime.MinValue`
- equal timestamps prefer autosave then manual 0/1/2
- all-MinValue tie follows the same fixed rank

### 15.2 Scene/layout

Add `MainMenuSceneTest` to prove:

- the Theme and background are scene-authored
- required menu/summary/host nodes exist before `_Ready()`
- the old centred `VBoxContainer` contract is gone
- all five action controls are inside the safe frame
- 640×360 compact content fits without overlap/clipping and uses at least 40px targets
- 1280×720 standard content fits and uses at least 44px targets
- every shared verification viewport keeps the safe frame/menu rail inside the viewport and respects the 1600px max content width

### 15.3 Root/host behavior

Extend `MainMenuTest` for:

- Continue initial focus versus New Game fallback
- Settings opens once through the host, preserves Settings initial focus, and restores Settings button focus
- Load opens once through the host and restores Load button focus
- all root actions are disabled while a hosted child is active and restored afterward according to Continue eligibility
- root Cancel is a no-op
- one root message at a time
- Continue full-load failure opens Load plus the themed child error and never requests Game
- dismissing the Continue failure error keeps Load open
- manual Load failure dismisses back to Main Menu
- successful Continue sets `PendingLoadData` and requests Game once
- New Game clears `PendingLoadData` and requests Game once
- repeated action activation after `_sceneChangeCommitted` cannot request a second scene change

Do not build a Cartesian viewport × input-device × save-state matrix. The pure policy tests and focused lifecycle/layout tests cover the real branches.

## 16. File boundary

Expected production changes:

- `scenes/ui/MainMenu.tscn`
- `scripts/ui/MainMenu.cs`

Expected tests:

- `tests/ui/MainMenuTest.cs`
- `tests/ui/MainMenuSceneTest.cs` (new)

Planning documents:

- `docs/superpowers/specs/2026-08-09-hpa-380-main-menu-design.md`
- `docs/superpowers/plans/2026-08-09-hpa-380-main-menu.md`

Do not modify for HPA-380 unless implementation proves the design impossible:

- `scripts/save/SaveManager.cs`
- `scripts/ui/SaveLoadDialog.cs`
- `scripts/ui/SettingsMenuController.cs`
- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/hosting/*`
- `resources/ui/theme/SiriusTheme.tres`
- `project.godot`

## 17. Risks and mitigations

### Host does not inert the unregistered Main Menu root

Mitigation: root action availability is explicitly derived from host child presence. Do not register/reparent the entire menu solely to gain lower-layer effects.

### Continue metadata passes but full deserialization fails

Mitigation: test through the narrow virtual `LoadSlot` seam; open legacy Load plus themed child error, never New Game.

### Hosted Settings initial presentation is opened twice

Mitigation: preserve the already-documented host first-presentation behavior: `TryPresent` registers the view, then Main Menu calls `OpenSettings(showOverlay: false)` exactly once initially. `SetPresented` remains for later host-driven state changes.

### Scene change races with an open child

Mitigation: one `_sceneChangeCommitted` gate plus `PrepareForTeardown()` before scene replacement, matching the existing Game root pattern.

### Compact menu exceeds 360px height

Mitigation: 40px action targets are non-negotiable; compact spacing/typography and hiding only the timestamp are the first reductions. Scene tests assert actual enclosure at 640×360 rather than relying on nominal sizes.

## 18. Acceptance mapping

- **One local production `UIScreenHost`:** authored in `MainMenu.tscn`, configured in `_EnterTree()`.
- **Approved visual system:** scene uses Sirius Theme, lower-left rail, Continue summary, safe frame, existing metrics/input hint.
- **Deterministic Continue:** one pure selector implements eligibility/timestamp/tie rules exactly.
- **Primary actions preserved:** New Game, Load, Settings, Quit continue using existing managers/controllers and current full-load behavior.
- **Focus/layout usable:** explicit initial/restore targets plus 640×360 and shared viewport coverage.
- **No double scene activation:** `_sceneChangeCommitted` and host teardown gate every Game transition.

## 19. Explicit non-goals

HPA-380 does not:

- redesign Save/Load cards or slots (HPA-384)
- create the shared confirmation/warning/error family (HPA-572)
- add save migration, timezone normalization, repair, deletion, renaming, cloud saves, thumbnails, or extra slots
- change Settings data or Settings layout
- create a generic root-navigation framework
- create a generic Main Menu presenter/view model
- add transition animation or loading-screen infrastructure
- change game-domain rules
