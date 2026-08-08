# HPA-383 Scene-Authored Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's runtime-built Settings layout with a scene-authored, themed, responsive screen while preserving all existing settings behavior and the HPA-382 gameplay-host lifecycle.

**Architecture:** Keep `SettingsMenuController` as the single screen controller and `SettingsManager`/`SettingsData` as the existing domain owners. Move static controls and layout to `SettingsMenu.tscn`, bind those nodes from C#, use `SiriusModalShell` plus the existing Theme, and add only the minimal responsive/page-selection code required by the approved Settings wireframe. Preserve the current direct Main Menu invocation until HPA-380 adds the Main Menu host.

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
- `SettingsManager` remains the application/persistence owner.
- The gameplay `UIScreenHost` remains the presentation-lifecycle owner under Pause.
- HPA-380 owns the Main Menu `UIScreenHost`; do not add it here.
- HPA-572 owns generic host-managed confirmations/warnings/errors; use themed inline Settings validation here.
- HPA-541 owns Reduced Motion; do not add new settings in HPA-383.
- Do not create a Settings view model, generic settings-row component family, navigation framework, or another modal shell.
- Do not modify `SettingsData`, `SettingsManager`, `SiriusTheme.tres`, `SiriusModalShell`, or `scripts/ui/hosting/*` unless implementation proves the design impossible; reassess scope before doing so.

---

## File map

**Production**
- Modify `scenes/ui/SettingsMenu.tscn` — canonical static Settings hierarchy, Sirius styling, page-local scrolling, and fixed actions.
- Modify `scripts/ui/SettingsMenuController.cs` — scene-node binding, dynamic values, existing staged behavior, page selection, responsive reflow, and inline feedback.
- Modify `scripts/game/Game.cs` — add the Settings initial-focus callback to the existing HPA-382 host entry only.
- Modify `scripts/ui/MainMenu.cs` — restore focus to the existing Settings button after the direct Settings child closes.

**Tests**
- Create `tests/ui/SettingsMenuSceneTest.cs` — prove scene-authored controls exist before `_Ready()` and verify responsive structure/viewport fit.
- Modify `tests/ui/SettingsMenuControllerTest.cs` — preserve behavior coverage against scene-authored nodes and update obsolete generic-panel assumptions.
- Modify `tests/game/GameplayPauseHostTest.cs` — verify hosted Settings initial focus and existing Cancel/cleanup behavior.
- Modify `tests/ui/MainMenuTest.cs` — verify direct Settings close restores focus to the current Main Menu Settings button.

---

### Task 1: Cut Settings over from runtime UI construction to scene-authored controls

**Files:**
- Create: `tests/ui/SettingsMenuSceneTest.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `tests/ui/SettingsMenuControllerTest.cs`

**Interfaces:**
- Consumes: `SiriusModalShell`, `SiriusThemeTypes`, existing `SettingsData`, `SettingsManager`, and the existing public `OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)`.
- Produces: stable unique scene nodes, `SettingsMenuController.InitialFocusTarget`, and the same existing Settings behavior without `BuildUI()` or other runtime layout builders.

- [ ] **Step 1: Add a failing pre-`_Ready()` scene-authorship test**

Create `tests/ui/SettingsMenuSceneTest.cs` with a test that instantiates the packed scene but does not add it to a tree:

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
        "%AudioRows",
        "%DisplayRows",
        "%GameplayRows",
        "%ControlsRows",
        "%MasterSlider",
        "%MasterValueLabel",
        "%MusicSlider",
        "%MusicValueLabel",
        "%SfxSlider",
        "%SfxValueLabel",
        "%FullscreenCheck",
        "%ResolutionOption",
        "%DifficultyOption",
        "%AutoSaveCheck",
        "%InventoryKeyButton",
        "%InteractKeyButton",
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

- [ ] **Step 2: Run the new tests and verify they fail against current `main`**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: `SceneOwnsSettingsControlsBeforeReady` fails because the scene does not contain the named controls, and `ControllerHasNoRuntimeLayoutBuilders` fails because the builder methods still exist.

- [ ] **Step 3: Replace the skeleton `SettingsMenu.tscn` with the scene-authored hierarchy**

Keep the production path `res://scenes/ui/SettingsMenu.tscn`.

Author these concrete structural decisions in the scene:

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

Use one scene-authored `ButtonGroup` for the four page buttons. Set each page button to `toggle_mode = true`; Audio starts selected. Give every interactive control a minimum height of at least 44 logical pixels in the standard scene. Put the existing setting controls into the four pages with the unique names listed in Step 1.

Keep `ResolutionOption` and `DifficultyOption` empty in the scene because their items are dynamic controller data. Keep `ErrorPanel` hidden by default. Keep Apply/Cancel in `SiriusModalShell.ActionsHost` so page scrolling cannot move the actions.

Override the `SiriusModalShell` body scroll for this scene so it does not own vertical overflow; the four page `ScrollContainer`s own page overflow.

- [ ] **Step 4: Replace runtime construction with explicit scene-node binding**

In `SettingsMenuController`, delete:

```csharp
BuildUI(...)
BuildAudioTab()
BuildDisplayTab()
BuildGameplayTab()
BuildControlsTab()
AddSliderRow(...)
AddKeyRow(...)
```

Add fields for the scene structure while retaining the existing behavior fields:

```csharp
private SiriusModalShell _shell = null!;
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

public Control InitialFocusTarget => _pageButtons[_pageDeck.CurrentTab];
```

Make `_Ready()` bind the nodes before any data population:

```csharp
public override void _Ready()
{
    BindSceneNodes();
    PopulateChoiceItems();
    BindSignals();
    RefreshPageSelection();
    RefreshLayout();

    Hide();
    SetProcessInput(false);
}
```

Implement `BindSceneNodes()` using unique names:

```csharp
private void BindSceneNodes()
{
    _shell = GetNode<SiriusModalShell>("%ModalShell");
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

Populate only the values that were previously created dynamically:

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

- [ ] **Step 5: Bind existing behavior to the authored controls with named handlers**

Use named handlers so `_ExitTree()` can detach them cleanly:

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

    _applyButton.Pressed += OnApplyPressed;
    _cancelButton.Pressed += OnCancelPressed;
}
```

The handlers remain thin:

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
```

Add matching detachments in `_ExitTree()`.

Do not change the existing staging, resolution, keybinding, Apply, Cancel, `ShouldCancelOrClose`, or duplicate/reserved-key rules in this task.

- [ ] **Step 6: Move inline validation from a raw label to the scene-authored Sirius error surface**

Keep `ShowError(string)` as the behavior seam:

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

- [ ] **Step 7: Preserve the public open/close contract**

Keep the current signature:

```csharp
public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
```

Replace old generic-panel sizing code with scene-authored scrim visibility and existing population:

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
    InitialFocusTarget.GrabFocus();
}
```

Keep `EmitClosedOnce()` and `OnCancelPressed()` semantics unchanged.

- [ ] **Step 8: Update the existing controller tests only where the old generic layout leaked into assertions**

Retain current behavior tests. Update the old `OpenSettings_InGameMode_PanelSizeClampedToViewport` assertion to query the new shell panel path or move its fit responsibility to `SettingsMenuSceneTest`.

Keep the existing tests for:

```text
Apply/Cancel
custom resolution
difficulty
autosave
key capture
Pause binding capture
reserved/duplicate keys
mouse input
keyboard navigation
joypad navigation
dropdown Cancel priority
one-shot Closed
```

Do not rewrite those tests around a new state abstraction.

- [ ] **Step 9: Run the focused Settings tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: all Settings scene and behavior tests pass.

- [ ] **Step 10: Commit the scene-authored cutover**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuControllerTest.cs \
  tests/ui/SettingsMenuSceneTest.cs

git commit -m "feat(ui): scene-author settings screen"
```

---

### Task 2: Implement standard/compact Settings page navigation and page-local scrolling

**Files:**
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `tests/ui/SettingsMenuSceneTest.cs`

**Interfaces:**
- Consumes: `SiriusUiMetrics.IsCompact(Vector2)`, `SiriusModalShell.Compact`, `SiriusModalShell.RefreshPresentation(Vector2)`, the four page buttons, `PageDeck`, and the four `*Rows` grids from Task 1.
- Produces: one selected page, standard left-rail layout, compact top-selector layout, page-local overflow, and deterministic initial focus.

- [ ] **Step 1: Add failing tests for the standard and compact layouts**

Extend `SettingsMenuSceneTest` with a `SubViewport` fixture and these assertions after the screen enters the tree:

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

Add a page-selector test:

```csharp
[TestCase]
public async Task PageButtonShowsOnlyItsPage()
{
    await ResizeAndOpen(new Vector2I(1280, 720));

    var deck = _screen!.GetNode<TabContainer>("%PageDeck");
    _screen.GetNode<Button>("%ControlsPageButton").EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    AssertThat(deck.CurrentTab).IsEqual(3);
    AssertThat(_screen.GetNode<Button>("%ControlsPageButton").ButtonPressed).IsTrue();
    AssertThat(_screen.GetNode<Button>("%AudioPageButton").ButtonPressed).IsFalse();
}
```

- [ ] **Step 2: Run the layout tests and verify they fail before responsive code exists**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: the standard/compact column assertions and page-selection behavior fail.

- [ ] **Step 3: Add the minimal page-selection handlers**

In `SettingsMenuController`:

```csharp
private void BindPageSignals()
{
    _audioPageButton.Pressed += OnAudioPagePressed;
    _displayPageButton.Pressed += OnDisplayPagePressed;
    _gameplayPageButton.Pressed += OnGameplayPagePressed;
    _controlsPageButton.Pressed += OnControlsPagePressed;
}

private void OnAudioPagePressed() => SelectPage(0);
private void OnDisplayPagePressed() => SelectPage(1);
private void OnGameplayPagePressed() => SelectPage(2);
private void OnControlsPagePressed() => SelectPage(3);

private void SelectPage(int pageIndex)
{
    _pageDeck.CurrentTab = pageIndex;
    RefreshPageSelection();
}

private void RefreshPageSelection()
{
    for (var i = 0; i < _pageButtons.Length; i++)
        _pageButtons[i].ButtonPressed = i == _pageDeck.CurrentTab;
}
```

Call `BindPageSignals()` from `BindSignals()` and detach the same four handlers in `_ExitTree()`.

Do not create a separate page-router type.

- [ ] **Step 4: Add responsive reflow using only the existing Sirius breakpoint**

Implement:

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

    var pageHeight = compact
        ? Mathf.Clamp(size.Y - 240f, 120f, 260f)
        : Mathf.Clamp(size.Y - 260f, 320f, 520f);
    _pageDeck.CustomMinimumSize = new Vector2(0, pageHeight);
}
```

Keep the responsive behavior limited to container reflow. Do not create alternate standard/compact control trees.

- [ ] **Step 5: Add page-local scroll ownership and fit tests**

Add a parameterized smoke test over the approved sizes:

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

    var panel = _screen!.GetNode<PanelContainer>(
        "ModalShell/Panel");
    var panelRect = panel.GetGlobalRect();

    AssertThat(panelRect.Position.X).IsGreaterEqual(0f);
    AssertThat(panelRect.Position.Y).IsGreaterEqual(0f);
    AssertThat(panelRect.End.X).IsLessEqual(width + 0.5f);
    AssertThat(panelRect.End.Y).IsLessEqual(height + 0.5f);
}
```

At 640×360, set one row label to a representative long value and verify the panel still fits:

```csharp
var label = _screen!.GetNode<Label>("%InventoryKeyLabel");
label.Text = "Toggle Inventory With A Representative Localized Label";
await AwaitFrames(2);
```

Give the row labels unique names such as `%InventoryKeyLabel` in the scene so this test does not depend on child indices.

- [ ] **Step 6: Run scene/layout tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: standard, compact, page selection, long-label, and all viewport smoke tests pass.

- [ ] **Step 7: Run Settings behavior tests again**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: all existing Settings semantics still pass after responsive changes.

- [ ] **Step 8: Commit responsive Settings behavior**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuSceneTest.cs

git commit -m "feat(ui): make settings layout responsive"
```

---

### Task 3: Lock Settings focus and invocation behavior under Pause and Main Menu

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/ui/MainMenuTest.cs`

**Interfaces:**
- Consumes: `SettingsMenuController.InitialFocusTarget`, existing HPA-382 `TryOpenHostedSettings()`, existing `UIScreenEntrySpec.InitialFocus`, current direct Main Menu Settings child lifecycle.
- Produces: host-owned Settings initial focus under Pause and explicit focus restoration to the existing Main Menu Settings button.

- [ ] **Step 1: Add a failing gameplay-host focus assertion**

Extend `GameplayPauseHostTest.HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause()` after the Settings child is presented:

```csharp
var settings = FindDirectChild<SettingsMenuController>(modalLayer);
await AwaitFrames(1);

AssertThat(_viewport!.GuiGetFocusOwner())
    .IsEqual(settings.InitialFocusTarget);
```

Keep the existing assertions for parent handle, `ProcessPolicy.Always`, no second pause owner, inherited HUD, and return to the same Pause entry.

- [ ] **Step 2: Add a failing Main Menu focus-restoration test**

In `MainMenuTest`, open Settings through the existing button/method, wait for the child, close it through `OnCancelPressed`, then assert the Main Menu Settings button owns focus:

```csharp
var settingsButton = _mainMenu!.GetNode<Button>("VBoxContainer/SettingsButton");
settingsButton.EmitSignal(Button.SignalName.Pressed);
await AwaitFrames(2);

var settings = FindDirectChild<SettingsMenuController>(_mainMenu);
InvokePrivate(settings, "OnCancelPressed");
await AwaitFrames(2);

AssertThat(_mainMenu.GetNodeOrNull<SettingsMenuController>("SettingsMenuController"))
    .IsNull();
AssertThat(_mainMenu.GetViewport().GuiGetFocusOwner())
    .IsEqual(settingsButton);
```

Use the existing test helpers in `MainMenuTest` for private invocation and frame waiting rather than creating a second helper framework.

- [ ] **Step 3: Run the two focused integration suites and verify the new assertions fail**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: existing lifecycle assertions pass; the new explicit focus assertions fail until production code is updated.

- [ ] **Step 4: Give the existing HPA-382 hosted Settings entry its explicit initial-focus callback**

In `Game.TryOpenHostedSettings()`, add only:

```csharp
InitialFocus = () => settings.InitialFocusTarget,
```

to the existing `UIScreenEntrySpec`.

Do not change:

```text
Kind
Layer
InputPriority
ProcessPolicy
Parent
PauseTree
BlockGameplayInput
Cursor
Hud
LowerLayers
Cancel
InterceptCancel
Cleanup
NodeLifetime
```

Do not add another `Game._Input()` branch.

- [ ] **Step 5: Restore Main Menu focus without adding the HPA-380 host**

Add a cached reference in `MainMenu`:

```csharp
private Button? _settingsButton;
```

Bind it in `_Ready()`:

```csharp
_settingsButton = GetNodeOrNull<Button>("VBoxContainer/SettingsButton");
```

After `OnSettingsClosed()` performs the existing unsubscribe/queue-free/null cleanup, restore focus:

```csharp
if (_settingsButton != null && IsInstanceValid(_settingsButton))
    _settingsButton.GrabFocus();
```

Do not add `UIScreenHost`, Continue behavior, new Main Menu layout, or generic messages in this task.

- [ ] **Step 6: Re-run gameplay-host and Main Menu integration tests**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: hosted Settings owns the expected initial focus; closing returns to Pause or Main Menu correctly; existing host cleanup still passes.

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
- Consumes: the scene-authored Settings screen, controller parity, responsive layout, gameplay host integration, and Main Menu focus restoration.
- Produces: a clean HPA-383 implementation branch with no legacy runtime Settings layout builder and no scope creep.

- [ ] **Step 1: Run the complete focused HPA-383 suite**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: all focused tests pass.

- [ ] **Step 2: Prove the runtime Settings builder is gone**

Run:

```bash
rg -n \
  "BuildUI|BuildAudioTab|BuildDisplayTab|BuildGameplayTab|BuildControlsTab|AddSliderRow|AddKeyRow" \
  scripts/ui/SettingsMenuController.cs \
  scenes/ui/SettingsMenu.tscn \
  tests/ui
```

Expected: no production matches for the removed builder methods. Reflection-test string literals in `SettingsMenuSceneTest.cs` are the only acceptable test matches.

- [ ] **Step 3: Prove HPA-383 did not modify domain or shared-framework ownership**

Run:

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

- [ ] **Step 4: Build the solution**

Run:

```bash
dotnet build Sirius.sln --no-restore
```

Expected: build succeeds with zero errors.

- [ ] **Step 5: Run the full test suite**

Run:

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Existing repository warning noise is acceptable only if unchanged from `main`.

- [ ] **Step 6: Inspect the final diff for accidental architecture expansion**

Run:

```bash
git diff --check
git diff --stat main...HEAD
```

Reject and remove any of the following if they appeared without a failing acceptance test that required them:

```text
new Settings view model/presenter
new generic settings-row component hierarchy
new navigation/history service
new Main Menu UIScreenHost work
new generic error/confirmation service
new persisted setting
new Theme token or component-shell API
```

- [ ] **Step 7: Commit any verification-only correction if one was required**

If verification required a scoped HPA-383 fix, use `git status --short` and stage only corrected paths that appear in the expected HPA-383 path list from Step 3. Commit them with:

```bash
git commit -m "fix(ui): preserve settings layout parity"
```

If no correction was required, do not create an empty commit.

---

## Implementation completion definition

The implementation is ready for review only when all of the following are true:

- `SettingsMenu.tscn` owns the player-facing control tree before `_Ready()`.
- `SettingsMenuController` contains no runtime UI-construction helpers.
- Existing settings semantics remain covered and passing.
- Standard layout uses a left page rail.
- Compact layout uses a top page selector and page-local scrolling.
- Apply/Cancel remain fixed outside page scrolling.
- Inline validation uses the existing Sirius error Theme.
- Hosted Settings remains a child of Pause without taking a second pause/input authority.
- Hosted Settings initial focus is explicit and host-restored.
- Direct Main Menu Settings returns focus to its existing Settings button.
- No HPA-380, HPA-541, HPA-572, save-domain, settings-domain, Theme-core, modal-shell-core, or `UIScreenHost` scope is implemented early.
- Focused tests, full tests, build, `git diff --check`, and scope audit pass.
