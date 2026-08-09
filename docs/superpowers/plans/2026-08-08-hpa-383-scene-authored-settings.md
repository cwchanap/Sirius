# HPA-383 Scene-Authored Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's runtime-built Settings layout with a scene-authored, themed, responsive screen while preserving all existing settings behavior and the HPA-382 gameplay-host lifecycle.

**Architecture:** Keep `SettingsMenuController` as the single screen controller and `SettingsManager`/`SettingsData` as the existing domain owners. Move static controls and layout to `SettingsMenu.tscn`, bind those nodes from C#, reuse `SiriusModalShell` and the existing Theme, disable the shell's outer body scrolling for Settings, and let page-local scroll containers own overflow. Focus is applied exactly once by the invoking owner: `UIScreenHost` under Pause, direct `MainMenu` otherwise. The current host does not invoke incoming `SetPresented(true)` during initial attachment, so `Game` makes the sole initial `OpenSettings(showOverlay: false)` call after a successful `TryPresent`; `SetPresented` remains for later host-driven presentation transitions.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing `SiriusModalShell`, Sirius Theme, and `UIScreenHost`.

## Global Constraints

- Primary target is desktop landscape with mouse, keyboard, and gamepad.
- Minimum supported logical resolution is 640×360.
- Compact reflow is `SiriusUiMetrics.IsCompact(viewportSize)`; do not invent another breakpoint.
- Preserve Master, Music, and SFX volume; fullscreen; resolution; difficulty; autosave; Toggle Inventory, Interact, and Pause/Cancel bindings.
- Preserve staged edits until Apply; Cancel discards staged edits.
- Preserve custom/non-preset resolutions.
- Preserve reserved-key and duplicate-key validation.
- Preserve key-capture and `OptionButton` popup Cancel priority.
- Preserve the default initial-focus outcome: Audio's master-volume slider.
- `SettingsManager` remains the application/persistence owner.
- The gameplay `UIScreenHost` remains the presentation-lifecycle and initial-focus owner under Pause.
- Direct Main Menu remains responsible for its own initial-focus and return-focus handoff until HPA-380.
- HPA-380 owns the Main Menu `UIScreenHost`; do not add it here.
- HPA-572 owns generic host-managed confirmations/warnings/errors; use themed inline Settings validation here.
- HPA-541 owns Reduced Motion; do not add new settings in HPA-383.
- Do not create a Settings view model, generic settings-row component family, navigation framework, or another modal shell.
- Do not add hard-coded page-height math; use expand/fill containers and page-local scrolling.
- `SiriusErrorPanel` may remain assigned to a `PanelContainer`: `Control.theme_type_variation` resolves an explicitly named variation before class fallback. Do not change Theme resources just to align the variation's declared base type.
- Do not modify `SettingsData`, `SettingsManager`, `SiriusTheme.tres`, `SiriusModalShell`, or `scripts/ui/hosting/*` unless implementation proves the design impossible; reassess scope before doing so.

---

## File map

**Production**
- Modify `scenes/ui/SettingsMenu.tscn` — canonical static Settings hierarchy, row/control cells, Sirius styling, page-local scrolling, and fixed actions.
- Modify `scripts/ui/SettingsMenuController.cs` — scene-node binding, shell-scroll configuration, dynamic values, staged behavior, page selection, responsive reflow, and inline feedback.
- Modify `scripts/game/Game.cs` — add the Settings initial-focus callback and retain the explicit post-`TryPresent` `OpenSettings(showOverlay: false)` call as the sole initial hosted open.
- Modify `scripts/ui/MainMenu.cs` — apply initial focus after direct open and restore focus to the existing Settings button after close.

**Tests**
- Create `tests/ui/SettingsMenuSceneTest.cs` — prove scene-authored controls/labels exist before `_Ready()` and verify scroll ownership, responsive structure, page selection, and viewport fit.
- Modify `tests/ui/SettingsMenuControllerTest.cs` — preserve behavior coverage, replace obsolete focus ownership and old generic-panel sizing assumptions.
- Modify `tests/game/GameplayPauseHostTest.cs` — verify hosted Settings initial focus, the zero/one/duplicate initial-open guard, and existing Cancel/cleanup behavior.
- Modify `tests/ui/MainMenuTest.cs` — verify direct Settings initial focus and close restoration.

---

### Task 1: Scene-author Settings and preserve controller/domain behavior

**Files:**
- Create: `tests/ui/SettingsMenuSceneTest.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `tests/ui/SettingsMenuControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusModalShell`, `SiriusThemeTypes`, `SettingsData`, `SettingsManager`, and `OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)`.
- Produces: stable unique scene nodes, page selection on the authored scene, `SettingsMenuController.InitialFocusTarget`, the existing Settings behavior without runtime layout builders, and one explicit shell/page scroll boundary.

- [ ] **Step 1: Add the failing pre-`_Ready()` scene-authorship contract**

Create `tests/ui/SettingsMenuSceneTest.cs`:

```csharp
using System.Reflection;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuSceneTest : Node
{
    private static readonly string[] RequiredUniqueNodes =
    {
        "%ModalShell",
        "%SettingsFrame",
        "%PageSelector",
        "%PageDeck",
        "%AudioPageButton",
        "%DisplayPageButton",
        "%GameplayPageButton",
        "%ControlsPageButton",
        "%AudioScroll",
        "%DisplayScroll",
        "%GameplayScroll",
        "%ControlsScroll",
        "%AudioRows",
        "%DisplayRows",
        "%GameplayRows",
        "%ControlsRows",

        "%MasterVolumeLabel",
        "%MasterSlider",
        "%MasterValueLabel",
        "%MusicVolumeLabel",
        "%MusicSlider",
        "%MusicValueLabel",
        "%SfxVolumeLabel",
        "%SfxSlider",
        "%SfxValueLabel",

        "%FullscreenLabel",
        "%FullscreenCheck",
        "%ResolutionLabel",
        "%ResolutionOption",

        "%DifficultyLabel",
        "%DifficultyOption",
        "%AutoSaveLabel",
        "%AutoSaveCheck",

        "%InventoryKeyLabel",
        "%InventoryKeyButton",
        "%InteractKeyLabel",
        "%InteractKeyButton",
        "%PauseKeyLabel",
        "%PauseKeyButton",

        "%ErrorPanel",
        "%ErrorLabel",
        "%ApplyButton",
        "%CancelButton"
    };

    [TestCase]
    public void SceneOwnsSettingsControlsBeforeReady()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        AssertThat(packed).IsNotNull();

        var screen = packed!.Instantiate<SettingsMenuController>();
        try
        {
            foreach (var path in RequiredUniqueNodes)
                AssertThat(screen.GetNodeOrNull(path)).IsNotNull();
        }
        finally
        {
            screen.Free();
        }
    }

    [TestCase]
    public void ControllerHasNoRuntimeLayoutBuilders()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildUI", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildAudioTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildDisplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildGameplayTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("BuildControlsTab", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddSliderRow", flags)).IsNull();
        AssertThat(typeof(SettingsMenuController).GetMethod("AddKeyRow", flags)).IsNull();
    }
}
```

The first test is the primary contract. The reflection test is only a tripwire against reintroducing the known builder methods.

- [ ] **Step 2: Run the new scene tests and confirm the expected failure**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: the required authored nodes are missing and the builder-method assertions fail.

- [ ] **Step 3: Replace the skeleton scene with the concrete authored hierarchy**

Keep `res://scenes/ui/SettingsMenu.tscn` and author:

```text
SettingsMenuController
├── Background (Panel, SiriusScrim)
└── ModalShell (SiriusModalShell, Title="Settings", SizeClass=Large)
    ├── BodyHost
    │   ├── SettingsFrame (GridContainer)
    │   │   ├── PageSelector (GridContainer)
    │   │   │   ├── AudioPageButton
    │   │   │   ├── DisplayPageButton
    │   │   │   ├── GameplayPageButton
    │   │   │   └── ControlsPageButton
    │   │   └── PageDeck (TabContainer, tabs hidden)
    │   │       ├── AudioPage/AudioScroll/AudioRows
    │   │       ├── DisplayPage/DisplayScroll/DisplayRows
    │   │       ├── GameplayPage/GameplayScroll/GameplayRows
    │   │       └── ControlsPage/ControlsScroll/ControlsRows
    │   └── ErrorPanel (PanelContainer, SiriusErrorPanel, hidden)
    │       └── ErrorLabel
    └── ActionsHost
        ├── ApplyButton (SiriusPrimaryButton)
        └── CancelButton (SiriusSecondaryButton)
```

Use one scene-authored `ButtonGroup` for the four page buttons. Each page button uses toggle mode; Audio starts pressed and `PageDeck.CurrentTab = 0`.

Keep the outer shell hierarchy unchanged. Do **not** add another shell or duplicate `BodyHost`/`ActionsHost`.

Author the page rows with this exact shape:

```text
AudioRows
├── MasterVolumeLabel
├── MasterControlCell (HBoxContainer)
│   ├── MasterSlider
│   └── MasterValueLabel
├── MusicVolumeLabel
├── MusicControlCell (HBoxContainer)
│   ├── MusicSlider
│   └── MusicValueLabel
├── SfxVolumeLabel
└── SfxControlCell (HBoxContainer)
    ├── SfxSlider
    └── SfxValueLabel

DisplayRows
├── FullscreenLabel
├── FullscreenCheck
├── ResolutionLabel
└── ResolutionOption

GameplayRows
├── DifficultyLabel
├── DifficultyOption
├── AutoSaveLabel
└── AutoSaveCheck

ControlsRows
├── InventoryKeyLabel
├── InventoryKeyButton
├── InteractKeyLabel
├── InteractKeyButton
├── PauseKeyLabel
└── PauseKeyButton
```

Mark every label/control name listed in Step 1 as unique in the Settings scene. Set row labels to word-smart wrapping. Give interactive controls the existing minimum target contract: at least 44 logical pixels standard, with compact sizing allowed to reduce to the shared 40-pixel target through layout/Theme behavior.

Keep `ResolutionOption` and `DifficultyOption` empty in the scene; their items remain dynamic controller data.

Keep `ErrorPanel` as `PanelContainer` with `theme_type_variation = SiriusErrorPanel`. The explicit variation is valid for lookup even though the Theme variation's base is `Panel`, and retaining `PanelContainer` avoids extra wrapper layout solely for styling.

Keep Apply/Cancel in `SiriusModalShell.ActionsHost`, outside page scrolling.

- [ ] **Step 4: Add explicit scene-node binding and disable shell body scrolling**

Delete these runtime construction methods from `SettingsMenuController`:

```text
BuildUI
BuildAudioTab
BuildDisplayTab
BuildGameplayTab
BuildControlsTab
AddSliderRow
AddKeyRow
```

Add structure fields while retaining existing behavior fields:

```csharp
private SiriusModalShell _shell = null!;
private ScrollContainer _shellBodyScroll = null!;
private GridContainer _settingsFrame = null!;
private GridContainer _pageSelector = null!;
private TabContainer _pageDeck = null!;
private GridContainer _audioRows = null!;
private GridContainer _displayRows = null!;
private GridContainer _gameplayRows = null!;
private GridContainer _controlsRows = null!;

private Button _audioPageButton = null!;
private Button _displayPageButton = null!;
private Button _gameplayPageButton = null!;
private Button _controlsPageButton = null!;
private Button[] _pageButtons = null!;

private PanelContainer _errorPanel = null!;
private Button _applyButton = null!;
private Button _cancelButton = null!;
```

Expose first-control focus instead of page-selector focus:

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

Make `_Ready()` bind/configure before population:

```csharp
public override void _Ready()
{
    BindSceneNodes();
    ConfigureScrollOwnership();
    PopulateChoiceItems();
    BindSignals();
    RefreshLayout();

    Hide();
    SetProcessInput(false);
}
```

Bind shell internals through the shell instance; do not try to resolve `%BodyScroll` from the Settings root:

```csharp
private void BindSceneNodes()
{
    _shell = GetNode<SiriusModalShell>("%ModalShell");
    _shellBodyScroll = _shell.GetNode<ScrollContainer>("%BodyScroll");
    _settingsFrame = GetNode<GridContainer>("%SettingsFrame");
    _pageSelector = GetNode<GridContainer>("%PageSelector");
    _pageDeck = GetNode<TabContainer>("%PageDeck");
    _audioRows = GetNode<GridContainer>("%AudioRows");
    _displayRows = GetNode<GridContainer>("%DisplayRows");
    _gameplayRows = GetNode<GridContainer>("%GameplayRows");
    _controlsRows = GetNode<GridContainer>("%ControlsRows");

    _audioPageButton = GetNode<Button>("%AudioPageButton");
    _displayPageButton = GetNode<Button>("%DisplayPageButton");
    _gameplayPageButton = GetNode<Button>("%GameplayPageButton");
    _controlsPageButton = GetNode<Button>("%ControlsPageButton");
    _pageButtons =
    [
        _audioPageButton,
        _displayPageButton,
        _gameplayPageButton,
        _controlsPageButton
    ];

    _masterSlider = GetNode<HSlider>("%MasterSlider");
    _masterValueLabel = GetNode<Label>("%MasterValueLabel");
    _musicSlider = GetNode<HSlider>("%MusicSlider");
    _musicValueLabel = GetNode<Label>("%MusicValueLabel");
    _sfxSlider = GetNode<HSlider>("%SfxSlider");
    _sfxValueLabel = GetNode<Label>("%SfxValueLabel");

    _fullscreenCheck = GetNode<CheckBox>("%FullscreenCheck");
    _resolutionOption = GetNode<OptionButton>("%ResolutionOption");
    _difficultyOption = GetNode<OptionButton>("%DifficultyOption");
    _autoSaveCheck = GetNode<CheckBox>("%AutoSaveCheck");

    _inventoryKeyBtn = GetNode<Button>("%InventoryKeyButton");
    _interactKeyBtn = GetNode<Button>("%InteractKeyButton");
    _pauseKeyBtn = GetNode<Button>("%PauseKeyButton");

    _errorPanel = GetNode<PanelContainer>("%ErrorPanel");
    _errorLabel = GetNode<Label>("%ErrorLabel");
    _applyButton = GetNode<Button>("%ApplyButton");
    _cancelButton = GetNode<Button>("%CancelButton");
}
```

Configure the shell's built-in scroller once:

```csharp
private void ConfigureScrollOwnership()
{
    _shellBodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
    _shellBodyScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
}
```

The four page `ScrollContainer`s remain `Auto` and use expand/fill size flags. Do not add a `SiriusModalShell` export or edit the shared shell scene.

- [ ] **Step 5: Populate dynamic choices only**

```csharp
private void PopulateChoiceItems()
{
    _resolutionOption.Clear();
    foreach (var (w, h) in ResolutionPresets)
        _resolutionOption.AddItem($"{w}\u00d7{h}");

    _difficultyOption.Clear();
    foreach (var difficulty in Difficulties)
        _difficultyOption.AddItem(difficulty);
}
```

Keep custom-resolution insertion/removal behavior in the existing controller methods.

- [ ] **Step 6: Bind all authored controls with named handlers, including page selection**

Wire all signals in one place so Task 1 leaves the authored scene fully operable:

```csharp
private void BindSignals()
{
    Resized += OnResized;

    _masterSlider.ValueChanged += OnMasterVolumeChanged;
    _musicSlider.ValueChanged += OnMusicVolumeChanged;
    _sfxSlider.ValueChanged += OnSfxVolumeChanged;

    _inventoryKeyBtn.Pressed += OnInventoryKeyPressed;
    _interactKeyBtn.Pressed += OnInteractKeyPressed;
    _pauseKeyBtn.Pressed += OnPauseKeyPressed;

    _audioPageButton.Pressed += OnAudioPagePressed;
    _displayPageButton.Pressed += OnDisplayPagePressed;
    _gameplayPageButton.Pressed += OnGameplayPagePressed;
    _controlsPageButton.Pressed += OnControlsPagePressed;

    _applyButton.Pressed += OnApplyPressed;
    _cancelButton.Pressed += OnCancelPressed;
}
```

Add matching detachments in `_ExitTree()`.

Keep the handlers thin:

```csharp
private void OnMasterVolumeChanged(double value) =>
    _masterValueLabel.Text = $"{(int)value}%";

private void OnMusicVolumeChanged(double value) =>
    _musicValueLabel.Text = $"{(int)value}%";

private void OnSfxVolumeChanged(double value) =>
    _sfxValueLabel.Text = $"{(int)value}%";

private void OnInventoryKeyPressed() => StartKeyCapture("toggle_inventory");
private void OnInteractKeyPressed() => StartKeyCapture("interact");
private void OnPauseKeyPressed() => StartKeyCapture("pause_menu");

private void OnAudioPagePressed() => SelectPage(0);
private void OnDisplayPagePressed() => SelectPage(1);
private void OnGameplayPagePressed() => SelectPage(2);
private void OnControlsPagePressed() => SelectPage(3);

private void SelectPage(int pageIndex)
{
    _pageDeck.CurrentTab = pageIndex;
    _pageButtons[pageIndex].ButtonPressed = true;
}
```

`ButtonGroup` owns exclusivity. Do not add a separate `RefreshPageSelection()` loop.

- [ ] **Step 7: Move validation feedback onto the authored error surface**

```csharp
private void ShowError(string msg)
{
    _errorLabel.Text = msg;
    _errorLabel.Visible = true;
    _errorPanel.Visible = true;
}

private void ClearError()
{
    _errorLabel.Visible = false;
    _errorPanel.Visible = false;
}
```

Call `ClearError()` from `OpenSettings()` before population, from `StartKeyCapture()`, from successful key capture, and from `CancelKeyCapture()`.

Do not introduce HPA-572's generic message controller.

- [ ] **Step 8: Preserve `OpenSettings` state behavior but remove focus ownership from it**

Keep the signature:

```csharp
public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
```

The implementation should end after showing/enabling input; it must not call `GrabFocus()`:

```csharp
public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
{
    if (_listeningAction != null)
        CancelKeyCapture();

    _closedEmitted = false;
    GetNode<Control>("Background").Visible = showOverlay;
    ClearError();

    var source =
        snapshot ??
        SettingsManager.Instance?.GetSnapshot() ??
        SettingsData.CreateDefaults();

    _editedSettings = source.Clone();
    PopulateControls();
    Show();
    SetProcessInput(true);
}
```

Keep `EmitClosedOnce()` and `OnCancelPressed()` semantics unchanged.

- [ ] **Step 9: Update existing controller tests for the new ownership boundary**

Keep all behavior tests for staging, Apply/Cancel, custom resolution, difficulty, autosave, key capture, Pause binding capture, reserved/duplicate keys, pointer input, keyboard navigation, joypad navigation, dropdown Cancel priority, and one-shot `Closed`.

Replace the old direct focus side-effect test:

```csharp
[TestCase]
public void InitialFocusTarget_DefaultAudioPage_IsMasterSlider()
{
    _ctrl.OpenSettings(SettingsData.CreateDefaults());

    AssertThat(_ctrl.InitialFocusTarget)
        .IsEqual(GetField<HSlider>(_ctrl, "_masterSlider"));
}
```

Delete `OpenSettings_GrabsFocusOnFirstControl`; actual focus application is now tested at the hosted and Main Menu invocation boundaries in Task 3.

Delete `OpenSettings_InGameMode_PanelSizeClampedToViewport`; viewport fit belongs to `SettingsMenuSceneTest` and must target `ModalShell/Panel`, not controller-owned panel minimum-size math.

- [ ] **Step 10: Run the focused Settings tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: all Settings scene/authorship and behavior tests pass.

- [ ] **Step 11: Commit the authored cutover**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuControllerTest.cs \
  tests/ui/SettingsMenuSceneTest.cs

git commit -m "feat(ui): scene-author settings screen"
```

---

### Task 2: Add responsive reflow, page-local overflow, and viewport contracts

**Files:**
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `tests/ui/SettingsMenuSceneTest.cs`

**Interfaces:**
- Consumes: `SiriusUiMetrics.IsCompact(Vector2)`, `SiriusModalShell.Compact`, `SiriusModalShell.RefreshPresentation(Vector2)`, authored selector/deck/row grids, disabled shell `BodyScroll`, and page-local `ScrollContainer`s from Task 1.
- Produces: standard left rail, compact top selector, responsive row columns, page-local overflow, and fit at all approved viewports without hard-coded body heights.

- [ ] **Step 1: Add failing standard/compact structure tests**

Use a `SubViewport` fixture and assert after opening:

```csharp
[TestCase]
public async Task StandardViewportUsesLeftRailAndTwoColumnRows()
{
    await ResizeAndOpen(new Vector2I(1280, 720));

    AssertThat(_screen!.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%DisplayRows").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%GameplayRows").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(2);
}

[TestCase]
public async Task MinimumViewportUsesTopSelectorAndSingleColumnRows()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    AssertThat(_screen!.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(4);
    AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%DisplayRows").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%GameplayRows").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(1);
}
```

- [ ] **Step 2: Add failing scroll-ownership and page-selection tests**

```csharp
[TestCase]
public async Task SettingsDisablesShellScrollAndKeepsPageScrollAuto()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    var shell = _screen!.GetNode<SiriusModalShell>("%ModalShell");
    var shellScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
    var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");

    AssertThat(shellScroll.VerticalScrollMode)
        .IsEqual(ScrollContainer.ScrollMode.Disabled);
    AssertThat(shellScroll.HorizontalScrollMode)
        .IsEqual(ScrollContainer.ScrollMode.Disabled);
    AssertThat(controlsScroll.VerticalScrollMode)
        .IsEqual(ScrollContainer.ScrollMode.Auto);
}

[TestCase]
public async Task PageButtonSelectsOneTabAndButtonGroupKeepsExclusivity()
{
    await ResizeAndOpen(new Vector2I(1280, 720));

    var deck = _screen!.GetNode<TabContainer>("%PageDeck");
    var audio = _screen.GetNode<Button>("%AudioPageButton");
    var controls = _screen.GetNode<Button>("%ControlsPageButton");

    controls.EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    AssertThat(deck.CurrentTab).IsEqual(3);
    AssertThat(controls.ButtonPressed).IsTrue();
    AssertThat(audio.ButtonPressed).IsFalse();
}
```

- [ ] **Step 3: Run the new layout tests and confirm they fail before responsive code**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: compact/standard columns and scroll/page-selection assertions fail until the controller/scene is complete.

- [ ] **Step 4: Implement only container reflow**

```csharp
private void OnResized() => RefreshLayout();

private void RefreshLayout()
{
    var size = GetViewportRect().Size;
    var compact = SiriusUiMetrics.IsCompact(size);

    _shell.Compact = compact;
    _shell.RefreshPresentation(size);

    _settingsFrame.Columns = compact ? 1 : 2;
    _pageSelector.Columns = compact ? 4 : 1;

    var rowColumns = compact ? 1 : 2;
    _audioRows.Columns = rowColumns;
    _displayRows.Columns = rowColumns;
    _gameplayRows.Columns = rowColumns;
    _controlsRows.Columns = rowColumns;
}
```

Do not set `PageDeck.CustomMinimumSize` from viewport subtraction. Set `SettingsFrame`, `PageDeck`, each page root, and each page `ScrollContainer` to the appropriate expand/fill size flags in the scene so the shell and containers negotiate height naturally.

- [ ] **Step 5: Add the seven-viewport fit test using the real nested shell path**

```csharp
[TestCase(640, 360)]
[TestCase(1024, 768)]
[TestCase(1280, 720)]
[TestCase(1440, 900)]
[TestCase(1920, 1080)]
[TestCase(2560, 1080)]
[TestCase(2560, 1440)]
public async Task ApprovedViewportKeepsSettingsPanelInsideViewport(int width, int height)
{
    await ResizeAndOpen(new Vector2I(width, height));

    var panel = _screen!.GetNode<PanelContainer>("ModalShell/Panel");
    var panelRect = panel.GetGlobalRect();

    AssertThat(panelRect.Position.X).IsGreaterEqual(0f);
    AssertThat(panelRect.Position.Y).IsGreaterEqual(0f);
    AssertThat(panelRect.End.X).IsLessEqual(width + 0.5f);
    AssertThat(panelRect.End.Y).IsLessEqual(height + 0.5f);
}
```

Keep `ModalShell/Panel`. Do not replace this with `%Panel` on the Settings root; `%Panel` belongs to the modal-shell scene's unique-name owner.

- [ ] **Step 6: Add a long-label compact overflow test**

Switch to the Controls page at 640×360, expand a real authored row label, and verify the shell remains inside the viewport while the page-local scroll range grows rather than the outer shell scrolling:

```csharp
[TestCase]
public async Task LongCompactControlsLabelUsesPageScrollWithoutGrowingShell()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    _screen!.GetNode<Button>("%ControlsPageButton")
        .EmitSignal(Button.SignalName.Pressed);

    var label = _screen.GetNode<Label>("%InventoryKeyLabel");
    label.Text = string.Join(" ", Enumerable.Repeat(
        "RepresentativeLocalizedInventoryBindingLabel", 12));

    await AwaitFrames(3);

    var shell = _screen.GetNode<SiriusModalShell>("%ModalShell");
    var shellScroll = shell.GetNode<ScrollContainer>("%BodyScroll");
    var controlsScroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");
    var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");

    AssertThat(shellScroll.ScrollVertical).IsEqual(0);
    AssertThat(controlsScroll.GetVScrollBar().MaxValue)
        .IsGreater(controlsScroll.GetVScrollBar().Page);
    AssertThat(panel.GetGlobalRect().End.Y).IsLessEqual(360.5f);
}
```

Add `using System.Linq;` to the test file for the repeated representative text. If the exact `VScrollBar` range changes with Theme metrics, keep the semantic assertion `MaxValue > Page`; do not pin pixel values.

- [ ] **Step 7: Run scene/layout tests and Settings behavior tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: authorship, page selection, scroll ownership, standard/compact layout, long-label overflow, viewport fit, and existing behavior all pass.

- [ ] **Step 8: Commit responsive Settings behavior**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuSceneTest.cs

git commit -m "feat(ui): make settings layout responsive"
```

---

### Task 3: Make initial focus single-owned at Pause and Main Menu invocation boundaries

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/ui/MainMenuTest.cs`

**Interfaces:**
- Consumes: `SettingsMenuController.InitialFocusTarget`, existing HPA-382 `TryOpenHostedSettings()`, existing `UIScreenEntrySpec.InitialFocus`, current direct Main Menu Settings lifecycle.
- Produces: exactly one initial-focus owner per invocation, exactly one explicit initial hosted open guarded against zero and duplicate calls, host restoration under Pause, and direct focus/return-focus under Main Menu.

- [ ] **Step 1: Add failing gameplay-host initial-focus and exact-initial-open assertions**

Extend `GameplayPauseHostTest.HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause()` before presenting Settings so a `VisibilityChanged` handler marks the first visible presentation with an invalid sentinel. Keep that sentinel after presentation alongside the focus assertions:

```csharp
const int firstPresentationSentinel = -1;

void MarkFirstPresentation(Node node)
{
    if (node is not SettingsMenuController settings)
        return;

    settings.VisibilityChanged += () =>
    {
        if (settings.Visible)
            settings.EditedSettings.MasterVolumePercent = firstPresentationSentinel;
    };
}

tree.NodeAdded += MarkFirstPresentation;
try
{
    settingsButton.EmitSignal(Button.SignalName.Pressed);
}
finally
{
    tree.NodeAdded -= MarkFirstPresentation;
}

await AwaitFrames(2);
var settings = FindDirectChild<SettingsMenuController>(modalLayer);
await AwaitFrames(2);

AssertThat(settings.EditedSettings.MasterVolumePercent)
    .IsEqual(firstPresentationSentinel);
AssertThat(_viewport!.GuiGetFocusOwner())
    .IsEqual(settings.InitialFocusTarget);
AssertThat(settings.InitialFocusTarget)
    .IsEqual(settings.GetNode<HSlider>("%MasterSlider"));
```

This first-presentation sentinel fails for zero initial opens, passes for exactly the one explicit post-`TryPresent` open, and fails for a duplicate because the second `OpenSettings()` repopulates the normalized snapshot. Keep the existing parent handle, process policy, pause ownership, HUD, Cancel, and restoration assertions.

- [ ] **Step 2: Add failing Main Menu initial-focus and return-focus assertions**

Extend the existing Main Menu Settings lifecycle test or add one focused test:

```csharp
var settingsButton = _mainMenu!.GetNode<Button>("VBoxContainer/SettingsButton");
settingsButton.EmitSignal(Button.SignalName.Pressed);
await AwaitFrames(2);

var settings = FindDirectChild<SettingsMenuController>(_mainMenu);
AssertThat(_mainMenu.GetViewport().GuiGetFocusOwner())
    .IsEqual(settings.InitialFocusTarget);
AssertThat(settings.InitialFocusTarget)
    .IsEqual(settings.GetNode<HSlider>("%MasterSlider"));

InvokePrivate(settings, "OnCancelPressed");
await AwaitFrames(2);

AssertThat(_mainMenu.GetViewport().GuiGetFocusOwner())
    .IsEqual(settingsButton);
```

Use the existing helpers in `MainMenuTest`; do not create another test framework.

- [ ] **Step 3: Run gameplay-host and Main Menu suites and confirm only the new focus assertions fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: existing lifecycle assertions remain green; new initial-focus, zero/one/duplicate initial-open, and return-focus expectations fail until production seams change.

- [ ] **Step 4: Give the HPA-382 hosted Settings entry explicit initial focus and retain its sole initial open**

In `Game.TryOpenHostedSettings()`, add:

```csharp
InitialFocus = () => settings.InitialFocusTarget,
```

Keep the existing `SetPresented` callback for later host-driven presentation transitions:

```csharp
SetPresented = visible =>
{
    if (visible) settings.OpenSettings(showOverlay: false);
    else settings.Hide();
},
```

The current `UIScreenHost` does not invoke incoming `SetPresented(true)` during initial attachment. After a successful `TryPresent`, keep the handle/reference assignment and the explicit call below; it is the sole initial hosted open:

```csharp
_hostedSettingsHandle = result.Handle.Value;
_hostedSettingsMenu = settings;
settings.OpenSettings(showOverlay: false);
return true;
```

Do not remove or move this post-`TryPresent` call into `SetPresented`: the first-presentation sentinel from Step 1 guards zero, one, and duplicate initial opens. Do not change any other `UIScreenEntrySpec` policy field and do not add a `Game._Input()` branch.

- [ ] **Step 5: Make direct Main Menu own direct initial focus and close restoration**

Cache the existing button in `MainMenu`:

```csharp
private Button? _settingsButton;
```

Bind it in `_Ready()`:

```csharp
_settingsButton = GetNodeOrNull<Button>("VBoxContainer/SettingsButton");
```

After direct open:

```csharp
_settingsMenu = scene.Instantiate<SettingsMenuController>();
_settingsMenu.Closed += OnSettingsClosed;
AddChild(_settingsMenu);
_settingsMenu.OpenSettings();
_settingsMenu.InitialFocusTarget.GrabFocus();
```

After the existing close cleanup, restore focus:

```csharp
if (_settingsButton != null && IsInstanceValid(_settingsButton))
    _settingsButton.GrabFocus();
```

Do not add `UIScreenHost`, Continue behavior, new Main Menu layout, or generic messages.

- [ ] **Step 6: Re-run integration suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: hosted Settings focuses the master slider through the host and initializes through one explicit post-`TryPresent` open; the retained `SetPresented` callback remains available for later presentation transitions; direct Main Menu Settings focuses the same target once; close restores to Pause or Main Menu correctly; existing host cleanup remains green.

- [ ] **Step 7: Commit invocation/focus integration**

```bash
git add \
  scripts/game/Game.cs \
  scripts/ui/MainMenu.cs \
  tests/game/GameplayPauseHostTest.cs \
  tests/ui/MainMenuTest.cs

git commit -m "fix(ui): restore settings focus across parents"
```

---

### Task 4: Final parity, scope, and regression verification

**Files:**
- Modify only files from Tasks 1–3 if a failing verification proves a required HPA-383 correction.

**Interfaces:**
- Consumes: scene-authored Settings, preserved controller semantics, responsive layout/scroll ownership, gameplay host integration, and Main Menu focus handoff.
- Produces: a clean HPA-383 implementation branch with no legacy runtime Settings builder and no scope creep.

- [ ] **Step 1: Run the complete focused HPA-383 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: zero failures and zero skips introduced by HPA-383.

- [ ] **Step 2: Prove the runtime Settings builder is gone**

```bash
rg -n \
  "BuildUI|BuildAudioTab|BuildDisplayTab|BuildGameplayTab|BuildControlsTab|AddSliderRow|AddKeyRow" \
  scripts/ui/SettingsMenuController.cs \
  scenes/ui/SettingsMenu.tscn \
  tests/ui
```

Expected: no production matches. Reflection-test string literals in `SettingsMenuSceneTest.cs` are the only acceptable matches.

- [ ] **Step 3: Prove the rejected layout/focus patterns did not return**

```bash
rg -n \
  "pageHeight|CustomMinimumSize.*page|InitialFocusTarget\.GrabFocus\(\)" \
  scripts/ui/SettingsMenuController.cs
```

Expected: no matches. `SettingsMenuController` does not calculate body height or grab initial focus itself.

Confirm shell scroll configuration exists only as the scoped Settings integration:

```bash
rg -n \
  "VerticalScrollMode|HorizontalScrollMode" \
  scripts/ui/SettingsMenuController.cs
```

Expected: the two assignments that disable `%BodyScroll`; no new shell/shared-framework API.

- [ ] **Step 4: Prove HPA-383 did not modify domain or shared-framework ownership**

```bash
git diff --name-only main...HEAD
```

Expected paths are limited to:

```text
scenes/ui/SettingsMenu.tscn
scripts/ui/SettingsMenuController.cs
scripts/game/Game.cs
scripts/ui/MainMenu.cs
tests/ui/SettingsMenuControllerTest.cs
tests/ui/SettingsMenuSceneTest.cs
tests/game/GameplayPauseHostTest.cs
tests/ui/MainMenuTest.cs
docs/superpowers/specs/2026-08-08-hpa-383-scene-authored-settings-design.md
docs/superpowers/plans/2026-08-08-hpa-383-scene-authored-settings.md
```

Specifically confirm no changes under:

```text
scripts/settings/
resources/ui/theme/
scripts/ui/components/SiriusModalShell.cs
scripts/ui/hosting/
```

- [ ] **Step 5: Build the solution**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: zero errors.

- [ ] **Step 6: Run the full test suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Existing repository warning noise is acceptable only if unchanged from `main`.

- [ ] **Step 7: Inspect the final diff for accidental architecture expansion**

```bash
git diff --check
git diff --stat main...HEAD
```

Reject and remove any of the following if they appeared without a failing HPA-383 acceptance test that required them:

```text
new Settings view model/presenter
new generic settings-row component hierarchy
new navigation/history service
new Main Menu UIScreenHost work
new generic error/confirmation service
new persisted setting
new Theme token or component-shell API
magic page-height offsets
second initial-focus owner inside SettingsMenuController
```

- [ ] **Step 8: Commit a verification-only correction only when required**

If verification proves a scoped HPA-383 defect, stage only paths from Step 4 and commit:

```bash
git commit -m "fix(ui): preserve settings layout parity"
```

If no correction was required, do not create an empty commit.

---

## Implementation completion definition

The implementation is ready for review only when all of the following are true:

- `SettingsMenu.tscn` owns the player-facing control tree and row labels before `_Ready()`.
- `SettingsMenuController` contains no runtime UI-construction helpers.
- Existing settings semantics remain covered and passing.
- Audio rows preserve their visible percentage labels inside one control cell.
- Standard layout uses a left page rail and two-column setting rows.
- Compact layout uses a top page selector and one-column setting rows.
- `SiriusModalShell.BodyScroll` is disabled for Settings; page-local scroll containers own overflow.
- No hard-coded page-height calculation exists.
- Apply/Cancel remain fixed outside page scrolling.
- Inline validation uses the existing `SiriusErrorPanel` variation without changing Theme resources.
- The default `InitialFocusTarget` is the master-volume slider.
- `OpenSettings()` does not grab focus.
- Hosted Settings receives initial focus from `UIScreenHost`. Because the current host does not invoke incoming `SetPresented(true)` on initial attachment, `Game` makes exactly one explicit post-`TryPresent` `OpenSettings(showOverlay: false)` call; the retained callback handles later presentation transitions, and the regression guard covers zero, one, and duplicate initial opens.
- Direct Main Menu Settings applies initial focus once and restores to its existing Settings button.
- Hosted Settings remains a child of Pause without taking a second pause/input authority.
- No HPA-380, HPA-541, HPA-572, save-domain, settings-domain, Theme-core, modal-shell-core, or `UIScreenHost` scope is implemented early.
- Focused tests, full tests, build, `git diff --check`, and scope audit pass.
