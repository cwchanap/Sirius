# HPA-383 Scene-Authored Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Sirius's runtime-built Settings layout with a scene-authored, themed, responsive screen while preserving all existing settings behavior and the HPA-382 gameplay-host lifecycle.

**Architecture:** Keep `SettingsMenuController` as the single screen controller and `SettingsManager`/`SettingsData` as domain owners. Move static controls/layout to `SettingsMenu.tscn`, reuse `SiriusModalShell` for width/theme/composition, give Settings itself an explicit viewport-bounded panel height, and let page-local `ScrollContainer`s own overflow. Preserve the direct Main Menu invocation until HPA-380 adds the Main Menu host.

**Tech Stack:** Godot 4.6.2, C#/.NET 8, GdUnit4, existing `SiriusModalShell`, Sirius Theme, and `UIScreenHost`.

## Global Constraints

- Primary target is desktop landscape with mouse, keyboard, and gamepad.
- Minimum supported logical resolution is 640×360.
- Compact reflow is `SiriusUiMetrics.IsCompact(viewportSize)`; do not invent another breakpoint.
- `SiriusUiMetrics.VerificationViewports` is the authoritative all-viewport list.
- `SiriusUiMetrics.FocusVerificationViewports` is the authoritative detailed 640×360 / 1280×720 list.
- Preserve Master, Music, and SFX volume; fullscreen; resolution; difficulty; autosave; Toggle Inventory, Interact, and Pause/Cancel bindings.
- Preserve staged edits until Apply; Cancel discards staged edits.
- Preserve custom/non-preset resolutions.
- Preserve reserved-key and duplicate-key validation.
- Preserve key-capture and `OptionButton` popup Cancel priority.
- `SettingsManager` remains the application/persistence owner.
- The gameplay `UIScreenHost` remains the hosted presentation-lifecycle owner under Pause.
- `SiriusModalShell` remains a responsive-width component per HPA-377; do not add a shared height API in HPA-383.
- HPA-380 owns the Main Menu `UIScreenHost`; do not add it here.
- HPA-572 owns generic host-managed confirmations/warnings/errors; use themed inline Settings validation here.
- HPA-541 owns Reduced Motion; do not add new settings here.
- Do not create a Settings view model, presenter, generic settings-row family, navigation framework, or another modal shell.
- Do not modify `SettingsData`, `SettingsManager`, Sirius Theme resources, `SiriusModalShell`, or `scripts/ui/hosting/*` unless a failing HPA-383 acceptance test proves the design impossible; reassess scope before doing so.

---

## File map

**Production**
- Modify `scenes/ui/SettingsMenu.tscn` — canonical static Settings hierarchy, Sirius styling, row/control cells, page-local scrolling, fixed actions.
- Modify `scripts/ui/SettingsMenuController.cs` — node binding, dynamic values, existing staged behavior, page selection, Settings-specific panel height, responsive reflow, inline feedback.
- Modify `scripts/game/Game.cs` — add Settings `InitialFocus`; remove the redundant second hosted `OpenSettings()` call only.
- Modify `scripts/ui/MainMenu.cs` — apply direct Settings initial focus and restore the existing Settings button on close.

**Tests**
- Create `tests/ui/SettingsMenuSceneTest.cs` — pre-`_Ready()` authorship, responsive/perceptual layout, scrolling, long-label, viewport-fit coverage.
- Modify `tests/ui/SettingsMenuControllerTest.cs` — preserve behavior coverage and replace obsolete generic-panel/focus assumptions.
- Modify `tests/game/GameplayPauseHostTest.cs` — hosted initial focus and existing Cancel/cleanup behavior.
- Modify `tests/ui/MainMenuTest.cs` — direct initial focus and close restoration using the helpers that file already owns.

---

## Task 1: Cut Settings over to a scene-authored control tree

**Files:**
- Create: `tests/ui/SettingsMenuSceneTest.cs`
- Modify: `scenes/ui/SettingsMenu.tscn`
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `tests/ui/SettingsMenuControllerTest.cs`

**Interfaces:**
- Consumes: existing `SiriusModalShell`, Theme variations, `SettingsData`, `SettingsManager`, `OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)`.
- Produces: stable unique nodes, `InitialFocusTarget`, existing Settings behavior without runtime layout builders, and reusable test fixture helpers for Task 2.

- [ ] **Step 1: Create the scene-test fixture and failing authorship contract**

Create `tests/ui/SettingsMenuSceneTest.cs` with the runtime fixture up front so later task snippets compile literally:

```csharp
using System.Reflection;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public partial class SettingsMenuSceneTest : Node
{
    private SceneTree _sceneTree = null!;
    private SubViewport _viewport = null!;
    private SettingsMenuController _screen = null!;

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

    [BeforeTest]
    public async Task Setup()
    {
        _sceneTree = (SceneTree)Engine.GetMainLoop();
        _viewport = new SubViewport
        {
            Disable3D = true,
            HandleInputLocally = true,
            Size = new Vector2I(1280, 720)
        };
        _sceneTree.Root.AddChild(_viewport);

        var packed = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn")
            ?? throw new System.InvalidOperationException("SettingsMenu.tscn did not load.");
        _screen = packed.Instantiate<SettingsMenuController>();
        _viewport.AddChild(_screen);
        await AwaitFrames(2);
    }

    [AfterTest]
    public async Task Cleanup()
    {
        if (GodotObject.IsInstanceValid(_screen))
            _screen.Free();
        if (GodotObject.IsInstanceValid(_viewport))
            _viewport.Free();
        await AwaitFrames(1);
    }

    private async Task ResizeAndOpen(Vector2I size)
    {
        _viewport.Size = size;
        await AwaitFrames(1);
        _screen.OpenSettings(SettingsData.CreateDefaults());
        await AwaitFrames(2);
    }

    private async Task AwaitFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    }

    [TestCase]
    public void PackedSceneOwnsControlsBeforeReady()
    {
        var packed = GD.Load<PackedScene>("res://scenes/ui/SettingsMenu.tscn");
        AssertThat(packed).IsNotNull();

        var detached = packed!.Instantiate<SettingsMenuController>();
        try
        {
            foreach (var path in RequiredUniqueNodes)
                AssertThat(detached.GetNodeOrNull(path)).IsNotNull();
        }
        finally
        {
            detached.Free();
        }
    }

    [TestCase]
    public void ControllerHasNoRuntimeLayoutBuilders()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (var name in new[]
        {
            "BuildUI",
            "BuildAudioTab",
            "BuildDisplayTab",
            "BuildGameplayTab",
            "BuildControlsTab",
            "AddSliderRow",
            "AddKeyRow"
        })
        {
            AssertThat(typeof(SettingsMenuController).GetMethod(name, flags)).IsNull();
        }
    }
}
```

- [ ] **Step 2: Run the new suite and verify the authorship tests fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest"
```

Expected: authored-node test fails because the skeleton scene lacks the nodes; builder-removal test fails because the old methods still exist.

- [ ] **Step 3: Author the concrete Settings scene**

Replace the current skeleton with:

```text
SettingsMenuController
├── Background (Panel, SiriusScrim)
└── ModalShell (SiriusModalShell, title="Settings", SizeClass=Large)
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

Author one `ButtonGroup` shared by the four page buttons. Each page button has `toggle_mode = true`; Audio starts pressed. `PageDeck` starts on index `0` and hides its built-in tab bar.

Set every interactive control's scene minimum height to at least `44` logical pixels for the standard authored state.

Author rows as:

```text
MasterVolumeLabel | MasterControlCell(MasterSlider + MasterValueLabel)
MusicVolumeLabel  | MusicControlCell(MusicSlider + MusicValueLabel)
SfxVolumeLabel    | SfxControlCell(SfxSlider + SfxValueLabel)
FullscreenLabel   | FullscreenCheck
ResolutionLabel   | ResolutionOption
DifficultyLabel   | DifficultyOption
AutoSaveLabel     | AutoSaveCheck
InventoryKeyLabel | InventoryKeyButton
InteractKeyLabel  | InteractKeyButton
PauseKeyLabel     | PauseKeyButton
```

Every row label gets `autowrap_mode = TextServer.AutowrapMode.WordSmart` and the exact unique name from Step 1.

Keep `ResolutionOption` and `DifficultyOption` empty; the controller continues to populate those values.

- [ ] **Step 4: Bind scene nodes and disable the shell scroll owner**

Delete the runtime builder family from `SettingsMenuController`.

Add the structure fields:

```csharp
private SiriusModalShell _shell = null!;
private PanelContainer _modalPanel = null!;
private ScrollContainer _shellBodyScroll = null!;
private GridContainer _settingsFrame = null!;
private GridContainer _pageSelector = null!;
private TabContainer _pageDeck = null!;
private GridContainer _audioRows = null!;
private GridContainer _displayRows = null!;
private GridContainer _gameplayRows = null!;
private GridContainer _controlsRows = null!;
private ScrollContainer _audioScroll = null!;
private ScrollContainer _displayScroll = null!;
private ScrollContainer _gameplayScroll = null!;
private ScrollContainer _controlsScroll = null!;
private Button _audioPageButton = null!;
private Button _displayPageButton = null!;
private Button _gameplayPageButton = null!;
private Button _controlsPageButton = null!;
private PanelContainer _errorPanel = null!;
private Button _applyButton = null!;
private Button _cancelButton = null!;
```

Bind the shell internals through the shell instance, not from the Settings root:

```csharp
_shell = GetNode<SiriusModalShell>("%ModalShell");
_modalPanel = _shell.GetNode<PanelContainer>("%Panel");
_shellBodyScroll = _shell.GetNode<ScrollContainer>("%BodyScroll");
_shellBodyScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
_shellBodyScroll.VerticalScrollMode = ScrollContainer.ScrollMode.Disabled;
```

Bind the rest through their authored unique names. Keep the existing behavior field names (`_masterSlider`, `_resolutionOption`, `_inventoryKeyBtn`, etc.) so behavior tests do not need a state-model rewrite.

Do **not** change `SiriusModalShell.cs`; HPA-377 defines it as a responsive-width component.

- [ ] **Step 5: Populate dynamic choices and wire named behavior handlers**

Add:

```csharp
private void PopulateChoiceItems()
{
    _resolutionOption.Clear();
    foreach (var (w, h) in ResolutionPresets)
        _resolutionOption.AddItem($"{w}×{h}");

    _difficultyOption.Clear();
    foreach (var difficulty in Difficulties)
        _difficultyOption.AddItem(difficulty);
}
```

Use named handlers for sliders, key buttons, Apply, Cancel, page buttons, and `Resized`, and detach every one in `_ExitTree()`.

Thin examples:

```csharp
private void OnMasterVolumeChanged(double value) =>
    _masterValueLabel.Text = $"{(int)value}%";

private void OnInventoryKeyPressed() => StartKeyCapture("toggle_inventory");
private void OnApplyButtonPressed() => OnApplyPressed();
private void OnCancelButtonPressed() => OnCancelPressed();
```

Do not alter staged settings, resolution preservation, duplicate/reserved validation, Apply, Cancel, or input-priority rules.

- [ ] **Step 6: Move validation into the authored error surface**

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

Call `ClearError()` on open, when capture starts, after successful capture, and when capture is canceled.

Keep `ErrorPanel` as `PanelContainer` with the existing `SiriusErrorPanel` variation; do not add a wrapper or Theme type.

- [ ] **Step 7: Preserve the public open/close contract without applying focus inside the screen**

Keep:

```csharp
public void OpenSettings(SettingsData? snapshot = null, bool showOverlay = true)
```

Its end state becomes:

```csharp
_editedSettings = source.Clone();
PopulateControls();
Show();
SetProcessInput(true);
```

It must **not** call `GrabFocus()`. Task 3 assigns focus at the owning invocation boundary.

Expose:

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

- [ ] **Step 8: Update controller regression tests for the new ownership boundary**

Keep all existing semantic tests.

Replace `OpenSettings_GrabsFocusOnFirstControl` with:

```csharp
[TestCase]
public void DefaultInitialFocusTarget_IsMasterSlider()
{
    _ctrl.OpenSettings(SettingsData.CreateDefaults());
    var master = GetField<HSlider>(_ctrl, "_masterSlider");
    AssertThat(_ctrl.InitialFocusTarget).IsEqual(master);
}
```

Delete `OpenSettings_InGameMode_PanelSizeClampedToViewport`; Task 2 owns panel fit with the real shell panel.

- [ ] **Step 9: Run Settings tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: all tests pass.

- [ ] **Step 10: Commit the authored cutover**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuControllerTest.cs \
  tests/ui/SettingsMenuSceneTest.cs
git commit -m "feat(ui): scene-author settings screen"
```

---

## Task 2: Add responsive reflow, real panel height, and perceptual layout tests

**Files:**
- Modify: `scripts/ui/SettingsMenuController.cs`
- Modify: `tests/ui/SettingsMenuSceneTest.cs`
- Modify: `scenes/ui/SettingsMenu.tscn` only if a failing layout test requires authored size flags/wrap settings.

**Interfaces:**
- Consumes: `SiriusUiMetrics.IsCompact`, `VerificationViewports`, `FocusVerificationViewports`, `SiriusModalShell.Compact`, `RefreshPresentation`, `_modalPanel`, page controls from Task 1.
- Produces: standard left rail, compact top selector, non-zero page viewport, page-local overflow, deterministic fit on X/Y.

- [ ] **Step 1: Add failing standard/compact and page-selection tests**

```csharp
[TestCase]
public async Task StandardViewportUsesLeftRailAndTwoColumnRows()
{
    await ResizeAndOpen(new Vector2I(1280, 720));

    AssertThat(_screen.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(2);
    AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(2);
}

[TestCase]
public async Task CompactViewportUsesTopSelectorAndOneColumnRows()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    AssertThat(_screen.GetNode<GridContainer>("%SettingsFrame").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%PageSelector").Columns).IsEqual(4);
    AssertThat(_screen.GetNode<GridContainer>("%AudioRows").Columns).IsEqual(1);
    AssertThat(_screen.GetNode<GridContainer>("%ControlsRows").Columns).IsEqual(1);
}

[TestCase]
public async Task PageButtonSelectsOnePage()
{
    await ResizeAndOpen(new Vector2I(1280, 720));

    var deck = _screen.GetNode<TabContainer>("%PageDeck");
    var audio = _screen.GetNode<Button>("%AudioPageButton");
    var controls = _screen.GetNode<Button>("%ControlsPageButton");

    controls.EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    AssertThat(deck.CurrentTab).IsEqual(3);
    AssertThat(controls.ButtonPressed).IsTrue();
    AssertThat(audio.ButtonPressed).IsFalse();
}
```

- [ ] **Step 2: Add the perceptual tests that fail a collapsed page**

Use the authoritative focus-verification array instead of hard-coding the two sizes:

```csharp
[TestCase]
public async Task DetailedViewportsKeepMasterSliderVisibleInsideModal()
{
    foreach (var size in SiriusUiMetrics.FocusVerificationViewports)
    {
        await ResizeAndOpen(size);

        var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");
        var slider = _screen.GetNode<HSlider>("%MasterSlider");
        var panelRect = panel.GetGlobalRect();
        var sliderRect = slider.GetGlobalRect();

        AssertThat(slider.Size.Y).IsGreater(0f);
        AssertThat(panelRect.Encloses(sliderRect)).IsTrue();
    }
}
```

This is the load-bearing test: correct `Columns` values are insufficient if the page viewport is zero-height.

- [ ] **Step 3: Implement one page-selection owner**

Use the authored `ButtonGroup` for exclusivity and named handlers only for choosing the `TabContainer` page:

```csharp
private void OnAudioPagePressed() => SelectPage(0, _audioPageButton);
private void OnDisplayPagePressed() => SelectPage(1, _displayPageButton);
private void OnGameplayPagePressed() => SelectPage(2, _gameplayPageButton);
private void OnControlsPagePressed() => SelectPage(3, _controlsPageButton);

private void SelectPage(int index, Button selected)
{
    _pageDeck.CurrentTab = index;
    selected.SetPressedNoSignal(true);
}
```

Do not add a second `RefreshPageSelection()` loop.

- [ ] **Step 4: Implement responsive reflow and the Settings-specific modal height**

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

    var panelHeight = compact
        ? size.Y - SiriusUiMetrics.SafeMargin(true) * 2f
        : size.Y * 0.90f;

    _modalPanel.CustomMinimumSize = new Vector2(
        _modalPanel.CustomMinimumSize.X,
        Mathf.Max(0f, panelHeight));
}
```

The ordering is intentional: `_shell.RefreshPresentation(size)` owns X and resets Y, then Settings restores only its screen-specific Y contract.

Do not add `SiriusModalShell` height state or page-specific height constants.

- [ ] **Step 5: Assert scroll ownership and real page viewport**

```csharp
[TestCase]
public async Task SettingsUsesOnlyPageLocalVerticalScroll()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    var shell = _screen.GetNode<SiriusModalShell>("%ModalShell");
    var outer = shell.GetNode<ScrollContainer>("%BodyScroll");
    var controls = _screen.GetNode<ScrollContainer>("%ControlsScroll");

    AssertThat(outer.HorizontalScrollMode).IsEqual(ScrollContainer.ScrollMode.Disabled);
    AssertThat(outer.VerticalScrollMode).IsEqual(ScrollContainer.ScrollMode.Disabled);
    AssertThat(controls.VerticalScrollMode).IsNotEqual(ScrollContainer.ScrollMode.Disabled);
    AssertThat(controls.GetVScrollBar().Page).IsGreater(0f);
}
```

- [ ] **Step 6: Add all-viewport X/Y fit coverage from `SiriusUiMetrics`**

```csharp
[TestCase]
public async Task EveryApprovedViewportKeepsModalInsideViewport()
{
    foreach (var size in SiriusUiMetrics.VerificationViewports)
    {
        await ResizeAndOpen(size);

        var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");
        var rect = panel.GetGlobalRect();

        AssertThat(rect.Position.X).IsGreaterEqual(-0.5f);
        AssertThat(rect.Position.Y).IsGreaterEqual(-0.5f);
        AssertThat(rect.End.X).IsLessEqual(size.X + 0.5f);
        AssertThat(rect.End.Y).IsLessEqual(size.Y + 0.5f);
    }
}
```

No duplicated `[TestCase(640, 360)] ...` list is required.

- [ ] **Step 7: Add a long-label test that checks the axis that can actually blow out**

```csharp
[TestCase]
public async Task CompactLongControlsLabelWrapsAndKeepsUsableScrollViewport()
{
    await ResizeAndOpen(new Vector2I(640, 360));

    _screen.GetNode<Button>("%ControlsPageButton")
        .EmitSignal(Button.SignalName.Pressed);
    await AwaitFrames(1);

    var label = _screen.GetNode<Label>("%InventoryKeyLabel");
    label.Text = "Toggle Inventory With A Representative Localized Label That Must Wrap";
    await AwaitFrames(2);

    var panel = _screen.GetNode<PanelContainer>("ModalShell/Panel");
    var scroll = _screen.GetNode<ScrollContainer>("%ControlsScroll");
    var rect = panel.GetGlobalRect();
    var bar = scroll.GetVScrollBar();

    AssertThat(label.AutowrapMode).IsEqual(TextServer.AutowrapMode.WordSmart);
    AssertThat(rect.Position.X).IsGreaterEqual(-0.5f);
    AssertThat(rect.End.X).IsLessEqual(640.5f);
    AssertThat(rect.Position.Y).IsGreaterEqual(-0.5f);
    AssertThat(rect.End.Y).IsLessEqual(360.5f);
    AssertThat(bar.Page).IsGreater(0f);
    AssertThat(bar.MaxValue).IsGreater(bar.Page);
}
```

- [ ] **Step 8: Run layout and behavior suites**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest"
```

Expected: authored, standard/compact, perceptual visibility, scroll ownership, long-label, all-viewport fit, and existing Settings behavior all pass.

- [ ] **Step 9: Commit responsive layout**

```bash
git add \
  scenes/ui/SettingsMenu.tscn \
  scripts/ui/SettingsMenuController.cs \
  tests/ui/SettingsMenuSceneTest.cs
git commit -m "feat(ui): make settings layout responsive"
```

---

## Task 3: Lock focus at the Pause and Main Menu invocation boundaries

**Files:**
- Modify: `scripts/game/Game.cs`
- Modify: `scripts/ui/MainMenu.cs`
- Modify: `tests/game/GameplayPauseHostTest.cs`
- Modify: `tests/ui/MainMenuTest.cs`

**Interfaces:**
- Consumes: `SettingsMenuController.InitialFocusTarget`, current HPA-382 hosted Settings flow, current Main Menu helpers/fields.
- Produces: one focus owner per invocation, Pause restoration, Main Menu restoration, no duplicate hosted open.

- [ ] **Step 1: Add hosted Settings initial-focus assertion**

Extend the existing `GameplayPauseHostTest.HostedSettings_HostsLogicalPauseChildAndRestoresExistingPause()`:

```csharp
var settings = FindDirectChild<SettingsMenuController>(modalLayer);
await AwaitFrames(2);

AssertThat(_viewport!.GuiGetFocusOwner())
    .IsEqual(settings.InitialFocusTarget);
```

Keep the existing parent/pause/process/cleanup assertions intact.

- [ ] **Step 2: Add Main Menu direct-focus and restoration coverage using the file's real helpers**

Do **not** introduce `_mainMenu`, `FindDirectChild`, `AwaitFrames`, or `InvokePrivate`; `MainMenuTest` does not own those helpers.

Add an async test using `_menu`, `InvokePrivateAcrossHierarchy`, `GetPrivateField`, and `ToSignal`:

```csharp
[TestCase]
public async Task SettingsOpenAndClose_AppliesInitialFocusAndRestoresSettingsButton()
{
    var settingsButton = _menu.GetNode<Button>("VBoxContainer/SettingsButton");

    InvokePrivateAcrossHierarchy(_menu, "_on_settings_button_pressed");
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

    var settings = GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu");
    AssertThat(settings).IsNotNull();
    AssertThat(_menu.GetViewport().GuiGetFocusOwner())
        .IsEqual(settings!.InitialFocusTarget);

    InvokePrivateAcrossHierarchy(settings, "OnCancelPressed");
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);
    await ToSignal(_sceneTree, SceneTree.SignalName.ProcessFrame);

    AssertThat(GetPrivateField<SettingsMenuController?>(_menu, "_settingsMenu")).IsNull();
    AssertThat(_menu.GetViewport().GuiGetFocusOwner()).IsEqual(settingsButton);
}
```

- [ ] **Step 3: Run the integration suites and verify the new assertions fail**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: existing lifecycle assertions pass; new focus assertions fail until production seams are updated.

- [ ] **Step 4: Add only the host initial-focus callback and remove the duplicate hosted open**

In `Game.TryOpenHostedSettings()` add:

```csharp
InitialFocus = () => settings.InitialFocusTarget,
```

Keep the existing `SetPresented` callback as the only hosted `OpenSettings(showOverlay: false)` call.

Delete the redundant call after successful `TryPresent`:

```csharp
settings.OpenSettings(showOverlay: false);
```

Do not change the other `UIScreenEntrySpec` policy values and do not add another `Game._Input()` branch.

- [ ] **Step 5: Give direct Main Menu invocation explicit focus and restoration**

Cache the existing Settings button:

```csharp
private Button? _settingsButton;
```

Bind in `_Ready()`:

```csharp
_settingsButton = GetNodeOrNull<Button>("VBoxContainer/SettingsButton");
```

After direct open:

```csharp
_settingsMenu.OpenSettings();
_settingsMenu.InitialFocusTarget.GrabFocus();
```

After existing close cleanup:

```csharp
if (_settingsButton != null && IsInstanceValid(_settingsButton))
    _settingsButton.GrabFocus();
```

Do not add HPA-380's Main Menu host or redesign.

- [ ] **Step 6: Re-run integration tests**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: hosted/direct initial focus and parent restoration pass with the existing lifecycle assertions.

- [ ] **Step 7: Commit invocation integration**

```bash
git add \
  scripts/game/Game.cs \
  scripts/ui/MainMenu.cs \
  tests/game/GameplayPauseHostTest.cs \
  tests/ui/MainMenuTest.cs
git commit -m "fix(ui): restore settings focus across parents"
```

---

## Task 4: Final parity, regression, and scope verification

**Files:**
- Modify only Tasks 1–3 files if a failing verification proves a required HPA-383 correction.

**Interfaces:**
- Consumes: authored Settings scene, responsive panel/page contract, behavior parity, hosted/direct focus seams.
- Produces: review-ready HPA-383 implementation with no legacy builder and no scope creep.

- [ ] **Step 1: Run the complete focused HPA-383 suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local \
  --filter "FullyQualifiedName~SettingsMenuSceneTest|FullyQualifiedName~SettingsMenuControllerTest|FullyQualifiedName~GameplayPauseHostTest|FullyQualifiedName~MainMenuTest"
```

Expected: zero failures.

- [ ] **Step 2: Prove the runtime builder is gone**

```bash
rg -n \
  "BuildUI|BuildAudioTab|BuildDisplayTab|BuildGameplayTab|BuildControlsTab|AddSliderRow|AddKeyRow" \
  scripts/ui/SettingsMenuController.cs \
  scenes/ui/SettingsMenu.tscn \
  tests/ui
```

Expected: no production matches; reflection-test string literals are the only acceptable test matches.

- [ ] **Step 3: Prove no speculative sizing/framework layer appeared**

```bash
rg -n \
  "pageHeight|240f|260f|SettingsViewModel|SettingsPresenter|SettingsRow|NavigationHistory|FillAvailableHeight" \
  scripts scenes tests
```

Expected: no HPA-383 implementation matches for these rejected patterns.

- [ ] **Step 4: Audit changed paths**

```bash
git diff --name-only main...HEAD
```

Expected runtime/test paths are limited to:

```text
scenes/ui/SettingsMenu.tscn
scripts/ui/SettingsMenuController.cs
scripts/game/Game.cs
scripts/ui/MainMenu.cs
tests/ui/SettingsMenuControllerTest.cs
tests/ui/SettingsMenuSceneTest.cs
tests/game/GameplayPauseHostTest.cs
tests/ui/MainMenuTest.cs
```

Planning docs are also expected:

```text
docs/superpowers/specs/2026-08-08-hpa-383-scene-authored-settings-design.md
docs/superpowers/plans/2026-08-08-hpa-383-scene-authored-settings.md
```

Specifically confirm no changes under:

```text
scripts/settings/
resources/ui/theme/
scripts/ui/components/SiriusModalShell.cs
scenes/ui/components/SiriusModalShell.tscn
scripts/ui/hosting/
```

- [ ] **Step 5: Build**

```bash
dotnet build Sirius.sln --no-restore
```

Expected: zero errors.

- [ ] **Step 6: Run the full suite**

```bash
dotnet test Sirius.sln --settings test.runsettings.local --no-restore
```

Expected: zero failed tests. Existing warning noise is acceptable only if unchanged from `main`.

- [ ] **Step 7: Final diff hygiene**

```bash
git diff --check
git diff --stat main...HEAD
```

Reject any unproven addition of:

```text
new Settings view model/presenter
new generic settings-row hierarchy
new navigation/history service
new Main Menu UIScreenHost work
new generic error/confirmation service
new persisted setting
new Theme token
new SiriusModalShell API/height policy
```

- [ ] **Step 8: Commit a verification correction only if required**

If verification forced a scoped correction:

```bash
git status --short
git add <only approved HPA-383 paths>
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
- Compact layout uses a top page selector.
- Settings has an explicit viewport-bounded modal height; the active page does not collapse to zero visible height.
- Apply/Cancel remain fixed outside page scrolling.
- Shell scrolling is disabled for Settings; page scroll containers exclusively own overflow.
- `MasterSlider` has non-zero visible size and is enclosed by `ModalShell/Panel` at both focus-verification viewports.
- Long Controls text wraps and cannot push the modal outside 640×360 on either axis.
- Inline validation uses the existing Sirius error Theme.
- Hosted Settings remains a child of Pause without taking a second pause/input authority.
- Hosted focus is applied by `UIScreenHost`; direct focus is applied by Main Menu; `OpenSettings()` applies neither.
- Direct Main Menu Settings returns focus to its existing Settings button.
- No HPA-380, HPA-541, HPA-572, save-domain, settings-domain, Theme-core, modal-shell-core, or host-framework scope is implemented early.
- Focused tests, full tests, build, `git diff --check`, and scope audit pass.
