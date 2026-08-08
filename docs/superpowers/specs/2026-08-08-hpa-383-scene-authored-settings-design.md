# HPA-383 Scene-Authored Settings Design

**Date:** 2026-08-08  
**Status:** Approved for implementation planning  
**Linear:** HPA-383 — Migrate Sirius settings to a scene-authored themed screen

## 1. Purpose

Migrate Sirius Settings from a mostly runtime-built utility panel to a scene-authored, themed, responsive screen while preserving all existing settings semantics.

This is a presentation migration. `SettingsManager` remains the persistence and application owner, `SettingsData` remains the staged data shape, and `SettingsMenuController` remains the screen controller. HPA-383 must not turn into a settings architecture rewrite.

## 2. Current state

`scenes/ui/SettingsMenu.tscn` currently provides only a root `Control`, a dark background, a generic panel/scroll skeleton, and an empty content container.

`scripts/ui/SettingsMenuController.cs` constructs the player-facing UI at runtime through `BuildUI()`, `BuildAudioTab()`, `BuildDisplayTab()`, `BuildGameplayTab()`, `BuildControlsTab()`, `AddSliderRow()`, and `AddKeyRow()`. The same controller also owns behavior that must be preserved:

- a cloned `_editedSettings` staging snapshot,
- Apply/Cancel semantics,
- custom-resolution preservation,
- difficulty and autosave binding,
- reserved-key and duplicate-key validation,
- remappable Pause/Cancel handling,
- key-capture cancellation,
- `OptionButton` popup cancellation,
- mouse interaction,
- keyboard/gamepad GUI navigation, and
- one-shot `Closed` emission.

HPA-382 already integrates this controller into the gameplay `UIScreenHost` as a child of Pause. The hosted path gives Settings the correct parent relationship, pause inheritance, cursor ownership, lower-layer inertness, Cancel interception, cleanup, and focus-restoration boundary.

Main Menu still instantiates Settings directly. HPA-380 owns the Main Menu `UIScreenHost` and broader Main Menu redesign, so HPA-383 preserves the current direct Main Menu entry rather than pulling HPA-380 forward.

## 3. Goals

HPA-383 will:

1. Move all static Settings structure, copy, rows, buttons, and layout constraints into `SettingsMenu.tscn`.
2. Reuse `SiriusModalShell` and the existing Sirius Theme.
3. Preserve every current settings field and persistence behavior.
4. Preserve current key-capture, dropdown, mouse, keyboard, and gamepad behavior.
5. Implement the approved standard and compact Settings compositions.
6. Make Settings expose an explicit first-control focus target for `UIScreenHost` and direct Main Menu invocation.
7. Restore focus to the invoking Settings action after closing from Pause or Main Menu.
8. Use themed inline feedback for recoverable Settings validation failures.
9. Keep all supported desktop landscape viewports usable, with deep coverage at 1280×720 and 640×360.

## 4. Non-goals

HPA-383 will not:

- change `SettingsData` serialization or add new persisted settings;
- add Reduced Motion; HPA-541 owns that;
- add another Settings view model, presenter, repository, or state machine;
- split the four Settings pages into separate controllers;
- create generic settings-row components before another real screen needs them;
- create another modal shell;
- add a Main Menu `UIScreenHost`; HPA-380 owns it;
- build generic confirmation/error infrastructure; HPA-572 owns it;
- redesign Save/Load or Pause;
- change audio, display, difficulty, autosave, or input domain behavior;
- add localization infrastructure;
- add touch-first, portrait, or mobile layouts.

## 5. Ownership rules

### 5.1 Scene owns presentation

The scene owns:

- title and static labels,
- page selector buttons,
- page containers,
- sliders, checkboxes, option buttons, and keybinding buttons,
- Apply and Cancel controls,
- the themed inline error surface,
- Theme variations,
- minimum target sizes,
- spacing,
- scroll containers, and
- responsive container structure.

The controller looks up those nodes and binds behavior. It does not create replacement controls at runtime.

### 5.2 Controller owns screen behavior

`SettingsMenuController` continues to own:

- the `_editedSettings` clone,
- dynamic `OptionButton` items,
- value population,
- staged keybinding changes,
- validation,
- Apply/Cancel,
- input interception local to Settings,
- page selection, and
- compact/standard layout switching.

No new abstraction is inserted between `SettingsMenuController` and `SettingsManager`.

### 5.3 Existing host owns hosted presentation lifecycle

When opened from Pause, the gameplay `UIScreenHost` remains the only owner of parent/child presentation state, tree-pause inheritance, cursor state, lower-layer interactivity, Cancel priority, and focus restoration.

Settings must not write `SceneTree.Paused`, change gameplay-input suppression directly, or implement its own parent stack.

When opened from Main Menu, HPA-383 preserves the current direct-child flow. The direct caller owns initial focus and restores focus to the existing Main Menu Settings button on close. HPA-380 later replaces that invocation with the Main Menu host.

## 6. Scene structure

`SettingsMenu.tscn` remains the production scene path.

The root stays a full-rect `SettingsMenuController`. Its static children become:

```text
SettingsMenuController
├── Background                         # Panel + SiriusScrim; visible for direct Main Menu use
└── ModalShell                         # SiriusModalShell; title "Settings"; Large
    ├── BodyHost                       # inside ModalShell.BodyScroll
    │   ├── SettingsFrame              # responsive GridContainer
    │   │   ├── PageSelector           # responsive GridContainer
    │   │   │   ├── AudioPageButton
    │   │   │   ├── DisplayPageButton
    │   │   │   ├── GameplayPageButton
    │   │   │   └── ControlsPageButton
    │   │   └── PageDeck               # TabContainer; built-in tabs hidden
    │   │       ├── AudioPage/AudioScroll/AudioRows
    │   │       ├── DisplayPage/DisplayScroll/DisplayRows
    │   │       ├── GameplayPage/GameplayScroll/GameplayRows
    │   │       └── ControlsPage/ControlsScroll/ControlsRows
    │   └── ErrorPanel                  # PanelContainer + SiriusErrorPanel; hidden by default
    │       └── ErrorLabel
    └── ActionsHost
        ├── ApplyButton
        └── CancelButton
```

`SiriusModalShell.BodyHost` is physically nested inside the shell's `BodyScroll`. Settings cannot allow that outer scroller to compete with its four page-local scrollers. `SettingsMenuController` therefore binds the existing `%BodyScroll` through the shell instance and disables both shell scroll axes once during `_Ready()`. This is a per-screen integration setting, not a new `SiriusModalShell` API.

The concrete setting controls and row labels use stable unique names:

- Audio:
  - `MasterVolumeLabel`, `MasterSlider`, `MasterValueLabel`
  - `MusicVolumeLabel`, `MusicSlider`, `MusicValueLabel`
  - `SfxVolumeLabel`, `SfxSlider`, `SfxValueLabel`
- Display:
  - `FullscreenLabel`, `FullscreenCheck`
  - `ResolutionLabel`, `ResolutionOption`
- Gameplay:
  - `DifficultyLabel`, `DifficultyOption`
  - `AutoSaveLabel`, `AutoSaveCheck`
- Controls:
  - `InventoryKeyLabel`, `InventoryKeyButton`
  - `InteractKeyLabel`, `InteractKeyButton`
  - `PauseKeyLabel`, `PauseKeyButton`

The existing private controller field names may remain so current behavior tests can continue exercising the same state without broad test churn.

## 7. Row composition

Each `*Rows` page container has exactly two logical cells per setting in standard mode and one logical cell per row item in compact mode.

Audio rows preserve the current visible percentage labels by packing the slider and value into one control cell:

```text
MasterVolumeLabel | MasterControlCell(HSlider MasterSlider + Label MasterValueLabel)
MusicVolumeLabel  | MusicControlCell(HSlider MusicSlider + Label MusicValueLabel)
SfxVolumeLabel    | SfxControlCell(HSlider SfxSlider + Label SfxValueLabel)
```

Other rows use:

```text
Label | CheckBox / OptionButton / KeyButton
```

At compact size, changing the row grid to one column places the label above its control cell without duplicating controls or dropping the audio value labels.

## 8. Layout and responsive behavior

### 8.1 Standard layout

At non-compact sizes:

- `SiriusModalShell` uses the Large size class.
- `SettingsFrame.Columns = 2`.
- `PageSelector.Columns = 1`, producing the approved left rail.
- Each page's `*Rows.Columns = 2`.
- `PageDeck` and its page-local `ScrollContainer`s use expand/fill size flags instead of hard-coded height math.
- Only the active page is visible.
- Apply and Cancel remain in `ActionsHost`, outside page scrolling.

### 8.2 Compact layout

When `SiriusUiMetrics.IsCompact(viewportSize)` is true:

- `SettingsFrame.Columns = 1`.
- `PageSelector.Columns = 4`, producing the short top selector.
- Each page's `*Rows.Columns = 1` so long labels are above their controls rather than squeezed beside them.
- The active page's own `ScrollContainer` owns vertical overflow.
- Apply and Cancel stay fixed in `ActionsHost`.
- The shell uses compact title sizing and safe margins through `SiriusModalShell`.

There is no `pageHeight` or viewport-minus-magic-offset calculation. The shell width contract, container size flags, and page-local scrolling determine body height.

### 8.3 Supported viewports

Behavior must remain usable at:

- 640×360
- 1024×768
- 1280×720
- 1440×900
- 1920×1080
- 2560×1080
- 2560×1440

1280×720 and 640×360 receive detailed assertions. The remaining sizes receive a light fit/usability smoke check rather than a combinatorial matrix.

## 9. Page selection

The four scene-authored page buttons share one scene-authored `ButtonGroup`, use toggle mode, and Audio starts selected with `PageDeck.CurrentTab = 0`.

Each page button has one named `Pressed` handler that calls `SelectPage(index)`. `SelectPage` changes `PageDeck.CurrentTab` and ensures the destination button is pressed; `ButtonGroup` owns exclusivity. There is no second manual loop that continuously re-synchronizes all four buttons.

No navigation history abstraction is added.

## 10. Initial focus contract

The migration preserves the existing Settings focus outcome instead of redefining it around the page-selector chrome.

`SettingsMenuController` exposes the first interactive control of the active page:

```csharp
public Control InitialFocusTarget => _pageDeck.CurrentTab switch
{
    0 => _masterSlider,
    1 => _fullscreenCheck,
    2 => _difficultyOption,
    3 => _inventoryKeyBtn,
    _ => _masterSlider
};
```

The default Audio page therefore still resolves to the master-volume slider.

`OpenSettings()` populates and shows the screen but does **not** call `GrabFocus()` itself. This prevents hosted Settings from racing the `UIScreenHost` deferred initial-focus path.

Focus ownership is explicit at each invocation boundary:

- Pause-hosted Settings supplies `InitialFocus = () => settings.InitialFocusTarget`; the host applies it.
- Direct Main Menu Settings calls `settings.InitialFocusTarget.GrabFocus()` once after `OpenSettings()`.
- Closing hosted Settings returns through the host restoration lease to Pause's Settings button.
- Closing direct Main Menu Settings restores focus to the existing Main Menu Settings button.

## 11. Data binding and staged edits

`OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)` remains the public entry point.

It continues to:

1. cancel any active key capture;
2. reset the one-shot close guard;
3. show or hide the scene-authored `Background` scrim according to `showOverlay`;
4. clear stale inline error state;
5. clone the supplied snapshot, or the current `SettingsManager` snapshot, into `_editedSettings`;
6. populate all scene-authored controls;
7. show the screen; and
8. enable its input processing.

It does not decide which parent owns focus.

`PopulateControls()`, `PopulateResolutionOption()`, `ResolveSelectedResolution()`, and `ResolveSelectedDifficulty()` remain behavior-oriented methods. They are not moved into the scene.

The four resolution presets and three difficulty values remain controller-owned dynamic data because they are values, not layout.

## 12. Input and Cancel behavior

HPA-383 preserves the current priority exactly:

1. An open `OptionButton` popup consumes Cancel before Settings.
2. Active key capture consumes Cancel before Settings close.
3. Capturing the Pause binding allows Escape to become the new binding.
4. Otherwise Pause/Cancel closes Settings and discards staged edits.
5. Mouse input reaches child GUI controls.
6. Keyboard/gamepad GUI-navigation actions reach Godot focus handling.
7. Unrelated input is consumed so it cannot leak into gameplay.

The gameplay host keeps the existing `InterceptCancel` rule based on `settings.IsRebinding` and `settings.IsPopupOpen`.

No duplicate Cancel logic is added to `Game`.

## 13. Validation and feedback

Recoverable Settings validation stays inline for HPA-383.

`SettingsMenu.tscn` provides a hidden `PanelContainer` named `ErrorPanel` with `theme_type_variation = SiriusErrorPanel` and a child `ErrorLabel`. Godot resolves an explicitly assigned theme variation before falling back to the control's class, so this can retain the layout behavior of `PanelContainer` while using the existing Sirius error style; no new Theme type is required.

`ShowError(string)` makes the panel and label visible without replacing `_editedSettings` or closing the screen.

The current messages remain semantically equivalent:

- `Key reserved`
- `Key already in use`
- `Settings system unavailable.`
- `Invalid settings — could not apply.`

Starting or canceling key capture clears the inline error state.

HPA-572 may later migrate appropriate non-inline errors to the shared host-managed error path. HPA-383 does not pre-build that infrastructure.

## 14. Invocation boundaries

### 14.1 Gameplay Pause

`scripts/game/Game.cs` keeps its HPA-382 hosted Settings flow and policy:

- `Kind = UIScreenKinds.Settings`
- modal layer and priority
- parent = current Pause handle
- `ProcessPolicy = Always`
- no new pause ownership
- no new gameplay-block ownership
- visible cursor
- inherited HUD
- inert lower layers
- close-on-Cancel, with Settings native interception first
- queue-free node lifetime

HPA-383 adds the explicit `InitialFocus` callback. The host's existing `SetPresented` callback remains the single call to `OpenSettings(showOverlay: false)`; the redundant post-`TryPresent` `OpenSettings()` call is removed so hosted presentation happens once.

### 14.2 Main Menu

`scripts/ui/MainMenu.cs` keeps loading `SettingsMenu.tscn` directly.

HPA-383 may touch this file only to:

- retain one live Settings instance,
- call `InitialFocusTarget.GrabFocus()` once after direct open,
- restore focus to the existing Settings button on close, and
- preserve current cleanup.

It does not add a host, Continue, new layout, new message presentation, or scene-transition changes. Those remain HPA-380.

## 15. Testing strategy

### 15.1 Scene-authorship contract

A new scene-focused test instantiates `SettingsMenu.tscn` without entering the tree and proves that all page selectors, page roots, row labels, setting controls, Apply/Cancel controls, and inline error nodes already exist before `_Ready()`.

The scene-before-ready assertions are the primary authorship contract. A reflection test that the old builder method names no longer exist remains a secondary regression tripwire.

### 15.2 Existing behavior regression

`SettingsMenuControllerTest` continues covering:

- snapshot cloning,
- Apply/Cancel,
- custom resolution,
- difficulty,
- keybinding text,
- capture,
- duplicate/reserved validation,
- keyboard and joypad Cancel,
- dropdown priority,
- pointer/UI navigation behavior, and
- one-shot close.

The old `OpenSettings_GrabsFocusOnFirstControl` unit expectation is replaced with a contract assertion that the default `InitialFocusTarget` is the master slider; actual focus application is verified at the two invocation boundaries.

The obsolete `OpenSettings_InGameMode_PanelSizeClampedToViewport` test is removed. Shell fit is verified against `ModalShell/Panel` in the scene/layout tests instead of reintroducing controller-owned panel sizing.

### 15.3 Responsive layout and scroll ownership

Scene/layout tests cover:

- shell `BodyScroll` horizontal and vertical modes are disabled for Settings,
- standard two-column frame and vertical selector,
- compact one-column frame and horizontal selector,
- compact one-column setting rows,
- page-local scroll containers remain enabled and expand/fill,
- page selection changes only the `TabContainer` page while `ButtonGroup` keeps selector exclusivity,
- panel fit at the seven approved viewports, and
- representative long labels at 640×360 without growing the modal outside the viewport.

The shell panel is queried through the concrete nested path `ModalShell/Panel`; `%Panel` is not resolved from the Settings root because the unique-name owner is the modal-shell scene instance.

### 15.4 Invocation integration

`GameplayPauseHostTest` proves:

- Settings is still a logical child of Pause,
- Settings receives host-owned initial focus on its first active-page control,
- Cancel returns to the same Pause entry,
- key capture/dropdown still intercept Cancel before the host, and
- teardown leaves no Settings entry or focus lease.

`MainMenuTest` proves:

- direct Settings still opens,
- the first active-page control receives focus after direct open,
- close removes the child, and
- focus returns to the existing Settings button.

## 16. File boundaries

Implementation should be limited to:

- `scenes/ui/SettingsMenu.tscn`
- `scripts/ui/SettingsMenuController.cs`
- `scripts/game/Game.cs`
- `scripts/ui/MainMenu.cs`
- `tests/ui/SettingsMenuControllerTest.cs`
- `tests/ui/SettingsMenuSceneTest.cs` (new)
- `tests/game/GameplayPauseHostTest.cs`
- `tests/ui/MainMenuTest.cs`

No change is expected in:

- `scripts/settings/SettingsData.cs`
- `scripts/settings/SettingsManager.cs`
- `resources/ui/theme/SiriusTheme.tres`
- `scripts/ui/components/SiriusModalShell.cs`
- `scripts/ui/hosting/*`

If implementation discovers that one of those shared files must change, stop and reassess whether the change is truly required by HPA-383 instead of silently broadening scope.

## 17. Acceptance criteria

HPA-383 is complete when:

- the player-facing Settings structure is scene-authored;
- no runtime `BuildUI()`-style layout builder remains;
- all existing settings fields and persistence behavior remain intact;
- Apply commits staged values and Cancel discards them;
- custom resolutions remain preserved;
- duplicate/reserved key validation and key capture remain correct;
- dropdown and key-capture Cancel priority remain correct;
- mouse, keyboard, and gamepad navigation remain usable;
- the standard left rail and compact top selector both work;
- shell scrolling is disabled and page-local scrolling owns Settings overflow;
- no hard-coded page-height calculation exists;
- the default first-control focus remains the master slider;
- Pause-hosted Settings restores focus to Pause;
- Main Menu direct Settings restores focus to the existing Settings button;
- the screen fits all approved landscape viewports;
- representative long text remains usable at 640×360;
- focused Settings, gameplay-host, and Main Menu tests pass; and
- the full test suite and build pass.

## 18. Deferred follow-ups

- HPA-380: Main Menu `UIScreenHost` and Main Menu redesign
- HPA-384: scene-authored Save/Load cards
- HPA-572: shared host-managed confirmations, warnings, and errors
- HPA-541: persisted Reduced Motion preference

These tickets consume the stable Settings result; HPA-383 does not implement their scope early.
