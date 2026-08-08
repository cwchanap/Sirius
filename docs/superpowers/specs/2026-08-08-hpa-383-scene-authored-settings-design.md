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

HPA-382 already integrates this controller into the gameplay `UIScreenHost` as a child of Pause. Main Menu still instantiates Settings directly; HPA-380 owns the Main Menu `UIScreenHost` and broader Main Menu redesign.

The current `OpenSettings(..., showOverlay: false)` path also gives the old generic panel an explicit height (`min(500, viewportHeight * 0.9)`). That is currently the only Settings-specific height owner. The new scene must preserve a real body-height contract rather than deleting that sizing and relying on expand/fill flags with no vertical space to expand into.

`SiriusModalShell` itself intentionally owns **responsive width only** under the HPA-377 component contract. HPA-383 therefore does not broaden the shared shell into a general height-policy API. Settings owns its own large information-screen height locally.

## 3. Goals

HPA-383 will:

1. Move all static Settings structure, copy, rows, buttons, and layout constraints into `SettingsMenu.tscn`.
2. Reuse `SiriusModalShell` and the existing Sirius Theme.
3. Preserve every current settings field and persistence behavior.
4. Preserve current key-capture, dropdown, mouse, keyboard, and gamepad behavior.
5. Implement the approved standard and compact Settings compositions.
6. Give the large Settings surface an explicit safe-frame height so page-local scrolling always has non-zero viewport space.
7. Expose an explicit first-control focus target for hosted and direct invocation.
8. Restore focus to the invoking Settings action after closing from Pause or Main Menu.
9. Use themed inline feedback for recoverable Settings validation failures.
10. Keep all supported desktop landscape viewports usable, with perceptual coverage at 1280×720 and 640×360.

## 4. Non-goals

HPA-383 will not:

- change `SettingsData` serialization or add new persisted settings;
- add Reduced Motion; HPA-541 owns that;
- add another Settings view model, presenter, repository, or state machine;
- split the four Settings pages into separate controllers;
- create generic settings-row components before another real screen needs them;
- create another modal shell;
- change the HPA-377 `SiriusModalShell` contract from responsive-width ownership into a general modal-height policy;
- fix unrelated Pause/confirmation sizing in this ticket;
- add a Main Menu `UIScreenHost`; HPA-380 owns it;
- build generic confirmation/error infrastructure; HPA-572 owns it;
- redesign Save/Load or Pause;
- change audio, display, difficulty, autosave, or input domain behavior;
- add localization infrastructure;
- add touch-first, portrait, or mobile layouts.

If a separate reproducible defect shows that another `SiriusModalShell` consumer needs a shared height contract, that should be fixed under the owning screen/component ticket rather than silently widening HPA-383.

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

### 5.2 Controller owns screen behavior and Settings-specific sizing

`SettingsMenuController` continues to own:

- the `_editedSettings` clone,
- dynamic `OptionButton` items,
- value population,
- staged keybinding changes,
- validation,
- Apply/Cancel,
- input interception local to Settings,
- page selection,
- compact/standard layout switching, and
- the large Settings panel's explicit vertical extent.

The last item is screen-specific presentation policy, not a new shared shell responsibility.

### 5.3 Existing host owns hosted presentation lifecycle

When opened from Pause, the gameplay `UIScreenHost` remains the only owner of parent/child presentation state, tree-pause inheritance, cursor state, lower-layer interactivity, Cancel priority, and focus restoration.

Settings must not write `SceneTree.Paused`, change gameplay-input suppression directly, or implement its own parent stack.

When opened from Main Menu, HPA-383 preserves the current direct-child flow. The direct caller owns initial focus and restores focus to the existing Main Menu Settings button on close. HPA-380 later replaces that invocation with the Main Menu host.

## 6. Scene structure

`SettingsMenu.tscn` remains the production scene path.

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

`SiriusModalShell.BodyHost` is physically nested inside the shell's `BodyScroll`. Settings cannot allow that outer scroller to compete with its four page-local scrollers. `SettingsMenuController` therefore binds the shell instance's `%BodyScroll` and disables both shell scroll axes once during `_Ready()`. The four page `ScrollContainer`s are the only overflow owners.

The controller also binds the nested `ModalShell/Panel` as `_modalPanel`. `SiriusModalShell.RefreshPresentation(size)` continues to own width. Immediately afterward, Settings assigns only the Y component of `_modalPanel.CustomMinimumSize` using the Settings safe-frame height contract in §8.3.

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

All row labels use word-smart wrapping so horizontal scrolling can remain disabled without allowing localized text to widen the panel beyond the viewport.

## 7. Row composition

Each `*Rows` page container has two columns at standard size and one column at compact size.

Audio rows preserve the percentage labels by packing slider and value into one control cell:

```text
MasterVolumeLabel | MasterControlCell(HSlider MasterSlider + Label MasterValueLabel)
MusicVolumeLabel  | MusicControlCell(HSlider MusicSlider + Label MusicValueLabel)
SfxVolumeLabel    | SfxControlCell(HSlider SfxSlider + Label SfxValueLabel)
```

Other rows use:

```text
Label | CheckBox / OptionButton / KeyButton
```

At compact size the one-column grid places the label above its control cell without duplicating controls.

## 8. Layout and responsive behavior

### 8.1 Standard layout

At non-compact sizes:

- `SiriusModalShell` uses the Large size class.
- `SettingsFrame.Columns = 2`.
- `PageSelector.Columns = 1`, producing the approved left rail.
- Each page's `*Rows.Columns = 2`.
- `PageDeck` and its page-local `ScrollContainer`s use expand/fill size flags.
- Only the active page is visible.
- Apply and Cancel remain in `ActionsHost`, outside page scrolling.

### 8.2 Compact layout

When `SiriusUiMetrics.IsCompact(viewportSize)` is true:

- `SettingsFrame.Columns = 1`.
- `PageSelector.Columns = 4`, producing the short top selector.
- Each page's `*Rows.Columns = 1`.
- The active page's own `ScrollContainer` owns vertical overflow.
- Apply and Cancel stay fixed in `ActionsHost`.
- The shell uses compact title sizing and safe margins through `SiriusModalShell`.

### 8.3 Settings modal height contract

There is no page-height magic and no new shared shell API. The screen gives its Large modal a concrete vertical extent after the shell computes width:

```csharp
var panelHeight = compact
    ? size.Y - SiriusUiMetrics.SafeMargin(true) * 2f
    : size.Y * 0.90f;

_modalPanel.CustomMinimumSize = new Vector2(
    _modalPanel.CustomMinimumSize.X,
    Mathf.Max(0f, panelHeight));
```

This preserves the existing Settings notion of a viewport-bounded large panel while removing the old fixed `500` cap. It also ensures `PageDeck` has real vertical space to distribute to the active page scroller.

The controller never assigns a page-specific height. Page content uses expand/fill and scrolls only when content exceeds the page viewport.

### 8.4 Supported viewports

The authoritative list is `SiriusUiMetrics.VerificationViewports`; tests iterate that array instead of copying the seven sizes into a second table.

`SiriusUiMetrics.FocusVerificationViewports` supplies the detailed 640×360 and 1280×720 perceptual/focus cases.

## 9. Page selection

The four scene-authored page buttons share one scene-authored `ButtonGroup`, use toggle mode, and Audio starts selected with `PageDeck.CurrentTab = 0`.

Each page button has one named `Pressed` handler that calls `SelectPage(index)`. `SelectPage` changes `PageDeck.CurrentTab` and sets the destination button pressed without emitting another signal; `ButtonGroup` owns exclusivity. There is no manual loop that continuously re-synchronizes all four buttons.

The hidden `TabContainer` remains because the current Settings UI already uses that abstraction and it provides simple one-page visibility. Tests treat it as implementation detail and verify the actual visible/focusable controls rather than relying only on `CurrentTab`.

## 10. Initial focus contract

The migration preserves the existing Settings focus outcome.

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

`OpenSettings()` populates and shows the screen but does **not** call `GrabFocus()` itself.

Focus ownership is explicit:

- Pause-hosted Settings supplies `InitialFocus = () => settings.InitialFocusTarget`; the host applies it.
- Direct Main Menu Settings calls `settings.InitialFocusTarget.GrabFocus()` once after `OpenSettings()`.
- Closing hosted Settings restores Pause through the host restoration lease.
- Closing direct Main Menu Settings restores the existing Main Menu Settings button.

## 11. Data binding and staged edits

`OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)` remains the public entry point.

It continues to:

1. cancel active key capture;
2. reset the one-shot close guard;
3. show or hide the scene-authored `Background` scrim;
4. clear stale inline error state;
5. clone the supplied/current snapshot into `_editedSettings`;
6. populate all authored controls;
7. show the screen; and
8. enable input processing.

It does not decide which parent owns focus.

Resolution presets and difficulty values remain controller-owned dynamic data because they are values, not layout.

## 12. Input and Cancel behavior

HPA-383 preserves the current priority:

1. An open `OptionButton` popup consumes Cancel before Settings.
2. Active key capture consumes Cancel before Settings close.
3. Capturing the Pause binding allows Escape to become the new binding.
4. Otherwise Pause/Cancel closes Settings and discards staged edits.
5. Mouse input reaches child GUI controls.
6. Keyboard/gamepad GUI-navigation actions reach Godot focus handling.
7. Unrelated input is consumed so it cannot leak into gameplay.

The gameplay host keeps the existing `InterceptCancel` rule based on `settings.IsRebinding` and `settings.IsPopupOpen`.

## 13. Validation and feedback

Recoverable Settings validation stays inline.

`SettingsMenu.tscn` provides a hidden `PanelContainer` named `ErrorPanel` with `theme_type_variation = SiriusErrorPanel` and a child `ErrorLabel`. This matches the existing Sirius pattern where `Panel`-based Theme variations are explicitly applied to `PanelContainer` controls; no wrapper or Theme change is needed.

`ShowError(string)` makes the panel and label visible without replacing `_editedSettings` or closing the screen. Starting/canceling key capture clears the inline error state.

HPA-572 may later migrate appropriate non-inline errors to the shared host-managed path.

## 14. Invocation boundaries

### 14.1 Gameplay Pause

`Game.TryOpenHostedSettings()` keeps the existing HPA-382 policy and adds only:

```csharp
InitialFocus = () => settings.InitialFocusTarget,
```

The host's `SetPresented` callback remains the single hosted call to `OpenSettings(showOverlay: false)`. The redundant call after successful `TryPresent` is removed.

No new pause/input/cursor ownership is added.

### 14.2 Main Menu

`MainMenu` continues to load `SettingsMenu.tscn` directly. HPA-383 only:

- retains one live Settings instance,
- applies `InitialFocusTarget` once after direct open,
- restores the existing Settings button on close, and
- preserves current cleanup.

No Main Menu host or redesign is pulled forward.

## 15. Testing strategy

### 15.1 Scene-authorship contract

`SettingsMenuSceneTest` instantiates the packed scene before `_Ready()` and proves page selectors, page roots, row labels, controls, actions, and error nodes already exist.

The reflection check for removed builder names is secondary; the authored-node contract is primary.

### 15.2 Behavior regression

`SettingsMenuControllerTest` continues covering staged edits, Apply/Cancel, custom resolution, difficulty, capture, duplicate/reserved validation, Cancel priority, pointer/UI navigation, and one-shot close.

The old focus test becomes a target-contract test (`InitialFocusTarget` defaults to master slider); actual `GrabFocus()` behavior is verified at the two invocation boundaries.

The old controller-owned panel clamp test is removed because the new scene/layout test owns the concrete panel fit contract.

### 15.3 Responsive and perceptual layout

Tests cover:

- Settings disables the shell `BodyScroll` on both axes;
- page scrollers remain vertically enabled and expand/fill;
- standard/compact columns;
- page selection;
- all row labels use word-smart wrapping;
- the nested `ModalShell/Panel` stays inside every `SiriusUiMetrics.VerificationViewports` size on both X and Y;
- at both `SiriusUiMetrics.FocusVerificationViewports`, `MasterSlider.Size.Y > 0` and the shell panel encloses `MasterSlider.GetGlobalRect()`;
- at 640×360 with representative long Controls text, the panel still fits X/Y and the controls page scrollbar has `Page > 0` (a real visible viewport) as well as `MaxValue > Page` when overflow exists.

These assertions intentionally fail a structurally-correct but visually collapsed page.

### 15.4 Invocation integration

`GameplayPauseHostTest` proves hosted Settings receives host-owned initial focus, Cancel returns to the same Pause entry, native capture/dropdown priority still wins, and teardown leaves no lease.

`MainMenuTest` uses its existing `_menu`, `InvokePrivateAcrossHierarchy`, `GetPrivateField`, and `ToSignal` helpers to prove direct open receives initial focus and close restores the existing Settings button.

## 16. File boundaries

Implementation is limited to:

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
- `scenes/ui/components/SiriusModalShell.tscn`
- `scripts/ui/hosting/*`

This is intentional: HPA-377 defines the shell as owning responsive width only. HPA-383 supplies the Settings-specific Y extent through the existing nested panel without redefining the component API.

## 17. Risks and mitigations

### 17.1 Collapsed modal body

**Risk:** a scroll container can have a zero minimum height; correct column values alone do not prove that any setting is visible.

**Mitigation:** Settings explicitly assigns the modal panel's safe-frame height and tests that the master slider has non-zero height and is enclosed by the panel at 1280×720 and 640×360.

### 17.2 Competing nested scroll owners

**Risk:** shell scrolling plus page scrolling produces double-scroll behavior.

**Mitigation:** shell `BodyScroll` is disabled for Settings; page scroll containers exclusively own overflow.

### 17.3 Horizontal expansion from long labels

**Risk:** with horizontal scrolling disabled, an unwrapped label can push the panel wider than the viewport.

**Mitigation:** authored row labels use word-smart wrapping and 640×360 tests assert X/Y fit.

### 17.4 Focus race

**Risk:** `OpenSettings()` and `UIScreenHost` can both attempt focus and produce nondeterministic hosted focus.

**Mitigation:** the screen only exposes `InitialFocusTarget`; the invoking host/direct Main Menu path applies focus exactly once.

### 17.5 Hosted/direct divergence

**Risk:** direct Main Menu and hosted Pause invocation can drift.

**Mitigation:** shared scene/controller behavior plus one focused integration test for each parent.

## 18. Acceptance criteria

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
- the active page has non-zero visible area at 1280×720 and 640×360;
- only page-local scrollers own overflow;
- long text cannot expand the panel beyond 640×360;
- Pause-hosted Settings restores focus to Pause;
- Main Menu direct Settings restores focus to its Settings button;
- the screen fits every `SiriusUiMetrics.VerificationViewports` size;
- focused Settings, gameplay-host, and Main Menu tests pass; and
- the full test suite and build pass.

## 19. Deferred follow-ups

- HPA-380: Main Menu `UIScreenHost` and Main Menu redesign
- HPA-384: scene-authored Save/Load cards
- HPA-572: shared host-managed confirmations, warnings, and errors
- HPA-541: persisted Reduced Motion preference

These tickets consume the stable Settings result; HPA-383 does not implement their scope early.
